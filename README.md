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
| `literal/sherlock-zh` | 21.556 us | 18.226 us | 2,076.805 us | 1.18x |
| `literal/sherlock-ru` | 68.238 us | 97.370 us | 5,137.307 us | 0.70x |
| `literal-alternate/sherlock-zh` | 41.731 us | 65.845 us | 2,126.072 us | 0.63x |
| `ruff-noqa/tweaked` | 3,815.500 us | 7,202.436 us | 37,657.300 us | 0.53x |
| `literal/sherlock-casei-en` | 77.579 us | 183.535 us | 614.198 us | 0.42x |
| `literal/sherlock-casei-ru` | 148.856 us | 353.069 us | 5,475.607 us | 0.42x |
| `bounded-repeat/letters-en` | 1,061.110 us | 2,717.297 us | 2,889.015 us | 0.39x |
| `literal/sherlock-en` | 31.008 us | 84.322 us | 475.132 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,049.189 us | 5,574.879 us | 6,423.575 us | 0.37x |
| `dictionary/single` | 7,754.160 us | 25,098.750 us | 25,036.125 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,388.618 us | 4,572.206 us | 9,735.450 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 154.574 us | 569.536 us | 995.299 us | 0.27x |
| `bounded-repeat/capitals` | 11,151.300 us | 43,107.300 us | 47,414.250 us | 0.26x |
| `literal-alternate/sherlock-en` | 336.966 us | 1,483.656 us | 1,979.105 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 124.225 us | 623.946 us | 1,036.428 us | 0.20x |
| `aws-keys/quick` | 1,844.783 us | 9,517.938 us | 45,363.300 us | 0.19x |
| `aws-keys/full` | 8,413.589 us | 82,646.600 us | 114,469.200 us | 0.10x |
| `bounded-repeat/context` | 8,573.900 us | 140,963.300 us | 145,962.200 us | 0.06x |
| `ruff-noqa/real` | 3,369.504 us | 67,574.850 us | 100,448.200 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 821.224 us | 22,613.550 us | 27,621.500 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 103.547 us | 10,951.857 us | 11,797.614 us | 0.0095x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 381.132 us | 489.747 us | 8,671.000 us | 0.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.104 us | 0.140 us | 0.73x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.073 us | 0.087 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.067 us | 0.104 us | 0.132 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.080 us | 0.096 us | 0.63x |
| `industry/mariomka-uri-count` | `Count` | 1,135.666 us | 1,881.961 us | 10,042.650 us | 0.60x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.042 us | 0.079 us | 0.096 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.126 us | 0.263 us | 0.288 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 610.665 us | 1,286.728 us | 8,423.478 us | 0.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.071 us | 0.155 us | 0.171 us | 0.46x |
| `common/uri-miss` | `IsMatch` | 0.077 us | 0.181 us | 0.199 us | 0.43x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.167 us | 0.186 us | 0.42x |
| `common/match-word` | `Match` | 0.048 us | 0.122 us | 1.140 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,091.260 us | 8,022.740 us | 16,316.067 us | 0.39x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.225 us | 0.259 us | 0.38x |
| `common/matches-set` | `Count` | 65.112 us | 185.514 us | 186.627 us | 0.35x |
| `common/ip-miss` | `IsMatch` | 0.214 us | 0.692 us | 0.718 us | 0.31x |
| `common/matches-word` | `Count` | 1.180 us | 4.079 us | 5.768 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 68.705 us | 254.348 us | 479.455 us | 0.27x |
| `common/replace-words` | `Replace` | 20.016 us | 75.666 us | 77.637 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.104 us | 0.537 us | 0.569 us | 0.19x |
| `common/matches-boundary` | `Count` | 29.743 us | 159.517 us | 161.007 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.212 us | 919.658 us | 1,189.264 us | 0.17x |
| `common/split-words` | `Split` | 12.761 us | 75.149 us | 76.255 us | 0.17x |
| `common/matches-words` | `Count` | 8.756 us | 67.119 us | 68.356 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,053.208 us | 9,538.122 us | 16,498.667 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,850.598 us | 18,578.140 us | 19,293.400 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 861.423 us | 11,329.829 us | 11,961.786 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,578.404 us | 30,470.900 us | 37,875.000 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.596 us | 0.629 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.328 us | 0.348 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.800 us | 0.840 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 938.248 us | 21,604.350 us | 28,443.433 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 280.097 us | 8,348.740 us | 8,844.556 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 41.437 us | 34.240 us | 2,104.136 us | 1.21x |
| `literal/sherlock-casei-en` | 77.635 us | 75.484 us | 422.413 us | 1.03x |
| `literal-alternate/sherlock-en` | 337.455 us | 332.716 us | 738.786 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,044.051 us | 2,048.481 us | 2,883.235 us | 1.00x |
| `literal/sherlock-zh` | 21.501 us | 23.272 us | 2,111.984 us | 0.92x |
| `literal-alternate/sherlock-en-mixed` | 156.849 us | 171.092 us | 526.410 us | 0.92x |
| `ruff-noqa/tweaked` | 3,834.755 us | 4,341.667 us | 35,117.367 us | 0.88x |
| `bounded-repeat/capitals` | 11,187.671 us | 12,934.500 us | 15,912.850 us | 0.86x |
| `literal/sherlock-ru` | 68.231 us | 85.044 us | 5,159.793 us | 0.80x |
| `literal/sherlock-casei-ru` | 148.402 us | 188.529 us | 5,232.633 us | 0.79x |
| `literal-alternate/sherlock-casei-en` | 103.443 us | 141.645 us | 549.702 us | 0.73x |
| `literal-alternate/sherlock-en-nomatch` | 123.826 us | 193.147 us | 562.665 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,389.715 us | 2,241.968 us | 7,258.625 us | 0.62x |
| `bounded-repeat/letters-en` | 1,063.392 us | 1,771.536 us | 1,910.733 us | 0.60x |
| `aws-keys/quick` | 1,854.441 us | 3,586.714 us | 36,886.550 us | 0.52x |
| `aws-keys/full` | 8,390.033 us | 17,784.200 us | 48,999.300 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 821.774 us | 1,882.803 us | 6,869.975 us | 0.44x |
| `literal/sherlock-en` | 31.157 us | 73.999 us | 415.018 us | 0.42x |
| `dictionary/single` | 7,752.250 us | 43,969.950 us | 43,953.950 us | 0.18x |
| `ruff-noqa/real` | 3,402.209 us | 19,969.475 us | 54,210.633 us | 0.17x |
| `bounded-repeat/context` | 8,608.922 us | 65,739.350 us | 69,061.250 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.066 us | 0.042 us | 0.070 us | 1.59x |
| `common/email-match` | `IsMatch` | 0.123 us | 0.077 us | 0.104 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.067 us | 0.046 us | 0.065 us | 1.47x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.043 us | 0.031 us | 0.049 us | 1.39x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.068 us | 0.053 us | 0.073 us | 1.29x |
| `common/uri-miss` | `IsMatch` | 0.076 us | 0.060 us | 0.081 us | 1.27x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.042 us | 0.034 us | 0.051 us | 1.24x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 69.461 us | 56.406 us | 256.695 us | 1.23x |
| `common/ip-miss` | `IsMatch` | 0.208 us | 0.173 us | 0.196 us | 1.20x |
| `common/replace-words` | `Replace` | 19.898 us | 16.790 us | 18.178 us | 1.19x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.036 us | 0.031 us | 0.047 us | 1.17x |
| `industry/leipzig-river-window-count` | `Count` | 1,614.692 us | 1,461.473 us | 7,706.840 us | 1.10x |
| `industry/mariomka-uri-count` | `Count` | 1,134.294 us | 1,079.581 us | 8,147.525 us | 1.05x |
| `common/match-word` | `Match` | 0.048 us | 0.047 us | 0.935 us | 1.04x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.049 us | 0.048 us | 0.083 us | 1.02x |
| `common/uri-match` | `IsMatch` | 0.082 us | 0.085 us | 0.116 us | 0.97x |
| `industry/mariomka-ip-count` | `Count` | 3,082.400 us | 3,305.817 us | 10,509.900 us | 0.93x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.041 us | 0.071 us | 0.90x |
| `common/matches-words` | `Count` | 8.817 us | 10.545 us | 13.650 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 381.931 us | 459.115 us | 7,384.750 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 942.688 us | 1,162.222 us | 6,649.040 us | 0.81x |
| `common/matches-set` | `Count` | 64.922 us | 101.405 us | 104.008 us | 0.64x |
| `common/split-words` | `Split` | 12.870 us | 20.394 us | 22.421 us | 0.63x |
| `common/date-miss` | `IsMatch` | 0.104 us | 0.194 us | 0.230 us | 0.54x |
| `common/matches-word` | `Count` | 1.178 us | 2.241 us | 3.371 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 617.640 us | 1,258.972 us | 7,351.660 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,063.189 us | 3,040.960 us | 8,025.750 us | 0.35x |
| `common/matches-boundary` | `Count` | 29.938 us | 100.707 us | 101.814 us | 0.30x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.103 us | 0.124 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.147 us | 566.290 us | 808.629 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,853.385 us | 8,282.267 us | 8,735.411 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 858.593 us | 4,596.882 us | 5,010.347 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.085 us | 0.112 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 277.932 us | 3,834.300 us | 4,190.147 us | 0.07x |
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
| `lokad/imports/module-imports` | 5.909 us | 6.315 us | 31.585 us | 0.94x |
| `fallback/lookahead` | 5.028 us | 5.775 us | 19.280 us | 0.87x |
| `structural/keyword-to-generic-type` | 2.925 us | 3.928 us | 17.161 us | 0.74x |
| `structural/method-family-generic-call` | 4.151 us | 6.351 us | 19.723 us | 0.65x |
| `structural/method-family-call` | 5.092 us | 7.910 us | 20.863 us | 0.64x |
| `structural/ordered-keyword-window` | 11.154 us | 17.393 us | 30.051 us | 0.64x |
| `lokad/langserv/color-short-hex` | 0.491 us | 0.794 us | 0.965 us | 0.62x |
| `structural/ordered-keyword-pair` | 3.146 us | 5.155 us | 18.775 us | 0.61x |
| `lokad/lexer/identifier` | 7.983 us | 13.327 us | 16.412 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.107 us | 16.940 us | 29.798 us | 0.60x |
| `lokad/langserv/identifier-validator` | 4.184 us | 7.171 us | 8.720 us | 0.58x |
| `lokad/lexer/doc-line` | 1.007 us | 1.830 us | 2.549 us | 0.55x |
| `literal/identifier-token` | 2.230 us | 4.150 us | 16.688 us | 0.54x |
| `lokad/docs/inline-doc-prefix` | 1.795 us | 3.447 us | 4.110 us | 0.52x |
| `lokad/style/hex-color` | 0.255 us | 0.502 us | 0.581 us | 0.51x |
| `lokad/langserv/helper-identifier` | 2.275 us | 4.542 us | 5.498 us | 0.50x |
| `lokad/lexer/operator-run` | 1.031 us | 2.270 us | 2.737 us | 0.45x |
| `literal/call-token` | 1.769 us | 4.131 us | 16.720 us | 0.43x |
| `lokad/style/cell-ref` | 0.759 us | 1.805 us | 1.982 us | 0.42x |
| `lokad/style/range-ref` | 0.729 us | 1.768 us | 1.944 us | 0.41x |
| `lokad/lexer/string` | 30.935 us | 78.514 us | 83.340 us | 0.39x |
| `lokad/langserv/url-dashboard` | 8.834 us | 22.620 us | 22.892 us | 0.39x |
| `literal-family/type-token-family` | 8.875 us | 23.071 us | 36.649 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.616 us | 1.767 us | 1.996 us | 0.35x |
| `structural/ordered-identifier-window` | 7.907 us | 27.818 us | 41.366 us | 0.28x |
| `literal-family/method-token-family` | 2.520 us | 8.992 us | 21.144 us | 0.28x |
| `lokad/lexer/number` | 2.833 us | 10.581 us | 11.466 us | 0.27x |
| `literal/identifier-token-casei` | 3.760 us | 14.537 us | 27.420 us | 0.26x |
| `structural/modifier-family-to-type-window` | 56.774 us | 248.711 us | 263.135 us | 0.23x |
| `lokad/langserv/url-download` | 11.142 us | 50.297 us | 50.629 us | 0.22x |
| `lokad/folding/region-marker` | 5.686 us | 35.536 us | 61.536 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.650 us | 209.601 us | 223.553 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.877 us | 126.070 us | 139.176 us | 0.02x |
| `balancing/begin-end-blocks` | 45.658 us | 4,102.300 us | 4,051.400 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 8.806 us | 6.047 us | 6.645 us | 1.46x |
| `lokad/imports/module-imports` | 5.899 us | 4.366 us | 28.773 us | 1.35x |
| `structural/ordered-keyword-window` | 11.227 us | 8.399 us | 20.856 us | 1.34x |
| `lokad/docs/inline-doc-prefix` | 1.784 us | 1.406 us | 2.125 us | 1.27x |
| `structural/keyword-family-to-capitalized-identifier` | 9.586 us | 7.674 us | 20.786 us | 1.25x |
| `fallback/lookahead` | 5.048 us | 4.097 us | 17.026 us | 1.23x |
| `structural/ordered-modifier-pair` | 10.081 us | 8.427 us | 21.082 us | 1.20x |
| `lokad/lexer/operator-run` | 1.008 us | 0.916 us | 1.369 us | 1.10x |
| `lokad/lexer/string` | 30.937 us | 29.180 us | 32.181 us | 1.06x |
| `structural/modifier-family-to-type-window` | 57.267 us | 54.257 us | 67.896 us | 1.06x |
| `lokad/langserv/identifier-validator` | 4.178 us | 4.012 us | 5.603 us | 1.04x |
| `lokad/lexer/identifier` | 8.018 us | 7.748 us | 10.559 us | 1.03x |
| `structural/method-family-call` | 5.117 us | 4.949 us | 17.507 us | 1.03x |
| `lokad/style/range-ref` | 0.723 us | 0.715 us | 0.845 us | 1.01x |
| `lokad/lexer/doc-line` | 1.002 us | 1.016 us | 1.782 us | 0.99x |
| `lokad/langserv/color-short-hex` | 0.461 us | 0.468 us | 0.656 us | 0.98x |
| `lokad/style/cell-ref` | 0.757 us | 0.793 us | 0.996 us | 0.96x |
| `structural/method-family-generic-call` | 4.152 us | 4.431 us | 17.078 us | 0.94x |
| `lokad/langserv/url-download` | 11.077 us | 11.833 us | 12.337 us | 0.94x |
| `lokad/langserv/helper-identifier` | 2.278 us | 2.595 us | 3.510 us | 0.88x |
| `lokad/style/hex-color` | 0.196 us | 0.224 us | 0.305 us | 0.87x |
| `structural/keyword-to-generic-type` | 2.884 us | 3.360 us | 16.069 us | 0.86x |
| `literal-family/type-token-family` | 8.897 us | 11.078 us | 23.866 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.142 us | 3.980 us | 16.781 us | 0.79x |
| `lokad/lexer/number` | 2.881 us | 3.747 us | 4.794 us | 0.77x |
| `structural/ordered-identifier-window` | 7.925 us | 10.385 us | 23.836 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 0.563 us | 0.814 us | 1.072 us | 0.69x |
| `literal/identifier-token-casei` | 3.771 us | 5.706 us | 18.418 us | 0.66x |
| `literal-family/method-token-family` | 2.536 us | 4.873 us | 17.159 us | 0.52x |
| `literal/identifier-token` | 2.245 us | 4.501 us | 16.419 us | 0.50x |
| `literal/call-token` | 1.769 us | 3.817 us | 15.871 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 2.859 us | 10.345 us | 23.701 us | 0.28x |
| `lokad/folding/region-marker` | 5.574 us | 35.149 us | 61.713 us | 0.16x |
| `balancing/begin-end-blocks` | 45.954 us | 454.581 us | 479.133 us | 0.10x |
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
