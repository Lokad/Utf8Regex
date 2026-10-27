# Non-null result defaults and reliable focused validation

Review baseline 8724b586. Library fix 070a8013, runner correction dffea42e,
style-test cleanup and final source 589bb885; development 0.4.0-rc.3-dev.
No new package/candidate, historical snapshot refresh or publication.

## Delivered changes

- PythonRe `Utf8PythonMatchData.ValueText`, `Utf8PythonGroupMatchData.ValueText`
  and detailed `Groups`/`NameEntries` getters normalize zero-initialized storage
  to shared empty values. This fixes actual Search/Match/FullMatch detailed
  misses, including `result.Value.ValueText.Length`, and `default(T)`. Existing
  init setters and supplied-array identity remain. Redundant null checks in the
  detailed helpers are removed. One shipped C# file changes; public API shape,
  successful results, discovery, coordinates and admission remain unchanged.
  XML, profile README/spec and unreleased notes describe the corrected contract.
- Local tests, existing CI commands and the detached package helper use VSTest's
  native `RunConfiguration.TreatNoTestsAsError=true`. A nonexistent filter now
  returns 1 instead of 0; a known eight-case filter returns 0. The option is last
  after other test arguments. No output scraping, parser, switch or qualification
  framework was added. Project builds, binary reuse and lock-only recovery remain.
- Removed three blanket style tests: mandatory internal parameters, source-text
  null-suppression scan and tuple-domain ban. Their exclusive tuple helper and
  reflection import are deleted: 69 test/helper lines. Actual dependency,
  architecture/direction, scalar, resource, managed-safety, expansion and seeded
  wide-repeat tests remain. This does not alter the rejected FullMatch decision.

The scope is the four demonstrated result members, not normalization of every
public adapter data shape. The default getters need no constructor or per-call
payload. No new result wrapper, owner, matcher, layout or compatibility mode.

## Validation

[Summary](validation.json), [scope audit](scope-checks.json),
[source CI](ci-source.json).

- Focused Release PythonRe: 929 pass, including defaults, optional/retained
  captures, ownership, finite result policies and public API/member inventories.
- Focused Release Core: 74 pass, including retained architecture, seeded wide
  repeats, public API and dependency inventory. Only the three identified style
  gates were removed; seeded production-state witnesses are unchanged.
- The installed runner's native empty-filter probe exits 1. The final test.ps1
  empty-filter probe exits 1 and its matching eight-case filter exits 0. Both
  PowerShell scripts parse successfully. Existing CI and package entry points
  forward that same native setting; no package qualification was repeated merely
  to exercise an argument already proved with the installed runner.
- Final Windows/Linux CI 40493641055
  is green at 589bb885, including complete profile suites and both disabled-
  dynamic-code steps with the new zero-test policy. Work continued while CI ran;
  status checks were spaced apart. No build-server shutdown or local full matrix.
- [Protected-input checks](immutable-inputs.json) preserve original handoffs,
  benchmark snapshots and frozen package/symbol/archive bytes. The incoming
  untracked POSIX handoff was also left untouched.

Strict whole-subject UTF-8/scalars, captures, NB/RTL, cursor/result aliases,
native routes, original synchronous cancellation, absolute deadlines and useful
work/depth/working/result controls retain their implementation. This correction
is development-only; a future release needs fresh immutable candidate bytes.

## Bounded public costs: tiny miss row remains inconclusive

[Predeclared protocol](PREDECLARED.md), [initial samples](public-cost.json),
[sole confirmation](public-cost-confirmation.json), [preflight](preflight.json),
[DLL/PDB identities](measured-identities.json). The existing
[ThirtySixthReview host](../ThirtySixthReview-20261026/cost-host.cs.txt) and
[host project](../ThirtySixthReview-20261026/cost-host.csproj.txt) were reused
unchanged. The new public lane source/project are beside this receipt.

Ubuntu 24.04.4 / .NET 10.0.12, default tiers. Each version/lane warms for 2.5 s,
calibrates toward 40 ms and retains outputs/checksums. Independent preflight
checks coordinates, names, optional captures and full output values. Identical
defensive consumer reads make original null misses and corrected empty defaults
comparable. These are complete public calls plus consumer getter reads, not
isolated-kernel timings. Allocations are unchanged in every measured lane.

Initial 54.50 s / nine alternating pairs used CPU2 (0x4). The unlimited miss row
was +11.00%, near the 10% gate, while the unchanged control moved +3.13% and some
rows varied about 5–7%. The predeclared sole 60.81 s / 17-pair reversed-first
confirmation preserved the entire nine-row catalog/context, using CPU6 (0x40)
selected by the unchanged processor policy. No subset/filter or tuning was used.

| Public operation | Initial unlimited / finite+live | Confirmation unlimited / finite+live |
| --- | ---: | ---: |
| Captured short SearchDetailedData hit | +5.36% / +2.18% | -4.98% / -0.37% |
| SearchDetailedData miss | **+11.00%** / -2.62% | **-14.87%** / -9.11% |
| Optional Unicode FullMatchDetailedData | +2.31% / +1.34% | -2.21% / -2.35% |
| Captured 64-result FindIterDetailed | -1.04% / +2.23% | +1.22% / -1.29% |

The unchanged Core compiled literal Count control is +3.13%, then -0.16%.
The unlimited miss moves from 267.38 -> 296.79 ns initially to 291.95 -> 248.54 ns
in confirmation. The confirmation has 1.23x within-lane spread on the short hit
and 1.20x on the unlimited miss, versus at most 1.066x initially. Attribution of
the noise/reversal to processor, order or JIT effects is unproved. The first adverse observation is not
superseded by the second. **This row and complete cost acceptance remain
inconclusive**, without a performance-equivalence or universal <=10% claim.
All other representative public rows fit 10% in both observations.

The correctness fix remains because it fulfills the existing public non-null
contract. Its cost uncertainty is deferred to future delivery qualification;
this milestone stops after the declared comparison and sole confirmation.
No third run, code tuning, package matrix or historical Status refresh.
Frozen RC2's separate cumulative gate also remains
[inconclusive](../ReleaseCandidate2-20261026/ERRATA.md); neither gate is waived.
