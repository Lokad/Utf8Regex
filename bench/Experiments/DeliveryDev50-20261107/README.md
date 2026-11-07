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

Linux exact-Windows-byte acceptance and the single cumulative dev.49-to-dev.50
public-operation comparison are in progress. This receipt does not yet claim
completion of those checks or a universal package speedup. Publication is not
authorized.
