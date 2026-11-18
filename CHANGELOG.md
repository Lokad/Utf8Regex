# Release notes

## Unreleased — 0.5.0

### Platform and profiles

- Add .NET 11 alongside .NET 10. Benchmark comparisons use .NET 11 as their
  primary runtime.
- Add `Lokad.Utf8Regex.Posix` for a scalar POSIX ERE subset with leftmost-longest
  matching, hierarchical capture priority and invariant optional case folding.
- All adapters require the exact Core package version.

### Upgrading

- All profiles require already-valid, unchanged UTF-8 subjects. Validate external
  bytes where they become text. Matching no longer validates the entire subject.
- Use byte offsets and result ranges throughout. Use caller-owned capture slots
  for synchronous matches and memory cursors for capture snapshots that live
  across calls.
- Compile expressions in the host and pass `Utf8RegexPolicy` for timeouts,
  limits and program funding. Remove the static Core and PCRE2 matching
  conveniences and the Core global pattern cache.
- Use UTF-8 replacement templates and capture-name keys. Decode, collect or
  compute custom output in the host. PythonRe exposes numbered captures and
  `LastIndex`; PCRE2 exposes final capture slots, MARK and its supported partial
  probes.
- See the [migration guide](docs/MIGRATION.md) for current signatures and the
  [profile contracts](README.md#choose-a-profile) for supported semantics.

### Execution

- Use the general owned scalar engines for more patterns, including calls with
  limits, deadlines and caller-provided memory allowances. Improve candidate
  discovery, repeat execution and reuse of assertion scratch.
- Preserve original-token cancellation, scalar progress, byte coordinates and
  retained result lifetimes. Limits cover modeled work and capacities. Recheck
  finite settings after upgrading.
- The libraries remain managed and bounds-checked. Benchmark rows record their
  measured configuration and workload. Compatibility follows each profile's
  documented scope.


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
