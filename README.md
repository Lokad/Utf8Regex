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
and resource checks passed in **43m31s**. The [run receipt](bench/Experiments/DeliveryDev40-20261106/full-refresh.json)
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
| `literal/sherlock-zh` | 86.214 us | 18.959 us | 2,186.322 us | 4.55x |
| `literal/sherlock-ru` | 170.294 us | 103.323 us | 5,315.630 us | 1.65x |
| `literal-alternate/sherlock-zh` | 105.508 us | 68.472 us | 2,196.100 us | 1.54x |
| `literal/sherlock-casei-en` | 181.997 us | 192.881 us | 581.657 us | 0.94x |
| `literal/sherlock-en` | 57.516 us | 85.523 us | 488.545 us | 0.67x |
| `literal/sherlock-casei-ru` | 247.440 us | 395.148 us | 5,638.556 us | 0.63x |
| `ruff-noqa/tweaked` | 4,368.275 us | 7,847.714 us | 42,903.550 us | 0.56x |
| `aws-keys/quick` | 5,145.590 us | 10,253.740 us | 45,190.200 us | 0.50x |
| `bounded-repeat/letters-en` | 1,047.741 us | 2,812.383 us | 2,919.817 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,673.245 us | 4,796.864 us | 9,910.633 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,956.312 us | 6,029.922 us | 6,812.175 us | 0.32x |
| `dictionary/single` | 8,360.314 us | 26,489.500 us | 26,393.100 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 186.695 us | 624.918 us | 1,041.929 us | 0.30x |
| `literal-alternate/sherlock-en` | 400.943 us | 1,543.236 us | 2,040.185 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 154.898 us | 654.526 us | 1,055.769 us | 0.24x |
| `bounded-repeat/capitals` | 9,466.283 us | 46,063.850 us | 52,500.700 us | 0.21x |
| `bounded-repeat/context` | 24,135.700 us | 148,702.200 us | 152,515.400 us | 0.16x |
| `aws-keys/full` | 10,698.760 us | 87,063.800 us | 118,278.300 us | 0.12x |
| `ruff-noqa/real` | 4,575.955 us | 69,888.600 us | 86,534.600 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 952.315 us | 23,836.133 us | 29,035.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 133.081 us | 11,713.600 us | 12,530.050 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,194.867 us | 487.922 us | 8,857.167 us | 2.45x |
| `common/match-word` | `Match` | 0.200 us | 0.166 us | 1.156 us | 1.20x |
| `industry/leipzig-twain-count` | `Count` | 1,158.000 us | 1,342.298 us | 8,347.083 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,453.054 us | 1,951.258 us | 10,584.500 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.111 us | 0.148 us | 0.74x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.193 us | 0.213 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.138 us | 258.866 us | 480.558 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.168 us | 0.187 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.175 us | 0.194 us | 0.43x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.233 us | 0.264 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.188 us | 0.207 us | 0.40x |
| `industry/leipzig-name-family-count` | `Count` | 3,991.515 us | 9,910.067 us | 16,553.050 us | 0.40x |
| `common/email-match` | `IsMatch` | 0.101 us | 0.279 us | 0.304 us | 0.36x |
| `common/replace-words` | `Replace` | 28.535 us | 78.828 us | 82.336 us | 0.36x |
| `common/matches-word` | `Count` | 1.360 us | 4.295 us | 6.033 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.105 us | 0.351 us | 0.379 us | 0.30x |
| `industry/mariomka-ip-count` | `Count` | 2,319.105 us | 8,591.100 us | 17,066.900 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 14.579 us | 78.559 us | 80.440 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.869 us | 975.148 us | 1,257.681 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.564 us | 0.598 us | 0.17x |
| `common/matches-words` | `Count` | 9.393 us | 72.030 us | 73.143 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.090 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.619 us | 0.653 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.097 us | 0.868 us | 0.906 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,963.423 us | 19,603.433 us | 20,354.700 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,343.320 us | 33,414.600 us | 45,590.100 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,887.537 us | 23,345.100 us | 30,420.350 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 501.151 us | 8,763.167 us | 9,341.533 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.890 us | 164.756 us | 166.707 us | 0.05x |
| `common/matches-set` | `Count` | 7.849 us | 195.118 us | 196.532 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 248.101 us | 11,887.700 us | 12,413.420 us | 0.02x |
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
| `literal/sherlock-zh` | 85.907 us | 24.131 us | 2,179.835 us | 3.56x |
| `literal-alternate/sherlock-zh` | 105.356 us | 36.513 us | 2,162.667 us | 2.89x |
| `literal/sherlock-casei-en` | 181.638 us | 83.011 us | 446.395 us | 2.19x |
| `literal/sherlock-ru` | 170.510 us | 91.190 us | 5,263.920 us | 1.87x |
| `aws-keys/quick` | 5,138.410 us | 3,696.386 us | 37,616.450 us | 1.39x |
| `literal/sherlock-casei-ru` | 248.839 us | 225.092 us | 5,344.650 us | 1.11x |
| `literal-alternate/sherlock-en` | 399.349 us | 375.514 us | 793.952 us | 1.06x |
| `literal-alternate/sherlock-en-mixed` | 187.493 us | 177.784 us | 565.709 us | 1.05x |
| `ruff-noqa/tweaked` | 4,379.258 us | 4,588.100 us | 37,334.500 us | 0.95x |
| `bounded-repeat/letters-ru` | 1,948.935 us | 2,075.296 us | 2,884.306 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 132.318 us | 146.514 us | 582.325 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 154.921 us | 202.862 us | 588.874 us | 0.76x |
| `literal/sherlock-en` | 57.329 us | 75.943 us | 422.863 us | 0.75x |
| `bounded-repeat/capitals` | 9,476.000 us | 13,715.000 us | 20,400.133 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,629.423 us | 2,368.168 us | 7,424.600 us | 0.69x |
| `aws-keys/full` | 10,686.160 us | 18,826.167 us | 51,106.950 us | 0.57x |
| `bounded-repeat/letters-en` | 1,044.061 us | 1,856.732 us | 1,935.885 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 956.204 us | 1,981.519 us | 7,172.133 us | 0.48x |
| `bounded-repeat/context` | 24,661.633 us | 69,765.400 us | 71,940.200 us | 0.35x |
| `ruff-noqa/real` | 4,656.200 us | 21,118.200 us | 34,083.750 us | 0.22x |
| `dictionary/single` | 8,338.600 us | 46,342.300 us | 46,274.200 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,196.555 us | 455.921 us | 7,802.167 us | 2.62x |
| `common/match-word` | `Match` | 0.199 us | 0.077 us | 1.013 us | 2.57x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.972 us | 59.327 us | 281.200 us | 2.26x |
| `industry/leipzig-river-window-count` | `Count` | 3,354.331 us | 1,515.821 us | 13,694.300 us | 2.21x |
| `common/backtracking` | `IsMatch` | 0.097 us | 0.045 us | 0.075 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.068 us | 0.088 us | 1.77x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.029 us | 0.047 us | 1.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.050 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.049 us | 0.088 us | 1.65x |
| `common/replace-words` | `Replace` | 28.495 us | 17.844 us | 21.154 us | 1.60x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.048 us | 0.068 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.051 us | 0.073 us | 1.48x |
| `industry/leipzig-symbol-count` | `Count` | 1,891.170 us | 1,279.662 us | 6,883.933 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.057 us | 0.077 us | 1.36x |
| `industry/leipzig-name-family-count` | `Count` | 3,995.869 us | 3,033.876 us | 8,234.933 us | 1.32x |
| `industry/mariomka-uri-count` | `Count` | 1,450.417 us | 1,117.602 us | 8,592.500 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.102 us | 0.081 us | 0.109 us | 1.25x |
| `common/email-miss` | `IsMatch` | 0.104 us | 0.089 us | 0.116 us | 1.17x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.088 us | 0.121 us | 1.06x |
| `industry/leipzig-twain-count` | `Count` | 1,158.075 us | 1,280.315 us | 7,015.571 us | 0.90x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.089 us | 0.127 us | 0.90x |
| `common/matches-words` | `Count` | 9.379 us | 11.034 us | 14.081 us | 0.85x |
| `common/split-words` | `Split` | 14.618 us | 21.487 us | 23.458 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,319.377 us | 3,526.820 us | 11,157.750 us | 0.66x |
| `common/matches-word` | `Count` | 1.352 us | 2.377 us | 3.701 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.207 us | 0.240 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.037 us | 0.053 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,975.854 us | 8,930.400 us | 9,837.600 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 497.723 us | 4,000.192 us | 4,409.250 us | 0.12x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.069 us | 1,471.597 us | 1,804.743 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.850 us | 105.477 us | 107.177 us | 0.07x |
| `common/matches-set` | `Count` | 7.893 us | 106.289 us | 108.184 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 238.407 us | 4,846.491 us | 5,238.010 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.686 us | 6.387 us | 29.541 us | 1.83x |
| `structural/keyword-to-generic-type` | 5.504 us | 4.123 us | 15.858 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.673 us | 1.350 us | 1.456 us | 1.24x |
| `lokad/lexer/doc-line` | 3.485 us | 2.997 us | 3.602 us | 1.16x |
| `lokad/lexer/identifier` | 23.872 us | 22.597 us | 25.473 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.560 us | 5.306 us | 17.383 us | 1.05x |
| `lokad/langserv/helper-identifier` | 7.160 us | 7.089 us | 7.841 us | 1.01x |
| `lokad/langserv/identifier-validator` | 11.332 us | 12.317 us | 13.989 us | 0.92x |
| `fallback/lookahead` | 5.441 us | 6.169 us | 17.121 us | 0.88x |
| `lokad/lexer/operator-run` | 2.894 us | 3.357 us | 3.816 us | 0.86x |
| `literal-family/method-token-family` | 7.181 us | 9.287 us | 20.197 us | 0.77x |
| `lokad/style/hex-color` | 0.611 us | 0.795 us | 0.871 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.093 us | 2.943 us | 3.020 us | 0.71x |
| `lokad/docs/inline-doc-prefix` | 3.354 us | 4.773 us | 5.501 us | 0.70x |
| `literal/identifier-token` | 2.801 us | 4.408 us | 15.655 us | 0.64x |
| `lokad/lexer/number` | 8.444 us | 13.738 us | 15.014 us | 0.61x |
| `literal/call-token` | 2.604 us | 4.356 us | 15.802 us | 0.60x |
| `structural/ordered-identifier-window` | 16.174 us | 28.749 us | 40.472 us | 0.56x |
| `lokad/style/cell-ref` | 1.498 us | 2.896 us | 3.054 us | 0.52x |
| `structural/method-family-generic-call` | 3.457 us | 6.815 us | 18.111 us | 0.51x |
| `structural/method-family-call` | 3.854 us | 8.076 us | 19.090 us | 0.48x |
| `literal-family/type-token-family` | 10.550 us | 23.981 us | 35.160 us | 0.44x |
| `literal/identifier-token-casei` | 6.217 us | 15.900 us | 26.929 us | 0.39x |
| `lokad/style/range-ref` | 0.914 us | 2.708 us | 2.744 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.475 us | 17.283 us | 28.265 us | 0.32x |
| `structural/ordered-keyword-window` | 5.520 us | 17.797 us | 29.012 us | 0.31x |
| `structural/modifier-family-to-type-window` | 79.362 us | 256.099 us | 269.278 us | 0.31x |
| `lokad/lexer/string` | 21.761 us | 95.001 us | 100.484 us | 0.23x |
| `lokad/folding/region-marker` | 6.449 us | 37.036 us | 61.901 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.693 us | 24.610 us | 24.997 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.484 us | 215.573 us | 227.236 us | 0.05x |
| `lokad/langserv/url-download` | 2.606 us | 52.254 us | 52.514 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.957 us | 128.326 us | 140.072 us | 0.04x |
| `balancing/begin-end-blocks` | 5.713 us | 4,619.273 us | 4,621.936 us | 0.0012x |
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
| `lokad/imports/module-imports` | 11.713 us | 4.420 us | 26.591 us | 2.65x |
| `lokad/langserv/color-short-hex` | 1.666 us | 1.008 us | 1.215 us | 1.65x |
| `lokad/lexer/doc-line` | 3.497 us | 2.147 us | 2.923 us | 1.63x |
| `structural/keyword-to-generic-type` | 5.504 us | 3.541 us | 14.707 us | 1.55x |
| `lokad/langserv/helper-identifier` | 7.160 us | 4.739 us | 5.756 us | 1.51x |
| `structural/ordered-identifier-window` | 16.143 us | 10.717 us | 21.862 us | 1.51x |
| `lokad/lexer/operator-run` | 2.921 us | 1.990 us | 2.567 us | 1.47x |
| `lokad/lexer/identifier` | 23.905 us | 16.513 us | 20.021 us | 1.45x |
| `structural/keyword-family-to-capitalized-identifier` | 11.457 us | 7.914 us | 18.506 us | 1.45x |
| `structural/modifier-family-to-type-window` | 79.505 us | 56.565 us | 68.691 us | 1.41x |
| `structural/ordered-keyword-pair` | 5.580 us | 4.132 us | 15.283 us | 1.35x |
| `fallback/lookahead` | 5.422 us | 4.249 us | 14.926 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.312 us | 9.158 us | 12.069 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.347 us | 2.852 us | 3.544 us | 1.17x |
| `lokad/lexer/number` | 8.404 us | 7.163 us | 8.224 us | 1.17x |
| `lokad/style/hex-color` | 0.611 us | 0.529 us | 0.593 us | 1.15x |
| `lokad/langserv/color-rgb-hex` | 2.085 us | 1.899 us | 2.111 us | 1.10x |
| `literal/identifier-token-casei` | 6.189 us | 5.936 us | 17.488 us | 1.04x |
| `literal-family/type-token-family` | 10.533 us | 11.537 us | 22.473 us | 0.91x |
| `lokad/style/cell-ref` | 1.499 us | 1.771 us | 1.935 us | 0.85x |
| `structural/method-family-generic-call` | 3.442 us | 4.518 us | 15.230 us | 0.76x |
| `structural/method-family-call` | 3.858 us | 5.078 us | 15.700 us | 0.76x |
| `literal-family/method-token-family` | 3.905 us | 5.342 us | 15.964 us | 0.73x |
| `literal/call-token` | 2.613 us | 3.990 us | 14.868 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.536 us | 8.648 us | 19.556 us | 0.64x |
| `structural/ordered-keyword-window` | 5.499 us | 8.683 us | 19.574 us | 0.63x |
| `lokad/style/range-ref` | 0.916 us | 1.512 us | 1.622 us | 0.61x |
| `literal/identifier-token` | 2.800 us | 4.745 us | 15.339 us | 0.59x |
| `lokad/lexer/string` | 21.661 us | 45.986 us | 48.511 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.937 us | 10.866 us | 21.464 us | 0.45x |
| `lokad/langserv/url-dashboard` | 2.567 us | 7.494 us | 8.003 us | 0.34x |
| `lokad/langserv/url-download` | 2.615 us | 12.772 us | 13.777 us | 0.20x |
| `lokad/folding/region-marker` | 6.451 us | 37.095 us | 59.323 us | 0.17x |
| `balancing/begin-end-blocks` | 5.735 us | 467.481 us | 495.013 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
