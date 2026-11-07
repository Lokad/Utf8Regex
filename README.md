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
and resource checks passed in **46m19s**. The [run receipt](bench/Experiments/DeliveryDev50-20261107/full-refresh.json)
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
| `literal/sherlock-zh` | 84.150 us | 18.606 us | 2,173.260 us | 4.52x |
| `literal/sherlock-ru` | 169.866 us | 98.131 us | 5,360.464 us | 1.73x |
| `literal-alternate/sherlock-zh` | 103.050 us | 67.611 us | 2,214.712 us | 1.52x |
| `literal/sherlock-casei-en` | 160.055 us | 188.494 us | 601.938 us | 0.85x |
| `literal/sherlock-en` | 56.914 us | 85.653 us | 465.035 us | 0.66x |
| `literal/sherlock-casei-ru` | 245.220 us | 389.867 us | 5,382.607 us | 0.63x |
| `ruff-noqa/tweaked` | 4,342.222 us | 7,566.482 us | 41,105.550 us | 0.57x |
| `aws-keys/quick` | 4,760.688 us | 9,687.763 us | 44,126.750 us | 0.49x |
| `bounded-repeat/letters-en` | 1,097.987 us | 2,805.307 us | 2,908.165 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,643.102 us | 4,775.531 us | 9,843.500 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 197.290 us | 588.767 us | 1,001.676 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,965.926 us | 5,900.308 us | 6,719.500 us | 0.33x |
| `dictionary/single` | 8,156.880 us | 26,100.167 us | 26,118.500 us | 0.31x |
| `literal-alternate/sherlock-en` | 385.105 us | 1,510.517 us | 1,971.844 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 152.358 us | 662.068 us | 1,084.579 us | 0.23x |
| `bounded-repeat/capitals` | 9,446.225 us | 46,045.100 us | 50,028.500 us | 0.21x |
| `bounded-repeat/context` | 20,919.275 us | 145,968.200 us | 148,904.600 us | 0.14x |
| `aws-keys/full` | 10,518.138 us | 84,710.500 us | 116,443.700 us | 0.12x |
| `ruff-noqa/real` | 4,325.261 us | 68,784.300 us | 101,409.300 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 939.974 us | 23,384.150 us | 28,864.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 130.784 us | 11,113.386 us | 12,086.457 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,168.542 us | 482.473 us | 8,850.275 us | 2.42x |
| `common/match-word` | `Match` | 0.197 us | 0.167 us | 1.141 us | 1.18x |
| `industry/leipzig-twain-count` | `Count` | 1,145.377 us | 1,289.460 us | 8,432.389 us | 0.89x |
| `industry/mariomka-uri-count` | `Count` | 1,436.343 us | 1,844.081 us | 10,384.600 us | 0.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.111 us | 0.147 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.194 us | 0.211 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.099 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 128.428 us | 254.833 us | 471.106 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.164 us | 0.180 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.079 us | 0.170 us | 0.190 us | 0.46x |
| `industry/leipzig-name-family-count` | `Count` | 3,928.700 us | 9,656.712 us | 16,818.300 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.187 us | 0.209 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.233 us | 0.268 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.264 us | 0.290 us | 0.36x |
| `common/replace-words` | `Replace` | 27.212 us | 79.311 us | 82.647 us | 0.34x |
| `common/matches-word` | `Count` | 1.345 us | 4.165 us | 5.851 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.344 us | 0.366 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,287.033 us | 8,203.070 us | 16,637.967 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.106 us | 0.137 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.097 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.566 us | 974.488 us | 1,246.310 us | 0.18x |
| `common/split-words` | `Split` | 14.172 us | 79.242 us | 80.761 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.558 us | 0.588 us | 0.17x |
| `common/matches-words` | `Count` | 9.263 us | 70.280 us | 72.195 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.612 us | 0.651 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.089 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.832 us | 0.877 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,325.957 us | 31,718.167 us | 39,026.300 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,989.131 us | 19,317.650 us | 19,912.250 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,853.439 us | 22,771.450 us | 29,184.300 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 510.718 us | 8,382.790 us | 8,872.667 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.929 us | 161.635 us | 163.789 us | 0.05x |
| `common/matches-set` | `Count` | 7.326 us | 191.069 us | 192.278 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 271.831 us | 11,792.714 us | 12,247.900 us | 0.02x |
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
| `literal/sherlock-zh` | 84.457 us | 23.699 us | 2,166.880 us | 3.56x |
| `literal-alternate/sherlock-zh` | 104.071 us | 36.070 us | 2,156.306 us | 2.89x |
| `literal/sherlock-casei-en` | 159.180 us | 76.621 us | 434.643 us | 2.08x |
| `literal/sherlock-ru` | 170.258 us | 90.683 us | 5,260.864 us | 1.88x |
| `aws-keys/quick` | 4,764.931 us | 3,600.477 us | 35,240.350 us | 1.32x |
| `literal-alternate/sherlock-en-mixed` | 196.125 us | 174.398 us | 539.083 us | 1.12x |
| `literal-alternate/sherlock-en` | 382.293 us | 340.468 us | 735.079 us | 1.12x |
| `literal/sherlock-casei-ru` | 247.114 us | 220.187 us | 5,240.880 us | 1.12x |
| `ruff-noqa/tweaked` | 4,392.750 us | 4,554.482 us | 35,291.900 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,956.172 us | 2,036.932 us | 2,886.662 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 129.834 us | 149.391 us | 508.611 us | 0.87x |
| `literal-alternate/sherlock-en-nomatch` | 151.863 us | 200.989 us | 600.616 us | 0.76x |
| `literal/sherlock-en` | 56.597 us | 75.704 us | 405.918 us | 0.75x |
| `bounded-repeat/capitals` | 9,382.513 us | 13,412.133 us | 16,405.125 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,607.665 us | 2,325.503 us | 7,366.050 us | 0.69x |
| `bounded-repeat/letters-en` | 1,093.259 us | 1,821.481 us | 1,898.305 us | 0.60x |
| `aws-keys/full` | 10,631.913 us | 18,075.500 us | 51,183.567 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 939.130 us | 1,951.023 us | 7,252.600 us | 0.48x |
| `bounded-repeat/context` | 20,108.600 us | 68,617.250 us | 72,077.500 us | 0.29x |
| `ruff-noqa/real` | 4,318.544 us | 20,472.850 us | 54,072.400 us | 0.21x |
| `dictionary/single` | 8,128.850 us | 45,853.250 us | 45,668.650 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,176.295 us | 449.962 us | 7,569.450 us | 2.61x |
| `common/match-word` | `Match` | 0.198 us | 0.078 us | 1.002 us | 2.55x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 128.861 us | 57.459 us | 247.570 us | 2.24x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.043 us | 0.073 us | 2.17x |
| `industry/leipzig-river-window-count` | `Count` | 3,323.987 us | 1,542.914 us | 8,139.160 us | 2.15x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.067 us | 0.084 us | 1.75x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.047 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.048 us | 0.086 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.047 us | 0.068 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.048 us | 0.069 us | 1.54x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.033 us | 0.050 us | 1.54x |
| `industry/leipzig-symbol-count` | `Count` | 1,862.890 us | 1,251.932 us | 7,357.160 us | 1.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.054 us | 0.076 us | 1.39x |
| `industry/leipzig-name-family-count` | `Count` | 4,108.268 us | 3,056.080 us | 8,007.350 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,444.696 us | 1,083.906 us | 8,340.950 us | 1.33x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.082 us | 0.107 us | 1.14x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.089 us | 0.115 us | 1.11x |
| `common/replace-words` | `Replace` | 27.113 us | 24.606 us | 25.402 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.087 us | 0.119 us | 1.06x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.087 us | 0.128 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 1,151.829 us | 1,269.602 us | 7,284.850 us | 0.91x |
| `common/matches-words` | `Count` | 9.211 us | 10.900 us | 14.066 us | 0.85x |
| `industry/mariomka-ip-count` | `Count` | 2,280.706 us | 3,401.452 us | 10,893.475 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.65x |
| `common/matches-word` | `Count` | 1.348 us | 2.312 us | 3.405 us | 0.58x |
| `common/split-words` | `Split` | 14.328 us | 25.827 us | 27.653 us | 0.55x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.208 us | 0.238 us | 0.47x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.765 us | 589.436 us | 858.116 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.052 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,992.692 us | 8,674.044 us | 9,297.513 us | 0.23x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 504.375 us | 3,932.005 us | 4,317.044 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.938 us | 104.120 us | 105.661 us | 0.08x |
| `common/matches-set` | `Count` | 7.277 us | 103.660 us | 105.152 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 268.162 us | 4,757.113 us | 5,195.020 us | 0.06x |
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
| `lokad/imports/module-imports` | 10.313 us | 6.385 us | 29.033 us | 1.62x |
| `structural/keyword-to-generic-type` | 5.385 us | 4.031 us | 15.576 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.642 us | 1.347 us | 1.446 us | 1.22x |
| `lokad/lexer/doc-line` | 3.345 us | 2.966 us | 3.487 us | 1.13x |
| `lokad/lexer/identifier` | 23.577 us | 22.971 us | 25.048 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.278 us | 5.207 us | 16.415 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.073 us | 7.247 us | 7.717 us | 0.98x |
| `fallback/lookahead` | 5.620 us | 6.014 us | 17.248 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.257 us | 12.363 us | 13.810 us | 0.91x |
| `lokad/style/hex-color` | 0.734 us | 0.811 us | 0.864 us | 0.91x |
| `lokad/lexer/operator-run` | 2.921 us | 3.401 us | 3.799 us | 0.86x |
| `literal-family/method-token-family` | 7.045 us | 9.094 us | 19.908 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.012 us | 2.932 us | 2.959 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.331 us | 4.924 us | 5.483 us | 0.68x |
| `literal/identifier-token` | 2.765 us | 4.311 us | 15.362 us | 0.64x |
| `literal/call-token` | 2.560 us | 4.226 us | 15.457 us | 0.61x |
| `lokad/lexer/number` | 8.192 us | 14.185 us | 15.219 us | 0.58x |
| `structural/ordered-identifier-window` | 15.966 us | 28.358 us | 39.181 us | 0.56x |
| `structural/method-family-generic-call` | 3.431 us | 6.528 us | 17.717 us | 0.53x |
| `lokad/style/cell-ref` | 1.481 us | 2.903 us | 2.954 us | 0.51x |
| `structural/method-family-call` | 3.814 us | 8.084 us | 18.898 us | 0.47x |
| `literal-family/type-token-family` | 10.356 us | 23.249 us | 34.252 us | 0.45x |
| `literal/identifier-token-casei` | 5.679 us | 14.922 us | 25.921 us | 0.38x |
| `lokad/style/range-ref` | 0.908 us | 2.706 us | 2.662 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.482 us | 17.130 us | 28.197 us | 0.32x |
| `structural/ordered-keyword-window` | 5.449 us | 17.451 us | 28.958 us | 0.31x |
| `structural/modifier-family-to-type-window` | 76.923 us | 252.396 us | 264.914 us | 0.30x |
| `lokad/lexer/string` | 21.496 us | 102.827 us | 98.758 us | 0.21x |
| `lokad/folding/region-marker` | 6.185 us | 36.422 us | 59.995 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.481 us | 24.259 us | 24.640 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.416 us | 213.693 us | 225.642 us | 0.05x |
| `lokad/langserv/url-download` | 2.508 us | 53.272 us | 53.534 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.894 us | 131.010 us | 144.045 us | 0.04x |
| `balancing/begin-end-blocks` | 5.598 us | 4,290.606 us | 4,328.383 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.295 us | 4.400 us | 26.367 us | 2.34x |
| `lokad/langserv/color-short-hex` | 1.677 us | 0.987 us | 1.214 us | 1.70x |
| `lokad/langserv/helper-identifier` | 7.191 us | 4.526 us | 5.559 us | 1.59x |
| `lokad/lexer/doc-line` | 3.321 us | 2.129 us | 2.865 us | 1.56x |
| `structural/keyword-to-generic-type` | 5.403 us | 3.464 us | 14.463 us | 1.56x |
| `structural/ordered-identifier-window` | 15.906 us | 10.413 us | 21.124 us | 1.53x |
| `lokad/lexer/operator-run` | 2.882 us | 1.959 us | 2.521 us | 1.47x |
| `lokad/style/hex-color` | 0.740 us | 0.510 us | 0.590 us | 1.45x |
| `structural/keyword-family-to-capitalized-identifier` | 11.344 us | 7.809 us | 18.299 us | 1.45x |
| `lokad/lexer/identifier` | 23.628 us | 16.313 us | 19.977 us | 1.45x |
| `structural/modifier-family-to-type-window` | 76.651 us | 56.354 us | 68.531 us | 1.36x |
| `fallback/lookahead` | 5.591 us | 4.162 us | 15.023 us | 1.34x |
| `lokad/langserv/identifier-validator` | 11.252 us | 8.616 us | 10.899 us | 1.31x |
| `structural/ordered-keyword-pair` | 5.287 us | 4.083 us | 14.849 us | 1.29x |
| `lokad/docs/inline-doc-prefix` | 3.327 us | 2.749 us | 3.475 us | 1.21x |
| `lokad/lexer/number` | 8.241 us | 7.062 us | 8.176 us | 1.17x |
| `lokad/langserv/color-rgb-hex` | 2.007 us | 1.867 us | 2.037 us | 1.08x |
| `literal/identifier-token-casei` | 5.717 us | 5.784 us | 16.964 us | 0.99x |
| `literal-family/type-token-family` | 10.354 us | 11.337 us | 21.888 us | 0.91x |
| `lokad/style/cell-ref` | 1.475 us | 1.767 us | 1.929 us | 0.83x |
| `structural/method-family-generic-call` | 3.410 us | 4.441 us | 14.898 us | 0.77x |
| `structural/method-family-call` | 3.803 us | 4.998 us | 15.442 us | 0.76x |
| `literal-family/method-token-family` | 3.862 us | 5.260 us | 15.867 us | 0.73x |
| `literal/call-token` | 2.552 us | 3.973 us | 14.555 us | 0.64x |
| `structural/ordered-keyword-window` | 5.494 us | 8.579 us | 19.603 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.450 us | 8.581 us | 19.349 us | 0.64x |
| `lokad/style/range-ref` | 0.895 us | 1.472 us | 1.522 us | 0.61x |
| `literal/identifier-token` | 2.788 us | 4.990 us | 15.297 us | 0.56x |
| `lokad/lexer/string` | 21.574 us | 45.698 us | 48.424 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.919 us | 10.701 us | 21.274 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.499 us | 7.367 us | 7.959 us | 0.34x |
| `lokad/langserv/url-download` | 2.523 us | 12.869 us | 13.431 us | 0.20x |
| `lokad/folding/region-marker` | 6.204 us | 36.412 us | 58.956 us | 0.17x |
| `balancing/begin-end-blocks` | 5.628 us | 462.170 us | 492.118 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
