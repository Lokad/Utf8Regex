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
| `literal/sherlock-zh` | 132.834 us | 18.987 us | 2,155.374 us | 7.00x |
| `literal-alternate/sherlock-zh` | 238.176 us | 68.625 us | 2,221.804 us | 3.47x |
| `literal/sherlock-ru` | 254.684 us | 98.712 us | 5,275.220 us | 2.58x |
| `ruff-noqa/tweaked` | 10,624.860 us | 7,952.586 us | 43,646.300 us | 1.34x |
| `literal/sherlock-casei-en` | 176.087 us | 192.107 us | 589.103 us | 0.92x |
| `literal/sherlock-en` | 70.102 us | 82.539 us | 490.658 us | 0.85x |
| `aws-keys/quick` | 6,652.867 us | 9,894.183 us | 45,010.750 us | 0.67x |
| `literal/sherlock-casei-ru` | 253.346 us | 398.163 us | 5,843.589 us | 0.64x |
| `bounded-repeat/letters-en` | 1,066.075 us | 2,840.817 us | 2,950.171 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,694.887 us | 4,871.009 us | 10,053.133 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 208.489 us | 601.344 us | 1,040.443 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,982.496 us | 6,012.533 us | 6,812.988 us | 0.33x |
| `dictionary/single` | 8,449.483 us | 26,291.450 us | 26,341.150 us | 0.32x |
| `literal-alternate/sherlock-en` | 393.059 us | 1,510.176 us | 2,052.533 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 168.185 us | 670.655 us | 1,111.771 us | 0.25x |
| `bounded-repeat/capitals` | 9,691.883 us | 46,729.350 us | 52,977.000 us | 0.21x |
| `aws-keys/full` | 10,797.180 us | 88,049.700 us | 122,881.600 us | 0.12x |
| `bounded-repeat/context` | 15,953.050 us | 149,250.500 us | 152,921.400 us | 0.11x |
| `ruff-noqa/real` | 4,699.327 us | 70,331.500 us | 87,338.100 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,088.304 us | 23,281.700 us | 28,735.050 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 231.385 us | 11,537.220 us | 12,306.180 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,534.197 us | 494.023 us | 9,016.933 us | 3.11x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.176 us | 0.111 us | 0.149 us | 1.59x |
| `common/match-word` | `Match` | 0.200 us | 0.168 us | 1.152 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,262.605 us | 1,354.223 us | 8,864.533 us | 0.93x |
| `industry/mariomka-uri-count` | `Count` | 1,749.190 us | 1,941.033 us | 10,567.667 us | 0.90x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 240.346 us | 270.227 us | 526.822 us | 0.89x |
| `common/uri-miss` | `IsMatch` | 0.158 us | 0.193 us | 0.210 us | 0.82x |
| `common/replace-words` | `Replace` | 66.280 us | 81.681 us | 84.355 us | 0.81x |
| `common/uri-match` | `IsMatch` | 0.168 us | 0.235 us | 0.269 us | 0.71x |
| `common/ip-match` | `IsMatch` | 0.114 us | 0.173 us | 0.193 us | 0.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.099 us | 0.60x |
| `common/ip-miss` | `IsMatch` | 0.106 us | 0.187 us | 0.206 us | 0.56x |
| `common/email-match` | `IsMatch` | 0.150 us | 0.274 us | 0.302 us | 0.55x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.164 us | 0.182 us | 0.48x |
| `common/email-miss` | `IsMatch` | 0.164 us | 0.348 us | 0.373 us | 0.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,140.517 us | 10,041.340 us | 17,755.100 us | 0.41x |
| `common/matches-word` | `Count` | 1.355 us | 4.271 us | 6.019 us | 0.32x |
| `industry/mariomka-ip-count` | `Count` | 2,721.042 us | 8,756.600 us | 17,313.700 us | 0.31x |
| `common/one-node-backtracking` | `IsMatch` | 0.170 us | 0.617 us | 0.654 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.107 us | 0.137 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `common/backtracking` | `IsMatch` | 0.167 us | 0.835 us | 0.881 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 181.080 us | 975.904 us | 1,264.740 us | 0.19x |
| `common/split-words` | `Split` | 14.682 us | 80.025 us | 81.474 us | 0.18x |
| `industry/leipzig-river-window-count` | `Count` | 5,715.300 us | 32,299.200 us | 46,501.700 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.562 us | 0.595 us | 0.16x |
| `common/matches-words` | `Count` | 9.390 us | 72.072 us | 75.214 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.087 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,012.240 us | 19,684.250 us | 20,575.767 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,873.715 us | 23,519.933 us | 31,170.450 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 521.179 us | 8,372.150 us | 8,875.300 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.054 us | 169.723 us | 171.644 us | 0.04x |
| `common/matches-set` | `Count` | 6.947 us | 199.884 us | 201.729 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 253.072 us | 12,466.480 us | 12,876.200 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 237.072 us | 36.579 us | 2,178.358 us | 6.48x |
| `literal/sherlock-zh` | 133.137 us | 24.133 us | 2,172.442 us | 5.52x |
| `literal/sherlock-ru` | 254.126 us | 90.149 us | 5,206.500 us | 2.82x |
| `ruff-noqa/tweaked` | 10,912.540 us | 4,587.009 us | 39,349.300 us | 2.38x |
| `literal/sherlock-casei-en` | 175.232 us | 79.420 us | 445.065 us | 2.21x |
| `aws-keys/quick` | 6,670.244 us | 3,603.486 us | 37,029.850 us | 1.85x |
| `literal-alternate/sherlock-casei-en` | 221.405 us | 132.571 us | 576.214 us | 1.67x |
| `literal/sherlock-casei-ru` | 255.964 us | 222.998 us | 5,603.167 us | 1.15x |
| `literal-alternate/sherlock-en-mixed` | 208.609 us | 185.367 us | 566.791 us | 1.13x |
| `literal-alternate/sherlock-en` | 392.004 us | 376.464 us | 795.500 us | 1.04x |
| `literal/sherlock-en` | 69.911 us | 72.625 us | 475.937 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,983.696 us | 2,079.812 us | 2,884.250 us | 0.95x |
| `literal-alternate/sherlock-en-nomatch` | 167.496 us | 202.669 us | 588.479 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,693.993 us | 2,363.945 us | 7,542.667 us | 0.72x |
| `bounded-repeat/capitals` | 9,710.217 us | 13,717.400 us | 20,314.467 us | 0.71x |
| `bounded-repeat/letters-en` | 1,064.365 us | 1,850.375 us | 1,936.100 us | 0.58x |
| `aws-keys/full` | 10,788.320 us | 19,005.367 us | 52,967.750 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 1,089.115 us | 1,983.781 us | 7,214.067 us | 0.55x |
| `bounded-repeat/context` | 16,139.700 us | 69,937.300 us | 72,660.000 us | 0.23x |
| `ruff-noqa/real` | 4,690.073 us | 21,283.033 us | 35,069.100 us | 0.22x |
| `dictionary/single` | 8,443.367 us | 46,358.200 us | 46,352.250 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/rust-sherlock-holmes-window-count` | `Count` | 241.467 us | 60.004 us | 288.467 us | 4.02x |
| `industry/leipzig-river-window-count` | `Count` | 5,725.867 us | 1,541.950 us | 14,224.433 us | 3.71x |
| `common/backtracking` | `IsMatch` | 0.165 us | 0.045 us | 0.074 us | 3.69x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.177 us | 0.049 us | 0.089 us | 3.61x |
| `common/replace-words` | `Replace` | 66.351 us | 18.824 us | 20.253 us | 3.52x |
| `industry/mariomka-email-count` | `Count` | 1,536.545 us | 458.954 us | 7,876.167 us | 3.35x |
| `common/match-word` | `Match` | 0.199 us | 0.079 us | 1.001 us | 2.54x |
| `common/uri-miss` | `IsMatch` | 0.158 us | 0.068 us | 0.088 us | 2.32x |
| `common/ip-match` | `IsMatch` | 0.115 us | 0.050 us | 0.069 us | 2.30x |
| `common/ip-miss` | `IsMatch` | 0.104 us | 0.050 us | 0.070 us | 2.09x |
| `common/one-node-backtracking` | `IsMatch` | 0.170 us | 0.088 us | 0.130 us | 1.92x |
| `common/uri-match` | `IsMatch` | 0.166 us | 0.088 us | 0.120 us | 1.88x |
| `common/email-match` | `IsMatch` | 0.150 us | 0.081 us | 0.108 us | 1.84x |
| `common/email-miss` | `IsMatch` | 0.165 us | 0.090 us | 0.115 us | 1.84x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.029 us | 0.047 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.048 us | 1.66x |
| `industry/mariomka-uri-count` | `Count` | 1,749.572 us | 1,103.641 us | 8,585.100 us | 1.59x |
| `industry/leipzig-symbol-count` | `Count` | 1,881.085 us | 1,271.950 us | 7,026.533 us | 1.48x |
| `industry/leipzig-name-family-count` | `Count` | 4,297.283 us | 3,142.612 us | 8,626.467 us | 1.37x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.34x |
| `industry/leipzig-twain-count` | `Count` | 1,264.300 us | 1,282.148 us | 5,618.067 us | 0.99x |
| `common/matches-words` | `Count` | 9.377 us | 10.987 us | 14.093 us | 0.85x |
| `industry/mariomka-ip-count` | `Count` | 2,728.979 us | 3,539.247 us | 11,908.500 us | 0.77x |
| `common/split-words` | `Split` | 14.671 us | 21.180 us | 23.503 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.045 us | 0.074 us | 0.65x |
| `common/matches-word` | `Count` | 1.353 us | 2.348 us | 3.642 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.203 us | 0.237 us | 0.43x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.176 us | 599.931 us | 878.195 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.051 us | 0.28x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 522.013 us | 3,976.046 us | 4,388.117 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,013.424 us | 18,083.350 us | 18,949.567 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.122 us | 106.331 us | 108.196 us | 0.07x |
| `common/matches-set` | `Count` | 6.978 us | 106.536 us | 107.986 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 249.302 us | 4,894.336 us | 5,318.580 us | 0.05x |
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
| `lokad/imports/module-imports` | 31.521 us | 6.594 us | 28.675 us | 4.78x |
| `lokad/lexer/doc-line` | 4.942 us | 3.055 us | 3.645 us | 1.62x |
| `lokad/lexer/identifier` | 33.437 us | 22.934 us | 26.030 us | 1.46x |
| `structural/keyword-to-generic-type` | 5.329 us | 4.082 us | 15.365 us | 1.31x |
| `lokad/lexer/operator-run` | 4.173 us | 3.267 us | 3.819 us | 1.28x |
| `lokad/langserv/color-short-hex` | 1.666 us | 1.340 us | 1.461 us | 1.24x |
| `lokad/langserv/helper-identifier` | 9.787 us | 8.713 us | 8.693 us | 1.12x |
| `lokad/style/hex-color` | 0.820 us | 0.772 us | 0.854 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.232 us | 5.291 us | 17.030 us | 0.99x |
| `lokad/docs/inline-doc-prefix` | 4.643 us | 5.000 us | 5.567 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.350 us | 12.328 us | 13.660 us | 0.92x |
| `fallback/lookahead` | 5.555 us | 6.117 us | 17.375 us | 0.91x |
| `lokad/lexer/number` | 11.547 us | 14.110 us | 14.825 us | 0.82x |
| `literal-family/method-token-family` | 7.032 us | 8.981 us | 19.501 us | 0.78x |
| `lokad/style/cell-ref` | 2.136 us | 2.787 us | 2.928 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.046 us | 2.899 us | 2.956 us | 0.71x |
| `literal/identifier-token` | 2.742 us | 4.335 us | 15.389 us | 0.63x |
| `literal/call-token` | 2.558 us | 4.201 us | 15.337 us | 0.61x |
| `structural/method-family-generic-call` | 3.475 us | 6.575 us | 17.885 us | 0.53x |
| `structural/method-family-call` | 3.951 us | 8.200 us | 18.855 us | 0.48x |
| `lokad/style/range-ref` | 1.306 us | 2.773 us | 2.822 us | 0.47x |
| `literal-family/type-token-family` | 10.486 us | 24.699 us | 35.458 us | 0.42x |
| `literal/identifier-token-casei` | 5.766 us | 15.569 us | 26.122 us | 0.37x |
| `lokad/lexer/string` | 31.059 us | 93.512 us | 99.295 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.694 us | 17.355 us | 28.347 us | 0.33x |
| `structural/ordered-keyword-window` | 5.381 us | 17.633 us | 28.389 us | 0.31x |
| `structural/keyword-family-to-capitalized-identifier` | 64.463 us | 221.197 us | 232.959 us | 0.29x |
| `structural/modifier-family-to-type-window` | 73.876 us | 255.303 us | 269.175 us | 0.29x |
| `lokad/folding/region-marker` | 6.895 us | 35.921 us | 61.115 us | 0.19x |
| `structural/modifier-family-to-type-pair` | 23.623 us | 132.307 us | 144.019 us | 0.18x |
| `structural/ordered-identifier-window` | 4.897 us | 28.417 us | 39.226 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.876 us | 24.725 us | 25.149 us | 0.12x |
| `lokad/langserv/url-download` | 2.871 us | 51.375 us | 51.613 us | 0.06x |
| `balancing/begin-end-blocks` | 7.394 us | 4,374.692 us | 4,401.933 us | 0.0017x |
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
| `structural/keyword-family-to-capitalized-identifier` | 64.539 us | 7.567 us | 18.660 us | 8.53x |
| `lokad/imports/module-imports` | 31.557 us | 4.467 us | 25.970 us | 7.06x |
| `structural/modifier-family-to-type-pair` | 23.656 us | 10.756 us | 21.653 us | 2.20x |
| `lokad/lexer/doc-line` | 4.841 us | 2.241 us | 2.953 us | 2.16x |
| `lokad/lexer/operator-run` | 4.110 us | 1.924 us | 2.514 us | 2.14x |
| `lokad/langserv/helper-identifier` | 9.775 us | 4.641 us | 5.703 us | 2.11x |
| `lokad/lexer/identifier` | 33.297 us | 16.800 us | 20.287 us | 1.98x |
| `lokad/langserv/color-short-hex` | 1.656 us | 0.989 us | 1.161 us | 1.67x |
| `lokad/docs/inline-doc-prefix` | 4.594 us | 2.794 us | 3.522 us | 1.64x |
| `lokad/style/hex-color` | 0.826 us | 0.509 us | 0.599 us | 1.62x |
| `lokad/lexer/number` | 11.629 us | 7.352 us | 8.351 us | 1.58x |
| `structural/keyword-to-generic-type` | 5.340 us | 3.475 us | 14.280 us | 1.54x |
| `structural/modifier-family-to-type-window` | 74.112 us | 55.486 us | 69.190 us | 1.34x |
| `fallback/lookahead` | 5.564 us | 4.258 us | 15.168 us | 1.31x |
| `structural/ordered-keyword-pair` | 5.258 us | 4.134 us | 15.142 us | 1.27x |
| `lokad/langserv/identifier-validator` | 11.381 us | 9.175 us | 10.885 us | 1.24x |
| `lokad/style/cell-ref` | 2.123 us | 1.721 us | 1.919 us | 1.23x |
| `lokad/langserv/color-rgb-hex` | 2.042 us | 1.862 us | 2.003 us | 1.10x |
| `literal/identifier-token-casei` | 5.715 us | 5.884 us | 16.862 us | 0.97x |
| `literal-family/type-token-family` | 10.403 us | 11.413 us | 22.124 us | 0.91x |
| `lokad/style/range-ref` | 1.320 us | 1.535 us | 1.564 us | 0.86x |
| `structural/method-family-call` | 3.898 us | 5.085 us | 15.542 us | 0.77x |
| `structural/method-family-generic-call` | 3.464 us | 4.554 us | 15.062 us | 0.76x |
| `literal-family/method-token-family` | 3.791 us | 5.071 us | 15.520 us | 0.75x |
| `lokad/lexer/string` | 31.036 us | 45.672 us | 48.808 us | 0.68x |
| `literal/call-token` | 2.552 us | 3.847 us | 14.450 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.685 us | 8.770 us | 19.633 us | 0.65x |
| `structural/ordered-keyword-window` | 5.382 us | 8.527 us | 19.296 us | 0.63x |
| `literal/identifier-token` | 2.766 us | 4.709 us | 14.874 us | 0.59x |
| `structural/ordered-identifier-window` | 4.908 us | 10.576 us | 21.472 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.876 us | 7.451 us | 7.881 us | 0.39x |
| `lokad/langserv/url-download` | 2.868 us | 12.979 us | 13.486 us | 0.22x |
| `lokad/folding/region-marker` | 6.891 us | 37.025 us | 59.851 us | 0.19x |
| `balancing/begin-end-blocks` | 7.450 us | 471.194 us | 497.563 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
