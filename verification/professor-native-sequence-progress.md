# Professor sequence native integration progress

The two-instance SR Core test now writes and reads an actual version 1 ZIP/JSON world archive after the accepted third rising edge, then checks exact module availability, authored component and instance counts and IDs, stopped-low time-zero clock, four Off source states, and Q=X/X after reopen. The literal expected Q sequence remains X/X → 1/X → 1/0 → 0/0; the serializer and simulator do not generate the oracle.

A Play Mode test starts the actual `PlayableWorld` scene, prepares the fixed SR package, places two independent instances and four sources, attaches both CLK ports to the 10 Hz world clock, checks the first two rising edges, moves the real camera until both instances lie behind it, and continuously advances through the third rising edge. It checks A=0 and B=0 while offscreen and after returning the camera. This is a scene-level offscreen simulation check, not a recorded manual presentation.

Native verification command:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

5 October 2026 result: **192 Edit Mode tests passed, 15 Play Mode tests passed, macOS build and built-player smoke passed**. The new test first failed because the Play Mode fixture resumed from a paused test-runner state; it was corrected, then the full gate passed. Changed files: `ProfessorStatefulSequenceTests.cs`, `ProfessorOffscreenPlayTests.cs` and its `.meta`, and this report. Manual professor presentation of all five steps, GUI signal inspection, and Mac/Windows performance acceptance remain unverified. CI has not run.
