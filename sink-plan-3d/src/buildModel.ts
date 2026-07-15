import * as THREE from "three";
import type { SinkBowl, SinkPlan } from "./types";
import {
  makeCabinetMaterial,
  makeCounterMaterial,
  makeSinkMaterial,
} from "./materials";

/** Convert mm → meters */
export const MM = 0.001;

function makeShapeFromPoints(points: Array<[number, number]>, reverse = false): THREE.Shape {
  const pts = reverse ? [...points].reverse() : points;
  const shape = new THREE.Shape();
  shape.moveTo(pts[0][0], pts[0][1]);
  for (let i = 1; i < pts.length; i++) shape.lineTo(pts[i][0], pts[i][1]);
  shape.closePath();
  return shape;
}

function counterOuterPoints(plan: SinkPlan): Array<[number, number]> {
  const W = plan.counterWidth * MM;
  const D = plan.counterDepth * MM;
  if (plan.template === "l-shape" && plan.returnWidth > 0 && plan.returnDepth > 0) {
    const RW = plan.returnWidth * MM;
    const RD = plan.returnDepth * MM;
    // CCW on XZ-plan mapped as XY for Shape
    return [
      [0, 0],
      [W, 0],
      [W, D],
      [RD, D],
      [RD, RW],
      [0, RW],
    ];
  }
  return [
    [0, 0],
    [W, 0],
    [W, D],
    [0, D],
  ];
}

function bowlHolePoints(bowl: SinkBowl): Array<[number, number]> {
  const x = bowl.offsetX * MM;
  const z = bowl.offsetZ * MM;
  const w = bowl.width * MM;
  const d = bowl.depth * MM;
  // Same order as outer (CCW) — caller will reverse for hole winding
  return [
    [x, z],
    [x + w, z],
    [x + w, z + d],
    [x, z + d],
  ];
}

function createCountertop(plan: SinkPlan, material: THREE.Material): THREE.Mesh {
  const shape = makeShapeFromPoints(counterOuterPoints(plan), false);
  for (const bowl of plan.bowls) {
    // Holes must wind opposite to outer path
    const hole = makeShapeFromPoints(bowlHolePoints(bowl), true);
    shape.holes.push(hole);
  }

  const geo = new THREE.ExtrudeGeometry(shape, {
    depth: plan.counterThickness * MM,
    bevelEnabled: false,
    curveSegments: 8,
  });
  // Extrude +Z then rotate so plan XY → world XZ, thickness down −Y
  geo.rotateX(Math.PI / 2);

  const mesh = new THREE.Mesh(geo, material);
  // Local top (y=0) becomes world counter top
  mesh.position.y = plan.cabinetHeight * MM + plan.counterThickness * MM;
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  return mesh;
}

function createSinkBasin(bowl: SinkBowl, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const w = bowl.width * MM;
  const d = bowl.depth * MM;
  const depth = bowl.bowlDepth * MM;
  const wall = 0.01;

  const bottom = new THREE.Mesh(
    new THREE.BoxGeometry(Math.max(0.05, w - wall * 2), 0.008, Math.max(0.05, d - wall * 2)),
    material,
  );
  bottom.position.set(w / 2, -depth, d / 2);
  bottom.castShadow = true;
  group.add(bottom);

  const mkWall = (gw: number, gd: number, px: number, pz: number) => {
    const m = new THREE.Mesh(new THREE.BoxGeometry(gw, depth, gd), material);
    m.position.set(px, -depth / 2, pz);
    m.castShadow = true;
    group.add(m);
  };
  mkWall(w, wall, w / 2, wall / 2);
  mkWall(w, wall, w / 2, d - wall / 2);
  mkWall(wall, d - wall * 2, wall / 2, d / 2);
  mkWall(wall, d - wall * 2, w - wall / 2, d / 2);

  const drainMat = new THREE.MeshStandardMaterial({
    color: "#4a4a4a",
    metalness: 0.85,
    roughness: 0.3,
  });
  const drain = new THREE.Mesh(new THREE.CylinderGeometry(0.022, 0.022, 0.012, 20), drainMat);
  drain.position.set(w / 2, -depth + 0.008, d / 2);
  group.add(drain);

  group.position.set(bowl.offsetX * MM, 0, bowl.offsetZ * MM);
  return group;
}

