# Smaller fixed capture-trail chunks

**Accepted with deep-path costs.** Captured atomic Search still allocated2784B,
including a128-entry capture trail. Reduce only this existing chunk to32 entries
(2080->544 modeled bytes). Frame16/journal32/balance128/table4 stay unchanged. No
new runner, storage mechanism, pool, selector, mode or unchecked access. Actual
Unsafe.SizeOf queries, chunk algorithms, rollback and checked indexing remain.

Strict full-subject UTF-8/scalars, captures/history, RTL/NonBacktracking, nested
atomic/assertion/balancing behavior, retained aliases, original synchronous
cancellation/deadlines and admission before allocation/growth remain. Intentional
resource revision: smaller initial trail loans, with more chunks/headers/table
growth and metered acquisition checkpoints on deep histories. Covered storage
still refunds through its existing owner. [Contract](../../../docs/SPEC.md).

One **102.495s** separate ordinary Default-process A0/B0/B1/A1 compares the exact
accepted AtomicRepeatCommit afterCore with parent `fc20a08a` plus the archived
five-file runtime/tests patch. The baseline is reused without rebuild; six frozen
FD Full adapter/benchmark DLL/PDB companions and the worker are identical. Normal
eight-CPU startup thenCPU2; producer warm1s/min16, BCL250ms/min4, independent60ms
calibration and five alternating samples. All180 producer samples exceed48.7297ms.
Complete typed results are consumed after timing: range/borrowed values, every
FindAll capture string and counts. Ref-struct fields are copied inside identical
timed wrappers. BCL predecoded/decode-then lanes are equal-result diagnostics,
not CPython/catalog verdicts. [All observations/process spreads](cost-summary.json).

| Public operation | Before us | After us | Change | B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| core-literal-control | 2.506 | 2.490 | -0.63% | 0 / 0 |
| captured-atomic-search | 0.690 | 0.575 | -16.63% | 2784 / 1248 |
| two-capture-search | 1.266 | 1.092 | -13.76% | 3248 / 1712 |
| captured-finite-live | 0.855 | 0.771 | -9.82% | 2896 / 1360 |
| captured-deadline-live | 1.161 | 1.050 | -9.53% | 2944 / 1408 |
| actual-one-capture-findall | 403.596 | 392.771 | -2.68% | 96536 / 95000 |
| deep-observed-capture-count | 206.143 | 208.698 | +1.24% | 228224 / 237488 |
| deep-observed-capture-rtl-count | 219.859 | 221.752 | +0.86% | 295304 / 304568 |
| uncaptured-greedy-control | 0.361 | 0.358 | -0.91% | 64 / 64 |

Both after-process medians beat both before on the five affected short/bulk rows.
Deep2048-scalar LTR/RTL Count uses backreferences to force live capture state;
allocations rise9264B each. Their pooled time costs are retained even though some
process observations overlap. Unchanged Core literal and uncaptured greedy
controls move -0.63/-0.91%; no normalization or causal gain credit. The public
FindAll case is exactly catalog `findall/one-capture-strings` (item-([0-9]+),
"item-12 item-345 " repeated1024). Its isolated403.60us baseline is separate from
dev34's491.54us Full paired observation; no cross-harness speedup or new Status.
No confirmation, capacity/source tuning or individual package/Full replay follows.

Windows/Linux focused Release per .NET10/11: Core3827/Python1143/PCRE202 PASS.
New20 Core cases cover capture/history and failed-branch rollback around chunk/
table boundaries, count0..257, scalar BMP/astral histories, RTL/compiled and retained
results after subsequent calls. Existing focused suites cover meaningful limits,
funded cleanup/cursor ownership, assertions/balancing and original interruption.
Eight initial Windows failures were obsolete TrailReserveBytes-1 fixture budgets
that no longer admitted value-only frame/journal storage. Updated fixtures admit
those stacks but still require detailed/cursor captures to fail WorkingBytes;
no producer enforcement or semantic assertion is weakened. Initial logs retained.
Actual Unsafe.SizeOf assertions remain; only the pinned trail charge changes.

Before any preflight/timing, catalog inspection corrected the initial draft's
Unicode Split-derived captured neighbor to the actual one-capture FindAll row.
Original declaration96569E8F and scope amendment97AFFB72 are retained; no measured
outcome motivated the correction. FullOpts preserves existing Push implementations
(frame2193/trail2207/journal2216/restore216bytes each before/after); no kernel claim.

Immutable artifacts/experiments/small-capture-trail-20261105, verified off VM:

- evidence.tar.gz: `92BA42BB258A11C05764D45E8CBE8300AD3D1A910DCA579DB7DEC17B8FFAC990`.
- windows-correctness.zip: `452D2E4F181BB20F39DB9C03B4D74AB95EEC842118DC6F4A600C8C22D405B034`.

Declaration `96569E8F798EDAB6028C3AEC061F1679A02BB8C1455F947B1B23E4EA079FB4AB`,
scope amendment `97AFFB7286A9FC9BA632148E157D56CE5B95DC693162315C70969BDF7A04A001`
and runtime/tests patch
`20095557007406E50A3FF56CC084178DBADBE4F0E1527027DEFF69F3F230EC54` remain fixed.
Documentation/common-version freeze follows measurement; no runtime tuning. The
old accepted before/after graphs, eight dev34 Full DLL/PDBs and staged33/34 packages
remain immutable. Combine this with atomic repeat commit for the next checkpoint.
