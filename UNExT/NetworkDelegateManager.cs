using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace UNExT
{
    public class NetworkDelegateManager : NetworkManager
    {

        public static new NetworkDelegateManager singleton
        {
            get
            {
                return NetworkManager.singleton as NetworkDelegateManager;
            }
        }

        private List<System.WeakReference> delegateBehaviours = new List<System.WeakReference>(10);

        public void RegisterDelegates(NetworkDelegates delegates)
        {
            delegateBehaviours.RemoveAll((System.WeakReference r) =>
            {
                return r.Target as NetworkDelegates == null;
            });

            if (delegates)
            {
                delegateBehaviours.Add(new System.WeakReference(delegates));
            }
        }

        public void UnregisterDelegates(NetworkDelegates delegates)
        {
            delegateBehaviours.RemoveAll((System.WeakReference r) =>
            {
                var target = r.Target as NetworkDelegates;
                return target == null || target == delegates;
            });
        }

        private void CallDelegates(System.Action<NetworkDelegates> cb)
        {
            foreach (var d in delegateBehaviours)
            {
                var target = d.Target as NetworkDelegates;
                if (target) cb(target);
            }
        }


        private static System.Action onStartServer;// = delegate() { };
        private static System.Action onStopServer;// = delegate () { };
        private static System.Action<NetworkConnection> onClientConnect;// = delegate (NetworkConnection n) { };
        private static System.Action<NetworkConnection> onClientDisconnect;// = delegate (NetworkConnection n) { };
        private static System.Action<NetworkConnection> onServerReady;// = delegate (NetworkConnection n) { };
        private static System.Action<NetworkConnection> onClientSceneChanged;// = delegate (NetworkConnection n) { };
        private static System.Action<string> onServerSceneChanged;// = delegate (string n) { };

        public static void AddOnStartServer(System.Action a)
        {
            onStartServer += a;
            //AddDelegate(onStartServer, a);
        }

        public static void RemoveOnStartServer(System.Action a)
        {
            onStartServer -= a;
            //RemoveDelegate(onStartServer, a);
        }

        public static void AddOnStopServer(System.Action a)
        {
            onStopServer += a;
            //AddDelegate(onStopServer, a);
        }

        public static void RemoveOnStopServer(System.Action a)
        {
            onStopServer -= a;
            //RemoveDelegate(onStopServer, a);
        }

        public static void AddOnClientConnect(System.Action<NetworkConnection> a)
        {
            onClientConnect += a;
            //AddDelegate(onClientConnect, a);
        }

        public static void RemoveOnClientConnect(System.Action<NetworkConnection> a)
        {
            onClientConnect -= a;
            //RemoveDelegate(onClientConnect, a);
        }

        public static void AddOnClientDisconnect(System.Action<NetworkConnection> a)
        {
            onClientDisconnect += a;
            //AddDelegate(onClientDisconnect, a);
        }

        public static void RemoveOnClientDisconnect(System.Action<NetworkConnection> a)
        {
            onClientDisconnect -= a;
            //RemoveDelegate(onClientDisconnect, a);
        }

        public static void AddOnServerReady(System.Action<NetworkConnection> a)
        {
            onServerReady += a;
            //AddDelegate(onServerReady, a);
        }

        public static void RemoveOnServerReady(System.Action<NetworkConnection> a)
        {
            onServerReady -= a;
            //RemoveDelegate(onServerReady, a);
        }

        public static void AddOnClientSceneChanged(System.Action<NetworkConnection> a)
        {
            onClientSceneChanged += a;
            //AddDelegate(onClientSceneChanged, a);
        }

        public static void RemoveOnClientSceneChanged(System.Action<NetworkConnection> a)
        {
            onClientSceneChanged -= a;
            //RemoveDelegate(onClientSceneChanged, a);
        }

        public static void AddOnServerSceneChanged(System.Action<string> a)
        {
            onServerSceneChanged += a;
            //AddDelegate(onServerSceneChanged, a);
        }

        public static void RemoveOnServerSceneChanged(System.Action<string> a)
        {
            onServerSceneChanged -= a;
            //RemoveDelegate(onServerSceneChanged, a);
        }



        // Overload vistual functions

        public override void OnStartServer()
        {
            base.OnStartServer();

            //onStartServer();
            CallDelegates((NetworkDelegates d) => d.OnStartServer());
            //Debug.Log("onStartServer()");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            //onStopServer();
            CallDelegates((NetworkDelegates d) => d.OnStopServer());
            //Debug.Log("onStopServer()");
        }

        public override void OnStartClient(NetworkClient client)
        {
            base.OnStartClient(client);

            CallDelegates((NetworkDelegates d) => d.OnStartClient(client));
        }

        public override void OnStopClient()
        {
            base.OnStartClient(client);

            CallDelegates((NetworkDelegates d) => d.OnStopClient());
        }

        public override void OnClientConnect(NetworkConnection c)
        {
            base.OnClientConnect(c);

            //onClientConnect(c);
            CallDelegates((NetworkDelegates d) => d.OnClientConnect(c));
            //Debug.Log("onClientConnect()");
        }

        public override void OnClientDisconnect(NetworkConnection c)
        {
            base.OnClientDisconnect(c);

            //onClientDisconnect(c);
            CallDelegates((NetworkDelegates d) => d.OnClientDisconnect(c));
            //Debug.Log("onClientDisconnect()");
        }

        public override void OnServerReady(NetworkConnection c)
        {
            base.OnServerReady(c);

            //onServerReady(c);
            CallDelegates((NetworkDelegates d) => d.OnServerReady(c));
            //Debug.Log("onServerReady()");
        }

        public override void OnClientSceneChanged(NetworkConnection c)
        {
            base.OnClientSceneChanged(c);

            //onClientSceneChanged(c);
            CallDelegates((NetworkDelegates d) => d.OnClientSceneChanged(c));
            //Debug.Log("onClientSceneChanged()");
        }

        public override void OnServerSceneChanged(string c)
        {
            base.OnServerSceneChanged(c);

            //onServerSceneChanged(c);
            CallDelegates((NetworkDelegates d) => d.OnServerSceneChanged(c));
            //Debug.Log("onServerSceneChanged()");
        }


        // Utility functions

        private static void AddDelegate(System.Action a, System.Action b)
        {
            if (b != null) a += b;
        }

        private static void AddDelegate<T>(System.Action<T> a, System.Action<T> b)
        {
            if (b != null) a += b;
        }

        private static void RemoveDelegate(System.Action a, System.Action b)
        {
            if (b != null) a -= b;
        }

        private static void RemoveDelegate<T>(System.Action<T> a, System.Action<T> b)
        {
            if (b != null) a -= b;
        }

        private static void InvokeDelegate(System.Action a)
        {
            if (a != null) a.Invoke();
        }

        private static void InvokeDelegate<T>(System.Action<T> a, T b)
        {
            if (a != null) a.Invoke(b);
        }

    }
}
