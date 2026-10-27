# Release notes

## Unreleased — 0.4.0

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
