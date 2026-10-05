# Authored one-bit placement progress — 5 October 2026

This is core groundwork, not slice 2 acceptance. The Unity ghost, inventory,
pin targeting, right-click placement, connector routing, Configure panel,
rotation confirmation, and full invalid-action feedback are still unfinished.

`OneBitWorldDesign` owns one-cell source, AND, and SR placement records with
stable object/pin IDs, exact versioned pin locations, orientation, source
startup configuration, and optional initial SR Q. It validates saved pin
snapshots, occupied cells, world bounds, and component/connector overlap.
`OneBitWorldEdits.PlaceComponent` and `PlaceConnector` build complete
candidate revisions and reject invalid edits before publication. `OneBitWorldSession` reaches a
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

`PlaceConnector` accepts a connector route and its explicit joins in one
transaction. Geometric contact alone leaves a pin electrically unconnected.
The new NUnit cases check a source-to-AND wire with and without joins,
propagation after breaking that wire, an atomic rejection when a second
connector targets an occupied pin, and two crossing channels that remain
separate until an explicit center junction is added.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0, including `PASS: atomic source/AND placement and occupied-cell
rejection` and `PASS: safe authored revision publish preserves unrelated
simulation`. The connector extension also exited 0 with `PASS: explicit
connector joins and authored break propagation`. `git diff --check` exited
0. The NUnit suite and Unity build have not run for these changes.

The source Configure foundation now publishes saved On value and initial
On/Off at one safe revision. It keeps the source's current transient On/Off
choice, updates the live drive when appropriate, and preserves unrelated SR
state. An independent NUnit case expects an On source reconfigured to Z to
release its net, then expects Reset Simulation to return it to its newly
configured Off state and drive 0. A second case rejects configuring an AND
gate without publishing any revision. The autonomous game decision is
recorded in [components and timing](../docs/components-and-rtl-timing.md#sources-on-load-and-reset).

The same offline command above exited 0 with `PASS: authored source
configuration and ordinary reset`; `git diff --check` exited 0. After the
user opened the project in Unity Hub, I retried:

```sh
./scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

The Editor again stalled at Licensing Client channel initialization after
assembly reload. I interrupted the stalled wrapper (exit 130). This attempt
did not run Edit Mode tests, Play Mode tests, or a build and supplies no Unity
pass claim for the newer code.

The source-body action now toggles only live state. Its NUnit case expects
settled output 1 then 0 during a safe pause, and 1 again after resuming
simulation while the world clock remains stopped. Authored design, revision,
and simulated time remain unchanged. The offline command above exited 0 with
`PASS: live source toggles while paused or running with clock stopped`.
