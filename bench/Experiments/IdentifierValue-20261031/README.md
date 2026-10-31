# Bounded identifier value matching

Accepted source `04e75512`; frozen baseline `a26445d9`. The existing <=2048-byte,
zero-origin, fully ASCII value envelope now admits the complete AsciiIdentifierToken
grammar and its existing checked finder. Its selected index is forwarded to the
existing value projection. No new executor, state, field, mode, API or unchecked
access. Strict full validation, bounded work and original-token/deadline discovery
and publication remain. Detailed captures and mixed/wider/nonzero-origin/RTL/NB/
global-IgnoreCase paths retain their contracts. Lower workspace/depth admission
is documented in [SPEC](../../../docs/SPEC.md).

[Release checks](verification.txt) pass on net10/net11: Core361/runtime,
PythonRe589/runtime, PCRE280/runtime, plus snapshot3/runtime. New coverage includes
nonzero selected indices, 2047–2049-byte bounds, offsets, supplementary prefixes,
Kelvin/inline folding, captures/copied aliases, canaries/malformed tails, work
cutoffs and original token/deadline publication. No failing prototype or test fix.

One [public A/B](comparison.json), **105.047s**, twelve cases/four lanes, minimum
sample **41.054ms**, quiet Ubuntu VM/.NET11 RC3/CPU mask4. Actual workload:
62 independent public Match calls/815ASCII bytes. Complete UTF-16 range digests
agree per subject and whole loop; byte/scalar/capture behavior is separately
tested. Full validation/publication included, setup/compilation excluded.
Warm1.5s/min1000 calls, independent60ms calibration, five alternating samples;
retained batches are consumed after timing.

| Public Match loop | Before us | After us | Change | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Actual ordinary | 40.088 | 10.125 | -74.74%, unsettled magnitude | 104160 / 0 |
| Actual compiled | 31.607 | 9.923 | -68.60%, unsettled magnitude | 104160 / 0 |
| Actual finite/live | 72.463 | 25.142 | -65.30% | 111600 / 7440 |
| Actual deadline/live | 50.877 | 20.640 | -59.43%, unsettled magnitude | 114576 / 10416 |
| Nonzero-index gaps | 163.628 | 0.455 | -99.72%, unsettled magnitude | 1680 / 0 |
| Dense2048-byte ASCII | 11.975 | 0.614 | -94.87% | 1680 / 0 |
| Unchanged Unicode | 4.792 | 4.840 | +1.00% | 6720 / 6720 |
| Empty/ASCII misses | 122.991 | 0.767 | -99.38%, unsettled magnitude | 192 / 0 |
| Large scalar control | 240.729 | 213.813 | -11.18%, unsettled | 1680 / 1680 |
| Number-prefix control | 23.668 | 19.560 | -17.36%, unsettled | 0 / 0 |
| Quoted-prefix control | 58.371 | 52.749 | -9.63% | 30536 / 30536 |
| Literal control | 0.800 | 0.790 | -1.26% | 0 / 0 |

Finite/live spreads are <=1.03x; ordinary/compiled/deadline lanes are bimodal
1.5–1.65x, but ranges still show clear improvements. Unicode/quoted/literal
controls have <=1.02x spreads and unchanged allocations. Large/number controls
have noisy negative medians; do not claim causal gains. [Summary](summary.json)
retains every sample/spread. **No confirmation** needed; no universal speedup,
zero-allocation or blanket10% equivalence claim. Controlled calls retain the
original120/168B invocation cost, totaling7440/10416B over62 calls.

Only helper-identifier rows were selectively refreshed from clean `04e75512`:
**10.136/10.139us** ordinary/compiled,0B, ratios **1.413/2.062x** against matching
predecoded .NET11. Both remain **Gap**. The previous compiled6.46x is a historical
observation; producer gains come from the fixed-baseline A/B. [Selective log](selective-refresh.txt)
retains raw durations (minimum42.462ms); generated JSON/Markdown verify and
both-runtime snapshot checks pass. No full catalog/package qualification.

[Driver](driver.cs.txt), preflights and [manifest](identities.json) bind timed
precommit DLL/PDBs to their source patch/test hash. Committed-source refresh has
separate [binary hashes](clean-linux-hashes.txt). Immutable archives retain raw
measurements, source/binaries, correctness and the previous VM snapshot:

- [Raw/source/binaries](../../../artifacts/experiments/identifier-value-20261031/evidence.tar.gz):
  `898277A6CD0A847A4AFF3F758758B466CCDA1E5EDB4AEA48CC9D9BD6A3F3582B`.
- [Correctness/source](../../../artifacts/experiments/identifier-value-20261031/correctness-source.zip):
  `75F51455A40B689739F59C8418274252E1C9386B3E7AC61B94016EF3D8436F10`.

Published/staged bytes remain immutable. Highest remaining active Core ratios are
Chinese Sherlock6.247x ordinary and lexer identifier6.13x compiled. Final-source
cumulative costs, full refresh and fresh package/consumer acceptance remain.
