import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import {
  UnityConstraintRuntime,
  applyUnityRuntimeConstraints,
} from "../dist/haruki-3d-engine-internal.js";

function makeGraph() {
  const root = new THREE.Group();
  root.name = "root";
  const parent = new THREE.Group();
  parent.name = "parent";
  parent.position.set(1, 0, 0);
  parent.rotation.y = 0.2;
  root.add(parent);

  const nodes = new Map();
  const add = (path, name, position) => {
    const node = new THREE.Group();
    node.name = name;
    node.position.copy(position);
    parent.add(node);
    nodes.set(path, node);
    return node;
  };
  const sourceA = add("rig/sourceA", "sourceA", new THREE.Vector3(2, 1, 0));
  const sourceB = add("rig/sourceB", "sourceB", new THREE.Vector3(-1, 2, 1));
  sourceA.rotation.set(0.2, 0.3, 0.1);
  sourceB.rotation.set(-0.3, 0.1, -0.2);
  const worldUp = add("rig/worldUp", "worldUp", new THREE.Vector3(0, 4, 2));
  const coincident = add("rig/coincident", "coincident", new THREE.Vector3(0, 0, 0));
  const owners = [];
  for (let index = 0; index < 16; index += 1) {
    owners.push(add(`rig/owner${index}`, `owner${index}`, new THREE.Vector3(0, 0, 0)));
  }
  root.updateMatrixWorld(true);
  return { graph: { root, nodeByPath: nodes }, sourceA, sourceB, worldUp, coincident, owners };
}

function binding(path, name, weight = 1, extra = {}) {
  return { sourcePath: path, sourceName: name, weight, ...extra };
}

test("constraint runtime applies parent, rotation, and every aim world-up policy", () => {
  const { graph, owners } = makeGraph();
  const constraints = [
    {
      type: "parent", ownerPath: "rig/owner0", ownerName: "owner0", weight: 0.5,
      translationAxis: 1 | 4, rotationAxis: 2,
      translationAtRest: { x: 3, y: 4, z: 5 }, rotationAtRest: { x: 5, y: 10, z: 15 },
      sources: [
        binding("missing/path", "sourceA", 1, {
          translationOffset: { x: 1, y: 2, z: 3 }, rotationOffset: { x: 10, y: 0, z: 0 },
        }),
        binding("rig/sourceB", "sourceB", 2, {
          translationOffset: { X: -1, Y: 0, Z: 1 }, rotationOffset: { X: 0, Y: 20, Z: 0 },
        }),
        binding("rig/sourceB", "sourceB", -1),
      ],
    },
    {
      type: "rotation", ownerPath: "rig/owner1", rotationAxis: 1 | 2 | 4,
      rotationOffset: { x: 0, y: 15, z: 0 }, sources: [
        binding("rig/sourceA", "sourceA", 1), binding("rig/sourceB", "sourceB", 1),
      ],
    },
    ...[0, 1, 2, 3, 4].map((worldUpType, index) => ({
      type: "aim", ownerPath: `rig/owner${index + 2}`, weight: 1,
      aimVector: index === 0 ? { x: 0, y: 0, z: 0 } : { x: 0, y: 0, z: 1 },
      upVector: index === 1 ? { x: 0, y: 0, z: 0 } : { x: 0, y: 1, z: 0 },
      worldUpType,
      worldUpVector: { x: 1, y: 1, z: 0 },
      worldUpObjectPath: index === 1 ? "missing" : "rig/worldUp",
      worldUpObjectName: index === 2 ? "worldUp" : null,
      rotationOffset: index === 3 ? { x: 0, y: 0, z: 5 } : null,
      sources: [binding("rig/sourceA", "sourceA")],
    })),
  ];
  const debug = applyUnityRuntimeConstraints(graph, {
    version: "0414", sourceKind: "test", constraints, warnings: ["kept", 123, null],
  }, 2);

  assert.ok(debug);
  assert.equal(debug.constraintCount, 7);
  assert.equal(debug.appliedCount, 7);
  assert.equal(debug.resolvedCount, 7);
  assert.deepEqual(debug.warnings, ["kept"]);
  assert.deepEqual(debug.constraints[0].sources[0].translationOffset, { x: -2, y: 4, z: 6 });
  assert.deepEqual(debug.constraints[0].sources[0].rotationOffset, { x: 10, y: 0, z: 0 });
  for (const owner of owners.slice(0, 7)) {
    assert.ok(owner.position.toArray().every(Number.isFinite));
    assert.ok(owner.quaternion.toArray().every(Number.isFinite));
  }

  const runtime = new UnityConstraintRuntime(graph, { constraints }, 2);
  const persistent = runtime.update();
  assert.equal(persistent.appliedCount, 7);
});

