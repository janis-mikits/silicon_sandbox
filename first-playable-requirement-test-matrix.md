# First-playable requirement-to-test matrix

Reviewed 30 September 2026 and approved for saving with the [implementation plan](first-playable-implementation-plan.md). This matrix defines the planned complete acceptance coverage, not a claim that every check ran. The executed slice 0 and slice 1 checks are recorded in `verification/` reports. Canonical [delivery requirements](docs/delivery-and-acceptance.md) govern; this matrix adds test design, not game rules. Reference keys match the implementation plan.

## Test levels and independent oracles

- **E:** small unit tests in Unity Edit Mode.
- **I:** Core subsystem integration, normally run in Edit Mode.
- **P:** Play Mode tests of actual Unity scene/input/render interaction.
- **B/M:** native build, manual walkthrough or measured performance.

Expected results are literal reviewed fixtures or separate mathematical calculations. Production resolver, scheduler, graph builder and serializer cannot generate their own expected answers. Comparing optimized and unoptimized paths supplements these oracles; agreement alone does not establish correctness.

## Every accepted internal correctness row

| ID / canonical row | Independent expected result | Planned tests and gate |
| --- | --- | --- |
| C01 One-bit net resolution | Empty/all-Z is Z; released drivers do not oppose known values; agreeing active known drivers retain value; opposed 0/1 or any active X gives X. | E: empty/singleton, 16 ordered two-driver pairs, duplicates, order permutations and three-driver cases. I/P: Inspect distinguishes no driver, known contention and other X causes. S, I. |
| C02 One-bit AND | Literal 16-entry AND table below. | E: every pair. I: source action propagates through authored topology with clock stopped. P: professor's five input cases and truthful colors/values. S, T. |
| C03 SR data/state | Literal tables for prior Q=0,1,X below; Q_bar is complement, with X/Z giving X. Uninitialized Q=X. | E: all 48 required cases, plus explicit prior/initial Z supported by V1; definite set/reset recovers uncertainty. I/P: hold, set/reset, invalid and uncertain controls. C, S. |
| C04 CLK transition/settling | Exactly 0→1, 0→X, 0→Z, X→1 and Z→1 sample state. | E: all 16 transitions, with distinguishable prior and next Q. I: settled connector values, cascaded edges, simultaneous sampling. P: edge/cycle steps return settled. T. |
| C05 Edits/connection identity | Hand-authored endpoint sets distinguish crossing/junction; break splits specified endpoints; unrelated nets/state unchanged. | E/I: graph fixtures, safe-boundary edits, invalid-operation rollback, split IDs/reference repair, undo IDs, tag choice, rotation. P: targeted joins/crossing shapes/picking. P, T, V1. |
| C06 Instances/offscreen | A/B: X/X→1/X→1/0→0/0. After edge 2, stop, set next inputs, leave view, restart through next rise; return 0/0. | I: instance-qualified nets/state/events. P: both render regions hidden/unloaded, return to actual settled values and Inspect. M, D, S. |
| C07 Save/reopen/reset | Save at 0/0; reopen Q=X/X, four controls Off/0, time zero, clock stopped/0 with saved frequency; 00 hold edges retain X/X. | I: exact authored fixture comparison and forbidden transient-field checks. P/M: reopen and Reset retain construction. SAVE, T, D. |
| C08 Scheduler ties/precision/limit | Same-timestamp source action is sampled by edge; exact rational 3 Hz phase; 1 GHz half-period 500 ps; overflow pauses with diagnostic. | E/I: independent rational arithmetic, tie/insertion permutations, mid-slot action queueing, frequency interval boundary, near-limit overflow and convergence fixtures. T. |
| C09 Authored round trip | Junction connected/crossing separate; exact pin locations, port internal endpoint, captured internal bounds and separately compact exterior footprint, instance IDs and versions survive. One corrupted module does not change another version. | E/I: golden records/archive, 24 orientations, UUID casing/duplicates, exact global-copy recovery, placeholders driving X, invalid snapshot backup path. Save/reopen a 3 × 2 capture whose exterior is one block and reject a missing required size field. P: correct targeting after reload. V1, P, M. |
| C10 Packaging boundary | Reject partial component/module; exact cut terminations; outside routes cannot join captured pieces; pure pass-through and unwired interior pins excluded; every detected connected candidate retained with fixed internal mapping; invalid ports publish nothing. | E/I: manually drawn routes and enumerated captured endpoints, including a padded SR selection whose unwired interior Q_bar is not exposed; source records unchanged; fresh copied IDs; invalid mapping and durable-write failure injection. P: preview agrees with capture. M, T, V1. |

