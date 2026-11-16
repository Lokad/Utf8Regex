# Release notes

## Unreleased — 0.5.0

- Support .NET 10 and .NET 11 across Core and all three adapters. .NET 11 is the
  primary benchmark baseline. Adapters require the exact matching Core version.
- Add immutable `inputValidation` and read-only `InputValidation` in Core and
  PCRE2. Core defaults to strict whole-subject validation; PCRE2 follows native
  UTF-mode checking from start minus maximum lookbehind through subject end.
  `AssumeValid` omits subject checking when the host guarantees valid unchanged
  UTF-8. Pattern/replacement checks and managed bounds remain independent.
- PythonRe models Python `re` over `str`; PythonRe and POSIX require already-valid
  UTF-8 text and omit regex-operation validity passes. Validate external bytes at
  the text boundary. Required coordinates and output may still inspect text.
- Add Lokad.Utf8Regex.Posix: a scalar ERE subset with leftmost-
  longest matching and hierarchical capture priority. Match into caller-owned
  capture spans, count, or use a disposable memory cursor. Named classes are fixed
  ASCII; locale collation, backreferences and lookarounds are excluded. General
  capture selection may use quadratic interval storage; linear matching is not
  promised.
- Add `PosixCompileOptions.IgnoreCase`: invariant scalar case equivalence with
  leftmost-longest matching, hierarchical captures and original byte offsets.
- Preserve POSIX `Newline` matches when a whitespace run must rewind before LF
  to satisfy `$`. Proved flat runs reuse scalar capture selection and reject
  impossible starts before recording capture events.
- Extend proved POSIX runs to single repeats and fixed scalar pieces, avoiding
  unnecessary rewind storage. Valid-text coordinate projection reuses bounded
  counting. Numerical work/depth/working requirements can decrease on these routes.
- Add direct UTF-8 pattern/template APIs, byte capture-name lookup and caller-
  buffer pattern metadata in Core, PythonRe and PCRE2. Add caller-funded program
  ownership, persistent memory cursors and explicit retained-result/output
  admission. Existing value cursors keep their documented copy/alias contracts.
- Core value `Match` paths reuse subject validation and avoid discarded whole-
  input UTF-16 metadata for exact literals and literal families. Returned byte/
  UTF-16 coordinates remain relative to the original input; `Validate` still
  rejects malformed tails.
- Finite-limit value `Match` reuses bounded search for literal families of up to
  eight alternatives, reducing work and temporary storage on those routes.
  Detailed capture results retain their separate admission requirements.
- Reduce repeated preparation, search, capture traversal, coordinate scans and
  output copies. Results and numerical resource requirements depend on the route;
  some workloads use more time or storage. Recheck finite policies when upgrading.
- Preserve scoped Python verbose spaces and Unicode class/folding rules. Correct
  PCRE2 class quoting of supplementary scalars and literal Count on a trailing
  partial Unicode needle. Null replacement evaluators reject even on misses;
  explicit pre-cancellation retains precedence.

Resource controls admit modeled major capacity and growth overlap, not total heap,
RSS, arbitrary callbacks/getters or host retention. Defaults remain unlimited.
Work/depth/working/result requirements can change between versions; temporary loans
refund synchronously. Original-token cancellation, absolute deadlines, scalar
semantics, captures and retained-result lifetimes remain enforced.

Execution remains owned, managed and checked. No blanket speedup or complete
.NET/Python/PCRE2/POSIX equivalence is promised. See the
[migration guide](docs/MIGRATION.md) and [profile contracts](README.md#choose-a-profile).

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
