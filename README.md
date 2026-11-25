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
| `literal/sherlock-zh` | 21.450 us | 17.937 us | 2,122.472 us | 1.20x |
| `literal/sherlock-ru` | 65.208 us | 94.009 us | 5,176.343 us | 0.69x |
| `literal-alternate/sherlock-zh` | 41.297 us | 65.174 us | 2,113.314 us | 0.63x |
| `ruff-noqa/tweaked` | 3,859.700 us | 7,423.264 us | 44,447.950 us | 0.52x |
| `literal/sherlock-casei-ru` | 140.473 us | 347.955 us | 5,375.879 us | 0.40x |
| `bounded-repeat/letters-en` | 1,062.369 us | 2,688.900 us | 2,855.092 us | 0.40x |
| `bounded-repeat/letters-ru` | 2,040.811 us | 5,552.586 us | 6,416.900 us | 0.37x |
| `literal/sherlock-en` | 30.210 us | 82.909 us | 450.422 us | 0.36x |
| `literal/sherlock-casei-en` | 66.765 us | 191.147 us | 562.317 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,383.445 us | 4,655.471 us | 9,554.225 us | 0.30x |
| `dictionary/single` | 7,185.082 us | 24,615.575 us | 24,631.675 us | 0.29x |
| `literal-alternate/sherlock-en-mixed` | 152.927 us | 565.559 us | 1,003.691 us | 0.27x |
| `bounded-repeat/capitals` | 11,203.757 us | 44,257.850 us | 47,841.250 us | 0.25x |
| `literal-alternate/sherlock-en` | 332.237 us | 1,446.326 us | 1,933.022 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 121.889 us | 636.622 us | 1,029.500 us | 0.19x |
| `aws-keys/quick` | 1,766.653 us | 9,230.862 us | 44,770.800 us | 0.19x |
| `aws-keys/full` | 8,308.030 us | 82,057.000 us | 110,220.000 us | 0.10x |
| `bounded-repeat/context` | 8,458.844 us | 141,038.200 us | 145,570.200 us | 0.06x |
| `ruff-noqa/real` | 3,321.974 us | 66,498.000 us | 98,485.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 826.152 us | 21,944.650 us | 26,796.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 96.829 us | 10,641.086 us | 11,477.700 us | 0.0091x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 377.658 us | 491.398 us | 8,757.975 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.107 us | 0.143 us | 0.75x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.104 us | 0.165 us | 0.181 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.082 us | 0.100 us | 0.60x |
| `industry/mariomka-uri-count` | `Count` | 1,152.282 us | 1,922.653 us | 10,191.250 us | 0.60x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.045 us | 0.075 us | 0.088 us | 0.59x |
| `common/email-match` | `IsMatch` | 0.152 us | 0.263 us | 0.291 us | 0.58x |
| `common/date-match` | `IsMatch` | 0.059 us | 0.106 us | 0.134 us | 0.56x |
| `industry/leipzig-twain-count` | `Count` | 649.439 us | 1,342.850 us | 8,594.333 us | 0.48x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.082 us | 0.097 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.079 us | 0.184 us | 0.202 us | 0.43x |
| `common/ip-match` | `IsMatch` | 0.068 us | 0.172 us | 0.192 us | 0.40x |
| `common/match-word` | `Match` | 0.050 us | 0.127 us | 1.140 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,174.109 us | 8,167.922 us | 16,526.733 us | 0.39x |
| `common/matches-set` | `Count` | 65.838 us | 190.682 us | 191.117 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.078 us | 0.236 us | 0.274 us | 0.33x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.091 us | 252.110 us | 474.169 us | 0.31x |
| `common/matches-word` | `Count` | 1.216 us | 4.177 us | 5.916 us | 0.29x |
| `common/ip-miss` | `IsMatch` | 0.205 us | 0.740 us | 0.757 us | 0.28x |
| `common/replace-words` | `Replace` | 20.295 us | 76.467 us | 79.397 us | 0.27x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.550 us | 0.587 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.787 us | 964.938 us | 1,223.890 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.982 us | 162.841 us | 164.542 us | 0.17x |
| `common/split-words` | `Split` | 13.000 us | 77.779 us | 78.947 us | 0.17x |
| `common/matches-words` | `Count` | 9.295 us | 69.744 us | 71.181 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,064.973 us | 9,709.600 us | 16,485.467 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,888.023 us | 19,195.000 us | 19,754.325 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 874.529 us | 11,656.143 us | 12,446.357 us | 0.08x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.616 us | 0.649 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.343 us | 0.361 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.817 us | 0.857 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 980.500 us | 23,049.900 us | 30,261.150 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,235.927 us | 31,728.033 us | 38,284.050 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 263.736 us | 8,290.790 us | 8,881.756 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 41.274 us | 34.228 us | 2,026.553 us | 1.21x |
| `bounded-repeat/letters-ru` | 2,029.078 us | 2,036.211 us | 2,866.400 us | 1.00x |
| `literal-alternate/sherlock-en` | 333.379 us | 350.974 us | 732.305 us | 0.95x |
| `literal/sherlock-zh` | 21.259 us | 22.989 us | 2,103.737 us | 0.92x |
| `literal-alternate/sherlock-en-mixed` | 154.668 us | 167.811 us | 526.660 us | 0.92x |
| `literal/sherlock-casei-en` | 66.877 us | 75.698 us | 438.907 us | 0.88x |
| `ruff-noqa/tweaked` | 3,949.532 us | 4,485.247 us | 38,852.950 us | 0.88x |
| `bounded-repeat/capitals` | 11,126.957 us | 12,919.083 us | 15,824.875 us | 0.86x |
| `literal/sherlock-ru` | 65.524 us | 84.992 us | 5,161.660 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 96.956 us | 137.065 us | 552.741 us | 0.71x |
| `literal/sherlock-casei-ru` | 140.722 us | 211.577 us | 5,025.600 us | 0.67x |
| `literal-alternate/sherlock-en-nomatch` | 122.965 us | 193.650 us | 610.237 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,389.678 us | 2,236.424 us | 7,017.383 us | 0.62x |
| `bounded-repeat/letters-en` | 1,067.634 us | 1,761.849 us | 1,888.210 us | 0.61x |
| `aws-keys/quick` | 1,781.109 us | 3,470.332 us | 36,000.400 us | 0.51x |
| `aws-keys/full` | 8,413.220 us | 17,624.680 us | 47,714.233 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 829.787 us | 1,882.957 us | 6,873.920 us | 0.44x |
| `literal/sherlock-en` | 30.250 us | 72.703 us | 441.132 us | 0.42x |
| `ruff-noqa/real` | 3,374.022 us | 19,766.550 us | 49,982.750 us | 0.17x |
| `dictionary/single` | 7,177.909 us | 43,692.950 us | 43,695.750 us | 0.16x |
| `bounded-repeat/context` | 8,468.022 us | 65,865.700 us | 68,458.450 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.104 us | 0.055 us | 0.075 us | 1.90x |
| `common/email-match` | `IsMatch` | 0.151 us | 0.081 us | 0.110 us | 1.87x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.048 us | 0.084 us | 1.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.043 us | 0.031 us | 0.051 us | 1.39x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 77.930 us | 56.920 us | 251.121 us | 1.37x |
| `common/ip-match` | `IsMatch` | 0.065 us | 0.047 us | 0.068 us | 1.37x |
| `common/date-match` | `IsMatch` | 0.058 us | 0.043 us | 0.072 us | 1.36x |
| `common/uri-miss` | `IsMatch` | 0.079 us | 0.061 us | 0.084 us | 1.28x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.037 us | 0.030 us | 0.047 us | 1.24x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.042 us | 0.035 us | 0.051 us | 1.18x |
| `common/replace-words` | `Replace` | 20.329 us | 17.535 us | 18.658 us | 1.16x |
| `common/ip-miss` | `IsMatch` | 0.199 us | 0.178 us | 0.205 us | 1.12x |
| `industry/mariomka-uri-count` | `Count` | 1,153.332 us | 1,096.584 us | 8,220.775 us | 1.05x |
| `common/match-word` | `Match` | 0.050 us | 0.048 us | 0.935 us | 1.04x |
| `industry/mariomka-ip-count` | `Count` | 3,161.025 us | 3,371.400 us | 10,682.633 us | 0.94x |
| `common/backtracking` | `IsMatch` | 0.041 us | 0.043 us | 0.072 us | 0.94x |
| `common/uri-match` | `IsMatch` | 0.078 us | 0.088 us | 0.121 us | 0.89x |
| `industry/mariomka-email-count` | `Count` | 381.471 us | 455.368 us | 7,619.575 us | 0.84x |
| `common/matches-words` | `Count` | 9.196 us | 10.987 us | 14.300 us | 0.84x |
| `industry/leipzig-river-window-count` | `Count` | 1,250.474 us | 1,508.039 us | 7,457.050 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 980.999 us | 1,209.375 us | 6,838.125 us | 0.81x |
| `common/split-words` | `Split` | 13.024 us | 20.264 us | 22.216 us | 0.64x |
| `common/matches-set` | `Count` | 65.780 us | 102.616 us | 103.812 us | 0.64x |
| `common/matches-word` | `Count` | 1.225 us | 2.317 us | 3.290 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.198 us | 0.238 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 650.328 us | 1,329.428 us | 7,413.170 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,082.804 us | 2,999.552 us | 7,878.775 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.030 us | 0.107 us | 0.129 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.423 us | 600.862 us | 862.529 us | 0.28x |
| `common/matches-boundary` | `Count` | 27.907 us | 103.503 us | 106.073 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,887.144 us | 8,598.267 us | 9,114.656 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.088 us | 0.116 us | 0.18x |
| `industry/rust-sherlock-ing-count` | `Count` | 874.339 us | 4,738.663 us | 5,126.473 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 266.932 us | 3,905.647 us | 4,287.578 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.212 us | 5.996 us | 30.306 us | 1.20x |
| `fallback/lookahead` | 6.782 us | 6.059 us | 19.925 us | 1.12x |
| `lokad/lexer/identifier` | 13.762 us | 12.972 us | 15.980 us | 1.06x |
| `lokad/lexer/doc-line` | 1.943 us | 1.844 us | 2.578 us | 1.05x |
| `structural/method-family-call` | 7.876 us | 7.942 us | 21.197 us | 0.99x |
| `structural/method-family-generic-call` | 6.373 us | 6.525 us | 19.521 us | 0.98x |
| `lokad/lexer/operator-run` | 1.949 us | 2.242 us | 2.703 us | 0.87x |
| `lokad/langserv/identifier-validator` | 6.233 us | 7.323 us | 8.672 us | 0.85x |
| `lokad/lexer/number` | 7.928 us | 10.319 us | 11.298 us | 0.77x |
| `structural/ordered-keyword-window` | 13.150 us | 17.377 us | 30.628 us | 0.76x |
| `structural/keyword-to-generic-type` | 2.997 us | 4.030 us | 17.721 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 2.352 us | 3.219 us | 3.992 us | 0.73x |
| `lokad/langserv/helper-identifier` | 3.150 us | 4.573 us | 5.487 us | 0.69x |
| `lokad/langserv/color-short-hex` | 0.494 us | 0.787 us | 0.959 us | 0.63x |
| `structural/ordered-keyword-pair` | 3.233 us | 5.277 us | 19.080 us | 0.61x |
| `structural/ordered-modifier-pair` | 9.873 us | 16.336 us | 29.692 us | 0.60x |
| `literal/identifier-token` | 2.283 us | 4.227 us | 17.072 us | 0.54x |
| `lokad/style/range-ref` | 0.773 us | 1.746 us | 1.888 us | 0.44x |
| `lokad/style/cell-ref` | 0.801 us | 1.823 us | 2.008 us | 0.44x |
| `lokad/langserv/url-dashboard` | 9.832 us | 22.505 us | 22.956 us | 0.44x |
| `literal/call-token` | 1.836 us | 4.286 us | 17.387 us | 0.43x |
| `lokad/style/hex-color` | 0.211 us | 0.500 us | 0.580 us | 0.42x |
| `literal-family/type-token-family` | 9.129 us | 23.423 us | 37.092 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.643 us | 1.708 us | 1.955 us | 0.38x |
| `lokad/lexer/string` | 27.165 us | 78.434 us | 82.584 us | 0.35x |
| `structural/ordered-identifier-window` | 8.514 us | 28.699 us | 41.981 us | 0.30x |
| `literal-family/method-token-family` | 2.615 us | 9.351 us | 22.152 us | 0.28x |
| `literal/identifier-token-casei` | 3.876 us | 15.220 us | 28.711 us | 0.25x |
| `structural/modifier-family-to-type-window` | 64.187 us | 254.192 us | 267.480 us | 0.25x |
| `lokad/langserv/url-download` | 11.302 us | 49.261 us | 50.025 us | 0.23x |
| `lokad/folding/region-marker` | 5.161 us | 34.298 us | 61.045 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 14.339 us | 215.619 us | 232.028 us | 0.07x |
| `structural/modifier-family-to-type-pair` | 2.828 us | 125.839 us | 139.929 us | 0.02x |
| `balancing/begin-end-blocks` | 46.625 us | 4,278.628 us | 4,332.800 us | 0.01x |
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
| `lokad/lexer/number` | 7.896 us | 3.628 us | 4.689 us | 2.18x |
| `lokad/lexer/operator-run` | 1.935 us | 0.890 us | 1.346 us | 2.18x |
| `lokad/lexer/doc-line` | 1.946 us | 1.016 us | 1.751 us | 1.92x |
| `structural/keyword-family-to-capitalized-identifier` | 14.293 us | 7.693 us | 20.734 us | 1.86x |
| `lokad/lexer/identifier` | 13.732 us | 7.665 us | 10.384 us | 1.79x |
| `lokad/imports/module-imports` | 7.197 us | 4.235 us | 27.734 us | 1.70x |
| `lokad/docs/inline-doc-prefix` | 2.357 us | 1.390 us | 2.127 us | 1.70x |
| `lokad/langserv/url-dashboard` | 9.949 us | 6.058 us | 6.545 us | 1.64x |
| `fallback/lookahead` | 6.787 us | 4.196 us | 17.525 us | 1.62x |
| `structural/ordered-keyword-window` | 13.072 us | 8.485 us | 21.179 us | 1.54x |
| `structural/method-family-call` | 7.824 us | 5.105 us | 17.940 us | 1.53x |
| `lokad/langserv/identifier-validator` | 6.179 us | 4.101 us | 5.529 us | 1.51x |
| `structural/method-family-generic-call` | 6.393 us | 4.578 us | 16.696 us | 1.40x |
| `lokad/langserv/helper-identifier` | 3.149 us | 2.595 us | 3.481 us | 1.21x |
| `structural/ordered-modifier-pair` | 9.862 us | 8.395 us | 21.221 us | 1.17x |
| `structural/modifier-family-to-type-window` | 63.965 us | 55.976 us | 70.386 us | 1.14x |
| `lokad/style/range-ref` | 0.767 us | 0.696 us | 0.832 us | 1.10x |
| `lokad/style/cell-ref` | 0.802 us | 0.802 us | 1.010 us | 1.00x |
| `lokad/langserv/color-short-hex` | 0.452 us | 0.466 us | 0.649 us | 0.97x |
| `lokad/langserv/url-download` | 11.340 us | 11.891 us | 12.337 us | 0.95x |
| `lokad/lexer/string` | 27.078 us | 29.439 us | 32.441 us | 0.92x |
| `lokad/style/hex-color` | 0.194 us | 0.221 us | 0.299 us | 0.88x |
| `structural/keyword-to-generic-type` | 3.000 us | 3.485 us | 16.545 us | 0.86x |
| `literal-family/type-token-family` | 9.140 us | 11.302 us | 24.418 us | 0.81x |
| `structural/ordered-identifier-window` | 8.474 us | 10.592 us | 23.749 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.198 us | 4.132 us | 17.169 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 0.580 us | 0.820 us | 1.059 us | 0.71x |
| `literal/identifier-token-casei` | 3.909 us | 5.840 us | 18.986 us | 0.67x |
| `literal/identifier-token` | 2.263 us | 4.198 us | 16.849 us | 0.54x |
| `literal-family/method-token-family` | 2.639 us | 5.136 us | 18.071 us | 0.51x |
| `literal/call-token` | 1.832 us | 3.918 us | 16.510 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.836 us | 10.296 us | 23.075 us | 0.28x |
| `lokad/folding/region-marker` | 5.104 us | 35.027 us | 59.802 us | 0.15x |
| `balancing/begin-end-blocks` | 46.670 us | 468.893 us | 497.272 us | 0.10x |
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
