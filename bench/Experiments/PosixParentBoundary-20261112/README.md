# Direct POSIX parent-boundary exclusion

Accept the POSIX-only change on 57ccddbb. The parent proof only needs to know
whether an accepted parent can cross its next scalar. Replace its general
min/max alphabet construction and 2048-byte search with that one scalar and
the existing literal/dot/class predicates over the admitted body instructions.
Restore the whole-expression ceiling helper's implicit instruction stream.
No new helper abstraction, retained state, cache, mode, owner or executor.

The prior leading-parent, unique-close and confined-control-flow proofs remain.
The first acceptance must already reach the whole ceiling; EOF is an immediate
parent bound. Otherwise every consuming body atom must reject the boundary
scalar. Unicode tables, negation and newline-dot semantics use the actual matcher;
an ASCII mask miss alone cannot prove exclusion. Unproved histories retain general
competition. Work charges/checkpoints remain bounded; numerical work can change.
Strict/scalar input, captures, output-size independence, original interruption,
admission, refunds and retained aliases remain. No general polynomial guarantee.

Windows/Linux each pass 353 POSIX tests per .NET 10/.NET 11, including 353 per target
on Linux software paths. Independent occurrence extents cover negative/Unicode
bodies, nullable/reordered alternatives, scalar tails, separate parent endpoints,
floors and destination sizes. Counterexamples preserve a later longer parent
when a Unicode table or a negated class accepts the next scalar. Newline-dot,
earlier-shorter whole and real proof-loop interruption/refund checks pass.
Twelve public 100001/100000 work refusals become exact successes; two EOF controls
stay successful. All seven emitted programs and the common Core pair are unchanged.

One **114.562-second** fourteen-row, two-policy ordinary-process ABBA comparison:
560 samples, minimum 60.487 ms, maximum 55.263 MiB retained batch. Runtime .NET 11.0.0,
eight startup CPUs then CPU2; default runtime settings. Complete results, common
Core/caller/.NET bytes and distinct staged/loaded POSIX DLL/PDB pairs verify.
Both producers’ 10 source documents verify, including six generated documents.

| Public captured operation | Validate before → after | AssumeValid before → after |
|---|---:|---:|
| Negated parent, 16 scalars |990.123 → 2.812µs|1028.055 → 2.784µs|
| Unicode-class parent, 12 scalars |138.327 → 2.311µs|137.357 → 2.293µs|
| Branched captured tail |114.462 → 1.833µs|114.866 → 1.816µs|
| Astral negated parent |138.894 → 2.494µs|139.354 → 2.404µs|
| Negated variable tail |143.204 → 2.263µs|144.541 → 2.262µs|
| Finite/live |1210.110 → 3.498µs|1216.686 → 3.497µs|
| Funded/deadline/live |1296.547 → 4.284µs|1296.741 → 4.245µs|

These seven workloads have disjoint ranges and 98.2–99.7% lower execution time.
Ordinary allocation stays 0 B. Finite/funded allocations fall 3936/3984B to about
2608/2656B; preserve the tiny fractional pool observation in one trusted sample.
Aggregate modeled shared peak remains 2833 B including the held 17 B parent, final 0.

The cursor subject has an unmatched trailing sentinel. Its whole endpoint remains
unproved, so it retains general execution: 1270.466→1321.435µs strict(**+4.01%**,
disjoint), 1271.414→1292.628µs trusted(+1.67%,overlap), 4320B unchanged. Do not claim
a cursor gain from this comparison. The unproved Unicode-boundary control costs
**+8.00/+9.25%**, disjoint: 117.183→126.553/116.870→127.680µs. Accept those explicit
costs for the larger target/admission gains. Old-alphabet, SKU, whole-only, Core
and .NET controls overlap, including whole-only +4.00/+3.64%; no normalization.
[All observations](cost-summary.json). No confirmation or tuning.

Raw archives under artifacts/experiments/posix-parent-boundary-20261112:

- Comparison/checks/source:138335D91EB0F0B47513F2B5887FFF93B57A5E505D49A856EB673C5B3265A758
- Windows:8F1B2515BFE2C425A01B96C09157453B7036DCBA44DD6261FCFFB82570EBDB00
- Initial baseline probe:CAFD7A206BD7D565645F0BAA724EA039DD548051E623B526BB8F90547EE772E1

Evidence is hash-verified canonical/offVM; all checks and the single comparison
completed successfully. No complete catalog, Full/package matrix, source-version
parity claim or immutable dev.73 artifact change follows this bounded step.
Unproved whole endpoints/general parent histories and warm Python output remain
open. Preserve this limitation rather than add a growing grammar registry.
