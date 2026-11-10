# Transfer the proven winning capture history

Accepted after `fb2ac920`. Every first acceptance already proved to be the winner
still copied its complete event trace into a second buffer immediately before
returning. Exchange the existing trace/best buffer owners at that terminal
acceptance instead. General competition retains its independent selected copy.
There is no new field, pool, execution mode, history representation or ledger.
Event layout, opening indices, rollback and hierarchical comparisons stay intact.

Both owners keep their existing reset/release lifetimes and reservations; the
exchange allocates nothing. Single-call selected histories avoid the duplicate
acquisition and copy. A reused cursor can still retain capacity in both buffers;
no additional reuse optimization was added to this experiment. Strict UTF8,
scalar/capture semantics, retained values, aliases, original interruption and
useful admission/refunds remain. Modeled work and working requirements can fall;
exact old cutoffs are not promised. The
[contract](../../../docs/SPEC-POSIX.md#interruption-and-admission) records this scope.

One **86.901s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s:
22 public lanes,440 samples, minimum58.899ms. Reuse the previous bounded worker
and frozen N41 producer, with identical Core/caller, distinct staged POSIX
DLL/PDB pairs and one loaded producer/default context per process. Preflights
verify actual first-acceptance engagement and lower funded capture peaks; full
tuples and zero loans are checked outside timing. No confirmation, native
retiming, package/catalog/Full repeat or composition with earlier gains.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
|Empty capture|0.730|0.611|-16.3%|
|Nullable capture|0.697|0.584|-16.2%|
|Absent capture|0.553|0.445|-19.5%|
|Nullable, finite|0.984|0.818|-16.9%|
|Nullable, funded|1.137|0.938|-17.5%|
|Nullable, deadline|1.227|1.050|-14.5%|
|SKU,4096 calls|4138.738|3487.414|-15.7%|
|Nested, general history|2.785|2.900|+4.1%|
|History8, general history|13.198|13.978|+5.9%|
|Unchanged Core Count|0.832|0.835|+0.3%|

The affected unlimited and controlled capture observations have disjoint ranges.
Unlimited steady-state allocations stay unchanged because existing pooled
buffers can be retained; finite/funded calls allocate216B less per operation.
Funded empty/nullable peaks fall976→752B and960→736B, respectively, with zero
outstanding loans. Actual GC allocation bytes and modeled working capacity are
different measures. SKU remains an unranked4096-call consumer control.

**Retain the adverse general-history costs:** nested captures+4.1% and history8
+5.9%, both with disjoint observed ranges. These routes still execute general
competition, but the shared dispatch changed; unchanged algorithm is not evidence
of unchanged cost. This trade-off is accepted for the larger capture gains and
lower finite allocation costs, as authorized. No universal10% gate, normalization
or tuning confirmation is introduced. Every lane/process/range is in the
[complete summary](summary.json). Catalog ratios/status stay frozen at dev64.

Windows/Linux Release pass225 checks per .NET10/11 target. A new stateful cursor
case alternates capture/whole windows through aliases across nonempty, required
empty-tail and final-empty matches; retained values survive subsequent reuse and
disposal. Existing independent history/ERE oracles, every capture-span size,
malformed subjects/floors, admission at every acquisition, work/depth/working
refusal, original cancellation/deadline publication and synchronous refunds pass.
No Core/PythonRe/PCRE2 production source changed.

Core `6807BC5DF0F625D173FB9BDC1677275EB24485C8C0142C101F5F226958B6A340`;
POSIX before `5C687E9E545E9DE7034E98DD5C5ECCB14773188138DDE415A10AB522CD52A368`;
after `281BBAA0366E0C9D8BB34DAD8FE25CE223F3413740065EFCAE3F019075A0517E`.
Worker `27B9C9FD5E376B5EEB46A30916996A8BE014787904022161F9E58C9CB567D218`.
Protocol, staged graphs/PDBs, patch, preflights, tests and all samples are verified
off VM at `artifacts/experiments/posix-winning-trace-20261110/evidence.tar.gz`,
SHA-256 `4108F6083D64FDE983B2CA1BA55E5FA9FBC68D4BFD0DF46EE6052D281B452C53`.
Windows checks are alongside the archive. Immutable dev64 packages and historical
published/staged bytes remain unchanged.
Source CI42765476499 passes Windows/Linux for `d64af63c0110379630d1b8244c168107f442826c`.
