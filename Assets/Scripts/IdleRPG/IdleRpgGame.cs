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

        private IdleRpgState state;
        private Transform heroRoot;
        private Transform enemyRoot;
        private SpriteRenderer enemyRenderer;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private float saveTimer;
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

        private void OnGUI()
        {
            EnsureStyles();

            float safeWidth = Mathf.Min(Screen.width - 24f, 1180f);
            Rect header = new Rect(12f, 12f, safeWidth, 108f);
            Rect leftPanel = new Rect(12f, 132f, 330f, Screen.height - 150f);
            Rect rightPanel = new Rect(Screen.width - 362f, 132f, 350f, Screen.height - 150f);
            Rect bottomPanel = new Rect(360f, Screen.height - 150f, Screen.width - 720f, 132f);

            DrawPanel(header, new Color(0.05f, 0.08f, 0.15f, 0.88f));
            GUILayout.BeginArea(header);
            GUILayout.Space(12f);
            GUILayout.Label("2D Idle RPG", smallStyle);
            GUILayout.Label("빛바랜 숲의 방치 용사", titleStyle);
            GUILayout.Label("자동 전투로 골드와 경험치를 모아 강화하고 더 깊은 스테이지로 진입하세요.", labelStyle);
            GUILayout.EndArea();

            DrawStatsPanel(leftPanel);
            DrawUpgradePanel(rightPanel);
            DrawBattlePanel(bottomPanel);
            DrawFloatingTextsGui();
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
            heroRoot = CreateHero();
            enemyRoot = CreateEnemyVisual();
            UpdateSceneObjects();
        }

        private void CreateBackdrop()
        {
            GameObject ground = CreateSpriteObject("Forest Ground", CreateSolidSprite(new Color(0.10f, 0.23f, 0.16f), 4, 4));
            ground.transform.position = new Vector3(0f, -2.8f, 2f);
            ground.transform.localScale = new Vector3(9f, 2.1f, 1f);

            GameObject hillBack = CreateSpriteObject("Blue Hills", CreateSolidSprite(new Color(0.11f, 0.19f, 0.32f), 4, 4));
            hillBack.transform.position = new Vector3(0f, -1.4f, 3f);
            hillBack.transform.localScale = new Vector3(8.5f, 1.7f, 1f);

            for (int index = 0; index < 12; index += 1)
            {
                GameObject star = CreateSpriteObject("Star", CreateSolidSprite(new Color(0.9f, 0.96f, 1f), 2, 2));
                float x = -4.2f + index * 0.76f;
                float y = 3.15f + Mathf.Sin(index * 1.7f) * 0.55f;
                star.transform.position = new Vector3(x, y, 1f);
                star.transform.localScale = Vector3.one * (0.04f + (index % 3) * 0.015f);
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

        private void AddPart(Transform parent, string name, Color color, Vector2 scale, Vector3 localPosition, float rotation = 0f)
        {
            GameObject part = CreateSpriteObject(name, CreateSolidSprite(color, 8, 8));
            part.transform.SetParent(parent);
            part.transform.localPosition = localPosition;
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private GameObject CreateSpriteObject(string name, Sprite sprite)
        {
            GameObject spriteObject = new GameObject(name);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return spriteObject;
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
                    float alpha = Mathf.Clamp01(radius - distance);
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
            state.hero.hp = Mathf.Min(state.hero.maxHp, state.hero.hp + state.hero.regen * safeDelta);
            state.combat.heroAttack += safeDelta;
            state.combat.enemyAttack += safeDelta;

            while (state.combat.heroAttack >= 1f)
            {
                state.combat.heroAttack -= 1f;
                ApplyHeroDamage(1f, "자동 공격");
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

            ApplyHeroDamage(0.65f, "직접 공격");
            SaveState();
        }

        private void ApplyHeroDamage(float multiplier, string source)
        {
            bool critical = UnityEngine.Random.value < state.hero.critChance;
            float criticalMultiplier = critical ? state.hero.critMultiplier : 1f;
            int damage = Mathf.Max(1, Mathf.FloorToInt(state.hero.attack * multiplier * criticalMultiplier));

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
            loaded.hero.hp = Mathf.Clamp(loaded.hero.hp, 1f, loaded.hero.maxHp);
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
            DrawStat("공격력", FormatNumber(state.hero.attack));
            DrawStat("초당 회복", state.hero.regen.ToString("0.0"));
            DrawStat("치명타", Mathf.RoundToInt(state.hero.critChance * 100f) + "%");
            DrawStat("처치 수", FormatNumber(state.stats.kills));
            GUILayout.Space(10f);
            if (GUILayout.Button("직접 공격", buttonStyle, GUILayout.Height(44f)))
            {
                ManualStrike();
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
            DrawProgressBar("용사 HP", state.hero.hp, state.hero.maxHp, new Color(0.24f, 0.85f, 0.54f));
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
