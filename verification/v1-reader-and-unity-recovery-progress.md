# Version 1 reader and native Unity recovery — 5 October 2026

The local Unity 6000.3.24f1 command-line gate is now runnable with normal Unity licensing IPC access. The stale Licensing Client process that blocked a new channel was cleared through the user's Unity Hub/Activity Monitor restart. A later restricted process still could not use that channel, but the approved Unity verification command with normal app access completed. Unity resolved the approved `com.unity.nuget.newtonsoft-json` 3.2.2 package and rewrote its lock entry with the official registry URL.

The first complete native rerun found assembly-reference and Play Mode lifecycle faults that the supplemental .NET checks could not detect. The interaction assembly no longer depends on Bootstrap for floor picking; a floor marker keeps the assembly direction acyclic. Material property blocks are created in `Awake`, and reopening teleports the CharacterController through its own reset method. The time-zero Play Mode check now asserts at the instant of reopen, before the next live frame advances simulated time.

The persistence reader accepts the first-playable version 1 world/module subset with explicit required fields, duplicate-property rejection, lowercase UUIDv4 identities, typed endpoints, pin geometry snapshots, interface agreement, and derived topology validation. The manifest reader checks its typed fields and structure. Tests include hand-written JSON expectations, malformed records, and preservation of source, connector, module-port, and instance identities. These readers do not yet connect to a complete archive/open/save UI or library recovery flow. Nested modules and future record variants are rejected explicitly; broader version 1 compatibility still needs implementation before claiming it.

Verification command, run from the repository root with approved normal Unity app access:

```sh
scripts/verify-unity.sh --editor '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity' --target StandaloneOSX
```

Final result for this revision: Edit Mode 183 total, 0 failed, 0 inconclusive, 0 skipped; Play Mode 11 total, 0 failed, 0 inconclusive, 0 skipped; macOS player build and built-player smoke check passed. The XML reports and Editor/player logs are in `UnityProject/Logs/Verification/` (ignored by Git). `git diff --check` passed. Intermediate runs failed on the identified compiler and Play Mode defects and are not counted as passing gates.

Remaining material risks and unverified work: complete ZIP archive save/reopen and recovery, durable library/package publication through the UI, native player demonstration, graphical 1,000-gate benchmark and performance acceptance, actual GitHub Actions run, and Windows checks (temporarily waived by the user). The current native gate confirms compilation and tests for the code above, not the full professor milestone.
