# Release notes

## Unreleased — 0.5.0

- Target .NET 10 and .NET 11 in all three packages. Benchmarks default to .NET 11;
  the current pinned SDK/runtime is RC3, with historical measurements labeled.
- Compile UTF-8 patterns and parse UTF-8 replacement templates directly across
  Core, PythonRe and PCRE2. Owned bytes and admitted cache keys replace mandatory
  whole-pattern/template strings; explicit text interoperability remains.
- Add UTF-8 capture-name lookup, validated capture counts and caller-buffer
  pattern metadata without exposing mutable program storage.
- Add caller-funded Core and PCRE2 program owners with separate working admission,
  deterministic close and byte-template replacement into caller-owned destinations.
  Their bounded cache maxima are conservatively prepaid. Controls cover modeled
  major capacity and overlap, not total heap/RSS; execution defaults remain unlimited.
- Extend PythonRe hosting with effective flags and structured errors, funded
  compilation/cache storage, native `Replace`/`Subn` and retained substitution
  output with explicit disposal.
- Add persistent memory cursors, byte-range captures and retained result receipts.
  Current-call tokens, original cancellation and absolute deadlines preserve
  cursor/result lifetimes across advances and program closure.
- Reduce live-token ASCII Boolean matching scratch through existing metered
  native plans. Reuse proved literal/category/count and bounded candidate ranges
  for matching, preserving strict full-subject validation and resource controls.
- Reject impossible bounded scalar-gap candidates using short ASCII tail prefixes.
  Corpus Count and finite/deadline operations improve; single-byte tails can cost
  more. Strict validation, captures and bounded interruption remain.
- Reuse scalar repeat execution for unfolded literal runs, reducing nullable
  FindAll and Count scratch. Capture-inside, overlapping-tail and RTL operations
  can cost more; numeric work/depth requirements can decrease.
- Avoid unnecessary repeat frames for proved nonempty bounded word runs before
  positive boundaries. Date recognition and finite controls use less time and
  temporary storage. Excluded ASCII separators give bounded word Count the same
  frame savings; declined proofs can cost more.
- Speed up repeated-segment Count with Unicode separators and declaration searches
  by skipping impossible word-interior starts, preserving strict validation and
  existing cancellation and resource authority.
- Speed up mixed-text keyword-family Count with the existing checked SIMD prefix
  filter. Miss scans and dense matches can cost more; the additional compiled
  search storage is admitted through the existing resource controls.
- Speed up multiline searches beginning with a mandatory ASCII literal using
  bounded checked line-start discovery. Finite/live-token and deadline operations
  benefit too; sparse and false-prefix misses can cost more.
- Reuse the existing bounded ASCII dotted-decimal boolean executor in PythonRe.
  Eligible repeated IsMatch calls use less time and scratch; retaining the
  admitted Core root increases resident preparation and construction costs.
- Skip impossible ASCII starts in PythonRe atomic and possessive searches while
  retaining complete atomic verification, captures and resource controls.
- Skip proved ASCII word-interior candidates after PythonRe VM admission.
  Date-like misses improve; Unicode-prefix and boundary-only operations can cost
  more. Strict validation, captures and interruption authority remain.
- Reuse admitted literals for capture-free repeated-literal PythonRe value
  FullMatch, reducing execution work and temporary storage while preserving
  strict validation, cancellation and finite resource limits.
- Compare exact PythonRe literals up to 256 bytes directly at a required Match
  or FullMatch start, retaining full validation and bounded invocation checks.
- Extend that bounded FullMatch verification to fixed literal tails using owned
  scalar instructions, avoiding VM arrays and cold preparation for those calls.
- Reuse validated subject-end coordinates for PythonRe scalar value results,
  avoiding redundant Unicode scans without changing strict input validation.
- Use the runtime's checked ASCII predicate for boolean input checks, reducing
  literal Count validation cost. Accept a complete ASCII validity proof before
  Unicode selection for faster matching; late Unicode after a long ASCII prefix
  can cost more. Resource controls remain enforced.
- Reduce shared invocation metadata, bounded output-copy overhead and finite-limit
  Core Match overhead while
  preserving numeric working limits, caller-funded admission and deterministic refunds.
- Reduce required-literal screening and empty confirmation overhead while
  retaining strict validation, matching and resource controls.
- Speed up capture-free PythonRe word-boundary Split and Count by avoiding
  impossible word-interior attempts and nonempty retries, preserving scalar
  progression and resource controls. No-boundary misses and empty input can cost
  more; execution allocations remain unchanged.
- Speed up scalar Count with class-delimiter candidate windows and shared
  capture-free PythonRe counting, preserving pinned Unicode and resource controls.
- Reduce shared PCRE2 VM overhead for controlled matching and capture operations.
- Skip impossible PCRE2 date-like candidates using existing bounded literal
  windows across word boundaries. Some controlled, capture and UCP workloads
  cost more; captures, scalar semantics and interruption remain.
- Speed up NonBacktracking batches and scan proved complete anchored-line Count
  roots without a viability table. Captures, cursors and bounded interruption
  checks remain. Work and working requirements change; recheck finite policies
  when upgrading.
- Preserve scoped Python verbose spaces and Unicode class/folding rules; quote
  complete scalars in PCRE2 classes, including escaped supplementary characters.
- Fix literal Count throwing on a trailing partial Unicode needle after a match.
- Keep execution owned, managed and checked. Core, PythonRe and PCRE2 retain their
  documented grammar/semantic boundaries; POSIX and complete profile equivalence
  remain outside this release.

## 0.4.0 — 2026-10-27

Published on NuGet: [Core](https://www.nuget.org/packages/Lokad.Utf8Regex/0.4.0),
[PythonRe](https://www.nuget.org/packages/Lokad.Utf8Regex.PythonRe/0.4.0) and
[PCRE2](https://www.nuget.org/packages/Lokad.Utf8Regex.Pcre2/0.4.0).

- **Breaking:** matching uses Unicode scalars over strict UTF-8, with primary byte
  coordinates. Dot consumes one complete scalar; malformed UTF-8 anywhere in the
  subject is rejected. UTF-16 coordinates remain derived conveniences.
- Core, PythonRe and PCRE2 use owned managed execution, replacing BCL matching
  fallbacks. Core retains .NET-style grammar and its supported NonBacktracking subset.
- Instances support synchronous cancellation, one absolute invocation deadline,
  and finite work, depth, working-capacity and result-capacity limits. Defaults
  remain unlimited; byte caps cover modeled capacities rather than process memory.
  Compile provides separate construction limits, timeout and cancellation.
- Captures, references, lookarounds, RTL and empty-match advancement preserve scalar
  boundaries. PythonRe follows its documented capture numbering, completion metadata
  and result shapes; PCRE2 follows its separate grammar and capture-slot contract.
- Owned results survive cursor advance/disposal. Borrowed inputs require a live owner
  and unchanged bytes. Replacement writers accept scoped local spans.
- Superseded UTF-16/fallback machinery and unchecked engine access are removed.
  Actual Unsafe.SizeOf queries remain.

Upgrade selected packages together; adapters require the matching Core version.
Read the [migration guide](docs/MIGRATION.md) and profile specifications for
supported behavior and resource-control scope.

## 0.3.0 — 2026-08-28

[Published on NuGet](https://www.nuget.org/packages/Lokad.Utf8Regex/0.3.0).

- .NET 10 matching, counting, enumeration and replacement over UTF-8 spans.
- .NET Regex semantics with UTF-16 coordinates and byte-aligned UTF-8 fast paths.
