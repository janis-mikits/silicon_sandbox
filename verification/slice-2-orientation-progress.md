# Authored grid orientation progress — 5 October 2026

This is an intermediate prerequisite for placement, rotation, and version 1
save validation. The player ghost, occupancy checks, rotation confirmation,
and authored component placement workflow are not complete.

`GridOrientation` implements the canonical `forward`/`up` representation and
exact transform: local +z is forward, local +y is up, and local +x is
`up × forward`. It supports all 24 proper axis-aligned orientations while
exposing the first-playable vertical-axis clockwise/counterclockwise turns.
Pin face quadrants remain integer quarter-cell coordinates. Invalid parallel
or opposite direction pairs are rejected; saved face names use lowercase.

Independent NUnit cases check the accepted axis convention, one exact yawed
pin and cell transform, all 24 valid orientation pairs and face quadrants,
four-turn identity, and invalid saved labels.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0, including `PASS: all 24 grid orientations and exact pin
transform`. The NUnit suite, Unity build, and placement/rotation Play Mode
checks have not run for this change.