test("constraint diagnostics retain every skip and resolution reason", () => {
  const { graph } = makeGraph();
  const constraints = [
    { type: "parent", ownerPath: "rig/owner0", enabled: false, sources: [binding("rig/sourceA", "sourceA")] },
    { type: "parent", ownerPath: "missing", ownerName: "missing", sources: [binding("rig/sourceA", "sourceA")] },
    { type: "parent", ownerPath: "rig/owner2", sources: [] },
    { type: "parent", ownerPath: "rig/owner3", sources: [binding("missing", "missing")] },
    { type: "parent", ownerPath: "rig/owner4", sources: [binding("rig/sourceA", "sourceA", 0)] },
    { type: "rotation", ownerPath: "rig/owner5", sources: [binding("rig/sourceA", "sourceA", -1)] },
    { type: "aim", ownerPath: "rig/owner6", sources: [binding("rig/coincident", "coincident")] },
    { type: "position", ownerPath: "rig/owner7", sources: [binding("rig/sourceA", "sourceA")] },
    { type: 42, ownerPath: "rig/owner8", sources: [binding("rig/sourceA", "sourceA")] },
    { type: "parent", ownerName: "owner9", sources: [binding(null, "sourceA", Number.NaN)] },
    { type: "parent", ownerPath: null, ownerName: null, sources: [binding(null, null)] },
  ];
  const debug = applyUnityRuntimeConstraints(graph, {
    constraints, warnings: "invalid",
  }, 1);
  assert.ok(debug);
  assert.equal(debug.appliedCount, 1);
  assert.equal(debug.unresolvedCount, 4);
  assert.equal(debug.constraints[0].reason, "constraint component is disabled");
  assert.match(debug.constraints[1].reason, /was not found/);
  assert.equal(debug.constraints[2].reason, "constraint has no source transforms");
  assert.match(debug.constraints[3].reason, /not uniquely resolved/);
  assert.equal(debug.constraints[4].reason, "parent constraint has no positive source weight");
  assert.equal(debug.constraints[5].reason, "rotation constraint has no positive source weight");
  assert.equal(debug.constraints[6].reason, "aim constraint target direction or source weight was invalid");
  assert.match(debug.constraints[7].reason, /unsupported constraint type position/);
  assert.match(debug.constraints[8].reason, /unsupported constraint type unknown/);
  assert.equal(debug.constraints[10].reason, "constraint transform path and name are missing");
  assert.deepEqual(debug.warnings, []);
  assert.equal(applyUnityRuntimeConstraints(graph, undefined, 1), null);
});

test("constraint name fallback rejects ambiguous owner matches and accepts a unique source", () => {
  const { graph } = makeGraph();
  const duplicateA = new THREE.Group();
  const duplicateB = new THREE.Group();
  duplicateA.name = duplicateB.name = "duplicate";
  graph.root.add(duplicateA, duplicateB);
  graph.nodeByPath.set("a/duplicate", duplicateA);
  graph.nodeByPath.set("b/duplicate", duplicateB);

  const debug = applyUnityRuntimeConstraints(graph, {
    constraints: [{
      type: "parent", ownerName: "duplicate",
      sources: [{ sourceName: "sourceA", translationOffset: null, rotationOffset: null }],
    }],
  }, 1);
  assert.ok(debug);
  assert.equal(debug.constraints[0].resolvedOwner, false);
  assert.match(debug.constraints[0].reason, /matched 2 nodes/);
});
