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
and resource checks passed in **46m51s**. The [run receipt](bench/Experiments/DeliveryDev64-20261110/full-refresh.json)
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
| `literal/sherlock-zh` | 82.955 us | 19.014 us | 2,217.517 us | 4.36x |
| `literal/sherlock-ru` | 167.969 us | 99.287 us | 5,409.579 us | 1.69x |
| `literal-alternate/sherlock-zh` | 105.022 us | 69.491 us | 2,161.459 us | 1.51x |
| `literal/sherlock-casei-en` | 163.252 us | 197.743 us | 598.866 us | 0.83x |
| `literal/sherlock-en` | 58.420 us | 87.229 us | 461.944 us | 0.67x |
| `literal/sherlock-casei-ru` | 254.956 us | 392.928 us | 5,631.454 us | 0.65x |
| `ruff-noqa/tweaked` | 4,503.059 us | 7,898.709 us | 43,246.200 us | 0.57x |
| `aws-keys/quick` | 4,877.306 us | 9,668.925 us | 44,918.200 us | 0.50x |
| `bounded-repeat/letters-en` | 1,122.657 us | 2,857.185 us | 2,966.835 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,684.189 us | 4,828.775 us | 10,262.100 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,991.492 us | 5,977.938 us | 6,791.225 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 198.484 us | 623.122 us | 1,022.265 us | 0.32x |
| `dictionary/single` | 8,339.710 us | 26,189.400 us | 26,251.333 us | 0.32x |
| `literal-alternate/sherlock-en` | 390.591 us | 1,534.510 us | 2,021.881 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 154.226 us | 664.268 us | 1,061.655 us | 0.23x |
| `bounded-repeat/capitals` | 9,499.538 us | 46,896.750 us | 50,075.900 us | 0.20x |
| `bounded-repeat/context` | 20,952.125 us | 149,371.400 us | 153,275.300 us | 0.14x |
| `aws-keys/full` | 10,656.211 us | 88,387.900 us | 119,523.400 us | 0.12x |
| `ruff-noqa/real` | 4,381.333 us | 70,797.100 us | 102,210.400 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 961.381 us | 23,257.850 us | 28,355.500 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 123.081 us | 11,275.771 us | 12,137.471 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,209.568 us | 512.497 us | 9,016.300 us | 2.36x |
| `common/match-word` | `Match` | 0.200 us | 0.169 us | 1.124 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,158.942 us | 1,323.458 us | 8,655.920 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,439.374 us | 1,957.385 us | 10,471.975 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.116 us | 0.154 us | 0.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.210 us | 0.230 us | 0.57x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.528 us | 266.719 us | 494.722 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.169 us | 0.188 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.181 us | 0.201 us | 0.42x |
| `common/replace-words` | `Replace` | 31.657 us | 76.954 us | 78.729 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.188 us | 0.207 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 4,071.647 us | 9,990.462 us | 16,847.200 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.235 us | 0.270 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.098 us | 0.270 us | 0.295 us | 0.36x |
| `common/matches-word` | `Count` | 1.355 us | 4.315 us | 6.064 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.371 us | 0.396 us | 0.27x |
| `industry/mariomka-ip-count` | `Count` | 2,238.597 us | 8,365.930 us | 16,908.600 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.703 us | 977.019 us | 1,258.495 us | 0.18x |
| `common/split-words` | `Split` | 14.750 us | 80.804 us | 82.198 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.569 us | 0.608 us | 0.17x |
| `common/matches-words` | `Count` | 9.383 us | 71.607 us | 73.439 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.619 us | 0.656 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.078 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.100 us | 0.844 us | 0.885 us | 0.12x |
| `industry/leipzig-river-window-count` | `Count` | 3,422.500 us | 32,628.400 us | 39,651.500 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,951.779 us | 19,641.350 us | 20,183.025 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,840.785 us | 22,947.175 us | 30,221.000 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 501.041 us | 8,538.767 us | 8,989.211 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.869 us | 171.343 us | 173.109 us | 0.05x |
| `common/matches-set` | `Count` | 7.532 us | 195.987 us | 197.792 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.467 us | 11,929.600 us | 12,498.986 us | 0.02x |
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
| `literal/sherlock-zh` | 82.980 us | 24.115 us | 2,208.818 us | 3.44x |
| `literal-alternate/sherlock-zh` | 105.131 us | 36.269 us | 2,128.637 us | 2.90x |
| `literal/sherlock-casei-en` | 162.990 us | 80.120 us | 429.925 us | 2.03x |
| `literal/sherlock-ru` | 167.994 us | 90.807 us | 5,357.621 us | 1.85x |
| `aws-keys/quick` | 4,875.725 us | 3,615.771 us | 36,469.650 us | 1.35x |
| `literal/sherlock-casei-ru` | 254.828 us | 222.958 us | 5,314.187 us | 1.14x |
| `literal-alternate/sherlock-en` | 387.943 us | 344.515 us | 732.471 us | 1.13x |
| `literal-alternate/sherlock-en-mixed` | 196.221 us | 178.400 us | 557.922 us | 1.10x |
| `ruff-noqa/tweaked` | 4,512.706 us | 4,629.306 us | 38,307.200 us | 0.97x |
| `bounded-repeat/letters-ru` | 1,995.642 us | 2,078.451 us | 2,891.438 us | 0.96x |
| `literal-alternate/sherlock-en-nomatch` | 154.254 us | 201.787 us | 622.899 us | 0.76x |
| `literal/sherlock-en` | 58.246 us | 76.847 us | 426.872 us | 0.76x |
| `literal-alternate/sherlock-ru` | 1,702.413 us | 2,366.197 us | 7,567.200 us | 0.72x |
| `bounded-repeat/capitals` | 9,511.250 us | 13,672.367 us | 16,589.175 us | 0.70x |
| `bounded-repeat/letters-en` | 1,122.576 us | 1,843.390 us | 1,929.631 us | 0.61x |
| `aws-keys/full` | 10,656.663 us | 18,571.300 us | 51,011.333 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 960.530 us | 1,977.003 us | 7,166.200 us | 0.49x |
| `literal-alternate/sherlock-casei-en` | 122.444 us | 291.832 us | 698.066 us | 0.42x |
| `bounded-repeat/context` | 20,826.575 us | 69,732.450 us | 73,091.300 us | 0.30x |
| `ruff-noqa/real` | 4,378.200 us | 21,962.900 us | 53,047.767 us | 0.20x |
| `dictionary/single` | 8,328.690 us | 46,166.550 us | 46,170.050 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.199 us | 0.077 us | 1.015 us | 2.58x |
| `industry/mariomka-email-count` | `Count` | 1,214.160 us | 478.575 us | 7,832.525 us | 2.54x |
| `common/backtracking` | `IsMatch` | 0.100 us | 0.043 us | 0.074 us | 2.32x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.828 us | 59.130 us | 262.850 us | 2.25x |
| `industry/leipzig-river-window-count` | `Count` | 3,429.657 us | 1,604.480 us | 7,960.700 us | 2.14x |
| `common/replace-words` | `Replace` | 31.598 us | 17.555 us | 20.457 us | 1.80x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.067 us | 0.086 us | 1.78x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.031 us | 0.048 us | 1.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.049 us | 0.088 us | 1.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.032 us | 0.049 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.049 us | 0.069 us | 1.57x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.050 us | 0.070 us | 1.51x |
| `industry/leipzig-symbol-count` | `Count` | 1,842.527 us | 1,231.505 us | 6,767.150 us | 1.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.35x |
| `industry/leipzig-name-family-count` | `Count` | 4,078.111 us | 3,019.140 us | 8,062.825 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,444.638 us | 1,102.079 us | 8,382.550 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.082 us | 0.109 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.104 us | 0.090 us | 0.116 us | 1.16x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.089 us | 0.120 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,178.084 us | 1,282.047 us | 6,293.840 us | 0.92x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.088 us | 0.129 us | 0.89x |
| `common/matches-words` | `Count` | 9.372 us | 11.231 us | 14.276 us | 0.83x |
| `common/split-words` | `Split` | 14.713 us | 21.626 us | 23.429 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,238.650 us | 3,476.595 us | 10,901.100 us | 0.64x |
| `common/matches-word` | `Count` | 1.349 us | 2.356 us | 3.511 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.198 us | 0.237 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,950.405 us | 8,786.400 us | 9,740.500 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 499.854 us | 3,976.974 us | 4,329.117 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.367 us | 1,505.788 us | 1,813.947 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.895 us | 106.274 us | 108.891 us | 0.07x |
| `common/matches-set` | `Count` | 7.540 us | 105.963 us | 107.473 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.041 us | 4,847.312 us | 5,228.400 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.587 us | 6.525 us | 29.293 us | 1.47x |
| `structural/keyword-to-generic-type` | 5.564 us | 4.122 us | 16.008 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.678 us | 1.382 us | 1.483 us | 1.21x |
| `lokad/lexer/doc-line` | 3.354 us | 3.066 us | 3.727 us | 1.09x |
| `lokad/lexer/identifier` | 24.059 us | 23.445 us | 26.096 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.377 us | 5.328 us | 17.294 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.226 us | 7.300 us | 7.652 us | 0.99x |
| `fallback/lookahead` | 5.628 us | 6.132 us | 17.129 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.400 us | 12.535 us | 13.584 us | 0.91x |
| `lokad/lexer/operator-run` | 2.963 us | 3.468 us | 3.837 us | 0.85x |
| `literal-family/method-token-family` | 7.167 us | 9.214 us | 19.860 us | 0.78x |
| `lokad/style/hex-color` | 0.610 us | 0.802 us | 0.842 us | 0.76x |
| `lokad/docs/inline-doc-prefix` | 3.393 us | 4.864 us | 5.397 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.034 us | 2.957 us | 2.941 us | 0.69x |
| `literal/identifier-token` | 2.819 us | 4.397 us | 15.775 us | 0.64x |
| `literal/call-token` | 2.593 us | 4.210 us | 15.593 us | 0.62x |
| `lokad/lexer/number` | 8.734 us | 14.628 us | 15.615 us | 0.60x |
| `structural/ordered-identifier-window` | 16.194 us | 31.343 us | 42.337 us | 0.52x |
| `structural/method-family-generic-call` | 3.462 us | 6.725 us | 17.923 us | 0.51x |
| `lokad/style/cell-ref` | 1.500 us | 2.931 us | 3.045 us | 0.51x |
| `structural/method-family-call` | 3.893 us | 8.466 us | 19.431 us | 0.46x |
| `literal-family/type-token-family` | 10.561 us | 25.066 us | 35.802 us | 0.42x |
| `literal/identifier-token-casei` | 6.167 us | 15.439 us | 27.191 us | 0.40x |
| `lokad/style/range-ref` | 0.923 us | 2.712 us | 2.657 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.541 us | 17.320 us | 28.383 us | 0.32x |
| `structural/ordered-keyword-window` | 5.500 us | 17.872 us | 28.844 us | 0.31x |
| `structural/modifier-family-to-type-window` | 78.785 us | 258.081 us | 269.675 us | 0.31x |
| `lokad/lexer/string` | 22.576 us | 95.952 us | 100.096 us | 0.24x |
| `lokad/folding/region-marker` | 6.045 us | 36.642 us | 60.443 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.561 us | 25.729 us | 26.059 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.475 us | 225.327 us | 237.497 us | 0.05x |
| `lokad/langserv/url-download` | 2.486 us | 52.345 us | 52.753 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.016 us | 135.189 us | 146.966 us | 0.04x |
| `balancing/begin-end-blocks` | 5.746 us | 4,404.778 us | 4,405.329 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.653 us | 4.481 us | 26.634 us | 2.15x |
| `lokad/langserv/color-short-hex` | 1.682 us | 1.007 us | 1.197 us | 1.67x |
| `structural/keyword-to-generic-type` | 5.575 us | 3.539 us | 14.897 us | 1.58x |
| `lokad/langserv/helper-identifier` | 7.232 us | 4.599 us | 5.592 us | 1.57x |
| `structural/ordered-identifier-window` | 16.209 us | 10.589 us | 21.329 us | 1.53x |
| `lokad/lexer/doc-line` | 3.356 us | 2.209 us | 2.943 us | 1.52x |
| `lokad/lexer/operator-run` | 2.956 us | 1.985 us | 2.549 us | 1.49x |
| `structural/keyword-family-to-capitalized-identifier` | 11.448 us | 7.870 us | 18.789 us | 1.45x |
| `lokad/lexer/identifier` | 24.067 us | 17.053 us | 21.120 us | 1.41x |
| `structural/modifier-family-to-type-window` | 78.846 us | 56.666 us | 69.073 us | 1.39x |
| `fallback/lookahead` | 5.635 us | 4.222 us | 14.899 us | 1.33x |
| `structural/ordered-keyword-pair` | 5.382 us | 4.151 us | 15.219 us | 1.30x |
| `lokad/langserv/identifier-validator` | 11.429 us | 8.946 us | 10.990 us | 1.28x |
| `lokad/style/hex-color` | 0.609 us | 0.502 us | 0.593 us | 1.21x |
| `lokad/docs/inline-doc-prefix` | 3.388 us | 2.822 us | 3.506 us | 1.20x |
| `lokad/lexer/number` | 8.734 us | 7.395 us | 8.503 us | 1.18x |
| `lokad/langserv/color-rgb-hex` | 2.026 us | 1.814 us | 2.054 us | 1.12x |
| `literal/identifier-token-casei` | 6.171 us | 5.950 us | 17.265 us | 1.04x |
| `literal-family/type-token-family` | 10.549 us | 11.583 us | 22.458 us | 0.91x |
| `lokad/style/cell-ref` | 1.503 us | 1.786 us | 1.929 us | 0.84x |
| `structural/method-family-call` | 3.894 us | 5.108 us | 15.770 us | 0.76x |
| `structural/method-family-generic-call` | 3.458 us | 4.537 us | 15.056 us | 0.76x |
| `literal-family/method-token-family` | 3.895 us | 5.348 us | 15.797 us | 0.73x |
| `literal/call-token` | 2.597 us | 4.006 us | 14.640 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.554 us | 8.636 us | 19.829 us | 0.64x |
| `structural/ordered-keyword-window` | 5.475 us | 8.676 us | 19.466 us | 0.63x |
| `lokad/style/range-ref` | 0.924 us | 1.480 us | 1.563 us | 0.62x |
| `literal/identifier-token` | 2.825 us | 4.737 us | 15.602 us | 0.60x |
| `lokad/lexer/string` | 22.600 us | 46.047 us | 48.268 us | 0.49x |
| `structural/modifier-family-to-type-pair` | 5.020 us | 10.757 us | 21.806 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.632 us | 7.533 us | 8.083 us | 0.35x |
| `lokad/langserv/url-download` | 2.525 us | 12.972 us | 13.572 us | 0.19x |
| `lokad/folding/region-marker` | 6.030 us | 36.723 us | 61.133 us | 0.16x |
| `balancing/begin-end-blocks` | 5.751 us | 469.807 us | 496.572 us | 0.01x |
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
