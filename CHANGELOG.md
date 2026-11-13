# Release notes

## Unreleased — 0.5.0

- PCRE2 follows native UTF-mode checking from the start offset minus maximum
  lookbehind through the subject end, with the existing explicit trusted bypass.
  Byte-only discovery no longer prepares whole-subject UTF-16 coordinates.

- Target .NET 10 and .NET 11 in Core and all three adapters. The stable .NET 11
  runtime is the new benchmark baseline; historical measurements keep their
  runtime labels. Adapters require the exact matching Core package version.
- Add immutable `inputValidation` and read-only `InputValidation` in Core and
  PCRE2. Core `Validate` remains the default whole-subject UTF-8 check;
  `AssumeValid` omits that pass when the caller guarantees valid, unchanged
  borrowed UTF-8. Operations and cursors inherit the policy. Pattern/replacement
  checks, managed bounds, scalar matching and resource controls remain active.
- PythonRe models Python `re` over `str`; PythonRe and POSIX consume already-valid
  UTF-8 text without regex-operation validity passes. Validate external bytes at
  the text boundary. The earlier development-only subject-validation options and
  properties in these adapters are removed; pattern/replacement checks remain.
- Add Lokad.Utf8Regex.Posix: a case-sensitive scalar ERE subset with
  leftmost-longest matching and hierarchical subexpression priority. Match into
  caller-owned capture spans, count, or iterate with a disposable cursor. Named
  classes are fixed ASCII; locale collation, backreferences and lookarounds are
  excluded. Proven first-acceptance cases keep their direct execution; general
  captures select closed subexpression intervals after whole-match discovery.
  Interval memo capacity can be quadratic and whole-match search can still be
  expensive. Worst-case linear matching is not promised.
- Add direct UTF-8 pattern/replacement parsing, byte capture-name lookup and
  caller-buffer pattern metadata across Core, PythonRe and PCRE2. Explicit text
  interoperability remains; owned byte keys avoid mandatory whole-pattern strings.
- Add caller-funded Core/PCRE2 program owners and byte-template replacement into
  caller-owned destinations. Extend PythonRe hosting with effective flags,
  structured errors, funded compilation/cache storage and retained substitution
  output. Program owners and retained outputs support deterministic disposal.
- Add persistent memory cursors, byte-range captures and retained result receipts.
  Current-call tokens supplement original cancellation and absolute deadlines;
  returned results remain valid across cursor advances and program closure.
- Reduce repeated preparation, candidate verification, scalar-repeat dispatch,
  capture journaling, coordinate mapping and output copies. More finite-policy
  operations reuse owned checked span/SIMD paths. Complete terminal repeats omit
  unused assertion orchestration while assertion/history execution remains.
  Performance depends on the operation and input policy; some capture, folded,
  reverse, dense-match and controlled workloads use more time or memory.
- Preserve scoped Python verbose spaces and Unicode class/folding rules; quote
  complete scalars in PCRE2 classes, including escaped supplementary characters.
  Fix literal Count on a trailing partial Unicode needle after a match. Reject
  null replacement evaluators even on misses; an already-cancelled explicit token
  retains precedence.

Resource limits cover modeled major capacity and growth overlap, not total heap,
RSS or arbitrary caller retention; execution defaults remain unlimited. Numerical
work, depth and working/result requirements depend on the execution route and can
change between versions. POSIX depth now includes explicit interval dependencies;
its major memo growth is admitted conservatively before allocation. Recheck finite
policies when upgrading. Temporary loans refund synchronously; original-token
cancellation, deadlines, captures and conditional whole-input validation remain.

No blanket speedup or complete .NET/Python/PCRE2/POSIX equivalence is promised.
See the [migration guide](docs/MIGRATION.md), [Core](docs/SPEC.md),
[PythonRe](docs/SPEC-PYTHONRE.md), [PCRE2](docs/SPEC-PCRE2.md) and
[POSIX](docs/SPEC-POSIX.md) specifications for contracts and limitations.
Execution remains owned, managed and checked.

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
