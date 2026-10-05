# Slice 4 clock-time core progress — 5 October 2026

This is intermediate core work, not scheduler or slice 4 acceptance.

`SimulationTime` stores a checked `(ulong seconds, picosecondsWithinSecond)`
pair. `WorldClockEdgeSchedule` parses an exact decimal frequency and computes
each alternating 50-percent-duty edge from its absolute rational index. The
first-playable range remains 0.1–100 Hz; a separate precision-check mode
demonstrates representable 500 ps half-periods for a possible later 1 GHz
clock. Exact half-picosecond ties use the approved nearest-even rule in the
[canonical clock document](../docs/circuit-time-and-clock.md#first-playable-scheduler-contract).

The version 1 special world-clock `netLink` now participates in derived-net
construction. Remote stubs with the reserved name/scope share a net, and the
graph execution plan drives that net from a clock level independent of scene
visibility. A span break retains link identity only on the piece containing
the first saved route node; the other piece becomes an ordinary wire, as
recorded in the [version 1 schema](../docs/first-playable-data-schema.md#connector-geometry-and-joins).

NUnit expectations independently specify 10 Hz and 3 Hz edge timestamps,
3 Hz edge 6000 at exactly 1000 seconds, nearest-even ties at 32.768 Hz,
the later 1 GHz precision example, range/syntax rejection, edge-collapse
rejection, and checked overflow.
Further NUnit expectations cover remote clock-stub connectivity, reserved
field validation, a split that truly disconnects, and a same-slot source
change settling before a world-clock edge samples SR storage.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0; the harness reported `PASS: absolute rational clock edge times
and future 1 GHz precision`, `PASS: scoped world-clock links and
anchor-preserving break`, and `PASS: same-slot source value precedes
world-clock SR sample` along with the preceding core checks. A new NUnit
case covers a newly placed SR that observes an already-high CLK as
its baseline, then responds to the next real rising edge. The offline harness
reported `PASS: newly placed SR baselines an already-high CLK`. The NUnit
suite and Unity build remain unrun for this change. A timestamped event queue,
safe pause, complete same-timestamp ordering, frequency-change phase behavior,
and world-clock scene controls remain to be implemented and verified.
