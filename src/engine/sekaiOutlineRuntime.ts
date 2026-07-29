import * as THREE from "three";
import type {
  RawMaterialProperties,
} from "../data/sampleScene";
import {
  readRawMaterialColor,
  readRawMaterialFloat,
  readRawMaterialTexture,
} from "./rawMaterialRuntime";
import { setSekaiGammaColor } from "../materials/sekaiCharacterShader";

export {
  readRawMaterialColor,
} from "./rawMaterialRuntime";

export const sekaiCostumeShopOutlineSettings = {
  widthMin: 0.0004,
  widthMax: 0.0095,
  distanceNear: 0.45,
  distanceFar: 20,
} as const;

const sekaiOutlineFovCurve = {
  startTime: -0.013763427734375,
  startValue: 27.81246566772461,
  startOutTangent: -0.13214513659477234,
  endTime: 100.92341613769531,
  endValue: -0.03620624542236328,
  endInTangent: -0.5713597536087036,
} as const;

/** Reconstructs the captured ClampForever, unweighted Unity FOV curve. */
export function evaluateSekaiOutlineFovFactor(fieldOfView: number) {
  const fov = Number.isFinite(fieldOfView) ? fieldOfView : 25;
  const curve = sekaiOutlineFovCurve;
  let curveValue: number;
  if (fov <= curve.startTime) {
    curveValue = curve.startValue;
  } else if (fov >= curve.endTime) {
    curveValue = curve.endValue;
  } else {
    const duration = curve.endTime - curve.startTime;
    const t = (fov - curve.startTime) / duration;
    const t2 = t * t;
    const t3 = t2 * t;
    curveValue =
      (2 * t3 - 3 * t2 + 1) * curve.startValue +
      (t3 - 2 * t2 + t) * duration * curve.startOutTangent +
      (-2 * t3 + 3 * t2) * curve.endValue +
      (t3 - t2) * duration * curve.endInTangent;
  }
  return Math.abs(curveValue) > Number.EPSILON ? fov / curveValue : 1;
}

export type SekaiRgb = {
  r: number;
  g: number;
  b: number;
};

export type SekaiRgba = SekaiRgb & {
  a: number;
};

export const sekaiCostumeShopOutlineControllerDefaults = {
  color: { r: 0, g: 0, b: 0 },
  blending: 0.5,
} as const;

/**
 * Presentation calibration for direct high-resolution browser output.
 *
 * CostumeShop renders its preview into a smaller intermediate texture before
 * the UI scales it up. The browser kernel renders directly at the final device
 * resolution, so applying the captured world-space shell unchanged makes it
 * visibly thicker and lets too much shaded material color into the outline.
 * Keep the captured globals above intact and correct only the presentation.
 */
export const sekaiPreviewOutlineCalibration = {
  widthScale: 0.5,
  shadedColorBlend: 0.3,
} as const;

function createSekaiPreviewOutlineWidth() {
  return new THREE.Vector2(
    sekaiCostumeShopOutlineSettings.widthMin *
      sekaiPreviewOutlineCalibration.widthScale,
    sekaiCostumeShopOutlineSettings.widthMax *
      sekaiPreviewOutlineCalibration.widthScale
  );
}

export function isSekaiOutlinePassEnabled(
  rawMaterial: RawMaterialProperties | null | undefined
) {
  return !rawMaterial?.disabledShaderPasses?.some(
    (pass) => pass.toLowerCase() === "outline"
  );
}

const legacyMaterialOutlineColor = {
  r: 0.52,
  g: 0.47,
  b: 0.55,
  a: 1,
} as const;

type SekaiOutlineControllerState = {
  color: THREE.Color;
  blending: number;
};

export function evaluateSekaiOutlineColor(
  mainTexture: SekaiRgb,
  materialOutline: SekaiRgb,
  globalOutline: SekaiRgb,
  blending: number
): SekaiRgb {
  // Bounded non-character fallback. CostumeShop character materials instead
  // use the driver-final shaded-color/global-outline blend below.
  const weight = THREE.MathUtils.clamp(blending, 0, 1);
  const materialTerm = {
    r: mainTexture.r * materialOutline.r,
    g: mainTexture.g * materialOutline.g,
    b: mainTexture.b * materialOutline.b,
  };
  return {
    r: materialTerm.r + weight * (globalOutline.r - materialTerm.r),
    g: materialTerm.g + weight * (globalOutline.g - materialTerm.g),
    b: materialTerm.b + weight * (globalOutline.b - materialTerm.b),
  };
}

