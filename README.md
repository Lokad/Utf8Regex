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
| `literal/sherlock-zh` | 22.045 us | 18.616 us | 2,200.660 us | 1.18x |
| `literal/sherlock-ru` | 67.529 us | 96.805 us | 5,308.973 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.117 us | 68.594 us | 2,170.969 us | 0.63x |
| `ruff-noqa/tweaked` | 3,993.963 us | 7,590.055 us | 42,066.550 us | 0.53x |
| `literal/sherlock-casei-en` | 78.138 us | 186.552 us | 619.166 us | 0.42x |
| `bounded-repeat/letters-en` | 1,118.431 us | 2,791.807 us | 2,970.773 us | 0.40x |
| `literal/sherlock-casei-ru` | 144.818 us | 365.725 us | 5,780.808 us | 0.40x |
| `literal/sherlock-en` | 31.583 us | 86.978 us | 526.724 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,005.165 us | 5,827.946 us | 6,671.108 us | 0.34x |
| `literal-alternate/sherlock-ru` | 1,428.328 us | 4,853.813 us | 10,085.525 us | 0.29x |
| `dictionary/single` | 7,549.840 us | 25,968.667 us | 26,098.500 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 154.443 us | 593.536 us | 1,186.128 us | 0.26x |
| `bounded-repeat/capitals` | 11,601.900 us | 45,483.950 us | 49,071.000 us | 0.26x |
| `literal-alternate/sherlock-en` | 349.774 us | 1,470.296 us | 2,017.472 us | 0.24x |
| `aws-keys/quick` | 2,037.003 us | 9,797.675 us | 43,633.650 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 127.158 us | 650.409 us | 1,280.089 us | 0.20x |
| `aws-keys/full` | 8,442.378 us | 85,895.800 us | 120,860.200 us | 0.10x |
| `bounded-repeat/context` | 8,861.644 us | 146,516.200 us | 150,997.200 us | 0.06x |
| `ruff-noqa/real` | 3,472.432 us | 69,476.850 us | 85,321.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 849.106 us | 23,257.575 us | 28,460.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 108.208 us | 11,459.786 us | 12,261.743 us | 0.0094x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.084 us | 0.111 us | 0.146 us | 0.76x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.075 us | 0.090 us | 0.66x |
| `common/ip-match` | `IsMatch` | 0.111 us | 0.170 us | 0.190 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.109 us | 0.137 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,202.037 us | 1,901.422 us | 10,556.875 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.083 us | 0.100 us | 0.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.046 us | 0.082 us | 0.100 us | 0.56x |
| `industry/mariomka-email-count` | `Count` | 271.969 us | 495.624 us | 8,938.900 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.138 us | 0.271 us | 0.299 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.081 us | 0.169 us | 0.184 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.088 us | 0.183 us | 0.203 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 629.635 us | 1,320.902 us | 8,652.080 us | 0.48x |
| `common/match-word` | `Match` | 0.051 us | 0.129 us | 1.163 us | 0.39x |
| `common/ip-miss` | `IsMatch` | 0.279 us | 0.718 us | 0.733 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,059.252 us | 8,182.460 us | 16,864.867 us | 0.37x |
| `common/uri-match` | `IsMatch` | 0.088 us | 0.236 us | 0.275 us | 0.37x |
| `common/matches-set` | `Count` | 63.123 us | 194.243 us | 196.517 us | 0.32x |
| `common/matches-word` | `Count` | 1.227 us | 4.154 us | 5.877 us | 0.30x |
| `common/replace-words` | `Replace` | 20.298 us | 77.362 us | 78.756 us | 0.26x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.544 us | 262.718 us | 513.078 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.110 us | 0.569 us | 0.601 us | 0.19x |
| `common/split-words` | `Split` | 14.213 us | 79.867 us | 80.763 us | 0.18x |
| `common/matches-boundary` | `Count` | 29.464 us | 166.133 us | 167.385 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.621 us | 972.701 us | 1,270.103 us | 0.17x |
| `common/matches-words` | `Count` | 9.132 us | 68.682 us | 70.003 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,066.903 us | 9,700.975 us | 17,074.600 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,915.062 us | 19,132.275 us | 19,709.025 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.613 us | 0.651 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.339 us | 0.362 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,670.887 us | 32,178.567 us | 39,901.450 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,015.163 us | 22,572.425 us | 33,058.750 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.817 us | 0.857 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 246.642 us | 8,553.633 us | 9,084.967 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 284.780 us | 12,245.629 us | 12,709.083 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.600 us | 35.658 us | 2,154.985 us | 1.22x |
| `literal/sherlock-casei-en` | 78.592 us | 76.235 us | 551.825 us | 1.03x |
| `literal-alternate/sherlock-en` | 349.141 us | 340.853 us | 944.813 us | 1.02x |
| `bounded-repeat/letters-ru` | 2,006.563 us | 2,056.924 us | 2,908.746 us | 0.98x |
| `literal/sherlock-zh` | 21.861 us | 23.747 us | 2,247.233 us | 0.92x |
| `literal-alternate/sherlock-en-mixed` | 155.475 us | 174.425 us | 668.039 us | 0.89x |
| `ruff-noqa/tweaked` | 4,060.968 us | 4,566.041 us | 37,030.150 us | 0.89x |
| `bounded-repeat/capitals` | 11,616.529 us | 13,566.033 us | 16,536.025 us | 0.86x |
| `literal-alternate/sherlock-casei-en` | 108.206 us | 133.503 us | 754.438 us | 0.81x |
| `literal/sherlock-ru` | 66.898 us | 88.744 us | 5,166.420 us | 0.75x |
| `literal/sherlock-casei-ru` | 147.579 us | 222.719 us | 5,533.247 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 126.593 us | 202.798 us | 702.053 us | 0.62x |
| `bounded-repeat/letters-en` | 1,118.348 us | 1,814.339 us | 1,936.538 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,434.681 us | 2,358.370 us | 7,473.625 us | 0.61x |
| `aws-keys/quick` | 2,040.759 us | 3,714.919 us | 36,267.850 us | 0.55x |
| `aws-keys/full` | 8,446.044 us | 18,310.000 us | 53,880.400 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 849.092 us | 1,970.815 us | 7,229.025 us | 0.43x |
| `literal/sherlock-en` | 31.702 us | 76.561 us | 432.889 us | 0.41x |
| `ruff-noqa/real` | 3,574.052 us | 20,797.425 us | 34,926.967 us | 0.17x |
| `dictionary/single` | 7,552.550 us | 45,525.450 us | 45,787.650 us | 0.17x |
| `bounded-repeat/context` | 8,856.622 us | 69,046.500 us | 71,773.650 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.109 us | 0.047 us | 0.067 us | 2.33x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.042 us | 0.072 us | 1.68x |
| `common/ip-miss` | `IsMatch` | 0.282 us | 0.182 us | 0.204 us | 1.56x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.032 us | 0.051 us | 1.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.055 us | 0.076 us | 1.41x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.035 us | 0.053 us | 1.33x |
| `common/uri-miss` | `IsMatch` | 0.087 us | 0.066 us | 0.085 us | 1.32x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.031 us | 0.050 us | 1.31x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.613 us | 58.192 us | 273.152 us | 1.16x |
| `common/replace-words` | `Replace` | 20.467 us | 17.708 us | 19.209 us | 1.16x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.048 us | 0.086 us | 1.11x |
| `industry/mariomka-uri-count` | `Count` | 1,206.111 us | 1,100.342 us | 8,444.650 us | 1.10x |
| `common/match-word` | `Match` | 0.050 us | 0.047 us | 0.939 us | 1.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,695.493 us | 1,585.490 us | 8,644.040 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.087 us | 0.090 us | 0.122 us | 0.97x |
| `common/email-match` | `IsMatch` | 0.075 us | 0.081 us | 0.109 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,092.612 us | 3,396.317 us | 10,745.600 us | 0.91x |
| `industry/leipzig-symbol-count` | `Count` | 1,025.730 us | 1,238.045 us | 10,391.500 us | 0.83x |
| `common/backtracking` | `IsMatch` | 0.031 us | 0.043 us | 0.074 us | 0.72x |
| `common/split-words` | `Split` | 14.142 us | 21.393 us | 23.118 us | 0.66x |
| `common/matches-set` | `Count` | 62.927 us | 105.062 us | 107.066 us | 0.60x |
| `industry/mariomka-email-count` | `Count` | 271.436 us | 460.355 us | 7,673.800 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.198 us | 0.233 us | 0.55x |
| `common/matches-word` | `Count` | 1.227 us | 2.338 us | 3.425 us | 0.52x |
| `common/matches-words` | `Count` | 9.146 us | 18.578 us | 21.188 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 633.513 us | 1,307.338 us | 7,376.310 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 1,068.380 us | 3,032.377 us | 8,317.750 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.111 us | 0.130 us | 0.34x |
| `common/matches-boundary` | `Count` | 29.534 us | 103.887 us | 106.302 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.570 us | 594.771 us | 863.276 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,916.405 us | 8,594.867 us | 9,110.175 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.017 us | 0.089 us | 0.114 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 246.854 us | 3,916.925 us | 4,279.617 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 283.698 us | 4,773.075 us | 5,188.450 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.383 us | 6.288 us | 32.044 us | 1.02x |
| `fallback/lookahead` | 5.266 us | 6.202 us | 21.527 us | 0.85x |
| `lokad/lexer/identifier` | 9.863 us | 13.588 us | 16.612 us | 0.73x |
| `lokad/langserv/color-short-hex` | 0.576 us | 0.815 us | 1.001 us | 0.71x |
| `structural/ordered-keyword-window` | 11.859 us | 17.471 us | 32.001 us | 0.68x |
| `structural/method-family-generic-call` | 4.312 us | 6.493 us | 21.290 us | 0.66x |
| `structural/method-family-call` | 5.518 us | 8.332 us | 22.693 us | 0.66x |
| `lokad/lexer/doc-line` | 1.212 us | 1.869 us | 2.622 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.862 us | 7.582 us | 8.985 us | 0.64x |
| `structural/keyword-to-generic-type` | 2.436 us | 4.089 us | 19.090 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.326 us | 17.833 us | 32.082 us | 0.58x |
| `lokad/docs/inline-doc-prefix` | 1.996 us | 3.506 us | 4.228 us | 0.57x |
| `literal/identifier-token` | 2.248 us | 4.206 us | 18.714 us | 0.53x |
| `structural/ordered-keyword-pair` | 2.866 us | 5.415 us | 20.170 us | 0.53x |
| `lokad/langserv/helper-identifier` | 2.465 us | 4.772 us | 5.716 us | 0.52x |
| `lokad/lexer/operator-run` | 1.209 us | 2.365 us | 2.837 us | 0.51x |
| `lokad/style/hex-color` | 0.236 us | 0.534 us | 0.615 us | 0.44x |
| `literal/call-token` | 1.812 us | 4.140 us | 19.029 us | 0.44x |
| `lokad/style/range-ref` | 0.784 us | 1.817 us | 1.963 us | 0.43x |
| `lokad/style/cell-ref` | 0.850 us | 1.976 us | 2.074 us | 0.43x |
| `lokad/lexer/string` | 32.698 us | 81.229 us | 87.468 us | 0.40x |
| `lokad/langserv/url-dashboard` | 9.073 us | 23.602 us | 23.868 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.686 us | 1.789 us | 2.040 us | 0.38x |
| `literal-family/type-token-family` | 9.282 us | 25.322 us | 40.271 us | 0.37x |
| `lokad/lexer/number` | 3.652 us | 10.768 us | 11.945 us | 0.34x |
| `structural/ordered-identifier-window` | 8.143 us | 28.515 us | 43.165 us | 0.29x |
| `literal-family/method-token-family` | 2.634 us | 9.439 us | 23.653 us | 0.28x |
| `structural/modifier-family-to-type-window` | 61.281 us | 253.452 us | 270.357 us | 0.24x |
| `lokad/langserv/url-download` | 11.227 us | 48.323 us | 49.093 us | 0.23x |
| `literal/identifier-token-casei` | 3.439 us | 15.324 us | 29.956 us | 0.22x |
| `lokad/folding/region-marker` | 5.557 us | 35.570 us | 63.597 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.969 us | 221.784 us | 240.224 us | 0.04x |
| `structural/modifier-family-to-type-pair` | 2.933 us | 128.229 us | 145.020 us | 0.02x |
| `balancing/begin-end-blocks` | 47.020 us | 4,357.422 us | 4,378.078 us | 0.01x |
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
| `lokad/imports/module-imports` | 6.357 us | 4.367 us | 29.429 us | 1.46x |
| `lokad/langserv/url-dashboard` | 9.069 us | 6.283 us | 6.797 us | 1.44x |
| `structural/ordered-keyword-window` | 11.876 us | 8.536 us | 22.721 us | 1.39x |
| `lokad/docs/inline-doc-prefix` | 1.990 us | 1.445 us | 2.175 us | 1.38x |
| `structural/keyword-family-to-capitalized-identifier` | 10.038 us | 7.826 us | 22.501 us | 1.28x |
| `fallback/lookahead` | 5.302 us | 4.230 us | 18.895 us | 1.25x |
| `lokad/lexer/operator-run` | 1.164 us | 0.941 us | 1.393 us | 1.24x |
| `structural/ordered-modifier-pair` | 10.291 us | 8.634 us | 22.867 us | 1.19x |
| `lokad/lexer/identifier` | 9.483 us | 8.008 us | 10.785 us | 1.18x |
| `lokad/lexer/doc-line` | 1.202 us | 1.053 us | 1.862 us | 1.14x |
| `lokad/langserv/identifier-validator` | 4.909 us | 4.378 us | 5.740 us | 1.12x |
| `structural/modifier-family-to-type-window` | 62.234 us | 55.608 us | 71.921 us | 1.12x |
| `structural/method-family-call` | 5.538 us | 5.064 us | 19.385 us | 1.09x |
| `lokad/style/range-ref` | 0.780 us | 0.730 us | 0.864 us | 1.07x |
| `lokad/langserv/color-short-hex` | 0.509 us | 0.478 us | 0.679 us | 1.06x |
| `lokad/lexer/string` | 32.305 us | 30.628 us | 33.439 us | 1.05x |
| `lokad/style/cell-ref` | 0.846 us | 0.837 us | 1.053 us | 1.01x |
| `lokad/lexer/number` | 3.643 us | 3.839 us | 4.997 us | 0.95x |
| `structural/method-family-generic-call` | 4.303 us | 4.584 us | 18.345 us | 0.94x |
| `lokad/style/hex-color` | 0.217 us | 0.234 us | 0.317 us | 0.93x |
| `lokad/langserv/url-download` | 11.203 us | 12.174 us | 12.655 us | 0.92x |
| `lokad/langserv/helper-identifier` | 2.457 us | 2.706 us | 3.674 us | 0.91x |
| `literal-family/type-token-family` | 9.186 us | 11.352 us | 25.701 us | 0.81x |
| `structural/ordered-identifier-window` | 8.201 us | 10.591 us | 25.187 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 0.639 us | 0.845 us | 1.097 us | 0.76x |
| `structural/keyword-to-generic-type` | 2.435 us | 3.492 us | 17.964 us | 0.70x |
| `structural/ordered-keyword-pair` | 2.861 us | 4.131 us | 18.359 us | 0.69x |
| `literal/identifier-token-casei` | 3.454 us | 5.475 us | 20.393 us | 0.63x |
| `literal-family/method-token-family` | 2.628 us | 5.094 us | 19.431 us | 0.52x |
| `literal/identifier-token` | 2.245 us | 4.672 us | 18.106 us | 0.48x |
| `literal/call-token` | 1.837 us | 3.942 us | 17.888 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.928 us | 10.742 us | 25.148 us | 0.27x |
| `lokad/folding/region-marker` | 5.631 us | 35.591 us | 62.737 us | 0.16x |
| `balancing/begin-end-blocks` | 46.542 us | 463.027 us | 495.066 us | 0.10x |
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
