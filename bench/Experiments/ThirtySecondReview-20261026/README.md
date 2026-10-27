# Finite adapter preparation and distinct Python capture names

Final source `4d89398a`, still `0.4.0-rc.2-dev`. No package or published benchmark
snapshot was refreshed. Frozen unpublished RC1 stays unchanged and must not be
released. `review.md` preserves the source assessment at `4aeb0a6e` with existing
product `9ea498ad`; this receipt records the bounded implementation and checks.

## Delivered behavior and removal

- `9c004c9c`: PythonRe and both PCRE2 Core-construction sites carry their fixed
  numeric policy into existing Core preparation. Finite instances retain useful
  search/scalar payloads while omitting unmetered verifier/runtime/emission storage
  they cannot execute. PCRE2 maps legacy zero to common null in one request
  property; its original invocation still supplies execution units and errors.
  PythonRe also skips prefix/dotall buffers disabled for the instance lifetime.
  Unrestricted measured native paths remain. No mode or preparation framework.
- PCRE2 slice selection explicitly keeps its prior fallback-only boundary rather
  than inheriting Core's policy-sensitive inspection. Original/derived literal
  families, captured backreferences and structural cases retain their selected
  operation types in focused tests.
- `6090ba8f`: preserve policy-independent native eligibility for borrowed
  enumeration invocations. The complete PythonRe suite exposed three failures
  after initial policy propagation: a finite literal family acquired a subject
  map and exceeded the existing 4,096-byte result allowance. Moving the lifetime
  policy checks to the two public Core offset selectors restores the adapter's
  existing metered native cursor. Public Core selection remains guarded; strict
  validation, original invocation and the small-budget tests are preserved.
  No threshold was raised and no failing test was disabled.
- `97556c73`: optional native names are `g` plus each named capture's unique
  Python number. Group declarations, named references and conditionals use that
  namespace; user spellings cannot collide with generated names. Numeric
  references keep the existing named/unnamed Core permutation. Owned scalar
  metadata borrows original Python names and creates decimal text only for
  unnamed slots. Its compiler no longer takes a translation record.
- Removed the alias dictionary, temporary sorting buffer/pass, ASCII-name
  checker and translation-record name-map field. The existing benchmark/test
  comparator renders the same ordinal namespace from parse metadata. Alias-pinning
  fixtures now test original owned names, capture identity and real production
  preparation. References and fixed scalar lookbehind are still validated even
  when native text rendering is skipped.
- `4d89398a`: release notes, host contract and packaged profile READMEs describe
  the preparation/accounting scope and naming guarantees. Actual construction
  work/storage can fall as dead preparation disappears. Correct distinct Core
  capture metadata can also cost more than the formerly merged metadata; these
  charges remain versioned, modeled capacities rather than portable heap limits.

Public Python names/numbering, scalar grammar, captures/LastIndex/LastGroup,
strict complete-subject UTF-8, retained snapshots/aliases, synchronous original
tokens, absolute deadlines and useful work/depth/working/result/state bounds
remain. The observed public scalar results were correct before the alias fix;
the auxiliary Core language and capture identity now agree too. Captured literal
booleans can regain complete native discovery. No shipped BCL/native matcher,
new naming framework or reopened accounting/checked-access campaign.

## Correctness and source boundary

One final Release solution build at the coherent source boundary; affected
projects built incrementally during experiments and matching binaries reused.
Complete PythonRe: 3,937 pass in 12 s; complete PCRE2: 6,138 in 9 s. Focused Core
offset/native/fixed-policy/capability checks: 2,034 in 4 s. Existing full source
CI covers Core, both profiles and tooling on Windows/Linux, green at `4d89398a`
in run 40304946205.
Both jobs also pass the existing disabled-dynamic-code check. Two spaced status
requests; local verification/measurement continued while CI ran.

Tests cover each/all finite controls, preserved PCRE2 operation selection,
original/derived Core ownership, working/result admission, original cancellation,
deadlines, captures, full matching, enumeration and replacement. Collision
regressions include both declaration orders, BMP/supplementary identifiers,
named/numeric references, conditionals, repetition, mixed numbering and retained
capture values. The old 4 KiB mixed-width native-family admission test passes
unchanged after the shared-selector correction.

