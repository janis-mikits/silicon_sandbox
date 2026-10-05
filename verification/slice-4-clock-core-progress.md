# Slice 4 clock-time core progress — 5 October 2026

This is intermediate core work, not scheduler or slice 4 acceptance.

`SimulationTime` stores a checked `(ulong seconds, picosecondsWithinSecond)`
pair. `WorldClockEdgeSchedule` parses an exact decimal frequency and computes
each alternating 50-percent-duty edge from its absolute rational index. The
first-playable range remains 0.1–100 Hz; a separate precision-check mode
demonstrates representable 500 ps half-periods for a possible later 1 GHz
clock. Exact half-picosecond ties use the approved nearest-even rule in the
[canonical clock document](../docs/circuit-time-and-clock.md#first-playable-scheduler-contract).

NUnit expectations independently specify 10 Hz and 3 Hz edge timestamps,
3 Hz edge 6000 at exactly 1000 seconds, nearest-even ties at 32.768 Hz,
the later 1 GHz precision example, range/syntax rejection, edge-collapse
rejection, and checked overflow.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0; the harness reported `PASS: absolute rational clock edge times
and future 1 GHz precision` along with the preceding core checks. The NUnit
suite and Unity build remain unrun for this change. An event queue, safe
pause, same-timestamp ordering, frequency-change phase behavior, world-clock
controls, and actual SR integration remain to be implemented and verified.
