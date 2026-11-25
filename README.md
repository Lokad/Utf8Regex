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
| `literal/sherlock-zh` | 21.890 us | 18.395 us | 2,164.434 us | 1.19x |
| `literal/sherlock-ru` | 68.459 us | 96.326 us | 5,242.533 us | 0.71x |
| `literal-alternate/sherlock-zh` | 42.360 us | 67.983 us | 2,160.009 us | 0.62x |
| `ruff-noqa/tweaked` | 3,943.740 us | 7,429.009 us | 40,958.250 us | 0.53x |
| `literal/sherlock-casei-ru` | 148.845 us | 357.441 us | 5,485.193 us | 0.42x |
| `bounded-repeat/letters-en` | 1,087.088 us | 2,719.325 us | 2,901.433 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,077.451 us | 5,725.350 us | 6,536.642 us | 0.36x |
| `literal/sherlock-en` | 31.237 us | 86.602 us | 461.790 us | 0.36x |
| `literal/sherlock-casei-en` | 67.579 us | 190.988 us | 567.399 us | 0.35x |
| `dictionary/single` | 8,027.950 us | 25,531.433 us | 25,334.067 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,413.100 us | 4,624.218 us | 9,685.125 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 153.613 us | 584.792 us | 986.676 us | 0.26x |
| `bounded-repeat/capitals` | 11,599.557 us | 46,464.000 us | 50,433.500 us | 0.25x |
| `literal-alternate/sherlock-en` | 347.716 us | 1,496.364 us | 2,066.223 us | 0.23x |
| `aws-keys/quick` | 1,960.244 us | 9,370.650 us | 40,679.000 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 124.881 us | 644.764 us | 1,061.659 us | 0.19x |
| `aws-keys/full` | 8,542.011 us | 85,732.000 us | 118,221.800 us | 0.10x |
| `bounded-repeat/context` | 8,534.900 us | 143,923.200 us | 150,028.100 us | 0.06x |
| `ruff-noqa/real` | 3,448.000 us | 72,051.700 us | 104,668.200 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 844.399 us | 22,607.725 us | 27,797.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 111.607 us | 11,173.286 us | 12,100.000 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 380.856 us | 489.108 us | 8,708.150 us | 0.78x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.054 us | 0.075 us | 0.090 us | 0.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.108 us | 0.145 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.068 us | 0.107 us | 0.134 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,169.498 us | 1,890.090 us | 10,115.200 us | 0.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.082 us | 0.099 us | 0.61x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.043 us | 0.080 us | 0.096 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 620.491 us | 1,295.925 us | 8,498.350 us | 0.48x |
| `common/email-match` | `IsMatch` | 0.129 us | 0.271 us | 0.296 us | 0.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.073 us | 0.159 us | 0.177 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.167 us | 0.187 us | 0.44x |
| `common/uri-miss` | `IsMatch` | 0.078 us | 0.182 us | 0.203 us | 0.43x |
| `common/match-word` | `Match` | 0.049 us | 0.127 us | 1.114 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,142.792 us | 8,245.930 us | 16,503.100 us | 0.38x |
| `common/matches-set` | `Count` | 65.440 us | 186.862 us | 188.280 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.073 us | 0.223 us | 0.257 us | 0.33x |
| `common/ip-miss` | `IsMatch` | 0.221 us | 0.721 us | 0.742 us | 0.31x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.469 us | 258.075 us | 505.175 us | 0.30x |
| `common/replace-words` | `Replace` | 22.930 us | 77.047 us | 77.775 us | 0.30x |
| `common/matches-word` | `Count` | 1.201 us | 4.105 us | 5.824 us | 0.29x |
| `common/date-miss` | `IsMatch` | 0.105 us | 0.556 us | 0.589 us | 0.19x |
| `common/matches-boundary` | `Count` | 29.117 us | 159.850 us | 162.005 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.319 us | 957.390 us | 1,235.216 us | 0.17x |
| `common/split-words` | `Split` | 12.745 us | 74.443 us | 76.455 us | 0.17x |
| `common/matches-words` | `Count` | 8.915 us | 66.860 us | 69.066 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,042.373 us | 9,739.663 us | 16,884.633 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,940.630 us | 19,067.125 us | 19,660.350 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 876.284 us | 12,129.029 us | 12,697.350 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.598 us | 0.623 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.340 us | 0.363 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 967.623 us | 21,873.375 us | 29,733.550 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.814 us | 0.854 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,262.082 us | 31,641.700 us | 39,159.650 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 270.749 us | 8,222.950 us | 8,707.167 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 42.221 us | 35.567 us | 2,120.034 us | 1.19x |
| `bounded-repeat/letters-ru` | 2,072.268 us | 2,149.111 us | 3,003.254 us | 0.96x |
| `literal-alternate/sherlock-en` | 349.381 us | 363.503 us | 828.045 us | 0.96x |
| `literal/sherlock-zh` | 22.043 us | 23.642 us | 2,156.331 us | 0.93x |
| `literal/sherlock-casei-en` | 68.684 us | 76.241 us | 409.191 us | 0.90x |
| `ruff-noqa/tweaked` | 3,953.570 us | 4,472.759 us | 36,033.250 us | 0.88x |
| `literal-alternate/sherlock-en-mixed` | 153.921 us | 174.582 us | 542.552 us | 0.88x |
| `bounded-repeat/capitals` | 11,698.400 us | 13,552.417 us | 16,562.200 us | 0.86x |
| `literal-alternate/sherlock-casei-en` | 111.948 us | 142.034 us | 589.194 us | 0.79x |
| `literal/sherlock-ru` | 68.918 us | 88.634 us | 5,159.620 us | 0.78x |
| `literal/sherlock-casei-ru` | 146.970 us | 219.465 us | 5,359.953 us | 0.67x |
| `literal-alternate/sherlock-en-nomatch` | 126.430 us | 197.274 us | 573.690 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,415.075 us | 2,280.373 us | 7,282.167 us | 0.62x |
| `bounded-repeat/letters-en` | 1,085.767 us | 1,779.402 us | 1,916.345 us | 0.61x |
| `aws-keys/quick` | 1,972.662 us | 3,593.686 us | 34,470.033 us | 0.55x |
| `aws-keys/full` | 8,522.622 us | 18,302.860 us | 53,949.567 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 853.353 us | 1,887.880 us | 6,876.675 us | 0.45x |
| `literal/sherlock-en` | 31.030 us | 76.145 us | 429.514 us | 0.41x |
| `dictionary/single` | 7,936.910 us | 44,399.300 us | 44,192.350 us | 0.18x |
| `ruff-noqa/real` | 3,403.987 us | 20,853.575 us | 54,743.333 us | 0.16x |
| `bounded-repeat/context` | 8,574.917 us | 67,444.100 us | 71,377.150 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.068 us | 0.041 us | 0.071 us | 1.64x |
| `common/email-match` | `IsMatch` | 0.127 us | 0.079 us | 0.107 us | 1.61x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.075 us | 0.048 us | 0.085 us | 1.57x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.046 us | 0.067 us | 1.50x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.044 us | 0.031 us | 0.050 us | 1.42x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 76.820 us | 56.706 us | 268.214 us | 1.35x |
| `common/replace-words` | `Replace` | 23.023 us | 17.119 us | 18.252 us | 1.34x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.035 us | 0.053 us | 1.30x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.070 us | 0.055 us | 0.075 us | 1.29x |
| `common/ip-miss` | `IsMatch` | 0.221 us | 0.175 us | 0.196 us | 1.26x |
| `common/uri-miss` | `IsMatch` | 0.077 us | 0.065 us | 0.087 us | 1.20x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.036 us | 0.031 us | 0.047 us | 1.16x |
| `industry/mariomka-uri-count` | `Count` | 1,182.117 us | 1,106.625 us | 8,268.725 us | 1.07x |
| `common/match-word` | `Match` | 0.049 us | 0.046 us | 0.901 us | 1.05x |
| `industry/mariomka-ip-count` | `Count` | 3,136.004 us | 3,417.743 us | 10,689.267 us | 0.92x |
| `common/uri-match` | `IsMatch` | 0.074 us | 0.085 us | 0.119 us | 0.87x |
| `industry/mariomka-email-count` | `Count` | 379.927 us | 453.957 us | 7,435.400 us | 0.84x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.043 us | 0.073 us | 0.83x |
| `industry/leipzig-river-window-count` | `Count` | 1,261.182 us | 1,554.486 us | 8,077.850 us | 0.81x |
| `industry/leipzig-symbol-count` | `Count` | 983.592 us | 1,221.029 us | 7,026.675 us | 0.81x |
| `common/split-words` | `Split` | 12.691 us | 19.695 us | 21.451 us | 0.64x |
| `common/matches-set` | `Count` | 65.127 us | 103.470 us | 105.351 us | 0.63x |
| `common/date-miss` | `IsMatch` | 0.105 us | 0.197 us | 0.231 us | 0.53x |
| `common/matches-word` | `Count` | 1.201 us | 2.298 us | 3.375 us | 0.52x |
| `common/matches-words` | `Count` | 8.932 us | 17.721 us | 20.469 us | 0.50x |
| `industry/leipzig-twain-count` | `Count` | 626.398 us | 1,277.213 us | 6,637.533 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,055.996 us | 2,945.078 us | 8,097.225 us | 0.36x |
| `common/matches-boundary` | `Count` | 29.945 us | 101.690 us | 103.474 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.233 us | 587.544 us | 852.685 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.104 us | 0.127 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,951.777 us | 8,691.133 us | 9,156.900 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 867.936 us | 4,720.235 us | 5,137.360 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.087 us | 0.113 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 268.178 us | 3,863.535 us | 4,261.342 us | 0.07x |
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
| `lokad/imports/module-imports` | 5.888 us | 6.315 us | 31.029 us | 0.93x |
| `fallback/lookahead` | 5.441 us | 5.854 us | 19.406 us | 0.93x |
| `lokad/lexer/identifier` | 11.033 us | 13.405 us | 16.294 us | 0.82x |
| `structural/keyword-to-generic-type` | 2.960 us | 3.939 us | 17.446 us | 0.75x |
| `structural/ordered-keyword-window` | 11.993 us | 17.046 us | 30.092 us | 0.70x |
| `structural/method-family-generic-call` | 4.393 us | 6.362 us | 20.176 us | 0.69x |
| `structural/method-family-call` | 5.101 us | 7.916 us | 21.192 us | 0.64x |
| `lokad/langserv/color-short-hex` | 0.513 us | 0.802 us | 0.997 us | 0.64x |
| `structural/ordered-keyword-pair` | 3.148 us | 5.261 us | 18.342 us | 0.60x |
| `structural/ordered-modifier-pair` | 9.966 us | 16.812 us | 30.295 us | 0.59x |
| `lokad/lexer/doc-line` | 1.056 us | 1.816 us | 2.565 us | 0.58x |
| `lokad/langserv/identifier-validator` | 4.127 us | 7.305 us | 8.778 us | 0.56x |
| `literal/identifier-token` | 2.301 us | 4.242 us | 17.122 us | 0.54x |
| `lokad/docs/inline-doc-prefix` | 1.749 us | 3.384 us | 4.115 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.260 us | 4.641 us | 5.530 us | 0.49x |
| `lokad/lexer/operator-run` | 1.057 us | 2.310 us | 2.768 us | 0.46x |
| `literal/call-token` | 1.812 us | 4.211 us | 16.996 us | 0.43x |
| `lokad/style/hex-color` | 0.217 us | 0.511 us | 0.594 us | 0.42x |
| `lokad/style/cell-ref` | 0.796 us | 1.919 us | 2.120 us | 0.41x |
| `lokad/langserv/url-dashboard` | 9.493 us | 23.090 us | 23.598 us | 0.41x |
| `lokad/style/range-ref` | 0.729 us | 1.782 us | 1.951 us | 0.41x |
| `literal-family/type-token-family` | 9.066 us | 23.541 us | 37.452 us | 0.39x |
| `lokad/lexer/string` | 30.255 us | 79.452 us | 82.950 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.619 us | 1.754 us | 2.008 us | 0.35x |
| `structural/ordered-identifier-window` | 8.821 us | 28.127 us | 40.921 us | 0.31x |
| `literal-family/method-token-family` | 2.576 us | 9.153 us | 21.879 us | 0.28x |
| `lokad/lexer/number` | 2.939 us | 10.664 us | 11.597 us | 0.28x |
| `literal/identifier-token-casei` | 3.882 us | 14.881 us | 28.783 us | 0.26x |
| `structural/modifier-family-to-type-window` | 58.808 us | 250.190 us | 266.116 us | 0.24x |
| `lokad/langserv/url-download` | 11.527 us | 50.511 us | 51.182 us | 0.23x |
| `lokad/folding/region-marker` | 5.417 us | 34.603 us | 61.061 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.879 us | 212.226 us | 226.238 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.853 us | 133.615 us | 147.267 us | 0.02x |
| `balancing/begin-end-blocks` | 45.955 us | 4,234.022 us | 4,224.706 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.459 us | 6.303 us | 6.830 us | 1.50x |
| `lokad/lexer/identifier` | 11.078 us | 7.592 us | 10.562 us | 1.46x |
| `structural/ordered-keyword-window` | 12.074 us | 8.323 us | 21.082 us | 1.45x |
| `lokad/imports/module-imports` | 5.945 us | 4.363 us | 28.465 us | 1.36x |
| `fallback/lookahead` | 5.467 us | 4.092 us | 16.999 us | 1.34x |
| `structural/keyword-family-to-capitalized-identifier` | 9.858 us | 7.775 us | 21.046 us | 1.27x |
| `lokad/docs/inline-doc-prefix` | 1.749 us | 1.418 us | 2.188 us | 1.23x |
| `structural/ordered-modifier-pair` | 9.928 us | 8.325 us | 21.027 us | 1.19x |
| `lokad/langserv/color-short-hex` | 0.568 us | 0.484 us | 0.671 us | 1.17x |
| `lokad/lexer/operator-run` | 1.039 us | 0.952 us | 1.388 us | 1.09x |
| `structural/modifier-family-to-type-window` | 58.810 us | 54.851 us | 68.920 us | 1.07x |
| `lokad/lexer/doc-line` | 1.074 us | 1.015 us | 1.792 us | 1.06x |
| `lokad/lexer/string` | 30.893 us | 29.570 us | 32.664 us | 1.04x |
| `structural/method-family-call` | 5.130 us | 4.967 us | 17.753 us | 1.03x |
| `lokad/style/range-ref` | 0.731 us | 0.718 us | 0.855 us | 1.02x |
| `structural/method-family-generic-call` | 4.452 us | 4.465 us | 17.311 us | 1.00x |
| `lokad/langserv/identifier-validator` | 4.131 us | 4.223 us | 5.709 us | 0.98x |
| `lokad/style/cell-ref` | 0.788 us | 0.816 us | 1.029 us | 0.97x |
| `lokad/langserv/url-download` | 11.485 us | 12.205 us | 12.633 us | 0.94x |
| `structural/keyword-to-generic-type` | 2.999 us | 3.422 us | 16.349 us | 0.88x |
| `lokad/style/hex-color` | 0.198 us | 0.227 us | 0.313 us | 0.87x |
| `lokad/langserv/helper-identifier` | 2.272 us | 2.638 us | 3.538 us | 0.86x |
| `structural/ordered-identifier-window` | 8.869 us | 10.358 us | 23.418 us | 0.86x |
| `literal-family/type-token-family` | 9.071 us | 11.231 us | 24.067 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.173 us | 4.064 us | 16.707 us | 0.78x |
| `lokad/lexer/number` | 2.868 us | 3.739 us | 4.876 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 0.576 us | 0.816 us | 1.088 us | 0.71x |
| `literal/identifier-token-casei` | 3.896 us | 5.871 us | 19.350 us | 0.66x |
| `literal-family/method-token-family` | 2.577 us | 5.025 us | 17.762 us | 0.51x |
| `literal/identifier-token` | 2.296 us | 4.604 us | 17.043 us | 0.50x |
| `literal/call-token` | 1.802 us | 3.864 us | 16.035 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.852 us | 10.465 us | 22.856 us | 0.27x |
| `lokad/folding/region-marker` | 5.527 us | 34.873 us | 60.742 us | 0.16x |
| `balancing/begin-end-blocks` | 46.535 us | 454.167 us | 484.899 us | 0.10x |
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
