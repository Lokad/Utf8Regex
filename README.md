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
and resource checks passed in **46m19s**. The [run receipt](bench/Experiments/DeliveryDev48-20261107/full-refresh.json)
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
| `literal/sherlock-zh` | 85.696 us | 18.821 us | 2,175.714 us | 4.55x |
| `literal/sherlock-ru` | 176.262 us | 99.236 us | 5,248.820 us | 1.78x |
| `literal-alternate/sherlock-zh` | 104.882 us | 69.449 us | 2,206.226 us | 1.51x |
| `literal/sherlock-casei-en` | 156.075 us | 198.144 us | 577.327 us | 0.79x |
| `literal/sherlock-casei-ru` | 252.502 us | 366.880 us | 5,629.338 us | 0.69x |
| `literal/sherlock-en` | 56.844 us | 87.679 us | 467.547 us | 0.65x |
| `ruff-noqa/tweaked` | 4,421.506 us | 7,659.855 us | 42,790.150 us | 0.58x |
| `aws-keys/quick` | 5,051.907 us | 9,620.750 us | 44,246.850 us | 0.53x |
| `bounded-repeat/letters-en` | 1,113.735 us | 2,838.944 us | 2,942.073 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,661.959 us | 4,856.887 us | 10,020.950 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 198.811 us | 598.244 us | 1,028.935 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,978.724 us | 5,983.915 us | 6,807.700 us | 0.33x |
| `dictionary/single` | 8,249.540 us | 26,070.600 us | 26,098.167 us | 0.32x |
| `literal-alternate/sherlock-en` | 392.489 us | 1,550.756 us | 2,064.545 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 153.311 us | 669.336 us | 1,076.361 us | 0.23x |
| `bounded-repeat/capitals` | 9,378.625 us | 46,134.150 us | 49,999.900 us | 0.20x |
| `bounded-repeat/context` | 24,431.250 us | 147,859.900 us | 152,820.300 us | 0.17x |
| `aws-keys/full` | 10,778.014 us | 87,480.700 us | 117,584.000 us | 0.12x |
| `ruff-noqa/real` | 4,518.247 us | 70,367.950 us | 86,368.900 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 946.385 us | 23,565.400 us | 28,992.500 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 131.635 us | 11,469.829 us | 12,371.329 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,191.946 us | 484.373 us | 8,948.075 us | 2.46x |
| `common/match-word` | `Match` | 0.198 us | 0.168 us | 1.132 us | 1.18x |
| `industry/leipzig-twain-count` | `Count` | 1,154.095 us | 1,321.480 us | 8,551.260 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,442.704 us | 1,885.241 us | 10,429.875 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.110 us | 0.148 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.192 us | 0.211 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 131.665 us | 262.405 us | 493.407 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.170 us | 0.187 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.178 us | 0.200 us | 0.44x |
| `industry/leipzig-name-family-count` | `Count` | 4,044.083 us | 9,881.663 us | 16,926.133 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.188 us | 0.207 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.240 us | 0.276 us | 0.39x |
| `common/replace-words` | `Replace` | 28.207 us | 75.978 us | 77.851 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.098 us | 0.271 us | 0.297 us | 0.36x |
| `common/matches-word` | `Count` | 1.352 us | 4.231 us | 5.959 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.352 us | 0.376 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,342.215 us | 8,356.389 us | 16,975.967 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.137 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.082 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 14.595 us | 80.039 us | 81.651 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.023 us | 975.349 us | 1,238.697 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.563 us | 0.594 us | 0.17x |
| `common/matches-words` | `Count` | 9.370 us | 70.154 us | 71.895 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.615 us | 0.651 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.831 us | 0.878 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,095.084 us | 19,627.175 us | 20,269.875 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,456.391 us | 32,501.400 us | 43,591.100 us | 0.11x |
| `industry/leipzig-symbol-count` | `Count` | 1,819.045 us | 23,205.650 us | 30,273.400 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 521.199 us | 8,216.230 us | 8,751.544 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.470 us | 164.516 us | 166.259 us | 0.05x |
| `common/matches-set` | `Count` | 7.858 us | 197.995 us | 200.712 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 235.617 us | 11,952.814 us | 12,629.614 us | 0.02x |
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
| `literal/sherlock-zh` | 85.735 us | 23.899 us | 2,196.003 us | 3.59x |
| `literal-alternate/sherlock-zh` | 104.787 us | 36.076 us | 2,144.317 us | 2.90x |
| `literal/sherlock-casei-en` | 155.622 us | 76.951 us | 440.738 us | 2.02x |
| `literal/sherlock-ru` | 176.865 us | 90.181 us | 5,206.900 us | 1.96x |
| `aws-keys/quick` | 5,069.613 us | 3,671.448 us | 35,947.850 us | 1.38x |
| `literal/sherlock-casei-ru` | 252.781 us | 198.435 us | 5,378.153 us | 1.27x |
| `literal-alternate/sherlock-en` | 406.635 us | 343.061 us | 769.189 us | 1.19x |
| `literal-alternate/sherlock-en-mixed` | 197.925 us | 176.005 us | 561.281 us | 1.12x |
| `bounded-repeat/letters-ru` | 1,976.151 us | 2,074.892 us | 2,896.927 us | 0.95x |
| `ruff-noqa/tweaked` | 4,448.629 us | 4,768.265 us | 36,480.100 us | 0.93x |
| `literal-alternate/sherlock-casei-en` | 132.619 us | 151.841 us | 530.724 us | 0.87x |
| `literal-alternate/sherlock-en-nomatch` | 154.447 us | 205.427 us | 563.606 us | 0.75x |
| `literal/sherlock-en` | 56.987 us | 77.465 us | 410.572 us | 0.74x |
| `literal-alternate/sherlock-ru` | 1,684.369 us | 2,353.594 us | 7,510.925 us | 0.72x |
| `bounded-repeat/capitals` | 9,443.325 us | 13,432.867 us | 16,374.525 us | 0.70x |
| `bounded-repeat/letters-en` | 1,113.378 us | 1,851.595 us | 1,930.677 us | 0.60x |
| `aws-keys/full` | 9,357.725 us | 18,554.200 us | 49,952.167 us | 0.50x |
| `literal-alternate/sherlock-casei-ru` | 949.109 us | 1,969.377 us | 7,268.100 us | 0.48x |
| `bounded-repeat/context` | 24,408.825 us | 69,250.400 us | 72,364.050 us | 0.35x |
| `ruff-noqa/real` | 4,524.076 us | 20,622.950 us | 34,435.800 us | 0.22x |
| `dictionary/single` | 8,259.650 us | 45,898.200 us | 45,974.100 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,200.531 us | 453.254 us | 7,721.225 us | 2.65x |
| `common/match-word` | `Match` | 0.199 us | 0.079 us | 1.018 us | 2.51x |
| `industry/leipzig-river-window-count` | `Count` | 3,488.123 us | 1,564.388 us | 9,179.980 us | 2.23x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 131.686 us | 59.816 us | 267.298 us | 2.20x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.044 us | 0.074 us | 2.15x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.068 us | 0.086 us | 1.76x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.030 us | 0.047 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.088 us | 1.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.032 us | 0.051 us | 1.58x |
| `common/replace-words` | `Replace` | 28.104 us | 18.209 us | 21.019 us | 1.54x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.049 us | 0.069 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,825.869 us | 1,216.592 us | 7,123.650 us | 1.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.055 us | 0.078 us | 1.39x |
| `industry/mariomka-uri-count` | `Count` | 1,445.531 us | 1,086.063 us | 8,368.200 us | 1.33x |
| `industry/leipzig-name-family-count` | `Count` | 4,042.647 us | 3,064.808 us | 7,980.500 us | 1.32x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.058 us | 0.075 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.098 us | 0.080 us | 0.108 us | 1.22x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.116 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.088 us | 0.121 us | 1.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.082 us | 0.088 us | 0.138 us | 0.93x |
| `industry/leipzig-twain-count` | `Count` | 1,158.388 us | 1,276.378 us | 6,522.020 us | 0.91x |
| `common/split-words` | `Split` | 14.571 us | 21.698 us | 23.449 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.073 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,352.176 us | 3,550.868 us | 11,015.700 us | 0.66x |
| `common/matches-words` | `Count` | 9.377 us | 14.291 us | 16.796 us | 0.66x |
| `common/matches-word` | `Count` | 1.346 us | 2.347 us | 3.484 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.203 us | 0.238 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.052 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.597 us | 603.266 us | 861.591 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,094.272 us | 8,887.867 us | 9,407.275 us | 0.24x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 520.789 us | 3,951.165 us | 4,309.900 us | 0.13x |
| `common/matches-set` | `Count` | 7.810 us | 106.293 us | 106.902 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.447 us | 105.416 us | 107.603 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 235.983 us | 4,820.106 us | 5,216.140 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.048 us | 6.325 us | 28.619 us | 1.75x |
| `structural/keyword-to-generic-type` | 5.528 us | 4.120 us | 15.766 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.674 us | 1.353 us | 1.457 us | 1.24x |
| `lokad/lexer/doc-line` | 3.356 us | 3.017 us | 3.571 us | 1.11x |
| `lokad/lexer/identifier` | 23.504 us | 22.997 us | 25.697 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.611 us | 5.526 us | 17.171 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.151 us | 7.213 us | 7.565 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.461 us | 12.345 us | 13.788 us | 0.93x |
| `fallback/lookahead` | 5.602 us | 6.145 us | 17.430 us | 0.91x |
| `lokad/style/hex-color` | 0.737 us | 0.809 us | 0.858 us | 0.91x |
| `lokad/lexer/operator-run` | 2.916 us | 3.481 us | 3.857 us | 0.84x |
| `literal-family/method-token-family` | 7.137 us | 9.157 us | 20.187 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 2.070 us | 2.966 us | 3.034 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.374 us | 4.973 us | 5.566 us | 0.68x |
| `literal/identifier-token` | 2.781 us | 4.359 us | 15.602 us | 0.64x |
| `literal/call-token` | 2.589 us | 4.309 us | 15.528 us | 0.60x |
| `lokad/lexer/number` | 8.310 us | 14.433 us | 14.976 us | 0.58x |
| `structural/ordered-identifier-window` | 16.049 us | 28.713 us | 39.707 us | 0.56x |
| `structural/method-family-generic-call` | 3.468 us | 6.771 us | 18.205 us | 0.51x |
| `lokad/style/cell-ref` | 1.491 us | 2.959 us | 3.050 us | 0.50x |
| `structural/method-family-call` | 3.876 us | 8.136 us | 19.330 us | 0.48x |
| `literal-family/type-token-family` | 10.533 us | 23.500 us | 34.794 us | 0.45x |
| `literal/identifier-token-casei` | 5.699 us | 15.017 us | 26.360 us | 0.38x |
| `lokad/style/range-ref` | 0.923 us | 2.769 us | 2.775 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.557 us | 17.558 us | 28.763 us | 0.32x |
| `structural/modifier-family-to-type-window` | 79.409 us | 254.102 us | 268.078 us | 0.31x |
| `structural/ordered-keyword-window` | 5.403 us | 17.862 us | 29.122 us | 0.30x |
| `lokad/lexer/string` | 21.690 us | 95.103 us | 99.583 us | 0.23x |
| `lokad/folding/region-marker` | 6.391 us | 36.450 us | 60.742 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.559 us | 24.195 us | 24.797 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.385 us | 215.951 us | 229.095 us | 0.05x |
| `lokad/langserv/url-download` | 2.487 us | 53.098 us | 53.369 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.937 us | 131.037 us | 143.158 us | 0.04x |
| `balancing/begin-end-blocks` | 5.650 us | 4,199.974 us | 4,198.250 us | 0.0013x |
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
| `lokad/imports/module-imports` | 11.065 us | 4.412 us | 26.137 us | 2.51x |
| `lokad/langserv/color-short-hex` | 1.674 us | 1.006 us | 1.176 us | 1.66x |
| `lokad/langserv/helper-identifier` | 7.190 us | 4.514 us | 5.634 us | 1.59x |
| `lokad/lexer/doc-line` | 3.358 us | 2.122 us | 2.951 us | 1.58x |
| `structural/keyword-to-generic-type` | 5.524 us | 3.528 us | 14.625 us | 1.57x |
| `structural/ordered-identifier-window` | 16.069 us | 10.582 us | 21.335 us | 1.52x |
| `lokad/style/hex-color` | 0.735 us | 0.498 us | 0.589 us | 1.47x |
| `lokad/lexer/identifier` | 23.596 us | 16.100 us | 20.673 us | 1.47x |
| `lokad/lexer/operator-run` | 2.913 us | 1.993 us | 2.541 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.523 us | 7.897 us | 18.988 us | 1.46x |
| `structural/modifier-family-to-type-window` | 79.593 us | 55.928 us | 67.961 us | 1.42x |
| `structural/ordered-keyword-pair` | 5.608 us | 4.168 us | 15.233 us | 1.35x |
| `fallback/lookahead` | 5.616 us | 4.245 us | 15.121 us | 1.32x |
| `lokad/docs/inline-doc-prefix` | 3.388 us | 2.812 us | 3.553 us | 1.20x |
| `lokad/langserv/identifier-validator` | 11.346 us | 9.438 us | 11.232 us | 1.20x |
| `lokad/lexer/number` | 8.322 us | 7.256 us | 8.223 us | 1.15x |
| `lokad/langserv/color-rgb-hex` | 2.030 us | 1.819 us | 2.078 us | 1.12x |
| `literal/identifier-token-casei` | 5.719 us | 5.920 us | 17.187 us | 0.97x |
| `literal-family/type-token-family` | 10.538 us | 11.469 us | 22.396 us | 0.92x |
| `lokad/style/cell-ref` | 1.510 us | 1.845 us | 2.054 us | 0.82x |
| `structural/method-family-call` | 3.879 us | 5.088 us | 15.837 us | 0.76x |
| `structural/method-family-generic-call` | 3.438 us | 4.643 us | 15.296 us | 0.74x |
| `literal-family/method-token-family` | 3.883 us | 5.316 us | 16.097 us | 0.73x |
| `literal/call-token` | 2.604 us | 3.935 us | 14.618 us | 0.66x |
| `literal/identifier-token` | 2.791 us | 4.329 us | 15.304 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.600 us | 8.702 us | 19.800 us | 0.64x |
| `lokad/style/range-ref` | 0.930 us | 1.475 us | 1.560 us | 0.63x |
| `structural/ordered-keyword-window` | 5.412 us | 8.621 us | 19.817 us | 0.63x |
| `lokad/lexer/string` | 21.751 us | 45.195 us | 49.075 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 4.935 us | 10.640 us | 21.459 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.573 us | 7.504 us | 8.034 us | 0.34x |
| `lokad/langserv/url-download` | 2.513 us | 12.824 us | 13.337 us | 0.20x |
| `lokad/folding/region-marker` | 6.365 us | 36.639 us | 59.137 us | 0.17x |
| `balancing/begin-end-blocks` | 5.705 us | 462.338 us | 490.910 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
