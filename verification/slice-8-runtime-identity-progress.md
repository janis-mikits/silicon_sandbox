# Instance-qualified runtime identity progress — 5 October 2026

This is a prerequisite for independent module instances, not slice 8
acceptance. Packaging, placement, port mapping, and the internal viewer are
not implemented by this change.

The simulator now keys each runtime component by exact module version,
blueprint-local object ID, and outer-to-inner placed-instance ID chain.
Top-level world objects use their world object ID with an empty chain.
Driver identity also includes the qualified object and output pin, so two
instances of one blueprint can drive the same world net without overwriting
each other's entries. Source state and SR Q/CLK state survive graph refresh
by this full key. These are runtime indexes; the accepted authored IDs and
version 1 save schema remain unchanged.

Independent NUnit cases compare equal/different version and instance paths,
show two identical blueprint-local source IDs resolving to X from opposed
0/1 drivers, and show two identical blueprint-local SR IDs retaining
different Q states through graph refresh.

Executed offline, repository-local core compile and harness:

```sh
DOTNET_CLI_HOME="$PWD/.verification-tools/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/.verification-tools/packages" dotnet run --project .verification-tools/CoreCompile/CoreCompile.csproj --no-restore -v quiet
```

Result: exit 0; `PASS: instance-qualified duplicate local source drivers`
and `PASS: duplicate local SR IDs preserve separate instance states`.
The NUnit suite and Unity build remain unrun. Actual module expansion must
still create these keys and preserve port/net boundaries; scene-level
independence is not yet verified.
