# 12 Delivery and acceptance

## In this file

- [First playable professor milestone](#first-playable-professor-milestone)
- [First playable internal correctness matrix](#first-playable-internal-correctness-matrix)
- [First public release](#first-public-release)
- [Later additions](#later-additions)
- [Proposed verification checklist derived from confirmed rules](#proposed-verification-checklist-derived-from-confirmed-rules)

## First playable professor milestone

The narrow milestone is a working 3D flat-world experience in which the player can build a small single-bit circuit, run four-state event-driven simulation, inspect behavior, package a stateful circuit, place two independent instances, and save/reopen without losing the design. The user presents it to a professor and can teach the controls directly. Harness concatenation, module-version migration, physical re-expansion, persisted undo history, and NVM may arrive after this milestone. This staging does not rescind their first-public-release specifications. The milestone must also meet the first-playable frame-rate target defined in [Section 10](performance-and-platforms.md).

**Required first-playable component subset, 30 September 2026:** The milestone requires the built-in world clock, one-bit Constant Logic Sources, visible one-bit connectors, a one-bit AND gate, and a one-bit SR flip-flop. These support the accepted logic, packaging, inspection, and independent-instance demonstrations below. Other catalog components may follow after the first playable; their absence from this milestone does not remove them from the full game specification. The [physical connection](physical-connections.md) and [four-state simulation](digital-simulation.md) rules still govern this subset.

For placed components and modules, the first playable needs only vertical-axis 90-degree rotation controls, while the [authored/save representation supports all 24 proper grid-aligned orientations](physical-connections.md#post-placement-rotation).

**Professor demonstration choices, 30 September 2026:** Use the one-bit [SR flip-flop](components-and-rtl-timing.md#sr-storage) as the stateful element in the circuit packaged for the two-instance demonstration. Expose one-bit S, R, and CLK input ports and a one-bit Q output port. Connect CLK on each placed instance to the [world clock](circuit-time-and-clock.md#clock-and-event-model); give each instance independent S and R controls. Q_bar remains available in the internal viewer without requiring an external port for this demonstration. Do not configure an explicit initial Q value for the demonstration, so ordinary uninitialized storage starts at X under the [four-state rule](digital-simulation.md#four-state-behavior). These demonstration choices do not change the SR flip-flop's canonical behavior or require it to serve as the separate four-state logic demonstration.

**First-playable internal inspection choice, 5 October 2026:** The one-level professor package can show its selected instance's live internal component pins, including Q and Q_bar, in a read-only scrollable inspection panel reached by targeting the module body and pressing I. Values come from that instance's settled simulation nets, not from the fixed blueprint or another placement. This milestone UI does not yet navigate nested child instances; the broader live-viewer and nested-navigation rule in [modules and packaging](modules-and-packaging.md#what-is-captured) remains for later module depth support.

**Professor clock choice, 30 September 2026:** Run the world clock at 10 Hz for this demonstration. This demonstration setting is not a general maximum clock frequency for the first playable or later game. The first playable need not demonstrate or meet a real-time throughput target at a simulated 1 GHz; the simulator's time model must still leave a sound path to representing a 1 GHz clock later, as specified in [circuit time](circuit-time-and-clock.md#clock-and-event-model).

**S/R demonstration controls, 30 September 2026:** Use four separate one-bit Constant Logic Sources: one S and one R source for each of the two module instances. Configure each source's initial On/Off state to Off and its On value to 1, so Off drives 0 and On drives 1 under the [source rule](components-and-rtl-timing.md#sources-on-load-and-reset). The player can operate each source independently to apply the accepted edge sequence. Loading the world or using Reset Simulation restores all four sources to their configured Off state. Source-body operation and free-pin placement follow the accepted [right-click precedence](building-and-interface.md#default-controls).

**Accepted two-instance acceptance sequence:** After packaging and placing instances A and B, observe Q = X in each before a rising clock edge. Apply the following inputs before each indicated rising edge; observe each Q after that edge and its event consequences settle. This table specifies the demonstration's expected behavior, including independence and hold behavior.

| Rising world-clock edge | Instance A: S, R → Q | Instance B: S, R → Q |
| --- | --- | --- |
| First | 1, 0 → 1 (set) | 0, 0 → X (hold) |
| Second | 0, 0 → 1 (hold) | 0, 1 → 0 (reset) |
| Third | 0, 1 → 0 (reset) | 0, 0 → 0 (hold) |

**Offscreen acceptance check, 30 September 2026:** After the second rising edge, verify A Q = 1 and B Q = 0. Stop the world clock, set A to S = 0, R = 1 and B to S = 0, R = 0, then move until both placed instances are out of view. Start the world clock and let it run continuously through at least the next rising edge while they remain out of view. On returning, verify A Q = 0 and B Q = 0. Graphics visibility must not determine whether those edges were simulated. The earlier edge observations may use manual stepping for repeatability.

**Professor logic-circuit choice, 30 September 2026:** The separate one-bit four-state demonstration uses two one-bit sources connected by visible connectors to a one-bit AND gate. Observe the settled output Y after each input change:

| A | B | Expected Y | Demonstrated behavior |
| --- | --- | --- | --- |
| 0 | 1 | 0 | Known zero |
| 1 | 1 | 1 | Known one and response to an input change |
| 0 | Z | 0 | Known zero determines the result despite the released input |
| 1 | Z | X | Result is uncertain with the released input |
| 1 | X | X | Unknown input propagates |

B may be configured to release its net as Z under the [source rule](components-and-rtl-timing.md#sources-on-load-and-reset). Inspect shall show B = Z in the Z cases and Y = X in the fourth case without falsely labeling that X as a driver conflict. This sequence observes all four values across circuit nets; it does not require the AND output to equal Z.

**Save/reopen acceptance check, 30 September 2026:** Save after the third rising edge of the accepted two-instance sequence, when both Q outputs are 0. Reopen the world and verify that the one-bit AND circuit, exact packaged module design and 3D layout, both placed instances, and their connections remain. All four S/R Constant Logic Sources return to their configured initial Off state and drive 0. The world clock retains its saved frequency but starts stopped at level 0. Ordinary simulation restarts at time zero rather than restoring the saved live state; without explicit flip-flop initialization, both Q outputs are X instead of the pre-save 0. Subsequent rising edges with S = R = 0 retain X. This check does not require NVM or persisted undo, which the [milestone scope](#first-playable-professor-milestone) permits after the first playable.

**Inspection acceptance check, 30 September 2026:** In the AND circuit with A = 1 and B = Z, target B's connector. Quick look and detailed Inspect must show a one-bit net with value Z; detailed Inspect identifies its actual connections to source B's output and the AND gate's B input, and reports no active driver because the source releases the net. Then target Y's connector. Inspect must show X and identify the uncertain B input as the known cause, without falsely labeling the result a driver conflict. Follow the [inspection rules](inspection-and-diagnostics.md#quick-look-and-detailed-inspection) for highlighting and detail presentation.

The agreed professor demonstration is a five-step acceptance path:

1. Create or load a flat world and build a small one-bit circuit.
2. Show output changes under four-state rules when inputs change, including while the circuit is out of view.
3. Inspect a signal’s value and actual connections.
4. Package a stateful circuit, place two instances, and show that their internal states are independent.
5. Save and reopen; show that geometry and module design persist while ordinary simulation restarts from the defined initial state.

## First playable internal correctness matrix

**Accepted verification scope, 30 September 2026:** The professor demonstration above is accompanied by internal checks of the required first-playable component subset. These checks verify the existing subsystem rules; they do not add player-visible components or replace the separate [performance benchmark](performance-and-platforms.md#benchmark). Simulator-level tests may drive a CLK net directly to cover four-state transitions that the ordinary 0/1 world clock does not generate.

| Area | Check and expected result | Binding rule |
| --- | --- | --- |
| One-bit net resolution | An undriven net is Z; a Z/released driver does not override an active 0 or 1; agreeing active known drivers retain that value; opposed active 0 and 1 or any active X resolve to X. Inspect distinguishes no active driver, known contention, and other known causes of X. | [Four-state behavior](digital-simulation.md#four-state-behavior); [inspection](inspection-and-diagnostics.md#common-faults). |
| One-bit AND | Check every ordered pair from {0, 1, X, Z} × {0, 1, X, Z}. A known 0 on either input makes Y = 0; 1 & 1 makes Y = 1; every remaining pair makes Y = X. A live source change propagates and settles even with the world clock stopped. | [Four-state behavior](digital-simulation.md#four-state-behavior); [clock model](circuit-time-and-clock.md#clock-and-event-model). |
| SR flip-flop data and state | With prior Q = 0, 1, and X, check every S/R pair from {0, 1, X, Z} × {0, 1, X, Z} at a rising edge against the [known rows and possible-outcome merge](components-and-rtl-timing.md#sr-storage). In particular, 00 holds, 01 resets, 10 sets, and 11 yields X; Q_bar follows the specified four-state complement. Without explicit initialization, Q begins X. | [SR storage](components-and-rtl-timing.md#sr-storage); [initialization](digital-simulation.md#four-state-behavior). |
| CLK transition detection and settling | Exercise all 16 ordered CLK transitions among 0, 1, X, and Z. Exactly 0→1, 0→X, 0→Z, X→1, and Z→1 trigger the SR flip-flop; the others do not. Check the post-edge Q and connector values only after the current time slot and zero-time consequences settle. Manual edge/cycle stepping also returns after settling. | [Positive-edge detection](circuit-time-and-clock.md#four-state-positive-edge-detection); [event model](circuit-time-and-clock.md#clock-and-event-model). |
| Edits and connection identity | A crossing without a targeted junction remains separate; a targeted junction joins one-bit nets; breaking a segment can split a net and recalculate each part. Structural edits reach a safe pause boundary before altering the graph; placement or removal does not change an unrelated net. | [Physical connections](physical-connections.md#junctions-and-crossings); [safe editing](circuit-time-and-clock.md#safe-pause-and-editing). |
| Instances and offscreen operation | Two placed copies of one fixed SR module version have independent Q and internal net state under the accepted [three-edge sequence](#first-playable-professor-milestone). While both are out of view, the next world-clock edge still changes A as specified; on return, visuals and Inspect show settled current values. | [Module representation](modules-and-packaging.md#canonical-design-and-runtime-representation); [offscreen check](#first-playable-professor-milestone). |
| Save, reopen, and reset | Run the accepted [save/reopen sequence](#first-playable-professor-milestone): exact authored circuit, layout, versions, instances, and connections survive; transient Q, time, events, and clock-running state restart under the specified rules. Reset Simulation produces the same ordinary initial simulation state without removing construction. | [Saving](saving-and-recovery.md#saved-design-versus-transient-simulation); [reset](circuit-time-and-clock.md#clock-and-event-model). |
| Scheduler ties, precision, and limit | A source change and world-clock edge at one timestamp sample the new source value. A 3 Hz clock maintains its rational long-run phase without repeatedly rounded half-period drift; the 1 ps representation distinguishes a 1 GHz clock's 500 ps half-period without demanding real-time throughput. A synthetic near-limit timestamp pauses with a diagnostic rather than wrapping. | [First-playable scheduler](circuit-time-and-clock.md#first-playable-scheduler-contract). |
| Authored-record round trip | Save and reopen a targeted junction and a crossing without a join; the former stays connected and the latter separate. Preserve exact component pin positions, a module port's internal endpoint, two placements' instance IDs, and the module's spatial layout. Corrupt one embedded module entry and verify exact global-copy or placeholder recovery without changing another version. | [Version 1 schema](first-playable-data-schema.md); [physical topology](physical-connections.md#authored-topology-representation); [module recovery](modules-and-packaging.md#library-and-world-independence). |
| Packaging at a region boundary | A partially selected component/module cannot be packaged. A wire crossing the selection boundary and connected to an inside part produces an exact inside route termination and port candidate without changing the source world. A route leaving and re-entering the region does not silently connect its two captured pieces through omitted outside geometry; a pure pass-through wire with no inside connection is excluded. Reject an invalid port mapping without publishing a partial definition. | [Packaging extraction](modules-and-packaging.md#packaging-region-and-ports); [atomic package](circuit-time-and-clock.md#safe-pause-and-editing). |

Use independent expected-result tables or reference calculations for these tests; do not treat the simulator's own output as its oracle. Focused IEEE/VCS comparisons may supplement the matrix under the [verification reference limits](digital-simulation.md#verification-reference-and-limits). The exact tool configuration and broader language/component conformance claims remain to be defined before making those claims.

## First public release

Complete the agreed core building, simulation, components, module, debugging, persistence, and performance experience in Sections 1–10, for Windows and macOS, with a short player introduction. “Complete” here still respects the documented proposals and open details, which need resolution during design and implementation. The full education curriculum is not required at this stage.

## Later additions

Full education mode, waveform/advanced EDA tools, physical implementation timing, automatic routing, and multiplayer are later scope. Optional checkpointing, richer NVM durability/migration, and clearly illustrative update animations also belong to future exploration rather than the core commitment.


## Proposed verification checklist derived from confirmed rules

Additional acceptance checks for the broader release should include a split net after breaking a segment; a crossing that remains unconnected until explicitly joined; bitwise equal-width harness junction versus Shift-concatenation; a failed width mismatch with one red flash and quiet click but no automatic text; safe pause while an event slot settles; nonconvergent feedback stopped with affected values X; exact module version pinning and compatible deliberate upgrade; missing-module recovery; nonvolatile memory surviving load/reset while volatile state restarts; and no electrical change when the circuit leaves the camera view. A packaged circuit must reproduce its original logic and event behavior, without a claim of physical gate or wire delay. Undo should restore a deleted object and its connections within the five-minute edit-history window.
