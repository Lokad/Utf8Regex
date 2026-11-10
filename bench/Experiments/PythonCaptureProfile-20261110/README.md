# Python captured FindAll: production CPU sample

One15s actual public `FindAllToStrings` batch, inside a30s cpu-clock:u/997Hz
capture on the quiet .NET11 RC3 VM. Current clean90d18c2f producers, normal8CPU
startup then affinity4; no runtime settings, kernel settings or producer code
change. Warm1495 calls, then19840 calls in15024.621ms. Each64-call batch only
stores results; complete2048 two-capture tuples are checked outside the batch.
The18432-byte catalog input SHA256 is
`DCDD3DF5C16D4EBC22138EE5589DF31722DCADD6C1EEF5BF2A5E1D4EC18E34AA`.

| Containing method | Whole-process self samples |
|---|---:|
| Core RunSegment, Tier1 |19.04%|
| Core RunClassRepeat, Tier1 |11.68%|
| Python MoveNextCore, Tier1 |9.23%|
| Core TryRunClassRepeatWithoutRewind |7.15%|
| Core result-ledger DecodeUtf8, Tier1 |5.55%|
| Python Checkpoint, Tier1 |4.48%|
| Python SeekCandidate, Tier1 |3.08%|
| Core result-ledger AllocateArray, Tier1 |2.21%|
| Python GetText, Tier1 |1.39%|

16887 samples over16.846s; JIT-map resolves13360, with zero ambiguous method
ranges.3527 native/unresolved samples remain, including15.85% in libcoreclr.
Coverage includes startup, warmup, checksums, GC and runtime work. Inlined work
belongs to its containing method. Shares are sampled attribution, not exact CPU
times, public performance gains, additive phases or predicted optimization savings.

This directs investigation toward existing VM/class-repeat work. Capture lookup
already indexes slots directly and ASCII string shaping needs no coordinate map.
Old FindAll phase discovery/range controls use BCL/replay and cannot establish
this production attribution. No repeat of those phase timings is needed.
N27 post-decoding checkpoint and rejected RunSegment readonly-binding trials stay
closed. A distinct bounded candidate can pass already-loaded immutable repeat/atom
instructions into the existing repeat helper, avoiding its duplicate array reads;
only a public comparison can establish whether that is useful.

Core DLL `43DC7D1A390BDE1FA6A37A20A77FA17AD2B66CAC11CDA3B3D7C994E48F227BF1`;
Python DLL `DF38E39FA76011B9D9A6FC0D6F635FF9814E6F1BEE8D7F3B8DAABFC5C740DF94`.
Raw perf data, worker/graphs/PDBs, JIT map/dump, full counts and capture protocol
verified offVM: `artifacts/experiments/python-capture-profile-20261110/evidence.tar.gz`,
SHA256 `B19DD5145E496332AD364C24FD3AA4208D8970A4709E781737B7B584AAF07F30`.
Derived complete mapping/analysis is alongside it. No speedup/status claim,
profiling campaign, package/catalog/Full repeat or immutable-package change.
