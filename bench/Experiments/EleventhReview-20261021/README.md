# Eleventh simplification milestone — 2026-10-21

Original checkout `0266a2ea5427bf25265d72240bc8853e260bb7ce`, matching original
products `1603f2dd94928b14ccd9ea1cfa1d88a813e3200c`; candidate
`cd9844388e970c48a766bcd1275657eb9603784c`. Implementation `40807822` and final
unused-property deletion `cd984438` are committed/pushed. Development remains
**0.4.0-rc.2-dev**. This is a source cleanup receipt, not package qualification.

## Removed and preserved

- Removed the three unused materialized/deferred-array/example-generator
  constructors from the PCRE2 value enumerator, their ten hard-coded variants,
  exclusive state/helpers/enums and existence-only layout output/assertion.
  Direct and shared-slice algorithms remain. Default cursors stay empty; copies
  keep independent positions and original cumulative enforcement/deadline.
  Actual generic deferred failures and retained values remain valid.
- Backtracking programs with PartialProbe.Kind=None no longer retain their AST.
  Curated Probe programs retain the trees they actually read. Lowered programs
  and option fallbacks remain. A public 4,096-optional-repeat witness drops
  **8,194 AST nodes and 4,097 child-reference slots**; matching still succeeds.
  These are reachable-object observations, not RSS or exact saved-byte estimates.
- Both public named-slot span-copy APIs scan immutable entries directly.
  Temporary LINQ arrays/predicates are removed, with pattern order, duplicate
  names, missing/null names, truncation, isMore and retained contexts preserved.
  On 64 names, regex/context getters fall **1,104/1,192 → 0 B per call**.
- Removed the optional-Core slot interface and empty/populated wrappers; the
  existing nullable Core reference retains the identity/lifetime invariant.
  Removed two unread specialized fallback aliases/arguments, preserving actual
  fallback programs in other operation slots. Corrected PythonReNameEntry.Number
  documentation: named capture numbers start at one; zero is the whole match.

The public x64 value enumerator falls **1,632 → 1,576 bytes**, measured with its
existing Unsafe.SizeOf query. This is a layout observation, not a speedup claim.
No public API or numeric accounting contract changes. Strict UTF-8/scalars,
captures, reset/empty progression, managed safety, NonBacktracking, native
algorithms/SIMD, original cancellation/deadlines and useful construction/work/
depth/working/result controls stay. Conservative construction charges are not
made more precise merely because an unused retained root disappears.

## One bounded cost comparison, now closed

See [measurement.json](measurement.json) for both raw runs, identities, samples,
allocations, spreads, achieved sample durations and summaries, and
[cost-driver.cs.txt](cost-driver.cs.txt) for reproduction. The driver DLL is
identical in both directories, SHA256
`dd7cd539e95db8770b69d56ef3e75ea9ad8f228b101f9da01160988e45d16285`.

Local Windows/.NET 10.0.13, default tiers, one logical CPU. Eleven public lanes
receive at least 1.5 seconds of warm timed loops plus fixed call floors;
calibration targets 45 ms with a 35 ms floor and five alternating samples.
Recorded achieved durations can fall below that floor as throughput drifts.
Each process has a 60-second cap. Checksums cover returned lengths/counts and
enumeration checks successful well-formed/exact-coordinate projections. The
context getter uses an actual retained public detailed-match context; detailed
matching is outside getter timing. No private kernel timing is substituted.

Initial order is original then candidate. Moving controls make it inconclusive,
so the **single allowed confirmation** reverses candidate/original order without
changing driver/product bytes or protocol. Timing then stops.

| Public operation | Initial median change | Confirmation median change | B/call before → after |
| --- | ---: | ---: | ---: |
| PCRE2 literal enumeration | -32.19% | -8.15% | 0 → 0 |
| PCRE2 branch-reset enumeration | -20.25% | +3.55% | 0 → 0 |
| PCRE2 scalar enumeration | -16.74% | +18.08% | 251,680 → 251,680 |
| PCRE2 scalar enumeration, four finite limits | +8.54% | +54.99% | 251,792 → 251,792 |
| PCRE2 reset enumeration | +1.84% | -1.30% | 0 → 0 |
| PCRE2 curated soft Probe | -5.03% | +36.27% | 80 → 80 |
| Regex named-slot copy, 64 names | -88.69% | -86.93% | 1,104 → 0 |
| Context named-slot copy, 64 names | -91.36% | -88.60% | 1,192 → 0 |
| Unchanged Core literal Count | -18.87% | +25.68% | 0 → 0 |
| Predecoded Regex Count control | -28.16% | +30.24% | 0 → 0 |
| Strict-decode then Regex Count control | -32.35% | +25.32% | 1,432 → 1,432 |

Finite limits are experimental allowances: one million work steps, depth 2,048,
working/result capacity 16 MiB each, infinite timeout/no token. Ordinary and
finite scalar enumeration have substantial existing allocation cost; this
cleanup does not resolve or conceal it. These are whole public calls over
480–832-byte subjects; Probe is eight bytes and metadata getters have no subject
scan. See raw rows for exact input sizes and iteration counts.

**The timing decision is inconclusive.** Confirmation controls move +25–30%,
and an affected lane's sample spread reaches 4.61x. No affected lane exceeds
+10% in both comparisons, but this does not certify a 10% bound. The finite
enumeration and Probe increases in the confirmation remain explicitly visible;
do not call them proved harmless, causal regressions or equivalence. No general
throughput speedup is attributed. Retention and allocation removal have separate
evidence. No further timing/confirmation, benchmark Status refresh or package
qualification is part of this milestone; current-package/host cost acceptance
remains a later delivery gate.

## Validation and provenance

Focused Release checks pass (150 cases), then the complete affected PCRE2 suite
passes (5,974, no failure/skip), reusing the matching test binaries. New checks
cover default cursor copies/repeated terminal advances, absent retained trees
with normal/nondefault/Probe operations, name-copy order/truncation/null handling
and zero temporary allocation. Existing semantic/capture/reset/Unicode/resource/
interruption/alias fixtures remain enabled.

One affected test build and matching SkipBuild suite reuse; the benchmark/library
closure builds incrementally. Its build caught one missed unused ordered-window
property, removed in cd984438 before freezing/timing. The introduced nullable
warning is gone; existing unrelated Core warnings remain. No SDK/server shutdown,
per-edit solution matrix or package rebuild. The original 40807822 CI run was
canceled after the final source fix to avoid duplicate runner work.

Final source CI 39598950761
is terminal green at exact cd984438: all four Release suites pass on Windows
and Linux. The read-only page/parity/source preflight results are in
[validation.json](validation.json). No test was disabled to obtain a pass.
[retention.json](retention.json) records untimed original/candidate observations;
[retention-driver.cs.txt](retention-driver.cs.txt) reproduces the candidate check.
[immutable-files.json](immutable-files.json) confirms all four benchmark/parity
snapshots and nine frozen RC1 files retain their original hashes. No package,
symbol, feed, ZIP, manifest or handoff was rebuilt/relabelled/published.

## Remaining delivery work

Removing every curated Probe tree by extracting a few tokens remains optional;
this milestone removes the demonstrated unused roots only. Private PythonRe
rendered-name collisions still have no reproduced public failure, and broader
translation changes stay deferred. Earlier checked-access/DFA/performance
campaigns remain closed. A fresh coherent immutable package candidate, final
API/package/isolated Awk/Lython/PCRE2 consumers, actual-package/host costs and
publication remain separate delivery work. Frozen RC1 stays unpublished with
known PCRE2 defects; never repack, relabel or publish its bytes.
