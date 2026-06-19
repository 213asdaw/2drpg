using System;
using UnityEngine;

namespace IdleRPG
{
    public enum UpgradeType
    {
        Blade,
        Armor,
        Regeneration,
        Focus
    }

    public enum RelicRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public sealed class UpgradeDefinition
    {
        public readonly UpgradeType Type;
        public readonly string Label;
        public readonly string Description;
        public readonly int BaseCost;
        public readonly float Growth;

        public UpgradeDefinition(UpgradeType type, string label, string description, int baseCost, float growth)
        {
            Type = type;
            Label = label;
            Description = description;
            BaseCost = baseCost;
            Growth = growth;
        }
    }

    public sealed class EnemyTemplate
    {
        public readonly string Name;
        public readonly float Hue;
        public readonly int BaseHp;
        public readonly int BaseAttack;
        public readonly int Gold;
        public readonly int Xp;
        public readonly bool Boss;

        public EnemyTemplate(string name, float hue, int baseHp, int baseAttack, int gold, int xp, bool boss = false)
        {
            Name = name;
            Hue = hue;
            BaseHp = baseHp;
            BaseAttack = baseAttack;
            Gold = gold;
            Xp = xp;
            Boss = boss;
        }
    }

    public sealed class RelicDefinition
    {
        public readonly int Id;
        public readonly string Name;
        public readonly string Description;
        public readonly RelicRarity Rarity;
        public readonly float Weight;
        public readonly int AttackPerLevel;
        public readonly int MaxHpPerLevel;
        public readonly float RegenPerLevel;
        public readonly float CritChancePerLevel;

        public RelicDefinition(
            int id,
            string name,
            string description,
            RelicRarity rarity,
            float weight,
            int attackPerLevel,
            int maxHpPerLevel,
            float regenPerLevel,
            float critChancePerLevel)
        {
            Id = id;
            Name = name;
            Description = description;
            Rarity = rarity;
            Weight = weight;
            AttackPerLevel = attackPerLevel;
            MaxHpPerLevel = maxHpPerLevel;
            RegenPerLevel = regenPerLevel;
            CritChancePerLevel = critChancePerLevel;
        }
    }

    public sealed class CompanionDefinition
    {
        public readonly int Id;
        public readonly string Name;
        public readonly string Title;
        public readonly string Description;
        public readonly RelicRarity Rarity;
        public readonly float Weight;
        public readonly int AttackPerLevel;
        public readonly int MaxHpPerLevel;
        public readonly float RegenPerLevel;
        public readonly float CritChancePerLevel;
        public readonly int SkillDamagePerLevel;
        public readonly Color HairColor;
        public readonly Color OutfitColor;
        public readonly Color AccentColor;

        public CompanionDefinition(
            int id,
            string name,
            string title,
            string description,
            RelicRarity rarity,
            float weight,
            int attackPerLevel,
            int maxHpPerLevel,
            float regenPerLevel,
            float critChancePerLevel,
            int skillDamagePerLevel,
            Color hairColor,
            Color outfitColor,
            Color accentColor)
        {
            Id = id;
            Name = name;
            Title = title;
            Description = description;
            Rarity = rarity;
            Weight = weight;
            AttackPerLevel = attackPerLevel;
            MaxHpPerLevel = maxHpPerLevel;
            RegenPerLevel = regenPerLevel;
            CritChancePerLevel = critChancePerLevel;
            SkillDamagePerLevel = skillDamagePerLevel;
            HairColor = hairColor;
            OutfitColor = outfitColor;
            AccentColor = accentColor;
        }
    }

    public static class IdleRpgBalance
    {
        public const int GachaGoldCost = 120;
        public const int RarePityPulls = 8;
        public const int CompanionGachaGemCost = 160;
        public const int CompanionGachaTenPullGemCost = 1440;
        public const int CompanionRarePityPulls = 10;
        public const int StartingGems = 480;
        public const int StageClearGemReward = 10;
        public const int BossKillGemReward = 6;
        public const int NormalKillGemReward = 1;

