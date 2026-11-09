# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.

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
| [Lokad.Utf8Regex.Posix](src/Lokad.Utf8Regex.Posix/README.md) | POSIX ERE subset with leftmost-longest selection and hierarchical capture priority; new in 0.5.0 |

Adapters require the matching Core package version. Their specifications
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

The tables were refreshed on **9 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m02s**. The [run receipt](bench/Experiments/DeliveryDev57-20261109/full-refresh.json)
records the scope and timings; package and consumer qualification are separate.
Each row retains its recorded implementation. Selective updates do not imply a
new complete run; the full-run receipt applies to its recorded revision.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

Profile comparisons are in the
[PythonRe](src/Lokad.Utf8Regex.PythonRe/README.md#benchmarks),
[PCRE2](src/Lokad.Utf8Regex.Pcre2/README.md#benchmarks) and
[POSIX](src/Lokad.Utf8Regex.Posix/README.md#benchmarks) README sections.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 83.156 us | 18.990 us | 2,179.586 us | 4.38x |
| `literal/sherlock-ru` | 168.133 us | 99.225 us | 5,352.871 us | 1.69x |
| `literal-alternate/sherlock-zh` | 105.036 us | 68.891 us | 2,177.809 us | 1.52x |
| `literal/sherlock-casei-en` | 164.964 us | 204.029 us | 622.169 us | 0.81x |
| `aws-keys/quick` | 6,648.975 us | 9,762.200 us | 45,177.400 us | 0.68x |
| `literal/sherlock-en` | 57.595 us | 88.033 us | 491.970 us | 0.65x |
| `literal/sherlock-casei-ru` | 249.610 us | 396.324 us | 5,624.154 us | 0.63x |
| `ruff-noqa/tweaked` | 4,418.117 us | 7,841.773 us | 41,905.650 us | 0.56x |
| `bounded-repeat/letters-en` | 1,107.262 us | 2,833.074 us | 2,951.673 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,669.378 us | 4,833.919 us | 10,059.300 us | 0.35x |
| `bounded-repeat/letters-ru` | 2,036.954 us | 6,007.046 us | 6,823.518 us | 0.34x |
| `dictionary/single` | 8,377.450 us | 26,903.667 us | 26,917.767 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 183.779 us | 609.831 us | 1,009.144 us | 0.30x |
| `literal-alternate/sherlock-en` | 395.549 us | 1,527.220 us | 2,051.097 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 154.622 us | 652.426 us | 1,056.283 us | 0.24x |
| `bounded-repeat/capitals` | 9,479.850 us | 47,371.700 us | 51,000.400 us | 0.20x |
| `bounded-repeat/context` | 20,551.675 us | 150,222.800 us | 152,614.600 us | 0.14x |
| `aws-keys/full` | 10,699.143 us | 90,323.700 us | 123,604.900 us | 0.12x |
| `ruff-noqa/real` | 4,610.382 us | 73,925.300 us | 91,805.400 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 951.701 us | 23,302.725 us | 28,571.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 132.303 us | 11,363.986 us | 12,291.971 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,179.997 us | 488.983 us | 9,005.800 us | 2.41x |
| `common/match-word` | `Match` | 0.200 us | 0.168 us | 1.168 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,158.774 us | 1,325.127 us | 8,650.800 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,433.157 us | 1,879.412 us | 10,463.875 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.110 us | 0.148 us | 0.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.207 us | 0.228 us | 0.58x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.576 us | 268.401 us | 506.654 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.164 us | 0.180 us | 0.48x |
| `common/ip-miss` | `IsMatch` | 0.082 us | 0.188 us | 0.207 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.175 us | 0.194 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,073.321 us | 9,829.763 us | 17,256.033 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.233 us | 0.269 us | 0.40x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.274 us | 0.299 us | 0.35x |
| `common/replace-words` | `Replace` | 28.124 us | 80.782 us | 82.348 us | 0.35x |
| `common/matches-word` | `Count` | 1.350 us | 4.269 us | 6.035 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.102 us | 0.349 us | 0.374 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,221.909 us | 8,352.850 us | 16,922.600 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.138 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.708 us | 968.503 us | 1,230.438 us | 0.18x |
| `common/split-words` | `Split` | 14.608 us | 79.528 us | 81.448 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.563 us | 0.595 us | 0.17x |
| `common/matches-words` | `Count` | 9.440 us | 70.766 us | 72.568 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.615 us | 0.647 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.840 us | 0.879 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,429.517 us | 32,717.600 us | 40,841.200 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,955.215 us | 19,688.400 us | 20,227.975 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,837.427 us | 22,883.067 us | 30,553.200 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 514.679 us | 8,541.100 us | 9,054.322 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.869 us | 165.293 us | 166.945 us | 0.05x |
| `common/matches-set` | `Count` | 7.826 us | 193.924 us | 195.716 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 264.602 us | 11,992.486 us | 12,656.650 us | 0.02x |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 83.094 us | 24.143 us | 2,140.706 us | 3.44x |
| `literal-alternate/sherlock-zh` | 105.070 us | 36.476 us | 2,152.508 us | 2.88x |
| `literal/sherlock-casei-en` | 165.082 us | 80.975 us | 429.705 us | 2.04x |
| `literal/sherlock-ru` | 168.029 us | 89.861 us | 5,298.227 us | 1.87x |
| `aws-keys/quick` | 6,659.817 us | 3,700.748 us | 37,548.950 us | 1.80x |
| `literal/sherlock-casei-ru` | 250.440 us | 221.986 us | 5,383.373 us | 1.13x |
| `literal-alternate/sherlock-en` | 388.612 us | 348.398 us | 781.550 us | 1.12x |
| `literal-alternate/sherlock-en-mixed` | 182.180 us | 176.984 us | 554.108 us | 1.03x |
| `ruff-noqa/tweaked` | 4,514.800 us | 4,577.124 us | 36,812.550 us | 0.99x |
| `bounded-repeat/letters-ru` | 2,038.159 us | 2,082.892 us | 2,897.623 us | 0.98x |
| `literal-alternate/sherlock-casei-en` | 132.121 us | 145.555 us | 553.605 us | 0.91x |
| `literal/sherlock-en` | 57.420 us | 77.049 us | 414.053 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 154.658 us | 210.434 us | 583.984 us | 0.73x |
| `literal-alternate/sherlock-ru` | 1,691.909 us | 2,361.091 us | 7,515.100 us | 0.72x |
| `bounded-repeat/capitals` | 9,502.175 us | 13,706.817 us | 16,554.050 us | 0.69x |
| `bounded-repeat/letters-en` | 1,103.928 us | 1,849.083 us | 1,929.995 us | 0.60x |
| `aws-keys/full` | 9,396.737 us | 18,660.000 us | 52,678.333 us | 0.50x |
| `literal-alternate/sherlock-casei-ru` | 952.925 us | 1,989.713 us | 7,238.450 us | 0.48x |
| `bounded-repeat/context` | 20,473.125 us | 70,174.550 us | 73,313.250 us | 0.29x |
| `ruff-noqa/real` | 4,628.082 us | 20,828.000 us | 48,324.867 us | 0.22x |
| `dictionary/single` | 8,380.056 us | 46,128.800 us | 46,186.500 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,183.098 us | 454.827 us | 7,801.425 us | 2.60x |
| `common/match-word` | `Match` | 0.199 us | 0.080 us | 1.005 us | 2.48x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.581 us | 60.360 us | 260.134 us | 2.23x |
| `industry/leipzig-river-window-count` | `Count` | 3,434.795 us | 1,603.490 us | 8,237.950 us | 2.14x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.044 us | 0.074 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.067 us | 0.087 us | 1.77x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.049 us | 1.68x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.088 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.031 us | 0.047 us | 1.61x |
| `common/ip-miss` | `IsMatch` | 0.081 us | 0.051 us | 0.071 us | 1.60x |
| `common/replace-words` | `Replace` | 28.182 us | 18.042 us | 19.404 us | 1.56x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.049 us | 0.070 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,841.290 us | 1,252.327 us | 7,362.000 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.35x |
| `industry/leipzig-name-family-count` | `Count` | 4,077.858 us | 3,036.396 us | 8,774.675 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,435.787 us | 1,107.232 us | 8,446.575 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.082 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.116 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.089 us | 0.121 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,159.980 us | 1,282.793 us | 7,333.938 us | 0.90x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.088 us | 0.131 us | 0.89x |
| `common/matches-words` | `Count` | 9.440 us | 11.590 us | 14.759 us | 0.81x |
| `common/split-words` | `Split` | 14.602 us | 21.242 us | 23.147 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,221.183 us | 3,497.877 us | 10,939.067 us | 0.64x |
| `common/matches-word` | `Count` | 1.351 us | 2.374 us | 3.667 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.196 us | 0.235 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.053 us | 0.31x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,943.897 us | 8,905.633 us | 9,451.788 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 514.629 us | 3,981.268 us | 4,334.061 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.083 us | 1,500.239 us | 1,830.036 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.873 us | 106.842 us | 108.748 us | 0.07x |
| `common/matches-set` | `Count` | 7.812 us | 107.003 us | 108.708 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.418 us | 4,845.344 us | 5,251.753 us | 0.05x |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 10.436 us | 6.381 us | 29.002 us | 1.64x |
| `structural/keyword-to-generic-type` | 5.527 us | 4.110 us | 15.726 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.684 us | 1.389 us | 1.515 us | 1.21x |
| `lokad/lexer/doc-line` | 3.367 us | 3.067 us | 3.703 us | 1.10x |
| `structural/ordered-keyword-pair` | 5.588 us | 5.353 us | 17.004 us | 1.04x |
| `lokad/lexer/identifier` | 23.963 us | 23.090 us | 25.884 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.260 us | 7.396 us | 8.109 us | 0.98x |
| `lokad/langserv/identifier-validator` | 11.473 us | 12.532 us | 14.200 us | 0.92x |
| `lokad/style/hex-color` | 0.753 us | 0.847 us | 0.920 us | 0.89x |
| `fallback/lookahead` | 5.451 us | 6.173 us | 17.240 us | 0.88x |
| `lokad/lexer/operator-run` | 2.914 us | 3.561 us | 3.932 us | 0.82x |
| `literal-family/method-token-family` | 7.208 us | 9.309 us | 20.028 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.058 us | 2.949 us | 3.064 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.385 us | 4.876 us | 5.571 us | 0.69x |
| `literal/identifier-token` | 2.797 us | 4.429 us | 15.888 us | 0.63x |
| `literal/call-token` | 2.604 us | 4.338 us | 15.806 us | 0.60x |
| `lokad/lexer/number` | 8.341 us | 14.507 us | 15.353 us | 0.57x |
| `structural/ordered-identifier-window` | 16.197 us | 28.842 us | 40.116 us | 0.56x |
| `structural/method-family-generic-call` | 3.472 us | 6.752 us | 18.323 us | 0.51x |
| `lokad/style/cell-ref` | 1.499 us | 2.962 us | 3.018 us | 0.51x |
| `structural/method-family-call` | 3.894 us | 8.400 us | 19.485 us | 0.46x |
| `literal-family/type-token-family` | 10.586 us | 24.204 us | 35.174 us | 0.44x |
| `literal/identifier-token-casei` | 6.153 us | 15.315 us | 26.393 us | 0.40x |
| `lokad/style/range-ref` | 0.983 us | 2.789 us | 2.833 us | 0.35x |
| `structural/ordered-modifier-pair` | 5.436 us | 17.633 us | 28.677 us | 0.31x |
| `structural/modifier-family-to-type-window` | 79.008 us | 257.308 us | 270.855 us | 0.31x |
| `structural/ordered-keyword-window` | 5.513 us | 17.968 us | 29.168 us | 0.31x |
| `lokad/lexer/string` | 21.851 us | 96.773 us | 102.238 us | 0.23x |
| `lokad/folding/region-marker` | 6.063 us | 37.004 us | 59.923 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.539 us | 24.512 us | 25.074 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.542 us | 222.055 us | 234.148 us | 0.05x |
| `lokad/langserv/url-download` | 2.574 us | 50.937 us | 51.516 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.016 us | 136.946 us | 147.672 us | 0.04x |
| `balancing/begin-end-blocks` | 5.719 us | 4,429.059 us | 4,429.476 us | 0.0013x |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 10.467 us | 4.443 us | 26.233 us | 2.36x |
| `lokad/langserv/color-short-hex` | 1.678 us | 1.035 us | 1.221 us | 1.62x |
| `structural/keyword-to-generic-type` | 5.523 us | 3.487 us | 14.664 us | 1.58x |
| `lokad/langserv/helper-identifier` | 7.264 us | 4.826 us | 5.870 us | 1.50x |
| `structural/ordered-identifier-window` | 16.217 us | 10.796 us | 21.725 us | 1.50x |
| `lokad/lexer/doc-line` | 3.372 us | 2.264 us | 3.003 us | 1.49x |
| `structural/keyword-family-to-capitalized-identifier` | 11.482 us | 7.955 us | 18.813 us | 1.44x |
| `lokad/lexer/identifier` | 23.980 us | 16.665 us | 20.877 us | 1.44x |
| `lokad/lexer/operator-run` | 2.914 us | 2.051 us | 2.751 us | 1.42x |
| `lokad/style/hex-color` | 0.753 us | 0.537 us | 0.624 us | 1.40x |
| `structural/modifier-family-to-type-window` | 79.316 us | 56.658 us | 68.948 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.588 us | 4.174 us | 15.226 us | 1.34x |
| `fallback/lookahead` | 5.480 us | 4.246 us | 14.916 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.467 us | 8.945 us | 11.369 us | 1.28x |
| `lokad/docs/inline-doc-prefix` | 3.377 us | 2.865 us | 3.690 us | 1.18x |
| `lokad/lexer/number` | 8.345 us | 7.517 us | 8.759 us | 1.11x |
| `lokad/langserv/color-rgb-hex` | 2.053 us | 1.928 us | 2.110 us | 1.07x |
| `literal/identifier-token-casei` | 6.180 us | 5.907 us | 17.183 us | 1.05x |
| `literal-family/type-token-family` | 10.561 us | 11.573 us | 22.229 us | 0.91x |
| `lokad/style/cell-ref` | 1.500 us | 1.806 us | 2.002 us | 0.83x |
| `structural/method-family-generic-call` | 3.457 us | 4.533 us | 15.396 us | 0.76x |
| `structural/method-family-call` | 3.890 us | 5.119 us | 15.921 us | 0.76x |
| `literal-family/method-token-family` | 3.911 us | 5.370 us | 15.987 us | 0.73x |
| `literal/call-token` | 2.605 us | 3.988 us | 14.881 us | 0.65x |
| `lokad/style/range-ref` | 0.984 us | 1.513 us | 1.600 us | 0.65x |
| `structural/ordered-keyword-window` | 5.530 us | 8.677 us | 19.741 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.441 us | 8.659 us | 19.756 us | 0.63x |
| `literal/identifier-token` | 2.806 us | 4.731 us | 15.619 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 5.036 us | 10.780 us | 22.236 us | 0.47x |
| `lokad/lexer/string` | 21.907 us | 46.932 us | 49.656 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.553 us | 7.540 us | 8.118 us | 0.34x |
| `lokad/langserv/url-download` | 2.593 us | 13.046 us | 13.611 us | 0.20x |
| `lokad/folding/region-marker` | 6.063 us | 36.977 us | 60.800 us | 0.16x |
| `balancing/begin-end-blocks` | 5.729 us | 470.770 us | 500.273 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->

<details>
<summary>Regenerating benchmark tables</summary>

The compact summaries in `bench/Snapshots/` regenerate the README tables offline
through the existing `--rewrite-*-benchmark-markdown` commands. Ordinary tests need
no raw measurements or downloads. Regeneration preserves recorded observations;
it does not qualify the current engine or create a new benchmark run.

Refreshes and full evidence checks use local raw snapshots under
`artifacts/benchmark-refresh/current/`. `--export-benchmark-summaries` updates the
compact summaries from those files; `--rewrite-readme-parity-report` generates the
expanded Core report there on demand. Use `tools/bench.ps1`, build once and reuse
matching binaries with `-SkipBuild`. Keep or share raw outputs when useful; there
is no required archive service or upload step.

</details>
