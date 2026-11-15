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

The tables were refreshed on **15 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks completed in **47m15s**. The [run receipt](bench/Experiments/DeliveryDev95-20261115/full-refresh.json)
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
| `literal/sherlock-zh` | 69.282 us | 18.562 us | 2,154.887 us | 3.73x |
| `literal/sherlock-ru` | 164.268 us | 98.165 us | 5,248.787 us | 1.67x |
| `literal-alternate/sherlock-zh` | 91.604 us | 67.842 us | 2,198.797 us | 1.35x |
| `literal/sherlock-casei-en` | 132.548 us | 186.336 us | 587.181 us | 0.71x |
| `literal/sherlock-en` | 58.639 us | 86.599 us | 458.819 us | 0.68x |
| `literal/sherlock-casei-ru` | 246.243 us | 380.958 us | 5,505.614 us | 0.65x |
| `ruff-noqa/tweaked` | 4,450.575 us | 7,654.582 us | 40,879.650 us | 0.58x |
| `aws-keys/quick` | 3,978.179 us | 9,374.800 us | 39,431.500 us | 0.42x |
| `bounded-repeat/letters-en` | 1,092.419 us | 2,748.361 us | 2,872.550 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,085.695 us | 5,749.415 us | 6,551.600 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,601.758 us | 4,632.182 us | 9,848.100 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 195.556 us | 590.952 us | 1,012.814 us | 0.33x |
| `dictionary/single` | 8,261.060 us | 25,732.000 us | 25,931.700 us | 0.32x |
| `literal-alternate/sherlock-en` | 386.477 us | 1,446.623 us | 2,020.417 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 153.863 us | 661.803 us | 1,076.079 us | 0.23x |
| `bounded-repeat/capitals` | 9,460.237 us | 45,947.550 us | 49,077.800 us | 0.21x |
| `bounded-repeat/context` | 20,809.300 us | 145,468.700 us | 151,647.900 us | 0.14x |
| `aws-keys/full` | 9,054.633 us | 85,865.200 us | 115,555.400 us | 0.11x |
| `ruff-noqa/real` | 4,559.882 us | 69,829.200 us | 98,162.200 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 965.269 us | 22,936.425 us | 28,000.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 161.470 us | 11,520.357 us | 12,393.657 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 917.751 us | 505.313 us | 8,931.025 us | 1.82x |
| `common/match-word` | `Match` | 0.191 us | 0.167 us | 1.126 us | 1.14x |
| `industry/leipzig-twain-count` | `Count` | 1,249.861 us | 1,305.717 us | 8,404.344 us | 0.96x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.109 us | 0.144 us | 0.75x |
| `common/uri-miss` | `IsMatch` | 0.112 us | 0.184 us | 0.202 us | 0.61x |
| `industry/mariomka-uri-count` | `Count` | 1,123.394 us | 1,867.126 us | 10,233.700 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.082 us | 0.098 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.086 us | 0.161 us | 0.178 us | 0.53x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 110.135 us | 260.064 us | 524.258 us | 0.42x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.176 us | 0.192 us | 0.39x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.225 us | 0.255 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.185 us | 0.202 us | 0.37x |
| `common/replace-words` | `Replace` | 26.831 us | 75.276 us | 75.973 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.266 us | 0.292 us | 0.35x |
| `common/matches-word` | `Count` | 1.331 us | 4.170 us | 5.635 us | 0.32x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.024 us | 0.080 us | 0.095 us | 0.30x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.341 us | 0.363 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.108 us | 0.136 us | 0.26x |
| `industry/mariomka-ip-count` | `Count` | 2,104.150 us | 8,235.850 us | 16,569.533 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.553 us | 0.595 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 185.808 us | 954.253 us | 1,229.514 us | 0.19x |
| `common/split-words` | `Split` | 14.385 us | 76.864 us | 78.115 us | 0.19x |
| `industry/leipzig-name-family-count` | `Count` | 1,643.109 us | 9,589.611 us | 16,394.067 us | 0.17x |
| `common/matches-words` | `Count` | 9.261 us | 68.460 us | 69.978 us | 0.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.088 us | 0.12x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.609 us | 0.648 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.822 us | 0.860 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,884.700 us | 19,391.750 us | 20,060.950 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,658.004 us | 22,252.050 us | 28,742.200 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,206.880 us | 31,991.700 us | 39,137.550 us | 0.07x |
| `common/matches-boundary` | `Count` | 10.362 us | 165.427 us | 166.312 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 510.022 us | 8,359.380 us | 8,831.567 us | 0.06x |
| `common/matches-set` | `Count` | 10.432 us | 192.849 us | 195.601 us | 0.05x |
| `industry/rust-sherlock-ing-count` | `Count` | 252.990 us | 12,083.186 us | 12,730.917 us | 0.02x |
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
| `literal/sherlock-zh` | 70.027 us | 23.640 us | 2,183.168 us | 2.96x |
| `literal-alternate/sherlock-zh` | 91.561 us | 35.867 us | 2,160.369 us | 2.55x |
| `literal/sherlock-ru` | 165.917 us | 88.843 us | 5,218.527 us | 1.87x |
| `literal/sherlock-casei-en` | 138.187 us | 75.897 us | 433.490 us | 1.82x |
| `literal/sherlock-casei-ru` | 246.702 us | 192.299 us | 5,244.387 us | 1.28x |
| `literal-alternate/sherlock-casei-en` | 160.660 us | 141.094 us | 629.854 us | 1.14x |
| `literal-alternate/sherlock-en-mixed` | 194.231 us | 177.000 us | 578.533 us | 1.10x |
| `literal-alternate/sherlock-en` | 388.055 us | 363.082 us | 772.962 us | 1.07x |
| `aws-keys/quick` | 3,958.147 us | 3,713.352 us | 35,102.267 us | 1.07x |
| `bounded-repeat/letters-ru` | 2,067.976 us | 2,024.035 us | 2,839.915 us | 1.02x |
| `ruff-noqa/tweaked` | 4,609.406 us | 4,513.076 us | 36,126.250 us | 1.02x |
| `literal-alternate/sherlock-en-nomatch` | 154.454 us | 198.478 us | 598.036 us | 0.78x |
| `literal/sherlock-en` | 58.599 us | 76.515 us | 436.758 us | 0.77x |
| `bounded-repeat/capitals` | 9,400.750 us | 13,276.050 us | 15,997.275 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,613.034 us | 2,304.382 us | 7,402.433 us | 0.70x |
| `bounded-repeat/letters-en` | 1,091.049 us | 1,805.993 us | 1,887.479 us | 0.60x |
| `aws-keys/full` | 10,799.862 us | 18,059.760 us | 49,168.800 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 969.687 us | 1,936.603 us | 7,109.175 us | 0.50x |
| `bounded-repeat/context` | 20,693.100 us | 68,034.100 us | 70,887.200 us | 0.30x |
| `ruff-noqa/real` | 4,703.112 us | 20,081.650 us | 52,010.567 us | 0.23x |
| `dictionary/single` | 8,260.740 us | 45,458.150 us | 45,765.200 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.191 us | 0.075 us | 1.004 us | 2.56x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.044 us | 0.073 us | 1.97x |
| `industry/mariomka-email-count` | `Count` | 914.619 us | 470.732 us | 7,691.325 us | 1.94x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 109.898 us | 57.442 us | 270.857 us | 1.91x |
| `common/uri-miss` | `IsMatch` | 0.111 us | 0.064 us | 0.083 us | 1.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.048 us | 0.085 us | 1.70x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.047 us | 0.029 us | 0.046 us | 1.61x |
| `common/replace-words` | `Replace` | 26.746 us | 17.055 us | 19.001 us | 1.57x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.055 us | 0.076 us | 1.56x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.032 us | 0.049 us | 1.54x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.047 us | 0.068 us | 1.48x |
| `industry/leipzig-river-window-count` | `Count` | 2,222.557 us | 1,556.339 us | 7,594.880 us | 1.43x |
| `common/ip-miss` | `IsMatch` | 0.068 us | 0.051 us | 0.070 us | 1.35x |
| `industry/leipzig-symbol-count` | `Count` | 1,671.807 us | 1,250.050 us | 7,063.380 us | 1.34x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.081 us | 0.108 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.088 us | 0.115 us | 1.10x |
| `industry/mariomka-uri-count` | `Count` | 1,134.052 us | 1,095.594 us | 8,645.850 us | 1.04x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.087 us | 0.118 us | 0.99x |
| `industry/leipzig-twain-count` | `Count` | 1,248.659 us | 1,298.183 us | 7,312.560 us | 0.96x |
| `common/matches-words` | `Count` | 9.199 us | 10.805 us | 13.963 us | 0.85x |
| `common/one-node-backtracking` | `IsMatch` | 0.073 us | 0.090 us | 0.127 us | 0.81x |
| `common/split-words` | `Split` | 14.370 us | 20.348 us | 22.193 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.071 us | 0.65x |
| `industry/mariomka-ip-count` | `Count` | 2,105.228 us | 3,489.123 us | 10,795.300 us | 0.60x |
| `common/matches-word` | `Count` | 1.329 us | 2.291 us | 3.260 us | 0.58x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.197 us | 0.237 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,666.585 us | 3,022.024 us | 7,810.625 us | 0.55x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 189.937 us | 584.112 us | 838.408 us | 0.33x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.034 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,879.983 us | 8,781.989 us | 9,354.175 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.057 us | 3,926.411 us | 4,311.356 us | 0.13x |
| `common/matches-boundary` | `Count` | 10.292 us | 102.646 us | 104.729 us | 0.10x |
| `common/matches-set` | `Count` | 10.422 us | 104.858 us | 106.316 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 255.382 us | 4,732.494 us | 5,172.293 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.132 us | 6.322 us | 31.096 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.469 us | 4.039 us | 18.374 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.623 us | 1.324 us | 1.417 us | 1.23x |
| `lokad/lexer/doc-line` | 3.148 us | 2.940 us | 3.513 us | 1.07x |
| `lokad/lexer/identifier` | 22.536 us | 21.937 us | 24.495 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.245 us | 5.174 us | 20.012 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.278 us | 7.215 us | 7.565 us | 1.01x |
| `fallback/lookahead` | 5.663 us | 5.972 us | 19.393 us | 0.95x |
| `lokad/langserv/identifier-validator` | 10.962 us | 12.333 us | 13.625 us | 0.89x |
| `lokad/lexer/operator-run` | 2.746 us | 3.314 us | 3.631 us | 0.83x |
| `literal-family/method-token-family` | 7.039 us | 8.971 us | 21.695 us | 0.78x |
| `lokad/style/hex-color` | 0.559 us | 0.785 us | 0.826 us | 0.71x |
| `lokad/docs/inline-doc-prefix` | 3.210 us | 4.771 us | 5.203 us | 0.67x |
| `lokad/langserv/color-rgb-hex` | 1.952 us | 2.916 us | 2.868 us | 0.67x |
| `literal/identifier-token` | 2.727 us | 4.202 us | 17.495 us | 0.65x |
| `literal/call-token` | 2.552 us | 4.216 us | 17.509 us | 0.61x |
| `lokad/lexer/number` | 7.809 us | 14.014 us | 14.733 us | 0.56x |
| `structural/method-family-generic-call` | 3.412 us | 6.696 us | 20.128 us | 0.51x |
| `lokad/style/cell-ref` | 1.380 us | 2.743 us | 2.788 us | 0.50x |
| `structural/ordered-identifier-window` | 14.307 us | 28.682 us | 42.164 us | 0.50x |
| `structural/method-family-call` | 3.779 us | 7.955 us | 21.066 us | 0.48x |
| `literal-family/type-token-family` | 10.377 us | 24.001 us | 37.566 us | 0.43x |
| `lokad/style/range-ref` | 0.882 us | 2.685 us | 2.608 us | 0.33x |
| `literal/identifier-token-casei` | 4.704 us | 15.406 us | 28.847 us | 0.31x |
| `structural/ordered-keyword-window` | 5.217 us | 17.483 us | 31.283 us | 0.30x |
| `structural/ordered-modifier-pair` | 5.113 us | 17.271 us | 30.984 us | 0.30x |
| `structural/modifier-family-to-type-window` | 67.555 us | 252.243 us | 268.079 us | 0.27x |
| `lokad/lexer/string` | 20.984 us | 92.906 us | 97.044 us | 0.23x |
| `lokad/folding/region-marker` | 6.082 us | 35.795 us | 62.987 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.314 us | 23.890 us | 24.202 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.251 us | 216.427 us | 231.425 us | 0.05x |
| `lokad/langserv/url-download` | 2.496 us | 51.967 us | 52.857 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.897 us | 133.767 us | 148.393 us | 0.04x |
| `balancing/begin-end-blocks` | 5.562 us | 4,273.906 us | 4,263.339 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.085 us | 4.383 us | 28.232 us | 2.07x |
| `lokad/langserv/color-short-hex` | 1.622 us | 0.955 us | 1.150 us | 1.70x |
| `lokad/langserv/helper-identifier` | 7.259 us | 4.456 us | 5.526 us | 1.63x |
| `structural/keyword-to-generic-type` | 5.456 us | 3.400 us | 17.080 us | 1.60x |
| `lokad/lexer/doc-line` | 3.182 us | 2.093 us | 2.808 us | 1.52x |
| `lokad/lexer/operator-run` | 2.728 us | 1.859 us | 2.437 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.251 us | 7.876 us | 21.052 us | 1.43x |
| `lokad/lexer/identifier` | 22.749 us | 15.987 us | 19.103 us | 1.42x |
| `structural/ordered-identifier-window` | 14.328 us | 10.500 us | 24.078 us | 1.36x |
| `fallback/lookahead` | 5.635 us | 4.162 us | 17.185 us | 1.35x |
| `structural/ordered-keyword-pair` | 5.189 us | 4.041 us | 17.935 us | 1.28x |
| `lokad/langserv/identifier-validator` | 10.930 us | 8.794 us | 10.708 us | 1.24x |
| `structural/modifier-family-to-type-window` | 67.543 us | 55.225 us | 70.552 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.222 us | 2.675 us | 3.398 us | 1.20x |
| `lokad/style/hex-color` | 0.560 us | 0.483 us | 0.557 us | 1.16x |
| `lokad/langserv/color-rgb-hex` | 1.990 us | 1.735 us | 1.958 us | 1.15x |
| `lokad/lexer/number` | 7.824 us | 6.962 us | 7.882 us | 1.12x |
| `literal-family/type-token-family` | 10.380 us | 11.401 us | 24.478 us | 0.91x |
| `lokad/style/cell-ref` | 1.387 us | 1.677 us | 1.825 us | 0.83x |
| `literal/identifier-token-casei` | 4.639 us | 5.812 us | 19.431 us | 0.80x |
| `structural/method-family-call` | 3.790 us | 4.989 us | 17.814 us | 0.76x |
| `structural/method-family-generic-call` | 3.402 us | 4.489 us | 17.168 us | 0.76x |
| `literal-family/method-token-family` | 3.808 us | 5.184 us | 17.854 us | 0.73x |
| `literal/call-token` | 2.541 us | 3.911 us | 16.609 us | 0.65x |
| `structural/ordered-keyword-window` | 5.280 us | 8.434 us | 21.962 us | 0.63x |
| `lokad/style/range-ref` | 0.886 us | 1.417 us | 1.506 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.135 us | 8.535 us | 22.108 us | 0.60x |
| `literal/identifier-token` | 2.746 us | 4.620 us | 17.261 us | 0.59x |
| `lokad/lexer/string` | 20.797 us | 43.178 us | 45.841 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 4.853 us | 10.488 us | 24.164 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.322 us | 7.392 us | 7.956 us | 0.31x |
| `lokad/langserv/url-download` | 2.537 us | 12.870 us | 13.398 us | 0.20x |
| `lokad/folding/region-marker` | 6.077 us | 36.106 us | 62.432 us | 0.17x |
| `balancing/begin-end-blocks` | 5.583 us | 459.907 us | 490.507 us | 0.01x |
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
