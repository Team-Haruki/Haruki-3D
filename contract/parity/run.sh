#!/usr/bin/env bash
# Cross-language parity contract checks beyond the ext-42 wire format:
# exporter production path/role-table formulas vs engine production formulas.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENGINE_DIR="$(cd "$SCRIPT_DIR/../../engine" && pwd)"
OUT_DIR="$SCRIPT_DIR/out"

mkdir -p "$OUT_DIR"

dotnet run --project "$SCRIPT_DIR/emit/ParityEmit.csproj" -c Release -- \
  "$SCRIPT_DIR/unit-segment-vector.json" \
  "$OUT_DIR"

(cd "$ENGINE_DIR" && node_modules/.bin/rolldown -c "$SCRIPT_DIR/rolldown.config.mjs")

node --test "$SCRIPT_DIR/parity.test.mjs"

echo "parity contract: PASS"
