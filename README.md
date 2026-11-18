# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes ongoing 0.5.0 development. Its API and execution machinery
are being simplified. Published 0.4.0 and frozen development packages retain
their original behavior; development package qualification is not a release.

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
`ReadOnlyMemory<byte>` for groups and capture history. Match byte ranges can be used directly with UTF-8
slices. `EnumerateMatches(input, startOffsetInBytes, cancellationToken)` searches
from a scalar-aligned byte position while retaining full context for assertions.
The byte-start `Match` can fill caller-owned `Utf8CaptureRange` slots; slot zero
is the whole match and `GroupNumberFromCaptureSlot` resolves sparse group numbers.
Use the persistent memory cursor when capture history is required.
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

See [release notes](CHANGELOG.md) and the [migration guide](docs/MIGRATION.md)
for the 0.4.0 changes.

## Runtime support

The 0.5.0 release candidate targets `net10.0` and `net11.0`. .NET 11
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The **18 November 2026** refresh measured source `654d5716` on **.NET 11.0.0**,
Ubuntu 24.04.4 x64 and an Intel Xeon Platinum 8573C. The Release build, .NET 10/11
tests, all-profile catalogs, paired comparisons, lifecycle, scaling and resource
checks completed and verified in **52m17s**.

The measured operations start with valid, unchanged UTF-8 and include the required
result materialization. [The Core summary](bench/Snapshots/Core.json) records each
row's source, qualification and snapshot identity. [Generality measurements](docs/BENCHMARKS.md)
cover ordinary patterns and calls with finite limits, deadlines and cancellation.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

