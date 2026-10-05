# Persistence foundation progress (5 October 2026)

The new `SiliconSandbox.Persistence` assembly contains a repository-local ZIP
envelope for the accepted world-save entry structure. It writes
`manifest.json`, `world.json`, and canonical
`modules/<lowercase-uuid-v4>.json` entries. The reader rejects unknown,
duplicate, missing, and traversal paths, noncanonical module names, excessive
entry counts, oversized compressed input, and oversized expanded entries.
The envelope retains exact UTF-8 bytes for the later manifest hash checks.
The typed manifest layer now records SHA-256 and byte lengths for the exact
uncompressed world/module bytes, checks listed entries and dependency closure,
and distinguishes a damaged world record from an individually damaged or
missing module record. Its SHA-256 implementation is checked against the
independent published `abc` digest vector.

This does **not** yet serialize or strictly parse the version 1 JSON records,
verify record identity/topology, implement atomic file replacement or backups,
or reopen a world. It uses only the C# standard library; no new package or
external service was accessed.

Offline repository-local NUnit command executed from the repository root:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Latest result: 127 passed, 0 failed, 0 ignored. Unity Test Runner and a player build
for this change did not run because Codex-launched Unity batch mode still
cannot connect to the Licensing Client.
