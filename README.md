# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes ongoing 0.5.0 development. Its API and execution machinery
are being simplified. Published 0.4.0 and frozen development packages retain
their original behavior; development package qualification is not a release.

## Quick start

```csharp
using Lokad.Utf8Regex;
using System.Text.RegularExpressions;

var regex = new Utf8Regex(@"[A-Z]{2}\d{4}", RegexOptions.CultureInvariant);
ReadOnlySpan<byte> input = "Order AB1234 is ready."u8;

bool found = regex.IsMatch(input);              // true
int count = regex.Count(input);                 // 1
Utf8CaptureRange first = regex.Match(input, 0, []); // bytes [6, 12)
byte[] redacted = regex.Replace(input, "[order]");
```

Use `EnumerateMatches` to visit whole matches and `CreateMatchCursor` over
`ReadOnlyMemory<byte>` for groups and capture history. Match byte ranges can be used directly with UTF-8
slices. `EnumerateMatches(input, startOffsetInBytes, cancellationToken)` searches
from a scalar-aligned byte position while retaining full context for assertions.
The byte-start `Match` can fill caller-owned `Utf8CaptureRange` slots; slot zero
is the whole match and `GroupNumberFromCaptureSlot` resolves sparse group numbers.
Use the persistent memory cursor when capture history is required.

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

Core's default Validate policy checks the entire subject, including text after
an early match. PythonRe and POSIX consume existing valid text; PCRE2 checks its
native UTF-mode window and has an explicit trusted-input bypass.
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

