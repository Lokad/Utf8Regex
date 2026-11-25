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
| `literal/sherlock-zh` | 22.212 us | 18.694 us | 2,153.109 us | 1.19x |
| `literal/sherlock-ru` | 66.871 us | 98.620 us | 5,306.010 us | 0.68x |
| `literal-alternate/sherlock-zh` | 43.133 us | 69.750 us | 2,194.159 us | 0.62x |
| `ruff-noqa/tweaked` | 4,085.379 us | 7,907.230 us | 41,931.050 us | 0.52x |
| `bounded-repeat/letters-en` | 1,174.255 us | 2,792.093 us | 2,954.231 us | 0.42x |
| `literal/sherlock-casei-ru` | 146.853 us | 382.500 us | 5,391.179 us | 0.38x |
| `literal/sherlock-en` | 31.925 us | 84.974 us | 466.372 us | 0.38x |
| `literal/sherlock-casei-en` | 68.942 us | 188.724 us | 583.674 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,122.814 us | 5,838.346 us | 6,666.792 us | 0.36x |
| `dictionary/single` | 8,080.700 us | 25,668.367 us | 25,659.567 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,434.830 us | 4,729.175 us | 9,856.750 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 170.959 us | 589.809 us | 1,029.784 us | 0.29x |
| `bounded-repeat/capitals` | 11,487.671 us | 45,950.000 us | 49,764.400 us | 0.25x |
| `literal-alternate/sherlock-en` | 342.624 us | 1,504.748 us | 2,012.065 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 126.797 us | 642.199 us | 1,069.026 us | 0.20x |
| `aws-keys/quick` | 1,840.902 us | 9,534.250 us | 40,116.800 us | 0.19x |
| `aws-keys/full` | 8,548.367 us | 84,703.800 us | 114,761.000 us | 0.10x |
| `bounded-repeat/context` | 8,905.556 us | 147,394.500 us | 151,368.300 us | 0.06x |
| `ruff-noqa/real` | 3,456.045 us | 69,986.300 us | 100,067.700 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 865.426 us | 22,970.525 us | 28,261.050 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 110.717 us | 11,162.129 us | 11,930.600 us | 0.0099x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 383.287 us | 487.435 us | 8,931.075 us | 0.79x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.075 us | 0.110 us | 0.146 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.070 us | 0.110 us | 0.138 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,184.553 us | 1,913.405 us | 10,321.250 us | 0.62x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.076 us | 0.089 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.046 us | 0.085 us | 0.100 us | 0.55x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.082 us | 0.097 us | 0.49x |
| `common/email-match` | `IsMatch` | 0.126 us | 0.264 us | 0.288 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 626.546 us | 1,313.054 us | 8,385.440 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.073 us | 0.168 us | 0.183 us | 0.43x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.171 us | 0.191 us | 0.43x |
| `common/match-word` | `Match` | 0.049 us | 0.124 us | 1.119 us | 0.40x |
| `common/uri-miss` | `IsMatch` | 0.078 us | 0.200 us | 0.220 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,160.237 us | 8,212.110 us | 16,558.367 us | 0.38x |
| `common/matches-set` | `Count` | 67.164 us | 194.210 us | 196.326 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.078 us | 0.231 us | 0.268 us | 0.34x |
| `common/ip-miss` | `IsMatch` | 0.224 us | 0.730 us | 0.753 us | 0.31x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.038 us | 260.636 us | 513.108 us | 0.30x |
| `common/matches-word` | `Count` | 1.233 us | 4.217 us | 5.954 us | 0.29x |
| `common/replace-words` | `Replace` | 21.097 us | 76.077 us | 77.815 us | 0.28x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.558 us | 0.587 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.225 us | 978.630 us | 1,284.738 us | 0.17x |
| `common/split-words` | `Split` | 12.978 us | 76.403 us | 77.941 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.660 us | 163.119 us | 164.816 us | 0.15x |
| `common/matches-words` | `Count` | 9.086 us | 69.561 us | 71.141 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,062.101 us | 9,769.625 us | 16,881.967 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,921.362 us | 19,392.250 us | 19,980.175 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 867.433 us | 11,774.157 us | 12,309.371 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.608 us | 0.640 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.341 us | 0.367 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.820 us | 0.866 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 971.079 us | 23,734.200 us | 31,434.950 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,255.133 us | 31,673.467 us | 38,580.600 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 285.904 us | 8,327.100 us | 8,863.278 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.192 us | 35.781 us | 2,126.953 us | 1.21x |
| `literal-alternate/sherlock-en` | 345.727 us | 341.177 us | 748.627 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,118.189 us | 2,120.483 us | 2,980.312 us | 1.00x |
| `literal-alternate/sherlock-en-mixed` | 167.537 us | 173.684 us | 529.487 us | 0.96x |
| `literal/sherlock-zh` | 22.173 us | 23.837 us | 2,162.511 us | 0.93x |
| `ruff-noqa/tweaked` | 4,107.179 us | 4,578.265 us | 36,417.800 us | 0.90x |
| `literal/sherlock-casei-en` | 69.081 us | 77.447 us | 442.229 us | 0.89x |
| `bounded-repeat/capitals` | 11,616.671 us | 13,422.600 us | 16,382.075 us | 0.87x |
| `literal-alternate/sherlock-casei-en` | 112.336 us | 144.763 us | 549.190 us | 0.78x |
| `literal/sherlock-ru` | 67.112 us | 89.420 us | 5,147.286 us | 0.75x |
| `literal/sherlock-casei-ru` | 145.363 us | 216.745 us | 5,250.773 us | 0.67x |
| `bounded-repeat/letters-en` | 1,169.942 us | 1,806.705 us | 1,963.756 us | 0.65x |
| `literal-alternate/sherlock-en-nomatch` | 127.056 us | 199.934 us | 571.582 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,429.557 us | 2,303.788 us | 7,451.200 us | 0.62x |
| `aws-keys/quick` | 1,844.445 us | 3,631.038 us | 35,187.800 us | 0.51x |
| `aws-keys/full` | 8,539.856 us | 18,492.840 us | 50,182.367 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 868.983 us | 1,949.600 us | 7,175.575 us | 0.45x |
| `literal/sherlock-en` | 31.768 us | 74.303 us | 438.548 us | 0.43x |
| `dictionary/single` | 8,047.150 us | 45,023.500 us | 45,042.650 us | 0.18x |
| `ruff-noqa/real` | 3,482.168 us | 20,784.750 us | 52,085.233 us | 0.17x |
| `bounded-repeat/context` | 8,917.500 us | 68,499.350 us | 71,938.050 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.071 us | 0.043 us | 0.072 us | 1.64x |
| `common/email-match` | `IsMatch` | 0.125 us | 0.079 us | 0.108 us | 1.58x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.074 us | 0.048 us | 0.086 us | 1.55x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.048 us | 0.067 us | 1.47x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.043 us | 0.031 us | 0.049 us | 1.36x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.025 us | 57.163 us | 263.908 us | 1.35x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.071 us | 0.056 us | 0.076 us | 1.27x |
| `common/replace-words` | `Replace` | 20.964 us | 16.867 us | 18.355 us | 1.24x |
| `common/uri-miss` | `IsMatch` | 0.078 us | 0.064 us | 0.086 us | 1.22x |
| `common/ip-miss` | `IsMatch` | 0.215 us | 0.179 us | 0.201 us | 1.20x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.040 us | 0.035 us | 0.053 us | 1.14x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.035 us | 0.032 us | 0.048 us | 1.09x |
| `common/match-word` | `Match` | 0.049 us | 0.046 us | 0.901 us | 1.07x |
| `industry/mariomka-uri-count` | `Count` | 1,174.820 us | 1,104.309 us | 8,332.100 us | 1.06x |
| `industry/mariomka-ip-count` | `Count` | 3,166.146 us | 3,451.786 us | 10,736.900 us | 0.92x |
| `common/uri-match` | `IsMatch` | 0.078 us | 0.088 us | 0.121 us | 0.90x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.043 us | 0.072 us | 0.86x |
| `common/matches-words` | `Count` | 9.092 us | 10.793 us | 13.829 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 380.577 us | 454.565 us | 7,596.625 us | 0.84x |
| `industry/leipzig-river-window-count` | `Count` | 1,251.595 us | 1,519.522 us | 7,807.880 us | 0.82x |
| `industry/leipzig-symbol-count` | `Count` | 984.115 us | 1,208.598 us | 6,854.000 us | 0.81x |
| `common/split-words` | `Split` | 13.063 us | 20.197 us | 22.115 us | 0.65x |
| `common/matches-set` | `Count` | 67.056 us | 104.596 us | 106.380 us | 0.64x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.199 us | 0.237 us | 0.55x |
| `common/matches-word` | `Count` | 1.230 us | 2.331 us | 3.451 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 630.618 us | 1,295.214 us | 7,182.178 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,073.330 us | 2,988.384 us | 8,087.450 us | 0.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.036 us | 595.862 us | 859.202 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.106 us | 0.127 us | 0.27x |
| `common/matches-boundary` | `Count` | 23.795 us | 104.613 us | 105.992 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,930.737 us | 8,648.467 us | 9,268.650 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 868.929 us | 4,723.712 us | 5,180.453 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.089 us | 0.116 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 284.611 us | 3,943.095 us | 4,308.339 us | 0.07x |
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
| `lokad/imports/module-imports` | 6.804 us | 6.364 us | 31.067 us | 1.07x |
| `fallback/lookahead` | 5.562 us | 5.991 us | 19.207 us | 0.93x |
| `lokad/lexer/doc-line` | 1.512 us | 1.841 us | 2.553 us | 0.82x |
| `structural/method-family-generic-call` | 5.462 us | 6.679 us | 20.589 us | 0.82x |
| `lokad/langserv/identifier-validator` | 6.073 us | 7.451 us | 8.884 us | 0.82x |
| `lokad/lexer/identifier` | 11.058 us | 13.652 us | 16.629 us | 0.81x |
| `lokad/langserv/helper-identifier` | 3.655 us | 4.648 us | 5.570 us | 0.79x |
| `structural/method-family-call` | 6.532 us | 8.314 us | 21.543 us | 0.79x |
| `structural/ordered-keyword-window` | 13.497 us | 17.785 us | 31.184 us | 0.76x |
| `structural/keyword-to-generic-type` | 2.994 us | 4.017 us | 17.511 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 2.279 us | 3.477 us | 4.277 us | 0.66x |
| `lokad/langserv/color-short-hex` | 0.508 us | 0.796 us | 0.996 us | 0.64x |
| `structural/ordered-modifier-pair` | 10.378 us | 17.012 us | 30.410 us | 0.61x |
| `structural/ordered-keyword-pair` | 3.195 us | 5.244 us | 18.556 us | 0.61x |
| `literal/identifier-token` | 2.283 us | 4.322 us | 17.194 us | 0.53x |
| `lokad/lexer/operator-run` | 1.025 us | 2.317 us | 2.781 us | 0.44x |
| `literal/call-token` | 1.832 us | 4.248 us | 16.939 us | 0.43x |
| `lokad/langserv/url-dashboard` | 9.555 us | 22.928 us | 23.399 us | 0.42x |
| `lokad/style/hex-color` | 0.213 us | 0.512 us | 0.604 us | 0.42x |
| `lokad/style/cell-ref` | 0.779 us | 1.880 us | 2.084 us | 0.41x |
| `lokad/style/range-ref` | 0.738 us | 1.808 us | 1.955 us | 0.41x |
| `literal-family/type-token-family` | 9.102 us | 22.999 us | 36.482 us | 0.40x |
| `lokad/lexer/string` | 32.184 us | 84.610 us | 88.721 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.625 us | 1.824 us | 2.049 us | 0.34x |
| `structural/ordered-identifier-window` | 8.504 us | 28.100 us | 41.885 us | 0.30x |
| `literal-family/method-token-family` | 2.588 us | 9.242 us | 21.789 us | 0.28x |
| `structural/modifier-family-to-type-window` | 66.547 us | 253.783 us | 268.128 us | 0.26x |
| `lokad/lexer/number` | 2.795 us | 10.809 us | 11.892 us | 0.26x |
| `literal/identifier-token-casei` | 3.901 us | 15.299 us | 28.839 us | 0.25x |
| `lokad/langserv/url-download` | 11.492 us | 51.235 us | 51.605 us | 0.22x |
| `lokad/folding/region-marker` | 5.233 us | 34.990 us | 61.909 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 10.915 us | 220.906 us | 233.362 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.865 us | 132.275 us | 146.905 us | 0.02x |
| `balancing/begin-end-blocks` | 46.528 us | 4,163.033 us | 4,203.722 us | 0.01x |
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
| `lokad/docs/inline-doc-prefix` | 2.300 us | 1.424 us | 2.166 us | 1.61x |
| `lokad/imports/module-imports` | 6.786 us | 4.337 us | 28.216 us | 1.56x |
| `structural/ordered-keyword-window` | 13.397 us | 8.568 us | 21.837 us | 1.56x |
| `lokad/langserv/url-dashboard` | 9.557 us | 6.212 us | 6.720 us | 1.54x |
| `lokad/lexer/doc-line` | 1.510 us | 1.047 us | 1.787 us | 1.44x |
| `lokad/langserv/identifier-validator` | 6.078 us | 4.220 us | 5.730 us | 1.44x |
| `lokad/lexer/identifier` | 11.057 us | 7.924 us | 10.732 us | 1.40x |
| `structural/keyword-family-to-capitalized-identifier` | 10.889 us | 7.870 us | 20.978 us | 1.38x |
| `fallback/lookahead` | 5.485 us | 4.195 us | 16.985 us | 1.31x |
| `lokad/langserv/helper-identifier` | 3.659 us | 2.814 us | 3.620 us | 1.30x |
| `structural/method-family-call` | 6.533 us | 5.038 us | 17.759 us | 1.30x |
| `structural/ordered-modifier-pair` | 10.314 us | 8.548 us | 21.637 us | 1.21x |
| `structural/modifier-family-to-type-window` | 66.393 us | 55.981 us | 70.669 us | 1.19x |
| `structural/method-family-generic-call` | 5.412 us | 4.612 us | 17.570 us | 1.17x |
| `lokad/lexer/operator-run` | 1.022 us | 0.951 us | 1.407 us | 1.07x |
| `lokad/lexer/string` | 32.339 us | 30.502 us | 32.782 us | 1.06x |
| `lokad/style/range-ref` | 0.738 us | 0.735 us | 0.850 us | 1.00x |
| `lokad/langserv/url-download` | 11.523 us | 11.951 us | 12.456 us | 0.96x |
| `lokad/style/cell-ref` | 0.780 us | 0.826 us | 1.051 us | 0.95x |
| `lokad/langserv/color-short-hex` | 0.448 us | 0.477 us | 0.672 us | 0.94x |
| `structural/keyword-to-generic-type` | 3.010 us | 3.452 us | 16.438 us | 0.87x |
| `lokad/style/hex-color` | 0.194 us | 0.230 us | 0.312 us | 0.84x |
| `structural/ordered-identifier-window` | 8.522 us | 10.287 us | 23.799 us | 0.83x |
| `literal-family/type-token-family` | 9.101 us | 11.291 us | 24.276 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.203 us | 4.077 us | 16.736 us | 0.79x |
| `lokad/lexer/number` | 2.779 us | 3.774 us | 4.842 us | 0.74x |
| `lokad/langserv/color-rgb-hex` | 0.564 us | 0.839 us | 1.104 us | 0.67x |
| `literal/identifier-token-casei` | 3.854 us | 5.867 us | 19.041 us | 0.66x |
| `literal/identifier-token` | 2.269 us | 4.274 us | 16.790 us | 0.53x |
| `literal-family/method-token-family` | 2.608 us | 5.067 us | 17.545 us | 0.51x |
| `literal/call-token` | 1.805 us | 3.860 us | 16.150 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.869 us | 10.502 us | 23.218 us | 0.27x |
| `lokad/folding/region-marker` | 5.232 us | 35.336 us | 60.982 us | 0.15x |
| `balancing/begin-end-blocks` | 46.464 us | 462.900 us | 493.512 us | 0.10x |
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
