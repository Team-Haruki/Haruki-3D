# Haruki-3D

Monorepo for the Haruki 3D pipeline: an offline converter that turns Project
SEKAI Unity AssetBundles into a browser-friendly runtime package format, and a
browser engine that renders those packages.

- `exporter/` — C# / .NET 8 offline converter. Reads Unity AssetBundles with
  the Team-Haruki AssetStudio fork and emits the Brotli-compressed MessagePack
  (`.msgpack.br`) runtime package format.
- `engine/` — TypeScript + Three.js browser runtime plus the Unity WebGL MV
  host (npm package `haruki-3d-engine`). Loads only the exported runtime
  packages, never raw bundles.
- `contract/` — the authoritative package format specification
  ([contract/SPEC.md](contract/SPEC.md)) and the cross-language round-trip
  test ([contract/roundtrip/](contract/roundtrip/)) that keeps exporter output
  and engine input in lockstep.

## Pipeline

```text
game bundles + masterdata -> exporter -> .msgpack.br package -> engine (CostumeShop / MV) -> browser
```

## Quick Start

Each subproject builds from its own directory; see its README for detail.

`engine/` ([engine/README.md](engine/README.md)):

```bash
cd engine
npm install
npm run build
```

`exporter/` ([exporter/README.md](exporter/README.md)):

```bash
cd exporter
./scripts/dotnet.sh build
```

## Repository Conventions

- Release tags are prefixed per subproject: `engine-v*` and `exporter-v*`.
- CI lives only in the root `.github/workflows/`: `ci.yml` (`CI`) and
  `release.yml` (`Release`), thin callers of the shared templates in
  [`seiunx-dev/ci-templates`](https://github.com/seiunx-dev/ci-templates)
  (`@v1`). Reuse the templates first; customize in the caller only when they
  genuinely cannot meet a need (the .NET exporter build, the contract test and
  the dotnet-sonarscanner scan have no template), with a comment saying why.
- `ci.yml` is path-filtered per subproject through `.github/path-filters.yml`
  (pull requests against the base branch, `main` pushes against the previous
  commit; manual dispatch runs everything): `Engine` (`node-ci`: unit tests
  under c8, the consumer build, Playwright on chromium for PRs and on
  chromium/firefox/webkit for `main`), `Exporter` (AssetStudio prepare, build,
  node tests, config smoke), `Contract round-trip` (`contract/roundtrip/run.sh`
  and `contract/parity/run.sh`), `Sonar` (`scripts/ci/sonar-dotnet.sh`, reusing
  the engine's lcov), and the two images (`docker.yml`, in parallel with the
  tests; `:sha-*` right away, `:main` only after `CI OK`). The aggregate job
  **`CI OK`** is the only required status check.
- `release.yml`: an `engine-v<version>` tag must match `engine/package.json`;
  an `exporter-v*` tag takes its version from the tag. The gate waits for the
  tagged commit's `CI OK`, then the `main` image of that commit is re-tagged
  (rebuilt if `main` never built it) to `:<version>`, `:<major>.<minor>` and,
  for the highest stable tag, `:latest`.
- Each Docker image builds with its subproject directory as the build context:
  `engine/` with `engine/Dockerfile`, `exporter/` with `exporter/Dockerfile`.
- GHCR image names are unchanged from the standalone repositories:
  `ghcr.io/team-haruki/haruki-3d-engine` and
  `ghcr.io/team-haruki/haruki-3d-exporter`.

## Licensing

The repository is released under the MIT License. See [LICENSE](LICENSE).

- `exporter/` keeps its own MIT license text. See
  [exporter/LICENSE](exporter/LICENSE).
- `engine/` is MIT licensed. See [engine/LICENSE](engine/LICENSE). Third-party
  notices for code embedded in the engine live at
  [engine/THIRD_PARTY_NOTICES.md](engine/THIRD_PARTY_NOTICES.md).

## History

This repository was assembled from
[Team-Haruki/Haruki-3D-Engine](https://github.com/Team-Haruki/Haruki-3D-Engine)
and
[Team-Haruki/Haruki-3D-Exporter](https://github.com/Team-Haruki/Haruki-3D-Exporter)
via `git subtree`, preserving the full history of both projects.
