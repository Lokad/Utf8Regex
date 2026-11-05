# Smaller fixed frame and repeat-journal chunks

**Accepted with deep-path costs.** Generic short Python searches allocated 3552B
for only a few frames and repeat mutations. Reduce the existing fixed frame chunk
from 64 to 16 entries and repeat-journal chunk from 128 to 32. Trail, balance and
table capacities stay unchanged. No new storage mechanism, pool, selector or mode.
Actual Unsafe.SizeOf queries and checked indexing remain.

Scalar/capture/RTL/NonBacktracking/assertion semantics, retained results/cursors,
instruction/depth metering, original cancellation/deadlines and admission before
allocation/growth remain. Intentional resource revision: smaller minimum working
loans, but more chunks, headers and growth checkpoints on deep paths. Numerical
budget costs remain route/version dependent; [Core's contract](../../../docs/SPEC.md)
records the trade-off. Covered storage still refunds through its existing owner.

One **101.833s** ordinary Default-process A0/B0/B1/A1 compares frozen Full Core
`6cf84468` with receipt-only parent `0fe5ad96` plus the two-constant change. Six
adapter/benchmark DLL/PDB companions and caller are byte-identical; only Core
changes. Normal eight-CPU startup then CPU2. Producer warmup one second/minimum
16 calls; diagnostic BCL 250ms/minimum four; independent 60ms calibration/five
alternating samples. All 180 producer samples exceed 53.9441ms. Typed Count and
complete match-range/value digests are checked after timing. The ref-struct Search
wrapper copies fixed range fields into the same small record inside each timed
call. BCL diagnostics use overlapping ASCII semantics, with `a++b` translated to
`(?>a+)b`; no CPython or catalog Status claim. [All observations](cost-summary.json).

| Public operation | Before us | After us | Change | B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| Core literal control, first | 2.492 | 2.161 | -13.27% | 0 / 0 |
| Python early-literal control | 0.710 | 0.711 | +0.12% | 0 / 0 |
| Actual possessive Search | 0.857 | 0.683 | -20.37% | 3552 / 1056 |
| Actual reluctant Search | 1.015 | 0.821 | -19.14% | 3552 / 1056 |
| Possessive finite/live | 1.061 | 0.858 | -19.14% | 3664 / 1168 |
| Reluctant deadline/live | 1.509 | 1.298 | -13.98% | 3712 / 1216 |
| Captured reluctant Search | 1.490 | 1.252 | -15.93% | 5744 / 3248 |
| Deep 2048-repeat Count | 102.939 | 107.148 | +4.09% | 85848 / 91320 |
| Deep RTL Count | 115.510 | 124.155 | +7.48% | 147360 / 158400 |

Both after-process medians beat both before medians for each targeted Search.
Possessive before 0.845/0.870us versus after 0.685/0.683us retains wider before
spread. Deep costs are consistent across processes. The unchanged Core control's
13.27% movement bypasses these chunks: no causal gain credit or normalization is
assigned. Python's unchanged control is stable overall. The reproducible allocation
reduction and useful Search savings support promotion with the explicit deep costs.
No confirmation, capacity tuning, selected refresh or package/Full matrix follows.

Focused Release checks pass on Windows/Linux for each .NET 10/11 target:
Core 1557, PythonRe 1037, PCRE2 174. Existing cross-chunk depth/admission, assertion,
capture/history, backreference/balancing, RTL/NonBacktracking, finite work/working/
result limits, original interruption and cursor-lifetime checks pass. Only pinned
frame/journal charge expectations change; actual struct-size assertions remain.
All nine independent public preflights agree. Frozen Full DLL/PDBs and staged
package/symbol bytes remain unchanged. This source is qualified in the next useful
delivery batch, not by repeating the completed dev33 checkpoint.

Source CI42035487720 found three admission fixtures relying on the old 4KiB
scratch boundary. Smaller chunks correctly let detailed/read-dependent operations
fit, sometimes reaching the result cap instead. The fixtures now derive their
working limit from the unchanged capture-trail reservation: value-only operations
fit while observed captures still fail working admission. All 84 checks pass per
target on Windows/Linux. No producer or timing change follows this correction.

Immutable artifacts/experiments/small-vm-chunks-20261105, verified off VM:

- evidence.tar.gz: `E6E77C01B118C540B7D17C584359EEB9E9E0002815C1FF41AA4B87C3566D91A8`.
- windows-correctness.zip: `5A13715DDD626EB24D9B0454F25B9686407E6556A06E67C7F3778898BD856BD6`.
- admission-fix.zip: `3D18A71BE87E0D6B335A06B8DB38F3541C3D50895DC705C307AAFD9403C96646`.

Declaration `2A7D539B51CD344F2527E568753108639C7547613D4072C5EBBED6B36D5BBC5B`
and initial two-file patch `A1A689D52B69A87AE724F55926D866D8D60FC572430EAC8B1605BE87390B490A`
are unchanged. Documentation follows the completed comparison; no runtime tuning.
