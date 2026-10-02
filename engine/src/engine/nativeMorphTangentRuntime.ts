import * as THREE from "three";

const tangentStates = new WeakMap<THREE.BufferGeometry, {
  base: Float32Array;
  weights: number[];
}>();

// Three morphs position and normal on the GPU, but not tangent. Update the
// relative tangent channel before renderer.render() uploads vertex attributes.
export function updateNativeMorphTangents(root: THREE.Object3D): void {
  const updated = new Set<THREE.BufferGeometry>();
  root.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh || mesh.userData.pjskOutlineShell || updated.has(mesh.geometry)) return;
    const geometry = mesh.geometry;
    const targets = (geometry.morphAttributes as typeof geometry.morphAttributes & {
      tangent?: THREE.BufferAttribute[];
    }).tangent;
    if (!targets?.length) return;
    updated.add(geometry);
    const tangent = geometry.getAttribute("tangent") as THREE.BufferAttribute;
    const weights = mesh.morphTargetInfluences;
    if (!weights || weights.length !== targets.length || !weights.every(Number.isFinite)) {
      throw new Error(`Native mesh '${mesh.name}' has invalid tangent morph influences.`);
    }
    let state = tangentStates.get(geometry);
    if (!state) {
      state = { base: new Float32Array(tangent.array), weights: [] };
      tangentStates.set(geometry, state);
    }
    if (weights.length === state.weights.length && weights.every((weight, i) => weight === state.weights[i])) return;
    // Always start from the authored vec4, retaining handedness in w.
    tangent.copyArray(state.base);
    for (let index = 0; index < targets.length; index += 1) {
      const weight = weights[index]!;
      if (weight === 0) continue;
      const delta = targets[index]!;
      for (let vertex = 0; vertex < tangent.count; vertex += 1) {
        tangent.setXYZ(vertex,
          tangent.getX(vertex) + delta.getX(vertex) * weight,
          tangent.getY(vertex) + delta.getY(vertex) * weight,
          tangent.getZ(vertex) + delta.getZ(vertex) * weight);
      }
    }
    state.weights = weights.slice();
    tangent.needsUpdate = true;
  });
}
