# First-playable rotation progress

The Z/X interaction now offers a vertical-axis rotation preview for placed
components and modules. It auto-pauses, shows candidate geometry and pin/port
locations, highlights connector joins that will be lost, and requires Enter to
confirm or Esc to cancel. The core candidate preserves object, module instance,
pin, port, and connector identities; it removes only joins whose fixed connector
end no longer aligns with the moved pin. An open connector face end can remain in
an occupied cell after rotation, but connector interior geometry is still
invalid there. A stale preview cannot overwrite a later authored revision.

Specification: `docs/physical-connections.md` (Post placement rotation),
`docs/building-and-interface.md` (Default controls), and
`docs/first-playable-data-schema.md` (Grid and orientation).

Verification run from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

The supplemental offline NUnit runner passed 135 Edit Mode tests, including
independent rotation geometry/state assertions. Play Mode sources compiled with
zero warnings and errors. These do not replace a Unity Test Runner or player
build result; Unity batch licensing remains unresolved for this newer code.
