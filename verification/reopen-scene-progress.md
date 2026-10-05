# In-memory scene reopen progress

The Unity bootstrap can now capture the current player position and look
direction into an authored world snapshot and reopen that snapshot in the
existing scene. Reopen replaces the circuit session and inventory, rebuilds
generated floor/walls and selectable graphics, resets interaction state, and
restores player pose. The ordinary source, SR, clock, event, and time state
comes from fresh simulation initialization, not the prior live circuit.

Specification: `docs/saving-and-recovery.md` (Saved design versus transient
simulation), `docs/delivery-and-acceptance.md` (Save/reopen acceptance check),
and `docs/first-playable-data-schema.md` (Player pose and authored design).

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

Result: 143 supplemental offline NUnit Edit Mode tests passed; Play Mode
sources compiled with zero warnings and errors. The new scene integration test
is compiled but has not run in Unity. This in-memory reopen is not the required
ZIP save/reopen demonstration; strict V1 JSON and durable file operations are
still unfinished, and batch licensing remains blocked.
