# Pin connection corridor verification — 9 October 2026

Implemented the user-approved corridor rule for new wire routing and component/module placement. Each corridor follows the outward pin axis, protects the seated attachment portion, and extends 0.25 block beyond the pin's front face (0.0625 block beyond its authored attachment point). Unrelated wire centerlines must stay at least 0.15 block away. Only an explicit attachment to the exact pin grants an exception; net membership alone does not. Touching-pin bridges remain supported.

The planner uses a cell-indexed corridor lookup alongside its existing obstacles and priorities. Connector publication validates clearance again. Component and module placement validate every new pin against existing wires before publishing; placement previews already call these same functions and invalidate their cache on design changes. A rejected edit leaves the design and revision unchanged. Existing wires are neither deleted nor rerouted. A replacement block can require removal of an abandoned wire obstructing its new pin. Existing rotation behavior was not changed by this placement task.

Exact version 2 wire coordinates are checked directly. Older lane-offset geometry uses its display positions and explicit attachment positions; the diagonal center-to-face leg uses a conservative bounding envelope. No new save fields or dependencies were introduced.

## Executed verification

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
git diff --check
```

Both exited 0. Final results: **219 Edit Mode tests and 26 Play Mode tests passed**, with no failures, skips, or inconclusive results. macOS build, player startup, save/reopen, and exit-autosave checks passed. Full logs and XML: `UnityProject/Logs/Verification/`.

New regressions cover:

- SR Q → AND B leaves Q_bar accessible, followed by a successful Q_bar → AND A connection with separate electrical nets.
- A front-on Unity physics ray selects Q_bar rather than the nearby wire, then the second connection succeeds.
- Wire at 0.25 block in front rejects block placement; a wire at 0.5 block permits it.
- Correct corridor orientation for all 24 block orientations.
- Module-port clearance, rejected module placement, and permitted explicit module-pin attachment.
- The touching-pin bridge exception.
- Older wire lanes at approximately 0.099 versus 0.297 block transverse separation reject versus permit placement.
- Replacement component/module placement remains blocked by an abandoned wire until that wire is removed.

The existing large-world routing sample measured 8.330 ms in this run. This is one local routing measurement, not a worst-case or full frame-performance claim. Windows and a new full 1,000-gate rendering benchmark were not run.

## Files changed in this task

Under `UnityProject/Assets/SiliconSandbox/`:

- New `Core/Application/PinConnectionCorridors.cs` and `.meta`.
- `Core/Application/OneBitPinRoutePlanner.cs`, `OneBitWorldEdits.cs`.
- New `Tests/EditMode/PinConnectionCorridorTests.cs` and `.meta`.
- `Tests/EditMode/OneBitPinRoutePlannerTests.cs`, `OneBitWorldPlacementTests.cs`.
- `Tests/PlayMode/VisualArtPlayTests.cs`.

Documentation: `docs/physical-connections.md`, `docs/building-and-interface.md`, `first-playable-requirement-test-matrix.md`, and this report. The existing render test refreshed `Art/Review~/unity-wire-fit.tga`. Prior uncommitted work was preserved. No commit or push was performed.
