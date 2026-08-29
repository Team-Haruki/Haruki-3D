import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import {
  UtjColliderStatus,
  applyUtjCollisionVelocityResponse,
  applyUtjLengthLimits,
  cacheUtjSpringBonePosition,
  checkCapsuleCollisionAndReact,
  checkCollisionWithAlignedPlaneAndReact,
  checkLocalCapsuleCollisionAndReact,
  checkLocalCylinderCollisionAndReact,
  checkLocalYAxisCapsuleCollisionAndReact,
  checkSphereCollisionAndReact,
  checkUtjCollisions,
  checkUtjGroundCollision,
  computeAnimatedTipPosition,
  computeNewTailPosition,
  computeSphereIntersectionCircle,
  computeUtjWorldRotation,
  constrainUtjAngleLimit,
  createUtjSpringBoneState,
  enforceSpringLength,
  fixBoneLength,
  updateUtjSpring,
} from "../dist/haruki-3d-engine-internal.js";

const identity = () => new THREE.Matrix4();
const vector = (x, y, z) => new THREE.Vector3(x, y, z);

test("UTJ state integration, cached movement, and length guards stay finite", () => {
  const state = createUtjSpringBoneState(vector(1, 2, 3), vector(2, 2, 3));
  assert.deepEqual(state.cachedPosition.toArray(), [1, 2, 3]);
  cacheUtjSpringBonePosition(state, vector(2, 4, 6));
  assert.deepEqual(state.cachedMovement.toArray(), [1, 2, 3]);

  const animated = computeAnimatedTipPosition({
    headPosition: vector(1, 0, 0),
    parentRotation: new THREE.Quaternion(),
    initialLocalRotation: new THREE.Quaternion().setFromAxisAngle(vector(0, 0, 1), Math.PI / 2),
    boneAxis: vector(1, 0, 0),
    springLength: 2,
  });
  assert.ok(animated.distanceTo(vector(1, 2, 0)) < 1e-12);

  updateUtjSpring(state, {
    headPosition: vector(1, 2, 3),
    parentRotation: new THREE.Quaternion(),
    initialLocalRotation: new THREE.Quaternion(),
    boneAxis: vector(1, 0, 0),
    lengthFallbackDirection: vector(0, 1, 0),
    springLength: 1,
    stiffnessForce: 0.25,
    dragForce: 0.1,
    springForce: vector(0, 1, 0),
    externalForce: vector(0, 0, 1),
    deltaTime: 1 / 60,
  });
  assert.ok(Math.abs(state.currTipPos.distanceTo(vector(1, 2, 3)) - 1) < 1e-12);

  const collapsed = vector(3, 3, 3);
  enforceSpringLength(collapsed, vector(3, 3, 3), 2, vector(0, 0, 1));
  assert.deepEqual(collapsed.toArray(), [3, 3, 5]);
  enforceSpringLength(collapsed, vector(3, 3, 3), 1);
  assert.ok(Math.abs(collapsed.distanceTo(vector(3, 3, 3)) - 1) < 1e-12);

  const fixed = new THREE.Vector3();
  fixBoneLength(fixed, vector(0, 0, 0), vector(0, 0, 0), 2, 4, vector(0, 1, 0));
  assert.deepEqual(fixed.toArray(), [0, 2, 0]);
  fixBoneLength(fixed, vector(0, 0, 0), vector(10, 0, 0), 2, 4);
  assert.deepEqual(fixed.toArray(), [4, 0, 0]);
  fixBoneLength(fixed, vector(0, 0, 0), vector(3, 0, 0), 2, 4);
  assert.deepEqual(fixed.toArray(), [3, 0, 0]);

  const limited = vector(2, 0, 0);
  applyUtjLengthLimits({
    currTipPos: limited,
    springConstant: 10,
    deltaTime: 0.1,
    targets: [
      { position: vector(0, 0, 0), initialLength: 1 },
      { position: vector(2, 0, 0), initialLength: 1 },
    ],
  });
  assert.ok(limited.x < 2);
  applyUtjLengthLimits({ currTipPos: limited, springConstant: 10, deltaTime: 0.1, targets: [] });
});

