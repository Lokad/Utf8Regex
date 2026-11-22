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
| `literal/sherlock-zh` | 21.859 us | 18.506 us | 2,162.097 us | 1.18x |
| `literal/sherlock-ru` | 67.240 us | 96.030 us | 5,191.507 us | 0.70x |
| `literal-alternate/sherlock-zh` | 42.911 us | 67.721 us | 2,160.709 us | 0.63x |
| `aws-keys/quick` | 5,166.190 us | 9,469.938 us | 38,897.300 us | 0.55x |
| `aws-keys/full` | 42,693.400 us | 82,853.900 us | 118,034.400 us | 0.52x |
| `ruff-noqa/tweaked` | 3,748.080 us | 7,346.327 us | 42,738.450 us | 0.51x |
| `literal/sherlock-casei-en` | 77.868 us | 181.387 us | 551.690 us | 0.43x |
| `bounded-repeat/letters-en` | 1,103.052 us | 2,755.632 us | 2,934.696 us | 0.40x |
| `literal/sherlock-casei-ru` | 143.450 us | 359.208 us | 5,458.550 us | 0.40x |
| `literal/sherlock-en` | 30.364 us | 82.485 us | 448.529 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,988.562 us | 5,664.679 us | 6,596.783 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,399.244 us | 4,685.231 us | 9,749.750 us | 0.30x |
| `dictionary/single` | 7,220.000 us | 24,577.550 us | 24,628.167 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 151.285 us | 576.072 us | 1,039.618 us | 0.26x |
| `bounded-repeat/capitals` | 10,810.814 us | 43,654.800 us | 47,142.000 us | 0.25x |
| `literal-alternate/sherlock-en` | 337.468 us | 1,513.182 us | 2,055.584 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 125.413 us | 642.102 us | 1,047.733 us | 0.20x |
| `bounded-repeat/context` | 9,693.413 us | 140,316.000 us | 145,079.300 us | 0.07x |
| `ruff-noqa/real` | 3,413.513 us | 67,525.300 us | 83,688.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 815.902 us | 22,580.825 us | 27,804.850 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 98.426 us | 10,985.614 us | 11,774.029 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.120 us | 0.072 us | 0.087 us | 1.66x |
| `common/date-match` | `IsMatch` | 0.164 us | 0.105 us | 0.132 us | 1.56x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.114 us | 0.078 us | 0.094 us | 1.46x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.117 us | 0.082 us | 0.098 us | 1.43x |
| `common/uri-match` | `IsMatch` | 0.291 us | 0.220 us | 0.257 us | 1.32x |
| `common/ip-match` | `IsMatch` | 0.194 us | 0.167 us | 0.186 us | 1.16x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.133 us | 0.116 us | 0.146 us | 1.14x |
| `common/uri-miss` | `IsMatch` | 0.200 us | 0.191 us | 0.212 us | 1.05x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.160 us | 0.155 us | 0.171 us | 1.03x |
| `industry/mariomka-uri-count` | `Count` | 1,583.633 us | 1,810.105 us | 9,992.300 us | 0.87x |
| `industry/mariomka-email-count` | `Count` | 419.293 us | 491.494 us | 8,784.750 us | 0.85x |
| `common/email-match` | `IsMatch` | 0.206 us | 0.265 us | 0.289 us | 0.78x |
| `common/ip-miss` | `IsMatch` | 0.503 us | 0.701 us | 0.721 us | 0.72x |
| `common/date-miss` | `IsMatch` | 0.327 us | 0.537 us | 0.566 us | 0.61x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 147.111 us | 252.360 us | 478.049 us | 0.58x |
| `industry/leipzig-twain-count` | `Count` | 616.718 us | 1,281.133 us | 8,278.578 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.125 us | 1.103 us | 0.43x |
| `common/matches-set` | `Count` | 59.439 us | 185.043 us | 186.562 us | 0.32x |
| `common/matches-word` | `Count` | 1.184 us | 4.041 us | 5.659 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,161.283 us | 7,998.570 us | 16,491.367 us | 0.27x |
| `common/replace-words` | `Replace` | 20.203 us | 74.916 us | 75.929 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.921 us | 924.686 us | 1,216.210 us | 0.18x |
| `common/split-words` | `Split` | 12.901 us | 75.013 us | 76.677 us | 0.17x |
| `common/matches-boundary` | `Count` | 25.254 us | 158.455 us | 160.431 us | 0.16x |
| `common/matches-words` | `Count` | 8.848 us | 68.474 us | 69.012 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,008.141 us | 9,443.500 us | 16,220.067 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,803.464 us | 18,716.380 us | 19,308.175 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 854.882 us | 11,302.843 us | 11,816.229 us | 0.08x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.354 us | 0.357 us | 0.07x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.797 us | 0.842 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.593 us | 0.627 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,769.933 us | 30,438.033 us | 37,433.600 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 999.499 us | 21,809.000 us | 29,386.500 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 285.433 us | 8,024.490 us | 8,418.044 us | 0.04x |
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
| `aws-keys/full` | 42,765.300 us | 17,674.460 us | 53,015.900 us | 2.42x |
| `aws-keys/quick` | 5,259.140 us | 3,631.752 us | 34,413.567 us | 1.45x |
| `literal-alternate/sherlock-zh` | 42.532 us | 35.323 us | 2,093.514 us | 1.20x |
| `literal/sherlock-casei-en` | 78.338 us | 77.453 us | 440.161 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,955.203 us | 2,006.353 us | 2,877.338 us | 0.97x |
| `literal/sherlock-zh` | 21.895 us | 23.454 us | 2,164.340 us | 0.93x |
| `literal-alternate/sherlock-en` | 337.093 us | 361.311 us | 780.451 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 152.344 us | 170.279 us | 584.485 us | 0.89x |
| `ruff-noqa/tweaked` | 3,777.175 us | 4,360.244 us | 36,878.850 us | 0.87x |
| `bounded-repeat/capitals` | 10,936.700 us | 12,972.400 us | 15,823.400 us | 0.84x |
| `literal/sherlock-ru` | 67.496 us | 87.108 us | 5,145.920 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 99.032 us | 142.240 us | 563.515 us | 0.70x |
| `literal/sherlock-casei-ru` | 144.480 us | 218.181 us | 5,226.280 us | 0.66x |
| `bounded-repeat/letters-en` | 1,107.450 us | 1,785.191 us | 1,907.405 us | 0.62x |
| `literal-alternate/sherlock-en-nomatch` | 123.902 us | 200.741 us | 572.255 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,396.521 us | 2,279.406 us | 7,388.400 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 817.136 us | 1,900.680 us | 7,027.875 us | 0.43x |
| `literal/sherlock-en` | 30.350 us | 72.382 us | 443.401 us | 0.42x |
| `ruff-noqa/real` | 3,360.013 us | 20,072.150 us | 47,860.933 us | 0.17x |
| `dictionary/single` | 7,220.345 us | 43,667.400 us | 43,603.600 us | 0.17x |
| `bounded-repeat/context` | 9,684.462 us | 65,976.450 us | 68,780.850 us | 0.15x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-date-match` | `IsMatch` | 0.125 us | 0.029 us | 0.049 us | 4.27x |
| `common/ip-match` | `IsMatch` | 0.194 us | 0.046 us | 0.066 us | 4.22x |
| `common/date-match` | `IsMatch` | 0.163 us | 0.041 us | 0.069 us | 4.00x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.114 us | 0.031 us | 0.047 us | 3.63x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.120 us | 0.035 us | 0.050 us | 3.46x |
| `common/uri-match` | `IsMatch` | 0.289 us | 0.084 us | 0.116 us | 3.45x |
| `common/uri-miss` | `IsMatch` | 0.201 us | 0.065 us | 0.082 us | 3.10x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.162 us | 0.053 us | 0.073 us | 3.07x |
| `common/ip-miss` | `IsMatch` | 0.502 us | 0.173 us | 0.199 us | 2.90x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.132 us | 0.047 us | 0.082 us | 2.80x |
| `common/email-match` | `IsMatch` | 0.207 us | 0.077 us | 0.104 us | 2.69x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 147.557 us | 55.873 us | 274.502 us | 2.64x |
| `common/date-miss` | `IsMatch` | 0.327 us | 0.195 us | 0.228 us | 1.68x |
| `industry/mariomka-uri-count` | `Count` | 1,587.411 us | 1,081.914 us | 8,136.375 us | 1.47x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.042 us | 0.072 us | 1.34x |
| `industry/leipzig-river-window-count` | `Count` | 1,780.240 us | 1,465.275 us | 7,513.025 us | 1.21x |
| `common/replace-words` | `Replace` | 20.041 us | 16.734 us | 17.998 us | 1.20x |
| `common/match-word` | `Match` | 0.053 us | 0.046 us | 0.898 us | 1.16x |
| `industry/mariomka-email-count` | `Count` | 417.854 us | 458.297 us | 7,495.250 us | 0.91x |
| `common/matches-words` | `Count` | 8.818 us | 10.492 us | 13.445 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,000.197 us | 1,230.547 us | 7,297.660 us | 0.81x |
| `industry/mariomka-ip-count` | `Count` | 2,157.469 us | 3,415.952 us | 10,691.767 us | 0.63x |
| `common/split-words` | `Split` | 12.786 us | 20.477 us | 22.335 us | 0.62x |
| `common/matches-set` | `Count` | 59.492 us | 100.663 us | 102.317 us | 0.59x |
| `common/matches-word` | `Count` | 1.180 us | 2.244 us | 3.284 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 618.959 us | 1,278.125 us | 7,216.880 us | 0.48x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.086 us | 0.123 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 1,013.355 us | 2,924.900 us | 7,793.250 us | 0.35x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.085 us | 0.112 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.096 us | 570.745 us | 837.939 us | 0.29x |
| `common/matches-boundary` | `Count` | 25.198 us | 100.001 us | 101.882 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,804.098 us | 8,505.233 us | 8,981.322 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 860.468 us | 4,545.676 us | 4,967.550 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 286.033 us | 3,809.585 us | 4,164.495 us | 0.08x |
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
| `lokad/langserv/identifier-validator` | 22.213 us | 7.061 us | 8.601 us | 3.15x |
| `lokad/lexer/doc-line` | 4.591 us | 1.799 us | 2.529 us | 2.55x |
| `lokad/lexer/identifier` | 32.842 us | 12.894 us | 15.902 us | 2.55x |
| `lokad/langserv/color-rgb-hex` | 4.150 us | 1.715 us | 1.967 us | 2.42x |
| `lokad/langserv/helper-identifier` | 10.716 us | 4.621 us | 5.537 us | 2.32x |
| `lokad/style/hex-color` | 1.125 us | 0.507 us | 0.613 us | 2.22x |
| `lokad/langserv/color-short-hex` | 1.632 us | 0.800 us | 0.983 us | 2.04x |
| `lokad/lexer/number` | 20.200 us | 10.312 us | 11.232 us | 1.96x |
| `lokad/lexer/operator-run` | 4.351 us | 2.244 us | 2.665 us | 1.94x |
| `lokad/style/range-ref` | 3.388 us | 1.753 us | 1.885 us | 1.93x |
| `lokad/docs/inline-doc-prefix` | 6.456 us | 3.368 us | 4.144 us | 1.92x |
| `fallback/lookahead` | 10.272 us | 5.721 us | 18.131 us | 1.80x |
| `structural/method-family-generic-call` | 10.908 us | 6.312 us | 19.683 us | 1.73x |
| `lokad/style/cell-ref` | 3.065 us | 1.863 us | 2.055 us | 1.65x |
| `lokad/imports/module-imports` | 8.005 us | 6.050 us | 30.390 us | 1.32x |
| `structural/method-family-call` | 9.612 us | 7.706 us | 20.569 us | 1.25x |
| `structural/ordered-keyword-window` | 20.842 us | 17.341 us | 30.422 us | 1.20x |
| `lokad/lexer/string` | 92.658 us | 78.940 us | 83.022 us | 1.17x |
| `structural/keyword-to-generic-type` | 2.949 us | 3.897 us | 17.331 us | 0.76x |
| `structural/ordered-keyword-pair` | 3.415 us | 5.074 us | 18.389 us | 0.67x |
| `structural/ordered-modifier-pair` | 9.819 us | 16.393 us | 28.992 us | 0.60x |
| `structural/ordered-identifier-window` | 14.571 us | 27.380 us | 40.828 us | 0.53x |
| `lokad/langserv/url-dashboard` | 11.657 us | 22.477 us | 23.101 us | 0.52x |
| `literal/identifier-token` | 2.178 us | 4.205 us | 17.013 us | 0.52x |
| `literal/call-token` | 1.786 us | 4.184 us | 16.911 us | 0.43x |
| `structural/modifier-family-to-type-window` | 104.392 us | 245.263 us | 261.742 us | 0.43x |
| `literal-family/type-token-family` | 9.477 us | 22.690 us | 36.017 us | 0.42x |
| `literal-family/method-token-family` | 3.641 us | 8.786 us | 21.109 us | 0.41x |
| `balancing/begin-end-blocks` | 1,304.756 us | 3,988.026 us | 3,979.337 us | 0.33x |
| `lokad/langserv/url-download` | 12.262 us | 48.757 us | 49.665 us | 0.25x |
| `literal/identifier-token-casei` | 3.355 us | 14.502 us | 27.531 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 22.765 us | 130.036 us | 143.019 us | 0.18x |
| `lokad/folding/region-marker` | 5.076 us | 34.068 us | 63.252 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 15.227 us | 214.570 us | 228.588 us | 0.07x |
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
| `lokad/lexer/number` | 20.228 us | 3.640 us | 4.737 us | 5.56x |
| `lokad/langserv/identifier-validator` | 22.186 us | 4.036 us | 5.554 us | 5.50x |
| `lokad/style/hex-color` | 1.125 us | 0.218 us | 0.299 us | 5.16x |
| `lokad/langserv/color-rgb-hex` | 4.147 us | 0.846 us | 1.063 us | 4.90x |
| `lokad/lexer/operator-run` | 4.360 us | 0.898 us | 1.343 us | 4.86x |
| `lokad/style/range-ref` | 3.390 us | 0.706 us | 0.821 us | 4.80x |
| `lokad/docs/inline-doc-prefix` | 6.428 us | 1.361 us | 2.079 us | 4.72x |
| `lokad/lexer/doc-line` | 4.552 us | 1.012 us | 1.767 us | 4.50x |
| `lokad/lexer/identifier` | 32.947 us | 7.685 us | 10.500 us | 4.29x |
| `lokad/langserv/helper-identifier` | 10.698 us | 2.610 us | 3.543 us | 4.10x |
| `lokad/style/cell-ref` | 3.020 us | 0.793 us | 0.997 us | 3.81x |
| `lokad/langserv/color-short-hex` | 1.637 us | 0.459 us | 0.642 us | 3.56x |
| `lokad/lexer/string` | 94.219 us | 29.227 us | 32.348 us | 3.22x |
| `balancing/begin-end-blocks` | 1,310.781 us | 443.911 us | 473.370 us | 2.95x |
| `fallback/lookahead` | 10.278 us | 4.038 us | 15.936 us | 2.55x |
| `structural/ordered-keyword-window` | 20.854 us | 8.311 us | 21.419 us | 2.51x |
| `structural/method-family-generic-call` | 10.918 us | 4.429 us | 16.960 us | 2.47x |
| `structural/modifier-family-to-type-pair` | 22.751 us | 10.321 us | 22.928 us | 2.20x |
| `structural/keyword-family-to-capitalized-identifier` | 15.219 us | 7.418 us | 20.590 us | 2.05x |
| `structural/method-family-call` | 9.526 us | 4.865 us | 17.270 us | 1.96x |
| `lokad/langserv/url-dashboard` | 11.730 us | 6.093 us | 6.539 us | 1.93x |
| `structural/modifier-family-to-type-window` | 104.831 us | 54.732 us | 68.586 us | 1.92x |
| `lokad/imports/module-imports` | 8.024 us | 4.247 us | 27.730 us | 1.89x |
| `structural/ordered-identifier-window` | 14.583 us | 10.209 us | 23.324 us | 1.43x |
| `structural/ordered-modifier-pair` | 9.803 us | 8.177 us | 20.590 us | 1.20x |
| `lokad/langserv/url-download` | 12.105 us | 11.592 us | 12.271 us | 1.04x |
| `structural/keyword-to-generic-type` | 2.951 us | 3.365 us | 16.213 us | 0.88x |
| `structural/ordered-keyword-pair` | 3.416 us | 3.976 us | 16.539 us | 0.86x |
| `literal-family/type-token-family` | 9.486 us | 11.060 us | 23.616 us | 0.86x |
| `literal-family/method-token-family` | 3.670 us | 5.072 us | 17.299 us | 0.72x |
| `literal/identifier-token-casei` | 3.381 us | 5.734 us | 18.490 us | 0.59x |
| `literal/identifier-token` | 2.192 us | 4.140 us | 16.616 us | 0.53x |
| `literal/call-token` | 1.788 us | 3.816 us | 15.994 us | 0.47x |
| `lokad/folding/region-marker` | 5.040 us | 34.481 us | 60.720 us | 0.15x |
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
