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
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The tables were refreshed on **12 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **46m32s**. The [run receipt](bench/Experiments/DeliveryDev77-20261112/full-refresh.json)
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
| `literal/sherlock-zh` | 97.362 us | 18.942 us | 2,194.842 us | 5.14x |
| `literal-alternate/sherlock-zh` | 118.982 us | 69.234 us | 2,210.491 us | 1.72x |
| `literal/sherlock-ru` | 165.483 us | 101.539 us | 5,316.493 us | 1.63x |
| `literal/sherlock-casei-en` | 159.068 us | 185.804 us | 566.198 us | 0.86x |
| `literal/sherlock-casei-ru` | 241.627 us | 363.786 us | 5,535.029 us | 0.66x |
| `literal/sherlock-en` | 57.295 us | 86.278 us | 480.155 us | 0.66x |
| `ruff-noqa/tweaked` | 4,579.194 us | 7,641.945 us | 41,582.500 us | 0.60x |
| `bounded-repeat/letters-en` | 1,148.418 us | 2,839.952 us | 2,974.844 us | 0.40x |
| `aws-keys/quick` | 3,797.475 us | 9,616.438 us | 43,212.650 us | 0.39x |
| `bounded-repeat/letters-ru` | 1,978.861 us | 5,763.014 us | 6,594.575 us | 0.34x |
| `dictionary/single` | 8,297.680 us | 25,526.767 us | 25,336.567 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,517.400 us | 4,800.100 us | 9,809.400 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 183.025 us | 594.471 us | 1,024.210 us | 0.31x |
| `literal-alternate/sherlock-en` | 378.388 us | 1,468.633 us | 1,960.300 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 153.078 us | 652.615 us | 1,061.360 us | 0.23x |
| `bounded-repeat/capitals` | 9,226.462 us | 45,192.100 us | 49,389.600 us | 0.20x |
| `bounded-repeat/context` | 22,243.175 us | 146,350.700 us | 149,097.500 us | 0.15x |
| `aws-keys/full` | 8,987.663 us | 85,356.300 us | 116,873.200 us | 0.11x |
| `ruff-noqa/real` | 4,537.394 us | 69,316.700 us | 97,612.900 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 937.630 us | 22,835.450 us | 28,068.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 129.737 us | 11,257.414 us | 12,018.914 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,036.526 us | 504.163 us | 8,965.475 us | 2.06x |
| `common/match-word` | `Match` | 0.199 us | 0.172 us | 1.147 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,247.605 us | 1,366.334 us | 8,517.950 us | 0.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.084 us | 0.108 us | 0.141 us | 0.77x |
| `industry/mariomka-uri-count` | `Count` | 1,250.413 us | 1,861.364 us | 10,384.567 us | 0.67x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.182 us | 0.201 us | 0.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.083 us | 0.100 us | 0.61x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.166 us | 0.184 us | 0.53x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.174 us | 0.193 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.076 us | 0.183 us | 0.203 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.097 us | 0.237 us | 0.272 us | 0.41x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 101.377 us | 259.948 us | 510.322 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.265 us | 0.292 us | 0.36x |
| `common/replace-words` | `Replace` | 27.227 us | 77.455 us | 79.386 us | 0.35x |
| `common/matches-word` | `Count` | 1.324 us | 4.227 us | 5.865 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.347 us | 0.372 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,092.822 us | 8,273.000 us | 16,703.767 us | 0.25x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.107 us | 0.136 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.081 us | 0.095 us | 0.20x |
| `common/date-miss` | `IsMatch` | 0.108 us | 0.562 us | 0.595 us | 0.19x |
| `common/split-words` | `Split` | 14.515 us | 76.654 us | 77.832 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.451 us | 957.926 us | 1,248.283 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,674.093 us | 9,702.200 us | 16,728.033 us | 0.17x |
| `common/matches-words` | `Count` | 9.227 us | 69.992 us | 71.525 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.603 us | 0.638 us | 0.12x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.076 us | 0.090 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.889 us | 0.925 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,805.240 us | 19,291.775 us | 19,938.875 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,500.606 us | 22,227.050 us | 29,382.650 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,058.959 us | 31,671.067 us | 39,272.900 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 485.586 us | 8,269.690 us | 8,731.778 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.879 us | 167.106 us | 170.525 us | 0.04x |
| `common/matches-set` | `Count` | 7.060 us | 192.493 us | 195.050 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 236.293 us | 11,638.800 us | 12,299.350 us | 0.02x |
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
| `literal/sherlock-zh` | 97.913 us | 23.994 us | 2,194.992 us | 4.08x |
| `literal-alternate/sherlock-zh` | 119.048 us | 36.301 us | 2,176.231 us | 3.28x |
| `literal/sherlock-casei-en` | 158.259 us | 78.647 us | 421.282 us | 2.01x |
| `literal/sherlock-ru` | 165.940 us | 88.911 us | 5,243.600 us | 1.87x |
| `literal/sherlock-casei-ru` | 241.569 us | 197.580 us | 5,398.171 us | 1.22x |
| `literal-alternate/sherlock-en` | 375.830 us | 335.442 us | 748.168 us | 1.12x |
| `aws-keys/quick` | 3,857.885 us | 3,698.410 us | 35,053.050 us | 1.04x |
| `literal-alternate/sherlock-en-mixed` | 182.188 us | 177.253 us | 568.687 us | 1.03x |
| `ruff-noqa/tweaked` | 4,573.153 us | 4,587.541 us | 35,600.750 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,985.176 us | 2,029.124 us | 2,846.944 us | 0.98x |
| `literal-alternate/sherlock-casei-en` | 128.389 us | 142.038 us | 611.102 us | 0.90x |
| `literal/sherlock-en` | 57.085 us | 76.133 us | 415.251 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 152.418 us | 205.240 us | 600.783 us | 0.74x |
| `bounded-repeat/capitals` | 9,266.900 us | 13,317.450 us | 16,400.925 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,509.544 us | 2,302.164 us | 7,401.700 us | 0.66x |
| `bounded-repeat/letters-en` | 1,151.173 us | 1,840.383 us | 1,946.018 us | 0.63x |
| `aws-keys/full` | 10,461.038 us | 18,383.300 us | 52,501.400 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 937.055 us | 1,925.249 us | 7,102.975 us | 0.49x |
| `bounded-repeat/context` | 21,853.950 us | 68,110.000 us | 71,089.200 us | 0.32x |
| `ruff-noqa/real` | 4,546.059 us | 20,504.425 us | 52,127.500 us | 0.22x |
| `dictionary/single` | 8,286.111 us | 45,498.250 us | 45,406.950 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.198 us | 0.079 us | 1.006 us | 2.51x |
| `industry/mariomka-email-count` | `Count` | 1,043.242 us | 470.737 us | 7,710.850 us | 2.22x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.044 us | 0.074 us | 2.01x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.055 us | 0.029 us | 0.047 us | 1.87x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.067 us | 0.085 us | 1.78x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 101.236 us | 57.664 us | 272.544 us | 1.76x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.031 us | 0.048 us | 1.71x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.048 us | 0.085 us | 1.62x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.088 us | 0.055 us | 0.076 us | 1.61x |
| `common/replace-words` | `Replace` | 27.125 us | 17.477 us | 20.300 us | 1.55x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.047 us | 0.067 us | 1.49x |
| `common/ip-miss` | `IsMatch` | 0.072 us | 0.049 us | 0.069 us | 1.47x |
| `industry/leipzig-river-window-count` | `Count` | 2,064.614 us | 1,506.931 us | 7,802.600 us | 1.37x |
| `industry/leipzig-symbol-count` | `Count` | 1,512.664 us | 1,193.460 us | 7,062.940 us | 1.27x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.080 us | 0.107 us | 1.16x |
| `industry/mariomka-uri-count` | `Count` | 1,252.459 us | 1,116.129 us | 8,399.267 us | 1.12x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.089 us | 0.115 us | 1.09x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.090 us | 0.123 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,252.967 us | 1,327.544 us | 7,371.650 us | 0.94x |
| `common/one-node-backtracking` | `IsMatch` | 0.073 us | 0.096 us | 0.126 us | 0.77x |
| `common/split-words` | `Split` | 14.478 us | 20.578 us | 22.537 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.043 us | 0.073 us | 0.63x |
| `industry/mariomka-ip-count` | `Count` | 2,090.856 us | 3,433.591 us | 10,847.433 us | 0.61x |
| `common/matches-word` | `Count` | 1.332 us | 2.313 us | 3.388 us | 0.58x |
| `common/matches-words` | `Count` | 9.231 us | 16.566 us | 18.995 us | 0.56x |
| `industry/leipzig-name-family-count` | `Count` | 1,691.539 us | 3,050.092 us | 8,225.225 us | 0.55x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.197 us | 0.236 us | 0.55x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.462 us | 591.342 us | 856.936 us | 0.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.053 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,795.398 us | 8,795.200 us | 9,389.325 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 486.163 us | 3,912.790 us | 4,322.922 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.054 us | 103.496 us | 105.927 us | 0.07x |
| `common/matches-set` | `Count` | 7.107 us | 104.529 us | 105.565 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 234.721 us | 4,737.956 us | 5,182.773 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.411 us | 6.430 us | 31.703 us | 1.46x |
| `structural/keyword-to-generic-type` | 4.943 us | 4.105 us | 19.058 us | 1.20x |
| `lokad/langserv/color-short-hex` | 1.666 us | 1.391 us | 1.505 us | 1.20x |
| `lokad/lexer/doc-line` | 3.257 us | 3.092 us | 3.814 us | 1.05x |
| `lokad/lexer/identifier` | 23.877 us | 23.413 us | 26.584 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.378 us | 5.370 us | 20.141 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.346 us | 7.413 us | 7.942 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.044 us | 12.514 us | 14.295 us | 0.88x |
| `fallback/lookahead` | 5.307 us | 6.067 us | 19.718 us | 0.87x |
| `lokad/lexer/operator-run` | 2.815 us | 3.447 us | 3.905 us | 0.82x |
| `literal-family/method-token-family` | 7.203 us | 9.125 us | 22.388 us | 0.79x |
| `lokad/style/hex-color` | 0.572 us | 0.825 us | 0.872 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.011 us | 3.023 us | 3.077 us | 0.67x |
| `lokad/docs/inline-doc-prefix` | 3.286 us | 5.033 us | 5.704 us | 0.65x |
| `literal/identifier-token` | 2.751 us | 4.301 us | 17.578 us | 0.64x |
| `literal/call-token` | 2.547 us | 4.244 us | 17.790 us | 0.60x |
| `lokad/lexer/number` | 8.037 us | 14.509 us | 15.704 us | 0.55x |
| `structural/ordered-identifier-window` | 14.631 us | 28.514 us | 42.205 us | 0.51x |
| `structural/method-family-generic-call` | 3.449 us | 6.828 us | 20.431 us | 0.51x |
| `lokad/style/cell-ref` | 1.423 us | 2.906 us | 3.027 us | 0.49x |
| `structural/method-family-call` | 3.820 us | 8.018 us | 21.857 us | 0.48x |
| `literal-family/type-token-family` | 10.520 us | 23.083 us | 36.926 us | 0.46x |
| `literal/identifier-token-casei` | 6.092 us | 15.080 us | 29.140 us | 0.40x |
| `lokad/style/range-ref` | 0.881 us | 2.757 us | 2.747 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.264 us | 17.223 us | 30.973 us | 0.31x |
| `structural/ordered-keyword-window` | 5.477 us | 17.924 us | 31.694 us | 0.31x |
| `structural/modifier-family-to-type-window` | 69.401 us | 257.526 us | 273.155 us | 0.27x |
| `lokad/lexer/string` | 21.423 us | 96.924 us | 102.508 us | 0.22x |
| `lokad/folding/region-marker` | 6.181 us | 34.161 us | 64.049 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.375 us | 24.519 us | 25.020 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.340 us | 216.825 us | 231.559 us | 0.05x |
| `lokad/langserv/url-download` | 2.540 us | 51.280 us | 51.759 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.031 us | 129.771 us | 144.223 us | 0.04x |
| `balancing/begin-end-blocks` | 5.804 us | 4,285.400 us | 4,295.933 us | 0.0014x |
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
| `lokad/imports/module-imports` | 9.372 us | 4.453 us | 28.706 us | 2.10x |
| `lokad/langserv/color-short-hex` | 1.662 us | 0.999 us | 1.189 us | 1.66x |
| `lokad/langserv/helper-identifier` | 7.412 us | 4.769 us | 5.780 us | 1.55x |
| `structural/keyword-family-to-capitalized-identifier` | 11.359 us | 7.861 us | 21.102 us | 1.44x |
| `lokad/lexer/doc-line` | 3.259 us | 2.265 us | 3.077 us | 1.44x |
| `structural/keyword-to-generic-type` | 4.927 us | 3.534 us | 17.859 us | 1.39x |
| `lokad/lexer/identifier` | 23.671 us | 17.010 us | 20.906 us | 1.39x |
| `lokad/lexer/operator-run` | 2.813 us | 2.026 us | 2.618 us | 1.39x |
| `structural/ordered-identifier-window` | 14.613 us | 10.716 us | 24.290 us | 1.36x |
| `structural/ordered-keyword-pair` | 5.378 us | 4.169 us | 18.096 us | 1.29x |
| `fallback/lookahead` | 5.298 us | 4.212 us | 17.373 us | 1.26x |
| `structural/modifier-family-to-type-window` | 69.304 us | 57.077 us | 72.065 us | 1.21x |
| `lokad/langserv/identifier-validator` | 10.994 us | 9.241 us | 11.228 us | 1.19x |
| `lokad/docs/inline-doc-prefix` | 3.286 us | 2.880 us | 3.582 us | 1.14x |
| `lokad/style/hex-color` | 0.574 us | 0.512 us | 0.602 us | 1.12x |
| `lokad/lexer/number` | 8.038 us | 7.632 us | 8.642 us | 1.05x |
| `lokad/langserv/color-rgb-hex` | 2.005 us | 1.938 us | 2.135 us | 1.03x |
| `literal/identifier-token-casei` | 6.063 us | 5.930 us | 19.829 us | 1.02x |
| `literal-family/type-token-family` | 10.524 us | 11.437 us | 24.794 us | 0.92x |
| `lokad/style/cell-ref` | 1.422 us | 1.829 us | 2.030 us | 0.78x |
| `structural/method-family-generic-call` | 3.466 us | 4.489 us | 17.440 us | 0.77x |
| `structural/method-family-call` | 3.868 us | 5.060 us | 18.285 us | 0.76x |
| `literal-family/method-token-family` | 3.859 us | 5.303 us | 18.231 us | 0.73x |
| `literal/call-token` | 2.550 us | 3.915 us | 16.817 us | 0.65x |
| `structural/ordered-keyword-window` | 5.487 us | 8.644 us | 22.209 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.270 us | 8.670 us | 22.253 us | 0.61x |
| `literal/identifier-token` | 2.740 us | 4.638 us | 17.243 us | 0.59x |
| `lokad/style/range-ref` | 0.884 us | 1.544 us | 1.623 us | 0.57x |
| `structural/modifier-family-to-type-pair` | 5.039 us | 10.716 us | 24.573 us | 0.47x |
| `lokad/lexer/string` | 21.596 us | 46.867 us | 50.547 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.360 us | 7.672 us | 8.249 us | 0.31x |
| `lokad/langserv/url-download` | 2.525 us | 13.174 us | 13.692 us | 0.19x |
| `lokad/folding/region-marker` | 6.230 us | 34.055 us | 63.321 us | 0.18x |
| `balancing/begin-end-blocks` | 5.795 us | 470.408 us | 503.116 us | 0.01x |
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
