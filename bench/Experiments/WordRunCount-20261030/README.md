# Metered word-run Count under scalar dispatch

Producer **dab878a6d49247a127c8ac638c5b3476d3236773**, baseline
**5a91f9c4c5e973258af511d6479c16ab6b57e7cb**. One bounded public A/B;
the two affected published cases then refreshed through existing tools.

The classifier already proved greedy unbounded word repeats with a positive
minimum, optionally surrounded by word boundaries and ordinary captures. Public
Count's scalar selector preempted the legacy counter. [Before](before-route.jsonl)
and [after](after-route.jsonl) probes show actual dispatch; the historical
common/matches-boundary case ID measures **Count**, not result materialization.

The same word-run algorithm now runs on fully validated ASCII with the original
invocation. Every bounded 32-byte count block and publication admits work and
observes the original token/deadline. No capture workspace is acquired. Mixed
UTF-8 continues scalar execution with the same spent counters/deadline. Lazy and
finite-repeat shapes, extended classes, RTL, NonBacktracking and ECMAScript retain
their existing paths. The superseded word case/raw loop was removed from the
legacy router, leaving one metered kernel; no dormant runtime was restored.
No public API, unsafe access, allocation framework or precision accounting added.

Tight working/depth/result limits can admit this constant-space count where VM
scratch formerly required more capacity. Work/resource totals depend on the route;
denial remains typed failure, not partial/no-match success. A new test caught
raw validation WorkSteps denial escaping as InvalidOperationException; the new
branch now uses the existing sticky-budget conversion. Native-work cancellation,
deadline and post-validation work denial are separately exercised. See the
[resource contract](../../../docs/SPEC.md).

Release correctness: **40054 Core/runtime**, **169 affected PythonRe/runtime**
and **89 affected PCRE2/runtime**. Core includes 26 new cases, random 7-bit
membership/minimum comparisons, block/tail/capture aliases, lazy/finite exclusions,
RTL/NB/ECMA, BMP/supplementary scalar membership, strict malformed tails,
pre-cancellation, constant-space finite admission and native interruption/denial.
All tests pass on .NET 10 and 11, including existing API/managed inventory guards.
[Verification](verification.txt) retains the summaries. Failed compiler/test
fixtures and the demonstrated validation-error fix are preserved in the archives.

One [public comparison](comparison.json) took **63.06s**, minimum sample
**79.58ms**, under a 120s cap. No noise confirmation. Quiet Ubuntu 24.04.4 VM,
Xeon Platinum 8573C, .NET 11.0.0-rc.3.26522.148, CPU mask 4. Eight independent
lanes: before/after ordinary, before/after compiled, predecoded ordinary/compiled
.NET, decode-then ordinary/compiled .NET. 750ms warmup/80ms calibration per lane,
three alternating samples. Equal public Count results retained/checked after
timing. The 8208-byte source is exactly CommonSearchText's repeated paragraph;
raw subject hashes are in JSON. Finite/live producers enforce controls unavailable
from BCL Count; comparator lanes do not emulate those guarantees.

| Public Count workload | Ordinary before / after, us | Ordinary gain | Compiled gain | Allocated B before / after |
| --- | ---: | ---: | ---: | ---: |
| Published boundary miss | 831.310 / 6.983 | 119.05x | 120.00x | 1680 / 0 |
| Positive boundary matches | 927.435 / 9.381 | 98.86x | 100.87x | 3552 / 0 |
| Captured word repeat | 402.816 / 7.170 | 56.18x | 55.32x | 1680 / 0 |
| Finite/live boundary miss | 879.089 / 7.818 | 112.44x | 111.75x | 1800 / 120 |
| Unicode scalar control | 841.180 / 832.320 | 1.01x | 1.01x | 3552 / 3552 |
| Literal control | 0.945 / 0.940 | 1.00x | 0.99x | 0 / 0 |

Controls remain within roughly 1%; allocations are unchanged. Finite/live after
cost is 7.818us/120B versus 6.983us/0B unrestricted: an observed **+12.0%** for
explicit controls. These are complete public costs, not attribution estimates.
The mixed-input control still has a large gap; this ASCII proof does not claim a
Unicode counter. No checked-access campaign or broad package matrix was reopened.

The existing --refresh-readme-case command refreshed **only**
common/matches-boundary and common/matches-set, five V3 samples each. Both ordinary
and compiled sections retain clean dab878a6 provenance and all per-lane samples
exceed 20ms (minimum **49.263ms**). The boundary row is **7.002us**, ratio
**0.043 ordinary / 0.067 compiled**, from **5.06 / 7.85**. Set row is **7.892us**,
ratio **0.039 / 0.074**, from **0.41 / 0.75**. Both now have zero producer
allocations. Every other case, its historical identities and lifecycle/scaling
evidence remain unchanged; prior raw samples stay in preserved historical evidence.
Generated README tables reorder by ratio.
README.Parity.json verification passes. Raw [boundary refresh](refresh-boundary.log)
and [set refresh](refresh-set.log) are retained.

[Driver](driver.cs.txt), [project](driver.csproj.txt), [route probe](route-probe.cs.txt),
[console](comparison.txt) and [preflight](preflight.txt) accompany the raw data.
Immutable local archives under artifacts/experiments/parity-boundary-count-20261030/:

- word-run-count-dab878a6.tar.gz:
  `BC71469BD630177367D3CE4AD083AFF76D286B34D4E89A467FAE946C2A04E3DA`.
- correctness-logs.zip:
  `CD66D2C47DB25446CDBE2BA1922475E8401D40E5BA44BD3AC8F9BC74F49D3FAD`.

Archives include source bundle, baseline/candidate DLL/PDB pairs, matching benchmark
host/profile identities, exact driver/binary, raw refreshes/snapshots and failed/
passing correctness evidence. Published/staged packages are unchanged. Final
fixed-baseline cumulative comparison, full current sub-hour refresh and actual
package/platform/consumer release gates remain open.
