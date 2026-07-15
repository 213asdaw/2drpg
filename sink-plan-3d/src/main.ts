import "./style.css";
import { createDefaultPlan, TEMPLATES, resizeWidths, sumWidths, syncPlanFromCabinets, type SinkPlan, type TemplateId } from "./types";
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
  plan = syncPlanFromCabinets(next);
  if (!syncing) {
    syncing = true;
    editor.setPlan(plan, opts.refitEditor ?? false);
    scene.setPlan(plan, opts.frameCamera ?? true);
    syncing = false;
  }
  renderControls();
}

editor.on("change", (next) => {
  plan = syncPlanFromCabinets(next);
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
        <label>가로 폭 (하부장 합)</label>
        <div class="field-row">
          <output style="text-align:left">${plan.counterWidth} mm</output>
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
              <label>리턴 길이 (하부장 합)</label>
              <div class="field-row">
                <output style="text-align:left">${plan.returnWidth} mm</output>
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
        <label for="counterThickness">상판 두께</label>
        <div class="field-row">
          <input id="counterThickness" type="range" min="12" max="60" step="1" value="${plan.counterThickness}" />
          <output>${plan.counterThickness}</output>
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
      <h2>하부장 디테일</h2>
      <div class="field">
        <label for="cabinetCount">하부장 개수</label>
        <div class="field-row">
          <input id="cabinetCount" type="range" min="1" max="8" step="1" value="${plan.cabinetWidths.length}" />
          <output>${plan.cabinetWidths.length}</output>
        </div>
      </div>
      ${plan.cabinetWidths
        .map(
          (w, i) => `<div class="field">
        <label for="cabWidth${i}">하부장 ${i + 1} 길이</label>
        <div class="field-row">
          <input id="cabWidth${i}" type="range" min="300" max="1200" step="10" value="${w}" data-cab-index="${i}" />
          <output>${w} mm</output>
        </div>
      </div>`,
        )
        .join("")}
      ${
        plan.template === "l-shape"
          ? `<div class="field" style="margin-top:0.75rem">
              <label for="returnCabinetCount">리턴 하부장 개수</label>
              <div class="field-row">
                <input id="returnCabinetCount" type="range" min="1" max="6" step="1" value="${Math.max(1, plan.returnCabinetWidths.length)}" />
                <output>${Math.max(1, plan.returnCabinetWidths.length)}</output>
              </div>
            </div>
            ${plan.returnCabinetWidths
              .map(
                (w, i) => `<div class="field">
              <label for="retCabWidth${i}">리턴 장 ${i + 1} 길이</label>
              <div class="field-row">
                <input id="retCabWidth${i}" type="range" min="300" max="1200" step="10" value="${w}" data-ret-cab-index="${i}" />
                <output>${w} mm</output>
              </div>
            </div>`,
              )
              .join("")}`
          : ""
      }
      <div class="field">
        <label for="counterOverhang">상판 앞 오버행</label>
        <div class="field-row">
          <input id="counterOverhang" type="range" min="0" max="80" step="1" value="${plan.counterOverhang}" />
          <output>${plan.counterOverhang} mm</output>
        </div>
      </div>
      <label class="toggle">
        <input type="checkbox" id="showToeKick" ${plan.showToeKick ? "checked" : ""} />
        걸레받이 표시
      </label>
      <div class="field" style="margin-top:0.55rem">
        <label for="toeKickHeight">걸레받이 높이</label>
        <div class="field-row">
          <input id="toeKickHeight" type="range" min="40" max="150" step="5" value="${plan.toeKickHeight}" ${plan.showToeKick ? "" : "disabled"} />
          <output>${plan.toeKickHeight} mm</output>
        </div>
      </div>
      <div class="field">
        <label for="toeKickDepth">걸레받이 깊이</label>
        <div class="field-row">
          <input id="toeKickDepth" type="range" min="20" max="100" step="5" value="${plan.toeKickDepth}" ${plan.showToeKick ? "" : "disabled"} />
          <output>${plan.toeKickDepth} mm</output>
        </div>
      </div>
      <div class="field">
        <label for="drawerRows">서랍 단수</label>
        <div class="field-row">
          <input id="drawerRows" type="range" min="0" max="2" step="1" value="${plan.drawerRows}" />
          <output>${plan.drawerRows}</output>
        </div>
      </div>
      <label class="toggle">
        <input type="checkbox" id="showHandles" ${plan.showHandles ? "checked" : ""} />
        손잡이 표시
      </label>
      <div class="field" style="margin-top:0.55rem">
        <label for="handleHeightPct">손잡이 높이</label>
        <div class="field-row">
          <input id="handleHeightPct" type="range" min="20" max="85" step="1" value="${plan.handleHeightPct}" ${plan.showHandles ? "" : "disabled"} />
          <output>${plan.handleHeightPct}%</output>
        </div>
      </div>
      <p class="hint">하부장마다 길이를 따로 조절하면 상판 가로(합계 ${sumWidths(plan.cabinetWidths)}mm)가 따라갑니다.</p>
    </div>

    <div class="section">
      <h2>벽 위치</h2>
      <label class="toggle">
        <input type="checkbox" id="showWall" ${plan.showWall ? "checked" : ""} />
        뒷벽 표시
      </label>
      <div class="field" style="margin-top:0.65rem">
        <label for="wallBackOffset">뒷벽 간격 (상판 뒤쪽 기준)</label>
        <div class="field-row">
          <input id="wallBackOffset" type="range" min="0" max="1500" step="10" value="${plan.wallBackOffset}" ${plan.showWall ? "" : "disabled"} />
          <output>${plan.wallBackOffset} mm</output>
        </div>
      </div>
      ${
        plan.template === "l-shape"
          ? `<div class="field">
              <label for="wallLeftOffset">옆벽 간격 (왼쪽 기준)</label>
              <div class="field-row">
                <input id="wallLeftOffset" type="range" min="0" max="1500" step="10" value="${plan.wallLeftOffset}" ${plan.showWall ? "" : "disabled"} />
                <output>${plan.wallLeftOffset} mm</output>
              </div>
            </div>`
          : ""
      }
      <p class="hint">값을 키우면 벽이 상판에서 더 멀어집니다. ㄱ자형은 뒷벽·옆벽을 따로 옮길 수 있습니다.</p>
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
    "counterDepth",
    "returnDepth",
    "cabinetHeight",
    "counterThickness",
    "backsplashHeight",
    "wallBackOffset",
    "wallLeftOffset",
    "counterOverhang",
    "toeKickHeight",
    "toeKickDepth",
    "drawerRows",
    "handleHeightPct",
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
      else if (id === "counterDepth") next.counterDepth = v;
      else if (id === "returnDepth") next.returnDepth = v;
      else if (id === "cabinetHeight") next.cabinetHeight = v;
      else if (id === "counterThickness") next.counterThickness = v;
      else if (id === "backsplashHeight") next.backsplashHeight = v;
      else if (id === "wallBackOffset") next.wallBackOffset = v;
      else if (id === "wallLeftOffset") next.wallLeftOffset = v;
      else if (id === "counterOverhang") next.counterOverhang = v;
      else if (id === "toeKickHeight") next.toeKickHeight = v;
      else if (id === "toeKickDepth") next.toeKickDepth = v;
      else if (id === "drawerRows") next.drawerRows = v;
      else if (id === "handleHeightPct") next.handleHeightPct = v;

      const out = el.parentElement?.querySelector("output");
      if (out) {
        if (id === "blueprintOpacity") out.textContent = `${Math.round(v * 100)}%`;
        else if (
          id === "wallBackOffset" ||
          id === "wallLeftOffset" ||
          id === "counterOverhang" ||
          id === "toeKickHeight" ||
          id === "toeKickDepth"
        ) {
          out.textContent = `${v} mm`;
        } else if (id === "handleHeightPct") out.textContent = `${v}%`;
        else out.textContent = String(v);
      }
      applyPlan(next);
    });
  }

  const cabinetCount = document.getElementById("cabinetCount") as HTMLInputElement | null;
  cabinetCount?.addEventListener("input", () => {
    const next = structuredClone(plan);
    next.cabinetWidths = resizeWidths(next.cabinetWidths, Number(cabinetCount.value), next.counterWidth || 1800);
    applyPlan(next, { refitEditor: true, frameCamera: true });
  });

  document.querySelectorAll<HTMLInputElement>("[data-cab-index]").forEach((el) => {
    el.addEventListener("input", () => {
      const idx = Number(el.dataset.cabIndex);
      const next = structuredClone(plan);
      next.cabinetWidths[idx] = Number(el.value);
      const out = el.parentElement?.querySelector("output");
      if (out) out.textContent = `${el.value} mm`;
      applyPlan(next, { refitEditor: true });
    });
  });

  const returnCabinetCount = document.getElementById("returnCabinetCount") as HTMLInputElement | null;
  returnCabinetCount?.addEventListener("input", () => {
    const next = structuredClone(plan);
    const fallback = Math.max(600, next.returnWidth - next.counterDepth);
    next.returnCabinetWidths = resizeWidths(
      next.returnCabinetWidths,
      Number(returnCabinetCount.value),
      fallback || 1000,
    );
    applyPlan(next, { refitEditor: true, frameCamera: true });
  });

  document.querySelectorAll<HTMLInputElement>("[data-ret-cab-index]").forEach((el) => {
    el.addEventListener("input", () => {
      const idx = Number(el.dataset.retCabIndex);
      const next = structuredClone(plan);
      next.returnCabinetWidths[idx] = Number(el.value);
      const out = el.parentElement?.querySelector("output");
      if (out) out.textContent = `${el.value} mm`;
      applyPlan(next, { refitEditor: true });
    });
  });

  const faucet = document.getElementById("faucet") as HTMLInputElement | null;
  faucet?.addEventListener("change", () => {
    const next = structuredClone(plan);
    next.faucet = faucet.checked;
    applyPlan(next);
  });

  const showWall = document.getElementById("showWall") as HTMLInputElement | null;
  showWall?.addEventListener("change", () => {
    const next = structuredClone(plan);
    next.showWall = showWall.checked;
    applyPlan(next);
  });

  const showToeKick = document.getElementById("showToeKick") as HTMLInputElement | null;
  showToeKick?.addEventListener("change", () => {
    const next = structuredClone(plan);
    next.showToeKick = showToeKick.checked;
    applyPlan(next);
  });

  const showHandles = document.getElementById("showHandles") as HTMLInputElement | null;
  showHandles?.addEventListener("change", () => {
    const next = structuredClone(plan);
    next.showHandles = showHandles.checked;
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
