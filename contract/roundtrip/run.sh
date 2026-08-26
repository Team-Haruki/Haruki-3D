#!/usr/bin/env bash
# Cross-language round-trip contract test:
# exporter production MessagePack/ext-42 encoder -> engine production decoder.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="$SCRIPT_DIR/out"

mkdir -p "$OUT_DIR"

dotnet run --project "$SCRIPT_DIR/emit/RoundtripEmit.csproj" -c Release -- \
  "$SCRIPT_DIR/fixture-manifest.json" \
  "$OUT_DIR/fixture.msgpack.br"

node --test "$SCRIPT_DIR/decode.test.mjs"

echo "roundtrip contract: PASS"
