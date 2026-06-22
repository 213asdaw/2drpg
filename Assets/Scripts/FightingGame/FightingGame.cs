using UnityEngine;

namespace FightingGame
{
    public sealed class FightingGame : MonoBehaviour
    {
        private FighterController playerOne;
        private FighterController playerTwo;
        private MatchManager matchManager;
        private Camera mainCamera;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle bannerStyle;
        private GUIStyle hintStyle;
        private GUIStyle cooldownStyle;
        private readonly System.Collections.Generic.List<FloatingCombatText> floatingTexts = new System.Collections.Generic.List<FloatingCombatText>();
        private bool sawGameplayInput;
        private float fightingPhaseStartedAt = -1f;

        private void Awake()
        {
            FightSceneBuilder.ConfigureDisplay();
            mainCamera = FightSceneBuilder.BuildStage();
            CreateFighters();
            matchManager = new MatchManager();
            matchManager.RoundStarted += HandleRoundStarted;
            matchManager.RoundEnded += HandleRoundEnded;
            matchManager.MatchEnded += HandleMatchEnded;
            matchManager.BeginMatch(playerOne, playerTwo);
        }

        private void Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            if (playerOne == null || playerTwo == null || matchManager == null)
            {
                return;
            }

            matchManager.Tick(Time.deltaTime);
            UpdateFloatingTexts(Time.deltaTime);

            if (matchManager.Phase == MatchPhase.MatchEnd && Input.GetKeyDown(KeyCode.R))
            {
                RestartMatch();
            }

            if (matchManager.Phase == MatchPhase.MatchEnd && Input.GetKeyDown(KeyCode.M))
            {
                FightSessionCleanup.ReturnToMainMenu();
            }
        }

        private int lastFighterTickFrame = -1;

        private void LateUpdate()
        {
            if (playerOne == null || playerTwo == null || matchManager == null)
            {
                return;
            }

            if (lastFighterTickFrame == Time.frameCount)
            {
                FightKeyCapture.EndFrame();
                return;
            }

            TickFighters();
            FightKeyCapture.EndFrame();
        }

        private void TickFighters()
        {
            lastFighterTickFrame = Time.frameCount;
            bool controlsEnabled = matchManager.ControlsEnabled;
            FighterInputSnapshot playerOneInput = FighterInputReader.ReadPlayerOne();
            FighterInputSnapshot playerTwoInput = FighterInputReader.ReadPlayerTwo();

            if (controlsEnabled)
            {
                if (fightingPhaseStartedAt < 0f)
                {
                    fightingPhaseStartedAt = Time.time;
                }

                if (FighterInputReader.HasGameplayInput(playerOneInput)
                    || FighterInputReader.HasGameplayInput(playerTwoInput))
                {
                    sawGameplayInput = true;
                }
            }
            else
            {
                fightingPhaseStartedAt = -1f;
            }

            playerOne.Tick(Time.deltaTime, playerOneInput, controlsEnabled);
            playerTwo.Tick(Time.deltaTime, playerTwoInput, controlsEnabled);
        }

        private void OnGUI()
        {
            FightKeyCapture.ProcessGuiEvent(Event.current);

            if (lastFighterTickFrame != Time.frameCount && Event.current.type == EventType.Repaint)
            {
                TickFighters();
            }

            EnsureStyles();
            DrawHealthBar(new Rect(40f, 24f, 420f, 28f), playerOne, false);
            DrawHealthBar(new Rect(Screen.width - 460f, 24f, 420f, 28f), playerTwo, true);
            DrawRoundInfo();
            DrawControlsHelp();
            DrawBanner();
            DrawFloatingTexts();
            DrawSkillCooldowns();
            DrawInputDebug();
            DrawMatchEndMenu();
        }

        private void DrawMatchEndMenu()
        {
            if (matchManager.Phase != MatchPhase.MatchEnd)
            {
                return;
            }

            EnsureStyles();
            float centerX = Screen.width * 0.5f;
            float y = Screen.height - 118f;

            if (GUI.Button(new Rect(centerX - 170f, y, 160f, 42f), "재대결 (R)", buttonStyle))
            {
                RestartMatch();
            }

            if (GUI.Button(new Rect(centerX + 10f, y, 160f, 42f), "메인 메뉴 (M)", buttonStyle))
            {
                FightSessionCleanup.ReturnToMainMenu();
            }
        }

