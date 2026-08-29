import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import {
  CharacterLightingRuntime,
  characterLightingRuntimeInternals as lighting,
  createSekaiBodyMaterial,
  createSekaiFaceMaterial,
} from "../dist/haruki-3d-engine-internal.js";

const state = (mode = "normal", extra = {}) => ({
  mode, faceSdfEnabled: true, eyelightOnly: false, noEyelight: false,
  faceLayersVisible: true, outlineOnly: false, outlineVisible: true,
  noEyeThroughHair: false, eyeThroughHairOnly: false,
  ...extra,
});

test("lighting material-kind and isolation policy tables cover every mode", () => {
  for (const kind of ["eyelash", "eyebrow", "eye", "eyelight"]) {
    assert.equal(lighting.isFaceLayerMaterialKind(kind), true);
    assert.equal(lighting.isFaceOrFaceLayerMaterialKind(kind), true);
  }
  assert.equal(lighting.isFaceLayerMaterialKind("face"), false);
  assert.equal(lighting.isFaceOrFaceLayerMaterialKind("face"), true);
  assert.equal(lighting.isFaceOrFaceLayerMaterialKind("face_sdf"), true);
  assert.equal(lighting.isFaceOrFaceLayerMaterialKind("hair"), false);
  assert.equal(lighting.normalizeHairShadowMode("head_proximity"), "sekai_head_position");
  assert.equal(lighting.normalizeHairShadowMode("off"), "off");

  const sourceModes = [
    ["eye_through_hair_eye_only", "eye", true],
    ["eye_through_hair_eye_only", "eyebrow", false],
    ["eye_through_hair_eyebrow_only", "eyebrow", true],
    ["eye_through_hair_eyebrow_only", "eye", false],
    ["eye_through_hair_eyelash_only", "eyelash", true],
    ["eye_through_hair_eyelash_only", "eye", false],
    ["no_eye_through_hair_eye", "eye", false],
    ["no_eye_through_hair_eye", "eyebrow", true],
    ["no_eye_through_hair_eyebrow", "eyebrow", false],
    ["no_eye_through_hair_eyelash", "eyelash", false],
    ["normal", "eye", true],
  ];
  for (const [mode, kind, expected] of sourceModes) {
    assert.equal(lighting.isEyeThroughHairSourceAllowed(kind, mode), expected);
  }
  assert.equal(lighting.isEyeThroughHairPassAllowed("eyelash", "overlay", "no_eye_through_hair_eyelash_overlay"), false);
  assert.equal(lighting.isEyeThroughHairPassAllowed("eye", "overlay", "no_eye_through_hair_eyelash_overlay"), true);
  assert.equal(lighting.isEyeThroughHairPassAllowed("eyelash", "stencil_prepass", "no_eye_through_hair_eyelash_prepass"), false);
  assert.equal(lighting.isEyeThroughHairPassAllowed("eyelash", "overlay", "normal"), true);

  assert.equal(lighting.isOutlineHiddenByIsolation("body", "no_body_outline"), true);
  assert.equal(lighting.isOutlineHiddenByIsolation("hair", "no_hair_outline"), true);
  assert.equal(lighting.isOutlineHiddenByIsolation("face", "no_face_layers"), true);
  assert.equal(lighting.isOutlineHiddenByIsolation("eye", "no_face_outline"), true);
  assert.equal(lighting.isOutlineHiddenByIsolation("hair", "normal"), false);
});

test("face-layer inspection updates SDF capability and recognizes visible highlights", () => {
  const plain = new THREE.MeshBasicMaterial();
  const sdf = new THREE.ShaderMaterial({ uniforms: { uFaceSdfEnabled: { value: 0 } } });
  sdf.userData.pjskFaceSdfCapable = true;
  const unavailable = new THREE.ShaderMaterial({ uniforms: { uFaceSdfEnabled: { value: 1 } } });
  const eye = new THREE.ShaderMaterial({ uniforms: { uMode: { value: 1 } } });
  const highlight = new THREE.ShaderMaterial({ uniforms: { uMode: { value: 2 } } });
  assert.deepEqual(lighting.inspectFaceLayers([plain], true), { faceLayer: false, eyelightLayer: false });
  assert.deepEqual(lighting.inspectFaceLayers([sdf, unavailable, eye, highlight], true), {
    faceLayer: true, eyelightLayer: true,
  });
  assert.equal(sdf.uniforms.uFaceSdfEnabled.value, 1);
  assert.equal(unavailable.uniforms.uFaceSdfEnabled.value, 0);
  highlight.visible = false;
  highlight.colorWrite = false;
  assert.deepEqual(lighting.inspectFaceLayers([highlight], false), { faceLayer: true, eyelightLayer: false });
});

