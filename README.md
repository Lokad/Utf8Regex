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
| `literal/sherlock-zh` | 22.353 us | 18.689 us | 2,170.500 us | 1.20x |
| `literal/sherlock-ru` | 67.909 us | 99.049 us | 5,372.507 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.996 us | 67.775 us | 2,190.654 us | 0.63x |
| `ruff-noqa/tweaked` | 4,020.516 us | 7,848.410 us | 42,016.350 us | 0.51x |
| `literal/sherlock-casei-en` | 79.964 us | 186.619 us | 602.942 us | 0.43x |
| `literal/sherlock-casei-ru` | 145.749 us | 363.631 us | 5,527.650 us | 0.40x |
| `bounded-repeat/letters-en` | 1,122.149 us | 2,874.089 us | 3,027.024 us | 0.39x |
| `literal/sherlock-en` | 31.699 us | 85.183 us | 469.539 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,117.866 us | 5,768.286 us | 6,657.642 us | 0.37x |
| `dictionary/single` | 8,164.520 us | 25,777.833 us | 25,722.033 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,451.598 us | 4,735.069 us | 10,016.600 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 158.515 us | 622.487 us | 1,049.770 us | 0.25x |
| `bounded-repeat/capitals` | 11,597.071 us | 46,549.500 us | 50,217.900 us | 0.25x |
| `literal-alternate/sherlock-en` | 346.646 us | 1,531.059 us | 2,070.439 us | 0.23x |
| `aws-keys/quick` | 2,033.362 us | 9,567.675 us | 46,713.200 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 128.283 us | 683.551 us | 1,119.538 us | 0.19x |
| `aws-keys/full` | 9,778.644 us | 85,921.200 us | 115,677.100 us | 0.11x |
| `bounded-repeat/context` | 8,866.833 us | 146,995.500 us | 151,153.400 us | 0.06x |
| `ruff-noqa/real` | 3,422.914 us | 69,066.200 us | 85,796.000 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 866.822 us | 23,076.375 us | 28,415.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 102.272 us | 11,676.843 us | 12,470.267 us | 0.0088x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 385.867 us | 495.078 us | 8,985.600 us | 0.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.075 us | 0.110 us | 0.148 us | 0.68x |
| `industry/mariomka-uri-count` | `Count` | 1,196.576 us | 1,854.537 us | 10,498.467 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.111 us | 0.139 us | 0.64x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.047 us | 0.075 us | 0.090 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.101 us | 0.60x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.083 us | 0.099 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 631.648 us | 1,330.523 us | 8,709.450 us | 0.47x |
| `common/email-match` | `IsMatch` | 0.122 us | 0.268 us | 0.290 us | 0.46x |
| `common/uri-miss` | `IsMatch` | 0.082 us | 0.184 us | 0.202 us | 0.45x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.074 us | 0.169 us | 0.186 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.174 us | 0.193 us | 0.43x |
| `common/match-word` | `Match` | 0.051 us | 0.127 us | 1.194 us | 0.40x |
| `industry/mariomka-ip-count` | `Count` | 3,209.617 us | 8,578.767 us | 17,147.700 us | 0.37x |
| `common/matches-set` | `Count` | 67.212 us | 197.167 us | 199.622 us | 0.34x |
| `common/uri-match` | `IsMatch` | 0.076 us | 0.231 us | 0.265 us | 0.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.775 us | 263.261 us | 502.259 us | 0.30x |
| `common/ip-miss` | `IsMatch` | 0.218 us | 0.741 us | 0.762 us | 0.29x |
| `common/matches-word` | `Count` | 1.237 us | 4.218 us | 5.928 us | 0.29x |
| `common/replace-words` | `Replace` | 20.468 us | 77.275 us | 78.441 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.554 us | 0.586 us | 0.20x |
| `common/matches-boundary` | `Count` | 28.310 us | 162.070 us | 163.826 us | 0.17x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.552 us | 964.078 us | 1,236.522 us | 0.17x |
| `common/split-words` | `Split` | 13.288 us | 79.160 us | 81.051 us | 0.17x |
| `common/matches-words` | `Count` | 9.242 us | 70.932 us | 72.772 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,067.710 us | 9,915.475 us | 17,104.833 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,945.097 us | 19,447.975 us | 20,079.975 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 851.594 us | 11,753.657 us | 12,404.350 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.611 us | 0.640 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.830 us | 0.872 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.344 us | 0.369 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 983.563 us | 24,335.350 us | 31,855.850 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,227.521 us | 31,839.033 us | 39,681.200 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 259.903 us | 8,422.200 us | 8,810.011 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 42.869 us | 36.223 us | 2,168.443 us | 1.18x |
| `bounded-repeat/letters-ru` | 2,139.097 us | 2,023.768 us | 2,886.778 us | 1.06x |
| `literal/sherlock-casei-en` | 80.300 us | 79.411 us | 479.325 us | 1.01x |
| `literal-alternate/sherlock-en` | 346.161 us | 367.607 us | 777.025 us | 0.94x |
| `literal/sherlock-zh` | 22.101 us | 23.664 us | 2,167.335 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 158.126 us | 175.863 us | 564.560 us | 0.90x |
| `ruff-noqa/tweaked` | 4,031.263 us | 4,545.418 us | 37,091.750 us | 0.89x |
| `bounded-repeat/capitals` | 11,650.829 us | 13,533.800 us | 16,474.550 us | 0.86x |
| `literal-alternate/sherlock-casei-en` | 103.457 us | 131.640 us | 601.232 us | 0.79x |
| `literal/sherlock-ru` | 67.370 us | 89.919 us | 5,264.943 us | 0.75x |
| `literal/sherlock-casei-ru` | 147.096 us | 221.619 us | 5,286.347 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 127.996 us | 201.961 us | 587.044 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,453.886 us | 2,317.650 us | 7,517.400 us | 0.63x |
| `bounded-repeat/letters-en` | 1,121.221 us | 1,849.724 us | 1,978.967 us | 0.61x |
| `aws-keys/quick` | 2,027.171 us | 3,681.552 us | 39,392.450 us | 0.55x |
| `aws-keys/full` | 8,913.411 us | 18,484.620 us | 50,636.933 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 873.344 us | 1,954.013 us | 7,156.600 us | 0.45x |
| `literal/sherlock-en` | 31.830 us | 74.604 us | 447.721 us | 0.43x |
| `dictionary/single` | 8,129.850 us | 45,272.800 us | 45,527.350 us | 0.18x |
| `ruff-noqa/real` | 3,445.048 us | 20,463.825 us | 47,470.833 us | 0.17x |
| `bounded-repeat/context` | 8,893.933 us | 68,684.250 us | 72,338.350 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.071 us | 0.044 us | 0.075 us | 1.60x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.073 us | 0.048 us | 0.088 us | 1.53x |
| `common/email-match` | `IsMatch` | 0.121 us | 0.081 us | 0.106 us | 1.50x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.048 us | 0.068 us | 1.48x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.045 us | 0.032 us | 0.050 us | 1.43x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.808 us | 57.700 us | 282.783 us | 1.35x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.072 us | 0.057 us | 0.078 us | 1.27x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.043 us | 0.034 us | 0.054 us | 1.25x |
| `common/uri-miss` | `IsMatch` | 0.081 us | 0.066 us | 0.086 us | 1.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.035 us | 0.029 us | 0.048 us | 1.19x |
| `common/ip-miss` | `IsMatch` | 0.214 us | 0.181 us | 0.202 us | 1.18x |
| `common/replace-words` | `Replace` | 20.594 us | 17.802 us | 20.759 us | 1.16x |
| `industry/mariomka-uri-count` | `Count` | 1,206.306 us | 1,106.668 us | 8,563.367 us | 1.09x |
| `common/match-word` | `Match` | 0.051 us | 0.049 us | 0.957 us | 1.05x |
| `industry/mariomka-ip-count` | `Count` | 3,212.283 us | 3,431.032 us | 10,971.200 us | 0.94x |
| `common/backtracking` | `IsMatch` | 0.038 us | 0.043 us | 0.073 us | 0.87x |
| `common/matches-words` | `Count` | 9.285 us | 10.876 us | 13.933 us | 0.85x |
| `common/uri-match` | `IsMatch` | 0.074 us | 0.086 us | 0.119 us | 0.85x |
| `industry/mariomka-email-count` | `Count` | 385.243 us | 462.703 us | 7,781.500 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 984.416 us | 1,253.381 us | 7,797.825 us | 0.79x |
| `industry/leipzig-river-window-count` | `Count` | 1,236.590 us | 1,583.576 us | 8,073.540 us | 0.78x |
| `common/matches-set` | `Count` | 67.369 us | 105.957 us | 107.853 us | 0.64x |
| `common/split-words` | `Split` | 13.277 us | 21.083 us | 22.844 us | 0.63x |
| `common/date-miss` | `IsMatch` | 0.110 us | 0.199 us | 0.235 us | 0.55x |
| `common/matches-word` | `Count` | 1.232 us | 2.341 us | 3.801 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 634.418 us | 1,308.005 us | 6,829.667 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,072.661 us | 3,029.804 us | 8,566.075 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.928 us | 595.277 us | 858.912 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.107 us | 0.128 us | 0.27x |
| `common/matches-boundary` | `Count` | 28.170 us | 103.451 us | 104.958 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,935.762 us | 8,615.667 us | 9,183.411 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 849.578 us | 4,755.975 us | 5,198.840 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.089 us | 0.117 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 265.851 us | 3,964.370 us | 4,314.633 us | 0.07x |
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
| `lokad/imports/module-imports` | 6.859 us | 6.457 us | 31.322 us | 1.06x |
| `fallback/lookahead` | 5.585 us | 6.002 us | 19.062 us | 0.93x |
| `lokad/langserv/identifier-validator` | 6.126 us | 7.436 us | 8.969 us | 0.82x |
| `structural/method-family-generic-call` | 5.439 us | 6.630 us | 20.312 us | 0.82x |
| `structural/method-family-call` | 6.467 us | 7.943 us | 21.320 us | 0.81x |
| `lokad/lexer/doc-line` | 1.504 us | 1.859 us | 2.573 us | 0.81x |
| `lokad/lexer/identifier` | 10.999 us | 13.684 us | 16.459 us | 0.80x |
| `structural/ordered-keyword-window` | 13.839 us | 17.422 us | 30.787 us | 0.79x |
| `lokad/langserv/helper-identifier` | 3.749 us | 4.877 us | 5.810 us | 0.77x |
| `structural/keyword-to-generic-type` | 3.020 us | 4.052 us | 17.555 us | 0.75x |
| `structural/ordered-modifier-pair` | 11.151 us | 17.484 us | 30.409 us | 0.64x |
| `lokad/langserv/color-short-hex` | 0.518 us | 0.820 us | 1.012 us | 0.63x |
| `structural/ordered-keyword-pair` | 3.285 us | 5.312 us | 19.647 us | 0.62x |
| `literal/identifier-token` | 2.337 us | 4.341 us | 17.184 us | 0.54x |
| `lokad/docs/inline-doc-prefix` | 1.732 us | 3.448 us | 4.212 us | 0.50x |
| `lokad/lexer/operator-run` | 1.034 us | 2.327 us | 2.787 us | 0.44x |
| `literal/call-token` | 1.843 us | 4.278 us | 17.348 us | 0.43x |
| `lokad/style/hex-color` | 0.212 us | 0.512 us | 0.594 us | 0.41x |
| `lokad/langserv/url-dashboard` | 9.538 us | 23.491 us | 23.815 us | 0.41x |
| `lokad/style/range-ref` | 0.736 us | 1.819 us | 1.997 us | 0.40x |
| `lokad/lexer/string` | 32.360 us | 80.375 us | 84.943 us | 0.40x |
| `lokad/style/cell-ref` | 0.784 us | 1.952 us | 2.088 us | 0.40x |
| `literal-family/type-token-family` | 9.106 us | 23.738 us | 37.710 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.648 us | 1.830 us | 2.061 us | 0.35x |
| `structural/ordered-identifier-window` | 8.695 us | 28.270 us | 41.894 us | 0.31x |
| `literal-family/method-token-family` | 2.605 us | 9.294 us | 21.734 us | 0.28x |
| `structural/modifier-family-to-type-window` | 66.307 us | 256.124 us | 271.566 us | 0.26x |
| `lokad/lexer/number` | 2.790 us | 10.849 us | 11.781 us | 0.26x |
| `literal/identifier-token-casei` | 3.460 us | 14.948 us | 28.521 us | 0.23x |
| `lokad/langserv/url-download` | 11.578 us | 50.490 us | 52.267 us | 0.23x |
| `lokad/folding/region-marker` | 5.682 us | 35.784 us | 61.908 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 13.098 us | 226.455 us | 241.979 us | 0.06x |
| `structural/modifier-family-to-type-pair` | 2.884 us | 129.311 us | 143.151 us | 0.02x |
| `balancing/begin-end-blocks` | 46.832 us | 4,441.300 us | 4,431.924 us | 0.01x |
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
| `structural/keyword-family-to-capitalized-identifier` | 13.102 us | 7.942 us | 21.659 us | 1.65x |
| `structural/ordered-keyword-window` | 13.877 us | 8.583 us | 21.434 us | 1.62x |
| `lokad/imports/module-imports` | 6.851 us | 4.413 us | 28.237 us | 1.55x |
| `lokad/langserv/url-dashboard` | 9.575 us | 6.300 us | 6.882 us | 1.52x |
| `lokad/langserv/identifier-validator` | 6.120 us | 4.177 us | 5.733 us | 1.47x |
| `lokad/lexer/doc-line` | 1.510 us | 1.039 us | 1.833 us | 1.45x |
| `lokad/lexer/identifier` | 10.998 us | 7.903 us | 10.537 us | 1.39x |
| `lokad/langserv/helper-identifier` | 3.745 us | 2.717 us | 3.587 us | 1.38x |
| `fallback/lookahead` | 5.527 us | 4.178 us | 16.973 us | 1.32x |
| `structural/ordered-modifier-pair` | 11.193 us | 8.593 us | 21.468 us | 1.30x |
| `structural/method-family-call` | 6.462 us | 5.055 us | 17.986 us | 1.28x |
| `lokad/docs/inline-doc-prefix` | 1.720 us | 1.438 us | 2.172 us | 1.20x |
| `structural/modifier-family-to-type-window` | 66.523 us | 56.046 us | 70.114 us | 1.19x |
| `structural/method-family-generic-call` | 5.396 us | 4.606 us | 17.432 us | 1.17x |
| `lokad/lexer/operator-run` | 1.028 us | 0.949 us | 1.414 us | 1.08x |
| `lokad/lexer/string` | 32.215 us | 30.304 us | 33.666 us | 1.06x |
| `lokad/style/range-ref` | 0.738 us | 0.736 us | 0.870 us | 1.00x |
| `lokad/langserv/url-download` | 11.591 us | 12.063 us | 12.696 us | 0.96x |
| `lokad/langserv/color-short-hex` | 0.460 us | 0.482 us | 0.678 us | 0.95x |
| `lokad/style/cell-ref` | 0.784 us | 0.838 us | 1.051 us | 0.94x |
| `structural/keyword-to-generic-type` | 3.053 us | 3.510 us | 16.444 us | 0.87x |
| `lokad/style/hex-color` | 0.194 us | 0.229 us | 0.317 us | 0.85x |
| `structural/ordered-identifier-window` | 8.704 us | 10.474 us | 23.989 us | 0.83x |
| `literal-family/type-token-family` | 9.126 us | 11.337 us | 24.521 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.258 us | 4.125 us | 17.323 us | 0.79x |
| `lokad/lexer/number` | 2.819 us | 3.843 us | 4.881 us | 0.73x |
| `lokad/langserv/color-rgb-hex` | 0.579 us | 0.842 us | 1.118 us | 0.69x |
| `literal/identifier-token-casei` | 3.461 us | 5.881 us | 19.204 us | 0.59x |
| `literal-family/method-token-family` | 2.610 us | 5.113 us | 17.620 us | 0.51x |
| `literal/identifier-token` | 2.328 us | 4.665 us | 16.902 us | 0.50x |
| `literal/call-token` | 1.854 us | 3.933 us | 16.441 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.896 us | 10.652 us | 23.676 us | 0.27x |
| `lokad/folding/region-marker` | 5.584 us | 35.400 us | 62.218 us | 0.16x |
| `balancing/begin-end-blocks` | 47.321 us | 465.723 us | 501.794 us | 0.10x |
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
