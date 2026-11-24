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
| `literal/sherlock-zh` | 21.483 us | 18.337 us | 2,175.991 us | 1.17x |
| `literal/sherlock-ru` | 65.933 us | 95.529 us | 5,225.140 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.735 us | 67.227 us | 2,163.243 us | 0.64x |
| `ruff-noqa/tweaked` | 3,994.047 us | 7,785.009 us | 41,507.650 us | 0.51x |
| `literal/sherlock-casei-en` | 79.910 us | 185.852 us | 632.180 us | 0.43x |
| `literal/sherlock-casei-ru` | 148.619 us | 361.199 us | 5,493.807 us | 0.41x |
| `bounded-repeat/letters-en` | 1,106.449 us | 2,740.437 us | 2,904.388 us | 0.40x |
| `literal/sherlock-en` | 31.538 us | 85.164 us | 444.078 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,025.889 us | 5,738.386 us | 6,659.892 us | 0.35x |
| `dictionary/single` | 8,092.920 us | 25,251.900 us | 25,325.775 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,426.581 us | 4,669.165 us | 9,683.450 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 163.696 us | 574.423 us | 1,006.874 us | 0.28x |
| `bounded-repeat/capitals` | 11,509.457 us | 45,723.200 us | 52,447.900 us | 0.25x |
| `literal-alternate/sherlock-en` | 344.174 us | 1,446.777 us | 1,972.739 us | 0.24x |
| `aws-keys/quick` | 1,863.934 us | 9,495.200 us | 46,155.950 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 126.034 us | 654.417 us | 1,078.060 us | 0.19x |
| `aws-keys/full` | 8,479.344 us | 85,082.600 us | 117,418.700 us | 0.10x |
| `bounded-repeat/context` | 8,851.078 us | 145,613.900 us | 150,301.800 us | 0.06x |
| `ruff-noqa/real` | 3,373.617 us | 72,541.300 us | 101,957.600 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 840.296 us | 23,682.675 us | 29,130.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 101.141 us | 11,129.557 us | 11,888.329 us | 0.0091x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.079 us | 0.076 us | 0.088 us | 1.04x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.081 us | 0.081 us | 0.096 us | 1.01x |
| `common/ip-match` | `IsMatch` | 0.164 us | 0.170 us | 0.192 us | 0.96x |
| `common/date-match` | `IsMatch` | 0.102 us | 0.108 us | 0.137 us | 0.95x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.068 us | 0.084 us | 0.100 us | 0.81x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.131 us | 0.165 us | 0.183 us | 0.80x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.087 us | 0.111 us | 0.145 us | 0.79x |
| `industry/mariomka-email-count` | `Count` | 378.439 us | 497.079 us | 8,860.375 us | 0.76x |
| `common/uri-match` | `IsMatch` | 0.160 us | 0.227 us | 0.265 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.132 us | 0.196 us | 0.217 us | 0.67x |
| `industry/mariomka-uri-count` | `Count` | 1,207.312 us | 1,867.056 us | 10,157.850 us | 0.65x |
| `common/email-match` | `IsMatch` | 0.160 us | 0.266 us | 0.294 us | 0.60x |
| `common/ip-miss` | `IsMatch` | 0.386 us | 0.716 us | 0.741 us | 0.54x |
| `common/match-word` | `Match` | 0.066 us | 0.131 us | 1.151 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 613.423 us | 1,274.526 us | 8,279.333 us | 0.48x |
| `industry/mariomka-ip-count` | `Count` | 3,265.908 us | 8,081.740 us | 16,689.633 us | 0.40x |
| `common/matches-set` | `Count` | 61.784 us | 193.377 us | 194.363 us | 0.32x |
| `common/date-miss` | `IsMatch` | 0.175 us | 0.563 us | 0.594 us | 0.31x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 78.035 us | 256.226 us | 499.149 us | 0.30x |
| `common/matches-word` | `Count` | 1.230 us | 4.199 us | 5.982 us | 0.29x |
| `common/replace-words` | `Replace` | 20.115 us | 76.643 us | 77.077 us | 0.26x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.847 us | 960.529 us | 1,244.264 us | 0.17x |
| `common/split-words` | `Split` | 12.915 us | 79.935 us | 81.172 us | 0.16x |
| `common/matches-boundary` | `Count` | 22.924 us | 160.825 us | 160.679 us | 0.14x |
| `common/matches-words` | `Count` | 8.977 us | 69.025 us | 70.399 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,035.566 us | 9,983.962 us | 16,991.600 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,912.178 us | 19,160.725 us | 20,154.825 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 885.093 us | 11,717.057 us | 12,358.243 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.603 us | 0.633 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,470.367 us | 31,584.400 us | 38,680.850 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.818 us | 0.863 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.340 us | 0.366 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 993.246 us | 22,420.000 us | 29,565.050 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 273.729 us | 7,885.050 us | 8,398.178 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 42.601 us | 35.370 us | 2,141.271 us | 1.20x |
| `literal/sherlock-casei-en` | 79.741 us | 77.309 us | 453.063 us | 1.03x |
| `literal-alternate/sherlock-en` | 343.849 us | 337.564 us | 741.887 us | 1.02x |
| `bounded-repeat/letters-ru` | 2,033.853 us | 2,024.078 us | 2,905.265 us | 1.00x |
| `literal-alternate/sherlock-en-mixed` | 164.345 us | 170.683 us | 535.949 us | 0.96x |
| `literal/sherlock-zh` | 21.696 us | 23.373 us | 2,165.697 us | 0.93x |
| `ruff-noqa/tweaked` | 4,062.370 us | 4,537.253 us | 36,031.350 us | 0.90x |
| `bounded-repeat/capitals` | 11,505.229 us | 13,376.783 us | 20,544.600 us | 0.86x |
| `literal-alternate/sherlock-casei-en` | 101.057 us | 129.309 us | 587.551 us | 0.78x |
| `literal/sherlock-ru` | 66.603 us | 87.121 us | 5,148.080 us | 0.76x |
| `literal/sherlock-casei-ru` | 149.614 us | 220.700 us | 5,191.058 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 126.544 us | 200.209 us | 607.947 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,425.207 us | 2,307.806 us | 7,336.767 us | 0.62x |
| `bounded-repeat/letters-en` | 1,107.968 us | 1,796.173 us | 1,931.346 us | 0.62x |
| `aws-keys/quick` | 1,868.810 us | 3,603.791 us | 37,986.150 us | 0.52x |
| `aws-keys/full` | 8,431.675 us | 18,042.820 us | 52,324.833 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 839.228 us | 1,940.280 us | 7,147.900 us | 0.43x |
| `literal/sherlock-en` | 31.331 us | 74.415 us | 435.058 us | 0.42x |
| `dictionary/single` | 8,140.630 us | 44,910.850 us | 45,243.450 us | 0.18x |
| `ruff-noqa/real` | 3,437.064 us | 20,592.750 us | 51,819.400 us | 0.17x |
| `bounded-repeat/context` | 8,880.789 us | 68,199.500 us | 71,879.500 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.159 us | 0.048 us | 0.067 us | 3.30x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.079 us | 0.031 us | 0.048 us | 2.56x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.130 us | 0.054 us | 0.076 us | 2.40x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.068 us | 0.029 us | 0.049 us | 2.34x |
| `common/date-match` | `IsMatch` | 0.099 us | 0.043 us | 0.073 us | 2.32x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.078 us | 0.036 us | 0.052 us | 2.15x |
| `common/ip-miss` | `IsMatch` | 0.383 us | 0.179 us | 0.204 us | 2.14x |
| `common/uri-miss` | `IsMatch` | 0.133 us | 0.064 us | 0.085 us | 2.06x |
| `common/email-match` | `IsMatch` | 0.160 us | 0.078 us | 0.108 us | 2.03x |
| `common/uri-match` | `IsMatch` | 0.161 us | 0.088 us | 0.119 us | 1.82x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.086 us | 0.048 us | 0.083 us | 1.81x |
| `common/match-word` | `Match` | 0.067 us | 0.048 us | 0.935 us | 1.40x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.921 us | 57.593 us | 264.400 us | 1.35x |
| `common/replace-words` | `Replace` | 20.145 us | 17.232 us | 18.609 us | 1.17x |
| `industry/mariomka-uri-count` | `Count` | 1,207.458 us | 1,121.476 us | 8,285.600 us | 1.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,494.467 us | 1,514.525 us | 7,886.300 us | 0.99x |
| `industry/mariomka-ip-count` | `Count` | 3,282.139 us | 3,417.113 us | 10,727.300 us | 0.96x |
| `common/date-miss` | `IsMatch` | 0.176 us | 0.195 us | 0.233 us | 0.90x |
| `industry/leipzig-symbol-count` | `Count` | 1,004.900 us | 1,189.594 us | 7,190.720 us | 0.84x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.043 us | 0.073 us | 0.84x |
| `common/matches-words` | `Count` | 8.973 us | 10.795 us | 13.816 us | 0.83x |
| `industry/mariomka-email-count` | `Count` | 380.048 us | 466.390 us | 7,555.900 us | 0.81x |
| `common/split-words` | `Split` | 13.020 us | 20.353 us | 22.369 us | 0.64x |
| `common/matches-set` | `Count` | 62.147 us | 103.920 us | 106.225 us | 0.60x |
| `common/matches-word` | `Count` | 1.215 us | 2.314 us | 3.521 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 627.334 us | 1,255.764 us | 7,121.170 us | 0.50x |
| `industry/leipzig-name-family-count` | `Count` | 1,032.723 us | 2,966.665 us | 8,336.975 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.105 us | 0.126 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.455 us | 622.202 us | 892.466 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,920.387 us | 8,559.800 us | 9,104.633 us | 0.22x |
| `common/matches-boundary` | `Count` | 22.916 us | 102.625 us | 104.459 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 888.122 us | 4,744.675 us | 5,134.427 us | 0.19x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.089 us | 0.114 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 274.204 us | 3,879.605 us | 4,238.828 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.704 us | 6.276 us | 31.104 us | 1.23x |
| `fallback/lookahead` | 6.877 us | 5.971 us | 19.038 us | 1.15x |
| `structural/method-family-call` | 7.833 us | 7.809 us | 20.889 us | 1.00x |
| `structural/method-family-generic-call` | 6.499 us | 6.540 us | 19.685 us | 0.99x |
| `lokad/lexer/doc-line` | 1.641 us | 1.861 us | 2.595 us | 0.88x |
| `lokad/langserv/identifier-validator` | 6.443 us | 7.401 us | 8.867 us | 0.87x |
| `lokad/langserv/color-short-hex` | 0.708 us | 0.818 us | 0.996 us | 0.87x |
| `lokad/lexer/identifier` | 11.639 us | 13.624 us | 16.407 us | 0.85x |
| `lokad/style/hex-color` | 0.423 us | 0.509 us | 0.597 us | 0.83x |
| `structural/ordered-keyword-window` | 13.112 us | 17.346 us | 30.321 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 1.341 us | 1.786 us | 2.020 us | 0.75x |
| `lokad/lexer/operator-run` | 1.755 us | 2.356 us | 2.809 us | 0.74x |
| `structural/keyword-to-generic-type` | 2.948 us | 4.021 us | 17.591 us | 0.73x |
| `lokad/docs/inline-doc-prefix` | 2.453 us | 3.440 us | 4.197 us | 0.71x |
| `structural/ordered-keyword-pair` | 3.134 us | 5.229 us | 18.750 us | 0.60x |
| `lokad/lexer/number` | 6.233 us | 10.638 us | 11.680 us | 0.59x |
| `lokad/langserv/helper-identifier` | 2.774 us | 4.825 us | 5.724 us | 0.57x |
| `structural/ordered-modifier-pair` | 9.932 us | 17.316 us | 30.077 us | 0.57x |
| `literal/identifier-token` | 2.318 us | 4.300 us | 17.153 us | 0.54x |
| `lokad/style/cell-ref` | 0.904 us | 1.875 us | 2.060 us | 0.48x |
| `lokad/style/range-ref` | 0.811 us | 1.794 us | 1.955 us | 0.45x |
| `literal/call-token` | 1.821 us | 4.215 us | 17.233 us | 0.43x |
| `lokad/langserv/url-dashboard` | 9.802 us | 23.115 us | 23.399 us | 0.42x |
| `literal-family/type-token-family` | 9.114 us | 24.036 us | 37.765 us | 0.38x |
| `lokad/lexer/string` | 26.421 us | 79.868 us | 84.348 us | 0.33x |
| `structural/ordered-identifier-window` | 8.572 us | 28.286 us | 41.948 us | 0.30x |
| `literal-family/method-token-family` | 2.611 us | 9.015 us | 21.719 us | 0.29x |
| `structural/modifier-family-to-type-window` | 65.563 us | 247.940 us | 264.277 us | 0.26x |
| `lokad/langserv/url-download` | 11.506 us | 50.326 us | 50.054 us | 0.23x |
| `literal/identifier-token-casei` | 3.438 us | 15.061 us | 28.009 us | 0.23x |
| `lokad/folding/region-marker` | 5.100 us | 35.256 us | 60.301 us | 0.14x |
| `structural/keyword-family-to-capitalized-identifier` | 16.768 us | 213.432 us | 229.069 us | 0.08x |
| `structural/modifier-family-to-type-pair` | 2.853 us | 132.093 us | 145.855 us | 0.02x |
| `balancing/begin-end-blocks` | 47.689 us | 4,099.189 us | 4,063.805 us | 0.01x |
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
| `structural/keyword-family-to-capitalized-identifier` | 16.837 us | 7.691 us | 21.351 us | 2.19x |
| `lokad/style/hex-color` | 0.430 us | 0.228 us | 0.309 us | 1.89x |
| `lokad/lexer/operator-run` | 1.736 us | 0.919 us | 1.383 us | 1.89x |
| `lokad/imports/module-imports` | 7.789 us | 4.361 us | 28.262 us | 1.79x |
| `lokad/docs/inline-doc-prefix` | 2.483 us | 1.441 us | 2.179 us | 1.72x |
| `lokad/lexer/number` | 6.254 us | 3.786 us | 4.862 us | 1.65x |
| `fallback/lookahead` | 6.857 us | 4.172 us | 16.696 us | 1.64x |
| `lokad/langserv/color-rgb-hex` | 1.352 us | 0.842 us | 1.097 us | 1.60x |
| `structural/method-family-call` | 7.808 us | 4.926 us | 17.668 us | 1.59x |
| `lokad/langserv/url-dashboard` | 9.792 us | 6.186 us | 6.654 us | 1.58x |
| `structural/ordered-keyword-window` | 13.101 us | 8.457 us | 21.298 us | 1.55x |
| `lokad/lexer/identifier` | 11.772 us | 7.621 us | 10.552 us | 1.54x |
| `lokad/langserv/identifier-validator` | 6.527 us | 4.259 us | 5.632 us | 1.53x |
| `lokad/lexer/doc-line` | 1.621 us | 1.082 us | 1.780 us | 1.50x |
| `lokad/langserv/color-short-hex` | 0.709 us | 0.486 us | 0.674 us | 1.46x |
| `structural/method-family-generic-call` | 6.486 us | 4.553 us | 16.690 us | 1.42x |
| `structural/modifier-family-to-type-window` | 65.276 us | 55.456 us | 69.012 us | 1.18x |
| `structural/ordered-modifier-pair` | 9.937 us | 8.469 us | 21.555 us | 1.17x |
| `lokad/style/range-ref` | 0.810 us | 0.729 us | 0.862 us | 1.11x |
| `lokad/style/cell-ref` | 0.901 us | 0.822 us | 1.017 us | 1.10x |
| `lokad/langserv/helper-identifier` | 2.812 us | 2.687 us | 3.572 us | 1.05x |
| `lokad/langserv/url-download` | 11.579 us | 12.149 us | 12.525 us | 0.95x |
| `lokad/lexer/string` | 26.414 us | 30.062 us | 32.807 us | 0.88x |
| `structural/keyword-to-generic-type` | 2.962 us | 3.469 us | 16.498 us | 0.85x |
| `structural/ordered-identifier-window` | 8.542 us | 10.443 us | 24.178 us | 0.82x |
| `literal-family/type-token-family` | 9.140 us | 11.351 us | 24.277 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.142 us | 4.065 us | 16.961 us | 0.77x |
| `literal/identifier-token-casei` | 3.456 us | 5.847 us | 19.136 us | 0.59x |
| `literal-family/method-token-family` | 2.568 us | 5.027 us | 17.556 us | 0.51x |
| `literal/identifier-token` | 2.309 us | 4.627 us | 16.837 us | 0.50x |
| `literal/call-token` | 1.821 us | 3.846 us | 16.315 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.841 us | 10.352 us | 23.462 us | 0.27x |
| `lokad/folding/region-marker` | 5.055 us | 34.920 us | 60.499 us | 0.14x |
| `balancing/begin-end-blocks` | 48.314 us | 457.477 us | 488.759 us | 0.11x |
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
