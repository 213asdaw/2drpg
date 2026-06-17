using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleRPG
{
    public sealed class IdleRpgGame : MonoBehaviour
    {
        private const string SaveKey = "idle-rpg-save-v1";
        private const int MaxLogEntries = 8;
        private const int MaxOfflineSeconds = 2 * 60 * 60;
        private const int FormationSlotCount = 3;

        private IdleRpgState state;
        private Transform heroRoot;
        private Transform[] companionRoots;
        private Transform enemyRoot;
        private SpriteRenderer[] companionRenderers;
        private SpriteRenderer enemyRenderer;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private float saveTimer;
        private bool companionGachaScreenOpen;
        private bool companionFormationScreenOpen;
        private bool stageProgressScreenOpen;
        private int[] displayedCompanionIds;
        private Texture2D[] companionPortraitTextures;
        private Sprite[] companionSprites;
        private Vector2 companionGachaScroll;
        private Vector2 companionFormationScroll;
        private readonly List<int> recentCompanionPullIds = new List<int>();
        private readonly List<FloatingText> floatingTexts = new List<FloatingText>();

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
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            UpdateSceneObjects();
            UpdateFloatingTexts(Time.deltaTime);

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

            float safeWidth = Mathf.Min(Screen.width - 24f, 1180f);
            Rect header = new Rect(12f, 12f, safeWidth, 108f);
            Rect leftPanel = new Rect(12f, 132f, 330f, Screen.height - 150f);
            Rect rightColumn = new Rect(Screen.width - 362f, 132f, 350f, Screen.height - 150f);
            Rect upgradePanel = new Rect(rightColumn.x, rightColumn.y, rightColumn.width, rightColumn.height * 0.50f);
            Rect gachaPanel = new Rect(rightColumn.x, upgradePanel.yMax + 12f, rightColumn.width, rightColumn.height - upgradePanel.height - 12f);
            Rect bottomPanel = new Rect(360f, Screen.height - 150f, Screen.width - 720f, 132f);

            DrawPanel(header, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(header);
            GUILayout.Space(12f);
            GUILayout.Label("2D Idle RPG", smallStyle);
            GUILayout.Label("빛바랜 숲의 방치 용사", titleStyle);
            GUILayout.Label("자동 전투로 골드와 경험치를 모아 강화하고 더 깊은 스테이지로 진입하세요.", labelStyle);
            GUILayout.EndArea();

            DrawStatsPanel(leftPanel);
            DrawUpgradePanel(upgradePanel);
            DrawGachaPanel(gachaPanel);
            DrawBattlePanel(bottomPanel);
            DrawFloatingTextsGui();

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
            camera.backgroundColor = new Color(0.06f, 0.09f, 0.17f);

            CreateBackdrop();
            CreateCompanionArt();
            heroRoot = CreateHero();
            companionRoots = new Transform[FormationSlotCount];
            companionRenderers = new SpriteRenderer[FormationSlotCount];
            displayedCompanionIds = new int[FormationSlotCount];
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                displayedCompanionIds[slotIndex] = -999;
                companionRoots[slotIndex] = CreateCompanionVisual(slotIndex);
            }

            enemyRoot = CreateEnemyVisual();
            UpdateSceneObjects();
        }

        private void CreateBackdrop()
        {
            GameObject sky = CreateSpriteObject("Moonlit Gradient Sky", CreateVerticalGradientSprite(new Color(0.05f, 0.07f, 0.16f), new Color(0.17f, 0.24f, 0.42f), 8, 96));
            sky.transform.position = new Vector3(0f, 0.35f, 5f);
            sky.transform.localScale = new Vector3(10.5f, 10f, 1f);
            SetSortingOrder(sky, -100);

            GameObject moonGlow = CreateSpriteObject("Moon Glow", CreateCircleSprite(new Color(0.86f, 0.94f, 1f, 0.32f), 128));
            moonGlow.transform.position = new Vector3(3.25f, 2.85f, 0f);
            moonGlow.transform.localScale = Vector3.one * 1.7f;
            SetSortingOrder(moonGlow, -92);

            GameObject moon = CreateSpriteObject("Moon", CreateCircleSprite(new Color(0.92f, 0.96f, 1f), 96));
            moon.transform.position = new Vector3(3.25f, 2.85f, 0f);
            moon.transform.localScale = Vector3.one * 0.72f;
            SetSortingOrder(moon, -91);

            AddCloud(-2.8f, 2.65f, 0.72f, -90);
            AddCloud(1.25f, 2.25f, 0.54f, -90);
            AddCloud(4.1f, 1.95f, 0.48f, -90);

            for (int index = 0; index < 34; index += 1)
            {
                GameObject star = CreateSpriteObject("Star", CreateCircleSprite(new Color(0.9f, 0.96f, 1f, 0.8f), 12));
                float x = -4.6f + (index * 0.91f) % 9.2f;
                float y = 1.35f + Mathf.Abs(Mathf.Sin(index * 1.37f)) * 2.35f;
                star.transform.position = new Vector3(x, y, 0f);
                star.transform.localScale = Vector3.one * (0.035f + (index % 4) * 0.008f);
                SetSortingOrder(star, -89);
            }

            GameObject farHills = CreateSpriteObject("Distant Violet Hills", CreateVerticalGradientSprite(new Color(0.08f, 0.13f, 0.25f), new Color(0.15f, 0.22f, 0.37f), 8, 12));
            farHills.transform.position = new Vector3(0f, -1.35f, 0f);
            farHills.transform.localScale = new Vector3(10.2f, 2f, 1f);
            SetSortingOrder(farHills, -80);

            for (int index = 0; index < 16; index += 1)
            {
                float x = -4.7f + index * 0.64f;
                float scale = 0.75f + (index % 5) * 0.12f;
                AddTree(x, -1.15f + Mathf.Sin(index) * 0.08f, scale, -70 + index % 2);
            }

            GameObject ground = CreateSpriteObject("Forest Ground", CreateVerticalGradientSprite(new Color(0.05f, 0.15f, 0.11f), new Color(0.15f, 0.31f, 0.19f), 8, 32));
            ground.transform.position = new Vector3(0f, -2.85f, 0f);
            ground.transform.localScale = new Vector3(10.5f, 2.1f, 1f);
            SetSortingOrder(ground, -48);

            for (int index = 0; index < 28; index += 1)
            {
                GameObject grass = CreateSpriteObject("Moon Grass", CreateSolidSprite(new Color(0.24f, 0.50f, 0.30f), 2, 10));
                float x = -4.8f + index * 0.36f;
                grass.transform.position = new Vector3(x, -2.02f + Mathf.Sin(index * 1.8f) * 0.05f, 0f);
                grass.transform.localScale = new Vector3(0.08f, 0.14f + (index % 4) * 0.03f, 1f);
                grass.transform.localRotation = Quaternion.Euler(0f, 0f, -12f + (index % 5) * 6f);
                SetSortingOrder(grass, -45);
            }

            for (int index = 0; index < 10; index += 1)
            {
                GameObject firefly = CreateSpriteObject("Firefly", CreateCircleSprite(new Color(1f, 0.92f, 0.42f, 0.78f), 18));
                firefly.transform.position = new Vector3(-4.2f + index * 0.92f, -0.85f + Mathf.Sin(index * 0.9f) * 0.42f, 0f);
                firefly.transform.localScale = Vector3.one * 0.055f;
                SetSortingOrder(firefly, -40);
            }
        }

        private Transform CreateHero()
        {
            GameObject root = new GameObject("Hero");
            root.transform.position = new Vector3(-2.35f, -1.35f, 0f);

            AddPart(root.transform, "Body", new Color(0.24f, 0.41f, 1f), new Vector2(0.8f, 1.2f), new Vector3(0f, 0.25f, 0f));
            AddPart(root.transform, "Chest", new Color(0.56f, 0.69f, 1f), new Vector2(0.55f, 0.36f), new Vector3(0f, 0.42f, -0.01f));
            AddPart(root.transform, "Head", new Color(0.96f, 0.76f, 0.54f), new Vector2(0.58f, 0.56f), new Vector3(0f, 1.05f, 0f));
            AddPart(root.transform, "Hair", new Color(0.18f, 0.13f, 0.25f), new Vector2(0.68f, 0.22f), new Vector3(0f, 1.36f, -0.01f));
            AddPart(root.transform, "Sword", new Color(0.86f, 0.93f, 1f), new Vector2(0.16f, 1.05f), new Vector3(0.68f, 0.62f, -0.02f), -38f);
            AddPart(root.transform, "Left Leg", new Color(0.08f, 0.10f, 0.18f), new Vector2(0.22f, 0.56f), new Vector3(-0.18f, -0.58f, 0f));
            AddPart(root.transform, "Right Leg", new Color(0.08f, 0.10f, 0.18f), new Vector2(0.22f, 0.56f), new Vector3(0.22f, -0.58f, 0f));

            return root.transform;
        }

        private void CreateCompanionArt()
        {
            companionPortraitTextures = new Texture2D[IdleRpgBalance.Companions.Length];
            companionSprites = new Sprite[IdleRpgBalance.Companions.Length];

            for (int index = 0; index < IdleRpgBalance.Companions.Length; index += 1)
            {
                Texture2D texture = CreateCompanionTexture(IdleRpgBalance.Companions[index]);
                companionPortraitTextures[index] = texture;
                companionSprites[index] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.08f), 44f);
            }
        }

        private Transform CreateCompanionVisual(int slotIndex)
        {
            GameObject root = new GameObject("Pixel Companion " + (slotIndex + 1));
            root.transform.position = new Vector3(-1.45f, -1.45f, 0f);
            root.transform.localScale = Vector3.one * (0.82f - slotIndex * 0.04f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sortingOrder = 6 + slotIndex;
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

        private void AddPart(Transform parent, string name, Color color, Vector2 scale, Vector3 localPosition, float rotation = 0f)
        {
            GameObject part = CreateSpriteObject(name, CreateSolidSprite(color, 8, 8));
            part.transform.SetParent(parent);
            part.transform.localPosition = localPosition;
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            SetSortingOrder(part, 0);
        }

        private void AddCloud(float x, float y, float scale, int sortingOrder)
        {
            Color cloudColor = new Color(0.70f, 0.81f, 0.98f, 0.34f);
            for (int index = 0; index < 4; index += 1)
            {
                GameObject puff = CreateSpriteObject("Soft Cloud", CreateCircleSprite(cloudColor, 48));
                puff.transform.position = new Vector3(x + (index - 1.5f) * 0.34f * scale, y + Mathf.Sin(index) * 0.08f * scale, 0f);
                puff.transform.localScale = Vector3.one * scale * (0.52f + index * 0.06f);
                SetSortingOrder(puff, sortingOrder);
            }
        }

        private void AddTree(float x, float y, float scale, int sortingOrder)
        {
            GameObject trunk = CreateSpriteObject("Tree Trunk", CreateSolidSprite(new Color(0.09f, 0.07f, 0.07f), 4, 18));
            trunk.transform.position = new Vector3(x, y - 0.18f * scale, 0f);
            trunk.transform.localScale = new Vector3(0.16f * scale, 0.64f * scale, 1f);
            SetSortingOrder(trunk, sortingOrder);

            Color leafColor = new Color(0.06f, 0.18f + scale * 0.04f, 0.16f);
            for (int tier = 0; tier < 3; tier += 1)
            {
                GameObject leaves = CreateSpriteObject("Tree Leaves", CreateCircleSprite(leafColor, 48));
                leaves.transform.position = new Vector3(x, y + (0.1f + tier * 0.28f) * scale, 0f);
                leaves.transform.localScale = new Vector3((0.88f - tier * 0.12f) * scale, (0.58f - tier * 0.05f) * scale, 1f);
                SetSortingOrder(leaves, sortingOrder + 1);
            }
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
            state.combat.heroAttack += safeDelta;
            state.combat.enemyAttack += safeDelta;

            while (state.combat.heroAttack >= 1f)
            {
                state.combat.heroAttack -= 1f;
                ApplyHeroDamage(1f);
            }

            while (state.combat.enemyAttack >= 1.45f)
            {
                state.combat.enemyAttack -= 1.45f;
                ApplyEnemyDamage();
            }
        }

        private void ManualStrike()
        {
            if (state.hero.hp <= 0f)
            {
                return;
            }

            ApplyHeroDamage(0.65f);
            SaveState();
        }

        private void ApplyHeroDamage(float multiplier)
        {
            bool critical = UnityEngine.Random.value < GetEffectiveCritChance();
            float criticalMultiplier = critical ? state.hero.critMultiplier : 1f;
            int damage = Mathf.Max(1, Mathf.FloorToInt(GetEffectiveAttack() * multiplier * criticalMultiplier));

            state.enemy.hp = Mathf.Max(0f, state.enemy.hp - damage);
            AddFloatingText(critical ? "CRIT " + damage : damage.ToString(), new Vector2(0.68f, 0.52f), critical ? Color.yellow : Color.white);

            if (state.enemy.hp <= 0f)
            {
                DefeatEnemy();
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
                state.hero.hp = state.hero.maxHp;
                state.combat.heroAttack = 0f;
                state.combat.enemyAttack = 0f;
                AddLog("용사가 쓰러졌지만 캠프에서 회복했습니다.");
            }
        }

        private void DefeatEnemy()
        {
            EnemyState defeated = state.enemy;
            state.hero.gold += defeated.rewardGold;
            state.stats.totalGold += defeated.rewardGold;
            state.stats.kills += 1;
            state.stageProgress += 1;
            GainXp(defeated.rewardXp);
            AddFloatingText("+" + defeated.rewardGold + "G", new Vector2(0.70f, 0.60f), new Color(1f, 0.82f, 0.4f));

            if (state.stageProgress >= 5)
            {
                state.stage += 1;
                state.stageProgress = 0;
                state.stats.highestStage = Mathf.Max(state.stats.highestStage, state.stage);
                AddLog("스테이지 " + state.stage + "에 도달했습니다.");
            }
            else
            {
                AddLog(defeated.name + " 처치! +" + defeated.rewardGold + "골드");
            }

            state.enemy = IdleRpgBalance.CreateEnemy(state.stage);
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
                    state.hero.attack += 4;
                    break;
                case UpgradeType.Armor:
                    state.hero.maxHp += 22;
                    state.hero.hp += 22f;
                    break;
                case UpgradeType.Regeneration:
                    state.hero.regen += 0.7f;
                    break;
                case UpgradeType.Focus:
                    state.hero.critChance = Mathf.Min(0.45f, state.hero.critChance + 0.025f);
                    break;
            }

            AddFloatingText("강화!", new Vector2(0.50f, 0.66f), new Color(0.97f, 0.84f, 0.43f));
            AddLog(IdleRpgBalance.GetUpgrade(type).Label + " 강화 완료!");
            SaveState();
            return true;
        }

        private bool PullRelic()
        {
            if (state.hero.gold < IdleRpgBalance.GachaGoldCost)
            {
                return false;
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

            Color rarityColor = IdleRpgBalance.GetRarityColor(relic.Rarity);
            AddFloatingText(state.gacha.lastRarity + "!", new Vector2(0.50f, 0.72f), rarityColor);
            AddLog("뽑기 성공: [" + state.gacha.lastRarity + "] " + relic.Name + " Lv." + newLevel);
            SaveState();
            return true;
        }

        private bool PullCompanion()
        {
            if (state.hero.gold < IdleRpgBalance.CompanionGachaGoldCost)
            {
                return false;
            }

            state.hero.gold -= IdleRpgBalance.CompanionGachaGoldCost;
            bool forceRareOrBetter = state.companionGacha.pity >= IdleRpgBalance.CompanionRarePityPulls - 1;
            CompanionDefinition companion = IdleRpgBalance.RollCompanion(UnityEngine.Random.value, forceRareOrBetter);
            state.companionGacha.companions.Increment(companion.Id);
            EquipCompanionAutomatically(state, companion.Id);
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
                state.hero.hp = Mathf.Min(GetEffectiveMaxHp(), state.hero.hp + companion.MaxHpPerLevel);
            }

            Color rarityColor = IdleRpgBalance.GetRarityColor(companion.Rarity);
            AddFloatingText(companion.Name + " 합류!", new Vector2(0.42f, 0.72f), rarityColor);
            AddLog("동료 소환: [" + state.companionGacha.lastRarity + "] " + companion.Name + " Lv." + newLevel);
            SaveState();
            return true;
        }

        private IdleRpgState CreateInitialState()
        {
            IdleRpgState newState = new IdleRpgState();
            newState.enemy = IdleRpgBalance.CreateEnemy(1);
            newState.stats.highestStage = 1;
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
            SaveState();
            return true;
        }

        private void UnequipCompanion(int slotIndex)
        {
            state.companionGacha.formation.Set(slotIndex, -1);
            SaveState();
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
                float attackLean = Mathf.Clamp01(state.combat.heroAttack) * 0.16f;
                heroRoot.position = new Vector3(-2.35f + attackLean, -1.35f + Mathf.Sin(Time.time * 3.2f) * 0.025f, 0f);
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

                        float x = -1.52f + slotIndex * 0.42f;
                        float y = -1.62f + Mathf.Sin(Time.time * (4.2f + slotIndex * 0.35f)) * 0.04f + slotIndex * 0.05f;
                        root.position = new Vector3(x, y, 0f);
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

        private void DrawStatsPanel(Rect rect)
        {
            DrawPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(14f);
            GUILayout.Label("능력치", titleStyle);
            GUILayout.Space(6f);
            DrawStat("스테이지", state.stage.ToString());
            DrawStat("레벨", state.hero.level.ToString());
            DrawStat("골드", FormatNumber(state.hero.gold));
            DrawStat("공격력", FormatNumber(GetEffectiveAttack()));
            DrawStat("최대 HP", FormatNumber(GetEffectiveMaxHp()));
            DrawStat("초당 회복", GetEffectiveRegen().ToString("0.0"));
            DrawStat("치명타", Mathf.RoundToInt(GetEffectiveCritChance() * 100f) + "%");
            DrawStat("처치 수", FormatNumber(state.stats.kills));
            GUILayout.Space(10f);
            if (GUILayout.Button("직접 공격", buttonStyle, GUILayout.Height(44f)))
            {
                ManualStrike();
            }

            if (GUILayout.Button("동료 편성", buttonStyle, GUILayout.Height(40f)))
            {
                companionFormationScreenOpen = true;
                companionGachaScreenOpen = false;
                stageProgressScreenOpen = false;
            }

            if (GUILayout.Button("스테이지 진행도", buttonStyle, GUILayout.Height(40f)))
            {
                stageProgressScreenOpen = true;
                companionGachaScreenOpen = false;
                companionFormationScreenOpen = false;
            }

            GUILayout.EndArea();
        }

        private void DrawUpgradePanel(Rect rect)
        {
            DrawPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(14f);
            GUILayout.Label("강화", titleStyle);
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

        private void DrawGachaPanel(Rect rect)
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
                PullRelic();
            }

            GUI.enabled = true;
            GUILayout.Label("희귀 이상 보정: " + state.gacha.pity + " / " + IdleRpgBalance.RarePityPulls, smallStyle);
            GUILayout.Space(8f);

            if (GUILayout.Button("동료 소환 화면 열기", buttonStyle, GUILayout.Height(42f)))
            {
                companionGachaScreenOpen = true;
                companionFormationScreenOpen = false;
                stageProgressScreenOpen = false;
            }

            if (state.gacha.lastRelicId >= 0)
            {
                RelicDefinition lastRelic = IdleRpgBalance.GetRelic(state.gacha.lastRelicId);
                Color previous = GUI.color;
                GUI.color = IdleRpgBalance.GetRarityColor(lastRelic.Rarity);
                GUILayout.Label("최근: [" + state.gacha.lastRarity + "] " + lastRelic.Name, labelStyle);
                GUI.color = previous;
            }

            GUILayout.Space(4f);
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                DrawRelicRow(IdleRpgBalance.Relics[index]);
            }

            GUILayout.EndArea();
        }

        private void DrawCompanionGachaScreen()
        {
            Rect overlay = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawPanel(overlay, new Color(0.02f, 0.02f, 0.05f, 0.92f));

            float width = Mathf.Min(1040f, Screen.width - 60f);
            float height = Mathf.Min(640f, Screen.height - 60f);
            Rect screen = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(screen, new Color(0.07f, 0.06f, 0.14f, 0.97f));

            GUILayout.BeginArea(new Rect(screen.x + 24f, screen.y + 18f, screen.width - 48f, 62f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("달빛 동료 소환", titleStyle);
            GUILayout.Label("도트 여캐 동료를 소환해 전투 보너스와 함께 모험하세요.", labelStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(92f), GUILayout.Height(42f)))
            {
                companionGachaScreenOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect left = new Rect(screen.x + 24f, screen.y + 96f, 350f, screen.height - 124f);
            Rect right = new Rect(left.xMax + 22f, left.y, screen.width - 420f, left.height);
            DrawPanel(left, new Color(0.11f, 0.10f, 0.21f, 0.92f));
            DrawPanel(right, new Color(0.05f, 0.08f, 0.15f, 0.88f));

            GUILayout.BeginArea(new Rect(left.x + 18f, left.y + 16f, left.width - 36f, left.height - 32f));
            GUILayout.Label("소환 결과", titleStyle);
            CompanionDefinition featuredCompanion = GetFeaturedCompanion();
            DrawCompanionPortrait(featuredCompanion, 220f);
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(featuredCompanion.Rarity);
            GUILayout.Label("[" + IdleRpgBalance.GetRarityName(featuredCompanion.Rarity) + "] " + featuredCompanion.Name, titleStyle);
            GUI.color = previous;
            GUILayout.Label(featuredCompanion.Title, labelStyle);
            GUILayout.Label(featuredCompanion.Description, smallStyle);
            GUILayout.Space(10f);
            GUILayout.Label("보유 골드: " + FormatNumber(state.hero.gold) + "G", labelStyle);
            GUILayout.Label("희귀 이상 보정: " + state.companionGacha.pity + " / " + IdleRpgBalance.CompanionRarePityPulls, smallStyle);
            DrawRecentCompanionPulls();
            GUI.enabled = state.hero.gold >= IdleRpgBalance.CompanionGachaGoldCost;
            if (GUILayout.Button("1회 소환 - " + FormatNumber(IdleRpgBalance.CompanionGachaGoldCost) + "G", buttonStyle, GUILayout.Height(52f)))
            {
                PullCompanion();
            }

            GUI.enabled = state.hero.gold >= IdleRpgBalance.CompanionGachaGoldCost * 10;
            if (GUILayout.Button("10회 소환 - " + FormatNumber(IdleRpgBalance.CompanionGachaGoldCost * 10) + "G", buttonStyle, GUILayout.Height(46f)))
            {
                for (int index = 0; index < 10; index += 1)
                {
                    PullCompanion();
                }
            }

            GUI.enabled = true;
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(right.x + 18f, right.y + 16f, right.width - 36f, right.height - 32f));
            GUILayout.Label("동료 도감", titleStyle);
            GUILayout.Label("중복 소환 시 동료 레벨이 올라가고 영구 능력치 보너스가 증가합니다.", smallStyle);
            GUILayout.Space(10f);
            companionGachaScroll = GUILayout.BeginScrollView(companionGachaScroll);

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
            GUILayout.Label("획득한 동료를 원하는 슬롯에 배치할 수 있습니다.", smallStyle);
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
            Rect cardRect = GUILayoutUtility.GetRect(10f, 92f, GUILayout.ExpandWidth(true));
            DrawPanel(cardRect, level > 0 ? new Color(0.12f, 0.14f, 0.25f, 0.95f) : new Color(0.06f, 0.07f, 0.10f, 0.74f));

            Rect portrait = new Rect(cardRect.x + 10f, cardRect.y + 8f, 58f, 76f);
            GUI.DrawTexture(portrait, companionPortraitTextures[companion.Id], ScaleMode.ScaleToFit, true);

            Rect textRect = new Rect(cardRect.x + 78f, cardRect.y + 8f, cardRect.width - 330f, cardRect.height - 16f);
            GUILayout.BeginArea(textRect);
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
            GUILayout.Label("Lv." + level + " [" + IdleRpgBalance.GetRarityName(companion.Rarity) + "] " + companion.Name, labelStyle);
            GUI.color = previous;
            GUILayout.Label(companion.Title + " - " + companion.Description, smallStyle);
            GUILayout.Label("편성 보너스: 공격 +" + companion.AttackPerLevel + " / HP +" + companion.MaxHpPerLevel + " / 회복 +" + companion.RegenPerLevel.ToString("0.0") + " / 치명 +" + Mathf.RoundToInt(companion.CritChancePerLevel * 100f) + "%", smallStyle);
            GUILayout.EndArea();

            Rect buttonsRect = new Rect(cardRect.xMax - 240f, cardRect.y + 12f, 226f, cardRect.height - 24f);
            GUILayout.BeginArea(buttonsRect);
            GUI.enabled = level > 0;
            GUILayout.BeginHorizontal();
            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                string buttonText = GetFormationCompanionId(slotIndex) == companion.Id ? "배치됨" : (slotIndex + 1) + "번";
                if (GUILayout.Button(buttonText, buttonStyle, GUILayout.Height(34f)))
                {
                    EquipCompanion(companion.Id, slotIndex);
                }
            }

            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label(level > 0 ? "원하는 슬롯 번호를 누르세요." : "아직 미획득", smallStyle);
            GUILayout.EndArea();
        }

        private void DrawStageProgressScreen()
        {
            Rect overlay = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawPanel(overlay, new Color(0.02f, 0.02f, 0.05f, 0.90f));

            float width = Mathf.Min(1040f, Screen.width - 60f);
            float height = Mathf.Min(610f, Screen.height - 60f);
            Rect screen = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            DrawPanel(screen, new Color(0.05f, 0.08f, 0.15f, 0.97f));

            GUILayout.BeginArea(new Rect(screen.x + 24f, screen.y + 18f, screen.width - 48f, 62f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("스테이지 진행도", titleStyle);
            GUILayout.Label("스테이지마다 5번 승리하면 다음 지역으로 넘어갑니다.", labelStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", buttonStyle, GUILayout.Width(92f), GUILayout.Height(42f)))
            {
                stageProgressScreenOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect summaryRect = new Rect(screen.x + 24f, screen.y + 100f, screen.width - 48f, 130f);
            DrawPanel(summaryRect, new Color(0.11f, 0.10f, 0.21f, 0.94f));
            GUILayout.BeginArea(new Rect(summaryRect.x + 18f, summaryRect.y + 16f, summaryRect.width - 36f, summaryRect.height - 32f));
            GUILayout.Label("현재 스테이지 " + state.stage, titleStyle);
            DrawProgressBar("다음 스테이지까지", state.stageProgress, 5, new Color(0.49f, 0.97f, 0.76f));
            GUILayout.Label("최고 도달 스테이지: " + state.stats.highestStage + " / 현재 적: " + state.enemy.name, labelStyle);
            GUILayout.EndArea();

            Rect pathRect = new Rect(screen.x + 24f, summaryRect.yMax + 24f, screen.width - 48f, 170f);
            DrawPanel(pathRect, new Color(0.06f, 0.11f, 0.17f, 0.92f));
            GUILayout.BeginArea(new Rect(pathRect.x + 18f, pathRect.y + 16f, pathRect.width - 36f, pathRect.height - 32f));
            GUILayout.Label("현재 지역 진행", titleStyle);
            Rect nodeRow = GUILayoutUtility.GetRect(10f, 90f, GUILayout.ExpandWidth(true));
            for (int node = 0; node < 5; node += 1)
            {
                float x = nodeRow.x + 45f + node * ((nodeRow.width - 90f) / 4f);
                DrawStageNode(new Rect(x - 32f, nodeRow.y + 18f, 64f, 64f), node + 1, node < state.stageProgress, node == state.stageProgress);
            }

            GUILayout.EndArea();

            Rect previewRect = new Rect(screen.x + 24f, pathRect.yMax + 24f, screen.width - 48f, screen.height - pathRect.yMax + screen.y - 48f);
            DrawPanel(previewRect, new Color(0.08f, 0.08f, 0.14f, 0.92f));
            GUILayout.BeginArea(new Rect(previewRect.x + 18f, previewRect.y + 14f, previewRect.width - 36f, previewRect.height - 28f));
            GUILayout.Label("앞으로 만날 적", titleStyle);
            GUILayout.BeginHorizontal();
            for (int index = 0; index < 6; index += 1)
            {
                int stage = state.stage + index;
                EnemyState preview = IdleRpgBalance.CreateEnemy(stage);
                GUILayout.BeginVertical(GUILayout.Width(150f));
                GUILayout.Label("Stage " + stage, labelStyle);
                GUILayout.Label(preview.isBoss ? preview.name + " ★" : preview.name, smallStyle);
                GUILayout.Label("HP " + preview.maxHp + " / 보상 " + preview.rewardGold + "G", smallStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawStageNode(Rect rect, int nodeNumber, bool cleared, bool current)
        {
            Color previous = GUI.color;
            GUI.color = cleared ? new Color(0.49f, 0.97f, 0.76f) : current ? new Color(1f, 0.82f, 0.35f) : new Color(0.25f, 0.30f, 0.42f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(rect.x, rect.y + 20f, rect.width, 24f), cleared ? "완료" : current ? "진행" : nodeNumber.ToString(), labelStyle);
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
            for (int index = 0; index < recentCompanionPullIds.Count; index += 1)
            {
                CompanionDefinition companion = IdleRpgBalance.GetCompanion(recentCompanionPullIds[index]);
                Color previous = GUI.color;
                GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
                GUILayout.Label((index + 1) + ". [" + IdleRpgBalance.GetRarityName(companion.Rarity) + "] " + companion.Name, smallStyle);
                GUI.color = previous;
            }
        }

        private void DrawCompanionPortrait(CompanionDefinition companion, float size)
        {
            Rect portraitRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            portraitRect.x += 38f;
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
            GUI.DrawTexture(new Rect(portraitRect.x - 8f, portraitRect.y - 8f, portraitRect.width + 16f, portraitRect.height + 16f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.DrawTexture(portraitRect, companionPortraitTextures[companion.Id], ScaleMode.ScaleToFit, true);
        }

        private void DrawCompanionCard(CompanionDefinition companion)
        {
            int level = state.companionGacha.companions.Get(companion.Id);
            Rect cardRect = GUILayoutUtility.GetRect(10f, 86f, GUILayout.ExpandWidth(true));
            DrawPanel(cardRect, level > 0 ? new Color(0.12f, 0.14f, 0.25f, 0.95f) : new Color(0.08f, 0.09f, 0.15f, 0.80f));

            Rect portrait = new Rect(cardRect.x + 10f, cardRect.y + 8f, 52f, 70f);
            GUI.DrawTexture(portrait, companionPortraitTextures[companion.Id], ScaleMode.ScaleToFit, true);

            Rect textRect = new Rect(cardRect.x + 74f, cardRect.y + 8f, cardRect.width - 86f, cardRect.height - 16f);
            GUILayout.BeginArea(textRect);
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(companion.Rarity);
            GUILayout.Label("Lv." + level + " [" + IdleRpgBalance.GetRarityName(companion.Rarity) + "] " + companion.Name, labelStyle);
            GUI.color = previous;
            GUILayout.Label(companion.Title + " - " + companion.Description, smallStyle);
            GUILayout.Label("공격 +" + companion.AttackPerLevel + " / HP +" + companion.MaxHpPerLevel + " / 회복 +" + companion.RegenPerLevel.ToString("0.0") + " / 치명 +" + Mathf.RoundToInt(companion.CritChancePerLevel * 100f) + "%", smallStyle);
            GUILayout.EndArea();
        }

        private void DrawRelicRow(RelicDefinition relic)
        {
            int level = state.gacha.relics.Get(relic.Id);
            Color previous = GUI.color;
            GUI.color = IdleRpgBalance.GetRarityColor(relic.Rarity);
            GUILayout.Label("Lv." + level + " " + relic.Name + " - " + relic.Description, smallStyle);
            GUI.color = previous;
        }

        private void DrawBattlePanel(Rect rect)
        {
            if (rect.width < 220f)
            {
                return;
            }

            DrawPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(rect);
            GUILayout.Space(12f);
            GUILayout.Label("전투 상황", titleStyle);
            DrawProgressBar("용사 HP", state.hero.hp, GetEffectiveMaxHp(), new Color(0.24f, 0.85f, 0.54f));
            DrawProgressBar(state.enemy.isBoss ? state.enemy.name + " ★" : state.enemy.name, state.enemy.hp, state.enemy.maxHp, new Color(1f, 0.36f, 0.48f));
            DrawProgressBar("경험치", state.hero.xp, state.hero.xpToNext, new Color(0.40f, 0.60f, 1f));
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

            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(targetState, slotIndex);
                if (companionId < 0)
                {
                    continue;
                }

                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                bonus += companion.AttackPerLevel * GetCompanionLevel(targetState, companion.Id);
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

            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(targetState, slotIndex);
                if (companionId < 0)
                {
                    continue;
                }

                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                bonus += companion.MaxHpPerLevel * GetCompanionLevel(targetState, companion.Id);
            }

            return targetState.hero.maxHp + bonus;
        }

        private float GetEffectiveRegen()
        {
            float bonus = 0f;
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                RelicDefinition relic = IdleRpgBalance.Relics[index];
                bonus += relic.RegenPerLevel * GetRelicLevel(state, relic.Id);
            }

            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(state, slotIndex);
                if (companionId < 0)
                {
                    continue;
                }

                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                bonus += companion.RegenPerLevel * GetCompanionLevel(state, companion.Id);
            }

            return state.hero.regen + bonus;
        }

        private float GetEffectiveCritChance()
        {
            float bonus = 0f;
            for (int index = 0; index < IdleRpgBalance.Relics.Length; index += 1)
            {
                RelicDefinition relic = IdleRpgBalance.Relics[index];
                bonus += relic.CritChancePerLevel * GetRelicLevel(state, relic.Id);
            }

            for (int slotIndex = 0; slotIndex < FormationSlotCount; slotIndex += 1)
            {
                int companionId = GetFormationCompanionId(state, slotIndex);
                if (companionId < 0)
                {
                    continue;
                }

                CompanionDefinition companion = IdleRpgBalance.GetCompanion(companionId);
                bonus += companion.CritChancePerLevel * GetCompanionLevel(state, companion.Id);
            }

            return Mathf.Min(0.60f, state.hero.critChance + bonus);
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
                normal = { textColor = Color.white }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.86f, 0.90f, 0.98f) }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
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
    }
}
