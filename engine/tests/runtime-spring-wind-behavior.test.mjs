import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";
import { UnityPrefabSpringRuntime } from "../dist/haruki-3d-engine-internal.js";

test("shared wind provider advances inside each official GetForceOnBone call", () => {
  const root = new THREE.Group();
  const wind = addNode(root, "wind");
  const boneA = addNode(root, "boneA");
  const boneB = addNode(root, "boneB");
  wind.rotation.y = 0.2;

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(
    makeWindRuntimeExtension(),
    root
  );

  assert.ok(runtime);
  runtime.update(0.125);

  assert.ok(boneA.quaternion.angleTo(new THREE.Quaternion()) > 1e-6);
  assert.ok(boneB.quaternion.angleTo(new THREE.Quaternion()) > 1e-6);
  assert.ok(boneA.quaternion.angleTo(boneB.quaternion) > 1e-6);
});

test("active late-update wind also advances per manager bone", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  const boneA = addNode(root, "boneA");
  const boneB = addNode(root, "boneB");
  const extension = makeWindRuntimeExtension();
  extension.pjskSpringBone.runtimeUnitySetup.managers[0].forceProviders[0].raw.isActive = true;

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(extension, root);

  assert.ok(runtime);
  runtime.update(0.125);
  assert.ok(boneA.quaternion.angleTo(new THREE.Quaternion()) > 1e-6);
  assert.ok(boneA.quaternion.angleTo(boneB.quaternion) > 1e-6);
});

test("clearing timeline controls restores official per-bone values and manager defaults", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  addNode(root, "boneA");
  addNode(root, "boneB");
  const extension = makeWindRuntimeExtension();
  const manager = extension.pjskSpringBone.runtimeUnitySetup.managers[0];
  manager.slowMotionScale = 0.75;
  manager.isPaused = true;

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(extension, root);

  assert.ok(runtime);
  runtime.setTimelineControl({
    stiffnessForce: 50,
    dragForce: 0.8,
    windInfluence: 0.25,
    slowMotionScale: 0.4,
    paused: false,
  });
  let snapshot = runtime.getSnapshot();
  assert.equal(snapshot.topOffsets[0].stiffnessForce, 50);
  assert.equal(snapshot.topOffsets[0].slowMotionScale, 0.4);
  assert.equal(snapshot.topOffsets[0].bonePaused, false);

  runtime.clearTimelineControl();
  snapshot = runtime.getSnapshot();
  assert.equal(snapshot.topOffsets[0].stiffnessForce, 0);
  assert.equal(snapshot.topOffsets[0].slowMotionScale, 1);
  assert.equal(snapshot.topOffsets[0].bonePaused, false);
});

test("spring reset preserves serialized local bone scale", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  const boneA = addNode(root, "boneA");
  addNode(root, "boneB");
  boneA.scale.set(1.2, 0.9, 1.1);

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(
    makeWindRuntimeExtension(),
    root
  );

  assert.ok(runtime);
  boneA.scale.setScalar(2);
  runtime.resetPose();
  assert.deepEqual(boneA.scale.toArray(), [1.2, 0.9, 1.1]);
});

test("official includeInactive spring discovery retains serialized inactive children", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  addNode(root, "boneA");
  addNode(root, "boneB");
  const extension = makeWindRuntimeExtension();
  const setup = extension.pjskSpringBone.runtimeUnitySetup;
  setup.managers[0].activeSelf = false;
  setup.bones[0].activeInHierarchy = false;

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(extension, root);

  assert.ok(runtime);
  assert.equal(runtime.getSnapshot().boneCount, 2);
});

test("prefab component identity prevents a static transform from becoming a spring bone", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  addNode(root, "boneA");
  addNode(root, "boneB");
  const extension = makeWindRuntimeExtension();
  extension.pjskSpringBone.runtimeUnitySetup.prefabGraphs[0].monoBehaviours = [
    {
      pathId: 10,
      scriptName: "ExtraBone",
      transformPath: "boneA",
    },
    {
      pathId: 20,
      scriptName: "SekaiSpringBone",
      transformPath: "boneB",
    },
  ];

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(extension, root);

  assert.ok(runtime);
  const snapshot = runtime.getSnapshot();
  assert.equal(snapshot.boneCount, 1);
  assert.equal(snapshot.topOffsets[0].name, "boneB");
  assert.equal(snapshot.setupDiagnostics.officialSpringComponentCount, 1);
  assert.equal(snapshot.setupDiagnostics.rejectedUnverifiedBoneSourceCount, 1);
});

