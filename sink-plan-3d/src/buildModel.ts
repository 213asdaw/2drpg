import * as THREE from "three";
import type { SinkBowl, SinkPlan } from "./types";
import {
  makeCabinetMaterial,
  makeCounterMaterial,
  makeSinkMaterial,
} from "./materials";

/** Convert mm to Three.js units (1 unit = 1 meter) */
export const MM = 0.001;

function roundedRectShape(
  x: number,
  y: number,
  w: number,
  h: number,
  r: number,
): THREE.Shape {
  const shape = new THREE.Shape();
  const rr = Math.min(r, w / 2, h / 2);
  shape.moveTo(x + rr, y);
  shape.lineTo(x + w - rr, y);
  shape.quadraticCurveTo(x + w, y, x + w, y + rr);
  shape.lineTo(x + w, y + h - rr);
  shape.quadraticCurveTo(x + w, y + h, x + w - rr, y + h);
  shape.lineTo(x + rr, y + h);
  shape.quadraticCurveTo(x, y + h, x, y + h - rr);
  shape.lineTo(x, y + rr);
  shape.quadraticCurveTo(x, y, x + rr, y);
  return shape;
}

function counterTopShape(plan: SinkPlan): THREE.Shape {
  const W = plan.counterWidth * MM;
  const D = plan.counterDepth * MM;

  let shape: THREE.Shape;
  if (plan.template === "l-shape" && plan.returnWidth > 0 && plan.returnDepth > 0) {
    const RW = plan.returnWidth * MM;
    const RD = plan.returnDepth * MM;
    // L: main run along +X, return leg along +Z from left
    shape = new THREE.Shape();
    shape.moveTo(0, 0);
    shape.lineTo(W, 0);
    shape.lineTo(W, D);
    shape.lineTo(RD, D);
    shape.lineTo(RD, RW);
    shape.lineTo(0, RW);
    shape.closePath();
  } else {
    shape = roundedRectShape(0, 0, W, D, 0.01);
  }

  for (const bowl of plan.bowls) {
    const hx = bowl.offsetX * MM;
    const hy = bowl.offsetZ * MM;
    const hw = bowl.width * MM;
    const hd = bowl.depth * MM;
    const hole = roundedRectShape(hx, hy, hw, hd, 0.025);
    shape.holes.push(hole);
  }

  return shape;
}

function createSinkBasin(bowl: SinkBowl, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const w = bowl.width * MM;
  const d = bowl.depth * MM;
  const depth = bowl.bowlDepth * MM;
  const wall = 0.012;
  const lip = 0.008;

  // Outer lip ring on counter underside visually as basin walls
  const wallMat = material;

  // Bottom
  const bottom = new THREE.Mesh(
    new THREE.BoxGeometry(w - wall * 2, 0.008, d - wall * 2),
    wallMat,
  );
  bottom.position.set(w / 2, -depth, d / 2);
  bottom.castShadow = true;
  bottom.receiveShadow = true;
  group.add(bottom);

  // Four walls
  const front = new THREE.Mesh(
    new THREE.BoxGeometry(w, depth, wall),
    wallMat,
  );
  front.position.set(w / 2, -depth / 2, wall / 2);
  front.castShadow = true;
  group.add(front);

  const back = new THREE.Mesh(
    new THREE.BoxGeometry(w, depth, wall),
    wallMat,
  );
  back.position.set(w / 2, -depth / 2, d - wall / 2);
  back.castShadow = true;
  group.add(back);

  const left = new THREE.Mesh(
    new THREE.BoxGeometry(wall, depth, d - wall * 2),
    wallMat,
  );
  left.position.set(wall / 2, -depth / 2, d / 2);
  left.castShadow = true;
  group.add(left);

  const right = new THREE.Mesh(
    new THREE.BoxGeometry(wall, depth, d - wall * 2),
    wallMat,
  );
  right.position.set(w - wall / 2, -depth / 2, d / 2);
  right.castShadow = true;
  group.add(right);

  // Outer rim flush with counter hole
  const rim = new THREE.Mesh(
    new THREE.BoxGeometry(w + lip * 2, 0.006, d + lip * 2),
    wallMat,
  );
  rim.position.set(w / 2, 0.002, d / 2);
  group.add(rim);

  // Drain
  const drainMat = new THREE.MeshStandardMaterial({
    color: "#555555",
    metalness: 0.9,
    roughness: 0.25,
  });
  const drain = new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.025, 0.01, 24), drainMat);
  drain.position.set(w / 2, -depth + 0.006, d / 2);
  group.add(drain);

  group.position.set(bowl.offsetX * MM, 0, bowl.offsetZ * MM);
  return group;
}

