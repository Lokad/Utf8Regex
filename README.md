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
| `literal/sherlock-zh` | 22.359 us | 18.889 us | 2,232.118 us | 1.18x |
| `literal/sherlock-ru` | 69.671 us | 97.690 us | 5,345.536 us | 0.71x |
| `literal-alternate/sherlock-zh` | 43.809 us | 68.940 us | 2,225.841 us | 0.64x |
| `ruff-noqa/tweaked` | 4,126.879 us | 7,692.873 us | 41,791.100 us | 0.54x |
| `literal/sherlock-casei-ru` | 149.323 us | 370.223 us | 5,561.754 us | 0.40x |
| `bounded-repeat/letters-en` | 1,108.941 us | 2,817.622 us | 2,997.328 us | 0.39x |
| `literal/sherlock-casei-en` | 69.311 us | 190.016 us | 781.976 us | 0.36x |
| `literal/sherlock-en` | 31.619 us | 86.747 us | 518.171 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,075.722 us | 5,863.854 us | 6,748.125 us | 0.35x |
| `dictionary/single` | 8,180.250 us | 25,836.267 us | 25,900.467 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,438.949 us | 4,848.400 us | 10,081.850 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 168.768 us | 591.948 us | 1,068.949 us | 0.29x |
| `bounded-repeat/capitals` | 12,013.443 us | 45,694.400 us | 49,929.500 us | 0.26x |
| `literal-alternate/sherlock-en` | 345.852 us | 1,543.867 us | 2,070.344 us | 0.22x |
| `aws-keys/quick` | 1,971.536 us | 9,654.625 us | 47,856.900 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 128.699 us | 666.880 us | 1,122.466 us | 0.19x |
| `aws-keys/full` | 8,770.622 us | 88,117.000 us | 118,977.000 us | 0.10x |
| `bounded-repeat/context` | 9,061.544 us | 149,270.700 us | 153,104.900 us | 0.06x |
| `ruff-noqa/real` | 3,476.332 us | 70,638.700 us | 86,273.600 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 859.248 us | 23,512.875 us | 28,891.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 104.957 us | 11,262.114 us | 12,181.986 us | 0.0093x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.086 us | 0.112 us | 0.149 us | 0.77x |
| `common/ip-match` | `IsMatch` | 0.116 us | 0.173 us | 0.194 us | 0.67x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.077 us | 0.091 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.110 us | 0.140 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,201.221 us | 1,922.097 us | 10,559.475 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.085 us | 0.102 us | 0.62x |
| `industry/mariomka-email-count` | `Count` | 277.884 us | 504.809 us | 9,094.500 us | 0.55x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.083 us | 0.098 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.142 us | 0.266 us | 0.292 us | 0.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.082 us | 0.170 us | 0.186 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 626.118 us | 1,312.230 us | 8,509.580 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.087 us | 0.198 us | 0.220 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.282 us | 0.717 us | 0.743 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,162.229 us | 8,270.100 us | 16,791.900 us | 0.38x |
| `common/match-word` | `Match` | 0.050 us | 0.131 us | 1.179 us | 0.38x |
| `common/uri-match` | `IsMatch` | 0.083 us | 0.232 us | 0.273 us | 0.36x |
| `common/matches-set` | `Count` | 63.114 us | 195.541 us | 195.681 us | 0.32x |
| `common/matches-word` | `Count` | 1.244 us | 4.288 us | 6.022 us | 0.29x |
| `common/replace-words` | `Replace` | 20.553 us | 78.162 us | 78.396 us | 0.26x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 68.554 us | 263.727 us | 511.184 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.560 us | 0.596 us | 0.20x |
| `common/matches-boundary` | `Count` | 30.219 us | 166.249 us | 168.089 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.452 us | 966.115 us | 1,270.210 us | 0.18x |
| `common/split-words` | `Split` | 13.234 us | 77.639 us | 79.646 us | 0.17x |
| `common/matches-words` | `Count` | 9.282 us | 70.691 us | 72.040 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,115.762 us | 9,910.462 us | 17,152.633 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,895.980 us | 19,635.725 us | 20,106.875 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.620 us | 0.660 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.351 us | 0.375 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,740.625 us | 33,711.833 us | 41,133.750 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 998.793 us | 22,948.175 us | 30,298.850 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.831 us | 0.871 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 242.954 us | 8,272.520 us | 8,786.100 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 292.191 us | 11,911.614 us | 12,463.450 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.979 us | 36.252 us | 2,199.562 us | 1.21x |
| `bounded-repeat/letters-ru` | 2,080.056 us | 2,069.876 us | 2,960.038 us | 1.00x |
| `literal-alternate/sherlock-en-mixed` | 169.730 us | 174.998 us | 612.235 us | 0.97x |
| `literal-alternate/sherlock-en` | 346.108 us | 370.627 us | 902.621 us | 0.93x |
| `literal/sherlock-zh` | 22.335 us | 24.194 us | 2,202.091 us | 0.92x |
| `ruff-noqa/tweaked` | 4,172.217 us | 4,604.494 us | 36,345.100 us | 0.91x |
| `literal/sherlock-casei-en` | 69.150 us | 77.282 us | 471.484 us | 0.89x |
| `bounded-repeat/capitals` | 12,037.286 us | 13,620.067 us | 16,705.600 us | 0.88x |
| `literal/sherlock-ru` | 69.699 us | 88.891 us | 5,242.673 us | 0.78x |
| `literal-alternate/sherlock-casei-en` | 104.956 us | 146.265 us | 696.592 us | 0.72x |
| `literal/sherlock-casei-ru` | 146.478 us | 222.829 us | 5,370.764 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 129.126 us | 204.821 us | 670.171 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,436.253 us | 2,349.606 us | 7,553.200 us | 0.61x |
| `bounded-repeat/letters-en` | 1,110.527 us | 1,829.926 us | 1,964.205 us | 0.61x |
| `aws-keys/quick` | 1,970.697 us | 3,691.886 us | 39,675.100 us | 0.53x |
| `aws-keys/full` | 8,779.278 us | 18,737.940 us | 51,555.367 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 860.095 us | 1,979.121 us | 7,352.075 us | 0.43x |
| `literal/sherlock-en` | 31.734 us | 75.899 us | 434.793 us | 0.42x |
| `dictionary/single` | 8,187.220 us | 45,697.000 us | 45,871.000 us | 0.18x |
| `ruff-noqa/real` | 3,498.464 us | 20,878.125 us | 35,502.633 us | 0.17x |
| `bounded-repeat/context` | 9,060.267 us | 69,444.400 us | 72,872.500 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.111 us | 0.049 us | 0.070 us | 2.28x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.043 us | 0.073 us | 1.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.032 us | 0.049 us | 1.53x |
| `common/ip-miss` | `IsMatch` | 0.279 us | 0.183 us | 0.204 us | 1.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.056 us | 0.077 us | 1.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.047 us | 0.035 us | 0.052 us | 1.34x |
| `common/uri-miss` | `IsMatch` | 0.088 us | 0.067 us | 0.085 us | 1.33x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.031 us | 0.047 us | 1.31x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 68.763 us | 58.262 us | 287.920 us | 1.18x |
| `common/replace-words` | `Replace` | 20.561 us | 17.652 us | 18.942 us | 1.16x |
| `industry/leipzig-river-window-count` | `Count` | 1,751.895 us | 1,546.720 us | 8,075.800 us | 1.13x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.054 us | 0.050 us | 0.089 us | 1.10x |
| `industry/mariomka-uri-count` | `Count` | 1,200.811 us | 1,130.103 us | 8,506.400 us | 1.06x |
| `common/match-word` | `Match` | 0.051 us | 0.048 us | 0.945 us | 1.06x |
| `common/email-match` | `IsMatch` | 0.076 us | 0.082 us | 0.109 us | 0.93x |
| `common/uri-match` | `IsMatch` | 0.083 us | 0.090 us | 0.122 us | 0.92x |
| `industry/mariomka-ip-count` | `Count` | 3,190.925 us | 3,470.627 us | 10,934.800 us | 0.92x |
| `common/matches-words` | `Count` | 9.277 us | 10.793 us | 13.980 us | 0.86x |
| `industry/leipzig-symbol-count` | `Count` | 1,023.903 us | 1,207.581 us | 7,519.500 us | 0.85x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.044 us | 0.073 us | 0.74x |
| `common/split-words` | `Split` | 13.229 us | 20.642 us | 22.630 us | 0.64x |
| `common/matches-set` | `Count` | 63.138 us | 105.518 us | 107.315 us | 0.60x |
| `industry/mariomka-email-count` | `Count` | 280.136 us | 474.167 us | 7,783.275 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.200 us | 0.236 us | 0.55x |
| `common/matches-word` | `Count` | 1.242 us | 2.372 us | 3.617 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 633.282 us | 1,277.787 us | 7,467.740 us | 0.50x |
| `industry/leipzig-name-family-count` | `Count` | 1,112.794 us | 3,019.324 us | 8,438.400 us | 0.37x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.108 us | 0.131 us | 0.34x |
| `common/matches-boundary` | `Count` | 30.105 us | 105.343 us | 106.993 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 170.084 us | 599.418 us | 869.583 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,899.828 us | 8,835.000 us | 9,382.425 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.089 us | 0.116 us | 0.20x |
| `industry/rust-sherlock-ing-count` | `Count` | 293.431 us | 4,790.031 us | 5,209.843 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 243.249 us | 3,991.479 us | 4,369.372 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.054 us | 6.490 us | 32.834 us | 0.93x |
| `fallback/lookahead` | 5.444 us | 6.066 us | 20.777 us | 0.90x |
| `lokad/langserv/color-short-hex` | 0.585 us | 0.828 us | 1.001 us | 0.71x |
| `structural/ordered-keyword-window` | 12.153 us | 17.761 us | 32.296 us | 0.68x |
| `structural/method-family-generic-call` | 4.355 us | 6.433 us | 21.352 us | 0.68x |
| `lokad/lexer/identifier` | 9.220 us | 13.713 us | 16.721 us | 0.67x |
| `structural/method-family-call` | 5.318 us | 8.059 us | 22.580 us | 0.66x |
| `lokad/langserv/identifier-validator` | 4.911 us | 7.448 us | 8.988 us | 0.66x |
| `lokad/lexer/doc-line` | 1.215 us | 1.865 us | 2.615 us | 0.65x |
| `structural/keyword-to-generic-type` | 2.450 us | 4.098 us | 19.263 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.383 us | 17.594 us | 32.136 us | 0.59x |
| `lokad/docs/inline-doc-prefix` | 1.988 us | 3.444 us | 4.195 us | 0.58x |
| `structural/ordered-keyword-pair` | 2.855 us | 5.279 us | 20.108 us | 0.54x |
| `literal/identifier-token` | 2.288 us | 4.387 us | 18.835 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.475 us | 4.762 us | 5.682 us | 0.52x |
| `lokad/style/hex-color` | 0.268 us | 0.524 us | 0.606 us | 0.51x |
| `lokad/lexer/operator-run` | 1.188 us | 2.326 us | 2.799 us | 0.51x |
| `lokad/style/cell-ref` | 0.855 us | 1.933 us | 2.179 us | 0.44x |
| `lokad/style/range-ref` | 0.806 us | 1.823 us | 1.985 us | 0.44x |
| `literal/call-token` | 1.858 us | 4.206 us | 18.802 us | 0.44x |
| `lokad/langserv/url-dashboard` | 9.151 us | 23.436 us | 23.878 us | 0.39x |
| `literal-family/type-token-family` | 9.298 us | 24.027 us | 40.061 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.692 us | 1.813 us | 2.055 us | 0.38x |
| `lokad/lexer/string` | 32.282 us | 85.123 us | 89.015 us | 0.38x |
| `lokad/lexer/number` | 3.200 us | 10.838 us | 11.971 us | 0.30x |
| `structural/ordered-identifier-window` | 8.333 us | 28.822 us | 43.453 us | 0.29x |
| `literal-family/method-token-family` | 2.643 us | 9.266 us | 23.746 us | 0.29x |
| `literal/identifier-token-casei` | 3.936 us | 15.195 us | 30.246 us | 0.26x |
| `structural/modifier-family-to-type-window` | 59.572 us | 264.230 us | 273.703 us | 0.23x |
| `lokad/langserv/url-download` | 11.592 us | 52.136 us | 52.341 us | 0.22x |
| `lokad/folding/region-marker` | 5.789 us | 36.854 us | 64.434 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.937 us | 215.285 us | 231.503 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.934 us | 128.904 us | 143.847 us | 0.02x |
| `balancing/begin-end-blocks` | 48.226 us | 4,305.017 us | 4,327.128 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.172 us | 6.392 us | 6.844 us | 1.44x |
| `structural/ordered-keyword-window` | 12.152 us | 8.590 us | 22.920 us | 1.41x |
| `lokad/docs/inline-doc-prefix` | 2.001 us | 1.430 us | 2.168 us | 1.40x |
| `lokad/imports/module-imports` | 6.095 us | 4.405 us | 29.855 us | 1.38x |
| `structural/keyword-family-to-capitalized-identifier` | 9.922 us | 7.719 us | 22.079 us | 1.29x |
| `fallback/lookahead` | 5.436 us | 4.257 us | 18.444 us | 1.28x |
| `lokad/lexer/operator-run` | 1.176 us | 0.958 us | 1.436 us | 1.23x |
| `structural/ordered-modifier-pair` | 10.399 us | 8.686 us | 23.048 us | 1.20x |
| `lokad/lexer/identifier` | 9.231 us | 8.076 us | 10.728 us | 1.14x |
| `lokad/langserv/identifier-validator` | 4.924 us | 4.345 us | 5.911 us | 1.13x |
| `lokad/lexer/doc-line` | 1.214 us | 1.074 us | 1.847 us | 1.13x |
| `lokad/style/range-ref` | 0.799 us | 0.740 us | 0.876 us | 1.08x |
| `lokad/langserv/color-short-hex` | 0.516 us | 0.482 us | 0.676 us | 1.07x |
| `lokad/lexer/string` | 32.652 us | 30.553 us | 33.707 us | 1.07x |
| `structural/modifier-family-to-type-window` | 59.135 us | 56.461 us | 72.164 us | 1.05x |
| `structural/method-family-call` | 5.351 us | 5.110 us | 19.163 us | 1.05x |
| `lokad/style/cell-ref` | 0.861 us | 0.836 us | 1.050 us | 1.03x |
| `lokad/style/hex-color` | 0.219 us | 0.230 us | 0.311 us | 0.95x |
| `lokad/langserv/url-download` | 11.621 us | 12.313 us | 12.806 us | 0.94x |
| `structural/method-family-generic-call` | 4.342 us | 4.621 us | 18.500 us | 0.94x |
| `lokad/langserv/helper-identifier` | 2.489 us | 2.710 us | 3.559 us | 0.92x |
| `lokad/lexer/number` | 3.176 us | 3.864 us | 4.921 us | 0.82x |
| `literal-family/type-token-family` | 9.298 us | 11.533 us | 26.746 us | 0.81x |
| `structural/ordered-identifier-window` | 8.288 us | 10.682 us | 25.372 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 0.647 us | 0.847 us | 1.107 us | 0.76x |
| `literal/identifier-token-casei` | 3.941 us | 5.485 us | 20.579 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.448 us | 3.526 us | 18.078 us | 0.69x |
| `structural/ordered-keyword-pair` | 2.842 us | 4.136 us | 18.196 us | 0.69x |
| `literal-family/method-token-family` | 2.659 us | 5.345 us | 19.504 us | 0.50x |
| `literal/identifier-token` | 2.292 us | 4.744 us | 18.120 us | 0.48x |
| `literal/call-token` | 1.866 us | 3.992 us | 17.724 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.931 us | 10.608 us | 25.166 us | 0.28x |
| `lokad/folding/region-marker` | 5.852 us | 35.864 us | 63.843 us | 0.16x |
| `balancing/begin-end-blocks` | 47.765 us | 473.416 us | 504.109 us | 0.10x |
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
