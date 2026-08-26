export type SekaiBaseShadowInput = {
  normalDotLight: number;
  valueB: number;
  useLambert: boolean;
  useValueTex: boolean;
  threshold: number;
  width: number;
  fadeMode: number;
};

export type SekaiFaceShadowInput = {
  sdf: number;
  mirroredSdf: number;
  headDotX: number;
  headDotY: number;
  useLimiter: boolean;
  rangeLimit: number;
  width: number;
  fadeMode: number;
};

export type SekaiSkinColorInput = {
  skinValue: number;
  globalShadow: readonly [number, number, number];
  defaultSkin: readonly [number, number, number];
  shadow1Skin: readonly [number, number, number];
  shadow2Skin: readonly [number, number, number];
};

function saturate(value: number) {
  return Math.min(Math.max(value, 0), 1);
}

function smooth01(value: number) {
  const x = saturate(value);
  return x * x * (3 - 2 * x);
}

export function evaluateSekaiBaseShadow(input: SekaiBaseShadowInput) {
  const halfLambert = input.normalDotLight * 0.5 + 0.5;
  const baseLight = input.useLambert ? halfLambert : 1;
  const valueB = input.useValueTex ? input.valueB : 0.5;
  const rawLight = saturate(baseLight + 2 * valueB - 1);
  const threshold = saturate(input.threshold);
  const width = saturate(input.width);
  const denominator = input.fadeMode < 0.5
    ? Math.max(threshold * width, 1e-5)
    : Math.max((1 - threshold) * width, 1e-5);
  const q = input.fadeMode < 0.5
    ? saturate((rawLight - threshold * (1 - width)) / denominator)
    : saturate((rawLight - threshold) / denominator);
  return { rawLight, shadow: 1 - smooth01(q) };
}

export function evaluateSekaiFaceShadow(input: SekaiFaceShadowInput) {
  const sdf = input.headDotX <= 0 ? input.mirroredSdf : input.sdf;
  const threshold = saturate((input.useLimiter
    ? Math.min(
        Math.max((1 - Math.abs(2 * input.headDotY - 1)) * 0.5, 0),
        input.rangeLimit
      )
    : input.headDotY));
  const width = saturate(input.width);
  const q = input.fadeMode < 0.5
    ? saturate((threshold - sdf) / Math.max((1 - sdf) * width, 1e-5))
    : saturate((sdf - threshold) / Math.max((1 - threshold) * width, 1e-5));
  const shadow = input.fadeMode < 0.5 ? smooth01(q) : 1 - smooth01(q);
  return { sdf, threshold, shadow };
}

export function evaluateSekaiSkinColor(input: SekaiSkinColorInput) {
  const lower = saturate(input.skinValue * 2);
  const upper = saturate(input.skinValue * 2 - 1);
  return input.defaultSkin.map((lit, index) => {
    const mid = input.shadow1Skin[index] * input.globalShadow[index];
    const dark = input.shadow2Skin[index] * input.globalShadow[index];
    return dark + (mid + (lit - mid) * upper - dark) * lower;
  }) as [number, number, number];
}

export const sekaiCharacterShadowFunctionsGlsl = `
float sekaiSmooth01(float value) {
  float x = clamp(value, 0.0, 1.0);
  return x * x * (3.0 - 2.0 * x);
}

float sekaiBaseShadow(
  float normalDotLight,
  float valueB,
  float useLambert,
  float useValueTex,
  float threshold,
  float width,
  float fadeMode
) {
  float halfLambert = normalDotLight * 0.5 + 0.5;
  float baseLight = useLambert > 0.5 ? halfLambert : 1.0;
  float selectedValueB = useValueTex > 0.5 ? valueB : 0.5;
  float rawLight = clamp(baseLight + 2.0 * selectedValueB - 1.0, 0.0, 1.0);
  float t = clamp(threshold, 0.0, 1.0);
  float w = clamp(width, 0.0, 1.0);
  float q = fadeMode < 0.5
    ? clamp((rawLight - t * (1.0 - w)) / max(t * w, 0.00001), 0.0, 1.0)
    : clamp((rawLight - t) / max((1.0 - t) * w, 0.00001), 0.0, 1.0);
  return 1.0 - sekaiSmooth01(q);
}

float sekaiFaceShadow(
  float sdf,
  float threshold,
  float width,
  float fadeMode
) {
  float w = clamp(width, 0.0, 1.0);
  float q = fadeMode < 0.5
    ? clamp((threshold - sdf) / max((1.0 - sdf) * w, 0.00001), 0.0, 1.0)
    : clamp((sdf - threshold) / max((1.0 - threshold) * w, 0.00001), 0.0, 1.0);
  return fadeMode < 0.5 ? sekaiSmooth01(q) : 1.0 - sekaiSmooth01(q);
}
`;

export const sekaiCharacterColorFunctionsGlsl = `
vec3 sekaiApplyHsvc(
  vec3 color,
  float hueSin,
  float hueCos,
  float saturation,
  float value,
  float contrast
) {
  vec3 axis = vec3(0.577350259);
  vec3 rotated =
    color * hueCos +
    cross(axis, color) * hueSin +
    axis * dot(axis, color) * (1.0 - hueCos);
  rotated =
    (rotated - vec3(0.5)) * (contrast * 2.0) +
    vec3(value * 2.0 - 0.5);
  float luma = dot(rotated, vec3(0.22, 0.707, 0.071));
  return (rotated - vec3(luma)) * (saturation * 2.0) + vec3(luma);
}

vec3 sekaiSkinRamp(
  float skinValue,
  vec3 globalShadow,
  vec3 defaultSkin,
  vec3 shadow1Skin,
  vec3 shadow2Skin
) {
  vec3 mid = globalShadow * shadow1Skin;
  vec3 dark = globalShadow * shadow2Skin;
  vec3 upperBand = mix(mid, defaultSkin, clamp(skinValue * 2.0 - 1.0, 0.0, 1.0));
  return mix(dark, upperBand, clamp(skinValue * 2.0, 0.0, 1.0));
}

vec3 sekaiOverlay(vec3 base, vec3 blend) {
  vec3 multiplyBranch = 2.0 * base * blend;
  vec3 screenBranch = 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
  return mix(multiplyBranch, screenBranch, step(vec3(0.5), base));
}

vec3 sekaiApplyCharacterAmbient(
  vec3 color,
  vec3 ambientColor,
  float ambientIntensity,
  vec4 partsAmbientColor
) {
  vec3 overlaid = sekaiOverlay(color, ambientColor);
  float intensity = ambientIntensity;
  vec3 multiplied = overlaid * intensity * partsAmbientColor.rgb;
  vec3 screened =
    1.0 -
    2.0 * (1.0 - overlaid * intensity) * (1.0 - partsAmbientColor.rgb);
  return mix(screened, multiplied, clamp(partsAmbientColor.a, 0.0, 1.0));
}

`;
