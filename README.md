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
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The tables were refreshed on **12 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m11s**. The [run receipt](bench/Experiments/DeliveryDev74-20261112/full-refresh.json)
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
| `literal/sherlock-zh` | 81.384 us | 18.897 us | 2,244.434 us | 4.31x |
| `literal/sherlock-ru` | 168.137 us | 99.255 us | 5,208.829 us | 1.69x |
| `literal-alternate/sherlock-zh` | 103.279 us | 70.414 us | 2,199.394 us | 1.47x |
| `literal/sherlock-casei-en` | 158.491 us | 194.575 us | 556.778 us | 0.81x |
| `literal/sherlock-en` | 56.559 us | 84.788 us | 467.861 us | 0.67x |
| `literal/sherlock-casei-ru` | 243.391 us | 365.457 us | 5,504.231 us | 0.67x |
| `ruff-noqa/tweaked` | 4,659.376 us | 7,701.500 us | 41,547.900 us | 0.60x |
| `aws-keys/quick` | 3,848.575 us | 9,780.475 us | 47,371.000 us | 0.39x |
| `bounded-repeat/letters-en` | 1,121.346 us | 2,862.674 us | 2,981.615 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,038.868 us | 5,788.985 us | 6,617.717 us | 0.35x |
| `dictionary/single` | 8,335.860 us | 25,972.767 us | 26,034.600 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,552.980 us | 4,916.400 us | 10,124.325 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 195.525 us | 627.125 us | 1,062.025 us | 0.31x |
| `literal-alternate/sherlock-en` | 376.066 us | 1,520.129 us | 2,125.917 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 153.584 us | 669.017 us | 1,098.922 us | 0.23x |
| `bounded-repeat/capitals` | 9,476.575 us | 46,961.550 us | 50,058.300 us | 0.20x |
| `bounded-repeat/context` | 22,241.300 us | 150,206.700 us | 153,364.000 us | 0.15x |
| `aws-keys/full` | 10,953.086 us | 87,520.100 us | 120,864.500 us | 0.13x |
| `ruff-noqa/real` | 4,404.600 us | 73,638.200 us | 103,034.800 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 978.913 us | 23,172.700 us | 28,647.050 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 134.027 us | 11,378.429 us | 12,274.929 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,174.005 us | 496.037 us | 8,991.300 us | 2.37x |
| `common/match-word` | `Match` | 0.194 us | 0.170 us | 1.138 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,250.908 us | 1,365.541 us | 8,484.678 us | 0.92x |
| `industry/mariomka-uri-count` | `Count` | 1,433.179 us | 1,861.361 us | 10,454.700 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.111 us | 0.148 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.191 us | 0.211 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.083 us | 0.101 us | 0.61x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.092 us | 0.169 us | 0.185 us | 0.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 118.512 us | 262.451 us | 496.925 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.178 us | 0.198 us | 0.44x |
| `common/uri-match` | `IsMatch` | 0.097 us | 0.234 us | 0.269 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.188 us | 0.209 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.265 us | 0.289 us | 0.36x |
| `common/replace-words` | `Replace` | 27.304 us | 79.228 us | 80.125 us | 0.34x |
| `common/matches-word` | `Count` | 1.359 us | 4.249 us | 5.979 us | 0.32x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.026 us | 0.081 us | 0.098 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.348 us | 0.375 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,252.200 us | 8,271.930 us | 16,781.833 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.108 us | 0.136 us | 0.25x |
| `common/split-words` | `Split` | 14.611 us | 78.259 us | 79.788 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.521 us | 973.447 us | 1,245.220 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.565 us | 0.594 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,582.746 us | 9,773.625 us | 16,927.733 us | 0.16x |
| `common/matches-words` | `Count` | 9.324 us | 69.762 us | 71.601 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.092 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.618 us | 0.649 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.839 us | 0.883 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,791.360 us | 19,660.750 us | 20,327.925 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,864.790 us | 22,556.775 us | 29,671.550 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,154.560 us | 32,152.567 us | 39,456.350 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 497.912 us | 8,247.130 us | 8,797.256 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.156 us | 165.231 us | 166.576 us | 0.04x |
| `common/matches-set` | `Count` | 7.104 us | 197.161 us | 198.402 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.591 us | 11,927.843 us | 12,528.971 us | 0.02x |
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
| `literal/sherlock-zh` | 81.314 us | 23.989 us | 2,214.836 us | 3.39x |
| `literal-alternate/sherlock-zh` | 103.064 us | 36.025 us | 2,152.085 us | 2.86x |
| `literal/sherlock-casei-en` | 158.576 us | 78.380 us | 409.422 us | 2.02x |
| `literal/sherlock-ru` | 167.752 us | 91.075 us | 5,210.180 us | 1.84x |
| `literal/sherlock-casei-ru` | 241.420 us | 219.762 us | 5,299.880 us | 1.10x |
| `literal-alternate/sherlock-en-mixed` | 195.586 us | 178.976 us | 593.280 us | 1.09x |
| `aws-keys/quick` | 3,938.276 us | 3,690.200 us | 39,426.600 us | 1.07x |
| `literal-alternate/sherlock-casei-en` | 134.317 us | 132.745 us | 583.320 us | 1.01x |
| `literal-alternate/sherlock-en` | 372.755 us | 371.427 us | 781.313 us | 1.00x |
| `ruff-noqa/tweaked` | 4,657.053 us | 4,641.924 us | 36,652.050 us | 1.00x |
| `bounded-repeat/letters-ru` | 2,025.695 us | 2,039.154 us | 2,870.862 us | 0.99x |
| `literal/sherlock-en` | 56.283 us | 74.516 us | 394.142 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 154.059 us | 206.334 us | 571.725 us | 0.75x |
| `bounded-repeat/capitals` | 9,485.263 us | 13,620.150 us | 16,358.875 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,563.516 us | 2,374.253 us | 7,504.050 us | 0.66x |
| `bounded-repeat/letters-en` | 1,111.121 us | 1,844.410 us | 1,931.897 us | 0.60x |
| `aws-keys/full` | 10,972.300 us | 18,575.460 us | 53,545.467 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 976.406 us | 1,973.850 us | 7,143.025 us | 0.49x |
| `bounded-repeat/context` | 22,277.875 us | 69,946.250 us | 72,476.500 us | 0.32x |
| `ruff-noqa/real` | 4,519.544 us | 20,348.375 us | 52,375.433 us | 0.22x |
| `dictionary/single` | 8,315.980 us | 46,048.950 us | 46,060.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,170.249 us | 462.860 us | 7,694.125 us | 2.53x |
| `common/match-word` | `Match` | 0.194 us | 0.081 us | 0.988 us | 2.39x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 118.396 us | 58.378 us | 270.787 us | 2.03x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.045 us | 0.074 us | 1.98x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.031 us | 0.046 us | 1.76x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.069 us | 0.087 us | 1.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.030 us | 0.048 us | 1.73x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.094 us | 0.056 us | 0.077 us | 1.68x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.085 us | 1.64x |
| `common/ip-miss` | `IsMatch` | 0.079 us | 0.049 us | 0.070 us | 1.62x |
| `common/replace-words` | `Replace` | 27.356 us | 17.534 us | 17.901 us | 1.56x |
| `industry/leipzig-symbol-count` | `Count` | 1,866.729 us | 1,244.900 us | 7,210.700 us | 1.50x |
| `common/ip-match` | `IsMatch` | 0.072 us | 0.049 us | 0.068 us | 1.49x |
| `industry/leipzig-river-window-count` | `Count` | 2,158.657 us | 1,527.836 us | 7,854.280 us | 1.41x |
| `industry/mariomka-uri-count` | `Count` | 1,443.330 us | 1,123.043 us | 8,548.300 us | 1.29x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.090 us | 0.115 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.089 us | 0.120 us | 1.05x |
| `industry/leipzig-twain-count` | `Count` | 1,251.248 us | 1,334.231 us | 7,477.860 us | 0.94x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.097 us | 0.128 us | 0.78x |
| `common/split-words` | `Split` | 14.569 us | 20.885 us | 22.710 us | 0.70x |
| `industry/mariomka-ip-count` | `Count` | 2,259.506 us | 3,466.243 us | 10,825.633 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.043 us | 0.073 us | 0.64x |
| `common/matches-words` | `Count` | 9.317 us | 15.283 us | 17.785 us | 0.61x |
| `common/matches-word` | `Count` | 1.356 us | 2.350 us | 3.418 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 1,597.938 us | 2,985.192 us | 7,645.750 us | 0.54x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.198 us | 0.239 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.691 us | 606.653 us | 862.440 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,789.693 us | 8,838.178 us | 9,413.650 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 505.150 us | 3,968.832 us | 4,342.061 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.155 us | 104.581 us | 106.795 us | 0.07x |
| `common/matches-set` | `Count` | 7.105 us | 105.168 us | 106.606 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 243.714 us | 4,804.525 us | 5,223.753 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.286 us | 6.450 us | 31.306 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.189 us | 4.145 us | 18.708 us | 1.25x |
| `lokad/langserv/color-short-hex` | 1.641 us | 1.350 us | 1.448 us | 1.22x |
| `lokad/lexer/doc-line` | 3.216 us | 3.020 us | 3.657 us | 1.06x |
| `lokad/lexer/identifier` | 23.321 us | 22.906 us | 25.163 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.387 us | 7.373 us | 7.733 us | 1.00x |
| `structural/ordered-keyword-pair` | 5.308 us | 5.335 us | 19.586 us | 1.00x |
| `fallback/lookahead` | 5.472 us | 6.116 us | 19.558 us | 0.89x |
| `lokad/langserv/identifier-validator` | 11.024 us | 12.616 us | 13.955 us | 0.87x |
| `lokad/lexer/operator-run` | 2.777 us | 3.389 us | 3.763 us | 0.82x |
| `literal-family/method-token-family` | 7.156 us | 9.354 us | 22.386 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 1.995 us | 2.877 us | 2.897 us | 0.69x |
| `lokad/style/hex-color` | 0.562 us | 0.821 us | 0.871 us | 0.68x |
| `lokad/docs/inline-doc-prefix` | 3.260 us | 5.005 us | 5.713 us | 0.65x |
| `literal/identifier-token` | 2.791 us | 4.354 us | 17.871 us | 0.64x |
| `lokad/lexer/number` | 8.809 us | 14.431 us | 15.064 us | 0.61x |
| `literal/call-token` | 2.591 us | 4.324 us | 17.988 us | 0.60x |
| `structural/method-family-generic-call` | 3.452 us | 6.701 us | 20.453 us | 0.52x |
| `structural/ordered-identifier-window` | 14.729 us | 28.892 us | 41.899 us | 0.51x |
| `lokad/style/cell-ref` | 1.407 us | 2.876 us | 2.902 us | 0.49x |
| `structural/method-family-call` | 3.851 us | 7.954 us | 20.889 us | 0.48x |
| `literal-family/type-token-family` | 10.451 us | 24.117 us | 38.165 us | 0.43x |
| `literal/identifier-token-casei` | 5.781 us | 14.827 us | 29.416 us | 0.39x |
| `lokad/style/range-ref` | 0.881 us | 2.735 us | 2.743 us | 0.32x |
| `structural/ordered-keyword-window` | 5.392 us | 17.856 us | 31.888 us | 0.30x |
| `structural/ordered-modifier-pair` | 5.276 us | 17.724 us | 31.396 us | 0.30x |
| `structural/modifier-family-to-type-window` | 72.041 us | 258.121 us | 271.967 us | 0.28x |
| `lokad/lexer/string` | 21.188 us | 94.065 us | 97.966 us | 0.23x |
| `lokad/folding/region-marker` | 6.092 us | 36.575 us | 62.937 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.329 us | 24.191 us | 24.511 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.472 us | 216.993 us | 231.480 us | 0.05x |
| `lokad/langserv/url-download` | 2.453 us | 52.737 us | 53.324 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.033 us | 130.340 us | 144.314 us | 0.04x |
| `balancing/begin-end-blocks` | 5.769 us | 4,420.861 us | 4,413.283 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.299 us | 4.472 us | 28.566 us | 2.08x |
| `lokad/langserv/color-short-hex` | 1.643 us | 0.970 us | 1.145 us | 1.69x |
| `lokad/langserv/helper-identifier` | 7.357 us | 4.669 us | 5.658 us | 1.58x |
| `structural/keyword-to-generic-type` | 5.197 us | 3.475 us | 17.406 us | 1.50x |
| `lokad/lexer/operator-run` | 2.786 us | 1.911 us | 2.475 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.383 us | 7.968 us | 21.370 us | 1.43x |
| `lokad/lexer/doc-line` | 3.211 us | 2.294 us | 2.974 us | 1.40x |
| `lokad/lexer/identifier` | 23.429 us | 16.785 us | 20.206 us | 1.40x |
| `structural/ordered-identifier-window` | 14.712 us | 10.573 us | 24.257 us | 1.39x |
| `fallback/lookahead` | 5.489 us | 4.234 us | 17.325 us | 1.30x |
| `structural/ordered-keyword-pair` | 5.338 us | 4.156 us | 17.717 us | 1.28x |
| `structural/modifier-family-to-type-window` | 71.814 us | 56.376 us | 71.472 us | 1.27x |
| `lokad/langserv/identifier-validator` | 10.933 us | 9.147 us | 11.276 us | 1.20x |
| `lokad/docs/inline-doc-prefix` | 3.277 us | 2.801 us | 3.627 us | 1.17x |
| `lokad/lexer/number` | 8.389 us | 7.201 us | 8.218 us | 1.17x |
| `lokad/style/hex-color` | 0.564 us | 0.505 us | 0.591 us | 1.12x |
| `lokad/langserv/color-rgb-hex` | 1.979 us | 1.777 us | 2.000 us | 1.11x |
| `literal/identifier-token-casei` | 5.817 us | 5.920 us | 19.743 us | 0.98x |
| `literal-family/type-token-family` | 10.474 us | 11.475 us | 24.925 us | 0.91x |
| `lokad/style/cell-ref` | 1.406 us | 1.700 us | 1.944 us | 0.83x |
| `structural/method-family-generic-call` | 3.441 us | 4.520 us | 17.579 us | 0.76x |
| `structural/method-family-call` | 3.844 us | 5.075 us | 17.639 us | 0.76x |
| `literal-family/method-token-family` | 3.916 us | 5.348 us | 18.248 us | 0.73x |
| `literal/call-token` | 2.589 us | 3.974 us | 16.991 us | 0.65x |
| `structural/ordered-keyword-window` | 5.446 us | 8.627 us | 22.310 us | 0.63x |
| `lokad/style/range-ref` | 0.881 us | 1.442 us | 1.563 us | 0.61x |
| `structural/ordered-modifier-pair` | 5.270 us | 8.642 us | 22.249 us | 0.61x |
| `literal/identifier-token` | 2.781 us | 4.745 us | 17.343 us | 0.59x |
| `lokad/lexer/string` | 21.138 us | 44.295 us | 47.487 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 5.036 us | 10.749 us | 24.622 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.328 us | 7.385 us | 7.905 us | 0.32x |
| `lokad/langserv/url-download` | 2.440 us | 12.904 us | 13.496 us | 0.19x |
| `lokad/folding/region-marker` | 6.081 us | 36.628 us | 61.479 us | 0.17x |
| `balancing/begin-end-blocks` | 5.751 us | 468.466 us | 497.152 us | 0.01x |
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
