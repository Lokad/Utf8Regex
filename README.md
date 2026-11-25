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
| `literal/sherlock-zh` | 21.141 us | 17.886 us | 2,083.305 us | 1.18x |
| `literal/sherlock-ru` | 65.258 us | 94.352 us | 5,132.333 us | 0.69x |
| `literal-alternate/sherlock-zh` | 41.037 us | 65.069 us | 2,115.983 us | 0.63x |
| `ruff-noqa/tweaked` | 3,909.320 us | 7,641.755 us | 43,675.900 us | 0.51x |
| `literal/sherlock-casei-ru` | 143.893 us | 347.129 us | 5,430.271 us | 0.41x |
| `literal/sherlock-casei-en` | 74.738 us | 188.797 us | 522.665 us | 0.40x |
| `bounded-repeat/letters-en` | 1,059.528 us | 2,700.489 us | 2,849.489 us | 0.39x |
| `literal/sherlock-en` | 30.007 us | 81.539 us | 464.272 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,925.108 us | 5,498.757 us | 6,317.833 us | 0.35x |
| `dictionary/single` | 7,791.750 us | 24,379.550 us | 24,575.950 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,378.640 us | 4,566.924 us | 9,463.000 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 147.429 us | 592.657 us | 1,023.920 us | 0.25x |
| `bounded-repeat/capitals` | 11,035.229 us | 44,384.150 us | 47,686.150 us | 0.25x |
| `literal-alternate/sherlock-en` | 322.902 us | 1,441.862 us | 2,022.513 us | 0.22x |
| `aws-keys/quick` | 1,808.163 us | 9,083.200 us | 38,686.850 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 121.840 us | 619.971 us | 1,024.781 us | 0.20x |
| `aws-keys/full` | 8,287.011 us | 81,577.200 us | 109,860.900 us | 0.10x |
| `bounded-repeat/context` | 8,307.380 us | 139,145.700 us | 143,091.000 us | 0.06x |
| `ruff-noqa/real` | 3,362.017 us | 68,451.950 us | 97,823.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 805.383 us | 22,328.875 us | 27,369.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 99.738 us | 10,569.663 us | 11,441.857 us | 0.0094x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.106 us | 0.141 us | 0.74x |
| `industry/mariomka-email-count` | `Count` | 353.982 us | 491.759 us | 8,904.625 us | 0.72x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.073 us | 0.087 us | 0.69x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.056 us | 0.081 us | 0.097 us | 0.69x |
| `industry/mariomka-uri-count` | `Count` | 1,152.588 us | 1,828.779 us | 9,891.225 us | 0.63x |
| `common/date-match` | `IsMatch` | 0.061 us | 0.106 us | 0.135 us | 0.58x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.077 us | 0.092 us | 0.57x |
| `common/email-match` | `IsMatch` | 0.129 us | 0.254 us | 0.277 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 643.358 us | 1,296.398 us | 8,569.911 us | 0.50x |
| `common/uri-miss` | `IsMatch` | 0.081 us | 0.182 us | 0.199 us | 0.45x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.071 us | 0.163 us | 0.178 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.175 us | 0.195 us | 0.41x |
| `common/match-word` | `Match` | 0.048 us | 0.123 us | 1.126 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,085.340 us | 7,923.850 us | 16,313.533 us | 0.39x |
| `common/uri-match` | `IsMatch` | 0.078 us | 0.228 us | 0.262 us | 0.34x |
| `common/matches-set` | `Count` | 60.115 us | 189.726 us | 190.266 us | 0.32x |
| `common/ip-miss` | `IsMatch` | 0.219 us | 0.696 us | 0.719 us | 0.31x |
| `common/matches-word` | `Count` | 1.192 us | 4.076 us | 5.736 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.127 us | 251.444 us | 474.303 us | 0.27x |
| `common/replace-words` | `Replace` | 19.852 us | 75.249 us | 78.557 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.101 us | 0.545 us | 0.583 us | 0.18x |
| `common/matches-boundary` | `Count` | 28.387 us | 157.568 us | 160.279 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.874 us | 936.095 us | 1,249.732 us | 0.18x |
| `common/split-words` | `Split` | 12.625 us | 75.347 us | 76.873 us | 0.17x |
| `common/matches-words` | `Count` | 8.747 us | 67.388 us | 68.775 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,076.774 us | 9,375.962 us | 16,386.733 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,912.078 us | 18,882.125 us | 19,423.575 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 858.513 us | 11,417.514 us | 12,046.129 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.032 us | 0.597 us | 0.633 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,622.894 us | 32,020.100 us | 38,662.750 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.017 us | 0.337 us | 0.363 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 978.323 us | 22,024.300 us | 28,943.500 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.031 us | 0.806 us | 0.848 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 271.251 us | 7,891.560 us | 8,317.544 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 40.935 us | 33.833 us | 2,052.186 us | 1.21x |
| `literal/sherlock-casei-en` | 75.036 us | 73.934 us | 413.432 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,920.815 us | 2,026.600 us | 2,823.293 us | 0.95x |
| `literal/sherlock-zh` | 21.240 us | 22.772 us | 2,045.533 us | 0.93x |
| `literal-alternate/sherlock-en` | 325.445 us | 349.946 us | 752.647 us | 0.93x |
| `ruff-noqa/tweaked` | 3,931.550 us | 4,454.853 us | 38,048.200 us | 0.88x |
| `literal-alternate/sherlock-en-mixed` | 148.123 us | 170.226 us | 535.357 us | 0.87x |
| `bounded-repeat/capitals` | 11,118.014 us | 12,818.433 us | 15,587.425 us | 0.87x |
| `literal/sherlock-casei-ru` | 144.282 us | 187.350 us | 5,212.720 us | 0.77x |
| `literal/sherlock-ru` | 65.325 us | 86.095 us | 5,045.620 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 98.604 us | 139.563 us | 550.455 us | 0.71x |
| `literal-alternate/sherlock-en-nomatch` | 122.061 us | 189.992 us | 547.975 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,381.980 us | 2,227.168 us | 7,148.450 us | 0.62x |
| `bounded-repeat/letters-en` | 1,055.776 us | 1,736.380 us | 1,860.937 us | 0.61x |
| `aws-keys/quick` | 1,907.274 us | 3,495.455 us | 34,257.800 us | 0.55x |
| `aws-keys/full` | 8,551.400 us | 17,575.300 us | 47,960.033 us | 0.49x |
| `literal-alternate/sherlock-casei-ru` | 810.457 us | 1,887.329 us | 6,926.650 us | 0.43x |
| `literal/sherlock-en` | 30.239 us | 71.836 us | 418.250 us | 0.42x |
| `dictionary/single` | 7,787.380 us | 43,626.400 us | 43,561.500 us | 0.18x |
| `ruff-noqa/real` | 3,427.696 us | 20,418.350 us | 51,689.267 us | 0.17x |
| `bounded-repeat/context` | 8,312.089 us | 65,317.050 us | 68,201.800 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.069 us | 0.047 us | 0.067 us | 1.48x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.045 us | 0.031 us | 0.048 us | 1.47x |
| `common/date-match` | `IsMatch` | 0.062 us | 0.042 us | 0.071 us | 1.46x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.069 us | 0.053 us | 0.074 us | 1.29x |
| `common/ip-miss` | `IsMatch` | 0.217 us | 0.174 us | 0.199 us | 1.25x |
| `common/uri-miss` | `IsMatch` | 0.079 us | 0.065 us | 0.084 us | 1.22x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.042 us | 0.034 us | 0.050 us | 1.21x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.036 us | 0.030 us | 0.046 us | 1.19x |
| `common/replace-words` | `Replace` | 19.815 us | 16.745 us | 19.397 us | 1.18x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.457 us | 56.920 us | 266.985 us | 1.17x |
| `common/match-word` | `Match` | 0.048 us | 0.045 us | 0.898 us | 1.07x |
| `industry/mariomka-uri-count` | `Count` | 1,140.907 us | 1,069.786 us | 8,116.575 us | 1.07x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.050 us | 0.047 us | 0.083 us | 1.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,630.272 us | 1,557.818 us | 8,030.900 us | 1.05x |
| `industry/mariomka-ip-count` | `Count` | 3,078.388 us | 3,273.083 us | 10,496.767 us | 0.94x |
| `common/email-match` | `IsMatch` | 0.072 us | 0.078 us | 0.105 us | 0.92x |
| `common/uri-match` | `IsMatch` | 0.076 us | 0.084 us | 0.117 us | 0.90x |
| `common/matches-words` | `Count` | 8.771 us | 10.283 us | 13.452 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 975.185 us | 1,192.489 us | 6,796.720 us | 0.82x |
| `industry/mariomka-email-count` | `Count` | 353.755 us | 458.812 us | 7,532.875 us | 0.77x |
| `common/backtracking` | `IsMatch` | 0.030 us | 0.042 us | 0.072 us | 0.71x |
| `common/split-words` | `Split` | 12.721 us | 19.761 us | 21.421 us | 0.64x |
| `common/matches-set` | `Count` | 59.998 us | 101.739 us | 103.162 us | 0.59x |
| `common/matches-word` | `Count` | 1.191 us | 2.257 us | 3.327 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.102 us | 0.195 us | 0.233 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 647.315 us | 1,310.468 us | 7,424.000 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,077.416 us | 2,941.142 us | 7,892.625 us | 0.37x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.103 us | 0.124 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.106 us | 588.411 us | 864.410 us | 0.28x |
| `common/matches-boundary` | `Count` | 27.333 us | 101.008 us | 103.925 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,903.690 us | 8,573.022 us | 9,056.300 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.017 us | 0.087 us | 0.112 us | 0.19x |
| `industry/rust-sherlock-ing-count` | `Count` | 858.627 us | 4,615.941 us | 4,996.550 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 272.358 us | 3,859.005 us | 4,238.583 us | 0.07x |
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
| `lokad/imports/module-imports` | 5.985 us | 6.185 us | 30.194 us | 0.97x |
| `fallback/lookahead` | 5.030 us | 5.823 us | 18.937 us | 0.86x |
| `structural/keyword-to-generic-type` | 2.919 us | 3.938 us | 17.428 us | 0.74x |
| `structural/method-family-call` | 5.029 us | 7.668 us | 20.477 us | 0.66x |
| `structural/ordered-keyword-window` | 11.175 us | 17.098 us | 30.259 us | 0.65x |
| `structural/method-family-generic-call` | 4.090 us | 6.426 us | 19.552 us | 0.64x |
| `lokad/langserv/color-short-hex` | 0.493 us | 0.792 us | 0.976 us | 0.62x |
| `structural/ordered-keyword-pair` | 3.164 us | 5.157 us | 18.427 us | 0.61x |
| `structural/ordered-modifier-pair` | 9.894 us | 16.419 us | 29.000 us | 0.60x |
| `lokad/lexer/identifier` | 7.860 us | 13.071 us | 16.116 us | 0.60x |
| `lokad/langserv/identifier-validator` | 4.165 us | 7.299 us | 8.690 us | 0.57x |
| `lokad/lexer/doc-line` | 1.004 us | 1.811 us | 2.501 us | 0.55x |
| `lokad/docs/inline-doc-prefix` | 1.752 us | 3.343 us | 4.124 us | 0.52x |
| `literal/identifier-token` | 2.176 us | 4.199 us | 16.740 us | 0.52x |
| `lokad/langserv/helper-identifier` | 2.251 us | 4.571 us | 5.449 us | 0.49x |
| `lokad/lexer/operator-run` | 1.044 us | 2.268 us | 2.714 us | 0.46x |
| `literal/call-token` | 1.779 us | 4.141 us | 17.265 us | 0.43x |
| `lokad/style/hex-color` | 0.210 us | 0.499 us | 0.575 us | 0.42x |
| `lokad/style/range-ref` | 0.718 us | 1.749 us | 1.911 us | 0.41x |
| `lokad/style/cell-ref` | 0.759 us | 1.903 us | 2.048 us | 0.40x |
| `literal-family/type-token-family` | 8.928 us | 23.257 us | 36.720 us | 0.38x |
| `lokad/lexer/string` | 30.212 us | 79.119 us | 84.000 us | 0.38x |
| `lokad/langserv/url-dashboard` | 8.905 us | 23.517 us | 24.092 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.639 us | 1.730 us | 1.977 us | 0.37x |
| `structural/ordered-identifier-window` | 8.028 us | 27.679 us | 40.455 us | 0.29x |
| `literal-family/method-token-family` | 2.535 us | 8.978 us | 21.226 us | 0.28x |
| `literal/identifier-token-casei` | 3.763 us | 14.460 us | 27.807 us | 0.26x |
| `lokad/lexer/number` | 2.759 us | 10.696 us | 11.590 us | 0.26x |
| `lokad/langserv/url-download` | 10.983 us | 47.037 us | 47.117 us | 0.23x |
| `structural/modifier-family-to-type-window` | 56.225 us | 245.498 us | 258.530 us | 0.23x |
| `lokad/folding/region-marker` | 5.489 us | 35.039 us | 59.917 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.817 us | 216.043 us | 228.484 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.827 us | 129.447 us | 142.303 us | 0.02x |
| `balancing/begin-end-blocks` | 45.444 us | 4,052.463 us | 4,063.795 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 8.940 us | 6.085 us | 6.606 us | 1.47x |
| `lokad/imports/module-imports` | 5.983 us | 4.278 us | 27.652 us | 1.40x |
| `structural/ordered-keyword-window` | 11.273 us | 8.339 us | 20.961 us | 1.35x |
| `structural/keyword-family-to-capitalized-identifier` | 9.795 us | 7.700 us | 20.732 us | 1.27x |
| `lokad/docs/inline-doc-prefix` | 1.754 us | 1.389 us | 2.121 us | 1.26x |
| `fallback/lookahead` | 5.086 us | 4.063 us | 16.607 us | 1.25x |
| `structural/ordered-modifier-pair` | 9.917 us | 8.264 us | 20.668 us | 1.20x |
| `lokad/lexer/operator-run` | 1.026 us | 0.907 us | 1.381 us | 1.13x |
| `structural/method-family-call` | 5.069 us | 4.841 us | 17.263 us | 1.05x |
| `structural/modifier-family-to-type-window` | 55.842 us | 53.778 us | 67.062 us | 1.04x |
| `lokad/lexer/string` | 30.178 us | 29.453 us | 31.959 us | 1.02x |
| `lokad/style/range-ref` | 0.719 us | 0.704 us | 0.842 us | 1.02x |
| `lokad/langserv/identifier-validator` | 4.146 us | 4.083 us | 5.698 us | 1.02x |
| `lokad/lexer/identifier` | 7.840 us | 7.788 us | 10.468 us | 1.01x |
| `lokad/lexer/doc-line` | 1.000 us | 1.023 us | 1.739 us | 0.98x |
| `lokad/style/cell-ref` | 0.764 us | 0.804 us | 1.011 us | 0.95x |
| `lokad/langserv/color-short-hex` | 0.453 us | 0.479 us | 0.660 us | 0.95x |
| `lokad/langserv/url-download` | 11.069 us | 11.717 us | 12.160 us | 0.94x |
| `structural/method-family-generic-call` | 4.123 us | 4.464 us | 16.811 us | 0.92x |
| `lokad/langserv/helper-identifier` | 2.277 us | 2.626 us | 3.503 us | 0.87x |
| `lokad/style/hex-color` | 0.195 us | 0.226 us | 0.304 us | 0.86x |
| `structural/keyword-to-generic-type` | 2.906 us | 3.369 us | 16.371 us | 0.86x |
| `literal-family/type-token-family` | 8.898 us | 11.082 us | 23.741 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.163 us | 3.983 us | 16.751 us | 0.79x |
| `structural/ordered-identifier-window` | 7.974 us | 10.240 us | 23.349 us | 0.78x |
| `lokad/lexer/number` | 2.831 us | 3.692 us | 4.710 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 0.566 us | 0.818 us | 1.078 us | 0.69x |
| `literal/identifier-token-casei` | 3.754 us | 5.685 us | 18.647 us | 0.66x |
| `literal-family/method-token-family` | 2.554 us | 4.936 us | 17.322 us | 0.52x |
| `literal/identifier-token` | 2.186 us | 4.531 us | 16.216 us | 0.48x |
| `literal/call-token` | 1.789 us | 3.825 us | 16.429 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.804 us | 10.237 us | 22.711 us | 0.27x |
| `lokad/folding/region-marker` | 5.480 us | 33.983 us | 59.244 us | 0.16x |
| `balancing/begin-end-blocks` | 45.890 us | 450.185 us | 474.850 us | 0.10x |
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
