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
and resource checks passed in **43m31s**. The [run receipt](bench/Experiments/DeliveryDev26-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 95.110 us | 19.021 us | 2,196.461 us | 5.00x |
| `literal-alternate/sherlock-zh` | 153.480 us | 68.412 us | 2,145.092 us | 2.24x |
| `literal/sherlock-ru` | 199.702 us | 97.903 us | 5,203.830 us | 2.04x |
| `ruff-noqa/tweaked` | 10,187.540 us | 7,989.443 us | 41,685.400 us | 1.28x |
| `literal/sherlock-casei-en` | 160.984 us | 198.455 us | 620.856 us | 0.81x |
| `literal/sherlock-en` | 58.329 us | 90.332 us | 497.412 us | 0.65x |
| `literal/sherlock-casei-ru` | 247.776 us | 393.398 us | 5,713.789 us | 0.63x |
| `aws-keys/quick` | 5,084.790 us | 9,894.817 us | 45,600.200 us | 0.51x |
| `bounded-repeat/letters-en` | 1,044.673 us | 2,842.867 us | 2,940.047 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,695.887 us | 4,916.936 us | 10,096.200 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,935.162 us | 6,004.944 us | 6,811.175 us | 0.32x |
| `dictionary/single` | 8,261.171 us | 25,902.550 us | 26,064.550 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 186.336 us | 600.685 us | 1,031.260 us | 0.31x |
| `literal-alternate/sherlock-en` | 404.181 us | 1,537.779 us | 2,096.992 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 155.280 us | 672.233 us | 1,113.766 us | 0.23x |
| `bounded-repeat/capitals` | 9,570.733 us | 46,347.950 us | 54,252.300 us | 0.21x |
| `aws-keys/full` | 10,774.040 us | 87,663.300 us | 120,047.500 us | 0.12x |
| `bounded-repeat/context` | 16,305.633 us | 148,391.400 us | 152,190.100 us | 0.11x |
| `ruff-noqa/real` | 4,435.018 us | 71,042.900 us | 87,346.700 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,066.983 us | 23,608.167 us | 28,626.650 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 178.907 us | 11,521.500 us | 12,295.400 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,234.644 us | 497.999 us | 9,029.933 us | 2.48x |
| `common/match-word` | `Match` | 0.201 us | 0.168 us | 1.206 us | 1.20x |
| `industry/leipzig-twain-count` | `Count` | 1,158.416 us | 1,345.490 us | 8,626.633 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,469.266 us | 1,966.437 us | 10,568.900 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.112 us | 0.149 us | 0.73x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 181.995 us | 264.031 us | 498.137 us | 0.69x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.205 us | 0.226 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.171 us | 0.188 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.077 us | 0.180 us | 0.200 us | 0.43x |
| `common/ip-miss` | `IsMatch` | 0.078 us | 0.188 us | 0.208 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 4,056.983 us | 9,802.617 us | 17,283.950 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.237 us | 0.273 us | 0.39x |
| `common/replace-words` | `Replace` | 28.328 us | 78.898 us | 81.700 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.274 us | 0.302 us | 0.35x |
| `common/matches-word` | `Count` | 1.355 us | 4.266 us | 6.004 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.348 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,394.300 us | 8,417.557 us | 16,903.200 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.111 us | 0.141 us | 0.26x |
| `common/split-words` | `Split` | 17.036 us | 77.690 us | 79.009 us | 0.22x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.372 us | 965.700 us | 1,256.621 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.566 us | 0.604 us | 0.15x |
| `common/matches-words` | `Count` | 9.380 us | 72.085 us | 73.525 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.617 us | 0.655 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.834 us | 0.878 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,016.724 us | 19,646.350 us | 20,538.400 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,427.364 us | 33,525.000 us | 46,241.900 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,812.675 us | 24,059.233 us | 31,503.500 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 538.552 us | 8,761.350 us | 9,327.617 us | 0.06x |
| `common/matches-set` | `Count` | 8.186 us | 199.445 us | 201.237 us | 0.04x |
| `common/matches-boundary` | `Count` | 6.957 us | 170.889 us | 172.856 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 272.626 us | 12,281.160 us | 12,813.325 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 153.284 us | 36.176 us | 2,136.588 us | 4.24x |
| `literal/sherlock-zh` | 95.502 us | 24.117 us | 2,188.796 us | 3.96x |
| `literal/sherlock-ru` | 208.262 us | 89.481 us | 5,161.220 us | 2.33x |
| `ruff-noqa/tweaked` | 10,428.680 us | 4,582.491 us | 37,574.100 us | 2.28x |
| `literal/sherlock-casei-en` | 161.103 us | 81.377 us | 454.728 us | 1.98x |
| `aws-keys/quick` | 5,078.120 us | 3,698.757 us | 38,151.200 us | 1.37x |
| `literal-alternate/sherlock-casei-en` | 178.206 us | 145.968 us | 601.600 us | 1.22x |
| `literal/sherlock-casei-ru` | 250.249 us | 222.058 us | 5,498.567 us | 1.13x |
| `literal-alternate/sherlock-en` | 402.279 us | 375.611 us | 772.074 us | 1.07x |
| `literal-alternate/sherlock-en-mixed` | 186.983 us | 177.299 us | 577.493 us | 1.05x |
| `bounded-repeat/letters-ru` | 1,941.800 us | 2,148.888 us | 2,955.482 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 154.401 us | 202.089 us | 577.406 us | 0.76x |
| `literal/sherlock-en` | 58.067 us | 79.385 us | 431.254 us | 0.73x |
| `literal-alternate/sherlock-ru` | 1,714.147 us | 2,372.809 us | 7,539.600 us | 0.72x |
| `bounded-repeat/capitals` | 9,604.817 us | 13,769.825 us | 21,131.900 us | 0.70x |
| `aws-keys/full` | 10,778.500 us | 19,006.200 us | 52,513.300 us | 0.57x |
| `bounded-repeat/letters-en` | 1,046.288 us | 1,852.089 us | 1,927.592 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,057.685 us | 1,985.254 us | 7,123.233 us | 0.53x |
| `bounded-repeat/context` | 16,449.033 us | 69,540.400 us | 72,605.600 us | 0.24x |
| `ruff-noqa/real` | 4,423.508 us | 21,628.333 us | 35,212.850 us | 0.20x |
| `dictionary/single` | 8,247.629 us | 46,219.250 us | 46,264.150 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/rust-sherlock-holmes-window-count` | `Count` | 182.468 us | 58.493 us | 276.449 us | 3.12x |
| `industry/mariomka-email-count` | `Count` | 1,237.168 us | 458.565 us | 7,870.333 us | 2.70x |
| `common/match-word` | `Match` | 0.200 us | 0.082 us | 1.022 us | 2.45x |
| `industry/leipzig-river-window-count` | `Count` | 3,447.107 us | 1,621.370 us | 14,548.100 us | 2.13x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.044 us | 0.075 us | 2.09x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.066 us | 0.087 us | 1.84x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.049 us | 0.088 us | 1.68x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.048 us | 1.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.049 us | 1.64x |
| `common/ip-match` | `IsMatch` | 0.077 us | 0.048 us | 0.069 us | 1.59x |
| `common/replace-words` | `Replace` | 28.415 us | 17.979 us | 21.163 us | 1.58x |
| `common/ip-miss` | `IsMatch` | 0.078 us | 0.051 us | 0.071 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,811.854 us | 1,269.607 us | 7,291.267 us | 1.43x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.37x |
| `industry/mariomka-uri-count` | `Count` | 1,472.237 us | 1,101.946 us | 8,541.033 us | 1.34x |
| `industry/leipzig-name-family-count` | `Count` | 4,179.108 us | 3,136.353 us | 8,522.100 us | 1.33x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.081 us | 0.109 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.116 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.089 us | 0.120 us | 1.02x |
| `industry/leipzig-twain-count` | `Count` | 1,159.843 us | 1,300.823 us | 5,276.733 us | 0.89x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.089 us | 0.129 us | 0.89x |
| `common/matches-words` | `Count` | 9.374 us | 11.070 us | 14.161 us | 0.85x |
| `common/split-words` | `Split` | 17.026 us | 21.663 us | 23.368 us | 0.79x |
| `industry/mariomka-ip-count` | `Count` | 2,396.745 us | 3,543.913 us | 11,118.700 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.046 us | 0.075 us | 0.63x |
| `common/matches-word` | `Count` | 1.352 us | 2.367 us | 3.730 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.203 us | 0.235 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.037 us | 0.053 us | 0.29x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 539.652 us | 3,964.508 us | 4,410.042 us | 0.14x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.000 us | 1,504.703 us | 1,840.690 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 2,014.576 us | 17,373.150 us | 18,392.900 us | 0.12x |
| `common/matches-set` | `Count` | 8.186 us | 106.411 us | 108.571 us | 0.08x |
| `common/matches-boundary` | `Count` | 7.005 us | 106.888 us | 108.440 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 272.536 us | 4,825.882 us | 5,286.050 us | 0.06x |
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
| `lokad/imports/module-imports` | 13.965 us | 6.457 us | 29.210 us | 2.16x |
| `structural/keyword-to-generic-type` | 5.385 us | 4.109 us | 15.707 us | 1.31x |
| `lokad/langserv/color-short-hex` | 1.674 us | 1.384 us | 1.522 us | 1.21x |
| `lokad/lexer/doc-line` | 3.354 us | 3.070 us | 3.718 us | 1.09x |
| `structural/ordered-keyword-pair` | 5.608 us | 5.397 us | 16.883 us | 1.04x |
| `lokad/lexer/identifier` | 23.881 us | 23.322 us | 25.992 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.250 us | 7.420 us | 8.064 us | 0.98x |
| `fallback/lookahead` | 5.597 us | 6.086 us | 17.137 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.491 us | 12.703 us | 14.849 us | 0.90x |
| `lokad/style/hex-color` | 0.728 us | 0.806 us | 0.885 us | 0.90x |
| `lokad/lexer/operator-run` | 2.913 us | 3.470 us | 3.920 us | 0.84x |
| `literal-family/method-token-family` | 7.709 us | 9.211 us | 19.887 us | 0.84x |
| `lokad/docs/inline-doc-prefix` | 3.535 us | 4.812 us | 5.659 us | 0.73x |
| `lokad/langserv/color-rgb-hex` | 2.056 us | 2.964 us | 3.167 us | 0.69x |
| `literal/identifier-token` | 2.828 us | 4.420 us | 15.570 us | 0.64x |
| `literal/call-token` | 2.609 us | 4.340 us | 15.828 us | 0.60x |
| `lokad/lexer/number` | 8.332 us | 13.996 us | 15.159 us | 0.60x |
| `structural/method-family-generic-call` | 3.471 us | 6.776 us | 18.166 us | 0.51x |
| `lokad/style/cell-ref` | 1.509 us | 2.996 us | 3.201 us | 0.50x |
| `structural/method-family-call` | 3.872 us | 8.083 us | 19.325 us | 0.48x |
| `literal-family/type-token-family` | 10.581 us | 24.166 us | 35.531 us | 0.44x |
| `literal/identifier-token-casei` | 6.182 us | 15.910 us | 26.824 us | 0.39x |
| `lokad/style/range-ref` | 0.917 us | 2.715 us | 2.841 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.836 us | 17.449 us | 28.617 us | 0.33x |
| `structural/ordered-keyword-window` | 5.528 us | 17.834 us | 29.749 us | 0.31x |
| `structural/modifier-family-to-type-window` | 75.318 us | 258.059 us | 270.388 us | 0.29x |
| `lokad/lexer/string` | 23.203 us | 97.365 us | 102.762 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 23.570 us | 133.861 us | 144.945 us | 0.18x |
| `structural/ordered-identifier-window` | 5.027 us | 29.109 us | 39.859 us | 0.17x |
| `lokad/folding/region-marker` | 6.413 us | 37.327 us | 61.452 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.540 us | 24.698 us | 25.061 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 19.071 us | 222.308 us | 232.588 us | 0.09x |
| `lokad/langserv/url-download` | 2.602 us | 52.737 us | 53.541 us | 0.05x |
| `balancing/begin-end-blocks` | 5.730 us | 4,403.700 us | 4,418.758 us | 0.0013x |
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
| `lokad/imports/module-imports` | 13.980 us | 4.459 us | 26.273 us | 3.14x |
| `structural/keyword-family-to-capitalized-identifier` | 19.045 us | 7.754 us | 18.874 us | 2.46x |
| `structural/modifier-family-to-type-pair` | 23.525 us | 10.783 us | 21.717 us | 2.18x |
| `lokad/langserv/color-short-hex` | 1.669 us | 1.070 us | 1.229 us | 1.56x |
| `structural/keyword-to-generic-type` | 5.378 us | 3.524 us | 14.681 us | 1.53x |
| `lokad/lexer/doc-line` | 3.361 us | 2.261 us | 3.055 us | 1.49x |
| `lokad/langserv/helper-identifier` | 7.214 us | 4.874 us | 5.852 us | 1.48x |
| `lokad/lexer/operator-run` | 2.913 us | 2.130 us | 2.737 us | 1.37x |
| `structural/modifier-family-to-type-window` | 75.678 us | 56.004 us | 68.278 us | 1.35x |
| `lokad/lexer/identifier` | 23.842 us | 17.645 us | 21.053 us | 1.35x |
| `structural/ordered-keyword-pair` | 5.603 us | 4.179 us | 15.029 us | 1.34x |
| `lokad/style/hex-color` | 0.728 us | 0.544 us | 0.622 us | 1.34x |
| `fallback/lookahead` | 5.605 us | 4.249 us | 14.885 us | 1.32x |
| `lokad/docs/inline-doc-prefix` | 3.537 us | 2.926 us | 3.637 us | 1.21x |
| `lokad/langserv/identifier-validator` | 11.481 us | 9.680 us | 12.039 us | 1.19x |
| `lokad/lexer/number` | 8.399 us | 7.476 us | 8.707 us | 1.12x |
| `literal/identifier-token-casei` | 6.178 us | 5.949 us | 17.227 us | 1.04x |
| `lokad/langserv/color-rgb-hex` | 2.042 us | 1.967 us | 2.159 us | 1.04x |
| `literal-family/type-token-family` | 10.525 us | 11.574 us | 22.489 us | 0.91x |
| `lokad/style/cell-ref` | 1.500 us | 1.856 us | 1.999 us | 0.81x |
| `structural/method-family-generic-call` | 3.468 us | 4.530 us | 15.309 us | 0.77x |
| `structural/method-family-call` | 3.882 us | 5.109 us | 15.893 us | 0.76x |
| `literal-family/method-token-family` | 3.901 us | 5.352 us | 15.752 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.868 us | 8.704 us | 19.869 us | 0.67x |
| `literal/call-token` | 2.613 us | 4.024 us | 14.876 us | 0.65x |
| `structural/ordered-keyword-window` | 5.499 us | 8.700 us | 20.154 us | 0.63x |
| `literal/identifier-token` | 2.826 us | 4.747 us | 15.345 us | 0.60x |
| `lokad/style/range-ref` | 0.917 us | 1.618 us | 1.698 us | 0.57x |
| `lokad/lexer/string` | 23.068 us | 46.694 us | 50.735 us | 0.49x |
| `structural/ordered-identifier-window` | 5.034 us | 10.773 us | 21.471 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.505 us | 7.509 us | 8.100 us | 0.33x |
| `lokad/langserv/url-download` | 2.595 us | 13.059 us | 13.583 us | 0.20x |
| `lokad/folding/region-marker` | 6.429 us | 37.246 us | 59.981 us | 0.17x |
| `balancing/begin-end-blocks` | 5.725 us | 471.304 us | 496.821 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
