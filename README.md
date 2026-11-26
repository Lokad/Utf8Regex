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
| `literal/sherlock-zh` | 22.293 us | 18.899 us | 2,193.391 us | 1.18x |
| `literal/sherlock-ru` | 67.871 us | 99.952 us | 5,301.747 us | 0.68x |
| `literal-alternate/sherlock-zh` | 43.372 us | 68.501 us | 2,186.450 us | 0.63x |
| `ruff-noqa/tweaked` | 4,072.772 us | 7,724.091 us | 48,008.050 us | 0.53x |
| `literal/sherlock-casei-en` | 79.237 us | 188.041 us | 618.151 us | 0.42x |
| `literal/sherlock-casei-ru` | 151.344 us | 367.662 us | 5,570.362 us | 0.41x |
| `bounded-repeat/letters-en` | 1,111.390 us | 2,787.729 us | 2,945.423 us | 0.40x |
| `literal/sherlock-en` | 31.272 us | 84.409 us | 448.691 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,057.259 us | 5,771.377 us | 6,681.258 us | 0.36x |
| `dictionary/single` | 8,272.750 us | 25,760.900 us | 25,806.100 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,462.827 us | 4,935.406 us | 10,214.100 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 155.006 us | 587.236 us | 1,187.581 us | 0.26x |
| `bounded-repeat/capitals` | 11,949.243 us | 45,946.850 us | 49,861.250 us | 0.26x |
| `literal-alternate/sherlock-en` | 347.494 us | 1,477.363 us | 2,023.967 us | 0.24x |
| `aws-keys/quick` | 1,910.247 us | 9,669.475 us | 47,430.500 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 127.153 us | 653.890 us | 1,283.712 us | 0.19x |
| `aws-keys/full` | 8,642.611 us | 88,222.000 us | 123,234.300 us | 0.10x |
| `bounded-repeat/context` | 8,896.711 us | 146,151.500 us | 151,000.000 us | 0.06x |
| `ruff-noqa/real` | 3,490.436 us | 74,092.950 us | 90,214.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 854.320 us | 23,184.300 us | 28,505.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 107.817 us | 11,460.829 us | 12,259.129 us | 0.0094x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.085 us | 0.109 us | 0.145 us | 0.79x |
| `common/ip-match` | `IsMatch` | 0.116 us | 0.173 us | 0.193 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.107 us | 0.137 us | 0.66x |
| `industry/mariomka-email-count` | `Count` | 330.150 us | 502.799 us | 8,975.775 us | 0.66x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.078 us | 0.090 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,194.470 us | 1,846.768 us | 10,152.525 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.084 us | 0.101 us | 0.62x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.081 us | 0.098 us | 0.54x |
| `common/uri-miss` | `IsMatch` | 0.096 us | 0.184 us | 0.202 us | 0.52x |
| `common/email-match` | `IsMatch` | 0.139 us | 0.266 us | 0.291 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.080 us | 0.164 us | 0.182 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 653.848 us | 1,366.531 us | 8,737.275 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.127 us | 1.172 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.234 us | 0.273 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.275 us | 0.721 us | 0.740 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,096.692 us | 8,485.756 us | 17,000.500 us | 0.36x |
| `common/matches-set` | `Count` | 63.555 us | 196.081 us | 198.069 us | 0.32x |
| `common/matches-word` | `Count` | 1.218 us | 4.160 us | 5.924 us | 0.29x |
| `common/replace-words` | `Replace` | 21.309 us | 77.808 us | 78.164 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.031 us | 258.964 us | 527.618 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.552 us | 0.583 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.996 us | 960.041 us | 1,245.032 us | 0.17x |
| `common/split-words` | `Split` | 13.067 us | 76.945 us | 78.136 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.338 us | 172.214 us | 174.307 us | 0.16x |
| `common/matches-words` | `Count` | 9.072 us | 70.855 us | 72.507 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,060.949 us | 9,863.175 us | 17,481.933 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,961.200 us | 19,426.725 us | 20,253.925 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.612 us | 0.651 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.340 us | 0.369 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,642.550 us | 32,179.133 us | 40,026.100 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,010.592 us | 22,540.900 us | 30,123.150 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.835 us | 0.882 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 261.620 us | 8,437.050 us | 8,971.844 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 286.035 us | 12,102.971 us | 12,688.150 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.222 us | 36.264 us | 2,166.640 us | 1.19x |
| `literal-alternate/sherlock-en` | 347.427 us | 336.309 us | 912.978 us | 1.03x |
| `bounded-repeat/letters-ru` | 2,068.167 us | 2,037.714 us | 2,914.604 us | 1.01x |
| `literal/sherlock-casei-en` | 79.790 us | 80.174 us | 479.207 us | 1.00x |
| `literal/sherlock-zh` | 22.272 us | 23.963 us | 2,152.674 us | 0.93x |
| `ruff-noqa/tweaked` | 4,166.268 us | 4,645.753 us | 43,589.350 us | 0.90x |
| `literal-alternate/sherlock-en-mixed` | 155.688 us | 174.043 us | 587.863 us | 0.89x |
| `bounded-repeat/capitals` | 11,993.714 us | 13,565.167 us | 16,594.125 us | 0.88x |
| `literal-alternate/sherlock-casei-en` | 109.329 us | 131.002 us | 769.981 us | 0.83x |
| `literal/sherlock-ru` | 68.341 us | 90.327 us | 5,270.113 us | 0.76x |
| `literal/sherlock-casei-ru` | 156.972 us | 222.660 us | 5,413.986 us | 0.70x |
| `literal-alternate/sherlock-en-nomatch` | 127.342 us | 201.278 us | 621.140 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,456.248 us | 2,332.930 us | 7,524.825 us | 0.62x |
| `bounded-repeat/letters-en` | 1,108.912 us | 1,818.069 us | 1,949.587 us | 0.61x |
| `aws-keys/quick` | 1,925.880 us | 3,605.391 us | 39,685.800 us | 0.53x |
| `aws-keys/full` | 8,639.411 us | 18,467.500 us | 54,770.233 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 852.275 us | 1,953.972 us | 7,048.200 us | 0.44x |
| `literal/sherlock-en` | 31.198 us | 73.759 us | 405.158 us | 0.42x |
| `dictionary/single` | 8,248.240 us | 45,922.850 us | 45,805.550 us | 0.18x |
| `ruff-noqa/real` | 3,516.564 us | 20,921.375 us | 35,055.867 us | 0.17x |
| `bounded-repeat/context` | 8,923.800 us | 68,548.550 us | 71,485.950 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.110 us | 0.048 us | 0.068 us | 2.31x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.042 us | 0.072 us | 1.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.032 us | 0.049 us | 1.52x |
| `common/ip-miss` | `IsMatch` | 0.268 us | 0.181 us | 0.203 us | 1.48x |
| `common/uri-miss` | `IsMatch` | 0.096 us | 0.067 us | 0.085 us | 1.43x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.054 us | 0.076 us | 1.42x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.035 us | 0.052 us | 1.28x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.031 us | 0.047 us | 1.27x |
| `common/replace-words` | `Replace` | 21.415 us | 17.231 us | 18.117 us | 1.24x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.396 us | 58.137 us | 271.664 us | 1.16x |
| `common/match-word` | `Match` | 0.054 us | 0.049 us | 0.962 us | 1.11x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.054 us | 0.048 us | 0.087 us | 1.11x |
| `industry/mariomka-uri-count` | `Count` | 1,197.241 us | 1,093.314 us | 8,288.125 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.090 us | 0.121 us | 1.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,649.337 us | 1,597.432 us | 8,545.600 us | 1.03x |
| `common/email-match` | `IsMatch` | 0.077 us | 0.079 us | 0.108 us | 0.97x |
| `industry/mariomka-ip-count` | `Count` | 3,086.572 us | 3,373.870 us | 10,711.633 us | 0.91x |
| `common/matches-words` | `Count` | 9.055 us | 10.601 us | 13.717 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 1,014.000 us | 1,220.479 us | 6,976.725 us | 0.83x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.043 us | 0.074 us | 0.76x |
| `industry/mariomka-email-count` | `Count` | 328.295 us | 470.049 us | 7,741.250 us | 0.70x |
| `common/split-words` | `Split` | 13.117 us | 20.619 us | 22.739 us | 0.64x |
| `common/matches-set` | `Count` | 63.835 us | 104.838 us | 108.031 us | 0.61x |
| `common/date-miss` | `IsMatch` | 0.108 us | 0.200 us | 0.235 us | 0.54x |
| `common/matches-word` | `Count` | 1.226 us | 2.310 us | 3.488 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 657.915 us | 1,333.675 us | 6,824.533 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,075.480 us | 3,047.762 us | 8,247.400 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.106 us | 0.128 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.424 us | 586.345 us | 878.136 us | 0.29x |
| `common/matches-boundary` | `Count` | 27.088 us | 105.389 us | 106.152 us | 0.26x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,953.808 us | 8,669.167 us | 9,377.350 us | 0.23x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.087 us | 0.115 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 259.054 us | 3,928.858 us | 4,300.506 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 288.762 us | 4,788.025 us | 5,230.960 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.998 us | 6.439 us | 32.480 us | 0.93x |
| `fallback/lookahead` | 5.235 us | 5.989 us | 20.701 us | 0.87x |
| `structural/ordered-keyword-window` | 12.251 us | 17.825 us | 32.131 us | 0.69x |
| `lokad/langserv/color-short-hex` | 0.549 us | 0.813 us | 0.984 us | 0.68x |
| `structural/method-family-call` | 5.218 us | 7.784 us | 21.995 us | 0.67x |
| `structural/method-family-generic-call` | 4.386 us | 6.665 us | 21.415 us | 0.66x |
| `lokad/lexer/identifier` | 8.735 us | 13.391 us | 16.515 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.732 us | 7.406 us | 8.972 us | 0.64x |
| `structural/ordered-modifier-pair` | 11.019 us | 17.447 us | 31.511 us | 0.63x |
| `lokad/lexer/doc-line` | 1.137 us | 1.862 us | 2.588 us | 0.61x |
| `structural/keyword-to-generic-type` | 2.467 us | 4.075 us | 19.332 us | 0.61x |
| `lokad/docs/inline-doc-prefix` | 1.968 us | 3.438 us | 4.234 us | 0.57x |
| `lokad/langserv/helper-identifier` | 2.617 us | 4.695 us | 5.592 us | 0.56x |
| `structural/ordered-keyword-pair` | 2.916 us | 5.235 us | 20.219 us | 0.56x |
| `literal/identifier-token` | 2.234 us | 4.328 us | 18.719 us | 0.52x |
| `lokad/lexer/operator-run` | 1.137 us | 2.326 us | 2.779 us | 0.49x |
| `lokad/langserv/color-rgb-hex` | 0.825 us | 1.798 us | 2.051 us | 0.46x |
| `lokad/style/hex-color` | 0.225 us | 0.514 us | 0.591 us | 0.44x |
| `literal/call-token` | 1.844 us | 4.232 us | 18.522 us | 0.44x |
| `lokad/style/range-ref` | 0.766 us | 1.795 us | 1.942 us | 0.43x |
| `lokad/style/cell-ref` | 0.829 us | 1.953 us | 2.050 us | 0.42x |
| `literal-family/type-token-family` | 9.227 us | 23.334 us | 38.267 us | 0.40x |
| `lokad/lexer/string` | 32.002 us | 81.618 us | 85.525 us | 0.39x |
| `lokad/langserv/url-dashboard` | 9.100 us | 23.339 us | 23.696 us | 0.39x |
| `structural/ordered-identifier-window` | 8.249 us | 28.711 us | 43.729 us | 0.29x |
| `literal-family/method-token-family` | 2.619 us | 9.198 us | 23.769 us | 0.28x |
| `lokad/lexer/number` | 3.046 us | 10.763 us | 11.736 us | 0.28x |
| `literal/identifier-token-casei` | 3.918 us | 15.660 us | 29.997 us | 0.25x |
| `structural/modifier-family-to-type-window` | 60.948 us | 254.326 us | 269.909 us | 0.24x |
| `lokad/langserv/url-download` | 11.202 us | 48.841 us | 49.383 us | 0.23x |
| `lokad/folding/region-marker` | 5.528 us | 36.287 us | 64.045 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 10.097 us | 220.724 us | 237.099 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.883 us | 132.083 us | 146.917 us | 0.02x |
| `balancing/begin-end-blocks` | 46.726 us | 4,370.950 us | 4,363.339 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.032 us | 6.285 us | 6.756 us | 1.44x |
| `structural/ordered-keyword-window` | 12.161 us | 8.559 us | 22.543 us | 1.42x |
| `lokad/docs/inline-doc-prefix` | 1.963 us | 1.426 us | 2.161 us | 1.38x |
| `lokad/imports/module-imports` | 5.983 us | 4.415 us | 29.675 us | 1.36x |
| `structural/keyword-family-to-capitalized-identifier` | 10.054 us | 7.689 us | 22.468 us | 1.31x |
| `structural/ordered-modifier-pair` | 10.972 us | 8.531 us | 22.608 us | 1.29x |
| `fallback/lookahead` | 5.263 us | 4.189 us | 18.458 us | 1.26x |
| `lokad/lexer/operator-run` | 1.116 us | 0.946 us | 1.409 us | 1.18x |
| `lokad/lexer/identifier` | 8.880 us | 7.854 us | 10.601 us | 1.13x |
| `lokad/langserv/identifier-validator` | 4.732 us | 4.251 us | 5.727 us | 1.11x |
| `structural/modifier-family-to-type-window` | 60.724 us | 55.574 us | 72.273 us | 1.09x |
| `lokad/lexer/string` | 32.296 us | 30.230 us | 33.659 us | 1.07x |
| `lokad/style/range-ref` | 0.768 us | 0.727 us | 0.866 us | 1.06x |
| `structural/method-family-call` | 5.241 us | 4.966 us | 18.904 us | 1.06x |
| `lokad/lexer/doc-line` | 1.129 us | 1.086 us | 1.862 us | 1.04x |
| `lokad/langserv/color-short-hex` | 0.495 us | 0.477 us | 0.672 us | 1.04x |
| `lokad/style/cell-ref` | 0.836 us | 0.833 us | 1.068 us | 1.00x |
| `lokad/langserv/helper-identifier` | 2.584 us | 2.678 us | 3.589 us | 0.97x |
| `structural/method-family-generic-call` | 4.400 us | 4.601 us | 18.434 us | 0.96x |
| `lokad/langserv/url-download` | 11.286 us | 12.042 us | 12.702 us | 0.94x |
| `lokad/style/hex-color` | 0.208 us | 0.227 us | 0.311 us | 0.92x |
| `lokad/langserv/color-rgb-hex` | 0.765 us | 0.841 us | 1.109 us | 0.91x |
| `lokad/lexer/number` | 3.091 us | 3.801 us | 4.888 us | 0.81x |
| `literal-family/type-token-family` | 9.207 us | 11.333 us | 25.942 us | 0.81x |
| `structural/ordered-identifier-window` | 8.277 us | 10.587 us | 25.264 us | 0.78x |
| `structural/keyword-to-generic-type` | 2.467 us | 3.414 us | 18.015 us | 0.72x |
| `structural/ordered-keyword-pair` | 2.915 us | 4.117 us | 18.446 us | 0.71x |
| `literal/identifier-token-casei` | 3.904 us | 5.885 us | 20.614 us | 0.66x |
| `literal-family/method-token-family` | 2.629 us | 5.337 us | 19.659 us | 0.49x |
| `literal/identifier-token` | 2.232 us | 4.633 us | 18.302 us | 0.48x |
| `literal/call-token` | 1.831 us | 3.922 us | 17.581 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.897 us | 10.388 us | 25.258 us | 0.28x |
| `lokad/folding/region-marker` | 5.573 us | 36.121 us | 63.413 us | 0.15x |
| `balancing/begin-end-blocks` | 47.489 us | 463.329 us | 498.015 us | 0.10x |
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
