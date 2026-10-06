# 8 Inspection and diagnostics

## In this file

- [Quick look and detailed inspection](#quick-look-and-detailed-inspection)
- [Common faults](#common-faults)
- [Speed and animation](#speed-and-animation)

## Quick look and detailed inspection

Looking at a connector or pin highlights the selected connection and shows its tag, width, and present value without opening a panel. Harness quick inspection can lead to its packed-vector view so individual X/Z bits and bit order are visible. The remappable I key opens detailed Inspect for a targeted connector, pin, or component: exact four-state values and bit indices, width, connected net, connected drivers where known, and the known reason for an error. The simple hover feedback remains available in the normal world view.

**Professor walkthrough presentation correction, 6 October 2026:** In the first playable, detailed Inspect for nets, components, and module internals opens a centered, scrollable, high-contrast panel. At a 1080p display its text should be roughly four times the provisional small text, with a much larger panel and a dark background around 85% opaque. Scale responsively at smaller resolutions without hiding content or allowing edits behind the panel. This changes presentation only; values and explanations still come from the selected live instance or net.

## Common faults

An undriven net or disconnected input appears gray Z; inspection says “No active driver.” Opposed drivers make only affected bits X and pulse red. Inspection lists known driving pins and values and identifies affected bus bits; it must not claim every X is a driver conflict. A physical width mismatch triggers only the agreed single red flash and quiet click; it never auto-generates a text popup, including on repeat. The player can inspect widths. Invalid Configure fields get inline red explanation and cannot be confirmed. A circuit that exceeds the convergence limit follows [Section 2](circuit-time-and-clock.md) and produces a diagnostic entry pointing to the suspected nonsettling area. These are necessary basic aids before a full EDA interface exists.

## Speed and animation

A small HUD indication appears when actual simulation speed falls below requested playback. F3 opens more detailed performance data showing requested simulation speed, achieved simulation speed, and rendered frames per second separately. Ordinary world colors show the latest settled simulated value. The red X pulse is a state marker, not a circuit event or clock. A traveling glow along a wire would imply physical propagation time that the RTL core does not model; any future teaching animation of update order must be clearly labeled illustrative. A full waveform viewer and Verdi-inspired advanced debugging remain later features; Verdi is the confirmed design reference for that future interface.
