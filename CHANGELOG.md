# Release notes

## Unreleased

- Remove `MaxWorkSteps` from execution and construction policies and
  `MatchLimit` from PCRE2's managed limits. Use a cancellation token for
  cooperative interruption. Timeouts, depth limits and storage admission
  remain available.
- POSIX reports recognized syntax outside its ERE subset through
  `PosixUnsupportedSyntaxException`, a subtype of `ArgumentException`.
- Add opt-in GNU basic and extended backreferences to POSIX longest matching.
- Align PCRE2 `\X` boundaries with native 10.47 for Indic conjuncts,
  regional indicators followed by marks or joiners, and `Prepend` before pictographs.
- Preserve regional-indicator pairing when PCRE2 `\X` starts inside a run.
- Accept numbered PCRE2 callouts without callbacks.
- Keep subject anchors, boundaries and nested lookaheads visible inside
  PCRE2 lookbehind assertions.
- Add PCRE2 `MatchInvalidUtf8` to search valid fragments of malformed subjects
  with original byte offsets.

## 0.5.0 — 2026-11-27

Published on NuGet. [Core](https://www.nuget.org/packages/Lokad.Utf8Regex/0.5.0) ·
[PythonRe](https://www.nuget.org/packages/Lokad.Utf8Regex.PythonRe/0.5.0) ·
[PCRE2](https://www.nuget.org/packages/Lokad.Utf8Regex.Pcre2/0.5.0) ·
[POSIX](https://www.nuget.org/packages/Lokad.Utf8Regex.Posix/0.5.0)

### Added

- Support .NET 11 alongside .NET 10. Benchmarks use .NET 11.
- Add `Lokad.Utf8Regex.Posix` for a POSIX ERE subset with Unicode scalar
  matching, leftmost-longest selection and hierarchical capture priority.
- Extend managed scalar execution to more patterns and operations with
  cancellation, deadlines and finite limits.

### Upgrading from 0.4.0

- Upgrade Core and any adapters together. Adapters require the exact Core
  version.
- Validate external bytes where they become text. Matching trusts valid,
  unchanged UTF-8 subjects. Pattern and replacement-template checks are separate.
- Use byte offsets, UTF-8 templates and UTF-8 capture-name keys. Supply capture
  slots for synchronous matches or use memory cursors for snapshots across calls.
  PythonRe `Match` takes an explicit mode for search or full matching. Core
  `EnumerateMatches` accepts a scalar-aligned byte start.
- Replace static Core and PCRE2 matching calls with reusable instances.
  Manage compiled instances in the host in place of the Core global pattern
  cache. Set timeouts, limits and optional program funding through
  `Utf8RegexPolicy`. Recheck finite limits after upgrading. Byte limits cover
  modeled capacities.

See the [profile contracts](README.md#choose-a-profile) for APIs and supported
semantics.

### Known issues

- Core can miss `ab` for the string pattern `(?x)(a b)`.
- Core can match `ABx` for `(?i)a(?-i)bx` despite the case-sensitive suffix.

Both fixes are on `master`.

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

## 0.3.0 — 2026-08-28

[Published on NuGet](https://www.nuget.org/packages/Lokad.Utf8Regex/0.3.0).

- .NET 10 matching, counting, enumeration and replacement over UTF-8 spans.
- .NET Regex semantics with UTF-16 coordinates and byte-aligned UTF-8 fast paths.
