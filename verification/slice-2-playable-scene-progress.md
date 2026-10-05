# Separate playable scene progress — 5 October 2026

This is an interactive slice 2 scaffold, not first-playable acceptance. The
new `PlayableWorld` scene starts an empty 32 × 32 flat authored world. Its
bootstrap creates a creative camera/player, a read-only world view, and an
interaction shell. Hotbar keys 1–4 select Source, wire, AND, and SR. Right
click places a component preview or, when aiming at a source body, toggles
that source's live state. With wire selected, two free pin targets create one
authored route and explicit joins. Left click breaks a targeted connector
span. C configures a source's saved On value and initial On/Off, I inspects a
targeted pin or connector, P controls the world clock, Escape pauses/resumes
simulation, and bracket keys step while paused. A 36-slot inventory screen
and nine-slot hotbar are present as UI scaffolding. The existing
`FlatWorld` fixture scene remains available to its current tests.

The Unity view reads stable authored part IDs and settled circuit values.
The new Play Mode test is designed to load the scene, place a source, see a
selectable pin, toggle it, and verify that saved startup configuration did
not change. It has only been compiled, not executed. The copied Unity scene
YAML, build-scene ordering, camera movement, ray targeting, ghost
transparency, controls, and native player behavior require a real Unity
Editor and player run before any acceptance claim.

Executed offline with the approved SDK state under
`UnityProject/Temp/OfflineVerification` and no downloads:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/EditorCompile/EditorCompile.csproj --no-restore -v quiet
```

Results: 104 pure Edit Mode NUnit cases passed with 0 failures; Play Mode
and Editor build sources compiled with 0 warnings and 0 errors. These checks do not exercise
the Unity engine. The official `scripts/verify-unity.sh` wrapper remains
blocked by the local Licensing Client channel, as recorded in
[authored placement progress](slice-2-authored-placement-progress.md).

Remaining slice 2 work includes connector-to-connector junctions and visible
crossings, precise overlapping-part selection and net highlighting,
full invalid-action sound/feedback, source and component UI polish, and
actual Editor/Play Mode checks. Later slices still require rotations,
clock-link placement, packaging, independent module instances, persistence,
the complete professor path, and performance acceptance.
