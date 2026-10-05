# Autosave and recovery choice progress

The built player now writes a verified autosave after each five minutes of real time while a world is open, including when the simulation is paused, and writes a separate verified save on normal quit. Manual saves remain separate. Retention removes a validated autosave only when it is older than 20 minutes and at least four newer validated autosaves remain. Damaged files are preserved; browsing the pause menu offers the last valid manual save, a valid previous manual copy if present, and the latest valid autosave with UTC timestamps. Choosing one reopens authored data at simulation time zero without replacing a damaged current file. Editor tests and the opt-in smoke/benchmark player skip automatic writes to the user's game-data directory.

Verification command:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

5 October 2026 result: **192 Edit Mode passed, 14 Play Mode passed, macOS build and built-player smoke passed**. An Edit Mode test checks damaged-current, valid-previous, latest-autosave, and retention behavior against independently specified files and timestamps. A Play Mode test corrupts the manual archive, reloads the real scene, chooses its validated autosave, checks authored identity and time-zero reopening, and confirms the damaged bytes remain in place. Logs and XML are under ignored `UnityProject/Logs/Verification/`.

Changed files: `WorldRecoveryStore.cs` and `.meta`, `WorldRecoveryStoreTests.cs` and `.meta`, `PlayableWorldBootstrap.cs`, `OneBitWorldInteraction.cs`, and `PlayableWorldPlayTests.cs`. The five-minute wall-clock timer and normal-exit callback have not been observed for five real minutes in a graphical player, and the pause-menu recovery list has not had a manual visual walkthrough. These are still unverified. CI has no confirmed run; Windows verification remains waived temporarily by the user. Performance acceptance remains open as recorded in `benchmark-reference-progress.md`.

Follow-up built-player check, 5 October 2026: the shared `scripts/verify-unity.sh` command now launches a second packaged macOS player in batch mode. That player authors an AND gate, writes and reopens the V1 archive, exits normally, and the wrapper confirms its separate exit autosave exists under ignored repository-local `UnityProject/Logs/Verification/persistence-smoke/`. The same full command passed with **192 Edit Mode, 15 Play Mode, Mac build, both built-player checks, and normal-exit autosave**. This verifies the quit callback in a built player; a five-real-minute graphical autosave interval and recovery screen visual walkthrough remain unrun.
