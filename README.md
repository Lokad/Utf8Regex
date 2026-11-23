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
| `literal/sherlock-zh` | 20.946 us | 17.781 us | 2,110.769 us | 1.18x |
| `literal/sherlock-ru` | 64.662 us | 93.643 us | 5,012.327 us | 0.69x |
| `literal-alternate/sherlock-zh` | 41.701 us | 65.651 us | 2,093.425 us | 0.64x |
| `ruff-noqa/tweaked` | 3,854.540 us | 7,333.082 us | 36,359.000 us | 0.53x |
| `literal/sherlock-casei-ru` | 142.892 us | 347.930 us | 5,299.493 us | 0.41x |
| `bounded-repeat/letters-en` | 1,075.726 us | 2,674.854 us | 2,851.535 us | 0.40x |
| `literal/sherlock-casei-en` | 74.994 us | 187.920 us | 523.036 us | 0.40x |
| `aws-keys/quick` | 3,695.624 us | 9,268.388 us | 44,513.650 us | 0.40x |
| `literal/sherlock-en` | 30.107 us | 81.083 us | 451.298 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,912.530 us | 5,516.121 us | 6,343.033 us | 0.35x |
| `dictionary/single` | 7,837.270 us | 24,520.450 us | 24,776.400 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,381.995 us | 4,560.665 us | 9,400.675 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 159.569 us | 565.151 us | 981.917 us | 0.28x |
| `bounded-repeat/capitals` | 11,198.700 us | 42,735.850 us | 47,074.750 us | 0.26x |
| `literal-alternate/sherlock-en` | 328.593 us | 1,454.721 us | 1,950.774 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 120.874 us | 612.585 us | 1,016.578 us | 0.20x |
| `aws-keys/full` | 8,084.980 us | 82,764.700 us | 113,916.900 us | 0.10x |
| `bounded-repeat/context` | 8,259.990 us | 141,850.000 us | 145,525.500 us | 0.06x |
| `ruff-noqa/real` | 3,345.009 us | 70,273.850 us | 101,220.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 806.074 us | 22,826.075 us | 27,911.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 98.977 us | 10,956.743 us | 11,701.029 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.086 us | 0.078 us | 0.094 us | 1.10x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.078 us | 0.073 us | 0.085 us | 1.08x |
| `common/date-match` | `IsMatch` | 0.109 us | 0.103 us | 0.131 us | 1.06x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.076 us | 0.078 us | 0.095 us | 0.98x |
| `common/ip-match` | `IsMatch` | 0.149 us | 0.167 us | 0.187 us | 0.89x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.088 us | 0.103 us | 0.137 us | 0.85x |
| `industry/mariomka-email-count` | `Count` | 415.331 us | 489.303 us | 8,667.200 us | 0.85x |
| `common/uri-miss` | `IsMatch` | 0.152 us | 0.179 us | 0.196 us | 0.85x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.131 us | 0.162 us | 0.175 us | 0.81x |
| `common/uri-match` | `IsMatch` | 0.150 us | 0.217 us | 0.251 us | 0.69x |
| `industry/mariomka-uri-count` | `Count` | 1,178.800 us | 1,768.223 us | 9,939.800 us | 0.67x |
| `common/email-match` | `IsMatch` | 0.152 us | 0.255 us | 0.279 us | 0.60x |
| `common/ip-miss` | `IsMatch` | 0.374 us | 0.673 us | 0.698 us | 0.56x |
| `industry/leipzig-twain-count` | `Count` | 603.847 us | 1,267.802 us | 8,338.922 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.123 us | 1.114 us | 0.43x |
| `common/date-miss` | `IsMatch` | 0.229 us | 0.534 us | 0.573 us | 0.43x |
| `industry/mariomka-ip-count` | `Count` | 3,159.596 us | 7,760.570 us | 15,760.200 us | 0.41x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 82.348 us | 246.752 us | 484.802 us | 0.33x |
| `common/matches-set` | `Count` | 59.489 us | 185.392 us | 188.233 us | 0.32x |
| `common/matches-word` | `Count` | 1.166 us | 4.030 us | 5.707 us | 0.29x |
| `common/replace-words` | `Replace` | 20.069 us | 72.806 us | 74.608 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 159.957 us | 908.252 us | 1,166.320 us | 0.18x |
| `common/split-words` | `Split` | 12.458 us | 74.450 us | 76.103 us | 0.17x |
| `common/matches-boundary` | `Count` | 22.937 us | 159.556 us | 161.482 us | 0.14x |
| `common/matches-words` | `Count` | 8.841 us | 68.644 us | 70.053 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,003.513 us | 9,460.244 us | 16,404.967 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,825.342 us | 18,622.200 us | 19,425.700 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 822.414 us | 11,306.471 us | 11,813.914 us | 0.07x |
| `common/backtracking` | `IsMatch` | 0.039 us | 0.790 us | 0.829 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,408.598 us | 30,530.933 us | 37,830.000 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.330 us | 0.354 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.026 us | 0.582 us | 0.615 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 956.624 us | 22,510.475 us | 29,634.450 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 259.147 us | 7,910.480 us | 8,366.733 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 41.575 us | 34.660 us | 2,061.369 us | 1.20x |
| `aws-keys/quick` | 3,739.909 us | 3,547.500 us | 35,974.300 us | 1.05x |
| `literal/sherlock-casei-en` | 74.842 us | 74.553 us | 411.900 us | 1.00x |
| `literal-alternate/sherlock-en-mixed` | 160.563 us | 168.498 us | 506.134 us | 0.95x |
| `literal-alternate/sherlock-en` | 329.912 us | 347.903 us | 731.497 us | 0.95x |
| `bounded-repeat/letters-ru` | 1,906.430 us | 2,041.079 us | 2,852.478 us | 0.93x |
| `literal/sherlock-zh` | 21.040 us | 22.737 us | 2,132.714 us | 0.93x |
| `ruff-noqa/tweaked` | 3,873.890 us | 4,354.261 us | 35,372.533 us | 0.89x |
| `bounded-repeat/capitals` | 11,193.471 us | 12,882.217 us | 15,819.875 us | 0.87x |
| `literal/sherlock-ru` | 66.067 us | 85.201 us | 4,940.881 us | 0.78x |
| `literal-alternate/sherlock-casei-en` | 98.676 us | 138.866 us | 557.327 us | 0.71x |
| `literal/sherlock-casei-ru` | 147.921 us | 210.454 us | 5,080.033 us | 0.70x |
| `literal-alternate/sherlock-en-nomatch` | 121.071 us | 190.763 us | 561.144 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,383.587 us | 2,247.956 us | 7,051.600 us | 0.62x |
| `bounded-repeat/letters-en` | 1,074.553 us | 1,747.168 us | 1,860.233 us | 0.62x |
| `aws-keys/full` | 8,056.050 us | 17,563.000 us | 51,164.100 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 802.542 us | 1,874.870 us | 6,707.275 us | 0.43x |
| `literal/sherlock-en` | 30.274 us | 71.220 us | 415.349 us | 0.43x |
| `dictionary/single` | 7,798.300 us | 43,819.300 us | 43,685.600 us | 0.18x |
| `ruff-noqa/real` | 3,328.904 us | 19,750.750 us | 53,071.933 us | 0.17x |
| `bounded-repeat/context` | 8,248.600 us | 65,876.550 us | 68,981.350 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.148 us | 0.046 us | 0.065 us | 3.21x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.080 us | 0.029 us | 0.046 us | 2.73x |
| `common/date-match` | `IsMatch` | 0.107 us | 0.039 us | 0.069 us | 2.73x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.132 us | 0.053 us | 0.073 us | 2.47x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.075 us | 0.032 us | 0.050 us | 2.37x |
| `common/uri-miss` | `IsMatch` | 0.150 us | 0.065 us | 0.084 us | 2.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.077 us | 0.034 us | 0.050 us | 2.27x |
| `common/ip-miss` | `IsMatch` | 0.375 us | 0.171 us | 0.193 us | 2.19x |
| `common/email-match` | `IsMatch` | 0.156 us | 0.077 us | 0.104 us | 2.04x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.084 us | 0.046 us | 0.083 us | 1.83x |
| `common/uri-match` | `IsMatch` | 0.148 us | 0.082 us | 0.115 us | 1.80x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 82.280 us | 55.109 us | 262.285 us | 1.49x |
| `common/replace-words` | `Replace` | 20.112 us | 16.350 us | 17.105 us | 1.23x |
| `common/match-word` | `Match` | 0.053 us | 0.045 us | 0.885 us | 1.18x |
| `common/date-miss` | `IsMatch` | 0.227 us | 0.194 us | 0.223 us | 1.17x |
| `industry/mariomka-uri-count` | `Count` | 1,178.708 us | 1,063.807 us | 8,029.650 us | 1.11x |
| `industry/mariomka-ip-count` | `Count` | 3,152.542 us | 3,268.674 us | 10,285.533 us | 0.96x |
| `common/backtracking` | `IsMatch` | 0.040 us | 0.042 us | 0.070 us | 0.96x |
| `industry/leipzig-river-window-count` | `Count` | 1,419.343 us | 1,511.173 us | 7,765.150 us | 0.94x |
| `industry/mariomka-email-count` | `Count` | 416.743 us | 456.150 us | 7,505.100 us | 0.91x |
| `common/matches-words` | `Count` | 8.818 us | 10.449 us | 13.579 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 956.051 us | 1,176.545 us | 6,790.275 us | 0.81x |
| `common/split-words` | `Split` | 12.458 us | 19.889 us | 21.762 us | 0.63x |
| `common/matches-set` | `Count` | 59.288 us | 101.486 us | 103.439 us | 0.58x |
| `common/matches-word` | `Count` | 1.162 us | 2.249 us | 3.273 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 608.827 us | 1,251.231 us | 7,193.330 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,024.725 us | 2,931.312 us | 7,646.550 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.026 us | 0.085 us | 0.125 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 158.909 us | 559.907 us | 815.985 us | 0.28x |
| `common/matches-boundary` | `Count` | 23.057 us | 99.098 us | 100.766 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,808.888 us | 8,768.110 us | 9,316.511 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 817.871 us | 4,507.176 us | 4,890.319 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.085 us | 0.111 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 258.925 us | 3,799.860 us | 4,142.416 us | 0.07x |
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
| `structural/method-family-generic-call` | 10.806 us | 6.203 us | 19.615 us | 1.74x |
| `structural/method-family-call` | 9.516 us | 7.568 us | 20.319 us | 1.26x |
| `lokad/imports/module-imports` | 7.093 us | 6.133 us | 28.972 us | 1.16x |
| `fallback/lookahead` | 6.209 us | 5.757 us | 18.398 us | 1.08x |
| `structural/ordered-keyword-window` | 18.014 us | 17.005 us | 29.595 us | 1.06x |
| `lokad/style/cell-ref` | 1.834 us | 1.774 us | 2.013 us | 1.03x |
| `lokad/style/range-ref` | 1.697 us | 1.765 us | 1.889 us | 0.96x |
| `lokad/langserv/identifier-validator` | 6.200 us | 7.077 us | 8.535 us | 0.88x |
| `lokad/lexer/doc-line` | 1.540 us | 1.768 us | 2.474 us | 0.87x |
| `lokad/langserv/color-short-hex` | 0.671 us | 0.775 us | 0.949 us | 0.87x |
| `lokad/lexer/identifier` | 11.118 us | 12.977 us | 15.898 us | 0.86x |
| `lokad/style/hex-color` | 0.407 us | 0.486 us | 0.568 us | 0.84x |
| `lokad/langserv/color-rgb-hex` | 1.277 us | 1.698 us | 1.942 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 2.402 us | 3.239 us | 3.990 us | 0.74x |
| `structural/keyword-to-generic-type` | 2.799 us | 3.889 us | 16.979 us | 0.72x |
| `lokad/lexer/operator-run` | 1.587 us | 2.207 us | 2.685 us | 0.72x |
| `structural/ordered-modifier-pair` | 10.090 us | 16.527 us | 29.404 us | 0.61x |
| `structural/ordered-keyword-pair` | 2.994 us | 5.168 us | 17.942 us | 0.58x |
| `lokad/lexer/number` | 5.926 us | 10.298 us | 11.207 us | 0.58x |
| `lokad/langserv/helper-identifier` | 2.433 us | 4.497 us | 5.366 us | 0.54x |
| `literal/identifier-token` | 2.182 us | 4.121 us | 16.458 us | 0.53x |
| `literal/call-token` | 1.743 us | 4.078 us | 16.436 us | 0.43x |
| `lokad/langserv/url-dashboard` | 9.326 us | 22.197 us | 22.665 us | 0.42x |
| `structural/ordered-identifier-window` | 11.563 us | 27.593 us | 40.796 us | 0.42x |
| `literal-family/type-token-family` | 8.769 us | 22.475 us | 35.769 us | 0.39x |
| `structural/modifier-family-to-type-window` | 91.625 us | 241.570 us | 253.017 us | 0.38x |
| `lokad/lexer/string` | 25.494 us | 77.600 us | 82.201 us | 0.33x |
| `literal-family/method-token-family` | 2.549 us | 8.701 us | 20.932 us | 0.29x |
| `literal/identifier-token-casei` | 3.283 us | 14.222 us | 26.877 us | 0.23x |
| `lokad/langserv/url-download` | 10.844 us | 48.635 us | 49.177 us | 0.22x |
| `structural/modifier-family-to-type-pair` | 22.582 us | 124.811 us | 138.564 us | 0.18x |
| `lokad/folding/region-marker` | 4.926 us | 33.320 us | 58.863 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 13.882 us | 211.430 us | 225.994 us | 0.07x |
| `balancing/begin-end-blocks` | 44.518 us | 3,955.974 us | 3,970.989 us | 0.01x |
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
| `structural/method-family-generic-call` | 10.807 us | 4.359 us | 16.748 us | 2.48x |
| `lokad/style/range-ref` | 1.701 us | 0.697 us | 0.811 us | 2.44x |
| `lokad/style/cell-ref` | 1.830 us | 0.785 us | 0.981 us | 2.33x |
| `structural/ordered-keyword-window` | 17.902 us | 8.163 us | 20.592 us | 2.19x |
| `structural/modifier-family-to-type-pair` | 22.697 us | 10.382 us | 23.352 us | 2.19x |
| `structural/method-family-call` | 9.506 us | 4.786 us | 17.123 us | 1.99x |
| `structural/keyword-family-to-capitalized-identifier` | 13.911 us | 7.342 us | 21.068 us | 1.89x |
| `lokad/style/hex-color` | 0.405 us | 0.218 us | 0.297 us | 1.85x |
| `lokad/lexer/operator-run` | 1.604 us | 0.868 us | 1.313 us | 1.85x |
| `structural/modifier-family-to-type-window` | 91.263 us | 52.651 us | 65.772 us | 1.73x |
| `lokad/docs/inline-doc-prefix` | 2.369 us | 1.369 us | 2.067 us | 1.73x |
| `lokad/imports/module-imports` | 7.103 us | 4.133 us | 26.740 us | 1.72x |
| `lokad/lexer/number` | 5.965 us | 3.591 us | 4.673 us | 1.66x |
| `lokad/lexer/doc-line` | 1.555 us | 0.992 us | 1.740 us | 1.57x |
| `fallback/lookahead` | 6.218 us | 3.988 us | 16.171 us | 1.56x |
| `lokad/langserv/identifier-validator` | 6.237 us | 4.035 us | 5.458 us | 1.55x |
| `lokad/langserv/url-dashboard` | 9.296 us | 6.023 us | 6.474 us | 1.54x |
| `lokad/langserv/color-rgb-hex` | 1.271 us | 0.824 us | 1.061 us | 1.54x |
| `lokad/lexer/identifier` | 11.153 us | 7.477 us | 10.224 us | 1.49x |
| `lokad/langserv/color-short-hex` | 0.671 us | 0.454 us | 0.637 us | 1.48x |
| `structural/ordered-modifier-pair` | 10.156 us | 8.225 us | 20.800 us | 1.23x |
| `structural/ordered-identifier-window` | 11.487 us | 10.137 us | 23.006 us | 1.13x |
| `lokad/langserv/url-download` | 10.856 us | 11.588 us | 12.036 us | 0.94x |
| `lokad/lexer/string` | 25.211 us | 28.965 us | 31.574 us | 0.87x |
| `structural/keyword-to-generic-type` | 2.800 us | 3.304 us | 15.813 us | 0.85x |
| `lokad/langserv/helper-identifier` | 2.460 us | 2.912 us | 3.786 us | 0.84x |
| `literal-family/type-token-family` | 8.789 us | 10.842 us | 23.372 us | 0.81x |
| `structural/ordered-keyword-pair` | 2.997 us | 3.909 us | 16.180 us | 0.77x |
| `literal/identifier-token-casei` | 3.286 us | 5.545 us | 18.214 us | 0.59x |
| `literal-family/method-token-family` | 2.552 us | 5.020 us | 17.063 us | 0.51x |
| `literal/identifier-token` | 2.189 us | 4.475 us | 15.899 us | 0.49x |
| `literal/call-token` | 1.745 us | 3.726 us | 15.646 us | 0.47x |
| `lokad/folding/region-marker` | 4.943 us | 34.207 us | 59.194 us | 0.14x |
| `balancing/begin-end-blocks` | 44.375 us | 443.960 us | 468.457 us | 0.10x |
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
