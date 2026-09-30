# Project vision and world

## In this file

- [Purpose and learning rationale](#purpose-and-learning-rationale)
- [Modes and platform scope](#modes-and-platform-scope)
- [Definitions and extensibility](#definitions-and-extensibility)
- [Default sandbox and boundaries](#default-sandbox-and-boundaries)
- [World data and rendering architecture](#world-data-and-rendering-architecture)

## Source document preface

**Source title:** SiliconSandbox

**Source subtitle:** Revised Game Design Specification

Decisions consolidated through 29 September 2026

SiliconSandbox is a single-player 3D digital-design sandbox for building circuits from logic gates, packaging them into reusable modules, and learning successive levels of abstraction. This specification combines the original design with the final decisions from the twelve-area discussion. It defines the complete core game and separates that scope from the smaller professor demonstration and later educational and EDA features.

Status convention. Unqualified requirements in the main specification are confirmed by the original design or an accepted chat decision. Proposed text needs approval; Open text identifies an unresolved implementation or design choice; Future text is outside the core release. A confirmed future direction is not a commitment to deliver it in the first release. Accepted provisional numbers remain provisional. Later explicit decisions govern earlier conflicting text.

The original research questions, supporting notes, and quotations are retained as a historical source in [Appendix C](original-research-backlog.md). Many have since been researched or decided; the [current research backlog](research-backlog.md) contains only remaining actionable questions. Neither backlog file overrides the operative requirements in Sections 1–12.

### Original Document map

- Project vision and world
- 1 Digital simulation and signal values
- 2 Circuit time and world clock
- 3 Physical connections and rotation
- 4 Harnesses and Net Links
- 5 Module lifecycle and packaging
- 6 Components and RTL timing
- 7 Building and interface
- 8 Inspection and diagnostics
- 9 Saving and recovery
- 10 Performance and platforms
- 11 Education and onboarding
- 12 Delivery and acceptance
- Appendix A Graphics and future tools
- Appendix B Open decisions and technical cautions
- Appendix C Original research backlog
- Appendix D Review findings and change log

## Purpose and learning rationale

The game gives computer engineering students a spatial, hands-on way to understand digital logic while having fun. Students can start with individual gates, build functional blocks, package those circuits, and use nested modules to build increasingly complex systems and functioning integrated circuits. A reusable four-bit multiplexer, for example, avoids rebuilding or copying its entire circuit every time a larger design needs it.

The motivating problem is the difficulty of translating hundreds of lines of SystemVerilog into an understandable hardware design. RTL diagrams help, but the intended experience lets students stand inside and navigate a three-dimensional representation, with flight providing an overhead view of large circuits. The educational benefit is a design hypothesis, not a measured outcome of this project.

Minecraft supplies the voxel placement and creative-sandbox inspiration. SiliconSandbox supplies digital components, explicit wiring, and packaging to reduce sprawling constructions and teach abstraction. The original comparison with Minecraft redstone and Logic World, and the original educational evidence claims, are retained below as rationale needing verification rather than current verified facts.

### Original rationale claims pending verification

Unverified source claims. The original document states that Purdue ECE 270, ECE 337, and ECE 437 use SystemVerilog and each has a GPA average below 3.0; that tactile, immersive environments improve technical mastery; and that educational games improve motivation. It contrasts diagrams and code with physical breadboarding and spatial interaction. These claims require sources before use as evidence in a proposal or publication.

Unverified comparison. The original describes Minecraft and Logic World as partial precedents: Minecraft redstone is not intended to replicate electronic components, has large components and no circuit packaging; Logic World is described as having realistic components but lacking packaging into higher abstraction levels. It argues that widely spread wires and switches confuse beginners. These are the original comparison claims, not a verified assessment of either product’s current capabilities.

## Modes and platform scope

The core is single player on Windows and macOS PCs and laptops. Freeplay provides all available default modules and educational material from world creation. Education mode is the lowest-priority major mode: learners start with single-bit primitives, construct modules themselves, and unlock optimized developer-built equivalents. Sections 11 and 12 define the progression and delivery stages. Multiplayer and feasible VR headset support are future possibilities.

## Definitions and extensibility

A game object is a block, component, or module. A pin is a component or module input, output, or inout connection carrying 1–128 bits. A connector is a wire, harness, or Net Link. Looking at an object means targeting it with the player’s crosshair. A net is an electrical connection; a harness contains separately resolved bit nets, not one shorted signal.

Keep specified values configurable and make blocks, components, default modules, menus, textures, and world-generation parameters easy to extend without breaking existing functionality or requiring a complete rebuild. The first-playable settings and save-compatibility policy are fixed below; roles for later settings remain open until their features. The original notes mentioned Unity only as an example. **Decision, 29 September 2026:** use Unity 6.3 LTS, starting with Editor 6000.3.24f1, for the first playable and intended full game. The user chose to evaluate engine fit through the first playable professor milestone rather than build a separate disposable performance prototype. The primary simulation backend was separately chosen as a C# event-driven system; see [digital simulation](digital-simulation.md#runtime-architecture-decision). See [engine selection research](../engine-selection-research.md) for the dated engine evidence and alternatives.

**First-playable configuration compatibility decision, 30 September 2026:** The world-clock frequency and Constant Logic Source initial state/value are player-facing settings as already specified. New Game exposes configurable world dimensions; floor material, thickness, breakability constraint, and boundary style remain developer-configurable for the first playable, with the specified unbreakable one-block sandstone floor as the default. The first-playable save stores the resolved chosen world-generation values and component settings rather than pointers to mutable application defaults. Changing a developer default or catalog asset later affects new worlds; an existing world's circuit geometry, pins, topology, and saved settings change only through an explicit player edit or a versioned save migration. Texture/art replacements may change appearance without redefining connectivity or pin positions. Later Education catalog controls and full developer-versus-player setting policy can be added without reinterpreting version 1 world records.

## Default sandbox and boundaries

Generate a finite, Minecraft Superflat-style creative sandbox, provisionally 1,000 blocks wide by 1,000 blocks long with a height limit of 300 blocks. The player may neither fly nor place objects above the limit. There are no mobs, generated structures, survival mode, or required physical support for circuits.

The generated floor is one block thick, using smooth sandstone. It is unbreakable to avoid exposing a void. Inventory sandstone remains freely placeable and breakable. Terrain material, thickness, breakability, dimensions, and height limit must be easy to change rather than hardcoded.

A bounding wall surrounds the terrain and rises into the sky. The player cannot touch, pass through, or break it. It should resemble the wooden walls of a child’s backyard sandbox, with trees visible beyond and toy trowels and rakes propped on the wall. The wall may be a custom non-placeable object rather than many blocks. World data and backups are local, not a cloud database; [Section 9](saving-and-recovery.md) specifies saving.

## World data and rendering architecture

**Authored grid coordinate decision, 30 September 2026:** Save occupied world cells as integer `[x,y,z]` coordinates. Positive x is east, positive y is up, and positive z is north. The generated floor occupies the `y = 0` layer; the first player-placeable layer above it is `y = 1`. The southwest floor cell is `(0,0,0)`. Configurable world width, length, and height are measured from that authored origin. This is a durable save-coordinate convention, independent of Unity's internal transforms. A module blueprint uses the same axes in its own local grid; its local `(0,0,0)` is the minimum x/y/z cell of its captured bounding region before placement. Its placed local cells are transformed by the saved [orientation](physical-connections.md#post-placement-rotation) and an instance anchor cell. The provisional world dimensions and height limit above retain their provisional status.

**Confirmed, 29 September 2026:** The professor milestone and intended full game must keep circuits simulating even when their graphics are out of view. Rendering or hiding an area must never determine whether its circuits run. This reiterates [digital simulation](digital-simulation.md#runtime-architecture-decision) and [circuit time](circuit-time-and-clock.md#clock-and-event-model).

**Decision, 29 September 2026:** Store player-placed world content in a sparse grid-indexed authored design rather than allocating an object for every empty cell. Represent the generated unbreakable sandstone floor by its configurable world-generation rules and render it efficiently, while preserving its specified one-block thickness, grid targeting/outline, collision and placement behavior, and unbreakability. Player-placed sandstone remains ordinary breakable content. The floor's number of GameObjects or mesh pieces is not a design requirement. Choose one or several pieces according to measured performance and implementation simplicity; preserve the floor's specified behavior in either case.

**Decision, 29 September 2026:** Organize visible placed geometry into bounded spatial render regions with vertical subdivisions, so a local edit need not rebuild the entire world's graphics. Regions outside the relevant view may have their graphics culled or unloaded. **Culling or unloading graphics must never remove authored circuit data, stop simulation, discard electrical state, or alter results.** The whole-circuit simulation graph remains independent of rendered regions. When an area becomes visible again, its graphics show the latest settled simulated state. Precise connector, pin, channel, and floor-cell targeting must remain available despite batching or merged render meshes; see [physical connections](physical-connections.md).

This foundation applies to both the first playable professor milestone and the intended full game. [Rendering and update invariants](graphics-and-future-tools.md#rendering-and-update-invariants) now require certain hidden opaque faces to be omitted, local edits to affect only relevant graphics regions and boundaries, and changing signal colors to avoid reconstruction of unrelated static geometry. The [renderer boundary contract](graphics-and-future-tools.md#renderer-boundary-contract) specifies how derived graphics remain replaceable without taking ownership of authored data, simulation, or exact targeting. No 16 × 16 × full-height chunk shape, one GameObject per region, particular floor mesh layout, mesh/batching algorithm, or render pipeline has been accepted. Exact region dimensions and graphics techniques remain choices to measure against the [performance benchmark](performance-and-platforms.md#benchmark). See the [world representation research](../world-representation-research.md) and [Minecraft optimization research](../minecraft-optimization-research.md) for rationale. The first playable's benchmark does not prove performance for every later large circuit.