        public const int QuestPushStageTarget = 5;
        public const int QuestBaseRelicPullTarget = 3;
        public const int QuestBaseCompanionPullTarget = 2;
        public const int QuestBaseCombatPowerTarget = 280;

        public const float BaseCritChance = 0.05f;
        public const float BaseCritMultiplier = 1.8f;
        public const float FocusCritChancePerLevel = 0.03f;
        public const float FocusCritDamagePerLevel = 0.10f;
        public const float MaxCritChance = 0.65f;
        public const float MaxCritMultiplier = 3.2f;

        public const int BladeAttackPerLevel = 5;
        public const int ArmorHpPerLevel = 28;
        public const float RegenPerLevel = 0.9f;

        public static readonly UpgradeDefinition[] Upgrades =
        {
            new UpgradeDefinition(UpgradeType.Blade, "검술 훈련", "공격력 +" + BladeAttackPerLevel, 20, 1.28f),
            new UpgradeDefinition(UpgradeType.Armor, "강화 갑옷", "최대 HP +" + ArmorHpPerLevel, 28, 1.30f),
            new UpgradeDefinition(UpgradeType.Regeneration, "회복의 룬", "초당 회복 +" + RegenPerLevel.ToString("0.0"), 38, 1.34f),
            new UpgradeDefinition(UpgradeType.Focus, "집중 수련", "치명타 +" + Mathf.RoundToInt(FocusCritChancePerLevel * 100f) + "% / 치명 피해 +" + Mathf.RoundToInt(FocusCritDamagePerLevel * 100f) + "%", 48, 1.36f)
        };

        private static readonly EnemyTemplate[] EnemyRoster =
        {
            new EnemyTemplate("이끼 슬라임", 0.32f, 38, 4, 8, 5),
            new EnemyTemplate("숲 고블린", 0.22f, 48, 5, 10, 6),
            new EnemyTemplate("검은 박쥐", 0.75f, 44, 6, 11, 7),
            new EnemyTemplate("나무 정령", 0.40f, 62, 7, 14, 9),
            new EnemyTemplate("숲의 수호자", 0.04f, 110, 10, 30, 18, true)
        };

        public static readonly RelicDefinition[] Relics =
        {
            new RelicDefinition(0, "불씨 검", "공격력 증가", RelicRarity.Common, 56f, 3, 0, 0f, 0f),
            new RelicDefinition(1, "수호 부적", "최대 HP 증가", RelicRarity.Rare, 28f, 0, 18, 0f, 0f),
            new RelicDefinition(2, "달빛 목걸이", "회복력 증가", RelicRarity.Epic, 12f, 1, 6, 0.35f, 0f),
            new RelicDefinition(3, "숲의 왕관", "치명타와 모든 능력 증가", RelicRarity.Legendary, 4f, 4, 14, 0.2f, 0.015f)
        };

