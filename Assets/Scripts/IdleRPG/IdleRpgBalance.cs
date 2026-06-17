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

    public static class IdleRpgBalance
    {
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
    }
}
