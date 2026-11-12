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
and resource checks passed in **47m21s**. The [run receipt](bench/Experiments/DeliveryDev76-20261112/full-refresh.json)
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
| `literal/sherlock-zh` | 81.515 us | 18.919 us | 2,215.194 us | 4.31x |
| `literal/sherlock-ru` | 173.294 us | 100.310 us | 5,287.664 us | 1.73x |
| `literal-alternate/sherlock-zh` | 104.579 us | 69.112 us | 2,167.576 us | 1.51x |
| `literal/sherlock-casei-en` | 160.188 us | 200.863 us | 594.967 us | 0.80x |
| `literal/sherlock-en` | 56.822 us | 87.837 us | 448.955 us | 0.65x |
| `literal/sherlock-casei-ru` | 236.705 us | 395.859 us | 5,636.385 us | 0.60x |
| `ruff-noqa/tweaked` | 4,676.206 us | 8,059.520 us | 45,299.800 us | 0.58x |
| `bounded-repeat/letters-en` | 1,113.038 us | 2,830.581 us | 2,956.692 us | 0.39x |
| `aws-keys/quick` | 3,880.800 us | 9,872.188 us | 43,826.950 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,050.681 us | 5,893.669 us | 6,721.842 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 198.072 us | 594.153 us | 1,011.923 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,545.722 us | 4,804.950 us | 10,118.000 us | 0.32x |
| `dictionary/single` | 8,268.970 us | 26,110.333 us | 26,169.067 us | 0.32x |
| `literal-alternate/sherlock-en` | 377.533 us | 1,513.856 us | 2,015.367 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 154.025 us | 660.389 us | 1,096.600 us | 0.23x |
| `bounded-repeat/capitals` | 9,421.337 us | 46,701.850 us | 50,584.300 us | 0.20x |
| `bounded-repeat/context` | 21,385.200 us | 147,951.400 us | 151,436.800 us | 0.14x |
| `aws-keys/full` | 10,868.900 us | 86,785.900 us | 121,936.500 us | 0.13x |
| `ruff-noqa/real` | 4,646.824 us | 74,520.000 us | 91,078.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 965.738 us | 23,223.325 us | 28,578.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 134.579 us | 11,177.686 us | 12,087.714 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,200.041 us | 507.246 us | 8,985.175 us | 2.37x |
| `common/match-word` | `Match` | 0.195 us | 0.174 us | 1.172 us | 1.12x |
| `industry/leipzig-twain-count` | `Count` | 1,151.868 us | 1,313.540 us | 8,464.450 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,428.819 us | 1,970.456 us | 10,313.725 us | 0.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.111 us | 0.146 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.124 us | 0.203 us | 0.221 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.102 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.170 us | 0.186 us | 0.52x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 116.191 us | 258.841 us | 495.151 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.179 us | 0.200 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.080 us | 0.189 us | 0.209 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.239 us | 0.275 us | 0.40x |
| `common/replace-words` | `Replace` | 27.721 us | 77.539 us | 80.468 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.270 us | 0.298 us | 0.36x |
| `common/matches-word` | `Count` | 1.353 us | 4.293 us | 5.974 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.104 us | 0.345 us | 0.369 us | 0.30x |
| `industry/mariomka-ip-count` | `Count` | 2,260.556 us | 8,524.178 us | 16,985.400 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.108 us | 0.138 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.20x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.567 us | 0.606 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.609 us | 963.571 us | 1,259.695 us | 0.18x |
| `common/split-words` | `Split` | 14.587 us | 80.868 us | 82.744 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,589.381 us | 9,899.475 us | 17,067.100 us | 0.16x |
| `common/matches-words` | `Count` | 9.344 us | 69.985 us | 71.751 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.090 us | 0.12x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.619 us | 0.656 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.834 us | 0.877 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,793.536 us | 19,320.725 us | 19,996.150 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,626.500 us | 22,674.550 us | 30,388.150 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,155.979 us | 32,343.733 us | 39,910.400 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 464.249 us | 8,647.967 us | 9,129.044 us | 0.05x |
| `common/matches-boundary` | `Count` | 7.016 us | 164.474 us | 166.822 us | 0.04x |
| `common/matches-set` | `Count` | 7.162 us | 197.933 us | 199.753 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 242.251 us | 11,876.414 us | 12,468.514 us | 0.02x |
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
| `literal/sherlock-zh` | 81.592 us | 23.978 us | 2,189.577 us | 3.40x |
| `literal-alternate/sherlock-zh` | 108.919 us | 36.314 us | 2,152.867 us | 3.00x |
| `literal/sherlock-casei-en` | 160.495 us | 79.749 us | 435.541 us | 2.01x |
| `literal/sherlock-ru` | 179.603 us | 90.740 us | 5,262.100 us | 1.98x |
| `literal-alternate/sherlock-en-mixed` | 196.862 us | 176.629 us | 560.725 us | 1.11x |
| `literal/sherlock-casei-ru` | 238.381 us | 221.402 us | 5,460.233 us | 1.08x |
| `aws-keys/quick` | 3,901.495 us | 3,706.986 us | 35,536.150 us | 1.05x |
| `literal-alternate/sherlock-en` | 377.355 us | 372.183 us | 768.107 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,064.200 us | 2,057.949 us | 2,888.015 us | 1.00x |
| `ruff-noqa/tweaked` | 4,663.200 us | 4,669.476 us | 40,176.300 us | 1.00x |
| `literal-alternate/sherlock-casei-en` | 134.800 us | 148.455 us | 594.773 us | 0.91x |
| `literal-alternate/sherlock-en-nomatch` | 154.367 us | 201.206 us | 576.128 us | 0.77x |
| `literal/sherlock-en` | 56.855 us | 76.737 us | 455.182 us | 0.74x |
| `bounded-repeat/capitals` | 9,441.112 us | 13,613.583 us | 16,599.875 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,549.557 us | 2,361.794 us | 7,597.275 us | 0.66x |
| `bounded-repeat/letters-en` | 1,110.546 us | 1,833.219 us | 1,935.856 us | 0.61x |
| `aws-keys/full` | 10,837.112 us | 18,575.840 us | 53,939.300 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 964.801 us | 2,000.995 us | 7,393.200 us | 0.48x |
| `bounded-repeat/context` | 21,463.050 us | 69,070.800 us | 72,458.400 us | 0.31x |
| `ruff-noqa/real` | 4,658.500 us | 21,203.825 us | 49,042.400 us | 0.22x |
| `dictionary/single` | 8,275.530 us | 45,932.450 us | 45,863.550 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,200.322 us | 471.799 us | 7,790.725 us | 2.54x |
| `common/match-word` | `Match` | 0.195 us | 0.081 us | 1.013 us | 2.42x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.044 us | 0.074 us | 2.04x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 116.408 us | 58.548 us | 277.706 us | 1.99x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.068 us | 0.085 us | 1.79x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.056 us | 0.031 us | 0.049 us | 1.79x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.032 us | 0.049 us | 1.70x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.087 us | 1.61x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.056 us | 0.077 us | 1.60x |
| `common/replace-words` | `Replace` | 27.721 us | 17.697 us | 19.078 us | 1.57x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.050 us | 0.071 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.049 us | 0.069 us | 1.51x |
| `industry/leipzig-river-window-count` | `Count` | 2,201.918 us | 1,532.878 us | 8,020.500 us | 1.44x |
| `industry/leipzig-symbol-count` | `Count` | 1,627.117 us | 1,255.257 us | 7,558.820 us | 1.30x |
| `industry/mariomka-uri-count` | `Count` | 1,429.983 us | 1,110.322 us | 8,423.525 us | 1.29x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.109 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.104 us | 0.092 us | 0.117 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.088 us | 0.123 us | 1.09x |
| `industry/leipzig-twain-count` | `Count` | 1,158.433 us | 1,276.707 us | 7,293.180 us | 0.91x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.092 us | 0.130 us | 0.81x |
| `common/split-words` | `Split` | 14.609 us | 21.128 us | 22.875 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,257.959 us | 3,479.577 us | 10,821.767 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.043 us | 0.073 us | 0.63x |
| `common/matches-words` | `Count` | 9.311 us | 15.259 us | 17.807 us | 0.61x |
| `common/matches-word` | `Count` | 1.359 us | 2.359 us | 3.520 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.200 us | 0.239 us | 0.55x |
| `industry/leipzig-name-family-count` | `Count` | 1,597.055 us | 3,005.388 us | 8,362.950 us | 0.53x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.766 us | 588.366 us | 850.510 us | 0.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.052 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,798.712 us | 8,828.767 us | 9,368.337 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 466.285 us | 3,987.537 us | 4,365.522 us | 0.12x |
| `common/matches-set` | `Count` | 7.164 us | 106.346 us | 108.737 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.049 us | 105.583 us | 107.828 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 241.940 us | 4,830.625 us | 5,253.280 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.426 us | 6.395 us | 31.406 us | 1.47x |
| `lokad/langserv/color-short-hex` | 1.665 us | 1.363 us | 1.483 us | 1.22x |
| `structural/keyword-to-generic-type` | 4.907 us | 4.191 us | 18.515 us | 1.17x |
| `lokad/lexer/doc-line` | 3.242 us | 3.048 us | 3.685 us | 1.06x |
| `lokad/lexer/identifier` | 23.384 us | 23.192 us | 25.707 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.395 us | 7.385 us | 7.971 us | 1.00x |
| `structural/ordered-keyword-pair` | 5.284 us | 5.316 us | 19.492 us | 0.99x |
| `lokad/style/hex-color` | 0.722 us | 0.820 us | 0.866 us | 0.88x |
| `fallback/lookahead` | 5.399 us | 6.165 us | 20.513 us | 0.88x |
| `lokad/langserv/identifier-validator` | 11.108 us | 12.686 us | 14.252 us | 0.88x |
| `lokad/lexer/operator-run` | 2.802 us | 3.477 us | 3.893 us | 0.81x |
| `literal-family/method-token-family` | 7.180 us | 9.302 us | 22.325 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.089 us | 3.051 us | 3.111 us | 0.68x |
| `lokad/docs/inline-doc-prefix` | 3.295 us | 4.917 us | 5.669 us | 0.67x |
| `literal/identifier-token` | 2.826 us | 4.366 us | 17.726 us | 0.65x |
| `literal/call-token` | 2.604 us | 4.331 us | 17.893 us | 0.60x |
| `lokad/lexer/number` | 8.035 us | 14.456 us | 15.586 us | 0.56x |
| `structural/method-family-generic-call` | 3.453 us | 6.781 us | 20.368 us | 0.51x |
| `structural/ordered-identifier-window` | 14.554 us | 28.715 us | 42.134 us | 0.51x |
| `structural/method-family-call` | 3.870 us | 8.013 us | 21.378 us | 0.48x |
| `lokad/style/cell-ref` | 1.426 us | 2.998 us | 3.104 us | 0.48x |
| `literal-family/type-token-family` | 10.495 us | 24.624 us | 38.460 us | 0.43x |
| `literal/identifier-token-casei` | 5.918 us | 15.067 us | 29.075 us | 0.39x |
| `structural/ordered-modifier-pair` | 5.471 us | 17.193 us | 30.921 us | 0.32x |
| `lokad/style/range-ref` | 0.881 us | 2.778 us | 2.736 us | 0.32x |
| `structural/ordered-keyword-window` | 5.477 us | 17.750 us | 31.683 us | 0.31x |
| `structural/modifier-family-to-type-window` | 67.966 us | 258.069 us | 273.816 us | 0.26x |
| `lokad/lexer/string` | 21.362 us | 96.589 us | 101.706 us | 0.22x |
| `lokad/folding/region-marker` | 6.076 us | 37.052 us | 63.359 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.379 us | 24.459 us | 24.953 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.352 us | 217.249 us | 231.108 us | 0.05x |
| `lokad/langserv/url-download` | 2.470 us | 50.087 us | 50.539 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.011 us | 140.219 us | 154.125 us | 0.04x |
| `balancing/begin-end-blocks` | 5.733 us | 4,363.144 us | 4,383.144 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.474 us | 4.476 us | 28.774 us | 2.12x |
| `lokad/langserv/color-short-hex` | 1.653 us | 1.004 us | 1.190 us | 1.65x |
| `lokad/langserv/helper-identifier` | 7.324 us | 4.781 us | 5.935 us | 1.53x |
| `structural/keyword-family-to-capitalized-identifier` | 11.395 us | 7.940 us | 21.292 us | 1.44x |
| `lokad/lexer/doc-line` | 3.246 us | 2.289 us | 2.992 us | 1.42x |
| `structural/keyword-to-generic-type` | 4.927 us | 3.481 us | 17.312 us | 1.42x |
| `lokad/lexer/identifier` | 23.314 us | 16.541 us | 20.601 us | 1.41x |
| `lokad/style/hex-color` | 0.724 us | 0.519 us | 0.617 us | 1.39x |
| `lokad/lexer/operator-run` | 2.801 us | 2.034 us | 2.583 us | 1.38x |
| `structural/ordered-identifier-window` | 14.556 us | 10.686 us | 24.201 us | 1.36x |
| `structural/ordered-keyword-pair` | 5.280 us | 4.151 us | 17.492 us | 1.27x |
| `fallback/lookahead` | 5.401 us | 4.256 us | 18.114 us | 1.27x |
| `structural/modifier-family-to-type-window` | 67.899 us | 56.257 us | 71.515 us | 1.21x |
| `lokad/langserv/identifier-validator` | 11.056 us | 9.271 us | 11.385 us | 1.19x |
| `lokad/docs/inline-doc-prefix` | 3.290 us | 2.908 us | 3.577 us | 1.13x |
| `lokad/lexer/number` | 8.029 us | 7.555 us | 8.683 us | 1.06x |
| `lokad/langserv/color-rgb-hex` | 2.085 us | 1.964 us | 2.178 us | 1.06x |
| `literal/identifier-token-casei` | 5.915 us | 5.954 us | 19.760 us | 0.99x |
| `literal-family/type-token-family` | 10.527 us | 11.547 us | 24.714 us | 0.91x |
| `lokad/style/cell-ref` | 1.426 us | 1.804 us | 1.972 us | 0.79x |
| `structural/method-family-generic-call` | 3.446 us | 4.524 us | 17.445 us | 0.76x |
| `structural/method-family-call` | 3.862 us | 5.078 us | 17.994 us | 0.76x |
| `literal-family/method-token-family` | 3.897 us | 5.330 us | 18.247 us | 0.73x |
| `literal/call-token` | 2.606 us | 3.984 us | 16.954 us | 0.65x |
| `structural/ordered-keyword-window` | 5.486 us | 8.651 us | 22.307 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.459 us | 8.643 us | 22.208 us | 0.63x |
| `lokad/style/range-ref` | 0.881 us | 1.545 us | 1.609 us | 0.57x |
| `literal/identifier-token` | 2.817 us | 5.058 us | 17.701 us | 0.56x |
| `lokad/lexer/string` | 21.459 us | 47.226 us | 49.885 us | 0.45x |
| `lokad/langserv/url-dashboard` | 2.376 us | 7.492 us | 8.138 us | 0.32x |
| `structural/modifier-family-to-type-pair` | 5.024 us | 15.906 us | 24.594 us | 0.32x |
| `lokad/langserv/url-download` | 2.458 us | 12.775 us | 13.458 us | 0.19x |
| `lokad/folding/region-marker` | 6.107 us | 35.883 us | 63.980 us | 0.17x |
| `balancing/begin-end-blocks` | 5.735 us | 470.494 us | 499.118 us | 0.01x |
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
