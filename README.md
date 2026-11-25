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
| `literal/sherlock-zh` | 22.393 us | 18.941 us | 2,165.276 us | 1.18x |
| `literal/sherlock-ru` | 70.877 us | 99.073 us | 5,375.621 us | 0.72x |
| `literal-alternate/sherlock-zh` | 43.576 us | 67.577 us | 2,222.494 us | 0.64x |
| `ruff-noqa/tweaked` | 4,097.726 us | 7,580.664 us | 41,487.900 us | 0.54x |
| `literal/sherlock-casei-en` | 80.457 us | 189.778 us | 576.669 us | 0.42x |
| `literal/sherlock-casei-ru` | 150.563 us | 366.911 us | 5,607.608 us | 0.41x |
| `bounded-repeat/letters-en` | 1,145.888 us | 2,850.941 us | 3,004.040 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,174.297 us | 5,874.262 us | 6,768.125 us | 0.37x |
| `literal/sherlock-en` | 31.920 us | 88.192 us | 473.645 us | 0.36x |
| `dictionary/single` | 8,149.000 us | 25,530.667 us | 25,501.433 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,462.242 us | 4,775.012 us | 9,937.275 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 156.972 us | 596.736 us | 1,073.647 us | 0.26x |
| `bounded-repeat/capitals` | 11,791.114 us | 46,769.250 us | 50,726.950 us | 0.25x |
| `literal-alternate/sherlock-en` | 344.141 us | 1,478.953 us | 2,009.490 us | 0.23x |
| `aws-keys/quick` | 2,023.995 us | 9,468.925 us | 40,477.200 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 128.070 us | 650.229 us | 1,098.003 us | 0.20x |
| `aws-keys/full` | 8,818.300 us | 85,869.900 us | 119,495.700 us | 0.10x |
| `bounded-repeat/context` | 8,901.411 us | 148,154.100 us | 152,784.400 us | 0.06x |
| `ruff-noqa/real` | 3,449.395 us | 69,007.300 us | 102,782.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 874.399 us | 23,176.025 us | 28,469.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 105.598 us | 11,674.557 us | 12,515.783 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 382.530 us | 491.454 us | 8,933.950 us | 0.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.110 us | 0.149 us | 0.75x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.109 us | 0.137 us | 0.63x |
| `industry/mariomka-uri-count` | `Count` | 1,195.273 us | 1,910.917 us | 10,321.575 us | 0.63x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.077 us | 0.090 us | 0.59x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.046 us | 0.084 us | 0.101 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.134 us | 0.271 us | 0.297 us | 0.49x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.083 us | 0.098 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 630.104 us | 1,322.269 us | 8,303.800 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.074 us | 0.168 us | 0.184 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.075 us | 0.174 us | 0.193 us | 0.43x |
| `common/uri-miss` | `IsMatch` | 0.081 us | 0.202 us | 0.220 us | 0.40x |
| `common/match-word` | `Match` | 0.049 us | 0.127 us | 1.129 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,161.112 us | 8,526.400 us | 17,056.533 us | 0.37x |
| `common/matches-set` | `Count` | 66.163 us | 194.312 us | 194.441 us | 0.34x |
| `common/uri-match` | `IsMatch` | 0.079 us | 0.234 us | 0.271 us | 0.34x |
| `common/ip-miss` | `IsMatch` | 0.222 us | 0.715 us | 0.736 us | 0.31x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.060 us | 257.203 us | 484.764 us | 0.30x |
| `common/matches-word` | `Count` | 1.245 us | 4.267 us | 5.964 us | 0.29x |
| `common/replace-words` | `Replace` | 20.525 us | 80.036 us | 81.229 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.108 us | 0.568 us | 0.598 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.763 us | 961.647 us | 1,233.000 us | 0.17x |
| `common/split-words` | `Split` | 13.238 us | 77.567 us | 79.352 us | 0.17x |
| `common/matches-boundary` | `Count` | 25.151 us | 163.775 us | 166.442 us | 0.15x |
| `common/matches-words` | `Count` | 9.129 us | 71.115 us | 72.297 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,082.213 us | 9,646.650 us | 16,742.933 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,930.235 us | 19,329.850 us | 19,929.525 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 847.061 us | 12,068.014 us | 12,458.883 us | 0.07x |
| `common/email-miss` | `IsMatch` | 0.020 us | 0.344 us | 0.373 us | 0.06x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.617 us | 0.647 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.842 us | 0.881 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,020.978 us | 23,471.950 us | 31,269.650 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,243.013 us | 35,094.200 us | 39,366.250 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 262.758 us | 8,127.870 us | 8,665.844 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.322 us | 36.346 us | 2,180.874 us | 1.19x |
| `bounded-repeat/letters-ru` | 2,181.643 us | 2,061.973 us | 2,951.300 us | 1.06x |
| `literal-alternate/sherlock-en` | 346.322 us | 341.634 us | 764.399 us | 1.01x |
| `literal/sherlock-casei-en` | 80.578 us | 80.017 us | 428.315 us | 1.01x |
| `literal/sherlock-zh` | 22.494 us | 24.161 us | 2,188.269 us | 0.93x |
| `ruff-noqa/tweaked` | 4,057.622 us | 4,536.165 us | 36,212.350 us | 0.89x |
| `literal-alternate/sherlock-en-mixed` | 157.814 us | 180.399 us | 612.147 us | 0.87x |
| `bounded-repeat/capitals` | 11,764.143 us | 13,615.517 us | 16,598.225 us | 0.86x |
| `literal/sherlock-ru` | 71.049 us | 91.002 us | 5,313.900 us | 0.78x |
| `literal-alternate/sherlock-casei-en` | 105.786 us | 143.416 us | 569.098 us | 0.74x |
| `literal/sherlock-casei-ru` | 152.653 us | 222.630 us | 5,377.980 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 128.038 us | 202.561 us | 590.128 us | 0.63x |
| `bounded-repeat/letters-en` | 1,147.444 us | 1,840.176 us | 1,970.328 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,464.148 us | 2,360.506 us | 7,458.000 us | 0.62x |
| `aws-keys/quick` | 2,033.608 us | 3,647.276 us | 35,444.433 us | 0.56x |
| `aws-keys/full` | 8,854.011 us | 18,298.080 us | 53,115.133 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 871.627 us | 1,979.437 us | 7,160.450 us | 0.44x |
| `literal/sherlock-en` | 31.985 us | 76.799 us | 422.725 us | 0.42x |
| `dictionary/single` | 8,175.290 us | 45,474.000 us | 45,657.450 us | 0.18x |
| `ruff-noqa/real` | 3,438.268 us | 20,526.725 us | 54,931.833 us | 0.17x |
| `bounded-repeat/context` | 8,924.633 us | 69,172.050 us | 72,574.050 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.136 us | 0.079 us | 0.107 us | 1.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.083 us | 0.049 us | 0.088 us | 1.69x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.042 us | 0.072 us | 1.63x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.047 us | 0.067 us | 1.53x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.043 us | 0.031 us | 0.050 us | 1.37x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 75.992 us | 57.146 us | 262.839 us | 1.33x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.072 us | 0.056 us | 0.081 us | 1.29x |
| `common/uri-miss` | `IsMatch` | 0.081 us | 0.063 us | 0.087 us | 1.27x |
| `common/ip-miss` | `IsMatch` | 0.214 us | 0.179 us | 0.203 us | 1.20x |
| `common/replace-words` | `Replace` | 20.504 us | 17.418 us | 18.582 us | 1.18x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.040 us | 0.034 us | 0.051 us | 1.17x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.035 us | 0.031 us | 0.048 us | 1.14x |
| `industry/mariomka-uri-count` | `Count` | 1,196.911 us | 1,095.167 us | 8,277.575 us | 1.09x |
| `common/match-word` | `Match` | 0.050 us | 0.047 us | 0.915 us | 1.05x |
| `industry/mariomka-ip-count` | `Count` | 3,157.800 us | 3,443.250 us | 10,894.300 us | 0.92x |
| `common/uri-match` | `IsMatch` | 0.079 us | 0.087 us | 0.122 us | 0.91x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.043 us | 0.074 us | 0.86x |
| `industry/mariomka-email-count` | `Count` | 380.743 us | 459.081 us | 7,642.350 us | 0.83x |
| `common/matches-words` | `Count` | 9.171 us | 11.082 us | 14.179 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 1,024.435 us | 1,248.502 us | 7,487.740 us | 0.82x |
| `industry/leipzig-river-window-count` | `Count` | 1,247.798 us | 1,544.698 us | 7,745.650 us | 0.81x |
| `common/matches-set` | `Count` | 66.622 us | 104.611 us | 106.538 us | 0.64x |
| `common/split-words` | `Split` | 13.234 us | 21.118 us | 22.773 us | 0.63x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.200 us | 0.236 us | 0.54x |
| `common/matches-word` | `Count` | 1.245 us | 2.366 us | 3.445 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 632.114 us | 1,304.031 us | 7,086.889 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 1,087.107 us | 3,020.204 us | 7,926.175 us | 0.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.155 us | 595.623 us | 856.336 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.107 us | 0.130 us | 0.27x |
| `common/matches-boundary` | `Count` | 25.336 us | 105.201 us | 106.567 us | 0.24x |
| `common/email-miss` | `IsMatch` | 0.020 us | 0.088 us | 0.116 us | 0.22x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,917.074 us | 8,804.956 us | 9,304.900 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 845.033 us | 4,711.788 us | 5,133.620 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 261.765 us | 3,952.605 us | 4,352.178 us | 0.07x |
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
| `lokad/imports/module-imports` | 6.750 us | 6.465 us | 31.395 us | 1.04x |
| `fallback/lookahead` | 5.501 us | 6.008 us | 19.047 us | 0.92x |
| `structural/method-family-generic-call` | 5.899 us | 6.474 us | 20.332 us | 0.91x |
| `lokad/lexer/doc-line` | 1.552 us | 1.886 us | 2.632 us | 0.82x |
| `lokad/lexer/identifier` | 11.010 us | 13.429 us | 16.351 us | 0.82x |
| `lokad/langserv/identifier-validator` | 6.098 us | 7.498 us | 8.994 us | 0.81x |
| `structural/method-family-call` | 6.441 us | 8.075 us | 21.103 us | 0.80x |
| `lokad/langserv/helper-identifier` | 3.764 us | 4.763 us | 5.630 us | 0.79x |
| `structural/ordered-keyword-window` | 13.269 us | 17.344 us | 30.666 us | 0.77x |
| `structural/keyword-to-generic-type` | 3.040 us | 4.172 us | 17.846 us | 0.73x |
| `lokad/docs/inline-doc-prefix` | 2.353 us | 3.423 us | 4.226 us | 0.69x |
| `structural/ordered-keyword-pair` | 3.232 us | 5.234 us | 18.902 us | 0.62x |
| `lokad/langserv/color-short-hex` | 0.498 us | 0.828 us | 0.992 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.057 us | 17.507 us | 30.545 us | 0.57x |
| `literal/identifier-token` | 2.336 us | 4.353 us | 17.291 us | 0.54x |
| `lokad/lexer/operator-run` | 1.039 us | 2.326 us | 2.768 us | 0.45x |
| `lokad/langserv/url-dashboard` | 10.199 us | 23.212 us | 23.710 us | 0.44x |
| `lokad/style/range-ref` | 0.810 us | 1.846 us | 1.962 us | 0.44x |
| `literal/call-token` | 1.846 us | 4.262 us | 17.279 us | 0.43x |
| `lokad/style/cell-ref` | 0.786 us | 1.884 us | 2.143 us | 0.42x |
| `lokad/style/hex-color` | 0.213 us | 0.516 us | 0.601 us | 0.41x |
| `literal-family/type-token-family` | 9.217 us | 23.885 us | 37.903 us | 0.39x |
| `lokad/lexer/string` | 30.469 us | 81.043 us | 85.371 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.646 us | 1.784 us | 2.023 us | 0.36x |
| `structural/ordered-identifier-window` | 8.506 us | 28.478 us | 41.514 us | 0.30x |
| `literal-family/method-token-family` | 2.616 us | 9.209 us | 21.886 us | 0.28x |
| `lokad/lexer/number` | 2.821 us | 10.905 us | 12.051 us | 0.26x |
| `structural/modifier-family-to-type-window` | 64.574 us | 253.914 us | 267.553 us | 0.25x |
| `literal/identifier-token-casei` | 3.440 us | 14.844 us | 28.461 us | 0.23x |
| `lokad/langserv/url-download` | 11.741 us | 53.154 us | 53.065 us | 0.22x |
| `lokad/folding/region-marker` | 5.134 us | 35.458 us | 61.810 us | 0.14x |
| `structural/keyword-family-to-capitalized-identifier` | 11.017 us | 211.997 us | 227.564 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.911 us | 130.562 us | 144.501 us | 0.02x |
| `balancing/begin-end-blocks` | 46.616 us | 4,091.611 us | 4,085.626 us | 0.01x |
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
| `lokad/docs/inline-doc-prefix` | 2.347 us | 1.414 us | 2.174 us | 1.66x |
| `lokad/langserv/url-dashboard` | 10.197 us | 6.321 us | 6.791 us | 1.61x |
| `structural/ordered-keyword-window` | 13.364 us | 8.500 us | 21.561 us | 1.57x |
| `lokad/imports/module-imports` | 6.744 us | 4.375 us | 28.537 us | 1.54x |
| `lokad/lexer/doc-line` | 1.548 us | 1.046 us | 1.809 us | 1.48x |
| `lokad/langserv/identifier-validator` | 6.131 us | 4.229 us | 5.697 us | 1.45x |
| `lokad/lexer/identifier` | 11.010 us | 7.788 us | 10.499 us | 1.41x |
| `structural/keyword-family-to-capitalized-identifier` | 10.964 us | 7.883 us | 21.249 us | 1.39x |
| `lokad/langserv/helper-identifier` | 3.743 us | 2.719 us | 3.568 us | 1.38x |
| `fallback/lookahead` | 5.511 us | 4.181 us | 16.836 us | 1.32x |
| `structural/method-family-generic-call` | 5.922 us | 4.524 us | 17.370 us | 1.31x |
| `structural/method-family-call` | 6.441 us | 5.017 us | 17.891 us | 1.28x |
| `structural/ordered-modifier-pair` | 10.181 us | 8.466 us | 21.370 us | 1.20x |
| `structural/modifier-family-to-type-window` | 64.943 us | 56.195 us | 70.048 us | 1.16x |
| `lokad/style/range-ref` | 0.809 us | 0.736 us | 0.868 us | 1.10x |
| `lokad/lexer/operator-run` | 1.017 us | 0.941 us | 1.380 us | 1.08x |
| `lokad/lexer/string` | 30.617 us | 30.701 us | 33.000 us | 1.00x |
| `lokad/langserv/url-download` | 11.913 us | 12.173 us | 12.527 us | 0.98x |
| `lokad/langserv/color-short-hex` | 0.451 us | 0.479 us | 0.667 us | 0.94x |
| `lokad/style/cell-ref` | 0.786 us | 0.834 us | 1.060 us | 0.94x |
| `structural/keyword-to-generic-type` | 3.039 us | 3.496 us | 16.644 us | 0.87x |
| `lokad/style/hex-color` | 0.194 us | 0.232 us | 0.313 us | 0.84x |
| `literal-family/type-token-family` | 9.169 us | 11.467 us | 24.447 us | 0.80x |
| `structural/ordered-identifier-window` | 8.564 us | 10.723 us | 23.743 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.249 us | 4.100 us | 17.020 us | 0.79x |
| `lokad/lexer/number` | 2.829 us | 3.778 us | 4.846 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 0.571 us | 0.830 us | 1.092 us | 0.69x |
| `literal/identifier-token-casei` | 3.437 us | 5.820 us | 19.377 us | 0.59x |
| `literal-family/method-token-family` | 2.603 us | 5.050 us | 17.761 us | 0.52x |
| `literal/identifier-token` | 2.332 us | 4.678 us | 16.752 us | 0.50x |
| `literal/call-token` | 1.840 us | 3.935 us | 16.302 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.913 us | 10.642 us | 23.579 us | 0.27x |
| `lokad/folding/region-marker` | 5.020 us | 35.830 us | 60.885 us | 0.14x |
| `balancing/begin-end-blocks` | 46.932 us | 463.659 us | 492.339 us | 0.10x |
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
