import {
  STORAGE_KEY,
  UPGRADE_DEFS,
  buyUpgrade,
  createInitialState,
  manualStrike,
  reviveState,
  serializeState,
  tick
} from "./gameState.js";

const canvas = document.querySelector("#battleCanvas");
const ctx = canvas.getContext("2d");
const manualStrikeButton = document.querySelector("#manualStrikeButton");
const upgradeList = document.querySelector("#upgradeList");
const offlineToast = document.querySelector("#offlineToast");

const elements = {
  stage: document.querySelector("#stageValue"),
  level: document.querySelector("#levelValue"),
  gold: document.querySelector("#goldValue"),
  heroHpText: document.querySelector("#heroHpText"),
  heroHpBar: document.querySelector("#heroHpBar"),
  enemyName: document.querySelector("#enemyName"),
  enemyHpText: document.querySelector("#enemyHpText"),
  enemyHpBar: document.querySelector("#enemyHpBar"),
  xpText: document.querySelector("#xpText"),
  xpBar: document.querySelector("#xpBar"),
  attack: document.querySelector("#attackValue"),
  regen: document.querySelector("#regenValue"),
  crit: document.querySelector("#critValue"),
  kills: document.querySelector("#killsValue"),
  battleLog: document.querySelector("#battleLog")
};

let state = loadState();
let lastFrame = performance.now();
let lastSave = performance.now();
let lastDomRender = 0;
let floatingTexts = [];

createUpgradeControls();
showOfflineReward();
renderDom();
requestAnimationFrame(gameLoop);

manualStrikeButton.addEventListener("click", () => {
  handleEvents(manualStrike(state));
  renderDom();
  saveGame();
});

canvas.addEventListener("click", () => {
  handleEvents(manualStrike(state));
  renderDom();
});

window.addEventListener("beforeunload", saveGame);

function gameLoop(now) {
  const delta = now - lastFrame;
  lastFrame = now;

  handleEvents(tick(state, delta));
  updateFloatingTexts(delta);
  drawBattlefield(now);

  if (now - lastDomRender > 120) {
    renderDom();
    lastDomRender = now;
  }

  if (now - lastSave > 3000) {
    saveGame();
    lastSave = now;
  }

  requestAnimationFrame(gameLoop);
}

function loadState() {
  try {
    return reviveState(localStorage.getItem(STORAGE_KEY));
  } catch {
    return createInitialState();
  }
}

function saveGame() {
  try {
    localStorage.setItem(STORAGE_KEY, serializeState(state));
  } catch {
    // Private browsing or storage quota errors should not interrupt gameplay.
  }
}

function createUpgradeControls() {
  upgradeList.innerHTML = "";

  Object.entries(UPGRADE_DEFS).forEach(([upgradeId, definition]) => {
    const button = document.createElement("button");
    button.className = "upgrade-card";
    button.type = "button";
    button.dataset.upgradeId = upgradeId;
    button.innerHTML = `
      <span>
        <strong>${definition.label}</strong>
        <small>${definition.description}</small>
      </span>
      <em></em>
    `;
    button.addEventListener("click", () => {
      if (buyUpgrade(state, upgradeId)) {
        addFloatingText("강화!", 480, 190, "#f8d66d");
        renderDom();
        saveGame();
      }
    });
    upgradeList.append(button);
  });
}

function renderDom() {
  const heroHpPercent = percent(state.hero.hp, state.hero.maxHp);
  const enemyHpPercent = percent(state.enemy.hp, state.enemy.maxHp);
  const xpPercent = percent(state.hero.xp, state.hero.xpToNext);

  elements.stage.textContent = state.stage;
  elements.level.textContent = state.hero.level;
  elements.gold.textContent = formatNumber(state.hero.gold);
  elements.heroHpText.textContent = `${formatNumber(state.hero.hp)} / ${formatNumber(state.hero.maxHp)}`;
  elements.heroHpBar.style.width = `${heroHpPercent}%`;
  elements.enemyName.textContent = state.enemy.isBoss ? `${state.enemy.name} ★` : state.enemy.name;
  elements.enemyHpText.textContent = `${formatNumber(state.enemy.hp)} / ${formatNumber(state.enemy.maxHp)}`;
  elements.enemyHpBar.style.width = `${enemyHpPercent}%`;
  elements.xpText.textContent = `${formatNumber(state.hero.xp)} / ${formatNumber(state.hero.xpToNext)}`;
  elements.xpBar.style.width = `${xpPercent}%`;
  elements.attack.textContent = formatNumber(state.hero.attack);
  elements.regen.textContent = state.hero.regen.toFixed(1);
  elements.crit.textContent = `${Math.round(state.hero.critChance * 100)}%`;
  elements.kills.textContent = formatNumber(state.stats.kills);

  document.querySelectorAll(".upgrade-card").forEach((button) => {
    const upgradeId = button.dataset.upgradeId;
    const level = state.upgrades[upgradeId];
    const cost = getCost(upgradeId);
    const costElement = button.querySelector("em");
    button.disabled = state.hero.gold < cost;
    costElement.textContent = `Lv.${level} · ${formatNumber(cost)}G`;
  });

  elements.battleLog.innerHTML = state.battleLog.map((entry) => `<li>${entry}</li>`).join("");
}

