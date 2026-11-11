# Combine UTF8 validation predicates before extracting their masks

Accept four local reductions in the existing common-UTF8 drain. Both256/512-bit
main and reactivation paths combine the safe-three-byte range and excluded
surrogate-lead vector predicates before extracting one mask. This matches the
sample selector's existing reduction. Remove four scalar extraction calls;
validation algorithms, selectors, checked loads, carries, exact error offsets,
fallbacks, allocations, validation policies and resource/interruption checks stay.
No public contract, fields, metadata, cache, decoder or execution mode is added.

One1.2s diagnostic warmup of actual strict compiled Chinese Count confirms the
engaged path:13178 complete calls,30 matches,813478B, input F129E819.... This is
code inspection, not public throughput evidence. Release Tier1 removes two kmovq
transfers (7→5), code1275→1266B. No repeated CPU capture or isolated-kernel claim.

One **110.860s**, twelve-row, two-policy ordinary-process ABBA comparison,
480samples, minimum71.527ms, maximum0.413MiB retained batch. Each starts with8CPUs
thenCPU2; runtime.NET11.0.0. Distinct staged/loaded Core DLL/PDB pairs, common
caller/.NET hashes, complete counts and inputs verify. Both342-document producer
source sets and six generated documents verify. No confirmation or tuning variant.

| Public operation | Validate before → after | Observation |
|---|---:|---|
| Chinese literal Count | 79.412 → 78.434µs | −1.23%, overlapping ranges |
| Chinese alternation Count | 100.837 → 98.610µs | −2.21%, overlap |
| Chinese finite/live Count | 113.503 → 111.056µs | −2.16%, disjoint |
| Chinese funded/live Count | 113.086 → 111.687µs | −1.24%, overlap |
| Chinese funded/deadline Count | 121.036 → 118.290µs | −2.27%, overlap |
| Russian / email / symbol Count | −1.07% / −1.14% / −0.02% | Overlap |

Accept the simpler predicate form and small finite-lane benefit; ordinary effects
remain uncertain. No stable broad speedup or native parity claim. Preserve strict
mixed+0.33%, ASCII+1.11%, short+2.76%, .NET+0.27% and trusted Russian+2.85%, all
with overlapping ranges. The other trusted controls also overlap. No control
normalization. Ordinary0B, governed/funded112B and deadline160B remain unchanged;
parent peak17/final0 throughout. Full observations remain in the receipt/raw files.

Focused Release checks per.NET10/.NET11: Windows/Linux Core1468, Python298,
PCRE2108 and POSIX7 pass, using existing independent byte-tag/scalar-error oracles,
all vector lanes, adaptation, constrained/surrogate/astral/tail cases and profile
input/resource/cancellation/publication checks. Selected70 per target pass with
all intrinsics disabled and with AVX512 disabled. Actual runtime witnesses show
Vector512=false/Vector256=true for.NET10.0.12 and.NET11.0.0 in the corrected lane.
The source and tested final Core pair remain identical to the measured pair.

The initial AVX512F switch alone was ignored by.NET11. The runtime's own binary
configuration names identify EnableAVX512; the corrected child sets both names
for the two runtimes. The guard stopped before timing. A separate single-target
probe restore override then failed and was corrected with an isolated temporary
net10 project; producers stayed frozen. Original logs/failed preflight are retained.
There was one public comparison. No new qualification framework, Full/package
refresh or changes to immutable dev.72 assets and published GA benchmark tables.

Raw archives under artifacts/experiments/validation-mask-reduction-20261111:

- Complete:F90D03118921DDD80ECF612CEC56A8939C9BC283F09260BCA735966FFD8525E5
- Preflight guard:C3E88854F6A054F8D2552D938E0F9C65CDC81F877BE8FE60DAE95F2C1AEFE619
- [Compact observations/source and Windows archive hashes](cost-summary.json).
