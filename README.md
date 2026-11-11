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

The tables were refreshed on **11 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **46m45s**. The [run receipt](bench/Experiments/DeliveryDev68-20261111/full-refresh.json)
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
| `literal/sherlock-zh` | 89.046 us | 19.008 us | 2,159.029 us | 4.68x |
| `literal/sherlock-ru` | 168.508 us | 98.230 us | 5,409.650 us | 1.72x |
| `literal-alternate/sherlock-zh` | 112.915 us | 69.625 us | 2,203.157 us | 1.62x |
| `literal/sherlock-casei-en` | 165.308 us | 197.076 us | 598.812 us | 0.84x |
| `literal/sherlock-en` | 57.173 us | 87.033 us | 488.590 us | 0.66x |
| `literal/sherlock-casei-ru` | 238.097 us | 369.237 us | 5,726.685 us | 0.64x |
| `ruff-noqa/tweaked` | 4,418.818 us | 8,012.370 us | 42,060.500 us | 0.55x |
| `bounded-repeat/letters-en` | 1,126.228 us | 2,829.515 us | 2,949.565 us | 0.40x |
| `aws-keys/quick` | 3,778.765 us | 9,550.860 us | 24,043.515 us | 0.40x |
| `bounded-repeat/letters-ru` | 1,994.039 us | 6,015.823 us | 6,867.036 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,535.214 us | 4,758.550 us | 10,003.275 us | 0.32x |
| `dictionary/single` | 8,276.880 us | 26,327.367 us | 26,362.567 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 187.047 us | 627.982 us | 1,087.464 us | 0.30x |
| `literal-alternate/sherlock-en` | 376.643 us | 1,497.738 us | 2,008.285 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 155.033 us | 651.247 us | 1,108.715 us | 0.24x |
| `bounded-repeat/capitals` | 9,435.300 us | 47,209.750 us | 50,582.250 us | 0.20x |
| `bounded-repeat/context` | 20,703.275 us | 149,002.500 us | 152,366.900 us | 0.14x |
| `aws-keys/full` | 10,609.622 us | 87,030.300 us | 119,357.100 us | 0.12x |
| `ruff-noqa/real` | 4,710.412 us | 70,532.150 us | 86,721.800 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 981.043 us | 23,132.750 us | 28,468.750 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 138.105 us | 11,172.400 us | 12,133.343 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,186.091 us | 490.602 us | 9,078.375 us | 2.42x |
| `common/match-word` | `Match` | 0.194 us | 0.167 us | 1.127 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,200.632 us | 1,335.729 us | 8,597.220 us | 0.90x |
| `industry/mariomka-uri-count` | `Count` | 1,471.835 us | 1,939.765 us | 10,605.800 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.113 us | 0.151 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.190 us | 0.209 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.085 us | 0.101 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.171 us | 0.188 us | 0.51x |
| `common/replace-words` | `Replace` | 36.379 us | 75.688 us | 77.545 us | 0.48x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 121.065 us | 262.369 us | 503.134 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.079 us | 0.175 us | 0.195 us | 0.45x |
| `common/uri-match` | `IsMatch` | 0.098 us | 0.231 us | 0.268 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.079 us | 0.192 us | 0.212 us | 0.41x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.274 us | 0.300 us | 0.35x |
| `common/matches-word` | `Count` | 1.354 us | 4.266 us | 6.004 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.348 us | 0.373 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,285.406 us | 8,454.611 us | 17,020.000 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.21x |
| `common/split-words` | `Split` | 14.612 us | 79.082 us | 80.579 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.169 us | 982.581 us | 1,296.168 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.562 us | 0.605 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,685.136 us | 9,743.788 us | 16,988.000 us | 0.17x |
| `common/matches-words` | `Count` | 9.341 us | 70.662 us | 72.272 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.613 us | 0.644 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.848 us | 0.895 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,998.887 us | 19,867.575 us | 20,405.700 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,871.217 us | 22,692.250 us | 30,178.400 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,231.288 us | 32,108.233 us | 39,602.700 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 484.282 us | 8,325.730 us | 8,840.044 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.824 us | 187.399 us | 190.048 us | 0.04x |
| `common/matches-set` | `Count` | 7.839 us | 205.367 us | 208.274 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 236.427 us | 12,034.729 us | 12,491.833 us | 0.02x |
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
| `literal/sherlock-zh` | 89.179 us | 24.158 us | 2,160.180 us | 3.69x |
| `literal-alternate/sherlock-zh` | 114.646 us | 36.216 us | 2,169.871 us | 3.17x |
| `literal/sherlock-casei-en` | 165.195 us | 79.430 us | 463.897 us | 2.08x |
| `literal/sherlock-ru` | 168.800 us | 89.151 us | 5,347.820 us | 1.89x |
| `literal/sherlock-casei-ru` | 237.522 us | 223.305 us | 5,517.471 us | 1.06x |
| `aws-keys/quick` | 3,805.295 us | 3,654.590 us | 18,077.410 us | 1.04x |
| `literal-alternate/sherlock-en-mixed` | 187.412 us | 180.753 us | 608.977 us | 1.04x |
| `literal-alternate/sherlock-en` | 375.826 us | 371.316 us | 788.937 us | 1.01x |
| `ruff-noqa/tweaked` | 4,462.294 us | 4,617.147 us | 35,936.050 us | 0.97x |
| `bounded-repeat/letters-ru` | 1,988.053 us | 2,078.305 us | 2,912.888 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 137.941 us | 146.258 us | 572.934 us | 0.94x |
| `literal-alternate/sherlock-en-nomatch` | 154.879 us | 203.498 us | 580.465 us | 0.76x |
| `literal/sherlock-en` | 57.205 us | 76.521 us | 429.354 us | 0.75x |
| `bounded-repeat/capitals` | 9,460.163 us | 13,664.700 us | 16,649.650 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,536.110 us | 2,367.934 us | 7,522.783 us | 0.65x |
| `bounded-repeat/letters-en` | 1,123.428 us | 1,853.568 us | 1,942.687 us | 0.61x |
| `aws-keys/full` | 9,444.700 us | 18,560.360 us | 52,558.967 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 979.997 us | 1,985.474 us | 7,145.050 us | 0.49x |
| `bounded-repeat/context` | 20,631.225 us | 70,657.600 us | 73,314.600 us | 0.29x |
| `ruff-noqa/real` | 4,728.841 us | 21,188.500 us | 48,546.833 us | 0.22x |
| `dictionary/single` | 8,274.330 us | 46,386.700 us | 46,288.100 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,188.948 us | 459.736 us | 7,718.150 us | 2.59x |
| `common/match-word` | `Match` | 0.193 us | 0.081 us | 1.013 us | 2.38x |
| `common/replace-words` | `Replace` | 36.352 us | 17.587 us | 20.004 us | 2.07x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 121.188 us | 59.000 us | 258.065 us | 2.05x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.044 us | 0.073 us | 1.99x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.055 us | 0.031 us | 0.048 us | 1.81x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.056 us | 0.032 us | 0.049 us | 1.78x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.069 us | 0.087 us | 1.73x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.056 us | 0.076 us | 1.55x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.049 us | 0.069 us | 1.51x |
| `industry/leipzig-symbol-count` | `Count` | 1,871.061 us | 1,244.906 us | 6,997.250 us | 1.50x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.051 us | 0.070 us | 1.47x |
| `industry/leipzig-river-window-count` | `Count` | 2,235.526 us | 1,588.235 us | 7,812.600 us | 1.41x |
| `industry/mariomka-uri-count` | `Count` | 1,467.746 us | 1,131.752 us | 8,549.350 us | 1.30x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.069 us | 0.101 us | 1.20x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.109 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.120 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.095 us | 0.092 us | 0.122 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,200.743 us | 1,309.393 us | 6,659.000 us | 0.92x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.094 us | 0.127 us | 0.81x |
| `common/split-words` | `Split` | 14.573 us | 21.127 us | 22.941 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.073 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,284.182 us | 3,520.791 us | 11,090.800 us | 0.65x |
| `common/matches-words` | `Count` | 9.341 us | 15.064 us | 17.782 us | 0.62x |
| `common/matches-word` | `Count` | 1.351 us | 2.374 us | 3.520 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,683.549 us | 3,042.600 us | 8,437.300 us | 0.55x |
| `common/date-miss` | `IsMatch` | 0.100 us | 0.201 us | 0.238 us | 0.50x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.361 us | 603.371 us | 861.078 us | 0.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.037 us | 0.053 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,994.571 us | 8,973.133 us | 9,565.087 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 486.724 us | 4,021.637 us | 4,393.128 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.798 us | 105.720 us | 107.615 us | 0.07x |
| `common/matches-set` | `Count` | 7.820 us | 106.366 us | 107.838 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 236.465 us | 4,833.606 us | 5,260.007 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.460 us | 6.472 us | 31.483 us | 1.46x |
| `structural/keyword-to-generic-type` | 5.207 us | 4.109 us | 18.540 us | 1.27x |
| `lokad/langserv/color-short-hex` | 1.655 us | 1.369 us | 1.493 us | 1.21x |
| `lokad/lexer/doc-line` | 3.437 us | 3.012 us | 3.627 us | 1.14x |
| `lokad/lexer/identifier` | 23.750 us | 22.610 us | 25.456 us | 1.05x |
| `structural/ordered-keyword-pair` | 5.329 us | 5.497 us | 19.620 us | 0.97x |
| `lokad/langserv/helper-identifier` | 6.891 us | 7.398 us | 7.768 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.091 us | 12.493 us | 14.055 us | 0.89x |
| `fallback/lookahead` | 5.315 us | 6.132 us | 19.455 us | 0.87x |
| `lokad/lexer/operator-run` | 2.866 us | 3.504 us | 3.886 us | 0.82x |
| `literal-family/method-token-family` | 7.148 us | 9.213 us | 22.371 us | 0.78x |
| `lokad/style/hex-color` | 0.579 us | 0.824 us | 0.889 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.018 us | 2.945 us | 2.970 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.277 us | 4.903 us | 5.436 us | 0.67x |
| `literal/identifier-token` | 2.798 us | 4.265 us | 17.742 us | 0.66x |
| `literal/call-token` | 2.672 us | 4.336 us | 17.731 us | 0.62x |
| `lokad/lexer/number` | 8.082 us | 14.571 us | 15.068 us | 0.55x |
| `structural/method-family-generic-call` | 3.492 us | 6.735 us | 20.127 us | 0.52x |
| `structural/ordered-identifier-window` | 14.900 us | 28.922 us | 43.457 us | 0.52x |
| `lokad/style/cell-ref` | 1.441 us | 2.882 us | 2.933 us | 0.50x |
| `structural/method-family-call` | 3.882 us | 8.176 us | 21.262 us | 0.47x |
| `literal-family/type-token-family` | 10.547 us | 24.134 us | 37.508 us | 0.44x |
| `literal/identifier-token-casei` | 5.879 us | 15.517 us | 29.841 us | 0.38x |
| `lokad/style/range-ref` | 0.880 us | 2.732 us | 2.788 us | 0.32x |
| `structural/ordered-keyword-window` | 5.523 us | 17.808 us | 31.705 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.296 us | 17.571 us | 30.773 us | 0.30x |
| `structural/modifier-family-to-type-window` | 71.177 us | 255.731 us | 270.627 us | 0.28x |
| `lokad/lexer/string` | 22.054 us | 96.770 us | 100.810 us | 0.23x |
| `lokad/folding/region-marker` | 6.119 us | 36.710 us | 62.563 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.407 us | 24.753 us | 25.004 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.493 us | 220.065 us | 233.142 us | 0.05x |
| `lokad/langserv/url-download` | 2.506 us | 52.416 us | 52.747 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.030 us | 136.220 us | 150.297 us | 0.04x |
| `balancing/begin-end-blocks` | 5.815 us | 4,351.528 us | 4,390.189 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.377 us | 4.439 us | 28.724 us | 2.11x |
| `lokad/langserv/color-short-hex` | 1.660 us | 1.009 us | 1.174 us | 1.65x |
| `lokad/lexer/doc-line` | 3.460 us | 2.164 us | 2.878 us | 1.60x |
| `lokad/langserv/helper-identifier` | 6.895 us | 4.556 us | 5.637 us | 1.51x |
| `structural/keyword-to-generic-type` | 5.210 us | 3.542 us | 17.326 us | 1.47x |
| `lokad/lexer/identifier` | 23.881 us | 16.236 us | 19.852 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.473 us | 7.905 us | 21.237 us | 1.45x |
| `lokad/lexer/operator-run` | 2.893 us | 1.999 us | 2.530 us | 1.45x |
| `structural/ordered-identifier-window` | 14.891 us | 10.607 us | 24.835 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.339 us | 4.165 us | 17.563 us | 1.28x |
| `structural/modifier-family-to-type-window` | 71.345 us | 56.097 us | 72.176 us | 1.27x |
| `fallback/lookahead` | 5.329 us | 4.233 us | 17.150 us | 1.26x |
| `lokad/langserv/identifier-validator` | 11.211 us | 9.128 us | 11.255 us | 1.23x |
| `lokad/docs/inline-doc-prefix` | 3.277 us | 2.742 us | 3.531 us | 1.20x |
| `lokad/lexer/number` | 8.035 us | 7.077 us | 8.258 us | 1.14x |
| `lokad/style/hex-color` | 0.587 us | 0.519 us | 0.593 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.009 us | 1.861 us | 2.041 us | 1.08x |
| `literal/identifier-token-casei` | 5.879 us | 5.946 us | 19.709 us | 0.99x |
| `literal-family/type-token-family` | 10.563 us | 11.559 us | 24.611 us | 0.91x |
| `lokad/style/cell-ref` | 1.449 us | 1.728 us | 1.912 us | 0.84x |
| `structural/method-family-generic-call` | 3.491 us | 4.550 us | 17.358 us | 0.77x |
| `structural/method-family-call` | 3.878 us | 5.104 us | 17.863 us | 0.76x |
| `literal-family/method-token-family` | 3.911 us | 5.342 us | 18.096 us | 0.73x |
| `literal/call-token` | 2.671 us | 3.988 us | 16.716 us | 0.67x |
| `structural/ordered-keyword-window` | 5.539 us | 8.655 us | 22.366 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.295 us | 8.664 us | 21.920 us | 0.61x |
| `lokad/style/range-ref` | 0.887 us | 1.482 us | 1.567 us | 0.60x |
| `literal/identifier-token` | 2.796 us | 4.729 us | 17.115 us | 0.59x |
| `lokad/lexer/string` | 21.936 us | 44.998 us | 47.638 us | 0.49x |
| `structural/modifier-family-to-type-pair` | 5.027 us | 10.735 us | 24.671 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.374 us | 7.615 us | 8.169 us | 0.31x |
| `lokad/langserv/url-download` | 2.507 us | 13.093 us | 13.590 us | 0.19x |
| `lokad/folding/region-marker` | 6.135 us | 36.835 us | 62.917 us | 0.17x |
| `balancing/begin-end-blocks` | 5.806 us | 470.588 us | 499.822 us | 0.01x |
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