export function applySekaiOutlineController(
  material: THREE.Material,
  color: THREE.ColorRepresentation | SekaiRgb | null | undefined,
  blending: number | null | undefined
) {
  if (material.name !== "pjsk_shell_outline") {
    return;
  }
  const state = material.userData.pjskOutlineController as
    | SekaiOutlineControllerState
    | undefined;
  if (!state) {
    return;
  }
  if (
    color &&
    typeof color === "object" &&
    "r" in color &&
    "g" in color &&
    "b" in color
  ) {
    state.color.setRGB(color.r, color.g, color.b);
  } else {
    setSekaiGammaColor(
      state.color,
      color ??
        new THREE.Color().setRGB(
          sekaiCostumeShopOutlineControllerDefaults.color.r,
          sekaiCostumeShopOutlineControllerDefaults.color.g,
          sekaiCostumeShopOutlineControllerDefaults.color.b
        )
    );
  }
  state.blending = THREE.MathUtils.clamp(
    blending ?? sekaiCostumeShopOutlineControllerDefaults.blending,
    0,
    1
  );
}

function createSekaiToonOutlineMaterial(
  source: THREE.ShaderMaterial,
  useVertexColor: boolean,
  rawMaterial: RawMaterialProperties | undefined,
  useSecondNormal: boolean
) {
  const outlineFactor = new THREE.Vector3(
    sekaiCostumeShopOutlineSettings.distanceNear,
    1 /
      (
        sekaiCostumeShopOutlineSettings.distanceFar -
        sekaiCostumeShopOutlineSettings.distanceNear
      ),
    evaluateSekaiOutlineFovFactor(25)
  );
  const controllerState: SekaiOutlineControllerState = {
    color: new THREE.Color().setRGB(
      sekaiCostumeShopOutlineControllerDefaults.color.r,
      sekaiCostumeShopOutlineControllerDefaults.color.g,
      sekaiCostumeShopOutlineControllerDefaults.color.b
    ),
    blending: sekaiPreviewOutlineCalibration.shadedColorBlend,
  };
  const outlineOffset = readRawMaterialFloat(rawMaterial, "_OutlineOffset") ?? 0;
  const material = source.clone();
  material.name = "pjsk_shell_outline";
  material.side = THREE.BackSide;
  material.transparent = false;
  material.opacity = 1;
  material.depthFunc = THREE.LessDepth;
  material.depthWrite = true;
  material.depthTest = true;
  material.blending = THREE.NoBlending;
  material.polygonOffset = false;
  material.userData = {
    ...source.userData,
    pjskOutlineController: controllerState,
  };

  // The captured CostumeShop outline fragment keeps the base Toon/skin/ambient
  // path, but it has no FaceSDF, rim-light, or specular branch. Keep separate
  // uniform objects so the outline pass cannot mutate the visible front pass.
  const outlineOnlyUniforms: Record<string, THREE.IUniform> = {};
  for (const name of [
    "uFaceSdfEnabled",
    "uRimColorAlpha",
    "uControllerSpecularIntensity",
  ]) {
    if (name in source.uniforms) {
      outlineOnlyUniforms[name] = { value: 0 };
    }
  }
  material.uniforms = {
    ...source.uniforms,
    ...outlineOnlyUniforms,
    uSekaiOutlineWidth: { value: createSekaiPreviewOutlineWidth() },
    uSekaiOutlineFactor: { value: outlineFactor },
    uSekaiOutlineOffset: { value: outlineOffset },
    uSekaiCharacterOutlineColor: { value: controllerState.color },
    uSekaiCharacterOutlineBlending: {
      get value() {
        return controllerState.blending;
      },
    },
  };
  material.vertexShader = source.vertexShader.replace(
    "#include <common>",
    [
      "#include <common>",
      "uniform vec2 uSekaiOutlineWidth;",
      "uniform vec3 uSekaiOutlineFactor;",
      "uniform float uSekaiOutlineOffset;",
      useSecondNormal ? "attribute vec4 tangent;" : "",
      useSecondNormal ? "attribute vec2 uv1;" : "",
      useSecondNormal ? "attribute vec2 uv2;" : "",
    ].join("\n")
  );
  material.vertexShader = material.vertexShader.replace(
    "#include <defaultnormal_vertex>",
    [
      "#include <defaultnormal_vertex>",
      // The captured outline vertex shaders (0089/0091) pass the raw mesh
      // normal to the Toon ramp. Three defines FLIP_SIDED for this BackSide
      // shell and negates transformedNormal inside defaultnormal_vertex,
      // which inverts the ramp at silhouettes; undo it so the shell shades
      // with the same normal as the front pass.
      "#ifdef FLIP_SIDED",
      "transformedNormal = -transformedNormal;",
      "#endif",
    ].join("\n")
  );
  material.vertexShader = material.vertexShader.replace(
    "#include <begin_vertex>",
    [
      "#include <begin_vertex>",
      "vec3 outlineWorldPosition = (modelMatrix * vec4(position, 1.0)).xyz;",
      "float outlineDistance = length(outlineWorldPosition - cameraPosition);",
      "float outlineDistanceFactor = clamp((outlineDistance - uSekaiOutlineFactor.x) * uSekaiOutlineFactor.y, 0.0, 1.0);",
      "outlineDistanceFactor = min(outlineDistanceFactor * uSekaiOutlineFactor.z, 1.0);",
      "float outlineWidth = mix(uSekaiOutlineWidth.x, uSekaiOutlineWidth.y, outlineDistanceFactor);",
      useSecondNormal
        ? [
            // Official 0091 builds the direction from the raw attributes
            // with a single final normalize; per-term normalizes turn
            // degenerate tangents into NaN vertices where the official
            // shader still produces a finite direction.
            "vec3 outlineSecondBitangent = cross(normal, tangent.xyz) * tangent.w;",
            "vec3 outlineDirection = normalize(tangent.xyz * uv1.x + outlineSecondBitangent * uv1.y + normal * uv2.x);",
          ].join("\n")
        : "vec3 outlineDirection = normalize(normal);",
      useVertexColor
        ? "float outlineScale = clamp(color.r, 0.0, 1.0);"
        : "float outlineScale = 1.0;",
      useVertexColor
        ? "float outlineOffsetScale = clamp(color.b, 0.0, 1.0);"
        : "float outlineOffsetScale = 0.0;",
      "transformed += outlineDirection * outlineWidth * outlineScale;",
    ].join("\n")
  );
  // The body shader ends with "gl_Position = projectionMatrix * viewPosition;"
  // while the face shader ends with
  // "gl_Position = projectionMatrix * viewMatrix * worldPosition;" — the
  // offset pushback must attach to whichever form the source uses, or the
  // face/eyelash shells silently lose their official _OutlineOffset push.
  material.vertexShader = material.vertexShader.replace(
    /gl_Position\s*=\s*projectionMatrix\s*\*[^;]*;/,
    (glPositionStatement) => [
      glPositionStatement,
      "vec4 projectedCameraOrigin = projectionMatrix * viewMatrix * vec4(cameraPosition, 1.0);",
      "gl_Position += projectedCameraOrigin * (-0.01 * uSekaiOutlineOffset) * outlineOffsetScale;",
    ].join("\n")
  );
  material.fragmentShader = source.fragmentShader.replace(
    /vec3 outputColor\s*\(\s*vec3 color\s*\)\s*\{\s*return color;\s*\}/,
    [
      "uniform vec3 uSekaiCharacterOutlineColor;",
      "uniform float uSekaiCharacterOutlineBlending;",
      "",
      // The captured 0090 outline fragment runs in the game's Gamma
      // pipeline and computes
      //   mix(outlineColorArray[i].rgb * a, shadedColor, blendingArray[i])
      // directly on gamma-form values. The Toon color reaching outputColor
      // is already gamma-form, so blend it as-is; a linear-space blend
      // lifts the outline a full tier brighter than the official preview.
      "vec3 outputColor(vec3 color) {",
      "  return mix(",
      "    uSekaiCharacterOutlineColor,",
      "    clamp(color, 0.0, 1.0),",
      "    clamp(uSekaiCharacterOutlineBlending, 0.0, 1.0)",
      "  );",
      "}",
    ].join("\n")
  );
  material.customProgramCacheKey = () =>
    `sekai-toon-outline:${useVertexColor ? 1 : 0}:${useSecondNormal ? 1 : 0}`;
  material.onBeforeRender = (_renderer, _scene, camera) => {
    if (camera instanceof THREE.PerspectiveCamera) {
      outlineFactor.z = evaluateSekaiOutlineFovFactor(camera.fov);
    }
  };
  return material;
}

