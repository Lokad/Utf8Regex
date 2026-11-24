# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes the upcoming 0.5.0 API.

## Quick start

```csharp
using Lokad.Utf8Regex;
using System.Text.RegularExpressions;

var regex = new Utf8Regex(@"[A-Z]{2}\d{4}", RegexOptions.CultureInvariant);
ReadOnlySpan<byte> input = "Order AB1234 is ready."u8;

bool found = regex.IsMatch(input);              // true
int count = regex.Count(input);                 // 1
Utf8CaptureRange first = regex.Match(input, 0, []); // bytes [6, 12)
byte[] redacted = regex.Replace(input, "[order]"u8);
```

Use `EnumerateMatches` to visit whole matches and `CreateMatchCursor` over
`ReadOnlyMemory<byte>` for groups and capture history. Use match ranges directly
with UTF-8 slices. `EnumerateMatches(input, startOffsetInBytes, cancellationToken)` searches
from a scalar-aligned byte position while retaining full context for assertions.
The byte-start `Match` can fill caller-owned `Utf8CaptureRange` slots; slot zero
is the whole match and `GroupNumberFromCaptureSlot` resolves sparse group numbers.
Copy a slot's name into caller storage with `TryCopyGroupNameUtf8`; metadata
collections and text decoding belong to the host.

## Choose a profile

| Package | Matching profile |
| --- | --- |
| [Lokad.Utf8Regex](docs/SPEC.md) | .NET-style grammar, captures/history, backreferences, lookarounds, RTL and a NonBacktracking subset |
| [Lokad.Utf8Regex.PythonRe](src/Lokad.Utf8Regex.PythonRe/README.md) | Python re-style grammar, capture numbering and result shaping |
| [Lokad.Utf8Regex.Pcre2](src/Lokad.Utf8Regex.Pcre2/README.md) | Managed PCRE2-style grammar, capture slots, substitution and partial probing |
| [Lokad.Utf8Regex.Posix](src/Lokad.Utf8Regex.Posix/README.md) | POSIX ERE subset with leftmost-longest selection and hierarchical capture priority; new in 0.5.0 |

Adapters require the matching Core package version. Their specifications
define the supported grammar and operation boundaries.

## Unicode and input

Matching uses **Unicode scalars**. For example,
`new Utf8Regex(".").Count("A😀"u8)` returns 2; the emoji occupies four bytes.
Classes, quantifiers, captures and empty-match advancement respect scalar boundaries.

All four profiles consume already-valid UTF-8 text. Match and capture results
expose byte coordinates only; callers derive character positions or strings.
Keep borrowed input unchanged and its owner alive while a view or cursor uses it.
Owned snapshots remain valid after cursor advance or disposal.

## Subject input contract

Validate external bytes where they become text, using
`System.Text.Unicode.Utf8.IsValid` when validity is not already guaranteed by the
host's text representation. Regex operations do not perform a separate subject
validity pass and expose no validation policy or token.