test("eye-through-hair isolation follows source visibility, layers, source kind, and pass kind", () => {
  const source = new THREE.Mesh();
  source.visible = true;
  source.layers.set(5);
  const mesh = new THREE.Mesh();
  mesh.userData.pjskEyeThroughHairSource = source;
  mesh.userData.pjskEyeThroughHairSourceKind = "eyelash";
  mesh.userData.pjskEyeThroughHairPassKind = "overlay";
  lighting.applyEyeThroughHairIsolation(mesh, state());
  assert.equal(mesh.visible, true);
  assert.equal(mesh.layers.mask, source.layers.mask);
  assert.equal(mesh.userData.pjskEyeThroughHairBaseVisible, true);

  source.visible = false;
  lighting.applyEyeThroughHairIsolation(mesh, state());
  assert.equal(mesh.visible, false);
  source.visible = true;
  for (const disabled of [
    { outlineOnly: true }, { eyelightOnly: true }, { noEyeThroughHair: true },
    { faceLayersVisible: false },
  ]) {
    lighting.applyEyeThroughHairIsolation(mesh, state("normal", disabled));
    assert.equal(mesh.visible, false);
  }
  lighting.applyEyeThroughHairIsolation(mesh, state("no_eye_through_hair_eyelash_overlay"));
  assert.equal(mesh.visible, false);
  mesh.userData.pjskEyeThroughHairSourceKind = "eyelight";
  lighting.applyEyeThroughHairIsolation(mesh, state("normal", { noEyelight: true }));
  assert.equal(mesh.visible, false);
  delete mesh.userData.pjskEyeThroughHairSource;
  delete mesh.userData.pjskEyeThroughHairSourceKind;
  delete mesh.userData.pjskEyeThroughHairPassKind;
  lighting.applyEyeThroughHairIsolation(mesh, state());
  assert.equal(mesh.visible, true);
});

test("outline isolation covers highlight-only, through-hair-only, visibility, and face-layer gates", () => {
  const mesh = new THREE.Mesh();
  mesh.userData.pjskSourceMaterialKind = "eye";
  lighting.applyOutlineIsolation(mesh, state("normal", { eyelightOnly: true }));
  assert.equal(mesh.visible, true);
  mesh.userData.pjskSourceMaterialKind = "hair";
  lighting.applyOutlineIsolation(mesh, state("normal", { eyelightOnly: true }));
  assert.equal(mesh.visible, false);
  lighting.applyOutlineIsolation(mesh, state("normal", { eyeThroughHairOnly: true }));
  assert.equal(mesh.visible, false);
  lighting.applyOutlineIsolation(mesh, state("normal", { outlineVisible: false }));
  assert.equal(mesh.visible, false);
  lighting.applyOutlineIsolation(mesh, state("no_hair_outline"));
  assert.equal(mesh.visible, false);
  mesh.userData.pjskSourceMaterialKind = "eyelight";
  lighting.applyOutlineIsolation(mesh, state("normal", { noEyelight: true }));
  assert.equal(mesh.visible, false);
  mesh.userData.pjskSourceMaterialKind = "face";
  lighting.applyOutlineIsolation(mesh, state("normal", { faceLayersVisible: false }));
  assert.equal(mesh.visible, false);
  delete mesh.userData.pjskSourceMaterialKind;
  lighting.applyOutlineIsolation(mesh, state());
  assert.equal(mesh.visible, true);
});

