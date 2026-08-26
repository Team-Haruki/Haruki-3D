# Haruki 3D Runtime Package Contract

Authoritative specification of the package format produced by `exporter/` (Haruki-3D-Exporter)
and consumed by `engine/` (haruki-3d-engine). Every normative statement below is derived from the
cited source files. Where the two READMEs and the code disagree, this document follows the code and
flags the disagreement.

Terminology: **package root** is the exporter `--out` directory for one region; the engine mounts it
as `assetBaseUrl` (engine `kernel.load` / capture `HARUKI_RUNTIME_ROOT`, see `engine/README.md`).
All paths below are package-root-relative. `<unit>` path segments substitute `"default"` when the
unit is null (`exporter/Services/CostumeRegistryExporter.cs` `RuntimePathUnitSegment`,
`engine/src/runtime/runtimePackageLoader.ts` `runtimePathUnitSegment`).

RFC-2119 keywords (MUST, MUST NOT, MAY) are used with their usual meaning.

---

## 1. Package directory layout

| Path pattern | Producer | Consumer |
|---|---|---|
| `runtime-role-catalog.msgpack.br` | `exporter/Services/RuntimeRoleCatalogExporter.cs` (`Write`) | Haruki Cloud / operations. The engine never reads the root catalog. |
| `parts/by-role/<characterId>/<unit>/runtime-role-catalog.msgpack.br` | `RuntimeRoleCatalogExporter.Write` (one single-role catalog per role) | Engine: first fetch of every load (`engine/src/runtime/runtimePackageLoader.ts` `loadPartPackageSetFromBaseUrl`). |
| `parts/by-role/<characterId>/<unit>/part-registry.msgpack.br` | `exporter/Services/CostumeRegistryExporter.cs` (`WriteScopedPartRegistryIndexes`) | Engine (`runtimePackageLoader.ts:112-118`). |
| `parts/part-registry.msgpack.br` | `CostumeRegistryExporter.Export` | Audits / Haruki Cloud. Not read by the engine. |
| `parts/part-registry-compact.msgpack.br` | `CostumeRegistryExporter.WriteCompactPartRegistry` | Haruki Cloud (field-name-free rows; `exporter/README.md`). |
| `parts/part-source-map.msgpack.br` | `CostumeRegistryExporter.Export` (line 37) | Audits only; no runtime consumer. Not listed in `exporter/README.md` (see Issues). |
| `parts/head-hair-compatibility.msgpack.br` | `CostumeRegistryExporter.Export` | Audits; full rule set (`not_available` + `default_hint`). |
| `parts/head-hair-compatibility-compact.msgpack.br` | `CostumeRegistryExporter.WriteCompactHeadHairCompatibility` | Haruki Cloud. |
| `parts/compat/by-unit/<unit>/head-hair-compatibility.msgpack.br` | `CostumeRegistryExporter.WriteScopedHeadHairCompatibilityIndexes` — rules filtered to `state == "not_available"` only | Engine, lazily on first custom selection (`runtimePackageLoader.ts` `ensureCompatibilityForSelection`). |
| `parts/card-costume-unlocks.msgpack.br` | `CostumeRegistryExporter.Export` | Haruki Cloud (card unlock/source metadata). |
| `parts/<partType>/<costume3dId>/<unit>/part-runtime.msgpack.br` | `exporter/Services/PartPackageExporter.cs` (delta package; path formula `BuildPackagePath` in `CostumeRegistryExporter.cs:807-810`) | Engine (`runtimePackageLoader.ts` `fetchPartRuntime`). |
| `parts/_cores/<partType>/<hash>/part-runtime-core.msgpack.br` | `PartPackageExporter.cs:505-527`; `<hash>` = lowercase SHA-256 hex of the part's shard key (`BaseSourceKey ?? SourceKey ?? PackagePath`, `ShardKey`/`BuildCoreKey`, lines 246-256) | Engine, fetched via the delta's `corePath` (`runtimePackageLoader.ts:316-322`). |
| `parts/<partType>/<costume3dId>/<unit>/part-export-error.json` | `PartPackageExporter.cs:148-163` on a failed export; deleted on success | Operations only. |
| `roles/<characterId>/<unit>/role-runtime.msgpack.br` | `exporter/Services/RoleRuntimeExporter.cs` (`BuildRoleRuntimeDirectory`, line 252-255) | Engine (`runtimePackageLoader.ts` `loadRoleRuntimePackages` / `ensureRoleRuntimePackage`). |
| `roles/<characterId>/<unit>/motion/unity-motion.msgpack.br` | `exporter/Services/MotionPackageExporter.cs` via `RoleRuntimeExporter.cs:156,183-187` | Engine, resolved from the role runtime's `motionPackage.unityMotionJson`, which is a path relative to the role-runtime file's directory (`RoleRuntimeExporter.cs` `RewriteMotionPackageForRoleDirectory`; `runtimePackageLoader.ts` `normalizeRoleRuntimePackage`/`resolveSiblingRuntimePath`). |
| `_texture_store/sha256/<hh>/<sha256>.png` / `.ktx2` | `exporter/Services/RuntimeTextureStore.cs` (PNG), `exporter/Services/TextureCompactor.cs` (KTX2 / optimized PNG); `<hh>` = first two hex chars of the hash | Engine, via texture references inside part packages (section 3). |
| `content-addressed-store-state.json`, `content-addressed-store-report.json` | `exporter/Services/ContentAddressedStore.cs` | Exporter-internal incremental state / operations report. |
| `texture-store-optimization-report.json`, `ktx2-transcode-report.json` | `TextureCompactor.cs` | Operations. |

