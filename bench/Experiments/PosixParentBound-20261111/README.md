# Compare POSIX descendant histories after fixed parent boundaries

Accepted POSIX-only change against `7f9ff590`. A bounded proof over at most
64 instructions recognizes leading once-entered enclosures, confined body edges,
and a suffix containing only fixed literals and capture markers. At a maximal
whole endpoint those earlier capture priorities have fixed starts and ends, so
the existing losing-prefix comparison can consider their first unresolved child.
Variable/branched tails and unproved scopes retain the root-child comparison.

One local tag and the existing prefix/first-close facts suffice: no retained
metadata, storage, cache, rank, executor or public option. Hierarchical scalar
captures, strict/AssumeValid contracts, coordinates, cancellation/deadlines,
admission and cursor/result ownership remain. Removed exploration lowers work
totals and can lower workspace demand; SPEC-POSIX records that scope. General
unresolved-parent competition can still be exponential.

One **113.596s** quiet .NET11 RC3 VM ordinary-process ABBA per input policy:
14 public rows, 560 samples, minimum59.308ms,75ms calibration target,150s cap.
Common Core/caller, explicit distinct POSIX DLL/PDB pairs, eight startup CPUs then
CPU2 affinity. Staged and loaded hashes, input identities, complete results and
all sample checksums independently verified. No confirmation or tuning variant.

| Complete public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Captured fixed-tail Match, ASCII12 |109.196 →1.599|109.576 →1.562|
| Captured fixed-tail Match, ASCII16 |824.224 →2.060|829.156 →2.010|
| Nested-parent fixed tail, ASCII16 |820.978 →2.075|827.254 →2.035|
| Class-alternative fixed tail, ASCII12 |128.917 →1.634|129.778 →1.597|
| Astral fixed tail, 12 scalars |111.021 →1.804|112.035 →1.712|
| ASCII16, finite/live token |1003.188 →2.670|983.240 →2.638|
| ASCII16, funded/deadline/live |1040.067 →3.459|1051.997 →3.312|
| ASCII16, complete funded cursor |1003.240 →3.344|983.874 →3.302|

All target ranges are disjoint,98.37–99.76% reductions. Ordinary owned capture
results remain64B. Finite, funded/deadline and cursor allocations fall by1328B:
4000→2672,4048→2720 and4304→2976B. Retain the strict finite pool-fill observation
2672.000276B alongside2672B. Shared-account peak falls2689→1985B, including an
unchanged17B parent loan; outstanding loans return to zero.

Two adverse controls have disjoint ranges: strict variable-tail Match rises
121.433→130.342µs (+7.34%), and trusted existing1024-history Match rises
78.977→82.027µs (+3.86%). Accept those local costs for the target gains; neither
normalization nor a claim of noise removes them. Their other-policy ranges
overlap (+5.29% variable-tail trusted; +0.54% existing-history strict).
SKU−1.64/+2.37%, whole-only−1.93/+2.89%, unchanged Core+0.32/−2.86% and .NET
+1.07/+1.97% also overlap. Preserve small pool-fill observations on SKU/whole-only
in the raw summary. No generic native-parity or composed speedup claim.

Full Release POSIX **311 checks per target** pass Windows/Linux, .NET10/11;
Linux also passes311/target without hardware intrinsics. Independent occurrence
oracles cover nullable/absent/parent/whole/variable-tail cases, nested parents
with different scalar closing coordinates, short/large programs, byte floors,
ASCII/BMP/astral, short capture output and retained cursor aliases/results.
Original-token cancellation/deadlines during live history rollback, cleared
outputs and denial/refund at each working acquisition remain covered.
Ten new32-scalar100k-work cases fail the frozen baseline at100001/100000 and
pass the restored candidate on both Windows targets, establishing live admission
benefit independently of timing.

Verified canonical offVM evidence under
artifacts/experiments/posix-parent-bound-20261111:

- `evidence.tar.gz`: `7C9581D576877B3E9A712B6FBB38F00CE15CFB5EF17C0607B38C0B752F3DA12D`.
- `windows-evidence.zip`: `BFF2676408A0BACC7A2B6B92E0D8F06AE7EDF5ABCE760D566E5095206D6D0605`.

Full summaries, independent verification and the initial untimed refusal/bytecode
probe accompany those bytes. Runner19844 terminated successfully. Immutable
dev.70 packages and generated Full tables remain unchanged; no package/catalog/
Full repeat. Warm Python, strict Core and broader POSIX history work remain open.
