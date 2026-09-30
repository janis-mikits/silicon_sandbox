# Appendix C Original research backlog (archive)

**Historical source record.** This file preserves the original DOCX research questions, links, and quotations for traceability. Many have since been answered or superseded. Read [the current research backlog](research-backlog.md) for **only the remaining actionable questions**, and use [INDEX.md](../INDEX.md) to find the canonical decisions. The source wording below is retained as archival material, not as instructions to reopen settled design choices.

## In this file

- [Engine selection](#engine-selection)
- [Simulation architecture and HDL integration](#simulation-architecture-and-hdl-integration)
- [Module representation and reconstruction](#module-representation-and-reconstruction)
- [World representation and chunking](#world-representation-and-chunking)
- [Textures and voxel geometry](#textures-and-voxel-geometry)
- [Rendering and mesh optimization](#rendering-and-mesh-optimization)
- [Minecraft performance mods](#minecraft-performance-mods)
- [Level of detail and sparse voxel octrees](#level-of-detail-and-sparse-voxel-octrees)

Original research retained for provenance. The questions, supporting notes, links, and quotations below preserve the source's historical wording; some have been answered and some contain superseded premises. In particular, rendering is not required for simulation, the world clock does not reevaluate every object, the backend and engine have since been selected, and core RTL has no physical propagation delay. The later confirmed requirements govern; preserving a question does not accept its premise.

RESEARCH: The following questions remain open. They are grouped by topic so that implementation decisions can be investigated together.

## Engine selection

RESEARCH: Determine which game engine is the best for building and using a game like this, according to all of the specifications and requirements I give you. Once you determine which game engine is best for this game, determine which version of that game engine would be the best, while still being very stable and have support from the developer. For example if you choose Unity, which LTS version of Unity would be best and most stable for development?

## Simulation architecture and HDL integration

RESEARCH: SPEND EXTRA TIME ON THIS BECAUSE IT IS IMPORTANT TO THE FUNCTIONING OF THE GAME. Would it be more efficient to convert all of the circuit components and modules in a world file into a netlist that can be saved and run with hardware description language (HDL) simulators in the background? That way, the game does not need to perform calculations or updates in-game to simulate circuit functionality, like how Redstone is simulated in Minecraft. If the entire circuit is saved in hardware description language, then it can be simulated in the background much more efficiently than it could be if done in-game. Then, each time a component or module is added to the circuit, the HDL file would update. If this system were used, then how would that affect how the rendering engine works? Would 16x16xworld_height chunks still be beneficial?

Would chunks make circuit simulation difficult or slow? If circuit simulation is able to be done off-game, and then the results imported into the game-file to display the results on the in-game circuit, then it would be okay to not show the wires and components lighting up during circuit simulation, because the simulation would occur in nano-seconds, which would not be possible to display on screen. Therefore, when the user turns on a source block, the simulation would happen off screen, in HDL, and then the results would show up on screen afterwards. The user could then use in-game EDA style tools and waveforms to see what happened during simulation.

If this is best way to design this game, then there could also be a slower and more educational version where the player turns the game clock down to less than 10Hz, and then the HDL simulator would have enough time to compute the simulation, and display the results in real-time on the circuit, so the user could see how signals pass through each of the wires, components, and modules.

RESEARCH: Should the game use a native C++ Dynamic Link Library (DLL) as the primary simulation engine to decouple logic processing from Unity rendering? Or should it use something else?

RESEARCH: Does Minecraft only update blocks if an adjacent block experiences an update? Therefore is the game able to render more efficiently because it is not constantly updating every block in the world all at once, and is instead just updating a block if an adjacent block is updated? Even if Minecraft doesn’t do that, could SiliconSandbox do something like that? Would it even work, or be more effective than some other method? How would this work with the world CLK, since every block is updated with every edge of CLK?

Can the entire world file somehow be stored as an optimized SystemVerilog file, that is then run on SystemVerilog compilation software, and simulated via that software, instead of simulating the circuit in-game? Would that be faster or even feasible to do?

Can circuits be turned into a netlist of some kind that is then simulated outside of the game, so that the game does not need to rely on block updates to run a circuit, and can instead just simulate a circuit with existing Hardware description languages? Would this be faster or more efficient? Essentially every world file would be translated into hardware description languages and could therefore be optimized by existing HDL compilation software and EDA tools. I don’t have any paid access to any such tools, so would that even be possible? If it’s possible, is it feasible or even more efficient to do?

## Module representation and reconstruction

RESEARCH: What is the best way to implement the functionality of a module? What is the best way to save a circuit so that its logic can be replicated by the module object? Would the module just directly save a netlist of the circuit and the circuit simulation logic for the entire world file would know to simulate the netlist stored in the module whenever its input signals are powered, essentially pretending like an entire circuit exists in place of the module block for the sake of simulation?

RESEARCH: The user should be able to view the contents of a module to see what the circuit inside of it is doing. Therefore, a circuit needs to be recreated from however the module is storing the data. What is the best way therefore to store the functionality of a module so that it can be re-expanded back into the original circuit?

## World representation and chunking

RESEARCH: I am primarily modeling this game after Minecraft, so do research on optimal ways of designing a Minecraft game and design the game so that it runs as efficiently as possible, using the best design practices that game developers have developed for designing Minecraft. This will include all of the optimization they have for world generation, block rendering, block textures and texture grouping/meshes, and every other way that Minecraft runs optimally. I am not a game developer so I do not know everything that they do. Do comprehensive research to understand how Minecraft is optimized, so that SiliconSandbox can run as efficiently as possible.

RESEARCH: Would it be more efficient to have a chunk system similar to in Minecraft, where the world is sectioned into chunks that are 16 blocks wide, 16 blocks long, and as tall as the world height limit? These chunks are simply referred to as “chunks”.

RESEARCH: how to do each of these things most efficiently:

RESEARCH: Unlike in Minecraft, SiliconSandbox needs all chunks with player placed components to be rendered so that all logic components can run at the same time without being suddenly deactivated when the player moves out of render distance.

RESEARCH: Render each chunk as a single game object so that there are fewer objects to render.

RESEARCH: To make the game run more efficiently, would it be better to just have the sandstone ground be a single entity, not made out of blocks, so that the game does not need to render so many blocks? Since the blocks cannot be broken at the moment, would that be okay? Or would this mess up the way that chunks are rendered? Would it mess up how the grid system of placing blocks works? Would players still be able to see an outline of the block that they are looking at with a thin black box, just like when you are looking at a block in Minecraft?

## Textures and voxel geometry

RESEARCH: if a texture atlas like Minecraft uses would be the best practice for a game like this, or should each block be designed in blender? Which is better for game performance? Does it make a difference in how the game looks if the same textures are used in a texture atlas vs a Blender object?

RESEARCH: should this game use voxels? Can smooth voxels or marching cubes be used to make better looking textures without sacrificing rendering performance or game fps? Can the technique shown in this reddit post be used (https://www.reddit.com/r/VoxelGameDev/comments/1v9usl8/making_voxel_assets_look_like_lowpoly_using/)?

## Rendering and mesh optimization

RESEARCH: culling works in Minecraft rendering and how it can be applied to my game.

RESEARCH: each of the following techniques or topics to learn how Minecraft is written. Determine if there are any similar topics or techniques that can be used in the design of this game. Research the most optimal ways to design a block placing game like Minecraft when optimizing for performance:

3D Geometry: Each block is a physical 3D model with vertices, edges, and faces, not just a flat 2D image wrapping empty space.

Face Culling: The engine deletes hidden faces from the render list entirely so your computer wastes no power drawing covered surfaces.

Texture Atlases: Minecraft stitches individual block textures into one massive image file in your graphics memory at startup.

Vertex Buffer Objects (VBOs): The game groups all visible 3D faces inside a chunk into a single geometric object so the graphics card can draw them all at once.

RESEARCH: how to use Mipmaps to lower block textures pixel width when they are viewed from very far away, in the same way that Minecraft does it.

RESEARCH: how each of these topics can be used in the design of my game: block culling, uv recalculation, face merging with greedy meshing, the chunk system, and recalculation of the only faces affected when a block is placed with greedy meshing, and finally materials are dynamic depending on the blocks used in the chunk.

RESEARCH:

There are a lot of different algorithms for geometry simplification, so it's difficult to give a one size fits all answer here. From a really naive perspective, for each block, you need to draw 2 triangles per face, 6 faces per block. For a more advanced approach, you can hide faces which are blocked by other blocks. For an even more advanced approach, you can "merge" neighboring faces which share the same texture. For an even more advanced approach, you can actually merge distant faces IF they both share the same texture and every block between them is occluded or also shares the same texture. This results in some overdraw, though, so depending on your geometry it may actually perform worse (testing is needed). From a high level perspective, the idea is that you could draw a 16x16x16 chunk of blocks as 4,096 individual cubes with 49,152 individual triangles, or, if they're all the exact same block, you could "merge" the entire chunk and just draw a single cube with appropriate UVs such that it looks like 4,096 blocks.

RESEARCH: Are the following things beneficial for a game like SiliconSandbox?: Some level of greedy meshing (combining adjacent polygons of the same block type into one poly). Doesn't need to be perfect, just pretty good. Multi-threaded mesh generation (generating meshes on a background thread, ideally multiple). LOD meshes. Farther meshes are downsampled version of the original data. Some system for managing the memory of the voxel data in RAM, either aggressively faulting data to disk or compressing it in RAM

RESEARCH: Are the following sub-bullets helpful for a game like this? If so, how would you include them and use them in design?

Sparse voxel octrees with intelligent LOD and shader geometry generation/tessellation, with a custom tessellator that reduces the geometry by running a shape matching algorithm, or a dual surface nets approach or classic matching cubes or matching tetrahedron may be more appropriate. In order to save all of that data, generate chunks using noise functions that can sample those functions at different resolutions if needed.

## Minecraft performance mods

RESEARCH: how the Voxy Minecraft mod and Nvidium Minecraft mod work and how to implement the techniques they use in the design of my game.

RESEARCH: how the each of the following performance mods work in Minecraft and determine if the techniques they use can also be used in the design of my game to make it more efficient or have higher performance:

Sodium, Lithium, ModernFix, FerriteCore, Entity Culling, MoreCulling, Iris, ImmediatelyFast, Sodium Extra, Reese's Sodium Options, C2ME, Noisium, Krypton

## Level of detail and sparse voxel octrees

RESEARCH: What is u/Green_Gem talking about on reddit when they say in this feed (https://www.reddit.com/r/VoxelGameDev/comments/1i4r9zl/if_you_were_to_develop_something_like_this_with/) “games like Minecraft which generate chunks using noise functions can sample those functions at different resolutions if they want to. You're used to a chunk sampling the noise at every block, some of that chunk being populated with things like trees and ore veins, then structures getting sprinkled on top. There's more but I'm simplifying. If you're willing to skip some steps and make lower res versions of some of the population algs, you can check the noise function(s) every other block, every fourth block, or even just the blocks at each corner of the chunk (this type of terrain would use cubic chunks). This is how Distant Horizons works. The LoD chunks are "fake" until a player gets close enough to generate them at full resolution, then the mod has definitive info to work with next time you get far away from that chunk. This technique works especially well with something like a voxel octree, since adding more detail as you need it is already baked into the algorithm. Ray tracing is popular with this system for exactly the same reason. You get dynamic step size ("detail as you need it" but for light) for free (kinda). If you've seen a ton of "I wrote a sparse voxel octree engine with crazy detail and ray traced graphics" videos lately, that's why. "We only need this eventually" is friendly to async and multithreaded patterns, so you'll also commonly see these engines be written in Rust or Go (languages good at both).”

u/Leonature26 responds with “Very interesting, but why is Minecraft not optimized to use this system? There must be some reason, some kind of drawback to this right? Is there a documented implementation that I can read more of somewhere?” and u/Green_Gem_ answers with “Not all computers can run raytracing. Not all computers have enough cores to run multithreaded terrain sampling. Not all computers have the speed to regenerate octrees whenever anything moves without stuttering. Minecraft benefits from the rendering being straightforward to both implement and scale down. Need to run on a console? Same code, shorter render distance. Need to run on a phone? Same code, fog to hide the even smaller render distance. Minecraft is designed to work the same way at different scales on different devices. This is why we saw that huge push for the cross-platform Bedrock version. For documented examples, you can literally just put "sparse voxel octree" into YouTube and scroll forever. There are tons of ways to combine all this (or implement any given part), so I can't give you a specific reference.”

Another response by u/Green_Gem_ “Check out this linked GitLab issue, The concept of "original fake chunks" that are generated with "shortcuts" is very common in LoD systems for procedurally generated games. If the chunk ever gets properly generated, you throw away the original fake LoDs and generate new ones based on what's actually there. When the chunk changes (or changes too much), you regenerate its LoDs. Regarding player-modified terrain in multiplayer, this is an ongoing issue (that wouldn't exist if LoDs were baked into Minecraft's engine). This thread shows the team working on it, and this plugin describes the issue it attempts to fix as "Distant Horizons will work fine without this plugin, but then each client will have to be within normal view distance of chunks to load them, and they will not receive updates for distant chunks when they change.”
