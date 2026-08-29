import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import {
  cloneBodyShaderMaterial,
  createGroupedLayerMesh,
  createSekaiBodyMaterial,
  createSekaiThroughHairOverlayMesh,
  extractMaterialColorMap,
  getHeadLayerRenderOrder,
  getSekaiPreviewRimDirection,
  loadRuntimeTexture,
  normalizeMeshSlotName,
  sortHeadMeshGroupsByMaterialKind,
  syncReplacementTextureFromOriginal,
  tuneLightingForPreview,
} from "../dist/haruki-3d-engine-internal.js";

function bodyTemplate() {
  return createSekaiBodyMaterial({
    baseColor: "#ffffff", shadowColor: "#808080",
    lightDirection: new THREE.Vector3(0, 1, 0), lightIntensity: 1,
    ambientIntensity: 1, shadowThreshold: 0.5, shadowWeight: 1,
  });
}

test("runtime texture loading deduplicates in flight requests and recovers after errors", async () => {
  let calls = 0;
  let resolveTexture;
  const loader = {
    loadAsync() {
      calls += 1;
      return new Promise((resolve) => { resolveTexture = resolve; });
    },
  };
  assert.equal(await loadRuntimeTexture(loader, undefined), null);
  const first = loadRuntimeTexture(loader, "texture.ktx2");
  const second = loadRuntimeTexture(loader, "texture.ktx2");
  assert.equal(calls, 1);
  resolveTexture(new THREE.Texture());
  const [a, b] = await Promise.all([first, second]);
  assert.equal(a, b);
  assert.equal(a.wrapS, THREE.RepeatWrapping);
  assert.equal(a.wrapT, THREE.RepeatWrapping);
  assert.equal(a.flipY, false);
  assert.equal(a.colorSpace, THREE.SRGBColorSpace);

  const noColor = loadRuntimeTexture(loader, "texture.ktx2", THREE.NoColorSpace);
  resolveTexture(new THREE.Texture());
  await noColor;
  const failing = { loadAsync: async () => { throw new Error("missing"); } };
  assert.equal(await loadRuntimeTexture(failing, "missing.ktx2"), null);
  assert.equal(await loadRuntimeTexture(failing, "missing.ktx2"), null);
});

test("body clone accepts explicit lighting overrides and source-uniform fallbacks", () => {
  const source = bodyTemplate();
  const cloned = cloneBodyShaderMaterial(source, {
    mainTex: new THREE.Texture(), shadowTex: null, valueTex: new THREE.Texture(),
    lighting: {
      useValueTex: false, sekaiShadowThreshold: 0.2, specularPower: 4,
      rimThreshold: 0.3, shadowTexWeight: 0.4, fadeMode: 1,
      hueSinAngle: 0.1, hueCosAngle: 0.9, shadowWidth: 0.6,
      useLambert: false, headNormalBlend: 0.2, saturation: 0.7,
      value: 0.8, contrast: 0.9, partsAmbientColor: "#123456",
      reflectionBlendColor: "#654321",
    },
    shadowWidthOverride: 0.1, valueShadowInfluence: 0.2,
    hairShadowEnabled: true, useLambert: false, headPosition: new THREE.Vector3(1, 2, 3),
    bodyDebugMode: 2, alphaCutoff: 0.25,
  });
  assert.equal(cloned.uniforms.uUseValueTex.value, 0);
  assert.equal(cloned.uniforms.uUseLambert.value, 0);
  assert.equal(cloned.uniforms.uHairShadowEnabled.value, 1);
  assert.equal(cloned.uniforms.uAlphaCutoff.value, 0.25);
  assert.ok(getSekaiPreviewRimDirection().length() > 0);

  const fallback = cloneBodyShaderMaterial(source, {});
  assert.ok(fallback instanceof THREE.ShaderMaterial);
});

