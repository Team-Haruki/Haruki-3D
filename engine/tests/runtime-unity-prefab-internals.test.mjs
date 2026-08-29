import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import { unityPrefabRuntimeInternals as prefab } from "../dist/haruki-3d-engine-internal.js";

test("prefab readers accept direct and nested 0414 payload aliases", () => {
  assert.deepEqual(prefab.asRecord(null), {});
  assert.deepEqual(prefab.asRecord({ a: 1 }), { a: 1 });
  assert.equal(prefab.readRuntimeNumber(2), 2);
  assert.equal(prefab.readRuntimeNumber(Infinity), null);
  assert.equal(prefab.readRuntimeUnitySetup0414({ runtimeUnitySetup: { version: "0414" } }).version, "0414");
  assert.equal(prefab.readRuntimeUnitySetup0414({ PjskSpringBone: { RuntimeUnitySetup: { version: 414 } } }).version, 414);
  assert.equal(prefab.readRuntimeUnitySetup0414({ runtimeUnitySetup: { version: 1 } }), null);
  assert.equal(prefab.readRuntimeNativeMeshSet0414({ nativeMeshes: { version: "0414" } }).version, "0414");
  assert.equal(prefab.readRuntimeNativeMeshSet0414({ NativeMeshes: { version: 414 } }).version, 414);
  assert.equal(prefab.readRuntimeNativeMeshSet0414({}), null);
  assert.equal(prefab.readRuntimeUnitySetupVersion({ runtimeUnitySetup: { Version: 414 } }), "414");
  assert.equal(prefab.readRuntimeUnitySetupVersion({ pjskSpringBone: { runtimeUnitySetup: {} } }), "");
});

test("graph lookup and parent operations cover absent, duplicate, drain, detach, and alias paths", () => {
  const root = new THREE.Group();
  root.name = "root";
  const parentA = new THREE.Group();
  const parentB = new THREE.Group();
  parentA.name = "a";
  parentB.name = "b";
  root.add(parentA, parentB);
  const child = new THREE.Group();
  child.name = "child";
  const grandchild = new THREE.Group();
  grandchild.name = "grandchild";
  child.add(grandchild);
  parentA.add(child);
  const paths = new Map([["a/child", child], ["alias", child], ["a/child/grandchild", grandchild]]);
  const ids = new Map([[1, child], [2, grandchild]]);
  assert.equal(prefab.resolvePrefabGraphNode(paths, [null, "missing", "a/child"]).node, child);
  assert.equal(prefab.resolvePrefabGraphNode(paths, ["missing"]), null);
  assert.equal(prefab.resolvePrefabNodeCandidate(paths, ["missing", "alias"]).node, child);
  assert.equal(prefab.resolvePrefabNodeCandidate(paths, []), null);
  assert.equal(prefab.isModelCombineSetupAssembly({ parentingMode: "model_combine_setup" }), true);
  assert.equal(prefab.isModelCombineSetupAssembly({ parentingMode: "other" }), false);

  prefab.setParentKeepingLocal(child, parentB);
  assert.equal(child.parent, parentB);
  const one = new THREE.Group();
  const two = new THREE.Group();
  parentA.add(one, two);
  prefab.drainChildrenKeepingLocal(parentA, parentB);
  assert.equal(parentA.children.length, 0);
  const moved = prefab.moveFaceRendererTransforms(paths, ["missing", "a/child", "alias"], parentA);
  assert.deepEqual(moved, ["a/child"]);
  prefab.replacePathIdNodeReferences(ids, child, parentB);
  assert.equal(ids.get(1), parentB);
  prefab.detachRuntimeSubtree(child, paths, ids);
  assert.equal(child.parent, null);
  assert.equal(paths.has("a/child"), false);
  assert.equal(paths.has("a/child/grandchild"), false);
  assert.equal(child.userData.pjskModelCombineDestroyed, true);
});