function createFaucet(bowl: SinkBowl, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const baseY = 0.01;
  const cx = bowl.offsetX * MM + bowl.width * MM * 0.5;
  const cz = bowl.offsetZ * MM + bowl.depth * MM + 0.045;

  const base = new THREE.Mesh(new THREE.CylinderGeometry(0.028, 0.032, 0.02, 24), material);
  base.position.set(cx, baseY + 0.01, cz);
  group.add(base);

  const column = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.014, 0.18, 20), material);
  column.position.set(cx, baseY + 0.11, cz);
  group.add(column);

  const spout = new THREE.Mesh(new THREE.CylinderGeometry(0.01, 0.01, 0.14, 16), material);
  spout.rotation.z = Math.PI / 2;
  spout.position.set(cx, baseY + 0.2, cz - 0.05);
  group.add(spout);

  const tip = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.01, 0.03, 16), material);
  tip.position.set(cx, baseY + 0.185, cz - 0.12);
  group.add(tip);

  return group;
}

function createCabinets(plan: SinkPlan, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const H = plan.cabinetHeight * MM;
  const T = plan.counterThickness * MM;
  const inset = 0.04;

  const addCabinetBox = (x: number, z: number, w: number, d: number) => {
    const body = new THREE.Mesh(
      new THREE.BoxGeometry(w - inset * 0.5, H, d - inset),
      material,
    );
    body.position.set(x + w / 2, H / 2, z + d / 2);
    body.castShadow = true;
    body.receiveShadow = true;
    group.add(body);

    // Toe kick notch visual
    const kickMat = new THREE.MeshStandardMaterial({ color: "#1c1c1c", roughness: 0.9 });
    const kick = new THREE.Mesh(new THREE.BoxGeometry(w - inset, 0.08, 0.04), kickMat);
    kick.position.set(x + w / 2, 0.04, z + 0.02);
    group.add(kick);

    // Simple door seams
    const seamMat = new THREE.MeshStandardMaterial({ color: "#000000", roughness: 1, opacity: 0.15, transparent: true });
    const doors = Math.max(1, Math.round(w / 0.45));
    for (let i = 1; i < doors; i++) {
      const seam = new THREE.Mesh(new THREE.BoxGeometry(0.004, H * 0.7, 0.01), seamMat);
      seam.position.set(x + (w * i) / doors, H * 0.55, z + 0.02);
      group.add(seam);
    }

    // Counter sits on top — handled separately; return for reference
    void T;
  };

  if (plan.template === "l-shape" && plan.returnWidth > 0) {
    const W = plan.counterWidth * MM;
    const D = plan.counterDepth * MM;
    const RW = plan.returnWidth * MM;
    const RD = plan.returnDepth * MM;
    addCabinetBox(0, 0, W, D);
    addCabinetBox(0, D, RD, RW - D);
  } else {
    addCabinetBox(0, 0, plan.counterWidth * MM, plan.counterDepth * MM);
  }

  return group;
}

function createBacksplash(plan: SinkPlan, material: THREE.Material): THREE.Mesh | null {
  if (plan.backsplashHeight <= 0) return null;
  const H = plan.backsplashHeight * MM;
  const T = 0.012;
  const y = plan.cabinetHeight * MM + plan.counterThickness * MM + H / 2;

  if (plan.template === "l-shape" && plan.returnWidth > 0) {
    // Combined via group caller; single main run backsplash for simplicity
  }

  const mesh = new THREE.Mesh(
    new THREE.BoxGeometry(plan.counterWidth * MM, H, T),
    material,
  );
  mesh.position.set(
    (plan.counterWidth * MM) / 2,
    y,
    plan.counterDepth * MM - T / 2,
  );
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  return mesh;
}

