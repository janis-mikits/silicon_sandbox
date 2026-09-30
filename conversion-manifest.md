# SiliconSandbox DOCX conversion manifest

## Source and method

- Authoritative migration source: [SiliconSandbox_Design_Document_Revised.docx](SiliconSandbox_Design_Document_Revised.docx), SHA-256 `7189b9f132ec2e59786e95319d1086d24002b7dcf1d05101189b9015a97cce9f`.
- The source was inventoried before writing the knowledge base: a preface, “Project vision and world,” numbered Sections 1–12, Appendices A–D, five tables, and one embedded main-menu illustration.
- Source body ranges below are zero-based positions among top-level DOCX body elements, including blank elements and tables. They give a stable migration locator within this exact source file; they are not page or paragraph numbers.
- Each source major section has one canonical Markdown destination. The source's hierarchy and paragraph order remain within that destination. Single-section references became Markdown links where unambiguous. The original document map remains in the project-vision file, while `INDEX.md` adds task routing.
- The DOCX remains available for audit. The Markdown in `docs/` is the working canonical specification produced by this migration.

## Destination map and source-section inventory

| Source portion and body range | Markdown destination | Topics accounted for |
| --- | --- | --- |
| Preface, 0–24; Project vision and world, 25–41 | [docs/project-vision-and-world.md](docs/project-vision-and-world.md) | Title/date/status convention, original document map, purpose and unverified rationale/comparison claims, modes/platform scope, definitions, configurability, finite world, floor and bounding wall. |
| Section 1, 42–55 | [docs/digital-simulation.md](docs/digital-simulation.md) | IEEE/VCS/Verdi reference and vendor-protection rule; four-state resolution; colors; no analog modeling; verification references and unresolved conformance work. |
| Section 2, 56–67 | [docs/circuit-time-and-clock.md](docs/circuit-time-and-clock.md) | World and derived clocks, event/delta ordering, simulation/playback/render distinction, safe pause and edits, live frequency changes, nonconvergent feedback, controls, offscreen and fast transitions. |
| Section 3, 68–83 | [docs/physical-connections.md](docs/physical-connections.md) | Connector/channel terms, six faces, pin targeting, intentional attachment, junctions/crossings, topology markers, crowded selection/invalid feedback, post-placement rotation, geometry. |
| Section 4, 84–98 | [docs/harnesses-and-net-links.md](docs/harnesses-and-net-links.md) | Packed vectors, direct junction versus Shift-concatenation, universal bit order, split and width adapter, Net Link identity/scope, wide gates and education. |
| Section 5, 99–123 | [docs/modules-and-packaging.md](docs/modules-and-packaging.md) | Definition versus instance, selection/ports, captured design and excluded runtime state, footprint, versions/upgrades, library and world recovery, physical re-expansion, package UI/layout examples, optimization limits. |
| Section 6, 124–151 | [docs/components-and-rtl-timing.md](docs/components-and-rtl-timing.md) | Functional RTL/zero physical delay, seven-segment display, up counter, divider, MUX, SR storage, pulse and constant sources, original gate/component catalogs, sandstone and exact 16-color palette. Both source tables in the catalog and the palette table remain here. |
| Section 7, 152–174 | [docs/building-and-interface.md](docs/building-and-interface.md) | Placement/movement, undo/redo, group operations, inventory/Configure, invalid placement, player/tags/colors, menus, complete original control table, and original menu illustration. |
| Section 8, 175–181 | [docs/inspection-and-diagnostics.md](docs/inspection-and-diagnostics.md) | Hover/detailed Inspect, faults and known causes, performance HUD, animation limits, later waveform reference. |
| Section 9, 182–192 | [docs/saving-and-recovery.md](docs/saving-and-recovery.md) | Saved design versus transient state, NVM exception, source startup state, snapshots/autosaves/recovery, world duplication/deletion, file migration and player profile. |
| Section 10, 193–198 | [docs/performance-and-platforms.md](docs/performance-and-platforms.md) | Windows/macOS, proposed test-machine procedure, 60 FPS/1080p/1,000 equivalent-gate benchmark, measurement fields, provisional response targets and open benchmark details. |
| Section 11, 199–202 | [docs/education-and-onboarding.md](docs/education-and-onboarding.md) | Freeplay access, later Education progression, lesson specification/validation, shared unlock profile, first-public-release introduction. |
| Section 12, 203–217 | [docs/delivery-and-acceptance.md](docs/delivery-and-acceptance.md) | Professor milestone, public release, later additions, five-step demo path, proposed broader verification checklist. |
| Appendix A, 218–226 | [docs/graphics-and-future-tools.md](docs/graphics-and-future-tools.md) | Graphic priorities, rendering behavior, later EDA/timing, optional routing, multiplayer/VR and other future work. |
| Appendix B, 227–254 | [docs/open-decisions-and-cautions.md](docs/open-decisions-and-cautions.md) | All original open-design bullets, technical interpretation cautions, and additional unresolved merge choices. |
| Appendix C, 255–302 | [docs/original-research-backlog.md](docs/original-research-backlog.md) | Archived source questions: engine, HDL backend, module storage, world/chunks, textures, rendering/meshes, Minecraft mods, LOD/octrees, links and attributed quotations. Many have since been researched or decided. [The current backlog](docs/research-backlog.md) lists only unanswered follow-ups; neither file is an operative answer to a design question. |
| Appendix D, 303–335 | [docs/revision-history.md](docs/revision-history.md) | Review method, material corrections, changes from earlier design, all 15 reversals, original-topic coverage table, development context, source-file references. |

