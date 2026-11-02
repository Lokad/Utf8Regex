# Public scalar CPU attribution

Read-only investigation of original `a40bda03` and canonical `05c6fac7`
(runtime changes `b757befb`). Four separate .NET 11 RC3 quiet-VM processes
checked complete public UTF-16 range digests throughout a two-second warmup and
the sampled loop. Normal runtime startup precedes CPU2 affinity. One eight-second
`cpu-clock:u` recording at 199 Hz per process completed under the predeclared
60-second batch cap. Hardware instruction sampling was unsupported; no kernel
policy was changed. These are CPU shares, **not throughput comparisons**.

| Public operation | Original samples | Current samples | Original RunSegment self | Current RunSegment self |
| --- | ---: | ---: | ---: | ---: |
| PythonRe FullMatch `(?:ab)+c`, 2049 bytes |1590|1590|93.02%|93.77%|
| PythonRe Search `(?>a?)b`, `xxb` |1586|1588|39.91%|35.52%|

The suffixed workload is dominated by the shared scalar VM in both versions.
The atomic workload also spends material time in runtime, state construction,
search setup and chunked stack operations. Sampling does not identify the cause
of an incremental regression or justify removing admission or basic metering.

Actual final optimized Tier1 `RunSegment` bytes were extracted from each captured
process's JIT dump, then decoded offline with the existing benchmark-private Iced
assembly. Current code is smaller: suffix **13282 vs 13696 bytes**; atomic
**17976 vs 19812 bytes**. Workload-specific PGO and timer sample locations do not
prove that any single source operation became more expensive. Previously rejected
caller-layout, instruction-reference and repeat-root experiments remain closed.

Initial copied perf maps were buffered and incomplete. Finalized maps copied
after those same processes exited resolved the saved absolute instruction
addresses offline; no profiling process was repeated. Stub JIT records can omit
machine bytes: extraction validates the selected RunSegment records only. Early
maps and intermediate analysis remain preserved alongside the final maps.

Raw source/protocol, exact DLL/PDB sets and hashes, checked results, perf data,
maps, stacks, JIT dumps, extracted code, decoder and summaries:
`artifacts/experiments/python-scalar-cpu-20261102/evidence.tar.gz`, SHA256
`C471D0FC988E9E13713AB82F94175A9EC3B244C30ABA8D2A5AA2B150BD965B93`.
No product edit, performance acceptance, package qualification or snapshot update.
