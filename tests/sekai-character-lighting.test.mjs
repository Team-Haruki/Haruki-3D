import assert from "node:assert/strict";
import test from "node:test";

import {
  evaluateSekaiBaseShadow,
  evaluateSekaiFaceShadow,
  evaluateSekaiSkinColor,
  previewLightDefaults,
  sekaiCostumeShopControllerDefaults,
  sekaiCostumeShopDirectionalLightDirection,
  sekaiCostumeShopDirectionalLightRotationDegrees,
  sekaiCostumeShopRimLightDirection,
} from "../dist/haruki-3d-engine-internal.js";

test("costume preview uses the official costume-shop directional transform", () => {
  assert.deepEqual(sekaiCostumeShopDirectionalLightRotationDegrees, {
    x: -15,
    y: 50,
    z: 0,
  });
  assert.deepEqual(
    sekaiCostumeShopDirectionalLightDirection,
    {
      x: -0.7127446532249451,
      y: 0.258819043636322,
      z: 0.6519262194633484,
    }
  );
  assert.deepEqual({
    x: previewLightDefaults.x,
    y: previewLightDefaults.y,
    z: previewLightDefaults.z,
  }, sekaiCostumeShopDirectionalLightDirection);
  assert.ok(Math.abs(Math.hypot(
    previewLightDefaults.x,
    previewLightDefaults.y,
    previewLightDefaults.z
  ) - 1) < 1e-7);
  assert.equal(previewLightDefaults.shadowThreshold, 0.40625);
  assert.equal(previewLightDefaults.characterAmbient, 1);
  assert.equal(previewLightDefaults.rimColorAlpha, 1);
  assert.equal(previewLightDefaults.rimRange, 7);
  assert.equal(previewLightDefaults.rimEdgeSmoothness, 0.0010000000474974513);
  assert.equal(previewLightDefaults.rimEmission, 0);
  assert.equal(previewLightDefaults.rimLightInfluence, 1);
  assert.equal(previewLightDefaults.rimShadowSharpness, 0.5);
});

test("costume preview controller defaults come from the coherent 6.6.2 frame", () => {
  assert.deepEqual(sekaiCostumeShopControllerDefaults, {
    ambientColor: { r: 0.5, g: 0.5, b: 0.5 },
    ambientIntensity: 1,
    specularColor: { r: 1, g: 1, b: 1 },
    specularIntensity: 1,
    rimColor: { r: 0.5, g: 0.5, b: 0.5 },
    rimColorAlpha: 1,
    rimRange: 7,
    rimEdgeSmoothness: 0.0010000000474974513,
    rimEmission: 0,
    rimLightInfluence: 1,
    shadowRimColor: { r: 0, g: 0, b: 0 },
    rimShadowSharpness: 0.5,
  });
  assert.ok(Math.abs(sekaiCostumeShopRimLightDirection.x - 0.833125114440918) < 1e-12);
  assert.ok(Math.abs(sekaiCostumeShopRimLightDirection.y + 0.3420201539993286) < 1e-12);
  assert.ok(Math.abs(sekaiCostumeShopRimLightDirection.z - 0.43465474247932434) < 1e-12);
});

test("official base toon uses half Lambert only when enabled", () => {
  assert.equal(evaluateSekaiBaseShadow({
    normalDotLight: -1,
    valueB: 0.5,
    useLambert: false,
    useValueTex: true,
    threshold: 0.4,
    width: 0,
    fadeMode: 0,
  }).shadow, 0);
  assert.equal(evaluateSekaiBaseShadow({
    normalDotLight: -1,
    valueB: 0.5,
    useLambert: true,
    useValueTex: true,
    threshold: 0.4,
    width: 0,
    fadeMode: 0,
  }).shadow, 1);
});

test("official toon width uses asymmetric FadeOut and Spread intervals", () => {
  const fadeOut = evaluateSekaiBaseShadow({
    normalDotLight: -0.4,
    valueB: 0.5,
    useLambert: true,
    useValueTex: true,
    threshold: 0.4,
    width: 0.5,
    fadeMode: 0,
  });
  const spread = evaluateSekaiBaseShadow({
    normalDotLight: 0.1,
    valueB: 0.5,
    useLambert: true,
    useValueTex: true,
    threshold: 0.4,
    width: 0.5,
    fadeMode: 1,
  });

  assert.ok(Math.abs(fadeOut.shadow - 0.5) < 1e-6);
  assert.ok(Math.abs(spread.shadow - 0.5) < 1e-6);
});

test("official FaceSDF mirrors UV choice and reuses the toon band", () => {
  const result = evaluateSekaiFaceShadow({
    sdf: 0.9,
    mirroredSdf: 0.1,
    headDotX: -0.5,
    headDotY: 0.75,
    useLimiter: true,
    rangeLimit: 0.25,
    width: 0.2,
    fadeMode: 0,
  });

  assert.equal(result.sdf, 0.1);
  assert.equal(result.threshold, 0.25);
  assert.ok(Math.abs(result.shadow - 0.9259259259259258) < 1e-6);
});

test("official skin path uses shaded C/S red and H.r as a binary selector", () => {
  assert.deepEqual(
    evaluateSekaiSkinColor({
      skinValue: 0,
      globalShadow: [0.5, 0.5, 0.5],
      defaultSkin: [1, 0.9, 0.8],
      shadow1Skin: [0.8, 0.6, 0.7],
      shadow2Skin: [0.6, 0.3, 0.4],
    }),
    [0.3, 0.15, 0.2]
  );
  assert.deepEqual(
    evaluateSekaiSkinColor({
      skinValue: 0.5,
      globalShadow: [0.5, 0.5, 0.5],
      defaultSkin: [1, 0.9, 0.8],
      shadow1Skin: [0.8, 0.6, 0.7],
      shadow2Skin: [0.6, 0.3, 0.4],
    }),
    [0.4, 0.3, 0.35]
  );
  const lit = evaluateSekaiSkinColor({
    skinValue: 1,
    globalShadow: [0.5, 0.5, 0.5],
    defaultSkin: [1, 0.9, 0.8],
    shadow1Skin: [0.8, 0.6, 0.7],
    shadow2Skin: [0.6, 0.3, 0.4],
  });
  assert.ok(lit.every((channel, index) => Math.abs(channel - [1, 0.9, 0.8][index]) < 1e-12));
});
