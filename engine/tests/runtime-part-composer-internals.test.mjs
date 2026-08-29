import assert from "node:assert/strict";
import test from "node:test";

import { runtimePartComposerInternals as composer } from "../dist/haruki-3d-engine-internal.js";

const makePart = (partType, extra = {}) => ({
  runtime: { part: { partType, modelAssetbundleName: `${partType}-bundle` } },
  partIndex: 0,
  partType,
  setup: {},
  prefabGraph: null,
  managers: [],
  bones: [],
  extraBones: [],
  colliders: [],
  colliderBindings: [],
  managerColliderCaches: [],
  constraints: [],
  activeRoots: [partType === "body" ? "body" : "face"],
  ...extra,
});

test("deferred collider warnings disappear only after every flag binding resolves", () => {
  const warnings = [
    "hair has colliderFlag 1 but no body colliders matched runtime CL_* prefixes",
    "keep me",
  ];
  assert.equal(composer.discardResolvedDeferredColliderWarnings(warnings, []), warnings);
  assert.equal(composer.discardResolvedDeferredColliderWarnings(warnings, [
    { sourceKind: "colliderFlag", colliders: [] },
  ]), warnings);
  assert.deepEqual(composer.discardResolvedDeferredColliderWarnings(warnings, [
    { sourceKind: "direct", colliders: [] },
    { sourceKind: "colliderFlag", colliders: [1] },
  ]), ["keep me"]);
});

test("head optional mounting traverses active roots, applies controllers, and trims unrelated prefab data", () => {
  const attach = { pathId: 10, name: "Head", transformPath: "body/Head", parentPathId: 1, childPathIds: [] };
  const body = makePart("body", {
    activeRoots: ["body"],
    prefabGraph: {
      transforms: [
        { pathId: 1, name: "body", transformPath: "body", parentPathId: null, childPathIds: [9, 10] },
        { pathId: 9, name: "Inactive", transformPath: "body/Inactive", parentPathId: 1, gameObjectPathId: 90, childPathIds: [] },
        attach,
      ],
      gameObjects: [{ pathId: 90, activeSelf: false }],
    },
  });
  const optionalRuntime = {
    part: { partType: "head_optional", modelAssetbundleName: "optional-hat" },
    mount: {
      attachNode: "folder\\Head",
      accessoryTransformAdjustments: {
        "001/002": {
          position: { X: 1, Y: 2, Z: 3 },
          rotationEulerDegrees: { x: 10, y: 20, z: 30 },
          scale: { x: -2, y: 3, z: -4 },
        },
      },
    },
  };
  const graph = {
    transforms: [
      { pathId: 20, name: "optional", transformPath: "optional", parentPathId: null, childPathIds: [21] },
      { pathId: 21, name: "controller", transformPath: "optional/controller", parentPathId: 20, childPathIds: [] },
      { pathId: 22, name: "discard", transformPath: "other", parentPathId: null, childPathIds: [] },
    ],
    gameObjects: [{ transformPath: "optional" }, { transformPath: "other" }],
    renderers: [{ transformPath: "optional/controller" }, { transformPath: "other" }],
    animators: [{ transformPath: "optional" }, { transformPath: "other" }],
    constraints: [{ transformPath: "optional/controller" }, { transformPath: "other" }],
    monoBehaviours: [
      { scriptName: "CharacterAccessoryTransformController", transformPath: "optional/controller" },
      { scriptName: "Other", transformPath: "other" },
    ],
  };
  const optional = makePart("head_optional", {
    runtime: optionalRuntime,
    prefabGraph: graph,
    activeRoots: ["optional"],
  });
  const setup = { warnings: [] };
  composer.mountHeadOptionalPrefabGraphs([body, optional], setup, "001/002");
  assert.equal(graph.headOptionalAttachPath, "body/Head");
  assert.equal(graph.headOptionalPrefabRootPath, "optional");
  assert.equal(graph.headOptionalControllerPath, "optional/controller");
  assert.equal(graph.transforms.length, 2);
  assert.deepEqual(graph.rootTransformPathIds, [20]);
  assert.equal(graph.transforms[0].parentPathId, 10);
  assert.deepEqual(attach.childPathIds, [20]);
  assert.deepEqual(graph.transforms[1].localScale, { X: 2, Y: 3, Z: 4 });
  assert.deepEqual(setup.warnings, []);

  assert.equal(composer.findHeadOptionalAttachTransform([body], "Inactive"), null);
  assert.equal(composer.findHeadOptionalAttachTransform([optional], "Head"), null);
});

