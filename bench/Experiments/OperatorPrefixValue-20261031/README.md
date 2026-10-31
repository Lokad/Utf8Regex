# Bounded operator-prefix value Match

Accepted source: `81ae7ee8`; baseline: `926bdf0d`. The existing bounded,
fully validated ASCII value-prefix path now admits the already proved
AnchoredOperatorRun family. This adds one family to the existing admission list,
without another executor, selector or ownership mechanism. The original
invocation meters discovery/publication and checks cancellation/deadlines.

The envelope remains zero byte origin, at most 2048 bytes, complete ASCII
validation and no RTL/NonBacktracking. Larger/mixed inputs, nonzero origins and
detailed captures retain their existing routes. Avoiding VM capture workspace
intentionally reduces route-dependent working/result admission costs; matching
semantics and retained lifetimes are unchanged. See [Core contract](../../../docs/SPEC.md).

[Focused Release checks](verification.txt) pass on both .NET 10 and 11: Core116,
PythonRe137 and PCRE293 per runtime, plus three snapshot checks. Coverage includes
actual route selection, 2047/2048/2049-byte bounds, borrowed spans/canaries, strict
malformed tails, Unicode/nonzero origins, G progression, detailed captures,
cursor aliases, folded/RTL/NB exclusions, finite admission/work denial and original
token/deadline publication. The initial test incorrectly expected a long RTL
fallback to fit its finite work cap; the archived failure remains, and the final
test explicitly preserves that work denial.

One [public A/B](comparison.json), **90.004s**, minimum sample **51.152ms**, ten
cases/four lanes, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Each actual operation is
28 public Match calls over 451 bytes, matching the published independent-subject
loop. Per-subject/full-loop results agree with predecoded and decode-then Regex.
Warm1.5s/min1000 calls, independent60ms calibration and five alternating samples;
result batches are checked after timing. No confirmation was needed.

| Public loop | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Actual ordinary | 25.513 | 6.029 | -76.37% | 47040 / 0 |
| Actual compiled | 24.112 | 6.075 | -74.80% | 47040 / 0 |
| Actual finite/live | 28.833 | 9.426 | -67.31% | 50400 / 3360 |
| Actual deadline/live | 35.118 | 13.048 | -62.85% | 51744 / 4704 |
| Unicode-tail control | 3.397 | 3.476 | +2.33% | 6720 / 6720 |
| Large scalar control | 11.731 | 11.244 | -4.16% | 1680 / 1680 |
| Literal control | 0.709 | 0.762 | +7.59% | 0 / 0 |

Target ordinary/compiled spreads are at most1.10x, funded/live at most1.04x.
Controls have wider spreads (large1.65x, literal after1.33x); do not attribute
their median changes to this source. The dense/number-control results are also
noisy: [summary](summary.json) retains them without claiming their point gains.
Explicit live/finite guarantees still cost allocations; this is not a universal
zero-allocation claim or repayment of the separate warm-template debt.

The existing [selective refresh](refresh-operator.txt) changed only
`lokad/lexer/operator-run` in the ordinary/compiled sections, clean `81ae7ee8`
provenance. Ratios are now **1.32x / 2.43x**, previously **4.73x / 8.24x**;
producer medians6.811/6.794us and0B. All30 samples exceed20ms (minimum27.546ms).
Other cases/identities/lifecycle evidence remain unchanged. The generated README
reorders by ratio, parity verification and both-runtime snapshot checks pass.
These separate catalog observations are not the A/B effect-size measurement.
An initial shell argument-array error selected CLI help rather than measurements;
the archived diagnostic is retained. Only the subsequent proper command timed rows.

The A/B used the baseline commit plus the recorded [patch/test identities](identities.json);
the separately committed refresh has its own clean DLL/PDB/source identities.
[Driver](driver.cs.txt), [predeclared scope](PREDECLARED.txt), raw console and
preflights accompany the result. Immutable archives:

- [Raw/binaries/refresh/source bundle](../../../artifacts/experiments/operator-prefix-value-20261031/evidence.tar.gz):
  `AEE833010E7E03945137BDC27CD2A18DC6206C5B8E859CEBB03F25807ED5B788`.
- [Correctness logs/source snapshot](../../../artifacts/experiments/operator-prefix-value-20261031/correctness-source.zip):
  `27240896091E43E55831D527C5231FA922D82CF352F3AB29608C7373ED336405`.

Published/staged packages are unchanged. The fixed-baseline final-source cost,
full refresh and final immutable package/consumer release boundary remain open.
