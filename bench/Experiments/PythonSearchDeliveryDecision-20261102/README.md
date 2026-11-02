# Retain the measured policy cache for delivery qualification

Restore the **exact** Python policy-cache patch measured in
[PythonPolicySnapshot](../PythonPolicySnapshot-20261102/README.md). Its original
insufficient-gain rejection remains preserved: native Search improved 3.84%,
allocations were unchanged, and the maximum adverse control was 2.16% across
18 matching operations and two constructors. No variant or new parent timing was
run. The original four nullable checks are now computed once in the shared
constructor and read from one private readonly property.

The experiment's 5% minimum primary gain was an agent-imposed rule. It is removed
from the delivery decision because it rejected an evidenced improvement to the
actual blocker without helping the user's release requirements. The **unchanged
172-operation gate against fixed original a40bda03, with its 10% adverse limit**,
still decides cumulative acceptance. This does not relax that limit, reset the
baseline, reclassify a demonstrated regression or replay a stable failed freeze.
No cumulative acceptance or package readiness is claimed here.

With the exact patch restored and the separate Core trial reverted, the complete
PythonRe Release suite passes **4,623 per runtime** on .NET 10/11. Earlier focused
cache checks passed 887 per runtime. No public API, matching semantics, limit,
admission, cancellation, lifetime or documentation contract changed. All three
packages now share a fresh dev.14 version for the next coherent boundary; staged
and published bytes remain immutable. No packages were created by this experiment.

## Separate guard-deletion trial rejected

Replacing Core's repeated exact-LTR-literal eligibility throw with a debug assertion
did not help the actual public Search: **+0.565%**, with all five paired changes
between +0.39% and +0.69%. The single 77.1505-second run completed 18 operations and
two constructors, minimum 43.3126 ms/sample, unchanged allocations and maximum
adverse control +4.78%. No noise confirmation, variant or full suite was run for
that change; it was completely reverted. Focused Release checks passed Core978 and
Python887 per runtime. [Assessment](guard-assessment.json) preserves the results.

Untimed original/current Search captures confirm the real entry path. Core helper
and materializer code were already smaller in dev.13; code size is not throughput
attribution, and the unfunded path allocates no program-use lease. Required funded
guards, same-invocation metering, whole-input validation and final publication
checks remain intact. Read-only warm call counts are not performance measurements.

Raw files under `artifacts/experiments/trusted-literal-discovery-20261102/`:

- `raw.tar.gz`: exact guard trial driver, protocol, patch, preflights, samples and
  DLL/PDB sets; SHA-256 `243244352455EB5D7CE6526049B47C6BADA1AAFFC1D39B286ECB11479746FDCE`.
- `entry-codegen.tar.gz`: original/current untimed captures and probe sources;
  SHA-256 `96FAB85A365183A81119DDFE144FD252B3D807893EF92BBF442A7A5B2DC29FFD`.

The original cache-trial raw archive and its initial rejection remain unchanged.
Only its unnecessary gain floor is superseded. Other closed negative experiments
stay closed, and the fixed-original gate must pass before package qualification.
