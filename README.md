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
and resource checks completed in **47m20s**. The [run receipt](bench/Experiments/DeliveryDev96-20261115/full-refresh.json)
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
| `literal/sherlock-zh` | 70.376 us | 18.834 us | 2,143.374 us | 3.74x |
| `literal/sherlock-ru` | 168.683 us | 96.498 us | 5,235.693 us | 1.75x |
| `literal-alternate/sherlock-zh` | 90.825 us | 68.354 us | 2,161.319 us | 1.33x |
| `literal/sherlock-casei-ru` | 250.338 us | 364.838 us | 5,503.900 us | 0.69x |
| `literal/sherlock-en` | 58.183 us | 87.382 us | 453.145 us | 0.67x |
| `literal/sherlock-casei-en` | 130.064 us | 196.500 us | 619.078 us | 0.66x |
| `ruff-noqa/tweaked` | 4,542.824 us | 7,527.445 us | 40,578.400 us | 0.60x |
| `aws-keys/quick` | 3,910.268 us | 9,544.962 us | 39,141.350 us | 0.41x |
| `bounded-repeat/letters-en` | 1,106.743 us | 2,785.911 us | 2,885.723 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,104.327 us | 5,799.677 us | 6,660.100 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 207.252 us | 578.615 us | 1,040.129 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,613.928 us | 4,689.913 us | 9,863.550 us | 0.34x |
| `dictionary/single` | 8,155.560 us | 25,969.633 us | 25,760.167 us | 0.31x |
| `literal-alternate/sherlock-en` | 404.362 us | 1,511.652 us | 2,038.839 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 153.469 us | 643.865 us | 1,069.923 us | 0.24x |
| `bounded-repeat/capitals` | 9,346.788 us | 45,376.000 us | 48,630.300 us | 0.21x |
| `bounded-repeat/context` | 21,005.600 us | 144,094.800 us | 148,113.500 us | 0.15x |
| `aws-keys/full` | 10,831.622 us | 85,049.700 us | 114,611.600 us | 0.13x |
| `ruff-noqa/real` | 4,610.659 us | 69,002.150 us | 96,574.600 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 979.617 us | 22,496.125 us | 27,442.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 164.950 us | 11,236.671 us | 12,114.629 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 909.280 us | 487.731 us | 8,851.925 us | 1.86x |
| `common/match-word` | `Match` | 0.190 us | 0.163 us | 1.078 us | 1.17x |
| `industry/leipzig-twain-count` | `Count` | 1,220.281 us | 1,289.130 us | 8,351.189 us | 0.95x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.111 us | 0.145 us | 0.69x |
| `common/uri-miss` | `IsMatch` | 0.114 us | 0.184 us | 0.204 us | 0.62x |
| `industry/mariomka-uri-count` | `Count` | 1,153.935 us | 1,897.410 us | 10,394.650 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.081 us | 0.098 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.167 us | 0.185 us | 0.51x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.173 us | 0.192 us | 0.43x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 108.302 us | 260.648 us | 507.017 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.192 us | 0.211 us | 0.39x |
| `common/uri-match` | `IsMatch` | 0.087 us | 0.231 us | 0.265 us | 0.38x |
| `common/email-match` | `IsMatch` | 0.092 us | 0.257 us | 0.284 us | 0.36x |
| `common/replace-words` | `Replace` | 26.766 us | 75.757 us | 78.601 us | 0.35x |
| `common/matches-word` | `Count` | 1.340 us | 4.198 us | 5.916 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.344 us | 0.368 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,177.749 us | 8,204.840 us | 16,551.467 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.107 us | 0.137 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.097 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 183.320 us | 959.922 us | 1,239.864 us | 0.19x |
| `common/split-words` | `Split` | 14.554 us | 76.944 us | 78.577 us | 0.19x |
| `industry/leipzig-name-family-count` | `Count` | 1,650.733 us | 9,784.362 us | 16,476.300 us | 0.17x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.554 us | 0.585 us | 0.15x |
| `common/matches-words` | `Count` | 9.186 us | 69.061 us | 70.792 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.075 us | 0.088 us | 0.12x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.605 us | 0.641 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.085 us | 0.822 us | 0.864 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,820.812 us | 19,427.125 us | 20,059.250 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,651.941 us | 23,401.725 us | 30,151.950 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,132.889 us | 31,591.733 us | 38,739.450 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 508.958 us | 8,450.356 us | 8,956.322 us | 0.06x |
| `common/matches-boundary` | `Count` | 9.569 us | 163.516 us | 165.145 us | 0.06x |
| `common/matches-set` | `Count` | 9.456 us | 191.414 us | 194.849 us | 0.05x |
| `industry/rust-sherlock-ing-count` | `Count` | 270.438 us | 12,043.971 us | 12,461.250 us | 0.02x |
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
| `literal/sherlock-zh` | 70.303 us | 24.110 us | 2,156.400 us | 2.92x |
| `literal-alternate/sherlock-zh` | 91.016 us | 35.667 us | 2,119.550 us | 2.55x |
| `literal/sherlock-ru` | 169.926 us | 87.616 us | 5,130.400 us | 1.94x |
| `literal/sherlock-casei-en` | 129.110 us | 75.987 us | 436.676 us | 1.70x |
| `literal/sherlock-casei-ru` | 250.154 us | 194.646 us | 5,290.979 us | 1.29x |
| `literal-alternate/sherlock-casei-en` | 166.149 us | 130.427 us | 611.163 us | 1.27x |
| `literal-alternate/sherlock-en-mixed` | 206.905 us | 173.089 us | 570.108 us | 1.20x |
| `aws-keys/quick` | 3,977.589 us | 3,594.605 us | 34,377.000 us | 1.11x |
| `literal-alternate/sherlock-en` | 401.671 us | 369.834 us | 785.889 us | 1.09x |
| `bounded-repeat/letters-ru` | 2,099.130 us | 2,081.953 us | 2,876.096 us | 1.01x |
| `ruff-noqa/tweaked` | 4,571.459 us | 4,540.156 us | 34,780.500 us | 1.01x |
| `literal-alternate/sherlock-en-nomatch` | 154.959 us | 201.811 us | 571.723 us | 0.77x |
| `literal/sherlock-en` | 58.339 us | 76.295 us | 451.969 us | 0.76x |
| `literal-alternate/sherlock-ru` | 1,612.385 us | 2,302.564 us | 7,306.850 us | 0.70x |
| `bounded-repeat/capitals` | 9,321.125 us | 13,375.217 us | 16,415.675 us | 0.70x |
| `bounded-repeat/letters-en` | 1,103.284 us | 1,795.298 us | 1,879.885 us | 0.61x |
| `aws-keys/full` | 10,916.625 us | 18,157.240 us | 49,362.000 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 970.313 us | 1,942.062 us | 6,880.175 us | 0.50x |
| `bounded-repeat/context` | 21,089.000 us | 68,798.950 us | 70,774.850 us | 0.31x |
| `ruff-noqa/real` | 4,627.753 us | 20,138.200 us | 51,092.500 us | 0.23x |
| `dictionary/single` | 8,160.367 us | 44,677.600 us | 45,120.350 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.190 us | 0.073 us | 0.970 us | 2.60x |
| `industry/mariomka-email-count` | `Count` | 919.157 us | 452.684 us | 7,626.875 us | 2.03x |
| `common/backtracking` | `IsMatch` | 0.085 us | 0.042 us | 0.073 us | 2.01x |
| `common/uri-miss` | `IsMatch` | 0.113 us | 0.066 us | 0.083 us | 1.73x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 106.252 us | 61.924 us | 269.938 us | 1.72x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.047 us | 0.029 us | 0.046 us | 1.63x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.048 us | 0.085 us | 1.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.054 us | 0.076 us | 1.58x |
| `common/replace-words` | `Replace` | 26.988 us | 17.229 us | 18.147 us | 1.57x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.031 us | 0.049 us | 1.56x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.049 us | 0.069 us | 1.53x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.051 us | 0.069 us | 1.44x |
| `industry/leipzig-river-window-count` | `Count` | 2,149.634 us | 1,554.422 us | 7,614.300 us | 1.38x |
| `industry/leipzig-symbol-count` | `Count` | 1,651.213 us | 1,234.272 us | 6,595.025 us | 1.34x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.079 us | 0.105 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.089 us | 0.117 us | 1.11x |
| `industry/mariomka-uri-count` | `Count` | 1,155.955 us | 1,093.109 us | 8,527.567 us | 1.06x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.087 us | 0.119 us | 0.99x |
| `industry/leipzig-twain-count` | `Count` | 1,211.863 us | 1,282.224 us | 7,366.664 us | 0.95x |
| `common/one-node-backtracking` | `IsMatch` | 0.073 us | 0.088 us | 0.129 us | 0.83x |
| `common/split-words` | `Split` | 14.468 us | 20.842 us | 22.894 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.071 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,176.223 us | 3,451.955 us | 10,766.467 us | 0.63x |
| `common/matches-words` | `Count` | 9.180 us | 15.203 us | 17.475 us | 0.60x |
| `common/matches-word` | `Count` | 1.336 us | 2.333 us | 3.376 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,669.113 us | 2,979.962 us | 7,902.550 us | 0.56x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.196 us | 0.230 us | 0.44x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 183.218 us | 588.577 us | 866.033 us | 0.31x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.051 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,826.027 us | 8,766.733 us | 9,388.975 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 513.039 us | 3,900.932 us | 4,293.844 us | 0.13x |
| `common/matches-boundary` | `Count` | 9.539 us | 102.997 us | 104.925 us | 0.09x |
| `common/matches-set` | `Count` | 9.452 us | 103.199 us | 105.106 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 272.894 us | 4,745.969 us | 5,143.860 us | 0.06x |
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
| `lokad/imports/module-imports` | 9.083 us | 6.172 us | 30.618 us | 1.47x |
| `structural/keyword-to-generic-type` | 5.387 us | 4.030 us | 18.075 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.657 us | 1.332 us | 1.422 us | 1.24x |
| `lokad/lexer/doc-line` | 3.155 us | 2.968 us | 3.532 us | 1.06x |
| `lokad/lexer/identifier` | 23.401 us | 22.971 us | 25.617 us | 1.02x |
| `lokad/langserv/helper-identifier` | 6.983 us | 6.973 us | 7.555 us | 1.00x |
| `structural/ordered-keyword-pair` | 5.315 us | 5.396 us | 19.459 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.997 us | 12.518 us | 14.135 us | 0.96x |
| `fallback/lookahead` | 5.631 us | 5.929 us | 19.515 us | 0.95x |
| `lokad/style/hex-color` | 0.711 us | 0.789 us | 0.844 us | 0.90x |
| `lokad/lexer/operator-run` | 2.760 us | 3.388 us | 3.750 us | 0.81x |
| `literal-family/method-token-family` | 7.082 us | 9.074 us | 22.710 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 2.030 us | 2.797 us | 2.911 us | 0.73x |
| `lokad/docs/inline-doc-prefix` | 3.283 us | 4.811 us | 5.308 us | 0.68x |
| `literal/identifier-token` | 2.727 us | 4.301 us | 17.800 us | 0.63x |
| `literal/call-token` | 2.555 us | 4.290 us | 17.861 us | 0.60x |
| `lokad/lexer/number` | 7.894 us | 14.219 us | 15.065 us | 0.56x |
| `structural/ordered-identifier-window` | 14.957 us | 28.441 us | 42.561 us | 0.53x |
| `structural/method-family-generic-call` | 3.381 us | 6.593 us | 20.118 us | 0.51x |
| `lokad/style/cell-ref` | 1.395 us | 2.868 us | 2.896 us | 0.49x |
| `structural/method-family-call` | 3.807 us | 8.000 us | 21.015 us | 0.48x |
| `literal-family/type-token-family` | 10.345 us | 23.675 us | 37.269 us | 0.44x |
| `lokad/style/range-ref` | 0.854 us | 2.613 us | 2.614 us | 0.33x |
| `literal/identifier-token-casei` | 4.610 us | 14.638 us | 28.833 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.154 us | 16.863 us | 30.441 us | 0.31x |
| `structural/ordered-keyword-window` | 5.339 us | 17.636 us | 31.484 us | 0.30x |
| `structural/modifier-family-to-type-window` | 68.877 us | 251.934 us | 265.442 us | 0.27x |
| `lokad/lexer/string` | 21.008 us | 94.454 us | 99.347 us | 0.22x |
| `lokad/folding/region-marker` | 6.060 us | 35.926 us | 62.689 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.320 us | 23.988 us | 24.368 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.231 us | 217.472 us | 231.419 us | 0.05x |
| `lokad/langserv/url-download` | 2.417 us | 49.472 us | 49.633 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.978 us | 133.127 us | 147.790 us | 0.04x |
| `balancing/begin-end-blocks` | 6.405 us | 4,260.121 us | 4,262.417 us | 0.0015x |
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
| `lokad/imports/module-imports` | 9.086 us | 4.297 us | 28.132 us | 2.11x |
| `lokad/langserv/color-short-hex` | 1.636 us | 0.956 us | 1.113 us | 1.71x |
| `lokad/langserv/helper-identifier` | 7.025 us | 4.398 us | 5.493 us | 1.60x |
| `structural/keyword-to-generic-type` | 5.429 us | 3.404 us | 16.945 us | 1.59x |
| `lokad/lexer/doc-line` | 3.152 us | 2.117 us | 2.777 us | 1.49x |
| `lokad/lexer/operator-run` | 2.750 us | 1.910 us | 2.455 us | 1.44x |
| `lokad/style/hex-color` | 0.709 us | 0.493 us | 0.584 us | 1.44x |
| `structural/keyword-family-to-capitalized-identifier` | 11.227 us | 7.822 us | 21.012 us | 1.44x |
| `lokad/lexer/identifier` | 23.382 us | 16.518 us | 20.591 us | 1.42x |
| `structural/ordered-identifier-window` | 14.858 us | 10.540 us | 24.550 us | 1.41x |
| `fallback/lookahead` | 5.662 us | 4.192 us | 17.298 us | 1.35x |
| `lokad/langserv/identifier-validator` | 12.042 us | 9.259 us | 11.321 us | 1.30x |
| `structural/ordered-keyword-pair` | 5.287 us | 4.122 us | 17.474 us | 1.28x |
| `structural/modifier-family-to-type-window` | 69.450 us | 55.353 us | 69.430 us | 1.25x |
| `lokad/docs/inline-doc-prefix` | 3.268 us | 2.695 us | 3.409 us | 1.21x |
| `lokad/langserv/color-rgb-hex` | 2.011 us | 1.755 us | 2.010 us | 1.15x |
| `lokad/lexer/number` | 7.936 us | 7.311 us | 8.136 us | 1.09x |
| `literal-family/type-token-family` | 10.369 us | 11.168 us | 24.303 us | 0.93x |
| `lokad/style/cell-ref` | 1.391 us | 1.714 us | 1.879 us | 0.81x |
| `literal/identifier-token-casei` | 4.582 us | 5.932 us | 19.749 us | 0.77x |
| `structural/method-family-call` | 3.845 us | 5.001 us | 17.792 us | 0.77x |
| `structural/method-family-generic-call` | 3.379 us | 4.466 us | 17.477 us | 0.76x |
| `literal-family/method-token-family` | 3.802 us | 5.263 us | 18.393 us | 0.72x |
| `literal/call-token` | 2.558 us | 3.938 us | 16.934 us | 0.65x |
| `structural/ordered-keyword-window` | 5.342 us | 8.568 us | 22.066 us | 0.62x |
| `structural/ordered-modifier-pair` | 5.173 us | 8.483 us | 21.925 us | 0.61x |
| `lokad/style/range-ref` | 0.851 us | 1.444 us | 1.515 us | 0.59x |
| `literal/identifier-token` | 2.731 us | 4.665 us | 17.143 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 4.931 us | 10.552 us | 24.293 us | 0.47x |
| `lokad/lexer/string` | 20.972 us | 45.768 us | 48.636 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.318 us | 7.311 us | 7.913 us | 0.32x |
| `lokad/langserv/url-download` | 2.411 us | 12.838 us | 13.454 us | 0.19x |
| `lokad/folding/region-marker` | 6.020 us | 35.844 us | 61.019 us | 0.17x |
| `balancing/begin-end-blocks` | 6.463 us | 459.961 us | 488.168 us | 0.01x |
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
