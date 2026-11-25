# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes the upcoming 0.5.0 API.

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
`ReadOnlyMemory<byte>` for groups and capture history. Use match ranges directly
with UTF-8 slices. `EnumerateMatches(input, startOffsetInBytes, cancellationToken)` searches
from a scalar-aligned byte position while retaining full context for assertions.
The byte-start `Match` can fill caller-owned `Utf8CaptureRange` slots; slot zero
is the whole match and `GroupNumberFromCaptureSlot` resolves sparse group numbers.
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

See the [release notes](CHANGELOG.md) for published versions and 0.5.0 upgrades.

## Runtime support

The 0.5.0 development branch targets `net10.0` and `net11.0`. .NET 11
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The measured operations start with valid, unchanged UTF-8 and include the required
result materialization. The Core summary records the measured sources, runtimes,
qualification and snapshot identity. The benchmark guide covers ordinary patterns
and calls with finite limits, deadlines and cancellation.
[Core summary](bench/Snapshots/Core.json) · [Benchmark guide](docs/BENCHMARKS.md)

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.
The `common/match-word` and `lokad/docs/inline-doc-prefix` rows compare the
first whole-match range on both sides. The other Lokad prefix-loop rows from
the dev.117 refresh include .NET's `Match` object allocation.

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
| `literal/sherlock-zh` | 22.050 us | 18.562 us | 2,147.671 us | 1.19x |
| `literal/sherlock-ru` | 67.902 us | 98.168 us | 5,374.057 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.988 us | 67.739 us | 2,188.474 us | 0.63x |
| `ruff-noqa/tweaked` | 4,121.621 us | 7,897.460 us | 41,860.550 us | 0.52x |
| `literal/sherlock-casei-ru` | 150.174 us | 364.330 us | 5,488.057 us | 0.41x |
| `bounded-repeat/letters-en` | 1,107.542 us | 2,809.178 us | 2,986.468 us | 0.39x |
| `literal/sherlock-en` | 31.967 us | 85.269 us | 469.733 us | 0.37x |
| `literal/sherlock-casei-en` | 69.112 us | 190.731 us | 579.438 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,112.694 us | 5,860.469 us | 6,772.375 us | 0.36x |
| `dictionary/single` | 8,162.510 us | 25,965.600 us | 26,287.333 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,446.221 us | 4,786.688 us | 9,885.175 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 157.436 us | 591.630 us | 1,021.926 us | 0.27x |
| `bounded-repeat/capitals` | 11,711.386 us | 44,712.950 us | 48,712.100 us | 0.26x |
| `literal-alternate/sherlock-en` | 335.301 us | 1,527.946 us | 2,046.600 us | 0.22x |
| `aws-keys/quick` | 2,014.274 us | 9,273.100 us | 46,230.050 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 127.979 us | 674.458 us | 1,112.126 us | 0.19x |
| `aws-keys/full` | 8,672.413 us | 86,285.700 us | 121,994.900 us | 0.10x |
| `bounded-repeat/context` | 8,902.022 us | 147,569.900 us | 152,613.600 us | 0.06x |
| `ruff-noqa/real` | 3,458.004 us | 69,915.750 us | 86,129.700 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 836.800 us | 22,869.900 us | 27,686.700 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 103.289 us | 11,677.000 us | 12,429.371 us | 0.0088x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 381.152 us | 492.064 us | 8,958.000 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.073 us | 0.109 us | 0.144 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.109 us | 0.138 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,179.581 us | 1,898.060 us | 10,399.850 us | 0.62x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.076 us | 0.090 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.084 us | 0.099 us | 0.57x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.082 us | 0.097 us | 0.50x |
| `industry/leipzig-twain-count` | `Count` | 628.980 us | 1,314.118 us | 8,514.200 us | 0.48x |
| `common/email-match` | `IsMatch` | 0.127 us | 0.268 us | 0.296 us | 0.48x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.171 us | 0.193 us | 0.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.073 us | 0.166 us | 0.184 us | 0.44x |
| `common/match-word` | `Match` | 0.050 us | 0.127 us | 1.147 us | 0.39x |
| `common/uri-miss` | `IsMatch` | 0.079 us | 0.203 us | 0.224 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,173.071 us | 8,466.200 us | 16,780.900 us | 0.37x |
| `common/matches-set` | `Count` | 67.157 us | 194.725 us | 196.091 us | 0.34x |
| `common/uri-match` | `IsMatch` | 0.076 us | 0.229 us | 0.266 us | 0.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 83.165 us | 253.641 us | 516.996 us | 0.33x |
| `common/ip-miss` | `IsMatch` | 0.223 us | 0.718 us | 0.737 us | 0.31x |
| `common/matches-word` | `Count` | 1.233 us | 4.219 us | 5.928 us | 0.29x |
| `common/replace-words` | `Replace` | 20.441 us | 77.510 us | 80.160 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.107 us | 0.557 us | 0.594 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.579 us | 955.979 us | 1,227.288 us | 0.18x |
| `common/split-words` | `Split` | 13.220 us | 77.712 us | 79.442 us | 0.17x |
| `common/matches-boundary` | `Count` | 26.982 us | 171.448 us | 175.312 us | 0.16x |
| `common/matches-words` | `Count` | 9.148 us | 70.511 us | 71.650 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,051.814 us | 9,973.038 us | 17,277.967 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,844.832 us | 19,466.275 us | 20,044.000 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 886.147 us | 11,865.929 us | 12,434.000 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.614 us | 0.643 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.819 us | 0.855 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.346 us | 0.370 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 981.271 us | 22,778.025 us | 30,467.450 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,258.348 us | 33,477.900 us | 40,918.000 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 273.667 us | 8,588.556 us | 9,097.822 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.531 us | 35.749 us | 2,146.031 us | 1.22x |
| `bounded-repeat/letters-ru` | 2,105.683 us | 2,066.954 us | 2,975.496 us | 1.02x |
| `literal/sherlock-zh` | 22.055 us | 23.800 us | 2,145.900 us | 0.93x |
| `literal-alternate/sherlock-en` | 337.289 us | 366.394 us | 782.179 us | 0.92x |
| `ruff-noqa/tweaked` | 4,155.778 us | 4,566.012 us | 36,683.600 us | 0.91x |
| `literal-alternate/sherlock-en-mixed` | 157.212 us | 174.576 us | 565.183 us | 0.90x |
| `literal/sherlock-casei-en` | 69.067 us | 80.262 us | 463.290 us | 0.86x |
| `bounded-repeat/capitals` | 11,612.771 us | 13,621.200 us | 16,708.675 us | 0.85x |
| `literal/sherlock-ru` | 67.508 us | 88.348 us | 5,248.114 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 103.406 us | 142.970 us | 540.180 us | 0.72x |
| `literal/sherlock-casei-ru` | 147.591 us | 222.463 us | 5,357.840 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 128.043 us | 204.002 us | 608.467 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,442.660 us | 2,326.312 us | 7,456.617 us | 0.62x |
| `bounded-repeat/letters-en` | 1,092.040 us | 1,819.958 us | 1,981.145 us | 0.60x |
| `aws-keys/quick` | 2,006.947 us | 3,672.562 us | 38,715.150 us | 0.55x |
| `aws-keys/full` | 8,742.438 us | 18,659.900 us | 55,263.633 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 840.180 us | 1,945.031 us | 6,952.950 us | 0.43x |
| `literal/sherlock-en` | 32.006 us | 74.887 us | 432.830 us | 0.43x |
| `dictionary/single` | 8,164.160 us | 45,088.100 us | 45,001.550 us | 0.18x |
| `ruff-noqa/real` | 3,442.568 us | 20,873.175 us | 35,343.167 us | 0.16x |
| `bounded-repeat/context` | 8,923.356 us | 69,302.550 us | 72,369.750 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.069 us | 0.042 us | 0.073 us | 1.64x |
| `common/email-match` | `IsMatch` | 0.124 us | 0.080 us | 0.107 us | 1.55x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.073 us | 0.048 us | 0.087 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.047 us | 0.067 us | 1.48x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 80.985 us | 57.972 us | 258.395 us | 1.40x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.044 us | 0.032 us | 0.052 us | 1.37x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.070 us | 0.054 us | 0.076 us | 1.30x |
| `common/uri-miss` | `IsMatch` | 0.079 us | 0.063 us | 0.086 us | 1.25x |
| `common/ip-miss` | `IsMatch` | 0.216 us | 0.178 us | 0.200 us | 1.21x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.041 us | 0.035 us | 0.052 us | 1.17x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.036 us | 0.031 us | 0.048 us | 1.15x |
| `common/replace-words` | `Replace` | 20.420 us | 17.812 us | 19.421 us | 1.15x |
| `industry/mariomka-uri-count` | `Count` | 1,187.138 us | 1,126.596 us | 8,335.325 us | 1.05x |
| `common/match-word` | `Match` | 0.050 us | 0.048 us | 0.929 us | 1.04x |
| `industry/mariomka-ip-count` | `Count` | 3,168.992 us | 3,446.522 us | 10,860.133 us | 0.92x |
| `common/uri-match` | `IsMatch` | 0.076 us | 0.088 us | 0.121 us | 0.87x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.043 us | 0.072 us | 0.86x |
| `common/matches-words` | `Count` | 9.131 us | 10.882 us | 13.931 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 380.914 us | 460.523 us | 7,663.950 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 990.223 us | 1,240.871 us | 7,487.280 us | 0.80x |
| `industry/leipzig-river-window-count` | `Count` | 1,272.675 us | 1,611.061 us | 8,409.875 us | 0.79x |
| `common/split-words` | `Split` | 13.391 us | 20.712 us | 22.424 us | 0.65x |
| `common/matches-set` | `Count` | 67.090 us | 105.502 us | 107.358 us | 0.64x |
| `common/date-miss` | `IsMatch` | 0.108 us | 0.197 us | 0.235 us | 0.55x |
| `common/matches-word` | `Count` | 1.230 us | 2.336 us | 3.646 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 635.537 us | 1,305.007 us | 7,476.960 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,068.203 us | 3,018.200 us | 8,210.025 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.132 us | 599.620 us | 866.358 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.107 us | 0.127 us | 0.27x |
| `common/matches-boundary` | `Count` | 27.007 us | 104.214 us | 105.801 us | 0.26x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,858.476 us | 8,669.000 us | 9,309.925 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 889.299 us | 4,740.175 us | 5,180.807 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.089 us | 0.116 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 276.652 us | 3,898.535 us | 4,292.022 us | 0.07x |
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
| `lokad/imports/module-imports` | 5.889 us | 6.316 us | 30.782 us | 0.93x |
| `fallback/lookahead` | 5.483 us | 5.967 us | 19.360 us | 0.92x |
| `lokad/langserv/identifier-validator` | 6.139 us | 7.389 us | 8.974 us | 0.83x |
| `structural/method-family-generic-call` | 5.378 us | 6.579 us | 20.246 us | 0.82x |
| `structural/method-family-call` | 6.425 us | 8.001 us | 21.216 us | 0.80x |
| `lokad/lexer/doc-line` | 1.536 us | 1.913 us | 2.624 us | 0.80x |
| `lokad/lexer/identifier` | 10.963 us | 13.699 us | 16.699 us | 0.80x |
| `lokad/langserv/helper-identifier` | 3.787 us | 4.758 us | 5.589 us | 0.80x |
| `structural/ordered-keyword-window` | 13.565 us | 17.350 us | 30.553 us | 0.78x |
| `structural/keyword-to-generic-type` | 3.042 us | 4.073 us | 17.665 us | 0.75x |
| `lokad/langserv/color-short-hex` | 0.515 us | 0.832 us | 0.993 us | 0.62x |
| `structural/ordered-keyword-pair` | 3.221 us | 5.298 us | 18.632 us | 0.61x |
| `structural/ordered-modifier-pair` | 10.174 us | 17.503 us | 30.600 us | 0.58x |
| `literal/identifier-token` | 2.278 us | 4.353 us | 17.772 us | 0.52x |
| `lokad/docs/inline-doc-prefix` | 1.742 us | 3.465 us | 4.251 us | 0.50x |
| `lokad/lexer/operator-run` | 1.033 us | 2.322 us | 2.793 us | 0.44x |
| `literal/call-token` | 1.856 us | 4.335 us | 17.466 us | 0.43x |
| `lokad/style/hex-color` | 0.212 us | 0.503 us | 0.595 us | 0.42x |
| `lokad/style/range-ref` | 0.729 us | 1.777 us | 1.934 us | 0.41x |
| `lokad/style/cell-ref` | 0.770 us | 1.896 us | 2.083 us | 0.41x |
| `lokad/langserv/url-dashboard` | 9.666 us | 23.867 us | 24.034 us | 0.41x |
| `literal-family/type-token-family` | 9.180 us | 23.528 us | 37.227 us | 0.39x |
| `lokad/lexer/string` | 31.350 us | 81.206 us | 85.580 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.641 us | 1.777 us | 2.031 us | 0.36x |
| `structural/ordered-identifier-window` | 8.688 us | 28.398 us | 41.612 us | 0.31x |
| `literal-family/method-token-family` | 2.596 us | 9.409 us | 22.076 us | 0.28x |
| `lokad/lexer/number` | 2.817 us | 10.845 us | 11.916 us | 0.26x |
| `structural/modifier-family-to-type-window` | 64.752 us | 251.884 us | 268.620 us | 0.26x |
| `literal/identifier-token-casei` | 3.936 us | 15.316 us | 28.757 us | 0.26x |
| `lokad/langserv/url-download` | 11.642 us | 50.826 us | 51.518 us | 0.23x |
| `lokad/folding/region-marker` | 5.654 us | 35.739 us | 62.052 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.769 us | 221.672 us | 236.108 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.894 us | 126.348 us | 140.753 us | 0.02x |
| `balancing/begin-end-blocks` | 46.174 us | 4,147.556 us | 4,172.895 us | 0.01x |
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
| `structural/ordered-keyword-window` | 14.261 us | 8.543 us | 21.474 us | 1.67x |
| `lokad/langserv/url-dashboard` | 9.694 us | 6.398 us | 6.863 us | 1.52x |
| `lokad/lexer/doc-line` | 1.538 us | 1.050 us | 1.852 us | 1.46x |
| `lokad/langserv/identifier-validator` | 6.111 us | 4.287 us | 5.806 us | 1.43x |
| `lokad/langserv/helper-identifier` | 3.782 us | 2.668 us | 3.561 us | 1.42x |
| `structural/keyword-family-to-capitalized-identifier` | 10.836 us | 7.786 us | 21.401 us | 1.39x |
| `lokad/lexer/identifier` | 10.998 us | 8.010 us | 10.692 us | 1.37x |
| `lokad/imports/module-imports` | 5.827 us | 4.330 us | 28.103 us | 1.35x |
| `fallback/lookahead` | 5.495 us | 4.175 us | 17.078 us | 1.32x |
| `structural/method-family-call` | 6.431 us | 5.023 us | 17.860 us | 1.28x |
| `lokad/docs/inline-doc-prefix` | 1.741 us | 1.426 us | 2.172 us | 1.22x |
| `structural/ordered-modifier-pair` | 10.234 us | 8.582 us | 21.650 us | 1.19x |
| `structural/method-family-generic-call` | 5.410 us | 4.595 us | 17.287 us | 1.18x |
| `structural/modifier-family-to-type-window` | 64.674 us | 55.177 us | 69.055 us | 1.17x |
| `lokad/lexer/operator-run` | 1.005 us | 0.936 us | 1.396 us | 1.07x |
| `lokad/lexer/string` | 31.327 us | 30.235 us | 32.574 us | 1.04x |
| `lokad/style/range-ref` | 0.734 us | 0.725 us | 0.848 us | 1.01x |
| `lokad/langserv/url-download` | 11.667 us | 11.926 us | 12.561 us | 0.98x |
| `lokad/langserv/color-short-hex` | 0.456 us | 0.476 us | 0.677 us | 0.96x |
| `lokad/style/cell-ref` | 0.766 us | 0.831 us | 1.029 us | 0.92x |
| `structural/keyword-to-generic-type` | 3.029 us | 3.483 us | 16.611 us | 0.87x |
| `lokad/style/hex-color` | 0.193 us | 0.230 us | 0.313 us | 0.84x |
| `structural/ordered-identifier-window` | 8.750 us | 10.577 us | 23.869 us | 0.83x |
| `literal-family/type-token-family` | 9.198 us | 11.385 us | 24.145 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.214 us | 4.093 us | 16.845 us | 0.79x |
| `lokad/lexer/number` | 2.829 us | 3.859 us | 4.934 us | 0.73x |
| `literal/identifier-token-casei` | 3.921 us | 5.850 us | 19.109 us | 0.67x |
| `lokad/langserv/color-rgb-hex` | 0.569 us | 0.853 us | 1.096 us | 0.67x |
| `literal-family/method-token-family` | 2.599 us | 5.134 us | 17.872 us | 0.51x |
| `literal/identifier-token` | 2.270 us | 4.670 us | 17.131 us | 0.49x |
| `literal/call-token` | 1.845 us | 3.989 us | 16.545 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 2.873 us | 10.450 us | 23.613 us | 0.27x |
| `lokad/folding/region-marker` | 5.709 us | 35.953 us | 62.537 us | 0.16x |
| `balancing/begin-end-blocks` | 46.595 us | 457.983 us | 489.812 us | 0.10x |
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
