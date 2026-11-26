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
| `literal/sherlock-zh` | 21.622 us | 17.950 us | 2,140.566 us | 1.20x |
| `literal/sherlock-ru` | 65.437 us | 95.675 us | 5,082.800 us | 0.68x |
| `literal-alternate/sherlock-zh` | 42.053 us | 68.303 us | 2,117.983 us | 0.62x |
| `ruff-noqa/tweaked` | 3,798.005 us | 7,534.591 us | 37,954.950 us | 0.50x |
| `literal/sherlock-casei-ru` | 142.160 us | 350.747 us | 5,458.221 us | 0.41x |
| `literal/sherlock-casei-en` | 76.700 us | 192.808 us | 620.759 us | 0.40x |
| `bounded-repeat/letters-en` | 1,070.865 us | 2,714.764 us | 2,863.333 us | 0.39x |
| `literal/sherlock-en` | 30.979 us | 84.449 us | 467.462 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,954.190 us | 5,627.771 us | 6,443.500 us | 0.35x |
| `dictionary/single` | 7,896.490 us | 25,011.867 us | 25,087.133 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,381.355 us | 4,593.418 us | 9,680.075 us | 0.30x |
| `bounded-repeat/capitals` | 11,426.471 us | 43,589.000 us | 47,212.700 us | 0.26x |
| `literal-alternate/sherlock-en-mixed` | 156.168 us | 600.963 us | 1,085.786 us | 0.26x |
| `literal-alternate/sherlock-en` | 325.875 us | 1,438.908 us | 1,995.993 us | 0.23x |
| `aws-keys/quick` | 1,855.778 us | 9,283.925 us | 44,137.050 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 122.757 us | 645.442 us | 1,247.273 us | 0.19x |
| `aws-keys/full` | 8,364.089 us | 83,132.000 us | 114,496.800 us | 0.10x |
| `bounded-repeat/context` | 8,641.311 us | 141,900.600 us | 146,186.100 us | 0.06x |
| `ruff-noqa/real` | 3,376.730 us | 71,253.000 us | 100,120.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 824.733 us | 22,386.450 us | 27,769.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 101.653 us | 10,956.914 us | 11,777.200 us | 0.0093x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.108 us | 0.104 us | 0.131 us | 1.04x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.083 us | 0.106 us | 0.142 us | 0.79x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.072 us | 0.087 us | 0.69x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.106 us | 0.160 us | 0.177 us | 0.66x |
| `industry/mariomka-email-count` | `Count` | 313.659 us | 480.345 us | 8,456.411 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.080 us | 0.096 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,155.397 us | 1,799.498 us | 10,022.200 us | 0.64x |
| `common/ip-match` | `IsMatch` | 0.107 us | 0.166 us | 0.188 us | 0.64x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.077 us | 0.092 us | 0.58x |
| `common/email-match` | `IsMatch` | 0.135 us | 0.256 us | 0.283 us | 0.53x |
| `common/uri-miss` | `IsMatch` | 0.091 us | 0.183 us | 0.202 us | 0.50x |
| `industry/leipzig-twain-count` | `Count` | 605.115 us | 1,268.103 us | 8,407.280 us | 0.48x |
| `common/ip-miss` | `IsMatch` | 0.274 us | 0.685 us | 0.712 us | 0.40x |
| `common/match-word` | `Match` | 0.049 us | 0.123 us | 1.159 us | 0.40x |
| `industry/mariomka-ip-count` | `Count` | 3,078.438 us | 8,010.070 us | 16,382.400 us | 0.38x |
| `common/uri-match` | `IsMatch` | 0.083 us | 0.225 us | 0.262 us | 0.37x |
| `common/matches-set` | `Count` | 60.561 us | 189.672 us | 193.128 us | 0.32x |
| `common/matches-word` | `Count` | 1.213 us | 4.130 us | 5.849 us | 0.29x |
| `common/date-miss` | `IsMatch` | 0.154 us | 0.550 us | 0.579 us | 0.28x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.355 us | 247.342 us | 475.076 us | 0.27x |
| `common/replace-words` | `Replace` | 19.644 us | 73.506 us | 75.400 us | 0.27x |
| `common/split-words` | `Split` | 13.358 us | 74.584 us | 75.819 us | 0.18x |
| `common/matches-boundary` | `Count` | 27.855 us | 157.362 us | 158.970 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.020 us | 926.700 us | 1,203.706 us | 0.17x |
| `common/matches-words` | `Count` | 8.884 us | 67.689 us | 69.660 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,054.990 us | 9,594.225 us | 16,552.875 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,885.573 us | 18,711.380 us | 19,457.900 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.023 us | 0.331 us | 0.354 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.034 us | 0.586 us | 0.621 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,619.958 us | 31,045.600 us | 37,897.850 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 994.714 us | 21,954.025 us | 29,699.150 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.810 us | 0.851 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 241.549 us | 7,870.080 us | 8,307.156 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 279.931 us | 11,437.486 us | 12,140.214 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 41.766 us | 35.094 us | 2,046.997 us | 1.19x |
| `literal/sherlock-casei-en` | 77.012 us | 76.078 us | 474.896 us | 1.01x |
| `literal-alternate/sherlock-en` | 325.374 us | 330.333 us | 811.611 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,952.131 us | 2,057.421 us | 2,891.815 us | 0.95x |
| `literal/sherlock-zh` | 21.660 us | 23.087 us | 2,115.170 us | 0.94x |
| `literal-alternate/sherlock-en-mixed` | 155.693 us | 171.364 us | 602.748 us | 0.91x |
| `ruff-noqa/tweaked` | 3,817.635 us | 4,380.571 us | 35,273.200 us | 0.87x |
| `bounded-repeat/capitals` | 11,355.229 us | 13,043.417 us | 16,013.625 us | 0.87x |
| `literal/sherlock-ru` | 65.002 us | 85.494 us | 5,076.880 us | 0.76x |
| `literal/sherlock-casei-ru` | 141.256 us | 188.422 us | 5,281.820 us | 0.75x |
| `literal-alternate/sherlock-casei-en` | 102.601 us | 142.814 us | 709.686 us | 0.72x |
| `literal-alternate/sherlock-en-nomatch` | 123.237 us | 193.602 us | 574.396 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,378.066 us | 2,256.056 us | 7,167.283 us | 0.61x |
| `bounded-repeat/letters-en` | 1,075.197 us | 1,763.005 us | 1,911.905 us | 0.61x |
| `aws-keys/quick` | 1,871.768 us | 3,512.509 us | 36,356.450 us | 0.53x |
| `aws-keys/full` | 8,419.822 us | 17,716.500 us | 51,450.733 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 829.425 us | 1,903.940 us | 6,896.400 us | 0.44x |
| `literal/sherlock-en` | 30.793 us | 73.991 us | 451.132 us | 0.42x |
| `dictionary/single` | 7,942.620 us | 43,941.950 us | 43,717.800 us | 0.18x |
| `ruff-noqa/real` | 3,361.622 us | 20,273.850 us | 51,083.800 us | 0.17x |
| `bounded-repeat/context` | 8,696.400 us | 66,612.550 us | 69,581.850 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.108 us | 0.041 us | 0.069 us | 2.62x |
| `common/ip-match` | `IsMatch` | 0.100 us | 0.046 us | 0.067 us | 2.18x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.101 us | 0.053 us | 0.073 us | 1.92x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.031 us | 0.048 us | 1.58x |
| `common/ip-miss` | `IsMatch` | 0.265 us | 0.170 us | 0.197 us | 1.56x |
| `common/uri-miss` | `IsMatch` | 0.094 us | 0.065 us | 0.087 us | 1.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.034 us | 0.051 us | 1.36x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.030 us | 0.047 us | 1.36x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.132 us | 55.574 us | 286.897 us | 1.19x |
| `common/replace-words` | `Replace` | 19.686 us | 16.766 us | 19.824 us | 1.17x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.047 us | 0.086 us | 1.13x |
| `industry/mariomka-uri-count` | `Count` | 1,167.756 us | 1,071.740 us | 8,128.600 us | 1.09x |
| `industry/leipzig-river-window-count` | `Count` | 1,624.089 us | 1,559.551 us | 7,890.000 us | 1.04x |
| `common/match-word` | `Match` | 0.049 us | 0.047 us | 0.923 us | 1.03x |
| `common/uri-match` | `IsMatch` | 0.084 us | 0.084 us | 0.115 us | 1.01x |
| `common/email-match` | `IsMatch` | 0.076 us | 0.077 us | 0.104 us | 0.98x |
| `industry/mariomka-ip-count` | `Count` | 3,088.316 us | 3,335.439 us | 10,657.333 us | 0.93x |
| `common/matches-words` | `Count` | 8.873 us | 10.519 us | 13.576 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,004.781 us | 1,218.421 us | 7,277.300 us | 0.82x |
| `common/date-miss` | `IsMatch` | 0.154 us | 0.196 us | 0.229 us | 0.78x |
| `common/backtracking` | `IsMatch` | 0.030 us | 0.042 us | 0.071 us | 0.72x |
| `industry/mariomka-email-count` | `Count` | 314.821 us | 447.559 us | 7,542.900 us | 0.70x |
| `common/split-words` | `Split` | 13.343 us | 19.819 us | 21.648 us | 0.67x |
| `common/matches-set` | `Count` | 60.447 us | 101.356 us | 103.056 us | 0.60x |
| `common/matches-word` | `Count` | 1.198 us | 2.290 us | 3.609 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 608.837 us | 1,254.649 us | 6,589.233 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,053.476 us | 2,941.627 us | 8,763.987 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.033 us | 0.103 us | 0.124 us | 0.32x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.657 us | 575.371 us | 831.092 us | 0.29x |
| `common/matches-boundary` | `Count` | 28.058 us | 100.565 us | 102.661 us | 0.28x |
| `common/email-miss` | `IsMatch` | 0.021 us | 0.086 us | 0.111 us | 0.24x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,878.505 us | 8,463.267 us | 9,022.933 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 241.644 us | 3,846.360 us | 4,195.295 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 278.066 us | 4,584.453 us | 5,043.740 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.975 us | 6.151 us | 31.107 us | 0.97x |
| `fallback/lookahead` | 5.022 us | 5.794 us | 20.012 us | 0.87x |
| `structural/keyword-to-generic-type` | 2.942 us | 3.945 us | 18.460 us | 0.75x |
| `lokad/langserv/color-short-hex` | 0.551 us | 0.788 us | 0.967 us | 0.70x |
| `structural/ordered-keyword-window` | 11.333 us | 16.926 us | 31.061 us | 0.67x |
| `structural/method-family-generic-call` | 4.162 us | 6.259 us | 20.308 us | 0.66x |
| `structural/method-family-call` | 5.050 us | 7.766 us | 21.523 us | 0.65x |
| `lokad/lexer/identifier` | 8.165 us | 13.112 us | 16.062 us | 0.62x |
| `structural/ordered-keyword-pair` | 3.149 us | 5.108 us | 19.652 us | 0.62x |
| `lokad/langserv/identifier-validator` | 4.319 us | 7.256 us | 8.821 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.023 us | 17.050 us | 31.243 us | 0.59x |
| `lokad/lexer/doc-line` | 1.051 us | 1.814 us | 2.539 us | 0.58x |
| `lokad/docs/inline-doc-prefix` | 1.884 us | 3.336 us | 4.108 us | 0.56x |
| `literal/identifier-token` | 2.154 us | 4.147 us | 17.913 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.343 us | 4.732 us | 5.462 us | 0.50x |
| `lokad/lexer/operator-run` | 1.106 us | 2.266 us | 2.692 us | 0.49x |
| `lokad/style/hex-color` | 0.233 us | 0.489 us | 0.562 us | 0.48x |
| `lokad/style/range-ref` | 0.763 us | 1.762 us | 1.910 us | 0.43x |
| `lokad/style/cell-ref` | 0.784 us | 1.824 us | 2.000 us | 0.43x |
| `literal/call-token` | 1.774 us | 4.167 us | 18.014 us | 0.43x |
| `lokad/lexer/string` | 32.682 us | 78.492 us | 82.710 us | 0.42x |
| `literal-family/type-token-family` | 8.836 us | 22.541 us | 37.826 us | 0.39x |
| `lokad/langserv/url-dashboard` | 8.592 us | 22.165 us | 22.600 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.643 us | 1.742 us | 1.958 us | 0.37x |
| `lokad/lexer/number` | 2.968 us | 10.404 us | 11.274 us | 0.29x |
| `structural/ordered-identifier-window` | 7.830 us | 27.528 us | 41.822 us | 0.28x |
| `literal-family/method-token-family` | 2.519 us | 9.041 us | 22.871 us | 0.28x |
| `literal/identifier-token-casei` | 3.733 us | 14.483 us | 28.651 us | 0.26x |
| `structural/modifier-family-to-type-window` | 57.242 us | 244.048 us | 261.353 us | 0.23x |
| `lokad/langserv/url-download` | 10.990 us | 47.476 us | 48.103 us | 0.23x |
| `lokad/folding/region-marker` | 5.654 us | 35.394 us | 63.748 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.476 us | 205.226 us | 222.193 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.832 us | 127.520 us | 141.855 us | 0.02x |
| `balancing/begin-end-blocks` | 45.261 us | 4,004.000 us | 3,998.600 us | 0.01x |
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
| `lokad/imports/module-imports` | 5.990 us | 4.223 us | 28.542 us | 1.42x |
| `lokad/langserv/url-dashboard` | 8.630 us | 6.236 us | 6.579 us | 1.38x |
| `structural/ordered-keyword-window` | 11.337 us | 8.284 us | 22.006 us | 1.37x |
| `structural/keyword-family-to-capitalized-identifier` | 10.009 us | 7.370 us | 21.355 us | 1.36x |
| `lokad/docs/inline-doc-prefix` | 1.879 us | 1.397 us | 2.080 us | 1.34x |
| `fallback/lookahead` | 5.027 us | 4.071 us | 17.833 us | 1.23x |
| `structural/ordered-modifier-pair` | 10.061 us | 8.309 us | 22.371 us | 1.21x |
| `lokad/lexer/operator-run` | 1.081 us | 0.916 us | 1.349 us | 1.18x |
| `lokad/lexer/string` | 32.595 us | 29.490 us | 32.343 us | 1.11x |
| `lokad/langserv/identifier-validator` | 4.322 us | 4.043 us | 5.617 us | 1.07x |
| `lokad/style/range-ref` | 0.759 us | 0.715 us | 0.846 us | 1.06x |
| `structural/modifier-family-to-type-window` | 57.483 us | 54.292 us | 70.235 us | 1.06x |
| `lokad/lexer/doc-line` | 1.052 us | 1.017 us | 1.774 us | 1.03x |
| `lokad/lexer/identifier` | 7.981 us | 7.723 us | 10.305 us | 1.03x |
| `structural/method-family-call` | 5.025 us | 4.886 us | 18.348 us | 1.03x |
| `lokad/langserv/color-short-hex` | 0.470 us | 0.469 us | 0.653 us | 1.00x |
| `lokad/style/cell-ref` | 0.785 us | 0.793 us | 1.016 us | 0.99x |
| `lokad/langserv/url-download` | 11.043 us | 11.649 us | 12.296 us | 0.95x |
| `structural/method-family-generic-call` | 4.153 us | 4.420 us | 17.656 us | 0.94x |
| `lokad/style/hex-color` | 0.204 us | 0.220 us | 0.303 us | 0.93x |
| `lokad/langserv/helper-identifier` | 2.329 us | 2.604 us | 3.475 us | 0.89x |
| `structural/keyword-to-generic-type` | 2.943 us | 3.333 us | 17.301 us | 0.88x |
| `lokad/lexer/number` | 2.984 us | 3.632 us | 4.655 us | 0.82x |
| `literal-family/type-token-family` | 8.851 us | 10.974 us | 25.370 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.145 us | 3.995 us | 17.905 us | 0.79x |
| `structural/ordered-identifier-window` | 7.823 us | 10.349 us | 24.701 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 0.602 us | 0.812 us | 1.076 us | 0.74x |
| `literal/identifier-token-casei` | 3.715 us | 5.623 us | 19.807 us | 0.66x |
| `literal-family/method-token-family` | 2.521 us | 4.907 us | 18.866 us | 0.51x |
| `literal/identifier-token` | 2.150 us | 4.512 us | 17.626 us | 0.48x |
| `literal/call-token` | 1.773 us | 3.820 us | 17.190 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 2.829 us | 10.215 us | 24.191 us | 0.28x |
| `lokad/folding/region-marker` | 5.632 us | 35.173 us | 62.690 us | 0.16x |
| `balancing/begin-end-blocks` | 45.611 us | 450.280 us | 480.771 us | 0.10x |
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
