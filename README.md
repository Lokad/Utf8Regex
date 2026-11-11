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
and resource checks passed in **46m45s**. The [run receipt](bench/Experiments/DeliveryDev71-20261111/full-refresh.json)
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
| `literal/sherlock-zh` | 83.040 us | 18.998 us | 2,185.412 us | 4.37x |
| `literal/sherlock-ru` | 166.013 us | 97.723 us | 5,155.620 us | 1.70x |
| `literal-alternate/sherlock-zh` | 104.815 us | 69.130 us | 2,175.176 us | 1.52x |
| `literal/sherlock-casei-en` | 156.463 us | 203.874 us | 594.147 us | 0.77x |
| `literal/sherlock-casei-ru` | 243.184 us | 369.113 us | 5,660.362 us | 0.66x |
| `literal/sherlock-en` | 57.228 us | 88.516 us | 476.780 us | 0.65x |
| `ruff-noqa/tweaked` | 4,603.306 us | 7,727.536 us | 43,270.700 us | 0.60x |
| `bounded-repeat/letters-en` | 1,124.660 us | 2,844.185 us | 2,952.631 us | 0.40x |
| `aws-keys/quick` | 3,765.467 us | 9,898.150 us | 41,178.850 us | 0.38x |
| `bounded-repeat/letters-ru` | 1,988.400 us | 6,006.938 us | 6,798.208 us | 0.33x |
| `dictionary/single` | 8,288.200 us | 25,876.000 us | 25,912.933 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,549.982 us | 4,896.894 us | 10,110.600 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 198.264 us | 628.813 us | 1,068.080 us | 0.32x |
| `literal-alternate/sherlock-en-nomatch` | 164.046 us | 655.422 us | 1,083.982 us | 0.25x |
| `literal-alternate/sherlock-en` | 379.105 us | 1,540.776 us | 2,075.351 us | 0.25x |
| `bounded-repeat/capitals` | 9,495.013 us | 46,608.800 us | 53,296.850 us | 0.20x |
| `aws-keys/full` | 17,565.600 us | 87,839.800 us | 121,547.300 us | 0.20x |
| `bounded-repeat/context` | 22,108.800 us | 147,591.800 us | 151,232.800 us | 0.15x |
| `ruff-noqa/real` | 4,392.288 us | 70,388.200 us | 86,408.000 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 972.973 us | 23,296.925 us | 28,601.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 126.659 us | 11,599.700 us | 12,484.829 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,172.211 us | 489.311 us | 8,984.150 us | 2.40x |
| `common/match-word` | `Match` | 0.194 us | 0.172 us | 1.163 us | 1.13x |
| `industry/leipzig-twain-count` | `Count` | 1,178.011 us | 1,352.834 us | 8,707.120 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,456.540 us | 1,880.490 us | 10,265.425 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.111 us | 0.150 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.124 us | 0.205 us | 0.224 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.170 us | 0.189 us | 0.51x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 118.138 us | 265.623 us | 512.647 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.175 us | 0.195 us | 0.43x |
| `common/ip-miss` | `IsMatch` | 0.079 us | 0.188 us | 0.208 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.097 us | 0.234 us | 0.272 us | 0.42x |
| `common/replace-words` | `Replace` | 27.965 us | 77.625 us | 80.114 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.271 us | 0.299 us | 0.35x |
| `common/matches-word` | `Count` | 1.369 us | 4.266 us | 6.008 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.346 us | 0.373 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,295.803 us | 8,333.300 us | 16,864.433 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.138 us | 0.27x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.098 us | 0.20x |
| `common/split-words` | `Split` | 14.579 us | 79.157 us | 80.547 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.538 us | 980.582 us | 1,272.032 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.561 us | 0.592 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,626.870 us | 9,923.513 us | 16,961.133 us | 0.16x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.076 us | 0.091 us | 0.13x |
| `common/matches-words` | `Count` | 9.316 us | 71.072 us | 72.524 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.626 us | 0.656 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.842 us | 0.883 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,982.326 us | 19,648.625 us | 20,243.675 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,871.585 us | 22,954.250 us | 30,482.700 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,215.735 us | 32,990.733 us | 40,258.250 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 489.226 us | 8,367.289 us | 8,855.589 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.732 us | 165.461 us | 167.611 us | 0.05x |
| `common/matches-set` | `Count` | 7.841 us | 195.791 us | 197.100 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 258.416 us | 12,064.586 us | 12,626.333 us | 0.02x |
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
| `literal/sherlock-zh` | 83.063 us | 24.109 us | 2,198.294 us | 3.45x |
| `literal-alternate/sherlock-zh` | 104.948 us | 35.994 us | 2,127.519 us | 2.92x |
| `literal/sherlock-casei-en` | 156.786 us | 80.827 us | 436.800 us | 1.94x |
| `literal/sherlock-ru` | 166.157 us | 88.604 us | 5,239.113 us | 1.88x |
| `literal-alternate/sherlock-en-mixed` | 198.609 us | 179.663 us | 616.032 us | 1.11x |
| `literal/sherlock-casei-ru` | 240.531 us | 228.614 us | 5,441.586 us | 1.05x |
| `aws-keys/quick` | 3,803.530 us | 3,665.833 us | 36,042.067 us | 1.04x |
| `literal-alternate/sherlock-en` | 378.831 us | 376.657 us | 825.216 us | 1.01x |
| `ruff-noqa/tweaked` | 4,600.841 us | 4,588.429 us | 38,759.450 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,984.932 us | 2,076.146 us | 2,876.481 us | 0.96x |
| `aws-keys/full` | 17,516.987 us | 18,621.100 us | 51,240.633 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 127.648 us | 147.767 us | 566.856 us | 0.86x |
| `literal-alternate/sherlock-en-nomatch` | 164.389 us | 208.075 us | 622.096 us | 0.79x |
| `literal/sherlock-en` | 57.311 us | 77.370 us | 404.199 us | 0.74x |
| `bounded-repeat/capitals` | 9,501.900 us | 13,674.600 us | 21,004.675 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,548.216 us | 2,370.278 us | 7,519.217 us | 0.65x |
| `bounded-repeat/letters-en` | 1,124.124 us | 1,853.746 us | 1,935.505 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 952.248 us | 1,990.016 us | 7,215.875 us | 0.48x |
| `bounded-repeat/context` | 22,320.000 us | 69,737.650 us | 73,190.750 us | 0.32x |
| `ruff-noqa/real` | 4,436.806 us | 21,295.100 us | 47,685.967 us | 0.21x |
| `dictionary/single` | 8,295.380 us | 46,105.250 us | 46,163.450 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,178.503 us | 456.140 us | 7,756.500 us | 2.58x |
| `common/match-word` | `Match` | 0.194 us | 0.081 us | 1.006 us | 2.40x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.043 us | 0.074 us | 2.02x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 118.233 us | 60.903 us | 259.531 us | 1.94x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.066 us | 0.087 us | 1.84x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.056 us | 0.032 us | 0.050 us | 1.74x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.056 us | 0.077 us | 1.59x |
| `common/replace-words` | `Replace` | 28.047 us | 17.649 us | 18.991 us | 1.59x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.050 us | 0.090 us | 1.58x |
| `industry/leipzig-symbol-count` | `Count` | 1,871.485 us | 1,229.813 us | 7,263.720 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.049 us | 0.068 us | 1.50x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.052 us | 0.071 us | 1.43x |
| `industry/leipzig-river-window-count` | `Count` | 2,219.959 us | 1,579.983 us | 8,170.550 us | 1.41x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.040 us | 0.056 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,456.765 us | 1,120.385 us | 8,432.625 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.109 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.117 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.091 us | 0.122 us | 1.06x |
| `industry/leipzig-twain-count` | `Count` | 1,179.888 us | 1,304.538 us | 7,750.483 us | 0.90x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.090 us | 0.129 us | 0.83x |
| `common/split-words` | `Split` | 14.586 us | 21.272 us | 23.081 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.074 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,292.797 us | 3,529.591 us | 10,967.333 us | 0.65x |
| `common/matches-words` | `Count` | 9.321 us | 15.044 us | 17.556 us | 0.62x |
| `common/matches-word` | `Count` | 1.358 us | 2.369 us | 3.612 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,626.783 us | 3,018.920 us | 8,186.800 us | 0.54x |
| `common/date-miss` | `IsMatch` | 0.100 us | 0.201 us | 0.238 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.036 us | 0.052 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.894 us | 599.663 us | 861.062 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,984.329 us | 8,772.978 us | 9,363.562 us | 0.23x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 491.701 us | 4,030.926 us | 4,395.778 us | 0.12x |
| `common/matches-set` | `Count` | 7.843 us | 105.555 us | 107.142 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.754 us | 105.031 us | 106.999 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 259.600 us | 4,875.113 us | 5,327.247 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.594 us | 6.514 us | 31.524 us | 1.47x |
| `structural/keyword-to-generic-type` | 4.996 us | 4.123 us | 18.324 us | 1.21x |
| `lokad/langserv/color-short-hex` | 1.673 us | 1.384 us | 1.506 us | 1.21x |
| `lokad/lexer/doc-line` | 3.487 us | 3.074 us | 3.765 us | 1.13x |
| `lokad/lexer/identifier` | 23.821 us | 23.159 us | 26.611 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.409 us | 5.451 us | 19.496 us | 0.99x |
| `lokad/langserv/helper-identifier` | 6.844 us | 7.392 us | 8.020 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.151 us | 12.534 us | 14.543 us | 0.89x |
| `fallback/lookahead` | 5.318 us | 6.124 us | 19.507 us | 0.87x |
| `lokad/lexer/operator-run` | 2.891 us | 3.510 us | 3.977 us | 0.82x |
| `literal-family/method-token-family` | 7.135 us | 9.309 us | 22.524 us | 0.77x |
| `lokad/style/hex-color` | 0.583 us | 0.813 us | 0.884 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 3.331 us | 4.892 us | 5.674 us | 0.68x |
| `lokad/langserv/color-rgb-hex` | 2.014 us | 2.987 us | 3.062 us | 0.67x |
| `literal/identifier-token` | 2.797 us | 4.370 us | 17.676 us | 0.64x |
| `literal/call-token` | 2.642 us | 4.341 us | 17.839 us | 0.61x |
| `lokad/lexer/number` | 8.162 us | 14.476 us | 15.739 us | 0.56x |
| `structural/method-family-generic-call` | 3.535 us | 6.684 us | 20.575 us | 0.53x |
| `structural/ordered-identifier-window` | 14.863 us | 28.982 us | 42.406 us | 0.51x |
| `lokad/style/cell-ref` | 1.457 us | 3.030 us | 3.098 us | 0.48x |
| `structural/method-family-call` | 3.946 us | 8.217 us | 21.638 us | 0.48x |
| `literal-family/type-token-family` | 10.581 us | 25.019 us | 38.231 us | 0.42x |
| `literal/identifier-token-casei` | 5.897 us | 15.853 us | 29.150 us | 0.37x |
| `lokad/style/range-ref` | 0.896 us | 2.777 us | 2.815 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.439 us | 17.487 us | 31.026 us | 0.31x |
| `structural/ordered-keyword-window` | 5.486 us | 17.779 us | 31.872 us | 0.31x |
| `structural/modifier-family-to-type-window` | 71.625 us | 258.358 us | 273.109 us | 0.28x |
| `lokad/lexer/string` | 21.789 us | 96.624 us | 102.695 us | 0.23x |
| `lokad/folding/region-marker` | 6.056 us | 36.509 us | 62.650 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.440 us | 24.712 us | 25.141 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.510 us | 217.356 us | 231.150 us | 0.05x |
| `lokad/langserv/url-download` | 2.528 us | 51.178 us | 51.659 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.032 us | 140.745 us | 154.699 us | 0.04x |
| `balancing/begin-end-blocks` | 5.737 us | 4,352.483 us | 4,385.728 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.578 us | 4.474 us | 28.536 us | 2.14x |
| `lokad/langserv/color-short-hex` | 1.638 us | 1.026 us | 1.209 us | 1.60x |
| `lokad/lexer/doc-line` | 3.472 us | 2.286 us | 3.022 us | 1.52x |
| `structural/keyword-family-to-capitalized-identifier` | 11.490 us | 7.959 us | 21.050 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.002 us | 3.485 us | 17.057 us | 1.44x |
| `lokad/lexer/operator-run` | 2.882 us | 2.020 us | 2.643 us | 1.43x |
| `structural/ordered-identifier-window` | 14.861 us | 10.667 us | 24.310 us | 1.39x |
| `lokad/langserv/helper-identifier` | 6.877 us | 5.029 us | 5.783 us | 1.37x |
| `lokad/lexer/identifier` | 23.738 us | 17.537 us | 21.412 us | 1.35x |
| `structural/ordered-keyword-pair` | 5.406 us | 4.168 us | 17.502 us | 1.30x |
| `structural/modifier-family-to-type-window` | 71.698 us | 56.533 us | 72.282 us | 1.27x |
| `fallback/lookahead` | 5.322 us | 4.243 us | 17.245 us | 1.25x |
| `lokad/langserv/identifier-validator` | 11.173 us | 9.178 us | 11.289 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.336 us | 2.872 us | 3.829 us | 1.16x |
| `lokad/style/hex-color` | 0.585 us | 0.518 us | 0.602 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 2.020 us | 1.907 us | 2.133 us | 1.06x |
| `lokad/lexer/number` | 8.099 us | 7.664 us | 8.843 us | 1.06x |
| `literal/identifier-token-casei` | 5.877 us | 5.947 us | 19.613 us | 0.99x |
| `literal-family/type-token-family` | 10.600 us | 11.579 us | 24.750 us | 0.92x |
| `lokad/style/cell-ref` | 1.458 us | 1.904 us | 2.095 us | 0.77x |
| `structural/method-family-call` | 3.939 us | 5.175 us | 18.092 us | 0.76x |
| `structural/method-family-generic-call` | 3.535 us | 4.698 us | 17.670 us | 0.75x |
| `literal-family/method-token-family` | 3.892 us | 5.366 us | 18.291 us | 0.73x |
| `literal/call-token` | 2.645 us | 4.015 us | 16.821 us | 0.66x |
| `structural/ordered-keyword-window` | 5.464 us | 8.636 us | 22.313 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.444 us | 8.654 us | 22.095 us | 0.63x |
| `literal/identifier-token` | 2.801 us | 4.733 us | 17.076 us | 0.59x |
| `lokad/style/range-ref` | 0.894 us | 1.563 us | 1.648 us | 0.57x |
| `structural/modifier-family-to-type-pair` | 5.027 us | 10.707 us | 24.499 us | 0.47x |
| `lokad/lexer/string` | 21.827 us | 47.057 us | 50.534 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.390 us | 7.557 us | 8.103 us | 0.32x |
| `lokad/langserv/url-download` | 2.520 us | 13.150 us | 13.669 us | 0.19x |
| `lokad/folding/region-marker` | 6.064 us | 36.607 us | 62.917 us | 0.17x |
| `balancing/begin-end-blocks` | 5.751 us | 470.364 us | 501.639 us | 0.01x |
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
