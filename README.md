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
and resource checks passed in **43m24s**. The [run receipt](bench/Experiments/DeliveryDev31-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 89.494 us | 18.824 us | 2,153.979 us | 4.75x |
| `literal-alternate/sherlock-zh` | 149.964 us | 68.562 us | 2,164.626 us | 2.19x |
| `literal/sherlock-ru` | 172.651 us | 104.285 us | 5,395.580 us | 1.66x |
| `literal/sherlock-casei-en` | 158.871 us | 197.511 us | 591.001 us | 0.80x |
| `aws-keys/quick` | 6,560.856 us | 9,941.260 us | 43,497.600 us | 0.66x |
| `literal/sherlock-casei-ru` | 257.501 us | 392.104 us | 5,580.967 us | 0.66x |
| `literal/sherlock-en` | 56.835 us | 86.626 us | 479.658 us | 0.66x |
| `ruff-noqa/tweaked` | 4,382.850 us | 8,101.443 us | 41,642.150 us | 0.54x |
| `bounded-repeat/letters-en` | 1,054.365 us | 2,849.700 us | 2,936.512 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,689.465 us | 4,875.882 us | 10,116.433 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,968.492 us | 5,959.656 us | 6,767.387 us | 0.33x |
| `dictionary/single` | 8,210.943 us | 25,920.850 us | 25,935.200 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 198.043 us | 625.661 us | 1,081.738 us | 0.32x |
| `literal-alternate/sherlock-en` | 396.872 us | 1,496.935 us | 2,019.726 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 154.315 us | 654.310 us | 1,091.014 us | 0.24x |
| `bounded-repeat/capitals` | 9,535.983 us | 46,486.650 us | 51,646.500 us | 0.21x |
| `aws-keys/full` | 10,640.660 us | 86,511.500 us | 116,556.700 us | 0.12x |
| `bounded-repeat/context` | 16,688.233 us | 147,847.100 us | 151,372.900 us | 0.11x |
| `ruff-noqa/real` | 4,523.908 us | 70,226.000 us | 85,651.000 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 870.310 us | 23,151.767 us | 35,072.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 171.235 us | 11,189.520 us | 12,060.480 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,226.888 us | 487.041 us | 8,849.500 us | 2.52x |
| `common/match-word` | `Match` | 0.199 us | 0.164 us | 1.128 us | 1.21x |
| `industry/leipzig-twain-count` | `Count` | 1,162.791 us | 1,328.562 us | 8,287.267 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,454.203 us | 1,953.173 us | 10,522.300 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.112 us | 0.150 us | 0.72x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.101 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.207 us | 0.225 us | 0.57x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.040 us | 266.232 us | 534.032 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.170 us | 0.186 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.176 us | 0.195 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,012.608 us | 9,796.383 us | 17,023.650 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.231 us | 0.265 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.073 us | 0.191 us | 0.212 us | 0.38x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.268 us | 0.297 us | 0.36x |
| `common/replace-words` | `Replace` | 27.754 us | 76.966 us | 78.502 us | 0.36x |
| `common/matches-word` | `Count` | 1.351 us | 4.270 us | 5.872 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.348 us | 0.374 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,347.214 us | 8,563.083 us | 16,995.600 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.108 us | 0.137 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 14.550 us | 79.346 us | 80.941 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.329 us | 972.772 us | 1,272.626 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.570 us | 0.608 us | 0.15x |
| `common/matches-words` | `Count` | 9.395 us | 72.525 us | 73.089 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.611 us | 0.643 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.090 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.838 us | 0.881 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,070.500 us | 19,579.600 us | 20,466.000 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,267.925 us | 32,287.550 us | 46,746.600 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,798.850 us | 22,920.833 us | 30,466.200 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 519.288 us | 8,644.583 us | 9,213.750 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.869 us | 164.588 us | 166.457 us | 0.05x |
| `common/matches-set` | `Count` | 7.792 us | 195.653 us | 197.297 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 264.214 us | 12,233.240 us | 12,731.650 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 149.269 us | 36.539 us | 2,159.571 us | 4.09x |
| `literal/sherlock-zh` | 89.626 us | 23.842 us | 2,148.904 us | 3.76x |
| `literal/sherlock-casei-en` | 158.761 us | 80.644 us | 441.405 us | 1.97x |
| `literal/sherlock-ru` | 172.693 us | 92.024 us | 5,263.540 us | 1.88x |
| `aws-keys/quick` | 6,572.250 us | 3,696.321 us | 35,331.150 us | 1.78x |
| `literal-alternate/sherlock-casei-en` | 170.151 us | 147.295 us | 645.975 us | 1.16x |
| `literal/sherlock-casei-ru` | 255.623 us | 222.687 us | 5,326.840 us | 1.15x |
| `literal-alternate/sherlock-en-mixed` | 198.519 us | 177.680 us | 553.914 us | 1.12x |
| `literal-alternate/sherlock-en` | 411.271 us | 375.214 us | 794.932 us | 1.10x |
| `ruff-noqa/tweaked` | 4,466.000 us | 4,570.618 us | 35,725.900 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,966.865 us | 2,073.040 us | 2,891.200 us | 0.95x |
| `literal/sherlock-en` | 56.983 us | 76.053 us | 438.261 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 153.998 us | 208.870 us | 591.372 us | 0.74x |
| `literal-alternate/sherlock-ru` | 1,651.339 us | 2,350.464 us | 7,460.633 us | 0.70x |
| `bounded-repeat/capitals` | 9,516.583 us | 13,632.200 us | 20,279.033 us | 0.70x |
| `bounded-repeat/letters-en` | 1,055.790 us | 1,843.214 us | 1,932.331 us | 0.57x |
| `aws-keys/full` | 10,641.160 us | 18,721.400 us | 50,139.200 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 874.662 us | 1,978.981 us | 11,787.933 us | 0.44x |
| `bounded-repeat/context` | 16,645.033 us | 69,323.500 us | 72,161.300 us | 0.24x |
| `ruff-noqa/real` | 4,522.400 us | 21,332.133 us | 33,789.200 us | 0.21x |
| `dictionary/single` | 8,243.350 us | 45,973.150 us | 45,975.550 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,223.190 us | 452.194 us | 7,724.700 us | 2.71x |
| `common/match-word` | `Match` | 0.198 us | 0.078 us | 0.994 us | 2.55x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.124 us | 59.196 us | 272.330 us | 2.25x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.043 us | 0.074 us | 2.18x |
| `industry/leipzig-river-window-count` | `Count` | 3,296.000 us | 1,527.609 us | 12,941.000 us | 2.16x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.068 us | 0.087 us | 1.73x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.030 us | 0.049 us | 1.69x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.049 us | 0.087 us | 1.65x |
| `common/replace-words` | `Replace` | 27.839 us | 17.148 us | 19.716 us | 1.62x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.047 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.048 us | 0.069 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,809.386 us | 1,217.254 us | 6,757.967 us | 1.49x |
| `common/ip-miss` | `IsMatch` | 0.073 us | 0.050 us | 0.068 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.055 us | 0.077 us | 1.37x |
| `industry/leipzig-name-family-count` | `Count` | 4,168.150 us | 3,082.153 us | 8,233.533 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,460.491 us | 1,099.059 us | 8,565.667 us | 1.33x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.114 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.117 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.089 us | 0.120 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,166.355 us | 1,293.792 us | 6,895.500 us | 0.90x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.088 us | 0.128 us | 0.90x |
| `common/matches-words` | `Count` | 9.384 us | 11.314 us | 14.453 us | 0.83x |
| `common/split-words` | `Split` | 14.629 us | 21.527 us | 23.269 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,353.291 us | 3,469.160 us | 11,025.350 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-word` | `Count` | 1.348 us | 2.385 us | 3.550 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.203 us | 0.237 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.037 us | 0.053 us | 0.27x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 519.366 us | 3,974.969 us | 4,385.542 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.918 us | 1,495.712 us | 1,845.211 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,070.684 us | 18,550.800 us | 19,364.133 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.846 us | 106.068 us | 107.581 us | 0.07x |
| `common/matches-set` | `Count` | 7.805 us | 106.460 us | 108.647 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 264.009 us | 4,829.073 us | 5,261.420 us | 0.05x |
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
| `lokad/imports/module-imports` | 12.339 us | 6.477 us | 29.023 us | 1.91x |
| `structural/keyword-to-generic-type` | 5.520 us | 4.108 us | 15.670 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.682 us | 1.366 us | 1.458 us | 1.23x |
| `lokad/lexer/doc-line` | 3.349 us | 2.960 us | 3.544 us | 1.13x |
| `lokad/lexer/identifier` | 23.690 us | 22.139 us | 25.236 us | 1.07x |
| `lokad/langserv/helper-identifier` | 7.215 us | 7.166 us | 7.758 us | 1.01x |
| `structural/ordered-keyword-pair` | 5.351 us | 5.420 us | 16.902 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.313 us | 12.113 us | 13.980 us | 0.93x |
| `fallback/lookahead` | 5.485 us | 6.122 us | 17.134 us | 0.90x |
| `lokad/lexer/operator-run` | 2.893 us | 3.316 us | 3.855 us | 0.87x |
| `literal-family/method-token-family` | 7.576 us | 9.212 us | 20.068 us | 0.82x |
| `lokad/style/hex-color` | 0.607 us | 0.780 us | 0.860 us | 0.78x |
| `lokad/docs/inline-doc-prefix` | 3.375 us | 4.812 us | 5.432 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.046 us | 2.927 us | 3.004 us | 0.70x |
| `lokad/lexer/number` | 9.366 us | 14.020 us | 14.837 us | 0.67x |
| `literal/identifier-token` | 2.770 us | 4.365 us | 15.904 us | 0.63x |
| `literal/call-token` | 2.620 us | 4.311 us | 15.430 us | 0.61x |
| `lokad/style/cell-ref` | 1.586 us | 2.832 us | 2.957 us | 0.56x |
| `structural/ordered-identifier-window` | 16.024 us | 28.708 us | 39.459 us | 0.56x |
| `structural/method-family-generic-call` | 3.447 us | 6.743 us | 17.852 us | 0.51x |
| `structural/method-family-call` | 3.875 us | 8.370 us | 19.608 us | 0.46x |
| `literal-family/type-token-family` | 10.587 us | 24.274 us | 35.427 us | 0.44x |
| `literal/identifier-token-casei` | 5.673 us | 15.104 us | 26.881 us | 0.38x |
| `lokad/style/range-ref` | 0.924 us | 2.712 us | 2.767 us | 0.34x |
| `structural/modifier-family-to-type-window` | 82.420 us | 258.414 us | 271.332 us | 0.32x |
| `structural/ordered-keyword-window` | 5.419 us | 17.689 us | 29.315 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.377 us | 17.622 us | 28.499 us | 0.31x |
| `lokad/lexer/string` | 22.215 us | 96.842 us | 103.230 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 23.544 us | 132.581 us | 144.301 us | 0.18x |
| `lokad/folding/region-marker` | 6.418 us | 36.732 us | 60.366 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.520 us | 24.617 us | 24.781 us | 0.10x |
| `lokad/langserv/url-download` | 2.688 us | 50.857 us | 51.306 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 10.505 us | 220.835 us | 234.602 us | 0.05x |
| `balancing/begin-end-blocks` | 5.716 us | 4,451.167 us | 4,508.133 us | 0.0013x |
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
| `lokad/imports/module-imports` | 12.330 us | 4.427 us | 26.111 us | 2.78x |
| `structural/modifier-family-to-type-pair` | 23.617 us | 10.716 us | 21.634 us | 2.20x |
| `lokad/langserv/color-short-hex` | 1.695 us | 1.020 us | 1.205 us | 1.66x |
| `structural/keyword-to-generic-type` | 5.507 us | 3.510 us | 14.621 us | 1.57x |
| `lokad/langserv/helper-identifier` | 7.205 us | 4.621 us | 5.588 us | 1.56x |
| `lokad/lexer/doc-line` | 3.323 us | 2.179 us | 2.840 us | 1.52x |
| `structural/ordered-identifier-window` | 16.107 us | 10.594 us | 21.239 us | 1.52x |
| `structural/modifier-family-to-type-window` | 82.665 us | 57.009 us | 68.950 us | 1.45x |
| `lokad/lexer/operator-run` | 2.899 us | 2.015 us | 2.560 us | 1.44x |
| `lokad/lexer/identifier` | 23.567 us | 16.542 us | 19.912 us | 1.42x |
| `structural/keyword-family-to-capitalized-identifier` | 10.542 us | 7.918 us | 18.956 us | 1.33x |
| `fallback/lookahead` | 5.499 us | 4.245 us | 14.913 us | 1.30x |
| `structural/ordered-keyword-pair` | 5.335 us | 4.142 us | 15.075 us | 1.29x |
| `lokad/lexer/number` | 9.396 us | 7.448 us | 8.648 us | 1.26x |
| `lokad/langserv/identifier-validator` | 11.362 us | 9.011 us | 11.237 us | 1.26x |
| `lokad/docs/inline-doc-prefix` | 3.361 us | 2.691 us | 3.475 us | 1.25x |
| `lokad/style/hex-color` | 0.605 us | 0.506 us | 0.592 us | 1.20x |
| `lokad/langserv/color-rgb-hex` | 2.039 us | 1.843 us | 2.070 us | 1.11x |
| `literal/identifier-token-casei` | 5.724 us | 5.934 us | 17.413 us | 0.96x |
| `literal-family/type-token-family` | 10.553 us | 11.533 us | 22.199 us | 0.92x |
| `lokad/style/cell-ref` | 1.586 us | 1.795 us | 1.973 us | 0.88x |
| `structural/method-family-call` | 3.881 us | 5.058 us | 15.911 us | 0.77x |
| `structural/method-family-generic-call` | 3.432 us | 4.497 us | 15.010 us | 0.76x |
| `literal-family/method-token-family` | 3.886 us | 5.358 us | 15.994 us | 0.73x |
| `literal/call-token` | 2.609 us | 3.969 us | 14.590 us | 0.66x |
| `structural/ordered-keyword-window` | 5.416 us | 8.570 us | 19.945 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.363 us | 8.743 us | 19.598 us | 0.61x |
| `lokad/style/range-ref` | 0.928 us | 1.539 us | 1.540 us | 0.60x |
| `literal/identifier-token` | 2.773 us | 4.694 us | 15.652 us | 0.59x |
| `lokad/lexer/string` | 22.121 us | 45.627 us | 48.148 us | 0.48x |
| `lokad/langserv/url-dashboard` | 2.526 us | 7.471 us | 7.943 us | 0.34x |
| `lokad/langserv/url-download` | 2.696 us | 13.210 us | 13.371 us | 0.20x |
| `lokad/folding/region-marker` | 6.454 us | 36.920 us | 60.372 us | 0.17x |
| `balancing/begin-end-blocks` | 5.725 us | 469.527 us | 495.600 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
