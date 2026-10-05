# Slice 1 local verification report

5 October 2026. Repository branch: `main`. Unity Editor: `6000.3.24f1`. Host: macOS. This report covers the four-state one-bit AND development fixture, not the complete first playable.

## Result and exact command

From the repository root:

```bash
./scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

The final full command exited 0. It imported and compiled the project, ran 41 Edit Mode tests and 2 Play Mode tests with zero failed, skipped or inconclusive, built a macOS player, and ran the built-player smoke check (`PASS`). XML results and Editor/player logs are generated under ignored `UnityProject/Logs/Verification/`; the built app is under ignored `UnityProject/Builds/macOS/`.

| Gate | Executed evidence |
| --- | --- |
| C01 net resolution | Literal, independent 16-pair two-driver table; empty/all-Z, released/agreeing drivers, conflict versus active X causes, and removal of the last driver. |
| C02 AND | Literal, independent 16-pair AND table. Five professor input cases settle through the fixture's explicitly attached connectors while the clock is stopped. |
| Source behavior | Off drives 0; On drives configured 0/1/X/Z; reset restores configured initial On/Off state and retains configured On value. |
| Built-in version 1 geometry | Exact approved source, AND and SR one-cell pin coordinates checked against literals. |
| Play Mode | Flat scene creates two source bodies, three visible state-colored connectors with neutral identity caps, an AND body and live value labels; all five professor AND cases update the scene fixture. |
| Native player | macOS build completed and headless player smoke found a running zero-output fixture. |

The first restricted process attempt stalled before compilation because its spawned Unity Licensing Client held the single-instance mutex without providing a usable channel. I stopped only that process. The first full licensing-enabled attempt then reached compilation and found a missing Play Mode test assembly reference; I fixed it. The final complete rerun above passed. Neither failed attempt is counted as a successful gate.

## Scope and remaining work

`Core/Contracts`, `Core/Authoring`, `Core/Graph` and `Core/Simulation` have separate C# assemblies. The simulation logic has no UnityEngine dependency; source changes enqueue work and `AdvanceToSettled` processes it independently of rendered frames. The fixture owns fixed UUIDv4 component, pin and connector identities and explicit pin attachments. Its visible route shapes are development presentation, **not** version 1 route-node/span/join records. That full authored topology, exact picking, player placement, inspection, junction/crossing edits and serialization remain for later slices. The fixture does not implement the world clock or SR storage.

The Windows wrapper, Windows build/player, GitHub Actions workflow, manual interactive walkthrough, offscreen checks and performance benchmark have not run. No CI pass is claimed. No external service was connected during verification, and no dependency or Unity version changed. The verification command itself made no commit or push. The only outside-repository diagnostic read was the previously authorized Unity Licensing Client log; no account data or credentials were printed.

## Changed files

- `docs/first-playable-data-schema.md` records the approved binding built-in version 1 pin geometry; `first-playable-implementation-plan.md` marks that decision resolved.
- `UnityProject/Assets/SiliconSandbox/Core/{Contracts,Authoring,Graph,Simulation}/` adds the isolated one-bit fixture contracts, catalog geometry, explicit connector binding, logic, net resolution, source and AND event processing.
- `UnityProject/Assets/SiliconSandbox/Unity/Bootstrap/` adds the visible interactive fixture to the existing flat scene bootstrap.
- `UnityProject/Assets/SiliconSandbox/Tests/{EditMode,PlayMode}/` adds the literal-table, integration, geometry and scene checks and assembly references.
- `scripts/verify-unity.sh`, `scripts/verify-unity.ps1` use a slice-neutral success message; `first-playable-requirement-test-matrix.md` points to executed reports.

Next proposed task is slice 2 in the [implementation plan](../first-playable-implementation-plan.md#ordered-runnable-slices): replace fixture-only connector binding with canonical version 1 authored route/join records, then add player placement, exact targeting and truthful Inspect behavior. Its C01/C02/C05 oracles are in the [matrix](../first-playable-requirement-test-matrix.md#every-accepted-internal-correctness-row).
