using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

using Steamworks;

namespace UNExT
{
    public class SteamNetManager : NetworkSyncManager
    {
        private int connectionIdCounter;

        private static uint BufferLength = 1200;

        private static byte[] netBuffer = new byte[BufferLength];

        private CSteamID remoteSteamId = CSteamID.Nil;
        private CSteamID steamLobbyId = CSteamID.Nil;

        internal Dictionary<CSteamID, NetworkConnection> steamIdToConnection;

        private Coroutine steamNetworkingLoopCoroutine;
        private Coroutine pingSenderCoroutine;

        private Callback<P2PSessionRequest_t> sessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> sessionErrorCallback;
        private Callback<LobbyChatUpdate_t> steamLobbyChatUpdate;

        private HostTopology steamHostTopology;


        private bool isClientConnected;

        public static new SteamNetManager singleton
        {
            get
            {
                return NetworkManager.singleton as SteamNetManager;
            }
        }

        public int connectedCount
        {
            get
            {
                return steamIdToConnection.Count;
            }
        }

        private HostTopology GetSteamHostTopology()
        {
            if (steamHostTopology != null)
            {
                return steamHostTopology;
            }

            ConnectionConfig config;
            if (customConfig && connectionConfig != null)
            {
                config = connectionConfig;
                config.Channels.Clear();
                for (int i = 0; i < channels.Count; i++)
                {
                    config.AddChannel(channels[i]);
                }
            }
            else
            {
                config = new ConnectionConfig();

                config.AddChannel(QosType.ReliableSequenced);
                config.AddChannel(QosType.Unreliable);
            }

            config.UsePlatformSpecificProtocols = false;
            config.PacketSize = 1200;

            steamHostTopology = new HostTopology(config, maxConnections);

            return steamHostTopology;
        }

        public bool StartSteamHost(CSteamID steamLobbyId)
        {
            if (steamNetworkingLoopCoroutine != null)
            {
                return false;
            }

            if (SteamUser.GetSteamID() != SteamMatchmaking.GetLobbyOwner(steamLobbyId))
            {
                Debug.LogError("Cannot be a server if you are not the lobby owner");
                return false;
            }

            this.steamLobbyId = steamLobbyId;

            steamIdToConnection = new Dictionary<CSteamID, NetworkConnection>(maxConnections);

            connectionIdCounter = 0;

            NetworkServer.dontListen = true;
            StartHost(GetSteamHostTopology().DefaultConfig, steamHostTopology.MaxDefaultConnections);

            steamIdToConnection.Add(SteamUser.GetSteamID(), NetworkServer.localConnections[0]);

            sessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnSessionRequest);
            sessionErrorCallback = Callback<P2PSessionConnectFail_t>.Create(OnSessionConnectFail);
            steamLobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

            steamNetworkingLoopCoroutine = StartCoroutine(CoSteamNetworkingLoop());
            pingSenderCoroutine = StartCoroutine(CoPingSender());

            return true;
        }

        public void StopSteamHost()
        {
            if (sessionRequestCallback != null && steamNetworkingLoopCoroutine != null)
            {
                StopCoroutine(steamNetworkingLoopCoroutine);
                steamNetworkingLoopCoroutine = null;

                StopCoroutine(pingSenderCoroutine);
                pingSenderCoroutine = null;

                sessionRequestCallback.Dispose();
                sessionRequestCallback = null;

                sessionErrorCallback.Dispose();
                sessionErrorCallback = null;

                steamLobbyChatUpdate.Dispose();
                steamLobbyChatUpdate = null;

                steamIdToConnection = null;

                SteamMatchmaking.LeaveLobby(steamLobbyId);
                steamLobbyId = CSteamID.Nil;
            }

            if (NetworkServer.active)
            {
                StopHost();
            }
        }

