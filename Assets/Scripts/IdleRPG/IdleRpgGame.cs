using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleRPG
{
    public enum GameScreenMode
    {
        Battle,
        Lobby
    }

    public sealed class IdleRpgGame : MonoBehaviour
    {
        private const string SaveKey = "idle-rpg-save-v1";
        private const int MaxLogEntries = 8;
        private const int MaxOfflineSeconds = 2 * 60 * 60;
        private const int FormationSlotCount = 3;
        private const float BackdropPixelsPerUnit = 100f;
        private static readonly string[] CompanionSpriteResourceNames =
        {
            "Companions/companion_00_luna",
            "Companions/companion_01_mio",
            "Companions/companion_02_nari",
            "Companions/companion_03_aria",
            "Companions/companion_04_rin",
            "Companions/companion_05_chae",
            "Companions/companion_06_serin",
            "Companions/companion_07_haneul",
            "Companions/companion_08_rena",
            "Companions/companion_09_yuri",
            "Companions/companion_10_sia",
            "Companions/companion_11_iren"
        };

        private IdleRpgState state;
        private Transform heroRoot;
        private Transform heroSwordTransform;
        private Transform heroSwingTrailTransform;
        private Vector3 heroSwordRestLocalPosition;
        private float heroSwordRestLocalRotation;
        private float heroAttackSwingTimer;
        private const float HeroAttackSwingDuration = 0.4f;
        private Transform[] companionRoots;
        private Transform enemyRoot;
        private GameObject battleBackdropRoot;
        private GameObject lobbyBackdropRoot;
        private Transform lobbyHearthGlow;
        private Transform lobbyLanternGlow;
        private Transform lobbyMoonGlow;
        private Transform[] lobbyEmbers;
        private SpriteRenderer[] companionRenderers;
        private SpriteRenderer enemyRenderer;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private float saveTimer;
        private bool companionFormationScreenOpen;
        private bool companionGachaScreenOpen;
        private bool relicProbabilityScreenOpen;
        private bool companionProbabilityScreenOpen;
        private bool stageProgressScreenOpen;
        private bool companionDamageDrawerOpen;
        private GameScreenMode currentScreen = GameScreenMode.Battle;
        private readonly GachaCutsceneState gachaCutscene = new GachaCutsceneState();
        private int[] displayedCompanionIds;
        private float[] companionAttackTimers;
        private Texture2D[] companionPortraitTextures;
        private Sprite[] companionSprites;
        private Vector2 companionGachaScroll;
        private Vector2 relicGachaScroll;
        private Vector2 relicProbabilityScroll;
        private Vector2 companionProbabilityScroll;
        private Vector2 companionFormationScroll;
        private Vector2 stageSelectScroll;
        private Vector2 battleStatsScroll;
        private Vector2 recentCompanionPullScroll;
        private readonly List<int> recentCompanionPullIds = new List<int>();
        private readonly List<FloatingText> floatingTexts = new List<FloatingText>();
        private readonly List<CompanionAttackEffect> companionAttackEffects = new List<CompanionAttackEffect>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<IdleRpgGame>() != null)
            {
                return;
            }

            GameObject game = new GameObject("Idle RPG Game");
            game.AddComponent<IdleRpgGame>();
            DontDestroyOnLoad(game);
        }

        private void Awake()
        {
            ConfigureLandscapeDisplay();
            state = LoadState();
            BuildScene();
            UpdateBattleSceneVisibility();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            UpdateSceneObjects();
            UpdateLobbyAmbience(Time.deltaTime);
            UpdateFloatingTexts(Time.deltaTime);
            UpdateCompanionAttackEffects(Time.deltaTime);
            UpdateGachaCutscene(Time.deltaTime);
            UpdateBattleSceneVisibility();

            saveTimer += Time.deltaTime;
            if (saveTimer >= 3f)
            {
                SaveState();
                saveTimer = 0f;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveState();
            }
        }

        private void OnApplicationQuit()
        {
            SaveState();
        }

        private void ConfigureLandscapeDisplay()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;

