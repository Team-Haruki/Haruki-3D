import assert from "node:assert/strict";
import test from "node:test";
import * as THREE from "three";

import {
  createSekaiOutlineMaterial,
  evaluateSekaiOutlineColor,
  evaluateSekaiOutlineFovFactor,
  isSekaiOutlinePassEnabled,
  readRawMaterialBoolean,
  readRawMaterialColor,
  readRawMaterialFloat,
  sekaiCostumeShopOutlineControllerDefaults,
  sekaiCostumeShopOutlineSettings,
  sekaiPreviewOutlineCalibration,
} from "../dist/haruki-3d-engine-internal.js";

function rawMaterial(overrides = {}) {
  return {
    shaderName: "Sekai/Character",
    shaderFileId: 0,
    shaderPathId: 1,
    shaderKey: "ref:0:1",
    textureProperties: [],
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
    ...overrides,
  };
}

test("costume shop outline globals match the captured 6.6.2 runtime", () => {
  assert.deepEqual(sekaiCostumeShopOutlineSettings, {
    widthMin: 0.0004,
    widthMax: 0.0095,
    distanceNear: 0.45,
    distanceFar: 20,
  });
  assert.ok(
    Math.abs(evaluateSekaiOutlineFovFactor(25) - 1.027823567390442) < 1e-7
  );
});

test("high-resolution preview keeps the official shell thin and dark", () => {
  assert.deepEqual(sekaiPreviewOutlineCalibration, {
    widthScale: 0.5,
    shadedColorBlend: 0.3,
  });

  const fieldOfView = 25;
  const cameraDistance = 4.5;
  const distanceFactor = Math.min(
    ((cameraDistance - sekaiCostumeShopOutlineSettings.distanceNear) /
      (sekaiCostumeShopOutlineSettings.distanceFar -
        sekaiCostumeShopOutlineSettings.distanceNear)) *
      evaluateSekaiOutlineFovFactor(fieldOfView),
    1
  );
  const worldWidth =
    sekaiCostumeShopOutlineSettings.widthMin +
    (sekaiCostumeShopOutlineSettings.widthMax -
      sekaiCostumeShopOutlineSettings.widthMin) *
      distanceFactor;
  const focalPixels = 2048 / (2 * Math.tan(fieldOfView * Math.PI / 360));
  const outputPixels =
    worldWidth *
    sekaiPreviewOutlineCalibration.widthScale *
    focalPixels /
    cameraDistance;
  assert.ok(outputPixels >= 1.15 && outputPixels <= 1.25, outputPixels);
});

test("raw material lookup preserves unknown exported color properties", () => {
  assert.deepEqual(
    readRawMaterialColor(rawMaterial({
      colorProperties: [
        { name: "_FutureColor", r: 0.1, g: 0.2, b: 0.3, a: 0.4 },
        { name: "_OutlineColor", r: 0.52, g: 0.47, b: 0.55, a: 1 },
      ],
    }), "_OutlineColor"),
    { r: 0.52, g: 0.47, b: 0.55, a: 1 }
  );
});

test("raw material feature state prefers values and preserves serialized enabled keywords", () => {
  const raw = rawMaterial({
    floatProperties: [{ name: "_UseLambert", value: 0 }],
    intProperties: [{ name: "_USELAMBERT", value: 1 }],
    validKeywords: ["_HAIR_SHADOW"],
    invalidKeywords: ["_USE_FACE_SDF"],
  });

  assert.equal(readRawMaterialFloat(raw, "_uselambert"), 0);
  assert.equal(readRawMaterialBoolean(raw, "_UseLambert", "_LAMBERT"), false);
  assert.equal(readRawMaterialBoolean(raw, "_HairShadow", "_hair_shadow"), true);
  assert.equal(readRawMaterialBoolean(raw, "_UseFaceSDF", "_use_face_sdf"), true);
});

