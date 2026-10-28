# Cache small NonBacktracking viability rows

0a4072d9 keeps a <=64-state viability row and its completed successor in local
words while building the existing backwards table. The row is written once;
wider programs retain the packed table path. The algorithm, O(scalars * states)
work/storage bounds, allocations, state charges and interruption checkpoints are
unchanged. There is no new prepared table, matcher or execution mode.

[Raw comparison](comparison.json), [console](comparison.txt),
[driver](driver.cs.txt), [project](driver.csproj.txt),
[selective README refresh](refresh.txt), [verification](verify.txt).
Same quiet VM, .NET 11 RC3, CPU2, VM-built frozen a40bda03 and 0a4072d9. Four
public Count lanes use independent calibration, 250 ms warmup and three
alternating approximately 50 ms samples. Equal-result preflight and timing pass.
One comparison completed in **11.90 seconds**, within its 120-second cap; no
confirmation or favorable rerun. The standalone driver needs the frozen host's
LokadReplica corpus directory beside its executable; the archive includes it.

| Public Count | States | Before us | After us | Elapsed change | B/op, unchanged |
| --- | ---: | ---: | ---: | ---: | ---: |
| Region marker, ordinary, 46,429 bytes / 10 matches | 15 | 7602.79 | 6736.98 | -11.4% | 557416 |
| Region marker, compiled | 15 | 6067.65 | 5227.55 | -13.8% | 557416 |
| 32 KiB slice, finite limits and live token | 15 | 5831.86 | 5015.90 | -14.0% | 393432 |
| Captured alternatives with word boundary / Unicode | 14 | 1026.43 | 917.13 | -10.6% | 135384 |
| Wide-program control | 79 | 3547.43 | 3589.06 | +1.2% | 99560 |
| Unchanged compiled English literal | 16, native literal | 70.244 | 69.867 | -0.5% | 0 |

The ordinary lane has more sample drift than the later compiled lane; all raw
durations remain. The independent five-sample README row is **144.91x ordinary /
144.68x compiled** versus predecoded .NET, previously 176.66x / 172.18x. Its
5376/5395 us medians are separate from the paired costs above. Both remain Gap;
this is a modest reduction, not parity or removal of the preparation cost.

Focused Release checks pass on [.NET 10](core-net10.txt) and
[.NET 11](core-net11.txt): actual 63/64/65-state graphs, counts/ranges/captures,
empty progress, Unicode, replacement, copied cursor aliases, linear batch work,
every work/storage cutoff and original-token cancellation. No public accounting
or lifetime contract changes. Adapter semantics and profiles are not broadened.

Exact measured DLLs/PDBs, driver, raw outputs and generated snapshots are in the
[local immutable archive](../../../artifacts/experiments/nb-rows-20261028/parity-nb-row-0a4072d9.tar.gz),
SHA256 `A80367C0116BD45234FC5767D71B449644FF447C392ED69FA821817C908F6B8F`.
No complete catalog or package matrix was repeated.

Further large reductions need a broader change to full-subject viability
preparation. Keep that cost visible; do not replace this general linear engine
with the benchmark-only line-prefix replay or reduce metering. This bounded
pilot is closed; assess the next existing general fast-path opportunity first.