        public static readonly CompanionDefinition[] Companions =
        {
            new CompanionDefinition(0, "루나", "달빛 견습 마법사", "가벼운 별마법으로 공격을 보조합니다.", RelicRarity.Common, 28f, 4, 4, 0f, 0f, 8, new Color(0.25f, 0.18f, 0.38f), new Color(0.42f, 0.52f, 0.98f), new Color(0.92f, 0.88f, 1f)),
            new CompanionDefinition(1, "미오", "고양이 귀 도적", "빠른 단검술로 공격력을 올립니다.", RelicRarity.Common, 26f, 5, 2, 0f, 0.002f, 9, new Color(0.20f, 0.16f, 0.14f), new Color(0.98f, 0.58f, 0.42f), new Color(1f, 0.86f, 0.38f)),
            new CompanionDefinition(2, "나리", "민들레 치유사", "작은 치유 마법으로 회복을 돕습니다.", RelicRarity.Common, 24f, 2, 8, 0.15f, 0f, 4, new Color(0.46f, 0.30f, 0.18f), new Color(0.92f, 0.72f, 0.28f), new Color(0.74f, 1f, 0.58f)),
            new CompanionDefinition(3, "아리아", "꽃잎 궁수", "꽃잎 화살로 체력과 치명타를 올립니다.", RelicRarity.Rare, 18f, 3, 18, 0f, 0.006f, 6, new Color(0.58f, 0.31f, 0.20f), new Color(0.96f, 0.45f, 0.65f), new Color(0.64f, 1f, 0.70f)),
            new CompanionDefinition(4, "린", "푸른 검무희", "검무로 공격과 생존력을 함께 올립니다.", RelicRarity.Rare, 17f, 5, 12, 0.08f, 0.004f, 7, new Color(0.10f, 0.24f, 0.45f), new Color(0.26f, 0.78f, 0.95f), new Color(0.82f, 1f, 1f)),
            new CompanionDefinition(5, "채이", "체리 폭탄 연금술사", "폭발 물약으로 공격 보너스를 줍니다.", RelicRarity.Rare, 16f, 6, 8, 0f, 0.005f, 11, new Color(0.72f, 0.16f, 0.25f), new Color(0.98f, 0.38f, 0.46f), new Color(1f, 0.78f, 0.32f)),
            new CompanionDefinition(6, "세린", "별빛 성녀", "별빛 기도로 회복과 생존력을 보강합니다.", RelicRarity.Epic, 9f, 3, 24, 0.45f, 0.006f, 5, new Color(0.95f, 0.88f, 0.58f), new Color(0.82f, 0.55f, 1f), new Color(1f, 0.96f, 0.60f)),
            new CompanionDefinition(7, "하늘", "구름 용기사", "용의 바람으로 모든 능력을 고르게 올립니다.", RelicRarity.Epic, 8f, 5, 20, 0.20f, 0.008f, 7, new Color(0.55f, 0.78f, 1f), new Color(0.34f, 0.48f, 0.92f), new Color(1f, 1f, 0.74f)),
            new CompanionDefinition(8, "레나", "홍련 아이돌", "응원 무대로 공격과 치명타를 강화합니다.", RelicRarity.Epic, 7f, 7, 10, 0.12f, 0.012f, 10, new Color(0.96f, 0.28f, 0.44f), new Color(1f, 0.48f, 0.72f), new Color(1f, 0.92f, 0.42f)),
            new CompanionDefinition(9, "유리", "여우 검희", "여우불 검술로 공격과 치명타를 폭발적으로 강화합니다.", RelicRarity.Legendary, 3.2f, 12, 22, 0.32f, 0.024f, 26, new Color(0.98f, 0.78f, 0.45f), new Color(0.95f, 0.22f, 0.30f), new Color(1f, 0.82f, 0.30f)),
            new CompanionDefinition(10, "시아", "은하 마녀", "은하 주문으로 회복과 치명타를 크게 올립니다.", RelicRarity.Legendary, 2.8f, 10, 32, 0.55f, 0.022f, 22, new Color(0.74f, 0.62f, 1f), new Color(0.22f, 0.18f, 0.48f), new Color(0.64f, 1f, 1f)),
            new CompanionDefinition(11, "이렌", "백화 공주기사", "공주기사의 축복으로 모든 능력을 크게 올립니다.", RelicRarity.Legendary, 2.4f, 11, 36, 0.40f, 0.020f, 24, new Color(1f, 0.92f, 0.82f), new Color(0.98f, 0.88f, 0.96f), new Color(0.92f, 0.68f, 1f))
        };

        public static EnemyState CreateEnemy(int stage)
        {
            int rosterIndex = Mathf.Abs(stage - 1) % EnemyRoster.Length;
            int cycle = Mathf.Max(0, (stage - 1) / EnemyRoster.Length);
            EnemyTemplate template = EnemyRoster[rosterIndex];
            float stageScale = 1f + (stage - 1) * 0.18f + cycle * 0.28f;
            float bossScale = template.Boss ? 1.35f : 1f;
            int maxHp = Mathf.FloorToInt(template.BaseHp * stageScale * bossScale);

            return new EnemyState
            {
                name = template.Name,
                hue = template.Hue,
                isBoss = template.Boss,
                maxHp = maxHp,
                hp = maxHp,
                attack = Mathf.FloorToInt(template.BaseAttack * stageScale * bossScale),
                rewardGold = Mathf.FloorToInt(template.Gold * stageScale * bossScale),
                rewardXp = Mathf.FloorToInt(template.Xp * stageScale * bossScale)
            };
        }

