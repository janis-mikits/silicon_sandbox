# First-playable implementation plan

Reviewed 30 September 2026. The user approved this plan and saving it in the repository. Immediate implementation starts with slice 0 and its real local test/build gate; the four-state AND slice follows that gate and its prerequisite decisions. Approval of this plan does not resolve the explicitly open decisions below, authorize new dependencies, or authorize access outside the repository.

## Authority and current status

Follow [AGENTS.md](AGENTS.md) and the [README source hierarchy](README.md). Canonical documents in `docs/` govern game behavior; this document organizes implementation and does not replace them. The companion [requirement-to-test matrix](first-playable-requirement-test-matrix.md) defines planned evidence, not executed results.

Planning verified the repository root as `/Users/janismikits/Desktop/2026 Fall Semester/ECE 49595 SD II/SiliconSandbox_janis_version`, branch `main`, a clean working tree, and locally configured fetch/push remote `https://github.com/janis-mikits/silicon_sandbox.git`. The remote was not contacted. No implementation tests or builds ran during planning. No subagents were spawned.

The user subsequently selected “Approve for me”; the exposed approval reviewer is Auto-review with a workspace-write sandbox, not Full access. Writable roots also include the session visualization directory and temporary directories. These capabilities do not grant task authorization: the user requires explicit permission before accessing files outside this repository. Do not inspect user-level Codex configuration. No model/effort switch was verified during planning.

## Canonical reference key

| Key | Document |
| --- | --- |
| D | [Delivery and acceptance](docs/delivery-and-acceptance.md) |
| S | [Digital simulation](docs/digital-simulation.md) |
| T | [Circuit time and clock](docs/circuit-time-and-clock.md) |
| C | [Components and RTL timing](docs/components-and-rtl-timing.md) |
| P | [Physical connections](docs/physical-connections.md) |
| M | [Modules and packaging](docs/modules-and-packaging.md) |
| V1 | [First-playable data schema](docs/first-playable-data-schema.md) |
| SAVE | [Saving and recovery](docs/saving-and-recovery.md) |
| W | [Project vision and world](docs/project-vision-and-world.md) |
| UI | [Building and interface](docs/building-and-interface.md) |
| I | [Inspection and diagnostics](docs/inspection-and-diagnostics.md) |
| R | [Graphics and renderer contract](docs/graphics-and-future-tools.md) |
| PERF | [Performance and platforms](docs/performance-and-platforms.md) |

Also consult [open decisions](docs/open-decisions-and-cautions.md), [harnesses and Net Links](docs/harnesses-and-net-links.md), and [education/onboarding](docs/education-and-onboarding.md) for compatibility and milestone boundaries.

## Fixed requirements versus engineering proposals

Fixed: Unity 6.3 LTS Editor `6000.3.24f1`; flat 3D world; one-bit Constant Logic Sources, visible connectors, AND, SR flip-flop and world clock; four-state event-driven behavior; exact topology and persistent UUID identities; independent module instances; version 1 ZIP/JSON persistence; all five demonstration steps and internal correctness rows; accepted performance protocol.

Preserve checked `(wholeSeconds, picosecondsWithinSecond)` time, unsigned 64-bit seconds, 1 ps precision, rational clock scheduling, stable event order, settled sampling, simultaneous sequential commits and convergence handling. The first-playable clock defaults to 10 Hz, accepts 0.1–100 Hz and starts stopped at 0. Represent a later 1 GHz clock without promising real-time GHz throughput.

Preserve authored design as authority. Derived graph indexes, Unity object IDs, meshes, current values and event queues are not saved identities. Version 1 retains all 24 proper grid orientations even though player controls initially expose yaw only. Preserve type/pin snapshots, explicit spans/joins, per-instance identity chains, exact immutable versions and dependency closure.

Proposed engineering choices: assembly layout and interface names below; single-threaded deterministic simulation initially; immutable snapshots at subsystem boundaries; replaceable full graph derivation; bounded spatial render regions; separate dynamic signal presentation. A full graph rebuild is acceptable only while complete place/break/undo responses meet the provisional 100 ms target. Preserve unaffected runtime state through stable identities when replacing a graph.

