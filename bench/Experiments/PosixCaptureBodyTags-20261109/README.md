# POSIX capture-body tag simplification

Accepted after frozen dev60 (`848dbb29`), without changing its immutable packages
or snapshots. A capture's immediate non-capture body has exactly the same
occurrences and boundaries as its wrapper. Its fictive tag cannot distinguish
paths once the wrapper's higher priority is equal. Lower that body under the
wrapper's tag, preserving descendant order and occurrence enclosure. Directly
nested public captures retain separate tags. No runtime, buffer, mode or admission
mechanism is added; construction removes redundant tag/state/instruction work.

One **67.354-second** ordinary-process ABBA on the quiet .NET11 RC3 VM, within the
declared180s cap, reuses the existing17-lane public worker. Core and caller DLLs
are identical; one producer is loaded per process. Five samples per process/row
follow warmup and independent calibration. All340 samples exceed63ms; full results
and zero outstanding working loans are checked after timing. No confirmation,
catalog refresh or package qualification.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Nested captures |4.073|2.842|−30.2%|
| Empty captures |1.434|1.490|+3.9%|
| Nullable captures |1.062|1.075|+1.2%|
| Absent capture |0.645|0.656|+1.8%|
| Empty captures, finite/live |1.894|1.922|+1.5%|
| Empty captures, funded/live |2.058|2.096|+1.9%|
| Empty captures, deadline/live |2.221|2.267|+2.1%|
| History length12 |101.697|102.872|+1.2%|
| SKU control,4096 calls |5145.587|5188.993|+0.8%|
| Unchanged Core Count |0.839|0.845|+0.8%|

Nested-call allocation falls224→208B. Both independent before processes measure
4.045/4.114µs and after2.790/2.902µs; the benefit is clear. Other allocations
are unchanged; funded empty/nullable peaks stay976/960B. The small adverse
observations and controls are retained without normalization or causal claims.
Most catalog captures already omitted their unobservable body internals, so this
does not close the highest empty/nullable gaps or repeated-history complexity.
[Summary](summary.json) retains every process median, range, allocation and peak.

All177 POSIX Release checks pass per target on Windows/Linux, .NET10/11. Three
new independently specified public cases cover a nested alternative before a
suffix, final-iteration absence under concatenation and supplementary-scalar
coordinates. Existing checks cover output window sizes, hierarchical/repeated/
empty participation, cursor aliases, strict malformed input, original token/
deadline authority, finite refusal, construction admission and exact refunds.
No result/lifetime contract changes. Numerical work/storage admission may decrease
because actual redundant instructions and histories are removed; the existing
version-dependent budget contract and bounded checkpoints remain.

The first VM patch transfer used CRLF and was rejected before source application,
build or timing. Corrected LF transfer succeeded; no measurement was repeated.
Raw protocol, patch, matching graphs, Linux checks and public observations are
verified off VM at
`artifacts/experiments/posix-capture-body-tags-20261109/evidence.tar.gz`, SHA-256
`12D3F980DDA4DBACFE9B754781B0EA87BE22EC0EB25B71B8F4163D6E39B8FC81`.
Windows checks are alongside it. Source CI42693168125 is green on Windows/Linux for5880c17a;
dev60 delivery remains independently complete and immutable.
