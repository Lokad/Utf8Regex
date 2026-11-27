# Lokad.Utf8Regex

Fully managed regular expressions over UTF-8 spans for **.NET 10 and .NET 11**.
Match, count, enumerate and replace without decoding the subject to a string.

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
| [Lokad.Utf8Regex.Posix](src/Lokad.Utf8Regex.Posix/README.md) | POSIX ERE subset with leftmost-longest selection, hierarchical capture priority and opt-in GNU backreferences |

Adapters require the matching Core package version. Their specifications
define the supported grammar and operation boundaries.

## Subject input contract

Matching uses **Unicode scalars**. For example,
`new Utf8Regex(".").Count("A😀"u8)` returns 2. The emoji occupies four bytes.
Match and capture ranges use byte coordinates.

All profiles require valid UTF-8 by default. Use `System.Text.Unicode.Utf8.IsValid` where
external bytes become text. Regex operations trust that guarantee. Pattern,
replacement-template and assembled-output checks follow their own rules.
Keep borrowed input unchanged and its owner alive during use. Owned snapshots
remain valid after cursor advance or disposal.
[Input contract](docs/SPEC.md#subject-input-contract)

PCRE2 offers `Pcre2CompileOptions.MatchInvalidUtf8` for searching valid
fragments of a malformed subject with original byte offsets.
[PCRE2 input contract](docs/SPEC-PCRE2.md#subject-input-contract)

## Cancellation and limits

Instances accept cancellation tokens, timeouts and optional limits on depth,
working capacity and result capacity.

```csharp
var bounded = new Utf8Regex(
    @"(?<word>\w+)", RegexOptions.CultureInvariant,
    policy: new Utf8RegexPolicy
    {
        MatchTimeout = TimeSpan.FromSeconds(1),
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxDepth = 256,
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

The packages target `net10.0` and `net11.0`. CI tests both on Windows and Linux.
Benchmarks use .NET 11. [Pinned SDK](global.json)

## Benchmarks

The measured operations start with valid, unchanged UTF-8 and include the required
result materialization. The Core summary records the measured sources, runtimes,
qualification and snapshot identity. The benchmark guide covers ordinary patterns
and calls with finite limits, deadlines and cancellation.
[Core summary](bench/Snapshots/Core.json) · [Benchmark guide](docs/BENCHMARKS.md)

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.
The `common/match-word` and Lokad prefix-loop rows compare the first
whole-match range on both sides.

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
| `literal/sherlock-zh` | 13.559 us | 18.200 us | 2,135.500 us | 0.75x |
| `literal/sherlock-ru` | 68.617 us | 95.513 us | 5,152.633 us | 0.72x |
| `literal-alternate/sherlock-zh` | 41.581 us | 66.592 us | 2,189.026 us | 0.62x |
| `ruff-noqa/tweaked` | 3,856.295 us | 7,715.580 us | 40,644.100 us | 0.50x |
| `literal/sherlock-casei-ru` | 143.345 us | 347.556 us | 5,337.736 us | 0.41x |
| `bounded-repeat/letters-en` | 1,020.284 us | 2,730.070 us | 2,898.338 us | 0.37x |
| `literal/sherlock-en` | 30.422 us | 83.373 us | 462.623 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,954.369 us | 5,584.136 us | 6,456.408 us | 0.35x |
| `literal/sherlock-casei-en` | 65.882 us | 191.634 us | 637.334 us | 0.34x |
| `dictionary/single` | 7,971.830 us | 24,820.200 us | 24,967.125 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,366.615 us | 4,613.724 us | 9,553.450 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 158.386 us | 572.210 us | 1,022.448 us | 0.28x |
| `bounded-repeat/capitals` | 11,411.383 us | 44,867.950 us | 47,775.150 us | 0.25x |
| `literal-alternate/sherlock-en` | 353.226 us | 1,443.430 us | 2,006.060 us | 0.24x |
| `aws-keys/quick` | 1,883.253 us | 9,134.312 us | 45,505.300 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 123.163 us | 642.919 us | 1,080.751 us | 0.19x |
| `aws-keys/full` | 8,283.850 us | 82,297.000 us | 112,874.600 us | 0.10x |
| `bounded-repeat/context` | 8,354.130 us | 143,640.700 us | 148,016.000 us | 0.06x |
| `ruff-noqa/real` | 3,370.126 us | 66,976.900 us | 94,800.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 830.467 us | 22,070.475 us | 26,969.750 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 99.208 us | 10,705.129 us | 11,530.543 us | 0.0093x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.107 us | 0.141 us | 0.77x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.072 us | 0.086 us | 0.64x |
| `common/date-match` | `IsMatch` | 0.068 us | 0.108 us | 0.136 us | 0.63x |
| `industry/mariomka-uri-count` | `Count` | 1,125.467 us | 1,847.014 us | 10,096.300 us | 0.61x |
| `common/ip-match` | `IsMatch` | 0.100 us | 0.165 us | 0.184 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.047 us | 0.082 us | 0.097 us | 0.58x |
| `industry/mariomka-email-count` | `Count` | 267.684 us | 487.605 us | 8,628.600 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.134 us | 0.263 us | 0.294 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 615.982 us | 1,282.639 us | 8,397.878 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.162 us | 0.179 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.088 us | 0.192 us | 0.211 us | 0.46x |
| `common/match-word` | `Match` | 0.049 us | 0.124 us | 1.135 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.085 us | 0.225 us | 0.259 us | 0.38x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.109 us | 0.122 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.260 us | 0.692 us | 0.706 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,030.119 us | 8,179.190 us | 16,160.867 us | 0.37x |
| `common/matches-set` | `Count` | 58.627 us | 185.241 us | 187.284 us | 0.32x |
| `common/matches-word` | `Count` | 1.206 us | 4.088 us | 5.779 us | 0.30x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.251 us | 251.582 us | 499.543 us | 0.26x |
| `common/replace-words` | `Replace` | 19.314 us | 74.145 us | 74.818 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.104 us | 0.533 us | 0.565 us | 0.19x |
| `common/matches-boundary` | `Count` | 29.439 us | 157.694 us | 159.366 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.138 us | 928.035 us | 1,192.546 us | 0.17x |
| `common/split-words` | `Split` | 12.476 us | 75.459 us | 77.246 us | 0.17x |
| `common/matches-words` | `Count` | 8.853 us | 67.220 us | 68.492 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,006.264 us | 9,536.250 us | 16,565.675 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,776.348 us | 19,088.125 us | 19,644.275 us | 0.09x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.594 us | 0.623 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,424.455 us | 30,935.700 us | 47,294.250 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.331 us | 0.357 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 966.620 us | 23,201.125 us | 29,837.400 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.026 us | 0.793 us | 0.835 us | 0.03x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 227.582 us | 7,767.490 us | 8,137.089 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 290.406 us | 11,414.857 us | 11,996.657 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 41.425 us | 34.410 us | 2,152.389 us | 1.20x |
| `literal-alternate/sherlock-en` | 351.524 us | 325.719 us | 849.515 us | 1.08x |
| `bounded-repeat/letters-ru` | 1,956.413 us | 2,044.068 us | 2,875.500 us | 0.96x |
| `literal-alternate/sherlock-en-mixed` | 157.483 us | 170.754 us | 589.467 us | 0.92x |
| `literal/sherlock-casei-en` | 66.386 us | 75.255 us | 440.344 us | 0.88x |
| `ruff-noqa/tweaked` | 3,875.575 us | 4,455.850 us | 34,718.600 us | 0.87x |
| `bounded-repeat/capitals` | 11,223.000 us | 13,065.183 us | 15,749.275 us | 0.86x |
| `literal/sherlock-ru` | 68.408 us | 87.493 us | 5,107.640 us | 0.78x |
| `literal/sherlock-casei-ru` | 144.425 us | 188.142 us | 5,162.221 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 99.235 us | 147.274 us | 688.785 us | 0.67x |
| `literal-alternate/sherlock-en-nomatch` | 124.593 us | 191.978 us | 613.153 us | 0.65x |
| `literal-alternate/sherlock-ru` | 1,373.629 us | 2,282.994 us | 7,153.400 us | 0.60x |
| `literal/sherlock-zh` | 13.672 us | 23.018 us | 2,094.853 us | 0.59x |
| `bounded-repeat/letters-en` | 1,013.017 us | 1,767.847 us | 1,902.377 us | 0.57x |
| `aws-keys/quick` | 1,902.972 us | 3,504.491 us | 37,461.100 us | 0.54x |
| `aws-keys/full` | 8,376.122 us | 17,691.760 us | 48,488.933 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 829.458 us | 1,897.949 us | 6,923.175 us | 0.44x |
| `literal/sherlock-en` | 30.232 us | 73.219 us | 462.511 us | 0.41x |
| `dictionary/single` | 7,959.010 us | 44,229.550 us | 43,770.300 us | 0.18x |
| `ruff-noqa/real` | 3,333.564 us | 19,748.075 us | 50,556.000 us | 0.17x |
| `bounded-repeat/context` | 8,467.611 us | 66,641.450 us | 70,196.550 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.095 us | 0.046 us | 0.066 us | 2.07x |
| `common/date-match` | `IsMatch` | 0.067 us | 0.041 us | 0.070 us | 1.64x |
| `common/ip-miss` | `IsMatch` | 0.253 us | 0.172 us | 0.199 us | 1.47x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.046 us | 0.032 us | 0.048 us | 1.42x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.075 us | 0.054 us | 0.074 us | 1.39x |
| `common/uri-miss` | `IsMatch` | 0.087 us | 0.063 us | 0.085 us | 1.38x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.037 us | 0.030 us | 0.046 us | 1.23x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.042 us | 0.035 us | 0.050 us | 1.21x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.440 us | 56.334 us | 276.439 us | 1.18x |
| `common/replace-words` | `Replace` | 19.243 us | 16.489 us | 17.488 us | 1.17x |
| `common/match-word` | `Match` | 0.049 us | 0.046 us | 0.915 us | 1.06x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.049 us | 0.047 us | 0.082 us | 1.05x |
| `industry/mariomka-uri-count` | `Count` | 1,127.727 us | 1,094.716 us | 8,266.900 us | 1.03x |
| `common/uri-match` | `IsMatch` | 0.086 us | 0.087 us | 0.118 us | 0.98x |
| `industry/leipzig-river-window-count` | `Count` | 1,418.838 us | 1,513.670 us | 10,218.650 us | 0.94x |
| `industry/mariomka-ip-count` | `Count` | 3,001.800 us | 3,286.126 us | 10,478.233 us | 0.91x |
| `common/email-match` | `IsMatch` | 0.069 us | 0.078 us | 0.112 us | 0.90x |
| `common/matches-words` | `Count` | 8.800 us | 10.478 us | 13.630 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 981.051 us | 1,185.809 us | 7,178.060 us | 0.83x |
| `common/split-words` | `Split` | 12.608 us | 20.048 us | 22.034 us | 0.63x |
| `common/backtracking` | `IsMatch` | 0.026 us | 0.043 us | 0.071 us | 0.61x |
| `industry/mariomka-email-count` | `Count` | 267.019 us | 459.258 us | 7,423.300 us | 0.58x |
| `common/matches-set` | `Count` | 58.504 us | 101.935 us | 103.088 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.103 us | 0.188 us | 0.227 us | 0.55x |
| `common/matches-word` | `Count` | 1.204 us | 2.258 us | 3.307 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 617.140 us | 1,277.338 us | 7,281.590 us | 0.48x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.085 us | 0.124 us | 0.36x |
| `industry/leipzig-name-family-count` | `Count` | 999.444 us | 2,962.508 us | 8,430.800 us | 0.34x |
| `common/matches-boundary` | `Count` | 29.489 us | 100.294 us | 102.151 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.445 us | 570.566 us | 827.675 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,779.207 us | 8,585.322 us | 9,101.100 us | 0.21x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.085 us | 0.112 us | 0.17x |
| `industry/rust-sherlock-ing-count` | `Count` | 286.821 us | 4,624.012 us | 5,044.220 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 227.304 us | 3,792.780 us | 4,197.521 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.617 us | 6.106 us | 32.114 us | 0.92x |
| `fallback/lookahead` | 5.057 us | 5.807 us | 19.610 us | 0.87x |
| `structural/method-family-generic-call` | 4.350 us | 6.334 us | 20.950 us | 0.69x |
| `structural/method-family-call` | 5.173 us | 7.729 us | 21.605 us | 0.67x |
| `lokad/langserv/color-short-hex` | 0.515 us | 0.775 us | 0.963 us | 0.66x |
| `structural/ordered-keyword-window` | 11.020 us | 17.122 us | 31.223 us | 0.64x |
| `lokad/lexer/identifier` | 8.002 us | 13.052 us | 15.982 us | 0.61x |
| `structural/ordered-modifier-pair` | 10.042 us | 16.749 us | 30.778 us | 0.60x |
| `lokad/langserv/identifier-validator` | 4.251 us | 7.166 us | 8.791 us | 0.59x |
| `lokad/lexer/doc-line` | 1.046 us | 1.831 us | 2.558 us | 0.57x |
| `lokad/docs/inline-doc-prefix` | 1.868 us | 3.320 us | 4.097 us | 0.56x |
| `structural/keyword-to-generic-type` | 2.163 us | 3.925 us | 18.550 us | 0.55x |
| `literal/identifier-token` | 2.161 us | 4.166 us | 17.549 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.267 us | 4.481 us | 5.410 us | 0.51x |
| `lokad/lexer/operator-run` | 1.089 us | 2.217 us | 2.660 us | 0.49x |
| `structural/ordered-keyword-pair` | 2.438 us | 5.226 us | 19.787 us | 0.47x |
| `literal/call-token` | 1.773 us | 3.993 us | 17.765 us | 0.44x |
| `lokad/style/hex-color` | 0.215 us | 0.500 us | 0.575 us | 0.43x |
| `lokad/style/cell-ref` | 0.800 us | 1.865 us | 2.083 us | 0.43x |
| `lokad/style/range-ref` | 0.748 us | 1.744 us | 1.916 us | 0.43x |
| `literal-family/type-token-family` | 8.833 us | 22.847 us | 37.620 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.666 us | 1.740 us | 1.974 us | 0.38x |
| `lokad/lexer/string` | 30.125 us | 82.145 us | 85.903 us | 0.37x |
| `lokad/langserv/url-dashboard` | 7.979 us | 23.150 us | 23.649 us | 0.34x |
| `literal-family/method-token-family` | 2.559 us | 8.767 us | 22.754 us | 0.29x |
| `structural/ordered-identifier-window` | 7.904 us | 27.555 us | 41.424 us | 0.29x |
| `lokad/lexer/number` | 2.940 us | 10.500 us | 11.441 us | 0.28x |
| `literal/identifier-token-casei` | 3.774 us | 14.792 us | 28.676 us | 0.26x |
| `structural/modifier-family-to-type-window` | 55.998 us | 246.277 us | 260.502 us | 0.23x |
| `lokad/langserv/url-download` | 10.125 us | 49.500 us | 49.484 us | 0.20x |
| `lokad/folding/region-marker` | 5.286 us | 33.888 us | 59.937 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.647 us | 209.762 us | 225.161 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.841 us | 126.423 us | 141.727 us | 0.02x |
| `balancing/begin-end-blocks` | 44.282 us | 4,050.268 us | 4,086.768 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 8.265 us | 6.082 us | 6.598 us | 1.36x |
| `lokad/docs/inline-doc-prefix` | 1.873 us | 1.389 us | 2.091 us | 1.35x |
| `structural/ordered-keyword-window` | 11.015 us | 8.216 us | 21.969 us | 1.34x |
| `lokad/imports/module-imports` | 5.638 us | 4.269 us | 28.518 us | 1.32x |
| `structural/keyword-family-to-capitalized-identifier` | 9.621 us | 7.468 us | 22.295 us | 1.29x |
| `fallback/lookahead` | 5.039 us | 4.065 us | 17.597 us | 1.24x |
| `structural/ordered-modifier-pair` | 10.036 us | 8.241 us | 21.771 us | 1.22x |
| `lokad/lexer/operator-run` | 1.057 us | 0.923 us | 1.366 us | 1.15x |
| `lokad/style/range-ref` | 0.747 us | 0.700 us | 0.831 us | 1.07x |
| `structural/method-family-call` | 5.154 us | 4.842 us | 18.271 us | 1.06x |
| `lokad/lexer/string` | 30.618 us | 29.401 us | 32.360 us | 1.04x |
| `lokad/lexer/identifier` | 8.029 us | 7.750 us | 10.601 us | 1.04x |
| `lokad/langserv/identifier-validator` | 4.252 us | 4.132 us | 5.667 us | 1.03x |
| `structural/modifier-family-to-type-window` | 55.546 us | 54.491 us | 70.288 us | 1.02x |
| `lokad/lexer/doc-line` | 1.039 us | 1.020 us | 1.811 us | 1.02x |
| `lokad/langserv/color-short-hex` | 0.464 us | 0.467 us | 0.661 us | 0.99x |
| `lokad/style/cell-ref` | 0.798 us | 0.807 us | 1.002 us | 0.99x |
| `structural/method-family-generic-call` | 4.369 us | 4.451 us | 18.018 us | 0.98x |
| `lokad/style/hex-color` | 0.196 us | 0.223 us | 0.303 us | 0.88x |
| `lokad/langserv/helper-identifier` | 2.238 us | 2.562 us | 3.520 us | 0.87x |
| `lokad/langserv/url-download` | 10.167 us | 11.930 us | 12.499 us | 0.85x |
| `literal-family/type-token-family` | 8.919 us | 11.031 us | 24.938 us | 0.81x |
| `lokad/lexer/number` | 2.954 us | 3.700 us | 4.689 us | 0.80x |
| `structural/ordered-identifier-window` | 7.948 us | 10.340 us | 24.588 us | 0.77x |
| `literal/identifier-token-casei` | 3.766 us | 5.180 us | 19.667 us | 0.73x |
| `lokad/langserv/color-rgb-hex` | 0.582 us | 0.811 us | 1.065 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.196 us | 3.361 us | 17.432 us | 0.65x |
| `structural/ordered-keyword-pair` | 2.428 us | 3.957 us | 17.806 us | 0.61x |
| `literal-family/method-token-family` | 2.551 us | 5.050 us | 18.753 us | 0.51x |
| `literal/identifier-token` | 2.182 us | 4.480 us | 17.331 us | 0.49x |
| `literal/call-token` | 1.765 us | 3.785 us | 16.940 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.860 us | 10.353 us | 24.707 us | 0.28x |
| `lokad/folding/region-marker` | 5.234 us | 34.123 us | 60.638 us | 0.15x |
| `balancing/begin-end-blocks` | 44.498 us | 445.541 us | 477.739 us | 0.10x |
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