function handleEvents(events) {
  events.forEach((event) => {
    if (event.type === "hero-hit") {
      addFloatingText(
        event.critical ? `CRIT ${event.damage}` : event.damage,
        680,
        270,
        event.critical ? "#ffef9f" : "#ffffff"
      );
    }

    if (event.type === "enemy-hit") {
      addFloatingText(`-${event.damage}`, 285, 265, "#ff9b9b");
    }

    if (event.type === "enemy-defeated") {
      addFloatingText(`+${event.gold}G`, 695, 220, "#ffd166");
    }

    if (event.type === "level-up") {
      addFloatingText(`LEVEL ${event.level}`, 315, 170, "#7cf7c2");
    }
  });
}

function drawBattlefield(now) {
  const width = canvas.width;
  const height = canvas.height;
  const pulse = Math.sin(now / 350) * 4;
  const heroAttackProgress = state.combat.heroAttackTimer / 1000;
  const enemyAttackProgress = state.combat.enemyAttackTimer / 1450;

  const sky = ctx.createLinearGradient(0, 0, 0, height);
  sky.addColorStop(0, "#172446");
  sky.addColorStop(0.55, "#243a64");
  sky.addColorStop(1, "#16251f");
  ctx.fillStyle = sky;
  ctx.fillRect(0, 0, width, height);

  drawStars(now);
  drawHills();

  ctx.fillStyle = "#1f3a2b";
  ctx.fillRect(0, 385, width, 155);
  ctx.fillStyle = "rgba(255, 255, 255, 0.08)";
  ctx.fillRect(0, 386, width, 2);

  drawStageProgress();
  drawHero(260 + heroAttackProgress * 18, 330 + pulse * 0.2);
  drawEnemy(690 - enemyAttackProgress * 12, 322 - pulse * 0.35);
  drawFloatingTexts();
}

function drawStars(now) {
  ctx.fillStyle = "rgba(255, 255, 255, 0.65)";
  for (let i = 0; i < 32; i += 1) {
    const x = (i * 83 + 29) % canvas.width;
    const y = (i * 47 + 18) % 180;
    const alpha = 0.35 + Math.sin(now / 900 + i) * 0.25;
    ctx.globalAlpha = alpha;
    ctx.fillRect(x, y, i % 3 === 0 ? 3 : 2, 2);
  }
  ctx.globalAlpha = 1;
}

function drawHills() {
  ctx.fillStyle = "#203556";
  ctx.beginPath();
  ctx.moveTo(0, 320);
  ctx.quadraticCurveTo(160, 210, 310, 320);
  ctx.quadraticCurveTo(455, 220, 610, 320);
  ctx.quadraticCurveTo(770, 205, 960, 318);
  ctx.lineTo(960, 390);
  ctx.lineTo(0, 390);
  ctx.closePath();
  ctx.fill();

  ctx.fillStyle = "#183144";
  ctx.beginPath();
  ctx.moveTo(0, 350);
  ctx.quadraticCurveTo(210, 255, 390, 350);
  ctx.quadraticCurveTo(570, 250, 780, 350);
  ctx.quadraticCurveTo(860, 315, 960, 344);
  ctx.lineTo(960, 390);
  ctx.lineTo(0, 390);
  ctx.closePath();
  ctx.fill();
}

function drawStageProgress() {
  ctx.fillStyle = "rgba(255, 255, 255, 0.12)";
  roundRect(382, 38, 196, 28, 14);
  ctx.fill();

  for (let i = 0; i < 5; i += 1) {
    ctx.beginPath();
    ctx.arc(415 + i * 33, 52, 8, 0, Math.PI * 2);
    ctx.fillStyle = i < state.stageProgress ? "#7cf7c2" : "rgba(255, 255, 255, 0.22)";
    ctx.fill();
  }

  ctx.fillStyle = "#edf6ff";
  ctx.font = "700 16px system-ui";
  ctx.textAlign = "center";
  ctx.fillText(`Stage ${state.stage}`, 548, 57);
}

