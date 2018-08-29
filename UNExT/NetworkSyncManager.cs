using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using System.Collections;
using System.Collections.Generic;

namespace UNExT
{
    /// <summary>
    /// A NetworkManager that additionally uses a simplified version of SNTP to synchornize time between client and server.
    /// NetworkSyncManager.time will therefore return an accurate estimate of the server time.
    /// </summary>
    public class NetworkSyncManager : NetworkDelegateManager
    {
        /// <summary>
        /// Use this value as a low-pass filter for high latencies. Default: 0.6 seconds.
        /// </summary>
        public static double MaxAllowedRtt = 0.6;

        /// <summary>
        /// This determines how many seconds (0.05 by default) the client should wait before sending the next synchronization message to the server.
        /// </summary>
        public static float SyncRequestInterval = 0.05f;

        /// <summary>
        /// SyncManager will continue sending synchronization messages until the estimated time delay stabilizes.
        /// This value defines the target delta seconds (0.0001 by default) between subsequent estimates that causes the synchronization to stop.
        /// The default value of 0.0001 seconds should make the system converge in less than 10 seconds and produce an accurate estimate.
        /// </summary>
        public static float ConvergenceTarget = 0.0001f;

        /// <summary>
        /// Minimum number of synchronization messages (10 by default) that should be exchanged between client and server.
        /// SyncManager will continue sending synchronization messages until the estimated time delay stabilizes.
        /// Hence, setting this to a high value is useless and would only help overloading the server.
        /// </summary>
        public static int MinSyncRequests = 10;

        /// <summary>
        /// The default id for the synchronization messages is (MsgType.Highest + 1). You can change it to any valid value.
        /// </summary>
        public static short TimeSyncMsgType = MsgType.Highest + 1;

        /// <summary>
        /// The number of seconds since the server started.
        /// If all clients and the server read this value in the same instant, it would ideally be the same.
        /// The syncronizations takes at least one RTTs to happen. Use isTimeValid to check if this value is valid.
        /// </summary>
        public static double time
        {
            get
            {
                return ((NetworkSyncManager)singleton).elapsedTime + ((NetworkSyncManager)singleton).timeDelay;
            }
        }

        /// <summary>
        /// Will SyncManager.time return a valid value? The syncronization starts immediately after the client connects to a server,
        /// but it takes at least one RTT to receive the first syncronization message from the server. Until that happens, this returns false.
        /// </summary>
        public static bool isTimeValid
        {
            get
            {
                return NetworkServer.active || ((NetworkSyncManager)singleton).numSyncs > 0;
            }
        }

        private double elapsedTime
        {
            get
            {
                return stopwatch.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency;
            }
        }

        //private struct PendingRequest
        //{
        //    public double sendTime;
        //}

        //private const int RequestBufferSize = 20;

        private Coroutine syncCoroutine;

        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        //private Dictionary<int, PendingRequest> pendingRequests;

        //private int requestCounter;

        private double timeDelay;
        private double numSyncs;
        private double meanChange;


        public override void OnClientConnect(NetworkConnection conn)
        {
            // If the client is also a host, not time syncronization is needed
            if (!NetworkServer.active)
            {
                //pendingRequests = new Dictionary<int, PendingRequest>(RequestBufferSize);

                stopwatch.Start();

                client.RegisterHandler(TimeSyncMsgType, OnSyncResponse);

                StartCoroutine(CoSendSyncRequests());

                return;
            }

            base.OnClientConnect(conn);
        }

        public override void OnClientDisconnect(NetworkConnection conn)
        {
            if (syncCoroutine != null)
            {
                StopCoroutine(syncCoroutine);
            }

            //requestCounter = 0;
            numSyncs = 0;
            timeDelay = 0;
            meanChange = 0;

            //pendingRequests.Clear();

            client = null;

            stopwatch.Reset();

            base.OnClientDisconnect(conn);
        }

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler(TimeSyncMsgType, OnSyncRequest);

            stopwatch.Start();

            base.OnStartServer();
        }

        public override void OnStopServer()
        {
            stopwatch.Reset();

            base.OnStopServer();
        }

        private void OnSyncRequest(NetworkMessage msg)
        {
            SyncResponse res = new SyncResponse(elapsedTime, msg.ReadMessage<SyncRequest>().clientTime);

            msg.conn.SendUnreliable(TimeSyncMsgType, res);
        }

        private void OnSyncResponse(NetworkMessage msg)
        {
            var receiveTime = elapsedTime;

            var res = msg.ReadMessage<SyncResponse>();

            //PendingRequest req;

            //if(pendingRequests.TryGetValue(res.requestIndex, out req))
            //{
            var rtt = receiveTime - res.clientTime;

            if (rtt <= MaxAllowedRtt)
            {
                var delta = (res.serverTime + rtt / 2.0) - receiveTime;

                var prevMean = timeDelay;
                var prevCount = numSyncs;
                numSyncs++;
                timeDelay = timeDelay * (prevCount / numSyncs) + delta / numSyncs;

                if (numSyncs == 2)
                {
                    meanChange = System.Math.Abs(prevMean - timeDelay);
                }
                else if (numSyncs > 2)
                {
                    meanChange = meanChange * 0.9 + System.Math.Abs(prevMean - timeDelay) * 0.1;
                }
            }

            //pendingRequests.Remove(res.requestIndex);
            //}
        }

        private IEnumerator CoSendSyncRequests()
        {
            while (numSyncs < MinSyncRequests || meanChange > ConvergenceTarget)
            {
                SendSyncRequest();

                yield return new WaitForSecondsRealtime(SyncRequestInterval);
            }

            Debug.Log("SyncManager: converged with delay of " + timeDelay + " seconds after " + (int)numSyncs + " requests");

            syncCoroutine = null;
        }

        private void SendSyncRequest()
        {
            //PendingRequest req;
            //req.sendTime = elapsedTime;

            // Don't send too many requests
            //if (pendingRequests.Count >= RequestBufferSize)
            //{
            //    bool expiredRemoved = false;

            //    // Try to remove an old request that expired
            //    foreach (var pair in pendingRequests)
            //    {
            //        if (req.sendTime - pair.Value.sendTime >= MaxAllowedRtt)
            //        {
            //            pendingRequests.Remove(pair.Key);
            //            expiredRemoved = true;
            //            break;
            //        }
            //    }

            //    if(!expiredRemoved)
            //    {
            //        return;
            //    }
            //}

            var msg = new SyncRequest(elapsedTime);

            //pendingRequests.Add(requestCounter, req);

            //requestCounter = (requestCounter + 1) % int.MaxValue;

            client.SendUnreliable(TimeSyncMsgType, msg);
        }

        private class SyncRequest : MessageBase
        {
            public double clientTime { get; private set; }

            public override void Deserialize(NetworkReader reader)
            {
                clientTime = reader.ReadDouble();
            }

            // This method would be generated
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(clientTime);
            }

            public SyncRequest() { }

            public SyncRequest(double time)
            {
                clientTime = time;
            }
        }

        private class SyncResponse : MessageBase
        {
            public double serverTime { get; private set; }
            public double clientTime { get; private set; }

            public override void Deserialize(NetworkReader reader)
            {
                serverTime = reader.ReadDouble();
                clientTime = reader.ReadDouble();
            }

            // This method would be generated
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(serverTime);
                writer.Write(clientTime);
            }

            public SyncResponse() { }

            public SyncResponse(double serverTime, double clientTime)
            {
                this.serverTime = serverTime;
                this.clientTime = clientTime;
            }
        }

    }
}
