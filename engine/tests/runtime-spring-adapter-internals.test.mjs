import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import { unityPrefabSpringRuntimeInternals as runtime } from "../dist/haruki-3d-engine-internal.js";

const v = (x, y, z) => new THREE.Vector3(x, y, z);

function makeNodeGraph() {
  const root = new THREE.Group();
  root.name = "Loaded:root";
  const body = new THREE.Group();
  body.name = "body";
  root.add(body);
  const spring = new THREE.Bone();
  spring.name = "Hair_1";
  spring.userData.pjskRuntimePartIndex = 2;
  spring.userData.pjskTransformPath = "body/Hair";
  spring.position.set(0, 1, 0);
  body.add(spring);
  const left = new THREE.Bone();
  left.name = "Left";
  left.userData.pjskRuntimePartIndex = 2;
  left.userData.pjskTransformPath = "body/Hair/Left";
  left.position.set(-1, 1, 0);
  const right = new THREE.Bone();
  right.name = "Right";
  right.userData.pjskRuntimePartIndex = 2;
  right.userData.pjskTransformPath = "body/Hair/Right";
  right.position.set(1, 1, 0);
  spring.add(left, right);
  const skin = new THREE.SkinnedMesh(new THREE.BufferGeometry(), new THREE.MeshBasicMaterial());
  skin.name = "mesh";
  skin.bind(new THREE.Skeleton([spring, left, right]));
  body.add(skin);
  root.updateMatrixWorld(true);
  return { root, body, spring, left, right };
}

function makeCollider(index, nodePath, nodeName = "CL") {
  const node = new THREE.Group();
  node.name = nodeName;
  return {
    source: {
      index,
      pathId: 100 + index,
      nodePath,
      nodeName,
      shape: { sphere: { radius: 0.1, offset: { x: 0, y: 0, z: 0 } } },
    },
    node,
  };
}

test("spring component and prefab indexes enforce verified part-scoped identities", () => {
  const setup = {
    prefabGraphs: [{
      transforms: [
        { pathId: 1, runtimePartIndex: 2, name: "Hair", transformPath: "body/Hair", childPathIds: [2, 3, 999] },
        { pathId: 2, runtimePartIndex: 2, name: "Left", transformPath: "body/Hair/Left", childPathIds: [] },
        { pathId: 3, runtimePartIndex: 2, name: "Right", transformPath: "body/Hair/Right", childPathIds: [] },
        { name: "anonymous", childPathIds: [] },
      ],
      monoBehaviours: [
        { pathId: 10, runtimePartIndex: 2, scriptName: " SpringBone ", transformPath: "body/Hair" },
        { pathId: 11, scriptName: "SekaiSpringBone", transformPath: "body/Other" },
        { pathId: 2, scriptName: "SpringBonePivot", transformPath: "body/Hair/Left" },
        { pathId: 12, scriptName: "ExtraBone", transformPath: "body/Extra" },
      ],
    }],
  };
  const components = runtime.buildRuntimeSpringComponentIndex(setup);
  assert.equal(components.hasComponentMetadata, true);
  assert.deepEqual([...components.pathIds], [10, 11]);
  assert.equal(runtime.isOfficialRuntimeSpringComponent("springbone"), true);
  assert.equal(runtime.isOfficialRuntimeSpringComponent("SEKAISPRINGBONE"), true);
  assert.equal(runtime.isOfficialRuntimeSpringComponent("ExtraBone"), false);
  assert.equal(runtime.isOfficialRuntimeSpringComponent(null), false);
  assert.equal(runtime.isVerifiedRuntimeSpringBone({ pathId: 10 }, components), true);
  assert.equal(runtime.isVerifiedRuntimeSpringBone({ runtimePartIndex: 2, nodePath: "body/Hair" }, components), true);
  assert.equal(runtime.isVerifiedRuntimeSpringBone({ runtimePartIndex: 3, nodePath: "body/Hair" }, components), false);
  assert.equal(runtime.isVerifiedRuntimeSpringBone({}, { hasComponentMetadata: false, pathIds: new Set(), partPaths: new Set() }), true);

  const graphIndex = runtime.buildPrefabGraphIndex(setup);
  assert.equal(graphIndex.transformByPathId.size, 3);
  assert.equal(graphIndex.pivotTransformPathIds.has(2), true);
  assert.equal(runtime.resolvePrefabTransformForPart(graphIndex, "body/Hair", 2).pathId, 1);
  assert.equal(runtime.resolvePrefabTransformForPart(graphIndex, "body/Hair", 99).pathId, 1);
  assert.equal(runtime.resolvePrefabTransformForPart(graphIndex, "missing"), undefined);
  assert.equal(runtime.isValidPrefabSpringTailChild(graphIndex.transformByPathId.get(2), graphIndex), false);
  assert.equal(runtime.isValidPrefabSpringTailChild(graphIndex.transformByPathId.get(3), graphIndex), true);
});

