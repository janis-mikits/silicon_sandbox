# SR flip-flop uncertain-control research

**Date:** 30 September 2026  
**Status:** Research record. The user accepted the possible-outcome merge policy on 30 September 2026; the operative rule is now in [SR storage](docs/components-and-rtl-timing.md#sr-storage). This note retains the SystemVerilog comparison and its verification limits, not a second source of game rules.

## Question and standards boundary

The [IEEE 1800-2023 SystemVerilog standard](https://ieeexplore.ieee.org/document/10458102) specifies language semantics. It does not give every circuit called an SR flip-flop one universal X/Z-control truth table: results depend on the chosen HDL description. Accellera language-committee material discusses [an X condition in `if`](https://www.accellera.org/images/eda/sv-bc/11108.html), [X/Z branch merging in `?:`](https://www.accellera.org/images/eda/sv-bc/0591.html), and [case-statement forms](https://accellera.org/images/eda/sv-bc/att-7339/1041_D4_case.htm). These committee records are explanatory evidence, not a substitute for checking the operative IEEE edition and a particular reference simulator configuration.

For known S/R values, the canonical game rule is 00 hold, 01 reset, 10 set, 11 X. This comparison assumes the clock transition is the already specified clean 0-to-1 rising edge; X/Z on CLK is a separate open question. The comparison below uses prior Q of 0 or 1 so that the consequences of uncertain S/R are easy to distinguish.

## Three explicit models

The following are **illustrative reference descriptions**, not game code or interchangeable implementations of one required IEEE SR behavior.

**Priority `if` model:**

```systemverilog
always_ff @(posedge clk) begin
    if (s && r) q <= 1'bx;
    else if (s) q <= 1'b1;
    else if (r) q <= 1'b0;
    // Otherwise hold q.
end
```

An X/Z condition is not taken as true by `if`; this model can retain a known Q even when an unknown control could have changed it. Its exact outcome follows this code, not a general rule for all flip-flops.

**Exact `case` with X default:**

```systemverilog
always_ff @(posedge clk) begin
    case ({s, r})
        2'b00: q <= q;
        2'b01: q <= 1'b0;
        2'b10: q <= 1'b1;
        2'b11: q <= 1'bx;
        default: q <= 1'bx;
    endcase
end
```

This model makes Q = X whenever S or R is X/Z, even where every possible binary control interpretation would yield the same known result. This is an explicit default policy, not an unavoidable result of using SystemVerilog.

**Possible-outcome merge policy:** Treat each X/Z on S or R as either 0 or 1 for evaluating the four known-control rows. If all resulting Q values are identical, retain that value; otherwise produce X. A four-state conditional expression can implement some of this branch-merging behavior, but the policy should be specified in full rather than inferred from one convenient expression. The game would treat this as its own documented component policy and compare a matching explicit SystemVerilog reference model in VCS when available.

The following is a **proposed explicit SystemVerilog reference model** for that policy. It uses the four known-control rows directly and case inequality to compare possible outcomes, including X/Z in the previous stored value. It has not yet been compiled or run in VCS.

```systemverilog
function automatic bit control_allows(input logic control, input bit candidate);
    return (control === 1'bx) || (control === 1'bz) ||
           (control === candidate);
endfunction

function automatic logic sr_possible_next(input logic prior_q,
                                          input logic s,
                                          input logic r);
    logic result, candidate_q;
    bit first;
    first = 1'b1;
    for (int sb = 0; sb < 2; sb++) begin
        if (!control_allows(s, sb[0])) continue;
        for (int rb = 0; rb < 2; rb++) begin
            if (!control_allows(r, rb[0])) continue;
            case ({sb[0], rb[0]})
                2'b00: candidate_q = prior_q;
                2'b01: candidate_q = 1'b0;
                2'b10: candidate_q = 1'b1;
                2'b11: candidate_q = 1'bx;
            endcase
            if (first) begin
                result = candidate_q;
                first = 1'b0;
            end else if (candidate_q !== result) begin
                result = 1'bx;
            end
        end
    end
    return result;
endfunction

always_ff @(posedge clk) q <= sr_possible_next(q, s, r);
assign q_bar = ~q;
```

| Prior Q | S | R | Priority `if` | Exact `case` default | Possible-outcome merge |
| --- | --- | --- | --- | --- | --- |
| 0 | X | 0 | 0 | X | X |
| 1 | X | 0 | 1 | X | 1 |
| 0 | 0 | X | 0 | X | 0 |
| 1 | 0 | X | 1 | X | X |
| 1 | 1 | X | 1 | X | X |
| 0 | Z | 0 | 0 | X | X |

The Z-control cases follow the same uncertainty interpretation as X for this proposed policy; Z remains a distinct **net value** for inspection and driver resolution. The table was checked against the stated model definitions, not executed in VCS.

## Recommendation and limits

The user accepted the **possible-outcome merge policy** because it preserves a definite stored result when every allowed control interpretation agrees, while exposing genuine ambiguity as X. The [canonical component rule](docs/components-and-rtl-timing.md#sr-storage) records it as an explicit SiliconSandbox policy, not a claim that all IEEE-compliant SR descriptions give the same result. Before implementation, derive and test the complete S/R/Q table, including prior Q = X or Z and Q_bar. The subsequently accepted [shared positive-edge rule](docs/circuit-time-and-clock.md#four-state-positive-edge-detection) covers the SR flip-flop's X/Z CLK transitions; the comparative table above assumes a clean clock edge.

Neither VCS nor another HDL simulator was available in the project environment on 30 September 2026, so these example models have not been run against a reference tool. A focused VCS comparison remains a verification step when access is available; it must use the exact HDL model and simulator options claimed as the reference.
