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
and resource checks passed in **46m12s**. The [run receipt](bench/Experiments/DeliveryDev65-20261110/full-refresh.json)
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
| `literal/sherlock-zh` | 83.082 us | 19.048 us | 2,202.542 us | 4.36x |
| `literal/sherlock-ru` | 171.515 us | 98.330 us | 5,404.950 us | 1.74x |
| `literal-alternate/sherlock-zh` | 105.077 us | 69.402 us | 2,207.032 us | 1.51x |
| `literal/sherlock-casei-en` | 164.330 us | 191.869 us | 586.169 us | 0.86x |
| `literal/sherlock-en` | 58.148 us | 85.654 us | 429.624 us | 0.68x |
| `literal/sherlock-casei-ru` | 249.309 us | 394.007 us | 5,596.736 us | 0.63x |
| `ruff-noqa/tweaked` | 4,381.272 us | 7,679.209 us | 43,805.200 us | 0.57x |
| `aws-keys/quick` | 4,927.844 us | 9,872.312 us | 44,699.650 us | 0.50x |
| `bounded-repeat/letters-en` | 1,128.445 us | 2,833.141 us | 2,943.688 us | 0.40x |
| `literal-alternate/sherlock-ru` | 1,673.320 us | 4,772.613 us | 10,019.725 us | 0.35x |
| `bounded-repeat/letters-ru` | 2,055.978 us | 6,010.708 us | 6,838.291 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 198.430 us | 600.325 us | 1,022.034 us | 0.33x |
| `dictionary/single` | 8,374.340 us | 25,885.633 us | 25,972.800 us | 0.32x |
| `literal-alternate/sherlock-en` | 393.243 us | 1,499.254 us | 1,982.159 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 156.049 us | 671.687 us | 1,072.651 us | 0.23x |
| `bounded-repeat/capitals` | 9,532.150 us | 46,423.600 us | 49,827.600 us | 0.21x |
| `bounded-repeat/context` | 20,853.275 us | 148,725.000 us | 152,073.300 us | 0.14x |
| `aws-keys/full` | 10,643.812 us | 86,710.100 us | 118,941.800 us | 0.12x |
| `ruff-noqa/real` | 4,377.572 us | 74,700.900 us | 91,427.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 949.998 us | 23,356.125 us | 28,529.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 141.251 us | 11,305.829 us | 12,155.600 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,168.325 us | 494.814 us | 8,974.775 us | 2.36x |
| `common/match-word` | `Match` | 0.201 us | 0.167 us | 1.124 us | 1.20x |
| `industry/leipzig-twain-count` | `Count` | 1,210.853 us | 1,350.795 us | 8,565.578 us | 0.90x |
| `industry/mariomka-uri-count` | `Count` | 1,441.106 us | 1,951.546 us | 10,476.950 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.112 us | 0.150 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.195 us | 0.213 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.031 us | 264.699 us | 502.588 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.162 us | 0.181 us | 0.48x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.175 us | 0.195 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 4,070.679 us | 9,920.862 us | 17,228.000 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.078 us | 0.194 us | 0.214 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.239 us | 0.278 us | 0.39x |
| `common/replace-words` | `Replace` | 28.216 us | 77.036 us | 78.713 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.273 us | 0.301 us | 0.35x |
| `common/matches-word` | `Count` | 1.353 us | 4.263 us | 6.023 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.350 us | 0.376 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,230.200 us | 8,387.790 us | 16,900.933 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.138 us | 0.26x |
| `common/split-words` | `Split` | 17.901 us | 78.549 us | 79.762 us | 0.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.099 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.818 us | 974.303 us | 1,247.404 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.575 us | 0.608 us | 0.15x |
| `common/one-node-backtracking` | `IsMatch` | 0.087 us | 0.622 us | 0.655 us | 0.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.076 us | 0.090 us | 0.14x |
| `common/matches-words` | `Count` | 9.406 us | 71.566 us | 72.416 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.099 us | 0.844 us | 0.881 us | 0.12x |
| `industry/leipzig-river-window-count` | `Count` | 3,422.183 us | 33,897.433 us | 40,262.200 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,946.385 us | 19,774.600 us | 20,316.850 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,887.328 us | 22,984.575 us | 30,244.500 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.311 us | 8,201.430 us | 8,704.078 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.862 us | 165.155 us | 166.709 us | 0.05x |
| `common/matches-set` | `Count` | 7.942 us | 196.694 us | 199.308 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 265.783 us | 11,920.300 us | 12,449.533 us | 0.02x |
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
| `literal/sherlock-zh` | 83.004 us | 24.143 us | 2,194.718 us | 3.44x |
| `literal-alternate/sherlock-zh` | 105.007 us | 36.349 us | 2,153.931 us | 2.89x |
| `literal/sherlock-casei-en` | 164.670 us | 78.735 us | 426.484 us | 2.09x |
| `literal/sherlock-ru` | 169.871 us | 90.194 us | 5,271.520 us | 1.88x |
| `aws-keys/quick` | 4,949.175 us | 3,607.567 us | 36,434.200 us | 1.37x |
| `literal-alternate/sherlock-en-mixed` | 198.887 us | 176.161 us | 566.950 us | 1.13x |
| `literal-alternate/sherlock-en` | 393.237 us | 348.412 us | 753.071 us | 1.13x |
| `literal/sherlock-casei-ru` | 249.812 us | 224.020 us | 5,267.736 us | 1.12x |
| `bounded-repeat/letters-ru` | 2,052.332 us | 2,073.743 us | 2,895.142 us | 0.99x |
| `literal-alternate/sherlock-casei-en` | 142.017 us | 147.081 us | 564.128 us | 0.97x |
| `ruff-noqa/tweaked` | 4,412.900 us | 4,573.206 us | 38,486.450 us | 0.96x |
| `literal-alternate/sherlock-en-nomatch` | 155.896 us | 201.660 us | 602.474 us | 0.77x |
| `literal/sherlock-en` | 58.091 us | 75.466 us | 437.256 us | 0.77x |
| `bounded-repeat/capitals` | 9,521.538 us | 13,708.017 us | 16,425.600 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,630.619 us | 2,364.725 us | 7,431.250 us | 0.69x |
| `bounded-repeat/letters-en` | 1,137.822 us | 1,847.437 us | 1,925.505 us | 0.62x |
| `aws-keys/full` | 10,661.057 us | 18,685.140 us | 52,399.967 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 952.092 us | 1,979.663 us | 7,119.220 us | 0.48x |
| `bounded-repeat/context` | 20,659.625 us | 69,752.150 us | 72,958.650 us | 0.30x |
| `ruff-noqa/real` | 4,387.856 us | 21,077.475 us | 47,655.467 us | 0.21x |
| `dictionary/single` | 8,372.222 us | 46,168.350 us | 46,182.250 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.200 us | 0.078 us | 1.018 us | 2.55x |
| `industry/mariomka-email-count` | `Count` | 1,168.474 us | 460.697 us | 7,771.100 us | 2.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.502 us | 58.738 us | 246.012 us | 2.31x |
| `common/backtracking` | `IsMatch` | 0.099 us | 0.044 us | 0.075 us | 2.25x |
| `industry/leipzig-river-window-count` | `Count` | 3,433.827 us | 1,576.043 us | 8,320.560 us | 2.18x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.068 us | 0.087 us | 1.79x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.030 us | 0.050 us | 1.71x |
| `common/replace-words` | `Replace` | 28.334 us | 17.192 us | 19.803 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.088 us | 1.62x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.031 us | 0.048 us | 1.61x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.048 us | 0.069 us | 1.54x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.050 us | 0.070 us | 1.51x |
| `industry/leipzig-symbol-count` | `Count` | 1,893.440 us | 1,277.440 us | 7,194.440 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.056 us | 0.077 us | 1.39x |
| `industry/leipzig-name-family-count` | `Count` | 4,138.837 us | 3,035.520 us | 8,116.375 us | 1.36x |
| `industry/mariomka-uri-count` | `Count` | 1,444.750 us | 1,096.548 us | 8,425.500 us | 1.32x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.108 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.116 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.091 us | 0.121 us | 1.01x |
| `common/one-node-backtracking` | `IsMatch` | 0.087 us | 0.088 us | 0.129 us | 0.98x |
| `industry/leipzig-twain-count` | `Count` | 1,210.716 us | 1,329.609 us | 7,604.660 us | 0.91x |
| `common/matches-words` | `Count` | 9.393 us | 11.033 us | 14.095 us | 0.85x |
| `common/split-words` | `Split` | 14.737 us | 20.895 us | 22.772 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.074 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,236.588 us | 3,517.405 us | 10,976.033 us | 0.64x |
| `common/matches-word` | `Count` | 1.352 us | 2.386 us | 3.492 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.199 us | 0.236 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.036 us | 0.052 us | 0.35x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,952.144 us | 8,826.322 us | 9,354.062 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.609 us | 4,012.658 us | 4,373.567 us | 0.12x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.530 us | 1,501.400 us | 1,835.758 us | 0.12x |
| `common/matches-set` | `Count` | 7.860 us | 106.063 us | 107.644 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.883 us | 106.614 us | 108.141 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.863 us | 4,829.444 us | 5,260.827 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.113 us | 6.460 us | 29.663 us | 1.41x |
| `structural/keyword-to-generic-type` | 5.498 us | 4.114 us | 15.756 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.671 us | 1.360 us | 1.469 us | 1.23x |
| `lokad/lexer/doc-line` | 3.410 us | 3.043 us | 3.669 us | 1.12x |
| `lokad/lexer/identifier` | 23.853 us | 23.139 us | 25.802 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.390 us | 5.475 us | 17.245 us | 0.98x |
| `lokad/langserv/helper-identifier` | 7.231 us | 7.393 us | 7.692 us | 0.98x |
| `lokad/langserv/identifier-validator` | 11.895 us | 12.719 us | 14.203 us | 0.94x |
| `fallback/lookahead` | 5.425 us | 6.135 us | 17.153 us | 0.88x |
| `lokad/lexer/operator-run` | 2.924 us | 3.488 us | 3.849 us | 0.84x |
| `literal-family/method-token-family` | 7.208 us | 9.355 us | 20.116 us | 0.77x |
| `lokad/style/hex-color` | 0.608 us | 0.814 us | 0.856 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 2.056 us | 2.990 us | 3.029 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.392 us | 4.943 us | 5.811 us | 0.69x |
| `literal/identifier-token` | 2.802 us | 4.423 us | 16.276 us | 0.63x |
| `literal/call-token` | 2.596 us | 4.343 us | 15.855 us | 0.60x |
| `lokad/lexer/number` | 8.293 us | 14.580 us | 15.576 us | 0.57x |
| `structural/ordered-identifier-window` | 16.286 us | 29.049 us | 40.093 us | 0.56x |
| `structural/method-family-generic-call` | 3.459 us | 6.737 us | 17.925 us | 0.51x |
| `lokad/style/cell-ref` | 1.503 us | 2.971 us | 3.050 us | 0.51x |
| `structural/method-family-call` | 3.897 us | 8.468 us | 19.324 us | 0.46x |
| `literal-family/type-token-family` | 10.563 us | 24.815 us | 36.089 us | 0.43x |
| `literal/identifier-token-casei` | 5.714 us | 15.173 us | 27.409 us | 0.38x |
| `lokad/style/range-ref` | 0.926 us | 2.785 us | 2.799 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.462 us | 17.344 us | 28.226 us | 0.31x |
| `structural/modifier-family-to-type-window` | 79.007 us | 256.933 us | 269.351 us | 0.31x |
| `structural/ordered-keyword-window` | 5.513 us | 18.001 us | 29.670 us | 0.31x |
| `lokad/lexer/string` | 21.924 us | 98.280 us | 102.546 us | 0.22x |
| `lokad/folding/region-marker` | 6.048 us | 36.854 us | 62.701 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.562 us | 24.535 us | 24.949 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.524 us | 214.270 us | 227.662 us | 0.05x |
| `lokad/langserv/url-download` | 2.592 us | 51.259 us | 51.735 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.042 us | 133.591 us | 145.073 us | 0.04x |
| `balancing/begin-end-blocks` | 5.737 us | 4,346.939 us | 4,360.333 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.163 us | 4.454 us | 26.664 us | 2.06x |
| `lokad/langserv/color-short-hex` | 1.680 us | 0.986 us | 1.157 us | 1.70x |
| `lokad/langserv/helper-identifier` | 7.243 us | 4.600 us | 5.682 us | 1.57x |
| `structural/keyword-to-generic-type` | 5.500 us | 3.548 us | 14.667 us | 1.55x |
| `structural/ordered-identifier-window` | 16.298 us | 10.724 us | 21.850 us | 1.52x |
| `lokad/lexer/doc-line` | 3.409 us | 2.261 us | 2.980 us | 1.51x |
| `lokad/lexer/operator-run` | 2.919 us | 1.963 us | 2.596 us | 1.49x |
| `structural/keyword-family-to-capitalized-identifier` | 11.529 us | 7.974 us | 18.590 us | 1.45x |
| `lokad/lexer/identifier` | 23.906 us | 16.772 us | 20.853 us | 1.43x |
| `structural/modifier-family-to-type-window` | 79.244 us | 56.661 us | 68.336 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.392 us | 4.161 us | 15.267 us | 1.30x |
| `lokad/langserv/identifier-validator` | 11.889 us | 9.279 us | 11.516 us | 1.28x |
| `fallback/lookahead` | 5.425 us | 4.264 us | 14.929 us | 1.27x |
| `lokad/style/hex-color` | 0.609 us | 0.514 us | 0.596 us | 1.19x |
| `lokad/docs/inline-doc-prefix` | 3.384 us | 2.881 us | 3.859 us | 1.17x |
| `lokad/lexer/number` | 8.294 us | 7.464 us | 8.633 us | 1.11x |
| `lokad/langserv/color-rgb-hex` | 2.041 us | 1.923 us | 2.083 us | 1.06x |
| `literal/identifier-token-casei` | 5.733 us | 5.943 us | 17.267 us | 0.96x |
| `literal-family/type-token-family` | 10.594 us | 11.549 us | 22.443 us | 0.92x |
| `lokad/style/cell-ref` | 1.504 us | 1.774 us | 1.915 us | 0.85x |
| `structural/method-family-generic-call` | 3.459 us | 4.533 us | 15.063 us | 0.76x |
| `structural/method-family-call` | 3.894 us | 5.123 us | 15.749 us | 0.76x |
| `literal-family/method-token-family` | 3.896 us | 5.346 us | 15.949 us | 0.73x |
| `literal/call-token` | 2.603 us | 3.973 us | 14.912 us | 0.66x |
| `structural/ordered-keyword-window` | 5.515 us | 8.693 us | 20.171 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.445 us | 8.661 us | 19.620 us | 0.63x |
| `lokad/style/range-ref` | 0.913 us | 1.520 us | 1.592 us | 0.60x |
| `literal/identifier-token` | 2.806 us | 4.735 us | 15.984 us | 0.59x |
| `lokad/lexer/string` | 21.829 us | 45.700 us | 50.535 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 5.046 us | 10.782 us | 21.761 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.620 us | 8.010 us | 8.793 us | 0.33x |
| `lokad/langserv/url-download` | 2.616 us | 13.057 us | 13.597 us | 0.20x |
| `lokad/folding/region-marker` | 6.045 us | 36.121 us | 62.615 us | 0.17x |
| `balancing/begin-end-blocks` | 5.742 us | 471.385 us | 500.742 us | 0.01x |
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
