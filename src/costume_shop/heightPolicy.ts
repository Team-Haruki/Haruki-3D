import * as THREE from "three";

const NEUTRAL_HEIGHT_METERS = 1.6;

export function resolveCostumeShopHeightRate(masterHeightMeters: number) {
  const height = normalizeCharacterHeight(masterHeightMeters);
  return 0.5 + 0.8 / height;
}

export function resolveCostumeShopModelScale(masterHeightMeters: number) {
  return resolveCostumeShopHeightRate(masterHeightMeters);
}

function normalizeCharacterHeight(masterHeightMeters: number) {
  return THREE.MathUtils.clamp(
    masterHeightMeters || NEUTRAL_HEIGHT_METERS,
    0.5,
    2
  );
}
