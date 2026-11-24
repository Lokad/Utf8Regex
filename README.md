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
| `literal/sherlock-zh` | 22.437 us | 18.928 us | 2,182.440 us | 1.19x |
| `literal/sherlock-ru` | 69.095 us | 100.094 us | 5,331.240 us | 0.69x |
| `literal-alternate/sherlock-zh` | 43.332 us | 68.372 us | 2,184.928 us | 0.63x |
| `aws-keys/quick` | 5,361.035 us | 9,930.325 us | 43,909.000 us | 0.54x |
| `ruff-noqa/tweaked` | 4,077.779 us | 7,612.700 us | 41,924.600 us | 0.54x |
| `literal/sherlock-casei-ru` | 154.907 us | 365.377 us | 5,727.238 us | 0.42x |
| `literal/sherlock-casei-en` | 79.798 us | 199.752 us | 564.221 us | 0.40x |
| `bounded-repeat/letters-en` | 1,100.375 us | 2,804.619 us | 3,001.136 us | 0.39x |
| `literal/sherlock-en` | 31.548 us | 87.186 us | 473.393 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,986.921 us | 5,866.292 us | 6,834.208 us | 0.34x |
| `dictionary/single` | 8,142.190 us | 26,179.467 us | 26,183.900 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,449.081 us | 4,769.094 us | 10,088.450 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 162.163 us | 595.765 us | 1,025.273 us | 0.27x |
| `bounded-repeat/capitals` | 11,881.629 us | 45,690.800 us | 51,934.400 us | 0.26x |
| `literal-alternate/sherlock-en` | 349.594 us | 1,537.816 us | 2,109.055 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 127.552 us | 657.995 us | 1,078.026 us | 0.19x |
| `aws-keys/full` | 8,478.733 us | 86,043.900 us | 118,041.000 us | 0.10x |
| `bounded-repeat/context` | 8,899.022 us | 148,266.600 us | 152,215.400 us | 0.06x |
| `ruff-noqa/real` | 3,507.986 us | 69,709.350 us | 86,266.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 886.912 us | 23,486.325 us | 28,920.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 102.053 us | 11,141.357 us | 11,954.257 us | 0.0092x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.117 us | 0.109 us | 0.138 us | 1.08x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.080 us | 0.076 us | 0.090 us | 1.05x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.082 us | 0.083 us | 0.098 us | 0.99x |
| `common/ip-match` | `IsMatch` | 0.167 us | 0.172 us | 0.191 us | 0.97x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.078 us | 0.084 us | 0.100 us | 0.93x |
| `industry/mariomka-email-count` | `Count` | 440.066 us | 504.478 us | 9,071.400 us | 0.87x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.091 us | 0.112 us | 0.147 us | 0.81x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.135 us | 0.169 us | 0.188 us | 0.80x |
| `common/uri-match` | `IsMatch` | 0.164 us | 0.232 us | 0.269 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.138 us | 0.200 us | 0.221 us | 0.69x |
| `industry/mariomka-uri-count` | `Count` | 1,297.954 us | 1,892.478 us | 10,672.467 us | 0.69x |
| `common/email-match` | `IsMatch` | 0.156 us | 0.269 us | 0.297 us | 0.58x |
| `common/ip-miss` | `IsMatch` | 0.396 us | 0.712 us | 0.731 us | 0.56x |
| `common/match-word` | `Match` | 0.066 us | 0.128 us | 1.163 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 656.001 us | 1,369.990 us | 8,624.520 us | 0.48x |
| `industry/mariomka-ip-count` | `Count` | 3,319.739 us | 8,508.622 us | 17,051.967 us | 0.39x |
| `common/date-miss` | `IsMatch` | 0.217 us | 0.558 us | 0.594 us | 0.39x |
| `common/matches-set` | `Count` | 62.861 us | 193.268 us | 196.477 us | 0.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 79.200 us | 262.808 us | 505.535 us | 0.30x |
| `common/matches-word` | `Count` | 1.237 us | 4.226 us | 5.941 us | 0.29x |
| `common/replace-words` | `Replace` | 21.512 us | 78.992 us | 80.005 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.488 us | 982.226 us | 1,285.729 us | 0.17x |
| `common/split-words` | `Split` | 13.159 us | 80.446 us | 82.455 us | 0.16x |
| `common/matches-boundary` | `Count` | 27.293 us | 168.626 us | 168.553 us | 0.16x |
| `common/matches-words` | `Count` | 9.206 us | 70.748 us | 71.570 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,057.171 us | 9,882.013 us | 17,134.500 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,913.415 us | 19,746.800 us | 20,346.050 us | 0.10x |
| `common/backtracking` | `IsMatch` | 0.066 us | 0.839 us | 0.876 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 876.424 us | 11,861.429 us | 12,345.067 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.619 us | 0.650 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,463.736 us | 32,107.133 us | 39,816.200 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.348 us | 0.373 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,000.909 us | 22,538.675 us | 30,095.500 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 291.897 us | 8,618.989 us | 9,147.600 us | 0.03x |
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
| `aws-keys/quick` | 5,398.924 us | 3,753.729 us | 35,919.900 us | 1.44x |
| `literal-alternate/sherlock-zh` | 43.450 us | 36.112 us | 2,139.300 us | 1.20x |
| `literal/sherlock-casei-en` | 79.898 us | 78.902 us | 456.350 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,990.413 us | 2,069.400 us | 2,981.560 us | 0.96x |
| `literal-alternate/sherlock-en` | 348.853 us | 374.204 us | 773.775 us | 0.93x |
| `literal/sherlock-zh` | 22.436 us | 24.163 us | 2,189.600 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 161.769 us | 176.389 us | 549.036 us | 0.92x |
| `ruff-noqa/tweaked` | 4,125.442 us | 4,602.276 us | 36,311.200 us | 0.90x |
| `bounded-repeat/capitals` | 11,915.414 us | 13,528.850 us | 20,554.350 us | 0.88x |
| `literal/sherlock-ru` | 69.025 us | 89.847 us | 5,298.600 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 101.765 us | 146.626 us | 573.388 us | 0.69x |
| `literal/sherlock-casei-ru` | 154.977 us | 225.629 us | 5,524.573 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 128.170 us | 203.311 us | 576.344 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,437.637 us | 2,348.281 us | 7,485.125 us | 0.61x |
| `bounded-repeat/letters-en` | 1,098.528 us | 1,828.195 us | 1,964.289 us | 0.60x |
| `aws-keys/full` | 8,470.833 us | 18,488.580 us | 50,980.933 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 890.359 us | 1,971.113 us | 7,175.250 us | 0.45x |
| `literal/sherlock-en` | 31.483 us | 76.202 us | 426.678 us | 0.41x |
| `dictionary/single` | 8,140.720 us | 45,447.300 us | 45,531.150 us | 0.18x |
| `ruff-noqa/real` | 3,489.455 us | 20,879.950 us | 47,523.567 us | 0.17x |
| `bounded-repeat/context` | 8,973.322 us | 68,720.350 us | 73,161.200 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.166 us | 0.049 us | 0.069 us | 3.41x |
| `common/date-match` | `IsMatch` | 0.116 us | 0.043 us | 0.072 us | 2.71x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.086 us | 0.032 us | 0.049 us | 2.69x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.079 us | 0.032 us | 0.051 us | 2.45x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.134 us | 0.057 us | 0.078 us | 2.36x |
| `common/ip-miss` | `IsMatch` | 0.395 us | 0.180 us | 0.202 us | 2.20x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.078 us | 0.036 us | 0.051 us | 2.15x |
| `common/uri-miss` | `IsMatch` | 0.137 us | 0.066 us | 0.089 us | 2.06x |
| `common/email-match` | `IsMatch` | 0.154 us | 0.080 us | 0.108 us | 1.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.090 us | 0.049 us | 0.086 us | 1.84x |
| `common/uri-match` | `IsMatch` | 0.163 us | 0.089 us | 0.121 us | 1.83x |
| `common/backtracking` | `IsMatch` | 0.065 us | 0.044 us | 0.074 us | 1.49x |
| `common/match-word` | `Match` | 0.066 us | 0.049 us | 0.941 us | 1.36x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 79.184 us | 59.845 us | 256.388 us | 1.32x |
| `common/replace-words` | `Replace` | 21.390 us | 17.301 us | 18.684 us | 1.24x |
| `industry/mariomka-uri-count` | `Count` | 1,288.750 us | 1,138.291 us | 8,614.800 us | 1.13x |
| `common/date-miss` | `IsMatch` | 0.219 us | 0.197 us | 0.235 us | 1.11x |
| `industry/mariomka-ip-count` | `Count` | 3,322.091 us | 3,500.436 us | 10,982.033 us | 0.95x |
| `industry/mariomka-email-count` | `Count` | 440.634 us | 474.609 us | 7,787.300 us | 0.93x |
| `industry/leipzig-river-window-count` | `Count` | 1,457.617 us | 1,605.692 us | 8,237.650 us | 0.91x |
| `common/matches-words` | `Count` | 9.193 us | 10.916 us | 14.014 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 998.160 us | 1,213.886 us | 7,296.480 us | 0.82x |
| `common/split-words` | `Split` | 13.241 us | 21.267 us | 23.064 us | 0.62x |
| `common/matches-set` | `Count` | 63.098 us | 105.707 us | 107.700 us | 0.60x |
| `common/matches-word` | `Count` | 1.239 us | 2.321 us | 3.481 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 655.107 us | 1,335.681 us | 7,299.613 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,061.576 us | 3,094.480 us | 8,427.600 us | 0.34x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.107 us | 0.129 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.897 us | 603.979 us | 897.988 us | 0.28x |
| `common/matches-boundary` | `Count` | 27.908 us | 104.447 us | 105.955 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,910.100 us | 8,851.167 us | 9,689.650 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 875.809 us | 4,789.056 us | 5,204.293 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.089 us | 0.117 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 293.759 us | 3,980.389 us | 4,365.289 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.749 us | 6.368 us | 31.163 us | 1.22x |
| `fallback/lookahead` | 6.737 us | 6.116 us | 19.517 us | 1.10x |
| `structural/method-family-generic-call` | 6.507 us | 6.631 us | 20.450 us | 0.98x |
| `structural/method-family-call` | 7.902 us | 8.104 us | 21.327 us | 0.98x |
| `lokad/langserv/identifier-validator` | 6.669 us | 7.508 us | 9.015 us | 0.89x |
| `lokad/lexer/identifier` | 11.938 us | 13.628 us | 16.852 us | 0.88x |
| `lokad/lexer/doc-line` | 1.671 us | 1.925 us | 2.696 us | 0.87x |
| `lokad/langserv/color-short-hex` | 0.713 us | 0.823 us | 1.013 us | 0.87x |
| `structural/ordered-keyword-window` | 14.505 us | 17.699 us | 30.890 us | 0.82x |
| `lokad/style/hex-color` | 0.427 us | 0.522 us | 0.592 us | 0.82x |
| `lokad/docs/inline-doc-prefix` | 2.608 us | 3.371 us | 4.214 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 1.372 us | 1.797 us | 2.056 us | 0.76x |
| `lokad/lexer/operator-run` | 1.707 us | 2.325 us | 2.803 us | 0.73x |
| `structural/keyword-to-generic-type` | 2.974 us | 4.080 us | 17.912 us | 0.73x |
| `structural/ordered-keyword-pair` | 3.162 us | 5.294 us | 18.703 us | 0.60x |
| `lokad/lexer/number` | 6.313 us | 10.722 us | 11.756 us | 0.59x |
| `structural/ordered-modifier-pair` | 10.252 us | 17.823 us | 31.090 us | 0.58x |
| `lokad/langserv/helper-identifier` | 2.562 us | 4.699 us | 5.662 us | 0.55x |
| `literal/identifier-token` | 2.340 us | 4.366 us | 17.367 us | 0.54x |
| `lokad/style/cell-ref` | 0.873 us | 1.917 us | 2.126 us | 0.46x |
| `lokad/style/range-ref` | 0.824 us | 1.884 us | 2.016 us | 0.44x |
| `lokad/langserv/url-dashboard` | 10.013 us | 23.138 us | 23.593 us | 0.43x |
| `literal/call-token` | 1.856 us | 4.301 us | 17.349 us | 0.43x |
| `literal-family/type-token-family` | 9.284 us | 24.339 us | 38.132 us | 0.38x |
| `lokad/lexer/string` | 26.899 us | 85.845 us | 89.515 us | 0.31x |
| `structural/ordered-identifier-window` | 8.710 us | 28.979 us | 43.018 us | 0.30x |
| `structural/modifier-family-to-type-window` | 75.859 us | 255.549 us | 269.728 us | 0.30x |
| `literal-family/method-token-family` | 2.685 us | 9.209 us | 21.959 us | 0.29x |
| `literal/identifier-token-casei` | 3.521 us | 15.386 us | 28.798 us | 0.23x |
| `lokad/langserv/url-download` | 11.415 us | 51.848 us | 51.635 us | 0.22x |
| `lokad/folding/region-marker` | 5.308 us | 35.626 us | 61.654 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 15.990 us | 219.673 us | 233.494 us | 0.07x |
| `structural/modifier-family-to-type-pair` | 2.875 us | 131.334 us | 145.143 us | 0.02x |
| `balancing/begin-end-blocks` | 47.039 us | 4,175.578 us | 4,153.479 us | 0.01x |
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
| `structural/keyword-family-to-capitalized-identifier` | 16.124 us | 7.673 us | 21.333 us | 2.10x |
| `lokad/style/hex-color` | 0.423 us | 0.228 us | 0.309 us | 1.85x |
| `lokad/lexer/operator-run` | 1.710 us | 0.949 us | 1.420 us | 1.80x |
| `lokad/docs/inline-doc-prefix` | 2.587 us | 1.458 us | 2.195 us | 1.77x |
| `lokad/imports/module-imports` | 7.780 us | 4.422 us | 28.412 us | 1.76x |
| `lokad/langserv/identifier-validator` | 7.249 us | 4.274 us | 5.773 us | 1.70x |
| `structural/ordered-keyword-window` | 14.546 us | 8.643 us | 21.539 us | 1.68x |
| `lokad/lexer/number` | 6.346 us | 3.813 us | 4.929 us | 1.66x |
| `lokad/langserv/color-rgb-hex` | 1.363 us | 0.846 us | 1.096 us | 1.61x |
| `fallback/lookahead` | 6.788 us | 4.232 us | 17.296 us | 1.60x |
| `lokad/langserv/url-dashboard` | 9.979 us | 6.292 us | 6.792 us | 1.59x |
| `lokad/lexer/doc-line` | 1.663 us | 1.058 us | 1.862 us | 1.57x |
| `structural/method-family-call` | 7.871 us | 5.090 us | 17.986 us | 1.55x |
| `lokad/lexer/identifier` | 12.034 us | 7.955 us | 10.762 us | 1.51x |
| `lokad/langserv/color-short-hex` | 0.717 us | 0.487 us | 0.682 us | 1.47x |
| `structural/method-family-generic-call` | 6.487 us | 4.619 us | 17.394 us | 1.40x |
| `structural/modifier-family-to-type-window` | 76.002 us | 55.933 us | 69.658 us | 1.36x |
| `structural/ordered-modifier-pair` | 10.280 us | 8.704 us | 21.822 us | 1.18x |
| `lokad/style/range-ref` | 0.833 us | 0.728 us | 0.865 us | 1.14x |
| `lokad/style/cell-ref` | 0.872 us | 0.832 us | 1.050 us | 1.05x |
| `lokad/langserv/helper-identifier` | 2.560 us | 2.706 us | 3.619 us | 0.95x |
| `lokad/langserv/url-download` | 11.536 us | 12.427 us | 12.644 us | 0.93x |
| `lokad/lexer/string` | 26.950 us | 30.676 us | 33.693 us | 0.88x |
| `structural/keyword-to-generic-type` | 2.968 us | 3.521 us | 16.787 us | 0.84x |
| `structural/ordered-identifier-window` | 8.673 us | 10.645 us | 24.439 us | 0.81x |
| `literal-family/type-token-family` | 9.303 us | 11.482 us | 24.592 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.155 us | 4.091 us | 16.750 us | 0.77x |
| `literal/identifier-token-casei` | 3.515 us | 5.943 us | 19.738 us | 0.59x |
| `literal-family/method-token-family` | 2.675 us | 5.273 us | 17.768 us | 0.51x |
| `literal/identifier-token` | 2.342 us | 4.703 us | 17.014 us | 0.50x |
| `literal/call-token` | 1.852 us | 3.928 us | 16.477 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.882 us | 10.595 us | 23.609 us | 0.27x |
| `lokad/folding/region-marker` | 5.347 us | 36.244 us | 62.070 us | 0.15x |
| `balancing/begin-end-blocks` | 47.592 us | 467.800 us | 496.060 us | 0.10x |
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
