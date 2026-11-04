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
and resource checks passed in **44m28s**. The [run receipt](bench/Experiments/DeliveryDev27-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 94.407 us | 18.579 us | 2,143.257 us | 5.08x |
| `literal-alternate/sherlock-zh` | 152.021 us | 68.021 us | 2,155.343 us | 2.23x |
| `literal/sherlock-ru` | 200.720 us | 100.976 us | 5,446.490 us | 1.99x |
| `ruff-noqa/tweaked` | 10,074.960 us | 8,057.629 us | 42,556.450 us | 1.25x |
| `literal/sherlock-casei-en` | 155.035 us | 188.446 us | 606.654 us | 0.82x |
| `aws-keys/quick` | 6,366.089 us | 9,704.740 us | 42,549.450 us | 0.66x |
| `literal/sherlock-en` | 56.900 us | 88.278 us | 473.841 us | 0.64x |
| `literal/sherlock-casei-ru` | 243.272 us | 385.929 us | 5,602.100 us | 0.63x |
| `bounded-repeat/letters-en` | 1,025.448 us | 2,776.111 us | 2,895.583 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,682.297 us | 4,832.082 us | 10,092.967 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,934.063 us | 5,916.389 us | 6,746.850 us | 0.33x |
| `dictionary/single` | 8,156.743 us | 25,313.050 us | 25,585.150 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 189.825 us | 593.518 us | 1,031.647 us | 0.32x |
| `literal-alternate/sherlock-en` | 393.880 us | 1,532.184 us | 2,047.669 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 154.361 us | 666.801 us | 1,104.441 us | 0.23x |
| `bounded-repeat/capitals` | 9,351.417 us | 45,312.100 us | 52,394.400 us | 0.21x |
| `aws-keys/full` | 10,595.860 us | 84,425.900 us | 116,058.600 us | 0.13x |
| `bounded-repeat/context` | 15,951.900 us | 146,692.900 us | 150,067.500 us | 0.11x |
| `ruff-noqa/real` | 4,329.942 us | 71,320.200 us | 99,376.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,050.153 us | 22,750.767 us | 27,984.150 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 171.789 us | 11,190.920 us | 11,937.500 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,226.745 us | 483.059 us | 8,849.767 us | 2.54x |
| `common/match-word` | `Match` | 0.193 us | 0.168 us | 1.135 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,160.180 us | 1,329.758 us | 8,809.967 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,461.891 us | 1,919.554 us | 10,316.233 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.108 us | 0.147 us | 0.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.083 us | 0.099 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.114 us | 0.190 us | 0.208 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.773 us | 266.160 us | 517.212 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.170 us | 0.186 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.169 us | 0.191 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 4,055.458 us | 9,856.317 us | 17,406.200 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.184 us | 0.206 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.233 us | 0.265 us | 0.39x |
| `common/replace-words` | `Replace` | 27.496 us | 76.909 us | 78.652 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.270 us | 0.295 us | 0.35x |
| `common/matches-word` | `Count` | 1.339 us | 4.255 us | 5.932 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.345 us | 0.367 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,330.205 us | 8,399.967 us | 16,849.550 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.108 us | 0.137 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.081 us | 0.097 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.944 us | 958.809 us | 1,279.123 us | 0.18x |
| `common/split-words` | `Split` | 14.415 us | 81.116 us | 82.781 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.549 us | 0.582 us | 0.17x |
| `common/matches-words` | `Count` | 9.319 us | 71.026 us | 71.862 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.613 us | 0.642 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.089 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.091 us | 0.823 us | 0.865 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,035.084 us | 19,278.150 us | 20,196.833 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,361.656 us | 31,918.150 us | 45,870.800 us | 0.11x |
| `industry/leipzig-symbol-count` | `Count` | 1,849.764 us | 22,926.267 us | 30,253.550 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 494.017 us | 8,346.271 us | 8,767.600 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.891 us | 162.238 us | 162.135 us | 0.04x |
| `common/matches-set` | `Count` | 7.603 us | 190.206 us | 192.421 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.700 us | 11,928.020 us | 12,478.700 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 151.468 us | 35.684 us | 2,112.716 us | 4.24x |
| `literal/sherlock-zh` | 94.334 us | 23.706 us | 2,150.404 us | 3.98x |
| `ruff-noqa/tweaked` | 10,321.760 us | 4,585.900 us | 37,116.050 us | 2.25x |
| `literal/sherlock-ru` | 197.814 us | 89.884 us | 5,446.980 us | 2.20x |
| `literal/sherlock-casei-en` | 155.027 us | 81.451 us | 427.780 us | 1.90x |
| `aws-keys/quick` | 6,353.750 us | 3,543.147 us | 34,537.200 us | 1.79x |
| `literal/sherlock-casei-ru` | 242.651 us | 193.655 us | 5,445.144 us | 1.25x |
| `literal-alternate/sherlock-casei-en` | 172.484 us | 146.762 us | 570.623 us | 1.18x |
| `literal-alternate/sherlock-en` | 393.947 us | 368.807 us | 793.734 us | 1.07x |
| `literal-alternate/sherlock-en-mixed` | 186.539 us | 175.827 us | 542.372 us | 1.06x |
| `bounded-repeat/letters-ru` | 1,942.000 us | 2,041.232 us | 2,857.722 us | 0.95x |
| `literal-alternate/sherlock-en-nomatch` | 154.682 us | 201.175 us | 579.306 us | 0.77x |
| `literal/sherlock-en` | 56.593 us | 77.469 us | 438.955 us | 0.73x |
| `bounded-repeat/capitals` | 9,350.650 us | 13,435.825 us | 20,439.500 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,623.128 us | 2,340.873 us | 7,455.800 us | 0.69x |
| `aws-keys/full` | 10,588.920 us | 18,456.633 us | 50,697.500 us | 0.57x |
| `bounded-repeat/letters-en` | 1,020.046 us | 1,813.475 us | 1,921.989 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,056.565 us | 1,955.692 us | 7,122.833 us | 0.54x |
| `bounded-repeat/context` | 16,027.775 us | 68,848.000 us | 70,713.100 us | 0.23x |
| `ruff-noqa/real` | 4,328.967 us | 20,538.400 us | 50,740.250 us | 0.21x |
| `dictionary/single` | 8,174.700 us | 45,256.900 us | 45,165.100 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,214.190 us | 453.813 us | 7,758.700 us | 2.68x |
| `common/match-word` | `Match` | 0.194 us | 0.077 us | 0.977 us | 2.52x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.550 us | 60.796 us | 271.059 us | 2.23x |
| `industry/leipzig-river-window-count` | `Count` | 3,369.847 us | 1,569.470 us | 13,954.567 us | 2.15x |
| `common/backtracking` | `IsMatch` | 0.091 us | 0.044 us | 0.075 us | 2.08x |
| `common/uri-miss` | `IsMatch` | 0.114 us | 0.066 us | 0.084 us | 1.73x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.029 us | 0.048 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.030 us | 0.048 us | 1.68x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.048 us | 0.086 us | 1.66x |
| `common/replace-words` | `Replace` | 27.604 us | 17.010 us | 18.405 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.047 us | 0.068 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.049 us | 0.069 us | 1.52x |
| `industry/leipzig-symbol-count` | `Count` | 1,879.218 us | 1,266.870 us | 6,886.433 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.055 us | 0.076 us | 1.36x |
| `industry/leipzig-name-family-count` | `Count` | 4,174.492 us | 3,108.024 us | 8,523.733 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,471.628 us | 1,096.717 us | 8,490.200 us | 1.34x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.080 us | 0.106 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.115 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.088 us | 0.118 us | 1.02x |
| `industry/leipzig-twain-count` | `Count` | 1,158.540 us | 1,299.718 us | 5,562.367 us | 0.89x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.088 us | 0.128 us | 0.88x |
| `common/matches-words` | `Count` | 9.190 us | 10.902 us | 13.917 us | 0.84x |
| `common/split-words` | `Split` | 14.447 us | 21.320 us | 23.033 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,337.818 us | 3,459.487 us | 11,045.400 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-word` | `Count` | 1.326 us | 2.334 us | 3.562 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.200 us | 0.233 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.052 us | 0.29x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 496.464 us | 3,939.285 us | 4,341.792 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.204 us | 1,489.950 us | 1,833.183 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,025.692 us | 17,367.250 us | 18,311.167 us | 0.12x |
| `common/matches-set` | `Count` | 7.609 us | 104.768 us | 105.348 us | 0.07x |
| `common/matches-boundary` | `Count` | 6.958 us | 103.786 us | 105.541 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 245.296 us | 4,751.264 us | 5,198.130 us | 0.05x |
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
| `lokad/imports/module-imports` | 13.798 us | 6.447 us | 29.451 us | 2.14x |
| `structural/keyword-to-generic-type` | 5.448 us | 4.082 us | 16.028 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.666 us | 1.364 us | 1.473 us | 1.22x |
| `lokad/lexer/doc-line` | 3.280 us | 2.988 us | 3.700 us | 1.10x |
| `lokad/lexer/identifier` | 23.517 us | 22.400 us | 25.900 us | 1.05x |
| `structural/ordered-keyword-pair` | 5.325 us | 5.262 us | 16.993 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.170 us | 7.109 us | 7.842 us | 1.01x |
| `lokad/langserv/identifier-validator` | 11.442 us | 12.331 us | 13.965 us | 0.93x |
| `fallback/lookahead` | 5.351 us | 6.067 us | 17.330 us | 0.88x |
| `lokad/lexer/operator-run` | 2.904 us | 3.293 us | 3.830 us | 0.88x |
| `lokad/style/hex-color` | 0.592 us | 0.778 us | 0.862 us | 0.76x |
| `literal-family/method-token-family` | 6.961 us | 9.221 us | 19.767 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 3.316 us | 4.777 us | 5.496 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.029 us | 2.956 us | 3.032 us | 0.69x |
| `lokad/style/cell-ref` | 1.838 us | 2.845 us | 2.953 us | 0.65x |
| `literal/identifier-token` | 2.744 us | 4.305 us | 15.477 us | 0.64x |
| `literal/call-token` | 2.559 us | 4.221 us | 15.383 us | 0.61x |
| `lokad/lexer/number` | 8.282 us | 13.695 us | 15.345 us | 0.60x |
| `structural/ordered-identifier-window` | 16.074 us | 28.254 us | 39.585 us | 0.57x |
| `structural/method-family-generic-call` | 3.441 us | 6.692 us | 17.880 us | 0.51x |
| `structural/method-family-call` | 3.851 us | 8.348 us | 19.112 us | 0.46x |
| `literal-family/type-token-family` | 10.387 us | 23.476 us | 34.711 us | 0.44x |
| `literal/identifier-token-casei` | 5.762 us | 15.356 us | 26.725 us | 0.38x |
| `structural/ordered-modifier-pair` | 6.039 us | 17.329 us | 28.425 us | 0.35x |
| `lokad/style/range-ref` | 0.910 us | 2.707 us | 2.689 us | 0.34x |
| `structural/modifier-family-to-type-window` | 78.256 us | 252.306 us | 268.673 us | 0.31x |
| `structural/ordered-keyword-window` | 5.402 us | 17.890 us | 28.991 us | 0.30x |
| `lokad/lexer/string` | 22.661 us | 95.224 us | 99.543 us | 0.24x |
| `lokad/folding/region-marker` | 6.374 us | 33.540 us | 60.704 us | 0.19x |
| `structural/modifier-family-to-type-pair` | 23.188 us | 131.190 us | 144.259 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.512 us | 24.473 us | 24.804 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.794 us | 219.686 us | 232.896 us | 0.09x |
| `lokad/langserv/url-download` | 2.577 us | 52.901 us | 53.737 us | 0.05x |
| `balancing/begin-end-blocks` | 5.636 us | 4,714.573 us | 4,699.200 us | 0.0012x |
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
| `lokad/imports/module-imports` | 13.821 us | 4.433 us | 26.761 us | 3.12x |
| `structural/keyword-family-to-capitalized-identifier` | 18.456 us | 7.612 us | 18.675 us | 2.42x |
| `structural/modifier-family-to-type-pair` | 23.146 us | 10.599 us | 21.808 us | 2.18x |
| `lokad/langserv/color-short-hex` | 1.668 us | 1.013 us | 1.172 us | 1.65x |
| `structural/keyword-to-generic-type` | 5.497 us | 3.417 us | 14.814 us | 1.61x |
| `lokad/langserv/helper-identifier` | 7.166 us | 4.695 us | 5.692 us | 1.53x |
| `structural/ordered-identifier-window` | 15.919 us | 10.681 us | 21.235 us | 1.49x |
| `lokad/lexer/doc-line` | 3.286 us | 2.209 us | 2.914 us | 1.49x |
| `lokad/lexer/operator-run` | 2.914 us | 2.006 us | 2.598 us | 1.45x |
| `structural/modifier-family-to-type-window` | 78.505 us | 55.807 us | 68.058 us | 1.41x |
| `lokad/lexer/identifier` | 23.622 us | 16.833 us | 20.439 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.299 us | 4.115 us | 15.141 us | 1.29x |
| `fallback/lookahead` | 5.377 us | 4.193 us | 15.134 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.287 us | 9.370 us | 11.392 us | 1.20x |
| `lokad/docs/inline-doc-prefix` | 3.324 us | 2.772 us | 3.493 us | 1.20x |
| `lokad/lexer/number` | 8.265 us | 7.284 us | 8.677 us | 1.13x |
| `lokad/style/hex-color` | 0.597 us | 0.527 us | 0.598 us | 1.13x |
| `lokad/style/cell-ref` | 1.857 us | 1.756 us | 1.945 us | 1.06x |
| `lokad/langserv/color-rgb-hex` | 2.016 us | 1.924 us | 2.068 us | 1.05x |
| `literal/identifier-token-casei` | 5.726 us | 5.825 us | 17.248 us | 0.98x |
| `literal-family/type-token-family` | 10.364 us | 11.365 us | 22.169 us | 0.91x |
| `structural/method-family-generic-call` | 3.446 us | 4.492 us | 15.019 us | 0.77x |
| `structural/method-family-call` | 3.854 us | 5.076 us | 15.722 us | 0.76x |
| `literal-family/method-token-family` | 3.876 us | 5.249 us | 15.637 us | 0.74x |
| `structural/ordered-modifier-pair` | 6.057 us | 8.601 us | 19.497 us | 0.70x |
| `literal/call-token` | 2.569 us | 3.912 us | 14.516 us | 0.66x |
| `structural/ordered-keyword-window` | 5.441 us | 8.580 us | 19.402 us | 0.63x |
| `lokad/style/range-ref` | 0.904 us | 1.484 us | 1.542 us | 0.61x |
| `literal/identifier-token` | 2.761 us | 4.684 us | 14.976 us | 0.59x |
| `lokad/lexer/string` | 22.880 us | 46.382 us | 48.411 us | 0.49x |
| `lokad/langserv/url-dashboard` | 2.505 us | 7.436 us | 7.904 us | 0.34x |
| `lokad/langserv/url-download` | 2.585 us | 12.830 us | 13.381 us | 0.20x |
| `lokad/folding/region-marker` | 6.397 us | 35.215 us | 59.758 us | 0.18x |
| `balancing/begin-end-blocks` | 5.635 us | 463.832 us | 491.839 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
