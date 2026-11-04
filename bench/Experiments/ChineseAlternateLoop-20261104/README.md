# Hoist immutable SIMD mask metadata

**Accepted**, source `53356816`. The existing three-mask AVX2 candidate loop loads
its three offsets and six mask vectors before scanning. Baseline Release code
reloaded those immutable values and repeated nine array-entry checks per32-byte
block. Candidate codegen moves them outside the hot loop; checked span/input loads
remain inside. FullOpts method size1355 ->1448 bytes. The mask algorithm,32/16-byte
and scalar tails, cursor state, preparation and allocation behavior remain.
No new runner, mode, retained fields, accounting mechanism or unchecked access.

The actual Chinese route is the earlier packed-nibble SIMD Count strategy,
[verified independently](../ChineseAlternateRoute-20261104/README.md). Its offsets
2/5/8 produce212 candidates for207 matches; changing candidate selectivity would
address little work. The bypassed generic packed search is not the live bottleneck.

One **112.3984674s**,17-case public comparison uses accepted wide Core `3a8cf7e2`
as baseline. Only Core DLL/PDB changes; six PythonRe/PCRE2/benchmark companions
remain byte-identical. Normal eight-CPU startup thenCPU2;1.5s/minimum128 managed
and250ms/minimum64 diagnostic warmup, independent60ms calibration, five alternating
samples. All170 owned samples qualify, minimum **56.1ms**. Counts are consumed after
timing. Allocations are unchanged in all cases. No confirmation or tuning variant.

| Public Count | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| ASCII control first | 2.52 | 2.52 | +0.04% |
| Actual Chinese alternation, ordinary | 199.51 | 154.20 | -22.71% |
| Actual Chinese alternation, compiled | 201.75 | 154.46 | -23.44% |
| Actual Chinese alternation, finite/live | 48623.85 | 48931.65 | +0.63% |
| Actual Chinese alternation, deadline/live | 15061.80 | 15231.67 | +1.13% |
| Actual Chinese alternation, live only | 7291.91 | 7445.37 | +2.10% |
| Single Chinese control | 95.82 | 96.20 | +0.40% |
| Dense Chinese matches | 27.74 | 28.15 | +1.48% |
| Dense false prefixes | 239.99 | 252.22 | +5.10% |
| Chinese no-prefix miss | 18.17 | 13.37 | -26.45% |
| Short offset/tail | 0.26 | 0.25 | -4.50% |
| Ordered overlap | 36.40 | 35.68 | -1.99% |
| Reverse Chinese control | 50903.55 | 52409.75 | +2.96% |
| ASCII family control | 53.72 | 53.73 | +0.01% |
| Folded family | 23.75 | 23.69 | -0.25% |
| Folded Russian control | 228.43 | 259.13 | +13.44% |
| PythonRe control | 9.66 | 9.63 | -0.27% |

Retain the folded Russian cost, about30.7us, and dense false-prefix cost, about12.2us,
without assigning speculative causes. The user's local-cost/major-gain policy
supports promotion alongside the23% actual Chinese and26% miss gains. Separate
same-options .NET predecoded/decode-then lanes are diagnostics; this is neither
catalog Status nor a cumulative performance claim. [All observations](cost-summary.json)
retain spreads, allocations and qualification flags. Controlled results use each
operation's existing budget route; their absolute costs are not universal overhead.

Focused Release checks pass493 per .NET10/11 on Windows/Linux. Existing fixtures
cover exact/folded helper boundaries and public Count, and add borrowed offsets,
dense false prefixes and31/32/33/2047/2048/2049/4096-byte crossings. Ordered matching,
scalars, malformed complete input, original cancellation/deadlines and useful work/
working/result admission remain. Forced SSE fallback passes52 checks per target;
its disassembly has SSE shuffle instructions and no AVX2 shuffle path. No package
or Full matrix was repeated for the experiment. Selective rows are deferred to the
next coherent delivery refresh, preserving earlier immutable evidence.

Immutable evidence under artifacts/experiments/chinese-alternate-loop-20261104/:

- evidence.tar.gz: `010B1E61FF67391C82031D9D51D00C67D0AF7F5E669CACB61C0F67C00B85092B`.
- windows-correctness.zip: `8ACD3DB6BA5359310BCA586209F3A2295AA8EAF65EB7CC20383BF98BAA76A153`.

Raw evidence includes frozen before/after assets, source patch, driver, predeclared
cases, exact corpora, Release codegen, checks, timings and terminal status.
