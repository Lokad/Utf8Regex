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
Frozen `common/match-word` and Lokad prefix-loop results through dev.117
include .NET's `Match` object allocation. New runs compare the first
whole-match range on both sides.

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
| `literal/sherlock-zh` | 21.586 us | 18.104 us | 2,130.037 us | 1.19x |
| `literal/sherlock-ru` | 65.927 us | 94.136 us | 5,165.671 us | 0.70x |
| `literal-alternate/sherlock-zh` | 42.009 us | 66.361 us | 2,143.571 us | 0.63x |
| `aws-keys/full` | 43,145.950 us | 82,953.000 us | 113,121.700 us | 0.52x |
| `ruff-noqa/tweaked` | 3,806.125 us | 7,402.700 us | 45,187.450 us | 0.51x |
| `literal/sherlock-casei-en` | 78.610 us | 183.949 us | 624.386 us | 0.43x |
| `literal/sherlock-casei-ru` | 143.458 us | 354.802 us | 5,452.250 us | 0.40x |
| `aws-keys/quick` | 3,599.552 us | 9,279.150 us | 38,690.100 us | 0.39x |
| `bounded-repeat/letters-en` | 1,045.084 us | 2,718.039 us | 2,887.381 us | 0.38x |
| `literal/sherlock-en` | 30.600 us | 84.114 us | 464.711 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,844.693 us | 5,673.614 us | 6,439.133 us | 0.33x |
| `dictionary/single` | 7,758.490 us | 25,064.133 us | 25,107.533 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,412.755 us | 4,649.606 us | 9,692.550 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 153.134 us | 581.445 us | 985.833 us | 0.26x |
| `bounded-repeat/capitals` | 10,923.914 us | 43,881.700 us | 46,847.850 us | 0.25x |
| `literal-alternate/sherlock-en` | 332.817 us | 1,458.925 us | 2,041.221 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 124.190 us | 626.935 us | 1,022.484 us | 0.20x |
| `bounded-repeat/context` | 10,182.850 us | 141,258.700 us | 145,110.000 us | 0.07x |
| `ruff-noqa/real` | 3,391.940 us | 66,920.850 us | 95,849.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 819.844 us | 22,509.900 us | 27,656.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 97.832 us | 11,309.214 us | 12,111.600 us | 0.0087x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.112 us | 0.074 us | 0.088 us | 1.51x |
| `common/date-match` | `IsMatch` | 0.161 us | 0.106 us | 0.135 us | 1.51x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.106 us | 0.078 us | 0.094 us | 1.37x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.111 us | 0.081 us | 0.097 us | 1.36x |
| `common/uri-match` | `IsMatch` | 0.293 us | 0.225 us | 0.260 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.321 us | 0.263 us | 0.289 us | 1.22x |
| `common/ip-match` | `IsMatch` | 0.199 us | 0.172 us | 0.194 us | 1.16x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.106 us | 0.105 us | 0.141 us | 1.01x |
| `common/uri-miss` | `IsMatch` | 0.195 us | 0.197 us | 0.219 us | 0.99x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.160 us | 0.163 us | 0.178 us | 0.98x |
| `industry/mariomka-email-count` | `Count` | 419.140 us | 487.032 us | 8,719.425 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,575.775 us | 1,869.540 us | 10,065.600 us | 0.84x |
| `common/ip-miss` | `IsMatch` | 0.509 us | 0.691 us | 0.714 us | 0.74x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 157.189 us | 252.275 us | 494.483 us | 0.62x |
| `common/date-miss` | `IsMatch` | 0.329 us | 0.554 us | 0.592 us | 0.59x |
| `industry/leipzig-twain-count` | `Count` | 639.517 us | 1,339.090 us | 8,487.467 us | 0.48x |
| `common/match-word` | `Match` | 0.058 us | 0.163 us | 1.095 us | 0.35x |
| `common/matches-set` | `Count` | 59.150 us | 188.825 us | 191.297 us | 0.31x |
| `common/matches-word` | `Count` | 1.197 us | 4.127 us | 5.820 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,180.258 us | 7,988.790 us | 16,293.700 us | 0.27x |
| `common/replace-words` | `Replace` | 20.211 us | 74.964 us | 75.865 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.811 us | 926.898 us | 1,191.588 us | 0.18x |
| `common/split-words` | `Split` | 12.644 us | 75.920 us | 77.925 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.511 us | 159.093 us | 160.541 us | 0.15x |
| `common/matches-words` | `Count` | 8.914 us | 69.253 us | 71.068 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,034.185 us | 9,564.612 us | 16,781.767 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,845.948 us | 18,929.650 us | 19,614.675 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.051 us | 0.601 us | 0.635 us | 0.09x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.335 us | 0.362 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 900.167 us | 11,602.529 us | 12,109.657 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.804 us | 0.841 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,851.915 us | 31,521.767 us | 38,356.450 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 1,003.061 us | 22,905.225 us | 29,033.700 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 279.113 us | 8,285.660 us | 8,645.000 us | 0.03x |
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
| `aws-keys/full` | 42,543.300 us | 17,832.940 us | 48,642.900 us | 2.39x |
| `literal-alternate/sherlock-zh` | 42.276 us | 34.580 us | 2,097.129 us | 1.22x |
| `aws-keys/quick` | 3,655.971 us | 3,512.995 us | 34,324.333 us | 1.04x |
| `literal/sherlock-casei-en` | 78.464 us | 78.861 us | 422.169 us | 0.99x |
| `literal-alternate/sherlock-en` | 330.730 us | 351.989 us | 726.143 us | 0.94x |
| `literal/sherlock-zh` | 21.568 us | 23.158 us | 2,095.514 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 154.039 us | 171.215 us | 534.226 us | 0.90x |
| `bounded-repeat/letters-ru` | 1,840.929 us | 2,074.433 us | 2,870.819 us | 0.89x |
| `ruff-noqa/tweaked` | 3,853.215 us | 4,458.567 us | 38,738.300 us | 0.86x |
| `bounded-repeat/capitals` | 10,828.971 us | 13,037.417 us | 15,861.150 us | 0.83x |
| `literal/sherlock-ru` | 66.200 us | 86.186 us | 5,155.420 us | 0.77x |
| `literal/sherlock-casei-ru` | 144.426 us | 219.605 us | 5,224.400 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 123.838 us | 197.007 us | 602.118 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,422.579 us | 2,286.936 us | 7,324.083 us | 0.62x |
| `bounded-repeat/letters-en` | 1,040.582 us | 1,796.112 us | 1,909.931 us | 0.58x |
| `literal-alternate/sherlock-casei-en` | 99.870 us | 174.408 us | 601.594 us | 0.57x |
| `literal-alternate/sherlock-casei-ru` | 823.889 us | 1,924.013 us | 6,900.625 us | 0.43x |
| `literal/sherlock-en` | 30.698 us | 72.878 us | 429.048 us | 0.42x |
| `dictionary/single` | 7,782.290 us | 44,123.000 us | 44,027.150 us | 0.18x |
| `ruff-noqa/real` | 3,388.083 us | 20,166.450 us | 50,641.133 us | 0.17x |
| `bounded-repeat/context` | 10,361.212 us | 66,431.550 us | 69,366.000 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.199 us | 0.047 us | 0.067 us | 4.25x |
| `common/email-match` | `IsMatch` | 0.321 us | 0.078 us | 0.105 us | 4.10x |
| `common/date-match` | `IsMatch` | 0.159 us | 0.042 us | 0.071 us | 3.78x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.112 us | 0.030 us | 0.049 us | 3.74x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.108 us | 0.030 us | 0.048 us | 3.66x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.113 us | 0.034 us | 0.051 us | 3.35x |
| `common/uri-match` | `IsMatch` | 0.293 us | 0.088 us | 0.120 us | 3.32x |
| `common/uri-miss` | `IsMatch` | 0.196 us | 0.064 us | 0.085 us | 3.06x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.162 us | 0.054 us | 0.074 us | 3.03x |
| `common/ip-miss` | `IsMatch` | 0.515 us | 0.174 us | 0.200 us | 2.95x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 157.555 us | 58.135 us | 257.477 us | 2.71x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.108 us | 0.048 us | 0.082 us | 2.25x |
| `common/date-miss` | `IsMatch` | 0.328 us | 0.197 us | 0.230 us | 1.67x |
| `industry/mariomka-uri-count` | `Count` | 1,578.610 us | 1,068.674 us | 8,029.175 us | 1.48x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.043 us | 0.071 us | 1.31x |
| `industry/leipzig-river-window-count` | `Count` | 1,848.544 us | 1,498.427 us | 7,594.625 us | 1.23x |
| `common/replace-words` | `Replace` | 20.296 us | 16.864 us | 17.785 us | 1.20x |
| `industry/mariomka-email-count` | `Count` | 418.367 us | 453.573 us | 7,435.575 us | 0.92x |
| `common/matches-words` | `Count` | 8.936 us | 10.436 us | 13.649 us | 0.86x |
| `industry/leipzig-symbol-count` | `Count` | 1,010.716 us | 1,231.769 us | 7,195.760 us | 0.82x |
| `common/match-word` | `Match` | 0.057 us | 0.076 us | 0.965 us | 0.75x |
| `industry/mariomka-ip-count` | `Count` | 2,184.769 us | 3,377.448 us | 10,786.633 us | 0.65x |
| `common/split-words` | `Split` | 12.707 us | 20.046 us | 21.934 us | 0.63x |
| `common/one-node-backtracking` | `IsMatch` | 0.051 us | 0.086 us | 0.124 us | 0.59x |
| `common/matches-set` | `Count` | 59.483 us | 102.938 us | 104.057 us | 0.58x |
| `common/matches-word` | `Count` | 1.194 us | 2.323 us | 3.365 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 642.571 us | 1,306.816 us | 7,432.540 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,031.801 us | 3,020.065 us | 8,198.125 us | 0.34x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.086 us | 0.113 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.929 us | 574.341 us | 838.175 us | 0.29x |
| `common/matches-boundary` | `Count` | 23.756 us | 102.422 us | 104.128 us | 0.23x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,830.810 us | 8,498.056 us | 8,905.856 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 903.781 us | 4,666.125 us | 5,076.187 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 279.854 us | 3,794.045 us | 4,135.194 us | 0.07x |
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
| `lokad/docs/inline-doc-prefix` | 9.880 us | 4.649 us | 5.350 us | 2.13x |
| `lokad/langserv/identifier-validator` | 25.365 us | 12.313 us | 13.462 us | 2.06x |
| `lokad/lexer/doc-line` | 5.714 us | 2.964 us | 3.533 us | 1.93x |
| `fallback/lookahead` | 10.781 us | 5.850 us | 18.244 us | 1.84x |
| `lokad/lexer/identifier` | 40.187 us | 21.976 us | 24.550 us | 1.83x |
| `lokad/style/hex-color` | 1.386 us | 0.799 us | 0.852 us | 1.74x |
| `structural/method-family-generic-call` | 11.196 us | 6.488 us | 19.809 us | 1.73x |
| `lokad/langserv/helper-identifier` | 11.607 us | 7.030 us | 7.543 us | 1.65x |
| `lokad/lexer/number` | 22.441 us | 13.835 us | 14.558 us | 1.62x |
| `lokad/langserv/color-rgb-hex` | 4.536 us | 2.812 us | 2.862 us | 1.61x |
| `lokad/langserv/color-short-hex` | 2.050 us | 1.276 us | 1.400 us | 1.61x |
| `lokad/lexer/operator-run` | 4.924 us | 3.377 us | 3.659 us | 1.46x |
| `lokad/style/range-ref` | 3.644 us | 2.550 us | 2.653 us | 1.43x |
| `lokad/imports/module-imports` | 8.060 us | 6.179 us | 29.884 us | 1.30x |
| `structural/method-family-call` | 10.008 us | 7.889 us | 20.771 us | 1.27x |
| `structural/ordered-keyword-window` | 21.185 us | 16.882 us | 29.911 us | 1.25x |
| `lokad/style/cell-ref` | 3.401 us | 2.748 us | 2.853 us | 1.24x |
| `lokad/lexer/string` | 99.180 us | 93.274 us | 96.236 us | 1.06x |
| `structural/keyword-to-generic-type` | 3.071 us | 3.943 us | 17.274 us | 0.78x |
| `structural/ordered-keyword-pair` | 3.516 us | 5.066 us | 18.295 us | 0.69x |
| `structural/ordered-modifier-pair` | 10.028 us | 16.873 us | 29.853 us | 0.59x |
| `structural/ordered-identifier-window` | 15.163 us | 28.017 us | 40.588 us | 0.54x |
| `literal/identifier-token` | 2.209 us | 4.205 us | 16.962 us | 0.53x |
| `lokad/langserv/url-dashboard` | 12.066 us | 23.113 us | 23.776 us | 0.52x |
| `structural/modifier-family-to-type-window` | 106.090 us | 245.896 us | 258.015 us | 0.43x |
| `literal/call-token` | 1.780 us | 4.156 us | 17.261 us | 0.43x |
| `literal-family/method-token-family` | 3.705 us | 8.850 us | 21.609 us | 0.42x |
| `literal-family/type-token-family` | 9.511 us | 23.256 us | 36.939 us | 0.41x |
| `balancing/begin-end-blocks` | 1,299.816 us | 4,258.041 us | 4,240.741 us | 0.31x |
| `literal/identifier-token-casei` | 3.831 us | 15.359 us | 28.086 us | 0.25x |
| `lokad/langserv/url-download` | 12.866 us | 52.091 us | 52.603 us | 0.25x |
| `structural/modifier-family-to-type-pair` | 22.806 us | 131.098 us | 144.271 us | 0.17x |
| `lokad/folding/region-marker` | 5.126 us | 33.949 us | 62.000 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 15.785 us | 215.384 us | 230.921 us | 0.07x |
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
| `lokad/docs/inline-doc-prefix` | 9.888 us | 2.569 us | 3.393 us | 3.85x |
| `lokad/lexer/number` | 22.465 us | 7.061 us | 8.019 us | 3.18x |
| `balancing/begin-end-blocks` | 1,313.245 us | 446.487 us | 475.039 us | 2.94x |
| `lokad/langserv/identifier-validator` | 25.496 us | 8.697 us | 10.586 us | 2.93x |
| `lokad/style/hex-color` | 1.434 us | 0.491 us | 0.575 us | 2.92x |
| `lokad/lexer/doc-line` | 5.688 us | 2.128 us | 2.869 us | 2.67x |
| `lokad/langserv/helper-identifier` | 11.646 us | 4.394 us | 5.555 us | 2.65x |
| `fallback/lookahead` | 10.782 us | 4.085 us | 16.193 us | 2.64x |
| `lokad/style/range-ref` | 3.634 us | 1.387 us | 1.482 us | 2.62x |
| `lokad/langserv/color-rgb-hex` | 4.513 us | 1.723 us | 1.890 us | 2.62x |
| `lokad/lexer/identifier` | 40.273 us | 15.498 us | 19.175 us | 2.60x |
| `structural/ordered-keyword-window` | 21.119 us | 8.307 us | 20.818 us | 2.54x |
| `structural/method-family-generic-call` | 11.214 us | 4.453 us | 17.030 us | 2.52x |
| `lokad/lexer/operator-run` | 4.876 us | 1.962 us | 2.405 us | 2.49x |
| `lokad/langserv/color-short-hex` | 2.053 us | 0.922 us | 1.093 us | 2.23x |
| `structural/modifier-family-to-type-pair` | 22.837 us | 10.322 us | 22.794 us | 2.21x |
| `lokad/lexer/string` | 98.367 us | 44.926 us | 46.962 us | 2.19x |
| `structural/keyword-family-to-capitalized-identifier` | 15.880 us | 7.465 us | 20.968 us | 2.13x |
| `lokad/style/cell-ref` | 3.386 us | 1.657 us | 1.865 us | 2.04x |
| `structural/method-family-call` | 10.050 us | 4.962 us | 17.304 us | 2.03x |
| `structural/modifier-family-to-type-window` | 105.905 us | 53.559 us | 66.799 us | 1.98x |
| `lokad/imports/module-imports` | 8.034 us | 4.271 us | 27.261 us | 1.88x |
| `lokad/langserv/url-dashboard` | 12.051 us | 7.119 us | 7.746 us | 1.69x |
| `structural/ordered-identifier-window` | 15.046 us | 10.356 us | 23.294 us | 1.45x |
| `structural/ordered-modifier-pair` | 10.065 us | 8.321 us | 21.186 us | 1.21x |
| `lokad/langserv/url-download` | 12.708 us | 12.657 us | 13.195 us | 1.00x |
| `structural/keyword-to-generic-type` | 3.043 us | 3.318 us | 16.016 us | 0.92x |
| `structural/ordered-keyword-pair` | 3.482 us | 3.982 us | 16.475 us | 0.87x |
| `literal-family/type-token-family` | 9.481 us | 11.240 us | 24.038 us | 0.84x |
| `literal-family/method-token-family` | 3.688 us | 5.148 us | 17.613 us | 0.72x |
| `literal/identifier-token-casei` | 3.882 us | 5.797 us | 18.670 us | 0.67x |
| `literal/identifier-token` | 2.197 us | 4.612 us | 16.457 us | 0.48x |
| `literal/call-token` | 1.796 us | 3.806 us | 16.408 us | 0.47x |
| `lokad/folding/region-marker` | 5.180 us | 36.137 us | 61.535 us | 0.14x |
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