function createFaucet(bowl: SinkBowl, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const cx = bowl.offsetX * MM + (bowl.width * MM) / 2;
  const cz = bowl.offsetZ * MM + bowl.depth * MM + 0.05;

  const base = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.034, 0.018, 20), material);
  base.position.set(cx, 0.012, cz);
  group.add(base);

  const column = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.014, 0.2, 16), material);
  column.position.set(cx, 0.12, cz);
  group.add(column);

  const spout = new THREE.Mesh(new THREE.CylinderGeometry(0.01, 0.01, 0.15, 12), material);
  spout.rotation.x = Math.PI / 2;
  spout.position.set(cx, 0.21, cz - 0.06);
  group.add(spout);

  const tip = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.01, 0.028, 12), material);
  tip.position.set(cx, 0.195, cz - 0.13);
  group.add(tip);

  return group;
}

function addCabinetBox(
  group: THREE.Group,
  x: number,
  z: number,
  w: number,
  d: number,
  h: number,
  material: THREE.Material,
  opts: { flush?: boolean } = {},
) {
  const bw = opts.flush ? w : w * 0.98;
  const bd = opts.flush ? d : d * 0.94;
  const body = new THREE.Mesh(new THREE.BoxGeometry(bw, h, bd), material);
  body.position.set(x + w / 2, h / 2, z + d / 2);
  body.castShadow = true;
  body.receiveShadow = true;
  group.add(body);

  const kick = new THREE.Mesh(
    new THREE.BoxGeometry(bw, 0.08, 0.04),
    new THREE.MeshStandardMaterial({ color: "#1a1a1a", roughness: 0.9 }),
  );
  kick.position.set(x + w / 2, 0.04, z + 0.03);
  group.add(kick);

  // Door handles
  const handleMat = new THREE.MeshStandardMaterial({
    color: "#c5c9ce",
    metalness: 0.8,
    roughness: 0.25,
  });
  const doors = Math.max(1, Math.round(w / 0.5));
  for (let i = 0; i < doors; i++) {
    const hx = x + ((i + 0.5) / doors) * w;
    const handle = new THREE.Mesh(new THREE.BoxGeometry(0.012, 0.08, 0.012), handleMat);
    handle.position.set(hx, h * 0.55, z + 0.02);
    group.add(handle);
  }
}

function createCabinets(plan: SinkPlan, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const H = plan.cabinetHeight * MM;

  if (plan.template === "l-shape" && plan.returnWidth > 0) {
    const W = plan.counterWidth * MM;
    const D = plan.counterDepth * MM;
    const RW = plan.returnWidth * MM;
    const RD = plan.returnDepth * MM;
    // Flush boxes so the corner does not show a cut gap
    addCabinetBox(group, 0, 0, W, D, H, material, { flush: true });
    addCabinetBox(group, 0, D, RD, RW - D, H, material, { flush: true });
  } else {
    addCabinetBox(group, 0, 0, plan.counterWidth * MM, plan.counterDepth * MM, H, material);
  }
  return group;
}