test("aligned plane and ground collision cover free, embedded, radial, and vertical responses", () => {
  const free = vector(0, 2, 0);
  assert.equal(
    checkCollisionWithAlignedPlaneAndReact(vector(0, 1, 0), 1, free, 0.1, 1),
    UtjColliderStatus.NoCollision
  );

  const embedded = vector(0, -1, 0);
  assert.equal(
    checkCollisionWithAlignedPlaneAndReact(vector(0, -2, 0), 1, embedded, 0.1, 1),
    UtjColliderStatus.HeadIsEmbedded
  );
  assert.deepEqual(embedded.toArray(), [0, -1, 0]);

  const radial = vector(0.8, -0.2, 0.4);
  assert.equal(
    checkCollisionWithAlignedPlaneAndReact(vector(0, 1, 0), 1.2, radial, 0.1, 1),
    UtjColliderStatus.TailCollision
  );
  assert.equal(radial.y, 0.1);

  const vertical = vector(0, 0, 0);
  assert.equal(
    checkCollisionWithAlignedPlaneAndReact(vector(0, 1, 0), 1, vertical, 0.1, 1),
    UtjColliderStatus.TailCollision
  );
  assert.deepEqual(vertical.toArray(), [0, 1, 0]);

  const clearState = createUtjSpringBoneState(vector(0, 1, 0), vector(0, 2, 0));
  assert.equal(checkUtjGroundCollision(clearState, {
    headPosition: vector(0, 1, 0), springLength: 1, tailRadius: 0.1,
    groundHeight: 0, lengthFallbackDirection: vector(0, 1, 0), bounce: 0, friction: 0,
  }), false);

  const hitState = createUtjSpringBoneState(vector(0, 1, 0), vector(0.8, -0.1, 0));
  assert.equal(checkUtjGroundCollision(hitState, {
    headPosition: vector(0, 1, 0), springLength: 1.4, tailRadius: 0.1,
    groundHeight: 0, lengthFallbackDirection: vector(0, 1, 0), bounce: 0, friction: 0,
  }), true);
  assert.deepEqual(hitState.prevTipPos.toArray(), hitState.currTipPos.toArray());
  assert.deepEqual(hitState.hitNormal.toArray(), [0, 1, 0]);
});

test("sphere solver covers separation, embedded heads, missing intersections, and circle projection", () => {
  assert.equal(
    checkSphereCollisionAndReact(vector(3, 0, 0), vector(4, 0, 0), 0.1, vector(0, 0, 0), 1).status,
    UtjColliderStatus.NoCollision
  );
  const embedded = checkSphereCollisionAndReact(
    vector(0, 0, 0), vector(0, 0, 0), 0.25, vector(0, 0, 0), 1
  );
  assert.equal(embedded.status, UtjColliderStatus.HeadIsEmbedded);
  assert.deepEqual(embedded.hitNormal.toArray(), [0, 1, 0]);

  const explicitFallback = checkSphereCollisionAndReact(
    vector(0, 0, 0), vector(0, 0, 0), 0.25, vector(0, 0, 0), 1, 1,
    { headEmbeddedFallback: vector(1, 0, 0) }
  );
  assert.deepEqual(explicitFallback.hitNormal.toArray(), [1, 0, 0]);

  const noCircle = checkSphereCollisionAndReact(
    vector(2, 0, 0), vector(0.5, 0, 0), 0.1, vector(0, 0, 0), 1, 0.1,
    { noIntersectionStatus: UtjColliderStatus.TailCollision }
  );
  assert.equal(noCircle.status, UtjColliderStatus.TailCollision);

  assert.equal(computeSphereIntersectionCircle(vector(0, 0, 0), 1, vector(0, 0, 0), 1), null);
  assert.equal(computeSphereIntersectionCircle(vector(0, 0, 0), 1, vector(3, 0, 0), 1), null);
  const circle = computeSphereIntersectionCircle(vector(0, 0, 0), 2, vector(2, 0, 0), 2);
  assert.ok(circle);
  assert.ok(Math.abs(circle.radius - Math.sqrt(3)) < 1e-12);
  assert.deepEqual(computeNewTailPosition({
    origin: vector(1, 0, 0), upVector: vector(1, 0, 0), radius: 0,
  }, vector(1, 2, 0)).toArray(), [1, 0, 0]);
  const projected = computeNewTailPosition(circle, vector(1, 2, 0));
  assert.ok(Math.abs(projected.distanceTo(circle.origin) - circle.radius) < 1e-12);
});

