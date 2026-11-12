# Share failure-result construction in the existing VM

Accept the one-method change on b041ce21. RunSegment now returns its failure
result through one existing-style exit, retaining the original breach expression
at each branch. It replaces 24 duplicate construction sites; the method has two
constructor sites in total. No new type, helper, mode, owner, allocation or
accounting policy. Matching/SIMD, every original instruction/byte/scalar work
charge, original token/deadline/hooks, first denial, captures/rollback, RTL/NB,
admission/refunds and result/cursor lifetimes retain their contracts.

The initial read-only IL audit identified 25 constructor sites, not their native
cost. Inner results' two history fields are null, but they remain live at outer
history-publication/NB roots; those fields and histories are preserved. A fresh
actual-public FindAllToStrings codegen gate, with complete 2048-tuple checks and
the same caller, measures the current baseline rather than borrowing older code
sizes. Runtime .NET11.0.0, normal eight-CPU startup then CPU2, default tiering/PGO.

| Final Tier1 RunSegment | Before | After |
|---|---:|---:|
| Native code |11266B|10175B|
| Stack subtraction |1432B|872B|
| Entry clearing loop |1344B|768B|
| IL constructor sites |25|2|

The repeated entry loop clears 576 fewer bytes; stack reservation falls 560B.
IL 4697→4631 bytes while locals increase 76→77. This is a real code-generation
reduction, not a public speedup estimate. Windows/Linux focused Release checks
pass per .NET10/.NET11 target: Core 1608, Python 1225, PCRE2 180 and POSIX 145. Existing
semantic/resource/alias/interruption tests are unchanged. Core was built/tested
once per platform, then matching binaries were reused; no new annotation tests.

One **132.054-second** fourteen-row, two-policy ordinary-process public ABBA
comparison uses the unchanged complete-result caller without rebuilding it.
There are 560 samples, minimum 65.309ms, maximum 69.687MiB retained result batch.
Per-row warmup, 75ms target/20ms floor; complete results are consumed after timing.
Distinct staged/loaded Core DLL/PDB pairs and common Python/caller/.NET bytes
verify. Both PE/PDB identities and 342 documents per producer verify, including
six generated observations and exactly one changed production source. Windows
raw and Linux LF manifests preserve actual compiler-source identities.

| Public operation | Validate before → after | AssumeValid before → after |
|---|---:|---:|
| Core captured Count |386.818 →388.990µs|395.448 →376.693µs|
| Forward ordinal references |51.856 →46.726µs|47.740 →45.404µs|
| RTL ordinal references |127.907 →121.285µs|129.145 →122.912µs|
| Folded references, unequal UTF8 widths |125.236 →109.999µs|127.489 →109.946µs|
| Python many-capture strings |646.535 →592.100µs|631.720 →613.441µs|
| Python one-capture strings |222.170 →212.702µs|219.769 →204.133µs|
| Detailed durable cursor |1051.470 →1163.632µs|1139.238 →1168.763µs|

RTL −5.18/−4.83% and folded −12.17/−13.76% have disjoint ranges. Trusted captured
Count −4.74% and forward ordinal −4.89% are also disjoint. Other table ranges overlap,
including adverse strict cursor +10.67% and trusted +2.59%. Python many −8.42/−2.89%,
one −4.26/−7.11%, UTF8 −2.72/−5.86%, finite −6.80/−6.61%, funded −1.45/−3.24% and
folded strings −5.56/−5.70% remain unresolved. Preserve Split +.70/−10.65%, strict
Core captured +.56%, forward −9.89%, unchanged Core −1.67/+3.89% and .NET −1.25/−.29%,
all overlapping. No stable Python-output gain, normalization, composed recovery
of earlier costs or blanket 10% veto. [All observations](cost-summary.json).

Allocations are unchanged for every row. Modeled shared peak 357073B/held 45425B
and original 17B parent refund remain, final loans 0; this is not total heap/RSS.
No confirmation, source tuning, new caller build, package or Full/catalog refresh.
All 452 immutable staged assets, including qualified dev.74, remain unchanged.

Baseline is the exact qualified dev.74 Linux Full Core/Python pairs at 7e6f00e6;
their production CSharp is unchanged through b041ce21. The initial private analysis
label still named dev.73/EAE. Loaded pair versions/hashes and PDB/source checks
used dev.74 throughout; only that stale descriptive label was corrected. Raw
archives preserve the initial label, and no timing/checks were rerun to change it.

Canonical hash-verified offVM artifacts/experiments/shared-vm-failure-exit-20261112:

- Gate/source:B24FBB2B479D45956D0939605F9ECF093F196DB81FB12FC09014BD0DE6A9986D
- Public comparison:7F67E2068F9515B493B1716B702856FF8A6C6371A2F47BCF424CD4AD8E11D100
- Windows:2DE7470E9EAA0868D618282578544ECCA99EF42D3734F9FA6F49D33714B5FAB1

All workers are terminal and keepers released. Keep broader warm Python/Core and
POSIX history costs open; another constructor rewrite needs its own live-path
evidence rather than assuming that source-site counts predict performance.