#if UNITY_STANDALONE || UNITY_EDITOR
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
#endif
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (gachaCutscene.Active)
            {
                DrawGachaCutscene();
                return;
            }

            DrawScreenNavigationHeader();

            if (currentScreen == GameScreenMode.Battle)
            {
                DrawBattleScreenGui();
            }
            else
            {
                DrawLobbyScreenGui();
            }

            if (companionGachaScreenOpen)
            {
                DrawCompanionGachaScreen();
            }

            if (companionFormationScreenOpen)
            {
                DrawCompanionFormationScreen();
            }

            if (stageProgressScreenOpen)
            {
                DrawStageProgressScreen();
            }

            if (relicProbabilityScreenOpen)
            {
                DrawRelicProbabilityScreen();
            }

            if (companionProbabilityScreenOpen)
            {
                DrawCompanionProbabilityScreen();
            }
        }

        private void DrawScreenNavigationHeader()
        {
            Rect header = new Rect(12f, 12f, Screen.width - 24f, 52f);
            DrawPanel(header, new Color(0.05f, 0.08f, 0.15f, 0.92f));
            GUILayout.BeginArea(new Rect(header.x + 14f, header.y + 8f, header.width - 28f, header.height - 16f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(currentScreen == GameScreenMode.Battle ? "전투" : "로비", titleStyle, GUILayout.Width(72f));
            GUILayout.Label("스테이지 " + state.stage + "  |  전투력 " + FormatNumber(GetCombatPower()) + "  |  치명 " + Mathf.RoundToInt(GetEffectiveCritChance() * 100f) + "%  |  골드 " + FormatNumber(state.hero.gold) + "G  |  보석 " + FormatNumber(state.hero.gems) + "♦", labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("퀘스트", buttonStyle, GUILayout.Width(80f), GUILayout.Height(34f)))
            {
                stageProgressScreenOpen = true;
            }

            if (currentScreen == GameScreenMode.Battle)
            {
                if (GUILayout.Button("로비", buttonStyle, GUILayout.Width(88f), GUILayout.Height(34f)))
                {
                    currentScreen = GameScreenMode.Lobby;
                }
            }
            else if (GUILayout.Button("전투 복귀", buttonStyle, GUILayout.Width(108f), GUILayout.Height(34f)))
            {
                currentScreen = GameScreenMode.Battle;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawBattleScreenGui()
        {
            Rect leftPanel = new Rect(12f, 76f, 330f, Screen.height - 196f);
            Rect bottomPanel = new Rect(360f, Screen.height - 276f, Screen.width - 720f, 164f);
            Rect partyPanel = new Rect(24f, Screen.height - 102f, Screen.width - 48f, 86f);

            DrawBattleStatsPanel(leftPanel);
            DrawBattlePanel(bottomPanel);
            DrawPartyHud(partyPanel);
            DrawCompanionDamageDrawer();
            DrawCompanionAttackEffectsGui();
            DrawFloatingTextsGui();
            DrawEnemyTopBar(new Rect(Screen.width * 0.5f - 280f, 68f, 560f, 62f));
            DrawManualBattleHint();
            HandleBattleScreenInput();
        }

        private void HandleBattleScreenInput()
        {
            if (state.combatMode != CombatMode.Manual)
            {
                return;
            }

            if (companionFormationScreenOpen || stageProgressScreenOpen || companionGachaScreenOpen || relicProbabilityScreenOpen || companionProbabilityScreenOpen)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {
                return;
            }

            if (!IsManualBattleTapZone(currentEvent.mousePosition))
            {
                return;
            }

            if (ManualStrike())
            {
                currentEvent.Use();
            }
        }

        private bool IsManualBattleTapZone(Vector2 mousePosition)
        {
            Rect tapZone = new Rect(360f, 140f, Screen.width - 720f, Screen.height - 340f);
            return tapZone.Contains(mousePosition);
        }

        private void DrawManualBattleHint()
        {
            if (state.combatMode != CombatMode.Manual)
            {
                return;
            }

            Rect tapZone = new Rect(360f, 140f, Screen.width - 720f, Screen.height - 340f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(tapZone.x, tapZone.yMax - 42f, tapZone.width, 28f), "전투 화면 터치 = 공격", smallStyle);
            GUI.color = previous;
        }

        private void DrawLobbyScreenGui()
        {
            Rect leftPanel = new Rect(12f, 76f, 280f, Screen.height - 96f);
            Rect centerPanel = new Rect(304f, 76f, Screen.width - 676f, Screen.height - 96f);
            Rect rightColumn = new Rect(Screen.width - 352f, 76f, 340f, Screen.height - 96f);
            Rect upgradePanel = new Rect(rightColumn.x, rightColumn.y, rightColumn.width, rightColumn.height * 0.52f);
            Rect gachaPanel = new Rect(rightColumn.x, upgradePanel.yMax + 12f, rightColumn.width, rightColumn.height - upgradePanel.height - 12f);

            DrawLobbyStatsPanel(leftPanel);
            DrawLobbyCenterPanel(centerPanel);
            DrawUpgradePanel(upgradePanel);
            DrawRelicGachaPanel(gachaPanel);
        }

        private bool IsLobbyModalOpen()
        {
            return companionFormationScreenOpen || companionGachaScreenOpen || relicProbabilityScreenOpen || companionProbabilityScreenOpen;
        }

        private void OpenCompanionFormationScreen()
        {
            if (companionGachaScreenOpen)
            {
                return;
            }

            stageProgressScreenOpen = false;
            companionFormationScreenOpen = true;
        }

        private void OpenCompanionGachaScreen()
        {
            if (companionFormationScreenOpen)
            {
                return;
            }

            stageProgressScreenOpen = false;
            companionGachaScreenOpen = true;
        }

        private void DrawLobbyCenterPanel(Rect rect)
        {
            DrawPanel(rect, new Color(0.08f, 0.07f, 0.13f, 0.94f));
            GUILayout.BeginArea(new Rect(rect.x + 20f, rect.y + 18f, rect.width - 40f, rect.height - 36f));
            GUILayout.Label("모험가의 거점", titleStyle);
            GUILayout.Label("로비에서 강화, 유물 뽑기, 동료 소환, 편성을 진행할 수 있습니다.", labelStyle);
            GUILayout.Space(12f);
            DrawStat("보유 보석", FormatNumber(state.hero.gems) + "♦");
            DrawStat("동료 보유", GetOwnedCompanionCount() + " / " + IdleRpgBalance.Companions.Length);
            DrawStat("전투력", FormatNumber(GetCombatPower()));
            GUILayout.Space(10f);
            DrawQuestSummaryCompact();
            GUILayout.Space(10f);
            if (IsLobbyModalOpen())
            {
                string modalMessage = companionFormationScreenOpen
                    ? "동료 편성 화면이 열려 있습니다."
                    : companionGachaScreenOpen
                        ? "동료 소환 화면이 열려 있습니다."
                        : relicProbabilityScreenOpen
                            ? "유물 확률표가 열려 있습니다."
                            : "동료 소환 확률표가 열려 있습니다.";
                GUILayout.Label(modalMessage, smallStyle);
                GUILayout.Label("닫기 전에는 다른 화면을 열 수 없습니다.", smallStyle);
            }
            else
            {
                GUILayout.Label("왼쪽에서 동료 편성 또는 동료 소환을 열어주세요.", smallStyle);
            }

            GUILayout.EndArea();
        }

        private void BuildScene()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.22f);

            CreateBackdrop(camera);
            CreateLobbyBackdrop(camera);
            CreateCompanionArt();
            heroRoot = CreateHero();
            companionRoots = new Transform[FormationSlotCount];
            companionRenderers = new SpriteRenderer[FormationSlotCount];
            displayedCompanionIds = new int[FormationSlotCount];
            companionAttackTimers = new float[FormationSlotCount];
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                displayedCompanionIds[slotIndex] = -999;
                companionRoots[slotIndex] = CreateCompanionVisual(slotIndex);
            }

            enemyRoot = CreateEnemyVisual();
            UpdateSceneObjects();
        }

        private void CreateBackdrop(Camera camera)
        {
            GameObject root = new GameObject("Battle Backdrop");
            root.transform.SetParent(transform);
            battleBackdropRoot = root;

            Transform sky = CreateBackdropLayer(root.transform, "Sky", "Background/bg_sky", -100);
            if (sky != null)
            {
                BackgroundFill fill = sky.gameObject.AddComponent<BackgroundFill>();
                fill.fitMode = BackgroundFill.FitMode.Cover;
                fill.targetCamera = camera;
            }

            Transform mountains = CreateBackdropLayer(root.transform, "Mountains", "Background/bg_mountains", -80);
            if (mountains != null)
            {
                mountains.localScale = new Vector3(1.35f, 1.35f, 1f);
                ParallaxLayer parallax = mountains.gameObject.AddComponent<ParallaxLayer>();
                parallax.targetCamera = camera;
                parallax.parallaxFactor = 0.6f;
            }

            Transform forest = CreateBackdropLayer(root.transform, "Forest", "Background/bg_forest", -60);
            if (forest != null)
            {
                forest.localScale = new Vector3(1.35f, 1.35f, 1f);
                ParallaxLayer parallax = forest.gameObject.AddComponent<ParallaxLayer>();
                parallax.targetCamera = camera;
                parallax.parallaxFactor = 0.2f;
            }

            if (sky == null && mountains == null && forest == null)
            {
                Debug.LogWarning("[IdleRpgGame] 배경 이미지를 찾지 못해 단색 배경을 사용합니다.");
            }
        }

        private void CreateLobbyBackdrop(Camera camera)
        {
            GameObject root = new GameObject("Lobby Backdrop");
            root.transform.SetParent(transform);
            lobbyBackdropRoot = root;

            Transform sky = CreateBackdropLayer(root.transform, "Lobby Sky", "Background/bg_sky", -112);
            if (sky != null)
            {
                sky.position = new Vector3(0f, 1.4f, 5f);
                sky.localScale = new Vector3(1.55f, 1.55f, 1f);
                SpriteRenderer skyRenderer = sky.GetComponent<SpriteRenderer>();
                if (skyRenderer != null)
                {
                    skyRenderer.color = new Color(0.42f, 0.48f, 0.82f);
                }

                BackgroundFill fill = sky.gameObject.AddComponent<BackgroundFill>();
                fill.fitMode = BackgroundFill.FitMode.Cover;
                fill.targetCamera = camera;
            }

            Transform mountains = CreateBackdropLayer(root.transform, "Lobby Mountains", "Background/bg_mountains", -105);
            if (mountains != null)
            {
                mountains.position = new Vector3(0f, -0.35f, 4.8f);
                mountains.localScale = new Vector3(1.45f, 1.25f, 1f);
                SpriteRenderer mountainRenderer = mountains.GetComponent<SpriteRenderer>();
                if (mountainRenderer != null)
                {
                    mountainRenderer.color = new Color(0.22f, 0.18f, 0.34f);
                }

                ParallaxLayer parallax = mountains.gameObject.AddComponent<ParallaxLayer>();
                parallax.targetCamera = camera;
                parallax.parallaxFactor = 0.18f;
            }

            GameObject ceiling = CreateSpriteObject("Lobby Ceiling", CreateVerticalGradientSprite(
                new Color(0.08f, 0.05f, 0.10f),
                new Color(0.16f, 0.10f, 0.18f),
                8,
                64));
            ceiling.transform.position = new Vector3(0f, 3.4f, 3.5f);
            ceiling.transform.localScale = new Vector3(18f, 3.2f, 1f);
            SetSortingOrder(ceiling, -98);

            GameObject wall = CreateSpriteObject("Lobby Wall", CreateVerticalGradientSprite(
                new Color(0.16f, 0.10f, 0.08f),
                new Color(0.28f, 0.18f, 0.12f),
                8,
                96));
            wall.transform.position = new Vector3(0f, 0.15f, 4f);
            wall.transform.localScale = new Vector3(18f, 10.2f, 1f);
            SetSortingOrder(wall, -96);

            GameObject windowFrame = CreateSpriteObject("Window Frame", CreateSolidSprite(new Color(0.34f, 0.22f, 0.14f), 8, 8));
            windowFrame.transform.position = new Vector3(0f, 1.55f, 3.2f);
            windowFrame.transform.localScale = new Vector3(5.4f, 3.2f, 1f);
            SetSortingOrder(windowFrame, -94);

            GameObject windowGlass = CreateSpriteObject("Window Glass", CreateSolidSprite(new Color(0.52f, 0.68f, 0.95f, 0.22f), 8, 8));
            windowGlass.transform.position = new Vector3(0f, 1.55f, 3.15f);
            windowGlass.transform.localScale = new Vector3(4.8f, 2.7f, 1f);
            SetSortingOrder(windowGlass, -93);

            lobbyMoonGlow = CreateSpriteObject("Moon Glow", CreateCircleSprite(new Color(0.92f, 0.96f, 1f, 0.18f), 96)).transform;
            lobbyMoonGlow.position = new Vector3(1.1f, 2.05f, 3.1f);
            lobbyMoonGlow.localScale = Vector3.one * 1.8f;
            SetSortingOrder(lobbyMoonGlow.gameObject, -92);

            GameObject moon = CreateSpriteObject("Moon", CreateCircleSprite(new Color(0.98f, 0.98f, 0.88f, 0.95f), 64));
            moon.transform.position = new Vector3(1.1f, 2.05f, 3.05f);
            moon.transform.localScale = Vector3.one * 0.42f;
            SetSortingOrder(moon, -91);

            for (int index = 0; index < 6; index += 1)
            {
                GameObject shelf = CreateSpriteObject("Bookshelf", CreateSolidSprite(new Color(0.24f + index * 0.02f, 0.14f, 0.10f), 4, 8));
                float side = index < 3 ? -1f : 1f;
                float offset = index % 3;
                shelf.transform.position = new Vector3(side * (5.2f + offset * 0.15f), -0.2f + offset * 0.55f, 2.8f);
                shelf.transform.localScale = new Vector3(1.8f, 2.4f + offset * 0.35f, 1f);
                SetSortingOrder(shelf, -89);
            }

            GameObject rug = CreateSpriteObject("Lobby Rug", CreateCircleSprite(new Color(0.62f, 0.22f, 0.18f, 0.55f), 96));
            rug.transform.position = new Vector3(0.4f, -2.55f, 1.5f);
            rug.transform.localScale = new Vector3(5.8f, 1.4f, 1f);
            SetSortingOrder(rug, -88);

            GameObject floor = CreateSpriteObject("Lobby Floor", CreateVerticalGradientSprite(
                new Color(0.20f, 0.12f, 0.07f),
                new Color(0.34f, 0.20f, 0.11f),
                8,
                24));
            floor.transform.position = new Vector3(0f, -3.15f, 1.2f);
            floor.transform.localScale = new Vector3(18f, 2.4f, 1f);
            SetSortingOrder(floor, -87);

            lobbyHearthGlow = CreateSpriteObject("Hearth Glow", CreateCircleSprite(new Color(1f, 0.48f, 0.18f, 0.42f), 96)).transform;
            lobbyHearthGlow.position = new Vector3(-4.1f, -1.15f, 0.8f);
            lobbyHearthGlow.localScale = Vector3.one * 3.8f;
            SetSortingOrder(lobbyHearthGlow.gameObject, -86);

            GameObject hearth = CreateSpriteObject("Hearth", CreateCircleSprite(new Color(1f, 0.68f, 0.24f, 0.82f), 64));
            hearth.transform.position = new Vector3(-4.1f, -1.3f, 0.7f);
            hearth.transform.localScale = Vector3.one * 0.62f;
            SetSortingOrder(hearth, -85);

            lobbyLanternGlow = CreateSpriteObject("Lantern Glow", CreateCircleSprite(new Color(1f, 0.82f, 0.38f, 0.34f), 96)).transform;
            lobbyLanternGlow.position = new Vector3(4.2f, 0.55f, 0.8f);
            lobbyLanternGlow.localScale = Vector3.one * 2.8f;
            SetSortingOrder(lobbyLanternGlow.gameObject, -86);

            GameObject lantern = CreateSpriteObject("Lantern", CreateCircleSprite(new Color(1f, 0.92f, 0.52f, 0.88f), 48));
            lantern.transform.position = new Vector3(4.2f, 0.55f, 0.7f);
            lantern.transform.localScale = Vector3.one * 0.34f;
            SetSortingOrder(lantern, -84);

            lobbyEmbers = new Transform[10];
            for (int index = 0; index < lobbyEmbers.Length; index += 1)
            {
                GameObject ember = CreateSpriteObject("Ember", CreateCircleSprite(new Color(1f, 0.62f + index * 0.03f, 0.18f, 0.75f), 16));
                ember.transform.position = new Vector3(-4.1f + UnityEngine.Random.Range(-0.35f, 0.35f), -1.0f + index * 0.08f, 0.6f);
                ember.transform.localScale = Vector3.one * (0.08f + index * 0.01f);
                SetSortingOrder(ember, -83);
                lobbyEmbers[index] = ember.transform;
            }

            for (int index = 0; index < 8; index += 1)
            {
                GameObject garland = CreateSpriteObject("Garland Light", CreateCircleSprite(new Color(1f, 0.88f, 0.45f, 0.55f), 24));
                garland.transform.position = new Vector3(-3.4f + index * 0.95f, 2.75f, 2.5f);
                garland.transform.localScale = Vector3.one * 0.12f;
                SetSortingOrder(garland, -82);
            }
        }

        private void UpdateLobbyAmbience(float deltaTime)
        {
            if (currentScreen != GameScreenMode.Lobby || lobbyBackdropRoot == null || !lobbyBackdropRoot.activeSelf)
            {
                return;
            }

            float hearthPulse = 0.88f + Mathf.Sin(Time.time * 3.1f) * 0.14f;
            if (lobbyHearthGlow != null)
            {
                lobbyHearthGlow.localScale = Vector3.one * 3.8f * hearthPulse;
            }

            float lanternPulse = 0.92f + Mathf.Sin(Time.time * 2.2f + 0.8f) * 0.1f;
            if (lobbyLanternGlow != null)
            {
                lobbyLanternGlow.localScale = Vector3.one * 2.8f * lanternPulse;
            }

            if (lobbyMoonGlow != null)
            {
                lobbyMoonGlow.localScale = Vector3.one * (1.8f + Mathf.Sin(Time.time * 1.4f) * 0.08f);
            }

            if (lobbyEmbers != null)
            {
                for (int index = 0; index < lobbyEmbers.Length; index += 1)
                {
                    Transform ember = lobbyEmbers[index];
                    if (ember == null)
                    {
                        continue;
                    }

                    Vector3 position = ember.position;
                    position.y += deltaTime * (0.55f + index * 0.04f);
                    position.x += Mathf.Sin(Time.time * 2.4f + index) * deltaTime * 0.12f;
                    if (position.y > -0.35f)
                    {
                        position = new Vector3(-4.1f + UnityEngine.Random.Range(-0.35f, 0.35f), -1.15f, 0.6f);
                    }

                    ember.position = position;
                }
            }
        }

        private Transform CreateBackdropLayer(Transform parent, string name, string resourcePath, int sortingOrder)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning("[IdleRpgGame] 배경 텍스처를 찾을 수 없습니다: Resources/" + resourcePath);
                return null;
            }

            GameObject layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            layer.transform.position = new Vector3(0f, 0f, 5f);

            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                BackdropPixelsPerUnit);
            renderer.sortingOrder = sortingOrder;
            return layer.transform;
        }

        private Transform CreateHero()
        {
            GameObject root = new GameObject("Hero");
            root.transform.position = new Vector3(-2.35f, -1.35f, 0f);

            AddPart(root.transform, "Body", new Color(0.24f, 0.41f, 1f), new Vector2(0.8f, 1.2f), new Vector3(0f, 0.25f, 0f));
            AddPart(root.transform, "Chest", new Color(0.56f, 0.69f, 1f), new Vector2(0.55f, 0.36f), new Vector3(0f, 0.42f, -0.01f));
            AddPart(root.transform, "Head", new Color(0.96f, 0.76f, 0.54f), new Vector2(0.58f, 0.56f), new Vector3(0f, 1.05f, 0f));
            AddPart(root.transform, "Hair", new Color(0.18f, 0.13f, 0.25f), new Vector2(0.68f, 0.22f), new Vector3(0f, 1.36f, -0.01f));
            AddPart(root.transform, "Left Leg", new Color(0.08f, 0.10f, 0.18f), new Vector2(0.22f, 0.56f), new Vector3(-0.18f, -0.58f, 0f));
            AddPart(root.transform, "Right Leg", new Color(0.08f, 0.10f, 0.18f), new Vector2(0.22f, 0.56f), new Vector3(0.22f, -0.58f, 0f));

            GameObject sword = AddPart(root.transform, "Sword", new Color(0.86f, 0.93f, 1f), new Vector2(0.16f, 1.05f), new Vector3(0.68f, 0.62f, -0.02f), -38f);
            heroSwordTransform = sword.transform;
            heroSwordRestLocalPosition = sword.transform.localPosition;
            heroSwordRestLocalRotation = -38f;
            SetSortingOrder(sword, 8);

            GameObject swingTrail = CreateSpriteObject("Swing Trail", CreateSolidSprite(new Color(1f, 0.92f, 0.45f, 0f), 6, 28));
            swingTrail.transform.SetParent(root.transform);
            swingTrail.transform.localPosition = new Vector3(0.72f, 0.82f, -0.04f);
            swingTrail.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            swingTrail.transform.localScale = Vector3.zero;
            SetSortingOrder(swingTrail, 9);
            heroSwingTrailTransform = swingTrail.transform;

            return root.transform;
        }

        private void CreateCompanionArt()
        {
            companionPortraitTextures = new Texture2D[IdleRpgBalance.Companions.Length];
            companionSprites = new Sprite[IdleRpgBalance.Companions.Length];

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                Texture2D texture = LoadCompanionTexture(index);
                companionPortraitTextures[index] = texture;
                companionSprites[index] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.08f), 96f);
            }
        }

        private Texture2D LoadCompanionTexture(int companionIndex)
        {
            if (companionIndex >= 0 && companionIndex < CompanionSpriteResourceNames.Length)
            {
                Texture2D assetTexture = Resources.Load<Texture2D>(CompanionSpriteResourceNames[companionIndex]);
                if (assetTexture != null)
                {
                    assetTexture.filterMode = FilterMode.Point;
                    return assetTexture;
                }
            }

            return CreateCompanionTexture(IdleRpgBalance.Companions[companionIndex]);
        }

        private Transform CreateCompanionVisual(int slotIndex)
        {
            GameObject root = new GameObject("Pixel Companion " + (slotIndex + 1));
            root.transform.position = new Vector3(-1.45f, -1.45f, 0f);
            root.transform.localScale = Vector3.one * (0.56f - slotIndex * 0.03f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sortingOrder = -2 + slotIndex;
            companionRenderers[slotIndex] = renderer;

            return root.transform;
        }

        private Transform CreateEnemyVisual()
        {
            GameObject root = new GameObject("Enemy");
            root.transform.position = new Vector3(2.55f, -1.1f, 0f);

            GameObject body = CreateSpriteObject("Enemy Body", CreateCircleSprite(Color.white, 96));
            body.transform.SetParent(root.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one;
            enemyRenderer = body.GetComponent<SpriteRenderer>();

            AddPart(root.transform, "Left Eye", new Color(0.02f, 0.03f, 0.05f), new Vector2(0.12f, 0.12f), new Vector3(-0.22f, 0.16f, -0.03f));
            AddPart(root.transform, "Right Eye", new Color(0.02f, 0.03f, 0.05f), new Vector2(0.12f, 0.12f), new Vector3(0.22f, 0.16f, -0.03f));

            return root.transform;
        }

        private Texture2D CreateCompanionTexture(CompanionDefinition companion)
        {
            Texture2D texture = new Texture2D(64, 96, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[texture.width * texture.height];
            for (int index = 0; index < pixels.Length; index += 1)
            {
                pixels[index] = Color.clear;
            }

            texture.SetPixels(pixels);

            int style = companion.Id % 6;
            Color skin = new Color(1f, 0.79f, 0.66f);
            Color skinShade = new Color(0.86f, 0.55f, 0.50f);
            Color skinLight = new Color(1f, 0.90f, 0.76f);
            Color shadow = new Color(0.16f, 0.10f, 0.16f, 0.92f);
            Color eye = new Color(0.08f, 0.07f, 0.12f);
            Color blush = new Color(1f, 0.42f, 0.48f, 0.75f);
            Color hairShade = Color.Lerp(companion.HairColor, Color.black, 0.30f);
            Color hairDeep = Color.Lerp(companion.HairColor, Color.black, 0.48f);
            Color hairLight = Color.Lerp(companion.HairColor, Color.white, 0.30f);
            Color outfitShade = Color.Lerp(companion.OutfitColor, Color.black, 0.26f);
            Color outfitLight = Color.Lerp(companion.OutfitColor, Color.white, 0.22f);
            Color accentLight = Color.Lerp(companion.AccentColor, Color.white, 0.34f);
            Color outline = new Color(0.08f, 0.06f, 0.10f, 0.95f);

            SetPixelEllipse(texture, 32, 92, 21, 4, new Color(0f, 0f, 0f, 0.28f));

            // Hair mass and individual silhouettes.
            SetPixelEllipse(texture, 32, 27, 22, 20, hairDeep);
            SetPixelEllipse(texture, 32, 24, 19, 17, hairShade);
            SetPixelEllipse(texture, 32, 22, 16, 13, companion.HairColor);
            SetPixelBlock(texture, 16, 29, 6, 36, hairDeep);
            SetPixelBlock(texture, 42, 29, 6, 36, hairDeep);
            SetPixelBlock(texture, 20, 13, 24, 7, companion.HairColor);
            SetPixelBlock(texture, 23, 11, 16, 4, hairLight);

            if (style == 1 || style == 4)
            {
                SetPixelBlock(texture, 11, 22, 7, 35, hairDeep);
                SetPixelBlock(texture, 46, 22, 7, 35, hairDeep);
                SetPixelBlock(texture, 13, 25, 5, 24, companion.HairColor);
                SetPixelBlock(texture, 46, 25, 5, 24, companion.HairColor);
            }

            if (style == 2)
            {
                SetPixelBlock(texture, 17, 47, 8, 22, hairDeep);
                SetPixelBlock(texture, 39, 47, 8, 22, hairDeep);
                SetPixelBlock(texture, 20, 51, 4, 14, hairLight);
                SetPixelBlock(texture, 40, 51, 4, 14, hairLight);
            }

            if (style == 3 || companion.Rarity == RelicRarity.Legendary)
            {
                SetPixelBlock(texture, 18, 7, 28, 5, companion.AccentColor);
                SetPixelBlock(texture, 22, 3, 4, 8, accentLight);
                SetPixelBlock(texture, 31, 1, 4, 10, accentLight);
                SetPixelBlock(texture, 40, 3, 4, 8, accentLight);
            }

            if (style == 5)
            {
                SetPixelBlock(texture, 14, 13, 8, 10, companion.HairColor);
                SetPixelBlock(texture, 42, 13, 8, 10, companion.HairColor);
                SetPixelBlock(texture, 16, 10, 5, 5, accentLight);
                SetPixelBlock(texture, 43, 10, 5, 5, accentLight);
            }

            // Face, eyes, smile.
            SetPixelEllipse(texture, 32, 34, 17, 18, skin);
            SetPixelEllipse(texture, 32, 29, 14, 9, skinLight);
            SetPixelBlock(texture, 18, 38, 4, 6, skinShade);
            SetPixelBlock(texture, 42, 38, 4, 6, skinShade);
            SetPixelBlock(texture, 21, 33, 7, 6, Color.white);
            SetPixelBlock(texture, 36, 33, 7, 6, Color.white);
            SetPixelBlock(texture, 23, 34, 4, 5, eye);
            SetPixelBlock(texture, 37, 34, 4, 5, eye);
            SetPixelBlock(texture, 24, 34, 2, 2, companion.AccentColor);
            SetPixelBlock(texture, 38, 34, 2, 2, companion.AccentColor);
            SetPixelBlock(texture, 25, 33, 1, 1, Color.white);
            SetPixelBlock(texture, 39, 33, 1, 1, Color.white);
            SetPixelBlock(texture, 22, 43, 4, 2, blush);
            SetPixelBlock(texture, 38, 43, 4, 2, blush);
            SetPixelBlock(texture, 28, 45, 8, 2, new Color(0.42f, 0.15f, 0.20f));
            SetPixelBlock(texture, 30, 46, 4, 1, new Color(1f, 0.66f, 0.74f));

            // Neck, body, dress and limbs.
            SetPixelBlock(texture, 28, 50, 8, 7, skin);
            SetPixelBlock(texture, 22, 55, 20, 12, companion.OutfitColor);
            SetPixelBlock(texture, 17, 65, 30, 16, outfitShade);
            SetPixelBlock(texture, 21, 56, 22, 5, outfitLight);
            SetPixelBlock(texture, 29, 55, 7, 22, companion.AccentColor);
            SetPixelBlock(texture, 26, 67, 12, 4, accentLight);
            SetPixelBlock(texture, 16, 57, 7, 21, skin);
            SetPixelBlock(texture, 41, 57, 7, 21, skin);
            SetPixelBlock(texture, 13, 75, 8, 5, companion.AccentColor);
            SetPixelBlock(texture, 43, 75, 8, 5, companion.AccentColor);
            SetPixelBlock(texture, 22, 81, 8, 10, skinShade);
            SetPixelBlock(texture, 35, 81, 8, 10, skinShade);
            SetPixelBlock(texture, 19, 91, 11, 3, shadow);
            SetPixelBlock(texture, 34, 91, 11, 3, shadow);

            // Outfit polish.
            SetPixelLine(texture, 18, 66, 46, 66, companion.AccentColor);
            SetPixelLine(texture, 21, 72, 43, 72, outfitLight);
            SetPixelBlock(texture, 30, 61, 4, 4, accentLight);
            SetPixelBlock(texture, 12, 55, 7, 5, companion.AccentColor);
            SetPixelBlock(texture, 45, 55, 7, 5, companion.AccentColor);
            SetPixelBlock(texture, 13, 53, 4, 2, accentLight);
            SetPixelBlock(texture, 47, 53, 4, 2, accentLight);

            // Decorative collection-game sparkle frame details.
            SetPixelBlock(texture, 5, 18, 3, 3, accentLight);
            SetPixelBlock(texture, 7, 16, 1, 7, accentLight);
            SetPixelBlock(texture, 3, 20, 7, 1, accentLight);
            SetPixelBlock(texture, 55, 25, 3, 3, accentLight);
            SetPixelBlock(texture, 57, 23, 1, 7, accentLight);
            SetPixelBlock(texture, 53, 27, 7, 1, accentLight);

            if (companion.Rarity == RelicRarity.Legendary)
            {
                SetPixelEllipse(texture, 32, 6, 19, 4, new Color(1f, 0.92f, 0.30f, 0.45f));
                SetPixelBlock(texture, 9, 36, 4, 10, accentLight);
                SetPixelBlock(texture, 51, 36, 4, 10, accentLight);
                SetPixelLine(texture, 8, 46, 17, 54, companion.AccentColor);
                SetPixelLine(texture, 56, 46, 47, 54, companion.AccentColor);
            }

            if (companion.Rarity == RelicRarity.Epic)
            {
                SetPixelBlock(texture, 15, 10, 7, 5, companion.AccentColor);
                SetPixelBlock(texture, 42, 10, 7, 5, companion.AccentColor);
                SetPixelBlock(texture, 17, 8, 3, 3, accentLight);
                SetPixelBlock(texture, 44, 8, 3, 3, accentLight);
            }

            if (companion.Rarity == RelicRarity.Rare)
            {
                SetPixelBlock(texture, 45, 18, 6, 5, companion.AccentColor);
                SetPixelBlock(texture, 47, 16, 3, 3, accentLight);
            }

            // Crisp dark outline last for a cleaner pixel-doll silhouette.
            SetPixelLine(texture, 20, 18, 14, 31, outline);
            SetPixelLine(texture, 44, 18, 50, 31, outline);
            SetPixelLine(texture, 18, 55, 13, 78, outline);
            SetPixelLine(texture, 46, 55, 51, 78, outline);

            texture.Apply();
            return texture;
        }

        private void SetPixelBlock(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (int px = x; px < x + width; px += 1)
            {
                for (int py = y; py < y + height; py += 1)
                {
                    SetPixel(texture, px, py, color);
                }
            }
        }

        private void SetPixelEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
        {
            for (int px = centerX - radiusX; px <= centerX + radiusX; px += 1)
            {
                for (int py = centerY - radiusY; py <= centerY + radiusY; py += 1)
                {
                    float normalizedX = (px - centerX) / Mathf.Max(1f, radiusX);
                    float normalizedY = (py - centerY) / Mathf.Max(1f, radiusY);
                    if (normalizedX * normalizedX + normalizedY * normalizedY <= 1f)
                    {
                        SetPixel(texture, px, py, color);
                    }
                }
            }
        }

        private void SetPixelLine(Texture2D texture, int startX, int startY, int endX, int endY, Color color)
        {
            int dx = Mathf.Abs(endX - startX);
            int dy = -Mathf.Abs(endY - startY);
            int stepX = startX < endX ? 1 : -1;
            int stepY = startY < endY ? 1 : -1;
            int error = dx + dy;
            int x = startX;
            int y = startY;

            while (true)
            {
                SetPixel(texture, x, y, color);
                if (x == endX && y == endY)
                {
                    break;
                }

                int doubledError = 2 * error;
                if (doubledError >= dy)
                {
                    error += dy;
                    x += stepX;
                }

                if (doubledError <= dx)
                {
                    error += dx;
                    y += stepY;
                }
            }
        }

        private void SetPixel(Texture2D texture, int x, int y, Color color)
        {
            if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            {
                texture.SetPixel(x, texture.height - 1 - y, color);
            }
        }

        private GameObject AddPart(Transform parent, string name, Color color, Vector2 scale, Vector3 localPosition, float rotation = 0f)
        {
            GameObject part = CreateSpriteObject(name, CreateSolidSprite(color, 8, 8));
            part.transform.SetParent(parent);
            part.transform.localPosition = localPosition;
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            SetSortingOrder(part, 0);
            return part;
        }

        private GameObject CreateSpriteObject(string name, Sprite sprite)
        {
            GameObject spriteObject = new GameObject(name);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return spriteObject;
        }

        private void SetSortingOrder(GameObject spriteObject, int sortingOrder)
        {
            SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }

        private Sprite CreateSolidSprite(Color color, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[width * height];
            for (int index = 0; index < pixels.Length; index += 1)
            {
                pixels[index] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 8f);
        }

        private Sprite CreateVerticalGradientSprite(Color bottom, Color top, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y += 1)
            {
                Color color = Color.Lerp(bottom, top, y / Mathf.Max(1f, height - 1f));
                for (int x = 0; x < width; x += 1)
                {
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 8f);
        }

        private Sprite CreateCircleSprite(Color color, int size)
        {
            Texture2D texture = new Texture2D(size, size);
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.48f;

            for (int y = 0; y < size; y += 1)
            {
                for (int x = 0; x < size; x += 1)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - distance) * color.a;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        private void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            float safeDelta = Mathf.Min(deltaTime, 5f);
            state.hero.hp = Mathf.Min(GetEffectiveMaxHp(), state.hero.hp + GetEffectiveRegen() * safeDelta);
            state.combat.enemyAttack += safeDelta;

            if (state.combatMode == CombatMode.Auto)
            {
                state.combat.heroAttack += safeDelta;
                while (state.combat.heroAttack >= 1f)
                {
                    state.combat.heroAttack -= 1f;
                    ApplyHeroDamage(1f);
                }
            }

            while (state.combat.enemyAttack >= 1.45f)
            {
                state.combat.enemyAttack -= 1.45f;
                ApplyEnemyDamage();
            }
        }

        private bool ManualStrike()
        {
            if (state.hero.hp <= 0f)
            {
                return false;
            }

            ApplyHeroDamage(1f);
            SaveState();
            return true;
        }

        private void ApplyHeroDamage(float multiplier)
        {
            heroAttackSwingTimer = HeroAttackSwingDuration;
            bool critical = UnityEngine.Random.value < GetEffectiveCritChance();
            float criticalMultiplier = critical ? GetEffectiveCritMultiplier() : 1f;
            int damage = Mathf.Max(1, Mathf.FloorToInt(GetEffectiveAttack() * multiplier * criticalMultiplier));

            state.enemy.hp = Mathf.Max(0f, state.enemy.hp - damage);
            AddFloatingText(critical ? "CRIT " + damage : damage.ToString(), new Vector2(0.68f, 0.52f), critical ? Color.yellow : Color.white);

            if (state.enemy.hp <= 0f)
            {
                DefeatEnemy();
                return;
            }

            ApplyCompanionAssists();
        }

        private void ApplyCompanionAssists()
        {
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(slotIndex);
                if (companionId < 0 || state.enemy.hp <= 0f)
                {
                    continue;
                }

                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                int level = Mathf.Max(1, GetCompanionLevel(state, companionId));
                int assistDamage = IdleRpgBalance.GetCompanionSkillDamage(companion, level, state.hero.level);
                state.enemy.hp = Mathf.Max(0f, state.enemy.hp - assistDamage);
                state.stageCompanionDamage.Add(companionId, assistDamage);
                AddCompanionAttackEffect(companion, slotIndex, assistDamage);

                if (state.enemy.hp <= 0f)
                {
                    DefeatEnemy();
                    return;
                }
            }
        }

        private void ApplyEnemyDamage()
        {
            if (state.enemy.hp <= 0f)
            {
                return;
            }

            state.hero.hp = Mathf.Max(0f, state.hero.hp - state.enemy.attack);
            AddFloatingText("-" + state.enemy.attack, new Vector2(0.34f, 0.53f), new Color(1f, 0.55f, 0.55f));

            if (state.hero.hp <= 0f)
            {
                RestartCurrentStageAfterDeath();
            }
        }

        private void RestartCurrentStageAfterDeath()
        {
            state.hero.hp = GetEffectiveMaxHp();
            state.stageProgress = 0;
            state.enemy = IdleRpgBalance.CreateEnemy(state.stage);
            state.combat.heroAttack = 0f;
            state.combat.enemyAttack = 0f;
            ResetStageCompanionDamage();
            AddLog("용사가 쓰러져 스테이지 " + state.stage + "를 처음부터 다시 시작합니다.");
            AddFloatingText("STAGE RETRY", new Vector2(0.50f, 0.66f), new Color(1f, 0.62f, 0.35f));
        }

        private void DefeatEnemy()
        {
            EnemyState defeated = state.enemy;
            state.hero.gold += defeated.rewardGold;
            state.stats.totalGold += defeated.rewardGold;
            state.stats.kills += 1;
            int gemReward = defeated.isBoss ? IdleRpgBalance.BossKillGemReward : IdleRpgBalance.NormalKillGemReward;
            state.hero.gems += gemReward;
            state.stageProgress += 1;
            GainXp(defeated.rewardXp);
            AddFloatingText("+" + defeated.rewardGold + "G", new Vector2(0.70f, 0.60f), new Color(1f, 0.82f, 0.4f));
            if (gemReward > 0)
            {
                AddFloatingText("+" + gemReward + "♦", new Vector2(0.62f, 0.54f), new Color(0.58f, 0.86f, 1f));
            }

            if (state.stageProgress >= 5)
            {
                state.stageProgress = 0;
                ResetStageCompanionDamage();
                if (state.stage >= state.stats.highestStage)
                {
                    state.stage += 1;
                    state.stats.highestStage = state.stage;
                    state.hero.gems += IdleRpgBalance.StageClearGemReward;
                    AddLog("스테이지 " + state.stage + "에 도달했습니다. 보석 +" + IdleRpgBalance.StageClearGemReward);
                }
                else
                {
                    AddLog("스테이지 " + state.stage + " 파밍을 계속합니다.");
                }
            }
            else
            {
                AddLog(defeated.name + " 처치! +" + defeated.rewardGold + "골드");
            }

            state.enemy = IdleRpgBalance.CreateEnemy(state.stage);
            RefreshQuestProgress();
            UpdatePeakCombatPower();
        }

        private void ResetStageCompanionDamage()
        {
            if (state.stageCompanionDamage == null)
            {
                state.stageCompanionDamage = new CompanionDamageStats();
            }

            state.stageCompanionDamage.Reset();
        }

        private void GainXp(int amount)
        {
            int levelsGained = GainXp(state, amount);
            for (int index = 0; index < levelsGained; index += 1)
            {
                AddFloatingText("LEVEL " + state.hero.level, new Vector2(0.36f, 0.66f), new Color(0.49f, 0.97f, 0.76f));
                AddLog("레벨 " + state.hero.level + " 달성!");
            }
        }

        private int GainXp(IdleRpgState targetState, int amount)
        {
            int levelsGained = 0;
            targetState.hero.xp += amount;
            targetState.stats.totalXp += amount;

            while (targetState.hero.xp >= targetState.hero.xpToNext)
            {
                targetState.hero.xp -= targetState.hero.xpToNext;
                targetState.hero.level += 1;
                targetState.hero.xpToNext = Mathf.FloorToInt(targetState.hero.xpToNext * 1.35f + 10f);
                targetState.hero.maxHp += 12;
                targetState.hero.attack += 2;
                targetState.hero.hp = targetState.hero.maxHp;
                levelsGained += 1;
            }

            if (targetState == state && levelsGained > 0)
            {
                RefreshQuestProgress();
                UpdatePeakCombatPower();
            }

            return levelsGained;
        }

        private bool BuyUpgrade(UpgradeType type)
        {
            int cost = IdleRpgBalance.GetUpgradeCost(state, type);
            if (state.hero.gold < cost)
            {
                return false;
            }

            state.hero.gold -= cost;
            state.upgrades.Increment(type);

            switch (type)
            {
                case UpgradeType.Blade:
                    state.hero.attack += IdleRpgBalance.BladeAttackPerLevel;
                    break;
                case UpgradeType.Armor:
                    state.hero.maxHp += IdleRpgBalance.ArmorHpPerLevel;
                    state.hero.hp += IdleRpgBalance.ArmorHpPerLevel;
                    break;
                case UpgradeType.Regeneration:
                    state.hero.regen += IdleRpgBalance.RegenPerLevel;
                    break;
                case UpgradeType.Focus:
                    break;
            }

            SyncHeroCritStatsFromUpgrades(state);

            if (type == UpgradeType.Focus)
            {
                AddFloatingText("치명 " + Mathf.RoundToInt(GetEffectiveCritChance() * 100f) + "%", new Vector2(0.50f, 0.70f), new Color(1f, 0.88f, 0.35f));
                AddLog("집중 수련 Lv." + state.upgrades.focus + "  |  치명타 " + Mathf.RoundToInt(GetEffectiveCritChance() * 100f) + "%  |  치명 피해 " + Mathf.RoundToInt(GetEffectiveCritMultiplier() * 100f) + "%");
            }
            else
            {
                AddFloatingText("강화!", new Vector2(0.50f, 0.66f), new Color(0.97f, 0.84f, 0.43f));
                AddLog(IdleRpgBalance.GetUpgrade(type).Label + " 강화 완료!");
            }
            RefreshQuestProgress();
            UpdatePeakCombatPower();
            SaveState();
            return true;
        }

        private void StartRelicGacha()
        {
            if (state.hero.gold < IdleRpgBalance.GachaGoldCost)
            {
                return;
            }

            state.hero.gold -= IdleRpgBalance.GachaGoldCost;
            bool forceRareOrBetter = state.gacha.pity >= IdleRpgBalance.RarePityPulls - 1;
            RelicDefinition relic = IdleRpgBalance.RollRelic(UnityEngine.Random.value, forceRareOrBetter);
            state.gacha.relics.Increment(relic.Id);
            state.gacha.totalPulls += 1;
            state.gacha.lastRelicId = relic.Id;
            state.gacha.lastRarity = IdleRpgBalance.GetRarityName(relic.Rarity);
            state.gacha.pity = relic.Rarity == RelicRarity.Common ? state.gacha.pity + 1 : 0;

            int newLevel = state.gacha.relics.Get(relic.Id);
            if (relic.MaxHpPerLevel > 0)
            {
                state.hero.hp = Mathf.Min(GetEffectiveMaxHp(), state.hero.hp + relic.MaxHpPerLevel);
            }

            gachaCutscene.BeginRelic(relic, newLevel);
            AddLog("뽑기 성공: [" + state.gacha.lastRarity + "] " + relic.Name + " Lv." + newLevel);
            RefreshQuestProgress();
            UpdatePeakCombatPower();
            SaveState();
        }

        private void StartCompanionGacha(int count)
        {
            int cost = count >= 10 ? IdleRpgBalance.CompanionGachaTenPullGemCost : IdleRpgBalance.CompanionGachaGemCost * count;
            if (state.hero.gems < cost)
            {
                return;
            }

            state.hero.gems -= cost;
            List<GachaCutsceneResult> results = new List<GachaCutsceneResult>(count);
            for (int index = 0; index < count; index += 1)
            {
                CompanionDefinition companion = RollCompanionPull();
                int newLevel = state.companionGacha.companions.Get(companion.Id);
                results.Add(new GachaCutsceneResult(companion.Id, companion.Name, state.companionGacha.lastRarity, IdleRpgBalance.GetRarityColor(companion.Rarity), true, newLevel));
                AddLog("동료 소환: [" + state.companionGacha.lastRarity + "] " + companion.Name + " Lv." + newLevel);
            }

            gachaCutscene.BeginCompanion(results);
            RefreshQuestProgress();
            UpdatePeakCombatPower();
            SaveState();
        }

        private CompanionDefinition RollCompanionPull()
        {
            bool forceRareOrBetter = state.companionGacha.pity >= IdleRpgBalance.CompanionRarePityPulls - 1;
            CompanionDefinition companion = IdleRpgBalance.RollCompanion(UnityEngine.Random.value, forceRareOrBetter);
            state.companionGacha.companions.Increment(companion.Id);
            EquipCompanionAutomatically(state, companion.Id);
            ForceCompanionVisualRefresh();
            state.companionGacha.totalPulls += 1;
            state.companionGacha.lastCompanionId = companion.Id;
            state.companionGacha.lastRarity = IdleRpgBalance.GetRarityName(companion.Rarity);
            state.companionGacha.pity = companion.Rarity == RelicRarity.Common ? state.companionGacha.pity + 1 : 0;
            recentCompanionPullIds.Insert(0, companion.Id);
            while (recentCompanionPullIds.Count > 10)
            {
                recentCompanionPullIds.RemoveAt(recentCompanionPullIds.Count - 1);
            }

            int newLevel = state.companionGacha.companions.Get(companion.Id);
            if (companion.MaxHpPerLevel > 0)
            {
                float immediateHpGain = state.companionGacha.formation.Contains(companion.Id) ? companion.MaxHpPerLevel : companion.MaxHpPerLevel * 0.25f;
                state.hero.hp = Mathf.Min(GetEffectiveMaxHp(), state.hero.hp + immediateHpGain);
            }

            return companion;
        }

        private IdleRpgState CreateInitialState()
        {
            IdleRpgState newState = new IdleRpgState();
            newState.hero.gems = IdleRpgBalance.StartingGems;
            newState.enemy = IdleRpgBalance.CreateEnemy(1);
            newState.stats.highestStage = 1;
            newState.quest = new QuestState();
            InitializeQuest(newState);
            newState.lastSavedUnixSeconds = NowUnixSeconds();
            newState.battleLog.Add("모험을 시작했습니다. 용사가 자동으로 전투합니다.");
            return newState;
        }

        private IdleRpgState LoadState()
        {
            string serialized = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(serialized))
            {
                return CreateInitialState();
            }

            try
            {
                IdleRpgState loaded = JsonUtility.FromJson<IdleRpgState>(serialized);
                if (loaded == null || loaded.version != 1)
                {
                    return CreateInitialState();
                }

                EnsureStateDefaults(loaded);
                ApplyOfflineProgress(loaded, NowUnixSeconds() - loaded.lastSavedUnixSeconds);
                loaded.lastSavedUnixSeconds = NowUnixSeconds();
                return loaded;
            }
            catch (Exception)
            {
                return CreateInitialState();
            }
        }

        private void EnsureStateDefaults(IdleRpgState loaded)
        {
            if (loaded.hero == null)
            {
                loaded.hero = new HeroState();
            }

            if (loaded.hero.gems <= 0 && loaded.stats.kills > 0)
            {
                loaded.hero.gems = IdleRpgBalance.StartingGems;
            }

            if (loaded.upgrades == null)
            {
                loaded.upgrades = new UpgradeLevels();
            }

            if (loaded.combat == null)
            {
                loaded.combat = new CombatTimers();
            }

            if (loaded.stats == null)
            {
                loaded.stats = new GameStats();
            }

            if (loaded.stageCompanionDamage == null)
            {
                loaded.stageCompanionDamage = new CompanionDamageStats();
            }

            if (loaded.gacha == null)
            {
                loaded.gacha = new GachaState();
            }

            if (loaded.gacha.relics == null)
            {
                loaded.gacha.relics = new RelicCollection();
            }

            if (loaded.companionGacha == null)
            {
                loaded.companionGacha = new CompanionGachaState();
            }

            if (loaded.companionGacha.companions == null)
            {
                loaded.companionGacha.companions = new CompanionCollection();
            }

            if (loaded.companionGacha.formation == null)
            {
                loaded.companionGacha.formation = new CompanionFormation();
            }

            EnsureFormationHasOwnedCompanions(loaded);
            SyncHeroCritStatsFromUpgrades(loaded);
            EnsureQuestState(loaded);
            UpdatePeakCombatPower(loaded);

            if (loaded.battleLog == null)
            {
                loaded.battleLog = new List<string>();
            }

            if (loaded.enemy == null || loaded.enemy.maxHp <= 0 || loaded.enemy.hp <= 0f)
            {
                loaded.enemy = IdleRpgBalance.CreateEnemy(Mathf.Max(1, loaded.stage));
            }

            loaded.stage = Mathf.Max(1, loaded.stage);
            loaded.hero.maxHp = Mathf.Max(1, loaded.hero.maxHp);
            loaded.hero.hp = Mathf.Clamp(loaded.hero.hp, 1f, GetEffectiveMaxHp(loaded));
            loaded.stats.highestStage = Mathf.Max(loaded.stats.highestStage, loaded.stage);
        }

        private void ApplyOfflineProgress(IdleRpgState targetState, long elapsedSeconds)
        {
            int offlineSeconds = Mathf.Clamp((int)Math.Max(0, elapsedSeconds), 0, MaxOfflineSeconds);
            if (offlineSeconds < 60)
            {
                return;
            }

            float minutes = offlineSeconds / 60f;
            float stageBonus = 1f + (targetState.stage - 1) * 0.16f;
            int gold = Mathf.FloorToInt(minutes * (7f + stageBonus * 4f));
            int xp = Mathf.FloorToInt(minutes * (3f + stageBonus * 2f));

            targetState.hero.gold += gold;
            targetState.stats.totalGold += gold;
            int levelsGained = GainXp(targetState, xp);
            AddLog(targetState, "오프라인 보상: " + gold + "골드, " + xp + "경험치");
            if (levelsGained > 0)
            {
                AddLog(targetState, "쉬는 동안 레벨 " + targetState.hero.level + "에 도달했습니다.");
            }
        }

        private void EnsureFormationHasOwnedCompanions(IdleRpgState targetState)
        {
            if (targetState.companionGacha == null || targetState.companionGacha.companions == null || targetState.companionGacha.formation == null)
            {
                return;
            }

            if (!targetState.companionGacha.formation.IsEmpty())
            {
                for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
                {
                    int companionId = targetState.companionGacha.formation.Get(slotIndex);
                    if (companionId >= 0 && targetState.companionGacha.companions.Get(companionId) <= 0)
                    {
                        targetState.companionGacha.formation.Set(slotIndex, -1);
                    }
                }
            }

            for (int index = IdleRpgBalance.Companions.Length - 1; index >= 0; index -= 1)
            {
                int companionId = IdleRpgBalance.Companions[index].Id;
                if (targetState.companionGacha.companions.Get(companionId) > 0)
                {
                    EquipCompanionAutomatically(targetState, companionId);
                }
            }
        }

        private void EquipCompanionAutomatically(IdleRpgState targetState, int companionId)
        {
            if (targetState.companionGacha == null || targetState.companionGacha.formation == null || targetState.companionGacha.formation.Contains(companionId))
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                if (targetState.companionGacha.formation.Get(slotIndex) < 0)
                {
                    targetState.companionGacha.formation.Set(slotIndex, companionId);
                    return;
                }
            }
        }

        private bool EquipCompanionToFirstAvailableSlot(int companionId)
        {
            if (state.companionGacha.companions.Get(companionId) <= 0)
            {
                return false;
            }

            if (state.companionGacha.formation.Contains(companionId))
            {
                return true;
            }

            int targetSlot = 0;
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                if (state.companionGacha.formation.Get(slotIndex) < 0)
                {
                    targetSlot = slotIndex;
                    break;
                }
            }

            return EquipCompanion(companionId, targetSlot);
        }

        private bool EquipCompanion(int companionId, int slotIndex)
        {
            if (state.companionGacha.companions.Get(companionId) <= 0)
            {
                return false;
            }

            for (int index = 0; index < FormationSlotCount; index += 1)
            {
                if (state.companionGacha.formation.Get(index) == companionId)
                {
                    state.companionGacha.formation.Set(index, -1);
                }
            }

            state.companionGacha.formation.Set(slotIndex, companionId);
            ForceCompanionVisualRefresh();
            AddLog(IdleRpgBalance.GetCompanion(companionId).Name + " 편성 완료!");
            RefreshQuestProgress();
            UpdatePeakCombatPower();
            SaveState();
            return true;
        }

        private void UnequipCompanion(int slotIndex)
        {
            state.companionGacha.formation.Set(slotIndex, -1);
            ForceCompanionVisualRefresh();
            RefreshQuestProgress();
            UpdatePeakCombatPower();
            SaveState();
        }

        private void ForceCompanionVisualRefresh()
        {
            if (displayedCompanionIds == null)
            {
                return;
            }

            for (int index = 0; index < displayedCompanionIds.Length; index += 1)
            {
                displayedCompanionIds[index] = -999;
            }
        }

        private void SaveState()
        {
            state.lastSavedUnixSeconds = NowUnixSeconds();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        private void UpdateSceneObjects()
        {
            if (heroRoot != null)
            {
                float swing = GetHeroAttackSwing();
                float attackLean = Mathf.Clamp01(state.combat.heroAttack) * 0.10f;
                float lunge = swing * 0.42f;
                heroRoot.position = new Vector3(-2.35f + attackLean + lunge, -1.35f + Mathf.Sin(Time.time * 3.2f) * 0.025f, 0f);
                heroRoot.localRotation = Quaternion.Euler(0f, 0f, -18f * swing);

                if (heroSwordTransform != null)
                {
                    float swordAngle = Mathf.Lerp(heroSwordRestLocalRotation, 92f, swing);
                    Vector3 swordOffset = new Vector3(0.22f * swing, 0.14f * swing, 0f);
                    heroSwordTransform.localPosition = heroSwordRestLocalPosition + swordOffset;
                    heroSwordTransform.localRotation = Quaternion.Euler(0f, 0f, swordAngle);
                    heroSwordTransform.localScale = new Vector3(0.16f * (1f + swing * 0.35f), 1.05f * (1f + swing * 0.2f), 1f);
                }

                if (heroSwingTrailTransform != null)
                {
                    heroSwingTrailTransform.localScale = new Vector3(0.18f + swing * 1.8f, 0.06f + swing * 0.34f, 1f);
                    heroSwingTrailTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-28f, 78f, swing));
                    heroSwingTrailTransform.localPosition = new Vector3(0.62f + swing * 0.72f, 0.72f + swing * 0.24f, -0.04f);
                    SpriteRenderer trailRenderer = heroSwingTrailTransform.GetComponent<SpriteRenderer>();
                    if (trailRenderer != null)
                    {
                        trailRenderer.color = new Color(1f, 0.92f, 0.45f, swing * 0.9f);
                    }
                }
            }

            if (companionRoots != null && companionRenderers != null)
            {
                for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
                {
                    SpriteRenderer renderer = companionRenderers[slotIndex];
                    Transform root = companionRoots[slotIndex];
                    if (renderer == null || root == null)
                    {
                        continue;
                    }

                    int companionId = GetFormationCompanionId(slotIndex);
                    renderer.enabled = companionId >= 0;
                    if (companionId >= 0)
                    {
                        if (displayedCompanionIds[slotIndex] != companionId)
                        {
                            renderer.sprite = companionSprites[companionId];
                            displayedCompanionIds[slotIndex] = companionId;
                        }

                        float x = -4.45f + slotIndex * 0.60f;
                        float y = -1.82f + Mathf.Sin(Time.time * (4.2f + slotIndex * 0.35f)) * 0.025f + slotIndex * 0.03f;
                        float baseScale = 0.56f - slotIndex * 0.03f;
                        float attackLunge = GetCompanionAttackLunge(slotIndex);
                        x += attackLunge * 0.30f;
                        y += attackLunge * 0.07f;
                        root.position = new Vector3(x, y, 0f);
                        root.localScale = Vector3.one * (baseScale + attackLunge * 0.08f);
                        root.localRotation = Quaternion.Euler(0f, 0f, -8f * attackLunge);
                    }
                }
            }

            if (enemyRoot != null)
            {
                float enemyLean = Mathf.Clamp01(state.combat.enemyAttack / 1.45f) * 0.10f;
                float scale = state.enemy.isBoss ? 1.34f : 1f;
                enemyRoot.position = new Vector3(2.55f - enemyLean, -1.1f + Mathf.Sin(Time.time * 2.4f) * 0.045f, 0f);
                enemyRoot.localScale = Vector3.one * scale;
            }

            if (enemyRenderer != null)
            {
                enemyRenderer.color = Color.HSVToRGB(state.enemy.hue, 0.72f, state.enemy.isBoss ? 0.95f : 0.82f);
            }
        }

        private void DrawBattleStatsPanel(Rect rect)
        {
            DrawPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(10f);
            GUILayout.Label("전투 정보", titleStyle);
            GUILayout.Space(4f);
            battleStatsScroll = GUILayout.BeginScrollView(battleStatsScroll, GUILayout.ExpandHeight(true));
            DrawStat("스테이지", state.stage + " (" + state.stageProgress + "/5)");
            DrawStat("레벨", state.hero.level.ToString());
            DrawStat("경험치", FormatNumber(state.hero.xp) + " / " + FormatNumber(state.hero.xpToNext));
            DrawStat("공격력", FormatNumber(GetEffectiveAttack()));
            DrawStat("최대 HP", FormatNumber(GetEffectiveMaxHp()));
            DrawStat("초당 회복", GetEffectiveRegen().ToString("0.0"));
            DrawStat("치명타", Mathf.RoundToInt(GetEffectiveCritChance() * 100f) + "%");
            DrawStat("치명타 피해", Mathf.RoundToInt(GetEffectiveCritMultiplier() * 100f) + "%");
            DrawStat("집중 수련", "Lv." + state.upgrades.focus);
            DrawStat("전투력", FormatNumber(GetCombatPower()));
            DrawStat("처치 수", FormatNumber(state.stats.kills));
            GUILayout.Space(8f);
            GUILayout.Label("전투 방식", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(state.combatMode == CombatMode.Auto ? "[자동]" : "자동", buttonStyle, GUILayout.Height(36f)))
            {
                state.combatMode = CombatMode.Auto;
                state.combat.heroAttack = 0f;
            }

            if (GUILayout.Button(state.combatMode == CombatMode.Manual ? "[직접]" : "직접", buttonStyle, GUILayout.Height(36f)))
            {
                state.combatMode = CombatMode.Manual;
                state.combat.heroAttack = 0f;
            }

            GUILayout.EndHorizontal();
            if (state.combatMode == CombatMode.Auto)
            {
                GUILayout.Label("1초마다 자동 공격합니다. 화면 터치는 공격하지 않습니다.", smallStyle);
            }
            else
            {
                GUILayout.Label("전투 화면 중앙을 터치하면 공격합니다.", smallStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.Space(6f);
            if (GUILayout.Button("스테이지 / 퀘스트", buttonStyle, GUILayout.Height(40f)))
            {
                stageProgressScreenOpen = true;
            }

            GUILayout.EndArea();
        }

        private void DrawLobbyStatsPanel(Rect rect)
        {
            DrawPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(14f);
            GUILayout.Label("로비", titleStyle);
            GUILayout.Space(6f);
            DrawStat("스테이지", state.stage.ToString());
            DrawStat("레벨", state.hero.level.ToString());
            DrawStat("골드", FormatNumber(state.hero.gold) + "G");
            DrawStat("보석", FormatNumber(state.hero.gems) + "♦");
            DrawStat("동료 보유", GetOwnedCompanionCount() + " / " + IdleRpgBalance.Companions.Length);
            DrawStat("최고 스테이지", state.stats.highestStage.ToString());
            DrawStat("전투력", FormatNumber(GetCombatPower()));
            DrawStat("치명타", Mathf.RoundToInt(GetEffectiveCritChance() * 100f) + "%");
            DrawStat("치명타 피해", Mathf.RoundToInt(GetEffectiveCritMultiplier() * 100f) + "%");
            DrawStat("집중 수련", "Lv." + state.upgrades.focus);
            GUILayout.Space(8f);
            GUILayout.Label("골드: 강화 / 유물 뽑기", smallStyle);
            GUILayout.Label("보석: 동료 소환", smallStyle);
            GUILayout.Space(10f);
            GUI.enabled = !companionGachaScreenOpen;
            if (GUILayout.Button("동료 편성", buttonStyle, GUILayout.Height(40f)))
            {
                OpenCompanionFormationScreen();
            }

            GUI.enabled = !companionFormationScreenOpen;
            if (GUILayout.Button("동료 소환", buttonStyle, GUILayout.Height(40f)))
            {
                OpenCompanionGachaScreen();
            }

            GUI.enabled = true;

            GUILayout.Space(8f);
            if (GUILayout.Button("퀘스트 / 스테이지", buttonStyle, GUILayout.Height(40f)))
            {
                stageProgressScreenOpen = true;
            }

            GUILayout.EndArea();
        }

        private void DrawUpgradePanel(Rect rect)
        {
            DrawPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(14f);
            GUILayout.Label("강화", titleStyle);
            GUILayout.Space(4f);
            DrawStat("치명타", Mathf.RoundToInt(GetEffectiveCritChance() * 100f) + "%");
            DrawStat("치명타 피해", Mathf.RoundToInt(GetEffectiveCritMultiplier() * 100f) + "%");
            GUILayout.Space(6f);

            for (int index = 0; index < IdleRpgBalance.Upgrades.Length; index += 1)
            {
                UpgradeDefinition upgrade = IdleRpgBalance.Upgrades[index];
                int cost = IdleRpgBalance.GetUpgradeCost(state, upgrade.Type);
                GUI.enabled = state.hero.gold >= cost;
                string text = upgrade.Label + " Lv." + state.upgrades.Get(upgrade.Type) + "\n" + upgrade.Description + " / " + FormatNumber(cost) + "G";
                if (GUILayout.Button(text, buttonStyle, GUILayout.Height(62f)))
                {
                    BuyUpgrade(upgrade.Type);
                }
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawRelicGachaPanel(Rect rect)
        {
            DrawPanel(rect, new Color(0.06f, 0.07f, 0.14f, 0.90f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(12f);
            GUILayout.Label("유물 뽑기", titleStyle);
            GUILayout.Label("골드로 유물을 뽑아 영구 능력치를 얻습니다.", smallStyle);
            GUILayout.Space(5f);

            GUI.enabled = state.hero.gold >= IdleRpgBalance.GachaGoldCost;
            if (GUILayout.Button("1회 뽑기 - " + FormatNumber(IdleRpgBalance.GachaGoldCost) + "G", buttonStyle, GUILayout.Height(44f)))
            {
                StartRelicGacha();
            }

            GUI.enabled = true;
            GUILayout.Label("희귀 이상 보정: " + state.gacha.pity + " / " + IdleRpgBalance.RarePityPulls, smallStyle);

            if (state.gacha.lastRelicId >= 0)
            {
                RelicDefinition lastRelic = IdleRpgBalance.GetRelic(state.gacha.lastRelicId);
                Color previous = GUI.color;
                GUI.color = IdleRpgBalance.GetRarityColor(lastRelic.Rarity);
                GUILayout.Label("최근: [" + state.gacha.lastRarity + "] " + lastRelic.Name, labelStyle);
                GUI.color = previous;
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("확률표 보기", buttonStyle, GUILayout.Height(38f)))
            {
                relicProbabilityScreenOpen = true;
            }

            GUILayout.Space(6f);
            GUILayout.Label("보유 유물", labelStyle);
            relicGachaScroll = GUILayout.BeginScrollView(relicGachaScroll, GUILayout.ExpandHeight(true));
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                DrawRelicRow(IdleRpgBalance.Relics[index]);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCompanionGachaScreen()
        {
            Rect overlay = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawPanel(overlay, new Color(0.02f, 0.02f, 0.05f, 0.92f));

            float width = Mathf.Min(1100f, Screen.width - 60f);
            float height = Mathf.Min(650f, Screen.height - 60f);
            Rect screen = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(screen, new Color(0.07f, 0.06f, 0.14f, 0.97f));

            GUILayout.BeginArea(new Rect(screen.x + 24f, screen.y + 18f, screen.width - 48f, 62f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("달빛 동료 소환", titleStyle);
            GUILayout.Label("보석으로 동료를 소환합니다. 중복 획득 시 레벨이 올라갑니다.", labelStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("확률표", buttonStyle, GUILayout.Width(92f), GUILayout.Height(42f)))
            {
                companionProbabilityScreenOpen = true;
            }

            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(92f), GUILayout.Height(42f)))
            {
                companionGachaScreenOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect contentRect = new Rect(screen.x + 24f, screen.y + 96f, screen.width - 48f, screen.height - 120f);
            DrawPanel(contentRect, new Color(0.10f, 0.09f, 0.18f, 0.95f));
            GUILayout.BeginArea(new Rect(contentRect.x + 18f, contentRect.y + 14f, contentRect.width - 36f, contentRect.height - 28f));
            GUILayout.Label("보유 보석: " + FormatNumber(state.hero.gems) + "♦   |   희귀 이상 보정: " + state.companionGacha.pity + " / " + IdleRpgBalance.CompanionRarePityPulls, labelStyle);
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            GUI.enabled = state.hero.gems >= IdleRpgBalance.CompanionGachaGemCost;
            if (GUILayout.Button("1회 소환\n" + FormatNumber(IdleRpgBalance.CompanionGachaGemCost) + "♦", buttonStyle, GUILayout.Height(64f), GUILayout.Width((contentRect.width - 56f) * 0.48f)))
            {
                StartCompanionGacha(1);
            }

            GUI.enabled = state.hero.gems >= IdleRpgBalance.CompanionGachaTenPullGemCost;
            if (GUILayout.Button("10회 소환\n" + FormatNumber(IdleRpgBalance.CompanionGachaTenPullGemCost) + "♦", buttonStyle, GUILayout.Height(64f), GUILayout.Width((contentRect.width - 56f) * 0.48f)))
            {
                StartCompanionGacha(10);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);

            CompanionDefinition featuredCompanion = GetFeaturedCompanion();
            GUILayout.BeginHorizontal();
            Rect portraitRect = GUILayoutUtility.GetRect(120f, 160f, GUILayout.Width(120f));
            DrawCompanionPortraitInRect(featuredCompanion, portraitRect);
            GUILayout.BeginVertical();
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(featuredCompanion.Rarity);
            GUILayout.Label("[" + IdleRpgBalance.GetRarityName(featuredCompanion.Rarity) + "] " + featuredCompanion.Name, titleStyle);
            GUI.color = previous;
            GUILayout.Label(featuredCompanion.Title, labelStyle);
            GUILayout.Label(featuredCompanion.Description, smallStyle);
            int featuredLevel = GetCompanionLevel(state, featuredCompanion.Id);
            if (featuredLevel > 0)
            {
                GUILayout.Label(FormatCompanionSkillText(featuredCompanion, featuredLevel), labelStyle);
                GUILayout.Label(FormatCompanionStatBonusText(featuredCompanion, featuredLevel), smallStyle);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            GUILayout.Label("최근 소환 / 동료 도감", labelStyle);
            companionGachaScroll = GUILayout.BeginScrollView(companionGachaScroll, GUILayout.ExpandHeight(true));
            DrawRecentCompanionPulls();
            GUILayout.Space(8f);
            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                DrawCompanionCard(IdleRpgBalance.Companions[index]);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCompanionFormationScreen()
        {
            Rect overlay = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawPanel(overlay, new Color(0.02f, 0.02f, 0.05f, 0.92f));

            float width = Mathf.Min(1100f, Screen.width - 60f);
            float height = Mathf.Min(650f, Screen.height - 60f);
            Rect screen = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(screen, new Color(0.06f, 0.08f, 0.15f, 0.97f));

            GUILayout.BeginArea(new Rect(screen.x + 24f, screen.y + 18f, screen.width - 48f, 62f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("동료 편성", titleStyle);
            GUILayout.Label("전투에 함께 나갈 동료 3명을 배치하세요. 편성된 동료만 능력치 보너스를 줍니다.", labelStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(92f), GUILayout.Height(42f)))
            {
                companionFormationScreenOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect slotsRect = new Rect(screen.x + 24f, screen.y + 96f, screen.width - 48f, 160f);
            DrawPanel(slotsRect, new Color(0.10f, 0.11f, 0.22f, 0.95f));
            GUILayout.BeginArea(new Rect(slotsRect.x + 18f, slotsRect.y + 14f, slotsRect.width - 36f, slotsRect.height - 28f));
            GUILayout.Label("현재 편성", titleStyle);
            GUILayout.BeginHorizontal();
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                DrawFormationSlot(slotIndex);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect listRect = new Rect(screen.x + 24f, slotsRect.yMax + 18f, screen.width - 48f, screen.height - slotsRect.height - 138f);
            DrawPanel(listRect, new Color(0.05f, 0.07f, 0.13f, 0.92f));
            GUILayout.BeginArea(new Rect(listRect.x + 18f, listRect.y + 14f, listRect.width - 36f, listRect.height - 28f));
            GUILayout.Label("보유 동료", titleStyle);
            int ownedCompanionCount = GetOwnedCompanionCount();
            GUILayout.Label("보유 " + ownedCompanionCount + "명 / " + IdleRpgBalance.Companions.Length + "명", labelStyle);
            GUILayout.Label("획득한 동료 카드의 큰 '편성' 버튼을 누르면 빈 슬롯에 바로 배치됩니다.", smallStyle);
            if (ownedCompanionCount <= 0)
            {
                GUILayout.Label("아직 보유 동료가 없습니다. 동료 소환에서 먼저 동료를 획득하세요.", labelStyle);
                if (GUILayout.Button("동료 소환 화면 열기", buttonStyle, GUILayout.Height(42f)))
                {
                    companionFormationScreenOpen = false;
                    OpenCompanionGachaScreen();
                }
            }

            GUILayout.Space(8f);
            companionFormationScroll = GUILayout.BeginScrollView(companionFormationScroll);

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                DrawFormationCompanionCard(IdleRpgBalance.Companions[index]);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawFormationSlot(int slotIndex)
        {
            GUILayout.BeginVertical(GUILayout.Width(320f));
            int companionId = GetFormationCompanionId(slotIndex);
            string slotTitle = "슬롯 " + (slotIndex + 1);
            GUILayout.Label(slotTitle, labelStyle);

            if (companionId >= 0)
            {
                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                GUILayout.BeginHorizontal();
                GUILayout.Label(companionPortraitTextures[companion.Id], GUILayout.Width(56f), GUILayout.Height(74f));
                GUILayout.BeginVertical();
                Color previous = GUI.color;
                GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
                GUILayout.Label("[" + IdleRpgBalance.GetRarityName(companion.Rarity) + "] " + companion.Name, labelStyle);
                GUI.color = previous;
                GUILayout.Label("Lv." + GetCompanionLevel(state, companion.Id) + " " + companion.Title, smallStyle);
                if (GUILayout.Button("해제", buttonStyle, GUILayout.Height(28f)))
                {
                    UnequipCompanion(slotIndex);
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("비어 있음", labelStyle);
                GUILayout.Label("보유 동료 카드에서 이 슬롯에 배치하세요.", smallStyle);
                GUILayout.Space(30f);
            }

            GUILayout.EndVertical();
        }

        private void DrawFormationCompanionCard(CompanionDefinition companion)
        {
            int level = state.companionGacha.companions.Get(companion.Id);
            Rect cardRect = GUILayoutUtility.GetRect(10f, 188f, GUILayout.ExpandWidth(true));
            DrawPanel(cardRect, level > 0 ? new Color(0.12f, 0.14f, 0.25f, 0.95f) : new Color(0.06f, 0.07f, 0.10f, 0.74f));

            Rect portrait = new Rect(cardRect.x + 10f, cardRect.y + 8f, 92f, 138f);
            GUI.DrawTexture(portrait, companionPortraitTextures[companion.Id], ScaleMode.ScaleToFit, true);

            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
            GUI.Label(new Rect(cardRect.x + 104f, cardRect.y + 10f, cardRect.width - 370f, 24f), "Lv." + level + " [" + IdleRpgBalance.GetRarityName(companion.Rarity) + "] " + companion.Name, labelStyle);
            GUI.color = previous;

            GUI.Label(new Rect(cardRect.x + 104f, cardRect.y + 36f, cardRect.width - 370f, 42f), companion.Title + " - " + companion.Description, smallStyle);
            GUI.Label(new Rect(cardRect.x + 104f, cardRect.y + 78f, cardRect.width - 370f, 44f), FormatCompanionSkillText(companion, level), smallStyle);
            GUI.Label(new Rect(cardRect.x + 104f, cardRect.y + 122f, cardRect.width - 370f, 36f), FormatCompanionStatBonusText(companion, level), smallStyle);

            string statusText;
            if (level <= 0)
            {
                statusText = "미보유: 동료 소환에서 획득하면 편성할 수 있습니다.";
            }
            else if (state.companionGacha.formation.Contains(companion.Id))
            {
                statusText = "현재 편성 중";
            }
            else
            {
                statusText = "편성 가능";
            }

            GUI.Label(new Rect(cardRect.x + 104f, cardRect.y + 158f, cardRect.width - 370f, 20f), statusText, smallStyle);

            Rect equipButton = new Rect(cardRect.xMax - 242f, cardRect.y + 14f, 228f, 42f);
            Rect directLabel = new Rect(cardRect.xMax - 242f, cardRect.y + 62f, 228f, 20f);
            Rect slot0 = new Rect(cardRect.xMax - 242f, cardRect.y + 88f, 70f, 34f);
            Rect slot1 = new Rect(cardRect.xMax - 163f, cardRect.y + 88f, 70f, 34f);
            Rect slot2 = new Rect(cardRect.xMax - 84f, cardRect.y + 88f, 70f, 34f);

            GUI.enabled = level > 0;
            if (GUI.Button(equipButton, state.companionGacha.formation.Contains(companion.Id) ? "편성됨" : "편성", buttonStyle))
            {
                EquipCompanionToFirstAvailableSlot(companion.Id);
            }

            GUI.Label(directLabel, level > 0 ? "직접 슬롯 배치" : "아직 미획득", smallStyle);
            DrawFormationSlotButton(companion.Id, 0, slot0);
            DrawFormationSlotButton(companion.Id, 1, slot1);
            DrawFormationSlotButton(companion.Id, 2, slot2);

            GUI.enabled = true;
        }

        private void DrawFormationSlotButton(int companionId, int slotIndex, Rect rect)
        {
            string buttonText = GetFormationCompanionId(slotIndex) == companionId ? "배치됨" : (slotIndex + 1) + "번";
            if (GUI.Button(rect, buttonText, buttonStyle))
            {
                EquipCompanion(companionId, slotIndex);
            }
        }

        private void DrawStageProgressScreen()
        {
            Rect overlay = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawPanel(overlay, new Color(0.02f, 0.02f, 0.05f, 0.90f));

            float width = Mathf.Min(1120f, Screen.width - 60f);
            float height = Mathf.Min(650f, Screen.height - 60f);
            Rect screen = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(screen, new Color(0.05f, 0.08f, 0.15f, 0.97f));

            GUILayout.BeginArea(new Rect(screen.x + 24f, screen.y + 18f, screen.width - 48f, 62f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("스테이지 선택 / 퀘스트", titleStyle);
            GUILayout.Label("해금된 스테이지를 선택해 파밍하거나, 퀘스트를 반복해 보상을 받으세요.", labelStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(92f), GUILayout.Height(42f)))
            {
                stageProgressScreenOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect leftRect = new Rect(screen.x + 24f, screen.y + 96f, screen.width * 0.42f - 18f, screen.height - 120f);
            Rect rightRect = new Rect(leftRect.xMax + 24f, leftRect.y, screen.width - leftRect.width - 72f, leftRect.height);
            DrawPanel(leftRect, new Color(0.10f, 0.09f, 0.18f, 0.95f));
            DrawPanel(rightRect, new Color(0.08f, 0.08f, 0.14f, 0.95f));

            GUILayout.BeginArea(new Rect(leftRect.x + 16f, leftRect.y + 14f, leftRect.width - 32f, leftRect.height - 28f));
            DrawQuestPanelContent();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(rightRect.x + 16f, rightRect.y + 14f, rightRect.width - 32f, rightRect.height - 28f));
            DrawStageSelectContent();
            GUILayout.EndArea();
        }

        private void DrawQuestPanelContent()
        {
            RefreshQuestProgress();
            QuestType questType = IdleRpgBalance.GetQuestType(state.quest.cycleIndex);
            GUILayout.Label("반복 퀘스트", titleStyle);
            GUILayout.Label("순서: 스테이지 밀기 → 유물 뽑기 → 동료 소환 → 전투력 올리기", smallStyle);
            GUILayout.Space(10f);
            GUILayout.Label("현재 퀘스트 #" + state.quest.tier, labelStyle);
            GUILayout.Label(IdleRpgBalance.GetQuestTitle(questType), titleStyle);
            GUILayout.Label(IdleRpgBalance.GetQuestDescription(questType, state.quest.target), labelStyle);
            DrawProgressBar("진행도", state.quest.progress, state.quest.target, new Color(0.98f, 0.74f, 0.25f));
            GUILayout.Space(8f);
            IdleRpgBalance.GetQuestRewards(questType, state.quest.tier, out int rewardGold, out int rewardGems);
            GUILayout.Label("완료 보상: " + FormatNumber(rewardGold) + "G  +  " + FormatNumber(rewardGems) + "♦", labelStyle);
            GUILayout.Space(8f);
            DrawStat("현재 전투력", FormatNumber(GetCombatPower()));
            DrawStat("최고 전투력", FormatNumber(state.stats.peakCombatPower));
            GUILayout.Space(8f);
            GUILayout.Label(GetQuestCyclePreviewText(), smallStyle);
        }

        private void DrawQuestSummaryCompact()
        {
            RefreshQuestProgress();
            QuestType questType = IdleRpgBalance.GetQuestType(state.quest.cycleIndex);
            GUILayout.Label("진행 퀘스트: " + IdleRpgBalance.GetQuestTitle(questType), labelStyle);
            DrawProgressBar(IdleRpgBalance.GetQuestDescription(questType, state.quest.target), state.quest.progress, state.quest.target, new Color(0.98f, 0.74f, 0.25f));
        }

        private string GetQuestCyclePreviewText()
        {
            string[] labels = { "스테이지", "유물", "동료", "전투력" };
            string text = "다음 순환: ";
            for (int index = 0; index < labels.Length; index += 1)
            {
                int cycle = (state.quest.cycleIndex + index) % labels.Length;
                text += (index == 0 ? string.Empty : " → ") + labels[cycle];
            }

            return text;
        }

        private void DrawStageSelectContent()
        {
            GUILayout.Label("스테이지 선택", titleStyle);
            GUILayout.Label("현재 전투: 스테이지 " + state.stage + " (" + state.stageProgress + "/5)  |  최고 해금: " + state.stats.highestStage, labelStyle);
            GUILayout.Space(8f);
            stageSelectScroll = GUILayout.BeginScrollView(stageSelectScroll, GUILayout.ExpandHeight(true));
            for (int stageNumber = 1; stageNumber <= state.stats.highestStage; stageNumber += 1)
            {
                EnemyState preview = IdleRpgBalance.CreateEnemy(stageNumber);
                bool isCurrent = state.stage == stageNumber;
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(220f));
                GUILayout.Label("Stage " + stageNumber + (isCurrent ? "  [전투 중]" : string.Empty), isCurrent ? titleStyle : labelStyle);
                GUILayout.Label(preview.isBoss ? preview.name + " ★" : preview.name, smallStyle);
                GUILayout.Label("HP " + preview.maxHp + " / 골드 " + preview.rewardGold + "G", smallStyle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(isCurrent ? "선택됨" : "입장", buttonStyle, GUILayout.Width(88f), GUILayout.Height(40f)))
                {
                    if (!isCurrent)
                    {
                        SelectStage(stageNumber);
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }

            GUILayout.EndScrollView();
        }

        private CompanionDefinition GetFeaturedCompanion()
        {
            if (state.companionGacha.lastCompanionId >= 0 && state.companionGacha.companions.Get(state.companionGacha.lastCompanionId) > 0)
            {
                return IdleRpgBalance.GetCompanion(state.companionGacha.lastCompanionId);
            }

            int displayedCompanionId = GetDisplayedCompanionId();
            if (displayedCompanionId >= 0)
            {
                return IdleRpgBalance.GetCompanion(displayedCompanionId);
            }

            return IdleRpgBalance.Companions[IdleRpgBalance.Companions.Length - 1];
        }

        private void DrawRecentCompanionPulls()
        {
            if (recentCompanionPullIds.Count <= 0)
            {
                GUILayout.Label("아직 이번 화면에서 소환한 결과가 없습니다.", smallStyle);
                return;
            }

            GUILayout.Label("방금 소환 결과", labelStyle);
            recentCompanionPullScroll = GUILayout.BeginScrollView(recentCompanionPullScroll, GUILayout.Height(104f));
            for (int index = 0; index < recentCompanionPullIds.Count; index += 1)
            {
                CompanionDefinition companion = IdleRpgBalance.GetCompanion(recentCompanionPullIds[index]);
                Color previous = GUI.color;
                GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
                GUILayout.Label((index + 1) + ". [" + IdleRpgBalance.GetRarityName(companion.Rarity) + "] " + companion.Name, smallStyle);
                GUI.color = previous;
            }

            GUILayout.EndScrollView();
        }

        private void DrawCompanionPortrait(CompanionDefinition companion, float size)
        {
            Rect portraitRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            portraitRect.x += 38f;
            DrawCompanionPortraitInRect(companion, portraitRect);
        }

        private void DrawCompanionPortraitInRect(CompanionDefinition companion, Rect portraitRect)
        {
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
            GUI.DrawTexture(new Rect(portraitRect.x - 8f, portraitRect.y - 8f, portraitRect.width + 16f, portraitRect.height + 16f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.DrawTexture(portraitRect, companionPortraitTextures[companion.Id], ScaleMode.ScaleToFit, true);
        }

        private void DrawCompanionCard(CompanionDefinition companion)
        {
            int level = state.companionGacha.companions.Get(companion.Id);
            Rect cardRect = GUILayoutUtility.GetRect(10f, 176f, GUILayout.ExpandWidth(true));
            DrawPanel(cardRect, level > 0 ? new Color(0.12f, 0.14f, 0.25f, 0.95f) : new Color(0.08f, 0.09f, 0.15f, 0.80f));

            Rect portrait = new Rect(cardRect.x + 10f, cardRect.y + 10f, 98f, 148f);
            GUI.DrawTexture(portrait, companionPortraitTextures[companion.Id], ScaleMode.ScaleToFit, true);

            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
            GUI.Label(new Rect(cardRect.x + 122f, cardRect.y + 12f, cardRect.width - 134f, 26f), "Lv." + level + " [" + IdleRpgBalance.GetRarityName(companion.Rarity) + "] " + companion.Name, labelStyle);
            GUI.color = previous;
            GUI.Label(new Rect(cardRect.x + 122f, cardRect.y + 42f, cardRect.width - 134f, 60f), companion.Title + " - " + companion.Description, smallStyle);
            if (level > 0)
            {
                GUI.Label(new Rect(cardRect.x + 122f, cardRect.y + 88f, cardRect.width - 134f, 40f), FormatCompanionSkillText(companion, level), smallStyle);
                GUI.Label(new Rect(cardRect.x + 122f, cardRect.y + 126f, cardRect.width - 134f, 44f), FormatCompanionStatBonusText(companion, level), smallStyle);
            }
            else
            {
                GUI.Label(new Rect(cardRect.x + 122f, cardRect.y + 104f, cardRect.width - 134f, 64f), "소환 후 레벨에 따라 능력치와 스킬 피해가 증가합니다.", smallStyle);
            }
        }

        private void DrawRelicRow(RelicDefinition relic)
        {
            int level = state.gacha.relics.Get(relic.Id);
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(relic.Rarity);
            GUILayout.Label("Lv." + level + " " + relic.Name + " - " + relic.Description, smallStyle);
            GUI.color = previous;
        }

        private void DrawRelicProbabilityScreen()
        {
            DrawGachaProbabilityModal(
                "유물 뽑기 확률표",
                "골드 " + FormatNumber(IdleRpgBalance.GachaGoldCost) + "G로 1회 뽑기  |  희귀 이상 보정은 " + IdleRpgBalance.RarePityPulls + "회 연속 일반 미출 시 다음 뽑기에 적용",
                ref relicProbabilityScreenOpen,
                ref relicProbabilityScroll,
                DrawRelicGachaProbabilityContent);
        }

        private void DrawCompanionProbabilityScreen()
        {
            DrawGachaProbabilityModal(
                "동료 소환 확률표",
                "보석 " + FormatNumber(IdleRpgBalance.CompanionGachaGemCost) + "♦ / 10회 " + FormatNumber(IdleRpgBalance.CompanionGachaTenPullGemCost) + "♦  |  희귀 이상 보정은 " + IdleRpgBalance.CompanionRarePityPulls + "회 연속 일반 미출 시 다음 뽑기에 적용",
                ref companionProbabilityScreenOpen,
                ref companionProbabilityScroll,
                DrawCompanionGachaProbabilityContent);
        }

        private void DrawGachaProbabilityModal(string title, string description, ref bool isOpen, ref Vector2 scrollPosition, Action<bool> drawContent)
        {
            Rect overlay = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawPanel(overlay, new Color(0.02f, 0.02f, 0.05f, 0.92f));

            float width = Mathf.Min(760f, Screen.width - 80f);
            float height = Mathf.Min(620f, Screen.height - 80f);
            Rect screen = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(screen, new Color(0.07f, 0.06f, 0.14f, 0.98f));

            GUILayout.BeginArea(new Rect(screen.x + 24f, screen.y + 18f, screen.width - 48f, 62f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(title, titleStyle);
            GUILayout.Label(description, labelStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(92f), GUILayout.Height(42f)))
            {
                isOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect contentRect = new Rect(screen.x + 24f, screen.y + 96f, screen.width - 48f, screen.height - 120f);
            DrawPanel(contentRect, new Color(0.10f, 0.09f, 0.18f, 0.95f));
            GUILayout.BeginArea(new Rect(contentRect.x + 18f, contentRect.y + 14f, contentRect.width - 36f, contentRect.height - 28f));
            GUILayout.Label("등급별 확률", labelStyle);
            DrawGachaProbabilityHeader(false);
            drawContent(false);
            GUILayout.Space(12f);
            GUILayout.Label("개별 확률", labelStyle);
            DrawGachaProbabilityHeader(true);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            drawContent(true);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRelicGachaProbabilityContent(bool itemsOnly)
        {
            if (!itemsOnly)
            {
                DrawGachaRarityProbabilityRow(RelicRarity.Common, IdleRpgBalance.GetRelicRarityDropProbability);
                DrawGachaRarityProbabilityRow(RelicRarity.Rare, IdleRpgBalance.GetRelicRarityDropProbability);
                DrawGachaRarityProbabilityRow(RelicRarity.Epic, IdleRpgBalance.GetRelicRarityDropProbability);
                DrawGachaRarityProbabilityRow(RelicRarity.Legendary, IdleRpgBalance.GetRelicRarityDropProbability);
                return;
            }

            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                DrawRelicProbabilityRow(IdleRpgBalance.Relics[index]);
            }
        }

        private void DrawCompanionGachaProbabilityContent(bool itemsOnly)
        {
            if (!itemsOnly)
            {
                DrawGachaRarityProbabilityRow(RelicRarity.Common, IdleRpgBalance.GetCompanionRarityDropProbability);
                DrawGachaRarityProbabilityRow(RelicRarity.Rare, IdleRpgBalance.GetCompanionRarityDropProbability);
                DrawGachaRarityProbabilityRow(RelicRarity.Epic, IdleRpgBalance.GetCompanionRarityDropProbability);
                DrawGachaRarityProbabilityRow(RelicRarity.Legendary, IdleRpgBalance.GetCompanionRarityDropProbability);
                return;
            }

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                DrawCompanionProbabilityRow(IdleRpgBalance.Companions[index]);
            }
        }

        private void DrawGachaProbabilityHeader(bool includeNameColumn)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("등급", smallStyle, GUILayout.Width(44f));
            if (includeNameColumn)
            {
                GUILayout.Label("이름", smallStyle, GUILayout.Width(88f));
            }

            GUILayout.Label("일반", smallStyle, GUILayout.Width(58f));
            GUILayout.Label("보정", smallStyle, GUILayout.Width(58f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawGachaRarityProbabilityRow(RelicRarity rarity, Func<RelicRarity, bool, float> probabilityLookup)
        {
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(rarity);
            GUILayout.BeginHorizontal();
            GUILayout.Label(IdleRpgBalance.GetRarityName(rarity), smallStyle, GUILayout.Width(44f));
            GUI.color = previous;
            GUILayout.Label(FormatPercent(probabilityLookup(rarity, false)), smallStyle, GUILayout.Width(58f));
            GUILayout.Label(FormatPercent(probabilityLookup(rarity, true)), smallStyle, GUILayout.Width(58f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawRelicProbabilityRow(RelicDefinition relic)
        {
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(relic.Rarity);
            GUILayout.BeginHorizontal();
            GUILayout.Label("[" + IdleRpgBalance.GetRarityName(relic.Rarity) + "]", smallStyle, GUILayout.Width(44f));
            GUI.color = previous;
            GUILayout.Label(relic.Name, smallStyle, GUILayout.Width(88f));
            GUILayout.Label(FormatPercent(IdleRpgBalance.GetRelicDropProbability(relic, false)), smallStyle, GUILayout.Width(58f));
            GUILayout.Label(FormatPercent(IdleRpgBalance.GetRelicDropProbability(relic, true)), smallStyle, GUILayout.Width(58f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawCompanionProbabilityRow(CompanionDefinition companion)
        {
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
            GUILayout.BeginHorizontal();
            GUILayout.Label("[" + IdleRpgBalance.GetRarityName(companion.Rarity) + "]", smallStyle, GUILayout.Width(44f));
            GUI.color = previous;
            GUILayout.Label(companion.Name, smallStyle, GUILayout.Width(88f));
            GUILayout.Label(FormatPercent(IdleRpgBalance.GetCompanionDropProbability(companion, false)), smallStyle, GUILayout.Width(58f));
            GUILayout.Label(FormatPercent(IdleRpgBalance.GetCompanionDropProbability(companion, true)), smallStyle, GUILayout.Width(58f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawBattlePanel(Rect rect)
        {
            if (rect.width < 220f)
            {
                return;
            }

            DrawPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(8f);
            GUILayout.Label("전투 상황", titleStyle);
            DrawProgressBar("용사 HP", state.hero.hp, GetEffectiveMaxHp(), new Color(0.24f, 0.85f, 0.54f));
            DrawProgressBar("경험치 - 레벨업 시 공격 +2 / 최대 HP +12", state.hero.xp, state.hero.xpToNext, new Color(0.40f, 0.60f, 1f));
            GUILayout.EndArea();

            Rect logRect = new Rect(rect.x, rect.y - 188f, rect.width, 176f);
            DrawPanel(logRect, new Color(0.05f, 0.08f, 0.15f, 0.78f));
            GUILayout.BeginArea(logRect);
            GUILayout.Space(10f);
            GUILayout.Label("전투 기록", titleStyle);
            for (int index = 0; index < state.battleLog.Count; index += 1)
            {
                GUILayout.Label("• " + state.battleLog[index], smallStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawEnemyTopBar(Rect rect)
        {
            DrawPanel(rect, new Color(0.01f, 0.015f, 0.03f, 0.94f));
            Rect inner = new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, rect.height - 16f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), state.enemy.isBoss ? state.enemy.name + " ★" : state.enemy.name, labelStyle);
            float hpPercent = Mathf.Clamp01(state.enemy.hp / Mathf.Max(1f, state.enemy.maxHp));
            Rect bar = new Rect(inner.x, inner.y + 28f, inner.width, 16f);
            GUI.Box(bar, GUIContent.none);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.36f, 0.48f);
            GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, Mathf.Max(0f, bar.width - 4f) * hpPercent, bar.height - 4f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(bar.x, bar.y - 1f, bar.width, 18f), Mathf.FloorToInt(state.enemy.hp) + " / " + state.enemy.maxHp, smallStyle);
        }

        private void DrawCompanionDamageDrawer()
        {
            const float drawerWidth = 252f;
            const float drawerHeight = 196f;
            const float tabWidth = 38f;
            float drawerY = Screen.height - 352f;

            Rect tabRect = new Rect(12f, drawerY + (drawerHeight - 108f) * 0.5f, tabWidth, 108f);
            DrawPanel(tabRect, new Color(0.08f, 0.12f, 0.22f, 0.94f));
            string tabLabel = companionDamageDrawerOpen ? "딜량\n닫기" : "딜량\n열기";
            if (GUI.Button(tabRect, tabLabel, buttonStyle))
            {
                companionDamageDrawerOpen = !companionDamageDrawerOpen;
            }

            if (!companionDamageDrawerOpen)
            {
                return;
            }

            Rect panelRect = new Rect(tabRect.xMax + 4f, drawerY, drawerWidth, drawerHeight);
            DrawPanel(panelRect, new Color(0.05f, 0.08f, 0.15f, 0.94f));

            Rect contentRect = new Rect(panelRect.x + 10f, panelRect.y + 10f, panelRect.width - 20f, panelRect.height - 20f);
            DrawPanel(contentRect, new Color(0.08f, 0.11f, 0.20f, 0.92f));
            GUILayout.BeginArea(contentRect);
            GUILayout.Space(8f);
            GUILayout.Label("이번 스테이지 동료 딜량", titleStyle);
            GUILayout.Label("편성 동료의 누적 피해량", smallStyle);
            DrawCompanionDamageMeter();
            GUILayout.EndArea();
        }

        private void DrawCompanionDamageMeter()
        {
            bool hasCompanion = false;
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(slotIndex);
                if (companionId < 0)
                {
                    continue;
                }

                hasCompanion = true;
                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                int damage = state.stageCompanionDamage != null ? state.stageCompanionDamage.Get(companionId) : 0;
                GUILayout.BeginHorizontal();
                GUILayout.Label("[" + IdleRpgBalance.GetRarityName(companion.Rarity) + "]", smallStyle, GUILayout.Width(44f));
                GUILayout.Label(companion.Name, smallStyle, GUILayout.Width(72f));
                GUILayout.FlexibleSpace();
                int skillDamage = IdleRpgBalance.GetCompanionSkillDamage(companion, GetCompanionLevel(state, companionId), state.hero.level);
                GUILayout.Label("스킬 " + skillDamage, smallStyle, GUILayout.Width(58f));
                GUILayout.Label(FormatNumber(damage), labelStyle);
                GUILayout.EndHorizontal();
            }

            if (!hasCompanion)
            {
                GUILayout.Label("편성된 동료가 없습니다.", smallStyle);
            }
        }

        private void DrawPartyHud(Rect rect)
        {
            DrawPanel(rect, new Color(0.02f, 0.03f, 0.07f, 0.86f));

            Rect heroCard = new Rect(rect.x + 10f, rect.y + 8f, 132f, rect.height - 16f);
            DrawPanel(heroCard, new Color(0.12f, 0.15f, 0.24f, 0.96f));
            GUI.Label(new Rect(heroCard.x + 10f, heroCard.y + 8f, heroCard.width - 20f, 20f), "용사 Lv." + state.hero.level, labelStyle);
            GUI.Label(new Rect(heroCard.x + 10f, heroCard.y + 32f, heroCard.width - 20f, 20f), "HP " + Mathf.FloorToInt(state.hero.hp) + "/" + GetEffectiveMaxHp(), smallStyle);

            float cardWidth = Mathf.Min(190f, (rect.width - 170f) / FormationSlotCount - 10f);
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                Rect card = new Rect(rect.x + 156f + slotIndex * (cardWidth + 10f), rect.y + 8f, cardWidth, rect.height - 16f);
                int companionId = GetFormationCompanionId(slotIndex);
                DrawPanel(card, companionId >= 0 ? new Color(0.10f, 0.12f, 0.22f, 0.96f) : new Color(0.06f, 0.07f, 0.11f, 0.86f));

                if (companionId >= 0)
                {
                    CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                    GUI.DrawTexture(new Rect(card.x + 6f, card.y + 5f, 44f, card.height - 10f), companionPortraitTextures[companion.Id], ScaleMode.ScaleToFit, true);
                    Color previous = GUI.color;
                    GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
                    GUI.Label(new Rect(card.x + 56f, card.y + 8f, card.width - 62f, 20f), companion.Name, labelStyle);
                    GUI.color = previous;
                    GUI.Label(new Rect(card.x + 56f, card.y + 32f, card.width - 62f, 20f), "Lv." + GetCompanionLevel(state, companion.Id) + " " + companion.Title, smallStyle);
                    int skillDamage = IdleRpgBalance.GetCompanionSkillDamage(companion, GetCompanionLevel(state, companion.Id), state.hero.level);
                    GUI.Label(new Rect(card.x + 56f, card.y + 54f, card.width - 62f, 18f), GetCompanionAttackLabel(companion) + " " + skillDamage, smallStyle);
                }
                else
                {
                    GUI.Label(new Rect(card.x + 10f, card.y + 18f, card.width - 20f, 22f), "빈 동료 슬롯", labelStyle);
                    GUI.Label(new Rect(card.x + 10f, card.y + 42f, card.width - 20f, 20f), "동료 편성에서 배치", smallStyle);
                }
            }
        }

        private void DrawStat(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, labelStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawProgressBar(string label, float value, float max, Color color)
        {
            GUILayout.Label(label + "  " + Mathf.FloorToInt(value) + " / " + Mathf.FloorToInt(max), smallStyle);
            Rect rect = GUILayoutUtility.GetRect(10f, 16f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);
            Rect fill = new Rect(rect.x + 2f, rect.y + 2f, Mathf.Max(0f, rect.width - 4f) * Mathf.Clamp01(value / Mathf.Max(1f, max)), rect.height - 4f);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private int GetEffectiveAttack()
        {
            return GetEffectiveAttack(state);
        }

        private int GetEffectiveAttack(IdleRpgState targetState)
        {
            int bonus = 0;
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                RelicDefinition relic = IdleRpgBalance.Relics[index];
                bonus += relic.AttackPerLevel * GetRelicLevel(targetState, relic.Id);
            }

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                CompanionDefinition companion = IdleRpgBalance.Companions[index];
                float multiplier = GetCompanionStatMultiplier(targetState, companion.Id);
                if (multiplier <= 0f)
                {
                    continue;
                }

                bonus += IdleRpgBalance.GetCompanionAttackBonus(companion, GetCompanionLevel(targetState, companion.Id), multiplier);
            }

            return targetState.hero.attack + bonus;
        }

        private int GetEffectiveMaxHp()
        {
            return GetEffectiveMaxHp(state);
        }

        private int GetEffectiveMaxHp(IdleRpgState targetState)
        {
            int bonus = 0;
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                RelicDefinition relic = IdleRpgBalance.Relics[index];
                bonus += relic.MaxHpPerLevel * GetRelicLevel(targetState, relic.Id);
            }

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                CompanionDefinition companion = IdleRpgBalance.Companions[index];
                float multiplier = GetCompanionStatMultiplier(targetState, companion.Id);
                if (multiplier <= 0f)
                {
                    continue;
                }

                bonus += IdleRpgBalance.GetCompanionMaxHpBonus(companion, GetCompanionLevel(targetState, companion.Id), multiplier);
            }

            return targetState.hero.maxHp + bonus;
        }

        private float GetEffectiveRegen()
        {
            return GetEffectiveRegen(state);
        }

        private float GetEffectiveRegen(IdleRpgState targetState)
        {
            float bonus = 0f;
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                RelicDefinition relic = IdleRpgBalance.Relics[index];
                bonus += relic.RegenPerLevel * GetRelicLevel(targetState, relic.Id);
            }

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                CompanionDefinition companion = IdleRpgBalance.Companions[index];
                float multiplier = GetCompanionStatMultiplier(targetState, companion.Id);
                if (multiplier <= 0f)
                {
                    continue;
                }

                bonus += IdleRpgBalance.GetCompanionRegenBonus(companion, GetCompanionLevel(targetState, companion.Id), multiplier);
            }

            return targetState.hero.regen + bonus;
        }

        private void SyncHeroCritStatsFromUpgrades(IdleRpgState targetState)
        {
            if (targetState == null || targetState.hero == null || targetState.upgrades == null)
            {
                return;
            }

            targetState.hero.critChance = IdleRpgBalance.GetHeroCritChanceFromUpgrades(targetState.upgrades.focus);
            targetState.hero.critMultiplier = IdleRpgBalance.GetHeroCritMultiplierFromUpgrades(targetState.upgrades.focus);
        }

        private void EnsureQuestState(IdleRpgState targetState)
        {
            if (targetState.quest == null)
            {
                targetState.quest = new QuestState();
            }

            if (targetState.quest.target <= 0)
            {
                InitializeQuest(targetState);
            }
        }

        private void InitializeQuest(IdleRpgState targetState)
        {
            if (targetState.quest == null)
            {
                targetState.quest = new QuestState();
            }

            QuestType questType = IdleRpgBalance.GetQuestType(targetState.quest.cycleIndex);
            targetState.quest.target = IdleRpgBalance.GetQuestTarget(questType, targetState.quest.tier);
            targetState.quest.progress = 0;
            targetState.quest.stageBaseline = targetState.stats.highestStage;
            targetState.quest.relicPullBaseline = targetState.gacha != null ? targetState.gacha.totalPulls : 0;
            targetState.quest.companionPullBaseline = targetState.companionGacha != null ? targetState.companionGacha.totalPulls : 0;
            targetState.quest.combatPowerBaseline = GetCombatPower(targetState);
        }

        private void RefreshQuestProgress()
        {
            if (state == null || state.quest == null)
            {
                return;
            }

            QuestType questType = IdleRpgBalance.GetQuestType(state.quest.cycleIndex);
            switch (questType)
            {
                case QuestType.PushStages:
                    state.quest.progress = Mathf.Max(0, state.stats.highestStage - state.quest.stageBaseline);
                    break;
                case QuestType.RelicGacha:
                    state.quest.progress = Mathf.Max(0, state.gacha.totalPulls - state.quest.relicPullBaseline);
                    break;
                case QuestType.CompanionGacha:
                    state.quest.progress = Mathf.Max(0, state.companionGacha.totalPulls - state.quest.companionPullBaseline);
                    break;
                case QuestType.RaiseCombatPower:
                    state.quest.progress = Mathf.Max(0, GetCombatPower(state) - state.quest.combatPowerBaseline);
                    break;
            }

            if (state.quest.progress >= state.quest.target)
            {
                CompleteQuest(questType);
            }
        }

        private void CompleteQuest(QuestType questType)
        {
            IdleRpgBalance.GetQuestRewards(questType, state.quest.tier, out int rewardGold, out int rewardGems);
            state.hero.gold += rewardGold;
            state.hero.gems += rewardGems;
            state.stats.totalGold += rewardGold;
            AddLog("퀘스트 완료: " + IdleRpgBalance.GetQuestTitle(questType) + "  +" + FormatNumber(rewardGold) + "G  +" + FormatNumber(rewardGems) + "♦");
            AddFloatingText("퀘스트 완료!", new Vector2(0.50f, 0.72f), new Color(0.98f, 0.74f, 0.25f));

            state.quest.cycleIndex = (state.quest.cycleIndex + 1) % 4;
            state.quest.tier += 1;
            InitializeQuest(state);
            SaveState();
        }

        private void SelectStage(int stageNumber)
        {
            stageNumber = Mathf.Clamp(stageNumber, 1, state.stats.highestStage);
            state.stage = stageNumber;
            state.stageProgress = 0;
            state.enemy = IdleRpgBalance.CreateEnemy(state.stage);
            state.combat.heroAttack = 0f;
            state.combat.enemyAttack = 0f;
            state.hero.hp = Mathf.Min(GetEffectiveMaxHp(), state.hero.hp);
            ResetStageCompanionDamage();
            AddLog("스테이지 " + stageNumber + "에 입장했습니다.");
            SaveState();
        }

        private int GetCombatPower()
        {
            return GetCombatPower(state);
        }

        private int GetCombatPower(IdleRpgState targetState)
        {
            if (targetState == null || targetState.hero == null)
            {
                return 0;
            }

            int relicLevelSum = 0;
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                relicLevelSum += GetRelicLevel(targetState, IdleRpgBalance.Relics[index].Id);
            }

            int totalUpgradeLevels = 0;
            if (targetState.upgrades != null)
            {
                totalUpgradeLevels = targetState.upgrades.blade
                    + targetState.upgrades.armor
                    + targetState.upgrades.regeneration
                    + targetState.upgrades.focus;
            }

            return IdleRpgBalance.CalculateCombatPower(
                GetEffectiveAttack(targetState),
                GetEffectiveMaxHp(targetState),
                GetEffectiveRegen(targetState),
                GetEffectiveCritChance(targetState),
                GetEffectiveCritMultiplier(targetState),
                targetState.hero.level,
                totalUpgradeLevels,
                relicLevelSum,
                GetFormationSkillDamageTotal(targetState));
        }

        private int GetFormationSkillDamageTotal(IdleRpgState targetState)
        {
            int total = 0;
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(targetState, slotIndex);
                if (companionId < 0)
                {
                    continue;
                }

                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                int level = Mathf.Max(1, GetCompanionLevel(targetState, companionId));
                total += IdleRpgBalance.GetCompanionSkillDamage(companion, level, targetState.hero.level);
            }

            return total;
        }

        private void UpdatePeakCombatPower()
        {
            UpdatePeakCombatPower(state);
        }

        private void UpdatePeakCombatPower(IdleRpgState targetState)
        {
            if (targetState == null || targetState.stats == null)
            {
                return;
            }

            targetState.stats.peakCombatPower = Mathf.Max(targetState.stats.peakCombatPower, GetCombatPower(targetState));
        }

        private float GetEffectiveCritChance()
        {
            return GetEffectiveCritChance(state);
        }

        private float GetEffectiveCritChance(IdleRpgState targetState)
        {
            float bonus = 0f;
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                RelicDefinition relic = IdleRpgBalance.Relics[index];
                bonus += relic.CritChancePerLevel * GetRelicLevel(targetState, relic.Id);
            }

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                CompanionDefinition companion = IdleRpgBalance.Companions[index];
                float multiplier = GetCompanionStatMultiplier(targetState, companion.Id);
                if (multiplier <= 0f)
                {
                    continue;
                }

                bonus += IdleRpgBalance.GetCompanionCritBonus(companion, GetCompanionLevel(targetState, companion.Id), multiplier);
            }

            float fromUpgrades = IdleRpgBalance.GetHeroCritChanceFromUpgrades(targetState.upgrades != null ? targetState.upgrades.Get(UpgradeType.Focus) : 0);
            return Mathf.Min(IdleRpgBalance.MaxCritChance, fromUpgrades + bonus);
        }

        private float GetEffectiveCritMultiplier()
        {
            return GetEffectiveCritMultiplier(state);
        }

        private float GetEffectiveCritMultiplier(IdleRpgState targetState)
        {
            int focusLevel = targetState.upgrades != null ? targetState.upgrades.Get(UpgradeType.Focus) : 0;
            return Mathf.Min(
                IdleRpgBalance.MaxCritMultiplier,
                IdleRpgBalance.GetHeroCritMultiplierFromUpgrades(focusLevel));
        }

        private int GetRelicLevel(IdleRpgState targetState, int relicId)
        {
            if (targetState.gacha == null || targetState.gacha.relics == null)
            {
                return 0;
            }

            return targetState.gacha.relics.Get(relicId);
        }

        private int GetCompanionLevel(IdleRpgState targetState, int companionId)
        {
            if (targetState.companionGacha == null || targetState.companionGacha.companions == null)
            {
                return 0;
            }

            return targetState.companionGacha.companions.Get(companionId);
        }

        private float GetCompanionStatMultiplier(IdleRpgState targetState, int companionId)
        {
            if (GetCompanionLevel(targetState, companionId) <= 0)
            {
                return 0f;
            }

            if (targetState.companionGacha != null && targetState.companionGacha.formation != null && targetState.companionGacha.formation.Contains(companionId))
            {
                return 1f;
            }

            return 0.25f;
        }

        private int GetOwnedCompanionCount()
        {
            int ownedCount = 0;
            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                if (GetCompanionLevel(state, IdleRpgBalance.Companions[index].Id) > 0)
                {
                    ownedCount += 1;
                }
            }

            return ownedCount;
        }

        private int GetFormationCompanionId(int slotIndex)
        {
            return GetFormationCompanionId(state, slotIndex);
        }

        private int GetFormationCompanionId(IdleRpgState targetState, int slotIndex)
        {
            if (targetState.companionGacha == null || targetState.companionGacha.formation == null || targetState.companionGacha.companions == null)
            {
                return -1;
            }

            int companionId = targetState.companionGacha.formation.Get(slotIndex);
            if (companionId < 0 || targetState.companionGacha.companions.Get(companionId) <= 0)
            {
                return -1;
            }

            return companionId;
        }

        private int GetDisplayedCompanionId()
        {
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(slotIndex);
                if (companionId >= 0)
                {
                    return companionId;
                }
            }

            return -1;
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
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = new Color(0.86f, 0.90f, 0.98f) }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.72f, 0.78f, 0.88f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        private void AddLog(string message)
        {
            AddLog(state, message);
        }

        private void AddLog(IdleRpgState targetState, string message)
        {
            targetState.battleLog.Insert(0, message);
            while (targetState.battleLog.Count > MaxLogEntries)
            {
                targetState.battleLog.RemoveAt(targetState.battleLog.Count - 1);
            }
        }

        private void AddFloatingText(string text, Vector2 viewportPosition, Color color)
        {
            floatingTexts.Add(new FloatingText(text, viewportPosition, color));
        }

        private void UpdateFloatingTexts(float deltaTime)
        {
            for (int index = floatingTexts.Count - 1; index >= 0; index -= 1)
            {
                floatingTexts[index].Life -= deltaTime;
                floatingTexts[index].ViewportPosition += Vector2.up * deltaTime * 0.08f;
                if (floatingTexts[index].Life <= 0f)
                {
                    floatingTexts.RemoveAt(index);
                }
            }
        }

        private void AddCompanionAttackEffect(CompanionDefinition companion, int slotIndex, int damage)
        {
            string label = GetCompanionAttackLabel(companion);
            Vector2 start = new Vector2(0.22f + slotIndex * 0.04f, 0.62f - slotIndex * 0.02f);
            Vector2 end = new Vector2(0.68f, 0.56f);
            if (companionAttackTimers != null && slotIndex >= 0 && slotIndex < companionAttackTimers.Length)
            {
                companionAttackTimers[slotIndex] = 0.42f;
            }

            companionAttackEffects.Add(new CompanionAttackEffect(label, damage, start, end, IdleRpgBalance.GetRarityColor(companion.Rarity)));
        }

        private string GetCompanionAttackLabel(CompanionDefinition companion)
        {
            switch (companion.Id)
            {
                case 0:
                    return "달빛탄!";
                case 5:
                    return "체리 폭탄!";
                case 6:
                    return "별빛 기도";
                case 8:
                    return "아이돌 응원";
                case 9:
                    return "여우불 검격";
                case 10:
                    return "은하 주문";
                default:
                    return companion.Name + " 지원";
            }
        }

        private string GetCompanionSkillDescription(CompanionDefinition companion)
        {
            switch (companion.Id)
            {
                case 0:
                    return "달빛탄을 발사해 보조 피해를 줍니다.";
                case 1:
                    return "빠른 기습으로 작은 치명 보너스를 제공합니다.";
                case 2:
                    return "민들레 치유로 회복 보너스를 제공합니다.";
                case 3:
                    return "꽃잎 화살로 HP와 치명타를 보조합니다.";
                case 4:
                    return "검무로 공격과 생존력을 고르게 올립니다.";
                case 5:
                    return "체리 폭탄을 던져 폭발 보조 피해를 줍니다.";
                case 6:
                    return "별빛 기도로 회복과 생존력을 크게 보강합니다.";
                case 7:
                    return "용의 바람으로 모든 능력을 균형 있게 강화합니다.";
                case 8:
                    return "응원 무대로 공격과 치명타를 강화합니다.";
                case 9:
                    return "여우불 검격으로 강한 보조 피해를 줍니다.";
                case 10:
                    return "은하 주문으로 회복과 치명타를 크게 올립니다.";
                case 11:
                    return "공주기사의 축복으로 전체 능력을 크게 올립니다.";
                default:
                    return "편성 시 보조 공격과 능력치 보너스를 제공합니다.";
            }
        }

        private string FormatCompanionSkillText(CompanionDefinition companion, int level)
        {
            int skillDamage = IdleRpgBalance.GetCompanionSkillDamage(companion, level, state.hero.level);
            return GetCompanionAttackLabel(companion) + " - " + GetCompanionSkillDescription(companion) + "  [스킬 피해 " + skillDamage + "]";
        }

        private string FormatCompanionStatBonusText(CompanionDefinition companion, int level)
        {
            int ownedAttack = IdleRpgBalance.GetCompanionAttackBonus(companion, level, 0.25f);
            int formedAttack = IdleRpgBalance.GetCompanionAttackBonus(companion, level, 1f);
            int ownedHp = IdleRpgBalance.GetCompanionMaxHpBonus(companion, level, 0.25f);
            int formedHp = IdleRpgBalance.GetCompanionMaxHpBonus(companion, level, 1f);
            float ownedRegen = IdleRpgBalance.GetCompanionRegenBonus(companion, level, 0.25f);
            float formedRegen = IdleRpgBalance.GetCompanionRegenBonus(companion, level, 1f);
            int ownedCrit = Mathf.RoundToInt(IdleRpgBalance.GetCompanionCritBonus(companion, level, 0.25f) * 100f);
            int formedCrit = Mathf.RoundToInt(IdleRpgBalance.GetCompanionCritBonus(companion, level, 1f) * 100f);

            return "보유 Lv." + level + ": 공격+" + ownedAttack + ", HP+" + ownedHp
                + (ownedRegen > 0f ? ", 회복+" + ownedRegen.ToString("0.0") : string.Empty)
                + (ownedCrit > 0 ? ", 치명+" + ownedCrit + "%" : string.Empty)
                + "\n편성 Lv." + level + ": 공격+" + formedAttack + ", HP+" + formedHp
                + (formedRegen > 0f ? ", 회복+" + formedRegen.ToString("0.0") : string.Empty)
                + (formedCrit > 0 ? ", 치명+" + formedCrit + "%" : string.Empty);
        }

        private void UpdateCompanionAttackEffects(float deltaTime)
        {
            heroAttackSwingTimer = Mathf.Max(0f, heroAttackSwingTimer - deltaTime);

            if (companionAttackTimers != null)
            {
                for (int index = 0; index < companionAttackTimers.Length; index += 1)
                {
                    companionAttackTimers[index] = Mathf.Max(0f, companionAttackTimers[index] - deltaTime);
                }
            }

            for (int index = companionAttackEffects.Count - 1; index >= 0; index -= 1)
            {
                companionAttackEffects[index].Life -= deltaTime;
                if (companionAttackEffects[index].Life <= 0f)
                {
                    companionAttackEffects.RemoveAt(index);
                }
            }
        }

        private float GetHeroAttackSwing()
        {
            if (heroAttackSwingTimer <= 0f)
            {
                return 0f;
            }

            float normalized = 1f - heroAttackSwingTimer / HeroAttackSwingDuration;
            return Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
        }

        private float GetCompanionAttackLunge(int slotIndex)
        {
            if (companionAttackTimers == null || slotIndex < 0 || slotIndex >= companionAttackTimers.Length)
            {
                return 0f;
            }

            float normalized = 1f - companionAttackTimers[slotIndex] / 0.42f;
            return Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
        }

        private void DrawFloatingTextsGui()
        {
            EnsureStyles();
            GUIStyle style = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };

            for (int index = 0; index < floatingTexts.Count; index += 1)
            {
                FloatingText item = floatingTexts[index];
                Color previous = GUI.color;
                GUI.color = new Color(item.Color.r, item.Color.g, item.Color.b, Mathf.Clamp01(item.Life));
                Vector2 screen = new Vector2(item.ViewportPosition.x * Screen.width, (1f - item.ViewportPosition.y) * Screen.height);
                GUI.Label(new Rect(screen.x - 80f, screen.y - 18f, 160f, 36f), item.Text, style);
                GUI.color = previous;
            }
        }

        private void DrawCompanionAttackEffectsGui()
        {
            EnsureStyles();
            GUIStyle style = new GUIStyle(smallStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            for (int index = 0; index < companionAttackEffects.Count; index += 1)
            {
                CompanionAttackEffect effect = companionAttackEffects[index];
                float progress = Mathf.Clamp01(1f - effect.Life / effect.MaxLife);
                Vector2 viewport = Vector2.Lerp(effect.StartViewport, effect.EndViewport, progress);
                Vector2 screen = new Vector2(viewport.x * Screen.width, (1f - viewport.y) * Screen.height);
                Color previous = GUI.color;
                GUI.color = new Color(effect.Color.r, effect.Color.g, effect.Color.b, Mathf.Clamp01(effect.Life / effect.MaxLife));
                GUI.DrawTexture(new Rect(screen.x - 8f, screen.y - 8f, 16f, 16f), Texture2D.whiteTexture);
                GUI.Label(new Rect(screen.x - 70f, screen.y - 34f, 140f, 24f), effect.Label + " -" + effect.Damage, style);
                GUI.color = previous;
            }
        }

        private void UpdateBattleSceneVisibility()
        {
            bool battleVisible = currentScreen == GameScreenMode.Battle;
            bool lobbyVisible = currentScreen == GameScreenMode.Lobby;
            if (battleBackdropRoot != null)
            {
                battleBackdropRoot.SetActive(battleVisible);
            }

            if (lobbyBackdropRoot != null)
            {
                lobbyBackdropRoot.SetActive(lobbyVisible);
            }

            if (heroRoot != null)
            {
                heroRoot.gameObject.SetActive(battleVisible);
            }

            if (enemyRoot != null)
            {
                enemyRoot.gameObject.SetActive(battleVisible);
            }

            if (companionRoots != null)
            {
                for (int index = 0; index < companionRoots.Length; index += 1)
                {
                    if (companionRoots[index] != null)
                    {
                        companionRoots[index].gameObject.SetActive(battleVisible);
                    }
                }
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = lobbyVisible
                    ? new Color(0.10f, 0.07f, 0.13f)
                    : new Color(0.08f, 0.10f, 0.22f);
            }
        }

        private void UpdateGachaCutscene(float deltaTime)
        {
            if (!gachaCutscene.Active)
            {
                return;
            }

            gachaCutscene.Timer += deltaTime;
            if (gachaCutscene.Phase == 0 && gachaCutscene.Timer >= 1.15f)
            {
                gachaCutscene.Phase = 1;
                gachaCutscene.Timer = 0f;
            }
        }

        private void DrawGachaCutscene()
        {
            Rect overlay = new Rect(0f, 0f, Screen.width, Screen.height);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            Color overlayColor = gachaCutscene.Phase == 0
                ? new Color(0.02f, 0.03f, 0.10f, 0.92f + pulse * 0.05f)
                : new Color(0.02f, 0.03f, 0.08f, 0.94f);
            DrawPanel(overlay, overlayColor);

            float width = Mathf.Min(920f, Screen.width - 80f);
            float height = Mathf.Min(560f, Screen.height - 80f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(panel, new Color(0.08f, 0.09f, 0.18f, 0.98f));

            if (gachaCutscene.Phase == 0)
            {
                GUI.Label(new Rect(panel.x, panel.y + panel.height * 0.42f, panel.width, 40f), "소환 중...", titleStyle);
                Color previous = GUI.color;
                GUI.color = new Color(0.72f, 0.88f, 1f, 0.35f + pulse * 0.45f);
                GUI.DrawTexture(new Rect(panel.x + panel.width * 0.25f, panel.y + panel.height * 0.28f, panel.width * 0.5f, panel.height * 0.44f), Texture2D.whiteTexture);
                GUI.color = previous;
                return;
            }

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, panel.height - 80f));
            GUILayout.Label(gachaCutscene.IsCompanion ? "동료 소환 결과" : "유물 뽑기 결과", titleStyle);
            GUILayout.Space(10f);

            if (gachaCutscene.Results.Count <= 1)
            {
                GachaCutsceneResult result = gachaCutscene.Results[0];
                Rect portraitRect = GUILayoutUtility.GetRect(180f, 220f, GUILayout.ExpandWidth(false));
                portraitRect.x += (panel.width - 48f - portraitRect.width) * 0.5f - 24f;
                if (result.IsCompanion)
                {
                    DrawCompanionPortraitInRect(IdleRpgBalance.GetCompanion(result.ItemId), portraitRect);
                }
                else
                {
                    Color previous = GUI.color;
                    GUI.color = result.RarityColor;
                    GUI.DrawTexture(new Rect(portraitRect.x - 10f, portraitRect.y - 10f, portraitRect.width + 20f, portraitRect.height + 20f), Texture2D.whiteTexture);
                    GUI.color = previous;
                    GUI.Label(portraitRect, "유물", titleStyle);
                }

                Color rarityPrevious = GUI.color;
                GUI.color = result.RarityColor;
                GUILayout.Label("[" + result.RarityName + "] " + result.ItemName + "  Lv." + result.Level, titleStyle);
                GUI.color = rarityPrevious;
            }
            else
            {
                int columns = 5;
                for (int index = 0; index < gachaCutscene.Results.Count; index += 1)
                {
                    if (index % columns == 0)
                    {
                        GUILayout.BeginHorizontal();
                    }

                    GachaCutsceneResult result = gachaCutscene.Results[index];
                    GUILayout.BeginVertical(GUILayout.Width((panel.width - 80f) / columns));
                    if (result.IsCompanion)
                    {
                        Rect miniRect = GUILayoutUtility.GetRect(72f, 88f);
                        DrawCompanionPortraitInRect(IdleRpgBalance.GetCompanion(result.ItemId), miniRect);
                    }

                    Color rarityPrevious = GUI.color;
                    GUI.color = result.RarityColor;
                    GUILayout.Label(result.RarityName, smallStyle);
                    GUI.color = rarityPrevious;
                    GUILayout.Label(result.ItemName, smallStyle);
                    GUILayout.EndVertical();
                    if (index % columns == columns - 1 || index == gachaCutscene.Results.Count - 1)
                    {
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndArea();

            Rect confirmRect = new Rect(panel.x + panel.width * 0.5f - 90f, panel.yMax - 58f, 180f, 42f);
            if (GUI.Button(confirmRect, "확인", buttonStyle))
            {
                gachaCutscene.Close();
                for (int index = 0; index < gachaCutscene.Results.Count; index += 1)
                {
                    GachaCutsceneResult result = gachaCutscene.Results[index];
                    AddFloatingText(result.ItemName + "!", new Vector2(0.42f + index * 0.02f, 0.68f), result.RarityColor);
                }
            }
        }

        private string FormatPercent(float probability)
        {
            return (probability * 100f).ToString("0.#") + "%";
        }

        private string FormatNumber(int value)
        {
            return value.ToString("N0");
        }

        private long NowUnixSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private sealed class FloatingText
        {
            public readonly string Text;
            public readonly Color Color;
            public Vector2 ViewportPosition;
            public float Life = 1f;

            public FloatingText(string text, Vector2 viewportPosition, Color color)
            {
                Text = text;
                ViewportPosition = viewportPosition;
                Color = color;
            }
        }

        private sealed class CompanionAttackEffect
        {
            public readonly string Label;
            public readonly int Damage;
            public readonly Vector2 StartViewport;
            public readonly Vector2 EndViewport;
            public readonly Color Color;
            public readonly float MaxLife = 0.62f;
            public float Life = 0.62f;

            public CompanionAttackEffect(string label, int damage, Vector2 startViewport, Vector2 endViewport, Color color)
            {
                Label = label;
                Damage = damage;
                StartViewport = startViewport;
                EndViewport = endViewport;
                Color = color;
            }
        }

        private sealed class GachaCutsceneState
        {
            public bool Active;
            public float Timer;
            public int Phase;
            public bool IsCompanion;
            public readonly List<GachaCutsceneResult> Results = new List<GachaCutsceneResult>();

            public void BeginRelic(RelicDefinition relic, int level)
            {
                Results.Clear();
                Results.Add(new GachaCutsceneResult(relic.Id, relic.Name, IdleRpgBalance.GetRarityName(relic.Rarity), IdleRpgBalance.GetRarityColor(relic.Rarity), false, level));
                Active = true;
                Timer = 0f;
                Phase = 0;
                IsCompanion = false;
            }

            public void BeginCompanion(List<GachaCutsceneResult> results)
            {
                Results.Clear();
                Results.AddRange(results);
                Active = true;
                Timer = 0f;
                Phase = 0;
                IsCompanion = true;
            }

            public void Close()
            {
                Active = false;
                Results.Clear();
                Timer = 0f;
                Phase = 0;
            }
        }

        private struct GachaCutsceneResult
        {
            public int ItemId;
            public string ItemName;
            public string RarityName;
            public Color RarityColor;
            public bool IsCompanion;
            public int Level;

            public GachaCutsceneResult(int itemId, string itemName, string rarityName, Color rarityColor, bool isCompanion, int level)
            {
                ItemId = itemId;
                ItemName = itemName;
                RarityName = rarityName;
                RarityColor = rarityColor;
                IsCompanion = isCompanion;
                Level = level;
            }
        }
    }
}
