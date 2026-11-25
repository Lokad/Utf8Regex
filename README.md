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
| `literal/sherlock-zh` | 21.771 us | 18.452 us | 2,157.391 us | 1.18x |
| `literal/sherlock-ru` | 69.476 us | 96.319 us | 5,262.436 us | 0.72x |
| `literal-alternate/sherlock-zh` | 42.967 us | 67.220 us | 2,140.891 us | 0.64x |
| `ruff-noqa/tweaked` | 3,928.500 us | 7,472.018 us | 43,767.200 us | 0.53x |
| `literal/sherlock-casei-en` | 78.229 us | 186.784 us | 578.986 us | 0.42x |
| `literal/sherlock-casei-ru` | 145.879 us | 353.902 us | 5,551.157 us | 0.41x |
| `bounded-repeat/letters-en` | 1,094.987 us | 2,739.307 us | 2,899.138 us | 0.40x |
| `literal/sherlock-en` | 31.348 us | 85.740 us | 446.904 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,045.503 us | 5,792.985 us | 6,598.258 us | 0.35x |
| `dictionary/single` | 8,028.480 us | 25,317.233 us | 25,429.267 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,422.239 us | 4,797.356 us | 9,876.300 us | 0.30x |
| `bounded-repeat/capitals` | 11,586.229 us | 44,343.050 us | 47,775.950 us | 0.26x |
| `literal-alternate/sherlock-en-mixed` | 152.513 us | 606.541 us | 1,055.674 us | 0.25x |
| `literal-alternate/sherlock-en` | 337.106 us | 1,497.256 us | 2,053.754 us | 0.23x |
| `aws-keys/quick` | 2,006.492 us | 9,533.862 us | 43,001.350 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 126.664 us | 653.373 us | 1,095.854 us | 0.19x |
| `aws-keys/full` | 8,587.350 us | 84,160.500 us | 114,553.400 us | 0.10x |
| `bounded-repeat/context` | 9,237.578 us | 145,233.700 us | 151,221.100 us | 0.06x |
| `ruff-noqa/real` | 3,493.687 us | 72,285.400 us | 99,895.300 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 865.955 us | 22,891.075 us | 28,139.450 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 99.210 us | 11,516.786 us | 12,162.300 us | 0.0086x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 380.359 us | 498.174 us | 8,761.000 us | 0.76x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.107 us | 0.140 us | 0.71x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.075 us | 0.088 us | 0.68x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.081 us | 0.099 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.067 us | 0.106 us | 0.134 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,155.191 us | 1,858.929 us | 10,037.925 us | 0.62x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.043 us | 0.080 us | 0.096 us | 0.54x |
| `industry/leipzig-twain-count` | `Count` | 652.393 us | 1,354.537 us | 8,651.500 us | 0.48x |
| `common/email-match` | `IsMatch` | 0.121 us | 0.261 us | 0.288 us | 0.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.072 us | 0.165 us | 0.184 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.072 us | 0.174 us | 0.194 us | 0.41x |
| `common/match-word` | `Match` | 0.049 us | 0.124 us | 1.116 us | 0.40x |
| `industry/mariomka-ip-count` | `Count` | 3,160.879 us | 8,106.940 us | 16,758.533 us | 0.39x |
| `common/uri-miss` | `IsMatch` | 0.078 us | 0.199 us | 0.215 us | 0.39x |
| `common/matches-set` | `Count` | 66.709 us | 194.080 us | 197.320 us | 0.34x |
| `common/uri-match` | `IsMatch` | 0.074 us | 0.226 us | 0.264 us | 0.33x |
| `common/ip-miss` | `IsMatch` | 0.224 us | 0.730 us | 0.746 us | 0.31x |
| `common/matches-word` | `Count` | 1.219 us | 4.142 us | 5.887 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 71.186 us | 257.690 us | 523.391 us | 0.28x |
| `common/replace-words` | `Replace` | 19.913 us | 75.422 us | 76.284 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.555 us | 0.591 us | 0.20x |
| `common/matches-boundary` | `Count` | 29.531 us | 161.324 us | 162.159 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 163.745 us | 943.090 us | 1,210.877 us | 0.17x |
| `common/split-words` | `Split` | 12.745 us | 75.589 us | 77.560 us | 0.17x |
| `common/matches-words` | `Count` | 8.937 us | 69.084 us | 70.495 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,065.783 us | 9,425.467 us | 16,333.175 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,845.980 us | 19,017.400 us | 19,630.775 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 853.706 us | 11,939.614 us | 12,316.586 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,637.946 us | 31,202.133 us | 38,153.400 us | 0.05x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.595 us | 0.628 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.809 us | 0.843 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 973.242 us | 22,370.700 us | 29,995.650 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.344 us | 0.366 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 271.534 us | 8,436.467 us | 8,875.111 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 42.845 us | 35.449 us | 2,111.286 us | 1.21x |
| `bounded-repeat/letters-ru` | 2,067.932 us | 2,019.268 us | 2,826.173 us | 1.02x |
| `literal/sherlock-casei-en` | 78.864 us | 77.558 us | 432.465 us | 1.02x |
| `literal-alternate/sherlock-en` | 338.585 us | 357.845 us | 762.307 us | 0.95x |
| `literal/sherlock-zh` | 21.722 us | 23.554 us | 2,149.069 us | 0.92x |
| `ruff-noqa/tweaked` | 3,986.820 us | 4,497.559 us | 38,568.650 us | 0.89x |
| `literal-alternate/sherlock-en-mixed` | 153.374 us | 174.679 us | 594.937 us | 0.88x |
| `bounded-repeat/capitals` | 11,500.614 us | 13,184.233 us | 15,967.250 us | 0.87x |
| `literal/sherlock-ru` | 68.628 us | 86.674 us | 5,242.050 us | 0.79x |
| `literal-alternate/sherlock-casei-en` | 99.652 us | 144.044 us | 559.294 us | 0.69x |
| `literal/sherlock-casei-ru` | 145.261 us | 223.530 us | 5,307.860 us | 0.65x |
| `literal-alternate/sherlock-en-nomatch` | 126.418 us | 196.427 us | 585.297 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,423.943 us | 2,307.945 us | 7,319.500 us | 0.62x |
| `bounded-repeat/letters-en` | 1,090.620 us | 1,785.819 us | 1,931.221 us | 0.61x |
| `aws-keys/quick` | 2,015.132 us | 3,714.838 us | 35,082.900 us | 0.54x |
| `aws-keys/full` | 8,621.744 us | 17,912.440 us | 52,810.733 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 853.200 us | 1,940.405 us | 7,168.750 us | 0.44x |
| `literal/sherlock-en` | 31.511 us | 74.543 us | 428.540 us | 0.42x |
| `dictionary/single` | 8,060.620 us | 44,390.550 us | 44,848.450 us | 0.18x |
| `ruff-noqa/real` | 3,461.173 us | 20,645.850 us | 54,526.267 us | 0.17x |
| `bounded-repeat/context` | 9,125.744 us | 67,460.300 us | 72,083.850 us | 0.14x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.068 us | 0.042 us | 0.070 us | 1.63x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.075 us | 0.047 us | 0.084 us | 1.58x |
| `common/email-match` | `IsMatch` | 0.121 us | 0.078 us | 0.104 us | 1.55x |
| `common/ip-match` | `IsMatch` | 0.068 us | 0.047 us | 0.066 us | 1.46x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.043 us | 0.031 us | 0.050 us | 1.38x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.070 us | 0.054 us | 0.075 us | 1.28x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.043 us | 0.034 us | 0.052 us | 1.26x |
| `common/ip-miss` | `IsMatch` | 0.220 us | 0.176 us | 0.199 us | 1.25x |
| `common/uri-miss` | `IsMatch` | 0.078 us | 0.063 us | 0.084 us | 1.24x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.036 us | 0.030 us | 0.047 us | 1.23x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 70.818 us | 59.195 us | 262.244 us | 1.20x |
| `common/replace-words` | `Replace` | 19.876 us | 16.738 us | 18.232 us | 1.19x |
| `industry/leipzig-river-window-count` | `Count` | 1,652.036 us | 1,534.644 us | 7,860.900 us | 1.08x |
| `industry/mariomka-uri-count` | `Count` | 1,163.703 us | 1,096.162 us | 8,167.875 us | 1.06x |
| `common/match-word` | `Match` | 0.049 us | 0.047 us | 0.912 us | 1.04x |
| `industry/mariomka-ip-count` | `Count` | 3,166.033 us | 3,382.183 us | 10,696.267 us | 0.94x |
| `common/uri-match` | `IsMatch` | 0.074 us | 0.086 us | 0.118 us | 0.86x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.042 us | 0.072 us | 0.85x |
| `common/matches-words` | `Count` | 8.904 us | 10.572 us | 13.705 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 378.884 us | 463.386 us | 7,454.275 us | 0.82x |
| `industry/leipzig-symbol-count` | `Count` | 982.481 us | 1,210.489 us | 7,405.380 us | 0.81x |
| `common/matches-set` | `Count` | 66.797 us | 104.590 us | 106.783 us | 0.64x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.197 us | 0.236 us | 0.55x |
| `common/matches-word` | `Count` | 1.214 us | 2.309 us | 3.446 us | 0.53x |
| `common/split-words` | `Split` | 12.786 us | 24.922 us | 27.013 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 655.195 us | 1,324.205 us | 7,452.300 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,082.837 us | 2,982.781 us | 8,457.783 us | 0.36x |
| `common/matches-boundary` | `Count` | 29.833 us | 103.691 us | 105.121 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 166.351 us | 583.312 us | 838.006 us | 0.29x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.104 us | 0.125 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,854.381 us | 8,847.289 us | 9,299.656 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 861.947 us | 4,735.681 us | 5,167.093 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.087 us | 0.113 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 272.387 us | 3,930.560 us | 4,227.344 us | 0.07x |
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
| `lokad/imports/module-imports` | 5.975 us | 6.220 us | 30.566 us | 0.96x |
| `fallback/lookahead` | 5.461 us | 5.881 us | 18.738 us | 0.93x |
| `structural/keyword-to-generic-type` | 2.953 us | 4.005 us | 17.354 us | 0.74x |
| `structural/ordered-keyword-window` | 12.126 us | 17.467 us | 30.233 us | 0.69x |
| `structural/method-family-call` | 5.178 us | 7.900 us | 21.112 us | 0.66x |
| `structural/method-family-generic-call` | 4.141 us | 6.369 us | 19.415 us | 0.65x |
| `lokad/langserv/color-short-hex` | 0.507 us | 0.796 us | 0.974 us | 0.64x |
| `structural/ordered-keyword-pair` | 3.158 us | 5.272 us | 18.736 us | 0.60x |
| `lokad/langserv/identifier-validator` | 4.258 us | 7.281 us | 8.789 us | 0.58x |
| `structural/ordered-modifier-pair` | 9.959 us | 17.071 us | 29.647 us | 0.58x |
| `lokad/lexer/identifier` | 7.805 us | 13.463 us | 16.332 us | 0.58x |
| `literal/identifier-token` | 2.297 us | 4.066 us | 16.973 us | 0.56x |
| `lokad/lexer/doc-line` | 1.007 us | 1.837 us | 2.537 us | 0.55x |
| `lokad/docs/inline-doc-prefix` | 1.719 us | 3.415 us | 4.159 us | 0.50x |
| `lokad/langserv/helper-identifier` | 2.270 us | 4.603 us | 5.525 us | 0.49x |
| `lokad/lexer/operator-run` | 1.083 us | 2.293 us | 2.701 us | 0.47x |
| `literal/call-token` | 1.802 us | 4.152 us | 16.886 us | 0.43x |
| `lokad/style/hex-color` | 0.212 us | 0.505 us | 0.582 us | 0.42x |
| `lokad/style/range-ref` | 0.729 us | 1.753 us | 1.906 us | 0.42x |
| `lokad/lexer/string` | 34.097 us | 82.063 us | 87.249 us | 0.42x |
| `lokad/langserv/url-dashboard` | 9.299 us | 22.643 us | 23.113 us | 0.41x |
| `lokad/style/cell-ref` | 0.781 us | 1.929 us | 2.046 us | 0.41x |
| `literal-family/type-token-family` | 8.995 us | 23.023 us | 36.493 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.650 us | 1.759 us | 1.995 us | 0.37x |
| `structural/ordered-identifier-window` | 8.539 us | 28.358 us | 41.895 us | 0.30x |
| `literal-family/method-token-family` | 2.572 us | 9.281 us | 21.749 us | 0.28x |
| `lokad/lexer/number` | 2.885 us | 10.538 us | 11.520 us | 0.27x |
| `structural/modifier-family-to-type-window` | 57.755 us | 251.117 us | 263.571 us | 0.23x |
| `lokad/langserv/url-download` | 11.326 us | 49.799 us | 50.385 us | 0.23x |
| `literal/identifier-token-casei` | 3.368 us | 14.962 us | 27.528 us | 0.23x |
| `lokad/folding/region-marker` | 5.674 us | 35.059 us | 61.267 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.679 us | 211.118 us | 226.557 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.802 us | 133.506 us | 146.616 us | 0.02x |
| `balancing/begin-end-blocks` | 45.417 us | 4,280.976 us | 4,295.183 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.419 us | 6.228 us | 6.756 us | 1.51x |
| `structural/ordered-keyword-window` | 12.075 us | 8.357 us | 21.101 us | 1.44x |
| `lokad/imports/module-imports` | 5.970 us | 4.327 us | 28.080 us | 1.38x |
| `fallback/lookahead` | 5.494 us | 4.094 us | 16.639 us | 1.34x |
| `structural/keyword-family-to-capitalized-identifier` | 9.655 us | 7.740 us | 20.829 us | 1.25x |
| `lokad/docs/inline-doc-prefix` | 1.716 us | 1.405 us | 2.138 us | 1.22x |
| `structural/ordered-modifier-pair` | 10.007 us | 8.287 us | 20.907 us | 1.21x |
| `lokad/lexer/operator-run` | 1.074 us | 0.894 us | 1.402 us | 1.20x |
| `lokad/lexer/string` | 34.607 us | 30.013 us | 32.705 us | 1.15x |
| `structural/modifier-family-to-type-window` | 57.713 us | 55.340 us | 68.684 us | 1.04x |
| `structural/method-family-call` | 5.162 us | 4.953 us | 17.580 us | 1.04x |
| `lokad/style/range-ref` | 0.729 us | 0.705 us | 0.842 us | 1.03x |
| `lokad/langserv/identifier-validator` | 4.230 us | 4.203 us | 5.634 us | 1.01x |
| `lokad/lexer/identifier` | 7.915 us | 8.013 us | 10.495 us | 0.99x |
| `lokad/langserv/color-short-hex` | 0.455 us | 0.469 us | 0.662 us | 0.97x |
| `lokad/lexer/doc-line` | 0.997 us | 1.035 us | 1.770 us | 0.96x |
| `lokad/style/cell-ref` | 0.782 us | 0.814 us | 1.024 us | 0.96x |
| `lokad/langserv/url-download` | 11.195 us | 11.902 us | 12.491 us | 0.94x |
| `structural/method-family-generic-call` | 4.123 us | 4.423 us | 16.755 us | 0.93x |
| `lokad/langserv/helper-identifier` | 2.273 us | 2.625 us | 3.434 us | 0.87x |
| `structural/keyword-to-generic-type` | 2.965 us | 3.427 us | 16.331 us | 0.86x |
| `lokad/style/hex-color` | 0.192 us | 0.224 us | 0.308 us | 0.86x |
| `literal-family/type-token-family` | 9.020 us | 11.199 us | 24.166 us | 0.81x |
| `structural/ordered-identifier-window` | 8.461 us | 10.505 us | 23.775 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.140 us | 3.967 us | 16.703 us | 0.79x |
| `lokad/lexer/number` | 2.849 us | 3.783 us | 4.834 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 0.583 us | 0.813 us | 1.094 us | 0.72x |
| `literal/identifier-token-casei` | 3.379 us | 5.725 us | 18.804 us | 0.59x |
| `literal-family/method-token-family` | 2.568 us | 4.954 us | 17.555 us | 0.52x |
| `literal/identifier-token` | 2.275 us | 4.577 us | 16.724 us | 0.50x |
| `literal/call-token` | 1.799 us | 3.867 us | 16.023 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.827 us | 10.289 us | 22.853 us | 0.27x |
| `lokad/folding/region-marker` | 5.684 us | 34.576 us | 61.629 us | 0.16x |
| `balancing/begin-end-blocks` | 45.853 us | 458.798 us | 489.026 us | 0.10x |
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