test("serialized Unity spring force crosses into Three space exactly once", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  addNode(root, "boneA");
  addNode(root, "boneB");
  const extension = makeWindRuntimeExtension();
  extension.pjskSpringBone.runtimeUnitySetup.bones[0].rawSpringForce = {
    x: 2,
    y: 3,
    z: 4,
  };

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(
    extension,
    root
  );

  assert.ok(runtime);
  const boneA = runtime.getSnapshot().topOffsets.find(
    (entry) => entry.name === "boneA"
  );
  assert.ok(boneA);
  assert.equal(boneA.springForce.x, -2);
  assert.equal(boneA.springForce.y, 3);
  assert.equal(boneA.springForce.z, 4);
});

test("spinning Unity wind uses the mirrored local right axis", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  addNode(root, "boneA");
  addNode(root, "boneB");
  const extension = makeWindRuntimeExtension();
  extension.pjskSpringBone.runtimeUnitySetup.managers[0].forceProviders[0].raw.spinPeriod = 1;

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(
    extension,
    root
  );

  assert.ok(runtime);
  runtime.setTraceBoneFilters(["boneA"]);
  runtime.update(0);
  const trace = runtime.getTraceSnapshot().events[0];
  assert.ok(trace);
  assert.ok(trace.externalForce.x < 0);
});

test("wind phase evaluates the original Unity local X coordinate", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  const boneA = addNode(root, "boneA");
  addNode(root, "boneB");
  // Viewer -X is Unity +X after the import mirror.
  boneA.position.x = -0.25;

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(
    makeWindRuntimeExtension(),
    root
  );

  assert.ok(runtime);
  runtime.setTraceBoneFilters(["boneA"]);
  runtime.update(0);
  const trace = runtime.getTraceSnapshot().events[0];
  assert.ok(trace);
  assert.ok(trace.externalForce.y > 0.3);
});

function makeWindRuntimeExtension() {
  return {
    pjskSpringBone: {
      runtimeUnitySetup: {
        version: "0414",
        prefabGraphs: [{
          transforms: [
            transform(1, "boneA"),
            transform(2, "boneB"),
            transform(3, "wind"),
          ],
        }],
        managers: [{
          pathId: 100,
          nodeName: "shared-wind-manager",
          automaticUpdates: true,
          isSumOfForcesOnBone: true,
          simulationFrameRate: 60,
          rawGravity: { x: 0, y: 0, z: 0 },
          bonePathIds: [10, 20],
          forceProviders: [{
            sourcePathId: 500,
            scriptName: "WindVolumeOneSelf",
            nodePath: "wind",
            springManagerPathId: 100,
            raw: {
              m_Enabled: true,
              isActive: false,
              weight: 1,
              strength: 1,
              period: 1,
              currentTime: 0,
              spinPeriod: 0,
              amplitude: 0.5,
              peakDistance: 1,
            },
          }],
        }],
        bones: [
          springBone(10, "boneA"),
          springBone(20, "boneB"),
        ],
      },
    },
  };
}

function transform(pathId, transformPath) {
  return {
    pathId,
    name: transformPath,
    transformPath,
    childPathIds: [],
    localPosition: { x: 0, y: 0, z: 0 },
  };
}

function springBone(pathId, nodePath) {
  return {
    pathId,
    nodeName: nodePath,
    nodePath,
    rawStiffnessForce: 0,
    rawDragForce: 0,
    rawWindInfluence: 1,
    rawSpringForce: { x: 0, y: 0, z: 0 },
  };
}

function addNode(root, name) {
  const node = new THREE.Group();
  node.name = name;
  root.add(node);
  return node;
}
