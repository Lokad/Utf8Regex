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
| `literal/sherlock-zh` | 21.252 us | 18.001 us | 2,119.903 us | 1.18x |
| `literal/sherlock-ru` | 66.564 us | 94.344 us | 5,105.627 us | 0.71x |
| `literal-alternate/sherlock-zh` | 42.174 us | 66.286 us | 2,135.495 us | 0.64x |
| `ruff-noqa/tweaked` | 3,965.365 us | 7,412.564 us | 40,891.750 us | 0.53x |
| `literal/sherlock-casei-en` | 76.364 us | 177.718 us | 587.453 us | 0.43x |
| `literal/sherlock-casei-ru` | 142.848 us | 351.201 us | 5,398.207 us | 0.41x |
| `bounded-repeat/letters-en` | 1,062.990 us | 2,689.761 us | 2,828.054 us | 0.40x |
| `literal/sherlock-en` | 30.296 us | 82.567 us | 470.494 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,999.411 us | 5,539.229 us | 6,421.167 us | 0.36x |
| `dictionary/single` | 7,818.290 us | 24,858.150 us | 24,937.225 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,378.204 us | 4,629.653 us | 9,784.675 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 164.426 us | 575.131 us | 981.306 us | 0.29x |
| `bounded-repeat/capitals` | 11,293.200 us | 44,408.150 us | 47,719.700 us | 0.25x |
| `literal-alternate/sherlock-en` | 333.877 us | 1,432.140 us | 2,003.371 us | 0.23x |
| `aws-keys/quick` | 1,838.293 us | 9,368.487 us | 38,339.900 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 123.119 us | 642.266 us | 1,058.302 us | 0.19x |
| `aws-keys/full` | 8,333.900 us | 82,685.300 us | 114,366.900 us | 0.10x |
| `bounded-repeat/context` | 8,319.544 us | 141,314.200 us | 144,622.000 us | 0.06x |
| `ruff-noqa/real` | 3,408.100 us | 67,423.200 us | 98,915.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 831.120 us | 22,799.050 us | 28,438.150 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 98.088 us | 11,161.929 us | 11,976.057 us | 0.0088x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 377.327 us | 491.367 us | 8,610.975 us | 0.77x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.056 us | 0.073 us | 0.087 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.074 us | 0.105 us | 0.142 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.081 us | 0.096 us | 0.68x |
| `industry/mariomka-uri-count` | `Count` | 1,117.569 us | 1,837.926 us | 9,943.700 us | 0.61x |
| `common/date-match` | `IsMatch` | 0.062 us | 0.106 us | 0.134 us | 0.58x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.079 us | 0.094 us | 0.55x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.084 us | 0.158 us | 0.175 us | 0.53x |
| `common/email-match` | `IsMatch` | 0.123 us | 0.255 us | 0.280 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 609.787 us | 1,282.610 us | 8,460.211 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.077 us | 0.181 us | 0.196 us | 0.43x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.169 us | 0.191 us | 0.42x |
| `common/match-word` | `Match` | 0.048 us | 0.123 us | 1.137 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,067.264 us | 8,236.630 us | 16,514.900 us | 0.37x |
| `common/matches-set` | `Count` | 64.084 us | 183.332 us | 186.625 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.074 us | 0.223 us | 0.257 us | 0.33x |
| `common/ip-miss` | `IsMatch` | 0.216 us | 0.708 us | 0.719 us | 0.30x |
| `common/matches-word` | `Count` | 1.192 us | 4.077 us | 5.810 us | 0.29x |
| `common/replace-words` | `Replace` | 19.696 us | 72.137 us | 74.977 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 63.684 us | 251.497 us | 477.054 us | 0.25x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.534 us | 0.562 us | 0.18x |
| `common/matches-boundary` | `Count` | 27.769 us | 155.782 us | 157.555 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.805 us | 935.361 us | 1,184.682 us | 0.17x |
| `common/split-words` | `Split` | 12.722 us | 74.509 us | 76.539 us | 0.17x |
| `common/matches-words` | `Count` | 8.783 us | 67.510 us | 69.564 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,029.008 us | 9,219.067 us | 16,227.833 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,756.588 us | 17,674.820 us | 18,121.580 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 828.066 us | 11,294.086 us | 11,841.857 us | 0.07x |
| `common/email-miss` | `IsMatch` | 0.020 us | 0.335 us | 0.365 us | 0.06x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.590 us | 0.625 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,579.360 us | 30,465.600 us | 37,462.150 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 936.050 us | 21,474.025 us | 28,471.750 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.797 us | 0.823 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 269.886 us | 7,842.960 us | 8,353.478 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 41.900 us | 34.724 us | 2,090.742 us | 1.21x |
| `literal/sherlock-casei-en` | 76.397 us | 74.474 us | 409.911 us | 1.03x |
| `bounded-repeat/letters-ru` | 2,000.403 us | 1,970.379 us | 2,760.193 us | 1.02x |
| `literal-alternate/sherlock-en-mixed` | 164.546 us | 172.388 us | 533.339 us | 0.95x |
| `literal-alternate/sherlock-en` | 334.467 us | 358.763 us | 743.231 us | 0.93x |
| `literal/sherlock-zh` | 21.173 us | 22.739 us | 2,115.700 us | 0.93x |
| `ruff-noqa/tweaked` | 3,990.440 us | 4,425.044 us | 47,749.150 us | 0.90x |
| `bounded-repeat/capitals` | 11,283.186 us | 12,954.817 us | 16,024.525 us | 0.87x |
| `literal/sherlock-ru` | 66.396 us | 86.238 us | 5,033.053 us | 0.77x |
| `literal/sherlock-casei-ru` | 142.986 us | 189.433 us | 5,089.040 us | 0.75x |
| `literal-alternate/sherlock-casei-en` | 97.869 us | 139.066 us | 556.831 us | 0.70x |
| `literal-alternate/sherlock-en-nomatch` | 123.251 us | 198.356 us | 562.614 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,389.315 us | 2,258.471 us | 7,282.600 us | 0.62x |
| `bounded-repeat/letters-en` | 1,064.648 us | 1,748.742 us | 1,879.930 us | 0.61x |
| `aws-keys/quick` | 1,854.307 us | 3,486.195 us | 33,841.300 us | 0.53x |
| `aws-keys/full` | 8,218.270 us | 17,458.960 us | 50,815.500 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 832.022 us | 1,926.897 us | 7,177.500 us | 0.43x |
| `literal/sherlock-en` | 30.669 us | 73.069 us | 449.929 us | 0.42x |
| `dictionary/single` | 7,838.840 us | 44,019.950 us | 43,784.300 us | 0.18x |
| `ruff-noqa/real` | 3,380.065 us | 19,733.150 us | 49,841.400 us | 0.17x |
| `bounded-repeat/context` | 8,312.056 us | 65,697.350 us | 68,569.700 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.081 us | 0.053 us | 0.074 us | 1.53x |
| `common/date-match` | `IsMatch` | 0.062 us | 0.042 us | 0.070 us | 1.48x |
| `common/ip-match` | `IsMatch` | 0.067 us | 0.046 us | 0.066 us | 1.47x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.046 us | 0.032 us | 0.050 us | 1.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.047 us | 0.034 us | 0.051 us | 1.40x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.037 us | 0.030 us | 0.045 us | 1.23x |
| `common/ip-miss` | `IsMatch` | 0.210 us | 0.172 us | 0.196 us | 1.22x |
| `common/uri-miss` | `IsMatch` | 0.076 us | 0.063 us | 0.084 us | 1.20x |
| `common/replace-words` | `Replace` | 19.541 us | 16.697 us | 19.375 us | 1.17x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 63.155 us | 56.970 us | 256.562 us | 1.11x |
| `industry/leipzig-river-window-count` | `Count` | 1,586.317 us | 1,462.156 us | 7,914.025 us | 1.08x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.050 us | 0.047 us | 0.083 us | 1.07x |
| `industry/mariomka-uri-count` | `Count` | 1,120.209 us | 1,053.044 us | 8,167.300 us | 1.06x |
| `common/match-word` | `Match` | 0.049 us | 0.046 us | 0.923 us | 1.05x |
| `common/email-match` | `IsMatch` | 0.073 us | 0.077 us | 0.103 us | 0.94x |
| `industry/mariomka-ip-count` | `Count` | 3,063.252 us | 3,305.413 us | 10,407.367 us | 0.93x |
| `common/uri-match` | `IsMatch` | 0.071 us | 0.083 us | 0.116 us | 0.86x |
| `common/matches-words` | `Count` | 8.785 us | 10.471 us | 13.423 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 379.215 us | 458.762 us | 7,354.175 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 944.640 us | 1,158.577 us | 6,844.600 us | 0.82x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.042 us | 0.071 us | 0.76x |
| `common/matches-set` | `Count` | 63.851 us | 100.862 us | 102.067 us | 0.63x |
| `common/split-words` | `Split` | 12.702 us | 20.198 us | 22.155 us | 0.63x |
| `common/matches-word` | `Count` | 1.194 us | 2.251 us | 3.302 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.191 us | 0.227 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 614.226 us | 1,248.531 us | 7,295.460 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,042.282 us | 2,878.907 us | 7,429.350 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.104 us | 0.124 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.938 us | 572.197 us | 812.183 us | 0.28x |
| `common/matches-boundary` | `Count` | 27.892 us | 100.966 us | 102.636 us | 0.28x |
| `common/email-miss` | `IsMatch` | 0.020 us | 0.086 us | 0.112 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,759.012 us | 8,471.922 us | 8,911.667 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 823.741 us | 4,582.035 us | 4,944.069 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 264.228 us | 3,778.745 us | 4,151.142 us | 0.07x |
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
| `lokad/imports/module-imports` | 5.718 us | 6.137 us | 29.604 us | 0.93x |
| `fallback/lookahead` | 4.935 us | 5.752 us | 18.519 us | 0.86x |
| `lokad/lexer/identifier` | 9.988 us | 13.154 us | 16.046 us | 0.76x |
| `structural/keyword-to-generic-type` | 2.883 us | 3.901 us | 17.120 us | 0.74x |
| `structural/ordered-keyword-window` | 11.140 us | 16.781 us | 29.878 us | 0.66x |
| `structural/method-family-call` | 4.951 us | 7.638 us | 20.284 us | 0.65x |
| `lokad/langserv/color-short-hex` | 0.505 us | 0.787 us | 0.955 us | 0.64x |
| `structural/method-family-generic-call` | 4.015 us | 6.376 us | 19.506 us | 0.63x |
| `structural/ordered-keyword-pair` | 3.133 us | 5.019 us | 17.879 us | 0.62x |
| `structural/ordered-modifier-pair` | 9.925 us | 16.721 us | 29.060 us | 0.59x |
| `lokad/langserv/identifier-validator` | 4.156 us | 7.119 us | 8.649 us | 0.58x |
| `lokad/lexer/doc-line` | 1.000 us | 1.828 us | 2.536 us | 0.55x |
| `lokad/docs/inline-doc-prefix` | 1.768 us | 3.324 us | 4.068 us | 0.53x |
| `literal/identifier-token` | 2.242 us | 4.232 us | 16.580 us | 0.53x |
| `lokad/langserv/helper-identifier` | 2.237 us | 4.548 us | 5.452 us | 0.49x |
| `lokad/lexer/operator-run` | 1.032 us | 2.253 us | 2.705 us | 0.46x |
| `literal/call-token` | 1.797 us | 3.986 us | 16.480 us | 0.45x |
| `lokad/style/hex-color` | 0.207 us | 0.476 us | 0.556 us | 0.44x |
| `lokad/style/cell-ref` | 0.816 us | 1.890 us | 2.033 us | 0.43x |
| `lokad/style/range-ref` | 0.717 us | 1.745 us | 1.905 us | 0.41x |
| `literal-family/type-token-family` | 8.852 us | 22.784 us | 36.214 us | 0.39x |
| `lokad/langserv/url-dashboard` | 8.609 us | 22.259 us | 22.530 us | 0.39x |
| `lokad/lexer/string` | 29.652 us | 77.295 us | 81.708 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.626 us | 1.725 us | 1.966 us | 0.36x |
| `structural/ordered-identifier-window` | 7.969 us | 27.499 us | 40.146 us | 0.29x |
| `literal-family/method-token-family` | 2.539 us | 9.068 us | 21.653 us | 0.28x |
| `lokad/lexer/number` | 2.798 us | 10.313 us | 11.302 us | 0.27x |
| `literal/identifier-token-casei` | 3.740 us | 14.732 us | 27.081 us | 0.25x |
| `structural/modifier-family-to-type-window` | 56.264 us | 244.946 us | 255.680 us | 0.23x |
| `lokad/langserv/url-download` | 10.899 us | 48.983 us | 49.074 us | 0.22x |
| `lokad/folding/region-marker` | 5.601 us | 34.689 us | 60.686 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.704 us | 210.046 us | 223.653 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.804 us | 128.509 us | 139.961 us | 0.02x |
| `balancing/begin-end-blocks` | 44.962 us | 4,037.284 us | 4,030.179 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 8.559 us | 6.007 us | 6.490 us | 1.42x |
| `structural/ordered-keyword-window` | 11.066 us | 8.172 us | 20.894 us | 1.35x |
| `lokad/imports/module-imports` | 5.722 us | 4.266 us | 27.407 us | 1.34x |
| `lokad/lexer/identifier` | 10.017 us | 7.567 us | 10.281 us | 1.32x |
| `structural/keyword-family-to-capitalized-identifier` | 9.690 us | 7.602 us | 20.582 us | 1.27x |
| `lokad/docs/inline-doc-prefix` | 1.762 us | 1.387 us | 2.113 us | 1.27x |
| `fallback/lookahead` | 4.974 us | 4.025 us | 16.462 us | 1.24x |
| `structural/ordered-modifier-pair` | 9.928 us | 8.224 us | 20.808 us | 1.21x |
| `lokad/lexer/operator-run` | 1.002 us | 0.883 us | 1.361 us | 1.14x |
| `structural/modifier-family-to-type-window` | 56.155 us | 53.680 us | 67.011 us | 1.05x |
| `lokad/style/cell-ref` | 0.819 us | 0.794 us | 0.996 us | 1.03x |
| `lokad/langserv/identifier-validator` | 4.141 us | 4.024 us | 5.537 us | 1.03x |
| `structural/method-family-call` | 4.929 us | 4.826 us | 17.035 us | 1.02x |
| `lokad/style/range-ref` | 0.716 us | 0.703 us | 0.847 us | 1.02x |
| `lokad/lexer/string` | 29.534 us | 29.048 us | 31.341 us | 1.02x |
| `lokad/lexer/doc-line` | 0.997 us | 1.017 us | 1.753 us | 0.98x |
| `lokad/langserv/color-short-hex` | 0.446 us | 0.463 us | 0.646 us | 0.96x |
| `lokad/langserv/url-download` | 10.866 us | 11.666 us | 12.138 us | 0.93x |
| `structural/method-family-generic-call` | 4.055 us | 4.378 us | 16.731 us | 0.93x |
| `lokad/style/hex-color` | 0.191 us | 0.220 us | 0.299 us | 0.87x |
| `lokad/langserv/helper-identifier` | 2.216 us | 2.595 us | 3.457 us | 0.85x |
| `structural/keyword-to-generic-type` | 2.878 us | 3.382 us | 16.265 us | 0.85x |
| `literal-family/type-token-family` | 8.815 us | 11.009 us | 23.566 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.131 us | 3.940 us | 16.118 us | 0.79x |
| `structural/ordered-identifier-window` | 7.921 us | 10.175 us | 22.996 us | 0.78x |
| `lokad/lexer/number` | 2.813 us | 3.703 us | 4.665 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 0.554 us | 0.808 us | 1.066 us | 0.69x |
| `literal/identifier-token-casei` | 3.747 us | 5.643 us | 18.395 us | 0.66x |
| `literal-family/method-token-family` | 2.552 us | 4.926 us | 17.471 us | 0.52x |
| `literal/identifier-token` | 2.264 us | 4.509 us | 16.297 us | 0.50x |
| `literal/call-token` | 1.775 us | 3.768 us | 15.684 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.798 us | 10.327 us | 22.671 us | 0.27x |
| `lokad/folding/region-marker` | 5.586 us | 34.376 us | 60.309 us | 0.16x |
| `balancing/begin-end-blocks` | 45.112 us | 445.919 us | 476.220 us | 0.10x |
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
