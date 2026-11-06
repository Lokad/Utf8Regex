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
and resource checks passed in **43m37s**. The [run receipt](bench/Experiments/DeliveryDev39-20261106/full-refresh.json)
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
| `literal/sherlock-zh` | 84.766 us | 18.666 us | 2,140.373 us | 4.54x |
| `literal/sherlock-ru` | 166.121 us | 100.044 us | 5,224.280 us | 1.66x |
| `literal-alternate/sherlock-zh` | 105.414 us | 69.425 us | 2,196.146 us | 1.52x |
| `literal/sherlock-casei-en` | 157.925 us | 204.013 us | 613.360 us | 0.77x |
| `literal/sherlock-en` | 57.450 us | 87.106 us | 483.535 us | 0.66x |
| `literal/sherlock-casei-ru` | 254.062 us | 386.346 us | 5,535.167 us | 0.66x |
| `ruff-noqa/tweaked` | 4,319.150 us | 7,769.814 us | 41,319.750 us | 0.56x |
| `aws-keys/quick` | 5,007.430 us | 9,626.033 us | 43,161.800 us | 0.52x |
| `bounded-repeat/letters-en` | 1,055.544 us | 2,837.500 us | 2,942.535 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,671.345 us | 4,730.718 us | 10,112.733 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,952.008 us | 5,918.533 us | 6,695.913 us | 0.33x |
| `dictionary/single` | 8,312.586 us | 25,730.000 us | 25,832.450 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 199.434 us | 621.837 us | 1,041.757 us | 0.32x |
| `literal-alternate/sherlock-en` | 393.138 us | 1,503.512 us | 2,015.389 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 153.093 us | 653.999 us | 1,066.368 us | 0.23x |
| `bounded-repeat/capitals` | 9,436.717 us | 46,974.900 us | 52,559.400 us | 0.20x |
| `bounded-repeat/context` | 24,088.833 us | 147,193.300 us | 149,821.400 us | 0.16x |
| `aws-keys/full` | 10,672.617 us | 86,330.100 us | 116,852.800 us | 0.12x |
| `ruff-noqa/real` | 4,429.675 us | 70,866.600 us | 101,056.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 971.064 us | 23,195.133 us | 28,445.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 125.992 us | 11,764.840 us | 12,616.675 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,177.102 us | 484.572 us | 8,827.300 us | 2.43x |
| `common/match-word` | `Match` | 0.200 us | 0.162 us | 1.113 us | 1.23x |
| `industry/leipzig-twain-count` | `Count` | 1,160.833 us | 1,323.105 us | 8,306.550 us | 0.88x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.063 us | 0.084 us | 0.099 us | 0.74x |
| `industry/mariomka-uri-count` | `Count` | 1,444.589 us | 1,955.452 us | 10,498.933 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.111 us | 0.147 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.126 us | 0.209 us | 0.228 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.018 us | 269.285 us | 508.853 us | 0.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.162 us | 0.180 us | 0.49x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.172 us | 0.193 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.187 us | 0.205 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 3,988.817 us | 9,810.500 us | 17,061.850 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.235 us | 0.270 us | 0.39x |
| `common/replace-words` | `Replace` | 28.060 us | 76.748 us | 79.191 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.269 us | 0.296 us | 0.36x |
| `common/matches-word` | `Count` | 1.348 us | 4.241 us | 6.007 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.102 us | 0.348 us | 0.370 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,365.250 us | 8,590.200 us | 17,036.250 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.267 us | 961.838 us | 1,249.353 us | 0.19x |
| `common/split-words` | `Split` | 14.504 us | 78.019 us | 79.444 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.558 us | 0.591 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.613 us | 0.647 us | 0.13x |
| `common/matches-words` | `Count` | 9.288 us | 71.757 us | 72.982 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.090 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.836 us | 0.877 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,995.865 us | 19,440.000 us | 20,331.633 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,310.006 us | 32,465.050 us | 46,891.200 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,806.803 us | 23,530.033 us | 30,539.850 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 493.846 us | 8,689.617 us | 9,064.700 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.982 us | 163.537 us | 165.749 us | 0.05x |
| `common/matches-set` | `Count` | 7.872 us | 196.056 us | 197.271 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 257.412 us | 11,853.980 us | 12,307.820 us | 0.02x |
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
| `literal/sherlock-zh` | 85.681 us | 23.808 us | 2,181.322 us | 3.60x |
| `literal-alternate/sherlock-zh` | 105.017 us | 36.675 us | 2,176.457 us | 2.86x |
| `literal/sherlock-casei-en` | 157.580 us | 79.776 us | 457.623 us | 1.98x |
| `literal/sherlock-ru` | 167.828 us | 89.649 us | 5,251.760 us | 1.87x |
| `aws-keys/quick` | 5,054.980 us | 3,679.050 us | 34,763.500 us | 1.37x |
| `literal/sherlock-casei-ru` | 252.447 us | 197.169 us | 5,268.370 us | 1.28x |
| `literal-alternate/sherlock-en` | 404.571 us | 366.648 us | 799.859 us | 1.10x |
| `literal-alternate/sherlock-en-mixed` | 197.519 us | 185.512 us | 587.436 us | 1.06x |
| `ruff-noqa/tweaked` | 4,335.758 us | 4,541.167 us | 35,785.800 us | 0.95x |
| `bounded-repeat/letters-ru` | 1,938.330 us | 2,042.136 us | 2,840.700 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 126.604 us | 133.907 us | 592.705 us | 0.95x |
| `literal/sherlock-en` | 57.296 us | 76.356 us | 408.516 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 153.221 us | 205.463 us | 583.049 us | 0.75x |
| `bounded-repeat/capitals` | 9,439.867 us | 13,625.225 us | 20,069.567 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,623.175 us | 2,346.482 us | 7,467.133 us | 0.69x |
| `bounded-repeat/letters-en` | 1,050.439 us | 1,825.382 us | 1,913.493 us | 0.58x |
| `aws-keys/full` | 10,748.217 us | 18,801.900 us | 49,992.000 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 967.547 us | 1,970.773 us | 7,261.233 us | 0.49x |
| `bounded-repeat/context` | 24,171.950 us | 69,237.600 us | 71,491.200 us | 0.35x |
| `ruff-noqa/real` | 4,434.717 us | 21,262.467 us | 53,445.450 us | 0.21x |
| `dictionary/single` | 8,285.557 us | 45,753.850 us | 45,941.000 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.197 us | 0.075 us | 0.987 us | 2.62x |
| `industry/mariomka-email-count` | `Count` | 1,183.339 us | 454.740 us | 7,736.667 us | 2.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.269 us | 60.147 us | 262.738 us | 2.22x |
| `industry/leipzig-river-window-count` | `Count` | 3,309.275 us | 1,506.729 us | 13,596.300 us | 2.20x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.043 us | 0.074 us | 2.15x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.065 us | 0.032 us | 0.048 us | 2.06x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.067 us | 0.088 us | 1.81x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.088 us | 1.65x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.030 us | 0.048 us | 1.63x |
| `common/replace-words` | `Replace` | 27.940 us | 17.132 us | 19.995 us | 1.63x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.047 us | 0.069 us | 1.57x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.050 us | 0.070 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,808.950 us | 1,211.293 us | 6,743.400 us | 1.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.057 us | 0.079 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,132.108 us | 3,060.206 us | 8,430.600 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,446.435 us | 1,108.720 us | 8,560.767 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.092 us | 0.117 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.088 us | 0.120 us | 1.06x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.088 us | 0.126 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 1,165.866 us | 1,285.840 us | 7,042.643 us | 0.91x |
| `common/split-words` | `Split` | 14.498 us | 21.290 us | 23.161 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,376.832 us | 3,514.587 us | 11,107.200 us | 0.68x |
| `common/matches-words` | `Count` | 9.335 us | 14.136 us | 16.463 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.66x |
| `common/matches-word` | `Count` | 1.339 us | 2.385 us | 3.662 us | 0.56x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.203 us | 0.236 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.051 us | 0.28x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.347 us | 3,995.554 us | 4,406.150 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.178 us | 1,486.444 us | 1,800.936 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,999.819 us | 17,983.550 us | 18,817.600 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.952 us | 105.009 us | 106.647 us | 0.08x |
| `common/matches-set` | `Count` | 7.999 us | 106.604 us | 108.046 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 260.663 us | 4,783.091 us | 5,254.230 us | 0.05x |
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
| `lokad/imports/module-imports` | 11.649 us | 6.403 us | 29.083 us | 1.82x |
| `structural/keyword-to-generic-type` | 5.489 us | 4.084 us | 15.755 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.694 us | 1.325 us | 1.458 us | 1.28x |
| `lokad/lexer/doc-line` | 3.358 us | 3.009 us | 3.622 us | 1.12x |
| `lokad/lexer/identifier` | 24.551 us | 22.800 us | 25.821 us | 1.08x |
| `structural/ordered-keyword-pair` | 5.567 us | 5.367 us | 17.012 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.199 us | 7.042 us | 7.712 us | 1.02x |
| `lokad/style/hex-color` | 0.735 us | 0.789 us | 0.840 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.360 us | 12.515 us | 13.889 us | 0.91x |
| `fallback/lookahead` | 5.406 us | 6.138 us | 17.355 us | 0.88x |
| `lokad/lexer/operator-run` | 2.855 us | 3.302 us | 3.771 us | 0.86x |
| `literal-family/method-token-family` | 7.177 us | 9.316 us | 19.913 us | 0.77x |
| `lokad/docs/inline-doc-prefix` | 3.404 us | 4.801 us | 5.478 us | 0.71x |
| `lokad/langserv/color-rgb-hex` | 2.025 us | 2.868 us | 2.954 us | 0.71x |
| `literal/identifier-token` | 2.769 us | 4.349 us | 15.916 us | 0.64x |
| `literal/call-token` | 2.633 us | 4.322 us | 15.800 us | 0.61x |
| `lokad/lexer/number` | 8.304 us | 14.237 us | 15.106 us | 0.58x |
| `structural/ordered-identifier-window` | 16.228 us | 28.858 us | 39.622 us | 0.56x |
| `structural/method-family-generic-call` | 3.433 us | 6.590 us | 17.843 us | 0.52x |
| `lokad/style/cell-ref` | 1.478 us | 2.889 us | 2.990 us | 0.51x |
| `structural/method-family-call` | 3.857 us | 8.085 us | 18.951 us | 0.48x |
| `literal-family/type-token-family` | 10.538 us | 23.518 us | 34.840 us | 0.45x |
| `literal/identifier-token-casei` | 5.756 us | 15.212 us | 26.926 us | 0.38x |
| `lokad/style/range-ref` | 0.911 us | 2.697 us | 2.728 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.500 us | 17.190 us | 31.837 us | 0.32x |
| `structural/modifier-family-to-type-window` | 79.428 us | 257.638 us | 270.630 us | 0.31x |
| `structural/ordered-keyword-window` | 5.439 us | 17.711 us | 28.717 us | 0.31x |
| `lokad/lexer/string` | 21.263 us | 92.012 us | 97.134 us | 0.23x |
| `lokad/folding/region-marker` | 6.454 us | 36.871 us | 61.430 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.508 us | 24.503 us | 24.671 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.428 us | 218.411 us | 231.424 us | 0.05x |
| `lokad/langserv/url-download` | 2.641 us | 50.559 us | 50.607 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.986 us | 134.568 us | 145.783 us | 0.04x |
| `balancing/begin-end-blocks` | 5.762 us | 4,200.875 us | 4,249.808 us | 0.0014x |
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
| `lokad/imports/module-imports` | 11.743 us | 4.422 us | 26.732 us | 2.66x |
| `lokad/langserv/color-short-hex` | 1.680 us | 1.005 us | 1.161 us | 1.67x |
| `structural/keyword-to-generic-type` | 5.502 us | 3.519 us | 14.648 us | 1.56x |
| `lokad/langserv/helper-identifier` | 7.237 us | 4.663 us | 5.633 us | 1.55x |
| `structural/ordered-identifier-window` | 16.137 us | 10.669 us | 21.381 us | 1.51x |
| `lokad/lexer/doc-line` | 3.326 us | 2.232 us | 2.926 us | 1.49x |
| `lokad/lexer/operator-run` | 2.855 us | 1.940 us | 2.492 us | 1.47x |
| `lokad/lexer/identifier` | 24.328 us | 16.703 us | 20.714 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.404 us | 7.942 us | 18.549 us | 1.44x |
| `structural/modifier-family-to-type-window` | 79.360 us | 56.137 us | 69.318 us | 1.41x |
| `lokad/style/hex-color` | 0.735 us | 0.521 us | 0.601 us | 1.41x |
| `structural/ordered-keyword-pair` | 5.549 us | 4.157 us | 15.212 us | 1.33x |
| `fallback/lookahead` | 5.454 us | 4.206 us | 15.125 us | 1.30x |
| `lokad/langserv/identifier-validator` | 11.302 us | 9.206 us | 11.038 us | 1.23x |
| `lokad/docs/inline-doc-prefix` | 3.392 us | 2.848 us | 3.617 us | 1.19x |
| `lokad/langserv/color-rgb-hex` | 2.032 us | 1.775 us | 2.000 us | 1.15x |
| `lokad/lexer/number` | 8.319 us | 7.645 us | 9.021 us | 1.09x |
| `literal/identifier-token-casei` | 5.734 us | 5.905 us | 17.145 us | 0.97x |
| `literal-family/type-token-family` | 10.544 us | 11.473 us | 22.475 us | 0.92x |
| `lokad/style/cell-ref` | 1.483 us | 1.746 us | 1.884 us | 0.85x |
| `structural/method-family-generic-call` | 3.436 us | 4.506 us | 15.077 us | 0.76x |
| `structural/method-family-call` | 3.844 us | 5.073 us | 15.442 us | 0.76x |
| `literal-family/method-token-family` | 3.901 us | 5.308 us | 15.932 us | 0.73x |
| `literal/call-token` | 2.642 us | 4.015 us | 14.869 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.534 us | 8.626 us | 19.554 us | 0.64x |
| `structural/ordered-keyword-window` | 5.416 us | 8.631 us | 19.321 us | 0.63x |
| `lokad/style/range-ref` | 0.903 us | 1.498 us | 1.532 us | 0.60x |
| `literal/identifier-token` | 2.779 us | 4.671 us | 15.552 us | 0.59x |
| `lokad/lexer/string` | 21.246 us | 44.740 us | 47.673 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.977 us | 10.791 us | 21.708 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.529 us | 7.451 us | 7.927 us | 0.34x |
| `lokad/langserv/url-download` | 2.651 us | 12.882 us | 13.211 us | 0.21x |
| `lokad/folding/region-marker` | 6.468 us | 36.124 us | 60.229 us | 0.18x |
| `balancing/begin-end-blocks` | 5.741 us | 467.652 us | 494.702 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
