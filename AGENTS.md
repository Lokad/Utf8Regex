# Lokad.Utf8Regex

.NET 10/11 libraries matching Unicode scalars over strict UTF-8 spans, with primary
byte coordinates. Dot matches 😀 once across four bytes. UTF-16 coordinates are
derived interoperability, not matching units.

## Contracts and scope

- Follow [Core](docs/SPEC.md), [PythonRe](docs/SPEC-PYTHONRE.md) and
  [PCRE2](docs/SPEC-PCRE2.md) contracts. The ignored root PLAN.md holds active work.
- Preserve scalar boundaries, captures, RTL/NonBacktracking behavior, retained
  results/cursor aliases, resource admission and synchronous original-token
  cancellation/deadlines. Do not restore UTF-16 parity or dormant fallback engines.
- Shipped execution is fully managed and owned: no BCL matcher/results, native
  regex dependency or unchecked reads. Actual Unsafe.SizeOf<T>() is permitted.
  BCL Regex is a test/benchmark comparator only on overlapping semantics.

## Build and test

Use the SDK pinned by global.json (host install or ignored .dotnet/), and
`--tl:off` for stable output. With a local SDK, put .dotnet/ on PATH so direct
DLL launches and benchmark children use its runtimes. Prefer the repository scripts:

```powershell
./test.ps1 -Configuration Release -Project tests/Lokad.Utf8Regex.Tests/Lokad.Utf8Regex.Tests.csproj -Filter 'FullyQualifiedName~Utf8CoreWorkPolicyTests'
./test.ps1 -Configuration Release -Project tests/Lokad.Utf8Regex.Tests/Lokad.Utf8Regex.Tests.csproj -SkipBuild -Filter 'FullyQualifiedName~Utf8FiniteLiteralCountTests'
```

Use -Framework net10.0 or net11.0 for focused runs; the test default covers both.
Build affected projects during experiments; reuse matching binaries with
-SkipBuild. Zero discovered tests is failure. Reserve solution/platform/package
matrices for coherent delivery boundaries; do not repeat unchanged qualification.

Reuse build/compiler servers. Recover test hosts only after an observed file-lock
failure and only when their executable path is inside this checkout; command-line
mentions do not prove ownership. Leave unknown/outside hosts running. Normal test
and benchmark builds disable incidental packing.

## Benchmarks

Use ./bench.ps1 in Release, sequentially, with a focused filter or custom command.
Benchmarks default to net11.0; use -Framework net10.0 for compatibility controls.
Use warm throughput (typically -Job Short); Dry is smoke evidence, not parity.
Custom commands default to a 120-second outer cap; disable it only for preflighted
milestone refreshes. The cap returns 124 and owns only its launched process tree;
normal runs do not shut down servers or kill other benchmark hosts.

```powershell
./bench.ps1 -Configuration Release -Job Short -Filter '*Literal*'
./bench.ps1 -SkipBuild -CommandArgs '--measure-case','literal/sherlock-en','20'
./bench.ps1 -SkipBuild -CommandArgs '--measure-case-deep','lokad/imports/module-imports','20'
```

Use `./refresh-benchmarks.ps1 -Phase Resources` for a selected refresh phase.
The default runs one Release build, both-runtime tests and the complete .NET 11
catalogs, lifecycle/scaling, paired comparators and resource checks, reusing those
binaries. Phase logs and `refresh.json` go to `artifacts/benchmark-refresh/`.
Only a completed full run counts toward the 60-minute VM target. Benchmark children
share the remaining budget; on Linux wrap the runner in
`timeout --kill-after=5s 3600s pwsh -NoProfile -File ./refresh-benchmarks.ps1`
to hard-cap builds/tests too. Set PATH and UTF8REGEX_CPYTHON before starting.
The Verify phase requires matching frozen paired evidence; it deliberately rejects
old comparisons after source or binaries change.

- Compare equal-result public operations against both decode-then-Regex and
  predecoded Regex. Include finite-limit costs and allocations where relevant.
  Kernel/phase attribution is not a public speedup or benchmark Status.
- Bound comparisons and any confirmation before running them; use public-operation
  costs and unchanged controls rather than isolated-kernel estimates.
- JSON snapshots are authoritative: README.Benchmarks.json/README.Parity.json,
  PythonRe.Benchmarks.json and PCRE2.Benchmarks.json. Regenerate their Markdown
  through the existing rewrite/verify commands; never hand-edit generated tables.
  Prefer selective refresh and preserve historical identities/uncertainty.
  New Core/PCRE2 refresh lanes calibrate independently and retain raw durations;
  Core V3 samples below 20 ms remain unqualified. Python/.NET/CPython result
  batches consume checksums after timing. Keep lifecycle/scaling evidence when
  refreshing ordinary rows; package matrices stay at delivery boundaries.
- Python comparisons use an official CPython in a long-lived worker, excluding
  startup/compilation. Set UTF8REGEX_CPYTHON if needed. Qualification requires
  matching loaded binaries/PDB source checksums, clean relevant inputs and the
  existing paired protocol; do not refresh data merely to update provenance.
- PCRE.NET is a private benchmark-only native comparator. Keep it out of shipped
  projects and mark unsupported/result-incompatible rows with exclusion reasons.
  Command details live in the profile benchmark pages and CLI help.

## Package delivery

Use ./qualify-package-candidate.ps1 at a delivery boundary; Core/PythonRe is the
default, or select -Profiles Core,PythonRe,Pcre2. Require clean tracked source,
a new common version and exact adapter-to-Core pins. -SkipBuild requires matching
configuration/revision binaries for both targets; retain source/PDB/DLL/feed/cache/
lock checks for each asset and isolated consumer.

Qualified packages and symbols are delivered to artifacts/nuget/. Qualification
records under artifacts/package-candidates/<version> and historical published/
staged bytes are immutable. Platform, performance and original consumer acceptance
remain separate checks. Detached fixtures stage API snapshots and filter
Category!=SourceOnly; source CI keeps repository/layout/snapshot guards.
Core-only qualification must not require adapters or the benchmark executable.

## Files and documentation

Keep guides in docs/; README.md, CHANGELOG.md, AGENTS.md and ignored PLAN.md stay
at root. Use tmp/<task>/ for disposable logs/probes/scripts, then delete task
scratch. Do not create root logs, backup copies or a planning-history file.
Use existing tracked receipts for durable evidence and preserve raw measurements.
Use relative repository links in Markdown; retain external attribution and render
raw commit references as text.

## PCRE2 isolation

Keep all PCRE2-specific implementation under src/Lokad.Utf8Regex.Pcre2/ and use
Pcre2 in its types, namespaces, folders and test fixtures. PCRE2-only helpers belong
there; unavoidable Core hooks carry PCRE2-INTEGRATION-POINT.
Share genuine low-level utilities without a flavor framework. Core loads alone.
The adapter's sole non-BCL implementation dependency is Core; no P/Invoke,
NativeLibrary, external PCRE2 executable/binary or RID-native assets.

## CI

Push authorized scoped changes and check CI without idle waiting while other work
is available. Space GitHub status queries minutes apart; anonymous API quota is
60/hour. Do not poll in a tight loop or repeat unchanged package matrices.
