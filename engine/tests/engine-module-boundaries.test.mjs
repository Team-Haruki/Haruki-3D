import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const readSource = (relativePath) => fs.readFileSync(path.join(repoRoot, relativePath), "utf8");

test("camera policy is isolated from the engine orchestrator", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const cameraSource = readSource("src/costume_shop/cameraPolicy.ts");

  assert.match(engineSource, /from "\.\.\/costume_shop\/cameraPolicy"/);
  assert.doesNotMatch(engineSource, /function calculateCostumeShopCameraPose/);
  assert.match(cameraSource, /export function getCostumeShopCameraPose/);
  assert.match(cameraSource, /export function getDefaultCameraPose/);
});

test("base, CostumeShop, and MV have one-way module boundaries", () => {
  const baseSource = [
    readSource("src/base/index.ts"),
    readSource("src/base/browserCharacterRuntime.ts"),
  ].join("\n");
  const costumeShopSource = readSource("src/costume_shop/CostumeShopKernel.ts");
  const mvSource = [
    readSource("src/mv/index.ts"),
    readSource("src/mv/HarukiMvRuntime.ts"),
    readSource("src/mv/unityWebGLBuild.ts"),
  ].join("\n");

  assert.doesNotMatch(baseSource, /costume_shop|\/mv\//i);
  assert.match(costumeShopSource, /\.\.\/base\/browserCharacterRuntime/);
  assert.doesNotMatch(mvSource, /costume_shop|from ["']three["']/i);
  assert.match(mvSource, /createUnityInstance/);
});

test("capture background generation is isolated from rendering", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const backgroundSource = readSource("src/engine/captureBackground.ts");

  assert.doesNotMatch(engineSource, /function drawCaptureTriangleBackground/);
  assert.match(backgroundSource, /export function createCaptureBackgroundTexture/);
});

test("projected shadow state is owned by one module", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const shadowSource = readSource("src/engine/projectedShadow.ts");

  assert.doesNotMatch(engineSource, /class CharacterProjectedShadowController/);
  assert.match(shadowSource, /export class CharacterProjectedShadowController/);
  assert.match(shadowSource, /export const defaultProjectedShadowSettings/);
});

test("motion decoding and retargeting are isolated from playback state", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const motionSource = readSource("src/engine/runtimeMotion.ts");

  assert.doesNotMatch(engineSource, /function readUnityMotionRuntime0414/);
  assert.doesNotMatch(engineSource, /function retargetUnityPrefabAnimationClip/);
  assert.match(motionSource, /export function decodeUnityMotionClips/);
  assert.match(motionSource, /export function retargetUnityPrefabAnimationClip/);
});

test("animation playback state is isolated from the engine orchestrator", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const playbackSource = readSource("src/engine/animationPlaybackRuntime.ts");

  assert.match(playbackSource, /export class AnimationPlaybackRuntime/);
  assert.match(engineSource, /private readonly animationPlayback/);
  assert.doesNotMatch(engineSource, /private currentAnimationMixer/);
  assert.doesNotMatch(engineSource, /private currentAnimationAction/);
  assert.doesNotMatch(engineSource, /private async refreshAnimationPlayback/);
});

test("face motion state and morph binding are isolated from the engine orchestrator", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const faceMotionSource = readSource("src/engine/faceMotionRuntime.ts");

  assert.match(faceMotionSource, /export class FaceMotionRuntime/);
  assert.match(engineSource, /private readonly faceMotion/);
  assert.doesNotMatch(engineSource, /private currentFaceMotionClip/);
  assert.doesNotMatch(engineSource, /private sampleFaceCurve/);
  assert.doesNotMatch(engineSource, /private bindHeadMorphTargets/);
});

test("prefab graph assembly and native mesh import are isolated from engine state", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const prefabSource = readSource("src/engine/unityPrefabRuntime.ts");

  assert.doesNotMatch(engineSource, /function applyOfficialModelCombineSetup/);
  assert.doesNotMatch(engineSource, /function buildUnityRuntimeNativeGeometry/);
  assert.match(prefabSource, /export function buildUnityPrefabSourceGraph/);
  assert.match(prefabSource, /export function installUnityRuntimeNativeMeshes/);
  assert.match(prefabSource, /export function createUnityPrefabConstraintRuntime/);
  assert.doesNotMatch(engineSource, /function readRuntimeUnitySetup0414/);
  assert.match(engineSource, /private async loadCombinedCharacterAsset/);
});

