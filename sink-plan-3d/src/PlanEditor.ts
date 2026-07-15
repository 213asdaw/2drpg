import type { SinkBowl, SinkPlan } from "./types";

export type PlanEditorEvents = {
  change: (plan: SinkPlan) => void;
  selectBowl: (index: number) => void;
};

/**
 * Top-down 2D blueprint editor. Units: millimetres.
 * Drag bowls to reposition; scroll to zoom.
 */
export class PlanEditor {
  private canvas: HTMLCanvasElement;
  private plan: SinkPlan;
  private scale = 0.18; // px per mm
  private offsetX = 40;
  private offsetY = 40;
  private dragging: { bowl: number; dx: number; dy: number } | null = null;
  private selectedBowl = 0;
  private blueprintImg: HTMLImageElement | null = null;
  private listeners: Partial<{ [K in keyof PlanEditorEvents]: PlanEditorEvents[K][] }> = {};

  constructor(canvas: HTMLCanvasElement, plan: SinkPlan) {
    this.canvas = canvas;
    this.plan = structuredClone(plan);
    this.fitView();
    canvas.addEventListener("pointerdown", this.onPointerDown);
    canvas.addEventListener("pointermove", this.onPointerMove);
    canvas.addEventListener("pointerup", this.onPointerUp);
    canvas.addEventListener("pointerleave", this.onPointerUp);
    canvas.addEventListener("wheel", this.onWheel, { passive: false });
    window.addEventListener("resize", this.onResize);
    this.onResize();
    this.draw();
  }

  on<K extends keyof PlanEditorEvents>(event: K, fn: PlanEditorEvents[K]) {
    (this.listeners[event] ??= []).push(fn as never);
  }

  private emit<K extends keyof PlanEditorEvents>(event: K, ...args: Parameters<PlanEditorEvents[K]>) {
    for (const fn of this.listeners[event] ?? []) {
      (fn as (...a: unknown[]) => void)(...args);
    }
  }

  getPlan(): SinkPlan {
    return structuredClone(this.plan);
  }

  setPlan(plan: SinkPlan, refit = false) {
    this.plan = structuredClone(plan);
    this.selectedBowl = Math.min(this.selectedBowl, Math.max(0, plan.bowls.length - 1));
    if (refit) this.fitView();
    this.draw();
  }

  setBlueprintImage(dataUrl: string | null) {
    if (!dataUrl) {
      this.blueprintImg = null;
      this.draw();
      return;
    }
    const img = new Image();
    img.onload = () => {
      this.blueprintImg = img;
      this.draw();
    };
    img.src = dataUrl;
  }

  private fitView() {
    const pad = 80;
    const w = this.canvas.clientWidth || 400;
    const h = this.canvas.clientHeight || 400;
    const planW = this.plan.counterWidth;
    const planD =
      this.plan.template === "l-shape"
        ? Math.max(this.plan.counterDepth, this.plan.returnWidth)
        : this.plan.counterDepth;
    this.scale = Math.min((w - pad * 2) / planW, (h - pad * 2) / planD);
    this.offsetX = (w - planW * this.scale) / 2;
    this.offsetY = (h - planD * this.scale) / 2;
  }

  private onResize = () => {
    const dpr = Math.min(window.devicePixelRatio, 2);
    const w = this.canvas.clientWidth;
    const h = this.canvas.clientHeight;
    this.canvas.width = Math.floor(w * dpr);
    this.canvas.height = Math.floor(h * dpr);
    const ctx = this.canvas.getContext("2d");
    if (ctx) ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.draw();
  };

  private mmToPx(x: number, z: number): { x: number; y: number } {
    return {
      x: this.offsetX + x * this.scale,
      y: this.offsetY + z * this.scale,
    };
  }

  private pxToMm(px: number, py: number): { x: number; z: number } {
    return {
      x: (px - this.offsetX) / this.scale,
      z: (py - this.offsetY) / this.scale,
    };
  }

