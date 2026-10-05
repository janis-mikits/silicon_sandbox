# Slice 4 SR core progress — 5 October 2026

This is intermediate core work, not slice 4 acceptance. The world-time event
queue, world-clock connector, scene controls, and full professor journey are
not implemented here.

`OneBitSrFlipFlop` applies the canonical four-state positive-edge table and
the accepted S/R next-state rule. It preserves uninitialized X, explicit Z on
hold, and authored initialization on Reset; Q_bar is X when Q is X or Z. The
uncertain-control rule evaluates all permitted known S/R rows and takes their
common result only when every row agrees. NUnit tests use a literal 4×4 edge
expectation table, known S/R rows, representative uncertain controls, and
reset behavior. The expected table is independent of the implementation.

The graph execution plan now includes versioned SR pin bindings. The simulator
settles sources and combinational gates before sampling each SR from the same
pre-update state, commits storage updates together, and propagates Q/Q_bar
afterward. It preserves independent SR state across graph replacement by
component identity. Initialization and Reset Simulation establish the settled
CLK baseline without inventing an edge, as recorded in the
[canonical clock rule](../docs/circuit-time-and-clock.md#clock-and-event-model).
Independent NUnit cases cover a two-SR X/X→1/X→1/0→0/0 sequence, same-edge
sampling between chained flip-flops, reset, and exact five-pin projection.

The repository-local offline command recorded in
[slice 2 progress](slice-2-graph-runtime-progress.md) compiled all core C#
files and exited 0. Its additional harness line was:

```text
PASS: all 16 SR clock transitions and uncertain-control examples
PASS: graph-driven independent two-SR three-edge sequence
PASS: same-edge SR sampling uses pre-update outputs
```

The NUnit tests, Unity build, and player behavior await an actual Unity Editor
run. The harness does not verify timestamped event ordering, offscreen play,
module instance qualification, or performance.
