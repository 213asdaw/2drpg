using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace FightingGame
{
    public static class FightingNetworkBootstrap
    {
        public const ushort DefaultPort = 7777;
        private const string NetworkFighterPrefabId = "FightingGame.NetworkFighterPrefab";
        private const string NetworkMatchCoordinatorPrefabId = "FightingGame.NetworkMatchCoordinatorPrefab";

        private static GameObject fighterPrefabTemplate;
        private static GameObject matchCoordinatorPrefabTemplate;
        private static NetworkManager registeredNetworkManager;
        private static FieldInfo globalObjectIdHashField;

        public static NetworkManager EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                RegisterRuntimeNetworkPrefabs(NetworkManager.Singleton);
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

        public static void ResetRegistrationState()
        {
            registeredNetworkManager = null;
        }

        private static void RegisterRuntimeNetworkPrefabs(NetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return;
            }

            if (registeredNetworkManager == networkManager)
            {
                return;
            }

            EnsurePrefabTemplates();
            TryAddNetworkPrefab(networkManager, fighterPrefabTemplate);
            TryAddNetworkPrefab(networkManager, matchCoordinatorPrefabTemplate);
            registeredNetworkManager = networkManager;
        }

        private static void TryAddNetworkPrefab(NetworkManager networkManager, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                return;
            }

            uint prefabHash = GetPrefabGlobalObjectIdHash(networkObject);
            foreach (NetworkPrefab registeredPrefab in networkManager.NetworkConfig.Prefabs.PrefabsList)
            {
                if (registeredPrefab.Prefab == prefab)
                {
                    return;
                }

                if (registeredPrefab.Prefab == null)
                {
                    continue;
                }

                NetworkObject registeredObject = registeredPrefab.Prefab.GetComponent<NetworkObject>();
                if (registeredObject != null && GetPrefabGlobalObjectIdHash(registeredObject) == prefabHash)
                {
                    return;
                }
            }

            networkManager.AddNetworkPrefab(prefab);
        }

        private static void EnsurePrefabTemplates()
        {
            if (fighterPrefabTemplate != null && matchCoordinatorPrefabTemplate != null)
            {
                return;
            }

            fighterPrefabTemplate = CreateHiddenPrefab(
                "NetworkFighterPrefab",
                typeof(NetworkFighter),
                GetStablePrefabHash(NetworkFighterPrefabId));
            matchCoordinatorPrefabTemplate = CreateHiddenPrefab(
                "NetworkMatchCoordinatorPrefab",
                typeof(NetworkMatchCoordinator),
                GetStablePrefabHash(NetworkMatchCoordinatorPrefabId));
        }

        private static uint GetStablePrefabHash(string stableId)
        {
            return unchecked((uint)Animator.StringToHash(stableId));
        }

        private static GameObject CreateHiddenPrefab(string name, System.Type behaviourType, uint globalObjectIdHash)
        {
            GameObject prefab = new GameObject(name);
            NetworkObject networkObject = prefab.AddComponent<NetworkObject>();
            SetPrefabGlobalObjectIdHash(networkObject, globalObjectIdHash);
            prefab.AddComponent(behaviourType);
            prefab.SetActive(false);
            Object.DontDestroyOnLoad(prefab);
            return prefab;
        }

        private static FieldInfo GetGlobalObjectIdHashField()
        {
            if (globalObjectIdHashField != null)
            {
                return globalObjectIdHashField;
            }

            globalObjectIdHashField = typeof(NetworkObject).GetField(
                "GlobalObjectIdHash",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (globalObjectIdHashField == null)
            {
                globalObjectIdHashField = typeof(NetworkObject).GetField(
                    "NetworkObjectIdHash",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return globalObjectIdHashField;
        }

        private static uint GetPrefabGlobalObjectIdHash(NetworkObject networkObject)
        {
            FieldInfo field = GetGlobalObjectIdHashField();
            if (field == null || networkObject == null)
            {
                return 0;
            }

            return (uint)field.GetValue(networkObject);
        }

        private static void SetPrefabGlobalObjectIdHash(NetworkObject networkObject, uint hash)
        {
            FieldInfo field = GetGlobalObjectIdHashField();
            if (field == null || networkObject == null)
            {
                Debug.LogError("FightingNetworkBootstrap: failed to assign runtime NetworkObject hash.");
                return;
            }

            field.SetValue(networkObject, hash);
        }
    }
}
