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
and resource checks completed in **45m47s**. The [run receipt](bench/Experiments/DeliveryDev87-20261114/full-refresh.json)
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
| `literal/sherlock-zh` | 68.446 us | 18.316 us | 2,153.247 us | 3.74x |
| `literal/sherlock-ru` | 157.742 us | 94.313 us | 5,105.840 us | 1.67x |
| `literal-alternate/sherlock-zh` | 89.767 us | 67.209 us | 2,194.685 us | 1.34x |
| `literal/sherlock-casei-en` | 147.130 us | 181.441 us | 540.735 us | 0.81x |
| `literal/sherlock-en` | 63.042 us | 84.093 us | 437.555 us | 0.75x |
| `literal/sherlock-casei-ru` | 232.918 us | 357.828 us | 5,445.743 us | 0.65x |
| `ruff-noqa/tweaked` | 4,518.341 us | 7,633.382 us | 43,291.700 us | 0.59x |
| `aws-keys/quick` | 4,037.879 us | 9,327.812 us | 45,132.100 us | 0.43x |
| `bounded-repeat/letters-en` | 1,087.670 us | 2,772.900 us | 2,898.856 us | 0.39x |
| `bounded-repeat/letters-ru` | 1,978.187 us | 5,724.507 us | 6,530.300 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,568.853 us | 4,716.324 us | 9,892.575 us | 0.33x |
| `literal-alternate/sherlock-en-mixed` | 187.817 us | 585.378 us | 994.977 us | 0.32x |
| `dictionary/single` | 7,415.670 us | 25,031.175 us | 25,058.600 us | 0.30x |
| `literal-alternate/sherlock-en-nomatch` | 164.469 us | 653.705 us | 1,065.090 us | 0.25x |
| `literal-alternate/sherlock-en` | 373.547 us | 1,490.462 us | 2,035.570 us | 0.25x |
| `bounded-repeat/capitals` | 9,246.000 us | 44,887.850 us | 48,270.650 us | 0.21x |
| `bounded-repeat/context` | 22,130.400 us | 143,525.100 us | 149,335.100 us | 0.15x |
| `aws-keys/full` | 10,618.775 us | 82,813.900 us | 111,939.700 us | 0.13x |
| `ruff-noqa/real` | 4,568.135 us | 72,194.950 us | 100,533.000 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 969.999 us | 23,354.275 us | 28,216.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 165.516 us | 10,932.186 us | 11,693.543 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 939.270 us | 496.982 us | 8,741.375 us | 1.89x |
| `common/match-word` | `Match` | 0.190 us | 0.165 us | 1.088 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,285.646 us | 1,289.162 us | 8,387.011 us | 1.00x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.105 us | 0.139 us | 0.74x |
| `common/uri-miss` | `IsMatch` | 0.123 us | 0.192 us | 0.211 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,138.475 us | 1,832.671 us | 10,252.267 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.083 us | 0.099 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.165 us | 0.182 us | 0.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 112.128 us | 248.614 us | 494.464 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.171 us | 0.190 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.184 us | 0.203 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.095 us | 0.231 us | 0.264 us | 0.41x |
| `common/replace-words` | `Replace` | 26.974 us | 73.402 us | 75.730 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.264 us | 0.287 us | 0.35x |
| `common/matches-word` | `Count` | 1.316 us | 4.135 us | 5.832 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.096 us | 0.340 us | 0.360 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.105 us | 0.132 us | 0.25x |
| `industry/mariomka-ip-count` | `Count` | 2,099.536 us | 8,415.978 us | 16,891.333 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.096 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 180.095 us | 953.179 us | 1,250.014 us | 0.19x |
| `common/split-words` | `Split` | 14.241 us | 75.415 us | 77.194 us | 0.19x |
| `industry/leipzig-name-family-count` | `Count` | 1,654.048 us | 9,454.175 us | 16,387.167 us | 0.17x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.548 us | 0.581 us | 0.17x |
| `common/matches-words` | `Count` | 8.988 us | 67.863 us | 69.346 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.075 us | 0.088 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.603 us | 0.631 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.810 us | 0.855 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,736.332 us | 19,025.550 us | 19,798.950 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,616.291 us | 23,785.650 us | 30,580.900 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,122.244 us | 31,553.233 us | 38,706.250 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 465.779 us | 8,223.810 us | 8,638.733 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.673 us | 160.174 us | 162.320 us | 0.05x |
| `common/matches-set` | `Count` | 7.356 us | 188.784 us | 189.959 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 273.235 us | 11,631.229 us | 12,286.943 us | 0.02x |
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
| `literal/sherlock-zh` | 68.509 us | 23.261 us | 2,150.080 us | 2.95x |
| `literal-alternate/sherlock-zh` | 89.870 us | 34.944 us | 2,120.769 us | 2.57x |
| `literal/sherlock-casei-en` | 146.762 us | 75.733 us | 459.912 us | 1.94x |
| `literal/sherlock-ru` | 156.536 us | 85.598 us | 5,033.907 us | 1.83x |
| `literal/sherlock-casei-ru` | 232.857 us | 192.293 us | 5,269.300 us | 1.21x |
| `literal-alternate/sherlock-casei-en` | 164.662 us | 142.408 us | 609.659 us | 1.16x |
| `literal-alternate/sherlock-en-mixed` | 190.261 us | 169.898 us | 552.295 us | 1.12x |
| `aws-keys/quick` | 4,054.132 us | 3,643.395 us | 36,062.000 us | 1.11x |
| `literal-alternate/sherlock-en` | 372.480 us | 358.806 us | 744.828 us | 1.04x |
| `ruff-noqa/tweaked` | 4,522.406 us | 4,416.806 us | 37,154.300 us | 1.02x |
| `bounded-repeat/letters-ru` | 1,979.508 us | 2,006.661 us | 2,805.052 us | 0.99x |
| `literal/sherlock-en` | 63.180 us | 73.883 us | 421.232 us | 0.86x |
| `literal-alternate/sherlock-en-nomatch` | 164.529 us | 196.694 us | 614.345 us | 0.84x |
| `bounded-repeat/capitals` | 9,251.562 us | 13,059.900 us | 16,003.800 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,588.961 us | 2,297.309 us | 7,423.533 us | 0.69x |
| `bounded-repeat/letters-en` | 1,088.240 us | 1,772.614 us | 1,888.692 us | 0.61x |
| `aws-keys/full` | 9,276.288 us | 17,902.460 us | 48,992.633 us | 0.52x |
| `literal-alternate/sherlock-casei-ru` | 969.705 us | 1,952.026 us | 6,976.600 us | 0.50x |
| `bounded-repeat/context` | 21,739.700 us | 67,274.000 us | 70,100.100 us | 0.32x |
| `ruff-noqa/real` | 4,561.824 us | 20,217.150 us | 51,696.000 us | 0.23x |
| `dictionary/single` | 7,410.427 us | 44,483.050 us | 44,863.050 us | 0.17x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.190 us | 0.073 us | 0.975 us | 2.59x |
| `common/backtracking` | `IsMatch` | 0.086 us | 0.041 us | 0.073 us | 2.07x |
| `industry/mariomka-email-count` | `Count` | 940.570 us | 466.722 us | 7,571.400 us | 2.02x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 111.671 us | 56.832 us | 257.170 us | 1.96x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.064 us | 0.084 us | 1.87x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.030 us | 0.046 us | 1.80x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.030 us | 0.048 us | 1.79x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.094 us | 0.054 us | 0.076 us | 1.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.075 us | 0.047 us | 0.082 us | 1.61x |
| `common/replace-words` | `Replace` | 26.940 us | 17.025 us | 19.340 us | 1.58x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.047 us | 0.067 us | 1.52x |
| `industry/leipzig-river-window-count` | `Count` | 2,145.497 us | 1,500.157 us | 7,461.680 us | 1.43x |
| `common/ip-miss` | `IsMatch` | 0.072 us | 0.051 us | 0.070 us | 1.41x |
| `industry/leipzig-symbol-count` | `Count` | 1,624.285 us | 1,248.810 us | 7,026.350 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.080 us | 0.107 us | 1.17x |
| `common/uri-match` | `IsMatch` | 0.095 us | 0.086 us | 0.119 us | 1.11x |
| `common/email-miss` | `IsMatch` | 0.096 us | 0.088 us | 0.113 us | 1.09x |
| `industry/mariomka-uri-count` | `Count` | 1,137.833 us | 1,077.852 us | 8,572.025 us | 1.06x |
| `industry/leipzig-twain-count` | `Count` | 1,282.462 us | 1,288.568 us | 7,403.273 us | 1.00x |
| `common/one-node-backtracking` | `IsMatch` | 0.073 us | 0.099 us | 0.126 us | 0.74x |
| `common/split-words` | `Split` | 14.282 us | 20.407 us | 22.400 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.042 us | 0.069 us | 0.63x |
| `industry/mariomka-ip-count` | `Count` | 2,121.350 us | 3,404.852 us | 10,816.033 us | 0.62x |
| `common/matches-words` | `Count` | 8.988 us | 14.803 us | 17.182 us | 0.61x |
| `common/matches-word` | `Count` | 1.315 us | 2.287 us | 3.316 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,668.528 us | 3,006.477 us | 8,144.975 us | 0.55x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.195 us | 0.231 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.050 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.501 us | 588.505 us | 838.505 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,748.055 us | 8,610.578 us | 9,157.822 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 463.557 us | 3,938.011 us | 4,280.633 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.688 us | 104.037 us | 106.571 us | 0.07x |
| `common/matches-set` | `Count` | 7.401 us | 102.614 us | 104.412 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 271.765 us | 4,658.700 us | 5,050.800 us | 0.06x |
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
| `lokad/imports/module-imports` | 10.480 us | 6.304 us | 31.406 us | 1.66x |
| `structural/keyword-to-generic-type` | 5.373 us | 4.009 us | 18.295 us | 1.34x |
| `lokad/langserv/color-short-hex` | 1.598 us | 1.283 us | 1.388 us | 1.25x |
| `lokad/lexer/doc-line` | 3.129 us | 2.908 us | 3.483 us | 1.08x |
| `lokad/lexer/identifier` | 22.624 us | 21.588 us | 24.451 us | 1.05x |
| `structural/ordered-keyword-pair` | 5.384 us | 5.321 us | 19.744 us | 1.01x |
| `lokad/langserv/helper-identifier` | 6.980 us | 7.039 us | 7.514 us | 0.99x |
| `fallback/lookahead` | 5.255 us | 5.797 us | 19.273 us | 0.91x |
| `lokad/langserv/identifier-validator` | 10.930 us | 12.072 us | 13.380 us | 0.91x |
| `lokad/lexer/operator-run` | 2.744 us | 3.381 us | 3.710 us | 0.81x |
| `literal-family/method-token-family` | 6.954 us | 9.034 us | 21.982 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 1.977 us | 2.884 us | 2.866 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.194 us | 4.725 us | 5.153 us | 0.68x |
| `lokad/style/hex-color` | 0.556 us | 0.825 us | 0.855 us | 0.67x |
| `literal/identifier-token` | 2.748 us | 4.252 us | 17.564 us | 0.65x |
| `literal/call-token` | 2.519 us | 4.241 us | 17.517 us | 0.59x |
| `lokad/lexer/number` | 7.853 us | 14.216 us | 14.799 us | 0.55x |
| `lokad/style/cell-ref` | 1.456 us | 2.769 us | 2.788 us | 0.53x |
| `structural/method-family-generic-call` | 3.302 us | 6.333 us | 19.746 us | 0.52x |
| `structural/ordered-identifier-window` | 14.037 us | 27.676 us | 41.340 us | 0.51x |
| `structural/method-family-call` | 3.780 us | 8.037 us | 21.219 us | 0.47x |
| `literal-family/type-token-family` | 10.261 us | 23.559 us | 38.388 us | 0.44x |
| `literal/identifier-token-casei` | 5.612 us | 14.780 us | 29.065 us | 0.38x |
| `structural/ordered-modifier-pair` | 5.609 us | 16.834 us | 30.112 us | 0.33x |
| `lokad/style/range-ref` | 0.855 us | 2.683 us | 2.770 us | 0.32x |
| `structural/ordered-keyword-window` | 5.187 us | 17.385 us | 30.789 us | 0.30x |
| `structural/modifier-family-to-type-window` | 65.668 us | 247.967 us | 265.135 us | 0.26x |
| `lokad/lexer/string` | 20.648 us | 92.152 us | 96.587 us | 0.22x |
| `lokad/folding/region-marker` | 6.263 us | 36.955 us | 63.364 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.354 us | 24.033 us | 24.435 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.112 us | 213.693 us | 227.407 us | 0.05x |
| `lokad/langserv/url-download` | 2.454 us | 49.734 us | 50.346 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.761 us | 134.130 us | 149.780 us | 0.04x |
| `balancing/begin-end-blocks` | 5.578 us | 4,302.850 us | 4,307.539 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.462 us | 4.322 us | 28.656 us | 2.42x |
| `lokad/langserv/color-short-hex` | 1.598 us | 0.920 us | 1.129 us | 1.74x |
| `structural/keyword-to-generic-type` | 5.394 us | 3.372 us | 17.215 us | 1.60x |
| `lokad/langserv/helper-identifier` | 6.946 us | 4.517 us | 5.461 us | 1.54x |
| `lokad/lexer/doc-line` | 3.153 us | 2.118 us | 2.765 us | 1.49x |
| `lokad/lexer/operator-run` | 2.739 us | 1.886 us | 2.444 us | 1.45x |
| `structural/keyword-family-to-capitalized-identifier` | 11.122 us | 7.693 us | 20.634 us | 1.45x |
| `lokad/lexer/identifier` | 22.562 us | 15.773 us | 19.328 us | 1.43x |
| `structural/ordered-identifier-window` | 14.027 us | 10.205 us | 23.645 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.397 us | 4.023 us | 17.729 us | 1.34x |
| `fallback/lookahead` | 5.242 us | 4.062 us | 16.961 us | 1.29x |
| `lokad/langserv/identifier-validator` | 10.851 us | 8.539 us | 10.727 us | 1.27x |
| `lokad/docs/inline-doc-prefix` | 3.208 us | 2.626 us | 3.326 us | 1.22x |
| `structural/modifier-family-to-type-window` | 65.810 us | 55.639 us | 70.770 us | 1.18x |
| `lokad/style/hex-color` | 0.558 us | 0.479 us | 0.569 us | 1.17x |
| `lokad/langserv/color-rgb-hex` | 1.952 us | 1.744 us | 1.961 us | 1.12x |
| `lokad/lexer/number` | 7.791 us | 7.142 us | 8.182 us | 1.09x |
| `literal/identifier-token-casei` | 5.594 us | 5.843 us | 19.572 us | 0.96x |
| `literal-family/type-token-family` | 10.204 us | 11.227 us | 24.540 us | 0.91x |
| `lokad/style/cell-ref` | 1.458 us | 1.605 us | 1.791 us | 0.91x |
| `structural/method-family-call` | 3.780 us | 4.961 us | 17.777 us | 0.76x |
| `structural/method-family-generic-call` | 3.288 us | 4.326 us | 16.826 us | 0.76x |
| `literal-family/method-token-family` | 3.789 us | 5.223 us | 17.915 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.612 us | 8.454 us | 22.057 us | 0.66x |
| `literal/call-token` | 2.535 us | 3.863 us | 16.527 us | 0.66x |
| `structural/ordered-keyword-window` | 5.174 us | 8.333 us | 21.468 us | 0.62x |
| `literal/identifier-token` | 2.745 us | 4.599 us | 17.043 us | 0.60x |
| `lokad/style/range-ref` | 0.865 us | 1.455 us | 1.506 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 4.796 us | 10.298 us | 23.751 us | 0.47x |
| `lokad/lexer/string` | 20.387 us | 44.191 us | 46.341 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.351 us | 7.344 us | 8.001 us | 0.32x |
| `lokad/langserv/url-download` | 2.460 us | 12.810 us | 13.310 us | 0.19x |
| `lokad/folding/region-marker` | 6.277 us | 36.623 us | 60.730 us | 0.17x |
| `balancing/begin-end-blocks` | 5.599 us | 456.735 us | 485.481 us | 0.01x |
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