Additional catalog components, harness operations, ordinary named Net Links, upgrades, physical re-expansion, NVM, persisted undo, Education and advanced graphics remain deferred. Their future compatibility requirements remain binding. No automatic simplification may erase authored structure, observations, diagnostic causes or gate counts. Do not introduce Burst, native code, background mesh jobs or third-party dependencies without evidence and approval.

## Proposed folders and assembly ownership

An assembly is a separately compiled group of C# files. Explicit boundaries prevent presentation from owning simulation or persistence.

```text
UnityProject/
  Assets/SiliconSandbox/
    Core/
      Contracts/
      Authoring/
      Graph/
      Simulation/
      Persistence/
      Application/
    Unity/
      Interaction/
      Presentation/
      Bootstrap/
    Editor/Build/
    Scenes/
    Catalog/
    Tests/
      EditMode/
      Integration/
      PlayMode/
      Fixtures/
  Packages/
  ProjectSettings/
scripts/
  verify-unity.sh
  verify-unity.ps1
.github/workflows/unity-verification.yml
verification/
  demo/
  benchmark/
```

Each code directory receives a matching `SiliconSandbox.<Name>.asmdef` when needed. Core assemblies must not reference UnityEngine or UnityEditor. Do not create speculative implementations for future slices.

| Assembly | Owns | May depend on |
| --- | --- | --- |
| Contracts | Typed identities, grid/orientation primitives, endpoint references, four-state values, immutable boundary records | Base C# libraries |
| Authoring | Sparse world, immutable module definitions, validation, edit candidates, packaging extraction | Contracts |
| Graph | Connectivity derivation, instance expansion, authored endpoint/runtime index maps | Contracts, Authoring |
| Simulation | Resolution, gates, SR state, scheduler, rational clocks, settled values and causes | Contracts |
| Persistence | V1 records, strict serialization, archives, hashes, library transactions/recovery | Contracts, Authoring |
| Application | Safe-boundary orchestration, atomic edits, graph replacement/state reconciliation, save/reset/package use cases | Core assemblies above |
| Interaction | Input, commands, previews, Configure/Inspect/package screens | Application, Contracts |
| Presentation | Region graphics, exact picking proxies, signal visuals, internal viewer | Read-only Application views, Contracts |
| Bootstrap | Scene composition/service wiring | Application and Unity assemblies |
| Editor | Verification/build entry points | Runtime assemblies, Unity Editor APIs |
| Tests | Independent fixtures and assertions | Assemblies under test |

Graph produces a runtime circuit description defined in Contracts. Simulation consumes it without reaching into authored storage or Unity objects. Unity requests advancement toward a time budget and displays the latest settled state. Slow frames may reduce achieved speed; they must never drop, reorder or suppress electrical events. Culling discards graphics only.

### Shared contracts: one owner

- `AuthoredId`, `ModuleVersionId`, `InstancePath`, `EndpointRef`.
- Exact grid/orientation transforms and component type-version definitions.
- Runtime circuit description and authored-to-runtime mapping.
- Safe-boundary commands and coherent authored revision publication.
- Settled signal snapshots and diagnostic causes.
- Region invalidation and exact selectable-part records.
- V1 serialization records and validation errors.
- Persistence outcomes distinguishing valid load, recoverable module loss and invalid world.

Freeze and review these interfaces before dependent parallel work. A name above is a proposal, not permission to change canonical identity or save semantics.

## Ordered runnable slices

Each slice ends with executable evidence. Early fixture scenes are development aids; later slices replace fixture-only interaction with the real player workflow. Test IDs refer to the companion matrix.

