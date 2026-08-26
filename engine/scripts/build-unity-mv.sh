#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project_root="$repo_root/unity/Haruki3DMV"
unity_editor="${UNITY_EDITOR:-/data/xy/.toolchains/unity-2022.3.62f2/Editor/Unity}"
unity_state="${HARUKI_UNITY_STATE_DIR:-/data/xy/.toolchains/unity-home}"
unity_log_dir="${HARUKI_UNITY_LOG_DIR:-$project_root/Logs}"
editmode_log="$unity_log_dir/editmode.log"
build_log="$unity_log_dir/build-webgl.log"
license_client="$(dirname "$unity_editor")/Data/Resources/Licensing/Client/Unity.Licensing.Client"
license_user="${USER:-$(id -un)}"
license_pipe="LicenseClient-${license_user}-2022.3.62"
editor_license_pipe="Unity-${license_pipe}"
license_socket="/tmp/$license_pipe.sock"
license_shm="/tmp/.dotnet/shm/global/Unity.Licensing.Client.Pipe.$license_pipe"
license_lock="/tmp/haruki-unity-license-${license_user}-2022.3.62.lock"

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

mkdir -p "$project_root/TestResults" "$unity_log_dir"

# Unity 2022.3 only discovers the versioned per-user licensing channel. Keep
# one owner at a time, while allowing an already-running Hub client to be
# reused without killing or deleting its IPC state.
exec 9>"$license_lock"
flock 9
license_pid=""
if ! pgrep -f -- "$license_client --namedPipe $license_pipe" >/dev/null; then
  rm -f "$license_socket" \
    "/tmp/$license_pipe-notifications.sock" \
    "$license_shm"
  "$license_client" --namedPipe "$license_pipe" --cloudEnvironment production &
  license_pid=$!
fi
cleanup_license_client() {
  if [[ -n "$license_pid" ]]; then
    kill "$license_pid" 2>/dev/null || true
    wait "$license_pid" 2>/dev/null || true
    rm -f \
      "$license_socket" \
      "/tmp/$license_pipe-notifications.sock" \
      "$license_shm"
  fi
}
trap cleanup_license_client EXIT

for _ in $(seq 1 50); do
  [[ -S "$license_socket" || -e "$license_shm" ]] && break
  [[ -z "$license_pid" ]] || kill -0 "$license_pid" 2>/dev/null || break
  sleep 0.1
done
if [[ ! -S "$license_socket" && ! -e "$license_shm" ]]; then
  echo "Unity Licensing Client did not open $license_pipe." >&2
  exit 1
fi
# CoreCLR publishes its shared-memory marker just before the named-pipe
# listener starts accepting clients. Give that listener one scheduler turn so
# Unity cannot race the licensing process during batch startup.
sleep 0.5

if ! "$unity_editor" \
  -batchmode \
  -nographics \
  -licensingIpc "$editor_license_pipe" \
  -projectPath "$project_root" \
  -runTests \
  -testPlatform EditMode \
  -testResults "$project_root/TestResults/editmode.xml" \
  -logFile "$editmode_log"; then
  tail -n 200 "$editmode_log" >&2
  exit 1
fi
echo "Unity EditMode log: $editmode_log"

if [[ "${HARUKI_UNITY_TEST_ONLY:-0}" == "1" ]]; then
  exit 0
fi

if ! "$unity_editor" \
  -batchmode \
  -nographics \
  -licensingIpc "$editor_license_pipe" \
  -quit \
  -projectPath "$project_root" \
  -executeMethod Haruki.MV.Editor.BuildWebGL.PerformBuild \
  -logFile "$build_log"; then
  grep -nE "Exception|error CS|Failed|not safe to publish" "$build_log" | tail -n 80 >&2 || true
  tail -n 200 "$build_log" >&2
  exit 1
fi
if grep -qE '^Shader error|error CS[0-9]+:|not safe to publish' "$build_log"; then
  echo "Unity reported compiler errors despite returning success:" >&2
  grep -nE '^Shader error|error CS[0-9]+:|not safe to publish' "$build_log" >&2
  exit 1
fi
echo "Unity WebGL build log: $build_log"
