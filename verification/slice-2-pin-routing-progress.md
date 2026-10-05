# One-bit two-pin route planning progress — 5 October 2026

This is a core authoring helper, not a finished player wire tool or slice 2
acceptance. A caller chooses two free component pins; the planner finds a path
through empty cells, selects an unoccupied channel, records face-quadrant
nodes and spans, and returns two explicit pin joins. It validates the complete
candidate before the session publishes one safe authored revision. The
underlying route and joins, rather than the pathfinder or its rendered shape,
determine connectivity. A blocked path leaves the design and revision
unchanged.

The new NUnit cases expect a source-to-AND route to settle A = 0, expect a
visible in-cell quadrant shift from source OUT to SR R, and expect a
four-channel blocked corridor to reject without a partial edit. The route
planner does not yet handle adjacent component pins with no free routing
cell, branching to an existing connector, or the full freeform player
interface. Those remain slice 2 work.

Executed with the approved offline .NET SDK, no downloads, and temporary CLI
home, package cache, and projects under `UnityProject/Temp/OfflineVerification`:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/CoreCompile/CoreCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Both exited 0. The core harness included `PASS: two-pin route with exact
quadrant shift and explicit joins`. The offline NUnit harness executed 102
pure Edit Mode cases with 0 failures; it excludes the Unity Editor scene
test. It compiled existing Unity presentation code against the installed
Editor reference assemblies and exposed an older NUnit collection assertion
that did not compile; the assertion now checks both replacement IDs directly.
This is offline evidence, not a Unity Editor Test Runner or Play Mode result.
The Unity wrapper retry is recorded in
[authored placement progress](slice-2-authored-placement-progress.md).
