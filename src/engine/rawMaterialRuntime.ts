import * as THREE from "three";
import type {
  RawMaterialProperties,
  RawMaterialTextureProperty,
} from "../data/sampleScene";

function matchesPropertyName(name: string, propertyName: string) {
  return name.toLowerCase() === propertyName.toLowerCase();
}

export function readRawMaterialFloat(
  rawMaterial: RawMaterialProperties | null | undefined,
  propertyName: string
) {
  const floatProperty = rawMaterial?.floatProperties?.find((entry) =>
    matchesPropertyName(entry.name, propertyName)
  );
  if (Number.isFinite(floatProperty?.value)) {
    return floatProperty!.value;
  }
  const intProperty = rawMaterial?.intProperties?.find((entry) =>
    matchesPropertyName(entry.name, propertyName)
  );
  return Number.isFinite(intProperty?.value) ? intProperty!.value : null;
}

export function readRawMaterialBoolean(
  rawMaterial: RawMaterialProperties | null | undefined,
  propertyName: string,
  keyword?: string
) {
  const value = readRawMaterialFloat(rawMaterial, propertyName);
  if (value !== null) {
    return value > 0.5;
  }
  if (!keyword) {
    return null;
  }
  if (rawMaterial?.validKeywords?.some((entry) => matchesPropertyName(entry, keyword))) {
    return true;
  }
  if (rawMaterial?.invalidKeywords?.some((entry) => matchesPropertyName(entry, keyword))) {
    // Unity serializes enabled keywords into valid/invalid buckets according
    // to the keyword space available while the Material is loaded. Character
    // bundles can reference their shader externally, so an enabled keyword
    // such as _LAMBERT is commonly stored in m_InvalidKeywords until runtime.
    return true;
  }
  return null;
}

export function readRawMaterialColor(
  rawMaterial: RawMaterialProperties | null | undefined,
  propertyName: string
) {
  const property = rawMaterial?.colorProperties?.find((entry) =>
    matchesPropertyName(entry.name, propertyName)
  );
  if (
    !property ||
    !Number.isFinite(property.r) ||
    !Number.isFinite(property.g) ||
    !Number.isFinite(property.b) ||
    !Number.isFinite(property.a)
  ) {
    return null;
  }
  return { r: property.r, g: property.g, b: property.b, a: property.a };
}

export function readRawMaterialTexture(
  rawMaterial: RawMaterialProperties | null | undefined,
  propertyName: string
): RawMaterialTextureProperty | null {
  return rawMaterial?.textureProperties?.find((entry) =>
    matchesPropertyName(entry.name, propertyName)
  ) ?? null;
}

export function applyRawMaterialTextureTransform(
  texture: THREE.Texture | null | undefined,
  rawMaterial: RawMaterialProperties | null | undefined,
  propertyName: string
) {
  if (!texture) {
    return;
  }
  const property = readRawMaterialTexture(rawMaterial, propertyName);
  if (property) {
    texture.repeat.set(property.scaleX, property.scaleY);
    texture.offset.set(property.offsetX, property.offsetY);
    texture.wrapS = unityWrapMode(property.wrapU);
    texture.wrapT = unityWrapMode(property.wrapV);
    texture.anisotropy = Math.max(1, property.anisoLevel || 1);
    if (property.filterMode === 0) {
      texture.magFilter = THREE.NearestFilter;
      texture.minFilter = THREE.NearestMipmapNearestFilter;
    } else {
      texture.magFilter = THREE.LinearFilter;
      texture.minFilter = property.filterMode === 2
        ? THREE.LinearMipmapLinearFilter
        : THREE.LinearMipmapNearestFilter;
    }
  }
  texture.updateMatrix();
  texture.needsUpdate = true;
}

function unityWrapMode(value: number) {
  switch (value) {
    case 1:
      return THREE.ClampToEdgeWrapping;
    case 2:
      return THREE.MirroredRepeatWrapping;
    case 3:
      // WebGL has no MirrorOnce sampler mode. Clamp is the bounded half of
      // Unity's MirrorOnce behavior and avoids repeating outside the first tile.
      return THREE.ClampToEdgeWrapping;
    default:
      return THREE.RepeatWrapping;
  }
}