test("head optional mounting reports missing roots and missing controller targets, with identity fallback", () => {
  const body = makePart("body", {
    activeRoots: ["body"],
    prefabGraph: { transforms: [
      { pathId: 1, name: "body", transformPath: "body", parentPathId: null, childPathIds: [2] },
      { pathId: 2, name: "Head", transformPath: "body/Head", parentPathId: 1, childPathIds: [] },
    ] },
  });
  const missing = makePart("head_optional", {
    runtime: { part: { partType: "head_optional", modelAssetbundleName: "missing" }, mount: {} },
    prefabGraph: { transforms: [] }, activeRoots: ["optional"],
  });
  const missingController = makePart("head_optional", {
    runtime: { part: { partType: "head_optional", modelAssetbundleName: "broken" }, mount: { attachNode: "Head" } },
    prefabGraph: {
      transforms: [{ pathId: 20, name: "optional", transformPath: "optional", parentPathId: null }],
      monoBehaviours: [{ scriptName: "CharacterAccessoryTransformController", transformPath: "optional/missing" }],
    }, activeRoots: ["optional"],
  });
  const plain = makePart("head_optional", {
    runtime: { part: { partType: "head_optional", modelAssetbundleName: "plain" }, mount: { attachNode: "Head" } },
    prefabGraph: {
      transforms: [{ pathId: 30, name: "optional", transformPath: "optional", parentPathId: null }],
      monoBehaviours: [],
    }, activeRoots: ["optional"],
  });
  const setup = { warnings: [] };
  composer.mountHeadOptionalPrefabGraphs([body, missing, missingController, plain], setup, null);
  assert.equal(setup.warnings.length, 2);
  assert.match(setup.warnings[0], /was not instantiated/);
  assert.match(setup.warnings[1], /controller target/);
  assert.deepEqual(plain.prefabGraph.transforms[0].localPosition, { X: 0, Y: 0, Z: 0 });
  assert.deepEqual(plain.prefabGraph.transforms[0].localRotation, { x: 0, y: 0, z: 0, w: 1 });
});

test("path, transform, FUnit, and body-head assembly helpers cover fallback forms", () => {
  assert.equal(composer.isSameOrDescendantPath("optional", "optional"), true);
  assert.equal(composer.isSameOrDescendantPath("optional/a", "optional"), true);
  assert.equal(composer.isSameOrDescendantPath("other", "optional"), false);
  const target = {};
  composer.applyAccessoryControllerTransform(target, null);
  assert.deepEqual(target.localPosition, { X: 0, Y: 0, Z: 0 });
  assert.deepEqual(target.localScale, { X: 1, Y: 1, Z: 1 });
  const quaternion = composer.unityQuaternionFromEulerDegrees({ x: 0, y: 0, z: 0 });
  assert.deepEqual(quaternion, { x: 0, y: 0, z: 0, w: 1 });

  const funit = composer.mergeRuntimeFUnitSummaries([
    { springBone: { funit: { present: true, scriptCount: 2.9, springBoneCount: -2, detectedScripts: ["B", "A"] } } },
    { springBone: { funit: { scriptCount: Number.NaN, springManagerCount: 2, detectedScripts: ["A", 1] } } },
    { springBone: { funit: null } },
  ]);
  assert.equal(funit.present, true);
  assert.equal(funit.scriptCount, 2);
  assert.equal(funit.springBoneCount, 0);
  assert.deepEqual(funit.detectedScripts, ["A", "B"]);

  const neck = "body/Position/PositionOffset/Hip/Waist/Spine/Chest/Neck";
  const graphs = [{ transforms: [
    { transformPath: neck }, { transformPath: "face" }, { transformPath: "face/Position" },
  ] }];
  assert.equal(composer.resolveComposedBodyAttachPath(graphs), neck);
  assert.equal(composer.resolveComposedHeadOriginPath(graphs), "face/Position");
  assert.equal(composer.resolveComposedBodyHeadAssembly(graphs).parentAttachPath, neck);
  assert.equal(composer.resolveComposedBodyHeadAssembly([]), null);
  assert.equal(composer.hasRuntimeSetupTransformPath([null, { transforms: [1, { transformPath: "x" }] }], "x"), true);
});

