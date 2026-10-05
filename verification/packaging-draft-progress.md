# Package configuration draft progress

`OneBitPackageDraft` builds an independent authored snapshot from a rectangular
selection and holds editable candidate ports. It can validate a fixed module
version without publishing it to a world, inventory, or global library. The
default one-bit layout places inferred inputs on west faces and outputs on east
faces, preserving exact internal authored endpoints. It now supports a region
larger than one cell when enough face quadrants are available. The professor SR
candidate can remove `Q_bar` and expose S, R, CLK, and Q while leaving the
source circuit intact.

Specification: `docs/modules-and-packaging.md` (Packaging region and ports,
Selection and package configuration), `docs/delivery-and-acceptance.md`
(Professor demonstration choices), and `docs/first-playable-data-schema.md`
(Module versions and instances).

Verification from the repository root:

```sh
SILICON_SANDBOX_TEST_ROOT="$PWD/UnityProject/Temp/OfflineVerification/StoreTests" DOTNET_CLI_HOME="$PWD/UnityProject/Temp/OfflineVerification/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true NUGET_PACKAGES="$PWD/UnityProject/Temp/OfflineVerification/packages" dotnet run --project UnityProject/Temp/OfflineVerification/EditTestsCompile/EditTestsCompile.csproj --no-restore -v quiet
```

Result: 138 supplemental offline NUnit Edit Mode tests passed. Unity Test Runner
and player build were not run for this slice because Codex-launched Unity batch
mode still cannot connect to its Licensing Client. Durable library/inventory
publication, in-game configuration UI, and full world-file round trip remain
unimplemented.