## Literal logic tables

### AND: every ordered pair

| A / B | 0 | 1 | X | Z |
| --- | --- | --- | --- | --- |
| 0 | 0 | 0 | 0 | 0 |
| 1 | 0 | 1 | X | X |
| X | 0 | X | X | X |
| Z | 0 | X | X | X |

### Two-driver net resolution

This also covers every ordered pair. Test no drivers and larger driver collections separately; an AND truth table is not a net-resolution oracle.

| Driver A / Driver B | 0 | 1 | X | Z |
| --- | --- | --- | --- | --- |
| 0 | 0 | X | X | 0 |
| 1 | X | 1 | X | 1 |
| X | X | X | X | X |
| Z | 0 | 1 | X | Z |

### Positive-edge detection: all 16 transitions

| Previous / Next | 0 | 1 | X | Z |
| --- | --- | --- | --- | --- |
| 0 | No | Yes | Yes | Yes |
| 1 | No | No | No | No |
| X | No | Yes | No | No |
| Z | No | Yes | No | No |

### SR: all 48 required cases

Each literal tuple lists next Q for R=0,1,X,Z, respectively. These results follow the canonical known-input rows and possible-outcome merge, independently of production code.

| S | Prior Q=0 | Prior Q=1 | Prior Q=X |
| --- | --- | --- | --- |
| 0 | (0,0,0,0) | (1,0,X,X) | (X,0,X,X) |
| 1 | (1,X,X,X) | (1,X,X,X) | (1,X,X,X) |
| X | (X,X,X,X) | (1,X,X,X) | (X,X,X,X) |
| Z | (X,X,X,X) | (1,X,X,X) | (X,X,X,X) |

Additional V1 explicit-Q=Z checks: 00 holds Z; 01 yields 0; 10 yields 1; 11 yields X; all uncertain-control combinations yield X. Q_bar is X for Q=X or Z, otherwise the known complement. Input Z remains Z on its net even when treated as uncertainty by SR evaluation.

## Timing and state integration fixtures

- Two SR components sample the same settled pre-commit values; reversing execution iteration order cannot expose uncommitted updates.
- A newly generated clock edge executes through later delta work before a slot returns.
- A queued source action and world edge at the same time sample the new source value. An action arriving during a slot waits for the next safe boundary.
- Pause preserves time, queue, phase and state; Resume continues rather than restarting.
- Clock frequency changes finish the current interval under the old setting; later intervals use the new exact rational schedule.
- Compute 3 Hz expected absolute edge times from k/6 seconds, independently rounded at 1 ps; check a long sequence and bounded rounding error, not repeatedly rounded half-period addition.
- A later 1 GHz fixture has edges separated by 500 ps; it is an arithmetic correctness test, not a real-time throughput demand.
- Near-limit normalized time arithmetic pauses with a diagnostic and never wraps.
- Acyclic propagation through more than 1,024 gates settles without an oscillator diagnosis.
- Stable feedback, converging feedback and a synthetic zero-delay oscillator have separate fixtures. Test-only components may create the oscillator without adding a player-visible catalog component.
- Convergence guard exhaustion pauses, marks affected signals X, points to the affected area and retains construction.
- Repeat independent expected event traces under varied Unity frame durations and visible/hidden/unloaded rendering.
- Structural graph refresh preserves unaffected instance state/phase; failed edits preserve state reached at the safe boundary.

