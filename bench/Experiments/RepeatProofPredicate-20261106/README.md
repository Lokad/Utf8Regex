# Hoist pure predicate dispatch in the repeat proof

**Accepted with local costs.** Parent `94c73a95cf3f2600ebed5598a268d8ddeb1ed78d`.
The existing positive-ASCII follower proof binds a sole, unfolded scalar predicate
and its mask once before enumerating members. It preserves mask short-circuit,
table/outer negation, predicate-call order and the original charge per member.
Folded, shorthand-union, multiple-table and class-program shapes retain generic
evaluation. No field, buffer, cached flag, executor, mode or allocation is added.

The actual Python verbose FullMatch [operands](operands.json) contain one pure
IsSpace table followed by the ten ASCII digits. Previously each digit repeated
generic scalar dispatch and Rune wrapping. The21-line source change reuses the
same immutable table/delegate. New production tests compare the sole-table path
with an equivalent two-table generic path: outcomes, predicate order, exact work,
finite denial and working refunds agree across negation/fold controls. Numerical
resource costs stay unchanged, as do strict UTF-8/scalars, captures/aliases,
original synchronous cancellation/deadlines and admission before needed storage.

One **85.380036009s** Default-process ABBA, quiet VM/.NET11 RC3, normal eight-CPU
startup thenCPU2,1s/min128warmup, independent75ms targets/five samples:14public
rows/280samples, minimum68.7872ms, under120s. Before reuses accepted Core03C32073,
after1ADD2918; qualified46 adapters and caller bytes are identical. Typed complete
results are consumed after timing. No confirmation or control normalization.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual verbose FullMatch |0.59|0.48|-18.42%|0|
| Same finite/live |0.75|0.68|-10.37%|112|
| Same deadline/live |0.87|0.80|-7.70%|160|
| Unicode whitespace, finite |0.77|0.70|-9.02%|112|
| Captured whitespace, finite |0.84|0.76|-9.51%|176|
| 4KiB whitespace, finite |34.45|32.45|-5.82%|112|
| Core shorthand control |0.69|0.74|+7.28%|112|
| One-capture strings control |376.04|385.25|+2.45%|94344|
| Two-capture strings control |631.52|675.04|+6.89%|242008|

Both candidate process medians beat both baselines on the actual ordinary/finite/
deadline/Unicode/captured/long target rows. Both are slower on Core shorthand,
one- and two-capture controls; these public costs are retained. Two-capture
before632.70/629.27us versus after667.27/688.05us is not treated as noise.
All allocations are unchanged. Accept these local costs for the larger targeted
gains, following the user's trade-off policy. No universal slowdown gate applies.

[All observations](cost-summary.json) retain overlapping-set-0.90%, folded+1.07%,
multiple-table+0.05%, PCRE class-program+0.50% and native+0.53%, with ranges and
process medians; they do not receive causal credit. Two equal-result predecoded/
decode-then-.NET diagnostics supplement this comparison. No new CPython/native
verdict, catalog Status, table refresh or package qualification is claimed.

Release FullOpts confirms the bound predicate path and grows4076->4691B (+615B).
That is a code-size cost, not an isolated speedup. Focused Release passes on
Windows/Linux and .NET10/11: Core208/Python1375/PCRE1014 per runtime, including
repeat/negation/folding/capture/history, finite work/depth/working/result refusal,
retained cursors and original interruption. A new fixture's outcome property names
were corrected after a compiler error; production code needed no correction.

Canonical off-VM `artifacts/experiments/repeat-proof-predicate-20261106/evidence.tar.gz`:
`CF8B0E3AB98A819D629AA13BD9413F2CA6B8330AAA1228FA40A652CD8D12B892`,4566547B.
Raw graphs, source overlay, declaration, worker/probes, tests, codegen and durations
are preserved. Eight Full46 assets and all immutable staged packages stay unchanged.
Final package/platform/consumer and complete refresh checks remain at a later
coherent delivery boundary.
