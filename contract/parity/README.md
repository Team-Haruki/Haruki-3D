# Cross-language parity contract checks

The [round-trip test](../roundtrip/README.md) guards the ext-42 wire format;
this suite guards the contracts *around* it that are hardcoded on both sides
and could drift silently:

- the **`<unit>` path segment formula** (`null`/`""` → `default`) —
  `RuntimeJsonWriter.RuntimePathUnitSegment` (exporter) vs
  `runtimePathUnitSegment` in `engine/src/runtime/runtimePackageLoader.ts`;
- the **31-role identity table** (roleId → characterId + unit) —
  `RuntimeRoleCatalogExporter.ExpectedRole` (exporter) vs
  `expectedRuntimeRoleIdentity` (engine);
- the **runtime package path formulas** —
  `roles/<characterId>/<unit>/role-runtime.msgpack.br` and the
  `parts/by-role/<characterId>/<unit>/` catalog layout, as emitted by the
  production exporter and as expected/validated by the production engine
  loader.

Both sides run their **production** code — nothing is reimplemented in the
tests.

## How it works

1. `emit/ParityEmit.csproj` compiles the exporter's production sources **in
   place** via `<Compile Include>` (the same direct-inclusion pattern the
   round-trip harness and the exporter's `HARUKI_EXPORTER_CONFIG_TEST` mode
   use). `emit/Program.cs`:
   - runs `RuntimeJsonWriter.RuntimePathUnitSegment` over the shared
     `unit-segment-vector.json` inputs;
   - reads the exporter's role table through its private
     `RuntimeRoleCatalogExporter.ExpectedRole` seam via reflection (the same
     way `exporter/Tests/ConfigParserSmoke.cs` reaches private production
     seams, instead of widening visibility for a test);
   - synthesizes a minimal master-data fixture **from that table** (so the
     fixture can never drift from the exporter silently) and runs the
     production `RuntimeRoleCatalogExporter.WriteFromMaster`, which validates
     the fixture, computes every `roleRuntimePath` with the production
     formula, and writes the root + 31 scoped `runtime-role-catalog.msgpack.br`
     files with the production `parts/by-role/...` layout into `out/package/`;
   - writes `out/exporter-parity.json` with the exporter-side results.
2. `rolldown.config.mjs` bundles the engine's production
   `src/runtime/runtimePackageLoader.ts` **in place** with the engine's own
   bundler (rolldown) to `out/engine-runtime-package-loader.mjs`, so the Node
   test executes the real TypeScript module (vite-only `?url` asset imports
   are stubbed; the parity tests never reach the code paths that use them).
3. `parity.test.mjs` (run with `node --test`) compares the two sides:
   - engine `runtimePathUnitSegment` output must equal the exporter output for
     every shared vector input (plus `undefined` behaving like `null`);
   - engine `expectedRuntimeRoleIdentity(1..31)` must match the exporter table
     field-by-field, and stay `null` outside `1..31`;
   - every exporter-emitted scoped catalog (decoded with the production
     `engine/runtime-binary-codec.mjs`) must pass the engine's production
     `validateScopedRoleCatalog`, which recomputes the expected
     `roleRuntimePath` from the engine's own table + segment formula and
     throws on any drift in identity, path, versions, skin colors, or heights;
   - every exporter-emitted `parts/by-role/...` catalog path and
     `roles/...` role-runtime path must match the engine's runtime-metadata
     URL patterns (`isCacheableRuntimeMetadataUrl`), with a negative control.

The engine functions under test are exported from `runtimePackageLoader.ts`
solely for this harness (marked in the source); the exports change no runtime
behavior.

## Running

```sh
contract/parity/run.sh
```

The script resolves paths from its own location: it builds and runs the emit
harness, bundles the engine module, then runs the Node test; it exits nonzero
on any failure. It needs the `dotnet` CLI (any SDK able to target `net8.0` —
the emit project uses `<RollForward>LatestMajor</RollForward>` like the
round-trip harness; `exporter/global.json` only applies beneath `exporter/`),
Node, and `engine/node_modules` installed (`npm ci` in `engine/`).

## Contract-change rule

The unit-segment formula, the 31-role table, and the runtime package path
formulas are contracts between the exporter and the engine. Any change must
land as **one PR** that updates both sides and this suite together — and
`run.sh` must pass. Known limitation: the engine-side
`parts/by-role/<characterId>/<unit>/` *fetch* template lives inside the
network-bound `loadPartPackageSetFromBaseUrl` and is covered here only through
`isCacheableRuntimeMetadataUrl`'s URL patterns, not by executing the fetch
itself.
