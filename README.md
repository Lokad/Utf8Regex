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
Core's default Validate policy checks this. PythonRe and POSIX consume existing
valid text; PCRE2 has its own UTF-mode checking and trusted-input bypass.
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

The tables were refreshed on **13 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks completed in **46m54s**. The [run receipt](bench/Experiments/DeliveryDev84-20261114/full-refresh.json)
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
| `literal/sherlock-zh` | 67.527 us | 18.078 us | 2,113.436 us | 3.74x |
| `literal/sherlock-ru` | 158.484 us | 96.515 us | 5,317.933 us | 1.64x |
| `literal-alternate/sherlock-zh` | 90.294 us | 66.844 us | 2,140.089 us | 1.35x |
| `literal/sherlock-casei-en` | 152.715 us | 188.107 us | 574.896 us | 0.81x |
| `literal/sherlock-en` | 63.710 us | 85.980 us | 459.020 us | 0.74x |
| `literal/sherlock-casei-ru` | 229.902 us | 352.557 us | 5,421.886 us | 0.65x |
| `ruff-noqa/tweaked` | 4,452.076 us | 7,309.873 us | 42,939.100 us | 0.61x |
| `aws-keys/quick` | 4,010.595 us | 9,323.144 us | 45,045.350 us | 0.43x |
| `bounded-repeat/letters-en` | 1,075.794 us | 2,753.893 us | 2,894.322 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,013.062 us | 5,629.338 us | 6,494.983 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 197.516 us | 573.575 us | 998.091 us | 0.34x |
| `literal-alternate/sherlock-ru` | 1,583.183 us | 4,611.300 us | 9,504.150 us | 0.34x |
| `dictionary/single` | 8,062.720 us | 24,919.075 us | 24,976.550 us | 0.32x |
| `literal-alternate/sherlock-en` | 375.337 us | 1,480.688 us | 2,001.833 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 156.016 us | 647.866 us | 1,090.762 us | 0.24x |
| `bounded-repeat/capitals` | 9,169.756 us | 44,743.950 us | 48,139.750 us | 0.20x |
| `bounded-repeat/context` | 22,435.975 us | 144,125.400 us | 149,761.900 us | 0.16x |
| `aws-keys/full` | 10,996.529 us | 85,319.400 us | 113,224.100 us | 0.13x |
| `ruff-noqa/real` | 4,520.176 us | 68,087.250 us | 97,049.200 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 941.966 us | 22,447.325 us | 27,711.000 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 163.829 us | 11,396.345 us | 11,984.555 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 899.085 us | 469.401 us | 8,273.550 us | 1.92x |
| `common/match-word` | `Match` | 0.188 us | 0.164 us | 1.093 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,279.368 us | 1,297.992 us | 8,503.275 us | 0.99x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.107 us | 0.140 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.178 us | 0.197 us | 0.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.081 us | 0.098 us | 0.61x |
| `industry/mariomka-uri-count` | `Count` | 1,115.903 us | 1,862.307 us | 10,029.475 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.165 us | 0.180 us | 0.53x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.174 us | 0.194 us | 0.43x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.226 us | 0.256 us | 0.42x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 107.311 us | 258.217 us | 507.230 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.073 us | 0.186 us | 0.204 us | 0.40x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.262 us | 0.288 us | 0.37x |
| `common/replace-words` | `Replace` | 26.573 us | 74.293 us | 75.671 us | 0.36x |
| `common/matches-word` | `Count` | 1.311 us | 4.132 us | 5.768 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.335 us | 0.360 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.102 us | 0.133 us | 0.27x |
| `industry/mariomka-ip-count` | `Count` | 2,089.586 us | 8,100.030 us | 16,729.767 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.080 us | 0.095 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 172.674 us | 929.101 us | 1,189.261 us | 0.19x |
| `common/split-words` | `Split` | 14.005 us | 75.431 us | 76.131 us | 0.19x |
| `industry/leipzig-name-family-count` | `Count` | 1,685.311 us | 9,570.478 us | 16,546.633 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.094 us | 0.540 us | 0.569 us | 0.17x |
| `common/matches-words` | `Count` | 9.045 us | 67.895 us | 69.174 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.075 us | 0.088 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.071 us | 0.594 us | 0.628 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.085 us | 0.800 us | 0.840 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,760.307 us | 18,927.475 us | 19,722.025 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,698.330 us | 22,409.950 us | 29,635.750 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,170.581 us | 31,530.400 us | 38,481.250 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 486.659 us | 7,971.580 us | 8,487.022 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.835 us | 164.672 us | 164.719 us | 0.04x |
| `common/matches-set` | `Count` | 6.799 us | 189.411 us | 189.254 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 238.962 us | 11,600.600 us | 12,228.686 us | 0.02x |
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
| `literal/sherlock-zh` | 67.601 us | 22.796 us | 2,120.124 us | 2.97x |
| `literal-alternate/sherlock-zh` | 89.742 us | 35.281 us | 2,070.694 us | 2.54x |
| `literal/sherlock-casei-en` | 152.857 us | 79.537 us | 438.337 us | 1.92x |
| `literal/sherlock-ru` | 157.689 us | 87.927 us | 5,193.153 us | 1.79x |
| `literal-alternate/sherlock-casei-en` | 162.732 us | 128.854 us | 607.960 us | 1.26x |
| `literal/sherlock-casei-ru` | 229.679 us | 191.574 us | 5,164.379 us | 1.20x |
| `literal-alternate/sherlock-en-mixed` | 196.589 us | 170.293 us | 515.563 us | 1.15x |
| `aws-keys/quick` | 4,057.374 us | 3,539.432 us | 36,755.300 us | 1.15x |
| `literal-alternate/sherlock-en` | 377.282 us | 359.939 us | 796.123 us | 1.05x |
| `bounded-repeat/letters-ru` | 2,015.703 us | 1,978.925 us | 2,817.342 us | 1.02x |
| `ruff-noqa/tweaked` | 4,442.206 us | 4,397.718 us | 38,042.650 us | 1.01x |
| `literal/sherlock-en` | 63.594 us | 75.658 us | 405.126 us | 0.84x |
| `literal-alternate/sherlock-en-nomatch` | 157.040 us | 199.516 us | 589.726 us | 0.79x |
| `literal-alternate/sherlock-ru` | 1,580.549 us | 2,257.200 us | 7,161.883 us | 0.70x |
| `bounded-repeat/capitals` | 9,235.850 us | 13,227.850 us | 16,009.175 us | 0.70x |
| `aws-keys/full` | 11,094.329 us | 18,023.160 us | 49,328.400 us | 0.62x |
| `bounded-repeat/letters-en` | 1,067.228 us | 1,777.202 us | 1,887.020 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 943.011 us | 1,896.685 us | 6,953.675 us | 0.50x |
| `bounded-repeat/context` | 22,743.525 us | 67,735.300 us | 70,443.550 us | 0.34x |
| `ruff-noqa/real` | 4,528.018 us | 20,073.950 us | 50,799.733 us | 0.23x |
| `dictionary/single` | 8,036.200 us | 44,297.350 us | 44,653.000 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.188 us | 0.073 us | 0.976 us | 2.57x |
| `common/backtracking` | `IsMatch` | 0.085 us | 0.041 us | 0.072 us | 2.07x |
| `industry/mariomka-email-count` | `Count` | 899.651 us | 440.023 us | 7,567.450 us | 2.04x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 106.626 us | 57.297 us | 276.961 us | 1.86x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.065 us | 0.083 us | 1.82x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.053 us | 0.029 us | 0.045 us | 1.80x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.031 us | 0.049 us | 1.73x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.054 us | 0.075 us | 1.62x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.048 us | 0.083 us | 1.60x |
| `common/replace-words` | `Replace` | 26.806 us | 16.801 us | 18.982 us | 1.60x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.047 us | 0.067 us | 1.51x |
| `industry/leipzig-river-window-count` | `Count` | 2,171.560 us | 1,464.302 us | 7,602.050 us | 1.48x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.048 us | 0.067 us | 1.45x |
| `industry/leipzig-symbol-count` | `Count` | 1,710.675 us | 1,236.356 us | 7,191.460 us | 1.38x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.080 us | 0.107 us | 1.22x |
| `common/email-miss` | `IsMatch` | 0.095 us | 0.087 us | 0.112 us | 1.09x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.086 us | 0.119 us | 1.05x |
| `industry/mariomka-uri-count` | `Count` | 1,122.021 us | 1,079.704 us | 8,159.700 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,295.403 us | 1,282.077 us | 6,607.067 us | 1.01x |
| `common/one-node-backtracking` | `IsMatch` | 0.071 us | 0.095 us | 0.125 us | 0.75x |
| `common/split-words` | `Split` | 14.029 us | 20.191 us | 22.107 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.041 us | 0.070 us | 0.66x |
| `common/matches-words` | `Count` | 9.036 us | 14.591 us | 17.001 us | 0.62x |
| `industry/mariomka-ip-count` | `Count` | 2,083.239 us | 3,415.561 us | 10,728.800 us | 0.61x |
| `common/matches-word` | `Count` | 1.311 us | 2.284 us | 3.363 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,679.302 us | 2,982.385 us | 7,935.525 us | 0.56x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.194 us | 0.226 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.034 us | 0.051 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.436 us | 575.523 us | 830.546 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,748.539 us | 8,623.956 us | 9,224.825 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 488.110 us | 3,927.045 us | 4,309.194 us | 0.12x |
| `common/matches-set` | `Count` | 6.903 us | 101.716 us | 103.770 us | 0.07x |
| `common/matches-boundary` | `Count` | 6.872 us | 101.914 us | 103.913 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 240.239 us | 4,688.062 us | 5,096.253 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.652 us | 6.153 us | 30.320 us | 1.57x |
| `lokad/langserv/color-short-hex` | 1.583 us | 1.277 us | 1.404 us | 1.24x |
| `structural/keyword-to-generic-type` | 4.830 us | 4.018 us | 18.216 us | 1.20x |
| `lokad/lexer/doc-line` | 3.148 us | 2.957 us | 3.569 us | 1.06x |
| `lokad/langserv/helper-identifier` | 7.271 us | 7.123 us | 7.591 us | 1.02x |
| `lokad/lexer/identifier` | 22.957 us | 22.507 us | 24.958 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.405 us | 5.318 us | 19.573 us | 1.02x |
| `lokad/style/hex-color` | 0.694 us | 0.767 us | 0.811 us | 0.90x |
| `fallback/lookahead` | 5.338 us | 5.919 us | 19.328 us | 0.90x |
| `lokad/langserv/identifier-validator` | 10.751 us | 12.089 us | 13.099 us | 0.89x |
| `lokad/lexer/operator-run` | 2.722 us | 3.446 us | 3.744 us | 0.79x |
| `literal-family/method-token-family` | 6.981 us | 8.892 us | 22.051 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 1.932 us | 2.872 us | 2.820 us | 0.67x |
| `lokad/docs/inline-doc-prefix` | 3.157 us | 4.808 us | 5.248 us | 0.66x |
| `literal/identifier-token` | 2.754 us | 4.318 us | 18.060 us | 0.64x |
| `literal/call-token` | 2.558 us | 4.267 us | 17.587 us | 0.60x |
| `lokad/lexer/number` | 7.713 us | 13.552 us | 14.526 us | 0.57x |
| `structural/method-family-generic-call` | 3.356 us | 6.522 us | 19.689 us | 0.51x |
| `lokad/style/cell-ref` | 1.385 us | 2.752 us | 2.860 us | 0.50x |
| `structural/ordered-identifier-window` | 13.921 us | 27.866 us | 41.573 us | 0.50x |
| `structural/method-family-call` | 3.784 us | 7.842 us | 20.957 us | 0.48x |
| `literal-family/type-token-family` | 10.318 us | 23.295 us | 37.050 us | 0.44x |
| `literal/identifier-token-casei` | 5.821 us | 15.082 us | 28.214 us | 0.39x |
| `lokad/style/range-ref` | 0.848 us | 2.644 us | 2.609 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.082 us | 16.534 us | 30.480 us | 0.31x |
| `structural/ordered-keyword-window` | 5.179 us | 17.123 us | 30.233 us | 0.30x |
| `structural/modifier-family-to-type-window` | 65.865 us | 248.056 us | 265.505 us | 0.27x |
| `lokad/lexer/string` | 20.374 us | 91.859 us | 96.506 us | 0.22x |
| `lokad/folding/region-marker` | 6.336 us | 32.642 us | 62.410 us | 0.19x |
| `lokad/langserv/url-dashboard` | 2.336 us | 23.797 us | 24.072 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.083 us | 213.206 us | 228.742 us | 0.05x |
| `lokad/langserv/url-download` | 2.412 us | 49.022 us | 49.468 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.742 us | 126.485 us | 139.994 us | 0.04x |
| `balancing/begin-end-blocks` | 5.650 us | 4,233.528 us | 4,248.239 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.600 us | 4.227 us | 27.964 us | 2.27x |
| `lokad/langserv/color-short-hex` | 1.591 us | 0.940 us | 1.108 us | 1.69x |
| `lokad/langserv/helper-identifier` | 7.217 us | 4.412 us | 5.424 us | 1.64x |
| `lokad/style/hex-color` | 0.696 us | 0.470 us | 0.558 us | 1.48x |
| `lokad/lexer/doc-line` | 3.150 us | 2.189 us | 2.824 us | 1.44x |
| `structural/keyword-family-to-capitalized-identifier` | 11.089 us | 7.738 us | 21.323 us | 1.43x |
| `lokad/lexer/identifier` | 22.684 us | 16.289 us | 20.252 us | 1.39x |
| `structural/keyword-to-generic-type` | 4.811 us | 3.456 us | 17.109 us | 1.39x |
| `lokad/lexer/operator-run` | 2.705 us | 1.961 us | 2.453 us | 1.38x |
| `structural/ordered-identifier-window` | 14.047 us | 10.392 us | 24.385 us | 1.35x |
| `structural/ordered-keyword-pair` | 5.386 us | 4.081 us | 17.435 us | 1.32x |
| `fallback/lookahead` | 5.337 us | 4.122 us | 17.114 us | 1.29x |
| `lokad/langserv/identifier-validator` | 10.791 us | 8.705 us | 10.286 us | 1.24x |
| `structural/modifier-family-to-type-window` | 65.341 us | 54.400 us | 69.442 us | 1.20x |
| `lokad/docs/inline-doc-prefix` | 3.163 us | 2.696 us | 3.444 us | 1.17x |
| `lokad/langserv/color-rgb-hex` | 1.927 us | 1.731 us | 1.907 us | 1.11x |
| `lokad/lexer/number` | 7.674 us | 7.109 us | 7.966 us | 1.08x |
| `literal/identifier-token-casei` | 5.806 us | 5.883 us | 19.549 us | 0.99x |
| `literal-family/type-token-family` | 10.339 us | 11.406 us | 24.441 us | 0.91x |
| `lokad/style/cell-ref` | 1.381 us | 1.668 us | 1.863 us | 0.83x |
| `structural/method-family-call` | 3.792 us | 4.971 us | 17.693 us | 0.76x |
| `structural/method-family-generic-call` | 3.310 us | 4.379 us | 16.871 us | 0.76x |
| `literal-family/method-token-family` | 3.792 us | 5.193 us | 17.908 us | 0.73x |
| `literal/call-token` | 2.557 us | 3.929 us | 16.695 us | 0.65x |
| `structural/ordered-keyword-window` | 5.120 us | 8.281 us | 21.310 us | 0.62x |
| `structural/ordered-modifier-pair` | 5.093 us | 8.315 us | 21.968 us | 0.61x |
| `literal/identifier-token` | 2.754 us | 4.635 us | 17.658 us | 0.59x |
| `lokad/style/range-ref` | 0.851 us | 1.477 us | 1.558 us | 0.58x |
| `lokad/lexer/string` | 20.615 us | 43.077 us | 46.116 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 4.726 us | 10.169 us | 23.294 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.323 us | 7.213 us | 7.774 us | 0.32x |
| `lokad/langserv/url-download` | 2.427 us | 12.497 us | 13.436 us | 0.19x |
| `lokad/folding/region-marker` | 6.423 us | 34.572 us | 61.309 us | 0.19x |
| `balancing/begin-end-blocks` | 5.646 us | 452.752 us | 486.219 us | 0.01x |
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
