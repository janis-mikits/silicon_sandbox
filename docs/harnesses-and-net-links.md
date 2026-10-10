# 4 Harnesses and Net Links

## In this file

- [Harness representation](#harness-representation)
- [Direct junction versus concatenation](#direct-junction-versus-concatenation)
- [Universal bit order](#universal-bit-order)
- [Split and conversion](#split-and-conversion)
- [Net Link identity and scope](#net-link-identity-and-scope)
- [Wide gates and education](#wide-gates-and-education)

## Harness representation

A wire carries one bit; a harness carries a packed vector of 1 through 128 bits in one physical channel and does not change its visible diameter with width. Bit indices are [width-1:0]; bit zero is least significant. Every bit has its own 0/1/X/Z value and its own driver resolution. An individual harness is fixed-width. The requested “dynamic width” experience comes from creating a new harness and tag when combining or splitting, not mutating an existing connector’s width in place. The player can inspect the complete packed vector and reorder individual bits or whole recently concatenated groups; grouping metadata is for the editor and does not create electrical coupling.

## Direct junction versus concatenation

Normal right-click placement onto an existing connector performs a junction. Two wires become one one-bit net. Two equal-width harnesses connect bit i to bit i and remain width N. A direct wire-to-harness or unequal-width harness connection is invalid, as is a direct connector-to-pin width mismatch. This construction rule is deliberately stricter than some SystemVerilog assignment contexts and must be described as a game rule.

Hold Shift while right-clicking with a connector selected and another connector targeted to concatenate instead. In every other placement context, Shift + right-click follows the [force-placement rule](building-and-interface.md#default-controls). This is a momentary alternate action, not a persistent mode; controls are remappable and a controller needs an alternate-action modifier. Concatenation creates a visible combination point, a new wider harness, and a new tag. Its width is the sum of participating widths; a result over 128 is invalid. It does not short its inputs together: each original bit net is mapped to one bit position of the result. This mapping does not impose signal-flow direction; direction is determined by the connected drivers and pins. A target wire W plus a new wire V makes a 2-bit harness {W,V} rather than a one-bit junction. The exact accepted electrical principle is:

> A combination or split point maps constituent bits into a packed-vector order but does not impose a signal-flow direction. Each mapped bit remains the same electrical net on both sides of the connection point. Direction comes from the connected component pins and drivers.

## Universal bit order

For every concatenation, the existing targeted connector or harness group occupies the MSB side; the newly placed or subsequently selected connector occupies the LSB side, independent of width or type. Thus target A[3:0], add B[7:0] yields {A,B}; reverse the target and the result is {B,A}. Adding C later to targeted {A,B} yields {A,B,C}. A normal junction preview highlights the paths that will become continuous and, for harnesses, shows bit-for-bit correspondence without MSB/LSB labels. A concatenation preview instead shows both groups, MSB and LSB roles, resulting width, and its distinct combination shape. The player may reorder groups or bits afterward. This rule supersedes the earlier larger-width-first and equal-width-random proposals.

## Split and conversion

A configurable split point can map a harness bit to a wire, a range such as [7:4] to another harness, or selected ranges to several connectors. Combination and split points are connection points, not separate block-sized Harness Splitter or Combiner objects. They expose bit mapping for inspection/editing and use the safe structural-edit pause. Direct width changes never occur silently. A separate Width Adapter component explicitly performs zero-extension, sign-extension, or chosen truncation. Zero-extension adds 0 high bits; sign-extension repeats the sign bit into added high bits; truncation discards selected bits. Exact UI for initiating a split, some overlapping-range rules, and adapter handling of all unknown-value corner cases still need a focused spec.

## Net Link identity and scope

Net Links can be 1 through 128 bits wide. Matching link name, width, and scope joins noncontiguous stubs as corresponding bit nets. A duplicate name in the same scope with a conflicting width is rejected. Link names and ordinary wire tags are case-sensitive, but ordinary tags alone do not create wireless connectivity. The link configuration panel autocompletes existing names and lets the player select one. Top-level Net Links have world scope. A Net Link packaged inside a module is private to each placed instance; matching internal names in separate instances cannot cross-connect except through ports.

## Wide gates and education

Basic gate width is configurable from 1 through 128, defaults to one, and all of a gate’s ports share the configured width. The operation is bitwise at matching bit indices; wide connections use equal-width harnesses or Net Links. Freeplay can offer these immediately. Education mode initially offers only one-bit primitives; learners construct multi-bit circuits and modules themselves before unlocking optimized width-configurable versions.
