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
byte[] redacted = regex.Replace(input, "[order]"u8);
```

Use `EnumerateMatches` to visit whole matches and `CreateMatchCursor` over
`ReadOnlyMemory<byte>` for groups and capture history. Match byte ranges can be used directly with UTF-8
slices. `EnumerateMatches(input, startOffsetInBytes, cancellationToken)` searches
from a scalar-aligned byte position while retaining full context for assertions.
The byte-start `Match` can fill caller-owned `Utf8CaptureRange` slots; slot zero
is the whole match and `GroupNumberFromCaptureSlot` resolves sparse group numbers.
Use the persistent memory cursor when capture history is required.
Copy a slot's name into caller storage with `TryCopyGroupNameUtf8`; metadata
collections and text decoding belong to the host.

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

All four profiles consume already-valid UTF-8 text. Match and capture results
expose byte coordinates only; callers derive character positions or strings.
Keep borrowed input unchanged and its owner alive while a view or cursor uses it.
Owned snapshots remain valid after cursor advance or disposal.

## Subject input contract

Validate external bytes where they become text, using
`System.Text.Unicode.Utf8.IsValid` when validity is not already guaranteed by the
host's text representation. Regex operations do not perform a separate subject
validity pass and expose no validation policy or token.

This applies to matching, counting, replacements, splits and cursors in Core,
PythonRe, PCRE2 and POSIX. Malformed subjects have unspecified matching results;
managed bounds checking, cancellation and enforceable resource controls remain.
Pattern, replacement-template and assembled-output checks remain independent.
Live route eligibility, scalar decoding and output sizing may still inspect text.
See the [input contract](docs/SPEC.md#subject-input-contract).

## Cancellation and limits

Instances accept cancellation tokens, timeouts and optional work, depth, working-
capacity and result-capacity limits:

```csharp
var bounded = new Utf8Regex(
    @"(?<word>\w+)", RegexOptions.CultureInvariant,
    policy: new Utf8RegexPolicy
    {
        MatchTimeout = TimeSpan.FromSeconds(1),
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 256,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
        }
    });

