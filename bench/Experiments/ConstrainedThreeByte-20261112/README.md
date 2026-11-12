# Constrained three-byte validity in the shared vector proof

Accepted against source `6ed7bc0a97b3ebb984c907167bcb7813fe0a56f7`.
The existing UTF8 continuation-adjacency proof now admits E0/ED triples whose
second byte is in the current vector and satisfies its extra range constraint.
A constrained lead in the final lane and malformed constrained seconds retain
the scalar diagnostic path. Continuation carry, first-error ordering, initial
selection, scalar/4-byte fallbacks, checked reads and interruption remain.
Two typed vector leaf helpers replace four duplicated classifications; there
is no new mode, carry field, cache, owner, allocation or instruction-work charge.

ONE **99.030s** comparison, 14 public rows under Validate/AssumeValid in separate
ordinary-process ABBA order: 560 samples, minimum 72.156ms, maximum 0.314MiB timed
batch allocation, 140s cap and 1GiB available-memory admission. Default .NET 11.0.0
tiering/PGO, 8 CPUs visible at startup then CPU2. Distinct staged/loaded Core
DLL/PDBs, common caller/.NET hashes, complete counts and input hashes verify.
No confirmation, tuning variant, Full/package cycle or new catalog Status.

| Public Count | Validate before → after µs | Change | AssumeValid change |
|---|---:|---:|---:|
| Actual email, 92 results | 1226.375 → 1057.466 | −13.77%, disjoint | −0.31%, overlap |
| Email finite/live | 1504.113 → 1455.235 | −3.25%, disjoint | −1.03%, overlap |
| Email funded/deadline/live | 1591.381 → 1547.073 | −2.78%, disjoint | −1.65%, disjoint |
| Actual URI, 5301 results | 1452.789 → 1282.144 | −11.75%, disjoint | −0.59%, overlap |
| Chinese literal | 85.169 → 96.784 | +13.64%, disjoint | −0.65%, overlap |
| Chinese alternation | 107.760 → 121.066 | +12.35%, disjoint | −1.31%, overlap |
| Chinese finite/live | 123.474 → 135.110 | +9.42%, disjoint | −0.16%, overlap |
| Russian literal | 175.889 → 172.306 | −2.04%, disjoint | −0.83%, overlap |
| Math symbols | 1652.349 → 1653.434 | +0.07%, overlap | −0.14%, overlap |
| River window | 2250.562 → 2253.454 | +0.13%, overlap | +0.64%, overlap |
| Synthetic constrained mixed input | 18.100 → 7.174 | −60.37%, disjoint | −0.16%, overlap |
| ASCII literal control | 12.623 → 12.575 | −0.38%, overlap | +0.04%, overlap |
| Short literal control | 0.345 → 0.350 | +1.34%, overlap | +1.69%, overlap |
| Unchanged predecoded .NET email | 476.682 → 475.381 | −0.27%, overlap | +0.02%, overlap |

Accept the approximately 169/171µs email/URI savings with the explicit 11–13µs
Chinese costs under the user's trade-off policy. The completed checked-access
allowance is not a universal veto. Synthetic gains are not a corpus-parity claim.
Trusted calls omit this validity path: retain their movements, including the
disjoint funded/deadline observation, without crediting them to this change or
normalizing other rows. All ordinary allocations stay 0B, finite/live 112B and
funded/deadline 160B; caller peak stays its held 17B and final loans return to zero.
This is modeled covered capacity, not an RSS measurement.

Focused Release checks per .NET 10/11 on Windows/Linux: Core 1009, PythonRe 382,
PCRE2 232, Posix 7. Software and forced-256 add 50 checks per target/OS; native
pattern/template authority adds Core 96/PCRE2 69. Exhaustive second/third bytes,
wide lanes, borrowed offsets/tails and competing earlier errors agree with
independent BCL validity and the scalar diagnostic oracle. Existing tests retain
policy propagation, validation-once, work/resource refusals and original tokens/
deadlines. Both 342-document Core inventories/PE/PDB IDs verify; only this source
and generated revision differ.

Untimed public email engagement completes 1289 default-width and 933 forced-256
calls with Count 92; witnesses report Vector512 true/false and Vector256 true.
Those call counts are not throughput evidence. The final default Tier1 listing
is 1430B with the leaf helpers inlined and two vector bit shifts; Tier0/OSR
listings remain in the archive. No before/after code-size or kernel-speed claim.

Remove the benchmark census's obsolete scalar-escape and adaptive-policy
estimates rather than replicate the changed executor. Its scalar/fixed-grid input
composition, initial selector facts and total checks remain; CLI labels explicitly
exclude execution-policy estimates. A single affected benchmark build into scratch
and current email inspection preserve the input census. Its first private launch
needed explicit staging of existing project-reference DLL/PDBs; no library rebuild
or timing repeat. Generated tables and the dev76 frozen graph remain unchanged.

Verified off-VM archives in artifacts/experiments/constrained-three-byte-20261112/:

- Gate: B007C900D0E184AA7A2EADAE7DE462A9A7F7301EBF4875EFEE1455C47E0988C8.
- Public/codegen/native checks: 12D38921BC597C6ED73A5A10894EB84763A619082F36895CE784D263EBC17424.
- Windows: D17ECD548BA75F8DFD8531BD51D7855D65F4DC4FBA70C5420D43A2152763C977.

Raw producer graphs, callers, patches, samples, source identities and test logs
remain there. All 468 canonical package/symbol bytes remain unchanged. Keepers
release only after archive verification; broader P0 gaps and POSIX histories remain.
