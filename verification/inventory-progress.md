# First-playable inventory progress

The freeplay inventory now owns exactly 36 typed slots and a selected hotbar
index. Its first five catalog entries use stable version 1 IDs for Source, Wire,
AND, SR, and World Clock Link; UI labels are separate. A fixed module reference
uses exact family/version IDs and takes the first free slot without duplication.
The Unity hotbar and inventory display read this model. Package publication
must still make the library definition and inventory reference durable together
before calling `AddModuleVersion` in the player flow.

Specification: `docs/building-and-interface.md` (Inventory and component
settings) and `docs/first-playable-data-schema.md` (World archive). The new
stable connector tool IDs are recorded in
`verification/first-playable-autonomous-decisions.md`.

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

Result: 141 supplemental offline NUnit Edit Mode tests passed; Play Mode
sources compiled with zero warnings and errors. Unity Test Runner and built
player checks remain unrun for this code because Codex-launched Unity batchmode
still cannot connect to its Licensing Client.
