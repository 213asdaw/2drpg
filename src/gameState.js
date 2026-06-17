export const STORAGE_KEY = "idle-rpg-save-v1";

const SAVE_VERSION = 1;
const MAX_LOG_ENTRIES = 8;
const MAX_OFFLINE_MS = 2 * 60 * 60 * 1000;

const ENEMY_ROSTER = [
  { name: "이끼 슬라임", hue: 116, baseHp: 38, baseAttack: 4, gold: 8, xp: 5 },
  { name: "숲 고블린", hue: 80, baseHp: 48, baseAttack: 5, gold: 10, xp: 6 },
  { name: "검은 박쥐", hue: 270, baseHp: 44, baseAttack: 6, gold: 11, xp: 7 },
  { name: "나무 정령", hue: 145, baseHp: 62, baseAttack: 7, gold: 14, xp: 9 },
  { name: "숲의 수호자", hue: 18, baseHp: 110, baseAttack: 10, gold: 30, xp: 18, boss: true }
];

export const UPGRADE_DEFS = {
  blade: {
    label: "검술 훈련",
    description: "공격력 +4",
    baseCost: 24,
    growth: 1.32
  },
  armor: {
    label: "강화 갑옷",
    description: "최대 HP +22",
    baseCost: 32,
    growth: 1.36
  },
  regeneration: {
    label: "회복의 룬",
    description: "초당 회복 +0.7",
    baseCost: 45,
    growth: 1.42
  },
  focus: {
    label: "집중 수련",
    description: "치명타 확률 +2.5%",
    baseCost: 58,
    growth: 1.46
  }
};

export function createInitialState(now = Date.now()) {
  return {
    version: SAVE_VERSION,
    hero: {
      level: 1,
      xp: 0,
      xpToNext: 20,
      gold: 0,
      attack: 8,
      maxHp: 100,
      hp: 100,
      regen: 1,
      critChance: 0.05,
      critMultiplier: 1.8
    },
    upgrades: {
      blade: 0,
      armor: 0,
      regeneration: 0,
      focus: 0
    },
    stage: 1,
    stageProgress: 0,
    enemy: createEnemy(1),
    combat: {
      heroAttackTimer: 0,
      enemyAttackTimer: 0
    },
    stats: {
      kills: 0,
      totalGold: 0,
      totalXp: 0,
      highestStage: 1
    },
    battleLog: ["모험을 시작했습니다. 용사가 자동으로 전투합니다."],
    offlineReward: null,
    lastSavedAt: now
  };
}

export function createEnemy(stage) {
  const rosterIndex = (stage - 1) % ENEMY_ROSTER.length;
  const template = ENEMY_ROSTER[rosterIndex];
  const cycle = Math.floor((stage - 1) / ENEMY_ROSTER.length);
  const stageScale = 1 + (stage - 1) * 0.18 + cycle * 0.28;
  const bossScale = template.boss ? 1.35 : 1;

  return {
    name: template.name,
    hue: template.hue,
    isBoss: Boolean(template.boss),
    maxHp: Math.floor(template.baseHp * stageScale * bossScale),
    hp: Math.floor(template.baseHp * stageScale * bossScale),
    attack: Math.floor(template.baseAttack * stageScale * bossScale),
    rewardGold: Math.floor(template.gold * stageScale * bossScale),
    rewardXp: Math.floor(template.xp * stageScale * bossScale)
  };
}

export function getUpgradeCost(state, upgradeId) {
  const definition = UPGRADE_DEFS[upgradeId];
  if (!definition) {
    throw new Error(`Unknown upgrade: ${upgradeId}`);
  }

  return Math.floor(definition.baseCost * definition.growth ** state.upgrades[upgradeId]);
}

export function buyUpgrade(state, upgradeId) {
  const cost = getUpgradeCost(state, upgradeId);
  if (state.hero.gold < cost) {
    return false;
  }

  state.hero.gold -= cost;
  state.upgrades[upgradeId] += 1;

  if (upgradeId === "blade") {
    state.hero.attack += 4;
  }

  if (upgradeId === "armor") {
    state.hero.maxHp += 22;
    state.hero.hp += 22;
  }

  if (upgradeId === "regeneration") {
    state.hero.regen += 0.7;
  }

  if (upgradeId === "focus") {
    state.hero.critChance = Math.min(0.45, state.hero.critChance + 0.025);
  }

  addLog(state, `${definitionLabel(upgradeId)} 강화 완료!`);
  return true;
}

export function manualStrike(state, random = Math.random) {
  if (state.hero.hp <= 0) {
    return [];
  }

  const events = [];
  applyHeroDamage(state, 0.65, random, events, "직접 공격");
  return events;
}

export function tick(state, deltaMs, random = Math.random) {
  if (deltaMs <= 0) {
    return [];
  }

  const events = [];
  const safeDeltaMs = Math.min(deltaMs, 5000);
  const seconds = safeDeltaMs / 1000;

  state.hero.hp = Math.min(state.hero.maxHp, state.hero.hp + state.hero.regen * seconds);
  state.combat.heroAttackTimer += safeDeltaMs;
  state.combat.enemyAttackTimer += safeDeltaMs;

  while (state.combat.heroAttackTimer >= 1000) {
    state.combat.heroAttackTimer -= 1000;
    applyHeroDamage(state, 1, random, events, "자동 공격");
  }

  while (state.combat.enemyAttackTimer >= 1450) {
    state.combat.enemyAttackTimer -= 1450;
    applyEnemyDamage(state, events);
  }

  return events;
}

