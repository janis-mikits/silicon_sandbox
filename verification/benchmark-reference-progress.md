# Saved performance reference and Mac measurement status

The current benchmark input is `UnityProject/Assets/StreamingAssets/Benchmarks/first-playable-1000-gates-compact-v1.ssworld`, SHA-256 `016e9cff9773be7cb0b931a7d7492b8a9ec856b79418e445a39ec0acf1c95c3c`. It has 500 standalone one-bit AND gates and ten instances of the same immutable 50-AND version, for exactly 1,000 equivalent operations. An independent Edit Mode assertion checks the literal file hash and gate distribution. The player benchmark reads and validates this saved archive rather than generating a fresh in-memory fixture. The original `first-playable-1000-gates.ssworld`, SHA-256 `ccbfc944c7ef245141d13a6b1183c0cb7785f34088694ea114414e5454e26967`, is retained for historical comparison; its pre-release module record lacks the now-required compact exterior size. Measurements below dated 5 October and the earlier 6 October run used that old archive.

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

## Supplementary shape views on the same reference, 5 October 2026

The later benchmark run retained the same archived world and SHA-256, four acceptance cameras/activity cases, Standard preset, 1920×1080, and strict numerical thresholds. Two additional stationary views measured separate regions of that same world with the clock stopped: sparse standalone gates from `(8, 0.3, 55)` facing south, and ten repeated opaque module bodies from `(100, 0.3, 31)` facing east. Each used five seconds of warm-up and at least 20 seconds of samples. `scripts/check-benchmark-report.py` now requires both views and checks their FPS and p99 values without changing the four primary acceptance gates.

Commands:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
scripts/benchmark-mac.sh
```

Result: **192 Edit Mode passed, 17 Play Mode passed, macOS build, player startup, save/reopen and exit-autosave passed**. The benchmark printed `Strict Mac numerical frame, shape, clock, edit, save and load gates passed.` The ignored full report is `UnityProject/Logs/Verification/benchmark-mac-reference-20261005T182117Z.txt`.

| View | Average FPS | p99 ms | Visible renderers | Main/GPU mean ms |
| --- | ---: | ---: | ---: | ---: |
| Sparse standalone gates | 331.223 | 3.199 | 3,632 | 3.005 / 2.283 |
| Repeated module bodies | 1,356.005 | 0.861 | 323 | 0.737 / 0.451 |

The four primary cases also passed: idle/stationary 241.685 FPS, active/stationary 205.986, idle/flying 759.712, active/flying 684.395; p99 was 4.727–6.913 ms and both active runs processed all 1,200 expected edges. All eight boundary edits took 20–47 ms, save 175 ms, and load through the next frame 302 ms. Largest reserved-memory reading was 188 MiB. The module view has fewer visible renderers, so its higher FPS is a scene-shape observation, **not** a claim that module presentation is intrinsically faster per object. A dedicated densely packed opaque-block scene and a manual visual comparison have not been measured. Windows and CI remain unrun.

## Dense opaque diagnostic and full Mac rerun, 5 October 2026

A separate saved V1 diagnostic, `UnityProject/Assets/StreamingAssets/Benchmarks/first-playable-dense-diagnostic.ssworld`, freezes 1,000 one-bit AND bodies in a contiguous 40 × 25-cell grid. It has no modules or connectors; it isolates opaque body density, not electrical switching or the mixed-scene milestone. Literal SHA-256 `9dfc121962af93d0151c09203b82bb2dc2d4275b72145aeba2d505f7dbef9242` and every expected cell are asserted in Edit Mode. The original mixed reference and hash remain unchanged. The diagnostic uses the same Mac, 1920 × 1080 Standard preset, five-second warm-up, 20-second measurement, and camera `(20,0.3,34)` facing south. Bodies retain small visual gaps, so no claim is made about fully occluded interior faces.

Commands and observed result:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
scripts/benchmark-mac.sh
```

The Unity gate passed **193 Edit Mode tests, 17 Play Mode tests, Mac build, player startup, V1 save/reopen, and normal-exit autosave**. The benchmark printed `Strict Mac numerical frame, shape, clock, edit, save and load gates passed.` The ignored raw report is `UnityProject/Logs/Verification/benchmark-mac-reference-20261005T195215Z.txt`.

| Saved world / view | Average FPS | p99 ms | Visible renderers | Main/GPU mean ms |
| --- | ---: | ---: | ---: | ---: |
| Mixed reference, idle/stationary | 248.263 | 4.542 | 4,800 | 3.987 / 3.164 |
| Mixed reference, active/stationary | 209.477 | 6.754 | 4,800 | 4.720 / 3.350 |
| Mixed reference, idle/flying | 795.379 | 5.495 | 6,483 → 139 | 1.241 / 0.869 |
| Mixed reference, active/flying | 683.557 | 6.545 | 6,483 → 139 | 1.454 / 0.906 |
| Mixed reference, sparse gates | 305.402 | 3.620 | 3,632 | 3.033 / 2.448 |
| Mixed reference, repeated modules | 1,220.397 | 0.955 | 323 | 0.819 / 0.523 |
| Dense diagnostic, opaque AND bodies | 234.091 | 4.807 | 4,534 | 4.271 / 3.282 |

