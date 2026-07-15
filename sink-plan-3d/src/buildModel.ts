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
  plan: SinkPlan,
  x: number,
  z: number,
  w: number,
  d: number,
  material: THREE.Material,
  opts: { flush?: boolean; frontAlongZ?: boolean } = {},
) {
  const h = plan.cabinetHeight * MM;
  const kickH = plan.showToeKick ? plan.toeKickHeight * MM : 0;
  const kickD = plan.showToeKick ? plan.toeKickDepth * MM : 0;
  const overhang = plan.counterOverhang * MM;
  // Cabinet body sits inset from front by overhang; slightly shy of back when not flush
  const bodyH = Math.max(0.2, h - kickH);
  const frontAlongZ = opts.frontAlongZ !== false;

  let bw: number;
  let bd: number;
  let cx: number;
  let cz: number;

  if (frontAlongZ) {
    // Front face is toward -Z (smaller z) for main run
    bw = opts.flush ? w : w * 0.995;
    bd = Math.max(0.2, d - overhang);
    cx = x + w / 2;
    cz = z + overhang + bd / 2;
  } else {
    // Return leg: front faces +X (outer) when along left wall — keep simple: front toward +X open side
    bw = Math.max(0.2, w - overhang);
    bd = opts.flush ? d : d * 0.995;
    cx = x + overhang + bw / 2;
    cz = z + d / 2;
  }

  // Carcass
  const body = new THREE.Mesh(new THREE.BoxGeometry(bw, bodyH, bd), material);
  body.position.set(cx, kickH + bodyH / 2, cz);
  body.castShadow = true;
  body.receiveShadow = true;
  group.add(body);

  // Toe kick
  if (plan.showToeKick && kickH > 0.01) {
    const kickMat = new THREE.MeshStandardMaterial({ color: "#1a1a1a", roughness: 0.9 });
    if (frontAlongZ) {
      const kick = new THREE.Mesh(
        new THREE.BoxGeometry(bw, kickH, Math.max(0.02, bd - kickD)),
        kickMat,
      );
      kick.position.set(cx, kickH / 2, z + overhang + kickD + (bd - kickD) / 2);
      group.add(kick);
    } else {
      const kick = new THREE.Mesh(
        new THREE.BoxGeometry(Math.max(0.02, bw - kickD), kickH, bd),
        kickMat,
      );
      kick.position.set(x + overhang + kickD + (bw - kickD) / 2, kickH / 2, cz);
      group.add(kick);
    }
  }

  // Door / drawer face layout — one door per module box
  const doors = 1;
  const gap = 0.003;
  const drawerH = plan.drawerRows > 0 ? Math.min(0.14, bodyH * 0.22) : 0;
  const drawersTotal = drawerH * plan.drawerRows + gap * Math.max(0, plan.drawerRows);
  const doorH = Math.max(0.12, bodyH - drawersTotal - gap);
  const faceMat = material;
  const seamMat = new THREE.MeshStandardMaterial({
    color: "#0d0d0d",
    roughness: 1,
    transparent: true,
    opacity: 0.22,
  });

  const faceFrontZ = frontAlongZ ? z + overhang - 0.004 : undefined;
  const faceFrontX = !frontAlongZ ? x + overhang - 0.004 : undefined;

  // Drawers
  for (let r = 0; r < plan.drawerRows; r++) {
    const dy = kickH + bodyH - gap - drawerH / 2 - r * (drawerH + gap);
    for (let i = 0; i < doors; i++) {
      const cell = bw / doors;
      const dw = cell - gap;
      const dx = cx - bw / 2 + cell * i + cell / 2;
      if (frontAlongZ) {
        const face = new THREE.Mesh(new THREE.BoxGeometry(dw, drawerH, 0.016), faceMat);
        face.position.set(dx, dy, faceFrontZ!);
        face.castShadow = true;
        group.add(face);
        // subtle rail line
        const rail = new THREE.Mesh(new THREE.BoxGeometry(dw * 0.7, 0.004, 0.01), seamMat);
        rail.position.set(dx, dy, faceFrontZ! - 0.006);
        group.add(rail);
      } else {
        const face = new THREE.Mesh(new THREE.BoxGeometry(0.016, drawerH, dw), faceMat);
        face.position.set(faceFrontX!, dy, cz - bd / 2 + cell * i + cell / 2);
        face.castShadow = true;
        group.add(face);
      }
    }
  }

  // Doors
  const doorY = kickH + doorH / 2 + gap * 0.5;
  for (let i = 0; i < doors; i++) {
    const cell = bw / doors;
    const dw = cell - gap;
    const dx = cx - bw / 2 + cell * i + cell / 2;
    if (frontAlongZ) {
      const face = new THREE.Mesh(new THREE.BoxGeometry(dw, doorH, 0.016), faceMat);
      face.position.set(dx, doorY, faceFrontZ!);
      face.castShadow = true;
      group.add(face);
      if (i > 0) {
        const seam = new THREE.Mesh(new THREE.BoxGeometry(0.003, doorH * 0.92, 0.012), seamMat);
        seam.position.set(cx - bw / 2 + cell * i, doorY, faceFrontZ! - 0.005);
        group.add(seam);
      }
    } else {
      const face = new THREE.Mesh(new THREE.BoxGeometry(0.016, doorH, dw), faceMat);
      const dz = cz - bd / 2 + cell * i + cell / 2;
      face.position.set(faceFrontX!, doorY, dz);
      face.castShadow = true;
      group.add(face);
    }
  }

  // Handles
  if (plan.showHandles) {
    const handleMat = new THREE.MeshStandardMaterial({
      color: "#c5c9ce",
      metalness: 0.85,
      roughness: 0.22,
    });
    const hy = kickH + bodyH * (plan.handleHeightPct / 100);
    for (let i = 0; i < doors; i++) {
      const cell = bw / doors;
      const dx = cx - bw / 2 + cell * i + cell / 2;
      if (frontAlongZ) {
        const handle = new THREE.Mesh(new THREE.BoxGeometry(0.012, 0.09, 0.014), handleMat);
        handle.position.set(dx + cell * 0.28, hy, faceFrontZ! - 0.012);
        group.add(handle);
      } else {
        const handle = new THREE.Mesh(new THREE.BoxGeometry(0.014, 0.09, 0.012), handleMat);
        handle.position.set(faceFrontX! - 0.012, hy, cz - bd / 2 + cell * i + cell / 2);
        group.add(handle);
      }
    }
  }
}

