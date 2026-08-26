# Cross-language round-trip contract test

This test proves that the exporter's **production** MessagePack/ext-42 encoder
(`exporter/Services/RuntimeJsonWriter.cs`) and the engine's **production**
decoder (`engine/runtime-binary-codec.mjs`) agree on the semantics of the
`.msgpack.br` runtime package format. It is the seam the monorepo exists to
protect: the C# side emits a fixture through the real writer, the Node side
decodes it through the real codec, and both compare against the same
`fixture-manifest.json` so the two halves cannot drift apart.

## How it works

1. `emit/` is a small console project that compiles
   `../../exporter/Services/RuntimeJsonWriter.cs` **in place** via
   `<Compile Include>` (the same direct-inclusion pattern
   `exporter/Haruki-3D-Exporter.csproj` uses for its
   `HARUKI_EXPORTER_CONFIG_TEST` mode), so the bytes are produced by the
   production encoder — including its Brotli path (quality 6, window 22). No
   code is copied. It reads the `document` object from `fixture-manifest.json`,
   converts it to a plain CLR object graph (dictionaries, lists, longs,
   doubles — the same object-graph branches the production exporters feed), and
   calls `RuntimeJsonWriter.Write(..., RuntimeBinaryArraySchema.PartRuntime)`
   to produce `out/fixture.msgpack.br`.
2. `decode.test.mjs` (run with `node --test`) Brotli-decompresses the fixture
   with `node:zlib`, decodes it with `decodeRuntimeMessagePack` imported from
   `../../engine/runtime-binary-codec.mjs`, and asserts every value against the
   same manifest. Float32 expectations are computed as `Math.fround(...)` of
   the manifest doubles; `-0` is checked with `Object.is`.

## What the fixture exercises

- **ext-42 float32 array** (`nativeMeshes.meshes.positions`): `0`, `-0`, `1`,
  `-1`, `0.5`, `3.14159274`, `1e-7`, `3.4028235e38` — exact values, rounding
  cases, sign of zero, and max-float magnitude.
- **ext-42 uint16 index array** (`nativeMeshes.meshes.skinIndices`): spans
  `0`..`65535`, stays narrow.
- **ext-42 uint32 index array** (`nativeMeshes.meshes.submeshes.indices`):
  contains `65536` and `4294967295`, forcing the wide encoding.
- **Empty array on an ext path** (`nativeMeshes.meshes.normals`): the
  production encoder only ext-encodes arrays of at least 8 floats (or 16
  indexes), so empty and below-threshold arrays — the encoder never emits an
  empty ext-42 payload — stay ordinary MessagePack arrays; the test pins that.
- **Name collision on an unrelated schema** (`unrelated.positions`, and
  `clips.tracks.times` which is a UnityMotion path serialized under the
  PartRuntime schema): per the rule documented in `exporter/README.md`
  ("Unrelated arrays with the same property names remain ordinary MessagePack
  arrays"), they decode as plain arrays that keep full float64 precision.
- **Surrounding structure**: nested maps, ASCII and non-ASCII strings, nulls,
  bools, and integers at the fixnum/int32/uint32/uint64 boundaries.

## Running

```sh
contract/roundtrip/run.sh
```

The script resolves paths from its own location, builds and runs the emit
harness, then runs the Node test; it exits nonzero on any failure. It needs the
`dotnet` CLI (any SDK able to target `net8.0`), Node, and
`engine/node_modules` installed (`npm ci` in `engine/`) for
`@msgpack/msgpack`.

The emit project targets `net8.0` — the same target framework as the exporter —
with `<RollForward>LatestMajor</RollForward>` so machines that only carry a
newer SDK/runtime (dev machines without an 8.x SDK; CI installs SDK 8) can
still build it against the NuGet reference packs and run it on whatever
runtime is present. `exporter/global.json` (SDK 8 pin) only applies beneath
`exporter/` and does not affect this project.

## Contract-change rule

The `.msgpack.br` format (extension type 42, its payload layouts, the
schema-scoped path lists, and the size thresholds) is a contract between the
exporter and the engine. Any change to it must land as **one PR** that updates
the format documentation (`exporter/README.md`), the exporter writer, the
engine decoder, and this round-trip test (`fixture-manifest.json` +
`decode.test.mjs`) together — and `run.sh` must pass.