test("capsule and cylinder solvers cover degenerate, cap, body, and head-status branches", () => {
  const head = vector(2, 0, 0);
  const degenerate = checkCapsuleCollisionAndReact(
    head, vector(0.5, 0, 0), 0.1, vector(0, 0, 0), vector(0, 0, 0), 1
  );
  assert.notEqual(degenerate.status, UtjColliderStatus.NoCollision);

  assert.equal(checkCapsuleCollisionAndReact(
    head, vector(3, 0, 0), 0.1, vector(0, -1, 0), vector(0, 1, 0), 0.5
  ).status, UtjColliderStatus.NoCollision);
  assert.notEqual(checkCapsuleCollisionAndReact(
    head, vector(0.2, -1, 0), 0.1, vector(0, -1, 0), vector(0, 1, 0), 0.5
  ).status, UtjColliderStatus.NoCollision);
  assert.notEqual(checkCapsuleCollisionAndReact(
    head, vector(0.2, 1, 0), 0.1, vector(0, -1, 0), vector(0, 1, 0), 0.5
  ).status, UtjColliderStatus.NoCollision);

  assert.equal(checkCapsuleCollisionAndReact(
    vector(0.1, 0, 0), vector(0.2, 0, 0), 0.1,
    vector(0, -1, 0), vector(0, 1, 0), 0.5
  ).status, UtjColliderStatus.HeadIsEmbedded);
  assert.equal(checkCapsuleCollisionAndReact(
    vector(2, 0, 0), vector(0.2, 0, 0), 0.1,
    vector(0, -1, 0), vector(0, 1, 0), 0.5
  ).status, UtjColliderStatus.TailCollision);

  assert.equal(checkLocalYAxisCapsuleCollisionAndReact(
    head, vector(0.2, 0, 0), 0.1, vector(0, -1, 0), vector(0, 1, 0), 0
  ).status, UtjColliderStatus.NoCollision);
  assert.notEqual(checkLocalYAxisCapsuleCollisionAndReact(
    head, vector(0.2, -1, 0), 0.1, vector(0, -1, 0), vector(0, 1, 0), 0.5
  ).status, UtjColliderStatus.NoCollision);
  assert.notEqual(checkLocalYAxisCapsuleCollisionAndReact(
    head, vector(0.2, 1, 0), 0.1, vector(0, 1, 0), vector(0, -1, 0), 0.5
  ).status, UtjColliderStatus.NoCollision);

  assert.equal(checkLocalCylinderCollisionAndReact(
    head, vector(2, 0, 0), 0.1, 0.5
  ).status, UtjColliderStatus.NoCollision);
  assert.equal(checkLocalCylinderCollisionAndReact(
    vector(0.1, 0, 0), vector(0, 0, 0), 0.1, 0.5
  ).status, UtjColliderStatus.HeadIsEmbedded);
  assert.equal(checkLocalCylinderCollisionAndReact(
    head, vector(0.2, 0, 0), 0.1, 0.5
  ).status, UtjColliderStatus.TailCollision);
});

