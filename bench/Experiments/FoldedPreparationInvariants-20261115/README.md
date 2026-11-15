# Retire fallback paths excluded by prepared search construction

Accepted source `fb6a9ebe`, test-only cleanup `9eda7612`; frozen Core baseline
`368e4b4f`. Remove **49 net production lines** without changing algorithms,
allocation, public contracts or charged construction/execution work. The prepared
value remains immutable; no descriptors, compatibility flags or new framework.
[Acceptance](acceptance.json), [public observations](cost-summary.json).

Reachability proof:

- Only IndexOf/Count with the value's readonly IgnoreCase true call the private
  forward folded search. Default structs and exact searches cannot enter it.
- Length <=8 returns through the existing short kernel. Every successful longer
  ignore-case constructor prepares folded bytes, shifts and an index in [1,n).
- Anchor selection returns positions from its loop over [1,n); the general
  construction selector also returns such a position or n-1. Construction failure
  does not publish a partially prepared value.
- The post-Horspool scalar loop, runtime index recomputation and nullable owner
  path therefore have no consumers. Delete them and redundant null/length guards.
  Keep the construction selector, charges, and real scalar/Horspool fallback when
  SIMD declines. Reverse folded searches retain their algorithm and prepared bytes.

ONE **63.323s** ordinary-process ABBA, eight public Count workloads, five
150ms-target samples per process, 20ms floor, 150s cap, no confirmation. Default
.NET 11.0.0 tiers/PGO, normal eight-processor startup before fixed allowed CPU2.
Same valid unchanged UTF-8, options and integer output; Validate unless trusted
is explicit. Finite calls retain live original tokens. Actual producer DLL/PDB
and compiler inputs were frozen before mutation and verified after loading.

| Public Count | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| English, Validate | 153.932 | 135.053 | -12.26% | overlapping, unstable baseline |
| English, AssumeValid | 103.175 | 99.189 | -3.86% | overlapping |
| English, finite/live | 1034.984 | 1036.599 | +0.16% | overlapping |
| False heads | 18.642 | 17.958 | -3.67% | separated |
| Dense folded hits | 248.170 | 249.807 | +0.66% | overlapping |
| Unicode/Kelvin | 97.797 | 97.910 | +0.12% | overlapping |
| Short folded contrast | 28.024 | 27.778 | -0.88% | overlapping |
| Unchanged predecoded .NET | 76.392 | 76.283 | -0.14% | overlapping |

Accept the simpler implementation with the false-head gain. English Validate's
before-process medians differ substantially: 188.94/119.95us versus 130.95/140.67us
after. Its pooled -12.26% establishes no stable speedup or updated parity ratio.
Retain finite/dense/Unicode costs and process uncertainty; no normalization or
additional timing campaign. Allocations stay zero, or 112B for finite calls.
Short folded search shares the changed dispatcher and is a contrast, not an
unchanged control. No canonical .NET/native benchmark table is refreshed.

Separate FullOpts code generation: preferred-index construction 570→552B; checked
vector search 1511→1452B. The old 723B standalone forward method has no standalone
after listing; do not count that as total native-code removal or an inlining gain.
The shared short kernel stays 429B. No isolated-kernel timing claim.

Focused Release checks pass on Windows net10/net11 and Linux net11, each Core675,
PythonRe892, PCRE2 989 and POSIX36. Existing independent-byte, all-ASCII/length,
borrowed-offset/tail, punctuation, Kelvin/scalar/RTL, strict input, original-token,
deadline and construction admission tests remain. A separate no-intrinsics public
preflight confirms Hardware512/256 false and matching counts; 32 scalar kernel
cases verify the retained Horspool path. Only Core builds; matching test hosts
and adapters are reused.

Remove four net test lines of retired-private-overload/field-count inventory from
the suffix borrowing test. Its production zero-budget borrowing/admission assertions
remain; nine affected cases pass on both Windows targets after only the test
project rebuild. Public API/managed inventories remain. No new mirror tests.

No package, Full refresh, consumer adoption or staged/published byte changes;
qualified dev95 remains immutable. Raw is verified offVM before keeper release in
artifacts/experiments/folded-preparation-invariants-20261115/. Baseline/Windows/raw
hashes are in acceptance. All original workers/collectors are terminal0. This
bounded comparison is closed; do not reopen width/census or tuning campaigns.
