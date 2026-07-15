import "./style.css";
import { createDefaultPlan, TEMPLATES, type SinkPlan, type TemplateId } from "./types";
import { PlanEditor } from "./PlanEditor";
import { SinkScene } from "./SinkScene";

const app = document.querySelector<HTMLDivElement>("#app")!;

app.innerHTML = `
  <header class="hero">
    <div class="brand-row">
      <h1 class="brand">싱크<em>플랜</em></h1>
    </div>
    <p class="tagline">
      싱크대 도면을 맞추면, 바로 옆에 3D로 구현된 모습이 펼쳐집니다.
      템플릿을 고르고 치수를 조정하거나, 도면 이미지를 올려 맞춰 보세요.
    </p>
  </header>
  <main class="workspace">
    <aside class="panel" id="controls"></aside>
    <section class="plan-pane">
      <div class="pane-label"><span class="dot"></span> 2D 도면</div>
      <canvas id="plan-canvas"></canvas>
    </section>
    <section class="view-pane">
      <div class="pane-label"><span class="dot"></span> 3D 미리보기</div>
      <div id="view-host"></div>
    </section>
  </main>
`;

let plan = createDefaultPlan("straight-single");
const planCanvas = document.querySelector<HTMLCanvasElement>("#plan-canvas")!;
const viewHost = document.querySelector<HTMLDivElement>("#view-host")!;
const controls = document.querySelector<HTMLElement>("#controls")!;

const editor = new PlanEditor(planCanvas, plan);
const scene = new SinkScene(viewHost);
scene.setPlan(plan, true);
// Layout can settle after first paint — reframe once.
requestAnimationFrame(() => {
  scene.frame();
  window.dispatchEvent(new Event("resize"));
});

let syncing = false;

function applyPlan(next: SinkPlan, opts: { refitEditor?: boolean; frameCamera?: boolean } = {}) {
  plan = next;
  if (!syncing) {
    syncing = true;
    editor.setPlan(plan, opts.refitEditor ?? false);
    // Reframe whenever a substantial plan rebuild happens
    scene.setPlan(plan, opts.frameCamera ?? true);
    syncing = false;
  }
  renderControls();
}

editor.on("change", (next) => {
  plan = next;
  // Drag updates: keep camera
  scene.setPlan(plan, false);
});

