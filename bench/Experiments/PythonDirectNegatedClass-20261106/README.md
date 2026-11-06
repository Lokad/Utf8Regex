# Lower ordinary Python complements to scalar classes

**Accepted.** Parent `7323558fbd8dec460e1fafecc880c399436ae925`. One translator
file changes +16/-8 lines. Ordinary representable complements now render directly
as negated classes for the existing owned scalar parser. Empty/full scalar sets
render their direct outcome. Unions containing standalone predicate branches
retain the real complement guard. Existing buffers, construction admission,
folding expansion, scalar matching and result owners remain; no new runtime,
program fact, cache, execution switch or framework.

The prior untimed public Unicode `[^ab]+a` probe found a ten-instruction repeat
body containing Fork/Set/Commit/Fail/Dot. It allocated448160B for a20001-byte hit.
The fixed-Core candidate has six instructions, with one negative Set atom, and
allocates0B. The existing pure-atom fusion can now consume it. These are public
allocation and structural observations, not timing or branch-count evidence.
Ordinary class complements were absent from the qualified Python catalog; these
novel workloads do not rewrite its ratios or claim improvements for other cases.

ONE **88.868332475-second** public comparison uses four separate Default producer
processes in ABBA order, followed by separate .NET diagnostics. Only Python DLL/PDB
differs; Core, PCRE2, benchmark and worker DLL/PDB files are unchanged. Normal
eight-CPU startup then CPU2 affinity, default tiering/PGO, one-second/minimum128
warmup, independent60ms calibration and five samples/row/process. All **300 producer
samples are at least55.1293ms**. Typed results are consumed after timing; preflights and
timed results agree. Total cap120s, per producer35s, diagnostics15s. Conservative
2GiB admission passed immediately at7708548KiB available. No confirmation or tuning.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core native Count |12.47|12.47|+0.00%|0/0|
| Actual Python date miss control |1.56|1.58|+0.98%|0/0|
| Actual named-reference FullMatch control |0.49|0.50|+2.20%|400/400|
| Unicode pair IsMatch |807.02|10.91|-98.65%|448160/0|
| ASCII pair IsMatch |1537.50|1.31|-99.91%|895488/0|
| Unicode pair Count |777.05|119.93|-84.57%|448160/0|
| Unicode pair IsMatch, finite/live |814.65|10.69|-98.69%|448272/112|
| Same, deadline/live |846.69|10.78|-98.73%|448320/160|
| Unicode captured SearchDetailedData |894.93|66.85|-92.53%|530048/81888|
| Unicode single-exclusion IsMatch |808.09|78.55|-90.28%|448160/0|
| Wide-scalar exclusions IsMatch |472.53|340.99|-27.84%|224440/224440|
| Standalone-predicate control |1176.74|1179.10|+0.20%|448160/448160|
| Folded complement IsMatch |849.43|170.26|-79.96%|448160/0|
| Short dense pair Count |1121.86|542.88|-51.61%|1056/0|
| Overlapping-tail rewind IsMatch |795.10|477.60|-39.93%|448160/447688|

[All observations](cost-summary.json) preserve process medians, ranges, allocations
and identities. The target gains are substantial; retain the +0.98% date and
+2.20% named-reference estimates. Date after process medians exceed both before
medians; named-reference and standalone controls have overlapping observations.
No universal10% rule, normalization or broad catalog/profile claim. Controlled
Unicode calls still allocate112B/160B; their overlapping time estimates do not
establish negative control overhead. Count is still materially slower than
IsMatch, and wide/rewind paths retain significant state. Lower per-call allocation
does not imply lower process RSS: producer peaks151456–152932KiB are retained.

Separate ordinary BCL IsMatch diagnostics give Unicode predecoded89.52us/
decode-then100.65us, ASCII95.08/95.11us; allocation0/20024B and0/40024B. They have
equal Boolean results on these inputs. They are not CPython, compiled-.NET or
qualified catalog measurements and do not emulate owned resource controls.

Release .NET10/11: existing Windows PythonRe4817 per target passes; all29 new
focused cases pass. Linux full4846 per target passes. Coverage includes scalar
counts, supplementary/surrogate/empty/full sets, Unicode/ASCII predefined classes,
folding/Kelvin, escaped class punctuation, block tails/offset captures, retained
cursor aliases/results, tiny meaningful capacity limits, malformed suffixes and
original cancellation/deadlines. One initial capacity-witness test incorrectly
also required10000 work steps; its failure is preserved. The corrected witness
retains finite1M work, depth/working/result each1. Production code did not change
during that repair. Construction/capture/storage controls remain enforced.

Fixed Core: `B515BD2DF5E67A96D8FD78F110223F7507F93858CF22B8891142206DBA3B83F9`.
Python before `2E76F402929F563DB7984A8A6690D051EC50B447CD86880283173BB957A1A623`;
after `84FEA93CD687F0C67F067769BE70265825434CA29E6C3F8BE64ABBE95C35860E`.
Runtime Git blob `0bc981072313a74773c61f42ae17e716c5296604` matches both machines.

Immutable `artifacts/experiments/python-direct-negated-class-20261106/`:

- `evidence.tar.gz`: `34250394C0625E0920F533AA0F494225361DE579208CC6C48C367FEB9C8C0480`.
- `windows-evidence.zip`: `60B88F1312950CAC2A10F0256D884319ACE469830BF14A96F8C5FEBF2F0554A1`.

Raw retains exact graphs, source/tests, probes, driver, every observation, process
receipts and terminal zero. All8frozen dev40 Full assets, staged/published packages
and tables remain unchanged. Package/Full qualification stays at a coherent batch
boundary; this focused experiment did not repeat it.