This applies to matching, counting, replacements, splits and cursors in Core,
PythonRe, PCRE2 and POSIX. Malformed subjects have unspecified matching results;
managed bounds checking, cancellation and enforceable resource controls remain.
Pattern, replacement-template and assembled-output checks remain independent.
Live route eligibility, scalar decoding and output sizing may still inspect text.
See the [input contract](docs/SPEC.md#subject-input-contract).

## Cancellation and limits

Instances accept cancellation tokens, timeouts and optional work, depth, working-
capacity and result-capacity limits:

```csharp
var bounded = new Utf8Regex(
    @"(?<word>\w+)", RegexOptions.CultureInvariant,
    policy: new Utf8RegexPolicy
    {
        MatchTimeout = TimeSpan.FromSeconds(1),
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 256,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
        }
    });

using var cancellation = new CancellationTokenSource();
int count = bounded.Count("café tea"u8, cancellation.Token); // 2
```

Core operations have optional cancellation parameters. IsMatch/Count also accept
`workingMemoryAllowance:` for shared temporary-storage admission. Omitting it or
passing null keeps numeric limits and inherits program funding when selected;
a refused non-null account fails
without retry. For span enumeration, name the token `cancellationToken:`.

These caps are examples. Defaults have unlimited invocation
budgets. Byte limits cover modeled engine/result capacities, not total process
memory. Constructors and `Compile` accept immutable `Utf8RegexPolicy` construction
controls and optional program funding. Dispose funded expressions. Span enumerators
borrow an open regex; persistent cursors can retain admission after it closes.
See the [resource contract](docs/SPEC.md#cancellation-deadlines-and-resource-limits)
for failure, deadline and ownership details.

See the [release notes](CHANGELOG.md) for published versions and 0.5.0 upgrades.

## Runtime support

The 0.5.0 development branch targets `net10.0` and `net11.0`. .NET 11
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The measured operations start with valid, unchanged UTF-8 and include the required
result materialization. The Core summary records the measured sources, runtimes,
qualification and snapshot identity. The benchmark guide covers ordinary patterns
and calls with finite limits, deadlines and cancellation.
[Core summary](bench/Snapshots/Core.json) · [Benchmark guide](docs/BENCHMARKS.md)

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.
The `common/match-word` and `lokad/docs/inline-doc-prefix` rows compare the
first whole-match range on both sides. The other Lokad prefix-loop rows from
the dev.117 refresh include .NET's `Match` object allocation.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

Focused Core counts use the caller-valid text contract, for example
`--measure-readme-public-case industry/mariomka-email-count 1 9`.
These measurements label that contract and leave recorded tables unchanged.

Profile comparisons are in the
[PythonRe](src/Lokad.Utf8Regex.PythonRe/README.md#benchmarks),
[PCRE2](src/Lokad.Utf8Regex.Pcre2/README.md#benchmarks) and
[POSIX](src/Lokad.Utf8Regex.Posix/README.md#benchmarks) README sections.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 22.478 us | 18.891 us | 2,215.355 us | 1.19x |
| `literal/sherlock-ru` | 68.068 us | 98.325 us | 5,311.520 us | 0.69x |
| `literal-alternate/sherlock-zh` | 43.656 us | 68.354 us | 2,177.740 us | 0.64x |
| `ruff-noqa/tweaked` | 3,991.045 us | 7,819.073 us | 42,171.350 us | 0.51x |
| `literal/sherlock-casei-ru` | 156.483 us | 368.283 us | 5,593.514 us | 0.42x |
| `literal/sherlock-casei-en` | 80.048 us | 201.053 us | 591.109 us | 0.40x |
| `bounded-repeat/letters-en` | 1,098.039 us | 2,803.139 us | 2,950.373 us | 0.39x |
| `literal/sherlock-en` | 31.718 us | 89.717 us | 481.766 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,987.053 us | 5,862.338 us | 6,726.858 us | 0.34x |
| `literal-alternate/sherlock-ru` | 1,435.385 us | 4,794.137 us | 9,955.825 us | 0.30x |
| `dictionary/single` | 7,682.380 us | 25,847.100 us | 25,756.933 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 161.198 us | 600.179 us | 1,055.849 us | 0.27x |
| `bounded-repeat/capitals` | 11,998.514 us | 45,578.000 us | 49,275.550 us | 0.26x |
| `literal-alternate/sherlock-en` | 356.175 us | 1,546.949 us | 2,058.303 us | 0.23x |
| `aws-keys/quick` | 1,982.105 us | 9,514.511 us | 46,251.250 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 128.743 us | 662.256 us | 1,092.714 us | 0.19x |
| `aws-keys/full` | 8,248.178 us | 86,948.400 us | 120,781.100 us | 0.09x |
| `bounded-repeat/context` | 8,989.067 us | 148,213.500 us | 152,277.800 us | 0.06x |
| `ruff-noqa/real` | 3,554.064 us | 70,614.900 us | 86,905.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 856.758 us | 23,376.375 us | 28,631.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 106.110 us | 11,382.757 us | 12,309.371 us | 0.0093x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.116 us | 0.109 us | 0.138 us | 1.06x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.086 us | 0.083 us | 0.098 us | 1.04x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.080 us | 0.077 us | 0.091 us | 1.04x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.079 us | 0.085 us | 0.102 us | 0.93x |
| `common/ip-match` | `IsMatch` | 0.164 us | 0.178 us | 0.198 us | 0.92x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.090 us | 0.111 us | 0.147 us | 0.81x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.130 us | 0.168 us | 0.184 us | 0.77x |
| `industry/mariomka-email-count` | `Count` | 382.226 us | 496.605 us | 9,069.450 us | 0.77x |
| `common/uri-miss` | `IsMatch` | 0.137 us | 0.188 us | 0.207 us | 0.73x |
| `common/uri-match` | `IsMatch` | 0.161 us | 0.231 us | 0.269 us | 0.70x |
| `industry/mariomka-uri-count` | `Count` | 1,242.855 us | 1,956.962 us | 10,680.033 us | 0.64x |
| `common/email-match` | `IsMatch` | 0.156 us | 0.274 us | 0.303 us | 0.57x |
| `common/ip-miss` | `IsMatch` | 0.382 us | 0.721 us | 0.745 us | 0.53x |
| `common/match-word` | `Match` | 0.068 us | 0.130 us | 1.171 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 634.917 us | 1,331.833 us | 8,529.200 us | 0.48x |
| `industry/mariomka-ip-count` | `Count` | 3,292.187 us | 8,394.510 us | 17,081.633 us | 0.39x |
| `common/date-miss` | `IsMatch` | 0.219 us | 0.565 us | 0.600 us | 0.39x |
| `common/matches-set` | `Count` | 63.392 us | 196.174 us | 197.375 us | 0.32x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 78.872 us | 260.463 us | 494.097 us | 0.30x |
| `common/matches-word` | `Count` | 1.252 us | 4.280 us | 5.978 us | 0.29x |
| `common/replace-words` | `Replace` | 21.688 us | 82.032 us | 82.588 us | 0.26x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.803 us | 975.186 us | 1,257.552 us | 0.17x |
| `common/matches-boundary` | `Count` | 29.122 us | 168.604 us | 170.586 us | 0.17x |
| `common/split-words` | `Split` | 13.245 us | 80.707 us | 82.664 us | 0.16x |
| `common/matches-words` | `Count` | 9.241 us | 70.960 us | 72.210 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,060.372 us | 9,970.950 us | 17,435.667 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,890.403 us | 19,738.450 us | 20,376.325 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 906.506 us | 11,949.157 us | 12,443.617 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.617 us | 0.653 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,452.569 us | 31,551.933 us | 39,353.450 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.354 us | 0.382 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.038 us | 0.847 us | 0.885 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,009.824 us | 22,677.050 us | 30,051.150 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 273.127 us | 8,639.444 us | 9,079.600 us | 0.03x |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal-alternate/sherlock-zh` | 43.348 us | 36.529 us | 2,152.481 us | 1.19x |
| `literal/sherlock-casei-en` | 80.346 us | 78.173 us | 450.887 us | 1.03x |
| `bounded-repeat/letters-ru` | 1,991.087 us | 2,055.751 us | 2,924.542 us | 0.97x |
| `literal-alternate/sherlock-en` | 355.740 us | 370.313 us | 767.011 us | 0.96x |
| `literal/sherlock-zh` | 22.494 us | 24.156 us | 2,224.883 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 160.934 us | 176.567 us | 538.636 us | 0.91x |
| `bounded-repeat/capitals` | 11,973.614 us | 13,592.283 us | 16,699.600 us | 0.88x |
| `ruff-noqa/tweaked` | 3,966.555 us | 4,573.071 us | 36,655.250 us | 0.87x |
| `literal/sherlock-ru` | 68.388 us | 89.638 us | 5,211.760 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 107.520 us | 145.296 us | 585.882 us | 0.74x |
| `literal/sherlock-casei-ru` | 157.000 us | 224.104 us | 5,355.940 us | 0.70x |
| `literal-alternate/sherlock-en-nomatch` | 128.504 us | 201.364 us | 589.175 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,435.894 us | 2,344.184 us | 7,495.475 us | 0.61x |
| `bounded-repeat/letters-en` | 1,102.619 us | 1,838.385 us | 1,977.339 us | 0.60x |
| `aws-keys/quick` | 1,975.947 us | 3,622.548 us | 39,339.250 us | 0.55x |
| `aws-keys/full` | 8,284.440 us | 18,552.320 us | 53,474.667 us | 0.45x |
| `literal-alternate/sherlock-casei-ru` | 852.276 us | 1,987.395 us | 7,284.575 us | 0.43x |
| `literal/sherlock-en` | 31.922 us | 78.377 us | 403.569 us | 0.41x |
| `ruff-noqa/real` | 3,584.723 us | 21,116.275 us | 49,152.433 us | 0.17x |
| `dictionary/single` | 7,664.870 us | 45,892.300 us | 45,834.300 us | 0.17x |
| `bounded-repeat/context` | 8,927.044 us | 69,357.150 us | 72,959.550 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.164 us | 0.049 us | 0.068 us | 3.37x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.083 us | 0.031 us | 0.047 us | 2.64x |
| `common/date-match` | `IsMatch` | 0.115 us | 0.044 us | 0.073 us | 2.59x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.077 us | 0.031 us | 0.049 us | 2.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.130 us | 0.055 us | 0.076 us | 2.34x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.080 us | 0.035 us | 0.054 us | 2.28x |
| `common/ip-miss` | `IsMatch` | 0.382 us | 0.181 us | 0.204 us | 2.11x |
| `common/uri-miss` | `IsMatch` | 0.137 us | 0.065 us | 0.089 us | 2.10x |
| `common/email-match` | `IsMatch` | 0.155 us | 0.081 us | 0.111 us | 1.90x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.088 us | 0.049 us | 0.087 us | 1.82x |
| `common/uri-match` | `IsMatch` | 0.162 us | 0.092 us | 0.124 us | 1.76x |
| `common/match-word` | `Match` | 0.068 us | 0.048 us | 0.943 us | 1.40x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 78.568 us | 58.551 us | 269.813 us | 1.34x |
| `common/replace-words` | `Replace` | 21.669 us | 17.614 us | 18.883 us | 1.23x |
| `industry/mariomka-uri-count` | `Count` | 1,245.900 us | 1,125.045 us | 8,517.467 us | 1.11x |
| `common/date-miss` | `IsMatch` | 0.217 us | 0.202 us | 0.238 us | 1.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,465.273 us | 1,514.284 us | 8,034.625 us | 0.97x |
| `industry/mariomka-ip-count` | `Count` | 3,296.983 us | 3,491.486 us | 10,901.967 us | 0.94x |
| `common/matches-words` | `Count` | 9.214 us | 10.896 us | 14.247 us | 0.85x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.044 us | 0.075 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 381.568 us | 462.193 us | 7,748.325 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 1,009.499 us | 1,236.825 us | 7,194.750 us | 0.82x |
| `common/split-words` | `Split` | 13.261 us | 20.786 us | 22.706 us | 0.64x |
| `common/matches-set` | `Count` | 63.296 us | 106.253 us | 107.793 us | 0.60x |
| `common/matches-word` | `Count` | 1.244 us | 2.375 us | 3.676 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 634.046 us | 1,303.609 us | 7,494.256 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,074.323 us | 3,112.052 us | 8,830.575 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.476 us | 604.648 us | 860.395 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.784 us | 105.080 us | 106.788 us | 0.27x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.110 us | 0.129 us | 0.26x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,894.822 us | 8,869.889 us | 9,414.962 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 905.854 us | 4,816.969 us | 5,199.353 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.090 us | 0.116 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 273.190 us | 3,949.232 us | 4,348.694 us | 0.07x |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 7.708 us | 6.464 us | 31.314 us | 1.19x |
| `fallback/lookahead` | 6.850 us | 6.211 us | 20.084 us | 1.10x |
| `structural/method-family-generic-call` | 6.494 us | 6.591 us | 20.422 us | 0.99x |
| `lokad/langserv/color-short-hex` | 0.730 us | 0.824 us | 1.014 us | 0.89x |
| `lokad/langserv/identifier-validator` | 6.689 us | 7.568 us | 9.062 us | 0.88x |
| `lokad/lexer/doc-line` | 1.648 us | 1.880 us | 2.627 us | 0.88x |
| `structural/method-family-call` | 7.978 us | 9.240 us | 21.361 us | 0.86x |
| `lokad/lexer/identifier` | 11.993 us | 14.018 us | 17.059 us | 0.86x |
| `lokad/style/hex-color` | 0.435 us | 0.520 us | 0.605 us | 0.84x |
| `structural/ordered-keyword-window` | 13.510 us | 17.888 us | 31.410 us | 0.76x |
| `lokad/lexer/operator-run` | 1.734 us | 2.341 us | 2.799 us | 0.74x |
| `lokad/langserv/color-rgb-hex` | 1.375 us | 1.866 us | 2.089 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 2.560 us | 3.499 us | 4.343 us | 0.73x |
| `structural/keyword-to-generic-type` | 2.953 us | 4.065 us | 17.990 us | 0.73x |
| `structural/ordered-modifier-pair` | 10.365 us | 17.136 us | 30.208 us | 0.60x |
| `lokad/lexer/number` | 6.355 us | 10.817 us | 11.740 us | 0.59x |
| `structural/ordered-keyword-pair` | 3.195 us | 5.498 us | 19.129 us | 0.58x |
| `lokad/langserv/helper-identifier` | 2.581 us | 4.807 us | 5.765 us | 0.54x |
| `literal/identifier-token` | 2.344 us | 4.382 us | 17.253 us | 0.53x |
| `lokad/style/cell-ref` | 0.882 us | 1.913 us | 2.131 us | 0.46x |
| `lokad/style/range-ref` | 0.815 us | 1.824 us | 1.962 us | 0.45x |
| `literal/call-token` | 1.841 us | 4.340 us | 17.361 us | 0.42x |
| `lokad/langserv/url-dashboard` | 9.884 us | 23.405 us | 23.940 us | 0.42x |
| `literal-family/type-token-family` | 9.283 us | 24.422 us | 38.362 us | 0.38x |
| `lokad/lexer/string` | 26.922 us | 84.261 us | 88.008 us | 0.32x |
| `structural/ordered-identifier-window` | 8.600 us | 28.543 us | 42.848 us | 0.30x |
| `literal-family/method-token-family` | 2.693 us | 9.384 us | 21.866 us | 0.29x |
| `structural/modifier-family-to-type-window` | 68.916 us | 259.975 us | 275.345 us | 0.27x |
| `lokad/langserv/url-download` | 11.538 us | 50.620 us | 51.263 us | 0.23x |
| `literal/identifier-token-casei` | 3.513 us | 15.479 us | 28.538 us | 0.23x |
| `lokad/folding/region-marker` | 5.179 us | 35.465 us | 62.890 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 16.105 us | 223.554 us | 237.991 us | 0.07x |
| `structural/modifier-family-to-type-pair` | 2.955 us | 130.177 us | 144.117 us | 0.02x |
| `balancing/begin-end-blocks` | 47.017 us | 4,330.989 us | 4,350.472 us | 0.01x |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `structural/keyword-family-to-capitalized-identifier` | 16.144 us | 7.712 us | 21.439 us | 2.09x |
| `lokad/style/hex-color` | 0.435 us | 0.230 us | 0.311 us | 1.89x |
| `lokad/lexer/operator-run` | 1.750 us | 0.947 us | 1.406 us | 1.85x |
| `lokad/docs/inline-doc-prefix` | 2.562 us | 1.459 us | 2.219 us | 1.76x |
| `lokad/imports/module-imports` | 7.667 us | 4.427 us | 28.465 us | 1.73x |
| `lokad/lexer/number` | 6.391 us | 3.791 us | 4.853 us | 1.69x |
| `lokad/langserv/color-rgb-hex` | 1.387 us | 0.858 us | 1.117 us | 1.62x |
| `fallback/lookahead` | 6.847 us | 4.261 us | 17.717 us | 1.61x |
| `structural/ordered-keyword-window` | 13.515 us | 8.620 us | 21.884 us | 1.57x |
| `lokad/lexer/doc-line` | 1.646 us | 1.050 us | 1.889 us | 1.57x |
| `lokad/langserv/identifier-validator` | 6.686 us | 4.278 us | 5.815 us | 1.56x |
| `structural/method-family-call` | 7.945 us | 5.089 us | 18.019 us | 1.56x |
| `lokad/langserv/url-dashboard` | 9.870 us | 6.346 us | 6.850 us | 1.56x |
| `lokad/lexer/identifier` | 12.000 us | 7.834 us | 11.161 us | 1.53x |
| `lokad/langserv/color-short-hex` | 0.727 us | 0.487 us | 0.684 us | 1.49x |
| `structural/method-family-generic-call` | 6.489 us | 4.606 us | 17.405 us | 1.41x |
| `structural/modifier-family-to-type-window` | 68.582 us | 57.143 us | 71.175 us | 1.20x |
| `structural/ordered-modifier-pair` | 10.389 us | 8.713 us | 21.644 us | 1.19x |
| `lokad/style/range-ref` | 0.830 us | 0.734 us | 0.873 us | 1.13x |
| `lokad/style/cell-ref` | 0.877 us | 0.835 us | 1.046 us | 1.05x |
| `lokad/langserv/url-download` | 11.647 us | 12.254 us | 12.747 us | 0.95x |
| `lokad/langserv/helper-identifier` | 2.570 us | 2.732 us | 3.601 us | 0.94x |
| `lokad/lexer/string` | 27.010 us | 30.233 us | 33.308 us | 0.89x |
| `structural/keyword-to-generic-type` | 2.964 us | 3.497 us | 16.782 us | 0.85x |
| `structural/ordered-identifier-window` | 8.573 us | 10.621 us | 24.534 us | 0.81x |
| `literal-family/type-token-family` | 9.230 us | 11.480 us | 24.611 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.199 us | 4.152 us | 17.149 us | 0.77x |
| `literal/identifier-token-casei` | 3.503 us | 5.946 us | 19.252 us | 0.59x |
| `literal-family/method-token-family` | 2.691 us | 5.097 us | 17.729 us | 0.53x |
| `literal/identifier-token` | 2.341 us | 4.691 us | 16.714 us | 0.50x |
| `literal/call-token` | 1.854 us | 3.968 us | 16.407 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.972 us | 10.832 us | 23.725 us | 0.27x |
| `lokad/folding/region-marker` | 5.153 us | 36.198 us | 62.242 us | 0.14x |
| `balancing/begin-end-blocks` | 46.984 us | 466.311 us | 499.434 us | 0.10x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->

<details>
<summary>Regenerating benchmark tables</summary>

The compact summaries in `bench/Snapshots/` regenerate the README tables offline
through the existing `--rewrite-*-benchmark-markdown` commands. Ordinary tests need
no raw measurements or downloads. Regeneration preserves recorded observations;
it does not qualify the current engine or create a new benchmark run.

Refreshes and full evidence checks use local raw snapshots under
`artifacts/benchmark-refresh/current/`. `--export-benchmark-summaries` updates the
compact summaries from those files; `--rewrite-readme-parity-report` generates the
expanded Core report there on demand. Use `tools/bench.ps1`, build once and reuse
matching binaries with `-SkipBuild`. Keep or share raw outputs when useful; there
is no required archive service or upload step.

</details>
