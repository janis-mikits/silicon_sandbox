# Recoverable package file transaction progress

`AtomicPackageFilePublisher` prepares and validates three caller-supplied byte
streams before publication: the new fixed module definition, global library
index, and world archive carrying its inventory reference. A small flushed
journal records hashes of old and new files. On interruption, recovery keeps
the complete validated new set if all three committed; otherwise it restores
the prior world/index and removes the new definition. A rejected validation
leaves existing files unchanged. The publisher uses only a caller-provided
storage root; tests use `UnityProject/Temp` inside this repository.

Specification: `docs/modules-and-packaging.md` (atomic package operation and
library/world independence), `docs/first-playable-data-schema.md` (Global
library and recovery), and `docs/saving-and-recovery.md` (Consistent save and
recovery). This is file coordination only. The caller must pass strict V1
world, index, and definition validators and must not update live inventory or
announce success until `Publish` returns. Wiring it to in-game confirmation,
actual JSON bytes, and real load recovery remains unfinished.

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Result: 147 supplemental offline NUnit Edit Mode tests passed, including
repository-local file interruption cases. Unity Test Runner, integrated save
round trip, and player build remain unrun for this code while batch licensing
is blocked.
