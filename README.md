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
| `aws-keys/full` | 123,540.600 us | 81,742.500 us | 112,336.800 us | 1.51x |
| `literal/sherlock-zh` | 21.385 us | 18.037 us | 2,093.464 us | 1.19x |
| `literal/sherlock-ru` | 66.204 us | 95.308 us | 5,017.600 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.062 us | 67.386 us | 2,139.818 us | 0.62x |
| `ruff-noqa/tweaked` | 3,765.740 us | 7,388.527 us | 36,512.950 us | 0.51x |
| `literal/sherlock-casei-en` | 89.098 us | 204.101 us | 554.455 us | 0.44x |
| `literal/sherlock-casei-ru` | 147.945 us | 353.464 us | 5,434.521 us | 0.42x |
| `bounded-repeat/letters-en` | 1,063.066 us | 2,715.118 us | 2,812.467 us | 0.39x |
| `aws-keys/quick` | 3,746.643 us | 9,600.425 us | 45,307.050 us | 0.39x |
| `literal/sherlock-en` | 30.450 us | 84.446 us | 460.758 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,904.983 us | 5,618.457 us | 6,414.100 us | 0.34x |
| `dictionary/single` | 7,807.410 us | 24,688.000 us | 24,700.325 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,381.369 us | 4,621.459 us | 9,603.400 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 151.668 us | 595.825 us | 1,016.910 us | 0.25x |
| `bounded-repeat/capitals` | 10,815.000 us | 44,273.250 us | 47,318.350 us | 0.24x |
| `literal-alternate/sherlock-en` | 335.840 us | 1,440.504 us | 2,017.435 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 124.475 us | 655.641 us | 1,064.153 us | 0.19x |
| `bounded-repeat/context` | 13,587.550 us | 140,417.600 us | 146,099.100 us | 0.10x |
| `ruff-noqa/real` | 3,364.543 us | 67,144.200 us | 98,892.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 869.656 us | 22,577.550 us | 27,819.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 124.534 us | 10,715.562 us | 11,761.171 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.226 us | 0.074 us | 0.088 us | 3.04x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.265 us | 0.105 us | 0.140 us | 2.51x |
| `common/date-match` | `IsMatch` | 0.217 us | 0.105 us | 0.131 us | 2.08x |
| `industry/mariomka-uri-count` | `Count` | 3,154.862 us | 1,870.880 us | 10,015.400 us | 1.69x |
| `common/uri-match` | `IsMatch` | 0.370 us | 0.227 us | 0.259 us | 1.63x |
| `common/email-match` | `IsMatch` | 0.405 us | 0.251 us | 0.276 us | 1.61x |
| `common/ip-match` | `IsMatch` | 0.277 us | 0.173 us | 0.191 us | 1.61x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.118 us | 0.078 us | 0.093 us | 1.52x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.115 us | 0.080 us | 0.096 us | 1.44x |
| `common/ip-miss` | `IsMatch` | 0.870 us | 0.699 us | 0.712 us | 1.25x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.168 us | 0.161 us | 0.177 us | 1.05x |
| `common/uri-miss` | `IsMatch` | 0.187 us | 0.193 us | 0.211 us | 0.97x |
| `industry/mariomka-email-count` | `Count` | 417.122 us | 468.805 us | 8,551.100 us | 0.89x |
| `common/date-miss` | `IsMatch` | 0.458 us | 0.537 us | 0.571 us | 0.85x |
| `common/match-word` | `Match` | 0.111 us | 0.162 us | 1.083 us | 0.68x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 141.644 us | 249.935 us | 486.194 us | 0.57x |
| `industry/leipzig-twain-count` | `Count` | 638.117 us | 1,340.876 us | 8,664.125 us | 0.48x |
| `common/matches-set` | `Count` | 58.971 us | 185.771 us | 189.085 us | 0.32x |
| `common/matches-word` | `Count` | 1.195 us | 4.052 us | 5.681 us | 0.29x |
| `common/backtracking` | `IsMatch` | 0.237 us | 0.824 us | 0.864 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,224.459 us | 8,243.370 us | 16,474.567 us | 0.27x |
| `common/replace-words` | `Replace` | 20.051 us | 74.323 us | 75.883 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.410 us | 936.827 us | 1,201.280 us | 0.17x |
| `common/split-words` | `Split` | 12.709 us | 74.197 us | 76.514 us | 0.17x |
| `common/matches-boundary` | `Count` | 26.243 us | 163.399 us | 164.288 us | 0.16x |
| `common/matches-words` | `Count` | 8.843 us | 67.128 us | 68.873 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.037 us | 0.340 us | 0.361 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 1,004.327 us | 9,555.975 us | 16,399.867 us | 0.11x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,126.525 us | 11,304.129 us | 11,914.529 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,858.755 us | 18,995.500 us | 19,392.250 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.047 us | 0.589 us | 0.624 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,744.698 us | 32,961.400 us | 40,121.750 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 962.892 us | 22,938.675 us | 29,598.050 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 317.363 us | 7,863.720 us | 8,312.778 us | 0.04x |
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
| `aws-keys/full` | 122,443.100 us | 17,710.980 us | 48,576.533 us | 6.91x |
| `literal-alternate/sherlock-zh` | 41.655 us | 34.691 us | 2,088.578 us | 1.20x |
| `literal/sherlock-casei-en` | 89.835 us | 76.191 us | 435.234 us | 1.18x |
| `aws-keys/quick` | 3,807.824 us | 3,599.568 us | 36,116.450 us | 1.06x |
| `bounded-repeat/letters-ru` | 1,910.725 us | 1,978.621 us | 2,750.481 us | 0.97x |
| `literal-alternate/sherlock-en` | 336.296 us | 355.171 us | 758.728 us | 0.95x |
| `literal/sherlock-zh` | 21.201 us | 23.048 us | 2,100.489 us | 0.92x |
| `literal-alternate/sherlock-en-mixed` | 151.008 us | 169.980 us | 546.106 us | 0.89x |
| `literal-alternate/sherlock-casei-en` | 124.452 us | 141.092 us | 537.470 us | 0.88x |
| `ruff-noqa/tweaked` | 3,831.633 us | 4,504.933 us | 35,605.933 us | 0.85x |
| `bounded-repeat/capitals` | 10,770.643 us | 12,824.483 us | 15,646.100 us | 0.84x |
| `literal/sherlock-ru` | 66.283 us | 87.260 us | 5,020.973 us | 0.76x |
| `literal/sherlock-casei-ru` | 146.952 us | 214.467 us | 5,229.293 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 124.322 us | 192.724 us | 591.000 us | 0.65x |
| `literal-alternate/sherlock-ru` | 1,392.075 us | 2,246.400 us | 7,156.783 us | 0.62x |
| `bounded-repeat/letters-en` | 1,063.921 us | 1,778.958 us | 1,843.290 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 867.867 us | 1,887.010 us | 6,860.175 us | 0.46x |
| `literal/sherlock-en` | 30.623 us | 74.118 us | 410.093 us | 0.41x |
| `bounded-repeat/context` | 13,535.450 us | 65,844.150 us | 68,660.200 us | 0.21x |
| `dictionary/single` | 7,794.030 us | 43,939.150 us | 43,839.700 us | 0.18x |
| `ruff-noqa/real` | 3,402.774 us | 19,614.875 us | 53,496.400 us | 0.17x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.227 us | 0.035 us | 0.051 us | 6.46x |
| `common/ip-match` | `IsMatch` | 0.275 us | 0.046 us | 0.067 us | 5.91x |
| `common/backtracking` | `IsMatch` | 0.236 us | 0.042 us | 0.072 us | 5.68x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.263 us | 0.047 us | 0.082 us | 5.64x |
| `common/date-match` | `IsMatch` | 0.219 us | 0.041 us | 0.069 us | 5.37x |
| `common/ip-miss` | `IsMatch` | 0.864 us | 0.171 us | 0.197 us | 5.07x |
| `common/email-match` | `IsMatch` | 0.399 us | 0.082 us | 0.103 us | 4.87x |
| `common/uri-match` | `IsMatch` | 0.370 us | 0.084 us | 0.116 us | 4.42x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.117 us | 0.029 us | 0.046 us | 3.98x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.118 us | 0.030 us | 0.045 us | 3.89x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.170 us | 0.053 us | 0.074 us | 3.23x |
| `common/uri-miss` | `IsMatch` | 0.186 us | 0.062 us | 0.082 us | 2.97x |
| `industry/mariomka-uri-count` | `Count` | 3,133.348 us | 1,064.803 us | 8,006.150 us | 2.94x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 140.914 us | 55.968 us | 255.304 us | 2.52x |
| `common/date-miss` | `IsMatch` | 0.458 us | 0.195 us | 0.231 us | 2.35x |
| `common/match-word` | `Match` | 0.110 us | 0.073 us | 0.980 us | 1.51x |
| `common/replace-words` | `Replace` | 20.175 us | 16.688 us | 18.938 us | 1.21x |
| `industry/leipzig-river-window-count` | `Count` | 1,777.419 us | 1,524.614 us | 7,687.000 us | 1.17x |
| `industry/mariomka-email-count` | `Count` | 423.233 us | 440.028 us | 7,399.025 us | 0.96x |
| `common/matches-words` | `Count` | 8.789 us | 10.531 us | 13.595 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 968.840 us | 1,182.744 us | 7,285.800 us | 0.82x |
| `industry/mariomka-ip-count` | `Count` | 2,232.997 us | 3,344.513 us | 10,647.767 us | 0.67x |
| `common/split-words` | `Split` | 12.668 us | 19.645 us | 21.237 us | 0.64x |
| `common/matches-set` | `Count` | 59.162 us | 102.087 us | 102.975 us | 0.58x |
| `common/one-node-backtracking` | `IsMatch` | 0.046 us | 0.087 us | 0.124 us | 0.53x |
| `common/matches-word` | `Count` | 1.193 us | 2.251 us | 3.342 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 640.319 us | 1,299.524 us | 6,793.033 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.037 us | 0.086 us | 0.113 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 1,015.615 us | 2,937.642 us | 7,872.725 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 163.779 us | 573.019 us | 812.678 us | 0.29x |
| `common/matches-boundary` | `Count` | 26.326 us | 100.989 us | 102.077 us | 0.26x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,129.866 us | 4,605.847 us | 5,054.567 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,873.885 us | 8,417.180 us | 8,810.278 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 313.876 us | 3,833.635 us | 4,191.967 us | 0.08x |
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
| `lokad/lexer/string` | 247.414 us | 90.300 us | 95.388 us | 2.74x |
| `lokad/style/cell-ref` | 6.990 us | 2.590 us | 2.777 us | 2.70x |
| `lokad/lexer/number` | 34.977 us | 13.071 us | 14.370 us | 2.68x |
| `lokad/docs/inline-doc-prefix` | 11.772 us | 4.488 us | 5.092 us | 2.62x |
| `lokad/style/range-ref` | 6.109 us | 2.396 us | 2.505 us | 2.55x |
| `structural/ordered-modifier-pair` | 41.027 us | 16.443 us | 29.250 us | 2.50x |
| `lokad/lexer/identifier` | 50.925 us | 22.168 us | 24.410 us | 2.30x |
| `lokad/lexer/doc-line` | 6.584 us | 2.987 us | 3.480 us | 2.20x |
| `lokad/langserv/identifier-validator` | 26.164 us | 11.934 us | 12.969 us | 2.19x |
| `fallback/lookahead` | 11.834 us | 5.795 us | 18.625 us | 2.04x |
| `lokad/style/hex-color` | 1.476 us | 0.773 us | 0.806 us | 1.91x |
| `lokad/langserv/helper-identifier` | 12.564 us | 6.996 us | 7.318 us | 1.80x |
| `lokad/langserv/color-rgb-hex` | 4.936 us | 2.839 us | 2.874 us | 1.74x |
| `structural/ordered-keyword-pair` | 8.963 us | 5.171 us | 18.424 us | 1.73x |
| `lokad/lexer/operator-run` | 5.687 us | 3.289 us | 3.594 us | 1.73x |
| `lokad/langserv/color-short-hex` | 2.260 us | 1.337 us | 1.414 us | 1.69x |
| `structural/method-family-generic-call` | 10.262 us | 6.393 us | 19.803 us | 1.61x |
| `lokad/imports/module-imports` | 8.728 us | 6.205 us | 30.542 us | 1.41x |
| `structural/method-family-call` | 10.530 us | 7.654 us | 20.765 us | 1.38x |
| `structural/keyword-to-generic-type` | 5.253 us | 3.935 us | 17.151 us | 1.33x |
| `lokad/langserv/url-dashboard` | 29.516 us | 23.148 us | 23.790 us | 1.28x |
| `structural/ordered-keyword-window` | 20.548 us | 16.997 us | 29.989 us | 1.21x |
| `lokad/langserv/url-download` | 46.456 us | 51.138 us | 51.866 us | 0.91x |
| `literal/identifier-token` | 2.636 us | 4.146 us | 16.745 us | 0.64x |
| `structural/ordered-identifier-window` | 14.443 us | 27.407 us | 40.056 us | 0.53x |
| `literal/call-token` | 1.781 us | 4.148 us | 16.694 us | 0.43x |
| `literal-family/method-token-family` | 3.695 us | 8.761 us | 21.524 us | 0.42x |
| `literal-family/type-token-family` | 9.513 us | 23.337 us | 36.602 us | 0.41x |
| `structural/modifier-family-to-type-window` | 97.341 us | 245.474 us | 256.651 us | 0.40x |
| `balancing/begin-end-blocks` | 1,499.912 us | 4,180.568 us | 4,184.244 us | 0.36x |
| `literal/identifier-token-casei` | 3.721 us | 14.363 us | 27.276 us | 0.26x |
| `structural/modifier-family-to-type-pair` | 20.016 us | 129.805 us | 142.231 us | 0.15x |
| `lokad/folding/region-marker` | 5.195 us | 34.379 us | 61.598 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 25.515 us | 208.571 us | 222.002 us | 0.12x |
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
| `lokad/lexer/string` | 249.186 us | 43.127 us | 45.840 us | 5.78x |
| `lokad/lexer/number` | 35.081 us | 6.672 us | 7.798 us | 5.26x |
| `structural/ordered-modifier-pair` | 40.977 us | 8.324 us | 20.718 us | 4.92x |
| `lokad/docs/inline-doc-prefix` | 11.690 us | 2.589 us | 3.379 us | 4.51x |
| `lokad/style/range-ref` | 6.160 us | 1.396 us | 1.509 us | 4.41x |
| `lokad/style/cell-ref` | 6.832 us | 1.578 us | 1.777 us | 4.33x |
| `lokad/langserv/url-dashboard` | 29.604 us | 7.112 us | 7.746 us | 4.16x |
| `lokad/langserv/url-download` | 47.221 us | 12.528 us | 13.217 us | 3.77x |
| `structural/keyword-family-to-capitalized-identifier` | 25.482 us | 7.395 us | 20.994 us | 3.45x |
| `balancing/begin-end-blocks` | 1,504.846 us | 449.347 us | 482.588 us | 3.35x |
| `lokad/lexer/identifier` | 51.029 us | 15.507 us | 18.971 us | 3.29x |
| `lokad/style/hex-color` | 1.482 us | 0.468 us | 0.555 us | 3.17x |
| `lokad/langserv/identifier-validator` | 25.872 us | 8.212 us | 10.276 us | 3.15x |
| `lokad/lexer/doc-line` | 6.572 us | 2.109 us | 2.821 us | 3.12x |
| `lokad/lexer/operator-run` | 5.670 us | 1.855 us | 2.354 us | 3.06x |
| `fallback/lookahead` | 11.855 us | 4.041 us | 16.427 us | 2.93x |
| `lokad/langserv/helper-identifier` | 12.523 us | 4.416 us | 5.310 us | 2.84x |
| `lokad/langserv/color-rgb-hex` | 4.927 us | 1.746 us | 1.942 us | 2.82x |
| `structural/ordered-keyword-window` | 20.596 us | 8.281 us | 20.777 us | 2.49x |
| `lokad/langserv/color-short-hex` | 2.255 us | 0.951 us | 1.118 us | 2.37x |
| `structural/method-family-generic-call` | 10.235 us | 4.391 us | 16.980 us | 2.33x |
| `structural/ordered-keyword-pair` | 8.957 us | 3.952 us | 16.482 us | 2.27x |
| `structural/method-family-call` | 10.506 us | 4.794 us | 17.396 us | 2.19x |
| `lokad/imports/module-imports` | 8.841 us | 4.268 us | 27.813 us | 2.07x |
| `structural/modifier-family-to-type-pair` | 20.119 us | 10.341 us | 22.642 us | 1.95x |
| `structural/modifier-family-to-type-window` | 97.745 us | 54.593 us | 68.825 us | 1.79x |
| `structural/keyword-to-generic-type` | 5.268 us | 3.357 us | 16.137 us | 1.57x |
| `structural/ordered-identifier-window` | 14.607 us | 10.157 us | 22.926 us | 1.44x |
| `literal-family/type-token-family` | 9.452 us | 11.044 us | 24.039 us | 0.86x |
| `literal-family/method-token-family` | 3.673 us | 5.131 us | 17.599 us | 0.72x |
| `literal/identifier-token-casei` | 3.749 us | 5.599 us | 18.373 us | 0.67x |
| `literal/identifier-token` | 2.637 us | 4.526 us | 16.407 us | 0.58x |
| `literal/call-token` | 1.785 us | 3.797 us | 15.777 us | 0.47x |
| `lokad/folding/region-marker` | 5.161 us | 36.036 us | 61.283 us | 0.14x |
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
