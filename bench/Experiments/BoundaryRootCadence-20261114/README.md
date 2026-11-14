# Complete boundary roots with original deadline cadence

Accepted corrected source `e630021b`, against the exact frozen Core `1f2b34c1`
pair. A complete LTR backtracking Boundary/Accept root without captures/history
or assertions uses the same complete-neighbor predicate as the VM. Keep initial
workspace admission/reset, entry/final work, original tokens and existing step-
based deadline polling. Public operation/cursor publication checks remain. NB,
RTL and extended roots keep their routes. No state, descriptor, allocation, API
or execution mode. [All observations and both trials](acceptance.json).

The initial `77785e76` trial improved Python splits13–15% but **regressed funded
Core Count38.83%**,124.242→172.484us, with separated ranges. It introduced two forced
clock checks per attempt through TryChargeInputStep. Restored all source/tests to
green741a94e3 with `678a163a`, rebuilt Core only and checked42 boundaries per target.
Then one explicitly bounded correction reused TryChargeStep's original cadence.
The first result/restoration remain; neither is omitted from this decision.

Two measured comparisons total **151.951s**: initial76.042s, corrected75.909s.
Each is a ten-row ordinary-process ABBA, five150ms-target samples per process/row,
all>=20ms,150s cap, **zero confirmations**. Same original baseline/compiler bytes;
identical adapters/caller/.NET within each comparison. Caller source is unchanged
between comparisons. No further tuning or timing is authorized by this experiment.

The corrective first before process was anomalously slow in early rows. Report
both process medians below; **do not headline the inflated pooled48% split gain**.
Every after sample in the split/negative Count/funded Count rows is faster than
the fastest before sample, so the direction survives the faster baseline. This
is observed range separation, not a formal confidence interval or a normalized
estimate. All allocations are unchanged.

| Complete public operation | Before process medians µs | After process medians µs | B/op |
|---|---:|---:|---:|
| Python ASCII split | 193.277/83.346 | 71.206/71.793 | 40016→40016 |
| Python funded/deadline split | 379.356/198.804 | 182.005/184.842 | 44288→44288 |
| Python Unicode split | 325.607/142.803 | 122.461/124.804 | 65640→65640 |
| Python negative-boundary Count | 467.211/267.440 | 231.906/232.363 | 528→528 |
| Core Unicode value | 0.497/0.223 | 0.199/0.195 | 0→0 |
| Core funded/deadline Count | 211.456/118.281 | 110.331/105.863 | 160→160 |
| Declined general assertion | 168.205/163.237 | 160.463/164.404 | 1056→1056 |
| Captured-string path control | 103.680/100.860 | 103.676/102.640 | 57640→57640 |
| Core literal path control | 11.945/12.073 | 12.093/12.273 | 0→0 |
| Identical .NET control | 22.156/22.427 | 22.324/22.244 | 0→0 |

Core value/general assertion/captured-string/literal/.NET ranges overlap; no
separated gain is claimed there. The literal control's+1.68% pooled median remains
disclosed. Complete strings, byte/UTF-16 values and Counts agree after timing;
funded loans refund. Core boundary rows select AssumeValid; the unchanged literal
control uses default Validate. PythonRe consumes valid unchanged UTF-8 text. The
archived driver's general Core-policy sentence overstates the control; archived
source and this explicit policy list are authoritative. No new CPython/native ratio.

Focused Release checks pass for BOTH variants on Windows.NET10/11 and Linux.NET11:
Core1410,Python5198,PCRE2427. Coverage includes independent expanded predicates at
every scalar offset, ASCII/Unicode/scoped word sets, empty/nonempty retry, captures,
NB/RTL, byte/UTF-16 projections, aliases, validation and original interruption/
resource controls. Only Core was built; unchanged test/adapter binaries reused.

Raw archives verified offVM before releasing keepers:

- Initial `artifacts/experiments/boundary-root-execution-20261114/evidence.tar.gz` — `CBD7CBE71DAA0246E0D6F7FE71642391B533AEC8CCA417425FC183CFEC96FD6E`.
- Restoration `artifacts/experiments/boundary-root-restore-20261114/evidence.tar.gz` — `D73178E007CA471E2EF57E354097974B99773B8D157A0DB35EE1F87E69B3D66D`.
- Corrected `artifacts/experiments/boundary-root-cadence-20261114/evidence.tar.gz` — `D712E1BD610EC29C7CFC71BE451285EA24AD93293223D1635C01CF8E581EE605`.

Local checks are archived alongside corrected evidence. All workers/collectors/
keepers terminal0, all588 package/symbol bytes unchanged. No package, Full or
canonical refresh. This bounded path is CLOSED; retain the corrected variant.
