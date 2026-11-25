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
| `literal/sherlock-zh` | 21.933 us | 18.536 us | 2,163.343 us | 1.18x |
| `literal/sherlock-ru` | 66.794 us | 95.804 us | 5,180.979 us | 0.70x |
| `literal-alternate/sherlock-zh` | 41.667 us | 65.745 us | 2,135.572 us | 0.63x |
| `ruff-noqa/tweaked` | 3,903.010 us | 7,421.945 us | 41,185.150 us | 0.53x |
| `literal/sherlock-casei-ru` | 152.781 us | 358.991 us | 5,437.864 us | 0.43x |
| `literal/sherlock-casei-en` | 80.594 us | 199.310 us | 577.881 us | 0.40x |
| `bounded-repeat/letters-en` | 1,052.473 us | 2,735.850 us | 2,903.193 us | 0.38x |
| `bounded-repeat/letters-ru` | 2,091.430 us | 5,683.400 us | 6,581.458 us | 0.37x |
| `literal/sherlock-en` | 30.575 us | 84.561 us | 462.890 us | 0.36x |
| `dictionary/single` | 7,825.720 us | 24,567.300 us | 24,694.175 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,416.819 us | 4,701.912 us | 9,566.425 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 160.836 us | 573.856 us | 989.547 us | 0.28x |
| `bounded-repeat/capitals` | 11,478.671 us | 43,726.150 us | 48,059.650 us | 0.26x |
| `literal-alternate/sherlock-en` | 347.124 us | 1,444.253 us | 1,972.711 us | 0.24x |
| `aws-keys/quick` | 1,906.790 us | 9,415.288 us | 39,945.150 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 122.616 us | 623.483 us | 1,044.259 us | 0.20x |
| `aws-keys/full` | 8,431.550 us | 82,418.000 us | 112,509.600 us | 0.10x |
| `bounded-repeat/context` | 8,576.922 us | 144,064.500 us | 149,395.100 us | 0.06x |
| `ruff-noqa/real` | 3,388.148 us | 68,536.300 us | 101,593.700 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 819.653 us | 22,099.950 us | 27,017.700 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 97.294 us | 10,855.562 us | 11,608.243 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 370.291 us | 482.009 us | 8,741.225 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.074 us | 0.105 us | 0.140 us | 0.71x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.049 us | 0.073 us | 0.087 us | 0.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.080 us | 0.096 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.067 us | 0.106 us | 0.134 us | 0.63x |
| `industry/mariomka-uri-count` | `Count` | 1,145.808 us | 1,849.832 us | 9,627.475 us | 0.62x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.042 us | 0.080 us | 0.094 us | 0.53x |
| `common/email-match` | `IsMatch` | 0.124 us | 0.255 us | 0.279 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 615.026 us | 1,281.039 us | 8,407.944 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.072 us | 0.162 us | 0.181 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.072 us | 0.166 us | 0.186 us | 0.43x |
| `common/uri-miss` | `IsMatch` | 0.076 us | 0.182 us | 0.200 us | 0.42x |
| `industry/mariomka-ip-count` | `Count` | 3,086.128 us | 7,995.140 us | 16,229.067 us | 0.39x |
| `common/match-word` | `Match` | 0.048 us | 0.126 us | 1.138 us | 0.38x |
| `common/matches-set` | `Count` | 64.761 us | 186.028 us | 188.373 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.077 us | 0.226 us | 0.262 us | 0.34x |
| `common/ip-miss` | `IsMatch` | 0.210 us | 0.698 us | 0.721 us | 0.30x |
| `common/matches-word` | `Count` | 1.198 us | 4.143 us | 5.800 us | 0.29x |
| `common/replace-words` | `Replace` | 20.019 us | 74.745 us | 76.382 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 64.926 us | 254.249 us | 491.791 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.103 us | 0.541 us | 0.572 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.387 us | 930.028 us | 1,192.305 us | 0.18x |
| `common/split-words` | `Split` | 12.853 us | 75.568 us | 77.608 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.105 us | 160.435 us | 161.693 us | 0.17x |
| `common/matches-words` | `Count` | 8.834 us | 67.353 us | 68.587 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,022.922 us | 9,393.678 us | 16,217.900 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,871.141 us | 18,701.475 us | 19,247.950 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 840.252 us | 11,389.400 us | 12,059.529 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,616.240 us | 31,125.833 us | 38,883.100 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.590 us | 0.622 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.332 us | 0.358 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.806 us | 0.843 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 934.296 us | 21,815.500 us | 29,292.000 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 255.224 us | 7,872.400 us | 8,416.900 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 41.545 us | 34.524 us | 2,091.409 us | 1.20x |
| `bounded-repeat/letters-ru` | 2,102.354 us | 1,996.400 us | 2,866.919 us | 1.05x |
| `literal-alternate/sherlock-en` | 346.762 us | 333.654 us | 748.769 us | 1.04x |
| `literal/sherlock-casei-en` | 81.193 us | 78.770 us | 447.938 us | 1.03x |
| `literal-alternate/sherlock-en-mixed` | 160.637 us | 167.650 us | 533.804 us | 0.96x |
| `literal/sherlock-zh` | 21.762 us | 23.642 us | 2,151.561 us | 0.92x |
| `ruff-noqa/tweaked` | 3,925.125 us | 4,469.006 us | 35,552.600 us | 0.88x |
| `bounded-repeat/capitals` | 11,315.029 us | 12,969.317 us | 16,325.275 us | 0.87x |
| `literal/sherlock-ru` | 66.359 us | 85.754 us | 5,086.000 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 97.409 us | 138.832 us | 536.378 us | 0.70x |
| `literal/sherlock-casei-ru` | 149.826 us | 216.360 us | 5,233.833 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 122.948 us | 191.332 us | 574.737 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,406.991 us | 2,269.506 us | 7,187.017 us | 0.62x |
| `bounded-repeat/letters-en` | 1,065.966 us | 1,767.277 us | 1,898.040 us | 0.60x |
| `aws-keys/quick` | 1,918.330 us | 3,600.759 us | 34,676.533 us | 0.53x |
| `aws-keys/full` | 8,563.989 us | 17,801.960 us | 49,296.933 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 813.934 us | 1,887.030 us | 6,804.525 us | 0.43x |
| `literal/sherlock-en` | 30.580 us | 73.457 us | 416.566 us | 0.42x |
| `dictionary/single` | 7,836.700 us | 43,670.450 us | 43,565.700 us | 0.18x |
| `ruff-noqa/real` | 3,412.530 us | 20,590.800 us | 54,516.933 us | 0.17x |
| `bounded-repeat/context` | 8,649.211 us | 67,478.150 us | 71,176.900 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.074 us | 0.047 us | 0.083 us | 1.59x |
| `common/date-match` | `IsMatch` | 0.066 us | 0.042 us | 0.071 us | 1.59x |
| `common/email-match` | `IsMatch` | 0.121 us | 0.077 us | 0.103 us | 1.57x |
| `common/ip-match` | `IsMatch` | 0.068 us | 0.047 us | 0.066 us | 1.46x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.043 us | 0.031 us | 0.049 us | 1.41x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.069 us | 0.054 us | 0.074 us | 1.28x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.041 us | 0.034 us | 0.051 us | 1.22x |
| `common/ip-miss` | `IsMatch` | 0.212 us | 0.175 us | 0.199 us | 1.21x |
| `common/uri-miss` | `IsMatch` | 0.075 us | 0.063 us | 0.083 us | 1.18x |
| `common/replace-words` | `Replace` | 19.930 us | 17.158 us | 20.026 us | 1.16x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.035 us | 0.030 us | 0.046 us | 1.16x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 64.935 us | 56.029 us | 260.181 us | 1.16x |
| `industry/leipzig-river-window-count` | `Count` | 1,629.542 us | 1,512.996 us | 7,981.225 us | 1.08x |
| `industry/mariomka-uri-count` | `Count` | 1,147.013 us | 1,084.261 us | 8,135.075 us | 1.06x |
| `common/match-word` | `Match` | 0.048 us | 0.046 us | 0.916 us | 1.05x |
| `industry/mariomka-ip-count` | `Count` | 3,080.848 us | 3,360.939 us | 10,684.267 us | 0.92x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.043 us | 0.072 us | 0.86x |
| `common/uri-match` | `IsMatch` | 0.074 us | 0.087 us | 0.119 us | 0.85x |
| `industry/mariomka-email-count` | `Count` | 369.487 us | 448.650 us | 7,561.475 us | 0.82x |
| `industry/leipzig-symbol-count` | `Count` | 942.106 us | 1,175.154 us | 7,137.420 us | 0.80x |
| `common/matches-set` | `Count` | 64.779 us | 101.267 us | 102.917 us | 0.64x |
| `common/matches-words` | `Count` | 8.822 us | 14.059 us | 16.948 us | 0.63x |
| `common/split-words` | `Split` | 12.796 us | 20.501 us | 22.410 us | 0.62x |
| `common/date-miss` | `IsMatch` | 0.103 us | 0.194 us | 0.234 us | 0.53x |
| `common/matches-word` | `Count` | 1.198 us | 2.281 us | 3.420 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 614.276 us | 1,264.107 us | 7,304.040 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,031.059 us | 2,944.877 us | 8,315.350 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.720 us | 577.369 us | 830.293 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.102 us | 0.123 us | 0.28x |
| `common/matches-boundary` | `Count` | 27.594 us | 100.982 us | 103.844 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,870.812 us | 8,463.022 us | 8,928.467 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.086 us | 0.111 us | 0.18x |
| `industry/rust-sherlock-ing-count` | `Count` | 833.987 us | 4,601.547 us | 5,004.081 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 259.761 us | 3,849.985 us | 4,255.589 us | 0.07x |
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
| `fallback/lookahead` | 5.365 us | 5.723 us | 18.595 us | 0.94x |
| `lokad/imports/module-imports` | 5.672 us | 6.114 us | 30.689 us | 0.93x |
| `structural/keyword-to-generic-type` | 2.883 us | 3.920 us | 16.999 us | 0.74x |
| `structural/ordered-keyword-window` | 11.261 us | 17.048 us | 30.394 us | 0.66x |
| `structural/method-family-call` | 5.067 us | 7.726 us | 20.503 us | 0.66x |
| `structural/method-family-generic-call` | 4.069 us | 6.368 us | 19.641 us | 0.64x |
| `structural/ordered-keyword-pair` | 3.188 us | 5.114 us | 18.500 us | 0.62x |
| `lokad/langserv/color-short-hex` | 0.487 us | 0.798 us | 0.979 us | 0.61x |
| `lokad/lexer/identifier` | 7.830 us | 13.075 us | 16.110 us | 0.60x |
| `structural/ordered-modifier-pair` | 9.819 us | 16.929 us | 29.933 us | 0.58x |
| `lokad/langserv/identifier-validator` | 4.116 us | 7.141 us | 8.735 us | 0.58x |
| `lokad/lexer/doc-line` | 0.993 us | 1.805 us | 2.519 us | 0.55x |
| `literal/identifier-token` | 2.266 us | 4.173 us | 16.699 us | 0.54x |
| `lokad/langserv/color-rgb-hex` | 0.883 us | 1.715 us | 1.960 us | 0.51x |
| `lokad/docs/inline-doc-prefix` | 1.711 us | 3.325 us | 4.093 us | 0.51x |
| `lokad/langserv/helper-identifier` | 2.246 us | 4.602 us | 5.420 us | 0.49x |
| `lokad/lexer/operator-run` | 0.999 us | 2.234 us | 2.640 us | 0.45x |
| `literal/call-token` | 1.763 us | 4.131 us | 16.668 us | 0.43x |
| `lokad/style/hex-color` | 0.206 us | 0.496 us | 0.591 us | 0.42x |
| `lokad/langserv/url-dashboard` | 9.241 us | 22.240 us | 22.859 us | 0.42x |
| `lokad/style/range-ref` | 0.719 us | 1.747 us | 1.915 us | 0.41x |
| `lokad/style/cell-ref` | 0.772 us | 1.885 us | 2.011 us | 0.41x |
| `literal-family/type-token-family` | 8.866 us | 22.271 us | 35.550 us | 0.40x |
| `lokad/lexer/string` | 29.833 us | 78.113 us | 82.125 us | 0.38x |
| `structural/ordered-identifier-window` | 7.803 us | 27.619 us | 41.073 us | 0.28x |
| `literal-family/method-token-family` | 2.546 us | 9.139 us | 21.538 us | 0.28x |
| `lokad/lexer/number` | 2.776 us | 10.381 us | 11.372 us | 0.27x |
| `literal/identifier-token-casei` | 3.362 us | 14.494 us | 27.392 us | 0.23x |
| `lokad/langserv/url-download` | 11.297 us | 49.443 us | 50.684 us | 0.23x |
| `structural/modifier-family-to-type-window` | 55.619 us | 245.790 us | 258.900 us | 0.23x |
| `lokad/folding/region-marker` | 5.555 us | 34.694 us | 59.514 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.521 us | 208.016 us | 226.728 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.823 us | 127.064 us | 140.270 us | 0.02x |
| `balancing/begin-end-blocks` | 47.154 us | 4,203.142 us | 4,196.828 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.251 us | 6.011 us | 6.586 us | 1.54x |
| `structural/ordered-keyword-window` | 11.293 us | 8.275 us | 21.147 us | 1.36x |
| `fallback/lookahead` | 5.408 us | 4.032 us | 16.474 us | 1.34x |
| `lokad/imports/module-imports` | 5.680 us | 4.251 us | 28.204 us | 1.34x |
| `structural/keyword-family-to-capitalized-identifier` | 9.526 us | 7.364 us | 20.465 us | 1.29x |
| `lokad/docs/inline-doc-prefix` | 1.713 us | 1.402 us | 2.127 us | 1.22x |
| `structural/ordered-modifier-pair` | 9.932 us | 8.271 us | 20.944 us | 1.20x |
| `lokad/lexer/operator-run` | 0.996 us | 0.902 us | 1.353 us | 1.10x |
| `lokad/langserv/color-rgb-hex` | 0.912 us | 0.832 us | 1.054 us | 1.10x |
| `structural/method-family-call` | 5.043 us | 4.847 us | 17.189 us | 1.04x |
| `structural/modifier-family-to-type-window` | 56.399 us | 54.316 us | 66.852 us | 1.04x |
| `lokad/lexer/identifier` | 7.810 us | 7.609 us | 10.484 us | 1.03x |
| `lokad/style/range-ref` | 0.721 us | 0.713 us | 0.833 us | 1.01x |
| `lokad/langserv/identifier-validator` | 4.118 us | 4.082 us | 5.630 us | 1.01x |
| `lokad/lexer/string` | 29.934 us | 29.737 us | 32.322 us | 1.01x |
| `lokad/lexer/doc-line` | 0.994 us | 1.027 us | 1.748 us | 0.97x |
| `lokad/langserv/color-short-hex` | 0.447 us | 0.466 us | 0.656 us | 0.96x |
| `lokad/langserv/url-download` | 11.198 us | 11.693 us | 12.129 us | 0.96x |
| `lokad/style/cell-ref` | 0.768 us | 0.813 us | 1.016 us | 0.95x |
| `structural/method-family-generic-call` | 4.070 us | 4.395 us | 16.995 us | 0.93x |
| `structural/keyword-to-generic-type` | 2.896 us | 3.336 us | 15.914 us | 0.87x |
| `lokad/langserv/helper-identifier` | 2.228 us | 2.608 us | 3.421 us | 0.85x |
| `lokad/style/hex-color` | 0.189 us | 0.223 us | 0.308 us | 0.85x |
| `literal-family/type-token-family` | 8.857 us | 10.985 us | 23.697 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.190 us | 3.986 us | 16.499 us | 0.80x |
| `structural/ordered-identifier-window` | 7.767 us | 10.253 us | 23.562 us | 0.76x |
| `lokad/lexer/number` | 2.768 us | 3.661 us | 4.677 us | 0.76x |
| `literal/identifier-token-casei` | 3.358 us | 5.639 us | 18.340 us | 0.60x |
| `literal-family/method-token-family` | 2.557 us | 4.970 us | 17.666 us | 0.51x |
| `literal/identifier-token` | 2.253 us | 4.502 us | 16.441 us | 0.50x |
| `literal/call-token` | 1.772 us | 3.809 us | 15.962 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.807 us | 10.321 us | 23.722 us | 0.27x |
| `lokad/folding/region-marker` | 5.587 us | 34.493 us | 59.429 us | 0.16x |
| `balancing/begin-end-blocks` | 47.381 us | 449.339 us | 480.088 us | 0.11x |
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
