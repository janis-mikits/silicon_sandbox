# In-session construction undo progress (5 October 2026)

`OneBitWorldSession` now retains authored revisions from the preceding five
wall-clock minutes for U/J undo and redo. A successful undo restores exact
object/pin/topology identities and a new coherent derived graph. It leaves
simulation time and unrelated live source On/Off state in place. Undoing an
authored source setting refreshes its configured drive while keeping that
live switch state. A new construction edit clears redo history. This history
is not serialized yet; persisted undo is outside the narrow first playable.

Independent NUnit tests cover identity restoration, redo, live-state and
simulation-time preservation, source-configuration undo, and expiry after
five minutes. The Unity interaction shell binds U and J; those keys have
compiled offline but have not run in Unity.

Offline command executed from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Result: 132 passed, 0 failed, 0 ignored. Unity Test Runner and the current
player build remain unrun because Codex-launched batch mode cannot establish
the Licensing Client channel.