test("head renderer and body-root discovery distinguish skinned and destroyed static renderers", () => {
  const extension = {
    runtimeUnitySetup: {
      version: "0414",
      bodyHeadAssembly: { childRootPath: "face" },
      prefabGraphs: [{ renderers: [
        { transformPath: "face/Face", typeName: "SkinnedMeshRenderer" },
        { transformPath: "face/Static", typeName: "MeshRenderer" },
        { transformPath: "body/Body", typeName: "SkinnedMeshRenderer" },
        { transformPath: "face/Face", typeName: "SkinnedMeshRenderer" },
      ] }],
    },
    nativeMeshes: { version: "0414", meshes: [
      { rendererTransformPath: "body/Mesh", rootBonePath: "body/Hip" },
    ] },
  };
  assert.deepEqual(prefab.collectOfficialHeadRendererPaths(extension, "face"), ["face/Face"]);
  assert.equal(prefab.isDestroyedStaticFaceRenderer(extension, { rendererTransformPath: "face/Static" }), true);
  assert.equal(prefab.isDestroyedStaticFaceRenderer(extension, { rendererTransformPath: "face/Face" }), false);
  assert.equal(prefab.isDestroyedStaticFaceRenderer(extension, { rendererTransformPath: "body/Body" }), false);
  assert.equal(prefab.isDestroyedStaticFaceRenderer({}, { rendererTransformPath: "face/Static" }), false);
  assert.equal(prefab.resolveOfficialBodyRootBone(extension, "body"), "body/Hip");
  assert.equal(prefab.resolveOfficialBodyRootBone({}, "body"), null);
});

test("prefab instance resolution detects missing parents, cycles, and conflicting native roots", () => {
  const rootA = { pathId: 1, runtimePartIndex: 0, transformPath: "body" };
  const childA = { pathId: 2, parentPathId: 1, runtimePartIndex: 0, transformPath: "body/Mesh" };
  const orphan = { pathId: 3, parentPathId: 999, runtimePartIndex: 0, transformPath: "body/Orphan" };
  const sources = new Map([[1, rootA], [2, childA], [3, orphan]]);
  assert.equal(prefab.resolvePrefabInstanceRoot(childA, sources), rootA);
  assert.equal(prefab.resolvePrefabInstanceRoot(orphan, sources), orphan);
  assert.equal(prefab.prefabInstanceKey(childA), "0:body");
  assert.equal(prefab.prefabInstanceKey({}), null);

  const cycleA = { pathId: 4, parentPathId: 5, transformPath: "face/A" };
  const cycleB = { pathId: 5, parentPathId: 4, transformPath: "face/B" };
  assert.throws(() => prefab.resolvePrefabInstanceRoot(cycleA, new Map([[4, cycleA], [5, cycleB]])), /parent cycle/);

  const extension = { nativeMeshes: { version: "0414", meshes: [
    { rendererTransformPathId: 2 }, { rendererTransformPathId: null }, { rendererTransformPathId: 999 },
  ] } };
  assert.deepEqual([...prefab.resolvePreferredPrefabRoots(extension, sources)], [["0:body", 1]]);
  const rootB = { pathId: 6, runtimePartIndex: 0, transformPath: "body" };
  const childB = { pathId: 7, parentPathId: 6, runtimePartIndex: 0, transformPath: "body/Mesh2" };
  const conflictSources = new Map([...sources, [6, rootB], [7, childB]]);
  assert.throws(() => prefab.resolvePreferredPrefabRoots({ nativeMeshes: { version: "0414", meshes: [
    { rendererTransformPathId: 2 }, { rendererTransformPathId: 7 },
  ] } }, conflictSources), /multiple Unity prefab instances/);
});

