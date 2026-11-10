# Current captured FindAll CPU after checked span repetition

One15s public FindAllToStrings capture on accepted source16bd8f69, policy
AssumeValid (valid unchanged UTF8), quiet.NET11 RC3 VM. Normal8CPU startup then
CPU2 affinity. Existing18432-byte input/2048 two-capture tuples; batches store64
full results and verify every tuple outside the batch. Warm1566 calls, then20736
calls over15009.615ms. This is attribution, not a timing comparison or speedup.

| Containing method | Whole-process self samples |
|---|---:|
| Core RunSegment |17.04%|
| Core RunClassRepeat |12.38%|
| Python MoveNextCore |8.50%|
| Core TryRunClassRepeatWithoutRewind |7.07%|
| Core result-ledger DecodeUtf8 |5.91%|
| Python Checkpoint |3.99%|
| Python SeekCandidate |3.27%|
| Core ChargeResultBytes |2.49%|
| Core result-ledger AllocateArray |2.32%|
| Python GetText |1.68%|

16877 cpu-clock:u/997Hz samples over16.821s;13261 JIT-map samples,3616 native/
unresolved, zero ambiguous non-stub ranges. libcoreclr accounts for16.22% self
samples and libclrjit2.06%. Include startup, warmup, checking, GC and runtime work;
inlined costs belong to their containing method. These are not additive phase
times, exact CPU percentages or optimization forecasts. No matched before/after
CPU sample is being subtracted; the prior sample used another producer/policy.

The dense gap remains distributed across general execution, preparation per
match, shaping and runtime work. Do not call the interval-repeat step a solved
FindAll gap or infer a new shortcut from one containing-method share. The existing
closed readonly-binding, loaded-instruction-argument and search-hint-cache trials
remain closed. Further work needs a distinct removable cause or an explicit
architectural disposition; construction duplication can be reviewed independently.

Only scoped perf/JIT-map diagnostics are enabled; kernel/runtime defaults remain
unchanged. Exact accepted A/B-after graph reused without rebuilding libraries:
Core metadata d1b0f50a plus interval patch, matching source16bd8f69; Python is the
accepted anchored-start build. Core DLL F65EB5D4D176E34691D7711DD8C95116411FA4A736841611F81CB4D257EB6F63;
Python DLL8F2948ADCEFF5DFB719542953D106E9DD3895DF3052CDB30E82B25F43A4F2859.
Input SHA DCDD3DF5C16D4EBC22138EE5589DF31722DCADD6C1EEF5BF2A5E1D4EC18E34AA.

Raw perf data, JIT map/dump, graphs/PDBs, caller and scoped script verified offVM:
`artifacts/experiments/python-capture-after-range-20261110/evidence.tar.gz`, SHA256
`4845F8EAF69F554AA9DACE1820224A859123C898063133D816BCF5E70E039C11`.
Full mapping/counts accompany it. No catalog/package/Full repeat or changes to
immutable published/staged artifacts.
