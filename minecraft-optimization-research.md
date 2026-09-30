# Minecraft optimization research for SiliconSandbox

**Status:** Research record. The four proposals below were incorporated as canonical behavior and measurement rules on 29 September 2026; the exact mesh, material, LOD, and compression techniques remain open. Reviewed against [the original research backlog](docs/original-research-backlog.md), [the accepted world foundation](docs/project-vision-and-world.md#world-data-and-rendering-architecture), [graphics](docs/graphics-and-future-tools.md), and [performance](docs/performance-and-platforms.md).

For a detailed account of how each named mod operates inside Minecraft, read [Minecraft mod mechanisms research](minecraft-mod-mechanisms-research.md). For a detailed assessment of possible use in SiliconSandbox, read [Minecraft mod transfer analysis](minecraft-mod-transfer-analysis.md). Those later research records make no additional SiliconSandbox design decisions.

## Navigation

- [What transfers to this game](#what-transfers-to-this-game)
- [Technique inventory](#technique-inventory)
- [Every named mod](#every-named-mod)
- [Accepted additions and open techniques](#accepted-additions-and-open-techniques)
- [Measurement and correctness checks](#measurement-and-correctness-checks)

## What transfers to this game

Minecraft mods cannot be installed into Unity or copied as a ready-made renderer. Their useful contribution here is evidence about *categories of work*: hidden geometry, mesh updates, repeated assets, memory duplication, and operations done on the main thread. SiliconSandbox also differs materially from Minecraft: its floor is generated and unbreakable, the rest of its world is player-authored and sparse, it must support precise thin-wire targeting, and circuit simulation cannot depend on graphics visibility. The already accepted architecture addresses the largest structural issue: sparse authored data, bounded render regions, and simulation independent of rendering.

**Research conclusion, not a benchmark result:** For the first playable, simple view culling and bounded local geometry updates are better candidates than a bespoke distant-terrain or GPU-specific renderer. This is a priority judgment from the game's requirements and the sources below; actual wins must be measured on the target Windows and macOS hardware.

## Technique inventory

| Backlog topic | Finding for SiliconSandbox | Status |
| --- | --- | --- |
| 3D block geometry and hidden-face culling | Opaque neighboring blocks can omit truly hidden faces in generated render geometry. Wires, pins, transparent parts, exposed faces, selection outlines, and collision/targeting need their own rules; visual culling must never remove authored data. Breaking a block must expose the newly visible faces, already required by [graphics](docs/graphics-and-future-tools.md). | Worth proposing as an explicit renderer correctness rule. |
| Chunking, combined meshes, VBO-style batching | Existing bounded render regions are the transferable idea. A region may use one or multiple meshes/draw batches; neither Minecraft's 16×16×full-height dimensions nor one Unity GameObject per chunk follows from the evidence. Unity provides several draw-call reduction methods, including GPU instancing; method choice depends on mesh/material sharing and measurements. [Unity draw-call guide](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html), [GPU instancing](https://docs.unity3d.com/6000.3/Documentation/Manual/GPUInstancing.html). | Foundation already accepted; concrete rendering method open. |
| Local face updates after edits | Invalidate the edited render region and any neighbor region whose boundary faces changed. Rebuild only affected graphics, not the entire world or simulation. A full rebuild of one small affected region may outperform fine-grained per-face mutation; measure both before prescribing an algorithm. | Worth proposing as an edit-scope requirement. |
| Greedy meshing and UV recalculation | Merge adjacent opaque coplanar faces only when material, appearance, orientation, lighting, and interaction requirements permit. Preserve repeated texture appearance by suitable UVs. Avoid merging thin electrical topology into surfaces that hide junctions, crossings, or precise hit targets. Greedy meshing may reduce geometry but raises rebuild/selection complexity, so it is conditional. | Measure if geometry or draw submission is a bottleneck. |
| Texture atlases, material grouping, Blender meshes | A texture atlas/material grouping can reduce state changes for compatible block faces. A Blender-authored mesh and an atlas solve different problems: shape versus texture/material organization; either can be used together. Unity's batching and instancing effectiveness depends on shared materials. Preserve the specified configurable pixel density and component readability. [Unity draw-call guide](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html). | Asset-pipeline choice, not yet a design decision. |
| Mipmaps | Mipmaps may reduce aliasing for distant textured surfaces but consume additional texture memory; filtering must be checked against the intended pixel-art appearance. The backlog's proposed automatic FPS gain should not be assumed. | Visual/performance comparison later. |
| Dynamic materials and signal colors | Electrical state changes frequently. Avoid rebuilding whole region meshes solely to recolor a signal if a cheaper material/instance-data method works and preserves four-state colors. Unity documents tradeoffs between draw-call methods and material property approaches. [Unity draw-call guide](https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html). | Worth proposing as a measured design objective, without fixing the implementation. |
| Multithreaded mesh generation | Preparing mesh data off the main thread can reduce edit/travel stutter when region rebuilds are costly; actual Unity mesh uploads and engine-object access need correct threading boundaries. Unity's writable `MeshData` supports Job System workflows. [Unity MeshData API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mesh.MeshData.html). | Conditional optimization after profiling. |
| Frustum/occlusion culling | Cull graphics outside the camera view while preserving simulation. Unity's built-in baked occlusion is unsuitable for geometry generated at runtime, so do not assume it solves player-built occlusion. [Unity occlusion manual](https://docs.unity3d.com/6000.3/Documentation/Manual/OcclusionCulling.html). | View culling already accepted; dynamic occlusion conditional. |
| LOD, Voxy, Distant Horizons, sparse voxel octrees | Coarser distant *visuals* may help if a later world presents enough distant geometry. Exact authored layout, readable signal state, editing, and targeting must return at interaction distance. Generated approximate terrain is a poor model for player-authored circuits; a sparse voxel octree is an option to benchmark, not a foundational need. [Voxy](https://github.com/MCRcortex/voxy). | Defer until a measured large-world rendering problem. |
| Smooth voxels, marching cubes/tetrahedra, surface nets, tessellation | These are surface-shape approaches, mainly useful for smooth terrain. They may conflict with explicit grid/block faces and connector readability. They do not by themselves guarantee better FPS and are not required to achieve the stated visual style. | Defer as art research, not a first-playable optimization. |
| Noise-sampled fake distant terrain | The backlog's Reddit idea concerns procedural terrain at multiple resolutions. SiliconSandbox's flat floor needs no complex noise sampling, and placed circuits have authoritative authored geometry that cannot be guessed from a seed. | Not applicable to the accepted world model. |
| RAM compression, streaming, disk faulting | Sparse storage already avoids empty-cell allocation. Share immutable definitions and repeated asset data where possible. Streaming graphics or cached render data is compatible with offscreen simulation; unloading authored circuit data or live electrical state merely because it is distant is not. Compression/streaming should follow measured RAM or load-time pressure. [FerriteCore technique summary](https://github.com/malte0811/FerriteCore/blob/26.1/summary.md). | Data-sharing principle relevant now; compression deferred. |

The Reddit posts named in the backlog remain hypotheses/visual references, not primary evidence for performance or an instruction to adopt their algorithms.

## Every named mod

| Mod | What its author describes | Transferable lesson / decision for this game |
| --- | --- | --- |
| [Voxy](https://github.com/MCRcortex/voxy) | Distant-terrain level of detail rendering. | Consider distant visual LOD only after measuring large-world geometry costs; keep circuit data and simulation exact. |
| [Nvidium](https://github.com/MCRcortex/nvidium) | Sodium rendering backend using recent NVIDIA hardware, requiring a GTX 1600 series or newer. | A hardware-specific renderer cannot be the sole path for a Windows/macOS game. Optional specialized paths would need a portable fallback and evidence of need. |
| [Sodium](https://github.com/CaffeineMC/sodium) | Replaces Minecraft's client renderer to improve FPS and microstutter. Its [configuration text](https://github.com/CaffeineMC/sodium/blob/dev/common/src/main/resources/assets/sodium/lang/en_us.json) documents early block-face elimination and chunk build threads. | Prioritize avoiding invisible faces and large render-update stalls; do not infer that Unity needs a renderer replacement. |
| [Lithium](https://github.com/CaffeineMC/lithium) | Optimizes general game logic while preserving gameplay behavior. Its [configuration documentation](https://github.com/CaffeineMC/lithium/wiki/Configuration-File) includes an optimized tick scheduler and fewer allocations. | Profile the C# event scheduler and allocations; optimize without changing four-state/event semantics. The accepted event-driven simulator already avoids a world-wide visual tick. |
| [ModernFix](https://github.com/embeddedt/ModernFix) | Targets launch time, world load time, memory, and bugs. | Measure loading and memory as well as FPS; concrete Minecraft fixes do not transfer automatically. |
| [FerriteCore](https://github.com/malte0811/FerriteCore/blob/26.1/summary.md) | Reduces memory by avoiding duplicated state and compacting data structures. | Share immutable module definitions and reusable geometry/material data; keep each module instance's mutable simulation state independent. |
| [Entity Culling](https://github.com/tr7zw/entityculling) | Hides invisible entities/block entities without affecting server-side logic. | Strong analogue for the already accepted separation between graphics and offscreen circuit behavior. |
| [MoreCulling](https://github.com/fxmorin/MoreCulling) | Expands the kinds of hidden geometry that Minecraft can omit. | Culling can help, but apply only where visibility is certain; preserve face exposure and the visual truth of wires/connectors. |
| [Iris](https://github.com/IrisShaders/Iris) | Shader-pack compatibility and graphical customization with performance aims. | No direct core-performance design change; advanced shaders are lower priority than clear circuit visuals. |
| [ImmediatelyFast](https://github.com/RaphiMC/ImmediatelyFast) | Batches immediate-mode rendering and improves GPU uploads, including GUI/HUD and text paths. | If HUD, labels, outlines, or many repeated visual objects become CPU-render bottlenecks, batch/cache their rendering where appropriate. |
| [Sodium Extra](https://github.com/FlashyReese/sodium-extra) | Extra Sodium features and settings. | Settings/mod-control reference, not a foundational optimization algorithm for Unity. |
| [Reese's Sodium Options](https://github.com/FlashyReese/reeses-sodium-options) | Replaces Sodium's options screen. | UI organization reference, not a world or simulator optimization. |
| [C2ME](https://github.com/RelativityMC/C2ME-fabric) | Parallel chunk generation, I/O, and loading using multiple CPU cores. | Parallel mesh/data preparation may help later; Minecraft terrain generation itself is far less relevant to a flat generated floor. |
| [Noisium](https://github.com/Steveplays28/noisium) | Optimizes Minecraft noise-driven world generation and block-state population. | Noise-worldgen optimization is not needed for the specified flat world. |
| [Krypton](https://github.com/astei/krypton) | Optimizes Minecraft networking. | Revisit only if the later multiplayer feature becomes an active requirement; no first-playable benefit. |

These rows describe each project's stated purpose and an **inference** about relevance to SiliconSandbox; they are not claims that a mod's measured speedup will reproduce in Unity.

## Accepted additions and open techniques

The user authorized incorporation of these proposals. Their precise operative wording is in [rendering and update invariants](docs/graphics-and-future-tools.md#rendering-and-update-invariants), with [world architecture](docs/project-vision-and-world.md#world-data-and-rendering-architecture) and [performance checks](docs/performance-and-platforms.md#rendering-optimization-checks). The following preserves the research-to-decision mapping:

1. **Render correctness:** Generated opaque block geometry should omit fully hidden faces where visibility is certain. Edits must expose or hide affected neighbor faces correctly, including across render-region boundaries. Precise wire, pin, channel, and floor targeting and visible electrical topology take precedence over mesh merging.
2. **Bounded edit work:** A local edit should invalidate only affected region graphics and necessary neighbor boundaries. The implementation may rebuild a small affected region or update a smaller mesh part; choose by measured edit latency and correctness.
3. **State-change efficiency:** Frequent simulated signal-color changes should not rebuild unrelated static world geometry. Electrical state and render state remain separate.
4. **Measurement gate:** When choosing atlases, instancing, greedy meshing, mesh jobs, occlusion, LOD, or compression, compare candidates on the same first-playable and later large-circuit scenes. Record frame time/stutters, main-thread and GPU time, edit latency, memory, and simulator throughput; retain correctness tests for targeting, face exposure, and offscreen state. This extends the existing [performance benchmark](docs/performance-and-platforms.md#benchmark), rather than replacing it.

The accepted rules do not require atlases, instancing, greedy meshing, multithreaded mesh generation, dynamic occlusion, LOD, or compression. Those remain choices to compare with evidence. Earlier accepted foundations—sparse authored storage, generated floor, bounded vertically subdivided render regions, view-dependent graphics, offscreen simulation, readable circuit visuals, and the milestone benchmark—remain in effect.

## Measurement and correctness checks

- Scene shapes: scattered sparse components; dense opaque block clusters; many exposed wires/connectors; many identical modules; frequently changing signals; rapidly edited region boundaries; stationary/flying camera; circuit out of view.
- Compare identical authored circuit behavior with graphics visible and culled; no lost clock edges or state transitions.
- Place/break at a region edge and confirm correct neighboring faces, no gaps, correct targeting and undo.
- Inspect frame-time distribution, CPU render work, GPU time, memory, geometry/draw counts, region rebuild time, and simulator work separately. A higher average FPS alone does not establish lower stutter or preserved logic.
- Compare on both supported operating systems before making a hardware-specific optimization mandatory.
