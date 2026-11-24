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
| `literal/sherlock-zh` | 21.922 us | 18.503 us | 2,168.529 us | 1.18x |
| `literal/sherlock-ru` | 67.152 us | 97.552 us | 5,169.980 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.599 us | 66.815 us | 2,150.491 us | 0.64x |
| `aws-keys/quick` | 5,243.593 us | 9,717.763 us | 42,449.150 us | 0.54x |
| `ruff-noqa/tweaked` | 3,998.837 us | 7,552.864 us | 41,127.300 us | 0.53x |
| `literal/sherlock-casei-en` | 84.463 us | 186.279 us | 580.190 us | 0.45x |
| `literal/sherlock-casei-ru` | 150.117 us | 361.582 us | 5,643.292 us | 0.42x |
| `bounded-repeat/letters-en` | 1,085.449 us | 2,790.726 us | 2,919.112 us | 0.39x |
| `literal/sherlock-en` | 32.084 us | 84.158 us | 470.250 us | 0.38x |
| `bounded-repeat/letters-ru` | 1,961.421 us | 5,739.885 us | 6,640.583 us | 0.34x |
| `dictionary/single` | 8,042.920 us | 25,621.367 us | 25,625.200 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,400.339 us | 4,662.981 us | 9,603.450 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 166.593 us | 581.256 us | 1,001.265 us | 0.29x |
| `bounded-repeat/capitals` | 11,369.500 us | 45,054.750 us | 47,982.150 us | 0.25x |
| `literal-alternate/sherlock-en` | 354.483 us | 1,470.365 us | 2,004.611 us | 0.24x |
| `literal-alternate/sherlock-en-nomatch` | 125.108 us | 636.815 us | 1,072.341 us | 0.20x |
| `aws-keys/full` | 8,341.360 us | 84,159.200 us | 116,768.400 us | 0.10x |
| `bounded-repeat/context` | 8,597.189 us | 147,178.100 us | 150,462.000 us | 0.06x |
| `ruff-noqa/real` | 3,386.217 us | 68,410.250 us | 100,237.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 834.991 us | 23,419.225 us | 28,704.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 98.184 us | 11,023.829 us | 11,961.429 us | 0.0089x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.116 us | 0.108 us | 0.135 us | 1.08x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.081 us | 0.085 us | 0.096 us | 0.96x |
| `common/ip-match` | `IsMatch` | 0.169 us | 0.177 us | 0.200 us | 0.96x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.078 us | 0.083 us | 0.099 us | 0.94x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.075 us | 0.083 us | 0.095 us | 0.90x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.091 us | 0.106 us | 0.139 us | 0.86x |
| `industry/mariomka-email-count` | `Count` | 430.742 us | 500.478 us | 8,836.500 us | 0.86x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.131 us | 0.170 us | 0.186 us | 0.77x |
| `common/uri-miss` | `IsMatch` | 0.147 us | 0.198 us | 0.220 us | 0.74x |
| `common/uri-match` | `IsMatch` | 0.155 us | 0.231 us | 0.268 us | 0.67x |
| `industry/mariomka-uri-count` | `Count` | 1,223.879 us | 1,847.876 us | 10,454.300 us | 0.66x |
| `common/match-word` | `Match` | 0.082 us | 0.127 us | 1.151 us | 0.65x |
| `common/email-match` | `IsMatch` | 0.155 us | 0.264 us | 0.292 us | 0.59x |
| `common/ip-miss` | `IsMatch` | 0.391 us | 0.718 us | 0.746 us | 0.54x |
| `industry/leipzig-twain-count` | `Count` | 646.292 us | 1,364.678 us | 8,516.150 us | 0.47x |
| `industry/mariomka-ip-count` | `Count` | 3,283.246 us | 8,254.620 us | 16,611.400 us | 0.40x |
| `common/date-miss` | `IsMatch` | 0.211 us | 0.550 us | 0.583 us | 0.38x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 86.481 us | 256.414 us | 499.785 us | 0.34x |
| `common/matches-set` | `Count` | 61.404 us | 189.820 us | 193.443 us | 0.32x |
| `common/matches-word` | `Count` | 1.225 us | 4.140 us | 5.911 us | 0.30x |
| `common/replace-words` | `Replace` | 20.801 us | 76.625 us | 76.758 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.119 us | 947.706 us | 1,245.160 us | 0.18x |
| `common/matches-boundary` | `Count` | 28.120 us | 161.759 us | 163.086 us | 0.17x |
| `common/split-words` | `Split` | 13.049 us | 80.943 us | 82.340 us | 0.16x |
| `common/matches-words` | `Count` | 8.914 us | 68.223 us | 70.147 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,035.746 us | 9,683.112 us | 16,616.800 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,850.527 us | 18,904.275 us | 19,474.550 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 876.958 us | 11,409.200 us | 12,252.857 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,423.726 us | 31,015.233 us | 38,618.150 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.027 us | 0.610 us | 0.637 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 976.132 us | 22,276.375 us | 29,511.300 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.347 us | 0.372 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.820 us | 0.856 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 267.794 us | 8,077.000 us | 8,628.367 us | 0.03x |
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
| `aws-keys/quick` | 5,330.607 us | 3,658.333 us | 35,086.550 us | 1.46x |
| `literal-alternate/sherlock-zh` | 42.657 us | 34.944 us | 2,105.274 us | 1.22x |
| `literal/sherlock-casei-en` | 83.568 us | 80.562 us | 471.930 us | 1.04x |
| `literal-alternate/sherlock-en-mixed` | 167.333 us | 171.712 us | 545.939 us | 0.97x |
| `bounded-repeat/letters-ru` | 1,960.254 us | 2,025.603 us | 2,888.996 us | 0.97x |
| `literal-alternate/sherlock-en` | 352.161 us | 365.412 us | 769.163 us | 0.96x |
| `literal/sherlock-zh` | 21.935 us | 23.768 us | 2,144.858 us | 0.92x |
| `ruff-noqa/tweaked` | 4,022.174 us | 4,546.424 us | 35,104.850 us | 0.88x |
| `bounded-repeat/capitals` | 11,448.986 us | 13,329.383 us | 16,940.350 us | 0.86x |
| `literal-alternate/sherlock-casei-en` | 99.335 us | 131.105 us | 595.075 us | 0.76x |
| `literal/sherlock-ru` | 67.056 us | 89.213 us | 5,224.586 us | 0.75x |
| `literal/sherlock-casei-ru` | 151.652 us | 223.953 us | 5,339.614 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 125.862 us | 197.483 us | 571.546 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,411.408 us | 2,286.800 us | 7,244.667 us | 0.62x |
| `bounded-repeat/letters-en` | 1,084.236 us | 1,805.937 us | 1,929.664 us | 0.60x |
| `aws-keys/full` | 8,307.390 us | 18,021.500 us | 52,882.200 us | 0.46x |
| `literal/sherlock-en` | 31.781 us | 73.613 us | 438.523 us | 0.43x |
| `literal-alternate/sherlock-casei-ru` | 836.147 us | 1,959.810 us | 7,186.900 us | 0.43x |
| `dictionary/single` | 8,041.410 us | 44,968.700 us | 44,975.350 us | 0.18x |
| `ruff-noqa/real` | 3,384.757 us | 19,916.175 us | 54,355.767 us | 0.17x |
| `bounded-repeat/context` | 8,584.767 us | 67,838.000 us | 70,817.050 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.168 us | 0.048 us | 0.067 us | 3.50x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.081 us | 0.030 us | 0.047 us | 2.64x |
| `common/date-match` | `IsMatch` | 0.114 us | 0.044 us | 0.072 us | 2.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.079 us | 0.031 us | 0.050 us | 2.57x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.130 us | 0.054 us | 0.075 us | 2.40x |
| `common/uri-miss` | `IsMatch` | 0.147 us | 0.066 us | 0.088 us | 2.23x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.077 us | 0.036 us | 0.052 us | 2.17x |
| `common/ip-miss` | `IsMatch` | 0.387 us | 0.179 us | 0.202 us | 2.16x |
| `common/email-match` | `IsMatch` | 0.152 us | 0.079 us | 0.107 us | 1.93x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.089 us | 0.048 us | 0.082 us | 1.87x |
| `common/uri-match` | `IsMatch` | 0.156 us | 0.089 us | 0.119 us | 1.76x |
| `common/match-word` | `Match` | 0.082 us | 0.048 us | 0.940 us | 1.71x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 86.497 us | 57.032 us | 273.608 us | 1.52x |
| `common/replace-words` | `Replace` | 20.812 us | 17.486 us | 18.402 us | 1.19x |
| `industry/mariomka-uri-count` | `Count` | 1,227.325 us | 1,095.016 us | 8,685.000 us | 1.12x |
| `common/date-miss` | `IsMatch` | 0.213 us | 0.198 us | 0.234 us | 1.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,423.541 us | 1,476.549 us | 8,007.350 us | 0.96x |
| `industry/mariomka-ip-count` | `Count` | 3,272.270 us | 3,452.850 us | 10,799.833 us | 0.95x |
| `industry/mariomka-email-count` | `Count` | 430.536 us | 467.081 us | 7,612.750 us | 0.92x |
| `common/matches-words` | `Count` | 8.856 us | 10.528 us | 13.775 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 984.635 us | 1,176.618 us | 6,957.700 us | 0.84x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.044 us | 0.073 us | 0.77x |
| `common/split-words` | `Split` | 13.053 us | 20.583 us | 22.259 us | 0.63x |
| `common/matches-set` | `Count` | 61.498 us | 103.551 us | 104.555 us | 0.59x |
| `common/matches-word` | `Count` | 1.216 us | 2.297 us | 3.370 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 649.472 us | 1,321.663 us | 5,751.350 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,042.033 us | 3,070.862 us | 8,163.825 us | 0.34x |
| `common/one-node-backtracking` | `IsMatch` | 0.027 us | 0.089 us | 0.127 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.430 us | 584.153 us | 849.473 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.293 us | 103.517 us | 105.995 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,838.920 us | 8,462.222 us | 9,090.812 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 875.634 us | 4,687.141 us | 5,107.400 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.089 us | 0.116 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 267.650 us | 3,873.345 us | 4,257.250 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.220 us | 6.198 us | 31.295 us | 1.16x |
| `fallback/lookahead` | 6.527 us | 5.991 us | 19.158 us | 1.09x |
| `structural/ordered-keyword-window` | 18.803 us | 17.692 us | 30.806 us | 1.06x |
| `structural/method-family-generic-call` | 6.378 us | 6.345 us | 19.989 us | 1.01x |
| `structural/method-family-call` | 7.907 us | 7.897 us | 21.094 us | 1.00x |
| `lokad/lexer/doc-line` | 1.644 us | 1.832 us | 2.545 us | 0.90x |
| `lokad/langserv/color-short-hex` | 0.695 us | 0.786 us | 0.980 us | 0.88x |
| `lokad/langserv/identifier-validator` | 6.512 us | 7.384 us | 8.883 us | 0.88x |
| `lokad/lexer/identifier` | 11.739 us | 13.472 us | 16.196 us | 0.87x |
| `lokad/style/hex-color` | 0.424 us | 0.535 us | 0.611 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 1.355 us | 1.795 us | 2.016 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 2.463 us | 3.414 us | 4.167 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.829 us | 3.980 us | 17.445 us | 0.71x |
| `lokad/lexer/operator-run` | 1.633 us | 2.311 us | 2.753 us | 0.71x |
| `structural/ordered-modifier-pair` | 10.356 us | 17.153 us | 30.743 us | 0.60x |
| `structural/ordered-keyword-pair` | 3.121 us | 5.310 us | 18.934 us | 0.59x |
| `lokad/lexer/number` | 6.277 us | 10.692 us | 11.636 us | 0.59x |
| `lokad/langserv/helper-identifier` | 2.540 us | 4.719 us | 5.528 us | 0.54x |
| `literal/identifier-token` | 2.273 us | 4.320 us | 17.092 us | 0.53x |
| `lokad/style/cell-ref` | 0.868 us | 1.865 us | 2.094 us | 0.47x |
| `lokad/style/range-ref` | 0.816 us | 1.836 us | 1.922 us | 0.44x |
| `lokad/langserv/url-dashboard` | 9.684 us | 22.740 us | 23.289 us | 0.43x |
| `structural/ordered-identifier-window` | 11.918 us | 28.052 us | 41.028 us | 0.42x |
| `literal/call-token` | 1.807 us | 4.416 us | 17.134 us | 0.41x |
| `literal-family/type-token-family` | 9.085 us | 23.237 us | 36.665 us | 0.39x |
| `structural/modifier-family-to-type-window` | 94.971 us | 252.251 us | 268.127 us | 0.38x |
| `lokad/lexer/string` | 25.933 us | 83.170 us | 86.364 us | 0.31x |
| `literal-family/method-token-family` | 2.644 us | 9.235 us | 22.001 us | 0.29x |
| `literal/identifier-token-casei` | 3.870 us | 14.486 us | 27.680 us | 0.27x |
| `lokad/langserv/url-download` | 11.240 us | 51.015 us | 50.585 us | 0.22x |
| `structural/modifier-family-to-type-pair` | 24.948 us | 132.205 us | 146.871 us | 0.19x |
| `lokad/folding/region-marker` | 5.075 us | 34.312 us | 62.935 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 16.067 us | 216.467 us | 229.038 us | 0.07x |
| `balancing/begin-end-blocks` | 46.322 us | 4,297.250 us | 4,279.561 us | 0.01x |
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
| `structural/modifier-family-to-type-pair` | 24.918 us | 10.520 us | 23.349 us | 2.37x |
| `structural/ordered-keyword-window` | 18.870 us | 8.547 us | 21.427 us | 2.21x |
| `structural/keyword-family-to-capitalized-identifier` | 16.223 us | 7.563 us | 21.125 us | 2.14x |
| `lokad/style/hex-color` | 0.427 us | 0.231 us | 0.315 us | 1.85x |
| `lokad/lexer/operator-run` | 1.645 us | 0.929 us | 1.385 us | 1.77x |
| `lokad/docs/inline-doc-prefix` | 2.465 us | 1.416 us | 2.141 us | 1.74x |
| `structural/modifier-family-to-type-window` | 95.265 us | 54.891 us | 69.746 us | 1.74x |
| `lokad/lexer/number` | 6.270 us | 3.757 us | 4.830 us | 1.67x |
| `lokad/imports/module-imports` | 7.224 us | 4.339 us | 28.135 us | 1.67x |
| `lokad/langserv/color-rgb-hex` | 1.363 us | 0.832 us | 1.083 us | 1.64x |
| `structural/method-family-call` | 7.904 us | 4.953 us | 17.635 us | 1.60x |
| `lokad/langserv/identifier-validator` | 6.513 us | 4.108 us | 5.775 us | 1.59x |
| `lokad/langserv/url-dashboard` | 9.755 us | 6.156 us | 6.687 us | 1.58x |
| `lokad/lexer/doc-line` | 1.657 us | 1.046 us | 1.790 us | 1.58x |
| `fallback/lookahead` | 6.542 us | 4.139 us | 16.882 us | 1.58x |
| `lokad/langserv/color-short-hex` | 0.697 us | 0.469 us | 0.663 us | 1.49x |
| `lokad/lexer/identifier` | 11.684 us | 7.922 us | 10.468 us | 1.47x |
| `structural/method-family-generic-call` | 6.442 us | 4.493 us | 17.115 us | 1.43x |
| `structural/ordered-modifier-pair` | 10.373 us | 8.390 us | 21.447 us | 1.24x |
| `structural/ordered-identifier-window` | 12.009 us | 10.427 us | 23.564 us | 1.15x |
| `lokad/style/range-ref` | 0.817 us | 0.716 us | 0.849 us | 1.14x |
| `lokad/style/cell-ref` | 0.863 us | 0.831 us | 1.038 us | 1.04x |
| `lokad/langserv/helper-identifier` | 2.554 us | 2.681 us | 3.546 us | 0.95x |
| `lokad/langserv/url-download` | 11.243 us | 12.012 us | 12.465 us | 0.94x |
| `lokad/lexer/string` | 25.898 us | 29.473 us | 33.012 us | 0.88x |
| `structural/keyword-to-generic-type` | 2.836 us | 3.411 us | 16.280 us | 0.83x |
| `literal-family/type-token-family` | 9.145 us | 11.308 us | 24.095 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.126 us | 4.089 us | 16.946 us | 0.76x |
| `literal/identifier-token-casei` | 3.864 us | 5.856 us | 18.734 us | 0.66x |
| `literal-family/method-token-family` | 2.649 us | 4.998 us | 18.110 us | 0.53x |
| `literal/identifier-token` | 2.273 us | 4.611 us | 16.905 us | 0.49x |
| `literal/call-token` | 1.814 us | 3.899 us | 16.253 us | 0.47x |
| `lokad/folding/region-marker` | 5.139 us | 35.538 us | 61.417 us | 0.14x |
| `balancing/begin-end-blocks` | 46.488 us | 456.339 us | 488.383 us | 0.10x |
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
