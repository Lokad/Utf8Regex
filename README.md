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
and resource checks passed in **43m08s**. The [run receipt](bench/Experiments/DeliveryDev19-20261103/full-refresh.json)
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
| `literal/sherlock-zh` | 134.098 us | 19.024 us | 2,206.442 us | 7.05x |
| `literal-alternate/sherlock-zh` | 237.932 us | 70.513 us | 2,186.235 us | 3.37x |
| `literal/sherlock-ru` | 255.208 us | 98.908 us | 5,221.210 us | 2.58x |
| `ruff-noqa/tweaked` | 10,590.400 us | 8,110.000 us | 42,001.200 us | 1.31x |
| `literal/sherlock-casei-en` | 169.241 us | 197.837 us | 601.351 us | 0.86x |
| `literal/sherlock-en` | 70.225 us | 89.039 us | 460.725 us | 0.79x |
| `literal/sherlock-casei-ru` | 256.704 us | 392.734 us | 5,612.533 us | 0.65x |
| `aws-keys/quick` | 5,003.500 us | 9,766.540 us | 43,096.450 us | 0.51x |
| `bounded-repeat/letters-en` | 1,065.740 us | 2,836.244 us | 2,935.876 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,742.066 us | 4,887.145 us | 10,120.433 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,953.312 us | 6,023.844 us | 6,869.363 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 201.835 us | 624.870 us | 1,063.994 us | 0.32x |
| `dictionary/single` | 8,206.457 us | 26,571.100 us | 26,587.650 us | 0.31x |
| `literal-alternate/sherlock-en` | 414.996 us | 1,574.450 us | 2,121.715 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 168.678 us | 670.212 us | 1,103.704 us | 0.25x |
| `bounded-repeat/capitals` | 9,530.017 us | 47,075.050 us | 53,528.500 us | 0.20x |
| `bounded-repeat/context` | 15,576.650 us | 149,394.300 us | 152,778.600 us | 0.10x |
| `aws-keys/full` | 9,018.433 us | 86,604.200 us | 116,743.300 us | 0.10x |
| `ruff-noqa/real` | 4,588.200 us | 74,607.500 us | 90,930.000 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,105.009 us | 23,379.800 us | 28,703.000 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 231.108 us | 11,197.780 us | 12,010.960 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,527.764 us | 494.180 us | 9,019.900 us | 3.09x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.177 us | 0.111 us | 0.147 us | 1.59x |
| `common/match-word` | `Match` | 0.201 us | 0.164 us | 1.169 us | 1.22x |
| `industry/leipzig-twain-count` | `Count` | 1,262.915 us | 1,350.145 us | 8,669.850 us | 0.94x |
| `industry/mariomka-uri-count` | `Count` | 1,749.897 us | 1,904.770 us | 10,476.300 us | 0.92x |
| `common/uri-miss` | `IsMatch` | 0.159 us | 0.206 us | 0.226 us | 0.77x |
| `common/replace-words` | `Replace` | 101.078 us | 136.556 us | 82.151 us | 0.74x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 194.463 us | 264.208 us | 501.566 us | 0.74x |
| `common/uri-match` | `IsMatch` | 0.167 us | 0.235 us | 0.274 us | 0.71x |
| `common/ip-miss` | `IsMatch` | 0.121 us | 0.193 us | 0.216 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.083 us | 0.100 us | 0.61x |
| `common/ip-match` | `IsMatch` | 0.104 us | 0.175 us | 0.195 us | 0.60x |
| `common/email-match` | `IsMatch` | 0.147 us | 0.270 us | 0.296 us | 0.54x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.167 us | 0.184 us | 0.45x |
| `common/email-miss` | `IsMatch` | 0.152 us | 0.348 us | 0.373 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,158.558 us | 9,973.317 us | 17,107.400 us | 0.42x |
| `common/matches-word` | `Count` | 1.367 us | 4.269 us | 6.056 us | 0.32x |
| `industry/mariomka-ip-count` | `Count` | 2,708.068 us | 8,632.483 us | 17,171.150 us | 0.31x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.023 us | 0.084 us | 0.099 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.137 us | 0.27x |
| `common/one-node-backtracking` | `IsMatch` | 0.161 us | 0.621 us | 0.660 us | 0.26x |
| `common/split-words` | `Split` | 14.662 us | 79.243 us | 80.417 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.132 us | 976.150 us | 1,270.881 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.564 us | 0.595 us | 0.15x |
| `common/matches-words` | `Count` | 9.442 us | 70.537 us | 72.191 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.836 us | 0.885 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,569.900 us | 34,772.375 us | 41,136.900 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,929.748 us | 19,738.250 us | 20,564.133 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,875.119 us | 23,892.100 us | 31,394.700 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 536.584 us | 8,544.400 us | 9,161.900 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.803 us | 163.823 us | 165.160 us | 0.05x |
| `common/matches-set` | `Count` | 7.874 us | 196.223 us | 198.148 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 276.378 us | 11,842.640 us | 12,421.000 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 238.558 us | 36.513 us | 2,161.450 us | 6.53x |
| `literal/sherlock-zh` | 134.678 us | 24.151 us | 2,203.117 us | 5.58x |
| `literal/sherlock-ru` | 254.859 us | 90.083 us | 5,182.400 us | 2.83x |
| `ruff-noqa/tweaked` | 10,768.520 us | 4,619.036 us | 36,709.350 us | 2.33x |
| `literal/sherlock-casei-en` | 169.745 us | 79.546 us | 445.258 us | 2.13x |
| `literal-alternate/sherlock-casei-en` | 221.608 us | 132.639 us | 600.600 us | 1.67x |
| `aws-keys/quick` | 5,008.390 us | 3,605.564 us | 35,530.050 us | 1.39x |
| `literal/sherlock-casei-ru` | 251.532 us | 197.403 us | 5,379.190 us | 1.27x |
| `literal-alternate/sherlock-en` | 429.956 us | 375.119 us | 806.858 us | 1.15x |
| `literal-alternate/sherlock-en-mixed` | 200.528 us | 181.218 us | 555.550 us | 1.11x |
| `bounded-repeat/letters-ru` | 1,954.973 us | 2,073.272 us | 2,899.961 us | 0.94x |
| `literal/sherlock-en` | 70.226 us | 78.431 us | 409.642 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 168.539 us | 201.831 us | 592.460 us | 0.84x |
| `literal-alternate/sherlock-ru` | 1,700.153 us | 2,365.482 us | 7,497.767 us | 0.72x |
| `bounded-repeat/capitals` | 9,548.033 us | 13,702.700 us | 20,471.600 us | 0.70x |
| `bounded-repeat/letters-en` | 1,062.719 us | 1,852.863 us | 1,944.415 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 1,108.989 us | 1,988.631 us | 7,319.833 us | 0.56x |
| `aws-keys/full` | 9,816.260 us | 18,722.800 us | 50,079.000 us | 0.52x |
| `bounded-repeat/context` | 15,507.800 us | 69,606.800 us | 72,208.300 us | 0.22x |
| `ruff-noqa/real` | 4,593.073 us | 21,857.600 us | 35,359.050 us | 0.21x |
| `dictionary/single` | 8,208.543 us | 46,163.450 us | 46,273.800 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.176 us | 0.049 us | 0.088 us | 3.57x |
| `common/replace-words` | `Replace` | 101.258 us | 29.792 us | 25.232 us | 3.40x |
| `industry/mariomka-email-count` | `Count` | 1,536.855 us | 457.439 us | 7,957.700 us | 3.36x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 194.835 us | 58.733 us | 260.282 us | 3.32x |
| `common/match-word` | `Match` | 0.200 us | 0.081 us | 1.022 us | 2.48x |
| `common/ip-miss` | `IsMatch` | 0.121 us | 0.051 us | 0.073 us | 2.40x |
| `common/uri-miss` | `IsMatch` | 0.159 us | 0.067 us | 0.087 us | 2.38x |
| `industry/leipzig-river-window-count` | `Count` | 3,576.950 us | 1,581.756 us | 8,990.950 us | 2.26x |
| `common/ip-match` | `IsMatch` | 0.103 us | 0.049 us | 0.069 us | 2.13x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.046 us | 0.076 us | 2.04x |
| `common/uri-match` | `IsMatch` | 0.165 us | 0.090 us | 0.121 us | 1.83x |
| `common/one-node-backtracking` | `IsMatch` | 0.160 us | 0.089 us | 0.128 us | 1.81x |
| `common/email-match` | `IsMatch` | 0.147 us | 0.082 us | 0.107 us | 1.80x |
| `common/email-miss` | `IsMatch` | 0.152 us | 0.089 us | 0.115 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.051 us | 1.67x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.046 us | 1.67x |
| `industry/mariomka-uri-count` | `Count` | 1,750.579 us | 1,098.698 us | 8,510.733 us | 1.59x |
| `industry/leipzig-symbol-count` | `Count` | 1,887.478 us | 1,299.727 us | 7,062.567 us | 1.45x |
| `industry/leipzig-name-family-count` | `Count` | 4,168.467 us | 3,049.429 us | 8,335.267 us | 1.37x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.35x |
| `industry/leipzig-twain-count` | `Count` | 1,264.017 us | 1,280.683 us | 6,116.850 us | 0.99x |
| `industry/mariomka-ip-count` | `Count` | 2,705.479 us | 3,511.860 us | 11,093.300 us | 0.77x |
| `common/split-words` | `Split` | 14.672 us | 21.211 us | 23.039 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-words` | `Count` | 9.453 us | 14.843 us | 17.478 us | 0.64x |
| `common/matches-word` | `Count` | 1.351 us | 2.363 us | 3.670 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.205 us | 0.236 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.052 us | 0.31x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 535.386 us | 3,990.654 us | 4,407.733 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.217 us | 1,506.347 us | 1,844.010 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,925.362 us | 17,717.250 us | 18,616.900 us | 0.11x |
| `common/matches-set` | `Count` | 7.899 us | 106.483 us | 108.516 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.806 us | 106.895 us | 107.785 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 272.937 us | 4,823.955 us | 5,282.720 us | 0.06x |
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
| `lokad/imports/module-imports` | 15.214 us | 6.466 us | 29.353 us | 2.35x |
| `structural/keyword-to-generic-type` | 5.376 us | 4.116 us | 16.430 us | 1.31x |
| `lokad/langserv/color-short-hex` | 1.675 us | 1.353 us | 1.484 us | 1.24x |
| `lokad/lexer/doc-line` | 3.376 us | 3.098 us | 3.812 us | 1.09x |
| `structural/ordered-keyword-pair` | 5.612 us | 5.339 us | 16.901 us | 1.05x |
| `lokad/lexer/identifier` | 23.956 us | 22.892 us | 25.763 us | 1.05x |
| `lokad/langserv/helper-identifier` | 7.284 us | 7.221 us | 7.917 us | 1.01x |
| `fallback/lookahead` | 5.560 us | 6.112 us | 17.193 us | 0.91x |
| `lokad/langserv/identifier-validator` | 11.381 us | 12.560 us | 13.919 us | 0.91x |
| `lokad/lexer/operator-run` | 2.931 us | 3.429 us | 4.028 us | 0.85x |
| `literal-family/method-token-family` | 7.180 us | 9.341 us | 20.083 us | 0.77x |
| `lokad/style/hex-color` | 0.608 us | 0.820 us | 0.884 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 3.382 us | 4.898 us | 5.798 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.030 us | 2.959 us | 3.058 us | 0.69x |
| `literal/identifier-token` | 2.797 us | 4.359 us | 15.582 us | 0.64x |
| `literal/call-token` | 2.615 us | 4.340 us | 15.603 us | 0.60x |
| `lokad/lexer/number` | 8.266 us | 13.907 us | 15.107 us | 0.59x |
| `structural/method-family-generic-call` | 3.490 us | 6.681 us | 17.797 us | 0.52x |
| `lokad/style/cell-ref` | 1.480 us | 2.883 us | 3.086 us | 0.51x |
| `structural/method-family-call` | 3.901 us | 8.068 us | 18.912 us | 0.48x |
| `literal-family/type-token-family` | 10.593 us | 24.214 us | 35.519 us | 0.44x |
| `literal/identifier-token-casei` | 6.125 us | 15.146 us | 26.714 us | 0.40x |
| `lokad/style/range-ref` | 0.926 us | 2.711 us | 2.717 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.760 us | 17.336 us | 28.245 us | 0.33x |
| `structural/ordered-keyword-window` | 5.502 us | 17.735 us | 29.342 us | 0.31x |
| `structural/modifier-family-to-type-window` | 75.222 us | 257.231 us | 270.626 us | 0.29x |
| `lokad/lexer/string` | 25.652 us | 93.599 us | 99.843 us | 0.27x |
| `lokad/folding/region-marker` | 6.984 us | 36.819 us | 60.364 us | 0.19x |
| `structural/modifier-family-to-type-pair` | 23.597 us | 130.402 us | 142.344 us | 0.18x |
| `structural/ordered-identifier-window` | 4.996 us | 29.110 us | 39.610 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.559 us | 24.625 us | 25.016 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.241 us | 222.587 us | 235.932 us | 0.08x |
| `lokad/langserv/url-download` | 2.702 us | 50.954 us | 51.363 us | 0.05x |
| `balancing/begin-end-blocks` | 7.458 us | 4,444.367 us | 4,470.317 us | 0.0017x |
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
| `lokad/imports/module-imports` | 15.357 us | 4.472 us | 26.604 us | 3.43x |
| `structural/keyword-family-to-capitalized-identifier` | 17.888 us | 7.658 us | 18.665 us | 2.34x |
| `structural/modifier-family-to-type-pair` | 23.580 us | 10.804 us | 21.933 us | 2.18x |
| `lokad/langserv/color-short-hex` | 1.669 us | 1.001 us | 1.179 us | 1.67x |
| `structural/keyword-to-generic-type` | 5.376 us | 3.539 us | 15.275 us | 1.52x |
| `lokad/lexer/doc-line` | 3.380 us | 2.231 us | 2.945 us | 1.51x |
| `lokad/langserv/helper-identifier` | 7.293 us | 4.889 us | 5.711 us | 1.49x |
| `lokad/lexer/operator-run` | 2.935 us | 2.083 us | 2.690 us | 1.41x |
| `lokad/lexer/identifier` | 23.890 us | 17.023 us | 20.515 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.611 us | 4.181 us | 15.040 us | 1.34x |
| `structural/modifier-family-to-type-window` | 75.309 us | 56.295 us | 68.879 us | 1.34x |
| `fallback/lookahead` | 5.560 us | 4.256 us | 14.996 us | 1.31x |
| `lokad/langserv/identifier-validator` | 11.356 us | 9.405 us | 11.127 us | 1.21x |
| `lokad/style/hex-color` | 0.629 us | 0.528 us | 0.601 us | 1.19x |
| `lokad/lexer/number` | 8.254 us | 7.335 us | 8.507 us | 1.13x |
| `lokad/docs/inline-doc-prefix` | 3.376 us | 3.041 us | 3.865 us | 1.11x |
| `literal/identifier-token-casei` | 6.153 us | 5.937 us | 17.198 us | 1.04x |
| `lokad/langserv/color-rgb-hex` | 2.034 us | 1.968 us | 2.137 us | 1.03x |
| `literal-family/type-token-family` | 10.599 us | 11.567 us | 22.483 us | 0.92x |
| `lokad/style/cell-ref` | 1.488 us | 1.791 us | 1.928 us | 0.83x |
| `structural/method-family-call` | 3.874 us | 4.976 us | 15.530 us | 0.78x |
| `structural/method-family-generic-call` | 3.469 us | 4.517 us | 15.107 us | 0.77x |
| `literal-family/method-token-family` | 3.911 us | 5.329 us | 16.003 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.817 us | 8.683 us | 19.680 us | 0.67x |
| `literal/call-token` | 2.608 us | 3.987 us | 14.709 us | 0.65x |
| `structural/ordered-keyword-window` | 5.499 us | 8.679 us | 19.838 us | 0.63x |
| `lokad/style/range-ref` | 0.914 us | 1.526 us | 1.610 us | 0.60x |
| `literal/identifier-token` | 2.809 us | 4.730 us | 15.321 us | 0.59x |
| `lokad/lexer/string` | 25.591 us | 45.970 us | 49.437 us | 0.56x |
| `structural/ordered-identifier-window` | 5.007 us | 10.857 us | 21.694 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.524 us | 7.463 us | 8.054 us | 0.34x |
| `lokad/langserv/url-download` | 2.701 us | 13.034 us | 13.562 us | 0.21x |
| `lokad/folding/region-marker` | 6.980 us | 35.947 us | 59.766 us | 0.19x |
| `balancing/begin-end-blocks` | 7.452 us | 470.347 us | 496.841 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
