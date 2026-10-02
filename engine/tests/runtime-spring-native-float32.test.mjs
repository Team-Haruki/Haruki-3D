import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import * as THREE from "three";
import { computeUtjLocalRotation, constrainUtjAngleLimit, createUtjSpringBoneState, updateUtjSpring } from "../dist/haruki-3d-engine-internal.js";
import { getUnityWorldPosition, getUnityWorldQuaternion, normalizeUnityLocalQuaternion, inverseUnityTransformPoint, lerpUnityQuaternionFloat32, transformUnityDirectionToWorld } from "../dist/haruki-3d-engine-internal.js";
const native = JSON.parse(fs.readFileSync(new URL("./fixtures/unity-spring-float32.json", import.meta.url)));
const vector = v => new THREE.Vector3(-v.x, v.y, v.z);
const quaternion = q => new THREE.Quaternion(q.x, -q.y, -q.z, q.w);
const exact = (actual, expected) => assert.ok(actual.toArray().every((value, i) => value === expected.toArray()[i]), `${actual.toArray()} != ${expected.toArray()}`);

test("SpringBone integration and three-dimensional limits preserve native float32 outputs", () => {
  for (const row of native.integration) {
    const state = createUtjSpringBoneState(vector(row.head), vector(row.tip));
    state.prevTipPos.copy(vector(row.previous));
    updateUtjSpring(state, { headPosition: vector(row.head), parentRotation: quaternion(row.parent), initialLocalRotation: quaternion(row.initial), boneAxis: vector(row.axis), springLength: row.length, stiffnessForce: row.stiffness, dragForce: row.drag, springForce: vector(row.force), externalForce: vector(row.external), deltaTime: row.dt });
    exact(state.currTipPos, vector(row.result));
  }
  for (const row of native.angles) {
    const value = vector(row.vector);
    constrainUtjAngleLimit({ vector: value, basisSide: vector(row.side), basisUp: vector(row.up), basisForward: vector(row.forward), springStrength: row.strength, deltaTime: row.dt, limit: { active: true, min: row.min, max: row.max } });
    exact(value, vector(row.result));
  }
});

test("Unity transform and blend operations match native samples including signed nonuniform scale", () => {
  for (const row of native.transforms) {
    const parent = new THREE.Object3D(), child = new THREE.Object3D();
    parent.add(child);
    parent.position.copy(vector(row.head)); child.position.copy(vector(row.tip));
    parent.quaternion.copy(quaternion(row.parent)); child.quaternion.copy(quaternion(row.initial));
    if (row.scaled) {
      parent.scale.set(row.parentScale.x, row.parentScale.y, row.parentScale.z);
      child.scale.set(row.childScale.x, row.childScale.y, row.childScale.z);
    }
    exact(getUnityWorldPosition(child, new THREE.Vector3()), vector(row.worldPosition));
    exact(getUnityWorldQuaternion(child, new THREE.Quaternion()), quaternion(row.worldRotation));
    exact(inverseUnityTransformPoint(child, vector(row.tip)), vector(row.inversePoint));
    exact(lerpUnityQuaternionFloat32(quaternion(row.parent), quaternion(row.initial), 1), quaternion(row.lerp));
    for (const [name, axis] of [["right", [-1, 0, 0]], ["up", [0, 1, 0]], ["forward", [0, 0, 1]]]) {
      exact(transformUnityDirectionToWorld(child, new THREE.Vector3(...axis)), vector(row[name]));
    }
  }
});

test("SpringBone local rotation retains the native branch at the actual critical frames", () => {
  for (const row of native.rotations) {
    const actual = computeUtjLocalRotation(vector(row.head), vector(row.tip), quaternion(row.parentRotation), quaternion(row.initialRotation), vector(row.axis));
    assert.ok(actual.normalize().angleTo(quaternion(row.rotation).normalize()) < 1e-6);
  }
});

test("Unity local rotation assignment retains its component-specific float32 normalization", () => {
  for (const row of native.setters) exact(normalizeUnityLocalQuaternion(quaternion(row.input)), quaternion(row.expected));
});
