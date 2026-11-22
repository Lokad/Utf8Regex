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
| `literal/sherlock-zh` | 21.129 us | 17.776 us | 2,088.963 us | 1.19x |
| `literal/sherlock-ru` | 66.856 us | 95.161 us | 5,160.671 us | 0.70x |
| `literal-alternate/sherlock-zh` | 40.706 us | 64.890 us | 2,088.003 us | 0.63x |
| `aws-keys/quick` | 5,087.920 us | 9,228.712 us | 37,704.950 us | 0.55x |
| `ruff-noqa/tweaked` | 3,757.357 us | 7,252.045 us | 35,630.450 us | 0.52x |
| `aws-keys/full` | 41,607.900 us | 81,318.500 us | 110,271.700 us | 0.51x |
| `literal/sherlock-casei-ru` | 146.898 us | 345.436 us | 5,228.370 us | 0.43x |
| `literal/sherlock-casei-en` | 76.494 us | 185.542 us | 546.496 us | 0.41x |
| `bounded-repeat/letters-en` | 1,014.118 us | 2,650.843 us | 2,802.281 us | 0.38x |
| `literal/sherlock-en` | 30.186 us | 86.010 us | 454.027 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,949.730 us | 5,559.429 us | 6,392.842 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,379.365 us | 4,630.794 us | 9,475.825 us | 0.30x |
| `dictionary/single` | 7,142.400 us | 24,242.800 us | 24,298.525 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 149.056 us | 564.260 us | 970.314 us | 0.26x |
| `bounded-repeat/capitals` | 11,211.286 us | 44,402.150 us | 47,646.250 us | 0.25x |
| `literal-alternate/sherlock-en` | 327.957 us | 1,434.592 us | 1,921.074 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 120.832 us | 622.481 us | 1,005.561 us | 0.19x |
| `bounded-repeat/context` | 8,373.433 us | 140,546.200 us | 144,777.600 us | 0.06x |
| `ruff-noqa/real` | 3,398.730 us | 66,244.150 us | 100,840.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 819.691 us | 21,933.825 us | 26,996.350 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 96.164 us | 10,699.087 us | 11,454.729 us | 0.009x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.166 us | 0.100 us | 0.130 us | 1.65x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.118 us | 0.072 us | 0.086 us | 1.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.114 us | 0.079 us | 0.095 us | 1.44x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.112 us | 0.078 us | 0.093 us | 1.44x |
| `common/uri-match` | `IsMatch` | 0.277 us | 0.219 us | 0.254 us | 1.26x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.131 us | 0.105 us | 0.140 us | 1.25x |
| `common/ip-match` | `IsMatch` | 0.190 us | 0.168 us | 0.187 us | 1.13x |
| `common/uri-miss` | `IsMatch` | 0.196 us | 0.191 us | 0.210 us | 1.02x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.157 us | 0.160 us | 0.174 us | 0.98x |
| `industry/mariomka-uri-count` | `Count` | 1,544.817 us | 1,822.834 us | 9,797.000 us | 0.85x |
| `industry/mariomka-email-count` | `Count` | 409.924 us | 485.144 us | 8,369.730 us | 0.84x |
| `common/email-match` | `IsMatch` | 0.194 us | 0.255 us | 0.278 us | 0.76x |
| `common/ip-miss` | `IsMatch` | 0.492 us | 0.695 us | 0.715 us | 0.71x |
| `common/date-miss` | `IsMatch` | 0.346 us | 0.533 us | 0.571 us | 0.65x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 153.537 us | 242.347 us | 462.841 us | 0.63x |
| `industry/leipzig-twain-count` | `Count` | 631.840 us | 1,322.333 us | 8,244.689 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.123 us | 1.086 us | 0.43x |
| `common/matches-set` | `Count` | 57.603 us | 188.550 us | 189.402 us | 0.31x |
| `common/matches-word` | `Count` | 1.161 us | 3.978 us | 5.394 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,115.633 us | 7,829.670 us | 16,000.633 us | 0.27x |
| `common/replace-words` | `Replace` | 19.650 us | 75.625 us | 75.992 us | 0.26x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 158.566 us | 914.496 us | 1,171.685 us | 0.17x |
| `common/split-words` | `Split` | 12.472 us | 74.125 us | 75.649 us | 0.17x |
| `common/matches-boundary` | `Count` | 22.538 us | 154.298 us | 155.787 us | 0.15x |
| `common/matches-words` | `Count` | 8.609 us | 67.597 us | 68.310 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,008.417 us | 9,511.062 us | 16,330.850 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,784.788 us | 18,497.680 us | 19,000.725 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.327 us | 0.353 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 844.549 us | 11,105.414 us | 11,635.314 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.808 us | 0.848 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.584 us | 0.612 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,739.745 us | 30,542.733 us | 37,608.650 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 961.425 us | 21,355.300 us | 28,438.150 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 277.474 us | 7,883.550 us | 8,387.989 us | 0.04x |
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
| `aws-keys/full` | 41,619.650 us | 17,395.920 us | 47,572.633 us | 2.39x |
| `aws-keys/quick` | 5,079.473 us | 3,530.086 us | 33,688.633 us | 1.44x |
| `literal-alternate/sherlock-zh` | 40.975 us | 34.001 us | 2,031.022 us | 1.21x |
| `literal/sherlock-casei-en` | 76.582 us | 74.880 us | 409.833 us | 1.02x |
| `literal-alternate/sherlock-en` | 328.192 us | 326.163 us | 719.061 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,938.833 us | 1,951.697 us | 2,759.779 us | 0.99x |
| `literal/sherlock-zh` | 21.198 us | 22.683 us | 2,138.623 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 148.774 us | 166.898 us | 514.691 us | 0.89x |
| `bounded-repeat/capitals` | 11,201.129 us | 12,874.133 us | 15,663.975 us | 0.87x |
| `ruff-noqa/tweaked` | 3,749.290 us | 4,338.528 us | 34,527.100 us | 0.86x |
| `literal/sherlock-ru` | 66.739 us | 87.137 us | 5,143.180 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 96.105 us | 136.515 us | 548.058 us | 0.70x |
| `literal/sherlock-casei-ru` | 147.773 us | 213.673 us | 5,091.313 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 120.801 us | 192.702 us | 541.707 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,380.720 us | 2,223.485 us | 7,089.550 us | 0.62x |
| `bounded-repeat/letters-en` | 1,012.944 us | 1,741.127 us | 1,863.778 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 822.073 us | 1,854.433 us | 6,870.100 us | 0.44x |
| `literal/sherlock-en` | 30.342 us | 75.031 us | 406.633 us | 0.40x |
| `ruff-noqa/real` | 3,379.152 us | 19,400.660 us | 52,685.750 us | 0.17x |
| `dictionary/single` | 7,130.418 us | 43,246.300 us | 43,458.050 us | 0.16x |
| `bounded-repeat/context` | 8,349.222 us | 65,761.550 us | 68,295.900 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.113 us | 0.027 us | 0.045 us | 4.17x |
| `common/ip-match` | `IsMatch` | 0.188 us | 0.046 us | 0.065 us | 4.13x |
| `common/date-match` | `IsMatch` | 0.162 us | 0.040 us | 0.068 us | 4.04x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.113 us | 0.029 us | 0.047 us | 3.92x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.118 us | 0.034 us | 0.051 us | 3.51x |
| `common/uri-match` | `IsMatch` | 0.278 us | 0.086 us | 0.114 us | 3.25x |
| `common/uri-miss` | `IsMatch` | 0.194 us | 0.062 us | 0.083 us | 3.15x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.158 us | 0.053 us | 0.072 us | 2.98x |
| `common/ip-miss` | `IsMatch` | 0.492 us | 0.170 us | 0.193 us | 2.89x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.131 us | 0.047 us | 0.083 us | 2.80x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 152.550 us | 54.943 us | 266.691 us | 2.78x |
| `common/email-match` | `IsMatch` | 0.189 us | 0.076 us | 0.101 us | 2.49x |
| `common/date-miss` | `IsMatch` | 0.346 us | 0.190 us | 0.224 us | 1.82x |
| `industry/mariomka-uri-count` | `Count` | 1,546.139 us | 1,072.152 us | 8,002.900 us | 1.44x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.041 us | 0.071 us | 1.36x |
| `common/replace-words` | `Replace` | 19.559 us | 16.543 us | 17.300 us | 1.18x |
| `industry/leipzig-river-window-count` | `Count` | 1,760.805 us | 1,506.639 us | 7,458.575 us | 1.17x |
| `common/match-word` | `Match` | 0.053 us | 0.045 us | 0.886 us | 1.17x |
| `industry/mariomka-email-count` | `Count` | 409.101 us | 453.662 us | 7,466.310 us | 0.90x |
| `common/matches-words` | `Count` | 8.667 us | 10.410 us | 13.453 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 961.338 us | 1,190.802 us | 6,737.975 us | 0.81x |
| `common/split-words` | `Split` | 12.462 us | 19.557 us | 21.339 us | 0.64x |
| `industry/mariomka-ip-count` | `Count` | 2,116.217 us | 3,329.100 us | 10,348.033 us | 0.64x |
| `common/matches-set` | `Count` | 57.569 us | 99.144 us | 100.842 us | 0.58x |
| `common/matches-word` | `Count` | 1.159 us | 2.191 us | 3.108 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 632.824 us | 1,280.115 us | 7,233.040 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.087 us | 0.121 us | 0.44x |
| `industry/leipzig-name-family-count` | `Count` | 1,013.171 us | 2,941.452 us | 8,390.400 us | 0.34x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.084 us | 0.110 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.542 us | 565.995 us | 830.500 us | 0.28x |
| `common/matches-boundary` | `Count` | 22.599 us | 98.653 us | 100.537 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,779.876 us | 8,357.900 us | 8,866.911 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 849.052 us | 4,522.682 us | 4,950.019 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 277.229 us | 3,771.995 us | 4,135.232 us | 0.07x |
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
| `lokad/langserv/identifier-validator` | 15.174 us | 7.061 us | 8.582 us | 2.15x |
| `lokad/langserv/helper-identifier` | 9.299 us | 4.503 us | 5.362 us | 2.06x |
| `lokad/langserv/color-short-hex` | 1.603 us | 0.787 us | 0.966 us | 2.04x |
| `lokad/lexer/doc-line` | 3.476 us | 1.779 us | 2.483 us | 1.95x |
| `lokad/style/hex-color` | 0.890 us | 0.474 us | 0.568 us | 1.88x |
| `lokad/lexer/identifier` | 23.968 us | 13.130 us | 15.798 us | 1.83x |
| `fallback/lookahead` | 10.144 us | 5.692 us | 17.755 us | 1.78x |
| `structural/method-family-generic-call` | 10.823 us | 6.181 us | 18.995 us | 1.75x |
| `lokad/langserv/color-rgb-hex` | 3.046 us | 1.793 us | 1.975 us | 1.70x |
| `lokad/docs/inline-doc-prefix` | 5.416 us | 3.351 us | 4.108 us | 1.62x |
| `structural/method-family-call` | 10.227 us | 7.648 us | 20.589 us | 1.34x |
| `lokad/lexer/number` | 13.624 us | 10.235 us | 11.272 us | 1.33x |
| `lokad/lexer/operator-run` | 2.868 us | 2.223 us | 2.662 us | 1.29x |
| `lokad/imports/module-imports` | 7.553 us | 6.064 us | 29.743 us | 1.25x |
| `structural/ordered-keyword-window` | 20.397 us | 16.840 us | 30.087 us | 1.21x |
| `lokad/lexer/string` | 91.309 us | 78.012 us | 81.799 us | 1.17x |
| `lokad/style/range-ref` | 1.971 us | 1.762 us | 1.863 us | 1.12x |
| `lokad/style/cell-ref` | 1.999 us | 1.799 us | 2.004 us | 1.11x |
| `structural/keyword-to-generic-type` | 2.903 us | 3.869 us | 16.876 us | 0.75x |
| `structural/ordered-keyword-pair` | 3.382 us | 5.009 us | 18.057 us | 0.68x |
| `structural/ordered-modifier-pair` | 9.823 us | 16.225 us | 28.739 us | 0.61x |
| `structural/ordered-identifier-window` | 14.606 us | 26.969 us | 40.471 us | 0.54x |
| `literal/identifier-token` | 2.146 us | 4.085 us | 16.951 us | 0.53x |
| `lokad/langserv/url-dashboard` | 9.840 us | 22.186 us | 22.482 us | 0.44x |
| `literal/call-token` | 1.767 us | 4.054 us | 16.513 us | 0.44x |
| `structural/modifier-family-to-type-window` | 102.537 us | 241.212 us | 254.484 us | 0.43x |
| `literal-family/method-token-family` | 3.609 us | 8.683 us | 20.885 us | 0.42x |
| `literal-family/type-token-family` | 9.335 us | 23.073 us | 36.507 us | 0.40x |
| `balancing/begin-end-blocks` | 1,285.102 us | 4,101.900 us | 4,098.884 us | 0.31x |
| `literal/identifier-token-casei` | 3.732 us | 14.907 us | 27.041 us | 0.25x |
| `lokad/langserv/url-download` | 11.489 us | 48.172 us | 49.218 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 22.599 us | 128.113 us | 141.270 us | 0.18x |
| `lokad/folding/region-marker` | 5.035 us | 33.374 us | 59.996 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 14.890 us | 210.414 us | 220.921 us | 0.07x |
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
| `lokad/style/hex-color` | 0.888 us | 0.213 us | 0.295 us | 4.16x |
| `lokad/docs/inline-doc-prefix` | 5.419 us | 1.317 us | 2.048 us | 4.11x |
| `lokad/lexer/number` | 13.602 us | 3.590 us | 4.639 us | 3.79x |
| `lokad/langserv/color-rgb-hex` | 3.041 us | 0.807 us | 1.040 us | 3.77x |
| `lokad/langserv/identifier-validator` | 15.199 us | 4.078 us | 5.486 us | 3.73x |
| `lokad/langserv/helper-identifier` | 9.313 us | 2.563 us | 3.465 us | 3.63x |
| `lokad/langserv/color-short-hex` | 1.611 us | 0.450 us | 0.628 us | 3.58x |
| `lokad/lexer/doc-line` | 3.493 us | 1.016 us | 1.687 us | 3.44x |
| `lokad/lexer/identifier` | 24.056 us | 7.337 us | 10.153 us | 3.28x |
| `lokad/lexer/operator-run` | 2.869 us | 0.901 us | 1.333 us | 3.19x |
| `lokad/lexer/string` | 91.521 us | 28.760 us | 31.805 us | 3.18x |
| `balancing/begin-end-blocks` | 1,283.049 us | 439.977 us | 466.096 us | 2.92x |
| `lokad/style/range-ref` | 1.984 us | 0.691 us | 0.807 us | 2.87x |
| `fallback/lookahead` | 10.202 us | 3.960 us | 15.669 us | 2.58x |
| `lokad/style/cell-ref` | 2.007 us | 0.779 us | 0.976 us | 2.58x |
| `structural/ordered-keyword-window` | 20.287 us | 8.132 us | 21.182 us | 2.49x |
| `structural/method-family-generic-call` | 10.666 us | 4.387 us | 16.568 us | 2.43x |
| `structural/modifier-family-to-type-pair` | 22.698 us | 9.917 us | 23.026 us | 2.29x |
| `structural/method-family-call` | 9.879 us | 4.838 us | 17.281 us | 2.04x |
| `structural/keyword-family-to-capitalized-identifier` | 14.857 us | 7.317 us | 20.377 us | 2.03x |
| `structural/modifier-family-to-type-window` | 102.382 us | 52.874 us | 65.987 us | 1.94x |
| `lokad/imports/module-imports` | 7.544 us | 4.185 us | 27.276 us | 1.80x |
| `lokad/langserv/url-dashboard` | 9.821 us | 6.002 us | 6.418 us | 1.64x |
| `structural/ordered-identifier-window` | 14.687 us | 10.061 us | 23.332 us | 1.46x |
| `structural/ordered-modifier-pair` | 9.832 us | 8.205 us | 20.629 us | 1.20x |
| `lokad/langserv/url-download` | 11.516 us | 11.744 us | 12.151 us | 0.98x |
| `structural/keyword-to-generic-type` | 2.903 us | 3.253 us | 15.869 us | 0.89x |
| `structural/ordered-keyword-pair` | 3.381 us | 3.913 us | 16.269 us | 0.86x |
| `literal-family/type-token-family` | 9.295 us | 10.861 us | 23.385 us | 0.86x |
| `literal-family/method-token-family` | 3.616 us | 5.016 us | 16.938 us | 0.72x |
| `literal/identifier-token-casei` | 3.723 us | 5.644 us | 18.280 us | 0.66x |
| `literal/identifier-token` | 2.140 us | 4.062 us | 16.748 us | 0.53x |
| `literal/call-token` | 1.763 us | 3.770 us | 15.588 us | 0.47x |
| `lokad/folding/region-marker` | 4.968 us | 33.936 us | 58.841 us | 0.15x |
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
