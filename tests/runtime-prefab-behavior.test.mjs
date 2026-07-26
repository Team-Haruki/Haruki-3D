import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";
import {
  applyUnityCharacterHeight,
  buildUnityPrefabSourceGraph,
  createUnityPrefabConstraintRuntime,
  installUnityRuntimeNativeMeshes,
  makeUnityPrefabHeadFollowDebugSnapshot,
  syncUnityPrefabSourceGraph,
} from "../dist/haruki-3d-engine-internal.js";

test("character height follows the official PositionNote scale boundary", () => {
  const extension = makeRuntimeExtension();
  extension.runtimeUnitySetup.prefabGraphs[0].transforms.push(
    transform(15, "body/Position", 1),
    transform(16, "body/Body", 1)
  );
  const graph = buildUnityPrefabSourceGraph(extension);

  assert.ok(graph);
  applyUnityCharacterHeight(graph, 1.68);

  assert.deepEqual(graph.root.scale.toArray(), [1, 1, 1]);
  assert.deepEqual(
    graph.nodeByPath.get("body/Position").scale.toArray(),
    [1.68, 1.68, 1.68]
  );
  assert.deepEqual(
    graph.nodeByPath.get("body/Body").scale.toArray(),
    [1, 1, 1]
  );
});

test("character height never applies a one-off body bundle scale override", () => {
  const extension = makeRuntimeExtension();
  extension.character = {
    characterHeightMeters: 1.68,
    bodyBundlePath:
      "live_pv/model/characterv2/body/99/0141/ladies_s.bundle",
  };
  extension.runtimeUnitySetup.prefabGraphs[0].transforms.push(
    transform(15, "body/Position", 1)
  );

  const graph = buildUnityPrefabSourceGraph(extension);

  assert.ok(graph);
  assert.deepEqual(graph.root.scale.toArray(), [1, 1, 1]);
  assert.deepEqual(graph.debug.sourceScaleCorrection, {
    characterHeightMeters: 1.68,
    characterModelScaleMeters: 1.68,
    scale: 1,
    reason: "master-character-height-via-position-note",
  });
});

test("raw Unity prefab rotations cross the coordinate boundary once", () => {
  const extension = makeRuntimeExtension();
  extension.runtimeUnitySetup.prefabGraphs[0].transforms[0].localRotation = {
    x: 0.5,
    y: 0.5,
    z: 0.5,
    w: 0.5,
  };

  const graph = buildUnityPrefabSourceGraph(extension);
  const body = graph.nodeByPath.get("body");

  assert.ok(body);
  assert.deepEqual(body.quaternion.toArray(), [0.5, -0.5, -0.5, 0.5]);
});