| Slice | Visible result and prerequisites | Rules | Tests and acceptance gate | Likely files/assemblies |
| --- | --- | --- | --- | --- |
| 0. Reproducible foundation | Empty flat-world smoke scene opens; native desktop build launches. Needs approved setup/template, available Editor and license. | W, R, V1 | Exact Editor pin, compilation, Edit Mode smoke test, Play Mode scene test and native build/player report. Create workflow and repeatable command with project. Runner blockage reported separately. | Bootstrap, Scenes, Editor, Tests, scripts, Packages, ProjectSettings, workflow |
| 1. Four-state circuit | Two sources, visible wires, AND and labels run in a fixture scene with clock stopped. Needs approved component geometry. | S, C, T, P | C01/C02; all 16 AND pairs, net resolution, professor logic sequence, live settling with clock stopped. | Contracts, Authoring, Graph, Simulation, minimal Presentation |
| 2. Build and inspect | Place/break/configure/toggle circuit, target pins, create junction/crossing and inspect connections. | P, UI, I, R | C01/C02/C05; source-body precedence, four-channel capacity, occupied-pin rejection, exact picking and truthful X/Z causes; invalid edits leave no partial changes. | Application, Interaction, Presentation |
| 3. Safe edits and identity | Rotation preview/confirmation and in-session undo/redo; unrelated identity/state preserved. Needs edit contracts. | T, P, V1, R | C05; 24 orientation transforms, yaw controls, split IDs, tag merge, rollback, region-boundary faces; coherent revision before resume. | Authoring, Application, Graph, Presentation |
| 4. Clock and SR | P, edge/cycle steps, Reset and frequency settings operate an SR circuit. Needs stop/restart decision. | T, C, D | C03/C04/C08; SR/edge tables, ties, simultaneous commits, rational phase, overflow, convergence; steps return settled. | Simulation, Application, Interaction |
| 5. Offscreen behavior | Stateful circuit progresses while away; return to current state. | W, S, T, R, I | C06; visible/hidden/unloaded graphics and varied frame schedules produce same expected electrical trace; no stale Inspect or lost state. | Application, Presentation, PlayMode tests |
| 6. Persistence foundation | Save/reopen unwrapped AND/SR design with runtime restart. Fixed module fixture supports library/archive tests. | SAVE, V1 | C07/C09; exact records, hashes, strict validation, interrupted-write recovery, initial sources/time/clock. | Persistence, Application, Integration tests |
| 7. Package extraction/publication | Region preview, exact geometry and port mappings; one immutable package published. Needs module-bounds decision and slice 6 stores. | M, T, P, V1 | C10; partial-object refusal, clipped routes, exit/re-entry separation, pass-through exclusion, fresh IDs, source unchanged; invalid mapping and durable-write failure leave no partial publication. | Authoring, Persistence, Application, package UI |
| 8. Independent instances | Place SR package twice; four independent controls plus clock; live internal Q/Q_bar. | D, M, S, V1 | C03/C04/C06; X/X→1/X→1/0→0/0, offscreen third edge, exact pinned version and instance-specific diagnostics; no captured live Q. | Graph, Simulation, Application, internal viewer |
| 9. Complete demo/recovery | All five steps, save at 0/0 and reopen X/X; exact-copy and placeholder recovery visible. | D, SAVE, V1, M | D1–D5 and full matrix; scripted journey and native Windows/Mac walkthrough; no lost design/topology/version. | Integration owner across existing assemblies |
| 10. Performance acceptance | Frozen 1,000-AND scene passes both platforms with correctness intact. | PERF, S, R | PERF01 protocol; complete regression after optimization; measured timing and throughput. | Fixtures, measurement harness, measured bottleneck owner |

Begin instrumentation by slice 2. Build the performance scene once packaging works; slice 10 is final acceptance, not the first measurement. Persistence hardening includes autosave, normal-exit saving, backup selection and durable package publication. In-session undo supports the benchmark; persisted undo remains deferred.

## Local verification and GitHub Actions design

These are proposed commands to implement, not existing or executed commands. From the repository root:

```bash
./scripts/verify-unity.sh --editor "$UNITY_EDITOR" --target StandaloneOSX
```

```powershell
.\scripts\verify-unity.ps1 -Editor $env:UNITY_EDITOR -Target StandaloneWindows64
```

Both wrappers perform the same logical checks: exact Editor/package pin; import/compile; Edit Mode unit/integration tests; Play Mode tests; native desktop build; deterministic built-player smoke scenario; result collection. Fail for compilation/build errors, failed tests, missing reports, unexpectedly empty suites and timeouts. Keep output/logs in a repository-local ignored artifacts directory.

Test command shape:

```text
<editor> -batchmode -projectPath <absolute-project-path>
  -runTests -testPlatform EditMode
  -testResults <absolute-results-path>
  -logFile <absolute-log-path>
```

Repeat for PlayMode. Wait for completion and inspect XML; do not prematurely terminate test runs with unconditional `-quit`. Build uses a separate `-executeMethod` entry point. Graphics-dependent tests require a graphics-capable session. Verify exact installed Test Framework CLI behavior in slice 0; the 1.6 command-reference page was unavailable during planning.

