import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import {
  createSekaiBodyMaterial,
  createSekaiFaceMaterial,
  headMaterialRuntimeInternals as materials,
} from "../dist/haruki-3d-engine-internal.js";

function makeTemplates() {
  const light = {
    baseColor: "#ffffff", shadowColor: "#808080",
    lightDirection: new THREE.Vector3(0, 1, 0), lightIntensity: 1,
    ambientIntensity: 1, shadowThreshold: 0.5, shadowWeight: 1,
  };
  return {
    body: createSekaiBodyMaterial(light),
    hair: createSekaiBodyMaterial(light),
    face: createSekaiFaceMaterial({
      baseColor: "#ffffff", warmColor: "#808080",
      lightDirection: new THREE.Vector3(0, 1, 0), lightIntensity: 1,
      ambientIntensity: 1,
    }),
  };
}

const headAsset = {
  proxy: {
    faceColor: "#abcdef", faceShadeColor: "#654321",
    hairColor: "#112233", hairShadowColor: "#223344",
    skinColorDefault: "#fedcba", skinColor1: "#ba9876", skinColor2: "#765432",
  },
};

const textures = () => ({
  mainTex: new THREE.Texture(), shadowTex: new THREE.Texture(),
  valueTex: new THREE.Texture(), faceShadowTex: new THREE.Texture(),
});

test("head slot factory covers eye, highlight, lash, hair, accessory, body, and face policies", () => {
  const templates = makeTemplates();
  const view = { bodyDebugMode: 2, faceDebugMode: 3, faceSdfEnabled: true };
  const hair = { controllerPresent: true, proximityShadowEnabled: true, headPosition: new THREE.Vector3(1, 2, 3) };
  const eyeController = {
    lightInfluence: 0.2, lightInfluenceForEyeHighlight: 0.3,
    tintColor: "#ffffff", emissionColor: "#111111",
    baseTiling: { tileX: 2, tileY: 2, sample: 1 },
    highlightTiling: { tileX: 4, tileY: 4, sample: 2 },
  };
  const lighting = {
    useLambert: false, hairShadow: true, lightInfluence: 0.4,
    lightInfluenceForEyeHighlight: 0.5, threshold: 0.6,
    distortionFps: 2, distortionIntensity: 0.1,
  };
  for (const kind of ["eye", "eyelight", "eyelash", "eyebrow", "hair", "accessory", "body", "face_sdf"]) {
    const result = materials.createHeadSlotMaterials(
      { materialKind: kind, rawMaterial: {} }, textures(), headAsset,
      templates, view, hair, eyeController, lighting
    );
    assert.ok(result.material instanceof THREE.Material);
    if (kind === "eye") {
      assert.ok(result.material.userData.pjskOverlayMaterial);
      assert.ok(result.material.userData.pjskStencilPrepassMaterial);
    }
    if (kind === "eyelight") {
      assert.ok(result.topLayerMaterial);
      assert.equal(result.material.visible, false);
    }
    if (kind === "eyelash" || kind === "eyebrow") {
      assert.ok(result.outlineSourceMaterial);
    }
    if (kind === "hair") {
      assert.equal(result.material.uniforms.uHairShadowEnabled.value, 1);
    }
    if (kind === "face_sdf") {
      assert.equal(result.material.uniforms.uFaceDebugMode.value, 3);
    }
  }

  const noControllerHair = materials.createHeadSlotMaterials(
    { materialKind: "hair" }, textures(), headAsset, templates, view,
    { controllerPresent: false, proximityShadowEnabled: true, headPosition: new THREE.Vector3() },
    null, { useLambert: false, hairShadow: true }
  ).material;
  assert.equal(noControllerHair.uniforms.uHairShadowEnabled.value, 0);
  assert.equal(noControllerHair.uniforms.uUseLambert.value, 0);
});

test("eye option fallback and face cloning accept sparse controller shader state", () => {
  assert.deepEqual(materials.createEyeLayerOptions(null, { lightInfluence: 0.4 }), {
    tintColor: undefined, emissionColor: undefined, lightInfluence: 0.4,
    distortionFps: undefined, distortionIntensity: undefined,
    distortionIntensityX: undefined, distortionIntensityY: undefined,
    distortionOffsetX: undefined, distortionOffsetY: undefined,
    distortionScrollSpeed: undefined, distortionScrollX: undefined, distortionScrollY: undefined,
    distortionTexTilingX: undefined, distortionTexTilingY: undefined, threshold: undefined,
  });
  assert.equal(materials.createHighlightLayerOptions(
    { lightInfluenceForEyeHighlight: null }, { lightInfluenceForEyeHighlight: 0.7 }
  ).highlightInfluence, 0.7);

  const source = makeTemplates().face;
  const cloned = materials.cloneFaceShaderMaterial(source, {
    mainTex: null, valueTex: new THREE.Texture(),
    lighting: {
      useValueTex: false, sekaiShadowThreshold: 0.2, shadowWidth: 0.3,
      fadeMode: 1, useLambert: false, shadowTexWeight: 0.4,
      useFaceShadowLimiter: false, rangeLimit: 0.5,
      hueSinAngle: 0.1, hueCosAngle: 0.9, saturation: 0.8,
      value: 0.7, contrast: 0.6, partsAmbientColor: "#123456",
      specularPower: 4, rimThreshold: 0.25,
    },
  });
  assert.ok(cloned instanceof THREE.ShaderMaterial);
  assert.equal(cloned.uniforms.uUseValueTex.value, 0);
  assert.equal(cloned.uniforms.uUseLambert.value, 0);
});

