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

The tables were refreshed on **3 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **44m12s**. The [run receipt](bench/Experiments/DeliveryDev18-20261103/full-refresh.json)
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
| `literal/sherlock-zh` | 134.700 us | 18.948 us | 2,168.852 us | 7.11x |
| `literal-alternate/sherlock-zh` | 248.723 us | 69.428 us | 2,196.122 us | 3.58x |
| `literal/sherlock-ru` | 253.384 us | 98.980 us | 5,304.350 us | 2.56x |
| `ruff-noqa/tweaked` | 10,626.740 us | 7,953.514 us | 41,518.400 us | 1.34x |
| `literal/sherlock-casei-en` | 171.497 us | 197.568 us | 616.881 us | 0.87x |
| `literal/sherlock-en` | 70.572 us | 85.946 us | 449.512 us | 0.82x |
| `literal/sherlock-casei-ru` | 249.191 us | 396.414 us | 5,843.300 us | 0.63x |
| `aws-keys/quick` | 5,045.220 us | 9,666.400 us | 44,728.700 us | 0.52x |
| `bounded-repeat/letters-en` | 1,058.977 us | 2,858.106 us | 2,963.871 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,747.503 us | 4,908.330 us | 10,086.500 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,969.488 us | 6,001.689 us | 6,800.350 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 197.406 us | 622.440 us | 1,055.871 us | 0.32x |
| `dictionary/single` | 8,215.386 us | 26,095.200 us | 26,110.800 us | 0.31x |
| `literal-alternate/sherlock-en` | 402.374 us | 1,550.821 us | 2,105.900 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 167.983 us | 669.979 us | 1,114.673 us | 0.25x |
| `bounded-repeat/capitals` | 9,583.217 us | 45,751.650 us | 53,939.200 us | 0.21x |
| `aws-keys/full` | 10,817.420 us | 87,885.400 us | 120,333.500 us | 0.12x |
| `bounded-repeat/context` | 15,652.025 us | 148,082.600 us | 150,817.700 us | 0.11x |
| `ruff-noqa/real` | 4,596.318 us | 74,947.300 us | 90,092.700 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,098.091 us | 23,655.933 us | 35,828.300 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 223.959 us | 11,578.560 us | 12,383.960 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,541.642 us | 493.351 us | 9,023.800 us | 3.12x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.179 us | 0.111 us | 0.149 us | 1.62x |
| `common/match-word` | `Match` | 0.200 us | 0.169 us | 1.151 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,262.537 us | 1,349.110 us | 8,394.583 us | 0.94x |
| `industry/mariomka-uri-count` | `Count` | 1,761.217 us | 1,943.093 us | 10,441.000 us | 0.91x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 242.234 us | 270.983 us | 537.184 us | 0.89x |
| `common/replace-words` | `Replace` | 69.208 us | 80.292 us | 82.204 us | 0.86x |
| `common/uri-miss` | `IsMatch` | 0.156 us | 0.191 us | 0.209 us | 0.82x |
| `common/uri-match` | `IsMatch` | 0.166 us | 0.232 us | 0.268 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `common/ip-match` | `IsMatch` | 0.105 us | 0.179 us | 0.201 us | 0.59x |
| `common/email-match` | `IsMatch` | 0.150 us | 0.271 us | 0.298 us | 0.55x |
| `common/ip-miss` | `IsMatch` | 0.105 us | 0.193 us | 0.213 us | 0.55x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.169 us | 0.187 us | 0.47x |
| `common/email-miss` | `IsMatch` | 0.154 us | 0.348 us | 0.373 us | 0.44x |
| `industry/leipzig-name-family-count` | `Count` | 4,215.917 us | 9,912.350 us | 17,291.750 us | 0.43x |
| `industry/mariomka-ip-count` | `Count` | 2,700.347 us | 8,324.800 us | 16,765.550 us | 0.32x |
| `common/matches-word` | `Count` | 1.366 us | 4.320 us | 6.049 us | 0.32x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.138 us | 0.27x |
| `common/one-node-backtracking` | `IsMatch` | 0.161 us | 0.620 us | 0.656 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.098 us | 0.20x |
| `common/backtracking` | `IsMatch` | 0.166 us | 0.848 us | 0.889 us | 0.20x |
| `common/split-words` | `Split` | 14.716 us | 77.736 us | 79.069 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.772 us | 966.925 us | 1,260.179 us | 0.18x |
| `industry/leipzig-river-window-count` | `Count` | 5,669.367 us | 34,158.850 us | 47,074.100 us | 0.17x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.561 us | 0.593 us | 0.16x |
| `common/matches-words` | `Count` | 9.451 us | 73.147 us | 75.021 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.091 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,210.071 us | 19,850.600 us | 20,700.550 us | 0.11x |
| `industry/leipzig-symbol-count` | `Count` | 1,872.211 us | 22,910.433 us | 33,955.400 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 527.688 us | 8,620.700 us | 9,130.017 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.855 us | 170.121 us | 172.747 us | 0.05x |
| `common/matches-set` | `Count` | 7.907 us | 195.100 us | 195.744 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 267.992 us | 12,490.980 us | 12,923.225 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 238.500 us | 36.380 us | 2,225.671 us | 6.56x |
| `literal/sherlock-zh` | 134.437 us | 24.140 us | 2,178.912 us | 5.57x |
| `literal/sherlock-ru` | 260.732 us | 90.187 us | 5,311.820 us | 2.89x |
| `ruff-noqa/tweaked` | 11,198.260 us | 4,550.833 us | 36,217.650 us | 2.46x |
| `literal/sherlock-casei-en` | 171.764 us | 79.846 us | 450.794 us | 2.15x |
| `literal-alternate/sherlock-casei-en` | 223.476 us | 148.501 us | 561.385 us | 1.50x |
| `aws-keys/quick` | 5,048.240 us | 3,607.550 us | 36,713.150 us | 1.40x |
| `literal/sherlock-casei-ru` | 247.528 us | 223.901 us | 5,567.190 us | 1.11x |
| `literal-alternate/sherlock-en-mixed` | 196.115 us | 177.660 us | 579.513 us | 1.10x |
| `literal-alternate/sherlock-en` | 402.578 us | 377.906 us | 789.490 us | 1.07x |
| `bounded-repeat/letters-ru` | 1,964.527 us | 2,073.352 us | 2,875.372 us | 0.95x |
| `literal/sherlock-en` | 70.483 us | 75.939 us | 419.855 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 167.811 us | 201.779 us | 612.676 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,763.207 us | 2,366.259 us | 7,515.367 us | 0.75x |
| `bounded-repeat/capitals` | 9,613.733 us | 13,700.625 us | 21,324.600 us | 0.70x |
| `aws-keys/full` | 10,806.580 us | 18,915.767 us | 52,725.000 us | 0.57x |
| `bounded-repeat/letters-en` | 1,055.500 us | 1,852.354 us | 1,933.242 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 1,083.523 us | 1,983.215 us | 9,645.000 us | 0.55x |
| `bounded-repeat/context` | 15,679.900 us | 69,427.100 us | 72,391.100 us | 0.23x |
| `ruff-noqa/real` | 4,579.518 us | 21,746.200 us | 33,786.350 us | 0.21x |
| `dictionary/single` | 8,225.671 us | 46,184.400 us | 46,196.950 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/rust-sherlock-holmes-window-count` | `Count` | 242.741 us | 59.352 us | 276.742 us | 4.09x |
| `common/backtracking` | `IsMatch` | 0.166 us | 0.043 us | 0.074 us | 3.85x |
| `industry/leipzig-river-window-count` | `Count` | 5,712.389 us | 1,562.009 us | 15,900.467 us | 3.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.178 us | 0.049 us | 0.089 us | 3.64x |
| `common/replace-words` | `Replace` | 69.175 us | 19.755 us | 21.645 us | 3.50x |
| `industry/mariomka-email-count` | `Count` | 1,537.497 us | 456.822 us | 7,804.400 us | 3.37x |
| `common/match-word` | `Match` | 0.200 us | 0.080 us | 0.999 us | 2.51x |
| `common/uri-miss` | `IsMatch` | 0.158 us | 0.067 us | 0.087 us | 2.35x |
| `common/ip-match` | `IsMatch` | 0.105 us | 0.049 us | 0.070 us | 2.16x |
| `common/ip-miss` | `IsMatch` | 0.105 us | 0.051 us | 0.070 us | 2.07x |
| `common/uri-match` | `IsMatch` | 0.165 us | 0.089 us | 0.120 us | 1.85x |
| `common/email-match` | `IsMatch` | 0.150 us | 0.081 us | 0.109 us | 1.84x |
| `common/one-node-backtracking` | `IsMatch` | 0.162 us | 0.089 us | 0.127 us | 1.82x |
| `common/email-miss` | `IsMatch` | 0.155 us | 0.089 us | 0.115 us | 1.74x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.029 us | 0.046 us | 1.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.049 us | 1.64x |
| `industry/mariomka-uri-count` | `Count` | 1,761.228 us | 1,108.033 us | 8,519.967 us | 1.59x |
| `industry/leipzig-symbol-count` | `Count` | 1,884.400 us | 1,336.838 us | 11,352.367 us | 1.41x |
| `industry/leipzig-name-family-count` | `Count` | 4,212.517 us | 3,048.565 us | 8,443.967 us | 1.38x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.057 us | 0.078 us | 1.37x |
| `industry/leipzig-twain-count` | `Count` | 1,261.950 us | 1,280.445 us | 7,123.214 us | 0.99x |
| `common/matches-words` | `Count` | 9.450 us | 10.802 us | 13.946 us | 0.87x |
| `industry/mariomka-ip-count` | `Count` | 2,703.926 us | 3,534.660 us | 11,183.100 us | 0.76x |
| `common/split-words` | `Split` | 14.676 us | 20.838 us | 22.716 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `common/matches-word` | `Count` | 1.350 us | 2.369 us | 3.633 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.202 us | 0.242 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.052 us | 0.28x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 528.162 us | 3,998.631 us | 4,426.325 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,215.655 us | 17,686.500 us | 18,613.000 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.599 us | 1,496.215 us | 1,818.461 us | 0.12x |
| `common/matches-set` | `Count` | 7.901 us | 105.776 us | 107.578 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.881 us | 106.013 us | 107.505 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 268.642 us | 4,824.364 us | 5,252.270 us | 0.06x |
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
| `lokad/imports/module-imports` | 31.909 us | 6.486 us | 28.955 us | 4.92x |
| `structural/keyword-to-generic-type` | 5.289 us | 4.115 us | 16.063 us | 1.29x |
| `lokad/langserv/color-short-hex` | 1.695 us | 1.364 us | 1.494 us | 1.24x |
| `lokad/lexer/doc-line` | 3.385 us | 3.053 us | 3.782 us | 1.11x |
| `lokad/lexer/identifier` | 23.954 us | 22.816 us | 25.928 us | 1.05x |
| `structural/ordered-keyword-pair` | 5.609 us | 5.414 us | 16.908 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.210 us | 7.130 us | 7.906 us | 1.01x |
| `lokad/lexer/operator-run` | 3.204 us | 3.456 us | 3.967 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.422 us | 12.459 us | 14.252 us | 0.92x |
| `lokad/style/hex-color` | 0.730 us | 0.807 us | 0.876 us | 0.90x |
| `fallback/lookahead` | 5.482 us | 6.303 us | 17.487 us | 0.87x |
| `literal-family/method-token-family` | 7.209 us | 9.280 us | 20.451 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 2.055 us | 2.956 us | 3.079 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.404 us | 5.204 us | 5.745 us | 0.65x |
| `literal/identifier-token` | 2.793 us | 4.383 us | 15.815 us | 0.64x |
| `lokad/lexer/number` | 8.813 us | 14.321 us | 15.632 us | 0.62x |
| `literal/call-token` | 2.621 us | 4.433 us | 16.022 us | 0.59x |
| `lokad/style/cell-ref` | 1.504 us | 2.838 us | 3.088 us | 0.53x |
| `structural/method-family-generic-call` | 3.473 us | 6.765 us | 17.867 us | 0.51x |
| `structural/method-family-call` | 3.865 us | 8.155 us | 19.020 us | 0.47x |
| `literal-family/type-token-family` | 10.592 us | 23.887 us | 34.812 us | 0.44x |
| `literal/identifier-token-casei` | 5.721 us | 15.598 us | 26.586 us | 0.37x |
| `lokad/style/range-ref` | 0.913 us | 2.811 us | 2.853 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.685 us | 17.750 us | 28.687 us | 0.32x |
| `structural/ordered-keyword-window` | 5.512 us | 17.859 us | 29.260 us | 0.31x |
| `structural/modifier-family-to-type-window` | 75.117 us | 257.615 us | 268.826 us | 0.29x |
| `lokad/lexer/string` | 25.217 us | 95.684 us | 101.488 us | 0.26x |
| `lokad/folding/region-marker` | 6.865 us | 37.200 us | 61.912 us | 0.18x |
| `structural/modifier-family-to-type-pair` | 23.795 us | 135.901 us | 148.160 us | 0.18x |
| `structural/ordered-identifier-window` | 5.018 us | 29.172 us | 40.913 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.533 us | 24.735 us | 24.970 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.343 us | 224.628 us | 237.403 us | 0.08x |
| `lokad/langserv/url-download` | 2.689 us | 53.907 us | 54.039 us | 0.05x |
| `balancing/begin-end-blocks` | 7.458 us | 4,441.242 us | 4,486.783 us | 0.0017x |
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
| `lokad/imports/module-imports` | 31.731 us | 4.450 us | 26.080 us | 7.13x |
| `structural/keyword-family-to-capitalized-identifier` | 18.036 us | 7.737 us | 18.746 us | 2.33x |
| `structural/modifier-family-to-type-pair` | 23.770 us | 10.747 us | 22.459 us | 2.21x |
| `lokad/langserv/color-short-hex` | 1.688 us | 1.031 us | 1.200 us | 1.64x |
| `lokad/lexer/operator-run` | 3.208 us | 2.091 us | 2.687 us | 1.53x |
| `structural/keyword-to-generic-type` | 5.302 us | 3.485 us | 14.945 us | 1.52x |
| `lokad/langserv/helper-identifier` | 7.213 us | 4.888 us | 5.848 us | 1.48x |
| `lokad/lexer/doc-line` | 3.360 us | 2.341 us | 2.996 us | 1.44x |
| `lokad/lexer/identifier` | 23.979 us | 17.540 us | 21.104 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.605 us | 4.180 us | 15.072 us | 1.34x |
| `lokad/style/hex-color` | 0.732 us | 0.550 us | 0.624 us | 1.33x |
| `structural/modifier-family-to-type-window` | 75.519 us | 57.105 us | 69.032 us | 1.32x |
| `fallback/lookahead` | 5.501 us | 4.248 us | 15.192 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.414 us | 9.439 us | 11.253 us | 1.21x |
| `lokad/docs/inline-doc-prefix` | 3.382 us | 2.864 us | 3.605 us | 1.18x |
| `lokad/lexer/number` | 8.824 us | 7.657 us | 8.781 us | 1.15x |
| `lokad/langserv/color-rgb-hex` | 2.047 us | 1.875 us | 2.126 us | 1.09x |
| `literal/identifier-token-casei` | 5.732 us | 5.930 us | 17.299 us | 0.97x |
| `literal-family/type-token-family` | 10.578 us | 11.573 us | 22.280 us | 0.91x |
| `lokad/style/cell-ref` | 1.505 us | 1.779 us | 2.000 us | 0.85x |
| `structural/method-family-generic-call` | 3.454 us | 4.534 us | 15.040 us | 0.76x |
| `structural/method-family-call` | 3.872 us | 5.102 us | 15.494 us | 0.76x |
| `literal-family/method-token-family` | 3.929 us | 5.357 us | 16.122 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.688 us | 8.668 us | 19.542 us | 0.66x |
| `literal/call-token` | 2.623 us | 3.999 us | 14.999 us | 0.66x |
| `structural/ordered-keyword-window` | 5.520 us | 8.699 us | 19.653 us | 0.63x |
| `literal/identifier-token` | 2.802 us | 4.769 us | 15.637 us | 0.59x |
| `lokad/style/range-ref` | 0.912 us | 1.589 us | 1.647 us | 0.57x |
| `lokad/lexer/string` | 25.136 us | 46.494 us | 49.884 us | 0.54x |
| `structural/ordered-identifier-window` | 5.016 us | 10.755 us | 22.237 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.542 us | 7.501 us | 8.115 us | 0.34x |
| `lokad/langserv/url-download` | 2.675 us | 13.093 us | 13.571 us | 0.20x |
| `lokad/folding/region-marker` | 6.884 us | 37.469 us | 60.595 us | 0.18x |
| `balancing/begin-end-blocks` | 7.455 us | 471.235 us | 497.746 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
