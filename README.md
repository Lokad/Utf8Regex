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
and resource checks passed in **46m14s**. The [run receipt](bench/Experiments/DeliveryDev60-20261109/full-refresh.json)
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
| `literal/sherlock-zh` | 82.713 us | 19.020 us | 2,157.477 us | 4.35x |
| `literal/sherlock-ru` | 166.907 us | 99.737 us | 5,346.820 us | 1.67x |
| `literal-alternate/sherlock-zh` | 104.691 us | 69.299 us | 2,200.836 us | 1.51x |
| `literal/sherlock-casei-en` | 157.684 us | 194.979 us | 601.072 us | 0.81x |
| `literal/sherlock-en` | 58.174 us | 87.214 us | 462.329 us | 0.67x |
| `literal/sherlock-casei-ru` | 254.890 us | 396.994 us | 5,555.792 us | 0.64x |
| `ruff-noqa/tweaked` | 4,445.653 us | 7,653.355 us | 42,955.800 us | 0.58x |
| `aws-keys/quick` | 4,899.706 us | 9,753.750 us | 44,728.100 us | 0.50x |
| `bounded-repeat/letters-en` | 1,115.175 us | 2,829.300 us | 2,948.319 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,676.671 us | 4,821.712 us | 9,942.625 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,973.050 us | 5,967.500 us | 6,846.773 us | 0.33x |
| `dictionary/single` | 8,236.380 us | 26,450.900 us | 26,503.400 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 185.533 us | 600.797 us | 1,022.591 us | 0.31x |
| `literal-alternate/sherlock-en` | 391.239 us | 1,546.163 us | 2,049.108 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 154.527 us | 646.314 us | 1,056.195 us | 0.24x |
| `bounded-repeat/capitals` | 9,483.587 us | 45,483.150 us | 49,394.700 us | 0.21x |
| `bounded-repeat/context` | 20,429.775 us | 148,339.700 us | 152,549.800 us | 0.14x |
| `aws-keys/full` | 8,878.275 us | 87,570.900 us | 118,953.600 us | 0.10x |
| `ruff-noqa/real` | 4,372.883 us | 74,496.750 us | 90,747.600 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 951.887 us | 23,473.300 us | 28,746.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 141.852 us | 11,355.357 us | 12,280.800 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,181.723 us | 514.395 us | 9,071.550 us | 2.30x |
| `common/match-word` | `Match` | 0.209 us | 0.169 us | 1.128 us | 1.24x |
| `industry/leipzig-twain-count` | `Count` | 1,156.545 us | 1,320.677 us | 8,594.960 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,453.188 us | 1,895.872 us | 10,513.350 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.111 us | 0.150 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.210 us | 0.229 us | 0.56x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.026 us | 266.332 us | 487.554 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.169 us | 0.187 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.174 us | 0.195 us | 0.43x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.188 us | 0.207 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 3,983.789 us | 9,844.513 us | 16,871.700 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.236 us | 0.275 us | 0.39x |
| `common/replace-words` | `Replace` | 28.343 us | 78.698 us | 79.404 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.098 us | 0.274 us | 0.301 us | 0.36x |
| `common/matches-word` | `Count` | 1.343 us | 4.223 us | 5.989 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.347 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,204.160 us | 8,370.678 us | 16,791.167 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.109 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 14.690 us | 79.925 us | 81.709 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.751 us | 975.344 us | 1,239.935 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.568 us | 0.608 us | 0.15x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.090 us | 0.14x |
| `common/matches-words` | `Count` | 9.372 us | 70.160 us | 72.324 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.082 us | 0.622 us | 0.653 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.834 us | 0.874 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,339.878 us | 33,112.633 us | 40,101.000 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,944.454 us | 19,662.375 us | 20,140.825 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,832.366 us | 22,620.650 us | 29,993.850 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.767 us | 8,377.322 us | 8,890.600 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.464 us | 170.812 us | 172.560 us | 0.04x |
| `common/matches-set` | `Count` | 7.527 us | 192.877 us | 194.617 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 261.866 us | 11,762.586 us | 12,398.414 us | 0.02x |
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
| `literal/sherlock-zh` | 82.891 us | 24.173 us | 2,177.089 us | 3.43x |
| `literal-alternate/sherlock-zh` | 104.471 us | 36.229 us | 2,144.571 us | 2.88x |
| `literal/sherlock-casei-en` | 156.518 us | 76.664 us | 422.627 us | 2.04x |
| `literal/sherlock-ru` | 166.506 us | 90.442 us | 5,249.600 us | 1.84x |
| `aws-keys/quick` | 4,922.506 us | 3,593.657 us | 36,631.650 us | 1.37x |
| `literal/sherlock-casei-ru` | 255.589 us | 199.492 us | 5,451.820 us | 1.28x |
| `literal-alternate/sherlock-en` | 389.240 us | 344.005 us | 775.672 us | 1.13x |
| `literal-alternate/sherlock-en-mixed` | 185.904 us | 177.445 us | 534.950 us | 1.05x |
| `ruff-noqa/tweaked` | 4,404.771 us | 4,581.388 us | 37,114.300 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,969.608 us | 2,073.957 us | 2,896.127 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 141.383 us | 151.322 us | 544.637 us | 0.93x |
| `literal/sherlock-en` | 58.237 us | 76.800 us | 420.289 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 154.798 us | 206.556 us | 571.910 us | 0.75x |
| `bounded-repeat/capitals` | 9,519.987 us | 13,677.117 us | 16,545.200 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,639.636 us | 2,360.728 us | 7,504.325 us | 0.69x |
| `bounded-repeat/letters-en` | 1,114.634 us | 1,843.498 us | 1,917.892 us | 0.60x |
| `aws-keys/full` | 10,460.013 us | 18,505.180 us | 51,844.000 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 955.957 us | 1,980.692 us | 7,219.840 us | 0.48x |
| `bounded-repeat/context` | 20,422.725 us | 69,721.650 us | 73,246.000 us | 0.29x |
| `ruff-noqa/real` | 4,363.344 us | 21,045.900 us | 47,901.533 us | 0.21x |
| `dictionary/single` | 8,245.240 us | 46,059.200 us | 46,071.850 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.209 us | 0.080 us | 0.996 us | 2.61x |
| `industry/mariomka-email-count` | `Count` | 1,183.909 us | 478.313 us | 7,799.875 us | 2.48x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.826 us | 58.630 us | 267.753 us | 2.27x |
| `industry/leipzig-river-window-count` | `Count` | 3,359.422 us | 1,517.126 us | 7,888.260 us | 2.21x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.044 us | 0.074 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.066 us | 0.086 us | 1.77x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.048 us | 1.68x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.088 us | 1.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.032 us | 0.049 us | 1.60x |
| `common/replace-words` | `Replace` | 28.394 us | 17.703 us | 20.785 us | 1.60x |
| `common/ip-miss` | `IsMatch` | 0.079 us | 0.050 us | 0.070 us | 1.58x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.048 us | 0.070 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,844.761 us | 1,224.669 us | 6,996.025 us | 1.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,455.360 us | 1,112.323 us | 8,511.900 us | 1.31x |
| `industry/leipzig-name-family-count` | `Count` | 3,984.216 us | 3,061.592 us | 8,112.550 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.099 us | 0.082 us | 0.108 us | 1.20x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.117 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.127 us | 1.02x |
| `industry/leipzig-twain-count` | `Count` | 1,159.245 us | 1,279.961 us | 7,486.818 us | 0.91x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.087 us | 0.129 us | 0.89x |
| `common/matches-words` | `Count` | 9.365 us | 10.798 us | 13.955 us | 0.87x |
| `common/split-words` | `Split` | 14.665 us | 21.508 us | 23.472 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.073 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,206.149 us | 3,496.955 us | 10,857.200 us | 0.63x |
| `common/matches-word` | `Count` | 1.344 us | 2.367 us | 3.645 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.199 us | 0.236 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.036 us | 0.052 us | 0.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.933 us | 602.417 us | 844.086 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,943.562 us | 8,889.367 us | 9,466.200 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.960 us | 3,974.311 us | 4,340.772 us | 0.13x |
| `common/matches-set` | `Count` | 7.524 us | 106.854 us | 108.562 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.547 us | 107.960 us | 109.695 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 261.424 us | 4,814.387 us | 5,224.060 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.445 us | 6.454 us | 29.414 us | 1.62x |
| `structural/keyword-to-generic-type` | 5.385 us | 4.153 us | 16.026 us | 1.30x |
| `lokad/langserv/color-short-hex` | 1.679 us | 1.366 us | 1.472 us | 1.23x |
| `lokad/lexer/doc-line` | 3.374 us | 3.031 us | 3.639 us | 1.11x |
| `structural/ordered-keyword-pair` | 5.570 us | 5.347 us | 16.997 us | 1.04x |
| `lokad/lexer/identifier` | 23.764 us | 23.143 us | 25.506 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.292 us | 7.362 us | 7.830 us | 0.99x |
| `lokad/lexer/operator-run` | 3.268 us | 3.553 us | 3.891 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.482 us | 12.571 us | 14.233 us | 0.91x |
| `fallback/lookahead` | 5.396 us | 6.080 us | 17.546 us | 0.89x |
| `literal-family/method-token-family` | 7.190 us | 9.193 us | 20.068 us | 0.78x |
| `lokad/style/hex-color` | 0.607 us | 0.843 us | 0.906 us | 0.72x |
| `lokad/langserv/color-rgb-hex` | 2.068 us | 2.950 us | 3.025 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.380 us | 4.866 us | 5.506 us | 0.69x |
| `literal/identifier-token` | 2.798 us | 4.380 us | 15.593 us | 0.64x |
| `structural/ordered-identifier-window` | 17.675 us | 28.525 us | 40.039 us | 0.62x |
| `lokad/lexer/number` | 8.918 us | 14.786 us | 15.734 us | 0.60x |
| `literal/call-token` | 2.607 us | 4.333 us | 15.756 us | 0.60x |
| `structural/method-family-generic-call` | 3.450 us | 6.719 us | 18.107 us | 0.51x |
| `lokad/style/cell-ref` | 1.496 us | 3.054 us | 2.982 us | 0.49x |
| `structural/method-family-call` | 3.881 us | 8.426 us | 19.295 us | 0.46x |
| `literal-family/type-token-family` | 10.549 us | 23.469 us | 34.328 us | 0.45x |
| `literal/identifier-token-casei` | 5.788 us | 15.136 us | 26.731 us | 0.38x |
| `lokad/style/range-ref` | 0.912 us | 2.715 us | 2.708 us | 0.34x |
| `structural/modifier-family-to-type-window` | 79.699 us | 255.806 us | 268.981 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.541 us | 17.840 us | 29.300 us | 0.31x |
| `structural/ordered-keyword-window` | 5.475 us | 18.024 us | 28.944 us | 0.30x |
| `lokad/lexer/string` | 21.897 us | 95.831 us | 100.060 us | 0.23x |
| `lokad/folding/region-marker` | 6.072 us | 36.521 us | 60.144 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.587 us | 24.629 us | 25.133 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.469 us | 219.782 us | 230.583 us | 0.05x |
| `lokad/langserv/url-download` | 2.700 us | 52.452 us | 52.634 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.025 us | 131.580 us | 142.938 us | 0.04x |
| `balancing/begin-end-blocks` | 5.756 us | 4,363.278 us | 4,394.322 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.456 us | 4.437 us | 26.385 us | 2.36x |
| `lokad/langserv/color-short-hex` | 1.677 us | 0.975 us | 1.201 us | 1.72x |
| `structural/ordered-identifier-window` | 17.745 us | 10.583 us | 21.605 us | 1.68x |
| `lokad/lexer/operator-run` | 3.277 us | 2.013 us | 2.575 us | 1.63x |
| `lokad/langserv/helper-identifier` | 7.378 us | 4.650 us | 5.723 us | 1.59x |
| `structural/keyword-to-generic-type` | 5.377 us | 3.481 us | 14.911 us | 1.54x |
| `lokad/lexer/doc-line` | 3.371 us | 2.267 us | 3.009 us | 1.49x |
| `structural/keyword-family-to-capitalized-identifier` | 11.446 us | 7.896 us | 18.472 us | 1.45x |
| `lokad/lexer/identifier` | 23.877 us | 16.755 us | 20.311 us | 1.43x |
| `structural/modifier-family-to-type-window` | 79.670 us | 57.143 us | 69.233 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.576 us | 4.173 us | 15.239 us | 1.34x |
| `fallback/lookahead` | 5.415 us | 4.259 us | 15.278 us | 1.27x |
| `lokad/langserv/identifier-validator` | 11.520 us | 9.307 us | 11.297 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.378 us | 2.853 us | 3.551 us | 1.18x |
| `lokad/lexer/number` | 8.912 us | 7.660 us | 8.733 us | 1.16x |
| `lokad/style/hex-color` | 0.600 us | 0.531 us | 0.633 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.052 us | 1.840 us | 2.054 us | 1.12x |
| `literal/identifier-token-casei` | 5.799 us | 5.952 us | 17.151 us | 0.97x |
| `literal-family/type-token-family` | 10.558 us | 11.557 us | 22.235 us | 0.91x |
| `lokad/style/cell-ref` | 1.499 us | 1.752 us | 1.894 us | 0.86x |
| `structural/method-family-generic-call` | 3.449 us | 4.532 us | 15.281 us | 0.76x |
| `structural/method-family-call` | 3.886 us | 5.113 us | 15.738 us | 0.76x |
| `literal-family/method-token-family` | 3.909 us | 5.341 us | 16.018 us | 0.73x |
| `literal/call-token` | 2.617 us | 3.987 us | 14.897 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.543 us | 8.627 us | 19.596 us | 0.64x |
| `structural/ordered-keyword-window` | 5.445 us | 8.766 us | 19.481 us | 0.62x |
| `lokad/style/range-ref` | 0.913 us | 1.484 us | 1.594 us | 0.62x |
| `literal/identifier-token` | 2.806 us | 4.760 us | 15.362 us | 0.59x |
| `lokad/lexer/string` | 21.870 us | 45.683 us | 48.881 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 5.042 us | 10.739 us | 21.938 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.575 us | 7.533 us | 8.137 us | 0.34x |
| `lokad/langserv/url-download` | 2.627 us | 13.008 us | 13.594 us | 0.20x |
| `lokad/folding/region-marker` | 6.102 us | 36.352 us | 60.245 us | 0.17x |
| `balancing/begin-end-blocks` | 5.764 us | 469.061 us | 499.635 us | 0.01x |
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