function renderControls() {
  const bowl = plan.bowls[0];
  controls.innerHTML = `
    <div class="section">
      <h2>도면 템플릿</h2>
      <div class="templates">
        ${(Object.keys(TEMPLATES) as TemplateId[])
          .map((id) => {
            const t = TEMPLATES[id];
            return `<button type="button" class="template-btn ${plan.template === id ? "active" : ""}" data-template="${id}">
              <strong>${t.label}</strong>
              <span>${t.description}</span>
            </button>`;
          })
          .join("")}
      </div>
    </div>

    <div class="section">
      <h2>전체 치수 (mm)</h2>
      <div class="field">
        <label for="counterWidth">가로 폭</label>
        <div class="field-row">
          <input id="counterWidth" type="range" min="900" max="3200" step="50" value="${plan.counterWidth}" />
          <output>${plan.counterWidth}</output>
        </div>
      </div>
      <div class="field">
        <label for="counterDepth">깊이</label>
        <div class="field-row">
          <input id="counterDepth" type="range" min="450" max="1200" step="10" value="${plan.counterDepth}" />
          <output>${plan.counterDepth}</output>
        </div>
      </div>
      ${
        plan.template === "l-shape"
          ? `<div class="field">
              <label for="returnWidth">리턴 길이 (Z)</label>
              <div class="field-row">
                <input id="returnWidth" type="range" min="900" max="2800" step="50" value="${plan.returnWidth}" />
                <output>${plan.returnWidth}</output>
              </div>
            </div>
            <div class="field">
              <label for="returnDepth">리턴 깊이 (X)</label>
              <div class="field-row">
                <input id="returnDepth" type="range" min="450" max="900" step="10" value="${plan.returnDepth}" />
                <output>${plan.returnDepth}</output>
              </div>
            </div>`
          : ""
      }
      <div class="field">
        <label for="cabinetHeight">장 높이</label>
        <div class="field-row">
          <input id="cabinetHeight" type="range" min="600" max="900" step="10" value="${plan.cabinetHeight}" />
          <output>${plan.cabinetHeight}</output>
        </div>
      </div>
      <div class="field">
        <label for="backsplashHeight">백스플래시</label>
        <div class="field-row">
          <input id="backsplashHeight" type="range" min="0" max="300" step="10" value="${plan.backsplashHeight}" />
          <output>${plan.backsplashHeight}</output>
        </div>
      </div>
    </div>

    <div class="section">
      <h2>싱크 볼</h2>
      <div class="field">
        <label for="bowlWidth">볼 가로</label>
        <div class="field-row">
          <input id="bowlWidth" type="range" min="300" max="700" step="10" value="${bowl.width}" />
          <output>${bowl.width}</output>
        </div>
      </div>
      <div class="field">
        <label for="bowlDepth">볼 세로</label>
        <div class="field-row">
          <input id="bowlDepth" type="range" min="280" max="550" step="10" value="${bowl.depth}" />
          <output>${bowl.depth}</output>
        </div>
      </div>
      <div class="field">
        <label for="bowlBowlDepth">볼 깊이</label>
        <div class="field-row">
          <input id="bowlBowlDepth" type="range" min="120" max="280" step="5" value="${bowl.bowlDepth}" />
          <output>${bowl.bowlDepth}</output>
        </div>
      </div>
      <label class="toggle">
        <input type="checkbox" id="faucet" ${plan.faucet ? "checked" : ""} />
        수전 표시
      </label>
      <p class="hint">도면에서 볼을 드래그하면 위치가 바로 3D에 반영됩니다.</p>
    </div>

    <div class="section">
      <h2>재질</h2>
      <div class="field">
        <label>상판</label>
        <div class="chips" data-group="counterMaterial">
          ${chip("counterMaterial", "white-quartz", "화이트 쿼츠", plan.counterMaterial)}
          ${chip("counterMaterial", "oak", "오크", plan.counterMaterial)}
          ${chip("counterMaterial", "black-granite", "블랙 그라나이트", plan.counterMaterial)}
          ${chip("counterMaterial", "concrete", "콘크리트", plan.counterMaterial)}
        </div>
      </div>
      <div class="field">
        <label>수납장</label>
        <div class="chips" data-group="cabinetMaterial">
          ${chip("cabinetMaterial", "walnut", "월넛", plan.cabinetMaterial)}
          ${chip("cabinetMaterial", "white", "화이트", plan.cabinetMaterial)}
          ${chip("cabinetMaterial", "sage", "세이지", plan.cabinetMaterial)}
        </div>
      </div>
      <div class="field">
        <label>싱크</label>
        <div class="chips" data-group="sinkMaterial">
          ${chip("sinkMaterial", "stainless", "스텐", plan.sinkMaterial)}
          ${chip("sinkMaterial", "white-ceramic", "화이트 세라믹", plan.sinkMaterial)}
          ${chip("sinkMaterial", "matte-black", "매트 블랙", plan.sinkMaterial)}
        </div>
      </div>
    </div>

    <div class="section">
      <h2>도면 이미지</h2>
      <div class="actions">
        <label class="btn btn-ghost file-btn">
          도면 이미지 불러오기
          <input type="file" id="blueprintFile" accept="image/*" />
        </label>
        <button type="button" class="btn btn-ghost" id="clearBlueprint">이미지 제거</button>
      </div>
      <label class="toggle" style="margin-top:0.75rem">
        <input type="checkbox" id="showBlueprint" ${plan.showBlueprint ? "checked" : ""} />
        도면을 2D에 오버레이
      </label>
      <div class="field" style="margin-top:0.6rem">
        <label for="blueprintOpacity">오버레이 투명도</label>
        <div class="field-row">
          <input id="blueprintOpacity" type="range" min="0.1" max="0.8" step="0.05" value="${plan.blueprintOpacity}" />
          <output>${Math.round(plan.blueprintOpacity * 100)}%</output>
        </div>
      </div>
      <p class="hint">스케치·캐드 캡처를 올리면 치수에 맞춰 아래에 깔립니다. 볼 위치를 도면에 맞추세요.</p>
    </div>

    <div class="section">
      <h2>내보내기</h2>
      <div class="actions">
        <button type="button" class="btn btn-primary" id="captureBtn">3D 화면 저장 (PNG)</button>
        <button type="button" class="btn btn-ghost" id="resetCamera">카메라 리셋</button>
        <button type="button" class="btn btn-ghost" id="topViewBtn">위에서 보기</button>
      </div>
    </div>
  `;

  bindControls();
}

