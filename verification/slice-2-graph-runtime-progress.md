# Slice 2 graph/runtime progress — 5 October 2026

This is an intermediate report, not a slice acceptance result. Player placement,
junction targeting, and Configure are still unfinished. A targeted left-click
break in the development scene is now wired through the atomic edit path.

The authored one-bit topology now projects through `OneBitCircuitPlanBuilder`
into transient net indexes and source/AND bindings. `GraphDrivenOneBitCircuit`
settles source and gate work without Unity frame updates, preserves source
runtime state across a settled graph replacement, and reads a disconnected
input as Z. `OneBitCircuitInspection` reads the same graph and simulator values.
The development AND scene uses this path for its labels, colors, and Inspect
values. Left-clicking a span validates and publishes a break, redraws the
surviving authored routes, and updates settled signal colors. A new Play Mode
test checks the visible break and independent source-side signal. Fixed fixture
wiring remains only as a separate earlier test path.

Independent NUnit expectations added: an empty world has no invented nets;
all 16 ordered AND pairs use a literal expected table; breaking B's wire
changes B to Z and Y to X while the unrelated source remains on; a subsequent
live source change drives Y to 0; a missing component pin is rejected; and
Inspect distinguishes a released B net from an uncertain AND output.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0; three harness checks passed, including the graph-fed break and
live propagation. The harness and SDK cache are ignored, repository-local
verification tools. `git diff --check` passed. This is not Unity test or player
build evidence. The new NUnit Edit Mode tests, Play Mode scene test, and macOS
build/player smoke await an actual licensed Editor run. Previous slice 0/1
Unity results do not cover this change. Windows native, CI, and performance
acceptance remain unverified.
