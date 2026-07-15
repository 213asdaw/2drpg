import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { buildSinkModel } from "./buildModel";
import type { SinkPlan } from "./types";

export class SinkScene {
  readonly renderer: THREE.WebGLRenderer;
  readonly scene: THREE.Scene;
  readonly camera: THREE.PerspectiveCamera;
  readonly controls: OrbitControls;
  private container: HTMLElement;
  private model: THREE.Group | null = null;
  private animId = 0;
  private disposed = false;

  constructor(container: HTMLElement) {
    this.container = container;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color("#c5d0d4");
    this.scene.fog = new THREE.Fog("#c5d0d4", 6, 18);

    const w = container.clientWidth || 800;
    const h = container.clientHeight || 600;

    this.camera = new THREE.PerspectiveCamera(42, w / h, 0.05, 50);
    this.camera.position.set(2.2, 1.8, 2.6);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(w, h);
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.05;
    container.appendChild(this.renderer.domElement);

    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.06;
    this.controls.maxPolarAngle = Math.PI * 0.49;
    this.controls.minDistance = 0.8;
    this.controls.maxDistance = 8;

    this.setupLights();
    window.addEventListener("resize", this.onResize);
    this.loop();
  }

  private setupLights() {
    const hemi = new THREE.HemisphereLight("#f0f4f7", "#8a8074", 0.7);
    this.scene.add(hemi);

    const key = new THREE.DirectionalLight("#fff6e8", 1.35);
    key.position.set(3, 5, 2);
    key.castShadow = true;
    key.shadow.mapSize.set(2048, 2048);
    key.shadow.camera.near = 0.5;
    key.shadow.camera.far = 20;
    key.shadow.camera.left = -4;
    key.shadow.camera.right = 4;
    key.shadow.camera.top = 4;
    key.shadow.camera.bottom = -4;
    key.shadow.bias = -0.0002;
    this.scene.add(key);

    const fill = new THREE.DirectionalLight("#d7e4ea", 0.45);
    fill.position.set(-2.5, 2, -1.5);
    this.scene.add(fill);

    const rim = new THREE.DirectionalLight("#ffffff", 0.25);
    rim.position.set(0.5, 1.5, -3);
    this.scene.add(rim);
  }

  setPlan(plan: SinkPlan, frameCamera = false) {
    if (this.model) {
      this.scene.remove(this.model);
      this.model.traverse((obj) => {
        if (obj instanceof THREE.Mesh) {
          obj.geometry.dispose();
          const mats = Array.isArray(obj.material) ? obj.material : [obj.material];
          mats.forEach((m) => m.dispose());
        }
      });
      this.model = null;
    }

    this.model = buildSinkModel(plan);
    this.scene.add(this.model);

    const center: THREE.Vector3 = this.model.userData.center ?? new THREE.Vector3();
    this.controls.target.copy(center);
    if (frameCamera) {
      const box: THREE.Box3 = this.model.userData.bounds;
      const size = box.getSize(new THREE.Vector3());
      const maxDim = Math.max(size.x, size.y, size.z);
      const dist = maxDim * 1.7 + 0.8;
      this.camera.position.set(center.x + dist * 0.7, center.y + dist * 0.55, center.z + dist * 0.85);
    }
    this.controls.update();
  }

  private onResize = () => {
    const w = this.container.clientWidth;
    const h = this.container.clientHeight;
    if (!w || !h) return;
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h);
  };

  private loop = () => {
    if (this.disposed) return;
    this.animId = requestAnimationFrame(this.loop);
    this.controls.update();
    this.renderer.render(this.scene, this.camera);
  };

  dispose() {
    this.disposed = true;
    cancelAnimationFrame(this.animId);
    window.removeEventListener("resize", this.onResize);
    this.controls.dispose();
    this.renderer.dispose();
    this.renderer.domElement.remove();
  }

  capturePng(): string {
    this.renderer.render(this.scene, this.camera);
    return this.renderer.domElement.toDataURL("image/png");
  }
}
