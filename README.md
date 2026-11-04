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
and resource checks passed in **44m24s**. The [run receipt](bench/Experiments/DeliveryDev28-20261104/full-refresh.json)
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
| `literal/sherlock-zh` | 92.967 us | 18.625 us | 2,171.446 us | 4.99x |
| `literal-alternate/sherlock-zh` | 149.483 us | 67.515 us | 2,176.896 us | 2.21x |
| `literal/sherlock-ru` | 197.724 us | 101.293 us | 5,237.380 us | 1.95x |
| `ruff-noqa/tweaked` | 9,994.320 us | 7,927.043 us | 42,401.650 us | 1.26x |
| `literal/sherlock-casei-en` | 162.728 us | 191.386 us | 574.698 us | 0.85x |
| `literal/sherlock-casei-ru` | 251.878 us | 358.899 us | 5,549.011 us | 0.70x |
| `literal/sherlock-en` | 55.781 us | 84.107 us | 466.993 us | 0.66x |
| `aws-keys/quick` | 4,743.336 us | 9,689.940 us | 42,061.850 us | 0.49x |
| `bounded-repeat/letters-en` | 1,019.839 us | 2,786.811 us | 2,905.572 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,636.158 us | 4,686.518 us | 9,766.500 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 194.881 us | 580.988 us | 1,043.677 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,910.662 us | 5,823.589 us | 6,596.875 us | 0.33x |
| `dictionary/single` | 7,988.943 us | 24,978.150 us | 24,819.133 us | 0.32x |
| `literal-alternate/sherlock-en` | 389.392 us | 1,481.206 us | 2,056.244 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 149.782 us | 649.400 us | 1,092.732 us | 0.23x |
| `bounded-repeat/capitals` | 9,303.033 us | 45,698.650 us | 52,006.400 us | 0.20x |
| `aws-keys/full` | 10,586.100 us | 84,449.800 us | 114,031.900 us | 0.13x |
| `bounded-repeat/context` | 15,801.325 us | 145,853.400 us | 147,603.000 us | 0.11x |
| `ruff-noqa/real` | 4,320.458 us | 71,643.000 us | 101,646.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,017.790 us | 22,487.900 us | 27,651.150 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 165.467 us | 10,965.980 us | 11,768.580 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,209.702 us | 482.451 us | 8,796.500 us | 2.51x |
| `common/match-word` | `Match` | 0.196 us | 0.159 us | 1.138 us | 1.24x |
| `industry/leipzig-twain-count` | `Count` | 1,145.220 us | 1,298.329 us | 8,415.833 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,457.000 us | 1,834.732 us | 10,245.967 us | 0.79x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.112 us | 0.144 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.133 us | 0.191 us | 0.208 us | 0.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.100 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.178 us | 260.110 us | 504.141 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.159 us | 0.176 us | 0.48x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.180 us | 0.199 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,048.538 us | 9,703.367 us | 17,240.050 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.183 us | 0.203 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.227 us | 0.262 us | 0.40x |
| `common/replace-words` | `Replace` | 27.297 us | 74.028 us | 76.359 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.265 us | 0.284 us | 0.35x |
| `common/matches-word` | `Count` | 1.324 us | 4.164 us | 5.901 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.341 us | 0.363 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,297.927 us | 8,343.367 us | 16,512.050 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.108 us | 0.137 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.096 us | 0.21x |
| `common/split-words` | `Split` | 14.370 us | 77.492 us | 78.845 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.597 us | 945.330 us | 1,245.724 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.551 us | 0.585 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.101 us | 0.589 us | 0.621 us | 0.17x |
| `common/matches-words` | `Count` | 9.146 us | 68.626 us | 70.364 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.089 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.829 us | 0.873 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,350.631 us | 31,961.350 us | 45,718.400 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,874.596 us | 19,261.350 us | 20,206.200 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,837.479 us | 22,625.200 us | 30,224.700 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 504.008 us | 7,976.100 us | 8,497.583 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.610 us | 161.200 us | 163.175 us | 0.05x |
| `common/matches-set` | `Count` | 6.828 us | 194.261 us | 193.428 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 240.829 us | 12,016.240 us | 12,613.025 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 147.884 us | 36.142 us | 2,172.138 us | 4.09x |
| `literal/sherlock-zh` | 93.118 us | 23.616 us | 2,110.279 us | 3.94x |
| `ruff-noqa/tweaked` | 10,343.680 us | 4,549.300 us | 36,943.050 us | 2.27x |
| `literal/sherlock-ru` | 199.151 us | 89.467 us | 5,279.070 us | 2.23x |
| `literal/sherlock-casei-en` | 162.746 us | 76.889 us | 443.643 us | 2.12x |
| `aws-keys/quick` | 4,757.773 us | 3,497.287 us | 33,619.650 us | 1.36x |
| `literal/sherlock-casei-ru` | 254.175 us | 193.861 us | 5,239.200 us | 1.31x |
| `literal-alternate/sherlock-casei-en` | 165.789 us | 141.524 us | 553.048 us | 1.17x |
| `literal-alternate/sherlock-en-mixed` | 195.520 us | 174.567 us | 544.374 us | 1.12x |
| `literal-alternate/sherlock-en` | 388.192 us | 363.628 us | 777.946 us | 1.07x |
| `bounded-repeat/letters-ru` | 1,919.885 us | 2,029.220 us | 2,806.339 us | 0.95x |
| `literal-alternate/sherlock-en-nomatch` | 150.666 us | 194.848 us | 576.363 us | 0.77x |
| `literal/sherlock-en` | 55.917 us | 73.836 us | 474.912 us | 0.76x |
| `bounded-repeat/capitals` | 9,346.817 us | 13,315.300 us | 19,686.800 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,592.366 us | 2,283.362 us | 7,297.867 us | 0.70x |
| `aws-keys/full` | 10,851.520 us | 18,698.933 us | 48,884.800 us | 0.58x |
| `bounded-repeat/letters-en` | 1,017.431 us | 1,820.161 us | 1,913.374 us | 0.56x |
| `literal-alternate/sherlock-casei-ru` | 1,009.466 us | 1,912.948 us | 7,143.300 us | 0.53x |
| `bounded-repeat/context` | 15,934.375 us | 67,908.400 us | 70,319.700 us | 0.23x |
| `ruff-noqa/real` | 4,355.942 us | 20,307.800 us | 52,254.700 us | 0.21x |
| `dictionary/single` | 7,891.157 us | 44,557.550 us | 44,549.700 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,202.593 us | 446.401 us | 7,622.967 us | 2.69x |
| `common/match-word` | `Match` | 0.194 us | 0.074 us | 0.986 us | 2.62x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.209 us | 57.059 us | 266.145 us | 2.32x |
| `common/backtracking` | `IsMatch` | 0.094 us | 0.043 us | 0.073 us | 2.19x |
| `industry/leipzig-river-window-count` | `Count` | 3,360.887 us | 1,580.488 us | 14,033.900 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.134 us | 0.067 us | 0.086 us | 1.99x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.048 us | 0.029 us | 0.047 us | 1.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.031 us | 0.048 us | 1.64x |
| `common/replace-words` | `Replace` | 27.399 us | 16.835 us | 19.934 us | 1.63x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.047 us | 0.067 us | 1.60x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.049 us | 0.070 us | 1.51x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.052 us | 0.086 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,863.468 us | 1,268.680 us | 6,988.500 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.055 us | 0.076 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 4,186.554 us | 3,037.406 us | 8,484.333 us | 1.38x |
| `industry/mariomka-uri-count` | `Count` | 1,446.237 us | 1,078.070 us | 8,518.700 us | 1.34x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.079 us | 0.106 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.087 us | 0.112 us | 1.14x |
| `common/one-node-backtracking` | `IsMatch` | 0.097 us | 0.085 us | 0.123 us | 1.14x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.088 us | 0.118 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,150.841 us | 1,287.908 us | 7,078.214 us | 0.89x |
| `common/matches-words` | `Count` | 9.033 us | 10.684 us | 13.672 us | 0.85x |
| `common/split-words` | `Split` | 14.400 us | 20.886 us | 23.222 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,281.609 us | 3,378.933 us | 10,933.150 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.044 us | 0.073 us | 0.65x |
| `common/matches-word` | `Count` | 1.305 us | 2.284 us | 3.538 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.197 us | 0.229 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.053 us | 0.27x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 504.650 us | 3,914.762 us | 4,311.775 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.390 us | 1,460.203 us | 1,828.990 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,882.296 us | 17,594.700 us | 18,528.333 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.622 us | 103.443 us | 105.185 us | 0.07x |
| `common/matches-set` | `Count` | 6.828 us | 102.104 us | 103.841 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.293 us | 4,685.882 us | 5,162.910 us | 0.05x |
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
| `lokad/imports/module-imports` | 12.656 us | 6.294 us | 28.518 us | 2.01x |
| `structural/keyword-to-generic-type` | 5.315 us | 3.956 us | 15.414 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.631 us | 1.342 us | 1.482 us | 1.22x |
| `lokad/lexer/identifier` | 24.665 us | 22.094 us | 25.019 us | 1.12x |
| `lokad/lexer/doc-line` | 3.285 us | 2.960 us | 3.520 us | 1.11x |
| `structural/ordered-keyword-pair` | 5.270 us | 5.276 us | 16.459 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.082 us | 7.091 us | 7.853 us | 1.00x |
| `lokad/style/hex-color` | 0.738 us | 0.812 us | 0.865 us | 0.91x |
| `lokad/langserv/identifier-validator` | 11.162 us | 12.414 us | 13.575 us | 0.90x |
| `lokad/lexer/operator-run` | 2.878 us | 3.274 us | 3.835 us | 0.88x |
| `fallback/lookahead` | 5.368 us | 6.189 us | 17.138 us | 0.87x |
| `literal-family/method-token-family` | 7.069 us | 8.954 us | 19.550 us | 0.79x |
| `lokad/docs/inline-doc-prefix` | 3.306 us | 4.644 us | 5.481 us | 0.71x |
| `lokad/langserv/color-rgb-hex` | 2.020 us | 2.845 us | 2.954 us | 0.71x |
| `literal/identifier-token` | 2.753 us | 4.312 us | 15.396 us | 0.64x |
| `lokad/lexer/number` | 8.613 us | 13.768 us | 14.702 us | 0.63x |
| `literal/call-token` | 2.522 us | 4.218 us | 15.251 us | 0.60x |
| `structural/ordered-identifier-window` | 15.850 us | 28.292 us | 39.199 us | 0.56x |
| `lokad/style/cell-ref` | 1.547 us | 2.917 us | 2.942 us | 0.53x |
| `structural/method-family-generic-call` | 3.402 us | 6.609 us | 17.930 us | 0.51x |
| `structural/method-family-call` | 3.785 us | 8.199 us | 18.733 us | 0.46x |
| `literal-family/type-token-family` | 10.287 us | 22.866 us | 34.217 us | 0.45x |
| `literal/identifier-token-casei` | 5.953 us | 15.360 us | 26.291 us | 0.39x |
| `lokad/style/range-ref` | 0.892 us | 2.666 us | 2.674 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.560 us | 17.614 us | 28.617 us | 0.32x |
| `structural/ordered-keyword-window` | 5.319 us | 17.338 us | 29.060 us | 0.31x |
| `structural/modifier-family-to-type-window` | 78.241 us | 255.745 us | 267.495 us | 0.31x |
| `lokad/lexer/string` | 22.009 us | 93.417 us | 97.393 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 23.010 us | 126.619 us | 139.167 us | 0.18x |
| `lokad/folding/region-marker` | 6.359 us | 35.867 us | 61.676 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.506 us | 23.911 us | 24.320 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 18.518 us | 220.165 us | 230.371 us | 0.08x |
| `lokad/langserv/url-download` | 2.520 us | 50.488 us | 50.807 us | 0.05x |
| `balancing/begin-end-blocks` | 5.545 us | 4,249.450 us | 4,280.858 us | 0.0013x |
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
| `lokad/imports/module-imports` | 12.770 us | 4.329 us | 26.043 us | 2.95x |
| `structural/keyword-family-to-capitalized-identifier` | 18.535 us | 7.549 us | 18.911 us | 2.46x |
| `structural/modifier-family-to-type-pair` | 23.079 us | 10.545 us | 21.864 us | 2.19x |
| `lokad/langserv/color-short-hex` | 1.641 us | 0.966 us | 1.206 us | 1.70x |
| `structural/keyword-to-generic-type` | 5.335 us | 3.366 us | 14.400 us | 1.59x |
| `lokad/langserv/helper-identifier` | 7.062 us | 4.620 us | 5.785 us | 1.53x |
| `lokad/lexer/doc-line` | 3.276 us | 2.167 us | 2.893 us | 1.51x |
| `structural/ordered-identifier-window` | 15.821 us | 10.660 us | 21.158 us | 1.48x |
| `lokad/lexer/identifier` | 24.567 us | 16.726 us | 20.041 us | 1.47x |
| `lokad/lexer/operator-run` | 2.865 us | 1.984 us | 2.457 us | 1.44x |
| `structural/modifier-family-to-type-window` | 78.419 us | 54.806 us | 66.789 us | 1.43x |
| `lokad/style/hex-color` | 0.730 us | 0.528 us | 0.606 us | 1.38x |
| `fallback/lookahead` | 5.396 us | 4.156 us | 14.756 us | 1.30x |
| `structural/ordered-keyword-pair` | 5.191 us | 4.058 us | 14.683 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.236 us | 8.877 us | 11.105 us | 1.27x |
| `lokad/lexer/number` | 8.628 us | 7.139 us | 8.212 us | 1.21x |
| `lokad/docs/inline-doc-prefix` | 3.292 us | 2.770 us | 3.620 us | 1.19x |
| `lokad/langserv/color-rgb-hex` | 2.002 us | 1.852 us | 2.012 us | 1.08x |
| `literal/identifier-token-casei` | 5.981 us | 5.811 us | 17.029 us | 1.03x |
| `literal-family/type-token-family` | 10.251 us | 11.265 us | 21.936 us | 0.91x |
| `lokad/style/cell-ref` | 1.548 us | 1.804 us | 1.845 us | 0.86x |
| `structural/method-family-call` | 3.751 us | 4.922 us | 15.123 us | 0.76x |
| `structural/method-family-generic-call` | 3.404 us | 4.530 us | 15.078 us | 0.75x |
| `literal-family/method-token-family` | 3.837 us | 5.181 us | 15.584 us | 0.74x |
| `literal/call-token` | 2.530 us | 3.873 us | 14.437 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.559 us | 8.624 us | 19.635 us | 0.64x |
| `structural/ordered-keyword-window` | 5.315 us | 8.570 us | 19.618 us | 0.62x |
| `literal/identifier-token` | 2.765 us | 4.654 us | 14.887 us | 0.59x |
| `lokad/style/range-ref` | 0.887 us | 1.573 us | 1.594 us | 0.56x |
| `lokad/lexer/string` | 22.202 us | 44.670 us | 47.494 us | 0.50x |
| `lokad/langserv/url-dashboard` | 2.522 us | 7.269 us | 7.821 us | 0.35x |
| `lokad/langserv/url-download` | 2.535 us | 12.821 us | 13.379 us | 0.20x |
| `lokad/folding/region-marker` | 6.418 us | 36.257 us | 59.631 us | 0.18x |
| `balancing/begin-end-blocks` | 5.558 us | 461.786 us | 489.794 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
