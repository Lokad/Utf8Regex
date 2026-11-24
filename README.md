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
| `literal/sherlock-zh` | 21.672 us | 18.293 us | 2,112.843 us | 1.18x |
| `literal/sherlock-ru` | 66.673 us | 95.212 us | 5,133.220 us | 0.70x |
| `literal-alternate/sherlock-zh` | 42.713 us | 67.188 us | 2,139.731 us | 0.64x |
| `ruff-noqa/tweaked` | 3,903.868 us | 7,638.527 us | 41,518.450 us | 0.51x |
| `literal/sherlock-casei-en` | 77.552 us | 181.643 us | 592.069 us | 0.43x |
| `literal/sherlock-casei-ru` | 149.157 us | 360.827 us | 5,622.515 us | 0.41x |
| `bounded-repeat/letters-en` | 1,088.676 us | 2,725.468 us | 2,906.873 us | 0.40x |
| `literal/sherlock-en` | 30.711 us | 85.087 us | 460.640 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,964.149 us | 5,686.507 us | 6,513.033 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,399.768 us | 4,609.359 us | 9,685.625 us | 0.30x |
| `dictionary/single` | 7,339.720 us | 25,119.025 us | 25,396.333 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 167.536 us | 580.005 us | 1,012.473 us | 0.29x |
| `bounded-repeat/capitals` | 11,378.314 us | 43,800.650 us | 47,459.950 us | 0.26x |
| `literal-alternate/sherlock-en` | 336.293 us | 1,486.140 us | 2,073.900 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 125.872 us | 648.697 us | 1,089.068 us | 0.19x |
| `aws-keys/quick` | 1,860.534 us | 9,613.638 us | 39,331.650 us | 0.19x |
| `aws-keys/full` | 8,319.089 us | 86,102.700 us | 115,691.500 us | 0.10x |
| `bounded-repeat/context` | 9,529.222 us | 143,753.000 us | 147,398.100 us | 0.07x |
| `ruff-noqa/real` | 3,378.748 us | 68,188.000 us | 84,809.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 850.340 us | 22,856.025 us | 27,969.300 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 106.142 us | 10,888.914 us | 11,815.114 us | 0.0097x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.080 us | 0.076 us | 0.088 us | 1.06x |
| `common/date-match` | `IsMatch` | 0.110 us | 0.106 us | 0.134 us | 1.03x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.080 us | 0.080 us | 0.095 us | 0.99x |
| `common/ip-match` | `IsMatch` | 0.147 us | 0.173 us | 0.191 us | 0.85x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.068 us | 0.081 us | 0.099 us | 0.83x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.088 us | 0.108 us | 0.140 us | 0.81x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.130 us | 0.167 us | 0.183 us | 0.78x |
| `industry/mariomka-email-count` | `Count` | 376.000 us | 493.917 us | 8,756.950 us | 0.76x |
| `common/uri-miss` | `IsMatch` | 0.132 us | 0.188 us | 0.205 us | 0.70x |
| `common/uri-match` | `IsMatch` | 0.158 us | 0.229 us | 0.263 us | 0.69x |
| `common/email-match` | `IsMatch` | 0.152 us | 0.255 us | 0.280 us | 0.59x |
| `industry/mariomka-uri-count` | `Count` | 1,123.642 us | 1,894.659 us | 10,242.367 us | 0.59x |
| `industry/leipzig-twain-count` | `Count` | 615.594 us | 1,291.594 us | 8,562.589 us | 0.48x |
| `common/ip-miss` | `IsMatch` | 0.296 us | 0.701 us | 0.719 us | 0.42x |
| `industry/mariomka-ip-count` | `Count` | 3,182.112 us | 8,063.690 us | 16,307.700 us | 0.39x |
| `common/match-word` | `Match` | 0.048 us | 0.123 us | 1.106 us | 0.39x |
| `common/date-miss` | `IsMatch` | 0.183 us | 0.549 us | 0.584 us | 0.33x |
| `common/matches-set` | `Count` | 61.802 us | 188.272 us | 190.108 us | 0.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 79.469 us | 257.351 us | 495.211 us | 0.31x |
| `common/matches-word` | `Count` | 1.212 us | 4.283 us | 5.985 us | 0.28x |
| `common/replace-words` | `Replace` | 20.109 us | 75.254 us | 76.344 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.821 us | 950.587 us | 1,228.748 us | 0.17x |
| `common/split-words` | `Split` | 12.849 us | 79.003 us | 80.247 us | 0.16x |
| `common/matches-boundary` | `Count` | 23.645 us | 163.700 us | 165.403 us | 0.14x |
| `common/matches-words` | `Count` | 8.920 us | 69.493 us | 70.988 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,047.719 us | 9,512.978 us | 16,784.433 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,801.733 us | 19,104.400 us | 19,622.475 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 844.883 us | 11,846.571 us | 12,477.614 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.600 us | 0.624 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.811 us | 0.851 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 988.317 us | 22,058.275 us | 28,979.050 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.338 us | 0.364 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,229.106 us | 31,492.633 us | 38,109.050 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 270.711 us | 8,207.900 us | 8,644.000 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 42.640 us | 35.511 us | 2,108.886 us | 1.20x |
| `literal/sherlock-casei-en` | 77.495 us | 75.211 us | 457.796 us | 1.03x |
| `literal-alternate/sherlock-en-mixed` | 169.483 us | 175.709 us | 572.240 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,961.800 us | 2,083.995 us | 2,918.608 us | 0.94x |
| `literal-alternate/sherlock-en` | 337.499 us | 359.025 us | 743.725 us | 0.94x |
| `literal/sherlock-zh` | 21.630 us | 23.129 us | 2,124.189 us | 0.94x |
| `ruff-noqa/tweaked` | 3,977.084 us | 4,457.906 us | 35,414.650 us | 0.89x |
| `bounded-repeat/capitals` | 11,369.657 us | 13,097.900 us | 16,008.950 us | 0.87x |
| `literal/sherlock-ru` | 66.326 us | 86.111 us | 5,206.673 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 107.126 us | 140.991 us | 588.769 us | 0.76x |
| `literal/sherlock-casei-ru` | 149.034 us | 219.408 us | 5,389.014 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 125.213 us | 200.717 us | 568.682 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,403.954 us | 2,286.882 us | 7,290.750 us | 0.61x |
| `bounded-repeat/letters-en` | 1,089.210 us | 1,774.333 us | 1,907.703 us | 0.61x |
| `aws-keys/quick` | 1,873.507 us | 3,586.952 us | 34,888.200 us | 0.52x |
| `aws-keys/full` | 8,358.367 us | 18,124.180 us | 49,046.833 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 860.081 us | 1,944.782 us | 6,823.700 us | 0.44x |
| `literal/sherlock-en` | 30.837 us | 74.012 us | 433.079 us | 0.42x |
| `ruff-noqa/real` | 3,396.039 us | 20,328.000 us | 46,996.567 us | 0.17x |
| `dictionary/single` | 7,318.591 us | 44,256.800 us | 44,197.600 us | 0.17x |
| `bounded-repeat/context` | 9,302.644 us | 67,336.950 us | 70,396.100 us | 0.14x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.144 us | 0.047 us | 0.067 us | 3.04x |
| `common/date-match` | `IsMatch` | 0.110 us | 0.042 us | 0.070 us | 2.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.079 us | 0.031 us | 0.048 us | 2.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.129 us | 0.055 us | 0.075 us | 2.36x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.078 us | 0.035 us | 0.051 us | 2.23x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.067 us | 0.031 us | 0.049 us | 2.13x |
| `common/uri-miss` | `IsMatch` | 0.132 us | 0.063 us | 0.086 us | 2.09x |
| `common/email-match` | `IsMatch` | 0.150 us | 0.078 us | 0.105 us | 1.93x |
| `common/uri-match` | `IsMatch` | 0.159 us | 0.088 us | 0.118 us | 1.81x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.085 us | 0.047 us | 0.081 us | 1.80x |
| `common/ip-miss` | `IsMatch` | 0.296 us | 0.174 us | 0.197 us | 1.70x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 79.460 us | 56.793 us | 244.249 us | 1.40x |
| `common/replace-words` | `Replace` | 20.183 us | 16.642 us | 17.645 us | 1.21x |
| `common/match-word` | `Match` | 0.049 us | 0.047 us | 0.898 us | 1.03x |
| `industry/mariomka-uri-count` | `Count` | 1,122.150 us | 1,089.701 us | 8,610.825 us | 1.03x |
| `industry/mariomka-ip-count` | `Count` | 3,212.171 us | 3,366.539 us | 10,401.767 us | 0.95x |
| `common/date-miss` | `IsMatch` | 0.181 us | 0.196 us | 0.235 us | 0.92x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.041 us | 0.073 us | 0.86x |
| `common/matches-words` | `Count` | 8.916 us | 10.567 us | 13.614 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 996.237 us | 1,199.410 us | 7,084.900 us | 0.83x |
| `industry/mariomka-email-count` | `Count` | 375.719 us | 459.812 us | 7,537.250 us | 0.82x |
| `industry/leipzig-river-window-count` | `Count` | 1,245.037 us | 1,533.546 us | 7,721.360 us | 0.81x |
| `common/split-words` | `Split` | 12.840 us | 20.305 us | 22.057 us | 0.63x |
| `common/matches-set` | `Count` | 61.885 us | 103.017 us | 103.506 us | 0.60x |
| `common/matches-word` | `Count` | 1.204 us | 2.335 us | 3.490 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 616.692 us | 1,264.400 us | 7,497.540 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,057.142 us | 2,965.335 us | 7,778.400 us | 0.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.589 us | 580.529 us | 813.285 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.105 us | 0.126 us | 0.27x |
| `common/matches-boundary` | `Count` | 23.776 us | 102.080 us | 103.663 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,803.981 us | 8,506.111 us | 9,023.589 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 843.833 us | 4,625.871 us | 5,055.007 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.087 us | 0.113 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 269.044 us | 3,835.065 us | 4,161.689 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.277 us | 6.165 us | 30.683 us | 1.18x |
| `fallback/lookahead` | 6.633 us | 5.871 us | 19.128 us | 1.13x |
| `structural/method-family-call` | 7.716 us | 7.832 us | 20.822 us | 0.99x |
| `structural/method-family-generic-call` | 6.210 us | 6.464 us | 19.965 us | 0.96x |
| `lokad/lexer/identifier` | 12.260 us | 13.475 us | 16.047 us | 0.91x |
| `lokad/style/hex-color` | 0.436 us | 0.483 us | 0.570 us | 0.90x |
| `lokad/langserv/identifier-validator` | 6.737 us | 7.491 us | 8.833 us | 0.90x |
| `lokad/lexer/doc-line` | 1.623 us | 1.861 us | 2.646 us | 0.87x |
| `lokad/langserv/color-short-hex` | 0.701 us | 0.810 us | 0.987 us | 0.87x |
| `lokad/lexer/operator-run` | 1.799 us | 2.316 us | 2.733 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 1.372 us | 1.797 us | 2.006 us | 0.76x |
| `structural/ordered-keyword-window` | 12.960 us | 17.439 us | 30.097 us | 0.74x |
| `structural/keyword-to-generic-type` | 2.897 us | 3.941 us | 17.739 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 2.412 us | 3.378 us | 4.180 us | 0.71x |
| `lokad/langserv/helper-identifier` | 2.711 us | 4.646 us | 5.555 us | 0.58x |
| `structural/ordered-keyword-pair` | 3.097 us | 5.341 us | 19.070 us | 0.58x |
| `lokad/lexer/number` | 6.064 us | 10.597 us | 11.535 us | 0.57x |
| `structural/ordered-modifier-pair` | 9.807 us | 17.186 us | 30.695 us | 0.57x |
| `literal/identifier-token` | 2.282 us | 4.230 us | 17.629 us | 0.54x |
| `lokad/style/range-ref` | 0.803 us | 1.755 us | 1.888 us | 0.46x |
| `lokad/style/cell-ref` | 0.855 us | 1.944 us | 2.106 us | 0.44x |
| `literal/call-token` | 1.801 us | 4.267 us | 17.035 us | 0.42x |
| `lokad/langserv/url-dashboard` | 9.751 us | 23.211 us | 23.016 us | 0.42x |
| `literal-family/type-token-family` | 8.973 us | 23.144 us | 36.532 us | 0.39x |
| `lokad/lexer/string` | 26.301 us | 80.181 us | 83.899 us | 0.33x |
| `structural/ordered-identifier-window` | 8.249 us | 27.856 us | 40.697 us | 0.30x |
| `literal-family/method-token-family` | 2.561 us | 8.999 us | 21.700 us | 0.28x |
| `structural/modifier-family-to-type-window` | 64.339 us | 247.819 us | 260.675 us | 0.26x |
| `literal/identifier-token-casei` | 3.863 us | 15.049 us | 28.293 us | 0.26x |
| `lokad/langserv/url-download` | 11.209 us | 49.687 us | 53.346 us | 0.23x |
| `lokad/folding/region-marker` | 5.054 us | 34.446 us | 61.950 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 14.029 us | 214.467 us | 227.641 us | 0.07x |
| `structural/modifier-family-to-type-pair` | 2.811 us | 126.081 us | 142.901 us | 0.02x |
| `balancing/begin-end-blocks` | 45.296 us | 4,186.221 us | 4,162.695 us | 0.01x |
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
| `lokad/style/hex-color` | 0.443 us | 0.222 us | 0.305 us | 2.00x |
| `lokad/lexer/operator-run` | 1.773 us | 0.899 us | 1.385 us | 1.97x |
| `lokad/docs/inline-doc-prefix` | 2.431 us | 1.414 us | 2.172 us | 1.72x |
| `lokad/langserv/color-rgb-hex` | 1.394 us | 0.828 us | 1.077 us | 1.68x |
| `lokad/imports/module-imports` | 7.266 us | 4.321 us | 27.777 us | 1.68x |
| `lokad/lexer/number` | 6.173 us | 3.693 us | 4.809 us | 1.67x |
| `lokad/langserv/identifier-validator` | 6.721 us | 4.080 us | 5.636 us | 1.65x |
| `fallback/lookahead` | 6.650 us | 4.113 us | 16.758 us | 1.62x |
| `lokad/langserv/url-dashboard` | 9.829 us | 6.134 us | 6.823 us | 1.60x |
| `lokad/lexer/identifier` | 12.268 us | 7.838 us | 10.497 us | 1.57x |
| `lokad/lexer/doc-line` | 1.637 us | 1.051 us | 1.869 us | 1.56x |
| `structural/ordered-keyword-window` | 12.944 us | 8.383 us | 21.092 us | 1.54x |
| `structural/method-family-call` | 7.730 us | 5.010 us | 17.394 us | 1.54x |
| `lokad/langserv/color-short-hex` | 0.702 us | 0.476 us | 0.664 us | 1.47x |
| `structural/method-family-generic-call` | 6.221 us | 4.427 us | 17.094 us | 1.41x |
| `structural/keyword-family-to-capitalized-identifier` | 14.018 us | 11.176 us | 20.890 us | 1.25x |
| `structural/ordered-modifier-pair` | 9.846 us | 8.399 us | 21.405 us | 1.17x |
| `structural/modifier-family-to-type-window` | 64.161 us | 55.136 us | 68.260 us | 1.16x |
| `lokad/style/range-ref` | 0.786 us | 0.703 us | 0.838 us | 1.12x |
| `lokad/style/cell-ref` | 0.855 us | 0.807 us | 1.016 us | 1.06x |
| `lokad/langserv/helper-identifier` | 2.691 us | 2.637 us | 3.526 us | 1.02x |
| `lokad/langserv/url-download` | 11.265 us | 11.655 us | 12.120 us | 0.97x |
| `lokad/lexer/string` | 26.396 us | 29.733 us | 32.786 us | 0.89x |
| `structural/keyword-to-generic-type` | 2.922 us | 3.423 us | 16.800 us | 0.85x |
| `literal-family/type-token-family` | 9.040 us | 11.169 us | 24.180 us | 0.81x |
| `structural/ordered-identifier-window` | 8.289 us | 10.345 us | 23.422 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.129 us | 4.024 us | 17.011 us | 0.78x |
| `literal/identifier-token-casei` | 3.852 us | 5.843 us | 18.829 us | 0.66x |
| `literal-family/method-token-family` | 2.550 us | 4.920 us | 17.486 us | 0.52x |
| `literal/identifier-token` | 2.271 us | 4.576 us | 17.086 us | 0.50x |
| `literal/call-token` | 1.826 us | 3.876 us | 16.166 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.839 us | 10.316 us | 23.604 us | 0.28x |
| `lokad/folding/region-marker` | 4.938 us | 35.859 us | 61.174 us | 0.14x |
| `balancing/begin-end-blocks` | 46.090 us | 454.214 us | 481.335 us | 0.10x |
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
