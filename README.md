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
| `literal/sherlock-zh` | 14.029 us | 18.879 us | 2,185.812 us | 0.74x |
| `literal/sherlock-ru` | 67.728 us | 97.201 us | 5,283.171 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.657 us | 68.654 us | 2,210.273 us | 0.64x |
| `ruff-noqa/tweaked` | 3,965.568 us | 7,798.473 us | 41,527.300 us | 0.51x |
| `literal/sherlock-casei-en` | 79.259 us | 187.378 us | 665.245 us | 0.42x |
| `literal/sherlock-casei-ru` | 152.110 us | 361.991 us | 5,610.385 us | 0.42x |
| `bounded-repeat/letters-en` | 1,142.763 us | 2,823.293 us | 3,006.948 us | 0.40x |
| `literal/sherlock-en` | 31.550 us | 85.332 us | 491.357 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,059.451 us | 5,859.377 us | 6,671.025 us | 0.35x |
| `dictionary/single` | 8,064.080 us | 25,064.933 us | 25,325.133 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,428.768 us | 4,778.931 us | 10,042.125 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 163.998 us | 591.060 us | 1,081.207 us | 0.28x |
| `bounded-repeat/capitals` | 11,843.757 us | 46,010.250 us | 49,339.900 us | 0.26x |
| `literal-alternate/sherlock-en` | 347.705 us | 1,480.471 us | 1,992.505 us | 0.23x |
| `aws-keys/quick` | 1,955.913 us | 9,508.867 us | 47,257.250 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 126.361 us | 660.878 us | 1,102.076 us | 0.19x |
| `aws-keys/full` | 8,393.900 us | 85,500.100 us | 118,019.500 us | 0.10x |
| `bounded-repeat/context` | 9,025.578 us | 146,754.500 us | 151,750.500 us | 0.06x |
| `ruff-noqa/real` | 3,468.882 us | 69,929.500 us | 103,463.200 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 870.601 us | 23,031.950 us | 28,569.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 104.805 us | 11,639.871 us | 12,527.371 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.059 us | 0.077 us | 0.091 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.085 us | 0.111 us | 0.147 us | 0.76x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.108 us | 0.137 us | 0.66x |
| `common/ip-match` | `IsMatch` | 0.114 us | 0.173 us | 0.193 us | 0.66x |
| `industry/mariomka-uri-count` | `Count` | 1,226.611 us | 1,889.362 us | 10,285.800 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.084 us | 0.101 us | 0.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.080 us | 0.096 us | 0.56x |
| `industry/mariomka-email-count` | `Count` | 277.914 us | 505.048 us | 8,889.700 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.139 us | 0.264 us | 0.287 us | 0.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.084 us | 0.168 us | 0.184 us | 0.50x |
| `industry/leipzig-twain-count` | `Count` | 625.939 us | 1,321.622 us | 8,423.860 us | 0.47x |
| `common/uri-miss` | `IsMatch` | 0.078 us | 0.199 us | 0.221 us | 0.39x |
| `common/match-word` | `Match` | 0.051 us | 0.130 us | 1.162 us | 0.39x |
| `common/ip-miss` | `IsMatch` | 0.277 us | 0.718 us | 0.734 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,122.412 us | 8,520.311 us | 17,076.233 us | 0.37x |
| `common/uri-match` | `IsMatch` | 0.078 us | 0.234 us | 0.272 us | 0.33x |
| `common/matches-set` | `Count` | 62.603 us | 195.668 us | 196.151 us | 0.32x |
| `common/matches-word` | `Count` | 1.253 us | 4.229 us | 6.039 us | 0.30x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 73.881 us | 263.872 us | 512.227 us | 0.28x |
| `common/replace-words` | `Replace` | 20.559 us | 75.987 us | 77.315 us | 0.27x |
| `common/date-miss` | `IsMatch` | 0.113 us | 0.550 us | 0.588 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.773 us | 960.530 us | 1,262.325 us | 0.17x |
| `common/matches-boundary` | `Count` | 28.084 us | 165.564 us | 167.439 us | 0.17x |
| `common/split-words` | `Split` | 12.955 us | 78.424 us | 78.931 us | 0.17x |
| `common/matches-words` | `Count` | 9.209 us | 71.043 us | 73.012 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 992.075 us | 9,801.400 us | 16,978.867 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,921.422 us | 19,297.950 us | 20,032.975 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.033 us | 0.610 us | 0.641 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.343 us | 0.368 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,477.376 us | 32,304.200 us | 39,521.250 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,029.080 us | 22,605.675 us | 29,899.900 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.029 us | 0.821 us | 0.854 us | 0.03x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 243.105 us | 8,140.480 us | 8,658.089 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 287.389 us | 11,727.314 us | 12,313.650 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.563 us | 35.748 us | 2,165.071 us | 1.22x |
| `literal-alternate/sherlock-en` | 348.453 us | 340.664 us | 813.597 us | 1.02x |
| `literal/sherlock-casei-en` | 79.321 us | 77.552 us | 456.233 us | 1.02x |
| `bounded-repeat/letters-ru` | 2,073.703 us | 2,046.505 us | 2,902.254 us | 1.01x |
| `literal-alternate/sherlock-en-mixed` | 163.419 us | 174.855 us | 647.621 us | 0.93x |
| `bounded-repeat/capitals` | 11,759.171 us | 13,603.967 us | 16,459.875 us | 0.86x |
| `ruff-noqa/tweaked` | 3,925.795 us | 4,551.294 us | 35,904.300 us | 0.86x |
| `literal/sherlock-casei-ru` | 154.658 us | 195.450 us | 5,368.693 us | 0.79x |
| `literal/sherlock-ru` | 67.894 us | 89.078 us | 5,171.860 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 106.018 us | 149.389 us | 629.692 us | 0.71x |
| `literal-alternate/sherlock-en-nomatch` | 126.924 us | 200.266 us | 639.024 us | 0.63x |
| `bounded-repeat/letters-en` | 1,138.367 us | 1,838.602 us | 1,988.366 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,425.902 us | 2,347.779 us | 7,483.225 us | 0.61x |
| `literal/sherlock-zh` | 13.998 us | 23.934 us | 2,199.525 us | 0.58x |
| `aws-keys/quick` | 1,965.003 us | 3,624.333 us | 39,439.300 us | 0.54x |
| `aws-keys/full` | 8,486.389 us | 18,262.120 us | 50,375.633 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 865.691 us | 1,955.790 us | 7,098.025 us | 0.44x |
| `literal/sherlock-en` | 31.361 us | 74.956 us | 406.625 us | 0.42x |
| `dictionary/single` | 8,070.660 us | 45,226.550 us | 45,260.850 us | 0.18x |
| `ruff-noqa/real` | 3,468.118 us | 20,584.500 us | 56,700.333 us | 0.17x |
| `bounded-repeat/context` | 8,988.200 us | 68,637.200 us | 72,356.400 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.110 us | 0.048 us | 0.068 us | 2.31x |
| `common/date-match` | `IsMatch` | 0.072 us | 0.043 us | 0.073 us | 1.67x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.055 us | 0.034 us | 0.051 us | 1.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.031 us | 0.049 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.275 us | 0.177 us | 0.202 us | 1.55x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.056 us | 0.076 us | 1.42x |
| `industry/mariomka-uri-count` | `Count` | 1,537.797 us | 1,103.213 us | 8,440.850 us | 1.39x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.031 us | 0.047 us | 1.30x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 73.978 us | 59.419 us | 296.595 us | 1.25x |
| `common/uri-miss` | `IsMatch` | 0.081 us | 0.067 us | 0.086 us | 1.21x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.056 us | 0.049 us | 0.087 us | 1.15x |
| `common/replace-words` | `Replace` | 20.579 us | 18.051 us | 20.867 us | 1.14x |
| `common/match-word` | `Match` | 0.051 us | 0.047 us | 0.935 us | 1.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,492.437 us | 1,559.316 us | 7,952.450 us | 0.96x |
| `common/email-match` | `IsMatch` | 0.075 us | 0.081 us | 0.107 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,138.464 us | 3,423.432 us | 10,872.533 us | 0.92x |
| `common/uri-match` | `IsMatch` | 0.078 us | 0.089 us | 0.124 us | 0.88x |
| `common/matches-words` | `Count` | 9.131 us | 10.822 us | 14.758 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,031.262 us | 1,246.892 us | 7,324.740 us | 0.83x |
| `common/split-words` | `Split` | 13.027 us | 19.927 us | 21.861 us | 0.65x |
| `common/backtracking` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.65x |
| `common/matches-set` | `Count` | 62.630 us | 105.192 us | 106.796 us | 0.60x |
| `industry/mariomka-email-count` | `Count` | 277.986 us | 470.264 us | 7,687.000 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.198 us | 0.237 us | 0.57x |
| `common/matches-word` | `Count` | 1.234 us | 2.340 us | 3.669 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 629.811 us | 1,298.807 us | 7,115.425 us | 0.48x |
| `common/one-node-backtracking` | `IsMatch` | 0.032 us | 0.089 us | 0.126 us | 0.36x |
| `industry/leipzig-name-family-count` | `Count` | 1,001.333 us | 2,994.177 us | 7,993.725 us | 0.33x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.389 us | 593.963 us | 850.480 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.024 us | 102.531 us | 105.278 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,913.715 us | 8,788.233 us | 9,515.350 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.088 us | 0.117 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 242.774 us | 3,960.811 us | 4,357.100 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 288.305 us | 4,742.894 us | 5,125.329 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.037 us | 6.320 us | 32.257 us | 0.96x |
| `fallback/lookahead` | 5.362 us | 5.966 us | 20.619 us | 0.90x |
| `lokad/langserv/color-short-hex` | 0.586 us | 0.818 us | 1.010 us | 0.72x |
| `structural/method-family-generic-call` | 4.438 us | 6.481 us | 21.421 us | 0.68x |
| `structural/ordered-keyword-window` | 11.581 us | 17.221 us | 31.790 us | 0.67x |
| `structural/method-family-call` | 5.282 us | 8.028 us | 22.230 us | 0.66x |
| `lokad/lexer/identifier` | 8.830 us | 13.428 us | 16.572 us | 0.66x |
| `lokad/langserv/identifier-validator` | 4.748 us | 7.422 us | 9.036 us | 0.64x |
| `structural/ordered-modifier-pair` | 10.553 us | 17.138 us | 31.438 us | 0.62x |
| `lokad/lexer/doc-line` | 1.133 us | 1.881 us | 2.642 us | 0.60x |
| `lokad/docs/inline-doc-prefix` | 1.997 us | 3.480 us | 4.239 us | 0.57x |
| `structural/keyword-to-generic-type` | 2.266 us | 4.022 us | 18.715 us | 0.56x |
| `lokad/langserv/helper-identifier` | 2.527 us | 4.690 us | 5.540 us | 0.54x |
| `literal/identifier-token` | 2.276 us | 4.354 us | 18.282 us | 0.52x |
| `lokad/lexer/operator-run` | 1.173 us | 2.318 us | 2.763 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.513 us | 5.317 us | 20.114 us | 0.47x |
| `lokad/style/cell-ref` | 0.864 us | 1.885 us | 2.122 us | 0.46x |
| `lokad/style/hex-color` | 0.236 us | 0.517 us | 0.603 us | 0.46x |
| `literal/call-token` | 1.856 us | 4.323 us | 18.739 us | 0.43x |
| `lokad/style/range-ref` | 0.779 us | 1.823 us | 1.967 us | 0.43x |
| `lokad/lexer/string` | 32.816 us | 80.833 us | 86.257 us | 0.41x |
| `lokad/langserv/color-rgb-hex` | 0.705 us | 1.790 us | 2.035 us | 0.39x |
| `literal-family/type-token-family` | 9.071 us | 23.353 us | 38.977 us | 0.39x |
| `lokad/langserv/url-dashboard` | 9.037 us | 23.396 us | 23.718 us | 0.39x |
| `lokad/lexer/number` | 3.246 us | 10.613 us | 11.735 us | 0.31x |
| `structural/ordered-identifier-window` | 8.223 us | 28.512 us | 43.067 us | 0.29x |
| `literal-family/method-token-family` | 2.624 us | 9.147 us | 23.346 us | 0.29x |
| `literal/identifier-token-casei` | 3.923 us | 14.786 us | 30.482 us | 0.27x |
| `structural/modifier-family-to-type-window` | 58.667 us | 252.399 us | 269.052 us | 0.23x |
| `lokad/langserv/url-download` | 11.057 us | 52.427 us | 54.637 us | 0.21x |
| `lokad/folding/region-marker` | 5.611 us | 35.980 us | 63.384 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.074 us | 215.376 us | 230.669 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.928 us | 130.983 us | 147.080 us | 0.02x |
| `balancing/begin-end-blocks` | 47.705 us | 4,357.661 us | 4,358.444 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.119 us | 6.254 us | 6.807 us | 1.46x |
| `lokad/docs/inline-doc-prefix` | 2.009 us | 1.435 us | 2.191 us | 1.40x |
| `lokad/imports/module-imports` | 6.048 us | 4.370 us | 29.535 us | 1.38x |
| `structural/ordered-keyword-window` | 11.566 us | 8.467 us | 22.541 us | 1.37x |
| `fallback/lookahead` | 5.330 us | 4.184 us | 18.325 us | 1.27x |
| `structural/keyword-family-to-capitalized-identifier` | 10.057 us | 7.915 us | 22.287 us | 1.27x |
| `lokad/lexer/operator-run` | 1.152 us | 0.924 us | 1.426 us | 1.25x |
| `structural/ordered-modifier-pair` | 10.520 us | 8.614 us | 22.645 us | 1.22x |
| `lokad/langserv/identifier-validator` | 4.755 us | 4.231 us | 5.783 us | 1.12x |
| `lokad/lexer/identifier` | 8.774 us | 7.844 us | 10.680 us | 1.12x |
| `lokad/lexer/string` | 32.744 us | 30.163 us | 32.810 us | 1.09x |
| `lokad/style/range-ref` | 0.783 us | 0.730 us | 0.866 us | 1.07x |
| `structural/modifier-family-to-type-window` | 59.389 us | 55.725 us | 71.369 us | 1.07x |
| `lokad/style/cell-ref` | 0.866 us | 0.813 us | 1.057 us | 1.07x |
| `lokad/lexer/doc-line` | 1.130 us | 1.062 us | 1.846 us | 1.06x |
| `structural/method-family-call` | 5.282 us | 5.011 us | 18.709 us | 1.05x |
| `lokad/langserv/color-short-hex` | 0.508 us | 0.484 us | 0.686 us | 1.05x |
| `structural/method-family-generic-call` | 4.430 us | 4.574 us | 18.426 us | 0.97x |
| `lokad/langserv/helper-identifier` | 2.493 us | 2.625 us | 3.651 us | 0.95x |
| `lokad/style/hex-color` | 0.219 us | 0.231 us | 0.313 us | 0.94x |
| `lokad/langserv/url-download` | 11.086 us | 11.951 us | 12.552 us | 0.93x |
| `lokad/lexer/number` | 3.128 us | 3.798 us | 4.843 us | 0.82x |
| `literal-family/type-token-family` | 9.164 us | 11.390 us | 25.814 us | 0.80x |
| `structural/ordered-identifier-window` | 8.337 us | 10.542 us | 25.037 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 0.639 us | 0.842 us | 1.090 us | 0.76x |
| `literal/identifier-token-casei` | 3.921 us | 5.541 us | 20.764 us | 0.71x |
| `structural/keyword-to-generic-type` | 2.278 us | 3.453 us | 17.714 us | 0.66x |
| `structural/ordered-keyword-pair` | 2.549 us | 4.134 us | 17.821 us | 0.62x |
| `literal-family/method-token-family` | 2.625 us | 5.313 us | 19.170 us | 0.49x |
| `literal/identifier-token` | 2.271 us | 4.674 us | 18.024 us | 0.49x |
| `literal/call-token` | 1.860 us | 3.969 us | 17.897 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.953 us | 10.641 us | 26.082 us | 0.28x |
| `lokad/folding/region-marker` | 5.589 us | 36.005 us | 62.768 us | 0.16x |
| `balancing/begin-end-blocks` | 47.597 us | 464.491 us | 498.590 us | 0.10x |
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