The `common/ip-miss` row was refreshed separately on `edf04445` with the
corrected miss input.

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
| `literal/sherlock-zh` | 21.808 us | 18.294 us | 2,138.883 us | 1.19x |
| `aws-keys/full` | 61,354.500 us | 84,132.700 us | 116,322.500 us | 0.73x |
| `literal/sherlock-ru` | 68.415 us | 96.445 us | 5,182.180 us | 0.71x |
| `literal-alternate/sherlock-zh` | 41.809 us | 66.992 us | 2,152.074 us | 0.62x |
| `literal/sherlock-casei-en` | 103.145 us | 184.419 us | 589.249 us | 0.56x |
| `aws-keys/quick` | 5,138.660 us | 9,273.400 us | 39,280.550 us | 0.55x |
| `ruff-noqa/tweaked` | 3,612.319 us | 7,712.255 us | 41,081.450 us | 0.47x |
| `literal/sherlock-casei-ru` | 144.815 us | 357.935 us | 5,412.007 us | 0.40x |
| `bounded-repeat/letters-en` | 1,085.396 us | 2,759.146 us | 2,859.731 us | 0.39x |
| `literal/sherlock-en` | 30.574 us | 85.803 us | 444.857 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,909.550 us | 5,683.757 us | 6,486.992 us | 0.34x |
| `dictionary/single` | 8,051.740 us | 25,301.100 us | 25,458.667 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,409.807 us | 4,741.288 us | 9,752.850 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 154.846 us | 601.944 us | 1,014.192 us | 0.26x |
| `literal-alternate/sherlock-en` | 342.353 us | 1,485.822 us | 2,037.535 us | 0.23x |
| `bounded-repeat/capitals` | 9,725.100 us | 44,810.600 us | 48,048.200 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 125.032 us | 633.337 us | 1,052.392 us | 0.20x |
| `bounded-repeat/context` | 19,503.875 us | 144,645.100 us | 147,211.600 us | 0.13x |
| `ruff-noqa/real` | 3,166.537 us | 67,825.700 us | 100,446.100 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 841.208 us | 23,201.575 us | 28,247.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 127.115 us | 10,910.557 us | 11,806.129 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 1.009 us | 0.168 us | 0.187 us | 6.00x |
| `common/uri-miss` | `IsMatch` | 0.831 us | 0.180 us | 0.201 us | 4.62x |
| `industry/rust-sherlock-ing-count` | `Count` | 48,064.250 us | 11,613.886 us | 12,368.586 us | 4.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.304 us | 0.078 us | 0.087 us | 3.89x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.287 us | 0.081 us | 0.095 us | 3.57x |
| `common/date-match` | `IsMatch` | 0.363 us | 0.102 us | 0.130 us | 3.55x |
| `common/matches-boundary` | `Count` | 533.147 us | 158.590 us | 161.561 us | 3.36x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.275 us | 0.082 us | 0.099 us | 3.35x |
| `common/email-match` | `IsMatch` | 0.862 us | 0.259 us | 0.286 us | 3.32x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.544 us | 0.165 us | 0.182 us | 3.29x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.352 us | 0.108 us | 0.144 us | 3.25x |
| `common/uri-match` | `IsMatch` | 0.740 us | 0.237 us | 0.268 us | 3.13x |
| `common/date-miss` | `IsMatch` | 1.314 us | 0.548 us | 0.580 us | 2.40x |
| `industry/mariomka-uri-count` | `Count` | 4,365.665 us | 1,840.385 us | 10,097.300 us | 2.37x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 2,040.674 us | 942.910 us | 1,227.546 us | 2.16x |
| `common/ip-miss` | `IsMatch` | 1.092 us | 0.718 us | 0.721 us | 1.52x |
| `industry/mariomka-email-count` | `Count` | 672.148 us | 485.808 us | 8,740.525 us | 1.38x |
| `common/match-word` | `Match` | 0.155 us | 0.167 us | 1.121 us | 0.93x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 131.375 us | 256.407 us | 490.794 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 612.260 us | 1,277.247 us | 8,249.960 us | 0.48x |
| `common/matches-set` | `Count` | 60.862 us | 195.248 us | 197.331 us | 0.31x |
| `common/backtracking` | `IsMatch` | 0.249 us | 0.817 us | 0.856 us | 0.30x |
| `common/matches-word` | `Count` | 1.221 us | 4.089 us | 5.735 us | 0.30x |
| `common/replace-words` | `Replace` | 21.372 us | 75.038 us | 78.257 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,168.654 us | 8,129.890 us | 16,384.867 us | 0.27x |
| `common/split-words` | `Split` | 14.200 us | 75.101 us | 77.124 us | 0.19x |
| `common/matches-words` | `Count` | 8.909 us | 67.894 us | 69.795 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.037 us | 0.333 us | 0.360 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 1,027.929 us | 9,668.362 us | 16,369.700 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,822.264 us | 19,013.475 us | 19,584.300 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.047 us | 0.594 us | 0.634 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,729.951 us | 31,521.767 us | 38,691.550 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,018.546 us | 23,997.400 us | 31,659.250 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 301.163 us | 8,246.260 us | 8,748.544 us | 0.04x |
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
| `aws-keys/full` | 61,160.000 us | 17,904.140 us | 52,521.867 us | 3.42x |
| `aws-keys/quick` | 5,097.107 us | 3,579.667 us | 34,222.267 us | 1.42x |
| `literal/sherlock-casei-en` | 102.450 us | 77.818 us | 428.891 us | 1.32x |
| `literal-alternate/sherlock-zh` | 42.557 us | 35.526 us | 2,138.944 us | 1.20x |
| `literal-alternate/sherlock-en` | 342.651 us | 356.352 us | 772.739 us | 0.96x |
| `literal/sherlock-zh` | 21.868 us | 23.359 us | 2,105.597 us | 0.94x |
| `bounded-repeat/letters-ru` | 1,923.620 us | 2,087.889 us | 2,899.135 us | 0.92x |
| `literal-alternate/sherlock-casei-en` | 126.658 us | 139.977 us | 576.904 us | 0.90x |
| `literal-alternate/sherlock-en-mixed` | 153.793 us | 172.327 us | 598.070 us | 0.89x |
| `ruff-noqa/tweaked` | 3,639.832 us | 4,425.661 us | 34,932.600 us | 0.82x |
| `literal/sherlock-ru` | 68.272 us | 88.154 us | 5,147.720 us | 0.77x |
| `bounded-repeat/capitals` | 9,731.500 us | 13,106.150 us | 16,044.175 us | 0.74x |
| `literal/sherlock-casei-ru` | 145.452 us | 217.602 us | 5,191.060 us | 0.67x |
| `literal-alternate/sherlock-en-nomatch` | 125.681 us | 197.508 us | 568.352 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,400.627 us | 2,269.797 us | 7,236.350 us | 0.62x |
| `bounded-repeat/letters-en` | 1,078.314 us | 1,800.905 us | 1,872.553 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 840.949 us | 1,906.828 us | 7,033.925 us | 0.44x |
| `literal/sherlock-en` | 30.972 us | 74.986 us | 417.780 us | 0.41x |
| `bounded-repeat/context` | 19,674.200 us | 66,876.350 us | 70,393.600 us | 0.29x |
| `dictionary/single` | 7,987.330 us | 44,472.800 us | 44,314.350 us | 0.18x |
| `ruff-noqa/real` | 3,201.817 us | 19,844.275 us | 53,962.833 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 1.010 us | 0.046 us | 0.067 us | 21.83x |
| `common/uri-miss` | `IsMatch` | 0.832 us | 0.065 us | 0.085 us | 12.72x |
| `common/email-match` | `IsMatch` | 0.857 us | 0.078 us | 0.104 us | 11.03x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.542 us | 0.053 us | 0.075 us | 10.32x |
| `industry/rust-sherlock-ing-count` | `Count` | 47,477.700 us | 4,653.765 us | 5,079.712 us | 10.20x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.286 us | 0.029 us | 0.047 us | 9.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.273 us | 0.031 us | 0.049 us | 8.88x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.302 us | 0.035 us | 0.051 us | 8.70x |
| `common/uri-match` | `IsMatch` | 0.741 us | 0.086 us | 0.119 us | 8.61x |
| `common/date-match` | `IsMatch` | 0.360 us | 0.043 us | 0.072 us | 8.41x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.349 us | 0.047 us | 0.084 us | 7.43x |
| `common/date-miss` | `IsMatch` | 1.313 us | 0.195 us | 0.232 us | 6.73x |
| `common/ip-miss` | `IsMatch` | 1.084 us | 0.171 us | 0.193 us | 6.34x |
| `common/backtracking` | `IsMatch` | 0.247 us | 0.043 us | 0.072 us | 5.76x |
| `common/matches-boundary` | `Count` | 532.748 us | 101.823 us | 104.027 us | 5.23x |
| `industry/mariomka-uri-count` | `Count` | 4,395.800 us | 1,067.449 us | 8,175.600 us | 4.12x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 2,031.976 us | 584.772 us | 831.186 us | 3.47x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 130.917 us | 56.486 us | 271.452 us | 2.32x |
| `common/match-word` | `Match` | 0.155 us | 0.079 us | 0.979 us | 1.95x |
| `industry/mariomka-email-count` | `Count` | 685.894 us | 454.469 us | 7,594.775 us | 1.51x |
| `common/replace-words` | `Replace` | 21.494 us | 17.568 us | 18.498 us | 1.22x |
| `industry/leipzig-river-window-count` | `Count` | 1,744.220 us | 1,493.378 us | 7,411.950 us | 1.17x |
| `common/matches-words` | `Count` | 8.910 us | 10.637 us | 13.713 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,019.472 us | 1,236.367 us | 7,073.950 us | 0.82x |
| `common/split-words` | `Split` | 14.196 us | 20.085 us | 21.829 us | 0.71x |
| `industry/mariomka-ip-count` | `Count` | 2,160.791 us | 3,445.909 us | 10,528.400 us | 0.63x |
| `common/matches-set` | `Count` | 61.096 us | 104.446 us | 106.524 us | 0.58x |
| `common/one-node-backtracking` | `IsMatch` | 0.047 us | 0.086 us | 0.126 us | 0.55x |
| `common/matches-word` | `Count` | 1.212 us | 2.283 us | 3.277 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 621.721 us | 1,256.733 us | 6,898.511 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.037 us | 0.089 us | 0.113 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 1,038.715 us | 2,945.727 us | 7,861.850 us | 0.35x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,817.351 us | 8,490.433 us | 8,925.678 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 295.218 us | 3,839.210 us | 4,209.268 us | 0.08x |
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
| `lokad/style/hex-color` | 3.872 us | 0.744 us | 0.822 us | 5.20x |
| `lokad/langserv/color-short-hex` | 4.986 us | 1.326 us | 1.433 us | 3.76x |
| `lokad/docs/inline-doc-prefix` | 17.375 us | 4.654 us | 5.259 us | 3.73x |
| `lokad/lexer/doc-line` | 11.327 us | 3.055 us | 3.511 us | 3.71x |
| `lokad/langserv/identifier-validator` | 46.902 us | 12.861 us | 14.176 us | 3.65x |
| `lokad/lexer/identifier` | 79.104 us | 22.424 us | 25.058 us | 3.53x |
| `lokad/style/cell-ref` | 9.480 us | 2.721 us | 2.927 us | 3.48x |
| `lokad/langserv/helper-identifier` | 23.409 us | 7.223 us | 7.590 us | 3.24x |
| `lokad/lexer/operator-run` | 10.729 us | 3.384 us | 3.800 us | 3.17x |
| `lokad/lexer/number` | 42.045 us | 13.578 us | 14.766 us | 3.10x |
| `lokad/style/range-ref` | 7.672 us | 2.577 us | 2.759 us | 2.98x |
| `lokad/langserv/color-rgb-hex` | 7.554 us | 2.836 us | 2.868 us | 2.66x |
| `structural/ordered-modifier-pair` | 42.047 us | 16.721 us | 29.736 us | 2.51x |
| `lokad/lexer/string` | 211.520 us | 93.632 us | 98.799 us | 2.26x |
| `fallback/lookahead` | 11.563 us | 5.866 us | 18.397 us | 1.97x |
| `structural/method-family-generic-call` | 12.266 us | 6.357 us | 19.504 us | 1.93x |
| `structural/method-family-call` | 15.001 us | 8.037 us | 20.511 us | 1.87x |
| `structural/ordered-keyword-pair` | 9.027 us | 5.316 us | 18.902 us | 1.70x |
| `lokad/langserv/url-dashboard` | 40.068 us | 23.987 us | 24.247 us | 1.67x |
| `balancing/begin-end-blocks` | 6,713.558 us | 4,028.132 us | 4,053.728 us | 1.67x |
| `lokad/imports/module-imports` | 8.857 us | 6.219 us | 30.586 us | 1.42x |
| `structural/keyword-to-generic-type` | 5.264 us | 3.956 us | 17.656 us | 1.33x |
| `lokad/langserv/url-download` | 65.814 us | 51.330 us | 51.109 us | 1.28x |
| `structural/ordered-keyword-window` | 20.876 us | 17.503 us | 31.036 us | 1.19x |
| `structural/modifier-family-to-type-pair` | 90.757 us | 131.307 us | 145.616 us | 0.69x |
| `literal/identifier-token` | 2.696 us | 4.329 us | 17.181 us | 0.62x |
| `structural/modifier-family-to-type-window` | 138.113 us | 251.866 us | 262.762 us | 0.55x |
| `structural/ordered-identifier-window` | 14.829 us | 28.526 us | 42.084 us | 0.52x |
| `literal/call-token` | 1.916 us | 4.098 us | 17.113 us | 0.47x |
| `literal-family/type-token-family` | 9.639 us | 22.952 us | 36.858 us | 0.42x |
| `literal-family/method-token-family` | 3.697 us | 8.986 us | 22.108 us | 0.41x |
| `structural/keyword-family-to-capitalized-identifier` | 66.687 us | 210.163 us | 225.391 us | 0.32x |
| `literal/identifier-token-casei` | 3.964 us | 15.075 us | 29.306 us | 0.26x |
| `lokad/folding/region-marker` | 4.691 us | 34.362 us | 60.982 us | 0.14x |
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
| `balancing/begin-end-blocks` | 6,665.875 us | 451.748 us | 481.545 us | 14.76x |
| `structural/keyword-family-to-capitalized-identifier` | 65.972 us | 7.561 us | 20.609 us | 8.73x |
| `structural/modifier-family-to-type-pair` | 91.263 us | 10.489 us | 22.894 us | 8.70x |
| `lokad/style/hex-color` | 3.811 us | 0.489 us | 0.573 us | 7.79x |
| `lokad/docs/inline-doc-prefix` | 17.433 us | 2.663 us | 3.361 us | 6.55x |
| `lokad/lexer/number` | 42.063 us | 6.715 us | 7.985 us | 6.26x |
| `lokad/style/cell-ref` | 9.491 us | 1.698 us | 1.887 us | 5.59x |
| `lokad/langserv/url-dashboard` | 39.806 us | 7.239 us | 7.830 us | 5.50x |
| `lokad/lexer/operator-run` | 10.742 us | 1.971 us | 2.497 us | 5.45x |
| `lokad/langserv/color-short-hex` | 5.010 us | 0.943 us | 1.108 us | 5.31x |
| `lokad/lexer/doc-line` | 11.332 us | 2.142 us | 2.833 us | 5.29x |
| `lokad/style/range-ref` | 7.624 us | 1.443 us | 1.599 us | 5.28x |
| `lokad/langserv/helper-identifier` | 23.480 us | 4.554 us | 5.563 us | 5.16x |
| `lokad/langserv/url-download` | 63.731 us | 12.608 us | 13.022 us | 5.05x |
| `structural/ordered-modifier-pair` | 41.782 us | 8.341 us | 21.273 us | 5.01x |
| `lokad/langserv/identifier-validator` | 46.909 us | 9.421 us | 11.403 us | 4.98x |
| `lokad/lexer/string` | 210.407 us | 43.929 us | 47.074 us | 4.79x |
| `lokad/lexer/identifier` | 78.299 us | 16.355 us | 19.836 us | 4.79x |
| `lokad/langserv/color-rgb-hex` | 7.534 us | 1.740 us | 1.928 us | 4.33x |
| `structural/method-family-call` | 15.070 us | 4.948 us | 17.033 us | 3.05x |
| `fallback/lookahead` | 11.695 us | 4.089 us | 16.198 us | 2.86x |
| `structural/method-family-generic-call` | 12.187 us | 4.456 us | 16.460 us | 2.74x |
| `structural/modifier-family-to-type-window` | 138.496 us | 54.462 us | 68.148 us | 2.54x |
| `structural/ordered-keyword-window` | 20.840 us | 8.565 us | 21.734 us | 2.43x |
| `structural/ordered-keyword-pair` | 8.988 us | 4.052 us | 16.935 us | 2.22x |
| `lokad/imports/module-imports` | 8.844 us | 4.316 us | 28.017 us | 2.05x |
| `structural/keyword-to-generic-type` | 5.237 us | 3.431 us | 16.711 us | 1.53x |
| `structural/ordered-identifier-window` | 14.725 us | 10.497 us | 24.372 us | 1.40x |
| `literal-family/type-token-family` | 9.686 us | 11.173 us | 24.595 us | 0.87x |
| `literal-family/method-token-family` | 3.721 us | 5.172 us | 17.829 us | 0.72x |
| `literal/identifier-token-casei` | 3.999 us | 5.760 us | 19.088 us | 0.69x |
| `literal/identifier-token` | 2.702 us | 4.615 us | 16.865 us | 0.59x |
| `literal/call-token` | 1.917 us | 3.878 us | 16.139 us | 0.49x |
| `lokad/folding/region-marker` | 4.646 us | 35.018 us | 60.418 us | 0.13x |
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
