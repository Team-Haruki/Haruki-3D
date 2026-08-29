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

test("spring runtime traces skirt constraints and all official collider shapes", () => {
  const root = new THREE.Group();
  addNode(root, "wind");
  const skirt = addNode(root, "skirtBone");
  skirt.position.set(0, 0.2, 0);
  const pivot = addNode(root, "pivot");
  const target = addNode(root, "target");
  target.position.set(0.2, 0.2, 0);
  for (const name of ["sphere", "capsule", "panel"]) {
    addNode(root, name);
  }

  const extension = makeWindRuntimeExtension();
  const setup = extension.pjskSpringBone.runtimeUnitySetup;
  setup.prefabGraphs[0].transforms = [transform(1, "skirtBone")];
  setup.managers[0].bonePathIds = [10];
  setup.managers[0].forceProviders = [];
  setup.managers[0].collideWithGround = true;
  setup.managers[0].groundHeight = 0;
  setup.managers[0].enableLengthLimits = true;
  setup.managers[0].enableAngleLimits = true;
  setup.managers[0].enableCollision = true;
  setup.bones = [{
    ...springBone(10, "skirtBone"),
    runtimePartIndex: 4,
    runtimePartType: "hair",
    pivotNodeName: "pivot",
    pivotNodePath: "pivot",
    lengthLimitTargets: [{ nodePath: "target" }],
    hitRadius: 0.05,
    rawAngleLimits: {
      y: { active: true, min: -5, max: 5 },
      z: { active: true, min: -5, max: 5 },
    },
  }];
  setup.colliders = [
    { index: 1, pathId: 101, nodePath: "sphere", nodeName: "sphere", shape: {
      sphere: { radius: 0.1, offset: { x: 0, y: 0, z: 0 } },
    } },
    { index: 2, pathId: 102, nodePath: "capsule", nodeName: "capsule", shape: {
      capsule: { radius: 0.1, offset: { x: 0, y: 0, z: 0 }, tail: { x: 0, y: 0.3, z: 0 } },
    } },
    { index: 3, pathId: 103, nodePath: "panel", nodeName: "panel", shape: {
      panel: { width: 0.4, height: 0.4 },
    } },
  ];
  setup.colliderBindings = [{
    sourceSpringBonePathId: 10,
    sourceKind: "direct",
    colliders: [1, 2, 3],
  }];

  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension(extension, root);
  assert.ok(runtime);
  assert.deepEqual([...runtime.getControlledTrackNodeNames()], ["skirtBone"]);
  runtime.setTraceBoneFilters(["  SKIRT  "], 1);
  runtime.setTimelineControl({ paused: true });
  runtime.update(1 / 60);
  runtime.setTimelineControl({ paused: false, slowMotionScale: 0.5 });
  skirt.quaternion.setFromAxisAngle(new THREE.Vector3(0, 1, 0), 0.1);
  runtime.update(1 / 60);
  runtime.settleCurrentPose(2, 1 / 120);
  runtime.resetStateToCurrentPose();

  const trace = runtime.getTraceSnapshot();
  assert.equal(trace.filters[0], "skirt");
  assert.equal(trace.eventCount, 1);
  assert.equal(trace.events[0].colliderCount, 3);
  assert.equal(trace.events[0].angleLimit.enabled, true);
  const snapshot = runtime.getSnapshot(false, { springDebugAllOffsets: true });
  assert.equal(snapshot.enabled, false);
  assert.equal(snapshot.colliderCount, 3);
  assert.equal(snapshot.skirtOffsets.length, 1);
  assert.equal(snapshot.debugOffsets.length, 1);
  assert.equal(snapshot.controlledPartCounts[0].runtimePartIndex, 4);
  assert.equal(snapshot.controlledHairSamples[0].name, "skirtBone");
  assert.equal(pivot.name, "pivot");
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
