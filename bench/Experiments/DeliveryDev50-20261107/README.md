# Common dev.50 delivery checkpoint

The class-instruction borrowing, Python invocation transfer and direct class-window
changes are frozen at `34d35937625782f786bdb66c6bb2e214d6deed8f` for common
**0.5.0-dev.50**, with exact Core pins and .NET 10/11. Packages are unpublished.
Dev.49 and all 264 older staged assets remain immutable.

ONE complete quiet-VM refresh passed in **46m19s** (2779.0054958 seconds), below
the unchanged 3600-second cap. All 83 steps passed: one Release build,
110,150 source checks across both runtimes, disabled dynamic code, the complete
.NET 11 catalogs, paired comparators, lifecycle/scaling, resources and verifiers.
The original RAM/disk admission guards remained in force. No producer inputs
changed during the run. Source CI passed on Windows and Linux.

All seven generated files were hash-imported once. Root README retains four
generated blocks, six descending tables and 178 ratios. Six additional snapshot
test hosts passed 10 checks using the matching Full binaries without rebuilding.

| Qualified snapshot | Summary |
| --- | --- |
| Core versus predecoded .NET | 122 wins, 2 tie candidates, 54 gaps; no unqualified rows |
| PythonRe versus CPython | 42 managed faster, 35 CPython faster, 2 equivalent, 7 inconclusive, 4 floor exclusions |
| PCRE2 versus private native comparator | 76 managed faster, 11 native faster, 1 equivalent, 13 inconclusive, 25 excluded |

Retain uncertainty and exclusion reasons in the JSON. Successive catalog movement
does not establish causality. PythonRe diagnostic gaps against predecoded .NET
include folded miss 11.10x/0.38 us, early literal 7.96x/0.66 us and Unicode miss
6.67x/0.34 us, all 0 B/op. Core Chinese literal remains about 4.52x. These are
different comparators from the paired CPython and private native results.

The accepted source experiments preserve their own controlled gains and adverse
neighbors: [instruction borrowing](../ClassInstructionBorrow-20261107/README.md),
[invocation transfer](../PythonInvocationTransfer-20261107/README.md) and
[direct class windows](../DirectClassWindow-20261107/README.md). The last removes
impossible PythonRe mail candidate starts, rather than changing Core's native mail
route. Its focused ordinary/finite/deadline gains were 37.49%/33.31%/36.73%, with
unchanged allocations. Final package costs are a separate acceptance check.

[Full receipt](full-refresh.json), [ratios](ratio-summary.json),
[catalog movements](catalog-movements.json), [snapshot import](import-validation.json)
and [matching-binary guards](post-refresh-guards-acceptance.json) retain scope.
Canonical raw evidence is under `artifacts/experiments/delivery-dev50-20261107/`:

- `full-evidence.tar.gz`: `07D5C6B44E6559F7192FE1D20CA4ABBCB917AE628A8D3599DD218ECFD272F45B`, verified off VM.
- `guards-evidence.zip`: hash in the matching-binary guard receipt.

## Package acceptance

Windows qualification passed in 282.7471536 seconds: two builds, one pack per
profile, eight detached fixtures/110,110 checks. Original Awk 19/Lython 118 checks
per target and public producer probes passed with final root loans zero.
All 464 compiler documents and 12 restore audits are retained. Six new package
and symbol files bring `artifacts/nuget/` to 270 assets; the previous 264 remain
byte-for-byte unchanged. [Windows acceptance](windows-acceptance.json) and
[asset preservation](staged-assets-preservation.json) record the boundary.

Linux accepted the exact Windows bytes without rebuilding or repacking the
producer: eight fixtures/110,110 checks, original Awk 19/Lython 118 per target,
public probes with final root loans zero, 24 restore audits and 12 TRX identities.
The original 1,565 consumer source files change only three exact package pins.
[Linux acceptance](linux-archive-acceptance.json) and
[consumer inputs](boundary-inputs.json) retain their identities.

## Cumulative package costs

