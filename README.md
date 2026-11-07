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
and resource checks passed in **46m27s**. The [run receipt](bench/Experiments/DeliveryDev54-20261107/full-refresh.json)
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
| `literal/sherlock-zh` | 81.000 us | 18.512 us | 2,131.314 us | 4.38x |
| `literal/sherlock-ru` | 164.933 us | 95.388 us | 5,210.160 us | 1.73x |
| `literal-alternate/sherlock-zh` | 102.339 us | 68.425 us | 2,163.221 us | 1.50x |
| `literal/sherlock-casei-en` | 160.912 us | 195.352 us | 559.657 us | 0.82x |
| `literal/sherlock-en` | 57.408 us | 86.785 us | 437.250 us | 0.66x |
| `literal/sherlock-casei-ru` | 249.383 us | 385.709 us | 5,730.946 us | 0.65x |
| `ruff-noqa/tweaked` | 4,317.067 us | 7,353.209 us | 40,751.950 us | 0.59x |
| `aws-keys/quick` | 4,748.429 us | 9,453.844 us | 43,564.150 us | 0.50x |
| `bounded-repeat/letters-en` | 1,088.651 us | 2,774.907 us | 2,894.431 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,654.339 us | 4,785.663 us | 9,883.800 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,957.123 us | 5,934.000 us | 6,750.542 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 189.606 us | 581.517 us | 997.824 us | 0.33x |
| `dictionary/single` | 8,065.710 us | 25,481.167 us | 25,756.433 us | 0.32x |
| `literal-alternate/sherlock-en` | 377.202 us | 1,505.484 us | 2,078.392 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 151.400 us | 651.951 us | 1,062.628 us | 0.23x |
| `bounded-repeat/capitals` | 9,238.250 us | 44,746.400 us | 48,175.950 us | 0.21x |
| `bounded-repeat/context` | 20,122.550 us | 145,443.600 us | 148,528.500 us | 0.14x |
| `aws-keys/full` | 9,822.856 us | 84,290.500 us | 115,326.500 us | 0.12x |
| `ruff-noqa/real` | 4,334.061 us | 68,113.100 us | 101,797.400 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 951.462 us | 23,463.225 us | 28,925.150 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 133.784 us | 10,763.543 us | 11,540.886 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,107.629 us | 483.740 us | 8,821.350 us | 2.29x |
| `common/match-word` | `Match` | 0.193 us | 0.164 us | 1.082 us | 1.18x |
| `industry/leipzig-twain-count` | `Count` | 1,125.261 us | 1,270.405 us | 8,428.433 us | 0.89x |
| `industry/mariomka-uri-count` | `Count` | 1,424.187 us | 1,895.730 us | 10,322.475 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.108 us | 0.146 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.188 us | 0.208 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 128.647 us | 258.637 us | 500.276 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.165 us | 0.182 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.174 us | 0.193 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 3,984.511 us | 9,612.487 us | 16,421.133 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.183 us | 0.203 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.226 us | 0.265 us | 0.40x |
| `common/replace-words` | `Replace` | 27.245 us | 74.341 us | 76.845 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.266 us | 0.292 us | 0.36x |
| `common/matches-word` | `Count` | 1.315 us | 4.194 us | 5.931 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.336 us | 0.364 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,182.826 us | 8,121.510 us | 16,470.067 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.107 us | 0.136 us | 0.26x |
| `common/split-words` | `Split` | 17.280 us | 77.574 us | 78.002 us | 0.22x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.080 us | 0.096 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.356 us | 951.061 us | 1,219.414 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.085 us | 0.549 us | 0.580 us | 0.16x |
| `common/one-node-backtracking` | `IsMatch` | 0.087 us | 0.600 us | 0.634 us | 0.14x |
| `common/matches-words` | `Count` | 9.173 us | 69.494 us | 71.526 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.811 us | 0.854 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,259.242 us | 31,453.933 us | 38,907.000 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,929.717 us | 19,302.600 us | 20,192.225 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,788.747 us | 22,489.725 us | 29,775.450 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 613.282 us | 8,114.820 us | 8,576.533 us | 0.08x |
| `common/matches-boundary` | `Count` | 7.504 us | 160.450 us | 163.134 us | 0.05x |
| `common/matches-set` | `Count` | 7.466 us | 190.830 us | 192.132 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 234.792 us | 11,648.200 us | 12,157.129 us | 0.02x |
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
| `literal/sherlock-zh` | 80.524 us | 23.584 us | 2,120.714 us | 3.41x |
| `literal-alternate/sherlock-zh` | 101.220 us | 35.308 us | 2,100.808 us | 2.87x |
| `literal/sherlock-casei-en` | 161.627 us | 79.916 us | 425.990 us | 2.02x |
| `literal/sherlock-ru` | 165.088 us | 86.462 us | 5,151.633 us | 1.91x |
| `aws-keys/quick` | 4,772.069 us | 3,530.236 us | 35,531.750 us | 1.35x |
| `literal/sherlock-casei-ru` | 249.948 us | 194.978 us | 5,511.050 us | 1.28x |
| `literal-alternate/sherlock-en-mixed` | 188.341 us | 173.695 us | 512.374 us | 1.08x |
| `literal-alternate/sherlock-en` | 375.348 us | 364.121 us | 784.212 us | 1.03x |
| `ruff-noqa/tweaked` | 4,349.211 us | 4,445.528 us | 35,185.900 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,954.567 us | 2,054.546 us | 2,857.833 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 133.425 us | 143.027 us | 556.146 us | 0.93x |
| `literal-alternate/sherlock-en-nomatch` | 151.397 us | 198.543 us | 586.171 us | 0.76x |
| `literal/sherlock-en` | 57.223 us | 76.701 us | 431.827 us | 0.75x |
| `bounded-repeat/capitals` | 9,290.022 us | 13,291.067 us | 16,142.875 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,593.138 us | 2,331.988 us | 7,291.425 us | 0.68x |
| `bounded-repeat/letters-en` | 1,089.074 us | 1,777.937 us | 1,866.832 us | 0.61x |
| `aws-keys/full` | 10,216.289 us | 18,097.840 us | 51,506.900 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 949.252 us | 1,944.287 us | 7,496.375 us | 0.49x |
| `bounded-repeat/context` | 20,134.625 us | 68,862.400 us | 71,464.850 us | 0.29x |
| `ruff-noqa/real` | 4,402.894 us | 20,298.200 us | 52,910.267 us | 0.22x |
| `dictionary/single` | 7,974.940 us | 44,885.350 us | 44,998.950 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.194 us | 0.073 us | 0.967 us | 2.66x |
| `industry/mariomka-email-count` | `Count` | 1,102.984 us | 453.722 us | 7,702.700 us | 2.43x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 129.971 us | 57.674 us | 252.121 us | 2.25x |
| `industry/leipzig-river-window-count` | `Count` | 3,280.770 us | 1,483.647 us | 7,724.880 us | 2.21x |
| `common/backtracking` | `IsMatch` | 0.090 us | 0.043 us | 0.073 us | 2.10x |
| `common/uri-miss` | `IsMatch` | 0.115 us | 0.065 us | 0.087 us | 1.76x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.047 us | 1.62x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.048 us | 0.084 us | 1.61x |
| `common/replace-words` | `Replace` | 27.537 us | 17.116 us | 19.810 us | 1.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.032 us | 0.051 us | 1.60x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.048 us | 0.069 us | 1.53x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.050 us | 0.069 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,807.550 us | 1,224.400 us | 7,123.100 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.073 us | 0.054 us | 0.076 us | 1.34x |
| `industry/leipzig-name-family-count` | `Count` | 4,080.884 us | 3,050.096 us | 8,199.425 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,408.529 us | 1,073.220 us | 8,373.925 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.083 us | 0.108 us | 1.16x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.088 us | 0.115 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.089 us | 0.087 us | 0.117 us | 1.02x |
| `common/one-node-backtracking` | `IsMatch` | 0.085 us | 0.087 us | 0.126 us | 0.98x |
| `industry/leipzig-twain-count` | `Count` | 1,147.602 us | 1,261.473 us | 7,271.240 us | 0.91x |
| `common/matches-words` | `Count` | 9.150 us | 10.845 us | 13.925 us | 0.84x |
| `common/split-words` | `Split` | 17.348 us | 20.974 us | 23.011 us | 0.83x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.071 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,193.303 us | 3,401.859 us | 10,710.567 us | 0.64x |
| `common/matches-word` | `Count` | 1.324 us | 2.316 us | 3.333 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.205 us | 0.230 us | 0.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,923.923 us | 8,718.400 us | 9,380.638 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 613.304 us | 3,922.220 us | 4,321.033 us | 0.16x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.359 us | 1,446.275 us | 1,778.716 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.530 us | 102.580 us | 104.362 us | 0.07x |
| `common/matches-set` | `Count` | 7.438 us | 103.094 us | 105.870 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 229.385 us | 4,705.412 us | 5,172.400 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.237 us | 6.435 us | 29.066 us | 1.59x |
| `structural/keyword-to-generic-type` | 5.476 us | 4.111 us | 15.692 us | 1.33x |
| `lokad/langserv/color-short-hex` | 1.683 us | 1.367 us | 1.558 us | 1.23x |
| `lokad/lexer/doc-line` | 3.367 us | 3.127 us | 3.905 us | 1.08x |
| `lokad/lexer/identifier` | 24.039 us | 23.264 us | 27.152 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.530 us | 5.433 us | 17.129 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.237 us | 7.463 us | 8.355 us | 0.97x |
| `lokad/langserv/identifier-validator` | 11.422 us | 12.649 us | 14.606 us | 0.90x |
| `fallback/lookahead` | 5.415 us | 6.116 us | 17.195 us | 0.89x |
| `lokad/lexer/operator-run` | 2.925 us | 3.466 us | 4.024 us | 0.84x |
| `literal-family/method-token-family` | 7.100 us | 9.083 us | 19.588 us | 0.78x |
| `lokad/style/hex-color` | 0.613 us | 0.810 us | 0.869 us | 0.76x |
| `lokad/docs/inline-doc-prefix` | 3.400 us | 4.881 us | 5.705 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.037 us | 2.962 us | 3.104 us | 0.69x |
| `literal/identifier-token` | 2.802 us | 4.383 us | 15.777 us | 0.64x |
| `literal/call-token` | 2.613 us | 4.319 us | 15.765 us | 0.60x |
| `lokad/lexer/number` | 8.347 us | 14.424 us | 15.790 us | 0.58x |
| `structural/ordered-identifier-window` | 16.243 us | 29.021 us | 39.872 us | 0.56x |
| `lokad/style/cell-ref` | 1.500 us | 2.854 us | 2.885 us | 0.53x |
| `structural/method-family-generic-call` | 3.458 us | 6.785 us | 17.966 us | 0.51x |
| `structural/method-family-call` | 3.876 us | 8.387 us | 19.120 us | 0.46x |
| `literal-family/type-token-family` | 10.313 us | 23.349 us | 34.791 us | 0.44x |
| `literal/identifier-token-casei` | 5.769 us | 14.919 us | 26.620 us | 0.39x |
| `lokad/style/range-ref` | 0.917 us | 2.811 us | 2.805 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.508 us | 17.391 us | 28.378 us | 0.32x |
| `structural/modifier-family-to-type-window` | 80.093 us | 258.232 us | 270.101 us | 0.31x |
| `structural/ordered-keyword-window` | 5.495 us | 18.078 us | 29.159 us | 0.30x |
| `lokad/lexer/string` | 21.949 us | 97.060 us | 103.108 us | 0.23x |
| `lokad/folding/region-marker` | 6.036 us | 36.866 us | 61.687 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.562 us | 24.701 us | 25.162 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.523 us | 218.145 us | 228.503 us | 0.05x |
| `lokad/langserv/url-download` | 2.577 us | 52.325 us | 52.791 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.041 us | 133.087 us | 144.678 us | 0.04x |
| `balancing/begin-end-blocks` | 5.703 us | 4,367.912 us | 4,388.959 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.271 us | 4.476 us | 26.569 us | 2.29x |
| `structural/keyword-to-generic-type` | 5.494 us | 3.517 us | 14.639 us | 1.56x |
| `lokad/langserv/color-short-hex` | 1.677 us | 1.076 us | 1.289 us | 1.56x |
| `structural/ordered-identifier-window` | 16.253 us | 10.718 us | 21.507 us | 1.52x |
| `lokad/langserv/helper-identifier` | 7.231 us | 4.953 us | 6.252 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.512 us | 7.920 us | 18.448 us | 1.45x |
| `lokad/lexer/doc-line` | 3.366 us | 2.320 us | 3.193 us | 1.45x |
| `structural/modifier-family-to-type-window` | 80.026 us | 57.433 us | 69.049 us | 1.39x |
| `lokad/lexer/operator-run` | 2.927 us | 2.145 us | 2.827 us | 1.36x |
| `structural/ordered-keyword-pair` | 5.530 us | 4.147 us | 15.219 us | 1.33x |
| `lokad/lexer/identifier` | 24.029 us | 18.109 us | 22.422 us | 1.33x |
| `fallback/lookahead` | 5.393 us | 4.262 us | 14.925 us | 1.27x |
| `lokad/style/hex-color` | 0.610 us | 0.517 us | 0.611 us | 1.18x |
| `lokad/docs/inline-doc-prefix` | 3.408 us | 2.954 us | 3.875 us | 1.15x |
| `lokad/langserv/identifier-validator` | 11.405 us | 9.960 us | 12.267 us | 1.15x |
| `lokad/lexer/number` | 8.359 us | 7.649 us | 8.905 us | 1.09x |
| `lokad/langserv/color-rgb-hex` | 2.039 us | 1.918 us | 2.196 us | 1.06x |
| `literal/identifier-token-casei` | 5.695 us | 5.830 us | 17.213 us | 0.98x |
| `literal-family/type-token-family` | 10.385 us | 11.415 us | 22.123 us | 0.91x |
| `lokad/style/cell-ref` | 1.500 us | 1.702 us | 1.894 us | 0.88x |
| `structural/method-family-generic-call` | 3.456 us | 4.516 us | 15.026 us | 0.77x |
| `structural/method-family-call` | 3.892 us | 5.095 us | 15.574 us | 0.76x |
| `literal-family/method-token-family` | 3.789 us | 5.225 us | 15.544 us | 0.73x |
| `literal/call-token` | 2.606 us | 3.964 us | 14.849 us | 0.66x |
| `structural/ordered-keyword-window` | 5.512 us | 8.629 us | 19.734 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.515 us | 8.763 us | 19.598 us | 0.63x |
| `literal/identifier-token` | 2.805 us | 4.765 us | 15.551 us | 0.59x |
| `lokad/style/range-ref` | 0.917 us | 1.594 us | 1.621 us | 0.58x |
| `structural/modifier-family-to-type-pair` | 5.044 us | 10.747 us | 21.946 us | 0.47x |
| `lokad/lexer/string` | 21.873 us | 47.632 us | 51.703 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.542 us | 7.529 us | 8.121 us | 0.34x |
| `lokad/langserv/url-download` | 2.594 us | 13.063 us | 13.634 us | 0.20x |
| `lokad/folding/region-marker` | 6.064 us | 37.057 us | 60.397 us | 0.16x |
| `balancing/begin-end-blocks` | 5.692 us | 468.387 us | 497.429 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
