# Repeated literal capture selection

Accepted after `56831cdf`. The nullable `(a?){2,4}` capture row was the largest
qualified dev64 ratio. Extend the existing first-acceptance construction proof
to a root repetition of one capture containing only a greedy uniform literal
run, with inner minimum zero/one and a bounded or positive-minimum outer repeat.
There is no new executor, state, buffer, mode or change to history ranking.

These bounds make feasible total lengths contiguous: first greedy success
maximizes the whole endpoint, then occurrence lengths left to right, reserving
mandatory minima. Existing empty-repeat pruning retains required empty tails.
The proof inspects one exact twelve-instruction shape and charges twelve
construction units. It rejects larger inner minima, minimum-zero unbounded
outer repeats, nested captures, alternatives and consuming suffixes. In
particular, `(a{3,4})+` on six a's needs a different partition to reach the longest
endpoint; `(a?)*` needs general ranking to discard its redundant empty tail.

One **86.851s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s:
22 public lanes,440 samples, minimum57.396ms. Reuse the existing POSIX worker,
with actual nullable proof engagement, finite/funded/deadline lanes, a repeated
star family member and both counterexamples. Explicit producer DLL/PDB pairs,
distinct POSIX hashes and identical Core/caller precede timing; one producer
loads in each default context. Full result tuples and zero loans are checked
outside timing. The baseline reuses the frozen dev64 Full graph; candidate
POSIX references the compatible current Core API, while timing retains the
same frozen Core bytes. No confirmation, native retiming, package or Full repeat.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Nullable capture, finite |1.371|0.968|−29.4%|
| Nullable capture, funded |1.535|1.135|−26.1%|
| Nullable capture, deadline |1.653|1.204|−27.1%|
| Repeated star capture |3.370|0.753|−77.7%|
| Minimum-three counterexample |1.277|1.264|−1.0%|
| Unbounded-zero counterexample |0.936|0.958|+2.3%|
| Empty capture, finite control |0.996|0.998|+0.3%|
| Nullable construction |3.810|3.798|−0.3%|
| Unchanged Core Count |0.840|0.841|+0.1%|

The unlimited nullable baseline is bimodal: process medians2.110/1.015µs,
candidate0.675/0.683µs. Its pooled1.561→0.679µs (−56.5%) is retained, **not a
precise causal gain**. The unchanged unlimited empty control similarly moves
1.530/0.712→0.739/0.712µs; attribute neither its pooled−36.2% nor this shared
first-process movement to the proof. Controlled nullable and repeated-star
observations have disjoint before/after ranges. Other controls and adverse
movement remain in the [complete summary](summary.json), without normalization.
All allocations are unchanged; funded nullable peak stays960B and refunds zero.
Catalog/native ratios and status remain frozen until a coherent delivery refresh.

Windows/Linux Release pass224 checks per target (.NET10/11). The thirteen new
baseline cases first pass against the unchanged producer. Their independent
integer-extent/history oracle enumerates bounded/unbounded, mandatory/optional
and zero repeats without consulting instructions or greedy order. Further
checks cover eligibility and explicit declines, BMP/astral/NUL, offsets/tails,
short output and retained values. Existing refusal/cursor tests additionally
exercise this route for work/depth/working admission, every working acquisition,
original-token cancellation, deadlines, cleared output and synchronous refunds.
The full profile also retains independent ERE acceptance/history fixtures.

Untimed actual workspace inspection reduces Find work70→30 on nullable/a,
344→38 on repeated-star/aaa; these are modeled units, not timings. Matching
transitions and history publication still run through existing metering. Strict
whole-input validation, scalar/capture policy, cursor aliases, interruption,
resource admission and synchronous refunds remain. Numerical requirements can
fall across versions; the [contract](../../../docs/SPEC-POSIX.md#interruption-and-admission)
records the restricted proof and existing accounting scope.

Frozen Core `6807BC5DF0F625D173FB9BDC1677275EB24485C8C0142C101F5F226958B6A340`;
POSIX before `6168AB4D7AE3550BCDE420B2748F39E5953FC7B7148A7A146A8FB4F2A256514D`;
after `5C687E9E545E9DE7034E98DD5C5ECCB14773188138DDE415A10AB522CD52A368`.
Worker `62B70F2DD0E3C286F27318CBD927D7BB9A67957534F595A82B25CA6A1D904194`.
Protocol, staged graphs/PDBs, patch, preflights, checks and all samples are
verified off VM at
`artifacts/experiments/posix-repeated-capture-20261110/evidence.tar.gz`, SHA-256
`E24B25409BBF41ADA6A3D0CE98F761B58DD26BCA6B2E6C80EC8390B06E980971`.
Baseline/Windows checks and untimed inspection are alongside the archive.
An initial compile attempted to pass an int length to ulong ChargeWork; it was
corrected to the fixed admitted twelve-unit charge before accepted checks.
Immutable dev64 packages and published artifacts are unchanged.
