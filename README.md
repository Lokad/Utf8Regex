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
and resource checks completed in **50m54s**. The [run receipt](bench/Experiments/DeliveryDev80-20261113/full-completion.json)
records the scope, timings and presentation repair; package and consumer qualification are separate.
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
| `literal/sherlock-zh` | 97.687 us | 18.928 us | 2,165.544 us | 5.16x |
| `literal-alternate/sherlock-zh` | 119.528 us | 69.533 us | 2,215.451 us | 1.72x |
| `literal/sherlock-ru` | 171.338 us | 103.128 us | 5,470.571 us | 1.66x |
| `literal/sherlock-casei-en` | 160.515 us | 188.019 us | 611.220 us | 0.85x |
| `literal/sherlock-en` | 57.374 us | 87.379 us | 459.520 us | 0.66x |
| `literal/sherlock-casei-ru` | 255.841 us | 392.434 us | 5,691.123 us | 0.65x |
| `ruff-noqa/tweaked` | 4,594.424 us | 7,769.109 us | 45,126.200 us | 0.59x |
| `aws-keys/quick` | 3,976.515 us | 9,784.938 us | 46,179.050 us | 0.41x |
| `bounded-repeat/letters-en` | 1,115.516 us | 2,821.593 us | 2,942.904 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,029.495 us | 5,889.100 us | 6,706.050 us | 0.34x |
| `literal-alternate/sherlock-ru` | 1,546.173 us | 4,785.931 us | 9,969.425 us | 0.32x |
| `dictionary/single` | 8,330.360 us | 25,790.933 us | 25,809.900 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 186.411 us | 617.548 us | 1,061.683 us | 0.30x |
| `literal-alternate/sherlock-en` | 383.760 us | 1,535.461 us | 2,064.757 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 155.565 us | 685.057 us | 1,133.628 us | 0.23x |
| `bounded-repeat/capitals` | 9,532.163 us | 45,944.050 us | 53,666.200 us | 0.21x |
| `bounded-repeat/context` | 22,010.350 us | 149,877.000 us | 153,726.700 us | 0.15x |
| `aws-keys/full` | 8,998.500 us | 87,151.400 us | 119,316.600 us | 0.10x |
| `ruff-noqa/real` | 4,594.147 us | 70,313.350 us | 86,532.800 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 948.379 us | 23,120.625 us | 28,408.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 134.373 us | 11,539.000 us | 12,415.314 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,037.621 us | 506.825 us | 9,040.375 us | 2.05x |
| `common/match-word` | `Match` | 0.195 us | 0.168 us | 1.178 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,193.119 us | 1,325.600 us | 8,560.267 us | 0.90x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.110 us | 0.146 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.124 us | 0.185 us | 0.203 us | 0.67x |
| `industry/mariomka-uri-count` | `Count` | 1,248.316 us | 1,933.162 us | 10,360.875 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.101 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.171 us | 0.187 us | 0.52x |
| `common/ip-match` | `IsMatch` | 0.084 us | 0.174 us | 0.192 us | 0.48x |
| `common/ip-miss` | `IsMatch` | 0.080 us | 0.190 us | 0.209 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.097 us | 0.233 us | 0.266 us | 0.42x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 109.367 us | 265.810 us | 509.925 us | 0.41x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.272 us | 0.299 us | 0.35x |
| `common/replace-words` | `Replace` | 27.297 us | 77.185 us | 79.278 us | 0.35x |
| `common/matches-word` | `Count` | 1.356 us | 4.247 us | 5.944 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.103 us | 0.348 us | 0.372 us | 0.30x |
| `industry/mariomka-ip-count` | `Count` | 2,141.528 us | 8,331.480 us | 16,690.433 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.109 us | 0.140 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.20x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.558 us | 0.587 us | 0.19x |
| `common/split-words` | `Split` | 14.581 us | 80.945 us | 82.290 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.969 us | 977.403 us | 1,265.143 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,676.163 us | 9,783.625 us | 16,696.900 us | 0.17x |
| `common/matches-words` | `Count` | 9.280 us | 70.330 us | 72.345 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.619 us | 0.654 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.079 us | 0.092 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.830 us | 0.870 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,809.505 us | 19,743.000 us | 20,329.750 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,522.576 us | 22,531.225 us | 29,671.250 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,122.514 us | 32,241.033 us | 39,545.000 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 493.232 us | 8,173.120 us | 8,699.756 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.147 us | 164.352 us | 165.806 us | 0.04x |
| `common/matches-set` | `Count` | 7.241 us | 198.518 us | 199.466 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 238.044 us | 11,824.629 us | 12,420.686 us | 0.02x |
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
| `literal/sherlock-zh` | 97.644 us | 24.043 us | 2,162.809 us | 4.06x |
| `literal-alternate/sherlock-zh` | 119.691 us | 36.346 us | 2,161.500 us | 3.29x |
| `literal/sherlock-casei-en` | 160.389 us | 79.363 us | 434.867 us | 2.02x |
| `literal/sherlock-ru` | 171.676 us | 92.387 us | 5,301.964 us | 1.86x |
| `literal/sherlock-casei-ru` | 254.102 us | 199.681 us | 5,503.464 us | 1.27x |
| `aws-keys/quick` | 3,991.689 us | 3,698.681 us | 38,439.300 us | 1.08x |
| `literal-alternate/sherlock-en-mixed` | 187.145 us | 177.387 us | 588.668 us | 1.06x |
| `literal-alternate/sherlock-en` | 383.577 us | 374.634 us | 799.124 us | 1.02x |
| `ruff-noqa/tweaked` | 4,609.747 us | 4,602.529 us | 40,131.300 us | 1.00x |
| `bounded-repeat/letters-ru` | 2,020.168 us | 2,066.146 us | 2,882.285 us | 0.98x |
| `literal-alternate/sherlock-casei-en` | 133.776 us | 148.223 us | 566.199 us | 0.90x |
| `literal/sherlock-en` | 57.355 us | 76.650 us | 446.478 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 155.546 us | 209.012 us | 639.993 us | 0.74x |
| `bounded-repeat/capitals` | 9,652.450 us | 13,671.667 us | 21,175.175 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,544.596 us | 2,359.906 us | 7,469.725 us | 0.65x |
| `bounded-repeat/letters-en` | 1,115.188 us | 1,839.369 us | 1,938.177 us | 0.61x |
| `aws-keys/full` | 10,393.350 us | 18,764.700 us | 51,241.700 us | 0.55x |
| `literal-alternate/sherlock-casei-ru` | 950.047 us | 1,966.855 us | 7,106.975 us | 0.48x |
| `bounded-repeat/context` | 22,098.100 us | 69,727.900 us | 73,095.050 us | 0.32x |
| `ruff-noqa/real` | 4,608.471 us | 21,222.925 us | 35,210.933 us | 0.22x |
| `dictionary/single` | 8,334.211 us | 46,082.600 us | 46,136.750 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.194 us | 0.080 us | 0.999 us | 2.44x |
| `industry/mariomka-email-count` | `Count` | 1,041.129 us | 472.928 us | 7,841.550 us | 2.20x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.043 us | 0.074 us | 2.06x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 108.762 us | 58.238 us | 279.689 us | 1.87x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.056 us | 0.030 us | 0.047 us | 1.84x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.068 us | 0.089 us | 1.79x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.031 us | 0.050 us | 1.77x |
| `common/ip-match` | `IsMatch` | 0.083 us | 0.049 us | 0.068 us | 1.70x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.048 us | 0.086 us | 1.63x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.056 us | 0.076 us | 1.60x |
| `common/replace-words` | `Replace` | 27.640 us | 17.362 us | 20.162 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.051 us | 0.070 us | 1.49x |
| `industry/leipzig-river-window-count` | `Count` | 2,129.578 us | 1,529.834 us | 7,863.425 us | 1.39x |
| `industry/leipzig-symbol-count` | `Count` | 1,529.218 us | 1,202.708 us | 6,803.950 us | 1.27x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.081 us | 0.109 us | 1.18x |
| `industry/mariomka-uri-count` | `Count` | 1,249.162 us | 1,119.144 us | 8,352.725 us | 1.12x |
| `common/email-miss` | `IsMatch` | 0.103 us | 0.092 us | 0.117 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.089 us | 0.121 us | 1.04x |
| `industry/leipzig-twain-count` | `Count` | 1,198.854 us | 1,347.383 us | 7,464.170 us | 0.89x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.097 us | 0.130 us | 0.77x |
| `common/split-words` | `Split` | 14.618 us | 20.835 us | 22.587 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.073 us | 0.65x |
| `common/matches-words` | `Count` | 9.318 us | 15.306 us | 17.769 us | 0.61x |
| `industry/mariomka-ip-count` | `Count` | 2,143.351 us | 3,525.682 us | 10,836.867 us | 0.61x |
| `common/matches-word` | `Count` | 1.349 us | 2.353 us | 3.462 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.110 us | 0.200 us | 0.235 us | 0.55x |
| `industry/leipzig-name-family-count` | `Count` | 1,682.915 us | 3,097.244 us | 8,152.025 us | 0.54x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.034 us | 0.052 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.965 us | 602.616 us | 858.398 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,816.988 us | 8,747.889 us | 9,312.925 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 495.693 us | 3,965.400 us | 4,334.378 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.160 us | 105.141 us | 107.075 us | 0.07x |
| `common/matches-set` | `Count` | 7.250 us | 106.885 us | 108.876 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.848 us | 4,790.194 us | 5,231.693 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.363 us | 6.427 us | 31.221 us | 1.46x |
| `lokad/langserv/color-short-hex` | 1.854 us | 1.368 us | 1.498 us | 1.36x |
| `structural/keyword-to-generic-type` | 5.020 us | 4.143 us | 18.667 us | 1.21x |
| `lokad/lexer/doc-line` | 3.250 us | 3.022 us | 3.634 us | 1.08x |
| `lokad/lexer/identifier` | 23.773 us | 23.681 us | 26.367 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.265 us | 7.273 us | 7.871 us | 1.00x |
| `structural/ordered-keyword-pair` | 5.319 us | 5.415 us | 19.838 us | 0.98x |
| `fallback/lookahead` | 5.567 us | 6.137 us | 19.916 us | 0.91x |
| `lokad/style/hex-color` | 0.729 us | 0.811 us | 0.882 us | 0.90x |
| `lokad/langserv/identifier-validator` | 11.250 us | 12.972 us | 14.631 us | 0.87x |
| `lokad/lexer/operator-run` | 2.854 us | 3.498 us | 3.882 us | 0.82x |
| `literal-family/method-token-family` | 7.219 us | 9.211 us | 22.389 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 2.060 us | 2.970 us | 3.023 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.345 us | 4.962 us | 5.545 us | 0.67x |
| `literal/identifier-token` | 2.780 us | 4.359 us | 17.873 us | 0.64x |
| `literal/call-token` | 2.631 us | 4.354 us | 18.073 us | 0.60x |
| `lokad/lexer/number` | 7.949 us | 14.393 us | 15.090 us | 0.55x |
| `structural/ordered-identifier-window` | 14.812 us | 28.992 us | 43.130 us | 0.51x |
| `structural/method-family-generic-call` | 3.457 us | 6.832 us | 20.516 us | 0.51x |
| `lokad/style/cell-ref` | 1.419 us | 2.961 us | 3.105 us | 0.48x |
| `structural/method-family-call` | 3.861 us | 8.122 us | 21.197 us | 0.48x |
| `literal-family/type-token-family` | 10.551 us | 23.948 us | 37.982 us | 0.44x |
| `literal/identifier-token-casei` | 5.787 us | 14.875 us | 29.666 us | 0.39x |
| `lokad/style/range-ref` | 0.882 us | 2.789 us | 2.841 us | 0.32x |
| `structural/ordered-keyword-window` | 5.523 us | 17.845 us | 31.610 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.330 us | 17.328 us | 30.696 us | 0.31x |
| `structural/modifier-family-to-type-window` | 69.488 us | 256.773 us | 269.425 us | 0.27x |
| `lokad/lexer/string` | 21.185 us | 95.503 us | 101.111 us | 0.22x |
| `lokad/folding/region-marker` | 6.047 us | 36.806 us | 63.321 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.413 us | 24.557 us | 25.023 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.420 us | 216.764 us | 231.083 us | 0.05x |
| `lokad/langserv/url-download` | 2.519 us | 54.160 us | 54.291 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.025 us | 136.412 us | 150.610 us | 0.04x |
| `balancing/begin-end-blocks` | 5.735 us | 4,369.944 us | 4,383.378 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.272 us | 4.436 us | 28.645 us | 2.09x |
| `lokad/langserv/color-short-hex` | 1.852 us | 1.010 us | 1.165 us | 1.83x |
| `lokad/langserv/helper-identifier` | 7.386 us | 4.760 us | 5.707 us | 1.55x |
| `structural/keyword-family-to-capitalized-identifier` | 11.403 us | 7.891 us | 21.289 us | 1.45x |
| `lokad/lexer/doc-line` | 3.240 us | 2.250 us | 2.936 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.016 us | 3.504 us | 17.514 us | 1.43x |
| `lokad/lexer/operator-run` | 2.851 us | 2.021 us | 2.595 us | 1.41x |
| `lokad/style/hex-color` | 0.728 us | 0.522 us | 0.619 us | 1.39x |
| `lokad/lexer/identifier` | 23.653 us | 17.034 us | 21.390 us | 1.39x |
| `structural/ordered-identifier-window` | 14.764 us | 10.654 us | 24.676 us | 1.39x |
| `fallback/lookahead` | 5.499 us | 4.255 us | 17.692 us | 1.29x |
| `structural/ordered-keyword-pair` | 5.305 us | 4.134 us | 17.773 us | 1.28x |
| `structural/modifier-family-to-type-window` | 69.299 us | 55.665 us | 71.161 us | 1.24x |
| `lokad/docs/inline-doc-prefix` | 3.331 us | 2.872 us | 3.572 us | 1.16x |
| `lokad/langserv/identifier-validator` | 11.187 us | 9.670 us | 11.390 us | 1.16x |
| `lokad/langserv/color-rgb-hex` | 2.063 us | 1.860 us | 2.144 us | 1.11x |
| `lokad/lexer/number` | 7.959 us | 7.217 us | 8.237 us | 1.10x |
| `literal/identifier-token-casei` | 5.783 us | 5.944 us | 19.748 us | 0.97x |
| `literal-family/type-token-family` | 10.561 us | 11.558 us | 24.849 us | 0.91x |
| `lokad/style/cell-ref` | 1.419 us | 1.830 us | 2.023 us | 0.78x |
| `structural/method-family-generic-call` | 3.457 us | 4.540 us | 17.566 us | 0.76x |
| `structural/method-family-call` | 3.875 us | 5.119 us | 17.876 us | 0.76x |
| `literal-family/method-token-family` | 3.895 us | 5.352 us | 18.259 us | 0.73x |
| `literal/call-token` | 2.621 us | 4.008 us | 17.078 us | 0.65x |
| `structural/ordered-keyword-window` | 5.477 us | 8.614 us | 22.258 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.351 us | 8.584 us | 21.978 us | 0.62x |
| `literal/identifier-token` | 2.787 us | 4.729 us | 17.227 us | 0.59x |
| `lokad/style/range-ref` | 0.880 us | 1.559 us | 1.694 us | 0.56x |
| `structural/modifier-family-to-type-pair` | 5.019 us | 10.667 us | 24.480 us | 0.47x |
| `lokad/lexer/string` | 21.293 us | 46.218 us | 50.386 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.432 us | 7.479 us | 8.161 us | 0.33x |
| `lokad/langserv/url-download` | 2.514 us | 13.105 us | 13.630 us | 0.19x |
| `lokad/folding/region-marker` | 6.045 us | 36.276 us | 62.078 us | 0.17x |
| `balancing/begin-end-blocks` | 5.701 us | 469.011 us | 498.649 us | 0.01x |
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
