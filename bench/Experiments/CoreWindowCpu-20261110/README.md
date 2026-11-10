# Current public river Count: CPU attribution

One actual public compiled `Count` capture, **AssumeValid**, Core3dc2e65a.
The unchanged16,013,977-byte Leipzig input returns2 matches through
`native_ordered_literal_window`, with3445 candidates/verifications. Normal8CPU
startup precedes CPU2 affinity; .NET11 RC3 default runtime settings plus perf maps.
One15-second production loop performs5632 calls after503 warm calls.32-call
batches store integers; every result is checked afterward. This is CPU attribution,
not a public throughput comparison, .NET parity result or predicted speedup.

| Containing method | Whole-process self samples |
|---|---:|
| BCL SpanHelpers.IndexOf byte substring, Tier1 |77.60%|
| TryFindLiteralFamilyCore, Tier1 |6.79%|
| PreparedSearcher.TryFindMatch, Tier1 |3.12%|
| public CountCore, Tier1 |3.01%|
| window TryMatchAt, Tier1 |2.99%|
| trailing LastIndexOfExactAnchored, Tier1 |0.65%|

One16952-sample cpu-clock:u/997Hz capture spans16.941s. JIT-map addresses resolve
16265 samples,687 native/unresolved, zero ambiguous method ranges. Startup,
corpus decompression, warmup, checksums, JIT/runtime and GC remain included.
Inlined costs belong to containing methods. Flat percentages are neither exact
phase times nor additive predictions for an optimization.

This prioritizes existing pair/small-family candidate discovery. The live raw
pair route performs bounded complete-needle scans for each branch. Investigate
reuse of existing prepared family search inside those bounded windows, preserving
ordered verification, overlaps, byte/scalar boundaries and invocation metering.
Do not infer a scalar-window bottleneck or reopen the rejected dispatch,
class-repeat argument-flow, readonly-binding or cached Python-hint trials.
No library source, packaged bytes, ordinary catalog or generated README changes.

Core DLL `C3BFA249E9A408C4B6016B780A5F688106CE614C5F7D71209D81AA165E4B716D`,
PDB `2015DEBDF2D1E1A2E6BB3B3E6116E74DEB5FA7CCAE7FFB49A1A1DC752BBACBE2`.
Raw perf data, actual caller, binaries/PDBs, route proof, JIT map/dump and complete
sample report are retained/hash verified offVM:
`artifacts/experiments/core-window-cpu-20261110/evidence.tar.gz`, SHA256
`196950348DD75760BADB714AB8983E61C7AD57EBE48C25F041079C08AA8F4B04`.
The full derived address mapping is alongside it. Capture closed; no repeated
profile or benchmark campaign follows without a distinct supported change.
