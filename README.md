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
| `literal/sherlock-zh` | 130.333 us | 18.289 us | 2,165.325 us | 7.13x |
| `literal-alternate/sherlock-zh` | 231.914 us | 66.870 us | 2,159.162 us | 3.47x |
| `bounded-repeat/capitals` | 125,538.500 us | 46,511.150 us | 52,463.200 us | 2.70x |
| `literal/sherlock-ru` | 242.302 us | 94.284 us | 5,081.470 us | 2.57x |
| `ruff-noqa/tweaked` | 10,360.940 us | 7,532.029 us | 40,976.150 us | 1.38x |
| `literal/sherlock-casei-en` | 174.143 us | 185.969 us | 566.308 us | 0.94x |
| `literal/sherlock-en` | 68.620 us | 85.192 us | 454.185 us | 0.81x |
| `aws-keys/quick` | 6,528.600 us | 9,500.950 us | 43,641.350 us | 0.69x |
| `literal/sherlock-casei-ru` | 248.539 us | 378.399 us | 5,401.550 us | 0.66x |
| `bounded-repeat/letters-en` | 1,025.082 us | 2,725.094 us | 2,856.161 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,688.152 us | 4,658.691 us | 9,721.967 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,914.068 us | 5,840.389 us | 6,671.275 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 191.028 us | 588.181 us | 1,036.779 us | 0.32x |
| `dictionary/single` | 7,989.486 us | 26,030.750 us | 26,048.300 us | 0.31x |
| `literal-alternate/sherlock-en` | 384.347 us | 1,500.021 us | 2,054.488 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 163.763 us | 646.271 us | 1,063.831 us | 0.25x |
| `bounded-repeat/context` | 20,953.900 us | 146,209.900 us | 148,627.700 us | 0.14x |
| `aws-keys/full` | 10,787.067 us | 84,707.600 us | 114,509.800 us | 0.13x |
| `ruff-noqa/real` | 4,662.618 us | 68,798.800 us | 85,298.500 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,061.621 us | 22,559.300 us | 27,461.600 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 211.316 us | 11,420.160 us | 12,383.100 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,490.238 us | 478.583 us | 8,912.733 us | 3.11x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.179 us | 0.109 us | 0.145 us | 1.65x |
| `common/match-word` | `Match` | 0.196 us | 0.166 us | 1.100 us | 1.18x |
| `common/uri-miss` | `IsMatch` | 0.182 us | 0.190 us | 0.208 us | 0.96x |
| `industry/mariomka-uri-count` | `Count` | 1,730.883 us | 1,845.185 us | 10,205.600 us | 0.94x |
| `industry/leipzig-twain-count` | `Count` | 1,236.483 us | 1,321.575 us | 8,401.583 us | 0.94x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 236.258 us | 253.582 us | 515.682 us | 0.93x |
| `common/replace-words` | `Replace` | 66.174 us | 78.834 us | 80.693 us | 0.84x |
| `common/uri-match` | `IsMatch` | 0.175 us | 0.223 us | 0.257 us | 0.79x |
| `common/ip-match` | `IsMatch` | 0.116 us | 0.170 us | 0.190 us | 0.68x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.082 us | 0.098 us | 0.65x |
| `common/email-match` | `IsMatch` | 0.168 us | 0.266 us | 0.289 us | 0.63x |
| `common/ip-miss` | `IsMatch` | 0.109 us | 0.181 us | 0.202 us | 0.60x |
| `common/email-miss` | `IsMatch` | 0.174 us | 0.342 us | 0.364 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.082 us | 0.165 us | 0.180 us | 0.50x |
| `industry/leipzig-name-family-count` | `Count` | 4,161.425 us | 9,631.933 us | 17,007.550 us | 0.43x |
| `common/matches-word` | `Count` | 1.424 us | 4.126 us | 5.832 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.199 us | 0.604 us | 0.633 us | 0.33x |
| `industry/mariomka-ip-count` | `Count` | 2,622.125 us | 8,147.129 us | 16,677.350 us | 0.32x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.107 us | 0.136 us | 0.27x |
| `common/backtracking` | `IsMatch` | 0.184 us | 0.819 us | 0.858 us | 0.22x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.078 us | 0.094 us | 0.21x |
| `industry/leipzig-river-window-count` | `Count` | 5,773.500 us | 31,268.150 us | 45,089.100 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 172.801 us | 940.124 us | 1,231.739 us | 0.18x |
| `common/split-words` | `Split` | 14.503 us | 79.013 us | 81.465 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.094 us | 0.552 us | 0.572 us | 0.17x |
| `common/matches-words` | `Count` | 9.380 us | 69.522 us | 70.841 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.088 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,058.132 us | 19,052.650 us | 19,987.633 us | 0.11x |
| `industry/leipzig-symbol-count` | `Count` | 1,776.797 us | 22,209.867 us | 28,953.400 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 515.441 us | 8,206.314 us | 8,758.550 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.639 us | 161.327 us | 164.191 us | 0.04x |
| `common/matches-set` | `Count` | 6.578 us | 192.062 us | 191.542 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 259.513 us | 11,896.600 us | 12,451.875 us | 0.02x |
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
| `bounded-repeat/capitals` | 125,784.500 us | 13,337.900 us | 19,994.100 us | 9.43x |
| `literal-alternate/sherlock-zh` | 235.443 us | 35.138 us | 2,133.775 us | 6.70x |
| `literal/sherlock-zh` | 130.583 us | 23.356 us | 2,146.088 us | 5.59x |
| `literal/sherlock-ru` | 242.672 us | 86.234 us | 5,126.170 us | 2.81x |
| `ruff-noqa/tweaked` | 10,463.400 us | 4,553.983 us | 34,562.150 us | 2.30x |
| `literal/sherlock-casei-en` | 173.415 us | 77.754 us | 441.958 us | 2.23x |
| `aws-keys/quick` | 6,553.000 us | 3,565.520 us | 35,695.500 us | 1.84x |
| `literal-alternate/sherlock-casei-en` | 210.566 us | 140.254 us | 582.789 us | 1.50x |
| `literal/sherlock-casei-ru` | 249.848 us | 196.116 us | 5,281.180 us | 1.27x |
| `literal-alternate/sherlock-en` | 385.291 us | 335.533 us | 737.684 us | 1.15x |
| `literal-alternate/sherlock-en-mixed` | 191.365 us | 175.476 us | 535.048 us | 1.09x |
| `literal/sherlock-en` | 68.079 us | 75.079 us | 431.278 us | 0.91x |
| `bounded-repeat/letters-ru` | 1,893.082 us | 2,124.375 us | 2,917.718 us | 0.89x |
| `literal-alternate/sherlock-en-nomatch` | 163.412 us | 196.677 us | 584.394 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,650.484 us | 2,289.718 us | 7,354.700 us | 0.72x |
| `aws-keys/full` | 10,854.700 us | 18,583.367 us | 49,474.400 us | 0.58x |
| `bounded-repeat/letters-en` | 1,012.820 us | 1,807.517 us | 1,877.537 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,055.641 us | 1,913.367 us | 7,012.833 us | 0.55x |
| `bounded-repeat/context` | 20,303.133 us | 68,155.100 us | 70,361.100 us | 0.30x |
| `ruff-noqa/real` | 4,646.700 us | 21,293.900 us | 33,991.100 us | 0.22x |
| `dictionary/single` | 8,113.443 us | 44,937.800 us | 44,777.150 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/backtracking` | `IsMatch` | 0.185 us | 0.043 us | 0.072 us | 4.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 237.608 us | 57.133 us | 265.889 us | 4.16x |
| `industry/leipzig-river-window-count` | `Count` | 5,717.633 us | 1,507.451 us | 13,641.867 us | 3.79x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.178 us | 0.048 us | 0.085 us | 3.71x |
| `common/replace-words` | `Replace` | 65.981 us | 18.822 us | 20.688 us | 3.51x |
| `industry/mariomka-email-count` | `Count` | 1,483.706 us | 446.912 us | 7,522.333 us | 3.32x |
| `common/uri-miss` | `IsMatch` | 0.182 us | 0.067 us | 0.085 us | 2.72x |
| `common/match-word` | `Match` | 0.195 us | 0.075 us | 0.970 us | 2.61x |
| `common/ip-match` | `IsMatch` | 0.115 us | 0.048 us | 0.067 us | 2.42x |
| `common/one-node-backtracking` | `IsMatch` | 0.203 us | 0.087 us | 0.125 us | 2.34x |
| `common/ip-miss` | `IsMatch` | 0.108 us | 0.048 us | 0.069 us | 2.26x |
| `common/email-match` | `IsMatch` | 0.169 us | 0.081 us | 0.107 us | 2.09x |
| `common/uri-match` | `IsMatch` | 0.175 us | 0.086 us | 0.118 us | 2.03x |
| `common/email-miss` | `IsMatch` | 0.174 us | 0.088 us | 0.114 us | 1.98x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.028 us | 0.046 us | 1.76x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.031 us | 0.048 us | 1.69x |
| `industry/mariomka-uri-count` | `Count` | 1,724.863 us | 1,080.443 us | 8,440.967 us | 1.60x |
| `industry/leipzig-symbol-count` | `Count` | 1,790.121 us | 1,204.324 us | 6,649.700 us | 1.49x |
| `industry/leipzig-name-family-count` | `Count` | 4,326.408 us | 3,052.724 us | 8,489.100 us | 1.42x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.074 us | 0.054 us | 0.074 us | 1.36x |
| `industry/leipzig-twain-count` | `Count` | 1,252.322 us | 1,274.885 us | 7,000.157 us | 0.98x |
| `common/matches-words` | `Count` | 9.273 us | 10.689 us | 13.798 us | 0.87x |
| `industry/mariomka-ip-count` | `Count` | 2,641.295 us | 3,403.760 us | 11,042.500 us | 0.78x |
| `common/split-words` | `Split` | 14.484 us | 20.812 us | 22.928 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.073 us | 0.68x |
| `common/matches-word` | `Count` | 1.382 us | 2.282 us | 3.452 us | 0.61x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.198 us | 0.229 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.053 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.723 us | 586.069 us | 849.395 us | 0.29x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 510.562 us | 3,884.343 us | 4,312.600 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,085.264 us | 17,253.300 us | 18,156.467 us | 0.12x |
| `common/matches-boundary` | `Count` | 6.675 us | 102.661 us | 105.185 us | 0.07x |
| `common/matches-set` | `Count` | 6.654 us | 103.286 us | 104.839 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 258.362 us | 4,689.227 us | 5,115.930 us | 0.06x |
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
| `lokad/imports/module-imports` | 31.042 us | 6.331 us | 29.082 us | 4.90x |
| `lokad/lexer/doc-line` | 4.229 us | 2.886 us | 3.580 us | 1.47x |
| `structural/keyword-to-generic-type` | 5.679 us | 3.993 us | 15.717 us | 1.42x |
| `lokad/lexer/identifier` | 30.225 us | 22.235 us | 25.131 us | 1.36x |
| `lokad/langserv/helper-identifier` | 9.246 us | 6.962 us | 7.611 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.627 us | 1.316 us | 1.445 us | 1.24x |
| `structural/ordered-keyword-pair` | 6.085 us | 5.151 us | 16.526 us | 1.18x |
| `lokad/style/hex-color` | 0.899 us | 0.778 us | 0.855 us | 1.16x |
| `lokad/lexer/operator-run` | 3.654 us | 3.275 us | 3.791 us | 1.12x |
| `fallback/lookahead` | 5.984 us | 5.969 us | 16.925 us | 1.00x |
| `lokad/docs/inline-doc-prefix` | 4.306 us | 4.687 us | 5.344 us | 0.92x |
| `literal-family/method-token-family` | 7.917 us | 9.022 us | 19.626 us | 0.88x |
| `lokad/lexer/number` | 10.539 us | 13.920 us | 14.362 us | 0.76x |
| `literal/call-token` | 2.986 us | 4.205 us | 15.554 us | 0.71x |
| `lokad/langserv/identifier-validator` | 11.365 us | 16.249 us | 15.049 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 1.987 us | 2.866 us | 2.930 us | 0.69x |
| `lokad/style/cell-ref` | 1.885 us | 2.812 us | 3.005 us | 0.67x |
| `structural/method-family-generic-call` | 4.335 us | 6.493 us | 17.539 us | 0.67x |
| `literal/identifier-token` | 2.737 us | 4.302 us | 15.277 us | 0.64x |
| `structural/method-family-call` | 4.769 us | 8.179 us | 18.767 us | 0.58x |
| `literal-family/type-token-family` | 11.806 us | 24.138 us | 35.021 us | 0.49x |
| `literal/identifier-token-casei` | 6.991 us | 15.207 us | 26.128 us | 0.46x |
| `lokad/style/range-ref` | 1.163 us | 2.639 us | 2.641 us | 0.44x |
| `structural/ordered-keyword-window` | 6.669 us | 17.512 us | 28.507 us | 0.38x |
| `structural/ordered-modifier-pair` | 5.955 us | 17.157 us | 28.639 us | 0.35x |
| `structural/keyword-family-to-capitalized-identifier` | 72.591 us | 215.849 us | 229.427 us | 0.34x |
| `structural/modifier-family-to-type-window` | 75.106 us | 246.775 us | 259.727 us | 0.30x |
| `lokad/lexer/string` | 28.604 us | 94.089 us | 99.411 us | 0.30x |
| `structural/ordered-identifier-window` | 6.208 us | 28.048 us | 38.750 us | 0.22x |
| `lokad/folding/region-marker` | 6.721 us | 35.470 us | 60.776 us | 0.19x |
| `structural/modifier-family-to-type-pair` | 24.436 us | 129.100 us | 139.883 us | 0.19x |
| `lokad/langserv/url-dashboard` | 2.802 us | 23.572 us | 23.795 us | 0.12x |
| `lokad/langserv/url-download` | 2.800 us | 51.607 us | 51.400 us | 0.05x |
| `balancing/begin-end-blocks` | 7.290 us | 4,277.575 us | 4,294.942 us | 0.0017x |
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
| `structural/keyword-family-to-capitalized-identifier` | 72.620 us | 7.587 us | 19.309 us | 9.57x |
| `lokad/imports/module-imports` | 31.159 us | 4.347 us | 26.109 us | 7.17x |
| `structural/modifier-family-to-type-pair` | 24.476 us | 10.398 us | 21.519 us | 2.35x |
| `lokad/langserv/helper-identifier` | 9.175 us | 4.586 us | 5.566 us | 2.00x |
| `lokad/lexer/doc-line` | 4.208 us | 2.132 us | 2.919 us | 1.97x |
| `lokad/lexer/operator-run` | 3.622 us | 1.942 us | 2.514 us | 1.87x |
| `lokad/lexer/identifier` | 30.302 us | 16.760 us | 19.998 us | 1.81x |
| `lokad/style/hex-color` | 0.897 us | 0.516 us | 0.597 us | 1.74x |
| `lokad/langserv/color-short-hex` | 1.615 us | 0.962 us | 1.125 us | 1.68x |
| `structural/keyword-to-generic-type` | 5.665 us | 3.388 us | 14.663 us | 1.67x |
| `lokad/docs/inline-doc-prefix` | 4.294 us | 2.658 us | 3.425 us | 1.62x |
| `lokad/lexer/number` | 10.449 us | 6.971 us | 8.026 us | 1.50x |
| `structural/ordered-keyword-pair` | 6.086 us | 4.102 us | 14.987 us | 1.48x |
| `fallback/lookahead` | 6.035 us | 4.141 us | 14.985 us | 1.46x |
| `structural/modifier-family-to-type-window` | 75.783 us | 54.952 us | 67.086 us | 1.38x |
| `literal/identifier-token-casei` | 6.962 us | 5.827 us | 16.715 us | 1.19x |
| `lokad/langserv/color-rgb-hex` | 2.013 us | 1.746 us | 1.963 us | 1.15x |
| `lokad/style/cell-ref` | 1.913 us | 1.704 us | 1.898 us | 1.12x |
| `literal-family/type-token-family` | 11.805 us | 11.229 us | 22.071 us | 1.05x |
| `structural/method-family-call` | 4.728 us | 4.955 us | 15.264 us | 0.95x |
| `structural/method-family-generic-call` | 4.267 us | 4.540 us | 14.813 us | 0.94x |
| `literal-family/method-token-family` | 4.660 us | 5.148 us | 15.504 us | 0.91x |
| `lokad/langserv/identifier-validator` | 11.383 us | 13.092 us | 11.095 us | 0.87x |
| `lokad/style/range-ref` | 1.165 us | 1.440 us | 1.513 us | 0.81x |
| `structural/ordered-keyword-window` | 6.716 us | 8.412 us | 19.305 us | 0.80x |
| `literal/call-token` | 3.046 us | 3.845 us | 14.589 us | 0.79x |
| `structural/ordered-modifier-pair` | 5.981 us | 8.496 us | 19.443 us | 0.70x |
| `literal/identifier-token` | 2.716 us | 4.204 us | 14.983 us | 0.65x |
| `lokad/lexer/string` | 28.366 us | 44.558 us | 47.276 us | 0.64x |
| `structural/ordered-identifier-window` | 6.265 us | 10.420 us | 21.042 us | 0.60x |
| `lokad/langserv/url-dashboard` | 2.784 us | 7.276 us | 7.737 us | 0.38x |
| `lokad/langserv/url-download` | 2.785 us | 12.686 us | 13.165 us | 0.22x |
| `lokad/folding/region-marker` | 6.788 us | 35.298 us | 59.045 us | 0.19x |
| `balancing/begin-end-blocks` | 7.291 us | 458.540 us | 482.442 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
