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
| `literal/sherlock-zh` | 14.153 us | 18.921 us | 2,186.689 us | 0.75x |
| `literal/sherlock-ru` | 69.269 us | 98.321 us | 5,255.043 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.442 us | 68.097 us | 2,221.517 us | 0.64x |
| `ruff-noqa/tweaked` | 3,985.821 us | 7,589.318 us | 44,465.600 us | 0.53x |
| `literal/sherlock-casei-ru` | 150.658 us | 367.233 us | 5,574.362 us | 0.41x |
| `bounded-repeat/letters-en` | 1,134.456 us | 2,796.059 us | 2,964.285 us | 0.41x |
| `literal/sherlock-casei-en` | 70.136 us | 186.058 us | 669.664 us | 0.38x |
| `literal/sherlock-en` | 31.758 us | 89.170 us | 454.835 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,069.136 us | 5,881.554 us | 6,757.008 us | 0.35x |
| `dictionary/single` | 8,289.640 us | 26,327.433 us | 26,487.267 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,433.543 us | 4,804.725 us | 9,908.550 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 168.611 us | 595.627 us | 1,050.522 us | 0.28x |
| `bounded-repeat/capitals` | 11,806.100 us | 44,877.900 us | 48,415.000 us | 0.26x |
| `literal-alternate/sherlock-en` | 367.996 us | 1,488.212 us | 2,023.360 us | 0.25x |
| `aws-keys/quick` | 1,966.987 us | 9,487.938 us | 43,277.400 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 128.317 us | 654.969 us | 1,210.895 us | 0.20x |
| `aws-keys/full` | 8,631.167 us | 87,380.100 us | 122,509.000 us | 0.10x |
| `bounded-repeat/context` | 9,039.589 us | 146,793.000 us | 151,843.400 us | 0.06x |
| `ruff-noqa/real` | 3,549.723 us | 70,277.400 us | 104,784.600 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 858.503 us | 23,182.150 us | 28,598.650 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 108.344 us | 11,285.457 us | 12,125.443 us | 0.0096x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.084 us | 0.109 us | 0.146 us | 0.77x |
| `common/ip-match` | `IsMatch` | 0.116 us | 0.173 us | 0.192 us | 0.67x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.078 us | 0.091 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.110 us | 0.141 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,217.208 us | 1,931.828 us | 10,375.625 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.083 us | 0.100 us | 0.62x |
| `industry/mariomka-email-count` | `Count` | 272.083 us | 492.542 us | 8,952.425 us | 0.55x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.082 us | 0.098 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.138 us | 0.268 us | 0.293 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.083 us | 0.162 us | 0.181 us | 0.51x |
| `common/uri-miss` | `IsMatch` | 0.098 us | 0.201 us | 0.221 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 629.433 us | 1,324.532 us | 8,488.233 us | 0.48x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.233 us | 0.269 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.284 us | 0.717 us | 0.735 us | 0.40x |
| `common/match-word` | `Match` | 0.051 us | 0.130 us | 1.158 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,103.820 us | 8,239.550 us | 16,764.800 us | 0.38x |
| `common/matches-set` | `Count` | 62.009 us | 191.183 us | 192.908 us | 0.32x |
| `common/matches-word` | `Count` | 1.239 us | 4.218 us | 5.999 us | 0.29x |
| `common/replace-words` | `Replace` | 20.763 us | 79.104 us | 80.529 us | 0.26x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 64.601 us | 258.891 us | 517.704 us | 0.25x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.555 us | 0.596 us | 0.20x |
| `common/matches-boundary` | `Count` | 29.923 us | 162.479 us | 165.057 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.983 us | 963.288 us | 1,281.786 us | 0.17x |
| `common/split-words` | `Split` | 13.188 us | 80.727 us | 82.705 us | 0.16x |
| `common/matches-words` | `Count` | 9.107 us | 70.192 us | 71.741 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,072.651 us | 9,943.962 us | 17,308.600 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,850.327 us | 19,570.000 us | 20,082.150 us | 0.09x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.613 us | 0.650 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.345 us | 0.370 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,656.167 us | 31,869.033 us | 44,286.850 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,013.607 us | 22,669.600 us | 30,506.950 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.834 us | 0.869 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 261.368 us | 8,503.156 us | 9,030.689 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 292.918 us | 11,956.114 us | 12,472.733 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.320 us | 35.734 us | 2,167.750 us | 1.21x |
| `bounded-repeat/letters-ru` | 2,073.978 us | 2,069.419 us | 2,929.892 us | 1.00x |
| `literal-alternate/sherlock-en` | 367.340 us | 368.250 us | 823.935 us | 1.00x |
| `literal-alternate/sherlock-en-mixed` | 167.746 us | 176.506 us | 629.532 us | 0.95x |
| `ruff-noqa/tweaked` | 4,037.437 us | 4,570.629 us | 40,203.050 us | 0.88x |
| `bounded-repeat/capitals` | 11,840.071 us | 13,601.983 us | 16,515.875 us | 0.87x |
| `literal/sherlock-casei-en` | 69.807 us | 80.279 us | 498.417 us | 0.87x |
| `literal-alternate/sherlock-casei-en` | 108.676 us | 134.895 us | 751.792 us | 0.81x |
| `literal/sherlock-casei-ru` | 151.249 us | 195.902 us | 5,442.536 us | 0.77x |
| `literal/sherlock-ru` | 69.284 us | 90.038 us | 5,279.407 us | 0.77x |
| `literal-alternate/sherlock-en-nomatch` | 127.881 us | 203.017 us | 622.148 us | 0.63x |
| `bounded-repeat/letters-en` | 1,131.424 us | 1,827.254 us | 1,979.061 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,433.387 us | 2,346.091 us | 7,447.450 us | 0.61x |
| `literal/sherlock-zh` | 14.156 us | 24.082 us | 2,187.771 us | 0.59x |
| `aws-keys/quick` | 1,959.442 us | 3,606.686 us | 35,085.350 us | 0.54x |
| `aws-keys/full` | 8,691.489 us | 18,537.250 us | 53,963.633 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 855.314 us | 1,987.082 us | 7,308.150 us | 0.43x |
| `literal/sherlock-en` | 31.752 us | 77.836 us | 418.355 us | 0.41x |
| `dictionary/single` | 8,292.311 us | 45,753.850 us | 45,968.500 us | 0.18x |
| `ruff-noqa/real` | 3,547.391 us | 20,811.500 us | 56,629.967 us | 0.17x |
| `bounded-repeat/context` | 9,107.689 us | 69,180.450 us | 72,923.150 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.111 us | 0.047 us | 0.067 us | 2.37x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.044 us | 0.074 us | 1.64x |
| `common/uri-miss` | `IsMatch` | 0.097 us | 0.064 us | 0.085 us | 1.53x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.032 us | 0.049 us | 1.52x |
| `common/ip-miss` | `IsMatch` | 0.273 us | 0.181 us | 0.204 us | 1.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.055 us | 0.077 us | 1.43x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.031 us | 0.047 us | 1.27x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.036 us | 0.053 us | 1.26x |
| `common/replace-words` | `Replace` | 20.747 us | 17.951 us | 19.443 us | 1.16x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 64.756 us | 57.779 us | 264.050 us | 1.12x |
| `industry/mariomka-uri-count` | `Count` | 1,225.060 us | 1,113.814 us | 8,348.650 us | 1.10x |
| `industry/leipzig-river-window-count` | `Count` | 1,660.954 us | 1,512.435 us | 7,983.100 us | 1.10x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.049 us | 0.088 us | 1.08x |
| `common/match-word` | `Match` | 0.051 us | 0.047 us | 0.940 us | 1.08x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.090 us | 0.121 us | 1.02x |
| `common/email-match` | `IsMatch` | 0.075 us | 0.080 us | 0.107 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,110.938 us | 3,436.091 us | 10,841.067 us | 0.91x |
| `common/matches-words` | `Count` | 9.150 us | 10.695 us | 13.836 us | 0.86x |
| `industry/leipzig-symbol-count` | `Count` | 1,012.319 us | 1,221.034 us | 7,321.600 us | 0.83x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.043 us | 0.074 us | 0.74x |
| `common/split-words` | `Split` | 13.139 us | 20.415 us | 22.326 us | 0.64x |
| `common/matches-set` | `Count` | 62.129 us | 104.136 us | 106.686 us | 0.60x |
| `industry/mariomka-email-count` | `Count` | 272.494 us | 461.523 us | 7,754.750 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.198 us | 0.238 us | 0.56x |
| `common/matches-word` | `Count` | 1.231 us | 2.348 us | 3.551 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 629.964 us | 1,335.743 us | 7,331.700 us | 0.47x |
| `industry/leipzig-name-family-count` | `Count` | 1,097.272 us | 3,062.588 us | 8,612.950 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.106 us | 0.126 us | 0.34x |
| `common/matches-boundary` | `Count` | 29.858 us | 104.934 us | 106.150 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.245 us | 597.293 us | 858.950 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,844.588 us | 8,703.544 us | 9,289.538 us | 0.21x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.089 us | 0.115 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 266.794 us | 4,002.574 us | 4,350.867 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 295.139 us | 4,774.694 us | 5,238.267 us | 0.06x |
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
| `lokad/imports/module-imports` | 5.939 us | 6.370 us | 32.429 us | 0.93x |
| `fallback/lookahead` | 5.294 us | 6.012 us | 20.355 us | 0.88x |
| `lokad/lexer/string` | 64.812 us | 80.890 us | 85.284 us | 0.80x |
| `lokad/langserv/color-short-hex` | 0.581 us | 0.826 us | 1.013 us | 0.70x |
| `structural/ordered-keyword-window` | 11.742 us | 17.772 us | 31.961 us | 0.66x |
| `structural/method-family-generic-call` | 4.334 us | 6.583 us | 21.661 us | 0.66x |
| `structural/method-family-call` | 5.313 us | 8.089 us | 22.383 us | 0.66x |
| `lokad/lexer/identifier` | 8.778 us | 13.530 us | 16.639 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.688 us | 7.399 us | 8.956 us | 0.63x |
| `lokad/lexer/doc-line` | 1.127 us | 1.883 us | 2.607 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.321 us | 17.285 us | 31.556 us | 0.60x |
| `lokad/docs/inline-doc-prefix` | 2.027 us | 3.490 us | 4.292 us | 0.58x |
| `structural/keyword-to-generic-type` | 2.284 us | 4.093 us | 19.249 us | 0.56x |
| `lokad/lexer/operator-run` | 1.203 us | 2.327 us | 2.812 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.468 us | 4.792 us | 5.744 us | 0.51x |
| `literal/identifier-token` | 2.248 us | 4.390 us | 18.544 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.612 us | 5.371 us | 20.724 us | 0.49x |
| `lokad/style/hex-color` | 0.235 us | 0.518 us | 0.606 us | 0.45x |
| `lokad/style/cell-ref` | 0.863 us | 1.937 us | 2.200 us | 0.45x |
| `literal/call-token` | 1.855 us | 4.283 us | 18.725 us | 0.43x |
| `lokad/style/range-ref` | 0.787 us | 1.844 us | 2.007 us | 0.43x |
| `literal-family/type-token-family` | 9.151 us | 23.802 us | 39.830 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.697 us | 1.824 us | 2.056 us | 0.38x |
| `lokad/langserv/url-dashboard` | 8.737 us | 23.490 us | 23.772 us | 0.37x |
| `lokad/lexer/number` | 3.283 us | 10.694 us | 11.710 us | 0.31x |
| `structural/ordered-identifier-window` | 8.223 us | 28.636 us | 42.853 us | 0.29x |
| `literal-family/method-token-family` | 2.642 us | 9.366 us | 23.671 us | 0.28x |
| `literal/identifier-token-casei` | 3.882 us | 14.765 us | 29.898 us | 0.26x |
| `structural/modifier-family-to-type-window` | 58.860 us | 255.199 us | 270.148 us | 0.23x |
| `lokad/langserv/url-download` | 11.091 us | 51.216 us | 51.705 us | 0.22x |
| `lokad/folding/region-marker` | 5.636 us | 35.947 us | 62.930 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.829 us | 219.159 us | 234.849 us | 0.04x |
| `structural/modifier-family-to-type-pair` | 2.997 us | 135.346 us | 151.427 us | 0.02x |
| `balancing/begin-end-blocks` | 46.466 us | 4,156.778 us | 4,141.632 us | 0.01x |
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
| `lokad/lexer/string` | 64.901 us | 30.713 us | 33.325 us | 2.11x |
| `lokad/docs/inline-doc-prefix` | 2.046 us | 1.451 us | 2.248 us | 1.41x |
| `lokad/langserv/url-dashboard` | 8.737 us | 6.309 us | 6.808 us | 1.38x |
| `lokad/imports/module-imports` | 5.982 us | 4.394 us | 29.636 us | 1.36x |
| `structural/ordered-keyword-window` | 11.695 us | 8.638 us | 22.711 us | 1.35x |
| `fallback/lookahead` | 5.359 us | 4.215 us | 18.067 us | 1.27x |
| `structural/keyword-family-to-capitalized-identifier` | 9.873 us | 7.849 us | 22.309 us | 1.26x |
| `lokad/lexer/operator-run` | 1.161 us | 0.946 us | 1.409 us | 1.23x |
| `structural/ordered-modifier-pair` | 10.326 us | 8.585 us | 22.775 us | 1.20x |
| `lokad/langserv/identifier-validator` | 4.715 us | 4.120 us | 5.804 us | 1.14x |
| `lokad/lexer/identifier` | 8.873 us | 8.132 us | 10.742 us | 1.09x |
| `lokad/style/range-ref` | 0.792 us | 0.736 us | 0.867 us | 1.08x |
| `lokad/lexer/doc-line` | 1.134 us | 1.058 us | 1.819 us | 1.07x |
| `lokad/langserv/color-short-hex` | 0.509 us | 0.475 us | 0.679 us | 1.07x |
| `structural/method-family-call` | 5.353 us | 5.049 us | 18.956 us | 1.06x |
| `structural/modifier-family-to-type-window` | 59.019 us | 56.060 us | 71.474 us | 1.05x |
| `lokad/style/cell-ref` | 0.859 us | 0.836 us | 1.058 us | 1.03x |
| `lokad/style/hex-color` | 0.219 us | 0.230 us | 0.313 us | 0.95x |
| `structural/method-family-generic-call` | 4.348 us | 4.591 us | 18.481 us | 0.95x |
| `lokad/langserv/helper-identifier` | 2.465 us | 2.679 us | 3.561 us | 0.92x |
| `lokad/langserv/url-download` | 11.082 us | 12.055 us | 12.692 us | 0.92x |
| `lokad/lexer/number` | 3.171 us | 3.845 us | 4.888 us | 0.82x |
| `literal-family/type-token-family` | 9.164 us | 11.322 us | 26.339 us | 0.81x |
| `structural/ordered-identifier-window` | 8.360 us | 10.491 us | 24.936 us | 0.80x |
| `lokad/langserv/color-rgb-hex` | 0.642 us | 0.849 us | 1.108 us | 0.76x |
| `literal/identifier-token-casei` | 3.918 us | 5.871 us | 20.132 us | 0.67x |
| `structural/keyword-to-generic-type` | 2.290 us | 3.478 us | 17.885 us | 0.66x |
| `structural/ordered-keyword-pair` | 2.617 us | 4.138 us | 18.491 us | 0.63x |
| `literal-family/method-token-family` | 2.634 us | 5.067 us | 19.422 us | 0.52x |
| `literal/identifier-token` | 2.251 us | 4.684 us | 17.991 us | 0.48x |
| `literal/call-token` | 1.858 us | 3.953 us | 17.681 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.989 us | 10.811 us | 25.516 us | 0.28x |
| `lokad/folding/region-marker` | 5.737 us | 35.977 us | 62.936 us | 0.16x |
| `balancing/begin-end-blocks` | 47.054 us | 463.341 us | 497.314 us | 0.10x |
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
