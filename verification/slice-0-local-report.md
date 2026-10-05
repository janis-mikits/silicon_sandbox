# Slice 0 local verification report

30 September 2026. Repository branch: `main`. Editor: Unity `6000.3.24f1`. Host check: macOS. This report covers only the project foundation; no first-playable circuit behavior has been implemented or verified.

## Outcome

The minimal Unity project imported and compiled. Unity generated `Assets/SiliconSandbox/Scenes/FlatWorld.unity` with a solid flat floor, camera and light. The Edit Mode scene test and Play Mode scene test each passed. A macOS player built and its headless smoke run wrote `PASS`.

## Exact local verification command

From the repository root:

```bash
./scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

The final command exited 0. The wrapper invoked Unity for scene setup, `-runTests -testPlatform EditMode`, `-runTests -testPlatform PlayMode`, a standalone macOS build, and the built player. Results:

| Gate | Result | Local evidence |
| --- | --- | --- |
| Editor/project pin | `6000.3.24f1` matched | `UnityProject/ProjectSettings/ProjectVersion.txt` |
| Edit Mode | 1 total, 1 passed, 0 failed/skipped/inconclusive | `UnityProject/Logs/Verification/editmode.xml` |
| Play Mode | 1 total, 1 passed, 0 failed/skipped/inconclusive | `UnityProject/Logs/Verification/playmode.xml` |
| macOS build | Succeeded; executable exists | `UnityProject/Builds/macOS/SiliconSandbox.app` |
| Built player | Exit 0 and `PASS` | `UnityProject/Logs/Verification/player-smoke.txt` |

Logs and build artifacts are ignored by Git; regenerate them with the command above. The generated scene, `.meta` files, lockfile and Project Settings are repository work. The lockfile records Test Framework `1.6.0`, matching the installed Editor.

## Scope and outstanding checks

The floor in this smoke scene is a small preview aligned to authored grid layer `y=0`; it is not the final sparse generated world. The tests establish project import, scene viability and player launch, not the professor milestone.

The Windows PowerShell wrapper, Windows build/player, macOS/Windows CI workflow, and performance benchmark have not run. No GitHub remote was contacted, no runner or license secret was configured, and no CI pass is claimed. The workflow is designed for a configured licensed self-hosted runner on each platform and runs on `main` pushes or manual dispatch once available. Its source checkout and artifact-upload actions are pinned to commit SHAs.

The earliest failed runs were environment/setup failures: an overly strict offline sandbox blocked local Package Manager sockets; another sandboxed run could not initialize the licensing channel. After Unity Hub's active Personal license was visible and a stale socket from the failed run was cleared, the Editor imported and compiled. An initial test failure came from incorrectly marking the Play Mode assembly Editor-only; the corrected assembly passed. An initial player check expected the product name before it was set; the product name and final smoke check passed. These failures do not count as successful tests, and the final result above is from a complete rerun.

## Changed files

- `UnityProject/` project settings, package manifest/lock, generated scene and asset metadata, bootstrap marker, Editor setup/build entry point, and one Edit Mode plus one Play Mode test.
- `scripts/verify-unity.sh` and `scripts/verify-unity.ps1` repeatable local gate; the PowerShell version remains unexecuted.
- `.github/workflows/unity-verification.yml` proposed two-platform CI gate.
- `.gitignore` excludes generated Unity state while retaining authored Editor build source.
- `verification/slice-0-local-report.md` records the executed local gate.

Next implementation work is slice 1 only after approval of exact built-in pin/footprint coordinates from the [plan's open decision](../first-playable-implementation-plan.md#unresolved-decisions-and-progress-protocol). Its independent AND and net-resolution oracles are in the [matrix](../first-playable-requirement-test-matrix.md#literal-logic-tables).
