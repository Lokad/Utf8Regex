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
| `literal/sherlock-zh` | 21.301 us | 17.791 us | 2,076.847 us | 1.20x |
| `aws-keys/full` | 59,521.500 us | 81,192.000 us | 112,424.600 us | 0.73x |
| `literal/sherlock-ru` | 65.233 us | 93.465 us | 5,128.627 us | 0.70x |
| `literal-alternate/sherlock-zh` | 41.859 us | 64.895 us | 2,108.665 us | 0.65x |
| `literal/sherlock-casei-en` | 100.297 us | 178.831 us | 614.238 us | 0.56x |
| `ruff-noqa/tweaked` | 3,618.248 us | 7,280.764 us | 39,755.950 us | 0.50x |
| `literal/sherlock-casei-ru` | 148.402 us | 343.588 us | 5,276.293 us | 0.43x |
| `aws-keys/quick` | 3,678.967 us | 9,266.567 us | 38,273.250 us | 0.40x |
| `bounded-repeat/letters-en` | 1,054.377 us | 2,692.750 us | 2,806.770 us | 0.39x |
| `literal/sherlock-en` | 30.427 us | 82.231 us | 470.513 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,887.733 us | 5,581.057 us | 6,355.167 us | 0.34x |
| `dictionary/single` | 7,786.940 us | 25,141.200 us | 25,009.350 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,368.841 us | 4,516.135 us | 9,490.675 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 161.247 us | 566.150 us | 975.917 us | 0.28x |
| `literal-alternate/sherlock-en` | 327.423 us | 1,401.278 us | 1,912.622 us | 0.23x |
| `bounded-repeat/capitals` | 9,590.250 us | 43,922.800 us | 47,663.150 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 122.229 us | 614.456 us | 1,025.544 us | 0.20x |
| `bounded-repeat/context` | 19,081.450 us | 142,112.600 us | 144,759.900 us | 0.13x |
| `ruff-noqa/real` | 3,137.721 us | 66,316.000 us | 100,798.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 803.757 us | 21,991.250 us | 26,992.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 131.821 us | 10,676.675 us | 11,505.257 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/uri-miss` | `IsMatch` | 0.800 us | 0.180 us | 0.196 us | 4.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.291 us | 0.071 us | 0.085 us | 4.08x |
| `common/ip-match` | `IsMatch` | 0.672 us | 0.168 us | 0.188 us | 4.01x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.277 us | 0.078 us | 0.092 us | 3.57x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.354 us | 0.102 us | 0.137 us | 3.46x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.271 us | 0.078 us | 0.094 us | 3.45x |
| `common/email-match` | `IsMatch` | 0.883 us | 0.257 us | 0.279 us | 3.44x |
| `common/date-match` | `IsMatch` | 0.354 us | 0.104 us | 0.132 us | 3.39x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.521 us | 0.160 us | 0.171 us | 3.27x |
| `common/uri-match` | `IsMatch` | 0.706 us | 0.228 us | 0.264 us | 3.10x |
| `common/date-miss` | `IsMatch` | 1.338 us | 0.539 us | 0.588 us | 2.48x |
| `industry/mariomka-uri-count` | `Count` | 4,313.317 us | 1,865.705 us | 9,815.325 us | 2.31x |
| `common/ip-miss` | `IsMatch` | 1.065 us | 0.679 us | 0.719 us | 1.57x |
| `industry/mariomka-email-count` | `Count` | 684.575 us | 478.263 us | 8,317.030 us | 1.43x |
| `common/match-word` | `Match` | 0.150 us | 0.160 us | 1.072 us | 0.94x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 135.969 us | 248.957 us | 472.942 us | 0.55x |
| `industry/leipzig-twain-count` | `Count` | 603.813 us | 1,262.810 us | 8,320.556 us | 0.48x |
| `common/matches-set` | `Count` | 59.092 us | 185.350 us | 187.058 us | 0.32x |
| `common/matches-word` | `Count` | 1.201 us | 4.058 us | 5.713 us | 0.30x |
| `common/backtracking` | `IsMatch` | 0.235 us | 0.809 us | 0.849 us | 0.29x |
| `common/replace-words` | `Replace` | 20.852 us | 75.807 us | 77.737 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,107.703 us | 7,907.490 us | 15,876.767 us | 0.27x |
| `common/split-words` | `Split` | 13.943 us | 76.007 us | 77.409 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 163.979 us | 932.231 us | 1,179.985 us | 0.18x |
| `common/matches-boundary` | `Count` | 24.829 us | 159.394 us | 160.854 us | 0.16x |
| `common/matches-words` | `Count` | 8.821 us | 68.641 us | 69.972 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.036 us | 0.335 us | 0.359 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 946.383 us | 9,087.689 us | 16,119.233 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,104.659 us | 11,258.257 us | 12,030.543 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,799.533 us | 18,624.900 us | 19,236.150 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.047 us | 0.581 us | 0.621 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,674.790 us | 31,513.367 us | 38,368.550 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 964.677 us | 21,383.225 us | 28,323.550 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 319.690 us | 8,516.730 us | 9,062.889 us | 0.04x |
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
| `aws-keys/full` | 59,469.950 us | 17,485.020 us | 50,421.800 us | 3.40x |
| `literal/sherlock-casei-en` | 101.175 us | 76.004 us | 480.980 us | 1.33x |
| `literal-alternate/sherlock-zh` | 41.614 us | 34.307 us | 2,087.482 us | 1.21x |
| `aws-keys/quick` | 3,755.571 us | 3,538.918 us | 33,857.500 us | 1.06x |
| `bounded-repeat/letters-ru` | 1,885.012 us | 1,956.513 us | 2,740.981 us | 0.96x |
| `literal-alternate/sherlock-en-mixed` | 160.885 us | 170.456 us | 523.878 us | 0.94x |
| `literal/sherlock-zh` | 21.264 us | 22.709 us | 2,055.719 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 130.337 us | 140.353 us | 543.411 us | 0.93x |
| `literal-alternate/sherlock-en` | 326.512 us | 352.235 us | 722.161 us | 0.93x |
| `ruff-noqa/tweaked` | 3,665.924 us | 4,384.967 us | 33,959.950 us | 0.84x |
| `literal/sherlock-ru` | 65.711 us | 85.339 us | 5,113.600 us | 0.77x |
| `bounded-repeat/capitals` | 9,506.712 us | 12,941.833 us | 15,679.150 us | 0.73x |
| `literal/sherlock-casei-ru` | 148.515 us | 210.561 us | 4,979.653 us | 0.71x |
| `literal-alternate/sherlock-en-nomatch` | 122.714 us | 191.559 us | 559.078 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,372.325 us | 2,233.279 us | 7,052.817 us | 0.61x |
| `bounded-repeat/letters-en` | 1,057.379 us | 1,765.295 us | 1,844.517 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 803.568 us | 1,877.585 us | 6,804.425 us | 0.43x |
| `literal/sherlock-en` | 30.000 us | 72.006 us | 410.399 us | 0.42x |
| `bounded-repeat/context` | 19,160.325 us | 65,905.800 us | 69,079.150 us | 0.29x |
| `dictionary/single` | 7,807.440 us | 43,771.950 us | 43,967.400 us | 0.18x |
| `ruff-noqa/real` | 3,177.683 us | 19,826.250 us | 53,008.650 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.682 us | 0.049 us | 0.068 us | 14.06x |
| `common/uri-miss` | `IsMatch` | 0.800 us | 0.064 us | 0.084 us | 12.41x |
| `common/email-match` | `IsMatch` | 0.895 us | 0.077 us | 0.102 us | 11.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.530 us | 0.052 us | 0.071 us | 10.29x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.271 us | 0.029 us | 0.045 us | 9.47x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.276 us | 0.029 us | 0.045 us | 9.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.291 us | 0.033 us | 0.049 us | 8.85x |
| `common/date-match` | `IsMatch` | 0.350 us | 0.042 us | 0.069 us | 8.40x |
| `common/uri-match` | `IsMatch` | 0.701 us | 0.084 us | 0.113 us | 8.31x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.353 us | 0.045 us | 0.079 us | 7.90x |
| `common/date-miss` | `IsMatch` | 1.338 us | 0.190 us | 0.228 us | 7.04x |
| `common/ip-miss` | `IsMatch` | 1.074 us | 0.172 us | 0.197 us | 6.25x |
| `common/backtracking` | `IsMatch` | 0.234 us | 0.042 us | 0.070 us | 5.52x |
| `industry/mariomka-uri-count` | `Count` | 4,307.167 us | 1,076.939 us | 8,077.775 us | 4.00x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 136.363 us | 56.164 us | 262.390 us | 2.43x |
| `common/match-word` | `Match` | 0.150 us | 0.077 us | 0.955 us | 1.94x |
| `industry/mariomka-email-count` | `Count` | 684.211 us | 447.030 us | 7,500.830 us | 1.53x |
| `common/replace-words` | `Replace` | 21.013 us | 16.386 us | 17.132 us | 1.28x |
| `industry/leipzig-river-window-count` | `Count` | 1,703.473 us | 1,448.411 us | 7,802.720 us | 1.18x |
| `common/matches-words` | `Count` | 8.754 us | 10.358 us | 13.523 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 966.324 us | 1,170.691 us | 6,956.880 us | 0.83x |
| `common/split-words` | `Split` | 14.001 us | 19.649 us | 21.397 us | 0.71x |
| `industry/mariomka-ip-count` | `Count` | 2,113.614 us | 3,321.874 us | 10,283.100 us | 0.64x |
| `common/matches-set` | `Count` | 59.032 us | 100.400 us | 101.282 us | 0.59x |
| `common/one-node-backtracking` | `IsMatch` | 0.048 us | 0.086 us | 0.123 us | 0.56x |
| `common/matches-word` | `Count` | 1.206 us | 2.252 us | 3.278 us | 0.54x |
| `industry/leipzig-twain-count` | `Count` | 608.499 us | 1,253.749 us | 7,142.690 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.036 us | 0.085 us | 0.113 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 970.768 us | 2,838.137 us | 7,814.500 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 163.476 us | 576.378 us | 823.913 us | 0.28x |
| `common/matches-boundary` | `Count` | 24.753 us | 99.897 us | 101.523 us | 0.25x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,110.787 us | 4,568.953 us | 4,923.663 us | 0.24x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,791.679 us | 8,378.289 us | 8,828.944 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 316.337 us | 3,789.935 us | 4,146.763 us | 0.08x |
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
| `lokad/style/hex-color` | 3.820 us | 0.733 us | 0.808 us | 5.21x |
| `lokad/docs/inline-doc-prefix` | 17.299 us | 4.454 us | 5.051 us | 3.88x |
| `lokad/lexer/doc-line` | 11.053 us | 2.858 us | 3.365 us | 3.87x |
| `lokad/langserv/color-short-hex` | 4.903 us | 1.274 us | 1.357 us | 3.85x |
| `lokad/langserv/identifier-validator` | 47.382 us | 12.332 us | 13.433 us | 3.84x |
| `lokad/style/cell-ref` | 9.472 us | 2.634 us | 2.803 us | 3.60x |
| `lokad/lexer/identifier` | 74.925 us | 21.014 us | 23.137 us | 3.57x |
| `lokad/langserv/helper-identifier` | 23.079 us | 6.697 us | 7.250 us | 3.45x |
| `lokad/lexer/operator-run` | 10.644 us | 3.299 us | 3.494 us | 3.23x |
| `lokad/style/range-ref` | 7.931 us | 2.467 us | 2.559 us | 3.21x |
| `lokad/lexer/number` | 41.614 us | 13.123 us | 13.984 us | 3.17x |
| `lokad/lexer/string` | 245.447 us | 88.683 us | 93.106 us | 2.77x |
| `lokad/langserv/color-rgb-hex` | 7.437 us | 2.766 us | 2.757 us | 2.69x |
| `structural/ordered-modifier-pair` | 41.330 us | 16.329 us | 28.772 us | 2.53x |
| `structural/method-family-generic-call` | 13.859 us | 6.181 us | 18.631 us | 2.24x |
| `fallback/lookahead` | 11.771 us | 5.644 us | 17.743 us | 2.09x |
| `structural/method-family-call` | 15.401 us | 7.545 us | 19.792 us | 2.04x |
| `lokad/langserv/url-dashboard` | 46.159 us | 23.864 us | 24.177 us | 1.93x |
| `structural/ordered-keyword-pair` | 8.957 us | 4.906 us | 18.298 us | 1.83x |
| `lokad/langserv/url-download` | 81.924 us | 49.658 us | 50.217 us | 1.65x |
| `lokad/imports/module-imports` | 8.605 us | 5.862 us | 29.211 us | 1.47x |
| `structural/keyword-to-generic-type` | 5.241 us | 3.895 us | 17.279 us | 1.35x |
| `structural/ordered-keyword-window` | 20.471 us | 16.755 us | 29.320 us | 1.22x |
| `literal/identifier-token` | 2.537 us | 4.055 us | 16.496 us | 0.63x |
| `structural/ordered-identifier-window` | 13.970 us | 26.898 us | 40.377 us | 0.52x |
| `structural/modifier-family-to-type-window` | 117.027 us | 239.327 us | 252.071 us | 0.49x |
| `structural/modifier-family-to-type-pair` | 59.772 us | 123.465 us | 136.487 us | 0.48x |
| `literal/call-token` | 1.835 us | 4.058 us | 16.489 us | 0.45x |
| `literal-family/type-token-family` | 9.133 us | 21.667 us | 34.795 us | 0.42x |
| `literal-family/method-token-family` | 3.579 us | 8.594 us | 21.192 us | 0.42x |
| `balancing/begin-end-blocks` | 1,452.622 us | 3,897.995 us | 3,900.150 us | 0.37x |
| `structural/keyword-family-to-capitalized-identifier` | 63.020 us | 208.755 us | 221.791 us | 0.30x |
| `literal/identifier-token-casei` | 3.798 us | 14.522 us | 27.407 us | 0.26x |
| `lokad/folding/region-marker` | 4.462 us | 32.394 us | 59.175 us | 0.14x |
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
| `structural/keyword-family-to-capitalized-identifier` | 62.957 us | 7.281 us | 23.169 us | 8.65x |
| `lokad/style/hex-color` | 3.795 us | 0.456 us | 0.557 us | 8.32x |
| `lokad/docs/inline-doc-prefix` | 17.252 us | 2.499 us | 3.252 us | 6.90x |
| `lokad/langserv/url-download` | 83.930 us | 12.361 us | 12.848 us | 6.79x |
| `lokad/lexer/number` | 41.431 us | 6.430 us | 7.577 us | 6.44x |
| `lokad/langserv/url-dashboard` | 45.109 us | 7.059 us | 7.554 us | 6.39x |
| `structural/modifier-family-to-type-pair` | 59.514 us | 9.976 us | 23.143 us | 5.97x |
| `lokad/lexer/operator-run` | 10.561 us | 1.776 us | 2.367 us | 5.95x |
| `lokad/style/cell-ref` | 9.436 us | 1.599 us | 1.797 us | 5.90x |
| `lokad/lexer/string` | 245.102 us | 42.226 us | 44.836 us | 5.80x |
| `lokad/style/range-ref` | 7.904 us | 1.363 us | 1.496 us | 5.80x |
| `lokad/langserv/helper-identifier` | 23.180 us | 4.135 us | 5.208 us | 5.61x |
| `lokad/langserv/color-short-hex` | 4.907 us | 0.888 us | 1.061 us | 5.52x |
| `lokad/lexer/doc-line` | 11.065 us | 2.007 us | 2.655 us | 5.51x |
| `lokad/langserv/identifier-validator` | 47.483 us | 8.937 us | 10.737 us | 5.31x |
| `structural/ordered-modifier-pair` | 41.243 us | 7.923 us | 20.447 us | 5.21x |
| `lokad/lexer/identifier` | 75.086 us | 15.037 us | 17.769 us | 4.99x |
| `lokad/langserv/color-rgb-hex` | 7.457 us | 1.747 us | 1.901 us | 4.27x |
| `balancing/begin-end-blocks` | 1,450.506 us | 438.594 us | 467.095 us | 3.31x |
| `structural/method-family-call` | 15.165 us | 4.674 us | 16.448 us | 3.24x |
| `structural/method-family-generic-call` | 13.884 us | 4.342 us | 16.200 us | 3.20x |
| `fallback/lookahead` | 11.799 us | 3.971 us | 15.767 us | 2.97x |
| `structural/ordered-keyword-window` | 20.508 us | 8.113 us | 20.461 us | 2.53x |
| `structural/ordered-keyword-pair` | 8.989 us | 3.839 us | 16.499 us | 2.34x |
| `structural/modifier-family-to-type-window` | 117.154 us | 52.589 us | 66.276 us | 2.23x |
| `lokad/imports/module-imports` | 8.634 us | 4.039 us | 26.826 us | 2.14x |
| `structural/keyword-to-generic-type` | 5.238 us | 3.289 us | 16.145 us | 1.59x |
| `structural/ordered-identifier-window` | 14.004 us | 9.968 us | 23.395 us | 1.40x |
| `literal-family/type-token-family` | 9.193 us | 10.802 us | 23.322 us | 0.85x |
| `literal-family/method-token-family` | 3.624 us | 4.813 us | 17.139 us | 0.75x |
| `literal/identifier-token-casei` | 3.816 us | 5.549 us | 18.240 us | 0.69x |
| `literal/identifier-token` | 2.573 us | 4.444 us | 16.290 us | 0.58x |
| `literal/call-token` | 1.831 us | 3.714 us | 15.571 us | 0.49x |
| `lokad/folding/region-marker` | 4.445 us | 34.108 us | 59.185 us | 0.13x |
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