test("base mesh isolation covers normal, outline, highlight, face-layer, and linked-source states", () => {
  const face = new THREE.ShaderMaterial({ uniforms: { uMode: { value: 1 } } });
  face.userData.pjskMaterialKind = "eye";
  const light = new THREE.ShaderMaterial({ uniforms: { uMode: { value: 2 } } });
  light.userData.pjskMaterialKind = "eyelight";
  const mesh = new THREE.Mesh(new THREE.BufferGeometry(), [face, light]);
  lighting.applyBaseMeshIsolation(mesh, state());
  assert.equal(mesh.visible, true);
  lighting.applyBaseMeshIsolation(mesh, state("normal", { outlineOnly: true }));
  assert.equal(mesh.visible, false);
  lighting.applyBaseMeshIsolation(mesh, state("normal", { eyeThroughHairOnly: true }));
  assert.equal(mesh.visible, false);
  lighting.applyBaseMeshIsolation(mesh, state("normal", { eyelightOnly: true }));
  assert.equal(mesh.visible, true);
  lighting.applyBaseMeshIsolation(mesh, state("normal", { faceLayersVisible: false }));
  assert.equal(mesh.visible, false);
  lighting.applyBaseMeshIsolation(mesh, state("normal", { noEyelight: true }));
  assert.equal(mesh.visible, false);

  const body = new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshBasicMaterial());
  lighting.applyBaseMeshIsolation(body, state());
  assert.equal(body.visible, true);
  const source = new THREE.Mesh();
  source.visible = false;
  source.layers.set(4);
  body.userData.pjskEyeThroughHairSource = source;
  lighting.applyBaseMeshIsolation(body, state());
  assert.equal(body.visible, false);
  assert.equal(body.layers.mask, source.layers.mask);
});

test("node dispatch and skin-color helpers handle non-mesh, overlay, outline, and base nodes", () => {
  lighting.applyRenderIsolationToNode(new THREE.Group(), state());
  const overlay = new THREE.Mesh();
  overlay.userData.pjskEyeThroughHairOverlay = true;
  lighting.applyRenderIsolationToNode(overlay, state());
  const outline = new THREE.Mesh();
  outline.userData.pjskOutlineShell = true;
  lighting.applyRenderIsolationToNode(outline, state());
  const base = new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshBasicMaterial());
  lighting.applyRenderIsolationToNode(base, state());
  assert.equal(base.visible, true);

  const shader = new THREE.ShaderMaterial({ uniforms: {
    uSkinColorDefault: { value: new THREE.Color() },
    uSkinColor1: { value: new THREE.Color() },
    uSkinColor2: { value: new THREE.Color() },
  } });
  const colors = { default: "#ABCDEF", shadow1: "#123456", shadow2: "#654321" };
  lighting.applySkinColors(shader, colors);
  assert.notEqual(shader.uniforms.uSkinColorDefault.value.getHex(), 0);
  lighting.applySkinColors(new THREE.ShaderMaterial({ uniforms: {} }), colors);
  const entry = {
    shaderSkinColorDefault: "#000000", shaderSkinColor1: null,
    shaderSkinColor2: "#000000",
  };
  lighting.applyDebugEntrySkinColors(entry, colors);
  assert.equal(entry.shaderSkinColorDefault, "#abcdef");
  assert.equal(entry.shaderSkinColor1, null);
  assert.equal(entry.shaderSkinColor2, "#654321");
});

