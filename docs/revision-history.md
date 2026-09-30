# Appendix D Review findings and change log

## In this file

- [Material record corrections](#material-record-corrections)
- [Important changes from the polished design](#important-changes-from-the-polished-design)
- [Reversal checklist](#reversal-checklist)
- [Coverage of the original document](#coverage-of-the-original-document)
- [Development context](#development-context)

This revision reconciles the original polished document, both portions of the design conversation, the original twelve-area outline, and the supplied decision record. The decision record is evidence to check, not authority over the user’s later choices. “I agree with everything” applies to the referenced response and its choices; moving to another section alone is not treated as approval of a new recommendation.

## Material record corrections

- Backend commitment was overstated during the original revision. Record [Section 1](digital-simulation.md) said the game “will implement its documented subset itself.” In the VCS recommendation, the assistant said it “can implement” the rules internally; the original research asked whether to use an external HDL simulator or native engine. The revised document initially left the backend open while retaining the IEEE/VCS behavioral target; the later [runtime architecture decision](digital-simulation.md#runtime-architecture-decision) selected a purpose-built C# simulator.
- Benchmark procedure lacked explicit acceptance. After the user selected “The average PC that a user on Steam has” and Windows/macOS, the assistant proposed frozen Windows/Mac machines per milestone. The next user reply moved to [Section 11](education-and-onboarding.md) and set education priority. Record [Section 10](performance-and-platforms.md) presents that procedure as settled. It is now proposed; the accepted performance numbers and platforms remain requirements, including Windows/macOS for the first playable target.
- Module-family wording was too restrictive. The user prohibited migration to a version “if the version has incompatible ports.” The accepted follow-up said a changed design “can still be packaged as a separate module” but cannot be an “in-place-compatible version.” That settles safe migration, not definitively whether incompatible versions may share a family for new placement. The revised document preserves the upgrade prohibition and marks the family policy open.
- Accepted details needed more explicit wording. [Section 1](digital-simulation.md)’s accepted resolution rules include “an active driver is unknown → X.” [Section 2](circuit-time-and-clock.md) requires every electrical transition to be processed even when it happens too quickly to animate, while retaining only the settled value and limited error diagnostics in the core game. Complete bounded waveform history is a future feature.
- Constant-source status needed finer separation. In [Section 6](components-and-rtl-timing.md) the user accepted restoring a configured source value and added right-click On/Off. The next assistant supplied Off=0, default On=1, multibit examples, and the all-segments X/Z display; the user then said “move to section 7.” The reconciliation later confirmed the seven-segment X/Z appearance and separately confirmed the Constant Logic Source rule: width 1 through 128, On drives the configured four-state value, Off drives all zeroes, the default configured value is all ones, load and reset restore the configured initial state and value, and runtime right-click toggles do not rewrite configuration.
## Important changes from the polished design

Integrated four-state IEEE 1800-2023 behavior with VCS comparison; event-driven timing and safe pause; explicit connector targeting; Net Links; distinct junction and concatenation actions; module definitions, instances, versions, embedded recovery and restored expansion; complete component clarifications; building controls and saved undo; inspection; persistent NVM and safe saves; measurable performance; education priorities; and three delivery scopes. Retained the component catalog, all dimensions and palette values, world presentation, controls, menu image, graphics priorities, and research notes. Corrected the module-size formula.

Retained the original up/down counter concept as an unresolved variant beside the later default up counter, and identified custom wire colors versus state colors as a visual decision still to make. Kept the original rationale and comparison claims as unverified context, not new factual findings. No research-backlog question was investigated or answered **during the original DOCX-to-Markdown conversion**; later research and decisions are recorded in the operative files and [current research backlog](research-backlog.md).

## Reversal checklist

1. Simulator reference: Purdue’s QuestaSim was the first reference. The final comparison target is IEEE 1800-2023 behavior with VCS as primary commercial simulator and Verdi as the confirmed reference for a future waveform/debugging interface. Do not claim a universal NVIDIA-only tool choice.
2. Clock model: The early “world clock controls all updates” idea became an event-driven simulator with the world clock as the default synchronous signal. Combinational updates can happen between world-clock edges.
3. Touching objects: A suggestion that compatible ports on touching faces automatically connect was rejected. Only deliberately placed connectors attach to pins or join other connectors.
4. Virtual wires: The provisional name became Net Link. Net Links do occupy one of the physical channels; matching names supply the non-contiguous electrical connection.
5. Junction visibility: An early “every joined connector has a point” reading was refined. One or two joined directions need only continuous/straight/elbow geometry; three to six need a junction marker. A concatenation point always has its own visible marker.
6. Harness width and combining: The fixed-width-no-concatenation idea was replaced by per-object fixed width with new harness/tag on explicit concatenation or split. A normal direct junction and a Shift-concatenation are different operations. Splitter and Combiner are connection points, not block-sized objects; Width Adapter remains a distinct conversion component.
7. Concatenation order: Larger-width-on-MSB, wire-on-LSB-by-type, and random choice for equal-width harnesses were all replaced. The targeted existing connector/group is always MSB; the newly added one is LSB.
8. Module update: Editing a source circuit never changes its packaged module automatically. A new version requires new packaging, and placed instances are pinned until deliberate compatible upgrade.
9. Module re-expansion: Re-expansion was first considered, then removed as redundant to a live viewer, then restored because the editable source may be destroyed. Final expansion places a fresh editable design copy at a bottom-right target anchor; it never replaces a live instance or copies its current state.
10. Physical rotation: [Section 7](building-and-interface.md) briefly proposed breaking and replacing instead of post-placement rotation. The final decision restores safe post-placement rotation with paused preview, confirmation, highlighting of lost connections, fixed connector geometry, and footprint fit check.
11. Timing: A functional/timed profile proposal was narrowed to a zero-physical-delay RTL core. Cell and interconnect timing are possible future mode content, not a current simulator feature.
12. Clock divider parameter: The original document’s 2^N formulation conflicts with the later accepted direct divisor N≥1; use the later direct-divisor rule for this component, or explicitly create a different component if both are ever desired.
13. Section labels: An assistant temporarily called saving “[Section 7](building-and-interface.md).” The original twelve-area outline defines [Section 7](building-and-interface.md) as building/editing and [Section 9](saving-and-recovery.md) as saving/persistence. Accepted save decisions belong under [Section 9](saving-and-recovery.md).
14. Offscreen work: An original research note suggested visible/rendered populated chunks were needed for active circuits. The required final outcome is active simulation independent of rendering; offscreen geometry may be culled.
15. First-demo scope: The full public core is not all due for the professor demonstration. The initial demo is the five-step path in [Section 12](delivery-and-acceptance.md); regular-game onboarding is for first public release.
## Coverage of the original document

| Original topic | Location in this revision |
| --- | --- |
| Project vision and scope | Project vision and world; Sections 11–12; [Appendix A](graphics-and-future-tools.md) |
| World and persistence | Project vision and world; [Section 9](saving-and-recovery.md) |
| Player interaction and interface | Sections 3–4 and 7–8; original image in [Section 7](building-and-interface.md) |
| Blocks and components | Sections 3–4 and 6; original gates and palette retained |
| Default modules | Sections 5–6; counter and divider changes explicit |
| Circuit simulation | Sections 1–2, 6, 8 and 10; IEEE 1012 retained |
| Module creation and packaging | [Section 5](modules-and-packaging.md) including selection, configuration, boundary tests, sizes and reuse |
| Graphics and rendering | [Appendix A](graphics-and-future-tools.md); Sections 3, 7–8 and 10 |
| Future features | Sections 6, 11–12 and [Appendix A](graphics-and-future-tools.md) |
| Research backlog | [Appendix C](original-research-backlog.md); every original research paragraph and continuation retained |

All twelve specification areas have their own numbered section. The record’s fifteen reversal entries are reconciled above. Its open-topic groups and technical interpretation cautions are retained in [Appendix B](open-decisions-and-cautions.md), with additional merge conflicts made explicit. Every original body heading is mapped to its subject in this revision; every original RESEARCH marker and its surrounding backlog text is retained unchanged.

## Development context

The designer is new to AI-assisted game development and wants AI to perform as much work as practical under these specifications. The stated subscriptions were ChatGPT Plus, Gemini Pro, and Claude Free; Claude Pro was only a possible purchase if substantially useful. These are project context, not game requirements. Advice about AI products should use current information, explain unfamiliar concepts before requesting decisions, and distinguish confirmed decisions from proposals. The conversation used one numbered specification area at a time, with whitespace separating topics. Future section-by-section updates may keep the specification current; a final consistency audit is still needed after reversals. Model-switching and token-efficiency advice does not change game behavior or guarantee completeness.

Reference source files: SiliconSandbox_Design_Document_Polished.docx; SiliconSandbox_Conversation_Decision_Record.docx; the SiliconSandbox chat and pasted twelve-area outline. This revised copy does not overwrite either source.
