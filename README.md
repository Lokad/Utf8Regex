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
| `literal/sherlock-zh` | 135.624 us | 19.014 us | 2,177.304 us | 7.13x |
| `literal-alternate/sherlock-zh` | 238.342 us | 69.500 us | 2,164.839 us | 3.43x |
| `literal/sherlock-ru` | 255.161 us | 94.470 us | 5,291.380 us | 2.70x |
| `ruff-noqa/tweaked` | 10,737.260 us | 8,009.629 us | 43,881.300 us | 1.34x |
| `literal/sherlock-casei-en` | 171.889 us | 204.050 us | 596.149 us | 0.84x |
| `literal/sherlock-en` | 70.551 us | 87.026 us | 453.039 us | 0.81x |
| `aws-keys/quick` | 6,675.089 us | 9,854.320 us | 43,379.800 us | 0.68x |
| `literal/sherlock-casei-ru` | 250.820 us | 396.331 us | 5,657.378 us | 0.63x |
| `bounded-repeat/letters-en` | 1,065.177 us | 2,850.089 us | 2,942.482 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,738.245 us | 4,858.636 us | 10,057.700 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 206.406 us | 600.042 us | 1,074.275 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,944.358 us | 6,034.611 us | 6,828.762 us | 0.32x |
| `dictionary/single` | 8,343.243 us | 26,021.400 us | 26,136.250 us | 0.32x |
| `literal-alternate/sherlock-en` | 404.252 us | 1,504.647 us | 2,031.359 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 168.492 us | 663.549 us | 1,108.422 us | 0.25x |
| `bounded-repeat/capitals` | 9,482.150 us | 47,208.500 us | 54,534.900 us | 0.20x |
| `aws-keys/full` | 10,779.820 us | 89,310.400 us | 120,694.200 us | 0.12x |
| `bounded-repeat/context` | 16,035.667 us | 149,876.900 us | 153,326.700 us | 0.11x |
| `ruff-noqa/real` | 4,566.309 us | 74,703.100 us | 91,470.200 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,099.913 us | 23,438.433 us | 28,784.850 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 228.976 us | 12,338.080 us | 13,200.500 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,518.803 us | 496.862 us | 8,998.733 us | 3.06x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.183 us | 0.110 us | 0.149 us | 1.66x |
| `common/match-word` | `Match` | 0.200 us | 0.165 us | 1.182 us | 1.21x |
| `industry/leipzig-twain-count` | `Count` | 1,268.635 us | 1,357.658 us | 8,823.733 us | 0.93x |
| `common/uri-miss` | `IsMatch` | 0.177 us | 0.193 us | 0.212 us | 0.92x |
| `industry/mariomka-uri-count` | `Count` | 1,768.728 us | 1,939.604 us | 10,542.400 us | 0.91x |
| `common/replace-words` | `Replace` | 70.254 us | 77.659 us | 78.975 us | 0.90x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 241.230 us | 266.875 us | 511.915 us | 0.90x |
| `common/uri-match` | `IsMatch` | 0.176 us | 0.238 us | 0.272 us | 0.74x |
| `common/ip-match` | `IsMatch` | 0.120 us | 0.175 us | 0.197 us | 0.68x |
| `common/email-match` | `IsMatch` | 0.171 us | 0.270 us | 0.297 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.086 us | 0.101 us | 0.63x |
| `common/ip-miss` | `IsMatch` | 0.112 us | 0.188 us | 0.207 us | 0.60x |
| `common/email-miss` | `IsMatch` | 0.176 us | 0.348 us | 0.374 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.169 us | 0.186 us | 0.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,290.342 us | 9,845.067 us | 17,107.750 us | 0.44x |
| `common/matches-word` | `Count` | 1.418 us | 4.289 us | 6.090 us | 0.33x |
| `common/one-node-backtracking` | `IsMatch` | 0.205 us | 0.621 us | 0.663 us | 0.33x |
| `industry/mariomka-ip-count` | `Count` | 2,713.295 us | 8,412.950 us | 17,180.550 us | 0.32x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.141 us | 0.26x |
| `common/backtracking` | `IsMatch` | 0.191 us | 0.853 us | 0.902 us | 0.22x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.098 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.206 us | 968.215 us | 1,264.469 us | 0.18x |
| `common/split-words` | `Split` | 14.730 us | 81.989 us | 83.593 us | 0.18x |
| `industry/leipzig-river-window-count` | `Count` | 5,830.133 us | 33,048.100 us | 47,595.400 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.561 us | 0.594 us | 0.16x |
| `common/matches-words` | `Count` | 9.633 us | 69.451 us | 70.853 us | 0.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,009.536 us | 19,819.200 us | 20,685.867 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,879.970 us | 23,071.400 us | 30,902.950 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 514.960 us | 8,640.850 us | 9,158.383 us | 0.06x |
| `common/matches-boundary` | `Count` | 8.007 us | 164.570 us | 167.078 us | 0.05x |
| `common/matches-set` | `Count` | 8.034 us | 195.291 us | 198.302 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 246.961 us | 11,958.700 us | 12,541.880 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 241.323 us | 36.461 us | 2,162.779 us | 6.62x |
| `literal/sherlock-zh` | 135.612 us | 24.155 us | 2,184.479 us | 5.61x |
| `literal/sherlock-ru` | 254.758 us | 86.838 us | 5,252.550 us | 2.93x |
| `ruff-noqa/tweaked` | 11,044.860 us | 4,589.100 us | 39,671.200 us | 2.41x |
| `literal/sherlock-casei-en` | 172.204 us | 80.607 us | 434.427 us | 2.14x |
| `aws-keys/quick` | 6,679.512 us | 3,608.943 us | 36,344.650 us | 1.85x |
| `literal-alternate/sherlock-casei-en` | 219.676 us | 131.993 us | 554.379 us | 1.66x |
| `literal-alternate/sherlock-en` | 404.352 us | 348.562 us | 770.839 us | 1.16x |
| `literal/sherlock-casei-ru` | 252.099 us | 220.103 us | 5,521.000 us | 1.15x |
| `literal-alternate/sherlock-en-mixed` | 206.271 us | 182.896 us | 554.599 us | 1.13x |
| `literal/sherlock-en` | 70.569 us | 76.471 us | 438.253 us | 0.92x |
| `bounded-repeat/letters-ru` | 1,947.008 us | 2,168.108 us | 2,992.771 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 168.438 us | 202.770 us | 602.085 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,703.393 us | 2,379.932 us | 7,625.367 us | 0.72x |
| `bounded-repeat/capitals` | 9,491.750 us | 13,691.950 us | 20,876.900 us | 0.69x |
| `aws-keys/full` | 10,827.840 us | 18,909.600 us | 50,818.650 us | 0.57x |
| `bounded-repeat/letters-en` | 1,062.902 us | 1,859.443 us | 1,932.996 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 1,098.957 us | 1,983.196 us | 7,251.600 us | 0.55x |
| `bounded-repeat/context` | 16,192.033 us | 69,695.100 us | 72,399.300 us | 0.23x |
| `ruff-noqa/real` | 4,572.518 us | 21,479.467 us | 35,422.950 us | 0.21x |
| `dictionary/single` | 8,334.633 us | 46,157.300 us | 46,200.350 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/backtracking` | `IsMatch` | 0.190 us | 0.044 us | 0.074 us | 4.28x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 241.831 us | 58.781 us | 262.062 us | 4.11x |
| `industry/leipzig-river-window-count` | `Count` | 5,841.322 us | 1,526.124 us | 14,331.067 us | 3.83x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.183 us | 0.049 us | 0.088 us | 3.73x |
| `common/replace-words` | `Replace` | 70.161 us | 19.477 us | 21.095 us | 3.60x |
| `industry/mariomka-email-count` | `Count` | 1,521.294 us | 459.923 us | 7,823.100 us | 3.31x |
| `common/uri-miss` | `IsMatch` | 0.177 us | 0.070 us | 0.089 us | 2.54x |
| `common/match-word` | `Match` | 0.200 us | 0.081 us | 0.995 us | 2.47x |
| `common/ip-match` | `IsMatch` | 0.118 us | 0.049 us | 0.069 us | 2.42x |
| `common/one-node-backtracking` | `IsMatch` | 0.205 us | 0.090 us | 0.129 us | 2.26x |
| `common/ip-miss` | `IsMatch` | 0.112 us | 0.052 us | 0.073 us | 2.18x |
| `common/email-match` | `IsMatch` | 0.171 us | 0.082 us | 0.109 us | 2.09x |
| `common/email-miss` | `IsMatch` | 0.175 us | 0.089 us | 0.116 us | 1.96x |
| `common/uri-match` | `IsMatch` | 0.176 us | 0.090 us | 0.120 us | 1.95x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.052 us | 0.029 us | 0.047 us | 1.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.032 us | 0.051 us | 1.71x |
| `industry/mariomka-uri-count` | `Count` | 1,774.024 us | 1,108.852 us | 8,566.200 us | 1.60x |
| `industry/leipzig-symbol-count` | `Count` | 1,870.700 us | 1,284.370 us | 7,139.033 us | 1.46x |
| `industry/leipzig-name-family-count` | `Count` | 4,444.500 us | 3,075.876 us | 8,500.767 us | 1.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.078 us | 1.37x |
| `industry/leipzig-twain-count` | `Count` | 1,270.710 us | 1,287.710 us | 5,407.767 us | 0.99x |
| `common/matches-words` | `Count` | 9.640 us | 11.005 us | 14.117 us | 0.88x |
| `industry/mariomka-ip-count` | `Count` | 2,720.753 us | 3,520.480 us | 11,155.100 us | 0.77x |
| `common/split-words` | `Split` | 14.705 us | 20.801 us | 22.894 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `common/matches-word` | `Count` | 1.418 us | 2.344 us | 3.714 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.204 us | 0.240 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.037 us | 0.053 us | 0.28x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 515.906 us | 3,994.169 us | 4,392.867 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.372 us | 1,500.785 us | 1,843.186 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,009.384 us | 18,043.600 us | 18,564.500 us | 0.11x |
| `common/matches-set` | `Count` | 7.922 us | 105.781 us | 107.960 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.934 us | 106.620 us | 108.877 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 249.311 us | 4,832.236 us | 5,258.110 us | 0.05x |
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
| `lokad/imports/module-imports` | 31.477 us | 6.411 us | 28.999 us | 4.91x |
| `structural/keyword-to-generic-type` | 5.830 us | 4.146 us | 15.999 us | 1.41x |
| `lokad/lexer/doc-line` | 4.343 us | 3.105 us | 3.880 us | 1.40x |
| `lokad/lexer/identifier` | 31.021 us | 23.011 us | 26.257 us | 1.35x |
| `lokad/langserv/helper-identifier` | 9.536 us | 7.335 us | 8.070 us | 1.30x |
| `lokad/langserv/color-short-hex` | 1.637 us | 1.359 us | 1.478 us | 1.20x |
| `structural/ordered-keyword-pair` | 6.224 us | 5.492 us | 17.166 us | 1.13x |
| `lokad/lexer/operator-run` | 3.789 us | 3.438 us | 3.933 us | 1.10x |
| `fallback/lookahead` | 6.129 us | 6.309 us | 17.466 us | 0.97x |
| `lokad/langserv/identifier-validator` | 11.574 us | 12.368 us | 14.437 us | 0.94x |
| `lokad/style/hex-color` | 0.768 us | 0.828 us | 0.910 us | 0.93x |
| `lokad/docs/inline-doc-prefix` | 4.394 us | 4.904 us | 5.655 us | 0.90x |
| `literal-family/method-token-family` | 8.216 us | 9.276 us | 19.949 us | 0.89x |
| `lokad/lexer/number` | 10.843 us | 14.215 us | 15.432 us | 0.76x |
| `literal/call-token` | 3.072 us | 4.342 us | 15.833 us | 0.71x |
| `structural/method-family-generic-call` | 4.474 us | 6.539 us | 18.146 us | 0.68x |
| `lokad/langserv/color-rgb-hex` | 2.040 us | 2.998 us | 3.103 us | 0.68x |
| `lokad/style/cell-ref` | 1.955 us | 3.020 us | 3.092 us | 0.65x |
| `literal/identifier-token` | 2.807 us | 4.410 us | 15.881 us | 0.64x |
| `structural/method-family-call` | 4.861 us | 8.144 us | 19.017 us | 0.60x |
| `literal-family/type-token-family` | 12.156 us | 24.834 us | 35.528 us | 0.49x |
| `literal/identifier-token-casei` | 7.135 us | 15.355 us | 26.974 us | 0.46x |
| `lokad/style/range-ref` | 1.202 us | 2.780 us | 2.806 us | 0.43x |
| `structural/ordered-keyword-window` | 6.853 us | 18.048 us | 28.959 us | 0.38x |
| `structural/ordered-modifier-pair` | 6.240 us | 17.415 us | 28.661 us | 0.36x |
| `structural/keyword-family-to-capitalized-identifier` | 67.795 us | 219.390 us | 230.374 us | 0.31x |
| `lokad/lexer/string` | 29.544 us | 96.073 us | 101.410 us | 0.31x |
| `structural/modifier-family-to-type-window` | 76.763 us | 256.517 us | 268.410 us | 0.30x |
| `structural/ordered-identifier-window` | 6.533 us | 29.153 us | 40.201 us | 0.22x |
| `structural/modifier-family-to-type-pair` | 24.857 us | 130.725 us | 142.048 us | 0.19x |
| `lokad/folding/region-marker` | 6.979 us | 37.208 us | 61.540 us | 0.19x |
| `lokad/langserv/url-dashboard` | 3.048 us | 24.629 us | 25.444 us | 0.12x |
| `lokad/langserv/url-download` | 2.870 us | 52.149 us | 52.457 us | 0.06x |
| `balancing/begin-end-blocks` | 7.544 us | 4,398.108 us | 4,420.642 us | 0.0017x |
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
| `structural/keyword-family-to-capitalized-identifier` | 67.860 us | 7.681 us | 18.773 us | 8.84x |
| `lokad/imports/module-imports` | 31.609 us | 4.442 us | 26.372 us | 7.12x |
| `structural/modifier-family-to-type-pair` | 24.861 us | 10.837 us | 21.763 us | 2.29x |
| `lokad/langserv/helper-identifier` | 9.430 us | 4.897 us | 5.966 us | 1.93x |
| `lokad/lexer/doc-line` | 4.344 us | 2.323 us | 3.122 us | 1.87x |
| `lokad/lexer/operator-run` | 3.803 us | 2.126 us | 2.701 us | 1.79x |
| `lokad/lexer/identifier` | 31.045 us | 17.788 us | 20.777 us | 1.75x |
| `structural/keyword-to-generic-type` | 5.843 us | 3.539 us | 14.907 us | 1.65x |
| `lokad/langserv/color-short-hex` | 1.636 us | 1.001 us | 1.191 us | 1.63x |
| `structural/ordered-keyword-pair` | 6.244 us | 4.168 us | 15.242 us | 1.50x |
| `lokad/docs/inline-doc-prefix` | 4.390 us | 3.024 us | 3.630 us | 1.45x |
| `fallback/lookahead` | 6.156 us | 4.251 us | 15.147 us | 1.45x |
| `lokad/lexer/number` | 10.839 us | 7.664 us | 8.866 us | 1.41x |
| `lokad/style/hex-color` | 0.768 us | 0.543 us | 0.650 us | 1.41x |
| `structural/modifier-family-to-type-window` | 76.996 us | 56.395 us | 68.674 us | 1.37x |
| `lokad/langserv/identifier-validator` | 11.566 us | 9.231 us | 11.452 us | 1.25x |
| `literal/identifier-token-casei` | 7.100 us | 5.960 us | 17.391 us | 1.19x |
| `literal-family/type-token-family` | 12.119 us | 11.545 us | 22.241 us | 1.05x |
| `lokad/langserv/color-rgb-hex` | 2.039 us | 1.947 us | 2.147 us | 1.05x |
| `lokad/style/cell-ref` | 1.947 us | 1.867 us | 1.968 us | 1.04x |
| `structural/method-family-generic-call` | 4.469 us | 4.635 us | 15.312 us | 0.96x |
| `structural/method-family-call` | 4.867 us | 5.110 us | 15.725 us | 0.95x |
| `literal-family/method-token-family` | 4.794 us | 5.350 us | 15.814 us | 0.90x |
| `structural/ordered-keyword-window` | 6.868 us | 8.661 us | 19.425 us | 0.79x |
| `lokad/style/range-ref` | 1.209 us | 1.570 us | 1.661 us | 0.77x |
| `literal/call-token` | 3.078 us | 4.017 us | 14.874 us | 0.77x |
| `structural/ordered-modifier-pair` | 6.283 us | 8.727 us | 19.794 us | 0.72x |
| `literal/identifier-token` | 2.834 us | 4.340 us | 15.733 us | 0.65x |
| `lokad/lexer/string` | 29.471 us | 47.068 us | 49.720 us | 0.63x |
| `structural/ordered-identifier-window` | 6.521 us | 10.784 us | 21.665 us | 0.60x |
| `lokad/langserv/url-dashboard` | 3.063 us | 7.486 us | 8.013 us | 0.41x |
| `lokad/langserv/url-download` | 2.869 us | 13.064 us | 13.548 us | 0.22x |
| `lokad/folding/region-marker` | 6.979 us | 36.856 us | 59.795 us | 0.19x |
| `balancing/begin-end-blocks` | 7.550 us | 471.712 us | 498.169 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
