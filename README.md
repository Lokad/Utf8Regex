# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes 0.5.0 development source. Published 0.4.0 and frozen
development candidates retain the scope recorded in their release evidence.

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
The default Validate policy checks this; AssumeValid makes it a caller guarantee.
Byte coordinates are primary; UTF-16 coordinates and strings are derived
conveniences. Keep borrowed input unchanged and its owner alive while a view or
cursor uses it. Owned snapshots remain valid after cursor advance or disposal.

## Subject validation policy

Choose once when constructing an engine or through its factory:

```csharp
var regex = new Utf8Regex("needle", inputValidation: Utf8InputValidation.AssumeValid);
// regex.InputValidation is read-only.
```

`Validate` (default) checks the whole subject at most once per operation or cursor
lifetime. `AssumeValid` omits that pass when the caller guarantees valid UTF-8 and
unchanged borrowed bytes. All four packages inherit the choice through matching,
counting, output, splitting and cursors where supported. Pattern/replacement
validation, checked memory access, scalar semantics and cooperative cancellation
remain. Coordinate/result work may still scan input. Read the
[contract](docs/SPEC.md#subject-input-validation) before choosing AssumeValid.
Representative [policy measurements](bench/Experiments/InputValidation-20261110/README.md)
report both input contracts separately, including finite-limit and output costs.

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
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m05s**. The [run receipt](bench/Experiments/DeliveryDev67-20261110/full-refresh.json)
records the scope and timings; package and consumer qualification are separate.
Each row retains its recorded implementation. Selective updates do not imply a
new complete run; the full-run receipt applies to its recorded revision.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

Focused Core counts accept a final validation policy, for example
`--measure-readme-public-case industry/mariomka-email-count 1 9 AssumeValid`.
These measurements label their input contract and leave recorded tables unchanged.

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
| `literal/sherlock-zh` | 80.748 us | 18.414 us | 2,204.182 us | 4.39x |
| `literal/sherlock-ru` | 166.332 us | 96.815 us | 5,137.993 us | 1.72x |
| `literal-alternate/sherlock-zh` | 105.065 us | 68.488 us | 2,169.809 us | 1.53x |
| `literal/sherlock-casei-en` | 157.374 us | 187.956 us | 615.255 us | 0.84x |
| `literal/sherlock-casei-ru` | 253.066 us | 365.858 us | 5,446.757 us | 0.69x |
| `literal/sherlock-en` | 56.546 us | 86.687 us | 471.932 us | 0.65x |
| `aws-keys/quick` | 6,237.208 us | 9,570.138 us | 44,676.050 us | 0.65x |
| `ruff-noqa/tweaked` | 4,337.812 us | 7,598.518 us | 42,135.350 us | 0.57x |
| `bounded-repeat/letters-en` | 1,122.383 us | 2,769.633 us | 2,891.954 us | 0.41x |
| `literal-alternate/sherlock-en-mixed` | 192.388 us | 585.850 us | 1,027.958 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,914.065 us | 5,876.631 us | 6,780.458 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,503.894 us | 4,652.350 us | 9,724.375 us | 0.32x |
| `dictionary/single` | 8,201.889 us | 25,768.100 us | 25,809.267 us | 0.32x |
| `literal-alternate/sherlock-en` | 376.695 us | 1,483.053 us | 2,025.280 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 150.667 us | 644.010 us | 1,063.792 us | 0.23x |
| `bounded-repeat/capitals` | 9,364.525 us | 46,131.450 us | 49,923.500 us | 0.20x |
| `bounded-repeat/context` | 20,418.200 us | 146,149.800 us | 150,843.600 us | 0.14x |
| `aws-keys/full` | 10,552.125 us | 86,347.200 us | 118,343.200 us | 0.12x |
| `ruff-noqa/real` | 4,567.359 us | 69,165.550 us | 100,950.700 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 938.616 us | 22,833.550 us | 28,157.200 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 132.166 us | 11,456.343 us | 12,148.471 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,177.883 us | 498.105 us | 8,899.650 us | 2.36x |
| `common/match-word` | `Match` | 0.191 us | 0.166 us | 1.098 us | 1.16x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.103 us | 0.108 us | 0.145 us | 0.96x |
| `industry/leipzig-twain-count` | `Count` | 1,153.361 us | 1,300.250 us | 8,525.500 us | 0.89x |
| `industry/mariomka-uri-count` | `Count` | 1,390.744 us | 1,850.895 us | 9,967.275 us | 0.75x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.191 us | 0.212 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.083 us | 0.099 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.086 us | 0.162 us | 0.180 us | 0.53x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 116.919 us | 258.396 us | 514.944 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.177 us | 0.195 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.183 us | 0.202 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.097 us | 0.232 us | 0.270 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 3,786.830 us | 9,649.367 us | 16,510.133 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.271 us | 0.295 us | 0.35x |
| `common/replace-words` | `Replace` | 27.512 us | 79.557 us | 81.757 us | 0.35x |
| `common/matches-word` | `Count` | 1.329 us | 4.155 us | 5.780 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.343 us | 0.360 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,250.815 us | 8,310.640 us | 16,602.833 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.108 us | 0.137 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.082 us | 0.098 us | 0.20x |
| `common/split-words` | `Split` | 14.277 us | 76.131 us | 77.144 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.438 us | 966.300 us | 1,262.763 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.552 us | 0.584 us | 0.18x |
| `common/matches-words` | `Count` | 9.252 us | 69.070 us | 70.580 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.089 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.829 us | 0.875 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,885.045 us | 19,334.950 us | 20,143.600 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,835.983 us | 22,821.500 us | 29,732.450 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.999 us | 1.029 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,096.844 us | 31,325.533 us | 38,462.950 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 506.546 us | 8,416.211 us | 8,948.067 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.837 us | 162.863 us | 163.499 us | 0.04x |
| `common/matches-set` | `Count` | 7.614 us | 191.351 us | 192.270 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 231.584 us | 11,729.257 us | 12,453.900 us | 0.02x |
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
| `literal/sherlock-zh` | 80.792 us | 23.562 us | 2,165.651 us | 3.43x |
| `literal-alternate/sherlock-zh` | 106.295 us | 35.519 us | 2,169.017 us | 2.99x |
| `literal/sherlock-casei-en` | 157.877 us | 79.651 us | 460.850 us | 1.98x |
| `literal/sherlock-ru` | 165.454 us | 88.542 us | 5,103.047 us | 1.87x |
| `aws-keys/quick` | 5,841.663 us | 3,645.705 us | 36,406.200 us | 1.60x |
| `literal/sherlock-casei-ru` | 249.898 us | 221.858 us | 5,226.836 us | 1.13x |
| `literal-alternate/sherlock-en-mixed` | 191.176 us | 175.723 us | 538.100 us | 1.09x |
| `literal-alternate/sherlock-en` | 377.331 us | 367.647 us | 776.255 us | 1.03x |
| `ruff-noqa/tweaked` | 4,404.435 us | 4,544.583 us | 36,777.300 us | 0.97x |
| `bounded-repeat/letters-ru` | 1,931.168 us | 2,021.189 us | 2,884.915 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 132.017 us | 142.751 us | 602.787 us | 0.92x |
| `literal/sherlock-en` | 55.914 us | 76.449 us | 412.601 us | 0.73x |
| `literal-alternate/sherlock-en-nomatch` | 150.733 us | 207.966 us | 594.591 us | 0.72x |
| `bounded-repeat/capitals` | 9,361.737 us | 13,467.050 us | 16,337.725 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,498.620 us | 2,284.815 us | 7,319.175 us | 0.66x |
| `bounded-repeat/letters-en` | 1,129.565 us | 1,800.767 us | 1,910.930 us | 0.63x |
| `aws-keys/full` | 9,268.350 us | 18,452.500 us | 51,574.333 us | 0.50x |
| `literal-alternate/sherlock-casei-ru` | 954.046 us | 1,955.397 us | 7,120.300 us | 0.49x |
| `bounded-repeat/context` | 20,504.400 us | 69,165.600 us | 72,424.050 us | 0.30x |
| `ruff-noqa/real` | 4,600.024 us | 20,685.925 us | 53,690.333 us | 0.22x |
| `dictionary/single` | 8,234.690 us | 45,730.550 us | 45,826.450 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,183.506 us | 469.992 us | 7,678.025 us | 2.52x |
| `common/match-word` | `Match` | 0.191 us | 0.078 us | 0.993 us | 2.45x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.103 us | 0.048 us | 0.088 us | 2.15x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 116.092 us | 57.153 us | 280.304 us | 2.03x |
| `common/backtracking` | `IsMatch` | 0.086 us | 0.043 us | 0.073 us | 1.99x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.053 us | 0.030 us | 0.045 us | 1.81x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.068 us | 0.089 us | 1.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.032 us | 0.050 us | 1.68x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.088 us | 0.055 us | 0.077 us | 1.59x |
| `common/replace-words` | `Replace` | 27.564 us | 17.572 us | 18.700 us | 1.57x |
| `industry/leipzig-symbol-count` | `Count` | 1,846.631 us | 1,219.973 us | 7,150.440 us | 1.51x |
| `common/ip-miss` | `IsMatch` | 0.073 us | 0.049 us | 0.069 us | 1.47x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.049 us | 0.069 us | 1.45x |
| `industry/leipzig-river-window-count` | `Count` | 2,098.844 us | 1,515.950 us | 7,626.580 us | 1.38x |
| `industry/leipzig-name-family-count` | `Count` | 3,807.157 us | 3,053.519 us | 8,044.200 us | 1.25x |
| `industry/mariomka-uri-count` | `Count` | 1,389.895 us | 1,119.593 us | 8,292.200 us | 1.24x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.080 us | 0.107 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.088 us | 0.113 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.088 us | 0.119 us | 1.07x |
| `industry/leipzig-twain-count` | `Count` | 1,154.876 us | 1,295.405 us | 7,358.490 us | 0.89x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.094 us | 0.129 us | 0.79x |
| `common/split-words` | `Split` | 14.370 us | 20.820 us | 22.414 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.041 us | 0.072 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,263.321 us | 3,393.291 us | 10,617.400 us | 0.67x |
| `common/matches-words` | `Count` | 9.270 us | 14.671 us | 17.167 us | 0.63x |
| `common/matches-word` | `Count` | 1.321 us | 2.288 us | 3.350 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.196 us | 0.235 us | 0.50x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.315 us | 597.091 us | 864.751 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.034 us | 0.053 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,898.574 us | 8,699.233 us | 9,400.638 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.415 us | 3,918.380 us | 4,302.811 us | 0.13x |
| `common/matches-set` | `Count` | 7.556 us | 102.858 us | 105.106 us | 0.07x |
| `common/matches-boundary` | `Count` | 6.896 us | 104.559 us | 105.052 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 230.092 us | 4,716.913 us | 5,164.100 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.361 us | 6.345 us | 30.932 us | 1.48x |
| `structural/keyword-to-generic-type` | 4.900 us | 4.045 us | 18.331 us | 1.21x |
| `lokad/langserv/color-short-hex` | 1.626 us | 1.358 us | 1.463 us | 1.20x |
| `lokad/lexer/doc-line` | 3.181 us | 2.944 us | 3.521 us | 1.08x |
| `lokad/lexer/identifier` | 23.316 us | 22.505 us | 24.974 us | 1.04x |
| `structural/ordered-keyword-pair` | 5.177 us | 5.200 us | 18.889 us | 1.00x |
| `lokad/langserv/helper-identifier` | 6.738 us | 7.177 us | 7.644 us | 0.94x |
| `fallback/lookahead` | 5.356 us | 5.946 us | 19.176 us | 0.90x |
| `lokad/langserv/identifier-validator` | 10.933 us | 12.260 us | 13.665 us | 0.89x |
| `lokad/lexer/operator-run` | 2.878 us | 3.415 us | 3.700 us | 0.84x |
| `literal-family/method-token-family` | 7.004 us | 9.124 us | 22.113 us | 0.77x |
| `lokad/style/hex-color` | 0.565 us | 0.810 us | 0.852 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.284 us | 4.783 us | 5.371 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 1.926 us | 2.927 us | 2.935 us | 0.66x |
| `literal/identifier-token` | 2.748 us | 4.289 us | 17.540 us | 0.64x |
| `literal/call-token` | 2.555 us | 4.249 us | 17.737 us | 0.60x |
| `lokad/lexer/number` | 7.935 us | 14.165 us | 14.985 us | 0.56x |
| `structural/ordered-identifier-window` | 14.357 us | 27.830 us | 41.090 us | 0.52x |
| `structural/method-family-generic-call` | 3.373 us | 6.564 us | 19.859 us | 0.51x |
| `lokad/style/cell-ref` | 1.415 us | 2.809 us | 2.897 us | 0.50x |
| `structural/method-family-call` | 3.875 us | 7.948 us | 20.852 us | 0.49x |
| `literal-family/type-token-family` | 10.491 us | 23.597 us | 36.896 us | 0.44x |
| `literal/identifier-token-casei` | 5.738 us | 14.721 us | 29.486 us | 0.39x |
| `lokad/style/range-ref` | 0.887 us | 2.733 us | 2.801 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.355 us | 17.370 us | 30.687 us | 0.31x |
| `structural/ordered-keyword-window` | 5.350 us | 17.467 us | 31.452 us | 0.31x |
| `structural/modifier-family-to-type-window` | 69.183 us | 248.925 us | 263.427 us | 0.28x |
| `lokad/lexer/string` | 22.116 us | 95.004 us | 100.920 us | 0.23x |
| `lokad/folding/region-marker` | 6.054 us | 37.487 us | 63.340 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.297 us | 24.218 us | 24.511 us | 0.09x |
| `structural/keyword-family-to-capitalized-identifier` | 11.324 us | 213.718 us | 228.861 us | 0.05x |
| `lokad/langserv/url-download` | 2.637 us | 52.647 us | 52.488 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.970 us | 132.548 us | 148.074 us | 0.04x |
| `balancing/begin-end-blocks` | 5.659 us | 4,337.872 us | 4,381.067 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.358 us | 4.426 us | 28.039 us | 2.11x |
| `lokad/langserv/color-short-hex` | 1.619 us | 0.970 us | 1.167 us | 1.67x |
| `lokad/lexer/operator-run` | 2.905 us | 1.913 us | 2.450 us | 1.52x |
| `lokad/lexer/doc-line` | 3.168 us | 2.117 us | 2.882 us | 1.50x |
| `lokad/lexer/identifier` | 23.255 us | 15.822 us | 19.504 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.343 us | 7.826 us | 20.781 us | 1.45x |
| `structural/keyword-to-generic-type` | 4.880 us | 3.448 us | 17.111 us | 1.42x |
| `structural/ordered-identifier-window` | 14.322 us | 10.262 us | 23.399 us | 1.40x |
| `lokad/langserv/helper-identifier` | 6.753 us | 4.901 us | 5.571 us | 1.38x |
| `fallback/lookahead` | 5.373 us | 4.145 us | 16.886 us | 1.30x |
| `structural/ordered-keyword-pair` | 5.151 us | 4.034 us | 17.033 us | 1.28x |
| `lokad/langserv/identifier-validator` | 10.921 us | 8.617 us | 10.731 us | 1.27x |
| `structural/modifier-family-to-type-window` | 69.488 us | 55.055 us | 70.260 us | 1.26x |
| `lokad/docs/inline-doc-prefix` | 3.227 us | 2.686 us | 3.515 us | 1.20x |
| `lokad/style/hex-color` | 0.566 us | 0.490 us | 0.585 us | 1.15x |
| `lokad/lexer/number` | 7.917 us | 7.084 us | 8.125 us | 1.12x |
| `lokad/langserv/color-rgb-hex` | 1.961 us | 1.847 us | 2.074 us | 1.06x |
| `literal/identifier-token-casei` | 5.737 us | 5.855 us | 19.696 us | 0.98x |
| `literal-family/type-token-family` | 10.455 us | 11.351 us | 24.518 us | 0.92x |
| `lokad/style/cell-ref` | 1.419 us | 1.707 us | 1.925 us | 0.83x |
| `structural/method-family-call` | 3.846 us | 5.002 us | 17.581 us | 0.77x |
| `structural/method-family-generic-call` | 3.369 us | 4.447 us | 17.103 us | 0.76x |
| `literal-family/method-token-family` | 3.848 us | 5.223 us | 17.927 us | 0.74x |
| `literal/call-token` | 2.559 us | 3.906 us | 16.830 us | 0.66x |
| `structural/ordered-keyword-window` | 5.367 us | 8.543 us | 22.220 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.315 us | 8.516 us | 21.741 us | 0.62x |
| `literal/identifier-token` | 2.763 us | 4.685 us | 17.313 us | 0.59x |
| `lokad/style/range-ref` | 0.869 us | 1.544 us | 1.603 us | 0.56x |
| `lokad/lexer/string` | 21.554 us | 45.295 us | 47.683 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 4.991 us | 10.679 us | 24.105 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.259 us | 7.535 us | 8.067 us | 0.30x |
| `lokad/langserv/url-download` | 2.613 us | 12.828 us | 13.409 us | 0.20x |
| `lokad/folding/region-marker` | 6.047 us | 37.694 us | 63.296 us | 0.16x |
| `balancing/begin-end-blocks` | 5.668 us | 460.114 us | 491.036 us | 0.01x |
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
