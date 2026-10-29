# Reuse metered whole-query URL ranges for value Match

c9d9b685 reuses the existing complete digits/hex query proofs and URL helper for
whole-ASCII value Match at origin zero, <=2048 bytes, without RTL/NonBacktracking.
The original classifier retains culture/scoped-fold exclusions. The selected
whole range needs no capture workspace; detailed captures and wider subjects
retain scalar execution. Final LF can belong to a negative value class or remain
outside the match, following the existing grammar's range rules.

The existing invocation now flows through prefix candidates, suffix alternatives
and optional-parameter retries. Those checkpoints meter nested retry work and
observe the original deadline/token; denial cannot publish a selected range.
Legacy helper entry points use an unlimited stack invocation. No meter framework,
execution switch, grammar, unchecked access or allocated helper was added.
Numerical denial thresholds remain route dependent; fixed coordinate values can
avoid working/result arrays as documented in the Core specification.

[Comparison](comparison.json), [console](comparison.txt), [driver](driver.cs.txt),
[project](driver.csproj.txt). Quiet VM, .NET 11 RC3, CPU2; exact afef0bf1 baseline
versus c9d9b685. One **78.70s** public A/B under a 120s cap, sixteen workload
loops, one-second warmup, independent calibration and three alternating ~50ms
samples. The shortest timed sample was 46.34ms. Each input and loop checksum
matches both corresponding .NET lanes. Catalog operations are twelve dashboard
Match calls over 601 bytes and eight download Match calls over 1163 bytes.
No timing confirmation was needed.

| Public operation loop | Before us | After us | Before / after B/op |
| --- | ---: | ---: | ---: |
| Dashboard Match, ordinary | 87.897 | 2.934 | 71896 / 0 |
| Dashboard Match, compiled | 88.009 | 2.926 | 71896 / 0 |
| Download Match, ordinary | 163.050 | 3.441 | 83448 / 0 |
| Download Match, compiled | 162.753 | 3.423 | 83448 / 0 |
| Dashboard finite limits/live token | 95.996 | 4.199 | 73240 / 1344 |
| Download finite limits/live token | 177.168 | 4.346 | 84344 / 896 |
| Dashboard misses | 13.585 | 0.944 | 29200 / 0 |
| Download misses | 17.288 | 0.721 | 17664 / 0 |
| Repeated prefix rejection | 24.827 | 1.978 | 32128 / 0 |
| Optional-parameter retry failure | 50981.700 | 28.371 | 28800 / 0 |
| Unicode control | 7.152 | 7.330 | 17520 / 17520 |
| Long-subject control | 137.664 | 136.738 | 175144 / 175144 |
| Existing Count control | 77.214 | 77.947 | 45016 / 45016 |
| Existing IsMatch control | 89.848 | 91.566 | 71896 / 71896 |
| Number Match control | 0.492 | 0.411 | 0 / 0 |
| Literal Match control | 0.279 | 0.223 | 0 / 0 |

Count/IsMatch remain unchanged-operation controls, with +0.9%/+1.9% costs. The
Unicode control is +2.5%. Negative control variation reaches -20.1% on a tiny
literal operation; it is not claimed as a gain. Allocations stay unchanged in
controls. a40bda03 remains the fixed cumulative release baseline.

The selected [dashboard refresh](dashboard-refresh.txt) qualifies compiled
**11.94x -> 0.3716x** and matching-flag ordinary **0.1198x**. The
[download refresh](download-refresh.txt) gives **11.23x -> 0.2152x** compiled
and matching-flag ordinary **0.0548x**. All four are Win with zero B/loop. Every
V3 sample exceeds 44ms. Historical ordinary script lanes used a compiled
comparator; their ratio changes are not like-for-like producer speedups. The
independent A/B establishes the ~30x/~47x producer gains.

[Parity](parity.txt), [generated Markdown](markdown.txt),
[snapshot comparison](snapshot-diff.txt) and [Release checks](verification.txt)
pass. Only these two cases' ordinary/compiled measurements and parity rows
changed. Focused Core/PythonRe/PCRE2 checks pass on .NET 10 and 11. New tests cover
final LF, padded spans, 2047/2048/2049-byte boundaries, culture/scoped flags,
Unicode/RTL/NB/offset routes, retained detailed captures and cursor aliases,
every retry work cutoff/borrowed loan, malformed tails and original interruption
after validation. No full catalog/platform/package matrix was repeated.

An initial fixture read byte coordinates after no match; the corrected assertions
respect the existing getter contract. Preflight also caught an intended miss
whose negative value class consumed LF: its input was corrected before timing.
Both logs remain archived; no timed data was discarded or retried.

Immutable archives under `artifacts/experiments/query-value-match-20261029/`:

- `parity-query-value-match-c9d9b685.tar.gz`: exact baseline/candidate DLL/PDBs,
  benchmark host, source bundle, driver/build/preflights/comparison and final
  selected snapshots/reports. SHA256
  `E84B5724680F4CC80A716690518EC6113331526AD9AAC242499D9D613C90DCB5`.
- `correctness-logs.zip`: initial/final focused, resource, API/inventory and
  snapshot checks. SHA256
  `80889A757A6E5E0F8E7488CBEDBBCD904B3EEF1F80EEDFD4F49BDEA839EBE1A0`.
