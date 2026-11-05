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
and resource checks passed in **43m31s**. The [run receipt](bench/Experiments/DeliveryDev37-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 87.614 us | 18.480 us | 2,148.609 us | 4.74x |
| `literal-alternate/sherlock-zh` | 117.514 us | 68.168 us | 2,155.714 us | 1.72x |
| `literal/sherlock-ru` | 168.473 us | 98.796 us | 5,322.740 us | 1.71x |
| `literal/sherlock-casei-en` | 155.227 us | 188.887 us | 576.262 us | 0.82x |
| `literal/sherlock-en` | 56.269 us | 82.523 us | 461.722 us | 0.68x |
| `literal/sherlock-casei-ru` | 243.168 us | 388.754 us | 5,512.622 us | 0.63x |
| `ruff-noqa/tweaked` | 4,359.567 us | 7,869.000 us | 40,963.500 us | 0.55x |
| `aws-keys/quick` | 4,831.500 us | 9,666.433 us | 44,877.700 us | 0.50x |
| `bounded-repeat/letters-en` | 1,022.963 us | 2,807.144 us | 2,919.476 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,664.423 us | 4,707.782 us | 9,935.433 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,939.727 us | 5,912.189 us | 6,754.375 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 188.642 us | 585.755 us | 1,022.939 us | 0.32x |
| `dictionary/single` | 8,119.214 us | 25,651.600 us | 25,661.100 us | 0.32x |
| `literal-alternate/sherlock-en` | 390.369 us | 1,498.312 us | 1,996.648 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 151.504 us | 659.747 us | 1,093.842 us | 0.23x |
| `bounded-repeat/capitals` | 9,307.233 us | 44,928.150 us | 50,859.700 us | 0.21x |
| `bounded-repeat/context` | 23,705.167 us | 144,991.500 us | 148,660.200 us | 0.16x |
| `aws-keys/full` | 8,679.740 us | 84,499.200 us | 113,543.100 us | 0.10x |
| `ruff-noqa/real` | 4,421.417 us | 69,549.100 us | 84,627.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 962.706 us | 23,797.900 us | 35,686.750 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 134.335 us | 11,702.280 us | 12,418.940 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,202.429 us | 505.974 us | 8,921.867 us | 2.38x |
| `common/match-word` | `Match` | 0.195 us | 0.161 us | 1.110 us | 1.21x |
| `industry/leipzig-twain-count` | `Count` | 1,142.882 us | 1,315.107 us | 8,089.700 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,414.922 us | 1,938.793 us | 10,296.033 us | 0.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.111 us | 0.147 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.124 us | 0.193 us | 0.215 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.083 us | 0.099 us | 0.62x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.042 us | 262.165 us | 507.833 us | 0.51x |
| `common/ip-match` | `IsMatch` | 0.082 us | 0.178 us | 0.197 us | 0.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.167 us | 0.183 us | 0.46x |
| `industry/leipzig-name-family-count` | `Count` | 4,012.862 us | 9,652.450 us | 16,839.200 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.229 us | 0.264 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.188 us | 0.206 us | 0.39x |
| `common/replace-words` | `Replace` | 27.748 us | 76.089 us | 76.498 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.263 us | 0.292 us | 0.36x |
| `common/matches-word` | `Count` | 1.331 us | 4.179 us | 5.872 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.345 us | 0.369 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,267.836 us | 8,145.400 us | 16,170.850 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.105 us | 0.133 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.606 us | 948.645 us | 1,244.844 us | 0.19x |
| `common/split-words` | `Split` | 14.514 us | 79.676 us | 80.580 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.541 us | 0.576 us | 0.18x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.606 us | 0.636 us | 0.13x |
| `common/matches-words` | `Count` | 9.144 us | 69.961 us | 72.507 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.090 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.831 us | 0.873 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,265.544 us | 31,532.400 us | 44,594.300 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,954.088 us | 19,183.050 us | 19,949.067 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,765.066 us | 22,749.233 us | 29,999.750 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.228 us | 8,252.714 us | 8,660.717 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.666 us | 161.734 us | 165.572 us | 0.05x |
| `common/matches-set` | `Count` | 7.355 us | 191.176 us | 193.187 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 248.165 us | 11,621.840 us | 12,192.240 us | 0.02x |
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
| `literal/sherlock-zh` | 87.912 us | 23.755 us | 2,167.283 us | 3.70x |
| `literal-alternate/sherlock-zh` | 116.725 us | 35.822 us | 2,165.158 us | 3.26x |
| `literal/sherlock-casei-en` | 155.581 us | 77.474 us | 482.985 us | 2.01x |
| `literal/sherlock-ru` | 169.961 us | 88.640 us | 5,326.030 us | 1.92x |
| `aws-keys/quick` | 4,858.927 us | 3,623.847 us | 35,989.700 us | 1.34x |
| `literal/sherlock-casei-ru` | 242.900 us | 196.688 us | 5,351.550 us | 1.23x |
| `literal-alternate/sherlock-en-mixed` | 190.405 us | 177.837 us | 573.122 us | 1.07x |
| `literal-alternate/sherlock-en` | 383.240 us | 366.733 us | 762.291 us | 1.05x |
| `ruff-noqa/tweaked` | 4,404.533 us | 4,521.950 us | 35,435.800 us | 0.97x |
| `bounded-repeat/letters-ru` | 1,954.900 us | 2,067.340 us | 2,873.322 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 133.402 us | 144.782 us | 607.564 us | 0.92x |
| `literal/sherlock-en` | 56.025 us | 73.442 us | 403.799 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 152.396 us | 202.369 us | 558.966 us | 0.75x |
| `bounded-repeat/capitals` | 9,412.350 us | 13,469.725 us | 20,086.833 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,610.725 us | 2,344.491 us | 7,453.267 us | 0.69x |
| `bounded-repeat/letters-en` | 1,020.720 us | 1,830.007 us | 1,924.522 us | 0.56x |
| `aws-keys/full` | 9,452.820 us | 18,479.433 us | 48,848.900 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 963.617 us | 1,970.888 us | 11,759.133 us | 0.49x |
| `bounded-repeat/context` | 23,813.333 us | 68,532.000 us | 71,567.300 us | 0.35x |
| `ruff-noqa/real` | 4,468.067 us | 20,786.400 us | 33,072.250 us | 0.21x |
| `dictionary/single` | 8,071.100 us | 44,977.050 us | 45,151.100 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,216.514 us | 474.773 us | 7,781.567 us | 2.56x |
| `common/match-word` | `Match` | 0.194 us | 0.077 us | 0.979 us | 2.53x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.682 us | 60.890 us | 269.073 us | 2.20x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.043 us | 0.074 us | 2.19x |
| `industry/leipzig-river-window-count` | `Count` | 3,281.644 us | 1,499.291 us | 15,946.233 us | 2.19x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.067 us | 0.086 us | 1.73x |
| `common/ip-match` | `IsMatch` | 0.081 us | 0.048 us | 0.067 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.030 us | 0.049 us | 1.70x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.029 us | 0.047 us | 1.67x |
| `common/replace-words` | `Replace` | 27.419 us | 16.634 us | 19.553 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.086 us | 1.64x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.051 us | 0.071 us | 1.48x |
| `industry/leipzig-symbol-count` | `Count` | 1,785.934 us | 1,213.179 us | 6,683.100 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.055 us | 0.075 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,037.177 us | 3,047.053 us | 8,250.433 us | 1.32x |
| `industry/mariomka-uri-count` | `Count` | 1,410.950 us | 1,080.455 us | 8,492.433 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.081 us | 0.108 us | 1.16x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.091 us | 0.117 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.088 us | 0.117 us | 1.03x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.087 us | 0.128 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 1,153.750 us | 1,273.659 us | 6,856.800 us | 0.91x |
| `common/matches-words` | `Count` | 9.168 us | 10.697 us | 13.834 us | 0.86x |
| `common/split-words` | `Split` | 14.368 us | 20.398 us | 22.073 us | 0.70x |
| `industry/mariomka-ip-count` | `Count` | 2,263.459 us | 3,404.107 us | 11,065.000 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.044 us | 0.075 us | 0.64x |
| `common/matches-word` | `Count` | 1.325 us | 2.311 us | 3.395 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.200 us | 0.233 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.053 us | 0.30x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 498.116 us | 3,834.754 us | 4,319.308 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.259 us | 1,453.206 us | 1,802.237 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,973.612 us | 17,379.000 us | 18,372.433 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.701 us | 104.621 us | 106.614 us | 0.07x |
| `common/matches-set` | `Count` | 7.373 us | 104.739 us | 106.575 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 243.223 us | 4,730.655 us | 5,200.830 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.814 us | 6.278 us | 28.550 us | 1.88x |
| `structural/keyword-to-generic-type` | 5.441 us | 4.046 us | 15.642 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.717 us | 1.305 us | 1.425 us | 1.32x |
| `lokad/lexer/doc-line` | 3.547 us | 2.986 us | 3.692 us | 1.19x |
| `lokad/lexer/identifier` | 24.394 us | 23.095 us | 25.182 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.491 us | 5.340 us | 16.589 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.157 us | 7.119 us | 7.807 us | 1.01x |
| `fallback/lookahead` | 5.678 us | 6.032 us | 17.107 us | 0.94x |
| `lokad/langserv/identifier-validator` | 11.174 us | 12.421 us | 14.016 us | 0.90x |
| `lokad/lexer/operator-run` | 2.906 us | 3.325 us | 3.808 us | 0.87x |
| `literal-family/method-token-family` | 7.056 us | 9.084 us | 19.568 us | 0.78x |
| `lokad/style/hex-color` | 0.603 us | 0.805 us | 0.877 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 3.404 us | 4.819 us | 5.516 us | 0.71x |
| `lokad/langserv/color-rgb-hex` | 2.028 us | 2.943 us | 3.036 us | 0.69x |
| `literal/identifier-token` | 2.734 us | 4.269 us | 15.275 us | 0.64x |
| `lokad/lexer/number` | 8.362 us | 13.818 us | 14.917 us | 0.61x |
| `literal/call-token` | 2.574 us | 4.259 us | 15.337 us | 0.60x |
| `structural/ordered-identifier-window` | 16.011 us | 28.583 us | 39.635 us | 0.56x |
| `lokad/style/cell-ref` | 1.475 us | 2.813 us | 2.905 us | 0.52x |
| `structural/method-family-generic-call` | 3.412 us | 6.670 us | 18.134 us | 0.51x |
| `structural/method-family-call` | 3.834 us | 7.995 us | 18.837 us | 0.48x |
| `literal-family/type-token-family` | 10.410 us | 23.871 us | 34.814 us | 0.44x |
| `literal/identifier-token-casei` | 5.651 us | 14.922 us | 26.767 us | 0.38x |
| `lokad/style/range-ref` | 0.913 us | 2.720 us | 2.697 us | 0.34x |
| `structural/modifier-family-to-type-window` | 77.663 us | 254.474 us | 268.682 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.273 us | 17.393 us | 28.705 us | 0.30x |
| `structural/ordered-keyword-window` | 5.261 us | 17.400 us | 28.313 us | 0.30x |
| `lokad/lexer/string` | 21.240 us | 93.235 us | 98.757 us | 0.23x |
| `lokad/folding/region-marker` | 6.385 us | 36.621 us | 61.104 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.508 us | 23.845 us | 24.271 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.312 us | 210.398 us | 223.964 us | 0.05x |
| `lokad/langserv/url-download` | 2.702 us | 53.342 us | 54.013 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.935 us | 133.467 us | 145.117 us | 0.04x |
| `balancing/begin-end-blocks` | 5.716 us | 4,288.600 us | 4,329.658 us | 0.0013x |
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
| `lokad/imports/module-imports` | 11.897 us | 4.386 us | 25.882 us | 2.71x |
| `lokad/langserv/color-short-hex` | 1.727 us | 0.944 us | 1.144 us | 1.83x |
| `structural/keyword-to-generic-type` | 5.446 us | 3.446 us | 14.532 us | 1.58x |
| `lokad/lexer/doc-line` | 3.581 us | 2.268 us | 2.946 us | 1.58x |
| `lokad/lexer/operator-run` | 2.879 us | 1.894 us | 2.487 us | 1.52x |
| `lokad/langserv/helper-identifier` | 7.146 us | 4.724 us | 5.671 us | 1.51x |
| `structural/ordered-identifier-window` | 15.928 us | 10.775 us | 21.621 us | 1.48x |
| `lokad/lexer/identifier` | 24.211 us | 16.490 us | 19.892 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.264 us | 7.782 us | 18.703 us | 1.45x |
| `structural/modifier-family-to-type-window` | 77.200 us | 54.440 us | 67.509 us | 1.42x |
| `fallback/lookahead` | 5.619 us | 4.166 us | 14.874 us | 1.35x |
| `structural/ordered-keyword-pair` | 5.525 us | 4.103 us | 14.834 us | 1.35x |
| `lokad/langserv/identifier-validator` | 11.244 us | 8.892 us | 11.301 us | 1.26x |
| `lokad/docs/inline-doc-prefix` | 3.384 us | 2.799 us | 3.478 us | 1.21x |
| `lokad/style/hex-color` | 0.599 us | 0.509 us | 0.595 us | 1.18x |
| `lokad/lexer/number` | 8.310 us | 7.354 us | 8.473 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.035 us | 1.959 us | 2.109 us | 1.04x |
| `literal/identifier-token-casei` | 5.608 us | 5.798 us | 17.481 us | 0.97x |
| `literal-family/type-token-family` | 10.435 us | 11.411 us | 21.989 us | 0.91x |
| `lokad/style/cell-ref` | 1.479 us | 1.797 us | 1.888 us | 0.82x |
| `structural/method-family-generic-call` | 3.386 us | 4.420 us | 15.386 us | 0.77x |
| `structural/method-family-call` | 3.798 us | 5.065 us | 15.606 us | 0.75x |
| `literal-family/method-token-family` | 3.798 us | 5.231 us | 15.533 us | 0.73x |
| `literal/call-token` | 2.562 us | 3.901 us | 14.489 us | 0.66x |
| `structural/ordered-keyword-window` | 5.348 us | 8.461 us | 19.191 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.292 us | 8.540 us | 19.361 us | 0.62x |
| `literal/identifier-token` | 2.732 us | 4.584 us | 15.031 us | 0.60x |
| `lokad/style/range-ref` | 0.905 us | 1.540 us | 1.532 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 4.978 us | 10.507 us | 21.675 us | 0.47x |
| `lokad/lexer/string` | 21.122 us | 44.875 us | 48.728 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.516 us | 7.293 us | 7.780 us | 0.35x |
| `lokad/langserv/url-download` | 2.685 us | 12.906 us | 13.350 us | 0.21x |
| `lokad/folding/region-marker` | 6.355 us | 36.235 us | 60.375 us | 0.18x |
| `balancing/begin-end-blocks` | 5.713 us | 465.511 us | 489.690 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
