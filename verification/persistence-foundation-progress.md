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
The application can also capture a coherent authored save snapshot and open a
fresh session from it. An in-memory regression changes a source and SR Q,
captures the design with a fixed module placement and 36-slot inventory, then
checks that identities/topology/settings remain while the source returns to
its configured Off state, Q returns to X, time returns to zero, and the world
clock starts stopped at 0. This is not a file round trip; JSON mapping remains.

This does **not** yet serialize or strictly parse the version 1 JSON records,
verify record identity/topology, implement atomic file replacement or backups,
or reopen a world. It uses only the C# standard library; no new package or
external service was accessed.
The local file store now writes a candidate beside the current save, flushes
and validates it, then replaces the current file while retaining `.previous`.
Its test writes only under `UnityProject/Temp`, confirms a rejected candidate
leaves the old bytes intact, and confirms a valid replacement preserves the
previous bytes. Autosave timing, backup selection, and strict candidate
validation remain to be integrated.

Offline repository-local NUnit command executed from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Latest result: 130 passed, 0 failed, 0 ignored. `bash -n scripts/verify-unity.sh`
also passed. Play Mode source compilation
also succeeded with zero warnings/errors. Unity Test Runner and a player build
for this change did not run because Codex-launched Unity batch mode still
cannot connect to the Licensing Client.
