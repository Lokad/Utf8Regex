# Direct POSIX whole-boundary exclusion

Accept the POSIX-only simplification on39490bbf (production behaviorb460).
Replace whole-expression min/max alphabet construction and scanning up to2048
following bytes with one checked scalar and the normal literal/dot/class
predicates over at most64 original instructions. Every path beyond an accepted
endpoint must consume its next scalar. If all consuming atoms reject that
scalar, the endpoint is maximal. EOF remains the safe bound when the proof
declines. Remove the scan/decoder dispatch and two unused imports; add no program
metadata, retained storage, cache, executor, mode or ownership mechanism.

Actual predicates cover negated and Unicode brackets and newline-dot semantics.
Existing parent/scope/history proofs still determine whether child histories can
be pruned. Captures, absent/empty groups, anchors, scalar/byte coordinates,
validation policies, original cancellation/deadlines, resource admission/refunds
and cursor/result aliases retain their contracts. Numerical proof work changes.
The former farther-ahead bound can be lost when the first acceptance is shorter;
longest-match semantics remain, at an explicitly accepted performance cost.

Windows/Linux Release POSIX361 tests per.NET10/.NET11 target pass, including361
per target on software paths with matching binaries. Extended independent0..8
occurrence enumeration covers Unicode/negated alternatives and unmatched sentinels.
Existing long scalar branches retain earlier-shorter/later-longer whole selection.
New public tests retain finite captures, short destination/whole-only independence,
Count, cursor aliases and retained captures. Real proof-loop cancellation/deadline
injection checks both validation policies, output clearing and the held17B refund.

Twelve public100001/100000 work refusals become exact capture successes. Nine
emitted programs and the shared Core pair are unchanged. Two EOF successes and
four genuinely unproved whole/parent work refusals remain. An initial nullable-dot
target had an unresolved parent at first acceptance, so it remains a refusal
control; the required-dot-tail target uses the existing fixed-width parent proof.
Two old interruption fixtures now optimize, so their general-history witness
retains an actually accepted boundary scalar. These explain the initial three
test failures per target; no extra production rule was added to force eligibility.
A private probe's unavailable value inequality operator and Windows console
Unicode printing were corrected before measurements; original diagnostic logs stay.

One **114.329-second** fourteen-row, two-policy ordinary-process ABBA comparison:
560 samples, minimum63.638ms, maximum54.047MiB retained batch. Runtime.NET11.0.0,
eight startup CPUs then CPU2, default tiering/PGO. Complete public results,
allocations and staged/loaded hashes verify. Core/caller/.NET bytes are identical;
POSIX DLL/PDB pairs are distinct. Both POSIX producers'10 source documents verify,
including six generated document observations.

| Public operation | Validate before → after | AssumeValid before → after |
|---|---:|---:|
| Negated whole boundary,16 scalars |1075.881 → 2.924µs|1086.459 → 2.859µs|
| Unicode-class boundary,12 scalars |156.287 → 2.396µs|156.165 → 2.363µs|
| Astral boundary,12 scalars |157.064 → 2.558µs|155.201 → 2.495µs|
| Newline dot with required tail |112.115 → 1.651µs|110.513 → 1.679µs|
| Finite/live |1278.785 → 3.547µs|1287.052 → 3.499µs|
| Funded/deadline/live |1390.453 → 4.291µs|1366.550 → 4.283µs|
| Cursor with unmatched sentinel |1285.671 → 4.510µs|1290.202 → 4.458µs|
| Whole-only longest selection |607.892 → .778µs|609.542 → .742µs|

These affected rows have disjoint ranges and98.4–99.9% lower public execution time.
Ordinary calls retain0B allocation; finite/funded/cursor fall3936/3984/4320B to
2608/2656/2992B. Preserve the tiny2608.004873B pool-fill observation in one trusted
finite process. Aggregate modeled shared peak falls2833→2001B including the held17B
parent, final0. This is not total heap/RSS accounting.

The earlier-shorter control costs **+10.57/+13.69%**, both disjoint:
42.676→47.186µs strict,42.538→48.360µs trusted. Accept this documented loss of the
old farther-ahead proof for the larger gains and simpler mechanism; no fallback
proof was retained to hide it. The unresolved nullable-dot parent remains general
and still refuses32 scalars under100000 work, although its12-scalar public cost
falls27.66/29.19%, disjoint. Do not infer general polynomial performance or profile
parity. Old positive-alphabet/SKU/Core/.NET controls overlap; preserve strict
unchanged-Core+4.17% and every observation without normalization.
[All observations](cost-summary.json). No confirmation or tuning.

Canonical local raw evidence under artifacts/experiments/posix-whole-boundary-20261112:

- Comparison/checks/source:B43E4133E024C17460D011622AA0C3E8CAE5C96660DA182F72C47014447999EF
- Windows:B64B4446AB4F9CF5C15127FF371CAEDE579656E5B29BE0B41C358F61F9AA14B2

Evidence is hash-verified offVM. The source stays frozen through checks/timing;
no Full/catalog/package matrix or immutable staged/published artifact changes.
General unresolved-parent/history architecture and large warm Python/Core gaps
remain open after this bounded improvement.
