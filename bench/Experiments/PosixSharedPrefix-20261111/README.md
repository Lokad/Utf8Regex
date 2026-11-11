# Skip shared POSIX history prefixes

Accepted POSIX-only change on baseline `de832ba9`. Saving a best history proves
an exact shared event prefix; rollback can only shorten it and appending preserves
it. The existing first-root-child comparison starts after that prefix. Its first
closed occurrence is identified during the existing copy, preserving participation
and extra-empty-tail ranking. Two local integers replace repeated equal-prefix
scans; no fields, arrays, ranks, cache, execution mode or new pruning rule.

Traversal, hierarchical captures, strict/AssumeValid input contracts, byte/UTF-16
coordinates, basic metering, original token/deadline, admission and cursor/result
ownership remain. Removed comparison work lowers numerical work requirements;
SPEC-POSIX records the scope. Unresolved parents and opaque non-EOF consumers can
still require exponential competition. No general polynomial/native-parity claim.

One **112.409s** quiet .NET11 RC3 VM ordinary-process ABBA per input policy:
14 public rows, 560 samples, minimum34.524ms,75ms calibration target,150s cap.
One caller and common Core, explicit distinct POSIX DLL/PDB pairs, eight CPUs
before CPU2 affinity. Complete results, loaded hashes and all sample checksums
verify independently after timing. No confirmation or tuning variant.

| Complete public operation | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Captured Match, ASCII256 at EOF |100.108 →46.255|127.292 →26.093|
| Captured Match, ASCII512 at EOF |354.816 →40.973|354.975 →41.782|
| Captured Match, ASCII1024 at EOF |1332.285 →79.088|1327.562 →81.136|
| Captured Match, ASCII1024 before `!` |1332.119 →79.754|1325.433 →79.145|
| Captured Match, astral1024 before `!` |1346.114 →95.169|1342.054 →91.451|
| ASCII1024, finite/live token |1566.502 →90.990|1566.618 →89.129|
| ASCII1024, funded/deadline/live |1756.740 →100.226|1723.513 →100.983|
| ASCII1024, complete funded cursor |1576.515 →92.557|1572.210 →91.456|

All target ranges are disjoint,53.8–94.3% reductions. Targets retain their exact
allocations: ordinary1024=172368B, finite196768B, funded/deadline196816B and
cursor197072B. Shared-account peak110833B includes an unchanged17B parent loan;
final loans are zero. Retain the256-case process variation rather than infer an
exact scaling curve from these selected observations.

All control ranges overlap. Wide non-EOF history remains62.457→62.763µs strict
and62.506→62.807µs trusted (+0.49/+0.48%). Unchanged Core moves+2.57/−3.35%; .NET
−0.83/+0.27%. Short history−11.54/−12.16%, SKU−2.11/−3.11% and whole-only
−2.21/−1.00% are uncertain. No credit or normalization from those controls.
SKU retains small candidate pool-fill observations96.057/96.013B alongside96B;
do not describe every observed allocation value as exactly unchanged.

Full Release POSIX **288 checks per target** pass Windows/Linux, .NET10/11;
Linux also passes288/target without hardware intrinsics. Independent occurrence
oracles, parent/whole counterexamples, nullable/absent/redundant tails, floors,
ASCII/astral coordinates, shorter capture output, retained values/cursor aliases,
strict malformed tails, cancellation/deadlines and refunds remain covered.
Eight new1024-scalar100k-work witnesses fail frozen baseline at100001/100000
and pass the restored candidate on both Windows targets. The initial two
interruption fixtures expected a64-step hook inside the now-short comparison;
they instead observe real captured-history rollback, retaining original-token/
deadline, cleared output, admitted storage and complete refund assertions.

Raw/source/pairs/Linux evidence is verified offVM under
artifacts/experiments/posix-shared-prefix-20261111:

- `evidence.tar.gz`: `E1BD7BBC6B076125D2BB02B0854FDE7B23234618BD84176108E3EAD3B4013988`.
- `windows-evidence.zip`: `D4B8891C3D611B3A410F10C5C02359D6477334241F94753E60DB6DA64A1042BE`.

Full summaries and independent verification accompany those bytes. Immutable
dev.70 packages and generated Full tables predate this change; no package/catalog/
Full repeat. Warm Python, strict Core and general POSIX history gaps remain active.
