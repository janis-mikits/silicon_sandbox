# Module hotbar placement progress

When a 36-slot inventory contains an exact module-version item and the active
session has that fixed definition, selecting its hotbar slot now shows the
complete rotated footprint and exterior ports. Validity checks use the same
all-or-nothing authored placement operation as confirmation; obstructed cells
show a red full-footprint preview. Right-click creates a new placed object and
instance identity with a unique display name. The fixed version remains shared
and simulation state remains instance-scoped.

Specification: `docs/modules-and-packaging.md` (Placement and independent
instances), `docs/building-and-interface.md` (Placement and movement), and
`docs/physical-connections.md` (Post placement rotation). Global publication
and player-driven package configuration are separate unfinished work.

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

Result: 141 supplemental offline NUnit Edit Mode tests passed; Play Mode
sources compiled with zero warnings and errors. Unity Play Mode and visual
placement checks still need a real Editor run; batch licensing remains blocked.
