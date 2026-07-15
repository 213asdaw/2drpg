export type TemplateId =
  | "straight-single"
  | "straight-double"
  | "l-shape"
  | "island";

export type CounterMaterial = "white-quartz" | "oak" | "black-granite" | "concrete";
export type CabinetMaterial = "white" | "walnut" | "sage";
export type SinkMaterial = "stainless" | "white-ceramic" | "matte-black";

export interface SinkBowl {
  /** mm from counter left edge to bowl left */
  offsetX: number;
  /** mm from counter front edge to bowl front */
  offsetZ: number;
  width: number;
  depth: number;
  bowlDepth: number;
}

export interface SinkPlan {
  template: TemplateId;
  /** Overall counter width (mm) — derived from sum of cabinetWidths */
  counterWidth: number;
  /** Overall counter depth (mm) */
  counterDepth: number;
  /** L-shape return leg length along Z (mm); derived when return cabinets set */
  returnWidth: number;
  /** L-shape return leg depth along X (mm); ignored for non-L */
  returnDepth: number;
  counterThickness: number;
  cabinetHeight: number;
  backsplashHeight: number;
  showWall: boolean;
  wallBackOffset: number;
  wallLeftOffset: number;
  bowls: SinkBowl[];
  faucet: boolean;
  counterOverhang: number;
  toeKickHeight: number;
  toeKickDepth: number;
  /** Main-run base cabinet widths (mm), one module each */
  cabinetWidths: number[];
  /** L-shape return-leg cabinet widths along Z (mm) */
  returnCabinetWidths: number[];
  drawerRows: number;
  showHandles: boolean;
  handleHeightPct: number;
  showToeKick: boolean;
  /** Wall / upper cabinets (2층 상부장) */
  showUpperCabinets: boolean;
  upperCabinetHeight: number;
  upperCabinetDepth: number;
  /** Clearance from counter top to upper cabinet bottom (mm) */
  upperGapFromCounter: number;
  upperCabinetWidths: number[];
  /** If true, upper module widths follow base cabinetWidths */
  matchUpperToLower: boolean;
  counterMaterial: CounterMaterial;
  cabinetMaterial: CabinetMaterial;
  sinkMaterial: SinkMaterial;
  showBlueprint: boolean;
  blueprintOpacity: number;
  /** Blueprint image scale relative to counter fit (1 = fit width) */
  blueprintScale: number;
  blueprintOffsetX: number;
  blueprintOffsetZ: number;
  blueprintInvert: boolean;
  snapToGrid: boolean;
  gridSizeMm: number;
}

/** Split total mm into `count` widths (10mm steps), last cell absorbs remainder. */
export function splitEvenWidths(total: number, count: number): number[] {
  const n = Math.max(1, Math.min(8, Math.round(count)));
  const safeTotal = Math.max(300 * n, Math.round(total));
  const base = Math.floor(safeTotal / n / 10) * 10;
  const widths = Array.from({ length: n }, () => Math.max(300, base));
  const used = widths.reduce((a, b) => a + b, 0);
  widths[n - 1] = Math.max(300, widths[n - 1] + (safeTotal - used));
  return widths;
}

export function sumWidths(widths: number[]): number {
  return widths.reduce((a, b) => a + b, 0);
}

export function resizeWidths(current: number[], count: number, fallbackTotal: number): number[] {
  const n = Math.max(1, Math.min(8, Math.round(count)));
  if (current.length === n) return [...current];
  const total = current.length > 0 ? sumWidths(current) : fallbackTotal;
  return splitEvenWidths(total, n);
}

export function syncPlanFromCabinets(plan: SinkPlan): SinkPlan {
  const next = structuredClone(plan);
  next.cabinetWidths = next.cabinetWidths.map((w) => Math.max(300, Math.round(w / 10) * 10));
  next.counterWidth = sumWidths(next.cabinetWidths);
  if (next.template === "l-shape") {
    next.returnCabinetWidths = next.returnCabinetWidths.map((w) =>
      Math.max(300, Math.round(w / 10) * 10),
    );
    if (next.returnCabinetWidths.length === 0) {
      next.returnCabinetWidths = splitEvenWidths(Math.max(600, next.returnWidth - next.counterDepth), 2);
    }
    next.returnWidth = next.counterDepth + sumWidths(next.returnCabinetWidths);
  } else {
    next.returnCabinetWidths = [];
  }

  if (next.matchUpperToLower || next.upperCabinetWidths.length === 0) {
    next.upperCabinetWidths = [...next.cabinetWidths];
  } else {
    next.upperCabinetWidths = next.upperCabinetWidths.map((w) =>
      Math.max(300, Math.round(w / 10) * 10),
    );
  }

  for (const b of next.bowls) {
    if (next.snapToGrid) {
      const g = Math.max(10, next.gridSizeMm);
      b.offsetX = Math.round(b.offsetX / g) * g;
      b.offsetZ = Math.round(b.offsetZ / g) * g;
      b.width = Math.round(b.width / g) * g;
      b.depth = Math.round(b.depth / g) * g;
    }
    b.offsetX = Math.min(b.offsetX, next.counterWidth - b.width - 20);
    b.offsetZ = Math.min(b.offsetZ, next.counterDepth - b.depth - 20);
    b.offsetX = Math.max(20, b.offsetX);
    b.offsetZ = Math.max(20, b.offsetZ);
  }
  return next;
}