test("prefab indexing builds part nodes, preferred paths, hierarchy, carrier bindings, and scale metadata", () => {
  const setup = { prefabGraphs: [{ transforms: [
    { pathId: 1, name: "body", transformPath: "body", runtimePartIndex: 0, childPathIds: [2] },
    { pathId: 2, parentPathId: 1, transformPath: "body/Mesh", runtimePartIndex: 0,
      localPosition: { x: 1, y: 2, z: 3 }, localRotation: { x: 0, y: 0, z: 0, w: 1 },
      localScale: { x: 2, y: 3, z: 4 } },
    { pathId: 3, transformPath: "body", runtimePartIndex: 0 },
    { pathId: "bad", transformPath: "bad" }, { pathId: 4 },
  ] }] };
  const indexed = prefab.indexPrefabTransformSources(setup);
  assert.equal(indexed.sourceByPathId.size, 3);
  assert.equal(indexed.pathCounts.get("body"), 2);
  const nodes = prefab.buildPrefabTransformNodes(setup, indexed.sourceByPathId, new Map([["0:body", 1]]));
  assert.equal(nodes.nodeByPathId.size, 3);
  assert.equal(nodes.nodeByPath.get("body").userData.pjskRuntimePartIndex, 0);
  assert.equal(nodes.nodeByPath.get("body"), nodes.nodeByPathId.get(1));
  prefab.attachPrefabTransformNodes(new THREE.Group(), nodes.nodeByPathId, indexed.sourceByPathId);
  assert.equal(nodes.nodeByPathId.get(2).parent, nodes.nodeByPathId.get(1));
  const before = nodes.nodeByPathId.size;
  prefab.addPrefabTransformNode({}, indexed.sourceByPathId, new Map(), nodes.nodeByPathId, nodes.nodeByPath);
  assert.equal(nodes.nodeByPathId.size, before);

  const carrier = new THREE.Group();
  const carrierBody = new THREE.Group();
  carrierBody.name = "body";
  const carrierMesh = new THREE.Group();
  carrierMesh.name = "Mesh";
  carrierBody.add(carrierMesh);
  carrier.add(carrierBody);
  assert.equal(prefab.buildMeshCarrierBindings(nodes.nodeByPath, null).length, 0);
  assert.ok(prefab.buildMeshCarrierBindings(nodes.nodeByPath, carrier).length >= 1);
  assert.equal(prefab.countRuntimeTransforms(carrier), 2);
  assert.deepEqual(prefab.resolveUnityPrefabSourceScaleCorrection({ character: { characterHeightMeters: 1.7 } }), {
    characterHeightMeters: 1.7, scale: 1, reason: "presentation-module-applies-position-scale",
  });
  assert.equal(prefab.resolveUnityPrefabSourceScaleCorrection({ BodyManifest: { CharacterHeightMeters: "bad" } }).characterHeightMeters, null);
});

test("native mesh validation reports ambiguity, mismatched ids, missing parents, and material identity", () => {
  const root = new THREE.Group();
  const parent = new THREE.Group();
  root.add(parent);
  const graph = {
    root,
    nodeByPath: new Map([["body/Mesh", parent], ["body/Bone", new THREE.Bone()]]),
    nodeByPathId: new Map([[1, parent], [2, new THREE.Bone()]]),
    ambiguousPaths: new Set(["ambiguous"]), headRendererPaths: [],
    bodyRootBone: null, bodyRootBonePath: null,
  };
  const warnings = [];
  const fatal = [];
  assert.equal(prefab.nativeMeshLabel({ meshPath: "path" }), "path");
  assert.equal(prefab.nativeMeshLabel({ meshName: "name" }), "name");
  assert.equal(prefab.nativeMeshLabel({}), "<unnamed>");
  assert.equal(prefab.validateNativeMeshBindingSource(graph, {
    rendererTransformPath: "ambiguous", bonePaths: [],
  }, [], [], warnings, fatal), false);
  assert.equal(prefab.validateNativeMeshBindingSource(graph, {}, ["a", "b"], [1], warnings, fatal), false);
  assert.equal(prefab.validateNativeMeshBindingSource(graph, {}, [], [], warnings, fatal), true);
  assert.equal(prefab.resolveNativeMeshParent(graph, { rendererTransformPathId: 1 }, warnings, fatal), parent);
  assert.equal(prefab.resolveNativeMeshParent(graph, { rendererTransformPath: "body/Mesh" }, warnings, fatal), parent);
  assert.equal(prefab.resolveNativeMeshParent(graph, { rendererTransformPath: "missing" }, warnings, fatal), null);
  prefab.resolveNativeMeshParent(graph, { rendererTransformPathId: 999, rendererTransformPath: "missing" }, warnings, fatal);
  assert.ok(fatal.length >= 2);

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("color", new THREE.Float32BufferAttribute([1, 1, 1, 1], 4));
  assert.throws(() => prefab.buildNativeMeshMaterials({ submeshes: [{}] }, geometry), /material identity/);
  const materials = prefab.buildNativeMeshMaterials({ meshName: "mesh", submeshes: [
    { materialKey: "key", slotIndex: 0, materialName: "mat" },
  ] }, geometry);
  assert.equal(materials[0].vertexColors, true);
  assert.equal(materials[0].userData.pjskMaterialKey, "key");
  assert.equal(prefab.buildNativeMeshMaterials({}, geometry).length, 1);
  assert.equal(prefab.resolveNativeMeshBones(graph, ["body/Bone"], []).length, 1);
  assert.equal(prefab.resolveNativeMeshBones(graph, ["ignored"], [2]).length, 1);
});

