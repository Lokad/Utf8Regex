# Rejected longer anchored-prefix hypothesis

**Reverted before performance timing.** Prototype 1d024a3a extended the existing
beginning-anchor rejection from one unfolded ASCII literal to at most 32
consecutive literals, retaining scalar verification and the original invocation.
[Both-runtime checks](verification.txt) and equal-result public preflight pass.

The actual imports [corpus census](census.txt) disproves the proposed cause:
all **19** lines starting with `i` already start with **`import`**. The longer
prefix cannot eliminate a VM attempt on this workload. The corpus is 46,429
bytes, SHA256 `507F5A33C406D155D02A9633551C5231E9CF9890D250C1A5153045F224C688D2`.
Synthetic false-prefix admission is not evidence of closing the real 8.40x gap.
No A/B, confirmation, snapshot refresh, new verdict or package qualification ran.

[Route inspection](initial-route.txt) also finds a FixedDistanceAsciiSets plan;
its facts do not grant CanGuideFallbackStarts. Do not widen scalar-distance
authority merely to reuse it. The short deep attribution probe is archived as
diagnostic evidence, with ordinary comparator/sub-20ms batches, not throughput.

Revert **7ca2b3d6** restores `src/`, `tests/` and CHANGELOG.md to accepted
6e9ace63 exactly. Its Windows/Linux/both-runtime/disabled-dynamic-code CI
40921894846 is green. Imports remains active at 8.40x: investigate its 19
successful VM verifications rather than another prefix-filter experiment.

Immutable files under `artifacts/experiments/anchored-prefix-census-20261029-rejected/`:

- `parity-anchored-prefix-1d024a3a-rejected.tar.gz`: exact DLL/PDBs, host,
  source bundle, driver, preflight and initial missing-driver-corpus correction.
  SHA256 `887AC5BA5E23923799B3E896CA29658134B72224348C17E2A45CD0EDA1C4CB55`.
- `correctness-and-census.zip`: focused checks, census, route and setup logs.
  SHA256 `A97919E578FEAA61177E764AB33072A25140D20F0C9EC4A9E4D4876ABD7BED5C`.
