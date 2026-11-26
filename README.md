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
| `literal/sherlock-zh` | 22.239 us | 18.822 us | 2,194.074 us | 1.18x |
| `literal/sherlock-ru` | 68.566 us | 97.788 us | 5,345.257 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.563 us | 68.790 us | 2,193.232 us | 0.63x |
| `ruff-noqa/tweaked` | 4,137.089 us | 7,548.273 us | 41,660.900 us | 0.55x |
| `literal/sherlock-casei-en` | 85.279 us | 198.376 us | 682.012 us | 0.43x |
| `literal/sherlock-casei-ru` | 147.252 us | 363.601 us | 5,514.793 us | 0.40x |
| `bounded-repeat/letters-en` | 1,118.133 us | 2,790.281 us | 2,953.888 us | 0.40x |
| `literal/sherlock-en` | 31.837 us | 87.084 us | 518.841 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,988.161 us | 5,884.246 us | 6,746.583 us | 0.34x |
| `dictionary/single` | 8,157.460 us | 26,177.433 us | 26,037.233 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,442.417 us | 4,672.887 us | 9,880.325 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 172.077 us | 592.806 us | 1,039.848 us | 0.29x |
| `bounded-repeat/capitals` | 11,778.729 us | 46,144.400 us | 49,063.700 us | 0.26x |
| `literal-alternate/sherlock-en` | 340.665 us | 1,514.790 us | 2,139.576 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 128.151 us | 653.060 us | 1,098.751 us | 0.20x |
| `aws-keys/quick` | 1,900.245 us | 9,716.375 us | 48,198.000 us | 0.20x |
| `aws-keys/full` | 8,740.044 us | 87,471.700 us | 123,958.200 us | 0.10x |
| `bounded-repeat/context` | 8,931.656 us | 148,422.500 us | 153,099.300 us | 0.06x |
| `ruff-noqa/real` | 3,504.673 us | 70,252.800 us | 87,148.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 853.098 us | 23,225.325 us | 28,665.350 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 102.806 us | 11,760.514 us | 12,611.757 us | 0.0087x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.085 us | 0.109 us | 0.145 us | 0.78x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.059 us | 0.076 us | 0.090 us | 0.77x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.108 us | 0.139 us | 0.65x |
| `industry/mariomka-uri-count` | `Count` | 1,208.644 us | 1,868.868 us | 10,292.150 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.084 us | 0.100 us | 0.63x |
| `common/ip-match` | `IsMatch` | 0.112 us | 0.178 us | 0.199 us | 0.63x |
| `industry/mariomka-email-count` | `Count` | 275.968 us | 480.531 us | 8,796.800 us | 0.57x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.082 us | 0.097 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.139 us | 0.274 us | 0.303 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 632.276 us | 1,326.698 us | 8,757.175 us | 0.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.081 us | 0.169 us | 0.188 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.090 us | 0.201 us | 0.221 us | 0.45x |
| `common/match-word` | `Match` | 0.051 us | 0.131 us | 1.191 us | 0.39x |
| `common/ip-miss` | `IsMatch` | 0.274 us | 0.723 us | 0.742 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,120.536 us | 8,461.710 us | 30,663.067 us | 0.37x |
| `common/uri-match` | `IsMatch` | 0.083 us | 0.236 us | 0.273 us | 0.35x |
| `common/matches-set` | `Count` | 63.670 us | 193.444 us | 196.011 us | 0.33x |
| `common/matches-word` | `Count` | 1.237 us | 4.249 us | 5.984 us | 0.29x |
| `common/replace-words` | `Replace` | 20.601 us | 77.452 us | 79.127 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.853 us | 261.368 us | 505.205 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.110 us | 0.555 us | 0.583 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.936 us | 965.846 us | 1,285.269 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.998 us | 161.705 us | 163.555 us | 0.17x |
| `common/split-words` | `Split` | 13.151 us | 79.547 us | 81.974 us | 0.17x |
| `common/matches-words` | `Count` | 9.256 us | 70.406 us | 71.976 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,072.637 us | 9,745.038 us | 17,013.400 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,943.236 us | 19,271.700 us | 19,754.550 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.613 us | 0.643 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,699.660 us | 32,443.633 us | 39,802.700 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.349 us | 0.376 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,029.637 us | 22,693.200 us | 30,431.600 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.832 us | 0.872 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 256.736 us | 8,228.330 us | 8,744.411 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 290.525 us | 11,865.214 us | 12,458.683 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.631 us | 36.159 us | 2,165.880 us | 1.21x |
| `literal/sherlock-casei-en` | 86.067 us | 79.427 us | 481.090 us | 1.08x |
| `literal-alternate/sherlock-en-mixed` | 170.814 us | 174.524 us | 612.059 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,972.626 us | 2,067.684 us | 2,935.154 us | 0.95x |
| `literal-alternate/sherlock-en` | 342.437 us | 365.408 us | 911.224 us | 0.94x |
| `literal/sherlock-zh` | 22.203 us | 24.291 us | 2,170.282 us | 0.91x |
| `ruff-noqa/tweaked` | 4,137.042 us | 4,554.200 us | 37,026.000 us | 0.91x |
| `bounded-repeat/capitals` | 11,839.814 us | 13,585.650 us | 16,493.925 us | 0.87x |
| `literal/sherlock-ru` | 68.686 us | 89.105 us | 5,438.250 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 103.003 us | 134.840 us | 649.854 us | 0.76x |
| `literal/sherlock-casei-ru` | 144.233 us | 223.352 us | 5,344.979 us | 0.65x |
| `bounded-repeat/letters-en` | 1,120.372 us | 1,814.598 us | 1,977.268 us | 0.62x |
| `literal-alternate/sherlock-en-nomatch` | 127.900 us | 207.890 us | 613.621 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,430.148 us | 2,341.861 us | 7,448.675 us | 0.61x |
| `aws-keys/quick` | 1,909.650 us | 3,610.386 us | 39,987.800 us | 0.53x |
| `aws-keys/full` | 8,720.222 us | 18,673.320 us | 54,996.367 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 866.198 us | 1,958.338 us | 7,284.825 us | 0.44x |
| `literal/sherlock-en` | 32.015 us | 76.826 us | 455.185 us | 0.42x |
| `dictionary/single` | 8,198.670 us | 45,868.950 us | 45,963.600 us | 0.18x |
| `ruff-noqa/real` | 3,495.564 us | 21,460.700 us | 36,311.633 us | 0.16x |
| `bounded-repeat/context` | 8,948.967 us | 68,263.100 us | 72,095.800 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.106 us | 0.048 us | 0.068 us | 2.21x |
| `common/date-match` | `IsMatch` | 0.070 us | 0.042 us | 0.072 us | 1.68x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.055 us | 0.035 us | 0.050 us | 1.58x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.032 us | 0.050 us | 1.51x |
| `common/ip-miss` | `IsMatch` | 0.270 us | 0.180 us | 0.203 us | 1.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.055 us | 0.077 us | 1.41x |
| `common/uri-miss` | `IsMatch` | 0.089 us | 0.067 us | 0.087 us | 1.33x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.030 us | 0.048 us | 1.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.207 us | 57.971 us | 256.957 us | 1.16x |
| `common/replace-words` | `Replace` | 20.500 us | 17.792 us | 20.818 us | 1.15x |
| `industry/mariomka-uri-count` | `Count` | 1,207.102 us | 1,112.135 us | 8,494.025 us | 1.09x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.049 us | 0.087 us | 1.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,708.542 us | 1,589.316 us | 8,374.540 us | 1.08x |
| `common/match-word` | `Match` | 0.051 us | 0.048 us | 0.966 us | 1.06x |
| `common/email-match` | `IsMatch` | 0.075 us | 0.080 us | 0.108 us | 0.94x |
| `common/uri-match` | `IsMatch` | 0.083 us | 0.088 us | 0.122 us | 0.94x |
| `industry/mariomka-ip-count` | `Count` | 3,122.872 us | 3,486.609 us | 20,876.400 us | 0.90x |
| `common/matches-words` | `Count` | 9.204 us | 10.928 us | 14.162 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,044.885 us | 1,259.990 us | 7,415.325 us | 0.83x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.044 us | 0.075 us | 0.75x |
| `common/split-words` | `Split` | 13.202 us | 21.481 us | 23.232 us | 0.61x |
| `industry/mariomka-email-count` | `Count` | 274.850 us | 451.672 us | 7,839.600 us | 0.61x |
| `common/matches-set` | `Count` | 63.505 us | 106.127 us | 107.773 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.196 us | 0.233 us | 0.56x |
| `common/matches-word` | `Count` | 1.242 us | 2.367 us | 3.592 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 633.244 us | 1,302.948 us | 7,678.029 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,085.862 us | 3,062.354 us | 8,404.475 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.107 us | 0.129 us | 0.33x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.768 us | 599.297 us | 878.057 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.006 us | 103.582 us | 105.651 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,959.921 us | 8,739.611 us | 9,290.050 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.089 us | 0.117 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 255.198 us | 3,944.089 us | 4,380.929 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 288.352 us | 4,801.438 us | 5,218.657 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.049 us | 6.306 us | 32.440 us | 0.96x |
| `fallback/lookahead` | 5.279 us | 6.027 us | 20.434 us | 0.88x |
| `lokad/langserv/color-short-hex` | 0.580 us | 0.819 us | 1.017 us | 0.71x |
| `structural/method-family-call` | 5.518 us | 8.012 us | 22.170 us | 0.69x |
| `structural/method-family-generic-call` | 4.388 us | 6.459 us | 21.312 us | 0.68x |
| `lokad/lexer/identifier` | 9.202 us | 13.611 us | 16.828 us | 0.68x |
| `lokad/langserv/identifier-validator` | 4.896 us | 7.326 us | 9.014 us | 0.67x |
| `structural/ordered-keyword-window` | 11.657 us | 17.810 us | 32.104 us | 0.65x |
| `lokad/lexer/doc-line` | 1.139 us | 1.891 us | 2.649 us | 0.60x |
| `structural/keyword-to-generic-type` | 2.455 us | 4.085 us | 19.172 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.467 us | 17.765 us | 32.455 us | 0.59x |
| `lokad/docs/inline-doc-prefix` | 2.006 us | 3.437 us | 4.239 us | 0.58x |
| `structural/ordered-keyword-pair` | 2.974 us | 5.524 us | 20.639 us | 0.54x |
| `literal/identifier-token` | 2.278 us | 4.240 us | 19.126 us | 0.54x |
| `lokad/langserv/helper-identifier` | 2.508 us | 4.795 us | 5.681 us | 0.52x |
| `lokad/lexer/operator-run` | 1.170 us | 2.349 us | 2.805 us | 0.50x |
| `lokad/style/hex-color` | 0.233 us | 0.521 us | 0.599 us | 0.45x |
| `literal/call-token` | 1.842 us | 4.182 us | 18.818 us | 0.44x |
| `lokad/style/range-ref` | 0.791 us | 1.804 us | 1.986 us | 0.44x |
| `lokad/style/cell-ref` | 0.844 us | 1.939 us | 2.133 us | 0.44x |
| `lokad/langserv/color-rgb-hex` | 0.737 us | 1.803 us | 2.043 us | 0.41x |
| `lokad/langserv/url-dashboard` | 9.007 us | 23.622 us | 23.806 us | 0.38x |
| `lokad/lexer/string` | 32.189 us | 84.434 us | 89.787 us | 0.38x |
| `literal-family/type-token-family` | 9.219 us | 24.834 us | 40.373 us | 0.37x |
| `lokad/lexer/number` | 3.169 us | 10.722 us | 11.611 us | 0.30x |
| `structural/ordered-identifier-window` | 8.111 us | 28.787 us | 43.342 us | 0.28x |
| `literal-family/method-token-family` | 2.618 us | 9.425 us | 23.824 us | 0.28x |
| `structural/modifier-family-to-type-window` | 58.917 us | 255.451 us | 270.800 us | 0.23x |
| `literal/identifier-token-casei` | 3.467 us | 15.394 us | 29.603 us | 0.23x |
| `lokad/langserv/url-download` | 11.358 us | 53.915 us | 54.058 us | 0.21x |
| `lokad/folding/region-marker` | 5.894 us | 33.702 us | 64.296 us | 0.17x |
| `structural/keyword-family-to-capitalized-identifier` | 10.104 us | 217.394 us | 236.931 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.932 us | 135.386 us | 150.120 us | 0.02x |
| `balancing/begin-end-blocks` | 47.135 us | 4,284.489 us | 4,269.528 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.001 us | 6.303 us | 6.821 us | 1.43x |
| `lokad/docs/inline-doc-prefix` | 2.008 us | 1.444 us | 2.196 us | 1.39x |
| `structural/ordered-keyword-window` | 11.659 us | 8.489 us | 22.843 us | 1.37x |
| `lokad/imports/module-imports` | 6.014 us | 4.399 us | 29.367 us | 1.37x |
| `structural/keyword-family-to-capitalized-identifier` | 10.099 us | 7.971 us | 22.155 us | 1.27x |
| `lokad/lexer/operator-run` | 1.157 us | 0.918 us | 1.417 us | 1.26x |
| `fallback/lookahead` | 5.286 us | 4.209 us | 18.072 us | 1.26x |
| `structural/ordered-modifier-pair` | 10.420 us | 8.692 us | 23.186 us | 1.20x |
| `lokad/lexer/identifier` | 9.250 us | 8.035 us | 10.814 us | 1.15x |
| `lokad/langserv/identifier-validator` | 4.901 us | 4.273 us | 5.813 us | 1.15x |
| `structural/method-family-call` | 5.527 us | 5.009 us | 18.785 us | 1.10x |
| `lokad/langserv/color-short-hex` | 0.516 us | 0.472 us | 0.689 us | 1.09x |
| `lokad/lexer/doc-line` | 1.138 us | 1.061 us | 1.874 us | 1.07x |
| `lokad/style/range-ref` | 0.784 us | 0.735 us | 0.869 us | 1.07x |
| `lokad/lexer/string` | 32.309 us | 30.507 us | 33.779 us | 1.06x |
| `structural/modifier-family-to-type-window` | 58.835 us | 56.081 us | 72.653 us | 1.05x |
| `lokad/style/cell-ref` | 0.854 us | 0.834 us | 1.062 us | 1.02x |
| `structural/method-family-generic-call` | 4.400 us | 4.596 us | 18.268 us | 0.96x |
| `lokad/style/hex-color` | 0.218 us | 0.231 us | 0.343 us | 0.94x |
| `lokad/langserv/url-download` | 11.418 us | 12.161 us | 12.718 us | 0.94x |
| `lokad/langserv/helper-identifier` | 2.496 us | 2.710 us | 3.608 us | 0.92x |
| `lokad/lexer/number` | 3.204 us | 3.800 us | 4.893 us | 0.84x |
| `literal-family/type-token-family` | 9.249 us | 11.455 us | 25.936 us | 0.81x |
| `structural/ordered-identifier-window` | 8.177 us | 10.461 us | 24.862 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 0.637 us | 0.847 us | 1.091 us | 0.75x |
| `structural/ordered-keyword-pair` | 2.956 us | 4.171 us | 18.592 us | 0.71x |
| `structural/keyword-to-generic-type` | 2.438 us | 3.497 us | 17.932 us | 0.70x |
| `literal/identifier-token-casei` | 3.473 us | 5.468 us | 20.672 us | 0.64x |
| `literal-family/method-token-family` | 2.631 us | 5.126 us | 19.495 us | 0.51x |
| `literal/identifier-token` | 2.276 us | 4.719 us | 18.378 us | 0.48x |
| `literal/call-token` | 1.855 us | 3.946 us | 17.793 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.940 us | 10.852 us | 26.027 us | 0.27x |
| `lokad/folding/region-marker` | 5.911 us | 34.976 us | 63.342 us | 0.17x |
| `balancing/begin-end-blocks` | 47.770 us | 462.115 us | 493.523 us | 0.10x |
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
