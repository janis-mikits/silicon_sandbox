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

## 5 October 2026 — Clock edge rounding at an exact half picosecond

**Issue:** The accepted clock rule says to round each rational edge time to the nearest picosecond, but an edge exactly halfway between two timestamps has two equally near results. At certain valid frequencies, that choice changes when an edge is processed by one picosecond.

**Options:** (1) Always round ties upward; (2) round ties to the even picosecond.

**Recommendation adopted:** Option 2 avoids a systematic upward bias while remaining deterministic. Every edge is still rounded from its absolute rational time, so interval rounding cannot accumulate drift. Recorded in [circuit time](../docs/circuit-time-and-clock.md#first-playable-scheduler-contract).

## 5 October 2026 — Initial CLK value versus a live clock transition

**Issue:** An unconnected SR CLK pin initially resolves to Z. Treating initialization as a live 0→Z transition would sample the flip-flop and could overwrite its authored initial Q before the player runs the circuit.

**Options:** (1) Treat initial net resolution as an edge; (2) settle initial nets and establish each component's previous CLK value without sampling.

**Recommendation adopted:** Option 2 preserves authored initialization and leaves the accepted 0→Z positive-edge rule intact for later live transitions. It applies to new world, reopen, Reset Simulation, and a newly placed edge-triggered component at a settled edit boundary. Existing components keep their prior CLK observations. Recorded in [circuit time](../docs/circuit-time-and-clock.md#clock-and-event-model).

## 5 October 2026 — Which split clock-stub piece keeps the Net Link

**Issue:** The accepted version 1 `netLink` record has route nodes but no separate anchor field. If a break splits a world-clock stub and both pieces keep the link name, the break fails electrically because matching stubs immediately rejoin.

**Options:** (1) Add a required anchor field to the accepted schema; (2) delete all surviving geometry on a split; (3) treat `nodes[0]` as the saved physical link anchor and convert detached pieces to ordinary wires.

**Recommendation adopted:** Option 3 preserves the accepted schema and the surviving geometry while making the break electrically real. Only the piece containing the first node retains the clock-link identity; detached pieces receive normal split IDs and no link fields. Recorded in the [version 1 schema](../docs/first-playable-data-schema.md#connector-geometry-and-joins).

## 5 October 2026 — Clock phase when stopped and restarted

**Issue:** The accepted clock rules specify stopping, resuming, stepping, and a running frequency change, but not whether a partly elapsed interval survives a stop or what a new frequency does to that frozen remainder.

**Options:** (1) Restart a full half-period on every resume; (2) preserve the remaining simulated time on ordinary stop/resume, but restart a full interval at the new frequency if the setting changes while stopped.

**Recommendation adopted:** Option 2 makes P behave like a pause of the clock phase and ensures a stopped frequency change takes effect on the very next interval. A manual step consumes an edge and leaves the clock stopped at the start of the next interval. Recorded in [circuit time](../docs/circuit-time-and-clock.md#clock-and-event-model).

## 5 October 2026 — Component placement in a connector cell

**Issue:** The first-playable documents define four connector channels per cell and complete component footprints, but do not explicitly say whether a one-cell component can be placed on top of an already routed wire cell.

**Options:** (1) Allow overlap and define per-shape collision checks now; (2) reject placement into any cell containing connector route geometry while allowing face attachment from adjacent cells.

**Recommendation adopted:** Option 2 gives the first-playable placement preview a clear all-or-nothing occupancy rule without moving or hiding an existing wire. It leaves the four-channel rule for connector-only cells intact. Recorded in [physical connections](../docs/physical-connections.md#pins-and-targeting).

## 5 October 2026 — Source Configure versus live On/Off state

**Issue:** The source specification separates saved initial On/Off from transient runtime On/Off, but does not explicitly say whether changing initial On/Off in Configure immediately flips a running source.

**Options:** (1) Apply the new initial state immediately; (2) preserve the current runtime On/Off choice and use the new initial state only on load or Reset Simulation.

**Recommendation adopted:** Option 2 keeps the meaning of “initial” consistent with save/reopen and avoids a surprise live toggle during configuration. A new configured On value still changes the present drive when the source is On. Recorded in [components and timing](../docs/components-and-rtl-timing.md#sources-on-load-and-reset).

## 5 October 2026 — Source action during a settled simulation pause

**Issue:** Structural edits leave simulation safely paused, but the accepted rules do not specify whether a right-click on a source during that pause changes its present value.

**Options:** (1) Defer the action until Resume; (2) settle its combinational effect immediately at the current simulated time without stepping the clock.

**Recommendation adopted:** Option 2 keeps the source usable immediately after placement and does not invent a clock edge. The authored initial state is unaffected. Recorded in [circuit time](../docs/circuit-time-and-clock.md#safe-pause-and-editing).
