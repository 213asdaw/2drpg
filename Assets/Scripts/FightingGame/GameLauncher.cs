using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public enum GameLaunchMode
    {
        MainMenu,
        Offline,
        OnlineHost,
        OnlineClient,
        RelayHost,
        RelayJoin,
        QuickMatch
    }

    public sealed class GameLauncher : MonoBehaviour
    {
        private GameLaunchMode mode = GameLaunchMode.MainMenu;
        private string joinAddress = "127.0.0.1";
        private string lobbyCode = string.Empty;
        private string statusMessage = string.Empty;
        private bool isConnecting;
        private bool showAdvancedLan;
        private OnlineFightingGame onlineSession;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle smallButtonStyle;

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
            }

            if (isConnecting)
            {
                DrawConnectingOverlay();
            }
        }

        private void DrawMainMenu()
        {
            float panelWidth = 540f;
            float panelHeight = showAdvancedLan ? 560f : 500f;
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 16f, panel.width - 48f, 40f), "2D Fighting Game", titleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 56f, panel.width - 48f, 24f), "플레이 방식을 선택하세요", labelStyle);

            float y = panel.y + 96f;
            if (GUI.Button(new Rect(panel.x + 40f, y, panel.width - 80f, 42f), "오프라인 (로컬 2P)", buttonStyle))
            {
                StartOffline();
            }

            y += 52f;
            if (GUI.Button(new Rect(panel.x + 40f, y, panel.width - 80f, 42f), "온라인 — 방 만들기 (로비 코드)", buttonStyle) && !isConnecting)
            {
                BeginRelayHost();
            }

            y += 52f;
            GUI.Label(new Rect(panel.x + 40f, y + 8f, 110f, 24f), "로비 코드", labelStyle);
            lobbyCode = GUI.TextField(new Rect(panel.x + 130f, y + 4f, panel.width - 170f, 28f), lobbyCode, textFieldStyle);
            y += 40f;
            if (GUI.Button(new Rect(panel.x + 40f, y, panel.width - 80f, 42f), "온라인 — 코드로 참가", buttonStyle) && !isConnecting)
            {
                BeginRelayJoin();
            }

            y += 52f;
            if (GUI.Button(new Rect(panel.x + 40f, y, panel.width - 80f, 42f), "온라인 — 빠른 매칭", buttonStyle) && !isConnecting)
            {
                BeginQuickMatch();
            }

            y += 52f;
            if (GUI.Button(new Rect(panel.x + 40f, y, 160f, 28f), showAdvancedLan ? "고급 LAN 숨기기" : "고급 LAN 접속", smallButtonStyle))
            {
                showAdvancedLan = !showAdvancedLan;
            }

            if (showAdvancedLan)
            {
                y += 36f;
                GUI.Label(new Rect(panel.x + 40f, y + 4f, 90f, 24f), "접속 IP", labelStyle);
                joinAddress = GUI.TextField(new Rect(panel.x + 120f, y, panel.width - 160f, 28f), joinAddress, textFieldStyle);
                y += 36f;
                if (GUI.Button(new Rect(panel.x + 40f, y, panel.width - 80f, 36f), "LAN — IP 직접 접속", buttonStyle) && !isConnecting)
                {
                    StartOnlineClient();
                }
            }

            GUI.Label(
                new Rect(panel.x + 24f, panel.y + panel.height - 72f, panel.width - 48f, 56f),
                "온라인 매칭: Unity Relay + Lobby (IP/port 불필요)\n"
                + "방 만들기 → 로비 코드 공유 → 상대가 코드로 참가",
                labelStyle);
        }

        private void DrawConnectingOverlay()
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Box(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 60f, 440f, 120f), GUIContent.none);
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.5f - 36f, 400f, 72f), statusMessage, labelStyle);
        }

        private void StartOffline()
        {
            mode = GameLaunchMode.Offline;
            gameObject.AddComponent<FightingGame>();
            enabled = false;
        }

        private void BeginRelayHost()
        {
            mode = GameLaunchMode.RelayHost;
            isConnecting = true;
            statusMessage = "Unity Services 연결 중...";
            _ = StartRelayHostAsync();
        }

        private void BeginRelayJoin()
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                statusMessage = "로비 코드를 입력하세요.";
                return;
            }

            mode = GameLaunchMode.RelayJoin;
            isConnecting = true;
            statusMessage = "로비 참가 중...";
            _ = StartRelayJoinAsync();
        }

        private void BeginQuickMatch()
        {
            mode = GameLaunchMode.QuickMatch;
            isConnecting = true;
            statusMessage = "빠른 매칭 중...";
            _ = StartQuickMatchAsync();
        }

        private async Task StartRelayHostAsync()
        {
            try
            {
                NetworkManager networkManager = FightingNetworkBootstrap.EnsureNetworkManager();
                statusMessage = "Relay/Lobby 생성 중...";
                RelayLobbySession session = await FightingRelayLobbyService.HostLobbyAsync(networkManager);

                if (!networkManager.StartHost())
                {
                    throw new InvalidOperationException("호스트 시작 실패");
                }

                BeginOnlineSession(networkManager, session, true);
            }
            catch (Exception exception)
            {
                HandleConnectionFailure(FightingRelayLobbyService.BuildSetupHint(exception));
            }
        }

        private async Task StartRelayJoinAsync()
        {
            try
            {
                NetworkManager networkManager = FightingNetworkBootstrap.EnsureNetworkManager();
                RelayLobbySession session = await FightingRelayLobbyService.JoinLobbyByCodeAsync(networkManager, lobbyCode);

                if (!networkManager.StartClient())
                {
                    throw new InvalidOperationException("클라이언트 시작 실패");
                }

                BeginOnlineSession(networkManager, session, false);
            }
            catch (Exception exception)
            {
                HandleConnectionFailure(FightingRelayLobbyService.BuildSetupHint(exception));
            }
        }

        private async Task StartQuickMatchAsync()
        {
            try
            {
                NetworkManager networkManager = FightingNetworkBootstrap.EnsureNetworkManager();
                statusMessage = "열린 방 검색 중...";
                RelayLobbySession session = await FightingRelayLobbyService.QuickMatchAsync(networkManager);

                if (session.IsHost)
                {
                    if (!networkManager.StartHost())
                    {
                        throw new InvalidOperationException("호스트 시작 실패");
                    }
                }
                else if (!networkManager.StartClient())
                {
                    throw new InvalidOperationException("클라이언트 시작 실패");
                }

                BeginOnlineSession(networkManager, session, session.IsHost);
            }
            catch (Exception exception)
            {
                HandleConnectionFailure(FightingRelayLobbyService.BuildSetupHint(exception));
            }
        }

        private void StartOnlineClient()
        {
            mode = GameLaunchMode.OnlineClient;
            isConnecting = true;
            statusMessage = "LAN 접속 중...";

            NetworkManager networkManager = FightingNetworkBootstrap.EnsureNetworkManager();
            FightingNetworkBootstrap.ConfigureClientAddress(joinAddress);

            if (!networkManager.StartClient())
            {
                HandleConnectionFailure("서버 접속 실패: " + joinAddress);
                return;
            }

            BeginOnlineSession(networkManager, null, false);
        }

        private void BeginOnlineSession(NetworkManager networkManager, RelayLobbySession relaySession, bool isHost)
        {
            onlineSession = gameObject.AddComponent<OnlineFightingGame>();
            onlineSession.Begin(networkManager, relaySession);

            if (isHost && relaySession != null)
            {
                gameObject.AddComponent<LobbyHeartbeatRunner>();
            }

            isConnecting = false;
            enabled = false;
        }

        private void HandleConnectionFailure(string message)
        {
            statusMessage = message;
            isConnecting = false;
            mode = GameLaunchMode.MainMenu;
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
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            smallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15
            };
        }
    }
}
