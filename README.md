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
| `literal/sherlock-zh` | 21.978 us | 18.873 us | 2,214.939 us | 1.16x |
| `literal/sherlock-ru` | 70.634 us | 97.957 us | 5,157.007 us | 0.72x |
| `literal-alternate/sherlock-zh` | 43.235 us | 68.938 us | 2,162.558 us | 0.63x |
| `ruff-noqa/tweaked` | 3,937.430 us | 7,720.725 us | 45,391.900 us | 0.51x |
| `literal/sherlock-casei-en` | 79.333 us | 183.850 us | 573.337 us | 0.43x |
| `literal/sherlock-casei-ru` | 147.160 us | 352.480 us | 5,582.946 us | 0.42x |
| `bounded-repeat/letters-en` | 1,116.422 us | 2,792.661 us | 2,914.927 us | 0.40x |
| `literal/sherlock-en` | 31.456 us | 85.169 us | 441.103 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,108.597 us | 5,714.523 us | 6,618.550 us | 0.37x |
| `dictionary/single` | 8,093.450 us | 25,582.233 us | 25,432.133 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,422.092 us | 4,722.394 us | 9,676.050 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 165.110 us | 582.876 us | 998.564 us | 0.28x |
| `bounded-repeat/capitals` | 11,613.600 us | 45,640.300 us | 48,528.850 us | 0.25x |
| `literal-alternate/sherlock-en` | 346.609 us | 1,487.884 us | 1,977.616 us | 0.23x |
| `aws-keys/quick` | 1,990.855 us | 9,586.925 us | 46,234.150 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 125.828 us | 637.456 us | 1,046.184 us | 0.20x |
| `aws-keys/full` | 8,578.550 us | 85,224.700 us | 118,725.100 us | 0.10x |
| `bounded-repeat/context` | 8,867.133 us | 146,033.300 us | 149,575.700 us | 0.06x |
| `ruff-noqa/real` | 3,450.365 us | 68,402.850 us | 100,303.700 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 841.286 us | 23,082.150 us | 28,478.750 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 101.446 us | 10,978.671 us | 11,867.343 us | 0.0092x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 384.701 us | 498.785 us | 8,699.075 us | 0.77x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.053 us | 0.075 us | 0.088 us | 0.71x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.109 us | 0.145 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.084 us | 0.099 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,141.159 us | 1,835.340 us | 10,216.925 us | 0.62x |
| `common/date-match` | `IsMatch` | 0.064 us | 0.107 us | 0.134 us | 0.60x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.081 us | 0.096 us | 0.56x |
| `common/email-match` | `IsMatch` | 0.127 us | 0.262 us | 0.289 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 620.792 us | 1,303.910 us | 8,690.025 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.080 us | 0.182 us | 0.201 us | 0.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.073 us | 0.166 us | 0.183 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.175 us | 0.193 us | 0.42x |
| `common/match-word` | `Match` | 0.049 us | 0.125 us | 1.124 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,137.900 us | 8,094.580 us | 16,269.333 us | 0.39x |
| `common/matches-set` | `Count` | 65.934 us | 190.596 us | 192.174 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.075 us | 0.230 us | 0.269 us | 0.33x |
| `common/ip-miss` | `IsMatch` | 0.227 us | 0.715 us | 0.726 us | 0.32x |
| `common/matches-word` | `Count` | 1.209 us | 4.172 us | 5.882 us | 0.29x |
| `common/replace-words` | `Replace` | 19.657 us | 73.646 us | 74.718 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.179 us | 256.479 us | 488.916 us | 0.26x |
| `common/matches-boundary` | `Count` | 31.174 us | 160.969 us | 161.823 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.102 us | 0.560 us | 0.596 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.764 us | 951.600 us | 1,220.603 us | 0.17x |
| `common/split-words` | `Split` | 12.943 us | 76.347 us | 78.340 us | 0.17x |
| `common/matches-words` | `Count` | 8.972 us | 72.456 us | 74.481 us | 0.12x |
| `industry/leipzig-name-family-count` | `Count` | 1,071.451 us | 9,809.125 us | 16,900.167 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,899.653 us | 19,334.525 us | 19,954.525 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 863.574 us | 11,545.200 us | 12,312.371 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,636.693 us | 31,737.600 us | 38,943.700 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.596 us | 0.634 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.810 us | 0.847 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.340 us | 0.367 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 963.529 us | 23,424.350 us | 31,073.850 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 277.475 us | 8,210.560 us | 8,698.100 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.262 us | 35.835 us | 2,147.114 us | 1.21x |
| `bounded-repeat/letters-ru` | 2,133.753 us | 2,013.553 us | 2,868.085 us | 1.06x |
| `literal/sherlock-casei-en` | 79.282 us | 77.778 us | 463.734 us | 1.02x |
| `literal-alternate/sherlock-en` | 349.138 us | 362.867 us | 760.023 us | 0.96x |
| `literal-alternate/sherlock-en-mixed` | 165.992 us | 172.843 us | 568.921 us | 0.96x |
| `literal/sherlock-zh` | 22.160 us | 23.804 us | 2,163.122 us | 0.93x |
| `ruff-noqa/tweaked` | 4,003.205 us | 4,454.159 us | 40,193.150 us | 0.90x |
| `bounded-repeat/capitals` | 11,468.857 us | 13,283.967 us | 16,089.700 us | 0.86x |
| `literal-alternate/sherlock-casei-en` | 102.453 us | 130.650 us | 595.217 us | 0.78x |
| `literal/sherlock-ru` | 70.349 us | 89.916 us | 5,165.160 us | 0.78x |
| `literal/sherlock-casei-ru` | 147.384 us | 193.262 us | 5,414.121 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 126.439 us | 198.693 us | 601.892 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,419.325 us | 2,309.430 us | 7,210.625 us | 0.61x |
| `bounded-repeat/letters-en` | 1,109.813 us | 1,820.450 us | 1,931.977 us | 0.61x |
| `aws-keys/quick` | 1,978.371 us | 3,636.248 us | 38,873.200 us | 0.54x |
| `aws-keys/full` | 8,694.411 us | 18,348.440 us | 52,601.733 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 845.154 us | 1,949.469 us | 7,045.550 us | 0.43x |
| `literal/sherlock-en` | 31.402 us | 74.608 us | 430.253 us | 0.42x |
| `dictionary/single` | 8,062.900 us | 44,771.700 us | 44,809.150 us | 0.18x |
| `ruff-noqa/real` | 3,441.182 us | 20,194.875 us | 52,236.500 us | 0.17x |
| `bounded-repeat/context` | 8,844.356 us | 68,055.000 us | 70,967.100 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.069 us | 0.047 us | 0.067 us | 1.48x |
| `common/date-match` | `IsMatch` | 0.064 us | 0.043 us | 0.071 us | 1.47x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.045 us | 0.032 us | 0.051 us | 1.42x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.071 us | 0.055 us | 0.076 us | 1.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.044 us | 0.035 us | 0.051 us | 1.26x |
| `common/ip-miss` | `IsMatch` | 0.221 us | 0.179 us | 0.202 us | 1.23x |
| `common/uri-miss` | `IsMatch` | 0.079 us | 0.064 us | 0.088 us | 1.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.037 us | 0.031 us | 0.047 us | 1.20x |
| `common/replace-words` | `Replace` | 19.657 us | 16.904 us | 19.265 us | 1.16x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.022 us | 56.834 us | 262.524 us | 1.16x |
| `industry/mariomka-uri-count` | `Count` | 1,161.502 us | 1,087.607 us | 8,272.450 us | 1.07x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.050 us | 0.049 us | 0.085 us | 1.03x |
| `common/match-word` | `Match` | 0.049 us | 0.047 us | 0.923 us | 1.03x |
| `industry/leipzig-river-window-count` | `Count` | 1,657.104 us | 1,616.624 us | 7,835.675 us | 1.03x |
| `common/email-match` | `IsMatch` | 0.072 us | 0.078 us | 0.110 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,140.504 us | 3,391.239 us | 10,604.700 us | 0.93x |
| `common/backtracking` | `IsMatch` | 0.038 us | 0.043 us | 0.075 us | 0.87x |
| `common/uri-match` | `IsMatch` | 0.073 us | 0.087 us | 0.120 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 383.182 us | 465.604 us | 7,634.375 us | 0.82x |
| `common/matches-words` | `Count` | 8.900 us | 10.843 us | 13.814 us | 0.82x |
| `industry/leipzig-symbol-count` | `Count` | 963.676 us | 1,200.986 us | 7,237.350 us | 0.80x |
| `common/matches-set` | `Count` | 65.606 us | 103.138 us | 104.962 us | 0.64x |
| `common/split-words` | `Split` | 12.882 us | 20.253 us | 22.202 us | 0.64x |
| `common/matches-word` | `Count` | 1.197 us | 2.313 us | 3.354 us | 0.52x |
| `common/date-miss` | `IsMatch` | 0.101 us | 0.199 us | 0.235 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 622.202 us | 1,290.766 us | 6,979.350 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 1,065.530 us | 2,981.442 us | 8,363.950 us | 0.36x |
| `common/matches-boundary` | `Count` | 31.007 us | 102.353 us | 104.240 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.876 us | 592.114 us | 831.789 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.105 us | 0.125 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,907.354 us | 8,720.556 us | 9,318.411 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 862.466 us | 4,728.159 us | 5,190.213 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.088 us | 0.115 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 279.372 us | 3,903.020 us | 4,280.594 us | 0.07x |
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
| `lokad/imports/module-imports` | 5.886 us | 6.115 us | 30.696 us | 0.96x |
| `fallback/lookahead` | 5.023 us | 5.866 us | 18.890 us | 0.86x |
| `structural/keyword-to-generic-type` | 3.007 us | 4.025 us | 17.585 us | 0.75x |
| `structural/ordered-keyword-window` | 11.651 us | 17.618 us | 31.141 us | 0.66x |
| `structural/method-family-generic-call` | 4.195 us | 6.355 us | 19.771 us | 0.66x |
| `structural/method-family-call` | 5.048 us | 7.869 us | 20.604 us | 0.64x |
| `lokad/langserv/color-short-hex` | 0.497 us | 0.791 us | 0.979 us | 0.63x |
| `structural/ordered-keyword-pair` | 3.213 us | 5.170 us | 18.542 us | 0.62x |
| `lokad/lexer/identifier` | 8.095 us | 13.515 us | 16.128 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.251 us | 17.452 us | 30.509 us | 0.59x |
| `lokad/langserv/identifier-validator` | 4.161 us | 7.386 us | 8.813 us | 0.56x |
| `lokad/lexer/doc-line` | 1.048 us | 1.864 us | 2.570 us | 0.56x |
| `literal/identifier-token` | 2.315 us | 4.296 us | 17.661 us | 0.54x |
| `lokad/docs/inline-doc-prefix` | 1.725 us | 3.386 us | 4.130 us | 0.51x |
| `lokad/langserv/helper-identifier` | 2.297 us | 4.646 us | 5.565 us | 0.49x |
| `lokad/lexer/operator-run` | 1.040 us | 2.243 us | 2.705 us | 0.46x |
| `lokad/style/cell-ref` | 0.823 us | 1.872 us | 2.099 us | 0.44x |
| `literal/call-token` | 1.808 us | 4.169 us | 17.088 us | 0.43x |
| `lokad/style/hex-color` | 0.212 us | 0.497 us | 0.582 us | 0.43x |
| `lokad/style/range-ref` | 0.718 us | 1.803 us | 1.893 us | 0.40x |
| `lokad/langserv/url-dashboard` | 8.926 us | 22.776 us | 23.253 us | 0.39x |
| `lokad/lexer/string` | 30.301 us | 78.381 us | 83.568 us | 0.39x |
| `literal-family/type-token-family` | 9.050 us | 23.607 us | 37.047 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.639 us | 1.752 us | 1.982 us | 0.36x |
| `structural/ordered-identifier-window` | 8.198 us | 27.925 us | 40.808 us | 0.29x |
| `literal-family/method-token-family` | 2.564 us | 9.144 us | 21.738 us | 0.28x |
| `lokad/lexer/number` | 2.864 us | 10.786 us | 11.634 us | 0.27x |
| `literal/identifier-token-casei` | 3.843 us | 15.445 us | 27.879 us | 0.25x |
| `structural/modifier-family-to-type-window` | 57.985 us | 248.163 us | 260.817 us | 0.23x |
| `lokad/langserv/url-download` | 11.307 us | 50.239 us | 50.821 us | 0.23x |
| `lokad/folding/region-marker` | 5.603 us | 35.360 us | 61.250 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 11.143 us | 214.854 us | 229.288 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.820 us | 130.784 us | 145.345 us | 0.02x |
| `balancing/begin-end-blocks` | 45.616 us | 4,242.539 us | 4,226.300 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 8.940 us | 6.272 us | 6.679 us | 1.43x |
| `structural/keyword-family-to-capitalized-identifier` | 11.147 us | 7.851 us | 20.986 us | 1.42x |
| `lokad/imports/module-imports` | 5.895 us | 4.326 us | 27.851 us | 1.36x |
| `structural/ordered-keyword-window` | 11.615 us | 8.587 us | 21.687 us | 1.35x |
| `fallback/lookahead` | 5.077 us | 4.134 us | 16.647 us | 1.23x |
| `structural/ordered-modifier-pair` | 10.230 us | 8.452 us | 21.487 us | 1.21x |
| `lokad/docs/inline-doc-prefix` | 1.723 us | 1.425 us | 2.161 us | 1.21x |
| `lokad/lexer/operator-run` | 1.019 us | 0.925 us | 1.371 us | 1.10x |
| `structural/modifier-family-to-type-window` | 57.924 us | 54.452 us | 67.998 us | 1.06x |
| `lokad/lexer/identifier` | 8.064 us | 7.641 us | 10.510 us | 1.06x |
| `lokad/style/cell-ref` | 0.832 us | 0.804 us | 1.035 us | 1.04x |
| `structural/method-family-call` | 5.029 us | 4.922 us | 17.493 us | 1.02x |
| `lokad/lexer/string` | 30.312 us | 29.761 us | 32.476 us | 1.02x |
| `lokad/style/range-ref` | 0.723 us | 0.714 us | 0.837 us | 1.01x |
| `lokad/lexer/doc-line` | 1.034 us | 1.025 us | 1.807 us | 1.01x |
| `lokad/langserv/identifier-validator` | 4.196 us | 4.261 us | 5.697 us | 0.98x |
| `lokad/langserv/color-short-hex` | 0.456 us | 0.475 us | 0.667 us | 0.96x |
| `structural/method-family-generic-call` | 4.201 us | 4.459 us | 16.880 us | 0.94x |
| `lokad/langserv/url-download` | 11.224 us | 12.182 us | 12.827 us | 0.92x |
| `lokad/style/hex-color` | 0.197 us | 0.225 us | 0.306 us | 0.88x |
| `structural/keyword-to-generic-type` | 3.004 us | 3.462 us | 16.537 us | 0.87x |
| `lokad/langserv/helper-identifier` | 2.283 us | 2.698 us | 3.583 us | 0.85x |
| `literal-family/type-token-family` | 9.149 us | 11.343 us | 24.254 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.214 us | 4.055 us | 16.615 us | 0.79x |
| `structural/ordered-identifier-window` | 8.173 us | 10.413 us | 23.198 us | 0.78x |
| `lokad/lexer/number` | 2.868 us | 3.774 us | 4.911 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 0.568 us | 0.822 us | 1.078 us | 0.69x |
| `literal/identifier-token-casei` | 3.849 us | 5.750 us | 18.720 us | 0.67x |
| `literal-family/method-token-family` | 2.572 us | 4.992 us | 17.577 us | 0.52x |
| `literal/identifier-token` | 2.302 us | 4.599 us | 17.268 us | 0.50x |
| `literal/call-token` | 1.806 us | 3.890 us | 16.228 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 2.842 us | 10.495 us | 23.216 us | 0.27x |
| `lokad/folding/region-marker` | 5.496 us | 34.903 us | 61.031 us | 0.16x |
| `balancing/begin-end-blocks` | 46.293 us | 451.368 us | 484.101 us | 0.10x |
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