using var cancellation = new CancellationTokenSource();
int count = bounded.Count("café tea"u8, cancellation.Token); // 2
```

Core operations have optional cancellation parameters. IsMatch/Count also accept
`workingMemoryAllowance:` for shared temporary-storage admission. Omitting it or
passing null keeps numeric limits and inherits program funding when selected;
a refused non-null account fails
without retry. For span enumeration, name the token `cancellationToken:`.

These caps are examples. Defaults have unlimited invocation
budgets. Byte limits cover modeled engine/result capacities, not total process
memory. Constructors and `Compile` accept immutable `Utf8RegexPolicy` construction
controls and optional program funding. Dispose funded expressions. Span enumerators
borrow an open regex; persistent cursors can retain admission after it closes.
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

The **18 November 2026** refresh measured source `654d5716` on **.NET 11.0.0**,
Ubuntu 24.04.4 x64 and an Intel Xeon Platinum 8573C. The Release build, .NET 10/11
tests, all-profile catalogs, paired comparisons, lifecycle, scaling and resource
checks completed and verified in **52m17s**.

The measured operations start with valid, unchanged UTF-8 and include the required
result materialization. [The Core summary](bench/Snapshots/Core.json) records each
row's source, qualification and snapshot identity. [Generality measurements](docs/BENCHMARKS.md)
cover ordinary patterns and calls with finite limits, deadlines and cancellation.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

The `common/ip-miss` row was refreshed separately on `edf04445` with the
corrected miss input.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

Focused Core counts use the caller-valid text contract, for example
`--measure-readme-public-case industry/mariomka-email-count 1 9`.
These measurements label that contract and leave recorded tables unchanged.

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
| `literal/sherlock-zh` | 22.214 us | 18.850 us | 2,164.203 us | 1.18x |
| `aws-keys/full` | 68,695.800 us | 85,803.100 us | 117,149.400 us | 0.80x |
| `literal/sherlock-ru` | 66.848 us | 98.337 us | 5,294.913 us | 0.68x |
| `literal-alternate/sherlock-zh` | 43.586 us | 68.132 us | 2,200.071 us | 0.64x |
| `literal/sherlock-casei-en` | 104.438 us | 188.444 us | 592.061 us | 0.55x |
| `ruff-noqa/tweaked` | 3,810.385 us | 7,589.282 us | 44,153.100 us | 0.50x |
| `aws-keys/quick` | 4,349.780 us | 9,656.350 us | 46,169.200 us | 0.45x |
| `literal/sherlock-casei-ru` | 148.242 us | 368.116 us | 5,556.892 us | 0.40x |
| `bounded-repeat/letters-en` | 1,098.835 us | 2,833.604 us | 2,944.932 us | 0.39x |
| `literal/sherlock-en` | 31.228 us | 86.774 us | 464.114 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,968.147 us | 5,822.438 us | 6,614.242 us | 0.34x |
| `dictionary/single` | 8,147.820 us | 26,014.600 us | 26,332.367 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,454.267 us | 4,816.356 us | 9,985.050 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 158.675 us | 613.020 us | 1,034.824 us | 0.26x |
| `bounded-repeat/capitals` | 11,290.657 us | 45,460.050 us | 48,196.650 us | 0.25x |
| `literal-alternate/sherlock-en` | 346.294 us | 1,549.290 us | 2,062.776 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 127.853 us | 666.287 us | 1,103.437 us | 0.19x |
| `bounded-repeat/context` | 13,979.383 us | 146,742.700 us | 150,990.500 us | 0.10x |
| `ruff-noqa/real` | 3,540.223 us | 69,384.300 us | 85,129.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 873.364 us | 23,185.175 us | 28,272.350 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 126.434 us | 11,908.414 us | 12,790.483 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.241 us | 0.108 us | 0.146 us | 2.23x |
| `common/date-match` | `IsMatch` | 0.238 us | 0.109 us | 0.140 us | 2.18x |
| `industry/mariomka-uri-count` | `Count` | 3,320.743 us | 1,825.000 us | 10,210.725 us | 1.82x |
| `common/uri-match` | `IsMatch` | 0.407 us | 0.234 us | 0.273 us | 1.74x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.130 us | 0.076 us | 0.089 us | 1.70x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.133 us | 0.082 us | 0.096 us | 1.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.128 us | 0.083 us | 0.099 us | 1.55x |
| `common/email-match` | `IsMatch` | 0.417 us | 0.270 us | 0.299 us | 1.54x |
| `common/ip-match` | `IsMatch` | 0.260 us | 0.172 us | 0.192 us | 1.51x |
| `common/uri-miss` | `IsMatch` | 0.225 us | 0.182 us | 0.204 us | 1.23x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.189 us | 0.170 us | 0.188 us | 1.11x |
| `common/ip-miss` | `IsMatch` | 0.780 us | 0.716 us | 0.728 us | 1.09x |
| `industry/mariomka-email-count` | `Count` | 438.906 us | 490.537 us | 8,901.400 us | 0.89x |
| `common/date-miss` | `IsMatch` | 0.496 us | 0.566 us | 0.627 us | 0.88x |
| `common/match-word` | `Match` | 0.113 us | 0.165 us | 1.093 us | 0.68x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 149.246 us | 259.446 us | 497.124 us | 0.58x |
| `industry/leipzig-twain-count` | `Count` | 617.862 us | 1,287.836 us | 8,414.756 us | 0.48x |
| `common/matches-set` | `Count` | 62.708 us | 195.211 us | 197.453 us | 0.32x |
| `common/matches-word` | `Count` | 1.234 us | 4.218 us | 5.946 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,294.727 us | 8,231.060 us | 16,703.833 us | 0.28x |
| `common/replace-words` | `Replace` | 20.947 us | 76.924 us | 77.582 us | 0.27x |
| `common/split-words` | `Split` | 14.085 us | 78.926 us | 80.029 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.497 us | 969.903 us | 1,259.188 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.388 us | 165.301 us | 167.809 us | 0.17x |
| `common/matches-words` | `Count` | 9.213 us | 68.877 us | 70.808 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.038 us | 0.351 us | 0.374 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 1,030.889 us | 9,844.850 us | 16,498.600 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,931.765 us | 19,165.225 us | 19,777.400 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,179.105 us | 11,747.829 us | 12,286.314 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.049 us | 0.611 us | 0.650 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.058 us | 0.817 us | 0.857 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,833.033 us | 31,904.100 us | 39,360.500 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 1,026.664 us | 24,079.325 us | 31,915.200 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 333.000 us | 8,094.480 us | 8,606.122 us | 0.04x |
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
| `aws-keys/full` | 67,720.200 us | 18,470.220 us | 53,035.167 us | 3.67x |
| `literal/sherlock-casei-en` | 103.778 us | 77.140 us | 438.413 us | 1.35x |
| `literal-alternate/sherlock-zh` | 43.424 us | 36.052 us | 2,150.306 us | 1.20x |
| `aws-keys/quick` | 4,404.347 us | 3,680.433 us | 37,997.300 us | 1.20x |
| `literal-alternate/sherlock-en` | 349.212 us | 342.826 us | 749.696 us | 1.02x |
| `bounded-repeat/letters-ru` | 1,967.656 us | 2,056.430 us | 2,866.693 us | 0.96x |
| `literal/sherlock-zh` | 22.175 us | 23.795 us | 2,156.891 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 157.468 us | 173.032 us | 532.060 us | 0.91x |
| `literal-alternate/sherlock-casei-en` | 126.037 us | 144.916 us | 594.065 us | 0.87x |
| `ruff-noqa/tweaked` | 3,871.685 us | 4,552.929 us | 38,515.550 us | 0.85x |
| `bounded-repeat/capitals` | 11,278.986 us | 13,751.833 us | 16,992.600 us | 0.82x |
| `literal/sherlock-ru` | 66.694 us | 90.088 us | 5,213.520 us | 0.74x |
| `literal/sherlock-casei-ru` | 148.124 us | 224.887 us | 5,419.307 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 127.032 us | 202.482 us | 641.058 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,452.738 us | 2,351.978 us | 7,430.650 us | 0.62x |
| `bounded-repeat/letters-en` | 1,099.130 us | 1,812.844 us | 1,916.038 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 873.587 us | 1,950.144 us | 7,115.600 us | 0.45x |
| `literal/sherlock-en` | 31.481 us | 76.497 us | 429.908 us | 0.41x |
| `bounded-repeat/context` | 13,905.300 us | 68,346.950 us | 71,606.600 us | 0.20x |
| `dictionary/single` | 8,141.300 us | 45,416.950 us | 45,624.600 us | 0.18x |
| `ruff-noqa/real` | 3,476.873 us | 20,678.275 us | 49,537.333 us | 0.17x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.237 us | 0.042 us | 0.072 us | 5.62x |
| `common/ip-match` | `IsMatch` | 0.265 us | 0.047 us | 0.067 us | 5.57x |
| `common/email-match` | `IsMatch` | 0.412 us | 0.081 us | 0.109 us | 5.09x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.238 us | 0.048 us | 0.087 us | 4.93x |
| `common/uri-match` | `IsMatch` | 0.409 us | 0.087 us | 0.122 us | 4.70x |
| `common/ip-miss` | `IsMatch` | 0.782 us | 0.177 us | 0.201 us | 4.43x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.134 us | 0.031 us | 0.047 us | 4.33x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.130 us | 0.031 us | 0.049 us | 4.21x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.130 us | 0.036 us | 0.055 us | 3.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.188 us | 0.055 us | 0.077 us | 3.40x |
| `common/uri-miss` | `IsMatch` | 0.224 us | 0.066 us | 0.088 us | 3.40x |
| `industry/mariomka-uri-count` | `Count` | 3,340.848 us | 1,102.075 us | 8,375.325 us | 3.03x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 149.773 us | 60.288 us | 267.283 us | 2.48x |
| `common/date-miss` | `IsMatch` | 0.494 us | 0.200 us | 0.236 us | 2.47x |
| `common/match-word` | `Match` | 0.112 us | 0.076 us | 0.985 us | 1.47x |
| `common/backtracking` | `IsMatch` | 0.058 us | 0.045 us | 0.073 us | 1.29x |
| `common/replace-words` | `Replace` | 20.740 us | 17.129 us | 18.386 us | 1.21x |
| `industry/leipzig-river-window-count` | `Count` | 1,838.655 us | 1,527.036 us | 7,762.060 us | 1.20x |
| `industry/mariomka-email-count` | `Count` | 441.760 us | 459.127 us | 7,651.975 us | 0.96x |
| `common/matches-words` | `Count` | 9.125 us | 10.914 us | 13.916 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,041.191 us | 1,251.433 us | 7,308.340 us | 0.83x |
| `common/split-words` | `Split` | 14.241 us | 20.947 us | 22.606 us | 0.68x |
| `industry/mariomka-ip-count` | `Count` | 2,292.967 us | 3,473.914 us | 10,694.800 us | 0.66x |
| `common/matches-set` | `Count` | 62.513 us | 104.877 us | 106.598 us | 0.60x |
| `common/one-node-backtracking` | `IsMatch` | 0.049 us | 0.087 us | 0.129 us | 0.57x |
| `common/matches-word` | `Count` | 1.239 us | 2.370 us | 3.437 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 623.735 us | 1,264.175 us | 7,310.610 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.039 us | 0.090 us | 0.117 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 1,043.597 us | 3,015.454 us | 7,939.025 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.354 us | 594.297 us | 840.823 us | 0.28x |
| `common/matches-boundary` | `Count` | 27.462 us | 104.806 us | 106.750 us | 0.26x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,184.868 us | 4,737.619 us | 5,139.867 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,924.860 us | 8,618.278 us | 9,260.712 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 339.119 us | 3,929.630 us | 4,296.978 us | 0.09x |
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
| `lokad/docs/inline-doc-prefix` | 12.800 us | 4.826 us | 5.601 us | 2.65x |
| `lokad/style/cell-ref` | 7.281 us | 2.759 us | 2.967 us | 2.64x |
| `structural/ordered-modifier-pair` | 44.061 us | 17.218 us | 30.659 us | 2.56x |
| `lokad/style/range-ref` | 6.322 us | 2.581 us | 2.637 us | 2.45x |
| `lokad/langserv/identifier-validator` | 28.720 us | 12.556 us | 14.578 us | 2.29x |
| `lokad/lexer/number` | 29.704 us | 13.898 us | 15.022 us | 2.14x |
| `fallback/lookahead` | 12.572 us | 5.932 us | 18.728 us | 2.12x |
| `lokad/lexer/doc-line` | 6.331 us | 3.077 us | 3.656 us | 2.06x |
| `lokad/lexer/identifier` | 45.863 us | 22.812 us | 26.869 us | 2.01x |
| `lokad/style/hex-color` | 1.557 us | 0.803 us | 0.853 us | 1.94x |
| `structural/method-family-generic-call` | 11.874 us | 6.491 us | 19.943 us | 1.83x |
| `lokad/langserv/helper-identifier` | 13.050 us | 7.354 us | 8.075 us | 1.77x |
| `structural/ordered-keyword-pair` | 9.220 us | 5.300 us | 18.809 us | 1.74x |
| `lokad/langserv/color-short-hex` | 2.292 us | 1.346 us | 1.435 us | 1.70x |
| `lokad/lexer/string` | 165.130 us | 98.833 us | 103.211 us | 1.67x |
| `lokad/langserv/color-rgb-hex` | 4.911 us | 3.028 us | 3.058 us | 1.62x |
| `lokad/lexer/operator-run` | 5.426 us | 3.429 us | 3.917 us | 1.58x |
| `lokad/imports/module-imports` | 9.375 us | 6.413 us | 31.429 us | 1.46x |
| `structural/method-family-call` | 10.892 us | 7.878 us | 21.171 us | 1.38x |
| `structural/keyword-to-generic-type` | 5.629 us | 4.115 us | 17.826 us | 1.37x |
| `lokad/langserv/url-dashboard` | 31.254 us | 24.092 us | 24.424 us | 1.30x |
| `structural/ordered-keyword-window` | 20.709 us | 17.678 us | 30.569 us | 1.17x |
| `lokad/langserv/url-download` | 48.487 us | 49.657 us | 50.182 us | 0.98x |
| `literal/identifier-token` | 2.739 us | 4.293 us | 17.168 us | 0.64x |
| `structural/ordered-identifier-window` | 15.433 us | 28.450 us | 41.813 us | 0.54x |
| `literal/call-token` | 1.816 us | 4.261 us | 17.168 us | 0.43x |
| `literal-family/method-token-family` | 3.815 us | 9.076 us | 21.752 us | 0.42x |
| `literal-family/type-token-family` | 9.731 us | 23.797 us | 37.938 us | 0.41x |
| `structural/modifier-family-to-type-window` | 102.360 us | 250.462 us | 265.597 us | 0.41x |
| `balancing/begin-end-blocks` | 1,477.456 us | 4,339.588 us | 4,364.459 us | 0.34x |
| `literal/identifier-token-casei` | 3.914 us | 15.661 us | 28.230 us | 0.25x |
| `structural/modifier-family-to-type-pair` | 20.733 us | 132.101 us | 146.270 us | 0.16x |
| `lokad/folding/region-marker` | 5.241 us | 35.474 us | 63.329 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 26.277 us | 218.561 us | 233.555 us | 0.12x |
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
| `structural/ordered-modifier-pair` | 44.684 us | 8.565 us | 21.772 us | 5.22x |
| `lokad/docs/inline-doc-prefix` | 12.684 us | 2.778 us | 3.676 us | 4.57x |
| `lokad/langserv/url-dashboard` | 32.120 us | 7.382 us | 7.901 us | 4.35x |
| `lokad/style/range-ref` | 6.289 us | 1.451 us | 1.563 us | 4.33x |
| `lokad/style/cell-ref` | 7.275 us | 1.730 us | 1.907 us | 4.21x |
| `lokad/lexer/number` | 29.374 us | 7.005 us | 8.395 us | 4.19x |
| `lokad/langserv/url-download` | 48.979 us | 12.861 us | 13.486 us | 3.81x |
| `lokad/lexer/string` | 164.685 us | 46.627 us | 50.016 us | 3.53x |
| `structural/keyword-family-to-capitalized-identifier` | 26.204 us | 7.621 us | 21.510 us | 3.44x |
| `balancing/begin-end-blocks` | 1,463.086 us | 465.599 us | 497.814 us | 3.14x |
| `lokad/langserv/identifier-validator` | 29.078 us | 9.514 us | 11.538 us | 3.06x |
| `lokad/style/hex-color` | 1.559 us | 0.512 us | 0.593 us | 3.04x |
| `fallback/lookahead` | 12.631 us | 4.171 us | 16.599 us | 3.03x |
| `lokad/lexer/doc-line` | 6.317 us | 2.231 us | 2.967 us | 2.83x |
| `lokad/langserv/helper-identifier` | 13.067 us | 4.814 us | 6.085 us | 2.71x |
| `lokad/lexer/identifier` | 45.778 us | 17.018 us | 21.355 us | 2.69x |
| `lokad/lexer/operator-run` | 5.426 us | 2.054 us | 2.600 us | 2.64x |
| `structural/method-family-generic-call` | 11.938 us | 4.551 us | 17.174 us | 2.62x |
| `lokad/langserv/color-rgb-hex` | 4.857 us | 1.917 us | 2.025 us | 2.53x |
| `structural/ordered-keyword-window` | 21.139 us | 8.491 us | 21.507 us | 2.49x |
| `lokad/langserv/color-short-hex` | 2.270 us | 0.963 us | 1.178 us | 2.36x |
| `structural/ordered-keyword-pair` | 9.225 us | 4.080 us | 17.045 us | 2.26x |
| `structural/method-family-call` | 10.920 us | 5.005 us | 17.946 us | 2.18x |
| `lokad/imports/module-imports` | 9.423 us | 4.410 us | 28.069 us | 2.14x |
| `structural/modifier-family-to-type-pair` | 20.518 us | 10.527 us | 23.198 us | 1.95x |
| `structural/modifier-family-to-type-window` | 101.812 us | 55.553 us | 69.221 us | 1.83x |
| `structural/keyword-to-generic-type` | 5.621 us | 3.466 us | 16.726 us | 1.62x |
| `structural/ordered-identifier-window` | 15.331 us | 10.564 us | 24.408 us | 1.45x |
| `literal-family/type-token-family` | 9.699 us | 11.402 us | 24.453 us | 0.85x |
| `literal-family/method-token-family` | 3.831 us | 5.085 us | 17.734 us | 0.75x |
| `literal/identifier-token-casei` | 3.934 us | 5.895 us | 18.944 us | 0.67x |
| `literal/identifier-token` | 2.744 us | 4.271 us | 16.885 us | 0.64x |
| `literal/call-token` | 1.820 us | 3.867 us | 16.225 us | 0.47x |
| `lokad/folding/region-marker` | 5.222 us | 36.710 us | 62.462 us | 0.14x |
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
