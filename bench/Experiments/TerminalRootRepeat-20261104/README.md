# Reuse complete-root repetition without counter vectors

**Accepted**, source `29039300`. The existing complete-root repetition proof now
admits nullable and unfolded literal atoms. It reuses the scalar repeat runner
and avoids two unused counter vectors. Count's positive-minimum accumulator stays
separate; nullable roots retain their caller's empty-match progression. A required
nonempty retry rejects an empty longest endpoint without advancing the caller.
No runner, retained state, API, mode or unchecked operation was added.

Strict whole-subject UTF-8, scalar boundaries, captures, RTL/NonBacktracking,
retained aliases/results, original cancellation/deadlines and useful limits remain.
Required major buffers still need admission. Version-dependent work/working
requirements can decrease; a scratch-free Count can fit a one-byte working limit.
Result admission and real capture/rewind storage remain enforced. The shared
literal-atom extension `9f2d0c46` already existed in the baseline; its earlier gains
are not attributed to this change.

One **94.9303522s**, 19-case public comparison used the frozen dev.22 Full graph
`60491532`. Only Core changed; all six companion PythonRe/PCRE2/benchmark DLL/PDB
bytes stayed identical. Normal eight-CPU startup then CPU2; existing1.5s/min128
managed and250ms/min64 comparator warmup, independent60ms calibration, five
alternating samples and complete result consumption after timing. All370 samples
qualified, minimum23.5906ms. No confirmation or implementation variant.

| Public operation | Before us | After us | Change | Allocated B before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual nullable FindAll strings |112.49|71.72|-36.24%|16528/16464|
| Finite limits/live token |147.63|100.98|-31.60%|20728/20664|
| Deadline/live token |212.12|165.08|-22.18%|20776/20712|
| Mixed supplementary scalars |92.83|62.94|-32.20%|11384/11320|
| Bounded nullable repeat |68.00|45.65|-32.86%|14456/14392|
| Actual empty-match replacement |58.44|38.62|-33.91%|2976/2912|
| Finite Count |100.82|57.21|-43.25%|176/112|
| Unchanged literal control FIRST |8.55|8.53|-0.16%|2112/2112|
| Existing Core class control |5.01|5.00|-0.07%|0/0|
| Core RTL control |33.30|34.47|+3.52%|3552/3552|

RTL's local cost is about1.17us, with tight spreads; overlap+2.03%, lazy+1.15%
and alternative+1.18% are retained. The user's local-cost/major-gain policy supports
promotion. There is no universal10% ceiling or claimed instruction/JIT cause.
All median allocations are unchanged or lower. The historical fold-control label
also exercises an admitted scalar-class root; its32% gain is not an unchanged-path
control. [All observations](cost-summary.json) retain identities, spreads and pairs.

Release suites pass per .NET10/11: Core41,326, differential1,503, PythonRe4,730,
PCRE2 6,507. New public cases cover nullable scalar values and byte ranges, EOF,
bounded maxima and working/result limits. Every work cutoff preserves enclosing
loans; original cancellation/deadlines interrupt scalar discovery. Existing capture,
alias, lazy/overlap/assertion and NB/RTL coverage remains.

Two test witnesses were corrected, without changing the runtime between failed
and corrected runs: the new empty-coordinate helper must advance one scalar, and
the injected wide-counter test needs an end anchor to remain on the general VM
counter path. Initial logs and prior/final patches are preserved. No billions-sized
input, test-only execution path or runtime switch was introduced.

Only three existing CPython rows were refreshed/paired, then two generated files
hash-imported. The other87 rows and catalog/corpus/lifecycle/scaling context are
unchanged; matching-binary snapshot checks pass once per target without rebuilding.
[Selected observations](selected-ratios.json): FindAll3.122 ->1.884x [1.881,1.891],
83.08us/16464B; replacement2.926 ->1.644x [1.634,1.651],38.99us/2912B. Both remain
CpythonFaster. Count1.176 ->0.518x is **Inconclusive**: comparator interquartile
spread1.140 exceeds1.10. Preserve that observation without another refresh. These
catalog observations are separate from the matched public before/after comparison.

[Dev.22 packages and42m48s Full](../DeliveryDev22-20261104/README.md) remain immutable
and predate this source improvement. No per-pilot package/full matrix or reopened
Chinese-fusion/Unicode-class campaign. Further delivery qualification is batched.

Immutable evidence under artifacts/experiments/terminal-root-repeat-20261104/:

- evidence.tar.gz: `5E5E1A2B76C8D27CD8C4EBBDAB7CC7CBCA8DD46F7B59FB09334924F8BAA4F419`.
- correctness-source.zip: `FDEFB199A79089F5E54967D4F5C597A12AB3427B51D97B1809634E1DD8C6B3CA`.
- selective-evidence.tar.gz: `5BFEB64ADB6536B3896A99D4B88C4DAD1DEE4F7724E923C1CF0EAB6F725EE70E`.
- import-guards.zip: `88A54CEBD749E3075AF3249F739401628D421A80E1E14C9CA71D92456A89A172`.
