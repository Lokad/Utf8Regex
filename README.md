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
| `literal/sherlock-zh` | 14.244 us | 18.913 us | 2,184.886 us | 0.75x |
| `literal/sherlock-ru` | 68.516 us | 102.543 us | 5,346.707 us | 0.67x |
| `literal-alternate/sherlock-zh` | 43.735 us | 68.936 us | 2,218.394 us | 0.63x |
| `ruff-noqa/tweaked` | 3,999.900 us | 8,002.100 us | 44,428.600 us | 0.50x |
| `literal/sherlock-casei-ru` | 148.874 us | 364.521 us | 5,669.892 us | 0.41x |
| `literal/sherlock-casei-en` | 80.688 us | 200.865 us | 724.054 us | 0.40x |
| `bounded-repeat/letters-en` | 1,125.721 us | 2,838.993 us | 2,995.516 us | 0.40x |
| `literal/sherlock-en` | 31.720 us | 82.327 us | 523.510 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,050.300 us | 5,898.338 us | 6,768.358 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,446.310 us | 4,833.450 us | 10,053.350 us | 0.30x |
| `dictionary/single` | 7,629.940 us | 25,886.100 us | 26,018.733 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 175.585 us | 597.554 us | 1,224.292 us | 0.29x |
| `bounded-repeat/capitals` | 11,936.257 us | 46,337.650 us | 50,254.000 us | 0.26x |
| `literal-alternate/sherlock-en` | 369.374 us | 1,543.878 us | 2,062.321 us | 0.24x |
| `aws-keys/quick` | 1,908.425 us | 9,464.444 us | 48,023.500 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 128.039 us | 660.668 us | 1,170.139 us | 0.19x |
| `aws-keys/full` | 8,743.511 us | 86,200.200 us | 119,840.100 us | 0.10x |
| `bounded-repeat/context` | 9,095.900 us | 148,935.000 us | 152,830.300 us | 0.06x |
| `ruff-noqa/real` | 3,619.281 us | 74,235.700 us | 90,850.000 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 857.270 us | 23,268.150 us | 28,443.150 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 105.600 us | 12,065.571 us | 12,851.633 us | 0.0088x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.087 us | 0.112 us | 0.149 us | 0.77x |
| `common/ip-match` | `IsMatch` | 0.118 us | 0.174 us | 0.194 us | 0.68x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.056 us | 0.082 us | 0.098 us | 0.68x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.076 us | 0.090 us | 0.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.057 us | 0.085 us | 0.102 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.072 us | 0.110 us | 0.139 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,204.967 us | 1,897.893 us | 10,558.633 us | 0.63x |
| `industry/mariomka-email-count` | `Count` | 277.699 us | 504.423 us | 8,965.725 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.142 us | 0.270 us | 0.298 us | 0.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.082 us | 0.165 us | 0.181 us | 0.49x |
| `common/uri-miss` | `IsMatch` | 0.098 us | 0.204 us | 0.222 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 633.465 us | 1,332.893 us | 8,694.400 us | 0.48x |
| `common/match-word` | `Match` | 0.052 us | 0.129 us | 1.181 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.239 us | 0.276 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,154.217 us | 8,280.022 us | 16,835.767 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.281 us | 0.768 us | 0.784 us | 0.37x |
| `common/matches-set` | `Count` | 63.687 us | 195.587 us | 198.016 us | 0.33x |
| `common/matches-word` | `Count` | 1.252 us | 4.256 us | 6.049 us | 0.29x |
| `common/replace-words` | `Replace` | 20.804 us | 77.554 us | 78.560 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.894 us | 263.457 us | 519.943 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.115 us | 0.560 us | 0.592 us | 0.20x |
| `common/matches-boundary` | `Count` | 29.247 us | 165.930 us | 165.830 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.360 us | 974.583 us | 1,277.697 us | 0.17x |
| `common/split-words` | `Split` | 13.199 us | 80.553 us | 81.895 us | 0.16x |
| `common/matches-words` | `Count` | 9.204 us | 71.227 us | 71.555 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,103.660 us | 9,900.612 us | 25,716.433 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,836.956 us | 19,779.150 us | 20,323.850 us | 0.09x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.617 us | 0.651 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.348 us | 0.373 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,672.398 us | 32,163.333 us | 39,959.750 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,018.295 us | 22,980.200 us | 30,320.800 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.834 us | 0.877 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 244.787 us | 8,932.689 us | 9,558.612 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 290.486 us | 12,265.471 us | 12,687.500 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.530 us | 35.851 us | 2,135.697 us | 1.21x |
| `literal-alternate/sherlock-en` | 368.949 us | 347.191 us | 838.860 us | 1.06x |
| `literal/sherlock-casei-en` | 80.663 us | 78.125 us | 480.676 us | 1.03x |
| `literal-alternate/sherlock-en-mixed` | 175.546 us | 176.552 us | 660.170 us | 0.99x |
| `bounded-repeat/letters-ru` | 2,044.573 us | 2,093.249 us | 2,972.885 us | 0.98x |
| `bounded-repeat/capitals` | 12,002.471 us | 13,652.550 us | 16,663.825 us | 0.88x |
| `ruff-noqa/tweaked` | 4,017.489 us | 4,619.688 us | 40,074.600 us | 0.87x |
| `literal/sherlock-ru` | 68.447 us | 90.611 us | 5,282.027 us | 0.76x |
| `literal/sherlock-casei-ru` | 147.191 us | 196.305 us | 5,462.050 us | 0.75x |
| `literal-alternate/sherlock-casei-en` | 103.470 us | 147.746 us | 772.198 us | 0.70x |
| `literal-alternate/sherlock-en-nomatch` | 127.317 us | 202.296 us | 595.208 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,454.888 us | 2,349.062 us | 7,557.900 us | 0.62x |
| `bounded-repeat/letters-en` | 1,125.539 us | 1,852.644 us | 1,982.076 us | 0.61x |
| `literal/sherlock-zh` | 14.175 us | 24.128 us | 2,230.609 us | 0.59x |
| `aws-keys/quick` | 1,912.562 us | 3,603.181 us | 39,986.100 us | 0.53x |
| `aws-keys/full` | 8,920.400 us | 18,638.320 us | 51,312.667 us | 0.48x |
| `literal/sherlock-en` | 31.630 us | 72.183 us | 431.863 us | 0.44x |
| `literal-alternate/sherlock-casei-ru` | 858.428 us | 1,971.954 us | 7,102.200 us | 0.44x |
| `ruff-noqa/real` | 3,636.710 us | 21,224.400 us | 36,764.200 us | 0.17x |
| `dictionary/single` | 7,619.180 us | 45,921.650 us | 46,075.550 us | 0.17x |
| `bounded-repeat/context` | 9,062.600 us | 69,080.850 us | 72,769.150 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.116 us | 0.047 us | 0.068 us | 2.45x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.042 us | 0.075 us | 1.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.031 us | 0.050 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.052 us | 0.033 us | 0.049 us | 1.58x |
| `common/ip-miss` | `IsMatch` | 0.271 us | 0.178 us | 0.206 us | 1.52x |
| `common/uri-miss` | `IsMatch` | 0.098 us | 0.067 us | 0.087 us | 1.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.056 us | 0.077 us | 1.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.047 us | 0.036 us | 0.053 us | 1.30x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 68.959 us | 58.737 us | 292.912 us | 1.17x |
| `common/replace-words` | `Replace` | 20.809 us | 17.851 us | 20.747 us | 1.17x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.054 us | 0.049 us | 0.087 us | 1.10x |
| `industry/mariomka-uri-count` | `Count` | 1,205.052 us | 1,097.175 us | 8,571.367 us | 1.10x |
| `common/email-match` | `IsMatch` | 0.087 us | 0.080 us | 0.109 us | 1.09x |
| `common/match-word` | `Match` | 0.052 us | 0.048 us | 0.948 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.090 us | 0.121 us | 1.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,677.259 us | 1,616.716 us | 8,635.150 us | 1.04x |
| `industry/mariomka-ip-count` | `Count` | 3,173.180 us | 3,455.118 us | 10,960.367 us | 0.92x |
| `common/matches-words` | `Count` | 9.185 us | 10.998 us | 14.089 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,018.973 us | 1,227.494 us | 7,049.600 us | 0.83x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.044 us | 0.074 us | 0.74x |
| `common/split-words` | `Split` | 13.187 us | 20.620 us | 22.387 us | 0.64x |
| `common/matches-set` | `Count` | 63.533 us | 106.289 us | 108.524 us | 0.60x |
| `industry/mariomka-email-count` | `Count` | 279.110 us | 472.989 us | 7,800.300 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.114 us | 0.200 us | 0.236 us | 0.57x |
| `common/matches-word` | `Count` | 1.247 us | 2.368 us | 3.825 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 636.232 us | 1,299.507 us | 7,412.587 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,118.221 us | 3,047.188 us | 14,445.200 us | 0.37x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.107 us | 0.128 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.624 us | 597.994 us | 862.146 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.869 us | 104.739 us | 106.646 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,841.656 us | 8,858.844 us | 9,470.475 us | 0.21x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.090 us | 0.116 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 241.703 us | 3,974.165 us | 4,327.972 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 292.782 us | 4,815.587 us | 5,221.021 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.098 us | 6.506 us | 32.876 us | 0.94x |
| `fallback/lookahead` | 5.499 us | 6.076 us | 20.439 us | 0.90x |
| `lokad/langserv/color-short-hex` | 0.559 us | 0.823 us | 1.015 us | 0.68x |
| `structural/method-family-generic-call` | 4.443 us | 6.595 us | 21.515 us | 0.67x |
| `structural/ordered-keyword-window` | 11.684 us | 17.807 us | 32.393 us | 0.66x |
| `structural/method-family-call` | 5.451 us | 8.421 us | 23.038 us | 0.65x |
| `structural/ordered-modifier-pair` | 10.981 us | 17.059 us | 32.190 us | 0.64x |
| `lokad/langserv/identifier-validator` | 4.805 us | 7.492 us | 9.167 us | 0.64x |
| `lokad/lexer/identifier` | 8.999 us | 14.125 us | 16.914 us | 0.64x |
| `lokad/lexer/doc-line` | 1.147 us | 1.907 us | 2.644 us | 0.60x |
| `lokad/docs/inline-doc-prefix` | 2.048 us | 3.503 us | 4.288 us | 0.58x |
| `structural/keyword-to-generic-type` | 2.305 us | 4.100 us | 19.199 us | 0.56x |
| `lokad/langserv/helper-identifier` | 2.477 us | 4.759 us | 5.607 us | 0.52x |
| `lokad/lexer/operator-run` | 1.200 us | 2.310 us | 2.753 us | 0.52x |
| `literal/identifier-token` | 2.251 us | 4.353 us | 19.252 us | 0.52x |
| `lokad/style/hex-color` | 0.269 us | 0.528 us | 0.609 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.578 us | 5.362 us | 20.775 us | 0.48x |
| `lokad/style/cell-ref` | 0.861 us | 1.899 us | 2.101 us | 0.45x |
| `lokad/style/range-ref` | 0.819 us | 1.832 us | 1.978 us | 0.45x |
| `literal/call-token` | 1.854 us | 4.201 us | 18.861 us | 0.44x |
| `lokad/lexer/string` | 32.414 us | 81.444 us | 86.584 us | 0.40x |
| `literal-family/type-token-family` | 9.295 us | 23.376 us | 38.601 us | 0.40x |
| `lokad/langserv/color-rgb-hex` | 0.698 us | 1.869 us | 2.097 us | 0.37x |
| `lokad/langserv/url-dashboard` | 8.729 us | 23.493 us | 23.903 us | 0.37x |
| `lokad/lexer/number` | 3.234 us | 11.009 us | 12.133 us | 0.29x |
| `structural/ordered-identifier-window` | 8.374 us | 29.423 us | 44.015 us | 0.28x |
| `literal-family/method-token-family` | 2.631 us | 9.348 us | 23.632 us | 0.28x |
| `literal/identifier-token-casei` | 3.966 us | 14.828 us | 29.398 us | 0.27x |
| `structural/modifier-family-to-type-window` | 58.533 us | 256.282 us | 273.382 us | 0.23x |
| `lokad/langserv/url-download` | 11.166 us | 51.189 us | 51.887 us | 0.22x |
| `lokad/folding/region-marker` | 5.651 us | 35.616 us | 63.500 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.196 us | 223.317 us | 240.055 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.965 us | 132.856 us | 148.194 us | 0.02x |
| `balancing/begin-end-blocks` | 48.065 us | 4,517.771 us | 4,533.129 us | 0.01x |
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
| `lokad/docs/inline-doc-prefix` | 2.026 us | 1.453 us | 2.232 us | 1.39x |
| `lokad/imports/module-imports` | 6.103 us | 4.428 us | 29.772 us | 1.38x |
| `lokad/langserv/url-dashboard` | 8.745 us | 6.355 us | 6.822 us | 1.38x |
| `structural/ordered-keyword-window` | 11.733 us | 8.657 us | 22.895 us | 1.36x |
| `structural/keyword-family-to-capitalized-identifier` | 10.189 us | 7.781 us | 24.058 us | 1.31x |
| `fallback/lookahead` | 5.473 us | 4.238 us | 18.197 us | 1.29x |
| `structural/ordered-modifier-pair` | 10.942 us | 8.651 us | 23.261 us | 1.26x |
| `lokad/lexer/operator-run` | 1.172 us | 0.960 us | 1.405 us | 1.22x |
| `lokad/lexer/identifier` | 9.082 us | 7.992 us | 11.145 us | 1.14x |
| `lokad/style/range-ref` | 0.820 us | 0.732 us | 0.872 us | 1.12x |
| `lokad/langserv/identifier-validator` | 4.812 us | 4.364 us | 5.878 us | 1.10x |
| `lokad/lexer/doc-line` | 1.147 us | 1.063 us | 1.873 us | 1.08x |
| `lokad/lexer/string` | 32.507 us | 30.488 us | 33.115 us | 1.07x |
| `structural/method-family-call` | 5.447 us | 5.137 us | 19.288 us | 1.06x |
| `structural/modifier-family-to-type-window` | 58.904 us | 55.932 us | 71.740 us | 1.05x |
| `lokad/langserv/color-short-hex` | 0.516 us | 0.490 us | 0.677 us | 1.05x |
| `lokad/style/cell-ref` | 0.860 us | 0.830 us | 1.047 us | 1.04x |
| `structural/method-family-generic-call` | 4.427 us | 4.608 us | 18.613 us | 0.96x |
| `lokad/style/hex-color` | 0.217 us | 0.231 us | 0.313 us | 0.94x |
| `lokad/langserv/url-download` | 11.189 us | 12.211 us | 12.684 us | 0.92x |
| `lokad/langserv/helper-identifier` | 2.476 us | 2.706 us | 3.580 us | 0.91x |
| `lokad/lexer/number` | 3.208 us | 3.891 us | 4.938 us | 0.82x |
| `literal-family/type-token-family` | 9.274 us | 11.481 us | 26.069 us | 0.81x |
| `structural/ordered-identifier-window` | 8.387 us | 10.635 us | 25.447 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 0.649 us | 0.849 us | 1.111 us | 0.76x |
| `literal/identifier-token-casei` | 3.946 us | 5.513 us | 20.495 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.310 us | 3.527 us | 17.970 us | 0.65x |
| `structural/ordered-keyword-pair` | 2.583 us | 4.166 us | 18.581 us | 0.62x |
| `literal-family/method-token-family` | 2.630 us | 5.106 us | 19.255 us | 0.52x |
| `literal/identifier-token` | 2.253 us | 4.718 us | 18.319 us | 0.48x |
| `literal/call-token` | 1.859 us | 3.988 us | 17.817 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.981 us | 10.828 us | 25.969 us | 0.28x |
| `lokad/folding/region-marker` | 5.721 us | 35.426 us | 63.658 us | 0.16x |
| `balancing/begin-end-blocks` | 48.410 us | 471.111 us | 503.208 us | 0.10x |
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