export const TEMPLATES: Record<
  TemplateId,
  { label: string; description: string; plan: Partial<SinkPlan> }
> = {
  "straight-single": {
    label: "일자형 · 싱글",
    description: "가장 흔한 일자 싱크대, 볼 1개",
    plan: {
      counterWidth: 1800,
      counterDepth: 600,
      returnWidth: 0,
      returnDepth: 0,
      cabinetWidths: splitEvenWidths(1800, 4),
      returnCabinetWidths: [],
      bowls: [{ offsetX: 650, offsetZ: 80, width: 500, depth: 400, bowlDepth: 200 }],
    },
  },
  "straight-double": {
    label: "일자형 · 더블",
    description: "세척·헹굼을 나누는 더블 볼",
    plan: {
      counterWidth: 2000,
      counterDepth: 600,
      returnWidth: 0,
      returnDepth: 0,
      cabinetWidths: splitEvenWidths(2000, 5),
      returnCabinetWidths: [],
      bowls: [
        { offsetX: 400, offsetZ: 80, width: 420, depth: 400, bowlDepth: 200 },
        { offsetX: 860, offsetZ: 80, width: 420, depth: 400, bowlDepth: 180 },
      ],
    },
  },
  "l-shape": {
    label: "ㄱ자형",
    description: "코너를 활용한 L형 조리대",
    plan: {
      counterWidth: 2200,
      counterDepth: 600,
      returnWidth: 1600,
      returnDepth: 600,
      cabinetWidths: splitEvenWidths(2200, 5),
      returnCabinetWidths: splitEvenWidths(1000, 2),
      bowls: [{ offsetX: 700, offsetZ: 90, width: 520, depth: 400, bowlDepth: 210 }],
    },
  },
  island: {
    label: "아일랜드",
    description: "중앙 독립형 아일랜드 싱크",
    plan: {
      counterWidth: 1600,
      counterDepth: 900,
      returnWidth: 0,
      returnDepth: 0,
      cabinetWidths: splitEvenWidths(1600, 4),
      returnCabinetWidths: [],
      bowls: [{ offsetX: 550, offsetZ: 220, width: 500, depth: 400, bowlDepth: 200 }],
    },
  },
};

export function createDefaultPlan(template: TemplateId = "straight-single"): SinkPlan {
  const base = TEMPLATES[template].plan;
  const plan: SinkPlan = {
    template,
    counterWidth: 1800,
    counterDepth: 600,
    returnWidth: 0,
    returnDepth: 0,
    counterThickness: 30,
    cabinetHeight: 720,
    backsplashHeight: 120,
    showWall: true,
    wallBackOffset: 20,
    wallLeftOffset: 20,
    bowls: [{ offsetX: 650, offsetZ: 80, width: 500, depth: 400, bowlDepth: 200 }],
    faucet: true,
    counterOverhang: 35,
    toeKickHeight: 80,
    toeKickDepth: 50,
    cabinetWidths: splitEvenWidths(1800, 4),
    returnCabinetWidths: [],
    drawerRows: 1,
    showHandles: true,
    handleHeightPct: 55,
    showToeKick: true,
    showUpperCabinets: true,
    upperCabinetHeight: 700,
    upperCabinetDepth: 350,
    upperGapFromCounter: 550,
    upperCabinetWidths: splitEvenWidths(1800, 4),
    matchUpperToLower: true,
    counterMaterial: "white-quartz",
    cabinetMaterial: "walnut",
    sinkMaterial: "stainless",
    showBlueprint: true,
    blueprintOpacity: 0.4,
    blueprintScale: 1,
    blueprintOffsetX: 0,
    blueprintOffsetZ: 0,
    blueprintInvert: false,
    snapToGrid: true,
    gridSizeMm: 50,
    ...base,
  };
  return syncPlanFromCabinets(plan);
}
