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
and resource checks passed in **43m37s**. The [run receipt](bench/Experiments/DeliveryDev33-20261105/full-refresh.json)
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
| `literal/sherlock-zh` | 89.299 us | 19.049 us | 2,190.883 us | 4.69x |
| `literal-alternate/sherlock-zh` | 145.097 us | 71.090 us | 2,194.158 us | 2.04x |
| `literal/sherlock-ru` | 175.335 us | 109.434 us | 5,318.770 us | 1.60x |
| `aws-keys/quick` | 9,763.589 us | 10,050.420 us | 43,706.300 us | 0.97x |
| `literal/sherlock-casei-en` | 161.393 us | 199.548 us | 593.166 us | 0.81x |
| `literal/sherlock-en` | 57.681 us | 87.334 us | 462.650 us | 0.66x |
| `literal/sherlock-casei-ru` | 258.077 us | 392.743 us | 5,598.167 us | 0.66x |
| `ruff-noqa/tweaked` | 4,390.900 us | 7,995.000 us | 41,885.600 us | 0.55x |
| `bounded-repeat/letters-en` | 1,037.304 us | 2,833.367 us | 2,936.372 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,683.680 us | 4,904.745 us | 10,148.533 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 200.066 us | 604.182 us | 1,050.213 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,944.819 us | 6,002.178 us | 6,801.675 us | 0.32x |
| `dictionary/single` | 8,407.950 us | 25,971.750 us | 25,993.500 us | 0.32x |
| `literal-alternate/sherlock-en-nomatch` | 174.626 us | 673.057 us | 1,109.978 us | 0.26x |
| `literal-alternate/sherlock-en` | 384.399 us | 1,544.367 us | 2,081.262 us | 0.25x |
| `bounded-repeat/capitals` | 9,569.833 us | 47,523.550 us | 55,509.100 us | 0.20x |
| `aws-keys/full` | 8,882.980 us | 87,369.700 us | 121,636.700 us | 0.10x |
| `bounded-repeat/context` | 14,079.100 us | 149,046.900 us | 152,577.000 us | 0.09x |
| `ruff-noqa/real` | 4,530.150 us | 70,707.000 us | 87,983.400 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 875.890 us | 23,331.767 us | 28,637.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 169.338 us | 11,780.160 us | 12,559.950 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,202.945 us | 493.448 us | 9,015.733 us | 2.44x |
| `common/match-word` | `Match` | 0.200 us | 0.167 us | 1.220 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,163.433 us | 1,364.515 us | 8,910.900 us | 0.85x |
| `industry/mariomka-uri-count` | `Count` | 1,446.760 us | 1,928.389 us | 10,486.733 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.111 us | 0.148 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.195 us | 0.212 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.086 us | 0.103 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 137.481 us | 267.114 us | 503.918 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.164 us | 0.181 us | 0.49x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.175 us | 0.196 us | 0.43x |
| `common/uri-match` | `IsMatch` | 0.099 us | 0.234 us | 0.269 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,103.642 us | 9,945.033 us | 17,740.150 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.189 us | 0.208 us | 0.40x |
| `common/replace-words` | `Replace` | 28.565 us | 80.037 us | 82.201 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.273 us | 0.299 us | 0.35x |
| `common/matches-word` | `Count` | 1.354 us | 4.265 us | 6.015 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.345 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,362.118 us | 8,384.457 us | 16,934.650 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.141 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `common/split-words` | `Split` | 14.730 us | 79.276 us | 80.478 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.897 us | 968.757 us | 1,280.374 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.561 us | 0.598 us | 0.17x |
| `common/matches-words` | `Count` | 9.383 us | 70.990 us | 72.280 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.622 us | 0.656 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.103 us | 0.836 us | 0.879 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,077.344 us | 19,896.500 us | 20,432.300 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,335.619 us | 33,443.050 us | 46,104.900 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,891.722 us | 23,557.067 us | 31,438.650 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 509.119 us | 8,591.250 us | 9,137.167 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.886 us | 171.365 us | 173.031 us | 0.05x |
| `common/matches-set` | `Count` | 8.206 us | 196.309 us | 198.266 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 246.131 us | 11,945.120 us | 12,492.920 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 145.204 us | 36.506 us | 2,171.217 us | 3.98x |
| `literal/sherlock-zh` | 89.399 us | 24.173 us | 2,177.721 us | 3.70x |
| `aws-keys/quick` | 9,701.312 us | 3,617.907 us | 36,393.300 us | 2.68x |
| `literal/sherlock-casei-en` | 161.232 us | 77.751 us | 425.463 us | 2.07x |
| `literal/sherlock-ru` | 177.041 us | 92.730 us | 5,288.810 us | 1.91x |
| `literal/sherlock-casei-ru` | 258.846 us | 226.532 us | 5,349.800 us | 1.14x |
| `literal-alternate/sherlock-casei-en` | 169.162 us | 148.840 us | 594.172 us | 1.14x |
| `literal-alternate/sherlock-en-mixed` | 202.177 us | 187.464 us | 549.474 us | 1.08x |
| `literal-alternate/sherlock-en` | 396.633 us | 374.783 us | 808.667 us | 1.06x |
| `ruff-noqa/tweaked` | 4,386.358 us | 4,579.427 us | 37,475.450 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,944.319 us | 2,086.567 us | 2,881.811 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 174.954 us | 202.268 us | 641.000 us | 0.86x |
| `literal/sherlock-en` | 57.714 us | 76.681 us | 406.807 us | 0.75x |
| `bounded-repeat/capitals` | 9,596.017 us | 13,715.200 us | 21,866.400 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,633.542 us | 2,365.745 us | 7,542.450 us | 0.69x |
| `bounded-repeat/letters-en` | 1,038.467 us | 1,854.289 us | 1,935.662 us | 0.56x |
| `aws-keys/full` | 9,654.280 us | 18,976.933 us | 52,916.100 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 876.881 us | 1,984.696 us | 7,134.667 us | 0.44x |
| `ruff-noqa/real` | 4,550.073 us | 21,614.833 us | 35,967.550 us | 0.21x |
| `bounded-repeat/context` | 14,234.675 us | 69,873.300 us | 73,038.200 us | 0.20x |
| `dictionary/single` | 8,403.433 us | 46,301.850 us | 46,341.300 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,203.555 us | 455.197 us | 7,818.833 us | 2.64x |
| `common/match-word` | `Match` | 0.208 us | 0.081 us | 1.024 us | 2.57x |
| `common/backtracking` | `IsMatch` | 0.103 us | 0.044 us | 0.074 us | 2.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 136.432 us | 61.956 us | 263.158 us | 2.20x |
| `industry/leipzig-river-window-count` | `Count` | 3,344.094 us | 1,582.521 us | 13,987.400 us | 2.11x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.094 us | 0.049 us | 0.088 us | 1.93x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.052 us | 0.030 us | 0.048 us | 1.76x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.069 us | 0.086 us | 1.72x |
| `common/replace-words` | `Replace` | 28.535 us | 17.551 us | 20.969 us | 1.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.032 us | 0.051 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.048 us | 0.069 us | 1.57x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.050 us | 0.070 us | 1.52x |
| `industry/leipzig-symbol-count` | `Count` | 1,896.319 us | 1,292.377 us | 7,596.833 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.056 us | 0.077 us | 1.39x |
| `industry/leipzig-name-family-count` | `Count` | 4,234.142 us | 3,111.847 us | 8,828.467 us | 1.36x |
| `industry/mariomka-uri-count` | `Count` | 1,445.349 us | 1,103.878 us | 8,530.133 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.109 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.090 us | 0.116 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.099 us | 0.089 us | 0.121 us | 1.11x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.089 us | 0.128 us | 0.90x |
| `industry/leipzig-twain-count` | `Count` | 1,165.789 us | 1,294.295 us | 5,855.767 us | 0.90x |
| `common/split-words` | `Split` | 14.653 us | 21.309 us | 23.143 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,365.414 us | 3,509.867 us | 11,035.500 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `common/matches-words` | `Count` | 9.378 us | 16.102 us | 18.501 us | 0.58x |
| `common/matches-word` | `Count` | 1.353 us | 2.376 us | 3.798 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.204 us | 0.237 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 508.086 us | 3,993.646 us | 4,402.050 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.245 us | 1,481.212 us | 1,806.210 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,078.696 us | 18,201.400 us | 19,027.650 us | 0.11x |
| `common/matches-set` | `Count` | 8.255 us | 106.522 us | 108.373 us | 0.08x |
| `common/matches-boundary` | `Count` | 7.915 us | 106.113 us | 107.476 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 236.128 us | 4,843.636 us | 5,312.990 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.799 us | 6.392 us | 28.718 us | 1.85x |
| `structural/keyword-to-generic-type` | 5.494 us | 4.178 us | 15.840 us | 1.32x |
| `lokad/langserv/color-short-hex` | 1.680 us | 1.368 us | 1.596 us | 1.23x |
| `lokad/lexer/doc-line` | 3.350 us | 3.052 us | 3.989 us | 1.10x |
| `lokad/lexer/identifier` | 24.089 us | 23.338 us | 26.502 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.375 us | 5.347 us | 16.882 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.218 us | 7.674 us | 8.451 us | 0.94x |
| `lokad/langserv/identifier-validator` | 11.384 us | 12.626 us | 14.355 us | 0.90x |
| `fallback/lookahead` | 5.433 us | 6.131 us | 17.152 us | 0.89x |
| `lokad/lexer/operator-run` | 2.934 us | 3.464 us | 4.033 us | 0.85x |
| `literal-family/method-token-family` | 7.182 us | 9.271 us | 20.074 us | 0.77x |
| `lokad/style/hex-color` | 0.611 us | 0.817 us | 0.884 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 2.112 us | 3.006 us | 3.225 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.416 us | 5.101 us | 5.914 us | 0.67x |
| `literal/identifier-token` | 2.804 us | 4.403 us | 16.087 us | 0.64x |
| `literal/call-token` | 2.606 us | 4.226 us | 15.585 us | 0.62x |
| `lokad/lexer/number` | 8.274 us | 14.279 us | 15.371 us | 0.58x |
| `structural/ordered-identifier-window` | 16.186 us | 29.168 us | 40.253 us | 0.55x |
| `lokad/style/cell-ref` | 1.498 us | 2.900 us | 3.126 us | 0.52x |
| `structural/method-family-generic-call` | 3.491 us | 6.864 us | 18.043 us | 0.51x |
| `structural/method-family-call` | 3.880 us | 8.184 us | 19.625 us | 0.47x |
| `literal-family/type-token-family` | 10.577 us | 24.473 us | 35.954 us | 0.43x |
| `literal/identifier-token-casei` | 5.903 us | 15.906 us | 26.632 us | 0.37x |
| `lokad/style/range-ref` | 0.913 us | 2.790 us | 2.920 us | 0.33x |
| `structural/modifier-family-to-type-window` | 81.090 us | 259.893 us | 272.660 us | 0.31x |
| `structural/ordered-keyword-window` | 5.553 us | 17.810 us | 29.229 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.411 us | 18.022 us | 28.986 us | 0.30x |
| `lokad/lexer/string` | 23.924 us | 96.372 us | 101.388 us | 0.25x |
| `lokad/folding/region-marker` | 6.403 us | 37.280 us | 62.602 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.483 us | 24.786 us | 25.293 us | 0.10x |
| `lokad/langserv/url-download` | 2.648 us | 50.958 us | 51.294 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 10.742 us | 225.618 us | 239.213 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.041 us | 136.211 us | 148.264 us | 0.04x |
| `balancing/begin-end-blocks` | 5.748 us | 4,382.358 us | 4,359.458 us | 0.0013x |
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
| `lokad/imports/module-imports` | 11.807 us | 4.463 us | 26.083 us | 2.65x |
| `structural/keyword-to-generic-type` | 5.505 us | 3.489 us | 14.700 us | 1.58x |
| `lokad/langserv/color-short-hex` | 1.686 us | 1.073 us | 1.296 us | 1.57x |
| `structural/ordered-identifier-window` | 16.182 us | 10.851 us | 21.706 us | 1.49x |
| `structural/modifier-family-to-type-window` | 81.198 us | 56.346 us | 68.992 us | 1.44x |
| `lokad/lexer/doc-line` | 3.347 us | 2.365 us | 3.255 us | 1.42x |
| `lokad/lexer/operator-run` | 2.925 us | 2.129 us | 2.790 us | 1.37x |
| `structural/keyword-family-to-capitalized-identifier` | 10.732 us | 7.929 us | 19.084 us | 1.35x |
| `lokad/lexer/identifier` | 23.975 us | 17.962 us | 21.492 us | 1.33x |
| `structural/ordered-keyword-pair` | 5.416 us | 4.176 us | 15.004 us | 1.30x |
| `fallback/lookahead` | 5.458 us | 4.243 us | 14.921 us | 1.29x |
| `lokad/langserv/helper-identifier` | 7.224 us | 5.673 us | 6.401 us | 1.27x |
| `lokad/langserv/identifier-validator` | 11.367 us | 9.636 us | 11.640 us | 1.18x |
| `lokad/docs/inline-doc-prefix` | 3.418 us | 3.014 us | 3.880 us | 1.13x |
| `lokad/style/hex-color` | 0.611 us | 0.547 us | 0.635 us | 1.12x |
| `lokad/lexer/number` | 8.267 us | 7.718 us | 8.864 us | 1.07x |
| `lokad/langserv/color-rgb-hex` | 2.062 us | 1.981 us | 2.232 us | 1.04x |
| `literal/identifier-token-casei` | 5.920 us | 5.964 us | 17.248 us | 0.99x |
| `literal-family/type-token-family` | 10.593 us | 11.580 us | 22.483 us | 0.91x |
| `lokad/style/cell-ref` | 1.497 us | 1.939 us | 2.160 us | 0.77x |
| `structural/method-family-generic-call` | 3.487 us | 4.528 us | 15.076 us | 0.77x |
| `structural/method-family-call` | 3.889 us | 5.108 us | 16.158 us | 0.76x |
| `literal-family/method-token-family` | 3.918 us | 5.370 us | 15.805 us | 0.73x |
| `literal/call-token` | 2.613 us | 3.996 us | 14.626 us | 0.65x |
| `structural/ordered-keyword-window` | 5.548 us | 8.649 us | 19.716 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.422 us | 8.684 us | 19.557 us | 0.62x |
| `literal/identifier-token` | 2.808 us | 4.750 us | 15.760 us | 0.59x |
| `lokad/style/range-ref` | 0.913 us | 1.651 us | 1.744 us | 0.55x |
| `lokad/lexer/string` | 23.956 us | 47.061 us | 50.933 us | 0.51x |
| `structural/modifier-family-to-type-pair` | 5.043 us | 10.813 us | 22.000 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.474 us | 7.563 us | 8.114 us | 0.33x |
| `lokad/langserv/url-download` | 2.638 us | 13.137 us | 13.649 us | 0.20x |
| `lokad/folding/region-marker` | 6.438 us | 37.246 us | 59.745 us | 0.17x |
| `balancing/begin-end-blocks` | 5.741 us | 471.299 us | 498.615 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
