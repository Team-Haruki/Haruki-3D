# Haruki-3D-Exporter

Offline converter for Project SEKAI character bundles.

The converter reads Unity AssetBundles with AssetStudio and writes a browser-friendly runtime package for Haruki 3D Engine.

Haruki-3D-Exporter is the `exporter/` subproject of the Haruki-3D monorepo.
Unless a command below spells out an `exporter/` path, run it from the
`exporter/` directory.

## Asset rights

The tool processes game assets owned by SEGA, Colorful Palette, and Crypton
Future Media. This repository contains no game assets. Exported outputs are
derived from the user's own game data and are intended for personal and
interoperability use only; they must not be redistributed.

## Final Pipeline

The exporter has one production package format: role-scoped registries, core+delta part packages, role runtimes, and Brotli-compressed MessagePack metadata.

Build the package in three stages:

```bash
dotnet run -- \
  --emit-costume-registries \
  --master /path/to/master \
  --asset-root /path/to/AssetBundles \
  --out /path/to/output

dotnet run -- \
  --emit-part-packages \
  --master /path/to/master \
  --asset-root /path/to/AssetBundles \
  --out /path/to/output

dotnet run -- \
  --emit-role-runtimes \
  --master /path/to/master \
  --asset-root /path/to/AssetBundles \
  --out /path/to/output
```

The CostumeShop asset root must contain `live_pv/model/characterv2`. Its runtime metadata is always emitted as `.msgpack.br`; JSON, gzip, self-contained part runtimes, legacy `character` roots, and direct VRM/GLB full exports are not supported by that preview pipeline. `--emit-mv-source-set` is separate and preserves the dependency-closed 3DMV selection, including per-part `characterv2` choices and any legacy `character` fallback selected by the official runtime rule.

## Build

```bash
./scripts/dotnet.sh build
```

`scripts/dotnet.sh` wraps the `dotnet` CLI with project-local obj/bin/NuGet
paths. It selects the SDK as follows:

- `PJSK_DOTNET_ROOT` set: uses `$PJSK_DOTNET_ROOT/dotnet` and exports that
  directory as `DOTNET_ROOT`.
- otherwise: falls back to `dotnet` found on `PATH`.

It exits with an error when neither yields an executable `dotnet`:

```bash
PJSK_DOTNET_ROOT="$HOME/.dotnet" ./scripts/dotnet.sh build
```

When building outside Docker against a local AssetStudio checkout, pass its path through MSBuild:

```bash
./scripts/dotnet.sh build -p:AssetStudioRoot=<AssetStudio-Haruki-directory>
```

Publish the Linux x64 runtime directory used by Haruki-Sekai-Asset-Updater external mounts:

```bash
scripts/publish-linux-x64.sh /data/xy/haruki-3d-exporter-runtime/linux-x64
```

The output directory contains a self-contained `Haruki-3D-Exporter` executable and its AssetStudio runtime dependencies.
Mount that directory into updater deployments that enable `regions.<region>.export.haruki_3d`.

If the host does not have a .NET SDK, build the Docker image and copy `/app/exporter` out of a created container.
That copied directory is the same external runtime mount payload.

## Docker

Build the Linux exporter image with `exporter/` as the build context. From the
monorepo root:

```bash
docker build -t haruki-3d-exporter exporter/
```

Or from `exporter/`:

```bash
docker build -t haruki-3d-exporter .
```

The Docker build clones `seiunx-dev/AssetStudio` and builds the required
AssetStudio `net8.0` dependencies inside the image. Override the source when
needed:

```bash
docker build \
  --build-arg ASSETSTUDIO_REPOSITORY=https://github.com/seiunx-dev/AssetStudio.git \
  --build-arg ASSETSTUDIO_BRANCH=sekai-modified \
  -t haruki-3d-exporter .
```

Run the image by mounting masterdata, AssetBundles, and an output directory:

```bash
docker run --rm \
  -v <config-file>:/app/haruki-3d-exporter.config.json:ro \
  -v <master-data-dir>:/data/master:ro \
  -v <asset-bundle-root>:/data/assets:ro \
  -v <output-dir>:/data/out \
  haruki-3d-exporter \
  --config /app/haruki-3d-exporter.config.json \
  --emit-role-runtimes \
  --role-character3d-id 5 \
  --master /data/master \
  --asset-root /data/assets \
  --out /data/out
```

GitHub Actions (the workflows in the monorepo root `.github/workflows/`) builds and publishes a
self-contained Linux image to GHCR as `ghcr.io/team-haruki/haruki-3d-exporter` on `main` and
`exporter-v*` tags. Pull requests only build the image.

## Command-Line Reference

Pick exactly one emit mode per invocation. `--out` is required in every mode.