`<partType>` ∈ `body | hair | head | head_optional`; masterdata `accessory` is normalized to
`head_optional` (`CostumeRegistryExporter.cs` `NormalizePackagePartType`).

Outside the package root, `--shared-content-store <dir>` maintains a cross-region CAS at
`<dir>/textures/sha256/<hh>/<hash><ext>` and `<dir>/part-runtime/sha256/<hh>/<hash>.msgpack.br`;
region paths are hard-linked to those immutable, read-only canonical objects, so output and store
MUST be on the same filesystem (`ContentAddressedStore.cs`). The KTX2 encode cache lives at
`<dir>/ktx2/<encoderVersion>/<transfer>/<hh>/<sourcePngSha256>.ktx2`, keyed by the *source PNG*
hash and `TextureCompactor.Ktx2EncoderVersion` (`"uastc-q2-zstd5-mip-v1"`).

### URL resolution rules (consumer)

- Every runtime-metadata URL MUST end in `.msgpack.br`; the engine rejects anything else
  (`runtimePackageLoader.ts:463-465`). There is no JSON or gzip transport.
- Relative package paths are resolved against `assetBaseUrl`; `.`/`..` segments are rejected and
  each segment is URL-encoded (`resolveRuntimePackageUrl`).
- Paths inside package payloads that begin with `/` (e.g. `/_texture_store/...`) are
  **package-root-absolute**: the engine strips the leading slash and resolves against
  `assetBaseUrl` (`engine/src/parts/runtimePartComposer.ts` `resolvePackageRelativePath` +
  `resolveRuntimePackageUrl`). Payload paths without a leading slash or URL scheme are resolved
  relative to the owning package's `packagePath`.
- Servers MAY send an `x-haruki-file-version` response header; when present the engine reuses its
  parsed copy of by-role registry/catalog, per-unit compatibility, role-runtime, and
  `motion/unity-motion` files while the header value is unchanged
  (`runtimePackageLoader.ts:488-521`). The value MUST change whenever the file bytes change.

---

## 2. Encoding

### 2.1 Container

Every `*.msgpack.br` file is exactly one MessagePack document compressed with Brotli
(`exporter/Services/RuntimeJsonWriter.cs` `WriteMessagePackBrotli`):

