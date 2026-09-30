# Engine selection research

**Date:** 29 September 2026  
**Status:** Research for [item R-01](docs/original-research-backlog.md#engine-selection). The user accepted Unity 6.3 LTS with starting Editor 6000.3.24f1 on 29 September 2026; the canonical decision is in [project vision](docs/project-vision-and-world.md#definitions-and-extensibility). This file records evidence and alternatives, not a second operative specification.

## Recommendation

Use **Unity 6.3 LTS**, starting with **Editor 6000.3.24f1** (released 10 September 2026), for the first playable and the full game. Pin the exact Editor version in the project when development starts. Before any later patch upgrade, review its release notes and run the project checks again. Unity says 6.3 LTS receives regular fixes and support through **December 2027**; this is the clearest published support horizon among the candidates evaluated here. [Unity release support](https://unity.com/releases/unity-6/support) · [6000.3.24f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.24f1)

This is a recommendation based on documented platform support, support lifetime, available tooling, and the project's risk priorities. It is **not** evidence that the specified 60 FPS/1080p/1,000-equivalent-gate target has been achieved, nor a selection of the circuit simulation backend.

## Project criteria from the canonical spec

- Windows and macOS delivery, with current development on an Apple Silicon Mac. [Platforms and performance](docs/performance-and-platforms.md)
- Editable 3D block world and fine-grained connection interaction. [Project vision](docs/project-vision-and-world.md) · [Physical connections](docs/physical-connections.md)
- Four-state, event-driven circuit behavior that continues offscreen; rendering and simulation must be separable. [Digital simulation](docs/digital-simulation.md) · [Circuit time](docs/circuit-time-and-clock.md)
- Stateful packaged circuit instances, stable save/load behavior, and later Education progression. [Modules](docs/modules-and-packaging.md) · [Saving](docs/saving-and-recovery.md) · [Education](docs/education-and-onboarding.md)
- First playable acceptance and the specified performance benchmark. [Delivery](docs/delivery-and-acceptance.md) · [Performance](docs/performance-and-platforms.md)

## Candidate comparison

| Candidate | Relevant evidence | Assessment for this project |
| --- | --- | --- |
| **Unity 6.3 LTS / 6000.3.24f1** | Unity publishes a support end date of December 2027 and regular LTS patches. The named patch offers macOS ARM64 and Windows Editor installers and build-support modules. Unity provides an official Edit/Play Mode test framework and batch test execution. [Support](https://unity.com/releases/unity-6/support) · [Patch](https://unity.com/releases/editor/whats-new/6000.3.24f1) · [Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html) | **Best provisional fit** for a project prioritizing stable, supported development and repeatable regression checks across the two target platforms. The circuit rules and voxel/connection representation still require project-specific implementation and measurement. |
| **Unity 6.6 Update / 6000.6.3f1** | Unity lists this newer release as of 24 September 2026. Unity says Update releases receive the same QA and support level as LTS while current, with newer features and platform/performance improvements, but each is supported only until the next Update release. [Release](https://unity.com/releases/editor/whats-new/6000.6.3f1) · [Support](https://unity.com/releases/unity-6/support) | A real alternative within Unity, potentially better if a measured project need benefits from a newer feature or fix. It has a shorter published support horizon than 6.3 LTS, so there is more pressure to move versions later. |
| **Godot 4.7.2 stable** | Official stable download offers macOS and Windows builds. Godot is MIT licensed, supports C#/.NET and native extensions, and has MultiMesh for repeated 3D objects; MultiMesh does not individually frustum-cull instances. [Release](https://godotengine.org/download/archive/4.7.2-stable/) · [License](https://godotengine.org/license/) · [MultiMesh](https://docs.godotengine.org/en/4.7/tutorials/performance/using_multimesh.html) | A credible alternative, particularly if open-source licensing and source access matter most. The current research did not establish a fixed, comparable multi-year support commitment for this version. Its scene and rendering approach must also be benchmarked against the actual interaction density. |
| **Unreal Engine 5.8** | Epic documents Windows/macOS packaging. Its published recommended Windows development configuration includes 32 GB RAM and at least 8 GB graphics memory. [Packaging](https://dev.epicgames.com/documentation/unreal-engine/packaging-your-project) · [Hardware](https://dev.epicgames.com/documentation/unreal-engine/hardware-and-software-specifications-for-unreal-engine) | Capable, but its heavier development requirements and rendering-oriented feature set offer no demonstrated advantage for this circuit-focused block world. The current development Mac has 16 GB RAM, below the cited 32 GB Windows recommendation; that comparison is indicative, not a Mac compatibility test. |

Unity's own guidance also says its Update releases are production-ready and generally preferred for new or mid-cycle productions. Choosing 6.3 LTS here is a project-specific judgment favoring its known support window and version stability; it is not a claim that Unity says Updates are unsafe. [Unity release support](https://unity.com/releases/unity-6/support)

## What this choice does and does not settle

The engine owns the player-facing world, rendering, input, UI, and platform builds. The canonical spec requires offscreen circuit behavior, so simulation cannot depend on a block being visible. The separate R-02 backend question was subsequently decided in favor of a purpose-built C# event-driven simulator; see [digital simulation](docs/digital-simulation.md#runtime-architecture-decision). A C++ plug-in remains a measured later option, not the initial backend. [Circuit time](docs/circuit-time-and-clock.md) · [Development readiness audit](development-readiness-audit.md)

An external HDL tool should not be assumed to supply the required behavior. For example, Verilator's own guide calls it mostly a **two-state** simulator, whereas SiliconSandbox explicitly needs X and Z. This does not rule out every external tool; it means backend selection needs a direct conformance test. [Verilator language guide](https://veripool.org/guide/latest/languages.html) · [Digital simulation](docs/digital-simulation.md)

## Checks during the first playable

1. Confirm the selected Unity patch opens and builds on the development Mac, and produces target-platform builds. A Windows run still needs a Windows machine or test environment.
2. Measure the actual first playable against the [canonical benchmark](docs/performance-and-platforms.md#benchmark), including the 1,000-equivalent-gate case, at 1080p on agreed hardware. The user explicitly chose this approach over a separate disposable engine comparison prototype to conserve time and AI use.
3. Verify the selected C# four-state event simulation, including offscreen behavior, in the actual first playable. Profile before considering a different backend; the engine choice does not itself prove simulator performance.
4. Review the selected patch's known issues against the actual rendering/UI approach. The 6000.3.24f1 notes list unresolved issues, so “LTS” does not mean bug-free. [Patch notes](https://unity.com/releases/editor/whats-new/6000.3.24f1)

**Remaining risk:** Moving to another engine after the first playable would require porting engine-specific scene, rendering, input, UI, and build work. It is possible, but not a low-cost switch. No engine has been installed and no game project has been started as part of this research.
