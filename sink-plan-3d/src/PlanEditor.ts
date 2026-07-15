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
    const padX = 56;
    const padTop = 48;
    const padBottom = 64; // legend / hints
    const w = this.canvas.clientWidth || 400;
    const h = this.canvas.clientHeight || 400;
    const planW = Math.max(1, this.plan.counterWidth);
    // Include wall-offset gutter so guides stay visible without shoving the plan down
    const wallPad =
      (this.plan.showWall ? Math.max(this.plan.wallBackOffset, 0) : 0) + 40;
    const planD =
      this.plan.template === "l-shape"
        ? Math.max(this.plan.counterDepth, this.plan.returnWidth) + wallPad
        : this.plan.counterDepth + wallPad;
    const usableW = Math.max(120, w - padX * 2);
    const usableH = Math.max(120, h - padTop - padBottom);
    this.scale = Math.min(usableW / planW, usableH / planD);
    // Keep the drawing anchored near the top so template switches don't drop it down
    this.offsetX = (w - planW * this.scale) / 2;
    this.offsetY = padTop;
  }

  /** Re-fit after layout settles (sidebar height changes). */
  refitSoon() {
    requestAnimationFrame(() => {
      this.fitView();
      this.draw();
    });
  }

  private onResize = () => {
    const dpr = Math.min(window.devicePixelRatio, 2);
    const w = this.canvas.clientWidth;
    const h = this.canvas.clientHeight;
    this.canvas.width = Math.floor(w * dpr);
    this.canvas.height = Math.floor(h * dpr);
    const ctx = this.canvas.getContext("2d");
    if (ctx) ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.fitView();
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
    if (this.plan.snapToGrid) {
      const g = Math.max(10, this.plan.gridSizeMm);
      b.offsetX = Math.round(b.offsetX / g) * g;
      b.offsetZ = Math.round(b.offsetZ / g) * g;
    }
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

    // Grid — major every 500mm, minor every gridSize
    const minor = Math.max(10, this.plan.gridSizeMm) * this.scale;
    const major = 500 * this.scale;
    ctx.strokeStyle = "rgba(40, 70, 90, 0.07)";
    ctx.lineWidth = 1;
    const startX = ((this.offsetX % minor) + minor) % minor;
    const startY = ((this.offsetY % minor) + minor) % minor;
    for (let x = startX; x < w; x += minor) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, h);
      ctx.stroke();
    }
    for (let y = startY; y < h; y += minor) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(w, y);
      ctx.stroke();
    }
    ctx.strokeStyle = "rgba(40, 70, 90, 0.16)";
    const startMajX = ((this.offsetX % major) + major) % major;
    const startMajY = ((this.offsetY % major) + major) % major;
    for (let x = startMajX; x < w; x += major) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, h);
      ctx.stroke();
    }
    for (let y = startMajY; y < h; y += major) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(w, y);
      ctx.stroke();
    }

    // Blueprint underlay (scale + offset + optional invert)
    if (this.plan.showBlueprint && this.blueprintImg) {
      ctx.save();
      const img = this.blueprintImg;
      const base = this.mmToPx(this.plan.blueprintOffsetX, this.plan.blueprintOffsetZ);
      const dw = this.plan.counterWidth * this.scale * this.plan.blueprintScale;
      const planH =
        this.plan.template === "l-shape" ? this.plan.returnWidth : this.plan.counterDepth;
      const dh = Math.max(planH, this.plan.counterDepth) * this.scale * this.plan.blueprintScale;
      ctx.globalAlpha = this.plan.blueprintOpacity;
      if (this.plan.blueprintInvert) {
        ctx.filter = "invert(1) contrast(1.15)";
      }
      ctx.drawImage(img, base.x, base.y, dw, dh);
      ctx.restore();

      // Fit frame so user sees how the drawing maps
      ctx.save();
      ctx.strokeStyle = "rgba(30, 120, 140, 0.7)";
      ctx.setLineDash([6, 4]);
      ctx.lineWidth = 1.5;
      ctx.strokeRect(base.x, base.y, dw, dh);
      ctx.setLineDash([]);
      ctx.fillStyle = "rgba(30, 100, 120, 0.85)";
      ctx.font = "600 11px Lexend, sans-serif";
      ctx.fillText(
        `도면 맞춤 ×${this.plan.blueprintScale.toFixed(2)}  이동(${this.plan.blueprintOffsetX},${this.plan.blueprintOffsetZ})mm`,
        base.x + 4,
        Math.max(14, base.y - 6),
      );
      ctx.restore();
    }

    // Counter fill
    this.drawCounterPath(ctx);
    ctx.fillStyle = "rgba(250, 248, 242, 0.72)";
    ctx.fill();
    ctx.strokeStyle = "#1f3a4a";
    ctx.lineWidth = 2.5;
    ctx.stroke();

    // Lower cabinet module guides
    this.drawCabinetModules(ctx);

    // Upper cabinet footprint (2층)
    this.drawUpperFootprint(ctx);

    // Dimension labels
    this.drawDims(ctx);

    // Wall guides
    this.drawWalls(ctx);

    // Bowls
    this.plan.bowls.forEach((bowl, i) => this.drawBowl(ctx, bowl, i === this.selectedBowl));

    // Legend
    this.drawLegend(ctx, w, h);
  }

  private drawCabinetModules(ctx: CanvasRenderingContext2D) {
    let x = 0;
    ctx.save();
    ctx.font = "600 11px Lexend, sans-serif";
    this.plan.cabinetWidths.forEach((wMm, i) => {
      const a = this.mmToPx(x, 0);
      const b = this.mmToPx(x + wMm, this.plan.counterDepth);
      ctx.strokeStyle = "rgba(90, 130, 90, 0.55)";
      ctx.setLineDash([3, 3]);
      ctx.lineWidth = 1.2;
      ctx.strokeRect(a.x, a.y, b.x - a.x, b.y - a.y);
      ctx.setLineDash([]);
      ctx.fillStyle = "rgba(50, 100, 60, 0.9)";
      ctx.fillText(`하부${i + 1} ${wMm}`, a.x + 6, a.y + 14);
      x += wMm;
    });
    ctx.restore();
  }

  private drawUpperFootprint(ctx: CanvasRenderingContext2D) {
    if (!this.plan.showUpperCabinets) return;
    const depths = this.plan.upperCabinetDepth;
    const widths =
      this.plan.matchUpperToLower || this.plan.upperCabinetWidths.length === 0
        ? this.plan.cabinetWidths
        : this.plan.upperCabinetWidths;
    let x = 0;
    ctx.save();
    ctx.font = "600 11px Lexend, sans-serif";
    widths.forEach((wMm, i) => {
      const z0 = this.plan.counterDepth - depths;
      const a = this.mmToPx(x, z0);
      const bw = wMm * this.scale;
      const bh = depths * this.scale;
      ctx.fillStyle = "rgba(70, 110, 180, 0.18)";
      ctx.strokeStyle = "rgba(50, 90, 170, 0.75)";
      ctx.setLineDash([5, 3]);
      ctx.lineWidth = 1.5;
      ctx.fillRect(a.x, a.y, bw, bh);
      ctx.strokeRect(a.x, a.y, bw, bh);
      ctx.setLineDash([]);
      ctx.fillStyle = "rgba(40, 70, 150, 0.95)";
      ctx.fillText(`상부${i + 1}`, a.x + 6, a.y + 14);
      x += wMm;
    });
    ctx.restore();
  }

  private drawLegend(ctx: CanvasRenderingContext2D, w: number, h: number) {
    const lines = [
      "상단뷰 · mm",
      "초록 점선=하부장 · 파란 영역=2층 상부장",
      "볼 드래그 · 휠 줌 · 격자 스냅",
    ];
    ctx.fillStyle = "rgba(15, 35, 45, 0.78)";
    ctx.fillRect(8, h - 52, Math.min(360, w - 16), 44);
    ctx.fillStyle = "#dce8ec";
    ctx.font = "11px Lexend, sans-serif";
    lines.forEach((t, i) => ctx.fillText(t, 14, h - 36 + i * 13));
  }

  private drawWalls(ctx: CanvasRenderingContext2D) {
    if (!this.plan.showWall) return;
    const p = this.plan;
    ctx.save();
    ctx.strokeStyle = "rgba(180, 90, 60, 0.85)";
    ctx.fillStyle = "rgba(180, 90, 60, 0.75)";
    ctx.lineWidth = 3;
    ctx.setLineDash([8, 5]);
    ctx.font = "600 11px Lexend, sans-serif";

    if (p.template === "l-shape" && p.returnWidth > 0) {
      const backZ = p.returnWidth + p.wallBackOffset;
      const leftX = -p.wallLeftOffset;
      const a = this.mmToPx(leftX - 40, backZ);
      const b = this.mmToPx(Math.max(p.returnDepth, p.counterWidth * 0.55) + 80, backZ);
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.fillText(`뒷벽 +${p.wallBackOffset}mm`, b.x - 90, b.y - 6);

      ctx.setLineDash([8, 5]);
      const c = this.mmToPx(leftX, -40);
      const d = this.mmToPx(leftX, p.returnWidth + p.wallBackOffset + 40);
      ctx.beginPath();
      ctx.moveTo(c.x, c.y);
      ctx.lineTo(d.x, d.y);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.fillText(`옆벽 +${p.wallLeftOffset}mm`, c.x + 6, c.y + 14);
    } else {
      const backZ = p.counterDepth + p.wallBackOffset;
      const a = this.mmToPx(-40, backZ);
      const b = this.mmToPx(p.counterWidth + 40, backZ);
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.fillText(`뒷벽 +${p.wallBackOffset}mm`, (a.x + b.x) / 2 - 40, a.y - 6);
    }
    ctx.restore();
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
