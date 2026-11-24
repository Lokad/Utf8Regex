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
| `literal/sherlock-zh` | 22.084 us | 18.819 us | 2,151.778 us | 1.17x |
| `literal/sherlock-ru` | 68.817 us | 95.067 us | 5,302.086 us | 0.72x |
| `literal-alternate/sherlock-zh` | 43.648 us | 70.487 us | 2,182.029 us | 0.62x |
| `ruff-noqa/tweaked` | 3,989.526 us | 7,531.500 us | 41,349.650 us | 0.53x |
| `literal/sherlock-casei-en` | 79.654 us | 189.273 us | 605.638 us | 0.42x |
| `literal/sherlock-casei-ru` | 148.560 us | 365.629 us | 5,582.314 us | 0.41x |
| `bounded-repeat/letters-en` | 1,094.036 us | 2,805.785 us | 2,990.200 us | 0.39x |
| `aws-keys/quick` | 3,732.450 us | 9,693.700 us | 43,665.900 us | 0.39x |
| `literal/sherlock-en` | 30.823 us | 85.710 us | 462.670 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,986.631 us | 5,824.531 us | 6,727.542 us | 0.34x |
| `literal-alternate/sherlock-ru` | 1,424.128 us | 4,820.844 us | 9,985.525 us | 0.30x |
| `dictionary/single` | 7,516.580 us | 25,544.000 us | 25,480.975 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 169.305 us | 589.704 us | 1,015.406 us | 0.29x |
| `bounded-repeat/capitals` | 11,410.900 us | 46,015.850 us | 49,836.600 us | 0.25x |
| `literal-alternate/sherlock-en` | 335.682 us | 1,450.452 us | 1,989.920 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 126.362 us | 657.332 us | 1,090.560 us | 0.19x |
| `aws-keys/full` | 8,387.811 us | 86,733.600 us | 116,788.000 us | 0.10x |
| `bounded-repeat/context` | 8,640.378 us | 147,835.500 us | 153,752.800 us | 0.06x |
| `ruff-noqa/real` | 3,444.209 us | 71,776.150 us | 91,377.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 869.299 us | 22,968.975 us | 28,304.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 110.050 us | 10,966.086 us | 12,000.343 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.120 us | 0.106 us | 0.131 us | 1.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.079 us | 0.074 us | 0.089 us | 1.06x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.081 us | 0.082 us | 0.097 us | 0.99x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.078 us | 0.084 us | 0.100 us | 0.93x |
| `common/ip-match` | `IsMatch` | 0.164 us | 0.178 us | 0.200 us | 0.92x |
| `industry/mariomka-email-count` | `Count` | 426.379 us | 491.990 us | 8,951.000 us | 0.87x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.089 us | 0.110 us | 0.144 us | 0.82x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.131 us | 0.161 us | 0.177 us | 0.81x |
| `common/uri-miss` | `IsMatch` | 0.150 us | 0.186 us | 0.204 us | 0.81x |
| `common/uri-match` | `IsMatch` | 0.158 us | 0.230 us | 0.267 us | 0.69x |
| `industry/mariomka-uri-count` | `Count` | 1,250.292 us | 1,951.572 us | 10,453.200 us | 0.64x |
| `common/match-word` | `Match` | 0.084 us | 0.131 us | 1.154 us | 0.64x |
| `common/email-match` | `IsMatch` | 0.154 us | 0.265 us | 0.291 us | 0.58x |
| `common/ip-miss` | `IsMatch` | 0.404 us | 0.706 us | 0.727 us | 0.57x |
| `industry/leipzig-twain-count` | `Count` | 649.239 us | 1,358.253 us | 8,494.140 us | 0.48x |
| `industry/mariomka-ip-count` | `Count` | 3,273.896 us | 8,184.650 us | 16,697.167 us | 0.40x |
| `common/date-miss` | `IsMatch` | 0.219 us | 0.551 us | 0.576 us | 0.40x |
| `common/matches-set` | `Count` | 61.893 us | 192.405 us | 193.451 us | 0.32x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 82.453 us | 257.803 us | 496.691 us | 0.32x |
| `common/matches-word` | `Count` | 1.236 us | 4.213 us | 5.903 us | 0.29x |
| `common/replace-words` | `Replace` | 21.165 us | 76.318 us | 78.164 us | 0.28x |
| `common/matches-boundary` | `Count` | 30.128 us | 164.581 us | 166.615 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.771 us | 953.332 us | 1,250.675 us | 0.17x |
| `common/split-words` | `Split` | 12.993 us | 79.782 us | 80.950 us | 0.16x |
| `common/matches-words` | `Count` | 9.119 us | 68.189 us | 69.838 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,032.173 us | 9,395.578 us | 16,502.600 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,845.671 us | 19,453.325 us | 20,103.775 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 888.495 us | 11,809.486 us | 12,257.471 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,489.200 us | 31,420.967 us | 39,208.300 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.027 us | 0.613 us | 0.643 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.342 us | 0.371 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,007.018 us | 22,687.400 us | 30,048.350 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.834 us | 0.868 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 272.754 us | 8,482.978 us | 8,842.211 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.651 us | 36.075 us | 2,169.292 us | 1.21x |
| `aws-keys/quick` | 3,793.985 us | 3,685.159 us | 35,108.150 us | 1.03x |
| `literal-alternate/sherlock-en` | 340.047 us | 336.692 us | 749.159 us | 1.01x |
| `literal/sherlock-casei-en` | 79.348 us | 78.881 us | 448.644 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,998.063 us | 2,038.792 us | 2,924.700 us | 0.98x |
| `literal-alternate/sherlock-en-mixed` | 170.223 us | 174.137 us | 551.871 us | 0.98x |
| `literal/sherlock-zh` | 22.133 us | 23.813 us | 2,165.546 us | 0.93x |
| `ruff-noqa/tweaked` | 4,052.232 us | 4,540.488 us | 36,480.150 us | 0.89x |
| `bounded-repeat/capitals` | 11,528.214 us | 13,547.567 us | 16,332.275 us | 0.85x |
| `literal/sherlock-ru` | 69.481 us | 87.318 us | 5,271.073 us | 0.80x |
| `literal-alternate/sherlock-casei-en` | 108.834 us | 142.574 us | 586.468 us | 0.76x |
| `literal/sherlock-casei-ru` | 149.069 us | 224.555 us | 5,383.573 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 125.839 us | 199.053 us | 581.246 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,432.634 us | 2,348.844 us | 7,394.225 us | 0.61x |
| `bounded-repeat/letters-en` | 1,085.913 us | 1,835.428 us | 1,966.745 us | 0.59x |
| `aws-keys/full` | 8,415.967 us | 18,456.780 us | 50,460.900 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 868.892 us | 1,953.564 us | 7,081.875 us | 0.44x |
| `literal/sherlock-en` | 30.997 us | 75.004 us | 419.200 us | 0.41x |
| `ruff-noqa/real` | 3,446.717 us | 20,821.375 us | 47,728.500 us | 0.17x |
| `dictionary/single` | 7,549.010 us | 45,672.400 us | 45,546.150 us | 0.17x |
| `bounded-repeat/context` | 8,697.011 us | 68,768.500 us | 72,827.700 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.161 us | 0.050 us | 0.071 us | 3.20x |
| `common/date-match` | `IsMatch` | 0.118 us | 0.042 us | 0.072 us | 2.79x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.083 us | 0.031 us | 0.047 us | 2.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.077 us | 0.032 us | 0.051 us | 2.41x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.130 us | 0.055 us | 0.076 us | 2.35x |
| `common/uri-miss` | `IsMatch` | 0.151 us | 0.065 us | 0.084 us | 2.31x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.078 us | 0.035 us | 0.052 us | 2.24x |
| `common/ip-miss` | `IsMatch` | 0.396 us | 0.178 us | 0.202 us | 2.23x |
| `common/email-match` | `IsMatch` | 0.151 us | 0.079 us | 0.107 us | 1.90x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.087 us | 0.048 us | 0.084 us | 1.82x |
| `common/uri-match` | `IsMatch` | 0.158 us | 0.089 us | 0.121 us | 1.78x |
| `common/match-word` | `Match` | 0.082 us | 0.047 us | 0.926 us | 1.73x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 82.101 us | 62.374 us | 267.505 us | 1.32x |
| `common/replace-words` | `Replace` | 21.185 us | 17.211 us | 18.755 us | 1.23x |
| `industry/mariomka-uri-count` | `Count` | 1,241.352 us | 1,119.541 us | 8,442.767 us | 1.11x |
| `common/date-miss` | `IsMatch` | 0.215 us | 0.199 us | 0.234 us | 1.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,503.124 us | 1,497.804 us | 8,129.000 us | 1.00x |
| `industry/mariomka-ip-count` | `Count` | 3,268.704 us | 3,453.309 us | 10,816.767 us | 0.95x |
| `industry/mariomka-email-count` | `Count` | 426.257 us | 459.020 us | 7,638.450 us | 0.93x |
| `common/matches-words` | `Count` | 9.121 us | 10.677 us | 13.822 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 1,011.820 us | 1,211.590 us | 6,889.775 us | 0.84x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.043 us | 0.073 us | 0.78x |
| `common/split-words` | `Split` | 13.077 us | 20.511 us | 22.101 us | 0.64x |
| `common/matches-set` | `Count` | 61.914 us | 103.858 us | 105.912 us | 0.60x |
| `common/matches-word` | `Count` | 1.228 us | 2.323 us | 3.474 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 652.494 us | 1,321.242 us | 7,470.300 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,044.210 us | 3,012.692 us | 8,131.425 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.027 us | 0.088 us | 0.128 us | 0.31x |
| `common/matches-boundary` | `Count` | 30.244 us | 104.016 us | 105.376 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.302 us | 592.258 us | 859.667 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,849.933 us | 8,779.311 us | 9,440.200 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 893.224 us | 4,733.981 us | 5,143.627 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.088 us | 0.115 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 271.290 us | 3,883.790 us | 4,239.094 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.426 us | 6.409 us | 31.061 us | 1.16x |
| `fallback/lookahead` | 6.578 us | 5.967 us | 19.406 us | 1.10x |
| `structural/ordered-keyword-window` | 18.580 us | 17.739 us | 30.731 us | 1.05x |
| `structural/method-family-call` | 7.993 us | 7.972 us | 21.245 us | 1.00x |
| `structural/method-family-generic-call` | 6.542 us | 6.577 us | 20.345 us | 0.99x |
| `lokad/lexer/doc-line` | 1.646 us | 1.859 us | 2.536 us | 0.89x |
| `lokad/langserv/identifier-validator` | 6.453 us | 7.452 us | 9.038 us | 0.87x |
| `lokad/lexer/identifier` | 11.786 us | 13.641 us | 16.499 us | 0.86x |
| `lokad/langserv/color-short-hex` | 0.703 us | 0.819 us | 1.009 us | 0.86x |
| `lokad/style/hex-color` | 0.422 us | 0.527 us | 0.623 us | 0.80x |
| `lokad/langserv/color-rgb-hex` | 1.363 us | 1.792 us | 2.000 us | 0.76x |
| `lokad/docs/inline-doc-prefix` | 2.481 us | 3.444 us | 4.248 us | 0.72x |
| `lokad/lexer/operator-run` | 1.648 us | 2.320 us | 2.739 us | 0.71x |
| `structural/keyword-to-generic-type` | 2.867 us | 4.065 us | 17.860 us | 0.71x |
| `lokad/lexer/number` | 6.323 us | 10.749 us | 11.872 us | 0.59x |
| `structural/ordered-modifier-pair` | 10.183 us | 17.483 us | 30.592 us | 0.58x |
| `structural/ordered-keyword-pair` | 3.156 us | 5.440 us | 18.911 us | 0.58x |
| `lokad/langserv/helper-identifier` | 2.568 us | 4.722 us | 5.615 us | 0.54x |
| `literal/identifier-token` | 2.263 us | 4.283 us | 17.222 us | 0.53x |
| `lokad/style/cell-ref` | 0.856 us | 1.891 us | 2.102 us | 0.45x |
| `lokad/style/range-ref` | 0.804 us | 1.832 us | 2.023 us | 0.44x |
| `structural/ordered-identifier-window` | 12.472 us | 28.566 us | 42.581 us | 0.44x |
| `lokad/langserv/url-dashboard` | 9.981 us | 22.918 us | 23.340 us | 0.44x |
| `literal/call-token` | 1.815 us | 4.258 us | 17.186 us | 0.43x |
| `literal-family/type-token-family` | 9.136 us | 23.653 us | 36.733 us | 0.39x |
| `structural/modifier-family-to-type-window` | 96.130 us | 253.081 us | 267.392 us | 0.38x |
| `lokad/lexer/string` | 26.987 us | 81.974 us | 85.944 us | 0.33x |
| `literal-family/method-token-family` | 2.651 us | 9.267 us | 21.885 us | 0.29x |
| `literal/identifier-token-casei` | 3.934 us | 15.330 us | 28.404 us | 0.26x |
| `lokad/langserv/url-download` | 11.477 us | 50.580 us | 51.290 us | 0.23x |
| `lokad/folding/region-marker` | 5.171 us | 35.046 us | 61.674 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 16.141 us | 213.544 us | 229.787 us | 0.08x |
| `structural/modifier-family-to-type-pair` | 2.949 us | 131.492 us | 145.747 us | 0.02x |
| `balancing/begin-end-blocks` | 46.085 us | 4,197.779 us | 4,181.679 us | 0.01x |
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
| `structural/ordered-keyword-window` | 18.670 us | 8.543 us | 21.550 us | 2.19x |
| `structural/keyword-family-to-capitalized-identifier` | 16.050 us | 7.587 us | 21.534 us | 2.12x |
| `lokad/style/hex-color` | 0.424 us | 0.229 us | 0.317 us | 1.85x |
| `lokad/lexer/operator-run` | 1.668 us | 0.914 us | 1.389 us | 1.83x |
| `lokad/docs/inline-doc-prefix` | 2.489 us | 1.438 us | 2.195 us | 1.73x |
| `structural/modifier-family-to-type-window` | 95.102 us | 55.011 us | 69.446 us | 1.73x |
| `lokad/imports/module-imports` | 7.372 us | 4.416 us | 28.265 us | 1.67x |
| `lokad/lexer/number` | 6.270 us | 3.759 us | 4.915 us | 1.67x |
| `lokad/langserv/color-rgb-hex` | 1.366 us | 0.840 us | 1.086 us | 1.63x |
| `lokad/langserv/url-dashboard` | 9.969 us | 6.205 us | 6.653 us | 1.61x |
| `fallback/lookahead` | 6.658 us | 4.177 us | 17.108 us | 1.59x |
| `lokad/lexer/doc-line` | 1.637 us | 1.041 us | 1.827 us | 1.57x |
| `structural/method-family-call` | 7.959 us | 5.070 us | 17.817 us | 1.57x |
| `lokad/langserv/identifier-validator` | 6.527 us | 4.197 us | 5.693 us | 1.56x |
| `lokad/langserv/color-short-hex` | 0.706 us | 0.473 us | 0.674 us | 1.49x |
| `lokad/lexer/identifier` | 11.810 us | 7.920 us | 10.615 us | 1.49x |
| `structural/method-family-generic-call` | 6.561 us | 4.605 us | 17.451 us | 1.42x |
| `structural/ordered-modifier-pair` | 10.023 us | 8.521 us | 21.372 us | 1.18x |
| `structural/ordered-identifier-window` | 12.474 us | 10.681 us | 24.631 us | 1.17x |
| `lokad/style/range-ref` | 0.814 us | 0.722 us | 0.860 us | 1.13x |
| `lokad/style/cell-ref` | 0.864 us | 0.820 us | 1.034 us | 1.05x |
| `lokad/langserv/helper-identifier` | 2.567 us | 2.684 us | 3.616 us | 0.96x |
| `lokad/langserv/url-download` | 11.525 us | 12.205 us | 12.620 us | 0.94x |
| `lokad/lexer/string` | 26.912 us | 30.175 us | 33.088 us | 0.89x |
| `structural/keyword-to-generic-type` | 2.885 us | 3.480 us | 16.766 us | 0.83x |
| `literal-family/type-token-family` | 9.167 us | 11.283 us | 24.017 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.130 us | 4.077 us | 17.027 us | 0.77x |
| `literal/identifier-token-casei` | 3.910 us | 5.923 us | 19.214 us | 0.66x |
| `literal-family/method-token-family` | 2.640 us | 5.085 us | 17.709 us | 0.52x |
| `literal/identifier-token` | 2.266 us | 4.629 us | 16.650 us | 0.49x |
| `literal/call-token` | 1.829 us | 3.908 us | 16.440 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.934 us | 10.612 us | 23.619 us | 0.28x |
| `lokad/folding/region-marker` | 5.154 us | 35.482 us | 61.025 us | 0.15x |
| `balancing/begin-end-blocks` | 46.812 us | 463.100 us | 490.621 us | 0.10x |
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
