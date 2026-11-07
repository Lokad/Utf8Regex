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
and resource checks passed in **46m27s**. The [run receipt](bench/Experiments/DeliveryDev49-20261107/full-refresh.json)
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
| `literal/sherlock-zh` | 85.846 us | 18.994 us | 2,156.322 us | 4.52x |
| `literal/sherlock-ru` | 171.770 us | 99.403 us | 5,170.200 us | 1.73x |
| `literal-alternate/sherlock-zh` | 103.845 us | 69.220 us | 2,174.343 us | 1.50x |
| `literal/sherlock-casei-en` | 155.616 us | 190.876 us | 632.932 us | 0.82x |
| `aws-keys/quick` | 6,475.400 us | 9,308.778 us | 39,933.000 us | 0.70x |
| `literal/sherlock-en` | 56.657 us | 84.960 us | 445.080 us | 0.67x |
| `literal/sherlock-casei-ru` | 256.749 us | 392.926 us | 5,707.007 us | 0.65x |
| `ruff-noqa/tweaked` | 4,353.917 us | 7,506.464 us | 41,067.050 us | 0.58x |
| `bounded-repeat/letters-en` | 1,103.646 us | 2,794.925 us | 2,908.138 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,647.587 us | 4,797.431 us | 10,057.175 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 198.900 us | 599.259 us | 1,032.091 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,951.515 us | 5,949.562 us | 6,762.742 us | 0.33x |
| `dictionary/single` | 8,216.020 us | 26,156.867 us | 26,210.100 us | 0.31x |
| `literal-alternate/sherlock-en` | 393.424 us | 1,507.010 us | 2,014.697 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 154.644 us | 648.225 us | 1,089.796 us | 0.24x |
| `bounded-repeat/capitals` | 9,432.575 us | 46,459.600 us | 49,939.250 us | 0.20x |
| `bounded-repeat/context` | 24,421.525 us | 147,080.500 us | 151,712.900 us | 0.17x |
| `aws-keys/full` | 10,499.938 us | 88,044.000 us | 117,351.600 us | 0.12x |
| `ruff-noqa/real` | 4,380.806 us | 68,728.400 us | 96,778.200 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 948.246 us | 22,876.225 us | 28,502.500 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 126.077 us | 11,248.086 us | 12,063.629 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,169.702 us | 488.854 us | 8,948.900 us | 2.39x |
| `common/match-word` | `Match` | 0.198 us | 0.172 us | 1.125 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,160.874 us | 1,326.158 us | 8,363.383 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,419.328 us | 1,905.624 us | 10,377.225 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.111 us | 0.149 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.193 us | 0.211 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.103 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 130.894 us | 262.294 us | 486.299 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.163 us | 0.180 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.172 us | 0.192 us | 0.44x |
| `common/email-match` | `IsMatch` | 0.112 us | 0.271 us | 0.297 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 4,005.247 us | 9,755.925 us | 16,805.433 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.187 us | 0.205 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.229 us | 0.268 us | 0.40x |
| `common/replace-words` | `Replace` | 28.046 us | 77.471 us | 79.007 us | 0.36x |
| `common/matches-word` | `Count` | 1.345 us | 4.215 us | 5.897 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.344 us | 0.367 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,325.158 us | 8,311.460 us | 16,790.967 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.085 us | 0.100 us | 0.20x |
| `common/split-words` | `Split` | 14.470 us | 77.876 us | 79.187 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.386 us | 962.787 us | 1,234.972 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.549 us | 0.582 us | 0.17x |
| `common/matches-words` | `Count` | 9.428 us | 71.885 us | 73.928 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.608 us | 0.636 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.090 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.827 us | 0.869 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,115.009 us | 19,274.100 us | 20,066.750 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,345.252 us | 31,876.733 us | 39,237.200 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,806.186 us | 23,479.875 us | 30,004.250 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 512.698 us | 8,288.610 us | 8,795.656 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.544 us | 163.054 us | 165.205 us | 0.05x |
| `common/matches-set` | `Count` | 7.457 us | 199.372 us | 199.290 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 259.383 us | 11,888.600 us | 12,452.550 us | 0.02x |
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
| `literal/sherlock-zh` | 85.745 us | 24.132 us | 2,176.854 us | 3.55x |
| `literal-alternate/sherlock-zh` | 104.728 us | 36.359 us | 2,157.309 us | 2.88x |
| `literal/sherlock-casei-en` | 155.951 us | 77.590 us | 432.632 us | 2.01x |
| `aws-keys/quick` | 6,815.258 us | 3,576.141 us | 35,338.433 us | 1.91x |
| `literal/sherlock-ru` | 168.195 us | 89.923 us | 5,160.553 us | 1.87x |
| `literal/sherlock-casei-ru` | 256.094 us | 225.471 us | 5,516.079 us | 1.14x |
| `literal-alternate/sherlock-en-mixed` | 198.628 us | 176.318 us | 573.323 us | 1.13x |
| `literal-alternate/sherlock-en` | 387.435 us | 372.402 us | 789.917 us | 1.04x |
| `ruff-noqa/tweaked` | 4,361.778 us | 4,570.576 us | 35,673.150 us | 0.95x |
| `bounded-repeat/letters-ru` | 1,963.572 us | 2,065.043 us | 2,846.644 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 126.087 us | 147.747 us | 576.241 us | 0.85x |
| `literal-alternate/sherlock-en-nomatch` | 152.897 us | 200.067 us | 590.890 us | 0.76x |
| `literal/sherlock-en` | 56.609 us | 75.077 us | 409.826 us | 0.75x |
| `bounded-repeat/capitals` | 9,439.288 us | 13,528.383 us | 16,402.800 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,606.571 us | 2,343.555 us | 7,502.275 us | 0.69x |
| `bounded-repeat/letters-en` | 1,111.862 us | 1,841.531 us | 1,920.708 us | 0.60x |
| `aws-keys/full` | 9,249.900 us | 18,345.980 us | 49,608.600 us | 0.50x |
| `literal-alternate/sherlock-casei-ru` | 948.137 us | 1,962.444 us | 7,228.560 us | 0.48x |
| `bounded-repeat/context` | 24,620.300 us | 69,165.250 us | 72,588.900 us | 0.36x |
| `ruff-noqa/real` | 4,402.156 us | 20,611.750 us | 51,770.000 us | 0.21x |
| `dictionary/single` | 8,242.160 us | 45,753.550 us | 46,053.500 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,171.388 us | 457.439 us | 7,711.425 us | 2.56x |
| `common/match-word` | `Match` | 0.195 us | 0.077 us | 0.996 us | 2.55x |
| `industry/leipzig-river-window-count` | `Count` | 3,353.639 us | 1,482.751 us | 7,631.820 us | 2.26x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.043 us | 0.073 us | 2.20x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.557 us | 60.784 us | 269.741 us | 2.18x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.030 us | 0.046 us | 1.79x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.067 us | 0.085 us | 1.77x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.050 us | 1.67x |
| `common/replace-words` | `Replace` | 27.997 us | 17.303 us | 18.096 us | 1.62x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.050 us | 0.086 us | 1.61x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.047 us | 0.067 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.049 us | 0.069 us | 1.54x |
| `industry/leipzig-symbol-count` | `Count` | 1,813.940 us | 1,218.182 us | 7,143.320 us | 1.49x |
| `common/email-match` | `IsMatch` | 0.112 us | 0.081 us | 0.108 us | 1.38x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.056 us | 0.078 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,418.564 us | 1,087.759 us | 8,395.350 us | 1.30x |
| `industry/leipzig-name-family-count` | `Count` | 4,164.259 us | 3,235.237 us | 8,180.000 us | 1.29x |
| `common/email-miss` | `IsMatch` | 0.102 us | 0.089 us | 0.116 us | 1.14x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.088 us | 0.120 us | 1.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.087 us | 0.127 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 1,160.280 us | 1,287.324 us | 7,227.878 us | 0.90x |
| `common/matches-words` | `Count` | 9.416 us | 10.709 us | 14.062 us | 0.88x |
| `common/split-words` | `Split` | 14.504 us | 20.545 us | 22.178 us | 0.71x |
| `industry/mariomka-ip-count` | `Count` | 2,319.264 us | 3,425.723 us | 10,805.067 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-word` | `Count` | 1.347 us | 2.355 us | 3.446 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.202 us | 0.236 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.052 us | 0.32x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,142.880 us | 8,849.789 us | 9,416.856 us | 0.24x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 511.509 us | 3,940.455 us | 4,323.150 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.143 us | 1,484.067 us | 1,813.840 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.553 us | 103.979 us | 106.478 us | 0.07x |
| `common/matches-set` | `Count` | 7.461 us | 105.570 us | 106.643 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 261.020 us | 4,800.325 us | 5,179.520 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.622 us | 6.402 us | 28.787 us | 1.66x |
| `structural/keyword-to-generic-type` | 5.510 us | 4.069 us | 15.767 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.649 us | 1.339 us | 1.434 us | 1.23x |
| `lokad/lexer/doc-line` | 3.384 us | 3.045 us | 3.676 us | 1.11x |
| `structural/ordered-keyword-pair` | 5.582 us | 5.340 us | 17.466 us | 1.05x |
| `lokad/lexer/identifier` | 23.772 us | 22.953 us | 25.546 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.158 us | 7.245 us | 7.778 us | 0.99x |
| `fallback/lookahead` | 5.639 us | 6.151 us | 17.381 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.467 us | 12.629 us | 13.948 us | 0.91x |
| `lokad/lexer/operator-run` | 2.948 us | 3.445 us | 3.859 us | 0.86x |
| `literal-family/method-token-family` | 7.552 us | 9.225 us | 20.226 us | 0.82x |
| `lokad/style/hex-color` | 0.601 us | 0.807 us | 0.842 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 2.023 us | 2.929 us | 2.901 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.365 us | 4.898 us | 5.445 us | 0.69x |
| `literal/identifier-token` | 2.770 us | 4.339 us | 15.490 us | 0.64x |
| `literal/call-token` | 2.586 us | 4.293 us | 15.557 us | 0.60x |
| `lokad/lexer/number` | 8.233 us | 14.416 us | 15.032 us | 0.57x |
| `structural/ordered-identifier-window` | 16.008 us | 28.605 us | 40.115 us | 0.56x |
| `lokad/style/cell-ref` | 1.494 us | 2.905 us | 2.992 us | 0.51x |
| `structural/method-family-generic-call` | 3.427 us | 6.766 us | 17.898 us | 0.51x |
| `structural/method-family-call` | 3.866 us | 8.065 us | 19.111 us | 0.48x |
| `literal-family/type-token-family` | 10.460 us | 23.785 us | 34.791 us | 0.44x |
| `literal/identifier-token-casei` | 5.684 us | 15.201 us | 26.441 us | 0.37x |
| `lokad/style/range-ref` | 0.914 us | 2.717 us | 2.702 us | 0.34x |
| `structural/modifier-family-to-type-window` | 78.240 us | 254.204 us | 267.519 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.400 us | 17.721 us | 29.014 us | 0.30x |
| `structural/ordered-keyword-window` | 5.390 us | 17.734 us | 29.014 us | 0.30x |
| `lokad/lexer/string` | 21.750 us | 95.155 us | 100.459 us | 0.23x |
| `lokad/folding/region-marker` | 6.228 us | 37.654 us | 61.379 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.563 us | 24.363 us | 24.665 us | 0.11x |
| `lokad/langserv/url-download` | 2.570 us | 52.409 us | 52.882 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 11.326 us | 235.770 us | 226.903 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.975 us | 134.920 us | 144.674 us | 0.04x |
| `balancing/begin-end-blocks` | 5.700 us | 4,333.383 us | 4,364.489 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.675 us | 4.394 us | 25.972 us | 2.43x |
| `lokad/langserv/color-short-hex` | 1.646 us | 0.979 us | 1.164 us | 1.68x |
| `structural/keyword-to-generic-type` | 5.542 us | 3.526 us | 14.640 us | 1.57x |
| `lokad/lexer/doc-line` | 3.378 us | 2.171 us | 2.945 us | 1.56x |
| `lokad/langserv/helper-identifier` | 7.174 us | 4.653 us | 5.758 us | 1.54x |
| `structural/ordered-identifier-window` | 16.089 us | 10.709 us | 21.399 us | 1.50x |
| `lokad/lexer/operator-run` | 2.944 us | 2.002 us | 2.504 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.321 us | 7.821 us | 18.414 us | 1.45x |
| `lokad/lexer/identifier` | 23.849 us | 16.870 us | 20.555 us | 1.41x |
| `structural/modifier-family-to-type-window` | 78.030 us | 55.886 us | 67.355 us | 1.40x |
| `fallback/lookahead` | 5.643 us | 4.195 us | 15.077 us | 1.35x |
| `structural/ordered-keyword-pair` | 5.569 us | 4.147 us | 15.517 us | 1.34x |
| `lokad/langserv/identifier-validator` | 11.520 us | 8.938 us | 10.964 us | 1.29x |
| `lokad/docs/inline-doc-prefix` | 3.365 us | 2.803 us | 3.593 us | 1.20x |
| `lokad/style/hex-color` | 0.605 us | 0.504 us | 0.587 us | 1.20x |
| `lokad/lexer/number` | 8.234 us | 7.103 us | 8.300 us | 1.16x |
| `lokad/langserv/color-rgb-hex` | 2.006 us | 1.774 us | 1.950 us | 1.13x |
| `literal/identifier-token-casei` | 5.683 us | 5.878 us | 17.129 us | 0.97x |
| `literal-family/type-token-family` | 10.447 us | 11.419 us | 22.058 us | 0.91x |
| `lokad/style/cell-ref` | 1.488 us | 1.736 us | 1.915 us | 0.86x |
| `structural/method-family-call` | 3.862 us | 4.944 us | 15.802 us | 0.78x |
| `structural/method-family-generic-call` | 3.431 us | 4.494 us | 15.058 us | 0.76x |
| `literal-family/method-token-family` | 3.876 us | 5.339 us | 16.083 us | 0.73x |
| `literal/call-token` | 2.591 us | 3.962 us | 14.662 us | 0.65x |
| `structural/ordered-keyword-window` | 5.432 us | 8.578 us | 19.595 us | 0.63x |
| `lokad/style/range-ref` | 0.914 us | 1.475 us | 1.546 us | 0.62x |
| `structural/ordered-modifier-pair` | 5.384 us | 8.716 us | 19.645 us | 0.62x |
| `literal/identifier-token` | 2.783 us | 4.729 us | 15.295 us | 0.59x |
| `lokad/lexer/string` | 21.805 us | 46.150 us | 49.087 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.989 us | 10.580 us | 21.893 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.547 us | 7.381 us | 7.948 us | 0.35x |
| `lokad/langserv/url-download` | 2.554 us | 12.931 us | 13.556 us | 0.20x |
| `lokad/folding/region-marker` | 6.205 us | 35.036 us | 61.008 us | 0.18x |
| `balancing/begin-end-blocks` | 5.658 us | 467.010 us | 495.753 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
