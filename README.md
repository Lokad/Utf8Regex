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
| `literal/sherlock-zh` | 22.170 us | 18.488 us | 2,142.986 us | 1.20x |
| `aws-keys/full` | 62,703.150 us | 86,262.500 us | 116,833.900 us | 0.73x |
| `literal/sherlock-ru` | 70.964 us | 98.241 us | 5,398.407 us | 0.72x |
| `literal-alternate/sherlock-zh` | 43.310 us | 67.764 us | 2,201.606 us | 0.64x |
| `literal/sherlock-casei-en` | 103.905 us | 187.499 us | 602.005 us | 0.55x |
| `ruff-noqa/tweaked` | 3,827.590 us | 7,939.491 us | 45,930.450 us | 0.48x |
| `literal/sherlock-casei-ru` | 151.582 us | 361.194 us | 5,506.429 us | 0.42x |
| `bounded-repeat/letters-en` | 1,106.833 us | 2,837.226 us | 2,955.242 us | 0.39x |
| `aws-keys/quick` | 3,856.785 us | 9,889.850 us | 47,346.800 us | 0.39x |
| `literal/sherlock-en` | 31.369 us | 85.388 us | 484.805 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,009.332 us | 5,861.346 us | 6,700.800 us | 0.34x |
| `dictionary/single` | 8,266.550 us | 26,085.967 us | 26,258.167 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,438.385 us | 4,732.206 us | 9,882.275 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 156.863 us | 596.934 us | 1,021.676 us | 0.26x |
| `literal-alternate/sherlock-en` | 347.040 us | 1,498.845 us | 2,084.437 us | 0.23x |
| `bounded-repeat/capitals` | 9,162.644 us | 45,319.250 us | 52,515.500 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 127.937 us | 645.815 us | 1,067.197 us | 0.20x |
| `bounded-repeat/context` | 20,185.950 us | 148,687.200 us | 151,182.300 us | 0.14x |
| `ruff-noqa/real` | 3,318.170 us | 70,639.800 us | 87,120.400 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 860.034 us | 22,937.050 us | 27,951.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 128.262 us | 11,076.614 us | 12,056.114 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.322 us | 0.077 us | 0.090 us | 4.17x |
| `common/ip-match` | `IsMatch` | 0.662 us | 0.174 us | 0.193 us | 3.80x |
| `common/date-match` | `IsMatch` | 0.417 us | 0.111 us | 0.140 us | 3.77x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.305 us | 0.084 us | 0.101 us | 3.64x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.294 us | 0.082 us | 0.099 us | 3.58x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.393 us | 0.111 us | 0.147 us | 3.55x |
| `common/email-match` | `IsMatch` | 0.747 us | 0.268 us | 0.300 us | 2.79x |
| `common/uri-match` | `IsMatch` | 0.620 us | 0.231 us | 0.269 us | 2.68x |
| `common/date-miss` | `IsMatch` | 1.437 us | 0.561 us | 0.602 us | 2.56x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.354 us | 0.172 us | 0.189 us | 2.06x |
| `industry/mariomka-uri-count` | `Count` | 3,181.138 us | 1,896.293 us | 10,475.700 us | 1.68x |
| `common/ip-miss` | `IsMatch` | 1.113 us | 0.709 us | 0.738 us | 1.57x |
| `common/uri-miss` | `IsMatch` | 0.197 us | 0.186 us | 0.207 us | 1.06x |
| `common/match-word` | `Match` | 0.160 us | 0.172 us | 1.202 us | 0.93x |
| `industry/mariomka-email-count` | `Count` | 451.019 us | 495.093 us | 9,005.850 us | 0.91x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 146.341 us | 264.366 us | 519.322 us | 0.55x |
| `industry/leipzig-twain-count` | `Count` | 656.716 us | 1,375.777 us | 8,722.680 us | 0.48x |
| `common/matches-set` | `Count` | 62.014 us | 193.058 us | 194.774 us | 0.32x |
| `common/backtracking` | `IsMatch` | 0.253 us | 0.830 us | 0.875 us | 0.30x |
| `common/matches-word` | `Count` | 1.270 us | 4.273 us | 5.950 us | 0.30x |
| `common/replace-words` | `Replace` | 22.255 us | 77.830 us | 79.384 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,269.291 us | 8,336.780 us | 16,879.033 us | 0.27x |
| `common/split-words` | `Split` | 14.820 us | 78.282 us | 80.315 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.868 us | 964.537 us | 1,230.857 us | 0.18x |
| `common/matches-boundary` | `Count` | 25.264 us | 166.086 us | 167.893 us | 0.15x |
| `common/matches-words` | `Count` | 9.344 us | 69.322 us | 71.551 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.038 us | 0.352 us | 0.376 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 1,036.660 us | 9,756.987 us | 17,027.900 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,939.957 us | 19,330.200 us | 19,904.375 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,195.841 us | 11,962.300 us | 12,590.500 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.048 us | 0.613 us | 0.648 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,749.781 us | 31,611.300 us | 39,158.600 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 1,002.607 us | 22,948.675 us | 30,403.800 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 305.549 us | 8,404.167 us | 8,937.989 us | 0.04x |
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
| `aws-keys/full` | 62,993.650 us | 18,765.040 us | 50,707.933 us | 3.36x |
| `literal/sherlock-casei-en` | 104.791 us | 78.311 us | 446.538 us | 1.34x |
| `literal-alternate/sherlock-zh` | 43.702 us | 35.673 us | 2,132.385 us | 1.23x |
| `aws-keys/quick` | 3,834.755 us | 3,677.995 us | 40,581.100 us | 1.04x |
| `bounded-repeat/letters-ru` | 2,011.179 us | 2,075.057 us | 2,901.508 us | 0.97x |
| `literal-alternate/sherlock-en` | 347.319 us | 365.634 us | 771.085 us | 0.95x |
| `literal/sherlock-zh` | 22.161 us | 23.621 us | 2,149.458 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 131.320 us | 143.939 us | 567.735 us | 0.91x |
| `literal-alternate/sherlock-en-mixed` | 157.567 us | 178.848 us | 552.631 us | 0.88x |
| `ruff-noqa/tweaked` | 3,852.915 us | 4,586.188 us | 41,049.900 us | 0.84x |
| `literal/sherlock-ru` | 70.715 us | 89.896 us | 5,250.727 us | 0.79x |
| `literal/sherlock-casei-ru` | 153.890 us | 220.068 us | 5,251.473 us | 0.70x |
| `bounded-repeat/capitals` | 9,193.644 us | 13,487.283 us | 21,261.500 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 128.822 us | 203.787 us | 570.829 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,442.737 us | 2,341.379 us | 7,457.233 us | 0.62x |
| `bounded-repeat/letters-en` | 1,112.326 us | 1,839.210 us | 1,933.226 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 858.179 us | 1,945.490 us | 7,038.725 us | 0.44x |
| `literal/sherlock-en` | 31.509 us | 75.203 us | 454.369 us | 0.42x |
| `bounded-repeat/context` | 20,086.125 us | 69,312.500 us | 72,996.800 us | 0.29x |
| `dictionary/single` | 8,231.644 us | 45,684.100 us | 45,364.850 us | 0.18x |
| `ruff-noqa/real` | 3,349.204 us | 25,763.150 us | 36,558.000 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.662 us | 0.049 us | 0.069 us | 13.60x |
| `common/date-match` | `IsMatch` | 0.415 us | 0.043 us | 0.073 us | 9.67x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.292 us | 0.031 us | 0.048 us | 9.35x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.291 us | 0.032 us | 0.049 us | 9.24x |
| `common/email-match` | `IsMatch` | 0.748 us | 0.081 us | 0.107 us | 9.18x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.320 us | 0.037 us | 0.053 us | 8.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.396 us | 0.048 us | 0.087 us | 8.25x |
| `common/date-miss` | `IsMatch` | 1.435 us | 0.200 us | 0.238 us | 7.19x |
| `common/uri-match` | `IsMatch` | 0.621 us | 0.088 us | 0.121 us | 7.02x |
| `common/ip-miss` | `IsMatch` | 1.106 us | 0.180 us | 0.203 us | 6.16x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.337 us | 0.056 us | 0.076 us | 6.07x |
| `common/backtracking` | `IsMatch` | 0.249 us | 0.044 us | 0.074 us | 5.71x |
| `industry/mariomka-uri-count` | `Count` | 3,186.688 us | 1,112.320 us | 8,450.667 us | 2.86x |
| `common/uri-miss` | `IsMatch` | 0.196 us | 0.070 us | 0.087 us | 2.79x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 146.093 us | 60.725 us | 264.840 us | 2.41x |
| `common/match-word` | `Match` | 0.159 us | 0.083 us | 1.019 us | 1.93x |
| `common/replace-words` | `Replace` | 22.280 us | 18.232 us | 19.720 us | 1.22x |
| `industry/leipzig-river-window-count` | `Count` | 1,755.802 us | 1,532.030 us | 7,791.520 us | 1.15x |
| `industry/mariomka-email-count` | `Count` | 449.629 us | 461.776 us | 7,653.475 us | 0.97x |
| `common/matches-words` | `Count` | 9.323 us | 11.240 us | 14.530 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 1,005.581 us | 1,229.633 us | 7,174.300 us | 0.82x |
| `common/split-words` | `Split` | 14.817 us | 21.178 us | 22.861 us | 0.70x |
| `industry/mariomka-ip-count` | `Count` | 2,280.529 us | 3,486.777 us | 10,875.800 us | 0.65x |
| `common/matches-set` | `Count` | 62.490 us | 104.745 us | 106.488 us | 0.60x |
| `common/one-node-backtracking` | `IsMatch` | 0.049 us | 0.089 us | 0.129 us | 0.55x |
| `common/matches-word` | `Count` | 1.265 us | 2.353 us | 3.740 us | 0.54x |
| `industry/leipzig-twain-count` | `Count` | 662.220 us | 1,333.255 us | 7,575.344 us | 0.50x |
| `common/email-miss` | `IsMatch` | 0.039 us | 0.092 us | 0.118 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 1,038.958 us | 3,064.628 us | 8,313.300 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.417 us | 601.587 us | 857.624 us | 0.28x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,194.931 us | 4,809.413 us | 5,269.240 us | 0.25x |
| `common/matches-boundary` | `Count` | 25.172 us | 106.438 us | 107.595 us | 0.24x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,926.185 us | 8,768.600 us | 9,349.462 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 308.511 us | 3,988.753 us | 4,369.561 us | 0.08x |
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
| `lokad/docs/inline-doc-prefix` | 14.003 us | 4.845 us | 5.540 us | 2.89x |
| `lokad/style/cell-ref` | 8.007 us | 2.837 us | 3.119 us | 2.82x |
| `lokad/lexer/string` | 249.569 us | 96.914 us | 102.904 us | 2.58x |
| `lokad/style/range-ref` | 6.711 us | 2.629 us | 2.771 us | 2.55x |
| `lokad/langserv/identifier-validator` | 31.187 us | 12.659 us | 14.291 us | 2.46x |
| `structural/method-family-generic-call` | 15.967 us | 6.666 us | 19.982 us | 2.40x |
| `lokad/lexer/number` | 34.195 us | 14.328 us | 15.406 us | 2.39x |
| `structural/ordered-modifier-pair` | 40.983 us | 17.305 us | 30.821 us | 2.37x |
| `lokad/lexer/doc-line` | 6.923 us | 3.105 us | 3.830 us | 2.23x |
| `lokad/lexer/identifier` | 50.260 us | 23.163 us | 26.425 us | 2.17x |
| `lokad/langserv/helper-identifier` | 15.931 us | 7.405 us | 8.189 us | 2.15x |
| `lokad/style/hex-color` | 1.747 us | 0.825 us | 0.899 us | 2.12x |
| `lokad/langserv/color-short-hex` | 2.871 us | 1.357 us | 1.496 us | 2.12x |
| `lokad/lexer/operator-run` | 7.002 us | 3.431 us | 3.884 us | 2.04x |
| `structural/method-family-call` | 16.214 us | 8.118 us | 21.004 us | 2.00x |
| `fallback/lookahead` | 11.808 us | 6.023 us | 18.754 us | 1.96x |
| `lokad/langserv/color-rgb-hex` | 5.028 us | 2.979 us | 3.043 us | 1.69x |
| `structural/ordered-keyword-pair` | 8.984 us | 5.330 us | 19.114 us | 1.69x |
| `lokad/imports/module-imports` | 9.139 us | 6.517 us | 31.357 us | 1.40x |
| `structural/keyword-to-generic-type` | 5.374 us | 4.064 us | 17.832 us | 1.32x |
| `lokad/langserv/url-dashboard` | 30.794 us | 25.158 us | 25.645 us | 1.22x |
| `structural/ordered-keyword-window` | 20.197 us | 17.784 us | 31.435 us | 1.14x |
| `lokad/langserv/url-download` | 52.504 us | 51.832 us | 52.258 us | 1.01x |
| `literal/identifier-token` | 2.765 us | 4.370 us | 17.487 us | 0.63x |
| `structural/ordered-identifier-window` | 14.992 us | 28.871 us | 42.729 us | 0.52x |
| `literal/call-token` | 1.964 us | 4.309 us | 17.581 us | 0.46x |
| `structural/modifier-family-to-type-window` | 114.688 us | 254.200 us | 269.520 us | 0.45x |
| `structural/modifier-family-to-type-pair` | 62.496 us | 139.800 us | 153.216 us | 0.45x |
| `literal-family/method-token-family` | 3.833 us | 9.188 us | 22.147 us | 0.42x |
| `literal-family/type-token-family` | 9.806 us | 23.534 us | 37.291 us | 0.42x |
| `balancing/begin-end-blocks` | 1,586.492 us | 4,225.061 us | 4,197.705 us | 0.38x |
| `structural/keyword-family-to-capitalized-identifier` | 65.983 us | 215.765 us | 231.487 us | 0.31x |
| `literal/identifier-token-casei` | 4.083 us | 15.524 us | 28.551 us | 0.26x |
| `lokad/folding/region-marker` | 4.919 us | 35.503 us | 63.890 us | 0.14x |
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
| `structural/keyword-family-to-capitalized-identifier` | 65.788 us | 7.680 us | 20.910 us | 8.57x |
| `structural/modifier-family-to-type-pair` | 62.170 us | 10.614 us | 23.733 us | 5.86x |
| `lokad/lexer/string` | 247.492 us | 47.030 us | 50.892 us | 5.26x |
| `lokad/docs/inline-doc-prefix` | 13.977 us | 2.841 us | 3.762 us | 4.92x |
| `structural/ordered-modifier-pair` | 41.161 us | 8.657 us | 22.080 us | 4.75x |
| `lokad/lexer/number` | 34.005 us | 7.492 us | 8.610 us | 4.54x |
| `lokad/style/cell-ref` | 8.016 us | 1.811 us | 2.020 us | 4.43x |
| `lokad/style/range-ref` | 6.700 us | 1.536 us | 1.657 us | 4.36x |
| `lokad/langserv/url-download` | 51.824 us | 12.933 us | 13.634 us | 4.01x |
| `lokad/langserv/url-dashboard` | 30.351 us | 7.624 us | 8.071 us | 3.98x |
| `structural/method-family-generic-call` | 15.874 us | 4.573 us | 16.950 us | 3.47x |
| `balancing/begin-end-blocks` | 1,582.806 us | 468.388 us | 498.717 us | 3.38x |
| `lokad/lexer/operator-run` | 6.972 us | 2.066 us | 2.708 us | 3.37x |
| `lokad/style/hex-color` | 1.768 us | 0.527 us | 0.636 us | 3.35x |
| `lokad/langserv/identifier-validator` | 31.305 us | 9.414 us | 11.404 us | 3.33x |
| `lokad/langserv/helper-identifier` | 15.748 us | 4.813 us | 5.957 us | 3.27x |
| `structural/method-family-call` | 16.232 us | 5.044 us | 17.593 us | 3.22x |
| `lokad/lexer/identifier` | 50.133 us | 16.738 us | 20.908 us | 3.00x |
| `lokad/lexer/doc-line` | 6.931 us | 2.348 us | 3.062 us | 2.95x |
| `fallback/lookahead` | 11.861 us | 4.202 us | 16.573 us | 2.82x |
| `lokad/langserv/color-rgb-hex` | 5.002 us | 1.786 us | 2.019 us | 2.80x |
| `lokad/langserv/color-short-hex` | 2.849 us | 1.048 us | 1.211 us | 2.72x |
| `structural/ordered-keyword-window` | 20.387 us | 8.580 us | 21.856 us | 2.38x |
| `structural/ordered-keyword-pair` | 8.995 us | 4.148 us | 17.136 us | 2.17x |
| `structural/modifier-family-to-type-window` | 115.816 us | 55.863 us | 69.875 us | 2.07x |
| `lokad/imports/module-imports` | 9.174 us | 4.426 us | 28.178 us | 2.07x |
| `structural/keyword-to-generic-type` | 5.385 us | 3.497 us | 16.761 us | 1.54x |
| `structural/ordered-identifier-window` | 14.972 us | 10.642 us | 24.348 us | 1.41x |
| `literal-family/type-token-family` | 9.809 us | 11.470 us | 24.599 us | 0.86x |
| `literal-family/method-token-family` | 3.828 us | 5.346 us | 17.974 us | 0.72x |
| `literal/identifier-token-casei` | 4.096 us | 5.936 us | 19.311 us | 0.69x |
| `literal/identifier-token` | 2.754 us | 4.726 us | 16.932 us | 0.58x |
| `literal/call-token` | 1.970 us | 3.985 us | 16.658 us | 0.49x |
| `lokad/folding/region-marker` | 4.917 us | 37.470 us | 63.253 us | 0.13x |
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
