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
and resource checks completed in **47m13s**. The [run receipt](bench/Experiments/DeliveryDev93-20261114/full-refresh.json)
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
| `literal/sherlock-zh` | 69.776 us | 18.675 us | 2,235.000 us | 3.74x |
| `literal/sherlock-ru` | 160.211 us | 100.571 us | 5,278.229 us | 1.59x |
| `literal-alternate/sherlock-zh` | 89.352 us | 66.981 us | 2,169.143 us | 1.33x |
| `literal/sherlock-casei-en` | 152.161 us | 191.220 us | 588.742 us | 0.80x |
| `literal/sherlock-en` | 63.767 us | 85.029 us | 464.076 us | 0.75x |
| `literal/sherlock-casei-ru` | 238.199 us | 359.025 us | 5,490.793 us | 0.66x |
| `ruff-noqa/tweaked` | 4,667.325 us | 7,466.664 us | 40,750.350 us | 0.63x |
| `aws-keys/quick` | 4,000.026 us | 9,630.562 us | 45,910.450 us | 0.42x |
| `bounded-repeat/letters-en` | 1,104.934 us | 2,793.871 us | 2,918.158 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,083.616 us | 5,718.414 us | 6,583.875 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,604.891 us | 4,704.441 us | 10,100.875 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 202.518 us | 614.702 us | 1,045.244 us | 0.33x |
| `dictionary/single` | 8,210.320 us | 25,776.233 us | 25,662.633 us | 0.32x |
| `literal-alternate/sherlock-en` | 384.634 us | 1,476.880 us | 2,001.840 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 159.559 us | 666.439 us | 1,083.505 us | 0.24x |
| `bounded-repeat/capitals` | 9,466.562 us | 45,885.050 us | 49,133.450 us | 0.21x |
| `bounded-repeat/context` | 20,748.800 us | 146,299.100 us | 152,357.900 us | 0.14x |
| `aws-keys/full` | 8,950.544 us | 84,994.100 us | 113,952.900 us | 0.11x |
| `ruff-noqa/real` | 4,626.441 us | 71,605.400 us | 104,829.900 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 982.311 us | 22,917.125 us | 28,464.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 162.843 us | 11,400.714 us | 12,379.071 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 927.565 us | 488.065 us | 8,906.625 us | 1.90x |
| `common/match-word` | `Match` | 0.193 us | 0.168 us | 1.115 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,262.662 us | 1,284.526 us | 8,408.744 us | 0.98x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.109 us | 0.148 us | 0.72x |
| `industry/mariomka-uri-count` | `Count` | 1,162.078 us | 1,847.837 us | 9,810.200 us | 0.63x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.197 us | 0.218 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.082 us | 0.098 us | 0.58x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.084 us | 0.158 us | 0.177 us | 0.53x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 111.835 us | 250.907 us | 479.317 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.172 us | 0.193 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.087 us | 0.228 us | 0.260 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.185 us | 0.203 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.263 us | 0.292 us | 0.36x |
| `common/replace-words` | `Replace` | 27.172 us | 75.212 us | 75.601 us | 0.36x |
| `common/matches-word` | `Count` | 1.321 us | 4.146 us | 5.864 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.345 us | 0.368 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.105 us | 0.135 us | 0.26x |
| `industry/mariomka-ip-count` | `Count` | 2,100.830 us | 8,078.930 us | 16,564.400 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.096 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 173.905 us | 931.138 us | 1,220.437 us | 0.19x |
| `common/split-words` | `Split` | 14.358 us | 77.055 us | 79.433 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.101 us | 0.556 us | 0.589 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,736.298 us | 9,636.938 us | 16,417.633 us | 0.18x |
| `common/matches-words` | `Count` | 9.240 us | 68.440 us | 70.213 us | 0.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.074 us | 0.088 us | 0.12x |
| `common/one-node-backtracking` | `IsMatch` | 0.073 us | 0.608 us | 0.645 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.086 us | 0.823 us | 0.867 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,764.042 us | 19,035.075 us | 19,751.025 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,702.358 us | 23,260.400 us | 30,514.700 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,167.111 us | 31,611.500 us | 38,911.900 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 492.551 us | 8,149.260 us | 8,621.589 us | 0.06x |
| `common/matches-boundary` | `Count` | 9.483 us | 161.730 us | 162.554 us | 0.06x |
| `common/matches-set` | `Count` | 10.308 us | 191.075 us | 193.222 us | 0.05x |
| `industry/rust-sherlock-ing-count` | `Count` | 241.053 us | 11,805.800 us | 12,350.483 us | 0.02x |
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
| `literal/sherlock-zh` | 69.887 us | 23.809 us | 2,179.021 us | 2.94x |
| `literal-alternate/sherlock-zh` | 89.325 us | 35.579 us | 2,124.136 us | 2.51x |
| `literal/sherlock-casei-en` | 152.218 us | 77.899 us | 444.965 us | 1.95x |
| `literal/sherlock-ru` | 160.236 us | 91.119 us | 5,319.900 us | 1.76x |
| `literal/sherlock-casei-ru` | 237.707 us | 195.003 us | 5,274.093 us | 1.22x |
| `literal-alternate/sherlock-en-mixed` | 202.779 us | 174.640 us | 546.992 us | 1.16x |
| `literal-alternate/sherlock-casei-en` | 163.636 us | 142.315 us | 573.590 us | 1.15x |
| `aws-keys/quick` | 4,030.621 us | 3,614.510 us | 37,165.000 us | 1.12x |
| `ruff-noqa/tweaked` | 4,853.725 us | 4,477.712 us | 34,510.000 us | 1.08x |
| `literal-alternate/sherlock-en` | 384.210 us | 365.217 us | 796.283 us | 1.05x |
| `bounded-repeat/letters-ru` | 2,068.416 us | 2,017.357 us | 2,835.731 us | 1.03x |
| `literal/sherlock-en` | 64.650 us | 75.308 us | 430.331 us | 0.86x |
| `literal-alternate/sherlock-en-nomatch` | 159.562 us | 196.636 us | 601.245 us | 0.81x |
| `bounded-repeat/capitals` | 9,513.650 us | 13,531.300 us | 16,236.325 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,611.836 us | 2,343.339 us | 7,456.650 us | 0.69x |
| `bounded-repeat/letters-en` | 1,107.630 us | 1,816.186 us | 1,907.555 us | 0.61x |
| `aws-keys/full` | 10,762.300 us | 17,956.680 us | 48,959.100 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 983.981 us | 1,960.408 us | 7,284.500 us | 0.50x |
| `bounded-repeat/context` | 20,858.550 us | 68,707.450 us | 72,097.100 us | 0.30x |
| `ruff-noqa/real` | 4,713.119 us | 20,355.350 us | 54,508.033 us | 0.23x |
| `dictionary/single` | 8,131.970 us | 45,468.850 us | 45,372.000 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.192 us | 0.078 us | 0.993 us | 2.46x |
| `industry/mariomka-email-count` | `Count` | 940.229 us | 456.959 us | 7,537.475 us | 2.06x |
| `common/backtracking` | `IsMatch` | 0.085 us | 0.043 us | 0.073 us | 1.96x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 110.382 us | 57.192 us | 263.179 us | 1.93x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.066 us | 0.086 us | 1.86x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.048 us | 0.085 us | 1.62x |
| `common/replace-words` | `Replace` | 27.307 us | 16.996 us | 19.370 us | 1.61x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.047 us | 0.029 us | 0.046 us | 1.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.084 us | 0.054 us | 0.075 us | 1.57x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.031 us | 0.048 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.047 us | 0.069 us | 1.50x |
| `industry/leipzig-river-window-count` | `Count` | 2,176.331 us | 1,479.217 us | 7,647.180 us | 1.47x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.050 us | 0.068 us | 1.40x |
| `industry/leipzig-symbol-count` | `Count` | 1,717.748 us | 1,244.736 us | 7,123.220 us | 1.38x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.080 us | 0.107 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.089 us | 0.114 us | 1.10x |
| `industry/mariomka-uri-count` | `Count` | 1,167.566 us | 1,098.453 us | 8,310.975 us | 1.06x |
| `industry/leipzig-twain-count` | `Count` | 1,262.073 us | 1,256.791 us | 7,337.720 us | 1.00x |
| `common/uri-match` | `IsMatch` | 0.087 us | 0.089 us | 0.118 us | 0.98x |
| `common/one-node-backtracking` | `IsMatch` | 0.072 us | 0.090 us | 0.130 us | 0.80x |
| `common/split-words` | `Split` | 14.337 us | 20.698 us | 22.547 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.071 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,100.056 us | 3,391.723 us | 10,714.867 us | 0.62x |
| `common/matches-words` | `Count` | 9.241 us | 15.082 us | 17.294 us | 0.61x |
| `industry/leipzig-name-family-count` | `Count` | 1,748.011 us | 3,046.276 us | 7,692.750 us | 0.57x |
| `common/matches-word` | `Count` | 1.312 us | 2.294 us | 3.376 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.103 us | 0.196 us | 0.235 us | 0.52x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.052 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.636 us | 577.964 us | 847.684 us | 0.31x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,771.358 us | 8,618.089 us | 9,253.688 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 492.340 us | 3,892.350 us | 4,260.822 us | 0.13x |
| `common/matches-set` | `Count` | 10.372 us | 104.436 us | 106.131 us | 0.10x |
| `common/matches-boundary` | `Count` | 9.421 us | 102.637 us | 104.474 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 239.362 us | 4,707.312 us | 5,154.673 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.054 us | 6.250 us | 31.346 us | 1.45x |
| `structural/keyword-to-generic-type` | 5.422 us | 4.038 us | 18.251 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.678 us | 1.341 us | 1.437 us | 1.25x |
| `lokad/lexer/doc-line` | 3.223 us | 2.865 us | 3.450 us | 1.13x |
| `lokad/lexer/identifier` | 23.297 us | 22.524 us | 24.920 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.161 us | 7.184 us | 7.504 us | 1.00x |
| `structural/ordered-keyword-pair` | 5.223 us | 5.280 us | 19.198 us | 0.99x |
| `fallback/lookahead` | 5.595 us | 5.945 us | 19.308 us | 0.94x |
| `lokad/langserv/identifier-validator` | 10.826 us | 12.220 us | 13.479 us | 0.89x |
| `literal-family/method-token-family` | 7.468 us | 8.993 us | 21.965 us | 0.83x |
| `lokad/lexer/operator-run` | 2.743 us | 3.409 us | 3.728 us | 0.80x |
| `lokad/style/hex-color` | 0.562 us | 0.802 us | 0.834 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.247 us | 4.721 us | 5.243 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 1.967 us | 2.893 us | 2.877 us | 0.68x |
| `literal/identifier-token` | 2.708 us | 4.238 us | 17.574 us | 0.64x |
| `literal/call-token` | 2.542 us | 4.236 us | 17.683 us | 0.60x |
| `lokad/lexer/number` | 7.768 us | 14.153 us | 14.758 us | 0.55x |
| `structural/method-family-generic-call` | 3.335 us | 6.470 us | 19.837 us | 0.52x |
| `structural/ordered-identifier-window` | 14.160 us | 28.415 us | 42.020 us | 0.50x |
| `structural/method-family-call` | 3.791 us | 7.868 us | 21.187 us | 0.48x |
| `lokad/style/cell-ref` | 1.376 us | 2.893 us | 2.908 us | 0.48x |
| `literal-family/type-token-family` | 10.244 us | 23.842 us | 37.568 us | 0.43x |
| `literal/identifier-token-casei` | 5.250 us | 14.925 us | 29.329 us | 0.35x |
| `lokad/style/range-ref` | 0.860 us | 2.704 us | 2.727 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.397 us | 17.434 us | 30.725 us | 0.31x |
| `structural/ordered-keyword-window` | 5.279 us | 17.772 us | 31.910 us | 0.30x |
| `structural/modifier-family-to-type-window` | 66.503 us | 249.884 us | 267.821 us | 0.27x |
| `lokad/lexer/string` | 20.734 us | 94.162 us | 99.412 us | 0.22x |
| `lokad/folding/region-marker` | 6.201 us | 35.920 us | 61.873 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.286 us | 24.262 us | 24.483 us | 0.09x |
| `structural/keyword-family-to-capitalized-identifier` | 11.317 us | 220.892 us | 234.917 us | 0.05x |
| `lokad/langserv/url-download` | 2.417 us | 49.137 us | 49.744 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.856 us | 130.647 us | 145.066 us | 0.04x |
| `balancing/begin-end-blocks` | 5.527 us | 4,320.389 us | 4,329.047 us | 0.0013x |
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
| `lokad/imports/module-imports` | 8.989 us | 4.332 us | 28.619 us | 2.08x |
| `lokad/langserv/color-short-hex` | 1.667 us | 0.965 us | 1.115 us | 1.73x |
| `lokad/langserv/helper-identifier` | 7.190 us | 4.491 us | 5.483 us | 1.60x |
| `structural/keyword-to-generic-type` | 5.435 us | 3.449 us | 17.186 us | 1.58x |
| `lokad/lexer/doc-line` | 3.220 us | 2.092 us | 2.761 us | 1.54x |
| `structural/keyword-family-to-capitalized-identifier` | 11.203 us | 7.777 us | 21.001 us | 1.44x |
| `lokad/lexer/identifier` | 23.113 us | 16.143 us | 20.181 us | 1.43x |
| `lokad/lexer/operator-run` | 2.733 us | 1.954 us | 2.496 us | 1.40x |
| `structural/ordered-identifier-window` | 14.159 us | 10.388 us | 24.142 us | 1.36x |
| `fallback/lookahead` | 5.619 us | 4.146 us | 17.059 us | 1.36x |
| `structural/ordered-keyword-pair` | 5.295 us | 4.116 us | 17.391 us | 1.29x |
| `lokad/langserv/identifier-validator` | 10.904 us | 8.674 us | 10.707 us | 1.26x |
| `structural/modifier-family-to-type-window` | 67.140 us | 54.092 us | 70.304 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.227 us | 2.665 us | 3.370 us | 1.21x |
| `lokad/style/hex-color` | 0.565 us | 0.474 us | 0.569 us | 1.19x |
| `lokad/langserv/color-rgb-hex` | 1.963 us | 1.744 us | 1.942 us | 1.13x |
| `lokad/lexer/number` | 7.748 us | 7.266 us | 8.082 us | 1.07x |
| `literal-family/type-token-family` | 10.325 us | 11.293 us | 24.258 us | 0.91x |
| `literal/identifier-token-casei` | 5.272 us | 5.849 us | 19.500 us | 0.90x |
| `lokad/style/cell-ref` | 1.375 us | 1.686 us | 1.860 us | 0.82x |
| `structural/method-family-call` | 3.776 us | 4.973 us | 17.671 us | 0.76x |
| `structural/method-family-generic-call` | 3.345 us | 4.480 us | 17.063 us | 0.75x |
| `literal-family/method-token-family` | 3.847 us | 5.251 us | 17.957 us | 0.73x |
| `literal/call-token` | 2.556 us | 3.916 us | 16.691 us | 0.65x |
| `structural/ordered-modifier-pair` | 5.393 us | 8.517 us | 21.900 us | 0.63x |
| `structural/ordered-keyword-window` | 5.323 us | 8.498 us | 22.510 us | 0.63x |
| `literal/identifier-token` | 2.728 us | 4.615 us | 17.229 us | 0.59x |
| `lokad/style/range-ref` | 0.856 us | 1.463 us | 1.524 us | 0.58x |
| `structural/modifier-family-to-type-pair` | 4.895 us | 10.390 us | 24.150 us | 0.47x |
| `lokad/lexer/string` | 20.892 us | 45.173 us | 47.302 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.296 us | 7.329 us | 7.913 us | 0.31x |
| `lokad/langserv/url-download` | 2.400 us | 12.621 us | 13.334 us | 0.19x |
| `lokad/folding/region-marker` | 6.181 us | 34.711 us | 61.342 us | 0.18x |
| `balancing/begin-end-blocks` | 5.542 us | 454.496 us | 480.682 us | 0.01x |
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
