import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";
import {
  applyRawMaterialTextureTransform,
  applyRawMaterialShaderUniforms,
  bindBodyRuntimeMaterials,
  createSekaiBodyMaterial,
  tuneLightingForPreview,
} from "../dist/haruki-3d-engine-internal.js";

test("raw CostumeShop feature keywords and exact rim property override promoted defaults", () => {
  const lighting = {
    specularPower: 0,
    rimThreshold: 0.2,
    shadowTexWeight: 1,
    fadeMode: 0,
    hueSinAngle: 0,
    hueCosAngle: 1,
    saturation: 0.5,
    value: 0.5,
    contrast: 0.5,
    partsAmbientColor: "#ffffff",
    reflectionBlendColor: "#ffffff",
    outlineWidth: 0.001,
    outlineOffset: 0,
    outlineLightness: 0.5,
    shadowWidth: 0,
    useOutlineSecondNormal: 0,
    distortionFps: 12,
    distortionIntensity: 0,
    distortionIntensityX: 0,
    distortionIntensityY: 0,
    distortionOffsetX: 0,
    distortionOffsetY: 0,
    distortionScrollSpeed: 1,
    distortionScrollX: 0,
    distortionScrollY: 0,
    distortionTexTilingX: 1,
    distortionTexTilingY: 1,
    threshold: 0.5,
    lightInfluence: 1,
    lightInfluenceForEyeHighlight: 1,
  };
  const tuned = tuneLightingForPreview("hair", lighting, {
    shaderFileId: 0,
    shaderPathId: 1,
    textureProperties: [],
    colorProperties: [],
    floatProperties: [
      { name: "_SpecularStrength", value: 0.99 },
      { name: "_RimThreshold", value: 0.25 },
      { name: "_HeadNormalBlend", value: 0.7 },
    ],
    intProperties: [],
    validKeywords: [
      "_OUTLINE_SECOND_NORMAL",
      "_USE_FACE_SDF",
      "_FACE_SHADOW_RANGE_LIMIT",
      "_HAIR_SHADOW",
    ],
    invalidKeywords: [],
    lightmapFlags: 0,
    enableInstancingVariants: false,
    doubleSidedGi: false,
    customRenderQueue: -1,
    stringTags: {},
    disabledShaderPasses: [],
  });

  assert.equal(tuned.rimThreshold, 0.25);
  assert.equal(tuned.useOutlineSecondNormal, 1);
  assert.equal(tuned.useFaceSdf, true);
  assert.equal(tuned.useFaceShadowLimiter, true);
  assert.equal(tuned.hairShadow, true);
  assert.equal(tuned.headNormalBlend, 0.7);
});

test("raw Unity texture scale and offset feed the shader UV matrix", () => {
  const texture = new THREE.Texture();
  applyRawMaterialTextureTransform(texture, {
    shaderFileId: 0,
    shaderPathId: 1,
    textureProperties: [{
      name: "_MainTex",
      textureKey: "main",
      scaleX: 2,
      scaleY: 3,
      offsetX: 0.25,
      offsetY: 0.5,
      filterMode: 2,
      anisoLevel: 8,
      wrapU: 2,
      wrapV: 1,
    }],
    colorProperties: [],
    floatProperties: [],
    intProperties: [],
    validKeywords: [],
    invalidKeywords: [],
    lightmapFlags: 0,
    enableInstancingVariants: false,
    doubleSidedGi: false,
    customRenderQueue: -1,
    stringTags: {},
    disabledShaderPasses: [],
  }, "_MainTex");

  assert.deepEqual(texture.repeat.toArray(), [2, 3]);
  assert.deepEqual(texture.offset.toArray(), [0.25, 0.5]);
  assert.equal(texture.wrapS, THREE.MirroredRepeatWrapping);
  assert.equal(texture.wrapT, THREE.ClampToEdgeWrapping);
  assert.equal(texture.magFilter, THREE.LinearFilter);
  assert.equal(texture.minFilter, THREE.LinearMipmapLinearFilter);
  assert.equal(texture.anisotropy, 8);
  const material = createSekaiBodyMaterial({
    baseColor: "#ffffff",
    shadowColor: "#808080",
    mainTex: texture,
    lightDirection: new THREE.Vector3(0, 1, 0),
    lightIntensity: 1,
    ambientIntensity: 1,
    shadowThreshold: 0.5,
    shadowWeight: 1,
  });
  assert.notEqual(material.uniforms.uMainTexTransform.value, texture.matrix);
  assert.deepEqual(
    material.uniforms.uMainTexTransform.value.toArray(),
    texture.matrix.toArray()
  );
  assert.match(material.fragmentShader, /uMainTexTransform \* vec3\(vUv, 1\.0\)/);
  assert.match(material.fragmentShader, /texture2D\(uShadowTex, mainUv\)/);
  assert.match(material.fragmentShader, /texture2D\(uValueTex, mainUv\)/);
  assert.doesNotMatch(material.fragmentShader, /uShadowTexTransform|uValueTexTransform/);
});

