# Python Unicode Split: CPU attribution

Actual `[,，]` Split on the3200B catalog subject returns385strings. Use the exact
restored0dc05633 Core/Python DLL/PDBs on clean receipt-only79d9256c, .NET11 RC3,
normal8CPU startup thenCPU2. Warm1.5s; public calls fill a64-reference batch and
complete output digests run outside it. No product instrumentation, source change,
optimization comparison, public speedup or new catalog Status. Frozen graph hashes
remain unchanged. Whole-subject scalar validation and all controls stay in place.

The first bounded EventPipe capture uses dotnet-trace10.0.750501's managed sampled
thread-time profile. Its15s batch makes116160calls. [Weighted stack intervals](stack-summary.json)
show88.32% inclusive Thread.PollGC, and CPU_TIME pseudo-leaves dominate exclusive
weight. These are not reliable CPU attribution: do not infer that result staging
is expensive from its apparent79% inclusive share. Inclusive shares overlap.

ONE30s-capped kernel cpu-clock:u/997Hz capture resolves that ambiguity, using
the SAME worker and producers,15009.6ms batch/120192calls. Existing sudo permits
process-scoped recording; kernel settings are unchanged. [Flat CPU samples](cpu-summary.json)
contain16884samples over16.805s, zero lost. JIT-map lookup resolves15607samples
with zero ambiguous method ranges;1277native/unresolved samples remain.
Whole-process coverage includes startup, warmup, checksums, GC and runtime work;
inlined work belongs to its containing method. Percentages are sampled attribution,
not exact CPU times or additive phase savings.

| Containing method | Whole-process self samples |
| --- | ---: |
| Core RunSegment, optimized Tier1 |34.90%|
| Core RunFrom, optimized |14.61%|
| Python MoveNextCore, optimized Tier1 |13.59%|
| Python Checkpoint |5.44%|
| UTF8 metadata validation helper |4.19%|
| Core DecodeUtf8 |3.56%|
| Python GetText |2.36%|
| Core CanRunTerminalClassRepeat |1.88%|
| Python AddResult |1.75%|
| Core result staging TryPush |1.14%|

The dominant demonstrated direction is general scalar VM dispatch/orchestration,
not result-buffer insertion or the two closed scalar-local reductions. Inspect
whether assertion-free, history-free root attempts can avoid RunFrom's unused
continuation orchestration while executing the SAME RunSegment with the same
workspace, admission, checkpoints and publication. This is a hypothesis, not a
qualified fix; no new runner/hint/mode/owner or a repeat of native class rendering.

Static JIT-dump decoding reused the benchmark's existing Iced.dll after objdump
was unavailable. Not all sampled IPs align with recorded instruction starts, so
do not infer instruction-level causes from those offsets. Preserve that inspection
as raw evidence; no further capture or tooling campaign. A root-driver experiment
needs its own focused semantic/resource checks and one bounded public A/B.

Immutable artifacts/experiments/python-split-profile-20261105:

- evidence.tar.gz (managed trace/worker/graphs): `5500D42999F7C62667E3D927C3AF90738760DE5D3B591DC33C31DB70284D9070`.
- cpu-evidence-complete.tar.gz (kernel data/samples/JIT map/dump): `57FE1C8C92AF8D5249BFE0B69A8C39D6865D977D6EEBA1DEEE779625BFDA6C96`.
- analysis-evidence.tar.gz (mapping algorithm, derived summary/static inspection): `BF6B91E9E683D881EB969F8C780CDF8D0D057C25EE57BB682E0C100F601D72E3`.

The CPU workload completed before export encountered root-owned perf-data
permissions. The initial partial export is preserved; sudo tar exported the same
data plus symbols without changing ownership or replaying the workload. Analysis
and static inspection are archived separately. Packages, Full snapshots and
producer source remain unchanged; stop profiling and investigate the live cause.
