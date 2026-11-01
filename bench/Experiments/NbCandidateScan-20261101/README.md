# Bound prepared candidate-start scanning

FindFirst now admits at most 64 candidate-start lookups per work quantum. Each
lookup reads a bit in the existing viability table. Original-token cancellation
and absolute deadlines are checked before each group and before no-match
publication. All atom, capture replay and preparation charges remain; storage
admission, scalar semantics and cursor aliases are unchanged. No allocation,
metadata, engine, mode, unchecked read or framework was added.

Candidate-search work intentionally changes from k to ceil(k/64) for k examined
starts, as documented in [SPEC](../../../docs/SPEC.md) and
[migration guidance](../../../docs/MIGRATION.md). Finite policies need qualification
against the new version's costs; the smaller numeric cost is not a cap exemption.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json) took
**92.532 seconds** on the quiet Ubuntu VM, .NET 11 RC3, CPU mask4. Frozen efcc6b61
binaries before (parent da684c0b changes data/receipt only); parent plus the
archived runtime patch after. Fourteen cases, four equal-result lanes, 1s/min64
warmup, independent 60ms calibration and five alternating samples, minimum
62.230ms. Full UTF-8 validation is included and batches are checked after timing.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Region Count ordinary | 1448.175 | 1411.880 | -2.51% |
| Region Count compiled | 1501.688 | 1440.508 | -4.07% |
| Region Count live token | 1566.158 | 1426.128 | -8.94% |
| Region Count finite/live | 1778.920 | 1592.727 | -10.47% |
| Region Count deadline/live | 3401.034 | 2994.859 | -11.94% |
| Unicode Count | 352.256 | 337.576 | -4.17% |
| Wide Count | 2325.594 | 2328.353 | +0.12% |
| Nullable Count | 80.972 | 78.918 | -2.54% |
| Dense ASCII Count | 320.138 | 323.127 | +0.93% |
| Dense supplementary Count | 237.888 | 233.285 | -1.93% |
| Category Count deadline/live | 661.201 | 629.900 | -4.73% |
| Category IsMatch control | 1142.742 | 1131.108 | -1.02% |
| Non-NB Count control | 4.762 | 4.702 | -1.25% |
| Literal Count control | 0.402 | 0.404 | +0.36% |

Every lane's allocations are unchanged. [Pairs/spreads](summary.json) show target
spreads at most 1.02; controls at most 1.04 except the tiny literal at1.13. The
small adverse observations are retained. No confirmation or variant campaign was
needed. These are incremental public costs, not a cumulative parity claim.

[Focused Release checks](verification.json) pass on net10/net11. Thirteen new
theory rows cover 63/64/65 boundaries, nonzero origins, ASCII/supplementary
hit/miss, packed/wide programs, refusal before the second group, interruption
within 64 lookups, final no-match publication and enclosing 13-byte loans.

[371 complete production probes](state-verification.json) preserve every table
bit, preparation and independent streaming-search work, batch ranges/captures,
peaks and loan refunds. Batch-search work differs by exactly k-ceil(k/64), with
k independently determined from the first viable bit (delta0..7 in this dataset).

No construction changes, cold measurements or package/fullcatalog reruns.
Existing published/staged artifacts remain immutable. Current-source full refresh,
cumulative fixed-a40 costs and fresh packages/consumer checks remain a later
coherent delivery gate.

- [Raw comparisons, harnesses, patch and frozen binaries](../../../artifacts/experiments/nb-candidate-scan-20261101/evidence.tar.gz):
  `B96D6DECCA2E4F53BDD5CDC62542F50B3FD0F0D8D19AB103D802CF131CF6B19D`.
- [Correctness logs and source](../../../artifacts/experiments/nb-candidate-scan-20261101/correctness-source.zip):
  `E585AEC8258C1DB909BA1A2FB688706B700646B49121B6345065286A4F91F7FE`.

Only region rows were selectively refreshed from clean source `6a55974a`:
ordinary 1382.928us / 38.60x, compiled 1392.136us / 38.31x predecoded .NET 11,
557416 bytes, both Gap (previous 39.08x / 40.12x). This separate observation is
not the controlled A/B percentage. All 30 samples exceeded 49.32ms. Other cases,
identities, statuses, lifecycle/scaling and descending ordering are unchanged.
The existing parity verifier and three snapshot guards per runtime pass.
[Selective raw/generated/binary evidence](../../../artifacts/experiments/nb-candidate-scan-20261101/selective-evidence.tar.gz):
`CC25241A95CC06CBEFC1F7FCC61BBE606AC9B63B5687270163FA8DE35CA976A3`.
Committed Core DLL SHA256:
`76D728E68C04CFD0C57D5A3A7F9956744FDCD1705881ED801EB166B4C9A8E67D`.
