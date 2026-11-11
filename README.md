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

The tables were refreshed on **11 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m40s**. The [run receipt](bench/Experiments/DeliveryDev72-20261111/full-refresh.json)
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
| `literal/sherlock-zh` | 83.948 us | 19.218 us | 2,213.053 us | 4.37x |
| `literal/sherlock-ru` | 169.697 us | 97.722 us | 5,464.300 us | 1.74x |
| `literal-alternate/sherlock-zh` | 106.355 us | 69.579 us | 2,207.506 us | 1.53x |
| `literal/sherlock-casei-en` | 158.914 us | 185.372 us | 594.966 us | 0.86x |
| `literal/sherlock-casei-ru` | 251.428 us | 368.008 us | 5,607.800 us | 0.68x |
| `literal/sherlock-en` | 56.839 us | 84.767 us | 493.724 us | 0.67x |
| `ruff-noqa/tweaked` | 4,358.822 us | 7,585.809 us | 44,427.050 us | 0.57x |
| `bounded-repeat/letters-en` | 1,112.524 us | 2,839.052 us | 2,938.181 us | 0.39x |
| `aws-keys/quick` | 3,771.950 us | 9,772.938 us | 40,926.550 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,015.113 us | 5,874.008 us | 6,690.583 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 194.758 us | 599.652 us | 1,032.719 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,543.173 us | 4,762.637 us | 9,971.125 us | 0.32x |
| `dictionary/single` | 8,401.700 us | 26,306.033 us | 26,488.867 us | 0.32x |
| `literal-alternate/sherlock-en` | 371.589 us | 1,516.143 us | 2,058.556 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 154.219 us | 648.028 us | 1,091.090 us | 0.24x |
| `bounded-repeat/capitals` | 9,496.750 us | 45,690.000 us | 49,235.850 us | 0.21x |
| `bounded-repeat/context` | 22,485.500 us | 153,492.200 us | 157,858.300 us | 0.15x |
| `aws-keys/full` | 11,031.487 us | 87,262.600 us | 117,069.000 us | 0.13x |
| `ruff-noqa/real` | 4,399.911 us | 70,155.400 us | 87,014.700 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 966.680 us | 23,096.125 us | 28,337.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 137.005 us | 11,511.300 us | 12,146.500 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,176.839 us | 490.509 us | 8,982.500 us | 2.40x |
| `common/match-word` | `Match` | 0.194 us | 0.169 us | 1.140 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,172.542 us | 1,339.076 us | 8,660.587 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,432.724 us | 1,932.258 us | 10,445.967 us | 0.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.110 us | 0.145 us | 0.74x |
| `common/uri-miss` | `IsMatch` | 0.124 us | 0.192 us | 0.210 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.086 us | 0.102 us | 0.58x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.164 us | 0.183 us | 0.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 119.411 us | 256.643 us | 493.308 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.173 us | 0.194 us | 0.45x |
| `common/ip-miss` | `IsMatch` | 0.080 us | 0.188 us | 0.207 us | 0.43x |
| `common/uri-match` | `IsMatch` | 0.098 us | 0.239 us | 0.277 us | 0.41x |
| `common/replace-words` | `Replace` | 27.612 us | 77.604 us | 79.637 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.270 us | 0.296 us | 0.35x |
| `common/matches-word` | `Count` | 1.362 us | 4.269 us | 5.972 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.347 us | 0.373 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,301.985 us | 8,700.044 us | 17,415.800 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.110 us | 0.139 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.098 us | 0.21x |
| `common/split-words` | `Split` | 14.654 us | 78.441 us | 79.835 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.652 us | 964.542 us | 1,274.483 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.562 us | 0.592 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,605.976 us | 10,092.562 us | 16,964.033 us | 0.16x |
| `common/matches-words` | `Count` | 9.289 us | 69.550 us | 71.441 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.613 us | 0.651 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.836 us | 0.878 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,926.513 us | 19,812.700 us | 20,365.225 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,899.495 us | 22,586.800 us | 29,923.200 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,253.338 us | 33,957.100 us | 41,282.100 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 483.490 us | 8,202.190 us | 8,723.533 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.826 us | 165.408 us | 167.004 us | 0.05x |
| `common/matches-set` | `Count` | 7.196 us | 196.814 us | 197.848 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.250 us | 12,477.371 us | 12,967.067 us | 0.02x |
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
| `literal/sherlock-zh` | 84.001 us | 24.314 us | 2,213.839 us | 3.45x |
| `literal-alternate/sherlock-zh` | 105.400 us | 36.514 us | 2,224.674 us | 2.89x |
| `literal/sherlock-casei-en` | 157.670 us | 78.096 us | 429.853 us | 2.02x |
| `literal/sherlock-ru` | 169.075 us | 90.121 us | 5,352.620 us | 1.88x |
| `literal-alternate/sherlock-en-mixed` | 198.918 us | 177.242 us | 568.807 us | 1.12x |
| `literal/sherlock-casei-ru` | 251.311 us | 227.634 us | 5,337.014 us | 1.10x |
| `aws-keys/quick` | 3,826.030 us | 3,611.252 us | 35,084.833 us | 1.06x |
| `literal-alternate/sherlock-casei-en` | 138.415 us | 131.976 us | 583.214 us | 1.05x |
| `literal-alternate/sherlock-en` | 372.719 us | 373.995 us | 823.557 us | 1.00x |
| `bounded-repeat/letters-ru` | 2,026.266 us | 2,061.557 us | 2,866.431 us | 0.98x |
| `ruff-noqa/tweaked` | 4,499.378 us | 4,591.712 us | 38,567.450 us | 0.98x |
| `literal-alternate/sherlock-en-nomatch` | 154.500 us | 202.083 us | 585.922 us | 0.76x |
| `literal/sherlock-en` | 56.940 us | 74.512 us | 417.470 us | 0.76x |
| `bounded-repeat/capitals` | 9,496.600 us | 13,698.250 us | 16,986.825 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,538.682 us | 2,352.703 us | 7,483.525 us | 0.65x |
| `bounded-repeat/letters-en` | 1,115.943 us | 1,854.366 us | 1,937.703 us | 0.60x |
| `aws-keys/full` | 11,041.900 us | 18,750.800 us | 50,620.267 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 963.151 us | 1,952.767 us | 6,994.125 us | 0.49x |
| `bounded-repeat/context` | 22,859.225 us | 70,163.550 us | 73,555.150 us | 0.33x |
| `ruff-noqa/real` | 4,415.250 us | 20,447.825 us | 47,469.967 us | 0.22x |
| `dictionary/single` | 8,377.122 us | 45,964.550 us | 46,162.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,182.234 us | 455.337 us | 7,681.575 us | 2.60x |
| `common/match-word` | `Match` | 0.193 us | 0.080 us | 1.009 us | 2.41x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 119.451 us | 58.507 us | 272.233 us | 2.04x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.044 us | 0.075 us | 2.00x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.066 us | 0.088 us | 1.84x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.030 us | 0.048 us | 1.82x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.032 us | 0.049 us | 1.71x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.055 us | 0.077 us | 1.62x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.086 us | 1.62x |
| `common/replace-words` | `Replace` | 27.568 us | 18.015 us | 20.689 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,900.963 us | 1,246.682 us | 6,941.300 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.072 us | 0.049 us | 0.069 us | 1.49x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.052 us | 0.071 us | 1.47x |
| `industry/leipzig-river-window-count` | `Count` | 2,258.409 us | 1,564.514 us | 7,910.100 us | 1.44x |
| `industry/mariomka-uri-count` | `Count` | 1,436.558 us | 1,112.600 us | 8,577.233 us | 1.29x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.081 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.090 us | 0.116 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.089 us | 0.120 us | 1.05x |
| `industry/leipzig-twain-count` | `Count` | 1,175.848 us | 1,301.986 us | 8,323.667 us | 0.90x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.088 us | 0.128 us | 0.85x |
| `common/split-words` | `Split` | 14.684 us | 20.983 us | 22.602 us | 0.70x |
| `industry/mariomka-ip-count` | `Count` | 2,310.388 us | 3,526.273 us | 11,053.933 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.073 us | 0.65x |
| `common/matches-words` | `Count` | 9.297 us | 15.335 us | 17.721 us | 0.61x |
| `common/matches-word` | `Count` | 1.358 us | 2.378 us | 3.475 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,597.617 us | 3,011.396 us | 7,862.050 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.199 us | 0.237 us | 0.50x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.538 us | 591.423 us | 852.298 us | 0.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.055 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,945.433 us | 8,992.511 us | 9,499.788 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 484.341 us | 4,095.216 us | 4,439.644 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.841 us | 105.117 us | 106.686 us | 0.07x |
| `common/matches-set` | `Count` | 7.176 us | 107.122 us | 108.572 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.834 us | 4,852.781 us | 5,287.233 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.257 us | 6.365 us | 31.593 us | 1.45x |
| `structural/keyword-to-generic-type` | 5.131 us | 4.155 us | 18.430 us | 1.24x |
| `lokad/langserv/color-short-hex` | 1.650 us | 1.367 us | 1.501 us | 1.21x |
| `lokad/lexer/doc-line` | 3.226 us | 3.009 us | 3.688 us | 1.07x |
| `structural/ordered-keyword-pair` | 5.537 us | 5.345 us | 19.620 us | 1.04x |
| `lokad/lexer/identifier` | 23.668 us | 23.033 us | 25.468 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.157 us | 7.363 us | 7.919 us | 0.97x |
| `fallback/lookahead` | 5.480 us | 6.113 us | 19.600 us | 0.90x |
| `lokad/style/hex-color` | 0.729 us | 0.822 us | 0.858 us | 0.89x |
| `lokad/langserv/identifier-validator` | 11.171 us | 12.691 us | 14.022 us | 0.88x |
| `lokad/lexer/operator-run` | 2.813 us | 3.495 us | 3.870 us | 0.80x |
| `literal-family/method-token-family` | 7.168 us | 9.185 us | 22.361 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 2.007 us | 2.972 us | 2.986 us | 0.68x |
| `lokad/docs/inline-doc-prefix` | 3.286 us | 4.894 us | 5.437 us | 0.67x |
| `literal/identifier-token` | 2.794 us | 4.281 us | 17.851 us | 0.65x |
| `literal/call-token` | 2.598 us | 4.338 us | 17.838 us | 0.60x |
| `lokad/lexer/number` | 8.037 us | 14.640 us | 15.769 us | 0.55x |
| `structural/ordered-identifier-window` | 14.911 us | 28.805 us | 42.521 us | 0.52x |
| `structural/method-family-generic-call` | 3.466 us | 6.814 us | 20.304 us | 0.51x |
| `structural/method-family-call` | 3.868 us | 8.033 us | 21.345 us | 0.48x |
| `lokad/style/cell-ref` | 1.424 us | 2.966 us | 3.109 us | 0.48x |
| `literal-family/type-token-family` | 10.546 us | 24.347 us | 38.450 us | 0.43x |
| `literal/identifier-token-casei` | 5.779 us | 15.423 us | 28.667 us | 0.37x |
| `lokad/style/range-ref` | 0.886 us | 2.769 us | 2.795 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.297 us | 17.308 us | 31.271 us | 0.31x |
| `structural/ordered-keyword-window` | 5.490 us | 18.013 us | 31.769 us | 0.30x |
| `structural/modifier-family-to-type-window` | 71.553 us | 259.639 us | 273.727 us | 0.28x |
| `lokad/lexer/string` | 21.292 us | 95.791 us | 100.653 us | 0.22x |
| `lokad/folding/region-marker` | 6.079 us | 37.775 us | 64.841 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.379 us | 24.629 us | 25.043 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.392 us | 224.163 us | 239.053 us | 0.05x |
| `lokad/langserv/url-download` | 2.484 us | 50.762 us | 51.229 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.014 us | 136.168 us | 150.752 us | 0.04x |
| `balancing/begin-end-blocks` | 5.731 us | 4,389.183 us | 4,402.459 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.238 us | 4.468 us | 28.628 us | 2.07x |
| `lokad/langserv/color-short-hex` | 1.642 us | 1.003 us | 1.194 us | 1.64x |
| `lokad/langserv/helper-identifier` | 7.139 us | 4.696 us | 5.764 us | 1.52x |
| `lokad/lexer/doc-line` | 3.232 us | 2.151 us | 2.995 us | 1.50x |
| `structural/keyword-to-generic-type` | 5.138 us | 3.484 us | 17.263 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.397 us | 7.915 us | 21.201 us | 1.44x |
| `lokad/lexer/identifier` | 23.649 us | 16.784 us | 20.303 us | 1.41x |
| `structural/ordered-identifier-window` | 14.887 us | 10.597 us | 24.333 us | 1.40x |
| `lokad/lexer/operator-run` | 2.813 us | 2.003 us | 2.552 us | 1.40x |
| `lokad/style/hex-color` | 0.730 us | 0.520 us | 0.595 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.540 us | 4.171 us | 17.711 us | 1.33x |
| `fallback/lookahead` | 5.479 us | 4.243 us | 17.295 us | 1.29x |
| `structural/modifier-family-to-type-window` | 71.733 us | 56.198 us | 71.778 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.059 us | 9.083 us | 11.017 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.280 us | 2.817 us | 3.533 us | 1.16x |
| `lokad/langserv/color-rgb-hex` | 1.991 us | 1.835 us | 2.065 us | 1.09x |
| `lokad/lexer/number` | 8.039 us | 7.584 us | 8.665 us | 1.06x |
| `literal/identifier-token-casei` | 5.791 us | 5.961 us | 19.789 us | 0.97x |
| `literal-family/type-token-family` | 10.590 us | 11.517 us | 24.980 us | 0.92x |
| `lokad/style/cell-ref` | 1.447 us | 1.799 us | 1.996 us | 0.80x |
| `structural/method-family-generic-call` | 3.466 us | 4.525 us | 17.371 us | 0.77x |
| `structural/method-family-call` | 3.869 us | 5.084 us | 17.996 us | 0.76x |
| `literal-family/method-token-family` | 3.894 us | 5.343 us | 18.298 us | 0.73x |
| `literal/call-token` | 2.602 us | 3.988 us | 16.815 us | 0.65x |
| `structural/ordered-keyword-window` | 5.498 us | 8.615 us | 22.201 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.300 us | 8.686 us | 22.328 us | 0.61x |
| `literal/identifier-token` | 2.790 us | 4.731 us | 17.517 us | 0.59x |
| `lokad/style/range-ref` | 0.884 us | 1.516 us | 1.603 us | 0.58x |
| `structural/modifier-family-to-type-pair` | 5.015 us | 10.834 us | 25.214 us | 0.46x |
| `lokad/lexer/string` | 21.321 us | 46.332 us | 49.882 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.377 us | 7.472 us | 8.016 us | 0.32x |
| `lokad/langserv/url-download` | 2.485 us | 13.049 us | 13.595 us | 0.19x |
| `lokad/folding/region-marker` | 6.061 us | 37.185 us | 62.307 us | 0.16x |
| `balancing/begin-end-blocks` | 5.731 us | 471.171 us | 500.958 us | 0.01x |
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
