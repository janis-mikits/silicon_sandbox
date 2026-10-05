# First-playable autonomous decisions

This log records only new game or material cross-system choices that would normally have required the user's decision. The user authorized choosing the recommended option during the extended first-playable implementation. Routine code choices are omitted; the canonical document linked for each entry governs behavior.

## 5 October 2026 — Breaking a wire when its channel must split

**Issue:** A break can leave two surviving connector pieces in one cell that previously shared one logical channel. The four-channel limit forbids leaving them as separate nets on that same channel.

**Options:** (1) Delete one extra piece or allow a fifth channel; (2) move a surviving piece to an available channel and reject the break atomically if none is free.

**Recommendation adopted:** Option 2 preserves the player's surviving geometry and the four-channel rule. Keep node/span IDs and exact face points; assign the lowest free channel in each affected cell. With no free channel, show the existing invalid-action feedback and leave the authored design unchanged. Recorded in [physical connections](../docs/physical-connections.md#authored-topology-representation).

## 5 October 2026 — CI trigger while dedicated runners are unavailable

**Issue:** The existing public-repository workflow required self-hosted Mac and Windows runners on every push. None are configured, and the user declined attaching this personal Mac. Each push would queue jobs that cannot start.

**Options:** (1) Keep automatic push runs pending and failing; (2) make the Unity workflow manual until suitable licensed runners are available, then restore automatic triggering after a real run.

**Recommendation adopted:** Option 2 keeps the workflow reviewable without implying active CI or attaching a personal machine to the public repository. The [workflow](../.github/workflows/unity-verification.yml) now uses manual dispatch only. Windows native/performance verification remains unrun until an appropriate machine exists.
