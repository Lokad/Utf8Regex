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
| `literal/sherlock-zh` | 22.492 us | 18.854 us | 2,185.097 us | 1.19x |
| `literal/sherlock-ru` | 68.362 us | 96.453 us | 5,331.907 us | 0.71x |
| `literal-alternate/sherlock-zh` | 43.210 us | 67.225 us | 2,180.974 us | 0.64x |
| `ruff-noqa/tweaked` | 4,122.832 us | 7,859.236 us | 46,143.800 us | 0.52x |
| `bounded-repeat/letters-en` | 1,130.763 us | 2,836.878 us | 2,984.968 us | 0.40x |
| `literal/sherlock-casei-en` | 78.506 us | 199.125 us | 809.332 us | 0.39x |
| `literal/sherlock-casei-ru` | 143.879 us | 371.020 us | 5,730.029 us | 0.39x |
| `literal/sherlock-en` | 31.511 us | 84.444 us | 493.598 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,992.376 us | 5,865.754 us | 6,713.367 us | 0.34x |
| `dictionary/single` | 8,196.580 us | 25,846.633 us | 25,862.633 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,442.998 us | 4,844.806 us | 10,250.750 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 160.766 us | 594.221 us | 1,062.690 us | 0.27x |
| `bounded-repeat/capitals` | 11,937.043 us | 45,939.800 us | 49,679.750 us | 0.26x |
| `literal-alternate/sherlock-en` | 353.846 us | 1,475.543 us | 2,068.535 us | 0.24x |
| `aws-keys/quick` | 1,859.098 us | 9,444.878 us | 46,689.200 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 128.345 us | 670.772 us | 1,167.021 us | 0.19x |
| `aws-keys/full` | 8,675.489 us | 86,643.400 us | 116,574.300 us | 0.10x |
| `bounded-repeat/context` | 8,839.837 us | 147,334.200 us | 151,465.400 us | 0.06x |
| `ruff-noqa/real` | 3,455.527 us | 69,582.300 us | 100,546.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 847.767 us | 22,858.325 us | 28,155.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 104.361 us | 11,932.443 us | 12,778.133 us | 0.0087x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.086 us | 0.108 us | 0.144 us | 0.80x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.052 us | 0.077 us | 0.090 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.070 us | 0.107 us | 0.136 us | 0.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.084 us | 0.100 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,256.983 us | 1,949.005 us | 10,340.200 us | 0.64x |
| `common/ip-match` | `IsMatch` | 0.113 us | 0.178 us | 0.193 us | 0.63x |
| `industry/mariomka-email-count` | `Count` | 281.902 us | 492.281 us | 9,221.475 us | 0.57x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.082 us | 0.098 us | 0.53x |
| `common/uri-miss` | `IsMatch` | 0.095 us | 0.186 us | 0.206 us | 0.51x |
| `common/email-match` | `IsMatch` | 0.137 us | 0.270 us | 0.296 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 626.761 us | 1,317.907 us | 8,552.175 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.168 us | 0.186 us | 0.47x |
| `common/uri-match` | `IsMatch` | 0.098 us | 0.228 us | 0.267 us | 0.43x |
| `common/match-word` | `Match` | 0.051 us | 0.126 us | 1.165 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.279 us | 0.712 us | 0.728 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,197.475 us | 8,518.467 us | 17,121.467 us | 0.38x |
| `common/matches-set` | `Count` | 62.674 us | 190.798 us | 193.114 us | 0.33x |
| `common/matches-word` | `Count` | 1.236 us | 4.200 us | 6.004 us | 0.29x |
| `common/replace-words` | `Replace` | 20.588 us | 77.274 us | 79.247 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.637 us | 259.859 us | 500.351 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.108 us | 0.554 us | 0.599 us | 0.19x |
| `common/split-words` | `Split` | 13.948 us | 77.494 us | 79.235 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.741 us | 967.087 us | 1,282.065 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.001 us | 163.354 us | 165.270 us | 0.17x |
| `common/matches-words` | `Count` | 9.146 us | 72.170 us | 73.749 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,068.191 us | 9,777.700 us | 17,385.733 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,838.273 us | 19,423.650 us | 19,969.900 us | 0.09x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.619 us | 0.656 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.343 us | 0.372 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,651.591 us | 31,931.100 us | 39,468.950 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,033.730 us | 24,407.425 us | 31,360.800 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.854 us | 0.893 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 250.837 us | 8,426.320 us | 8,888.678 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 293.080 us | 11,960.314 us | 12,598.650 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 42.771 us | 35.770 us | 2,153.557 us | 1.20x |
| `literal-alternate/sherlock-en` | 353.689 us | 343.641 us | 905.156 us | 1.03x |
| `literal/sherlock-casei-en` | 78.183 us | 77.842 us | 508.672 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,993.974 us | 2,065.005 us | 2,950.812 us | 0.97x |
| `literal/sherlock-zh` | 22.273 us | 24.150 us | 2,185.200 us | 0.92x |
| `literal-alternate/sherlock-en-mixed` | 159.091 us | 174.947 us | 664.378 us | 0.91x |
| `ruff-noqa/tweaked` | 4,090.753 us | 4,584.488 us | 41,581.100 us | 0.89x |
| `bounded-repeat/capitals` | 11,992.686 us | 13,536.217 us | 16,503.725 us | 0.89x |
| `literal-alternate/sherlock-casei-en` | 103.825 us | 131.638 us | 767.745 us | 0.79x |
| `literal/sherlock-ru` | 68.028 us | 87.638 us | 5,256.120 us | 0.78x |
| `literal-alternate/sherlock-en-nomatch` | 127.763 us | 200.160 us | 688.933 us | 0.64x |
| `literal/sherlock-casei-ru` | 143.223 us | 226.953 us | 5,427.043 us | 0.63x |
| `bounded-repeat/letters-en` | 1,131.989 us | 1,830.814 us | 1,964.174 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,449.117 us | 2,344.653 us | 7,524.367 us | 0.62x |
| `aws-keys/quick` | 1,860.829 us | 3,594.400 us | 38,947.950 us | 0.52x |
| `aws-keys/full` | 8,704.289 us | 18,515.880 us | 50,549.633 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 859.145 us | 1,962.795 us | 7,336.275 us | 0.44x |
| `literal/sherlock-en` | 31.093 us | 74.639 us | 437.546 us | 0.42x |
| `dictionary/single` | 8,219.000 us | 45,825.000 us | 45,578.050 us | 0.18x |
| `ruff-noqa/real` | 3,429.045 us | 21,043.225 us | 53,685.367 us | 0.16x |
| `bounded-repeat/context` | 8,833.800 us | 68,140.250 us | 71,608.400 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.112 us | 0.047 us | 0.068 us | 2.37x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.042 us | 0.073 us | 1.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.031 us | 0.053 us | 1.55x |
| `common/ip-miss` | `IsMatch` | 0.267 us | 0.178 us | 0.206 us | 1.50x |
| `common/uri-miss` | `IsMatch` | 0.096 us | 0.068 us | 0.086 us | 1.41x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.077 us | 1.38x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.034 us | 0.053 us | 1.34x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.031 us | 0.048 us | 1.32x |
| `common/replace-words` | `Replace` | 20.759 us | 17.537 us | 19.183 us | 1.18x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.507 us | 57.908 us | 270.002 us | 1.15x |
| `industry/mariomka-uri-count` | `Count` | 1,230.948 us | 1,098.172 us | 8,445.625 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.097 us | 0.088 us | 0.121 us | 1.10x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.048 us | 0.084 us | 1.10x |
| `industry/leipzig-river-window-count` | `Count` | 1,663.343 us | 1,551.676 us | 8,130.225 us | 1.07x |
| `common/match-word` | `Match` | 0.051 us | 0.048 us | 0.936 us | 1.06x |
| `common/email-match` | `IsMatch` | 0.078 us | 0.081 us | 0.109 us | 0.97x |
| `industry/mariomka-ip-count` | `Count` | 3,186.608 us | 3,484.741 us | 11,025.500 us | 0.91x |
| `common/matches-words` | `Count` | 9.133 us | 11.040 us | 14.143 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 1,039.461 us | 1,269.369 us | 7,666.950 us | 0.82x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.043 us | 0.073 us | 0.76x |
| `common/split-words` | `Split` | 13.908 us | 20.635 us | 22.439 us | 0.67x |
| `industry/mariomka-email-count` | `Count` | 284.944 us | 463.521 us | 7,887.450 us | 0.61x |
| `common/matches-set` | `Count` | 62.594 us | 105.108 us | 106.253 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.108 us | 0.197 us | 0.234 us | 0.55x |
| `common/matches-word` | `Count` | 1.239 us | 2.369 us | 3.929 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 632.199 us | 1,298.983 us | 7,390.157 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,087.970 us | 2,982.258 us | 8,446.375 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.109 us | 0.130 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.708 us | 601.540 us | 860.429 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.323 us | 104.289 us | 106.236 us | 0.27x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.087 us | 0.116 us | 0.21x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,829.641 us | 8,791.911 us | 9,468.050 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 252.807 us | 3,959.400 us | 4,348.317 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 293.160 us | 4,833.700 us | 5,263.829 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.999 us | 6.310 us | 32.485 us | 0.95x |
| `fallback/lookahead` | 5.283 us | 6.072 us | 20.702 us | 0.87x |
| `lokad/langserv/color-short-hex` | 0.562 us | 0.817 us | 1.008 us | 0.69x |
| `structural/method-family-generic-call` | 4.410 us | 6.516 us | 21.335 us | 0.68x |
| `structural/ordered-keyword-window` | 12.110 us | 17.895 us | 32.665 us | 0.68x |
| `lokad/lexer/identifier` | 8.908 us | 13.672 us | 16.634 us | 0.65x |
| `structural/method-family-call` | 5.263 us | 8.092 us | 21.902 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.725 us | 7.442 us | 8.997 us | 0.63x |
| `structural/ordered-modifier-pair` | 10.385 us | 17.455 us | 32.421 us | 0.60x |
| `structural/keyword-to-generic-type` | 2.417 us | 4.073 us | 19.148 us | 0.59x |
| `lokad/lexer/doc-line` | 1.127 us | 1.912 us | 2.627 us | 0.59x |
| `lokad/docs/inline-doc-prefix` | 1.965 us | 3.457 us | 4.277 us | 0.57x |
| `lokad/langserv/helper-identifier` | 2.616 us | 4.785 us | 5.680 us | 0.55x |
| `structural/ordered-keyword-pair` | 2.900 us | 5.344 us | 20.493 us | 0.54x |
| `literal/identifier-token` | 2.244 us | 4.344 us | 18.670 us | 0.52x |
| `lokad/lexer/operator-run` | 1.135 us | 2.296 us | 2.769 us | 0.49x |
| `lokad/style/hex-color` | 0.231 us | 0.512 us | 0.599 us | 0.45x |
| `literal/call-token` | 1.847 us | 4.302 us | 18.616 us | 0.43x |
| `lokad/style/range-ref` | 0.775 us | 1.822 us | 1.991 us | 0.43x |
| `lokad/style/cell-ref` | 0.838 us | 1.991 us | 2.098 us | 0.42x |
| `lokad/lexer/string` | 33.495 us | 84.349 us | 88.824 us | 0.40x |
| `lokad/langserv/color-rgb-hex` | 0.706 us | 1.795 us | 2.028 us | 0.39x |
| `literal-family/type-token-family` | 9.251 us | 25.113 us | 40.178 us | 0.37x |
| `lokad/langserv/url-dashboard` | 8.986 us | 24.414 us | 24.862 us | 0.37x |
| `lokad/lexer/number` | 3.091 us | 10.807 us | 11.722 us | 0.29x |
| `structural/ordered-identifier-window` | 8.111 us | 28.438 us | 43.128 us | 0.29x |
| `literal-family/method-token-family` | 2.635 us | 9.340 us | 23.664 us | 0.28x |
| `literal/identifier-token-casei` | 3.935 us | 14.641 us | 31.354 us | 0.27x |
| `structural/modifier-family-to-type-window` | 60.965 us | 251.404 us | 268.609 us | 0.24x |
| `lokad/langserv/url-download` | 11.380 us | 51.709 us | 52.022 us | 0.22x |
| `lokad/folding/region-marker` | 5.757 us | 34.918 us | 63.084 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.973 us | 227.800 us | 246.872 us | 0.04x |
| `structural/modifier-family-to-type-pair` | 2.922 us | 129.524 us | 144.662 us | 0.02x |
| `balancing/begin-end-blocks` | 47.292 us | 4,258.578 us | 4,251.611 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.015 us | 6.271 us | 6.801 us | 1.44x |
| `structural/ordered-keyword-window` | 12.036 us | 8.637 us | 23.307 us | 1.39x |
| `lokad/docs/inline-doc-prefix` | 1.958 us | 1.447 us | 2.192 us | 1.35x |
| `lokad/imports/module-imports` | 5.950 us | 4.407 us | 29.537 us | 1.35x |
| `fallback/lookahead` | 5.284 us | 4.203 us | 18.513 us | 1.26x |
| `structural/keyword-family-to-capitalized-identifier` | 9.935 us | 7.988 us | 22.619 us | 1.24x |
| `structural/ordered-modifier-pair` | 10.507 us | 8.608 us | 23.293 us | 1.22x |
| `lokad/lexer/operator-run` | 1.118 us | 0.939 us | 1.394 us | 1.19x |
| `lokad/lexer/identifier` | 8.941 us | 7.841 us | 10.818 us | 1.14x |
| `lokad/langserv/identifier-validator` | 4.733 us | 4.205 us | 5.795 us | 1.13x |
| `lokad/lexer/string` | 33.559 us | 30.612 us | 33.420 us | 1.10x |
| `structural/modifier-family-to-type-window` | 60.923 us | 56.179 us | 71.587 us | 1.08x |
| `structural/method-family-call` | 5.321 us | 5.038 us | 18.698 us | 1.06x |
| `lokad/style/range-ref` | 0.779 us | 0.739 us | 0.880 us | 1.05x |
| `lokad/lexer/doc-line` | 1.124 us | 1.071 us | 1.809 us | 1.05x |
| `lokad/langserv/color-short-hex` | 0.496 us | 0.482 us | 0.671 us | 1.03x |
| `lokad/style/cell-ref` | 0.837 us | 0.841 us | 1.056 us | 0.99x |
| `lokad/langserv/helper-identifier` | 2.609 us | 2.655 us | 3.552 us | 0.98x |
| `structural/method-family-generic-call` | 4.436 us | 4.627 us | 18.344 us | 0.96x |
| `lokad/style/hex-color` | 0.213 us | 0.231 us | 0.313 us | 0.92x |
| `lokad/langserv/url-download` | 11.395 us | 12.559 us | 13.052 us | 0.91x |
| `literal-family/type-token-family` | 9.225 us | 11.423 us | 25.914 us | 0.81x |
| `lokad/lexer/number` | 3.111 us | 3.854 us | 4.839 us | 0.81x |
| `structural/ordered-identifier-window` | 8.146 us | 10.495 us | 25.294 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 0.615 us | 0.840 us | 1.096 us | 0.73x |
| `structural/ordered-keyword-pair` | 2.883 us | 4.141 us | 18.444 us | 0.70x |
| `structural/keyword-to-generic-type` | 2.431 us | 3.497 us | 17.982 us | 0.70x |
| `literal/identifier-token-casei` | 3.970 us | 5.900 us | 21.059 us | 0.67x |
| `literal/identifier-token` | 2.247 us | 4.293 us | 18.326 us | 0.52x |
| `literal-family/method-token-family` | 2.633 us | 5.298 us | 19.489 us | 0.50x |
| `literal/call-token` | 1.848 us | 3.941 us | 17.613 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.935 us | 10.641 us | 25.010 us | 0.28x |
| `lokad/folding/region-marker` | 5.754 us | 35.724 us | 62.586 us | 0.16x |
| `balancing/begin-end-blocks` | 47.897 us | 466.914 us | 501.166 us | 0.10x |
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
