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
| `literal/sherlock-zh` | 22.215 us | 18.821 us | 2,186.774 us | 1.18x |
| `aws-keys/full` | 68,226.350 us | 85,886.700 us | 115,326.800 us | 0.79x |
| `literal/sherlock-ru` | 70.491 us | 97.958 us | 5,219.493 us | 0.72x |
| `literal-alternate/sherlock-zh` | 42.921 us | 67.428 us | 2,155.856 us | 0.64x |
| `ruff-noqa/tweaked` | 3,886.195 us | 7,528.473 us | 41,235.100 us | 0.52x |
| `literal/sherlock-casei-en` | 92.558 us | 198.455 us | 548.974 us | 0.47x |
| `literal/sherlock-casei-ru` | 153.373 us | 361.729 us | 5,626.429 us | 0.42x |
| `bounded-repeat/letters-en` | 1,089.535 us | 2,773.019 us | 2,889.981 us | 0.39x |
| `aws-keys/quick` | 3,736.262 us | 9,697.612 us | 40,863.050 us | 0.39x |
| `literal/sherlock-en` | 31.545 us | 87.443 us | 448.865 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,983.039 us | 5,819.379 us | 6,575.350 us | 0.34x |
| `dictionary/single` | 8,153.667 us | 26,504.700 us | 26,276.100 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,447.177 us | 4,798.844 us | 10,000.400 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 157.909 us | 583.795 us | 1,016.159 us | 0.27x |
| `bounded-repeat/capitals` | 11,255.286 us | 46,650.200 us | 50,048.050 us | 0.24x |
| `literal-alternate/sherlock-en` | 334.600 us | 1,499.522 us | 2,015.845 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 126.645 us | 641.378 us | 1,084.818 us | 0.20x |
| `bounded-repeat/context` | 13,998.833 us | 146,996.200 us | 152,067.300 us | 0.10x |
| `ruff-noqa/real` | 3,418.141 us | 69,731.750 us | 85,863.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 867.987 us | 23,005.450 us | 28,299.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 131.651 us | 11,143.629 us | 12,090.871 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.258 us | 0.108 us | 0.137 us | 2.38x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.171 us | 0.076 us | 0.090 us | 2.25x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.238 us | 0.110 us | 0.144 us | 2.17x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.163 us | 0.082 us | 0.098 us | 1.99x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.154 us | 0.084 us | 0.101 us | 1.83x |
| `industry/mariomka-uri-count` | `Count` | 3,387.422 us | 1,856.910 us | 10,331.300 us | 1.82x |
| `common/uri-match` | `IsMatch` | 0.390 us | 0.236 us | 0.273 us | 1.65x |
| `common/ip-match` | `IsMatch` | 0.281 us | 0.174 us | 0.195 us | 1.61x |
| `common/email-match` | `IsMatch` | 0.408 us | 0.270 us | 0.293 us | 1.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.216 us | 0.169 us | 0.187 us | 1.28x |
| `common/ip-miss` | `IsMatch` | 0.861 us | 0.718 us | 0.743 us | 1.20x |
| `common/uri-miss` | `IsMatch` | 0.232 us | 0.199 us | 0.219 us | 1.16x |
| `common/date-miss` | `IsMatch` | 0.527 us | 0.566 us | 0.603 us | 0.93x |
| `industry/mariomka-email-count` | `Count` | 452.972 us | 494.941 us | 8,915.175 us | 0.92x |
| `common/match-word` | `Match` | 0.112 us | 0.167 us | 1.132 us | 0.67x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 154.662 us | 265.426 us | 528.806 us | 0.58x |
| `industry/leipzig-twain-count` | `Count` | 630.171 us | 1,321.225 us | 8,499.050 us | 0.48x |
| `common/matches-set` | `Count` | 62.476 us | 195.917 us | 198.225 us | 0.32x |
| `common/replace-words` | `Replace` | 23.846 us | 78.252 us | 79.888 us | 0.30x |
| `common/matches-word` | `Count` | 1.240 us | 4.234 us | 5.972 us | 0.29x |
| `common/backtracking` | `IsMatch` | 0.242 us | 0.851 us | 0.894 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,283.585 us | 8,573.733 us | 17,017.600 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.565 us | 968.187 us | 1,271.468 us | 0.18x |
| `common/split-words` | `Split` | 13.263 us | 77.404 us | 78.782 us | 0.17x |
| `common/matches-boundary` | `Count` | 25.601 us | 164.059 us | 165.826 us | 0.16x |
| `common/matches-words` | `Count` | 9.267 us | 69.391 us | 71.166 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.039 us | 0.346 us | 0.374 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 1,035.081 us | 9,819.550 us | 16,737.167 us | 0.11x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,186.357 us | 11,849.400 us | 12,484.143 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,909.145 us | 19,469.375 us | 20,106.375 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.049 us | 0.609 us | 0.646 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,755.802 us | 31,499.033 us | 38,779.300 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 1,008.711 us | 22,816.500 us | 30,310.250 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 332.456 us | 8,571.389 us | 9,083.256 us | 0.04x |
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
| `aws-keys/full` | 68,077.250 us | 18,424.880 us | 50,272.900 us | 3.69x |
| `literal-alternate/sherlock-zh` | 43.405 us | 35.802 us | 2,131.253 us | 1.21x |
| `literal/sherlock-casei-en` | 92.391 us | 77.882 us | 432.208 us | 1.19x |
| `aws-keys/quick` | 3,725.980 us | 3,681.914 us | 36,090.067 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,974.237 us | 2,046.895 us | 2,831.058 us | 0.96x |
| `literal/sherlock-zh` | 22.325 us | 23.798 us | 2,194.665 us | 0.94x |
| `literal-alternate/sherlock-en-mixed` | 156.766 us | 172.226 us | 564.960 us | 0.91x |
| `literal-alternate/sherlock-casei-en` | 130.458 us | 143.715 us | 563.672 us | 0.91x |
| `literal-alternate/sherlock-en` | 333.787 us | 367.800 us | 757.312 us | 0.91x |
| `ruff-noqa/tweaked` | 3,894.000 us | 4,575.400 us | 36,060.450 us | 0.85x |
| `bounded-repeat/capitals` | 11,317.357 us | 13,458.900 us | 16,645.800 us | 0.84x |
| `literal/sherlock-ru` | 70.379 us | 89.563 us | 5,177.960 us | 0.79x |
| `literal/sherlock-casei-ru` | 152.136 us | 218.838 us | 5,325.157 us | 0.70x |
| `literal-alternate/sherlock-en-nomatch` | 126.221 us | 198.926 us | 578.876 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,450.735 us | 2,333.976 us | 7,426.475 us | 0.62x |
| `bounded-repeat/letters-en` | 1,086.141 us | 1,805.943 us | 1,887.590 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 870.026 us | 1,956.062 us | 7,294.275 us | 0.44x |
| `literal/sherlock-en` | 31.585 us | 76.512 us | 437.346 us | 0.41x |
| `bounded-repeat/context` | 14,124.550 us | 68,903.350 us | 72,163.900 us | 0.20x |
| `dictionary/single` | 8,259.644 us | 45,151.000 us | 45,329.800 us | 0.18x |
| `ruff-noqa/real` | 3,514.150 us | 20,705.300 us | 47,003.300 us | 0.17x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.259 us | 0.044 us | 0.074 us | 5.94x |
| `common/backtracking` | `IsMatch` | 0.253 us | 0.043 us | 0.073 us | 5.89x |
| `common/ip-match` | `IsMatch` | 0.279 us | 0.048 us | 0.069 us | 5.84x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.154 us | 0.029 us | 0.049 us | 5.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.167 us | 0.032 us | 0.048 us | 5.23x |
| `common/email-match` | `IsMatch` | 0.403 us | 0.080 us | 0.107 us | 5.02x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.236 us | 0.049 us | 0.087 us | 4.86x |
| `common/ip-miss` | `IsMatch` | 0.867 us | 0.179 us | 0.206 us | 4.85x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.171 us | 0.036 us | 0.053 us | 4.77x |
| `common/uri-match` | `IsMatch` | 0.390 us | 0.088 us | 0.121 us | 4.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.216 us | 0.056 us | 0.076 us | 3.88x |
| `common/uri-miss` | `IsMatch` | 0.232 us | 0.068 us | 0.088 us | 3.41x |
| `industry/mariomka-uri-count` | `Count` | 3,417.336 us | 1,093.559 us | 8,403.900 us | 3.12x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 154.592 us | 57.903 us | 270.804 us | 2.67x |
| `common/date-miss` | `IsMatch` | 0.524 us | 0.202 us | 0.242 us | 2.59x |
| `common/match-word` | `Match` | 0.113 us | 0.080 us | 1.005 us | 1.41x |
| `common/replace-words` | `Replace` | 23.712 us | 17.620 us | 18.990 us | 1.35x |
| `industry/leipzig-river-window-count` | `Count` | 1,794.770 us | 1,492.552 us | 7,622.525 us | 1.20x |
| `industry/mariomka-email-count` | `Count` | 453.496 us | 460.624 us | 7,682.300 us | 0.98x |
| `common/matches-words` | `Count` | 9.263 us | 11.025 us | 14.495 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,013.620 us | 1,220.974 us | 7,300.380 us | 0.83x |
| `industry/mariomka-ip-count` | `Count` | 2,286.203 us | 3,503.223 us | 10,996.767 us | 0.65x |
| `common/split-words` | `Split` | 13.505 us | 20.989 us | 22.996 us | 0.64x |
| `common/matches-set` | `Count` | 62.381 us | 105.909 us | 107.806 us | 0.59x |
| `common/one-node-backtracking` | `IsMatch` | 0.049 us | 0.089 us | 0.128 us | 0.55x |
| `common/matches-word` | `Count` | 1.238 us | 2.379 us | 3.533 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 635.218 us | 1,304.321 us | 7,276.645 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.039 us | 0.088 us | 0.116 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 1,033.265 us | 3,023.768 us | 7,896.925 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 170.081 us | 594.295 us | 842.055 us | 0.29x |
| `common/matches-boundary` | `Count` | 26.090 us | 104.713 us | 106.918 us | 0.25x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,193.564 us | 4,808.481 us | 5,220.207 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,899.697 us | 8,695.922 us | 9,291.462 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 333.586 us | 3,976.205 us | 4,333.611 us | 0.08x |
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
| `lokad/style/cell-ref` | 7.359 us | 2.770 us | 2.922 us | 2.66x |
| `lokad/docs/inline-doc-prefix` | 12.494 us | 4.783 us | 5.413 us | 2.61x |
| `lokad/style/range-ref` | 6.354 us | 2.579 us | 2.742 us | 2.46x |
| `structural/ordered-modifier-pair` | 42.742 us | 17.663 us | 30.789 us | 2.42x |
| `lokad/langserv/identifier-validator` | 28.363 us | 12.674 us | 14.003 us | 2.24x |
| `fallback/lookahead` | 12.570 us | 6.044 us | 18.812 us | 2.08x |
| `lokad/lexer/doc-line` | 6.430 us | 3.103 us | 3.794 us | 2.07x |
| `lokad/lexer/number` | 29.527 us | 14.280 us | 15.572 us | 2.07x |
| `lokad/lexer/identifier` | 45.902 us | 22.976 us | 26.312 us | 2.00x |
| `lokad/style/hex-color` | 1.557 us | 0.801 us | 0.854 us | 1.94x |
| `structural/method-family-generic-call` | 12.016 us | 6.714 us | 20.595 us | 1.79x |
| `lokad/langserv/helper-identifier` | 13.005 us | 7.340 us | 7.737 us | 1.77x |
| `structural/ordered-keyword-pair` | 9.317 us | 5.522 us | 19.196 us | 1.69x |
| `lokad/langserv/color-short-hex` | 2.272 us | 1.368 us | 1.458 us | 1.66x |
| `lokad/lexer/string` | 160.548 us | 97.522 us | 102.921 us | 1.65x |
| `lokad/langserv/color-rgb-hex` | 4.838 us | 2.949 us | 2.953 us | 1.64x |
| `lokad/lexer/operator-run` | 5.619 us | 3.448 us | 3.812 us | 1.63x |
| `lokad/imports/module-imports` | 9.194 us | 6.406 us | 31.281 us | 1.44x |
| `structural/method-family-call` | 10.912 us | 8.024 us | 21.032 us | 1.36x |
| `structural/keyword-to-generic-type` | 5.468 us | 4.119 us | 17.807 us | 1.33x |
| `lokad/langserv/url-dashboard` | 32.068 us | 24.473 us | 24.945 us | 1.31x |
| `structural/ordered-keyword-window` | 20.964 us | 17.581 us | 30.934 us | 1.19x |
| `lokad/langserv/url-download` | 48.933 us | 50.982 us | 51.105 us | 0.96x |
| `literal/identifier-token` | 2.732 us | 4.310 us | 17.351 us | 0.63x |
| `structural/ordered-identifier-window` | 15.198 us | 28.571 us | 41.780 us | 0.53x |
| `literal/call-token` | 1.860 us | 4.352 us | 17.491 us | 0.43x |
| `literal-family/method-token-family` | 3.855 us | 9.095 us | 21.990 us | 0.42x |
| `literal-family/type-token-family` | 9.795 us | 23.586 us | 38.093 us | 0.42x |
| `structural/modifier-family-to-type-window` | 103.334 us | 256.125 us | 269.688 us | 0.40x |
| `balancing/begin-end-blocks` | 1,563.537 us | 4,337.989 us | 4,357.472 us | 0.36x |
| `literal/identifier-token-casei` | 3.955 us | 15.838 us | 28.529 us | 0.25x |
| `structural/modifier-family-to-type-pair` | 20.795 us | 133.196 us | 146.693 us | 0.16x |
| `lokad/folding/region-marker` | 5.273 us | 35.777 us | 63.559 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 26.561 us | 217.577 us | 234.931 us | 0.12x |
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
| `structural/ordered-modifier-pair` | 43.118 us | 8.535 us | 21.653 us | 5.05x |
| `lokad/docs/inline-doc-prefix` | 12.581 us | 2.753 us | 3.615 us | 4.57x |
| `lokad/style/range-ref` | 6.365 us | 1.457 us | 1.567 us | 4.37x |
| `lokad/style/cell-ref` | 7.326 us | 1.715 us | 1.897 us | 4.27x |
| `lokad/langserv/url-dashboard` | 31.976 us | 7.495 us | 8.031 us | 4.27x |
| `lokad/lexer/number` | 29.188 us | 7.391 us | 8.508 us | 3.95x |
| `lokad/langserv/url-download` | 49.332 us | 13.049 us | 13.571 us | 3.78x |
| `lokad/lexer/string` | 160.197 us | 45.528 us | 48.960 us | 3.52x |
| `structural/keyword-family-to-capitalized-identifier` | 26.523 us | 7.720 us | 21.713 us | 3.44x |
| `balancing/begin-end-blocks` | 1,550.502 us | 467.751 us | 496.786 us | 3.31x |
| `lokad/langserv/identifier-validator` | 28.371 us | 8.974 us | 11.290 us | 3.16x |
| `lokad/style/hex-color` | 1.552 us | 0.508 us | 0.587 us | 3.05x |
| `fallback/lookahead` | 12.628 us | 4.193 us | 16.626 us | 3.01x |
| `lokad/lexer/doc-line` | 6.393 us | 2.190 us | 2.930 us | 2.92x |
| `lokad/lexer/operator-run` | 5.629 us | 1.983 us | 2.552 us | 2.84x |
| `lokad/langserv/helper-identifier` | 13.364 us | 4.777 us | 5.731 us | 2.80x |
| `lokad/lexer/identifier` | 45.816 us | 17.004 us | 20.808 us | 2.69x |
| `structural/method-family-generic-call` | 11.986 us | 4.625 us | 17.538 us | 2.59x |
| `lokad/langserv/color-rgb-hex` | 4.803 us | 1.864 us | 1.995 us | 2.58x |
| `structural/ordered-keyword-window` | 20.972 us | 8.576 us | 21.659 us | 2.45x |
| `lokad/langserv/color-short-hex` | 2.266 us | 0.963 us | 1.145 us | 2.35x |
| `structural/ordered-keyword-pair` | 9.324 us | 4.178 us | 17.287 us | 2.23x |
| `structural/method-family-call` | 10.868 us | 5.035 us | 17.759 us | 2.16x |
| `lokad/imports/module-imports` | 9.167 us | 4.422 us | 28.262 us | 2.07x |
| `structural/modifier-family-to-type-pair` | 20.825 us | 10.640 us | 23.759 us | 1.96x |
| `structural/modifier-family-to-type-window` | 104.053 us | 55.422 us | 69.811 us | 1.88x |
| `structural/keyword-to-generic-type` | 5.476 us | 3.491 us | 16.821 us | 1.57x |
| `structural/ordered-identifier-window` | 15.189 us | 10.561 us | 24.013 us | 1.44x |
| `literal-family/type-token-family` | 9.750 us | 11.430 us | 24.925 us | 0.85x |
| `literal-family/method-token-family` | 3.805 us | 5.309 us | 18.114 us | 0.72x |
| `literal/identifier-token-casei` | 3.956 us | 5.990 us | 19.064 us | 0.66x |
| `literal/identifier-token` | 2.723 us | 4.651 us | 16.950 us | 0.59x |
| `literal/call-token` | 1.852 us | 3.996 us | 16.551 us | 0.46x |
| `lokad/folding/region-marker` | 5.183 us | 37.207 us | 64.005 us | 0.14x |
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
