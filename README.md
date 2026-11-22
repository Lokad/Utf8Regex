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
| `literal/sherlock-zh` | 20.701 us | 17.478 us | 2,063.395 us | 1.18x |
| `literal/sherlock-ru` | 64.911 us | 93.109 us | 4,943.440 us | 0.70x |
| `literal-alternate/sherlock-zh` | 40.941 us | 65.047 us | 2,070.432 us | 0.63x |
| `aws-keys/quick` | 5,033.300 us | 9,266.100 us | 44,709.000 us | 0.54x |
| `ruff-noqa/tweaked` | 3,810.535 us | 7,432.618 us | 38,418.450 us | 0.51x |
| `aws-keys/full` | 41,798.400 us | 84,454.400 us | 116,841.500 us | 0.49x |
| `literal/sherlock-casei-ru` | 146.173 us | 349.206 us | 5,309.420 us | 0.42x |
| `bounded-repeat/letters-en` | 1,051.974 us | 2,650.759 us | 2,787.304 us | 0.40x |
| `literal/sherlock-en` | 30.133 us | 83.971 us | 445.299 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,903.741 us | 5,515.714 us | 6,333.858 us | 0.35x |
| `literal/sherlock-casei-en` | 65.772 us | 190.878 us | 523.919 us | 0.34x |
| `dictionary/single` | 7,691.180 us | 24,396.975 us | 24,371.050 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,395.760 us | 4,769.694 us | 9,679.350 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 158.609 us | 560.413 us | 964.373 us | 0.28x |
| `bounded-repeat/capitals` | 10,979.314 us | 43,509.650 us | 46,613.300 us | 0.25x |
| `literal-alternate/sherlock-en` | 334.417 us | 1,451.310 us | 1,945.888 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 120.065 us | 632.599 us | 1,023.067 us | 0.19x |
| `bounded-repeat/context` | 8,311.680 us | 139,358.200 us | 145,312.300 us | 0.06x |
| `ruff-noqa/real` | 3,266.678 us | 66,146.750 us | 99,822.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 835.385 us | 22,148.250 us | 26,928.450 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 95.553 us | 11,067.300 us | 11,732.443 us | 0.0086x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.152 us | 0.102 us | 0.129 us | 1.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.105 us | 0.072 us | 0.086 us | 1.46x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.142 us | 0.103 us | 0.138 us | 1.39x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.110 us | 0.080 us | 0.095 us | 1.37x |
| `common/uri-match` | `IsMatch` | 0.280 us | 0.218 us | 0.252 us | 1.29x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.100 us | 0.078 us | 0.095 us | 1.28x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.177 us | 0.154 us | 0.170 us | 1.15x |
| `common/ip-match` | `IsMatch` | 0.185 us | 0.165 us | 0.186 us | 1.12x |
| `common/uri-miss` | `IsMatch` | 0.195 us | 0.177 us | 0.194 us | 1.10x |
| `industry/mariomka-email-count` | `Count` | 402.429 us | 473.409 us | 8,433.267 us | 0.85x |
| `industry/mariomka-uri-count` | `Count` | 1,407.925 us | 1,805.721 us | 9,783.225 us | 0.78x |
| `common/email-match` | `IsMatch` | 0.196 us | 0.253 us | 0.276 us | 0.78x |
| `common/ip-miss` | `IsMatch` | 0.485 us | 0.676 us | 0.702 us | 0.72x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 156.154 us | 246.526 us | 462.167 us | 0.63x |
| `common/date-miss` | `IsMatch` | 0.297 us | 0.535 us | 0.570 us | 0.55x |
| `industry/leipzig-twain-count` | `Count` | 600.759 us | 1,265.410 us | 8,306.311 us | 0.47x |
| `common/match-word` | `Match` | 0.053 us | 0.121 us | 1.114 us | 0.44x |
| `common/matches-set` | `Count` | 57.550 us | 182.463 us | 183.944 us | 0.32x |
| `common/matches-word` | `Count` | 1.173 us | 4.030 us | 5.681 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,101.456 us | 7,813.890 us | 16,129.133 us | 0.27x |
| `common/replace-words` | `Replace` | 19.690 us | 73.504 us | 73.967 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.365 us | 927.241 us | 1,176.626 us | 0.17x |
| `common/split-words` | `Split` | 12.410 us | 72.835 us | 74.598 us | 0.17x |
| `common/matches-boundary` | `Count` | 24.862 us | 158.931 us | 161.723 us | 0.16x |
| `common/matches-words` | `Count` | 8.666 us | 67.028 us | 68.235 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 987.605 us | 9,453.289 us | 16,183.833 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,794.351 us | 18,231.460 us | 18,608.700 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.331 us | 0.356 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 853.702 us | 11,081.200 us | 11,605.443 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.782 us | 0.823 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.040 us | 0.580 us | 0.609 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,745.252 us | 32,021.500 us | 38,620.200 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 961.681 us | 21,762.100 us | 28,427.800 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 279.406 us | 8,166.140 us | 8,586.811 us | 0.03x |
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
| `aws-keys/full` | 42,044.900 us | 17,633.200 us | 51,976.567 us | 2.38x |
| `aws-keys/quick` | 5,042.753 us | 3,449.659 us | 35,586.900 us | 1.46x |
| `literal-alternate/sherlock-zh` | 40.994 us | 34.246 us | 2,044.779 us | 1.20x |
| `bounded-repeat/letters-ru` | 1,903.918 us | 1,937.215 us | 2,745.925 us | 0.98x |
| `literal-alternate/sherlock-en` | 333.861 us | 347.127 us | 716.533 us | 0.96x |
| `literal-alternate/sherlock-en-mixed` | 158.169 us | 164.695 us | 521.436 us | 0.96x |
| `literal/sherlock-zh` | 20.880 us | 22.634 us | 2,064.424 us | 0.92x |
| `literal/sherlock-casei-en` | 65.666 us | 74.318 us | 408.641 us | 0.88x |
| `ruff-noqa/tweaked` | 3,814.625 us | 4,338.789 us | 35,108.000 us | 0.88x |
| `bounded-repeat/capitals` | 11,017.329 us | 12,891.533 us | 15,591.425 us | 0.85x |
| `literal/sherlock-ru` | 64.836 us | 83.968 us | 4,940.960 us | 0.77x |
| `literal/sherlock-casei-ru` | 147.412 us | 211.516 us | 5,083.853 us | 0.70x |
| `literal-alternate/sherlock-casei-en` | 95.793 us | 140.655 us | 552.971 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 120.296 us | 194.356 us | 560.308 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,393.838 us | 2,266.615 us | 7,264.317 us | 0.61x |
| `bounded-repeat/letters-en` | 1,046.925 us | 1,728.939 us | 1,842.915 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 835.335 us | 1,874.598 us | 6,797.650 us | 0.45x |
| `literal/sherlock-en` | 30.119 us | 73.479 us | 423.875 us | 0.41x |
| `dictionary/single` | 7,686.480 us | 43,236.150 us | 43,465.350 us | 0.18x |
| `ruff-noqa/real` | 3,342.070 us | 19,137.820 us | 52,410.100 us | 0.17x |
| `bounded-repeat/context` | 8,361.200 us | 65,127.050 us | 68,134.550 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.185 us | 0.046 us | 0.064 us | 4.04x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.109 us | 0.029 us | 0.046 us | 3.72x |
| `common/date-match` | `IsMatch` | 0.152 us | 0.041 us | 0.069 us | 3.68x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.099 us | 0.029 us | 0.044 us | 3.46x |
| `common/uri-match` | `IsMatch` | 0.279 us | 0.084 us | 0.114 us | 3.32x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.171 us | 0.053 us | 0.073 us | 3.23x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.142 us | 0.046 us | 0.081 us | 3.11x |
| `common/uri-miss` | `IsMatch` | 0.194 us | 0.063 us | 0.081 us | 3.08x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.104 us | 0.034 us | 0.051 us | 3.06x |
| `common/ip-miss` | `IsMatch` | 0.487 us | 0.170 us | 0.194 us | 2.86x |
| `common/email-match` | `IsMatch` | 0.197 us | 0.076 us | 0.101 us | 2.59x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 148.803 us | 59.832 us | 253.613 us | 2.49x |
| `common/date-miss` | `IsMatch` | 0.297 us | 0.191 us | 0.227 us | 1.55x |
| `industry/mariomka-uri-count` | `Count` | 1,409.873 us | 1,047.701 us | 7,949.825 us | 1.35x |
| `common/backtracking` | `IsMatch` | 0.054 us | 0.041 us | 0.071 us | 1.31x |
| `common/replace-words` | `Replace` | 19.674 us | 16.541 us | 17.535 us | 1.19x |
| `industry/leipzig-river-window-count` | `Count` | 1,754.112 us | 1,504.337 us | 7,420.100 us | 1.17x |
| `common/match-word` | `Match` | 0.053 us | 0.046 us | 0.918 us | 1.15x |
| `industry/mariomka-email-count` | `Count` | 405.155 us | 440.939 us | 7,486.400 us | 0.92x |
| `industry/leipzig-symbol-count` | `Count` | 966.583 us | 1,156.547 us | 6,543.980 us | 0.84x |
| `common/matches-words` | `Count` | 8.644 us | 10.440 us | 13.325 us | 0.83x |
| `common/split-words` | `Split` | 12.438 us | 18.875 us | 21.039 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,111.017 us | 3,265.596 us | 10,304.033 us | 0.65x |
| `common/matches-set` | `Count` | 57.357 us | 100.301 us | 101.725 us | 0.57x |
| `common/matches-word` | `Count` | 1.172 us | 2.227 us | 3.201 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 607.705 us | 1,270.742 us | 7,258.730 us | 0.48x |
| `common/one-node-backtracking` | `IsMatch` | 0.040 us | 0.085 us | 0.121 us | 0.47x |
| `industry/leipzig-name-family-count` | `Count` | 990.141 us | 2,902.996 us | 7,757.275 us | 0.34x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.085 us | 0.109 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.335 us | 565.589 us | 799.157 us | 0.29x |
| `common/matches-boundary` | `Count` | 25.021 us | 99.977 us | 101.347 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,781.981 us | 8,323.040 us | 8,800.956 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 889.069 us | 4,485.347 us | 4,864.506 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 274.255 us | 3,744.176 us | 4,102.621 us | 0.07x |
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
| `lokad/langserv/helper-identifier` | 9.099 us | 4.461 us | 5.365 us | 2.04x |
| `lokad/lexer/doc-line` | 3.573 us | 1.752 us | 2.490 us | 2.04x |
| `lokad/langserv/identifier-validator` | 13.584 us | 7.133 us | 8.590 us | 1.90x |
| `lokad/lexer/identifier` | 23.813 us | 12.667 us | 15.933 us | 1.88x |
| `fallback/lookahead` | 10.223 us | 5.656 us | 17.629 us | 1.81x |
| `structural/method-family-generic-call` | 10.710 us | 6.319 us | 19.410 us | 1.69x |
| `lokad/docs/inline-doc-prefix` | 5.027 us | 3.198 us | 3.987 us | 1.57x |
| `structural/method-family-call` | 9.748 us | 7.758 us | 20.340 us | 1.26x |
| `lokad/imports/module-imports` | 7.570 us | 6.088 us | 28.913 us | 1.24x |
| `structural/ordered-keyword-window` | 20.432 us | 16.799 us | 29.359 us | 1.22x |
| `lokad/lexer/string` | 92.677 us | 77.512 us | 80.470 us | 1.20x |
| `lokad/style/range-ref` | 2.006 us | 1.773 us | 1.889 us | 1.13x |
| `lokad/langserv/color-short-hex` | 0.874 us | 0.776 us | 0.944 us | 1.13x |
| `lokad/style/cell-ref` | 1.857 us | 1.796 us | 1.978 us | 1.03x |
| `lokad/lexer/operator-run` | 2.228 us | 2.178 us | 2.659 us | 1.02x |
| `lokad/style/hex-color` | 0.489 us | 0.486 us | 0.558 us | 1.01x |
| `lokad/langserv/color-rgb-hex` | 1.494 us | 1.708 us | 1.943 us | 0.87x |
| `lokad/lexer/number` | 8.334 us | 10.048 us | 11.150 us | 0.83x |
| `structural/keyword-to-generic-type` | 2.907 us | 3.858 us | 17.006 us | 0.75x |
| `structural/ordered-keyword-pair` | 3.356 us | 4.982 us | 17.949 us | 0.67x |
| `structural/ordered-modifier-pair` | 9.820 us | 16.236 us | 28.623 us | 0.60x |
| `structural/ordered-identifier-window` | 14.348 us | 26.949 us | 39.418 us | 0.53x |
| `literal/identifier-token` | 2.132 us | 4.113 us | 16.469 us | 0.52x |
| `lokad/langserv/url-dashboard` | 9.958 us | 22.498 us | 23.044 us | 0.44x |
| `literal/call-token` | 1.753 us | 4.047 us | 16.462 us | 0.43x |
| `literal-family/method-token-family` | 3.629 us | 8.657 us | 20.913 us | 0.42x |
| `structural/modifier-family-to-type-window` | 102.392 us | 244.703 us | 258.065 us | 0.42x |
| `literal-family/type-token-family` | 9.292 us | 23.094 us | 35.448 us | 0.40x |
| `balancing/begin-end-blocks` | 1,283.927 us | 4,093.737 us | 4,094.668 us | 0.31x |
| `lokad/langserv/url-download` | 11.548 us | 48.699 us | 48.995 us | 0.24x |
| `literal/identifier-token-casei` | 3.290 us | 14.102 us | 26.813 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 22.444 us | 131.214 us | 145.141 us | 0.17x |
| `lokad/folding/region-marker` | 4.988 us | 34.234 us | 59.634 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 13.464 us | 208.369 us | 224.232 us | 0.06x |
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
| `lokad/docs/inline-doc-prefix` | 5.020 us | 1.388 us | 2.104 us | 3.62x |
| `lokad/langserv/helper-identifier` | 9.137 us | 2.554 us | 3.395 us | 3.58x |
| `lokad/lexer/doc-line` | 3.550 us | 1.015 us | 1.715 us | 3.50x |
| `lokad/langserv/identifier-validator` | 13.664 us | 3.968 us | 5.449 us | 3.44x |
| `lokad/lexer/string` | 92.313 us | 28.539 us | 31.590 us | 3.23x |
| `lokad/lexer/identifier` | 23.941 us | 7.487 us | 10.173 us | 3.20x |
| `balancing/begin-end-blocks` | 1,284.315 us | 440.737 us | 470.021 us | 2.91x |
| `lokad/style/range-ref` | 2.008 us | 0.702 us | 0.822 us | 2.86x |
| `fallback/lookahead` | 10.224 us | 3.980 us | 15.580 us | 2.57x |
| `lokad/lexer/operator-run` | 2.216 us | 0.872 us | 1.355 us | 2.54x |
| `structural/ordered-keyword-window` | 20.367 us | 8.138 us | 20.630 us | 2.50x |
| `structural/method-family-generic-call` | 10.691 us | 4.325 us | 16.609 us | 2.47x |
| `lokad/style/cell-ref` | 1.863 us | 0.785 us | 0.992 us | 2.37x |
| `lokad/lexer/number` | 8.330 us | 3.577 us | 4.601 us | 2.33x |
| `lokad/style/hex-color` | 0.486 us | 0.219 us | 0.295 us | 2.22x |
| `structural/modifier-family-to-type-pair` | 22.378 us | 10.150 us | 22.592 us | 2.20x |
| `structural/method-family-call` | 9.734 us | 4.769 us | 16.975 us | 2.04x |
| `structural/modifier-family-to-type-window` | 102.255 us | 53.268 us | 66.954 us | 1.92x |
| `lokad/langserv/color-short-hex` | 0.874 us | 0.458 us | 0.644 us | 1.91x |
| `structural/keyword-family-to-capitalized-identifier` | 13.543 us | 7.312 us | 19.713 us | 1.85x |
| `lokad/langserv/color-rgb-hex` | 1.509 us | 0.818 us | 1.072 us | 1.84x |
| `lokad/imports/module-imports` | 7.592 us | 4.161 us | 26.607 us | 1.82x |
| `lokad/langserv/url-dashboard` | 9.939 us | 5.988 us | 6.451 us | 1.66x |
| `structural/ordered-identifier-window` | 14.358 us | 10.195 us | 22.787 us | 1.41x |
| `structural/ordered-modifier-pair` | 9.859 us | 8.120 us | 20.448 us | 1.21x |
| `lokad/langserv/url-download` | 11.502 us | 11.523 us | 12.395 us | 1.00x |
| `structural/keyword-to-generic-type` | 2.913 us | 3.264 us | 15.851 us | 0.89x |
| `literal-family/type-token-family` | 9.316 us | 10.844 us | 23.284 us | 0.86x |
| `structural/ordered-keyword-pair` | 3.333 us | 3.881 us | 16.188 us | 0.86x |
| `literal-family/method-token-family` | 3.633 us | 5.046 us | 17.160 us | 0.72x |
| `literal/identifier-token-casei` | 3.298 us | 5.567 us | 18.163 us | 0.59x |
| `literal/identifier-token` | 2.129 us | 4.453 us | 15.905 us | 0.48x |
| `literal/call-token` | 1.740 us | 3.737 us | 15.566 us | 0.47x |
| `lokad/folding/region-marker` | 4.947 us | 33.940 us | 58.203 us | 0.15x |
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