        private void DrawInputDebug()
        {
            if (matchManager.Phase != MatchPhase.Fighting && matchManager.Phase != MatchPhase.Intro)
            {
                return;
            }

            EnsureStyles();
            string activeKeys = FighterInputReader.DescribeActiveKeys();
            string backend = FighterInputReader.DescribeInputBackend();
            float p1X = playerOne != null ? playerOne.transform.position.x : 0f;
            float p2X = playerTwo != null ? playerTwo.transform.position.x : 0f;

            if (!string.IsNullOrEmpty(activeKeys))
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 260f, Screen.height - 56f, 520f, 24f),
                    "입력 OK: " + activeKeys + " | P1 X=" + p1X.ToString("0.00") + " P2 X=" + p2X.ToString("0.00"),
                    hintStyle);
                return;
            }

            GUI.Label(
                new Rect(Screen.width * 0.5f - 340f, Screen.height - 88f, 680f, 72f),
                backend + " | P1 X=" + p1X.ToString("0.00") + " P2 X=" + p2X.ToString("0.00") + "\n"
                + "Game 탭 클릭 → P1: A/D | P2: ←/→ (E/O 대체) | ↑↓ 점프/가드\n"
                + "Active Input Handling = Both | Device Simulator 창 닫기",
                hintStyle);
        }

        private void CreateFighters()
        {
            playerOne = FightSceneBuilder.CreateFighter(0, new Vector3(-3.5f, FightConstants.GroundY, 0f), FighterArchetypeId.FlameSwordsman);
            playerTwo = FightSceneBuilder.CreateFighter(1, new Vector3(3.5f, FightConstants.GroundY, 0f), FighterArchetypeId.Default);
            playerOne.SetOpponent(playerTwo);
            playerTwo.SetOpponent(playerOne);
            playerOne.Damaged += (_, damage) => SpawnFloatingText(playerOne.transform.position + Vector3.up * 1.8f, "-" + damage.ToString("0"), new Color(1f, 0.45f, 0.45f));
            playerTwo.Damaged += (_, damage) => SpawnFloatingText(playerTwo.transform.position + Vector3.up * 1.8f, "-" + damage.ToString("0"), new Color(1f, 0.45f, 0.45f));
            playerOne.LandedHit += (_, __, type) => SpawnFloatingText(playerTwo.transform.position + Vector3.up * 2.1f, type.ToString().ToUpper(), new Color(1f, 0.92f, 0.45f));
            playerTwo.LandedHit += (_, __, type) => SpawnFloatingText(playerOne.transform.position + Vector3.up * 2.1f, type.ToString().ToUpper(), new Color(1f, 0.92f, 0.45f));
            playerOne.BuffActivated += fighter => SpawnFloatingText(
                fighter.transform.position + Vector3.up * 2.1f,
                "용암방어!",
                new Color(1f, 0.62f, 0.28f));
        }

        private void HandleRoundStarted()
        {
            sawGameplayInput = false;
            fightingPhaseStartedAt = -1f;
            playerOne.ResetForRound(new Vector3(-3.5f, FightConstants.GroundY, 0f));
            playerTwo.ResetForRound(new Vector3(3.5f, FightConstants.GroundY, 0f));
        }

        private void HandleRoundEnded(FighterController winner)
        {
            if (winner == playerOne)
            {
                playerOne.SetMatchResult(true);
                playerTwo.SetMatchResult(false);
            }
            else if (winner == playerTwo)
            {
                playerTwo.SetMatchResult(true);
                playerOne.SetMatchResult(false);
            }
        }

        private void HandleMatchEnded(FighterController winner)
        {
            if (winner == playerOne)
            {
                playerOne.SetMatchResult(true);
                playerTwo.SetMatchResult(false);
            }
            else if (winner == playerTwo)
            {
                playerTwo.SetMatchResult(true);
                playerOne.SetMatchResult(false);
            }
        }

        private void RestartMatch()
        {
            playerOne.ResetForRound(new Vector3(-3.5f, FightConstants.GroundY, 0f));
            playerTwo.ResetForRound(new Vector3(3.5f, FightConstants.GroundY, 0f));
            matchManager.RestartMatch();
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

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.35f) }
            };

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.82f, 0.35f) }
            };

            cooldownStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private void DrawSkillCooldowns()
        {
            Camera camera = mainCamera != null ? mainCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            EnsureStyles();
            FightHudDrawer.DrawSkillCooldowns(camera, playerOne, cooldownStyle, cooldownStyle);
            FightHudDrawer.DrawSkillCooldowns(camera, playerTwo, cooldownStyle, cooldownStyle);
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

        private void DrawRoundInfo()
        {
            string roundText = "ROUND " + matchManager.CurrentRound
                + "   " + matchManager.PlayerOneRoundWins + " - " + matchManager.PlayerTwoRoundWins;
            GUI.Label(new Rect(Screen.width * 0.5f - 120f, 18f, 240f, 30f), roundText, titleStyle);

            string timerText = Mathf.CeilToInt(Mathf.Max(0f, matchManager.RoundTimer)).ToString("00");
            GUI.Label(new Rect(Screen.width * 0.5f - 30f, 48f, 60f, 30f), timerText, bannerStyle);
        }

        private void DrawControlsHelp()
        {
            const float y = 78f;
            GUI.Label(new Rect(24f, y, 520f, 44f),
                "P1 카론: A/D 이동 W점프 S가드 J/K/L 공격 | U/I 스킬",
                labelStyle);
            GUI.Label(new Rect(Screen.width - 544f, y, 520f, 44f),
                "P2 검투사: ←/→ 또는 E/O | ↑/↓ | 1/2/3 공격",
                labelStyle);

            DrawInputFocusHint();

            if (matchManager.Phase == MatchPhase.MatchEnd)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height - 72f, 360f, 24f), "R — 재대결  |  M — 메인 메뉴", labelStyle);
            }
        }

        private void DrawInputFocusHint()
        {
            if (matchManager.Phase != MatchPhase.Intro && matchManager.Phase != MatchPhase.Fighting)
            {
                return;
            }

            EnsureStyles();

            if (matchManager.Phase == MatchPhase.Intro)
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 280f, Screen.height - 96f, 560f, 48f),
                    "▶ Game 화면을 클릭한 뒤 키보드로 조작하세요\n한/영 키로 영문 입력 모드인지 확인하세요",
                    hintStyle);
                return;
            }

            if (sawGameplayInput || fightingPhaseStartedAt < 0f || Time.time - fightingPhaseStartedAt < 1.2f)
            {
                return;
            }

            GUI.Label(
                new Rect(Screen.width * 0.5f - 300f, Screen.height - 110f, 600f, 72f),
                "키 입력이 안 되면:\n"
                + "1) Unity 상단 Game 탭을 클릭\n"
                + "2) ▶ Pause 가 켜져 있지 않은지 확인\n"
                +                 "3) Console 의 Error Pause 끄기\n"
                + "4) Edit > Project Settings > Player > Active Input Handling = Both\n"
                + "5) 한/영 으로 영문 입력 모드",
                hintStyle);
        }

        private void DrawBanner()
        {
            if (matchManager.Phase == MatchPhase.Fighting && matchManager.RoundTimer > FightConstants.RoundDuration - 0.5f)
            {
                return;
            }

            if (matchManager.Phase == MatchPhase.Intro || matchManager.Phase == MatchPhase.RoundEnd || matchManager.Phase == MatchPhase.MatchEnd)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.42f, 520f, 60f), matchManager.StatusMessage, bannerStyle);
            }
        }

        private void SpawnFloatingText(Vector3 worldPosition, string text, Color color)
        {
            floatingTexts.Add(new FloatingCombatText
            {
                WorldPosition = worldPosition,
                Text = text,
                Color = color,
                Life = 0.75f
            });
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
