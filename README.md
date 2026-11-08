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

The tables were refreshed on **7 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m17s**. The [run receipt](bench/Experiments/DeliveryDev55-20261107/full-refresh.json)
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
[PythonRe](src/Lokad.Utf8Regex.PythonRe/README.md#benchmarks) and
[PCRE2](src/Lokad.Utf8Regex.Pcre2/README.md#benchmarks) README sections.

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
| `literal/sherlock-zh` | 81.212 us | 18.639 us | 2,175.481 us | 4.36x |
| `literal/sherlock-ru` | 164.826 us | 97.445 us | 5,309.886 us | 1.69x |
| `literal-alternate/sherlock-zh` | 108.610 us | 67.077 us | 2,156.754 us | 1.62x |
| `literal/sherlock-casei-en` | 153.870 us | 199.553 us | 593.506 us | 0.77x |
| `literal/sherlock-casei-ru` | 253.186 us | 360.355 us | 5,542.157 us | 0.70x |
| `literal/sherlock-en` | 55.294 us | 83.838 us | 468.632 us | 0.66x |
| `aws-keys/quick` | 5,623.542 us | 9,532.250 us | 42,392.850 us | 0.59x |
| `ruff-noqa/tweaked` | 4,314.128 us | 7,397.509 us | 40,875.500 us | 0.58x |
| `bounded-repeat/letters-en` | 1,102.894 us | 2,839.122 us | 2,954.108 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,591.715 us | 4,713.962 us | 9,771.650 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,947.856 us | 5,914.946 us | 6,706.442 us | 0.33x |
| `dictionary/single` | 8,194.260 us | 26,038.967 us | 26,031.400 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 185.339 us | 612.275 us | 1,034.823 us | 0.30x |
| `literal-alternate/sherlock-en` | 379.151 us | 1,517.804 us | 2,044.364 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 151.274 us | 664.217 us | 1,074.032 us | 0.23x |
| `bounded-repeat/capitals` | 9,308.178 us | 45,542.400 us | 48,636.900 us | 0.20x |
| `bounded-repeat/context` | 20,953.925 us | 147,955.500 us | 151,528.700 us | 0.14x |
| `aws-keys/full` | 10,447.700 us | 86,168.100 us | 115,042.800 us | 0.12x |
| `ruff-noqa/real` | 4,343.972 us | 69,770.600 us | 100,750.400 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 935.770 us | 22,805.425 us | 27,916.500 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 123.149 us | 11,155.186 us | 12,030.629 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,164.461 us | 481.782 us | 8,908.850 us | 2.42x |
| `common/match-word` | `Match` | 0.195 us | 0.164 us | 1.074 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,206.013 us | 1,338.990 us | 8,669.489 us | 0.90x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.088 us | 0.107 us | 0.144 us | 0.82x |
| `industry/mariomka-uri-count` | `Count` | 1,428.630 us | 1,853.783 us | 10,477.150 us | 0.77x |
| `common/uri-miss` | `IsMatch` | 0.115 us | 0.191 us | 0.210 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.099 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 129.437 us | 260.837 us | 495.165 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.167 us | 0.189 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.172 us | 0.191 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.190 us | 0.210 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 3,922.784 us | 9,691.737 us | 16,720.433 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.226 us | 0.261 us | 0.40x |
| `common/replace-words` | `Replace` | 27.735 us | 75.663 us | 76.915 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.258 us | 0.286 us | 0.36x |
| `common/matches-word` | `Count` | 1.329 us | 4.159 us | 5.882 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.342 us | 0.368 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.107 us | 0.135 us | 0.26x |
| `industry/mariomka-ip-count` | `Count` | 2,183.026 us | 8,594.267 us | 16,989.567 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.097 us | 0.20x |
| `common/split-words` | `Split` | 14.437 us | 76.571 us | 78.472 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.556 us | 958.645 us | 1,229.641 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.555 us | 0.595 us | 0.17x |
| `common/matches-words` | `Count` | 9.220 us | 70.086 us | 70.945 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.610 us | 0.643 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.074 us | 0.088 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.091 us | 0.844 us | 0.879 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,272.046 us | 31,575.633 us | 38,425.950 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,902.551 us | 19,285.825 us | 19,933.600 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,863.515 us | 22,599.250 us | 30,096.250 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 493.354 us | 8,462.722 us | 8,983.544 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.499 us | 161.496 us | 162.766 us | 0.05x |
| `common/matches-set` | `Count` | 7.385 us | 196.037 us | 197.544 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 232.679 us | 12,035.686 us | 12,488.183 us | 0.02x |
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
| `literal/sherlock-zh` | 81.357 us | 23.520 us | 2,178.869 us | 3.46x |
| `literal-alternate/sherlock-zh` | 108.657 us | 35.466 us | 2,152.546 us | 3.06x |
| `literal/sherlock-casei-en` | 154.010 us | 77.003 us | 455.164 us | 2.00x |
| `literal/sherlock-ru` | 172.347 us | 88.742 us | 5,178.820 us | 1.94x |
| `aws-keys/quick` | 6,417.675 us | 3,542.777 us | 33,825.250 us | 1.81x |
| `literal/sherlock-casei-ru` | 251.443 us | 197.243 us | 5,269.640 us | 1.27x |
| `literal-alternate/sherlock-en` | 379.839 us | 342.138 us | 743.567 us | 1.11x |
| `literal-alternate/sherlock-en-mixed` | 183.013 us | 175.212 us | 565.684 us | 1.04x |
| `ruff-noqa/tweaked` | 4,357.944 us | 4,505.000 us | 34,669.850 us | 0.97x |
| `bounded-repeat/letters-ru` | 1,947.660 us | 2,028.711 us | 2,838.841 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 121.816 us | 150.731 us | 606.074 us | 0.81x |
| `literal-alternate/sherlock-en-nomatch` | 151.693 us | 198.296 us | 553.812 us | 0.76x |
| `literal/sherlock-en` | 55.114 us | 74.006 us | 428.223 us | 0.74x |
| `bounded-repeat/capitals` | 9,265.475 us | 13,386.767 us | 16,598.425 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,593.931 us | 2,308.803 us | 7,289.550 us | 0.69x |
| `bounded-repeat/letters-en` | 1,103.462 us | 1,813.279 us | 1,915.155 us | 0.61x |
| `aws-keys/full` | 9,131.062 us | 18,220.520 us | 48,937.733 us | 0.50x |
| `literal-alternate/sherlock-casei-ru` | 934.146 us | 1,930.720 us | 7,106.500 us | 0.48x |
| `bounded-repeat/context` | 20,097.025 us | 68,840.300 us | 72,347.100 us | 0.29x |
| `ruff-noqa/real` | 4,350.183 us | 20,636.975 us | 53,600.800 us | 0.21x |
| `dictionary/single` | 8,139.920 us | 45,563.650 us | 45,514.350 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.194 us | 0.075 us | 0.970 us | 2.59x |
| `industry/mariomka-email-count` | `Count` | 1,167.708 us | 451.659 us | 7,559.000 us | 2.59x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 130.629 us | 57.516 us | 264.311 us | 2.27x |
| `industry/leipzig-river-window-count` | `Count` | 3,300.770 us | 1,476.531 us | 7,657.260 us | 2.24x |
| `common/backtracking` | `IsMatch` | 0.091 us | 0.043 us | 0.073 us | 2.11x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.085 us | 0.048 us | 0.086 us | 1.78x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.028 us | 0.047 us | 1.73x |
| `common/uri-miss` | `IsMatch` | 0.115 us | 0.066 us | 0.086 us | 1.73x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.029 us | 0.048 us | 1.72x |
| `common/replace-words` | `Replace` | 27.881 us | 17.128 us | 19.955 us | 1.63x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.048 us | 0.068 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.050 us | 0.067 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,875.495 us | 1,286.167 us | 7,606.660 us | 1.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.055 us | 0.079 us | 1.37x |
| `industry/leipzig-name-family-count` | `Count` | 4,042.642 us | 3,016.700 us | 7,993.775 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,444.402 us | 1,077.954 us | 8,348.100 us | 1.34x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.079 us | 0.105 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.088 us | 0.113 us | 1.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.092 us | 0.088 us | 0.131 us | 1.05x |
| `common/uri-match` | `IsMatch` | 0.089 us | 0.088 us | 0.118 us | 1.02x |
| `industry/leipzig-twain-count` | `Count` | 1,200.963 us | 1,320.947 us | 7,520.200 us | 0.91x |
| `common/matches-words` | `Count` | 9.162 us | 10.864 us | 13.920 us | 0.84x |
| `common/split-words` | `Split` | 14.392 us | 20.780 us | 22.558 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.071 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,173.349 us | 3,452.974 us | 10,912.000 us | 0.63x |
| `common/matches-word` | `Count` | 1.335 us | 2.359 us | 3.430 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.202 us | 0.234 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.034 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,917.380 us | 8,794.789 us | 9,543.737 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 489.689 us | 3,897.660 us | 4,299.033 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.653 us | 1,473.885 us | 1,808.314 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.508 us | 104.037 us | 105.448 us | 0.07x |
| `common/matches-set` | `Count` | 7.395 us | 104.187 us | 106.422 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 231.709 us | 4,682.581 us | 5,121.553 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.035 us | 6.261 us | 28.984 us | 1.60x |
| `structural/keyword-to-generic-type` | 5.487 us | 4.036 us | 15.824 us | 1.36x |
| `lokad/langserv/color-short-hex` | 1.676 us | 1.320 us | 1.411 us | 1.27x |
| `lokad/lexer/doc-line` | 3.299 us | 3.018 us | 3.543 us | 1.09x |
| `lokad/lexer/identifier` | 24.038 us | 22.420 us | 25.210 us | 1.07x |
| `structural/ordered-keyword-pair` | 5.561 us | 5.401 us | 17.028 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.101 us | 7.002 us | 7.615 us | 1.01x |
| `lokad/lexer/operator-run` | 3.393 us | 3.436 us | 3.782 us | 0.99x |
| `lokad/style/hex-color` | 0.729 us | 0.784 us | 0.821 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.271 us | 12.372 us | 13.725 us | 0.91x |
| `fallback/lookahead` | 5.385 us | 5.956 us | 16.857 us | 0.90x |
| `literal-family/method-token-family` | 7.075 us | 9.255 us | 20.005 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 2.035 us | 2.917 us | 2.920 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.311 us | 4.930 us | 5.356 us | 0.67x |
| `literal/identifier-token` | 2.754 us | 4.264 us | 15.477 us | 0.65x |
| `lokad/lexer/number` | 8.708 us | 14.028 us | 14.746 us | 0.62x |
| `literal/call-token` | 2.569 us | 4.281 us | 15.650 us | 0.60x |
| `lokad/style/cell-ref` | 1.559 us | 2.783 us | 2.831 us | 0.56x |
| `structural/ordered-identifier-window` | 15.786 us | 28.506 us | 39.055 us | 0.55x |
| `structural/method-family-generic-call` | 3.402 us | 6.684 us | 17.796 us | 0.51x |
| `structural/method-family-call` | 3.810 us | 8.251 us | 19.222 us | 0.46x |
| `literal-family/type-token-family` | 10.400 us | 23.203 us | 34.117 us | 0.45x |
| `literal/identifier-token-casei` | 6.050 us | 15.473 us | 26.069 us | 0.39x |
| `lokad/style/range-ref` | 0.888 us | 2.746 us | 2.697 us | 0.32x |
| `structural/modifier-family-to-type-window` | 77.800 us | 251.355 us | 265.705 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.354 us | 17.372 us | 28.053 us | 0.31x |
| `structural/ordered-keyword-window` | 5.349 us | 17.650 us | 28.662 us | 0.30x |
| `lokad/lexer/string` | 21.390 us | 94.141 us | 99.042 us | 0.23x |
| `lokad/folding/region-marker` | 5.964 us | 36.346 us | 60.147 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.491 us | 24.090 us | 24.378 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.283 us | 213.735 us | 228.454 us | 0.05x |
| `lokad/langserv/url-download` | 2.506 us | 49.670 us | 49.974 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.900 us | 133.152 us | 144.879 us | 0.04x |
| `balancing/begin-end-blocks` | 5.640 us | 4,303.550 us | 4,295.350 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.049 us | 4.392 us | 26.055 us | 2.29x |
| `lokad/lexer/operator-run` | 3.389 us | 1.894 us | 2.498 us | 1.79x |
| `lokad/langserv/color-short-hex` | 1.656 us | 0.963 us | 1.175 us | 1.72x |
| `structural/keyword-to-generic-type` | 5.507 us | 3.473 us | 14.736 us | 1.59x |
| `lokad/langserv/helper-identifier` | 7.144 us | 4.511 us | 5.607 us | 1.58x |
| `lokad/lexer/doc-line` | 3.304 us | 2.101 us | 2.827 us | 1.57x |
| `structural/ordered-identifier-window` | 15.770 us | 10.380 us | 21.030 us | 1.52x |
| `lokad/lexer/identifier` | 23.929 us | 15.811 us | 20.269 us | 1.51x |
| `structural/keyword-family-to-capitalized-identifier` | 11.319 us | 7.773 us | 18.652 us | 1.46x |
| `lokad/style/hex-color` | 0.729 us | 0.510 us | 0.581 us | 1.43x |
| `structural/modifier-family-to-type-window` | 78.027 us | 55.073 us | 67.224 us | 1.42x |
| `structural/ordered-keyword-pair` | 5.556 us | 4.110 us | 15.127 us | 1.35x |
| `lokad/langserv/identifier-validator` | 11.196 us | 8.561 us | 10.757 us | 1.31x |
| `fallback/lookahead` | 5.370 us | 4.172 us | 14.632 us | 1.29x |
| `lokad/docs/inline-doc-prefix` | 3.341 us | 2.661 us | 3.462 us | 1.26x |
| `lokad/lexer/number` | 8.659 us | 6.970 us | 8.154 us | 1.24x |
| `lokad/langserv/color-rgb-hex` | 2.081 us | 1.806 us | 1.957 us | 1.15x |
| `literal/identifier-token-casei` | 5.970 us | 5.811 us | 16.970 us | 1.03x |
| `literal-family/type-token-family` | 10.393 us | 11.368 us | 22.055 us | 0.91x |
| `lokad/style/cell-ref` | 1.566 us | 1.735 us | 1.883 us | 0.90x |
| `structural/method-family-generic-call` | 3.395 us | 4.450 us | 14.969 us | 0.76x |
| `structural/method-family-call` | 3.791 us | 5.020 us | 15.536 us | 0.76x |
| `literal-family/method-token-family` | 3.819 us | 5.256 us | 15.889 us | 0.73x |
| `literal/call-token` | 2.587 us | 3.932 us | 14.796 us | 0.66x |
| `literal/identifier-token` | 2.732 us | 4.225 us | 15.268 us | 0.65x |
| `structural/ordered-keyword-window` | 5.381 us | 8.462 us | 19.150 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.364 us | 8.608 us | 19.444 us | 0.62x |
| `lokad/style/range-ref` | 0.901 us | 1.465 us | 1.503 us | 0.61x |
| `lokad/lexer/string` | 21.513 us | 45.320 us | 48.788 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.872 us | 10.732 us | 21.316 us | 0.45x |
| `lokad/langserv/url-dashboard` | 2.494 us | 7.406 us | 7.942 us | 0.34x |
| `lokad/langserv/url-download` | 2.506 us | 12.888 us | 13.792 us | 0.19x |
| `lokad/folding/region-marker` | 5.975 us | 35.458 us | 61.681 us | 0.17x |
| `balancing/begin-end-blocks` | 5.609 us | 462.279 us | 494.772 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
