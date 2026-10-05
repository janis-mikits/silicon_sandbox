# One-bit world view progress — 5 October 2026

This is presentation groundwork, not a finished playable scene or slice 2
acceptance. The new Presentation assembly draws component bodies, exact
targetable pin proxies, connector nodes/spans, identity caps, and a selectable
short face bridge for adjacent pins from one authored world revision. It
reads the current settled circuit to color one-bit pins and connector bodies;
X pulses visually, and saved identity color remains on a separate cap.
Unity object IDs and mesh positions never become electrical identities.
The view rebuilds after a successful authored revision and caches route
members for linear signal refresh.

Executed an offline compile against the installed Unity 6000.3.24f1
reference assemblies, with all temporary SDK state under
`UnityProject/Temp/OfflineVerification` and no downloads:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/UnityCompile/UnityCompile.csproj --no-restore -v quiet
```

Result: exit 0, 0 warnings and 0 errors. Unity Editor import, Play Mode
rendering/picking, player build, and performance were not executed for this
new assembly because batch licensing still cannot establish its client
channel. The view was later attached to the separate `PlayableWorld`
scene; that scene's runtime behavior still requires a Unity run.

The junction extension derives local visible incidence from authored spans
and targeted joins. It does not treat two paths in the same cell, or two
globally connected paths touching at an unjoined point, as a local junction.
A three- or four-direction local join gets a selectable marker; unjoined
crossing paths occupy slightly separated visual channel heights.
Center-to-face spans draw their leg along the actual face direction at the
junction. Independent NUnit cases expect no marker before a join, one
three-direction marker for a T branch, and one four-direction marker after
joining a crossing.

Additional offline checks:

```sh
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet build UnityProject/Temp/OfflineVerification/PlayTestsCompile/PlayTestsCompile.csproj --no-restore -v quiet
```

Results: 108 pure Edit Mode cases passed with 0 failures; Play Mode source
compiled with 0 warnings and 0 errors. A real Unity Play Mode visual check
and performance measurement remain unrun.
