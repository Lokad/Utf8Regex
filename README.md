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
and resource checks passed in **46m59s**. The [run receipt](bench/Experiments/DeliveryDev75-20261112/full-refresh.json)
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
| `literal/sherlock-zh` | 83.231 us | 19.254 us | 2,222.641 us | 4.32x |
| `literal/sherlock-ru` | 176.545 us | 98.283 us | 5,361.436 us | 1.80x |
| `literal-alternate/sherlock-zh` | 102.940 us | 69.199 us | 2,182.183 us | 1.49x |
| `literal/sherlock-casei-en` | 161.341 us | 194.910 us | 598.453 us | 0.83x |
| `literal/sherlock-en` | 57.146 us | 87.031 us | 453.416 us | 0.66x |
| `literal/sherlock-casei-ru` | 235.871 us | 369.738 us | 5,531.771 us | 0.64x |
| `ruff-noqa/tweaked` | 4,563.571 us | 7,672.409 us | 41,734.800 us | 0.59x |
| `bounded-repeat/letters-en` | 1,098.467 us | 2,799.337 us | 2,920.708 us | 0.39x |
| `aws-keys/quick` | 3,763.343 us | 9,703.100 us | 43,158.350 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,043.632 us | 5,908.869 us | 6,709.600 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 199.286 us | 599.235 us | 1,036.051 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,538.355 us | 4,796.325 us | 10,012.175 us | 0.32x |
| `dictionary/single` | 8,306.367 us | 26,370.433 us | 26,467.800 us | 0.31x |
| `literal-alternate/sherlock-en` | 379.742 us | 1,542.937 us | 2,048.738 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 153.938 us | 651.320 us | 1,080.291 us | 0.24x |
| `bounded-repeat/capitals` | 9,620.263 us | 47,189.550 us | 54,025.150 us | 0.20x |
| `bounded-repeat/context` | 22,129.900 us | 150,535.400 us | 154,255.300 us | 0.15x |
| `aws-keys/full` | 10,923.329 us | 87,377.200 us | 121,772.700 us | 0.13x |
| `ruff-noqa/real` | 4,580.406 us | 70,247.650 us | 103,924.600 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 979.514 us | 23,226.050 us | 28,298.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 133.277 us | 11,619.929 us | 12,493.386 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,177.567 us | 483.868 us | 8,955.400 us | 2.43x |
| `common/match-word` | `Match` | 0.194 us | 0.173 us | 1.135 us | 1.12x |
| `industry/leipzig-twain-count` | `Count` | 1,170.997 us | 1,331.368 us | 8,574.240 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,443.156 us | 1,928.330 us | 10,276.025 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.112 us | 0.148 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.201 us | 0.223 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.100 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.171 us | 0.188 us | 0.52x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.174 us | 0.193 us | 0.45x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 115.644 us | 265.601 us | 515.065 us | 0.44x |
| `common/uri-match` | `IsMatch` | 0.098 us | 0.232 us | 0.268 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.187 us | 0.207 us | 0.40x |
| `common/replace-words` | `Replace` | 27.551 us | 78.422 us | 79.508 us | 0.35x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.272 us | 0.298 us | 0.35x |
| `common/matches-word` | `Count` | 1.347 us | 4.219 us | 5.978 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.353 us | 0.377 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,283.832 us | 8,610.033 us | 16,953.367 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.108 us | 0.139 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.099 us | 0.21x |
| `common/split-words` | `Split` | 14.726 us | 78.670 us | 80.288 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.382 us | 969.682 us | 1,265.727 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,627.826 us | 10,017.337 us | 17,038.267 us | 0.16x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.567 us | 0.595 us | 0.15x |
| `common/matches-words` | `Count` | 9.341 us | 71.482 us | 73.198 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.111 us | 0.847 us | 0.890 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.616 us | 0.648 us | 0.12x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.090 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,807.936 us | 19,624.025 us | 20,133.450 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,873.171 us | 22,877.350 us | 30,236.250 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,153.069 us | 33,897.900 us | 41,354.050 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 481.658 us | 8,194.860 us | 8,722.944 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.057 us | 162.552 us | 164.131 us | 0.04x |
| `common/matches-set` | `Count` | 7.094 us | 193.497 us | 195.217 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 259.488 us | 12,018.000 us | 12,566.950 us | 0.02x |
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
| `literal/sherlock-zh` | 82.773 us | 24.377 us | 2,211.567 us | 3.40x |
| `literal-alternate/sherlock-zh` | 102.882 us | 36.472 us | 2,157.633 us | 2.82x |
| `literal/sherlock-casei-en` | 162.175 us | 82.057 us | 468.211 us | 1.98x |
| `literal/sherlock-ru` | 168.203 us | 89.936 us | 5,245.436 us | 1.87x |
| `literal-alternate/sherlock-en-mixed` | 199.541 us | 178.317 us | 551.579 us | 1.12x |
| `literal/sherlock-casei-ru` | 235.592 us | 223.281 us | 5,293.707 us | 1.06x |
| `aws-keys/quick` | 3,769.376 us | 3,663.476 us | 35,986.100 us | 1.03x |
| `literal-alternate/sherlock-en` | 378.833 us | 370.436 us | 792.870 us | 1.02x |
| `ruff-noqa/tweaked` | 4,605.424 us | 4,612.259 us | 36,682.850 us | 1.00x |
| `bounded-repeat/letters-ru` | 2,040.078 us | 2,067.295 us | 2,868.696 us | 0.99x |
| `literal-alternate/sherlock-casei-en` | 132.229 us | 146.390 us | 610.943 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 154.141 us | 200.843 us | 584.875 us | 0.77x |
| `literal/sherlock-en` | 57.287 us | 76.605 us | 430.641 us | 0.75x |
| `bounded-repeat/capitals` | 9,558.600 us | 13,697.833 us | 20,897.950 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,542.386 us | 2,360.491 us | 7,497.700 us | 0.65x |
| `bounded-repeat/letters-en` | 1,101.401 us | 1,843.233 us | 1,941.372 us | 0.60x |
| `aws-keys/full` | 11,015.987 us | 18,743.720 us | 54,487.500 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 981.356 us | 1,972.944 us | 7,084.275 us | 0.50x |
| `bounded-repeat/context` | 21,454.125 us | 70,094.750 us | 73,609.450 us | 0.31x |
| `ruff-noqa/real` | 4,625.059 us | 20,987.375 us | 56,168.233 us | 0.22x |
| `dictionary/single` | 8,310.370 us | 45,989.450 us | 46,043.100 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,177.581 us | 452.419 us | 7,700.350 us | 2.60x |
| `common/backtracking` | `IsMatch` | 0.111 us | 0.044 us | 0.074 us | 2.53x |
| `common/match-word` | `Match` | 0.194 us | 0.082 us | 0.999 us | 2.38x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 117.490 us | 59.341 us | 275.794 us | 1.98x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.054 us | 0.031 us | 0.048 us | 1.78x |
| `common/uri-miss` | `IsMatch` | 0.120 us | 0.068 us | 0.088 us | 1.77x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.032 us | 0.049 us | 1.74x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.093 us | 0.055 us | 0.077 us | 1.68x |
| `common/ip-miss` | `IsMatch` | 0.080 us | 0.049 us | 0.071 us | 1.64x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.087 us | 1.63x |
| `common/replace-words` | `Replace` | 27.512 us | 17.468 us | 18.606 us | 1.58x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.048 us | 0.069 us | 1.54x |
| `industry/leipzig-symbol-count` | `Count` | 1,878.107 us | 1,270.832 us | 6,972.550 us | 1.48x |
| `industry/leipzig-river-window-count` | `Count` | 2,162.694 us | 1,539.153 us | 8,120.025 us | 1.41x |
| `industry/mariomka-uri-count` | `Count` | 1,447.715 us | 1,132.891 us | 8,385.900 us | 1.28x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.109 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.090 us | 0.116 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.090 us | 0.121 us | 1.07x |
| `industry/leipzig-twain-count` | `Count` | 1,176.619 us | 1,301.385 us | 7,705.400 us | 0.90x |
| `common/matches-words` | `Count` | 9.334 us | 11.061 us | 14.197 us | 0.84x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.096 us | 0.130 us | 0.81x |
| `industry/mariomka-ip-count` | `Count` | 2,286.335 us | 3,482.227 us | 10,855.900 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.074 us | 0.65x |
| `common/split-words` | `Split` | 14.698 us | 23.034 us | 24.869 us | 0.64x |
| `common/matches-word` | `Count` | 1.344 us | 2.346 us | 3.504 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,633.419 us | 3,058.458 us | 8,561.175 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.199 us | 0.237 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.695 us | 593.457 us | 856.084 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,820.817 us | 8,923.056 us | 9,488.425 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 483.775 us | 4,009.658 us | 4,376.683 us | 0.12x |
| `common/matches-set` | `Count` | 7.120 us | 105.858 us | 107.494 us | 0.07x |
| `common/matches-boundary` | `Count` | 6.976 us | 105.320 us | 106.342 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 258.621 us | 4,815.931 us | 5,237.093 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.202 us | 6.337 us | 31.331 us | 1.45x |
| `lokad/langserv/color-short-hex` | 1.657 us | 1.384 us | 1.497 us | 1.20x |
| `structural/keyword-to-generic-type` | 4.944 us | 4.179 us | 18.585 us | 1.18x |
| `lokad/lexer/doc-line` | 3.244 us | 3.055 us | 3.653 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.564 us | 5.294 us | 19.590 us | 1.05x |
| `lokad/lexer/identifier` | 23.371 us | 23.314 us | 26.088 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.184 us | 7.274 us | 7.891 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.255 us | 12.522 us | 14.149 us | 0.90x |
| `fallback/lookahead` | 5.312 us | 6.102 us | 19.657 us | 0.87x |
| `lokad/lexer/operator-run` | 2.789 us | 3.453 us | 3.884 us | 0.81x |
| `literal-family/method-token-family` | 7.245 us | 9.241 us | 22.313 us | 0.78x |
| `lokad/style/hex-color` | 0.572 us | 0.831 us | 0.884 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 1.998 us | 2.956 us | 2.995 us | 0.68x |
| `literal/identifier-token` | 2.796 us | 4.360 us | 17.764 us | 0.64x |
| `lokad/docs/inline-doc-prefix` | 3.277 us | 5.112 us | 5.771 us | 0.64x |
| `literal/call-token` | 2.606 us | 4.324 us | 17.841 us | 0.60x |
| `lokad/lexer/number` | 8.041 us | 14.810 us | 15.261 us | 0.54x |
| `structural/method-family-generic-call` | 3.452 us | 6.587 us | 20.844 us | 0.52x |
| `structural/ordered-identifier-window` | 14.822 us | 28.836 us | 42.540 us | 0.51x |
| `structural/method-family-call` | 3.839 us | 8.021 us | 21.213 us | 0.48x |
| `lokad/style/cell-ref` | 1.408 us | 2.958 us | 2.974 us | 0.48x |
| `literal-family/type-token-family` | 10.532 us | 24.789 us | 38.886 us | 0.42x |
| `literal/identifier-token-casei` | 5.856 us | 15.081 us | 29.231 us | 0.39x |
| `lokad/style/range-ref` | 0.883 us | 2.751 us | 2.758 us | 0.32x |
| `structural/ordered-keyword-window` | 5.534 us | 17.876 us | 31.618 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.207 us | 17.228 us | 31.136 us | 0.30x |
| `structural/modifier-family-to-type-window` | 71.082 us | 257.187 us | 271.790 us | 0.28x |
| `lokad/lexer/string` | 21.279 us | 95.438 us | 100.810 us | 0.22x |
| `lokad/folding/region-marker` | 6.002 us | 36.743 us | 62.524 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.388 us | 24.550 us | 24.752 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.460 us | 224.914 us | 243.063 us | 0.05x |
| `lokad/langserv/url-download` | 2.477 us | 52.006 us | 52.595 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.012 us | 132.560 us | 147.065 us | 0.04x |
| `balancing/begin-end-blocks` | 5.718 us | 4,432.600 us | 4,424.339 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.188 us | 4.458 us | 28.619 us | 2.06x |
| `lokad/langserv/color-short-hex` | 1.644 us | 0.987 us | 1.162 us | 1.67x |
| `lokad/langserv/helper-identifier` | 7.171 us | 4.673 us | 5.820 us | 1.53x |
| `structural/keyword-family-to-capitalized-identifier` | 11.570 us | 7.912 us | 21.103 us | 1.46x |
| `lokad/lexer/doc-line` | 3.248 us | 2.226 us | 2.981 us | 1.46x |
| `structural/keyword-to-generic-type` | 4.947 us | 3.480 us | 17.392 us | 1.42x |
| `lokad/lexer/operator-run` | 2.792 us | 1.976 us | 2.554 us | 1.41x |
| `structural/ordered-identifier-window` | 14.750 us | 10.658 us | 24.356 us | 1.38x |
| `lokad/lexer/identifier` | 23.268 us | 16.855 us | 21.092 us | 1.38x |
| `structural/ordered-keyword-pair` | 5.551 us | 4.163 us | 17.589 us | 1.33x |
| `structural/modifier-family-to-type-window` | 71.055 us | 56.221 us | 70.700 us | 1.26x |
| `fallback/lookahead` | 5.323 us | 4.245 us | 17.456 us | 1.25x |
| `lokad/langserv/identifier-validator` | 11.161 us | 9.084 us | 11.006 us | 1.23x |
| `lokad/docs/inline-doc-prefix` | 3.284 us | 2.913 us | 3.640 us | 1.13x |
| `lokad/style/hex-color` | 0.572 us | 0.518 us | 0.607 us | 1.10x |
| `lokad/lexer/number` | 8.019 us | 7.450 us | 8.416 us | 1.08x |
| `lokad/langserv/color-rgb-hex` | 1.976 us | 1.896 us | 2.057 us | 1.04x |
| `literal/identifier-token-casei` | 5.866 us | 5.894 us | 19.775 us | 1.00x |
| `literal-family/type-token-family` | 10.595 us | 11.550 us | 25.161 us | 0.92x |
| `lokad/style/cell-ref` | 1.404 us | 1.764 us | 1.982 us | 0.80x |
| `structural/method-family-call` | 3.847 us | 5.098 us | 18.029 us | 0.75x |
| `structural/method-family-generic-call` | 3.456 us | 4.637 us | 17.940 us | 0.75x |
| `literal-family/method-token-family` | 3.885 us | 5.358 us | 18.120 us | 0.73x |
| `literal/call-token` | 2.601 us | 3.975 us | 16.871 us | 0.65x |
| `structural/ordered-keyword-window` | 5.487 us | 8.655 us | 22.321 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.209 us | 8.570 us | 22.296 us | 0.61x |
| `lokad/style/range-ref` | 0.882 us | 1.488 us | 1.578 us | 0.59x |
| `literal/identifier-token` | 2.795 us | 4.756 us | 17.427 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 4.983 us | 10.582 us | 24.541 us | 0.47x |
| `lokad/lexer/string` | 21.428 us | 45.996 us | 49.244 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.380 us | 7.472 us | 8.121 us | 0.32x |
| `lokad/langserv/url-download` | 2.453 us | 12.964 us | 13.598 us | 0.19x |
| `lokad/folding/region-marker` | 6.054 us | 37.138 us | 62.685 us | 0.16x |
| `balancing/begin-end-blocks` | 5.717 us | 469.254 us | 499.427 us | 0.01x |
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
