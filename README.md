# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and both optional adapters are fully managed .NET libraries.

## Quick start

```csharp
using Lokad.Utf8Regex;
using System.Text.RegularExpressions;

var regex = new Utf8Regex(@"[A-Z]{2}\d{4}", RegexOptions.CultureInvariant);
ReadOnlySpan<byte> input = "Order AB1234 is ready."u8;

bool found = regex.IsMatch(input);              // true
int count = regex.Count(input);                 // 1
Utf8ValueMatch first = regex.Match(input);       // byte index 6, length 6
byte[] redacted = regex.Replace(input, "[order]");
```

Use `EnumerateMatches` to visit matches and `MatchDetailed` for groups and
capture history. Match byte offsets and lengths can be used directly with UTF-8
slices.

## Choose a profile

| Package | Matching profile |
| --- | --- |
| [Lokad.Utf8Regex](docs/SPEC.md) | .NET-style grammar, captures/history, backreferences, lookarounds, RTL and a NonBacktracking subset |
| [Lokad.Utf8Regex.PythonRe](src/Lokad.Utf8Regex.PythonRe/README.md) | Python re-style grammar, capture numbering and result shaping |
| [Lokad.Utf8Regex.Pcre2](src/Lokad.Utf8Regex.Pcre2/README.md) | Managed PCRE2-style grammar, capture slots, substitution and partial probing |

Both adapters require the matching Core package version. Their specifications
define the supported grammar and operation boundaries.

## Unicode and input

Matching uses **Unicode scalars**. For example,
`new Utf8Regex(".").Count("A😀"u8)` returns 2; the emoji occupies four bytes.
Classes, quantifiers, captures and empty-match advancement respect scalar boundaries.

The entire subject must be valid UTF-8, including text after an early match.
Byte coordinates are primary; UTF-16 coordinates and strings are derived
conveniences. Keep borrowed input unchanged and its owner alive while a view or
cursor uses it. Owned snapshots remain valid after cursor advance or disposal.

## Cancellation and limits

Instances accept cancellation tokens, timeouts and optional work, depth, working-
capacity and result-capacity limits:

```csharp
var bounded = new Utf8Regex(
    @"(?<word>\w+)", RegexOptions.CultureInvariant,
    matchTimeout: TimeSpan.FromSeconds(1),
    resourcePolicy: new Utf8ResourcePolicy
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 256,
        MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
    });

using var cancellation = new CancellationTokenSource();
int count = bounded.Count("café tea"u8, cancellation.Token); // 2
```