| Concern | Proposed CI design |
| --- | --- |
| Same checks | Windows and macOS jobs invoke repository wrappers used locally. |
| Pinning | Commit ProjectVersion.txt, package manifest/lock and asset .meta files; refuse Editor mismatch. |
| Runners | Prefer explicitly configured licensed Windows/Mac runners initially; hosted runners need a validated installation/licensing design. |
| Isolation | Separate checkout and Unity process per job; never share a writable Library directory concurrently. |
| Evidence | Upload test XML, Editor/build/player logs, build reports and summary even after failure; unique platform/run artifact names. |
| Reproducibility | Record commit, OS, Editor, packages and target; caches optional, clean import still verified. |
| Security | Minimal permissions, reviewed full action commit SHAs, no secrets exposed to untrusted PR code; protect self-hosted runners from untrusted execution. |
| Builds | Native Windows/Mac builds and smoke checks; choose Mac architecture and scripting backend before baselines. |
| Performance | Separate opt-in runs on frozen reference machines; CI timing does not replace acceptance. |
| Status | Workflow file can exist before provisioning; never claim active/passing until an actual run is inspected. |

Unity needs an activated license. Personal activation uses Hub; the documented Pro command-line workflow must not be assumed to apply to Personal. Runner setup, credentials, dependencies, build modules and remote repository access require authorization. No credentials belong in source or reports. No installation, account inspection or GitHub settings change is authorized by this design.

Current implementation status, 5 October 2026: the personal repository is public and has no configured dedicated Unity runners. At the user's direction, do not attach this personal Mac as a self-hosted runner. The committed workflow is manual-dispatch only until licensed runners are available; it is not active CI evidence. After clearing a stale Unity Licensing Client process, local macOS Unity verification again runs with normal app access. The latest archive/save-control revision passed 189 native Edit Mode tests, 13 Play Mode tests, a macOS player build, and the player smoke check. The complete professor walkthrough, graphical performance acceptance, CI run, and Windows native checks remain unverified; Windows performance is temporarily waived by the user.

Official references reviewed during planning:

