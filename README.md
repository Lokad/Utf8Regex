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

Core's default Validate policy checks the entire subject, including text after
an early match. PythonRe and POSIX consume existing valid text; PCRE2 checks its
native UTF-mode window and has an explicit trusted-input bypass.
Byte coordinates are primary; UTF-16 coordinates and strings are derived
conveniences. Keep borrowed input unchanged and its owner alive while a view or
cursor uses it. Owned snapshots remain valid after cursor advance or disposal.

## Subject input contracts

Core selects its validation policy once at construction or through its factory:

```csharp
var regex = new Utf8Regex("needle", inputValidation: Utf8InputValidation.AssumeValid);
// regex.InputValidation is read-only.
```

`Validate` (default) checks the whole subject at most once per operation or cursor
lifetime. `AssumeValid` omits that pass when the caller guarantees valid UTF-8 and
unchanged borrowed bytes. Core operations and cursors inherit this choice.
PythonRe models Python `re` over `str`; PythonRe and POSIX require already-valid
UTF-8 text and omit regex-operation validity passes. Validate external bytes where
they become text. PCRE2 retains explicit UTF-mode validation controls.
Pattern/replacement
validation, checked memory access, scalar semantics and cooperative cancellation
remain. Coordinate/result work may still scan input. Read the
[contract](docs/SPEC.md#subject-input-validation) before choosing AssumeValid.
Historical [policy measurements](bench/Experiments/InputValidation-20261110/README.md)
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

The tables were refreshed on **14 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks completed in **46m54s**. The [run receipt](bench/Experiments/DeliveryDev92-20261114/full-refresh.json)
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
| `literal/sherlock-zh` | 69.727 us | 18.757 us | 2,126.100 us | 3.72x |
| `literal/sherlock-ru` | 159.153 us | 97.334 us | 5,123.707 us | 1.64x |
| `literal-alternate/sherlock-zh` | 91.584 us | 68.296 us | 2,164.665 us | 1.34x |
| `literal/sherlock-casei-en` | 153.349 us | 183.464 us | 576.910 us | 0.84x |
| `literal/sherlock-en` | 63.562 us | 83.681 us | 442.028 us | 0.76x |
| `literal/sherlock-casei-ru` | 240.076 us | 356.970 us | 5,677.438 us | 0.67x |
| `ruff-noqa/tweaked` | 4,629.782 us | 7,433.436 us | 41,489.400 us | 0.62x |
| `aws-keys/quick` | 4,113.267 us | 9,607.525 us | 39,799.150 us | 0.43x |
| `bounded-repeat/letters-en` | 1,090.554 us | 2,768.464 us | 2,891.623 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,082.049 us | 5,756.229 us | 6,567.383 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,612.789 us | 4,755.744 us | 9,707.375 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 188.562 us | 584.061 us | 1,003.111 us | 0.32x |
| `dictionary/single` | 8,091.730 us | 25,501.767 us | 25,489.533 us | 0.32x |
| `literal-alternate/sherlock-en` | 366.195 us | 1,494.442 us | 2,007.245 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 159.190 us | 658.017 us | 1,086.951 us | 0.24x |
| `bounded-repeat/capitals` | 9,394.725 us | 45,979.350 us | 49,812.700 us | 0.20x |
| `bounded-repeat/context` | 20,814.000 us | 146,110.300 us | 151,060.600 us | 0.14x |
| `aws-keys/full` | 8,928.211 us | 85,123.900 us | 114,487.600 us | 0.10x |
| `ruff-noqa/real` | 4,572.041 us | 73,002.250 us | 102,192.400 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 955.351 us | 22,450.475 us | 27,635.200 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 164.993 us | 11,208.486 us | 12,077.229 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 908.442 us | 479.676 us | 8,698.825 us | 1.89x |
| `common/match-word` | `Match` | 0.191 us | 0.165 us | 1.108 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,287.533 us | 1,293.807 us | 8,374.525 us | 1.00x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.075 us | 0.108 us | 0.144 us | 0.70x |
| `industry/mariomka-uri-count` | `Count` | 1,153.900 us | 1,888.200 us | 10,205.850 us | 0.61x |
| `common/uri-miss` | `IsMatch` | 0.114 us | 0.188 us | 0.208 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.083 us | 0.100 us | 0.58x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.160 us | 0.177 us | 0.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 116.235 us | 249.626 us | 487.822 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.175 us | 0.191 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.087 us | 0.232 us | 0.265 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.189 us | 0.210 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.263 us | 0.289 us | 0.36x |
| `common/replace-words` | `Replace` | 27.131 us | 76.238 us | 76.984 us | 0.36x |
| `common/matches-word` | `Count` | 1.319 us | 4.162 us | 5.898 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.342 us | 0.363 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.107 us | 0.136 us | 0.26x |
| `industry/mariomka-ip-count` | `Count` | 2,074.030 us | 8,122.290 us | 16,678.467 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.080 us | 0.096 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 180.026 us | 943.675 us | 1,220.949 us | 0.19x |
| `common/split-words` | `Split` | 14.322 us | 75.973 us | 77.695 us | 0.19x |
| `industry/leipzig-name-family-count` | `Count` | 1,751.577 us | 9,735.425 us | 16,612.833 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.574 us | 0.595 us | 0.17x |
| `common/matches-words` | `Count` | 9.078 us | 68.258 us | 70.034 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.089 us | 0.12x |
| `common/one-node-backtracking` | `IsMatch` | 0.072 us | 0.601 us | 0.638 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.084 us | 0.812 us | 0.852 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,767.784 us | 19,345.175 us | 20,001.525 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,620.613 us | 22,573.575 us | 29,935.500 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,157.446 us | 31,951.267 us | 38,902.200 us | 0.07x |
| `common/matches-boundary` | `Count` | 9.340 us | 160.679 us | 160.667 us | 0.06x |
| `common/matches-set` | `Count` | 10.377 us | 192.815 us | 193.815 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 457.882 us | 8,525.922 us | 9,083.900 us | 0.05x |
| `industry/rust-sherlock-ing-count` | `Count` | 270.690 us | 11,757.786 us | 12,285.786 us | 0.02x |
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
| `literal/sherlock-zh` | 69.027 us | 23.611 us | 2,109.856 us | 2.92x |
| `literal-alternate/sherlock-zh` | 91.698 us | 36.066 us | 2,133.019 us | 2.54x |
| `literal/sherlock-casei-en` | 152.708 us | 77.675 us | 456.899 us | 1.97x |
| `literal/sherlock-ru` | 160.069 us | 88.738 us | 5,104.127 us | 1.80x |
| `literal-alternate/sherlock-casei-en` | 164.820 us | 132.965 us | 595.061 us | 1.24x |
| `literal/sherlock-casei-ru` | 241.477 us | 195.973 us | 5,459.520 us | 1.23x |
| `aws-keys/quick` | 4,106.711 us | 3,673.857 us | 35,158.600 us | 1.12x |
| `literal-alternate/sherlock-en` | 366.886 us | 332.723 us | 727.971 us | 1.10x |
| `literal-alternate/sherlock-en-mixed` | 188.940 us | 176.176 us | 562.450 us | 1.07x |
| `bounded-repeat/letters-ru` | 2,096.805 us | 2,031.466 us | 2,854.411 us | 1.03x |
| `ruff-noqa/tweaked` | 4,598.306 us | 4,496.071 us | 35,934.100 us | 1.02x |
| `literal/sherlock-en` | 63.746 us | 74.013 us | 416.152 us | 0.86x |
| `literal-alternate/sherlock-en-nomatch` | 158.896 us | 201.812 us | 584.851 us | 0.79x |
| `bounded-repeat/capitals` | 9,453.025 us | 13,394.867 us | 16,249.875 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,606.680 us | 2,308.530 us | 7,328.733 us | 0.70x |
| `bounded-repeat/letters-en` | 1,089.831 us | 1,802.960 us | 1,899.031 us | 0.60x |
| `aws-keys/full` | 10,676.538 us | 18,262.940 us | 49,497.633 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 946.141 us | 1,912.332 us | 7,032.150 us | 0.49x |
| `bounded-repeat/context` | 20,671.475 us | 68,318.400 us | 70,881.900 us | 0.30x |
| `ruff-noqa/real` | 4,570.394 us | 20,522.300 us | 52,468.767 us | 0.22x |
| `dictionary/single` | 8,103.420 us | 45,242.300 us | 45,177.100 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.191 us | 0.076 us | 0.997 us | 2.50x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.064 us | 0.029 us | 0.045 us | 2.17x |
| `industry/mariomka-email-count` | `Count` | 916.395 us | 444.556 us | 7,471.575 us | 2.06x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 116.406 us | 58.419 us | 255.909 us | 1.99x |
| `common/backtracking` | `IsMatch` | 0.083 us | 0.042 us | 0.072 us | 1.97x |
| `common/uri-miss` | `IsMatch` | 0.113 us | 0.066 us | 0.084 us | 1.71x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.054 us | 0.075 us | 1.58x |
| `common/replace-words` | `Replace` | 27.325 us | 17.379 us | 20.119 us | 1.57x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.049 us | 0.086 us | 1.55x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.032 us | 0.050 us | 1.54x |
| `industry/leipzig-river-window-count` | `Count` | 2,170.300 us | 1,517.220 us | 7,598.525 us | 1.43x |
| `common/ip-match` | `IsMatch` | 0.068 us | 0.048 us | 0.068 us | 1.43x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.049 us | 0.070 us | 1.40x |
| `industry/leipzig-symbol-count` | `Count` | 1,620.463 us | 1,192.963 us | 7,056.940 us | 1.36x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.080 us | 0.107 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.088 us | 0.114 us | 1.12x |
| `industry/mariomka-uri-count` | `Count` | 1,156.348 us | 1,074.599 us | 8,229.350 us | 1.08x |
| `industry/leipzig-twain-count` | `Count` | 1,295.622 us | 1,289.052 us | 6,555.667 us | 1.01x |
| `common/uri-match` | `IsMatch` | 0.087 us | 0.087 us | 0.119 us | 0.99x |
| `common/matches-words` | `Count` | 9.057 us | 10.883 us | 13.875 us | 0.83x |
| `common/one-node-backtracking` | `IsMatch` | 0.071 us | 0.090 us | 0.126 us | 0.79x |
| `common/split-words` | `Split` | 14.318 us | 20.758 us | 22.669 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.071 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,070.826 us | 3,434.204 us | 10,791.333 us | 0.60x |
| `industry/leipzig-name-family-count` | `Count` | 1,758.466 us | 3,063.342 us | 8,114.500 us | 0.57x |
| `common/matches-word` | `Count` | 1.318 us | 2.314 us | 3.376 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.199 us | 0.233 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.034 us | 0.052 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.321 us | 584.883 us | 839.215 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,760.093 us | 8,541.644 us | 9,124.900 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 455.128 us | 3,855.620 us | 4,289.568 us | 0.12x |
| `common/matches-set` | `Count` | 10.317 us | 104.428 us | 106.345 us | 0.10x |
| `common/matches-boundary` | `Count` | 9.348 us | 102.751 us | 104.750 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 275.451 us | 4,682.812 us | 5,101.873 us | 0.06x |
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
| `lokad/imports/module-imports` | 9.127 us | 6.274 us | 31.070 us | 1.45x |
| `structural/keyword-to-generic-type` | 5.424 us | 4.056 us | 18.482 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.653 us | 1.310 us | 1.381 us | 1.26x |
| `lokad/lexer/doc-line` | 3.212 us | 3.026 us | 3.509 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.476 us | 5.250 us | 19.120 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.532 us | 7.311 us | 7.630 us | 1.03x |
| `lokad/lexer/identifier` | 23.199 us | 22.684 us | 24.745 us | 1.02x |
| `fallback/lookahead` | 5.597 us | 5.990 us | 19.454 us | 0.93x |
| `lokad/langserv/identifier-validator` | 10.967 us | 12.224 us | 13.714 us | 0.90x |
| `lokad/lexer/operator-run` | 2.704 us | 3.398 us | 3.663 us | 0.80x |
| `literal-family/method-token-family` | 7.039 us | 8.943 us | 22.049 us | 0.79x |
| `lokad/style/hex-color` | 0.563 us | 0.808 us | 0.850 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.213 us | 4.734 us | 5.357 us | 0.68x |
| `lokad/langserv/color-rgb-hex` | 1.972 us | 2.912 us | 2.920 us | 0.68x |
| `literal/identifier-token` | 2.731 us | 4.288 us | 17.650 us | 0.64x |
| `literal/call-token` | 2.545 us | 4.247 us | 17.868 us | 0.60x |
| `lokad/lexer/number` | 7.815 us | 14.095 us | 14.820 us | 0.55x |
| `structural/ordered-identifier-window` | 14.263 us | 28.299 us | 41.381 us | 0.50x |
| `lokad/style/cell-ref` | 1.395 us | 2.825 us | 2.846 us | 0.49x |
| `structural/method-family-call` | 3.782 us | 8.048 us | 21.254 us | 0.47x |
| `structural/method-family-generic-call` | 3.356 us | 7.154 us | 20.020 us | 0.47x |
| `literal-family/type-token-family` | 10.289 us | 22.941 us | 37.033 us | 0.45x |
| `literal/identifier-token-casei` | 5.277 us | 14.899 us | 28.844 us | 0.35x |
| `lokad/style/range-ref` | 0.865 us | 2.687 us | 2.678 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.144 us | 16.954 us | 30.429 us | 0.30x |
| `structural/ordered-keyword-window` | 5.326 us | 17.620 us | 31.504 us | 0.30x |
| `structural/modifier-family-to-type-window` | 66.388 us | 251.728 us | 265.708 us | 0.26x |
| `lokad/lexer/string` | 20.714 us | 93.309 us | 98.690 us | 0.22x |
| `lokad/folding/region-marker` | 6.252 us | 36.041 us | 63.345 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.388 us | 24.162 us | 24.714 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.251 us | 215.659 us | 230.580 us | 0.05x |
| `lokad/langserv/url-download` | 2.409 us | 51.656 us | 53.076 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.904 us | 133.296 us | 147.193 us | 0.04x |
| `balancing/begin-end-blocks` | 5.646 us | 4,367.956 us | 4,387.661 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.120 us | 4.345 us | 28.038 us | 2.10x |
| `lokad/langserv/color-short-hex` | 1.626 us | 0.912 us | 1.105 us | 1.78x |
| `lokad/langserv/helper-identifier` | 7.323 us | 4.551 us | 5.464 us | 1.61x |
| `structural/keyword-to-generic-type` | 5.426 us | 3.489 us | 17.210 us | 1.56x |
| `lokad/lexer/doc-line` | 3.174 us | 2.130 us | 2.844 us | 1.49x |
| `lokad/lexer/operator-run` | 2.729 us | 1.872 us | 2.444 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.178 us | 7.820 us | 21.117 us | 1.43x |
| `lokad/lexer/identifier` | 23.065 us | 16.214 us | 19.980 us | 1.42x |
| `structural/ordered-identifier-window` | 14.285 us | 10.329 us | 23.745 us | 1.38x |
| `fallback/lookahead` | 5.596 us | 4.162 us | 17.237 us | 1.34x |
| `structural/ordered-keyword-pair` | 5.493 us | 4.108 us | 17.364 us | 1.34x |
| `lokad/langserv/identifier-validator` | 11.045 us | 8.679 us | 10.817 us | 1.27x |
| `structural/modifier-family-to-type-window` | 67.003 us | 54.944 us | 69.521 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.214 us | 2.696 us | 3.425 us | 1.19x |
| `lokad/style/hex-color` | 0.557 us | 0.489 us | 0.576 us | 1.14x |
| `lokad/langserv/color-rgb-hex` | 1.961 us | 1.737 us | 1.953 us | 1.13x |
| `lokad/lexer/number` | 7.795 us | 6.916 us | 8.150 us | 1.13x |
| `literal-family/type-token-family` | 10.306 us | 11.238 us | 24.493 us | 0.92x |
| `literal/identifier-token-casei` | 5.288 us | 5.851 us | 19.700 us | 0.90x |
| `lokad/style/cell-ref` | 1.388 us | 1.730 us | 1.867 us | 0.80x |
| `structural/method-family-generic-call` | 3.363 us | 4.399 us | 17.010 us | 0.76x |
| `structural/method-family-call` | 3.771 us | 5.017 us | 17.716 us | 0.75x |
| `literal-family/method-token-family` | 3.787 us | 5.188 us | 18.041 us | 0.73x |
| `literal/call-token` | 2.539 us | 3.924 us | 16.889 us | 0.65x |
| `structural/ordered-keyword-window` | 5.281 us | 8.471 us | 21.893 us | 0.62x |
| `structural/ordered-modifier-pair` | 5.098 us | 8.445 us | 21.879 us | 0.60x |
| `lokad/style/range-ref` | 0.864 us | 1.443 us | 1.547 us | 0.60x |
| `literal/identifier-token` | 2.744 us | 4.657 us | 17.263 us | 0.59x |
| `lokad/lexer/string` | 20.643 us | 44.887 us | 47.987 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 4.904 us | 15.345 us | 24.268 us | 0.32x |
| `lokad/langserv/url-dashboard` | 2.320 us | 7.361 us | 8.049 us | 0.32x |
| `lokad/langserv/url-download` | 2.442 us | 12.982 us | 13.468 us | 0.19x |
| `lokad/folding/region-marker` | 6.302 us | 35.863 us | 61.946 us | 0.18x |
| `balancing/begin-end-blocks` | 5.605 us | 455.468 us | 494.643 us | 0.01x |
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
