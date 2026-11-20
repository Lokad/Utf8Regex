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
| `literal/sherlock-zh` | 21.767 us | 18.402 us | 2,125.214 us | 1.18x |
| `aws-keys/full` | 67,813.250 us | 83,785.800 us | 117,335.900 us | 0.81x |
| `literal/sherlock-ru` | 68.280 us | 95.343 us | 5,215.480 us | 0.72x |
| `literal-alternate/sherlock-zh` | 42.025 us | 66.655 us | 2,112.391 us | 0.63x |
| `literal/sherlock-casei-en` | 101.130 us | 181.475 us | 556.919 us | 0.56x |
| `aws-keys/quick` | 5,002.372 us | 9,417.188 us | 45,655.700 us | 0.53x |
| `ruff-noqa/tweaked` | 3,905.665 us | 7,702.355 us | 40,574.550 us | 0.51x |
| `literal/sherlock-casei-ru` | 142.183 us | 354.706 us | 5,419.800 us | 0.40x |
| `bounded-repeat/letters-en` | 1,078.301 us | 2,737.568 us | 2,875.435 us | 0.39x |
| `literal/sherlock-en` | 30.521 us | 83.601 us | 449.419 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,926.772 us | 5,623.600 us | 6,566.692 us | 0.34x |
| `dictionary/single` | 7,944.230 us | 25,435.000 us | 25,907.000 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,410.104 us | 4,675.529 us | 10,108.625 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 158.808 us | 602.619 us | 1,014.252 us | 0.26x |
| `bounded-repeat/capitals` | 11,261.871 us | 44,617.800 us | 48,475.350 us | 0.25x |
| `literal-alternate/sherlock-en` | 334.515 us | 1,486.255 us | 1,987.890 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 124.526 us | 625.750 us | 1,048.347 us | 0.20x |
| `bounded-repeat/context` | 14,221.383 us | 143,262.900 us | 148,966.100 us | 0.10x |
| `ruff-noqa/real` | 3,346.078 us | 67,332.100 us | 100,116.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 859.398 us | 22,663.975 us | 27,768.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 124.574 us | 11,051.743 us | 11,880.371 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.120 us | 0.074 us | 0.087 us | 1.61x |
| `common/date-match` | `IsMatch` | 0.166 us | 0.106 us | 0.132 us | 1.57x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.125 us | 0.080 us | 0.094 us | 1.56x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.114 us | 0.081 us | 0.097 us | 1.41x |
| `common/email-match` | `IsMatch` | 0.328 us | 0.260 us | 0.291 us | 1.26x |
| `common/ip-match` | `IsMatch` | 0.202 us | 0.166 us | 0.186 us | 1.21x |
| `common/uri-match` | `IsMatch` | 0.272 us | 0.233 us | 0.269 us | 1.17x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.114 us | 0.106 us | 0.139 us | 1.07x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.159 us | 0.161 us | 0.179 us | 0.99x |
| `common/uri-miss` | `IsMatch` | 0.174 us | 0.194 us | 0.211 us | 0.90x |
| `industry/mariomka-email-count` | `Count` | 421.159 us | 493.284 us | 8,712.650 us | 0.85x |
| `common/ip-miss` | `IsMatch` | 0.608 us | 0.716 us | 0.738 us | 0.85x |
| `industry/mariomka-uri-count` | `Count` | 1,459.287 us | 1,838.926 us | 9,997.325 us | 0.79x |
| `common/date-miss` | `IsMatch` | 0.344 us | 0.537 us | 0.568 us | 0.64x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 144.505 us | 253.551 us | 486.459 us | 0.57x |
| `industry/leipzig-twain-count` | `Count` | 640.477 us | 1,332.908 us | 8,340.922 us | 0.48x |
| `common/match-word` | `Match` | 0.056 us | 0.163 us | 1.065 us | 0.34x |
| `common/matches-set` | `Count` | 60.209 us | 192.704 us | 194.765 us | 0.31x |
| `common/matches-word` | `Count` | 1.199 us | 4.104 us | 5.583 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,172.568 us | 8,095.580 us | 16,523.367 us | 0.27x |
| `common/replace-words` | `Replace` | 20.124 us | 77.552 us | 77.956 us | 0.26x |
| `common/split-words` | `Split` | 12.928 us | 74.812 us | 76.476 us | 0.17x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.159 us | 954.200 us | 1,221.991 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.205 us | 166.764 us | 169.236 us | 0.14x |
| `common/matches-words` | `Count` | 8.823 us | 66.569 us | 67.457 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,025.512 us | 9,542.062 us | 16,269.200 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,815.200 us | 18,825.925 us | 19,226.425 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.029 us | 0.335 us | 0.364 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 952.892 us | 11,449.457 us | 11,981.171 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.589 us | 0.623 us | 0.06x |
| `common/backtracking` | `IsMatch` | 0.048 us | 0.800 us | 0.838 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,748.664 us | 30,691.500 us | 37,680.500 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 1,001.821 us | 22,042.775 us | 28,742.650 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 282.504 us | 7,999.940 us | 8,561.189 us | 0.04x |
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
| `aws-keys/full` | 67,484.750 us | 17,917.120 us | 53,019.200 us | 3.77x |
| `aws-keys/quick` | 5,201.272 us | 3,515.477 us | 36,195.700 us | 1.48x |
| `literal/sherlock-casei-en` | 100.277 us | 76.639 us | 426.694 us | 1.31x |
| `literal-alternate/sherlock-zh` | 42.255 us | 34.889 us | 2,085.795 us | 1.21x |
| `bounded-repeat/letters-ru` | 1,920.310 us | 1,988.315 us | 2,839.915 us | 0.97x |
| `literal-alternate/sherlock-en-mixed` | 161.540 us | 170.928 us | 529.229 us | 0.95x |
| `literal-alternate/sherlock-en` | 333.762 us | 358.147 us | 745.311 us | 0.93x |
| `literal/sherlock-zh` | 21.807 us | 23.456 us | 2,095.069 us | 0.93x |
| `ruff-noqa/tweaked` | 3,908.800 us | 4,450.259 us | 34,110.650 us | 0.88x |
| `literal-alternate/sherlock-casei-en` | 124.856 us | 142.689 us | 589.896 us | 0.88x |
| `bounded-repeat/capitals` | 11,220.800 us | 13,107.083 us | 16,198.750 us | 0.86x |
| `literal/sherlock-ru` | 68.720 us | 86.152 us | 5,211.200 us | 0.80x |
| `literal-alternate/sherlock-en-nomatch` | 124.852 us | 194.311 us | 587.572 us | 0.64x |
| `literal/sherlock-casei-ru` | 140.393 us | 218.790 us | 5,162.273 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,408.472 us | 2,301.774 us | 7,448.017 us | 0.61x |
| `bounded-repeat/letters-en` | 1,080.380 us | 1,787.095 us | 1,902.460 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 864.548 us | 1,926.915 us | 6,836.350 us | 0.45x |
| `literal/sherlock-en` | 30.582 us | 73.273 us | 429.804 us | 0.42x |
| `bounded-repeat/context` | 14,066.233 us | 67,117.050 us | 70,265.650 us | 0.21x |
| `dictionary/single` | 7,949.490 us | 44,397.300 us | 44,264.700 us | 0.18x |
| `ruff-noqa/real` | 3,398.313 us | 19,754.000 us | 54,250.000 us | 0.17x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.200 us | 0.046 us | 0.065 us | 4.34x |
| `common/email-match` | `IsMatch` | 0.329 us | 0.078 us | 0.106 us | 4.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.123 us | 0.030 us | 0.046 us | 4.09x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.114 us | 0.029 us | 0.047 us | 3.89x |
| `common/date-match` | `IsMatch` | 0.165 us | 0.043 us | 0.071 us | 3.87x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.121 us | 0.034 us | 0.051 us | 3.59x |
| `common/ip-miss` | `IsMatch` | 0.601 us | 0.175 us | 0.199 us | 3.43x |
| `common/uri-match` | `IsMatch` | 0.273 us | 0.085 us | 0.116 us | 3.23x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.160 us | 0.055 us | 0.075 us | 2.92x |
| `common/uri-miss` | `IsMatch` | 0.174 us | 0.066 us | 0.084 us | 2.63x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 145.204 us | 55.840 us | 259.378 us | 2.60x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.113 us | 0.048 us | 0.080 us | 2.37x |
| `common/date-miss` | `IsMatch` | 0.347 us | 0.190 us | 0.227 us | 1.83x |
| `industry/mariomka-uri-count` | `Count` | 1,451.252 us | 1,079.586 us | 8,119.925 us | 1.34x |
| `common/replace-words` | `Replace` | 20.136 us | 16.512 us | 17.586 us | 1.22x |
| `industry/leipzig-river-window-count` | `Count` | 1,779.566 us | 1,470.175 us | 7,463.180 us | 1.21x |
| `common/backtracking` | `IsMatch` | 0.048 us | 0.042 us | 0.071 us | 1.14x |
| `industry/mariomka-email-count` | `Count` | 420.989 us | 460.125 us | 7,565.975 us | 0.91x |
| `common/matches-words` | `Count` | 8.787 us | 10.492 us | 13.576 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,006.686 us | 1,215.681 us | 6,731.650 us | 0.83x |
| `common/match-word` | `Match` | 0.056 us | 0.076 us | 0.952 us | 0.74x |
| `industry/mariomka-ip-count` | `Count` | 2,183.032 us | 3,374.457 us | 10,527.000 us | 0.65x |
| `common/split-words` | `Split` | 12.902 us | 20.421 us | 22.139 us | 0.63x |
| `common/matches-set` | `Count` | 60.211 us | 102.237 us | 103.200 us | 0.59x |
| `common/matches-word` | `Count` | 1.201 us | 2.282 us | 3.279 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 640.552 us | 1,299.453 us | 7,281.790 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.087 us | 0.124 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 1,032.877 us | 2,931.788 us | 7,933.875 us | 0.35x |
| `common/email-miss` | `IsMatch` | 0.030 us | 0.085 us | 0.111 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.373 us | 585.299 us | 836.537 us | 0.28x |
| `common/matches-boundary` | `Count` | 22.983 us | 101.108 us | 103.037 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,798.774 us | 8,485.189 us | 8,972.211 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 951.423 us | 4,616.229 us | 5,017.688 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 279.330 us | 3,803.345 us | 4,193.044 us | 0.07x |
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
| `lokad/langserv/identifier-validator` | 24.715 us | 11.983 us | 13.062 us | 2.06x |
| `lokad/docs/inline-doc-prefix` | 9.289 us | 4.537 us | 5.138 us | 2.05x |
| `lokad/lexer/number` | 26.516 us | 13.188 us | 14.537 us | 2.01x |
| `lokad/lexer/doc-line` | 5.864 us | 2.923 us | 3.482 us | 2.01x |
| `fallback/lookahead` | 10.547 us | 5.760 us | 17.907 us | 1.83x |
| `lokad/lexer/identifier` | 40.686 us | 22.356 us | 24.668 us | 1.82x |
| `lokad/style/hex-color` | 1.373 us | 0.777 us | 0.819 us | 1.77x |
| `structural/method-family-generic-call` | 10.494 us | 6.280 us | 19.560 us | 1.67x |
| `lokad/lexer/string` | 155.861 us | 93.602 us | 97.053 us | 1.67x |
| `lokad/langserv/helper-identifier` | 11.196 us | 7.298 us | 7.766 us | 1.53x |
| `lokad/langserv/color-short-hex` | 1.940 us | 1.288 us | 1.386 us | 1.51x |
| `lokad/langserv/color-rgb-hex` | 4.276 us | 2.908 us | 2.822 us | 1.47x |
| `lokad/lexer/operator-run` | 4.896 us | 3.442 us | 3.600 us | 1.42x |
| `lokad/style/range-ref` | 3.491 us | 2.457 us | 2.525 us | 1.42x |
| `lokad/imports/module-imports` | 8.175 us | 6.053 us | 30.181 us | 1.35x |
| `structural/ordered-keyword-window` | 21.213 us | 17.031 us | 29.717 us | 1.25x |
| `lokad/style/cell-ref` | 3.233 us | 2.672 us | 2.866 us | 1.21x |
| `structural/method-family-call` | 9.146 us | 7.710 us | 20.616 us | 1.19x |
| `structural/keyword-to-generic-type` | 3.023 us | 3.937 us | 17.379 us | 0.77x |
| `structural/ordered-keyword-pair` | 3.470 us | 5.300 us | 18.871 us | 0.65x |
| `literal/identifier-token` | 2.648 us | 4.147 us | 17.341 us | 0.64x |
| `structural/ordered-modifier-pair` | 9.828 us | 16.874 us | 29.335 us | 0.58x |
| `structural/ordered-identifier-window` | 14.749 us | 27.462 us | 40.419 us | 0.54x |
| `lokad/langserv/url-dashboard` | 11.755 us | 23.683 us | 23.807 us | 0.50x |
| `literal/call-token` | 1.756 us | 4.139 us | 16.803 us | 0.42x |
| `literal-family/method-token-family` | 3.706 us | 8.855 us | 21.389 us | 0.42x |
| `literal-family/type-token-family` | 9.440 us | 22.873 us | 36.230 us | 0.41x |
| `structural/modifier-family-to-type-window` | 101.780 us | 248.332 us | 261.897 us | 0.41x |
| `balancing/begin-end-blocks` | 1,433.134 us | 4,088.384 us | 4,090.684 us | 0.35x |
| `literal/identifier-token-casei` | 3.764 us | 15.006 us | 27.482 us | 0.25x |
| `lokad/langserv/url-download` | 12.197 us | 50.337 us | 50.404 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 20.304 us | 130.192 us | 142.849 us | 0.16x |
| `lokad/folding/region-marker` | 5.097 us | 34.663 us | 60.934 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 25.789 us | 208.550 us | 225.163 us | 0.12x |
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
| `lokad/lexer/number` | 26.266 us | 6.680 us | 7.856 us | 3.93x |
| `lokad/docs/inline-doc-prefix` | 9.193 us | 2.567 us | 3.324 us | 3.58x |
| `lokad/lexer/string` | 155.520 us | 44.332 us | 46.374 us | 3.51x |
| `structural/keyword-family-to-capitalized-identifier` | 25.888 us | 7.434 us | 20.603 us | 3.48x |
| `balancing/begin-end-blocks` | 1,428.544 us | 448.192 us | 477.691 us | 3.19x |
| `lokad/langserv/identifier-validator` | 24.824 us | 8.561 us | 10.453 us | 2.90x |
| `lokad/style/hex-color` | 1.392 us | 0.493 us | 0.564 us | 2.82x |
| `lokad/lexer/doc-line` | 5.867 us | 2.128 us | 2.850 us | 2.76x |
| `structural/ordered-keyword-window` | 21.250 us | 8.178 us | 20.778 us | 2.60x |
| `fallback/lookahead` | 10.439 us | 4.053 us | 15.818 us | 2.58x |
| `lokad/lexer/identifier` | 40.870 us | 16.156 us | 19.795 us | 2.53x |
| `lokad/lexer/operator-run` | 4.887 us | 1.937 us | 2.406 us | 2.52x |
| `lokad/langserv/color-rgb-hex` | 4.279 us | 1.708 us | 1.885 us | 2.51x |
| `lokad/style/range-ref` | 3.501 us | 1.401 us | 1.522 us | 2.50x |
| `lokad/langserv/helper-identifier` | 11.170 us | 4.707 us | 5.670 us | 2.37x |
| `structural/method-family-generic-call` | 10.484 us | 4.443 us | 16.766 us | 2.36x |
| `lokad/langserv/color-short-hex` | 1.932 us | 0.932 us | 1.113 us | 2.07x |
| `lokad/style/cell-ref` | 3.180 us | 1.594 us | 1.793 us | 1.99x |
| `structural/modifier-family-to-type-pair` | 20.359 us | 10.342 us | 23.147 us | 1.97x |
| `lokad/imports/module-imports` | 8.163 us | 4.202 us | 27.672 us | 1.94x |
| `structural/method-family-call` | 9.172 us | 4.804 us | 17.260 us | 1.91x |
| `structural/modifier-family-to-type-window` | 102.004 us | 53.995 us | 67.394 us | 1.89x |
| `lokad/langserv/url-dashboard` | 11.748 us | 7.269 us | 7.608 us | 1.62x |
| `structural/ordered-identifier-window` | 14.698 us | 10.225 us | 23.005 us | 1.44x |
| `structural/ordered-modifier-pair` | 9.900 us | 8.255 us | 20.858 us | 1.20x |
| `lokad/langserv/url-download` | 12.141 us | 12.342 us | 13.183 us | 0.98x |
| `structural/keyword-to-generic-type` | 2.989 us | 3.352 us | 16.317 us | 0.89x |
| `structural/ordered-keyword-pair` | 3.481 us | 3.996 us | 16.852 us | 0.87x |
| `literal-family/type-token-family` | 9.502 us | 11.025 us | 23.753 us | 0.86x |
| `literal-family/method-token-family` | 3.675 us | 5.111 us | 17.475 us | 0.72x |
| `literal/identifier-token-casei` | 3.804 us | 5.649 us | 18.578 us | 0.67x |
| `literal/identifier-token` | 2.645 us | 4.531 us | 16.654 us | 0.58x |
| `literal/call-token` | 1.765 us | 3.797 us | 15.852 us | 0.46x |
| `lokad/folding/region-marker` | 5.120 us | 35.258 us | 60.937 us | 0.15x |
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