Both active reference cases processed all 1,200 expected edges. Eight x=16/x=32 boundary operations took 21–52 ms; manual save took 176 ms and load through first frame 306 ms. Dense diagnostic used 5,005 total renderer objects and reached 222 MiB reserved memory, compared with 7,077 renderers in the mixed reference. The shapes differ in visible-object count and circuit structure, so these are observed view costs, not per-object speed claims. Mac numerical first-playable acceptance passes; graphical readability, the manual professor walkthrough, Windows, and CI remain separate unverified checks.

## Compact-V1 frozen reference Mac run, 6 October 2026

The approved pre-release V1 correction added a separate module exterior footprint. The old benchmark archive remains in the repository for historical comparison; the active benchmark uses the newly frozen `first-playable-1000-gates-compact-v1.ssworld`, SHA-256 `016e9cff9773be7cb0b931a7d7492b8a9ec856b79418e445a39ec0acf1c95c3c`. Its Edit Mode test verifies the exact hash, 500 standalone gates, ten 50-gate module instances, all definitions available, and the same 1,011 connector routes and 1,013 visible spans. The module record has the required `exteriorSizeCells`. The dense diagnostic archive and hash are unchanged.

Commands:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
scripts/benchmark-mac.sh
```

The Unity gate passed **200 Edit Mode and 17 Play Mode tests, macOS build, player startup, save/reopen, and exit-autosave** when the benchmark was run. A subsequent rerun after adding two direct break-operation tests passed **202 Edit Mode and 17 Play Mode tests**, with the same build/player checks. The graphical benchmark printed `Strict Mac numerical frame, shape, clock, edit, save and load gates passed.` Its ignored raw report is `UnityProject/Logs/Verification/benchmark-mac-reference-20261006T215207Z.txt`. Hardware was Apple M4 on macOS 26.6.2; every case used 1920 × 1080 Standard, VSync off, and 100% built-in render scale. The four primary cases each warmed for ten seconds and measured at least 60 seconds.

| View | Average FPS | p99 ms | Processed clock edges |
| --- | ---: | ---: | ---: |
| Idle/stationary | 222.511 | 7.315 | 0 |
| Active/stationary | 184.052 | 8.700 | 1,200 |
| Idle/flying | 505.464 | 6.592 | 0 |
| Active/flying | 544.013 | 7.702 | 1,200 |
| Sparse standalone gates | 248.781 | 5.864 | 0 |
| Repeated module bodies | 772.339 | 6.211 | 0 |
| Dense opaque diagnostic | 219.280 | 6.928 | 0 |

Eight place/break/undo operations across x=16 and x=32 region lines took 20.546–46.240 ms. Manual save took 169.847 ms and load through first rendered frame 298.645 ms. The maximum single idle/stationary frame was 568.746 ms even though its p99 was 7.315 ms; the strict protocol gates on average FPS and p99, so this isolated outlier remains visible for follow-up rather than being called a clean maximum. The two active cases processed the complete 1,200-edge schedule. This is a numerical Mac pass for the revised archive, not a manual readability pass, Windows result, or executed CI job.

## Final topology-fix Mac rerun, 6 October 2026

After the additional connector-bridge fix, the exact commands above passed **203 Edit Mode tests, 17 Play Mode tests, macOS build, player startup, save/reopen, and exit-autosave**, followed by another strict graphical benchmark pass. The final ignored report is `UnityProject/Logs/Verification/benchmark-mac-reference-20261006T220223Z.txt`. It used the same compact-V1 reference hash `016e9cff9773be7cb0b931a7d7492b8a9ec856b79418e445a39ec0acf1c95c3c`, dense diagnostic hash `9dfc121962af93d0151c09203b82bb2dc2d4275b72145aeba2d505f7dbef9242`, machine, resolution, Standard preset, render scale and time windows.

| View | Average FPS | p99 ms | Clock edges |
| --- | ---: | ---: | ---: |
| Idle/stationary | 225.964 | 7.269 | 0 |
| Active/stationary | 193.216 | 7.862 | 1,200 |
| Idle/flying | 412.587 | 6.329 | 0 |
| Active/flying | 455.882 | 7.353 | 1,200 |
| Sparse standalone gates | 291.712 | 6.075 | 0 |
| Repeated module bodies | 1,027.842 | 4.992 | 0 |
| Dense opaque diagnostic | 215.525 | 7.204 | 0 |

The eight region-boundary edit latencies were 20.944–49.795 ms, manual save 175.634 ms, and load through first rendered frame 308.778 ms. The idle/stationary run again had one long maximum frame, 559.757 ms, although p99 was 7.269 ms. The strict protocol passes numerically on this Mac; the one-frame hitch is a recorded investigation item. Manual visual acceptance, Windows checks under the user's temporary waiver, and an executed CI job remain unverified.
