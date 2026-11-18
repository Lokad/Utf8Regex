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

The **18 November 2026** refresh measured source `fc72da76` on **.NET 11.0.0**,
Ubuntu 24.04.4 x64 and an Intel Xeon Platinum 8573C. The Release build, .NET 10/11
tests, all-profile catalogs, paired comparisons, lifecycle, scaling and resource
checks completed and verified in **57m02s**.

The measured operations start with valid, unchanged UTF-8 and include the required
result materialization. [The Core summary](bench/Snapshots/Core.json) records each
row's source, qualification and snapshot identity. [Generality measurements](docs/BENCHMARKS.md)
cover ordinary patterns and calls with finite limits, deadlines and cancellation.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

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
| `aws-keys/full` | 3,345,480.000 us | 86,660.000 us | 120,317.300 us | 38.60x |
| `bounded-repeat/capitals` | 137,039.300 us | 45,650.700 us | 49,100.400 us | 3.00x |
| `literal/sherlock-zh` | 22.680 us | 18.987 us | 2,171.283 us | 1.19x |
| `literal/sherlock-ru` | 70.247 us | 99.664 us | 5,319.267 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.818 us | 68.216 us | 2,225.221 us | 0.64x |
| `aws-keys/quick` | 5,326.824 us | 9,775.513 us | 46,378.550 us | 0.54x |
| `literal/sherlock-casei-en` | 94.812 us | 202.387 us | 565.326 us | 0.47x |
| `ruff-noqa/tweaked` | 3,404.996 us | 7,793.391 us | 42,053.000 us | 0.44x |
| `literal/sherlock-casei-ru` | 155.510 us | 366.527 us | 5,573.900 us | 0.42x |
| `bounded-repeat/letters-en` | 1,110.001 us | 2,881.878 us | 2,983.152 us | 0.39x |
| `literal/sherlock-en` | 31.965 us | 87.286 us | 470.394 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,971.779 us | 5,860.554 us | 6,666.600 us | 0.34x |
| `dictionary/single` | 8,259.280 us | 25,866.233 us | 25,798.300 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,454.817 us | 4,820.844 us | 10,225.025 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 173.625 us | 598.036 us | 1,026.465 us | 0.29x |
| `literal-alternate/sherlock-en` | 352.369 us | 1,559.743 us | 2,093.269 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 129.774 us | 655.621 us | 1,109.695 us | 0.20x |
| `bounded-repeat/context` | 20,748.525 us | 149,621.600 us | 153,698.400 us | 0.14x |
| `ruff-noqa/real` | 3,410.509 us | 71,239.800 us | 88,117.100 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 860.672 us | 24,025.100 us | 29,314.050 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 135.647 us | 11,263.843 us | 12,151.314 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-ip-count` | `Count` | 869,284.900 us | 8,395.578 us | 16,937.100 us | 103.54x |
| `industry/leipzig-symbol-count` | `Count` | 711,135.600 us | 22,833.375 us | 30,716.950 us | 31.14x |
| `common/uri-miss` | `IsMatch` | 3.708 us | 0.190 us | 0.209 us | 19.52x |
| `industry/mariomka-uri-count` | `Count` | 25,193.467 us | 1,966.638 us | 10,612.767 us | 12.81x |
| `common/uri-match` | `IsMatch` | 2.205 us | 0.233 us | 0.271 us | 9.45x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.594 us | 0.076 us | 0.090 us | 7.81x |
| `common/date-match` | `IsMatch` | 0.799 us | 0.111 us | 0.143 us | 7.22x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.578 us | 0.083 us | 0.099 us | 6.92x |
| `common/date-miss` | `IsMatch` | 3.286 us | 0.566 us | 0.612 us | 5.81x |
| `industry/rust-sherlock-ing-count` | `Count` | 61,571.650 us | 12,128.300 us | 12,588.250 us | 5.08x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.553 us | 0.110 us | 0.148 us | 5.04x |
| `common/backtracking` | `IsMatch` | 3.629 us | 0.847 us | 0.889 us | 4.29x |
| `common/ip-miss` | `IsMatch` | 0.809 us | 0.192 us | 0.213 us | 4.22x |
| `common/ip-match` | `IsMatch` | 0.762 us | 0.181 us | 0.202 us | 4.20x |
| `common/matches-boundary` | `Count` | 648.984 us | 166.112 us | 167.479 us | 3.91x |
| `common/email-match` | `IsMatch` | 0.862 us | 0.271 us | 0.297 us | 3.18x |
| `common/email-miss` | `IsMatch` | 1.048 us | 0.355 us | 0.378 us | 2.95x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.480 us | 0.166 us | 0.183 us | 2.89x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.236 us | 0.085 us | 0.102 us | 2.76x |
| `industry/mariomka-email-count` | `Count` | 905.686 us | 508.748 us | 9,053.225 us | 1.78x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 1,669.156 us | 973.397 us | 1,281.190 us | 1.71x |
| `industry/rust-sherlock-letter-count` | `Count` | 25,043.933 us | 19,724.450 us | 20,291.550 us | 1.27x |
| `common/match-word` | `Match` | 0.159 us | 0.172 us | 1.198 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 656.921 us | 1,381.046 us | 8,956.150 us | 0.48x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 122.276 us | 261.331 us | 506.306 us | 0.47x |
| `common/matches-set` | `Count` | 62.138 us | 195.352 us | 196.438 us | 0.32x |
| `common/matches-word` | `Count` | 1.286 us | 4.282 us | 6.033 us | 0.30x |
| `common/replace-words` | `Replace` | 22.529 us | 78.808 us | 80.414 us | 0.29x |
| `common/split-words` | `Split` | 14.618 us | 80.695 us | 82.194 us | 0.18x |
| `common/matches-words` | `Count` | 9.267 us | 70.386 us | 71.832 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,053.439 us | 10,100.425 us | 17,383.533 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.048 us | 0.617 us | 0.650 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,651.739 us | 32,587.867 us | 40,510.550 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 310.131 us | 8,478.089 us | 8,893.822 us | 0.04x |
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
| `aws-keys/full` | 3,305,849.100 us | 18,897.300 us | 52,092.600 us | 174.94x |
| `bounded-repeat/capitals` | 138,200.600 us | 13,691.233 us | 16,790.750 us | 10.09x |
| `aws-keys/quick` | 5,342.782 us | 3,696.190 us | 39,163.200 us | 1.45x |
| `literal-alternate/sherlock-zh` | 43.966 us | 36.480 us | 2,160.409 us | 1.21x |
| `literal/sherlock-casei-en` | 94.985 us | 79.698 us | 474.443 us | 1.19x |
| `literal-alternate/sherlock-en` | 351.050 us | 346.847 us | 764.567 us | 1.01x |
| `literal-alternate/sherlock-casei-en` | 135.437 us | 134.622 us | 612.291 us | 1.01x |
| `literal-alternate/sherlock-en-mixed` | 172.390 us | 179.826 us | 584.519 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,974.326 us | 2,070.970 us | 2,884.081 us | 0.95x |
| `literal/sherlock-zh` | 22.623 us | 24.272 us | 2,153.411 us | 0.93x |
| `literal/sherlock-ru` | 70.197 us | 89.957 us | 5,229.993 us | 0.78x |
| `ruff-noqa/tweaked` | 3,400.504 us | 4,621.453 us | 38,407.400 us | 0.74x |
| `literal/sherlock-casei-ru` | 155.223 us | 227.064 us | 5,428.953 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 129.798 us | 206.391 us | 662.384 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,455.504 us | 2,361.300 us | 7,567.875 us | 0.62x |
| `bounded-repeat/letters-en` | 1,109.157 us | 1,856.763 us | 1,944.000 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 851.148 us | 1,979.408 us | 7,242.550 us | 0.43x |
| `literal/sherlock-en` | 32.077 us | 76.759 us | 437.031 us | 0.42x |
| `bounded-repeat/context` | 20,767.925 us | 69,459.200 us | 73,172.050 us | 0.30x |
| `dictionary/single` | 8,231.340 us | 46,135.950 us | 46,124.100 us | 0.18x |
| `ruff-noqa/real` | 3,432.805 us | 21,667.375 us | 50,329.167 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/leipzig-symbol-count` | `Count` | 711,443.400 us | 1,248.226 us | 7,977.775 us | 569.96x |
| `industry/mariomka-ip-count` | `Count` | 879,477.500 us | 3,501.250 us | 10,970.267 us | 251.19x |
| `common/backtracking` | `IsMatch` | 3.647 us | 0.042 us | 0.075 us | 86.01x |
| `common/uri-miss` | `IsMatch` | 3.705 us | 0.066 us | 0.089 us | 56.51x |
| `common/uri-match` | `IsMatch` | 2.189 us | 0.089 us | 0.121 us | 24.62x |
| `industry/mariomka-uri-count` | `Count` | 25,126.600 us | 1,101.294 us | 8,572.033 us | 22.82x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.580 us | 0.030 us | 0.048 us | 19.36x |
| `common/date-match` | `IsMatch` | 0.798 us | 0.044 us | 0.074 us | 18.32x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.595 us | 0.033 us | 0.056 us | 17.78x |
| `common/date-miss` | `IsMatch` | 3.281 us | 0.197 us | 0.236 us | 16.68x |
| `common/ip-match` | `IsMatch` | 0.760 us | 0.048 us | 0.069 us | 15.89x |
| `common/ip-miss` | `IsMatch` | 0.798 us | 0.050 us | 0.074 us | 15.86x |
| `industry/rust-sherlock-ing-count` | `Count` | 61,943.800 us | 4,844.075 us | 5,293.067 us | 12.79x |
| `common/email-miss` | `IsMatch` | 1.051 us | 0.089 us | 0.117 us | 11.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.553 us | 0.049 us | 0.088 us | 11.34x |
| `common/email-match` | `IsMatch` | 0.864 us | 0.082 us | 0.108 us | 10.58x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.478 us | 0.054 us | 0.076 us | 8.79x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.235 us | 0.029 us | 0.050 us | 8.18x |
| `common/matches-boundary` | `Count` | 651.928 us | 105.846 us | 107.570 us | 6.16x |
| `industry/rust-sherlock-letter-count` | `Count` | 25,232.600 us | 8,872.056 us | 9,544.562 us | 2.84x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 1,665.350 us | 599.698 us | 866.403 us | 2.78x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 122.498 us | 59.005 us | 257.868 us | 2.08x |
| `common/match-word` | `Match` | 0.158 us | 0.082 us | 1.016 us | 1.92x |
| `industry/mariomka-email-count` | `Count` | 898.287 us | 472.716 us | 7,850.600 us | 1.90x |
| `common/replace-words` | `Replace` | 22.553 us | 17.867 us | 19.469 us | 1.26x |
| `industry/leipzig-river-window-count` | `Count` | 1,687.076 us | 1,592.380 us | 8,647.320 us | 1.06x |
| `common/matches-words` | `Count` | 9.280 us | 10.879 us | 14.180 us | 0.85x |
| `common/split-words` | `Split` | 14.643 us | 21.077 us | 22.912 us | 0.69x |
| `common/matches-set` | `Count` | 62.151 us | 105.890 us | 107.537 us | 0.59x |
| `common/one-node-backtracking` | `IsMatch` | 0.049 us | 0.089 us | 0.129 us | 0.55x |
| `common/matches-word` | `Count` | 1.278 us | 2.350 us | 3.784 us | 0.54x |
| `industry/leipzig-twain-count` | `Count` | 657.370 us | 1,339.375 us | 6,454.600 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,056.150 us | 3,016.135 us | 8,610.175 us | 0.35x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 312.412 us | 4,003.163 us | 4,383.394 us | 0.08x |
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
| `structural/ordered-keyword-window` | 2,200.597 us | 17.617 us | 30.618 us | 124.91x |
| `structural/ordered-modifier-pair` | 2,020.711 us | 17.580 us | 30.684 us | 114.95x |
| `structural/ordered-identifier-window` | 2,284.474 us | 29.163 us | 42.115 us | 78.33x |
| `structural/keyword-to-generic-type` | 23.149 us | 4.112 us | 18.040 us | 5.63x |
| `lokad/style/hex-color` | 4.134 us | 0.851 us | 0.929 us | 4.86x |
| `lokad/lexer/number` | 71.722 us | 14.828 us | 16.121 us | 4.84x |
| `lokad/docs/inline-doc-prefix` | 23.008 us | 4.822 us | 5.812 us | 4.77x |
| `structural/ordered-keyword-pair` | 25.192 us | 5.527 us | 19.408 us | 4.56x |
| `structural/modifier-family-to-type-window` | 936.702 us | 255.432 us | 269.814 us | 3.67x |
| `lokad/langserv/color-short-hex` | 5.008 us | 1.373 us | 1.553 us | 3.65x |
| `lokad/langserv/identifier-validator` | 45.673 us | 12.606 us | 14.832 us | 3.62x |
| `lokad/lexer/doc-line` | 10.913 us | 3.067 us | 3.935 us | 3.56x |
| `lokad/lexer/identifier` | 80.442 us | 23.514 us | 27.356 us | 3.42x |
| `lokad/style/cell-ref` | 9.691 us | 2.951 us | 3.113 us | 3.28x |
| `lokad/langserv/url-dashboard` | 79.926 us | 24.689 us | 25.105 us | 3.24x |
| `lokad/lexer/operator-run` | 10.793 us | 3.485 us | 4.016 us | 3.10x |
| `lokad/langserv/helper-identifier` | 22.896 us | 7.411 us | 8.210 us | 3.09x |
| `lokad/style/range-ref` | 7.867 us | 2.758 us | 2.983 us | 2.85x |
| `lokad/langserv/color-rgb-hex` | 7.841 us | 2.964 us | 3.118 us | 2.65x |
| `lokad/langserv/url-download` | 137.909 us | 54.820 us | 55.403 us | 2.52x |
| `lokad/lexer/string` | 215.579 us | 96.505 us | 102.684 us | 2.23x |
| `structural/method-family-generic-call` | 11.789 us | 6.565 us | 20.554 us | 1.80x |
| `structural/method-family-call` | 14.572 us | 8.118 us | 20.823 us | 1.80x |
| `fallback/lookahead` | 9.720 us | 6.049 us | 18.825 us | 1.61x |
| `balancing/begin-end-blocks` | 6,741.783 us | 4,239.567 us | 4,263.817 us | 1.59x |
| `lokad/imports/module-imports` | 8.012 us | 6.489 us | 31.370 us | 1.23x |
| `structural/modifier-family-to-type-pair` | 83.672 us | 132.913 us | 146.551 us | 0.63x |
| `literal/identifier-token` | 2.765 us | 4.397 us | 17.610 us | 0.63x |
| `literal/call-token` | 1.971 us | 4.343 us | 17.392 us | 0.45x |
| `literal-family/method-token-family` | 3.886 us | 9.350 us | 22.506 us | 0.42x |
| `literal-family/type-token-family` | 9.910 us | 25.117 us | 37.856 us | 0.39x |
| `structural/keyword-family-to-capitalized-identifier` | 65.092 us | 224.119 us | 238.529 us | 0.29x |
| `literal/identifier-token-casei` | 4.083 us | 14.749 us | 28.538 us | 0.28x |
| `lokad/folding/region-marker` | 4.852 us | 37.217 us | 62.999 us | 0.13x |
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
| `structural/ordered-keyword-window` | 2,201.266 us | 8.657 us | 21.311 us | 254.28x |
| `structural/ordered-modifier-pair` | 2,019.066 us | 8.717 us | 21.879 us | 231.62x |
| `structural/ordered-identifier-window` | 2,281.909 us | 10.674 us | 23.747 us | 213.79x |
| `structural/modifier-family-to-type-window` | 964.006 us | 56.132 us | 70.345 us | 17.17x |
| `balancing/begin-end-blocks` | 6,674.217 us | 470.525 us | 501.581 us | 14.18x |
| `lokad/langserv/url-download` | 136.939 us | 13.189 us | 13.591 us | 10.38x |
| `lokad/langserv/url-dashboard` | 79.995 us | 8.125 us | 8.666 us | 9.85x |
| `lokad/lexer/number` | 71.760 us | 7.592 us | 8.994 us | 9.45x |
| `structural/keyword-family-to-capitalized-identifier` | 65.110 us | 7.685 us | 20.946 us | 8.47x |
| `lokad/style/hex-color` | 4.114 us | 0.521 us | 0.633 us | 7.90x |
| `lokad/docs/inline-doc-prefix` | 22.810 us | 2.915 us | 3.887 us | 7.83x |
| `structural/modifier-family-to-type-pair` | 83.846 us | 10.762 us | 23.794 us | 7.79x |
| `structural/keyword-to-generic-type` | 23.165 us | 3.534 us | 16.846 us | 6.55x |
| `structural/ordered-keyword-pair` | 25.226 us | 4.201 us | 17.428 us | 6.00x |
| `lokad/lexer/operator-run` | 10.810 us | 2.100 us | 2.760 us | 5.15x |
| `lokad/style/cell-ref` | 9.672 us | 1.886 us | 2.113 us | 5.13x |
| `lokad/style/range-ref` | 7.824 us | 1.550 us | 1.661 us | 5.05x |
| `lokad/langserv/color-short-hex` | 5.103 us | 1.085 us | 1.255 us | 4.70x |
| `lokad/langserv/helper-identifier` | 22.913 us | 4.876 us | 6.155 us | 4.70x |
| `lokad/langserv/identifier-validator` | 45.451 us | 9.764 us | 12.110 us | 4.65x |
| `lokad/lexer/identifier` | 80.890 us | 17.595 us | 21.884 us | 4.60x |
| `lokad/lexer/string` | 214.299 us | 47.358 us | 51.359 us | 4.53x |
| `lokad/lexer/doc-line` | 10.944 us | 2.436 us | 3.226 us | 4.49x |
| `lokad/langserv/color-rgb-hex` | 7.782 us | 1.913 us | 2.127 us | 4.07x |
| `structural/method-family-call` | 14.518 us | 5.098 us | 17.508 us | 2.85x |
| `structural/method-family-generic-call` | 11.913 us | 4.624 us | 17.579 us | 2.58x |
| `fallback/lookahead` | 9.722 us | 4.241 us | 16.632 us | 2.29x |
| `lokad/imports/module-imports` | 8.025 us | 4.449 us | 28.548 us | 1.80x |
| `literal-family/type-token-family` | 9.942 us | 11.565 us | 24.827 us | 0.86x |
| `literal-family/method-token-family` | 3.877 us | 5.398 us | 18.404 us | 0.72x |
| `literal/identifier-token-casei` | 4.100 us | 5.978 us | 19.476 us | 0.69x |
| `literal/identifier-token` | 2.764 us | 4.730 us | 16.972 us | 0.58x |
| `literal/call-token` | 1.967 us | 3.985 us | 16.471 us | 0.49x |
| `lokad/folding/region-marker` | 4.854 us | 36.727 us | 64.027 us | 0.13x |
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