function drawHero(x, y) {
  drawShadow(x, y + 82, 95, 20);

  ctx.fillStyle = "#3d68ff";
  ctx.fillRect(x - 28, y - 4, 56, 78);
  ctx.fillStyle = "#8fb1ff";
  ctx.fillRect(x - 20, y + 8, 40, 25);
  ctx.fillStyle = "#f4c38a";
  ctx.fillRect(x - 22, y - 42, 44, 42);
  ctx.fillStyle = "#2e2140";
  ctx.fillRect(x - 26, y - 48, 52, 16);
  ctx.fillStyle = "#141a2f";
  ctx.fillRect(x - 13, y + 74, 18, 38);
  ctx.fillRect(x + 12, y + 74, 18, 38);

  ctx.strokeStyle = "#d9edff";
  ctx.lineWidth = 7;
  ctx.beginPath();
  ctx.moveTo(x + 34, y + 12);
  ctx.lineTo(x + 80, y - 34);
  ctx.stroke();

  ctx.fillStyle = "#ffffff";
  ctx.fillRect(x - 12, y - 25, 7, 7);
  ctx.fillRect(x + 8, y - 25, 7, 7);
}

function drawEnemy(x, y) {
  const radius = state.enemy.isBoss ? 72 : 54;
  drawShadow(x, y + radius, radius * 1.7, 26);

  const body = ctx.createRadialGradient(x - 16, y - 24, 10, x, y, radius);
  body.addColorStop(0, `hsl(${state.enemy.hue}, 85%, 68%)`);
  body.addColorStop(1, `hsl(${state.enemy.hue}, 58%, 38%)`);

  ctx.fillStyle = body;
  ctx.beginPath();
  ctx.arc(x, y, radius, 0, Math.PI * 2);
  ctx.fill();

  ctx.fillStyle = "rgba(0, 0, 0, 0.26)";
  ctx.beginPath();
  ctx.arc(x - 18, y - 8, 7, 0, Math.PI * 2);
  ctx.arc(x + 18, y - 8, 7, 0, Math.PI * 2);
  ctx.fill();

  ctx.strokeStyle = "rgba(0, 0, 0, 0.3)";
  ctx.lineWidth = 5;
  ctx.beginPath();
  ctx.arc(x, y + 14, 19, 0.1, Math.PI - 0.1);
  ctx.stroke();

  if (state.enemy.isBoss) {
    ctx.fillStyle = "#ffd166";
    ctx.fillRect(x - 34, y - radius - 20, 68, 12);
    ctx.fillRect(x - 22, y - radius - 34, 13, 18);
    ctx.fillRect(x + 9, y - radius - 34, 13, 18);
  }
}

function drawShadow(x, y, width, height) {
  ctx.fillStyle = "rgba(0, 0, 0, 0.28)";
  ctx.beginPath();
  ctx.ellipse(x, y, width / 2, height / 2, 0, 0, Math.PI * 2);
  ctx.fill();
}

function addFloatingText(text, x, y, color) {
  floatingTexts.push({ text, x, y, color, life: 900, maxLife: 900 });
}

function updateFloatingTexts(delta) {
  floatingTexts = floatingTexts
    .map((item) => ({ ...item, y: item.y - delta * 0.035, life: item.life - delta }))
    .filter((item) => item.life > 0);
}

function drawFloatingTexts() {
  ctx.font = "800 24px system-ui";
  ctx.textAlign = "center";
  floatingTexts.forEach((item) => {
    ctx.globalAlpha = Math.max(0, item.life / item.maxLife);
    ctx.fillStyle = item.color;
    ctx.fillText(item.text, item.x, item.y);
  });
  ctx.globalAlpha = 1;
}

function showOfflineReward() {
  if (!state.offlineReward) {
    return;
  }

  offlineToast.textContent = `쉬는 동안 ${formatNumber(state.offlineReward.gold)}골드와 ${formatNumber(
    state.offlineReward.xp
  )} 경험치를 획득했습니다.`;
  offlineToast.classList.add("visible");
  window.setTimeout(() => offlineToast.classList.remove("visible"), 5200);
}

function getCost(upgradeId) {
  const definition = UPGRADE_DEFS[upgradeId];
  return Math.floor(definition.baseCost * definition.growth ** state.upgrades[upgradeId]);
}

function percent(value, max) {
  if (max <= 0) {
    return 0;
  }

  return Math.max(0, Math.min(100, (value / max) * 100));
}

function formatNumber(value) {
  return Math.floor(value).toLocaleString("ko-KR");
}

function roundRect(x, y, width, height, radius) {
  ctx.beginPath();
  ctx.moveTo(x + radius, y);
  ctx.arcTo(x + width, y, x + width, y + height, radius);
  ctx.arcTo(x + width, y + height, x, y + height, radius);
  ctx.arcTo(x, y + height, x, y, radius);
  ctx.arcTo(x, y, x + width, y, radius);
  ctx.closePath();
}
