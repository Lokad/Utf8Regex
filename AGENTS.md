# Lokad.Utf8Regex

Follow the profile contracts and the ignored root PLAN.md.
[Core](docs/SPEC.md) · [PythonRe](docs/SPEC-PYTHONRE.md) · [PCRE2](docs/SPEC-PCRE2.md) · [POSIX](docs/SPEC-POSIX.md)

## Contracts

- Match Unicode scalars over caller-valid, unchanged UTF-8. Expose byte
  coordinates. Validate subjects at the text boundary and keep the matching
  API free of validation policies or tokens. Pattern, template and
  assembled-output checks are independent.
- Preserve captures, RTL, NonBacktracking, retained results and cursor aliases.
  Keep synchronous original-token cancellation, deadlines and resource bounds.
- Ship owned, checked managed matching. BCL Regex and native regex engines may
  serve as comparators. Actual `Unsafe.SizeOf<T>()` queries are permitted.
- Keep PCRE2-specific code and fixtures in the Pcre2 projects. Mark unavoidable
  Core hooks `PCRE2-INTEGRATION-POINT`. Share low-level utilities. Core loads
  independently and is the adapter's sole non-BCL implementation dependency.
  Keep shipped packages free of native regex binaries and interop.

## Build, test and benchmark

- Use the SDK pinned by global.json and `--tl:off`. Put the ignored `.dotnet/`
  on PATH when using that SDK, including for child processes.
- Use `tools/test.ps1 -Configuration Release -Project <project>` with a focused
  filter. The default covers .NET 10 and 11. Reuse matching binaries with
  `-SkipBuild`. Zero discovered tests is failure.
- Reuse build servers. Recover test hosts only after a file-lock failure and
  only when the executable is inside this checkout.
- Use `tools/bench.ps1` in Release, normally `-Job Short`, on .NET 11. Run
  focused comparisons sequentially and keep the public outputs equal.
  The quiet VM target for a complete refresh is one hour. Preserve raw
  evidence and regenerate tables with the existing tools.
  [Benchmark procedure](docs/BENCHMARKS.md)

## Delivery

- Reserve complete build, platform and package matrices for delivery boundaries.
  Use `tools/qualify-package-candidate.ps1` with clean source, a fresh common
  version and exact Core pins. Keep published and staged bytes immutable.
- Packages go to `artifacts/nuget/`. Keep package, platform, performance and
  original consumer checks distinct. NuGet uses the self-contained package
  README. [Package qualification](tools/qualify-package-candidate.ps1) ·
  [Package README](docs/nuget/README.md)
- Push authorized scoped commits. Space GitHub CI status queries minutes apart
  and continue useful work while CI runs.

## Files

Keep guides in `docs/` and README.md, CHANGELOG.md, AGENTS.md and ignored PLAN.md
at root. Put disposable work in `tmp/<task>/` and delete it after use. Preserve
measurement evidence in `artifacts/benchmark-refresh/`. Use relative repository
links, keep external attribution, and render raw commit IDs as text.
