import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function source(relativePath) {
  return readFileSync(path.join(repoRoot, relativePath), "utf8");
}

test("costume resolver keeps default face and head optional bundle fallbacks", () => {
  const resolver = source("Services/Character3dCostumeResolver.cs");
  const registry = source("Services/CostumeRegistryExporter.cs");

  for (const text of [resolver, registry]) {
    assert.match(text, /ResolveDefaultFaceBundleFallback/);
    assert.match(text, /leaf\.Any\(static character => character != '0'\)/);
    assert.match(text, /fallbackLeaf = new string\('0', Math\.Max\(leaf\.Length - 1, 0\)\) \+ "1"/);
    assert.match(text, /ResolveFaceModelTypeBundleName/);
    assert.match(text, /FaceModelType/);
    assert.match(text, /ResolveAssetBaseDirectoryCandidates\(assetRoot, "head_optional"\)/);
    assert.match(text, /ResolveColorVariationBaseDirectoryCandidates\(assetRoot, "head_optional"\)/);
  }
});

test("part exporter preserves official runtime resource names and FaceSDF metadata", () => {
  const exporter = source("Services/PartPackageExporter.cs");
  const officialExtractor = source("Services/OfficialUnityResourceExtractor.cs");

  assert.match(exporter, /var normalizedType = ResolveRuntimePartType\(entry\)/);
  assert.match(exporter, /"body" => "body"/);
  assert.match(exporter, /"head" or "hair" => "face"/);
  assert.match(exporter, /"head_optional" => "optional"/);
  assert.match(exporter, /resourceExtractor\.Extract/);
  assert.match(officialExtractor, /bundle\.m_Container/);
  assert.match(officialExtractor, /BuildExpectedContainerSuffix/);
  assert.match(officialExtractor, /ContainerPath\.EndsWith\(expectedContainerSuffix/);
  assert.match(officialExtractor, /Where\(candidate => candidate\.IsExact && !candidate\.IsGeneratedInput\)/);
  assert.match(officialExtractor, /must resolve to exactly one container ending in/);
  assert.match(officialExtractor, /\/fbx\//);
  assert.match(exporter, /modelFactory\.CreateImportedModel\(input, officialResource\.RootGameObject\)/);
  assert.match(exporter, /ValidateExactSkinBindings\(partType, nativeMeshes\)/);
  assert.match(exporter, /Refusing to publish an incomplete part package/);
  assert.match(exporter, /Refusing to publish an ambiguous part package/);
  assert.match(exporter, /Refusing to publish a partial part package/);
  assert.match(exporter, /"_FaceShadowTex"/);
  assert.match(exporter, /FaceShadowTex: RewriteTexturePath\(faceShadowTex, textures\)/);
  assert.match(exporter, /"accessory" => "head_optional"/);
  assert.match(exporter, /"head_optional" => "head_optional"/);
});

test("body resolution never falls back to a different figure or breast-size bundle", () => {
  const resolver = source("Services/Character3dCostumeResolver.cs");
  const inputResolver = source("Services/BundleInputResolver.cs");

  assert.match(resolver, /ResolveBodyBundleFileName\(character\)/);
  assert.doesNotMatch(
    resolver,
    /GetFiles\(directory,\s*"\*\.bundle"[\s\S]*?FirstOrDefault\(\)/
  );
  assert.match(
    inputResolver,
    /Pass the exact body bundle selected from gameCharacters\.figure and breastSize/
  );
  assert.doesNotMatch(inputResolver, /"ladies_m\.bundle"\s*=>\s*0/);
});

test("head optional export never guesses a mount when masterdata part is absent", () => {
  const exporter = source("Services/UnityRuntimeNativeMeshExporter.cs");

  assert.match(exporter, /string\.IsNullOrWhiteSpace\(attachNodeName\)/);
  assert.doesNotMatch(
    exporter,
    /foreach\s*\(var fallback in new\[\][\s\S]*?"Head"[\s\S]*?"Neck"[\s\S]*?"face"/
  );
  assert.doesNotMatch(
    exporter,
    /ResolveAccessoryAttachPath[\s\S]*?return transformPaths\.FirstOrDefault\(\);/
  );
});

test("part packages retain bundle and Unity resource identity", () => {
  const models = source("Models/PartRuntimeModels.cs");
  const exporter = source("Services/PartPackageExporter.cs");

  for (const field of [
    "logicalBundleName",
    "physicalBundleSha256",
    "dependencyBundleNames",
    "colorVariationLogicalBundleName",
    "colorVariationPhysicalBundleSha256",
    "colorVariationDependencyBundleNames",
    "unityResourceName",
    "unityObjectType",
  ]) {
    assert.match(models, new RegExp(field));
  }
  assert.match(exporter, /BundleDependencyIndex\.LogicalName/);
  assert.match(exporter, /UnityObjectType: "GameObject"/);
});