test("raw Unity material colors and alpha feature state override proxy defaults", () => {
  const material = createSekaiBodyMaterial({
    baseColor: "#ffffff",
    shadowColor: "#808080",
    lightDirection: new THREE.Vector3(0, 1, 0),
    lightIntensity: 1,
    ambientIntensity: 1,
    shadowThreshold: 0.5,
    shadowWeight: 1,
    alphaCutoff: 0.02,
  });
  assert.equal(material.side, THREE.FrontSide);
  applyRawMaterialShaderUniforms(material, {
    shaderFileId: 0,
    shaderPathId: 1,
    textureProperties: [],
    colorProperties: [
      { name: "_DefaultSkinColor", r: 0.9, g: 0.8, b: 0.7, a: 1 },
      { name: "_Shadow1SkinColor", r: 0.6, g: 0.5, b: 0.4, a: 1 },
      { name: "_Shadow2SkinColor", r: 0.3, g: 0.2, b: 0.1, a: 1 },
      { name: "_PartsAmbientColor", r: 0.4, g: 0.5, b: 0.6, a: 0.75 },
    ],
    floatProperties: [
      { name: "_UseAlphaClip", value: 1 },
      { name: "_Cutoff", value: 0.375 },
      { name: "_HeadNormalBlend", value: 0.35 },
    ],
    intProperties: [],
    validKeywords: [],
    invalidKeywords: [],
    lightmapFlags: 0,
    enableInstancingVariants: false,
    doubleSidedGi: false,
    customRenderQueue: -1,
    stringTags: {},
    disabledShaderPasses: [],
  });

  assert.deepEqual(material.uniforms.uSkinColorDefault.value.toArray(), [0.9, 0.8, 0.7]);
  assert.deepEqual(material.uniforms.uSkinColor1.value.toArray(), [0.6, 0.5, 0.4]);
  assert.deepEqual(material.uniforms.uSkinColor2.value.toArray(), [0.3, 0.2, 0.1]);
  assert.equal(material.uniforms.uPartsAmbientAlpha.value, 0.75);
  assert.equal(material.uniforms.uAlphaCutoff.value, 0.375);
  assert.equal(material.uniforms.uHeadNormalBlend.value, 0.35);
  assert.doesNotMatch(
    material.vertexShader,
    /worldNormal \+\s*uHeadNormalBlend \* \(radialNormal - worldNormal\)/
  );
  assert.match(
    material.vertexShader,
    /inverseTransformDirection\(\s*transformedNormal,\s*viewMatrix\s*\)/
  );
  assert.doesNotMatch(
    material.vertexShader,
    /mat3\(modelMatrix\) \* objectNormal/
  );
});

