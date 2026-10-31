# Terminal set-repeat Count

Accepted source `a3603a00`; frozen baseline `16502c14`. The existing complete-root
greedy class-repeat proof now admits Set. Count reuses the existing positive,
unfolded ASCII-mask predicate; wider, complemented and folded membership stays
scalar. There is no new engine, field, mode, allocation or unchecked access.
Strict full validation, one charge per examined scalar, enclosing working loans
and original-token/deadline publication checks remain. VM workspace/depth and
instruction charges can decrease, as documented in [SPEC](../../../docs/SPEC.md).

The [IR probe](ir-probe.cs.txt) shows that the actual English catalog pattern
`[A-Za-z]{8,13}` is exactly RepeatGreedy/Set/RepeatEnd/RepeatExit/Accept, without
captures. The existing category counter already admits the Russian control.
The shared matching gate was audited: only the complete root can suppress repeat
arrays; assertion children keep independent workspace and required-end handling.
Capture/lazy/nullable/tail/RTL/NonBacktracking shapes retain their paths.

[Release checks](verification.txt) pass on both net10/net11: Core554/runtime,
full PythonRe4387/runtime, affected PCRE2119/runtime and snapshot3/runtime.
Coverage includes independent supplementary-scalar counts, Kelvin folding,
canaries/invalid tails, maxima/minimum tails, captures/copied cursors, required-end
lookbehind, every work cutoff, enclosing loans and final cancellation/deadlines.
One initial fixture expected zero instead of one for a valid suffix; its failed
log and corrected test are retained in the correctness archive.

One [public comparison](comparison.json), **93.881s**, eleven cases/four lanes,
minimum sample **39.904ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Warm1.5s/min64
calls, independent60ms calibration, five alternating samples, retained result
batches checked after timing. Full validation included; compilation/setup excluded.
All counts agree with Regex on overlapping semantics. Supplementary negated sets
use independent correctness tests rather than a UTF-16 performance comparator.

| Public Count | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| English ordinary | 19492.125 | 1392.364 | -92.86% | 1680 / 0 |
| English compiled | 12237.900 | 1065.873 | -91.29%, unsettled magnitude | 1680 / 0 |
| English finite/live | 13406.500 | 1275.750 | -90.48%, unsettled magnitude | 1800 / 120 |
| English deadline/live | 27583.900 | 1676.994 | -93.92% | 1848 / 168 |
| Russian category control | 3993.169 | 3780.484 | -5.33%, unsettled | 0 / 0 |
| Dense maxima/short tail | 11.786 | 11.298 | -4.14% | 0 / 0 |
| Mixed scalar gaps | 657.993 | 52.390 | -92.04%, unsettled magnitude | 1680 / 0 |
| Complemented BMP set | 515.902 | 67.849 | -86.85% | 1680 / 0 |
| Folded Kelvin set | 1027.261 | 92.695 | -90.98%, unsettled magnitude | 1680 / 0 |
| Capture control | 3.527 | 3.705 | +5.07% | 0 / 0 |
| Literal control | 0.438 | 0.413 | -5.78% | 0 / 0 |

[Summary](summary.json) retains all samples/spreads. Ordinary/deadline English
spreads are at most1.04x; compiled/finite/mixed/folded lanes are bimodal, but their
improvement remains clear across the ranges. Russian control spreads1.61x/1.50x
prevent a precise cost claim. **No confirmation** was needed for acceptance. Do
not claim blanket10% equivalence or infer a public effect from kernel attribution.

Only English ordinary/compiled catalog rows were refreshed from clean `a3603a00`
binaries: **1057.606/1057.156us**, zero allocations, ratios **0.370/0.523x** against
matching predecoded .NET11, both recorded Win. Full-refresh historical ratios
were4.33/6.66x; comparator movement is separate from the fixed-baseline public A/B.
[Selective log](selective-refresh.txt) retains raw durations (minimum39.944ms).
Generated JSON/Markdown verify; no full catalog or package was requalified.

[Driver](driver.cs.txt), [identity manifest](identities.json) and preflights bind
the original precommit timed DLL/PDB pair to its patch and test hash. The clean
committed-source refresh has its own [binary hashes](clean-linux-hashes.txt).
The immutable archives include source/binaries, IR proof, correctness logs and
the preserved prior VM snapshot. An initial bundle-ref preparation failure was
corrected before build/timing; no measurement was retried.

- [Raw/source/binaries](../../../artifacts/experiments/terminal-set-count-20261031/evidence.tar.gz):
  `B1F42E8DBACB8E73484134C3985BA47A38F5DA6CDB96AF8D024D405743765E8D`.
- [Correctness/source](../../../artifacts/experiments/terminal-set-count-20261031/correctness-source.zip):
  `0520F63579D2B60188AB836F8721A98616B3D54C1C59653A408CFF290B2B003E`.

Published/staged packages remain immutable. Final-source cumulative performance,
full refresh and a fresh common-version package/consumer gate remain release work.
Next primary Core gap is lexer string6.53x compiled, followed by helper identifier6.46x.
