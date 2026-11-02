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

The full baseline was refreshed on **1 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Unqualified rows
retain their observations without establishing parity. The complete source
build, both-runtime tests and all-profile benchmark refresh passed in **42m10s**;
package and consumer qualification are separate. Measurements describe each
row's recorded implementation; later source changes need their own evidence.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

The complete Core/PythonRe/PCRE2 refresh took **45 minutes 37 seconds**, including
a fresh Release build, .NET 10/11 tests, paired comparators, lifecycle/scaling
measurements and resource checks. See the
[run receipt](bench/Experiments/Net11-20261027/subhour-refresh.json).
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
| `literal/sherlock-zh` | 134.993 us | 19.088 us | 2,161.674 us | 7.07x |
| `literal-alternate/sherlock-zh` | 239.736 us | 69.201 us | 2,218.883 us | 3.46x |
| `literal/sherlock-ru` | 252.036 us | 95.335 us | 5,513.711 us | 2.64x |
| `ruff-noqa/tweaked` | 10,831.100 us | 8,070.043 us | 42,190.850 us | 1.34x |
| `literal/sherlock-casei-en` | 176.184 us | 197.235 us | 626.281 us | 0.89x |
| `literal/sherlock-en` | 70.815 us | 87.657 us | 467.889 us | 0.81x |
| `literal/sherlock-casei-ru` | 248.027 us | 398.006 us | 5,664.189 us | 0.62x |
| `aws-keys/quick` | 5,067.180 us | 9,991.850 us | 45,689.300 us | 0.51x |
| `bounded-repeat/letters-en` | 1,043.051 us | 2,894.028 us | 2,995.147 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,739.152 us | 4,837.509 us | 10,085.767 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,955.827 us | 6,018.944 us | 6,827.913 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 195.113 us | 605.208 us | 1,030.848 us | 0.32x |
| `dictionary/single` | 8,355.614 us | 26,922.400 us | 27,020.550 us | 0.31x |
| `literal-alternate/sherlock-en` | 402.573 us | 1,528.827 us | 2,089.830 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 168.217 us | 670.197 us | 1,112.415 us | 0.25x |
| `bounded-repeat/capitals` | 9,577.933 us | 47,224.650 us | 54,848.300 us | 0.20x |
| `bounded-repeat/context` | 15,930.225 us | 149,728.600 us | 152,974.500 us | 0.11x |
| `aws-keys/full` | 8,994.167 us | 87,507.900 us | 123,190.900 us | 0.10x |
| `ruff-noqa/real` | 4,682.400 us | 71,354.300 us | 89,309.000 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,095.298 us | 24,251.933 us | 29,438.450 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 223.879 us | 11,172.100 us | 11,988.440 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,527.091 us | 497.275 us | 9,074.767 us | 3.07x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.202 us | 0.110 us | 0.149 us | 1.83x |
| `common/match-word` | `Match` | 0.202 us | 0.166 us | 1.204 us | 1.22x |
| `industry/leipzig-twain-count` | `Count` | 1,270.065 us | 1,360.758 us | 8,893.100 us | 0.93x |
| `industry/mariomka-uri-count` | `Count` | 1,763.145 us | 1,903.122 us | 10,488.933 us | 0.93x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 239.348 us | 271.897 us | 509.111 us | 0.88x |
| `common/replace-words` | `Replace` | 66.267 us | 79.624 us | 81.094 us | 0.83x |
| `common/uri-miss` | `IsMatch` | 0.161 us | 0.194 us | 0.213 us | 0.83x |
| `common/uri-match` | `IsMatch` | 0.167 us | 0.236 us | 0.271 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.60x |
| `common/ip-match` | `IsMatch` | 0.105 us | 0.175 us | 0.195 us | 0.60x |
| `common/ip-miss` | `IsMatch` | 0.105 us | 0.190 us | 0.208 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.148 us | 0.273 us | 0.300 us | 0.54x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.081 us | 0.165 us | 0.182 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.158 us | 0.369 us | 0.393 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,171.267 us | 9,802.267 us | 17,411.250 us | 0.43x |
| `common/matches-word` | `Count` | 1.362 us | 4.244 us | 5.990 us | 0.32x |
| `industry/mariomka-ip-count` | `Count` | 2,722.163 us | 8,788.933 us | 17,421.800 us | 0.31x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.111 us | 0.140 us | 0.26x |
| `common/one-node-backtracking` | `IsMatch` | 0.161 us | 0.623 us | 0.655 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.100 us | 0.21x |
| `common/backtracking` | `IsMatch` | 0.166 us | 0.837 us | 0.879 us | 0.20x |
| `common/split-words` | `Split` | 14.689 us | 80.316 us | 81.908 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.774 us | 971.835 us | 1,262.881 us | 0.18x |
| `industry/leipzig-river-window-count` | `Count` | 5,748.756 us | 33,699.550 us | 46,545.900 us | 0.17x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.570 us | 0.615 us | 0.15x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/matches-words` | `Count` | 9.424 us | 74.442 us | 75.516 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,022.232 us | 19,615.600 us | 20,541.150 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,875.796 us | 23,775.433 us | 31,202.650 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 517.807 us | 8,484.417 us | 8,983.783 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.903 us | 174.495 us | 175.678 us | 0.05x |
| `common/matches-set` | `Count` | 7.930 us | 196.510 us | 198.155 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 267.236 us | 12,434.140 us | 12,932.550 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 237.584 us | 36.397 us | 2,146.958 us | 6.53x |
| `literal/sherlock-zh` | 134.878 us | 24.192 us | 2,157.548 us | 5.58x |
| `literal/sherlock-ru` | 251.362 us | 87.510 us | 5,471.689 us | 2.87x |
| `ruff-noqa/tweaked` | 11,003.320 us | 4,604.473 us | 38,285.700 us | 2.39x |
| `literal/sherlock-casei-en` | 176.549 us | 77.974 us | 453.014 us | 2.26x |
| `literal-alternate/sherlock-casei-en` | 223.359 us | 133.216 us | 569.789 us | 1.68x |
| `aws-keys/quick` | 5,075.780 us | 3,624.993 us | 40,407.500 us | 1.40x |
| `literal-alternate/sherlock-en` | 403.156 us | 348.259 us | 765.588 us | 1.16x |
| `literal/sherlock-casei-ru` | 248.027 us | 224.092 us | 5,479.589 us | 1.11x |
| `literal-alternate/sherlock-en-mixed` | 195.974 us | 182.776 us | 566.935 us | 1.07x |
| `literal/sherlock-en` | 70.838 us | 78.695 us | 402.618 us | 0.90x |
| `bounded-repeat/letters-ru` | 1,958.796 us | 2,177.996 us | 2,967.647 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 168.174 us | 202.802 us | 595.191 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,757.997 us | 2,371.450 us | 7,517.300 us | 0.74x |
| `bounded-repeat/capitals` | 9,607.633 us | 13,710.475 us | 21,137.600 us | 0.70x |
| `aws-keys/full` | 11,208.833 us | 18,994.267 us | 53,924.700 us | 0.59x |
| `bounded-repeat/letters-en` | 1,045.373 us | 1,854.296 us | 1,937.573 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,092.683 us | 1,984.865 us | 7,259.533 us | 0.55x |
| `bounded-repeat/context` | 15,825.833 us | 69,653.000 us | 72,828.200 us | 0.23x |
| `ruff-noqa/real` | 4,754.345 us | 21,729.733 us | 35,123.500 us | 0.22x |
| `dictionary/single` | 8,346.100 us | 46,295.100 us | 46,327.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/rust-sherlock-holmes-window-count` | `Count` | 240.067 us | 58.849 us | 258.730 us | 4.08x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.201 us | 0.050 us | 0.089 us | 4.02x |
| `common/backtracking` | `IsMatch` | 0.166 us | 0.044 us | 0.074 us | 3.77x |
| `industry/leipzig-river-window-count` | `Count` | 5,768.800 us | 1,657.239 us | 13,761.133 us | 3.48x |
| `common/replace-words` | `Replace` | 66.240 us | 19.287 us | 21.011 us | 3.43x |
| `industry/mariomka-email-count` | `Count` | 1,519.645 us | 460.194 us | 7,892.500 us | 3.30x |
| `common/match-word` | `Match` | 0.200 us | 0.080 us | 1.000 us | 2.51x |
| `common/uri-miss` | `IsMatch` | 0.160 us | 0.069 us | 0.088 us | 2.33x |
| `common/ip-match` | `IsMatch` | 0.105 us | 0.048 us | 0.070 us | 2.19x |
| `common/ip-miss` | `IsMatch` | 0.104 us | 0.051 us | 0.071 us | 2.06x |
| `common/uri-match` | `IsMatch` | 0.165 us | 0.090 us | 0.120 us | 1.83x |
| `common/email-match` | `IsMatch` | 0.148 us | 0.081 us | 0.108 us | 1.82x |
| `common/one-node-backtracking` | `IsMatch` | 0.162 us | 0.090 us | 0.128 us | 1.81x |
| `common/email-miss` | `IsMatch` | 0.157 us | 0.090 us | 0.116 us | 1.75x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.030 us | 0.047 us | 1.69x |
| `industry/mariomka-uri-count` | `Count` | 1,766.538 us | 1,103.709 us | 8,514.867 us | 1.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.033 us | 0.051 us | 1.57x |
| `industry/leipzig-symbol-count` | `Count` | 1,875.922 us | 1,292.985 us | 7,150.567 us | 1.45x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.078 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,288.550 us | 3,122.918 us | 8,667.933 us | 1.37x |
| `industry/leipzig-twain-count` | `Count` | 1,273.237 us | 1,289.482 us | 5,651.833 us | 0.99x |
| `common/matches-words` | `Count` | 9.421 us | 11.439 us | 14.827 us | 0.82x |
| `industry/mariomka-ip-count` | `Count` | 2,723.958 us | 3,533.847 us | 11,114.900 us | 0.77x |
| `common/split-words` | `Split` | 14.705 us | 21.023 us | 22.755 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.045 us | 0.075 us | 0.64x |
| `common/matches-word` | `Count` | 1.362 us | 2.390 us | 3.779 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.204 us | 0.236 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.036 us | 0.055 us | 0.34x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 518.176 us | 4,006.023 us | 4,451.675 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.653 us | 1,504.682 us | 1,845.479 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,025.432 us | 18,288.050 us | 19,135.300 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.973 us | 105.659 us | 107.701 us | 0.08x |
| `common/matches-set` | `Count` | 7.948 us | 106.873 us | 109.085 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 266.969 us | 4,843.600 us | 5,247.550 us | 0.06x |
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
| `lokad/imports/module-imports` | 31.792 us | 6.471 us | 29.693 us | 4.91x |
| `lokad/lexer/doc-line` | 4.671 us | 3.100 us | 3.844 us | 1.51x |
| `lokad/lexer/identifier` | 32.759 us | 23.254 us | 26.393 us | 1.41x |
| `lokad/langserv/helper-identifier` | 9.863 us | 7.231 us | 8.104 us | 1.36x |
| `structural/keyword-to-generic-type` | 5.065 us | 4.113 us | 16.015 us | 1.23x |
| `lokad/lexer/operator-run` | 4.132 us | 3.414 us | 3.978 us | 1.21x |
| `lokad/langserv/color-short-hex` | 1.667 us | 1.380 us | 1.525 us | 1.21x |
| `lokad/style/hex-color` | 0.833 us | 0.813 us | 0.907 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.394 us | 5.478 us | 17.226 us | 0.98x |
| `lokad/docs/inline-doc-prefix` | 4.542 us | 4.839 us | 5.646 us | 0.94x |
| `fallback/lookahead` | 5.503 us | 6.107 us | 17.164 us | 0.90x |
| `lokad/langserv/identifier-validator` | 11.423 us | 12.702 us | 14.607 us | 0.90x |
| `lokad/lexer/number` | 11.544 us | 14.220 us | 15.511 us | 0.81x |
| `literal-family/method-token-family` | 7.172 us | 9.349 us | 19.978 us | 0.77x |
| `lokad/style/cell-ref` | 2.115 us | 3.072 us | 3.088 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.047 us | 3.001 us | 3.199 us | 0.68x |
| `literal/identifier-token` | 2.815 us | 4.398 us | 15.648 us | 0.64x |
| `literal/call-token` | 2.607 us | 4.200 us | 15.646 us | 0.62x |
| `structural/method-family-generic-call` | 3.498 us | 6.762 us | 18.192 us | 0.52x |
| `lokad/style/range-ref` | 1.329 us | 2.743 us | 2.843 us | 0.48x |
| `structural/method-family-call` | 3.873 us | 8.109 us | 18.853 us | 0.48x |
| `literal-family/type-token-family` | 10.627 us | 24.738 us | 35.906 us | 0.43x |
| `literal/identifier-token-casei` | 5.762 us | 15.198 us | 26.534 us | 0.38x |
| `lokad/lexer/string` | 35.521 us | 96.426 us | 101.216 us | 0.37x |
| `structural/ordered-modifier-pair` | 5.864 us | 17.335 us | 28.398 us | 0.34x |
| `structural/ordered-keyword-window` | 5.506 us | 18.010 us | 29.287 us | 0.31x |
| `structural/modifier-family-to-type-window` | 77.458 us | 254.148 us | 267.122 us | 0.30x |
| `structural/keyword-family-to-capitalized-identifier` | 66.273 us | 224.336 us | 236.927 us | 0.30x |
| `lokad/folding/region-marker` | 6.927 us | 36.544 us | 61.571 us | 0.19x |
| `structural/modifier-family-to-type-pair` | 23.575 us | 135.937 us | 148.444 us | 0.17x |
| `structural/ordered-identifier-window` | 5.003 us | 29.032 us | 40.254 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.944 us | 24.719 us | 25.485 us | 0.12x |
| `lokad/langserv/url-download` | 2.889 us | 52.653 us | 53.243 us | 0.05x |
| `balancing/begin-end-blocks` | 7.430 us | 4,418.225 us | 4,447.117 us | 0.0017x |
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
| `structural/keyword-family-to-capitalized-identifier` | 66.350 us | 7.756 us | 18.984 us | 8.55x |
| `lokad/imports/module-imports` | 31.685 us | 4.468 us | 26.533 us | 7.09x |
| `structural/modifier-family-to-type-pair` | 23.564 us | 10.773 us | 22.020 us | 2.19x |
| `lokad/langserv/helper-identifier` | 9.856 us | 4.900 us | 6.167 us | 2.01x |
| `lokad/lexer/doc-line` | 4.632 us | 2.347 us | 2.993 us | 1.97x |
| `lokad/lexer/operator-run` | 4.134 us | 2.096 us | 2.701 us | 1.97x |
| `lokad/lexer/identifier` | 33.061 us | 17.414 us | 21.425 us | 1.90x |
| `lokad/docs/inline-doc-prefix` | 4.602 us | 2.895 us | 3.772 us | 1.59x |
| `lokad/langserv/color-short-hex` | 1.670 us | 1.068 us | 1.229 us | 1.56x |
| `lokad/style/hex-color` | 0.825 us | 0.533 us | 0.640 us | 1.55x |
| `lokad/lexer/number` | 11.485 us | 7.469 us | 8.872 us | 1.54x |
| `structural/keyword-to-generic-type` | 5.071 us | 3.488 us | 14.926 us | 1.45x |
| `structural/modifier-family-to-type-window` | 77.895 us | 56.742 us | 68.696 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.396 us | 4.180 us | 15.307 us | 1.29x |
| `fallback/lookahead` | 5.483 us | 4.252 us | 14.931 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.483 us | 9.544 us | 11.592 us | 1.20x |
| `lokad/style/cell-ref` | 2.128 us | 1.859 us | 1.999 us | 1.14x |
| `lokad/langserv/color-rgb-hex` | 2.049 us | 2.010 us | 2.195 us | 1.02x |
| `literal/identifier-token-casei` | 5.761 us | 5.966 us | 17.229 us | 0.97x |
| `literal-family/type-token-family` | 10.612 us | 11.579 us | 22.255 us | 0.92x |
| `lokad/style/range-ref` | 1.324 us | 1.591 us | 1.691 us | 0.83x |
| `structural/method-family-generic-call` | 3.479 us | 4.529 us | 15.317 us | 0.77x |
| `structural/method-family-call` | 3.885 us | 5.111 us | 15.522 us | 0.76x |
| `lokad/lexer/string` | 35.606 us | 47.012 us | 50.896 us | 0.76x |
| `literal-family/method-token-family` | 3.912 us | 5.345 us | 15.722 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.865 us | 8.694 us | 19.637 us | 0.67x |
| `literal/call-token` | 2.614 us | 3.986 us | 14.653 us | 0.66x |
| `structural/ordered-keyword-window` | 5.487 us | 8.692 us | 19.713 us | 0.63x |
| `literal/identifier-token` | 2.817 us | 4.745 us | 15.374 us | 0.59x |
| `structural/ordered-identifier-window` | 5.008 us | 10.832 us | 21.778 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.939 us | 8.041 us | 8.618 us | 0.37x |
| `lokad/langserv/url-download` | 2.876 us | 13.077 us | 13.687 us | 0.22x |
| `lokad/folding/region-marker` | 6.975 us | 37.173 us | 59.590 us | 0.19x |
| `balancing/begin-end-blocks` | 7.451 us | 471.260 us | 498.470 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
