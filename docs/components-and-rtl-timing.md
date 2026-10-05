# 6 Components and RTL timing

## In this file

- [Primary timing model](#primary-timing-model)
- [Decoded seven segment display](#decoded-seven-segment-display)
- [Default up counter](#default-up-counter)
- [Clock divider](#clock-divider)
- [Multiplexer](#multiplexer)
- [SR storage](#sr-storage)
- [Pulse source](#pulse-source)
- [Sources on load and reset](#sources-on-load-and-reset)
- [Original catalog retained alongside clarified components](#original-catalog-retained-alongside-clarified-components)
- [Construction blocks](#construction-blocks)

## Primary timing model

The core is functional RTL simulation. Gates have zero modeled physical propagation delay; wires, harnesses, and Net Links have zero modeled physical interconnect delay; there is no clock-to-Q or setup/hold timing model. A gate type or individual gate instance cannot set a physical delay in the core. Event ordering, nonblocking updates, delta cycles, derived clocks, and convergence handling still apply, so “zero physical delay” does not mean ignoring event order. Circuit structure can be compared using gate count, registers, memory, fanout, logic depth, and clock cycles per result, but the game cannot infer a real implementation’s maximum clock frequency without a technology and layout model. Engine runtime is simulator performance, not the circuit’s physical timing.

A future implementation-timing mode may use technology-specific cell and interconnect delays, rise/fall differences, clock-to-Q, setup/hold checks, glitch and short-pulse behavior, annotated timing data, and critical-path/slack views. It should be separate from the core RTL rules and based on a chosen target technology/layout. The earlier dual-profile timed-mode proposal is not a current requirement.

## Decoded seven segment display

One component supports a configurable one through ten digits. Each digit receives its own four-bit nibble; there is no separate scan/address input. D0 controls the rightmost digit; larger indices run leftward. A fully known nibble displays the hexadecimal character 0 through F. A disconnected nibble reads ZZZZ. An input containing X or Z must not display a falsely definite numeral. The confirmed visual behavior is that all seven segments pulse red if any input bit is X; otherwise, all seven segments show steady gray if any bit is Z. X has priority when both occur. Inspection shows the exact four-bit value. A raw seven-segment device with separate segment, decimal-point, and digit-enable pins is an advanced/future component, and education may require the learner to build a decoder before unlocking the convenience display.

## Default up counter

The default counter width is configurable from 1 through 128 bits. It increments by one on the rising edge of its input clock when optional Enable permits. Active-high Reset is synchronous and clears count and overflow to zero. At maximum value, the next enabled edge wraps count to zero and raises Overflow for one complete counter-clock period. Overflow clears at the following rising edge even if Enable is then low. Sticky overflow and asynchronous-reset variants may be separate future components; they are not the default.

## Clock divider

N is a configuration setting, not a live signal input. It is a positive integer divisor. N=1 passes the input clock through; for larger N the output frequency is input frequency divided by N. Even N gives 50 percent duty cycle; odd N uses the nearest possible duty cycle, with the low interval one input-clock period longer than the high interval. Reset sets output to zero and restarts the internal count. Editing N auto-pauses and restarts count and phase. A world-clock frequency change does not reset a player-created divider driven by it: the divider retains its internal count and output phase and responds normally to future input edges. A separate future programmable divider could expose N as an input signal and specify its live-update behavior. This accepted direct-divisor definition supersedes the older design-document idea of N as an exponent in 2^N; the merger must not retain both as the same component.

## Multiplexer

Input count N retains the original range of 2–32, with inputs I0 through I(N-1) and output Y; all data inputs and output have the same width. Select width is ceil(log2(number of inputs)). A fully known valid select chooses that input. A fully known code with no input, such as 11 on a three-input MUX, yields X on every output bit. If select contains X or Z, consider every possible selected input and merge each output bit: a bit remains known only if all possible choices agree; otherwise it is X. A possible invalid index contributes X. This is the defined SiliconSandbox MUX policy, not a blanket assertion that every SystemVerilog case or indexed-array formulation behaves this way under every VCS option.

## SR storage

There are separate level-sensitive SR latch and edge-sampled SR flip-flop components. Inputs S and R are active high; both components expose Q and Q_bar. S,R=00 holds, 01 resets Q to zero, 10 sets Q to one, and 11 makes Q unknown X. The default SR flip-flop samples this table on rising CLK; the latch responds to S/R levels. A later definite set or reset can recover from X; hold retains an existing X. Q_bar is also X in the invalid condition. This is a digital uncertainty policy, not an analog metastability model.

**SR flip-flop uncertain-control decision, 30 September 2026:** At a rising CLK edge, if S or R is X or Z, evaluate the four known-input SR rows for every 0/1 interpretation permitted by those uncertain controls, using the flip-flop's prior Q for a hold row. If every possible resulting Q is identical as a four-state value, use that value; otherwise set Q to X. Z on an S/R input is treated as an uncertain control for this evaluation while remaining Z on its input net for inspection. The 11 row contributes X to the set of possible results. For example, with R=0 and S=X, prior Q=1 yields Q=1 (hold or set both give 1), while prior Q=0 yields Q=X (hold gives 0, set gives 1). With S=0 and R=X, prior Q=0 yields Q=0, while prior Q=1 yields Q=X. A known 00 input continues to hold the prior Q, including X or Z if that was the stored value; a known 10 or 01 recovers to 1 or 0. Q_bar is the four-state logical complement of the resulting Q, so Q=X or Z gives Q_bar=X. This is an explicit SiliconSandbox component policy, not a claim that every IEEE-compliant SystemVerilog description of an SR flip-flop behaves identically. The [research comparison](../sr-flip-flop-uncertain-controls-research.md) gives example HDL models; CLK edge detection is specified immediately below.

The SR flip-flop uses the shared [four-state positive-edge detection rule](circuit-time-and-clock.md#four-state-positive-edge-detection). On each detected positive edge, sample S/R and apply the SR next-state rule above after the required event-ordering steps. The professor demonstration's world clock uses clean 0→1 edges.

## Pulse source

Idle output is zero. Clicking schedules a pulse to start at the next rising edge of the world clock; clicking while paused arms it for resume. Pulse length is a configurable positive whole number of world-clock cycles, default one. It returns low on the appropriate later rising edge. Repeated clicks while a pulse is pending or active queue at most one additional pulse, with at least one full low cycle between pulses. Reset clears active and pending pulses. Idle, armed, and active states are visible. An asynchronous pushbutton can be a separate future source.

## Sources on load and reset

A Constant Logic Source has a configurable width from 1 through 128 bits, with a default width of one bit. It stores a configured four-state scalar or vector value; every bit may independently be 0, 1, X, or Z, and the default configured value is one on every bit. The source also stores a configured initial On/Off state, which defaults to **Off for a newly placed source** (decision, 30 September 2026). Loading the world or using Reset Simulation restores both that initial state and the configured value. While On, the source drives its configured value. While Off, it drives zero on every bit; a player who wants the source to release a net may configure its On value as Z. Right-clicking the source body toggles only its current runtime On/Off state, even if a placeable item is selected; targeting its separate free pin with a connector selected instead places the connector. This follows the [first-playable interaction precedence](building-and-interface.md#default-controls) and does not rewrite configured initial state or value or create a live checkpoint. A separate switch/toggle source defaults to initial zero unless configured otherwise. Pulse output resets to idle zero and loses pending activation while keeping its configured duration. World clock resets to level zero, restarts phase, and remains stopped while keeping configured frequency; P starts it again. Explicit initial conditions restore on load and Reset Simulation; otherwise stored four-state values use the X rule.

**First-playable source Configure decision, 5 October 2026:** Changing a placed source's configured On value or initial On/Off state at a safe edit boundary preserves its current runtime On/Off choice. A changed On value immediately changes the live drive if the source is currently On; changing only initial On/Off takes effect on the next load or Reset Simulation. The edit saves the new configuration but never saves the current transient On/Off choice.

## Original catalog retained alongside clarified components

The catalog below preserves components and parameter ranges not replaced by later decisions. Basic gates use matching-bit four-state operations at the configurable width in [Section 4](harnesses-and-net-links.md). The defined SR invalid-state rule above overrides a literal NOR-latch implementation’s forbidden-state outputs. Unknown-control truth tables not explicitly settled by the chat remain open; do not invent a two-state fallback.

| Gate | Defined operation |
| --- | --- |
| BUFFER | Single-input, single-output directional signal pass-through. Output equals Input (Y = A). Used to enforce directional signal flow or isolate sub-circuits. |
| NOT | Single-input inverter. Output is the logical negation of the input (Y = ~A). |
| AND | 2-input logic gate. Output is HIGH (1) if and only if both inputs are HIGH (Y = A & B). |
| NAND | 2-input logic gate. Output is LOW (0) if and only if both inputs are HIGH (Y = ~(A & B)). Universal logic gate. |
| OR | 2-input logic gate. Output is HIGH (1) if at least one input is HIGH (Y = A \| B). |
| NOR | 2-input logic gate. Output is HIGH (1) if and only if both inputs are LOW (Y = ~(A \| B)). Universal logic gate. |
| XOR | 2-input exclusive OR gate. Output is HIGH (1) if exactly one input is HIGH (Y = A ^ B). |
| XNOR | 2-input exclusive NOR gate. Output is HIGH (1) if both inputs are identical (Y = ~(A ^ B)). |

| Component | Retained definition |
| --- | --- |
| D latch | Inputs D and E; outputs Q and Q_bar. E=1 passes D transparently; E=0 retains the stored value. |
| JK flip flop | Rising CLK; inputs J and K; outputs Q and Q_bar. J,K=00 holds; 10 sets; 01 resets; 11 toggles. |
| D flip flop | Inputs D and CLK; outputs Q and Q_bar. Capture D on rising CLK and hold between edges. |
| T flip flop | Inputs T and CLK; outputs Q and Q_bar. Rising CLK toggles Q when T=1 and holds when T=0. |
| Binary decoder | N-bit select, N=1–5, and Enable. There are 2^N active-high output lines; known enabled selection asserts its corresponding line. |
| Binary priority encoder | N=1–5 output address bits; 2^N active-high input lines. Output the highest-index active input address, plus Valid indicating whether any input is active. |
| Single bit full adder | A, B, Cin inputs; S=A ^ B ^ Cin; Cout=(A & B) \| (Cin & (A ^ B)). |
| Multiple bit adder | Unsigned ripple-carry adder; N=1–32. N-bit A and B plus Cin produce N-bit sum S and Cout; S derives from A+B+Cin. |
| Clock Source | Former Square Wave Voltage Source. Continuous 0/1 square wave at configurable frequency in Hz. Multiple independent sources, phase controls, frequency range, and relation to world-clock stop remain open. |
| Red Blue and Yellow LEDs | Three original indicator types: illuminate in their named bright color at 1 and remain dim/off at 0. Their X/Z visual treatment and relationship to general signal colors need definition. |
| Original up down counter | Original catalog: four-bit counter with CLK, RESET, UP_DOWN (1 up, 0 down), ENABLE, four-bit Q and OVERFLOW/UNDERFLOW. The later confirmed default is the 1–128-bit up counter above. Preserve the original up/down capability as an unresolved catalog variant; no downward-wrap/underflow behavior or decision to remove it was made. |

## Construction blocks

Sandstone is the ground and a reusable inventory block. Its appearance is inspired by smooth sandstone in Minecraft. The original intent was a distinct texture; changing an existing texture slightly is not treated here as an established permission to reuse it. Scaffolding is a uniform solid-color building block in the following sixteen colors.

| Color | Hex | Color | Hex |
| --- | --- | --- | --- |
| White | #cfd5d6 | Lime | #5ea918 |
| Light Gray | #7d7d73 | Green | #495b24 |
| Gray | #373a3e | Cyan | #157788 |
| Black | #080a0f | Light Blue | #2489c7 |
| Brown | #603c20 | Blue | #2d2f8f |
| Red | #8e2121 | Purple | #64209c |
| Orange | #e06101 | Magenta | #a9309f |
| Yellow | #f1af15 | Pink | #d5658f |
