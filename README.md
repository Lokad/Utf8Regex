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
| `literal/sherlock-zh` | 22.346 us | 18.942 us | 2,185.160 us | 1.18x |
| `literal/sherlock-ru` | 67.495 us | 97.825 us | 5,069.033 us | 0.69x |
| `literal-alternate/sherlock-zh` | 43.305 us | 67.862 us | 2,166.789 us | 0.64x |
| `ruff-noqa/tweaked` | 3,992.658 us | 7,802.318 us | 44,183.950 us | 0.51x |
| `literal/sherlock-casei-ru` | 155.450 us | 365.439 us | 5,605.485 us | 0.43x |
| `literal/sherlock-casei-en` | 80.129 us | 198.513 us | 570.923 us | 0.40x |
| `bounded-repeat/letters-en` | 1,111.176 us | 2,809.407 us | 2,965.985 us | 0.40x |
| `aws-keys/quick` | 3,746.945 us | 9,851.100 us | 43,524.350 us | 0.38x |
| `literal/sherlock-en` | 31.559 us | 86.750 us | 481.096 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,980.733 us | 5,865.592 us | 6,792.033 us | 0.34x |
| `dictionary/single` | 8,203.370 us | 25,764.033 us | 25,853.767 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,434.917 us | 4,783.950 us | 10,172.150 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 167.562 us | 614.212 us | 1,060.861 us | 0.27x |
| `bounded-repeat/capitals` | 11,957.114 us | 46,924.100 us | 54,650.500 us | 0.25x |
| `literal-alternate/sherlock-en` | 338.945 us | 1,486.359 us | 2,028.786 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 127.766 us | 644.073 us | 1,084.848 us | 0.20x |
| `aws-keys/full` | 8,492.789 us | 86,748.100 us | 122,707.300 us | 0.10x |
| `bounded-repeat/context` | 8,514.389 us | 147,882.600 us | 154,971.600 us | 0.06x |
| `ruff-noqa/real` | 3,444.174 us | 74,448.500 us | 90,215.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 871.405 us | 22,931.625 us | 28,141.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 109.758 us | 11,173.857 us | 11,994.871 us | 0.0098x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.124 us | 0.107 us | 0.137 us | 1.15x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.079 us | 0.075 us | 0.089 us | 1.06x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.082 us | 0.083 us | 0.098 us | 0.99x |
| `common/ip-match` | `IsMatch` | 0.160 us | 0.173 us | 0.192 us | 0.92x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.077 us | 0.084 us | 0.101 us | 0.91x |
| `industry/mariomka-email-count` | `Count` | 437.120 us | 504.258 us | 9,011.425 us | 0.87x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.133 us | 0.160 us | 0.179 us | 0.83x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.088 us | 0.110 us | 0.144 us | 0.80x |
| `common/uri-miss` | `IsMatch` | 0.152 us | 0.203 us | 0.223 us | 0.75x |
| `common/uri-match` | `IsMatch` | 0.158 us | 0.232 us | 0.268 us | 0.68x |
| `common/match-word` | `Match` | 0.085 us | 0.128 us | 1.195 us | 0.67x |
| `industry/mariomka-uri-count` | `Count` | 1,250.607 us | 1,909.580 us | 10,509.433 us | 0.65x |
| `common/email-match` | `IsMatch` | 0.155 us | 0.266 us | 0.294 us | 0.58x |
| `common/ip-miss` | `IsMatch` | 0.397 us | 0.715 us | 0.734 us | 0.55x |
| `industry/leipzig-twain-count` | `Count` | 655.152 us | 1,373.560 us | 8,757.400 us | 0.48x |
| `industry/mariomka-ip-count` | `Count` | 3,283.061 us | 8,150.690 us | 16,764.033 us | 0.40x |
| `common/date-miss` | `IsMatch` | 0.216 us | 0.560 us | 0.593 us | 0.39x |
| `common/matches-set` | `Count` | 63.051 us | 192.282 us | 196.080 us | 0.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 85.292 us | 263.203 us | 492.198 us | 0.32x |
| `common/matches-word` | `Count` | 1.236 us | 4.253 us | 6.012 us | 0.29x |
| `common/replace-words` | `Replace` | 21.519 us | 78.436 us | 79.798 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.257 us | 974.539 us | 1,265.548 us | 0.17x |
| `common/matches-boundary` | `Count` | 26.573 us | 163.793 us | 165.461 us | 0.16x |
| `common/split-words` | `Split` | 13.101 us | 81.098 us | 82.528 us | 0.16x |
| `common/matches-words` | `Count` | 9.168 us | 70.101 us | 71.609 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,041.175 us | 9,953.450 us | 17,293.833 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,859.859 us | 19,365.150 us | 19,886.275 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 887.518 us | 11,757.743 us | 12,447.171 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.608 us | 0.642 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,448.038 us | 32,352.833 us | 39,858.950 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.345 us | 0.366 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,007.125 us | 24,047.025 us | 31,414.000 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.836 us | 0.875 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 281.965 us | 8,217.730 us | 8,734.700 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.463 us | 36.075 us | 2,178.389 us | 1.20x |
| `literal/sherlock-casei-en` | 79.598 us | 77.080 us | 441.672 us | 1.03x |
| `aws-keys/quick` | 3,749.086 us | 3,705.462 us | 36,590.200 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,988.721 us | 2,056.119 us | 2,938.677 us | 0.97x |
| `literal-alternate/sherlock-en-mixed` | 167.155 us | 174.856 us | 547.848 us | 0.96x |
| `literal/sherlock-zh` | 22.467 us | 24.122 us | 2,232.129 us | 0.93x |
| `literal-alternate/sherlock-en` | 338.717 us | 366.660 us | 770.868 us | 0.92x |
| `ruff-noqa/tweaked` | 4,037.942 us | 4,547.906 us | 40,052.450 us | 0.89x |
| `bounded-repeat/capitals` | 11,987.457 us | 13,635.250 us | 21,464.925 us | 0.88x |
| `literal/sherlock-ru` | 67.675 us | 90.067 us | 5,089.147 us | 0.75x |
| `literal-alternate/sherlock-casei-en` | 109.545 us | 146.898 us | 560.250 us | 0.75x |
| `literal/sherlock-casei-ru` | 154.677 us | 226.353 us | 5,453.753 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 127.167 us | 199.805 us | 573.516 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,434.455 us | 2,330.282 us | 7,540.950 us | 0.62x |
| `bounded-repeat/letters-en` | 1,111.801 us | 1,829.962 us | 1,958.636 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 900.388 us | 1,975.092 us | 7,038.200 us | 0.46x |
| `aws-keys/full` | 8,502.711 us | 18,715.640 us | 54,327.200 us | 0.45x |
| `literal/sherlock-en` | 31.439 us | 76.626 us | 444.252 us | 0.41x |
| `dictionary/single` | 8,129.580 us | 45,687.000 us | 45,429.550 us | 0.18x |
| `ruff-noqa/real` | 3,486.882 us | 21,404.675 us | 48,759.600 us | 0.16x |
| `bounded-repeat/context` | 8,607.689 us | 68,525.850 us | 72,620.050 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.159 us | 0.048 us | 0.068 us | 3.31x |
| `common/date-match` | `IsMatch` | 0.123 us | 0.043 us | 0.072 us | 2.87x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.082 us | 0.031 us | 0.047 us | 2.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.076 us | 0.031 us | 0.049 us | 2.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.132 us | 0.054 us | 0.077 us | 2.43x |
| `common/uri-miss` | `IsMatch` | 0.151 us | 0.064 us | 0.085 us | 2.35x |
| `common/ip-miss` | `IsMatch` | 0.393 us | 0.180 us | 0.205 us | 2.19x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.078 us | 0.036 us | 0.051 us | 2.17x |
| `common/email-match` | `IsMatch` | 0.152 us | 0.081 us | 0.107 us | 1.88x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.087 us | 0.049 us | 0.085 us | 1.79x |
| `common/uri-match` | `IsMatch` | 0.157 us | 0.089 us | 0.121 us | 1.77x |
| `common/match-word` | `Match` | 0.084 us | 0.048 us | 0.963 us | 1.75x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 85.407 us | 60.483 us | 258.403 us | 1.41x |
| `common/replace-words` | `Replace` | 21.516 us | 17.584 us | 20.588 us | 1.22x |
| `industry/mariomka-uri-count` | `Count` | 1,255.268 us | 1,101.723 us | 8,486.967 us | 1.14x |
| `common/date-miss` | `IsMatch` | 0.215 us | 0.200 us | 0.235 us | 1.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,458.969 us | 1,518.080 us | 8,218.125 us | 0.96x |
| `industry/mariomka-ip-count` | `Count` | 3,275.513 us | 3,449.759 us | 10,849.433 us | 0.95x |
| `industry/mariomka-email-count` | `Count` | 437.920 us | 470.516 us | 7,774.750 us | 0.93x |
| `common/matches-words` | `Count` | 9.141 us | 10.861 us | 13.960 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,004.389 us | 1,242.582 us | 7,467.525 us | 0.81x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.042 us | 0.074 us | 0.80x |
| `common/split-words` | `Split` | 13.155 us | 20.739 us | 22.534 us | 0.63x |
| `common/matches-set` | `Count` | 63.162 us | 104.869 us | 107.537 us | 0.60x |
| `common/matches-word` | `Count` | 1.236 us | 2.343 us | 3.496 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 656.183 us | 1,336.582 us | 7,877.000 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,049.080 us | 3,060.948 us | 8,660.875 us | 0.34x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.088 us | 0.128 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.112 us | 601.982 us | 865.352 us | 0.28x |
| `common/matches-boundary` | `Count` | 26.550 us | 104.346 us | 106.350 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,864.502 us | 8,667.944 us | 9,171.356 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 890.366 us | 4,742.000 us | 5,221.373 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.089 us | 0.115 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 285.252 us | 3,986.384 us | 4,350.500 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.613 us | 6.338 us | 31.390 us | 1.20x |
| `fallback/lookahead` | 6.684 us | 5.996 us | 19.117 us | 1.11x |
| `structural/method-family-generic-call` | 6.598 us | 6.612 us | 20.397 us | 1.00x |
| `structural/method-family-call` | 8.081 us | 8.118 us | 21.360 us | 1.00x |
| `lokad/langserv/identifier-validator` | 6.552 us | 7.253 us | 8.955 us | 0.90x |
| `lokad/lexer/doc-line` | 1.633 us | 1.857 us | 2.612 us | 0.88x |
| `lokad/langserv/color-short-hex` | 0.725 us | 0.827 us | 0.999 us | 0.88x |
| `lokad/lexer/identifier` | 11.958 us | 14.405 us | 17.593 us | 0.83x |
| `lokad/style/hex-color` | 0.430 us | 0.522 us | 0.595 us | 0.82x |
| `structural/ordered-keyword-window` | 14.276 us | 17.822 us | 31.095 us | 0.80x |
| `lokad/langserv/color-rgb-hex` | 1.378 us | 1.793 us | 2.033 us | 0.77x |
| `lokad/lexer/operator-run` | 1.681 us | 2.334 us | 2.810 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 2.509 us | 3.482 us | 4.277 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.922 us | 4.096 us | 18.258 us | 0.71x |
| `structural/ordered-modifier-pair` | 10.311 us | 17.192 us | 30.279 us | 0.60x |
| `structural/ordered-keyword-pair` | 3.200 us | 5.470 us | 18.845 us | 0.58x |
| `lokad/lexer/number` | 6.262 us | 10.783 us | 11.804 us | 0.58x |
| `lokad/langserv/helper-identifier` | 2.571 us | 4.763 us | 5.672 us | 0.54x |
| `literal/identifier-token` | 2.348 us | 4.368 us | 17.472 us | 0.54x |
| `lokad/style/cell-ref` | 0.873 us | 1.915 us | 2.170 us | 0.46x |
| `lokad/style/range-ref` | 0.823 us | 1.810 us | 1.965 us | 0.45x |
| `lokad/langserv/url-dashboard` | 10.055 us | 23.171 us | 23.624 us | 0.43x |
| `literal/call-token` | 1.858 us | 4.291 us | 17.254 us | 0.43x |
| `literal-family/type-token-family` | 9.307 us | 23.889 us | 37.943 us | 0.39x |
| `lokad/lexer/string` | 27.138 us | 80.899 us | 85.198 us | 0.34x |
| `structural/ordered-identifier-window` | 8.557 us | 28.566 us | 42.056 us | 0.30x |
| `structural/modifier-family-to-type-window` | 76.373 us | 258.151 us | 271.013 us | 0.30x |
| `literal-family/method-token-family` | 2.704 us | 9.406 us | 22.238 us | 0.29x |
| `literal/identifier-token-casei` | 3.935 us | 14.775 us | 28.276 us | 0.27x |
| `lokad/langserv/url-download` | 11.744 us | 51.059 us | 51.666 us | 0.23x |
| `lokad/folding/region-marker` | 5.357 us | 35.103 us | 61.343 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 16.237 us | 217.943 us | 232.138 us | 0.07x |
| `structural/modifier-family-to-type-pair` | 2.901 us | 134.349 us | 147.825 us | 0.02x |
| `balancing/begin-end-blocks` | 47.002 us | 4,386.311 us | 4,391.622 us | 0.01x |
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
| `structural/keyword-family-to-capitalized-identifier` | 16.251 us | 7.665 us | 20.688 us | 2.12x |
| `lokad/style/hex-color` | 0.428 us | 0.233 us | 0.315 us | 1.84x |
| `lokad/lexer/operator-run` | 1.687 us | 0.921 us | 1.411 us | 1.83x |
| `lokad/docs/inline-doc-prefix` | 2.528 us | 1.438 us | 2.197 us | 1.76x |
| `lokad/imports/module-imports` | 7.667 us | 4.422 us | 28.593 us | 1.73x |
| `lokad/lexer/number` | 6.300 us | 3.774 us | 4.881 us | 1.67x |
| `structural/ordered-keyword-window` | 14.301 us | 8.649 us | 21.636 us | 1.65x |
| `lokad/langserv/color-rgb-hex` | 1.378 us | 0.841 us | 1.112 us | 1.64x |
| `fallback/lookahead` | 6.716 us | 4.186 us | 16.932 us | 1.60x |
| `structural/method-family-call` | 8.124 us | 5.069 us | 17.800 us | 1.60x |
| `lokad/langserv/url-dashboard` | 10.064 us | 6.330 us | 6.760 us | 1.59x |
| `lokad/langserv/identifier-validator` | 6.595 us | 4.216 us | 5.768 us | 1.56x |
| `lokad/lexer/doc-line` | 1.621 us | 1.067 us | 1.845 us | 1.52x |
| `lokad/lexer/identifier` | 11.808 us | 7.776 us | 10.860 us | 1.52x |
| `lokad/langserv/color-short-hex` | 0.732 us | 0.483 us | 0.679 us | 1.52x |
| `structural/method-family-generic-call` | 6.590 us | 4.593 us | 17.365 us | 1.43x |
| `structural/modifier-family-to-type-window` | 77.187 us | 61.327 us | 76.434 us | 1.26x |
| `structural/ordered-modifier-pair` | 10.332 us | 8.620 us | 21.657 us | 1.20x |
| `lokad/style/range-ref` | 0.816 us | 0.730 us | 0.861 us | 1.12x |
| `lokad/style/cell-ref` | 0.877 us | 0.835 us | 1.045 us | 1.05x |
| `lokad/langserv/url-download` | 11.800 us | 12.213 us | 12.642 us | 0.97x |
| `lokad/langserv/helper-identifier` | 2.564 us | 2.708 us | 3.624 us | 0.95x |
| `lokad/lexer/string` | 27.134 us | 30.255 us | 33.493 us | 0.90x |
| `structural/keyword-to-generic-type` | 2.900 us | 3.519 us | 16.982 us | 0.82x |
| `literal-family/type-token-family` | 9.339 us | 11.487 us | 24.546 us | 0.81x |
| `structural/ordered-identifier-window` | 8.538 us | 10.674 us | 24.083 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.190 us | 4.124 us | 17.066 us | 0.77x |
| `literal/identifier-token-casei` | 3.949 us | 5.963 us | 19.236 us | 0.66x |
| `literal/identifier-token` | 2.351 us | 4.315 us | 17.289 us | 0.54x |
| `literal-family/method-token-family` | 2.699 us | 5.187 us | 18.218 us | 0.52x |
| `literal/call-token` | 1.853 us | 3.989 us | 16.303 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 2.909 us | 10.763 us | 23.613 us | 0.27x |
| `lokad/folding/region-marker` | 5.349 us | 35.161 us | 60.619 us | 0.15x |
| `balancing/begin-end-blocks` | 47.489 us | 469.906 us | 499.709 us | 0.10x |
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