test("body material binding preserves exact Unity slots and texture sampling state", async () => {
  const loaded = [];
  const textureLoader = {
    async loadAsync(url) {
      const texture = new THREE.Texture();
      texture.name = url;
      loaded.push(texture);
      return texture;
    },
  };
  const originalMap = new THREE.Texture();
  originalMap.wrapS = THREE.MirroredRepeatWrapping;
  originalMap.wrapT = THREE.ClampToEdgeWrapping;
  originalMap.offset.set(0.25, 0.5);
  originalMap.repeat.set(2, 3);
  originalMap.center.set(0.1, 0.2);
  originalMap.rotation = 0.75;
  originalMap.magFilter = THREE.NearestFilter;
  originalMap.minFilter = THREE.LinearMipmapNearestFilter;
  originalMap.anisotropy = 4;
  originalMap.flipY = true;
  originalMap.colorSpace = THREE.LinearSRGBColorSpace;

  const originalMaterial = new THREE.MeshBasicMaterial({ map: originalMap });
  originalMaterial.name = "BodySource";
  originalMaterial.userData.pjskMaterialKey = "body:0";
  let originalDisposed = false;
  originalMaterial.dispose = () => {
    originalDisposed = true;
  };
  const fallbackMap = new THREE.Texture();
  const fallbackMaterial = new THREE.MeshBasicMaterial({ map: fallbackMap });
  fallbackMaterial.name = "BodyFallbackSource";
  fallbackMaterial.userData.pjskMaterialKey = "body:1";
  let fallbackDisposed = false;
  fallbackMaterial.dispose = () => {
    fallbackDisposed = true;
  };
  const mesh = new THREE.Mesh(
    new THREE.BufferGeometry(),
    [originalMaterial, fallbackMaterial]
  );
  mesh.name = "Body";
  const root = new THREE.Group();
  root.add(mesh);

  const template = createSekaiBodyMaterial({
    baseColor: "#ffffff",
    shadowColor: "#808080",
    lightDirection: new THREE.Vector3(0, 1, 0),
    lightIntensity: 1,
    ambientIntensity: 1,
    shadowThreshold: 0.5,
    shadowWeight: 1,
  });
  const targetDebug = [];
  const debug = await bindBodyRuntimeMaterials({
    root,
    bodyAsset: {
      id: "body",
      displayName: "Body",
      source: { bundleRoot: "", manifestUrl: "", meshUrl: "" },
      neckAnchor: { x: 0, y: 0, z: 0 },
      skeleton: {
        skeletonId: "body",
        neckAttach: { fallbackPosition: { x: 0, y: 0, z: 0 } },
      },
      bodyMaterials: [{
        meshName: "Body",
        slotIndex: 0,
        materialKey: "body:0",
        materialFileId: 1,
        materialPathId: 2,
        materialName: "BodyRuntime",
        materialKind: "body",
        mainTex: "/body-c.png",
        shadowTex: "/body-s.png",
        valueTex: "/body-h.png",
      }, {
        meshName: "Body",
        slotIndex: 1,
        materialKey: "body:1",
        materialFileId: 1,
        materialPathId: 3,
        materialName: "BodyFallback",
        materialKind: "body",
      }],
      proxy: {
        bodyColor: "#f0d0c0",
        shadowColor: "#c09080",
        bodyScale: 1,
        torsoLength: 1,
        shoulderWidth: 1,
      },
    },
    headAsset: null,
    textureLoader,
    template,
    bodyDebugMode: 7,
    debug: targetDebug,
  });

  assert.deepEqual(loaded.map((texture) => texture.name), [
    "/body-c.png",
    "/body-s.png",
    "/body-h.png",
  ]);
  assert.ok(Array.isArray(mesh.material));
  const [replacementMaterial, fallbackReplacementMaterial] = mesh.material;
  assert.ok(replacementMaterial instanceof THREE.ShaderMaterial);
  assert.ok(fallbackReplacementMaterial instanceof THREE.ShaderMaterial);
  assert.equal(replacementMaterial.userData.pjskMaterialKey, "body:0");
  assert.equal(replacementMaterial.userData.pjskMaterialKind, "body");
  assert.equal(mesh.userData.pjskMaterialKind, "body");
  assert.equal(replacementMaterial.stencilWrite, true);
  assert.equal(replacementMaterial.stencilRef, 0);
  assert.equal(replacementMaterial.stencilFunc, THREE.AlwaysStencilFunc);
  assert.equal(replacementMaterial.stencilFuncMask, 0xff);
  assert.equal(replacementMaterial.stencilWriteMask, 0xff);
  assert.equal(replacementMaterial.stencilZPass, THREE.ReplaceStencilOp);
  assert.equal(replacementMaterial.uniforms.uShadowTex.value.colorSpace, THREE.SRGBColorSpace);
  assert.equal(replacementMaterial.uniforms.uValueTex.value.colorSpace, THREE.NoColorSpace);
  assert.equal(replacementMaterial.uniforms.uBodyDebugMode.value, 7);
  assert.equal(replacementMaterial.uniforms.uUseSkinColor, undefined);
  assert.equal(replacementMaterial.uniforms.uSkinMaskMode, undefined);

  const replacementMap = replacementMaterial.uniforms.uMainTex.value;
  assert.notEqual(
    replacementMaterial.uniforms.uMainTexTransform.value,
    replacementMap.matrix
  );
  assert.deepEqual(
    replacementMaterial.uniforms.uMainTexTransform.value.toArray(),
    replacementMap.matrix.toArray()
  );
  assert.equal(replacementMap.wrapS, originalMap.wrapS);
  assert.equal(replacementMap.wrapT, originalMap.wrapT);
  assert.deepEqual(replacementMap.offset.toArray(), originalMap.offset.toArray());
  assert.deepEqual(replacementMap.repeat.toArray(), originalMap.repeat.toArray());
  assert.deepEqual(replacementMap.center.toArray(), originalMap.center.toArray());
  assert.equal(replacementMap.rotation, originalMap.rotation);
  assert.equal(replacementMap.magFilter, originalMap.magFilter);
  assert.equal(replacementMap.minFilter, originalMap.minFilter);
  assert.equal(replacementMap.anisotropy, originalMap.anisotropy);
  assert.equal(replacementMap.flipY, originalMap.flipY);
  assert.equal(replacementMap.colorSpace, THREE.SRGBColorSpace);
  assert.equal(fallbackReplacementMaterial.uniforms.uMainTex.value, fallbackMap);
  assert.notEqual(
    fallbackReplacementMaterial.uniforms.uMainTexTransform.value,
    fallbackMap.matrix
  );
  assert.deepEqual(
    fallbackReplacementMaterial.uniforms.uMainTexTransform.value.toArray(),
    fallbackMap.matrix.toArray()
  );
  assert.equal(fallbackReplacementMaterial.uniforms.uUseMainTex.value, 1);
  assert.equal(fallbackReplacementMaterial.uniforms.uBaseColor.value.getHex(), 0xffffff);
  assert.equal(originalDisposed, true);
  assert.equal(fallbackDisposed, true);
  assert.equal(debug, targetDebug);
  assert.equal(mesh.castShadow, false);
  assert.equal(mesh.receiveShadow, false);
  assert.deepEqual(debug.map(({ meshName, sourceMaterialName, resolvedKey, resolvedKind, usedOriginalMap }) => ({
    meshName,
    sourceMaterialName,
    resolvedKey,
    resolvedKind,
    usedOriginalMap,
  })), [{
    meshName: "Body",
    sourceMaterialName: "BodySource",
    resolvedKey: "body:0",
    resolvedKind: "body",
    usedOriginalMap: false,
  }, {
    meshName: "Body",
    sourceMaterialName: "BodyFallbackSource",
    resolvedKey: "body:1",
    resolvedKind: "body",
    usedOriginalMap: true,
  }]);
});

