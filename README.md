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
and resource checks completed in **46m12s**. The [run receipt](bench/Experiments/DeliveryDev83-20261113/full-refresh.json)
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
| `literal/sherlock-zh` | 92.905 us | 18.066 us | 2,121.817 us | 5.14x |
| `literal-alternate/sherlock-zh` | 115.703 us | 66.781 us | 2,154.257 us | 1.73x |
| `literal/sherlock-ru` | 162.982 us | 96.287 us | 5,170.600 us | 1.69x |
| `literal/sherlock-casei-en` | 150.853 us | 180.374 us | 588.993 us | 0.84x |
| `literal/sherlock-casei-ru` | 234.647 us | 360.059 us | 5,468.057 us | 0.65x |
| `literal/sherlock-en` | 54.993 us | 85.007 us | 473.508 us | 0.65x |
| `ruff-noqa/tweaked` | 4,445.718 us | 7,481.482 us | 37,165.550 us | 0.59x |
| `aws-keys/quick` | 3,779.481 us | 9,478.038 us | 46,007.600 us | 0.40x |
| `bounded-repeat/letters-en` | 1,078.617 us | 2,762.961 us | 2,847.938 us | 0.39x |
| `bounded-repeat/letters-ru` | 1,971.946 us | 5,709.143 us | 6,515.733 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,499.996 us | 4,651.524 us | 9,892.000 us | 0.32x |
| `dictionary/single` | 7,906.400 us | 25,369.267 us | 25,401.300 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 180.354 us | 584.478 us | 1,017.532 us | 0.31x |
| `literal-alternate/sherlock-en` | 369.492 us | 1,494.372 us | 2,117.881 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 148.936 us | 651.180 us | 1,072.790 us | 0.23x |
| `bounded-repeat/capitals` | 9,351.362 us | 45,210.000 us | 48,133.850 us | 0.21x |
| `bounded-repeat/context` | 21,628.700 us | 142,635.400 us | 148,475.600 us | 0.15x |
| `aws-keys/full` | 8,630.856 us | 82,860.000 us | 112,005.600 us | 0.10x |
| `ruff-noqa/real` | 4,477.324 us | 67,803.500 us | 99,293.600 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 924.973 us | 22,611.775 us | 27,748.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 128.781 us | 11,419.871 us | 12,313.971 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,017.686 us | 495.673 us | 8,784.250 us | 2.05x |
| `common/match-word` | `Match` | 0.190 us | 0.167 us | 1.094 us | 1.13x |
| `industry/leipzig-twain-count` | `Count` | 1,239.495 us | 1,356.037 us | 8,475.163 us | 0.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.107 us | 0.142 us | 0.73x |
| `industry/mariomka-uri-count` | `Count` | 1,208.130 us | 1,854.336 us | 10,304.550 us | 0.65x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.188 us | 0.206 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.081 us | 0.097 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.163 us | 0.181 us | 0.53x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 107.247 us | 248.832 us | 473.859 us | 0.43x |
| `common/uri-match` | `IsMatch` | 0.095 us | 0.230 us | 0.263 us | 0.41x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.175 us | 0.193 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.072 us | 0.188 us | 0.208 us | 0.38x |
| `common/replace-words` | `Replace` | 26.719 us | 74.119 us | 76.263 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.268 us | 0.292 us | 0.35x |
| `common/matches-word` | `Count` | 1.340 us | 4.198 us | 5.838 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.096 us | 0.338 us | 0.363 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,074.892 us | 8,111.610 us | 16,278.333 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.106 us | 0.135 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.081 us | 0.095 us | 0.20x |
| `common/split-words` | `Split` | 14.289 us | 78.032 us | 78.787 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.827 us | 948.749 us | 1,214.414 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,580.086 us | 9,654.300 us | 16,494.433 us | 0.16x |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.537 us | 0.576 us | 0.16x |
| `common/matches-words` | `Count` | 9.133 us | 67.976 us | 69.054 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.604 us | 0.637 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.074 us | 0.087 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.086 us | 0.803 us | 0.839 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,766.402 us | 19,245.325 us | 19,788.925 us | 0.09x |
| `industry/leipzig-river-window-count` | `Count` | 2,126.092 us | 31,201.967 us | 38,806.250 us | 0.07x |
| `industry/leipzig-symbol-count` | `Count` | 1,485.067 us | 22,468.225 us | 29,329.600 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 449.783 us | 8,112.290 us | 8,654.678 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.892 us | 160.440 us | 163.086 us | 0.04x |
| `common/matches-set` | `Count` | 6.982 us | 188.904 us | 189.468 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 230.614 us | 11,629.543 us | 12,216.329 us | 0.02x |
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
| `literal/sherlock-zh` | 92.976 us | 22.894 us | 2,150.411 us | 4.06x |
| `literal-alternate/sherlock-zh` | 115.544 us | 35.244 us | 2,131.547 us | 3.28x |
| `literal/sherlock-casei-en` | 150.204 us | 76.715 us | 421.961 us | 1.96x |
| `literal/sherlock-ru` | 165.357 us | 87.472 us | 5,170.327 us | 1.89x |
| `literal/sherlock-casei-ru` | 235.076 us | 216.706 us | 5,251.513 us | 1.08x |
| `aws-keys/quick` | 3,801.090 us | 3,565.341 us | 38,382.250 us | 1.07x |
| `literal-alternate/sherlock-en-mixed` | 182.438 us | 173.540 us | 581.650 us | 1.05x |
| `literal-alternate/sherlock-en` | 366.131 us | 362.651 us | 771.309 us | 1.01x |
| `ruff-noqa/tweaked` | 4,520.435 us | 4,547.689 us | 35,829.600 us | 0.99x |
| `bounded-repeat/letters-ru` | 1,965.595 us | 2,022.041 us | 2,834.815 us | 0.97x |
| `literal-alternate/sherlock-casei-en` | 129.255 us | 139.832 us | 551.533 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 150.383 us | 201.376 us | 582.391 us | 0.75x |
| `literal/sherlock-en` | 55.263 us | 74.770 us | 437.990 us | 0.74x |
| `bounded-repeat/capitals` | 9,337.625 us | 13,065.433 us | 16,030.225 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,498.718 us | 2,278.500 us | 7,485.400 us | 0.66x |
| `bounded-repeat/letters-en` | 1,073.291 us | 1,776.502 us | 1,859.239 us | 0.60x |
| `aws-keys/full` | 10,347.550 us | 17,852.420 us | 48,788.500 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 923.762 us | 1,916.088 us | 7,002.625 us | 0.48x |
| `bounded-repeat/context` | 21,417.475 us | 66,953.000 us | 70,106.050 us | 0.32x |
| `ruff-noqa/real` | 4,504.194 us | 19,716.775 us | 54,214.400 us | 0.23x |
| `dictionary/single` | 7,850.430 us | 44,347.650 us | 44,295.350 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.190 us | 0.075 us | 0.989 us | 2.53x |
| `industry/mariomka-email-count` | `Count` | 1,024.723 us | 464.383 us | 7,507.275 us | 2.21x |
| `common/backtracking` | `IsMatch` | 0.085 us | 0.044 us | 0.072 us | 1.95x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 107.181 us | 58.006 us | 263.046 us | 1.85x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.065 us | 0.086 us | 1.83x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.030 us | 0.046 us | 1.83x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.031 us | 0.048 us | 1.71x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.046 us | 0.067 us | 1.64x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.088 us | 0.054 us | 0.076 us | 1.62x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.048 us | 0.083 us | 1.62x |
| `common/replace-words` | `Replace` | 26.791 us | 16.882 us | 19.234 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.049 us | 0.069 us | 1.55x |
| `industry/leipzig-river-window-count` | `Count` | 2,163.533 us | 1,521.030 us | 7,593.850 us | 1.42x |
| `industry/leipzig-symbol-count` | `Count` | 1,493.853 us | 1,178.214 us | 6,958.440 us | 1.27x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.080 us | 0.107 us | 1.17x |
| `industry/mariomka-uri-count` | `Count` | 1,206.953 us | 1,079.792 us | 8,228.875 us | 1.12x |
| `common/email-miss` | `IsMatch` | 0.096 us | 0.087 us | 0.113 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.087 us | 0.118 us | 1.05x |
| `industry/leipzig-twain-count` | `Count` | 1,235.452 us | 1,322.681 us | 7,893.760 us | 0.93x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.096 us | 0.125 us | 0.77x |
| `common/split-words` | `Split` | 14.309 us | 20.427 us | 22.364 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.042 us | 0.071 us | 0.64x |
| `common/matches-words` | `Count` | 9.168 us | 14.960 us | 17.525 us | 0.61x |
| `industry/mariomka-ip-count` | `Count` | 2,070.041 us | 3,408.448 us | 10,659.567 us | 0.61x |
| `common/matches-word` | `Count` | 1.339 us | 2.303 us | 3.424 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 1,587.106 us | 2,929.831 us | 7,751.800 us | 0.54x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.192 us | 0.229 us | 0.45x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 170.834 us | 581.176 us | 825.052 us | 0.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.052 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,771.137 us | 8,552.233 us | 9,079.067 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 453.970 us | 3,859.860 us | 4,197.083 us | 0.12x |
| `common/matches-set` | `Count` | 6.927 us | 103.154 us | 105.346 us | 0.07x |
| `common/matches-boundary` | `Count` | 6.826 us | 103.215 us | 104.887 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 232.475 us | 4,673.775 us | 5,082.025 us | 0.05x |
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
| `lokad/imports/module-imports` | 8.962 us | 6.188 us | 30.909 us | 1.45x |
| `structural/keyword-to-generic-type` | 4.951 us | 3.967 us | 18.097 us | 1.25x |
| `lokad/langserv/color-short-hex` | 1.600 us | 1.316 us | 1.428 us | 1.22x |
| `lokad/lexer/doc-line` | 3.190 us | 3.015 us | 3.535 us | 1.06x |
| `lokad/lexer/identifier` | 22.951 us | 22.550 us | 25.402 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.205 us | 5.185 us | 19.329 us | 1.00x |
| `lokad/langserv/helper-identifier` | 6.970 us | 7.054 us | 7.477 us | 0.99x |
| `fallback/lookahead` | 5.280 us | 5.875 us | 19.590 us | 0.90x |
| `lokad/langserv/identifier-validator` | 10.630 us | 12.153 us | 13.206 us | 0.87x |
| `literal-family/method-token-family` | 7.302 us | 8.834 us | 21.903 us | 0.83x |
| `lokad/lexer/operator-run` | 2.738 us | 3.341 us | 3.686 us | 0.82x |
| `lokad/style/hex-color` | 0.553 us | 0.774 us | 0.821 us | 0.72x |
| `lokad/langserv/color-rgb-hex` | 1.927 us | 2.887 us | 2.933 us | 0.67x |
| `lokad/docs/inline-doc-prefix` | 3.189 us | 4.790 us | 5.220 us | 0.67x |
| `literal/identifier-token` | 2.689 us | 4.209 us | 17.452 us | 0.64x |
| `lokad/lexer/number` | 8.504 us | 14.035 us | 14.598 us | 0.61x |
| `literal/call-token` | 2.507 us | 4.153 us | 17.654 us | 0.60x |
| `structural/method-family-generic-call` | 3.373 us | 6.656 us | 20.292 us | 0.51x |
| `structural/ordered-identifier-window` | 14.231 us | 28.093 us | 41.740 us | 0.51x |
| `lokad/style/cell-ref` | 1.373 us | 2.799 us | 2.794 us | 0.49x |
| `structural/method-family-call` | 3.651 us | 7.884 us | 20.790 us | 0.46x |
| `literal-family/type-token-family` | 10.249 us | 23.347 us | 37.503 us | 0.44x |
| `literal/identifier-token-casei` | 5.689 us | 14.714 us | 28.791 us | 0.39x |
| `lokad/style/range-ref` | 0.851 us | 2.663 us | 2.615 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.251 us | 16.747 us | 30.516 us | 0.31x |
| `structural/ordered-keyword-window` | 5.222 us | 17.382 us | 31.131 us | 0.30x |
| `structural/modifier-family-to-type-window` | 66.881 us | 251.605 us | 269.625 us | 0.27x |
| `lokad/lexer/string` | 20.847 us | 92.868 us | 98.021 us | 0.22x |
| `lokad/folding/region-marker` | 5.808 us | 34.722 us | 62.257 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.360 us | 23.650 us | 24.074 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.009 us | 210.397 us | 227.947 us | 0.05x |
| `lokad/langserv/url-download` | 2.450 us | 53.940 us | 53.435 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.834 us | 134.647 us | 149.543 us | 0.04x |
| `balancing/begin-end-blocks` | 5.539 us | 4,428.947 us | 4,391.735 us | 0.0013x |
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
| `lokad/imports/module-imports` | 8.986 us | 4.263 us | 27.838 us | 2.11x |
| `lokad/langserv/color-short-hex` | 1.605 us | 0.942 us | 1.105 us | 1.70x |
| `lokad/langserv/helper-identifier` | 6.958 us | 4.504 us | 5.413 us | 1.55x |
| `lokad/lexer/doc-line` | 3.184 us | 2.129 us | 2.845 us | 1.50x |
| `structural/keyword-to-generic-type` | 4.933 us | 3.363 us | 17.063 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.060 us | 7.614 us | 20.854 us | 1.45x |
| `lokad/lexer/operator-run` | 2.741 us | 1.889 us | 2.452 us | 1.45x |
| `lokad/lexer/identifier` | 22.771 us | 16.536 us | 20.174 us | 1.38x |
| `structural/ordered-identifier-window` | 14.252 us | 10.408 us | 24.199 us | 1.37x |
| `fallback/lookahead` | 5.287 us | 4.082 us | 17.311 us | 1.30x |
| `structural/ordered-keyword-pair` | 5.214 us | 4.043 us | 17.483 us | 1.29x |
| `lokad/langserv/identifier-validator` | 10.572 us | 8.616 us | 10.506 us | 1.23x |
| `lokad/docs/inline-doc-prefix` | 3.174 us | 2.618 us | 3.388 us | 1.21x |
| `structural/modifier-family-to-type-window` | 66.536 us | 55.539 us | 71.271 us | 1.20x |
| `lokad/lexer/number` | 8.244 us | 7.089 us | 8.138 us | 1.16x |
| `lokad/style/hex-color` | 0.551 us | 0.480 us | 0.564 us | 1.15x |
| `lokad/langserv/color-rgb-hex` | 1.903 us | 1.790 us | 1.986 us | 1.06x |
| `literal/identifier-token-casei` | 5.726 us | 5.772 us | 19.492 us | 0.99x |
| `literal-family/type-token-family` | 10.260 us | 11.185 us | 24.496 us | 0.92x |
| `lokad/style/cell-ref` | 1.372 us | 1.732 us | 1.840 us | 0.79x |
| `structural/method-family-generic-call` | 3.379 us | 4.430 us | 17.285 us | 0.76x |
| `structural/method-family-call` | 3.681 us | 4.886 us | 17.344 us | 0.75x |
| `literal-family/method-token-family` | 3.746 us | 5.137 us | 17.800 us | 0.73x |
| `literal/call-token` | 2.504 us | 3.874 us | 16.475 us | 0.65x |
| `structural/ordered-keyword-window` | 5.272 us | 8.303 us | 22.110 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.258 us | 8.506 us | 22.010 us | 0.62x |
| `lokad/style/range-ref` | 0.851 us | 1.439 us | 1.531 us | 0.59x |
| `literal/identifier-token` | 2.691 us | 4.579 us | 16.764 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 4.849 us | 10.291 us | 23.904 us | 0.47x |
| `lokad/lexer/string` | 20.904 us | 44.445 us | 47.315 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.375 us | 7.265 us | 7.790 us | 0.33x |
| `lokad/langserv/url-download` | 2.425 us | 12.463 us | 12.929 us | 0.19x |
| `lokad/folding/region-marker` | 5.894 us | 35.568 us | 60.515 us | 0.17x |
| `balancing/begin-end-blocks` | 5.572 us | 453.537 us | 485.721 us | 0.01x |
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
