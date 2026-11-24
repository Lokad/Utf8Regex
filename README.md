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
| `literal/sherlock-zh` | 22.397 us | 18.893 us | 2,207.671 us | 1.19x |
| `literal/sherlock-ru` | 71.353 us | 98.213 us | 5,257.753 us | 0.73x |
| `literal-alternate/sherlock-zh` | 43.471 us | 69.009 us | 2,201.046 us | 0.63x |
| `ruff-noqa/tweaked` | 4,115.378 us | 7,753.782 us | 42,201.800 us | 0.53x |
| `literal/sherlock-casei-ru` | 154.457 us | 370.735 us | 5,417.036 us | 0.42x |
| `literal/sherlock-casei-en` | 80.160 us | 201.604 us | 608.275 us | 0.40x |
| `bounded-repeat/letters-en` | 1,109.182 us | 2,809.807 us | 2,970.765 us | 0.39x |
| `literal/sherlock-en` | 31.639 us | 86.500 us | 459.639 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,120.419 us | 5,974.815 us | 6,870.273 us | 0.35x |
| `dictionary/single` | 8,331.090 us | 26,165.400 us | 26,191.600 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,464.500 us | 4,847.719 us | 10,033.350 us | 0.30x |
| `bounded-repeat/capitals` | 12,030.371 us | 47,139.150 us | 53,830.850 us | 0.26x |
| `literal-alternate/sherlock-en-mixed` | 158.105 us | 624.150 us | 1,044.317 us | 0.25x |
| `literal-alternate/sherlock-en` | 346.618 us | 1,501.778 us | 2,007.203 us | 0.23x |
| `aws-keys/quick` | 1,967.133 us | 10,006.150 us | 47,074.000 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 129.296 us | 658.974 us | 1,096.454 us | 0.20x |
| `aws-keys/full` | 8,557.711 us | 86,835.300 us | 118,133.100 us | 0.10x |
| `bounded-repeat/context` | 9,694.867 us | 147,418.400 us | 151,497.600 us | 0.07x |
| `ruff-noqa/real` | 3,551.464 us | 70,358.100 us | 87,168.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 871.129 us | 23,186.025 us | 28,486.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 105.113 us | 11,446.371 us | 12,293.200 us | 0.0092x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.081 us | 0.077 us | 0.092 us | 1.05x |
| `common/date-match` | `IsMatch` | 0.115 us | 0.110 us | 0.139 us | 1.05x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.083 us | 0.082 us | 0.098 us | 1.02x |
| `common/ip-match` | `IsMatch` | 0.170 us | 0.175 us | 0.197 us | 0.97x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.077 us | 0.085 us | 0.102 us | 0.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.096 us | 0.112 us | 0.146 us | 0.86x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.132 us | 0.169 us | 0.189 us | 0.78x |
| `industry/mariomka-email-count` | `Count` | 380.793 us | 495.072 us | 9,055.275 us | 0.77x |
| `common/uri-miss` | `IsMatch` | 0.136 us | 0.184 us | 0.203 us | 0.74x |
| `common/uri-match` | `IsMatch` | 0.160 us | 0.234 us | 0.272 us | 0.68x |
| `industry/mariomka-uri-count` | `Count` | 1,220.798 us | 1,889.350 us | 10,629.350 us | 0.65x |
| `common/email-match` | `IsMatch` | 0.155 us | 0.271 us | 0.296 us | 0.57x |
| `common/ip-miss` | `IsMatch` | 0.387 us | 0.714 us | 0.733 us | 0.54x |
| `common/match-word` | `Match` | 0.067 us | 0.132 us | 1.185 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 634.777 us | 1,335.405 us | 8,728.340 us | 0.48x |
| `industry/mariomka-ip-count` | `Count` | 3,329.830 us | 8,408.030 us | 16,939.867 us | 0.40x |
| `common/date-miss` | `IsMatch` | 0.217 us | 0.569 us | 0.602 us | 0.38x |
| `common/matches-set` | `Count` | 62.526 us | 194.440 us | 195.664 us | 0.32x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 80.672 us | 267.836 us | 509.886 us | 0.30x |
| `common/matches-word` | `Count` | 1.242 us | 4.259 us | 5.990 us | 0.29x |
| `common/replace-words` | `Replace` | 21.589 us | 76.671 us | 78.422 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.130 us | 965.608 us | 1,243.377 us | 0.18x |
| `common/split-words` | `Split` | 13.297 us | 79.903 us | 81.285 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.842 us | 168.585 us | 169.366 us | 0.14x |
| `common/matches-words` | `Count` | 9.223 us | 70.393 us | 72.017 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,049.394 us | 9,961.275 us | 17,383.833 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,917.728 us | 19,584.725 us | 20,140.125 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 892.227 us | 11,864.086 us | 12,429.271 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.618 us | 0.652 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,506.586 us | 32,289.000 us | 39,981.800 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.039 us | 0.833 us | 0.876 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.348 us | 0.372 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,012.795 us | 24,517.550 us | 31,865.100 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 283.127 us | 8,478.122 us | 9,009.133 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.663 us | 36.692 us | 2,154.151 us | 1.19x |
| `bounded-repeat/letters-ru` | 2,126.614 us | 2,099.264 us | 2,998.700 us | 1.01x |
| `literal/sherlock-casei-en` | 80.092 us | 79.914 us | 443.394 us | 1.00x |
| `literal/sherlock-zh` | 22.453 us | 24.088 us | 2,188.779 us | 0.93x |
| `literal-alternate/sherlock-en` | 346.762 us | 374.970 us | 815.152 us | 0.92x |
| `ruff-noqa/tweaked` | 4,172.437 us | 4,630.453 us | 37,312.100 us | 0.90x |
| `literal-alternate/sherlock-en-mixed` | 158.112 us | 176.746 us | 534.803 us | 0.89x |
| `bounded-repeat/capitals` | 12,012.200 us | 13,620.750 us | 20,736.975 us | 0.88x |
| `literal/sherlock-ru` | 71.324 us | 90.008 us | 5,241.333 us | 0.79x |
| `literal-alternate/sherlock-casei-en` | 103.882 us | 146.796 us | 593.338 us | 0.71x |
| `literal/sherlock-casei-ru` | 155.254 us | 223.910 us | 5,295.421 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,460.545 us | 2,356.537 us | 7,491.625 us | 0.62x |
| `literal-alternate/sherlock-en-nomatch` | 128.658 us | 209.894 us | 575.250 us | 0.61x |
| `bounded-repeat/letters-en` | 1,110.090 us | 1,832.485 us | 1,968.426 us | 0.61x |
| `aws-keys/quick` | 1,980.708 us | 3,761.000 us | 40,097.300 us | 0.53x |
| `aws-keys/full` | 8,559.700 us | 18,902.120 us | 51,469.733 us | 0.45x |
| `literal-alternate/sherlock-casei-ru` | 873.414 us | 1,961.441 us | 7,160.600 us | 0.45x |
| `literal/sherlock-en` | 31.680 us | 74.941 us | 447.906 us | 0.42x |
| `dictionary/single` | 8,326.533 us | 45,912.350 us | 45,978.700 us | 0.18x |
| `ruff-noqa/real` | 3,475.991 us | 21,539.050 us | 48,207.767 us | 0.16x |
| `bounded-repeat/context` | 9,031.556 us | 69,210.650 us | 73,277.150 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.166 us | 0.050 us | 0.071 us | 3.32x |
| `common/date-match` | `IsMatch` | 0.114 us | 0.043 us | 0.072 us | 2.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.083 us | 0.033 us | 0.050 us | 2.52x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.077 us | 0.032 us | 0.051 us | 2.43x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.131 us | 0.056 us | 0.077 us | 2.33x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.079 us | 0.036 us | 0.053 us | 2.19x |
| `common/ip-miss` | `IsMatch` | 0.385 us | 0.180 us | 0.202 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.137 us | 0.066 us | 0.086 us | 2.06x |
| `common/email-match` | `IsMatch` | 0.154 us | 0.080 us | 0.108 us | 1.93x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.089 us | 0.049 us | 0.085 us | 1.84x |
| `common/uri-match` | `IsMatch` | 0.161 us | 0.089 us | 0.122 us | 1.80x |
| `common/match-word` | `Match` | 0.067 us | 0.048 us | 0.956 us | 1.39x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 80.544 us | 58.382 us | 270.433 us | 1.38x |
| `common/replace-words` | `Replace` | 21.584 us | 18.233 us | 21.524 us | 1.18x |
| `industry/mariomka-uri-count` | `Count` | 1,235.775 us | 1,124.303 us | 8,544.325 us | 1.10x |
| `common/date-miss` | `IsMatch` | 0.213 us | 0.202 us | 0.238 us | 1.05x |
| `industry/mariomka-ip-count` | `Count` | 3,330.865 us | 3,501.968 us | 11,015.900 us | 0.95x |
| `industry/leipzig-river-window-count` | `Count` | 1,507.476 us | 1,586.302 us | 8,332.125 us | 0.95x |
| `common/matches-words` | `Count` | 9.210 us | 10.932 us | 14.112 us | 0.84x |
| `common/backtracking` | `IsMatch` | 0.038 us | 0.045 us | 0.074 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 381.526 us | 463.931 us | 7,807.650 us | 0.82x |
| `industry/leipzig-symbol-count` | `Count` | 1,019.393 us | 1,260.208 us | 7,547.950 us | 0.81x |
| `common/split-words` | `Split` | 13.317 us | 21.249 us | 22.989 us | 0.63x |
| `common/matches-set` | `Count` | 63.103 us | 105.941 us | 107.703 us | 0.60x |
| `common/matches-word` | `Count` | 1.239 us | 2.353 us | 3.680 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 635.674 us | 1,308.184 us | 7,668.244 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,046.439 us | 3,031.658 us | 8,619.400 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.108 us | 0.130 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.046 us | 601.777 us | 851.985 us | 0.28x |
| `common/matches-boundary` | `Count` | 23.703 us | 104.672 us | 106.632 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,916.375 us | 8,757.756 us | 9,379.025 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 892.707 us | 4,820.863 us | 5,270.573 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.090 us | 0.116 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 282.493 us | 3,986.132 us | 4,354.189 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.706 us | 6.462 us | 31.430 us | 1.19x |
| `fallback/lookahead` | 6.774 us | 6.051 us | 19.272 us | 1.12x |
| `structural/method-family-generic-call` | 6.344 us | 6.606 us | 20.419 us | 0.96x |
| `structural/method-family-call` | 7.828 us | 8.327 us | 22.091 us | 0.94x |
| `lokad/lexer/doc-line` | 1.672 us | 1.887 us | 2.646 us | 0.89x |
| `lokad/lexer/identifier` | 11.985 us | 13.600 us | 16.634 us | 0.88x |
| `lokad/langserv/identifier-validator` | 6.625 us | 7.611 us | 9.050 us | 0.87x |
| `lokad/langserv/color-short-hex` | 0.722 us | 0.839 us | 1.021 us | 0.86x |
| `lokad/style/hex-color` | 0.433 us | 0.521 us | 0.607 us | 0.83x |
| `structural/ordered-keyword-window` | 13.468 us | 17.736 us | 31.075 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 1.387 us | 1.858 us | 2.074 us | 0.75x |
| `lokad/lexer/operator-run` | 1.712 us | 2.352 us | 2.805 us | 0.73x |
| `lokad/docs/inline-doc-prefix` | 2.501 us | 3.440 us | 4.198 us | 0.73x |
| `structural/keyword-to-generic-type` | 2.955 us | 4.129 us | 17.987 us | 0.72x |
| `structural/ordered-keyword-pair` | 3.144 us | 5.311 us | 19.031 us | 0.59x |
| `lokad/lexer/number` | 6.397 us | 10.866 us | 11.870 us | 0.59x |
| `structural/ordered-modifier-pair` | 9.941 us | 17.619 us | 30.732 us | 0.56x |
| `literal/identifier-token` | 2.379 us | 4.290 us | 17.565 us | 0.55x |
| `lokad/langserv/helper-identifier` | 2.558 us | 4.900 us | 5.791 us | 0.52x |
| `lokad/style/cell-ref` | 0.875 us | 1.902 us | 2.115 us | 0.46x |
| `lokad/style/range-ref` | 0.826 us | 1.842 us | 1.994 us | 0.45x |
| `literal/call-token` | 1.866 us | 4.330 us | 17.477 us | 0.43x |
| `lokad/langserv/url-dashboard` | 9.942 us | 23.433 us | 23.865 us | 0.42x |
| `literal-family/type-token-family` | 9.222 us | 23.441 us | 37.039 us | 0.39x |
| `lokad/lexer/string` | 27.136 us | 81.292 us | 86.218 us | 0.33x |
| `structural/ordered-identifier-window` | 8.600 us | 28.692 us | 42.299 us | 0.30x |
| `literal-family/method-token-family` | 2.652 us | 9.485 us | 22.266 us | 0.28x |
| `structural/modifier-family-to-type-window` | 68.467 us | 258.052 us | 270.854 us | 0.27x |
| `literal/identifier-token-casei` | 3.955 us | 15.057 us | 28.845 us | 0.26x |
| `lokad/langserv/url-download` | 11.578 us | 53.277 us | 53.858 us | 0.22x |
| `lokad/folding/region-marker` | 5.295 us | 35.434 us | 61.316 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 16.101 us | 218.314 us | 235.133 us | 0.07x |
| `structural/modifier-family-to-type-pair` | 2.940 us | 132.943 us | 146.731 us | 0.02x |
| `balancing/begin-end-blocks` | 47.177 us | 4,129.321 us | 4,099.053 us | 0.01x |
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
| `structural/keyword-family-to-capitalized-identifier` | 16.078 us | 7.782 us | 21.830 us | 2.07x |
| `lokad/style/hex-color` | 0.433 us | 0.230 us | 0.314 us | 1.89x |
| `lokad/docs/inline-doc-prefix` | 2.538 us | 1.446 us | 2.189 us | 1.76x |
| `lokad/lexer/operator-run` | 1.702 us | 0.971 us | 1.409 us | 1.75x |
| `lokad/imports/module-imports` | 7.707 us | 4.464 us | 28.307 us | 1.73x |
| `lokad/lexer/number` | 6.390 us | 3.797 us | 4.894 us | 1.68x |
| `lokad/langserv/color-rgb-hex` | 1.391 us | 0.854 us | 1.110 us | 1.63x |
| `fallback/lookahead` | 6.777 us | 4.227 us | 17.002 us | 1.60x |
| `lokad/langserv/url-dashboard` | 9.962 us | 6.346 us | 6.808 us | 1.57x |
| `structural/ordered-keyword-window` | 13.491 us | 8.616 us | 21.679 us | 1.57x |
| `structural/method-family-call` | 7.874 us | 5.105 us | 18.153 us | 1.54x |
| `lokad/langserv/identifier-validator` | 6.626 us | 4.309 us | 5.816 us | 1.54x |
| `lokad/langserv/color-short-hex` | 0.726 us | 0.485 us | 0.679 us | 1.50x |
| `lokad/lexer/identifier` | 11.841 us | 7.981 us | 10.677 us | 1.48x |
| `lokad/lexer/doc-line` | 1.674 us | 1.138 us | 1.873 us | 1.47x |
| `structural/method-family-generic-call` | 6.365 us | 4.637 us | 17.468 us | 1.37x |
| `structural/modifier-family-to-type-window` | 68.347 us | 56.628 us | 75.324 us | 1.21x |
| `structural/ordered-modifier-pair` | 9.949 us | 8.602 us | 21.680 us | 1.16x |
| `lokad/style/range-ref` | 0.828 us | 0.741 us | 0.867 us | 1.12x |
| `lokad/style/cell-ref` | 0.876 us | 0.833 us | 1.049 us | 1.05x |
| `lokad/langserv/url-download` | 11.665 us | 12.191 us | 12.705 us | 0.96x |
| `lokad/langserv/helper-identifier` | 2.556 us | 2.779 us | 3.724 us | 0.92x |
| `lokad/lexer/string` | 27.279 us | 30.603 us | 33.680 us | 0.89x |
| `structural/keyword-to-generic-type` | 2.941 us | 3.523 us | 16.715 us | 0.83x |
| `literal-family/type-token-family` | 9.210 us | 11.468 us | 24.620 us | 0.80x |
| `structural/ordered-identifier-window` | 8.580 us | 10.719 us | 24.574 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.163 us | 4.145 us | 17.136 us | 0.76x |
| `literal/identifier-token-casei` | 3.937 us | 5.909 us | 19.326 us | 0.67x |
| `literal/identifier-token` | 2.361 us | 4.362 us | 17.195 us | 0.54x |
| `literal-family/method-token-family` | 2.645 us | 5.171 us | 18.005 us | 0.51x |
| `literal/call-token` | 1.883 us | 3.987 us | 16.580 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.920 us | 10.847 us | 23.902 us | 0.27x |
| `lokad/folding/region-marker` | 5.236 us | 35.182 us | 61.199 us | 0.15x |
| `balancing/begin-end-blocks` | 47.304 us | 467.533 us | 496.440 us | 0.10x |
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