test("collision dispatcher sorts collider kinds, skips disabled entries, and reports every trace shape", () => {
  const state = createUtjSpringBoneState(vector(2, 0, 0), vector(0.2, 0, 0));
  state.prevTipPos.set(0.1, 0, 0);
  const matrix = identity();
  const checked = [];
  const collided = [];
  const colliders = [
    { kind: "panel", enabled: true, width: 2, height: 2, localToWorldMatrix: matrix,
      worldToLocalMatrix: matrix, worldToLocalRadiusScale: 1, worldToLocalLengthScale: 1,
      localToWorldNormalMatrix: matrix },
    { kind: "sphere", enabled: true, radius: 0.5, localOffset: vector(0, 0, 0),
      localToWorldMatrix: matrix, worldToLocalMatrix: matrix, worldToLocalRadiusScale: 1,
      localToWorldNormalMatrix: matrix, lossyScaleX: 1 },
    { kind: "capsuleLocal", enabled: true, localStart: vector(0, -1, 0), localEnd: vector(0, 1, 0),
      radius: 0.5, localToWorldMatrix: matrix, worldToLocalMatrix: matrix,
      worldToLocalRadiusScale: 1, localToWorldNormalMatrix: matrix, lossyScaleX: 1 },
    { kind: "capsule", enabled: true, start: vector(0, -1, 0), end: vector(0, 1, 0), radius: 0.5 },
    { kind: "sphere", enabled: false, radius: 100, localOffset: vector(0, 0, 0),
      localToWorldMatrix: matrix, worldToLocalMatrix: matrix, worldToLocalRadiusScale: 1,
      localToWorldNormalMatrix: matrix, lossyScaleX: 1 },
  ];
  const status = checkUtjCollisions(state, {
    headPosition: vector(2, 0, 0), springLength: 1.8, tailRadius: 0.1,
    colliders, bounce: 0.5, friction: 0.25,
    onColliderCheck(collider, trace) { checked.push([collider.kind, trace.details.kind]); },
    onCollision(collider) { collided.push(collider.kind); },
  });
  assert.notEqual(status, UtjColliderStatus.NoCollision);
  assert.deepEqual(checked.map((entry) => entry[0]), ["capsuleLocal", "capsule", "sphere", "panel"]);
  assert.deepEqual(checked.map((entry) => entry[1]), ["capsuleLocal", "capsule", "sphere", "panel"]);
  assert.ok(collided.length > 0);

  const quiet = createUtjSpringBoneState(vector(0, 0, 0), vector(10, 0, 0));
  assert.equal(checkUtjCollisions(quiet, {
    headPosition: vector(0, 0, 0), springLength: 10, tailRadius: 0.1,
    colliders: [colliders[1]], bounce: 0, friction: 0,
  }), UtjColliderStatus.NoCollision);

  const local = checkLocalCapsuleCollisionAndReact(
    vector(2, 0, 0), vector(3, 0, 0), 0.1, colliders[2]
  );
  assert.equal(local.status, UtjColliderStatus.NoCollision);
});

test("collision velocity and angle limits cover stop, bounce, fallback, and clamp paths", () => {
  const stopped = createUtjSpringBoneState(vector(0, 0, 0), vector(1, 0, 0));
  applyUtjCollisionVelocityResponse(stopped, vector(0, 0, 0), 0, 1);
  assert.deepEqual(stopped.prevTipPos.toArray(), stopped.currTipPos.toArray());

  const bounced = createUtjSpringBoneState(vector(0, 0, 0), vector(1, 0, 0));
  bounced.prevTipPos.set(-2, 1, 0);
  applyUtjCollisionVelocityResponse(bounced, vector(1, 0, 0), 1, 0, vector(10, 0, 0));
  assert.ok(bounced.prevTipPos.distanceTo(bounced.currTipPos) > 0);
  assert.ok(bounced.currTipPos.x < 1);

  const base = {
    basisSide: vector(1, 0, 0), basisUp: vector(0, 1, 0), basisForward: vector(0, 0, 1),
    springStrength: 0, deltaTime: 1, limit: { active: false, min: -30, max: 30 },
  };
  assert.equal(constrainUtjAngleLimit({ ...base, vector: vector(1, 0, 1) }), false);
  for (const [value, min, max] of [
    [vector(1, 0, 0), -30, 30],
    [vector(-1, 0, 0), -30, 30],
    [vector(1, 0, 1), 0, 0],
    [vector(0, 1, 0), -30, 30],
  ]) {
    const changed = constrainUtjAngleLimit({
      ...base, limit: { active: true, min, max }, vector: value,
    });
    assert.equal(typeof changed, "boolean");
    assert.ok(value.toArray().every(Number.isFinite));
  }

  const worldRotation = computeUtjWorldRotation(
    vector(0, 0, 0), vector(0, 1, 0), new THREE.Quaternion(),
    new THREE.Quaternion(), vector(0, 0, 0)
  );
  assert.ok(worldRotation.toArray().every(Number.isFinite));
});
