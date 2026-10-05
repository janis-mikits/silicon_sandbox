# Saved performance reference and Mac measurement status

The committed benchmark input is `UnityProject/Assets/StreamingAssets/Benchmarks/first-playable-1000-gates.ssworld`, SHA-256 `ccbfc944c7ef245141d13a6b1183c0cb7785f34088694ea114414e5454e26967`. It has 500 standalone one-bit AND gates and ten instances of the same immutable 50-AND version, for exactly 1,000 equivalent operations. An independent Edit Mode assertion checks the literal file hash and gate distribution. The player benchmark now reads and validates this saved archive rather than generating a fresh in-memory fixture.

Native verification command:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

Result on 5 October 2026: 190 Edit Mode tests passed, 13 Play Mode tests passed, macOS build passed, and built-player smoke passed. The Unity Editor version was 6000.3.24f1. Logs and NUnit XML are in ignored `UnityProject/Logs/Verification/`. CI has not run.

Graphical measurement command: `scripts/benchmark-mac.sh`. The first attempt stalled with the terminal-launched player in the background; it was stopped and `Application.runInBackground` enabled for this opt-in runner. Two later attempts exited with an explicit `UNVERIFIED` report because Unity reported 3024×1898 for the actual player surface after a 1920×1080 request, including a fullscreen-window request. No FPS or clock-throughput measurement was collected. The required 1080p Mac acceptance is **unverified**, not passed. The graphical runner also does not yet measure scripted place/break/undo, manual save/load latency, GPU time, or renderer rebuild time. Windows performance and native checks remain deferred under the user's stated waiver; no Windows pass is claimed.

Changed files for this slice: benchmark fixture factory and player runner, editor reference builder/assembly, frozen archive and Unity `.meta` files, benchmark shell wrapper, the asset-hash Edit Mode test, this report, and the performance protocol note. The benchmark asset is generated only once by the Editor builder; it refuses to overwrite an existing reference.
