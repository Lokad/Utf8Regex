# Current multi-capture string CPU attribution

One15-second public FindAllToStrings capture on frozen dev.87/Core/PythonRe
`d7e28bd3`, .NET11GA, normal8CPU startup then existing single-processor placement.
Pattern `([a-z]+)-([0-9]+)`,18432 valid unchanged UTF8 bytes,2048 tuples/4096 strings.
Every output checked after64-call batches. Only private caller built; producer
DLL/PDB/source/member inventory and staged bytes verified unchanged.

17190 cpu-clock:u samples at997Hz over17.155s including startup/warmup/runtime:
RunClassRepeat12.02%, MoveNextCore11.22%, TryRunTerminalCaptureRoot9.30%,
DecodeUtf88.81%, FindAllScalarStrings6.60%, SeekCandidate4.14%, result AllocateArray
3.53%. Inlined work belongs to its containing method. Unresolved CoreCLR22.32%
is not a GC attribution. [Full sampled methods and producer identities](cpu-summary.json).

This identifies shared repeat traversal, match progression and complete-root
orchestration as substantial remaining work; it does not prove a new optimization
or exact phase durations. String-only results already omit coordinate maps and
subject validation. Do not reopen those removals or add decoder/cache/root-shape
variants merely from this capture. Assess a concrete simplification of the live
matching/capture path before another implementation/timing experiment.

No throughput, parity, allocation delta, package/Full refresh or performance gain
claimed. Canonical many-capture strings retain the separate1.997 ratio. Raw
verified offVM before keeper release at `artifacts/experiments/python-capture-output-cpu-20261114/evidence.tar.gz`,
SHA256 `2CC1943B8A18289D4598855BCC70FC5D34D7DB91B9499E67D81218D6E370B340`. CPU worker and SSH keeper terminal0.
