# First-playable benchmark fixture progress

`FirstPlayableBenchmarkFactory.Create()` builds the prescribed mixed reference
design in memory: 500 individually placed one-bit AND gates, ten instances of
one fixed version containing 50 AND gates, one configured Constant Logic Source,
1,011 authored world connectors, and 1,013 visible world connector spans. The
world clock drives each gate's A input; the source holds the first world gate's
B input at 1. Other B inputs are open, so expected output changes at successive
clock edges are 0→X→0, while the source-driven gate changes 0→1→0. Its spatial
layout is currently 128 × 80 × 6 cells, with world gates in ten rows and ten
module footprints along the eastern side. The test checks the 1,000 expanded
runtime gate bindings and those independent four-state expectations.

Specification: `docs/performance-and-platforms.md` (Benchmark) and
`docs/digital-simulation.md` (Four-state behavior). The saved reference world
file, its fixed hash, camera path, graphics preset record, timed Mac runs, edit
latencies, save/load latencies, and Windows checks are not yet available. This
factory is not a performance pass and is not yet the final frozen artifact.

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Result: 139 supplemental offline NUnit Edit Mode tests passed. Unity Test Runner,
player build, and timed FPS/throughput measurements remain unverified for this
slice because Codex-launched batchmode cannot connect to the Licensing Client.
