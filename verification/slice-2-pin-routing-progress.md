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
visible in-cell quadrant shift from source OUT to SR R, expect a
four-channel blocked corridor to reject without a partial edit, verify an
adjacent face bridge connects only after explicit placement, and reject a
connector node hidden inside a component cell without its pin join. The route
planner now also builds an explicit short face bridge for aligned adjacent
pins; mere physical contact still leaves them disconnected. A route node may
occupy a component cell only at an exact attached pin face, and no span may
cross that component's interior. A later extension routes from a free pin
to a specifically targeted existing connector node and publishes an explicit
join; its NUnit case expects separation first, then connectivity and 0 after
the edit. Targeting a connector span between nodes, overlapping-part cycling,
the full freeform player interface, and final junction/crossing visuals remain
slice 2 work.

Executed with the approved offline .NET SDK, no downloads, and temporary CLI
home, package cache, and projects under `UnityProject/Temp/OfflineVerification`:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/CoreCompile/CoreCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

All exited 0. The core harness included `PASS: two-pin route with exact
quadrant shift and explicit joins`. The offline NUnit harness executed 102
pure Edit Mode cases with 0 failures at that stage; after adding adjacent
face-bridge and invalid face-node cases, the same offline runner executed
104 cases with 0 failures. The targeted-node extension raised that count to
107 with 0 failures, and the offline Play Mode source compile exited 0 with
0 warnings or errors. It excludes the Unity Editor scene
test. It compiled existing Unity presentation code against the installed
Editor reference assemblies and exposed an older NUnit collection assertion
that did not compile; the assertion now checks both replacement IDs directly.
This is offline evidence, not a Unity Editor Test Runner or Play Mode result.
The Unity wrapper retry is recorded in
[authored placement progress](slice-2-authored-placement-progress.md).
