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
| `literal/sherlock-zh` | 21.253 us | 17.653 us | 2,039.108 us | 1.20x |
| `aws-keys/full` | 66,898.250 us | 83,255.700 us | 112,902.800 us | 0.80x |
| `literal/sherlock-ru` | 63.969 us | 92.145 us | 5,175.753 us | 0.69x |
| `literal-alternate/sherlock-zh` | 39.856 us | 64.076 us | 2,069.051 us | 0.62x |
| `literal/sherlock-casei-en` | 102.794 us | 172.323 us | 533.078 us | 0.60x |
| `ruff-noqa/tweaked` | 3,494.567 us | 7,160.100 us | 42,078.850 us | 0.49x |
| `aws-keys/quick` | 4,124.494 us | 8,906.575 us | 37,533.350 us | 0.46x |
| `literal/sherlock-casei-ru` | 138.849 us | 339.824 us | 5,215.029 us | 0.41x |
| `bounded-repeat/letters-en` | 1,046.599 us | 2,686.000 us | 2,810.507 us | 0.39x |
| `literal/sherlock-en` | 29.625 us | 80.148 us | 425.167 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,927.649 us | 5,589.593 us | 6,432.369 us | 0.34x |
| `dictionary/single` | 7,553.364 us | 23,911.875 us | 24,203.775 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,351.477 us | 4,435.329 us | 9,271.575 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 147.822 us | 561.214 us | 970.547 us | 0.26x |
| `literal-alternate/sherlock-en` | 326.382 us | 1,465.275 us | 1,954.232 us | 0.22x |
| `bounded-repeat/capitals` | 8,643.444 us | 41,951.800 us | 45,537.550 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 119.863 us | 608.311 us | 1,004.577 us | 0.20x |
| `bounded-repeat/context` | 20,145.900 us | 139,400.500 us | 144,218.800 us | 0.14x |
| `ruff-noqa/real` | 3,082.044 us | 65,826.350 us | 96,527.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 811.534 us | 21,648.625 us | 26,611.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 127.366 us | 10,737.837 us | 11,525.800 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.306 us | 0.072 us | 0.085 us | 4.27x |
| `common/date-match` | `IsMatch` | 0.379 us | 0.101 us | 0.126 us | 3.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.294 us | 0.079 us | 0.094 us | 3.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.364 us | 0.104 us | 0.137 us | 3.49x |
| `common/date-miss` | `IsMatch` | 1.754 us | 0.530 us | 0.560 us | 3.31x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.246 us | 0.077 us | 0.093 us | 3.20x |
| `common/uri-match` | `IsMatch` | 0.569 us | 0.220 us | 0.256 us | 2.58x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.363 us | 0.161 us | 0.177 us | 2.26x |
| `common/email-match` | `IsMatch` | 0.468 us | 0.244 us | 0.270 us | 1.92x |
| `industry/mariomka-uri-count` | `Count` | 3,020.460 us | 1,715.743 us | 9,420.200 us | 1.76x |
| `common/ip-miss` | `IsMatch` | 1.094 us | 0.688 us | 0.704 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.212 us | 0.165 us | 0.183 us | 1.29x |
| `common/uri-miss` | `IsMatch` | 0.185 us | 0.179 us | 0.197 us | 1.03x |
| `common/match-word` | `Match` | 0.148 us | 0.158 us | 1.030 us | 0.94x |
| `industry/mariomka-email-count` | `Count` | 431.601 us | 479.096 us | 8,320.833 us | 0.90x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 136.041 us | 243.222 us | 460.603 us | 0.56x |
| `industry/leipzig-twain-count` | `Count` | 591.931 us | 1,235.620 us | 8,332.067 us | 0.48x |
| `common/matches-set` | `Count` | 57.084 us | 180.814 us | 183.399 us | 0.32x |
| `common/matches-word` | `Count` | 1.196 us | 3.972 us | 5.510 us | 0.30x |
| `common/backtracking` | `IsMatch` | 0.226 us | 0.763 us | 0.808 us | 0.30x |
| `common/replace-words` | `Replace` | 20.433 us | 71.560 us | 72.446 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,099.084 us | 7,852.400 us | 16,203.300 us | 0.27x |
| `common/split-words` | `Split` | 13.569 us | 73.171 us | 74.325 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.627 us | 908.404 us | 1,181.625 us | 0.18x |
| `common/matches-boundary` | `Count` | 25.644 us | 153.195 us | 154.203 us | 0.17x |
| `common/matches-words` | `Count` | 8.459 us | 64.461 us | 65.555 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.035 us | 0.321 us | 0.343 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 980.159 us | 9,152.056 us | 16,061.233 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,867.359 us | 17,583.340 us | 18,098.260 us | 0.11x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,109.928 us | 10,942.586 us | 11,802.114 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.044 us | 0.574 us | 0.602 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,687.507 us | 32,586.433 us | 39,499.350 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 947.989 us | 21,172.025 us | 27,809.600 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 290.917 us | 7,744.030 us | 8,279.078 us | 0.04x |
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
| `aws-keys/full` | 66,480.350 us | 17,423.520 us | 47,317.567 us | 3.82x |
| `literal/sherlock-casei-en` | 103.153 us | 74.070 us | 402.720 us | 1.39x |
| `aws-keys/quick` | 4,329.837 us | 3,534.227 us | 33,317.267 us | 1.23x |
| `literal-alternate/sherlock-zh` | 39.966 us | 33.303 us | 2,067.182 us | 1.20x |
| `literal-alternate/sherlock-casei-en` | 128.769 us | 123.801 us | 525.736 us | 1.04x |
| `bounded-repeat/letters-ru` | 1,918.855 us | 2,022.271 us | 2,813.744 us | 0.95x |
| `literal/sherlock-zh` | 21.071 us | 22.490 us | 2,059.839 us | 0.94x |
| `literal-alternate/sherlock-en` | 326.969 us | 352.108 us | 711.953 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 149.687 us | 169.470 us | 542.904 us | 0.88x |
| `ruff-noqa/tweaked` | 3,583.367 us | 4,312.867 us | 36,224.100 us | 0.83x |
| `literal/sherlock-ru` | 64.340 us | 84.711 us | 5,047.353 us | 0.76x |
| `bounded-repeat/capitals` | 8,444.511 us | 12,578.214 us | 15,689.475 us | 0.67x |
| `literal/sherlock-casei-ru` | 138.762 us | 208.076 us | 5,082.687 us | 0.67x |
| `literal-alternate/sherlock-en-nomatch` | 118.305 us | 189.126 us | 549.314 us | 0.63x |
| `bounded-repeat/letters-en` | 1,050.743 us | 1,727.613 us | 1,834.007 us | 0.61x |
| `literal-alternate/sherlock-ru` | 1,343.553 us | 2,211.185 us | 6,993.575 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 813.726 us | 1,833.238 us | 6,897.100 us | 0.44x |
| `literal/sherlock-en` | 29.361 us | 69.855 us | 401.660 us | 0.42x |
| `bounded-repeat/context` | 20,352.600 us | 64,444.950 us | 67,637.150 us | 0.32x |
| `dictionary/single` | 7,518.580 us | 42,605.400 us | 41,930.350 us | 0.18x |
| `ruff-noqa/real` | 3,099.532 us | 19,109.480 us | 48,590.000 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-date-match` | `IsMatch` | 0.292 us | 0.029 us | 0.047 us | 10.09x |
| `common/date-match` | `IsMatch` | 0.382 us | 0.040 us | 0.068 us | 9.58x |
| `common/date-miss` | `IsMatch` | 1.752 us | 0.186 us | 0.224 us | 9.41x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.306 us | 0.034 us | 0.049 us | 9.02x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.247 us | 0.029 us | 0.046 us | 8.52x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.356 us | 0.047 us | 0.081 us | 7.58x |
| `common/uri-match` | `IsMatch` | 0.572 us | 0.083 us | 0.114 us | 6.92x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.361 us | 0.053 us | 0.071 us | 6.85x |
| `common/ip-miss` | `IsMatch` | 1.094 us | 0.167 us | 0.191 us | 6.54x |
| `common/email-match` | `IsMatch` | 0.470 us | 0.075 us | 0.102 us | 6.23x |
| `common/backtracking` | `IsMatch` | 0.227 us | 0.041 us | 0.069 us | 5.54x |
| `common/ip-match` | `IsMatch` | 0.208 us | 0.046 us | 0.065 us | 4.53x |
| `industry/mariomka-uri-count` | `Count` | 2,986.888 us | 1,005.014 us | 7,658.550 us | 2.97x |
| `common/uri-miss` | `IsMatch` | 0.185 us | 0.064 us | 0.081 us | 2.91x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 136.535 us | 54.458 us | 263.226 us | 2.51x |
| `common/match-word` | `Match` | 0.147 us | 0.070 us | 0.931 us | 2.09x |
| `common/replace-words` | `Replace` | 20.587 us | 15.770 us | 16.586 us | 1.31x |
| `industry/leipzig-river-window-count` | `Count` | 1,703.874 us | 1,455.243 us | 7,407.900 us | 1.17x |
| `industry/mariomka-email-count` | `Count` | 428.874 us | 452.748 us | 7,367.720 us | 0.95x |
| `common/matches-words` | `Count` | 8.475 us | 10.096 us | 12.975 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 951.195 us | 1,178.049 us | 6,583.060 us | 0.81x |
| `common/split-words` | `Split` | 13.553 us | 19.513 us | 21.516 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,113.086 us | 3,251.213 us | 10,192.567 us | 0.65x |
| `common/matches-set` | `Count` | 57.510 us | 97.560 us | 99.061 us | 0.59x |
| `common/matches-word` | `Count` | 1.185 us | 2.178 us | 3.173 us | 0.54x |
| `common/one-node-backtracking` | `IsMatch` | 0.044 us | 0.083 us | 0.119 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 595.195 us | 1,217.049 us | 7,230.940 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.036 us | 0.084 us | 0.110 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 984.171 us | 2,909.952 us | 7,556.400 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 159.212 us | 563.941 us | 799.075 us | 0.28x |
| `common/matches-boundary` | `Count` | 25.631 us | 98.367 us | 100.430 us | 0.26x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,102.730 us | 4,386.011 us | 4,924.312 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,860.675 us | 8,261.744 us | 8,827.344 us | 0.23x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 292.363 us | 3,741.457 us | 4,114.679 us | 0.08x |
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
| `lokad/docs/inline-doc-prefix` | 12.343 us | 4.453 us | 5.029 us | 2.77x |
| `lokad/lexer/string` | 239.900 us | 88.837 us | 94.582 us | 2.70x |
| `lokad/style/cell-ref` | 6.768 us | 2.612 us | 2.789 us | 2.59x |
| `lokad/style/range-ref` | 6.361 us | 2.494 us | 2.532 us | 2.55x |
| `structural/ordered-modifier-pair` | 38.815 us | 16.214 us | 28.870 us | 2.39x |
| `lokad/lexer/number` | 31.221 us | 13.249 us | 14.256 us | 2.36x |
| `lokad/lexer/doc-line` | 6.731 us | 2.857 us | 3.421 us | 2.36x |
| `lokad/langserv/identifier-validator` | 27.450 us | 12.010 us | 12.964 us | 2.29x |
| `lokad/lexer/identifier` | 48.374 us | 21.778 us | 23.806 us | 2.22x |
| `lokad/lexer/operator-run` | 7.105 us | 3.242 us | 3.574 us | 2.19x |
| `structural/method-family-generic-call` | 13.616 us | 6.234 us | 18.560 us | 2.18x |
| `lokad/langserv/color-short-hex` | 2.761 us | 1.268 us | 1.338 us | 2.18x |
| `lokad/langserv/helper-identifier` | 14.670 us | 6.745 us | 7.264 us | 2.18x |
| `fallback/lookahead` | 11.333 us | 5.554 us | 17.489 us | 2.04x |
| `structural/method-family-call` | 15.370 us | 7.555 us | 19.733 us | 2.03x |
| `lokad/style/hex-color` | 1.541 us | 0.772 us | 0.793 us | 2.00x |
| `lokad/langserv/color-rgb-hex` | 5.394 us | 2.813 us | 2.822 us | 1.92x |
| `structural/ordered-keyword-pair` | 8.549 us | 4.970 us | 18.082 us | 1.72x |
| `lokad/imports/module-imports` | 8.557 us | 6.011 us | 30.025 us | 1.42x |
| `structural/keyword-to-generic-type` | 4.954 us | 3.757 us | 17.029 us | 1.32x |
| `lokad/langserv/url-dashboard` | 29.731 us | 23.039 us | 23.453 us | 1.29x |
| `structural/ordered-keyword-window` | 19.091 us | 16.246 us | 28.696 us | 1.18x |
| `lokad/langserv/url-download` | 45.449 us | 49.690 us | 50.044 us | 0.91x |
| `literal/identifier-token` | 2.566 us | 4.101 us | 16.959 us | 0.63x |
| `structural/ordered-identifier-window` | 14.017 us | 26.879 us | 40.749 us | 0.52x |
| `structural/modifier-family-to-type-pair` | 61.016 us | 123.189 us | 137.682 us | 0.50x |
| `structural/modifier-family-to-type-window` | 110.002 us | 238.847 us | 254.657 us | 0.46x |
| `literal/call-token` | 1.823 us | 3.997 us | 16.497 us | 0.46x |
| `literal-family/method-token-family` | 3.558 us | 8.483 us | 21.018 us | 0.42x |
| `literal-family/type-token-family` | 9.220 us | 22.456 us | 35.827 us | 0.41x |
| `balancing/begin-end-blocks` | 1,473.594 us | 3,862.060 us | 3,871.975 us | 0.38x |
| `structural/keyword-family-to-capitalized-identifier` | 61.813 us | 206.701 us | 216.452 us | 0.30x |
| `literal/identifier-token-casei` | 3.813 us | 13.959 us | 26.358 us | 0.27x |
| `lokad/folding/region-marker` | 4.601 us | 33.534 us | 60.114 us | 0.14x |
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
| `structural/keyword-family-to-capitalized-identifier` | 62.079 us | 7.332 us | 19.781 us | 8.47x |
| `structural/modifier-family-to-type-pair` | 61.168 us | 10.155 us | 22.964 us | 6.02x |
| `lokad/lexer/string` | 238.470 us | 42.300 us | 45.655 us | 5.64x |
| `lokad/docs/inline-doc-prefix` | 12.315 us | 2.535 us | 3.308 us | 4.86x |
| `structural/ordered-modifier-pair` | 38.375 us | 8.048 us | 20.688 us | 4.77x |
| `lokad/style/range-ref` | 6.326 us | 1.345 us | 1.452 us | 4.70x |
| `lokad/lexer/number` | 31.194 us | 6.722 us | 7.799 us | 4.64x |
| `lokad/style/cell-ref` | 6.675 us | 1.583 us | 1.790 us | 4.22x |
| `lokad/langserv/url-dashboard` | 29.470 us | 6.993 us | 7.535 us | 4.21x |
| `lokad/lexer/operator-run` | 7.146 us | 1.821 us | 2.389 us | 3.93x |
| `lokad/langserv/url-download` | 44.815 us | 12.260 us | 12.804 us | 3.66x |
| `lokad/langserv/helper-identifier` | 14.756 us | 4.274 us | 5.226 us | 3.45x |
| `balancing/begin-end-blocks` | 1,482.202 us | 438.234 us | 463.292 us | 3.38x |
| `lokad/langserv/identifier-validator` | 27.542 us | 8.210 us | 10.258 us | 3.35x |
| `lokad/style/hex-color` | 1.555 us | 0.472 us | 0.552 us | 3.30x |
| `lokad/lexer/doc-line` | 6.669 us | 2.086 us | 2.751 us | 3.20x |
| `structural/method-family-call` | 15.161 us | 4.755 us | 16.546 us | 3.19x |
| `lokad/langserv/color-rgb-hex` | 5.407 us | 1.697 us | 1.877 us | 3.19x |
| `structural/method-family-generic-call` | 13.594 us | 4.274 us | 16.113 us | 3.18x |
| `lokad/lexer/identifier` | 48.341 us | 15.250 us | 18.855 us | 3.17x |
| `lokad/langserv/color-short-hex` | 2.764 us | 0.911 us | 1.065 us | 3.03x |
| `fallback/lookahead` | 11.282 us | 3.892 us | 15.488 us | 2.90x |
| `structural/ordered-keyword-window` | 18.891 us | 7.873 us | 20.078 us | 2.40x |
| `structural/ordered-keyword-pair` | 8.591 us | 3.914 us | 16.295 us | 2.20x |
| `structural/modifier-family-to-type-window` | 110.892 us | 52.734 us | 66.164 us | 2.10x |
| `lokad/imports/module-imports` | 8.508 us | 4.145 us | 27.188 us | 2.05x |
| `structural/keyword-to-generic-type` | 4.896 us | 3.223 us | 15.921 us | 1.52x |
| `structural/ordered-identifier-window` | 14.139 us | 9.937 us | 23.198 us | 1.42x |
| `literal-family/type-token-family` | 9.328 us | 10.828 us | 23.473 us | 0.86x |
| `literal-family/method-token-family` | 3.613 us | 4.911 us | 17.191 us | 0.74x |
| `literal/identifier-token-casei` | 3.812 us | 5.589 us | 18.159 us | 0.68x |
| `literal/identifier-token` | 2.582 us | 4.423 us | 16.684 us | 0.58x |
| `literal/call-token` | 1.839 us | 3.677 us | 15.681 us | 0.50x |
| `lokad/folding/region-marker` | 4.571 us | 34.756 us | 60.188 us | 0.13x |
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
