using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public sealed class OnlineFightingGame : MonoBehaviour
    {
        private NetworkManager networkManager;
        private RelayLobbySession relaySession;
        private Camera mainCamera;
        private bool coordinatorSpawned;
        private string statusText = "상대 접속 대기 중...";
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle bannerStyle;
        private GUIStyle cooldownStyle;
        private GUIStyle buttonStyle;
        private readonly System.Collections.Generic.List<FloatingCombatText> floatingTexts = new System.Collections.Generic.List<FloatingCombatText>();
        private bool submittedLocalArchetype;
        private bool aloneInSession;
        private bool wasConnectedToSession;

        public void Begin(NetworkManager manager, RelayLobbySession relayLobbySession = null)
        {
            networkManager = manager;
            relaySession = relayLobbySession;
            FightSceneBuilder.ConfigureDisplay();
            mainCamera = FightSceneBuilder.BuildStage();

            if (networkManager.IsHost)
            {
                FightSessionConfig.ApplyHostSlotArchetype();
            }

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnServerStarted += HandleServerStarted;

            if (networkManager.IsServer)
            {
                TrySpawnCoordinator();
            }

            UpdateStatusText();
        }

        private void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            networkManager.OnServerStarted -= HandleServerStarted;
        }

        private void Update()
        {
            UpdateFloatingTexts(Time.deltaTime);

            if (networkManager == null)
            {
                return;
            }

            NetworkMatchCoordinator coordinator = NetworkMatchCoordinator.Instance;
            if (coordinator != null && coordinator.Phase == MatchPhase.MatchEnd && Input.GetKeyDown(KeyCode.R) && networkManager.IsServer)
            {
                coordinator.ServerRestartMatch();
            }

            if (coordinator != null && coordinator.Phase == MatchPhase.MatchEnd && Input.GetKeyDown(KeyCode.M))
            {
                LeaveOnlineSession();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LeaveOnlineSession();
            }

            if (networkManager != null && networkManager.IsConnectedClient)
            {
                wasConnectedToSession = true;
            }
            else if (wasConnectedToSession && networkManager != null && !networkManager.IsConnectedClient)
            {
                aloneInSession = true;
                statusText = "연결이 끊어졌습니다.";
            }

            TrySubmitLocalArchetype(coordinator);
            UpdateStatusText();
        }

        private void TrySubmitLocalArchetype(NetworkMatchCoordinator coordinator)
        {
            if (submittedLocalArchetype || coordinator == null || networkManager == null || !networkManager.IsConnectedClient)
            {
                return;
            }

            coordinator.SubmitLocalArchetype(FightSessionConfig.LocalPlayerArchetype);
            submittedLocalArchetype = true;
        }

        private void OnGUI()
        {
            FightKeyCapture.ProcessGuiEvent(Event.current);
            EnsureStyles();
            DrawConnectionStatus();
            DrawLeaveSessionControls();

            NetworkMatchCoordinator coordinator = NetworkMatchCoordinator.Instance;
            if (coordinator == null || coordinator.Fighters.Count < 2)
            {
                return;
            }

            NetworkFighter playerOne = coordinator.Fighters[0];
            NetworkFighter playerTwo = coordinator.Fighters[1];
            DrawHealthBar(new Rect(40f, 24f, 420f, 28f), playerOne.Fighter, false);
            DrawHealthBar(new Rect(Screen.width - 460f, 24f, 420f, 28f), playerTwo.Fighter, true);
            DrawRoundInfo(coordinator);
            DrawControlsHelp(coordinator);
            DrawBanner(coordinator);
            DrawFloatingTexts();
            DrawSkillCooldowns(playerOne.Fighter, playerTwo.Fighter);
            DrawMatchEndMenu(coordinator);
        }

        private void HandleServerStarted()
        {
            TrySpawnCoordinator();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager.IsServer)
            {
                TrySpawnCoordinator();
            }

            UpdateStatusText();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (networkManager == null)
            {
                return;
            }

            if (networkManager.ConnectedClientsIds.Count < 2)
            {
                aloneInSession = true;
                statusText = "상대가 연결을 종료했습니다.";
            }
        }

        private void LeaveOnlineSession()
        {
            FightSessionCleanup.ReturnToMainMenu();
        }

        private void DrawLeaveSessionControls()
        {
            if (networkManager == null)
            {
                return;
            }

            EnsureStyles();

            NetworkMatchCoordinator coordinator = NetworkMatchCoordinator.Instance;
            bool waitingForOpponent = networkManager.ConnectedClientsIds.Count < 2
                || coordinator == null
                || !coordinator.IsMatchActive;

            if (aloneInSession)
            {
                DrawLeavePanel("상대가 나갔거나 연결이 끊어졌습니다.", "메인 메뉴");
                return;
            }

            if (waitingForOpponent)
            {
                bool isQuickMatch = relaySession != null && relaySession.IsQuickMatch;
                string waitingMessage = isQuickMatch
                    ? "빠른 매칭 대기 중...\n상대가 빠른 매칭을 누르면 자동으로 연결됩니다."
                    : relaySession != null
                        ? "로비 코드: " + relaySession.LobbyCode
                        : statusText;
                string cancelLabel = isQuickMatch ? "매칭 취소" : "방 나가기";
                DrawLeavePanel(waitingMessage, cancelLabel);
                return;
            }

            if (GUI.Button(new Rect(Screen.width - 168f, 12f, 144f, 34f), "나가기 (Esc)", buttonStyle))
            {
                LeaveOnlineSession();
            }
        }

        private void DrawLeavePanel(string message, string buttonLabel)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            const float panelWidth = 460f;
            const float panelHeight = 220f;
            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x + 24f, panel.y + 28f, panel.width - 48f, 72f), message, labelStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 96f, panel.width - 48f, 24f), statusText, labelStyle);

            if (GUI.Button(new Rect(panel.x + (panel.width - 200f) * 0.5f, panel.y + 148f, 200f, 44f), buttonLabel + " (Esc)", buttonStyle))
            {
                LeaveOnlineSession();
            }
        }

        private void TrySpawnCoordinator()
        {
            if (coordinatorSpawned || networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            GameObject template = FightingNetworkBootstrap.GetMatchCoordinatorPrefabTemplate();
            GameObject coordinatorObject = Instantiate(template);
            coordinatorObject.SetActive(true);
            coordinatorObject.GetComponent<NetworkObject>().Spawn();
            coordinatorSpawned = true;
        }

        private void UpdateStatusText()
        {
            if (networkManager == null)
            {
                return;
            }

            int playerCount = networkManager.ConnectedClientsIds.Count;
            bool isQuickMatch = relaySession != null && relaySession.IsQuickMatch;
            if (isQuickMatch && playerCount < 2)
            {
                statusText = networkManager.IsHost
                    ? "빠른 매칭 대기 중... (상대를 찾는 중)"
                    : "빠른 매칭 상대와 연결됨 — 시작 대기 중";
            }
            else if (relaySession != null && networkManager.IsHost && playerCount < 2)
            {
                statusText = "로비 코드: " + relaySession.LobbyCode + "  (상대에게 공유하세요)";
            }
            else if (relaySession != null && networkManager.IsClient && !networkManager.IsHost && playerCount < 2)
            {
                statusText = "Relay 접속됨 — 상대 대기 중 (로비: " + relaySession.LobbyCode + ")";
            }
            else if (networkManager.IsHost && playerCount < 2)
            {
                statusText = "LAN 호스트 대기 중 — IP: 이 PC IP, 포트 " + FightingNetworkBootstrap.DefaultPort;
            }
            else if (networkManager.IsClient && !networkManager.IsHost && playerCount < 2)
            {
                statusText = "서버 접속됨 — 상대 대기 중";
            }
            else if (playerCount >= 2)
            {
                statusText = "온라인 매치 진행 중";
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.92f, 0.94f, 1f) }
            };

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.35f) }
            };

            cooldownStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawMatchEndMenu(NetworkMatchCoordinator coordinator)
        {
            if (coordinator.Phase != MatchPhase.MatchEnd)
            {
                return;
            }

            EnsureStyles();
            float centerX = Screen.width * 0.5f;
            float y = Screen.height - 118f;
            bool isHost = networkManager != null && networkManager.IsServer;

            if (isHost)
            {
                if (GUI.Button(new Rect(centerX - 170f, y, 160f, 42f), "재대결 (R)", buttonStyle))
                {
                    coordinator.ServerRestartMatch();
                }
            }
            else
            {
                GUI.Label(new Rect(centerX - 170f, y + 10f, 160f, 24f), "재대결: 호스트만", labelStyle);
            }

            if (GUI.Button(new Rect(centerX + 10f, y, 160f, 42f), "메인 메뉴 (M)", buttonStyle))
            {
                LeaveOnlineSession();
            }
        }

        private void DrawSkillCooldowns(FighterController playerOneFighter, FighterController playerTwoFighter)
        {
            Camera camera = mainCamera != null ? mainCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            EnsureStyles();
            FightHudDrawer.DrawSkillCooldowns(camera, playerOneFighter, cooldownStyle, cooldownStyle);
            FightHudDrawer.DrawSkillCooldowns(camera, playerTwoFighter, cooldownStyle, cooldownStyle);
        }

        private void DrawConnectionStatus()
        {
            if (networkManager == null || networkManager.ConnectedClientsIds.Count < 2)
            {
                return;
            }

            GUI.Label(new Rect(24f, Screen.height - 36f, Screen.width - 48f, 24f), statusText, labelStyle);
        }

        private void DrawHealthBar(Rect frame, FighterController fighter, bool alignRight)
        {
            GUI.Box(frame, GUIContent.none);

            Rect fill = frame;
            fill.x += 2f;
            fill.y += 2f;
            fill.width -= 4f;
            fill.height -= 4f;
            fill.width *= fighter.HealthRatio;
            if (alignRight)
            {
                float lostWidth = (frame.width - 4f) * (1f - fighter.HealthRatio);
                fill.x += lostWidth;
            }

            Color previous = GUI.color;
            GUI.color = alignRight ? new Color(0.95f, 0.35f, 0.3f) : new Color(0.3f, 0.65f, 0.98f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(frame.x + 8f, frame.y - 2f, frame.width, frame.height + 4f), fighter.DisplayName, titleStyle);
        }

        private void DrawRoundInfo(NetworkMatchCoordinator coordinator)
        {
            string roundText = "ROUND " + coordinator.CurrentRound
                + "   " + coordinator.PlayerOneRoundWins + " - " + coordinator.PlayerTwoRoundWins;
            GUI.Label(new Rect(Screen.width * 0.5f - 120f, 18f, 240f, 30f), roundText, titleStyle);

            string timerText = Mathf.CeilToInt(Mathf.Max(0f, coordinator.RoundTimer)).ToString("00");
            GUI.Label(new Rect(Screen.width * 0.5f - 30f, 48f, 60f, 30f), timerText, bannerStyle);
        }

        private void DrawControlsHelp(NetworkMatchCoordinator coordinator)
        {
            if (coordinator.Fighters.Count < 2)
            {
                return;
            }

            const float y = 78f;
            NetworkFighter localFighter = FindLocalFighter(coordinator);
            if (localFighter != null && localFighter.Fighter != null)
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 280f, y, 560f, 44f),
                    FightingGame.BuildOnlineControlLine(localFighter.Fighter),
                    labelStyle);
            }

            if (coordinator.Phase == MatchPhase.MatchEnd)
            {
                string restartHint = networkManager != null && networkManager.IsServer
                    ? "R — 재대결  |  M — 메인 메뉴"
                    : "M — 메인 메뉴";
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height - 72f, 360f, 24f), restartHint, labelStyle);
            }
        }

        private NetworkFighter FindLocalFighter(NetworkMatchCoordinator coordinator)
        {
            foreach (NetworkFighter fighter in coordinator.Fighters)
            {
                if (fighter != null && fighter.IsOwner)
                {
                    return fighter;
                }
            }

            return null;
        }

        private void DrawBanner(NetworkMatchCoordinator coordinator)
        {
            if (coordinator.Phase == MatchPhase.Fighting && coordinator.RoundTimer > FightConstants.RoundDuration - 0.5f)
            {
                return;
            }

            if (coordinator.Phase == MatchPhase.Intro || coordinator.Phase == MatchPhase.RoundEnd || coordinator.Phase == MatchPhase.MatchEnd)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.42f, 520f, 60f), coordinator.StatusMessage, bannerStyle);
            }
        }

        private void UpdateFloatingTexts(float deltaTime)
        {
            for (int i = floatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingCombatText entry = floatingTexts[i];
                entry.Life -= deltaTime;
                entry.WorldPosition += Vector3.up * deltaTime * 0.8f;
                floatingTexts[i] = entry;
                if (entry.Life <= 0f)
                {
                    floatingTexts.RemoveAt(i);
                }
            }
        }

        private void DrawFloatingTexts()
        {
            Camera camera = mainCamera != null ? mainCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            EnsureStyles();
            foreach (FloatingCombatText entry in floatingTexts)
            {
                Vector3 screenPoint = camera.WorldToScreenPoint(entry.WorldPosition);
                if (screenPoint.z < 0f)
                {
                    continue;
                }

                Color previous = GUI.color;
                GUI.color = entry.Color;
                GUI.Label(new Rect(screenPoint.x - 40f, Screen.height - screenPoint.y - 12f, 80f, 24f), entry.Text, titleStyle);
                GUI.color = previous;
            }
        }

        private struct FloatingCombatText
        {
            public Vector3 WorldPosition;
            public string Text;
            public Color Color;
            public float Life;
        }
    }
}