function createFloor(plan: SinkPlan): THREE.Mesh {
  const size = Math.max(plan.counterWidth, plan.returnWidth || 0, plan.counterDepth) * MM * 2.5 + 1.5;
  const geo = new THREE.PlaneGeometry(size, size);
  const mat = new THREE.MeshStandardMaterial({
    color: "#d8d2c8",
    roughness: 0.9,
    metalness: 0,
  });
  const floor = new THREE.Mesh(geo, mat);
  floor.rotation.x = -Math.PI / 2;
  floor.position.set(
    (plan.counterWidth * MM) / 2,
    0,
    (plan.counterDepth * MM) / 2,
  );
  floor.receiveShadow = true;
  return floor;
}

function createWall(plan: SinkPlan): THREE.Group {
  const group = new THREE.Group();
  const mat = new THREE.MeshStandardMaterial({
    color: "#ebe7df",
    roughness: 0.95,
  });
  const wallH = 2.4;
  const wallT = 0.08;

  const back = new THREE.Mesh(
    new THREE.BoxGeometry(plan.counterWidth * MM + 0.8, wallH, wallT),
    mat,
  );
  back.position.set(
    (plan.counterWidth * MM) / 2,
    wallH / 2,
    plan.counterDepth * MM + wallT / 2 + 0.02,
  );
  back.receiveShadow = true;
  group.add(back);

  if (plan.template === "l-shape") {
    const side = new THREE.Mesh(
      new THREE.BoxGeometry(wallT, wallH, plan.returnWidth * MM + 0.4),
      mat,
    );
    side.position.set(
      -wallT / 2 - 0.02,
      wallH / 2,
      (plan.returnWidth * MM) / 2,
    );
    side.receiveShadow = true;
    group.add(side);
  }

  return group;
}

/**
 * Build a complete sink-unit scene graph from the 2D plan (mm).
 */
export function buildSinkModel(plan: SinkPlan): THREE.Group {
  const root = new THREE.Group();
  root.name = "SinkModel";

  const counterMat = makeCounterMaterial(plan.counterMaterial);
  const cabinetMat = makeCabinetMaterial(plan.cabinetMaterial);
  const sinkMat = makeSinkMaterial(plan.sinkMaterial);
  const faucetMat = new THREE.MeshStandardMaterial({
    color: plan.sinkMaterial === "matte-black" ? "#222" : "#c9ced3",
    metalness: 0.9,
    roughness: 0.2,
  });

  const cabinets = createCabinets(plan, cabinetMat);
  root.add(cabinets);

  const shape = counterTopShape(plan);
  const extrude = new THREE.ExtrudeGeometry(shape, {
    depth: plan.counterThickness * MM,
    bevelEnabled: true,
    bevelThickness: 0.002,
    bevelSize: 0.002,
    bevelSegments: 2,
  });
  // Extrude goes along +Z of shape's local; rotate so Y is up
  extrude.rotateX(Math.PI / 2);
  // After rotateX(90°): shape XY -> XZ, extrusion +Z becomes -Y. Shift to sit on cabinet.
  const counter = new THREE.Mesh(extrude, counterMat);
  counter.position.y = plan.cabinetHeight * MM + plan.counterThickness * MM;
  counter.castShadow = true;
  counter.receiveShadow = true;
  root.add(counter);

  const sinkLayer = new THREE.Group();
  sinkLayer.position.y = plan.cabinetHeight * MM + plan.counterThickness * MM;
  for (const bowl of plan.bowls) {
    sinkLayer.add(createSinkBasin(bowl, sinkMat));
    if (plan.faucet) {
      sinkLayer.add(createFaucet(bowl, faucetMat));
    }
  }
  root.add(sinkLayer);

  const splash = createBacksplash(plan, counterMat);
  if (splash) root.add(splash);

  root.add(createFloor(plan));
  root.add(createWall(plan));

  // Center pivot for nicer camera framing
  const box = new THREE.Box3().setFromObject(root);
  const center = box.getCenter(new THREE.Vector3());
  root.userData.bounds = box;
  root.userData.center = center;

  return root;
}