        public static int GetUpgradeCost(IdleRpgState state, UpgradeType type)
        {
            UpgradeDefinition definition = GetUpgrade(type);
            return Mathf.FloorToInt(definition.BaseCost * Mathf.Pow(definition.Growth, state.upgrades.Get(type)));
        }

        public static UpgradeDefinition GetUpgrade(UpgradeType type)
        {
            for (int index = 0; index < Upgrades.Length; index += 1)
            {
                if (Upgrades[index].Type == type)
                {
                    return Upgrades[index];
                }
            }

            throw new ArgumentOutOfRangeException("type", type, "Unknown upgrade type.");
        }

        public static float GetRelicPoolTotalWeight(bool forceRareOrBetter)
        {
            float totalWeight = 0f;
            for (int index = 0; index < Relics.Length; index += 1)
            {
                RelicDefinition relic = Relics[index];
                if (!forceRareOrBetter || relic.Rarity != RelicRarity.Common)
                {
                    totalWeight += relic.Weight;
                }
            }

            return totalWeight;
        }

        public static float GetRelicDropProbability(RelicDefinition relic, bool forceRareOrBetter)
        {
            if (forceRareOrBetter && relic.Rarity == RelicRarity.Common)
            {
                return 0f;
            }

            float totalWeight = GetRelicPoolTotalWeight(forceRareOrBetter);
            return totalWeight <= 0f ? 0f : relic.Weight / totalWeight;
        }

        public static float GetRelicRarityDropProbability(RelicRarity rarity, bool forceRareOrBetter)
        {
            float totalWeight = GetRelicPoolTotalWeight(forceRareOrBetter);
            if (totalWeight <= 0f)
            {
                return 0f;
            }

            float rarityWeight = 0f;
            for (int index = 0; index < Relics.Length; index += 1)
            {
                RelicDefinition relic = Relics[index];
                if (relic.Rarity == rarity && (!forceRareOrBetter || relic.Rarity != RelicRarity.Common))
                {
                    rarityWeight += relic.Weight;
                }
            }

            return rarityWeight / totalWeight;
        }

        public static float GetCompanionPoolTotalWeight(bool forceRareOrBetter)
        {
            float totalWeight = 0f;
            for (int index = 0; index < Companions.Length; index += 1)
            {
                CompanionDefinition companion = Companions[index];
                if (!forceRareOrBetter || companion.Rarity != RelicRarity.Common)
                {
                    totalWeight += companion.Weight;
                }
            }

            return totalWeight;
        }

        public static float GetCompanionDropProbability(CompanionDefinition companion, bool forceRareOrBetter)
        {
            if (forceRareOrBetter && companion.Rarity == RelicRarity.Common)
            {
                return 0f;
            }

            float totalWeight = GetCompanionPoolTotalWeight(forceRareOrBetter);
            return totalWeight <= 0f ? 0f : companion.Weight / totalWeight;
        }

        public static float GetCompanionRarityDropProbability(RelicRarity rarity, bool forceRareOrBetter)
        {
            float totalWeight = GetCompanionPoolTotalWeight(forceRareOrBetter);
            if (totalWeight <= 0f)
            {
                return 0f;
            }

            float rarityWeight = 0f;
            for (int index = 0; index < Companions.Length; index += 1)
            {
                CompanionDefinition companion = Companions[index];
                if (companion.Rarity == rarity && (!forceRareOrBetter || companion.Rarity != RelicRarity.Common))
                {
                    rarityWeight += companion.Weight;
                }
            }

            return rarityWeight / totalWeight;
        }