test("node resolution handles canonical Three names, source paths, part paths, and skinned bones", () => {
  const { root, spring, left } = makeNodeGraph();
  const resolution = runtime.buildNodeResolution(root);
  assert.equal(runtime.resolveNode(resolution, "body/Hair_1"), spring);
  assert.equal(runtime.resolveNode(resolution, "body/Hair"), spring);
  assert.equal(runtime.resolveNode(resolution, ""), null);
  assert.equal(runtime.resolveNodeForPart(resolution, "body/Hair", 2), spring);
  assert.equal(runtime.resolveNodeForPart(resolution, "body/Hair", 9), spring);
  assert.equal(runtime.resolveNodeForPart(resolution, null, 2), null);
  assert.equal(runtime.readRuntimePartIndex(spring), 2);
  assert.equal(runtime.readRuntimePartIndex(root), undefined);
  assert.equal(runtime.partPathKey(2, "body/Hair"), "2:body/Hair");
  assert.deepEqual([...runtime.collectSkinnedBones(root)], [spring, left, ...spring.children.filter((node) => node.isBone && node !== left)]);
  assert.equal(runtime.getObjectPath(left, root), "body/Hair_1/Left");
  assert.equal(runtime.getCanonicalObjectPath(left, root), "body/Hair/Left");
  assert.equal(runtime.stripThreeUniqueNameSuffix("Hair_12"), "Hair");
  assert.equal(runtime.stripThreeUniqueNameSuffix("Hair_0"), "Hair_0");
  assert.equal(runtime.getObjectDepth(left), 4);
});

test("prefab tail binding covers fallback, one child, pivot filtering, and averaged children", () => {
  const { root, spring } = makeNodeGraph();
  const resolution = runtime.buildNodeResolution(root);
  const baseSetup = {
    prefabGraphs: [{ transforms: [
      { pathId: 1, runtimePartIndex: 2, name: "Hair", transformPath: "body/Hair", childPathIds: [] },
      { pathId: 2, runtimePartIndex: 2, name: "Left", transformPath: "body/Hair/Left", childPathIds: [] },
      { pathId: 3, runtimePartIndex: 2, name: "Right", transformPath: "body/Hair/Right", childPathIds: [] },
    ] }],
  };
  let graph = runtime.buildPrefabGraphIndex(baseSetup);
  assert.equal(runtime.computeUnityPrefabChildPosition({ nodePath: "body/Hair", runtimePartIndex: 2 }, spring, graph, resolution).mode, "fallback");

  baseSetup.prefabGraphs[0].transforms[0].childPathIds = [2, 999];
  graph = runtime.buildPrefabGraphIndex(baseSetup);
  const single = runtime.computeUnityPrefabChildPosition({ nodePath: "body/Hair", runtimePartIndex: 2 }, spring, graph, resolution);
  assert.equal(single.mode, "singleChild");
  assert.deepEqual(single.childNames, ["Left"]);

  baseSetup.prefabGraphs[0].transforms[0].childPathIds = [2, 3];
  graph = runtime.buildPrefabGraphIndex(baseSetup);
  const average = runtime.computeUnityPrefabChildPosition({ nodePath: "body/Hair", runtimePartIndex: 2 }, spring, graph, resolution);
  assert.equal(average.mode, "averageChildren");
  assert.ok(average.tailPosition.toArray().every(Number.isFinite));
  assert.equal(runtime.collectUnityPrefabTailChildren(baseSetup.prefabGraphs[0].transforms[0], graph, resolution, 2).length, 2);
});

