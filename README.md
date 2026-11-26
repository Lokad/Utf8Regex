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
| `literal/sherlock-zh` | 21.682 us | 18.193 us | 2,146.062 us | 1.19x |
| `literal/sherlock-ru` | 65.583 us | 94.572 us | 5,163.193 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.078 us | 66.305 us | 2,161.384 us | 0.63x |
| `ruff-noqa/tweaked` | 3,918.835 us | 7,792.509 us | 43,652.100 us | 0.50x |
| `literal/sherlock-casei-ru` | 146.271 us | 353.258 us | 5,288.300 us | 0.41x |
| `bounded-repeat/letters-en` | 1,076.427 us | 2,722.750 us | 2,911.565 us | 0.40x |
| `literal/sherlock-en` | 30.550 us | 82.009 us | 469.407 us | 0.37x |
| `literal/sherlock-casei-en` | 67.146 us | 187.239 us | 723.326 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,971.062 us | 5,635.857 us | 6,528.267 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,380.536 us | 4,549.371 us | 9,512.300 us | 0.30x |
| `dictionary/single` | 7,373.840 us | 25,438.033 us | 25,579.267 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 163.828 us | 577.706 us | 1,056.197 us | 0.28x |
| `bounded-repeat/capitals` | 11,492.100 us | 44,210.350 us | 47,650.350 us | 0.26x |
| `literal-alternate/sherlock-en` | 326.060 us | 1,476.958 us | 2,005.395 us | 0.22x |
| `aws-keys/quick` | 1,964.292 us | 9,280.475 us | 40,586.700 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 122.518 us | 648.612 us | 1,093.188 us | 0.19x |
| `aws-keys/full` | 9,340.250 us | 83,352.100 us | 112,318.300 us | 0.11x |
| `bounded-repeat/context` | 8,690.589 us | 142,967.500 us | 146,250.900 us | 0.06x |
| `ruff-noqa/real` | 3,415.300 us | 68,802.350 us | 85,255.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 820.084 us | 22,193.950 us | 27,365.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 97.994 us | 10,514.800 us | 11,383.043 us | 0.0093x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.086 us | 0.108 us | 0.147 us | 0.79x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.052 us | 0.076 us | 0.091 us | 0.67x |
| `industry/mariomka-email-count` | `Count` | 323.298 us | 494.224 us | 8,413.150 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.107 us | 0.137 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,193.714 us | 1,870.638 us | 10,506.875 us | 0.64x |
| `common/ip-match` | `IsMatch` | 0.107 us | 0.171 us | 0.191 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.085 us | 0.103 us | 0.62x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.083 us | 0.099 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.133 us | 0.255 us | 0.279 us | 0.52x |
| `common/uri-miss` | `IsMatch` | 0.093 us | 0.182 us | 0.202 us | 0.51x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.081 us | 0.168 us | 0.184 us | 0.48x |
| `industry/leipzig-twain-count` | `Count` | 633.397 us | 1,324.628 us | 8,857.160 us | 0.48x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.226 us | 0.260 us | 0.40x |
| `common/match-word` | `Match` | 0.050 us | 0.127 us | 1.186 us | 0.40x |
| `industry/mariomka-ip-count` | `Count` | 3,167.713 us | 8,352.630 us | 17,503.267 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.268 us | 0.715 us | 0.734 us | 0.38x |
| `common/matches-set` | `Count` | 60.308 us | 188.036 us | 188.828 us | 0.32x |
| `common/matches-word` | `Count` | 1.189 us | 4.089 us | 5.811 us | 0.29x |
| `common/replace-words` | `Replace` | 20.638 us | 79.654 us | 81.459 us | 0.26x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 68.379 us | 264.915 us | 536.688 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.105 us | 0.535 us | 0.574 us | 0.20x |
| `common/matches-boundary` | `Count` | 29.473 us | 157.318 us | 160.303 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.893 us | 975.173 us | 1,280.425 us | 0.17x |
| `common/split-words` | `Split` | 13.144 us | 77.831 us | 79.349 us | 0.17x |
| `common/matches-words` | `Count` | 8.918 us | 67.524 us | 69.076 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,080.147 us | 9,804.987 us | 17,086.200 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,963.077 us | 19,490.400 us | 20,464.675 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.625 us | 0.660 us | 0.06x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.332 us | 0.358 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,660.613 us | 32,468.700 us | 40,622.600 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,007.338 us | 22,370.500 us | 29,764.650 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.829 us | 0.876 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 254.479 us | 8,552.622 us | 8,955.833 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 291.956 us | 12,077.814 us | 12,651.117 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 41.859 us | 34.730 us | 2,113.830 us | 1.21x |
| `bounded-repeat/letters-ru` | 1,976.526 us | 2,071.500 us | 2,895.512 us | 0.95x |
| `literal-alternate/sherlock-en-mixed` | 162.874 us | 170.752 us | 571.275 us | 0.95x |
| `literal/sherlock-zh` | 21.921 us | 23.408 us | 2,170.782 us | 0.94x |
| `literal-alternate/sherlock-en` | 325.698 us | 356.764 us | 843.389 us | 0.91x |
| `ruff-noqa/tweaked` | 3,925.089 us | 4,455.033 us | 39,349.800 us | 0.88x |
| `bounded-repeat/capitals` | 11,493.900 us | 13,092.783 us | 16,109.825 us | 0.88x |
| `literal/sherlock-casei-en` | 66.998 us | 78.081 us | 444.155 us | 0.86x |
| `literal/sherlock-casei-ru` | 148.564 us | 187.194 us | 5,134.857 us | 0.79x |
| `literal/sherlock-ru` | 65.673 us | 86.219 us | 5,026.540 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 98.023 us | 147.722 us | 613.254 us | 0.66x |
| `literal-alternate/sherlock-en-nomatch` | 122.243 us | 198.634 us | 606.492 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,379.859 us | 2,267.532 us | 7,204.725 us | 0.61x |
| `bounded-repeat/letters-en` | 1,074.351 us | 1,779.386 us | 1,929.156 us | 0.60x |
| `aws-keys/quick` | 1,983.013 us | 3,598.518 us | 35,673.133 us | 0.55x |
| `aws-keys/full` | 8,454.720 us | 17,818.960 us | 49,606.367 us | 0.47x |
| `literal-alternate/sherlock-casei-ru` | 838.607 us | 1,899.166 us | 6,859.700 us | 0.44x |
| `literal/sherlock-en` | 30.444 us | 71.465 us | 423.892 us | 0.43x |
| `dictionary/single` | 7,421.836 us | 44,568.650 us | 44,656.100 us | 0.17x |
| `ruff-noqa/real` | 3,417.941 us | 20,659.375 us | 34,292.267 us | 0.17x |
| `bounded-repeat/context` | 8,745.522 us | 66,689.900 us | 70,556.850 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.104 us | 0.046 us | 0.065 us | 2.29x |
| `common/date-match` | `IsMatch` | 0.068 us | 0.041 us | 0.072 us | 1.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.032 us | 0.050 us | 1.50x |
| `common/ip-miss` | `IsMatch` | 0.259 us | 0.176 us | 0.199 us | 1.47x |
| `common/uri-miss` | `IsMatch` | 0.094 us | 0.066 us | 0.084 us | 1.42x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.056 us | 0.078 us | 1.37x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.034 us | 0.053 us | 1.33x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.031 us | 0.047 us | 1.25x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 68.375 us | 57.897 us | 284.700 us | 1.18x |
| `common/replace-words` | `Replace` | 20.746 us | 17.769 us | 19.353 us | 1.17x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.053 us | 0.048 us | 0.088 us | 1.09x |
| `industry/mariomka-uri-count` | `Count` | 1,207.522 us | 1,121.309 us | 8,649.625 us | 1.08x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.087 us | 0.118 us | 1.05x |
| `common/match-word` | `Match` | 0.050 us | 0.048 us | 0.955 us | 1.03x |
| `industry/leipzig-river-window-count` | `Count` | 1,687.926 us | 1,645.071 us | 8,438.975 us | 1.03x |
| `common/email-match` | `IsMatch` | 0.074 us | 0.077 us | 0.102 us | 0.96x |
| `industry/mariomka-ip-count` | `Count` | 3,179.046 us | 3,450.439 us | 11,506.833 us | 0.92x |
| `common/matches-words` | `Count` | 8.891 us | 10.498 us | 13.649 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 1,006.316 us | 1,239.641 us | 6,978.025 us | 0.81x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.044 us | 0.074 us | 0.73x |
| `industry/mariomka-email-count` | `Count` | 325.343 us | 460.798 us | 8,347.600 us | 0.71x |
| `common/split-words` | `Split` | 13.181 us | 20.533 us | 22.678 us | 0.64x |
| `common/matches-set` | `Count` | 60.895 us | 103.058 us | 104.934 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.104 us | 0.190 us | 0.223 us | 0.55x |
| `common/matches-word` | `Count` | 1.183 us | 2.252 us | 3.681 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 633.543 us | 1,294.432 us | 7,823.711 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,082.993 us | 3,030.656 us | 8,675.525 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.108 us | 0.130 us | 0.33x |
| `common/matches-boundary` | `Count` | 28.779 us | 100.508 us | 102.791 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.432 us | 595.820 us | 859.556 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,968.711 us | 8,861.300 us | 9,520.743 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.017 us | 0.086 us | 0.114 us | 0.20x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 258.735 us | 3,933.940 us | 4,308.678 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 290.702 us | 4,814.837 us | 5,283.313 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.055 us | 6.410 us | 32.798 us | 0.94x |
| `fallback/lookahead` | 5.362 us | 6.048 us | 20.685 us | 0.89x |
| `structural/method-family-generic-call` | 4.639 us | 6.555 us | 21.377 us | 0.71x |
| `structural/ordered-keyword-window` | 11.826 us | 17.694 us | 32.328 us | 0.67x |
| `lokad/langserv/color-short-hex` | 0.535 us | 0.817 us | 1.003 us | 0.65x |
| `lokad/lexer/identifier` | 8.849 us | 13.594 us | 16.797 us | 0.65x |
| `structural/method-family-call` | 5.317 us | 8.402 us | 23.487 us | 0.63x |
| `lokad/langserv/identifier-validator` | 4.706 us | 7.459 us | 9.307 us | 0.63x |
| `structural/ordered-modifier-pair` | 10.723 us | 17.960 us | 32.410 us | 0.60x |
| `structural/keyword-to-generic-type` | 2.428 us | 4.082 us | 19.185 us | 0.59x |
| `lokad/lexer/doc-line` | 1.113 us | 1.884 us | 2.641 us | 0.59x |
| `lokad/docs/inline-doc-prefix` | 1.965 us | 3.485 us | 4.318 us | 0.56x |
| `lokad/langserv/helper-identifier` | 2.656 us | 4.845 us | 5.784 us | 0.55x |
| `structural/ordered-keyword-pair` | 2.845 us | 5.305 us | 20.799 us | 0.54x |
| `literal/identifier-token` | 2.237 us | 4.361 us | 18.355 us | 0.51x |
| `lokad/lexer/operator-run` | 1.149 us | 2.379 us | 2.830 us | 0.48x |
| `lokad/style/hex-color` | 0.229 us | 0.519 us | 0.605 us | 0.44x |
| `literal/call-token` | 1.844 us | 4.212 us | 18.657 us | 0.44x |
| `lokad/style/cell-ref` | 0.829 us | 1.903 us | 2.131 us | 0.44x |
| `lokad/style/range-ref` | 0.772 us | 1.856 us | 1.955 us | 0.42x |
| `lokad/lexer/string` | 32.358 us | 81.962 us | 87.542 us | 0.39x |
| `lokad/langserv/url-dashboard` | 9.060 us | 23.344 us | 23.904 us | 0.39x |
| `literal-family/type-token-family` | 9.238 us | 23.987 us | 39.412 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.673 us | 1.795 us | 2.045 us | 0.37x |
| `literal/identifier-token-casei` | 4.867 us | 15.137 us | 29.809 us | 0.32x |
| `lokad/lexer/number` | 3.152 us | 10.928 us | 12.048 us | 0.29x |
| `structural/ordered-identifier-window` | 8.269 us | 28.862 us | 43.228 us | 0.29x |
| `literal-family/method-token-family` | 2.638 us | 9.434 us | 23.635 us | 0.28x |
| `structural/modifier-family-to-type-window` | 60.665 us | 256.676 us | 273.106 us | 0.24x |
| `lokad/langserv/url-download` | 11.102 us | 51.759 us | 52.091 us | 0.21x |
| `lokad/folding/region-marker` | 5.640 us | 35.941 us | 64.137 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.963 us | 217.019 us | 232.517 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.914 us | 128.355 us | 144.436 us | 0.02x |
| `balancing/begin-end-blocks` | 47.949 us | 4,398.994 us | 4,422.141 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.015 us | 6.360 us | 6.812 us | 1.42x |
| `structural/ordered-keyword-window` | 11.886 us | 8.638 us | 23.120 us | 1.38x |
| `lokad/imports/module-imports` | 6.036 us | 4.435 us | 29.880 us | 1.36x |
| `lokad/docs/inline-doc-prefix` | 1.964 us | 1.447 us | 2.204 us | 1.36x |
| `structural/keyword-family-to-capitalized-identifier` | 9.968 us | 7.694 us | 22.244 us | 1.30x |
| `fallback/lookahead` | 5.400 us | 4.238 us | 18.485 us | 1.27x |
| `structural/ordered-modifier-pair` | 10.776 us | 8.791 us | 23.156 us | 1.23x |
| `lokad/lexer/operator-run` | 1.138 us | 0.935 us | 1.431 us | 1.22x |
| `lokad/lexer/identifier` | 8.875 us | 8.058 us | 10.839 us | 1.10x |
| `lokad/langserv/identifier-validator` | 4.706 us | 4.322 us | 5.820 us | 1.09x |
| `structural/modifier-family-to-type-window` | 60.944 us | 56.256 us | 72.426 us | 1.08x |
| `lokad/style/range-ref` | 0.783 us | 0.732 us | 0.862 us | 1.07x |
| `lokad/lexer/string` | 32.695 us | 30.956 us | 33.372 us | 1.06x |
| `structural/method-family-call` | 5.304 us | 5.080 us | 19.598 us | 1.04x |
| `lokad/lexer/doc-line` | 1.112 us | 1.069 us | 1.875 us | 1.04x |
| `lokad/langserv/color-short-hex` | 0.497 us | 0.480 us | 0.682 us | 1.03x |
| `structural/method-family-generic-call` | 4.641 us | 4.610 us | 18.360 us | 1.01x |
| `lokad/style/cell-ref` | 0.820 us | 0.827 us | 1.079 us | 0.99x |
| `lokad/langserv/helper-identifier` | 2.636 us | 2.704 us | 3.659 us | 0.98x |
| `lokad/langserv/url-download` | 11.082 us | 12.144 us | 12.583 us | 0.91x |
| `lokad/style/hex-color` | 0.211 us | 0.233 us | 0.315 us | 0.91x |
| `literal/identifier-token-casei` | 4.852 us | 5.951 us | 20.643 us | 0.82x |
| `literal-family/type-token-family` | 9.233 us | 11.490 us | 26.053 us | 0.80x |
| `lokad/lexer/number` | 3.107 us | 3.869 us | 5.039 us | 0.80x |
| `structural/ordered-identifier-window` | 8.274 us | 10.737 us | 25.408 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 0.623 us | 0.855 us | 1.102 us | 0.73x |
| `structural/keyword-to-generic-type` | 2.433 us | 3.519 us | 18.041 us | 0.69x |
| `structural/ordered-keyword-pair` | 2.834 us | 4.161 us | 18.662 us | 0.68x |
| `literal-family/method-token-family` | 2.652 us | 5.150 us | 19.530 us | 0.52x |
| `literal/identifier-token` | 2.244 us | 4.665 us | 18.097 us | 0.48x |
| `literal/call-token` | 1.852 us | 3.982 us | 17.729 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.918 us | 10.651 us | 25.527 us | 0.27x |
| `lokad/folding/region-marker` | 5.650 us | 36.022 us | 63.313 us | 0.16x |
| `balancing/begin-end-blocks` | 47.247 us | 471.183 us | 507.383 us | 0.10x |
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
