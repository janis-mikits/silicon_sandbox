# Authored one-bit placement progress — 5 October 2026

This is core groundwork, not slice 2 acceptance. The Unity ghost, inventory,
pin targeting, right-click placement, connector routing, Configure panel,
rotation confirmation, and full invalid-action feedback are still unfinished.

`OneBitWorldDesign` owns one-cell source, AND, and SR placement records with
stable object/pin IDs, exact versioned pin locations, orientation, source
startup configuration, and optional initial SR Q. It validates saved pin
snapshots, occupied cells, world bounds, and component/connector overlap.
`OneBitWorldEdits.PlaceComponent` builds a complete candidate revision and
rejects invalid placement before publication. `OneBitWorldSession` reaches a
settled pause boundary, rebuilds the derived graph and simulator from that
candidate, and publishes the authored design, graph, and revision together.
An unrelated placement preserves a live source's current On/Off state.

The first-playable occupied-cell rule required a game decision and is recorded
in [physical connections](../docs/physical-connections.md#pins-and-targeting)
and the [autonomous decision log](first-playable-autonomous-decisions.md).

Independent NUnit cases check source configuration and exact pins, yawed AND
pin positions, disconnected Z inputs, occupied/floor/out-of-bounds/wire-cell
rejection, corrupted pin snapshots, safe publication, and unchanged design
and simulation after rejection.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0, including `PASS: atomic source/AND placement and occupied-cell
rejection` and `PASS: safe authored revision publish preserves unrelated
simulation`. The NUnit suite and Unity build have not run for this change.