## Other files created

| File | Migration role |
| --- | --- |
| [README.md](README.md) | Future-agent operating instructions and source-of-truth hierarchy requested for the knowledge base. |
| [INDEX.md](INDEX.md) | Task-first file guide, topic routing, dependencies, and source section map. |
| [conversion-manifest.md](conversion-manifest.md) | This migration record, including section destinations and verification. |
| [docs/assets/main-menu-reference.png](docs/assets/main-menu-reference.png) | Byte-identical extraction of the DOCX's one embedded illustration; linked at its source position in Section 7. This is an image asset, not Markdown. |

**Post-migration maintenance:** [docs/research-backlog.md](docs/research-backlog.md) was created after the migration to hold only still-actionable research. It does not replace Appendix C as the destination for the original DOCX text; [docs/original-research-backlog.md](docs/original-research-backlog.md) remains the source archive. Later canonical decisions and research records are routed by [INDEX.md](INDEX.md).

## Reorganization and interpretation boundaries

The preface was placed ahead of Project vision and world in the same file. The original menu image was extracted from the DOCX package into `docs/assets/`. Source tables became Markdown tables; the source's bullet character became Markdown list syntax. Section and appendix citations with a singular number/letter became local links. No game rule was added to settle an open point. The source's older research questions and revision log remain in full, separated from operative subsystem files to keep routine context loading small. Original status words such as “Proposed,” “Open,” and “Future” remain attached to their content.

## Ambiguities, tensions, and uncategorized content

- All content had a confident destination; no source body content or image was left uncategorized.
- At conversion, the source identified unresolved matters in [Appendix B](docs/open-decisions-and-cautions.md), including backend/engine selection and supported IEEE subset; clock units/frequency/phase; split mapping and connector-edit details; source/placement and Shift/scroll interaction precedence; module-family and re-expansion details; component unknown-control cases and the original up/down-counter variant; undo timestamps; identity colors versus logic-state colors; benchmark hardware/procedure; and education curriculum. The engine and initial backend were later decided; consult the [current research backlog](docs/research-backlog.md), [open decisions](docs/open-decisions-and-cautions.md), and operative files for present status.
- The original research backlog contains older premises that conflict with settled rules, especially a world-clock-driven update model, rendered-chunk dependency for simulation, and possible physical delay. The source explicitly labels these as superseded research premises. They were preserved as historical questions, not adopted as parallel requirements.
- Appendix D records historical reversals and corrected interpretations (including direct divisor `N` rather than `2^N`, deliberate connector attachment, and restored physical re-expansion/rotation). The operative subsystem files retain the final decisions. No new contradiction was identified from the file split itself.
- The original illustrated menu is a visual reference with map-themed objects to replace; its visible labels are not treated as an independent functional specification beyond the source's menu text.

## Completeness and final checks

- A second DOCX-to-destination pass checked **325 nonempty paragraphs**, including headings and source list text, and **154 cells across all five tables** against their assigned Markdown files. All matched after accounting for Markdown link and list/table syntax.
- The sole embedded image matches `docs/assets/main-menu-reference.png` byte for byte.
- Thus all **331 nonempty top-level body items** (325 paragraphs, five tables, one image) are accounted for. Blank layout paragraphs and the page footer were not treated as substantive specification.
- Paragraph-level matching includes source numerical values, constraints, exceptions, edge cases, warnings, definitions, implementation instructions, unresolved items, and the complete research and revision appendices. This is stronger than a section-heading-only check.
- Local Markdown file/anchor links, index routing, and the README rules were checked after generation. The source-section table above and `INDEX.md` route plural section references that remain as source wording.

**Migration conclusion:** Every substantive portion of the revised DOCX is represented in the Markdown collection to the best of this conversion audit. No information was intentionally omitted or redesigned.
