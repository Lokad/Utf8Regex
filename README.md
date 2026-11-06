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
and resource checks passed in **43m46s**. The [run receipt](bench/Experiments/DeliveryDev38-20261106/full-refresh.json)
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
| `literal/sherlock-zh` | 84.498 us | 18.708 us | 2,108.361 us | 4.52x |
| `literal/sherlock-ru` | 179.292 us | 100.379 us | 5,211.820 us | 1.79x |
| `literal-alternate/sherlock-zh` | 104.712 us | 67.646 us | 2,154.079 us | 1.55x |
| `literal/sherlock-casei-en` | 156.941 us | 189.414 us | 611.008 us | 0.83x |
| `literal/sherlock-casei-ru` | 252.431 us | 366.684 us | 5,514.078 us | 0.69x |
| `literal/sherlock-en` | 56.265 us | 86.870 us | 450.108 us | 0.65x |
| `aws-keys/quick` | 5,761.311 us | 9,937.200 us | 42,600.000 us | 0.58x |
| `ruff-noqa/tweaked` | 4,304.508 us | 7,449.913 us | 41,265.850 us | 0.58x |
| `bounded-repeat/letters-en` | 1,021.394 us | 2,758.047 us | 2,847.933 us | 0.37x |
| `literal-alternate/sherlock-en-mixed` | 207.984 us | 587.292 us | 1,032.444 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,644.445 us | 4,783.330 us | 10,064.400 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,947.708 us | 5,911.878 us | 6,679.650 us | 0.33x |
| `dictionary/single` | 8,220.357 us | 25,244.300 us | 25,304.450 us | 0.33x |
| `literal-alternate/sherlock-en` | 389.222 us | 1,464.377 us | 2,018.922 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 152.318 us | 655.434 us | 1,084.465 us | 0.23x |
| `bounded-repeat/capitals` | 9,281.600 us | 45,928.950 us | 52,335.900 us | 0.20x |
| `bounded-repeat/context` | 23,448.200 us | 145,659.500 us | 148,127.600 us | 0.16x |
| `aws-keys/full` | 10,439.983 us | 85,938.300 us | 115,282.000 us | 0.12x |
| `ruff-noqa/real` | 4,380.742 us | 69,222.500 us | 85,744.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 944.557 us | 23,289.233 us | 28,922.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 123.785 us | 11,559.520 us | 12,320.360 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,185.900 us | 507.256 us | 8,847.167 us | 2.34x |
| `common/match-word` | `Match` | 0.195 us | 0.162 us | 1.091 us | 1.20x |
| `industry/leipzig-twain-count` | `Count` | 1,151.891 us | 1,297.747 us | 8,186.933 us | 0.89x |
| `industry/mariomka-uri-count` | `Count` | 1,397.200 us | 1,860.600 us | 10,076.633 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.107 us | 0.144 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.191 us | 0.209 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.083 us | 0.099 us | 0.61x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.409 us | 262.694 us | 482.857 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.166 us | 0.180 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.079 us | 0.173 us | 0.191 us | 0.45x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.183 us | 0.202 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 3,944.015 us | 9,625.550 us | 16,523.750 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.228 us | 0.260 us | 0.40x |
| `common/replace-words` | `Replace` | 27.865 us | 77.897 us | 81.502 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.270 us | 0.290 us | 0.35x |
| `common/matches-word` | `Count` | 1.328 us | 4.233 us | 5.854 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.347 us | 0.367 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,281.445 us | 8,217.843 us | 16,340.950 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.107 us | 0.137 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.081 us | 0.096 us | 0.20x |
| `common/split-words` | `Split` | 14.300 us | 77.371 us | 78.717 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 172.903 us | 948.562 us | 1,224.880 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.085 us | 0.550 us | 0.588 us | 0.15x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.602 us | 0.643 us | 0.13x |
| `common/matches-words` | `Count` | 9.285 us | 70.501 us | 71.498 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.074 us | 0.088 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.103 us | 0.836 us | 0.869 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,004.215 us | 19,193.000 us | 20,111.400 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,259.363 us | 31,739.700 us | 44,761.500 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,848.996 us | 22,881.667 us | 29,681.400 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 480.733 us | 8,299.557 us | 8,913.150 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.383 us | 170.895 us | 173.339 us | 0.04x |
| `common/matches-set` | `Count` | 7.348 us | 195.188 us | 196.954 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 235.077 us | 11,811.120 us | 12,451.550 us | 0.02x |
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
| `literal/sherlock-zh` | 84.773 us | 23.566 us | 2,135.188 us | 3.60x |
| `literal-alternate/sherlock-zh` | 105.005 us | 35.825 us | 2,151.461 us | 2.93x |
| `literal/sherlock-casei-en` | 157.223 us | 77.627 us | 433.481 us | 2.03x |
| `literal/sherlock-ru` | 178.734 us | 90.729 us | 5,107.080 us | 1.97x |
| `aws-keys/quick` | 6,548.244 us | 3,642.486 us | 33,308.550 us | 1.80x |
| `literal/sherlock-casei-ru` | 253.985 us | 195.146 us | 5,259.880 us | 1.30x |
| `literal-alternate/sherlock-en-mixed` | 207.736 us | 174.854 us | 563.311 us | 1.19x |
| `literal-alternate/sherlock-en` | 387.408 us | 345.291 us | 758.351 us | 1.12x |
| `ruff-noqa/tweaked` | 4,449.992 us | 4,507.792 us | 35,581.950 us | 0.99x |
| `bounded-repeat/letters-ru` | 1,952.088 us | 2,019.988 us | 2,826.422 us | 0.97x |
| `literal-alternate/sherlock-casei-en` | 124.510 us | 149.891 us | 531.483 us | 0.83x |
| `literal-alternate/sherlock-en-nomatch` | 153.186 us | 198.204 us | 569.400 us | 0.77x |
| `literal/sherlock-en` | 56.491 us | 76.362 us | 441.237 us | 0.74x |
| `literal-alternate/sherlock-ru` | 1,677.110 us | 2,329.168 us | 7,460.367 us | 0.72x |
| `bounded-repeat/capitals` | 9,292.833 us | 13,401.900 us | 19,712.333 us | 0.69x |
| `aws-keys/full` | 10,608.283 us | 18,448.600 us | 48,645.650 us | 0.58x |
| `bounded-repeat/letters-en` | 1,028.734 us | 1,793.524 us | 1,882.356 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 951.274 us | 1,940.400 us | 7,188.600 us | 0.49x |
| `bounded-repeat/context` | 23,675.300 us | 68,596.200 us | 71,137.700 us | 0.35x |
| `ruff-noqa/real` | 4,459.092 us | 21,132.600 us | 32,696.050 us | 0.21x |
| `dictionary/single` | 8,223.557 us | 45,414.650 us | 45,348.150 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.192 us | 0.073 us | 0.982 us | 2.64x |
| `industry/mariomka-email-count` | `Count` | 1,191.807 us | 471.960 us | 7,971.150 us | 2.53x |
| `common/backtracking` | `IsMatch` | 0.103 us | 0.043 us | 0.074 us | 2.38x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.606 us | 57.987 us | 251.887 us | 2.29x |
| `industry/leipzig-river-window-count` | `Count` | 3,285.181 us | 1,489.729 us | 13,469.733 us | 2.21x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.065 us | 0.086 us | 1.81x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.087 us | 1.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.030 us | 0.049 us | 1.64x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.048 us | 0.030 us | 0.047 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.047 us | 0.074 us | 1.57x |
| `common/replace-words` | `Replace` | 27.478 us | 17.536 us | 18.498 us | 1.57x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.049 us | 0.069 us | 1.56x |
| `industry/leipzig-symbol-count` | `Count` | 1,844.211 us | 1,260.352 us | 6,907.367 us | 1.46x |
| `industry/leipzig-name-family-count` | `Count` | 4,054.946 us | 2,975.235 us | 8,117.400 us | 1.36x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.055 us | 0.077 us | 1.36x |
| `industry/mariomka-uri-count` | `Count` | 1,407.517 us | 1,063.719 us | 8,246.367 us | 1.32x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.079 us | 0.106 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.088 us | 0.116 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.088 us | 0.117 us | 1.03x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.087 us | 0.127 us | 0.91x |
| `industry/leipzig-twain-count` | `Count` | 1,156.930 us | 1,286.702 us | 6,774.986 us | 0.90x |
| `common/matches-words` | `Count` | 9.241 us | 10.583 us | 13.625 us | 0.87x |
| `common/split-words` | `Split` | 14.230 us | 21.071 us | 22.643 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,286.474 us | 3,407.280 us | 11,176.450 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.075 us | 0.66x |
| `common/matches-word` | `Count` | 1.326 us | 2.331 us | 3.512 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.201 us | 0.237 us | 0.43x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.068 us | 589.946 us | 866.398 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.051 us | 0.29x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 482.721 us | 3,901.915 us | 4,374.292 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,992.108 us | 17,970.800 us | 19,044.300 us | 0.11x |
| `common/matches-set` | `Count` | 7.427 us | 104.691 us | 106.165 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.380 us | 105.054 us | 106.240 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 235.255 us | 4,702.209 us | 5,113.560 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.737 us | 6.350 us | 29.520 us | 1.85x |
| `structural/keyword-to-generic-type` | 5.426 us | 3.987 us | 15.490 us | 1.36x |
| `lokad/langserv/color-short-hex` | 1.642 us | 1.296 us | 1.411 us | 1.27x |
| `lokad/lexer/doc-line` | 3.308 us | 2.990 us | 3.499 us | 1.11x |
| `lokad/lexer/identifier` | 23.786 us | 22.524 us | 25.010 us | 1.06x |
| `lokad/langserv/helper-identifier` | 7.074 us | 6.953 us | 7.521 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.270 us | 5.355 us | 16.928 us | 0.98x |
| `lokad/style/hex-color` | 0.727 us | 0.774 us | 0.845 us | 0.94x |
| `lokad/langserv/identifier-validator` | 11.213 us | 12.153 us | 13.557 us | 0.92x |
| `lokad/lexer/operator-run` | 2.893 us | 3.174 us | 3.673 us | 0.91x |
| `fallback/lookahead` | 5.316 us | 5.990 us | 17.633 us | 0.89x |
| `literal-family/method-token-family` | 6.962 us | 9.086 us | 19.546 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.033 us | 2.815 us | 2.874 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 3.355 us | 4.693 us | 5.362 us | 0.71x |
| `literal/identifier-token` | 2.735 us | 4.243 us | 15.522 us | 0.64x |
| `literal/call-token` | 2.554 us | 4.195 us | 15.250 us | 0.61x |
| `lokad/lexer/number` | 8.235 us | 13.575 us | 14.693 us | 0.61x |
| `lokad/style/cell-ref` | 1.717 us | 2.927 us | 2.928 us | 0.59x |
| `structural/ordered-identifier-window` | 15.865 us | 28.116 us | 39.620 us | 0.56x |
| `structural/method-family-generic-call` | 3.419 us | 6.482 us | 17.744 us | 0.53x |
| `structural/method-family-call` | 3.822 us | 7.936 us | 18.529 us | 0.48x |
| `literal-family/type-token-family` | 10.335 us | 23.134 us | 34.247 us | 0.45x |
| `literal/identifier-token-casei` | 5.967 us | 14.683 us | 25.984 us | 0.41x |
| `lokad/style/range-ref` | 0.980 us | 2.662 us | 2.694 us | 0.37x |
| `structural/ordered-modifier-pair` | 5.521 us | 17.508 us | 28.640 us | 0.32x |
| `structural/ordered-keyword-window` | 5.409 us | 17.381 us | 28.953 us | 0.31x |
| `structural/modifier-family-to-type-window` | 76.622 us | 251.832 us | 265.770 us | 0.30x |
| `lokad/lexer/string` | 21.193 us | 91.991 us | 97.369 us | 0.23x |
| `lokad/folding/region-marker` | 6.273 us | 36.563 us | 60.522 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.475 us | 23.564 us | 24.056 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.289 us | 218.635 us | 229.325 us | 0.05x |
| `lokad/langserv/url-download` | 2.562 us | 51.454 us | 52.626 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.965 us | 128.797 us | 142.518 us | 0.04x |
| `balancing/begin-end-blocks` | 5.527 us | 4,268.546 us | 4,182.575 us | 0.0013x |
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
| `lokad/imports/module-imports` | 11.599 us | 4.422 us | 26.713 us | 2.62x |
| `lokad/langserv/color-short-hex` | 1.648 us | 0.953 us | 1.142 us | 1.73x |
| `lokad/lexer/doc-line` | 3.354 us | 2.083 us | 2.815 us | 1.61x |
| `structural/keyword-to-generic-type` | 5.413 us | 3.400 us | 14.437 us | 1.59x |
| `lokad/langserv/helper-identifier` | 7.050 us | 4.562 us | 5.361 us | 1.55x |
| `lokad/lexer/operator-run` | 2.867 us | 1.859 us | 2.403 us | 1.54x |
| `structural/ordered-identifier-window` | 15.803 us | 10.521 us | 21.316 us | 1.50x |
| `structural/keyword-family-to-capitalized-identifier` | 11.313 us | 7.790 us | 18.485 us | 1.45x |
| `lokad/style/hex-color` | 0.729 us | 0.504 us | 0.580 us | 1.45x |
| `lokad/lexer/identifier` | 23.535 us | 16.278 us | 20.166 us | 1.45x |
| `structural/modifier-family-to-type-window` | 76.487 us | 55.140 us | 66.762 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.282 us | 4.058 us | 15.105 us | 1.30x |
| `fallback/lookahead` | 5.326 us | 4.147 us | 15.168 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.122 us | 8.840 us | 10.780 us | 1.26x |
| `lokad/docs/inline-doc-prefix` | 3.352 us | 2.779 us | 3.493 us | 1.21x |
| `lokad/langserv/color-rgb-hex` | 2.021 us | 1.772 us | 1.987 us | 1.14x |
| `lokad/lexer/number` | 8.106 us | 7.208 us | 8.340 us | 1.12x |
| `literal/identifier-token-casei` | 5.962 us | 5.821 us | 16.970 us | 1.02x |
| `lokad/style/cell-ref` | 1.679 us | 1.777 us | 1.878 us | 0.95x |
| `literal-family/type-token-family` | 10.276 us | 11.234 us | 21.818 us | 0.91x |
| `structural/method-family-generic-call` | 3.397 us | 4.435 us | 14.889 us | 0.77x |
| `structural/method-family-call` | 3.770 us | 5.011 us | 15.355 us | 0.75x |
| `literal-family/method-token-family` | 3.805 us | 5.239 us | 15.519 us | 0.73x |
| `lokad/style/range-ref` | 0.970 us | 1.476 us | 1.534 us | 0.66x |
| `literal/call-token` | 2.535 us | 3.859 us | 14.480 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.528 us | 8.476 us | 19.617 us | 0.65x |
| `literal/identifier-token` | 2.736 us | 4.215 us | 15.411 us | 0.65x |
| `structural/ordered-keyword-window` | 5.364 us | 8.477 us | 19.668 us | 0.63x |
| `lokad/lexer/string` | 21.152 us | 45.313 us | 48.159 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.913 us | 10.695 us | 21.439 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.459 us | 7.244 us | 7.695 us | 0.34x |
| `lokad/langserv/url-download` | 2.551 us | 12.674 us | 13.167 us | 0.20x |
| `lokad/folding/region-marker` | 6.323 us | 35.292 us | 60.969 us | 0.18x |
| `balancing/begin-end-blocks` | 5.568 us | 457.061 us | 489.111 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
