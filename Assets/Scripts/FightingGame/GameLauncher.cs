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
        private MenuSection expandedSection = MenuSection.None;
        private Vector2 menuScroll;
        private OnlineFightingGame onlineSession;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle smallButtonStyle;
        private GUIStyle statusStyle;
        private GUIStyle scrollStyle;

        private enum MenuSection
        {
            None,
            FriendHelp,
            AdvancedLan
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<GameLauncher>() != null || FindFirstObjectByType<FightingGame>() != null)
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
            const float panelWidth = 560f;
            const float panelHeight = 640f;
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 16f, panel.width - 48f, 36f), "2D Fighting Game", titleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 52f, panel.width - 48f, 22f), "플레이 방식을 선택하세요", labelStyle);

            Rect scrollViewRect = new Rect(panel.x + 16f, panel.y + 84f, panel.width - 32f, panel.height - 130f);
            Rect scrollContentRect = new Rect(0f, 0f, panel.width - 52f, CalculateScrollContentHeight());

            menuScroll = GUI.BeginScrollView(scrollViewRect, menuScroll, scrollContentRect, false, true, GUIStyle.none, scrollStyle);

            float y = 8f;
            float buttonWidth = scrollContentRect.width - 16f;

            if (GUI.Button(new Rect(8f, y, buttonWidth, 44f), "오프라인 (로컬 2P)", buttonStyle))
            {
                StartOffline();
            }

            y += 52f;
            if (GUI.Button(new Rect(8f, y, buttonWidth, 44f), "온라인 — 방 만들기 (로비 코드)", buttonStyle) && !isConnecting)
            {
                BeginRelayHost();
            }

            y += 52f;
            GUI.Label(new Rect(8f, y + 10f, 100f, 22f), "로비 코드", labelStyle);
            lobbyCode = GUI.TextField(new Rect(108f, y + 6f, buttonWidth - 100f, 28f), lobbyCode, textFieldStyle);

            y += 44f;
            if (GUI.Button(new Rect(8f, y, buttonWidth, 44f), "온라인 — 코드로 참가", buttonStyle) && !isConnecting)
            {
                BeginRelayJoin();
            }

            y += 52f;
            if (GUI.Button(new Rect(8f, y, buttonWidth, 44f), "온라인 — 빠른 매칭", buttonStyle) && !isConnecting)
            {
                BeginQuickMatch();
            }

            y += 56f;
            DrawSectionToggle(ref y, buttonWidth, MenuSection.FriendHelp, "친구는 게임을 어떻게 켜?");
            if (expandedSection == MenuSection.FriendHelp)
            {
                GUI.Label(
                    new Rect(8f, y, buttonWidth, 96f),
                    "① 방장: Unity에서 Build 후 exe/zip을 친구에게 전송\n"
                    + "② 친구: zip 압축 해제 → exe 더블클릭\n"
                    + "③ 친구: 로비 코드 입력 → [코드로 참가]\n"
                    + "※ Unity 설치·IP 입력 불필요",
                    labelStyle);
                y += 100f;
            }

            y += 8f;
            DrawSectionToggle(ref y, buttonWidth, MenuSection.AdvancedLan, "고급 LAN (IP 직접 접속)");
            if (expandedSection == MenuSection.AdvancedLan)
            {
                GUI.Label(new Rect(8f, y + 6f, 80f, 22f), "접속 IP", labelStyle);
                joinAddress = GUI.TextField(new Rect(88f, y + 2f, buttonWidth - 80f, 28f), joinAddress, textFieldStyle);
                y += 36f;
                if (GUI.Button(new Rect(8f, y, buttonWidth, 38f), "LAN — IP로 접속", buttonStyle) && !isConnecting)
                {
                    StartOnlineClient();
                }

                y += 46f;
            }

            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUI.Label(new Rect(panel.x + 20f, panel.y + panel.height - 44f, panel.width - 40f, 36f), statusMessage, statusStyle);
            }
        }

        private void DrawSectionToggle(ref float y, float buttonWidth, MenuSection section, string label)
        {
            string buttonLabel = expandedSection == section ? "▲ " + label : "▼ " + label;
            if (GUI.Button(new Rect(8f, y, buttonWidth, 30f), buttonLabel, smallButtonStyle))
            {
                expandedSection = expandedSection == section ? MenuSection.None : section;
            }

            y += 34f;
        }

        private float CalculateScrollContentHeight()
        {
            float height = 8f + 52f + 52f + 44f + 52f + 52f + 56f + 34f + 8f + 34f + 24f;
            if (expandedSection == MenuSection.FriendHelp)
            {
                height += 100f;
            }

            if (expandedSection == MenuSection.AdvancedLan)
            {
                height += 82f;
            }

            return height;
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
            GameObject session = new GameObject("Offline Fighting Game");
            session.AddComponent<FightingGame>();
            DontDestroyOnLoad(session);
            Destroy(gameObject);
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
            GameObject session = new GameObject("Online Fighting Game");
            DontDestroyOnLoad(session);
            onlineSession = session.AddComponent<OnlineFightingGame>();
            onlineSession.Begin(networkManager, relaySession);

            if (isHost && relaySession != null)
            {
                session.AddComponent<LobbyHeartbeatRunner>();
            }

            isConnecting = false;
            Destroy(gameObject);
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
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.92f, 0.94f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            smallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(1f, 0.82f, 0.45f) }
            };

            scrollStyle = new GUIStyle(GUI.skin.verticalScrollbar);
        }
    }
}
