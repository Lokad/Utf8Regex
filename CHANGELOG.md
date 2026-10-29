# Release notes

## Unreleased — 0.5.0

- Target .NET 10 and .NET 11 across Core, PythonRe and PCRE2.
- Use .NET 11 by default for benchmark runs; historical figures retain their measured runtime.
- Reuse scalar-aligned folded literal-family routes on mixed UTF-8 subjects and
  prune impossible leading-whitespace Count starts before scalar VM verification.
- PCRE2 Count checks an existing required literal before allocating slice VM
  scratch, avoiding repeated attempts on subjects without that literal.
- Admit boundary-disjoint ASCII prefix families to existing structural routes;
  preserve identifier-tail bounds and narrower whitespace classes in native plans.
- Filter PythonRe search starts through convergent zero-width prefixes, retaining
  profile verification and Unicode behavior on validated UTF-8.
- Reuse the anchored leading-digits/tail grammar for Boolean matching without
  capture workspace, preserving Unicode-prefix verification and finite limits.
- Reuse the proved balanced-block counter with finite work/depth/working limits;
  deeper nesting retains scalar execution under the original invocation.
- Reuse complete simple-pattern branches for captured Unicode-class Boolean
  searches on ASCII subjects; mixed subjects retain scalar verification.
- Reuse the admitted dotted-decimal finder for Boolean searches with bounded
  work and original interruption, avoiding scalar capture workspace.
- Lower Python word boundaries directly to the owned boundary primitive,
  preserving pinned Unicode/strict ASCII membership and the non-boundary
  empty-subject rule.
- Reuse the complete anchored-email proof for bounded ASCII Boolean matching;
  Unicode aliases, long inputs and capture operations retain scalar execution.
- Filter mandatory ASCII literal/set prefixes after nullable whitespace before
  scalar Count verification, retaining capture-sensitive candidate windows and
  original resource limits and interruption.
- Guide scalar Count through bounded gaps after class/whitespace prefixes,
  retaining scalar gap lengths, Unicode predicates and VM match selection.
- Reuse complete number and trimmed optional-prefix ranges for bounded ASCII
  value Match; detailed captures, Unicode subjects and long inputs retain scalar
  execution.
- Reuse complete query URL ranges for bounded ASCII value Match, metering prefix
  and optional-parameter retries under the original invocation.
- Reuse the complete hex-color grammar for ASCII value Match without capture
  workspace, retaining strict validation and original invocation limits.
- Check a second proved mandatory ASCII scalar before Count's VM attempts,
  preserving scalar verification and bounded candidate-search admission.
- Avoid child-VM storage for small capture-free ASCII literal assertions,
  preserving logical assertion depth and original invocation limits.
- Require greedy, disjoint classes in native repeated-segment plans and retain
  the previous endpoint when an additional segment fails.
- Improve scalar Count candidate searches for exact literal pairs, retaining
  bounded metering, ordered alternatives and full VM verification.
- Reject impossible bounded Dot tails before greedy endpoint replay, retaining
  scalar limits, captures and original resource/interruption authority.

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