  private hitBowl(px: number, py: number): number {
    const { x, z } = this.pxToMm(px, py);
    for (let i = this.plan.bowls.length - 1; i >= 0; i--) {
      const b = this.plan.bowls[i];
      if (x >= b.offsetX && x <= b.offsetX + b.width && z >= b.offsetZ && z <= b.offsetZ + b.depth) {
        return i;
      }
    }
    return -1;
  }

  private onPointerDown = (e: PointerEvent) => {
    const rect = this.canvas.getBoundingClientRect();
    const px = e.clientX - rect.left;
    const py = e.clientY - rect.top;
    const idx = this.hitBowl(px, py);
    if (idx >= 0) {
      const b = this.plan.bowls[idx];
      const mm = this.pxToMm(px, py);
      this.dragging = { bowl: idx, dx: mm.x - b.offsetX, dy: mm.z - b.offsetZ };
      this.selectedBowl = idx;
      this.emit("selectBowl", idx);
      this.canvas.setPointerCapture(e.pointerId);
      this.draw();
    }
  };

  private onPointerMove = (e: PointerEvent) => {
    if (!this.dragging) return;
    const rect = this.canvas.getBoundingClientRect();
    const px = e.clientX - rect.left;
    const py = e.clientY - rect.top;
    const mm = this.pxToMm(px, py);
    const b = this.plan.bowls[this.dragging.bowl];
    let nx = mm.x - this.dragging.dx;
    let nz = mm.z - this.dragging.dy;
    nx = Math.max(20, Math.min(this.plan.counterWidth - b.width - 20, nx));
    nz = Math.max(20, Math.min(this.plan.counterDepth - b.depth - 20, nz));
    b.offsetX = Math.round(nx);
    b.offsetZ = Math.round(nz);
    this.draw();
    this.emit("change", this.getPlan());
  };

  private onPointerUp = () => {
    this.dragging = null;
  };

  private onWheel = (e: WheelEvent) => {
    e.preventDefault();
    const rect = this.canvas.getBoundingClientRect();
    const px = e.clientX - rect.left;
    const py = e.clientY - rect.top;
    const before = this.pxToMm(px, py);
    const factor = e.deltaY > 0 ? 0.9 : 1.1;
    this.scale = Math.min(0.6, Math.max(0.04, this.scale * factor));
    const after = this.mmToPx(before.x, before.z);
    this.offsetX += px - after.x;
    this.offsetY += py - after.y;
    this.draw();
  };

  private drawCounterPath(ctx: CanvasRenderingContext2D) {
    const p = this.plan;
    ctx.beginPath();
    if (p.template === "l-shape" && p.returnWidth > 0) {
      const a = this.mmToPx(0, 0);
      const b = this.mmToPx(p.counterWidth, 0);
      const c = this.mmToPx(p.counterWidth, p.counterDepth);
      const d = this.mmToPx(p.returnDepth, p.counterDepth);
      const e = this.mmToPx(p.returnDepth, p.returnWidth);
      const f = this.mmToPx(0, p.returnWidth);
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.lineTo(c.x, c.y);
      ctx.lineTo(d.x, d.y);
      ctx.lineTo(e.x, e.y);
      ctx.lineTo(f.x, f.y);
      ctx.closePath();
    } else {
      const a = this.mmToPx(0, 0);
      const w = p.counterWidth * this.scale;
      const h = p.counterDepth * this.scale;
      ctx.roundRect(a.x, a.y, w, h, 4);
    }
  }

