import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { brotliDecompressSync } from "node:zlib";

import { decodeRuntimeMessagePack } from "../../engine/runtime-binary-codec.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const engineBundlePath = path.join(here, "out", "engine-runtime-package-loader.mjs");

// The engine side under test is the PRODUCTION runtimePackageLoader.ts,
// bundled in place by contract/parity/run.sh via the engine's own bundler.
let engine;
try {
  engine = await import(engineBundlePath);
} catch (cause) {
  throw new Error(`missing or broken ${engineBundlePath}; run contract/parity/run.sh first`, { cause });
}
const {
  expectedRuntimeRoleIdentity,
  isCacheableRuntimeMetadataUrl,
  runtimePathUnitSegment,
  validateScopedRoleCatalog,
} = engine;

const vector = JSON.parse(readFileSync(path.join(here, "unit-segment-vector.json"), "utf8"));
const parity = readParityReport();
const packageBaseUrl = "https://packages.example/haruki-3d/";

function readParityReport() {
  try {
    return JSON.parse(readFileSync(path.join(here, "out", "exporter-parity.json"), "utf8"));
  } catch (cause) {
    throw new Error("missing out/exporter-parity.json; run contract/parity/run.sh first", { cause });
  }
}

function decodePackageFile(relativePath) {
  const compressed = readFileSync(path.join(here, "out", "package", ...relativePath.split("/")));
  return decodeRuntimeMessagePack(brotliDecompressSync(compressed));
}

function decodedScopedCatalogsByRoleId() {
  const byRoleId = new Map();
  for (const relativePath of parity.scopedCatalogPaths) {
    const catalog = decodePackageFile(relativePath);
    assert.ok(Array.isArray(catalog.roles), `${relativePath} must decode to a catalog with roles`);
    assert.equal(catalog.roles.length, 1, `${relativePath} must contain exactly one scoped role`);
    const roleId = catalog.roles[0].roleId;
    assert.ok(!byRoleId.has(roleId), `duplicate scoped catalog for roleId ${roleId}`);
    byRoleId.set(roleId, { relativePath, catalog });
  }
  return byRoleId;
}

test("unit-segment formula matches the exporter for the shared vector", () => {
  assert.equal(
    parity.unitSegment.length,
    vector.inputs.length,
    "exporter harness must consume the full shared vector",
  );
  vector.inputs.forEach((input, index) => {
    const exporterCase = parity.unitSegment[index];
    assert.equal(exporterCase.input, input, `vector[${index}] input drifted between the two sides`);
    assert.equal(
      runtimePathUnitSegment(input),
      exporterCase.output,
      `unit segment for ${JSON.stringify(input)}`,
    );
  });
  const nullCase = parity.unitSegment[vector.inputs.indexOf(null)];
  assert.equal(
    runtimePathUnitSegment(undefined),
    nullCase.output,
    "engine must treat an absent unit like the exporter treats null",
  );
});

test("31-role identity table matches the exporter field-by-field", () => {
  assert.equal(parity.roleIdentity.length, 31, "exporter table must cover exactly 31 roles");
  parity.roleIdentity.forEach((exporterRole, index) => {
    assert.equal(exporterRole.roleId, index + 1, "exporter table must cover role IDs 1..31 in order");
    const engineRole = expectedRuntimeRoleIdentity(exporterRole.roleId);
    assert.ok(engineRole, `engine must know role ${exporterRole.roleId}`);
    assert.equal(engineRole.characterId, exporterRole.characterId, `characterId for role ${exporterRole.roleId}`);
    assert.equal(engineRole.unit, exporterRole.unit, `unit for role ${exporterRole.roleId}`);
  });
  for (const outOfRange of [0, 32, 1.5]) {
    assert.equal(expectedRuntimeRoleIdentity(outOfRange), null, `role ${outOfRange} must stay unknown`);
  }
});

test("exporter-emitted scoped catalogs pass the engine's production validation", () => {
  const byRoleId = decodedScopedCatalogsByRoleId();
  assert.equal(byRoleId.size, 31, "the production exporter must emit one scoped catalog per role");
  for (let roleId = 1; roleId <= 31; roleId += 1) {
    const scoped = byRoleId.get(roleId);
    assert.ok(scoped, `missing scoped catalog for roleId ${roleId}`);
    const identity = expectedRuntimeRoleIdentity(roleId);
    // validateScopedRoleCatalog recomputes roleRuntimePath from the engine's
    // own role table + unit-segment formula and throws on any field drift.
    const roles = validateScopedRoleCatalog(scoped.catalog, identity.characterId, identity.unit);
    assert.equal(roles.length, 1, `scoped catalog for roleId ${roleId} must validate to one role`);
    assert.equal(roles[0].roleId, roleId, `scoped catalog at ${scoped.relativePath} role id`);
    assert.equal(
      scoped.catalog.masterVersion,
      parity.catalog.masterVersion,
      `scoped catalog at ${scoped.relativePath} master version`,
    );
  }
});

test("exporter-emitted paths match the engine's runtime metadata URL patterns", () => {
  for (const relativePath of parity.scopedCatalogPaths) {
    assert.ok(
      isCacheableRuntimeMetadataUrl(`${packageBaseUrl}${relativePath}`),
      `engine must recognize the exporter's by-role catalog path: ${relativePath}`,
    );
  }
  const byRoleId = decodedScopedCatalogsByRoleId();
  for (const { relativePath, catalog } of byRoleId.values()) {
    const roleRuntimePath = catalog.roles[0].roleRuntimePath;
    assert.ok(
      isCacheableRuntimeMetadataUrl(`${packageBaseUrl}${roleRuntimePath}`),
      `engine must recognize the exporter's role-runtime path from ${relativePath}: ${roleRuntimePath}`,
    );
  }
  assert.ok(
    !isCacheableRuntimeMetadataUrl(`${packageBaseUrl}parts/by-role/1/light_sound/runtime-role-catalog.json`),
    "the URL pattern check must reject non-contract paths, or it proves nothing",
  );
});

test("root catalog agrees with the scoped catalogs", () => {
  const root = decodePackageFile(parity.rootCatalogPath);
  assert.equal(root.version, parity.catalog.version, "root catalog version");
  assert.equal(root.masterVersion, parity.catalog.masterVersion, "root catalog master version");
  assert.ok(Array.isArray(root.roles), "root catalog roles");
  assert.equal(root.roles.length, 31, "root catalog must list all 31 roles");
});