- Brotli quality **6** (`RuntimeJsonWriter.DefaultBrotliQuality`), window 22. The two compact
  registries are written at quality 1 (`CompressionLevel.Fastest` →
  `RuntimeJsonWriter.BrotliQuality`; `CostumeRegistryExporter.cs:74,97`). Consumers MUST NOT depend
  on quality; any valid Brotli stream is acceptable.
- Files are written atomically (temp file + rename, `WriteAllBytesAtomic`).
- Map keys are the camelCase JSON property names declared with `JsonPropertyName` on the model
  records (`exporter/Models/*.cs`). Null suppression follows the models' `JsonIgnore` conditions.
- Scalar `float`/`double`/`decimal` values are emitted as MessagePack float64 (`0xcb`); integers
  use the shortest signed/unsigned form; `byte[]` is emitted as a base64 **string**; the msgpack
  `bin` family is never produced (`RuntimeJsonWriter.cs:209-213,410-450,660-738`).
- The engine decompresses with Brotli WASM and decodes with `@msgpack/msgpack` plus the extension
  codec below (`engine/src/runtime/runtimeMessagePackDecodeCore.ts`,
  `engine/src/runtime/runtimeMessagePackDecoder.ts`).

### 2.2 Extension type 42 (runtime binary array)

Declared as `RuntimeJsonWriter.BinaryArrayExtensionType = 42` (producer) and
`runtimeBinaryArrayExtensionType = 42` in `engine/runtime-binary-codec.mjs` /
`engine/runtime-binary-codec.d.mts` (consumer).

**Payload wire format** (after the standard MessagePack ext header):

```
offset 0        : element kind (uint8)
                    1 = float32, little-endian
                    2 = uint16,  little-endian
                    3 = uint32,  little-endian
offset 1 .. end : packed element data
```

- Element count is **implicit**: `(payloadLength - 1) / elementWidth`. The remaining byte length
  MUST be an exact multiple of the element width; the decoder throws otherwise
  (`runtime-binary-codec.mjs:18-39`). There is no explicit count field.
- The producer emits the ext header as `ext8`/`ext16`/`ext32` (`0xc7`/`0xc8`/`0xc9`) selected by
  payload length, followed by the type byte 42 (`RuntimeJsonWriter.cs` `WriteExtension`,
  lines 578-597). `fixext` forms are never produced (the minimum payload is 33 bytes). The
  consumer, via `@msgpack/msgpack`, accepts any well-formed ext header of type 42.
- The decoder materializes kind 1/2/3 as `Float32Array`/`Uint16Array`/`Uint32Array`; consumers of
  these properties MUST accept `number[] | Float32Array | Uint16Array | Uint32Array`
  (`RuntimeNumericArray`, `engine/runtime-binary-codec.d.mts`).

**When the producer emits ext 42.** Only when serializing with an explicit schema —
`RuntimeBinaryArraySchema.PartRuntime` (part core/delta packages and their rewrites:
`PartPackageExporter.cs:2068-2075`, `TextureCompactor.cs:589,978`, `CompiledPartCache.cs:164`) or
`RuntimeBinaryArraySchema.UnityMotion` (`MotionPackageExporter.cs:151,675`) — and only for
properties whose **path** matches the schema's allow-list (`RuntimeJsonWriter.cs:144-170`):

| Schema | Element kind | Property paths |
|---|---|---|
| PartRuntime | float32 (1) | `nativeMeshes.meshes.positions`, `.normals`, `.tangents`, `.colors`, `.uv0`, `.uv1`, `.uv2`, `.skinWeights`, `.boneInverseBindMatrices`, `nativeMeshes.meshes.morphTargets.positionDeltas`, `.normalDeltas` |
| PartRuntime | uint16/uint32 (2/3) | `nativeMeshes.meshes.skinIndices`, `nativeMeshes.meshes.submeshes.indices`, `nativeMeshes.meshes.morphTargets.indices` |
| UnityMotion | float32 (1) | `clips.tracks.times`, `clips.tracks.values` |

