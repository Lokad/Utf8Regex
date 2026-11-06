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
and resource checks passed in **46m04s**. The [run receipt](bench/Experiments/DeliveryDev43-20261106/full-refresh.json)
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
| `literal/sherlock-zh` | 86.681 us | 19.011 us | 2,167.883 us | 4.56x |
| `literal/sherlock-ru` | 168.634 us | 98.131 us | 5,427.700 us | 1.72x |
| `literal-alternate/sherlock-zh` | 105.119 us | 68.764 us | 2,206.494 us | 1.53x |
| `literal/sherlock-casei-en` | 156.455 us | 190.493 us | 558.892 us | 0.82x |
| `literal/sherlock-en` | 57.275 us | 82.664 us | 454.520 us | 0.69x |
| `literal/sherlock-casei-ru` | 247.699 us | 391.063 us | 5,617.315 us | 0.63x |
| `ruff-noqa/tweaked` | 4,464.565 us | 7,772.400 us | 42,252.850 us | 0.57x |
| `aws-keys/quick` | 4,882.619 us | 9,578.237 us | 44,966.350 us | 0.51x |
| `bounded-repeat/letters-en` | 1,066.283 us | 2,848.544 us | 2,950.758 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,678.051 us | 4,871.562 us | 10,166.125 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 199.872 us | 596.178 us | 1,006.728 us | 0.34x |
| `dictionary/single` | 8,377.256 us | 26,345.700 us | 26,373.100 us | 0.32x |
| `bounded-repeat/letters-ru` | 1,946.397 us | 6,546.638 us | 6,978.508 us | 0.30x |
| `literal-alternate/sherlock-en` | 397.691 us | 1,550.870 us | 2,041.255 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 156.272 us | 681.505 us | 1,099.584 us | 0.23x |
| `bounded-repeat/capitals` | 9,482.250 us | 46,564.700 us | 50,434.100 us | 0.20x |
| `bounded-repeat/context` | 24,792.425 us | 149,675.600 us | 153,005.300 us | 0.17x |
| `aws-keys/full` | 10,650.757 us | 86,742.700 us | 121,365.600 us | 0.12x |
| `ruff-noqa/real` | 4,520.065 us | 70,582.250 us | 86,789.600 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 959.594 us | 23,553.850 us | 28,983.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 130.361 us | 11,753.371 us | 12,570.686 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,196.602 us | 488.883 us | 8,961.425 us | 2.45x |
| `common/match-word` | `Match` | 0.199 us | 0.174 us | 1.158 us | 1.14x |
| `industry/leipzig-twain-count` | `Count` | 1,157.255 us | 1,320.528 us | 8,624.460 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,439.011 us | 1,976.197 us | 10,564.450 us | 0.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.112 us | 0.149 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.195 us | 0.213 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.345 us | 268.967 us | 502.757 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.170 us | 0.187 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.177 us | 0.197 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,097.678 us | 9,858.737 us | 17,038.133 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.233 us | 0.266 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.194 us | 0.214 us | 0.39x |
| `common/replace-words` | `Replace` | 28.224 us | 78.143 us | 79.763 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.272 us | 0.297 us | 0.35x |
| `common/matches-word` | `Count` | 1.356 us | 4.303 us | 5.997 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.347 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,323.336 us | 8,312.800 us | 16,842.800 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.137 us | 0.26x |
| `common/split-words` | `Split` | 17.844 us | 80.393 us | 82.048 us | 0.22x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.098 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.293 us | 965.859 us | 1,236.258 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.563 us | 0.594 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.093 us | 0.621 us | 0.655 us | 0.15x |
| `common/matches-words` | `Count` | 9.380 us | 70.746 us | 71.961 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.841 us | 0.886 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,090.636 us | 19,537.425 us | 20,095.200 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,310.900 us | 33,154.533 us | 39,787.700 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,884.862 us | 23,415.300 us | 31,235.800 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 509.837 us | 8,326.240 us | 8,826.289 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.501 us | 167.529 us | 169.206 us | 0.04x |
| `common/matches-set` | `Count` | 7.536 us | 198.067 us | 199.915 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 257.513 us | 11,899.371 us | 12,414.367 us | 0.02x |
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
| `literal/sherlock-zh` | 86.693 us | 24.175 us | 2,165.677 us | 3.59x |
| `literal-alternate/sherlock-zh` | 105.216 us | 36.582 us | 2,176.944 us | 2.88x |
| `literal/sherlock-casei-en` | 156.788 us | 77.791 us | 410.983 us | 2.02x |
| `literal/sherlock-ru` | 169.265 us | 90.000 us | 5,300.813 us | 1.88x |
| `aws-keys/quick` | 4,900.913 us | 3,611.200 us | 37,879.150 us | 1.36x |
| `literal/sherlock-casei-ru` | 248.845 us | 196.564 us | 5,432.167 us | 1.27x |
| `literal-alternate/sherlock-en-mixed` | 198.924 us | 175.790 us | 545.957 us | 1.13x |
| `literal-alternate/sherlock-en` | 416.403 us | 371.627 us | 773.302 us | 1.12x |
| `ruff-noqa/tweaked` | 4,496.771 us | 4,575.553 us | 37,110.250 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,941.336 us | 2,075.814 us | 2,895.954 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 129.839 us | 146.547 us | 581.315 us | 0.89x |
| `literal/sherlock-en` | 57.208 us | 72.976 us | 413.142 us | 0.78x |
| `literal-alternate/sherlock-en-nomatch` | 156.152 us | 200.475 us | 617.588 us | 0.78x |
| `literal-alternate/sherlock-ru` | 1,698.853 us | 2,367.953 us | 7,483.450 us | 0.72x |
| `bounded-repeat/capitals` | 9,502.450 us | 13,687.883 us | 16,607.725 us | 0.69x |
| `bounded-repeat/letters-en` | 1,064.230 us | 1,851.183 us | 1,943.315 us | 0.57x |
| `aws-keys/full` | 9,449.212 us | 18,667.300 us | 52,786.667 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 960.479 us | 1,978.472 us | 7,270.075 us | 0.49x |
| `bounded-repeat/context` | 24,619.425 us | 69,721.750 us | 73,205.300 us | 0.35x |
| `ruff-noqa/real` | 4,541.159 us | 21,034.875 us | 49,972.767 us | 0.22x |
| `dictionary/single` | 8,385.444 us | 46,104.950 us | 46,224.000 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,199.431 us | 454.019 us | 7,712.775 us | 2.64x |
| `common/match-word` | `Match` | 0.199 us | 0.079 us | 0.993 us | 2.51x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.739 us | 61.172 us | 255.973 us | 2.20x |
| `industry/leipzig-river-window-count` | `Count` | 3,319.783 us | 1,538.871 us | 8,207.260 us | 2.16x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.044 us | 0.075 us | 2.15x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.064 us | 0.031 us | 0.048 us | 2.03x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.067 us | 0.091 us | 1.79x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.030 us | 0.050 us | 1.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.049 us | 0.088 us | 1.64x |
| `common/replace-words` | `Replace` | 28.135 us | 17.389 us | 20.438 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.049 us | 0.069 us | 1.56x |
| `industry/leipzig-symbol-count` | `Count` | 1,889.668 us | 1,281.350 us | 7,458.520 us | 1.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,210.183 us | 3,106.417 us | 8,575.200 us | 1.36x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.056 us | 0.077 us | 1.33x |
| `industry/mariomka-uri-count` | `Count` | 1,441.932 us | 1,101.048 us | 8,475.475 us | 1.31x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.060 us | 0.076 us | 1.28x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.089 us | 0.116 us | 1.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.093 us | 0.089 us | 0.128 us | 1.05x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.089 us | 0.120 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,158.634 us | 1,279.998 us | 7,411.110 us | 0.91x |
| `common/matches-words` | `Count` | 9.369 us | 11.038 us | 14.160 us | 0.85x |
| `common/split-words` | `Split` | 17.870 us | 21.201 us | 23.079 us | 0.84x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.074 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,329.845 us | 3,480.764 us | 10,940.367 us | 0.67x |
| `common/matches-word` | `Count` | 1.351 us | 2.358 us | 3.714 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.209 us | 0.239 us | 0.47x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.052 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,098.439 us | 8,861.578 us | 9,404.812 us | 0.24x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 510.455 us | 3,995.284 us | 4,348.417 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.110 us | 1,493.245 us | 1,818.038 us | 0.12x |
| `common/matches-set` | `Count` | 7.531 us | 105.879 us | 107.559 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.492 us | 105.686 us | 107.812 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 259.053 us | 4,843.850 us | 5,262.953 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.350 us | 6.409 us | 29.083 us | 1.77x |
| `structural/keyword-to-generic-type` | 5.512 us | 4.143 us | 15.747 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.672 us | 1.366 us | 1.519 us | 1.22x |
| `lokad/lexer/doc-line` | 3.378 us | 3.094 us | 3.777 us | 1.09x |
| `lokad/lexer/identifier` | 23.869 us | 23.075 us | 26.743 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.622 us | 5.475 us | 16.889 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.328 us | 7.521 us | 8.109 us | 0.97x |
| `lokad/style/hex-color` | 0.767 us | 0.814 us | 0.886 us | 0.94x |
| `lokad/langserv/identifier-validator` | 11.440 us | 12.679 us | 14.558 us | 0.90x |
| `lokad/lexer/operator-run` | 3.158 us | 3.573 us | 4.001 us | 0.88x |
| `fallback/lookahead` | 5.406 us | 6.166 us | 17.197 us | 0.88x |
| `literal-family/method-token-family` | 7.205 us | 9.318 us | 19.855 us | 0.77x |
| `lokad/docs/inline-doc-prefix` | 3.400 us | 4.932 us | 5.635 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.063 us | 2.995 us | 3.046 us | 0.69x |
| `literal/identifier-token` | 2.792 us | 4.406 us | 15.679 us | 0.63x |
| `literal/call-token` | 2.605 us | 4.344 us | 15.550 us | 0.60x |
| `lokad/lexer/number` | 8.359 us | 14.456 us | 15.516 us | 0.58x |
| `structural/ordered-identifier-window` | 16.188 us | 28.746 us | 39.902 us | 0.56x |
| `lokad/style/cell-ref` | 1.535 us | 2.920 us | 3.029 us | 0.53x |
| `structural/method-family-generic-call` | 3.463 us | 6.913 us | 17.988 us | 0.50x |
| `structural/method-family-call` | 3.867 us | 8.136 us | 18.891 us | 0.48x |
| `literal-family/type-token-family` | 10.551 us | 23.614 us | 34.605 us | 0.45x |
| `literal/identifier-token-casei` | 5.718 us | 14.860 us | 26.422 us | 0.38x |
| `lokad/style/range-ref` | 0.912 us | 2.774 us | 2.816 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.480 us | 17.696 us | 28.505 us | 0.31x |
| `structural/ordered-keyword-window` | 5.515 us | 18.002 us | 29.409 us | 0.31x |
| `structural/modifier-family-to-type-window` | 78.963 us | 259.344 us | 271.742 us | 0.30x |
| `lokad/lexer/string` | 22.077 us | 98.154 us | 103.352 us | 0.22x |
| `lokad/folding/region-marker` | 6.448 us | 36.335 us | 61.310 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.600 us | 24.663 us | 25.013 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.537 us | 215.897 us | 227.200 us | 0.05x |
| `lokad/langserv/url-download` | 2.640 us | 52.522 us | 53.429 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.037 us | 136.244 us | 147.821 us | 0.04x |
| `balancing/begin-end-blocks` | 5.738 us | 4,368.944 us | 4,379.300 us | 0.0013x |
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
| `lokad/imports/module-imports` | 11.171 us | 4.443 us | 26.439 us | 2.51x |
| `lokad/langserv/color-short-hex` | 1.669 us | 1.038 us | 1.229 us | 1.61x |
| `structural/keyword-to-generic-type` | 5.524 us | 3.535 us | 14.668 us | 1.56x |
| `structural/ordered-identifier-window` | 16.193 us | 10.735 us | 21.376 us | 1.51x |
| `lokad/lexer/operator-run` | 3.155 us | 2.094 us | 2.706 us | 1.51x |
| `lokad/lexer/doc-line` | 3.368 us | 2.273 us | 3.019 us | 1.48x |
| `lokad/langserv/helper-identifier` | 7.254 us | 4.945 us | 6.064 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.480 us | 7.929 us | 18.502 us | 1.45x |
| `structural/modifier-family-to-type-window` | 79.828 us | 56.707 us | 69.317 us | 1.41x |
| `lokad/style/hex-color` | 0.739 us | 0.533 us | 0.632 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.623 us | 4.181 us | 14.988 us | 1.34x |
| `lokad/lexer/identifier` | 23.794 us | 17.940 us | 22.101 us | 1.33x |
| `fallback/lookahead` | 5.405 us | 4.246 us | 14.997 us | 1.27x |
| `lokad/langserv/identifier-validator` | 11.380 us | 9.468 us | 11.815 us | 1.20x |
| `lokad/docs/inline-doc-prefix` | 3.401 us | 2.920 us | 3.736 us | 1.16x |
| `lokad/lexer/number` | 8.371 us | 7.638 us | 8.591 us | 1.10x |
| `lokad/langserv/color-rgb-hex` | 2.065 us | 1.907 us | 2.109 us | 1.08x |
| `literal/identifier-token-casei` | 5.721 us | 5.922 us | 17.464 us | 0.97x |
| `literal-family/type-token-family` | 10.542 us | 11.635 us | 22.181 us | 0.91x |
| `lokad/style/cell-ref` | 1.535 us | 1.833 us | 2.005 us | 0.84x |
| `structural/method-family-generic-call` | 3.460 us | 4.524 us | 15.090 us | 0.76x |
| `structural/method-family-call` | 3.879 us | 5.135 us | 15.587 us | 0.76x |
| `literal-family/method-token-family` | 3.901 us | 5.351 us | 15.745 us | 0.73x |
| `literal/call-token` | 2.606 us | 4.022 us | 14.594 us | 0.65x |
| `structural/ordered-keyword-window` | 5.523 us | 8.662 us | 19.918 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.481 us | 8.707 us | 19.548 us | 0.63x |
| `lokad/style/range-ref` | 0.913 us | 1.525 us | 1.602 us | 0.60x |
| `literal/identifier-token` | 2.795 us | 4.764 us | 15.385 us | 0.59x |
| `lokad/lexer/string` | 22.054 us | 47.487 us | 50.985 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 5.025 us | 10.921 us | 22.102 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.558 us | 7.559 us | 8.084 us | 0.34x |
| `lokad/langserv/url-download` | 2.625 us | 13.115 us | 13.936 us | 0.20x |
| `lokad/folding/region-marker` | 6.468 us | 36.639 us | 60.416 us | 0.18x |
| `balancing/begin-end-blocks` | 5.731 us | 470.465 us | 499.487 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