Existing CPython 3.13.2 semantic verification passes all 91 catalog/supplementary
cases. Eight additional CPython controls confirm the collision fixtures' capture
values, LastIndex/LastGroup, reference rejection and replacement facts. These are
semantic checks, not speed comparisons or snapshot qualifications. All three
existing generated-page verifiers pass without writes.

Before/after witnesses: finite `abc|xyz` backends in PythonRe/PCRE2 previously
retained unlimited Core policies, verifiers and emitted literal counters. They
now carry the fixed policy and have no unmetered runtime/verifier/counter. The
formerly colliding auxiliary Core patterns now accept/reject the same witness
subjects as public PythonRe. `name-collision-before.json`, `name-probe-after.json`,
`preparation-before.json`, `preparation-probe-after.json` and probe source preserve
the observations. No public matching failure is falsely attributed to the old
scalar engine.

## One public source comparison, one noisy-row confirmation

Frozen product `9ea498ad` versus final `4d89398a`, same SDK 10.0.401 and observed
.NET 10.0.14. Isolated load contexts bind each Core/PythonRe/PCRE2 closure;
actual PE/PDB identities are recorded. All public results/checksums are checked.
The warm comparison pins one processor, warms each version for 2.5 s/lane,
calibrates toward 40 ms and alternates nine pairs. Ten public lanes, 62.26 s;
no concurrent tests/builds. These are source observations, not actual-package,
cumulative release or CPython/.NET ranking claims.

| Public operation | Initial median change | Before/after B/op |
| --- | ---: | ---: |
| PythonRe finite family Count | -8.54% | 112 / 112 |
| PythonRe finite family FindAllToUtf8 | -0.36% | 9,192 / 9,192 |
| PCRE2 finite family Count | -0.88% | 1,984 / 1,984 |
| PCRE2 finite derived-language Count | +0.76% | 112 / 112 |
| PythonRe captured-literal IsMatch, 8 KiB Unicode prefix | -72.40% | 2,192 / 0 |
| Same captured literal with finite controls | -26.20% | 2,304 / 112 |
| PythonRe captured-reference detailed Search | -3.62% | 2,448 / 2,448 |
| PythonRe finite prefix Match | -1.37% | 1,792 / 1,792 |
| PythonRe finite DotAll FullMatch | -6.46% | 3,664 / 3,664 |
| Unchanged Core compiled-literal Count | +11.15% | 0 / 0 |

The unchanged control moved materially and finite-family Count was noisy. The
single allowed confirmation is limited to PythonRe family Count, PCRE2
derived-language Count and that control: seventeen pairs, reversed first, 21.62 s.
Medians are +2.33%, -2.18% and -13.11%, with unchanged allocations. Raw spreads
remain substantial (control before spread 7.73x), so small timing differences are
inconclusive. No further campaign was run. No affected median shows a slowdown
above 10%; the moving control prevents an exact-equivalence or settled small-cost
claim. The clear captured-literal allocation reductions and route change have
direct evidence. Cumulative/package acceptance remains deferred.

Construction is separate diagnostic evidence: exactly sixteen instances per
case/version, no self-calibrated dynamic-construction loop. After the first
instance, median allocated bytes per constructor are:

| Constructor | Before | After | Difference |
| --- | ---: | ---: | ---: |
| PythonRe finite family | 94,848 | 50,480 | -44,368 B |
| PCRE2 finite family | 88,232 | 44,008 | -44,224 B |
| PCRE2 finite derived language | 94,760 | 86,696 | -8,064 B |
| PythonRe distinct captured literal | 63,496 | 65,336 | +1,840 B |

The last constructor now prepares correct distinct Core capture identities and
usable native discovery; it does not share the old accidental merged capture.
Constructor time samples, including the first instance, are retained but do not
establish matching throughput. The confirmation does not repeat construction.

## Preserved evidence and remaining scope

All 18 protected snapshots, RC1 files and original TODO handoffs retain exact
SHA256 bytes. PLAN/history remain ignored. No package/snapshot refresh; immutable
published/staged artifacts were not modified. Actual `Unsafe.SizeOf` remains.

No required implementation remains in this milestone. Deferred: the existing
out-of-process BDN ancestor/archive-discovery issue; rechecking the VM's earlier
disk/access state before VM-dependent work; a fresh immutable common-version
package and downstream/platform acceptance; actual-package finite costs and the
cumulative 10% gate against fixed `844dcbad`. The separate grammar-level optional
Core retention experiment and closed cleanup campaigns remain deferred.