Mode selectors:

- `--emit-costume-registries` writes the `.msgpack.br` character, part,
  compatibility, and unlock registries.
- `--emit-runtime-role-catalog` refreshes the runtime role Catalog from
  masterdata alone; it needs `--master` but no `--asset-root`.
- `--emit-part-packages` writes core+delta `part-runtime.msgpack.br` packages;
  with `--part-costume3d-id` and `--part-type` it builds one package, otherwise
  the full incremental set.
- `--emit-role-runtimes` writes `roles/<characterId>/<unit>/role-runtime.msgpack.br`
  packages with motion metadata.
- `--emit-mv-source-set` validates and stages a manifest-selected MV bundle
  closure; it needs `--asset-root` and `--mv-manifest` but no `--master`.
- `--export-face-motion` writes `face_motion.json` from a `costume_setting`
  bundle or decoded AnimationClip JSON.
- `--optimize-texture-store` runs the standalone lossless texture-store
  optimization pass over an existing output (`--out` plus the
  `--png-optimize`/`--texture-compact-workers` options).

Flags shared by every mode:

- `--config <json>` loads option defaults from a config file; a
  `haruki-3d-exporter.config.json` in the working directory is picked up
  automatically, and command-line flags override config values.
- `--out <directory>` (`-o`) sets the output directory; for
  `--export-face-motion` it may be a `face_motion.json` path or a directory.
- `--master <directory>` provides the masterdata used to resolve runtime roles
  and parts.
- `--asset-root <directory>` points at the AssetBundles root containing
  `live_pv/model/characterv2`.
- `--assetstudio-log-level <warning|info|debug>` sets the AssetStudio console
  log verbosity (default `warning`).
- `--help` (`-?`) prints usage.

`--emit-part-packages` flags:

- `--part-costume3d-id <id>` selects the costume3d id of a single part package;
  it must be paired with `--part-type`.
- `--part-type <body|head|hair|head_optional>` selects the part type of that
  single package; `accessory` is accepted as an alias for `head_optional`.
- `--part-unit <unit>` selects an optional unit variant for the single package.
- `--manifest <json>` records part package input file stamps so unchanged
  packages are skipped by later incremental runs; multi-worker runs default it
  to `<out>/haruki-3d-export-manifest.json`.
- `--part-package-process-concurrency <n>` runs the full export across `n`
  worker processes; `0` means the CPU count. Single-part exports must keep the
  default of `1`.
- `--part-package-workers <n>` and `--part-package-core-count <n>` are aliases
  for `--part-package-process-concurrency`.
- `--part-package-shard-count <n>` and `--part-package-shard-index <i>` process
  one deterministic shard of the package groups; shards cannot be combined with
  process concurrency.
- `--part-package-claim-directory <dir>` coordinates independently launched
  exporter processes: each package group is claimed once through atomic
  `.claim` files in that directory, and claiming workers do not save the
  manifest (the orchestrator rebuilds it).
- `--part-package-work-list <json>` limits a worker to the registry entries and
  character heights in a planner-written work list (this is how the concurrency
  parent and the updater hand each worker its share); the worker writes its
  metrics to `<work-list>.summary.json` and skips the manifest save and
  shared-store/KTX2 finalization.
- `--bundle-hash-index <path>` reuses updater-provided SHA-256 values when
  fingerprinting source bundles.
- `--bundle-dependency-index <path>` preserves the updater-provided logical
  bundle dependency closure.
- `--shared-content-store <directory>` hard-links exact texture and
  `part-runtime*.msgpack.br` bytes into a shared cross-region SHA-256 CAS.
- `--compiled-content-store <directory>` restores already compiled core/delta
  objects when resolved input bundles are byte-identical; it requires the
  shared content store.
- `--compact-textures` deduplicates package textures by exact SHA-256 and
  rewrites runtime package paths after export (skipped when the shard count is
  above 1).
- `--convert-model-textures <true|false>` controls AssetStudio model texture
  conversion (default `false`); also honored by `--emit-role-runtimes`.
- `--texture-format <png|ktx2>` selects the final runtime texture format
  (default `png`).
- `--png-optimize <oxipng|off>` selects the lossless PNG optimization mode used
  during compaction and store optimization (default `oxipng`).
- `--texture-compact-workers <n>` limits concurrent texture workers; `0` means
  `min(4, CPU count)`.

`--emit-role-runtimes` flags:

- `--role-character3d-id <id>` is repeatable and selects specific character3d
  rows; without it, one representative row per character+unit role is exported.
- `--motion <path>` (`-m`) supplies a `costume_setting` bundle or a folder
  containing `unity-motion.json`/`face_motion.json`/`light_motion.json` for
  motion metadata.
