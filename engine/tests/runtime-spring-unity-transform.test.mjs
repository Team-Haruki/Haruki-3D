import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";
import { computeUtjLocalRotation } from "../dist/haruki-3d-engine-internal.js";
import { UnityPrefabSpringRuntime, checkLocalSphereCollisionAndReact } from "../dist/haruki-3d-engine-internal.js";

// Unity 2022.3.62f2 Transform.rotation oracle, nonuniform parent scale (2,1,.5).
// Unlike matrix decomposition, Unity composes the hierarchy's local quaternions.
const UNITY_ROTATION = [0.11964727193117142, 0.25785890221595766, 0.1324305683374405, 0.9495553970336914];
const vector = value => new THREE.Vector3(value.x, value.y, value.z);

test("SpringBone preserves Unity FromToRotation's near-parallel identity branch", () => {
  // Unity 2022.3.62f2 API sweep: 0.07 degrees is identity; 0.09 is not.
  const cases = [
    { target: [0.9999992251396179, 0.0012217301409691572, 0], rotation: [0, 0, 0, 1] },
    { target: [0.9999987483024597, 0.00157079566270113, 0], rotation: [0, 0, 0.0007853981805965304, 0.9999997019767761] },
  ];
  for (const row of cases) {
    const actual = computeUtjLocalRotation(new THREE.Vector3(), new THREE.Vector3(...row.target),
      new THREE.Quaternion(), new THREE.Quaternion(), new THREE.Vector3(1, 0, 0));
    const expected = new THREE.Quaternion(...row.rotation).normalize();
    assert.ok(actual.toArray().every((value, i) => Math.abs(value - expected.toArray()[i]) < 1e-7));
  }
});

test("SpringBone retains both native cutoff branches after float32 normalization", () => {
  // Unity parses float32 arguments and normalizes them again inside the API.
  // Rounding just the unnormalized dot product chooses the wrong branch.
  const from = new THREE.Vector3(.9823489909004899, .1718866516019779, .07379321837302918);
  const to = new THREE.Vector3(.9825313747844168, .1714027388004474, .0724789534689161);
  const actual = computeUtjLocalRotation(new THREE.Vector3(), to,
    new THREE.Quaternion(), new THREE.Quaternion(), from);
  assert.deepEqual(actual.toArray(), [0, 0, 0, 1]);
  const other = computeUtjLocalRotation(new THREE.Vector3(),
    new THREE.Vector3(.9966714358464652, .05255198556864512, -.06232445571794574),
    new THREE.Quaternion(), new THREE.Quaternion(),
    new THREE.Vector3(.9966529478898056, .05161222858626568, -.06339620905795057));
  const expected = new THREE.Quaternion(.000057446202845312655, -.0005346686812117696,
    .0004678295226767659, .9999997615814209).normalize();
  assert.ok(other.clone().normalize().angleTo(expected) < 1e-6, "the other side of the cutoff must still rotate");
});

function fixture() {
  const root = new THREE.Group();
  root.scale.set(2, 1, .5);
  const parent = new THREE.Group();
  parent.name = "parent";
  parent.quaternion.set(.1196472663, .2578588953, .1324305474, .9495554075).normalize();
  root.add(parent);
  const bone = new THREE.Group(); bone.name = "spring"; parent.add(bone);
  const tail = new THREE.Group(); tail.name = "tail"; tail.position.set(.1, .03, 0); bone.add(tail);
  const setup = {
    version: "0414",
    prefabGraphs: [{ transforms: [
      { pathId: 1, name: "spring", transformPath: "parent/spring", childPathIds: [2] },
      { pathId: 2, name: "tail", transformPath: "parent/spring/tail", parentPathId: 1, childPathIds: [] },
    ] }],
    managers: [{ pathId: 100, automaticUpdates: true, simulationFrameRate: 60, dynamicRatio: 1,
      enableAngleLimits: true, enableLengthLimits: false, enableCollision: false,
      rawGravity: { x: 0, y: 0, z: 0 }, bonePathIds: [10] }],
    bones: [{ pathId: 10, nodeName: "spring", nodePath: "parent/spring", rawStiffnessForce: 0,
      rawDragForce: .4, rawSpringForce: { x: 0, y: 0, z: 0 }, hitRadius: .01,
      rawAngleLimits: { y: { active: true, min: -30, max: 30 } } }],
  };
  return { root, parent, bone, tail, setup };
}
function step({ root, setup }) {
  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension({ pjskSpringBone: { runtimeUnitySetup: setup } }, root);
  assert.ok(runtime);
  runtime.setTraceBoneFilters(["spring"]);
  runtime.update(1 / 60);
  return runtime.getTraceSnapshot().events[0];
}