export function createSekaiOutlineMaterial(
  useVertexColor: boolean,
  rawMaterial?: RawMaterialProperties,
  useSecondNormal = false,
  sourceMainTex: THREE.Texture | null = null,
  sourceMaterial: THREE.Material | null = null
) {
  if (
    sourceMaterial instanceof THREE.ShaderMaterial &&
    /vec3 outputColor\s*\(\s*vec3 color\s*\)/.test(sourceMaterial.fragmentShader)
  ) {
    return createSekaiToonOutlineMaterial(
      sourceMaterial,
      useVertexColor,
      rawMaterial,
      useSecondNormal
    );
  }
  const serializedOutlineColor =
    readRawMaterialColor(rawMaterial, "_OutlineColor") ??
    legacyMaterialOutlineColor;
  const mainTextureProperty = readRawMaterialTexture(rawMaterial, "_MainTex");
  const mainTextureTransform = new THREE.Vector4(
    mainTextureProperty?.scaleX ?? 1,
    mainTextureProperty?.scaleY ?? 1,
    mainTextureProperty?.offsetX ?? 0,
    mainTextureProperty?.offsetY ?? 0
  );
  const useAlphaClip =
    (readRawMaterialFloat(rawMaterial, "_UseAlphaClip") ?? 0) > 0.5;
  const alphaCutoff = THREE.MathUtils.clamp(
    readRawMaterialFloat(rawMaterial, "_Cutoff") ?? 0.5,
    0,
    1
  );
  const outlineOffset = readRawMaterialFloat(rawMaterial, "_OutlineOffset") ?? 0;
  const materialOutlineColor = new THREE.Color().setRGB(
    serializedOutlineColor.r,
    serializedOutlineColor.g,
    serializedOutlineColor.b
  );
  const controllerState: SekaiOutlineControllerState = {
    color: new THREE.Color().setRGB(
      sekaiCostumeShopOutlineControllerDefaults.color.r,
      sekaiCostumeShopOutlineControllerDefaults.color.g,
      sekaiCostumeShopOutlineControllerDefaults.color.b
    ),
    blending: sekaiPreviewOutlineCalibration.shadedColorBlend,
  };
  const material = new THREE.MeshBasicMaterial({
    color: materialOutlineColor,
    map: sourceMainTex,
    side: THREE.BackSide,
    transparent: false,
    opacity: 1,
    depthFunc: THREE.LessDepth,
    depthWrite: true,
    depthTest: true,
    blending: THREE.NoBlending,
    vertexColors: false,
    alphaTest: useAlphaClip ? alphaCutoff : 0,
  });
  const outlineFactor = new THREE.Vector3(
    sekaiCostumeShopOutlineSettings.distanceNear,
    1 /
      (
        sekaiCostumeShopOutlineSettings.distanceFar -
        sekaiCostumeShopOutlineSettings.distanceNear
      ),
    evaluateSekaiOutlineFovFactor(25)
  );
  material.name = "pjsk_shell_outline";
  material.userData.pjskOutlineController = controllerState;
  material.onBeforeCompile = (shader) => {
    shader.uniforms.uSekaiOutlineWidth = {
      value: createSekaiPreviewOutlineWidth(),
    };
    shader.uniforms.uSekaiOutlineFactor = {
      value: outlineFactor,
    };
    shader.uniforms.uSekaiOutlineOffset = {
      value: outlineOffset,
    };
    shader.uniforms.uSekaiMainTexST = { value: mainTextureTransform };
    shader.uniforms.uSekaiCharacterOutlineColor = {
      value: controllerState.color,
    };
    shader.uniforms.uSekaiCharacterOutlineBlending = {
      get value() {
        return controllerState.blending;
      },
    };
    shader.vertexShader = shader.vertexShader.replace(
      "#include <common>",
      [
        "#include <common>",
        "uniform vec2 uSekaiOutlineWidth;",
        "uniform vec3 uSekaiOutlineFactor;",
        "uniform float uSekaiOutlineOffset;",
        "uniform vec4 uSekaiMainTexST;",
        "#ifdef USE_MAP",
        "varying vec2 vSekaiMainTexUv;",
        "#endif",
        useVertexColor ? "attribute vec3 color;" : "",
        useSecondNormal ? "attribute vec4 tangent;" : "",
        useSecondNormal ? "attribute vec2 uv1;" : "",
        useSecondNormal ? "attribute vec2 uv2;" : "",
      ].join("\n")
    );
    shader.vertexShader = shader.vertexShader.replace(
      "#include <begin_vertex>",
      [
        "#include <begin_vertex>",
        "vec3 outlineWorldPosition = (modelMatrix * vec4(position, 1.0)).xyz;",
        "float outlineDistance = length(outlineWorldPosition - cameraPosition);",
        "float outlineDistanceFactor = clamp((outlineDistance - uSekaiOutlineFactor.x) * uSekaiOutlineFactor.y, 0.0, 1.0);",
        "outlineDistanceFactor = min(outlineDistanceFactor * uSekaiOutlineFactor.z, 1.0);",
        "float outlineWidth = mix(uSekaiOutlineWidth.x, uSekaiOutlineWidth.y, outlineDistanceFactor);",
        useSecondNormal
          ? [
              // Match 0091: raw attributes, one final normalize (see the
              // Toon path above).
              "vec3 outlineSecondBitangent = cross(normal, tangent.xyz) * tangent.w;",
              "vec3 outlineDirection = normalize(tangent.xyz * uv1.x + outlineSecondBitangent * uv1.y + normal * uv2.x);",
            ].join("\n")
          : "vec3 outlineDirection = normalize(normal);",
        useVertexColor
          ? "float outlineScale = clamp(color.r, 0.0, 1.0);"
          : "float outlineScale = 1.0;",
        useVertexColor
          ? "float outlineOffsetScale = clamp(color.b, 0.0, 1.0);"
          : "float outlineOffsetScale = 0.0;",
        "transformed += outlineDirection * outlineWidth * outlineScale;",
      ].join("\n")
    );
    shader.vertexShader = shader.vertexShader.replace(
      "#include <project_vertex>",
      [
        "#include <project_vertex>",
        "vec4 projectedCameraOrigin = projectionMatrix * viewMatrix * vec4(cameraPosition, 1.0);",
        "gl_Position += projectedCameraOrigin * (-0.01 * uSekaiOutlineOffset) * outlineOffsetScale;",
      ].join("\n")
    );
    shader.vertexShader = shader.vertexShader.replace(
      "#include <uv_vertex>",
      [
        "#include <uv_vertex>",
        "#ifdef USE_MAP",
        "vSekaiMainTexUv = uv * uSekaiMainTexST.xy + uSekaiMainTexST.zw;",
        "#endif",
      ].join("\n")
    );
    shader.fragmentShader = shader.fragmentShader.replace(
      "#include <common>",
      [
        "#include <common>",
        "uniform vec3 uSekaiCharacterOutlineColor;",
        "uniform float uSekaiCharacterOutlineBlending;",
        "#ifdef USE_MAP",
        "varying vec2 vSekaiMainTexUv;",
        "#endif",
      ].join("\n")
    );
    shader.fragmentShader = shader.fragmentShader.replace(
      "#include <map_fragment>",
      [
        "#ifdef USE_MAP",
        "  vec4 sampledDiffuseColor = texture2D(map, vSekaiMainTexUv);",
        "  #ifdef DECODE_VIDEO_TEXTURE",
        "    sampledDiffuseColor = sRGBTransferEOTF(sampledDiffuseColor);",
        "  #endif",
        "  sampledDiffuseColor = sRGBTransferOETF(sampledDiffuseColor);",
        "  diffuseColor *= sampledDiffuseColor;",
        "#endif",
      ].join("\n")
    );
    shader.fragmentShader = shader.fragmentShader.replace(
      "#include <color_fragment>",
      [
        "#include <color_fragment>",
        "diffuseColor.rgb = mix(",
        "  diffuseColor.rgb,",
        "  uSekaiCharacterOutlineColor,",
        "  clamp(uSekaiCharacterOutlineBlending, 0.0, 1.0)",
        ");",
      ].join("\n")
    );
    shader.fragmentShader = shader.fragmentShader.replace(
      "#include <colorspace_fragment>",
      ""
    );
  };
  material.customProgramCacheKey = () =>
    `sekai-outline:${useVertexColor ? 1 : 0}:${useSecondNormal ? 1 : 0}`;
  material.onBeforeRender = (_renderer, _scene, camera) => {
    if (camera instanceof THREE.PerspectiveCamera) {
      outlineFactor.z = evaluateSekaiOutlineFovFactor(camera.fov);
    }
  };
  return material;
}