test("part remapping scopes roots and every serialized identifier", () => {
  assert.deepEqual(composer.selectRuntimePartActiveRoots("body", ["face", "body"]), ["body"]);
  assert.deepEqual(composer.selectRuntimePartActiveRoots("head", ["face"]), ["face"]);
  assert.deepEqual(composer.selectRuntimePartActiveRoots("hair", ["body"]), ["body"]);
  assert.deepEqual(composer.selectRuntimePartActiveRoots("head_optional", ["optional"]), ["optional"]);
  assert.deepEqual(composer.selectRuntimePartActiveRoots("head_optional", []), ["face"]);
  assert.deepEqual(composer.filterRuntimeRecordsByActiveRoots([
    { nodePath: "body/A" }, { poseRoot: "face" }, { nodePath: null },
  ], ["body"]), [{ nodePath: "body/A" }, { nodePath: null }]);

  assert.equal(composer.remapNumericId(7, 2), 3_000_000_007);
  const cloned = composer.cloneArrayWithPartPrefix([{
    pathId: 1, index: 2, bonePathIds: [3, "x"], forceProviders: [
      { sourcePathId: 4, springManagerPathId: 5 }, "raw",
    ], collidersByRoot: { body: [6, "x"], face: "bad" },
    candidateRoots: { body: [7] },
  }, 4], 1, "hair");
  assert.equal(cloned[0].runtimePartIndex, 1);
  assert.equal(cloned[0].pathId, 2_000_000_001);
  assert.deepEqual(cloned[0].bonePathIds, [2_000_000_003, "x"]);
  assert.equal(cloned[0].forceProviders[0].sourcePathId, 2_000_000_004);
  assert.equal(cloned[1], 4);
  assert.deepEqual(composer.remapColliderRoots({ body: [1, "x"], bad: null }, 0), { body: [1_000_000_001], bad: [] });

  const extras = composer.remapRuntimeExtraBones([
    { GameObject: { PathId: 1, TransformPath: "face/A" }, ReferenceBone: { pathId: 2 } },
    { gameObject: null, referenceBone: "bad" },
  ], 0, "head");
  assert.equal(extras[0].GameObject.PathId, 1_000_000_001);
  assert.equal(extras[0].referenceBone.pathId, 1_000_000_002);
  assert.equal(extras[0].poseRoot, "face");
  assert.equal(composer.remapRuntimeObjectRef(null, 0), null);

  assert.deepEqual(composer.filterColliderBindingsByActiveBones([
    { sourceSpringBonePathId: 1 }, { sourceSpringBonePathId: 2 }, {},
  ], [{ pathId: 1 }, {}]), [{ sourceSpringBonePathId: 1 }, {}]);
  assert.deepEqual(composer.filterManagerColliderCachesByActiveManagers([
    { managerPathId: 1 }, { managerPathId: 2 }, {},
  ], [{ pathId: 1 }, {}]), [{ managerPathId: 1 }, {}]);
});

test("constraint and prefab remapping preserve active owners and nested source ids", () => {
  const constraints = composer.remapRuntimeConstraints({ constraints: [
    { pathId: 1, ownerPath: "face/A", worldUpObjectPathId: 2, sources: [{ sourcePathId: 3 }, "bad"] },
    { pathId: 4, ownerPath: "body/A", sources: [] },
    { pathId: 5, ownerPath: null, sources: [] },
  ] }, 1, "head", ["face"]);
  assert.equal(constraints.length, 1);
  assert.equal(constraints[0].pathId, 2_000_000_001);
  assert.equal(constraints[0].worldUpObjectPathId, 2_000_000_002);
  assert.equal(constraints[0].sources[0].sourcePathId, 2_000_000_003);
  assert.equal(composer.remapRuntimeConstraints(undefined, 0, "body", ["body"]).length, 0);

  assert.equal(composer.remapPrefabGraph(null, 0), null);
  const graph = composer.remapPrefabGraph({
    transforms: [{ pathId: 1, PathId: 2, parentPathId: 3, childPathIds: [4, "x"] }, "bad"],
    monoBehaviours: [{ pathId: 5 }, "bad"],
  }, 2);
  assert.equal(graph.runtimePartIndex, 2);
  assert.equal(graph.transforms[0].pathId, 3_000_000_001);
  assert.equal(graph.transforms[0].PathId, 3_000_000_002);
  assert.equal(graph.transforms[0].parentPathId, 3_000_000_003);
  assert.deepEqual(graph.transforms[0].childPathIds, [3_000_000_004, "x"]);
  assert.equal(graph.monoBehaviours[0].pathId, 3_000_000_005);
});

