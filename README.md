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
and resource checks passed in **46m08s**. The [run receipt](bench/Experiments/DeliveryDev45-20261106/full-refresh.json)
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
| `literal/sherlock-zh` | 85.286 us | 18.596 us | 2,152.647 us | 4.59x |
| `literal/sherlock-ru` | 165.866 us | 96.659 us | 5,294.943 us | 1.72x |
| `literal-alternate/sherlock-zh` | 104.527 us | 67.990 us | 2,146.814 us | 1.54x |
| `literal/sherlock-casei-en` | 155.232 us | 185.726 us | 596.261 us | 0.84x |
| `literal/sherlock-casei-ru` | 246.141 us | 356.380 us | 5,669.754 us | 0.69x |
| `literal/sherlock-en` | 56.451 us | 86.085 us | 435.058 us | 0.66x |
| `ruff-noqa/tweaked` | 4,321.906 us | 7,421.327 us | 42,078.750 us | 0.58x |
| `aws-keys/quick` | 4,963.407 us | 9,517.200 us | 44,575.000 us | 0.52x |
| `bounded-repeat/letters-en` | 1,032.462 us | 2,796.967 us | 2,914.500 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,613.821 us | 4,757.663 us | 10,001.525 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,909.915 us | 5,850.515 us | 6,725.292 us | 0.33x |
| `dictionary/single` | 8,212.950 us | 26,137.433 us | 26,082.467 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 184.128 us | 588.871 us | 1,003.021 us | 0.31x |
| `literal-alternate/sherlock-en` | 381.757 us | 1,512.310 us | 2,079.650 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 153.871 us | 653.749 us | 1,060.833 us | 0.24x |
| `bounded-repeat/capitals` | 9,369.775 us | 46,099.300 us | 50,349.100 us | 0.20x |
| `bounded-repeat/context` | 24,116.425 us | 145,343.700 us | 148,547.100 us | 0.17x |
| `aws-keys/full` | 8,731.112 us | 85,460.100 us | 116,220.800 us | 0.10x |
| `ruff-noqa/real` | 4,509.659 us | 69,369.200 us | 86,332.700 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 970.142 us | 22,821.050 us | 28,134.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 133.392 us | 11,565.671 us | 12,399.214 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,176.249 us | 486.005 us | 8,910.950 us | 2.42x |
| `common/match-word` | `Match` | 0.196 us | 0.171 us | 1.148 us | 1.14x |
| `industry/leipzig-twain-count` | `Count` | 1,201.963 us | 1,343.333 us | 8,636.180 us | 0.89x |
| `industry/mariomka-uri-count` | `Count` | 1,404.004 us | 1,841.448 us | 10,441.475 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.112 us | 0.147 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.083 us | 0.099 us | 0.59x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.202 us | 0.223 us | 0.59x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 131.045 us | 254.164 us | 488.062 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.167 us | 0.182 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.079 us | 0.170 us | 0.191 us | 0.46x |
| `industry/leipzig-name-family-count` | `Count` | 3,964.863 us | 9,762.950 us | 17,063.733 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.184 us | 0.203 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.235 us | 0.271 us | 0.39x |
| `common/replace-words` | `Replace` | 27.911 us | 76.867 us | 78.127 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.266 us | 0.289 us | 0.35x |
| `common/matches-word` | `Count` | 1.322 us | 4.185 us | 5.913 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.346 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,318.418 us | 8,331.844 us | 16,623.033 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.106 us | 0.135 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.095 us | 0.21x |
| `common/split-words` | `Split` | 14.357 us | 77.376 us | 79.184 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 173.042 us | 945.499 us | 1,243.554 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.555 us | 0.580 us | 0.15x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.594 us | 0.633 us | 0.13x |
| `common/matches-words` | `Count` | 9.115 us | 70.483 us | 71.939 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.089 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.814 us | 0.858 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,281.996 us | 31,820.000 us | 39,047.000 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,990.836 us | 19,469.550 us | 20,160.750 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,854.038 us | 23,078.625 us | 30,857.700 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 503.281 us | 8,241.700 us | 8,714.833 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.673 us | 159.850 us | 162.606 us | 0.05x |
| `common/matches-set` | `Count` | 7.309 us | 191.975 us | 192.644 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 239.339 us | 11,525.514 us | 12,275.243 us | 0.02x |
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
| `literal/sherlock-zh` | 84.483 us | 23.646 us | 2,171.557 us | 3.57x |
| `literal-alternate/sherlock-zh` | 105.423 us | 35.351 us | 2,125.049 us | 2.98x |
| `literal/sherlock-casei-en` | 156.306 us | 77.563 us | 445.135 us | 2.02x |
| `literal/sherlock-ru` | 167.148 us | 87.973 us | 5,251.720 us | 1.90x |
| `aws-keys/quick` | 5,010.394 us | 3,618.229 us | 37,407.050 us | 1.38x |
| `literal/sherlock-casei-ru` | 245.868 us | 195.267 us | 5,461.460 us | 1.26x |
| `literal-alternate/sherlock-en` | 383.197 us | 368.130 us | 785.801 us | 1.04x |
| `literal-alternate/sherlock-en-mixed` | 182.865 us | 181.929 us | 530.864 us | 1.01x |
| `ruff-noqa/tweaked` | 4,419.506 us | 4,492.747 us | 36,939.650 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,899.705 us | 2,025.159 us | 2,851.031 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 134.447 us | 143.603 us | 558.622 us | 0.94x |
| `literal-alternate/sherlock-en-nomatch` | 153.273 us | 196.758 us | 598.171 us | 0.78x |
| `literal/sherlock-en` | 56.292 us | 75.229 us | 419.550 us | 0.75x |
| `bounded-repeat/capitals` | 9,400.987 us | 13,268.833 us | 16,429.800 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,616.891 us | 2,313.194 us | 7,390.150 us | 0.70x |
| `bounded-repeat/letters-en` | 1,035.815 us | 1,830.281 us | 1,919.538 us | 0.57x |
| `aws-keys/full` | 10,178.837 us | 18,061.500 us | 51,085.567 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 960.036 us | 1,938.267 us | 7,122.280 us | 0.50x |
| `bounded-repeat/context` | 23,358.125 us | 68,027.650 us | 71,646.250 us | 0.34x |
| `ruff-noqa/real` | 4,572.500 us | 20,742.225 us | 37,202.167 us | 0.22x |
| `dictionary/single` | 8,179.570 us | 45,105.150 us | 45,119.850 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,177.029 us | 454.474 us | 7,708.450 us | 2.59x |
| `common/match-word` | `Match` | 0.196 us | 0.080 us | 1.011 us | 2.46x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 130.083 us | 57.627 us | 262.322 us | 2.26x |
| `industry/leipzig-river-window-count` | `Count` | 3,288.713 us | 1,511.160 us | 7,925.000 us | 2.18x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.043 us | 0.072 us | 2.16x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.066 us | 0.084 us | 1.81x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.029 us | 0.048 us | 1.72x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.048 us | 0.029 us | 0.046 us | 1.67x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.050 us | 0.086 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.048 us | 0.067 us | 1.58x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.049 us | 0.071 us | 1.52x |
| `industry/leipzig-symbol-count` | `Count` | 1,857.880 us | 1,266.685 us | 7,495.140 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.077 us | 1.37x |
| `industry/leipzig-name-family-count` | `Count` | 4,081.989 us | 3,051.248 us | 8,302.175 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,409.330 us | 1,076.796 us | 8,243.150 us | 1.31x |
| `common/replace-words` | `Replace` | 27.558 us | 22.527 us | 24.546 us | 1.22x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.079 us | 0.107 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.088 us | 0.114 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.087 us | 0.119 us | 1.05x |
| `industry/leipzig-twain-count` | `Count` | 1,213.571 us | 1,319.221 us | 6,353.500 us | 0.92x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.086 us | 0.127 us | 0.91x |
| `common/matches-words` | `Count` | 9.232 us | 10.851 us | 13.907 us | 0.85x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.072 us | 0.68x |
| `common/split-words` | `Split` | 14.416 us | 21.452 us | 23.142 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,315.148 us | 3,464.826 us | 10,717.000 us | 0.67x |
| `common/matches-word` | `Count` | 1.312 us | 2.303 us | 3.454 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.203 us | 0.236 us | 0.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.054 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,975.427 us | 8,707.900 us | 9,741.438 us | 0.23x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.785 us | 3,928.975 us | 4,293.189 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.568 us | 1,444.815 us | 1,800.407 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.655 us | 103.278 us | 104.240 us | 0.07x |
| `common/matches-set` | `Count` | 7.338 us | 103.648 us | 105.936 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 243.183 us | 4,725.994 us | 5,188.447 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.420 us | 6.279 us | 28.989 us | 1.66x |
| `structural/keyword-to-generic-type` | 5.395 us | 4.044 us | 15.928 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.637 us | 1.337 us | 1.439 us | 1.22x |
| `lokad/lexer/doc-line` | 3.294 us | 2.956 us | 3.616 us | 1.11x |
| `structural/ordered-keyword-pair` | 5.506 us | 5.227 us | 16.604 us | 1.05x |
| `lokad/lexer/identifier` | 23.318 us | 22.790 us | 25.337 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.113 us | 7.287 us | 7.618 us | 0.98x |
| `fallback/lookahead` | 5.565 us | 5.986 us | 17.067 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.258 us | 12.583 us | 14.130 us | 0.89x |
| `lokad/lexer/operator-run` | 2.903 us | 3.388 us | 3.866 us | 0.86x |
| `literal-family/method-token-family` | 7.041 us | 9.225 us | 19.638 us | 0.76x |
| `lokad/style/hex-color` | 0.595 us | 0.823 us | 0.869 us | 0.72x |
| `lokad/langserv/color-rgb-hex` | 2.007 us | 2.878 us | 2.910 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.330 us | 4.834 us | 5.267 us | 0.69x |
| `literal/identifier-token` | 2.722 us | 4.273 us | 15.381 us | 0.64x |
| `literal/call-token` | 2.563 us | 4.242 us | 15.423 us | 0.60x |
| `lokad/lexer/number` | 8.025 us | 13.969 us | 15.237 us | 0.57x |
| `structural/ordered-identifier-window` | 15.976 us | 28.297 us | 39.336 us | 0.56x |
| `structural/method-family-generic-call` | 3.453 us | 6.758 us | 18.223 us | 0.51x |
| `lokad/style/cell-ref` | 1.473 us | 2.900 us | 2.957 us | 0.51x |
| `structural/method-family-call` | 3.812 us | 8.006 us | 18.638 us | 0.48x |
| `literal-family/type-token-family` | 10.414 us | 23.643 us | 34.786 us | 0.44x |
| `literal/identifier-token-casei` | 5.658 us | 14.806 us | 26.074 us | 0.38x |
| `lokad/style/range-ref` | 0.984 us | 2.756 us | 2.697 us | 0.36x |
| `structural/ordered-modifier-pair` | 5.529 us | 16.866 us | 27.994 us | 0.33x |
| `structural/modifier-family-to-type-window` | 78.312 us | 255.288 us | 268.058 us | 0.31x |
| `structural/ordered-keyword-window` | 5.356 us | 17.536 us | 29.057 us | 0.31x |
| `lokad/lexer/string` | 21.431 us | 94.554 us | 99.230 us | 0.23x |
| `lokad/folding/region-marker` | 6.314 us | 37.235 us | 60.672 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.524 us | 24.068 us | 24.563 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.267 us | 215.554 us | 227.692 us | 0.05x |
| `lokad/langserv/url-download` | 2.670 us | 51.331 us | 51.076 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.910 us | 134.807 us | 145.344 us | 0.04x |
| `balancing/begin-end-blocks` | 5.612 us | 4,242.667 us | 4,287.056 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.451 us | 4.354 us | 26.350 us | 2.40x |
| `lokad/langserv/color-short-hex` | 1.667 us | 0.966 us | 1.157 us | 1.73x |
| `lokad/langserv/helper-identifier` | 7.129 us | 4.564 us | 5.535 us | 1.56x |
| `structural/keyword-to-generic-type` | 5.398 us | 3.465 us | 14.859 us | 1.56x |
| `structural/ordered-identifier-window` | 16.019 us | 10.473 us | 21.214 us | 1.53x |
| `lokad/lexer/doc-line` | 3.283 us | 2.199 us | 2.950 us | 1.49x |
| `lokad/lexer/operator-run` | 2.932 us | 2.037 us | 2.550 us | 1.44x |
| `lokad/lexer/identifier` | 23.498 us | 16.333 us | 20.201 us | 1.44x |
| `structural/keyword-family-to-capitalized-identifier` | 11.208 us | 7.884 us | 18.577 us | 1.42x |
| `structural/modifier-family-to-type-window` | 78.607 us | 55.935 us | 69.041 us | 1.41x |
| `structural/ordered-keyword-pair` | 5.533 us | 4.082 us | 14.873 us | 1.36x |
| `fallback/lookahead` | 5.549 us | 4.149 us | 14.943 us | 1.34x |
| `lokad/langserv/identifier-validator` | 11.254 us | 9.132 us | 11.567 us | 1.23x |
| `lokad/docs/inline-doc-prefix` | 3.326 us | 2.739 us | 3.522 us | 1.21x |
| `lokad/style/hex-color` | 0.599 us | 0.514 us | 0.596 us | 1.17x |
| `lokad/langserv/color-rgb-hex` | 1.999 us | 1.794 us | 1.993 us | 1.11x |
| `lokad/lexer/number` | 7.965 us | 7.369 us | 8.396 us | 1.08x |
| `literal/identifier-token-casei` | 5.642 us | 5.835 us | 17.064 us | 0.97x |
| `literal-family/type-token-family` | 10.399 us | 11.375 us | 22.028 us | 0.91x |
| `lokad/style/cell-ref` | 1.484 us | 1.758 us | 1.923 us | 0.84x |
| `structural/method-family-call` | 3.792 us | 4.866 us | 15.389 us | 0.78x |
| `structural/method-family-generic-call` | 3.419 us | 4.492 us | 15.163 us | 0.76x |
| `literal-family/method-token-family` | 3.871 us | 5.266 us | 15.646 us | 0.74x |
| `lokad/style/range-ref` | 0.985 us | 1.483 us | 1.563 us | 0.66x |
| `literal/call-token` | 2.565 us | 3.904 us | 14.532 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.529 us | 8.543 us | 19.603 us | 0.65x |
| `structural/ordered-keyword-window` | 5.371 us | 8.579 us | 19.698 us | 0.63x |
| `literal/identifier-token` | 2.734 us | 4.631 us | 15.208 us | 0.59x |
| `lokad/lexer/string` | 21.343 us | 45.270 us | 48.408 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.557 us | 7.394 us | 7.863 us | 0.35x |
| `structural/modifier-family-to-type-pair` | 4.920 us | 15.468 us | 21.598 us | 0.32x |
| `lokad/langserv/url-download` | 2.600 us | 12.741 us | 13.453 us | 0.20x |
| `lokad/folding/region-marker` | 6.341 us | 35.110 us | 59.287 us | 0.18x |
| `balancing/begin-end-blocks` | 5.633 us | 459.917 us | 489.288 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
