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
Frozen `common/match-word` and Lokad prefix-loop results through dev.117
include .NET's `Match` object allocation. New runs compare the first
whole-match range on both sides.

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
| `literal/sherlock-zh` | 21.924 us | 18.410 us | 2,189.789 us | 1.19x |
| `literal/sherlock-ru` | 68.032 us | 94.971 us | 5,103.153 us | 0.72x |
| `literal-alternate/sherlock-zh` | 42.812 us | 67.380 us | 2,174.351 us | 0.64x |
| `aws-keys/full` | 49,211.650 us | 86,596.900 us | 122,058.600 us | 0.57x |
| `ruff-noqa/tweaked` | 3,904.310 us | 7,842.982 us | 41,481.400 us | 0.50x |
| `aws-keys/quick` | 4,276.093 us | 9,716.712 us | 41,415.200 us | 0.44x |
| `literal/sherlock-casei-en` | 78.820 us | 192.239 us | 573.643 us | 0.41x |
| `literal/sherlock-casei-ru` | 145.748 us | 358.431 us | 5,362.564 us | 0.41x |
| `bounded-repeat/letters-en` | 1,091.990 us | 2,775.043 us | 2,948.996 us | 0.39x |
| `literal/sherlock-en` | 31.054 us | 84.600 us | 462.543 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,956.844 us | 5,751.707 us | 6,651.267 us | 0.34x |
| `dictionary/single` | 8,125.860 us | 25,508.433 us | 25,652.900 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,418.079 us | 4,726.400 us | 9,950.125 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 167.288 us | 601.600 us | 1,041.396 us | 0.28x |
| `bounded-repeat/capitals` | 11,224.429 us | 45,883.000 us | 49,604.750 us | 0.24x |
| `literal-alternate/sherlock-en` | 341.167 us | 1,469.098 us | 2,030.942 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 127.391 us | 661.537 us | 1,100.276 us | 0.19x |
| `bounded-repeat/context` | 10,644.600 us | 147,329.400 us | 151,922.300 us | 0.07x |
| `ruff-noqa/real` | 3,423.664 us | 69,799.950 us | 103,914.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 855.281 us | 23,291.475 us | 28,086.200 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 111.201 us | 11,022.200 us | 11,887.543 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.169 us | 0.106 us | 0.133 us | 1.60x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.124 us | 0.081 us | 0.096 us | 1.52x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.114 us | 0.076 us | 0.089 us | 1.50x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.114 us | 0.083 us | 0.098 us | 1.36x |
| `common/ip-match` | `IsMatch` | 0.219 us | 0.170 us | 0.189 us | 1.29x |
| `common/email-match` | `IsMatch` | 0.330 us | 0.270 us | 0.294 us | 1.22x |
| `common/uri-match` | `IsMatch` | 0.283 us | 0.234 us | 0.273 us | 1.21x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.116 us | 0.109 us | 0.145 us | 1.07x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.165 us | 0.166 us | 0.183 us | 0.99x |
| `common/uri-miss` | `IsMatch` | 0.194 us | 0.199 us | 0.216 us | 0.98x |
| `industry/mariomka-uri-count` | `Count` | 1,668.607 us | 1,917.345 us | 10,387.167 us | 0.87x |
| `industry/mariomka-email-count` | `Count` | 424.776 us | 492.682 us | 9,020.875 us | 0.86x |
| `common/ip-miss` | `IsMatch` | 0.595 us | 0.713 us | 0.738 us | 0.84x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 152.433 us | 255.306 us | 506.945 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.324 us | 0.554 us | 0.588 us | 0.58x |
| `industry/leipzig-twain-count` | `Count` | 620.156 us | 1,304.069 us | 8,502.944 us | 0.48x |
| `common/match-word` | `Match` | 0.059 us | 0.166 us | 1.160 us | 0.35x |
| `common/matches-set` | `Count` | 61.445 us | 195.232 us | 195.726 us | 0.31x |
| `common/matches-word` | `Count` | 1.238 us | 4.234 us | 5.925 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,226.806 us | 8,094.780 us | 16,403.100 us | 0.28x |
| `common/replace-words` | `Replace` | 20.807 us | 76.748 us | 77.840 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.494 us | 974.458 us | 1,241.644 us | 0.17x |
| `common/split-words` | `Split` | 13.242 us | 77.952 us | 79.154 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.707 us | 166.510 us | 166.637 us | 0.14x |
| `common/matches-words` | `Count` | 9.119 us | 71.547 us | 73.422 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,068.758 us | 9,696.362 us | 16,949.900 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,819.893 us | 19,078.725 us | 19,682.250 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.337 us | 0.365 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 902.202 us | 11,676.014 us | 12,293.471 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.817 us | 0.857 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.606 us | 0.633 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,835.326 us | 32,068.567 us | 39,317.850 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 1,029.359 us | 24,502.450 us | 31,885.150 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 315.457 us | 8,350.756 us | 8,856.356 us | 0.04x |
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
| `aws-keys/full` | 49,991.850 us | 18,104.620 us | 53,908.233 us | 2.76x |
| `literal-alternate/sherlock-zh` | 42.969 us | 35.846 us | 2,148.583 us | 1.20x |
| `aws-keys/quick` | 4,349.293 us | 3,679.986 us | 35,187.467 us | 1.18x |
| `literal/sherlock-casei-en` | 78.048 us | 79.079 us | 445.137 us | 0.99x |
| `literal-alternate/sherlock-en-mixed` | 166.592 us | 175.249 us | 570.252 us | 0.95x |
| `bounded-repeat/letters-ru` | 1,934.835 us | 2,050.805 us | 2,906.654 us | 0.94x |
| `literal-alternate/sherlock-en` | 341.607 us | 362.167 us | 778.718 us | 0.94x |
| `literal/sherlock-zh` | 21.718 us | 23.616 us | 2,176.650 us | 0.92x |
| `ruff-noqa/tweaked` | 3,934.735 us | 4,548.294 us | 35,664.550 us | 0.87x |
| `bounded-repeat/capitals` | 11,229.214 us | 13,448.400 us | 16,355.150 us | 0.83x |
| `literal-alternate/sherlock-casei-en` | 110.767 us | 139.013 us | 549.299 us | 0.80x |
| `literal/sherlock-ru` | 66.462 us | 86.122 us | 5,050.113 us | 0.77x |
| `literal/sherlock-casei-ru` | 143.455 us | 217.560 us | 5,205.620 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 126.674 us | 202.056 us | 593.107 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,409.242 us | 2,297.882 us | 7,285.083 us | 0.61x |
| `bounded-repeat/letters-en` | 1,080.843 us | 1,813.424 us | 1,938.321 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 858.661 us | 1,956.882 us | 7,035.700 us | 0.44x |
| `literal/sherlock-en` | 31.205 us | 73.703 us | 441.689 us | 0.42x |
| `dictionary/single` | 8,146.460 us | 45,044.850 us | 45,310.050 us | 0.18x |
| `ruff-noqa/real` | 3,449.457 us | 20,630.225 us | 55,172.467 us | 0.17x |
| `bounded-repeat/context` | 10,532.487 us | 68,145.900 us | 71,352.200 us | 0.15x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.219 us | 0.048 us | 0.069 us | 4.58x |
| `common/email-match` | `IsMatch` | 0.330 us | 0.080 us | 0.107 us | 4.11x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.123 us | 0.031 us | 0.047 us | 4.00x |
| `common/date-match` | `IsMatch` | 0.168 us | 0.043 us | 0.072 us | 3.95x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.116 us | 0.030 us | 0.049 us | 3.85x |
| `common/ip-miss` | `IsMatch` | 0.600 us | 0.179 us | 0.203 us | 3.35x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.115 us | 0.035 us | 0.052 us | 3.30x |
| `common/uri-match` | `IsMatch` | 0.283 us | 0.088 us | 0.120 us | 3.22x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.165 us | 0.055 us | 0.076 us | 3.00x |
| `common/uri-miss` | `IsMatch` | 0.192 us | 0.065 us | 0.084 us | 2.96x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 152.549 us | 57.374 us | 256.458 us | 2.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.116 us | 0.048 us | 0.088 us | 2.40x |
| `common/date-miss` | `IsMatch` | 0.323 us | 0.202 us | 0.235 us | 1.60x |
| `industry/mariomka-uri-count` | `Count` | 1,669.459 us | 1,122.828 us | 8,652.425 us | 1.49x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.043 us | 0.073 us | 1.30x |
| `common/replace-words` | `Replace` | 20.851 us | 17.464 us | 18.766 us | 1.19x |
| `industry/leipzig-river-window-count` | `Count` | 1,869.588 us | 1,589.006 us | 8,103.180 us | 1.18x |
| `industry/mariomka-email-count` | `Count` | 426.633 us | 460.647 us | 7,638.175 us | 0.93x |
| `common/matches-words` | `Count` | 9.123 us | 10.815 us | 13.896 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,037.295 us | 1,254.305 us | 7,334.975 us | 0.83x |
| `common/match-word` | `Match` | 0.059 us | 0.080 us | 1.009 us | 0.74x |
| `industry/mariomka-ip-count` | `Count` | 2,226.771 us | 3,377.926 us | 10,627.800 us | 0.66x |
| `common/split-words` | `Split` | 13.179 us | 20.080 us | 22.136 us | 0.66x |
| `common/matches-set` | `Count` | 61.687 us | 104.014 us | 105.346 us | 0.59x |
| `common/matches-word` | `Count` | 1.227 us | 2.333 us | 3.477 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 625.917 us | 1,271.939 us | 7,446.850 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.086 us | 0.125 us | 0.44x |
| `industry/leipzig-name-family-count` | `Count` | 1,067.858 us | 3,006.058 us | 8,131.725 us | 0.36x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.088 us | 0.114 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.683 us | 604.157 us | 848.597 us | 0.28x |
| `common/matches-boundary` | `Count` | 23.987 us | 104.847 us | 105.774 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,816.950 us | 8,625.533 us | 9,107.356 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 897.036 us | 4,772.069 us | 5,152.173 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 313.768 us | 3,898.800 us | 4,309.128 us | 0.08x |
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
| `lokad/docs/inline-doc-prefix` | 9.679 us | 4.684 us | 5.377 us | 2.07x |
| `lokad/lexer/doc-line` | 6.070 us | 3.018 us | 3.571 us | 2.01x |
| `lokad/langserv/identifier-validator` | 24.650 us | 12.549 us | 14.167 us | 1.96x |
| `lokad/lexer/identifier` | 42.081 us | 23.289 us | 25.835 us | 1.81x |
| `fallback/lookahead` | 10.577 us | 5.982 us | 18.654 us | 1.77x |
| `lokad/lexer/string` | 167.165 us | 94.802 us | 100.346 us | 1.76x |
| `lokad/lexer/number` | 23.465 us | 14.177 us | 14.951 us | 1.66x |
| `lokad/langserv/helper-identifier` | 11.690 us | 7.184 us | 7.657 us | 1.63x |
| `lokad/style/hex-color` | 1.354 us | 0.833 us | 0.880 us | 1.63x |
| `lokad/langserv/color-rgb-hex` | 4.613 us | 2.944 us | 3.009 us | 1.57x |
| `lokad/langserv/color-short-hex` | 2.090 us | 1.359 us | 1.469 us | 1.54x |
| `structural/method-family-generic-call` | 9.832 us | 6.453 us | 20.353 us | 1.52x |
| `lokad/lexer/operator-run` | 4.968 us | 3.530 us | 3.940 us | 1.41x |
| `lokad/style/range-ref` | 3.615 us | 2.575 us | 2.672 us | 1.40x |
| `lokad/imports/module-imports` | 8.248 us | 6.242 us | 30.886 us | 1.32x |
| `structural/ordered-keyword-window` | 21.752 us | 17.621 us | 30.460 us | 1.23x |
| `structural/method-family-call` | 9.628 us | 8.004 us | 21.654 us | 1.20x |
| `lokad/style/cell-ref` | 3.365 us | 2.801 us | 3.013 us | 1.20x |
| `structural/keyword-to-generic-type` | 3.150 us | 4.049 us | 17.882 us | 0.78x |
| `structural/ordered-keyword-pair` | 3.607 us | 5.336 us | 18.932 us | 0.68x |
| `structural/ordered-modifier-pair` | 10.149 us | 17.056 us | 30.312 us | 0.60x |
| `structural/ordered-identifier-window` | 15.188 us | 28.333 us | 41.738 us | 0.54x |
| `lokad/langserv/url-dashboard` | 12.619 us | 24.067 us | 24.611 us | 0.52x |
| `literal/identifier-token` | 2.264 us | 4.346 us | 17.339 us | 0.52x |
| `literal/call-token` | 1.813 us | 4.254 us | 17.453 us | 0.43x |
| `structural/modifier-family-to-type-window` | 104.924 us | 250.640 us | 264.976 us | 0.42x |
| `literal-family/method-token-family` | 3.759 us | 9.068 us | 21.904 us | 0.41x |
| `literal-family/type-token-family` | 9.689 us | 23.544 us | 37.835 us | 0.41x |
| `balancing/begin-end-blocks` | 1,575.696 us | 4,085.084 us | 4,096.272 us | 0.39x |
| `lokad/langserv/url-download` | 13.007 us | 52.511 us | 53.496 us | 0.25x |
| `literal/identifier-token-casei` | 3.429 us | 15.084 us | 28.220 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 20.736 us | 134.826 us | 149.476 us | 0.15x |
| `lokad/folding/region-marker` | 5.160 us | 35.556 us | 61.569 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 26.821 us | 212.326 us | 229.362 us | 0.13x |
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
| `lokad/lexer/string` | 169.542 us | 46.546 us | 49.243 us | 3.64x |
| `lokad/docs/inline-doc-prefix` | 9.669 us | 2.744 us | 3.489 us | 3.52x |
| `structural/keyword-family-to-capitalized-identifier` | 26.842 us | 7.807 us | 21.544 us | 3.44x |
| `balancing/begin-end-blocks` | 1,567.175 us | 461.007 us | 496.873 us | 3.40x |
| `lokad/lexer/number` | 23.459 us | 7.418 us | 8.455 us | 3.16x |
| `lokad/langserv/identifier-validator` | 24.911 us | 9.096 us | 11.137 us | 2.74x |
| `lokad/lexer/doc-line` | 6.075 us | 2.233 us | 2.888 us | 2.72x |
| `lokad/style/hex-color` | 1.342 us | 0.514 us | 0.594 us | 2.61x |
| `lokad/langserv/color-rgb-hex` | 4.625 us | 1.802 us | 2.020 us | 2.57x |
| `structural/ordered-keyword-window` | 21.871 us | 8.531 us | 21.325 us | 2.56x |
| `lokad/langserv/helper-identifier` | 12.052 us | 4.722 us | 5.633 us | 2.55x |
| `fallback/lookahead` | 10.604 us | 4.174 us | 16.434 us | 2.54x |
| `lokad/lexer/identifier` | 41.967 us | 16.631 us | 20.660 us | 2.52x |
| `lokad/lexer/operator-run` | 4.997 us | 2.045 us | 2.572 us | 2.44x |
| `lokad/style/range-ref` | 3.560 us | 1.498 us | 1.593 us | 2.38x |
| `structural/method-family-generic-call` | 9.787 us | 4.542 us | 17.280 us | 2.15x |
| `lokad/langserv/color-short-hex` | 2.100 us | 1.006 us | 1.196 us | 2.09x |
| `structural/modifier-family-to-type-pair` | 20.791 us | 10.464 us | 23.518 us | 1.99x |
| `structural/method-family-call` | 9.652 us | 5.022 us | 18.028 us | 1.92x |
| `lokad/style/cell-ref` | 3.389 us | 1.766 us | 1.947 us | 1.92x |
| `structural/modifier-family-to-type-window` | 105.008 us | 54.965 us | 68.886 us | 1.91x |
| `lokad/imports/module-imports` | 8.228 us | 4.348 us | 28.211 us | 1.89x |
| `lokad/langserv/url-dashboard` | 12.601 us | 7.356 us | 8.020 us | 1.71x |
| `structural/ordered-identifier-window` | 15.052 us | 10.458 us | 23.818 us | 1.44x |
| `structural/ordered-modifier-pair` | 10.157 us | 8.627 us | 21.406 us | 1.18x |
| `lokad/langserv/url-download` | 13.048 us | 12.852 us | 13.628 us | 1.02x |
| `structural/keyword-to-generic-type` | 3.157 us | 3.451 us | 16.832 us | 0.91x |
| `structural/ordered-keyword-pair` | 3.563 us | 4.088 us | 16.946 us | 0.87x |
| `literal-family/type-token-family` | 9.736 us | 11.366 us | 24.247 us | 0.86x |
| `literal-family/method-token-family` | 3.764 us | 5.269 us | 17.794 us | 0.71x |
| `literal/identifier-token-casei` | 3.434 us | 5.857 us | 18.860 us | 0.59x |
| `literal/identifier-token` | 2.260 us | 4.665 us | 16.701 us | 0.48x |
| `literal/call-token` | 1.816 us | 3.898 us | 16.453 us | 0.47x |
| `lokad/folding/region-marker` | 5.201 us | 34.709 us | 61.743 us | 0.15x |
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