Resolve the plan's open P-stop/restart phase question before asserting an expected mid-interval restart result.

## Every professor demonstration step

| ID / step | Expected evidence | Levels and slices |
| --- | --- | --- |
| D1 Create/load flat world and build | Flat world, two sources, one-bit AND, visible connectors and explicit pin attachments; editable construction. | P and native manual walkthrough; slices 1–3. |
| D2 Show four-state changes, including offscreen | (0,1)→0; (1,1)→1; (0,Z)→0; (1,Z)→X; (1,X)→X. Accepted stateful offscreen edge still executes. | C01/C02/C06; I/P. |
| D3 Inspect a signal | B connector: one-bit Z, source OUT and gate B connected, no active driver. Y: X caused by uncertain B, not falsely contention. Selected target/net highlight correctly. | C01/C02 diagnostics; P/M. |
| D4 Package and place two independent instances | Ports S/R/CLK inputs and Q/Q_bar outputs, with exterior Q_bar left unconnected; Q_bar visible internally; no explicit initial Q; a larger selected capture still yields the smallest port-fitting exterior; exact sequence below. | C03/C04/C06/C10; I/P/M. |
| D5 Save/reopen | Authored design, layout, instances and connections preserved; runtime restarts as specified below. | C07/C09; I/P/M. |

### Required two-instance sequence

Four separate Constant Logic Sources each have configured initial Off and On value 1. Both CLK ports connect to the 10 Hz world clock. Package does not capture transient state; both initial Q values are X.

| Rising edge | A: S,R → settled Q | B: S,R → settled Q |
| --- | --- | --- |
| First | 1,0 → 1 | 0,0 → X |
| Second | 0,0 → 1 | 0,1 → 0 |
| Third | 0,1 → 0 | 0,0 → 0 |

After the second edge, stop the clock, set the third-row inputs, move until both instances are out of view, restart and run continuously through at least the next rising edge. Return and inspect A=0, B=0. Earlier observations may use manual steps.

Save after the third edge with Q=0/0. Reopen: exact AND and module designs/layout, both instances and all connections survive. Four S/R sources return Off/0; frequency retained; clock stopped at 0; simulated time zero; Q=X/X. Subsequent rising edges with S=R=0 hold X/X. Reset produces the same ordinary initial simulation state without removing construction.

## Persistence, topology and renderer supporting checks

- V1 golden fixtures preserve UUIDs, quarter-cell pins, orientations, type versions/configuration, joins, route spans, tags/colors, inventory/player settings and exact module dependency closure.
- Hash exact uncompressed bytes; reject duplicate normalized archive paths, duplicate parsed UUIDs, invalid paths, bounded-decompression violations, invalid JSON/types, unknown V1 fields/variants and unsupported newer format versions without overwriting.
- Reject dangling references, disconnected connector records, bad face geometry/channel occupancy, repeated join members, extra pin attachments and invalid bit/width mappings.
- Retain unaffected node/span IDs when splitting connector records; assign fresh surviving connector IDs and repair references atomically. Undo may restore the prior IDs.
- Validate every orientation with independent integer-coordinate examples. Rotation never stretches connectors or changes unrelated state.
- Validate module interfaces, exact versions and recursive-containment refusal. Same-named ordinary tags never join nets; instance-private connectivity never leaks between instances.
- Corrupt one embedded module: use a hash/identity-matching global exact copy, never a newer version. If unavailable, preserve validated footprint/ports/world attachments as a marked placeholder; output/inout drives X. Invalid snapshot or world topology offers backup and preserves damaged input.
- Failure injection before/after each save/package durable-publication stage preserves the last good archive and exposes no half-published library/inventory entry.
- Autosave runs every five minutes including paused simulation; retention keeps at least four newer backups before removing an older-than-20-minute backup. Normal exit saves. Recovery offers timestamped valid choices.
- Thin wires/channels/pins/floor cells remain exactly targetable after batching/rebuild. Crossing and junction appearance differ. Identity stripe never obscures signal state.
- Pin connection corridors: an SR Q-to-AND-B wire must leave Q_bar targetable from its front and connectable to AND A afterward. Block/module placement rejects an existing wire in a new pin corridor atomically; rotated pins use their outward axis; explicit touching-pin bridges still work. E: literal route/placement geometry and all 24 orientations. P: front-on ray selects Q_bar before the second connection.
- Local edits invalidate affected render regions and neighbor boundaries only; signal color changes do not rebuild unrelated static geometry. Hidden opaque faces are omitted without erasing required detail.
- Placement/removal/rotation/load/undo/redo reveal correct faces. Superseded asynchronous results, if introduced, cannot restore stale geometry or selection. Returning to an offscreen region refreshes settled values before display.

