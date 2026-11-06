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
and resource checks passed in **43m35s**. The [run receipt](bench/Experiments/DeliveryDev41-20261106/full-refresh.json)
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
| `literal/sherlock-zh` | 86.150 us | 19.010 us | 2,155.330 us | 4.53x |
| `literal/sherlock-ru` | 174.494 us | 103.065 us | 5,427.033 us | 1.69x |
| `literal-alternate/sherlock-zh` | 105.430 us | 68.968 us | 2,201.229 us | 1.53x |
| `literal/sherlock-casei-en` | 166.736 us | 192.106 us | 577.028 us | 0.87x |
| `literal/sherlock-casei-ru` | 256.999 us | 391.184 us | 5,617.867 us | 0.66x |
| `aws-keys/quick` | 6,559.712 us | 10,031.320 us | 43,465.050 us | 0.65x |
| `literal/sherlock-en` | 57.521 us | 88.058 us | 455.498 us | 0.65x |
| `ruff-noqa/tweaked` | 4,380.783 us | 7,906.029 us | 42,987.150 us | 0.55x |
| `bounded-repeat/letters-en` | 1,052.990 us | 2,886.717 us | 2,982.265 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,677.674 us | 4,817.427 us | 10,011.633 us | 0.35x |
| `dictionary/single` | 8,382.300 us | 25,816.550 us | 25,777.150 us | 0.32x |
| `bounded-repeat/letters-ru` | 1,946.577 us | 6,007.011 us | 6,805.587 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 188.268 us | 604.170 us | 1,050.781 us | 0.31x |
| `literal-alternate/sherlock-en` | 398.944 us | 1,508.529 us | 2,047.719 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 156.192 us | 666.919 us | 1,099.978 us | 0.23x |
| `bounded-repeat/capitals` | 9,518.183 us | 47,144.600 us | 55,094.100 us | 0.20x |
| `bounded-repeat/context` | 24,313.367 us | 149,430.100 us | 152,438.500 us | 0.16x |
| `aws-keys/full` | 8,968.240 us | 87,636.300 us | 120,777.000 us | 0.10x |
| `ruff-noqa/real` | 4,497.575 us | 70,742.200 us | 88,315.200 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 951.643 us | 23,281.000 us | 28,470.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 133.310 us | 11,782.860 us | 12,516.080 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,211.957 us | 492.662 us | 8,992.967 us | 2.46x |
| `common/match-word` | `Match` | 0.200 us | 0.168 us | 1.202 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,158.766 us | 1,354.787 us | 8,774.233 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,432.147 us | 1,982.138 us | 10,601.233 us | 0.72x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.112 us | 0.151 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.191 us | 0.211 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.885 us | 272.895 us | 502.093 us | 0.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.169 us | 0.186 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.181 us | 0.199 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,098.933 us | 10,172.340 us | 17,653.250 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.235 us | 0.272 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.195 us | 0.215 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.103 us | 0.266 us | 0.292 us | 0.39x |
| `common/replace-words` | `Replace` | 28.471 us | 77.811 us | 80.200 us | 0.37x |
| `common/matches-word` | `Count` | 1.354 us | 4.365 us | 5.989 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.352 us | 0.378 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,334.227 us | 8,330.557 us | 16,843.500 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.111 us | 0.139 us | 0.26x |
| `common/split-words` | `Split` | 18.106 us | 77.492 us | 79.323 us | 0.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.482 us | 981.417 us | 1,272.060 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.566 us | 0.607 us | 0.17x |
| `common/matches-words` | `Count` | 9.474 us | 71.731 us | 73.486 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.082 us | 0.635 us | 0.673 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.833 us | 0.874 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,026.760 us | 19,898.833 us | 20,784.500 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,345.807 us | 32,911.400 us | 46,982.600 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,891.493 us | 23,158.067 us | 30,209.900 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 517.659 us | 8,326.333 us | 8,878.133 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.540 us | 165.758 us | 167.835 us | 0.05x |
| `common/matches-set` | `Count` | 7.869 us | 193.489 us | 196.683 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 244.741 us | 11,945.180 us | 12,490.200 us | 0.02x |
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
| `literal/sherlock-zh` | 86.058 us | 24.077 us | 2,164.567 us | 3.57x |
| `literal-alternate/sherlock-zh` | 105.172 us | 36.023 us | 2,158.904 us | 2.92x |
| `literal/sherlock-casei-en` | 166.580 us | 79.308 us | 447.156 us | 2.10x |
| `literal/sherlock-ru` | 174.671 us | 90.348 us | 5,353.060 us | 1.93x |
| `aws-keys/quick` | 5,724.012 us | 3,623.400 us | 36,528.950 us | 1.58x |
| `literal/sherlock-casei-ru` | 258.576 us | 197.473 us | 5,448.880 us | 1.31x |
| `literal-alternate/sherlock-en` | 414.463 us | 372.668 us | 808.511 us | 1.11x |
| `literal-alternate/sherlock-en-mixed` | 188.214 us | 178.198 us | 567.746 us | 1.06x |
| `ruff-noqa/tweaked` | 4,380.358 us | 4,580.773 us | 37,875.500 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,946.773 us | 2,081.952 us | 2,890.961 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 133.977 us | 149.906 us | 546.160 us | 0.89x |
| `literal-alternate/sherlock-en-nomatch` | 156.437 us | 203.540 us | 570.367 us | 0.77x |
| `literal/sherlock-en` | 57.691 us | 77.465 us | 436.417 us | 0.74x |
| `bounded-repeat/capitals` | 9,541.200 us | 13,661.500 us | 21,308.400 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,628.635 us | 2,369.668 us | 7,585.367 us | 0.69x |
| `bounded-repeat/letters-en` | 1,054.382 us | 1,851.500 us | 1,931.992 us | 0.57x |
| `aws-keys/full` | 9,782.820 us | 19,045.300 us | 50,945.750 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 949.915 us | 1,974.538 us | 7,268.400 us | 0.48x |
| `bounded-repeat/context` | 24,430.350 us | 69,718.200 us | 72,308.000 us | 0.35x |
| `ruff-noqa/real` | 4,513.827 us | 21,532.133 us | 34,468.350 us | 0.21x |
| `dictionary/single` | 8,380.450 us | 46,174.750 us | 46,252.700 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,207.907 us | 456.930 us | 7,844.333 us | 2.64x |
| `common/match-word` | `Match` | 0.199 us | 0.081 us | 0.998 us | 2.47x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.378 us | 60.715 us | 256.998 us | 2.20x |
| `industry/leipzig-river-window-count` | `Count` | 3,351.980 us | 1,578.956 us | 14,049.000 us | 2.12x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.045 us | 0.075 us | 2.11x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.068 us | 0.088 us | 1.76x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.047 us | 1.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.050 us | 1.63x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.088 us | 1.63x |
| `common/replace-words` | `Replace` | 28.445 us | 18.376 us | 21.830 us | 1.55x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.048 us | 0.068 us | 1.54x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.050 us | 0.071 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,893.196 us | 1,270.383 us | 6,866.933 us | 1.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.056 us | 0.076 us | 1.34x |
| `industry/leipzig-name-family-count` | `Count` | 4,187.808 us | 3,136.206 us | 8,514.033 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,432.181 us | 1,108.737 us | 8,523.300 us | 1.29x |
| `common/email-match` | `IsMatch` | 0.103 us | 0.081 us | 0.108 us | 1.26x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.117 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.088 us | 0.120 us | 1.06x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.088 us | 0.129 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 1,159.098 us | 1,280.817 us | 5,512.600 us | 0.90x |
| `common/matches-words` | `Count` | 9.481 us | 10.827 us | 13.931 us | 0.88x |
| `common/split-words` | `Split` | 18.060 us | 21.711 us | 23.597 us | 0.83x |
| `industry/mariomka-ip-count` | `Count` | 2,350.955 us | 3,537.713 us | 11,858.650 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `common/matches-word` | `Count` | 1.351 us | 2.375 us | 3.742 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.212 us | 0.238 us | 0.46x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.335 us | 612.610 us | 879.730 us | 0.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.053 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,027.412 us | 8,832.900 us | 9,585.880 us | 0.23x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 516.187 us | 3,972.754 us | 4,387.917 us | 0.13x |
| `common/matches-set` | `Count` | 7.850 us | 106.990 us | 108.207 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.504 us | 105.564 us | 106.850 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 244.265 us | 4,854.736 us | 5,295.320 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.908 us | 6.471 us | 30.216 us | 1.69x |
| `structural/keyword-to-generic-type` | 5.503 us | 4.127 us | 15.828 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.682 us | 1.366 us | 1.496 us | 1.23x |
| `lokad/lexer/doc-line` | 3.376 us | 3.062 us | 3.725 us | 1.10x |
| `lokad/lexer/identifier` | 24.022 us | 22.970 us | 25.969 us | 1.05x |
| `lokad/langserv/helper-identifier` | 7.281 us | 7.183 us | 7.967 us | 1.01x |
| `structural/ordered-keyword-pair` | 5.377 us | 5.511 us | 16.889 us | 0.98x |
| `lokad/style/hex-color` | 0.748 us | 0.818 us | 0.899 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.368 us | 12.615 us | 14.483 us | 0.90x |
| `fallback/lookahead` | 5.454 us | 6.098 us | 17.168 us | 0.89x |
| `lokad/lexer/operator-run` | 2.930 us | 3.372 us | 4.000 us | 0.87x |
| `literal-family/method-token-family` | 7.227 us | 9.406 us | 19.985 us | 0.77x |
| `lokad/docs/inline-doc-prefix` | 3.394 us | 4.709 us | 5.479 us | 0.72x |
| `lokad/langserv/color-rgb-hex` | 2.064 us | 2.988 us | 3.109 us | 0.69x |
| `literal/identifier-token` | 2.787 us | 4.402 us | 15.863 us | 0.63x |
| `literal/call-token` | 2.612 us | 4.332 us | 15.600 us | 0.60x |
| `lokad/lexer/number` | 8.374 us | 14.079 us | 15.298 us | 0.59x |
| `structural/ordered-identifier-window` | 16.225 us | 29.066 us | 39.889 us | 0.56x |
| `lokad/style/cell-ref` | 1.493 us | 2.921 us | 3.039 us | 0.51x |
| `structural/method-family-generic-call` | 3.474 us | 6.806 us | 18.310 us | 0.51x |
| `structural/method-family-call` | 3.874 us | 8.166 us | 18.774 us | 0.47x |
| `literal-family/type-token-family` | 10.528 us | 23.861 us | 34.562 us | 0.44x |
| `literal/identifier-token-casei` | 6.151 us | 15.161 us | 27.246 us | 0.41x |
| `lokad/style/range-ref` | 0.924 us | 2.795 us | 2.815 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.594 us | 17.418 us | 28.275 us | 0.32x |
| `structural/modifier-family-to-type-window` | 79.353 us | 258.406 us | 272.565 us | 0.31x |
| `structural/ordered-keyword-window` | 5.491 us | 18.134 us | 29.768 us | 0.30x |
| `lokad/lexer/string` | 21.935 us | 98.961 us | 103.498 us | 0.22x |
| `lokad/folding/region-marker` | 6.396 us | 37.423 us | 60.690 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.627 us | 25.151 us | 25.642 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.464 us | 220.191 us | 233.525 us | 0.05x |
| `lokad/langserv/url-download` | 2.575 us | 52.625 us | 53.460 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.027 us | 131.797 us | 144.102 us | 0.04x |
| `balancing/begin-end-blocks` | 5.746 us | 4,380.558 us | 4,405.392 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.916 us | 4.462 us | 27.058 us | 2.45x |
| `lokad/langserv/color-short-hex` | 1.674 us | 1.074 us | 1.207 us | 1.56x |
| `structural/keyword-to-generic-type` | 5.502 us | 3.544 us | 14.733 us | 1.55x |
| `lokad/langserv/helper-identifier` | 7.276 us | 4.759 us | 5.908 us | 1.53x |
| `structural/ordered-identifier-window` | 16.155 us | 10.712 us | 21.427 us | 1.51x |
| `lokad/lexer/doc-line` | 3.370 us | 2.251 us | 2.967 us | 1.50x |
| `structural/keyword-family-to-capitalized-identifier` | 11.554 us | 7.911 us | 19.022 us | 1.46x |
| `lokad/lexer/operator-run` | 2.926 us | 2.004 us | 2.718 us | 1.46x |
| `structural/modifier-family-to-type-window` | 79.910 us | 57.487 us | 69.340 us | 1.39x |
| `lokad/style/hex-color` | 0.743 us | 0.537 us | 0.615 us | 1.38x |
| `lokad/lexer/identifier` | 24.017 us | 17.478 us | 20.759 us | 1.37x |
| `fallback/lookahead` | 5.479 us | 4.250 us | 14.915 us | 1.29x |
| `structural/ordered-keyword-pair` | 5.371 us | 4.178 us | 15.013 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.413 us | 9.372 us | 11.596 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.396 us | 2.850 us | 3.581 us | 1.19x |
| `lokad/lexer/number` | 8.358 us | 7.573 us | 8.692 us | 1.10x |
| `literal/identifier-token-casei` | 6.127 us | 5.940 us | 17.639 us | 1.03x |
| `lokad/langserv/color-rgb-hex` | 2.058 us | 2.001 us | 2.190 us | 1.03x |
| `literal-family/type-token-family` | 10.555 us | 11.561 us | 22.236 us | 0.91x |
| `lokad/style/cell-ref` | 1.490 us | 1.840 us | 2.048 us | 0.81x |
| `structural/method-family-generic-call` | 3.460 us | 4.536 us | 15.425 us | 0.76x |
| `structural/method-family-call` | 3.887 us | 5.107 us | 15.523 us | 0.76x |
| `literal-family/method-token-family` | 3.912 us | 5.357 us | 15.755 us | 0.73x |
| `literal/call-token` | 2.613 us | 3.985 us | 14.698 us | 0.66x |
| `literal/identifier-token` | 2.803 us | 4.326 us | 15.802 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.593 us | 8.811 us | 19.606 us | 0.63x |
| `structural/ordered-keyword-window` | 5.468 us | 8.667 us | 20.167 us | 0.63x |
| `lokad/style/range-ref` | 0.925 us | 1.540 us | 1.640 us | 0.60x |
| `lokad/lexer/string` | 21.871 us | 46.908 us | 50.187 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 5.014 us | 10.870 us | 22.246 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.626 us | 7.987 us | 8.528 us | 0.33x |
| `lokad/langserv/url-download` | 2.580 us | 13.060 us | 13.601 us | 0.20x |
| `lokad/folding/region-marker` | 6.462 us | 37.696 us | 59.224 us | 0.17x |
| `balancing/begin-end-blocks` | 5.735 us | 470.981 us | 497.902 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
