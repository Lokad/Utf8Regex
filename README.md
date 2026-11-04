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
and resource checks passed in **44m08s**. The [run receipt](bench/Experiments/DeliveryDev20-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 135.639 us | 19.034 us | 2,173.152 us | 7.13x |
| `literal-alternate/sherlock-zh` | 248.127 us | 68.547 us | 2,237.405 us | 3.62x |
| `literal/sherlock-ru` | 248.112 us | 99.276 us | 5,225.950 us | 2.50x |
| `ruff-noqa/tweaked` | 10,734.020 us | 7,853.414 us | 43,709.200 us | 1.37x |
| `literal/sherlock-casei-en` | 168.864 us | 204.449 us | 596.445 us | 0.83x |
| `literal/sherlock-en` | 70.304 us | 86.870 us | 448.556 us | 0.81x |
| `literal/sherlock-casei-ru` | 261.146 us | 392.369 us | 5,654.478 us | 0.67x |
| `aws-keys/quick` | 5,005.270 us | 9,792.420 us | 44,614.900 us | 0.51x |
| `bounded-repeat/letters-en` | 1,047.179 us | 2,841.844 us | 2,929.441 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,737.690 us | 4,899.800 us | 10,154.867 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 208.664 us | 598.201 us | 1,032.364 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,975.950 us | 6,053.689 us | 6,850.700 us | 0.33x |
| `dictionary/single` | 8,272.114 us | 26,753.750 us | 26,747.450 us | 0.31x |
| `literal-alternate/sherlock-en` | 413.045 us | 1,492.186 us | 2,011.317 us | 0.28x |
| `literal-alternate/sherlock-en-nomatch` | 168.472 us | 664.152 us | 1,099.757 us | 0.25x |
| `bounded-repeat/capitals` | 9,588.750 us | 47,113.950 us | 53,384.100 us | 0.20x |
| `bounded-repeat/context` | 16,633.367 us | 149,193.800 us | 152,196.700 us | 0.11x |
| `aws-keys/full` | 9,082.717 us | 86,399.400 us | 118,046.600 us | 0.11x |
| `ruff-noqa/real` | 4,723.145 us | 70,545.100 us | 85,451.300 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,103.911 us | 23,829.700 us | 35,855.200 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 231.513 us | 11,509.720 us | 12,368.100 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,540.530 us | 494.670 us | 8,983.433 us | 3.11x |
| `common/match-word` | `Match` | 0.200 us | 0.163 us | 1.152 us | 1.23x |
| `industry/leipzig-twain-count` | `Count` | 1,273.720 us | 1,330.710 us | 8,317.583 us | 0.96x |
| `industry/mariomka-uri-count` | `Count` | 1,758.534 us | 1,927.246 us | 10,419.267 us | 0.91x |
| `common/replace-words` | `Replace` | 64.694 us | 76.985 us | 79.417 us | 0.84x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 200.601 us | 262.156 us | 495.733 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.112 us | 0.151 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.207 us | 0.226 us | 0.57x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.171 us | 0.185 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.174 us | 0.192 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 4,213.433 us | 9,940.500 us | 17,158.700 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.189 us | 0.209 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.233 us | 0.267 us | 0.40x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.268 us | 0.295 us | 0.36x |
| `common/email-miss` | `IsMatch` | 0.114 us | 0.344 us | 0.370 us | 0.33x |
| `industry/mariomka-ip-count` | `Count` | 2,726.300 us | 8,299.257 us | 16,845.800 us | 0.33x |
| `common/matches-word` | `Count` | 1.358 us | 4.265 us | 6.016 us | 0.32x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 14.982 us | 78.379 us | 79.781 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.489 us | 981.013 us | 1,276.190 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.556 us | 0.589 us | 0.18x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.615 us | 0.647 us | 0.13x |
| `common/matches-words` | `Count` | 9.392 us | 73.750 us | 75.546 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.090 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.837 us | 0.875 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,448.833 us | 32,514.700 us | 49,787.700 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,010.596 us | 19,501.650 us | 20,444.433 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,805.175 us | 23,600.000 us | 30,393.850 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 510.702 us | 8,198.929 us | 8,676.250 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.066 us | 167.533 us | 169.564 us | 0.04x |
| `common/matches-set` | `Count` | 7.002 us | 194.288 us | 197.374 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 252.797 us | 12,106.420 us | 13,085.400 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 239.319 us | 36.735 us | 2,182.004 us | 6.51x |
| `literal/sherlock-zh` | 135.906 us | 24.214 us | 2,217.730 us | 5.61x |
| `literal/sherlock-ru` | 247.937 us | 89.880 us | 5,233.080 us | 2.76x |
| `ruff-noqa/tweaked` | 10,787.200 us | 4,603.764 us | 38,073.200 us | 2.34x |
| `literal/sherlock-casei-en` | 169.703 us | 78.190 us | 459.565 us | 2.17x |
| `literal-alternate/sherlock-casei-en` | 223.991 us | 148.168 us | 602.815 us | 1.51x |
| `aws-keys/quick` | 5,003.770 us | 3,598.293 us | 36,174.250 us | 1.39x |
| `literal-alternate/sherlock-en` | 426.993 us | 348.286 us | 787.408 us | 1.23x |
| `literal/sherlock-casei-ru` | 265.965 us | 221.712 us | 5,323.500 us | 1.20x |
| `literal-alternate/sherlock-en-mixed` | 208.904 us | 185.756 us | 564.213 us | 1.12x |
| `bounded-repeat/letters-ru` | 1,978.212 us | 2,083.028 us | 2,867.311 us | 0.95x |
| `literal/sherlock-en` | 70.338 us | 76.488 us | 441.825 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 168.524 us | 202.055 us | 574.484 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,703.080 us | 2,368.991 us | 7,529.100 us | 0.72x |
| `bounded-repeat/capitals` | 9,590.133 us | 13,723.950 us | 20,182.500 us | 0.70x |
| `bounded-repeat/letters-en` | 1,049.423 us | 1,848.339 us | 1,930.019 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 1,091.421 us | 1,982.685 us | 9,605.500 us | 0.55x |
| `aws-keys/full` | 9,992.467 us | 18,745.800 us | 52,472.750 us | 0.53x |
| `bounded-repeat/context` | 16,824.133 us | 69,826.800 us | 72,419.000 us | 0.24x |
| `ruff-noqa/real` | 4,807.627 us | 20,936.167 us | 33,776.350 us | 0.23x |
| `dictionary/single` | 8,297.783 us | 46,207.200 us | 46,244.200 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/replace-words` | `Replace` | 64.587 us | 18.812 us | 20.490 us | 3.43x |
| `industry/mariomka-email-count` | `Count` | 1,536.682 us | 459.196 us | 7,833.800 us | 3.35x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 200.930 us | 60.457 us | 283.473 us | 3.32x |
| `common/match-word` | `Match` | 0.199 us | 0.077 us | 1.020 us | 2.60x |
| `industry/leipzig-river-window-count` | `Count` | 3,473.953 us | 1,525.382 us | 17,221.267 us | 2.28x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.046 us | 0.075 us | 2.09x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.067 us | 0.085 us | 1.74x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.030 us | 0.046 us | 1.67x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.087 us | 1.62x |
| `industry/mariomka-uri-count` | `Count` | 1,765.638 us | 1,096.862 us | 8,455.867 us | 1.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.032 us | 0.050 us | 1.61x |
| `common/ip-match` | `IsMatch` | 0.077 us | 0.050 us | 0.070 us | 1.55x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.051 us | 0.071 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,815.325 us | 1,223.429 us | 6,600.367 us | 1.48x |
| `industry/leipzig-name-family-count` | `Count` | 4,336.200 us | 3,078.571 us | 8,499.433 us | 1.41x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.077 us | 1.37x |
| `common/email-miss` | `IsMatch` | 0.114 us | 0.090 us | 0.116 us | 1.27x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.107 us | 1.19x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.088 us | 0.121 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,277.635 us | 1,295.395 us | 6,965.200 us | 0.99x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.087 us | 0.128 us | 0.92x |
| `common/matches-words` | `Count` | 9.389 us | 10.999 us | 14.083 us | 0.85x |
| `industry/mariomka-ip-count` | `Count` | 2,729.553 us | 3,523.113 us | 11,213.150 us | 0.77x |
| `common/split-words` | `Split` | 14.996 us | 21.241 us | 22.992 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-word` | `Count` | 1.358 us | 2.354 us | 3.647 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.203 us | 0.234 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.054 us | 0.29x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.458 us | 4,004.400 us | 4,412.700 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.793 us | 1,508.161 us | 1,838.357 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,011.488 us | 18,379.650 us | 19,158.100 us | 0.11x |
| `common/matches-set` | `Count` | 6.986 us | 105.579 us | 107.393 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.021 us | 106.728 us | 108.378 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 251.712 us | 4,836.100 us | 5,248.640 us | 0.05x |
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
| `lokad/imports/module-imports` | 15.688 us | 6.453 us | 29.047 us | 2.43x |
| `lokad/langserv/color-short-hex` | 1.809 us | 1.252 us | 1.369 us | 1.45x |
| `structural/keyword-to-generic-type` | 5.057 us | 4.104 us | 16.023 us | 1.23x |
| `lokad/lexer/doc-line` | 3.359 us | 2.970 us | 3.508 us | 1.13x |
| `lokad/lexer/identifier` | 23.932 us | 22.718 us | 25.655 us | 1.05x |
| `lokad/langserv/helper-identifier` | 7.109 us | 6.937 us | 7.555 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.379 us | 5.381 us | 16.912 us | 1.00x |
| `lokad/lexer/operator-run` | 3.171 us | 3.411 us | 3.896 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.248 us | 12.358 us | 13.746 us | 0.91x |
| `fallback/lookahead` | 5.450 us | 6.127 us | 17.397 us | 0.89x |
| `lokad/style/hex-color` | 0.659 us | 0.783 us | 0.830 us | 0.84x |
| `literal-family/method-token-family` | 7.188 us | 9.349 us | 20.151 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 1.961 us | 2.755 us | 2.838 us | 0.71x |
| `lokad/docs/inline-doc-prefix` | 3.385 us | 4.883 us | 5.730 us | 0.69x |
| `literal/identifier-token` | 2.802 us | 4.406 us | 15.615 us | 0.64x |
| `literal/call-token` | 2.623 us | 4.338 us | 15.807 us | 0.60x |
| `lokad/lexer/number` | 8.331 us | 14.196 us | 15.529 us | 0.59x |
| `structural/method-family-generic-call` | 3.516 us | 6.669 us | 18.023 us | 0.53x |
| `lokad/style/cell-ref` | 1.468 us | 2.908 us | 2.880 us | 0.50x |
| `structural/method-family-call` | 3.964 us | 8.126 us | 19.131 us | 0.49x |
| `literal/identifier-token-casei` | 7.193 us | 15.892 us | 27.144 us | 0.45x |
| `literal-family/type-token-family` | 10.616 us | 23.793 us | 34.627 us | 0.45x |
| `lokad/style/range-ref` | 0.954 us | 2.632 us | 2.727 us | 0.36x |
| `structural/ordered-modifier-pair` | 5.813 us | 17.280 us | 28.446 us | 0.34x |
| `structural/ordered-keyword-window` | 5.499 us | 17.801 us | 28.961 us | 0.31x |
| `structural/modifier-family-to-type-window` | 75.203 us | 258.806 us | 270.541 us | 0.29x |
| `lokad/lexer/string` | 24.337 us | 94.088 us | 99.718 us | 0.26x |
| `lokad/folding/region-marker` | 6.992 us | 36.969 us | 61.488 us | 0.19x |
| `structural/ordered-identifier-window` | 5.033 us | 28.772 us | 39.798 us | 0.17x |
| `structural/modifier-family-to-type-pair` | 23.454 us | 134.654 us | 146.141 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.469 us | 23.730 us | 24.191 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.087 us | 219.077 us | 232.232 us | 0.08x |
| `lokad/langserv/url-download` | 2.583 us | 51.736 us | 50.742 us | 0.05x |
| `balancing/begin-end-blocks` | 5.720 us | 4,356.308 us | 4,371.342 us | 0.0013x |
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
| `lokad/imports/module-imports` | 15.849 us | 4.455 us | 26.022 us | 3.56x |
| `structural/keyword-family-to-capitalized-identifier` | 17.886 us | 7.738 us | 18.560 us | 2.31x |
| `structural/modifier-family-to-type-pair` | 23.467 us | 10.785 us | 22.612 us | 2.18x |
| `lokad/langserv/color-short-hex` | 1.810 us | 0.913 us | 1.092 us | 1.98x |
| `lokad/lexer/operator-run` | 3.176 us | 2.014 us | 2.529 us | 1.58x |
| `lokad/langserv/helper-identifier` | 7.057 us | 4.477 us | 5.449 us | 1.58x |
| `lokad/lexer/doc-line` | 3.356 us | 2.152 us | 2.919 us | 1.56x |
| `structural/keyword-to-generic-type` | 5.062 us | 3.535 us | 14.888 us | 1.43x |
| `lokad/lexer/identifier` | 23.960 us | 17.009 us | 20.778 us | 1.41x |
| `structural/modifier-family-to-type-window` | 75.783 us | 56.857 us | 68.666 us | 1.33x |
| `lokad/style/hex-color` | 0.656 us | 0.501 us | 0.575 us | 1.31x |
| `structural/ordered-keyword-pair` | 5.379 us | 4.179 us | 15.038 us | 1.29x |
| `fallback/lookahead` | 5.458 us | 4.260 us | 15.159 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.002 us | 8.841 us | 11.179 us | 1.24x |
| `literal/identifier-token-casei` | 7.208 us | 5.966 us | 17.648 us | 1.21x |
| `lokad/langserv/color-rgb-hex` | 1.959 us | 1.708 us | 1.928 us | 1.15x |
| `lokad/docs/inline-doc-prefix` | 3.378 us | 2.978 us | 3.677 us | 1.13x |
| `lokad/lexer/number` | 8.326 us | 7.481 us | 8.598 us | 1.11x |
| `literal-family/type-token-family` | 10.588 us | 11.567 us | 22.470 us | 0.92x |
| `lokad/style/cell-ref` | 1.471 us | 1.697 us | 1.832 us | 0.87x |
| `structural/method-family-call` | 3.976 us | 5.087 us | 15.754 us | 0.78x |
| `structural/method-family-generic-call` | 3.512 us | 4.634 us | 15.078 us | 0.76x |
| `literal-family/method-token-family` | 3.916 us | 5.350 us | 16.001 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.803 us | 8.662 us | 19.662 us | 0.67x |
| `literal/call-token` | 2.627 us | 4.016 us | 14.851 us | 0.65x |
| `lokad/style/range-ref` | 0.949 us | 1.452 us | 1.522 us | 0.65x |
| `structural/ordered-keyword-window` | 5.491 us | 8.642 us | 19.403 us | 0.64x |
| `literal/identifier-token` | 2.803 us | 4.731 us | 15.369 us | 0.59x |
| `lokad/lexer/string` | 24.382 us | 45.804 us | 49.392 us | 0.53x |
| `structural/ordered-identifier-window` | 5.021 us | 10.822 us | 21.465 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.502 us | 7.283 us | 7.754 us | 0.34x |
| `lokad/langserv/url-download` | 2.567 us | 12.653 us | 13.173 us | 0.20x |
| `lokad/folding/region-marker` | 6.992 us | 36.556 us | 60.511 us | 0.19x |
| `balancing/begin-end-blocks` | 5.738 us | 471.107 us | 496.637 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
