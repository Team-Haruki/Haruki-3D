import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";
import {
  Haruki3DEngineCore, createGroupedLayerMesh, createSekaiOutlineMaterial,
  unityPrefabRuntimeInternals as prefab, updateNativeMorphTangents,
} from "../dist/haruki-3d-engine-internal.js";

function fixture() {
  const target = (name, tangentDeltas) => ({
    name, indices: [0], hasPositionDeltas: true, positionDeltas: [0, 0, 0],
    hasNormalDeltas: false, normalDeltas: [], hasTangentDeltas: true, tangentDeltas,
  });
  const geometry = prefab.buildUnityRuntimeNativeGeometry({
    positions: [0, 0, 0], normals: [0, 0, 1], tangents: [1, 2, 3, -1],
    uv1: [1, 0], uv2: [0, 0], submeshes: [{ indices: [0, 0, 0] }],
    morphTargets: [target("smile", [2, 4, 6]), target("blink", [4, -2, 8])],
  });
  const mesh = new THREE.Mesh(geometry, new THREE.MeshBasicMaterial());
  const shell = new THREE.Mesh(geometry, new THREE.MeshBasicMaterial());
  shell.userData.pjskOutlineShell = true;
  // Intentionally independent zero influences: shared geometry must have one
  // authoritative owner, regardless of sibling traversal order.
  const overlay = createGroupedLayerMesh(mesh, geometry.groups, [mesh.material], "overlay");
  const root = new THREE.Group();
  root.add(shell, mesh, overlay);
  return { root, mesh, shell, overlay, tangent: geometry.getAttribute("tangent") };
}

test("native tangent morphs combine signed weights, preserve handedness, restore zero and avoid accumulation", () => {
  const { root, mesh, shell, overlay, tangent } = fixture();
  mesh.morphTargetInfluences.splice(0, 2, 0.5, -0.25);
  updateNativeMorphTangents(root);
  assert.deepEqual([...tangent.array], [1, 4.5, 4, -1]);
  assert.deepEqual([...overlay.geometry.getAttribute("tangent").array], [1, 4.5, 4, -1]);
  assert.equal(shell.geometry.getAttribute("tangent"), tangent);
  const version = tangent.version;
  updateNativeMorphTangents(root);
  assert.equal(tangent.version, version, "unchanged weights must not re-upload or accumulate");
  mesh.morphTargetInfluences.splice(0, 2, -1, 0.5);
  updateNativeMorphTangents(root);
  assert.deepEqual([...tangent.array], [1, -3, 1, -1]);
  mesh.morphTargetInfluences.fill(0);
  updateNativeMorphTangents(root);
  assert.deepEqual([...tangent.array], [1, 2, 3, -1]);
  assert.deepEqual([...overlay.geometry.getAttribute("tangent").array], [1, 2, 3, -1]);
  mesh.morphTargetInfluences[0] = NaN;
  assert.throws(() => updateNativeMorphTangents(root), /invalid tangent morph influences/);
});

test("renderFrame updates tangent attributes before renderer upload and outline shaders consume them", () => {
  const { root, mesh, tangent } = fixture();
  mesh.morphTargetInfluences[0] = 0.5;
  let rendered = false;
  Haruki3DEngineCore.prototype.renderFrame.call({
    characterRoot: root, scene: root, camera: new THREE.PerspectiveCamera(),
    renderer: { render() {
      rendered = true;
      assert.deepEqual([...tangent.array], [2, 4, 6, -1]);
      assert.ok(tangent.version > 0, "attribute upload must be marked before render");
    } },
  });
  assert.ok(rendered);
  const source = new THREE.ShaderMaterial({
    vertexShader: '#include <common>\nvoid main(){\n#include <beginnormal_vertex>\n#include <defaultnormal_vertex>\n#include <begin_vertex>\ngl_Position = projectionMatrix * vec4(transformed, 1.0);\n}',
    fragmentShader: 'vec3 outputColor(vec3 color){return color;}\nvoid main(){gl_FragColor=vec4(outputColor(vec3(1.0)),1.0);}',
  });
  const outline = createSekaiOutlineMaterial(true, {}, true, null, source);
  assert.match(outline.vertexShader, /attribute vec4 tangent;/);
  assert.match(outline.vertexShader, /vec3 outlineTangent = tangent\.xyz;/);
  assert.match(outline.vertexShader, /cross\(outlineNormal, outlineTangent\) \* tangent\.w/);
});
