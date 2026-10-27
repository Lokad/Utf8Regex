# Incremental PCRE2 slice projection and buffer contracts

Final implementation source `575eeea6`, development `0.4.0-rc.2-dev`.
Frozen unpublished RC1 remains unchanged and must not be released. No package
or benchmark snapshot was refreshed. `review.md` preserves the assessment at
checkout `99bea745`, with existing product `4d89398a`.

## Delivered changes

- `aaa2ee7b`: retain the existing `Utf8ProjectionCursor` in the PCRE2 slice
  global cursor. Remove its per-result `FromByteOffsets` branch and fresh-zero
  projector. Required byte/UTF-16 coordinates remain; public `Current` reads
  its existing published snapshot. There is one projection per successful public
  move, not another cache. Cursor copies retain independent positions and
  their original shared allowance/deadline. The real single-match
  `FromByteOffsets` consumer remains.
- `75a19aa8` investigated a checked overlap guard, including large typed spans;
  `575eeea6` removes the entire guard and its ten new guard-specific tests after
  the bounded comparison/sole confirmation did not establish cost acceptance.
  No dormant guard, staging, switch or extra size query survives. Current
  `MatchMany` runtime behavior is the original implementation.
- `eec34354`/`575eeea6`: remove current Core promises for absent `MatchMany`, `Analyze`
  and `CopyRequiredLiterals` APIs; the first two remain PCRE2 extensions. Profile
  XML/README/specification, host contract and release notes describe incremental
  projection, lower versioned work requirements and the overlap precondition.
  No public signature was added or removed.

The earlier global unchanged-subject obligation already excluded overlapping
MatchMany use. Its XML/profile/host documentation now explicitly requires
disjoint input/result storage as a caller obligation. Overlap is unsupported and
is not checked; no exception guarantee is made. Valid disjoint partial-output,
pre-cancellation and empty-output probing remain. TryReplace retains its
separately documented support for overlapping buffers.

Strict complete-subject UTF-8/scalar matching, captures, retained result lifetimes,
original-token synchronous cancellation, absolute deadlines and useful cumulative
work/depth/working/result limits remain. Removed prefix scans can reduce work
charges; no subject-wide coordinate map or new owned workspace is acquired.
Native fast paths, Core/PythonRe behavior and permitted size queries remain.

## Validation and evidence scope

Affected Release projects built incrementally, with matching binaries reused.
Guard-containing candidate: complete affected PCRE2 suite 6,161 pass, zero
failed/skipped, 17 s; source-identity focus 109. Final source after guard removal:
180 focused Release checks pass. Core API/member inventory 13 and PythonRe
API/member inventory 2 pass at the candidate source; neither project's runtime
source changes in this milestone. Final combined CI supplies complete suites.
No local full-solution/platform/package matrix was repeated.

New cases cover mixed BMP/supplementary subjects, nonzero byte and backing-span
offsets, independent copied cursors and retained/repeated Current, same-position
empty/nonempty retries, each/all finite controls, malformed tails, original
cancellation and an absolute deadline after publication. A focused projection
work check proves the next result does not repay the already-projected long
Unicode prefix. The guard trial passed overlap rejection/adjacency/cancellation
checks, but those guard-specific tests were removed with its implementation.
Existing tests retain disjoint destination reuse/probing, resource denial,
captures, ownership and operation selection.

The guard-containing candidate's Windows/Linux source CI passed in
run 40313124902.
Final `575eeea6` Windows/Linux source CI, including both disabled-dynamic-code
steps, passes in run 40314312634.
ci-source.json records both successful jobs. Four spaced status requests across
the two source runs; local work continued during CI. No earlier package or
timing evidence qualifies new package bytes.

The bounded comparison uses frozen before/after DLL/PDB sets, isolated assembly
load contexts and the same public driver. Its 13 lanes verify all 26 outputs and
coordinate checksums before timing, covering dense Unicode enumeration/MatchMany,
ASCII, sparse Unicode, short native calls and an unchanged Core Count control.
It includes all four finite limits where labeled. Warmup is 2.5 s/version/lane;
samples alternate after calibration toward 40 ms. Raw times, allocations,
producer identities, spreads and protocol are retained. Public costs include
validation, matching and required projection/output traversal; they are not
isolated kernel estimates. No cumulative/actual-package qualification is claimed.

## Bounded cost result and guard deferral

The measured after bytes are the **guard-containing `eec34354` candidate**, not
final `575eeea6`. The frozen before/after sets remain unchanged; final source
identities are recorded separately. No timing rerun follows the guard removal.
Both candidate and final retain the same incremental slice projector. Measurements
support its large dense-Unicode effect but do not qualify exact final-byte costs.

Initial comparison: 80.52 s, nine alternating samples per lane. Every measured
median is within the 10% slowdown allowance. Dense 16 KiB Unicode/2,048 matches:

| Public operation | Before ms | Candidate ms | Change |
| --- | ---: | ---: | ---: |
| Unlimited enumeration | 16.10 | 2.25 | -86.05% |
| Four-finite enumeration | 28.41 | 1.99 | -93.00% |
| Unlimited MatchMany | 18.62 | 2.03 | -89.10% |
| Four-finite MatchMany | 29.12 | 2.26 | -92.24% |

Allocations are unchanged: 7,278,048 B/op unlimited and 7,278,160 B/op finite for
these dense rows. This projection fix does not solve their existing allocation
traffic. Sparse Unicode enumeration medians improve 4.13%/2.30%; ASCII dense
enumeration improves 19.88%. Small differences remain noisy; no exact equivalence.

One predeclared noisy-row confirmation: 31.49 s, 17 reversed-first pairs:

| Row | Initial change | Confirmation change |
| --- | ---: | ---: |
| Four-finite ASCII dense MatchMany | -6.26% | -0.29% |
| Four-finite short native MatchMany | +6.67% | +5.27% |
| Native dense MatchMany | +0.62% | **+18.78%** |
| Unchanged Core Count control | +1.66% | +5.00% |

The confirmation is highly disturbed: control before/after spreads 5.83x/4.77x,
and its absolute medians rise from 29.09/29.58 us to 66.74/70.07 us. Native dense
MatchMany spreads 3.78x/2.46x. This does not establish a causal 18.78% regression,
but also does not establish the <=10% acceptance for the optional guard. Do not
select the favorable initial row, discard the confirmation or start a third run.
The guard is therefore removed and its runtime enforcement explicitly deferred.
The existing caller disjointness contract is clarified without adding a mechanism.

Raw samples in public-cost.json/public-cost-confirmation.json preserve all rows,
allocations, producer identities and uncertainty. No broad current-engine ranking,
universal finite overhead or final-package/cumulative gate is established.

## Remaining delivery boundary

The new immutable common-version candidate, Windows/Linux package/downstream
checks and actual-package/host/cumulative performance acceptance against fixed
`844dcbad` remain deferred. Other cleanup and the closed checked-access/accounting/
layout/retention campaigns stay deferred or closed. The existing archived-project
BDN discovery problem is not expanded into this milestone. No VM operation,
external consumer change, package rewrite or historical snapshot refresh.