test("texture synchronization handles basic, shader, empty, and unsupported materials", () => {
  const original = new THREE.Texture();
  original.wrapS = THREE.MirroredRepeatWrapping;
  original.wrapT = THREE.ClampToEdgeWrapping;
  original.offset.set(0.2, 0.3);
  original.repeat.set(2, 3);
  original.center.set(0.5, 0.5);
  original.rotation = 0.4;
  original.anisotropy = 8;
  original.updateMatrix();
  const replacement = new THREE.Texture();
  const basic = new THREE.MeshBasicMaterial({ map: replacement });
  syncReplacementTextureFromOriginal(basic, original);
  assert.equal(replacement.wrapS, original.wrapS);
  assert.equal(replacement.rotation, original.rotation);
  assert.equal(extractMaterialColorMap(basic), replacement);
  assert.equal(extractMaterialColorMap(new THREE.MeshStandardMaterial()), null);

  const shader = new THREE.ShaderMaterial({
    uniforms: { uMainTex: { value: replacement }, uMainTexTransform: { value: new THREE.Matrix3() } },
  });
  syncReplacementTextureFromOriginal(shader, original);
  assert.equal(shader.uniforms.uMainTexTransform.value.equals(replacement.matrix), true);
  syncReplacementTextureFromOriginal(new THREE.ShaderMaterial({ uniforms: {} }), original);
  syncReplacementTextureFromOriginal(new THREE.MeshStandardMaterial(), original);
  syncReplacementTextureFromOriginal(basic, null);
});

test("mesh name and render order tables cover every material family", () => {
  assert.equal(normalizeMeshSlotName("MyFaceMesh"), "face");
  assert.equal(normalizeMeshSlotName("Hair_Main"), "hair");
  assert.equal(normalizeMeshSlotName("ACC_Ribbon"), "acc");
  assert.equal(normalizeMeshSlotName("BodySkin"), "body");
  assert.equal(normalizeMeshSlotName("Other"), "other");
  const expected = new Map([
    ["face_sdf", 2000], ["face", 2000], ["accessory", 2000], ["body", 2000], ["eyelight", 2000],
    ["eye_stencil_prepass", 2001], ["eyelash_stencil_prepass", 2001.1],
    ["eyebrow_stencil_prepass", 2001.2], ["eyelash", 2001], ["eyebrow", 2001],
    ["eye", 2002], ["hair", 2451], ["eye_through_hair", 2452],
    ["eyelash_through_hair", 2453], ["eyebrow_through_hair", 2454],
    ["eyelight_through_hair", 2455], ["unknown", 2000],
  ]);
  for (const [kind, order] of expected) assert.equal(getHeadLayerRenderOrder(kind), order);
});

test("group sorting stays stable and grouped meshes preserve static and skinned state", () => {
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute([
    0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0,
  ], 3));
  geometry.setIndex([0, 1, 2, 1, 3, 2]);
  geometry.addGroup(0, 3, 0);
  geometry.addGroup(3, 3, 1);
  const hair = new THREE.MeshBasicMaterial();
  hair.userData.pjskMaterialKind = "hair";
  const face = new THREE.MeshBasicMaterial();
  face.userData.pjskMaterialKind = "face";
  const source = new THREE.Mesh(geometry, [hair, face]);
  source.name = "source";
  source.position.set(1, 2, 3);
  source.layers.set(3);
  sortHeadMeshGroupsByMaterialKind(source, [hair, face]);
  assert.deepEqual(source.geometry.groups.map((group) => group.materialIndex), [1, 0]);
  sortHeadMeshGroupsByMaterialKind(source, [hair]);

  assert.equal(createGroupedLayerMesh(source, [], [hair], "empty"), null);
  assert.equal(createGroupedLayerMesh(source, [{ start: 0, count: 3, materialIndex: 0 }], [], "empty"), null);
  const grouped = createGroupedLayerMesh(source, [{ start: 0, count: 3, materialIndex: 0 }], [hair], "layer");
  assert.ok(grouped instanceof THREE.Mesh);
  assert.equal(grouped.position.equals(source.position), true);
  assert.equal(grouped.renderOrder, 2451);

  const bone = new THREE.Bone();
  const skinned = new THREE.SkinnedMesh(geometry, hair);
  skinned.bind(new THREE.Skeleton([bone]));
  skinned.morphTargetDictionary = { smile: 0 };
  skinned.morphTargetInfluences = [0.5];
  const skinnedLayer = createGroupedLayerMesh(
    skinned, [{ start: 0, count: 3, materialIndex: 0 }], [hair], "layer"
  );
  assert.ok(skinnedLayer instanceof THREE.SkinnedMesh);
  assert.equal(skinnedLayer.skeleton, skinned.skeleton);
  assert.equal(skinnedLayer.morphTargetInfluences, skinned.morphTargetInfluences);
});

