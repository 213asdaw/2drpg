using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public sealed class OnlineFightingGame : MonoBehaviour
    {
        private NetworkManager networkManager;
        private Camera mainCamera;
        private bool coordinatorSpawned;
        private string statusText = "상대 접속 대기 중...";
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle bannerStyle;
        private readonly System.Collections.Generic.List<FloatingCombatText> floatingTexts = new System.Collections.Generic.List<FloatingCombatText>();

        public void Begin(NetworkManager manager)
        {
            networkManager = manager;
            FightSceneBuilder.ConfigureDisplay();
            mainCamera = FightSceneBuilder.BuildStage();

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

            UpdateStatusText();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawConnectionStatus();

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
            statusText = "상대가 연결을 종료했습니다.";
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
            if (networkManager.IsHost && playerCount < 2)
            {
                statusText = "호스트 대기 중 — IP: 이 PC IP, 포트 " + FightingNetworkBootstrap.DefaultPort;
            }
            else if (networkManager.IsClient && !networkManager.IsHost)
            {
                statusText = playerCount < 2 ? "서버 접속됨 — 상대 대기 중" : "매치 준비 완료";
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
        }

        private void DrawConnectionStatus()
        {
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
            const float y = 78f;
            GUI.Label(new Rect(24f, y, 520f, 120f),
                "온라인: A/D 이동 | W 점프 | S 가드 | J 약공 | K 킥 | L 강공",
                labelStyle);

            if (coordinator.Phase == MatchPhase.MatchEnd && networkManager != null && networkManager.IsServer)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height - 48f, 240f, 30f), "R 키 — 재대결 (호스트만)", labelStyle);
            }
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
