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
| `literal/sherlock-zh` | 22.367 us | 18.906 us | 2,216.828 us | 1.18x |
| `literal/sherlock-ru` | 70.046 us | 99.081 us | 5,386.536 us | 0.71x |
| `literal-alternate/sherlock-zh` | 43.096 us | 67.957 us | 2,181.576 us | 0.63x |
| `ruff-noqa/tweaked` | 3,999.632 us | 7,425.055 us | 45,126.450 us | 0.54x |
| `literal/sherlock-casei-en` | 86.365 us | 187.661 us | 687.831 us | 0.46x |
| `literal/sherlock-casei-ru` | 149.919 us | 359.992 us | 5,591.079 us | 0.42x |
| `bounded-repeat/letters-en` | 1,120.281 us | 2,804.686 us | 2,997.732 us | 0.40x |
| `literal/sherlock-en` | 31.637 us | 85.359 us | 476.309 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,080.892 us | 5,784.136 us | 6,629.033 us | 0.36x |
| `dictionary/single` | 8,171.500 us | 26,067.900 us | 26,037.033 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,414.211 us | 4,768.031 us | 10,034.750 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 167.879 us | 591.021 us | 1,084.142 us | 0.28x |
| `bounded-repeat/capitals` | 11,757.514 us | 45,120.750 us | 48,168.200 us | 0.26x |
| `literal-alternate/sherlock-en` | 343.167 us | 1,465.492 us | 2,035.558 us | 0.23x |
| `aws-keys/quick` | 1,924.838 us | 9,427.300 us | 41,169.650 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 132.443 us | 671.546 us | 1,132.896 us | 0.20x |
| `aws-keys/full` | 8,416.044 us | 85,769.900 us | 116,313.700 us | 0.10x |
| `bounded-repeat/context` | 8,978.133 us | 146,314.300 us | 150,026.600 us | 0.06x |
| `ruff-noqa/real` | 3,445.336 us | 68,774.200 us | 85,986.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 855.213 us | 23,165.075 us | 28,431.450 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 101.987 us | 11,220.914 us | 12,224.714 us | 0.0091x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.086 us | 0.110 us | 0.143 us | 0.78x |
| `industry/mariomka-uri-count` | `Count` | 1,210.516 us | 1,821.693 us | 10,392.933 us | 0.66x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.076 us | 0.090 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.070 us | 0.108 us | 0.138 us | 0.65x |
| `common/ip-match` | `IsMatch` | 0.109 us | 0.169 us | 0.191 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.084 us | 0.099 us | 0.62x |
| `industry/mariomka-email-count` | `Count` | 272.746 us | 478.154 us | 8,757.150 us | 0.57x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.082 us | 0.098 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.137 us | 0.267 us | 0.291 us | 0.51x |
| `common/uri-miss` | `IsMatch` | 0.092 us | 0.185 us | 0.202 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 626.443 us | 1,308.243 us | 8,481.233 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.166 us | 0.183 us | 0.47x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.224 us | 0.261 us | 0.41x |
| `common/match-word` | `Match` | 0.051 us | 0.125 us | 1.131 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.277 us | 0.724 us | 0.753 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,112.640 us | 8,512.278 us | 16,786.333 us | 0.37x |
| `common/matches-set` | `Count` | 62.127 us | 191.967 us | 192.928 us | 0.32x |
| `common/matches-word` | `Count` | 1.207 us | 4.174 us | 5.901 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 69.776 us | 258.828 us | 502.692 us | 0.27x |
| `common/replace-words` | `Replace` | 20.238 us | 78.060 us | 79.808 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.107 us | 0.582 us | 0.599 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.772 us | 966.119 us | 1,264.959 us | 0.17x |
| `common/split-words` | `Split` | 13.229 us | 77.345 us | 79.303 us | 0.17x |
| `common/matches-boundary` | `Count` | 26.638 us | 161.521 us | 163.856 us | 0.16x |
| `common/matches-words` | `Count` | 9.080 us | 70.384 us | 72.067 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,051.481 us | 9,778.650 us | 16,731.300 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,916.168 us | 19,443.125 us | 20,035.275 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.609 us | 0.637 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.347 us | 0.369 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,649.998 us | 31,367.100 us | 38,363.150 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,004.682 us | 22,008.050 us | 28,903.350 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.828 us | 0.868 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 259.077 us | 8,109.380 us | 8,637.289 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 292.342 us | 11,664.829 us | 12,436.229 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.052 us | 35.644 us | 2,185.233 us | 1.21x |
| `literal/sherlock-casei-en` | 85.829 us | 77.525 us | 450.478 us | 1.11x |
| `bounded-repeat/letters-ru` | 2,091.361 us | 2,043.916 us | 2,902.865 us | 1.02x |
| `literal-alternate/sherlock-en` | 343.805 us | 341.180 us | 854.696 us | 1.01x |
| `literal-alternate/sherlock-en-mixed` | 167.071 us | 173.896 us | 613.647 us | 0.96x |
| `literal/sherlock-zh` | 22.380 us | 23.955 us | 2,184.040 us | 0.93x |
| `ruff-noqa/tweaked` | 4,046.179 us | 4,502.629 us | 39,754.400 us | 0.90x |
| `bounded-repeat/capitals` | 11,848.043 us | 13,344.733 us | 16,360.375 us | 0.89x |
| `literal/sherlock-casei-ru` | 150.099 us | 193.242 us | 5,434.067 us | 0.78x |
| `literal/sherlock-ru` | 68.927 us | 89.289 us | 5,390.520 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 103.143 us | 143.561 us | 746.145 us | 0.72x |
| `literal-alternate/sherlock-en-nomatch` | 132.541 us | 203.823 us | 595.846 us | 0.65x |
| `bounded-repeat/letters-en` | 1,124.611 us | 1,818.343 us | 1,993.558 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,421.396 us | 2,312.918 us | 7,477.267 us | 0.61x |
| `aws-keys/quick` | 1,914.290 us | 3,578.568 us | 35,746.533 us | 0.53x |
| `aws-keys/full` | 8,682.489 us | 18,347.000 us | 50,380.467 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 855.924 us | 1,967.641 us | 7,269.375 us | 0.44x |
| `literal/sherlock-en` | 31.344 us | 75.396 us | 408.323 us | 0.42x |
| `dictionary/single` | 8,110.230 us | 45,324.450 us | 45,464.300 us | 0.18x |
| `ruff-noqa/real` | 3,492.809 us | 20,643.175 us | 34,079.433 us | 0.17x |
| `bounded-repeat/context` | 9,018.888 us | 68,145.850 us | 71,944.000 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.106 us | 0.047 us | 0.066 us | 2.26x |
| `common/date-match` | `IsMatch` | 0.070 us | 0.042 us | 0.072 us | 1.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.031 us | 0.049 us | 1.55x |
| `common/ip-miss` | `IsMatch` | 0.270 us | 0.178 us | 0.201 us | 1.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.054 us | 0.076 us | 1.40x |
| `common/uri-miss` | `IsMatch` | 0.091 us | 0.066 us | 0.086 us | 1.38x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.031 us | 0.047 us | 1.28x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.036 us | 0.052 us | 1.24x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 69.400 us | 57.663 us | 279.940 us | 1.20x |
| `common/replace-words` | `Replace` | 20.406 us | 17.517 us | 20.102 us | 1.16x |
| `industry/mariomka-uri-count` | `Count` | 1,211.911 us | 1,097.971 us | 8,308.533 us | 1.10x |
| `industry/leipzig-river-window-count` | `Count` | 1,656.124 us | 1,514.898 us | 7,502.450 us | 1.09x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.052 us | 0.049 us | 0.086 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.088 us | 0.118 us | 1.06x |
| `common/email-match` | `IsMatch` | 0.077 us | 0.079 us | 0.106 us | 0.97x |
| `industry/mariomka-ip-count` | `Count` | 3,088.096 us | 3,442.665 us | 10,736.567 us | 0.90x |
| `industry/leipzig-symbol-count` | `Count` | 1,009.457 us | 1,187.606 us | 7,740.722 us | 0.85x |
| `common/matches-words` | `Count` | 9.012 us | 10.653 us | 13.791 us | 0.85x |
| `common/match-word` | `Match` | 0.049 us | 0.060 us | 0.933 us | 0.82x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.043 us | 0.072 us | 0.73x |
| `common/split-words` | `Split` | 13.180 us | 20.920 us | 23.099 us | 0.63x |
| `industry/mariomka-email-count` | `Count` | 273.773 us | 447.398 us | 7,459.075 us | 0.61x |
| `common/matches-set` | `Count` | 62.113 us | 104.215 us | 106.229 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.106 us | 0.196 us | 0.234 us | 0.54x |
| `common/matches-word` | `Count` | 1.212 us | 2.312 us | 3.488 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 628.772 us | 1,301.103 us | 7,305.460 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 1,054.449 us | 3,000.831 us | 8,052.100 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.035 us | 0.105 us | 0.127 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.633 us | 596.202 us | 860.661 us | 0.28x |
| `common/matches-boundary` | `Count` | 26.967 us | 105.016 us | 106.686 us | 0.26x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,926.478 us | 8,759.011 us | 9,328.038 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.089 us | 0.116 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 260.935 us | 3,925.455 us | 4,345.983 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 290.098 us | 4,755.150 us | 5,147.020 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.936 us | 6.374 us | 31.993 us | 0.93x |
| `fallback/lookahead` | 5.283 us | 5.862 us | 20.383 us | 0.90x |
| `lokad/langserv/color-short-hex` | 0.551 us | 0.812 us | 0.994 us | 0.68x |
| `structural/method-family-generic-call` | 4.366 us | 6.527 us | 20.987 us | 0.67x |
| `structural/ordered-keyword-window` | 11.711 us | 17.516 us | 31.787 us | 0.67x |
| `lokad/lexer/identifier` | 8.778 us | 13.379 us | 16.251 us | 0.66x |
| `structural/method-family-call` | 5.203 us | 8.052 us | 22.077 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.699 us | 7.380 us | 8.925 us | 0.64x |
| `lokad/lexer/doc-line` | 1.140 us | 1.857 us | 2.556 us | 0.61x |
| `structural/ordered-modifier-pair` | 10.434 us | 17.218 us | 31.702 us | 0.61x |
| `structural/keyword-to-generic-type` | 2.418 us | 4.055 us | 19.164 us | 0.60x |
| `lokad/docs/inline-doc-prefix` | 1.963 us | 3.437 us | 4.182 us | 0.57x |
| `lokad/langserv/helper-identifier` | 2.603 us | 4.661 us | 5.549 us | 0.56x |
| `literal/identifier-token` | 2.249 us | 4.319 us | 18.612 us | 0.52x |
| `structural/ordered-keyword-pair` | 2.777 us | 5.384 us | 20.052 us | 0.52x |
| `lokad/lexer/operator-run` | 1.139 us | 2.273 us | 2.760 us | 0.50x |
| `lokad/style/hex-color` | 0.228 us | 0.510 us | 0.591 us | 0.45x |
| `lokad/style/cell-ref` | 0.835 us | 1.900 us | 2.075 us | 0.44x |
| `literal/call-token` | 1.839 us | 4.284 us | 18.398 us | 0.43x |
| `lokad/style/range-ref` | 0.787 us | 1.852 us | 1.936 us | 0.42x |
| `lokad/langserv/color-rgb-hex` | 0.698 us | 1.768 us | 2.018 us | 0.39x |
| `lokad/lexer/string` | 31.968 us | 81.109 us | 86.128 us | 0.39x |
| `literal-family/type-token-family` | 9.184 us | 23.699 us | 38.931 us | 0.39x |
| `lokad/langserv/url-dashboard` | 8.937 us | 23.127 us | 23.466 us | 0.39x |
| `lokad/lexer/number` | 3.114 us | 10.793 us | 11.709 us | 0.29x |
| `literal-family/method-token-family` | 2.591 us | 9.001 us | 23.496 us | 0.29x |
| `structural/ordered-identifier-window` | 8.101 us | 28.191 us | 42.580 us | 0.29x |
| `literal/identifier-token-casei` | 3.877 us | 15.376 us | 29.575 us | 0.25x |
| `structural/modifier-family-to-type-window` | 59.304 us | 253.142 us | 269.039 us | 0.23x |
| `lokad/langserv/url-download` | 11.076 us | 48.396 us | 49.139 us | 0.23x |
| `lokad/folding/region-marker` | 5.558 us | 35.162 us | 63.881 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.840 us | 219.045 us | 236.419 us | 0.04x |
| `structural/modifier-family-to-type-pair` | 2.886 us | 133.145 us | 146.401 us | 0.02x |
| `balancing/begin-end-blocks` | 46.390 us | 4,220.347 us | 4,243.400 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 8.958 us | 6.256 us | 6.853 us | 1.43x |
| `lokad/docs/inline-doc-prefix` | 1.971 us | 1.417 us | 2.170 us | 1.39x |
| `structural/ordered-keyword-window` | 11.744 us | 8.519 us | 22.610 us | 1.38x |
| `lokad/imports/module-imports` | 5.845 us | 4.397 us | 29.226 us | 1.33x |
| `structural/keyword-family-to-capitalized-identifier` | 9.810 us | 7.551 us | 21.998 us | 1.30x |
| `fallback/lookahead` | 5.296 us | 4.122 us | 18.023 us | 1.28x |
| `structural/ordered-modifier-pair` | 10.369 us | 8.608 us | 22.658 us | 1.20x |
| `lokad/lexer/operator-run` | 1.104 us | 0.928 us | 1.376 us | 1.19x |
| `lokad/lexer/identifier` | 8.749 us | 7.552 us | 10.518 us | 1.16x |
| `lokad/langserv/identifier-validator` | 4.678 us | 4.137 us | 5.683 us | 1.13x |
| `lokad/style/range-ref` | 0.785 us | 0.723 us | 0.863 us | 1.09x |
| `structural/modifier-family-to-type-window` | 59.358 us | 55.275 us | 70.790 us | 1.07x |
| `lokad/lexer/string` | 31.847 us | 30.241 us | 33.133 us | 1.05x |
| `structural/method-family-call` | 5.231 us | 5.001 us | 18.671 us | 1.05x |
| `lokad/langserv/color-short-hex` | 0.510 us | 0.493 us | 0.678 us | 1.03x |
| `lokad/lexer/doc-line` | 1.117 us | 1.098 us | 1.889 us | 1.02x |
| `lokad/style/cell-ref` | 0.837 us | 0.828 us | 1.045 us | 1.01x |
| `structural/method-family-generic-call` | 4.374 us | 4.465 us | 18.005 us | 0.98x |
| `lokad/langserv/helper-identifier` | 2.583 us | 2.667 us | 3.521 us | 0.97x |
| `lokad/style/hex-color` | 0.210 us | 0.226 us | 0.311 us | 0.93x |
| `lokad/langserv/url-download` | 11.102 us | 11.994 us | 12.579 us | 0.93x |
| `lokad/lexer/number` | 3.085 us | 3.782 us | 4.913 us | 0.82x |
| `literal-family/type-token-family` | 9.182 us | 11.373 us | 25.661 us | 0.81x |
| `structural/ordered-identifier-window` | 8.155 us | 10.513 us | 25.148 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 0.611 us | 0.831 us | 1.080 us | 0.73x |
| `structural/keyword-to-generic-type` | 2.416 us | 3.435 us | 17.900 us | 0.70x |
| `literal/identifier-token-casei` | 3.877 us | 5.535 us | 20.644 us | 0.70x |
| `structural/ordered-keyword-pair` | 2.786 us | 4.077 us | 18.231 us | 0.68x |
| `literal-family/method-token-family` | 2.605 us | 5.264 us | 19.329 us | 0.49x |
| `literal/identifier-token` | 2.251 us | 4.641 us | 18.326 us | 0.48x |
| `literal/call-token` | 1.844 us | 3.903 us | 17.353 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.882 us | 10.687 us | 24.663 us | 0.27x |
| `lokad/folding/region-marker` | 5.542 us | 35.917 us | 64.894 us | 0.15x |
| `balancing/begin-end-blocks` | 46.920 us | 458.146 us | 496.513 us | 0.10x |
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
