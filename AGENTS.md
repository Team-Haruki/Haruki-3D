# AGENTS.md

Guidance for coding agents and contributors working in this repository. This
file is the single source of truth for agent instructions; `CLAUDE.md` only
points here.

## What this repository is

Monorepo for the Project SEKAI Costume 3D pipeline:

```text
costume bundles + masterdata -> exporter -> .msgpack.br packages -> engine (CostumeShop) -> browser / preview PNG
```

| Path | What it is |
|---|---|
| `exporter/` | C# / .NET 8 offline converter (`Haruki-3D-Exporter.csproj`). Reads Unity AssetBundles through AssetStudio (`seiunx-dev/AssetStudio`, branch `sekai-modified`, pinned revision) and writes Brotli-compressed MessagePack runtime packages. |
| `engine/` | TypeScript + Three.js browser runtime, npm package `haruki-3d-engine` (private), plus the persistent HTTP capture service `capture-server.mjs`. |
| `contract/` | `SPEC.md`, the authoritative package format specification, and two cross-language suites: `roundtrip/` (ext-42 wire format) and `parity/` (path formulas, role identity table, registry rows). |
| `scripts/ci/` | `sonar-dotnet.sh`, the SonarQube Cloud scan used by CI. |
| `.github/` | `workflows/ci.yml`, `workflows/release.yml`, `path-filters.yml`, `dependabot.yml`. |

Subproject docs: [README.md](README.md), [engine/README.md](engine/README.md),
[engine/docs/api.md](engine/docs/api.md) (browser API),
[engine/docs/adr/](engine/docs/adr/), [exporter/README.md](exporter/README.md)
(CLI reference), [contract/SPEC.md](contract/SPEC.md).

### exporter layout

- `Program.cs`: entry point. CLI parsing in `Services/ConversionOptionsParser.cs`.
- `Services/`: one service per stage. Examples: `CostumeRegistryExporter`
  (registries), `RuntimeRoleCatalogExporter`, `PartPackageExporter` (core+delta
  part packages), `RoleRuntimeExporter` / `MotionPackageExporter`,
  `TextureCompactor` (PNG optimization, KTX2), `ContentAddressedStore` (shared
  CAS), `RuntimeJsonWriter` (MessagePack + Brotli encoder, ext type 42).
- `Models/`: serialized records. Their `JsonPropertyName` values become the
  MessagePack map keys.
- `Tests/`: `ConfigParserSmoke.cs` and `PartMaterialMetadataSmoke.cs`, compiled
  only in `HARUKI_EXPORTER_CONFIG_TEST` mode, and `AssetStudioMetadata/`, a
  separate project.
- `scripts/`: `dotnet.sh` (wrapper with project-local obj/bin/NuGet paths),
  `prepare-assetstudio.sh` (clones and builds the pinned AssetStudio),
  `publish-linux-x64.sh`, plus the Node audit scripts and their `test-*.mjs`
  tests.

### engine layout

- `src/index.ts`: default entry (CostumeShop kernel). `src/internal.ts`:
  internal entry for capture and diagnostics. `src/base/`, `src/costume_shop/`:
  the `./base` and `./costume_shop` package exports.
- `src/runtime/`: package loading and decoding (`runtimePackageLoader.ts`,
  Brotli WASM, decode worker). `src/parts/`: part composition and wardrobe
  switching. `src/engine/`: the Three.js renderer (`Haruki3DEngine.ts`, Unity
  prefab, SpringBone, motion, lighting, outline). `src/materials/`: shaders.
  `src/kernel/`, `src/capture/`, `src/captureHarness.ts`: kernel and capture
  harness.
- Top-level `.mjs` files: `capture-server.mjs` (HTTP service),
  `runtime-binary-codec.mjs` (ext-42 decoder), `part-runtime-core.mjs`
  (core+delta merge), `region-routing.mjs`, `png-rgba.mjs`, `config/`
  (config resolution). `check-*.mjs`, `inspect-*.mjs` and `format-*.mjs` are
  standalone debug scripts.
- `tests/*.test.mjs`: `node --test` unit tests. `tests/browser/`: Playwright
  smoke against the capture preview. `tests/browser-runtime/`: Playwright
  against a real exported runtime. Run it with
  `npm run test:browser:runtime:local`, which needs Docker and
  `HARUKI_RUNTIME_E2E_ROOT` pointing at a multi-region exporter output.
- `examples/minimal/`: canvas-only consumer build used by `npm run test:consumer`.

## Build, test, run

CI is the reference. Use Node 22 and .NET SDK 8 (`exporter/global.json` pins
8.0.421 with `rollForward: latestFeature`).

### engine (run from `engine/`)

```bash
npm ci --ignore-scripts
npm run build                     # library + types + capture harness -> dist/
npm test                          # node --test tests/*.test.mjs (imports dist/, so build first)
npm run test:coverage             # c8, what CI runs
npm run test:consumer             # build + build:consumer + scripts/check-consumer-build.mjs
npm run test:browser:chromium     # PR CI; `npm run test:browser` runs all three browsers (main CI)
npm run dev:capture               # minimal capture harness (vite)
node capture-server.mjs           # persistent capture HTTP service (default port 8080)
```

### exporter (run from `exporter/`)

The exporter compiles against AssetStudio DLLs, so prepare them first and pass
the root explicitly. The `AssetStudioRoot` defaults in the csproj are
developer-machine paths.

