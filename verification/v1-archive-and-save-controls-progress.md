# Version 1 archive and first-playable Save/Open controls — 5 October 2026

The first-playable world can now be written as a ZIP archive containing strict version 1 JSON and an exact-byte manifest. The reader checks the world record, hashes, exact referenced module closure, topology, and module interface snapshots. A damaged embedded definition uses only a valid indexed global copy with matching identity and hashes; otherwise the placed instance remains an X-driving placeholder with its saved interface. A damaged world record is rejected. The global library index is strict, and orphaned or hash-mismatched definition files are not usable.

The pause screen now exposes Save World, Reopen Saved, and Browse saved worlds. Browse can load a named, validated saved world after the game starts a fresh session. The package preview now has a separate Publish module action. Publication stages and verifies the archive, library index, and immutable definition before the transaction makes all three durable and adds the exact module item to the live inventory. The Unity project company name is `SiliconSandbox`, so normal player saves use the user-approved Unity persistent-data directory. Automated Play Mode tests override storage into the repository-local test root.

Validation from the repository root with approved normal Unity licensing access:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

Result: **189 Edit Mode tests passed, 13 Play Mode tests passed, zero failed/inconclusive/skipped; macOS player build and built-player smoke check passed.** The new Play Mode checks save a real archive, reopen it with ordinary source state reset, reload the scene, browse and reopen the file by world ID, and publish a fixed module with a durable library/index/world transaction before placing and reopening an instance. Logs and XML reports are in ignored `UnityProject/Logs/Verification/`. An intermediate compilation attempt failed due to a missing `System.IO` import; the corrected full rerun passed.

Changed areas: persistence archive codec and library index/store; Bootstrap save/open/publish orchestration; Interaction pause/package controls; Project Settings company name; Edit Mode and Play Mode tests; canonical control note and autonomous decision log.

Remaining risks and unverified items: the Save/Open UI has not had a manual graphical walkthrough; a damaged world backup-selection screen and autosave/normal-exit save are still needed; nested module versions remain unsupported by the current first-playable reader; exact library recovery is covered at the archive/store level but not yet by a graphical recovery screen; the full five-step professor demonstration and performance benchmark have not run. CI has no configured runner and no run result. Windows checks remain unrun under the user's temporary waiver.