test("serialized maps discard invalid identifiers and constrain the special Hip collider cache", () => {
  const colliders = new Map([
    [1, makeCollider(1, "body/Position/PositionOffset/Hip/Left_Thigh/CL_Left")],
    [2, makeCollider(2, "body/Position/PositionOffset/Hip/CL_HipSphereCollider")],
    [3, { ...makeCollider(3, "body/Other/CL"), source: {
      ...makeCollider(3, "body/Other/CL").source,
      shape: { capsule: { radius: 0.1 } },
    } }],
  ]);
  const setup = {
    bones: [{ pathId: 1 }, { pathId: "bad" }, {}],
    colliderBindings: [{ sourceSpringBonePathId: 1 }, { sourceSpringBonePathId: null }],
    bindingDecisions: [{ sourceSpringBonePathId: 1 }, {}],
    managerColliderCaches: [{
      managerPathId: 7,
      managerNodeName: "HipManager",
      managerNodePath: "body/Position/PositionOffset/Hip",
      sphereColliderIndexes: [1, 2, 3, 999, "bad"],
      capsuleColliderIndexes: [], panelColliderIndexes: [],
    }, { managerPathId: null }],
  };
  assert.equal(runtime.buildBoneMap(setup).size, 1);
  assert.equal(runtime.buildColliderBindingMap(setup).size, 1);
  assert.equal(runtime.buildBindingDecisionMap(setup).size, 1);
  const caches = runtime.buildManagerColliderCacheMap(setup, colliders);
  assert.deepEqual([...caches.get(7).colliderIndexes], [1, 2]);
  assert.equal(runtime.isRuntimeManagerCacheCollider(setup.managerColliderCaches[0], colliders.get(3)), false);
  assert.equal(runtime.isRuntimeManagerCacheCollider({ managerNodePath: "body/other" }, colliders.get(3)), true);
  assert.match(runtime.managerCacheSummary(caches.get(7)), /HipManager manager cache/);
  assert.equal(runtime.managerCacheSummary(undefined), "no manager cache available");
});

test("active roots, length targets, and candidate maps retain only resolvable members", () => {
  const { root, left } = makeNodeGraph();
  const resolution = runtime.buildNodeResolution(root);
  const active = runtime.buildActiveRootSet({ activeRootProfile: { activeRoots: ["body/", "", null, "face"] } });
  assert.deepEqual([...active], ["body", "face"]);
  assert.equal(runtime.isRuntimePathActive("body/Hair", active), true);
  assert.equal(runtime.isRuntimePathActive("sit_body/Hair", active), false);
  assert.equal(runtime.isRuntimePathActive(null, active), false);
  assert.equal(runtime.isRuntimePathActive(null, new Set()), true);
  assert.deepEqual(runtime.resolveLengthLimitTargets(resolution, {
    runtimePartIndex: 2,
    lengthLimitTargets: [{ nodePath: "body/Hair/Left" }, { nodePath: "missing" }],
  }).map((entry) => entry.node), [left]);

  const c1 = makeCollider(1, "body/CL");
  const c2 = makeCollider(2, "face/CL");
  const byIndex = new Map([[1, c1], [2, c2]]);
  const roots = runtime.buildCandidateRootMap({ body: [1, 999], face: [2], empty: [999] }, byIndex);
  assert.deepEqual([...roots.keys()], ["body", "face"]);
  assert.deepEqual(runtime.filterCollidersByManagerCache([c1, c2], undefined), [c1, c2]);
  assert.deepEqual(runtime.filterCollidersByManagerCache([c1, c2], { colliderIndexes: new Set() }), [c1, c2]);
  assert.deepEqual(runtime.filterCollidersByManagerCache([c1, c2], { colliderIndexes: new Set([2]) }), [c2]);
  assert.deepEqual([...runtime.constrainColliderRootsByManagerCache(roots, { colliderIndexes: new Set([2]) }).keys()], ["face"]);
});

