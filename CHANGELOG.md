# Changelog

## Unreleased

This release completes and qualifies the selected managed PCRE2 profile while
preserving the public API and .NET 10 semantic contract of `Lokad.Utf8Regex`.

### Managed PCRE2 Profile

- Completed the generic managed execution of branching, repetition, captures,
  backreferences, assertions, scoped options, branch-reset groups, duplicate
  names, recursion and subroutines, conditionals, atomic and backtracking
  controls, `\K`, the selected Unicode atoms, and opted-in `\C` behavior.
- Completed `IsMatch`, `Match`, detailed matches, count, enumeration,
  `MatchMany`, partial probing, and PCRE2 substitution for the selected
  profile.
- Aligned global empty-match progress, reported-versus-consumed `\K` ranges,
  duplicate-name capture lookup, recursion, backtracking controls, and
  replacement behavior with the selected PCRE2 10.47 standard-matcher
  semantics.
- Made unsupported operations and constructs fail explicitly instead of
  falling through to a similar-looking .NET behavior.
- Kept the companion fully managed: it has no native PCRE2 binary, P/Invoke,
  RID-specific asset, external matcher, or non-BCL dependency other than
  `Lokad.Utf8Regex`.

### Core Engine

- Preserved the existing `Lokad.Utf8Regex` public API and its .NET 10 `Regex`
  semantics.
- Reduced repeated UTF-8 validation, coordinate projection, candidate scans,
  match materialization, and replacement assembly across matching, counting,
  enumeration, and replacement paths.
- Improved byte-offset execution, scalar-boundary handling, timeout polling,
  pooled backtracking state, capture rollback, and transactional destination
  writes.
- Kept PCRE2 syntax, options, captures, errors, and replacement rules outside
  the .NET-compatible core.

### Performance And Reliability

- Added reproducible core, PCRE2, and PythonRe comparative snapshots with
  source/runtime provenance, bounded warmup, per-case checkpointing, and
  adversarial 1×/2×/4×/8× scaling coverage.
- Reused prepared core literal-family search for compatible PCRE2 value
  operations and added AST-certified required literals, leading-byte sets,
  and bounded literal windows before PCRE2 verification.
- Reduced the former multi-second compatible PCRE2 `Count` cases to within
  6.1× decode-then-Regex; the retained `[^\n]*` VM case remains the sole
  material compatible P0 exception at about 15.3×.
- Decode non-ASCII case-insensitive fallback subjects once for capture-free
  core `Count` and `IsMatch` operations that cannot use native byte matching.
- Removed PythonRe's quadratic pure-zero-width suffix probes and reranked
  non-empty replacement through its translated managed regex after the new
  comparative catalog exposed both losses.
- Removed preventable temporary allocation from warmed non-result PCRE2
  operations and bounded pooled invocation state and replacement-plan caches.
- Kept candidate search, Unicode coordinate projection, zero-width progress,
  capture rollback, and replacement growth linear across the qualified
  scaling workloads.
- Improved failure-path cleanup, concurrent compiled-regex reuse, resource
  limits, and deterministic return of pooled buffers.
- Retained and documented the remaining compatible and predecoded-baseline
  gaps; this release does not claim general throughput parity with
  predecoded `.NET Regex`.

### Compatibility Boundaries

- `Probe` remains a curated partial-match surface rather than a promise that
  every normally matchable pattern supports partial matching.
- The DFA matcher, invalid-UTF subject mode, callouts, split, full capture
  history, and substitution forms outside the selected profile remain out of
  scope.
- Opted-in `\C` can report byte ranges that split a UTF-8 scalar, and
  lookaround `\K` can report non-monotone ranges; operations that cannot make
  safe forward progress reject those results explicitly.

## 0.2.0 - 2026-04-29

This release aligned the core and optional regex-flavor packages at version
0.2.0 and substantially broadened UTF-8-native execution coverage.

### Packages And APIs

- Added the optional `Lokad.Utf8Regex.PythonRe` package with a parser-first,
  managed CPython-`re`-style surface over UTF-8 inputs.
- Expanded the optional `Lokad.Utf8Regex.Pcre2` package and its managed PCRE2
  compatibility surface.
- Added host-facing PythonRe search, detailed-match, find-all, iteration, and
  substitution APIs while keeping Python-specific result types separate from
  the core and PCRE2 surfaces.
- Clarified the relationship between UTF-16-compatible coordinates and
  byte-aligned match coordinates in the core API.

### Execution And Performance

- Broadened native UTF-8 eligibility and fallback coverage for the core and
  PythonRe profiles.
- Added and refined structural, literal-family, trailing-literal, and
  candidate-search optimizations for common matching and counting workloads.

## 0.1.0 - 2026-04-13

Initial preview release of `Lokad.Utf8Regex` for .NET 10, with a UTF-8
`ReadOnlySpan<byte>` / `Span<byte>` surface and
`System.Text.RegularExpressions.Regex` as its semantic reference.
