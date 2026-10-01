#!/usr/bin/env bash
# dotnet-sonarscanner must wrap the exporter build, so this scan cannot use sonar.yml.
# Ported from the old sonar.yml, except that the engine is no longer rebuilt and retested
# here: the engine lcov comes from the Engine job (artifact coverage-js). When that job
# was path-filtered out, the scan runs without JS coverage.
# Env: SONAR_TOKEN, ASSETSTUDIO_ROOT (prepared by exporter/scripts/prepare-assetstudio.sh).
# Usage: scripts/ci/sonar-dotnet.sh [engine-lcov]   (default coverage/lcov-js.info)
set -euo pipefail
cd "$(dirname "$0")/../.."
: "${SONAR_TOKEN:?}" "${ASSETSTUDIO_ROOT:?}"
lcov="${1:-coverage/lcov-js.info}"
scanner_version="${SONAR_SCANNER_VERSION:-11.3.0}"
dotnet tool update dotnet-sonarscanner --version "$scanner_version" --tool-path "$RUNNER_TEMP/scanner"
js_args=()
if [ -f "$lcov" ]; then
  # lcov paths are relative to engine/, where the old workflow generated the report
  mkdir -p engine/coverage && cp "$lcov" engine/coverage/lcov.info
  js_args=("/d:sonar.javascript.lcov.reportPaths=engine/coverage/lcov.info")
fi
"$RUNNER_TEMP/scanner/dotnet-sonarscanner" begin /k:"Team-Haruki_Haruki-3D" /o:"team-haruki" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.exclusions="**/node_modules/**,**/dist/**,**/bin/**,**/obj/**" \
  /d:sonar.coverage.exclusions="exporter/**,engine/capture-server.mjs,engine/src/runtime/**,engine/src/engine/Haruki3DEngine.ts,engine/scripts/**,engine/examples/**,engine/check-*.mjs,engine/format-*.mjs,engine/inspect-*.mjs,engine/playwright*.mjs,engine/vite*.ts,engine/*.d.mts" \
  "${js_args[@]}"
dotnet build exporter/Haruki-3D-Exporter.csproj -p:AssetStudioRoot="$ASSETSTUDIO_ROOT"
"$RUNNER_TEMP/scanner/dotnet-sonarscanner" end /d:sonar.token="$SONAR_TOKEN"