- `--part-package-process-concurrency <n>` (and its aliases) also splits role
  runtime export across worker processes.

`--emit-mv-source-set` flags:

- `--mv-manifest <manifest.json>` selects the dependency-closed MV bundle
  closure to validate and stage.

`--export-face-motion` flags:

- `--motion <path>` (`-m`) is the required input: a `costume_setting` bundle, a
  decoded clip folder, or an AnimationClip JSON file.
- `--source-path <bundle-path>` overrides the source path recorded inside the
  emitted `face_motion.json`.

## Costume Registries

Generate compact viewer/exporter registries from masterdata and the local bundle
mirror:

```bash
./scripts/dotnet.sh run -- \
  --emit-costume-registries \
  --master <master-data-dir> \
  --asset-root <asset-bundle-root> \
  --out <output-dir>
```

This writes:

- `parts/part-registry.msgpack.br` for body, hair, and head/head_optional rows
- `parts/part-registry-compact.msgpack.br` as the field-name-free global
  registry consumed by Cloud
- `parts/head-hair-compatibility.msgpack.br` for custom-mode head/hair rules
- `parts/head-hair-compatibility-compact.msgpack.br` as the field-name-free
  compatibility registry consumed by Cloud
- `parts/compat/by-unit/*/head-hair-compatibility.msgpack.br` as a runtime-sized
  per-unit deny list; the full registry above remains available for audits
- `parts/card-costume-unlocks.msgpack.br` for card unlock/source metadata

When only masterdata changed and the transient raw 3D bundles have already been
removed, refresh the stable role Catalog without touching part packages:

```bash
./scripts/dotnet.sh run -- \
  --emit-runtime-role-catalog \
  --master <master-data-dir> \
  --out <output-dir>
```

This writes `runtime-role-catalog.msgpack.br` for the 31 public roles plus
`parts/by-role/<characterId>/<unit>/runtime-role-catalog.msgpack.br`. Publish
the Catalog after any changed part and role runtime packages, so clients never
observe defaults that point at an unfinished incremental export.

The output path is a stable incremental store. Registry generation is rerun for
each region whenever that region's masterdata updates; it does not create a
release directory or release identifier. The runtime role Catalog records the
masterdata `dataVersion` as `masterVersion` and skips unchanged refreshes.

Registry generation does not scan the bundle mirror for every row. Part entries
therefore use `status: "planned"` when masterdata can produce a deterministic
bundle path, and `status: "missing"` only when required masterdata is absent.
Single part export remains responsible for validating that the planned
bundle exists and can be opened.

There is no runtime preset mode. Default role components and explicitly selected
components use the same assembly and compatibility rules.
`costume3dModelNotAvailablePatterns.json` is the custom head/hair blacklist:
combinations absent from it are allowed. Default hairs are emitted separately
as conflict fallback hints.

## Runtime Part Packages

Custom runtime assembly uses the registries above plus incremental part packages.
Build one runtime-loadable package with:

```bash
./scripts/dotnet.sh run -- \
  --emit-part-packages \
  --part-costume3d-id 2 \
  --part-type body \
  --master <master-data-dir> \
  --asset-root <asset-bundle-root> \
  --out <output-dir>
```

This writes a light
`parts/<partType>/<costume3dId>/<unit>/part-runtime.msgpack.br` delta. Heavy
native meshes, SpringBone data, and morph bindings shared by color variants are
written once under `parts/_cores/<partType>/<hash>/part-runtime-core.msgpack.br`.
Textures are written directly to the output's exact SHA-256 store under
`_texture_store/sha256`; packages refer to those immutable files instead of
building and later deleting duplicate part-local texture trees.

A full `--emit-part-packages` run without `--part-costume3d-id` exports every
planned package incrementally; the `--manifest`, concurrency, shard,
claim-directory, and work-list flags in the command-line reference split that
run across workers.

Updater sparse inputs contain a `.haruki-sparse-input` marker and zero-byte
placeholders for unchanged bundles whose raw files were already removed. Part
workers process only non-empty bundles in that mode, preserve manifest stamps
for reusable runtime packages, and fail rather than publish an empty or
incomplete manifest.

Pass `--shared-content-store <directory>` to place exact texture and
`part-runtime*.msgpack.br` bytes in a cross-region SHA-256 CAS.
Region paths stay unchanged and are hard-linked to immutable CAS objects, so
the output and shared store must be on the same filesystem. The first run is an
explicit full migration. Later runs use `content-addressed-store-state.json` to
skip files that are still protected CAS links; only newly exported or replaced
files are hashed and relinked.

