# Current Core name-family attribution

One 15-second public `Count` CPU capture, compiled / AssumeValid, on the quiet
.NET 11 RC3 VM. No throughput comparison or native-parity claim. Current source
a2a60a44 has unchanged Core code since the frozen 56de0556 producer: Core DLL
AFDEA6155C7EABE48417DDD11C7B16B53CE751A6AF62863ECDF62AF27028B173,
PDB C6958ACA199E1617D4F055EA126E674943703D4F72BF2BC9FD7E518F97275254.

`Tom|Sawyer|Huckleberry|Finn` on the existing 16,013,977-byte corpus returns 2,598
matches, equal to predecoded .NET. Input SHA256
F2AA28234E7A8212C9E009FA9C67D1960D2D063D076765DE46B0FAED5FE44AD8.
The actual prepared route is ExactDirect with no boundary/trailing constraint,
widths 3–11. The short counter and minimum-five-byte primitive decline it.
The family uses one literal per first-byte bucket.

Eight processors visible at startup, then CPU2 affinity. Normal runtime settings,
scoped PerfMapEnabled only, existing cpu-clock:u/997 Hz/JIT-map workflow. After
1.5 seconds warmup, 4,608 calls complete in 15,109.608 ms; results are consumed
after each 32-call batch. The entire capture includes startup and warmup.

16,986 samples over 17.029 seconds: 16,293 managed mapped, 693 unresolved/native,
zero ambiguous non-stub mappings. Flat self samples:

| Containing method | Samples |
|---|---:|
| BCL four-value IndexOfAny |46.41%|
| compiled family Count |41.87%|
| PreparedByteSearch.IndexOf wrapper |6.28%|

Inlined costs belong to their containing method. These are neither isolated
phase timings nor proof that changing a method saves its entire share. The old
d371 trace predates the four/five-byte BCL search change and is historical.

Source inspection found that fused position/length search uses a generic bucket
loop even when the strategy proves one literal per bucket. The existing
position-only path already compares that literal directly. The
[distinct-head consolidation](../DistinctHeadSearch-20261111/README.md) evaluates
sharing that direct loop. Other candidate-filter changes need separate evidence.

Raw driver, binaries/PDBs, perf data/JIT dump/maps and reports are copied off VM
and hash verified in `artifacts/experiments/core-name-current-20261111/`:
`evidence.tar.gz`, SHA256
F0CEC504351A2A2D59FBE908CA753985194465D201891C3154B85B3BB15F8FE2.
The complete CPU summary and public result/producer metadata are alongside.
No production, package or presentation snapshot changed in this capture.