test("disabled Unity Outline passes never create a browser shell", () => {
  assert.equal(isSekaiOutlinePassEnabled(rawMaterial()), true);
  assert.equal(isSekaiOutlinePassEnabled(rawMaterial({
    disabledShaderPasses: ["ShadowCaster", "oUtLiNe"],
  })), false);
});

test("official outline consumes material tint, main texture transform, and alpha clip", () => {
  const texture = new THREE.Texture();
  const material = createSekaiOutlineMaterial(
    true,
    rawMaterial({
      textureProperties: [{
        name: "_MainTex",
        textureKey: "tex",
        scaleX: 2,
        scaleY: 3,
        offsetX: 0.25,
        offsetY: 0.5,
      }],
      colorProperties: [
        { name: "_OutlineColor", r: 0.2, g: 0.3, b: 0.4, a: 1 },
      ],
      floatProperties: [
        { name: "_UseAlphaClip", value: 1 },
        { name: "_Cutoff", value: 0.375 },
      ],
    }),
    true,
    texture
  );
  assert.ok(Math.abs(material.color.r - 0.2) < 1e-6);
  assert.ok(Math.abs(material.color.g - 0.3) < 1e-6);
  assert.ok(Math.abs(material.color.b - 0.4) < 1e-6);
  assert.equal(material.map, texture);
  assert.equal(material.alphaTest, 0.375);
  assert.equal(material.side, THREE.BackSide);
  assert.equal(material.depthFunc, THREE.LessDepth);
  assert.equal(material.depthWrite, true);
  assert.equal(material.blending, THREE.NoBlending);
  assert.equal(material.polygonOffset, false);

  const shader = {
    uniforms: THREE.UniformsUtils.clone(THREE.ShaderLib.basic.uniforms),
    vertexShader: THREE.ShaderLib.basic.vertexShader,
    fragmentShader: THREE.ShaderLib.basic.fragmentShader,
  };
  material.onBeforeCompile(shader, {});
  assert.equal(
    shader.uniforms.uSekaiOutlineWidth.value.x,
    sekaiCostumeShopOutlineSettings.widthMin *
      sekaiPreviewOutlineCalibration.widthScale
  );
  assert.equal(
    shader.uniforms.uSekaiOutlineWidth.value.y,
    sekaiCostumeShopOutlineSettings.widthMax *
      sekaiPreviewOutlineCalibration.widthScale
  );
  assert.deepEqual(
    shader.uniforms.uSekaiMainTexST.value.toArray(),
    [2, 3, 0.25, 0.5]
  );
  assert.match(shader.vertexShader, /vSekaiMainTexUv = uv \* uSekaiMainTexST\.xy/);
  assert.match(shader.fragmentShader, /texture2D\(map, vSekaiMainTexUv\)/);
  assert.match(shader.fragmentShader, /sRGBTransferOETF\(sampledDiffuseColor\)/);
  assert.match(shader.fragmentShader, /diffuseColor\.rgb = mix\(/);
  assert.doesNotMatch(shader.fragmentShader, /#include <colorspace_fragment>/);
  assert.doesNotMatch(shader.vertexShader, /uOutlineClipOffset/);

  material.dispose();
  texture.dispose();
});

test("non-Toon outline fallback keeps the bounded material/global blend", () => {
  assert.deepEqual(sekaiCostumeShopOutlineControllerDefaults, {
    color: { r: 0, g: 0, b: 0 },
    blending: 0.5,
  });
  assert.deepEqual(
    evaluateSekaiOutlineColor(
      { r: 0.8, g: 0.6, b: 0.4 },
      { r: 0.5, g: 0.25, b: 1 },
      { r: 0.1, g: 0.2, b: 0.3 },
      0.5
    ),
    { r: 0.25, g: 0.175, b: 0.35 }
  );
});

test("character outline blends the captured Toon result in gamma space like the official 0090 pass", () => {
  const source = new THREE.ShaderMaterial({
    uniforms: {
      uMainTex: { value: new THREE.Texture() },
      uSharedValue: { value: 0.25 },
      uFaceSdfEnabled: { value: 1 },
      uRimColorAlpha: { value: 1 },
      uControllerSpecularIntensity: { value: 1 },
    },
    vertexShader: `
      #include <common>
      #include <beginnormal_vertex>
      #include <begin_vertex>
      #include <skinning_vertex>
      void main() {
        vec4 viewPosition = viewMatrix * modelMatrix * vec4(transformed, 1.0);
        gl_Position = projectionMatrix * viewPosition;
      }
    `,
    fragmentShader: `
      vec3 outputColor(vec3 color) {
        return color;
      }
      void main() {
        gl_FragColor = vec4(outputColor(vec3(uSharedValue)), 1.0);
      }
    `,
  });
  const material = createSekaiOutlineMaterial(
    true,
    rawMaterial({
      floatProperties: [{ name: "_OutlineOffset", value: 10 }],
    }),
    false,
    null,
    source
  );

  assert.ok(material instanceof THREE.ShaderMaterial);
  assert.equal(material.uniforms.uSharedValue, source.uniforms.uSharedValue);
  assert.equal(material.uniforms.uFaceSdfEnabled.value, 0);
  assert.equal(material.uniforms.uRimColorAlpha.value, 0);
  assert.equal(material.uniforms.uControllerSpecularIntensity.value, 0);
  assert.equal(source.uniforms.uFaceSdfEnabled.value, 1);
  assert.equal(source.uniforms.uRimColorAlpha.value, 1);
  assert.equal(source.uniforms.uControllerSpecularIntensity.value, 1);
  assert.match(material.vertexShader, /outlineWidth \* outlineScale/);
  assert.match(material.vertexShader, /uSekaiOutlineOffset/);
  assert.match(material.fragmentShader, /uSekaiCharacterOutlineColor/);
  assert.match(material.fragmentShader, /uSekaiCharacterOutlineBlending/);
  assert.equal(
    material.uniforms.uSekaiCharacterOutlineBlending.value,
    sekaiPreviewOutlineCalibration.shadedColorBlend
  );
  // The captured 0090 outline fragment computes
  //   mix(outlineColorArray[i].rgb * a, shadedColor, blendingArray[i])
  // directly on the Gamma-space values of the game's Gamma pipeline. The
  // gamma-form Toon color must therefore be blended as-is; decoding to
  // linear first lifts the outline a full tier brighter than the official
  // preview (measured (94,45,33) vs official (65,32,24) on hair shadow2).
  assert.match(
    material.fragmentShader,
    /return mix\(\s*uSekaiCharacterOutlineColor,\s*clamp\(color, 0\.0, 1\.0\),/
  );
  assert.doesNotMatch(material.fragmentShader, /sekaiOutlineSrgbToLinear/);
  assert.doesNotMatch(
    material.fragmentShader,
    /return mix\(\s*color,\s*uSekaiCharacterOutlineColor/
  );
  assert.equal(material.side, THREE.BackSide);
  assert.equal(material.depthWrite, true);
  assert.equal(material.blending, THREE.NoBlending);

  material.dispose();
  source.uniforms.uMainTex.value.dispose();
  source.dispose();
});

test("outline offset pushback reaches face-style gl_Position statements", () => {
  // The face vertex shader ends with
  //   gl_Position = projectionMatrix * viewMatrix * worldPosition;
  // while the body shader uses a precomputed viewPosition. The clip-space
  // _OutlineOffset pushback must attach to both forms, or face/eyelash
  // shells (offsets 10 / 2.5) silently lose their official depth push.
  const faceStyleSource = new THREE.ShaderMaterial({
    uniforms: {},
    vertexShader: `
      #include <common>
      void main() {
        #include <beginnormal_vertex>
        #include <defaultnormal_vertex>
        #include <begin_vertex>
        vec4 worldPosition = modelMatrix * vec4(transformed, 1.0);
        gl_Position = projectionMatrix * viewMatrix * worldPosition;
      }
    `,
    fragmentShader: `
      vec3 outputColor(vec3 color) {
        return color;
      }
      void main() {
        gl_FragColor = vec4(outputColor(vec3(1.0)), 1.0);
      }
    `,
  });
  const material = createSekaiOutlineMaterial(
    true,
    rawMaterial({
      floatProperties: [{ name: "_OutlineOffset", value: 10 }],
    }),
    false,
    null,
    faceStyleSource
  );

  assert.ok(material instanceof THREE.ShaderMaterial);
  assert.match(
    material.vertexShader,
    /gl_Position \+= projectedCameraOrigin \* \(-0\.01 \* uSekaiOutlineOffset\) \* outlineOffsetScale;/
  );

  material.dispose();
  faceStyleSource.dispose();
});

test("second-normal outline direction uses the official single-normalize form", () => {
  const source = new THREE.ShaderMaterial({
    uniforms: {},
    vertexShader: `
      #include <common>
      void main() {
        #include <beginnormal_vertex>
        #include <defaultnormal_vertex>
        #include <begin_vertex>
        vec4 viewPosition = viewMatrix * modelMatrix * vec4(transformed, 1.0);
        gl_Position = projectionMatrix * viewPosition;
      }
    `,
    fragmentShader: `
      vec3 outputColor(vec3 color) {
        return color;
      }
      void main() {
        gl_FragColor = vec4(outputColor(vec3(1.0)), 1.0);
      }
    `,
  });
  const material = createSekaiOutlineMaterial(true, rawMaterial({}), true, null, source);

  assert.ok(material instanceof THREE.ShaderMaterial);
  // Official 0091 builds the direction from RAW attributes with one final
  // normalize: normalize(T*uv1.x + cross(N,T)*T.w*uv1.y + N*uv2.x).
  // Per-term normalizes turn degenerate tangents into NaN vertices where
  // the official shader still produces a finite direction.
  assert.match(
    material.vertexShader,
    /vec3 outlineSecondBitangent = cross\(normal, tangent\.xyz\) \* tangent\.w;/
  );
  assert.match(
    material.vertexShader,
    /vec3 outlineDirection = normalize\(tangent\.xyz \* uv1\.x \+ outlineSecondBitangent \* uv1\.y \+ normal \* uv2\.x\);/
  );
  assert.doesNotMatch(material.vertexShader, /normalize\(vec3\(uv1\.xy/);
  assert.doesNotMatch(material.vertexShader, /baseBitangent/);

  material.dispose();
  source.dispose();
});

test("character outline ramp normal cancels three's FLIP_SIDED negation", () => {
  const source = new THREE.ShaderMaterial({
    uniforms: {},
    vertexShader: `
      #include <common>
      void main() {
        #include <beginnormal_vertex>
        #include <defaultnormal_vertex>
        #include <begin_vertex>
        vec4 viewPosition = viewMatrix * modelMatrix * vec4(transformed, 1.0);
        gl_Position = projectionMatrix * viewPosition;
      }
    `,
    fragmentShader: `
      vec3 outputColor(vec3 color) {
        return color;
      }
      void main() {
        gl_FragColor = vec4(outputColor(vec3(1.0)), 1.0);
      }
    `,
  });
  const material = createSekaiOutlineMaterial(true, rawMaterial({}), false, null, source);

  assert.ok(material instanceof THREE.ShaderMaterial);
  // The BackSide shell gets FLIP_SIDED from three, which negates
  // transformedNormal inside defaultnormal_vertex. The captured official
  // outline vertex (0089/0091) feeds the raw mesh normal to the Toon ramp,
  // so the shell must undo that negation or the ramp inverts at silhouettes.
  assert.match(
    material.vertexShader,
    /#include <defaultnormal_vertex>\s*\n\s*#ifdef FLIP_SIDED\s*\n\s*transformedNormal = -transformedNormal;\s*\n\s*#endif/
  );
  assert.doesNotMatch(source.vertexShader, /FLIP_SIDED/);

  material.dispose();
  source.dispose();
});
