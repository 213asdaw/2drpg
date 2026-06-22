using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public static class FightSessionCleanup
    {
        private static readonly string[] StageObjectNames =
        {
            "Background",
            "Arena Platform",
            "Floor Edge"
        };

        public static void ReturnToMainMenu()
        {
            foreach (FighterController fighter in Object.FindObjectsByType<FighterController>(FindObjectsSortMode.None))
            {
                if (fighter != null)
                {
                    Object.Destroy(fighter.gameObject);
                }
            }

            foreach (FightingGame offlineSession in Object.FindObjectsByType<FightingGame>(FindObjectsSortMode.None))
            {
                if (offlineSession != null)
                {
                    Object.Destroy(offlineSession.gameObject);
                }
            }

            foreach (OnlineFightingGame onlineSession in Object.FindObjectsByType<OnlineFightingGame>(FindObjectsSortMode.None))
            {
                if (onlineSession != null)
                {
                    Object.Destroy(onlineSession.gameObject);
                }
            }

            foreach (LobbyHeartbeatRunner heartbeat in Object.FindObjectsByType<LobbyHeartbeatRunner>(FindObjectsSortMode.None))
            {
                if (heartbeat != null)
                {
                    Object.Destroy(heartbeat.gameObject);
                }
            }

            foreach (string objectName in StageObjectNames)
            {
                GameObject stageObject = GameObject.Find(objectName);
                if (stageObject != null)
                {
                    Object.Destroy(stageObject);
                }
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.Shutdown();
                Object.Destroy(networkManager.gameObject);
            }

            if (Object.FindFirstObjectByType<GameLauncher>() == null)
            {
                GameObject launcher = new GameObject("Game Launcher");
                launcher.AddComponent<GameLauncher>();
                Object.DontDestroyOnLoad(launcher);
            }
        }
    }
}
