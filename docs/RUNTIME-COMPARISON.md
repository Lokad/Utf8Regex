# Historical .NET 10 / 11 Count comparison

This completed experiment measured the same **Core114** DLL
(producer 01968d0f), not current development or RC2.
[Protocol/identities](../bench/Experiments/Runtime11-Scalar114-20261020/README.md)
and [raw results](../bench/Experiments/Runtime11-Scalar114-20261020/measurement.json)
are immutable. See [current evidence](CURRENT-PERFORMANCE.md) for release scope.

## Frozen observation: 2026-10-20

SDK 10.0.401/net10 ran on .NET 10.0.12 and 11.0.0-rc.1.26425.128 with the same
harness IL. The SDK11RC1/net11 cell also changed compiler/TFM/generator effects.
One 267.51-second batch, CPU2/FullOpts/R2R off; all 840 samples exceeded 65.453 ms.
No confirmation, retarget, package qualification or Status refresh followed.

Median public Count microseconds: unlimited / finite / finite+live-token.
Finite allowances were work 50M, depth 1,024, working 64 MiB, result 128 MiB.

| Case | Runtime10/SDK10 | Runtime11/SDK10 | Runtime11/SDK11 |
| --- | ---: | ---: | ---: |
| literal-late | 4.921 / 3.814 / 3.841 | 3.948 / 2.787 / 2.801 | 3.792 / 2.755 / 2.804 |
| fixed-width | 7.830 / 19.724 / 19.330 | 8.530 / 21.039 / 20.729 | 7.365 / 19.703 / 19.800 |
| optional-prefix | 138.814 / 539.587 / 535.357 | 134.942 / 1143.013 / 1138.786 | 134.062 / 1139.508 / 1136.508 |
| shadowed-family | 114.704 / 123.407 / 123.021 | 116.167 / 128.449 / 125.534 | 116.934 / 130.441 / 126.094 |

Literal medians improved about 20–27% on runtime11, while finite optional-prefix
cost about 2.1x runtime10. This was a route/runtime lead, not an attributed JIT
defect or justification for removing controls. Some controls varied up to 1.541x;
small differences and universal wins were not qualified.
Scalar/code-unit mismatches were excluded.

All compiled/BCL/generated/NonBacktracking/decode/predecoded/allocation lanes
remain in the numeric receipt. The experiment neither recommends a runtime
upgrade nor qualifies current runtime/profile correctness.
[Earlier decisions/history](https://github.com/Lokad/Utf8Regex/blob/91973e3a/RUNTIME-COMPARISON.md)
include the rejected ordinal-reference prototype; it remains reverted.