function chip(group: string, value: string, label: string, current: string) {
  return `<button type="button" class="chip ${current === value ? "active" : ""}" data-group="${group}" data-value="${value}">${label}</button>`;
}

function bindControls() {
  controls.querySelectorAll<HTMLButtonElement>("[data-template]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const id = btn.dataset.template as TemplateId;
      applyPlan(createDefaultPlan(id), { refitEditor: true, frameCamera: true });
    });
  });

  const rangeIds = [
    "counterWidth",
    "counterDepth",
    "returnWidth",
    "returnDepth",
    "cabinetHeight",
    "backsplashHeight",
    "bowlWidth",
    "bowlDepth",
    "bowlBowlDepth",
    "blueprintOpacity",
  ] as const;

  for (const id of rangeIds) {
    const el = document.getElementById(id) as HTMLInputElement | null;
    if (!el) continue;
    el.addEventListener("input", () => {
      const next = structuredClone(plan);
      const v = Number(el.value);
      if (id === "bowlWidth") next.bowls.forEach((b) => (b.width = v));
      else if (id === "bowlDepth") next.bowls.forEach((b) => (b.depth = v));
      else if (id === "bowlBowlDepth") next.bowls.forEach((b) => (b.bowlDepth = v));
      else if (id === "blueprintOpacity") next.blueprintOpacity = v;
      else if (id === "counterWidth") next.counterWidth = v;
      else if (id === "counterDepth") next.counterDepth = v;
      else if (id === "returnWidth") next.returnWidth = v;
      else if (id === "returnDepth") next.returnDepth = v;
      else if (id === "cabinetHeight") next.cabinetHeight = v;
      else if (id === "backsplashHeight") next.backsplashHeight = v;

      // Keep bowls inside counter
      for (const b of next.bowls) {
        b.offsetX = Math.min(b.offsetX, next.counterWidth - b.width - 20);
        b.offsetZ = Math.min(b.offsetZ, next.counterDepth - b.depth - 20);
      }

      const out = el.parentElement?.querySelector("output");
      if (out) {
        out.textContent = id === "blueprintOpacity" ? `${Math.round(v * 100)}%` : String(v);
      }
      applyPlan(next);
    });
  }

  const faucet = document.getElementById("faucet") as HTMLInputElement | null;
  faucet?.addEventListener("change", () => {
    const next = structuredClone(plan);
    next.faucet = faucet.checked;
    applyPlan(next);
  });

  const showBlueprint = document.getElementById("showBlueprint") as HTMLInputElement | null;
  showBlueprint?.addEventListener("change", () => {
    const next = structuredClone(plan);
    next.showBlueprint = showBlueprint.checked;
    applyPlan(next);
  });

  controls.querySelectorAll<HTMLButtonElement>(".chip").forEach((btn) => {
    btn.addEventListener("click", () => {
      const group = btn.dataset.group;
      const value = btn.dataset.value!;
      const next = structuredClone(plan);
      if (group === "counterMaterial") next.counterMaterial = value as SinkPlan["counterMaterial"];
      else if (group === "cabinetMaterial") next.cabinetMaterial = value as SinkPlan["cabinetMaterial"];
      else if (group === "sinkMaterial") next.sinkMaterial = value as SinkPlan["sinkMaterial"];
      applyPlan(next);
    });
  });

  const file = document.getElementById("blueprintFile") as HTMLInputElement | null;
  file?.addEventListener("change", () => {
    const f = file.files?.[0];
    if (!f) return;
    const reader = new FileReader();
    reader.onload = () => {
      const url = String(reader.result);
      editor.setBlueprintImage(url);
      const next = structuredClone(plan);
      next.showBlueprint = true;
      applyPlan(next);
    };
    reader.readAsDataURL(f);
  });

  document.getElementById("clearBlueprint")?.addEventListener("click", () => {
    editor.setBlueprintImage(null);
  });

  document.getElementById("captureBtn")?.addEventListener("click", () => {
    const url = scene.capturePng();
    const a = document.createElement("a");
    a.href = url;
    a.download = `sinkplan-${plan.template}-${Date.now()}.png`;
    a.click();
  });

  document.getElementById("resetCamera")?.addEventListener("click", () => {
    scene.frame();
  });

  document.getElementById("topViewBtn")?.addEventListener("click", () => {
    scene.topView();
  });
}

renderControls();
