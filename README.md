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
and resource checks passed in **43m47s**. The [run receipt](bench/Experiments/DeliveryDev29-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 92.121 us | 18.314 us | 2,153.138 us | 5.03x |
| `literal-alternate/sherlock-zh` | 148.932 us | 68.468 us | 2,125.742 us | 2.18x |
| `literal/sherlock-ru` | 179.925 us | 101.505 us | 5,190.840 us | 1.77x |
| `ruff-noqa/tweaked` | 10,099.800 us | 7,879.443 us | 41,181.200 us | 1.28x |
| `literal/sherlock-casei-en` | 151.867 us | 197.954 us | 619.870 us | 0.77x |
| `literal/sherlock-casei-ru` | 245.731 us | 358.335 us | 5,802.167 us | 0.69x |
| `literal/sherlock-en` | 56.202 us | 83.768 us | 459.735 us | 0.67x |
| `aws-keys/quick` | 5,026.980 us | 10,026.560 us | 43,787.800 us | 0.50x |
| `bounded-repeat/letters-en` | 1,035.533 us | 2,753.263 us | 2,883.272 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,653.874 us | 4,770.173 us | 10,212.300 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,953.050 us | 5,884.033 us | 6,659.100 us | 0.33x |
| `dictionary/single` | 8,139.429 us | 25,655.300 us | 25,562.950 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 184.006 us | 590.081 us | 1,023.670 us | 0.31x |
| `literal-alternate/sherlock-en` | 372.706 us | 1,465.814 us | 2,029.174 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 150.871 us | 656.651 us | 1,097.560 us | 0.23x |
| `bounded-repeat/capitals` | 9,343.683 us | 44,586.150 us | 51,428.400 us | 0.21x |
| `bounded-repeat/context` | 15,600.775 us | 146,369.300 us | 149,994.400 us | 0.11x |
| `aws-keys/full` | 8,723.220 us | 84,322.200 us | 116,487.600 us | 0.10x |
| `ruff-noqa/real` | 4,442.158 us | 68,473.800 us | 98,763.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,019.771 us | 22,636.967 us | 34,340.900 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 166.400 us | 11,524.120 us | 12,420.000 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,226.760 us | 483.315 us | 8,851.600 us | 2.54x |
| `common/match-word` | `Match` | 0.198 us | 0.163 us | 1.102 us | 1.21x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.106 us | 0.111 us | 0.149 us | 0.95x |
| `industry/leipzig-twain-count` | `Count` | 1,164.026 us | 1,355.735 us | 8,580.575 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,462.811 us | 1,881.704 us | 10,287.733 us | 0.78x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.083 us | 0.101 us | 0.61x |
| `common/uri-miss` | `IsMatch` | 0.115 us | 0.207 us | 0.225 us | 0.56x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.976 us | 255.231 us | 489.891 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.168 us | 0.186 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.077 us | 0.172 us | 0.191 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 4,029.738 us | 9,860.300 us | 16,500.500 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.189 us | 0.209 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.089 us | 0.232 us | 0.265 us | 0.39x |
| `common/replace-words` | `Replace` | 27.296 us | 75.007 us | 77.810 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.264 us | 0.287 us | 0.36x |
| `common/matches-word` | `Count` | 1.321 us | 4.201 us | 5.903 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.342 us | 0.362 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,329.041 us | 8,311.429 us | 16,448.400 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.106 us | 0.135 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.146 us | 967.217 us | 1,240.072 us | 0.18x |
| `common/split-words` | `Split` | 14.390 us | 79.123 us | 80.360 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.552 us | 0.584 us | 0.17x |
| `common/matches-words` | `Count` | 9.213 us | 68.966 us | 70.393 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.090 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.077 us | 0.604 us | 0.635 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.829 us | 0.863 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,387.360 us | 32,424.900 us | 46,607.800 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,893.874 us | 19,356.900 us | 20,188.867 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,864.937 us | 23,215.000 us | 30,743.150 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 498.045 us | 8,588.050 us | 9,125.867 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.659 us | 161.397 us | 165.384 us | 0.05x |
| `common/matches-set` | `Count` | 7.654 us | 196.779 us | 198.785 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 244.714 us | 11,582.400 us | 12,175.060 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 148.968 us | 35.206 us | 2,101.942 us | 4.23x |
| `literal/sherlock-zh` | 93.177 us | 23.521 us | 2,127.370 us | 3.96x |
| `ruff-noqa/tweaked` | 10,199.280 us | 4,773.233 us | 35,412.900 us | 2.14x |
| `literal/sherlock-ru` | 182.023 us | 90.003 us | 5,269.660 us | 2.02x |
| `literal/sherlock-casei-en` | 153.260 us | 77.984 us | 452.338 us | 1.97x |
| `aws-keys/quick` | 5,014.450 us | 3,660.507 us | 35,425.600 us | 1.37x |
| `literal/sherlock-casei-ru` | 243.278 us | 194.860 us | 5,494.750 us | 1.25x |
| `literal-alternate/sherlock-casei-en` | 166.350 us | 147.243 us | 528.673 us | 1.13x |
| `literal-alternate/sherlock-en` | 374.504 us | 342.851 us | 775.165 us | 1.09x |
| `literal-alternate/sherlock-en-mixed` | 185.106 us | 171.356 us | 561.797 us | 1.08x |
| `bounded-repeat/letters-ru` | 1,944.562 us | 2,017.273 us | 2,822.394 us | 0.96x |
| `literal/sherlock-en` | 56.054 us | 72.965 us | 403.788 us | 0.77x |
| `literal-alternate/sherlock-en-nomatch` | 150.790 us | 202.055 us | 567.041 us | 0.75x |
| `literal-alternate/sherlock-ru` | 1,630.938 us | 2,317.968 us | 7,479.800 us | 0.70x |
| `bounded-repeat/capitals` | 9,360.333 us | 13,407.900 us | 20,176.300 us | 0.70x |
| `bounded-repeat/letters-en` | 1,037.329 us | 1,829.782 us | 1,920.052 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 1,022.740 us | 1,947.926 us | 11,823.367 us | 0.53x |
| `aws-keys/full` | 9,492.720 us | 18,624.167 us | 50,916.700 us | 0.51x |
| `bounded-repeat/context` | 15,765.475 us | 67,971.700 us | 70,178.600 us | 0.23x |
| `ruff-noqa/real` | 4,500.667 us | 20,514.567 us | 52,657.800 us | 0.22x |
| `dictionary/single` | 8,082.186 us | 44,982.200 us | 44,806.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,234.348 us | 450.182 us | 7,621.933 us | 2.74x |
| `common/match-word` | `Match` | 0.195 us | 0.076 us | 0.973 us | 2.56x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.232 us | 61.045 us | 267.662 us | 2.20x |
| `industry/leipzig-river-window-count` | `Count` | 3,397.447 us | 1,575.394 us | 16,311.167 us | 2.16x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.043 us | 0.073 us | 2.15x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.106 us | 0.050 us | 0.087 us | 2.14x |
| `common/uri-miss` | `IsMatch` | 0.115 us | 0.067 us | 0.084 us | 1.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.049 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.048 us | 1.63x |
| `common/replace-words` | `Replace` | 27.265 us | 16.994 us | 19.488 us | 1.60x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.049 us | 0.068 us | 1.57x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.048 us | 0.069 us | 1.57x |
| `industry/leipzig-symbol-count` | `Count` | 1,878.715 us | 1,296.090 us | 6,973.333 us | 1.45x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.055 us | 0.077 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,061.031 us | 2,999.671 us | 8,253.433 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,446.109 us | 1,076.006 us | 8,361.400 us | 1.34x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.079 us | 0.107 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.088 us | 0.112 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.086 us | 0.118 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,167.173 us | 1,287.590 us | 6,199.175 us | 0.91x |
| `common/one-node-backtracking` | `IsMatch` | 0.077 us | 0.086 us | 0.126 us | 0.89x |
| `common/matches-words` | `Count` | 9.267 us | 10.747 us | 13.896 us | 0.86x |
| `industry/mariomka-ip-count` | `Count` | 2,338.259 us | 3,429.487 us | 11,053.500 us | 0.68x |
| `common/split-words` | `Split` | 14.328 us | 21.183 us | 23.035 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.070 us | 0.67x |
| `common/matches-word` | `Count` | 1.315 us | 2.333 us | 3.507 us | 0.56x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.200 us | 0.235 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.037 us | 0.051 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.274 us | 599.670 us | 848.133 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,884.837 us | 8,880.683 us | 9,785.840 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.298 us | 3,974.646 us | 4,399.000 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.730 us | 104.146 us | 105.456 us | 0.07x |
| `common/matches-set` | `Count` | 7.650 us | 104.475 us | 106.130 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 231.076 us | 4,698.927 us | 5,130.070 us | 0.05x |
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
| `lokad/imports/module-imports` | 12.211 us | 6.445 us | 29.110 us | 1.89x |
| `structural/keyword-to-generic-type` | 5.472 us | 4.065 us | 15.648 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.676 us | 1.366 us | 1.486 us | 1.23x |
| `lokad/lexer/doc-line` | 3.318 us | 2.922 us | 3.604 us | 1.14x |
| `lokad/lexer/identifier` | 23.880 us | 22.314 us | 25.536 us | 1.07x |
| `structural/ordered-keyword-pair` | 5.561 us | 5.378 us | 16.842 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.104 us | 7.178 us | 7.872 us | 0.99x |
| `fallback/lookahead` | 5.600 us | 5.979 us | 17.271 us | 0.94x |
| `lokad/langserv/identifier-validator` | 10.997 us | 12.312 us | 13.796 us | 0.89x |
| `lokad/lexer/operator-run` | 2.901 us | 3.336 us | 3.934 us | 0.87x |
| `lokad/style/hex-color` | 0.603 us | 0.762 us | 0.844 us | 0.79x |
| `literal-family/method-token-family` | 7.169 us | 9.361 us | 20.008 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.178 us | 2.951 us | 3.052 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 3.352 us | 4.826 us | 5.404 us | 0.69x |
| `literal/identifier-token` | 2.785 us | 4.360 us | 15.580 us | 0.64x |
| `literal/call-token` | 2.591 us | 4.321 us | 15.813 us | 0.60x |
| `lokad/lexer/number` | 8.291 us | 13.904 us | 15.118 us | 0.60x |
| `structural/ordered-identifier-window` | 16.034 us | 28.719 us | 39.700 us | 0.56x |
| `lokad/style/cell-ref` | 1.502 us | 2.844 us | 3.036 us | 0.53x |
| `structural/method-family-generic-call` | 3.423 us | 6.622 us | 17.864 us | 0.52x |
| `structural/method-family-call` | 3.856 us | 8.252 us | 19.089 us | 0.47x |
| `literal-family/type-token-family` | 10.564 us | 23.695 us | 34.570 us | 0.45x |
| `literal/identifier-token-casei` | 6.093 us | 15.053 us | 26.838 us | 0.40x |
| `lokad/style/range-ref` | 0.923 us | 2.712 us | 2.788 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.594 us | 17.143 us | 28.262 us | 0.33x |
| `structural/modifier-family-to-type-window` | 80.795 us | 258.802 us | 270.958 us | 0.31x |
| `structural/ordered-keyword-window` | 5.453 us | 17.558 us | 29.108 us | 0.31x |
| `lokad/lexer/string` | 21.990 us | 93.569 us | 99.464 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 23.425 us | 130.755 us | 142.922 us | 0.18x |
| `lokad/folding/region-marker` | 6.524 us | 36.898 us | 61.113 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.563 us | 24.537 us | 24.964 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.468 us | 218.364 us | 232.433 us | 0.08x |
| `lokad/langserv/url-download` | 2.666 us | 52.876 us | 53.374 us | 0.05x |
| `balancing/begin-end-blocks` | 5.714 us | 4,308.158 us | 4,322.408 us | 0.0013x |
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
| `lokad/imports/module-imports` | 12.221 us | 4.427 us | 25.957 us | 2.76x |
| `structural/keyword-family-to-capitalized-identifier` | 17.977 us | 7.646 us | 18.712 us | 2.35x |
| `structural/modifier-family-to-type-pair` | 23.461 us | 10.755 us | 21.568 us | 2.18x |
| `lokad/langserv/color-short-hex` | 1.658 us | 1.047 us | 1.210 us | 1.58x |
| `structural/keyword-to-generic-type` | 5.446 us | 3.503 us | 14.610 us | 1.55x |
| `lokad/lexer/doc-line` | 3.297 us | 2.129 us | 2.903 us | 1.55x |
| `structural/ordered-identifier-window` | 16.046 us | 10.630 us | 21.313 us | 1.51x |
| `lokad/langserv/helper-identifier` | 7.120 us | 4.724 us | 5.601 us | 1.51x |
| `lokad/lexer/operator-run` | 2.914 us | 1.985 us | 2.589 us | 1.47x |
| `structural/modifier-family-to-type-window` | 81.368 us | 56.387 us | 68.610 us | 1.44x |
| `lokad/lexer/identifier` | 23.703 us | 17.201 us | 20.414 us | 1.38x |
| `structural/ordered-keyword-pair` | 5.559 us | 4.156 us | 14.974 us | 1.34x |
| `fallback/lookahead` | 5.590 us | 4.229 us | 15.091 us | 1.32x |
| `lokad/docs/inline-doc-prefix` | 3.373 us | 2.687 us | 3.468 us | 1.26x |
| `lokad/langserv/identifier-validator` | 11.075 us | 9.107 us | 11.129 us | 1.22x |
| `lokad/style/hex-color` | 0.598 us | 0.506 us | 0.587 us | 1.18x |
| `lokad/langserv/color-rgb-hex` | 2.175 us | 1.865 us | 2.029 us | 1.17x |
| `lokad/lexer/number` | 8.256 us | 7.265 us | 8.592 us | 1.14x |
| `literal/identifier-token-casei` | 6.128 us | 5.897 us | 17.493 us | 1.04x |
| `literal-family/type-token-family` | 10.522 us | 11.568 us | 22.234 us | 0.91x |
| `lokad/style/cell-ref` | 1.508 us | 1.771 us | 1.942 us | 0.85x |
| `structural/method-family-call` | 3.844 us | 5.039 us | 15.521 us | 0.76x |
| `structural/method-family-generic-call` | 3.434 us | 4.521 us | 15.001 us | 0.76x |
| `literal-family/method-token-family` | 3.893 us | 5.348 us | 15.788 us | 0.73x |
| `literal/call-token` | 2.591 us | 3.938 us | 14.882 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.607 us | 8.711 us | 19.701 us | 0.64x |
| `structural/ordered-keyword-window` | 5.426 us | 8.549 us | 19.661 us | 0.63x |
| `lokad/style/range-ref` | 0.916 us | 1.506 us | 1.582 us | 0.61x |
| `literal/identifier-token` | 2.800 us | 4.718 us | 15.350 us | 0.59x |
| `lokad/lexer/string` | 21.937 us | 45.554 us | 48.868 us | 0.48x |
| `lokad/langserv/url-dashboard` | 2.553 us | 7.479 us | 8.080 us | 0.34x |
| `lokad/langserv/url-download` | 2.662 us | 12.995 us | 13.508 us | 0.20x |
| `lokad/folding/region-marker` | 6.538 us | 36.610 us | 59.760 us | 0.18x |
| `balancing/begin-end-blocks` | 5.708 us | 467.029 us | 496.944 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
