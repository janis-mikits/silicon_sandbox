# Module extraction progress (5 October 2026)

The pure C# package preview copies a selected one-bit circuit into local authored
coordinates with new component, pin, connector, node, span, and join IDs. It
keeps the source world unchanged. It clips routes at the exact inclusive
selection boundary, separates paths that leave and re-enter, excludes paths
without an inside component connection, and exposes exact authored pin/node
references as port candidates. The stage does not yet configure exterior
ports, place an instance, or save the result. A separate fixed-version factory
now accepts explicit one-bit port names, directions, exterior locations, and
authored internal endpoint mappings; it assigns stable port/version identities
and rejects duplicate names, occupied exterior positions, or missing targets
before returning a candidate version. This factory does not publish a library
entry or world inventory reference.

Independent NUnit expectations cover fresh IDs and retained clock-stub
connectivity; exclusion of an orphan route; exact endpoint for a connected
boundary cut; and separation of two inside pieces whose only original path
ran outside the region.

Offline repository-local NUnit command executed from the repository root:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Latest result: 115 passed, 0 failed, 0 ignored. The Play Mode source compile
also succeeded with zero warnings and zero errors using the same offline SDK
environment. This offline harness compiled the
current Core, Unity runtime, and Edit Mode test sources against installed
Unity assemblies, but it did not run Unity's Test Runner. Batch Unity
verification remains blocked because the Licensing Client channel does not
connect in the Codex-launched process, although the project opens in Unity Hub.
The next gate is a real Unity Edit Mode and Play Mode run and player build after
that batch-mode issue is resolved.