test("body and head material binding are isolated from engine state", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const materialSource = readSource("src/engine/characterMaterialRuntime.ts");
  const headMaterialSource = readSource("src/engine/headMaterialRuntime.ts");

  assert.doesNotMatch(engineSource, /Body mesh '\$\{mesh\.name\}' material key/);
  assert.match(materialSource, /export async function bindBodyRuntimeMaterials/);
  assert.match(materialSource, /original\.userData\.pjskMaterialKey/);
  assert.match(materialSource, /THREE\.NoColorSpace/);
  assert.match(engineSource, /this\.runtimeDebug\.body = \[\]/);
  assert.match(engineSource, /await bindBodyRuntimeMaterials/);
  assert.doesNotMatch(engineSource, /Head mesh '\$\{mesh\.name\}' material key/);
  assert.match(headMaterialSource, /export async function bindHeadRuntimeMaterials/);
  assert.match(engineSource, /await bindHeadRuntimeMaterials\(/);
});

test("character lighting and material view state are isolated from the engine orchestrator", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const lightingSource = readSource("src/engine/characterLightingRuntime.ts");

  assert.match(lightingSource, /export class CharacterLightingRuntime/);
  assert.match(engineSource, /private readonly characterLighting/);
  assert.doesNotMatch(engineSource, /private faceSdfEnabled/);
  assert.doesNotMatch(engineSource, /private hairShadowMode/);
  assert.doesNotMatch(engineSource, /private applyRenderIsolationMode/);
  assert.doesNotMatch(engineSource, /private updateLoadedMaterialLight/);
});

test("through-hair pass policy and submesh cloning are isolated from engine state", () => {
  const engineSource = readSource("src/engine/Haruki3DEngine.ts");
  const materialSource = readSource("src/engine/characterMaterialRuntime.ts");
  const headMaterialSource = readSource("src/engine/headMaterialRuntime.ts");

  assert.doesNotMatch(engineSource, /function configureFaceLayerOverlayStencil/);
  assert.doesNotMatch(engineSource, /function createGroupedOverlayMesh/);
  assert.doesNotMatch(engineSource, /function getHeadLayerRenderOrder/);
  assert.match(materialSource, /export function configureSekaiEyelashPass/);
  assert.match(materialSource, /material\.depthFunc = THREE\.AlwaysDepth/);
  assert.match(materialSource, /export function createSekaiThroughHairOverlayMesh/);
  assert.match(headMaterialSource, /const CHARACTER_STENCIL_BIT = 0x01/);
});

// ---------------------------------------------------------------------------
// Transitive import-graph enforcement.
//
// CONTEXT.md invariants: "Base never depends on Costume Shop or MV" and
// "Costume Shop and MV may depend on Base, but never on each other". The
// regex tests above check hand-picked files; the walker below resolves every
// import/export edge (static, re-export, dynamic, and `import type`) across
// engine/src/ and asserts the invariants over the full transitive closure, so
// a new edge in any shared file fails without this test being updated.
//
// POLICY on `import type`: CONTEXT.md motivates the boundary in runtime terms
// ("Runtime modules may share Base capabilities, but they do not inherit each
// other's camera, lighting, scene, or interaction rules") but states the
// invariant as an unqualified dependency rule ("never depends"). We enforce
// the strict reading: type-only edges are forbidden across the boundaries too,
// because today's tree has no cross-boundary type-only edge, so the strict
// reading locks the status quo without an allowlist. The `three` rule for MV
// is the exception: it is asserted on value imports only, because
// `import type` from "three" is erased at build time and would not pull
// three.js into the Unity-based MV bundle — that invariant is about runtime
// payload, not declarations.
// ---------------------------------------------------------------------------

const srcRoot = path.join(repoRoot, "src");