        private void OnSessionRequest(P2PSessionRequest_t request)
        {
            CSteamID clientId = request.m_steamIDRemote;

            if (steamIdToConnection.ContainsKey(clientId))
            {
                SteamNetworking.AcceptP2PSessionWithUser(clientId);
            }
            else
            {
                if (steamIdToConnection.Count < maxConnections - 1)
                {
                    if (ShouldAcceptSteamId(clientId))
                    {
                        if (SteamNetworking.AcceptP2PSessionWithUser(clientId))
                        {
                            if (SteamNetworking.SendP2PPacket(clientId, null, 0, EP2PSend.k_EP2PSendReliable))
                            {
                                var conn = new SteamNetConnection(clientId);
                                ForceInitConnection(conn);

                                steamIdToConnection.Add(clientId, conn);

                                NetworkServer.AddExternalConnection(conn);
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("Steam session request ignored");
                    }
                }
            }
        }

        public bool StartSteamClient(CSteamID steamLobbyId)
        {
            if (steamNetworkingLoopCoroutine != null)
            {
                return false;
            }

            remoteSteamId = SteamMatchmaking.GetLobbyOwner(steamLobbyId);

            if (remoteSteamId == CSteamID.Nil)
            {
                Debug.LogError("Cannot determine the lobby owner, did you join this lobby?");
                return false;
            }

            this.steamLobbyId = steamLobbyId;

            connectionIdCounter = -1;

            isClientConnected = false;

            var conn = new SteamNetConnection(remoteSteamId);
            var extClient = new NetworkClient(conn);
            extClient.Configure(GetSteamHostTopology());

            ForceInitConnection(conn);

            UseExternalClient(extClient);

            sessionErrorCallback = Callback<P2PSessionConnectFail_t>.Create(OnSessionConnectFail);
            steamLobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

            SteamNetworking.SendP2PPacket(remoteSteamId, null, 0, EP2PSend.k_EP2PSendReliable);

            steamNetworkingLoopCoroutine = StartCoroutine(CoSteamNetworkingLoop());

            return true;
        }

        public void StopSteamClient()
        {
            if (remoteSteamId != CSteamID.Nil && steamNetworkingLoopCoroutine != null)
            {
                StopCoroutine(steamNetworkingLoopCoroutine);
                steamNetworkingLoopCoroutine = null;

                sessionErrorCallback.Dispose();
                sessionErrorCallback = null;

                steamLobbyChatUpdate.Dispose();
                steamLobbyChatUpdate = null;

                remoteSteamId = CSteamID.Nil;

                isClientConnected = false;

                SteamMatchmaking.LeaveLobby(steamLobbyId);
                steamLobbyId = CSteamID.Nil;
            }

            if (client != null)
            {
                StopClient();
            }
        }

        public override void OnClientDisconnect(NetworkConnection conn)
        {
            if (remoteSteamId != CSteamID.Nil)
            {
                StopSteamClient();
            }

            base.OnClientDisconnect(conn);
        }

        private void OnSessionConnectFail(P2PSessionConnectFail_t fail)
        {
            if (NetworkServer.active)
            {
                HandleSteamServerDisconnect(fail.m_steamIDRemote, NetworkError.Timeout);
            }
            else
            {
                HandleSteamClientDisconnect(NetworkError.Timeout);
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t update)
        {
            var state = (EChatMemberStateChange)update.m_rgfChatMemberStateChange;
            if (state == EChatMemberStateChange.k_EChatMemberStateChangeLeft || state == EChatMemberStateChange.k_EChatMemberStateChangeDisconnected)
            {
                var changedUserId = (CSteamID)update.m_ulSteamIDUserChanged;
                if (NetworkServer.active)
                {
                    HandleSteamServerDisconnect(changedUserId);
                }
                else if (remoteSteamId == changedUserId)
                {
                    HandleSteamClientDisconnect();
                }
            }
        }

        private void HandleSteamServerDisconnect(CSteamID id, NetworkError error = NetworkError.Ok)
        {
            try
            {
                var conn = steamIdToConnection[id] as SteamNetConnection;

                if (conn != null)
                {
                    if (error != NetworkError.Ok)
                    {
                        OnServerError(conn, (int)error);
                    }

                    conn.Disconnect();

                    NetworkServer.RemoveExternalConnection(conn.connectionId);

                    conn.InvokeHandlerNoData(MsgType.Disconnect);

                    conn.Dispose();
                }
            }
            catch (KeyNotFoundException) { }
        }

        private void HandleSteamClientDisconnect(NetworkError error = NetworkError.Ok)
        {
            var conn = client.connection;

            if (error != NetworkError.Ok)
            {
                OnClientError(conn, (int)error);
            }

            conn.Disconnect();

            if (isClientConnected)
            {
                conn.InvokeHandlerNoData(MsgType.Disconnect);
            }
            else
            {
                StopSteamClient();
            }
        }

        private IEnumerator CoSteamNetworkingLoop()
        {
            var waitMode = new WaitForEndOfFrame();

            while (true)
            {
                uint packetSize;
                CSteamID steamID;

                // Receive data packages (on channel 0)
                while (SteamNetworking.ReadP2PPacket(netBuffer, BufferLength, out packetSize, out steamID))
                {
                    if (packetSize == 0)
                    {
                        if (!isClientConnected && steamID == remoteSteamId)
                        {
                            isClientConnected = true;
                            client.connection.InvokeHandlerNoData(MsgType.Connect);
                        }
                    }
                    else
                    {
                        if (steamID == remoteSteamId)
                        {
                            client.connection.TransportReceive(netBuffer, (int)packetSize, 0);
                        }
                        else if (NetworkServer.active)
                        {
                            try
                            {
                                steamIdToConnection[steamID].TransportReceive(netBuffer, (int)packetSize, 0);
                            }
                            catch (KeyNotFoundException) { }
                        }
                    }
                }


                // Receive ping packages (on channel 1)
                var time = Time.realtimeSinceStartup;

                while (SteamNetworking.ReadP2PPacket(netBuffer, BufferLength, out packetSize, out steamID, 1))
                {
                    if (packetSize == sizeof(float))
                    {
                        if (steamID == remoteSteamId)
                        {
                            SteamNetworking.SendP2PPacket(steamID, netBuffer, sizeof(float), EP2PSend.k_EP2PSendUnreliableNoDelay, 1);
                        }
                        else if (NetworkServer.active)
                        {
                            int rtt = Mathf.RoundToInt((time - System.BitConverter.ToSingle(netBuffer, 0)) * 1000);

                            try
                            {
                                (steamIdToConnection[steamID] as SteamNetConnection).rtt = rtt;
                            }
                            catch (KeyNotFoundException) { }
                        }
                    }
                }

                if (isClientConnected)
                {
                    client.connection.FlushChannels();
                }

                yield return waitMode;
            }
        }

        private IEnumerator CoPingSender()
        {
            var localId = SteamUser.GetSteamID();
            while (true)
            {
                foreach (var entry in steamIdToConnection)
                {
                    if (entry.Key == localId)
                    {
                        continue;
                    }
                    SteamNetworking.SendP2PPacket(entry.Key, System.BitConverter.GetBytes(Time.realtimeSinceStartup), sizeof(float), EP2PSend.k_EP2PSendUnreliableNoDelay, 1);
                }

                yield return new WaitForSecondsRealtime(connectionConfig.PingTimeout / 1000.0f);
            }
        }

        private void ForceInitConnection(NetworkConnection conn)
        {
            //if (NetworkServer.active)
            //{
            //    //var connections = NetworkServer.connections;
            //    //int connCount = connections.Count;
            //    //int connId = 0;
            //    //for (; connId < connCount || connections[connId] != null; connId++) ;

            //    conn.Initialize("localhost", -1, ++connectionIdCounter, topo);
            //}
            //else
            //{
            conn.Initialize("localhost", -1, ++connectionIdCounter, GetSteamHostTopology());
            //}
        }

        public int GetCurrentRTT(CSteamID id)
        {
            if (SteamUser.GetSteamID() == id)
            {
                return 0;
            }

            try
            {
                return (steamIdToConnection[id] as SteamNetConnection).rtt;
            }
            catch (KeyNotFoundException)
            {
                return -1;
            }
        }

        public int GetCurrentRTT(NetworkConnection conn)
        {
            if (conn is SteamNetConnection)
            {
                return (conn as SteamNetConnection).Rtt;
            }
            else if (NetworkServer.localConnections[0] == conn)
            {
                return 0;
            }

            return -1;
        }

        public CSteamID GetSteamIdForConnection(NetworkConnection conn)
        {
            if (conn is SteamNetConnection)
            {
                return (conn as SteamNetConnection).remoteSteamId;
            }
            else if (NetworkServer.localConnections[0] == conn)
            {
                return SteamUser.GetSteamID();
            }

            return CSteamID.Nil;
        }


        public virtual bool ShouldAcceptSteamId(CSteamID id)
        {
            int count = SteamMatchmaking.GetNumLobbyMembers(steamLobbyId);

            for (int i = 0; i < count; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(steamLobbyId, i) == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
