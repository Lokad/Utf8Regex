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
and resource checks passed in **46m30s**. The [run receipt](bench/Experiments/DeliveryDev58-20261109/full-refresh.json)
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
| `literal/sherlock-zh` | 82.987 us | 19.009 us | 2,182.256 us | 4.37x |
| `literal/sherlock-ru` | 164.955 us | 99.324 us | 5,117.907 us | 1.66x |
| `literal-alternate/sherlock-zh` | 105.037 us | 69.515 us | 2,198.841 us | 1.51x |
| `literal/sherlock-casei-en` | 163.533 us | 191.159 us | 588.363 us | 0.86x |
| `literal/sherlock-en` | 56.733 us | 87.054 us | 432.355 us | 0.65x |
| `literal/sherlock-casei-ru` | 255.274 us | 393.271 us | 5,690.438 us | 0.65x |
| `ruff-noqa/tweaked` | 4,420.329 us | 7,947.870 us | 43,373.150 us | 0.56x |
| `aws-keys/quick` | 4,855.300 us | 9,606.300 us | 41,241.950 us | 0.51x |
| `bounded-repeat/letters-en` | 1,124.996 us | 2,844.015 us | 2,948.712 us | 0.40x |
| `literal-alternate/sherlock-ru` | 1,671.811 us | 4,784.300 us | 10,184.975 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,973.579 us | 6,009.323 us | 6,815.864 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 189.942 us | 598.450 us | 1,009.252 us | 0.32x |
| `dictionary/single` | 8,294.970 us | 26,557.333 us | 26,215.567 us | 0.31x |
| `literal-alternate/sherlock-en` | 397.983 us | 1,509.144 us | 2,034.370 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 154.319 us | 669.069 us | 1,067.909 us | 0.23x |
| `bounded-repeat/capitals` | 9,537.413 us | 46,471.950 us | 49,935.700 us | 0.21x |
| `bounded-repeat/context` | 22,023.125 us | 148,980.900 us | 153,213.500 us | 0.15x |
| `aws-keys/full` | 10,703.843 us | 86,796.000 us | 120,256.200 us | 0.12x |
| `ruff-noqa/real` | 4,415.212 us | 70,585.700 us | 86,872.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 961.981 us | 23,209.150 us | 28,520.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 130.998 us | 11,655.814 us | 12,504.967 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,203.067 us | 513.141 us | 9,080.100 us | 2.34x |
| `common/match-word` | `Match` | 0.201 us | 0.167 us | 1.134 us | 1.20x |
| `industry/leipzig-twain-count` | `Count` | 1,164.194 us | 1,332.008 us | 8,493.020 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,443.219 us | 1,930.595 us | 10,556.725 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.112 us | 0.150 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.193 us | 0.211 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.086 us | 0.102 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.988 us | 265.025 us | 491.353 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.170 us | 0.187 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.176 us | 0.194 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,017.889 us | 9,898.987 us | 16,931.400 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.189 us | 0.209 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.239 us | 0.273 us | 0.39x |
| `common/replace-words` | `Replace` | 28.204 us | 76.674 us | 77.858 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.274 us | 0.302 us | 0.35x |
| `common/matches-word` | `Count` | 1.363 us | 4.317 us | 6.030 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.348 us | 0.375 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `industry/mariomka-ip-count` | `Count` | 2,230.912 us | 8,615.033 us | 17,163.633 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 180.354 us | 967.595 us | 1,277.958 us | 0.19x |
| `common/split-words` | `Split` | 14.622 us | 80.690 us | 82.352 us | 0.18x |
| `common/one-node-backtracking` | `IsMatch` | 0.096 us | 0.620 us | 0.656 us | 0.15x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.566 us | 0.605 us | 0.15x |
| `common/matches-words` | `Count` | 9.400 us | 72.386 us | 73.978 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.078 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.099 us | 0.858 us | 0.904 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,949.728 us | 19,623.875 us | 20,180.750 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,428.395 us | 34,602.700 us | 41,579.000 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,833.180 us | 23,062.750 us | 30,599.500 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 505.976 us | 8,713.133 us | 9,191.167 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.835 us | 165.386 us | 166.988 us | 0.05x |
| `common/matches-set` | `Count` | 7.873 us | 198.964 us | 200.464 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.592 us | 11,883.329 us | 12,429.583 us | 0.02x |
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
| `literal/sherlock-zh` | 83.021 us | 24.177 us | 2,202.956 us | 3.43x |
| `literal-alternate/sherlock-zh` | 104.952 us | 36.556 us | 2,143.403 us | 2.87x |
| `literal/sherlock-casei-en` | 163.556 us | 80.787 us | 428.557 us | 2.02x |
| `literal/sherlock-ru` | 165.104 us | 90.402 us | 5,131.673 us | 1.83x |
| `aws-keys/quick` | 4,858.175 us | 3,601.171 us | 36,066.000 us | 1.35x |
| `literal/sherlock-casei-ru` | 258.362 us | 223.712 us | 5,387.693 us | 1.15x |
| `literal-alternate/sherlock-en-mixed` | 186.287 us | 177.252 us | 546.778 us | 1.05x |
| `literal-alternate/sherlock-en` | 392.366 us | 373.704 us | 754.959 us | 1.05x |
| `ruff-noqa/tweaked` | 4,435.129 us | 4,585.953 us | 38,022.200 us | 0.97x |
| `bounded-repeat/letters-ru` | 1,971.623 us | 2,074.111 us | 2,868.123 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 130.682 us | 145.111 us | 555.182 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 154.489 us | 201.504 us | 565.819 us | 0.77x |
| `literal/sherlock-en` | 57.030 us | 76.256 us | 406.171 us | 0.75x |
| `bounded-repeat/capitals` | 9,552.438 us | 13,738.783 us | 16,783.900 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,623.087 us | 2,374.662 us | 7,556.975 us | 0.68x |
| `bounded-repeat/letters-en` | 1,128.364 us | 1,856.020 us | 1,944.328 us | 0.61x |
| `aws-keys/full` | 10,706.725 us | 18,980.825 us | 51,113.867 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 961.905 us | 1,976.026 us | 7,151.200 us | 0.49x |
| `bounded-repeat/context` | 20,563.925 us | 69,585.550 us | 72,949.050 us | 0.30x |
| `ruff-noqa/real` | 4,456.161 us | 21,135.900 us | 47,372.700 us | 0.21x |
| `dictionary/single` | 8,290.290 us | 46,018.700 us | 46,020.200 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,205.033 us | 478.775 us | 7,836.775 us | 2.52x |
| `common/match-word` | `Match` | 0.200 us | 0.080 us | 1.000 us | 2.50x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.752 us | 58.774 us | 253.257 us | 2.28x |
| `common/backtracking` | `IsMatch` | 0.099 us | 0.044 us | 0.074 us | 2.23x |
| `industry/leipzig-river-window-count` | `Count` | 3,443.955 us | 1,578.529 us | 7,969.075 us | 2.18x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.068 us | 0.086 us | 1.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.088 us | 1.64x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.031 us | 0.047 us | 1.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.032 us | 0.050 us | 1.59x |
| `common/replace-words` | `Replace` | 28.201 us | 18.280 us | 20.958 us | 1.54x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.050 us | 0.071 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,836.120 us | 1,243.710 us | 7,027.100 us | 1.48x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.052 us | 0.071 us | 1.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.078 us | 1.36x |
| `industry/leipzig-name-family-count` | `Count` | 4,029.184 us | 3,044.276 us | 8,343.250 us | 1.32x |
| `industry/mariomka-uri-count` | `Count` | 1,447.900 us | 1,113.018 us | 8,560.275 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.081 us | 0.108 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.089 us | 0.119 us | 1.12x |
| `common/one-node-backtracking` | `IsMatch` | 0.096 us | 0.089 us | 0.128 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.088 us | 0.121 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,163.843 us | 1,286.549 us | 7,344.273 us | 0.90x |
| `common/matches-words` | `Count` | 9.399 us | 11.021 us | 14.151 us | 0.85x |
| `common/split-words` | `Split` | 14.641 us | 22.053 us | 23.976 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.045 us | 0.074 us | 0.65x |
| `industry/mariomka-ip-count` | `Count` | 2,232.103 us | 3,505.223 us | 10,985.800 us | 0.64x |
| `common/matches-word` | `Count` | 1.362 us | 2.374 us | 3.539 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.199 us | 0.238 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.053 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,949.536 us | 8,932.878 us | 9,504.312 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 503.331 us | 3,978.711 us | 4,333.794 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.451 us | 1,476.680 us | 1,816.870 us | 0.12x |
| `common/matches-set` | `Count` | 7.847 us | 105.856 us | 107.733 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.814 us | 105.824 us | 107.431 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.590 us | 4,815.681 us | 5,209.560 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.270 us | 6.418 us | 29.169 us | 1.60x |
| `structural/keyword-to-generic-type` | 5.596 us | 4.122 us | 16.336 us | 1.36x |
| `lokad/langserv/color-short-hex` | 1.691 us | 1.377 us | 1.496 us | 1.23x |
| `lokad/lexer/doc-line` | 3.381 us | 3.050 us | 3.647 us | 1.11x |
| `lokad/lexer/identifier` | 23.901 us | 23.230 us | 26.294 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.377 us | 5.332 us | 17.324 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.234 us | 7.306 us | 7.863 us | 0.99x |
| `fallback/lookahead` | 5.714 us | 6.116 us | 17.427 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.412 us | 12.663 us | 14.164 us | 0.90x |
| `lokad/lexer/operator-run` | 2.925 us | 3.609 us | 3.894 us | 0.81x |
| `literal-family/method-token-family` | 7.215 us | 9.259 us | 19.891 us | 0.78x |
| `lokad/style/hex-color` | 0.611 us | 0.821 us | 0.883 us | 0.74x |
| `lokad/langserv/color-rgb-hex` | 2.067 us | 2.949 us | 3.023 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.382 us | 4.885 us | 5.543 us | 0.69x |
| `literal/identifier-token` | 2.800 us | 4.386 us | 15.596 us | 0.64x |
| `literal/call-token` | 2.608 us | 4.316 us | 15.645 us | 0.60x |
| `lokad/lexer/number` | 8.357 us | 14.593 us | 15.304 us | 0.57x |
| `structural/ordered-identifier-window` | 16.211 us | 28.854 us | 40.092 us | 0.56x |
| `structural/method-family-generic-call` | 3.450 us | 6.711 us | 17.878 us | 0.51x |
| `lokad/style/cell-ref` | 1.501 us | 2.961 us | 3.002 us | 0.51x |
| `structural/method-family-call` | 3.885 us | 8.231 us | 18.961 us | 0.47x |
| `literal-family/type-token-family` | 10.600 us | 24.337 us | 35.485 us | 0.44x |
| `literal/identifier-token-casei` | 6.266 us | 15.844 us | 26.646 us | 0.40x |
| `lokad/style/range-ref` | 0.913 us | 2.772 us | 2.751 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.450 us | 17.308 us | 28.134 us | 0.31x |
| `structural/modifier-family-to-type-window` | 79.904 us | 257.360 us | 270.351 us | 0.31x |
| `structural/ordered-keyword-window` | 5.450 us | 17.926 us | 29.032 us | 0.30x |
| `lokad/lexer/string` | 21.968 us | 95.818 us | 100.840 us | 0.23x |
| `lokad/folding/region-marker` | 6.140 us | 36.595 us | 59.920 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.585 us | 24.854 us | 25.302 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.518 us | 220.176 us | 232.336 us | 0.05x |
| `lokad/langserv/url-download` | 2.625 us | 52.987 us | 53.391 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.998 us | 130.888 us | 142.680 us | 0.04x |
| `balancing/begin-end-blocks` | 5.774 us | 4,380.088 us | 4,401.761 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.234 us | 4.454 us | 26.343 us | 2.30x |
| `lokad/langserv/color-short-hex` | 1.674 us | 1.031 us | 1.195 us | 1.62x |
| `structural/keyword-to-generic-type` | 5.629 us | 3.527 us | 15.270 us | 1.60x |
| `lokad/langserv/helper-identifier` | 7.274 us | 4.660 us | 5.714 us | 1.56x |
| `structural/ordered-identifier-window` | 16.226 us | 10.723 us | 21.939 us | 1.51x |
| `lokad/lexer/doc-line` | 3.365 us | 2.228 us | 2.967 us | 1.51x |
| `structural/keyword-family-to-capitalized-identifier` | 11.497 us | 7.936 us | 18.613 us | 1.45x |
| `lokad/lexer/operator-run` | 2.923 us | 2.035 us | 2.528 us | 1.44x |
| `structural/modifier-family-to-type-window` | 80.143 us | 56.239 us | 68.623 us | 1.43x |
| `lokad/lexer/identifier` | 23.950 us | 16.946 us | 21.150 us | 1.41x |
| `fallback/lookahead` | 5.712 us | 4.266 us | 15.218 us | 1.34x |
| `structural/ordered-keyword-pair` | 5.361 us | 4.158 us | 15.365 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.384 us | 9.207 us | 11.318 us | 1.24x |
| `lokad/style/hex-color` | 0.611 us | 0.523 us | 0.617 us | 1.17x |
| `lokad/docs/inline-doc-prefix` | 3.386 us | 2.903 us | 3.708 us | 1.17x |
| `lokad/lexer/number` | 8.370 us | 7.405 us | 8.611 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.073 us | 1.868 us | 2.108 us | 1.11x |
| `literal/identifier-token-casei` | 6.273 us | 5.922 us | 17.254 us | 1.06x |
| `literal-family/type-token-family` | 10.559 us | 11.555 us | 22.234 us | 0.91x |
| `lokad/style/cell-ref` | 1.498 us | 1.747 us | 1.917 us | 0.86x |
| `structural/method-family-call` | 3.889 us | 5.098 us | 15.465 us | 0.76x |
| `structural/method-family-generic-call` | 3.440 us | 4.539 us | 15.068 us | 0.76x |
| `literal-family/method-token-family` | 3.903 us | 5.357 us | 15.821 us | 0.73x |
| `literal/call-token` | 2.613 us | 4.008 us | 14.700 us | 0.65x |
| `structural/ordered-keyword-window` | 5.478 us | 8.651 us | 19.767 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.468 us | 8.644 us | 19.508 us | 0.63x |
| `lokad/style/range-ref` | 0.913 us | 1.499 us | 1.568 us | 0.61x |
| `literal/identifier-token` | 2.801 us | 4.757 us | 15.366 us | 0.59x |
| `lokad/lexer/string` | 21.935 us | 46.820 us | 49.884 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.993 us | 10.766 us | 21.692 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.589 us | 7.488 us | 8.099 us | 0.35x |
| `lokad/langserv/url-download` | 2.621 us | 13.017 us | 13.551 us | 0.20x |
| `lokad/folding/region-marker` | 6.142 us | 36.484 us | 60.160 us | 0.17x |
| `balancing/begin-end-blocks` | 5.774 us | 470.709 us | 499.203 us | 0.01x |
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
