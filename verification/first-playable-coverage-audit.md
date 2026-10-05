# First-playable correctness and demonstration coverage audit

Audit date: 5 October 2026. This maps the accepted [internal matrix](../docs/delivery-and-acceptance.md#first-playable-internal-correctness-matrix) and [five demonstration steps](../docs/delivery-and-acceptance.md#first-playable-professor-milestone) to executed native tests. The focused assertions use literal expected tables, authored endpoint fixtures, or exact identity/hash values rather than the simulator's own output as their oracle.

Command executed from the repository root:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

Result after the Inspect-panel check: **192 Edit Mode passed; 17 Play Mode passed; macOS build, built-player startup, V1 save/reopen, and normal-exit autosave passed**. NUnit XML and logs are in ignored `UnityProject/Logs/Verification/`. The professor Play Mode test uses the real playable scene and repository-local test save directory; its edits call game actions directly, so mouse/key control usability is still a manual check.

| Accepted row | Executed evidence | Remaining manual or platform check |
| --- | --- | --- |
| C01 Net resolution | `OneBitLogicTests`, `AndFixtureInspectionTests`, professor Edit/Play tests: independent 0/1/X/Z resolution and released versus conflicting drivers; `InspectOpensReadableModalForComponentAndConnector` checks the live connector value while the panel is open. | Graphical color/Inspect readability. |
| C02 AND | `OneBitLogicTests` literal 16 ordered pairs; `ProfessorAndSequenceTests` and professor Play Mode journey check all five specified inputs on visible authored connectors. | User builds and operates it through mouse/Configure controls. |
| C03 SR | `OneBitSrFlipFlopTests`, `GraphDrivenSrIntegrationTests`, and professor Edit/Play tests check truth/state behavior, independent Q and Q_bar. | Readability of the in-game internal panel. |
| C04 CLK transition/settling | `OneBitSrFlipFlopTests` and `WorldSimulationSchedulerTests` cover all ordered transitions and settled step results; professor tests exercise the 10 Hz world clock. | Pause-menu step buttons in a manual presentation. |
| C05 Edits and identity | `OneBitTopologyEditsTests`, `OneBitWorldConnectorTests`, `OneBitRotationTests`, and `PlayableWorldPlayTests` cover explicit junction/crossing identity, split/undo, and a real 16-cell render-region boundary. | Mouse precision for dense connector selection. |
| C06 Instances/offscreen | `OneBitHierarchicalCircuitTests`, `ProfessorStatefulSequenceTests`, and `ProfessorOffscreenPlayTests` check X/X→1/X→1/0→0/0 with both instances behind the scene camera. The Play Mode professor journey now lets the scene's normal frame loop advance the clock through the offscreen third rising edge, rather than driving `AdvanceUntil` directly. | User flies away and returns in a graphical walkthrough. |
| C07 Save/reopen/reset | `OneBitSaveBoundaryTests`, V1 archive tests, professor Edit/Play tests, and built-player persistence smoke check authored retention and transient time/Q/clock reset. | Pause-screen Save/Browse/Reopen visual walkthrough. |
| C08 Scheduler | `WorldClockEdgeScheduleTests` and `WorldSimulationSchedulerTests` check tie ordering, rational 3 Hz phase, 1 GHz representation, precision/limit and convergence behavior. | None for the narrow internal row. |
| C09 Authored round trip | `WorldV1JsonReaderTests`, `WorldV1ArchiveCodecTests`, topology/rotation tests, `WorldRecoveryStoreTests`, and damaged-world Play Mode test cover exact records, closure, corruption, backups and placeholders. | Recovery choice screen visual walkthrough. |
| C10 Packaging boundary | `OneBitPackageDraftTests`, `OneBitPackageStagerTests`, `AtomicPackageFilePublisherTests`, and professor Play Mode journey cover cut/port validation, fixed publication, and placement. | Player previews and publishes with the GUI. |
| D1 Flat-world build | `PlayableWorldPlayTests`, professor Play Mode journey, and built-player startup smoke. | Manual place/wire workflow. |
| D2 Four-state/offscreen | Professor Edit/Play journeys and offscreen camera assertion. | Manual source operation and flight. |
| D3 Inspect | Professor Edit/Play journeys check B=Z, two exact connections, no active driver and Y=X caused by B=Z. `InspectOpensReadableModalForComponentAndConnector` checks that component and connector Inspect open an unlocked cursor, use live values, and close cleanly. | Quick Look and Inspect panel visual confirmation, including scroll/readability. |
| D4 Package/two instances | Professor Edit/Play journeys and durable package Play Mode test check exact version, independent runtime and Q_bar. | Manual package preview, port layout, and in-game internal panel. |
| D5 Save/reopen | Professor Play Mode journey writes/reopens the combined V1 world after Q=0/0, then checks design/instances/connectors and Q=X/X; built player separately verifies file and exit save. | Manual Save/Browse/Reopen after closing the app. |

The strict saved-reference Mac benchmark at 1920×1080 passes the numerical FPS, p99, clock-edge, edit, save and load gates after a real render-region boundary check; two supplementary views compare standalone gates and repeated module bodies on that same saved world; see [measurement report](benchmark-reference-progress.md#supplementary-shape-views-on-the-same-reference-5-october-2026). The user temporarily waived Windows verification. Windows tests/build/performance, a dedicated dense-block shape comparison, and GitHub Actions remain unrun. Do not treat those as passes or the [five-step manual walkthrough](professor-manual-walkthrough.md) as completed.