function createBacksplash(plan: SinkPlan, material: THREE.Material): THREE.Group | null {
  if (plan.backsplashHeight <= 0) return null;
  const group = new THREE.Group();
  const H = plan.backsplashHeight * MM;
  const T = 0.014;
  const y =
    plan.cabinetHeight * MM + plan.counterThickness * MM + H / 2;

  if (plan.template === "l-shape" && plan.returnWidth > 0 && plan.returnDepth > 0) {
    const RW = plan.returnWidth * MM;
    const RD = plan.returnDepth * MM;

    // Outer left wall run (x ≈ 0), full return length
    const left = new THREE.Mesh(new THREE.BoxGeometry(T, H, RW), material);
    left.position.set(T / 2, y, RW / 2);
    left.castShadow = true;
    left.receiveShadow = true;
    group.add(left);

    // Outer back of return leg (z ≈ RW)
    const backReturn = new THREE.Mesh(new THREE.BoxGeometry(RD, H, T), material);
    backReturn.position.set(RD / 2, y, RW - T / 2);
    backReturn.castShadow = true;
    backReturn.receiveShadow = true;
    group.add(backReturn);

    return group;
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
  group.add(mesh);
  return group;
}

function createRoom(plan: SinkPlan): THREE.Group {
  const group = new THREE.Group();
  const wallMat = new THREE.MeshStandardMaterial({ color: "#ebe6dc", roughness: 0.95 });
  const wallT = 0.06;

  if (plan.template === "l-shape" && plan.returnWidth > 0 && plan.returnDepth > 0) {
    const W = plan.counterWidth * MM;
    const RW = plan.returnWidth * MM;
    const RD = plan.returnDepth * MM;
    const span = Math.max(W, RW, 1.2);

    const floor = new THREE.Mesh(
      new THREE.CircleGeometry(span * 1.15, 48),
      new THREE.MeshStandardMaterial({ color: "#c8c2b6", roughness: 0.95 }),
    );
    floor.rotation.x = -Math.PI / 2;
    floor.position.set(span * 0.35, -0.001, span * 0.4);
    floor.receiveShadow = true;
    group.add(floor);

    // Left room wall along the long return / corner (outside x=0)
    const leftWall = new THREE.Mesh(
      new THREE.BoxGeometry(wallT, 2.2, RW + 0.4),
      wallMat,
    );
    leftWall.position.set(-wallT / 2 - 0.01, 1.1, RW / 2);
    leftWall.receiveShadow = true;
    group.add(leftWall);

    // Back room wall behind return leg (outside z=RW) — must NOT cut through the L
    const backWall = new THREE.Mesh(
      new THREE.BoxGeometry(Math.max(RD, W * 0.55) + 0.5, 2.2, wallT),
      wallMat,
    );
    backWall.position.set(Math.max(RD, W * 0.55) / 2, 1.1, RW + wallT / 2 + 0.01);
    backWall.receiveShadow = true;
    group.add(backWall);

    return group;
  }

  const span = Math.max(plan.counterWidth * MM, plan.counterDepth * MM, 1.2);
  const floor = new THREE.Mesh(
    new THREE.CircleGeometry(span * 1.2, 48),
    new THREE.MeshStandardMaterial({ color: "#c8c2b6", roughness: 0.95 }),
  );
  floor.rotation.x = -Math.PI / 2;
  floor.position.set(span * 0.4, -0.001, span * 0.35);
  floor.receiveShadow = true;
  group.add(floor);

  const wall = new THREE.Mesh(
    new THREE.BoxGeometry(plan.counterWidth * MM + 0.6, 2.2, wallT),
    wallMat,
  );
  wall.position.set(
    (plan.counterWidth * MM) / 2,
    1.1,
    plan.counterDepth * MM + wallT / 2 + 0.02,
  );
  wall.receiveShadow = true;
  group.add(wall);

  return group;
}

export function buildSinkModel(plan: SinkPlan): THREE.Group {
  const root = new THREE.Group();
  root.name = "SinkModel";

  const counterMat = makeCounterMaterial(plan.counterMaterial);
  const cabinetMat = makeCabinetMaterial(plan.cabinetMaterial);
  const sinkMat = makeSinkMaterial(plan.sinkMaterial);
  const faucetMat = new THREE.MeshStandardMaterial({
    color: plan.sinkMaterial === "matte-black" ? "#1c1c1c" : "#c5c9ce",
    metalness: 0.88,
    roughness: 0.22,
  });

  const furniture = new THREE.Group();
  furniture.name = "Furniture";

  furniture.add(createCabinets(plan, cabinetMat));
  furniture.add(createCountertop(plan, counterMat));

  const sinkY = plan.cabinetHeight * MM + plan.counterThickness * MM;
  const sinkLayer = new THREE.Group();
  sinkLayer.position.y = sinkY;
  for (const bowl of plan.bowls) {
    sinkLayer.add(createSinkBasin(bowl, sinkMat));
    if (plan.faucet) sinkLayer.add(createFaucet(bowl, faucetMat));
  }
  furniture.add(sinkLayer);

  const splash = createBacksplash(plan, counterMat);
  if (splash) furniture.add(splash);

  root.add(furniture);
  root.add(createRoom(plan));

  // Force matrix update before measuring
  furniture.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(furniture);
  const center = box.getCenter(new THREE.Vector3());
  const size = box.getSize(new THREE.Vector3());
  root.userData.bounds = box;
  root.userData.center = center;
  root.userData.size = size;

  return root;
}
