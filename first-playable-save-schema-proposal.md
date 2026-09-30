# First-playable save schema proposal

**Status:** Historical review draft, superseded 30 September 2026 by the [canonical first-playable data schema](docs/first-playable-data-schema.md). This file preserves the intermediate questions; it is **not** the current JSON contract. Use the canonical schema and its subsystem dependencies for implementation. Any fields or open questions below that differ from the canonical schema are historical.

## Contents

- [Scope and invariants](#scope-and-invariants)
- [Archive entries](#archive-entries)
- [Proposed logical records](#proposed-logical-records)
- [References and validation](#references-and-validation)
- [Save and reopen behavior](#save-and-reopen-behavior)
- [Open schema choices](#open-schema-choices)

## Scope and invariants

This draft covers the accepted first-playable subset: generated flat world, placed blocks, one-bit Constant Logic Sources, AND gates, SR flip-flops, one-bit physical connectors, packaged fixed module versions, ports, and two independent placements. It leaves room for nested versions and later wider connectors without treating their unapproved editing details as first-playable behavior. The existing container is one ZIP file with `manifest.json`, `world.json`, and `modules/<version-id>.json` entries. The authored world and fixed module-version designs are authoritative; the netlist, rendered geometry, transient component values, pending events, and clock phase are derived or transient and are not saved as authoritative records.

Proposed field spellings below are **new choices for review**. The saved record must preserve the exact authored 3D design and explicit electrical connections. A module instance refers to a fixed version; it does not contain a mutable copy of that version's design. Reopening creates separate fresh simulation state for each instance. The logical schema must not equate a render region with a circuit boundary.

## Archive entries

| Entry | Proposed top-level fields | Role and existing constraints |
| --- | --- | --- |
| `manifest.json` | `formatVersion`, `worldId`, `entries`, `moduleVersions` | Declares the version and every required archive entry. `entries` maps each non-manifest path to the SHA-256 digest of its uncompressed stored bytes. `moduleVersions` lists each embedded family/version ID pair, path, and exact child-version dependencies. The manifest is not included in its own hash list. |
| `world.json` | `worldId`, `worldSettings`, `player`, `inventory`, `design` | Stores the generated-world parameters, clock configuration, saved player data, inventory/hotbar state, and authored placed content. Its `worldId` must equal the manifest ID. |
| `modules/<version-id>.json` | `moduleFamilyId`, `moduleVersionId`, `name`, `footprint`, `ports`, `childVersionIds`, `design` | One immutable blueprint per exact embedded version. The filename ID, record ID, and manifest ID must agree as UUID values. The design is independent of the source world circuit. |

The already accepted UUID text form is lowercase hyphenated text when written. The loader validates and compares UUID **values**, allowing either letter case in that syntax. `formatVersion` is a format identifier, not a Unity or game release number. Its initial value and migration table are implementation details still to define.

## Proposed logical records

### Authored design shared by world and module versions

`design` is proposed as an object containing `objects`, `connectors`, and `joins` arrays. An optional future format version may partition these arrays into region entries without changing their meaning. The arrays are the authored record, not a cached simulation graph.

| Record | Proposed required fields | Meaning |
| --- | --- | --- |
| Placed block | `id`, `kind: "block"`, `type`, `cell`, `orientation`, `appearance` | Stable authored ID, exact grid location and orientation, and saved block color/appearance choices. Generated unbreakable floor cells are not emitted as placed blocks; placed sandstone is. |
| Component | `id`, `kind: "component"`, `type`, `cell`, `orientation`, `configuration`, `tag` | Fixed component type and design settings, including initial conditions. First-playable `type` values need constants for Constant Logic Source, AND, and SR flip-flop. The source's configured initial On/Off and configured four-state value belong in `configuration`; its runtime toggle does not. |
| Module placement | `id`, `kind: "moduleInstance"`, `instanceId`, `moduleFamilyId`, `moduleVersionId`, `instanceName`, `cell`, `orientation`, `tag` | An authored placement ID and independent instance ID, exact pinned version, placement, and name. The instance's changing Q, events, nets, and clock phase are not stored here. |
| Connector | `id`, `kind`, `width`, `route`, `terminals`, `tag`, `identityColor` | Persistent physical identity, exact occupied path/channels, targetable terminal IDs, and saved presentation choices. `kind` is `wire` for the first playable; future versions can add harness and Net Link records under their own rules. `width` is 1 here. |
| Join | `id`, `members` | An explicit electrical junction or attachment. Members reference connector terminal IDs or a component/module pin; equal cell coordinates alone never create a join. A crossing without a join remains separate. |

`cell` is an integer `[x,y,z]` triple under the accepted [authored grid convention](docs/project-vision-and-world.md#world-data-and-rendering-architecture): x east, y up, z north, southwest generated-floor cell at `(0,0,0)` and floor layer `y=0`. The [accepted rotation and encoding rules](docs/physical-connections.md#post-placement-rotation) require 90-degree grid-aligned rotations, save support for all 24 proper component/module orientations, and `forward`/`up` face labels; first-playable controls need only vertical-axis turns. The exact `route` geometry and `terminals` placement encodings are **not yet resolved**; see [open schema choices](#open-schema-choices). The schema cannot be declared implementation-ready while those fields are placeholders. Every targetable connection part that exists independently must retain a persistent ID under the [identity rule](docs/modules-and-packaging.md#persistent-identity-model).

**Accepted orientation encoding:** Component and module orientation is saved as two perpendicular local directions expressed in world-face labels, for example `"orientation": {"forward": "north", "up": "up"}`. Each value is one of `north`, `south`, `east`, `west`, `up`, or `down`; parallel or opposed pairs are invalid. The pair identifies one of the 24 proper grid orientations without depending on a Unity runtime object ID or a numeric rotation table. See the [canonical rule](docs/physical-connections.md#post-placement-rotation).

`configuration` should be a typed record for each supported component rather than an uninterpreted Unity object serialization blob. For the first playable, the source needs its configured initial On/Off state and configured bit value; the AND gate needs its fixed one-bit identity; the SR flip-flop needs any explicit initial Q setting, if configured. An omitted explicit initial Q means X under the existing rule. The detailed field spellings and how an explicit initial Q is represented are still proposed choices. Save values as design settings only; do not accidentally serialize the currently observed Q.

### Connection references

A proposed `joins[].members[]` value is a tagged reference:

| `targetKind` | Fields | Resolution |
| --- | --- | --- |
| `connectorTerminal` | `connectorId`, `terminalId` | A terminal of a connector in the same owning design. |
| `componentPin` | `objectId`, `pinKey` | A named pin on a component in the same owning design, such as `A`, `B`, or `Y` for an AND gate. The pin key must exist for that component type and configuration. |
| `modulePort` | `objectId`, `portId`, `bitIndex` | A port bit on a placed module instance in the same owning design, resolved against its pinned module version. First-playable ports have width 1 and bit index 0. |

Connector continuity is recorded by `route`; joins record electrical attachments between physical paths and pins. A path crossing another route, sharing a cell, touching an unjoined pin, or sharing an ordinary tag cannot infer a join. The physical four-channel occupancy must be validated separately from net membership. If a break leaves two connector pieces, the original connector ID is retired and each surviving piece receives a new UUID under the [accepted split rule](docs/physical-connections.md#authored-topology-representation); every affected join and selection reference must be updated or the edit rejected atomically.

### Module-version port mapping

Each proposed `ports[]` record contains `portId`, `name`, `direction`, `width`, `bitOrder`, `exteriorPosition`, and `bitMappings`. `direction` has the accepted input/output/inout meanings. `exteriorPosition` must preserve face, block position on the module footprint, and quadrant. `bitMappings[]` pairs a `portBitIndex` with an explicit internal authored endpoint reference, never a transient derived-net index. First-playable port width is 1, but the same record shape should support the accepted 1–128-bit rule. Multiple exterior ports may map to the same internal net under the existing driver-resolution rules.

The exact endpoint-reference shape for a port bit is an open schema choice: it must identify the internal authored connection unambiguously even when multiple connectors make up one net, and it must survive save/reopen and exact-version embedding. It must not depend on a renderer mesh ID or a newly numbered derived net. Nested module placements inside a definition retain exact child-version IDs; `childVersionIds` in the module record and manifest is a declared dependency list checked against those placements, not a second circuit definition.

### World-only records

`worldSettings` should include the saved generated-floor/world-size settings and configured world-clock frequency. The first-playable default is 10 Hz and accepted range is 0.1–100 Hz; a running/stopped flag and current clock level are **not** saved as a live checkpoint. `player` saves position and view direction. `inventory` saves the hotbar, selected slot, and available module references as required by the existing save rules, without duplicating the module blueprint in every inventory slot. The exact numeric encoding for frequency and player orientation, and whether library-wide inventory references also live in a separate local profile, still need explicit schema choices.

The five-minute undo/redo history, per-instance NVM, and education-world lesson position are required by the broader canonical design but may follow the professor milestone. When implemented, versioned entries or fields must preserve their existing semantics. Their absence from a first-playable save is a documented milestone deferral, not evidence those requirements were removed. A format version that adds them must be migratable without silently changing the meaning of existing worlds.

## References and validation

Before using a loaded record, the loader should validate the archive and the complete referenced design:

1. Check ZIP entry names, duplicate paths, expected sizes, decompression limits, UTF-8/JSON validity, manifest version, listed entries, and SHA-256 digests. Do not accept path traversal or silently use an unlisted conflicting entry.
2. Parse UUIDs and compare their values. Check uniqueness in the correct scope, including authored IDs within one design, module-version IDs within the save, and instance IDs on placements. Do not merge two differently cased spellings of one UUID as distinct objects.
3. Check each world and module record against its declared identity; check every pinned module version and recursively required child version. Reject recursive containment under the accepted module rule. Do not substitute the newest library version for a missing exact version.
4. Validate object types/configuration, grid occupancy, connector channel capacity and path continuity, terminal placement, joins, pin/port existence and widths, module footprint/port positions, and every typed reference. Do not create a net from geometry or tags when a join is absent.
5. Build derived nets and renderer/selection data only after the authored data has passed the applicable validation or the accepted recovery path has produced a coherent placeholder representation. A damaged module uses the specified placeholder and relink behavior; a damaged world record leads to backup choice. Preserve damaged files for recovery.

These checks propose validation boundaries, not a new rule that every recoverable inconsistency must reject the entire world. The exact distinction between a recoverable missing/damaged module and invalid authored topology needs a concrete loader error policy before implementation.

## Save and reopen behavior

Take a consistent authored snapshot after the current simulation time slot completes. Write a new ZIP, verify its entries and references, then make it current under the existing backup rule. Do not expose a half-committed edit or package in the snapshot. On reopen, derive the graph from `world.json` plus exact embedded versions, create separate instance runtime states, set ordinary uninitialized SR Q values to X, restore configured sources, and start the world clock stopped at level 0 while retaining its configured frequency. The professor save/reopen case must reproduce the design but **not** the previously observed live Q=0 values.

## Open schema choices

These choices are **not resolved by this draft** and should be reviewed before making the schema canonical or writing its serializer:

1. **Footprint transforms:** The accepted [grid axes and origin](docs/project-vision-and-world.md#world-data-and-rendering-architecture), 90-degree grid-aligned rotation, save support for all 24 proper orientations, vertical-axis controls for the first playable, and `forward`/`up` encoding are settled. The precise rule that maps each module-local cell and port quadrant to a placed footprint remains to be written. No ordinary construction-block rotation rule was decided.
2. **Connector route/terminal encoding:** Exact per-cell path, face, quadrant, channel, tap, and targetable-part representation that covers elbows, branches, six-face paths, crossings, and a break that divides a connector. This must be defined with example records, not left as a generic `route` blob.
3. **Port-bit endpoint mapping:** Stable authored reference and behavior when a referenced internal connector is edited while preparing a *new* module version. Fixed old versions must never be modified in place.
4. **Component type and configuration registry:** Durable type keys, pin keys, typed settings, explicit initialization encoding, and compatibility handling for unknown future component types.
5. **World-only settings:** Exact generated-floor parameter fields, numeric frequency representation, player view representation, inventory/library reference split, and first format-version identifier.
6. **Error classification:** Precise rules for rejecting invalid world topology versus recovering a damaged/missing module with a placeholder, and how a placeholder retains pins and footprint when some definition metadata is damaged.
7. **Deferred persisted data:** Future undo/redo history, nested-instance NVM, education progress, and migration records must be designed before their affected features. This milestone's deferral should be explicit in its format version.

The next review should settle the coordinate/orientation and connector-route choices first, because they determine whether the proposed records truly retain the exact spatial circuit required for packaging and re-expansion. Do not treat this file as implementation authority until the user accepts a complete schema and the corresponding canonical files are updated.
