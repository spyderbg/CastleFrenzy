<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount } from "vue";
import * as THREE from "three";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import type { Model, Clip } from "../../shared/level";
import { api } from "./api";
const props = defineProps<{ assets: string; model: Model }>();
const host = ref<HTMLElement>();
const loadedName = ref(""),
  meshCount = ref(0);
const clips = ref<Clip[]>([]),
  selected = ref(0),
  playing = ref(true),
  loop = ref(true),
  step = ref(true),
  time = ref(0),
  duration = ref(0),
  speed = ref(1),
  error = ref(""),
  loading = ref(false),
  wireframe = ref(false);
let renderer: THREE.WebGLRenderer,
  scene: THREE.Scene,
  camera: THREE.PerspectiveCamera,
  controls: OrbitControls;
let root: THREE.Object3D | undefined,
  mixer: THREE.AnimationMixer | undefined,
  action: THREE.AnimationAction | undefined;
let animations: THREE.AnimationClip[] = [],
  frame = 0,
  generation = 0,
  observer: ResizeObserver;
const textureUrl = (p: string) => `/api/texture?path=${encodeURIComponent(p)}`;
function disposeRoot(group?: THREE.Object3D) {
  if (!group) return;
  scene.remove(group);
  group.traverse((node) => {
    if (node instanceof THREE.Mesh) {
      node.geometry.dispose();
      for (const material of Array.isArray(node.material)
        ? node.material
        : [node.material]) {
        for (const v of Object.values(material))
          if (v instanceof THREE.Texture) v.dispose();
        material.dispose();
      }
    }
  });
}
function fit() {
  if (!root) return;
  root.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(root, true),
    center = box.getCenter(new THREE.Vector3()),
    size = Math.max(1, box.getSize(new THREE.Vector3()).length());
  if (box.isEmpty() || !Number.isFinite(size))
    throw new Error("Model has no finite visible bounds");
  controls.target.copy(center);
  camera.position
    .copy(center)
    .add(new THREE.Vector3(size * 0.75, size * 0.5, size * 0.9));
  camera.near = size / 1000;
  camera.far = size * 100;
  camera.updateProjectionMatrix();
  controls.update();
}
function selectClip() {
  if (!mixer || !animations.length) {
    duration.value = 0;
    return;
  }
  mixer.stopAllAction();
  if (!animations[selected.value]) {
    action = undefined;
    time.value = 0;
    duration.value = 0;
    return;
  }
  const animation = animations[selected.value].clone();
  if (step.value)
    animation.tracks.forEach((t) =>
      t.setInterpolation(THREE.InterpolateDiscrete),
    );
  action = mixer.clipAction(animation);
  action.setLoop(
    loop.value ? THREE.LoopRepeat : THREE.LoopOnce,
    loop.value ? Infinity : 1,
  );
  action.clampWhenFinished = true;
  action.play();
  duration.value = Math.max(0, animation.duration);
  time.value = 0;
  mixer.update(0);
}
async function load() {
  if (!scene) return;
  const id = ++generation;
  loading.value = true;
  loadedName.value = "";
  meshCount.value = 0;
  error.value = "";
  mixer?.stopAllAction();
  disposeRoot(root);
  root = undefined;
  mixer = undefined;
  action = undefined;
  clips.value = [];
  animations = [];
  try {
    const result = await api<
      Model & {
        fbx: string;
        directory: string;
        textures: { file: string; object: string }[];
      }
    >(
      `/api/model?assets=${encodeURIComponent(props.assets)}&name=${encodeURIComponent(props.model.name)}`,
    );
    if (id !== generation) return;
    const manager = new THREE.LoadingManager();
    let texturesStarted = false;
    let finishTextures: () => void;
    const texturesReady = new Promise<void>((resolve) => {
      finishTextures = resolve;
    });
    manager.onStart = () => {
      texturesStarted = true;
    };
    manager.onLoad = () => {
      finishTextures();
    };
    manager.setURLModifier((url) => {
      if (url.startsWith("data:")) return url;
      // The FBX names its textures by their legacy labels (.tga). Game textures are siblings.
      const file =
        decodeURIComponent(url).replace(/\\/g, "/").split("/").pop() || "";
      return textureUrl(
        `${result.directory}/${file.replace(/\.(?:pvr|tga)$/i, ".png")}`,
      );
    });
    const failures: string[] = [];
    manager.onError = (url) => {
      failures.push(url);
      if (id === generation)
        error.value = "Some model textures could not be loaded.";
    };
    const file = await fetch(
      `/api/model-file?path=${encodeURIComponent(result.fbx)}`,
    );
    if (!file.ok) throw new Error(`Missing model: ${result.fbx}`);
    const loaded = new FBXLoader(manager).parse(await file.arrayBuffer(), "");
    if (id !== generation) return;
    // Blender names each take after its Action, which is the game clip name.
    loaded.animations.forEach((a) => (a.name = a.name.split("|").pop() || a.name));
    loaded.traverse((n) => {
      if (n instanceof THREE.Mesh) {
        meshCount.value++;
        n.geometry.computeVertexNormals();
        const materials = Array.isArray(n.material) ? n.material : [n.material];
        materials.forEach((m) => {
          m.side = THREE.DoubleSide;
          m.alphaTest = 0.5;
          const phong = m as THREE.MeshPhongMaterial;
          phong.wireframe = wireframe.value;
          // Blender's Principled shader exports a glossy specular and vertex tint
          // that the game's materials do not use.
          phong.specular?.set(0x000000);
          phong.vertexColors = false;
        });
      }
    });
    loaded.scale.multiplyScalar(result.scale);
    for (const override of result.textures) {
      const texture = await new THREE.TextureLoader().loadAsync(
        textureUrl(`Data/${override.file.replace(/\\/g, "/")}`),
      );
      texture.colorSpace = THREE.SRGBColorSpace;
      loaded.traverse((n) => {
        if (
          n instanceof THREE.Mesh &&
          (!override.object ||
            (n.name || "")
              .toLowerCase()
              .includes(override.object.toLowerCase()))
        )
          for (const m of Array.isArray(n.material) ? n.material : [n.material])
            if ("map" in m) {
              (m as THREE.MeshPhongMaterial).map = texture;
              m.needsUpdate = true;
            }
      });
    }
    if (id !== generation) {
      disposeRoot(loaded);
      return;
    }
    root = loaded;
    scene.add(loaded);
    animations = loaded.animations;
    clips.value = animations.map((a) => ({
      name: a.name,
      start: 0,
      end: a.duration,
    }));
    selected.value = 0;
    mixer = new THREE.AnimationMixer(loaded);
    selectClip();
    fit();
    if (texturesStarted) await texturesReady;
    if (id === generation) loadedName.value = result.name;
  } catch (e) {
    if (id === generation) {
      error.value = (e as Error).message;
      console.error((e as Error).stack);
    }
  } finally {
    if (id === generation) loading.value = false;
  }
}
function scrub() {
  if (action && mixer) {
    action.paused = false;
    action.time = time.value;
    mixer.update(0);
    action.paused = !playing.value;
  }
}
watch(() => [props.model.name, props.assets], load);
watch([selected, loop, step], selectClip);
watch(wireframe, (value) =>
  root?.traverse((n) => {
    if (n instanceof THREE.Mesh)
      for (const m of Array.isArray(n.material) ? n.material : [n.material])
        (m as THREE.MeshPhongMaterial).wireframe = value;
  }),
);
onMounted(() => {
  try {
    renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    renderer.setClearColor("#172a2e");
    host.value!.appendChild(renderer.domElement);
    scene = new THREE.Scene();
    scene.add(new THREE.HemisphereLight(0xe9f5ff, 0x425743, 2.5));
    const light = new THREE.DirectionalLight(0xffffff, 3);
    light.position.set(5, 10, 6);
    scene.add(light);
    scene.add(new THREE.GridHelper(40, 40, 0x6c8b83, 0x344849));
    camera = new THREE.PerspectiveCamera(40, 1, 0.01, 10000);
    controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    observer = new ResizeObserver(() => {
      if (!host.value) return;
      renderer.setSize(host.value.clientWidth, host.value.clientHeight);
      camera.aspect =
        host.value.clientWidth / Math.max(1, host.value.clientHeight);
      camera.updateProjectionMatrix();
    });
    observer.observe(host.value!);
    let previous = performance.now();
    const draw = (now: number) => {
      frame = requestAnimationFrame(draw);
      const delta = Math.min(0.1, (now - previous) / 1000);
      previous = now;
      if (mixer && action) {
        action.paused = !playing.value;
        mixer.update(delta * speed.value);
        time.value = action.time;
      }
      controls.update();
      renderer.render(scene, camera);
    };
    frame = requestAnimationFrame(draw);
    void load();
  } catch (e) {
    error.value = `3D preview unavailable: ${(e as Error).message}`;
  }
});
onBeforeUnmount(() => {
  generation++;
  cancelAnimationFrame(frame);
  observer?.disconnect();
  controls?.dispose();
  if (scene) disposeRoot(root);
  renderer?.dispose();
});
</script>
<template>
  <div class="model-preview">
    <div class="section-heading">
      <div>
        <h2>{{ model.name }}</h2>
        <small>{{ model.file }} · scale {{ model.scale }}</small>
      </div>
      <button @click="fit">Fit camera</button>
    </div>
    <div
      class="viewport"
      ref="host"
      data-testid="model-viewport"
      :data-loaded-model="loadedName"
      :data-meshes="meshCount"
    >
      <div v-if="loading" class="viewport-message">Loading model…</div>
      <div v-if="error" class="viewport-error" role="alert">{{ error }}</div>
    </div>
    <div class="animation-controls">
      <button @click="playing = !playing">
        {{ playing ? "Pause animation" : "Play animation" }}</button
      ><label><input type="checkbox" v-model="loop" /> Loop</label
      ><label><input type="checkbox" v-model="step" /> Step interpolation</label
      ><label><input type="checkbox" v-model="wireframe" /> Wireframe</label
      ><label
        >Speed
        <input type="number" min=".1" max="4" step=".1" v-model.number="speed"
      /></label>
    </div>
    <div class="animation-controls">
      <label
        >Clip
        <select aria-label="Animation clip" v-model.number="selected">
          <option :value="-1">Rest pose</option>
          <option v-for="(clip, i) in clips" :value="i" :key="i">
            {{ clip.name }}
          </option>
        </select></label
      ><input
        aria-label="Animation time"
        class="scrubber"
        type="range"
        min="0"
        :max="duration"
        step=".01"
        v-model.number="time"
        @input="scrub"
      /><small>{{ time.toFixed(2) }} / {{ duration.toFixed(2) }}s</small>
    </div>
    <p class="hint">
      Drag to orbit · Wheel to zoom · Right-drag to pan ·
      {{ clips.length }} animation clips
    </p>
  </div>
</template>
