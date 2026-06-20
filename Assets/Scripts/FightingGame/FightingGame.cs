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
        private readonly System.Collections.Generic.List<FloatingCombatText> floatingTexts = new System.Collections.Generic.List<FloatingCombatText>();

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

        private void Update()
        {
            if (playerOne == null || playerTwo == null || matchManager == null)
            {
                return;
            }

            bool controlsEnabled = matchManager.ControlsEnabled;
            playerOne.Tick(Time.deltaTime, FighterInputReader.ReadPlayerOne(), controlsEnabled);
            playerTwo.Tick(Time.deltaTime, FighterInputReader.ReadPlayerTwo(), controlsEnabled);
            matchManager.Tick(Time.deltaTime);
            UpdateFloatingTexts(Time.deltaTime);

            if (matchManager.Phase == MatchPhase.MatchEnd && Input.GetKeyDown(KeyCode.R))
            {
                RestartMatch();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHealthBar(new Rect(40f, 24f, 420f, 28f), playerOne, false);
            DrawHealthBar(new Rect(Screen.width - 460f, 24f, 420f, 28f), playerTwo, true);
            DrawRoundInfo();
            DrawControlsHelp();
            DrawBanner();
            DrawFloatingTexts();
        }

        private void CreateFighters()
        {
            playerOne = FightSceneBuilder.CreateFighter("Player 1", 0, new Color(0.28f, 0.62f, 0.95f), new Color(0.12f, 0.22f, 0.42f), new Vector3(-3.5f, FightConstants.GroundY, 0f));
            playerTwo = FightSceneBuilder.CreateFighter("Player 2", 1, new Color(0.95f, 0.38f, 0.32f), new Color(0.42f, 0.12f, 0.12f), new Vector3(3.5f, FightConstants.GroundY, 0f));
            playerOne.SetOpponent(playerTwo);
            playerTwo.SetOpponent(playerOne);
            playerOne.Damaged += (_, damage) => SpawnFloatingText(playerOne.transform.position + Vector3.up * 1.8f, "-" + damage.ToString("0"), new Color(1f, 0.45f, 0.45f));
            playerTwo.Damaged += (_, damage) => SpawnFloatingText(playerTwo.transform.position + Vector3.up * 1.8f, "-" + damage.ToString("0"), new Color(1f, 0.45f, 0.45f));
            playerOne.LandedHit += (_, __, type) => SpawnFloatingText(playerTwo.transform.position + Vector3.up * 2.1f, type.ToString().ToUpper(), new Color(1f, 0.92f, 0.45f));
            playerTwo.LandedHit += (_, __, type) => SpawnFloatingText(playerOne.transform.position + Vector3.up * 2.1f, type.ToString().ToUpper(), new Color(1f, 0.92f, 0.45f));
        }

        private void HandleRoundStarted()
        {
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
            GUI.Label(new Rect(24f, y, 420f, 120f),
                "P1: A/D 이동 | W 점프 | S 가드 | J 약공 | K 킥 | L 강공",
                labelStyle);
            GUI.Label(new Rect(Screen.width - 444f, y, 420f, 120f),
                "P2: ←/→ 이동 | ↑ 점프 | ↓ 가드 | 1 약공 | 2 킥 | 3 강공",
                labelStyle);

            if (matchManager.Phase == MatchPhase.MatchEnd)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height - 48f, 240f, 30f), "R 키 — 재대결", labelStyle);
            }
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