export function applyOfflineProgress(state, elapsedMs) {
  const offlineMs = Math.min(Math.max(0, elapsedMs), MAX_OFFLINE_MS);
  if (offlineMs < 60 * 1000) {
    state.offlineReward = null;
    return null;
  }

  const minutes = offlineMs / (60 * 1000);
  const stageBonus = 1 + (state.stage - 1) * 0.16;
  const reward = {
    gold: Math.floor(minutes * (7 + stageBonus * 4)),
    xp: Math.floor(minutes * (3 + stageBonus * 2)),
    minutes: Math.floor(minutes)
  };

  state.hero.gold += reward.gold;
  state.stats.totalGold += reward.gold;
  gainXp(state, reward.xp, []);
  state.offlineReward = reward;
  addLog(state, `오프라인 보상: ${reward.gold}골드, ${reward.xp}경험치`);
  return reward;
}

export function serializeState(state, now = Date.now()) {
  const snapshot = JSON.parse(JSON.stringify(state));
  snapshot.offlineReward = null;
  snapshot.lastSavedAt = now;
  return JSON.stringify(snapshot);
}

export function reviveState(serializedState, now = Date.now()) {
  if (!serializedState) {
    return createInitialState(now);
  }

  try {
    const saved = JSON.parse(serializedState);
    if (saved.version !== SAVE_VERSION) {
      return createInitialState(now);
    }

    const fallback = createInitialState(now);
    const state = {
      ...fallback,
      ...saved,
      hero: { ...fallback.hero, ...saved.hero },
      upgrades: { ...fallback.upgrades, ...saved.upgrades },
      combat: { ...fallback.combat, ...saved.combat },
      stats: { ...fallback.stats, ...saved.stats },
      enemy: { ...createEnemy(saved.stage || 1), ...saved.enemy },
      battleLog: Array.isArray(saved.battleLog) ? saved.battleLog.slice(0, MAX_LOG_ENTRIES) : fallback.battleLog
    };

    if (state.enemy.hp <= 0) {
      state.enemy = createEnemy(state.stage);
    }

    applyOfflineProgress(state, now - (saved.lastSavedAt || now));
    state.lastSavedAt = now;
    return state;
  } catch {
    return createInitialState(now);
  }
}

function applyHeroDamage(state, multiplier, random, events, source) {
  const isCritical = random() < state.hero.critChance;
  const criticalMultiplier = isCritical ? state.hero.critMultiplier : 1;
  const damage = Math.max(1, Math.floor(state.hero.attack * multiplier * criticalMultiplier));

  state.enemy.hp = Math.max(0, state.enemy.hp - damage);
  events.push({ type: "hero-hit", damage, critical: isCritical, source });

  if (state.enemy.hp <= 0) {
    defeatEnemy(state, events);
  }
}

function applyEnemyDamage(state, events) {
  if (state.enemy.hp <= 0) {
    return;
  }

  state.hero.hp = Math.max(0, state.hero.hp - state.enemy.attack);
  events.push({ type: "enemy-hit", damage: state.enemy.attack });

  if (state.hero.hp <= 0) {
    state.hero.hp = state.hero.maxHp;
    state.combat.heroAttackTimer = 0;
    state.combat.enemyAttackTimer = 0;
    addLog(state, "용사가 쓰러졌지만 캠프에서 회복했습니다.");
    events.push({ type: "hero-revive" });
  }
}

function defeatEnemy(state, events) {
  const defeatedEnemy = state.enemy;
  state.hero.gold += defeatedEnemy.rewardGold;
  state.stats.totalGold += defeatedEnemy.rewardGold;
  state.stats.kills += 1;
  state.stageProgress += 1;
  gainXp(state, defeatedEnemy.rewardXp, events);

  events.push({
    type: "enemy-defeated",
    name: defeatedEnemy.name,
    gold: defeatedEnemy.rewardGold,
    xp: defeatedEnemy.rewardXp
  });

  if (state.stageProgress >= 5) {
    state.stage += 1;
    state.stageProgress = 0;
    state.stats.highestStage = Math.max(state.stats.highestStage, state.stage);
    addLog(state, `스테이지 ${state.stage}에 도달했습니다.`);
  } else {
    addLog(state, `${defeatedEnemy.name} 처치! +${defeatedEnemy.rewardGold}골드`);
  }

  state.enemy = createEnemy(state.stage);
}

function gainXp(state, amount, events) {
  state.hero.xp += amount;
  state.stats.totalXp += amount;

  while (state.hero.xp >= state.hero.xpToNext) {
    state.hero.xp -= state.hero.xpToNext;
    state.hero.level += 1;
    state.hero.xpToNext = Math.floor(state.hero.xpToNext * 1.35 + 10);
    state.hero.maxHp += 12;
    state.hero.attack += 2;
    state.hero.hp = state.hero.maxHp;
    addLog(state, `레벨 ${state.hero.level} 달성!`);
    events.push({ type: "level-up", level: state.hero.level });
  }
}

function addLog(state, message) {
  state.battleLog = [message, ...state.battleLog].slice(0, MAX_LOG_ENTRIES);
}

function definitionLabel(upgradeId) {
  return UPGRADE_DEFS[upgradeId]?.label ?? "알 수 없는 능력";
}
