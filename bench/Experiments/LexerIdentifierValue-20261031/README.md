# Bounded folded identifier-prefix values

Accepted source `36efc279`; frozen baseline `5ebf8ba6`. One admission clause adds
the existing AnchoredIdentifierPrefix family to the metered <=2048-byte ASCII,
zero-origin value envelope, including proved IgnoreCase grammars. The existing
classifier/helper, strict validation, original bounded work/token/deadline and
value publication remain. No new executor, field, mode, API or unchecked access.
Mixed/wider/offset/RTL/NB/unproved cases retain their routes. Workspace/depth
admission can decrease, as documented in [SPEC](../../../docs/SPEC.md).

The [production-family probe](family-proof.txt) confirms invariant/en-US admission
and Turkish non-invariant refusal; [source](family-probe.cs.txt) uses existing
Inspection metadata. Full [Release checks](verification.txt) pass on net10/net11:
Core435/runtime, PythonRe589/runtime, PCRE280/runtime and snapshot3/runtime.
Coverage includes captured construction culture after current-culture changes,
Kelvin/Turkic/supplementary input, offsets, captures/copied aliases, 2047–2049-byte
bounds, malformed tails, every work cutoff and original publication authority.

Two scratch diagnostics were corrected before measurement: the initial probe
looked for a field where the producer exposes a ref-readonly property; the first
culture test used a shared scalar oracle that recompiles under the changed
culture. Direct comparison with the original Regex is valid for these BMP-only
consuming classes and passes. Failed/corrected logs are retained; neither required
a production correction beyond the planned admission clause.

One [public A/B](comparison.json), **104.593s**, twelve cases/four lanes, minimum
sample **40.693ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Actual workload:
220 public Match calls/1345ASCII bytes. Complete UTF-16 range digests agree per
subject/loop; byte/scalar/capture/culture behavior is independently tested.
Full validation/publication included, setup/compilation excluded. Warm1.5s/min1000
calls, independent60ms calibration, five alternating samples, retained batches
checked after timing. **No confirmation** needed for acceptance.

| Public Match loop | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Actual ordinary | 107.735 | 31.488 | -70.77% | 369600 / 0 |
| Actual compiled | 107.753 | 31.403 | -70.86%, unsettled magnitude | 369600 / 0 |
| Actual finite/live | 250.137 | 75.266 | -69.91% | 396000 / 26400 |
| Actual deadline/live | 272.074 | 106.292 | -60.93% | 406560 / 36960 |
| Dense2048-byte ASCII | 18.769 | 0.598 | -96.81%, unsettled magnitude | 1680 / 0 |
| Unchanged Unicode | 4.923 | 4.765 | -3.20% | 5168 / 5168 |
| Empty/ASCII misses | 3.463 | 0.760 | -78.07% | 256 / 0 |
| Large scalar control | 18.490 | 18.265 | -1.22% | 1680 / 1680 |
| Helper identifier control | 17.995 | 14.817 | -17.66% | 0 / 0 |
| Number-prefix control | 22.033 | 16.778 | -23.85% | 0 / 0 |
| Quoted-prefix control | 59.630 | 48.324 | -18.96% | 30536 / 30536 |
| Folded-literal control | 1130.188 | 960.355 | -15.03%, unsettled | 0 / 0 |

Ordinary/finite/deadline actual spreads are <=1.06x. Compiled1.77x/1.56x,
dense1.13x/1.14x and folded literal1.12x/1.68x retain uncertainty. Stable negative
control medians are observations, not demonstrated causal improvements. No
control shows an adverse regression. [Summary](summary.json) retains all samples.
No blanket cost equivalence, parity or universal zero-allocation claim; original
120/168B controlled invocation costs total26400/36960B over220 calls.

Only lexer-identifier ordinary/compiled rows were refreshed from clean `36efc279`:
**47.471/47.270us**,0B, ratios **1.449/1.992x** against matching predecoded.NET11.
Both remain **Gap**, versus historical compiled6.13x. This separate observation
is not the A/B denominator. [Selective log](selective-refresh.txt) keeps raw
durations (minimum41.410ms); generated parity/Markdown and snapshot checks pass.
No full catalog or package was requalified; staged/published bytes stay immutable.

[Driver](driver.cs.txt), preflights and [manifest](identities.json) bind original
precommit DLL/PDBs to their patch/test hash. Clean committed-source refresh has
separate [hashes](clean-linux-hashes.txt). Immutable archives retain source/binaries,
raw measurements, failed/corrected diagnostics and the preceding VM snapshot:

- [Raw/source/binaries](../../../artifacts/experiments/lexer-identifier-value-20261031/evidence.tar.gz):
  `4F5CC23B84396FEDD853AF43CE46F2BC4193CDB31DAC3BAC933D3328A4EEA12A`.
- [Correctness/source](../../../artifacts/experiments/lexer-identifier-value-20261031/correctness-source.zip):
  `519FF3B3B60DD8FF1C2E07528CB09F27FC89CE1C6A104DAF062695E09F2E72F3`.

Larger deferred gaps remain visible. Final-source cumulative costs, current full
refresh and fresh immutable package/consumer acceptance remain release work.