test("original texture adoption updates shader, layers, base color, and basic material", () => {
  const mainMap = new THREE.Texture();
  mainMap.matrix.setUvTransform(0.1, 0.2, 2, 3, 0, 0, 0);
  const shader = makeTemplates().face;
  shader.uniforms.uMainTex.value = null;
  const overlay = shader.clone();
  overlay.uniforms.uMainTex.value = null;
  const slot = {
    material: shader, overlayMaterial: overlay, stencilPrepassMaterial: null, topLayerMaterial: null,
  };
  materials.syncHeadSlotTextures(slot, mainMap);
  assert.equal(materials.applyOriginalHeadMap(slot, mainMap), true);
  assert.equal(shader.uniforms.uMainTex.value, mainMap);
  assert.equal(overlay.uniforms.uMainTex.value, mainMap);
  assert.equal(shader.uniforms.uUseMainTex.value, 1);
  assert.equal(shader.uniforms.uBaseColor.value.getHexString(), "ffffff");
  assert.equal(materials.applyOriginalHeadMap(slot, mainMap), false);

  const basic = new THREE.MeshBasicMaterial();
  const basicSlot = { material: basic, overlayMaterial: null, stencilPrepassMaterial: null, topLayerMaterial: null };
  assert.equal(materials.applyOriginalHeadMap(basicSlot, mainMap), true);
  assert.equal(basic.map, mainMap);
  assert.equal(materials.applyOriginalHeadMap(basicSlot, mainMap), false);
  assert.equal(materials.applyOriginalHeadMap({ ...basicSlot, material: new THREE.MeshStandardMaterial() }, mainMap), false);
});

test("SDF state, UV1 detection, and disposal preserve active rebound materials", () => {
  const geometry = new THREE.BufferGeometry();
  const mesh = new THREE.Mesh(geometry, new THREE.MeshBasicMaterial());
  assert.equal(materials.hasFaceSdfUv1Attribute(mesh), false);
  geometry.setAttribute("uv1", new THREE.Float32BufferAttribute([0, 0, 1, 1], 2));
  assert.equal(materials.hasFaceSdfUv1Attribute(mesh), true);

  const shader = makeTemplates().face;
  materials.updateHeadFaceSdfState(shader, { faceSdfEnabled: true }, true, true);
  assert.equal(shader.uniforms.uFaceSdfEnabled.value, 1);
  assert.equal(shader.userData.pjskFaceSdfCapable, true);
  materials.updateHeadFaceSdfState(shader, { faceSdfEnabled: false }, true, false);
  assert.equal(shader.uniforms.uFaceSdfEnabled.value, 0);
  materials.updateHeadFaceSdfState(new THREE.MeshBasicMaterial(), { faceSdfEnabled: true }, true, true);
  const withoutUniform = new THREE.ShaderMaterial({ uniforms: {} });
  materials.updateHeadFaceSdfState(withoutUniform, { faceSdfEnabled: true }, true, true);

  const preserved = new THREE.MeshBasicMaterial();
  const disposed = new THREE.MeshBasicMaterial();
  let disposeCount = 0;
  disposed.dispose = () => { disposeCount += 1; };
  materials.disposeReplacedMaterials([preserved, disposed], [preserved]);
  assert.equal(disposeCount, 1);
});

test("layer collection and queueing cover absent materials, detached meshes, and all pass types", () => {
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute([
    0, 0, 0, 1, 0, 0, 0, 1, 0,
  ], 3));
  geometry.setIndex([0, 1, 2]);
  const sourceMaterial = new THREE.MeshBasicMaterial();
  sourceMaterial.name = "source";
  const mesh = new THREE.Mesh(geometry, sourceMaterial);
  mesh.name = "Face";
  const parent = new THREE.Group();
  parent.add(mesh);
  const top = new THREE.ShaderMaterial();
  top.userData.pjskMaterialKind = "eyelight";
  const overlay = new THREE.ShaderMaterial();
  overlay.userData.pjskMaterialKind = "eye_through_hair";
  const stencil = new THREE.ShaderMaterial();
  stencil.userData.pjskMaterialKind = "eye_stencil_prepass";
  const slots = [{ topLayerMaterial: top, overlayMaterial: overlay, stencilPrepassMaterial: stencil }, null];
  const debug = [];
  const passes = materials.collectHeadLayerPasses([
    { start: 0, count: 3, materialIndex: 0 },
    { start: 0, count: 3, materialIndex: 1 },
  ], slots, [sourceMaterial], mesh.name, debug);
  assert.equal(passes.top.materials.length, 1);
  assert.equal(passes.overlay.groups.length, 1);
  assert.equal(passes.stencil.groups.length, 1);
  assert.equal(debug.length, 3);
  const empty = { materials: [], groups: [] };
  materials.addHeadLayerPass(empty, { start: 0, count: 1, materialIndex: 0 }, null);
  assert.equal(empty.groups.length, 0);

  const queued = [];
  materials.queueThroughHairPasses(mesh, passes.stencil, "stencil_prepass", queued);
  materials.queueThroughHairPasses(mesh, passes.overlay, "overlay", queued);
  materials.queueTopLayerPasses(mesh, passes.top, queued);
  assert.equal(queued.length, 3);
  assert.equal(queued[0].mesh.userData.pjskEyeThroughHairStencilPrepass, true);
  assert.equal(queued[2].mesh.userData.pjskMaterialKind, "eyelight");

  const detached = mesh.clone();
  materials.queueThroughHairPasses(detached, { materials: [overlay], groups: [{ start: 0, count: 3, materialIndex: 0 }] }, "overlay", queued);
  materials.queueTopLayerPasses(detached, { materials: [top], groups: [{ start: 0, count: 3, materialIndex: 0 }] }, queued);
  materials.queueTopLayerPasses(mesh, { materials: [], groups: [{ start: 0, count: 3, materialIndex: 0 }] }, queued);
  assert.equal(queued.length, 3);
});
