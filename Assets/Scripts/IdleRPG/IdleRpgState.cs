using System;
using System.Collections.Generic;

namespace IdleRPG
{
    public enum CombatMode
    {
        Auto,
        Manual
    }

    public enum QuestType
    {
        PushStages,
        RelicGacha,
        CompanionGacha,
        RaiseCombatPower
    }

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
        public CompanionGachaState companionGacha = new CompanionGachaState();
        public CompanionDamageStats stageCompanionDamage = new CompanionDamageStats();
        public QuestState quest = new QuestState();
        public CombatMode combatMode = CombatMode.Auto;
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
        public int gems;
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
        public int peakCombatPower;
    }

    [Serializable]
    public sealed class QuestState
    {
        public int formatVersion = 3;
        public int cycleIndex;
        public int tier = 1;
        public int progress;
        public int target = 5;
        public bool readyToClaim;
        public int relicPullBaseline;
        public int companionPullBaseline;
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

    [Serializable]
    public sealed class CompanionGachaState
    {
        public int totalPulls;
        public int pity;
        public int lastCompanionId = -1;
        public string lastRarity = string.Empty;
        public CompanionCollection companions = new CompanionCollection();
        public CompanionFormation formation = new CompanionFormation();
    }

    [Serializable]
    public sealed class CompanionCollection
    {
        public int luna;
        public int aria;
        public int serin;
        public int yuri;
        public int mio;
        public int nari;
        public int rin;
        public int chae;
        public int haneul;
        public int rena;
        public int sia;
        public int iren;

        public int Get(int companionId)
        {
            switch (companionId)
            {
                case 0:
                    return luna;
                case 1:
                    return mio;
                case 2:
                    return nari;
                case 3:
                    return aria;
                case 4:
                    return rin;
                case 5:
                    return chae;
                case 6:
                    return serin;
                case 7:
                    return haneul;
                case 8:
                    return rena;
                case 9:
                    return yuri;
                case 10:
                    return sia;
                case 11:
                    return iren;
                default:
                    return 0;
            }
        }

        public void Increment(int companionId)
        {
            switch (companionId)
            {
                case 0:
                    luna += 1;
                    break;
                case 1:
                    mio += 1;
                    break;
                case 2:
                    nari += 1;
                    break;
                case 3:
                    aria += 1;
                    break;
                case 4:
                    rin += 1;
                    break;
                case 5:
                    chae += 1;
                    break;
                case 6:
                    serin += 1;
                    break;
                case 7:
                    haneul += 1;
                    break;
                case 8:
                    rena += 1;
                    break;
                case 9:
                    yuri += 1;
                    break;
                case 10:
                    sia += 1;
                    break;
                case 11:
                    iren += 1;
                    break;
            }
        }
    }

    [Serializable]
    public sealed class CompanionFormation
    {
        public int slot0 = -1;
        public int slot1 = -1;
        public int slot2 = -1;

        public int Get(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return slot0;
                case 1:
                    return slot1;
                case 2:
                    return slot2;
                default:
                    return -1;
            }
        }

        public void Set(int slotIndex, int companionId)
        {
            switch (slotIndex)
            {
                case 0:
                    slot0 = companionId;
                    break;
                case 1:
                    slot1 = companionId;
                    break;
                case 2:
                    slot2 = companionId;
                    break;
            }
        }

        public bool Contains(int companionId)
        {
            return slot0 == companionId || slot1 == companionId || slot2 == companionId;
        }

        public bool IsEmpty()
        {
            return slot0 < 0 && slot1 < 0 && slot2 < 0;
        }
    }

    [Serializable]
    public sealed class CompanionDamageStats
    {
        public int luna;
        public int mio;
        public int nari;
        public int aria;
        public int rin;
        public int chae;
        public int serin;
        public int haneul;
        public int rena;
        public int yuri;
        public int sia;
        public int iren;

        public int Get(int companionId)
        {
            switch (companionId)
            {
                case 0:
                    return luna;
                case 1:
                    return mio;
                case 2:
                    return nari;
                case 3:
                    return aria;
                case 4:
                    return rin;
                case 5:
                    return chae;
                case 6:
                    return serin;
                case 7:
                    return haneul;
                case 8:
                    return rena;
                case 9:
                    return yuri;
                case 10:
                    return sia;
                case 11:
                    return iren;
                default:
                    return 0;
            }
        }

        public void Add(int companionId, int damage)
        {
            switch (companionId)
            {
                case 0:
                    luna += damage;
                    break;
                case 1:
                    mio += damage;
                    break;
                case 2:
                    nari += damage;
                    break;
                case 3:
                    aria += damage;
                    break;
                case 4:
                    rin += damage;
                    break;
                case 5:
                    chae += damage;
                    break;
                case 6:
                    serin += damage;
                    break;
                case 7:
                    haneul += damage;
                    break;
                case 8:
                    rena += damage;
                    break;
                case 9:
                    yuri += damage;
                    break;
                case 10:
                    sia += damage;
                    break;
                case 11:
                    iren += damage;
                    break;
            }
        }

        public void Reset()
        {
            luna = 0;
            mio = 0;
            nari = 0;
            aria = 0;
            rin = 0;
            chae = 0;
            serin = 0;
            haneul = 0;
            rena = 0;
            yuri = 0;
            sia = 0;
            iren = 0;
        }
    }
}
