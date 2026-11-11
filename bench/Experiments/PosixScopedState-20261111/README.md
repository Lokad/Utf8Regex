# Borrow scoped POSIX state spans

**Accepted.** Synchronous IsMatch/Match/Count borrow checked state spans: up to128
integers use bounded stack storage, larger vectors use exact heap arrays. The
sealed cursor explicitly owns its durable exact array. PosixWorkspace retains
the same interpreter, history buffers and conservative state-capacity reservation;
projection copies coordinates before state leaves scope. No new engine, pool,
eligibility flag, public option or owner framework. Capture priority, input policy,
original interruption, admission and cursor/result lifetimes remain.

[The contract](../../../docs/SPEC-POSIX.md) keeps the same ArrayBytes charge and
work step before use, including stack storage; refunds remain synchronous. This
removes allocation without relaxing numerical limits. It differs from the rejected
[exact-pool trial](../PosixStateReuse-20261110/README.md), whose selected capacities
were ineligible. No bucket rounding or replay of that trial is involved.

A current immutable-package audit found warm captured Match allocations48–96B
across seven selection cases under both input policies. The identical untimed
caller, now swapping only POSIX, proves all14 observations become0B with the same
captures/coordinates on Windows/Linux before timing. The first boundary fixture
assumed an extra root tag; actual nested public-capture metadata has2*groups state
slots. Corrected63/64/65-group cases cover126/128/130 slots, all captures, scalar
coordinates and cursor aliases. No runtime change followed that correction.

One **110.098-second** public comparison uses immutable dev.71 Full `2c5e3416`
Core/POSIX pairs versus metadata `01526d23` plus the archived patch. Source bodies
were unchanged between those commits. Core/caller stay byte-identical; only the
POSIX pair changes. Separate ordinary processes run ABBA for Validate/AssumeValid,
eight-CPU startup then CPU2 affinity. All480 samples qualify, minimum61.402ms;
75ms calibration target,120s cap. Results/checksums are consumed after timing.
Maximum observed batch allocation160.886MiB stays below512MiB. Validate checks the
complete subject; AssumeValid trusts valid unchanged UTF-8.

| Complete public operation | Validate change | AssumeValid change | Allocated B/op before → after |
|---|---:|---:|---:|
| Ambiguous captured Match | −7.31%, disjoint | −6.44%, disjoint | about56 → 0 |
| Absent-group Match | −4.73%, overlap | −3.63%, overlap | about48 → 0 |
| Unicode/nonzero-floor Match | −2.56%, overlap | −6.03%, overlap | about56 → 0 |
| SKU adapter control | −6.70%, overlap | −8.61%, disjoint | about96 → 0 |
| Finite/live-token captured Match | −0.59%, overlap | −3.17%, overlap | about1032 → 976 |
| Funded/deadline/live-token Match | −0.60%, overlap | −1.15%, overlap | 1080 → 1024 |
| Captured1024-scalar history | +0.85%, overlap | −1.28%, overlap | 172368 → 172320 |
| Complete three-match cursor | +5.93%, overlap | +3.12%, overlap | about336 unchanged |
| Large-state Count | −2.25%, disjoint | −2.11%, overlap | 4664 unchanged |
| Existence | −7.95%, disjoint | −2.78%, overlap | 32 → 0 |
| Shared Core Count control | −0.23%, overlap | +2.19%, overlap | 0 unchanged |
| Predecoded .NET Count control | −1.15%, overlap | +1.15%, overlap | 0 unchanged |

Ambiguous captures drop0.664→0.615µs strict and0.651→0.610µs trusted. Keep the
cursor's higher medians0.958→1.015/0.950→0.979µs with overlapping ranges; do not
call them equivalence or erase them. Other overlapping reductions are uncertain.
Fractional allocation/pool-fill observations remain in [full costs](cost-summary.json).
Shared-account peak977B, including parent17B, is unchanged; final loans return0.
Accept the allocation removal and settled selected gains with these local costs.
No confirmation, tuning variant, native ratio, general-history or parity claim.

Release Windows/Linux pass317 checks per.NET10/11; Linux also passes317 per target
with intrinsics disabled using matching binaries. Existing failure/refund, finite
cutoffs, capture/parent oracles, strict validation, original interruption and
retained alias checks remain. Independent verification checks all loaded pairs,
caller/inputs/results/samples, associated PE/PDB identities and339 Core/7 POSIX
compiled documents per producer. Only the three intended POSIX source files differ.

Canonical offVM artifacts/experiments/posix-scoped-state-20261111:

- Raw: `1D277E4C164444CC1F331C81FA822C8582C1A7DB1BA7C96D9FC66BA85FBFCA9E`.
- Windows/verification: `7BC5542A9C709274B66651B0C90164A58D94CF515C3FA126217AED55E667D933`.

Raw retains frozen pairs, common caller, protocol, patch, allocation probes, all
samples and Linux checks. No package/Full/catalog repeat or change to immutable
dev.71 bytes. Broader warm Python output, strict-input Core costs and general
POSIX parent/history architecture remain active.