        public static RelicDefinition RollRelic(float roll, bool forceRareOrBetter)
        {
            float totalWeight = 0f;
            for (int index = 0; index < Relics.Length; index += 1)
            {
                if (!forceRareOrBetter || Relics[index].Rarity != RelicRarity.Common)
                {
                    totalWeight += Relics[index].Weight;
                }
            }

            float weightedRoll = Mathf.Clamp01(roll) * totalWeight;
            float cursor = 0f;

            for (int index = 0; index < Relics.Length; index += 1)
            {
                RelicDefinition relic = Relics[index];
                if (forceRareOrBetter && relic.Rarity == RelicRarity.Common)
                {
                    continue;
                }

                cursor += relic.Weight;
                if (weightedRoll <= cursor)
                {
                    return relic;
                }
            }

            return Relics[Relics.Length - 1];
        }

        public static RelicDefinition GetRelic(int relicId)
        {
            for (int index = 0; index < Relics.Length; index += 1)
            {
                if (Relics[index].Id == relicId)
                {
                    return Relics[index];
                }
            }

            return Relics[0];
        }

        public static CompanionDefinition RollCompanion(float roll, bool forceRareOrBetter)
        {
            float totalWeight = 0f;
            for (int index = 0; index < Companions.Length; index += 1)
            {
                if (!forceRareOrBetter || Companions[index].Rarity != RelicRarity.Common)
                {
                    totalWeight += Companions[index].Weight;
                }
            }

            float weightedRoll = Mathf.Clamp01(roll) * totalWeight;
            float cursor = 0f;

            for (int index = 0; index < Companions.Length; index += 1)
            {
                CompanionDefinition companion = Companions[index];
                if (forceRareOrBetter && companion.Rarity == RelicRarity.Common)
                {
                    continue;
                }

                cursor += companion.Weight;
                if (weightedRoll <= cursor)
                {
                    return companion;
                }
            }

            return Companions[Companions.Length - 1];
        }

        public static CompanionDefinition GetCompanion(int companionId)
        {
            for (int index = 0; index < Companions.Length; index += 1)
            {
                if (Companions[index].Id == companionId)
                {
                    return Companions[index];
                }
            }

            return Companions[0];
        }

        public static string GetRarityName(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common:
                    return "일반";
                case RelicRarity.Rare:
                    return "희귀";
                case RelicRarity.Epic:
                    return "영웅";
                case RelicRarity.Legendary:
                    return "전설";
                default:
                    return "알 수 없음";
            }
        }

