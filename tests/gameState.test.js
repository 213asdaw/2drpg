import assert from "node:assert/strict";
import test from "node:test";

import {
  applyOfflineProgress,
  buyUpgrade,
  createInitialState,
  getUpgradeCost,
  manualStrike,
  reviveState,
  serializeState,
  tick
} from "../src/gameState.js";

test("hero defeats an enemy and receives rewards", () => {
  const state = createInitialState();
  const goldBefore = state.hero.gold;
  const xpBefore = state.hero.xp;
  state.enemy.hp = 1;

  const events = tick(state, 1000, () => 1);

  assert.equal(state.stats.kills, 1);
  assert.ok(state.hero.gold > goldBefore);
  assert.ok(state.hero.xp > xpBefore);
  assert.equal(state.stageProgress, 1);
  assert.ok(events.some((event) => event.type === "enemy-defeated"));
});

test("buying an upgrade spends gold and improves hero stats", () => {
  const state = createInitialState();
  const cost = getUpgradeCost(state, "blade");
  state.hero.gold = cost;

  const purchased = buyUpgrade(state, "blade");

  assert.equal(purchased, true);
  assert.equal(state.upgrades.blade, 1);
  assert.equal(state.hero.gold, 0);
  assert.equal(state.hero.attack, 12);
});

test("manual strike can critically hit", () => {
  const state = createInitialState();
  const hpBefore = state.enemy.hp;

  const events = manualStrike(state, () => 0);

  assert.equal(events[0].critical, true);
  assert.equal(hpBefore - state.enemy.hp, 9);
});

test("offline progress grants capped idle rewards", () => {
  const state = createInitialState();

  const reward = applyOfflineProgress(state, 3 * 60 * 60 * 1000);

  assert.equal(reward.minutes, 120);
  assert.ok(reward.gold > 0);
  assert.ok(reward.xp > 0);
  assert.equal(state.offlineReward, reward);
});

test("saved games revive with offline rewards applied", () => {
  const savedAt = 1_000;
  const state = createInitialState(savedAt);
  const serialized = serializeState(state, savedAt);

  const revived = reviveState(serialized, savedAt + 10 * 60 * 1000);

  assert.ok(revived.hero.gold > state.hero.gold);
  assert.equal(revived.offlineReward.minutes, 10);
  assert.equal(revived.lastSavedAt, savedAt + 10 * 60 * 1000);
});
