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
and resource checks passed in **43m41s**. The [run receipt](bench/Experiments/DeliveryDev23-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 94.670 us | 19.031 us | 2,172.261 us | 4.97x |
| `literal-alternate/sherlock-zh` | 206.022 us | 69.668 us | 2,195.246 us | 2.96x |
| `literal/sherlock-ru` | 204.682 us | 103.386 us | 5,284.590 us | 1.98x |
| `ruff-noqa/tweaked` | 10,855.660 us | 8,347.743 us | 43,657.050 us | 1.30x |
| `literal/sherlock-casei-en` | 170.179 us | 203.993 us | 635.114 us | 0.83x |
| `literal/sherlock-en` | 69.963 us | 87.288 us | 467.238 us | 0.80x |
| `literal/sherlock-casei-ru` | 248.007 us | 394.900 us | 5,795.667 us | 0.63x |
| `aws-keys/quick` | 4,992.300 us | 10,013.733 us | 45,406.550 us | 0.50x |
| `bounded-repeat/letters-en` | 1,050.852 us | 2,871.733 us | 2,964.494 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,729.747 us | 4,828.245 us | 10,098.633 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,961.785 us | 6,008.967 us | 6,788.512 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 193.775 us | 600.668 us | 1,026.024 us | 0.32x |
| `dictionary/single` | 8,269.729 us | 26,834.900 us | 26,868.400 us | 0.31x |
| `literal-alternate/sherlock-en` | 403.088 us | 1,519.830 us | 2,062.700 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 168.425 us | 660.104 us | 1,098.843 us | 0.26x |
| `bounded-repeat/capitals` | 9,509.433 us | 46,346.700 us | 53,482.900 us | 0.21x |
| `aws-keys/full` | 10,903.080 us | 89,088.400 us | 121,875.700 us | 0.12x |
| `bounded-repeat/context` | 17,467.367 us | 148,171.500 us | 151,517.100 us | 0.12x |
| `ruff-noqa/real` | 4,557.764 us | 70,587.000 us | 87,118.800 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,099.974 us | 23,287.967 us | 28,608.950 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 229.068 us | 11,224.980 us | 12,091.780 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,549.639 us | 513.260 us | 9,050.633 us | 3.02x |
| `common/match-word` | `Match` | 0.203 us | 0.170 us | 1.216 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,271.162 us | 1,352.695 us | 8,841.233 us | 0.94x |
| `industry/mariomka-uri-count` | `Count` | 1,762.955 us | 1,912.125 us | 10,535.600 us | 0.92x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 194.896 us | 264.443 us | 520.044 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.112 us | 0.148 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.085 us | 0.102 us | 0.64x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.191 us | 0.209 us | 0.61x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.081 us | 0.168 us | 0.187 us | 0.48x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.176 us | 0.194 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,165.142 us | 9,970.717 us | 17,232.850 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.187 us | 0.207 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.230 us | 0.269 us | 0.40x |
| `common/replace-words` | `Replace` | 28.548 us | 78.872 us | 80.108 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.270 us | 0.296 us | 0.36x |
| `industry/mariomka-ip-count` | `Count` | 2,693.037 us | 8,447.650 us | 16,866.400 us | 0.32x |
| `common/matches-word` | `Count` | 1.358 us | 4.324 us | 6.015 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.348 us | 0.374 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `common/split-words` | `Split` | 17.799 us | 78.996 us | 80.563 us | 0.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.098 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.084 us | 972.192 us | 1,265.507 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.568 us | 0.600 us | 0.15x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.620 us | 0.658 us | 0.13x |
| `common/matches-words` | `Count` | 9.381 us | 73.518 us | 74.802 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.078 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.835 us | 0.873 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,504.860 us | 33,394.000 us | 46,414.000 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,948.250 us | 19,614.200 us | 20,359.467 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,872.919 us | 23,031.633 us | 30,336.150 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 513.770 us | 8,523.750 us | 8,963.567 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.023 us | 169.899 us | 172.121 us | 0.04x |
| `common/matches-set` | `Count` | 7.013 us | 193.369 us | 195.909 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 275.637 us | 11,787.340 us | 12,395.000 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 214.217 us | 36.438 us | 2,176.278 us | 5.88x |
| `literal/sherlock-zh` | 94.824 us | 24.146 us | 2,154.626 us | 3.93x |
| `ruff-noqa/tweaked` | 10,879.620 us | 4,616.064 us | 38,832.500 us | 2.36x |
| `literal/sherlock-ru` | 204.586 us | 91.858 us | 5,285.910 us | 2.23x |
| `literal/sherlock-casei-en` | 169.560 us | 79.050 us | 457.432 us | 2.14x |
| `literal-alternate/sherlock-casei-en` | 218.187 us | 154.661 us | 622.831 us | 1.41x |
| `aws-keys/quick` | 4,997.840 us | 3,591.407 us | 36,256.300 us | 1.39x |
| `literal/sherlock-casei-ru` | 248.174 us | 223.041 us | 5,632.233 us | 1.11x |
| `literal-alternate/sherlock-en` | 402.520 us | 377.613 us | 763.296 us | 1.07x |
| `literal-alternate/sherlock-en-mixed` | 193.257 us | 188.072 us | 545.343 us | 1.03x |
| `bounded-repeat/letters-ru` | 1,965.431 us | 2,083.476 us | 2,873.811 us | 0.94x |
| `literal/sherlock-en` | 70.373 us | 76.405 us | 430.543 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 168.275 us | 203.891 us | 597.349 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,750.083 us | 2,365.100 us | 7,516.967 us | 0.74x |
| `bounded-repeat/capitals` | 9,513.967 us | 13,756.350 us | 20,699.733 us | 0.69x |
| `aws-keys/full` | 10,874.320 us | 18,910.633 us | 52,989.700 us | 0.58x |
| `bounded-repeat/letters-en` | 1,046.439 us | 1,863.507 us | 1,938.335 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,095.146 us | 1,984.392 us | 7,196.867 us | 0.55x |
| `bounded-repeat/context` | 17,351.900 us | 69,615.100 us | 72,557.200 us | 0.25x |
| `ruff-noqa/real` | 4,575.127 us | 21,386.100 us | 35,126.000 us | 0.21x |
| `dictionary/single` | 8,248.029 us | 45,980.400 us | 46,084.900 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,553.521 us | 478.963 us | 7,861.467 us | 3.24x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 194.206 us | 60.988 us | 260.776 us | 3.18x |
| `common/match-word` | `Match` | 0.202 us | 0.079 us | 1.026 us | 2.57x |
| `industry/leipzig-river-window-count` | `Count` | 3,527.120 us | 1,570.338 us | 15,683.300 us | 2.25x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.045 us | 0.075 us | 2.11x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.031 us | 0.050 us | 1.78x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.067 us | 0.085 us | 1.74x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.030 us | 0.047 us | 1.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.088 us | 1.62x |
| `industry/mariomka-uri-count` | `Count` | 1,758.603 us | 1,098.466 us | 8,563.600 us | 1.60x |
| `common/replace-words` | `Replace` | 28.526 us | 17.936 us | 20.988 us | 1.59x |
| `industry/leipzig-symbol-count` | `Count` | 1,878.248 us | 1,280.325 us | 6,952.900 us | 1.47x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.052 us | 0.071 us | 1.45x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.054 us | 0.070 us | 1.40x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.056 us | 0.078 us | 1.39x |
| `industry/leipzig-name-family-count` | `Count` | 4,181.800 us | 3,083.835 us | 8,365.033 us | 1.36x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.081 us | 0.108 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.090 us | 0.116 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.120 us | 1.02x |
| `industry/leipzig-twain-count` | `Count` | 1,273.835 us | 1,294.293 us | 5,646.267 us | 0.98x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.088 us | 0.128 us | 0.92x |
| `common/split-words` | `Split` | 17.761 us | 21.003 us | 22.937 us | 0.85x |
| `common/matches-words` | `Count` | 9.367 us | 11.169 us | 14.181 us | 0.84x |
| `industry/mariomka-ip-count` | `Count` | 2,709.068 us | 3,524.020 us | 11,042.600 us | 0.77x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `common/matches-word` | `Count` | 1.356 us | 2.377 us | 3.602 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.204 us | 0.235 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.037 us | 0.055 us | 0.28x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 512.164 us | 4,002.562 us | 4,409.050 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.561 us | 1,473.488 us | 1,828.372 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,950.267 us | 18,049.450 us | 19,064.033 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.034 us | 105.579 us | 107.103 us | 0.07x |
| `common/matches-set` | `Count` | 6.991 us | 105.654 us | 107.265 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 273.679 us | 4,822.155 us | 5,232.260 us | 0.06x |
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
| `lokad/imports/module-imports` | 15.695 us | 6.437 us | 29.503 us | 2.44x |
| `structural/keyword-to-generic-type` | 5.356 us | 4.136 us | 15.805 us | 1.29x |
| `lokad/langserv/color-short-hex` | 1.660 us | 1.351 us | 1.478 us | 1.23x |
| `lokad/lexer/doc-line` | 3.357 us | 3.038 us | 3.699 us | 1.10x |
| `lokad/lexer/identifier` | 23.917 us | 23.389 us | 26.680 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.246 us | 7.150 us | 7.983 us | 1.01x |
| `structural/ordered-keyword-pair` | 5.374 us | 5.349 us | 17.110 us | 1.00x |
| `lokad/langserv/identifier-validator` | 11.531 us | 12.591 us | 14.287 us | 0.92x |
| `fallback/lookahead` | 5.563 us | 6.107 us | 17.810 us | 0.91x |
| `lokad/lexer/operator-run` | 2.909 us | 3.357 us | 3.846 us | 0.87x |
| `literal-family/method-token-family` | 7.199 us | 9.281 us | 20.536 us | 0.78x |
| `lokad/style/hex-color` | 0.614 us | 0.837 us | 0.901 us | 0.73x |
| `lokad/docs/inline-doc-prefix` | 3.374 us | 4.800 us | 5.646 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.047 us | 3.008 us | 3.113 us | 0.68x |
| `literal/identifier-token` | 2.801 us | 4.407 us | 15.974 us | 0.64x |
| `literal/call-token` | 2.626 us | 4.358 us | 15.606 us | 0.60x |
| `lokad/lexer/number` | 8.323 us | 14.164 us | 15.153 us | 0.59x |
| `lokad/style/cell-ref` | 1.481 us | 2.883 us | 3.027 us | 0.51x |
| `structural/method-family-generic-call` | 3.457 us | 6.784 us | 18.205 us | 0.51x |
| `structural/method-family-call` | 3.884 us | 8.127 us | 19.181 us | 0.48x |
| `literal-family/type-token-family` | 10.583 us | 24.172 us | 35.418 us | 0.44x |
| `literal/identifier-token-casei` | 5.870 us | 15.811 us | 26.714 us | 0.37x |
| `lokad/style/range-ref` | 0.922 us | 2.745 us | 2.811 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.686 us | 17.860 us | 29.047 us | 0.32x |
| `structural/ordered-keyword-window` | 5.482 us | 17.713 us | 29.060 us | 0.31x |
| `structural/modifier-family-to-type-window` | 76.600 us | 259.201 us | 271.988 us | 0.30x |
| `lokad/lexer/string` | 25.151 us | 95.367 us | 101.145 us | 0.26x |
| `lokad/folding/region-marker` | 6.925 us | 36.394 us | 60.952 us | 0.19x |
| `structural/ordered-identifier-window` | 5.035 us | 28.951 us | 39.979 us | 0.17x |
| `structural/modifier-family-to-type-pair` | 23.539 us | 135.714 us | 147.831 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.625 us | 24.525 us | 24.956 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 18.179 us | 217.784 us | 230.418 us | 0.08x |
| `lokad/langserv/url-download` | 2.545 us | 50.520 us | 51.000 us | 0.05x |
| `balancing/begin-end-blocks` | 5.733 us | 4,410.883 us | 4,437.517 us | 0.0013x |
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
| `lokad/imports/module-imports` | 15.772 us | 4.473 us | 26.925 us | 3.53x |
| `structural/keyword-family-to-capitalized-identifier` | 18.149 us | 7.711 us | 18.530 us | 2.35x |
| `structural/modifier-family-to-type-pair` | 23.569 us | 10.743 us | 22.616 us | 2.19x |
| `lokad/langserv/color-short-hex` | 1.685 us | 1.001 us | 1.183 us | 1.68x |
| `structural/keyword-to-generic-type` | 5.359 us | 3.536 us | 14.708 us | 1.52x |
| `lokad/langserv/helper-identifier` | 7.234 us | 4.830 us | 5.805 us | 1.50x |
| `lokad/lexer/doc-line` | 3.352 us | 2.284 us | 2.974 us | 1.47x |
| `lokad/lexer/operator-run` | 2.914 us | 2.058 us | 2.501 us | 1.42x |
| `lokad/lexer/identifier` | 23.898 us | 17.949 us | 22.239 us | 1.33x |
| `fallback/lookahead` | 5.554 us | 4.248 us | 15.449 us | 1.31x |
| `structural/ordered-keyword-pair` | 5.381 us | 4.172 us | 15.252 us | 1.29x |
| `structural/modifier-family-to-type-window` | 76.754 us | 60.755 us | 69.508 us | 1.26x |
| `lokad/langserv/identifier-validator` | 11.419 us | 9.360 us | 11.705 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.377 us | 2.940 us | 3.624 us | 1.15x |
| `lokad/style/hex-color` | 0.613 us | 0.545 us | 0.615 us | 1.12x |
| `lokad/lexer/number` | 8.303 us | 7.604 us | 8.622 us | 1.09x |
| `lokad/langserv/color-rgb-hex` | 2.048 us | 1.972 us | 2.149 us | 1.04x |
| `literal/identifier-token-casei` | 5.848 us | 5.978 us | 17.192 us | 0.98x |
| `literal-family/type-token-family` | 10.620 us | 11.563 us | 22.374 us | 0.92x |
| `lokad/style/cell-ref` | 1.486 us | 1.838 us | 1.985 us | 0.81x |
| `structural/method-family-generic-call` | 3.461 us | 4.505 us | 15.301 us | 0.77x |
| `structural/method-family-call` | 3.866 us | 5.132 us | 15.752 us | 0.75x |
| `literal-family/method-token-family` | 3.927 us | 5.301 us | 16.386 us | 0.74x |
| `structural/ordered-modifier-pair` | 5.712 us | 8.672 us | 19.814 us | 0.66x |
| `literal/call-token` | 2.604 us | 3.988 us | 14.637 us | 0.65x |
| `literal/identifier-token` | 2.803 us | 4.340 us | 15.687 us | 0.65x |
| `structural/ordered-keyword-window` | 5.476 us | 8.622 us | 19.690 us | 0.64x |
| `lokad/style/range-ref` | 0.937 us | 1.553 us | 1.597 us | 0.60x |
| `lokad/lexer/string` | 25.299 us | 46.791 us | 50.397 us | 0.54x |
| `structural/ordered-identifier-window` | 5.044 us | 10.936 us | 21.633 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.633 us | 7.480 us | 8.003 us | 0.35x |
| `lokad/langserv/url-download` | 2.506 us | 12.991 us | 13.494 us | 0.19x |
| `lokad/folding/region-marker` | 6.927 us | 36.897 us | 59.882 us | 0.19x |
| `balancing/begin-end-blocks` | 5.726 us | 468.831 us | 495.988 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
