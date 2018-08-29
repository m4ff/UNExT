using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

using Steamworks;

namespace UNExT
{
    internal class SteamNetConnection : NetworkConnection
    {
        internal CSteamID remoteSteamId = CSteamID.Nil;

        internal int rtt;

        public int Rtt { get { return rtt; } }

        public SteamNetConnection(CSteamID remote)
        {
            remoteSteamId = remote;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (remoteSteamId != CSteamID.Nil)
            {
                SteamNetworking.CloseP2PSessionWithUser(remoteSteamId);

                if(SteamNetManager.singleton.steamIdToConnection != null)
                {
                    SteamNetManager.singleton.steamIdToConnection.Remove(remoteSteamId);
                }

                remoteSteamId = CSteamID.Nil;
            }
        }

        public override bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
        {
            EP2PSend eP2PSendType = EP2PSend.k_EP2PSendReliable;

            var hostTopology = NetworkServer.active ? NetworkServer.hostTopology : NetworkManager.singleton.client.hostTopology;

            QosType qos = hostTopology.DefaultConfig.Channels[channelId].QOS;
            if (qos == QosType.Unreliable || qos == QosType.UnreliableFragmented || qos == QosType.UnreliableSequenced)
            {
                eP2PSendType = EP2PSend.k_EP2PSendUnreliableNoDelay;
            }

            if (SteamNetworking.SendP2PPacket(remoteSteamId, bytes, (uint)numBytes, eP2PSendType))
            {
                error = (byte)NetworkError.Ok;
                return true;
            }
            else
            {
                P2PSessionState_t state;
                if (SteamNetworking.GetP2PSessionState(remoteSteamId, out state))
                {
                    if (state.m_eP2PSessionError == (byte)EP2PSessionError.k_EP2PSessionErrorTimeout)
                    {
                        error = (byte)NetworkError.Timeout;
                    }
                    else
                    {
                        error = (byte)NetworkError.WrongHost;
                    }
                }
                else
                {
                    error = (byte)NetworkError.WrongConnection;
                }

                return false;
            }
        }
    }
}
