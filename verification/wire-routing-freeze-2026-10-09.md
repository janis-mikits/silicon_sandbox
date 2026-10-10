# Wire routing freeze regression — 9 October 2026

The router checked only the destination of each candidate edge for existing wire occupancy. Its pin-entry preflight therefore missed an occupied approach point left behind after breaking the final source leg. The subsequent quarter-grid search could explore an enormous unreachable search space on Unity's main thread without a time or work bound, preventing frames and input from advancing. Code inspection and the reproduced obstruction establish this failure mechanism; no original force-quit trace was available.

The planner now checks occupancy at both ends of each edge. The explicit target-node exemption and touching-pin exception remain. All channel attempts share a search budget: 50 ms elapsed, at most 8,192 queue removals and 32,768 recorded candidates. Budget exhaustion throws through the normal invalid-action path without publishing any partial or unproven route. Failed previews remain cached until the target or world revision changes. Invalid clicks flash red, play a generated quiet 40 ms click, and clear the provisional wire selection so breaking and retrying remain available. The prior invalid-action implementation had the flash but no audio playback; no package or dependency was added.

## Executed verification

Command, run locally with Unity 6000.3.24f1 on macOS:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
git diff --check
```

Final verification wrapper exited 0: **222 Edit Mode and 27 Play Mode tests passed**, none failed/skipped/inconclusive. macOS player build, player startup, save/reopen, and normal-exit autosave checks passed. Logs and XML are in `UnityProject/Logs/Verification/`. An earlier run caught a missing Graph assembly reference in the new Play Mode assertion; that assertion now uses authored connector/revision state, while the Edit Mode regression verifies graph connectivity. The complete wrapper passed afterward.

- Broken-source-leg reconnect rejected before search in **0.057 ms** and **0.024 ms**, covering both click orders. The design and revision remained unchanged; removing the obstruction allowed connection.
- The separately enclosed unreachable approach rejected at the search budget in **27.482 ms** without a partial connector.
- Play Mode verified invalid-preview caching, red-flash scheduling, non-silent generated audio data and AudioSource configuration, continued frames, selection cleanup, and a successful new connection after removing the old wire. Hardware audibility was not manually tested.
- Existing nearby-pin, placement-clearance, touching-pin, routing-priority, and persistence regressions passed in the same suite.

## Changed files and limits

Task edits: `Core/Application/OneBitPinRoutePlanner.cs`, `Core/Application/QuarterWireRouter.cs`, `Unity/Interaction/OneBitWorldInteraction.cs`, `Tests/EditMode/WireRouteFailureTests.cs` and its Unity meta file, and `Tests/PlayMode/VisualArtPlayTests.cs` (all under `UnityProject/Assets/SiliconSandbox/`); `docs/physical-connections.md`; `first-playable-requirement-test-matrix.md`; this report. Existing art review tests refresh their local capture. Earlier uncommitted routing/art work was preserved.

The safety budget can reject an unusually expensive valid route. It bounds checked search work rather than guaranteeing that the entire edit or frame completes within exactly 50 ms. Routing preferences and geometry are unchanged for successfully completed searches. Windows and CI were not run, and this is not a general performance acceptance benchmark. No demo worlds were deleted and no changes were committed or pushed.