test("through-hair source kind inference covers every overlay prefix and empty input", () => {
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute([0, 0, 0], 3));
  const source = new THREE.Mesh(geometry, new THREE.MeshBasicMaterial());
  assert.equal(createSekaiThroughHairOverlayMesh(source, [], []), null);
  for (const [kind, expected] of [
    ["eyelash_through_hair", "eyelash"], ["eyebrow_through_hair", "eyebrow"],
    ["eyelight_through_hair", "eyelight"], ["eye_through_hair", "eye"], ["other", ""],
  ]) {
    const material = new THREE.MeshBasicMaterial();
    material.userData.pjskMaterialKind = kind;
    const overlay = createSekaiThroughHairOverlayMesh(
      source, [{ start: 0, count: 1, materialIndex: 0 }], [material]
    );
    assert.equal(overlay.userData.pjskEyeThroughHairSourceKind, expected);
    assert.equal(overlay.userData.pjskEyeThroughHairOverlay, true);
  }
});

test("preview lighting reads raw scalar, boolean, color, keyword, and fallback forms", () => {
  assert.equal(tuneLightingForPreview("body", undefined), undefined);
  const lighting = {
    specularPower: 1, rimThreshold: 1, shadowTexWeight: 1, fadeMode: 0,
    hueSinAngle: 0, hueCosAngle: 1, saturation: 0.5, value: 0.5, contrast: 0.5,
    partsAmbientColor: "#000000", reflectionBlendColor: "#000000",
    outlineWidth: 1, outlineOffset: 1, outlineLightness: 1, shadowWidth: 1,
    useOutlineSecondNormal: 0, sekaiShadowThreshold: 0.5,
    useLambert: false, useValueTex: false, useFaceSdf: false,
    useFaceShadowLimiter: false, rangeLimit: 1, hairShadow: false,
  };
  const raw = {
    floatProperties: [
      { name: "_SpecularPower", value: 3 },
      { name: "_UseOutlineSecondNormal", value: 1 },
      { name: "_UseLambert", value: 1 },
      { name: "_RangeLimit", value: 2 },
    ],
    colorProperties: [
      { name: "_PartsAmbientColor", r: 1, g: 0.5, b: 0, a: 1 },
    ],
    validKeywords: ["_USE_FACE_SDF", "_HAIR_SHADOW"],
    invalidKeywords: [], intProperties: [], textureProperties: [],
  };
  const tuned = tuneLightingForPreview("body", lighting, raw);
  assert.equal(tuned.specularPower, 3);
  assert.equal(tuned.useOutlineSecondNormal, 1);
  assert.equal(tuned.useLambert, true);
  assert.equal(tuned.useFaceSdf, true);
  assert.equal(tuned.hairShadow, true);
  assert.equal(tuned.partsAmbientColor, "#ff8000");
  assert.equal(tuned.headNormalBlend, 0.7);

  const keywordOnly = tuneLightingForPreview("body", lighting, {
    floatProperties: [], colorProperties: [], intProperties: [], textureProperties: [],
    validKeywords: ["_OUTLINE_SECOND_NORMAL"], invalidKeywords: [],
  });
  assert.equal(keywordOnly.useOutlineSecondNormal, 1);
});
