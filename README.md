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

The tables were refreshed on **10 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **46m47s**. The [run receipt](bench/Experiments/DeliveryDev62-20261110/full-refresh.json)
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
| `literal/sherlock-zh` | 87.745 us | 19.064 us | 2,176.280 us | 4.60x |
| `literal/sherlock-ru` | 175.996 us | 98.465 us | 5,364.643 us | 1.79x |
| `literal-alternate/sherlock-zh` | 105.247 us | 70.348 us | 2,217.676 us | 1.50x |
| `literal/sherlock-casei-en` | 162.303 us | 201.743 us | 554.536 us | 0.80x |
| `literal/sherlock-en` | 57.023 us | 89.745 us | 474.884 us | 0.64x |
| `literal/sherlock-casei-ru` | 226.692 us | 395.960 us | 5,586.746 us | 0.57x |
| `ruff-noqa/tweaked` | 4,475.747 us | 8,065.150 us | 42,061.100 us | 0.55x |
| `aws-keys/quick` | 4,927.069 us | 9,638.962 us | 43,278.400 us | 0.51x |
| `bounded-repeat/letters-en` | 1,116.978 us | 2,852.856 us | 2,963.515 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,067.662 us | 6,011.054 us | 6,830.500 us | 0.34x |
| `literal-alternate/sherlock-ru` | 1,669.542 us | 4,934.425 us | 10,351.025 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 203.141 us | 624.886 us | 1,029.185 us | 0.33x |
| `dictionary/single` | 8,338.430 us | 26,477.467 us | 26,484.167 us | 0.31x |
| `literal-alternate/sherlock-en` | 390.850 us | 1,535.535 us | 2,022.405 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 154.946 us | 668.353 us | 1,077.032 us | 0.23x |
| `bounded-repeat/capitals` | 9,521.125 us | 46,672.750 us | 53,834.150 us | 0.20x |
| `aws-keys/full` | 16,809.129 us | 86,308.500 us | 120,650.200 us | 0.19x |
| `bounded-repeat/context` | 20,982.325 us | 147,503.800 us | 151,121.000 us | 0.14x |
| `ruff-noqa/real` | 4,556.212 us | 70,656.750 us | 88,109.000 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 965.687 us | 23,533.225 us | 28,890.500 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 132.380 us | 11,094.714 us | 11,942.800 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,186.619 us | 494.615 us | 9,030.025 us | 2.40x |
| `common/match-word` | `Match` | 0.201 us | 0.168 us | 1.130 us | 1.20x |
| `industry/leipzig-twain-count` | `Count` | 1,165.848 us | 1,329.912 us | 8,502.911 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,449.838 us | 1,968.559 us | 10,494.775 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.111 us | 0.149 us | 0.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.057 us | 0.085 us | 0.102 us | 0.67x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.194 us | 0.213 us | 0.61x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.822 us | 265.018 us | 499.508 us | 0.50x |
| `common/ip-match` | `IsMatch` | 0.088 us | 0.177 us | 0.197 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.169 us | 0.186 us | 0.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,065.426 us | 9,876.837 us | 17,249.833 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.188 us | 0.209 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.232 us | 0.267 us | 0.40x |
| `common/replace-words` | `Replace` | 28.554 us | 78.004 us | 79.338 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.273 us | 0.301 us | 0.35x |
| `common/matches-word` | `Count` | 1.354 us | 4.321 us | 6.036 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.102 us | 0.346 us | 0.370 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,219.403 us | 8,405.844 us | 16,853.700 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.111 us | 0.140 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 16.021 us | 79.690 us | 81.354 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.897 us | 973.787 us | 1,235.608 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.566 us | 0.604 us | 0.17x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.078 us | 0.092 us | 0.13x |
| `common/matches-words` | `Count` | 9.393 us | 71.769 us | 72.352 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.627 us | 0.663 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.844 us | 0.888 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,410.639 us | 34,294.100 us | 42,423.300 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,953.415 us | 19,849.075 us | 20,525.375 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,834.812 us | 23,148.775 us | 30,940.200 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 498.036 us | 8,325.150 us | 8,808.611 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.636 us | 167.061 us | 168.821 us | 0.05x |
| `common/matches-set` | `Count` | 7.669 us | 196.936 us | 198.616 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.835 us | 11,952.771 us | 12,563.557 us | 0.02x |
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
| `literal/sherlock-zh` | 87.607 us | 24.151 us | 2,225.582 us | 3.63x |
| `literal-alternate/sherlock-zh` | 105.383 us | 36.081 us | 2,148.114 us | 2.92x |
| `literal/sherlock-casei-en` | 162.167 us | 77.261 us | 424.305 us | 2.10x |
| `literal/sherlock-ru` | 175.918 us | 89.682 us | 5,257.460 us | 1.96x |
| `aws-keys/quick` | 4,927.125 us | 3,615.205 us | 35,990.000 us | 1.36x |
| `literal-alternate/sherlock-en-mixed` | 200.285 us | 176.587 us | 554.786 us | 1.13x |
| `literal-alternate/sherlock-en` | 390.305 us | 377.852 us | 765.507 us | 1.03x |
| `literal/sherlock-casei-ru` | 226.505 us | 223.551 us | 5,357.533 us | 1.01x |
| `literal-alternate/sherlock-casei-en` | 132.076 us | 132.563 us | 580.705 us | 1.00x |
| `bounded-repeat/letters-ru` | 2,063.103 us | 2,076.959 us | 2,883.808 us | 0.99x |
| `ruff-noqa/tweaked` | 4,483.694 us | 4,593.559 us | 37,242.450 us | 0.98x |
| `aws-keys/full` | 14,185.300 us | 18,644.040 us | 51,736.067 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 154.825 us | 204.344 us | 564.092 us | 0.76x |
| `literal/sherlock-en` | 57.142 us | 78.457 us | 399.558 us | 0.73x |
| `literal-alternate/sherlock-ru` | 1,688.176 us | 2,368.059 us | 7,468.450 us | 0.71x |
| `bounded-repeat/capitals` | 9,534.212 us | 13,732.950 us | 21,095.375 us | 0.69x |
| `bounded-repeat/letters-en` | 1,120.355 us | 1,844.763 us | 1,924.377 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 970.708 us | 1,989.997 us | 7,266.650 us | 0.49x |
| `bounded-repeat/context` | 20,502.925 us | 69,977.550 us | 73,483.700 us | 0.29x |
| `ruff-noqa/real` | 4,550.665 us | 21,609.950 us | 34,555.650 us | 0.21x |
| `dictionary/single` | 8,343.900 us | 46,125.650 us | 46,204.750 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,187.216 us | 459.959 us | 7,743.100 us | 2.58x |
| `common/match-word` | `Match` | 0.201 us | 0.080 us | 1.000 us | 2.50x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.384 us | 58.737 us | 279.251 us | 2.25x |
| `industry/leipzig-river-window-count` | `Count` | 3,418.168 us | 1,588.894 us | 8,178.325 us | 2.15x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.045 us | 0.074 us | 2.09x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.058 us | 0.031 us | 0.050 us | 1.87x |
| `common/ip-match` | `IsMatch` | 0.088 us | 0.049 us | 0.070 us | 1.81x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.068 us | 0.086 us | 1.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.088 us | 1.62x |
| `common/replace-words` | `Replace` | 28.563 us | 17.687 us | 20.567 us | 1.61x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.031 us | 0.047 us | 1.60x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.051 us | 0.071 us | 1.49x |
| `industry/leipzig-symbol-count` | `Count` | 1,837.340 us | 1,249.119 us | 7,291.160 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.078 us | 1.39x |
| `industry/mariomka-uri-count` | `Count` | 1,452.254 us | 1,100.639 us | 8,470.250 us | 1.32x |
| `industry/leipzig-name-family-count` | `Count` | 4,059.200 us | 3,105.848 us | 8,394.800 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.109 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.102 us | 0.090 us | 0.116 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.121 us | 1.02x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.089 us | 0.130 us | 0.91x |
| `industry/leipzig-twain-count` | `Count` | 1,166.874 us | 1,287.002 us | 7,394.133 us | 0.91x |
| `common/matches-words` | `Count` | 9.384 us | 11.077 us | 14.295 us | 0.85x |
| `common/split-words` | `Split` | 16.008 us | 21.751 us | 23.510 us | 0.74x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,222.376 us | 3,527.464 us | 10,944.133 us | 0.63x |
| `common/matches-word` | `Count` | 1.348 us | 2.379 us | 3.643 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.199 us | 0.236 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.041 us | 0.055 us | 0.31x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,953.433 us | 8,909.722 us | 9,458.087 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 496.921 us | 4,004.668 us | 4,354.822 us | 0.12x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.454 us | 1,503.904 us | 1,834.129 us | 0.12x |
| `common/matches-set` | `Count` | 7.692 us | 106.422 us | 108.396 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.652 us | 106.545 us | 108.596 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.521 us | 4,848.781 us | 5,248.633 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.146 us | 6.457 us | 29.141 us | 1.57x |
| `structural/keyword-to-generic-type` | 5.506 us | 4.130 us | 15.776 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.696 us | 1.364 us | 1.506 us | 1.24x |
| `lokad/lexer/doc-line` | 3.397 us | 3.047 us | 3.720 us | 1.12x |
| `lokad/lexer/identifier` | 24.472 us | 23.171 us | 25.867 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.598 us | 5.530 us | 16.912 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.361 us | 7.344 us | 7.957 us | 1.00x |
| `lokad/langserv/identifier-validator` | 11.375 us | 12.703 us | 14.291 us | 0.90x |
| `fallback/lookahead` | 5.446 us | 6.155 us | 17.765 us | 0.88x |
| `lokad/lexer/operator-run` | 2.913 us | 3.463 us | 3.913 us | 0.84x |
| `literal-family/method-token-family` | 7.197 us | 9.452 us | 20.384 us | 0.76x |
| `lokad/style/hex-color` | 0.610 us | 0.846 us | 0.893 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 3.397 us | 4.969 us | 5.688 us | 0.68x |
| `literal/identifier-token` | 2.797 us | 4.375 us | 16.044 us | 0.64x |
| `literal/call-token` | 2.596 us | 4.213 us | 15.574 us | 0.62x |
| `lokad/lexer/number` | 8.365 us | 14.241 us | 15.316 us | 0.59x |
| `structural/ordered-identifier-window` | 16.353 us | 29.192 us | 40.328 us | 0.56x |
| `structural/method-family-generic-call` | 3.475 us | 6.557 us | 17.850 us | 0.53x |
| `lokad/style/cell-ref` | 1.493 us | 2.967 us | 2.997 us | 0.50x |
| `lokad/langserv/color-rgb-hex` | 2.043 us | 4.224 us | 3.386 us | 0.48x |
| `structural/method-family-call` | 3.895 us | 8.341 us | 19.132 us | 0.47x |
| `literal-family/type-token-family` | 10.606 us | 24.079 us | 35.738 us | 0.44x |
| `literal/identifier-token-casei` | 6.172 us | 15.307 us | 27.079 us | 0.40x |
| `lokad/style/range-ref` | 0.925 us | 2.778 us | 2.860 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.553 us | 17.211 us | 28.406 us | 0.32x |
| `structural/modifier-family-to-type-window` | 80.106 us | 257.603 us | 270.737 us | 0.31x |
| `structural/ordered-keyword-window` | 5.541 us | 17.972 us | 29.803 us | 0.31x |
| `lokad/lexer/string` | 21.910 us | 98.069 us | 103.383 us | 0.22x |
| `lokad/folding/region-marker` | 6.151 us | 36.412 us | 60.727 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.526 us | 25.059 us | 25.565 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.448 us | 221.409 us | 235.023 us | 0.05x |
| `lokad/langserv/url-download` | 2.621 us | 51.405 us | 51.748 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.045 us | 131.873 us | 144.122 us | 0.04x |
| `balancing/begin-end-blocks` | 5.721 us | 4,251.306 us | 4,256.839 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.175 us | 4.450 us | 26.299 us | 2.29x |
| `lokad/langserv/color-short-hex` | 1.680 us | 0.985 us | 1.218 us | 1.71x |
| `structural/keyword-to-generic-type` | 5.509 us | 3.543 us | 14.686 us | 1.55x |
| `lokad/langserv/helper-identifier` | 7.363 us | 4.769 us | 5.911 us | 1.54x |
| `structural/ordered-identifier-window` | 16.358 us | 10.758 us | 21.847 us | 1.52x |
| `lokad/lexer/doc-line` | 3.399 us | 2.265 us | 3.065 us | 1.50x |
| `structural/keyword-family-to-capitalized-identifier` | 11.449 us | 7.969 us | 19.395 us | 1.44x |
| `lokad/lexer/operator-run` | 2.916 us | 2.033 us | 2.620 us | 1.43x |
| `structural/modifier-family-to-type-window` | 80.367 us | 56.258 us | 67.754 us | 1.43x |
| `lokad/lexer/identifier` | 24.455 us | 17.336 us | 20.671 us | 1.41x |
| `structural/ordered-keyword-pair` | 5.598 us | 4.189 us | 15.023 us | 1.34x |
| `fallback/lookahead` | 5.426 us | 4.260 us | 15.522 us | 1.27x |
| `lokad/langserv/identifier-validator` | 11.416 us | 9.347 us | 11.587 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.395 us | 2.880 us | 3.801 us | 1.18x |
| `lokad/style/hex-color` | 0.609 us | 0.526 us | 0.619 us | 1.16x |
| `lokad/lexer/number` | 8.366 us | 7.395 us | 8.634 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.042 us | 1.883 us | 2.092 us | 1.08x |
| `literal/identifier-token-casei` | 6.158 us | 5.944 us | 17.222 us | 1.04x |
| `literal-family/type-token-family` | 10.596 us | 11.579 us | 22.582 us | 0.92x |
| `lokad/style/cell-ref` | 1.492 us | 1.782 us | 1.937 us | 0.84x |
| `structural/method-family-generic-call` | 3.463 us | 4.551 us | 15.045 us | 0.76x |
| `structural/method-family-call` | 3.891 us | 5.118 us | 15.562 us | 0.76x |
| `literal-family/method-token-family` | 3.896 us | 5.350 us | 16.031 us | 0.73x |
| `literal/call-token` | 2.600 us | 3.988 us | 14.679 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.567 us | 8.693 us | 19.811 us | 0.64x |
| `structural/ordered-keyword-window` | 5.536 us | 8.701 us | 20.195 us | 0.64x |
| `lokad/style/range-ref` | 0.924 us | 1.554 us | 1.639 us | 0.59x |
| `literal/identifier-token` | 2.800 us | 4.730 us | 15.731 us | 0.59x |
| `lokad/lexer/string` | 21.920 us | 47.167 us | 51.035 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 5.036 us | 11.026 us | 22.421 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.529 us | 7.511 us | 8.093 us | 0.34x |
| `lokad/langserv/url-download` | 2.630 us | 13.118 us | 13.638 us | 0.20x |
| `lokad/folding/region-marker` | 6.166 us | 36.237 us | 60.527 us | 0.17x |
| `balancing/begin-end-blocks` | 5.726 us | 471.176 us | 499.948 us | 0.01x |
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
