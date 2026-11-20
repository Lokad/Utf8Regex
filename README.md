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
| `literal/sherlock-zh` | 22.262 us | 18.889 us | 2,157.368 us | 1.18x |
| `literal/sherlock-ru` | 70.052 us | 98.847 us | 5,372.986 us | 0.71x |
| `aws-keys/full` | 61,188.800 us | 86,606.200 us | 117,569.800 us | 0.71x |
| `literal-alternate/sherlock-zh` | 43.655 us | 68.163 us | 2,162.314 us | 0.64x |
| `ruff-noqa/tweaked` | 4,000.332 us | 7,721.536 us | 45,828.050 us | 0.52x |
| `literal/sherlock-casei-en` | 94.141 us | 200.460 us | 596.457 us | 0.47x |
| `literal/sherlock-casei-ru` | 148.174 us | 365.169 us | 5,548.923 us | 0.41x |
| `bounded-repeat/letters-en` | 1,092.921 us | 2,792.050 us | 2,956.365 us | 0.39x |
| `aws-keys/quick` | 3,688.100 us | 9,714.900 us | 46,354.050 us | 0.38x |
| `literal/sherlock-en` | 31.581 us | 85.540 us | 472.746 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,954.861 us | 5,815.608 us | 6,769.473 us | 0.34x |
| `dictionary/single` | 8,324.967 us | 25,672.200 us | 25,887.500 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,439.111 us | 4,830.806 us | 10,148.425 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 165.473 us | 591.385 us | 1,035.716 us | 0.28x |
| `bounded-repeat/capitals` | 11,229.929 us | 45,390.050 us | 52,229.100 us | 0.25x |
| `literal-alternate/sherlock-en` | 338.732 us | 1,490.973 us | 2,059.008 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 128.671 us | 660.513 us | 1,097.582 us | 0.19x |
| `bounded-repeat/context` | 14,516.367 us | 149,532.700 us | 154,111.700 us | 0.10x |
| `ruff-noqa/real` | 3,441.609 us | 69,481.550 us | 102,571.600 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 847.490 us | 23,086.075 us | 28,050.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 128.873 us | 12,061.300 us | 12,820.867 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.241 us | 0.109 us | 0.138 us | 2.20x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.215 us | 0.109 us | 0.143 us | 1.97x |
| `industry/mariomka-uri-count` | `Count` | 3,294.922 us | 1,869.844 us | 10,609.233 us | 1.76x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.133 us | 0.076 us | 0.090 us | 1.75x |
| `common/uri-match` | `IsMatch` | 0.408 us | 0.235 us | 0.272 us | 1.74x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.135 us | 0.081 us | 0.097 us | 1.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.134 us | 0.084 us | 0.101 us | 1.60x |
| `common/email-match` | `IsMatch` | 0.360 us | 0.269 us | 0.299 us | 1.34x |
| `common/ip-match` | `IsMatch` | 0.231 us | 0.176 us | 0.197 us | 1.31x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.189 us | 0.167 us | 0.185 us | 1.13x |
| `common/uri-miss` | `IsMatch` | 0.191 us | 0.187 us | 0.206 us | 1.03x |
| `industry/mariomka-email-count` | `Count` | 453.551 us | 504.100 us | 8,957.450 us | 0.90x |
| `common/ip-miss` | `IsMatch` | 0.654 us | 0.744 us | 0.764 us | 0.88x |
| `common/date-miss` | `IsMatch` | 0.481 us | 0.551 us | 0.584 us | 0.87x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 151.713 us | 269.340 us | 509.401 us | 0.56x |
| `industry/leipzig-twain-count` | `Count` | 628.907 us | 1,317.223 us | 8,449.820 us | 0.48x |
| `common/match-word` | `Match` | 0.060 us | 0.168 us | 1.180 us | 0.35x |
| `common/matches-set` | `Count` | 62.161 us | 191.454 us | 194.610 us | 0.32x |
| `common/matches-word` | `Count` | 1.237 us | 4.205 us | 5.965 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,268.803 us | 8,348.780 us | 16,973.133 us | 0.27x |
| `common/replace-words` | `Replace` | 20.925 us | 77.385 us | 77.865 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.007 us | 955.795 us | 1,258.868 us | 0.18x |
| `common/split-words` | `Split` | 13.217 us | 77.369 us | 79.133 us | 0.17x |
| `common/matches-boundary` | `Count` | 24.806 us | 170.070 us | 171.288 us | 0.15x |
| `common/matches-words` | `Count` | 9.124 us | 69.713 us | 71.643 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,024.307 us | 9,556.538 us | 16,683.867 us | 0.11x |
| `common/email-miss` | `IsMatch` | 0.037 us | 0.348 us | 0.374 us | 0.11x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,185.795 us | 11,723.000 us | 12,229.843 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,929.026 us | 19,534.550 us | 20,144.675 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.048 us | 0.625 us | 0.667 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.057 us | 0.825 us | 0.865 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,836.080 us | 31,815.933 us | 39,275.400 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 990.916 us | 22,558.725 us | 29,829.550 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 317.282 us | 8,482.000 us | 8,925.433 us | 0.04x |
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
| `aws-keys/full` | 61,303.700 us | 18,535.400 us | 51,271.067 us | 3.31x |
| `literal-alternate/sherlock-zh` | 44.178 us | 36.486 us | 2,141.083 us | 1.21x |
| `literal/sherlock-casei-en` | 93.745 us | 78.047 us | 454.627 us | 1.20x |
| `aws-keys/quick` | 3,700.271 us | 3,643.610 us | 38,711.550 us | 1.02x |
| `bounded-repeat/letters-ru` | 1,946.013 us | 2,062.644 us | 2,959.344 us | 0.94x |
| `literal-alternate/sherlock-en-mixed` | 167.121 us | 178.564 us | 570.139 us | 0.94x |
| `literal/sherlock-zh` | 22.264 us | 24.005 us | 2,164.474 us | 0.93x |
| `literal-alternate/sherlock-en` | 338.038 us | 372.611 us | 789.580 us | 0.91x |
| `literal-alternate/sherlock-casei-en` | 128.239 us | 145.669 us | 607.105 us | 0.88x |
| `ruff-noqa/tweaked` | 4,021.368 us | 4,585.600 us | 41,001.100 us | 0.88x |
| `bounded-repeat/capitals` | 11,271.386 us | 13,584.483 us | 20,665.750 us | 0.83x |
| `literal/sherlock-ru` | 69.883 us | 89.342 us | 5,304.036 us | 0.78x |
| `literal/sherlock-casei-ru` | 150.196 us | 224.778 us | 5,270.393 us | 0.67x |
| `literal-alternate/sherlock-en-nomatch` | 128.769 us | 201.557 us | 600.491 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,435.275 us | 2,333.409 us | 7,551.183 us | 0.62x |
| `bounded-repeat/letters-en` | 1,105.738 us | 1,823.812 us | 1,961.941 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 855.702 us | 1,944.044 us | 7,088.400 us | 0.44x |
| `literal/sherlock-en` | 31.715 us | 74.769 us | 417.076 us | 0.42x |
| `bounded-repeat/context` | 14,383.000 us | 69,622.200 us | 73,301.250 us | 0.21x |
| `dictionary/single` | 8,334.440 us | 45,911.300 us | 45,859.900 us | 0.18x |
| `ruff-noqa/real` | 3,433.855 us | 21,124.100 us | 55,977.100 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.239 us | 0.044 us | 0.073 us | 5.46x |
| `common/ip-match` | `IsMatch` | 0.228 us | 0.048 us | 0.068 us | 4.71x |
| `common/uri-match` | `IsMatch` | 0.405 us | 0.087 us | 0.121 us | 4.64x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.139 us | 0.031 us | 0.048 us | 4.50x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.135 us | 0.030 us | 0.049 us | 4.50x |
| `common/email-match` | `IsMatch` | 0.358 us | 0.081 us | 0.109 us | 4.44x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.212 us | 0.048 us | 0.085 us | 4.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.136 us | 0.035 us | 0.052 us | 3.91x |
| `common/ip-miss` | `IsMatch` | 0.651 us | 0.180 us | 0.205 us | 3.62x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.191 us | 0.055 us | 0.076 us | 3.45x |
| `industry/mariomka-uri-count` | `Count` | 3,319.313 us | 1,110.899 us | 8,394.267 us | 2.99x |
| `common/uri-miss` | `IsMatch` | 0.192 us | 0.068 us | 0.085 us | 2.84x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 151.422 us | 61.111 us | 273.369 us | 2.48x |
| `common/date-miss` | `IsMatch` | 0.479 us | 0.198 us | 0.237 us | 2.42x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.044 us | 0.073 us | 1.29x |
| `industry/leipzig-river-window-count` | `Count` | 1,847.656 us | 1,503.327 us | 8,280.300 us | 1.23x |
| `common/replace-words` | `Replace` | 21.082 us | 17.634 us | 20.695 us | 1.20x |
| `industry/mariomka-email-count` | `Count` | 455.110 us | 470.314 us | 7,646.250 us | 0.97x |
| `common/matches-words` | `Count` | 9.146 us | 10.835 us | 13.996 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 994.047 us | 1,204.419 us | 7,075.260 us | 0.83x |
| `common/match-word` | `Match` | 0.060 us | 0.080 us | 0.991 us | 0.76x |
| `industry/mariomka-ip-count` | `Count` | 2,270.638 us | 3,490.159 us | 10,878.433 us | 0.65x |
| `common/split-words` | `Split` | 13.123 us | 20.630 us | 22.442 us | 0.64x |
| `common/matches-set` | `Count` | 61.936 us | 105.781 us | 107.510 us | 0.59x |
| `common/matches-word` | `Count` | 1.235 us | 2.322 us | 3.722 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 627.226 us | 1,301.798 us | 6,949.214 us | 0.48x |
| `common/one-node-backtracking` | `IsMatch` | 0.048 us | 0.107 us | 0.129 us | 0.45x |
| `common/email-miss` | `IsMatch` | 0.038 us | 0.090 us | 0.117 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 1,043.790 us | 3,022.296 us | 8,133.450 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.295 us | 585.500 us | 835.546 us | 0.29x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,184.103 us | 4,733.387 us | 5,147.140 us | 0.25x |
| `common/matches-boundary` | `Count` | 24.565 us | 105.115 us | 107.695 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,937.972 us | 8,849.467 us | 9,402.350 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 316.095 us | 3,926.490 us | 4,304.328 us | 0.08x |
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
| `lokad/style/cell-ref` | 6.982 us | 2.759 us | 2.882 us | 2.53x |
| `lokad/docs/inline-doc-prefix` | 11.562 us | 4.760 us | 5.345 us | 2.43x |
| `lokad/style/range-ref` | 6.236 us | 2.580 us | 2.707 us | 2.42x |
| `fallback/lookahead` | 12.508 us | 5.917 us | 18.615 us | 2.11x |
| `lokad/langserv/identifier-validator` | 25.430 us | 12.626 us | 14.153 us | 2.01x |
| `lokad/lexer/number` | 27.281 us | 14.349 us | 15.344 us | 1.90x |
| `structural/method-family-generic-call` | 11.884 us | 6.328 us | 20.061 us | 1.88x |
| `lokad/lexer/doc-line` | 5.710 us | 3.102 us | 3.817 us | 1.84x |
| `lokad/lexer/identifier` | 41.400 us | 22.740 us | 25.642 us | 1.82x |
| `lokad/style/hex-color` | 1.411 us | 0.822 us | 0.874 us | 1.72x |
| `lokad/lexer/string` | 159.741 us | 96.235 us | 100.170 us | 1.66x |
| `lokad/langserv/helper-identifier` | 11.794 us | 7.212 us | 7.780 us | 1.64x |
| `lokad/langserv/color-rgb-hex` | 4.553 us | 2.946 us | 3.017 us | 1.55x |
| `lokad/langserv/color-short-hex` | 2.013 us | 1.344 us | 1.459 us | 1.50x |
| `lokad/imports/module-imports` | 9.300 us | 6.329 us | 31.355 us | 1.47x |
| `lokad/lexer/operator-run` | 4.975 us | 3.484 us | 3.979 us | 1.43x |
| `structural/method-family-call` | 10.957 us | 8.141 us | 21.418 us | 1.35x |
| `lokad/langserv/url-dashboard` | 31.029 us | 24.045 us | 24.617 us | 1.29x |
| `structural/ordered-keyword-window` | 21.197 us | 17.776 us | 31.031 us | 1.19x |
| `lokad/langserv/url-download` | 48.123 us | 51.399 us | 51.790 us | 0.94x |
| `structural/keyword-to-generic-type` | 3.131 us | 4.119 us | 17.825 us | 0.76x |
| `structural/ordered-keyword-pair` | 3.520 us | 5.358 us | 18.782 us | 0.66x |
| `literal/identifier-token` | 2.740 us | 4.311 us | 17.214 us | 0.64x |
| `structural/ordered-modifier-pair` | 10.125 us | 17.322 us | 30.864 us | 0.58x |
| `structural/ordered-identifier-window` | 15.159 us | 28.504 us | 41.202 us | 0.53x |
| `literal/call-token` | 1.869 us | 4.185 us | 17.323 us | 0.45x |
| `structural/modifier-family-to-type-window` | 104.367 us | 251.528 us | 267.125 us | 0.41x |
| `literal-family/method-token-family` | 3.809 us | 9.208 us | 22.105 us | 0.41x |
| `literal-family/type-token-family` | 9.708 us | 23.555 us | 36.457 us | 0.41x |
| `balancing/begin-end-blocks` | 1,471.971 us | 4,261.878 us | 4,338.978 us | 0.35x |
| `literal/identifier-token-casei` | 3.462 us | 15.002 us | 28.796 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 20.439 us | 137.792 us | 152.058 us | 0.15x |
| `lokad/folding/region-marker` | 5.156 us | 35.333 us | 63.005 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 26.852 us | 217.763 us | 233.490 us | 0.12x |
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
| `lokad/docs/inline-doc-prefix` | 11.591 us | 2.646 us | 3.435 us | 4.38x |
| `lokad/style/range-ref` | 6.201 us | 1.430 us | 1.526 us | 4.34x |
| `lokad/langserv/url-dashboard` | 31.082 us | 7.372 us | 7.904 us | 4.22x |
| `lokad/style/cell-ref` | 6.990 us | 1.701 us | 1.876 us | 4.11x |
| `lokad/lexer/number` | 27.494 us | 7.031 us | 8.209 us | 3.91x |
| `lokad/langserv/url-download` | 47.582 us | 12.757 us | 13.398 us | 3.73x |
| `structural/keyword-family-to-capitalized-identifier` | 26.767 us | 7.802 us | 21.640 us | 3.43x |
| `lokad/lexer/string` | 157.497 us | 46.385 us | 48.734 us | 3.40x |
| `balancing/begin-end-blocks` | 1,476.688 us | 460.569 us | 489.101 us | 3.21x |
| `fallback/lookahead` | 12.513 us | 4.158 us | 16.492 us | 3.01x |
| `lokad/style/hex-color` | 1.421 us | 0.510 us | 0.590 us | 2.79x |
| `lokad/langserv/identifier-validator` | 25.285 us | 9.265 us | 11.111 us | 2.73x |
| `structural/method-family-generic-call` | 11.941 us | 4.553 us | 17.239 us | 2.62x |
| `lokad/langserv/helper-identifier` | 11.789 us | 4.561 us | 5.770 us | 2.58x |
| `lokad/lexer/doc-line` | 5.737 us | 2.310 us | 3.064 us | 2.48x |
| `structural/ordered-keyword-window` | 21.197 us | 8.560 us | 21.694 us | 2.48x |
| `lokad/langserv/color-rgb-hex` | 4.569 us | 1.846 us | 2.012 us | 2.48x |
| `lokad/lexer/identifier` | 41.164 us | 16.676 us | 20.316 us | 2.47x |
| `lokad/lexer/operator-run` | 4.978 us | 2.030 us | 2.686 us | 2.45x |
| `structural/method-family-call` | 11.016 us | 5.080 us | 17.891 us | 2.17x |
| `lokad/imports/module-imports` | 9.265 us | 4.433 us | 28.275 us | 2.09x |
| `lokad/langserv/color-short-hex` | 2.026 us | 0.981 us | 1.190 us | 2.07x |
| `structural/modifier-family-to-type-pair` | 20.288 us | 10.491 us | 23.397 us | 1.93x |
| `structural/modifier-family-to-type-window` | 104.339 us | 55.524 us | 69.214 us | 1.88x |
| `structural/ordered-identifier-window` | 15.178 us | 10.610 us | 23.752 us | 1.43x |
| `structural/ordered-modifier-pair` | 10.203 us | 8.529 us | 21.561 us | 1.20x |
| `structural/keyword-to-generic-type` | 3.120 us | 3.436 us | 16.796 us | 0.91x |
| `literal-family/type-token-family` | 9.791 us | 11.371 us | 24.216 us | 0.86x |
| `structural/ordered-keyword-pair` | 3.513 us | 4.087 us | 17.216 us | 0.86x |
| `literal-family/method-token-family` | 3.829 us | 5.290 us | 17.813 us | 0.72x |
| `literal/identifier-token-casei` | 3.460 us | 5.863 us | 19.476 us | 0.59x |
| `literal/identifier-token` | 2.718 us | 4.660 us | 16.727 us | 0.58x |
| `literal/call-token` | 1.820 us | 3.923 us | 16.509 us | 0.46x |
| `lokad/folding/region-marker` | 5.155 us | 35.955 us | 61.955 us | 0.14x |
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