function createCabinets(plan: SinkPlan, material: THREE.Material): THREE.Group {
  const group = new THREE.Group();
  const D = plan.counterDepth * MM;

  // Main run: one physical cabinet per width entry
  let x = 0;
  for (const wMm of plan.cabinetWidths) {
    const w = wMm * MM;
    addCabinetBox(group, plan, x, 0, w, D, material, {
      flush: true,
      frontAlongZ: true,
    });
    x += w;
  }

  if (plan.template === "l-shape" && plan.returnCabinetWidths.length > 0) {
    const RD = plan.returnDepth * MM;
    let z = D;
    for (const wMm of plan.returnCabinetWidths) {
      const depth = wMm * MM;
      addCabinetBox(group, plan, 0, z, RD, depth, material, {
        flush: true,
        frontAlongZ: false,
      });
      z += depth;
    }
  }
  return group;
}

function createUpperCabinets(plan: SinkPlan, material: THREE.Material): THREE.Group | null {
  if (!plan.showUpperCabinets) return null;
  const group = new THREE.Group();
  const heights = plan.upperCabinetHeight * MM;
  const depth = plan.upperCabinetDepth * MM;
  const gap = plan.upperGapFromCounter * MM;
  const y0 =
    plan.cabinetHeight * MM + plan.counterThickness * MM + gap;
  const widths =
    plan.matchUpperToLower || plan.upperCabinetWidths.length === 0
      ? plan.cabinetWidths
      : plan.upperCabinetWidths;

  const D = plan.counterDepth * MM;
  // Hang against the back of the main run
  let x = 0;
  for (const wMm of widths) {
    const w = wMm * MM;
    const body = new THREE.Mesh(new THREE.BoxGeometry(w * 0.98, heights, depth), material);
    body.position.set(x + w / 2, y0 + heights / 2, D - depth / 2);
    body.castShadow = true;
    body.receiveShadow = true;
    group.add(body);

    // Door face
    const door = new THREE.Mesh(
      new THREE.BoxGeometry(w * 0.95, heights * 0.92, 0.014),
      material,
    );
    door.position.set(x + w / 2, y0 + heights / 2, D - depth - 0.006);
    door.castShadow = true;
    group.add(door);

    if (plan.showHandles) {
      const handleMat = new THREE.MeshStandardMaterial({
        color: "#c5c9ce",
        metalness: 0.85,
        roughness: 0.22,
      });
      const handle = new THREE.Mesh(new THREE.BoxGeometry(0.012, 0.08, 0.012), handleMat);
      handle.position.set(x + w * 0.75, y0 + heights * 0.45, D - depth - 0.018);
      group.add(handle);
    }
    x += w;
  }

  // L return upper cabinets along left wall
  if (plan.template === "l-shape" && plan.returnCabinetWidths.length > 0) {
    let z = D;
    for (const wMm of plan.returnCabinetWidths) {
      const len = wMm * MM;
      const body = new THREE.Mesh(new THREE.BoxGeometry(depth, heights, len * 0.98), material);
      body.position.set(depth / 2, y0 + heights / 2, z + len / 2);
      body.castShadow = true;
      body.receiveShadow = true;
      group.add(body);

      const door = new THREE.Mesh(
        new THREE.BoxGeometry(0.014, heights * 0.92, len * 0.95),
        material,
      );
      door.position.set(depth + 0.006, y0 + heights / 2, z + len / 2);
      group.add(door);
      z += len;
    }
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
  const backGap = plan.wallBackOffset * MM;
  const leftGap = plan.wallLeftOffset * MM;

  if (plan.template === "l-shape" && plan.returnWidth > 0 && plan.returnDepth > 0) {
    const W = plan.counterWidth * MM;
    const RW = plan.returnWidth * MM;
    const RD = plan.returnDepth * MM;
    const span = Math.max(W, RW, 1.2) + Math.max(backGap, leftGap, 0);

    const floor = new THREE.Mesh(
      new THREE.CircleGeometry(span * 1.15, 48),
      new THREE.MeshStandardMaterial({ color: "#c8c2b6", roughness: 0.95 }),
    );
    floor.rotation.x = -Math.PI / 2;
    floor.position.set(span * 0.35, -0.001, span * 0.4);
    floor.receiveShadow = true;
    group.add(floor);

    if (plan.showWall) {
      // Left room wall — offset moves it further left (negative X)
      const leftWall = new THREE.Mesh(
        new THREE.BoxGeometry(wallT, 2.2, RW + 0.5 + Math.max(0, backGap)),
        wallMat,
      );
      leftWall.position.set(-leftGap - wallT / 2, 1.1, RW / 2 + backGap * 0.15);
      leftWall.receiveShadow = true;
      group.add(leftWall);

      // Back room wall behind return — offset moves it further +Z
      const backLen = Math.max(RD + leftGap, W * 0.5) + 0.5;
      const backWall = new THREE.Mesh(
        new THREE.BoxGeometry(backLen, 2.2, wallT),
        wallMat,
      );
      backWall.position.set(backLen / 2 - leftGap * 0.5, 1.1, RW + backGap + wallT / 2);
      backWall.receiveShadow = true;
      group.add(backWall);
    }

    return group;
  }

  const D = plan.counterDepth * MM;
  const W = plan.counterWidth * MM;
  const span = Math.max(W, D, 1.2) + Math.max(0, backGap);

  const floor = new THREE.Mesh(
    new THREE.CircleGeometry(span * 1.2, 48),
    new THREE.MeshStandardMaterial({ color: "#c8c2b6", roughness: 0.95 }),
  );
  floor.rotation.x = -Math.PI / 2;
  floor.position.set(span * 0.4, -0.001, span * 0.35);
  floor.receiveShadow = true;
  group.add(floor);

  if (plan.showWall) {
    const wall = new THREE.Mesh(
      new THREE.BoxGeometry(W + 0.8, 2.2, wallT),
      wallMat,
    );
    wall.position.set(W / 2, 1.1, D + backGap + wallT / 2);
    wall.receiveShadow = true;
    group.add(wall);
  }

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

  const upper = createUpperCabinets(plan, cabinetMat);
  if (upper) furniture.add(upper);

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
