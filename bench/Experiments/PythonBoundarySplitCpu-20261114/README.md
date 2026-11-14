# Python boundary-only split CPU attribution

One15-second public SplitToStrings capture, Core1f2b34c1/Python dev91 cd8eb5c7,
.NET11GA. Canonical ASCII `\b` input:2176 valid unchanged UTF-8 bytes,768 boundaries,
769 strings. Complete values checked after64-call batches. Only a private caller
built; producer DLL/PDB/source/member inventory and fixed images verify unchanged.

16992 cpu-clock:u samples at997Hz over16.952s including startup/warmup/runtime:
MoveNextCore29.04%, RunSegment17.33%, DecodeUtf88.63%, SplitScalar7.22%.
[All method self samples and producer identities](cpu-summary.json).
Inlined work belongs to its containing method. Unresolved CoreCLR16.24% is not a
GC attribution. No exact phase fraction, throughput, parity or speedup is claimed.

This distinct workload identifies progression and repeated VM entry as a material
remaining cost. Inspect a small complete-boundary-root simplification using the
existing predicate and invocation, preserving initial scratch admission, original
interruption and basic metering. Do not infer another decoder/validation campaign:
BCL UTF8 decoder samples do not show a whole-subject validation pass.

No producer/package/Full refresh. Raw archived/copied/hashed offVM before keeper
release at `artifacts/experiments/python-boundary-split-cpu-20261114/evidence.tar.gz`,
SHA256 `84CDE862DD61A48D40B78B6488DB24BD6D34548FC357229F6B37B77F905F0997`. CPU worker/collector/keeper terminal0. dev91 and every staged
artifact retain their original identities. This bounded attribution is closed.
