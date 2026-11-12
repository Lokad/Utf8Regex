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
and resource checks passed in **47m51s**. The [run receipt](bench/Experiments/DeliveryDev78-20261112/full-refresh.json)
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
| `literal/sherlock-zh` | 93.527 us | 18.223 us | 2,144.914 us | 5.13x |
| `literal-alternate/sherlock-zh` | 118.040 us | 68.237 us | 2,194.706 us | 1.73x |
| `literal/sherlock-ru` | 161.006 us | 96.173 us | 5,231.493 us | 1.67x |
| `literal/sherlock-casei-en` | 155.573 us | 184.809 us | 601.617 us | 0.84x |
| `literal/sherlock-casei-ru` | 234.663 us | 358.815 us | 5,437.007 us | 0.65x |
| `literal/sherlock-en` | 55.000 us | 85.546 us | 432.890 us | 0.64x |
| `ruff-noqa/tweaked` | 4,451.647 us | 7,659.927 us | 43,622.200 us | 0.58x |
| `bounded-repeat/letters-en` | 1,102.683 us | 2,806.100 us | 2,905.433 us | 0.39x |
| `aws-keys/quick` | 3,702.095 us | 9,493.925 us | 46,575.000 us | 0.39x |
| `bounded-repeat/letters-ru` | 1,983.808 us | 5,734.923 us | 6,506.042 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,500.878 us | 4,684.012 us | 9,903.550 us | 0.32x |
| `dictionary/single` | 8,039.770 us | 25,890.700 us | 25,729.267 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 179.803 us | 604.924 us | 1,058.409 us | 0.30x |
| `literal-alternate/sherlock-en` | 368.635 us | 1,451.502 us | 2,038.413 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 150.988 us | 660.668 us | 1,064.581 us | 0.23x |
| `bounded-repeat/capitals` | 9,137.078 us | 44,287.250 us | 47,938.100 us | 0.21x |
| `bounded-repeat/context` | 21,528.625 us | 143,461.300 us | 148,775.400 us | 0.15x |
| `aws-keys/full` | 10,669.250 us | 84,264.900 us | 117,094.500 us | 0.13x |
| `ruff-noqa/real` | 4,333.917 us | 68,389.450 us | 99,679.800 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 931.759 us | 22,605.575 us | 27,650.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 130.296 us | 11,293.857 us | 12,139.629 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,005.813 us | 482.559 us | 8,797.125 us | 2.08x |
| `common/match-word` | `Match` | 0.189 us | 0.164 us | 1.092 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,178.335 us | 1,292.922 us | 8,441.333 us | 0.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.108 us | 0.143 us | 0.71x |
| `industry/mariomka-uri-count` | `Count` | 1,226.444 us | 1,806.588 us | 10,112.925 us | 0.68x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.187 us | 0.205 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.083 us | 0.098 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.086 us | 0.166 us | 0.183 us | 0.52x |
| `common/ip-match` | `IsMatch` | 0.080 us | 0.171 us | 0.190 us | 0.47x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 105.976 us | 254.488 us | 504.152 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.095 us | 0.233 us | 0.272 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.184 us | 0.199 us | 0.40x |
| `common/replace-words` | `Replace` | 27.000 us | 74.980 us | 76.173 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.092 us | 0.258 us | 0.287 us | 0.36x |
| `common/matches-word` | `Count` | 1.313 us | 4.116 us | 5.758 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.336 us | 0.358 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,079.531 us | 8,085.930 us | 16,359.500 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.106 us | 0.135 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.081 us | 0.095 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.321 us | 932.894 us | 1,207.028 us | 0.18x |
| `common/split-words` | `Split` | 14.047 us | 78.964 us | 80.180 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.553 us | 0.589 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,553.729 us | 9,526.511 us | 16,319.000 us | 0.16x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.074 us | 0.089 us | 0.13x |
| `common/matches-words` | `Count` | 9.071 us | 68.447 us | 69.055 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.605 us | 0.636 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.086 us | 0.805 us | 0.847 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,764.507 us | 19,211.525 us | 19,671.575 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,606.051 us | 22,079.800 us | 29,453.450 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,142.364 us | 33,810.100 us | 41,240.100 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 455.788 us | 8,347.256 us | 8,869.067 us | 0.05x |
| `common/matches-boundary` | `Count` | 6.838 us | 159.171 us | 160.842 us | 0.04x |
| `common/matches-set` | `Count` | 6.895 us | 191.941 us | 193.501 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 234.828 us | 11,560.143 us | 12,208.586 us | 0.02x |
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
| `literal/sherlock-zh` | 93.652 us | 23.142 us | 2,130.917 us | 4.05x |
| `literal-alternate/sherlock-zh` | 117.507 us | 35.919 us | 2,142.714 us | 3.27x |
| `literal/sherlock-casei-en` | 154.340 us | 79.378 us | 442.037 us | 1.94x |
| `literal/sherlock-ru` | 164.594 us | 87.165 us | 5,156.840 us | 1.89x |
| `literal/sherlock-casei-ru` | 236.081 us | 193.731 us | 5,240.680 us | 1.22x |
| `literal-alternate/sherlock-en` | 369.695 us | 335.787 us | 753.204 us | 1.10x |
| `aws-keys/quick` | 3,733.867 us | 3,528.823 us | 38,561.400 us | 1.06x |
| `literal-alternate/sherlock-en-mixed` | 180.713 us | 176.694 us | 588.420 us | 1.02x |
| `bounded-repeat/letters-ru` | 1,986.959 us | 1,995.776 us | 2,843.170 us | 1.00x |
| `ruff-noqa/tweaked` | 4,456.233 us | 4,546.965 us | 37,620.500 us | 0.98x |
| `literal/sherlock-en` | 55.582 us | 74.530 us | 425.240 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 150.963 us | 206.183 us | 591.335 us | 0.73x |
| `bounded-repeat/capitals` | 9,189.050 us | 12,978.967 us | 16,131.625 us | 0.71x |
| `literal-alternate/sherlock-ru` | 1,482.363 us | 2,288.874 us | 7,375.900 us | 0.65x |
| `bounded-repeat/letters-en` | 1,085.943 us | 1,806.293 us | 1,914.722 us | 0.60x |
| `aws-keys/full` | 10,661.413 us | 18,340.040 us | 52,614.533 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 935.605 us | 1,926.692 us | 6,999.750 us | 0.49x |
| `literal-alternate/sherlock-casei-en` | 130.398 us | 286.368 us | 585.268 us | 0.46x |
| `bounded-repeat/context` | 21,573.325 us | 67,268.300 us | 70,305.150 us | 0.32x |
| `ruff-noqa/real` | 4,388.906 us | 20,840.975 us | 55,096.600 us | 0.21x |
| `dictionary/single` | 8,056.990 us | 44,632.850 us | 44,739.250 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.189 us | 0.076 us | 0.988 us | 2.48x |
| `industry/mariomka-email-count` | `Count` | 998.948 us | 454.187 us | 7,641.725 us | 2.20x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.042 us | 0.072 us | 2.07x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.055 us | 0.030 us | 0.046 us | 1.83x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 106.328 us | 58.846 us | 263.945 us | 1.81x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.066 us | 0.086 us | 1.80x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.032 us | 0.049 us | 1.74x |
| `common/ip-match` | `IsMatch` | 0.080 us | 0.047 us | 0.068 us | 1.69x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.047 us | 0.083 us | 1.63x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.086 us | 0.054 us | 0.075 us | 1.59x |
| `common/replace-words` | `Replace` | 26.919 us | 17.239 us | 19.599 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.048 us | 0.068 us | 1.43x |
| `industry/leipzig-river-window-count` | `Count` | 2,140.283 us | 1,541.526 us | 8,316.675 us | 1.39x |
| `industry/leipzig-symbol-count` | `Count` | 1,614.357 us | 1,238.602 us | 6,944.650 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.093 us | 0.080 us | 0.105 us | 1.17x |
| `industry/mariomka-uri-count` | `Count` | 1,224.440 us | 1,093.323 us | 8,247.700 us | 1.12x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.087 us | 0.113 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.089 us | 0.121 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,174.798 us | 1,286.907 us | 7,360.970 us | 0.91x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.089 us | 0.126 us | 0.83x |
| `common/split-words` | `Split` | 14.190 us | 20.431 us | 22.274 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.041 us | 0.071 us | 0.65x |
| `common/matches-words` | `Count` | 9.023 us | 14.819 us | 17.204 us | 0.61x |
| `industry/mariomka-ip-count` | `Count` | 2,082.859 us | 3,438.791 us | 10,844.700 us | 0.61x |
| `common/matches-word` | `Count` | 1.322 us | 2.287 us | 3.264 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 1,569.135 us | 2,978.548 us | 7,880.175 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.193 us | 0.229 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.035 us | 0.051 us | 0.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.472 us | 574.432 us | 832.046 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,764.066 us | 8,617.556 us | 9,094.900 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 457.160 us | 3,900.430 us | 4,286.728 us | 0.12x |
| `common/matches-boundary` | `Count` | 6.942 us | 101.422 us | 103.435 us | 0.07x |
| `common/matches-set` | `Count` | 6.906 us | 103.248 us | 104.581 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 232.021 us | 4,721.300 us | 5,176.420 us | 0.05x |
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
| `lokad/imports/module-imports` | 14.340 us | 6.204 us | 31.005 us | 2.31x |
| `lokad/langserv/color-short-hex` | 1.722 us | 1.348 us | 1.422 us | 1.28x |
| `structural/keyword-to-generic-type` | 4.808 us | 3.970 us | 18.142 us | 1.21x |
| `lokad/lexer/doc-line` | 3.154 us | 2.885 us | 3.472 us | 1.09x |
| `lokad/langserv/helper-identifier` | 7.183 us | 6.994 us | 7.535 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.238 us | 5.151 us | 19.273 us | 1.02x |
| `lokad/lexer/identifier` | 22.764 us | 22.389 us | 24.776 us | 1.02x |
| `fallback/lookahead` | 5.322 us | 5.936 us | 19.337 us | 0.90x |
| `lokad/style/hex-color` | 0.710 us | 0.795 us | 0.842 us | 0.89x |
| `lokad/langserv/identifier-validator` | 10.894 us | 12.407 us | 13.567 us | 0.88x |
| `lokad/lexer/operator-run` | 2.753 us | 3.358 us | 3.733 us | 0.82x |
| `literal-family/method-token-family` | 7.021 us | 8.913 us | 21.618 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 1.951 us | 2.869 us | 2.846 us | 0.68x |
| `lokad/docs/inline-doc-prefix` | 3.216 us | 4.960 us | 5.388 us | 0.65x |
| `literal/identifier-token` | 2.702 us | 4.224 us | 17.187 us | 0.64x |
| `literal/call-token` | 2.561 us | 4.189 us | 17.527 us | 0.61x |
| `lokad/lexer/number` | 7.716 us | 14.177 us | 15.012 us | 0.54x |
| `structural/method-family-generic-call` | 3.318 us | 6.511 us | 19.641 us | 0.51x |
| `structural/ordered-identifier-window` | 14.015 us | 27.788 us | 41.129 us | 0.50x |
| `lokad/style/cell-ref` | 1.376 us | 2.730 us | 2.784 us | 0.50x |
| `structural/method-family-call` | 3.772 us | 7.901 us | 21.147 us | 0.48x |
| `literal-family/type-token-family` | 10.121 us | 22.782 us | 36.228 us | 0.44x |
| `literal/identifier-token-casei` | 5.709 us | 14.822 us | 28.907 us | 0.39x |
| `lokad/style/range-ref` | 0.847 us | 2.724 us | 2.625 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.235 us | 17.019 us | 30.287 us | 0.31x |
| `structural/ordered-keyword-window` | 5.177 us | 17.110 us | 30.531 us | 0.30x |
| `structural/modifier-family-to-type-window` | 65.106 us | 248.926 us | 262.971 us | 0.26x |
| `lokad/lexer/string` | 21.411 us | 94.291 us | 98.131 us | 0.23x |
| `lokad/folding/region-marker` | 5.908 us | 35.533 us | 61.012 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.319 us | 23.547 us | 24.082 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 10.981 us | 214.584 us | 230.342 us | 0.05x |
| `lokad/langserv/url-download` | 2.379 us | 51.696 us | 52.766 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.890 us | 130.507 us | 144.091 us | 0.04x |
| `balancing/begin-end-blocks` | 5.556 us | 4,256.517 us | 4,255.717 us | 0.0013x |
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
| `lokad/imports/module-imports` | 14.292 us | 4.332 us | 28.512 us | 3.30x |
| `lokad/langserv/color-short-hex` | 1.869 us | 0.951 us | 1.149 us | 1.96x |
| `lokad/langserv/helper-identifier` | 7.159 us | 4.433 us | 5.383 us | 1.61x |
| `lokad/lexer/operator-run` | 2.747 us | 1.880 us | 2.469 us | 1.46x |
| `lokad/lexer/doc-line` | 3.156 us | 2.161 us | 2.840 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 11.110 us | 7.634 us | 20.678 us | 1.46x |
| `lokad/lexer/identifier` | 22.850 us | 15.896 us | 19.365 us | 1.44x |
| `structural/keyword-to-generic-type` | 4.768 us | 3.367 us | 17.098 us | 1.42x |
| `lokad/style/hex-color` | 0.711 us | 0.505 us | 0.583 us | 1.41x |
| `structural/ordered-identifier-window` | 14.044 us | 10.395 us | 23.802 us | 1.35x |
| `fallback/lookahead` | 5.395 us | 4.124 us | 17.182 us | 1.31x |
| `structural/ordered-keyword-pair` | 5.195 us | 4.013 us | 17.339 us | 1.29x |
| `lokad/langserv/identifier-validator` | 10.818 us | 8.974 us | 10.703 us | 1.21x |
| `structural/modifier-family-to-type-window` | 65.701 us | 54.541 us | 69.234 us | 1.20x |
| `lokad/docs/inline-doc-prefix` | 3.193 us | 2.658 us | 3.337 us | 1.20x |
| `lokad/langserv/color-rgb-hex` | 1.948 us | 1.763 us | 1.964 us | 1.10x |
| `lokad/lexer/number` | 7.728 us | 7.325 us | 8.530 us | 1.06x |
| `literal/identifier-token-casei` | 5.711 us | 5.809 us | 19.598 us | 0.98x |
| `literal-family/type-token-family` | 10.160 us | 11.027 us | 23.908 us | 0.92x |
| `lokad/style/cell-ref` | 1.368 us | 1.708 us | 1.855 us | 0.80x |
| `structural/method-family-call` | 3.777 us | 4.943 us | 17.773 us | 0.76x |
| `structural/method-family-generic-call` | 3.306 us | 4.340 us | 16.974 us | 0.76x |
| `literal-family/method-token-family` | 3.792 us | 5.173 us | 17.653 us | 0.73x |
| `literal/call-token` | 2.542 us | 3.844 us | 16.687 us | 0.66x |
| `structural/ordered-keyword-window` | 5.192 us | 8.310 us | 21.356 us | 0.62x |
| `structural/ordered-modifier-pair` | 5.264 us | 8.439 us | 21.838 us | 0.62x |
| `literal/identifier-token` | 2.695 us | 4.566 us | 17.005 us | 0.59x |
| `lokad/style/range-ref` | 0.846 us | 1.441 us | 1.531 us | 0.59x |
| `lokad/lexer/string` | 21.248 us | 44.799 us | 47.143 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.856 us | 10.517 us | 24.780 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.288 us | 7.206 us | 7.794 us | 0.32x |
| `lokad/langserv/url-download` | 2.401 us | 12.550 us | 13.141 us | 0.19x |
| `lokad/folding/region-marker` | 5.919 us | 35.398 us | 61.913 us | 0.17x |
| `balancing/begin-end-blocks` | 5.549 us | 452.507 us | 480.841 us | 0.01x |
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
