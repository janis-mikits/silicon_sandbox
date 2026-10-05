# Convergence guard progress

An independent synthetic acyclic fixture now chains 1,500 one-bit AND gates between a changing source and the observed output. It checks that a known 1 reaches the far end, then a source change to 0 settles all 1,500 gates without being mistaken for a 1,024-pass feedback fault. The offline repository-local Edit Mode runner passed **159 tests, zero failed** after this fixture was added; the exact command is recorded in [benchmark instrumentation](benchmark-instrumentation-progress.md).

This covers the long-path side of internal scheduler check C08. Stable and converging feedback fixtures, an actual synthetic zero-delay oscillator, and the specified safe pause/X diagnostic on guard exhaustion are still missing. The native Unity Test Runner has not executed this revision. Changed code: `UnityProject/Assets/SiliconSandbox/Tests/EditMode/GraphDrivenCircuitTests.cs`.
