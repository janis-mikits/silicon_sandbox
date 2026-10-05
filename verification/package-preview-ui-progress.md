# Package selection and configuration preview progress

R and T select inclusive world-cell corners, with a translucent region preview.
Enter safely pauses and extracts an independent draft. The configuration
preview lists six exterior faces, exact exposed endpoint mappings, editable
port names and directions, a mapping cycle, face relocation, and removal.
Validation rejects duplicate or invalid fixed ports without publishing a
definition or changing the source circuit. Closing the preview returns to the
world. Durable Confirm, global library/inventory publication, and the final
rotatable 3D module preview remain unfinished.

Specification: `docs/modules-and-packaging.md` (Packaging region and ports,
Selection and package configuration), `docs/building-and-interface.md`
(Default controls), and `docs/circuit-time-and-clock.md` (Safe pause and editing).

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

Result: 142 supplemental offline NUnit Edit Mode tests passed; Play Mode
sources compiled with zero warnings and errors. Unity Test Runner and visual
UI checks remain unrun for newer code due to the batch Licensing Client issue.
