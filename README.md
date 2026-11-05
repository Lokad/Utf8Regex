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
and resource checks passed in **43m07s**. The [run receipt](bench/Experiments/DeliveryDev32-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 91.184 us | 19.084 us | 2,225.839 us | 4.78x |
| `literal-alternate/sherlock-zh` | 150.664 us | 69.523 us | 2,231.329 us | 2.17x |
| `literal/sherlock-ru` | 170.919 us | 102.096 us | 5,524.933 us | 1.67x |
| `literal/sherlock-casei-en` | 159.664 us | 194.893 us | 650.496 us | 0.82x |
| `literal/sherlock-en` | 57.580 us | 86.430 us | 478.556 us | 0.67x |
| `literal/sherlock-casei-ru` | 248.224 us | 393.259 us | 5,640.989 us | 0.63x |
| `ruff-noqa/tweaked` | 4,426.450 us | 7,965.757 us | 41,817.200 us | 0.56x |
| `aws-keys/quick` | 5,087.180 us | 10,159.960 us | 43,971.750 us | 0.50x |
| `bounded-repeat/letters-en` | 1,060.344 us | 2,850.033 us | 2,941.818 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,675.761 us | 4,878.664 us | 10,130.467 us | 0.34x |
| `bounded-repeat/letters-ru` | 2,009.704 us | 6,020.911 us | 6,803.200 us | 0.33x |
| `dictionary/single` | 8,296.371 us | 26,127.300 us | 26,170.100 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 194.375 us | 625.388 us | 1,042.938 us | 0.31x |
| `literal-alternate/sherlock-en` | 394.982 us | 1,514.274 us | 2,031.111 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 155.231 us | 667.630 us | 1,108.180 us | 0.23x |
| `bounded-repeat/capitals` | 9,485.750 us | 46,598.400 us | 54,749.850 us | 0.20x |
| `bounded-repeat/context` | 16,356.933 us | 149,925.000 us | 153,213.400 us | 0.11x |
| `aws-keys/full` | 8,864.200 us | 87,185.400 us | 119,070.900 us | 0.10x |
| `ruff-noqa/real` | 4,413.933 us | 71,186.500 us | 87,117.000 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 861.302 us | 23,363.800 us | 35,694.150 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 173.461 us | 11,447.940 us | 12,255.200 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,222.785 us | 491.319 us | 9,020.633 us | 2.49x |
| `common/match-word` | `Match` | 0.200 us | 0.179 us | 1.214 us | 1.12x |
| `industry/leipzig-twain-count` | `Count` | 1,158.984 us | 1,349.388 us | 8,497.767 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,440.020 us | 1,947.638 us | 10,546.000 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.112 us | 0.150 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.197 us | 0.216 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.086 us | 0.103 us | 0.59x |
| `common/ip-match` | `IsMatch` | 0.096 us | 0.182 us | 0.202 us | 0.53x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 137.326 us | 265.747 us | 509.483 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.170 us | 0.187 us | 0.46x |
| `common/ip-miss` | `IsMatch` | 0.078 us | 0.190 us | 0.208 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 4,063.731 us | 9,875.433 us | 16,952.250 us | 0.41x |
| `common/email-match` | `IsMatch` | 0.106 us | 0.270 us | 0.296 us | 0.39x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.233 us | 0.268 us | 0.39x |
| `common/replace-words` | `Replace` | 28.371 us | 77.909 us | 79.620 us | 0.36x |
| `common/matches-word` | `Count` | 1.355 us | 4.268 us | 6.025 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.348 us | 0.369 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,354.491 us | 8,393.583 us | 16,920.950 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.140 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.21x |
| `common/split-words` | `Split` | 14.678 us | 79.528 us | 80.574 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.188 us | 967.956 us | 1,250.388 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.564 us | 0.596 us | 0.15x |
| `common/matches-words` | `Count` | 9.398 us | 70.716 us | 72.316 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.625 us | 0.653 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.096 us | 0.858 us | 0.901 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,076.180 us | 19,554.200 us | 20,315.900 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,319.669 us | 34,642.650 us | 46,999.400 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,820.421 us | 23,091.200 us | 30,438.850 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 505.882 us | 8,321.329 us | 8,843.133 us | 0.06x |
| `common/matches-boundary` | `Count` | 8.216 us | 166.225 us | 167.834 us | 0.05x |
| `common/matches-set` | `Count` | 7.034 us | 196.493 us | 198.157 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 266.650 us | 11,889.100 us | 12,376.560 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 150.928 us | 36.242 us | 2,165.471 us | 4.16x |
| `literal/sherlock-zh` | 91.149 us | 24.186 us | 2,183.161 us | 3.77x |
| `literal/sherlock-casei-en` | 159.883 us | 79.986 us | 452.872 us | 2.00x |
| `literal/sherlock-ru` | 170.928 us | 90.446 us | 5,478.089 us | 1.89x |
| `aws-keys/quick` | 5,085.650 us | 3,692.157 us | 36,251.000 us | 1.38x |
| `literal-alternate/sherlock-en` | 396.212 us | 349.892 us | 771.883 us | 1.13x |
| `literal/sherlock-casei-ru` | 249.600 us | 223.639 us | 5,372.680 us | 1.12x |
| `literal-alternate/sherlock-en-mixed` | 188.790 us | 177.233 us | 546.814 us | 1.07x |
| `bounded-repeat/letters-ru` | 2,011.944 us | 2,078.636 us | 2,870.867 us | 0.97x |
| `ruff-noqa/tweaked` | 4,427.892 us | 4,591.518 us | 37,327.950 us | 0.96x |
| `literal-alternate/sherlock-en-nomatch` | 155.037 us | 201.362 us | 595.655 us | 0.77x |
| `literal/sherlock-en` | 57.536 us | 76.448 us | 453.658 us | 0.75x |
| `literal-alternate/sherlock-ru` | 1,690.173 us | 2,366.705 us | 7,552.400 us | 0.71x |
| `bounded-repeat/capitals` | 9,481.250 us | 13,710.550 us | 21,363.267 us | 0.69x |
| `bounded-repeat/letters-en` | 1,058.250 us | 1,853.775 us | 1,933.650 us | 0.57x |
| `literal-alternate/sherlock-casei-en` | 173.299 us | 313.455 us | 714.199 us | 0.55x |
| `aws-keys/full` | 9,611.540 us | 19,003.667 us | 51,604.550 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 860.612 us | 1,980.419 us | 9,648.867 us | 0.43x |
| `bounded-repeat/context` | 16,636.667 us | 69,719.100 us | 72,607.200 us | 0.24x |
| `ruff-noqa/real` | 4,417.658 us | 21,676.967 us | 34,553.350 us | 0.20x |
| `dictionary/single` | 8,292.300 us | 46,243.900 us | 46,255.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,226.395 us | 455.794 us | 7,859.800 us | 2.69x |
| `common/match-word` | `Match` | 0.200 us | 0.080 us | 1.001 us | 2.50x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 137.181 us | 59.287 us | 275.635 us | 2.31x |
| `industry/leipzig-river-window-count` | `Count` | 3,317.363 us | 1,540.797 us | 14,348.933 us | 2.15x |
| `common/backtracking` | `IsMatch` | 0.096 us | 0.046 us | 0.075 us | 2.09x |
| `common/ip-match` | `IsMatch` | 0.096 us | 0.048 us | 0.069 us | 1.99x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.067 us | 0.087 us | 1.77x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.030 us | 0.049 us | 1.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.049 us | 0.088 us | 1.68x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.047 us | 1.63x |
| `common/replace-words` | `Replace` | 28.284 us | 17.794 us | 21.020 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.078 us | 0.052 us | 0.072 us | 1.51x |
| `industry/leipzig-symbol-count` | `Count` | 1,822.961 us | 1,266.167 us | 6,917.167 us | 1.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.078 us | 1.36x |
| `industry/leipzig-name-family-count` | `Count` | 4,062.900 us | 3,005.582 us | 8,292.867 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,440.957 us | 1,102.248 us | 8,581.933 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.106 us | 0.081 us | 0.107 us | 1.30x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.092 us | 0.118 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.119 us | 1.02x |
| `industry/leipzig-twain-count` | `Count` | 1,159.823 us | 1,280.930 us | 5,227.567 us | 0.91x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.089 us | 0.128 us | 0.89x |
| `common/matches-words` | `Count` | 9.381 us | 11.012 us | 14.095 us | 0.85x |
| `common/split-words` | `Split` | 14.688 us | 21.518 us | 23.228 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,352.459 us | 3,527.093 us | 11,091.350 us | 0.67x |
| `common/matches-word` | `Count` | 1.355 us | 2.385 us | 3.679 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.204 us | 0.238 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.036 us | 0.053 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.854 us | 595.029 us | 876.324 us | 0.30x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 513.013 us | 3,992.823 us | 4,369.650 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,077.372 us | 17,659.300 us | 18,585.800 us | 0.12x |
| `common/matches-boundary` | `Count` | 8.216 us | 106.664 us | 108.437 us | 0.08x |
| `common/matches-set` | `Count` | 7.044 us | 106.632 us | 108.206 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.472 us | 4,821.491 us | 5,236.680 us | 0.05x |
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
| `lokad/imports/module-imports` | 12.410 us | 6.411 us | 28.967 us | 1.94x |
| `structural/keyword-to-generic-type` | 5.499 us | 4.145 us | 16.035 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.690 us | 1.374 us | 1.477 us | 1.23x |
| `lokad/lexer/doc-line` | 3.351 us | 3.041 us | 3.701 us | 1.10x |
| `lokad/lexer/identifier` | 23.558 us | 22.968 us | 26.192 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.599 us | 5.480 us | 16.931 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.249 us | 7.261 us | 7.929 us | 1.00x |
| `fallback/lookahead` | 5.636 us | 6.167 us | 17.150 us | 0.91x |
| `lokad/langserv/identifier-validator` | 11.366 us | 12.524 us | 13.967 us | 0.91x |
| `lokad/lexer/operator-run` | 2.924 us | 3.464 us | 3.901 us | 0.84x |
| `literal-family/method-token-family` | 7.597 us | 9.223 us | 19.947 us | 0.82x |
| `lokad/style/hex-color` | 0.602 us | 0.819 us | 0.883 us | 0.74x |
| `lokad/langserv/color-rgb-hex` | 2.067 us | 3.007 us | 3.079 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.384 us | 5.003 us | 5.734 us | 0.68x |
| `literal/identifier-token` | 2.799 us | 4.388 us | 15.623 us | 0.64x |
| `lokad/lexer/number` | 8.344 us | 14.051 us | 15.211 us | 0.59x |
| `literal/call-token` | 2.601 us | 4.385 us | 15.618 us | 0.59x |
| `structural/ordered-identifier-window` | 16.201 us | 28.899 us | 39.971 us | 0.56x |
| `lokad/style/cell-ref` | 1.510 us | 2.914 us | 3.091 us | 0.52x |
| `structural/method-family-generic-call` | 3.472 us | 6.818 us | 18.050 us | 0.51x |
| `structural/method-family-call` | 3.878 us | 8.107 us | 19.067 us | 0.48x |
| `literal-family/type-token-family` | 10.595 us | 24.349 us | 36.234 us | 0.44x |
| `literal/identifier-token-casei` | 6.202 us | 15.297 us | 27.103 us | 0.41x |
| `lokad/style/range-ref` | 0.923 us | 2.767 us | 2.826 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.577 us | 17.851 us | 28.694 us | 0.31x |
| `structural/modifier-family-to-type-window` | 80.269 us | 258.204 us | 270.380 us | 0.31x |
| `structural/ordered-keyword-window` | 5.496 us | 18.081 us | 29.116 us | 0.30x |
| `lokad/lexer/string` | 22.201 us | 94.380 us | 100.272 us | 0.24x |
| `lokad/folding/region-marker` | 6.458 us | 37.273 us | 60.737 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.568 us | 25.529 us | 26.034 us | 0.10x |
| `lokad/langserv/url-download` | 2.706 us | 53.051 us | 53.503 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 10.726 us | 217.672 us | 228.933 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.078 us | 134.102 us | 145.161 us | 0.04x |
| `balancing/begin-end-blocks` | 5.790 us | 4,337.033 us | 4,371.567 us | 0.0013x |
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
| `lokad/imports/module-imports` | 12.370 us | 4.480 us | 26.085 us | 2.76x |
| `lokad/langserv/color-short-hex` | 1.698 us | 1.012 us | 1.182 us | 1.68x |
| `structural/keyword-to-generic-type` | 5.507 us | 3.552 us | 14.928 us | 1.55x |
| `structural/ordered-identifier-window` | 16.202 us | 10.739 us | 21.769 us | 1.51x |
| `lokad/langserv/helper-identifier` | 7.246 us | 4.846 us | 5.736 us | 1.50x |
| `lokad/lexer/doc-line` | 3.348 us | 2.256 us | 2.990 us | 1.48x |
| `structural/modifier-family-to-type-window` | 79.960 us | 56.939 us | 69.274 us | 1.40x |
| `lokad/lexer/operator-run` | 2.927 us | 2.113 us | 2.537 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.603 us | 4.167 us | 15.052 us | 1.34x |
| `structural/keyword-family-to-capitalized-identifier` | 10.728 us | 7.984 us | 18.597 us | 1.34x |
| `lokad/lexer/identifier` | 23.537 us | 17.670 us | 20.704 us | 1.33x |
| `fallback/lookahead` | 5.644 us | 4.254 us | 14.927 us | 1.33x |
| `lokad/langserv/identifier-validator` | 11.373 us | 9.176 us | 10.947 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.384 us | 2.862 us | 3.606 us | 1.18x |
| `lokad/lexer/number` | 8.338 us | 7.266 us | 8.603 us | 1.15x |
| `lokad/style/hex-color` | 0.602 us | 0.540 us | 0.624 us | 1.11x |
| `literal/identifier-token-casei` | 6.224 us | 5.936 us | 17.713 us | 1.05x |
| `lokad/langserv/color-rgb-hex` | 2.057 us | 1.997 us | 2.204 us | 1.03x |
| `literal-family/type-token-family` | 10.566 us | 11.573 us | 22.817 us | 0.91x |
| `lokad/style/cell-ref` | 1.512 us | 1.799 us | 1.963 us | 0.84x |
| `structural/method-family-generic-call` | 3.468 us | 4.530 us | 15.053 us | 0.77x |
| `structural/method-family-call` | 3.874 us | 5.105 us | 15.758 us | 0.76x |
| `literal-family/method-token-family` | 3.913 us | 5.373 us | 15.792 us | 0.73x |
| `literal/call-token` | 2.608 us | 3.990 us | 14.646 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.595 us | 8.690 us | 19.665 us | 0.64x |
| `structural/ordered-keyword-window` | 5.511 us | 8.665 us | 19.507 us | 0.64x |
| `literal/identifier-token` | 2.807 us | 4.736 us | 15.374 us | 0.59x |
| `lokad/style/range-ref` | 0.923 us | 1.596 us | 1.641 us | 0.58x |
| `lokad/lexer/string` | 22.241 us | 46.263 us | 49.183 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 5.085 us | 10.812 us | 21.746 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.530 us | 7.536 us | 8.077 us | 0.34x |
| `lokad/langserv/url-download` | 2.708 us | 13.088 us | 13.593 us | 0.21x |
| `lokad/folding/region-marker` | 6.480 us | 36.921 us | 61.356 us | 0.18x |
| `balancing/begin-end-blocks` | 5.790 us | 471.686 us | 497.917 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
