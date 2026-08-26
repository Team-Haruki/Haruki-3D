import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import {
  UtjColliderStatus,
  checkLocalCapsuleCollisionAndReact,
  checkLocalSphereCollisionAndReact,
} from "../dist/haruki-3d-engine-internal.js";

// Character-height scaling puts a uniform world scale on the whole rig
// (body/Position). Official UTJ colliders convert the WORLD tail radius into
// collider-local units (InverseTransformDirection) and compare against the
// RAW serialized radius in local space. Scaling the serialized radius by
// lossyScale while comparing in local space double-counts the scale and
// inflates every collider by scale^2 (the Mafuyu ponytail / Minori back-hair
// splay: chest spheres 0.03 acted as ~0.08 world).

const SCALE = 2;

function scaledSphereCollider(radius) {
  const localToWorld = new THREE.Matrix4().makeScale(SCALE, SCALE, SCALE);
  const worldToLocal = localToWorld.clone().invert();
  return {
    kind: "sphere",
    enabled: true,
    debugName: "test-sphere",
    debugPath: "test/sphere",
    debugSourcePathId: null,
    localOffset: new THREE.Vector3(),
    radius,
    localToWorldMatrix: localToWorld,
    worldToLocalMatrix: worldToLocal,
    worldToLocalRadiusScale: 1 / SCALE,
    localToWorldNormalMatrix: new THREE.Matrix4().identity(),
    lossyScaleX: SCALE,
  };
}

function scaledCapsuleCollider(radius) {
  const base = scaledSphereCollider(radius);
  return {
    ...base,
    kind: "capsuleLocal",
    debugName: "test-capsule",
    debugPath: "test/capsule",
    localStart: new THREE.Vector3(0, -0.05, 0),
    localEnd: new THREE.Vector3(0, 0.05, 0),
  };
}

// Serialized radius 0.03 on a scale-2 node = 0.06 world; world tail radius
// 0.02 (raw 0.01 on the same scale-2 rig). Official combined reach = 0.08.
const WORLD_TAIL_RADIUS = 0.02;
const HEAD = new THREE.Vector3(0.2, 0, 0);

test("scaled sphere collider keeps the official world reach (no scale double-count)", () => {
  const outside = checkLocalSphereCollisionAndReact(
    HEAD.clone(),
    new THREE.Vector3(0.09, 0, 0),
    WORLD_TAIL_RADIUS,
    scaledSphereCollider(0.03)
  );
  assert.equal(outside.status, UtjColliderStatus.NoCollision);

  const touching = checkLocalSphereCollisionAndReact(
    HEAD.clone(),
    new THREE.Vector3(0.07, 0, 0),
    WORLD_TAIL_RADIUS,
    scaledSphereCollider(0.03)
  );
  assert.notEqual(touching.status, UtjColliderStatus.NoCollision);
  // The official sphere resolver is intersection-based, not radial, so only
  // require an outward push; the discriminating case is 0.09 staying free.
  assert.ok(
    touching.tailPosition.length() > 0.07 + 1e-9,
    `pushed outward from 0.07, got ${touching.tailPosition.length()}`
  );
});

test("scaled local capsule collider keeps the official world reach (no scale double-count)", () => {
  const outside = checkLocalCapsuleCollisionAndReact(
    HEAD.clone(),
    new THREE.Vector3(0.09, 0, 0),
    WORLD_TAIL_RADIUS,
    scaledCapsuleCollider(0.03)
  );
  assert.equal(outside.status, UtjColliderStatus.NoCollision);

  const touching = checkLocalCapsuleCollisionAndReact(
    HEAD.clone(),
    new THREE.Vector3(0.07, 0, 0),
    WORLD_TAIL_RADIUS,
    scaledCapsuleCollider(0.03)
  );
  assert.notEqual(touching.status, UtjColliderStatus.NoCollision);
  assert.ok(
    Math.abs(touching.tailPosition.x - 0.08) < 1e-6,
    `pushed to world surface x=0.08, got ${touching.tailPosition.x}`
  );
});
