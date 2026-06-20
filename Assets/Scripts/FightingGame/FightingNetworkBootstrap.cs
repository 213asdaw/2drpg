using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace FightingGame
{
    public static class FightingNetworkBootstrap
    {
        public const ushort DefaultPort = 7777;

        private static GameObject fighterPrefabTemplate;
        private static GameObject matchCoordinatorPrefabTemplate;

        public static NetworkManager EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                return NetworkManager.Singleton;
            }

            GameObject networkObject = new GameObject("NetworkManager");
            Object.DontDestroyOnLoad(networkObject);

            UnityTransport transport = networkObject.AddComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", DefaultPort, "0.0.0.0");

            NetworkManager networkManager = networkObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                TickRate = 60,
                ClientConnectionBufferTimeout = 10
            };

            networkManager.ConnectionApprovalCallback = (request, response) =>
            {
                int connectedCount = networkManager.ConnectedClientsIds.Count;
                response.Approved = connectedCount < 2;
                response.CreatePlayerObject = false;
            };

            RegisterRuntimeNetworkPrefabs(networkManager);
            return networkManager;
        }

        public static GameObject GetFighterPrefabTemplate()
        {
            EnsurePrefabTemplates();
            return fighterPrefabTemplate;
        }

        public static GameObject GetMatchCoordinatorPrefabTemplate()
        {
            EnsurePrefabTemplates();
            return matchCoordinatorPrefabTemplate;
        }

        public static void ConfigureClientAddress(string address, ushort port = DefaultPort)
        {
            NetworkManager networkManager = EnsureNetworkManager();
            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            transport.SetConnectionData(address, port);
        }

        private static void RegisterRuntimeNetworkPrefabs(NetworkManager networkManager)
        {
            EnsurePrefabTemplates();
            networkManager.AddNetworkPrefab(fighterPrefabTemplate);
            networkManager.AddNetworkPrefab(matchCoordinatorPrefabTemplate);
        }

        private static void EnsurePrefabTemplates()
        {
            if (fighterPrefabTemplate != null && matchCoordinatorPrefabTemplate != null)
            {
                return;
            }

            fighterPrefabTemplate = CreateHiddenPrefab("NetworkFighterPrefab", typeof(NetworkFighter));
            matchCoordinatorPrefabTemplate = CreateHiddenPrefab("NetworkMatchCoordinatorPrefab", typeof(NetworkMatchCoordinator));
        }

        private static GameObject CreateHiddenPrefab(string name, System.Type behaviourType)
        {
            GameObject prefab = new GameObject(name);
            prefab.AddComponent<NetworkObject>();
            prefab.AddComponent(behaviourType);
            prefab.SetActive(false);
            Object.DontDestroyOnLoad(prefab);
            return prefab;
        }
    }
}