test("0414 prefab runtime applies official model combine and installs native meshes", () => {
  const extension = makeRuntimeExtension();
  const graph = buildUnityPrefabSourceGraph(extension);

  assert.ok(graph);
  const nativeMeshes = installUnityRuntimeNativeMeshes(graph, extension);
  assert.equal(graph.root.name, "UnityPrefabSourceRoot");
  assert.equal(nativeMeshes.error, null);
  assert.equal(nativeMeshes.meshCount, 2);
  assert.equal(nativeMeshes.skinnedMeshCount, 0);
  assert.deepEqual(nativeMeshes.skinBindings, []);

  const bodyRoot = graph.nodeByPath.get("body");
  const faceNeck = graph.nodeByPath.get("face/Neck");
  const faceHead = graph.nodeByPath.get("face/Neck/Head");
  const renderer = graph.nodeByPath.get("face/Face");
  const visorRenderer = graph.nodeByPath.get("face/Visor");
  const discardedTarget = graph.nodeByPath.get("body/hat_target");
  const discardedFaceControl = graph.nodeByPath.get("face/BS");
  const discardedFaceRoot = graph.nodeByPath.get("face");
  const drainedBodyChild = graph.nodeByPath.get(
    "body/Neck/Head/BodyChild"
  );

  assert.equal(graph.nodeByPath.get("body/Neck"), faceNeck);
  assert.equal(graph.nodeByPath.get("body/Neck/Head"), faceHead);
  assert.equal(graph.bodyAttach, faceNeck);
  assert.equal(graph.headRoot, faceNeck);
  assert.equal(graph.headOrigin, faceNeck);
  assert.equal(faceNeck.parent, bodyRoot);
  assert.deepEqual(faceNeck.position.toArray(), [-1, 2, 3]);
  assert.deepEqual(faceHead.position.toArray(), [-4, 5, 6]);
  assert.equal(renderer.parent, bodyRoot);
  assert.equal(visorRenderer.parent, bodyRoot);
  assert.equal(discardedTarget, undefined);
  assert.equal(discardedFaceControl, undefined);
  assert.equal(discardedFaceRoot, undefined);
  assert.equal(drainedBodyChild.parent, faceHead);

  const mesh = renderer.children.find((node) => node instanceof THREE.Mesh);
  assert.ok(mesh);
  assert.equal(mesh.name, "FaceMesh");
  assert.equal(mesh.geometry.getAttribute("position").count, 3);
  assert.equal(mesh.geometry.getAttribute("position").getX(1), 1);
  const material = Array.isArray(mesh.material) ? mesh.material[0] : mesh.material;
  assert.equal(material.userData.pjskMaterialKey, "face:0");
  assert.equal(graph.debug.active, true);
  assert.equal(graph.debug.sourcePath, "body/Neck");
  assert.equal(graph.debug.targetPath, "face/Neck");
  assert.deepEqual(graph.debug.assemblyCounts, {
    inputTransforms: 11,
    retainedTransforms: 6,
    removedTransforms: 5,
    capturedCommonRemovedTransforms: 14,
    removedAtLeastCapturedCommonCount: false,
  });
  assert.equal(graph.debug.keyNodes.modelCombineBodyNeck.destroyed, true);
  assert.equal(graph.debug.keyNodes.modelCombineFaceNeck.destroyed, false);
});

test("prefab runtime binds skinned morph meshes and applies exported constraints", () => {
  const extension = makeRuntimeExtension();
  extension.runtimeUnitySetup.constraintSetup = {
    version: "0414",
    sourceKind: "unity-prefab",
    constraints: [{
      type: "parent",
      ownerPath: "face/Face",
      sources: [{
        sourcePath: "body",
        weight: 1,
        translationOffset: { x: 1, y: 0, z: 0 },
      }],
    }],
  };
  const sourceMesh = extension.nativeMeshes.meshes[0];
  sourceMesh.bonePaths = ["face/Neck/Head"];
  sourceMesh.boneInverseBindMatrices = identityMatrix();
  sourceMesh.skinIndices = [
    0, 0, 0, 0,
    0, 0, 0, 0,
    0, 0, 0, 0,
  ];
  sourceMesh.skinWeights = [
    1, 0, 0, 0,
    1, 0, 0, 0,
    1, 0, 0, 0,
  ];
  sourceMesh.morphTargets = [{
    name: "smile",
    indices: [0],
    positionDeltas: [0.25, 0, 0],
    normalDeltas: [0, 0.1, 0],
  }];

  const graph = buildUnityPrefabSourceGraph(extension);
  const nativeMeshes = installUnityRuntimeNativeMeshes(graph, extension);
  const constraints = syncUnityPrefabSourceGraph(graph, extension, 2);
  const renderer = graph.nodeByPath.get("face/Face");
  const mesh = renderer.children.find((node) => node instanceof THREE.SkinnedMesh);

  assert.equal(nativeMeshes.error, null);
  assert.equal(nativeMeshes.skinnedMeshCount, 1);
  assert.deepEqual(nativeMeshes.skinBindings, [{
    meshName: "FaceMesh",
    partKind: "face",
    rendererTransformPath: "face/Face",
    rootBonePath: null,
    rootBoneResolved: false,
    effectiveRootBonePath: null,
    effectiveRootBoneResolved: false,
    boneCount: 1,
    restTranslation: { x: -5, y: 7, z: 9 },
    restScale: { x: 1, y: 1, z: 1 },
    restMatrixSpread: 0,
    restMatrixSpreadBonePath: null,
  }]);
  assert.ok(mesh);
  assert.equal(mesh.skeleton.bones[0], graph.nodeByPath.get("face/Neck/Head"));
  assert.deepEqual(mesh.skeleton.boneInverses[0].toArray(), identityMatrix());
  assert.equal(mesh.geometry.morphAttributes.position[0].name, "smile");
  assert.ok(
    Math.abs(mesh.geometry.morphAttributes.position[0].array[0] - 0.25) < 1e-6
  );
  assert.equal(constraints.appliedCount, 1);
  assert.deepEqual(renderer.position.toArray(), [-2, 0, 0]);
});

