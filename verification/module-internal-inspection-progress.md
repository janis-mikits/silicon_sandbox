# Live internal inspection of first-playable modules

Targeting a placed module body and pressing I now opens a scrollable read-only panel for that exact instance. It lists the immutable local component layout and its current settled pin values, including SR Q and Q_bar. The per-instance graph indexes, rather than saved blueprint state, supply the values. A missing exact definition displays the placeholder message without guessing internal values.

The accepted professor Core test now checks Q_bar for both copies at each literal sequence state: initial X/X, then 0/X, 0/1, 1/1, and X/X after the V1 archive reopens. The offscreen Play Mode test checks both Q_bar values after the third rising edge. These are independent expected values from the SR complement rule.

Command executed:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

5 October 2026 result: **192 Edit Mode and 16 Play Mode tests passed; macOS build, built-player startup, save/reopen, and normal-exit autosave passed**. Changed files: `OneBitCircuitInspection.cs`, `OneBitWorldInteraction.cs`, professor Edit/Play tests, canonical delivery/control notes, autonomous decision log, and this report. The panel has not had a manual visual walkthrough. Nested child-instance navigation remains later work as stated in the canonical choice; this test does not claim to verify it.
