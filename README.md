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
and resource checks passed in **43m29s**. The [run receipt](bench/Experiments/DeliveryDev36-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 85.729 us | 18.555 us | 2,162.442 us | 4.62x |
| `literal-alternate/sherlock-zh` | 118.406 us | 67.635 us | 2,154.513 us | 1.75x |
| `literal/sherlock-ru` | 168.211 us | 99.270 us | 5,109.240 us | 1.69x |
| `literal/sherlock-casei-en` | 154.230 us | 187.143 us | 596.621 us | 0.82x |
| `literal/sherlock-casei-ru` | 245.791 us | 359.292 us | 5,500.967 us | 0.68x |
| `literal/sherlock-en` | 56.632 us | 86.140 us | 454.843 us | 0.66x |
| `aws-keys/quick` | 6,683.122 us | 10,248.200 us | 44,831.000 us | 0.65x |
| `ruff-noqa/tweaked` | 4,416.175 us | 8,021.914 us | 43,148.450 us | 0.55x |
| `bounded-repeat/letters-en` | 1,024.294 us | 2,821.911 us | 2,916.653 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,660.842 us | 4,778.055 us | 10,008.633 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 194.377 us | 596.799 us | 1,060.643 us | 0.33x |
| `dictionary/single` | 8,231.757 us | 25,743.700 us | 25,797.550 us | 0.32x |
| `bounded-repeat/letters-ru` | 1,920.844 us | 6,021.722 us | 6,772.262 us | 0.32x |
| `literal-alternate/sherlock-en` | 383.496 us | 1,469.097 us | 2,012.930 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 154.192 us | 656.928 us | 1,091.634 us | 0.23x |
| `bounded-repeat/capitals` | 9,493.533 us | 45,533.600 us | 52,399.100 us | 0.21x |
| `aws-keys/full` | 10,460.100 us | 84,949.000 us | 116,826.800 us | 0.12x |
| `bounded-repeat/context` | 13,687.725 us | 147,932.400 us | 150,015.700 us | 0.09x |
| `ruff-noqa/real` | 4,468.917 us | 69,383.600 us | 85,188.900 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 965.494 us | 23,102.367 us | 28,311.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 133.555 us | 10,930.380 us | 11,774.660 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,203.698 us | 488.651 us | 8,765.267 us | 2.46x |
| `common/match-word` | `Match` | 0.196 us | 0.163 us | 1.148 us | 1.21x |
| `industry/leipzig-twain-count` | `Count` | 1,153.175 us | 1,321.100 us | 8,299.300 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,439.203 us | 1,929.419 us | 10,525.433 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.111 us | 0.146 us | 0.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.099 us | 0.59x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.206 us | 0.229 us | 0.58x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.394 us | 259.590 us | 493.607 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.168 us | 0.183 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.172 us | 0.192 us | 0.46x |
| `industry/leipzig-name-family-count` | `Count` | 3,968.808 us | 9,727.950 us | 16,882.500 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.186 us | 0.204 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.232 us | 0.264 us | 0.40x |
| `common/replace-words` | `Replace` | 27.653 us | 77.694 us | 81.015 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.272 us | 0.299 us | 0.35x |
| `common/matches-word` | `Count` | 1.357 us | 4.196 us | 5.921 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.346 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,297.065 us | 8,273.971 us | 16,631.200 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.111 us | 0.142 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.096 us | 0.20x |
| `common/split-words` | `Split` | 14.558 us | 77.403 us | 78.298 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.615 us | 956.213 us | 1,258.735 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.562 us | 0.591 us | 0.15x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.076 us | 0.090 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.613 us | 0.650 us | 0.13x |
| `common/matches-words` | `Count` | 9.317 us | 71.743 us | 73.078 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.828 us | 0.873 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,334.620 us | 31,794.650 us | 45,504.000 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,910.326 us | 19,522.850 us | 20,399.200 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,774.841 us | 22,461.033 us | 29,883.650 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 478.159 us | 8,366.843 us | 8,895.983 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.410 us | 162.262 us | 167.050 us | 0.05x |
| `common/matches-set` | `Count` | 7.548 us | 193.265 us | 194.994 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 239.886 us | 11,779.280 us | 12,402.900 us | 0.02x |
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
| `literal/sherlock-zh` | 85.592 us | 23.667 us | 2,170.562 us | 3.62x |
| `literal-alternate/sherlock-zh` | 119.224 us | 35.767 us | 2,140.360 us | 3.33x |
| `literal/sherlock-casei-en` | 155.443 us | 77.590 us | 427.525 us | 2.00x |
| `literal/sherlock-ru` | 170.211 us | 88.142 us | 5,104.360 us | 1.93x |
| `aws-keys/quick` | 6,674.189 us | 3,669.007 us | 36,914.600 us | 1.82x |
| `literal/sherlock-casei-ru` | 248.035 us | 194.182 us | 5,223.810 us | 1.28x |
| `literal-alternate/sherlock-en` | 383.345 us | 339.404 us | 736.525 us | 1.13x |
| `literal-alternate/sherlock-en-mixed` | 194.272 us | 175.419 us | 552.131 us | 1.11x |
| `literal-alternate/sherlock-casei-en` | 132.770 us | 132.799 us | 542.407 us | 1.00x |
| `ruff-noqa/tweaked` | 4,504.758 us | 4,515.150 us | 37,389.300 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,920.363 us | 2,172.117 us | 3,019.059 us | 0.88x |
| `literal-alternate/sherlock-en-nomatch` | 152.743 us | 200.540 us | 585.714 us | 0.76x |
| `literal/sherlock-en` | 55.997 us | 75.147 us | 418.056 us | 0.75x |
| `bounded-repeat/capitals` | 9,465.417 us | 13,555.425 us | 19,937.300 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,616.435 us | 2,326.873 us | 7,434.933 us | 0.69x |
| `bounded-repeat/letters-en` | 1,032.051 us | 1,789.896 us | 1,912.270 us | 0.58x |
| `aws-keys/full` | 10,510.040 us | 18,662.733 us | 51,912.550 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 963.796 us | 1,951.788 us | 7,089.700 us | 0.49x |
| `ruff-noqa/real` | 4,497.208 us | 21,016.567 us | 34,451.350 us | 0.21x |
| `bounded-repeat/context` | 13,876.875 us | 68,818.200 us | 70,953.700 us | 0.20x |
| `dictionary/single` | 8,225.757 us | 45,577.000 us | 45,676.100 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,205.267 us | 456.780 us | 7,832.967 us | 2.64x |
| `common/match-word` | `Match` | 0.195 us | 0.080 us | 0.999 us | 2.44x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.752 us | 58.238 us | 264.740 us | 2.30x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.044 us | 0.074 us | 2.11x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.062 us | 0.030 us | 0.046 us | 2.08x |
| `industry/leipzig-river-window-count` | `Count` | 3,390.607 us | 1,659.076 us | 13,795.400 us | 2.04x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.067 us | 0.088 us | 1.78x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.029 us | 0.048 us | 1.71x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.048 us | 0.086 us | 1.67x |
| `common/replace-words` | `Replace` | 27.870 us | 17.231 us | 20.036 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.048 us | 0.070 us | 1.58x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.049 us | 0.070 us | 1.55x |
| `industry/leipzig-symbol-count` | `Count` | 1,785.466 us | 1,223.795 us | 6,826.867 us | 1.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.055 us | 0.076 us | 1.36x |
| `industry/leipzig-name-family-count` | `Count` | 3,951.523 us | 2,968.339 us | 8,190.133 us | 1.33x |
| `industry/mariomka-uri-count` | `Count` | 1,443.006 us | 1,096.474 us | 8,544.500 us | 1.32x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.081 us | 0.110 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.089 us | 0.114 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.088 us | 0.118 us | 1.06x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.088 us | 0.127 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 1,150.548 us | 1,277.933 us | 7,160.671 us | 0.90x |
| `common/matches-words` | `Count` | 9.230 us | 10.695 us | 13.819 us | 0.86x |
| `common/split-words` | `Split` | 14.529 us | 21.373 us | 23.254 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,290.473 us | 3,461.753 us | 11,067.150 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-word` | `Count` | 1.338 us | 2.347 us | 3.607 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.201 us | 0.235 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.037 us | 0.056 us | 0.33x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 477.560 us | 3,915.269 us | 4,342.492 us | 0.12x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.725 us | 1,472.247 us | 1,847.014 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,909.174 us | 18,686.050 us | 19,377.133 us | 0.10x |
| `common/matches-set` | `Count` | 7.613 us | 105.326 us | 107.513 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.426 us | 104.468 us | 105.690 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 239.384 us | 4,766.082 us | 5,247.690 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.541 us | 6.373 us | 28.890 us | 1.81x |
| `structural/keyword-to-generic-type` | 5.488 us | 4.069 us | 15.899 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.659 us | 1.333 us | 1.454 us | 1.24x |
| `lokad/lexer/doc-line` | 3.326 us | 2.970 us | 3.522 us | 1.12x |
| `lokad/lexer/identifier` | 23.839 us | 22.511 us | 25.447 us | 1.06x |
| `lokad/langserv/helper-identifier` | 7.145 us | 7.062 us | 7.859 us | 1.01x |
| `structural/ordered-keyword-pair` | 5.277 us | 5.378 us | 17.065 us | 0.98x |
| `lokad/langserv/identifier-validator` | 11.314 us | 12.442 us | 13.931 us | 0.91x |
| `fallback/lookahead` | 5.389 us | 6.041 us | 17.792 us | 0.89x |
| `lokad/lexer/operator-run` | 2.878 us | 3.363 us | 3.849 us | 0.86x |
| `literal-family/method-token-family` | 7.094 us | 9.264 us | 20.060 us | 0.77x |
| `lokad/style/hex-color` | 0.606 us | 0.802 us | 0.873 us | 0.76x |
| `lokad/docs/inline-doc-prefix` | 3.361 us | 4.659 us | 5.527 us | 0.72x |
| `lokad/langserv/color-rgb-hex` | 2.041 us | 2.915 us | 3.035 us | 0.70x |
| `lokad/lexer/number` | 8.982 us | 13.866 us | 14.695 us | 0.65x |
| `literal/identifier-token` | 2.761 us | 4.309 us | 15.673 us | 0.64x |
| `literal/call-token` | 2.589 us | 4.307 us | 15.795 us | 0.60x |
| `structural/ordered-identifier-window` | 16.104 us | 28.359 us | 39.545 us | 0.57x |
| `lokad/style/cell-ref` | 1.484 us | 2.825 us | 3.098 us | 0.53x |
| `structural/method-family-generic-call` | 3.441 us | 6.673 us | 17.811 us | 0.52x |
| `structural/method-family-call` | 3.856 us | 8.007 us | 18.656 us | 0.48x |
| `literal-family/type-token-family` | 10.396 us | 24.183 us | 35.385 us | 0.43x |
| `literal/identifier-token-casei` | 5.687 us | 14.994 us | 26.636 us | 0.38x |
| `lokad/style/range-ref` | 0.921 us | 2.693 us | 2.764 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.588 us | 17.188 us | 28.040 us | 0.33x |
| `structural/modifier-family-to-type-window` | 79.766 us | 253.617 us | 271.267 us | 0.31x |
| `structural/ordered-keyword-window` | 5.416 us | 17.534 us | 29.257 us | 0.31x |
| `lokad/lexer/string` | 21.505 us | 94.320 us | 98.762 us | 0.23x |
| `lokad/folding/region-marker` | 6.351 us | 36.699 us | 60.018 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.575 us | 24.558 us | 24.974 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.414 us | 222.182 us | 234.368 us | 0.05x |
| `lokad/langserv/url-download` | 2.486 us | 52.340 us | 52.638 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.000 us | 130.857 us | 142.356 us | 0.04x |
| `balancing/begin-end-blocks` | 5.752 us | 4,179.692 us | 4,179.842 us | 0.0014x |
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
| `lokad/imports/module-imports` | 11.538 us | 4.420 us | 25.836 us | 2.61x |
| `lokad/langserv/color-short-hex` | 1.651 us | 0.975 us | 1.207 us | 1.69x |
| `lokad/langserv/helper-identifier` | 7.128 us | 4.488 us | 5.611 us | 1.59x |
| `structural/keyword-to-generic-type` | 5.494 us | 3.486 us | 14.797 us | 1.58x |
| `lokad/lexer/doc-line` | 3.324 us | 2.161 us | 2.832 us | 1.54x |
| `structural/ordered-identifier-window` | 16.047 us | 10.876 us | 21.610 us | 1.48x |
| `lokad/lexer/operator-run` | 2.893 us | 1.962 us | 2.707 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.457 us | 7.864 us | 18.418 us | 1.46x |
| `structural/modifier-family-to-type-window` | 81.307 us | 55.997 us | 68.723 us | 1.45x |
| `lokad/lexer/identifier` | 23.660 us | 17.144 us | 20.536 us | 1.38x |
| `structural/ordered-keyword-pair` | 5.309 us | 4.103 us | 15.165 us | 1.29x |
| `fallback/lookahead` | 5.414 us | 4.234 us | 15.386 us | 1.28x |
| `lokad/lexer/number` | 9.023 us | 7.135 us | 8.303 us | 1.26x |
| `lokad/langserv/identifier-validator` | 11.374 us | 9.096 us | 11.199 us | 1.25x |
| `lokad/docs/inline-doc-prefix` | 3.385 us | 2.870 us | 3.572 us | 1.18x |
| `lokad/style/hex-color` | 0.603 us | 0.526 us | 0.606 us | 1.15x |
| `lokad/langserv/color-rgb-hex` | 2.034 us | 1.861 us | 2.080 us | 1.09x |
| `literal/identifier-token-casei` | 5.682 us | 5.857 us | 17.160 us | 0.97x |
| `literal-family/type-token-family` | 10.405 us | 11.361 us | 22.250 us | 0.92x |
| `lokad/style/cell-ref` | 1.486 us | 1.834 us | 1.905 us | 0.81x |
| `structural/method-family-generic-call` | 3.442 us | 4.411 us | 14.917 us | 0.78x |
| `structural/method-family-call` | 3.885 us | 5.062 us | 15.431 us | 0.77x |
| `literal-family/method-token-family` | 3.883 us | 5.306 us | 15.888 us | 0.73x |
| `literal/call-token` | 2.585 us | 3.974 us | 14.796 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.589 us | 8.636 us | 19.423 us | 0.65x |
| `structural/ordered-keyword-window` | 5.424 us | 8.580 us | 19.890 us | 0.63x |
| `literal/identifier-token` | 2.753 us | 4.625 us | 15.432 us | 0.60x |
| `lokad/style/range-ref` | 0.915 us | 1.578 us | 1.819 us | 0.58x |
| `structural/modifier-family-to-type-pair` | 4.984 us | 10.698 us | 22.013 us | 0.47x |
| `lokad/lexer/string` | 21.247 us | 45.825 us | 48.588 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.544 us | 7.383 us | 7.898 us | 0.34x |
| `lokad/langserv/url-download` | 2.477 us | 12.574 us | 13.236 us | 0.20x |
| `lokad/folding/region-marker` | 6.385 us | 35.507 us | 60.348 us | 0.18x |
| `balancing/begin-end-blocks` | 5.698 us | 467.373 us | 495.417 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
