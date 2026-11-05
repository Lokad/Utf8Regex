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
and resource checks passed in **44m12s**. The [run receipt](bench/Experiments/DeliveryDev30-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 94.455 us | 18.930 us | 2,172.813 us | 4.99x |
| `literal-alternate/sherlock-zh` | 154.115 us | 69.606 us | 2,179.932 us | 2.21x |
| `literal/sherlock-ru` | 185.145 us | 99.110 us | 5,443.678 us | 1.87x |
| `ruff-noqa/tweaked` | 10,107.680 us | 7,898.029 us | 43,284.850 us | 1.28x |
| `literal/sherlock-casei-en` | 158.851 us | 192.482 us | 570.789 us | 0.83x |
| `literal/sherlock-en` | 57.328 us | 86.048 us | 481.090 us | 0.67x |
| `aws-keys/quick` | 6,452.075 us | 9,806.260 us | 44,744.600 us | 0.66x |
| `literal/sherlock-casei-ru` | 251.196 us | 394.712 us | 5,690.189 us | 0.64x |
| `bounded-repeat/letters-en` | 1,053.565 us | 2,861.006 us | 2,951.294 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,684.373 us | 4,877.973 us | 10,131.733 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 199.966 us | 599.307 us | 1,026.794 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,977.150 us | 6,064.011 us | 6,894.262 us | 0.33x |
| `dictionary/single` | 8,274.243 us | 26,305.600 us | 26,191.600 us | 0.31x |
| `literal-alternate/sherlock-en` | 383.879 us | 1,531.155 us | 2,064.511 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 151.905 us | 682.328 us | 1,125.656 us | 0.22x |
| `bounded-repeat/capitals` | 9,548.767 us | 47,124.650 us | 53,251.400 us | 0.20x |
| `bounded-repeat/context` | 16,387.200 us | 148,611.700 us | 152,055.900 us | 0.11x |
| `aws-keys/full` | 8,877.640 us | 88,739.700 us | 117,896.400 us | 0.10x |
| `ruff-noqa/real` | 4,408.867 us | 70,201.000 us | 101,214.600 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 881.619 us | 22,961.300 us | 28,503.700 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 172.919 us | 11,059.740 us | 11,703.220 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,259.398 us | 509.487 us | 8,882.300 us | 2.47x |
| `common/match-word` | `Match` | 0.199 us | 0.165 us | 1.133 us | 1.21x |
| `industry/leipzig-twain-count` | `Count` | 1,164.298 us | 1,336.965 us | 8,600.967 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,448.777 us | 1,914.444 us | 10,297.500 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.111 us | 0.149 us | 0.73x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.59x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.205 us | 0.223 us | 0.58x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 136.486 us | 265.588 us | 508.606 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.171 us | 0.187 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.175 us | 0.195 us | 0.45x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.187 us | 0.205 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 4,065.175 us | 9,985.940 us | 16,823.450 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.239 us | 0.275 us | 0.38x |
| `common/email-match` | `IsMatch` | 0.100 us | 0.269 us | 0.295 us | 0.37x |
| `common/replace-words` | `Replace` | 27.769 us | 76.089 us | 79.351 us | 0.36x |
| `common/matches-word` | `Count` | 1.372 us | 4.258 us | 5.980 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.347 us | 0.369 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,378.941 us | 8,276.229 us | 16,836.750 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.098 us | 0.20x |
| `common/split-words` | `Split` | 14.710 us | 77.826 us | 79.222 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.670 us | 975.877 us | 1,260.123 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.564 us | 0.598 us | 0.15x |
| `common/matches-words` | `Count` | 9.512 us | 71.560 us | 72.917 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.613 us | 0.652 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.078 us | 0.091 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.834 us | 0.874 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,070.624 us | 19,421.350 us | 20,230.300 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,307.050 us | 32,614.250 us | 46,599.900 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,869.733 us | 23,712.800 us | 33,860.550 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 530.457 us | 8,596.183 us | 9,107.250 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.056 us | 164.712 us | 166.674 us | 0.04x |
| `common/matches-set` | `Count` | 7.763 us | 195.579 us | 196.610 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 261.594 us | 11,718.580 us | 12,230.540 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 153.267 us | 35.998 us | 2,164.862 us | 4.26x |
| `literal/sherlock-zh` | 94.906 us | 23.947 us | 2,185.630 us | 3.96x |
| `ruff-noqa/tweaked` | 10,417.000 us | 4,563.545 us | 37,468.300 us | 2.28x |
| `literal/sherlock-ru` | 185.468 us | 90.911 us | 5,380.930 us | 2.04x |
| `literal/sherlock-casei-en` | 159.025 us | 78.256 us | 450.457 us | 2.03x |
| `aws-keys/quick` | 5,665.413 us | 3,596.579 us | 36,585.700 us | 1.58x |
| `literal-alternate/sherlock-casei-en` | 172.899 us | 146.968 us | 580.163 us | 1.18x |
| `literal-alternate/sherlock-en-mixed` | 199.992 us | 175.837 us | 536.676 us | 1.14x |
| `literal-alternate/sherlock-en` | 386.000 us | 345.382 us | 746.593 us | 1.12x |
| `literal/sherlock-casei-ru` | 247.176 us | 221.965 us | 5,352.070 us | 1.11x |
| `bounded-repeat/letters-ru` | 1,984.292 us | 2,078.560 us | 2,919.159 us | 0.95x |
| `literal-alternate/sherlock-en-nomatch` | 154.035 us | 201.001 us | 595.578 us | 0.77x |
| `literal/sherlock-en` | 57.350 us | 75.974 us | 440.117 us | 0.75x |
| `bounded-repeat/capitals` | 9,565.633 us | 13,686.275 us | 20,246.833 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,638.890 us | 2,357.986 us | 7,585.300 us | 0.70x |
| `bounded-repeat/letters-en` | 1,049.958 us | 1,849.664 us | 1,935.050 us | 0.57x |
| `aws-keys/full` | 9,693.180 us | 18,904.667 us | 49,909.650 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 879.691 us | 1,964.485 us | 7,245.033 us | 0.45x |
| `bounded-repeat/context` | 16,454.750 us | 69,757.600 us | 72,534.100 us | 0.24x |
| `ruff-noqa/real` | 4,412.158 us | 21,077.200 us | 52,930.150 us | 0.21x |
| `dictionary/single` | 8,285.171 us | 46,211.350 us | 46,134.700 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,257.442 us | 476.264 us | 7,902.833 us | 2.64x |
| `common/match-word` | `Match` | 0.199 us | 0.077 us | 0.996 us | 2.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.299 us | 59.003 us | 280.057 us | 2.29x |
| `industry/leipzig-river-window-count` | `Count` | 3,354.631 us | 1,526.682 us | 14,199.800 us | 2.20x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.044 us | 0.075 us | 2.14x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.067 us | 0.087 us | 1.74x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.049 us | 1.64x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.049 us | 0.087 us | 1.64x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.048 us | 0.069 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.031 us | 0.047 us | 1.61x |
| `common/replace-words` | `Replace` | 27.701 us | 17.548 us | 20.601 us | 1.58x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.051 us | 0.070 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,876.630 us | 1,279.100 us | 11,300.733 us | 1.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,091.992 us | 3,016.129 us | 8,230.700 us | 1.36x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.056 us | 0.078 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,462.317 us | 1,094.564 us | 8,444.667 us | 1.34x |
| `common/email-match` | `IsMatch` | 0.100 us | 0.081 us | 0.108 us | 1.23x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.089 us | 0.115 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.088 us | 0.120 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,166.933 us | 1,304.782 us | 5,368.267 us | 0.89x |
| `common/one-node-backtracking` | `IsMatch` | 0.077 us | 0.088 us | 0.127 us | 0.88x |
| `common/matches-words` | `Count` | 9.502 us | 11.025 us | 14.055 us | 0.86x |
| `common/split-words` | `Split` | 14.684 us | 21.412 us | 23.113 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,369.364 us | 3,497.553 us | 11,089.400 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-word` | `Count` | 1.373 us | 2.370 us | 3.642 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.204 us | 0.237 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.037 us | 0.053 us | 0.32x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 533.506 us | 3,973.462 us | 4,388.642 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.494 us | 1,461.257 us | 1,810.790 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,053.948 us | 18,498.400 us | 19,116.067 us | 0.11x |
| `common/matches-set` | `Count` | 7.781 us | 106.276 us | 108.082 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.004 us | 105.134 us | 106.885 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.888 us | 4,778.264 us | 5,228.190 us | 0.06x |
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
| `lokad/imports/module-imports` | 12.120 us | 6.359 us | 28.746 us | 1.91x |
| `structural/keyword-to-generic-type` | 5.485 us | 4.106 us | 15.953 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.711 us | 1.314 us | 1.439 us | 1.30x |
| `lokad/lexer/doc-line` | 3.478 us | 2.891 us | 3.524 us | 1.20x |
| `lokad/lexer/identifier` | 23.806 us | 22.109 us | 25.407 us | 1.08x |
| `lokad/langserv/helper-identifier` | 7.133 us | 7.013 us | 7.798 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.369 us | 5.315 us | 17.248 us | 1.01x |
| `lokad/langserv/identifier-validator` | 11.336 us | 12.311 us | 13.651 us | 0.92x |
| `lokad/lexer/operator-run` | 3.034 us | 3.344 us | 3.729 us | 0.91x |
| `fallback/lookahead` | 5.426 us | 6.105 us | 17.357 us | 0.89x |
| `literal-family/method-token-family` | 7.165 us | 9.269 us | 19.884 us | 0.77x |
| `lokad/style/hex-color` | 0.606 us | 0.811 us | 0.879 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 2.131 us | 2.874 us | 2.952 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 3.356 us | 4.708 us | 5.685 us | 0.71x |
| `literal/identifier-token` | 2.796 us | 4.361 us | 15.805 us | 0.64x |
| `literal/call-token` | 2.615 us | 4.348 us | 15.471 us | 0.60x |
| `lokad/lexer/number` | 8.223 us | 14.008 us | 15.015 us | 0.59x |
| `structural/ordered-identifier-window` | 16.266 us | 28.833 us | 39.835 us | 0.56x |
| `structural/method-family-generic-call` | 3.454 us | 6.614 us | 18.020 us | 0.52x |
| `lokad/style/cell-ref` | 1.493 us | 2.891 us | 2.971 us | 0.52x |
| `structural/method-family-call` | 3.888 us | 8.170 us | 18.944 us | 0.48x |
| `literal-family/type-token-family` | 10.588 us | 24.203 us | 35.193 us | 0.44x |
| `literal/identifier-token-casei` | 5.792 us | 15.698 us | 27.574 us | 0.37x |
| `lokad/style/range-ref` | 0.925 us | 2.774 us | 2.722 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.408 us | 17.228 us | 28.399 us | 0.31x |
| `structural/modifier-family-to-type-window` | 80.352 us | 257.426 us | 269.475 us | 0.31x |
| `structural/ordered-keyword-window` | 5.521 us | 17.710 us | 29.626 us | 0.31x |
| `lokad/lexer/string` | 22.528 us | 92.167 us | 98.614 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 23.197 us | 131.880 us | 144.257 us | 0.18x |
| `lokad/folding/region-marker` | 6.429 us | 36.652 us | 61.559 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.531 us | 24.380 us | 24.719 us | 0.10x |
| `lokad/langserv/url-download` | 2.574 us | 51.839 us | 52.677 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 10.533 us | 215.570 us | 228.539 us | 0.05x |
| `balancing/begin-end-blocks` | 5.694 us | 4,375.858 us | 4,386.800 us | 0.0013x |
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
| `lokad/imports/module-imports` | 12.217 us | 4.411 us | 26.241 us | 2.77x |
| `structural/modifier-family-to-type-pair` | 23.267 us | 10.684 us | 21.596 us | 2.18x |
| `lokad/langserv/color-short-hex` | 1.698 us | 0.968 us | 1.151 us | 1.75x |
| `lokad/lexer/doc-line` | 3.489 us | 2.145 us | 2.852 us | 1.63x |
| `lokad/langserv/helper-identifier` | 7.050 us | 4.529 us | 5.643 us | 1.56x |
| `structural/keyword-to-generic-type` | 5.484 us | 3.531 us | 14.880 us | 1.55x |
| `structural/ordered-identifier-window` | 16.221 us | 10.796 us | 21.572 us | 1.50x |
| `lokad/lexer/operator-run` | 2.949 us | 1.967 us | 2.468 us | 1.50x |
| `lokad/lexer/identifier` | 23.668 us | 16.230 us | 20.143 us | 1.46x |
| `structural/modifier-family-to-type-window` | 80.654 us | 56.047 us | 68.088 us | 1.44x |
| `structural/keyword-family-to-capitalized-identifier` | 10.597 us | 7.933 us | 18.523 us | 1.34x |
| `structural/ordered-keyword-pair` | 5.368 us | 4.146 us | 15.265 us | 1.29x |
| `fallback/lookahead` | 5.452 us | 4.241 us | 15.150 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.374 us | 8.968 us | 10.906 us | 1.27x |
| `lokad/docs/inline-doc-prefix` | 3.360 us | 2.727 us | 3.517 us | 1.23x |
| `lokad/style/hex-color` | 0.607 us | 0.511 us | 0.592 us | 1.19x |
| `lokad/langserv/color-rgb-hex` | 2.136 us | 1.825 us | 2.047 us | 1.17x |
| `lokad/lexer/number` | 8.221 us | 7.254 us | 8.359 us | 1.13x |
| `literal/identifier-token-casei` | 5.916 us | 5.988 us | 17.440 us | 0.99x |
| `literal-family/type-token-family` | 10.579 us | 11.557 us | 22.252 us | 0.92x |
| `lokad/style/cell-ref` | 1.495 us | 1.762 us | 1.932 us | 0.85x |
| `structural/method-family-call` | 3.880 us | 5.096 us | 15.584 us | 0.76x |
| `structural/method-family-generic-call` | 3.440 us | 4.539 us | 15.293 us | 0.76x |
| `literal-family/method-token-family` | 3.897 us | 5.385 us | 15.793 us | 0.72x |
| `literal/call-token` | 2.625 us | 3.953 us | 14.601 us | 0.66x |
| `structural/ordered-keyword-window` | 5.530 us | 8.614 us | 20.067 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.432 us | 8.689 us | 19.776 us | 0.63x |
| `lokad/style/range-ref` | 0.914 us | 1.508 us | 1.538 us | 0.61x |
| `literal/identifier-token` | 2.803 us | 4.734 us | 15.307 us | 0.59x |
| `lokad/lexer/string` | 22.424 us | 45.590 us | 47.742 us | 0.49x |
| `lokad/langserv/url-dashboard` | 2.502 us | 7.370 us | 7.815 us | 0.34x |
| `lokad/langserv/url-download` | 2.585 us | 12.826 us | 13.368 us | 0.20x |
| `lokad/folding/region-marker` | 6.447 us | 36.970 us | 59.805 us | 0.17x |
| `balancing/begin-end-blocks` | 5.675 us | 468.284 us | 493.109 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