ONE exact-package dev.49-to-dev.50 comparison completed in **98.265182093s**:
Default-process ABBA, 19 public workloads, 380 samples, minimum 51.0792 ms.
The fixed typed caller loads one producer per process, warms for 750 ms/minimum
32 operations, calibrates lanes independently toward 75 ms with one calibration
confirmation, and consumes results after timing. The outer cap remains 120 s.
No noise confirmation, tuning, package qualification or Full repeat followed.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Captured class FindAll | 0.85 | 0.82 | -4.40% | 128 |
| Anchored FullMatch | 0.34 | 0.31 | -9.98% | 0 |
| Unicode miss, unlimited | 0.35 | 0.29 | -18.78% | 0 |
| Same finite/live | 0.42 | 0.36 | -13.86% | 112 |
| Folded Search miss | 0.41 | 0.39 | -5.00% | 0 |
| Same finite/live | 0.55 | 0.53 | -3.88% | 112 |
| Full Unicode hit finite | 1.71 | 1.61 | -6.18% | 112 |
| Unicode hit finite/live | 1.49 | 1.38 | -7.08% | 112 |
| Long Unicode miss finite/live | 11.85 | 9.71 | -18.07% | 112 |
| Actual mail Count | 3630.85 | 2285.85 | -37.04% | 1544 |
| Same finite/live | 5074.17 | 3398.67 | -33.02% | 1656 |
| Same deadline/live | 5717.29 | 3611.45 | -36.83% | 1704 |
| Dense ASCII class union | 138.33 | 126.37 | -8.65% | 0 |
| Long captured class union | 51.35 | 53.36 | +3.90% | 64 |
| Folded category control | 173.46 | 153.46 | -11.53% | 0 |

**Accept the documented local cost for the substantial public gains.** The gains
above persist in both processes with disjoint overall ranges. Long captured
class union is also persistently/disjointly slower: +2.003922 us/+3.9023%, with
unchanged allocation. All 19 allocation medians are unchanged. Core's literal
control (-0.13%), partial-letter (-0.47%), PCRE literal (-2.39%) and explicit
whitespace (-3.65%) have overlapping ranges; retain their raw observations without
normalization or causal credit. The finite-mail extra cost falls from 1443.32 to
1112.81 us while its relative overhead rises from 39.75% to 48.68%.

[Summary](package-cost-summary.json), [all process observations](package-cost-observations.json),
[declaration](package-cost-declaration.json) and [acceptance](package-cost-acceptance.json)
retain every row, source/binary identity, absolute cost and range. Earlier
[dev.48-to-dev.49 costs](../DeliveryDev49-20261107/package-cost-acceptance.json)
remain separate evidence; this comparison does not establish universal gains or
attribute a particular change to each cumulative movement.

One missing staging identity file stopped Linux setup before tests. One local
analysis invocation initially looked for the local-only analysis helper inside
the worker archive. Both preparation corrections are [recorded](preparation-corrections.json);
neither changed producers nor repeated measurement or qualification.

Additional immutable, off-VM archives:

- `windows-evidence.zip`: `9B182445526441E734B31B9C881420C35FDCBC46F240BB7AA6C8525C32057CAC`.
- `windows-restore-inputs.zip`: `711E369BF9292AF4C9A671BABD67562E1AEC19A9EA260060D6E1ACDA316D3C96`.
- `linux-evidence.tar.gz`: `BD950CEA04F0B850DDFFAFD2C1F304C00C55E2356A15FA38CA749B67BE197D2D`, 808899755 bytes.
- `package-cost-evidence.tar.gz`: `14F10E36F0A575C0875AB76E05DDC1B9AF760F2A3E78D50D0CBAB87809D59AEA`, 3779425 bytes.

All delivery checks are complete. [Snapshot CI](snapshot-ci.json) passed on
Windows/Linux for `f2e8ec1f46bcd3b05081de71766f0b707e0eee4f`.
[Delivery state](delivery-state.json) records the completed checkpoint; broader
performance/profile work remains separately planned. Publication is not authorized.
