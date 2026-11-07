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
and resource checks passed in **46m59s**. The [run receipt](bench/Experiments/DeliveryDev52-20261107/full-refresh.json)
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
| `literal/sherlock-zh` | 86.210 us | 18.604 us | 2,183.406 us | 4.63x |
| `literal/sherlock-ru` | 178.939 us | 100.115 us | 5,343.079 us | 1.79x |
| `literal-alternate/sherlock-zh` | 103.388 us | 67.916 us | 2,199.415 us | 1.52x |
| `literal/sherlock-casei-en` | 160.332 us | 189.081 us | 589.598 us | 0.85x |
| `literal/sherlock-en` | 56.598 us | 83.952 us | 439.535 us | 0.67x |
| `literal/sherlock-casei-ru` | 245.791 us | 387.330 us | 5,589.479 us | 0.63x |
| `ruff-noqa/tweaked` | 4,341.967 us | 7,492.900 us | 42,480.550 us | 0.58x |
| `aws-keys/quick` | 4,986.973 us | 9,702.388 us | 43,393.800 us | 0.51x |
| `bounded-repeat/letters-en` | 1,064.994 us | 2,834.030 us | 2,964.877 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,658.170 us | 4,822.712 us | 10,023.225 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,998.626 us | 5,918.369 us | 6,709.958 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 195.190 us | 588.563 us | 1,034.816 us | 0.33x |
| `dictionary/single` | 8,136.450 us | 25,514.900 us | 25,443.233 us | 0.32x |
| `literal-alternate/sherlock-en` | 387.956 us | 1,494.354 us | 2,033.081 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 151.337 us | 666.817 us | 1,067.974 us | 0.23x |
| `bounded-repeat/capitals` | 9,397.025 us | 45,826.450 us | 49,691.950 us | 0.21x |
| `bounded-repeat/context` | 20,131.900 us | 146,224.000 us | 149,442.600 us | 0.14x |
| `aws-keys/full` | 10,443.938 us | 85,920.500 us | 117,933.100 us | 0.12x |
| `ruff-noqa/real` | 4,360.339 us | 68,770.600 us | 99,292.800 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 930.163 us | 22,943.450 us | 28,260.000 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 129.925 us | 11,397.543 us | 12,217.614 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,143.739 us | 488.179 us | 8,985.700 us | 2.34x |
| `common/match-word` | `Match` | 0.196 us | 0.166 us | 1.107 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,199.621 us | 1,336.264 us | 8,689.760 us | 0.90x |
| `industry/mariomka-uri-count` | `Count` | 1,430.276 us | 1,883.339 us | 10,297.700 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.109 us | 0.145 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.191 us | 0.210 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 130.726 us | 262.571 us | 518.815 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.166 us | 0.184 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.173 us | 0.190 us | 0.44x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.228 us | 0.263 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 3,932.311 us | 9,748.038 us | 16,536.500 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.187 us | 0.204 us | 0.40x |
| `common/replace-words` | `Replace` | 27.875 us | 75.873 us | 77.664 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.268 us | 0.293 us | 0.36x |
| `common/matches-word` | `Count` | 1.319 us | 4.217 us | 5.952 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.338 us | 0.363 us | 0.30x |
| `industry/mariomka-ip-count` | `Count` | 2,190.579 us | 8,256.110 us | 16,836.300 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.081 us | 0.097 us | 0.20x |
| `common/split-words` | `Split` | 14.510 us | 77.039 us | 78.456 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 173.484 us | 953.570 us | 1,218.400 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.555 us | 0.590 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.608 us | 0.645 us | 0.13x |
| `common/matches-words` | `Count` | 9.239 us | 70.686 us | 71.778 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.089 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.845 us | 0.886 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,296.774 us | 31,894.133 us | 38,822.350 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,946.590 us | 19,366.125 us | 20,108.850 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,824.695 us | 22,527.800 us | 30,191.850 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 504.701 us | 8,073.740 us | 8,653.833 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.583 us | 163.257 us | 166.343 us | 0.05x |
| `common/matches-set` | `Count` | 7.344 us | 188.670 us | 190.769 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 260.851 us | 12,092.229 us | 12,712.983 us | 0.02x |
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
| `literal/sherlock-zh` | 85.896 us | 23.855 us | 2,179.071 us | 3.60x |
| `literal-alternate/sherlock-zh` | 103.001 us | 36.017 us | 2,168.980 us | 2.86x |
| `literal/sherlock-casei-en` | 159.469 us | 78.335 us | 439.900 us | 2.04x |
| `literal/sherlock-ru` | 180.351 us | 90.415 us | 5,195.620 us | 1.99x |
| `aws-keys/quick` | 5,032.627 us | 3,676.086 us | 34,279.600 us | 1.37x |
| `literal/sherlock-casei-ru` | 248.291 us | 221.004 us | 5,262.379 us | 1.12x |
| `literal-alternate/sherlock-en-mixed` | 195.888 us | 177.955 us | 530.453 us | 1.10x |
| `literal-alternate/sherlock-en` | 387.021 us | 365.764 us | 737.799 us | 1.06x |
| `bounded-repeat/letters-ru` | 1,989.464 us | 2,041.284 us | 2,896.888 us | 0.97x |
| `ruff-noqa/tweaked` | 4,328.700 us | 4,520.529 us | 37,153.400 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 129.225 us | 143.458 us | 574.166 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 152.479 us | 196.097 us | 596.496 us | 0.78x |
| `literal/sherlock-en` | 57.053 us | 74.174 us | 409.390 us | 0.77x |
| `literal-alternate/sherlock-ru` | 1,656.376 us | 2,307.745 us | 7,380.725 us | 0.72x |
| `bounded-repeat/capitals` | 9,422.212 us | 13,561.483 us | 16,447.025 us | 0.69x |
| `bounded-repeat/letters-en` | 1,062.674 us | 1,816.719 us | 1,911.838 us | 0.58x |
| `aws-keys/full` | 9,172.450 us | 18,426.960 us | 51,254.333 us | 0.50x |
| `literal-alternate/sherlock-casei-ru` | 931.354 us | 1,943.228 us | 7,246.440 us | 0.48x |
| `bounded-repeat/context` | 20,084.850 us | 68,497.500 us | 71,517.500 us | 0.29x |
| `ruff-noqa/real` | 4,399.878 us | 20,798.500 us | 51,768.300 us | 0.21x |
| `dictionary/single` | 8,047.650 us | 45,561.550 us | 45,625.200 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,152.634 us | 453.629 us | 7,714.500 us | 2.54x |
| `common/match-word` | `Match` | 0.196 us | 0.078 us | 0.984 us | 2.52x |
| `industry/leipzig-river-window-count` | `Count` | 3,285.763 us | 1,495.447 us | 7,643.475 us | 2.20x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 130.797 us | 60.002 us | 260.591 us | 2.18x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.043 us | 0.074 us | 2.18x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.067 us | 0.086 us | 1.77x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.029 us | 0.046 us | 1.66x |
| `common/ip-match` | `IsMatch` | 0.082 us | 0.050 us | 0.070 us | 1.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.031 us | 0.049 us | 1.64x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.089 us | 1.63x |
| `common/replace-words` | `Replace` | 27.856 us | 17.260 us | 20.354 us | 1.61x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.049 us | 0.070 us | 1.52x |
| `industry/leipzig-symbol-count` | `Count` | 1,832.824 us | 1,215.386 us | 7,198.040 us | 1.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.055 us | 0.076 us | 1.36x |
| `industry/leipzig-name-family-count` | `Count` | 3,996.372 us | 2,969.488 us | 8,169.250 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,433.257 us | 1,090.987 us | 8,312.750 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.081 us | 0.106 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.088 us | 0.115 us | 1.14x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.087 us | 0.120 us | 1.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.086 us | 0.127 us | 0.94x |
| `industry/leipzig-twain-count` | `Count` | 1,197.608 us | 1,318.581 us | 6,530.500 us | 0.91x |
| `common/matches-words` | `Count` | 9.233 us | 11.077 us | 14.460 us | 0.83x |
| `common/split-words` | `Split` | 14.512 us | 21.398 us | 25.817 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.074 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,197.435 us | 3,480.295 us | 10,834.033 us | 0.63x |
| `common/matches-word` | `Count` | 1.336 us | 2.347 us | 3.498 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.201 us | 0.235 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,934.018 us | 8,762.522 us | 9,244.112 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.197 us | 3,933.084 us | 4,319.200 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.989 us | 1,465.477 us | 1,813.809 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.555 us | 102.726 us | 105.976 us | 0.07x |
| `common/matches-set` | `Count` | 7.351 us | 103.762 us | 105.573 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 259.088 us | 4,713.913 us | 5,191.013 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.917 us | 6.261 us | 28.883 us | 1.58x |
| `structural/keyword-to-generic-type` | 5.469 us | 4.003 us | 15.770 us | 1.37x |
| `lokad/langserv/color-short-hex` | 1.636 us | 1.343 us | 1.438 us | 1.22x |
| `lokad/lexer/doc-line` | 3.322 us | 3.017 us | 3.545 us | 1.10x |
| `structural/ordered-keyword-pair` | 5.475 us | 5.251 us | 16.661 us | 1.04x |
| `lokad/lexer/identifier` | 23.589 us | 22.730 us | 25.021 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.137 us | 7.172 us | 7.734 us | 1.00x |
| `lokad/langserv/identifier-validator` | 11.340 us | 12.325 us | 13.596 us | 0.92x |
| `fallback/lookahead` | 5.521 us | 6.035 us | 17.365 us | 0.91x |
| `lokad/lexer/operator-run` | 2.924 us | 3.465 us | 3.897 us | 0.84x |
| `literal-family/method-token-family` | 6.958 us | 8.982 us | 19.638 us | 0.77x |
| `lokad/style/hex-color` | 0.599 us | 0.812 us | 0.869 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 3.341 us | 4.753 us | 5.429 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.025 us | 2.921 us | 2.929 us | 0.69x |
| `literal/identifier-token` | 2.751 us | 4.321 us | 15.453 us | 0.64x |
| `literal/call-token` | 2.563 us | 4.249 us | 15.630 us | 0.60x |
| `lokad/lexer/number` | 8.555 us | 14.361 us | 15.152 us | 0.60x |
| `structural/ordered-identifier-window` | 15.976 us | 28.624 us | 39.327 us | 0.56x |
| `lokad/style/cell-ref` | 1.479 us | 2.903 us | 2.965 us | 0.51x |
| `structural/method-family-generic-call` | 3.403 us | 6.695 us | 18.328 us | 0.51x |
| `structural/method-family-call` | 3.854 us | 8.092 us | 18.705 us | 0.48x |
| `literal-family/type-token-family` | 10.375 us | 23.393 us | 34.167 us | 0.44x |
| `literal/identifier-token-casei` | 5.585 us | 14.735 us | 26.593 us | 0.38x |
| `lokad/style/range-ref` | 0.986 us | 2.737 us | 2.784 us | 0.36x |
| `structural/ordered-modifier-pair` | 5.401 us | 17.113 us | 28.055 us | 0.32x |
| `structural/modifier-family-to-type-window` | 79.245 us | 255.967 us | 267.185 us | 0.31x |
| `structural/ordered-keyword-window` | 5.394 us | 17.813 us | 28.977 us | 0.30x |
| `lokad/lexer/string` | 21.471 us | 93.780 us | 98.613 us | 0.23x |
| `lokad/folding/region-marker` | 5.954 us | 36.809 us | 60.713 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.503 us | 24.322 us | 24.600 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.405 us | 215.671 us | 230.366 us | 0.05x |
| `lokad/langserv/url-download` | 2.540 us | 49.763 us | 50.030 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.887 us | 131.417 us | 142.984 us | 0.04x |
| `balancing/begin-end-blocks` | 5.634 us | 4,257.779 us | 4,277.789 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.032 us | 4.382 us | 26.064 us | 2.29x |
| `lokad/langserv/color-short-hex` | 1.642 us | 0.966 us | 1.154 us | 1.70x |
| `structural/keyword-to-generic-type` | 5.460 us | 3.471 us | 14.744 us | 1.57x |
| `lokad/langserv/helper-identifier` | 7.200 us | 4.634 us | 5.689 us | 1.55x |
| `structural/ordered-identifier-window` | 16.150 us | 10.483 us | 21.259 us | 1.54x |
| `lokad/lexer/doc-line` | 3.311 us | 2.170 us | 2.914 us | 1.53x |
| `lokad/lexer/identifier` | 23.574 us | 16.058 us | 20.242 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.252 us | 7.764 us | 18.658 us | 1.45x |
| `lokad/lexer/operator-run` | 2.911 us | 2.012 us | 2.655 us | 1.45x |
| `structural/modifier-family-to-type-window` | 78.891 us | 55.320 us | 67.831 us | 1.43x |
| `fallback/lookahead` | 5.541 us | 4.175 us | 15.087 us | 1.33x |
| `structural/ordered-keyword-pair` | 5.462 us | 4.117 us | 14.866 us | 1.33x |
| `lokad/langserv/identifier-validator` | 11.313 us | 8.695 us | 11.000 us | 1.30x |
| `lokad/docs/inline-doc-prefix` | 3.334 us | 2.715 us | 3.537 us | 1.23x |
| `lokad/style/hex-color` | 0.599 us | 0.500 us | 0.605 us | 1.20x |
| `lokad/lexer/number` | 8.586 us | 7.364 us | 8.263 us | 1.17x |
| `lokad/langserv/color-rgb-hex` | 2.027 us | 1.789 us | 2.044 us | 1.13x |
| `literal/identifier-token-casei` | 5.606 us | 5.774 us | 17.117 us | 0.97x |
| `literal-family/type-token-family` | 10.405 us | 11.403 us | 22.009 us | 0.91x |
| `lokad/style/cell-ref` | 1.472 us | 1.728 us | 1.899 us | 0.85x |
| `structural/method-family-generic-call` | 3.390 us | 4.436 us | 15.410 us | 0.76x |
| `structural/method-family-call` | 3.816 us | 5.001 us | 15.398 us | 0.76x |
| `literal-family/method-token-family` | 3.772 us | 5.188 us | 15.555 us | 0.73x |
| `lokad/style/range-ref` | 0.986 us | 1.472 us | 1.526 us | 0.67x |
| `literal/call-token` | 2.551 us | 3.954 us | 14.729 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.406 us | 8.540 us | 19.543 us | 0.63x |
| `structural/ordered-keyword-window` | 5.347 us | 8.600 us | 19.592 us | 0.62x |
| `literal/identifier-token` | 2.759 us | 4.639 us | 14.930 us | 0.59x |
| `lokad/lexer/string` | 21.438 us | 46.228 us | 48.745 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 4.893 us | 10.645 us | 21.475 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.499 us | 7.437 us | 8.046 us | 0.34x |
| `lokad/langserv/url-download` | 2.549 us | 12.891 us | 13.541 us | 0.20x |
| `lokad/folding/region-marker` | 5.995 us | 35.374 us | 60.762 us | 0.17x |
| `balancing/begin-end-blocks` | 5.617 us | 461.665 us | 491.380 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