test("manager inference and full part remap retain only the chosen runtime root", () => {
  const managers = [{ pathId: 1, nodePath: "face/Hair" }, { pathId: 2, nodePath: "body/Skirt" }, { nodePath: "missing" }];
  const bones = [{ pathId: 10, nodePath: "face/Hair/A" }, { pathId: 11, nodePath: "body/Skirt/A" }, { nodePath: "face/Hair/B" }];
  const caches = [{ managerPathId: 1 }, { managerPathId: 2 }, {}];
  composer.withInferredSpringManagerBoneRefs(managers, bones, caches);
  assert.deepEqual(managers[0].bonePathIds, [10]);
  assert.deepEqual(caches[0].springBonePathIds, [10]);
  assert.equal(composer.isSameOrDescendantRuntimePath("face/A", "face"), true);
  assert.equal(composer.isSameOrDescendantRuntimePath("face", "face"), true);
  assert.equal(composer.isSameOrDescendantRuntimePath(null, "face"), false);

  const runtime = {
    part: { partType: "hair", modelAssetbundleName: "hair" },
    springBone: {
      activeRootProfile: { activeRoots: ["body", "face"] },
      managers: [{ pathId: 1, nodePath: "face/Hair" }, { pathId: 2, nodePath: "body/Skirt" }],
      bones: [{ pathId: 10, nodePath: "face/Hair/A" }, { pathId: 11, nodePath: "body/Skirt/A" }],
      colliders: [{ index: 1, nodePath: "face/CL" }, { index: 2, nodePath: "body/CL" }],
      colliderBindings: [{ sourceSpringBonePathId: 10 }, { sourceSpringBonePathId: 11 }],
      managerColliderCaches: [{ managerPathId: 1 }, { managerPathId: 2 }],
      prefabGraph: { transforms: [], monoBehaviours: [] },
      constraintSetup: { constraints: [] },
    },
  };
  const part = composer.remapRuntimePart(runtime, 0);
  assert.deepEqual(part.activeRoots, ["face"]);
  assert.equal(part.managers.length, 1);
  assert.equal(part.bones.length, 1);
  assert.equal(part.colliders.length, 1);
  assert.equal(part.prefabGraph.runtimePartIndex, 0);
});

