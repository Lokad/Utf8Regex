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
| `literal/sherlock-zh` | 21.368 us | 17.998 us | 2,177.791 us | 1.19x |
| `aws-keys/full` | 62,829.350 us | 81,764.500 us | 110,083.400 us | 0.77x |
| `literal/sherlock-ru` | 69.473 us | 94.913 us | 5,085.060 us | 0.73x |
| `literal-alternate/sherlock-zh` | 41.918 us | 66.344 us | 2,148.231 us | 0.63x |
| `literal/sherlock-casei-en` | 102.287 us | 181.930 us | 553.435 us | 0.56x |
| `ruff-noqa/tweaked` | 3,720.995 us | 7,119.150 us | 42,415.250 us | 0.52x |
| `literal/sherlock-casei-ru` | 147.019 us | 352.372 us | 5,398.779 us | 0.42x |
| `bounded-repeat/letters-en` | 1,056.504 us | 2,714.993 us | 2,818.256 us | 0.39x |
| `aws-keys/quick` | 3,513.750 us | 9,160.344 us | 44,821.600 us | 0.38x |
| `literal/sherlock-en` | 30.349 us | 83.282 us | 444.993 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,936.315 us | 5,645.421 us | 6,370.550 us | 0.34x |
| `dictionary/single` | 7,905.870 us | 24,997.067 us | 24,879.025 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,374.048 us | 4,551.971 us | 9,590.875 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 166.093 us | 565.320 us | 1,009.622 us | 0.29x |
| `bounded-repeat/capitals` | 10,686.575 us | 43,767.500 us | 47,317.750 us | 0.24x |
| `literal-alternate/sherlock-en` | 328.584 us | 1,440.524 us | 1,962.456 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 122.643 us | 634.309 us | 1,028.131 us | 0.19x |
| `bounded-repeat/context` | 13,418.333 us | 143,065.500 us | 145,715.000 us | 0.09x |
| `ruff-noqa/real` | 3,415.343 us | 67,623.150 us | 99,585.600 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 843.284 us | 22,025.525 us | 27,430.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 125.852 us | 10,808.729 us | 11,907.543 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.320 us | 0.107 us | 0.142 us | 2.98x |
| `common/date-match` | `IsMatch` | 0.279 us | 0.102 us | 0.130 us | 2.74x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.202 us | 0.074 us | 0.086 us | 2.72x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.210 us | 0.078 us | 0.093 us | 2.70x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.180 us | 0.080 us | 0.096 us | 2.24x |
| `common/uri-match` | `IsMatch` | 0.494 us | 0.226 us | 0.258 us | 2.18x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.342 us | 0.161 us | 0.176 us | 2.12x |
| `industry/mariomka-uri-count` | `Count` | 3,296.417 us | 1,820.767 us | 10,012.025 us | 1.81x |
| `common/ip-miss` | `IsMatch` | 1.118 us | 0.685 us | 0.706 us | 1.63x |
| `common/email-match` | `IsMatch` | 0.396 us | 0.260 us | 0.289 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.215 us | 0.166 us | 0.183 us | 1.29x |
| `common/date-miss` | `IsMatch` | 0.584 us | 0.535 us | 0.564 us | 1.09x |
| `common/uri-miss` | `IsMatch` | 0.186 us | 0.178 us | 0.197 us | 1.04x |
| `industry/mariomka-email-count` | `Count` | 432.267 us | 472.102 us | 8,332.120 us | 0.92x |
| `common/match-word` | `Match` | 0.110 us | 0.163 us | 1.067 us | 0.67x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 138.922 us | 247.550 us | 469.647 us | 0.56x |
| `industry/leipzig-twain-count` | `Count` | 607.498 us | 1,275.068 us | 8,354.800 us | 0.48x |
| `common/matches-set` | `Count` | 59.468 us | 185.578 us | 185.994 us | 0.32x |
| `common/matches-word` | `Count` | 1.195 us | 4.037 us | 5.546 us | 0.30x |
| `common/backtracking` | `IsMatch` | 0.232 us | 0.795 us | 0.836 us | 0.29x |
| `common/replace-words` | `Replace` | 21.113 us | 74.178 us | 75.997 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,159.917 us | 8,045.280 us | 16,390.300 us | 0.27x |
| `common/split-words` | `Split` | 13.560 us | 72.953 us | 74.835 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.458 us | 905.351 us | 1,172.067 us | 0.18x |
| `common/matches-boundary` | `Count` | 25.654 us | 160.067 us | 160.528 us | 0.16x |
| `common/matches-words` | `Count` | 8.811 us | 66.829 us | 68.543 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.036 us | 0.334 us | 0.359 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 988.371 us | 9,299.056 us | 16,015.767 us | 0.11x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,109.336 us | 11,568.286 us | 12,120.729 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,777.264 us | 18,624.360 us | 19,235.450 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.047 us | 0.588 us | 0.620 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,701.384 us | 30,857.067 us | 36,867.700 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 956.709 us | 22,211.725 us | 29,092.250 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 297.272 us | 7,863.130 us | 8,319.700 us | 0.04x |
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
| `aws-keys/full` | 62,917.900 us | 17,788.980 us | 48,397.033 us | 3.54x |
| `literal/sherlock-casei-en` | 101.912 us | 76.541 us | 422.148 us | 1.33x |
| `literal-alternate/sherlock-zh` | 42.013 us | 34.894 us | 2,107.131 us | 1.20x |
| `aws-keys/quick` | 3,572.771 us | 3,561.118 us | 35,573.650 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,939.938 us | 1,967.213 us | 2,738.854 us | 0.99x |
| `literal-alternate/sherlock-en-mixed` | 163.719 us | 168.194 us | 580.946 us | 0.97x |
| `literal/sherlock-zh` | 21.397 us | 22.997 us | 2,141.743 us | 0.93x |
| `literal-alternate/sherlock-en` | 327.393 us | 353.337 us | 729.917 us | 0.93x |
| `literal-alternate/sherlock-casei-en` | 125.382 us | 138.119 us | 526.876 us | 0.91x |
| `ruff-noqa/tweaked` | 3,749.225 us | 4,303.789 us | 36,589.000 us | 0.87x |
| `bounded-repeat/capitals` | 10,753.150 us | 12,967.333 us | 15,662.375 us | 0.83x |
| `literal/sherlock-casei-ru` | 151.591 us | 188.406 us | 5,147.900 us | 0.80x |
| `literal/sherlock-ru` | 68.371 us | 85.603 us | 5,034.007 us | 0.80x |
| `literal-alternate/sherlock-en-nomatch` | 123.366 us | 195.452 us | 566.376 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,381.864 us | 2,261.400 us | 7,258.933 us | 0.61x |
| `bounded-repeat/letters-en` | 1,055.504 us | 1,768.467 us | 1,854.562 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 842.638 us | 1,892.432 us | 6,995.525 us | 0.45x |
| `literal/sherlock-en` | 30.204 us | 72.876 us | 419.369 us | 0.41x |
| `bounded-repeat/context` | 13,454.133 us | 66,373.650 us | 69,250.400 us | 0.20x |
| `dictionary/single` | 7,940.840 us | 43,986.750 us | 44,004.850 us | 0.18x |
| `ruff-noqa/real` | 3,342.048 us | 19,925.125 us | 54,081.200 us | 0.17x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.210 us | 0.030 us | 0.045 us | 7.07x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.321 us | 0.046 us | 0.081 us | 6.90x |
| `common/date-match` | `IsMatch` | 0.279 us | 0.041 us | 0.069 us | 6.84x |
| `common/ip-miss` | `IsMatch` | 1.120 us | 0.171 us | 0.194 us | 6.54x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.341 us | 0.053 us | 0.074 us | 6.42x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.179 us | 0.030 us | 0.048 us | 5.98x |
| `common/uri-match` | `IsMatch` | 0.494 us | 0.084 us | 0.114 us | 5.86x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.201 us | 0.035 us | 0.051 us | 5.72x |
| `common/backtracking` | `IsMatch` | 0.233 us | 0.043 us | 0.072 us | 5.44x |
| `common/email-match` | `IsMatch` | 0.395 us | 0.077 us | 0.103 us | 5.12x |
| `common/ip-match` | `IsMatch` | 0.213 us | 0.046 us | 0.067 us | 4.59x |
| `common/date-miss` | `IsMatch` | 0.589 us | 0.189 us | 0.224 us | 3.12x |
| `industry/mariomka-uri-count` | `Count` | 3,302.061 us | 1,060.474 us | 8,018.925 us | 3.11x |
| `common/uri-miss` | `IsMatch` | 0.185 us | 0.064 us | 0.081 us | 2.91x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 138.295 us | 57.753 us | 255.377 us | 2.39x |
| `common/match-word` | `Match` | 0.107 us | 0.075 us | 0.956 us | 1.43x |
| `common/replace-words` | `Replace` | 21.035 us | 16.786 us | 17.812 us | 1.25x |
| `industry/leipzig-river-window-count` | `Count` | 1,726.942 us | 1,467.212 us | 7,484.725 us | 1.18x |
| `industry/mariomka-email-count` | `Count` | 429.328 us | 441.676 us | 7,476.730 us | 0.97x |
| `common/matches-words` | `Count` | 8.824 us | 10.611 us | 13.923 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 963.873 us | 1,179.831 us | 6,967.980 us | 0.82x |
| `common/split-words` | `Split` | 13.698 us | 19.706 us | 21.718 us | 0.70x |
| `industry/mariomka-ip-count` | `Count` | 2,153.789 us | 3,318.543 us | 10,416.300 us | 0.65x |
| `common/matches-set` | `Count` | 58.998 us | 101.171 us | 102.515 us | 0.58x |
| `common/one-node-backtracking` | `IsMatch` | 0.047 us | 0.086 us | 0.123 us | 0.55x |
| `common/matches-word` | `Count` | 1.184 us | 2.279 us | 3.228 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 612.039 us | 1,257.925 us | 7,260.510 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.036 us | 0.085 us | 0.112 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 988.867 us | 2,871.222 us | 7,979.680 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.547 us | 566.497 us | 814.515 us | 0.28x |
| `common/matches-boundary` | `Count` | 25.876 us | 99.563 us | 101.658 us | 0.26x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,119.439 us | 4,564.541 us | 4,942.294 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,786.960 us | 8,331.344 us | 8,910.633 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 296.496 us | 3,764.060 us | 4,184.711 us | 0.08x |
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
| `lokad/docs/inline-doc-prefix` | 12.545 us | 4.518 us | 5.028 us | 2.78x |
| `lokad/lexer/string` | 247.510 us | 90.902 us | 96.095 us | 2.72x |
| `lokad/style/cell-ref` | 6.942 us | 2.609 us | 2.790 us | 2.66x |
| `lokad/lexer/number` | 35.417 us | 13.476 us | 14.451 us | 2.63x |
| `structural/ordered-modifier-pair` | 41.813 us | 17.060 us | 29.663 us | 2.45x |
| `structural/method-family-generic-call` | 15.292 us | 6.280 us | 18.783 us | 2.44x |
| `lokad/style/range-ref` | 5.978 us | 2.520 us | 2.623 us | 2.37x |
| `lokad/lexer/doc-line` | 6.562 us | 2.943 us | 3.617 us | 2.23x |
| `lokad/langserv/identifier-validator` | 25.589 us | 11.779 us | 13.224 us | 2.17x |
| `lokad/lexer/identifier` | 47.465 us | 22.095 us | 23.923 us | 2.15x |
| `fallback/lookahead` | 11.642 us | 5.643 us | 17.919 us | 2.06x |
| `structural/method-family-call` | 15.691 us | 7.679 us | 20.033 us | 2.04x |
| `lokad/style/hex-color` | 1.434 us | 0.795 us | 0.810 us | 1.80x |
| `lokad/langserv/helper-identifier` | 12.336 us | 7.079 us | 7.646 us | 1.74x |
| `structural/ordered-keyword-pair` | 8.651 us | 5.058 us | 18.290 us | 1.71x |
| `lokad/langserv/color-rgb-hex` | 4.755 us | 2.800 us | 2.796 us | 1.70x |
| `lokad/lexer/operator-run` | 5.633 us | 3.326 us | 3.743 us | 1.69x |
| `lokad/langserv/color-short-hex` | 2.185 us | 1.302 us | 1.357 us | 1.68x |
| `lokad/imports/module-imports` | 8.851 us | 6.113 us | 29.836 us | 1.45x |
| `lokad/langserv/url-dashboard` | 30.338 us | 23.226 us | 23.941 us | 1.31x |
| `structural/keyword-to-generic-type` | 5.037 us | 3.869 us | 17.094 us | 1.30x |
| `structural/ordered-keyword-window` | 20.032 us | 17.014 us | 29.820 us | 1.18x |
| `lokad/langserv/url-download` | 46.435 us | 49.853 us | 49.943 us | 0.93x |
| `literal/identifier-token` | 2.616 us | 4.163 us | 16.617 us | 0.63x |
| `structural/ordered-identifier-window` | 14.604 us | 27.549 us | 40.121 us | 0.53x |
| `structural/modifier-family-to-type-pair` | 62.971 us | 126.349 us | 141.976 us | 0.50x |
| `structural/modifier-family-to-type-window` | 114.370 us | 243.917 us | 259.034 us | 0.47x |
| `literal/call-token` | 1.751 us | 4.066 us | 16.450 us | 0.43x |
| `literal-family/method-token-family` | 3.642 us | 8.758 us | 21.244 us | 0.42x |
| `literal-family/type-token-family` | 9.280 us | 22.431 us | 35.427 us | 0.41x |
| `balancing/begin-end-blocks` | 1,507.413 us | 4,204.928 us | 4,246.156 us | 0.36x |
| `literal/identifier-token-casei` | 3.692 us | 14.525 us | 27.777 us | 0.25x |
| `lokad/folding/region-marker` | 4.958 us | 34.616 us | 59.802 us | 0.14x |
| `structural/keyword-family-to-capitalized-identifier` | 24.959 us | 212.728 us | 227.777 us | 0.12x |
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
| `structural/modifier-family-to-type-pair` | 63.364 us | 10.351 us | 23.140 us | 6.12x |
| `lokad/lexer/string` | 248.165 us | 43.718 us | 46.379 us | 5.68x |
| `lokad/lexer/number` | 35.484 us | 6.656 us | 7.864 us | 5.33x |
| `structural/ordered-modifier-pair` | 41.715 us | 8.308 us | 21.019 us | 5.02x |
| `lokad/docs/inline-doc-prefix` | 12.558 us | 2.589 us | 3.342 us | 4.85x |
| `lokad/style/range-ref` | 5.912 us | 1.374 us | 1.501 us | 4.30x |
| `lokad/style/cell-ref` | 6.937 us | 1.630 us | 1.832 us | 4.25x |
| `lokad/langserv/url-dashboard` | 30.213 us | 7.107 us | 7.804 us | 4.25x |
| `lokad/langserv/url-download` | 46.461 us | 12.356 us | 12.759 us | 3.76x |
| `structural/method-family-generic-call` | 15.245 us | 4.322 us | 16.115 us | 3.53x |
| `structural/keyword-family-to-capitalized-identifier` | 25.090 us | 7.204 us | 20.073 us | 3.48x |
| `balancing/begin-end-blocks` | 1,502.060 us | 448.989 us | 479.419 us | 3.35x |
| `structural/method-family-call` | 15.577 us | 4.853 us | 16.660 us | 3.21x |
| `lokad/lexer/identifier` | 47.413 us | 15.310 us | 19.171 us | 3.10x |
| `lokad/langserv/identifier-validator` | 25.601 us | 8.311 us | 10.407 us | 3.08x |
| `lokad/style/hex-color` | 1.441 us | 0.470 us | 0.552 us | 3.06x |
| `lokad/lexer/doc-line` | 6.610 us | 2.188 us | 2.835 us | 3.02x |
| `fallback/lookahead` | 11.594 us | 3.981 us | 15.697 us | 2.91x |
| `lokad/lexer/operator-run` | 5.624 us | 1.940 us | 2.515 us | 2.90x |
| `lokad/langserv/color-rgb-hex` | 4.729 us | 1.733 us | 1.936 us | 2.73x |
| `lokad/langserv/helper-identifier` | 12.313 us | 4.514 us | 5.444 us | 2.73x |
| `structural/ordered-keyword-window` | 20.022 us | 8.224 us | 20.912 us | 2.43x |
| `lokad/langserv/color-short-hex` | 2.188 us | 0.908 us | 1.074 us | 2.41x |
| `structural/ordered-keyword-pair` | 8.668 us | 3.961 us | 16.467 us | 2.19x |
| `structural/modifier-family-to-type-window` | 114.821 us | 53.635 us | 67.034 us | 2.14x |
| `lokad/imports/module-imports` | 8.850 us | 4.215 us | 27.426 us | 2.10x |
| `structural/keyword-to-generic-type` | 5.086 us | 3.323 us | 16.131 us | 1.53x |
| `structural/ordered-identifier-window` | 14.618 us | 10.121 us | 23.056 us | 1.44x |
| `literal-family/type-token-family` | 9.217 us | 10.867 us | 23.204 us | 0.85x |
| `literal-family/method-token-family` | 3.659 us | 4.859 us | 17.360 us | 0.75x |
| `literal/identifier-token-casei` | 3.735 us | 5.601 us | 18.370 us | 0.67x |
| `literal/identifier-token` | 2.626 us | 4.468 us | 16.315 us | 0.59x |
| `literal/call-token` | 1.763 us | 3.722 us | 15.539 us | 0.47x |
| `lokad/folding/region-marker` | 4.967 us | 34.001 us | 59.418 us | 0.15x |
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
