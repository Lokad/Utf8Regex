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
| `literal/sherlock-zh` | 132.395 us | 18.395 us | 2,150.360 us | 7.20x |
| `literal-alternate/sherlock-zh` | 234.814 us | 67.122 us | 2,207.017 us | 3.50x |
| `bounded-repeat/capitals` | 129,203.000 us | 45,365.950 us | 51,683.100 us | 2.85x |
| `literal/sherlock-ru` | 244.451 us | 95.337 us | 5,214.990 us | 2.56x |
| `ruff-noqa/tweaked` | 10,435.620 us | 7,629.637 us | 40,780.450 us | 1.37x |
| `literal/sherlock-casei-en` | 168.152 us | 197.782 us | 561.635 us | 0.85x |
| `literal/sherlock-en` | 68.690 us | 83.179 us | 460.975 us | 0.83x |
| `literal/sherlock-casei-ru` | 238.954 us | 383.647 us | 5,494.800 us | 0.62x |
| `aws-keys/quick` | 5,812.775 us | 9,917.280 us | 43,556.500 us | 0.59x |
| `bounded-repeat/letters-en` | 1,034.257 us | 2,751.353 us | 2,886.576 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,698.943 us | 4,692.645 us | 9,945.633 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 195.908 us | 596.955 us | 1,038.536 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,883.815 us | 5,862.411 us | 6,674.700 us | 0.32x |
| `dictionary/single` | 8,015.343 us | 25,430.800 us | 25,645.000 us | 0.32x |
| `literal-alternate/sherlock-en` | 396.760 us | 1,498.094 us | 1,999.348 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 162.000 us | 652.818 us | 1,085.314 us | 0.25x |
| `bounded-repeat/context` | 15,032.425 us | 144,816.200 us | 147,608.700 us | 0.10x |
| `aws-keys/full` | 8,719.260 us | 84,675.500 us | 115,543.800 us | 0.10x |
| `ruff-noqa/real` | 4,521.017 us | 69,632.600 us | 85,058.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,085.277 us | 22,659.633 us | 27,788.900 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 215.946 us | 10,964.100 us | 11,842.480 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,518.606 us | 503.520 us | 8,852.267 us | 3.02x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.182 us | 0.107 us | 0.143 us | 1.70x |
| `common/match-word` | `Match` | 0.196 us | 0.161 us | 1.091 us | 1.21x |
| `industry/leipzig-twain-count` | `Count` | 1,229.740 us | 1,303.195 us | 8,376.667 us | 0.94x |
| `industry/mariomka-uri-count` | `Count` | 1,723.617 us | 1,875.875 us | 10,215.967 us | 0.92x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 235.075 us | 256.306 us | 488.244 us | 0.92x |
| `common/uri-miss` | `IsMatch` | 0.170 us | 0.190 us | 0.207 us | 0.90x |
| `common/replace-words` | `Replace` | 64.793 us | 74.876 us | 75.903 us | 0.87x |
| `common/uri-match` | `IsMatch` | 0.175 us | 0.232 us | 0.262 us | 0.75x |
| `common/ip-match` | `IsMatch` | 0.117 us | 0.174 us | 0.192 us | 0.68x |
| `common/email-match` | `IsMatch` | 0.164 us | 0.261 us | 0.293 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.082 us | 0.098 us | 0.63x |
| `common/ip-miss` | `IsMatch` | 0.111 us | 0.185 us | 0.202 us | 0.60x |
| `common/email-miss` | `IsMatch` | 0.173 us | 0.338 us | 0.362 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.164 us | 0.181 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 4,152.075 us | 9,722.583 us | 16,670.600 us | 0.43x |
| `common/matches-word` | `Count` | 1.380 us | 4.155 us | 5.845 us | 0.33x |
| `common/one-node-backtracking` | `IsMatch` | 0.199 us | 0.606 us | 0.641 us | 0.33x |
| `industry/mariomka-ip-count` | `Count` | 2,623.125 us | 8,125.429 us | 16,620.700 us | 0.32x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.107 us | 0.135 us | 0.27x |
| `common/backtracking` | `IsMatch` | 0.187 us | 0.811 us | 0.852 us | 0.23x |
| `common/split-words` | `Split` | 17.675 us | 77.489 us | 78.832 us | 0.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.082 us | 0.095 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 172.357 us | 955.526 us | 1,232.005 us | 0.18x |
| `industry/leipzig-river-window-count` | `Count` | 5,636.733 us | 31,788.650 us | 45,420.500 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.561 us | 0.591 us | 0.17x |
| `common/matches-words` | `Count` | 9.299 us | 68.991 us | 72.177 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.087 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,924.992 us | 19,267.900 us | 20,177.733 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,773.310 us | 22,323.633 us | 29,983.150 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 505.231 us | 7,854.529 us | 8,437.467 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.715 us | 159.373 us | 162.690 us | 0.05x |
| `common/matches-set` | `Count` | 7.553 us | 188.576 us | 190.381 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 260.376 us | 11,698.260 us | 12,197.600 us | 0.02x |
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
| `bounded-repeat/capitals` | 129,134.000 us | 13,426.650 us | 19,807.367 us | 9.62x |
| `literal-alternate/sherlock-zh` | 240.707 us | 36.037 us | 2,096.733 us | 6.68x |
| `literal/sherlock-zh` | 130.856 us | 23.636 us | 2,119.650 us | 5.54x |
| `literal/sherlock-ru` | 241.977 us | 88.044 us | 5,185.980 us | 2.75x |
| `ruff-noqa/tweaked` | 10,752.400 us | 4,485.192 us | 35,617.900 us | 2.40x |
| `literal/sherlock-casei-en` | 168.617 us | 77.617 us | 426.373 us | 2.17x |
| `aws-keys/quick` | 5,834.825 us | 3,668.021 us | 35,053.000 us | 1.59x |
| `literal-alternate/sherlock-casei-en` | 213.316 us | 148.752 us | 587.480 us | 1.43x |
| `literal/sherlock-casei-ru` | 243.946 us | 214.806 us | 5,287.050 us | 1.14x |
| `literal-alternate/sherlock-en` | 399.065 us | 364.935 us | 765.519 us | 1.09x |
| `literal-alternate/sherlock-en-mixed` | 198.086 us | 184.352 us | 532.960 us | 1.07x |
| `literal/sherlock-en` | 68.367 us | 71.374 us | 410.812 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,893.507 us | 2,128.525 us | 2,969.156 us | 0.89x |
| `literal-alternate/sherlock-en-nomatch` | 163.426 us | 200.103 us | 557.857 us | 0.82x |
| `literal-alternate/sherlock-ru` | 1,654.413 us | 2,323.877 us | 7,428.800 us | 0.71x |
| `bounded-repeat/letters-en` | 1,037.755 us | 1,797.921 us | 1,909.341 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 1,092.089 us | 1,936.552 us | 7,238.567 us | 0.56x |
| `aws-keys/full` | 9,636.980 us | 18,505.067 us | 50,532.400 us | 0.52x |
| `bounded-repeat/context` | 15,099.275 us | 68,680.500 us | 70,912.200 us | 0.22x |
| `ruff-noqa/real` | 4,516.300 us | 21,347.333 us | 34,001.000 us | 0.21x |
| `dictionary/single` | 7,992.114 us | 44,855.550 us | 44,901.550 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/backtracking` | `IsMatch` | 0.186 us | 0.043 us | 0.072 us | 4.31x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 235.138 us | 57.352 us | 264.131 us | 4.10x |
| `industry/leipzig-river-window-count` | `Count` | 5,773.933 us | 1,498.543 us | 13,824.900 us | 3.85x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.180 us | 0.048 us | 0.086 us | 3.77x |
| `common/replace-words` | `Replace` | 64.906 us | 18.735 us | 20.159 us | 3.46x |
| `industry/mariomka-email-count` | `Count` | 1,518.412 us | 470.872 us | 7,668.567 us | 3.22x |
| `common/ip-match` | `IsMatch` | 0.119 us | 0.048 us | 0.067 us | 2.49x |
| `common/uri-miss` | `IsMatch` | 0.165 us | 0.068 us | 0.085 us | 2.45x |
| `common/one-node-backtracking` | `IsMatch` | 0.200 us | 0.086 us | 0.125 us | 2.32x |
| `common/match-word` | `Match` | 0.195 us | 0.086 us | 0.980 us | 2.27x |
| `common/ip-miss` | `IsMatch` | 0.110 us | 0.049 us | 0.069 us | 2.23x |
| `common/email-match` | `IsMatch` | 0.164 us | 0.079 us | 0.105 us | 2.09x |
| `common/uri-match` | `IsMatch` | 0.175 us | 0.087 us | 0.118 us | 2.01x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.059 us | 0.030 us | 0.047 us | 1.96x |
| `common/email-miss` | `IsMatch` | 0.171 us | 0.088 us | 0.113 us | 1.95x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.030 us | 0.048 us | 1.68x |
| `industry/mariomka-uri-count` | `Count` | 1,727.533 us | 1,074.332 us | 8,366.700 us | 1.61x |
| `industry/leipzig-symbol-count` | `Count` | 1,810.941 us | 1,202.263 us | 6,758.267 us | 1.51x |
| `industry/leipzig-name-family-count` | `Count` | 4,254.775 us | 2,999.572 us | 8,380.700 us | 1.42x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.074 us | 0.055 us | 0.076 us | 1.34x |
| `industry/leipzig-twain-count` | `Count` | 1,237.578 us | 1,259.251 us | 7,037.943 us | 0.98x |
| `common/split-words` | `Split` | 17.659 us | 20.995 us | 22.550 us | 0.84x |
| `industry/mariomka-ip-count` | `Count` | 2,619.010 us | 3,395.700 us | 10,928.300 us | 0.77x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.072 us | 0.67x |
| `common/matches-word` | `Count` | 1.379 us | 2.323 us | 3.476 us | 0.59x |
| `common/matches-words` | `Count` | 9.251 us | 18.434 us | 20.988 us | 0.50x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.197 us | 0.230 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.034 us | 0.051 us | 0.31x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.233 us | 3,882.264 us | 4,275.233 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 173.167 us | 1,443.544 us | 1,788.938 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,967.808 us | 17,790.700 us | 18,738.367 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.702 us | 104.862 us | 105.003 us | 0.07x |
| `common/matches-set` | `Count` | 7.476 us | 104.183 us | 105.352 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 261.857 us | 4,724.955 us | 5,186.440 us | 0.06x |
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
| `lokad/imports/module-imports` | 30.312 us | 6.362 us | 28.956 us | 4.76x |
| `lokad/lexer/doc-line` | 4.246 us | 2.925 us | 3.497 us | 1.45x |
| `structural/keyword-to-generic-type` | 5.626 us | 3.970 us | 15.714 us | 1.42x |
| `lokad/lexer/identifier` | 30.082 us | 22.294 us | 25.783 us | 1.35x |
| `lokad/langserv/helper-identifier` | 9.383 us | 6.959 us | 7.803 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.670 us | 1.324 us | 1.479 us | 1.26x |
| `structural/ordered-keyword-pair` | 6.190 us | 5.293 us | 16.329 us | 1.17x |
| `lokad/lexer/operator-run` | 3.725 us | 3.227 us | 3.832 us | 1.15x |
| `fallback/lookahead` | 5.907 us | 5.943 us | 17.042 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.196 us | 12.039 us | 13.507 us | 0.93x |
| `lokad/style/hex-color` | 0.749 us | 0.807 us | 0.835 us | 0.93x |
| `lokad/docs/inline-doc-prefix` | 4.271 us | 4.846 us | 5.327 us | 0.88x |
| `literal-family/method-token-family` | 7.912 us | 9.066 us | 19.508 us | 0.87x |
| `lokad/lexer/number` | 10.276 us | 13.238 us | 14.584 us | 0.78x |
| `literal/call-token` | 3.027 us | 4.217 us | 15.635 us | 0.72x |
| `lokad/style/cell-ref` | 1.923 us | 2.706 us | 2.831 us | 0.71x |
| `lokad/langserv/color-rgb-hex` | 1.966 us | 2.862 us | 2.871 us | 0.69x |
| `structural/method-family-generic-call` | 4.310 us | 6.459 us | 17.515 us | 0.67x |
| `literal/identifier-token` | 2.697 us | 4.247 us | 15.397 us | 0.63x |
| `structural/method-family-call` | 4.691 us | 8.197 us | 18.738 us | 0.57x |
| `literal-family/type-token-family` | 11.867 us | 23.243 us | 34.232 us | 0.51x |
| `literal/identifier-token-casei` | 7.071 us | 15.031 us | 26.523 us | 0.47x |
| `lokad/style/range-ref` | 1.177 us | 2.666 us | 2.725 us | 0.44x |
| `structural/ordered-keyword-window` | 6.751 us | 17.481 us | 28.780 us | 0.39x |
| `structural/ordered-modifier-pair` | 5.994 us | 17.295 us | 28.250 us | 0.35x |
| `structural/keyword-family-to-capitalized-identifier` | 71.852 us | 210.219 us | 223.915 us | 0.34x |
| `lokad/lexer/string` | 28.717 us | 93.031 us | 99.192 us | 0.31x |
| `structural/modifier-family-to-type-window` | 77.097 us | 252.152 us | 266.828 us | 0.31x |
| `structural/ordered-identifier-window` | 6.404 us | 28.309 us | 39.208 us | 0.23x |
| `lokad/folding/region-marker` | 6.893 us | 35.148 us | 60.312 us | 0.20x |
| `structural/modifier-family-to-type-pair` | 24.632 us | 131.488 us | 145.315 us | 0.19x |
| `lokad/langserv/url-dashboard` | 2.748 us | 23.655 us | 24.239 us | 0.12x |
| `lokad/langserv/url-download` | 2.725 us | 52.011 us | 53.358 us | 0.05x |
| `balancing/begin-end-blocks` | 7.304 us | 4,275.625 us | 4,245.608 us | 0.0017x |
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
| `structural/keyword-family-to-capitalized-identifier` | 71.709 us | 7.566 us | 18.679 us | 9.48x |
| `lokad/imports/module-imports` | 30.197 us | 4.364 us | 26.394 us | 6.92x |
| `structural/modifier-family-to-type-pair` | 24.579 us | 10.579 us | 21.502 us | 2.32x |
| `lokad/langserv/helper-identifier` | 9.318 us | 4.707 us | 5.686 us | 1.98x |
| `lokad/lexer/operator-run` | 3.685 us | 1.881 us | 2.467 us | 1.96x |
| `lokad/lexer/doc-line` | 4.219 us | 2.166 us | 2.747 us | 1.95x |
| `lokad/lexer/identifier` | 30.285 us | 16.577 us | 20.385 us | 1.83x |
| `structural/keyword-to-generic-type` | 5.628 us | 3.444 us | 14.631 us | 1.63x |
| `lokad/langserv/color-short-hex` | 1.606 us | 1.017 us | 1.168 us | 1.58x |
| `lokad/docs/inline-doc-prefix` | 4.212 us | 2.697 us | 3.376 us | 1.56x |
| `structural/ordered-keyword-pair` | 6.132 us | 4.042 us | 14.783 us | 1.52x |
| `lokad/style/hex-color` | 0.749 us | 0.507 us | 0.571 us | 1.48x |
| `fallback/lookahead` | 6.010 us | 4.138 us | 14.948 us | 1.45x |
| `lokad/lexer/number` | 10.134 us | 7.024 us | 8.354 us | 1.44x |
| `structural/modifier-family-to-type-window` | 75.950 us | 55.684 us | 67.802 us | 1.36x |
| `lokad/langserv/identifier-validator` | 11.231 us | 8.639 us | 10.535 us | 1.30x |
| `literal/identifier-token-casei` | 6.951 us | 5.820 us | 16.917 us | 1.19x |
| `lokad/style/cell-ref` | 1.902 us | 1.665 us | 1.876 us | 1.14x |
| `lokad/langserv/color-rgb-hex` | 1.972 us | 1.747 us | 1.934 us | 1.13x |
| `literal-family/type-token-family` | 11.808 us | 11.151 us | 22.061 us | 1.06x |
| `structural/method-family-generic-call` | 4.280 us | 4.461 us | 14.734 us | 0.96x |
| `structural/method-family-call` | 4.703 us | 4.941 us | 15.230 us | 0.95x |
| `literal-family/method-token-family` | 4.655 us | 5.191 us | 15.669 us | 0.90x |
| `lokad/style/range-ref` | 1.173 us | 1.449 us | 1.510 us | 0.81x |
| `structural/ordered-keyword-window` | 6.705 us | 8.441 us | 19.382 us | 0.79x |
| `literal/call-token` | 3.021 us | 3.923 us | 14.764 us | 0.77x |
| `structural/ordered-modifier-pair` | 6.125 us | 8.493 us | 19.642 us | 0.72x |
| `literal/identifier-token` | 2.723 us | 4.211 us | 15.109 us | 0.65x |
| `lokad/lexer/string` | 28.320 us | 44.179 us | 47.093 us | 0.64x |
| `structural/ordered-identifier-window` | 6.376 us | 10.539 us | 21.183 us | 0.60x |
| `lokad/langserv/url-dashboard` | 2.757 us | 7.221 us | 7.733 us | 0.38x |
| `lokad/langserv/url-download` | 2.723 us | 12.682 us | 13.081 us | 0.21x |
| `lokad/folding/region-marker` | 6.942 us | 35.746 us | 58.868 us | 0.19x |
| `balancing/begin-end-blocks` | 7.299 us | 458.840 us | 487.118 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
