# Reuse checked ASCII adjacency in PCRE2 word boundaries

PCRE2 now uses the existing checked Core TryGetAsciiBoundary result when both
neighbor bytes are ASCII or absent. Otherwise its existing Rune-based PCRE2
policy still runs. Core's full Unicode policy is not used: PCRE2 UCP/joiner rules
differ. The Core edit is an integration-marker comment; no new predicate, engine,
selector, candidate plan, allocation, resource charge or unchecked access.

Baseline engine1cd08c54/data423a8145. Published/staged dev.4 packages are immutable
and do not contain this source change. No public contract or numeric resource
threshold changes. The rejected boundary candidate filters remain closed.

Public checks cover the ASCII pair matrix under both option modes, Unicode
letter/mark/digit/emoji/joiner neighbors, every scalar origin, retained captures,
Count/empty progression, strict malformed tails, original cancellation and finite
admission. Production helper checks preserve invalid interior-boundary/offset
failures. All14new checks,138 focused checks including existing partial probes,
and the full6486-check PCRE2 Release suite pass per runtime on .NET10/11.

The [single public comparison](comparison.json) completed **63.986s**,17workloads,
minimum48.361ms samples. Same Core DLL/PDB bytes and typed delegate driver in
isolated contexts; warm1.5s/min1000calls, independent60ms calibration, five
alternating samples, full result batches consumed after timing. Equal results,
allocations unchanged in every lane. No confirmation or variants ran.

| Public operation | Change |
| --- | ---: |
| Date hit/miss |−2.44% / −1.08%|
| Dense failed date candidates |−3.14%|
| Dense date Count |−1.46%|
| Nonword Count |−6.20%|
| Captured date Count |−2.01%|
| Finite/live miss |+0.12%|
| Finite/live/deadline hit/miss |+0.66% / −2.24%|
| Unicode |+3.66%|
| Unicode-heavy fallback |+1.81%|
| UCP |−0.37%|
| Existing bounded control |+1.45%|
| Literal control |−1.04%|

[All observations](summary.json) remain visible. Largest adverse median3.66% is
within10%. The unchanged subroutine control also improved5.26%; that observation
is retained without attributing its gain to this helper. Small date movements
are not a claim of closing its native gap, universal speedup or parity. The
fixed-original cumulative gate remains at a coherent delivery boundary.

Immutable archives under
`artifacts/experiments/pcre2-ascii-word-boundary-20261101/`:

- `evidence.tar.gz`: exact before/after DLL/PDBs, patch/source, public driver,
  build/preflight logs and raw measurements, SHA256
  `904DEA437D692A8CC529AB19F1BF452B1CE98C9C57D4E09B27F33B56E1BD516E`.
- `correctness.zip`: complete final source/tests, matching Windows assets and
  both-runtime checks, SHA256
  `42DAEE4C1F4FFF33CB693C1C60A6D829AB30BC1094327B67CDDFC3D627514ED2`.

Next refresh the affected existing date/native rows selectively at committed
source, preserving other identities/statuses, scaling and uncertainty. No package
or full catalog/platform matrix for this small step.
