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
and resource checks passed in **47m17s**. The [run receipt](bench/Experiments/DeliveryDev59-20261109/full-refresh.json)
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
| `literal/sherlock-zh` | 83.116 us | 19.007 us | 2,193.176 us | 4.37x |
| `literal/sherlock-ru` | 170.922 us | 100.181 us | 5,470.629 us | 1.71x |
| `literal-alternate/sherlock-zh` | 104.872 us | 70.258 us | 2,177.220 us | 1.49x |
| `literal/sherlock-casei-en` | 163.837 us | 190.161 us | 592.125 us | 0.86x |
| `literal/sherlock-en` | 56.725 us | 86.133 us | 425.115 us | 0.66x |
| `literal/sherlock-casei-ru` | 258.607 us | 393.402 us | 5,667.800 us | 0.66x |
| `ruff-noqa/tweaked` | 4,401.259 us | 7,879.455 us | 42,433.050 us | 0.56x |
| `aws-keys/quick` | 5,058.093 us | 9,674.212 us | 45,129.450 us | 0.52x |
| `bounded-repeat/letters-en` | 1,156.000 us | 2,821.800 us | 2,935.296 us | 0.41x |
| `literal-alternate/sherlock-ru` | 1,684.740 us | 4,797.275 us | 10,132.350 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 198.097 us | 594.506 us | 1,021.059 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,983.855 us | 5,977.723 us | 6,778.992 us | 0.33x |
| `dictionary/single` | 8,367.240 us | 26,433.300 us | 26,432.567 us | 0.32x |
| `literal-alternate/sherlock-en` | 387.534 us | 1,505.967 us | 2,001.645 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 154.318 us | 676.979 us | 1,088.397 us | 0.23x |
| `bounded-repeat/capitals` | 9,469.425 us | 47,302.600 us | 51,010.450 us | 0.20x |
| `bounded-repeat/context` | 20,608.025 us | 149,611.300 us | 153,538.600 us | 0.14x |
| `aws-keys/full` | 8,842.957 us | 87,566.500 us | 120,789.700 us | 0.10x |
| `ruff-noqa/real` | 4,388.689 us | 70,785.450 us | 86,293.700 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 950.695 us | 23,464.650 us | 28,863.200 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 124.795 us | 11,615.771 us | 12,486.871 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,173.352 us | 491.378 us | 9,001.050 us | 2.39x |
| `common/match-word` | `Match` | 0.199 us | 0.168 us | 1.177 us | 1.18x |
| `industry/leipzig-twain-count` | `Count` | 1,162.043 us | 1,334.814 us | 8,646.020 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,449.213 us | 1,908.483 us | 10,603.300 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.112 us | 0.151 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.193 us | 0.213 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.086 us | 0.103 us | 0.59x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.609 us | 262.138 us | 505.111 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.164 us | 0.182 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.176 us | 0.196 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,030.063 us | 10,133.413 us | 17,375.500 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.232 us | 0.269 us | 0.39x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.194 us | 0.215 us | 0.39x |
| `common/replace-words` | `Replace` | 28.443 us | 76.433 us | 79.250 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.278 us | 0.304 us | 0.35x |
| `common/matches-word` | `Count` | 1.352 us | 4.248 us | 6.012 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.102 us | 0.349 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,229.435 us | 8,390.822 us | 16,861.100 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.112 us | 0.141 us | 0.26x |
| `common/split-words` | `Split` | 15.989 us | 78.173 us | 80.112 us | 0.20x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.100 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 180.547 us | 971.185 us | 1,233.028 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.564 us | 0.599 us | 0.15x |
| `common/one-node-backtracking` | `IsMatch` | 0.095 us | 0.620 us | 0.657 us | 0.15x |
| `common/matches-words` | `Count` | 9.408 us | 70.524 us | 72.362 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.837 us | 0.877 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,382.939 us | 33,378.300 us | 40,979.250 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,951.815 us | 19,808.275 us | 20,394.100 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,830.526 us | 23,005.300 us | 30,483.350 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 477.437 us | 8,277.300 us | 8,795.722 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.646 us | 165.506 us | 166.871 us | 0.05x |
| `common/matches-set` | `Count` | 7.557 us | 196.025 us | 197.259 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 238.993 us | 11,931.457 us | 12,542.971 us | 0.02x |
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
| `literal/sherlock-zh` | 83.057 us | 24.020 us | 2,260.091 us | 3.46x |
| `literal-alternate/sherlock-zh` | 104.886 us | 36.048 us | 2,158.386 us | 2.91x |
| `literal/sherlock-casei-en` | 163.647 us | 79.812 us | 425.514 us | 2.05x |
| `literal/sherlock-ru` | 171.082 us | 91.683 us | 5,409.564 us | 1.87x |
| `aws-keys/quick` | 5,053.387 us | 3,699.176 us | 37,660.150 us | 1.37x |
| `literal/sherlock-casei-ru` | 256.560 us | 223.398 us | 5,496.307 us | 1.15x |
| `literal-alternate/sherlock-en` | 385.216 us | 346.387 us | 742.727 us | 1.11x |
| `literal-alternate/sherlock-en-mixed` | 196.968 us | 188.576 us | 532.416 us | 1.04x |
| `bounded-repeat/letters-ru` | 1,989.900 us | 2,073.365 us | 2,860.488 us | 0.96x |
| `ruff-noqa/tweaked` | 4,420.494 us | 4,610.200 us | 37,554.700 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 124.583 us | 132.072 us | 548.037 us | 0.94x |
| `literal/sherlock-en` | 56.804 us | 75.400 us | 441.331 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 154.600 us | 206.549 us | 576.292 us | 0.75x |
| `literal-alternate/sherlock-ru` | 1,644.787 us | 2,365.238 us | 7,495.925 us | 0.70x |
| `bounded-repeat/capitals` | 9,476.550 us | 13,723.250 us | 16,656.275 us | 0.69x |
| `bounded-repeat/letters-en` | 1,157.127 us | 1,847.112 us | 1,931.256 us | 0.63x |
| `aws-keys/full` | 10,424.800 us | 18,605.500 us | 51,206.500 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 950.119 us | 1,976.064 us | 7,264.100 us | 0.48x |
| `bounded-repeat/context` | 20,539.775 us | 69,945.600 us | 73,551.550 us | 0.29x |
| `ruff-noqa/real` | 4,401.622 us | 21,026.550 us | 47,715.667 us | 0.21x |
| `dictionary/single` | 8,365.533 us | 46,208.050 us | 46,301.800 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,176.803 us | 455.702 us | 7,740.650 us | 2.58x |
| `common/match-word` | `Match` | 0.199 us | 0.081 us | 1.006 us | 2.47x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.963 us | 59.160 us | 275.091 us | 2.26x |
| `industry/leipzig-river-window-count` | `Count` | 3,395.648 us | 1,588.643 us | 8,266.640 us | 2.14x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.045 us | 0.075 us | 2.05x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.068 us | 0.089 us | 1.75x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.029 us | 0.048 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.050 us | 1.68x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.088 us | 1.62x |
| `common/replace-words` | `Replace` | 28.417 us | 17.895 us | 20.877 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.048 us | 0.069 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.050 us | 0.071 us | 1.51x |
| `industry/leipzig-symbol-count` | `Count` | 1,839.341 us | 1,234.157 us | 7,436.300 us | 1.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.055 us | 0.078 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,116.394 us | 3,106.056 us | 8,568.775 us | 1.33x |
| `industry/mariomka-uri-count` | `Count` | 1,448.269 us | 1,110.854 us | 8,569.400 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.108 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.116 us | 1.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.095 us | 0.089 us | 0.129 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.090 us | 0.120 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,163.757 us | 1,294.580 us | 7,584.727 us | 0.90x |
| `common/matches-words` | `Count` | 9.395 us | 10.848 us | 13.968 us | 0.87x |
| `common/split-words` | `Split` | 15.997 us | 21.724 us | 23.522 us | 0.74x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,228.485 us | 3,506.605 us | 10,970.900 us | 0.64x |
| `common/matches-word` | `Count` | 1.352 us | 2.356 us | 3.665 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.199 us | 0.240 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,944.100 us | 8,931.444 us | 9,466.275 us | 0.22x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.577 us | 1,499.733 us | 1,837.531 us | 0.12x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 472.618 us | 3,986.863 us | 4,356.078 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.637 us | 106.573 us | 108.526 us | 0.07x |
| `common/matches-set` | `Count` | 7.544 us | 106.461 us | 108.368 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 235.472 us | 4,845.938 us | 5,229.521 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.331 us | 6.436 us | 29.766 us | 1.61x |
| `structural/keyword-to-generic-type` | 5.519 us | 4.111 us | 16.208 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.671 us | 1.369 us | 1.483 us | 1.22x |
| `lokad/lexer/doc-line` | 3.369 us | 3.067 us | 3.684 us | 1.10x |
| `lokad/lexer/identifier` | 23.946 us | 23.101 us | 26.170 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.287 us | 7.375 us | 8.100 us | 0.99x |
| `structural/ordered-keyword-pair` | 5.386 us | 5.480 us | 17.132 us | 0.98x |
| `lokad/langserv/identifier-validator` | 11.323 us | 12.642 us | 14.042 us | 0.90x |
| `fallback/lookahead` | 5.437 us | 6.088 us | 17.131 us | 0.89x |
| `lokad/lexer/operator-run` | 2.922 us | 3.509 us | 3.896 us | 0.83x |
| `literal-family/method-token-family` | 7.171 us | 9.613 us | 20.232 us | 0.75x |
| `lokad/style/hex-color` | 0.610 us | 0.846 us | 0.907 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 3.411 us | 4.893 us | 5.546 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.047 us | 2.973 us | 3.042 us | 0.69x |
| `literal/identifier-token` | 2.800 us | 4.394 us | 15.620 us | 0.64x |
| `literal/call-token` | 2.603 us | 4.348 us | 15.631 us | 0.60x |
| `lokad/lexer/number` | 8.357 us | 14.301 us | 15.385 us | 0.58x |
| `structural/ordered-identifier-window` | 16.154 us | 28.920 us | 40.509 us | 0.56x |
| `lokad/style/cell-ref` | 1.491 us | 2.899 us | 3.007 us | 0.51x |
| `structural/method-family-generic-call` | 3.449 us | 6.711 us | 17.888 us | 0.51x |
| `structural/method-family-call` | 3.927 us | 8.428 us | 19.076 us | 0.47x |
| `literal-family/type-token-family` | 10.567 us | 24.575 us | 35.341 us | 0.43x |
| `literal/identifier-token-casei` | 6.156 us | 15.671 us | 26.575 us | 0.39x |
| `lokad/style/range-ref` | 0.925 us | 2.799 us | 2.783 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.681 us | 17.330 us | 28.482 us | 0.33x |
| `structural/ordered-keyword-window` | 5.528 us | 18.008 us | 29.566 us | 0.31x |
| `structural/modifier-family-to-type-window` | 78.494 us | 258.924 us | 271.490 us | 0.30x |
| `lokad/lexer/string` | 21.884 us | 98.603 us | 102.913 us | 0.22x |
| `lokad/folding/region-marker` | 6.048 us | 36.735 us | 61.281 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.520 us | 24.564 us | 25.135 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.468 us | 221.654 us | 234.358 us | 0.05x |
| `lokad/langserv/url-download` | 2.526 us | 52.299 us | 53.498 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.026 us | 130.706 us | 142.348 us | 0.04x |
| `balancing/begin-end-blocks` | 5.719 us | 4,372.733 us | 4,377.067 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.303 us | 4.492 us | 27.041 us | 2.29x |
| `lokad/langserv/color-short-hex` | 1.673 us | 0.992 us | 1.171 us | 1.69x |
| `structural/keyword-to-generic-type` | 5.518 us | 3.538 us | 15.028 us | 1.56x |
| `lokad/langserv/helper-identifier` | 7.289 us | 4.827 us | 5.963 us | 1.51x |
| `structural/ordered-identifier-window` | 16.204 us | 10.885 us | 21.902 us | 1.49x |
| `lokad/lexer/doc-line` | 3.366 us | 2.308 us | 3.048 us | 1.46x |
| `lokad/lexer/operator-run` | 2.924 us | 2.016 us | 2.664 us | 1.45x |
| `structural/keyword-family-to-capitalized-identifier` | 11.484 us | 7.923 us | 18.715 us | 1.45x |
| `structural/modifier-family-to-type-window` | 78.650 us | 56.961 us | 69.094 us | 1.38x |
| `lokad/lexer/identifier` | 23.897 us | 17.476 us | 21.122 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.396 us | 4.181 us | 15.253 us | 1.29x |
| `fallback/lookahead` | 5.444 us | 4.262 us | 14.910 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.390 us | 9.126 us | 11.235 us | 1.25x |
| `lokad/docs/inline-doc-prefix` | 3.404 us | 2.800 us | 3.675 us | 1.22x |
| `lokad/style/hex-color` | 0.609 us | 0.524 us | 0.615 us | 1.16x |
| `lokad/lexer/number` | 8.355 us | 7.645 us | 8.674 us | 1.09x |
| `lokad/langserv/color-rgb-hex` | 2.064 us | 1.942 us | 2.130 us | 1.06x |
| `literal/identifier-token-casei` | 6.145 us | 5.953 us | 17.197 us | 1.03x |
| `literal-family/type-token-family` | 10.560 us | 11.555 us | 22.235 us | 0.91x |
| `lokad/style/cell-ref` | 1.487 us | 1.813 us | 1.967 us | 0.82x |
| `structural/method-family-call` | 3.938 us | 5.111 us | 15.517 us | 0.77x |
| `structural/method-family-generic-call` | 3.451 us | 4.536 us | 15.067 us | 0.76x |
| `literal-family/method-token-family` | 3.903 us | 5.341 us | 15.709 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.677 us | 8.652 us | 19.790 us | 0.66x |
| `literal/call-token` | 2.607 us | 3.988 us | 14.665 us | 0.65x |
| `literal/identifier-token` | 2.800 us | 4.334 us | 15.371 us | 0.65x |
| `structural/ordered-keyword-window` | 5.546 us | 8.709 us | 19.961 us | 0.64x |
| `lokad/style/range-ref` | 0.923 us | 1.493 us | 1.562 us | 0.62x |
| `lokad/lexer/string` | 21.854 us | 46.751 us | 50.303 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 5.019 us | 10.810 us | 21.808 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.512 us | 7.691 us | 8.292 us | 0.33x |
| `lokad/langserv/url-download` | 2.533 us | 13.065 us | 13.614 us | 0.19x |
| `lokad/folding/region-marker` | 6.060 us | 36.834 us | 60.392 us | 0.16x |
| `balancing/begin-end-blocks` | 5.718 us | 470.230 us | 499.310 us | 0.01x |
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
