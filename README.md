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
and resource checks passed in **46m23s**. The [run receipt](bench/Experiments/DeliveryDev51-20261107/full-refresh.json)
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
| `literal/sherlock-zh` | 84.626 us | 18.595 us | 2,144.100 us | 4.55x |
| `literal/sherlock-ru` | 167.435 us | 95.922 us | 5,342.750 us | 1.75x |
| `literal-alternate/sherlock-zh` | 107.004 us | 69.073 us | 2,189.836 us | 1.55x |
| `literal/sherlock-casei-en` | 156.422 us | 199.765 us | 565.329 us | 0.78x |
| `literal/sherlock-en` | 56.636 us | 86.213 us | 449.007 us | 0.66x |
| `aws-keys/quick` | 5,947.975 us | 9,388.438 us | 43,584.250 us | 0.63x |
| `literal/sherlock-casei-ru` | 242.839 us | 385.755 us | 5,563.129 us | 0.63x |
| `ruff-noqa/tweaked` | 4,353.961 us | 7,675.773 us | 41,331.450 us | 0.57x |
| `bounded-repeat/letters-en` | 1,069.507 us | 2,757.381 us | 2,896.019 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,639.972 us | 4,749.725 us | 9,899.075 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,967.077 us | 5,908.100 us | 6,713.600 us | 0.33x |
| `dictionary/single` | 8,208.990 us | 25,365.733 us | 25,616.975 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 182.278 us | 595.863 us | 1,007.414 us | 0.31x |
| `literal-alternate/sherlock-en` | 382.672 us | 1,523.571 us | 2,028.970 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 151.625 us | 642.550 us | 1,054.143 us | 0.24x |
| `bounded-repeat/capitals` | 9,431.225 us | 44,744.500 us | 48,429.900 us | 0.21x |
| `bounded-repeat/context` | 20,223.375 us | 147,753.700 us | 151,249.000 us | 0.14x |
| `aws-keys/full` | 8,784.500 us | 85,467.500 us | 118,940.300 us | 0.10x |
| `ruff-noqa/real` | 4,377.722 us | 68,562.400 us | 102,672.200 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 938.108 us | 23,162.025 us | 28,528.050 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 125.138 us | 11,070.600 us | 11,867.529 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,182.778 us | 507.818 us | 8,983.675 us | 2.33x |
| `common/match-word` | `Match` | 0.197 us | 0.171 us | 1.172 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,149.308 us | 1,299.222 us | 8,497.367 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,433.596 us | 1,881.235 us | 10,388.225 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.110 us | 0.148 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.194 us | 0.213 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.100 us | 0.61x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.360 us | 263.816 us | 502.539 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.165 us | 0.184 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.180 us | 0.199 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,060.074 us | 9,883.312 us | 17,012.800 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.184 us | 0.204 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.228 us | 0.264 us | 0.41x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.258 us | 0.285 us | 0.36x |
| `common/replace-words` | `Replace` | 27.853 us | 79.641 us | 81.510 us | 0.35x |
| `common/matches-word` | `Count` | 1.332 us | 4.195 us | 5.846 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.338 us | 0.365 us | 0.30x |
| `industry/mariomka-ip-count` | `Count` | 2,255.424 us | 8,549.033 us | 16,730.600 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.108 us | 0.140 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.097 us | 0.21x |
| `common/split-words` | `Split` | 14.440 us | 76.667 us | 78.395 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.688 us | 960.050 us | 1,223.555 us | 0.18x |
| `common/one-node-backtracking` | `IsMatch` | 0.111 us | 0.615 us | 0.647 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.554 us | 0.586 us | 0.17x |
| `common/matches-words` | `Count` | 9.205 us | 70.068 us | 71.437 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.089 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.825 us | 0.868 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,055.824 us | 19,174.775 us | 19,893.650 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,337.922 us | 31,592.100 us | 39,124.300 us | 0.11x |
| `industry/leipzig-symbol-count` | `Count` | 1,804.781 us | 23,108.375 us | 30,174.850 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 499.592 us | 8,053.500 us | 8,628.567 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.554 us | 162.673 us | 165.223 us | 0.05x |
| `common/matches-set` | `Count` | 7.575 us | 192.566 us | 193.377 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 267.337 us | 12,116.686 us | 12,678.667 us | 0.02x |
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
| `literal/sherlock-zh` | 84.461 us | 23.681 us | 2,118.580 us | 3.57x |
| `literal-alternate/sherlock-zh` | 107.132 us | 35.308 us | 2,150.026 us | 3.03x |
| `literal/sherlock-casei-en` | 154.548 us | 78.829 us | 433.404 us | 1.96x |
| `literal/sherlock-ru` | 168.408 us | 87.263 us | 5,301.520 us | 1.93x |
| `aws-keys/quick` | 6,383.058 us | 3,550.068 us | 35,141.950 us | 1.80x |
| `literal/sherlock-casei-ru` | 244.367 us | 194.197 us | 5,323.033 us | 1.26x |
| `literal-alternate/sherlock-en` | 385.102 us | 365.869 us | 770.131 us | 1.05x |
| `literal-alternate/sherlock-en-mixed` | 184.302 us | 184.339 us | 533.102 us | 1.00x |
| `literal-alternate/sherlock-casei-en` | 124.203 us | 130.823 us | 552.731 us | 0.95x |
| `ruff-noqa/tweaked` | 4,408.856 us | 4,656.341 us | 35,842.000 us | 0.95x |
| `bounded-repeat/letters-ru` | 1,974.946 us | 2,114.842 us | 2,911.646 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 152.522 us | 201.514 us | 575.363 us | 0.76x |
| `literal/sherlock-en` | 56.726 us | 75.831 us | 395.248 us | 0.75x |
| `bounded-repeat/capitals` | 9,361.344 us | 13,443.967 us | 16,437.250 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,596.496 us | 2,335.185 us | 7,407.075 us | 0.68x |
| `bounded-repeat/letters-en` | 1,071.196 us | 1,820.826 us | 1,924.760 us | 0.59x |
| `aws-keys/full` | 10,282.625 us | 18,378.040 us | 52,451.300 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 933.870 us | 1,945.997 us | 7,134.975 us | 0.48x |
| `bounded-repeat/context` | 20,231.325 us | 68,998.500 us | 72,325.700 us | 0.29x |
| `ruff-noqa/real` | 4,365.612 us | 20,368.950 us | 54,390.933 us | 0.21x |
| `dictionary/single` | 8,178.100 us | 45,613.150 us | 45,554.600 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,179.891 us | 475.376 us | 7,832.275 us | 2.48x |
| `common/match-word` | `Match` | 0.196 us | 0.079 us | 1.014 us | 2.47x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.071 us | 0.030 us | 0.048 us | 2.37x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.094 us | 58.385 us | 263.499 us | 2.26x |
| `industry/leipzig-river-window-count` | `Count` | 3,359.274 us | 1,549.667 us | 8,064.960 us | 2.17x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.044 us | 0.074 us | 2.11x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.066 us | 0.085 us | 1.80x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.049 us | 1.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.086 us | 1.63x |
| `common/replace-words` | `Replace` | 27.610 us | 17.703 us | 20.342 us | 1.56x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.050 us | 0.070 us | 1.48x |
| `industry/leipzig-symbol-count` | `Count` | 1,819.795 us | 1,230.139 us | 6,927.500 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.055 us | 0.077 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,073.326 us | 2,997.650 us | 8,255.025 us | 1.36x |
| `industry/mariomka-uri-count` | `Count` | 1,429.081 us | 1,087.154 us | 8,302.450 us | 1.31x |
| `common/one-node-backtracking` | `IsMatch` | 0.112 us | 0.086 us | 0.128 us | 1.30x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.060 us | 0.074 us | 1.27x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.080 us | 0.106 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.087 us | 0.113 us | 1.14x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.088 us | 0.120 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,147.517 us | 1,273.575 us | 7,521.720 us | 0.90x |
| `common/matches-words` | `Count` | 9.205 us | 10.898 us | 13.860 us | 0.84x |
| `common/split-words` | `Split` | 14.440 us | 21.552 us | 23.302 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,263.144 us | 3,407.813 us | 10,686.867 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.65x |
| `common/matches-word` | `Count` | 1.337 us | 2.342 us | 3.409 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.200 us | 0.240 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.037 us | 0.054 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,037.795 us | 8,743.700 us | 9,363.837 us | 0.23x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 495.465 us | 3,938.255 us | 4,299.483 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.698 us | 1,459.248 us | 1,799.314 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.542 us | 104.489 us | 106.003 us | 0.07x |
| `common/matches-set` | `Count` | 7.527 us | 104.468 us | 105.871 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 268.831 us | 4,775.312 us | 5,231.093 us | 0.06x |
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
| `lokad/imports/module-imports` | 10.226 us | 6.335 us | 28.906 us | 1.61x |
| `structural/keyword-to-generic-type` | 5.413 us | 4.095 us | 15.628 us | 1.32x |
| `lokad/langserv/color-short-hex` | 1.659 us | 1.340 us | 1.439 us | 1.24x |
| `lokad/lexer/doc-line` | 3.323 us | 2.961 us | 3.501 us | 1.12x |
| `structural/ordered-keyword-pair` | 5.507 us | 5.258 us | 16.724 us | 1.05x |
| `lokad/lexer/identifier` | 23.367 us | 22.457 us | 25.177 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.046 us | 7.192 us | 7.503 us | 0.98x |
| `lokad/style/hex-color` | 0.741 us | 0.799 us | 0.835 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.272 us | 12.384 us | 13.833 us | 0.91x |
| `fallback/lookahead` | 5.308 us | 6.010 us | 17.071 us | 0.88x |
| `lokad/lexer/operator-run` | 3.016 us | 3.510 us | 3.901 us | 0.86x |
| `literal-family/method-token-family` | 7.027 us | 9.154 us | 19.851 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.006 us | 2.916 us | 2.985 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.338 us | 4.862 us | 5.507 us | 0.69x |
| `literal/identifier-token` | 2.750 us | 4.334 us | 15.365 us | 0.63x |
| `literal/call-token` | 2.567 us | 4.271 us | 15.518 us | 0.60x |
| `lokad/lexer/number` | 8.126 us | 14.228 us | 15.276 us | 0.57x |
| `structural/ordered-identifier-window` | 15.961 us | 28.430 us | 39.206 us | 0.56x |
| `structural/method-family-generic-call` | 3.412 us | 6.686 us | 17.770 us | 0.51x |
| `lokad/style/cell-ref` | 1.459 us | 2.961 us | 2.915 us | 0.49x |
| `structural/method-family-call` | 3.834 us | 8.173 us | 19.118 us | 0.47x |
| `literal-family/type-token-family` | 10.264 us | 22.916 us | 33.957 us | 0.45x |
| `literal/identifier-token-casei` | 6.022 us | 14.897 us | 26.346 us | 0.40x |
| `lokad/style/range-ref` | 0.976 us | 2.682 us | 2.705 us | 0.36x |
| `structural/ordered-keyword-window` | 5.337 us | 17.357 us | 28.740 us | 0.31x |
| `structural/modifier-family-to-type-window` | 77.075 us | 252.662 us | 267.586 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.313 us | 17.421 us | 28.489 us | 0.30x |
| `lokad/lexer/string` | 21.271 us | 94.357 us | 98.628 us | 0.23x |
| `lokad/folding/region-marker` | 6.119 us | 36.486 us | 60.607 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.544 us | 24.402 us | 24.615 us | 0.10x |
| `lokad/langserv/url-download` | 2.491 us | 49.583 us | 50.283 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 11.284 us | 236.340 us | 250.362 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.952 us | 133.684 us | 145.671 us | 0.04x |
| `balancing/begin-end-blocks` | 5.639 us | 4,307.067 us | 4,350.406 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.182 us | 4.378 us | 26.097 us | 2.33x |
| `lokad/langserv/color-short-hex` | 1.666 us | 0.969 us | 1.157 us | 1.72x |
| `lokad/langserv/helper-identifier` | 7.106 us | 4.518 us | 5.571 us | 1.57x |
| `structural/keyword-to-generic-type` | 5.413 us | 3.474 us | 14.550 us | 1.56x |
| `lokad/lexer/doc-line` | 3.344 us | 2.167 us | 2.880 us | 1.54x |
| `structural/ordered-identifier-window` | 15.943 us | 10.433 us | 21.187 us | 1.53x |
| `lokad/style/hex-color` | 0.746 us | 0.494 us | 0.591 us | 1.51x |
| `structural/keyword-family-to-capitalized-identifier` | 11.359 us | 7.755 us | 18.303 us | 1.46x |
| `lokad/lexer/operator-run` | 3.004 us | 2.095 us | 2.675 us | 1.43x |
| `lokad/lexer/identifier` | 23.475 us | 16.710 us | 20.010 us | 1.40x |
| `structural/modifier-family-to-type-window` | 77.440 us | 55.540 us | 68.029 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.520 us | 4.106 us | 14.889 us | 1.34x |
| `lokad/langserv/identifier-validator` | 11.300 us | 8.648 us | 10.968 us | 1.31x |
| `fallback/lookahead` | 5.323 us | 4.183 us | 14.893 us | 1.27x |
| `lokad/docs/inline-doc-prefix` | 3.350 us | 2.691 us | 3.501 us | 1.24x |
| `lokad/lexer/number` | 8.170 us | 7.239 us | 8.427 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.025 us | 1.806 us | 1.937 us | 1.12x |
| `literal/identifier-token-casei` | 6.002 us | 5.850 us | 17.079 us | 1.03x |
| `literal-family/type-token-family` | 10.204 us | 11.301 us | 21.772 us | 0.90x |
| `lokad/style/cell-ref` | 1.458 us | 1.736 us | 1.910 us | 0.84x |
| `structural/method-family-generic-call` | 3.433 us | 4.440 us | 14.947 us | 0.77x |
| `structural/method-family-call` | 3.819 us | 5.021 us | 15.628 us | 0.76x |
| `literal-family/method-token-family` | 3.836 us | 5.259 us | 15.861 us | 0.73x |
| `lokad/style/range-ref` | 0.987 us | 1.431 us | 1.540 us | 0.69x |
| `literal/call-token` | 2.566 us | 3.946 us | 14.641 us | 0.65x |
| `structural/ordered-keyword-window` | 5.385 us | 8.480 us | 19.405 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.369 us | 8.547 us | 19.629 us | 0.63x |
| `literal/identifier-token` | 2.765 us | 4.619 us | 15.191 us | 0.60x |
| `lokad/lexer/string` | 21.491 us | 44.206 us | 47.649 us | 0.49x |
| `structural/modifier-family-to-type-pair` | 4.934 us | 10.656 us | 21.410 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.591 us | 7.493 us | 8.046 us | 0.35x |
| `lokad/langserv/url-download` | 2.517 us | 12.814 us | 13.363 us | 0.20x |
| `lokad/folding/region-marker` | 6.167 us | 36.398 us | 59.623 us | 0.17x |
| `balancing/begin-end-blocks` | 5.622 us | 461.441 us | 491.501 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
