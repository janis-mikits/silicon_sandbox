# Current research backlog

**Updated 30 September 2026.** This is the actionable list of research questions that remain after the engine, simulator backend, module model, world foundation, Minecraft-mod investigation, rendering invariants, and renderer boundary were addressed. The [original Appendix C](original-research-backlog.md) preserves the source questions for provenance; its resolved or superseded prompts are **not** active tasks. Accepted rules live in the subsystem files linked below, not in this backlog. [Open decisions and cautions](open-decisions-and-cautions.md) and the [development readiness audit](../development-readiness-audit.md) track broader design questions, including ones that did not originate in Appendix C.

## In this file

- [Before the affected first-playable feature](#before-the-affected-first-playable-feature)
- [Choose with first-playable measurements](#choose-with-first-playable-measurements)
- [Conditional scale and performance research](#conditional-scale-and-performance-research)
- [How to close an item](#how-to-close-an-item)

## Before the affected first-playable feature

The remaining first-playable item here is implementation validation, not an unresolved foundation choice. Its accepted architecture is already specified; do not reopen it merely because code and measurements remain.

| Original group | Still unanswered | Decide or verify when | Binding context |
| --- | --- | --- | --- |
| R-02, simulation architecture | The [required first-playable component subset and internal correctness matrix](delivery-and-acceptance.md) are settled. Execute that matrix and measure simulator CPU cost and achieved simulation rate separately from rendered FPS; determine whether measured hot paths need optimization. Select a specific external-tool configuration and additional comparison cases before claiming IEEE/VCS conformance beyond the documented game subset. Derived-clock behavior and additional catalog components can be specified before those later features. | Run the correctness matrix before claiming the first-playable simulator is correct; refine performance and reference comparisons as their claims or workloads arise. | [Digital simulation](digital-simulation.md), [circuit time](circuit-time-and-clock.md), [physical connections](physical-connections.md), [performance](performance-and-platforms.md). The initial backend, rendering separation, milestone component scope, and internal test coverage are settled. |

R-03 module representation is resolved for the first playable by the [canonical version 1 data schema](first-playable-data-schema.md), [module lifecycle](modules-and-packaging.md), and [connection rules](physical-connections.md). Implement and verify the serializer, graph derivation, live instance viewer, and save/reopen behavior under those rules; later structural replacement and re-expansion details remain before their later features.

## Choose with first-playable measurements

These questions ask for a concrete implementation parameter or method. The [first-playable benchmark](performance-and-platforms.md#benchmark) and [rendering checks](performance-and-platforms.md#rendering-optimization-checks) provide the comparison method. A candidate should not become a canonical requirement just because Minecraft uses it.

| Original group | Still unanswered | Evidence or decision needed | Binding context |
| --- | --- | --- | --- |
| R-04, world representation | What horizontal and vertical render-region dimensions keep flying, editing, and region-boundary updates responsive? How many floor mesh/collider pieces best serve rendering and exact grid targeting on Windows and macOS? | Compare candidate dimensions and floor representations in the first playable; preserve the sparse authored grid, generated floor behavior, and offscreen simulation. | [World foundation](project-vision-and-world.md#world-data-and-rendering-architecture), [renderer contract](graphics-and-future-tools.md#renderer-boundary-contract), [connections](physical-connections.md). |
| R-05, textures and voxel geometry | Which combination of authored component meshes, generated block/connector geometry, shared materials or a texture atlas, and mipmap/filtering settings gives the required readable block style and best measured cost? How should UVs preserve texture appearance if neighboring opaque faces are merged? | Compare appearance, signal readability, draw work, edit/rebuild cost, and memory on representative scenes. Detailed smooth-terrain algorithms are not part of the current block-world requirement. | [Graphics visual priorities](graphics-and-future-tools.md#confirmed-visual-priorities), [physical connections](physical-connections.md), [performance](performance-and-platforms.md). |
| R-06, rendering and mesh optimization | Which concrete draw method should implement bounded regions and separate changing signal visuals: region meshes, Unity batching/instancing, or a combination? Are buffer reuse or smaller mesh updates worth their complexity for ordinary edits? | Profile main-thread submission, GPU time, allocations, region rebuild time, selection correctness, and signal-color updates in the first playable. The hidden-face, local-invalidation, signal-update, and renderer-boundary rules are already settled. | [Rendering invariants](graphics-and-future-tools.md#rendering-and-update-invariants), [renderer contract](graphics-and-future-tools.md#renderer-boundary-contract), [performance checks](performance-and-platforms.md#rendering-optimization-checks). |

The [first-playable benchmark protocol](performance-and-platforms.md#benchmark) fixes preset conditions, activity, reporting, and stutter investigation. Choose the actual Windows/Mac machines and author/freeze the saved scene before comparing R-04 through R-06 methods; these are implementation and measurement tasks, not unresolved game mechanics.

## Conditional scale and performance research

Investigate these only when a measured scene or operation identifies the relevant cost. The [mod transfer analysis](../minecraft-mod-transfer-analysis.md) ranks possible mechanisms and their correctness hazards; the mod-mechanism survey itself is complete and is not a standing research task.

| Original group | Remaining conditional question | Trigger and decision test |
| --- | --- | --- |
| R-06, rendering methods | Would greedy meshing, more detailed partial-shape culling, background region-mesh generation, or graphics-data compression improve the measured bottleneck without harming edits, targeting, or four-state presentation? | Trigger: geometry, edit stutter, or graphics memory exceeds an agreed target. Compare each method against the current renderer using the same saved world and correctness checks. |
| R-07, mod-inspired optimizations | Which, if any, of the researched mod-inspired ideas should be adopted after profiling: simulator scheduling, immutable-data sharing, visual batching, lazy visual assets, occlusion, or bulk construction? | Trigger: a corresponding simulator CPU, memory, rendering, load, or bulk-edit bottleneck. Use the specific [transfer analysis](../minecraft-mod-transfer-analysis.md); do not redo a generic survey of all fifteen mods or treat their Minecraft speedups as game benchmarks. |
| R-08, distant detail and spatial structures | Does a large authored world need distant visual LOD or an octree-like index, and would either outperform the simpler region renderer? If LOD is used, how will edits refresh it and how will nearby exact topology, state, and targeting be restored? | Trigger: measured far-view rendering or memory cost in a large player-built world. Keep the exact authored circuit authoritative. Procedural noise-based “fake terrain” does not represent player-built circuits or the specified flat floor. |

## How to close an item

Record an accepted decision in the relevant canonical subsystem file, including its correctness constraints and any benchmark evidence. Update [INDEX.md](../INDEX.md) if routing or dependencies change, then remove or narrow the answered question here. If a candidate proves unnecessary for the specified workload, record that conclusion in its research note or the readiness audit and remove it from this active list. Preserve historical wording in [Appendix C](original-research-backlog.md) rather than restoring resolved questions to the working backlog.
