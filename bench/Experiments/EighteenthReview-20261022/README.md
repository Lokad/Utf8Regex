# PCRE2 analysis/metadata and residual plumbing cleanup

**Qualification correction, 2026-10-22:** the mixed-subject Core timings below
remain factual, but do not establish coverage of all changed native ASCII paths.
The original identifier pattern also has overlapping prefixes, so ASCII input
alone does not move it to native execution. The folded-boundary pattern selects
scalar execution. See [the nineteenth receipt](../NineteenthReview-20261022/README.md)
for route evidence and the inconclusive bounded follow-up. Raw measurements here
are unchanged; complete affected-route performance qualification remains open.

2026-10-22. Original checkout `347b909e`, matching Release producer `844dcbad`.
PCRE2 source `50c1d440`; complete source `2c73f6b4`, development
`0.4.0-rc.2-dev`. No package created/published; frozen RC1 remains unchanged.

## Delivered contracts and deletions

- PCRE2 Analyze now covers character and single-token-repeat Match backends.
  It reuses the existing consumed-token/fallback facts. MinRequiredLengthInBytes
  is explicitly a conservative byte lower bound, not an exact attainable
  minimum: [é] and [😀] contribute one byte, their two-token sequence contributes
  two, and [A-Z]{2,4} contributes two. Empty/optional/anchor cases retain zero.
  This fixes missing positive information without another parser/analysis tree.
- One global name-slot dictionary is acquired/admitted per PCRE2 compilation.
  Nested assertion and assertion-condition lowerers borrow the same read-only
  lookup. Removed their repeated LINQ grouping/dictionaries/slot arrays and
  the compiled-root NameEntries clone. Internal immutable tables/slot arrays
  are shared; public CopyNameEntries still isolates caller writes.
- Removed verified unused arguments through private search filters, structural
  helpers, the compiled enumerator, PCRE2 parser/DFA/cursor and PythonRe error
  construction. Two identifier wrappers and one window forwarding overload
  disappeared. Exact/folded search dispatch shares the same prepared-search
  ownership. Benchmark/test callers use the live implementations/signatures.
- Removed QueueLengthForTests => Count, duplicate assertions and obsolete queue/
  dictionary/faulted-Lazy test wording. Failure/generation/waiter/eviction tests
  remain. The cache's real mayEvict=false call and two-pass publication remain.

No matching algorithm, SIMD, cursor/value layout, candidate hint, result lifetime,
public signature or resource authority changed. The virtual default global-kernel
contract and trailing search plan used by Count stay live. Prior rejected Core
layout/window-emission and PythonRe pruning experiments remain closed.

Construction accounting intentionally loses charges for lookup acquisitions
that no longer occur. Admission before the first lookup, major arrays/buffers,
instructions/state growth, recursion and interruption remain. The root table
clone was covered coarsely; removing it adds no new accounting mechanism.
PythonRe mutable-result copies and actual Unsafe.SizeOf queries remain. No
total managed-heap/JIT/RSS guarantee is claimed.

## Validation

Focused PCRE2 Release: 97 pass. Full PCRE2: 6,005 pass in 9s; PythonRe: 3,910
in 10s; Core: 39,584 in 1m34s; no skips/failures. The new public cases cover both
pattern constructors, conservative bounds, nested/duplicate names, conditions,
subroutines, replacement, retained contexts and mutation of copied name entries.
Existing suites preserve folding/RTL/scalars, cancellation, deadlines and limits.
Builds target affected projects; matching PCRE2 binaries are reused.

Final producer/PE/PDB/compiled-source preflight accepts the benchmark/library
closure. Generated README parity and PythonRe/PCRE2 pages are current. Four
snapshots/nine frozen RC1 files retain hashes/sizes. Remaining unused-parameter
scan contains only the legitimate virtual default. CI closeout is in validation.json.
No local solution/package matrix or normal build-server shutdown was repeated.

## One bounded fixed-original comparison

Identical driver bytes, frozen original products versus the complete candidate,
Linux/.NET 10.0.12 CPU7/default tiering. Each lane receives 750ms warmup, bounded
35–45ms calibration and five samples with alternating lane order. Both children
have a 60s cap. No confirmation run or further timing campaign.

| Lane | Median change | Bytes/op before -> after |
| --- | ---: | ---: |
| PCRE2 named nested-assertion construction | -2.6% | 86,256 -> 83,184 |
| PCRE2 character Analyze | +48.5% (140.9 -> 209.3 ns) | 0 -> 0 |
| PCRE2 named-assertion Count | +1.7% | 4,080 -> 4,080 |
| Core identifier ordinary / compiled Count | +2.4% / +1.9% | 3,552 -> 3,552 |
| Core identifier Count, all four finite limits | +1.1% | 3,664 -> 3,664 |
| Core window compiled Count / IsMatch | +3.4% / +4.3% | 1,680 -> 1,680 |
| Core folded-boundary Count | +0.3% | 0 -> 0 |
| Core compiled literal enumeration | -10.0% | 0 -> 0 |
| Unchanged literal Count control | -0.3% | 0 -> 0 |
| Predecoded / decode-then-Regex controls | +2.1% / +0.9% | unchanged |

All matching medians are within the 10% allowance. Spreads are wide, so these
are bounded observations, not precise equivalence, upper bounds or attributed
speedups. The corrected Analyze performs new work: its original zero answer was
incorrect/incomplete, so that lane is not correctness-equivalent. Its observed
48.5% increase is retained/disclosed as a diagnostic correctness-repair cost;
no uniform public-operation 10% claim is made. Optimizing that metadata query
is deferred unless a concrete consumer needs it. Construction is separate.

The VM root is still full and background services busy; a new RAM directory
leaves previous files/services untouched. Driver/raw samples/hashes are adjacent.
Fresh immutable package/downstream/actual-host acceptance remains the next
separate delivery boundary; these source observations do not qualify packages.
