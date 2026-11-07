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
and resource checks passed in **46m16s**. The [run receipt](bench/Experiments/DeliveryDev47-20261107/full-refresh.json)
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
| `literal/sherlock-zh` | 84.359 us | 18.631 us | 2,142.626 us | 4.53x |
| `literal/sherlock-ru` | 168.655 us | 95.944 us | 5,328.540 us | 1.76x |
| `literal-alternate/sherlock-zh` | 103.621 us | 68.070 us | 2,171.606 us | 1.52x |
| `literal/sherlock-casei-en` | 152.755 us | 192.722 us | 547.903 us | 0.79x |
| `literal/sherlock-casei-ru` | 250.187 us | 362.157 us | 5,560.362 us | 0.69x |
| `aws-keys/quick` | 6,525.642 us | 9,485.450 us | 44,287.000 us | 0.69x |
| `literal/sherlock-en` | 57.149 us | 86.856 us | 459.699 us | 0.66x |
| `ruff-noqa/tweaked` | 4,366.541 us | 7,465.027 us | 42,439.300 us | 0.58x |
| `bounded-repeat/letters-en` | 1,035.575 us | 2,784.939 us | 2,888.446 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,632.920 us | 4,696.406 us | 9,862.075 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,900.900 us | 5,867.515 us | 6,753.567 us | 0.32x |
| `dictionary/single` | 8,259.420 us | 25,627.200 us | 26,059.300 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 186.603 us | 590.733 us | 1,022.918 us | 0.32x |
| `literal-alternate/sherlock-en` | 377.751 us | 1,496.440 us | 1,980.742 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 151.097 us | 642.798 us | 1,073.568 us | 0.24x |
| `bounded-repeat/capitals` | 9,317.288 us | 46,250.500 us | 49,456.100 us | 0.20x |
| `bounded-repeat/context` | 24,423.075 us | 147,676.700 us | 151,404.100 us | 0.17x |
| `aws-keys/full` | 10,493.200 us | 85,500.400 us | 114,653.800 us | 0.12x |
| `ruff-noqa/real` | 4,386.906 us | 68,828.200 us | 97,260.900 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 935.226 us | 23,426.150 us | 29,011.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 131.847 us | 11,494.743 us | 12,399.771 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,176.145 us | 504.434 us | 8,905.175 us | 2.33x |
| `common/match-word` | `Match` | 0.194 us | 0.165 us | 1.091 us | 1.17x |
| `industry/leipzig-twain-count` | `Count` | 1,203.411 us | 1,344.424 us | 8,632.689 us | 0.90x |
| `industry/mariomka-uri-count` | `Count` | 1,420.691 us | 1,875.183 us | 10,339.775 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.110 us | 0.150 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.191 us | 0.209 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.102 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 129.757 us | 260.868 us | 499.766 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.167 us | 0.183 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.176 us | 0.196 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 3,993.032 us | 9,753.663 us | 17,060.167 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.185 us | 0.203 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.232 us | 0.270 us | 0.40x |
| `common/replace-words` | `Replace` | 27.637 us | 76.038 us | 78.070 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.266 us | 0.291 us | 0.36x |
| `common/matches-word` | `Count` | 1.333 us | 4.241 us | 5.901 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.345 us | 0.367 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,289.150 us | 8,152.760 us | 16,533.933 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.107 us | 0.138 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.097 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.211 us | 961.267 us | 1,220.850 us | 0.18x |
| `common/split-words` | `Split` | 14.404 us | 79.151 us | 81.161 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.554 us | 0.589 us | 0.16x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.613 us | 0.644 us | 0.13x |
| `common/matches-words` | `Count` | 9.264 us | 71.878 us | 72.668 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.075 us | 0.090 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.834 us | 0.877 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,327.739 us | 31,676.900 us | 38,965.950 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,931.930 us | 19,151.000 us | 19,832.100 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,796.881 us | 22,796.550 us | 29,931.950 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 501.801 us | 8,064.330 us | 8,602.167 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.542 us | 160.931 us | 164.516 us | 0.05x |
| `common/matches-set` | `Count` | 7.492 us | 193.526 us | 192.962 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 241.767 us | 11,885.000 us | 12,458.457 us | 0.02x |
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
| `literal/sherlock-zh` | 84.675 us | 23.739 us | 2,147.932 us | 3.57x |
| `literal-alternate/sherlock-zh` | 102.547 us | 35.931 us | 2,125.737 us | 2.85x |
| `literal/sherlock-casei-en` | 151.406 us | 76.856 us | 416.470 us | 1.97x |
| `literal/sherlock-ru` | 167.045 us | 86.861 us | 5,178.987 us | 1.92x |
| `aws-keys/quick` | 6,539.458 us | 3,657.118 us | 36,231.850 us | 1.79x |
| `literal/sherlock-casei-ru` | 248.014 us | 222.087 us | 5,315.260 us | 1.12x |
| `literal-alternate/sherlock-en` | 391.189 us | 365.809 us | 770.945 us | 1.07x |
| `literal-alternate/sherlock-en-mixed` | 183.099 us | 173.308 us | 589.742 us | 1.06x |
| `literal-alternate/sherlock-casei-en` | 130.660 us | 131.596 us | 583.050 us | 0.99x |
| `ruff-noqa/tweaked` | 4,473.241 us | 4,533.653 us | 36,332.300 us | 0.99x |
| `bounded-repeat/letters-ru` | 1,904.422 us | 2,039.438 us | 2,876.816 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 153.034 us | 199.321 us | 568.729 us | 0.77x |
| `literal/sherlock-en` | 57.525 us | 76.108 us | 423.137 us | 0.76x |
| `literal-alternate/sherlock-ru` | 1,648.243 us | 2,306.806 us | 7,387.050 us | 0.71x |
| `bounded-repeat/capitals` | 9,246.263 us | 13,401.983 us | 16,223.250 us | 0.69x |
| `bounded-repeat/letters-en` | 1,035.392 us | 1,805.910 us | 1,898.018 us | 0.57x |
| `aws-keys/full` | 9,062.487 us | 18,369.460 us | 49,775.467 us | 0.49x |
| `literal-alternate/sherlock-casei-ru` | 936.700 us | 1,953.775 us | 7,206.480 us | 0.48x |
| `bounded-repeat/context` | 24,948.175 us | 68,785.700 us | 71,189.650 us | 0.36x |
| `ruff-noqa/real` | 4,355.694 us | 20,369.050 us | 51,509.533 us | 0.21x |
| `dictionary/single` | 8,204.944 us | 45,372.650 us | 45,686.350 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.191 us | 0.076 us | 0.962 us | 2.51x |
| `industry/mariomka-email-count` | `Count` | 1,166.148 us | 471.863 us | 7,782.900 us | 2.47x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 131.140 us | 58.017 us | 261.745 us | 2.26x |
| `industry/leipzig-river-window-count` | `Count` | 3,357.461 us | 1,496.776 us | 7,710.500 us | 2.24x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.043 us | 0.074 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.066 us | 0.085 us | 1.78x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.030 us | 0.051 us | 1.72x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.088 us | 1.64x |
| `common/replace-words` | `Replace` | 27.458 us | 16.824 us | 19.674 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.046 us | 1.63x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.047 us | 0.069 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.050 us | 0.069 us | 1.49x |
| `industry/leipzig-symbol-count` | `Count` | 1,806.652 us | 1,217.243 us | 7,181.060 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.077 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,163.800 us | 3,045.038 us | 8,171.000 us | 1.37x |
| `industry/mariomka-uri-count` | `Count` | 1,427.719 us | 1,098.034 us | 8,317.825 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.080 us | 0.107 us | 1.20x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.087 us | 0.112 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.088 us | 0.120 us | 1.05x |
| `industry/leipzig-twain-count` | `Count` | 1,208.125 us | 1,319.783 us | 7,491.430 us | 0.92x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.087 us | 0.129 us | 0.91x |
| `common/matches-words` | `Count` | 9.289 us | 10.969 us | 14.054 us | 0.85x |
| `common/split-words` | `Split` | 14.563 us | 21.225 us | 23.113 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.075 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,282.038 us | 3,462.917 us | 10,749.900 us | 0.66x |
| `common/matches-word` | `Count` | 1.333 us | 2.344 us | 3.507 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.204 us | 0.236 us | 0.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.034 us | 0.052 us | 0.32x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.713 us | 583.482 us | 848.289 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,915.423 us | 8,669.056 us | 9,346.675 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 499.392 us | 3,920.484 us | 4,317.694 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.504 us | 103.824 us | 105.441 us | 0.07x |
| `common/matches-set` | `Count` | 7.477 us | 104.072 us | 105.407 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 243.150 us | 4,713.887 us | 5,185.680 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.593 us | 6.459 us | 29.582 us | 1.64x |
| `structural/keyword-to-generic-type` | 5.404 us | 4.057 us | 15.613 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.658 us | 1.352 us | 1.479 us | 1.23x |
| `lokad/lexer/doc-line` | 3.294 us | 2.985 us | 3.541 us | 1.10x |
| `lokad/lexer/identifier` | 23.559 us | 22.760 us | 25.097 us | 1.04x |
| `structural/ordered-keyword-pair` | 5.317 us | 5.311 us | 16.809 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.154 us | 7.290 us | 7.830 us | 0.98x |
| `lokad/langserv/identifier-validator` | 11.311 us | 12.564 us | 13.935 us | 0.90x |
| `fallback/lookahead` | 5.303 us | 6.029 us | 17.224 us | 0.88x |
| `lokad/lexer/operator-run` | 2.956 us | 3.399 us | 3.843 us | 0.87x |
| `literal-family/method-token-family` | 7.116 us | 9.174 us | 19.761 us | 0.78x |
| `lokad/style/hex-color` | 0.603 us | 0.809 us | 0.862 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 3.519 us | 4.944 us | 5.489 us | 0.71x |
| `lokad/langserv/color-rgb-hex` | 2.031 us | 2.926 us | 3.035 us | 0.69x |
| `literal/identifier-token` | 2.752 us | 4.296 us | 15.458 us | 0.64x |
| `literal/call-token` | 2.555 us | 4.262 us | 15.325 us | 0.60x |
| `lokad/lexer/number` | 8.207 us | 14.499 us | 15.317 us | 0.57x |
| `structural/ordered-identifier-window` | 16.114 us | 28.713 us | 39.482 us | 0.56x |
| `structural/method-family-generic-call` | 3.401 us | 6.754 us | 18.016 us | 0.50x |
| `lokad/style/cell-ref` | 1.497 us | 3.021 us | 2.982 us | 0.50x |
| `structural/method-family-call` | 3.829 us | 8.217 us | 18.841 us | 0.47x |
| `literal-family/type-token-family` | 10.396 us | 23.345 us | 34.409 us | 0.45x |
| `literal/identifier-token-casei` | 6.116 us | 15.154 us | 26.723 us | 0.40x |
| `lokad/style/range-ref` | 0.916 us | 2.732 us | 2.732 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.550 us | 17.046 us | 28.050 us | 0.33x |
| `structural/ordered-keyword-window` | 5.426 us | 17.491 us | 28.997 us | 0.31x |
| `structural/modifier-family-to-type-window` | 77.020 us | 257.451 us | 276.973 us | 0.30x |
| `lokad/lexer/string` | 21.655 us | 94.528 us | 100.146 us | 0.23x |
| `lokad/folding/region-marker` | 6.343 us | 36.950 us | 60.544 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.510 us | 24.520 us | 24.956 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.415 us | 215.429 us | 230.342 us | 0.05x |
| `lokad/langserv/url-download` | 2.468 us | 50.635 us | 51.038 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.970 us | 133.114 us | 147.113 us | 0.04x |
| `balancing/begin-end-blocks` | 5.723 us | 4,312.956 us | 4,321.589 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.674 us | 4.455 us | 26.784 us | 2.40x |
| `lokad/langserv/color-short-hex` | 1.662 us | 1.001 us | 1.190 us | 1.66x |
| `structural/keyword-to-generic-type` | 5.459 us | 3.403 us | 14.563 us | 1.60x |
| `lokad/lexer/doc-line` | 3.268 us | 2.100 us | 2.908 us | 1.56x |
| `lokad/langserv/helper-identifier` | 7.131 us | 4.627 us | 5.753 us | 1.54x |
| `structural/ordered-identifier-window` | 16.193 us | 10.587 us | 21.321 us | 1.53x |
| `lokad/lexer/operator-run` | 2.947 us | 1.931 us | 2.566 us | 1.53x |
| `lokad/lexer/identifier` | 23.530 us | 16.338 us | 20.112 us | 1.44x |
| `structural/keyword-family-to-capitalized-identifier` | 11.300 us | 7.895 us | 18.735 us | 1.43x |
| `structural/modifier-family-to-type-window` | 77.774 us | 56.642 us | 68.508 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.338 us | 4.095 us | 14.969 us | 1.30x |
| `lokad/docs/inline-doc-prefix` | 3.532 us | 2.775 us | 3.595 us | 1.27x |
| `fallback/lookahead` | 5.312 us | 4.186 us | 15.018 us | 1.27x |
| `lokad/langserv/identifier-validator` | 11.400 us | 9.158 us | 11.964 us | 1.24x |
| `lokad/style/hex-color` | 0.604 us | 0.525 us | 0.619 us | 1.15x |
| `lokad/lexer/number` | 8.180 us | 7.318 us | 8.397 us | 1.12x |
| `lokad/langserv/color-rgb-hex` | 2.003 us | 1.834 us | 2.092 us | 1.09x |
| `literal/identifier-token-casei` | 6.111 us | 5.824 us | 17.115 us | 1.05x |
| `literal-family/type-token-family` | 10.417 us | 11.374 us | 22.192 us | 0.92x |
| `lokad/style/cell-ref` | 1.492 us | 1.773 us | 2.002 us | 0.84x |
| `structural/method-family-call` | 3.858 us | 5.028 us | 15.405 us | 0.77x |
| `structural/method-family-generic-call` | 3.384 us | 4.420 us | 15.207 us | 0.77x |
| `literal-family/method-token-family` | 3.868 us | 5.308 us | 15.632 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.578 us | 8.555 us | 19.549 us | 0.65x |
| `literal/call-token` | 2.527 us | 3.912 us | 14.471 us | 0.65x |
| `structural/ordered-keyword-window` | 5.407 us | 8.555 us | 19.652 us | 0.63x |
| `lokad/style/range-ref` | 0.913 us | 1.498 us | 1.571 us | 0.61x |
| `literal/identifier-token` | 2.772 us | 4.658 us | 14.916 us | 0.60x |
| `lokad/lexer/string` | 21.515 us | 46.288 us | 49.777 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 4.955 us | 10.698 us | 21.576 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.503 us | 7.476 us | 8.136 us | 0.33x |
| `lokad/langserv/url-download` | 2.474 us | 13.010 us | 13.513 us | 0.19x |
| `lokad/folding/region-marker` | 6.435 us | 36.976 us | 59.170 us | 0.17x |
| `balancing/begin-end-blocks` | 5.696 us | 466.730 us | 494.944 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
