# Release notes

## Unreleased — 0.5.0

- Target .NET 10 and .NET 11 in all three packages. Benchmarks default to .NET 11;
  the pinned SDK/runtime is currently RC3. Historical results keep their runtime
  labels. Adapters require the exact matching Core package version.
- Compile UTF-8 patterns and parse UTF-8 replacement templates directly across
  Core, PythonRe and PCRE2. Add UTF-8 capture-name lookup and caller-buffer pattern
  metadata. Owned byte keys replace mandatory whole-pattern/template strings;
  explicit text interoperability remains.
- Add caller-funded Core and PCRE2 program owners, working admission and byte-template
  replacement into caller-owned destinations. Extend PythonRe hosting with effective
  flags, structured errors, funded compilation/cache storage and retained substitution
  output. Program owners and retained outputs have deterministic disposal.
- Add persistent memory cursors, byte-range captures and retained result receipts.
  Current-call tokens supplement original cancellation and absolute deadlines;
  returned results remain valid across cursor advances and program closure.
- Improve literal, candidate-window, class/repeat, declaration and token matching
  through existing checked span/SIMD search and scalar execution. Reuse more native
  plans under finite limits, avoid unnecessary backtracking/capture/coordinate
  scratch and reduce small capture-trail allocations. PythonRe Count, Split and
  FullMatch also reuse proved scalar/native work and materialized result buffers.
  Global PythonRe matching skips proved impossible empty retries and word-interior
  starts while retaining bounded interruption and resource controls.
  Completed single-capture references avoid unnecessary opening journals, and
  bounded repeat proofs reuse pure predicate dispatch with unchanged work charges.
  Complete ASCII-range Count skips nonmembers, and PythonRe captured
  class/delimiter searches avoid repeated impossible starts. These routes can
  require less numerical work and temporary capacity under finite policies.
  Use checked SIMD in bounded validator selection, preserving its threshold,
  validation policy and interruption checkpoints.
  Reuse bounded literal value search under finite limits, live cancellation tokens
  and deadlines, avoiding unused miss metadata and scalar cursor preparation.
  Reuse positive ASCII mask hits in category repeats while retaining full scalar
  predicates for mask misses and unchanged per-scalar work charges.
  Borrow immutable class instructions during scalar classification to avoid
  repeated instruction copies.
  Borrow invocation values through Python cursor construction, retaining one
  owned state copy and the original controls.
  Narrow direct class/delimiter candidate windows to the actual scalar class,
  avoiding impossible starts across nonmember whitespace. Explicit whitespace
  and gap prefixes retain their conservative windows; required work can decrease.
  Reuse prepared prefix tries for short PythonRe searches under the same resource
  and interruption authority, avoiding repeated case-expanded prefix checks.
  Extend existing class/delimiter windows to short captured searches, avoiding
  failed prefix retries before detailed-result projection.
  Count native literal value coordinates over already validated scalar ranges,
  avoiding repeated validation while keeping bounded resource/interruption checks.
  Vectorize the common UTF-8 validator's existing lead-density census while
  preserving its exact counts, selection window, scalar tail and fallback.
- Preserve scoped Python verbose spaces and Unicode class/folding rules; quote
  complete scalars in PCRE2 classes, including escaped supplementary characters.
  Fix literal Count throwing on a trailing partial Unicode needle after a match.
- Reject null replacement evaluators even on misses; an already-cancelled explicit
  token retains precedence.

Performance changes include local trade-offs: some long capture histories,
assertions, reverse, folded, dense-match and controlled workloads can take more
execution time or memory. No blanket speedup or complete profile equivalence is
promised. Refreshed public-operation tables and focused receipts retain allocations,
uncertainty and adverse observations.

Resource limits cover modeled major capacity and growth overlap, not total heap/RSS
or arbitrary caller retention; execution defaults remain unlimited. Numerical work,
depth and working/result requirements depend on the execution route and can change
between versions. Recheck finite policies when upgrading. Admission still precedes
covered acquisition, temporary loans refund synchronously, and strict whole-subject
UTF-8 validation, scalar matching, captures and interruption contracts remain.

See the [migration guide](docs/MIGRATION.md) and the
[Core](docs/SPEC.md), [PythonRe](docs/SPEC-PYTHONRE.md) and
[PCRE2](docs/SPEC-PCRE2.md) specifications for API contracts and profile limitations.
Execution remains owned, managed and checked. POSIX remains outside this release.

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
