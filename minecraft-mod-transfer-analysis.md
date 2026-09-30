# Minecraft optimization methods: transfer analysis for SiliconSandbox

**Research and recommendation record — 29 September 2026.** This evaluates the fifteen mods in [the mechanism research](minecraft-mod-mechanisms-research.md) against SiliconSandbox. It is **not a canonical design decision or an implementation plan already approved by the user**. The [canonical files](INDEX.md) govern the game; this file identifies candidate techniques, boundaries, and evidence needed before adopting them. The mods themselves are Minecraft mods and cannot simply be installed into a Unity game. Their reported Minecraft speedups are not SiliconSandbox benchmarks.

## Navigation

1. [How to read the judgments](#how-to-read-the-judgments)
2. [Project invariants and performance evidence](#project-invariants-and-performance-evidence)
3. [Comparison at a glance](#comparison-at-a-glance)
4. [Detailed mod-by-mod analysis](#detailed-mod-by-mod-analysis)
5. [Cross-cutting implementation guidance](#cross-cutting-implementation-guidance)
6. [Recommended order of investigation](#recommended-order-of-investigation)
7. [Source and decision boundaries](#source-and-decision-boundaries)

## How to read the judgments

- **Already required** means the useful principle is present in the [world](docs/project-vision-and-world.md#world-data-and-rendering-architecture), [graphics](docs/graphics-and-future-tools.md#rendering-and-update-invariants), or [simulation](docs/digital-simulation.md#runtime-architecture-decision) specification. This analysis does not add another requirement.
- **Early candidate** means a plausible implementation approach worth evaluating during the first playable, if its relevant cost appears. It is not a commitment to a specific Unity API or data structure.
- **Profile first** means the technique may help at larger scale or with a measured bottleneck, but adding it now risks more complexity than benefit.
- **Later scope** means it serves a feature outside the professor milestone or first public release.
- **No useful direct transfer** means its Minecraft-specific mechanism does not address a current SiliconSandbox workload, though a broad design lesson may remain.

The cited mod mechanisms are documented in [the companion research](minecraft-mod-mechanisms-research.md), which links to each author's pages, wiki, or source code. All SiliconSandbox analogs below are **inferences and recommendations**, not mod-author claims. Unity-specific facts are cited to Unity 6.3 documentation where used. Final adoption should be based on the game's own measured builds and correctness checks.

## Project invariants and performance evidence

Any candidate must preserve all of these:

| Domain | Non-negotiable existing behavior | Canonical source |
| --- | --- | --- |
| Simulation | Four-state 0/1/X/Z resolution, complete clock/event processing and ordering, offscreen simulation, stable pause/edit boundaries, no silently skipped edges. Rendered frames do not define simulation time. | [Digital simulation](docs/digital-simulation.md); [circuit time](docs/circuit-time-and-clock.md) |
| Authored designs | The exact 3D layout and electrical topology remain available; packaging freezes a version; each placed copy has independent mutable state. Optimizers must not simplify away the player-built structure shown in inspection. | [Modules](docs/modules-and-packaging.md) |
| Interaction | A thin wire, its individual segment and logical channel, pins, crossings, junctions, Net Links, and floor grid cells remain visually understandable and precisely targetable. Mesh merging cannot redefine hitboxes or connectivity. | [Physical connections](docs/physical-connections.md); [graphics](docs/graphics-and-future-tools.md#rendering-and-update-invariants) |
| Saving | Saved authored content, exact referenced module versions, per-instance NVM, undo history, and recovery behavior remain correct; ordinary transient state follows reset/load rules. | [Saving](docs/saving-and-recovery.md) |
| Presentation | Visible graphics show the latest settled state, including X pulse and Z; local geometry edits expose faces correctly; signal-color changes do not rebuild unrelated static geometry. | [Graphics](docs/graphics-and-future-tools.md#rendering-and-update-invariants); [inspection](docs/inspection-and-diagnostics.md) |
| Platforms | Windows and macOS are supported. A hardware-specific fast path cannot become the only path. | [Performance](docs/performance-and-platforms.md) |

Use the existing [benchmark and rendering checks](docs/performance-and-platforms.md#benchmark): 60 rendered FPS at 1080p for the saved reference scene with 1,000 equivalent one-bit gates or fewer, tested with idle/active circuits, stationary/flying camera, editing, and visible/offscreen cases. Record frame-time distribution, simulator CPU time and achieved event/clock throughput, main-thread rendering cost, GPU time, edit/rebuild latency, and memory **separately**. The provisional ordinary place/break/undo target is 100 ms on that benchmark. Large-circuit scale and a numerical acceptance target remain open. A simulated GHz clock does not require a billion real-time cycles per second, but no implementation may silently drop events to seem faster.

The most diagnostic scene shapes are dense opaque blocks, many exposed thin connectors, sparse scattered components, repeated nested modules, rapidly changing signal colors, region-boundary edits, and large offscreen circuits. A technique should be compared to the same saved scene, settings, and machine, with correctness checks before and after. A profiler result may show that a mod's intended bottleneck is absent here.

## Comparison at a glance

| Mod | Useful SiliconSandbox analog | Primary cost it could reduce | Judgment |
| --- | --- | --- | --- |
| [Voxy](#voxy) | Derived distant visual levels of detail with local invalidation | Far-view GPU work/memory | Profile first, later scale |
| [Nvidium](#nvidium) | Optional GPU-driven culling/drawing fast path | Large-scene submission cost | No direct first-playable transfer |
| [Sodium](#sodium) | Bounded region meshes, safe face omission, visible-region submission, buffer reuse | Rendering CPU/GPU and edit stutter | Core principles already required; backend techniques early candidates |
| [Lithium](#lithium) | Event queues, indexed adjacency, change tracking, allocation control | Simulator CPU/garbage collection | High-value early candidate |
| [ModernFix](#modernfix) | Selective lazy *visual* asset loading and startup/memory profiling | Startup time and asset RAM | Profile first |
| [FerriteCore](#ferritecore) | Share immutable module/geometry data and compact repeated lookup data | RAM and allocation pressure | High-value early candidate |
| [Entity Culling](#entity-culling) | Graphics-only occlusion beyond ordinary view culling | Hidden-object draw work | Basic view culling already required; deeper occlusion profile first |
| [MoreCulling](#moreculling) | Material- and shape-aware face omission | Dense-world overdraw/mesh size | Simple opaque case required; partial-shape case profile first |
| [Iris](#iris) | Deliberate shader/pipeline compatibility and quality testing | No inherent speed gain | No optimization to adopt now |
| [ImmediatelyFast](#immediatelyfast) | Batch/collapse repetitive dynamic visuals and HUD work | CPU draw submission/UI cost | Profile first; early if scene is submission-bound |
| [Sodium Extra](#sodium-extra) | Optional presentation quality settings | Optional visual GPU/UI work | Later or profile first; never suppress logic visuals |
| [Reese's Sodium Options](#reeses-sodium-options) | Searchable, reversible performance-settings UI | Usability, not runtime work | Later scope |
| [C2ME](#c2me) | Versioned background region build and data I/O | Load/edit stalls | Profile first; correctness-sensitive |
| [Noisium](#noisium) | Bulk authored-data construction with one complete commit/invalidation | Large paste/load work | Later bulk-edit candidate; noise-generation part irrelevant |
| [Krypton](#krypton) | Network batching and serialization profiling | Future multiplayer network CPU | Later scope only |

## Detailed mod-by-mod analysis

### Voxy

**What transfers:** Voxy builds hierarchical, lower-detail terrain representations from authoritative chunk data, updates parent levels after local changes, and stores repeated values compactly ([mechanism and author sources](minecraft-mod-mechanisms-research.md#voxy)). SiliconSandbox could derive **distant graphics** from its sparse authored grid and exact module layouts. Nearby selected circuitry would still use exact geometry. A changed region would invalidate only the affected distant representation and relevant neighbors/ancestors. Repeated material or geometry identifiers could be stored compactly in derived render data.

**Where it helps:** Flying over a very large world may expose far more geometry than a local ground view. A bounded distant representation could reduce rendered vertices, draw work, and memory if profiling shows these costs dominate. It is not a simulator optimization; the full graph and per-instance state continue to run out of view.

**Where it can break the game:** SiliconSandbox's distant objects can contain meaningful wire topology and live signal colors. A coarse LOD could make a crossing look joined, hide an X, or cause a click on approximate geometry to select the wrong exact connector. Keep target selection tied to exact authored geometry or withhold fine selection until exact geometry is loaded; whichever interaction is eventually chosen must preserve the existing targeting contract. Reloaded/returned graphics must reflect current settled values and recent edits, not a stale cached color. Do not copy Voxy's explored-terrain cache semantics as authoritative world storage; world saves require exact authored designs and embedded module versions.

**Decision gate:** **Profile first, later scale.** Test a far-view large circuit scene and memory use on Windows and Mac. Compare total render/frame time, graphics memory, edit-to-visible latency, and correctness of close-up transition and selection. The professor milestone need not implement a distant LOD system just because this technique exists. Dependencies: [world](docs/project-vision-and-world.md#world-data-and-rendering-architecture), [connections](docs/physical-connections.md), [simulation](docs/digital-simulation.md).

### Nvidium

**What transfers:** Its broad idea is to use a separate GPU-driven terrain backend that filters work before drawing, with a supported-hardware fallback ([mechanism and author sources](minecraft-mod-mechanisms-research.md#nvidium)). A future SiliconSandbox renderer might test GPU-based culling or indirect/instanced drawing for enormous repeated geometry. Its NVIDIA OpenGL mesh-shader implementation is not a portable Unity solution.

**Where it helps:** If measured CPU draw submission or visibility preparation dominates large, dense scenes, moving some visibility and repetition handling to the GPU could help. It does nothing directly for event-queue throughput, save operations, or module-state correctness.

**Where it can break the game:** The mod's author limits its path to specific NVIDIA GPUs, and [Unity 6.3's draw-call guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html) depends on the selected render pipeline. Requiring Nvidium's hardware would conflict with the game's Mac support and ordinary-PC aim. A GPU-only representation must not own topology, picking IDs, or electrical state. A fallback should yield the same visible and selectable circuit, not a different gameplay mode.

**Decision gate:** **No direct first-playable transfer.** Choose and profile an ordinary cross-platform Unity renderer first. Consider an optional hardware fast path only after a repeated, demonstrated submission bottleneck, verified API/hardware coverage, and equivalent fallback tests. Dependencies: [platforms](docs/performance-and-platforms.md), [graphics](docs/graphics-and-future-tools.md), [connections](docs/physical-connections.md).

### Sodium

**What transfers:** Sodium's render-section compilation, safe face tests, visibility filtering, update scheduling, and buffer reuse are separate tactics ([mechanism and author sources](minecraft-mod-mechanisms-research.md#sodium)). SiliconSandbox already requires bounded vertical render regions, certain fully hidden opaque faces to be omitted, local edit invalidation, and simulation independent of graphics. Candidate implementation details include reusing geometry buffers, drawing only relevant regions, and building a changed region from a snapshot of its authored content rather than scanning the whole world.

**Where it helps:** Dense sandstone or block scenes may reduce mesh size; repeated edits may reduce allocations and frame spikes; flying may benefit from view-dependent region submission. Sodium's fog and texture-animation controls matter only if SiliconSandbox has equivalent expensive effects. Its worker-thread count is not a universal slider to turn up.

**Where it can break the game:** Incorrect face omission produces holes after place/break/undo or at region seams. Deferring an edited region too long creates visually stale placement even when the data changed correctly. A mesh may combine drawing but cannot combine hitboxes or electrical channels. Signal-color updates must not trigger rebuilding every static region, and offscreen regions must still have current logical state when shown.

**Decision gate:** **Core principles already required; detailed renderer tactics are early candidates.** Compare region meshes, Unity's renderer options, and buffer reuse on the exact [rendering check scenes](docs/performance-and-platforms.md#rendering-optimization-checks). Unity's own [draw-call guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html) warns that appropriate batching depends on render pipeline; do not assume a custom Minecraft-like renderer automatically beats built-in facilities. Dependencies: [world](docs/project-vision-and-world.md), [graphics](docs/graphics-and-future-tools.md), [connections](docs/physical-connections.md).

### Lithium

**What transfers:** Lithium targets specific game-logic hot paths using efficient scheduled-work structures, change tracking instead of repeated polling, compact collections, allocation control, and cheap early rejection ([mechanism and author sources](minecraft-mod-mechanisms-research.md#lithium)). For SiliconSandbox, the analog is an event-driven simulator that queues only affected nets/gates, indexes net-to-receiver and driver relationships, tracks changed inputs, and avoids per-event allocations where profiling supports it. This is consistent with the already selected C# event-driven backend; Lithium's Minecraft AI/collision/tick patches should not be copied literally.

**Where it helps:** Large circuits with sparse changes can avoid scanning every gate on every frame or clock edge. Efficient scheduling and locality could improve achieved simulation throughput and avoid garbage-collection pauses, without changing the rendered frame rate directly. For a circuit in which nearly everything switches, the gain from skipping unaffected work is smaller; scheduling overhead may itself dominate.

**Where it can break the game:** Deduplication or coalescing of queued work must not erase an electrically relevant intermediate transition, a derived-clock edge, a four-state X/Z change, or a nonsettling-loop detection. Event order and delta cycles must match the specified semantics, independent of visual frame rate. Connectivity caches need exact invalidation for breaking a wire and splitting a net, changing a bit mapping, rotating a module, or loading a world. Optimization must preserve intermediate behavior relevant to player inspection, not merely final outputs.

**Decision gate:** **High-value early candidate, but benchmark the actual scheduler.** Establish truth-table and timed-event reference cases, then compare pre/post event traces, resolved values, clock edges, queue length, allocations, simulator CPU time, and frame stutters on sparse- and high-activity circuits. Keep optimization switches separable where practical so regressions can be isolated. Dependencies: [digital simulation](docs/digital-simulation.md), [circuit time](docs/circuit-time-and-clock.md), [modules](docs/modules-and-packaging.md), [connections](docs/physical-connections.md).

### ModernFix

**What transfers:** Its patch-by-patch discipline and optional lazy model creation are useful analogies; the author explicitly notes compatibility risks and first-use cost ([mechanism and author sources](minecraft-mod-mechanisms-research.md#modernfix)). SiliconSandbox could defer loading or constructing **visual** assets for rarely used block/module types, and profile startup separately from the first time an asset is shown. A visible, needed asset could be prepared before display where feasible.

**Where it helps:** If the eventual component catalog and embedded worlds load many unused models or materials, selective visual lazy loading could reduce startup time and retained RAM. Object deduplication may also overlap with FerriteCore's immutable-data sharing. This is less likely to matter in the first playable's small catalog than in a full game or a large module library.

**Where it can break the game:** Lazy simulation-definition loading cannot make an offscreen circuit stop, postpone a clock edge, or cause a missing-module placeholder when a definition exists. Delayed visual construction may introduce a first-use hitch or absent texture, especially during flying. Saves still embed exact referenced definitions and verify them. Avoid a single blanket lazy-loading policy for models, authored data, simulation state, and recovery metadata.

**Decision gate:** **Profile first.** Instrument startup, world load, first placement/use of each type, RAM, and missing/late asset cases. Use this only if visual assets are significant and a fallback/first-use policy is defined. Dependencies: [saving](docs/saving-and-recovery.md), [modules](docs/modules-and-packaging.md), [world](docs/project-vision-and-world.md).

### FerriteCore

**What transfers:** Its main technique is to deduplicate equivalent immutable data and replace repeated bulky lookup maps with indexed structures; its author warns about sharing mutable arrays and race conditions ([mechanism and author sources](minecraft-mod-mechanisms-research.md#ferritecore)). SiliconSandbox has a strong use case: many placed copies can reference the same **immutable, exact module-version definition**, including layout and component configuration, while each copy owns its own mutable simulation values, scheduled events, registers, and NVM. Repeated material/mesh templates, fixed component pin layouts, and read-only lookup tables can also be shared. Compact IDs or palette-like references are candidates for large sparse region data, provided the saved format remains versionable and recoverable.

**Where it helps:** Repeated/nested modules and large numbers of similar blocks can otherwise multiply retained layout, geometry, and lookup data. Sharing may reduce RAM, load work, and garbage collection. The actual savings depend on the project's data representation; there is no transferable MB figure from FerriteCore.

**Where it can break the game:** A mutable array accidentally shared between instances could cause one circuit's clock state, signal, or NVM write to alter another. Editing a source circuit must not change an already packaged version, and world duplication must remain independent. Nested module/version identity and integrity checks must still be exact. Share only data with enforced immutability or copy-on-write semantics whose behavior has been verified; do not deduplicate by appearance when hidden port maps or initialization differ.

**Decision gate:** **High-value early candidate** because the accepted module model already distinguishes definition from instance. Measure retained bytes per definition and per instance, memory after many repeated copies, allocation rate, load time, and two-instance independence tests. No particular interning or compression format is yet required. Dependencies: [modules](docs/modules-and-packaging.md), [saving](docs/saving-and-recovery.md), [digital simulation](docs/digital-simulation.md).

### Entity Culling

**What transfers:** The mod tests occlusion *within* the camera view, skips hidden renderables, and keeps server simulation separate from visibility ([mechanism and author sources](minecraft-mod-mechanisms-research.md#entity-culling)). SiliconSandbox has already accepted the important separation: graphics may be culled/unloaded but every circuit keeps running. Further occlusion checks could hide fully blocked components or module shells behind walls, beyond ordinary frustum/region selection.

**Where it helps:** In a dense world where many regions are inside the camera frustum but blocked by opaque objects, skipping their draw and associated presentation work may help. CPU visibility tests themselves cost time, so a sparse open world may see little or no gain.

**Where it can break the game:** Signal glows, selection outlines, labels, and visible protrusions may extend outside an object's nominal bounds. If the object is hidden but its meaningful cue is visible, users can miss an X or target. Visibility results can lag camera/edit changes; newly exposed objects must reappear correctly. The mod's optional client-tick culling is **not** transferable to simulation updates. Unity 6.3 says its [built-in baked occlusion culling](https://docs.unity3d.com/6000.3/Documentation/Manual/OcclusionCulling.html) is unsuitable when scene geometry is generated at runtime, so a built-in bake should not be assumed to handle player-authored worlds.

**Decision gate:** **Basic view culling already required; deeper occlusion is profile first.** Compare GPU/main-thread cost of a dense occluded scene against visibility-test overhead and check outlines, X/Z cues, removal exposure, and offscreen event traces. Dependencies: [world](docs/project-vision-and-world.md), [graphics](docs/graphics-and-future-tools.md), [physical connections](docs/physical-connections.md).

### MoreCulling

**What transfers:** MoreCulling shows that a geometric overlap test alone may be insufficient: partial shapes and transparent materials need separate knowledge before a face can be discarded ([mechanism and author sources](minecraft-mod-mechanisms-research.md#moreculling)). SiliconSandbox's already accepted simple case is a fully hidden face of generated opaque block geometry. A later shape-aware rule could use per-type occlusion masks and material opacity to discard extra faces, while keeping unknown or semitransparent cases conservative.

**Where it helps:** Dense builds with adjacent partial blocks could use fewer faces and less GPU work. This may matter less for worlds dominated by thin wires and exposed components, where many faces are genuinely visible.

**Where it can break the game:** The 0.25-block-diameter wire, harness, pin, Net Link cap, crossing gap, junction marker, and selected outline are deliberately thin or partially exposed. A block's visual texture may contain transparent holes even if its collision shape is solid. A wrongly culled face can erase the only evidence of a connection or create a boundary gap after an edit.

**Decision gate:** **Simple opaque case is required; partial-shape extension is profile first.** Prove a visible face is fully covered by an opaque neighbor using actual rendered geometry/material rules, compare dense-scene mesh and GPU cost, and test changes on all six faces and across render-region boundaries. When uncertain, draw the face. Dependencies: [graphics](docs/graphics-and-future-tools.md), [physical connections](docs/physical-connections.md), [performance](docs/performance-and-platforms.md).

### Iris

**What transfers:** Iris is a shader-pack compatibility and render-pipeline layer that interacts with an optimized base renderer; enabling arbitrary shader packs is not itself a speed feature ([mechanism and author sources](minecraft-mod-mechanisms-research.md#iris)). The useful design practice is to treat visual enhancements as optional and verify them with the base renderer, distant representations, and hardware coverage. SiliconSandbox's logic-state colors and shape readability should remain clear under any future lighting or post-processing preset.

**Where it helps:** There is no established performance benefit for SiliconSandbox. Optional simplified shaders could improve performance, but that is a new measured graphics choice, not an Iris mechanism. Elaborate shaders often add GPU work.

**Where it can break the game:** A shader can make 0/1/X/Z colors ambiguous, obscure an X pulse, or cause selected wire outlines to disappear. Pipeline-specific features may differ on Mac and Windows. Iris's OpenGL shader-pack code and compatibility layers are not directly reusable in Unity.

**Decision gate:** **No optimization to adopt now.** Choose the Unity rendering pipeline for the game based on project needs, then test any later visual preset against signal readability, graphics cost, and supported hardware. Dependencies: [digital simulation visuals](docs/digital-simulation.md#visuals), [graphics](docs/graphics-and-future-tools.md), [platforms](docs/performance-and-platforms.md).

### ImmediatelyFast

**What transfers:** The mod batches repetitive immediate-mode graphics work, reduces upload/state changes, and caches repeated text/map work ([mechanism and author sources](minecraft-mod-mechanisms-research.md#immediatelyfast)). SiliconSandbox analogs include repeated component/connector visual templates, many dynamic signal indicators, selected-net highlighting, labels, small inspection readouts, and the performance HUD. Candidates are shared materials or instance data, batched compatible draws, cached unchanged text, and updates only when the displayed value actually changes.

**Where it helps:** A world can be limited by CPU render submission or UI regeneration even when GPU triangles and simulator work are modest. Frequent color changes can cause redundant uploads or allocations. The accepted rule already prevents a signal-color change from rebuilding unrelated static world geometry.

**Where it can break the game:** Caching can leave a visible signal, hover readout, selection outline, or performance warning stale. Each connector may need a distinct picking identity even if it shares draw geometry. Unity 6.3 offers multiple [draw-call optimization paths](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html); the best method varies by pipeline and may interact with material/instance data. A special Apple workaround in the mod does not imply the same issue exists in Unity/Metal.

**Decision gate:** **Profile first; early if main-thread drawing/UI work is high.** Record draw calls/SetPass, UI rebuild time, allocations, color-update time, and picked-object correctness before choosing a batching method. Keep signal and selection updates event-triggered even when their drawing is batched. Dependencies: [graphics](docs/graphics-and-future-tools.md), [inspection](docs/inspection-and-diagnostics.md), [connections](docs/physical-connections.md).

### Sodium Extra

**What transfers:** The mod exposes switches for optional visual work such as particles, sky/weather detail, texture animation, debug-HUD refresh, and resolution scaling ([mechanism and author sources](minecraft-mod-mechanisms-research.md#sodium-extra)). SiliconSandbox could eventually provide graphics presets or optional-effect controls to let slower PCs trade decorative detail for performance. A debug readout could refresh less often if its numerical usefulness remains intact.

**Where it helps:** If future decorative effects, labels, or high-resolution rendering consume measurable GPU/UI time, disabling or lowering *optional* work can improve frame rate. Current design has no Minecraft weather or procedural biome workload, so several switches have no analog.

**Where it can break the game:** Minecraft's optional light-update suppression is a poor analog for SiliconSandbox's required live 0/1/X/Z presentation. A setting must never skip simulation work, hide an X/Z warning, leave selected-net or pin feedback stale, or make thin wires unselectable. Lower resolution can reduce the legibility of quarter-block-diameter connectors, so it needs an interaction/visual test. The performance HUD must still distinguish requested versus achieved simulation speed and rendered FPS.

**Decision gate:** **Later or profile first.** Add options for effects that actually exist and cost time, with clear defaults and visual correctness tests; do not add a menu full of irrelevant Minecraft-style toggles. Dependencies: [inspection](docs/inspection-and-diagnostics.md), [graphics](docs/graphics-and-future-tools.md), [performance](docs/performance-and-platforms.md).

### Reese's Sodium Options

**What transfers:** Searchable, grouped settings with explanation and per-option undo/reset can make a large configuration surface easier to understand ([mechanism and author sources](minecraft-mod-mechanisms-research.md#reeses-sodium-options)). This might be useful when SiliconSandbox has real graphics and simulator controls, especially settings that affect quality, memory, or throughput.

**Where it helps:** It does not directly make the game run faster. It may help a player find and safely change an existing performance setting. It could reduce support errors caused by poorly explained settings, but that is a usability hypothesis, not a measured speed claim.

**Where it can break the game:** Presenting untested or behavior-altering switches as harmless FPS settings could undermine deterministic simulation. The specified clock speed, pause behavior, and diagnostic readouts have gameplay meaning; graphics controls must be distinguished from simulation controls. Undo/reset of settings must restore the intended value without silently changing saved-world semantics.

**Decision gate:** **Later scope.** Design the settings UI once the actual settings and player/developer scope are known. No first-playable engine optimization follows from this mod. Dependencies: [building/interface](docs/building-and-interface.md), [world extensibility](docs/project-vision-and-world.md#definitions-and-extensibility), [performance](docs/performance-and-platforms.md).

### C2ME

**What transfers:** C2ME organizes chunk generation and I/O as concurrent work and explicitly encounters sequencing and thread-safety risks ([mechanism and author sources](minecraft-mod-mechanisms-research.md#c2me)). SiliconSandbox's likely analog is background construction of *derived* render-region meshes and possibly background parsing/compression of world files, from immutable snapshots. A region build can carry a version; if an edit happens before the result is applied, discard or redo stale output. Unity's [MeshData API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mesh.MeshData.html) supports mesh-data processing/creation in the C# Job System, though the game must still obey Unity API thread restrictions.

**Where it helps:** Large paste/load or many region edits could otherwise stall the main thread. Parallel work may improve throughput on multicore PCs when the per-job work is large enough to outweigh scheduling and data-copy costs. The simple generated floor has little analog to Minecraft's expensive noise/chunk generation.

**Where it can break the game:** An asynchronous result can overwrite a newer edit, produce a gap at a region boundary, show a stale signal color, or make an object visible before its collider/selection mapping is ready. Save snapshots and derived graph rebuilds must reflect a consistent authored version. **Do not parallelize the simulator's ordered event execution by simply sending gates to worker threads**; delta-cycle and derived-clock ordering require a separate correctness design. Unbounded worker jobs can compete with the simulator and make frame times worse.

**Decision gate:** **Profile first.** Test edit and large-load spikes, compare synchronous/limited-background region construction, check stale-result cancellation/versioning and selection coherence, and verify unchanged event traces. Exact job count, region size, and scheduling policy remain open. Dependencies: [world](docs/project-vision-and-world.md), [graphics](docs/graphics-and-future-tools.md), [circuit time](docs/circuit-time-and-clock.md), [saving](docs/saving-and-recovery.md).

### Noisium

**What transfers:** Noisium's direct writes skip higher-level setter work only during *initial bulk generation*, when the normal per-edit side effects are redundant; its author does not present that as safe for live edits ([mechanism and author sources](minecraft-mod-mechanisms-research.md#noisium)). SiliconSandbox's floor is generated from simple rules, so optimizing procedural noise is irrelevant. A more relevant analog is constructing a new authored region for paste, module expansion, or load as a complete batch, then performing graph/topology, render-region, hitbox, undo, and save-index updates at one defined commit point.

**Where it helps:** Repeatedly invoking the ordinary live-placement path for thousands of objects may do redundant validation and rebuild work. A transactional bulk path could reduce large-paste and world-load latency while preserving the same final authored and simulated state. This is a candidate, not an accepted bulk-edit algorithm.

**Where it can break the game:** Bypassing normal setters can omit connector junction updates, split/merge logic, module IDs, pin occupancy checks, NVM initialization, undo records, or render-boundary invalidation. Paste has all-or-nothing footprint checks; a failed operation must leave no partial authored content. A bulk edit still needs the required safe simulation pause and a complete, correct post-commit derived graph. The direct-write shortcut should **not** be used for ordinary live single-block edits without equivalent side effects.

**Decision gate:** **Later bulk-edit candidate.** The professor milestone requires package/save but can defer large paste and expansion. When bulk editing matters, compare batch and ordinary paths for identical authored topology, selection, save/reload, undo, simulation results, and latency. Dependencies: [building/interface](docs/building-and-interface.md), [modules](docs/modules-and-packaging.md), [saving](docs/saving-and-recovery.md), [circuit time](docs/circuit-time-and-clock.md).

### Krypton

**What transfers:** Krypton optimizes packet handling, serialization, and network flushes for multiplayer/server communication ([mechanism and author sources](minecraft-mod-mechanisms-research.md#krypton)). If SiliconSandbox later adds multiplayer, batching messages and profiling network allocations might matter. That future system would need its own consistency model for authored edits and simulation state; Krypton's Minecraft wire protocol is not a reusable game protocol.

**Where it helps:** The specified first playable and first public release are single-player with local worlds. There is no multiplayer packet stream to optimize now, and local save-file I/O is a different workload.

**Where it can break the game:** Adding speculative network architecture now could complicate simulator ownership, save consistency, and deterministic state without any current benefit. Treat future network transport separately from the core event simulator. Batching must not reorder or lose causally important edits or state transitions if multiplayer is eventually designed.

**Decision gate:** **Later scope only.** Reopen after multiplayer is actually specified and network profiling identifies a bottleneck. Dependencies: [scope](docs/project-vision-and-world.md#modes-and-platform-scope), [future tools](docs/graphics-and-future-tools.md#future-automatic-routing-multiplayer-and-vr).

## Cross-cutting implementation guidance

1. **Optimize the measured path, not the mod name.** Sodium, Entity Culling, MoreCulling, Voxy, Nvidium, and ImmediatelyFast all affect different portions of rendering. Their gains cannot be added together by assumption; some duplicate work or require incompatible draw paths. Lithium addresses simulation CPU work, FerriteCore memory, ModernFix startup/asset work, and Krypton only networking.
2. **Preserve the authored, derived, and presented layers.** The sparse authored design and exact module versions remain the source for saves and rebuilds. The simulator derives a graph with independent instance state. Graphics and optional LOD/batching are disposable derived views. A rendering optimization cannot change net topology, pause simulation, or become the only copy of a design. See [modules](docs/modules-and-packaging.md) and [world](docs/project-vision-and-world.md).
3. **Do not confuse visual frame rate with simulation throughput.** A renderer optimization may raise FPS while the event queue still falls behind; a scheduler optimization may increase achieved simulated time while FPS stays unchanged. Both metrics matter, and high simulated clock frequency is a representation/rate-control question, not a guarantee of real-time GHz processing. See [performance](docs/performance-and-platforms.md) and [circuit time](docs/circuit-time-and-clock.md).
4. **Treat local invalidation as a correctness problem.** Every edit can affect a region and neighbor boundary, a net's membership, selected geometry, saved authored data, and possibly an asynchronous graphics job. Cache keys and versions must account for these; a faster stale cache is a bug. This is an implementation guidance inference, not an additional game rule.
5. **Use Unity facilities with measured fit.** Unity 6.3 offers several [draw-call paths](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html), [GPU instancing](https://docs.unity3d.com/6000.3/Documentation/Manual/GPUInstancing.html), and [job-compatible mesh data](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mesh.MeshData.html). Their suitability depends on the chosen render pipeline, repeated mesh/material patterns, dynamic state, and supported hardware. A Minecraft renderer's internal API choices should not be transplanted mechanically.
6. **Keep fallbacks and validation visible.** A candidate should have a reference behavior or safe fallback and an explicit acceptance test: same circuit results, same save/reload result, same visible topology/targeting, acceptable edit response, and better measured cost on Windows and Mac. Hardware-dependent paths need a common supported path.

## Recommended order of investigation

This is a **research priority order**, not a new game roadmap or permission to alter the spec:

| Stage | Candidate questions | Why here |
| --- | --- | --- |
| Before/while building the first playable | Can the event queue and adjacency/indexing avoid whole-circuit scans while preserving exact four-state event traces? Can repeated module versions share immutable definitions while keeping per-instance state separate? Can the accepted render-region and hidden-face rules be implemented without losing targeting? | These align with already accepted foundations and protect correctness as the game grows. Lithium, FerriteCore, Sodium, and the safe part of MoreCulling are the best analogs. |
| At the first playable benchmark | Is bottleneck simulator CPU, render main thread, GPU, RAM, edit rebuild, or load? Are color updates or HUD/UI allocations significant? | Selects among Sodium, ImmediatelyFast, C2ME, ModernFix, Entity Culling, and optional presets with evidence. |
| When larger authored worlds exist | Do distant views require LOD? Do dense worlds justify deeper occlusion/partial-face tests? Do paste/load spikes justify background/bulk construction? | Voxy, Entity Culling, MoreCulling, C2ME, and Noisium only earn complexity if their workload appears. |
| Only if later features are chosen | Do optional shader effects, extensive graphics settings, or multiplayer exist and have measured costs? | Iris, Sodium Extra, Reese's Sodium Options, Krypton, and hardware-specific Nvidium-style paths are not current foundations. |

## Source and decision boundaries

- [Mod mechanism research and its direct author/source links](minecraft-mod-mechanisms-research.md) is the factual basis for what each mod does. It distinguishes representative implementation evidence from promises about all releases.
- [Unity 6.3 draw-call guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html), [GPU instancing](https://docs.unity3d.com/6000.3/Documentation/Manual/GPUInstancing.html), [mesh data API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mesh.MeshData.html), and [baked occlusion limits](https://docs.unity3d.com/6000.3/Documentation/Manual/OcclusionCulling.html) support the Unity-specific transfer boundaries cited above.
- [Earlier broad optimization research](minecraft-optimization-research.md) records preliminary screening and the rationale behind some already accepted invariants. This detailed transfer record refines that screening; it does not itself supersede canonical text.
- **No new optimization method, graphics API, region size, data encoding, background job policy, or large-circuit performance number was made canonical by writing this file.** Any later accepted choice should be recorded in its operative subsystem file and routed from [INDEX.md](INDEX.md).
