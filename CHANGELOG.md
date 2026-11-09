# Release notes

## Unreleased — 0.5.0

- Target .NET 10 and .NET 11 in Core and the three adapters. Benchmarks default to .NET 11;
  the pinned SDK/runtime is currently RC3. Historical results keep their runtime
  labels. Adapters require the exact matching Core package version.
- Add the optional Lokad.Utf8Regex.Posix package: a case-sensitive scalar ERE
  subset with leftmost-longest matching and hierarchical capture priority.
  Match into caller-owned capture spans, count, or iterate with a disposable
  cursor under the existing cancellation and finite resource policies. Named
  classes are fixed ASCII; locale collation, backreferences and lookarounds are
  excluded. Whole-only operations omit unused capture histories; coordinate
  projection reuses validated ranges, and ASCII classes reuse Core's masks.
  Worst-case linear matching is not promised.
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
- Improve literal, class/repeat, assertion, declaration and token matching through
  checked span/SIMD search and scalar execution. More finite-policy operations
  reuse proved fast paths, and captures/coordinates/output avoid unused scratch,
  journals and copies. PythonRe Count, Search, Split and FullMatch share these
  improvements while retaining ordered scalar verification and interruption.
  Class/delimiter Count can reject impossible windows before VM admission.
- Reduce repeated POSIX root-history scans. PCRE2 commits word-only repeats
  before required boundaries when shorter endpoints cannot succeed, avoiding
  unused rewind workspace while retaining cancellation and working admission.
  Literal string substitutions reuse validated output sizes and decode no-match
  input directly, avoiding an intermediate byte array. Byte outputs remain owned.
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
[Core](docs/SPEC.md), [PythonRe](docs/SPEC-PYTHONRE.md),
[PCRE2](docs/SPEC-PCRE2.md) and [POSIX](docs/SPEC-POSIX.md) specifications for
API contracts and profile limitations. Execution remains owned, managed and checked.

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
