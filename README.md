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
| `literal/sherlock-zh` | 13.602 us | 18.363 us | 2,153.653 us | 0.74x |
| `literal/sherlock-ru` | 66.766 us | 98.870 us | 5,220.550 us | 0.68x |
| `literal-alternate/sherlock-zh` | 42.864 us | 67.532 us | 2,186.233 us | 0.63x |
| `ruff-noqa/tweaked` | 3,946.611 us | 7,565.436 us | 41,233.650 us | 0.52x |
| `literal/sherlock-casei-en` | 77.798 us | 182.067 us | 676.819 us | 0.43x |
| `literal/sherlock-casei-ru` | 151.130 us | 355.764 us | 5,590.093 us | 0.42x |
| `bounded-repeat/letters-en` | 1,095.268 us | 2,745.961 us | 2,935.054 us | 0.40x |
| `literal/sherlock-en` | 30.750 us | 84.008 us | 471.012 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,002.036 us | 5,794.207 us | 6,617.642 us | 0.35x |
| `dictionary/single` | 8,235.320 us | 25,398.667 us | 25,281.567 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,407.765 us | 4,790.756 us | 9,966.250 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 171.337 us | 600.903 us | 1,058.922 us | 0.29x |
| `bounded-repeat/capitals` | 11,456.957 us | 44,122.900 us | 47,940.550 us | 0.26x |
| `literal-alternate/sherlock-en` | 358.719 us | 1,470.167 us | 2,001.085 us | 0.24x |
| `aws-keys/quick` | 1,923.030 us | 9,487.625 us | 39,691.800 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 126.202 us | 644.515 us | 1,131.445 us | 0.20x |
| `aws-keys/full` | 8,282.544 us | 85,267.900 us | 120,724.700 us | 0.10x |
| `bounded-repeat/context` | 8,923.522 us | 145,194.700 us | 150,174.300 us | 0.06x |
| `ruff-noqa/real` | 3,432.755 us | 69,642.700 us | 101,214.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 833.942 us | 22,398.875 us | 27,535.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 100.803 us | 10,914.900 us | 11,693.114 us | 0.0092x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.083 us | 0.109 us | 0.144 us | 0.77x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.106 us | 0.135 us | 0.66x |
| `common/ip-match` | `IsMatch` | 0.113 us | 0.173 us | 0.194 us | 0.65x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.049 us | 0.075 us | 0.089 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.083 us | 0.099 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,182.758 us | 1,848.143 us | 10,323.575 us | 0.64x |
| `industry/mariomka-email-count` | `Count` | 268.710 us | 488.219 us | 8,794.675 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.140 us | 0.259 us | 0.281 us | 0.54x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.081 us | 0.096 us | 0.54x |
| `common/uri-miss` | `IsMatch` | 0.096 us | 0.184 us | 0.202 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 609.706 us | 1,293.231 us | 8,324.256 us | 0.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.168 us | 0.179 us | 0.46x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.228 us | 0.266 us | 0.41x |
| `common/match-word` | `Match` | 0.051 us | 0.126 us | 1.166 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.279 us | 0.722 us | 0.744 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,077.784 us | 8,506.644 us | 16,660.800 us | 0.36x |
| `common/matches-set` | `Count` | 61.352 us | 192.081 us | 191.568 us | 0.32x |
| `common/matches-word` | `Count` | 1.219 us | 4.169 us | 5.942 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 71.671 us | 262.051 us | 523.314 us | 0.27x |
| `common/replace-words` | `Replace` | 20.188 us | 77.894 us | 80.237 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.107 us | 0.549 us | 0.589 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.496 us | 949.841 us | 1,243.077 us | 0.18x |
| `common/matches-boundary` | `Count` | 27.481 us | 166.792 us | 168.869 us | 0.16x |
| `common/split-words` | `Split` | 13.080 us | 79.884 us | 81.302 us | 0.16x |
| `common/matches-words` | `Count` | 9.043 us | 68.742 us | 70.454 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,052.293 us | 9,676.212 us | 17,003.667 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,854.220 us | 19,223.525 us | 19,877.175 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.601 us | 0.636 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.019 us | 0.343 us | 0.365 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,642.877 us | 31,317.767 us | 38,838.350 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,022.185 us | 23,907.600 us | 31,294.850 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.033 us | 0.818 us | 0.864 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 241.126 us | 8,309.533 us | 8,695.200 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 284.203 us | 11,948.929 us | 12,455.017 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 42.449 us | 35.297 us | 2,127.978 us | 1.20x |
| `literal/sherlock-casei-en` | 77.845 us | 76.545 us | 445.810 us | 1.02x |
| `literal-alternate/sherlock-en-mixed` | 171.505 us | 171.600 us | 583.304 us | 1.00x |
| `bounded-repeat/letters-ru` | 2,012.384 us | 2,040.924 us | 2,868.181 us | 0.99x |
| `literal-alternate/sherlock-en` | 356.594 us | 363.616 us | 800.344 us | 0.98x |
| `ruff-noqa/tweaked` | 3,957.542 us | 4,486.035 us | 35,036.950 us | 0.88x |
| `bounded-repeat/capitals` | 11,487.914 us | 13,272.833 us | 16,326.400 us | 0.87x |
| `literal/sherlock-casei-ru` | 150.800 us | 191.080 us | 5,343.347 us | 0.79x |
| `literal-alternate/sherlock-casei-en` | 101.523 us | 134.807 us | 716.800 us | 0.75x |
| `literal/sherlock-ru` | 67.151 us | 91.153 us | 5,071.157 us | 0.74x |
| `literal-alternate/sherlock-en-nomatch` | 125.694 us | 198.536 us | 704.573 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,423.550 us | 2,316.515 us | 7,369.200 us | 0.61x |
| `bounded-repeat/letters-en` | 1,092.706 us | 1,801.243 us | 1,940.941 us | 0.61x |
| `literal/sherlock-zh` | 13.596 us | 23.394 us | 2,136.015 us | 0.58x |
| `aws-keys/quick` | 1,919.369 us | 3,592.477 us | 35,270.600 us | 0.53x |
| `aws-keys/full` | 8,494.000 us | 18,120.780 us | 54,134.967 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 834.489 us | 1,911.353 us | 6,960.625 us | 0.44x |
| `literal/sherlock-en` | 31.142 us | 73.300 us | 428.190 us | 0.42x |
| `dictionary/single` | 8,163.110 us | 44,999.950 us | 45,081.900 us | 0.18x |
| `ruff-noqa/real` | 3,527.095 us | 20,330.475 us | 54,269.167 us | 0.17x |
| `bounded-repeat/context` | 8,943.156 us | 67,816.750 us | 70,673.150 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.109 us | 0.047 us | 0.067 us | 2.34x |
| `common/date-match` | `IsMatch` | 0.070 us | 0.041 us | 0.071 us | 1.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.032 us | 0.050 us | 1.56x |
| `common/ip-miss` | `IsMatch` | 0.268 us | 0.175 us | 0.201 us | 1.53x |
| `common/uri-miss` | `IsMatch` | 0.095 us | 0.065 us | 0.086 us | 1.47x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.055 us | 0.074 us | 1.39x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.033 us | 0.050 us | 1.34x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.030 us | 0.046 us | 1.30x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 71.898 us | 56.888 us | 291.366 us | 1.26x |
| `common/replace-words` | `Replace` | 20.294 us | 17.701 us | 20.361 us | 1.15x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.048 us | 0.085 us | 1.11x |
| `industry/leipzig-river-window-count` | `Count` | 1,653.215 us | 1,550.334 us | 7,716.025 us | 1.07x |
| `industry/mariomka-uri-count` | `Count` | 1,184.322 us | 1,110.826 us | 8,260.800 us | 1.07x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.089 us | 0.120 us | 1.05x |
| `common/match-word` | `Match` | 0.051 us | 0.048 us | 0.949 us | 1.05x |
| `common/email-match` | `IsMatch` | 0.073 us | 0.078 us | 0.105 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,059.384 us | 3,410.087 us | 10,553.067 us | 0.90x |
| `industry/leipzig-symbol-count` | `Count` | 1,040.601 us | 1,246.961 us | 7,272.175 us | 0.83x |
| `common/matches-words` | `Count` | 9.032 us | 11.027 us | 14.157 us | 0.82x |
| `common/backtracking` | `IsMatch` | 0.031 us | 0.042 us | 0.074 us | 0.74x |
| `common/split-words` | `Split` | 13.103 us | 20.582 us | 22.243 us | 0.64x |
| `common/matches-set` | `Count` | 61.628 us | 103.769 us | 105.320 us | 0.59x |
| `industry/mariomka-email-count` | `Count` | 269.426 us | 455.814 us | 7,551.500 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.106 us | 0.195 us | 0.230 us | 0.54x |
| `common/matches-word` | `Count` | 1.217 us | 2.314 us | 3.410 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 622.186 us | 1,258.092 us | 7,237.530 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,064.028 us | 3,015.285 us | 8,005.200 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.105 us | 0.126 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.683 us | 585.356 us | 863.397 us | 0.29x |
| `common/matches-boundary` | `Count` | 27.600 us | 102.740 us | 104.213 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,856.195 us | 8,680.889 us | 9,739.587 us | 0.21x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.088 us | 0.114 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 241.421 us | 3,936.880 us | 4,245.989 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 284.662 us | 4,669.453 us | 5,119.020 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.067 us | 6.343 us | 32.153 us | 0.96x |
| `fallback/lookahead` | 5.080 us | 5.822 us | 19.764 us | 0.87x |
| `lokad/langserv/color-short-hex` | 0.558 us | 0.802 us | 1.047 us | 0.70x |
| `structural/method-family-generic-call` | 4.425 us | 6.501 us | 21.162 us | 0.68x |
| `structural/ordered-keyword-window` | 11.586 us | 17.534 us | 31.554 us | 0.66x |
| `structural/method-family-call` | 5.225 us | 7.952 us | 22.031 us | 0.66x |
| `lokad/lexer/identifier` | 8.795 us | 13.699 us | 16.446 us | 0.64x |
| `lokad/langserv/identifier-validator` | 4.656 us | 7.272 us | 8.893 us | 0.64x |
| `lokad/lexer/doc-line` | 1.125 us | 1.845 us | 2.564 us | 0.61x |
| `structural/ordered-modifier-pair` | 10.288 us | 17.206 us | 31.644 us | 0.60x |
| `lokad/docs/inline-doc-prefix` | 1.959 us | 3.424 us | 4.205 us | 0.57x |
| `structural/keyword-to-generic-type` | 2.248 us | 4.005 us | 18.967 us | 0.56x |
| `literal/identifier-token` | 2.235 us | 4.285 us | 18.485 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.436 us | 4.686 us | 5.562 us | 0.52x |
| `lokad/style/hex-color` | 0.264 us | 0.513 us | 0.593 us | 0.51x |
| `lokad/lexer/operator-run` | 1.174 us | 2.303 us | 2.755 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.487 us | 5.294 us | 19.904 us | 0.47x |
| `lokad/style/cell-ref` | 0.842 us | 1.868 us | 2.074 us | 0.45x |
| `lokad/style/range-ref` | 0.791 us | 1.795 us | 1.952 us | 0.44x |
| `literal/call-token` | 1.809 us | 4.242 us | 18.456 us | 0.43x |
| `lokad/langserv/color-rgb-hex` | 0.723 us | 1.759 us | 2.027 us | 0.41x |
| `literal-family/type-token-family` | 9.050 us | 22.983 us | 38.374 us | 0.39x |
| `lokad/lexer/string` | 32.031 us | 84.004 us | 88.499 us | 0.38x |
| `lokad/langserv/url-dashboard` | 8.589 us | 23.013 us | 23.465 us | 0.37x |
| `lokad/lexer/number` | 3.202 us | 10.681 us | 11.721 us | 0.30x |
| `structural/ordered-identifier-window` | 8.203 us | 28.400 us | 43.316 us | 0.29x |
| `literal-family/method-token-family` | 2.588 us | 9.248 us | 23.109 us | 0.28x |
| `literal/identifier-token-casei` | 3.847 us | 15.162 us | 29.179 us | 0.25x |
| `structural/modifier-family-to-type-window` | 60.041 us | 250.556 us | 265.417 us | 0.24x |
| `lokad/langserv/url-download` | 10.976 us | 50.604 us | 50.894 us | 0.22x |
| `lokad/folding/region-marker` | 5.749 us | 35.269 us | 62.752 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.788 us | 213.557 us | 229.245 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.918 us | 129.601 us | 145.275 us | 0.02x |
| `balancing/begin-end-blocks` | 46.193 us | 4,385.217 us | 4,363.467 us | 0.01x |
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
| `lokad/docs/inline-doc-prefix` | 1.955 us | 1.407 us | 2.149 us | 1.39x |
| `structural/ordered-keyword-window` | 11.633 us | 8.461 us | 22.194 us | 1.37x |
| `lokad/imports/module-imports` | 6.039 us | 4.396 us | 29.334 us | 1.37x |
| `lokad/langserv/url-dashboard` | 8.612 us | 6.725 us | 7.290 us | 1.28x |
| `structural/keyword-family-to-capitalized-identifier` | 9.832 us | 7.804 us | 22.248 us | 1.26x |
| `fallback/lookahead` | 5.144 us | 4.105 us | 17.582 us | 1.25x |
| `lokad/lexer/operator-run` | 1.149 us | 0.937 us | 1.391 us | 1.23x |
| `structural/ordered-modifier-pair` | 10.264 us | 8.564 us | 22.461 us | 1.20x |
| `lokad/lexer/identifier` | 8.799 us | 7.632 us | 10.697 us | 1.15x |
| `lokad/langserv/identifier-validator` | 4.664 us | 4.177 us | 5.643 us | 1.12x |
| `structural/modifier-family-to-type-window` | 60.045 us | 55.224 us | 70.114 us | 1.09x |
| `lokad/langserv/color-short-hex` | 0.500 us | 0.462 us | 0.672 us | 1.08x |
| `lokad/lexer/doc-line` | 1.131 us | 1.050 us | 1.807 us | 1.08x |
| `lokad/style/range-ref` | 0.784 us | 0.731 us | 0.858 us | 1.07x |
| `lokad/lexer/string` | 32.142 us | 30.024 us | 32.843 us | 1.07x |
| `structural/method-family-call` | 5.210 us | 4.924 us | 18.562 us | 1.06x |
| `lokad/style/cell-ref` | 0.841 us | 0.820 us | 1.016 us | 1.03x |
| `structural/method-family-generic-call` | 4.439 us | 4.547 us | 18.239 us | 0.98x |
| `lokad/style/hex-color` | 0.213 us | 0.227 us | 0.305 us | 0.94x |
| `lokad/langserv/helper-identifier` | 2.433 us | 2.637 us | 3.546 us | 0.92x |
| `lokad/langserv/url-download` | 11.056 us | 12.293 us | 12.811 us | 0.90x |
| `lokad/lexer/number` | 3.166 us | 3.796 us | 4.842 us | 0.83x |
| `literal-family/type-token-family` | 9.076 us | 11.149 us | 25.549 us | 0.81x |
| `structural/ordered-identifier-window` | 8.221 us | 10.448 us | 25.054 us | 0.79x |
| `lokad/langserv/color-rgb-hex` | 0.637 us | 0.871 us | 1.093 us | 0.73x |
| `literal/identifier-token-casei` | 3.824 us | 5.365 us | 20.326 us | 0.71x |
| `structural/keyword-to-generic-type` | 2.255 us | 3.422 us | 17.724 us | 0.66x |
| `structural/ordered-keyword-pair` | 2.490 us | 4.025 us | 17.793 us | 0.62x |
| `literal-family/method-token-family` | 2.600 us | 5.008 us | 19.129 us | 0.52x |
| `literal/identifier-token` | 2.230 us | 4.652 us | 17.984 us | 0.48x |
| `literal/call-token` | 1.812 us | 3.892 us | 17.385 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.907 us | 10.460 us | 24.835 us | 0.28x |
| `lokad/folding/region-marker` | 5.754 us | 34.586 us | 63.202 us | 0.17x |
| `balancing/begin-end-blocks` | 46.399 us | 454.010 us | 485.214 us | 0.10x |
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
