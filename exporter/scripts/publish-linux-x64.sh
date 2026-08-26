#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
OUTPUT_DIR="${1:-/tmp/haruki-3d-exporter-linux-x64}"
ASSETSTUDIO_ROOT="${ASSETSTUDIO_ROOT:-/tmp/haruki-assetstudio}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

export ASSETSTUDIO_ROOT DOTNET_BIN

cd "${REPO_ROOT}"
bash scripts/prepare-assetstudio.sh

"${DOTNET_BIN}" restore \
  -r linux-x64 \
  -p:AssetStudioRoot="${ASSETSTUDIO_ROOT}" \
  -p:RestoreConfigFile=NuGet.Config

rm -rf "${OUTPUT_DIR}"
mkdir -p "${OUTPUT_DIR}"
"${DOTNET_BIN}" publish -c Release -r linux-x64 \
  --self-contained true \
  --no-restore \
  -o "${OUTPUT_DIR}" \
  -p:AssetStudioRoot="${ASSETSTUDIO_ROOT}"

chmod +x "${OUTPUT_DIR}/Haruki-3D-Exporter"
printf '%s\n' "${OUTPUT_DIR}"
