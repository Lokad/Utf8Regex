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
| `literal/sherlock-zh` | 14.081 us | 18.826 us | 2,235.406 us | 0.75x |
| `literal/sherlock-ru` | 67.449 us | 96.680 us | 5,309.047 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.194 us | 67.427 us | 2,175.800 us | 0.64x |
| `ruff-noqa/tweaked` | 4,008.689 us | 7,874.991 us | 45,258.500 us | 0.51x |
| `bounded-repeat/letters-en` | 1,100.060 us | 2,748.732 us | 2,892.300 us | 0.40x |
| `literal/sherlock-casei-ru` | 145.233 us | 385.830 us | 5,522.800 us | 0.38x |
| `literal/sherlock-casei-en` | 68.094 us | 185.089 us | 681.224 us | 0.37x |
| `literal/sherlock-en` | 31.171 us | 85.963 us | 538.784 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,008.782 us | 5,745.179 us | 6,624.100 us | 0.35x |
| `dictionary/single` | 8,002.720 us | 25,576.933 us | 25,595.833 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,417.510 us | 4,699.165 us | 9,607.675 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 164.050 us | 602.472 us | 1,053.654 us | 0.27x |
| `bounded-repeat/capitals` | 11,807.043 us | 45,851.100 us | 49,960.450 us | 0.26x |
| `literal-alternate/sherlock-en` | 344.161 us | 1,480.278 us | 2,094.483 us | 0.23x |
| `aws-keys/quick` | 1,878.263 us | 9,210.633 us | 46,321.500 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 127.506 us | 652.132 us | 1,096.732 us | 0.20x |
| `aws-keys/full` | 8,580.144 us | 84,395.100 us | 113,418.100 us | 0.10x |
| `bounded-repeat/context` | 8,866.078 us | 144,325.000 us | 148,213.400 us | 0.06x |
| `ruff-noqa/real` | 3,460.322 us | 72,420.000 us | 104,881.700 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 842.920 us | 23,183.000 us | 28,248.050 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 99.659 us | 11,041.300 us | 12,102.757 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.084 us | 0.107 us | 0.140 us | 0.78x |
| `common/ip-match` | `IsMatch` | 0.119 us | 0.178 us | 0.196 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.107 us | 0.135 us | 0.66x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.076 us | 0.090 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,195.838 us | 1,880.902 us | 10,179.300 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.083 us | 0.099 us | 0.63x |
| `industry/mariomka-email-count` | `Count` | 268.918 us | 489.892 us | 8,562.050 us | 0.55x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.043 us | 0.080 us | 0.096 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.135 us | 0.264 us | 0.290 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 650.021 us | 1,349.081 us | 8,464.780 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.094 us | 0.196 us | 0.215 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.166 us | 0.182 us | 0.48x |
| `common/ip-miss` | `IsMatch` | 0.276 us | 0.708 us | 0.751 us | 0.39x |
| `common/uri-match` | `IsMatch` | 0.089 us | 0.229 us | 0.268 us | 0.39x |
| `common/match-word` | `Match` | 0.050 us | 0.130 us | 1.164 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,030.892 us | 8,137.880 us | 21,918.333 us | 0.37x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 81.954 us | 250.612 us | 508.243 us | 0.33x |
| `common/matches-set` | `Count` | 62.357 us | 190.699 us | 192.672 us | 0.33x |
| `common/matches-word` | `Count` | 1.205 us | 4.184 us | 5.757 us | 0.29x |
| `common/replace-words` | `Replace` | 20.191 us | 75.262 us | 77.738 us | 0.27x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.537 us | 0.569 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.677 us | 944.908 us | 1,229.368 us | 0.18x |
| `common/matches-boundary` | `Count` | 29.305 us | 166.355 us | 168.059 us | 0.18x |
| `common/split-words` | `Split` | 13.081 us | 76.666 us | 78.346 us | 0.17x |
| `common/matches-words` | `Count` | 9.023 us | 68.419 us | 71.014 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,061.094 us | 9,580.067 us | 17,063.733 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,970.118 us | 19,546.075 us | 20,134.200 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.594 us | 0.632 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.339 us | 0.360 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,656.989 us | 31,462.600 us | 38,790.150 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 973.903 us | 21,967.550 us | 29,636.400 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.031 us | 0.815 us | 0.854 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 250.068 us | 8,058.180 us | 8,502.767 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 288.484 us | 12,328.286 us | 12,609.500 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 42.460 us | 35.402 us | 2,151.629 us | 1.20x |
| `literal-alternate/sherlock-en-mixed` | 164.426 us | 172.681 us | 634.328 us | 0.95x |
| `literal-alternate/sherlock-en` | 345.515 us | 363.290 us | 806.505 us | 0.95x |
| `bounded-repeat/letters-ru` | 2,016.492 us | 2,137.169 us | 2,989.627 us | 0.94x |
| `literal/sherlock-casei-en` | 68.002 us | 77.027 us | 475.173 us | 0.88x |
| `bounded-repeat/capitals` | 11,752.014 us | 13,337.483 us | 16,395.350 us | 0.88x |
| `ruff-noqa/tweaked` | 4,032.061 us | 4,787.606 us | 39,675.250 us | 0.84x |
| `literal-alternate/sherlock-casei-en` | 99.406 us | 129.518 us | 726.856 us | 0.77x |
| `literal/sherlock-ru` | 67.856 us | 88.666 us | 5,253.787 us | 0.77x |
| `literal/sherlock-casei-ru` | 146.999 us | 194.094 us | 5,333.707 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 126.415 us | 199.191 us | 568.674 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,417.243 us | 2,292.947 us | 7,321.650 us | 0.62x |
| `bounded-repeat/letters-en` | 1,102.210 us | 1,797.911 us | 1,922.815 us | 0.61x |
| `literal/sherlock-zh` | 14.113 us | 24.025 us | 2,232.476 us | 0.59x |
| `aws-keys/quick` | 1,917.863 us | 3,548.718 us | 37,575.050 us | 0.54x |
| `aws-keys/full` | 8,669.822 us | 18,216.040 us | 49,930.200 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 828.803 us | 1,911.490 us | 6,974.525 us | 0.43x |
| `literal/sherlock-en` | 31.244 us | 75.372 us | 454.464 us | 0.41x |
| `dictionary/single` | 8,002.300 us | 44,715.100 us | 45,015.250 us | 0.18x |
| `ruff-noqa/real` | 3,497.714 us | 20,489.700 us | 54,555.767 us | 0.17x |
| `bounded-repeat/context` | 8,914.000 us | 67,368.850 us | 70,849.700 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.114 us | 0.047 us | 0.067 us | 2.44x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.042 us | 0.072 us | 1.69x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.031 us | 0.049 us | 1.53x |
| `common/ip-miss` | `IsMatch` | 0.271 us | 0.178 us | 0.201 us | 1.53x |
| `common/uri-miss` | `IsMatch` | 0.094 us | 0.065 us | 0.087 us | 1.46x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 82.094 us | 57.095 us | 273.695 us | 1.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.054 us | 0.075 us | 1.44x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.030 us | 0.047 us | 1.31x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.035 us | 0.052 us | 1.28x |
| `common/replace-words` | `Replace` | 20.301 us | 17.305 us | 18.309 us | 1.17x |
| `industry/leipzig-river-window-count` | `Count` | 1,664.700 us | 1,501.612 us | 7,634.850 us | 1.11x |
| `industry/mariomka-uri-count` | `Count` | 1,190.465 us | 1,082.578 us | 8,129.325 us | 1.10x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.051 us | 0.048 us | 0.081 us | 1.08x |
| `common/match-word` | `Match` | 0.050 us | 0.047 us | 0.928 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.088 us | 0.120 us | 1.04x |
| `common/email-match` | `IsMatch` | 0.073 us | 0.079 us | 0.105 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,057.184 us | 3,325.017 us | 10,690.533 us | 0.92x |
| `common/matches-words` | `Count` | 9.055 us | 10.780 us | 13.836 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 979.252 us | 1,189.360 us | 7,077.025 us | 0.82x |
| `common/backtracking` | `IsMatch` | 0.031 us | 0.042 us | 0.073 us | 0.74x |
| `common/split-words` | `Split` | 13.088 us | 21.940 us | 23.926 us | 0.60x |
| `common/matches-set` | `Count` | 62.123 us | 104.972 us | 106.514 us | 0.59x |
| `industry/mariomka-email-count` | `Count` | 269.417 us | 456.437 us | 7,519.200 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.194 us | 0.227 us | 0.56x |
| `common/matches-word` | `Count` | 1.204 us | 2.305 us | 3.375 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 650.003 us | 1,323.767 us | 5,741.375 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,083.535 us | 2,911.631 us | 8,280.325 us | 0.37x |
| `common/one-node-backtracking` | `IsMatch` | 0.035 us | 0.106 us | 0.125 us | 0.33x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.629 us | 582.235 us | 852.678 us | 0.29x |
| `common/matches-boundary` | `Count` | 29.148 us | 103.608 us | 105.173 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,967.749 us | 8,633.911 us | 9,182.612 us | 0.23x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.089 us | 0.114 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 250.293 us | 3,910.755 us | 4,270.233 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 287.631 us | 4,651.406 us | 5,100.227 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.952 us | 6.393 us | 32.101 us | 0.93x |
| `fallback/lookahead` | 5.294 us | 5.863 us | 20.095 us | 0.90x |
| `lokad/langserv/color-short-hex` | 0.596 us | 0.803 us | 0.988 us | 0.74x |
| `structural/method-family-generic-call` | 4.367 us | 6.383 us | 21.195 us | 0.68x |
| `lokad/lexer/identifier` | 8.692 us | 13.250 us | 16.324 us | 0.66x |
| `structural/method-family-call` | 5.222 us | 7.977 us | 22.435 us | 0.65x |
| `structural/ordered-keyword-window` | 11.102 us | 17.316 us | 31.567 us | 0.64x |
| `lokad/langserv/identifier-validator` | 4.677 us | 7.396 us | 8.882 us | 0.63x |
| `lokad/lexer/doc-line` | 1.127 us | 1.834 us | 2.567 us | 0.61x |
| `structural/ordered-modifier-pair` | 9.963 us | 16.779 us | 31.387 us | 0.59x |
| `lokad/docs/inline-doc-prefix` | 1.947 us | 3.395 us | 4.140 us | 0.57x |
| `structural/keyword-to-generic-type` | 2.256 us | 3.969 us | 18.833 us | 0.57x |
| `lokad/style/hex-color` | 0.266 us | 0.489 us | 0.590 us | 0.54x |
| `literal/identifier-token` | 2.198 us | 4.211 us | 18.356 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.412 us | 4.698 us | 5.571 us | 0.51x |
| `lokad/lexer/operator-run` | 1.169 us | 2.315 us | 2.729 us | 0.50x |
| `structural/ordered-keyword-pair` | 2.496 us | 5.185 us | 19.559 us | 0.48x |
| `lokad/style/range-ref` | 0.778 us | 1.784 us | 1.998 us | 0.44x |
| `lokad/style/cell-ref` | 0.828 us | 1.917 us | 2.104 us | 0.43x |
| `literal/call-token` | 1.822 us | 4.233 us | 18.256 us | 0.43x |
| `lokad/langserv/color-rgb-hex` | 0.704 us | 1.761 us | 2.013 us | 0.40x |
| `lokad/lexer/string` | 31.425 us | 79.828 us | 83.828 us | 0.39x |
| `literal-family/type-token-family` | 9.028 us | 23.257 us | 38.468 us | 0.39x |
| `lokad/langserv/url-dashboard` | 8.522 us | 22.842 us | 23.197 us | 0.37x |
| `lokad/lexer/number` | 3.185 us | 10.557 us | 11.569 us | 0.30x |
| `structural/ordered-identifier-window` | 8.179 us | 28.323 us | 42.423 us | 0.29x |
| `literal-family/method-token-family` | 2.580 us | 9.126 us | 23.371 us | 0.28x |
| `literal/identifier-token-casei` | 3.770 us | 14.502 us | 29.306 us | 0.26x |
| `structural/modifier-family-to-type-window` | 59.729 us | 249.723 us | 265.577 us | 0.24x |
| `lokad/langserv/url-download` | 11.202 us | 49.912 us | 50.405 us | 0.22x |
| `lokad/folding/region-marker` | 5.595 us | 34.650 us | 63.071 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.091 us | 213.589 us | 229.689 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.879 us | 132.188 us | 147.162 us | 0.02x |
| `balancing/begin-end-blocks` | 46.168 us | 4,345.250 us | 4,379.489 us | 0.01x |
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
| `lokad/docs/inline-doc-prefix` | 1.945 us | 1.414 us | 2.266 us | 1.38x |
| `lokad/imports/module-imports` | 5.999 us | 4.373 us | 29.200 us | 1.37x |
| `lokad/langserv/url-dashboard` | 8.494 us | 6.227 us | 6.677 us | 1.36x |
| `structural/ordered-keyword-window` | 11.131 us | 8.322 us | 22.149 us | 1.34x |
| `structural/keyword-family-to-capitalized-identifier` | 10.142 us | 7.639 us | 22.510 us | 1.33x |
| `fallback/lookahead` | 5.297 us | 4.172 us | 17.825 us | 1.27x |
| `lokad/lexer/operator-run` | 1.136 us | 0.924 us | 1.370 us | 1.23x |
| `structural/ordered-modifier-pair` | 9.917 us | 8.305 us | 22.573 us | 1.19x |
| `lokad/lexer/identifier` | 8.623 us | 7.776 us | 10.545 us | 1.11x |
| `lokad/langserv/identifier-validator` | 4.656 us | 4.201 us | 5.715 us | 1.11x |
| `lokad/style/range-ref` | 0.785 us | 0.714 us | 0.854 us | 1.10x |
| `structural/modifier-family-to-type-window` | 59.673 us | 54.568 us | 70.684 us | 1.09x |
| `lokad/lexer/doc-line` | 1.121 us | 1.046 us | 1.839 us | 1.07x |
| `lokad/lexer/string` | 31.244 us | 29.541 us | 32.650 us | 1.06x |
| `structural/method-family-call` | 5.229 us | 4.985 us | 18.962 us | 1.05x |
| `lokad/style/cell-ref` | 0.839 us | 0.813 us | 1.038 us | 1.03x |
| `lokad/langserv/color-short-hex` | 0.497 us | 0.483 us | 0.673 us | 1.03x |
| `structural/method-family-generic-call` | 4.330 us | 4.539 us | 18.176 us | 0.95x |
| `lokad/style/hex-color` | 0.215 us | 0.226 us | 0.305 us | 0.95x |
| `lokad/langserv/url-download` | 11.225 us | 11.929 us | 12.463 us | 0.94x |
| `lokad/langserv/helper-identifier` | 2.425 us | 2.662 us | 3.518 us | 0.91x |
| `lokad/lexer/number` | 3.141 us | 3.771 us | 4.826 us | 0.83x |
| `literal-family/type-token-family` | 8.975 us | 11.168 us | 25.595 us | 0.80x |
| `structural/ordered-identifier-window` | 8.182 us | 10.392 us | 24.611 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 0.635 us | 0.831 us | 1.090 us | 0.76x |
| `literal/identifier-token-casei` | 3.788 us | 5.440 us | 20.256 us | 0.70x |
| `structural/keyword-to-generic-type` | 2.246 us | 3.446 us | 17.524 us | 0.65x |
| `structural/ordered-keyword-pair` | 2.499 us | 4.057 us | 17.668 us | 0.62x |
| `literal-family/method-token-family` | 2.581 us | 5.259 us | 19.089 us | 0.49x |
| `literal/identifier-token` | 2.202 us | 4.555 us | 18.056 us | 0.48x |
| `literal/call-token` | 1.825 us | 3.906 us | 17.227 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.882 us | 10.434 us | 25.258 us | 0.28x |
| `lokad/folding/region-marker` | 5.542 us | 35.443 us | 64.335 us | 0.16x |
| `balancing/begin-end-blocks` | 46.945 us | 457.443 us | 487.414 us | 0.10x |
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