test("Unity bind poses do not apply a non-identity renderer transform twice", () => {
  const extension = makeRuntimeExtension();
  const faceRenderer = extension.runtimeUnitySetup.prefabGraphs[0].transforms
    .find((entry) => entry.transformPath === "face/Face");
  faceRenderer.localPosition = { x: 2, y: 3, z: 4 };

  const sourceMesh = extension.nativeMeshes.meshes[0];
  sourceMesh.bonePaths = ["face/Neck/Head"];
  sourceMesh.boneInverseBindMatrices = identityMatrix();
  sourceMesh.skinIndices = [
    0, 0, 0, 0,
    0, 0, 0, 0,
    0, 0, 0, 0,
  ];
  sourceMesh.skinWeights = [
    1, 0, 0, 0,
    1, 0, 0, 0,
    1, 0, 0, 0,
  ];

  const graph = buildUnityPrefabSourceGraph(extension);
  const nativeMeshes = installUnityRuntimeNativeMeshes(graph, extension);
  const renderer = graph.nodeByPath.get("face/Face");
  const mesh = renderer.children.find((node) => node instanceof THREE.SkinnedMesh);
  const bone = graph.nodeByPath.get("face/Neck/Head");

  assert.equal(nativeMeshes.error, null);
  assert.ok(mesh);
  assert.ok(bone);

  const skinnedVertexWorld = new THREE.Vector3()
    .fromBufferAttribute(mesh.geometry.getAttribute("position"), 0);
  mesh.applyBoneTransform(0, skinnedVertexWorld);
  mesh.localToWorld(skinnedVertexWorld);
  const unityExpectedWorld = bone.localToWorld(new THREE.Vector3());

  assert.ok(skinnedVertexWorld.distanceTo(unityExpectedWorld) < 1e-6);
  assert.deepEqual(
    mesh.skeleton.boneInverses[0].toArray().map((value) => Number(value.toFixed(6))),
    new THREE.Matrix4()
      .copy(mesh.bindMatrix)
      .invert()
      .toArray()
      .map((value) => Number(value.toFixed(6)))
  );
});

test("prefab debug snapshot preserves fallback state until a graph is loaded", () => {
  const fallback = {
    active: false,
    sourcePath: null,
    targetPath: null,
    reason: "not initialized",
  };
  const extension = { runtimeUnitySetup: { version: "0414" } };

  assert.deepEqual(
    makeUnityPrefabHeadFollowDebugSnapshot(null, extension, fallback),
    { ...fallback, setupVersion: "0414" }
  );

  const graph = buildUnityPrefabSourceGraph(makeRuntimeExtension());
  const snapshot = makeUnityPrefabHeadFollowDebugSnapshot(
    graph,
    extension,
    fallback
  );
  assert.equal(snapshot.active, true);
  assert.equal(snapshot.sourcePath, "body/Neck");
  assert.equal(snapshot.setupVersion, "0414");
  assert.deepEqual(snapshot.keyNodes.bodyNeck.worldPosition, { x: -1, y: 2, z: 3 });
  assert.deepEqual(snapshot.keyNodes.faceNeck.worldPosition, { x: -1, y: 2, z: 3 });
  assert.equal(snapshot.assemblyDistances.bodyNeckToFaceNeck, null);
  assert.equal(snapshot.assemblyDistances.bodyHeadToFaceHead, null);
});

