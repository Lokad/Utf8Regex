# Precomputed unconditional NonBacktracking ancestry

Packed programs (at most 64 states) compile unconditional Fork/Jump/Capture
ancestry once. Runtime rows fold it with word operations and evaluate only
ordered anchor/boundary conditions. The full viability table and ordered capture
replay are preserved. Wider programs retain their existing evaluation.

One construction-admitted immutable array replaces repeated unconditional runtime
dispatch. No invocation buffer, engine, mode, unchecked read or accounting system
was added. Invocation work units, table capacity, direct atom checks, scalar/row
checkpoints, original-token cancellation and absolute deadlines are unchanged.
The [contract](../../../docs/SPEC.md) records the construction cost distinction.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json), **96.372s**,
ran on the quiet Ubuntu VM, .NET 11 RC3, CPU mask 4. Frozen c8ed binaries before
(parent 28c changes only data/receipts); candidate 28c plus the archived producer
patch after. Fifteen cases, four equal-result lanes, warmup 1s/minimum 64 calls,
independent 60ms calibration and five alternating samples; minimum 20.818ms.
Full UTF-8 validation is included. Results are consumed after timing.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Region Count ordinary | 1971.341 | 1437.941 | -27.06% |
| Region Count compiled | 2026.572 | 1488.838 | -26.53% |
| Region Count live token | 2182.675 | 1573.472 | -27.91% |
| Region Count finite/live | 2348.922 | 1721.003 | -26.73% |
| Region Count deadline/live | 3874.806 | 3395.294 | -12.38% |
| Unicode Count | 461.264 | 336.635 | -27.02% |
| Wide Count control | 2383.847 | 2345.128 | -1.62% |
| Nullable Count | 145.704 | 79.822 | -45.22% |
| Dense ASCII Count | 391.682 | 321.082 | -18.02% |
| Dense supplementary Count | 282.308 | 234.059 | -17.09% |
| Category Count deadline/live | 715.616 | 658.951 | -7.92% |
| Category IsMatch control | 1163.227 | 1147.094 | -1.39% |
| NonBacktracking-disabled Count control | 4.736 | 4.674 | -1.31% |
| Literal Count control | 0.397 | 0.395 | -0.68% |

Matching allocations are unchanged in every lane. [Pairs and spreads](summary.json)
are retained. No adverse control regression was observed; these are incremental
gains, not a parity claim or a final cumulative comparison.

The initial cold medians were bimodal and **do not establish a 25% cold speedup**.
The sole allowed [cold-only confirmation](cold-confirmation.json) reused the same
producer/typed-lane binaries, with 5s warmup and a 45s cap. It took 23.038s;
minimum sample 81.554ms. Compile UTF-8 pattern plus first empty Count changed
49.326 to 48.935us (-0.79%); unchanged non-NB control 41.087 to 40.664us (-1.03%).
All spreads were at most 1.058. [Cold allocations](cold-summary.json) increased
51200 to 51328 bytes for the target and 44160 to 44168 for the control. No further
confirmation or variant campaign was run.

The [construction probe](construction-after.jsonl), compared with
[before](construction-before.jsonl), records region states 15, retained modeled
capacity 37344 to 37488 bytes (+144), construction work 2913 to 2967 (+54), cache
charge +144, and retained ordered conditions 8 to 2. Wide and non-NB modeled costs
are unchanged. Construction thresholds can therefore move; meaningful admission
before allocation remains. Invocation working/result/depth limits are unchanged.

[Focused Release checks](verification.json) pass on both runtimes. Six additional
public conditional-chain cases cover nullable captures, anchors and word/nonword
boundaries. All 371 complete production table/search probes match byte-for-byte,
including every viability bit, numeric invocation work, ranges/captures, storage
peaks and enclosing 13-byte loan refunds. The initial adapter build exposed one
missing required internal argument; the corrected PythonRe FullMatch bridge and
its focused tests pass. The failed build log is preserved in raw evidence.

No package or complete catalog qualification was repeated. Published/staged bytes
remain immutable; the older dev.2 boundary does not qualify current source.

- [Raw comparisons, harnesses, patches and frozen binaries](../../../artifacts/experiments/nb-epsilon-closure-20261101/evidence.tar.gz):
  `1152658368090B795CCB0091B0C0C6E232E3E321DF42ED02FBEB9082D7374936`.
- [Correctness logs and source](../../../artifacts/experiments/nb-epsilon-closure-20261101/correctness-source.zip):
  `8C38CA96E47D343898CDDEB892369AFDB7C572BD739CF6675A7D0D2317A48BB0`.

The producer patch changes two Core files and the required empty-ancestry argument
at PythonRe's non-NB FullMatch bridge. The initial predeclaration's two-file wording
omitted that bridge argument; its failed build and correction remain archived.
