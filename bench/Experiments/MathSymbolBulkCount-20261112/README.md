# Bulk ASCII math-symbol Count — accepted

Baseline dbcd36e8. Core counts exact ASCII math-symbol bytes once per admitted
extent, then keeps its existing non-ASCII scalar classification. UTF-8 bytes
>=128 cannot equal those ASCII symbols. This removes repeated SIMD/tail setup
between Unicode fragments, retaining the algorithms, managed bounds and allocations.

PCRE2's existing standalone positive case-sensitive Sm proof now reuses Core's
counter under the original invocation for controlled and unlimited Count. Each
admitted block ends at a scalar boundary and spans at most4096 bytes, followed by
publication checks. Other categories/classes/options retain their routes. No new
engine, prepared flag, cache or ownership mechanism. Numerical work and workspace
requirements can fall; SPEC-PCRE2 records that change. This does not establish
metering for every other existing PCRE2 direct operation.

## Public costs

ONE127.095s quiet Linux.NET11.0.0 comparison:14 rows/two input policies/ordinary
process ABBA,560 samples, minimum66.588ms, largest timed allocation27.605MiB.
Default tiering/PGO, eight startup CPUs thenCPU2;500ms warmup per row (first2s),
75ms calibration/20ms floor/150s outer cap. Common caller and.NET reference,
explicitly distinct loaded Core/PCRE2 DLL/PDBs. Full Count values are checked
against scalar oracles and consumed after timing. No confirmation or retiming.

| Public Count | Validate us, before -> after | AssumeValid us, before -> after |
|---|---:|---:|
| Core real16MiB corpus |1756.652 ->1528.694|1237.163 ->1012.500|
| Core finite/live token |1779.380 ->1561.847|1240.471 ->1016.142|
| Core caller-funded/live token |1780.612 ->1556.369|1242.855 ->1012.258|
| Core finite/deadline/live token |1801.373 ->1582.925|1263.513 ->1043.315|
| PCRE2 funded/deadline/live token |2262.706 ->504.136|2207.545 ->416.316|
| Core fragmented Unicode |421.696 ->415.155|305.354 ->299.748|
| PCRE2 fragmented Unicode |506.452 ->498.685|423.066 ->415.573|
| Core short mixed input |.238 ->.218|.114 ->.103|

These ranges are disjoint. Accept the trusted ASCII-only control's +2.24%
(.566 ->.579us, disjoint); strict ASCII -2.07% overlaps. Other-category, RTL,
NonBacktracking and literal controls overlap. Predecoded.NET +1.14/+.96% also
overlaps; retain process observations without normalization or a new parity Status.
All allocations remain equal: ordinary0B, Core finite/funded112B, deadline and
PCRE2 controlled160B. NonBacktracking's unchanged232B remains in the raw control.
Caller peak stays17B (its held loan), with no additional reservation and final0.
This is a scoped allowance observation, not a total heap/RSS bound.
[All observations, identities and uncertainty](cost-summary.json).

## Correctness and retained evidence

Windows/Linux Release399 Core/314 PCRE2 checks per.NET10/11 target pass; software
paths82/4 pass. New tests cover all ASCII bytes mixed with2/3/4-byte scalars,
borrowed offsets/vector/block tails, strict malformed suffixes, both input policies,
PCRE2 nonzero scalar starts, useful finite caps with no scratch loan, real work
refusal and original-token/deadline interruption inside the shared counter.
Existing public deadline translation, first denial and refund checks remain.
407 PDB documents per side verify; only the two intended production documents and
generated revisions differ from the qualified baseline, and PE/PDB identities match.

Initial test failures are preserved: tests overgeneralized PCRE2's Sm-only proof,
then expected public exception translation from an internal helper and a diagnostics
hook that PCRE2 does not install. Corrected tests use the actual boundaries; no
production bug fix was needed. An initial shell upload had CRLF and stopped before
running the gate; the corrected LF script ran once. No measured run was replayed.
All460 immutable packages/symbols remain unchanged. No package/Full refresh,
publication or downstream adoption; generated benchmark tables remain dev75.

Canonical raw at artifacts/experiments/math-symbol-bulk-20261112/:

- gate-evidence.tar.gz:405BDF0080371AF43B5CBC6647E542E6D7EF79C6021FF23DD598E97EBA18AC0E
- public-evidence.tar.gz:593447948554CAA0D3B1DE02AF0EE2515D8A29D8985BC662A864899475EB3FA3
- windows-evidence.tar.gz:7A256B69593BCE6E5F5BE27B5760087C33D55D4E4D92D3B2C860C97CDB13C9E0

Both remote archives verified offVM before keeper release; all workers terminal0.
Next: larger Core/Python public gaps, without replaying closed dispatch/validation
variants or another package matrix for this source step.