        public static Color GetRarityColor(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common:
                    return new Color(0.86f, 0.90f, 0.98f);
                case RelicRarity.Rare:
                    return new Color(0.45f, 0.75f, 1f);
                case RelicRarity.Epic:
                    return new Color(0.78f, 0.48f, 1f);
                case RelicRarity.Legendary:
                    return new Color(1f, 0.74f, 0.25f);
                default:
                    return Color.white;
            }
        }

        public static int GetCompanionAttackBonus(CompanionDefinition companion, int level, float multiplier)
        {
            return Mathf.FloorToInt(companion.AttackPerLevel * Mathf.Max(1, level) * multiplier);
        }

        public static int GetCompanionMaxHpBonus(CompanionDefinition companion, int level, float multiplier)
        {
            return Mathf.FloorToInt(companion.MaxHpPerLevel * Mathf.Max(1, level) * multiplier);
        }

        public static float GetCompanionRegenBonus(CompanionDefinition companion, int level, float multiplier)
        {
            return companion.RegenPerLevel * Mathf.Max(1, level) * multiplier;
        }

        public static float GetCompanionCritBonus(CompanionDefinition companion, int level, float multiplier)
        {
            return companion.CritChancePerLevel * Mathf.Max(1, level) * multiplier;
        }

        public static int GetCompanionSkillDamage(CompanionDefinition companion, int companionLevel, int heroLevel)
        {
            int level = Mathf.Max(1, companionLevel);
            float rarityScale = GetCompanionSkillRarityScale(companion.Rarity);
            return Mathf.Max(1, Mathf.FloorToInt((companion.SkillDamagePerLevel * level + heroLevel * 0.55f) * rarityScale));
        }

        public static float GetHeroCritChanceFromUpgrades(int focusLevel)
        {
            return BaseCritChance + focusLevel * FocusCritChancePerLevel;
        }

        public static float GetHeroCritMultiplierFromUpgrades(int focusLevel)
        {
            return BaseCritMultiplier + focusLevel * FocusCritDamagePerLevel;
        }

        private static float GetCompanionSkillRarityScale(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Legendary:
                    return 1.28f;
                case RelicRarity.Epic:
                    return 1.12f;
                case RelicRarity.Rare:
                    return 1.05f;
                default:
                    return 1f;
            }
        }

        public static QuestType GetQuestType(int cycleIndex)
        {
            int normalized = ((cycleIndex % 4) + 4) % 4;
            return (QuestType)normalized;
        }

        public static string GetQuestTitle(QuestType type)
        {
            switch (type)
            {
                case QuestType.PushStages:
                    return "스테이지 밀기";
                case QuestType.RelicGacha:
                    return "유물 뽑기";
                case QuestType.CompanionGacha:
                    return "동료 소환";
                case QuestType.RaiseCombatPower:
                    return "전투력 올리기";
                default:
                    return "퀘스트";
            }
        }

        public static string GetQuestDescription(QuestType type, int target)
        {
            switch (type)
            {
                case QuestType.PushStages:
                    return "최고 스테이지를 " + target + "단계 올리세요.";
                case QuestType.RelicGacha:
                    return "유물 뽑기를 " + target + "회 진행하세요.";
                case QuestType.CompanionGacha:
                    return "동료 소환을 " + target + "회 진행하세요.";
                case QuestType.RaiseCombatPower:
                    return "전투력을 " + FormatNumber(target) + " 이상 올리세요.";
                default:
                    return string.Empty;
            }
        }

        public static int GetQuestTarget(QuestType type, int tier)
        {
            int safeTier = Mathf.Max(1, tier);
            switch (type)
            {
                case QuestType.PushStages:
                    return QuestPushStageTarget;
                case QuestType.RelicGacha:
                    return QuestBaseRelicPullTarget + (safeTier - 1);
                case QuestType.CompanionGacha:
                    return QuestBaseCompanionPullTarget + (safeTier - 1) / 2;
                case QuestType.RaiseCombatPower:
                    return QuestBaseCombatPowerTarget + (safeTier - 1) * 90;
                default:
                    return 1;
            }
        }

        public static void GetQuestRewards(QuestType type, int tier, out int gold, out int gems)
        {
            int safeTier = Mathf.Max(1, tier);
            gold = 650 + safeTier * 140;
            gems = 180 + safeTier * 45;

            switch (type)
            {
                case QuestType.PushStages:
                    gold += 250;
                    gems += 70;
                    break;
                case QuestType.RelicGacha:
                    gold += 320;
                    gems += 40;
                    break;
                case QuestType.CompanionGacha:
                    gold += 180;
                    gems += 120;
                    break;
                case QuestType.RaiseCombatPower:
                    gold += 280;
                    gems += 90;
                    break;
            }
        }

        public static int CalculateCombatPower(
            int attack,
            int maxHp,
            float regen,
            float critChance,
            float critMultiplier,
            int heroLevel,
            int totalUpgradeLevels,
            int relicLevelSum,
            int formationSkillDamage)
        {
            int critValue = Mathf.RoundToInt(critChance * 140f + (critMultiplier - 1f) * 95f);
            return attack * 4
                + maxHp / 4
                + Mathf.RoundToInt(regen * 28f)
                + critValue
                + heroLevel * 10
                + totalUpgradeLevels * 14
                + relicLevelSum * 18
                + formationSkillDamage * 3;
        }

        private static string FormatNumber(int value)
        {
            if (value >= 1000000)
            {
                return (value / 1000000f).ToString("0.#") + "M";
            }

            if (value >= 1000)
            {
                return (value / 1000f).ToString("0.#") + "K";
            }

            return value.ToString();
        }
    }
}