test("collider root selection follows joint, head fallback, defaults, active roots, and failure order", () => {
  const c = makeCollider(1, "body/CL");
  const roots = new Map([["body", [c]], ["face", [c]], ["sit_body", [c]]]);
  assert.equal(runtime.selectUnityColliderRoot({}, {}, { nodePath: "body/Hair" }, undefined, undefined, new Map([["body", [c]]])).reason, "single manager-cache root");
  assert.equal(runtime.selectUnityColliderRoot({}, {}, { nodePath: "face/Hair" }, undefined, undefined, roots).root, "face");
  assert.equal(runtime.selectUnityColliderRoot({ rootSelectionProfile: { defaultBodyRoot: "sit_body/" } }, { partKind: "Head" }, { nodePath: "hair/A" }, undefined, undefined, roots).root, "sit_body");
  assert.equal(runtime.selectHeadColliderRoot({}, { partKind: "Head" }, null, roots).root, "body");
  assert.equal(runtime.selectHeadColliderRoot({}, { partKind: "Body" }, "body", roots), null);
  assert.equal(runtime.selectUnityColliderRoot({}, {}, { nodePath: "hair/A" }, { defaultRoot: "face" }, undefined, roots).reason, "bindingDecision.defaultRoot");
  assert.equal(runtime.selectUnityColliderRoot({ activeRootProfile: { activeRoots: ["missing", "sit_body/"] } }, {}, { nodePath: "hair/A" }, undefined, undefined, roots).reason, "activeRootProfile active root");
  assert.equal(runtime.selectUnityColliderRoot({}, {}, { nodePath: "hair/A" }, undefined, { defaultRoot: "body/" }, roots).reason, "binding.defaultRoot");
  assert.match(runtime.selectUnityColliderRoot({}, {}, { nodePath: "hair/A" }, undefined, { defaultRoot: "missing" }, roots).reason, /not available/);
  assert.equal(runtime.selectUnityColliderRoot({}, {}, { nodePath: "hair/A" }, undefined, undefined, roots).reason, "no matching root");
});

test("collider diagnostics and pose preference preserve exact serialized evidence", () => {
  const body = makeCollider(1, "body/Hip/CL", "same");
  const sitting = makeCollider(2, "sit_body/Hip/CL", "same");
  const unique = makeCollider(3, "face/CL", "unique");
  assert.deepEqual(runtime.preferMatchingPoseColliders([body, sitting, unique], {}, { nodePath: "body/Hair" }), [body, unique]);
  assert.deepEqual(runtime.preferMatchingPoseColliders([body, sitting], {}, { nodePath: "sit_body/Hair" }), [sitting]);
  assert.deepEqual(runtime.preferMatchingPoseColliders([body, sitting], {}, { nodePath: "hair/Hair" }), [body, sitting]);
  assert.equal(runtime.preferredColliderRoot({ partKind: "Head" }, {}), "body/");
  assert.equal(runtime.preferredColliderRoot({}, { nodePath: "face/Hair" }), "body/");
  assert.equal(runtime.preferredColliderRoot({}, {}), null);

  const diagnostic = runtime.buildColliderBindingDiagnostic(
    { partKind: "Head", nodeName: "manager" },
    { partKind: "Hair", nodeName: "bone", nodePath: "hair/bone", pathId: 5 },
    { sourceKind: "binding", colliderFlag: 3 },
    { sourceKind: "decision", colliderFlag: 4 },
    new Map([["body", [body, { ...sitting, source: { ...sitting.source, pathId: null } }]]]),
    "body/", "body", "selected", [body]
  );
  assert.equal(diagnostic.sourceKind, "decision");
  assert.equal(diagnostic.colliderFlag, 4);
  assert.deepEqual(diagnostic.candidateRoots[0].colliderSourcePathIds, [101]);
  const cloned = runtime.cloneColliderBindingDiagnostic(diagnostic);
  cloned.candidateRoots[0].colliderSourcePathIds.push(999);
  assert.deepEqual(diagnostic.candidateRoots[0].colliderSourcePathIds, [101]);
});

test("collider binding distinguishes absent, collider-flag, and direct serialized references", () => {
  const body = makeCollider(1, "body/Hip/CL", "same");
  const sitting = makeCollider(2, "sit_body/Hip/CL", "same");
  const byIndex = new Map([[1, body], [2, sitting]]);
  const manager = { partKind: "Head", nodeName: "manager", pathId: 7 };
  const bone = { partKind: "Hair", nodeName: "bone", nodePath: "face/bone", pathId: 8 };
  assert.deepEqual(runtime.resolveColliderBinding({}, manager, bone, undefined, undefined, undefined, byIndex), { colliders: [], diagnostics: [] });
  const cache = { source: { managerNodeName: "manager", sphereColliderIndexes: [1] }, colliderIndexes: new Set([1]) };
  assert.match(runtime.resolveColliderBinding({}, manager, bone, undefined, undefined, cache, byIndex).diagnostics[0].selectionReason, /no per-bone/);
  const flagged = runtime.resolveColliderBinding(
    { rootSelectionProfile: { defaultBodyRoot: "body" } }, manager, bone,
    { sourceKind: "colliderFlag", collidersByRoot: { body: [1], sit_body: [2] }, defaultRoot: "body" },
    undefined, cache, byIndex
  );
  assert.deepEqual(flagged.colliders, [body]);
  const direct = runtime.resolveColliderBinding({}, manager, bone,
    { sourceKind: "direct", colliders: [1, 2, 999] }, undefined, undefined, byIndex);
  assert.deepEqual(direct.colliders, [body]);
  const decision = runtime.resolveColliderBinding({}, manager, bone, undefined,
    { sourceKind: "direct", selectedColliderIndexes: [2] }, undefined, byIndex);
  assert.deepEqual(decision.colliders, [sitting]);
});

