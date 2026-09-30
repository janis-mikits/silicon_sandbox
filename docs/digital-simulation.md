# 1 Digital simulation and signal values

## In this file

- [Reference and scope](#reference-and-scope)
- [Runtime architecture decision](#runtime-architecture-decision)
- [Derived graph refresh after structural edits](#derived-graph-refresh-after-structural-edits)
- [Four state behavior](#four-state-behavior)
- [Visuals](#visuals)
- [Electrical boundary](#electrical-boundary)
- [Optimization fidelity](#optimization-fidelity)
- [Verification reference and limits](#verification-reference-and-limits)

## Reference and scope

SiliconSandbox is a digital circuit sandbox whose relevant behavior is guided by IEEE 1800-2023 SystemVerilog. Synopsys VCS is the primary commercial simulator for comparison, and Synopsys Verdi is the confirmed design reference for a future waveform and debugging interface. The earlier choice of QuestaSim reflected the user’s Purdue experience and was replaced to make the game more relevant to industry practice. This does not assert that NVIDIA exclusively uses VCS, and neither VCS nor Verdi must be bundled with the game. The original document left the implementation backend open; the [runtime architecture decision](#runtime-architecture-decision) below records the user's subsequent choice. A particular VCS setting or extension does not silently define game behavior.

The confirmed vendor-protection rule is:

> Where VCS behavior and IEEE 1800-2023 appear to differ, first determine whether VCS is using an optional feature, command-line setting, or proprietary extension. SiliconSandbox should follow standard SystemVerilog behavior unless the design document explicitly adopts the VCS-specific behavior.

## Runtime architecture decision

**Decision, 29 September 2026:** Start with a purpose-built, event-driven, four-state simulator in C# as the game's primary runtime backend. Represent the circuit's topology, values, scheduled events, and module-instance state as simulation data independent of Unity's visible GameObjects and rendering. Unity handles world interaction and presentation; an object being outside the camera or culled from rendering does not remove it from simulation. The simulation's event ordering and results must not depend on rendered frame rate. This reinforces the existing [offscreen and clock rules](circuit-time-and-clock.md#clock-and-event-model). A graph/netlist representation is useful for that separation; its exact schema and incremental-edit algorithm remain implementation choices. External HDL tools, including VCS where available, may be used for focused verification, but are not the selected shipped runtime.

The user chose C# after reviewing a custom C++ native plug-in, Verilator, Icarus Verilog, and Unity Burst as a possible optimization. C++ remains an option if actual measurements justify it; choosing C# now does **not** claim that it can meet every future large-circuit workload. Smooth play with large, complex circuits is a major priority, while real-time execution at a simulated GHz clock is not required. See [performance](performance-and-platforms.md#platforms-and-test-machine) and the [research record](../simulation-backend-research.md).

Replacing a C# simulator with C++ later would be a **substantial rewrite of the simulator**, not an automatic or easy translation. A clear boundary can preserve much of Unity's world, UI, and visual code, as well as the circuit rules, saved-data contract, and verification cases; the event scheduler, four-state logic, runtime state handling, native bindings, and cross-platform builds would still need porting and full retesting. The user accepted this tradeoff when choosing C#. Do not present later migration as cost-free. The chosen design must be measured against the first-playable benchmark and later defined large-circuit workloads before asserting adequate performance.

## Derived graph refresh after structural edits

**Decision, 30 September 2026:** The authored design remains authoritative. After a valid structural edit reaches its [safe boundary](circuit-time-and-clock.md#safe-pause-and-editing), refresh the derived simulation graph so it matches the committed authored revision before simulation resumes. For the first playable, a complete graph rebuild is permitted only if ordinary place, break, and undo meet the provisional [100 ms end-to-end response target](performance-and-platforms.md#benchmark) on the professor reference world. Measure the complete edit response, not only the graph step. If a complete rebuild misses that target, use a more local rebuild or update for the affected graph before accepting the milestone implementation. Either method must produce the same correct connectivity and instance separation, including offscreen circuits; rendering regions do not bound electrical updates. Keep graph derivation replaceable so later large-circuit measurements can justify a different update method. The exact algorithm and data layout remain implementation choices.

## Four state behavior

Every signal bit can be 0, 1, X (unknown), or Z (high impedance/no active driver). An ordinary undriven net resolves to Z. A stored four-state value without an explicit initialization or reset starts at X, subject to the separate nonvolatile-memory persistence rule. Conflicting active 0 and 1 drivers of equal strength resolve to X; a driver that releases Z does not fight an active value. The first implementation does not model drive strengths, so active drivers are equal strength. With no active driver the bit is Z; with only known agreeing active drivers it is their common 0 or 1; if an active driver is X or known active drivers disagree, the resolved bit is X. Logic handles X and Z with SystemVerilog-style truth tables and should retain a known result where the result is certain: 0 & Z = 0, while 1 & Z = X. A disconnected input must not be silently forced to zero. Inspection should distinguish known causes of X, such as contention or uninitialized state, from unknown causes.

## Visuals

Logic 0 is muted blue, logic 1 bright green, X pulsing red, and Z gray. These are game presentation choices, not colors mandated by IEEE or by a simulator. The single short red invalid-placement flash must look different from the repeating red pulse for X. A multibit connector retains per-bit values; its detailed packed-vector view must make mixed values visible.

## Electrical boundary

The core game does not simulate analog voltage, current, resistance, thresholds, power consumption, transistor behavior, or physical power/ground delivery. Sources drive logic values; names such as Constant Logic Source, Clock Source, and Pulse Source replace misleading “voltage source” wording. A later advanced electrical mode is only a possibility. Document exact game component rules separately from claims that IEEE mandates them; the game’s equal-strength simplification is one example.

## Optimization fidelity

**Decision, 30 September 2026:** An implementation may cache, batch, compile, or replace the derived execution graph for speed only if the same authored circuit remains visible and inspectable and the supported game rules yield the same settled four-state values at every authored pin, connector bit, module port, and internal viewer point. Preserve driver-conflict versus undriven/uncertain causes where the specified diagnostics expose them; preserve ordered clock-edge detection, sequential sampling, delta settling, convergence reporting, and independent per-instance state. An optimization may not merge two instances' mutable state, infer a join from geometry, skip offscreen events, remove a player-authored part from the viewer, or change authored gate/structural counts used by performance and teaching comparisons. Compare optimized and reference paths on the [first-playable correctness matrix](delivery-and-acceptance.md#first-playable-internal-correctness-matrix), including edits and save/reopen, before accepting the optimization. CPU time and frame time may change; modeled circuit behavior and authored layout may not.

## Verification reference and limits

IEEE 1800-2023 and the documented game-component subset replace the original IEEE 1076 VHDL comparison target. Validate the supported logic and event behavior using known input/output sequences, truth tables, Karnaugh-map examples, and reference circuits compared with the specified VCS configuration. Preserve the original plan to run verification at development milestones and use IEEE 1012 as a verification-and-validation process reference; naming that standard is not a claim of completed compliance. The supported construct list, detailed conformance tests, and applicable IEEE 1012 edition and obligations still need definition.

Reference pointers from the discussion: IEEE 1800-2023 https://standards.ieee.org/ieee/1800/7743/ ; VCS https://www.synopsys.com/verification/simulation/vcs.html ; Verdi https://www.synopsys.com/verification/debug/verdi.html ; original IEEE 1012 reference https://standards.ieee.org/ieee/1012/5609/ . The superseded VHDL reference was https://standards.ieee.org/ieee/1076/5179/ . Verify current authoritative editions and vendor settings before claiming conformance; these pointers do not settle the research backlog.
