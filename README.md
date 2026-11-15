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
and resource checks completed in **46m54s**. The [run receipt](bench/Experiments/DeliveryDev94-20261115/full-refresh.json)
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
| `literal/sherlock-zh` | 69.555 us | 18.577 us | 2,148.089 us | 3.74x |
| `literal/sherlock-ru` | 171.169 us | 96.562 us | 5,130.157 us | 1.77x |
| `literal-alternate/sherlock-zh` | 90.909 us | 68.652 us | 2,212.348 us | 1.32x |
| `literal/sherlock-casei-en` | 148.860 us | 195.236 us | 590.828 us | 0.76x |
| `literal/sherlock-en` | 63.525 us | 84.092 us | 466.209 us | 0.76x |
| `literal/sherlock-casei-ru` | 240.178 us | 381.586 us | 5,536.586 us | 0.63x |
| `ruff-noqa/tweaked` | 4,709.931 us | 7,595.818 us | 41,382.850 us | 0.62x |
| `aws-keys/quick` | 4,018.974 us | 9,337.575 us | 45,559.600 us | 0.43x |
| `bounded-repeat/letters-en` | 1,087.425 us | 2,762.707 us | 2,903.215 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,077.400 us | 5,851.600 us | 6,587.375 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,630.628 us | 4,804.259 us | 9,876.575 us | 0.34x |
| `dictionary/single` | 8,181.600 us | 25,982.733 us | 25,852.300 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 187.695 us | 612.436 us | 1,042.287 us | 0.31x |
| `literal-alternate/sherlock-en` | 384.467 us | 1,511.029 us | 2,058.303 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 162.036 us | 667.817 us | 1,103.430 us | 0.24x |
| `bounded-repeat/capitals` | 9,399.175 us | 46,485.400 us | 49,921.950 us | 0.20x |
| `bounded-repeat/context` | 20,706.450 us | 146,440.800 us | 150,958.900 us | 0.14x |
| `aws-keys/full` | 11,022.471 us | 85,231.900 us | 116,880.200 us | 0.13x |
| `ruff-noqa/real` | 4,634.753 us | 69,067.000 us | 97,752.200 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 998.345 us | 23,038.850 us | 28,306.450 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 162.629 us | 11,190.243 us | 12,217.714 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 924.739 us | 489.372 us | 8,725.875 us | 1.89x |
| `common/match-word` | `Match` | 0.191 us | 0.167 us | 1.141 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,278.256 us | 1,289.757 us | 8,558.522 us | 0.99x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.107 us | 0.142 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.184 us | 0.204 us | 0.66x |
| `industry/mariomka-uri-count` | `Count` | 1,146.168 us | 1,854.579 us | 10,003.575 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.083 us | 0.099 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.088 us | 0.164 us | 0.181 us | 0.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 108.005 us | 250.235 us | 476.023 us | 0.43x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.171 us | 0.191 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.226 us | 0.260 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.185 us | 0.203 us | 0.37x |
| `common/replace-words` | `Replace` | 26.979 us | 75.366 us | 77.565 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.092 us | 0.265 us | 0.290 us | 0.35x |
| `common/matches-word` | `Count` | 1.346 us | 4.206 us | 5.779 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.338 us | 0.363 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.107 us | 0.137 us | 0.26x |
| `industry/mariomka-ip-count` | `Count` | 2,111.992 us | 8,382.856 us | 16,584.933 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.096 us | 0.21x |
| `common/split-words` | `Split` | 14.386 us | 77.454 us | 78.846 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.382 us | 971.013 us | 1,236.670 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.553 us | 0.591 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,685.493 us | 9,862.750 us | 16,736.133 us | 0.17x |
| `common/matches-words` | `Count` | 9.120 us | 69.145 us | 70.072 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.072 us | 0.604 us | 0.636 us | 0.12x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.089 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.823 us | 0.855 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,784.205 us | 19,217.600 us | 19,715.150 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,618.545 us | 22,683.450 us | 29,208.450 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,205.683 us | 31,418.800 us | 38,838.700 us | 0.07x |
| `common/matches-boundary` | `Count` | 10.282 us | 165.757 us | 168.236 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 474.403 us | 8,113.520 us | 8,588.389 us | 0.06x |
| `common/matches-set` | `Count` | 9.348 us | 192.423 us | 191.814 us | 0.05x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.540 us | 11,686.600 us | 12,306.486 us | 0.02x |
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
| `literal/sherlock-zh` | 69.373 us | 23.590 us | 2,141.797 us | 2.94x |
| `literal-alternate/sherlock-zh` | 91.373 us | 35.932 us | 2,123.997 us | 2.54x |
| `literal/sherlock-casei-en` | 148.760 us | 76.516 us | 444.339 us | 1.94x |
| `literal/sherlock-ru` | 170.411 us | 87.786 us | 5,172.753 us | 1.94x |
| `literal/sherlock-casei-ru` | 239.830 us | 192.755 us | 5,276.573 us | 1.24x |
| `literal-alternate/sherlock-casei-en` | 162.915 us | 145.929 us | 577.022 us | 1.12x |
| `aws-keys/quick` | 4,018.726 us | 3,646.848 us | 36,880.200 us | 1.10x |
| `literal-alternate/sherlock-en-mixed` | 188.824 us | 173.526 us | 565.438 us | 1.09x |
| `literal-alternate/sherlock-en` | 385.674 us | 365.778 us | 778.017 us | 1.05x |
| `ruff-noqa/tweaked` | 4,796.700 us | 4,597.594 us | 35,153.950 us | 1.04x |
| `bounded-repeat/letters-ru` | 2,085.003 us | 2,039.395 us | 2,827.659 us | 1.02x |
| `literal/sherlock-en` | 63.477 us | 74.531 us | 415.690 us | 0.85x |
| `literal-alternate/sherlock-en-nomatch` | 162.327 us | 205.143 us | 606.981 us | 0.79x |
| `bounded-repeat/capitals` | 9,498.943 us | 13,574.633 us | 16,279.625 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,620.656 us | 2,324.018 us | 7,393.500 us | 0.70x |
| `aws-keys/full` | 10,921.529 us | 18,097.520 us | 53,130.167 us | 0.60x |
| `bounded-repeat/letters-en` | 1,083.013 us | 1,806.947 us | 1,895.425 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 987.232 us | 1,950.744 us | 7,149.450 us | 0.51x |
| `bounded-repeat/context` | 20,564.950 us | 68,336.150 us | 71,318.950 us | 0.30x |
| `ruff-noqa/real` | 4,744.165 us | 20,158.950 us | 51,880.600 us | 0.24x |
| `dictionary/single` | 8,173.610 us | 45,513.000 us | 45,688.950 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.191 us | 0.078 us | 1.005 us | 2.44x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.043 us | 0.072 us | 2.05x |
| `industry/mariomka-email-count` | `Count` | 929.177 us | 456.551 us | 7,520.850 us | 2.04x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.065 us | 0.086 us | 1.88x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 107.722 us | 57.615 us | 246.144 us | 1.87x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.047 us | 0.030 us | 0.046 us | 1.58x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.048 us | 0.086 us | 1.57x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.054 us | 0.075 us | 1.57x |
| `common/replace-words` | `Replace` | 26.799 us | 17.162 us | 19.906 us | 1.56x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.032 us | 0.049 us | 1.52x |
| `industry/leipzig-river-window-count` | `Count` | 2,213.769 us | 1,503.175 us | 7,538.260 us | 1.47x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.047 us | 0.068 us | 1.47x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.049 us | 0.070 us | 1.40x |
| `industry/leipzig-symbol-count` | `Count` | 1,642.770 us | 1,196.473 us | 6,559.025 us | 1.37x |
| `common/email-match` | `IsMatch` | 0.092 us | 0.080 us | 0.108 us | 1.15x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.088 us | 0.114 us | 1.12x |
| `industry/mariomka-uri-count` | `Count` | 1,151.056 us | 1,078.390 us | 8,155.225 us | 1.07x |
| `industry/leipzig-twain-count` | `Count` | 1,287.953 us | 1,298.105 us | 7,375.610 us | 0.99x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.087 us | 0.120 us | 0.99x |
| `common/one-node-backtracking` | `IsMatch` | 0.072 us | 0.090 us | 0.126 us | 0.80x |
| `common/split-words` | `Split` | 14.391 us | 20.306 us | 22.204 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.071 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,106.222 us | 3,428.659 us | 10,822.267 us | 0.61x |
| `common/matches-words` | `Count` | 9.096 us | 15.146 us | 17.396 us | 0.60x |
| `common/matches-word` | `Count` | 1.345 us | 2.353 us | 3.384 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,691.291 us | 2,969.165 us | 7,718.650 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.198 us | 0.240 us | 0.50x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.035 us | 590.624 us | 847.873 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.052 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,790.726 us | 8,640.744 us | 9,084.989 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 474.463 us | 3,886.215 us | 4,301.844 us | 0.12x |
| `common/matches-boundary` | `Count` | 10.326 us | 102.704 us | 104.678 us | 0.10x |
| `common/matches-set` | `Count` | 9.451 us | 104.287 us | 105.583 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.076 us | 4,683.494 us | 5,110.450 us | 0.06x |
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
| `lokad/imports/module-imports` | 9.232 us | 6.328 us | 30.953 us | 1.46x |
| `structural/keyword-to-generic-type` | 5.448 us | 4.046 us | 18.211 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.626 us | 1.342 us | 1.421 us | 1.21x |
| `lokad/lexer/doc-line` | 3.217 us | 2.970 us | 3.554 us | 1.08x |
| `structural/ordered-keyword-pair` | 5.424 us | 5.217 us | 19.302 us | 1.04x |
| `lokad/lexer/identifier` | 23.080 us | 22.580 us | 24.867 us | 1.02x |
| `fallback/lookahead` | 5.854 us | 5.967 us | 19.247 us | 0.98x |
| `lokad/langserv/helper-identifier` | 7.038 us | 7.251 us | 7.527 us | 0.97x |
| `lokad/style/hex-color` | 0.717 us | 0.777 us | 0.836 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.025 us | 12.444 us | 13.665 us | 0.89x |
| `lokad/lexer/operator-run` | 2.752 us | 3.443 us | 3.808 us | 0.80x |
| `literal-family/method-token-family` | 7.067 us | 8.923 us | 21.976 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 1.969 us | 2.836 us | 2.915 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.232 us | 4.856 us | 5.498 us | 0.67x |
| `literal/identifier-token` | 2.734 us | 4.313 us | 17.788 us | 0.63x |
| `literal/call-token` | 2.545 us | 4.212 us | 17.533 us | 0.60x |
| `lokad/lexer/number` | 7.812 us | 14.060 us | 14.670 us | 0.56x |
| `structural/ordered-identifier-window` | 14.335 us | 27.952 us | 41.799 us | 0.51x |
| `structural/method-family-generic-call` | 3.380 us | 6.659 us | 19.899 us | 0.51x |
| `lokad/style/cell-ref` | 1.404 us | 2.841 us | 2.886 us | 0.49x |
| `structural/method-family-call` | 3.775 us | 7.964 us | 21.660 us | 0.47x |
| `literal-family/type-token-family` | 10.312 us | 23.235 us | 37.194 us | 0.44x |
| `literal/identifier-token-casei` | 5.260 us | 14.957 us | 28.541 us | 0.35x |
| `lokad/style/range-ref` | 0.868 us | 2.695 us | 2.706 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.352 us | 16.952 us | 30.094 us | 0.32x |
| `structural/ordered-keyword-window` | 5.356 us | 17.607 us | 31.706 us | 0.30x |
| `structural/modifier-family-to-type-window` | 66.770 us | 249.576 us | 267.078 us | 0.27x |
| `lokad/lexer/string` | 21.015 us | 95.154 us | 99.040 us | 0.22x |
| `lokad/folding/region-marker` | 6.274 us | 35.966 us | 62.819 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.307 us | 23.961 us | 24.176 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.287 us | 215.446 us | 228.208 us | 0.05x |
| `lokad/langserv/url-download` | 2.446 us | 51.055 us | 51.784 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.937 us | 133.356 us | 148.486 us | 0.04x |
| `balancing/begin-end-blocks` | 5.568 us | 4,255.561 us | 4,260.983 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.209 us | 4.389 us | 28.283 us | 2.10x |
| `lokad/langserv/color-short-hex` | 1.625 us | 0.957 us | 1.140 us | 1.70x |
| `structural/keyword-to-generic-type` | 5.412 us | 3.412 us | 17.184 us | 1.59x |
| `lokad/langserv/helper-identifier` | 6.983 us | 4.558 us | 5.515 us | 1.53x |
| `lokad/lexer/doc-line` | 3.215 us | 2.152 us | 2.922 us | 1.49x |
| `lokad/lexer/operator-run` | 2.755 us | 1.919 us | 2.454 us | 1.44x |
| `lokad/style/hex-color` | 0.718 us | 0.501 us | 0.577 us | 1.43x |
| `structural/keyword-family-to-capitalized-identifier` | 11.138 us | 7.824 us | 20.887 us | 1.42x |
| `fallback/lookahead` | 5.871 us | 4.126 us | 17.071 us | 1.42x |
| `lokad/lexer/identifier` | 22.929 us | 16.262 us | 19.556 us | 1.41x |
| `structural/ordered-identifier-window` | 14.229 us | 10.370 us | 23.751 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.433 us | 4.046 us | 17.480 us | 1.34x |
| `lokad/langserv/identifier-validator` | 10.965 us | 8.870 us | 11.005 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.226 us | 2.641 us | 3.545 us | 1.22x |
| `structural/modifier-family-to-type-window` | 66.531 us | 55.025 us | 69.998 us | 1.21x |
| `lokad/langserv/color-rgb-hex` | 1.966 us | 1.706 us | 1.914 us | 1.15x |
| `lokad/lexer/number` | 7.856 us | 7.071 us | 8.073 us | 1.11x |
| `literal-family/type-token-family` | 10.337 us | 11.239 us | 24.658 us | 0.92x |
| `literal/identifier-token-casei` | 5.237 us | 5.824 us | 19.488 us | 0.90x |
| `lokad/style/cell-ref` | 1.406 us | 1.724 us | 1.850 us | 0.82x |
| `structural/method-family-generic-call` | 3.405 us | 4.407 us | 16.995 us | 0.77x |
| `structural/method-family-call` | 3.795 us | 5.024 us | 18.211 us | 0.76x |
| `literal-family/method-token-family` | 3.835 us | 5.252 us | 17.875 us | 0.73x |
| `literal/call-token` | 2.539 us | 3.889 us | 16.632 us | 0.65x |
| `structural/ordered-keyword-window` | 5.332 us | 8.461 us | 22.425 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.313 us | 8.481 us | 21.735 us | 0.63x |
| `lokad/style/range-ref` | 0.862 us | 1.441 us | 1.519 us | 0.60x |
| `literal/identifier-token` | 2.749 us | 4.612 us | 17.495 us | 0.60x |
| `structural/modifier-family-to-type-pair` | 4.925 us | 10.385 us | 24.262 us | 0.47x |
| `lokad/lexer/string` | 20.838 us | 45.450 us | 48.552 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.294 us | 7.236 us | 7.836 us | 0.32x |
| `lokad/langserv/url-download` | 2.438 us | 12.799 us | 13.685 us | 0.19x |
| `lokad/folding/region-marker` | 6.254 us | 36.212 us | 62.505 us | 0.17x |
| `balancing/begin-end-blocks` | 5.577 us | 457.440 us | 484.723 us | 0.01x |
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
