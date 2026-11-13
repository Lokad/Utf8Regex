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
and resource checks passed in **47m28s**. The [run receipt](bench/Experiments/DeliveryDev79-20261113/full-refresh.json)
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
| `literal/sherlock-zh` | 96.764 us | 18.805 us | 2,158.809 us | 5.15x |
| `literal-alternate/sherlock-zh` | 120.045 us | 68.064 us | 2,204.144 us | 1.76x |
| `literal/sherlock-ru` | 165.914 us | 96.102 us | 5,274.347 us | 1.73x |
| `literal/sherlock-casei-en` | 156.679 us | 194.401 us | 648.696 us | 0.81x |
| `literal/sherlock-casei-ru` | 234.306 us | 360.938 us | 5,575.221 us | 0.65x |
| `literal/sherlock-en` | 55.869 us | 86.948 us | 482.419 us | 0.64x |
| `ruff-noqa/tweaked` | 4,406.639 us | 7,588.109 us | 43,771.500 us | 0.58x |
| `aws-keys/quick` | 3,803.080 us | 9,359.513 us | 45,735.000 us | 0.41x |
| `bounded-repeat/letters-en` | 1,127.646 us | 2,805.400 us | 2,898.354 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,045.792 us | 5,858.285 us | 6,671.583 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 194.977 us | 585.221 us | 1,013.564 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,540.568 us | 4,774.756 us | 9,803.200 us | 0.32x |
| `dictionary/single` | 8,274.410 us | 26,761.267 us | 26,942.767 us | 0.31x |
| `literal-alternate/sherlock-en` | 374.042 us | 1,492.670 us | 2,045.660 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 151.689 us | 655.184 us | 1,084.644 us | 0.23x |
| `bounded-repeat/capitals` | 9,466.763 us | 44,560.900 us | 47,564.150 us | 0.21x |
| `bounded-repeat/context` | 22,304.800 us | 146,307.000 us | 150,627.300 us | 0.15x |
| `aws-keys/full` | 10,787.737 us | 85,969.800 us | 116,642.300 us | 0.13x |
| `ruff-noqa/real` | 4,358.756 us | 69,445.500 us | 100,763.600 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 954.095 us | 23,201.225 us | 28,630.200 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 126.849 us | 11,183.757 us | 12,075.557 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,037.823 us | 504.076 us | 8,869.225 us | 2.06x |
| `common/match-word` | `Match` | 0.200 us | 0.169 us | 1.118 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,244.462 us | 1,358.941 us | 8,480.722 us | 0.92x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.109 us | 0.146 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.123 us | 0.183 us | 0.201 us | 0.68x |
| `industry/mariomka-uri-count` | `Count` | 1,230.527 us | 1,847.576 us | 10,380.433 us | 0.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.101 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.092 us | 0.167 us | 0.183 us | 0.55x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.179 us | 0.197 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.078 us | 0.185 us | 0.205 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.226 us | 0.260 us | 0.42x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 102.979 us | 262.844 us | 505.365 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.265 us | 0.292 us | 0.36x |
| `common/replace-words` | `Replace` | 27.212 us | 77.769 us | 78.938 us | 0.35x |
| `common/matches-word` | `Count` | 1.348 us | 4.212 us | 5.931 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.339 us | 0.366 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,131.354 us | 8,242.730 us | 16,565.567 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.108 us | 0.139 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.20x |
| `common/split-words` | `Split` | 14.309 us | 75.417 us | 77.340 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.697 us | 960.885 us | 1,231.195 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.557 us | 0.594 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,532.512 us | 9,783.962 us | 16,629.100 us | 0.16x |
| `common/matches-words` | `Count` | 9.339 us | 69.142 us | 71.280 us | 0.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.089 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.618 us | 0.652 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.838 us | 0.874 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,818.860 us | 19,495.025 us | 20,151.375 us | 0.09x |
| `industry/leipzig-river-window-count` | `Count` | 2,148.526 us | 31,425.700 us | 38,142.550 us | 0.07x |
| `industry/leipzig-symbol-count` | `Count` | 1,502.250 us | 22,419.175 us | 28,959.750 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 459.728 us | 8,191.920 us | 8,705.678 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.960 us | 167.638 us | 169.694 us | 0.04x |
| `common/matches-set` | `Count` | 6.915 us | 193.763 us | 195.109 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 256.463 us | 11,875.929 us | 12,452.717 us | 0.02x |
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
| `literal/sherlock-zh` | 96.572 us | 23.933 us | 2,158.676 us | 4.04x |
| `literal-alternate/sherlock-zh` | 118.299 us | 36.331 us | 2,195.309 us | 3.26x |
| `literal/sherlock-casei-en` | 158.141 us | 77.732 us | 437.979 us | 2.03x |
| `literal/sherlock-ru` | 166.407 us | 87.158 us | 5,234.800 us | 1.91x |
| `literal/sherlock-casei-ru` | 233.425 us | 196.023 us | 5,322.960 us | 1.19x |
| `literal-alternate/sherlock-en-mixed` | 194.350 us | 180.291 us | 552.535 us | 1.08x |
| `aws-keys/quick` | 3,780.115 us | 3,630.845 us | 36,773.650 us | 1.04x |
| `literal-alternate/sherlock-en` | 375.806 us | 369.133 us | 829.683 us | 1.02x |
| `bounded-repeat/letters-ru` | 2,015.139 us | 2,055.238 us | 2,893.569 us | 0.98x |
| `ruff-noqa/tweaked` | 4,405.229 us | 4,559.365 us | 37,585.500 us | 0.97x |
| `literal-alternate/sherlock-casei-en` | 126.170 us | 148.016 us | 549.175 us | 0.85x |
| `literal-alternate/sherlock-en-nomatch` | 153.123 us | 204.843 us | 599.675 us | 0.75x |
| `literal/sherlock-en` | 56.063 us | 75.711 us | 433.537 us | 0.74x |
| `bounded-repeat/capitals` | 9,483.025 us | 13,641.817 us | 16,356.125 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,545.359 us | 2,350.170 us | 7,443.875 us | 0.66x |
| `bounded-repeat/letters-en` | 1,132.306 us | 1,826.024 us | 1,908.082 us | 0.62x |
| `aws-keys/full` | 10,822.043 us | 18,225.380 us | 49,811.967 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 950.102 us | 1,973.100 us | 7,113.475 us | 0.48x |
| `bounded-repeat/context` | 22,754.200 us | 68,303.250 us | 71,554.650 us | 0.33x |
| `ruff-noqa/real` | 4,506.594 us | 20,645.400 us | 51,552.333 us | 0.22x |
| `dictionary/single` | 8,207.000 us | 45,776.400 us | 46,057.600 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.198 us | 0.076 us | 1.011 us | 2.62x |
| `industry/mariomka-email-count` | `Count` | 1,040.143 us | 467.084 us | 7,651.950 us | 2.23x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.043 us | 0.073 us | 2.02x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.056 us | 0.030 us | 0.048 us | 1.86x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.069 us | 0.085 us | 1.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.032 us | 0.049 us | 1.74x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 102.621 us | 60.573 us | 276.512 us | 1.69x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.090 us | 0.054 us | 0.075 us | 1.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.048 us | 0.082 us | 1.61x |
| `common/replace-words` | `Replace` | 27.252 us | 17.434 us | 19.778 us | 1.56x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.048 us | 0.068 us | 1.52x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.050 us | 0.069 us | 1.48x |
| `industry/leipzig-river-window-count` | `Count` | 2,155.300 us | 1,552.224 us | 7,521.425 us | 1.39x |
| `industry/leipzig-symbol-count` | `Count` | 1,525.170 us | 1,199.265 us | 6,798.075 us | 1.27x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.080 us | 0.108 us | 1.19x |
| `industry/mariomka-uri-count` | `Count` | 1,247.875 us | 1,091.571 us | 8,710.225 us | 1.14x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.091 us | 0.115 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.088 us | 0.119 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,253.661 us | 1,326.029 us | 7,318.730 us | 0.95x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.090 us | 0.127 us | 0.84x |
| `common/split-words` | `Split` | 14.188 us | 20.478 us | 22.478 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.072 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,143.220 us | 3,469.573 us | 10,893.433 us | 0.62x |
| `common/matches-words` | `Count` | 9.301 us | 15.159 us | 17.572 us | 0.61x |
| `common/matches-word` | `Count` | 1.349 us | 2.332 us | 3.384 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 1,567.878 us | 2,945.759 us | 8,337.383 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.196 us | 0.237 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.053 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 170.646 us | 590.250 us | 845.188 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,814.576 us | 8,947.811 us | 9,443.962 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 468.070 us | 3,934.174 us | 4,328.861 us | 0.12x |
| `common/matches-boundary` | `Count` | 6.964 us | 103.005 us | 105.152 us | 0.07x |
| `common/matches-set` | `Count` | 6.939 us | 102.915 us | 105.421 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 257.967 us | 4,781.875 us | 5,201.371 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.245 us | 6.335 us | 31.228 us | 1.46x |
| `structural/keyword-to-generic-type` | 5.118 us | 4.084 us | 18.295 us | 1.25x |
| `lokad/langserv/color-short-hex` | 1.632 us | 1.322 us | 1.395 us | 1.23x |
| `lokad/lexer/doc-line` | 3.199 us | 3.024 us | 3.657 us | 1.06x |
| `lokad/lexer/identifier` | 23.429 us | 22.481 us | 24.906 us | 1.04x |
| `lokad/langserv/helper-identifier` | 7.242 us | 7.111 us | 7.770 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.239 us | 5.245 us | 19.882 us | 1.00x |
| `lokad/langserv/identifier-validator` | 11.081 us | 12.105 us | 13.477 us | 0.92x |
| `fallback/lookahead` | 5.247 us | 5.956 us | 19.322 us | 0.88x |
| `lokad/lexer/operator-run` | 2.793 us | 3.437 us | 3.761 us | 0.81x |
| `literal-family/method-token-family` | 7.180 us | 9.083 us | 22.909 us | 0.79x |
| `lokad/style/hex-color` | 0.557 us | 0.794 us | 0.849 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.028 us | 2.989 us | 2.988 us | 0.68x |
| `lokad/docs/inline-doc-prefix` | 3.242 us | 4.894 us | 5.516 us | 0.66x |
| `literal/identifier-token` | 2.784 us | 4.331 us | 17.820 us | 0.64x |
| `literal/call-token` | 2.537 us | 4.250 us | 17.966 us | 0.60x |
| `lokad/lexer/number` | 7.919 us | 14.136 us | 14.736 us | 0.56x |
| `structural/ordered-identifier-window` | 14.401 us | 28.250 us | 41.915 us | 0.51x |
| `structural/method-family-generic-call` | 3.393 us | 6.694 us | 20.314 us | 0.51x |
| `lokad/style/cell-ref` | 1.416 us | 2.898 us | 2.947 us | 0.49x |
| `structural/method-family-call` | 3.837 us | 7.873 us | 22.421 us | 0.49x |
| `literal-family/type-token-family` | 10.429 us | 22.970 us | 36.655 us | 0.45x |
| `literal/identifier-token-casei` | 5.897 us | 15.288 us | 28.334 us | 0.39x |
| `lokad/style/range-ref` | 0.864 us | 2.681 us | 2.601 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.434 us | 17.300 us | 31.177 us | 0.31x |
| `structural/ordered-keyword-window` | 5.416 us | 17.799 us | 32.207 us | 0.30x |
| `structural/modifier-family-to-type-window` | 67.926 us | 253.149 us | 270.649 us | 0.27x |
| `lokad/lexer/string` | 21.103 us | 94.964 us | 98.717 us | 0.22x |
| `lokad/folding/region-marker` | 5.999 us | 35.808 us | 62.224 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.352 us | 24.236 us | 24.653 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.314 us | 221.362 us | 234.763 us | 0.05x |
| `lokad/langserv/url-download` | 2.475 us | 52.295 us | 52.997 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.971 us | 135.362 us | 149.718 us | 0.04x |
| `balancing/begin-end-blocks` | 5.673 us | 4,198.528 us | 4,214.028 us | 0.0014x |
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
| `lokad/imports/module-imports` | 9.243 us | 4.389 us | 28.645 us | 2.11x |
| `lokad/langserv/color-short-hex` | 1.626 us | 0.940 us | 1.093 us | 1.73x |
| `lokad/langserv/helper-identifier` | 7.272 us | 4.627 us | 5.638 us | 1.57x |
| `structural/keyword-to-generic-type` | 5.042 us | 3.450 us | 17.162 us | 1.46x |
| `lokad/lexer/identifier` | 23.460 us | 16.110 us | 20.267 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.390 us | 7.871 us | 21.113 us | 1.45x |
| `lokad/lexer/doc-line` | 3.197 us | 2.249 us | 2.901 us | 1.42x |
| `lokad/lexer/operator-run` | 2.766 us | 1.982 us | 2.504 us | 1.40x |
| `structural/ordered-identifier-window` | 14.424 us | 10.636 us | 24.050 us | 1.36x |
| `structural/ordered-keyword-pair` | 5.219 us | 4.056 us | 17.731 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.196 us | 8.725 us | 10.653 us | 1.28x |
| `fallback/lookahead` | 5.222 us | 4.200 us | 17.198 us | 1.24x |
| `structural/modifier-family-to-type-window` | 68.474 us | 55.122 us | 71.901 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.234 us | 2.791 us | 3.513 us | 1.16x |
| `lokad/style/hex-color` | 0.561 us | 0.494 us | 0.573 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.036 us | 1.821 us | 1.999 us | 1.12x |
| `lokad/lexer/number` | 7.974 us | 7.154 us | 8.163 us | 1.11x |
| `literal/identifier-token-casei` | 5.877 us | 5.840 us | 19.444 us | 1.01x |
| `literal-family/type-token-family` | 10.425 us | 11.457 us | 24.686 us | 0.91x |
| `lokad/style/cell-ref` | 1.409 us | 1.667 us | 1.886 us | 0.84x |
| `structural/method-family-generic-call` | 3.409 us | 4.457 us | 17.358 us | 0.76x |
| `structural/method-family-call` | 3.823 us | 5.018 us | 17.805 us | 0.76x |
| `literal-family/method-token-family` | 3.891 us | 5.309 us | 18.588 us | 0.73x |
| `literal/call-token` | 2.562 us | 3.919 us | 17.016 us | 0.65x |
| `structural/ordered-keyword-window` | 5.462 us | 8.583 us | 22.500 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.388 us | 8.605 us | 22.236 us | 0.63x |
| `lokad/style/range-ref` | 0.866 us | 1.417 us | 1.521 us | 0.61x |
| `literal/identifier-token` | 2.782 us | 4.685 us | 17.151 us | 0.59x |
| `lokad/lexer/string` | 21.182 us | 44.833 us | 46.749 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.964 us | 10.668 us | 24.148 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.335 us | 7.498 us | 7.969 us | 0.31x |
| `lokad/langserv/url-download` | 2.474 us | 12.857 us | 13.599 us | 0.19x |
| `lokad/folding/region-marker` | 6.002 us | 35.700 us | 61.201 us | 0.17x |
| `balancing/begin-end-blocks` | 5.689 us | 467.609 us | 497.310 us | 0.01x |
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