- [Unity 6.3 Editor CLI](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)
- [Unity 6.3 Test Framework](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)
- [Test Framework 1.4 command reference](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html), background only; verify installed version.
- [Unity licensing](https://docs.unity3d.com/6000.3/Documentation/Manual/ManagingYourUnityLicense.html)
- [GitHub secure use](https://docs.github.com/en/actions/reference/security/secure-use)
- [GitHub workflow artifacts](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts)

## Agent workflow and model usage

No delegation is authorized by this plan alone. One integration owner coordinates future agents only when the user requests them. That owner controls shared Contracts, schema mappings, catalog identities, scenes, Project Settings, packages and integration order.

Each task card identifies one observable outcome, canonical references and test IDs, exact permitted files/assemblies, prohibited shared files, prerequisite revision/contracts, independent expected results, exact verification command, acceptance gate, open decisions and handoff requirements.

After contracts stabilize, independent simulator kernels/tests, persistence validation, presentation against fixed views and independent review can be parallel. Contract/schema changes, graph/state reconciliation, package/store/instance integration, shared scene/settings edits and final optimization/regression acceptance are sequential. No simultaneous edits to shared contracts, schema, scenes or Project Settings.

Separate worktrees may isolate future code tasks when authorized. They do not solve contract conflicts. An outside-repository worktree path needs explicit permission. Review every diff before integration and rerun affected tests plus the complete verification command afterward.

| Work | Requested model/effort plan |
| --- | --- |
| Planning | Astra Medium, selected in the app; do not claim a switch without verification. |
| Ordinary scoped coding with settled contracts | Sol Medium |
| Scheduler, topology/state reconciliation, durable packaging, difficult failures | Sol High or Astra |
| Independent cross-system review | Sol High or Astra with a bounded review brief |
| Mechanical renames, formatting, repetitive documentation | Luna with diff/test checks |

Additional agents help only with independent ownership and stable interfaces. They increase usage and integration risk on shared or unsettled work. Reserve Ultra for a stubborn, well-defined problem or unusually demanding review after narrower attempts fail; it cannot replace a missing specification or executed tests.

## Integration risks and detecting tests

| Risk | Prevention/evidence |
| --- | --- |
| X conflated with Z or false | Literal four-state tables and truthful causes; C01–C03. |
| Frame timing changes behavior | Explicit phases, simultaneous commits, varied-frame expected traces; C04/C08. |
| Hidden renderer disables simulation | Independent ownership; unload/recreate graphics during simulation; C06. |
| Geometry creates accidental joins | Explicit authored spans/joins; crossing/junction fixtures; C05/C09. |
| Rebuild loses identity/state | Instance-qualified stable mapping; unrelated edit retains Q/phase; C05/C06. |
| Package reconnects outside paths | Exact boundary fixtures and endpoint maps; C10. |
| Instances share mutable state | Immutable definitions only shared; separate runtime/events; C06. |
| Saves become checkpoints | Authored-only serializer and field inspection; save 0/0, reopen X/X; C07. |
| Interrupted library/world publication | Recoverable transaction and injected failures at every stage; C09/C10. |
| Optimization breaks picking/topology | Stable selectable parts, region-boundary edits and stale-result rejection; Play Mode checks. |
| Optimization hides authored signals/counts | Check all authored observations and structural counts against independent fixtures. |
| Acceptance shifts after measurement | Freeze artifacts/settings first and retain every latency/failure; PERF01. |

## Unresolved decisions and progress protocol

The plan's approval does not convert these recommendations into canonical rules. Continue unaffected work and ask before dependent implementation.

| Decision | Exact issue/options | Recommendation and blocking point |
| --- | --- | --- |
| Built-in geometry | Resolved 5 October 2026: the user approved one-cell source, AND, and SR footprints and exact version 1 quarter-cell pin coordinates. | The binding coordinates are recorded in the canonical [version 1 schema](docs/first-playable-data-schema.md#shared-authored-design). Validate saved pin snapshots against that type-version geometry. |
| Module bounds/footprint | V1 Grid describes sizeCells as captured bounding dimensions; module records/ports use exterior footprint. M permits compact player-chosen package size while retaining internal layout. | Distinguish captured internal bounds from exterior footprint; clarify whether bounds are derived or schema needs an authorized correction. Alternative: require exterior footprint to equal captured bounds. No silent V1 reinterpretation. Blocks packaging contracts. |
| Clock stop/restart | T separates P-stop from simulation pause but does not fully define a mid-interval stop/restart after source activity. | Preserve level and remaining interval rather than restart full interval; Reset remains phase restart. Resolve before slice 4. |
| Crowded cycling | UI leaves wheel hotbar/cycling precedence open while P requires cycling. | Separate remappable cycling key; retain wheel hotbar. Approve key before slice 2. |
| Movement/orientation | Creative-style movement and approach-based orientation lack exact numerical/tie rules. | Approve focused control specification before control acceptance; fixtures can proceed. No outside reference browsing without permission. |
| Engineering setup | Render pipeline, input/UI packages, serializer availability, backend/architectures and runner/license provisioning not selected. | Review minimal setup/dependency list before slice 0; no installation implied. |

M's final optimization paragraph retains an “Open” note, while S and open-decisions record the later accepted fidelity invariants. The explicit later dated decision governs; flag the stale cross-reference for an authorized documentation correction rather than changing rules here.

Later questions about harness mappings, persisted undo, expansion anchoring, incompatible families and other components do not block the professor subset.

Agents may choose routine private helper names, local file organization, queue structures and test parametrization within their assignment. Ask before changing behavior, scope, saves, shared/public interfaces, dependencies or material performance/platform tradeoffs. Approved game decisions go in the relevant canonical document; adjust open-decision records and INDEX routing when needed.

After each slice report visible result/test IDs, exact changed files, exact commands and exit/results with artifact locations, passed/failed/blocked/unrun checks separately, assumptions, remaining risks, decisions, integrated revision and next task. CI success requires real run evidence.

## Next implementation task

Start slice 0 after setup choices and outside access permissions are resolved: pinned project foundation, verification wrappers, workflow and one runnable flat-world smoke scene. Exit only with a real local test/build report; clearly identify unavailable platform/CI checks. Then begin the literal-table four-state AND slice after its geometry prerequisite is approved. No pushes, PRs, installations, subagents or external access are implied.
