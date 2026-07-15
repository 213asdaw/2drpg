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
  /** Overall counter width (mm) — for L-shape, main run width */
  counterWidth: number;
  /** Overall counter depth (mm) */
  counterDepth: number;
  /** L-shape return leg width along Z (mm); ignored for non-L */
  returnWidth: number;
  /** L-shape return leg depth along X (mm); ignored for non-L */
  returnDepth: number;
  counterThickness: number;
  cabinetHeight: number;
  backsplashHeight: number;
  bowls: SinkBowl[];
  faucet: boolean;
  counterMaterial: CounterMaterial;
  cabinetMaterial: CabinetMaterial;
  sinkMaterial: SinkMaterial;
  showBlueprint: boolean;
  blueprintOpacity: number;
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
      bowls: [{ offsetX: 550, offsetZ: 220, width: 500, depth: 400, bowlDepth: 200 }],
    },
  },
};

export function createDefaultPlan(template: TemplateId = "straight-single"): SinkPlan {
  const base = TEMPLATES[template].plan;
  return {
    template,
    counterWidth: 1800,
    counterDepth: 600,
    returnWidth: 0,
    returnDepth: 0,
    counterThickness: 30,
    cabinetHeight: 720,
    backsplashHeight: 120,
    bowls: [{ offsetX: 650, offsetZ: 80, width: 500, depth: 400, bowlDepth: 200 }],
    faucet: true,
    counterMaterial: "white-quartz",
    cabinetMaterial: "walnut",
    sinkMaterial: "stainless",
    showBlueprint: true,
    blueprintOpacity: 0.35,
    ...base,
  };
}
