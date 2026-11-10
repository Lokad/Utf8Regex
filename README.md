# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.

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
| [Lokad.Utf8Regex.Posix](src/Lokad.Utf8Regex.Posix/README.md) | POSIX ERE subset with leftmost-longest selection and hierarchical capture priority; new in 0.5.0 |

Adapters require the matching Core package version. Their specifications
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

The tables were refreshed on **10 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **46m05s**. The [run receipt](bench/Experiments/DeliveryDev63-20261110/full-refresh.json)
records the scope and timings; package and consumer qualification are separate.
Each row retains its recorded implementation. Selective updates do not imply a
new complete run; the full-run receipt applies to its recorded revision.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

Profile comparisons are in the
[PythonRe](src/Lokad.Utf8Regex.PythonRe/README.md#benchmarks),
[PCRE2](src/Lokad.Utf8Regex.Pcre2/README.md#benchmarks) and
[POSIX](src/Lokad.Utf8Regex.Posix/README.md#benchmarks) README sections.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 82.804 us | 18.953 us | 2,181.380 us | 4.37x |
| `literal/sherlock-ru` | 171.720 us | 93.959 us | 5,202.473 us | 1.83x |
| `literal-alternate/sherlock-zh` | 104.626 us | 69.404 us | 2,200.606 us | 1.51x |
| `literal/sherlock-casei-en` | 164.189 us | 196.612 us | 602.697 us | 0.84x |
| `aws-keys/quick` | 6,639.983 us | 9,766.675 us | 43,453.050 us | 0.68x |
| `literal/sherlock-en` | 57.023 us | 86.082 us | 476.363 us | 0.66x |
| `literal/sherlock-casei-ru` | 246.874 us | 395.921 us | 5,495.164 us | 0.62x |
| `ruff-noqa/tweaked` | 4,492.141 us | 7,695.609 us | 43,657.350 us | 0.58x |
| `bounded-repeat/letters-en` | 1,116.625 us | 2,858.911 us | 2,974.077 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,670.440 us | 4,900.675 us | 10,308.100 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,983.874 us | 6,041.562 us | 6,819.275 us | 0.33x |
| `dictionary/single` | 8,321.810 us | 25,721.467 us | 25,822.233 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 199.925 us | 626.474 us | 1,040.713 us | 0.32x |
| `literal-alternate/sherlock-en` | 384.632 us | 1,496.824 us | 1,989.061 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 155.376 us | 653.793 us | 1,038.251 us | 0.24x |
| `bounded-repeat/capitals` | 9,457.788 us | 47,030.850 us | 50,423.750 us | 0.20x |
| `bounded-repeat/context` | 20,705.775 us | 149,173.900 us | 152,676.500 us | 0.14x |
| `aws-keys/full` | 10,630.625 us | 86,770.900 us | 116,346.400 us | 0.12x |
| `ruff-noqa/real` | 4,358.833 us | 74,187.700 us | 103,142.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 957.657 us | 23,519.000 us | 29,110.150 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 133.331 us | 10,995.843 us | 11,842.986 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,179.952 us | 489.297 us | 8,914.850 us | 2.41x |
| `common/match-word` | `Match` | 0.200 us | 0.169 us | 1.134 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,162.634 us | 1,326.800 us | 8,412.360 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,450.077 us | 1,878.876 us | 10,565.750 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.110 us | 0.147 us | 0.73x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.086 us | 0.104 us | 0.59x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.206 us | 0.229 us | 0.58x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.126 us | 257.211 us | 482.972 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.170 us | 0.188 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.183 us | 0.203 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,067.632 us | 9,966.625 us | 17,148.167 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.188 us | 0.207 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.237 us | 0.272 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.266 us | 0.289 us | 0.36x |
| `common/replace-words` | `Replace` | 28.452 us | 80.496 us | 82.643 us | 0.35x |
| `common/matches-word` | `Count` | 1.350 us | 4.282 us | 6.022 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.344 us | 0.371 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,222.147 us | 8,293.970 us | 16,801.633 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.111 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.331 us | 966.462 us | 1,241.294 us | 0.18x |
| `common/split-words` | `Split` | 14.637 us | 79.374 us | 81.016 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.564 us | 0.602 us | 0.16x |
| `common/matches-words` | `Count` | 9.416 us | 70.267 us | 71.860 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.611 us | 0.642 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.078 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.093 us | 0.840 us | 0.881 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,464.414 us | 32,599.300 us | 39,807.450 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,960.062 us | 19,704.900 us | 20,246.950 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,831.798 us | 22,881.650 us | 30,257.100 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 499.557 us | 8,769.111 us | 9,235.013 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.849 us | 165.389 us | 166.855 us | 0.05x |
| `common/matches-set` | `Count` | 7.826 us | 195.734 us | 197.651 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.182 us | 11,802.800 us | 12,455.129 us | 0.02x |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 82.539 us | 23.906 us | 2,183.049 us | 3.45x |
| `literal-alternate/sherlock-zh` | 104.654 us | 36.685 us | 2,175.253 us | 2.85x |
| `literal/sherlock-casei-en` | 163.057 us | 80.195 us | 445.654 us | 2.03x |
| `literal/sherlock-ru` | 171.651 us | 87.217 us | 5,163.980 us | 1.97x |
| `aws-keys/quick` | 6,641.425 us | 3,700.310 us | 35,388.100 us | 1.79x |
| `literal-alternate/sherlock-en-mixed` | 199.277 us | 176.824 us | 560.482 us | 1.13x |
| `literal-alternate/sherlock-en` | 385.001 us | 341.927 us | 739.044 us | 1.13x |
| `literal/sherlock-casei-ru` | 248.073 us | 222.883 us | 5,365.900 us | 1.11x |
| `ruff-noqa/tweaked` | 4,552.212 us | 4,596.224 us | 38,365.050 us | 0.99x |
| `bounded-repeat/letters-ru` | 1,979.385 us | 2,083.711 us | 2,877.115 us | 0.95x |
| `literal-alternate/sherlock-casei-en` | 134.057 us | 145.225 us | 522.533 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 155.731 us | 200.983 us | 540.467 us | 0.77x |
| `literal/sherlock-en` | 57.184 us | 75.458 us | 427.922 us | 0.76x |
| `bounded-repeat/capitals` | 9,494.788 us | 13,720.417 us | 16,501.950 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,622.768 us | 2,359.562 us | 7,547.100 us | 0.69x |
| `bounded-repeat/letters-en` | 1,116.965 us | 1,852.724 us | 1,934.423 us | 0.60x |
| `aws-keys/full` | 10,692.950 us | 18,613.440 us | 50,997.667 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 958.830 us | 1,977.895 us | 7,228.825 us | 0.48x |
| `bounded-repeat/context` | 21,119.350 us | 70,316.900 us | 73,327.100 us | 0.30x |
| `ruff-noqa/real` | 4,367.739 us | 20,979.075 us | 52,382.967 us | 0.21x |
| `dictionary/single` | 8,337.000 us | 46,201.000 us | 46,150.800 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.199 us | 0.075 us | 1.019 us | 2.65x |
| `industry/mariomka-email-count` | `Count` | 1,179.869 us | 457.066 us | 7,682.750 us | 2.58x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 133.577 us | 58.799 us | 266.033 us | 2.27x |
| `industry/leipzig-river-window-count` | `Count` | 3,468.991 us | 1,631.763 us | 7,822.175 us | 2.13x |
| `common/backtracking` | `IsMatch` | 0.092 us | 0.044 us | 0.074 us | 2.08x |
| `common/uri-miss` | `IsMatch` | 0.117 us | 0.067 us | 0.089 us | 1.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.051 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.087 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.030 us | 0.047 us | 1.63x |
| `common/replace-words` | `Replace` | 28.651 us | 18.010 us | 20.908 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.049 us | 0.070 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.050 us | 0.071 us | 1.50x |
| `industry/leipzig-symbol-count` | `Count` | 1,836.615 us | 1,230.136 us | 6,723.800 us | 1.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.35x |
| `industry/leipzig-name-family-count` | `Count` | 4,071.767 us | 3,044.688 us | 8,074.975 us | 1.34x |
| `industry/mariomka-uri-count` | `Count` | 1,446.096 us | 1,122.820 us | 8,451.425 us | 1.29x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.108 us | 1.16x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.115 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.090 us | 0.120 us | 1.02x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.088 us | 0.128 us | 0.91x |
| `industry/leipzig-twain-count` | `Count` | 1,163.302 us | 1,293.463 us | 7,106.167 us | 0.90x |
| `common/matches-words` | `Count` | 9.454 us | 10.811 us | 13.913 us | 0.87x |
| `common/split-words` | `Split` | 14.678 us | 21.532 us | 23.302 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,225.462 us | 3,521.200 us | 10,981.100 us | 0.63x |
| `common/matches-word` | `Count` | 1.352 us | 2.378 us | 3.523 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.198 us | 0.237 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.037 us | 0.053 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,964.951 us | 8,932.278 us | 9,452.525 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 498.766 us | 3,964.079 us | 4,329.589 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.765 us | 1,482.869 us | 1,785.107 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.841 us | 106.530 us | 108.844 us | 0.07x |
| `common/matches-set` | `Count` | 7.818 us | 106.577 us | 108.319 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.417 us | 4,799.887 us | 5,218.333 us | 0.05x |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 15.587 us | 6.521 us | 29.417 us | 2.39x |
| `structural/keyword-to-generic-type` | 5.584 us | 4.116 us | 15.756 us | 1.36x |
| `lokad/langserv/color-short-hex` | 1.675 us | 1.374 us | 1.445 us | 1.22x |
| `lokad/lexer/doc-line` | 3.365 us | 3.082 us | 3.588 us | 1.09x |
| `lokad/lexer/identifier` | 23.836 us | 23.137 us | 25.365 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.362 us | 5.421 us | 16.881 us | 0.99x |
| `lokad/langserv/helper-identifier` | 7.235 us | 7.586 us | 7.978 us | 0.95x |
| `fallback/lookahead` | 5.635 us | 6.068 us | 17.130 us | 0.93x |
| `lokad/style/hex-color` | 0.744 us | 0.816 us | 0.866 us | 0.91x |
| `lokad/langserv/identifier-validator` | 11.418 us | 12.600 us | 13.974 us | 0.91x |
| `lokad/lexer/operator-run` | 2.961 us | 3.514 us | 3.759 us | 0.84x |
| `literal-family/method-token-family` | 7.168 us | 9.234 us | 19.828 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 2.052 us | 2.960 us | 3.045 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.356 us | 4.902 us | 5.419 us | 0.68x |
| `literal/identifier-token` | 2.791 us | 4.375 us | 15.636 us | 0.64x |
| `literal/call-token` | 2.604 us | 4.321 us | 15.522 us | 0.60x |
| `lokad/lexer/number` | 8.342 us | 14.467 us | 15.117 us | 0.58x |
| `structural/ordered-identifier-window` | 16.257 us | 28.818 us | 39.773 us | 0.56x |
| `structural/method-family-generic-call` | 3.466 us | 6.724 us | 18.295 us | 0.52x |
| `lokad/style/cell-ref` | 1.502 us | 2.966 us | 2.963 us | 0.51x |
| `structural/method-family-call` | 3.883 us | 8.284 us | 19.091 us | 0.47x |
| `literal-family/type-token-family` | 10.572 us | 23.537 us | 35.217 us | 0.45x |
| `literal/identifier-token-casei` | 5.762 us | 15.124 us | 26.420 us | 0.38x |
| `lokad/style/range-ref` | 0.915 us | 2.786 us | 2.834 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.450 us | 17.276 us | 28.257 us | 0.32x |
| `structural/modifier-family-to-type-window` | 79.172 us | 257.926 us | 270.165 us | 0.31x |
| `structural/ordered-keyword-window` | 5.462 us | 17.840 us | 28.844 us | 0.31x |
| `lokad/lexer/string` | 21.721 us | 95.881 us | 100.307 us | 0.23x |
| `lokad/folding/region-marker` | 6.033 us | 36.651 us | 60.663 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.552 us | 24.525 us | 25.059 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.467 us | 220.188 us | 232.172 us | 0.05x |
| `lokad/langserv/url-download` | 2.617 us | 52.645 us | 52.804 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.015 us | 136.088 us | 147.608 us | 0.04x |
| `balancing/begin-end-blocks` | 5.789 us | 4,349.000 us | 4,347.383 us | 0.0013x |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in [the Core summary](bench/Snapshots/Core.json) and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians. [The compact summary](bench/Snapshots/Core.json) retains qualification verdicts and recorded snapshot hashes; the full parity report is generated from the local raw snapshot.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 15.595 us | 4.452 us | 26.390 us | 3.50x |
| `lokad/langserv/color-short-hex` | 1.644 us | 0.975 us | 1.174 us | 1.69x |
| `structural/keyword-to-generic-type` | 5.570 us | 3.538 us | 14.680 us | 1.57x |
| `structural/ordered-identifier-window` | 16.308 us | 10.670 us | 21.480 us | 1.53x |
| `lokad/lexer/doc-line` | 3.359 us | 2.213 us | 2.967 us | 1.52x |
| `lokad/lexer/operator-run` | 2.954 us | 1.954 us | 2.516 us | 1.51x |
| `lokad/langserv/helper-identifier` | 7.225 us | 4.842 us | 5.884 us | 1.49x |
| `lokad/lexer/identifier` | 23.873 us | 16.116 us | 20.485 us | 1.48x |
| `structural/keyword-family-to-capitalized-identifier` | 11.482 us | 7.915 us | 18.506 us | 1.45x |
| `lokad/style/hex-color` | 0.745 us | 0.515 us | 0.605 us | 1.45x |
| `structural/modifier-family-to-type-window` | 79.255 us | 56.316 us | 68.658 us | 1.41x |
| `fallback/lookahead` | 5.638 us | 4.243 us | 14.927 us | 1.33x |
| `structural/ordered-keyword-pair` | 5.384 us | 4.176 us | 14.980 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.417 us | 8.934 us | 11.161 us | 1.28x |
| `lokad/docs/inline-doc-prefix` | 3.363 us | 2.749 us | 3.563 us | 1.22x |
| `lokad/lexer/number` | 8.324 us | 7.243 us | 8.339 us | 1.15x |
| `lokad/langserv/color-rgb-hex` | 2.040 us | 1.884 us | 2.059 us | 1.08x |
| `literal/identifier-token-casei` | 5.781 us | 5.988 us | 17.183 us | 0.97x |
| `literal-family/type-token-family` | 10.559 us | 11.575 us | 22.600 us | 0.91x |
| `lokad/style/cell-ref` | 1.501 us | 1.711 us | 1.900 us | 0.88x |
| `structural/method-family-generic-call` | 3.471 us | 4.525 us | 15.460 us | 0.77x |
| `structural/method-family-call` | 3.889 us | 5.135 us | 15.737 us | 0.76x |
| `literal-family/method-token-family` | 3.905 us | 5.336 us | 15.744 us | 0.73x |
| `literal/call-token` | 2.600 us | 4.007 us | 14.630 us | 0.65x |
| `structural/ordered-keyword-window` | 5.471 us | 8.645 us | 19.383 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.460 us | 8.670 us | 19.512 us | 0.63x |
| `literal/identifier-token` | 2.801 us | 4.728 us | 15.330 us | 0.59x |
| `lokad/style/range-ref` | 0.916 us | 1.558 us | 1.647 us | 0.59x |
| `lokad/lexer/string` | 21.722 us | 46.151 us | 48.774 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 5.027 us | 10.812 us | 21.573 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.572 us | 7.534 us | 8.050 us | 0.34x |
| `lokad/langserv/url-download` | 2.616 us | 13.070 us | 13.568 us | 0.20x |
| `lokad/folding/region-marker` | 6.043 us | 36.757 us | 60.521 us | 0.16x |
| `balancing/begin-end-blocks` | 5.753 us | 468.018 us | 497.473 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->

<details>
<summary>Regenerating benchmark tables</summary>

The compact summaries in `bench/Snapshots/` regenerate the README tables offline
through the existing `--rewrite-*-benchmark-markdown` commands. Ordinary tests need
no raw measurements or downloads. Regeneration preserves recorded observations;
it does not qualify the current engine or create a new benchmark run.

Refreshes and full evidence checks use local raw snapshots under
`artifacts/benchmark-refresh/current/`. `--export-benchmark-summaries` updates the
compact summaries from those files; `--rewrite-readme-parity-report` generates the
expanded Core report there on demand. Use `tools/bench.ps1`, build once and reuse
matching binaries with `-SkipBuild`. Keep or share raw outputs when useful; there
is no required archive service or upload step.

</details>
