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
| `literal/sherlock-zh` | 21.049 us | 17.785 us | 2,128.089 us | 1.18x |
| `literal/sherlock-ru` | 63.445 us | 91.666 us | 4,979.706 us | 0.69x |
| `literal-alternate/sherlock-zh` | 41.065 us | 64.788 us | 2,115.480 us | 0.63x |
| `ruff-noqa/tweaked` | 3,804.990 us | 7,133.575 us | 43,492.400 us | 0.53x |
| `literal/sherlock-casei-en` | 74.838 us | 175.735 us | 551.381 us | 0.43x |
| `literal/sherlock-casei-ru` | 138.011 us | 341.409 us | 5,279.557 us | 0.40x |
| `bounded-repeat/letters-en` | 1,051.946 us | 2,653.717 us | 2,807.163 us | 0.40x |
| `literal/sherlock-en` | 30.100 us | 85.331 us | 471.367 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,960.221 us | 5,578.100 us | 6,367.833 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,357.548 us | 4,522.035 us | 9,365.550 us | 0.30x |
| `dictionary/single` | 7,108.291 us | 24,101.775 us | 24,297.425 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 151.689 us | 564.888 us | 990.455 us | 0.27x |
| `bounded-repeat/capitals` | 11,096.443 us | 43,705.050 us | 46,970.100 us | 0.25x |
| `literal-alternate/sherlock-en` | 321.320 us | 1,441.710 us | 1,910.293 us | 0.22x |
| `aws-keys/quick` | 1,900.117 us | 9,157.312 us | 44,351.450 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 119.982 us | 627.642 us | 1,040.796 us | 0.19x |
| `aws-keys/full` | 8,241.370 us | 81,511.100 us | 109,289.100 us | 0.10x |
| `bounded-repeat/context` | 8,389.256 us | 141,143.500 us | 145,764.600 us | 0.06x |
| `ruff-noqa/real` | 3,268.874 us | 70,616.700 us | 100,689.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 807.953 us | 22,256.800 us | 27,104.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 102.680 us | 10,740.600 us | 11,554.514 us | 0.0096x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.059 us | 0.071 us | 0.085 us | 0.83x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.104 us | 0.139 us | 0.78x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.058 us | 0.080 us | 0.095 us | 0.73x |
| `industry/mariomka-email-count` | `Count` | 312.390 us | 485.934 us | 8,298.030 us | 0.64x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.078 us | 0.093 us | 0.63x |
| `industry/mariomka-uri-count` | `Count` | 1,107.491 us | 1,832.129 us | 9,778.050 us | 0.60x |
| `common/date-match` | `IsMatch` | 0.062 us | 0.102 us | 0.130 us | 0.60x |
| `common/email-match` | `IsMatch` | 0.137 us | 0.252 us | 0.277 us | 0.54x |
| `industry/leipzig-twain-count` | `Count` | 604.382 us | 1,260.703 us | 8,264.790 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.071 us | 0.160 us | 0.176 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.072 us | 0.163 us | 0.182 us | 0.44x |
| `common/uri-miss` | `IsMatch` | 0.075 us | 0.179 us | 0.195 us | 0.42x |
| `common/match-word` | `Match` | 0.047 us | 0.120 us | 1.106 us | 0.40x |
| `industry/mariomka-ip-count` | `Count` | 3,036.928 us | 8,161.090 us | 16,151.933 us | 0.37x |
| `common/uri-match` | `IsMatch` | 0.075 us | 0.219 us | 0.254 us | 0.34x |
| `common/ip-miss` | `IsMatch` | 0.222 us | 0.683 us | 0.701 us | 0.32x |
| `common/matches-set` | `Count` | 59.005 us | 182.904 us | 184.871 us | 0.32x |
| `common/matches-word` | `Count` | 1.155 us | 3.966 us | 5.589 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 65.672 us | 242.364 us | 475.449 us | 0.27x |
| `common/replace-words` | `Replace` | 19.376 us | 73.326 us | 74.554 us | 0.26x |
| `common/matches-boundary` | `Count` | 28.830 us | 153.632 us | 156.424 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.100 us | 0.543 us | 0.580 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 158.756 us | 924.311 us | 1,169.288 us | 0.17x |
| `common/split-words` | `Split` | 12.461 us | 76.131 us | 77.659 us | 0.16x |
| `common/matches-words` | `Count` | 8.601 us | 69.393 us | 70.706 us | 0.12x |
| `industry/leipzig-name-family-count` | `Count` | 1,025.031 us | 9,421.050 us | 16,185.125 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,840.285 us | 18,584.980 us | 19,248.725 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 831.743 us | 11,463.900 us | 12,037.671 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.583 us | 0.606 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,553.102 us | 30,900.900 us | 37,922.500 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.331 us | 0.355 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 959.547 us | 21,503.200 us | 28,595.667 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.031 us | 0.794 us | 0.824 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 241.269 us | 7,615.990 us | 8,103.520 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 40.955 us | 34.194 us | 2,053.378 us | 1.20x |
| `literal/sherlock-casei-en` | 75.074 us | 74.242 us | 441.413 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,967.526 us | 1,954.005 us | 2,779.975 us | 1.01x |
| `literal-alternate/sherlock-en` | 320.701 us | 325.545 us | 710.043 us | 0.99x |
| `literal/sherlock-zh` | 21.033 us | 22.721 us | 2,150.325 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 150.672 us | 166.573 us | 543.122 us | 0.90x |
| `ruff-noqa/tweaked` | 3,807.405 us | 4,308.100 us | 37,019.750 us | 0.88x |
| `bounded-repeat/capitals` | 11,074.786 us | 12,901.767 us | 15,737.275 us | 0.86x |
| `literal/sherlock-ru` | 63.098 us | 82.960 us | 4,904.027 us | 0.76x |
| `literal/sherlock-casei-ru` | 138.978 us | 185.537 us | 5,067.033 us | 0.75x |
| `literal-alternate/sherlock-casei-en` | 103.207 us | 138.356 us | 506.080 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 121.019 us | 194.015 us | 571.041 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,360.850 us | 2,231.044 us | 7,024.117 us | 0.61x |
| `bounded-repeat/letters-en` | 1,053.392 us | 1,731.818 us | 1,862.593 us | 0.61x |
| `aws-keys/quick` | 1,908.642 us | 3,518.682 us | 36,048.700 us | 0.54x |
| `aws-keys/full` | 8,223.350 us | 17,517.000 us | 48,262.233 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 808.823 us | 1,866.293 us | 6,819.660 us | 0.43x |
| `literal/sherlock-en` | 30.037 us | 74.021 us | 392.178 us | 0.41x |
| `ruff-noqa/real` | 3,287.946 us | 19,729.825 us | 52,341.000 us | 0.17x |
| `dictionary/single` | 7,129.427 us | 43,184.400 us | 43,245.650 us | 0.17x |
| `bounded-repeat/context` | 8,425.611 us | 65,488.150 us | 68,704.550 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-date-match` | `IsMatch` | 0.047 us | 0.030 us | 0.047 us | 1.57x |
| `common/date-match` | `IsMatch` | 0.062 us | 0.040 us | 0.070 us | 1.54x |
| `common/ip-match` | `IsMatch` | 0.068 us | 0.045 us | 0.065 us | 1.52x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.047 us | 0.032 us | 0.049 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.068 us | 0.052 us | 0.073 us | 1.31x |
| `common/ip-miss` | `IsMatch` | 0.218 us | 0.169 us | 0.194 us | 1.29x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.031 us | 0.046 us | 1.27x |
| `common/uri-miss` | `IsMatch` | 0.075 us | 0.063 us | 0.082 us | 1.19x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 64.725 us | 54.927 us | 256.629 us | 1.18x |
| `common/replace-words` | `Replace` | 19.407 us | 16.544 us | 18.918 us | 1.17x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.052 us | 0.046 us | 0.081 us | 1.11x |
| `industry/mariomka-uri-count` | `Count` | 1,112.330 us | 1,034.516 us | 8,005.950 us | 1.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,541.427 us | 1,458.038 us | 7,557.725 us | 1.06x |
| `common/match-word` | `Match` | 0.047 us | 0.045 us | 0.903 us | 1.05x |
| `common/email-match` | `IsMatch` | 0.074 us | 0.077 us | 0.102 us | 0.97x |
| `industry/mariomka-ip-count` | `Count` | 3,041.408 us | 3,275.461 us | 10,267.267 us | 0.93x |
| `common/uri-match` | `IsMatch` | 0.075 us | 0.084 us | 0.114 us | 0.89x |
| `common/matches-words` | `Count` | 8.550 us | 10.233 us | 13.362 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 958.609 us | 1,184.676 us | 6,610.780 us | 0.81x |
| `common/backtracking` | `IsMatch` | 0.030 us | 0.041 us | 0.070 us | 0.74x |
| `industry/mariomka-email-count` | `Count` | 313.286 us | 455.837 us | 7,461.610 us | 0.69x |
| `common/split-words` | `Split` | 12.731 us | 19.780 us | 21.562 us | 0.64x |
| `common/matches-set` | `Count` | 58.811 us | 99.169 us | 100.304 us | 0.59x |
| `common/matches-word` | `Count` | 1.153 us | 2.179 us | 3.194 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.191 us | 0.226 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 606.469 us | 1,247.256 us | 6,887.630 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,025.038 us | 2,913.196 us | 8,114.283 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.101 us | 0.121 us | 0.31x |
| `common/matches-boundary` | `Count` | 28.439 us | 98.898 us | 100.132 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 159.174 us | 565.245 us | 807.323 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,833.576 us | 8,412.667 us | 8,944.100 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.017 us | 0.084 us | 0.109 us | 0.20x |
| `industry/rust-sherlock-ing-count` | `Count` | 821.807 us | 4,517.435 us | 4,895.019 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 237.985 us | 3,731.681 us | 4,069.326 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.657 us | 6.105 us | 29.300 us | 0.93x |
| `fallback/lookahead` | 4.929 us | 5.703 us | 18.500 us | 0.86x |
| `structural/keyword-to-generic-type` | 2.889 us | 3.868 us | 16.953 us | 0.75x |
| `structural/ordered-keyword-window` | 11.114 us | 16.916 us | 29.435 us | 0.66x |
| `structural/method-family-call` | 4.945 us | 7.553 us | 20.392 us | 0.65x |
| `structural/method-family-generic-call` | 4.041 us | 6.326 us | 19.137 us | 0.64x |
| `structural/ordered-keyword-pair` | 3.094 us | 4.970 us | 18.034 us | 0.62x |
| `lokad/langserv/color-short-hex` | 0.482 us | 0.774 us | 0.940 us | 0.62x |
| `lokad/lexer/identifier` | 7.705 us | 12.674 us | 15.819 us | 0.61x |
| `structural/ordered-modifier-pair` | 9.964 us | 16.545 us | 28.941 us | 0.60x |
| `lokad/langserv/identifier-validator` | 4.108 us | 7.139 us | 8.571 us | 0.58x |
| `lokad/lexer/doc-line` | 0.985 us | 1.782 us | 2.516 us | 0.55x |
| `lokad/docs/inline-doc-prefix` | 1.774 us | 3.270 us | 4.016 us | 0.54x |
| `literal/identifier-token` | 2.127 us | 4.109 us | 16.697 us | 0.52x |
| `lokad/lexer/operator-run` | 1.105 us | 2.206 us | 2.643 us | 0.50x |
| `lokad/langserv/helper-identifier` | 2.208 us | 4.505 us | 5.397 us | 0.49x |
| `lokad/style/cell-ref` | 0.816 us | 1.855 us | 1.968 us | 0.44x |
| `literal/call-token` | 1.745 us | 4.064 us | 16.367 us | 0.43x |
| `lokad/style/hex-color` | 0.207 us | 0.488 us | 0.569 us | 0.43x |
| `lokad/style/range-ref` | 0.719 us | 1.742 us | 1.893 us | 0.41x |
| `literal-family/type-token-family` | 8.715 us | 21.841 us | 34.729 us | 0.40x |
| `lokad/langserv/url-dashboard` | 8.598 us | 22.007 us | 22.379 us | 0.39x |
| `lokad/lexer/string` | 31.032 us | 80.686 us | 85.862 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.604 us | 1.694 us | 1.932 us | 0.36x |
| `literal-family/method-token-family` | 2.500 us | 8.945 us | 21.029 us | 0.28x |
| `structural/ordered-identifier-window` | 7.600 us | 27.317 us | 39.520 us | 0.28x |
| `lokad/lexer/number` | 2.760 us | 10.152 us | 11.160 us | 0.27x |
| `literal/identifier-token-casei` | 3.697 us | 14.050 us | 27.237 us | 0.26x |
| `structural/modifier-family-to-type-window` | 57.394 us | 240.285 us | 253.576 us | 0.24x |
| `lokad/langserv/url-download` | 10.987 us | 46.409 us | 46.586 us | 0.24x |
| `lokad/folding/region-marker` | 5.424 us | 33.465 us | 59.482 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.471 us | 210.873 us | 223.673 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.810 us | 127.115 us | 140.962 us | 0.02x |
| `balancing/begin-end-blocks` | 44.915 us | 3,948.810 us | 3,941.679 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 8.591 us | 6.026 us | 6.443 us | 1.43x |
| `structural/ordered-keyword-window` | 11.175 us | 8.100 us | 20.734 us | 1.38x |
| `lokad/imports/module-imports` | 5.659 us | 4.178 us | 26.810 us | 1.35x |
| `structural/keyword-family-to-capitalized-identifier` | 10.091 us | 7.458 us | 20.010 us | 1.35x |
| `lokad/docs/inline-doc-prefix` | 1.774 us | 1.387 us | 2.097 us | 1.28x |
| `fallback/lookahead` | 4.940 us | 4.015 us | 16.381 us | 1.23x |
| `lokad/lexer/operator-run` | 1.070 us | 0.875 us | 1.355 us | 1.22x |
| `structural/ordered-modifier-pair` | 9.933 us | 8.127 us | 20.519 us | 1.22x |
| `structural/modifier-family-to-type-window` | 57.291 us | 52.682 us | 65.777 us | 1.09x |
| `lokad/lexer/string` | 31.362 us | 29.232 us | 31.576 us | 1.07x |
| `lokad/style/cell-ref` | 0.818 us | 0.792 us | 1.006 us | 1.03x |
| `lokad/langserv/identifier-validator` | 4.097 us | 3.974 us | 5.513 us | 1.03x |
| `lokad/style/range-ref` | 0.720 us | 0.700 us | 0.823 us | 1.03x |
| `structural/method-family-call` | 4.930 us | 4.799 us | 17.141 us | 1.03x |
| `lokad/lexer/identifier` | 7.688 us | 7.517 us | 10.286 us | 1.02x |
| `lokad/lexer/doc-line` | 0.994 us | 0.993 us | 1.726 us | 1.00x |
| `lokad/langserv/color-short-hex` | 0.444 us | 0.459 us | 0.632 us | 0.97x |
| `lokad/langserv/url-download` | 11.034 us | 11.517 us | 12.264 us | 0.96x |
| `structural/method-family-generic-call` | 4.069 us | 4.360 us | 16.581 us | 0.93x |
| `lokad/style/hex-color` | 0.192 us | 0.218 us | 0.296 us | 0.88x |
| `lokad/langserv/helper-identifier` | 2.211 us | 2.536 us | 3.376 us | 0.87x |
| `structural/keyword-to-generic-type` | 2.881 us | 3.313 us | 15.948 us | 0.87x |
| `literal-family/type-token-family` | 8.710 us | 10.819 us | 23.292 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.101 us | 3.915 us | 16.111 us | 0.79x |
| `lokad/lexer/number` | 2.791 us | 3.622 us | 4.615 us | 0.77x |
| `structural/ordered-identifier-window` | 7.709 us | 10.020 us | 22.462 us | 0.77x |
| `literal/identifier-token-casei` | 3.701 us | 5.562 us | 18.078 us | 0.67x |
| `lokad/langserv/color-rgb-hex` | 0.557 us | 0.854 us | 1.060 us | 0.65x |
| `literal-family/method-token-family` | 2.505 us | 4.833 us | 17.095 us | 0.52x |
| `literal/identifier-token` | 2.127 us | 4.436 us | 16.031 us | 0.48x |
| `literal/call-token` | 1.753 us | 3.738 us | 15.517 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.789 us | 10.177 us | 22.652 us | 0.27x |
| `lokad/folding/region-marker` | 5.433 us | 34.032 us | 60.223 us | 0.16x |
| `balancing/begin-end-blocks` | 44.745 us | 443.445 us | 469.766 us | 0.10x |
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