test("collider rebinding synthesizes missing flags and rebuilds decisions and manager caches", () => {
  const bodyColliders = [
    { index: 1, pathId: 101, nodePath: "body/Hip", nodeName: "CL_HipSphere", scriptName: "SpringSphereCollider" },
    { index: 2, pathId: 102, nodePath: "sit_body/Chest", nodeName: "CL_ChestCapsule", scriptName: "SpringCapsuleCollider" },
    { index: 3, pathId: 103, nodePath: "guitar_body/Arm", nodeName: "CL_Left_ArmPanel", scriptName: "SpringPanelCollider" },
    { index: null, nodeName: "CL_HipIgnored" },
  ];
  const body = makePart("body", { colliders: bodyColliders });
  const hair = makePart("hair", {
    bones: [
      { pathId: 10, nodePath: "face/Hair", poseRoot: "face", colliderFlag: 3 },
      { pathId: 11, colliderFlag: 0 }, { colliderFlag: 1 },
      { pathId: 12, colliderFlag: 1 },
    ],
    colliderBindings: [{ sourceSpringBonePathId: 12, sourceKind: "colliderFlag" }],
    managerColliderCaches: [{ managerPathId: 20, springBonePathIds: [10, 12], sphereColliderIndexes: [999] }],
  });
  const synthesized = composer.synthesizeMissingColliderFlagBindings([body, hair]);
  assert.equal(synthesized.length, 1);
  assert.deepEqual(synthesized[0].matchedPrefixes, ["CL_Hip", "CL_Chest"]);

  const selected = composer.selectBodyCollidersForColliderFlag(synthesized[0], bodyColliders);
  assert.deepEqual(selected.byRoot, { body: [1], sit_body: [2] });
  assert.equal(selected.defaultRoot, "body");
  assert.equal(composer.matchesColliderFlagPrefix(bodyColliders[0], []), false);
  assert.equal(composer.matchesColliderFlagPrefix(bodyColliders[0], ["CL_Hip"]), true);
  assert.equal(composer.matchesColliderFlagPrefix({ nodeName: null }, ["CL_Hip"]), false);

  const rebuilt = composer.rebuildColliderBindings([body, hair]);
  assert.ok(rebuilt.some((binding) => binding.originalSourceKind === "deferred_body_colliderFlag"));
  const decisions = composer.rebuildBindingDecisions(hair.bones, rebuilt);
  assert.ok(decisions.some((decision) => decision.sourceKind === "colliderFlag"));
  assert.equal(composer.rebuildBindingDecisions([], [{}]).length, 0);

  const byIndex = new Map(bodyColliders.filter((entry) => typeof entry.index === "number").map((entry) => [entry.index, entry]));
  const headCache = composer.rebuildHeadManagerColliderCache(
    { springBonePathIds: [10], sphereColliderIndexes: [999], capsuleColliderIndexes: [], panelColliderIndexes: [] },
    [{ sourceSpringBonePathId: 10, sourceKind: "colliderFlag", colliders: [1, 2, 3, 999] }], byIndex
  );
  assert.equal(headCache.reason, "viewer_composed_head_body_collider_cache");
  assert.deepEqual(headCache.sphereColliderIndexes, [1]);
  assert.deepEqual(headCache.capsuleColliderIndexes, [2]);
  assert.deepEqual(headCache.panelColliderIndexes, [3]);
  const filtered = composer.filterManagerCache({ sphereColliderIndexes: [1, 999], capsuleColliderIndexes: [2], panelColliderIndexes: [3] }, byIndex);
  assert.deepEqual(filtered.sphereColliderIndexes, [1]);
  assert.equal(filtered.reason, "viewer_composed_active_parts_manager_cache");
});

test("collider roots, priorities, native mesh ids, and optional mounting warnings remain deterministic", () => {
  const roots = composer.collidersByRoot([
    { index: 3, nodePath: "face/A" }, { index: 1, nodePath: "body/A" },
    { index: 2, poseRoot: "body" }, { index: 1, nodePath: "body/B" }, {},
  ]);
  assert.deepEqual(roots, { face: [3], body: [1, 2] });
  assert.equal(composer.hasColliderRoots(roots), true);
  assert.equal(composer.hasColliderRoots({ body: [] }), false);
  assert.equal(composer.hasColliderRoots(null), false);
  assert.deepEqual(composer.firstColliderRoot({ face: [3], guitar_body: [2], sit_body: [1], body: [0] }), { root: "body", indexes: [0] });
  assert.equal(composer.rootPriority("body"), 0);
  assert.equal(composer.rootPriority("sit_body"), 1);
  assert.equal(composer.rootPriority("guitar_body"), 2);
  assert.equal(composer.rootPriority("face"), 10);
  assert.equal(composer.normalizeRootName("  "), "body");
  assert.equal(composer.firstPathSegment("/face//Hair"), "face");
  assert.equal(composer.firstPathSegment(null), null);

  const mesh = composer.remapNativeMeshIds({
    rendererPathId: 1, rendererTransformPathId: 2, rootBonePathId: 3,
    bonePathIds: [4, "x"], name: "mesh",
  }, 1);
  assert.equal(mesh.rendererPathId, 2_000_000_001);
  assert.deepEqual(mesh.bonePathIds, [2_000_000_004, "x"]);
  const setup = { warnings: ["existing"], prefabGraphs: [] };
  const native = composer.mergeNativeMeshes([
    { part: { partType: "body" }, nativeMeshes: { meshes: [{ meshName: "body", rendererPathId: 1 }] } },
    { part: { partType: "head_optional" }, nativeMeshes: { meshes: [{ meshName: "hat", rendererTransformPath: "optional/hat" }] } },
  ], setup);
  assert.equal(native.meshes.length, 1);
  assert.equal(native.warnings.length, 2);
});