test("lighting controller mutators cover defaults, clamps, camera views, and outline propagation", () => {
  const body = createSekaiBodyMaterial({
    baseColor: "#ffffff", shadowColor: "#808080",
    lightDirection: new THREE.Vector3(0, 1, 0), lightIntensity: 1,
    ambientIntensity: 1, shadowThreshold: 0.5, shadowWeight: 1,
  });
  const hair = body.clone();
  hair.userData.pjskMaterialKind = "hair";
  const face = createSekaiFaceMaterial({
    baseColor: "#ffffff", warmColor: "#808080",
    lightDirection: new THREE.Vector3(0, 1, 0), lightIntensity: 1, ambientIntensity: 1,
  });
  face.userData.pjskFaceSdfCapable = true;
  const bodySlot = new THREE.Group();
  const headSlot = new THREE.Group();
  const loaded = body.clone();
  loaded.userData.pjskMaterialKind = "hair";
  headSlot.add(new THREE.Mesh(new THREE.BufferGeometry(), [loaded, new THREE.MeshBasicMaterial()]));
  const outlineMaterial = new THREE.ShaderMaterial();
  outlineMaterial.name = "pjsk_shell_outline";
  outlineMaterial.userData.pjskOutlineController = { color: new THREE.Color(), blending: 0 };
  const outline = new THREE.Mesh(new THREE.BufferGeometry(), outlineMaterial);
  outline.userData.pjskOutlineShell = true;
  headSlot.add(outline);

  const overlayMaterial = new THREE.ShaderMaterial({ uniforms: { uAlphaScale: { value: 1 } } });
  overlayMaterial.userData.pjskSekaiEyelashViewSettings = { opacity: 1, edge1: 0.9, edge2: 0.2 };
  const overlay = new THREE.Mesh(new THREE.BufferGeometry(), [overlayMaterial, new THREE.MeshBasicMaterial()]);
  overlay.userData.pjskEyeThroughHairOverlay = true;
  overlay.userData.pjskEyeThroughHairBaseVisible = true;
  const prepass = new THREE.Mesh(new THREE.BufferGeometry(), overlayMaterial);
  prepass.userData.pjskEyeThroughHairStencilPrepass = true;
  prepass.userData.pjskEyeThroughHairBaseVisible = false;
  headSlot.add(overlay, prepass);

  const debug = {
    hairShadowMode: "off",
    body: [{ resolvedKind: "body", shaderBodyDebugMode: null,
      shaderShadowWidthOverride: 0, shaderValueShadowInfluence: 0 }],
    head: [{ resolvedKind: "face_sdf", shaderFaceDebugMode: null,
      shaderFaceSdfEnabled: null, faceSdfCapable: false }],
  };
  const runtime = new CharacterLightingRuntime({
    bodyMaterial: body, hairMaterial: hair, faceMaterial: face,
    bodySlot, headSlot, directionalLight: new THREE.DirectionalLight(),
    fillLight: new THREE.AmbientLight(), debug, valueShadowInfluence: 0,
  });
  runtime.setHairShadowMode("head_proximity");
  assert.equal(runtime.getHairShadowMode(), "sekai_head_position");
  runtime.setFaceSdfDebugMode("range");
  runtime.setBodyDebugMode("off");
  runtime.setToonShadowPreview(-2, 2);
  assert.equal(body.uniforms.uShadowWidthOverride.value, 0);
  assert.equal(body.uniforms.uValueShadowInfluence.value, 1);
  runtime.setToonShadowPreview(null, -1);
  assert.equal(body.uniforms.uShadowWidthOverride.value, -1);
  runtime.setFaceSdfEnabled(false);
  runtime.setRenderIsolationMode("face_sdf");
  runtime.applyCharacterView();
  runtime.setCharacterSkinColors(null);

  const scene = new THREE.Vector3(0, 1, 0);
  const right = new THREE.Vector3(1, 0, 0);
  const forward = new THREE.Vector3(0, 0, 1);
  for (const [mode, expected] of [
    ["front", forward], ["left", right.clone().negate()], ["right", right],
    ["back", forward.clone().negate()], ["scene", scene],
  ]) {
    runtime.setFaceSdfDebugLightMode(mode);
    assert.ok(runtime.resolveFaceShadowLightDirection(scene, right, forward).distanceTo(expected) < 1e-12);
  }

  prepass.userData.pjskEyeThroughHairBaseVisible = false;
  runtime.updateEyeThroughHairView(new THREE.Vector3(), new THREE.Vector3(), forward);
  assert.equal(prepass.visible, false);
  runtime.updateEyeThroughHairView(new THREE.Vector3(0, 0, 2), new THREE.Vector3(), forward);
  assert.equal(overlay.visible, true);
  runtime.updateCamera(new THREE.Vector3(1, 2, 3));

  runtime.updateGlobalShadowColor("#123456", 2);
  assert.equal(body.uniforms.uGlobalShadowAlpha.value, 1);
  runtime.updateControllerColors({
    ambientColor: null, ambientIntensity: -1, specularColor: null,
    specularIntensity: -2, rimColor: null, shadowRimColor: null,
  });
  assert.equal(body.uniforms.uControllerAmbientIntensity.value, 0);
  assert.equal(body.uniforms.uControllerSpecularIntensity.value, 0);
  runtime.updateControllerColors({
    ambientColor: "#123456", ambientIntensity: 2, specularColor: "#654321",
    specularIntensity: 3, rimColor: "#abcdef", shadowRimColor: "#fedcba",
  });
  runtime.updateControllerRimShape({ edgeSmoothness: -1, emission: -2, shadowSharpness: 2 });
  assert.equal(body.uniforms.uControllerRimEdgeSmoothness.value, 0);
  assert.equal(body.uniforms.uControllerRimEmission.value, 0);
  assert.equal(body.uniforms.uControllerRimShadowSharpness.value, 1);
  runtime.updateControllerRimShape({});
  runtime.updateControllerOutline({});
  runtime.updateControllerOutline({ color: "#123456", blending: 2 });
  assert.equal(outlineMaterial.userData.pjskOutlineController.blending, 1);
  runtime.applyOutlineMaterial(new THREE.MeshBasicMaterial());
});
