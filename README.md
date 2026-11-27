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
| `literal/sherlock-zh` | 14.196 us | 19.230 us | 2,214.388 us | 0.74x |
| `literal/sherlock-ru` | 68.177 us | 97.954 us | 5,431.357 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.620 us | 69.094 us | 2,203.715 us | 0.63x |
| `ruff-noqa/tweaked` | 4,035.753 us | 7,876.009 us | 41,828.100 us | 0.51x |
| `literal/sherlock-casei-en` | 79.701 us | 189.104 us | 617.355 us | 0.42x |
| `literal/sherlock-casei-ru` | 148.302 us | 365.792 us | 5,668.220 us | 0.41x |
| `bounded-repeat/letters-en` | 1,108.794 us | 2,854.444 us | 3,031.632 us | 0.39x |
| `literal/sherlock-en` | 31.814 us | 87.199 us | 493.394 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,003.124 us | 5,901.408 us | 6,780.483 us | 0.34x |
| `dictionary/single` | 8,256.310 us | 25,894.533 us | 25,979.767 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,429.845 us | 4,800.062 us | 9,990.450 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 177.106 us | 597.115 us | 1,055.350 us | 0.30x |
| `bounded-repeat/capitals` | 11,839.586 us | 46,749.900 us | 50,282.700 us | 0.25x |
| `literal-alternate/sherlock-en` | 347.554 us | 1,529.761 us | 2,080.017 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 128.536 us | 660.369 us | 1,246.719 us | 0.19x |
| `aws-keys/quick` | 1,848.663 us | 9,812.850 us | 43,771.700 us | 0.19x |
| `aws-keys/full` | 8,590.950 us | 85,811.700 us | 120,066.700 us | 0.10x |
| `bounded-repeat/context` | 9,115.933 us | 148,610.100 us | 152,988.600 us | 0.06x |
| `ruff-noqa/real` | 3,495.476 us | 70,331.150 us | 86,875.400 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 859.459 us | 22,927.700 us | 28,230.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 105.847 us | 11,255.957 us | 12,044.186 us | 0.0094x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.087 us | 0.110 us | 0.147 us | 0.79x |
| `common/ip-match` | `IsMatch` | 0.122 us | 0.182 us | 0.202 us | 0.67x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.076 us | 0.091 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.072 us | 0.110 us | 0.140 us | 0.66x |
| `industry/mariomka-uri-count` | `Count` | 1,214.486 us | 1,943.175 us | 10,519.933 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.084 us | 0.101 us | 0.62x |
| `industry/mariomka-email-count` | `Count` | 276.662 us | 502.087 us | 9,038.500 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.146 us | 0.270 us | 0.297 us | 0.54x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.082 us | 0.098 us | 0.54x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.083 us | 0.170 us | 0.190 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 632.051 us | 1,332.678 us | 8,619.260 us | 0.47x |
| `common/uri-miss` | `IsMatch` | 0.082 us | 0.189 us | 0.208 us | 0.43x |
| `common/match-word` | `Match` | 0.051 us | 0.128 us | 1.169 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.283 us | 0.713 us | 0.742 us | 0.40x |
| `industry/mariomka-ip-count` | `Count` | 3,121.928 us | 8,593.800 us | 26,742.700 us | 0.36x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 88.929 us | 262.709 us | 526.404 us | 0.34x |
| `common/uri-match` | `IsMatch` | 0.082 us | 0.243 us | 0.274 us | 0.34x |
| `common/matches-set` | `Count` | 63.169 us | 194.671 us | 195.953 us | 0.32x |
| `common/matches-word` | `Count` | 1.265 us | 4.226 us | 6.010 us | 0.30x |
| `common/replace-words` | `Replace` | 20.385 us | 78.460 us | 79.687 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.566 us | 0.609 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.044 us | 959.908 us | 1,274.584 us | 0.18x |
| `common/split-words` | `Split` | 13.187 us | 77.729 us | 79.513 us | 0.17x |
| `common/matches-boundary` | `Count` | 26.158 us | 162.994 us | 165.629 us | 0.16x |
| `common/matches-words` | `Count` | 9.197 us | 69.833 us | 71.513 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,019.127 us | 9,887.237 us | 17,181.333 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,940.522 us | 19,594.250 us | 20,152.500 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.348 us | 0.375 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.033 us | 0.629 us | 0.659 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,494.077 us | 32,069.200 us | 39,805.550 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,009.300 us | 24,276.125 us | 31,352.650 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.030 us | 0.839 us | 0.877 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 257.153 us | 8,171.600 us | 8,676.700 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 294.169 us | 11,861.229 us | 12,405.400 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.697 us | 36.425 us | 2,154.942 us | 1.20x |
| `literal/sherlock-casei-en` | 79.916 us | 78.855 us | 463.701 us | 1.01x |
| `literal-alternate/sherlock-en` | 347.660 us | 345.591 us | 841.655 us | 1.01x |
| `literal-alternate/sherlock-en-mixed` | 174.488 us | 175.172 us | 648.180 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,999.732 us | 2,071.722 us | 2,925.672 us | 0.97x |
| `bounded-repeat/capitals` | 11,903.986 us | 13,437.167 us | 16,480.575 us | 0.89x |
| `ruff-noqa/tweaked` | 4,038.679 us | 4,587.076 us | 36,814.850 us | 0.88x |
| `literal-alternate/sherlock-casei-en` | 106.635 us | 137.587 us | 686.551 us | 0.78x |
| `literal/sherlock-casei-ru` | 148.816 us | 194.699 us | 5,397.220 us | 0.76x |
| `literal/sherlock-ru` | 68.433 us | 89.584 us | 5,382.493 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 128.741 us | 205.085 us | 635.978 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,420.583 us | 2,344.115 us | 7,527.583 us | 0.61x |
| `bounded-repeat/letters-en` | 1,110.551 us | 1,833.171 us | 1,993.058 us | 0.61x |
| `literal/sherlock-zh` | 14.205 us | 24.031 us | 2,200.391 us | 0.59x |
| `aws-keys/quick` | 1,856.966 us | 3,600.371 us | 35,389.050 us | 0.52x |
| `aws-keys/full` | 8,606.337 us | 18,728.520 us | 54,048.567 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 853.721 us | 1,982.264 us | 7,103.075 us | 0.43x |
| `literal/sherlock-en` | 31.693 us | 77.146 us | 427.363 us | 0.41x |
| `dictionary/single` | 8,235.840 us | 45,963.600 us | 45,926.750 us | 0.18x |
| `ruff-noqa/real` | 3,496.095 us | 21,082.500 us | 35,592.633 us | 0.17x |
| `bounded-repeat/context` | 9,111.911 us | 68,901.350 us | 72,296.950 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.117 us | 0.048 us | 0.068 us | 2.45x |
| `common/date-match` | `IsMatch` | 0.072 us | 0.043 us | 0.072 us | 1.66x |
| `common/ip-miss` | `IsMatch` | 0.275 us | 0.179 us | 0.202 us | 1.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 89.382 us | 58.794 us | 280.887 us | 1.52x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.033 us | 0.052 us | 1.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.055 us | 0.077 us | 1.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.035 us | 0.053 us | 1.31x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.033 us | 0.049 us | 1.23x |
| `common/uri-miss` | `IsMatch` | 0.080 us | 0.067 us | 0.087 us | 1.19x |
| `common/replace-words` | `Replace` | 20.612 us | 17.872 us | 19.365 us | 1.15x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.049 us | 0.088 us | 1.09x |
| `industry/mariomka-uri-count` | `Count` | 1,206.513 us | 1,126.226 us | 8,538.933 us | 1.07x |
| `common/match-word` | `Match` | 0.051 us | 0.051 us | 0.945 us | 1.01x |
| `common/email-match` | `IsMatch` | 0.076 us | 0.081 us | 0.110 us | 0.93x |
| `industry/leipzig-river-window-count` | `Count` | 1,495.857 us | 1,609.312 us | 8,346.825 us | 0.93x |
| `common/uri-match` | `IsMatch` | 0.082 us | 0.090 us | 0.121 us | 0.92x |
| `industry/mariomka-ip-count` | `Count` | 3,123.717 us | 3,447.836 us | 14,643.567 us | 0.91x |
| `common/matches-words` | `Count` | 9.175 us | 10.982 us | 13.980 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,016.196 us | 1,260.050 us | 7,558.025 us | 0.81x |
| `common/backtracking` | `IsMatch` | 0.029 us | 0.045 us | 0.074 us | 0.65x |
| `common/split-words` | `Split` | 13.146 us | 21.175 us | 22.674 us | 0.62x |
| `common/matches-set` | `Count` | 63.257 us | 106.363 us | 107.834 us | 0.59x |
| `industry/mariomka-email-count` | `Count` | 275.230 us | 468.604 us | 7,807.125 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.197 us | 0.236 us | 0.56x |
| `common/matches-word` | `Count` | 1.260 us | 2.362 us | 3.599 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 650.319 us | 1,293.869 us | 7,635.482 us | 0.50x |
| `common/one-node-backtracking` | `IsMatch` | 0.034 us | 0.088 us | 0.129 us | 0.39x |
| `industry/leipzig-name-family-count` | `Count` | 1,024.591 us | 3,070.816 us | 8,452.250 us | 0.33x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.373 us | 590.383 us | 858.586 us | 0.28x |
| `common/matches-boundary` | `Count` | 26.053 us | 104.779 us | 106.289 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,938.785 us | 8,769.500 us | 9,547.975 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.090 us | 0.118 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 263.274 us | 3,978.037 us | 4,365.622 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 293.399 us | 4,792.194 us | 5,214.121 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.019 us | 6.474 us | 32.483 us | 0.93x |
| `fallback/lookahead` | 5.331 us | 6.053 us | 20.232 us | 0.88x |
| `lokad/langserv/color-short-hex` | 0.571 us | 0.825 us | 1.027 us | 0.69x |
| `structural/method-family-generic-call` | 4.513 us | 6.565 us | 21.343 us | 0.69x |
| `structural/ordered-keyword-window` | 11.856 us | 17.883 us | 32.131 us | 0.66x |
| `lokad/lexer/identifier` | 8.873 us | 13.454 us | 16.717 us | 0.66x |
| `structural/method-family-call` | 5.396 us | 8.369 us | 22.705 us | 0.64x |
| `lokad/langserv/identifier-validator` | 4.719 us | 7.477 us | 9.066 us | 0.63x |
| `lokad/lexer/doc-line` | 1.147 us | 1.913 us | 2.652 us | 0.60x |
| `structural/ordered-modifier-pair` | 10.368 us | 17.512 us | 31.726 us | 0.59x |
| `lokad/docs/inline-doc-prefix` | 2.014 us | 3.451 us | 4.255 us | 0.58x |
| `structural/keyword-to-generic-type` | 2.286 us | 4.091 us | 18.981 us | 0.56x |
| `lokad/langserv/helper-identifier` | 2.476 us | 4.739 us | 5.655 us | 0.52x |
| `literal/identifier-token` | 2.261 us | 4.373 us | 18.408 us | 0.52x |
| `lokad/lexer/operator-run` | 1.182 us | 2.322 us | 2.776 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.541 us | 5.299 us | 20.079 us | 0.48x |
| `lokad/style/hex-color` | 0.238 us | 0.515 us | 0.600 us | 0.46x |
| `lokad/style/cell-ref` | 0.854 us | 1.952 us | 2.176 us | 0.44x |
| `literal/call-token` | 1.867 us | 4.290 us | 18.547 us | 0.44x |
| `lokad/style/range-ref` | 0.782 us | 1.844 us | 2.042 us | 0.42x |
| `lokad/langserv/color-rgb-hex` | 0.737 us | 1.803 us | 2.034 us | 0.41x |
| `lokad/lexer/string` | 33.139 us | 81.570 us | 87.045 us | 0.41x |
| `literal-family/type-token-family` | 9.261 us | 23.793 us | 38.963 us | 0.39x |
| `lokad/langserv/url-dashboard` | 8.783 us | 23.608 us | 24.011 us | 0.37x |
| `lokad/lexer/number` | 3.283 us | 10.925 us | 12.003 us | 0.30x |
| `structural/ordered-identifier-window` | 8.517 us | 28.606 us | 43.269 us | 0.30x |
| `literal-family/method-token-family` | 2.642 us | 9.373 us | 23.637 us | 0.28x |
| `literal/identifier-token-casei` | 3.922 us | 14.971 us | 29.682 us | 0.26x |
| `structural/modifier-family-to-type-window` | 64.316 us | 256.257 us | 273.196 us | 0.25x |
| `lokad/langserv/url-download` | 11.184 us | 50.865 us | 51.513 us | 0.22x |
| `lokad/folding/region-marker` | 5.753 us | 36.322 us | 64.063 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.160 us | 219.367 us | 233.897 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.942 us | 133.808 us | 148.062 us | 0.02x |
| `balancing/begin-end-blocks` | 47.265 us | 4,351.033 us | 4,337.144 us | 0.01x |
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
| `structural/ordered-keyword-window` | 11.859 us | 8.500 us | 22.502 us | 1.40x |
| `lokad/langserv/url-dashboard` | 8.747 us | 6.337 us | 6.904 us | 1.38x |
| `lokad/docs/inline-doc-prefix` | 2.018 us | 1.472 us | 2.226 us | 1.37x |
| `lokad/imports/module-imports` | 6.012 us | 4.395 us | 29.707 us | 1.37x |
| `structural/keyword-family-to-capitalized-identifier` | 10.185 us | 7.690 us | 22.850 us | 1.32x |
| `lokad/lexer/operator-run` | 1.163 us | 0.917 us | 1.412 us | 1.27x |
| `fallback/lookahead` | 5.329 us | 4.207 us | 17.861 us | 1.27x |
| `structural/ordered-modifier-pair` | 10.358 us | 8.557 us | 22.590 us | 1.21x |
| `structural/modifier-family-to-type-window` | 64.560 us | 55.911 us | 74.023 us | 1.15x |
| `lokad/lexer/identifier` | 8.954 us | 8.019 us | 10.765 us | 1.12x |
| `lokad/langserv/identifier-validator` | 4.767 us | 4.294 us | 5.786 us | 1.11x |
| `lokad/lexer/string` | 33.117 us | 30.610 us | 33.434 us | 1.08x |
| `structural/method-family-call` | 5.428 us | 5.089 us | 19.016 us | 1.07x |
| `lokad/style/range-ref` | 0.785 us | 0.740 us | 0.870 us | 1.06x |
| `lokad/lexer/doc-line` | 1.142 us | 1.090 us | 1.902 us | 1.05x |
| `lokad/langserv/color-short-hex` | 0.511 us | 0.490 us | 0.700 us | 1.04x |
| `lokad/style/cell-ref` | 0.860 us | 0.830 us | 1.041 us | 1.04x |
| `structural/method-family-generic-call` | 4.528 us | 4.596 us | 18.320 us | 0.99x |
| `lokad/style/hex-color` | 0.218 us | 0.231 us | 0.313 us | 0.95x |
| `lokad/langserv/url-download` | 11.220 us | 12.190 us | 12.730 us | 0.92x |
| `lokad/langserv/helper-identifier` | 2.453 us | 2.713 us | 3.584 us | 0.90x |
| `lokad/lexer/number` | 3.190 us | 3.842 us | 4.961 us | 0.83x |
| `literal-family/type-token-family` | 9.299 us | 11.514 us | 26.072 us | 0.81x |
| `structural/ordered-identifier-window` | 8.243 us | 10.570 us | 25.031 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 0.650 us | 0.848 us | 1.104 us | 0.77x |
| `literal/identifier-token-casei` | 3.936 us | 5.945 us | 20.320 us | 0.66x |
| `structural/keyword-to-generic-type` | 2.294 us | 3.528 us | 17.716 us | 0.65x |
| `structural/ordered-keyword-pair` | 2.551 us | 4.151 us | 18.093 us | 0.61x |
| `literal-family/method-token-family` | 2.653 us | 5.305 us | 19.256 us | 0.50x |
| `literal/identifier-token` | 2.258 us | 4.714 us | 17.813 us | 0.48x |
| `literal/call-token` | 1.863 us | 3.984 us | 17.455 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.937 us | 10.722 us | 25.376 us | 0.27x |
| `lokad/folding/region-marker` | 5.783 us | 36.143 us | 63.337 us | 0.16x |
| `balancing/begin-end-blocks` | 47.464 us | 467.057 us | 497.927 us | 0.10x |
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
