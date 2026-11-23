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
| `literal/sherlock-zh` | 22.535 us | 19.012 us | 2,222.326 us | 1.19x |
| `literal/sherlock-ru` | 68.255 us | 98.542 us | 5,296.286 us | 0.69x |
| `literal-alternate/sherlock-zh` | 43.908 us | 70.051 us | 2,217.523 us | 0.63x |
| `aws-keys/quick` | 5,404.336 us | 9,696.578 us | 46,556.250 us | 0.56x |
| `ruff-noqa/tweaked` | 3,972.753 us | 7,502.564 us | 41,450.550 us | 0.53x |
| `literal/sherlock-casei-en` | 86.831 us | 189.560 us | 610.613 us | 0.46x |
| `literal/sherlock-casei-ru` | 158.355 us | 366.927 us | 5,616.908 us | 0.43x |
| `bounded-repeat/letters-en` | 1,109.831 us | 2,870.737 us | 3,020.856 us | 0.39x |
| `literal/sherlock-en` | 31.924 us | 88.189 us | 469.893 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,992.397 us | 5,877.046 us | 6,747.667 us | 0.34x |
| `dictionary/single` | 8,216.890 us | 26,282.633 us | 26,331.800 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,442.691 us | 4,787.647 us | 10,200.850 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 157.038 us | 590.055 us | 1,014.846 us | 0.27x |
| `bounded-repeat/capitals` | 11,607.557 us | 46,479.950 us | 49,539.700 us | 0.25x |
| `literal-alternate/sherlock-en` | 353.380 us | 1,546.259 us | 2,048.408 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 128.592 us | 664.292 us | 1,094.980 us | 0.19x |
| `aws-keys/full` | 8,342.833 us | 87,087.500 us | 117,383.900 us | 0.10x |
| `bounded-repeat/context` | 8,737.256 us | 149,315.300 us | 153,901.600 us | 0.06x |
| `ruff-noqa/real` | 3,478.173 us | 69,764.700 us | 90,287.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 847.490 us | 23,229.825 us | 28,422.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 108.774 us | 10,984.729 us | 11,851.257 us | 0.0099x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.116 us | 0.110 us | 0.138 us | 1.06x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.082 us | 0.078 us | 0.091 us | 1.05x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.087 us | 0.082 us | 0.099 us | 1.05x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.081 us | 0.086 us | 0.102 us | 0.95x |
| `common/uri-miss` | `IsMatch` | 0.173 us | 0.187 us | 0.208 us | 0.93x |
| `common/ip-match` | `IsMatch` | 0.161 us | 0.181 us | 0.200 us | 0.89x |
| `industry/mariomka-email-count` | `Count` | 432.933 us | 486.973 us | 8,939.525 us | 0.89x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.091 us | 0.111 us | 0.145 us | 0.82x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.138 us | 0.170 us | 0.186 us | 0.81x |
| `common/uri-match` | `IsMatch` | 0.175 us | 0.237 us | 0.274 us | 0.74x |
| `industry/mariomka-uri-count` | `Count` | 1,226.327 us | 1,897.056 us | 10,409.275 us | 0.65x |
| `common/email-match` | `IsMatch` | 0.157 us | 0.278 us | 0.306 us | 0.57x |
| `common/ip-miss` | `IsMatch` | 0.386 us | 0.714 us | 0.731 us | 0.54x |
| `industry/leipzig-twain-count` | `Count` | 632.843 us | 1,334.242 us | 8,565.217 us | 0.47x |
| `common/match-word` | `Match` | 0.056 us | 0.129 us | 1.162 us | 0.44x |
| `common/date-miss` | `IsMatch` | 0.237 us | 0.560 us | 0.596 us | 0.42x |
| `industry/mariomka-ip-count` | `Count` | 2,878.252 us | 8,373.556 us | 16,883.667 us | 0.34x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 87.302 us | 269.362 us | 510.262 us | 0.32x |
| `common/matches-set` | `Count` | 61.348 us | 195.293 us | 196.737 us | 0.31x |
| `common/matches-word` | `Count` | 1.231 us | 4.242 us | 5.939 us | 0.29x |
| `common/replace-words` | `Replace` | 21.961 us | 78.107 us | 79.468 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.167 us | 974.228 us | 1,263.292 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.253 us | 163.336 us | 165.141 us | 0.17x |
| `common/split-words` | `Split` | 13.237 us | 81.213 us | 83.076 us | 0.16x |
| `common/matches-words` | `Count` | 9.215 us | 70.885 us | 72.560 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,062.066 us | 9,782.600 us | 17,233.067 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,857.695 us | 19,654.075 us | 20,287.875 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 888.571 us | 11,756.643 us | 12,356.417 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.044 us | 0.856 us | 0.896 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.615 us | 0.647 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,467.671 us | 32,786.033 us | 40,144.450 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,028.930 us | 23,000.800 us | 30,185.050 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.348 us | 0.373 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 281.945 us | 8,332.630 us | 8,904.689 us | 0.03x |
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
| `aws-keys/quick` | 5,407.914 us | 3,616.114 us | 38,247.150 us | 1.50x |
| `literal-alternate/sherlock-zh` | 43.967 us | 36.367 us | 2,163.124 us | 1.21x |
| `literal/sherlock-casei-en` | 86.657 us | 79.241 us | 452.016 us | 1.09x |
| `literal-alternate/sherlock-en` | 353.844 us | 344.501 us | 788.554 us | 1.03x |
| `bounded-repeat/letters-ru` | 1,992.874 us | 2,064.830 us | 2,933.488 us | 0.97x |
| `literal/sherlock-zh` | 22.462 us | 24.167 us | 2,216.476 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 157.128 us | 174.572 us | 568.928 us | 0.90x |
| `ruff-noqa/tweaked` | 3,941.458 us | 4,542.118 us | 36,178.800 us | 0.87x |
| `bounded-repeat/capitals` | 11,627.129 us | 13,599.967 us | 16,418.850 us | 0.85x |
| `literal/sherlock-ru` | 68.250 us | 89.783 us | 5,201.927 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 110.219 us | 145.335 us | 615.111 us | 0.76x |
| `literal/sherlock-casei-ru` | 154.017 us | 223.901 us | 5,522.947 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 128.762 us | 201.530 us | 591.349 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,444.796 us | 2,355.503 us | 7,566.525 us | 0.61x |
| `bounded-repeat/letters-en` | 1,109.909 us | 1,863.988 us | 1,990.053 us | 0.60x |
| `aws-keys/full` | 8,318.730 us | 18,325.800 us | 50,481.200 us | 0.45x |
| `literal-alternate/sherlock-casei-ru` | 848.351 us | 1,966.666 us | 7,189.300 us | 0.43x |
| `literal/sherlock-en` | 32.057 us | 77.042 us | 428.462 us | 0.42x |
| `dictionary/single` | 8,197.120 us | 45,764.950 us | 45,908.700 us | 0.18x |
| `ruff-noqa/real` | 3,487.182 us | 20,811.200 us | 49,685.000 us | 0.17x |
| `bounded-repeat/context` | 8,688.311 us | 69,592.150 us | 73,270.550 us | 0.12x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.161 us | 0.049 us | 0.068 us | 3.31x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.085 us | 0.031 us | 0.048 us | 2.74x |
| `common/uri-miss` | `IsMatch` | 0.173 us | 0.066 us | 0.086 us | 2.65x |
| `common/date-match` | `IsMatch` | 0.113 us | 0.044 us | 0.073 us | 2.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.081 us | 0.031 us | 0.051 us | 2.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.137 us | 0.055 us | 0.076 us | 2.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.080 us | 0.036 us | 0.054 us | 2.21x |
| `common/ip-miss` | `IsMatch` | 0.387 us | 0.180 us | 0.203 us | 2.15x |
| `common/uri-match` | `IsMatch` | 0.175 us | 0.091 us | 0.123 us | 1.93x |
| `common/email-match` | `IsMatch` | 0.156 us | 0.082 us | 0.110 us | 1.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.090 us | 0.049 us | 0.085 us | 1.84x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 87.056 us | 58.507 us | 262.775 us | 1.49x |
| `common/replace-words` | `Replace` | 22.039 us | 17.452 us | 19.059 us | 1.26x |
| `common/date-miss` | `IsMatch` | 0.235 us | 0.202 us | 0.239 us | 1.16x |
| `common/match-word` | `Match` | 0.056 us | 0.049 us | 0.950 us | 1.15x |
| `industry/mariomka-uri-count` | `Count` | 1,229.094 us | 1,142.169 us | 8,534.000 us | 1.08x |
| `industry/mariomka-email-count` | `Count` | 447.356 us | 456.279 us | 7,809.575 us | 0.98x |
| `common/backtracking` | `IsMatch` | 0.043 us | 0.044 us | 0.075 us | 0.98x |
| `industry/leipzig-river-window-count` | `Count` | 1,473.665 us | 1,527.439 us | 8,074.100 us | 0.96x |
| `industry/mariomka-ip-count` | `Count` | 2,874.089 us | 3,491.927 us | 10,856.833 us | 0.82x |
| `common/matches-words` | `Count` | 9.210 us | 11.296 us | 14.234 us | 0.82x |
| `industry/leipzig-symbol-count` | `Count` | 1,029.911 us | 1,263.756 us | 7,603.650 us | 0.81x |
| `common/split-words` | `Split` | 13.257 us | 21.207 us | 23.119 us | 0.63x |
| `common/matches-set` | `Count` | 61.288 us | 105.279 us | 106.624 us | 0.58x |
| `common/matches-word` | `Count` | 1.241 us | 2.354 us | 3.539 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 634.139 us | 1,305.717 us | 7,635.820 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,061.764 us | 3,078.100 us | 8,303.525 us | 0.34x |
| `common/one-node-backtracking` | `IsMatch` | 0.027 us | 0.090 us | 0.128 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.432 us | 591.820 us | 857.322 us | 0.28x |
| `common/matches-boundary` | `Count` | 27.048 us | 105.416 us | 106.667 us | 0.26x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,851.239 us | 8,762.856 us | 9,383.625 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 884.044 us | 4,784.469 us | 5,226.800 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.089 us | 0.116 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 281.035 us | 3,982.616 us | 4,360.778 us | 0.07x |
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
| `fallback/lookahead` | 10.851 us | 6.044 us | 18.744 us | 1.80x |
| `structural/method-family-generic-call` | 10.366 us | 6.775 us | 20.915 us | 1.53x |
| `structural/method-family-call` | 10.086 us | 8.359 us | 21.581 us | 1.21x |
| `lokad/imports/module-imports` | 7.462 us | 6.444 us | 31.747 us | 1.16x |
| `structural/ordered-keyword-window` | 20.017 us | 17.901 us | 31.061 us | 1.12x |
| `lokad/style/range-ref` | 1.780 us | 1.827 us | 1.990 us | 0.97x |
| `lokad/style/cell-ref` | 1.809 us | 1.920 us | 2.140 us | 0.94x |
| `lokad/lexer/doc-line` | 1.683 us | 1.881 us | 2.634 us | 0.89x |
| `lokad/langserv/identifier-validator` | 6.572 us | 7.435 us | 8.986 us | 0.88x |
| `lokad/lexer/identifier` | 11.915 us | 13.597 us | 16.853 us | 0.88x |
| `lokad/langserv/color-short-hex` | 0.709 us | 0.835 us | 1.016 us | 0.85x |
| `lokad/style/hex-color` | 0.426 us | 0.516 us | 0.599 us | 0.83x |
| `structural/keyword-to-generic-type` | 3.133 us | 4.173 us | 18.687 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 1.338 us | 1.804 us | 2.044 us | 0.74x |
| `lokad/lexer/operator-run` | 1.710 us | 2.378 us | 2.838 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 2.479 us | 3.504 us | 4.328 us | 0.71x |
| `structural/ordered-keyword-pair` | 3.637 us | 5.519 us | 19.219 us | 0.66x |
| `structural/ordered-modifier-pair` | 10.538 us | 17.314 us | 30.474 us | 0.61x |
| `lokad/lexer/number` | 6.307 us | 10.951 us | 12.185 us | 0.58x |
| `lokad/langserv/helper-identifier` | 2.602 us | 4.821 us | 5.805 us | 0.54x |
| `literal/identifier-token` | 2.310 us | 4.369 us | 17.453 us | 0.53x |
| `structural/ordered-identifier-window` | 14.448 us | 28.706 us | 42.046 us | 0.50x |
| `literal/call-token` | 1.864 us | 4.309 us | 17.237 us | 0.43x |
| `lokad/langserv/url-dashboard` | 9.642 us | 23.460 us | 23.993 us | 0.41x |
| `literal-family/type-token-family` | 9.346 us | 23.771 us | 37.800 us | 0.39x |
| `structural/modifier-family-to-type-window` | 92.871 us | 252.818 us | 267.920 us | 0.37x |
| `lokad/lexer/string` | 26.321 us | 82.536 us | 86.042 us | 0.32x |
| `literal-family/method-token-family` | 2.702 us | 9.300 us | 22.622 us | 0.29x |
| `literal/identifier-token-casei` | 3.925 us | 15.136 us | 29.148 us | 0.26x |
| `lokad/langserv/url-download` | 11.487 us | 51.654 us | 51.897 us | 0.22x |
| `structural/modifier-family-to-type-pair` | 23.746 us | 135.909 us | 149.876 us | 0.17x |
| `lokad/folding/region-marker` | 5.330 us | 35.825 us | 62.513 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 14.836 us | 226.148 us | 238.395 us | 0.07x |
| `balancing/begin-end-blocks` | 46.534 us | 4,283.861 us | 4,321.000 us | 0.01x |
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
| `fallback/lookahead` | 10.835 us | 4.212 us | 16.518 us | 2.57x |
| `lokad/style/range-ref` | 1.762 us | 0.741 us | 0.885 us | 2.38x |
| `structural/ordered-keyword-window` | 20.088 us | 8.598 us | 21.720 us | 2.34x |
| `structural/method-family-generic-call` | 10.409 us | 4.651 us | 17.770 us | 2.24x |
| `structural/modifier-family-to-type-pair` | 23.838 us | 10.780 us | 23.762 us | 2.21x |
| `lokad/style/cell-ref` | 1.805 us | 0.833 us | 1.042 us | 2.17x |
| `structural/method-family-call` | 10.113 us | 5.089 us | 18.061 us | 1.99x |
| `structural/keyword-family-to-capitalized-identifier` | 14.800 us | 7.728 us | 21.093 us | 1.92x |
| `lokad/style/hex-color` | 0.431 us | 0.230 us | 0.313 us | 1.87x |
| `lokad/lexer/operator-run` | 1.734 us | 0.928 us | 1.390 us | 1.87x |
| `lokad/docs/inline-doc-prefix` | 2.479 us | 1.433 us | 2.210 us | 1.73x |
| `structural/modifier-family-to-type-window` | 93.366 us | 55.022 us | 70.097 us | 1.70x |
| `lokad/imports/module-imports` | 7.465 us | 4.493 us | 28.998 us | 1.66x |
| `lokad/lexer/number` | 6.273 us | 3.839 us | 4.943 us | 1.63x |
| `lokad/lexer/doc-line` | 1.684 us | 1.057 us | 1.871 us | 1.59x |
| `lokad/langserv/color-rgb-hex` | 1.334 us | 0.847 us | 1.104 us | 1.58x |
| `lokad/langserv/identifier-validator` | 6.599 us | 4.292 us | 5.826 us | 1.54x |
| `lokad/langserv/url-dashboard` | 9.640 us | 6.329 us | 6.854 us | 1.52x |
| `lokad/lexer/identifier` | 11.859 us | 7.978 us | 10.804 us | 1.49x |
| `lokad/langserv/color-short-hex` | 0.708 us | 0.495 us | 0.686 us | 1.43x |
| `structural/ordered-identifier-window` | 14.465 us | 10.763 us | 24.019 us | 1.34x |
| `structural/ordered-modifier-pair` | 10.519 us | 8.692 us | 21.786 us | 1.21x |
| `lokad/langserv/helper-identifier` | 2.619 us | 2.745 us | 3.671 us | 0.95x |
| `lokad/langserv/url-download` | 11.526 us | 12.226 us | 12.672 us | 0.94x |
| `structural/keyword-to-generic-type` | 3.131 us | 3.539 us | 17.369 us | 0.88x |
| `lokad/lexer/string` | 26.537 us | 30.354 us | 33.847 us | 0.87x |
| `structural/ordered-keyword-pair` | 3.633 us | 4.173 us | 17.174 us | 0.87x |
| `literal-family/type-token-family` | 9.309 us | 11.548 us | 24.752 us | 0.81x |
| `literal/identifier-token-casei` | 3.911 us | 5.925 us | 19.183 us | 0.66x |
| `literal/identifier-token` | 2.304 us | 4.292 us | 17.179 us | 0.54x |
| `literal-family/method-token-family` | 2.707 us | 5.320 us | 18.519 us | 0.51x |
| `literal/call-token` | 1.857 us | 3.954 us | 16.401 us | 0.47x |
| `lokad/folding/region-marker` | 5.280 us | 35.712 us | 62.360 us | 0.15x |
| `balancing/begin-end-blocks` | 46.833 us | 464.973 us | 499.252 us | 0.10x |
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