test("miscellaneous spring helpers normalize serialized values and quaternion math", () => {
  assert.equal(runtime.angleLimitFromSource(null), null);
  assert.equal(runtime.angleLimitFromSource({ active: false }), null);
  assert.deepEqual(runtime.angleLimitFromSource({ active: true }), { active: true, min: 0, max: 0 });
  assert.deepEqual(runtime.angleLimitFromSource({ active: true, min: -2, max: 3 }), { active: true, min: -2, max: 3 });
  assert.equal(runtime.readStringSet("bad").size, 0);
  assert.deepEqual([...runtime.readStringSet(["Hair", 1, "", "Hair"])], ["Hair", ""]);
  assert.equal(runtime.containsAnimatedBoneName("LongHairA", new Set(["Hair"])), true);
  assert.equal(runtime.containsAnimatedBoneName("Hair", new Set(["Hair"])), true);
  assert.equal(runtime.containsAnimatedBoneName("Bone", new Set(["", "Hair"])), false);
  const node = new THREE.Group();
  node.name = "LongHairA";
  assert.equal(runtime.isBoneAnimated({}, node, { animatedBoneNames: [] }), false);
  assert.equal(runtime.isBoneAnimated({}, node, { animatedBoneNames: ["Hair"] }), true);
  assert.equal(runtime.isBoneAnimated({ nodeName: "HairSource" }, new THREE.Group(), { animatedBoneNames: ["Hair"] }), true);
  assert.equal(runtime.getEffectiveDynamicRatio({ isAnimated: false, dynamicRatio: 0.25 }), 1);
  assert.equal(runtime.getEffectiveDynamicRatio({ isAnimated: true, dynamicRatio: 0.25 }), 0.25);
  assert.equal(runtime.calcUtjManagerTimeStep(0.02, 50, 1), 0.02);
  assert.equal(runtime.calcUtjManagerTimeStep(0.02, 0, 0.5), 0.01);
  assert.equal(runtime.readRuntimeUnitySetup0414({ pjskSpringBone: { runtimeUnitySetup: { version: "0414" } } }).version, "0414");
  assert.equal(runtime.readRuntimeUnitySetup0414({ PjskSpringBone: { RuntimeUnitySetup: { version: 414 } } }).version, 414);
  assert.equal(runtime.readRuntimeUnitySetup0414({ pjskSpringBone: { runtimeUnitySetup: { version: 1 } } }), null);
  assert.equal(runtime.readRuntimeUnitySetup0414(null), null);

  assert.equal(runtime.rootNameFromPath("body/Hair"), "body");
  assert.equal(runtime.rootNameFromPath("body"), "body");
  assert.equal(runtime.rootNameFromPath(null), null);
  assert.equal(runtime.normalizeRootName("body/"), "body");
  assert.equal(runtime.normalizeRootName(""), null);
  assert.deepEqual(runtime.vectorFromUnity([1, 2, 3]).toArray(), [-1, 2, 3]);
  assert.deepEqual(runtime.vectorFromUnity({ X: 1, Y: 2, Z: 3 }).toArray(), [-1, 2, 3]);
  assert.equal(runtime.normalizeRuntimeAxis(v(0, 0, 0)), null);
  assert.deepEqual(runtime.normalizeRuntimeAxis(v(2, 0, 0)).toArray(), [1, 0, 0]);
  const axisNode = new THREE.Group();
  assert.equal(runtime.resolveRuntimeBoneAxis(axisNode, v(0, 0, 0)).source, "fallback-local-tip");
  assert.equal(runtime.resolveRuntimeBoneAxis(axisNode, v(0, 1, 0)).source, "computed-local-tip");

  const raw = { Strength: 2, Enabled: 0, Offset: [1, 2, 3], Manager: { m_PathID: 7 } };
  assert.equal(runtime.readRawNumber(raw, "strength", 1), 2);
  assert.equal(runtime.readRawNumber({ strength: Number.NaN }, "strength", 1), 1);
  assert.equal(runtime.readRawBoolean(raw, "enabled", true), false);
  assert.equal(runtime.readRawBoolean({ enabled: true }, "enabled", false), true);
  assert.equal(runtime.readRawBoolean({}, "enabled", true), true);
  assert.deepEqual(runtime.readUnityRawVector(raw, "offset").toArray(), [-1, 2, 3]);
  assert.deepEqual(runtime.readUnityRawVector({}, "offset").toArray(), [0, 0, 0]);
  assert.equal(runtime.readRawObjectPathId(raw, "manager"), 7);
  assert.equal(runtime.readRawObjectPathId({}, "manager"), null);
  assert.equal(runtime.capitalize("word"), "Word");
  assert.equal(runtime.capitalize(""), "");
  assert.ok(Math.abs(runtime.addPeriodically(0.9, 0.3, 1) - 0.2) < 1e-12);
  assert.equal(runtime.addPeriodically(1, 2, 0), 3);
  assert.equal(runtime.finiteOverride(Number.NaN, 4), 4);
  assert.equal(runtime.finiteOverride(2, 4), 2);
  assert.equal(runtime.readFiniteNumber(Infinity), null);
  assert.equal(runtime.readFiniteNumber(2), 2);
  assert.equal(runtime.asRecord(null), null);
  assert.deepEqual(runtime.asRecord({ a: 1 }), { a: 1 });

  const scaleNode = new THREE.Group();
  scaleNode.scale.set(2, 3, 4);
  scaleNode.updateMatrixWorld(true);
  assert.equal(runtime.worldScaleX(scaleNode), 2);
  assert.equal(runtime.matrixWorldXScale(scaleNode), 2);
  assert.equal(runtime.matrixXDirectionLength(new THREE.Matrix4().makeScale(3, 4, 5)), 3);
  assert.ok(runtime.makeNormalDirectionMatrix(new THREE.Matrix4().makeScale(2, 4, 8)).elements.every(Number.isFinite));
  assert.equal(runtime.sourceColliderOrder({ shape: { sphere: {} } }), 0);
  assert.equal(runtime.sourceColliderOrder({ shape: { capsule: {} } }), 1);
  assert.equal(runtime.sourceColliderOrder({ shape: { panel: {} } }), 2);

  const from = new THREE.Quaternion();
  const to = new THREE.Quaternion().setFromAxisAngle(v(0, 1, 0), Math.PI / 2);
  const opposite = new THREE.Quaternion(-to.x, -to.y, -to.z, -to.w);
  assert.ok(runtime.lerpQuaternionNormalized(from, to, 2).angleTo(to) < 1e-12);
  assert.ok(runtime.lerpQuaternionNormalized(from, opposite, 1).angleTo(to) < 1e-12);
  assert.equal(runtime.quaternionsAlmostEqual(to, to.clone()), true);
  assert.equal(runtime.quaternionsAlmostEqual(to, from), false);
});

test("debug offset selection supports all, empty, and multi-field substring filters", () => {
  const offsets = [{
    name: "HairA", path: "body/HairA", springName: "Head:manager",
    sourceBoneName: null, sourceBonePath: "face/source", pivotSourceName: "Pivot",
    pivotSourcePath: null, pivotResolvedPath: "body/Pivot",
  }, { name: "Skirt", path: "body/Skirt" }];
  assert.equal(runtime.selectDebugOffsets(offsets, { springDebugAllOffsets: true }), offsets);
  assert.deepEqual(runtime.selectDebugOffsets(offsets, {}), []);
  assert.deepEqual(runtime.selectDebugOffsets(offsets, { springDebugBones: ["  HAIR ", ""] }), [offsets[0]]);
  assert.deepEqual(runtime.selectDebugOffsets(offsets, { springDebugBones: ["pivot"] }), [offsets[0]]);
});
