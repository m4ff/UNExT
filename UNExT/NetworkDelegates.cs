using UnityEngine;
using UnityEngine.Networking;

namespace UNExT
{
    public class NetworkDelegates : MonoBehaviour
    {

        public virtual void OnStartServer() { }

        public virtual void OnStopServer() { }

        public virtual void OnStartClient(NetworkClient c) { }

        public virtual void OnStopClient() { }

        public virtual void OnClientConnect(NetworkConnection c) { }

        public virtual void OnClientDisconnect(NetworkConnection c) { }

        public virtual void OnServerReady(NetworkConnection c) { }

        public virtual void OnClientSceneChanged(NetworkConnection c) { }

        public virtual void OnServerSceneChanged(string c) { }

    }
}
