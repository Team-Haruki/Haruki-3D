import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";
import { UnityPrefabSpringRuntime } from "../dist/haruki-3d-engine-internal.js";

test("nested spring managers advance shared hair state through each live manager subtree", () => {
  const root = new THREE.Group();
  const records = [];
  const add = (id, name, parent, path, position = [0, 0, 0]) => {
    const node = new THREE.Group();
    node.name = name;
    node.position.fromArray(position);
    node.userData.pjskTransformPath = path;
    parent.add(node);
    records.push({ pathId: id, name, transformPath: path, childPathIds: [] });
    return node;
  };
  const hip = add(1, "Hip", root, "Hip");
  const skirt = add(2, "skirt", hip, "Hip/skirt");
  add(3, "skirt_end", skirt, "Hip/skirt/skirt_end", [-0.1, 0, 0]);
  const neck = add(4, "Neck", hip, "Hip/Neck", [0, 1, 0]);
  const hair = add(5, "hair", neck, "Hip/Neck/hair");
  add(6, "hair_end", hair, "Hip/Neck/hair/hair_end", [-0.1, 0, 0]);
  records[0].childPathIds = [2, 4];
  records[1].childPathIds = [3];
  records[3].childPathIds = [5];
  records[4].childPathIds = [6];
  const sourceBone = (pathId, nodePath) => ({ pathId, nodePath, nodeName: nodePath.split("/").pop(),
    rawStiffnessForce: 0, rawDragForce: 0, rawSpringForce: { x: 0, y: 0, z: 0 } });
  const runtime = UnityPrefabSpringRuntime.fromPjskRuntimeExtension({ pjskSpringBone: { runtimeUnitySetup: {
    version: "0414", prefabGraphs: [{ transforms: records, monoBehaviours: [
      { pathId: 10, scriptName: "SekaiSpringBone", transformPath: "Hip/skirt" },
      { pathId: 20, scriptName: "SekaiSpringBone", transformPath: "Hip/Neck/hair" },
    ] }],
    // These lists describe pre-combination parts. Native FindSpringBones runs
    // again after assembly, so Hip also sees Neck's hair component.
    managers: [
      { pathId: 100, nodePath: "Hip", bonePathIds: [10], simulationFrameRate: 60,
        rawGravity: { x: 0, y: -1, z: 0 }, enableCollision: false },
      { pathId: 200, nodePath: "Hip/Neck", bonePathIds: [20], simulationFrameRate: 30,
        rawGravity: { x: 0, y: -3, z: 0 }, enableCollision: true },
    ], bones: [sourceBone(10, "Hip/skirt"), sourceBone(20, "Hip/Neck/hair")],
  } } }, root);
  assert.ok(runtime);
  runtime.setTraceBoneFilters(["skirt", "hair"], 10);
  runtime.update(1 / 60);
  const events = runtime.getTraceSnapshot().events;
  assert.equal(runtime.getSnapshot().boneCount, 2, "one state per physical bone");
  assert.equal(events.length, 3, "Hip updates skirt+hair, then Neck updates the same hair");
  const hairEvents = events.filter(event => event.sourceBonePathId === 20);
  assert.equal(hairEvents.length, 2);
  assert.deepEqual(hairEvents.map(event => event.updateManagerPathId), [100, 200]);
  assert.deepEqual(hairEvents.map(event => event.managerPathId), [200, 200], "last Initialize owns the bone constraints");
  assert.deepEqual(hairEvents.map(event => event.deltaTime), [1 / 60, 1 / 30]);
  assert.deepEqual(hairEvents.map(event => event.externalForce.y), [-1, -3]);
  assert.ok(hairEvents.every(event => event.enableCollision));
  assert.deepEqual(hairEvents[1].stateBefore, hairEvents[0].stateAfterAngleLimits, "second manager must continue the first update's state");
});
