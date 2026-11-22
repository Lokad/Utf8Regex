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
| `literal/sherlock-zh` | 21.416 us | 18.097 us | 2,170.809 us | 1.18x |
| `literal/sherlock-ru` | 69.337 us | 95.093 us | 5,084.187 us | 0.73x |
| `literal-alternate/sherlock-zh` | 41.379 us | 66.341 us | 2,118.281 us | 0.62x |
| `ruff-noqa/tweaked` | 3,860.820 us | 7,499.564 us | 40,418.050 us | 0.51x |
| `aws-keys/full` | 41,908.600 us | 82,126.400 us | 116,351.200 us | 0.51x |
| `aws-keys/quick` | 3,624.690 us | 9,099.622 us | 44,619.750 us | 0.40x |
| `literal/sherlock-casei-ru` | 140.066 us | 353.107 us | 5,441.807 us | 0.40x |
| `bounded-repeat/letters-en` | 1,055.503 us | 2,675.179 us | 2,842.144 us | 0.39x |
| `literal/sherlock-en` | 30.638 us | 84.451 us | 439.601 us | 0.36x |
| `literal/sherlock-casei-en` | 66.668 us | 193.863 us | 556.086 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,872.241 us | 5,606.671 us | 6,490.500 us | 0.33x |
| `dictionary/single` | 7,713.560 us | 24,653.000 us | 24,506.275 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,388.100 us | 4,560.112 us | 9,613.825 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 161.947 us | 571.995 us | 1,017.351 us | 0.28x |
| `bounded-repeat/capitals` | 11,262.957 us | 43,841.050 us | 47,261.250 us | 0.26x |
| `literal-alternate/sherlock-en` | 339.111 us | 1,431.490 us | 1,914.344 us | 0.24x |
| `literal-alternate/sherlock-en-nomatch` | 122.480 us | 617.833 us | 1,011.114 us | 0.20x |
| `bounded-repeat/context` | 8,454.811 us | 142,231.200 us | 145,806.200 us | 0.06x |
| `ruff-noqa/real` | 3,387.365 us | 67,154.400 us | 101,603.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 816.319 us | 22,736.450 us | 27,658.050 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 99.481 us | 10,875.129 us | 11,694.714 us | 0.0091x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.165 us | 0.103 us | 0.131 us | 1.60x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.112 us | 0.074 us | 0.087 us | 1.52x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.114 us | 0.077 us | 0.093 us | 1.48x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.116 us | 0.080 us | 0.096 us | 1.46x |
| `common/uri-match` | `IsMatch` | 0.292 us | 0.226 us | 0.260 us | 1.29x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.131 us | 0.104 us | 0.141 us | 1.27x |
| `common/ip-match` | `IsMatch` | 0.191 us | 0.165 us | 0.183 us | 1.15x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.163 us | 0.154 us | 0.171 us | 1.06x |
| `common/uri-miss` | `IsMatch` | 0.198 us | 0.188 us | 0.209 us | 1.05x |
| `industry/mariomka-email-count` | `Count` | 407.185 us | 475.896 us | 8,502.125 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,549.080 us | 1,841.664 us | 9,795.200 us | 0.84x |
| `common/ip-miss` | `IsMatch` | 0.511 us | 0.694 us | 0.710 us | 0.74x |
| `common/email-match` | `IsMatch` | 0.190 us | 0.260 us | 0.284 us | 0.73x |
| `common/date-miss` | `IsMatch` | 0.326 us | 0.530 us | 0.563 us | 0.62x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 150.640 us | 248.659 us | 476.970 us | 0.61x |
| `industry/leipzig-twain-count` | `Count` | 609.908 us | 1,283.900 us | 8,413.011 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.125 us | 1.099 us | 0.43x |
| `common/matches-set` | `Count` | 58.333 us | 183.985 us | 184.624 us | 0.32x |
| `common/matches-word` | `Count` | 1.174 us | 4.022 us | 5.676 us | 0.29x |
| `common/replace-words` | `Replace` | 19.724 us | 72.689 us | 73.516 us | 0.27x |
| `industry/mariomka-ip-count` | `Count` | 2,105.719 us | 7,881.630 us | 15,937.933 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.165 us | 928.678 us | 1,188.181 us | 0.18x |
| `common/split-words` | `Split` | 12.567 us | 74.256 us | 75.951 us | 0.17x |
| `common/matches-boundary` | `Count` | 21.237 us | 155.285 us | 157.487 us | 0.14x |
| `common/matches-words` | `Count` | 8.736 us | 66.887 us | 68.874 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 996.172 us | 9,273.778 us | 16,141.425 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,757.374 us | 18,452.440 us | 19,184.125 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.331 us | 0.354 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 841.415 us | 11,488.829 us | 11,908.729 us | 0.07x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.801 us | 0.840 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.587 us | 0.615 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,776.419 us | 30,841.733 us | 37,629.250 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 969.782 us | 21,573.675 us | 28,372.300 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 283.740 us | 7,703.040 us | 8,168.580 us | 0.04x |
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
| `aws-keys/full` | 42,005.650 us | 17,615.260 us | 51,797.167 us | 2.38x |
| `literal-alternate/sherlock-zh` | 41.373 us | 34.605 us | 2,092.489 us | 1.20x |
| `aws-keys/quick` | 3,627.071 us | 3,549.859 us | 35,998.650 us | 1.02x |
| `literal-alternate/sherlock-en-mixed` | 162.523 us | 168.410 us | 535.118 us | 0.97x |
| `literal-alternate/sherlock-en` | 339.066 us | 351.745 us | 731.362 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,865.227 us | 1,964.174 us | 2,821.115 us | 0.95x |
| `literal/sherlock-zh` | 21.431 us | 23.106 us | 2,147.724 us | 0.93x |
| `ruff-noqa/tweaked` | 3,860.490 us | 4,326.011 us | 34,529.000 us | 0.89x |
| `bounded-repeat/capitals` | 11,309.414 us | 12,809.233 us | 15,815.100 us | 0.88x |
| `literal/sherlock-casei-en` | 66.561 us | 78.185 us | 424.394 us | 0.85x |
| `literal/sherlock-ru` | 69.000 us | 86.891 us | 5,005.567 us | 0.79x |
| `literal-alternate/sherlock-casei-en` | 99.031 us | 138.027 us | 558.624 us | 0.72x |
| `literal/sherlock-casei-ru` | 142.679 us | 215.801 us | 5,122.233 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 122.303 us | 190.969 us | 548.717 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,388.171 us | 2,254.374 us | 7,227.200 us | 0.62x |
| `bounded-repeat/letters-en` | 1,053.869 us | 1,735.195 us | 1,871.165 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 817.708 us | 1,901.510 us | 6,853.375 us | 0.43x |
| `literal/sherlock-en` | 30.384 us | 73.683 us | 405.185 us | 0.41x |
| `dictionary/single` | 7,753.740 us | 43,759.450 us | 43,634.450 us | 0.18x |
| `ruff-noqa/real` | 3,329.709 us | 19,466.950 us | 52,648.350 us | 0.17x |
| `bounded-repeat/context` | 8,429.244 us | 66,055.250 us | 69,267.000 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.190 us | 0.046 us | 0.065 us | 4.12x |
| `common/date-match` | `IsMatch` | 0.162 us | 0.041 us | 0.069 us | 3.92x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.116 us | 0.030 us | 0.048 us | 3.84x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.113 us | 0.030 us | 0.046 us | 3.76x |
| `common/uri-match` | `IsMatch` | 0.290 us | 0.084 us | 0.116 us | 3.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.111 us | 0.034 us | 0.050 us | 3.26x |
| `common/uri-miss` | `IsMatch` | 0.198 us | 0.063 us | 0.081 us | 3.16x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.162 us | 0.053 us | 0.073 us | 3.09x |
| `common/ip-miss` | `IsMatch` | 0.510 us | 0.174 us | 0.195 us | 2.92x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.132 us | 0.046 us | 0.082 us | 2.85x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 150.903 us | 58.189 us | 273.433 us | 2.59x |
| `common/email-match` | `IsMatch` | 0.189 us | 0.076 us | 0.103 us | 2.50x |
| `common/date-miss` | `IsMatch` | 0.326 us | 0.193 us | 0.225 us | 1.69x |
| `industry/mariomka-uri-count` | `Count` | 1,556.214 us | 1,060.962 us | 8,005.300 us | 1.47x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.042 us | 0.070 us | 1.34x |
| `common/replace-words` | `Replace` | 19.827 us | 16.482 us | 18.836 us | 1.20x |
| `industry/leipzig-river-window-count` | `Count` | 1,778.400 us | 1,500.288 us | 7,616.880 us | 1.19x |
| `common/match-word` | `Match` | 0.053 us | 0.046 us | 0.890 us | 1.15x |
| `industry/mariomka-email-count` | `Count` | 406.644 us | 446.075 us | 7,300.875 us | 0.91x |
| `common/matches-words` | `Count` | 8.728 us | 10.475 us | 13.514 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 970.594 us | 1,194.500 us | 6,882.900 us | 0.81x |
| `common/split-words` | `Split` | 12.933 us | 19.849 us | 21.719 us | 0.65x |
| `industry/mariomka-ip-count` | `Count` | 2,117.447 us | 3,321.204 us | 10,254.767 us | 0.64x |
| `common/matches-set` | `Count` | 58.426 us | 100.297 us | 102.128 us | 0.58x |
| `common/matches-word` | `Count` | 1.166 us | 2.237 us | 3.255 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 615.367 us | 1,271.708 us | 7,392.740 us | 0.48x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.086 us | 0.122 us | 0.45x |
| `industry/leipzig-name-family-count` | `Count` | 995.605 us | 2,964.004 us | 8,032.900 us | 0.34x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.085 us | 0.111 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 163.690 us | 570.486 us | 815.374 us | 0.29x |
| `common/matches-boundary` | `Count` | 21.273 us | 99.599 us | 101.633 us | 0.21x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,753.935 us | 8,292.750 us | 8,850.300 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 844.377 us | 4,561.329 us | 4,937.594 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 281.599 us | 3,774.290 us | 4,119.579 us | 0.07x |
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
| `lokad/langserv/identifier-validator` | 16.951 us | 7.149 us | 8.717 us | 2.37x |
| `lokad/langserv/color-short-hex` | 1.658 us | 0.794 us | 0.974 us | 2.09x |
| `lokad/lexer/doc-line` | 3.684 us | 1.792 us | 2.529 us | 2.06x |
| `lokad/lexer/identifier` | 26.938 us | 13.123 us | 15.771 us | 2.05x |
| `lokad/langserv/helper-identifier` | 9.194 us | 4.540 us | 5.358 us | 2.03x |
| `fallback/lookahead` | 10.289 us | 5.674 us | 17.943 us | 1.81x |
| `lokad/style/hex-color` | 0.891 us | 0.492 us | 0.598 us | 1.81x |
| `lokad/langserv/color-rgb-hex` | 3.053 us | 1.727 us | 2.003 us | 1.77x |
| `structural/method-family-generic-call` | 10.928 us | 6.287 us | 19.419 us | 1.74x |
| `lokad/docs/inline-doc-prefix` | 5.746 us | 3.308 us | 4.095 us | 1.74x |
| `lokad/lexer/number` | 16.413 us | 10.313 us | 11.222 us | 1.59x |
| `lokad/lexer/operator-run` | 2.866 us | 2.209 us | 2.670 us | 1.30x |
| `lokad/imports/module-imports` | 7.602 us | 6.135 us | 30.251 us | 1.24x |
| `structural/ordered-keyword-window` | 20.802 us | 17.154 us | 29.809 us | 1.21x |
| `structural/method-family-call` | 9.585 us | 7.918 us | 20.681 us | 1.21x |
| `lokad/lexer/string` | 93.267 us | 78.636 us | 82.524 us | 1.19x |
| `lokad/style/range-ref` | 2.031 us | 1.784 us | 1.902 us | 1.14x |
| `lokad/style/cell-ref` | 2.046 us | 1.851 us | 2.045 us | 1.11x |
| `structural/keyword-to-generic-type` | 2.922 us | 3.885 us | 16.985 us | 0.75x |
| `structural/ordered-keyword-pair` | 3.346 us | 5.093 us | 18.227 us | 0.66x |
| `structural/ordered-modifier-pair` | 9.944 us | 16.240 us | 28.918 us | 0.61x |
| `structural/ordered-identifier-window` | 14.771 us | 27.237 us | 40.372 us | 0.54x |
| `literal/identifier-token` | 2.150 us | 4.164 us | 16.764 us | 0.52x |
| `lokad/langserv/url-dashboard` | 10.208 us | 22.419 us | 22.825 us | 0.46x |
| `literal/call-token` | 1.754 us | 4.103 us | 16.560 us | 0.43x |
| `literal-family/method-token-family` | 3.615 us | 8.746 us | 21.468 us | 0.41x |
| `structural/modifier-family-to-type-window` | 99.994 us | 243.398 us | 258.758 us | 0.41x |
| `literal-family/type-token-family` | 9.373 us | 22.976 us | 36.253 us | 0.41x |
| `balancing/begin-end-blocks` | 1,295.797 us | 4,116.653 us | 4,116.522 us | 0.31x |
| `literal/identifier-token-casei` | 3.762 us | 14.998 us | 27.150 us | 0.25x |
| `lokad/langserv/url-download` | 11.599 us | 49.303 us | 50.270 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 22.545 us | 132.326 us | 146.023 us | 0.17x |
| `lokad/folding/region-marker` | 5.052 us | 35.773 us | 60.288 us | 0.14x |
| `structural/keyword-family-to-capitalized-identifier` | 13.369 us | 211.683 us | 227.563 us | 0.06x |
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
| `lokad/lexer/number` | 16.333 us | 3.628 us | 4.703 us | 4.50x |
| `lokad/docs/inline-doc-prefix` | 5.746 us | 1.377 us | 2.112 us | 4.17x |
| `lokad/langserv/identifier-validator` | 16.981 us | 4.101 us | 5.610 us | 4.14x |
| `lokad/style/hex-color` | 0.888 us | 0.218 us | 0.297 us | 4.07x |
| `lokad/langserv/color-rgb-hex` | 3.056 us | 0.816 us | 1.076 us | 3.75x |
| `lokad/langserv/color-short-hex` | 1.657 us | 0.458 us | 0.640 us | 3.62x |
| `lokad/lexer/doc-line` | 3.654 us | 1.017 us | 1.768 us | 3.59x |
| `lokad/lexer/identifier` | 27.240 us | 7.734 us | 10.328 us | 3.52x |
| `lokad/langserv/helper-identifier` | 9.136 us | 2.625 us | 3.488 us | 3.48x |
| `lokad/lexer/string` | 94.096 us | 29.229 us | 32.198 us | 3.22x |
| `balancing/begin-end-blocks` | 1,295.422 us | 445.018 us | 470.901 us | 2.91x |
| `lokad/style/range-ref` | 2.003 us | 0.715 us | 0.831 us | 2.80x |
| `lokad/lexer/operator-run` | 2.852 us | 1.086 us | 1.542 us | 2.63x |
| `fallback/lookahead` | 10.304 us | 3.994 us | 15.820 us | 2.58x |
| `lokad/style/cell-ref` | 2.038 us | 0.796 us | 1.001 us | 2.56x |
| `structural/ordered-keyword-window` | 20.867 us | 8.269 us | 20.784 us | 2.52x |
| `structural/method-family-generic-call` | 10.970 us | 4.375 us | 16.812 us | 2.51x |
| `structural/modifier-family-to-type-pair` | 22.432 us | 10.359 us | 22.778 us | 2.17x |
| `structural/method-family-call` | 9.604 us | 4.844 us | 17.165 us | 1.98x |
| `structural/modifier-family-to-type-window` | 100.183 us | 54.036 us | 67.511 us | 1.85x |
| `structural/keyword-family-to-capitalized-identifier` | 13.335 us | 7.350 us | 21.030 us | 1.81x |
| `lokad/imports/module-imports` | 7.653 us | 4.235 us | 27.672 us | 1.81x |
| `lokad/langserv/url-dashboard` | 10.203 us | 6.035 us | 6.425 us | 1.69x |
| `structural/ordered-identifier-window` | 14.729 us | 10.011 us | 23.238 us | 1.47x |
| `structural/ordered-modifier-pair` | 9.963 us | 8.316 us | 20.679 us | 1.20x |
| `lokad/langserv/url-download` | 11.652 us | 11.608 us | 12.055 us | 1.00x |
| `structural/keyword-to-generic-type` | 2.914 us | 3.270 us | 15.979 us | 0.89x |
| `structural/ordered-keyword-pair` | 3.357 us | 3.917 us | 16.337 us | 0.86x |
| `literal-family/type-token-family` | 9.329 us | 10.963 us | 23.395 us | 0.85x |
| `literal-family/method-token-family` | 3.623 us | 5.071 us | 17.889 us | 0.71x |
| `literal/identifier-token-casei` | 3.731 us | 5.646 us | 18.340 us | 0.66x |
| `literal/identifier-token` | 2.158 us | 4.511 us | 16.487 us | 0.48x |
| `literal/call-token` | 1.765 us | 3.767 us | 15.554 us | 0.47x |
| `lokad/folding/region-marker` | 5.053 us | 34.013 us | 60.291 us | 0.15x |
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
