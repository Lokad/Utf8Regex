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
| `literal/sherlock-zh` | 21.073 us | 17.835 us | 2,089.126 us | 1.18x |
| `literal/sherlock-ru` | 65.671 us | 91.139 us | 4,956.893 us | 0.72x |
| `literal-alternate/sherlock-zh` | 41.122 us | 65.051 us | 2,113.111 us | 0.63x |
| `aws-keys/quick` | 5,057.833 us | 9,008.456 us | 43,600.950 us | 0.56x |
| `ruff-noqa/tweaked` | 3,711.776 us | 7,153.850 us | 39,674.700 us | 0.52x |
| `aws-keys/full` | 42,120.750 us | 81,591.400 us | 113,634.200 us | 0.52x |
| `literal/sherlock-casei-ru` | 144.538 us | 342.318 us | 5,271.571 us | 0.42x |
| `literal/sherlock-casei-en` | 75.792 us | 181.337 us | 584.302 us | 0.42x |
| `bounded-repeat/letters-en` | 1,017.581 us | 2,652.400 us | 2,809.300 us | 0.38x |
| `literal/sherlock-en` | 29.751 us | 84.440 us | 442.882 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,853.117 us | 5,539.000 us | 6,342.492 us | 0.33x |
| `dictionary/single` | 7,746.564 us | 24,575.250 us | 24,672.950 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,375.682 us | 4,603.371 us | 9,420.725 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 148.487 us | 582.413 us | 975.999 us | 0.25x |
| `bounded-repeat/capitals` | 11,103.371 us | 43,918.100 us | 46,632.750 us | 0.25x |
| `literal-alternate/sherlock-en` | 333.892 us | 1,399.231 us | 1,901.824 us | 0.24x |
| `literal-alternate/sherlock-en-nomatch` | 120.767 us | 629.886 us | 1,034.025 us | 0.19x |
| `bounded-repeat/context` | 8,310.356 us | 138,868.700 us | 143,610.000 us | 0.06x |
| `ruff-noqa/real` | 3,296.965 us | 66,601.400 us | 98,963.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 793.236 us | 22,418.100 us | 27,212.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 102.570 us | 10,625.350 us | 11,401.014 us | 0.0097x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.162 us | 0.103 us | 0.132 us | 1.57x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.107 us | 0.074 us | 0.085 us | 1.46x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.110 us | 0.079 us | 0.095 us | 1.39x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.106 us | 0.078 us | 0.093 us | 1.36x |
| `common/uri-match` | `IsMatch` | 0.286 us | 0.221 us | 0.256 us | 1.29x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.122 us | 0.103 us | 0.139 us | 1.18x |
| `common/ip-match` | `IsMatch` | 0.188 us | 0.164 us | 0.183 us | 1.15x |
| `common/uri-miss` | `IsMatch` | 0.199 us | 0.181 us | 0.198 us | 1.10x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.158 us | 0.160 us | 0.177 us | 0.99x |
| `industry/mariomka-email-count` | `Count` | 403.242 us | 467.033 us | 8,151.630 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,434.437 us | 1,782.884 us | 9,736.600 us | 0.80x |
| `common/email-match` | `IsMatch` | 0.188 us | 0.255 us | 0.281 us | 0.73x |
| `common/ip-miss` | `IsMatch` | 0.494 us | 0.697 us | 0.717 us | 0.71x |
| `common/date-miss` | `IsMatch` | 0.338 us | 0.532 us | 0.566 us | 0.64x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 149.472 us | 247.904 us | 464.817 us | 0.60x |
| `industry/leipzig-twain-count` | `Count` | 601.359 us | 1,252.302 us | 8,349.311 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.123 us | 1.098 us | 0.43x |
| `common/matches-set` | `Count` | 57.787 us | 185.170 us | 185.816 us | 0.31x |
| `common/matches-word` | `Count` | 1.171 us | 3.984 us | 5.592 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,092.519 us | 7,803.590 us | 15,904.833 us | 0.27x |
| `common/replace-words` | `Replace` | 19.506 us | 72.890 us | 74.171 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.998 us | 921.638 us | 1,168.148 us | 0.17x |
| `common/split-words` | `Split` | 12.540 us | 74.140 us | 75.330 us | 0.17x |
| `common/matches-boundary` | `Count` | 21.324 us | 154.514 us | 155.859 us | 0.14x |
| `common/matches-words` | `Count` | 8.653 us | 67.228 us | 67.463 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,000.075 us | 9,294.289 us | 15,683.525 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,757.105 us | 18,419.900 us | 18,945.550 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.338 us | 0.362 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 851.767 us | 11,222.300 us | 11,777.800 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.582 us | 0.617 us | 0.07x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.847 us | 0.886 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,715.207 us | 30,978.733 us | 37,563.650 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 967.777 us | 21,463.350 us | 28,195.850 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 280.047 us | 7,875.060 us | 8,373.600 us | 0.04x |
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
| `aws-keys/full` | 42,174.650 us | 17,427.680 us | 51,807.000 us | 2.42x |
| `aws-keys/quick` | 5,081.180 us | 3,458.745 us | 36,124.800 us | 1.47x |
| `literal-alternate/sherlock-zh` | 41.108 us | 34.007 us | 2,072.558 us | 1.21x |
| `literal-alternate/sherlock-en` | 333.285 us | 326.571 us | 724.654 us | 1.02x |
| `literal/sherlock-casei-en` | 75.724 us | 75.637 us | 427.886 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,854.417 us | 1,944.605 us | 2,733.350 us | 0.95x |
| `literal/sherlock-zh` | 21.083 us | 22.789 us | 2,101.743 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 148.015 us | 170.340 us | 527.397 us | 0.87x |
| `ruff-noqa/tweaked` | 3,739.152 us | 4,305.361 us | 32,931.950 us | 0.87x |
| `bounded-repeat/capitals` | 11,104.400 us | 12,857.083 us | 15,699.125 us | 0.86x |
| `literal-alternate/sherlock-casei-en` | 104.956 us | 128.315 us | 549.397 us | 0.82x |
| `literal/sherlock-casei-ru` | 146.873 us | 184.848 us | 5,023.660 us | 0.79x |
| `literal/sherlock-ru` | 65.811 us | 83.902 us | 4,941.927 us | 0.78x |
| `literal-alternate/sherlock-en-nomatch` | 120.670 us | 190.906 us | 548.021 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,368.495 us | 2,214.021 us | 7,033.483 us | 0.62x |
| `bounded-repeat/letters-en` | 1,020.334 us | 1,719.318 us | 1,848.576 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 804.323 us | 1,849.085 us | 6,709.300 us | 0.43x |
| `literal/sherlock-en` | 29.969 us | 73.066 us | 421.376 us | 0.41x |
| `dictionary/single` | 7,740.940 us | 43,686.950 us | 43,656.750 us | 0.18x |
| `ruff-noqa/real` | 3,383.748 us | 19,758.250 us | 49,971.400 us | 0.17x |
| `bounded-repeat/context` | 8,332.889 us | 65,471.100 us | 68,693.050 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.189 us | 0.046 us | 0.065 us | 4.15x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.109 us | 0.029 us | 0.049 us | 3.72x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.107 us | 0.029 us | 0.046 us | 3.64x |
| `common/date-match` | `IsMatch` | 0.161 us | 0.044 us | 0.071 us | 3.62x |
| `common/uri-match` | `IsMatch` | 0.287 us | 0.083 us | 0.115 us | 3.47x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.107 us | 0.034 us | 0.051 us | 3.19x |
| `common/uri-miss` | `IsMatch` | 0.199 us | 0.063 us | 0.081 us | 3.13x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.160 us | 0.053 us | 0.073 us | 3.02x |
| `common/ip-miss` | `IsMatch` | 0.493 us | 0.171 us | 0.191 us | 2.88x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 148.882 us | 55.915 us | 264.222 us | 2.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.121 us | 0.047 us | 0.083 us | 2.61x |
| `common/email-match` | `IsMatch` | 0.187 us | 0.076 us | 0.101 us | 2.44x |
| `common/date-miss` | `IsMatch` | 0.324 us | 0.191 us | 0.223 us | 1.70x |
| `industry/mariomka-uri-count` | `Count` | 1,438.262 us | 1,067.585 us | 7,968.300 us | 1.35x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.042 us | 0.071 us | 1.31x |
| `industry/leipzig-river-window-count` | `Count` | 1,737.916 us | 1,429.766 us | 7,287.675 us | 1.22x |
| `common/match-word` | `Match` | 0.053 us | 0.045 us | 0.884 us | 1.16x |
| `common/replace-words` | `Replace` | 19.414 us | 16.726 us | 17.386 us | 1.16x |
| `industry/mariomka-email-count` | `Count` | 402.694 us | 434.669 us | 7,410.960 us | 0.93x |
| `common/matches-words` | `Count` | 8.653 us | 10.165 us | 13.278 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 971.200 us | 1,191.848 us | 6,714.400 us | 0.81x |
| `industry/mariomka-ip-count` | `Count` | 2,095.162 us | 3,304.352 us | 10,341.667 us | 0.63x |
| `common/split-words` | `Split` | 12.534 us | 20.184 us | 21.873 us | 0.62x |
| `common/matches-set` | `Count` | 57.853 us | 99.696 us | 101.078 us | 0.58x |
| `common/matches-word` | `Count` | 1.171 us | 2.231 us | 3.240 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 602.814 us | 1,234.582 us | 7,265.127 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.093 us | 0.122 us | 0.41x |
| `industry/leipzig-name-family-count` | `Count` | 1,011.655 us | 2,875.374 us | 8,080.367 us | 0.35x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.084 us | 0.113 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.492 us | 558.843 us | 797.949 us | 0.29x |
| `common/matches-boundary` | `Count` | 21.783 us | 99.088 us | 100.533 us | 0.22x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,744.548 us | 8,327.800 us | 8,741.867 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 857.206 us | 4,519.447 us | 4,904.031 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 280.863 us | 3,736.815 us | 4,107.363 us | 0.08x |
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
| `lokad/langserv/helper-identifier` | 9.622 us | 4.546 us | 5.394 us | 2.12x |
| `lokad/lexer/doc-line` | 3.547 us | 1.791 us | 2.485 us | 1.98x |
| `lokad/langserv/identifier-validator` | 13.713 us | 6.984 us | 8.551 us | 1.96x |
| `lokad/lexer/identifier` | 24.193 us | 13.223 us | 15.695 us | 1.83x |
| `lokad/style/hex-color` | 0.841 us | 0.474 us | 0.573 us | 1.77x |
| `fallback/lookahead` | 10.091 us | 5.776 us | 17.664 us | 1.75x |
| `structural/method-family-generic-call` | 10.769 us | 6.315 us | 19.113 us | 1.71x |
| `lokad/docs/inline-doc-prefix` | 5.234 us | 3.307 us | 4.072 us | 1.58x |
| `lokad/langserv/color-rgb-hex` | 2.706 us | 1.778 us | 1.966 us | 1.52x |
| `lokad/langserv/color-short-hex` | 1.114 us | 0.778 us | 0.951 us | 1.43x |
| `lokad/lexer/number` | 13.893 us | 10.216 us | 11.191 us | 1.36x |
| `lokad/lexer/operator-run` | 2.939 us | 2.209 us | 2.683 us | 1.33x |
| `lokad/imports/module-imports` | 7.954 us | 5.986 us | 29.176 us | 1.33x |
| `structural/method-family-call` | 9.555 us | 7.611 us | 20.808 us | 1.26x |
| `structural/ordered-keyword-window` | 20.479 us | 16.621 us | 29.332 us | 1.23x |
| `lokad/lexer/string` | 91.791 us | 77.322 us | 81.298 us | 1.19x |
| `lokad/style/range-ref` | 1.868 us | 1.760 us | 1.880 us | 1.06x |
| `lokad/style/cell-ref` | 1.875 us | 1.809 us | 2.042 us | 1.04x |
| `structural/keyword-to-generic-type` | 2.883 us | 3.855 us | 17.031 us | 0.75x |
| `structural/ordered-keyword-pair` | 3.384 us | 5.039 us | 18.021 us | 0.67x |
| `structural/ordered-modifier-pair` | 9.855 us | 16.246 us | 28.712 us | 0.61x |
| `structural/ordered-identifier-window` | 14.346 us | 26.976 us | 39.610 us | 0.53x |
| `literal/identifier-token` | 2.169 us | 4.107 us | 16.628 us | 0.53x |
| `lokad/langserv/url-dashboard` | 9.905 us | 22.117 us | 22.549 us | 0.45x |
| `literal/call-token` | 1.745 us | 4.059 us | 16.484 us | 0.43x |
| `structural/modifier-family-to-type-window` | 103.372 us | 243.630 us | 256.872 us | 0.42x |
| `literal-family/method-token-family` | 3.611 us | 8.608 us | 20.985 us | 0.42x |
| `literal-family/type-token-family` | 9.299 us | 23.067 us | 36.458 us | 0.40x |
| `balancing/begin-end-blocks` | 1,272.205 us | 4,107.853 us | 4,127.516 us | 0.31x |
| `lokad/langserv/url-download` | 11.802 us | 48.728 us | 49.672 us | 0.24x |
| `literal/identifier-token-casei` | 3.278 us | 14.488 us | 27.069 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 22.265 us | 128.902 us | 141.811 us | 0.17x |
| `lokad/folding/region-marker` | 4.789 us | 33.897 us | 59.268 us | 0.14x |
| `structural/keyword-family-to-capitalized-identifier` | 13.414 us | 208.898 us | 222.980 us | 0.06x |
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
| `lokad/style/hex-color` | 0.839 us | 0.214 us | 0.298 us | 3.91x |
| `lokad/lexer/number` | 13.908 us | 3.594 us | 4.620 us | 3.87x |
| `lokad/docs/inline-doc-prefix` | 5.164 us | 1.373 us | 2.078 us | 3.76x |
| `lokad/langserv/helper-identifier` | 9.524 us | 2.557 us | 3.436 us | 3.72x |
| `lokad/lexer/doc-line` | 3.544 us | 1.007 us | 1.701 us | 3.52x |
| `lokad/langserv/identifier-validator` | 13.494 us | 4.030 us | 5.468 us | 3.35x |
| `lokad/langserv/color-rgb-hex` | 2.701 us | 0.811 us | 1.049 us | 3.33x |
| `lokad/lexer/operator-run` | 2.921 us | 0.881 us | 1.322 us | 3.32x |
| `lokad/lexer/string` | 91.545 us | 28.715 us | 31.560 us | 3.19x |
| `lokad/lexer/identifier` | 24.127 us | 7.640 us | 10.201 us | 3.16x |
| `balancing/begin-end-blocks` | 1,282.107 us | 441.728 us | 467.838 us | 2.90x |
| `lokad/style/range-ref` | 1.873 us | 0.693 us | 0.821 us | 2.70x |
| `fallback/lookahead` | 10.152 us | 3.967 us | 15.523 us | 2.56x |
| `structural/ordered-keyword-window` | 20.475 us | 8.207 us | 20.498 us | 2.49x |
| `structural/method-family-generic-call` | 10.752 us | 4.333 us | 16.461 us | 2.48x |
| `lokad/langserv/color-short-hex` | 1.115 us | 0.454 us | 0.636 us | 2.46x |
| `lokad/style/cell-ref` | 1.872 us | 0.792 us | 1.006 us | 2.36x |
| `structural/modifier-family-to-type-pair` | 22.450 us | 10.218 us | 22.571 us | 2.20x |
| `structural/method-family-call` | 9.548 us | 4.782 us | 17.446 us | 2.00x |
| `structural/modifier-family-to-type-window` | 102.956 us | 54.024 us | 66.765 us | 1.91x |
| `lokad/imports/module-imports` | 7.752 us | 4.182 us | 26.921 us | 1.85x |
| `structural/keyword-family-to-capitalized-identifier` | 13.387 us | 7.323 us | 20.489 us | 1.83x |
| `lokad/langserv/url-dashboard` | 9.934 us | 6.008 us | 6.454 us | 1.65x |
| `structural/ordered-identifier-window` | 14.435 us | 10.013 us | 22.616 us | 1.44x |
| `structural/ordered-modifier-pair` | 9.852 us | 8.164 us | 20.501 us | 1.21x |
| `lokad/langserv/url-download` | 11.888 us | 11.596 us | 12.059 us | 1.03x |
| `structural/keyword-to-generic-type` | 2.884 us | 3.254 us | 15.969 us | 0.89x |
| `structural/ordered-keyword-pair` | 3.386 us | 3.903 us | 16.150 us | 0.87x |
| `literal-family/type-token-family` | 9.302 us | 10.845 us | 23.348 us | 0.86x |
| `literal-family/method-token-family` | 3.612 us | 5.020 us | 17.139 us | 0.72x |
| `literal/identifier-token-casei` | 3.269 us | 5.574 us | 18.230 us | 0.59x |
| `literal/identifier-token` | 2.135 us | 4.458 us | 16.318 us | 0.48x |
| `literal/call-token` | 1.749 us | 3.732 us | 15.638 us | 0.47x |
| `lokad/folding/region-marker` | 4.842 us | 34.179 us | 58.864 us | 0.14x |
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
