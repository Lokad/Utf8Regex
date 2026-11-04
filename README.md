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
and resource checks passed in **43m51s**. The [run receipt](bench/Experiments/DeliveryDev24-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 94.853 us | 19.079 us | 2,184.809 us | 4.97x |
| `literal-alternate/sherlock-zh` | 152.843 us | 69.516 us | 2,225.696 us | 2.20x |
| `literal/sherlock-ru` | 202.529 us | 100.772 us | 5,213.050 us | 2.01x |
| `ruff-noqa/tweaked` | 10,373.080 us | 7,984.357 us | 41,951.350 us | 1.30x |
| `literal/sherlock-casei-en` | 159.463 us | 205.051 us | 577.821 us | 0.78x |
| `literal/sherlock-casei-ru` | 257.701 us | 394.637 us | 5,636.156 us | 0.65x |
| `literal/sherlock-en` | 57.765 us | 88.770 us | 461.704 us | 0.65x |
| `aws-keys/quick` | 5,091.400 us | 9,959.900 us | 43,599.500 us | 0.51x |
| `bounded-repeat/letters-en` | 1,038.035 us | 2,844.139 us | 2,952.388 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,687.133 us | 4,787.673 us | 9,862.033 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,935.919 us | 6,038.778 us | 6,848.188 us | 0.32x |
| `dictionary/single` | 8,224.886 us | 25,955.600 us | 25,979.150 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 188.230 us | 602.498 us | 1,062.424 us | 0.31x |
| `literal-alternate/sherlock-en` | 400.805 us | 1,535.779 us | 2,083.062 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 162.618 us | 659.827 us | 1,112.335 us | 0.25x |
| `bounded-repeat/capitals` | 9,471.117 us | 46,875.900 us | 54,854.800 us | 0.20x |
| `bounded-repeat/context` | 16,068.825 us | 149,587.700 us | 153,156.900 us | 0.11x |
| `aws-keys/full` | 8,877.500 us | 86,066.200 us | 119,459.800 us | 0.10x |
| `ruff-noqa/real` | 4,458.750 us | 70,100.700 us | 86,126.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,054.598 us | 23,378.867 us | 28,608.800 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 179.187 us | 11,486.900 us | 12,214.000 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,232.527 us | 492.169 us | 9,015.333 us | 2.50x |
| `common/match-word` | `Match` | 0.200 us | 0.166 us | 1.187 us | 1.20x |
| `industry/leipzig-twain-count` | `Count` | 1,167.177 us | 1,360.077 us | 8,838.100 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,476.859 us | 1,947.948 us | 10,510.033 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.110 us | 0.147 us | 0.73x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 187.752 us | 264.252 us | 499.333 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.61x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.208 us | 0.229 us | 0.56x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.172 us | 0.191 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.180 us | 0.200 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 3,991.708 us | 9,971.933 us | 17,388.200 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.187 us | 0.207 us | 0.39x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.235 us | 0.271 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.264 us | 0.292 us | 0.37x |
| `common/replace-words` | `Replace` | 28.246 us | 76.853 us | 79.174 us | 0.37x |
| `common/matches-word` | `Count` | 1.358 us | 4.255 us | 6.010 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.347 us | 0.374 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,369.059 us | 8,309.786 us | 16,921.700 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.098 us | 0.20x |
| `common/split-words` | `Split` | 14.654 us | 79.468 us | 80.958 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.082 us | 980.031 us | 1,275.581 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.564 us | 0.598 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.090 us | 0.613 us | 0.649 us | 0.15x |
| `common/matches-words` | `Count` | 9.476 us | 70.209 us | 72.544 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.834 us | 0.878 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,373.313 us | 32,446.350 us | 46,230.900 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,996.188 us | 19,488.667 us | 20,054.900 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,871.881 us | 23,254.233 us | 30,889.850 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 505.774 us | 8,716.533 us | 9,214.733 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.824 us | 166.050 us | 167.777 us | 0.05x |
| `common/matches-set` | `Count` | 7.016 us | 195.446 us | 196.811 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 269.670 us | 11,770.420 us | 12,358.500 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 152.845 us | 35.985 us | 2,161.796 us | 4.25x |
| `literal/sherlock-zh` | 94.973 us | 24.149 us | 2,172.139 us | 3.93x |
| `ruff-noqa/tweaked` | 10,634.760 us | 4,597.291 us | 37,896.450 us | 2.31x |
| `literal/sherlock-ru` | 202.967 us | 89.423 us | 5,257.820 us | 2.27x |
| `literal/sherlock-casei-en` | 159.598 us | 79.140 us | 425.339 us | 2.02x |
| `aws-keys/quick` | 5,084.380 us | 3,701.007 us | 35,983.550 us | 1.37x |
| `literal-alternate/sherlock-casei-en` | 178.724 us | 148.211 us | 563.928 us | 1.21x |
| `literal/sherlock-casei-ru` | 258.290 us | 224.921 us | 5,334.880 us | 1.15x |
| `literal-alternate/sherlock-en` | 399.161 us | 375.212 us | 769.886 us | 1.06x |
| `literal-alternate/sherlock-en-mixed` | 187.700 us | 176.815 us | 553.895 us | 1.06x |
| `bounded-repeat/letters-ru` | 1,940.831 us | 2,076.580 us | 2,901.561 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 161.685 us | 206.650 us | 579.312 us | 0.78x |
| `literal/sherlock-en` | 57.815 us | 77.945 us | 447.856 us | 0.74x |
| `literal-alternate/sherlock-ru` | 1,642.797 us | 2,363.555 us | 7,487.900 us | 0.70x |
| `bounded-repeat/capitals` | 9,470.567 us | 13,740.450 us | 21,754.000 us | 0.69x |
| `bounded-repeat/letters-en` | 1,036.767 us | 1,851.325 us | 1,938.477 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,050.990 us | 1,984.177 us | 7,167.167 us | 0.53x |
| `aws-keys/full` | 9,837.000 us | 19,084.733 us | 53,638.450 us | 0.52x |
| `bounded-repeat/context` | 16,146.100 us | 69,688.200 us | 72,394.800 us | 0.23x |
| `ruff-noqa/real` | 4,436.350 us | 21,180.667 us | 34,338.000 us | 0.21x |
| `dictionary/single` | 8,228.386 us | 46,191.950 us | 46,191.550 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/rust-sherlock-holmes-window-count` | `Count` | 188.104 us | 58.912 us | 263.283 us | 3.19x |
| `industry/mariomka-email-count` | `Count` | 1,236.622 us | 455.110 us | 7,809.000 us | 2.72x |
| `common/match-word` | `Match` | 0.200 us | 0.081 us | 1.004 us | 2.48x |
| `industry/leipzig-river-window-count` | `Count` | 3,371.673 us | 1,540.832 us | 13,900.700 us | 2.19x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.046 us | 0.075 us | 2.04x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.065 us | 0.086 us | 1.83x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.029 us | 0.046 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.050 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.087 us | 1.64x |
| `common/replace-words` | `Replace` | 28.220 us | 17.548 us | 20.425 us | 1.61x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.048 us | 0.069 us | 1.51x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.049 us | 0.070 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,866.181 us | 1,281.725 us | 6,970.967 us | 1.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.056 us | 0.077 us | 1.40x |
| `industry/mariomka-uri-count` | `Count` | 1,478.497 us | 1,107.348 us | 8,511.600 us | 1.34x |
| `industry/leipzig-name-family-count` | `Count` | 4,100.475 us | 3,074.847 us | 8,548.533 us | 1.33x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.081 us | 0.109 us | 1.20x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.089 us | 0.116 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.120 us | 1.03x |
| `common/one-node-backtracking` | `IsMatch` | 0.090 us | 0.089 us | 0.129 us | 1.01x |
| `industry/leipzig-twain-count` | `Count` | 1,167.447 us | 1,305.731 us | 5,626.533 us | 0.89x |
| `common/matches-words` | `Count` | 9.464 us | 10.978 us | 14.068 us | 0.86x |
| `common/split-words` | `Split` | 14.669 us | 21.154 us | 22.930 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,375.432 us | 3,544.847 us | 11,149.450 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `common/matches-word` | `Count` | 1.358 us | 2.371 us | 3.718 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.205 us | 0.237 us | 0.48x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 180.380 us | 606.352 us | 886.813 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.054 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,998.658 us | 8,754.167 us | 9,291.617 us | 0.23x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 505.405 us | 3,983.292 us | 4,406.125 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.842 us | 105.576 us | 107.526 us | 0.07x |
| `common/matches-set` | `Count` | 7.172 us | 106.383 us | 107.893 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.799 us | 4,831.027 us | 5,264.380 us | 0.05x |
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
| `lokad/imports/module-imports` | 15.079 us | 6.455 us | 29.494 us | 2.34x |
| `lokad/langserv/color-short-hex` | 1.687 us | 1.388 us | 1.595 us | 1.21x |
| `structural/keyword-to-generic-type` | 5.050 us | 4.165 us | 16.099 us | 1.21x |
| `lokad/lexer/doc-line` | 3.354 us | 3.036 us | 3.924 us | 1.10x |
| `lokad/lexer/identifier` | 23.956 us | 23.247 us | 26.919 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.358 us | 5.355 us | 17.030 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.270 us | 7.353 us | 8.231 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.378 us | 12.635 us | 14.586 us | 0.90x |
| `fallback/lookahead` | 5.424 us | 6.076 us | 17.408 us | 0.89x |
| `lokad/style/hex-color` | 0.728 us | 0.821 us | 0.895 us | 0.89x |
| `lokad/lexer/operator-run` | 2.935 us | 3.428 us | 4.014 us | 0.86x |
| `literal-family/method-token-family` | 7.226 us | 9.215 us | 19.895 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 2.053 us | 2.994 us | 3.242 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.376 us | 4.988 us | 5.758 us | 0.68x |
| `literal/identifier-token` | 2.797 us | 4.420 us | 15.701 us | 0.63x |
| `literal/call-token` | 2.612 us | 4.327 us | 15.581 us | 0.60x |
| `lokad/lexer/number` | 8.276 us | 14.219 us | 15.374 us | 0.58x |
| `structural/method-family-generic-call` | 3.465 us | 6.659 us | 18.208 us | 0.52x |
| `lokad/style/cell-ref` | 1.495 us | 2.954 us | 3.119 us | 0.51x |
| `structural/method-family-call` | 3.878 us | 8.110 us | 19.224 us | 0.48x |
| `literal-family/type-token-family` | 10.614 us | 23.986 us | 35.073 us | 0.44x |
| `literal/identifier-token-casei` | 6.129 us | 15.221 us | 26.436 us | 0.40x |
| `lokad/style/range-ref` | 0.924 us | 2.808 us | 2.931 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.705 us | 17.467 us | 28.497 us | 0.33x |
| `structural/ordered-keyword-window` | 5.469 us | 17.756 us | 29.181 us | 0.31x |
| `structural/modifier-family-to-type-window` | 77.997 us | 266.454 us | 273.088 us | 0.29x |
| `lokad/lexer/string` | 24.633 us | 93.774 us | 100.450 us | 0.26x |
| `structural/modifier-family-to-type-pair` | 23.465 us | 132.303 us | 143.855 us | 0.18x |
| `lokad/folding/region-marker` | 6.578 us | 37.223 us | 60.781 us | 0.18x |
| `structural/ordered-identifier-window` | 5.014 us | 28.994 us | 40.046 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.580 us | 25.832 us | 26.359 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.460 us | 222.690 us | 235.729 us | 0.08x |
| `lokad/langserv/url-download` | 2.660 us | 52.262 us | 52.797 us | 0.05x |
| `balancing/begin-end-blocks` | 5.734 us | 4,422.117 us | 4,438.467 us | 0.0013x |
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
| `lokad/imports/module-imports` | 15.186 us | 4.485 us | 26.474 us | 3.39x |
| `structural/keyword-family-to-capitalized-identifier` | 18.609 us | 7.691 us | 18.741 us | 2.42x |
| `structural/modifier-family-to-type-pair` | 23.462 us | 10.903 us | 21.781 us | 2.15x |
| `lokad/langserv/color-short-hex` | 1.693 us | 1.095 us | 1.294 us | 1.55x |
| `structural/keyword-to-generic-type` | 5.047 us | 3.531 us | 14.940 us | 1.43x |
| `lokad/langserv/helper-identifier` | 7.235 us | 5.077 us | 6.213 us | 1.43x |
| `lokad/lexer/operator-run` | 2.950 us | 2.076 us | 2.658 us | 1.42x |
| `lokad/lexer/doc-line` | 3.354 us | 2.382 us | 3.093 us | 1.41x |
| `structural/modifier-family-to-type-window` | 77.747 us | 57.035 us | 68.873 us | 1.36x |
| `lokad/style/hex-color` | 0.729 us | 0.537 us | 0.634 us | 1.36x |
| `lokad/lexer/identifier` | 23.911 us | 18.313 us | 21.855 us | 1.31x |
| `structural/ordered-keyword-pair` | 5.351 us | 4.171 us | 15.240 us | 1.28x |
| `fallback/lookahead` | 5.436 us | 4.244 us | 15.143 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.388 us | 9.744 us | 11.728 us | 1.17x |
| `lokad/docs/inline-doc-prefix` | 3.366 us | 2.965 us | 3.717 us | 1.14x |
| `lokad/lexer/number` | 8.272 us | 7.517 us | 8.608 us | 1.10x |
| `lokad/langserv/color-rgb-hex` | 2.069 us | 2.018 us | 2.252 us | 1.03x |
| `literal/identifier-token-casei` | 6.076 us | 5.937 us | 17.169 us | 1.02x |
| `literal-family/type-token-family` | 10.608 us | 11.567 us | 22.291 us | 0.92x |
| `lokad/style/cell-ref` | 1.495 us | 1.889 us | 2.084 us | 0.79x |
| `structural/method-family-call` | 3.879 us | 5.091 us | 15.835 us | 0.76x |
| `structural/method-family-generic-call` | 3.455 us | 4.642 us | 15.286 us | 0.74x |
| `literal-family/method-token-family` | 3.902 us | 5.359 us | 15.758 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.700 us | 8.696 us | 19.888 us | 0.66x |
| `literal/call-token` | 2.619 us | 4.012 us | 14.633 us | 0.65x |
| `structural/ordered-keyword-window` | 5.489 us | 8.609 us | 19.688 us | 0.64x |
| `literal/identifier-token` | 2.809 us | 4.747 us | 15.143 us | 0.59x |
| `lokad/style/range-ref` | 0.923 us | 1.668 us | 1.757 us | 0.55x |
| `lokad/lexer/string` | 24.656 us | 46.324 us | 49.733 us | 0.53x |
| `structural/ordered-identifier-window` | 5.012 us | 10.743 us | 21.537 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.537 us | 7.525 us | 8.141 us | 0.34x |
| `lokad/langserv/url-download` | 2.658 us | 13.107 us | 13.545 us | 0.20x |
| `lokad/folding/region-marker` | 6.595 us | 37.124 us | 60.630 us | 0.18x |
| `balancing/begin-end-blocks` | 5.737 us | 471.592 us | 497.985 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