test("SpringBone captures the current transform for every native rotation blend", () => {
  const f = fixture();
  f.setup.bones[0].rawSpringForce = { x: 0, y: -10, z: 0 };
  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension({ pjskSpringBone: { runtimeUnitySetup: f.setup } }, f.root);
  runtime.setTraceBoneFilters(["spring"]);
  runtime.update(1 / 60);
  const previous = f.bone.quaternion.toArray();
  assert.ok(new THREE.Quaternion(...previous).angleTo(new THREE.Quaternion()) > 1e-5);
  runtime.update(1 / 60);
  const captured = runtime.getTraceSnapshot().events.at(-1).skinAnimationLocalRotation;
  assert.deepEqual([captured.x, captured.y, captured.z, captured.w], previous);
});

test("SpringBone world rotation matches native Unity under nonuniform ancestor scale", () => {
  const trace = step(fixture());
  const actual = ["x", "y", "z", "w"].map(key => trace.parentRotation[key]);
  actual.forEach((value, index) => assert.ok(Math.abs(value - UNITY_ROTATION[index]) < 1e-6,
    `rotation component ${index}: ${value} != Unity ${UNITY_ROTATION[index]}`));
});

test("SpringBone angle-limit axes remain orthonormal under nonuniform ancestor scale", () => {
  const { forward, back, down } = step(fixture()).angleLimit;
  for (const [a, b] of [[forward, back], [forward, down], [back, down]]) {
    assert.ok(Math.abs(vector(a).dot(vector(b))) < 1e-6, "Unity TransformDirection axes must remain orthogonal");
  }
});

test("spring collider hit direction follows Unity rotation, not inverse-transpose scaling", () => {
  const f = fixture();
  f.parent.quaternion.identity(); f.bone.position.set(.3, .3, 0); f.tail.position.set(-.29, -.29, 0);
  const collider = new THREE.Group(); collider.name = "sphere"; f.root.add(collider);
  f.setup.managers[0].enableAngleLimits = false; f.setup.managers[0].enableCollision = true;
  f.setup.colliders = [{ index: 0, nodeName: "sphere", nodePath: "sphere", shape: { sphere: { radius: .03 } } }];
  f.setup.colliderBindings = [{ sourceSpringBonePathId: 10, colliders: [0] }];
  const trace = step(f);
  assert.equal(trace.collisionChecks.length, 1);
  const collision = trace.collisionChecks[0];
  assert.notEqual(collision.status, 0);
  const local = checkLocalSphereCollisionAndReact(new THREE.Vector3(.3, .3, 0),
    new THREE.Vector3(.01, .01, 0), .01, {
      kind: "sphere", enabled: true, radius: .03, localOffset: new THREE.Vector3(),
      localToWorldMatrix: new THREE.Matrix4(), worldToLocalMatrix: new THREE.Matrix4(),
      worldToLocalRadiusScale: 1, localToWorldNormalMatrix: new THREE.Matrix4(), lossyScaleX: 1,
    });
  assert.ok(vector(collision.hitNormal).distanceTo(local.hitNormal) < 1e-6,
    "identity Unity TransformDirection must retain the local collision direction despite scale");
});

test("spring force providers use Unity forward without inherited scale distortion", () => {
  const f = fixture();
  const wind = new THREE.Group(); wind.name = "wind"; f.parent.add(wind);
  f.setup.managers[0].forceProviders = [{ scriptName: "ForceVolume", nodePath: "parent/wind", raw: { strength: 1 } }];
  const force = vector(step(f).externalForce);
  const expected = new THREE.Vector3(0, 0, 1).applyQuaternion(f.parent.quaternion);
  assert.ok(force.distanceTo(expected) < 1e-6, "force must use Unity Transform.forward");
});

test("animated SpringBone names match the complete Unity object name", () => {
  for (const [name, expectedRatio] of [["spr", 1], ["Spring", 1], ["spring", 0]]) {
    const f = fixture();
    f.setup.managers[0].animatedBoneNames = [name];
    f.setup.managers[0].dynamicRatio = 0;
    assert.equal(step(f).dynamicRatio, expectedRatio, name);
  }
});
