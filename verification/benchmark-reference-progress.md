# Saved performance reference and Mac measurement status

The committed benchmark input is `UnityProject/Assets/StreamingAssets/Benchmarks/first-playable-1000-gates.ssworld`, SHA-256 `ccbfc944c7ef245141d13a6b1183c0cb7785f34088694ea114414e5454e26967`. It has 500 standalone one-bit AND gates and ten instances of the same immutable 50-AND version, for exactly 1,000 equivalent operations. An independent Edit Mode assertion checks the literal file hash and gate distribution. The player benchmark now reads and validates this saved archive rather than generating a fresh in-memory fixture.

Native verification command:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

Result on 5 October 2026: 190 Edit Mode tests passed, 13 Play Mode tests passed, macOS build passed, and built-player smoke passed. The Unity Editor version was 6000.3.24f1. Logs and NUnit XML are in ignored `UnityProject/Logs/Verification/`. CI has not run.

Graphical measurement command: `scripts/benchmark-mac.sh`. The first attempt stalled with the terminal-launched player in the background; it was stopped and `Application.runInBackground` enabled for this opt-in runner. Two later attempts exited with an explicit `UNVERIFIED` report because Unity reported 3024×1898 for the actual player surface after a 1920×1080 request, including a fullscreen-window request. No FPS or clock-throughput measurement was collected. The required 1080p Mac acceptance is **unverified**, not passed. The graphical runner also does not yet measure scripted place/break/undo, manual save/load latency, GPU time, or renderer rebuild time. Windows performance and native checks remain deferred under the user's stated waiver; no Windows pass is claimed.

Changed files for this slice: benchmark fixture factory and player runner, editor reference builder/assembly, frozen archive and Unity `.meta` files, benchmark shell wrapper, the asset-hash Edit Mode test, this report, and the performance protocol note. The benchmark asset is generated only once by the Editor builder; it refuses to overwrite an existing reference.

## Strict Mac run, 5 October 2026

The earlier resolution failures were caused by checking one frame after `Screen.SetResolution`; Unity applied 1920×1080 a little later. The runner now waits up to five real seconds for the exact size, failing if it never appears. A diagnostic run at the prior guard setting showed the late transition, then this strict command completed without the diagnostic override:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
scripts/benchmark-mac.sh
```

Verification: **192 Edit Mode passed, 15 Play Mode passed, Mac build, startup, built-player save/reopen and exit-autosave passed**. Benchmark report: `UnityProject/Logs/Verification/benchmark-mac-reference-20261005T174210Z.txt` (ignored generated result). The fixed archive hash matched. Machine: macOS 26.6.2, Apple M4 CPU/GPU, 16 GiB RAM, Unity 6000.3.24f1. Player report: 1920×1080, Standard, 100% built-in render scale, VSync off, uncapped; 500 standalone + ten × 50 module AND gates, 1,011 connectors, 1,013 spans, no registers, one-bit wires, and 7,077 total renderer objects.

| Case | Average FPS | p99 frame ms | Max frame ms | Processed edges | Mean main/GPU ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Idle/stationary | 231.044 | 4.860 | 6.236 | 0 | 4.327 / 3.219 |
| Active/stationary | 195.930 | 7.266 | 10.042 | 1,200 | 5.087 / 3.334 |
| Idle/flying | 745.881 | 5.725 | 6.440 | 0 | 1.331 / 0.868 |
| Active/flying | 645.446 | 6.971 | 9.699 | 1,200 | 1.544 / 0.910 |

Every case warmed for ten seconds and measured for at least 60 seconds. All four Mac averages exceed 60 FPS and all p99 values are below the 33.3 ms investigation threshold. Both active cases processed the complete 1,200-edge, 10 Hz nominal 60-second schedule. The report divides by the slightly extended final-frame window and shows 9.999 and 10.000 cycles/s respectively; this is measurement-window rounding, not a dropped edge. Simulation CPU work over each active 60-second case was 446–503 ms. Reserved memory reached 190 MiB.

At two scripted grid lines, place AND took 31–34 ms, place connector 43–46 ms, break connector 41–45 ms, and undo 20–21 ms. Manual save took 172 ms; load through first rendered frame took 299 ms. All measured operations met their provisional budgets. The renderer currently reconciles objects by identity and has no spatial render-region partition, so the prescribed *render-region boundary* edit check is still unverified. The report counts all renderer objects, not the per-camera visible subset. Additional sparse/dense shape comparisons, a manual visual walkthrough, and Windows measurements have not run. The user temporarily waived Windows verification; no Windows result or full cross-platform acceptance is claimed. CI remains unrun without a safely configured licensed runner.

## Post-region Mac acceptance run, 5 October 2026

The first strict run above motivated one more renderer change: presentation objects are now grouped under 16 × 16-cell region roots, retaining keyed per-object reconciliation. A new Play Mode test crosses x=16 with a connector and verifies a distant region's graphics are not rebuilt. The benchmark now reports camera-frustum-visible renderer counts at both ends of each case. The same frozen archive and camera path were used; no benchmark input or thresholds changed.

Commands and result:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
scripts/benchmark-mac.sh
```

The first command passed **192 Edit Mode, 16 Play Mode, macOS build, startup, save/reopen, and exit-autosave**. The second printed `Strict Mac numerical frame, clock, edit, save and load gates passed.` Its ignored raw report is `UnityProject/Logs/Verification/benchmark-mac-reference-20261005T175326Z.txt`.

| Case | Average FPS | p99 ms | Max ms | Edges | Visible renderers start/end |
| --- | ---: | ---: | ---: | ---: | ---: |
| Idle/stationary | 233.419 | 4.994 | 87.161 | 0 | 4,800 / 4,800 |
| Active/stationary | 197.985 | 7.239 | 9.163 | 1,200 | 4,800 / 4,800 |
| Idle/flying | 749.794 | 5.742 | 9.491 | 0 | 6,483 / 139 |
| Active/flying | 653.625 | 6.876 | 9.170 | 1,200 | 6,483 / 139 |

All cases used 1920×1080 Standard with 10-second warm-up and at least 60 seconds measured. All four Mac FPS and p99 criteria passed; all required active edges were processed. The isolated 87.161 ms maximum in idle/stationary is recorded as a visible outlier even though its p99 is 4.994 ms. Eight place/break/undo operations across actual x=16 and x=32 region lines took 21–46 ms; manual save took 172 ms and load through the first rendered frame 299 ms. The largest reserved-memory reading was 188 MiB. The report records per-case main-thread and GPU means, the exact quality options, hardware, activity, structural counts, and visible-object counts.

This is a **Mac numerical performance pass** on the measured M4. It does not establish Windows performance, a representative ordinary Steam PC, a manual visual professor demonstration, sparse/dense renderer-shape comparisons, or a running CI job. Those remain open; Windows is temporarily waived by the user.
