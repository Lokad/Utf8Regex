# Twenty-eighth review, 2026-10-23

Checkout: 9971a8c0. Read-only product/PLAN review, concentrating on coordinate
entry, release-check fidelity and human-facing performance tooling. Scratch
projects reference existing Release DLLs; no product build, package qualification,
throughput experiment, full-suite rerun or GitHub request was made.

## 1. Ordinary PythonRe byte-offset collection allocates a discarded UTF-16 map

`Utf8PythonRegex.Utf8.cs:137-145` routes an unlimited, uncancelable, infinite-timeout
nonzero byte start through `GetUtf16OffsetOfBytePrefix`, then Core's public
`EnumerateMatchesFromUtf16Offset`. The former scans the prefix to obtain a UTF-16
index. Core `PrepareOffsetInput` (`Utf8Regex.OffsetEnumerationInvocation.cs:11`)
validates the subject and allocates its complete non-ASCII boundary map. The
native-offset branch immediately discards/releases that map at lines 37-39.

The existing `EnumerateMatchesForResult` byte-offset path (line 98) already used
by live-token/finite calls checks native eligibility before acquiring a map.
This is live duplicated coordinate preparation, not an unused public UTF-16 API
or a reason to remove derived interoperability coordinates generally.

`offset-witness.json` contains 18 public allocation observations: two patterns,
three 64-KiB-class prefixes, and unlimited/live-token/four-finite policies. On
prefix `é` plus 65,535 ASCII characters, followed by ` foo bar`, `foo` produces
the same result/coordinates but FindAllToUtf8 allocates:

| Current public route | Allocated bytes/call |
| --- | ---: |
| Unlimited, no token | 270,608 |
| Unlimited, live uncanceled token | 176 |
| Four finite controls | 328 |

For prefix-free `foo|bar`, the corresponding figures are 270,648 / 216 / 496.
The all-ASCII control is 176 / 176 / 328 for `foo`; the Unicode-heavy prefix
shows the map cost scaling with UTF-16 length. Values and primary/derived offsets
are checked before observing allocations. These are current-engine allocation
observations, not throughput or before/after improvement measurements.

Recommendation: use the existing byte-offset invocation route for this nonzero
start, preserve the zero-offset native shortcut, then remove the now-single-use
prefix conversion helper if no consumer remains. Keep complete UTF-8 validation,
scalar alignment, returned coordinates and the existing invocation authority.
Qualify native literal/family offsets, supplementary boundaries, malformed tails
and controlled calls; one bounded public comparison with an unchanged zero-offset
control would suffice during implementation. No new API, result mode or mapper.

## 2. Three duplicated API formatters miss material contract changes

CorePublicApiSnapshotTests, PythonRePublicApiSnapshotTests and
Pcre2PublicApiSnapshotTests each duplicate essentially the same formatter.
Core lines 29/42/50/73/85 illustrate the omissions: enum names without values,
field names without constants, accessor existence without public visibility,
type kind without lifetime/interfaces/modifiers, and parameters without defaults.

The API witness invokes each actual compiled private FormatAssembly method on
two tiny independent assemblies. The formatter source is unchanged from the
compiled test producer to this checkout. All three produce IDENTICAL snapshots
despite all of these changes:

- Enum Enabled: 1 -> 64.
- Public constant DefaultLimit: 100 -> 200.
- Optional Run(limit) default: 100 -> 200.
- Capacity setter: private -> public.
- Surface: sealed IDisposable -> unsealed without IDisposable.
- ValueView: readonly ref struct -> ordinary struct.

`api-snapshot-witness.json` preserves both identical outputs. This identifies
blind spots in API compatibility checks, not a failure of exact package-byte,
symbol or shipped dependency inventories.

Recommendation: consolidate into one small shared test formatter and extend the
existing snapshots to capture the material values/accessibility/lifetime facts.
Use the small before/after fixtures to test the checker itself. Remove the three
copies' duplication; do not add another qualification framework or repeat package
matrices solely to update snapshot text. Preserve inventory of the assembly each
fixture actually consumes, including detached package runs.

## 3. Unreferenced benchmark summary can combine incompatible jobs

`bench-summary.ps1:51-57` groups only by CaseId and selects the first row for each
of Utf8Regex, DecodeThenRegex and PredecodedRegex. It neither pairs Job/other
parameters nor rejects ambiguous duplicate rows. There are no repository callers
or documentation references to this root helper.

The synthetic, correctly shaped six-row CSV has two jobs, each showing Utf8Regex
at 0.1x its baselines. Interleaved row order makes the helper select Utf8Regex
from job A and both baselines from B: it reports 100x slowdown. See
`benchmark-summary-witness.json` / `.log` and `multiple-jobs.csv`.
This is a report-parsing witness, not a new timing or a defect in the JSON-backed
published benchmark snapshots/paired CLI measurements.

Recommendation: retire the helper if no manual consumer needs it. Otherwise use
a small fail-fast check for ambiguous/mixed identities or pair the actual report
identity before ratios. Avoid a replacement report framework. This is maintenance
work, not a new release prerequisite.

## Priority and retained scope

Prioritize the demonstrated downstream allocation waste, then improve the existing
API check while deleting its duplication. The summary helper is a small cleanup.
No new matching-semantic defect or dormant executor was demonstrated in this pass.
Public result name arrays have real ownership/copying checks; foreign-waiter
interruption recovery and conservative admission remain useful and should stay.

The six inspected product DLL/PDB files, 18 protected historical artifacts, source
and PLAN remain unchanged. Only the five original TODOs remain untracked outside
ignored review material. Fresh common-version packages, isolated consumers and
actual-package/cumulative cost acceptance retain their separate release scope.
