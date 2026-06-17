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
            HairColor = hairColor;
            OutfitColor = outfitColor;
            AccentColor = accentColor;
        }
    }

    public static class IdleRpgBalance
    {
        public const int GachaGoldCost = 120;
        public const int RarePityPulls = 8;
        public const int CompanionGachaGoldCost = 240;
        public const int CompanionRarePityPulls = 10;

        public static readonly UpgradeDefinition[] Upgrades =
        {
            new UpgradeDefinition(UpgradeType.Blade, "검술 훈련", "공격력 +4", 24, 1.32f),
            new UpgradeDefinition(UpgradeType.Armor, "강화 갑옷", "최대 HP +22", 32, 1.36f),
            new UpgradeDefinition(UpgradeType.Regeneration, "회복의 룬", "초당 회복 +0.7", 45, 1.42f),
            new UpgradeDefinition(UpgradeType.Focus, "집중 수련", "치명타 확률 +2.5%", 58, 1.46f)
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
            new CompanionDefinition(
                0,
                "루나",
                "달빛 견습 마법사",
                "전투를 도와 공격력을 올려줍니다.",
                RelicRarity.Common,
                52f,
                4,
                4,
                0f,
                0f,
                new Color(0.24f, 0.18f, 0.36f),
                new Color(0.42f, 0.52f, 0.98f),
                new Color(0.92f, 0.88f, 1f)),
            new CompanionDefinition(
                1,
                "아리아",
                "꽃잎 궁수",
                "체력과 치명타를 함께 올려줍니다.",
                RelicRarity.Rare,
                30f,
                2,
                18,
                0f,
                0.006f,
                new Color(0.58f, 0.31f, 0.20f),
                new Color(0.96f, 0.45f, 0.65f),
                new Color(0.64f, 1f, 0.70f)),
            new CompanionDefinition(
                2,
                "세린",
                "별빛 성녀",
                "회복과 생존력을 크게 보강합니다.",
                RelicRarity.Epic,
                14f,
                2,
                24,
                0.45f,
                0.006f,
                new Color(0.95f, 0.88f, 0.58f),
                new Color(0.82f, 0.55f, 1f),
                new Color(1f, 0.96f, 0.60f)),
            new CompanionDefinition(
                3,
                "유리",
                "여우 검희",
                "공격과 치명타를 폭발적으로 강화합니다.",
                RelicRarity.Legendary,
                4f,
                7,
                12,
                0.25f,
                0.018f,
                new Color(0.98f, 0.78f, 0.45f),
                new Color(0.95f, 0.22f, 0.30f),
                new Color(1f, 0.82f, 0.30f))
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
    }
}