test("body texture binding starts independent texture loads concurrently and deduplicates matching URLs", async () => {
  const pending = new Map();
  const calls = [];
  const textureLoader = {
    loadAsync(url) {
      calls.push(url);
      return new Promise((resolve) => pending.set(url, resolve));
    },
  };
  const root = new THREE.Group();
  const template = createSekaiBodyMaterial({
    baseColor: "#ffffff",
    shadowColor: "#808080",
    lightDirection: new THREE.Vector3(0, 1, 0),
    lightIntensity: 1,
    ambientIntensity: 1,
    shadowThreshold: 0.5,
    shadowWeight: 1,
  });

  const binding = bindBodyRuntimeMaterials({
    root,
    bodyAsset: {
      id: "body",
      displayName: "Body",
      source: { bundleRoot: "", manifestUrl: "", meshUrl: "" },
      neckAnchor: { x: 0, y: 0, z: 0 },
      skeleton: {
        skeletonId: "body",
        neckAttach: { fallbackPosition: { x: 0, y: 0, z: 0 } },
      },
      bodyMaterials: [{
        meshName: "BodyA",
        slotIndex: 0,
        materialKey: "body:0",
        materialKind: "body",
        mainTex: "/shared.png",
        shadowTex: "/shadow.png",
      }, {
        meshName: "BodyB",
        slotIndex: 0,
        materialKey: "body:1",
        materialKind: "body",
        mainTex: "/shared.png",
        valueTex: "/value.png",
      }],
      proxy: {
        bodyColor: "#ffffff",
        shadowColor: "#808080",
        bodyScale: 1,
        torsoLength: 1,
        shoulderWidth: 1,
      },
    },
    headAsset: null,
    textureLoader,
    template,
    bodyDebugMode: 0,
  });

  await Promise.resolve();
  assert.deepEqual(calls, ["/shared.png", "/shadow.png", "/value.png"]);
  for (const [url, resolve] of pending) {
    const texture = new THREE.Texture();
    texture.name = url;
    resolve(texture);
  }
  await binding;
});