test("face id, accessory adjustment, texture inheritance, and value readers cover malformed input", () => {
  assert.equal(composer.extractFaceIdFromBundlePath("live/face/001/002.bundle"), "001/002");
  assert.equal(composer.extractFaceIdFromBundlePath("live\\face\\003\\004.bundle"), "003/004");
  assert.equal(composer.extractFaceIdFromBundlePath("invalid"), null);
  const runtimes = [
    { part: { partType: "head_optional", modelAssetbundleName: "skip" } },
    { part: { partType: "head" }, source: { bundlePath: "face/001/002.bundle" } },
  ];
  assert.equal(composer.resolveHeadOptionalFaceId(runtimes), "001/002");
  assert.equal(composer.resolveHeadOptionalFaceId([{ part: { partType: "hair", modelAssetbundleName: "face/003/004" } }]), "003/004");
  assert.equal(composer.resolveHeadOptionalFaceId([{ part: { partType: "body" } }]), null);
  const optional = { mount: { accessoryTransformAdjustments: { "001/002": { scale: { x: 2 } }, bad: "x" } } };
  assert.deepEqual(composer.resolveAccessoryTransformAdjustment(optional, "001/002"), { scale: { x: 2 } });
  assert.equal(composer.resolveAccessoryTransformAdjustment(optional, "bad"), null);
  assert.equal(composer.resolveAccessoryTransformAdjustment(optional, null), null);
  assert.deepEqual(composer.readAccessoryTransformAdjustments({}), {});
  assert.deepEqual(composer.readVectorLike({ X: 1, y: Number.NaN }, 2, 3, 4), { x: 1, y: 3, z: 4 });
  assert.equal(composer.readNumber("1", 2), 2);
  assert.equal(composer.normalizePathSegment("a\\b/c"), "c");
  assert.equal(composer.normalizePathSegment(""), null);

  const role = {
    source: { packagePath: "role.pkg" },
    materialSlots: [
      { materialKind: "eye", mainTex: "role-eye.png" },
      { materialKind: "eyelight", mainTex: "role-light.png" },
    ],
  };
  const resolveUrl = (path) => `cdn/${path}`;
  const selected = [{ materialKind: "eye" }, { materialKind: "eyelight", mainTex: "own.png" }, { materialKind: "face" }];
  assert.equal(composer.inheritMissingRoleEyeTextures(selected, null, resolveUrl), selected);
  const inherited = composer.inheritMissingRoleEyeTextures(selected, role, resolveUrl);
  assert.equal(inherited[0].mainTex, "cdn/role-eye.png");
  assert.equal(inherited[1].mainTex, "own.png");
  assert.equal(inherited[2], selected[2]);

  const slots = composer.resolveMaterialSlotTextureUrls({
    mainTex: "a", shadowTex: null, valueTex: "b", faceShadowTex: "c",
    rawMaterial: { textureProperties: [{ uri: "d" }, { uri: null }] },
  }, resolveUrl);
  assert.equal(slots.mainTex, "cdn/a");
  assert.equal(slots.shadowTex, null);
  assert.equal(slots.rawMaterial.textureProperties[0].uri, "cdn/d");
  assert.equal(composer.resolveMaybeUrl(undefined, resolveUrl), undefined);
  assert.equal(composer.resolveRequiredUrl(undefined, resolveUrl), "");
});

test("generic readers clone and deduplicate only valid serialized values", () => {
  const original = { a: [{ b: 1 }] };
  const cloned = composer.cloneRecord(original);
  cloned.a[0].b = 2;
  assert.equal(original.a[0].b, 1);
  assert.equal(composer.isRecord({}), true);
  assert.equal(composer.isRecord([]), false);
  assert.equal(composer.isRecord(null), false);
  assert.deepEqual(composer.asRecord("bad"), {});
  assert.deepEqual(composer.readStringArray(["a", 1, "b"]), ["a", "b"]);
  assert.deepEqual(composer.readStringArray(null), []);
  assert.deepEqual(composer.readRecordArray([{}, [], null, { a: 1 }]), [{}, { a: 1 }]);
  assert.deepEqual(composer.readRecordArray(null), []);
  assert.deepEqual(composer.readNumberArray([1, "2", 3]), [1, 3]);
  assert.deepEqual(composer.readNumberArray(null), []);
  assert.deepEqual(composer.uniqueStrings(["b", "a", "b"]), ["b", "a"]);
  assert.deepEqual(composer.uniqueNumbers([3, 1, 3, 2]), [1, 2, 3]);
});
