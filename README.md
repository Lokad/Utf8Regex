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
and resource checks passed in **46m30s**. The [run receipt](bench/Experiments/DeliveryDev46-20261106/full-refresh.json)
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
| `literal/sherlock-zh` | 85.732 us | 18.858 us | 2,148.520 us | 4.55x |
| `literal/sherlock-ru` | 167.192 us | 98.479 us | 5,338.514 us | 1.70x |
| `literal-alternate/sherlock-zh` | 105.329 us | 69.792 us | 2,146.670 us | 1.51x |
| `literal/sherlock-casei-en` | 159.495 us | 192.519 us | 560.767 us | 0.83x |
| `literal/sherlock-casei-ru` | 255.242 us | 387.250 us | 5,654.950 us | 0.66x |
| `literal/sherlock-en` | 57.061 us | 87.079 us | 473.011 us | 0.66x |
| `ruff-noqa/tweaked` | 4,377.667 us | 7,815.782 us | 43,206.000 us | 0.56x |
| `aws-keys/quick` | 5,044.187 us | 9,789.837 us | 41,658.800 us | 0.52x |
| `bounded-repeat/letters-en` | 1,064.958 us | 2,808.174 us | 2,931.565 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,666.149 us | 4,780.581 us | 9,981.400 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,926.092 us | 5,921.385 us | 6,771.975 us | 0.33x |
| `dictionary/single` | 8,226.390 us | 25,643.233 us | 25,741.967 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 185.719 us | 597.874 us | 1,022.543 us | 0.31x |
| `literal-alternate/sherlock-en` | 384.775 us | 1,492.380 us | 1,981.436 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 155.141 us | 667.931 us | 1,072.103 us | 0.23x |
| `bounded-repeat/capitals` | 9,528.700 us | 46,546.200 us | 54,237.350 us | 0.20x |
| `bounded-repeat/context` | 24,677.400 us | 148,180.400 us | 152,173.500 us | 0.17x |
| `aws-keys/full` | 10,633.950 us | 85,880.800 us | 116,322.600 us | 0.12x |
| `ruff-noqa/real` | 4,482.824 us | 70,270.600 us | 103,817.300 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 957.045 us | 23,014.400 us | 28,130.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 128.721 us | 10,904.771 us | 11,879.500 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,188.234 us | 486.968 us | 8,840.425 us | 2.44x |
| `common/match-word` | `Match` | 0.199 us | 0.172 us | 1.169 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,160.592 us | 1,325.961 us | 8,525.960 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,426.526 us | 1,910.392 us | 10,442.375 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.110 us | 0.147 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.195 us | 0.218 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.393 us | 259.836 us | 480.201 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.161 us | 0.178 us | 0.49x |
| `common/ip-match` | `IsMatch` | 0.077 us | 0.173 us | 0.194 us | 0.44x |
| `industry/leipzig-name-family-count` | `Count` | 4,088.467 us | 9,862.900 us | 16,919.900 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.187 us | 0.238 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.233 us | 0.275 us | 0.40x |
| `common/replace-words` | `Replace` | 28.322 us | 78.842 us | 79.732 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.273 us | 0.303 us | 0.35x |
| `common/matches-word` | `Count` | 1.351 us | 4.243 us | 6.008 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.345 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,314.906 us | 8,574.911 us | 17,104.767 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.106 us | 0.133 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.082 us | 0.097 us | 0.20x |
| `common/split-words` | `Split` | 14.579 us | 77.649 us | 79.199 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.500 us | 973.450 us | 1,238.150 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.558 us | 0.591 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.085 us | 0.613 us | 0.643 us | 0.14x |
| `common/matches-words` | `Count` | 9.364 us | 71.662 us | 73.196 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.839 us | 0.878 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,412.309 us | 32,219.100 us | 38,974.900 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,887.230 us | 19,263.750 us | 19,776.750 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,893.965 us | 23,468.150 us | 30,883.150 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 513.346 us | 8,162.480 us | 8,631.444 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.439 us | 163.157 us | 166.347 us | 0.05x |
| `common/matches-set` | `Count` | 7.860 us | 193.485 us | 194.414 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 247.632 us | 11,798.057 us | 12,392.443 us | 0.02x |
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
| `literal/sherlock-zh` | 85.532 us | 23.877 us | 2,155.406 us | 3.58x |
| `literal-alternate/sherlock-zh` | 105.281 us | 35.806 us | 2,120.258 us | 2.94x |
| `literal/sherlock-casei-en` | 158.404 us | 78.749 us | 414.324 us | 2.01x |
| `literal/sherlock-ru` | 167.520 us | 89.880 us | 5,313.620 us | 1.86x |
| `aws-keys/quick` | 5,050.580 us | 3,692.019 us | 35,597.100 us | 1.37x |
| `literal/sherlock-casei-ru` | 257.157 us | 223.648 us | 5,358.467 us | 1.15x |
| `literal-alternate/sherlock-en` | 388.012 us | 369.635 us | 777.339 us | 1.05x |
| `literal-alternate/sherlock-en-mixed` | 184.955 us | 176.374 us | 530.943 us | 1.05x |
| `ruff-noqa/tweaked` | 4,382.139 us | 4,547.035 us | 38,342.550 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,928.847 us | 2,073.500 us | 2,880.319 us | 0.93x |
| `literal-alternate/sherlock-casei-en` | 129.160 us | 147.919 us | 547.475 us | 0.87x |
| `literal-alternate/sherlock-en-nomatch` | 154.810 us | 199.852 us | 577.942 us | 0.77x |
| `literal/sherlock-en` | 56.988 us | 75.937 us | 427.527 us | 0.75x |
| `bounded-repeat/capitals` | 9,599.633 us | 13,583.300 us | 21,459.925 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,620.526 us | 2,351.047 us | 7,447.050 us | 0.69x |
| `bounded-repeat/letters-en` | 1,064.599 us | 1,834.586 us | 1,917.620 us | 0.58x |
| `aws-keys/full` | 9,354.938 us | 18,423.640 us | 50,323.333 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 947.586 us | 1,968.095 us | 7,078.300 us | 0.48x |
| `bounded-repeat/context` | 24,697.200 us | 69,337.450 us | 72,433.350 us | 0.36x |
| `ruff-noqa/real` | 4,486.076 us | 21,084.300 us | 54,980.433 us | 0.21x |
| `dictionary/single` | 8,240.190 us | 46,071.050 us | 45,621.800 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,182.222 us | 456.305 us | 7,809.800 us | 2.59x |
| `common/match-word` | `Match` | 0.199 us | 0.080 us | 1.008 us | 2.49x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.944 us | 59.168 us | 264.189 us | 2.25x |
| `industry/leipzig-river-window-count` | `Count` | 3,436.030 us | 1,563.129 us | 7,938.620 us | 2.20x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.045 us | 0.074 us | 2.12x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.030 us | 0.046 us | 1.80x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.067 us | 0.089 us | 1.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.048 us | 0.087 us | 1.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.032 us | 0.052 us | 1.61x |
| `common/replace-words` | `Replace` | 28.320 us | 17.845 us | 20.627 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.050 us | 0.070 us | 1.50x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.052 us | 0.078 us | 1.48x |
| `industry/leipzig-symbol-count` | `Count` | 1,922.342 us | 1,310.367 us | 7,393.280 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.077 us | 1.37x |
| `industry/leipzig-name-family-count` | `Count` | 4,192.039 us | 3,077.888 us | 7,916.300 us | 1.36x |
| `industry/mariomka-uri-count` | `Count` | 1,433.121 us | 1,095.504 us | 8,413.000 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.080 us | 0.107 us | 1.20x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.089 us | 0.116 us | 1.14x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.089 us | 0.120 us | 1.04x |
| `common/one-node-backtracking` | `IsMatch` | 0.085 us | 0.088 us | 0.126 us | 0.97x |
| `industry/leipzig-twain-count` | `Count` | 1,160.514 us | 1,288.229 us | 7,333.510 us | 0.90x |
| `common/matches-words` | `Count` | 9.309 us | 10.867 us | 14.379 us | 0.86x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.073 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,314.282 us | 3,489.350 us | 10,865.167 us | 0.66x |
| `common/split-words` | `Split` | 14.555 us | 23.873 us | 25.716 us | 0.61x |
| `common/matches-word` | `Count` | 1.351 us | 2.345 us | 3.445 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.202 us | 0.235 us | 0.48x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.975 us | 594.788 us | 862.076 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.054 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,904.213 us | 8,691.744 us | 9,223.350 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 509.539 us | 3,971.474 us | 4,325.594 us | 0.13x |
| `common/matches-set` | `Count` | 7.855 us | 105.728 us | 107.865 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.455 us | 104.367 us | 106.057 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 243.646 us | 4,772.344 us | 5,210.340 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.667 us | 6.343 us | 28.699 us | 1.68x |
| `structural/keyword-to-generic-type` | 5.578 us | 4.036 us | 15.688 us | 1.38x |
| `lokad/langserv/color-short-hex` | 1.675 us | 1.363 us | 1.469 us | 1.23x |
| `lokad/lexer/doc-line` | 3.335 us | 3.089 us | 3.730 us | 1.08x |
| `structural/ordered-keyword-pair` | 5.695 us | 5.315 us | 16.776 us | 1.07x |
| `lokad/lexer/identifier` | 23.755 us | 23.666 us | 26.451 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.271 us | 7.356 us | 7.657 us | 0.99x |
| `fallback/lookahead` | 5.653 us | 6.117 us | 17.484 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.367 us | 12.475 us | 13.958 us | 0.91x |
| `lokad/lexer/operator-run` | 2.889 us | 3.538 us | 3.946 us | 0.82x |
| `literal-family/method-token-family` | 7.121 us | 9.294 us | 19.897 us | 0.77x |
| `lokad/style/hex-color` | 0.607 us | 0.815 us | 0.857 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 3.369 us | 4.864 us | 5.376 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.018 us | 2.933 us | 3.032 us | 0.69x |
| `literal/identifier-token` | 2.798 us | 4.411 us | 15.551 us | 0.63x |
| `lokad/lexer/number` | 8.858 us | 14.297 us | 15.213 us | 0.62x |
| `literal/call-token` | 2.600 us | 4.383 us | 15.884 us | 0.59x |
| `structural/ordered-identifier-window` | 16.062 us | 28.829 us | 39.730 us | 0.56x |
| `lokad/style/cell-ref` | 1.492 us | 2.850 us | 2.927 us | 0.52x |
| `structural/method-family-generic-call` | 3.460 us | 6.808 us | 17.896 us | 0.51x |
| `structural/method-family-call` | 3.860 us | 8.028 us | 18.774 us | 0.48x |
| `literal-family/type-token-family` | 10.487 us | 24.236 us | 35.799 us | 0.43x |
| `literal/identifier-token-casei` | 5.816 us | 15.696 us | 26.600 us | 0.37x |
| `lokad/style/range-ref` | 0.914 us | 2.756 us | 2.794 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.717 us | 17.697 us | 29.016 us | 0.32x |
| `structural/ordered-keyword-window` | 5.311 us | 17.486 us | 28.584 us | 0.30x |
| `structural/modifier-family-to-type-window` | 76.912 us | 254.500 us | 268.674 us | 0.30x |
| `lokad/lexer/string` | 21.534 us | 93.908 us | 99.630 us | 0.23x |
| `lokad/folding/region-marker` | 6.356 us | 37.076 us | 61.121 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.561 us | 24.383 us | 25.020 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 11.424 us | 217.395 us | 229.475 us | 0.05x |
| `lokad/langserv/url-download` | 2.674 us | 52.529 us | 52.654 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.959 us | 138.928 us | 151.113 us | 0.04x |
| `balancing/begin-end-blocks` | 5.686 us | 4,345.122 us | 4,361.239 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.752 us | 4.413 us | 25.923 us | 2.44x |
| `lokad/langserv/color-short-hex` | 1.680 us | 0.985 us | 1.198 us | 1.71x |
| `structural/keyword-to-generic-type` | 5.573 us | 3.500 us | 14.607 us | 1.59x |
| `lokad/langserv/helper-identifier` | 7.230 us | 4.555 us | 5.611 us | 1.59x |
| `structural/ordered-identifier-window` | 16.113 us | 10.552 us | 21.293 us | 1.53x |
| `lokad/lexer/doc-line` | 3.337 us | 2.190 us | 2.920 us | 1.52x |
| `lokad/lexer/operator-run` | 2.902 us | 1.988 us | 2.570 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.392 us | 7.964 us | 18.741 us | 1.43x |
| `lokad/lexer/identifier` | 23.784 us | 17.026 us | 21.699 us | 1.40x |
| `structural/modifier-family-to-type-window` | 77.787 us | 56.047 us | 68.623 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.688 us | 4.147 us | 14.968 us | 1.37x |
| `fallback/lookahead` | 5.662 us | 4.203 us | 15.194 us | 1.35x |
| `lokad/langserv/identifier-validator` | 11.892 us | 9.054 us | 11.187 us | 1.31x |
| `lokad/docs/inline-doc-prefix` | 3.348 us | 2.749 us | 3.530 us | 1.22x |
| `lokad/style/hex-color` | 0.608 us | 0.502 us | 0.604 us | 1.21x |
| `lokad/lexer/number` | 8.834 us | 7.333 us | 8.443 us | 1.20x |
| `lokad/langserv/color-rgb-hex` | 2.040 us | 1.896 us | 2.120 us | 1.08x |
| `literal/identifier-token-casei` | 5.831 us | 5.906 us | 17.124 us | 0.99x |
| `literal-family/type-token-family` | 10.455 us | 11.474 us | 22.405 us | 0.91x |
| `lokad/style/cell-ref` | 1.495 us | 1.716 us | 1.916 us | 0.87x |
| `structural/method-family-generic-call` | 3.447 us | 4.479 us | 14.998 us | 0.77x |
| `structural/method-family-call` | 3.833 us | 5.079 us | 15.444 us | 0.75x |
| `literal-family/method-token-family` | 3.891 us | 5.349 us | 15.757 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.710 us | 8.669 us | 19.631 us | 0.66x |
| `literal/call-token` | 2.611 us | 3.977 us | 14.966 us | 0.66x |
| `structural/ordered-keyword-window` | 5.354 us | 8.493 us | 19.345 us | 0.63x |
| `lokad/style/range-ref` | 0.927 us | 1.521 us | 1.586 us | 0.61x |
| `literal/identifier-token` | 2.822 us | 4.995 us | 15.596 us | 0.57x |
| `lokad/lexer/string` | 21.499 us | 44.942 us | 48.495 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 4.944 us | 10.798 us | 21.593 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.598 us | 7.478 us | 8.076 us | 0.35x |
| `lokad/langserv/url-download` | 2.674 us | 13.412 us | 13.456 us | 0.20x |
| `lokad/folding/region-marker` | 6.398 us | 37.164 us | 59.650 us | 0.17x |
| `balancing/begin-end-blocks` | 5.688 us | 467.665 us | 497.016 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
