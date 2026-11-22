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
| `literal/sherlock-zh` | 21.349 us | 18.028 us | 2,151.476 us | 1.18x |
| `literal/sherlock-ru` | 65.448 us | 94.635 us | 5,130.000 us | 0.69x |
| `literal-alternate/sherlock-zh` | 41.378 us | 65.321 us | 2,135.678 us | 0.63x |
| `aws-keys/quick` | 5,075.600 us | 9,150.778 us | 39,041.550 us | 0.55x |
| `ruff-noqa/tweaked` | 3,863.810 us | 7,345.364 us | 42,967.500 us | 0.53x |
| `aws-keys/full` | 41,419.000 us | 83,048.200 us | 113,220.200 us | 0.50x |
| `literal/sherlock-casei-en` | 76.749 us | 181.846 us | 569.604 us | 0.42x |
| `literal/sherlock-casei-ru` | 141.976 us | 349.694 us | 5,376.593 us | 0.41x |
| `bounded-repeat/letters-en` | 1,042.047 us | 2,671.948 us | 2,815.656 us | 0.39x |
| `literal/sherlock-en` | 30.407 us | 83.153 us | 462.175 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,928.713 us | 5,587.793 us | 6,398.033 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,396.104 us | 4,591.341 us | 9,699.675 us | 0.30x |
| `dictionary/single` | 7,352.900 us | 25,200.550 us | 25,369.700 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 166.648 us | 576.920 us | 990.919 us | 0.29x |
| `bounded-repeat/capitals` | 11,183.743 us | 43,614.900 us | 46,681.800 us | 0.26x |
| `literal-alternate/sherlock-en` | 343.589 us | 1,474.563 us | 2,021.574 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 122.899 us | 626.101 us | 1,072.446 us | 0.20x |
| `bounded-repeat/context` | 8,226.680 us | 141,686.200 us | 144,887.100 us | 0.06x |
| `ruff-noqa/real` | 3,311.543 us | 67,393.650 us | 96,462.000 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 840.455 us | 22,242.850 us | 27,600.200 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 99.496 us | 11,430.071 us | 12,156.529 us | 0.0087x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.150 us | 0.105 us | 0.132 us | 1.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.099 us | 0.074 us | 0.087 us | 1.34x |
| `common/uri-match` | `IsMatch` | 0.281 us | 0.227 us | 0.266 us | 1.24x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.095 us | 0.079 us | 0.093 us | 1.21x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.121 us | 0.106 us | 0.141 us | 1.14x |
| `common/ip-match` | `IsMatch` | 0.189 us | 0.167 us | 0.185 us | 1.13x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.092 us | 0.082 us | 0.098 us | 1.12x |
| `common/uri-miss` | `IsMatch` | 0.197 us | 0.179 us | 0.200 us | 1.10x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.146 us | 0.161 us | 0.178 us | 0.91x |
| `industry/mariomka-email-count` | `Count` | 415.228 us | 482.994 us | 8,580.725 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,397.680 us | 1,896.990 us | 9,895.425 us | 0.74x |
| `common/ip-miss` | `IsMatch` | 0.493 us | 0.681 us | 0.700 us | 0.72x |
| `common/email-match` | `IsMatch` | 0.185 us | 0.258 us | 0.284 us | 0.72x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 145.846 us | 244.002 us | 463.108 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.286 us | 0.536 us | 0.573 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 611.875 us | 1,269.832 us | 8,326.222 us | 0.48x |
| `common/match-word` | `Match` | 0.054 us | 0.123 us | 1.097 us | 0.44x |
| `common/matches-set` | `Count` | 59.485 us | 184.737 us | 186.695 us | 0.32x |
| `common/matches-word` | `Count` | 1.205 us | 4.049 us | 5.617 us | 0.30x |
| `industry/mariomka-ip-count` | `Count` | 2,164.174 us | 8,013.400 us | 16,318.667 us | 0.27x |
| `common/replace-words` | `Replace` | 19.817 us | 76.761 us | 77.715 us | 0.26x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.196 us | 925.680 us | 1,201.109 us | 0.18x |
| `common/split-words` | `Split` | 12.693 us | 76.233 us | 77.164 us | 0.17x |
| `common/matches-boundary` | `Count` | 24.530 us | 161.011 us | 162.985 us | 0.15x |
| `common/matches-words` | `Count` | 8.815 us | 66.724 us | 68.198 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.040 us | 0.334 us | 0.361 us | 0.12x |
| `industry/leipzig-name-family-count` | `Count` | 1,024.364 us | 9,551.950 us | 16,371.300 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,794.543 us | 18,585.925 us | 19,148.650 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 850.767 us | 11,273.871 us | 11,845.414 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.057 us | 0.796 us | 0.845 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.593 us | 0.632 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,804.749 us | 30,741.433 us | 37,572.200 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 976.244 us | 21,811.475 us | 28,389.633 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 288.348 us | 8,172.160 us | 8,604.156 us | 0.04x |
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
| `aws-keys/full` | 41,139.500 us | 17,710.080 us | 48,780.800 us | 2.32x |
| `aws-keys/quick` | 5,131.033 us | 3,447.545 us | 33,965.933 us | 1.49x |
| `literal-alternate/sherlock-zh` | 41.501 us | 34.165 us | 2,056.156 us | 1.21x |
| `literal/sherlock-casei-en` | 77.691 us | 71.993 us | 416.912 us | 1.08x |
| `bounded-repeat/letters-ru` | 1,923.310 us | 1,980.200 us | 2,789.607 us | 0.97x |
| `literal-alternate/sherlock-en` | 342.349 us | 355.078 us | 729.894 us | 0.96x |
| `literal-alternate/sherlock-en-mixed` | 162.495 us | 169.057 us | 565.504 us | 0.96x |
| `literal/sherlock-zh` | 21.402 us | 23.078 us | 2,124.900 us | 0.93x |
| `ruff-noqa/tweaked` | 3,871.640 us | 4,412.306 us | 37,213.400 us | 0.88x |
| `bounded-repeat/capitals` | 11,108.200 us | 13,056.283 us | 15,898.925 us | 0.85x |
| `literal/sherlock-ru` | 65.822 us | 86.590 us | 5,118.413 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 98.243 us | 140.657 us | 534.294 us | 0.70x |
| `literal/sherlock-casei-ru` | 143.239 us | 215.913 us | 5,230.633 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 122.680 us | 191.871 us | 566.396 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,410.431 us | 2,269.318 us | 7,321.667 us | 0.62x |
| `bounded-repeat/letters-en` | 1,044.747 us | 1,751.795 us | 1,853.380 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 844.278 us | 1,894.734 us | 6,848.150 us | 0.45x |
| `literal/sherlock-en` | 30.410 us | 73.397 us | 409.621 us | 0.41x |
| `dictionary/single` | 7,453.436 us | 44,272.750 us | 44,122.350 us | 0.17x |
| `ruff-noqa/real` | 3,317.791 us | 19,876.275 us | 50,142.967 us | 0.17x |
| `bounded-repeat/context` | 8,211.300 us | 66,376.150 us | 69,382.500 us | 0.12x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.187 us | 0.046 us | 0.066 us | 4.03x |
| `common/date-match` | `IsMatch` | 0.148 us | 0.041 us | 0.070 us | 3.63x |
| `common/uri-match` | `IsMatch` | 0.281 us | 0.085 us | 0.118 us | 3.30x |
| `common/uri-miss` | `IsMatch` | 0.198 us | 0.063 us | 0.081 us | 3.16x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.095 us | 0.031 us | 0.047 us | 3.01x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.090 us | 0.030 us | 0.047 us | 3.00x |
| `common/ip-miss` | `IsMatch` | 0.491 us | 0.172 us | 0.195 us | 2.85x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.098 us | 0.035 us | 0.051 us | 2.83x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.144 us | 0.054 us | 0.075 us | 2.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.121 us | 0.048 us | 0.081 us | 2.52x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 145.489 us | 60.228 us | 263.353 us | 2.42x |
| `common/email-match` | `IsMatch` | 0.183 us | 0.077 us | 0.105 us | 2.38x |
| `common/date-miss` | `IsMatch` | 0.288 us | 0.191 us | 0.226 us | 1.51x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.043 us | 0.072 us | 1.29x |
| `industry/mariomka-uri-count` | `Count` | 1,390.062 us | 1,079.903 us | 8,099.125 us | 1.29x |
| `industry/leipzig-river-window-count` | `Count` | 1,802.814 us | 1,472.006 us | 7,558.950 us | 1.22x |
| `common/match-word` | `Match` | 0.054 us | 0.046 us | 0.892 us | 1.18x |
| `common/replace-words` | `Replace` | 19.718 us | 16.809 us | 19.292 us | 1.17x |
| `industry/mariomka-email-count` | `Count` | 413.955 us | 451.478 us | 7,437.550 us | 0.92x |
| `common/matches-words` | `Count` | 8.777 us | 10.373 us | 13.524 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 982.571 us | 1,230.334 us | 6,603.300 us | 0.80x |
| `industry/mariomka-ip-count` | `Count` | 2,172.849 us | 3,382.100 us | 10,561.167 us | 0.64x |
| `common/split-words` | `Split` | 12.728 us | 20.201 us | 22.102 us | 0.63x |
| `common/matches-set` | `Count` | 59.632 us | 100.154 us | 102.942 us | 0.60x |
| `common/matches-word` | `Count` | 1.200 us | 2.242 us | 3.287 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 611.387 us | 1,263.616 us | 7,271.270 us | 0.48x |
| `common/email-miss` | `IsMatch` | 0.040 us | 0.086 us | 0.113 us | 0.47x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.088 us | 0.125 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 1,027.130 us | 2,998.312 us | 7,858.775 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.742 us | 562.946 us | 804.001 us | 0.29x |
| `common/matches-boundary` | `Count` | 24.509 us | 101.147 us | 102.945 us | 0.24x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,802.963 us | 8,425.767 us | 9,575.900 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 847.024 us | 4,566.759 us | 4,959.194 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 291.537 us | 3,796.538 us | 4,124.200 us | 0.08x |
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
| `lokad/langserv/helper-identifier` | 9.207 us | 4.390 us | 5.363 us | 2.10x |
| `lokad/langserv/identifier-validator` | 13.258 us | 7.115 us | 8.593 us | 1.86x |
| `lokad/lexer/identifier` | 23.798 us | 13.012 us | 15.843 us | 1.83x |
| `fallback/lookahead` | 10.376 us | 5.861 us | 18.412 us | 1.77x |
| `structural/method-family-generic-call` | 10.779 us | 6.345 us | 19.714 us | 1.70x |
| `lokad/docs/inline-doc-prefix` | 5.159 us | 3.284 us | 3.990 us | 1.57x |
| `lokad/lexer/doc-line` | 2.398 us | 1.794 us | 2.485 us | 1.34x |
| `structural/method-family-call` | 9.763 us | 7.640 us | 20.437 us | 1.28x |
| `lokad/imports/module-imports` | 7.309 us | 6.105 us | 29.364 us | 1.20x |
| `lokad/lexer/string` | 91.886 us | 77.110 us | 81.076 us | 1.19x |
| `structural/ordered-keyword-window` | 20.277 us | 17.205 us | 29.994 us | 1.18x |
| `lokad/langserv/color-short-hex` | 0.844 us | 0.783 us | 0.961 us | 1.08x |
| `lokad/style/cell-ref` | 1.809 us | 1.791 us | 2.018 us | 1.01x |
| `lokad/style/range-ref` | 1.762 us | 1.776 us | 1.921 us | 0.99x |
| `lokad/style/hex-color` | 0.464 us | 0.490 us | 0.567 us | 0.95x |
| `lokad/lexer/operator-run` | 2.129 us | 2.259 us | 2.684 us | 0.94x |
| `lokad/lexer/number` | 8.266 us | 10.060 us | 11.279 us | 0.82x |
| `lokad/langserv/color-rgb-hex` | 1.421 us | 1.737 us | 1.953 us | 0.82x |
| `structural/keyword-to-generic-type` | 2.977 us | 3.962 us | 17.689 us | 0.75x |
| `structural/ordered-keyword-pair` | 3.431 us | 5.250 us | 18.486 us | 0.65x |
| `structural/ordered-modifier-pair` | 9.919 us | 16.909 us | 29.903 us | 0.59x |
| `structural/ordered-identifier-window` | 14.546 us | 27.381 us | 40.528 us | 0.53x |
| `literal/identifier-token` | 2.149 us | 4.159 us | 16.707 us | 0.52x |
| `lokad/langserv/url-dashboard` | 9.731 us | 22.310 us | 22.697 us | 0.44x |
| `literal/call-token` | 1.803 us | 4.170 us | 17.021 us | 0.43x |
| `literal-family/method-token-family` | 3.709 us | 8.705 us | 21.597 us | 0.43x |
| `structural/modifier-family-to-type-window` | 100.910 us | 243.927 us | 260.459 us | 0.41x |
| `literal-family/type-token-family` | 9.417 us | 23.164 us | 36.412 us | 0.41x |
| `balancing/begin-end-blocks` | 1,314.022 us | 4,213.779 us | 4,226.472 us | 0.31x |
| `literal/identifier-token-casei` | 3.793 us | 14.589 us | 27.430 us | 0.26x |
| `lokad/langserv/url-download` | 11.482 us | 47.984 us | 48.368 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 22.842 us | 128.968 us | 144.809 us | 0.18x |
| `lokad/folding/region-marker` | 4.874 us | 34.212 us | 59.411 us | 0.14x |
| `structural/keyword-family-to-capitalized-identifier` | 13.650 us | 217.317 us | 236.880 us | 0.06x |
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
| `lokad/docs/inline-doc-prefix` | 5.148 us | 1.379 us | 2.116 us | 3.73x |
| `lokad/langserv/helper-identifier` | 9.225 us | 2.575 us | 3.378 us | 3.58x |
| `lokad/langserv/identifier-validator` | 13.331 us | 3.980 us | 5.474 us | 3.35x |
| `lokad/lexer/string` | 92.004 us | 28.334 us | 31.672 us | 3.25x |
| `lokad/lexer/identifier` | 23.563 us | 7.542 us | 10.302 us | 3.12x |
| `balancing/begin-end-blocks` | 1,311.142 us | 445.838 us | 474.716 us | 2.94x |
| `lokad/style/range-ref` | 1.767 us | 0.690 us | 0.805 us | 2.56x |
| `fallback/lookahead` | 10.441 us | 4.082 us | 16.230 us | 2.56x |
| `structural/ordered-keyword-window` | 20.334 us | 8.293 us | 21.170 us | 2.45x |
| `structural/method-family-generic-call` | 10.846 us | 4.427 us | 16.940 us | 2.45x |
| `lokad/lexer/operator-run` | 2.129 us | 0.888 us | 1.315 us | 2.40x |
| `lokad/lexer/doc-line` | 2.384 us | 0.996 us | 1.743 us | 2.39x |
| `lokad/style/cell-ref` | 1.807 us | 0.780 us | 0.995 us | 2.32x |
| `lokad/lexer/number` | 8.214 us | 3.601 us | 4.610 us | 2.28x |
| `structural/modifier-family-to-type-pair` | 22.820 us | 10.284 us | 23.180 us | 2.22x |
| `lokad/style/hex-color` | 0.466 us | 0.217 us | 0.295 us | 2.14x |
| `structural/method-family-call` | 9.730 us | 4.896 us | 17.357 us | 1.99x |
| `structural/modifier-family-to-type-window` | 100.255 us | 53.346 us | 67.683 us | 1.88x |
| `structural/keyword-family-to-capitalized-identifier` | 13.619 us | 7.509 us | 21.214 us | 1.81x |
| `lokad/langserv/color-short-hex` | 0.844 us | 0.468 us | 0.652 us | 1.80x |
| `lokad/langserv/color-rgb-hex` | 1.432 us | 0.810 us | 1.045 us | 1.77x |
| `lokad/imports/module-imports` | 7.281 us | 4.182 us | 26.890 us | 1.74x |
| `lokad/langserv/url-dashboard` | 9.711 us | 5.933 us | 6.337 us | 1.64x |
| `structural/ordered-identifier-window` | 14.423 us | 10.270 us | 23.486 us | 1.40x |
| `structural/ordered-modifier-pair` | 9.876 us | 8.241 us | 20.997 us | 1.20x |
| `lokad/langserv/url-download` | 11.463 us | 11.481 us | 11.876 us | 1.00x |
| `structural/keyword-to-generic-type` | 2.943 us | 3.328 us | 16.414 us | 0.88x |
| `literal-family/type-token-family` | 9.546 us | 11.040 us | 23.627 us | 0.86x |
| `structural/ordered-keyword-pair` | 3.428 us | 3.993 us | 16.758 us | 0.86x |
| `literal-family/method-token-family` | 3.682 us | 5.101 us | 17.571 us | 0.72x |
| `literal/identifier-token-casei` | 3.801 us | 5.658 us | 18.526 us | 0.67x |
| `literal/identifier-token` | 2.149 us | 4.495 us | 16.395 us | 0.48x |
| `literal/call-token` | 1.788 us | 3.838 us | 16.123 us | 0.47x |
| `lokad/folding/region-marker` | 4.900 us | 34.084 us | 58.762 us | 0.14x |
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