  draw() {
    const ctx = this.canvas.getContext("2d");
    if (!ctx) return;
    const w = this.canvas.clientWidth;
    const h = this.canvas.clientHeight;
    ctx.clearRect(0, 0, w, h);

    // Paper background
    ctx.fillStyle = "#e8edf0";
    ctx.fillRect(0, 0, w, h);

    // Grid
    ctx.strokeStyle = "rgba(40, 70, 90, 0.08)";
    ctx.lineWidth = 1;
    const step = 100 * this.scale; // 100mm
    const startX = this.offsetX % step;
    const startY = this.offsetY % step;
    for (let x = startX; x < w; x += step) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, h);
      ctx.stroke();
    }
    for (let y = startY; y < h; y += step) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(w, y);
      ctx.stroke();
    }

    // Blueprint underlay
    if (this.plan.showBlueprint && this.blueprintImg) {
      ctx.save();
      ctx.globalAlpha = this.plan.blueprintOpacity;
      const img = this.blueprintImg;
      const dest = this.mmToPx(0, 0);
      const dw = this.plan.counterWidth * this.scale;
      const dh =
        (this.plan.template === "l-shape" ? this.plan.returnWidth : this.plan.counterDepth) *
        this.scale;
      ctx.drawImage(img, dest.x, dest.y, dw, Math.max(dh, this.plan.counterDepth * this.scale));
      ctx.restore();
    }

    // Counter fill
    this.drawCounterPath(ctx);
    ctx.fillStyle = "rgba(250, 248, 242, 0.92)";
    ctx.fill();
    ctx.strokeStyle = "#1f3a4a";
    ctx.lineWidth = 2.5;
    ctx.stroke();

    // Dimension labels
    this.drawDims(ctx);

    // Bowls
    this.plan.bowls.forEach((bowl, i) => this.drawBowl(ctx, bowl, i === this.selectedBowl));

    // Legend
    ctx.fillStyle = "rgba(31, 58, 74, 0.7)";
    ctx.font = "12px Lexend, sans-serif";
    ctx.fillText("상단뷰 · mm · 볼을 드래그해 위치 조정 · 휠로 줌", 12, h - 12);
  }

  private drawBowl(ctx: CanvasRenderingContext2D, bowl: SinkBowl, selected: boolean) {
    const p = this.mmToPx(bowl.offsetX, bowl.offsetZ);
    const bw = bowl.width * this.scale;
    const bh = bowl.depth * this.scale;

    ctx.beginPath();
    ctx.roundRect(p.x, p.y, bw, bh, 6);
    ctx.fillStyle = selected ? "rgba(46, 120, 140, 0.25)" : "rgba(80, 100, 110, 0.18)";
    ctx.fill();
    ctx.strokeStyle = selected ? "#1a6b7c" : "#3a5560";
    ctx.lineWidth = selected ? 2.5 : 1.5;
    ctx.setLineDash(selected ? [] : [4, 3]);
    ctx.stroke();
    ctx.setLineDash([]);

    // Inner bowl hint
    ctx.beginPath();
    ctx.roundRect(p.x + 8, p.y + 8, bw - 16, bh - 16, 4);
    ctx.strokeStyle = "rgba(30, 60, 70, 0.35)";
    ctx.lineWidth = 1;
    ctx.stroke();

    ctx.fillStyle = "#1f3a4a";
    ctx.font = "600 11px Lexend, sans-serif";
    ctx.fillText(`${bowl.width}×${bowl.depth}`, p.x + 10, p.y + 18);
  }

  private drawDims(ctx: CanvasRenderingContext2D) {
    const p = this.plan;
    ctx.fillStyle = "#2a5566";
    ctx.font = "500 12px Lexend, sans-serif";
    const top = this.mmToPx(p.counterWidth / 2, -28);
    ctx.textAlign = "center";
    ctx.fillText(`${p.counterWidth} mm`, top.x, top.y);
    const side = this.mmToPx(-36, p.counterDepth / 2);
    ctx.save();
    ctx.translate(side.x, side.y);
    ctx.rotate(-Math.PI / 2);
    ctx.fillText(`${p.counterDepth} mm`, 0, 0);
    ctx.restore();
    ctx.textAlign = "left";
  }

  dispose() {
    this.canvas.removeEventListener("pointerdown", this.onPointerDown);
    this.canvas.removeEventListener("pointermove", this.onPointerMove);
    this.canvas.removeEventListener("pointerup", this.onPointerUp);
    this.canvas.removeEventListener("pointerleave", this.onPointerUp);
    this.canvas.removeEventListener("wheel", this.onWheel);
    window.removeEventListener("resize", this.onResize);
  }
}