test("native geometry covers optional attributes, submeshes, morphs, invalid deltas, and index guards", () => {
  assert.equal(prefab.buildUnityRuntimeNativeGeometry({ positions: [] }), null);
  assert.equal(prefab.buildUnityRuntimeNativeGeometry({ positions: [1, 2] }), null);
  const source = {
    positions: [0, 0, 0, 1, 0, 0],
    normals: [0, 1, 0, 0, 1, 0], tangents: [1, 0, 0, 1, 1, 0, 0, 1],
    uv0: [0, 0, 1, 0], uv1: [0, 0, 1, 0], uv2: [0, 0, 1, 0],
    colors: [1, 1, 1, 1, 1, 1, 1, 1],
    skinIndices: [0, 0, 0, 0, 0, 0, 0, 0], skinWeights: [1, 0, 0, 0, 1, 0, 0, 0],
    submeshes: [{ indices: [0, 1] }, {}],
    morphTargets: [
      { name: "ok", indices: [0, 9], positionDeltas: [1, 2, 3, 4, 5, 6], normalDeltas: [0, 1, 0, 0, 1, 0] },
      { indices: [], positionDeltas: [] },
      { indices: [0], positionDeltas: [1] },
      { name: "no-normal", indices: [1], positionDeltas: [1, 1, 1] },
    ],
  };
  const geometry = prefab.buildUnityRuntimeNativeGeometry(source);
  for (const name of ["position", "normal", "tangent", "uv", "uv1", "uv2", "color", "skinIndex", "skinWeight"]) {
    assert.ok(geometry.hasAttribute(name));
  }
  assert.equal(geometry.groups.length, 2);
  assert.equal(geometry.morphAttributes.position.length, 2);
  assert.equal(geometry.morphAttributes.normal, undefined);

  const output = new Float32Array(6);
  prefab.copyNativeMorphDelta(undefined, 0, 2, [1, 2, 3], output);
  prefab.copyNativeMorphDelta(-1, 0, 2, [1, 2, 3], output);
  prefab.copyNativeMorphDelta(2, 0, 2, [1, 2, 3], output);
  prefab.copyNativeMorphDelta(1, 0, 2, [1, undefined, 3], output);
  assert.deepEqual([...output], [0, 0, 0, 1, 0, 3]);

  const emptyGeometry = new THREE.BufferGeometry();
  prefab.addFloatGeometryAttribute(emptyGeometry, "normal", [1, 2], 3, 1);
  prefab.addUint16GeometryAttribute(emptyGeometry, "skinIndex", [1, 2], 4, 1);
  assert.equal(emptyGeometry.hasAttribute("normal"), false);
  assert.equal(emptyGeometry.hasAttribute("skinIndex"), false);
});

