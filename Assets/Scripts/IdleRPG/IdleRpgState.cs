using System;
using System.Collections.Generic;

namespace IdleRPG
{
    [Serializable]
    public sealed class IdleRpgState
    {
        public int version = 1;
        public HeroState hero = new HeroState();
        public UpgradeLevels upgrades = new UpgradeLevels();
        public int stage = 1;
        public int stageProgress;
        public EnemyState enemy = new EnemyState();
        public CombatTimers combat = new CombatTimers();
        public GameStats stats = new GameStats();
        public GachaState gacha = new GachaState();
        public List<string> battleLog = new List<string>();
        public long lastSavedUnixSeconds;
    }

    [Serializable]
    public sealed class HeroState
    {
        public int level = 1;
        public int xp;
        public int xpToNext = 20;
        public int gold;
        public int attack = 8;
        public int maxHp = 100;
        public float hp = 100f;
        public float regen = 1f;
        public float critChance = 0.05f;
        public float critMultiplier = 1.8f;
    }

    [Serializable]
    public sealed class UpgradeLevels
    {
        public int blade;
        public int armor;
        public int regeneration;
        public int focus;

        public int Get(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.Blade:
                    return blade;
                case UpgradeType.Armor:
                    return armor;
                case UpgradeType.Regeneration:
                    return regeneration;
                case UpgradeType.Focus:
                    return focus;
                default:
                    return 0;
            }
        }

        public void Increment(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.Blade:
                    blade += 1;
                    break;
                case UpgradeType.Armor:
                    armor += 1;
                    break;
                case UpgradeType.Regeneration:
                    regeneration += 1;
                    break;
                case UpgradeType.Focus:
                    focus += 1;
                    break;
            }
        }
    }

    [Serializable]
    public sealed class EnemyState
    {
        public string name = "이끼 슬라임";
        public float hue = 0.33f;
        public bool isBoss;
        public int maxHp = 38;
        public float hp = 38f;
        public int attack = 4;
        public int rewardGold = 8;
        public int rewardXp = 5;
    }

    [Serializable]
    public sealed class CombatTimers
    {
        public float heroAttack;
        public float enemyAttack;
    }

    [Serializable]
    public sealed class GameStats
    {
        public int kills;
        public int totalGold;
        public int totalXp;
        public int highestStage = 1;
    }

    [Serializable]
    public sealed class GachaState
    {
        public int totalPulls;
        public int pity;
        public int lastRelicId = -1;
        public string lastRarity = string.Empty;
        public RelicCollection relics = new RelicCollection();
    }

    [Serializable]
    public sealed class RelicCollection
    {
        public int emberBlade;
        public int guardianCharm;
        public int moonPendant;
        public int forestCrown;

        public int Get(int relicId)
        {
            switch (relicId)
            {
                case 0:
                    return emberBlade;
                case 1:
                    return guardianCharm;
                case 2:
                    return moonPendant;
                case 3:
                    return forestCrown;
                default:
                    return 0;
            }
        }

        public void Increment(int relicId)
        {
            switch (relicId)
            {
                case 0:
                    emberBlade += 1;
                    break;
                case 1:
                    guardianCharm += 1;
                    break;
                case 2:
                    moonPendant += 1;
                    break;
                case 3:
                    forestCrown += 1;
                    break;
            }
        }
    }
}
