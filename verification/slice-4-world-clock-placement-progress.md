# World-clock stub placement progress — 5 October 2026

The playable hotbar now offers a reserved world-clock Net Link. Right-clicking
an SR CLK pin with that item selected publishes a visible one-bit stub, a
physical channel, a stable route/node identity, and one explicit pin join at
a safe authored revision. An available adjacent cell is used for the stub;
at a world boundary it can sit on the exact component pin face. Separate
stubs remain separate physical connectors while their reserved
`@world-clock` identity joins their derived electrical clock net. A second
connector on an occupied CLK pin is rejected without a partial revision.

Independent NUnit expectations cover two SR CLK pins reading the initial 0
and the settled 1 after one 10 Hz manual edge, distinct authored stub IDs,
and the world-boundary face/occupied-pin cases. A Play Mode test is written
to check a selectable rendered stub and settled clock value, but has not run.

Executed offline with temporary SDK state under
`UnityProject/Temp/OfflineVerification`, no downloads:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

Results: 106 pure Edit Mode NUnit cases passed, 0 failed; Play Mode source
compiled with 0 warnings and 0 errors. These are not Unity Test Runner
results. The Unity Editor wrapper remains blocked at its Licensing Client
channel; no new Play Mode or native player pass is claimed.