test("bind matrices, mounting, rest spread, and debug path helpers cover edge cases", () => {
  const parent = new THREE.Group();
  const mesh = new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshBasicMaterial());
  prefab.prepareAndMountNativeMesh(mesh, "mesh", { partKind: "Body", rendererPathId: 7 }, parent);
  assert.equal(mesh.name, "mesh");
  assert.equal(mesh.userData.pjskNativeUnityMesh, true);
  assert.equal(mesh.frustumCulled, false);
  assert.equal(mesh.parent, parent);

  const identityValues = [...new THREE.Matrix4().elements];
  const warnings = [];
  assert.deepEqual(prefab.buildUnityRuntimeBoneInverseBindMatrices({}, 0, warnings), []);
  assert.deepEqual(prefab.buildUnityRuntimeBoneInverseBindMatrices({ boneInverseBindMatrices: [] }, 1, warnings), []);
  assert.deepEqual(prefab.buildUnityRuntimeBoneInverseBindMatrices({ boneInverseBindMatrices: [1, 2] }, 1, warnings), []);
  assert.equal(warnings.length, 1);
  const matrices = prefab.buildUnityRuntimeBoneInverseBindMatrices({ boneInverseBindMatrices: identityValues }, 1, warnings);
  prefab.convertUnityBindMatricesToThree([], new THREE.Matrix4());
  prefab.convertUnityBindMatricesToThree(matrices, new THREE.Matrix4().makeTranslation(1, 0, 0));
  assert.equal(matrices.length, 1);

  const root = new THREE.Group();
  const boneA = new THREE.Bone();
  const boneB = new THREE.Bone();
  boneA.name = "Bone_1";
  boneB.name = "Bone_2";
  boneB.position.set(1, 0, 0);
  boneB.userData.pjskTransformPath = "body/Bone";
  root.add(boneA, boneB);
  root.updateMatrixWorld(true);
  assert.deepEqual(prefab.measureSkinRestMatrixSpread([boneA], [new THREE.Matrix4()]), {
    restMatrixSpread: 0, restMatrixSpreadBonePath: null,
  });
  const spread = prefab.measureSkinRestMatrixSpread(
    [boneA, boneB], [new THREE.Matrix4(), new THREE.Matrix4()]
  );
  assert.equal(spread.restMatrixSpread, 1);
  assert.equal(spread.restMatrixSpreadBonePath, "body/Bone");
  assert.ok(prefab.makeSkinRestTransform(boneA, new THREE.Matrix4()).restScale);

  const graph = { nodeByPath: new Map([["bone", boneA]]), nodeByPathId: new Map([[1, boneA]]) };
  assert.equal(prefab.resolveNativeRootBoneStatus(graph, { rootBonePathId: 1 }), true);
  assert.equal(prefab.resolveNativeRootBoneStatus(graph, { rootBonePathId: 2 }), false);
  assert.equal(prefab.resolveNativeRootBoneStatus(graph, { rootBonePath: "bone" }), true);
  assert.equal(prefab.resolveNativeRootBoneStatus(graph, {}), false);

  assert.equal(prefab.stripThreeDuplicateSuffix("Bone_12"), "Bone");
  assert.equal(prefab.stripThreeDuplicateSuffix("Bone"), "Bone");
  assert.equal(prefab.buildObjectPath(boneB, root), "Bone_2");
  assert.equal(prefab.buildObjectPath(boneB, root, true), "Bone");
  assert.deepEqual(prefab.vectorDebugSnapshot(new THREE.Vector3(1.234567, 2, 3)), { x: 1.23457, y: 2, z: 3 });
  assert.deepEqual(prefab.quaternionDebugSnapshot(new THREE.Quaternion()), { x: 0, y: 0, z: 0, w: 1 });
  assert.equal(prefab.makePrefabNodeDebug(boneB, root).parentPath, null);

  const body = new THREE.Group();
  body.name = "body";
  const position = new THREE.Group();
  position.name = "Position";
  body.add(position);
  const face = new THREE.Group();
  face.name = "face";
  const facePosition = new THREE.Group();
  facePosition.name = "Position";
  face.add(facePosition);
  const model = new THREE.Group();
  model.name = "mdl_chr_test";
  const modelPosition = new THREE.Group();
  modelPosition.name = "Position";
  model.add(modelPosition);
  root.add(body, face, model, new THREE.Group());
  assert.equal(prefab.collectPrefabPositionRootDebug(root).length, 3);
});
