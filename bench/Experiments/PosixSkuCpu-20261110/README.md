# POSIX SKU capture call: current CPU attribution

One15-second actual public Match/captures loop, AssumeValid, .NET11 RC3/quiet VM.
The existing catalog pattern `^([[:alpha:]]+)([0-9]+)$` on `sku007` retains its
FirstAcceptanceIsLongest proof. A4096-call batch reuses caller capture storage,
stores all whole/group values and checks every byte/UTF16 coordinate afterward.
After1331200 warm calls,19120128 calls run in15002.534ms.8CPU startup precedes
CPU2 affinity. This is attribution, not a native ratio or throughput comparison.

| Containing method | Whole-process self samples |
|---|---:|
| POSIX MatchAt |25.55%|
| shared exact-state pool Return |23.06%|
| public POSIX Match |15.37%|
| shared exact-state pool Rent |7.96%|
| frame buffer TryPush |6.27%|
| tag-event buffer TryPush |4.00%|
| mutation buffer TryPush |3.83%|

16640 cpu-clock:u/997Hz samples span16.629s; JIT-map addresses resolve15703,
937 native/unresolved, zero ambiguous method ranges. Startup/warmup, result
recording/checks, JIT/runtime and GC remain included. Inlined work belongs to
containing methods. Percentages are not exact phase times or predicted gains.

Pool returns scan64 entries for identity duplicates, per-type byte totals and an
empty slot, then may evict through a clock. Rentals also scan for exact type/
capacity. This motivates investigation of simpler indexed replacement and
conservative retention caps, accepting earlier eviction. Preserve exact-array
types, cleared reference payloads, exclusive active loans, original working
admission, finite calls' inability to create retained roots, and global/per-type
retention bounds. No pool/source change is implied by sampled attribution.
Avoid a new cache framework or another ownership/lifetime mechanism.

The interpreter already omits undo entries when no frame can need them. Existing
capture-origin reuse, no-history whole-only execution and winning-trace transfer
also remain. Do not reimplement them or add another grammar proof based on this
SKU control. Profile-specific execution/semantic work remains independently open.

Core DLL `E82D9F8CE47E9E75C5076B571838D4D1D55B763C7FA0185E4F55DEEA15BB6114`,
POSIX `FF1F3F2AD3EAA7E23F596A8804DCA80F858E067908815F54DD3FECC173DEF243`.
Frozen current benchmark graph: Core/POSIX86440ee0, benchmark tool df3fc454;
source checkout8de80c8d contains the same relevant library code. Raw worker,
matching graphs/PDBs, perf data, JIT map/dump, sample report and complete result
checks are hash verified offVM:
`artifacts/experiments/posix-sku-cpu-20261110/evidence.tar.gz`, SHA256
`21634894E119CC04B148F0DAF02F1B92BBD6177676239FBA4B6756D8C2DCB492`.
The complete derived address mapping is alongside it. Capture closed; no repeated
profiling campaign, package/Full run or benchmark-table update follows.
