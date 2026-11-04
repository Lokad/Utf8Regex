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
and resource checks passed in **42m48s**. The [run receipt](bench/Experiments/DeliveryDev22-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 137.743 us | 18.979 us | 2,167.278 us | 7.26x |
| `literal-alternate/sherlock-zh` | 240.371 us | 69.054 us | 2,236.955 us | 3.48x |
| `literal/sherlock-ru` | 248.094 us | 98.678 us | 5,481.360 us | 2.51x |
| `ruff-noqa/tweaked` | 10,664.900 us | 7,943.129 us | 41,707.800 us | 1.34x |
| `literal/sherlock-casei-en` | 171.339 us | 191.820 us | 625.839 us | 0.89x |
| `literal/sherlock-en` | 70.394 us | 86.410 us | 456.734 us | 0.81x |
| `aws-keys/quick` | 6,594.311 us | 9,918.780 us | 43,382.700 us | 0.66x |
| `literal/sherlock-casei-ru` | 249.223 us | 395.749 us | 5,573.589 us | 0.63x |
| `bounded-repeat/letters-en` | 1,037.649 us | 2,850.939 us | 2,944.600 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,738.010 us | 4,848.955 us | 10,195.867 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 207.695 us | 606.159 us | 1,043.516 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,985.231 us | 6,007.556 us | 6,856.650 us | 0.33x |
| `dictionary/single` | 8,191.729 us | 26,795.900 us | 26,777.700 us | 0.31x |
| `literal-alternate/sherlock-en` | 406.885 us | 1,515.374 us | 2,062.716 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 167.808 us | 665.699 us | 1,139.718 us | 0.25x |
| `bounded-repeat/capitals` | 9,543.767 us | 46,834.850 us | 53,108.100 us | 0.20x |
| `aws-keys/full` | 10,708.380 us | 86,871.200 us | 117,945.800 us | 0.12x |
| `bounded-repeat/context` | 16,311.633 us | 148,743.100 us | 152,530.500 us | 0.11x |
| `ruff-noqa/real` | 4,576.264 us | 71,263.200 us | 88,079.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,092.950 us | 23,350.333 us | 28,632.850 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 231.477 us | 11,234.880 us | 11,939.820 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,535.591 us | 492.981 us | 9,026.900 us | 3.11x |
| `common/match-word` | `Match` | 0.198 us | 0.167 us | 1.227 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,264.678 us | 1,354.108 us | 8,817.933 us | 0.93x |
| `industry/mariomka-uri-count` | `Count` | 1,754.424 us | 1,980.404 us | 10,570.833 us | 0.89x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 195.186 us | 265.316 us | 503.899 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.111 us | 0.150 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.210 us | 0.232 us | 0.56x |
| `common/ip-miss` | `IsMatch` | 0.088 us | 0.187 us | 0.205 us | 0.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.170 us | 0.186 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.184 us | 0.205 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,169.692 us | 9,938.450 us | 17,421.000 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.237 us | 0.273 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.101 us | 0.275 us | 0.299 us | 0.37x |
| `common/replace-words` | `Replace` | 28.300 us | 79.294 us | 81.723 us | 0.36x |
| `industry/mariomka-ip-count` | `Count` | 2,706.647 us | 8,393.567 us | 16,955.350 us | 0.32x |
| `common/matches-word` | `Count` | 1.358 us | 4.271 us | 6.005 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.351 us | 0.376 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.030 us | 0.110 us | 0.140 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.099 us | 0.21x |
| `common/split-words` | `Split` | 14.666 us | 77.912 us | 79.070 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.524 us | 964.004 us | 1,247.653 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.561 us | 0.594 us | 0.16x |
| `common/one-node-backtracking` | `IsMatch` | 0.089 us | 0.613 us | 0.647 us | 0.15x |
| `common/matches-words` | `Count` | 9.425 us | 71.770 us | 73.346 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.859 us | 0.900 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,435.627 us | 32,531.200 us | 46,830.300 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,075.532 us | 19,823.100 us | 20,663.100 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,879.556 us | 23,389.633 us | 30,411.950 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 516.277 us | 8,545.017 us | 8,980.450 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.879 us | 165.137 us | 167.133 us | 0.05x |
| `common/matches-set` | `Count` | 7.858 us | 198.250 us | 200.146 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 272.714 us | 11,977.660 us | 12,618.025 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 242.551 us | 36.536 us | 2,174.671 us | 6.64x |
| `literal/sherlock-zh` | 137.333 us | 24.084 us | 2,202.117 us | 5.70x |
| `literal/sherlock-ru` | 248.539 us | 89.968 us | 5,359.390 us | 2.76x |
| `ruff-noqa/tweaked` | 10,740.440 us | 4,581.100 us | 36,831.500 us | 2.34x |
| `literal/sherlock-casei-en` | 171.845 us | 79.079 us | 443.767 us | 2.17x |
| `aws-keys/quick` | 6,586.288 us | 3,603.093 us | 35,948.200 us | 1.83x |
| `literal-alternate/sherlock-casei-en` | 222.181 us | 147.168 us | 577.078 us | 1.51x |
| `literal-alternate/sherlock-en-mixed` | 206.981 us | 177.504 us | 574.979 us | 1.17x |
| `literal/sherlock-casei-ru` | 250.636 us | 223.233 us | 5,268.280 us | 1.12x |
| `literal-alternate/sherlock-en` | 405.740 us | 376.497 us | 819.684 us | 1.08x |
| `bounded-repeat/letters-ru` | 1,977.700 us | 2,073.028 us | 2,901.956 us | 0.95x |
| `literal/sherlock-en` | 70.451 us | 75.916 us | 450.472 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 167.875 us | 202.780 us | 583.206 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,696.540 us | 2,367.782 us | 7,542.667 us | 0.72x |
| `bounded-repeat/capitals` | 9,562.050 us | 13,641.875 us | 21,352.333 us | 0.70x |
| `aws-keys/full` | 10,755.880 us | 18,928.233 us | 50,672.950 us | 0.57x |
| `bounded-repeat/letters-en` | 1,037.031 us | 1,857.646 us | 1,939.927 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,089.421 us | 1,986.762 us | 7,259.767 us | 0.55x |
| `bounded-repeat/context` | 16,444.233 us | 69,609.100 us | 72,754.800 us | 0.24x |
| `ruff-noqa/real` | 4,577.745 us | 21,180.133 us | 33,735.050 us | 0.22x |
| `dictionary/single` | 8,198.229 us | 46,142.050 us | 46,061.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,537.012 us | 455.186 us | 7,842.067 us | 3.38x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 196.347 us | 58.724 us | 258.008 us | 3.34x |
| `common/match-word` | `Match` | 0.199 us | 0.082 us | 1.005 us | 2.43x |
| `industry/leipzig-river-window-count` | `Count` | 3,443.373 us | 1,529.347 us | 17,267.667 us | 2.25x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.046 us | 0.075 us | 2.01x |
| `common/ip-miss` | `IsMatch` | 0.088 us | 0.050 us | 0.070 us | 1.76x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.067 us | 0.086 us | 1.73x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.029 us | 0.046 us | 1.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.050 us | 1.67x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.049 us | 0.069 us | 1.60x |
| `industry/mariomka-uri-count` | `Count` | 1,753.307 us | 1,102.174 us | 8,534.667 us | 1.59x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.050 us | 0.088 us | 1.58x |
| `common/replace-words` | `Replace` | 28.266 us | 18.198 us | 21.312 us | 1.55x |
| `industry/leipzig-symbol-count` | `Count` | 1,877.452 us | 1,283.240 us | 7,276.833 us | 1.46x |
| `industry/leipzig-name-family-count` | `Count` | 4,323.225 us | 3,120.471 us | 8,669.700 us | 1.39x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.058 us | 0.078 us | 1.33x |
| `common/email-match` | `IsMatch` | 0.101 us | 0.082 us | 0.108 us | 1.24x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.117 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.120 us | 1.03x |
| `common/one-node-backtracking` | `IsMatch` | 0.089 us | 0.089 us | 0.129 us | 1.00x |
| `industry/leipzig-twain-count` | `Count` | 1,265.185 us | 1,280.085 us | 5,700.900 us | 0.99x |
| `common/matches-words` | `Count` | 9.410 us | 10.920 us | 14.271 us | 0.86x |
| `industry/mariomka-ip-count` | `Count` | 2,704.611 us | 3,498.700 us | 11,055.550 us | 0.77x |
| `common/split-words` | `Split` | 14.656 us | 20.710 us | 22.540 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.046 us | 0.074 us | 0.64x |
| `common/matches-word` | `Count` | 1.357 us | 2.392 us | 3.678 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.089 us | 0.203 us | 0.238 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.052 us | 0.28x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 514.661 us | 3,997.838 us | 4,399.358 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,077.380 us | 17,450.600 us | 18,329.067 us | 0.12x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.003 us | 1,495.668 us | 1,846.618 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.987 us | 106.128 us | 107.379 us | 0.08x |
| `common/matches-set` | `Count` | 7.888 us | 106.164 us | 108.236 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 275.147 us | 4,811.491 us | 5,280.750 us | 0.06x |
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
| `lokad/imports/module-imports` | 15.871 us | 6.399 us | 28.973 us | 2.48x |
| `structural/keyword-to-generic-type` | 5.156 us | 4.145 us | 16.003 us | 1.24x |
| `lokad/langserv/color-short-hex` | 1.664 us | 1.369 us | 1.539 us | 1.22x |
| `lokad/lexer/doc-line` | 3.363 us | 3.060 us | 3.691 us | 1.10x |
| `lokad/lexer/identifier` | 24.390 us | 23.095 us | 26.718 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.359 us | 5.347 us | 17.332 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.207 us | 7.382 us | 8.223 us | 0.98x |
| `lokad/langserv/identifier-validator` | 11.394 us | 12.634 us | 14.384 us | 0.90x |
| `fallback/lookahead` | 5.473 us | 6.116 us | 17.196 us | 0.89x |
| `literal-family/method-token-family` | 8.147 us | 9.311 us | 20.116 us | 0.87x |
| `lokad/lexer/operator-run` | 2.928 us | 3.459 us | 3.911 us | 0.85x |
| `lokad/style/hex-color` | 0.608 us | 0.841 us | 0.902 us | 0.72x |
| `lokad/langserv/color-rgb-hex` | 2.070 us | 2.990 us | 3.126 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.406 us | 4.992 us | 5.870 us | 0.68x |
| `literal/identifier-token` | 2.800 us | 4.400 us | 15.621 us | 0.64x |
| `literal/call-token` | 2.607 us | 4.339 us | 15.610 us | 0.60x |
| `lokad/lexer/number` | 8.340 us | 14.368 us | 15.614 us | 0.58x |
| `structural/method-family-generic-call` | 3.460 us | 6.711 us | 17.984 us | 0.52x |
| `lokad/style/cell-ref` | 1.508 us | 3.004 us | 3.108 us | 0.50x |
| `structural/method-family-call` | 3.890 us | 8.135 us | 18.889 us | 0.48x |
| `literal-family/type-token-family` | 10.595 us | 24.118 us | 35.000 us | 0.44x |
| `literal/identifier-token-casei` | 5.753 us | 15.312 us | 26.840 us | 0.38x |
| `lokad/style/range-ref` | 0.928 us | 2.846 us | 2.928 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.665 us | 17.429 us | 28.611 us | 0.33x |
| `structural/ordered-keyword-window` | 5.508 us | 17.714 us | 28.952 us | 0.31x |
| `structural/modifier-family-to-type-window` | 73.527 us | 256.501 us | 269.266 us | 0.29x |
| `lokad/lexer/string` | 24.987 us | 95.935 us | 101.316 us | 0.26x |
| `lokad/folding/region-marker` | 6.956 us | 37.206 us | 61.863 us | 0.19x |
| `structural/modifier-family-to-type-pair` | 23.445 us | 131.477 us | 143.207 us | 0.18x |
| `structural/ordered-identifier-window` | 5.003 us | 28.850 us | 39.876 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.558 us | 24.719 us | 25.177 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.284 us | 220.773 us | 234.433 us | 0.08x |
| `lokad/langserv/url-download` | 2.623 us | 52.355 us | 53.267 us | 0.05x |
| `balancing/begin-end-blocks` | 5.697 us | 4,311.467 us | 4,365.100 us | 0.0013x |
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
| `lokad/imports/module-imports` | 15.855 us | 4.454 us | 26.209 us | 3.56x |
| `structural/keyword-family-to-capitalized-identifier` | 17.972 us | 7.661 us | 18.770 us | 2.35x |
| `structural/modifier-family-to-type-pair` | 23.462 us | 10.865 us | 21.764 us | 2.16x |
| `lokad/langserv/color-short-hex` | 1.671 us | 1.056 us | 1.234 us | 1.58x |
| `lokad/lexer/doc-line` | 3.367 us | 2.288 us | 3.024 us | 1.47x |
| `structural/keyword-to-generic-type` | 5.147 us | 3.535 us | 14.919 us | 1.46x |
| `lokad/langserv/helper-identifier` | 7.232 us | 5.060 us | 6.153 us | 1.43x |
| `lokad/lexer/identifier` | 24.322 us | 17.448 us | 21.222 us | 1.39x |
| `lokad/lexer/operator-run` | 2.931 us | 2.153 us | 2.640 us | 1.36x |
| `structural/modifier-family-to-type-window` | 74.048 us | 56.461 us | 69.393 us | 1.31x |
| `structural/ordered-keyword-pair` | 5.368 us | 4.172 us | 15.365 us | 1.29x |
| `fallback/lookahead` | 5.480 us | 4.262 us | 14.927 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.478 us | 9.559 us | 11.627 us | 1.20x |
| `lokad/style/hex-color` | 0.607 us | 0.528 us | 0.632 us | 1.15x |
| `lokad/docs/inline-doc-prefix` | 3.407 us | 2.974 us | 3.797 us | 1.15x |
| `lokad/lexer/number` | 8.364 us | 7.810 us | 8.972 us | 1.07x |
| `lokad/langserv/color-rgb-hex` | 2.061 us | 1.954 us | 2.142 us | 1.05x |
| `literal/identifier-token-casei` | 5.756 us | 5.999 us | 17.225 us | 0.96x |
| `literal-family/type-token-family` | 10.603 us | 11.449 us | 22.208 us | 0.93x |
| `lokad/style/cell-ref` | 1.551 us | 1.904 us | 2.018 us | 0.81x |
| `structural/method-family-call` | 3.880 us | 5.099 us | 15.521 us | 0.76x |
| `structural/method-family-generic-call` | 3.451 us | 4.571 us | 15.089 us | 0.76x |
| `literal-family/method-token-family` | 3.911 us | 5.334 us | 15.987 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.706 us | 8.692 us | 19.840 us | 0.66x |
| `literal/call-token` | 2.615 us | 3.996 us | 14.642 us | 0.65x |
| `literal/identifier-token` | 2.797 us | 4.321 us | 15.072 us | 0.65x |
| `structural/ordered-keyword-window` | 5.497 us | 8.683 us | 19.442 us | 0.63x |
| `lokad/style/range-ref` | 0.922 us | 1.606 us | 1.707 us | 0.57x |
| `lokad/lexer/string` | 25.035 us | 47.613 us | 51.466 us | 0.53x |
| `structural/ordered-identifier-window` | 5.001 us | 10.788 us | 21.396 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.585 us | 7.531 us | 8.095 us | 0.34x |
| `lokad/langserv/url-download` | 2.627 us | 13.172 us | 13.650 us | 0.20x |
| `lokad/folding/region-marker` | 6.940 us | 37.626 us | 59.804 us | 0.18x |
| `balancing/begin-end-blocks` | 5.728 us | 471.121 us | 497.144 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