Pass `--compiled-content-store <directory>` together with the shared content
store to reuse already compiled core/delta objects across sequential region
exports when their resolved input bundles are byte-identical. Restored deltas
are patched with the current region's identity and manifest fields. The shared
content store is the authoritative source for the cached texture hashes.

Texture lossless optimization is deliberately separate from package export.
After publishing an output, run the exporter with only `--out`,
`--optimize-texture-store`, and the desired `--png-optimize`/worker options.
The optimizer works on temporary files and keeps a result only when it is
smaller. It stores the optimized bytes under their new exact hash, rewrites
part-runtime references, and only then removes the old object, so exports do not
wait for oxipng and CAS paths remain truthful.

Set `--texture-format ktx2` (or `"textureFormat": "ktx2"`) to finalize runtime
textures as UASTC KTX2 with offline mipmaps and Zstandard compression. PNG stays
the default. Color textures (`main` and `shadow`) are encoded as sRGB, while
data textures (`value` and `faceShadow`) are encoded as linear UNORM. When one
PNG is used by both classes, the finalizer emits two content-addressed variants.
With `--shared-content-store`, finalization first preserves the source PNG in
the cross-region CAS for compiled-package restoration, then publishes KTX2 and
adds those exact bytes to the same CAS. Runtime references are validated before
the region-local PNG is removed. UASTC prioritizes render fidelity and GPU-ready
upload; with a full mip chain it is not guaranteed to be smaller than PNG on disk.

Runtime metadata uses direct object-to-MessagePack serialization and Brotli quality
6. It avoids the former JSON UTF-8 and DOM intermediate while retaining a good
size/speed balance.

Large arrays on the explicit native-mesh and Unity-motion
schemas use runtime extension type `42`: float data is little-endian float32 and
mesh indexes are little-endian uint16/uint32. Unrelated arrays with the same
property names remain ordinary MessagePack arrays.

The viewer must merge the active part SpringBone records, rebind current
body colliders, and reset simulation whenever body/head/hair/accessory selection
changes.

## Stage a 3DMV source bundle set

Use a dependency-closed MV manifest to validate and stage updater output or the
game's wrapped source bundles. The known 0x10 wrapper is normalized to UnityFS:

```bash
dotnet run -- \
  --emit-mv-source-set \
  --mv-manifest /path/to/mv-0112-manifest.json \
  --asset-root /path/to/raw-bundles \
  --out /path/to/mv-0112-source
```

The output contains `mv-source-set.json`, ClauseKAI-shaped `deps.json`, and the
logical bundle tree below `source_bundles/`. These are source-platform bundles,
not browser bundles. A Unity WebGL rebuild/conversion step is required before
serving them to `UnityWebRequestAssetBundle` in a browser.

## Masterdata Audit

The costume masterdata audit checks the relationships needed by role defaults
and custom component assembly without opening Unity bundles:

```bash
node --test scripts/test-costume-masterdata-audit.mjs

node scripts/audit-costume-masterdata.mjs \
  --master <master-data-dir>
```

The audit treats broken hard references as errors and known masterdata quirks as
warnings. Pattern rows that point to missing costume ids are kept for diagnostics,
but those ids should not be exposed as selectable viewer parts.

## SpringBone Audit

`scripts/audit-springbone.mjs` summarizes exported SpringBone data for manual
review. For each input it rebuilds the spring chains from the bone and pivot
transform paths, then prints one row per joint (pivot node, root-pivot and
spring-root classification, side and segment derived from the hair bone name,
collider flag, stiffness, drag, spring force, and Y/Z angle limits) and lists
detected `Left_`/`Right_` mirror pairs with whether their Y limits match and
their Z limits mirror each other. Inputs are `head.springbone.json` or combined
`springbone.json` files, or an export directory containing
`head.springbone.json`:

```bash
node scripts/audit-springbone.mjs <head.springbone.json|springbone.json|output-dir> [...]
```

## Character3D Hair Audit

`scripts/audit-character3d-hair.mjs` cross-checks `character3ds.json` and
`costume3dModels.json` against the bundle mirror. For each character3d row it
classifies the default hair and head bundles (default hair `0000`, alternate
no-accessory hair ending in `n`, lettered accessory variants, complete heads,
and `head_only` accessories), resolves the hair/head variant groups, verifies
that the referenced `face` and `head_optional` bundles exist under the asset
root, reports the resulting head composition kind, and lists the face bundles
available in the hair's variant group. Rows using the default `0000` hair are
skipped unless `--only-non-default false` is passed; `--json true` emits the
summary and rows as JSON:

```bash
node scripts/audit-character3d-hair.mjs \
  --master <master-data-dir> \
  --asset-root <asset-bundle-root> \
  [--character3d-id <id>]
```

## License

Haruki-3D-Exporter is released under the MIT License. See `LICENSE`.
