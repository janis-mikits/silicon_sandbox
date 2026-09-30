# How the Minecraft mods in the backlog work

**Research record — 29 September 2026.** This file answers the historical [Minecraft performance mods backlog item](docs/original-research-backlog.md#minecraft-performance-mods). It describes mechanisms **inside Minecraft**. It does not select features for SiliconSandbox or change its [canonical design spec](INDEX.md). The earlier [optimization research](minecraft-optimization-research.md) contains a preliminary transferability screen; the later [detailed transfer analysis](minecraft-mod-transfer-analysis.md) evaluates safe use in SiliconSandbox.

## Reading this record

- The mechanisms below come from mod authors' project pages, wikis, and linked source files. When a project is a bundle of many patches, this record identifies representative mechanisms instead of pretending it has one algorithm.
- Features differ by Minecraft and mod version. A linked source branch or dated wiki page supports the described example, not a guarantee that every version has it. Voxy and Nvidium source links below point to their `dev` branches as inspected on the research date. FerriteCore's author notes that some reported memory figures use older modpack measurements; those figures are not SiliconSandbox benchmarks.
- "How it works" describes the optimization target and the actual intervention. "Limits" records dependencies, compatibility, or correctness boundaries stated by authors or visible in source. Any later Unity translation requires a separate analysis.

## Navigation

1. [Terrain rendering and distant detail](#terrain-rendering-and-distant-detail): Voxy, Nvidium, Sodium
2. [Game logic, loading, and memory](#game-logic-loading-and-memory): Lithium, ModernFix, FerriteCore
3. [Visibility and presentation](#visibility-and-presentation): Entity Culling, MoreCulling, Iris, ImmediatelyFast
4. [Settings and render controls](#settings-and-render-controls): Sodium Extra, Reese's Sodium Options
5. [Chunk work, world generation, and networking](#chunk-work-world-generation-and-networking): C2ME, Noisium, Krypton
6. [Dependencies and boundaries](#dependencies-and-boundaries)

## Terrain rendering and distant detail

### Voxy

**Target:** Draw terrain far beyond Minecraft's normal detailed render distance by using lower-detail representations. The author describes it as a client-side, OpenGL 4.6 LOD renderer that converts explored terrain to LOD data. Existing worlds can also be imported. Shader packs need explicit Voxy support. [Author's project page](https://modrinth.com/mod/voxy).

**Mechanism:** Voxy converts Minecraft chunk data into its own voxelized sections. Its [world updater](https://github.com/MCRcortex/voxy/blob/dev/src/main/java/me/cortex/voxy/common/world/WorldUpdater.java) inserts an update into level 0 and propagates changed data through parent mip/LOD levels, stopping when no relevant state changes. The inspected [world engine](https://github.com/MCRcortex/voxy/blob/dev/src/main/java/me/cortex/voxy/common/world/WorldEngine.java) defines LOD levels through `MAX_LOD_LAYER = 4`, identifies sections by level and coordinates, tracks active sections, and marks changed sections dirty for saving or render-data rebuild. The updater tracks whether a changed section touches neighboring boundaries, so neighboring render data can be refreshed when necessary. Its [serialization code](https://github.com/MCRcortex/voxy/blob/dev/src/main/java/me/cortex/voxy/common/world/SaveLoadSystem3.java) uses a per-section lookup table/palette: repeated block values are stored once in the table and positions store short indices. Its [render system](https://github.com/MCRcortex/voxy/blob/dev/src/main/java/me/cortex/voxy/client/core/VoxyRenderSystem.java) has hierarchical traversal, render-distance tracking, section geometry generation, upload, and storage subsystems. These are observations of the linked implementation, not a claim that every detail is invariant across releases.

**Limits:** This is a **visual** distant-terrain representation; its generated LOD data is not the authoritative Minecraft world simulation. Its own page requires OpenGL 4.6 and explicit shader-pack integration, and says LODs are created as terrain is explored or imported. [Project page](https://modrinth.com/mod/voxy).

### Nvidium

**Target:** Replace Sodium's terrain-rendering backend on supported NVIDIA GPUs. The author says it uses NVIDIA-specific OpenGL extensions and mesh shaders to make terrain rendering largely GPU-driven, allowing fast geometry culling before unnecessary geometry is emitted. [Author's project page](https://legacy.modrinth.com/mod/nvidium), [repository](https://github.com/MCRcortex/nvidium).

**Mechanism:** Nvidium depends on Sodium's chunk/render pipeline, then intercepts its render-section manager. The inspected [integration code](https://github.com/MCRcortex/nvidium/blob/dev/src/main/java/me/cortex/nvidium/mixin/sodium/MixinRenderSectionManager.java) chooses a compact chunk vertex format when Nvidium is active, creates a `NvidiumWorldRenderer`, substitutes its solid/translucent render calls for Sodium's terrain passes, and restores/destructs that backend when inactive. The project page identifies mesh shaders as the means for GPU-driven terrain culling. This describes an alternate **rendering backend**, not an alternate game-logic or world-generation engine. [Project page](https://legacy.modrinth.com/mod/nvidium), [integration source](https://github.com/MCRcortex/nvidium/blob/dev/src/main/java/me/cortex/nvidium/mixin/sodium/MixinRenderSectionManager.java).

**Limits:** It requires Sodium plus NVIDIA Turing-class hardware (GTX 1600 series or newer, according to the author). It disables itself on unsupported hardware; the author warns about possible crashes with the uncommon mesh-shader path. It also disables itself while Iris is actively using shaders. [Author's project page](https://legacy.modrinth.com/mod/nvidium), [Iris compatibility FAQ](https://irisshaders.dev/).

### Sodium

**Target:** Replace much of Minecraft Java's client terrain-rendering path to improve frame rate and reduce microstutter. This is a renderer optimization mod, not a circuit/gameplay logic mod. [Author repository](https://github.com/CaffeineMC/sodium).

**Mechanism:** Sodium compiles chunk sections into renderable geometry and manages visibility and draw submission. Its [block renderer source](https://github.com/CaffeineMC/sodium/blob/dev/common/src/main/java/net/caffeinemc/mods/sodium/client/render/chunk/compile/pipeline/BlockRenderer.java) tests whether a block quad should be culled before buffering it into a chunk mesh. The project's [option descriptions](https://github.com/CaffeineMC/sodium/blob/dev/common/src/main/resources/assets/sodium/lang/en_us.json) describe several distinct controls: block-face culling before rendering, fog-based chunk omission, entity omission using already-computed chunk visibility, animating only visible textures, adjustable chunk-building/sorting worker threads, and different policies for waiting on changed chunk graphics. The worker-thread option explicitly warns that more threads can worsen frame times; the deferred-update option explicitly warns that avoiding waits can make edited blocks appear late. The author's [release notes](https://github.com/CaffeineMC/sodium/releases) also describe allocator changes to reduce terrain-buffer allocation/resizing during chunk loads. These are separate interventions across mesh creation, visibility, texture animation, update scheduling, and buffer management.

**Limits:** Sodium's option descriptions warn that aggressive face culling may expose holes with certain resource packs and that maximum deferral can create visible edit lag. Rendering optimizations need visual-correctness checks; a higher worker count is not automatically faster. [Option descriptions](https://github.com/CaffeineMC/sodium/blob/dev/common/src/main/resources/assets/sodium/lang/en_us.json).

## Game logic, loading, and memory

### Lithium

**Target:** Reduce Minecraft's client/server game-logic CPU work while keeping vanilla behavior. Lithium is a collection of mostly independent patches, not a new renderer. [Author repository](https://github.com/CaffeineMC/lithium), [author wiki](https://github.com/CaffeineMC/lithium/wiki).

**Mechanism:** The [configuration documentation](https://github.com/CaffeineMC/lithium/wiki/Configuration-File) and [current patch inventory](https://github.com/CaffeineMC/lithium/blob/develop/lithium-fabric-mixin-config.md) show several families: an optimized tick scheduler for fewer operations around scheduled work; tracking state changes (for example equipment) to skip repeated polling; caching frequently queried nearby chunk/block information; using arrays or specialized collections in place of boxed generic maps; avoiding per-tick allocations; and optimizing collision/entity lookup so expensive work is delayed or skipped when cheaper checks already settle the result. Some AI patches switch repeated nearby-entity polling to event-based tracking. Each patch addresses a specific hot path, and Lithium exposes configuration flags so a problematic patch can be disabled individually. [Author configuration documentation](https://github.com/CaffeineMC/lithium/wiki/Configuration-File), [patch inventory](https://github.com/CaffeineMC/lithium/blob/develop/lithium-fabric-mixin-config.md).

**Limits:** Lithium aims at behavioral parity but its authors acknowledge that patches may interact badly with other mods; optimizing or suppressing work still requires preserving game side effects. Its documented patches are version-specific and mainly tied to Minecraft entity, block, AI, and tick internals. [Author wiki](https://github.com/CaffeineMC/lithium/wiki), [patch inventory](https://github.com/CaffeineMC/lithium/blob/develop/lithium-fabric-mixin-config.md).

### ModernFix

**Target:** Improve launch and world-load times, memory consumption, and stability through a collection of bug fixes and performance patches. This is broader than rendering; the author says not every patch exists on every Minecraft version. [Author project page](https://modrinth.com/mod/modernfix), [patch summary](https://github.com/embeddedt/ModernFix/wiki/Summary-of-Patches).

**Mechanism:** The author's [versioned patch summary](https://github.com/embeddedt/ModernFix/wiki/1.20-Summary-of-Patches) includes startup/debug/threading fixes, object deduplication, and optional deferred construction of entity models. A particularly substantial optional feature, **dynamic resources**, replaces eagerly loading/baking all block and item models at startup with loading them when requested. That moves work from initial loading to first use and reduces memory for models never needed during a session. The author disables it by default because some mods assume the eager model system and can show missing textures or fail. Other example patches include avoiding rendering GUI-item sides when unseen and avoiding loading persistent spawn chunks, but those are distinct options with their own behavioral implications. [Author project page](https://modrinth.com/mod/modernfix), [dynamic resources FAQ](https://github.com/embeddedt/ModernFix/wiki/Dynamic-Resources-FAQ), [1.20 patch summary](https://github.com/embeddedt/ModernFix/wiki/1.20-Summary-of-Patches).

**Limits:** ModernFix explicitly distinguishes safe default patches from compatibility-sensitive optional patches. Its model-loading intervention can trade startup memory/time for first-use work and compatibility risk; it is not evidence that lazy loading every resource is universally faster. [Author project page](https://modrinth.com/mod/modernfix), [dynamic resources FAQ](https://github.com/embeddedt/ModernFix/wiki/Dynamic-Resources-FAQ).

### FerriteCore

**Target:** Reduce Minecraft's RAM use, especially when many block states/models are loaded. The author's [technical summary](https://github.com/malte0811/FerriteCore/blob/26.1/summary.md) explains the mechanisms and notes that most quoted savings are from older, specific modpacks.

**Mechanism:** FerriteCore replaces per-state neighbor lookup tables with a compact per-block `FastMap`/indexed representation; replaces bulky per-state property maps; caches equivalent multipart-model predicates; deduplicates repeated model-resource strings, equivalent multipart-model instances, block-state collision/render shapes, and repeated baked-quad data. It also reduces allocations from objects retained in lambdas. The main pattern is **canonicalizing immutable repeated data and compacting lookup structures**, while retaining equivalent query results. The technical summary is unusually explicit about per-feature memory effects and tradeoffs. [Author technical summary](https://github.com/malte0811/FerriteCore/blob/26.1/summary.md).

**Limits:** The author flags deduplicating baked-quad `int[]` data as not completely safe if another participant mutates it, and notes a threading-detector memory optimization disabled by default after rare race conditions. Thus even a memory-saving shared-data technique depends on immutability and thread assumptions. Quoted MB savings are for the author's measured Minecraft modpacks, not a portable speed claim. [Author technical summary](https://github.com/malte0811/FerriteCore/blob/26.1/summary.md).

## Visibility and presentation

### Entity Culling

**Target:** Avoid the client-side cost of rendering entities and block entities that are inside the camera's general view but hidden behind walls, ceilings, or other opaque geometry. Ordinary view-frustum culling only removes objects outside the camera view; this mod checks line of sight within it. [Author repository](https://github.com/tr7zw/entityculling).

**Mechanism:** The author describes asynchronous CPU path tracing against scene geometry. Worker threads calculate visibility while the main game continues; the main thread periodically supplies required data. The render path skips objects classified as hidden, reducing draw calls and related work. The mod also offers configurable **client-side** tick culling for invisible entities, with per-type whitelists. It can be toggled at runtime for before/after comparison. The author says its line-of-sight checks are more aggressive than Sodium's chunk-visibility-based entity check. [Author repository/FAQ](https://github.com/tr7zw/entityculling).

**Limits:** The author distinguishes client-side rendering or client ticks from **server simulation**: farms, spawns, and entity behavior keep running. Objects whose visible effects extend beyond their normal bounds, such as beacon beams, may need whitelisting. The author also notes that spare CPU cores matter and that conservative culling may leave some hidden objects rendered to avoid incorrect disappearance. [Author repository/FAQ](https://github.com/tr7zw/entityculling).

### MoreCulling

**Target:** Omit additional block faces and presentation objects that Minecraft's normal rules leave rendered even though they are hidden. It covers several separate cases rather than one global camera-culling algorithm. [Author wiki index](https://github.com/fxmorin/MoreCulling/wiki).

**Mechanism:** For partial or non-full blocks, MoreCulling compares the facing voxel shapes of two neighboring blocks; when one fully covers the other's face, it can omit the covered face. Because a texture may contain transparent pixels even if its geometric shape looks occluding, the mod checks face texture transparency at startup or resource-pack change and caches whether aggressive culling is safe. [BlockState Culling explanation](https://github.com/FxMorin/MoreCulling/wiki/BlockState-Culling). For item frames, it can select three visible faces of a 3D item according to camera and rotation, omit an unseen back face, and omit a map when viewed from behind; the author documents limits for 45-degree rotations and distance-based simplification. [Item Frame Culling explanation](https://github.com/FxMorin/MoreCulling/wiki/Item-Frame-Culling). The wiki also enumerates specialized leaves and painting culling. [Wiki index](https://github.com/fxmorin/MoreCulling/wiki).

**Limits:** The author describes missing-face risks if a modded block supplies incorrect occlusion shapes, and provides per-mod disable/API exemptions. Texture transparency is a correctness condition, not only geometry overlap. [BlockState Culling explanation](https://github.com/FxMorin/MoreCulling/wiki/BlockState-Culling).

### Iris

**Target:** Load and run shader packs, especially packs built for OptiFine, while maintaining compatibility with an optimized Sodium renderer. Iris is primarily a shader-pack and graphics-pipeline compatibility layer, not a general logic or memory optimization mod. [Author repository](https://github.com/IrisShaders/Iris), [official site](https://irisshaders.dev/).

**Mechanism:** Iris interprets pack-specified shader stages and passes, including terrain/object rendering, shadows, deferred/composite passes, and optional compute stages. Its [shader-stage reference](https://github.com/IrisShaders/docs/blob/main/src/content/docs/current/Reference/Programs/overview.mdx) documents program order and supported GPU program stages. Its [debug documentation](https://github.com/IrisShaders/Iris/blob/26.1/docs/usage/debugging.md) says it transforms GLSL source for compatibility before compiling it with the graphics driver. Iris ships with/works alongside Sodium so the shader-pipeline expansion can use Sodium's optimized terrain path where compatible. This does **not** mean enabling a shader pack is itself a universal speedup. [Official site](https://irisshaders.dev/), [program reference](https://github.com/IrisShaders/docs/blob/main/src/content/docs/current/Reference/Programs/overview.mdx).

**Limits:** The official FAQ notes that Voxy and Distant Horizons require explicit support from shader packs, and Nvidium disables itself while shaders are active. Compute-shader stages require OpenGL 4.3, which the referenced Iris docs say macOS lacks. Packs may have device-specific limits. [Official FAQ](https://irisshaders.dev/), [program reference](https://github.com/IrisShaders/docs/blob/main/src/content/docs/current/Reference/Programs/overview.mdx).

### ImmediatelyFast

**Target:** Reduce CPU and GPU submission cost in Minecraft's immediate-mode render paths, including entities, block entities, particles, text, and UI/HUD. This is distinct from Sodium's main terrain rendering. [Author repository](https://github.com/RaphiMC/ImmediatelyFast).

**Mechanism:** The author describes a custom buffer implementation that batches draw calls and uploads graphics data more efficiently. Additional targeted patches cache some text lookup work, generate one atlas for many maps, avoid redundant framebuffer switches, and optimize HUD/text/map drawing. Its README describes an Apple-GPU-specific buffer-upload workaround and optional sign-text buffering. The central mechanism is to reduce repetitive submission/state changes for many small visuals. [Author README](https://github.com/RaphiMC/ImmediatelyFast).

**Limits:** The author says its strongest gains occur when the CPU render path is the bottleneck; published example FPS figures are scenario-specific and cannot be assumed for other scenes. Some optional shortcuts, such as disabling error checks or resource-pack conflict handling, can make problems harder to detect or introduce visual issues. [Author README](https://github.com/RaphiMC/ImmediatelyFast).

## Settings and render controls

### Sodium Extra

**Target:** Add graphics and presentation controls outside Sodium's core scope. It is an add-on to Sodium, not a replacement terrain renderer. [Author project page](https://modrinth.com/mod/sodium-extra), [feature inventory](https://github.com/FlashyReese/sodium-extra/wiki/Features).

**Mechanism:** The author lists switches for animations, individual particle categories (including particles added by other mods), sky/weather/detail layers, fog, some static entities, HUD refresh, and other presentation effects. These are mostly controls that prevent selected visual work from being scheduled or drawn. The feature inventory includes disabling light updates, described as relevant to large redstone contraptions, and reducing macOS Retina rendering resolution to half. It also describes a debug HUD that refreshes on a chosen cadence instead of every frame. [Author feature inventory](https://github.com/FlashyReese/sodium-extra/wiki/Features).

**Limits:** The light-update control deliberately stops processing a visual update; that may change what players see even if underlying gameplay continues. The author warns it should not be disabled casually. The exact options and their side effects vary with version. [Author feature inventory](https://github.com/FlashyReese/sodium-extra/wiki/Features).

### Reese's Sodium Options

**Target:** Replace Sodium's settings screen with a more navigable interface. The author explicitly describes it as an alternate **frontend** to Sodium's existing configuration API, not a new rendering algorithm. [Author project page](https://modrinth.com/mod/reeses-sodium-options).

**Mechanism:** It reorganizes options into scrollable vertical tabs, collapsible mod/option groups, and searchable pages. It adds per-option undo/reset overlays, keyboard/controller navigation, tooltips and optional IDs, themes, and a way to return to Sodium's default screen. These features improve finding and safely changing settings; they do not by themselves make chunk rendering faster. [Author project page](https://modrinth.com/mod/reeses-sodium-options).

**Limits:** It depends on Sodium and provides a client UI alternative. Any FPS change would be due to a changed underlying setting, not the options screen layout alone. [Author project page](https://modrinth.com/mod/reeses-sodium-options).

## Chunk work, world generation, and networking

### C2ME

**Target:** Increase throughput for Minecraft chunk generation, chunk I/O, and loading by using multiple CPU cores. Its name means Concurrent Chunk Management Engine. [Author repository/README](https://github.com/RelativityMC/C2ME-fabric).

**Mechanism:** C2ME restructures chunk work so generation and I/O can proceed concurrently. The inspected repository separates its implementation into chunk-system and chunk-I/O rewrites, scheduling, serializer, world-generation, lighting, and threading-fix modules. It attempts to keep vanilla behavior while allowing parallel execution and includes checks for misuse of random/thread-local state that could make parallel work fail in hard-to-debug ways. The author's README states that generation and loading use multiple cores; the module layout shows the scope of the intervention, but does not establish that every module is enabled in every distribution. [Author repository/README](https://github.com/RelativityMC/C2ME-fabric).

**Limits:** The author warns that custom world generators may rely on sequencing assumptions broken by concurrency, and that a world can generate differently between runs because vanilla generation is itself nondeterministic even when the mod aims at vanilla behavior. Parallelizing a chunk pipeline is therefore a correctness/thread-safety project, not merely adding more threads. [Author README](https://github.com/RelativityMC/C2ME-fabric).

### Noisium

**Target:** Speed up Minecraft's procedural world-generation work, especially population of newly generated noise-based chunks. The author repository was archived in November 2025; its description remains useful as a historical explanation of the mod's technique. [Author repository/README](https://github.com/Steveplays28/noisium).

**Mechanism:** The author describes optimizing `NoiseChunkGenerator#populateNoise` by writing generated block states directly into the chunk's palette storage rather than calling the ordinary higher-level block setter, whose extra calculations are useful during normal edits but redundant during initial bulk generation. The README also lists faster biome population, state sampling, and chunk unlocking in supported versions. The author says a previous biome multithreading step was removed because C2ME does that part better; Noisium's remaining per-thread work could coexist with C2ME's parallel generation. [Author repository/README](https://github.com/Steveplays28/noisium).

**Limits:** The direct-write fast path is specifically for *new chunk generation*; the ordinary setter's side effects may be necessary for later live edits. The author claims parity with vanilla world generation for the supported versions, but this research has not independently reproduced that claim. [Author repository/README](https://github.com/Steveplays28/noisium).

### Krypton

**Target:** Reduce Minecraft networking CPU and allocation costs, mainly for servers. The author says it keeps the vanilla wire protocol compatible where possible. [Author wiki](https://github.com/astei/krypton/wiki), [project page](https://modrinth.com/mod/krypton).

**Mechanism:** Its author lists optimized Netty handlers derived from the Velocity proxy, consolidated network flushes so the server performs fewer expensive flush operations, and smaller/faster packet-serialization paths. The author says native code is used selectively where it helps. These interventions affect communication between clients and servers rather than terrain meshing or local block simulation. [Author project page](https://modrinth.com/mod/krypton).

**Limits:** The author calls the mod work in progress and provides no universal stability or mod-compatibility guarantee. The project's optimization path is network/protocol-specific and its author says it is tuned for Linux. [Author project page](https://modrinth.com/mod/krypton), [author wiki](https://github.com/astei/krypton/wiki).

## Dependencies and boundaries

| Relationship found in author sources | Evidence |
| --- | --- |
| Nvidium uses Sodium as its base backend, and falls back/disables on unsupported NVIDIA hardware or when Iris shaders are active. | [Nvidium author page](https://legacy.modrinth.com/mod/nvidium); [Iris official FAQ](https://irisshaders.dev/) |
| Iris adds shader-pack support and ships with Sodium, but shader packs may require explicit integration for Voxy's distant geometry. | [Iris official site](https://irisshaders.dev/); [Voxy author page](https://modrinth.com/mod/voxy) |
| Entity Culling can remove entities hidden *inside* visible chunks, beyond Sodium's chunk-level entity visibility check. Its author says the server simulation remains intact. | [Entity Culling author FAQ](https://github.com/tr7zw/entityculling) |
| C2ME increases parallel chunk-work capacity, while Noisium optimizes some generation work done within that pipeline; Noisium's author removed overlapping biome multithreading. | [C2ME author README](https://github.com/RelativityMC/C2ME-fabric); [Noisium author README](https://github.com/Steveplays28/noisium) |
| MoreCulling requires shape/transparency knowledge to avoid deleting visible faces; its author exposes exemptions when other mods provide incompatible shapes. | [MoreCulling author explanation](https://github.com/FxMorin/MoreCulling/wiki/BlockState-Culling) |

This research does not claim that the mods can be installed in Unity, that their code should be copied, or that their Minecraft speedups predict SiliconSandbox performance. The separate [transfer analysis](minecraft-mod-transfer-analysis.md) assesses each mechanism against SiliconSandbox's existing world, simulation, visual-topology, save, platform, and benchmark requirements; it remains recommendations rather than canonical design changes.
