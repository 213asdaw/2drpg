using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public enum GameLaunchMode
    {
        MainMenu,
        Offline,
        OnlineHost,
        OnlineClient
    }

    public sealed class GameLauncher : MonoBehaviour
    {
        private GameLaunchMode mode = GameLaunchMode.MainMenu;
        private string joinAddress = "127.0.0.1";
        private string statusMessage = string.Empty;
        private OnlineFightingGame onlineSession;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle textFieldStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<GameLauncher>() != null || FindObjectOfType<FightingGame>() != null)
            {
                return;
            }

            GameObject launcher = new GameObject("Game Launcher");
            launcher.AddComponent<GameLauncher>();
            DontDestroyOnLoad(launcher);
        }

        private void Awake()
        {
            FightSceneBuilder.ConfigureDisplay();
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (mode == GameLaunchMode.MainMenu)
            {
                DrawMainMenu();
                return;
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUI.Label(new Rect(24f, 16f, Screen.width - 48f, 30f), statusMessage, labelStyle);
            }
        }

        private void DrawMainMenu()
        {
            float panelWidth = 520f;
            float panelHeight = 420f;
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, 40f), "2D Fighting Game", titleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 64f, panel.width - 48f, 24f), "플레이 방식을 선택하세요", labelStyle);

            if (GUI.Button(new Rect(panel.x + 40f, panel.y + 110f, panel.width - 80f, 44f), "오프라인 (로컬 2P)", buttonStyle))
            {
                StartOffline();
            }

            if (GUI.Button(new Rect(panel.x + 40f, panel.y + 170f, panel.width - 80f, 44f), "온라인 — 호스트 만들기", buttonStyle))
            {
                StartOnlineHost();
            }

            GUI.Label(new Rect(panel.x + 40f, panel.y + 232f, 120f, 24f), "접속 IP", labelStyle);
            joinAddress = GUI.TextField(new Rect(panel.x + 120f, panel.y + 228f, panel.width - 160f, 28f), joinAddress, textFieldStyle);

            if (GUI.Button(new Rect(panel.x + 40f, panel.y + 280f, panel.width - 80f, 44f), "온라인 — IP로 접속", buttonStyle))
            {
                StartOnlineClient();
            }

            GUI.Label(
                new Rect(panel.x + 24f, panel.y + 340f, panel.width - 48f, 60f),
                "온라인: 한쪽은 호스트, 다른 쪽은 IP 입력 후 접속\n포트: " + FightingNetworkBootstrap.DefaultPort + "  |  같은 Wi‑Fi/LAN 권장",
                labelStyle);
        }

        private void StartOffline()
        {
            mode = GameLaunchMode.Offline;
            gameObject.AddComponent<FightingGame>();
            enabled = false;
        }

        private void StartOnlineHost()
        {
            mode = GameLaunchMode.OnlineHost;
            NetworkManager networkManager = FightingNetworkBootstrap.EnsureNetworkManager();
            FightingNetworkBootstrap.ConfigureClientAddress("0.0.0.0");

            if (!networkManager.StartHost())
            {
                statusMessage = "호스트 시작 실패";
                mode = GameLaunchMode.MainMenu;
                return;
            }

            onlineSession = gameObject.AddComponent<OnlineFightingGame>();
            onlineSession.Begin(networkManager);
            statusMessage = "호스트 실행 중 — 상대 접속 대기";
            enabled = false;
        }

        private void StartOnlineClient()
        {
            mode = GameLaunchMode.OnlineClient;
            NetworkManager networkManager = FightingNetworkBootstrap.EnsureNetworkManager();
            FightingNetworkBootstrap.ConfigureClientAddress(joinAddress);

            if (!networkManager.StartClient())
            {
                statusMessage = "서버 접속 실패: " + joinAddress;
                mode = GameLaunchMode.MainMenu;
                return;
            }

            onlineSession = gameObject.AddComponent<OnlineFightingGame>();
            onlineSession.Begin(networkManager);
            statusMessage = "접속 중: " + joinAddress;
            enabled = false;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.92f, 0.94f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15
            };
        }
    }
}