These caps are examples. Defaults and static helpers have unlimited invocation
budgets. Byte limits cover modeled engine/result capacities, not total process
memory. `Compile` offers separate construction controls.
See the [resource contract](docs/SPEC.md#cancellation-deadlines-and-resource-limits)
for failure, deadline and ownership details.

See [release notes](CHANGELOG.md) and the [migration guide](docs/MIGRATION.md)
for the 0.4.0 changes.

## Runtime support

The 0.5.0 development source targets `net10.0` and `net11.0`. .NET 11 currently
uses the release-candidate SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The tables were refreshed on **4 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **45m49s**. The [run receipt](bench/Experiments/DeliveryDev44-20261106/full-refresh.json)
records the scope and timings; package and consumer qualification are separate.
Each row retains its recorded implementation. Selective updates do not imply a
new complete run; the full-run receipt applies to its recorded revision.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

[refresh-benchmarks.ps1](refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](bench.ps1) supports focused single-case iterations.

Profile comparisons are in the
[PythonRe](src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md) and
[PCRE2](src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md) benchmark pages.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 85.732 us | 18.936 us | 2,156.706 us | 4.53x |
| `literal/sherlock-ru` | 168.482 us | 97.610 us | 5,340.136 us | 1.73x |
| `literal-alternate/sherlock-zh` | 105.228 us | 69.150 us | 2,179.091 us | 1.52x |
| `literal/sherlock-casei-en` | 160.822 us | 192.970 us | 604.425 us | 0.83x |
| `literal/sherlock-en` | 57.437 us | 90.124 us | 467.427 us | 0.64x |
| `literal/sherlock-casei-ru` | 246.408 us | 395.798 us | 5,593.593 us | 0.62x |
| `ruff-noqa/tweaked` | 4,443.478 us | 7,668.300 us | 42,892.950 us | 0.58x |
| `aws-keys/quick` | 5,055.060 us | 9,637.112 us | 43,611.050 us | 0.52x |
| `bounded-repeat/letters-en` | 1,043.336 us | 2,833.437 us | 2,950.819 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,675.373 us | 4,882.519 us | 10,065.450 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 199.746 us | 603.510 us | 1,021.240 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,978.241 us | 6,013.400 us | 6,811.864 us | 0.33x |
| `dictionary/single` | 8,297.000 us | 26,220.100 us | 26,199.100 us | 0.32x |
| `literal-alternate/sherlock-en` | 401.339 us | 1,549.471 us | 2,074.405 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 155.126 us | 654.020 us | 1,068.067 us | 0.24x |
| `bounded-repeat/capitals` | 9,494.312 us | 46,518.000 us | 50,776.700 us | 0.20x |
| `bounded-repeat/context` | 24,284.600 us | 148,807.700 us | 152,702.600 us | 0.16x |
| `aws-keys/full` | 10,625.038 us | 86,824.200 us | 119,187.400 us | 0.12x |
| `ruff-noqa/real` | 4,524.282 us | 70,782.150 us | 86,956.300 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 968.415 us | 23,281.725 us | 28,520.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 134.020 us | 11,777.186 us | 12,529.850 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,195.378 us | 495.581 us | 9,024.900 us | 2.41x |
| `common/match-word` | `Match` | 0.200 us | 0.169 us | 1.171 us | 1.18x |
| `industry/leipzig-twain-count` | `Count` | 1,159.111 us | 1,323.356 us | 8,481.360 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,417.211 us | 1,967.285 us | 10,532.350 us | 0.72x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.112 us | 0.151 us | 0.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.211 us | 0.229 us | 0.57x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.434 us | 258.616 us | 492.214 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.168 us | 0.186 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.081 us | 0.181 us | 0.202 us | 0.45x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.187 us | 0.207 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 4,068.611 us | 9,912.825 us | 16,935.933 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.229 us | 0.262 us | 0.41x |
| `common/replace-words` | `Replace` | 28.311 us | 79.117 us | 80.747 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.271 us | 0.300 us | 0.35x |
| `common/matches-word` | `Count` | 1.358 us | 4.250 us | 5.976 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.353 us | 0.380 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,321.194 us | 8,482.700 us | 17,054.333 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.21x |
| `common/split-words` | `Split` | 15.175 us | 80.804 us | 82.776 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.512 us | 978.505 us | 1,247.983 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.558 us | 0.588 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.610 us | 0.641 us | 0.13x |
| `common/matches-words` | `Count` | 9.380 us | 72.895 us | 74.548 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.078 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.096 us | 0.839 us | 0.884 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,929.444 us | 19,421.250 us | 20,037.800 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,318.517 us | 34,533.000 us | 41,791.550 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,886.970 us | 22,968.825 us | 33,982.550 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 508.094 us | 8,566.967 us | 9,024.756 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.494 us | 164.927 us | 166.628 us | 0.05x |
| `common/matches-set` | `Count` | 7.469 us | 197.774 us | 200.666 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 235.303 us | 12,282.200 us | 12,733.183 us | 0.02x |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 85.921 us | 24.088 us | 2,165.647 us | 3.57x |
| `literal-alternate/sherlock-zh` | 104.677 us | 35.995 us | 2,143.983 us | 2.91x |
| `literal/sherlock-casei-en` | 160.863 us | 80.325 us | 443.067 us | 2.00x |
| `literal/sherlock-ru` | 169.150 us | 88.425 us | 5,305.300 us | 1.91x |
| `aws-keys/quick` | 5,054.280 us | 3,697.033 us | 35,336.550 us | 1.37x |
| `literal-alternate/sherlock-en` | 416.912 us | 348.425 us | 766.318 us | 1.20x |
| `literal/sherlock-casei-ru` | 248.916 us | 220.898 us | 5,375.287 us | 1.13x |
| `literal-alternate/sherlock-en-mixed` | 198.041 us | 177.794 us | 536.790 us | 1.11x |
| `ruff-noqa/tweaked` | 4,484.644 us | 4,564.600 us | 38,810.850 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,974.189 us | 2,067.911 us | 2,883.096 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 133.179 us | 149.433 us | 562.100 us | 0.89x |
| `literal-alternate/sherlock-en-nomatch` | 155.309 us | 204.128 us | 582.325 us | 0.76x |
| `literal/sherlock-en` | 57.164 us | 78.624 us | 411.548 us | 0.73x |
| `literal-alternate/sherlock-ru` | 1,639.096 us | 2,364.331 us | 7,500.150 us | 0.69x |
| `bounded-repeat/capitals` | 9,421.750 us | 13,695.850 us | 16,618.850 us | 0.69x |
| `bounded-repeat/letters-en` | 1,041.083 us | 1,871.169 us | 1,930.872 us | 0.56x |
| `aws-keys/full` | 9,303.737 us | 18,565.540 us | 52,553.033 us | 0.50x |
| `literal-alternate/sherlock-casei-ru` | 969.222 us | 1,980.953 us | 7,228.250 us | 0.49x |
| `bounded-repeat/context` | 24,348.825 us | 70,010.850 us | 73,321.200 us | 0.35x |
| `ruff-noqa/real` | 4,652.476 us | 21,208.700 us | 35,314.200 us | 0.22x |
| `dictionary/single` | 8,291.189 us | 46,050.650 us | 46,214.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,189.263 us | 457.651 us | 7,779.750 us | 2.60x |
| `common/match-word` | `Match` | 0.200 us | 0.080 us | 1.023 us | 2.48x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 131.461 us | 58.790 us | 254.093 us | 2.24x |
| `common/backtracking` | `IsMatch` | 0.096 us | 0.044 us | 0.075 us | 2.19x |
| `industry/leipzig-river-window-count` | `Count` | 3,323.157 us | 1,554.580 us | 8,110.580 us | 2.14x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.065 us | 0.085 us | 1.83x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.051 us | 1.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.088 us | 1.64x |
| `common/replace-words` | `Replace` | 28.198 us | 17.321 us | 20.251 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.031 us | 0.047 us | 1.61x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.048 us | 0.069 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.050 us | 0.071 us | 1.55x |
| `industry/leipzig-symbol-count` | `Count` | 1,890.922 us | 1,284.500 us | 7,434.575 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.079 us | 1.35x |
| `industry/leipzig-name-family-count` | `Count` | 4,087.626 us | 3,040.276 us | 8,381.950 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,423.225 us | 1,097.003 us | 8,435.200 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.109 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.117 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.089 us | 0.120 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,177.100 us | 1,291.955 us | 6,437.340 us | 0.91x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.088 us | 0.128 us | 0.91x |
| `common/matches-words` | `Count` | 9.357 us | 11.041 us | 14.063 us | 0.85x |
| `common/split-words` | `Split` | 15.184 us | 21.174 us | 23.101 us | 0.72x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.074 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,330.836 us | 3,535.618 us | 10,971.133 us | 0.66x |
| `common/matches-word` | `Count` | 1.355 us | 2.352 us | 3.532 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.204 us | 0.236 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.054 us | 0.31x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,927.810 us | 8,889.244 us | 9,430.725 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 508.676 us | 3,966.458 us | 4,321.017 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.595 us | 1,481.912 us | 1,797.400 us | 0.12x |
| `common/matches-set` | `Count` | 7.474 us | 105.531 us | 107.655 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.471 us | 106.194 us | 107.623 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 245.082 us | 4,820.675 us | 5,230.593 us | 0.05x |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 10.627 us | 6.399 us | 29.234 us | 1.66x |
| `lokad/langserv/color-short-hex` | 1.967 us | 1.367 us | 1.471 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.621 us | 4.117 us | 15.956 us | 1.37x |
| `lokad/lexer/doc-line` | 3.379 us | 3.039 us | 3.657 us | 1.11x |
| `lokad/lexer/identifier` | 24.173 us | 23.113 us | 26.702 us | 1.05x |
| `lokad/langserv/helper-identifier` | 7.171 us | 7.209 us | 7.779 us | 0.99x |
| `structural/ordered-keyword-pair` | 5.385 us | 5.490 us | 16.934 us | 0.98x |
| `fallback/lookahead` | 5.715 us | 6.145 us | 17.177 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.319 us | 12.590 us | 14.558 us | 0.90x |
| `lokad/lexer/operator-run` | 2.922 us | 3.532 us | 3.907 us | 0.83x |
| `literal-family/method-token-family` | 7.244 us | 9.294 us | 19.823 us | 0.78x |
| `lokad/style/hex-color` | 0.604 us | 0.832 us | 0.898 us | 0.73x |
| `lokad/docs/inline-doc-prefix` | 3.391 us | 4.942 us | 5.504 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.028 us | 2.985 us | 3.076 us | 0.68x |
| `literal/identifier-token` | 2.782 us | 4.384 us | 15.620 us | 0.63x |
| `lokad/lexer/number` | 8.918 us | 14.631 us | 15.667 us | 0.61x |
| `literal/call-token` | 2.595 us | 4.320 us | 16.136 us | 0.60x |
| `structural/ordered-identifier-window` | 16.256 us | 28.819 us | 39.822 us | 0.56x |
| `structural/method-family-generic-call` | 3.481 us | 6.844 us | 18.445 us | 0.51x |
| `lokad/style/cell-ref` | 1.504 us | 2.999 us | 3.040 us | 0.50x |
| `structural/method-family-call` | 3.877 us | 8.200 us | 18.864 us | 0.47x |
| `literal-family/type-token-family` | 10.536 us | 23.747 us | 34.953 us | 0.44x |
| `literal/identifier-token-casei` | 5.829 us | 15.382 us | 26.508 us | 0.38x |
| `lokad/style/range-ref` | 0.923 us | 2.780 us | 2.830 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.374 us | 17.387 us | 28.793 us | 0.31x |
| `structural/ordered-keyword-window` | 5.466 us | 17.697 us | 29.096 us | 0.31x |
| `structural/modifier-family-to-type-window` | 78.798 us | 256.967 us | 268.644 us | 0.31x |
| `lokad/lexer/string` | 21.833 us | 97.362 us | 102.836 us | 0.22x |
| `lokad/folding/region-marker` | 6.392 us | 36.471 us | 61.379 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.597 us | 24.649 us | 24.961 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.499 us | 219.533 us | 232.551 us | 0.05x |
| `lokad/langserv/url-download` | 2.513 us | 52.563 us | 53.520 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.007 us | 134.244 us | 144.711 us | 0.04x |
| `balancing/begin-end-blocks` | 5.748 us | 4,382.500 us | 4,395.917 us | 0.0013x |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 10.714 us | 4.473 us | 26.290 us | 2.40x |
| `lokad/langserv/color-short-hex` | 1.969 us | 1.021 us | 1.211 us | 1.93x |
| `structural/keyword-to-generic-type` | 5.609 us | 3.533 us | 14.858 us | 1.59x |
| `lokad/langserv/helper-identifier` | 7.178 us | 4.607 us | 5.674 us | 1.56x |
| `lokad/lexer/doc-line` | 3.385 us | 2.186 us | 2.986 us | 1.55x |
| `structural/ordered-identifier-window` | 16.270 us | 10.826 us | 21.741 us | 1.50x |
| `structural/keyword-family-to-capitalized-identifier` | 11.391 us | 7.940 us | 19.076 us | 1.43x |
| `lokad/lexer/operator-run` | 2.922 us | 2.053 us | 2.619 us | 1.42x |
| `lokad/lexer/identifier` | 24.107 us | 17.091 us | 21.449 us | 1.41x |
| `structural/modifier-family-to-type-window` | 78.943 us | 56.241 us | 68.128 us | 1.40x |
| `fallback/lookahead` | 5.707 us | 4.260 us | 14.997 us | 1.34x |
| `structural/ordered-keyword-pair` | 5.374 us | 4.165 us | 15.028 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.402 us | 9.360 us | 11.716 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.384 us | 2.869 us | 3.740 us | 1.18x |
| `lokad/lexer/number` | 8.917 us | 7.614 us | 8.726 us | 1.17x |
| `lokad/style/hex-color` | 0.606 us | 0.533 us | 0.647 us | 1.14x |
| `lokad/langserv/color-rgb-hex` | 2.018 us | 1.956 us | 2.153 us | 1.03x |
| `literal/identifier-token-casei` | 5.841 us | 5.904 us | 17.220 us | 0.99x |
| `literal-family/type-token-family` | 10.553 us | 11.550 us | 22.190 us | 0.91x |
| `lokad/style/cell-ref` | 1.505 us | 1.773 us | 1.957 us | 0.85x |
| `structural/method-family-generic-call` | 3.471 us | 4.519 us | 15.617 us | 0.77x |
| `structural/method-family-call` | 3.870 us | 5.097 us | 15.490 us | 0.76x |
| `literal-family/method-token-family` | 3.910 us | 5.349 us | 15.741 us | 0.73x |
| `literal/call-token` | 2.603 us | 4.006 us | 15.185 us | 0.65x |
| `structural/ordered-keyword-window` | 5.518 us | 8.672 us | 19.745 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.381 us | 8.737 us | 19.774 us | 0.62x |
| `lokad/style/range-ref` | 0.923 us | 1.548 us | 1.631 us | 0.60x |
| `literal/identifier-token` | 2.797 us | 4.726 us | 15.374 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 5.011 us | 10.672 us | 22.072 us | 0.47x |
| `lokad/lexer/string` | 21.868 us | 46.925 us | 50.253 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.566 us | 7.521 us | 8.076 us | 0.34x |
| `lokad/langserv/url-download` | 2.513 us | 13.025 us | 13.597 us | 0.19x |
| `lokad/folding/region-marker` | 6.413 us | 37.084 us | 60.001 us | 0.17x |
| `balancing/begin-end-blocks` | 5.722 us | 469.530 us | 499.157 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