test("persistent constraints retain resolved transform bindings between frames", () => {
  const extension = makeRuntimeExtension();
  extension.runtimeUnitySetup.constraintSetup = {
    version: "0414",
    sourceKind: "unity-prefab",
    constraints: [{
      type: "parent",
      ownerPath: "face/Face",
      sources: [{
        sourcePath: "body",
        weight: 1,
        translationOffset: { x: 1, y: 0, z: 0 },
      }],
    }],
  };
  const graph = buildUnityPrefabSourceGraph(extension);
  const body = graph.nodeByPath.get("body");
  const renderer = graph.nodeByPath.get("face/Face");
  const runtime = createUnityPrefabConstraintRuntime(graph, extension, 2);

  assert.ok(runtime);
  runtime.update();
  const before = renderer.getWorldPosition(new THREE.Vector3());
  graph.nodeByPath.clear();
  body.position.x = 3;
  body.updateMatrixWorld(true);

  const diagnostics = runtime.update();
  const after = renderer.getWorldPosition(new THREE.Vector3());

  assert.equal(diagnostics.appliedCount, 1);
  assert.ok(Math.abs(after.x - before.x - 3) < 1e-6);
});

function makeRuntimeExtension() {
  return {
    runtimeUnitySetup: {
      version: "0414",
      prefabGraphs: [{
        partKind: "combined",
        transforms: [
          transform(1, "body", null),
          transform(2, "body/Neck", 1, [1, 2, 3]),
          transform(3, "body/Neck/Head", 2, [4, 5, 6]),
          transform(4, "body/Neck/Head/BodyChild", 3),
          transform(5, "body/hat_target", 1),
          transform(10, "face", null),
          transform(11, "face/Neck", 10, [7, 8, 9]),
          transform(12, "face/Neck/Head", 11),
          transform(13, "face/BS", 10),
          transform(14, "face/Face", 10),
          transform(15, "face/Visor", 10),
        ],
      }],
      bodyHeadAssembly: {
        version: "0414",
        parentingMode: "model_combine_setup",
        parentRootPath: "body",
        parentAttachPath: "body/Neck",
        childRootPath: "face",
        childOriginPath: "face/Neck",
        parentCombineNodeAPath: "body/Neck",
        parentCombineNodeBPath: "body/Neck/Head",
        childCombineNodeAPath: "face/Neck",
        childCombineNodeBPath: "face/Neck/Head",
        faceRendererName: "Face",
        childMoveSuffix: "_target",
      },
    },
    nativeMeshes: {
      version: "0414",
      meshes: [{
        partKind: "face",
        meshPath: "face/Face/FaceMesh",
        meshName: "FaceMesh",
        rendererTransformPath: "face/Face",
        positions: [0, 0, 0, 1, 0, 0, 0, 1, 0],
        normals: [0, 0, 1, 0, 0, 1, 0, 0, 1],
        uv0: [0, 0, 1, 0, 0, 1],
        submeshes: [{
          slotIndex: 0,
          materialKey: "face:0",
          materialName: "FaceMaterial",
          indices: [0, 1, 2],
        }],
      }, {
        partKind: "face",
        meshPath: "face/Visor/VisorMesh",
        meshName: "VisorMesh",
        rendererTransformPath: "face/Visor",
        positions: [0, 0, 0, 1, 0, 0, 0, 1, 0],
        normals: [0, 0, 1, 0, 0, 1, 0, 0, 1],
        uv0: [0, 0, 1, 0, 0, 1],
        submeshes: [{
          slotIndex: 0,
          materialKey: "face:1",
          materialName: "VisorMaterial",
          indices: [0, 1, 2],
        }],
      }],
    },
  };
}

function transform(pathId, transformPath, parentPathId, position = [0, 0, 0]) {
  return {
    pathId,
    name: transformPath.split("/").at(-1),
    transformPath,
    parentPathId,
    localPosition: { x: position[0], y: position[1], z: position[2] },
    localRotation: { x: 0, y: 0, z: 0, w: 1 },
    localScale: { x: 1, y: 1, z: 1 },
  };
}

function identityMatrix() {
  return [
    1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1,
  ];
}