Path semantics: the path is the dot-joined chain of map property names from the document root;
**traversing an array does not append a segment** (`RuntimeJsonWriter.cs:266,318,341,467,480`), so
the rule applies to every element of an array of objects at the matching chain (e.g. each mesh in
`nativeMeshes.meshes[]`).

Emission thresholds (`RuntimeJsonWriter.cs` `TryWriteBinaryArray`, both overloads):

- float32 paths: emitted only when the array has **≥ 8** elements and every value is finite;
  otherwise it stays an ordinary MessagePack array of numbers.
- index paths: emitted only when the array has **≥ 16** elements and every value is
  uint32-representable. **uint16 (kind 2) is chosen iff every value ≤ 65535**, else uint32
  (kind 3). Elements are always non-negative integers.
- Consequently consumers MUST handle both representations on these paths, and MUST NOT assume any
  other property is ever ext-42 encoded: arrays with the same property name reached through a
  different chain, or in documents written without a schema (registries, catalogs, role runtimes),
  are ordinary MessagePack arrays (also stated in `exporter/README.md`).

**Encoder/decoder conformance.** The engine decoder is a strict superset of what the encoder can
produce (it additionally accepts empty and sub-threshold payloads of any kind). No wire-format
disagreement exists between `RuntimeJsonWriter.cs` and `runtime-binary-codec.mjs` as of this
writing. One producer-internal asymmetry is documented for maintainers: on index paths the
object-graph encoder throws if a value is not uint32-representable (`Convert.ToUInt32`,
`RuntimeJsonWriter.cs:385`), while the `JsonElement` encoder silently falls back to a plain array
(`TryGetUInt32` failure, line 552-555). Both outcomes are valid on the wire.

### 2.3 Document version markers

| Document | Version value | Producer | Consumer check |
|---|---|---|---|
| Runtime role catalog | `version: 4` (int) | `RuntimeRoleCatalogExporter.cs:8` | Engine accepts 2, 3, 4; fields `skinColors` (v3+) and `characterHeightMeters` (v4+) are then mandatory (`runtimePackageLoader.ts:365-408`). |
| Part registry | `version: 2` | `CostumeRegistryExporter.cs:273` | Not checked by the engine. |
| Head/hair compatibility, card unlocks, part source map | `version: 1` | `CostumeRegistryExporter.cs:561,650,735` | Not checked by the engine. |
| Compact registries | leading array element `1` (`CompactRegistrySchemaVersion`) | `CostumeRegistryExporter.cs:12,72,95` | Haruki Cloud. |
| Part delta | `version: "0415-part-delta-3"`, `corePath` required | `PartPackageExporter.cs:528-530` | Engine checks only that `corePath` ends in `.msgpack.br` (`runtimePackageLoader.ts:316-318`); the version string is validated by the exporter's compiled-package cache (`PartPackageExporter.cs:1377-1379`). |
| Part core | `version: "0415-part-core-3"` | `PartPackageExporter.cs:516` | Exporter cache only (`PartPackageExporter.cs:1443-1444`). |
| Native mesh set (inside core) | `version: "0414"` | `exporter/Services/UnityRuntimeNativeMeshExporter.cs:18,72` | Engine requires `"0414"`/`414` (`engine/src/engine/unityPrefabRuntime.ts:230-247`). |
| Role runtime | `version: "0414-role-1"` | `RoleRuntimeExporter.cs:205` | Not checked by the engine. |

---

## 3. Texture store