## PERF01: exact first-playable performance acceptance

Binding source: [performance protocol](docs/performance-and-platforms.md#benchmark). Planned, not measured.

The saved reference world contains exactly 1,000 one-bit AND operations: 500 standalone and ten instances of one immutable 50-AND module. Include required sources/connectors; report actual counts. The separate professor world exercises SR packaging. Freeze the reference file/hash, cell layout, camera path and complete Standard preset before comparisons.

Choose one Windows machine and one Mac; record OS/CPU/GPU/RAM and keep hardware/settings fixed. Steam-PC representativeness remains unverified without authorized current evidence.

| Check | Expected acceptance/evidence |
| --- | --- |
| Rendering settings | 1920×1080, Standard, 100% scale, VSync and frame cap off; report all engine quality options. |
| Four cases | Idle/stationary, active/stationary, idle/flying, active/flying; identical saved world and recorded camera route. |
| Duration | Ten-second warm-up, 60-second measurement for each case on each platform. |
| FPS | Average ≥60 in every case; report median, p95, p99 and max frame times, not just peak FPS. |
| Stutter | p99 above 33.3 ms requires investigation even if average passes. |
| Electrical throughput | Active circuit switches with 10 Hz world clock; achieve ten complete cycles per real second, processing all edges. Report achieved cycles/edges and simulation CPU time separately. |
| Edits | Every scripted place/break/undo after warm-up, including region boundary, meets provisional ≤100 ms; retain every latency. |
| Persistence | Manual save ≤2 seconds; load ≤5 seconds, provisionally, on same world. |
| Counts | Report connectors, harness widths if any, module instances, registers, visible objects and activity; module internals count as authored gates. |
| Visibility | Offscreen results match independent expected electrical results; return to current signals. |
| Optimization | Compare relevant sparse/dense/connector/module/activity shapes and correctness; separate CPU/GPU/frame/edit/memory/simulation measurements. |

Do not silently change scene/settings or thresholds after seeing results. Revise provisional criteria only with explicit approval and measured evidence. CI smoke builds do not substitute for this gate; no 1 GHz real-time performance acceptance exists.

## Reporting and completion

- Failed wire reconnect: after breaking only the source leg, the remaining wire must reject a new connection immediately in either click order without publishing an edit. Removing the obstruction must allow retry. An enclosed approach that passes local clearance must hit a bounded search limit. E: literal quarter-cell obstruction and enclosed pocket in a 1024 × 1024 × 256 world. P: cached invalid preview, click flash/audio, continued frames, cleared selection, and successful retry. Executed evidence: [routing freeze regression](verification/wire-routing-freeze-2026-10-09.md).

For each test/slice, record exact commands, exit/result, test count, platform/Editor/commit, changed files, report paths and remaining risks. Distinguish passed, failed, blocked and unrun. The shared verification wrappers and Windows/Mac CI design are in the implementation plan. No row is complete until its evidence exists; no CI pass is claimed without a real run.
