import * as THREE from "three";
import type { CabinetMaterial, CounterMaterial, SinkMaterial } from "./types";

export function counterColor(m: CounterMaterial): THREE.ColorRepresentation {
  switch (m) {
    case "white-quartz":
      return "#f2efe8";
    case "oak":
      return "#c4a574";
    case "black-granite":
      return "#2a2c2f";
    case "concrete":
      return "#9a9b98";
  }
}

export function cabinetColor(m: CabinetMaterial): THREE.ColorRepresentation {
  switch (m) {
    case "white":
      return "#e8e6e1";
    case "walnut":
      return "#5c3d2e";
    case "sage":
      return "#7a8f7a";
  }
}

export function sinkColor(m: SinkMaterial): THREE.ColorRepresentation {
  switch (m) {
    case "stainless":
      return "#b8c1c6";
    case "white-ceramic":
      return "#fafafa";
    case "matte-black":
      return "#1a1a1a";
  }
}

export function makeCounterMaterial(m: CounterMaterial): THREE.MeshStandardMaterial {
  const color = counterColor(m);
  return new THREE.MeshStandardMaterial({
    color,
    roughness: m === "black-granite" ? 0.35 : m === "oak" ? 0.55 : 0.28,
    metalness: m === "black-granite" ? 0.15 : 0.05,
  });
}

export function makeCabinetMaterial(m: CabinetMaterial): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color: cabinetColor(m),
    roughness: 0.65,
    metalness: 0.02,
  });
}

export function makeSinkMaterial(m: SinkMaterial): THREE.MeshStandardMaterial {
  if (m === "stainless") {
    return new THREE.MeshStandardMaterial({
      color: sinkColor(m),
      roughness: 0.22,
      metalness: 0.85,
    });
  }
  if (m === "matte-black") {
    return new THREE.MeshStandardMaterial({
      color: sinkColor(m),
      roughness: 0.85,
      metalness: 0.1,
    });
  }
  return new THREE.MeshStandardMaterial({
    color: sinkColor(m),
    roughness: 0.35,
    metalness: 0.05,
  });
}
