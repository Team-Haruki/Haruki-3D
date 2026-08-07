#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project_root="$repo_root/unity/Haruki3DMV"
unity_editor="${UNITY_EDITOR:-/data/xy/.toolchains/unity-2022.3.62f2/Editor/Unity}"
unity_state="${HARUKI_UNITY_STATE_DIR:-/data/xy/.toolchains/unity-home}"
license_client="$(dirname "$unity_editor")/Data/Resources/Licensing/Client/Unity.Licensing.Client"
license_pipe="Haruki-Unity-LicenseClient-$$"

if [[ ! -x "$unity_editor" ]]; then
  echo "Unity Editor not found at: $unity_editor" >&2
  echo "Set UNITY_EDITOR to a Unity 2022.3.62f2 executable." >&2
  exit 1
fi
if [[ ! -x "$license_client" ]]; then
  echo "Unity Licensing Client not found at: $license_client" >&2
  exit 1
fi

export HOME="$unity_state/home"
export XDG_CACHE_HOME="$unity_state/cache"
export XDG_CONFIG_HOME="$unity_state/config"
export XDG_DATA_HOME="$unity_state/data"
mkdir -p "$HOME" "$XDG_CACHE_HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME"

mkdir -p "$project_root/TestResults"

# Use a per-process channel so concurrent builds never delete one another's
# licensing sockets. Batchmode does not start the licensing client itself.
"$license_client" --namedPipe "$license_pipe" --cloudEnvironment production &
license_pid=$!
cleanup_license_client() {
  kill "$license_pid" 2>/dev/null || true
  wait "$license_pid" 2>/dev/null || true
  rm -f \
    "/tmp/$license_pipe.sock" \
    "/tmp/$license_pipe-notifications.sock" \
    "/tmp/.dotnet/shm/global/Unity.Licensing.Client.Pipe.$license_pipe"
}
trap cleanup_license_client EXIT

for _ in $(seq 1 50); do
  [[ -S "/tmp/$license_pipe.sock" ]] && break
  kill -0 "$license_pid" 2>/dev/null || break
  sleep 0.1
done
if [[ ! -S "/tmp/$license_pipe.sock" ]]; then
  echo "Unity Licensing Client did not open $license_pipe." >&2
  exit 1
fi

"$unity_editor" \
  -batchmode \
  -nographics \
  -licensingIpc "$license_pipe" \
  -projectPath "$project_root" \
  -runTests \
  -testPlatform EditMode \
  -testResults "$project_root/TestResults/editmode.xml" \
  -logFile -

"$unity_editor" \
  -batchmode \
  -nographics \
  -licensingIpc "$license_pipe" \
  -quit \
  -projectPath "$project_root" \
  -executeMethod Haruki.MV.Editor.BuildWebGL.PerformBuild \
  -logFile -
