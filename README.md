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
| `literal/sherlock-zh` | 20.998 us | 17.765 us | 2,121.347 us | 1.18x |
| `literal/sherlock-ru` | 64.968 us | 92.472 us | 4,970.340 us | 0.70x |
| `literal-alternate/sherlock-zh` | 40.709 us | 64.653 us | 2,050.011 us | 0.63x |
| `ruff-noqa/tweaked` | 3,728.305 us | 7,297.191 us | 39,637.050 us | 0.51x |
| `aws-keys/full` | 40,866.850 us | 80,256.800 us | 113,342.100 us | 0.51x |
| `aws-keys/quick` | 4,336.460 us | 9,242.225 us | 38,312.350 us | 0.47x |
| `literal/sherlock-casei-en` | 74.969 us | 177.499 us | 560.041 us | 0.42x |
| `literal/sherlock-casei-ru` | 140.170 us | 344.241 us | 5,205.187 us | 0.41x |
| `bounded-repeat/letters-en` | 1,016.301 us | 2,651.085 us | 2,822.196 us | 0.38x |
| `literal/sherlock-en` | 29.713 us | 80.019 us | 443.601 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,904.559 us | 5,539.093 us | 6,362.367 us | 0.34x |
| `dictionary/single` | 7,760.780 us | 24,810.825 us | 24,885.825 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,383.907 us | 4,615.559 us | 9,615.200 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 147.679 us | 561.611 us | 947.480 us | 0.26x |
| `bounded-repeat/capitals` | 10,988.471 us | 43,292.500 us | 46,592.600 us | 0.25x |
| `literal-alternate/sherlock-en` | 328.320 us | 1,447.883 us | 1,949.990 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 120.077 us | 631.509 us | 1,022.344 us | 0.19x |
| `bounded-repeat/context` | 8,282.170 us | 139,921.900 us | 143,546.100 us | 0.06x |
| `ruff-noqa/real` | 3,286.817 us | 66,842.000 us | 98,029.100 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 827.030 us | 21,640.100 us | 26,646.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 96.030 us | 10,721.400 us | 11,519.600 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.157 us | 0.103 us | 0.130 us | 1.53x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.095 us | 0.072 us | 0.085 us | 1.31x |
| `common/uri-match` | `IsMatch` | 0.280 us | 0.222 us | 0.258 us | 1.27x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.099 us | 0.080 us | 0.096 us | 1.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.093 us | 0.077 us | 0.092 us | 1.20x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.122 us | 0.105 us | 0.142 us | 1.16x |
| `common/ip-match` | `IsMatch` | 0.185 us | 0.164 us | 0.183 us | 1.13x |
| `common/uri-miss` | `IsMatch` | 0.198 us | 0.188 us | 0.208 us | 1.05x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.148 us | 0.159 us | 0.175 us | 0.93x |
| `industry/mariomka-email-count` | `Count` | 405.117 us | 474.122 us | 8,272.380 us | 0.85x |
| `industry/mariomka-uri-count` | `Count` | 1,375.113 us | 1,770.537 us | 9,649.800 us | 0.78x |
| `common/email-match` | `IsMatch` | 0.185 us | 0.255 us | 0.280 us | 0.73x |
| `common/ip-miss` | `IsMatch` | 0.476 us | 0.701 us | 0.724 us | 0.68x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 142.675 us | 249.684 us | 479.080 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.293 us | 0.550 us | 0.587 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 594.692 us | 1,250.222 us | 8,285.233 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.119 us | 1.066 us | 0.44x |
| `common/matches-set` | `Count` | 57.543 us | 183.950 us | 184.818 us | 0.31x |
| `common/matches-word` | `Count` | 1.175 us | 4.007 us | 5.700 us | 0.29x |
| `common/replace-words` | `Replace` | 19.391 us | 72.631 us | 74.455 us | 0.27x |
| `industry/mariomka-ip-count` | `Count` | 2,103.797 us | 7,882.440 us | 16,049.933 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 158.445 us | 902.957 us | 1,161.775 us | 0.18x |
| `common/split-words` | `Split` | 12.547 us | 72.703 us | 74.734 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.330 us | 156.824 us | 158.505 us | 0.15x |
| `common/matches-words` | `Count` | 8.687 us | 67.133 us | 69.090 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 984.838 us | 9,390.956 us | 16,045.525 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,782.781 us | 18,712.760 us | 19,160.450 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.328 us | 0.347 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 864.720 us | 11,207.229 us | 11,867.471 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.783 us | 0.822 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.585 us | 0.615 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,740.542 us | 30,078.600 us | 37,272.900 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 937.205 us | 22,453.450 us | 29,166.300 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 272.161 us | 7,944.600 us | 8,352.189 us | 0.03x |
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
| `aws-keys/full` | 40,664.800 us | 17,413.220 us | 51,375.533 us | 2.34x |
| `aws-keys/quick` | 4,403.407 us | 3,502.577 us | 33,653.967 us | 1.26x |
| `literal-alternate/sherlock-zh` | 40.635 us | 33.744 us | 2,043.666 us | 1.20x |
| `literal-alternate/sherlock-en` | 328.927 us | 324.575 us | 706.894 us | 1.01x |
| `literal/sherlock-casei-en` | 74.930 us | 75.750 us | 429.285 us | 0.99x |
| `bounded-repeat/letters-ru` | 1,903.280 us | 1,958.931 us | 2,796.741 us | 0.97x |
| `literal/sherlock-zh` | 21.022 us | 22.705 us | 2,120.937 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 146.773 us | 167.674 us | 511.234 us | 0.88x |
| `ruff-noqa/tweaked` | 3,760.780 us | 4,298.628 us | 33,884.750 us | 0.87x |
| `bounded-repeat/capitals` | 11,014.514 us | 12,775.517 us | 15,526.450 us | 0.86x |
| `literal/sherlock-ru` | 65.115 us | 84.368 us | 4,960.673 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 95.704 us | 138.294 us | 530.774 us | 0.69x |
| `literal/sherlock-casei-ru` | 139.078 us | 213.490 us | 5,073.580 us | 0.65x |
| `literal-alternate/sherlock-en-nomatch` | 120.284 us | 194.515 us | 560.687 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,380.138 us | 2,240.809 us | 7,160.017 us | 0.62x |
| `bounded-repeat/letters-en` | 1,023.868 us | 1,735.289 us | 1,849.439 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 828.173 us | 1,861.839 us | 6,961.680 us | 0.44x |
| `literal/sherlock-en` | 29.858 us | 70.335 us | 419.628 us | 0.42x |
| `dictionary/single` | 7,759.870 us | 43,255.900 us | 43,308.950 us | 0.18x |
| `ruff-noqa/real` | 3,287.667 us | 19,649.650 us | 49,489.350 us | 0.17x |
| `bounded-repeat/context` | 8,275.460 us | 65,602.900 us | 68,252.900 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.185 us | 0.046 us | 0.065 us | 4.00x |
| `common/date-match` | `IsMatch` | 0.156 us | 0.041 us | 0.070 us | 3.79x |
| `common/uri-match` | `IsMatch` | 0.279 us | 0.082 us | 0.115 us | 3.41x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.097 us | 0.030 us | 0.047 us | 3.29x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.095 us | 0.030 us | 0.045 us | 3.22x |
| `common/uri-miss` | `IsMatch` | 0.197 us | 0.063 us | 0.082 us | 3.14x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.094 us | 0.032 us | 0.050 us | 2.93x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.146 us | 0.053 us | 0.072 us | 2.78x |
| `common/ip-miss` | `IsMatch` | 0.478 us | 0.172 us | 0.194 us | 2.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.122 us | 0.047 us | 0.083 us | 2.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 142.761 us | 56.929 us | 252.322 us | 2.51x |
| `common/email-match` | `IsMatch` | 0.186 us | 0.076 us | 0.101 us | 2.44x |
| `common/date-miss` | `IsMatch` | 0.293 us | 0.190 us | 0.224 us | 1.54x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.042 us | 0.071 us | 1.31x |
| `industry/mariomka-uri-count` | `Count` | 1,377.665 us | 1,070.720 us | 7,909.725 us | 1.29x |
| `industry/leipzig-river-window-count` | `Count` | 1,747.055 us | 1,444.506 us | 7,491.320 us | 1.21x |
| `common/match-word` | `Match` | 0.053 us | 0.045 us | 0.881 us | 1.18x |
| `common/replace-words` | `Replace` | 19.403 us | 16.589 us | 17.376 us | 1.17x |
| `industry/mariomka-email-count` | `Count` | 406.171 us | 442.789 us | 7,452.330 us | 0.92x |
| `common/matches-words` | `Count` | 8.622 us | 10.448 us | 13.314 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 950.677 us | 1,162.875 us | 6,883.400 us | 0.82x |
| `common/split-words` | `Split` | 12.587 us | 19.380 us | 21.290 us | 0.65x |
| `industry/mariomka-ip-count` | `Count` | 2,109.160 us | 3,292.648 us | 10,276.833 us | 0.64x |
| `common/matches-set` | `Count` | 57.580 us | 98.821 us | 100.850 us | 0.58x |
| `common/matches-word` | `Count` | 1.174 us | 2.226 us | 3.224 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 597.554 us | 1,236.156 us | 7,270.818 us | 0.48x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.085 us | 0.122 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 978.494 us | 2,869.267 us | 7,966.917 us | 0.34x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.084 us | 0.109 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 158.971 us | 554.439 us | 797.948 us | 0.29x |
| `common/matches-boundary` | `Count` | 23.217 us | 99.172 us | 100.469 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,791.431 us | 8,354.967 us | 8,875.589 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 869.294 us | 4,538.329 us | 4,927.994 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 272.986 us | 3,747.190 us | 4,089.758 us | 0.07x |
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
| `lokad/lexer/doc-line` | 3.921 us | 1.774 us | 2.482 us | 2.21x |
| `lokad/langserv/helper-identifier` | 9.305 us | 4.540 us | 5.386 us | 2.05x |
| `lokad/langserv/identifier-validator` | 13.506 us | 7.164 us | 8.622 us | 1.89x |
| `lokad/lexer/identifier` | 24.089 us | 13.064 us | 15.872 us | 1.84x |
| `fallback/lookahead` | 10.266 us | 5.652 us | 17.777 us | 1.82x |
| `structural/method-family-generic-call` | 9.661 us | 6.259 us | 19.117 us | 1.54x |
| `lokad/docs/inline-doc-prefix` | 5.001 us | 3.269 us | 4.041 us | 1.53x |
| `structural/method-family-call` | 9.714 us | 7.629 us | 20.170 us | 1.27x |
| `lokad/imports/module-imports` | 7.389 us | 6.057 us | 29.814 us | 1.22x |
| `lokad/lexer/string` | 94.605 us | 78.738 us | 81.949 us | 1.20x |
| `structural/ordered-keyword-window` | 19.565 us | 16.723 us | 29.251 us | 1.17x |
| `lokad/langserv/color-short-hex` | 0.852 us | 0.775 us | 0.976 us | 1.10x |
| `lokad/style/cell-ref` | 1.867 us | 1.816 us | 2.022 us | 1.03x |
| `lokad/lexer/operator-run` | 2.221 us | 2.210 us | 2.652 us | 1.01x |
| `lokad/style/range-ref` | 1.746 us | 1.773 us | 1.880 us | 0.98x |
| `lokad/style/hex-color` | 0.478 us | 0.496 us | 0.572 us | 0.96x |
| `lokad/langserv/color-rgb-hex` | 1.427 us | 1.770 us | 1.974 us | 0.81x |
| `lokad/lexer/number` | 8.206 us | 10.204 us | 11.258 us | 0.80x |
| `structural/keyword-to-generic-type` | 2.869 us | 3.829 us | 16.987 us | 0.75x |
| `structural/ordered-keyword-pair` | 3.386 us | 4.976 us | 17.859 us | 0.68x |
| `structural/ordered-modifier-pair` | 9.893 us | 16.673 us | 29.548 us | 0.59x |
| `literal/identifier-token` | 2.127 us | 4.098 us | 16.542 us | 0.52x |
| `structural/ordered-identifier-window` | 14.081 us | 27.230 us | 40.075 us | 0.52x |
| `lokad/langserv/url-dashboard` | 9.633 us | 22.032 us | 22.508 us | 0.44x |
| `literal/call-token` | 1.752 us | 4.067 us | 16.359 us | 0.43x |
| `literal-family/method-token-family` | 3.610 us | 8.587 us | 21.361 us | 0.42x |
| `literal-family/type-token-family` | 9.238 us | 22.191 us | 35.030 us | 0.42x |
| `structural/modifier-family-to-type-window` | 98.506 us | 242.858 us | 255.464 us | 0.41x |
| `balancing/begin-end-blocks` | 1,279.863 us | 4,103.716 us | 4,125.337 us | 0.31x |
| `lokad/langserv/url-download` | 11.395 us | 48.592 us | 48.675 us | 0.23x |
| `literal/identifier-token-casei` | 3.284 us | 14.274 us | 27.061 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 22.191 us | 127.215 us | 139.945 us | 0.17x |
| `lokad/folding/region-marker` | 4.958 us | 33.767 us | 58.976 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 13.542 us | 210.297 us | 224.763 us | 0.06x |
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
| `lokad/lexer/doc-line` | 3.905 us | 1.003 us | 1.761 us | 3.89x |
| `lokad/langserv/helper-identifier` | 9.336 us | 2.554 us | 3.414 us | 3.66x |
| `lokad/docs/inline-doc-prefix` | 4.985 us | 1.383 us | 2.127 us | 3.61x |
| `lokad/lexer/string` | 95.133 us | 28.942 us | 32.009 us | 3.29x |
| `lokad/lexer/identifier` | 24.138 us | 7.347 us | 10.223 us | 3.29x |
| `lokad/langserv/identifier-validator` | 13.461 us | 4.117 us | 5.655 us | 3.27x |
| `balancing/begin-end-blocks` | 1,283.234 us | 440.956 us | 469.161 us | 2.91x |
| `fallback/lookahead` | 10.246 us | 3.975 us | 15.661 us | 2.58x |
| `lokad/style/range-ref` | 1.752 us | 0.690 us | 0.815 us | 2.54x |
| `lokad/lexer/operator-run` | 2.219 us | 0.875 us | 1.321 us | 2.54x |
| `structural/ordered-keyword-window` | 19.531 us | 8.058 us | 20.545 us | 2.42x |
| `lokad/style/cell-ref` | 1.877 us | 0.786 us | 0.984 us | 2.39x |
| `lokad/lexer/number` | 8.235 us | 3.599 us | 4.672 us | 2.29x |
| `structural/method-family-generic-call` | 9.657 us | 4.349 us | 16.476 us | 2.22x |
| `structural/modifier-family-to-type-pair` | 22.232 us | 10.126 us | 22.461 us | 2.20x |
| `lokad/style/hex-color` | 0.473 us | 0.219 us | 0.300 us | 2.16x |
| `structural/method-family-call` | 9.653 us | 4.767 us | 17.140 us | 2.03x |
| `structural/keyword-family-to-capitalized-identifier` | 13.525 us | 7.245 us | 20.467 us | 1.87x |
| `lokad/langserv/color-short-hex` | 0.858 us | 0.462 us | 0.662 us | 1.86x |
| `structural/modifier-family-to-type-window` | 98.726 us | 53.416 us | 66.218 us | 1.85x |
| `lokad/imports/module-imports` | 7.485 us | 4.191 us | 27.319 us | 1.79x |
| `lokad/langserv/color-rgb-hex` | 1.432 us | 0.804 us | 1.048 us | 1.78x |
| `lokad/langserv/url-dashboard` | 9.598 us | 6.004 us | 6.456 us | 1.60x |
| `structural/ordered-identifier-window` | 14.163 us | 10.066 us | 22.936 us | 1.41x |
| `structural/ordered-modifier-pair` | 9.856 us | 8.191 us | 20.718 us | 1.20x |
| `lokad/langserv/url-download` | 11.436 us | 11.535 us | 12.010 us | 0.99x |
| `structural/keyword-to-generic-type` | 2.906 us | 3.224 us | 15.954 us | 0.90x |
| `structural/ordered-keyword-pair` | 3.379 us | 3.897 us | 16.074 us | 0.87x |
| `literal-family/type-token-family` | 9.232 us | 10.823 us | 23.142 us | 0.85x |
| `literal-family/method-token-family` | 3.606 us | 5.002 us | 17.525 us | 0.72x |
| `literal/identifier-token-casei` | 3.290 us | 5.632 us | 18.215 us | 0.58x |
| `literal/identifier-token` | 2.128 us | 4.429 us | 16.305 us | 0.48x |
| `literal/call-token` | 1.744 us | 3.717 us | 15.382 us | 0.47x |
| `lokad/folding/region-marker` | 4.988 us | 33.424 us | 59.129 us | 0.15x |
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
