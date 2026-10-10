# Wire routing verification — 9 October 2026

New routes enforce a 0.25-block minimum physical segment and coaxial entry at every attached pin. The touching-pin bridge remains the user-approved exception. Routes minimize turns, then total length, then maximize each successive straight run from the input. Input/output click order does not change the result. Exact quarter-block points are shared by preview, rendering, and save/reload; channel numbers no longer offset newly routed geometry.

The search is heading-aware A* with an admissible turn/length lower bound, followed by the input-side run-length tie-break. All available logical channels are compared; equivalent occupancy searches are reused. Component/module footprints, wire occupancy, quarter-grid physical separation, world bounds, and existing face-quadrant rules constrain candidates. A half-cell run along a face can connect its quadrants without inventing an invalid intermediate node. Cross-cell zero-length spans are topology bookkeeping, not visible wire segments. Failure remains atomic.

Version 2 manifests and connector `geometryVersion:2` preserve exact bends. Tag edits, breaks, and module extraction carry the geometry version forward. The user explicitly waived existing demo-world compatibility; existing routes are not automatically rerouted. No demo worlds were deleted.

## Executed checks

Final command from the repository root:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

Exit 0: **212 Edit Mode tests and 25 Play Mode tests passed**, with no failures, skips, or inconclusive results. macOS player build, startup, save/reopen, and exit-autosave checks passed. `git diff --check` also passed. Detailed logs and XML are under `UnityProject/Logs/Verification/`.

Focused coverage includes independently specified two-turn routes and literal bend coordinates, input-side priority for successive runs, click-order independence, all 24 orientations, obstacle detours, fewer turns winning over a shorter zigzag, independent wires sharing cells on distinct tracks, a branch into an existing boundary node, touching pins, occupied-pin/channel rejection, exact archive round-trip, connector splitting, and module snapshot copying. Play Mode verifies circular wire end faces at both pins and coincident miter rings between adjacent rendered spans.

The local routing sample in a 1,024 × 1,024 × 256 world took **4.652 ms** in the final Edit Mode run. This is a single routing measurement, not a full rendering benchmark or a worst-case guarantee. Windows and a new full 1,000-gate performance run were not executed for this change.

## Files changed for this request

All Unity paths below are under `UnityProject/Assets/SiliconSandbox/`:

- `Core/Application/OneBitPinRoutePlanner.cs`; new `QuarterWireRouter.cs` and its Unity metadata; `OneBitTopologyEdits.cs`; `OneBitModuleSnapshotBuilder.cs`.
- `Core/Authoring/OneBitAuthoredTopology.cs`; `Core/Graph/OneBitTopologyGraphBuilder.cs`.
- `Core/Persistence/V1DesignJsonWriter.cs`, `WorldV1JsonReader.cs`, `WorldManifestIntegrity.cs`, `WorldManifestJsonReader.cs`, `WorldManifestJsonWriter.cs`.
- `Unity/Presentation/WireMeshGeometry.cs`, `OneBitWorldView.cs`; `Unity/Interaction/OneBitWorldInteraction.cs`.
- `Tests/EditMode/OneBitPinRoutePlannerTests.cs`, `WorldManifestJsonReaderTests.cs`, `WorldManifestJsonWriterTests.cs`; `Tests/PlayMode/VisualArtPlayTests.cs`.
- Repository documentation: `INDEX.md`, `docs/physical-connections.md`, `docs/building-and-interface.md`, `docs/first-playable-data-schema.md`, `docs/saving-and-recovery.md`, and this report.

The existing Play Mode fit test refreshed `Art/Review~/unity-wire-fit.tga`. Earlier uncommitted art, wire-size, and texture-orientation work remains present. No commit, push, dependency installation, or external service was used.