These caps are examples. Defaults have unlimited invocation
budgets. Byte limits cover modeled engine/result capacities, not total process
memory. `Compile` offers separate construction controls.
See the [resource contract](docs/SPEC.md#cancellation-deadlines-and-resource-limits)
for failure, deadline and ownership details.

See [release notes](CHANGELOG.md) and the [migration guide](docs/MIGRATION.md)
for the 0.4.0 changes.

## Runtime support

The 0.5.0 release candidate targets `net10.0` and `net11.0`. .NET 11
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

These are historical results. Pattern-specific executors are being retired;
the `balancing/begin-end-blocks` counter has already been removed. Its recorded
ratios do not describe current execution. [Generality measurements](docs/BENCHMARKS.md)
cover ordinary patterns and controlled calls; tables will refresh after the
deletion batch.

The tables were refreshed on **15 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks completed in **47m02s**. Package and consumer qualification
are separate.
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
| `literal/sherlock-zh` | 70.855 us | 18.889 us | 2,187.450 us | 3.75x |
| `literal/sherlock-ru` | 162.114 us | 96.047 us | 5,301.340 us | 1.69x |
| `literal-alternate/sherlock-zh` | 95.493 us | 68.902 us | 2,233.151 us | 1.39x |
| `literal/sherlock-en` | 58.945 us | 87.395 us | 481.049 us | 0.67x |
| `literal/sherlock-casei-en` | 131.177 us | 198.831 us | 622.085 us | 0.66x |
| `literal/sherlock-casei-ru` | 238.257 us | 393.303 us | 5,397.764 us | 0.61x |
| `ruff-noqa/tweaked` | 4,609.425 us | 9,341.155 us | 41,911.400 us | 0.49x |
| `aws-keys/quick` | 3,976.037 us | 9,561.150 us | 40,976.400 us | 0.42x |
| `bounded-repeat/letters-en` | 1,135.727 us | 2,820.815 us | 2,933.735 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,115.261 us | 5,872.923 us | 6,737.950 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 214.445 us | 601.985 us | 1,024.265 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,642.643 us | 4,725.663 us | 9,866.675 us | 0.35x |
| `dictionary/single` | 8,296.840 us | 26,192.800 us | 26,268.100 us | 0.32x |
| `literal-alternate/sherlock-en` | 405.252 us | 1,541.151 us | 2,056.328 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 156.131 us | 651.272 us | 1,088.715 us | 0.24x |
| `bounded-repeat/capitals` | 9,531.737 us | 46,884.800 us | 50,356.000 us | 0.20x |
| `bounded-repeat/context` | 21,047.525 us | 147,576.400 us | 151,019.800 us | 0.14x |
| `aws-keys/full` | 11,033.200 us | 86,667.400 us | 118,030.000 us | 0.13x |
| `ruff-noqa/real` | 4,465.971 us | 73,535.800 us | 102,359.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 989.674 us | 23,371.250 us | 28,671.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 162.440 us | 11,186.000 us | 12,171.386 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 915.255 us | 484.792 us | 8,855.525 us | 1.89x |
| `common/match-word` | `Match` | 0.194 us | 0.167 us | 1.128 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,277.768 us | 1,367.228 us | 8,535.211 us | 0.93x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.111 us | 0.149 us | 0.72x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.189 us | 0.209 us | 0.61x |
| `industry/mariomka-uri-count` | `Count` | 1,133.357 us | 1,885.861 us | 10,075.575 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.085 us | 0.101 us | 0.58x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.086 us | 0.171 us | 0.190 us | 0.50x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 121.257 us | 265.269 us | 508.947 us | 0.46x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.184 us | 0.206 us | 0.41x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.181 us | 0.201 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.225 us | 0.263 us | 0.38x |
| `common/replace-words` | `Replace` | 27.796 us | 77.044 us | 79.496 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.268 us | 0.294 us | 0.35x |
| `common/matches-word` | `Count` | 1.348 us | 4.240 us | 5.935 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.343 us | 0.364 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,193.368 us | 8,263.430 us | 16,719.800 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.109 us | 0.135 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.083 us | 0.098 us | 0.20x |
| `common/date-miss` | `IsMatch` | 0.113 us | 0.561 us | 0.596 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 186.803 us | 947.601 us | 1,229.595 us | 0.20x |
| `common/split-words` | `Split` | 14.647 us | 77.566 us | 79.309 us | 0.19x |
| `industry/leipzig-name-family-count` | `Count` | 1,662.462 us | 9,845.475 us | 16,895.067 us | 0.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.083 us | 0.611 us | 0.644 us | 0.14x |
| `common/matches-words` | `Count` | 9.340 us | 69.368 us | 71.300 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.825 us | 0.865 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,858.430 us | 19,996.925 us | 20,716.700 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,667.007 us | 22,834.225 us | 30,221.000 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,218.297 us | 31,976.100 us | 39,383.650 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 493.487 us | 8,242.240 us | 8,711.867 us | 0.06x |
| `common/matches-boundary` | `Count` | 9.744 us | 162.996 us | 164.789 us | 0.06x |
| `common/matches-set` | `Count` | 10.579 us | 194.884 us | 197.596 us | 0.05x |
| `industry/rust-sherlock-ing-count` | `Count` | 256.514 us | 11,858.986 us | 12,408.317 us | 0.02x |
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
| `literal/sherlock-zh` | 70.361 us | 23.991 us | 2,207.540 us | 2.93x |
| `literal-alternate/sherlock-zh` | 95.738 us | 36.547 us | 2,176.700 us | 2.62x |
| `literal/sherlock-ru` | 159.765 us | 87.434 us | 5,230.513 us | 1.83x |
| `literal/sherlock-casei-en` | 130.950 us | 77.797 us | 444.041 us | 1.68x |
| `literal-alternate/sherlock-casei-en` | 163.210 us | 131.624 us | 674.759 us | 1.24x |
| `literal-alternate/sherlock-en-mixed` | 214.050 us | 178.921 us | 596.708 us | 1.20x |
| `literal-alternate/sherlock-en` | 404.666 us | 372.009 us | 775.613 us | 1.09x |
| `aws-keys/quick` | 3,978.726 us | 3,681.724 us | 35,830.533 us | 1.08x |
| `literal/sherlock-casei-ru` | 239.246 us | 222.720 us | 5,246.729 us | 1.07x |
| `bounded-repeat/letters-ru` | 2,125.611 us | 2,056.414 us | 2,921.027 us | 1.03x |
| `ruff-noqa/tweaked` | 4,602.531 us | 4,590.671 us | 35,747.000 us | 1.00x |
| `literal/sherlock-en` | 59.045 us | 76.738 us | 430.077 us | 0.77x |
| `literal-alternate/sherlock-en-nomatch` | 156.106 us | 203.981 us | 604.860 us | 0.77x |
| `bounded-repeat/capitals` | 9,513.987 us | 13,593.200 us | 16,571.475 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,646.385 us | 2,358.381 us | 7,475.575 us | 0.70x |
| `bounded-repeat/letters-en` | 1,133.139 us | 1,838.378 us | 1,931.679 us | 0.62x |
| `aws-keys/full` | 10,214.571 us | 18,595.400 us | 50,586.433 us | 0.55x |
| `literal-alternate/sherlock-casei-ru` | 977.497 us | 1,954.492 us | 7,102.500 us | 0.50x |
| `bounded-repeat/context` | 21,215.550 us | 69,026.750 us | 71,959.900 us | 0.31x |
| `ruff-noqa/real` | 4,535.947 us | 20,817.900 us | 52,173.933 us | 0.22x |
| `dictionary/single` | 8,299.111 us | 45,902.950 us | 45,700.650 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.193 us | 0.076 us | 1.012 us | 2.54x |
| `industry/mariomka-email-count` | `Count` | 923.890 us | 450.399 us | 7,687.950 us | 2.05x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.043 us | 0.073 us | 2.05x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 121.107 us | 59.854 us | 269.823 us | 2.02x |
| `common/uri-miss` | `IsMatch` | 0.114 us | 0.068 us | 0.086 us | 1.67x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.048 us | 0.088 us | 1.65x |
| `common/replace-words` | `Replace` | 27.786 us | 17.401 us | 20.106 us | 1.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.054 us | 0.075 us | 1.58x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.047 us | 0.068 us | 1.57x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.048 us | 0.070 us | 1.56x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.048 us | 0.031 us | 0.047 us | 1.55x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.033 us | 0.050 us | 1.50x |
| `industry/leipzig-river-window-count` | `Count` | 2,229.044 us | 1,554.135 us | 7,708.240 us | 1.43x |
| `industry/leipzig-symbol-count` | `Count` | 1,679.398 us | 1,241.033 us | 6,812.550 us | 1.35x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.081 us | 0.108 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.088 us | 0.115 us | 1.10x |
| `industry/mariomka-uri-count` | `Count` | 1,137.530 us | 1,100.596 us | 8,206.400 us | 1.03x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.088 us | 0.119 us | 0.98x |
| `industry/leipzig-twain-count` | `Count` | 1,277.193 us | 1,340.216 us | 7,461.410 us | 0.95x |
| `common/one-node-backtracking` | `IsMatch` | 0.083 us | 0.091 us | 0.128 us | 0.92x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.072 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,206.097 us | 3,483.982 us | 10,765.867 us | 0.63x |
| `common/split-words` | `Split` | 14.574 us | 23.339 us | 25.317 us | 0.62x |
| `common/matches-words` | `Count` | 9.329 us | 15.339 us | 17.666 us | 0.61x |
| `common/matches-word` | `Count` | 1.337 us | 2.345 us | 3.408 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.114 us | 0.201 us | 0.236 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,655.767 us | 3,039.884 us | 8,138.100 us | 0.54x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.036 us | 0.052 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 188.725 us | 586.302 us | 849.264 us | 0.32x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,863.856 us | 8,844.378 us | 9,500.425 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 491.908 us | 3,974.047 us | 4,300.828 us | 0.12x |
| `common/matches-set` | `Count` | 10.629 us | 105.765 us | 108.329 us | 0.10x |
| `common/matches-boundary` | `Count` | 9.719 us | 105.918 us | 107.311 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 254.795 us | 4,805.175 us | 5,225.227 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.273 us | 6.419 us | 31.483 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.624 us | 4.079 us | 18.500 us | 1.38x |
| `lokad/langserv/color-short-hex` | 1.658 us | 1.369 us | 1.473 us | 1.21x |
| `lokad/lexer/doc-line` | 3.230 us | 2.993 us | 3.640 us | 1.08x |
| `lokad/lexer/identifier` | 23.194 us | 22.913 us | 25.412 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.342 us | 7.365 us | 7.803 us | 1.00x |
| `structural/ordered-keyword-pair` | 5.325 us | 5.413 us | 19.626 us | 0.98x |
| `fallback/lookahead` | 5.883 us | 6.067 us | 19.653 us | 0.97x |
| `lokad/langserv/identifier-validator` | 11.175 us | 12.549 us | 13.866 us | 0.89x |
| `lokad/style/hex-color` | 0.725 us | 0.815 us | 0.867 us | 0.89x |
| `lokad/lexer/operator-run` | 2.811 us | 3.474 us | 3.840 us | 0.81x |
| `literal-family/method-token-family` | 7.176 us | 9.098 us | 22.345 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 2.055 us | 3.058 us | 2.965 us | 0.67x |
| `lokad/docs/inline-doc-prefix` | 3.318 us | 4.941 us | 5.448 us | 0.67x |
| `literal/identifier-token` | 2.771 us | 4.372 us | 17.698 us | 0.63x |
| `literal/call-token` | 2.630 us | 4.320 us | 18.109 us | 0.61x |
| `lokad/lexer/number` | 7.962 us | 14.328 us | 15.125 us | 0.56x |
| `structural/ordered-identifier-window` | 15.173 us | 28.870 us | 43.455 us | 0.53x |
| `structural/method-family-generic-call` | 3.413 us | 6.562 us | 20.657 us | 0.52x |
| `structural/method-family-call` | 3.936 us | 8.015 us | 21.437 us | 0.49x |
| `lokad/style/cell-ref` | 1.412 us | 2.977 us | 3.020 us | 0.47x |
| `literal-family/type-token-family` | 10.477 us | 24.639 us | 38.739 us | 0.43x |
| `lokad/style/range-ref` | 0.885 us | 2.774 us | 2.773 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.489 us | 17.641 us | 31.550 us | 0.31x |
| `structural/ordered-keyword-window` | 5.463 us | 17.722 us | 31.497 us | 0.31x |
| `literal/identifier-token-casei` | 4.660 us | 15.314 us | 28.887 us | 0.30x |
| `structural/modifier-family-to-type-window` | 68.232 us | 254.164 us | 270.597 us | 0.27x |
| `lokad/lexer/string` | 21.440 us | 99.136 us | 103.335 us | 0.22x |
| `lokad/folding/region-marker` | 6.151 us | 37.334 us | 63.830 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.452 us | 24.256 us | 24.815 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.387 us | 220.879 us | 235.941 us | 0.05x |
| `lokad/langserv/url-download` | 2.516 us | 52.995 us | 53.842 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.004 us | 129.551 us | 142.994 us | 0.04x |
| `balancing/begin-end-blocks` | 6.498 us | 4,321.550 us | 4,337.228 us | 0.0015x |
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
| `lokad/imports/module-imports` | 9.234 us | 4.461 us | 28.500 us | 2.07x |
| `lokad/langserv/color-short-hex` | 1.650 us | 0.979 us | 1.170 us | 1.69x |
| `structural/keyword-to-generic-type` | 5.642 us | 3.446 us | 17.299 us | 1.64x |
| `lokad/langserv/helper-identifier` | 7.317 us | 4.974 us | 5.901 us | 1.47x |
| `lokad/lexer/doc-line` | 3.238 us | 2.205 us | 3.065 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.329 us | 7.887 us | 20.964 us | 1.44x |
| `structural/ordered-identifier-window` | 15.060 us | 10.679 us | 24.717 us | 1.41x |
| `fallback/lookahead` | 5.950 us | 4.239 us | 17.313 us | 1.40x |
| `lokad/lexer/identifier` | 23.095 us | 16.459 us | 20.567 us | 1.40x |
| `lokad/style/hex-color` | 0.726 us | 0.517 us | 0.597 us | 1.40x |
| `lokad/lexer/operator-run` | 2.812 us | 2.013 us | 2.572 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.306 us | 4.153 us | 17.606 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.122 us | 8.736 us | 11.115 us | 1.27x |
| `structural/modifier-family-to-type-window` | 68.437 us | 56.958 us | 72.092 us | 1.20x |
| `lokad/docs/inline-doc-prefix` | 3.292 us | 2.751 us | 3.498 us | 1.20x |
| `lokad/langserv/color-rgb-hex` | 2.065 us | 1.836 us | 1.977 us | 1.13x |
| `lokad/lexer/number` | 8.314 us | 7.434 us | 8.325 us | 1.12x |
| `literal-family/type-token-family` | 10.465 us | 11.442 us | 24.745 us | 0.91x |
| `literal/identifier-token-casei` | 4.682 us | 5.889 us | 19.601 us | 0.80x |
| `lokad/style/cell-ref` | 1.420 us | 1.823 us | 1.958 us | 0.78x |
| `structural/method-family-call` | 3.931 us | 5.096 us | 18.043 us | 0.77x |
| `structural/method-family-generic-call` | 3.418 us | 4.500 us | 17.825 us | 0.76x |
| `literal-family/method-token-family` | 3.865 us | 5.259 us | 18.208 us | 0.73x |
| `literal/call-token` | 2.614 us | 3.991 us | 17.088 us | 0.66x |
| `literal/identifier-token` | 2.763 us | 4.268 us | 17.244 us | 0.65x |
| `structural/ordered-keyword-window` | 5.485 us | 8.597 us | 22.015 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.459 us | 8.673 us | 22.374 us | 0.63x |
| `lokad/style/range-ref` | 0.891 us | 1.488 us | 1.534 us | 0.60x |
| `lokad/lexer/string` | 21.594 us | 45.614 us | 48.085 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.970 us | 10.731 us | 24.418 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.440 us | 7.389 us | 7.939 us | 0.33x |
| `lokad/langserv/url-download` | 2.512 us | 13.214 us | 13.469 us | 0.19x |
| `lokad/folding/region-marker` | 6.169 us | 37.118 us | 63.079 us | 0.17x |
| `balancing/begin-end-blocks` | 6.520 us | 468.486 us | 499.883 us | 0.01x |
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