- **Content addressing.** Every stored object is named by the lowercase SHA-256 hex of its exact
  bytes, sharded by the first two hex chars:
  `_texture_store/sha256/<hh>/<hash>.<ext>` (`RuntimeTextureStore.cs`,
  `TextureCompactor.cs`). File content MUST always match the hash in its name ("CAS paths remain
  truthful", `exporter/README.md`).
- **References.** Packages reference textures by package-root-absolute path,
  `"/_texture_store/sha256/<hh>/<hash>.png"` (return value of `RuntimeTextureStore.StorePng`) or
  `".ktx2"`. Referenced properties are the material-slot texture fields `mainTex`, `shadowTex`,
  `valueTex`, `faceShadowTex`, the `textureRoles[].uri` entries, and the `characterTextures` map
  (`TextureCompactor.cs:362-378,605-641`; slot construction in
  `exporter/Services/PjskSekaiRuntimeExtensionBuilder.cs:18-81`).
- **PNG is the default finalized format.** Lossless optimization is a separate post-publish pass
  (`--optimize-texture-store`): oxipng runs on a temp copy, the result is kept only if smaller, the
  optimized bytes are stored under their **new** hash, every `part-runtime*.msgpack.br` reference
  is rewritten, all references are validated, and only then are the replaced objects deleted
  (`TextureCompactor.OptimizeStore`, lines 21-121; rewrite → `ValidateRuntimeTexturePaths` →
  `DeleteReplacedTextureFiles` ordering at lines 95-106). Exports never wait on the optimizer.
- **KTX2 option** (`--texture-format ktx2`): textures are finalized as UASTC KTX2 via
  `ktx create --encode uastc --uastc-quality 2 --zstd 5 --generate-mipmap
  --assign-texcoord-origin top-left` (`TextureCompactor.RunKtxCreate`, lines 522-567; tool override
  `HARUKI_KTX_TOOL`). Two transfer classes exist:
  - **sRGB** (`R8G8B8A8_SRGB`, `--assign-tf srgb`): color textures — `mainTex`, `shadowTex`,
    `characterTextures` values, and `textureRoles` entries whose `role` is `main` or `shadow`.
  - **Linear** (`R8G8B8A8_UNORM`, `--assign-tf linear`): data textures — `valueTex`,
    `faceShadowTex`, and `textureRoles` entries whose `role` is `value` or `faceShadow`
    (`TextureCompactor.cs:375-378,399-410,619-641`).
  - **Dual-variant rule.** When one PNG is referenced by both classes, one KTX2 variant per
    transfer class is emitted, each stored under its own content hash
    (`CollectKtx2Variants` accumulates a transfer set per source path;
    `Ktx2VariantKey(SourcePath, Transfer)`).
  - Rewrite guarantees mirror the PNG optimizer: references are rewritten and validated before the
    region-local source PNGs are deleted (`TranscodeStoreToKtx2`, lines 168-195). With
    `--shared-content-store`, the source PNG is preserved in the cross-region CAS first (for
    compiled-package restoration) and the encoded KTX2 is cached under the source-hash key
    (section 1).
- **Consumer.** The engine chooses its loader by URL suffix: `.ktx2` → three.js `KTX2Loader` with
  the Basis transcoder served at `/basis/`, anything else → image texture
  (`engine/src/engine/runtimeTextureLoader.ts`). Both formats MUST therefore remain addressable by
  plain URL suffix.
- Texture roles per material kind are constrained by
  `PjskSekaiRuntimeExtensionBuilder.cs:1385-1388`: `main` always; `shadow` for
  body/hair/accessory/face_sdf; `value` for body; `faceShadow` for face_sdf.

---

## 4. Role catalog and registry semantics

### 4.1 masterVersion

- `masterVersion` is the masterdata `dataVersion` read from `current_version.json` (in the master
  directory or the sibling `versions/` directory); export fails without it
  (`RuntimeRoleCatalogExporter.ResolveMasterVersion`, lines 233-256).
- **Skip-unchanged refresh:** the catalog writer compares version, masterVersion, and the full role
  list of the root catalog *and* every scoped copy, and rewrites nothing when all match
  (`CatalogIsCurrent`, lines 90-134). Registry regeneration reruns per region on every masterdata
  update; there is no release directory or release identifier (`exporter/README.md`).
- The engine appends `?masterVersion=<catalog.masterVersion>` to the by-role part registry,
  per-unit compatibility, role runtime, and unity-motion URLs as a cache-buster
  (`runtimePackageLoader.ts` `withRuntimeMasterVersion`; call sites at lines 112-117, 204-209,
  236-243, 280-286). Part runtime and core URLs are fetched without it.

### 4.2 Publish-ordering invariant

The catalog is the entry point that stamps `masterVersion` onto everything else, therefore:
**publish the catalog last**, after all changed part packages, cores, textures, and role runtimes
(`exporter/README.md`, "Publish the Catalog after any changed part and role runtime packages").
Sparse-input runs preserve manifest stamps for reusable packages and MUST fail rather than publish
an empty or incomplete manifest (`exporter/README.md`, updater sparse-inputs paragraph;
`.haruki-sparse-input` marker handling in `PartPackageExporter.cs:62-66` and
`exporter/Services/PartPackageWorkPlanner.cs:65-72` — zero-byte placeholder bundles are skipped,
existing role runtimes are kept, `RoleRuntimeExporter.cs:140-149`).

### 4.3 Role catalog

- Exactly the 31 public roles, `roleId` 1..31, in order; the (characterId, unit) identity per
  roleId is **hardcoded identically on both sides** (`RuntimeRoleCatalogExporter.ExpectedRole` /
  `UnitForCharacter`, lines 258-279; `runtimePackageLoader.ts` `expectedRuntimeRoleIdentity`,
  lines 422-443). Roles 21-26 are Miku's units in the fixed order
  `piapro, idol, light_sound, street, theme_park, school_refusal`.
- Each scoped catalog MUST contain exactly one role whose identity matches its directory, with
  positive `bodyCostume3dId`/`headCostume3dId`/`hairCostume3dId`, `skinColors` as `#rrggbb`
  strings, finite positive `characterHeightMeters`, and `roleRuntimePath` **byte-exactly**
  `roles/<characterId>/<unit>/role-runtime.msgpack.br` (producer formula
  `RuntimeRoleCatalogExporter.cs:186`; consumer equality check
  `runtimePackageLoader.ts:380-407`). Any deviation makes the engine reject the catalog.
- `characterHeightMeters` is the unmodified masterdata `gameCharacters.height`; the height policy
  (`heightRate = 0.5 + 0.8 / characterHeightMeters`) is applied by the engine, not baked into
  packages (`engine/README.md` Runtime Behavior).

### 4.4 Part registry

- Row schema: `exporter/Models/CostumeRegistryModels.cs` (`PartRegistryEntry`). The engine accepts
  the `{version, source, entries}` object or a bare array (`normalizePartRegistry`,
  `runtimePackageLoader.ts:531-533`).
- `status` values (producer, `CostumeRegistryExporter.cs:262-266,479`):
  - `"planned"` — masterdata yields a deterministic bundle path; the runtime package may or may not
    exist yet. Registry generation does not verify the bundle; single-part export does.
  - `"missing"` — required masterdata (or bundle path) is absent.
  - `"empty"` — a head_optional row representing the official *empty accessory slot*.
  Consumer semantics (`runtimePackageLoader.ts:656-662`): `missing` rows are unusable;
  `empty` rows are selectable but never loaded as packages; only remaining rows are load
  candidates. Fetch failures on `planned` rows are tolerated during default-selection probing
  (`fetchOptionalPartRuntime`). Note: `exporter/README.md` documents only planned/missing; `empty`
  is normative (see Issues).
- `packagePath` is the directory prefix `parts/<partType>/<costume3dId>/<unit>/` (with trailing
  slash from the producer; the engine appends `/part-runtime.msgpack.br` after its own
  normalization). Color variants of one asset share a `packagePath` via the source-identity
  aliasing recorded in `parts/part-source-map.msgpack.br`.
- There is no preset mode: role defaults and explicit selections use the same assembly and
  compatibility rules (`exporter/README.md`).

### 4.5 Head/hair compatibility

- The full registry (`parts/head-hair-compatibility.msgpack.br`) keeps both rule states:
  `"not_available"` (from `costume3dModelNotAvailablePatterns.json`) and `"default_hint"`
  (default hairs emitted as conflict-fallback hints)
  (`CostumeRegistryExporter.BuildHeadHairCompatibility`, lines 511-561).
- The runtime file (`parts/compat/by-unit/<unit>/head-hair-compatibility.msgpack.br`) is a
  **deny list**: only `not_available` rows survive (`CostumeRegistryExporter.cs:151-153`).
  Absence of a pair means **allowed**.
- The engine denies a (unit, head, hair) pair iff it appears in `denied[]` or in `rules[]` with
  `state === "not_available"`; key `${unit ?? ""}|${head}|${hair}`
  (`runtimePartComposer.ts` `getDeniedHeadHairCompatibilityKeys`/`headHairCompatibilityKey`).
  The check applies only when the selected head is **not** a complete `head` part — complete heads
  carry their own hair and bypass the deny list (`assertHeadHairCompatible`, lines 992-1009).

### 4.6 Part packages (core + delta)

- Part packages MUST be core+delta; self-contained part runtimes are not supported
  (`engine/README.md`, "part packages must use core+delta and declare `corePath`"; enforced by the
  `corePath` requirement in `runtimePackageLoader.ts:316-318`).
- Delta schema `PartRuntimeDeltaPackage`, core schema `PartRuntimeCorePackage`
  (`exporter/Models/PartRuntimeModels.cs`). Heavy shared data (native meshes, SpringBone,
  character controllers, morph bindings) lives in the core, shared by all color variants of one
  source; per-variant identity, mount, manifest, material slots, texture roles, and character
  textures live in the delta (`PartPackageExporter.cs:505-542`).
- **Merge rule** (consumer): shallow merge — every delta property overrides the same-named core
  property, `corePath` is dropped, and `warnings` is the concatenation core-then-delta
  (`engine/part-runtime-core.mjs`). Producers MUST NOT rely on deep merging.
- `corePath` is package-root-relative and MUST end in `.msgpack.br`.
- Incremental behavior: an export run skips a package when its manifest stamp and outputs are
  intact (`PartPackageExporter.cs:100-107`); compiled cores/deltas can be restored from the
  compiled content store when resolved input bundles are byte-identical, with the delta re-stamped
  for the current region (`CompiledPartCache`, `exporter/README.md`).

---

## 5. Viewer behavioral contract

Behavior the engine implements and packages MUST remain compatible with:

- **SpringBone record merging.** On every (re)composition the engine flattens the active parts'
  SpringBone records — managers, bones, extra bones, colliders, constraints — remapped per part,
  then rebuilds collider bindings, manager collider caches, and binding decisions from the
  composed hierarchy (`engine/src/parts/runtimePartComposer.ts` `mergeRuntimeSetup`,
  lines 1260-1334; ordered steps recorded at lines 1299-1307).
- **Collider rebinding.** `colliderFlag` springs are rebound to the *current* body's colliders on
  every composition (`rebuildColliderBindings`; step "rebind colliderFlag springs to current body
  colliders"). Packages MUST therefore ship collider data with the body part and flag-based
  bindings that survive recombination.
- **Simulation reset on selection change.** Whenever body/head/hair/head-optional selection
  changes, the engine imports a new combined character, recreates the spring runtime, and resets
  simulation state (`engine/src/engine/Haruki3DEngine.ts:1136-1148`; `reloadAnimationPlayback`
  `resetSpring`, lines 2755-2770). Animation playback position is preserved across same-role part
  switches and restored after the rebuild (`Haruki3DEngine.ts:1139,1143-1148`;
  `engine/README.md`, Custom wardrobe behavior). This matches the producer-side requirement in
  `exporter/README.md`'s closing paragraph ("The viewer must merge the active part SpringBone
  records, rebind current body colliders, and reset simulation whenever body/head/hair/accessory
  selection changes").
- **Role scoping.** A role is `characterId:unit`; custom switching is limited to parts of the
  currently loaded role, and every composed part's identity must match the active role
  (`runtimePartComposer.ts` role assertion at lines ~980-989;
  `customWardrobeController.ts` `assertSameActiveCharacter`).
- **Assembly data is mandatory.** Composition fails unless the parts' prefab graphs provide the
  official `model_combine_setup` body/head paths (`runtimePartComposer.ts:1283-1286`), and the
  runtime extension must expose `runtimeUnitySetup` version `"0414"` and a non-empty native mesh
  set version `"0414"` (`Haruki3DEngine.ts:2374`; `unityPrefabRuntime.ts:230-247,758-766`).
- **Motion.** The role runtime selects the Unity motion package; `motionPackage.unityMotionJson`
  must resolve to a `.msgpack.br` URL or clip loading fails
  (`engine/src/engine/animationPlaybackRuntime.ts:358-362`). Unity motion schema:
  `exporter/Models/MotionModels.cs` (`PjskUnityMotionRuntime`, `clips[].tracks[].times/values`
  ext-42 encoded per section 2.2). Embedded face clips are promoted with the body loop
  (`engine/README.md`).
- **Default selection probing.** At load the engine probes registry candidates in batches of 24
  (bounded at 720) until a compatible loaded body + (head|head_optional) + hair selection exists,
  else the load fails (`runtimePackageLoader.ts:132-157`). A published package set MUST therefore
  contain loadable packages for at least one compatible default combination per role — another
  reason for the section 4.2 publish ordering.

---

## 6. Change management

Any change to this contract — layout paths, the ext-42 wire format or its property allow-lists,
document schemas or version markers, texture-store addressing or transfer classes, catalog/registry
semantics, or the behavioral requirements in section 5 — MUST land as **one PR** that updates all
of, together:

1. the exporter encoder and emitters (`exporter/Services/RuntimeJsonWriter.cs` and the affected
   exporters/models),
2. the engine decoder and loaders (`engine/runtime-binary-codec.mjs`,
   `engine/src/runtime/**`, `engine/src/parts/**`, `engine/part-runtime-core.mjs`),
3. this specification (`contract/SPEC.md`),
4. the contract roundtrip check (`contract/roundtrip`).

Version markers (section 2.3) MUST be bumped whenever a consumer could otherwise misread old or new
bytes; the engine's accepted-version sets and the exporter's cache-validation strings MUST be
updated in the same PR. `TextureCompactor.Ktx2EncoderVersion` MUST change whenever KTX2 encode
parameters change, invalidating the shared encode cache.

---

## Known issues flagged in this revision

1. **`status: "empty"` is undocumented in `exporter/README.md`** (its registry section names only
   planned/missing) but is emitted for head_optional empty slots
   (`CostumeRegistryExporter.cs:262-266`) and has distinct consumer semantics
   (`runtimePackageLoader.ts:656-662`). This spec (section 4.4) is normative.
2. **Null-unit path-segment mismatch (latent).** For a registry group whose unit key is empty, the
   producer's `RuntimePathUnitSegment` returns `""` (it maps only *null* to `"default"`,
   `CostumeRegistryExporter.cs:506-509,812-815`), so `Path.Combine` collapses the segment and the
   scoped registry/compat file would be written one level up (e.g.
   `parts/by-role/<id>/part-registry.msgpack.br`), while the consumer always requests the
   `default` segment (`runtimePackageLoader.ts:445-447`). Unreachable for the 31 public roles
   (all have non-null units, enforced by `RuntimeRoleCatalogExporter.Build`), but producers MUST
   NOT emit engine-consumed rows with a null unit until the two sides agree.
3. **`parts/part-source-map.msgpack.br` is absent from the `exporter/README.md` output list**
   though always written (`CostumeRegistryExporter.cs:37`). Documented in section 1.
