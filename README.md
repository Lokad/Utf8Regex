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
| `literal/sherlock-zh` | 13.996 us | 18.774 us | 2,211.612 us | 0.75x |
| `literal/sherlock-ru` | 71.178 us | 97.909 us | 5,217.233 us | 0.73x |
| `literal-alternate/sherlock-zh` | 43.722 us | 68.066 us | 2,208.938 us | 0.64x |
| `ruff-noqa/tweaked` | 4,003.050 us | 7,861.864 us | 45,046.600 us | 0.51x |
| `literal/sherlock-casei-en` | 80.564 us | 187.772 us | 787.999 us | 0.43x |
| `literal/sherlock-casei-ru` | 149.286 us | 364.823 us | 5,603.460 us | 0.41x |
| `bounded-repeat/letters-en` | 1,138.078 us | 2,860.037 us | 3,029.108 us | 0.40x |
| `literal/sherlock-en` | 31.458 us | 86.227 us | 485.512 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,020.903 us | 5,857.208 us | 6,708.667 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,471.911 us | 4,874.381 us | 10,025.400 us | 0.30x |
| `dictionary/single` | 7,693.940 us | 25,958.233 us | 26,053.600 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 175.559 us | 618.632 us | 1,066.581 us | 0.28x |
| `bounded-repeat/capitals` | 11,834.729 us | 45,688.100 us | 48,489.800 us | 0.26x |
| `literal-alternate/sherlock-en` | 346.506 us | 1,542.598 us | 2,077.213 us | 0.22x |
| `aws-keys/quick` | 1,953.162 us | 9,436.622 us | 43,326.050 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 128.659 us | 650.476 us | 1,115.791 us | 0.20x |
| `aws-keys/full` | 8,779.356 us | 87,740.800 us | 121,437.100 us | 0.10x |
| `bounded-repeat/context` | 9,032.611 us | 147,097.900 us | 150,948.400 us | 0.06x |
| `ruff-noqa/real` | 3,450.550 us | 72,958.950 us | 107,881.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 854.331 us | 23,216.250 us | 28,641.150 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 107.281 us | 11,446.129 us | 12,217.786 us | 0.0094x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.087 us | 0.111 us | 0.145 us | 0.78x |
| `common/ip-match` | `IsMatch` | 0.118 us | 0.174 us | 0.193 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.110 us | 0.138 us | 0.65x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.078 us | 0.091 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.084 us | 0.100 us | 0.62x |
| `industry/mariomka-uri-count` | `Count` | 1,212.679 us | 1,963.254 us | 10,635.100 us | 0.62x |
| `industry/mariomka-email-count` | `Count` | 274.719 us | 496.517 us | 8,945.825 us | 0.55x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.082 us | 0.099 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.146 us | 0.271 us | 0.297 us | 0.54x |
| `common/uri-miss` | `IsMatch` | 0.098 us | 0.186 us | 0.205 us | 0.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.082 us | 0.163 us | 0.180 us | 0.50x |
| `industry/leipzig-twain-count` | `Count` | 633.505 us | 1,333.673 us | 8,857.650 us | 0.48x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.231 us | 0.274 us | 0.40x |
| `common/match-word` | `Match` | 0.052 us | 0.129 us | 1.191 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.280 us | 0.717 us | 0.740 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,144.617 us | 8,331.750 us | 16,767.833 us | 0.38x |
| `common/matches-set` | `Count` | 62.124 us | 192.901 us | 196.156 us | 0.32x |
| `common/matches-word` | `Count` | 1.243 us | 4.251 us | 6.019 us | 0.29x |
| `common/replace-words` | `Replace` | 20.586 us | 76.972 us | 79.453 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 65.827 us | 258.753 us | 534.724 us | 0.25x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.558 us | 0.597 us | 0.20x |
| `common/split-words` | `Split` | 14.977 us | 77.839 us | 79.260 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.073 us | 978.127 us | 1,263.648 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.836 us | 167.935 us | 167.536 us | 0.17x |
| `common/matches-words` | `Count` | 9.181 us | 71.869 us | 73.254 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,084.097 us | 9,821.413 us | 17,105.167 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,814.026 us | 19,666.225 us | 20,161.625 us | 0.09x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.626 us | 0.659 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.350 us | 0.376 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,684.196 us | 32,303.500 us | 39,675.600 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,016.116 us | 22,923.275 us | 30,516.600 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.833 us | 0.877 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 258.053 us | 8,608.067 us | 9,054.956 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 287.850 us | 11,729.114 us | 12,176.986 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.773 us | 36.512 us | 2,176.374 us | 1.20x |
| `literal/sherlock-casei-en` | 80.282 us | 79.118 us | 507.556 us | 1.01x |
| `literal-alternate/sherlock-en-mixed` | 177.019 us | 175.553 us | 600.958 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,027.068 us | 2,063.086 us | 2,918.646 us | 0.98x |
| `literal-alternate/sherlock-en` | 346.491 us | 368.193 us | 815.136 us | 0.94x |
| `ruff-noqa/tweaked` | 4,036.100 us | 4,582.465 us | 39,863.050 us | 0.88x |
| `bounded-repeat/capitals` | 11,872.986 us | 13,563.700 us | 16,447.650 us | 0.88x |
| `literal/sherlock-ru` | 71.321 us | 90.146 us | 5,189.580 us | 0.79x |
| `literal/sherlock-casei-ru` | 148.147 us | 195.648 us | 5,469.710 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 106.743 us | 149.490 us | 681.629 us | 0.71x |
| `literal-alternate/sherlock-en-nomatch` | 128.503 us | 198.533 us | 639.727 us | 0.65x |
| `literal-alternate/sherlock-ru` | 1,467.840 us | 2,359.650 us | 7,532.475 us | 0.62x |
| `bounded-repeat/letters-en` | 1,137.876 us | 1,855.057 us | 1,987.521 us | 0.61x |
| `literal/sherlock-zh` | 14.025 us | 24.110 us | 2,212.109 us | 0.58x |
| `aws-keys/quick` | 1,953.913 us | 3,625.319 us | 35,138.450 us | 0.54x |
| `aws-keys/full` | 8,803.222 us | 18,663.900 us | 53,838.400 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 855.586 us | 1,976.138 us | 7,192.250 us | 0.43x |
| `literal/sherlock-en` | 31.322 us | 75.092 us | 465.446 us | 0.42x |
| `dictionary/single` | 7,700.420 us | 46,048.400 us | 46,039.650 us | 0.17x |
| `ruff-noqa/real` | 3,484.177 us | 20,874.225 us | 55,226.233 us | 0.17x |
| `bounded-repeat/context` | 9,016.533 us | 69,485.150 us | 73,021.000 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.113 us | 0.048 us | 0.067 us | 2.36x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.043 us | 0.073 us | 1.67x |
| `common/ip-miss` | `IsMatch` | 0.270 us | 0.180 us | 0.205 us | 1.50x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.032 us | 0.050 us | 1.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.055 us | 0.077 us | 1.43x |
| `common/uri-miss` | `IsMatch` | 0.098 us | 0.069 us | 0.087 us | 1.43x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.032 us | 0.047 us | 1.27x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.037 us | 0.052 us | 1.25x |
| `common/replace-words` | `Replace` | 20.523 us | 17.704 us | 19.021 us | 1.16x |
| `industry/leipzig-river-window-count` | `Count` | 1,705.526 us | 1,520.755 us | 8,302.650 us | 1.12x |
| `industry/mariomka-uri-count` | `Count` | 1,214.905 us | 1,112.331 us | 8,480.500 us | 1.09x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.049 us | 0.086 us | 1.07x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 65.238 us | 61.092 us | 291.918 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.093 us | 0.090 us | 0.121 us | 1.04x |
| `common/match-word` | `Match` | 0.052 us | 0.050 us | 0.954 us | 1.04x |
| `common/email-match` | `IsMatch` | 0.075 us | 0.080 us | 0.109 us | 0.94x |
| `industry/mariomka-ip-count` | `Count` | 3,145.450 us | 3,466.491 us | 10,917.767 us | 0.91x |
| `common/matches-words` | `Count` | 9.211 us | 11.010 us | 14.047 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,018.667 us | 1,255.174 us | 7,131.650 us | 0.81x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.044 us | 0.075 us | 0.73x |
| `common/split-words` | `Split` | 14.965 us | 20.807 us | 22.456 us | 0.72x |
| `industry/mariomka-email-count` | `Count` | 274.249 us | 465.331 us | 7,735.775 us | 0.59x |
| `common/matches-set` | `Count` | 62.202 us | 105.614 us | 107.713 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.201 us | 0.233 us | 0.55x |
| `common/matches-word` | `Count` | 1.247 us | 2.362 us | 3.664 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 635.177 us | 1,310.040 us | 7,071.000 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 1,096.914 us | 3,039.238 us | 8,471.300 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.108 us | 0.129 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.226 us | 600.407 us | 871.325 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.187 us | 104.628 us | 107.047 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,807.930 us | 8,801.533 us | 9,338.000 us | 0.21x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.089 us | 0.116 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 258.495 us | 3,969.011 us | 4,359.111 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 289.614 us | 4,790.125 us | 5,179.460 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.211 us | 6.508 us | 32.482 us | 0.95x |
| `fallback/lookahead` | 5.307 us | 6.029 us | 20.572 us | 0.88x |
| `lokad/langserv/color-short-hex` | 0.581 us | 0.832 us | 1.013 us | 0.70x |
| `structural/ordered-keyword-window` | 11.753 us | 17.848 us | 31.774 us | 0.66x |
| `lokad/lexer/identifier` | 8.979 us | 13.676 us | 16.748 us | 0.66x |
| `structural/method-family-generic-call` | 4.406 us | 6.793 us | 22.188 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.786 us | 7.475 us | 9.048 us | 0.64x |
| `structural/method-family-call` | 5.265 us | 8.298 us | 23.141 us | 0.63x |
| `lokad/lexer/doc-line` | 1.147 us | 1.908 us | 2.661 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.357 us | 17.589 us | 31.954 us | 0.59x |
| `lokad/docs/inline-doc-prefix` | 2.014 us | 3.463 us | 4.229 us | 0.58x |
| `structural/keyword-to-generic-type` | 2.306 us | 4.192 us | 19.458 us | 0.55x |
| `lokad/langserv/helper-identifier` | 2.486 us | 4.744 us | 5.663 us | 0.52x |
| `literal/identifier-token` | 2.244 us | 4.316 us | 18.750 us | 0.52x |
| `lokad/lexer/operator-run` | 1.200 us | 2.338 us | 2.805 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.544 us | 5.306 us | 20.166 us | 0.48x |
| `lokad/langserv/url-download` | 24.050 us | 51.471 us | 52.347 us | 0.47x |
| `lokad/style/hex-color` | 0.236 us | 0.528 us | 0.605 us | 0.45x |
| `lokad/style/cell-ref` | 0.855 us | 1.912 us | 2.159 us | 0.45x |
| `literal/call-token` | 1.861 us | 4.321 us | 18.900 us | 0.43x |
| `lokad/style/range-ref` | 0.790 us | 1.862 us | 1.971 us | 0.42x |
| `lokad/langserv/color-rgb-hex` | 0.727 us | 1.807 us | 2.043 us | 0.40x |
| `lokad/lexer/string` | 32.968 us | 82.870 us | 87.985 us | 0.40x |
| `lokad/langserv/url-dashboard` | 9.345 us | 23.639 us | 24.181 us | 0.40x |
| `literal-family/type-token-family` | 9.295 us | 23.913 us | 39.575 us | 0.39x |
| `lokad/lexer/number` | 3.212 us | 10.976 us | 12.040 us | 0.29x |
| `structural/ordered-identifier-window` | 8.241 us | 29.119 us | 44.303 us | 0.28x |
| `literal-family/method-token-family` | 2.632 us | 9.434 us | 23.751 us | 0.28x |
| `literal/identifier-token-casei` | 3.946 us | 15.988 us | 29.723 us | 0.25x |
| `structural/modifier-family-to-type-window` | 60.410 us | 254.799 us | 270.444 us | 0.24x |
| `lokad/folding/region-marker` | 5.731 us | 35.172 us | 64.863 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.096 us | 222.476 us | 237.972 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.959 us | 135.078 us | 151.169 us | 0.02x |
| `balancing/begin-end-blocks` | 47.138 us | 4,338.894 us | 4,355.400 us | 0.01x |
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
| `lokad/langserv/url-download` | 24.083 us | 12.313 us | 12.813 us | 1.96x |
| `lokad/langserv/url-dashboard` | 9.360 us | 6.370 us | 6.916 us | 1.47x |
| `structural/ordered-keyword-window` | 11.834 us | 8.443 us | 22.627 us | 1.40x |
| `lokad/imports/module-imports` | 6.192 us | 4.439 us | 29.570 us | 1.39x |
| `lokad/docs/inline-doc-prefix` | 2.020 us | 1.456 us | 2.190 us | 1.39x |
| `structural/keyword-family-to-capitalized-identifier` | 10.087 us | 7.912 us | 22.563 us | 1.27x |
| `fallback/lookahead` | 5.325 us | 4.230 us | 18.333 us | 1.26x |
| `lokad/lexer/operator-run` | 1.165 us | 0.955 us | 1.421 us | 1.22x |
| `structural/ordered-modifier-pair` | 10.352 us | 8.601 us | 22.745 us | 1.20x |
| `lokad/langserv/identifier-validator` | 4.760 us | 4.224 us | 5.850 us | 1.13x |
| `lokad/lexer/identifier` | 8.969 us | 7.966 us | 11.205 us | 1.13x |
| `lokad/style/range-ref` | 0.791 us | 0.727 us | 0.867 us | 1.09x |
| `lokad/lexer/doc-line` | 1.153 us | 1.063 us | 1.884 us | 1.08x |
| `structural/modifier-family-to-type-window` | 60.515 us | 56.147 us | 71.236 us | 1.08x |
| `lokad/lexer/string` | 32.900 us | 30.543 us | 33.164 us | 1.08x |
| `lokad/langserv/color-short-hex` | 0.516 us | 0.490 us | 0.681 us | 1.05x |
| `structural/method-family-call` | 5.264 us | 5.067 us | 19.518 us | 1.04x |
| `lokad/style/cell-ref` | 0.852 us | 0.832 us | 1.063 us | 1.02x |
| `structural/method-family-generic-call` | 4.429 us | 4.636 us | 18.999 us | 0.96x |
| `lokad/style/hex-color` | 0.218 us | 0.231 us | 0.312 us | 0.94x |
| `lokad/langserv/helper-identifier` | 2.483 us | 2.724 us | 3.599 us | 0.91x |
| `lokad/lexer/number` | 3.190 us | 3.841 us | 4.918 us | 0.83x |
| `literal-family/type-token-family` | 9.277 us | 11.483 us | 26.437 us | 0.81x |
| `structural/ordered-identifier-window` | 8.269 us | 10.534 us | 25.797 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 0.648 us | 0.845 us | 1.100 us | 0.77x |
| `literal/identifier-token-casei` | 3.929 us | 5.919 us | 20.518 us | 0.66x |
| `structural/keyword-to-generic-type` | 2.298 us | 3.518 us | 18.239 us | 0.65x |
| `structural/ordered-keyword-pair` | 2.557 us | 4.136 us | 18.249 us | 0.62x |
| `literal/identifier-token` | 2.252 us | 4.316 us | 18.376 us | 0.52x |
| `literal-family/method-token-family` | 2.632 us | 5.119 us | 19.572 us | 0.51x |
| `literal/call-token` | 1.856 us | 3.992 us | 17.869 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 2.973 us | 10.855 us | 25.521 us | 0.27x |
| `lokad/folding/region-marker` | 5.712 us | 35.999 us | 64.343 us | 0.16x |
| `balancing/begin-end-blocks` | 46.908 us | 467.522 us | 500.519 us | 0.10x |
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