const stripComments = (source) =>
  source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^[ \t]*\/\/.*$/gm, "");

const importClauseIsTypeOnly = (typeKeyword, clause) => {
  if (typeKeyword) {
    return true;
  }
  const named = clause.trim().match(/^\{([\s\S]*)\}$/);
  if (!named) {
    return false;
  }
  const entries = named[1]
    .split(",")
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0);
  return entries.length > 0 && entries.every((entry) => /^type\s/.test(entry));
};

const parseModuleEdges = (source) => {
  const code = stripComments(source);
  const edges = [];
  const staticStatement = /(?:^|[;\n])\s*(?:import|export)\s+(type\s+)?([^;'"]*?)from\s*["']([^"']+)["']/g;
  for (const match of code.matchAll(staticStatement)) {
    edges.push({ specifier: match[3], typeOnly: importClauseIsTypeOnly(match[1], match[2]) });
  }
  const bareImport = /(?:^|[;\n])\s*import\s*["']([^"']+)["']/g;
  for (const match of code.matchAll(bareImport)) {
    edges.push({ specifier: match[1], typeOnly: false });
  }
  const dynamicImport = /\bimport\s*\(\s*["']([^"']+)["']\s*\)/g;
  for (const match of code.matchAll(dynamicImport)) {
    edges.push({ specifier: match[1], typeOnly: false });
  }
  return edges;
};

const externalPackageName = (specifier) => {
  const clean = specifier.split("?")[0];
  const parts = clean.split("/");
  return clean.startsWith("@") ? parts.slice(0, 2).join("/") : parts[0];
};

const resolveRelativeSpecifier = (fromFile, specifier) => {
  const clean = specifier.split("?")[0];
  const resolved = path.resolve(path.dirname(fromFile), clean);
  const marker = `${path.sep}node_modules${path.sep}`;
  if (resolved.includes(marker)) {
    const packagePath = resolved.split(marker).pop().split(path.sep).join("/");
    return { kind: "external", name: externalPackageName(packagePath) };
  }
  const candidates = [resolved, `${resolved}.ts`, `${resolved}.tsx`, path.join(resolved, "index.ts")];
  for (const candidate of candidates) {
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) {
      return { kind: "module", file: candidate };
    }
  }
  throw new Error(`Cannot resolve import "${specifier}" from ${path.relative(repoRoot, fromFile)}`);
};

const readModuleRecord = (file) => {
  const record = { imports: [], externals: [] };
  for (const edge of parseModuleEdges(fs.readFileSync(file, "utf8"))) {
    if (!edge.specifier.startsWith(".")) {
      record.externals.push({ name: externalPackageName(edge.specifier), typeOnly: edge.typeOnly });
      continue;
    }
    const resolved = resolveRelativeSpecifier(file, edge.specifier);
    if (resolved.kind === "external") {
      record.externals.push({ name: resolved.name, typeOnly: edge.typeOnly });
    } else {
      record.imports.push({ file: resolved.file, typeOnly: edge.typeOnly });
    }
  }
  return record;
};

const collectClosure = (entryFiles, { followTypeOnly }) => {
  const modules = new Map();
  const pending = [...entryFiles];
  while (pending.length > 0) {
    const file = pending.pop();
    if (modules.has(file)) {
      continue;
    }
    const record = readModuleRecord(file);
    modules.set(file, record);
    for (const edge of record.imports) {
      if (followTypeOnly || !edge.typeOnly) {
        pending.push(edge.file);
      }
    }
  }
  return modules;
};

const srcRelative = (file) => path.relative(srcRoot, file).split(path.sep).join("/");

const moduleGroup = (file) => {
  const relative = srcRelative(file);
  if (relative.startsWith("..")) {
    return "outside-src";
  }
  if (relative.startsWith("costume_shop/")) {
    return "costume_shop";
  }
  if (relative.startsWith("mv/")) {
    return "mv";
  }
  return "shared";
};

const listTsFiles = (dir) =>
  fs
    .readdirSync(dir, { recursive: true })
    .map((entry) => path.join(dir, entry))
    .filter((entry) => entry.endsWith(".ts") && fs.statSync(entry).isFile())
    .sort();

const closureMembersInGroup = (modules, group) =>
  [...modules.keys()].filter((file) => moduleGroup(file) === group).map(srcRelative).sort();

test("import graph: base transitive closure never reaches costume_shop or mv", () => {
  const modules = collectClosure([path.join(srcRoot, "base/index.ts")], { followTypeOnly: true });
  const reached = [...modules.keys()].map(srcRelative).sort();

  // Sanity guard: an empty or truncated walk must not pass vacuously.
  assert.ok(reached.includes("base/browserCharacterRuntime.ts"));
  assert.ok(reached.includes("runtime/runtimePackageLoader.ts"));
  assert.ok(reached.includes("engine/unityPrefabRuntime.ts"));

  assert.deepEqual(closureMembersInGroup(modules, "costume_shop"), []);
  assert.deepEqual(closureMembersInGroup(modules, "mv"), []);
});

test("import graph: costume_shop and mv never reach each other", () => {
  const costumeShop = collectClosure(listTsFiles(path.join(srcRoot, "costume_shop")), {
    followTypeOnly: true,
  });
  const costumeShopReached = [...costumeShop.keys()].map(srcRelative);
  // Costume Shop legitimately reaches the shared engine orchestrator (which
  // imports its camera/height policies back — see the debt-lock test below).
  assert.ok(costumeShopReached.includes("engine/Haruki3DEngine.ts"));
  assert.ok(costumeShopReached.includes("costume_shop/cameraPolicy.ts"));
  assert.deepEqual(closureMembersInGroup(costumeShop, "mv"), []);

  const mv = collectClosure(listTsFiles(path.join(srcRoot, "mv")), { followTypeOnly: true });
  assert.ok([...mv.keys()].map(srcRelative).includes("mv/HarukiMvRuntime.ts"));
  assert.deepEqual(closureMembersInGroup(mv, "costume_shop"), []);
});

test("import graph: mv runtime never value-imports three", () => {
  const mv = collectClosure(listTsFiles(path.join(srcRoot, "mv")), { followTypeOnly: true });
  const offenders = [...mv.entries()]
    .filter(([, record]) =>
      record.externals.some((external) => external.name === "three" && !external.typeOnly)
    )
    .map(([file]) => srcRelative(file))
    .sort();
  assert.deepEqual(offenders, []);
});

test("import graph: costume_shop back-edges outside the module are locked to known files", () => {
  // Every import of costume_shop from outside src/costume_shop/, locked as an
  // exact set so any NEW edge fails this test. Today's edges, by kind:
  // - index.ts, internal.ts, kernel/Haruki3DKernel.ts: sanctioned by
  //   CONTEXT.md ("The default package entry remains the Costume Shop kernel
  //   for compatibility").
  // - engine/Haruki3DEngine.ts, engine/cameraRuntime.ts: DEBT — shared engine
  //   code value-imports Costume Shop camera/height policy. The invariant
  //   still holds because neither file is reachable from src/base/index.ts
  //   (the closure test above proves it), but any base-reachable module that
  //   starts importing them would flip "Base never depends on Costume Shop".
  const importers = listTsFiles(srcRoot)
    .filter((file) => moduleGroup(file) !== "costume_shop")
    .filter((file) => readModuleRecord(file).imports.some((edge) => moduleGroup(edge.file) === "costume_shop"))
    .map(srcRelative)
    .sort();
  assert.deepEqual(importers, [
    "engine/Haruki3DEngine.ts",
    "engine/cameraRuntime.ts",
    "index.ts",
    "internal.ts",
    "kernel/Haruki3DKernel.ts",
  ]);

  // No file outside src/mv/ imports mv at all today; lock that exact state.
  const mvImporters = listTsFiles(srcRoot)
    .filter((file) => moduleGroup(file) !== "mv")
    .filter((file) => readModuleRecord(file).imports.some((edge) => moduleGroup(edge.file) === "mv"))
    .map(srcRelative)
    .sort();
  assert.deepEqual(mvImporters, []);
});
