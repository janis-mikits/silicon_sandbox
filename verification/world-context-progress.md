# Authored world context progress

`OneBitWorldContext` now carries stable world identity/name, generated floor
and wall style IDs, the circuit session, and the 36-slot player inventory.
Capturing an authored snapshot also accepts a normalized player pose; opening
that snapshot retains identity, bounds, settings, inventory, and placed-object
IDs while ordinary source state, clock level/running state, and simulation time
restart under the accepted rules. The playable bootstrap uses this context.
The new default generated-style IDs are recorded in the version 1 schema and
the autonomous decision log.

Specification: `docs/saving-and-recovery.md` (Saved design versus transient
simulation), `docs/first-playable-data-schema.md` (World archive), and
`docs/project-vision-and-world.md` (generated world settings).

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

Result: 143 supplemental offline NUnit Edit Mode tests passed; Play Mode
sources compiled with zero warnings and errors. This is an in-memory snapshot
boundary, not a JSON/ZIP file round trip. Unity tests and player build for
this slice remain unrun because batch licensing is blocked.
