#!/usr/bin/env bash
set -euo pipefail
diagnostic_native=0
if [[ "${1:-}" == --diagnostic-native && $# == 1 ]]; then
  diagnostic_native=1
elif (($#)); then
  echo 'Usage: scripts/benchmark-mac.sh [--diagnostic-native]' >&2
  exit 2
fi

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
player="$repo_root/UnityProject/Builds/macOS/SiliconSandbox.app/Contents/MacOS/SiliconSandbox"
logs="$repo_root/UnityProject/Logs/Verification"
report="$logs/benchmark-mac-reference-$(date -u +%Y%m%dT%H%M%SZ).txt"
[[ "$(uname -s)" == Darwin ]] || { echo 'This benchmark requires macOS.' >&2; exit 2; }
[[ -x "$player" ]] || { echo 'Build the current macOS player first.' >&2; exit 2; }
mkdir -p "$logs"
cd "$repo_root"
env -u SILICON_SANDBOX_SMOKE_OUTPUT \
  SILICON_SANDBOX_BENCHMARK_NATIVE_DIAGNOSTIC="$diagnostic_native" \
  SILICON_SANDBOX_BENCHMARK_OUTPUT="$report" \
  "$player" -logFile "$logs/benchmark-mac-player.log"
[[ -s "$report" ]] || { echo 'Benchmark report was not produced.' >&2; exit 1; }
rg -q '^REFERENCE MEASUREMENT COMPLETE$' "$report" || {
  echo 'Graphical benchmark did not complete; inspect the report and player log.' >&2
  exit 1
}
python3 "$repo_root/scripts/check-benchmark-report.py" "$report" "$diagnostic_native"
echo "Reference graphical benchmark report: $report"
