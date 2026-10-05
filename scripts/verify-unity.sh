#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo 'Usage: scripts/verify-unity.sh --editor /path/to/Unity --target StandaloneOSX|StandaloneWindows64' >&2
  exit 2
}

editor=''
target=''
while (($#)); do
  case "$1" in
    --editor) [[ $# -ge 2 ]] || usage; editor=$2; shift 2 ;;
    --target) [[ $# -ge 2 ]] || usage; target=$2; shift 2 ;;
    *) usage ;;
  esac
done
[[ -x "$editor" ]] || usage
[[ "$target" == StandaloneOSX || "$target" == StandaloneWindows64 ]] || usage

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
project="$repo_root/UnityProject"
version=$(sed -n 's/^m_EditorVersion: //p' "$project/ProjectSettings/ProjectVersion.txt")
[[ "$version" == 6000.3.24f1 ]] || { echo "Unexpected project Editor version: $version" >&2; exit 1; }
[[ "$editor" == *"/$version/"* ]] || { echo "Editor path does not match $version" >&2; exit 1; }
if [[ "$target" == StandaloneOSX ]]; then
  [[ "$(uname -s)" == Darwin ]] || { echo 'macOS build must run on macOS' >&2; exit 1; }
else
  echo 'Windows target uses the PowerShell wrapper on Windows.' >&2
  exit 2
fi

logs="$project/Logs/Verification"
mkdir -p "$logs"
export SILICON_SANDBOX_TEST_ROOT="$project/Temp/VerificationTests"
mkdir -p "$SILICON_SANDBOX_TEST_ROOT"
export UPM_CACHE_ROOT="$project/.upm-cache"
run_editor() {
  "$editor" -batchmode -projectPath "$project" -buildTarget "$target" "$@"
}

echo "Editor: $version; target: $target"
run_editor -quit -executeMethod SiliconSandbox.EditorBuild.SliceZeroBuild.EnsureScene -logFile "$logs/scene.log"
[[ -s "$project/Assets/SiliconSandbox/Scenes/FlatWorld.unity" ]] || { echo 'Smoke scene was not generated.' >&2; exit 1; }

run_editor -runTests -testPlatform EditMode -testResults "$logs/editmode.xml" -logFile "$logs/editmode.log"
run_editor -runTests -testPlatform PlayMode -testResults "$logs/playmode.xml" -logFile "$logs/playmode.log"

python3 - "$logs/editmode.xml" "$logs/playmode.xml" <<'PY'
import sys
import xml.etree.ElementTree as ET
for file in sys.argv[1:]:
    root = ET.parse(file).getroot()
    total = int(root.get('total', root.get('testcasecount', '0')))
    failed = int(root.get('failed', '0'))
    inconclusive = int(root.get('inconclusive', '0'))
    skipped = int(root.get('skipped', '0'))
    print(f'{file}: total={total}, failed={failed}, inconclusive={inconclusive}, skipped={skipped}')
    if total < 1 or failed or inconclusive or skipped:
        raise SystemExit(1)
PY

run_editor -quit -executeMethod SiliconSandbox.EditorBuild.SliceZeroBuild.BuildPlayer -logFile "$logs/build.log"
[[ -d "$project/Builds/macOS/SiliconSandbox.app" ]] || { echo 'macOS player build is missing.' >&2; exit 1; }
player="$project/Builds/macOS/SiliconSandbox.app/Contents/MacOS/SiliconSandbox"
[[ -x "$player" ]] || { echo 'macOS player executable is missing.' >&2; exit 1; }
SILICON_SANDBOX_SMOKE_OUTPUT="$logs/player-smoke.txt" "$player" -batchmode -nographics -logFile "$logs/player.log"
[[ "$(cat "$logs/player-smoke.txt")" == PASS ]] || { echo 'Built-player smoke check failed.' >&2; exit 1; }
echo 'Unity Edit Mode, Play Mode, macOS build and player checks passed.'
