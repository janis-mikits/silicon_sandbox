# Slice 4 timestamped scheduler progress — 5 October 2026

This is an intermediate core result, not slice 4 acceptance. The playable
world controls, packaged instances, renderer updates, and Unity test/build
gate remain unfinished.

`WorldSimulationScheduler` owns simulated time separately from Unity frames.
It queues source changes by timestamp in insertion order, applies all changes
at a slot before the scheduled world-clock transition, and then settles the
graph-driven circuit and SR storage. Advancing across a time range processes
each intervening edge; no camera or renderer state enters the scheduler.
It starts stopped at level 0, supports edge/cycle stepping, running frequency
changes after the current interval, stopped-phase preservation, Reset
Simulation, and a checked timestamp-limit pause. The stop/resume phase choice
is recorded in the [canonical clock rules](../docs/circuit-time-and-clock.md#clock-and-event-model)
and [decision log](first-playable-autonomous-decisions.md).

Independent NUnit expectations cover a source change at the exact first
rising edge, seven processed edges across an unrendered interval, manual
stepping, running and stopped frequency changes, reset, and timestamp
overflow. Their expected times and Q values are literals, not simulator
outputs used as oracles.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0, including `PASS: timestamped source-first slots and all
offscreen clock edges` and `PASS: clock stop/resume phase, frequency change,
and reset`. The NUnit suite and Unity build have not run for this change.
Convergence diagnostics, safe structural-edit orchestration, and a complete
scene-level offscreen demonstration remain unverified.