```bash
ASSETSTUDIO_ROOT=/tmp/haruki-assetstudio bash scripts/prepare-assetstudio.sh
dotnet build -p:AssetStudioRoot=/tmp/haruki-assetstudio
node --test scripts/test-costume-masterdata-audit.mjs scripts/test-face-motion-export.mjs
dotnet run --project Tests/AssetStudioMetadata/AssetStudioMetadata.csproj -c Release \
  -p:AssetStudioRoot=/tmp/haruki-assetstudio -- --synthetic-only
HARUKI_EXPORTER_CONFIG_TEST=true dotnet run --project Haruki-3D-Exporter.csproj   # config/parser smoke
```

`./scripts/dotnet.sh build` is the local wrapper. It uses `PJSK_DOTNET_ROOT`,
or `dotnet` on `PATH`. CLI usage (`--emit-costume-registries`,
`--emit-runtime-role-catalog`, `--emit-part-packages`, `--emit-role-runtimes`,
`--export-face-motion`, `--optimize-texture-store`) is documented in
`exporter/README.md`.

### contract (run from the repository root)

```bash
npm ci --prefix engine --ignore-scripts
./contract/roundtrip/run.sh
./contract/parity/run.sh
```

## Configuration

- engine: `haruki-3d-engine.config.json` (gitignored; copy
  `haruki-3d-engine.config.example.json`) or `HARUKI_ENGINE_CONFIG=<path>`.
  Sections: `capture`, `chromium`, `server`. Environment variables override the
  file. Examples: `HARUKI_RUNTIME_ROOT`, `HARUKI_CAPTURE_OUTPUT_DIR`,
  `HARUKI_CAPTURE_*` (size, scale, timeout, phase, clip, warmup, spring
  mode, camera, FaceSDF, temp TTL/size, GC interval, idle shutdown), `HARUKI_SERVER_HOST`, `PORT`,
  `CHROMIUM`. See `engine/config/haruki-3d-engine-config.mjs`.
  The service exposes `GET /healthz`, `POST /capture`, `/captures/*`,
  `/runtime/*`, and a `/regions/<region>` prefix for multi-region runtime
  roots.
- exporter: `haruki-3d-exporter.config.json` in the working directory
  (gitignored; copy `haruki-3d-exporter.config.example.json`) or `--config`.
  Command-line flags override config values. `HARUKI_KTX_TOOL` overrides the
  `ktx` binary used for KTX2 encoding.

## Conventions

- Commit titles: `[Type] Imperative summary`. Recent history uses `[Feat]`,
  `[Fix]`, `[Refactor]`, `[Perf]`, `[Chore]`, `[Docs]`. Dependabot uses the
  `[Chore] ` prefix. Older subtree history also has `feat:`/`fix:` titles; do
  not use that style for new commits.
- Work on a branch and merge through a PR into `main`.
- Format contract: any change to the package format (layout paths, ext-42
  wire format or allow-lists, schemas/version markers, texture store,
  catalog/registry semantics, viewer behaviour) lands as one PR that updates
  the exporter emitters, the engine decoder/loaders, `contract/SPEC.md`, and
  `contract/roundtrip` together (`contract/SPEC.md` section 6). Bump version
  markers whenever old or new bytes could be misread.
- Runtime metadata is `.msgpack.br` only. The engine rejects any other URL.
  There is no JSON or gzip transport.
- Engine module boundaries (`engine/docs/adr/0001-runtime-module-boundaries.md`):
  `base` must not import `costume_shop`, not even type-only imports.
  `tests/engine-module-boundaries.test.mjs` enforces this.
- Publish order for an export: changed part packages, cores, textures and role
  runtimes first, the runtime role catalog last.
- Keep local config files, machine paths, and game assets out of the
  repository. The repository contains no game assets.

## CI and release

- `ci.yml` (`CI`) is path-filtered through `.github/path-filters.yml`.
  Pull requests compare against the base branch, `main` pushes against the
  previous commit, and manual dispatch runs everything. Jobs:
  - `Engine`: shared `node-ci` template.
  - `Exporter`: AssetStudio prepare, build, Node tests, metadata contracts,
    config smoke.
  - `Contract round-trip`: both contract suites.
  - `Sonar`: `scripts/ci/sonar-dotnet.sh`. Skipped for Dependabot and fork PRs.
  - `Docker engine` and `Docker exporter`: images built in parallel with the
    tests.
  - `Workflow lint`.
  - `CI OK`: aggregate job.
  Images push `:sha-*` immediately and move `:main` only after `CI OK`
  passes.
- `release.yml`: push an `engine-v<version>` tag (the version must equal
  `engine/package.json`) or an `exporter-v<version>` tag. The gate waits for
  the commit's `CI OK`, then the image already built from `main` is re-tagged.
- Images: `ghcr.io/team-haruki/haruki-3d-engine` and
  `ghcr.io/team-haruki/haruki-3d-exporter`. Each builds with its subproject
  directory as the context.
- Workflows are thin callers of `seiunx-dev/ci-templates` `@v1`. Reuse a
  template first. Customize in the caller only when no template fits, with a
  comment saying why.
- `main` has no branch protection today, so `CI OK` is not enforced on
  merges. Check it before merging.

## Gotchas

- The engine unit tests import `dist/`. Run `npm run build` before `npm test`.
- `HARUKI_EXPORTER_CONFIG_TEST=true` switches the csproj compile items, so it
  always needs a rebuild.
- The AssetStudio revision is pinned in two places,
  `exporter/scripts/prepare-assetstudio.sh` and the `exporter/Dockerfile` build
  args. Change them together. The CI cache key hashes the script.
- Shared and compiled content stores rely on hard links. The output directory
  and `--shared-content-store` must be on the same filesystem.
