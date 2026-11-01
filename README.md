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
| `literal/sherlock-zh` | 130.960 us | 18.459 us | 2,168.104 us | 7.09x |
| `literal-alternate/sherlock-zh` | 229.877 us | 67.258 us | 2,136.721 us | 3.42x |
| `bounded-repeat/capitals` | 129,793.400 us | 45,321.900 us | 52,681.600 us | 2.86x |
| `literal/sherlock-ru` | 244.824 us | 97.048 us | 5,384.550 us | 2.52x |
| `ruff-noqa/tweaked` | 10,385.140 us | 7,860.271 us | 42,036.450 us | 1.32x |
| `literal/sherlock-casei-en` | 167.015 us | 190.887 us | 617.114 us | 0.87x |
| `literal/sherlock-en` | 69.126 us | 84.225 us | 454.163 us | 0.82x |
| `literal/sherlock-casei-ru` | 247.903 us | 360.213 us | 5,534.578 us | 0.69x |
| `aws-keys/quick` | 5,171.930 us | 9,994.580 us | 44,219.400 us | 0.52x |
| `bounded-repeat/letters-en` | 1,012.949 us | 2,734.874 us | 2,873.447 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,635.610 us | 4,697.409 us | 9,941.800 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 195.552 us | 584.956 us | 1,012.326 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,951.269 us | 5,882.178 us | 6,769.525 us | 0.33x |
| `dictionary/single` | 8,201.557 us | 25,909.950 us | 25,833.550 us | 0.32x |
| `literal-alternate/sherlock-en` | 391.874 us | 1,486.700 us | 1,963.356 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 161.726 us | 630.003 us | 1,034.192 us | 0.26x |
| `bounded-repeat/context` | 20,119.767 us | 143,282.600 us | 146,670.700 us | 0.14x |
| `aws-keys/full` | 8,616.500 us | 82,883.900 us | 116,187.800 us | 0.10x |
| `ruff-noqa/real` | 4,450.717 us | 67,836.600 us | 96,887.900 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,068.723 us | 22,454.167 us | 27,898.000 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 210.469 us | 11,195.060 us | 11,895.320 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,499.647 us | 503.041 us | 8,760.433 us | 2.98x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.179 us | 0.108 us | 0.141 us | 1.67x |
| `common/match-word` | `Match` | 0.199 us | 0.159 us | 1.094 us | 1.25x |
| `common/uri-miss` | `IsMatch` | 0.178 us | 0.190 us | 0.208 us | 0.94x |
| `industry/leipzig-twain-count` | `Count` | 1,226.546 us | 1,314.412 us | 8,670.267 us | 0.93x |
| `industry/mariomka-uri-count` | `Count` | 1,711.970 us | 1,868.922 us | 10,299.700 us | 0.92x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 233.119 us | 261.546 us | 496.475 us | 0.89x |
| `common/replace-words` | `Replace` | 65.240 us | 74.069 us | 75.764 us | 0.88x |
| `common/uri-match` | `IsMatch` | 0.171 us | 0.229 us | 0.262 us | 0.75x |
| `common/ip-match` | `IsMatch` | 0.117 us | 0.171 us | 0.190 us | 0.68x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.080 us | 0.097 us | 0.65x |
| `common/email-match` | `IsMatch` | 0.164 us | 0.263 us | 0.283 us | 0.62x |
| `common/ip-miss` | `IsMatch` | 0.112 us | 0.182 us | 0.202 us | 0.61x |
| `common/email-miss` | `IsMatch` | 0.173 us | 0.335 us | 0.358 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.163 us | 0.181 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 4,118.400 us | 9,560.817 us | 16,623.450 us | 0.43x |
| `common/one-node-backtracking` | `IsMatch` | 0.223 us | 0.608 us | 0.638 us | 0.37x |
| `common/matches-word` | `Count` | 1.405 us | 4.159 us | 5.838 us | 0.34x |
| `industry/mariomka-ip-count` | `Count` | 2,688.705 us | 8,129.443 us | 16,879.650 us | 0.33x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.108 us | 0.134 us | 0.25x |
| `common/backtracking` | `IsMatch` | 0.185 us | 0.805 us | 0.849 us | 0.23x |
| `common/split-words` | `Split` | 15.779 us | 80.147 us | 81.666 us | 0.20x |
| `common/date-miss` | `IsMatch` | 0.106 us | 0.550 us | 0.576 us | 0.19x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.015 us | 0.081 us | 0.095 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.048 us | 943.935 us | 1,219.825 us | 0.19x |
| `industry/leipzig-river-window-count` | `Count` | 5,646.830 us | 31,327.450 us | 44,407.500 us | 0.18x |
| `common/matches-words` | `Count` | 9.192 us | 68.313 us | 69.421 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.074 us | 0.090 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,909.319 us | 19,281.567 us | 20,035.233 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,775.379 us | 22,131.933 us | 28,755.750 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 489.694 us | 8,235.257 us | 8,845.667 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.577 us | 160.409 us | 162.775 us | 0.05x |
| `common/matches-set` | `Count` | 7.614 us | 192.089 us | 195.258 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 250.783 us | 11,661.720 us | 12,220.520 us | 0.02x |
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
| `bounded-repeat/capitals` | 130,475.000 us | 13,171.275 us | 20,050.433 us | 9.91x |
| `literal-alternate/sherlock-zh` | 243.271 us | 34.806 us | 2,104.936 us | 6.99x |
| `literal/sherlock-zh` | 131.399 us | 23.615 us | 2,149.808 us | 5.56x |
| `literal/sherlock-ru` | 243.397 us | 88.453 us | 5,228.020 us | 2.75x |
| `ruff-noqa/tweaked` | 10,902.620 us | 4,581.742 us | 36,913.650 us | 2.38x |
| `literal/sherlock-casei-en` | 166.271 us | 79.195 us | 459.205 us | 2.10x |
| `literal-alternate/sherlock-casei-en` | 212.615 us | 125.206 us | 545.453 us | 1.70x |
| `aws-keys/quick` | 5,179.020 us | 3,628.621 us | 35,402.400 us | 1.43x |
| `literal/sherlock-casei-ru` | 247.237 us | 216.237 us | 5,206.040 us | 1.14x |
| `literal-alternate/sherlock-en` | 408.793 us | 362.342 us | 776.828 us | 1.13x |
| `literal-alternate/sherlock-en-mixed` | 196.487 us | 174.516 us | 565.280 us | 1.13x |
| `bounded-repeat/letters-ru` | 1,947.577 us | 2,039.532 us | 2,875.517 us | 0.95x |
| `literal/sherlock-en` | 68.808 us | 74.382 us | 438.464 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 162.484 us | 196.932 us | 564.474 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,691.738 us | 2,281.726 us | 7,363.833 us | 0.74x |
| `bounded-repeat/letters-en` | 1,021.698 us | 1,784.172 us | 1,886.778 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 1,062.581 us | 1,938.374 us | 7,231.933 us | 0.55x |
| `aws-keys/full` | 9,554.140 us | 18,414.133 us | 49,514.700 us | 0.52x |
| `bounded-repeat/context` | 20,270.467 us | 68,391.100 us | 70,003.000 us | 0.30x |
| `ruff-noqa/real` | 4,459.350 us | 20,774.133 us | 50,397.750 us | 0.21x |
| `dictionary/single` | 8,119.629 us | 44,957.400 us | 44,854.800 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/backtracking` | `IsMatch` | 0.184 us | 0.042 us | 0.072 us | 4.37x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 233.318 us | 57.084 us | 262.429 us | 4.09x |
| `industry/leipzig-river-window-count` | `Count` | 5,678.556 us | 1,502.900 us | 14,907.933 us | 3.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.177 us | 0.048 us | 0.083 us | 3.71x |
| `common/replace-words` | `Replace` | 65.146 us | 18.184 us | 19.470 us | 3.58x |
| `industry/mariomka-email-count` | `Count` | 1,502.179 us | 468.280 us | 7,773.233 us | 3.21x |
| `common/uri-miss` | `IsMatch` | 0.175 us | 0.065 us | 0.082 us | 2.70x |
| `common/match-word` | `Match` | 0.197 us | 0.076 us | 0.969 us | 2.59x |
| `common/one-node-backtracking` | `IsMatch` | 0.218 us | 0.086 us | 0.125 us | 2.54x |
| `common/ip-match` | `IsMatch` | 0.117 us | 0.048 us | 0.067 us | 2.44x |
| `common/ip-miss` | `IsMatch` | 0.111 us | 0.049 us | 0.068 us | 2.26x |
| `common/email-match` | `IsMatch` | 0.166 us | 0.080 us | 0.104 us | 2.07x |
| `common/email-miss` | `IsMatch` | 0.175 us | 0.087 us | 0.112 us | 2.00x |
| `common/uri-match` | `IsMatch` | 0.170 us | 0.086 us | 0.116 us | 1.97x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.051 us | 0.029 us | 0.045 us | 1.77x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.031 us | 0.049 us | 1.76x |
| `industry/mariomka-uri-count` | `Count` | 1,720.477 us | 1,074.849 us | 8,360.133 us | 1.60x |
| `industry/leipzig-symbol-count` | `Count` | 1,780.572 us | 1,219.636 us | 6,481.300 us | 1.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.055 us | 0.074 us | 1.39x |
| `industry/leipzig-name-family-count` | `Count` | 4,121.546 us | 3,003.856 us | 8,076.667 us | 1.37x |
| `industry/leipzig-twain-count` | `Count` | 1,251.300 us | 1,267.517 us | 5,398.833 us | 0.99x |
| `common/matches-words` | `Count` | 9.121 us | 10.499 us | 13.597 us | 0.87x |
| `common/split-words` | `Split` | 15.569 us | 20.108 us | 21.859 us | 0.77x |
| `industry/mariomka-ip-count` | `Count` | 2,670.995 us | 3,498.420 us | 11,024.950 us | 0.76x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.042 us | 0.071 us | 0.65x |
| `common/matches-word` | `Count` | 1.389 us | 2.301 us | 3.452 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.106 us | 0.195 us | 0.227 us | 0.54x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.034 us | 0.052 us | 0.35x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,909.111 us | 8,619.250 us | 9,190.567 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 494.047 us | 3,904.046 us | 4,349.175 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.261 us | 1,453.771 us | 1,809.010 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.621 us | 102.587 us | 104.404 us | 0.07x |
| `common/matches-set` | `Count` | 7.598 us | 103.734 us | 104.615 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 241.238 us | 4,685.282 us | 5,119.890 us | 0.05x |
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
| `lokad/folding/region-marker` | 4,048.023 us | 38.250 us | 61.167 us | 105.83x |
| `lokad/imports/module-imports` | 31.333 us | 6.324 us | 28.805 us | 4.95x |
| `lokad/lexer/doc-line` | 4.269 us | 2.877 us | 3.506 us | 1.48x |
| `structural/keyword-to-generic-type` | 5.663 us | 3.970 us | 15.398 us | 1.43x |
| `lokad/lexer/identifier` | 30.401 us | 21.642 us | 24.985 us | 1.40x |
| `lokad/langserv/helper-identifier` | 9.421 us | 7.088 us | 7.668 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.635 us | 1.344 us | 1.448 us | 1.22x |
| `lokad/style/hex-color` | 0.905 us | 0.744 us | 0.839 us | 1.22x |
| `structural/ordered-keyword-pair` | 6.107 us | 5.301 us | 16.711 us | 1.15x |
| `lokad/lexer/operator-run` | 3.762 us | 3.444 us | 3.828 us | 1.09x |
| `fallback/lookahead` | 5.972 us | 5.968 us | 16.865 us | 1.00x |
| `lokad/docs/inline-doc-prefix` | 4.342 us | 4.640 us | 5.354 us | 0.94x |
| `lokad/langserv/identifier-validator` | 11.260 us | 12.277 us | 13.463 us | 0.92x |
| `literal-family/method-token-family` | 7.952 us | 9.015 us | 19.603 us | 0.88x |
| `lokad/lexer/number` | 10.553 us | 14.142 us | 15.155 us | 0.75x |
| `literal/call-token` | 2.972 us | 4.215 us | 15.433 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 1.989 us | 2.868 us | 2.965 us | 0.69x |
| `structural/method-family-generic-call` | 4.368 us | 6.506 us | 17.570 us | 0.67x |
| `lokad/style/cell-ref` | 1.949 us | 2.916 us | 3.014 us | 0.67x |
| `literal/identifier-token` | 2.699 us | 4.263 us | 15.484 us | 0.63x |
| `structural/method-family-call` | 4.765 us | 8.022 us | 18.783 us | 0.59x |
| `literal-family/type-token-family` | 11.831 us | 23.147 us | 33.504 us | 0.51x |
| `literal/identifier-token-casei` | 6.870 us | 14.586 us | 25.726 us | 0.47x |
| `lokad/style/range-ref` | 1.179 us | 2.653 us | 2.650 us | 0.44x |
| `structural/ordered-keyword-window` | 6.762 us | 17.683 us | 28.359 us | 0.38x |
| `structural/ordered-modifier-pair` | 6.038 us | 17.344 us | 28.558 us | 0.35x |
| `structural/keyword-family-to-capitalized-identifier` | 73.917 us | 218.503 us | 232.650 us | 0.34x |
| `lokad/lexer/string` | 28.997 us | 94.900 us | 99.149 us | 0.31x |
| `structural/modifier-family-to-type-window` | 75.617 us | 252.489 us | 272.056 us | 0.30x |
| `structural/ordered-identifier-window` | 6.403 us | 28.438 us | 39.776 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 24.585 us | 127.353 us | 141.479 us | 0.19x |
| `lokad/langserv/url-dashboard` | 2.861 us | 24.413 us | 24.678 us | 0.12x |
| `lokad/langserv/url-download` | 2.792 us | 51.110 us | 51.358 us | 0.05x |
| `balancing/begin-end-blocks` | 7.226 us | 4,295.833 us | 4,279.958 us | 0.0017x |
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
| `lokad/folding/region-marker` | 4,056.177 us | 37.782 us | 59.082 us | 107.36x |
| `structural/keyword-family-to-capitalized-identifier` | 73.860 us | 7.518 us | 18.548 us | 9.82x |
| `lokad/imports/module-imports` | 31.369 us | 4.309 us | 26.533 us | 7.28x |
| `structural/modifier-family-to-type-pair` | 24.669 us | 10.469 us | 21.326 us | 2.36x |
| `lokad/langserv/helper-identifier` | 9.343 us | 4.627 us | 5.606 us | 2.02x |
| `lokad/lexer/doc-line` | 4.197 us | 2.157 us | 2.891 us | 1.95x |
| `lokad/lexer/identifier` | 30.286 us | 16.019 us | 20.039 us | 1.89x |
| `lokad/lexer/operator-run` | 3.726 us | 2.025 us | 2.497 us | 1.84x |
| `lokad/style/hex-color` | 0.901 us | 0.497 us | 0.579 us | 1.81x |
| `structural/keyword-to-generic-type` | 5.667 us | 3.365 us | 14.240 us | 1.68x |
| `lokad/langserv/color-short-hex` | 1.610 us | 0.981 us | 1.154 us | 1.64x |
| `lokad/docs/inline-doc-prefix` | 4.368 us | 2.777 us | 3.486 us | 1.57x |
| `structural/ordered-keyword-pair` | 6.126 us | 4.079 us | 14.937 us | 1.50x |
| `fallback/lookahead` | 5.974 us | 4.131 us | 14.617 us | 1.45x |
| `lokad/lexer/number` | 10.589 us | 7.415 us | 8.396 us | 1.43x |
| `structural/modifier-family-to-type-window` | 74.933 us | 55.266 us | 67.371 us | 1.36x |
| `lokad/langserv/identifier-validator` | 11.364 us | 8.711 us | 10.719 us | 1.30x |
| `literal/identifier-token-casei` | 6.932 us | 5.797 us | 16.768 us | 1.20x |
| `lokad/langserv/color-rgb-hex` | 2.009 us | 1.770 us | 1.991 us | 1.13x |
| `lokad/style/cell-ref` | 1.970 us | 1.737 us | 1.939 us | 1.13x |
| `literal-family/type-token-family` | 11.600 us | 11.076 us | 21.588 us | 1.05x |
| `structural/method-family-generic-call` | 4.367 us | 4.534 us | 14.957 us | 0.96x |
| `structural/method-family-call` | 4.762 us | 5.031 us | 15.464 us | 0.95x |
| `literal-family/method-token-family` | 4.671 us | 5.173 us | 15.470 us | 0.90x |
| `lokad/style/range-ref` | 1.184 us | 1.484 us | 1.557 us | 0.80x |
| `structural/ordered-keyword-window` | 6.739 us | 8.555 us | 19.258 us | 0.79x |
| `literal/call-token` | 3.014 us | 3.848 us | 14.417 us | 0.78x |
| `structural/ordered-modifier-pair` | 6.066 us | 8.402 us | 19.647 us | 0.72x |
| `lokad/lexer/string` | 28.935 us | 44.393 us | 47.773 us | 0.65x |
| `literal/identifier-token` | 2.716 us | 4.180 us | 15.249 us | 0.65x |
| `structural/ordered-identifier-window` | 6.324 us | 10.504 us | 21.336 us | 0.60x |
| `lokad/langserv/url-dashboard` | 2.839 us | 7.917 us | 8.458 us | 0.36x |
| `lokad/langserv/url-download` | 2.814 us | 12.868 us | 13.434 us | 0.22x |
| `balancing/begin-end-blocks` | 7.189 us | 452.944 us | 484.757 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
