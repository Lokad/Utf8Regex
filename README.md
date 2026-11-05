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
and resource checks passed in **43m56s**. The [run receipt](bench/Experiments/DeliveryDev35-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 88.574 us | 19.052 us | 2,155.900 us | 4.65x |
| `literal-alternate/sherlock-zh` | 144.890 us | 69.582 us | 2,164.191 us | 2.08x |
| `literal/sherlock-ru` | 172.568 us | 100.961 us | 5,528.244 us | 1.71x |
| `literal/sherlock-casei-en` | 160.002 us | 203.557 us | 611.525 us | 0.79x |
| `literal/sherlock-en` | 57.313 us | 86.378 us | 512.405 us | 0.66x |
| `literal/sherlock-casei-ru` | 253.820 us | 396.531 us | 5,863.978 us | 0.64x |
| `ruff-noqa/tweaked` | 4,371.358 us | 8,165.071 us | 41,935.850 us | 0.54x |
| `aws-keys/quick` | 4,897.409 us | 9,955.900 us | 43,274.750 us | 0.49x |
| `bounded-repeat/letters-en` | 1,058.725 us | 2,838.628 us | 2,936.547 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,683.367 us | 4,849.127 us | 10,094.533 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,993.377 us | 6,026.944 us | 6,823.762 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 202.304 us | 627.986 us | 1,068.945 us | 0.32x |
| `dictionary/single` | 8,364.971 us | 26,187.800 us | 26,201.450 us | 0.32x |
| `literal-alternate/sherlock-en` | 384.249 us | 1,503.237 us | 1,997.230 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 160.832 us | 679.447 us | 1,131.631 us | 0.24x |
| `bounded-repeat/capitals` | 9,580.200 us | 46,507.850 us | 54,980.100 us | 0.21x |
| `aws-keys/full` | 8,835.500 us | 87,487.500 us | 121,623.800 us | 0.10x |
| `bounded-repeat/context` | 14,155.525 us | 148,621.900 us | 152,125.900 us | 0.10x |
| `ruff-noqa/real` | 4,392.742 us | 70,497.900 us | 86,701.600 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 874.734 us | 23,507.333 us | 28,794.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 169.394 us | 11,392.660 us | 12,075.700 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,220.527 us | 493.643 us | 8,936.200 us | 2.47x |
| `common/match-word` | `Match` | 0.200 us | 0.164 us | 1.201 us | 1.22x |
| `industry/leipzig-twain-count` | `Count` | 1,163.172 us | 1,358.822 us | 9,593.133 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,440.346 us | 1,947.122 us | 10,466.367 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.110 us | 0.146 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.194 us | 0.213 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.083 us | 0.100 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.211 us | 259.239 us | 500.096 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.168 us | 0.186 us | 0.46x |
| `common/ip-miss` | `IsMatch` | 0.085 us | 0.187 us | 0.208 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.176 us | 0.197 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,046.423 us | 9,881.900 us | 17,247.000 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.240 us | 0.276 us | 0.38x |
| `common/replace-words` | `Replace` | 28.129 us | 77.412 us | 78.908 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.272 us | 0.298 us | 0.36x |
| `common/matches-word` | `Count` | 1.351 us | 4.256 us | 6.052 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.346 us | 0.373 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,334.241 us | 8,335.900 us | 16,836.800 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.138 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.779 us | 971.115 us | 1,259.956 us | 0.18x |
| `common/split-words` | `Split` | 14.614 us | 80.736 us | 82.383 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.567 us | 0.599 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.098 us | 0.624 us | 0.660 us | 0.16x |
| `common/matches-words` | `Count` | 9.392 us | 70.695 us | 72.518 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.090 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.843 us | 0.886 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,355.662 us | 31,860.150 us | 45,375.100 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,086.088 us | 19,833.200 us | 20,699.000 us | 0.11x |
| `industry/leipzig-symbol-count` | `Count` | 1,854.581 us | 22,671.867 us | 30,269.650 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 497.713 us | 8,714.017 us | 9,273.367 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.099 us | 165.166 us | 167.101 us | 0.04x |
| `common/matches-set` | `Count` | 7.813 us | 193.606 us | 195.298 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 261.300 us | 11,924.620 us | 12,381.460 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 144.520 us | 36.618 us | 2,159.312 us | 3.95x |
| `literal/sherlock-zh` | 88.966 us | 24.136 us | 2,149.430 us | 3.69x |
| `literal/sherlock-casei-en` | 160.117 us | 79.855 us | 414.588 us | 2.01x |
| `literal/sherlock-ru` | 172.513 us | 89.254 us | 5,371.650 us | 1.93x |
| `aws-keys/quick` | 4,890.164 us | 3,611.414 us | 35,628.550 us | 1.35x |
| `literal-alternate/sherlock-casei-en` | 169.540 us | 132.883 us | 561.532 us | 1.28x |
| `literal-alternate/sherlock-en-mixed` | 199.373 us | 177.295 us | 565.076 us | 1.12x |
| `literal/sherlock-casei-ru` | 253.659 us | 225.687 us | 5,543.360 us | 1.12x |
| `literal-alternate/sherlock-en` | 383.859 us | 349.081 us | 750.173 us | 1.10x |
| `bounded-repeat/letters-ru` | 1,986.392 us | 2,079.920 us | 2,880.233 us | 0.96x |
| `ruff-noqa/tweaked` | 4,380.942 us | 4,590.100 us | 36,815.300 us | 0.95x |
| `literal-alternate/sherlock-en-nomatch` | 160.393 us | 203.493 us | 587.743 us | 0.79x |
| `literal/sherlock-en` | 57.302 us | 76.360 us | 445.534 us | 0.75x |
| `literal-alternate/sherlock-ru` | 1,700.507 us | 2,364.709 us | 7,495.133 us | 0.72x |
| `bounded-repeat/capitals` | 9,567.483 us | 13,696.575 us | 21,196.000 us | 0.70x |
| `bounded-repeat/letters-en` | 1,057.038 us | 1,853.746 us | 1,937.769 us | 0.57x |
| `aws-keys/full` | 9,672.360 us | 18,922.800 us | 53,292.400 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 874.050 us | 1,993.558 us | 7,142.467 us | 0.44x |
| `bounded-repeat/context` | 14,269.700 us | 69,914.500 us | 72,716.500 us | 0.20x |
| `ruff-noqa/real` | 4,393.975 us | 21,615.400 us | 34,615.500 us | 0.20x |
| `dictionary/single` | 8,374.000 us | 46,443.850 us | 46,439.550 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,222.485 us | 457.147 us | 7,768.333 us | 2.67x |
| `common/match-word` | `Match` | 0.200 us | 0.081 us | 1.000 us | 2.48x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.835 us | 61.558 us | 282.521 us | 2.19x |
| `industry/leipzig-river-window-count` | `Count` | 3,376.475 us | 1,569.306 us | 13,221.367 us | 2.15x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.044 us | 0.075 us | 2.11x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.068 us | 0.089 us | 1.73x |
| `common/ip-miss` | `IsMatch` | 0.085 us | 0.050 us | 0.071 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.030 us | 0.048 us | 1.67x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.029 us | 0.047 us | 1.67x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.048 us | 0.087 us | 1.61x |
| `common/replace-words` | `Replace` | 28.154 us | 17.781 us | 21.042 us | 1.58x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.048 us | 0.069 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,871.063 us | 1,274.975 us | 7,050.967 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.054 us | 0.077 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,082.492 us | 3,000.288 us | 8,391.833 us | 1.36x |
| `industry/mariomka-uri-count` | `Count` | 1,446.894 us | 1,111.257 us | 8,473.800 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.081 us | 0.109 us | 1.20x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.089 us | 0.116 us | 1.12x |
| `common/one-node-backtracking` | `IsMatch` | 0.096 us | 0.088 us | 0.129 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.090 us | 0.120 us | 1.01x |
| `industry/leipzig-twain-count` | `Count` | 1,168.456 us | 1,303.523 us | 5,611.367 us | 0.90x |
| `common/matches-words` | `Count` | 9.401 us | 11.036 us | 14.076 us | 0.85x |
| `common/split-words` | `Split` | 14.609 us | 20.881 us | 22.825 us | 0.70x |
| `industry/mariomka-ip-count` | `Count` | 2,346.236 us | 3,521.460 us | 11,030.850 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.67x |
| `common/matches-word` | `Count` | 1.352 us | 2.383 us | 3.785 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.204 us | 0.235 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.052 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,142.235 us | 8,943.833 us | 9,817.500 us | 0.24x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 498.528 us | 3,985.815 us | 4,383.583 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.358 us | 1,480.357 us | 1,821.079 us | 0.12x |
| `common/matches-set` | `Count` | 7.812 us | 107.194 us | 108.411 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.126 us | 105.391 us | 107.095 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 259.872 us | 4,828.909 us | 5,263.610 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.639 us | 6.308 us | 28.455 us | 1.85x |
| `structural/keyword-to-generic-type` | 5.318 us | 4.021 us | 15.619 us | 1.32x |
| `lokad/langserv/color-short-hex` | 1.659 us | 1.370 us | 1.503 us | 1.21x |
| `lokad/lexer/doc-line` | 3.312 us | 3.027 us | 3.660 us | 1.09x |
| `lokad/lexer/identifier` | 23.611 us | 22.796 us | 25.828 us | 1.04x |
| `structural/ordered-keyword-pair` | 5.322 us | 5.273 us | 16.851 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.113 us | 7.098 us | 8.090 us | 1.00x |
| `fallback/lookahead` | 5.552 us | 6.024 us | 16.981 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.149 us | 12.443 us | 13.840 us | 0.90x |
| `lokad/lexer/operator-run` | 2.881 us | 3.276 us | 3.805 us | 0.88x |
| `lokad/style/hex-color` | 0.722 us | 0.828 us | 0.909 us | 0.87x |
| `literal-family/method-token-family` | 7.051 us | 9.177 us | 19.901 us | 0.77x |
| `lokad/docs/inline-doc-prefix` | 3.364 us | 4.743 us | 5.432 us | 0.71x |
| `lokad/langserv/color-rgb-hex` | 2.004 us | 2.926 us | 3.107 us | 0.68x |
| `literal/identifier-token` | 2.756 us | 4.336 us | 15.478 us | 0.64x |
| `literal/call-token` | 2.569 us | 4.274 us | 15.601 us | 0.60x |
| `lokad/lexer/number` | 8.295 us | 14.009 us | 14.969 us | 0.59x |
| `structural/ordered-identifier-window` | 16.005 us | 28.297 us | 39.405 us | 0.57x |
| `lokad/style/cell-ref` | 1.499 us | 2.904 us | 2.999 us | 0.52x |
| `structural/method-family-generic-call` | 3.415 us | 6.662 us | 18.032 us | 0.51x |
| `structural/method-family-call` | 3.837 us | 8.183 us | 19.009 us | 0.47x |
| `literal-family/type-token-family` | 10.492 us | 24.254 us | 35.560 us | 0.43x |
| `literal/identifier-token-casei` | 6.092 us | 15.558 us | 26.407 us | 0.39x |
| `lokad/style/range-ref` | 0.900 us | 2.703 us | 2.715 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.338 us | 17.122 us | 28.217 us | 0.31x |
| `structural/ordered-keyword-window` | 5.417 us | 17.395 us | 29.000 us | 0.31x |
| `structural/modifier-family-to-type-window` | 78.290 us | 253.739 us | 268.129 us | 0.31x |
| `lokad/lexer/string` | 21.744 us | 97.331 us | 102.276 us | 0.22x |
| `lokad/folding/region-marker` | 6.413 us | 35.777 us | 61.308 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.485 us | 24.180 us | 24.720 us | 0.10x |
| `lokad/langserv/url-download` | 2.594 us | 50.406 us | 50.373 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 10.551 us | 222.247 us | 234.998 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.909 us | 134.461 us | 146.861 us | 0.04x |
| `balancing/begin-end-blocks` | 5.674 us | 4,276.775 us | 4,268.767 us | 0.0013x |
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
| `lokad/imports/module-imports` | 11.605 us | 4.413 us | 25.724 us | 2.63x |
| `lokad/langserv/color-short-hex` | 1.669 us | 1.043 us | 1.220 us | 1.60x |
| `structural/keyword-to-generic-type` | 5.365 us | 3.504 us | 14.578 us | 1.53x |
| `structural/ordered-identifier-window` | 15.892 us | 10.409 us | 21.098 us | 1.53x |
| `lokad/lexer/doc-line` | 3.341 us | 2.226 us | 2.916 us | 1.50x |
| `lokad/lexer/operator-run` | 2.898 us | 1.944 us | 2.467 us | 1.49x |
| `lokad/langserv/helper-identifier` | 7.145 us | 4.846 us | 5.933 us | 1.47x |
| `structural/modifier-family-to-type-window` | 78.028 us | 55.286 us | 68.283 us | 1.41x |
| `lokad/lexer/identifier` | 23.552 us | 16.938 us | 20.773 us | 1.39x |
| `structural/keyword-family-to-capitalized-identifier` | 10.607 us | 7.778 us | 18.773 us | 1.36x |
| `fallback/lookahead` | 5.607 us | 4.172 us | 14.718 us | 1.34x |
| `lokad/style/hex-color` | 0.714 us | 0.542 us | 0.626 us | 1.32x |
| `structural/ordered-keyword-pair` | 5.310 us | 4.150 us | 14.913 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.263 us | 9.080 us | 11.114 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.366 us | 2.860 us | 3.574 us | 1.18x |
| `lokad/lexer/number` | 8.212 us | 7.232 us | 8.560 us | 1.14x |
| `lokad/langserv/color-rgb-hex` | 2.015 us | 1.929 us | 2.071 us | 1.04x |
| `literal/identifier-token-casei` | 6.101 us | 5.880 us | 17.110 us | 1.04x |
| `literal-family/type-token-family` | 10.329 us | 11.371 us | 22.102 us | 0.91x |
| `lokad/style/cell-ref` | 1.496 us | 1.823 us | 1.927 us | 0.82x |
| `structural/method-family-call` | 3.882 us | 5.045 us | 15.505 us | 0.77x |
| `structural/method-family-generic-call` | 3.408 us | 4.436 us | 15.192 us | 0.77x |
| `literal-family/method-token-family` | 3.819 us | 5.260 us | 15.906 us | 0.73x |
| `literal/call-token` | 2.600 us | 3.918 us | 14.735 us | 0.66x |
| `structural/ordered-keyword-window` | 5.378 us | 8.560 us | 19.548 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.393 us | 8.607 us | 19.770 us | 0.63x |
| `literal/identifier-token` | 2.759 us | 4.686 us | 15.034 us | 0.59x |
| `lokad/style/range-ref` | 0.900 us | 1.589 us | 1.647 us | 0.57x |
| `structural/modifier-family-to-type-pair` | 4.993 us | 10.573 us | 21.515 us | 0.47x |
| `lokad/lexer/string` | 21.642 us | 46.594 us | 49.573 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.462 us | 7.536 us | 8.029 us | 0.33x |
| `lokad/langserv/url-download` | 2.594 us | 12.942 us | 13.349 us | 0.20x |
| `lokad/folding/region-marker` | 6.339 us | 36.470 us | 59.059 us | 0.17x |
| `balancing/begin-end-blocks` | 5.723 us | 464.826 us | 495.512 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
