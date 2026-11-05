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
and resource checks passed in **43m31s**. The [run receipt](bench/Experiments/DeliveryDev34-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 88.045 us | 19.037 us | 2,149.265 us | 4.63x |
| `literal-alternate/sherlock-zh` | 145.188 us | 69.779 us | 2,195.639 us | 2.08x |
| `literal/sherlock-ru` | 175.787 us | 99.937 us | 5,369.150 us | 1.76x |
| `literal/sherlock-casei-en` | 159.346 us | 200.061 us | 600.694 us | 0.80x |
| `literal/sherlock-en` | 56.981 us | 87.456 us | 458.498 us | 0.65x |
| `literal/sherlock-casei-ru` | 252.721 us | 390.196 us | 5,739.733 us | 0.65x |
| `ruff-noqa/tweaked` | 4,379.075 us | 8,233.886 us | 42,282.900 us | 0.53x |
| `aws-keys/quick` | 4,900.000 us | 9,815.483 us | 45,259.300 us | 0.50x |
| `bounded-repeat/letters-en` | 1,032.980 us | 2,844.872 us | 2,950.253 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,687.467 us | 4,829.445 us | 10,126.900 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 197.150 us | 604.916 us | 1,050.415 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,959.008 us | 6,021.678 us | 6,823.550 us | 0.33x |
| `dictionary/single` | 8,242.186 us | 25,832.350 us | 25,837.200 us | 0.32x |
| `literal-alternate/sherlock-en` | 395.961 us | 1,577.116 us | 2,122.950 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 155.566 us | 651.274 us | 1,096.498 us | 0.24x |
| `bounded-repeat/capitals` | 9,519.767 us | 47,279.500 us | 55,112.500 us | 0.20x |
| `aws-keys/full` | 10,721.917 us | 86,587.300 us | 120,780.200 us | 0.12x |
| `bounded-repeat/context` | 14,282.000 us | 150,289.900 us | 153,728.600 us | 0.10x |
| `ruff-noqa/real` | 4,431.917 us | 71,589.900 us | 87,688.900 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 873.426 us | 23,409.300 us | 28,601.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 168.120 us | 11,359.080 us | 12,130.560 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,194.852 us | 492.547 us | 9,007.500 us | 2.43x |
| `common/match-word` | `Match` | 0.209 us | 0.167 us | 1.239 us | 1.26x |
| `industry/leipzig-twain-count` | `Count` | 1,164.160 us | 1,365.195 us | 9,054.933 us | 0.85x |
| `industry/mariomka-uri-count` | `Count` | 1,459.837 us | 1,957.865 us | 10,648.800 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.113 us | 0.150 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.191 us | 0.211 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.543 us | 260.689 us | 499.821 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.164 us | 0.180 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 4,062.183 us | 9,899.583 us | 17,226.650 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.188 us | 0.208 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.234 us | 0.268 us | 0.39x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.203 us | 0.222 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.273 us | 0.298 us | 0.35x |
| `common/replace-words` | `Replace` | 28.298 us | 81.648 us | 83.221 us | 0.35x |
| `common/matches-word` | `Count` | 1.355 us | 4.274 us | 6.042 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.348 us | 0.373 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,358.609 us | 8,427.500 us | 17,087.000 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.115 us | 0.145 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 14.751 us | 78.499 us | 79.973 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.401 us | 964.962 us | 1,279.047 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.562 us | 0.598 us | 0.16x |
| `common/backtracking` | `IsMatch` | 0.123 us | 0.846 us | 0.886 us | 0.15x |
| `common/matches-words` | `Count` | 9.405 us | 70.939 us | 72.694 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.615 us | 0.650 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.078 us | 0.091 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,982.108 us | 19,648.550 us | 20,575.900 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,355.040 us | 33,531.950 us | 47,652.000 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,817.082 us | 23,437.467 us | 30,530.350 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 494.883 us | 8,529.250 us | 8,977.067 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.127 us | 165.082 us | 167.697 us | 0.04x |
| `common/matches-set` | `Count` | 7.874 us | 196.937 us | 198.693 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 246.463 us | 11,969.440 us | 12,468.050 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 145.168 us | 36.690 us | 2,167.646 us | 3.96x |
| `literal/sherlock-zh` | 88.095 us | 24.164 us | 2,178.737 us | 3.65x |
| `literal/sherlock-casei-en` | 159.892 us | 80.240 us | 428.365 us | 1.99x |
| `literal/sherlock-ru` | 175.730 us | 92.221 us | 5,273.090 us | 1.91x |
| `aws-keys/quick` | 4,860.327 us | 3,597.657 us | 38,074.650 us | 1.35x |
| `literal/sherlock-casei-ru` | 252.281 us | 197.596 us | 5,466.680 us | 1.28x |
| `literal-alternate/sherlock-casei-en` | 168.813 us | 133.512 us | 554.694 us | 1.26x |
| `literal-alternate/sherlock-en-mixed` | 196.852 us | 176.814 us | 566.882 us | 1.11x |
| `literal-alternate/sherlock-en` | 411.862 us | 374.306 us | 771.489 us | 1.10x |
| `ruff-noqa/tweaked` | 4,371.692 us | 4,593.355 us | 38,166.550 us | 0.95x |
| `bounded-repeat/letters-ru` | 1,962.350 us | 2,077.564 us | 2,869.278 us | 0.94x |
| `literal-alternate/sherlock-en-nomatch` | 155.493 us | 203.837 us | 611.184 us | 0.76x |
| `literal/sherlock-en` | 56.912 us | 76.882 us | 431.226 us | 0.74x |
| `bounded-repeat/capitals` | 9,517.400 us | 13,708.850 us | 21,254.733 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,643.410 us | 2,368.356 us | 7,545.900 us | 0.69x |
| `aws-keys/full` | 10,741.900 us | 19,039.133 us | 53,656.950 us | 0.56x |
| `bounded-repeat/letters-en` | 1,033.231 us | 1,857.371 us | 1,947.112 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 873.750 us | 2,001.977 us | 7,139.233 us | 0.44x |
| `bounded-repeat/context` | 14,300.675 us | 69,784.400 us | 72,955.800 us | 0.20x |
| `ruff-noqa/real` | 4,460.592 us | 21,873.150 us | 35,546.650 us | 0.20x |
| `dictionary/single` | 8,240.929 us | 46,181.450 us | 46,194.450 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/backtracking` | `IsMatch` | 0.120 us | 0.044 us | 0.074 us | 2.72x |
| `industry/mariomka-email-count` | `Count` | 1,195.388 us | 455.732 us | 7,793.167 us | 2.62x |
| `common/match-word` | `Match` | 0.209 us | 0.080 us | 1.006 us | 2.61x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.070 us | 0.030 us | 0.047 us | 2.36x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.954 us | 61.589 us | 271.589 us | 2.21x |
| `industry/leipzig-river-window-count` | `Count` | 3,344.056 us | 1,566.391 us | 14,292.367 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.068 us | 0.085 us | 1.74x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.030 us | 0.049 us | 1.67x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.049 us | 0.088 us | 1.65x |
| `common/replace-words` | `Replace` | 28.267 us | 17.690 us | 20.881 us | 1.60x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.050 us | 0.070 us | 1.55x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.048 us | 0.069 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,818.214 us | 1,262.985 us | 7,172.367 us | 1.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.078 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,183.308 us | 3,058.541 us | 8,409.667 us | 1.37x |
| `industry/mariomka-uri-count` | `Count` | 1,464.126 us | 1,103.133 us | 8,559.867 us | 1.33x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.109 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.089 us | 0.117 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.120 us | 1.02x |
| `industry/leipzig-twain-count` | `Count` | 1,166.702 us | 1,294.479 us | 6,064.067 us | 0.90x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.089 us | 0.129 us | 0.88x |
| `common/matches-words` | `Count` | 9.381 us | 11.041 us | 14.149 us | 0.85x |
| `common/split-words` | `Split` | 14.713 us | 21.417 us | 23.312 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,357.659 us | 3,584.193 us | 11,225.350 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `common/matches-word` | `Count` | 1.354 us | 2.378 us | 3.846 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.203 us | 0.236 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.037 us | 0.052 us | 0.28x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 492.642 us | 4,002.338 us | 4,432.142 us | 0.12x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.291 us | 1,474.806 us | 1,812.000 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,982.762 us | 17,799.950 us | 18,646.267 us | 0.11x |
| `common/matches-set` | `Count` | 7.883 us | 107.421 us | 108.923 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.100 us | 106.083 us | 107.959 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 238.106 us | 4,843.509 us | 5,283.500 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.796 us | 6.486 us | 29.367 us | 1.82x |
| `structural/keyword-to-generic-type` | 5.609 us | 4.095 us | 16.010 us | 1.37x |
| `lokad/langserv/color-short-hex` | 1.692 us | 1.375 us | 1.580 us | 1.23x |
| `lokad/lexer/doc-line` | 3.367 us | 3.093 us | 3.954 us | 1.09x |
| `lokad/lexer/identifier` | 23.860 us | 23.544 us | 26.677 us | 1.01x |
| `structural/ordered-keyword-pair` | 5.397 us | 5.476 us | 17.671 us | 0.99x |
| `lokad/lexer/operator-run` | 3.190 us | 3.441 us | 4.079 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.379 us | 12.773 us | 14.413 us | 0.89x |
| `fallback/lookahead` | 5.415 us | 6.095 us | 17.395 us | 0.89x |
| `lokad/langserv/helper-identifier` | 7.265 us | 8.797 us | 9.955 us | 0.83x |
| `literal-family/method-token-family` | 7.193 us | 9.233 us | 19.992 us | 0.78x |
| `lokad/style/hex-color` | 0.610 us | 0.829 us | 0.902 us | 0.74x |
| `lokad/langserv/color-rgb-hex` | 2.054 us | 2.983 us | 3.188 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.414 us | 5.088 us | 5.835 us | 0.67x |
| `literal/identifier-token` | 2.799 us | 4.393 us | 15.599 us | 0.64x |
| `literal/call-token` | 2.615 us | 4.336 us | 15.547 us | 0.60x |
| `lokad/lexer/number` | 8.301 us | 14.371 us | 15.482 us | 0.58x |
| `structural/ordered-identifier-window` | 16.116 us | 28.726 us | 40.002 us | 0.56x |
| `lokad/style/cell-ref` | 1.508 us | 2.967 us | 3.163 us | 0.51x |
| `structural/method-family-generic-call` | 3.478 us | 6.855 us | 18.303 us | 0.51x |
| `structural/method-family-call` | 3.880 us | 8.149 us | 19.062 us | 0.48x |
| `literal-family/type-token-family` | 10.636 us | 24.444 us | 35.766 us | 0.44x |
| `literal/identifier-token-casei` | 6.123 us | 15.883 us | 27.192 us | 0.39x |
| `lokad/style/range-ref` | 0.923 us | 2.773 us | 2.894 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.598 us | 17.746 us | 28.682 us | 0.32x |
| `structural/modifier-family-to-type-window` | 79.100 us | 253.951 us | 266.688 us | 0.31x |
| `structural/ordered-keyword-window` | 5.490 us | 18.027 us | 28.927 us | 0.30x |
| `lokad/lexer/string` | 21.778 us | 96.495 us | 101.608 us | 0.23x |
| `lokad/folding/region-marker` | 6.460 us | 37.973 us | 61.219 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.558 us | 24.675 us | 25.197 us | 0.10x |
| `lokad/langserv/url-download` | 2.571 us | 52.485 us | 52.860 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 10.702 us | 219.203 us | 232.391 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.023 us | 132.477 us | 143.551 us | 0.04x |
| `balancing/begin-end-blocks` | 5.761 us | 4,415.867 us | 4,433.858 us | 0.0013x |
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
| `lokad/imports/module-imports` | 11.822 us | 4.449 us | 26.242 us | 2.66x |
| `structural/keyword-to-generic-type` | 5.610 us | 3.536 us | 14.923 us | 1.59x |
| `lokad/langserv/color-short-hex` | 1.695 us | 1.093 us | 1.315 us | 1.55x |
| `structural/ordered-identifier-window` | 16.196 us | 10.730 us | 21.655 us | 1.51x |
| `lokad/lexer/operator-run` | 3.188 us | 2.125 us | 2.758 us | 1.50x |
| `lokad/langserv/helper-identifier` | 7.266 us | 4.876 us | 6.156 us | 1.49x |
| `lokad/lexer/doc-line` | 3.398 us | 2.421 us | 3.198 us | 1.40x |
| `structural/modifier-family-to-type-window` | 78.987 us | 56.428 us | 68.563 us | 1.40x |
| `structural/keyword-family-to-capitalized-identifier` | 10.720 us | 7.890 us | 18.547 us | 1.36x |
| `lokad/lexer/identifier` | 23.819 us | 17.879 us | 21.820 us | 1.33x |
| `structural/ordered-keyword-pair` | 5.408 us | 4.177 us | 15.582 us | 1.29x |
| `fallback/lookahead` | 5.424 us | 4.262 us | 15.183 us | 1.27x |
| `lokad/langserv/identifier-validator` | 11.379 us | 9.734 us | 11.573 us | 1.17x |
| `lokad/docs/inline-doc-prefix` | 3.416 us | 3.015 us | 3.756 us | 1.13x |
| `lokad/style/hex-color` | 0.610 us | 0.553 us | 0.641 us | 1.10x |
| `lokad/lexer/number` | 8.279 us | 7.711 us | 9.032 us | 1.07x |
| `literal/identifier-token-casei` | 6.135 us | 5.984 us | 17.812 us | 1.03x |
| `lokad/langserv/color-rgb-hex` | 2.063 us | 2.032 us | 2.268 us | 1.02x |
| `literal-family/type-token-family` | 10.612 us | 11.577 us | 22.510 us | 0.92x |
| `lokad/style/cell-ref` | 1.508 us | 1.906 us | 2.071 us | 0.79x |
| `structural/method-family-generic-call` | 3.487 us | 4.535 us | 15.372 us | 0.77x |
| `structural/method-family-call` | 3.890 us | 5.098 us | 15.731 us | 0.76x |
| `literal-family/method-token-family` | 3.928 us | 5.369 us | 15.736 us | 0.73x |
| `literal/call-token` | 2.622 us | 3.989 us | 14.662 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.608 us | 8.769 us | 19.583 us | 0.64x |
| `structural/ordered-keyword-window` | 5.518 us | 8.662 us | 19.471 us | 0.64x |
| `literal/identifier-token` | 2.802 us | 4.734 us | 15.348 us | 0.59x |
| `lokad/style/range-ref` | 0.923 us | 1.624 us | 1.691 us | 0.57x |
| `structural/modifier-family-to-type-pair` | 5.015 us | 10.880 us | 21.803 us | 0.46x |
| `lokad/lexer/string` | 21.738 us | 47.584 us | 51.104 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.549 us | 7.554 us | 8.133 us | 0.34x |
| `lokad/langserv/url-download` | 2.616 us | 13.114 us | 13.588 us | 0.20x |
| `lokad/folding/region-marker` | 6.458 us | 37.437 us | 60.072 us | 0.17x |
| `balancing/begin-end-blocks` | 5.740 us | 471.346 us | 499.154 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
