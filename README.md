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
| `literal/sherlock-zh` | 14.160 us | 18.837 us | 2,243.589 us | 0.75x |
| `literal/sherlock-ru` | 66.538 us | 93.632 us | 5,405.771 us | 0.71x |
| `literal-alternate/sherlock-zh` | 43.419 us | 68.860 us | 2,164.644 us | 0.63x |
| `ruff-noqa/tweaked` | 3,996.963 us | 7,866.400 us | 43,943.800 us | 0.51x |
| `literal/sherlock-casei-ru` | 144.475 us | 364.245 us | 5,441.364 us | 0.40x |
| `bounded-repeat/letters-en` | 1,110.194 us | 2,812.046 us | 2,978.012 us | 0.39x |
| `literal/sherlock-en` | 31.581 us | 84.631 us | 480.220 us | 0.37x |
| `literal/sherlock-casei-en` | 68.653 us | 188.743 us | 675.530 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,006.108 us | 5,801.338 us | 6,707.175 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,441.168 us | 4,792.113 us | 9,972.325 us | 0.30x |
| `dictionary/single` | 7,657.410 us | 26,332.467 us | 26,190.100 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 161.385 us | 591.991 us | 1,072.212 us | 0.27x |
| `bounded-repeat/capitals` | 11,849.443 us | 46,059.550 us | 49,380.000 us | 0.26x |
| `literal-alternate/sherlock-en` | 353.270 us | 1,525.996 us | 2,110.914 us | 0.23x |
| `aws-keys/quick` | 1,894.000 us | 9,455.050 us | 42,057.650 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 126.038 us | 656.147 us | 1,106.014 us | 0.19x |
| `aws-keys/full` | 8,662.256 us | 86,318.100 us | 117,000.900 us | 0.10x |
| `bounded-repeat/context` | 9,022.089 us | 146,638.800 us | 151,427.400 us | 0.06x |
| `ruff-noqa/real` | 3,554.427 us | 70,166.750 us | 104,056.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 855.536 us | 23,194.150 us | 28,322.850 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 105.968 us | 11,431.400 us | 12,247.129 us | 0.0093x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.085 us | 0.110 us | 0.146 us | 0.78x |
| `common/ip-match` | `IsMatch` | 0.117 us | 0.169 us | 0.191 us | 0.69x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.077 us | 0.091 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.110 us | 0.140 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.083 us | 0.100 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,194.535 us | 1,864.627 us | 10,368.033 us | 0.64x |
| `industry/mariomka-email-count` | `Count` | 274.001 us | 501.454 us | 8,809.750 us | 0.55x |
| `common/uri-miss` | `IsMatch` | 0.100 us | 0.184 us | 0.202 us | 0.54x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.081 us | 0.098 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.139 us | 0.267 us | 0.293 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.163 us | 0.181 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 631.324 us | 1,323.912 us | 8,415.000 us | 0.48x |
| `common/match-word` | `Match` | 0.051 us | 0.127 us | 1.168 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.229 us | 0.267 us | 0.39x |
| `common/ip-miss` | `IsMatch` | 0.272 us | 0.714 us | 0.734 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,130.604 us | 8,760.622 us | 17,157.433 us | 0.36x |
| `common/matches-set` | `Count` | 62.474 us | 192.832 us | 194.059 us | 0.32x |
| `common/matches-word` | `Count` | 1.244 us | 4.299 us | 5.921 us | 0.29x |
| `common/replace-words` | `Replace` | 20.606 us | 77.306 us | 78.480 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.987 us | 256.545 us | 515.493 us | 0.27x |
| `common/date-miss` | `IsMatch` | 0.110 us | 0.554 us | 0.591 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.691 us | 961.029 us | 1,267.559 us | 0.18x |
| `common/matches-boundary` | `Count` | 28.932 us | 172.820 us | 175.444 us | 0.17x |
| `common/split-words` | `Split` | 13.004 us | 80.248 us | 82.046 us | 0.16x |
| `common/matches-words` | `Count` | 9.286 us | 70.839 us | 72.803 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,037.585 us | 9,848.163 us | 16,717.333 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,817.785 us | 19,531.175 us | 20,381.975 us | 0.09x |
| `common/one-node-backtracking` | `IsMatch` | 0.033 us | 0.615 us | 0.649 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.344 us | 0.367 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,628.957 us | 31,793.567 us | 39,202.100 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,026.707 us | 23,531.625 us | 30,970.350 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.029 us | 0.830 us | 0.866 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 262.913 us | 8,593.267 us | 9,044.622 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 290.417 us | 11,827.329 us | 12,360.650 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.429 us | 35.996 us | 2,140.217 us | 1.21x |
| `bounded-repeat/letters-ru` | 2,014.658 us | 2,041.032 us | 2,925.588 us | 0.99x |
| `literal-alternate/sherlock-en` | 354.928 us | 371.915 us | 829.482 us | 0.95x |
| `literal-alternate/sherlock-en-mixed` | 162.240 us | 174.330 us | 674.517 us | 0.93x |
| `ruff-noqa/tweaked` | 4,022.595 us | 4,571.488 us | 38,026.300 us | 0.88x |
| `bounded-repeat/capitals` | 11,840.929 us | 13,562.033 us | 16,364.950 us | 0.87x |
| `literal/sherlock-casei-en` | 68.505 us | 80.118 us | 475.085 us | 0.86x |
| `literal/sherlock-ru` | 66.426 us | 85.984 us | 5,249.600 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 104.288 us | 144.911 us | 761.519 us | 0.72x |
| `literal/sherlock-casei-ru` | 145.690 us | 219.716 us | 5,213.986 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 127.135 us | 200.052 us | 608.762 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,446.892 us | 2,343.606 us | 7,489.025 us | 0.62x |
| `bounded-repeat/letters-en` | 1,110.557 us | 1,835.138 us | 1,954.369 us | 0.61x |
| `literal/sherlock-zh` | 14.145 us | 23.906 us | 2,229.129 us | 0.59x |
| `aws-keys/quick` | 1,903.366 us | 3,602.527 us | 36,262.700 us | 0.53x |
| `aws-keys/full` | 8,647.989 us | 18,422.700 us | 50,366.867 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 859.667 us | 1,962.005 us | 7,151.075 us | 0.44x |
| `literal/sherlock-en` | 31.516 us | 73.944 us | 421.283 us | 0.43x |
| `ruff-noqa/real` | 3,562.727 us | 20,724.900 us | 57,116.467 us | 0.17x |
| `dictionary/single` | 7,599.750 us | 45,401.450 us | 45,503.100 us | 0.17x |
| `bounded-repeat/context` | 9,039.900 us | 69,040.050 us | 72,371.900 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.109 us | 0.047 us | 0.068 us | 2.30x |
| `common/date-match` | `IsMatch` | 0.070 us | 0.042 us | 0.071 us | 1.68x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.032 us | 0.050 us | 1.57x |
| `common/uri-miss` | `IsMatch` | 0.100 us | 0.064 us | 0.084 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.258 us | 0.180 us | 0.205 us | 1.43x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.054 us | 0.076 us | 1.43x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.031 us | 0.047 us | 1.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.036 us | 0.052 us | 1.25x |
| `common/replace-words` | `Replace` | 20.625 us | 17.567 us | 20.801 us | 1.17x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.699 us | 60.536 us | 284.600 us | 1.12x |
| `industry/leipzig-river-window-count` | `Count` | 1,646.830 us | 1,505.760 us | 7,774.940 us | 1.09x |
| `industry/mariomka-uri-count` | `Count` | 1,201.249 us | 1,105.096 us | 8,487.067 us | 1.09x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.049 us | 0.089 us | 1.07x |
| `common/match-word` | `Match` | 0.052 us | 0.050 us | 0.944 us | 1.04x |
| `common/uri-match` | `IsMatch` | 0.088 us | 0.090 us | 0.122 us | 0.98x |
| `common/email-match` | `IsMatch` | 0.075 us | 0.080 us | 0.108 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,156.060 us | 3,431.843 us | 10,742.933 us | 0.92x |
| `common/matches-words` | `Count` | 9.234 us | 10.891 us | 14.004 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 1,035.428 us | 1,253.457 us | 7,322.900 us | 0.83x |
| `common/backtracking` | `IsMatch` | 0.028 us | 0.044 us | 0.074 us | 0.65x |
| `common/split-words` | `Split` | 12.933 us | 20.531 us | 22.450 us | 0.63x |
| `common/matches-set` | `Count` | 62.000 us | 105.503 us | 107.122 us | 0.59x |
| `industry/mariomka-email-count` | `Count` | 275.761 us | 469.461 us | 7,662.375 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.198 us | 0.234 us | 0.55x |
| `common/matches-word` | `Count` | 1.239 us | 2.346 us | 3.444 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 638.892 us | 1,307.705 us | 7,238.900 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,044.801 us | 2,989.273 us | 7,817.225 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.032 us | 0.107 us | 0.127 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.623 us | 589.390 us | 865.948 us | 0.29x |
| `common/matches-boundary` | `Count` | 28.968 us | 104.390 us | 106.515 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,824.405 us | 8,816.067 us | 9,460.050 us | 0.21x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.090 us | 0.115 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 259.647 us | 3,977.495 us | 4,316.389 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 291.374 us | 4,785.475 us | 5,201.457 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.002 us | 6.420 us | 32.366 us | 0.93x |
| `fallback/lookahead` | 5.372 us | 5.894 us | 20.031 us | 0.91x |
| `lokad/langserv/color-short-hex` | 0.553 us | 0.808 us | 0.986 us | 0.68x |
| `structural/ordered-keyword-window` | 11.921 us | 17.770 us | 32.083 us | 0.67x |
| `structural/method-family-generic-call` | 4.409 us | 6.628 us | 21.451 us | 0.67x |
| `structural/method-family-call` | 5.324 us | 8.066 us | 22.517 us | 0.66x |
| `lokad/lexer/identifier` | 8.933 us | 13.637 us | 16.556 us | 0.66x |
| `structural/ordered-modifier-pair` | 11.168 us | 17.055 us | 31.784 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.745 us | 7.407 us | 8.980 us | 0.64x |
| `lokad/lexer/doc-line` | 1.119 us | 1.836 us | 2.592 us | 0.61x |
| `lokad/docs/inline-doc-prefix` | 2.023 us | 3.474 us | 4.218 us | 0.58x |
| `structural/keyword-to-generic-type` | 2.261 us | 4.077 us | 18.724 us | 0.55x |
| `lokad/style/hex-color` | 0.266 us | 0.506 us | 0.594 us | 0.53x |
| `lokad/langserv/helper-identifier` | 2.441 us | 4.647 us | 5.653 us | 0.53x |
| `literal/identifier-token` | 2.231 us | 4.290 us | 18.332 us | 0.52x |
| `lokad/lexer/operator-run` | 1.185 us | 2.322 us | 2.791 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.547 us | 5.243 us | 20.107 us | 0.49x |
| `lokad/style/cell-ref` | 0.848 us | 1.904 us | 2.122 us | 0.45x |
| `lokad/style/range-ref` | 0.781 us | 1.821 us | 1.962 us | 0.43x |
| `literal/call-token` | 1.828 us | 4.292 us | 18.699 us | 0.43x |
| `lokad/lexer/string` | 32.407 us | 83.118 us | 87.827 us | 0.39x |
| `literal-family/type-token-family` | 9.288 us | 23.950 us | 39.008 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.693 us | 1.803 us | 2.050 us | 0.38x |
| `lokad/langserv/url-dashboard` | 8.654 us | 23.075 us | 23.555 us | 0.38x |
| `lokad/lexer/number` | 3.183 us | 10.644 us | 11.704 us | 0.30x |
| `structural/ordered-identifier-window` | 8.184 us | 28.516 us | 43.594 us | 0.29x |
| `literal-family/method-token-family` | 2.625 us | 9.182 us | 23.466 us | 0.29x |
| `literal/identifier-token-casei` | 3.923 us | 14.963 us | 30.071 us | 0.26x |
| `structural/modifier-family-to-type-window` | 58.967 us | 258.722 us | 273.615 us | 0.23x |
| `lokad/langserv/url-download` | 11.054 us | 51.638 us | 52.146 us | 0.21x |
| `lokad/folding/region-marker` | 5.636 us | 35.804 us | 63.801 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.053 us | 214.917 us | 234.767 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.928 us | 133.500 us | 149.471 us | 0.02x |
| `balancing/begin-end-blocks` | 47.408 us | 4,391.578 us | 4,399.618 us | 0.01x |
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
| `lokad/docs/inline-doc-prefix` | 2.009 us | 1.427 us | 2.217 us | 1.41x |
| `structural/ordered-keyword-window` | 11.943 us | 8.550 us | 22.773 us | 1.40x |
| `lokad/langserv/url-dashboard` | 8.723 us | 6.296 us | 6.783 us | 1.39x |
| `fallback/lookahead` | 5.674 us | 4.171 us | 17.810 us | 1.36x |
| `lokad/imports/module-imports` | 5.944 us | 4.441 us | 29.728 us | 1.34x |
| `structural/ordered-modifier-pair` | 11.185 us | 8.512 us | 22.892 us | 1.31x |
| `structural/keyword-family-to-capitalized-identifier` | 10.013 us | 7.950 us | 23.420 us | 1.26x |
| `lokad/lexer/operator-run` | 1.158 us | 0.946 us | 1.430 us | 1.22x |
| `lokad/lexer/identifier` | 8.868 us | 7.956 us | 10.730 us | 1.11x |
| `lokad/langserv/identifier-validator` | 4.739 us | 4.362 us | 5.813 us | 1.09x |
| `lokad/lexer/doc-line` | 1.128 us | 1.045 us | 1.805 us | 1.08x |
| `lokad/style/range-ref` | 0.788 us | 0.731 us | 0.864 us | 1.08x |
| `lokad/lexer/string` | 32.467 us | 30.119 us | 32.809 us | 1.08x |
| `lokad/langserv/color-short-hex` | 0.513 us | 0.478 us | 0.680 us | 1.07x |
| `structural/modifier-family-to-type-window` | 58.888 us | 55.609 us | 72.072 us | 1.06x |
| `structural/method-family-call` | 5.292 us | 5.049 us | 18.960 us | 1.05x |
| `lokad/style/cell-ref` | 0.850 us | 0.824 us | 1.045 us | 1.03x |
| `structural/method-family-generic-call` | 4.405 us | 4.598 us | 18.377 us | 0.96x |
| `lokad/style/hex-color` | 0.215 us | 0.228 us | 0.313 us | 0.94x |
| `lokad/langserv/url-download` | 11.120 us | 12.177 us | 12.653 us | 0.91x |
| `lokad/langserv/helper-identifier` | 2.427 us | 2.671 us | 3.570 us | 0.91x |
| `lokad/lexer/number` | 3.152 us | 3.801 us | 4.884 us | 0.83x |
| `literal-family/type-token-family` | 9.298 us | 11.419 us | 25.795 us | 0.81x |
| `structural/ordered-identifier-window` | 8.213 us | 10.617 us | 25.328 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 0.644 us | 0.844 us | 1.102 us | 0.76x |
| `literal/identifier-token-casei` | 3.924 us | 5.468 us | 20.583 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.266 us | 3.462 us | 17.541 us | 0.65x |
| `structural/ordered-keyword-pair` | 2.536 us | 4.124 us | 18.007 us | 0.61x |
| `literal/identifier-token` | 2.232 us | 4.243 us | 18.089 us | 0.53x |
| `literal-family/method-token-family` | 2.630 us | 5.302 us | 19.258 us | 0.50x |
| `literal/call-token` | 1.842 us | 3.937 us | 17.598 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.935 us | 10.632 us | 25.505 us | 0.28x |
| `lokad/folding/region-marker` | 5.724 us | 35.918 us | 63.109 us | 0.16x |
| `balancing/begin-end-blocks` | 47.407 us | 466.947 us | 500.528 us | 0.10x |
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
