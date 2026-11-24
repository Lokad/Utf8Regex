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
| `literal/sherlock-zh` | 22.409 us | 18.734 us | 2,197.797 us | 1.20x |
| `literal/sherlock-ru` | 69.150 us | 98.379 us | 5,324.007 us | 0.70x |
| `literal-alternate/sherlock-zh` | 43.431 us | 69.284 us | 2,211.723 us | 0.63x |
| `ruff-noqa/tweaked` | 4,081.042 us | 7,697.955 us | 45,843.550 us | 0.53x |
| `literal/sherlock-casei-en` | 79.854 us | 201.359 us | 594.484 us | 0.40x |
| `literal/sherlock-casei-ru` | 144.646 us | 370.393 us | 5,517.164 us | 0.39x |
| `bounded-repeat/letters-en` | 1,102.694 us | 2,826.893 us | 2,965.556 us | 0.39x |
| `literal/sherlock-en` | 31.852 us | 87.994 us | 478.482 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,971.989 us | 5,879.692 us | 6,746.117 us | 0.34x |
| `dictionary/single` | 8,195.910 us | 25,715.667 us | 25,902.233 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,453.038 us | 4,910.256 us | 10,437.100 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 168.129 us | 594.579 us | 1,017.476 us | 0.28x |
| `bounded-repeat/capitals` | 11,626.043 us | 46,802.800 us | 50,714.500 us | 0.25x |
| `literal-alternate/sherlock-en` | 345.207 us | 1,536.953 us | 2,068.816 us | 0.22x |
| `aws-keys/quick` | 2,037.870 us | 9,791.038 us | 46,793.850 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 128.246 us | 659.683 us | 1,092.832 us | 0.19x |
| `aws-keys/full` | 8,419.110 us | 86,847.700 us | 120,883.800 us | 0.10x |
| `bounded-repeat/context` | 9,115.122 us | 148,672.000 us | 153,642.600 us | 0.06x |
| `ruff-noqa/real` | 3,509.918 us | 70,047.450 us | 86,599.500 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 850.016 us | 22,982.450 us | 28,368.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 105.055 us | 11,137.343 us | 11,782.900 us | 0.0094x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.119 us | 0.109 us | 0.138 us | 1.10x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.089 us | 0.084 us | 0.100 us | 1.06x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.080 us | 0.077 us | 0.091 us | 1.04x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.084 us | 0.082 us | 0.098 us | 1.02x |
| `common/ip-match` | `IsMatch` | 0.166 us | 0.182 us | 0.202 us | 0.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.090 us | 0.112 us | 0.146 us | 0.80x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.129 us | 0.169 us | 0.186 us | 0.77x |
| `industry/mariomka-email-count` | `Count` | 379.286 us | 495.893 us | 8,958.200 us | 0.76x |
| `common/uri-match` | `IsMatch` | 0.163 us | 0.238 us | 0.274 us | 0.68x |
| `common/uri-miss` | `IsMatch` | 0.138 us | 0.203 us | 0.246 us | 0.68x |
| `industry/mariomka-uri-count` | `Count` | 1,221.822 us | 1,908.653 us | 10,340.900 us | 0.64x |
| `common/email-match` | `IsMatch` | 0.158 us | 0.275 us | 0.301 us | 0.57x |
| `common/ip-miss` | `IsMatch` | 0.392 us | 0.724 us | 0.744 us | 0.54x |
| `common/match-word` | `Match` | 0.067 us | 0.136 us | 1.184 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 653.523 us | 1,367.669 us | 8,811.200 us | 0.48x |
| `industry/mariomka-ip-count` | `Count` | 3,296.400 us | 8,138.120 us | 16,752.233 us | 0.41x |
| `common/date-miss` | `IsMatch` | 0.212 us | 0.563 us | 0.594 us | 0.38x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 85.170 us | 258.941 us | 486.263 us | 0.33x |
| `common/matches-set` | `Count` | 62.718 us | 194.323 us | 196.236 us | 0.32x |
| `common/matches-word` | `Count` | 1.243 us | 4.253 us | 5.961 us | 0.29x |
| `common/replace-words` | `Replace` | 21.433 us | 76.955 us | 78.598 us | 0.28x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.887 us | 973.083 us | 1,256.758 us | 0.17x |
| `common/split-words` | `Split` | 13.140 us | 79.532 us | 80.687 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.161 us | 166.055 us | 166.929 us | 0.14x |
| `common/matches-words` | `Count` | 9.233 us | 71.281 us | 73.389 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,052.567 us | 9,805.388 us | 17,106.733 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,839.632 us | 19,376.500 us | 20,026.500 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 867.354 us | 11,788.529 us | 12,515.000 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.624 us | 0.657 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,495.145 us | 32,365.733 us | 39,818.900 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.038 us | 0.838 us | 0.882 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.346 us | 0.375 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 1,010.127 us | 24,522.850 us | 32,636.200 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 281.303 us | 8,272.120 us | 8,745.100 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.662 us | 36.793 us | 2,133.414 us | 1.19x |
| `literal/sherlock-casei-en` | 80.231 us | 79.601 us | 421.913 us | 1.01x |
| `literal-alternate/sherlock-en` | 344.211 us | 343.583 us | 769.998 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,984.374 us | 2,049.186 us | 2,936.846 us | 0.97x |
| `literal/sherlock-zh` | 22.428 us | 23.999 us | 2,173.173 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 167.781 us | 182.171 us | 581.037 us | 0.92x |
| `bounded-repeat/capitals` | 11,596.271 us | 13,571.650 us | 16,508.025 us | 0.85x |
| `ruff-noqa/tweaked` | 4,106.105 us | 4,896.424 us | 41,642.900 us | 0.84x |
| `literal/sherlock-ru` | 69.118 us | 90.037 us | 5,245.820 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 106.729 us | 146.759 us | 537.994 us | 0.73x |
| `literal/sherlock-casei-ru` | 144.799 us | 223.900 us | 5,364.964 us | 0.65x |
| `literal-alternate/sherlock-en-nomatch` | 128.145 us | 203.350 us | 614.472 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,450.808 us | 2,349.838 us | 7,607.550 us | 0.62x |
| `bounded-repeat/letters-en` | 1,109.331 us | 1,838.129 us | 1,965.811 us | 0.60x |
| `aws-keys/quick` | 2,034.111 us | 3,682.838 us | 39,829.050 us | 0.55x |
| `aws-keys/full` | 8,433.580 us | 18,627.100 us | 54,142.033 us | 0.45x |
| `literal-alternate/sherlock-casei-ru` | 844.217 us | 1,982.959 us | 7,175.925 us | 0.43x |
| `literal/sherlock-en` | 31.816 us | 77.000 us | 428.736 us | 0.41x |
| `dictionary/single` | 8,165.390 us | 45,582.900 us | 45,902.450 us | 0.18x |
| `ruff-noqa/real` | 3,541.564 us | 21,436.275 us | 48,759.033 us | 0.17x |
| `bounded-repeat/context` | 9,130.522 us | 69,224.100 us | 73,407.100 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.165 us | 0.049 us | 0.069 us | 3.40x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.089 us | 0.031 us | 0.049 us | 2.87x |
| `common/date-match` | `IsMatch` | 0.118 us | 0.044 us | 0.073 us | 2.67x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.082 us | 0.031 us | 0.047 us | 2.64x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.129 us | 0.056 us | 0.077 us | 2.30x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.079 us | 0.036 us | 0.052 us | 2.19x |
| `common/ip-miss` | `IsMatch` | 0.387 us | 0.181 us | 0.205 us | 2.14x |
| `common/uri-miss` | `IsMatch` | 0.137 us | 0.067 us | 0.088 us | 2.04x |
| `common/email-match` | `IsMatch` | 0.154 us | 0.081 us | 0.108 us | 1.91x |
| `common/uri-match` | `IsMatch` | 0.163 us | 0.091 us | 0.121 us | 1.80x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.087 us | 0.049 us | 0.085 us | 1.78x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 84.863 us | 58.129 us | 268.771 us | 1.46x |
| `common/match-word` | `Match` | 0.067 us | 0.048 us | 0.953 us | 1.40x |
| `common/replace-words` | `Replace` | 21.316 us | 17.633 us | 19.253 us | 1.21x |
| `industry/mariomka-uri-count` | `Count` | 1,225.983 us | 1,088.110 us | 8,480.875 us | 1.13x |
| `common/date-miss` | `IsMatch` | 0.210 us | 0.201 us | 0.238 us | 1.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,500.908 us | 1,566.930 us | 8,266.525 us | 0.96x |
| `industry/mariomka-ip-count` | `Count` | 3,278.765 us | 3,453.670 us | 10,779.100 us | 0.95x |
| `common/matches-words` | `Count` | 9.250 us | 10.847 us | 13.954 us | 0.85x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.043 us | 0.073 us | 0.84x |
| `industry/mariomka-email-count` | `Count` | 378.572 us | 460.663 us | 7,644.400 us | 0.82x |
| `industry/leipzig-symbol-count` | `Count` | 1,010.761 us | 1,272.182 us | 7,751.020 us | 0.79x |
| `common/split-words` | `Split` | 13.154 us | 20.281 us | 22.297 us | 0.65x |
| `common/matches-set` | `Count` | 62.528 us | 105.259 us | 106.738 us | 0.59x |
| `common/matches-word` | `Count` | 1.243 us | 2.383 us | 3.770 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 655.876 us | 1,335.770 us | 6,445.525 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,050.292 us | 3,033.484 us | 8,496.900 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.756 us | 603.862 us | 890.526 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.107 us | 0.131 us | 0.27x |
| `common/matches-boundary` | `Count` | 23.310 us | 104.961 us | 106.265 us | 0.22x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,838.263 us | 8,719.111 us | 9,280.167 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 865.920 us | 4,771.262 us | 5,242.227 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.089 us | 0.116 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 285.127 us | 3,941.495 us | 4,306.450 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.643 us | 6.391 us | 31.134 us | 1.20x |
| `fallback/lookahead` | 6.750 us | 6.008 us | 19.200 us | 1.12x |
| `structural/method-family-generic-call` | 6.406 us | 6.551 us | 20.904 us | 0.98x |
| `structural/method-family-call` | 7.839 us | 8.108 us | 21.424 us | 0.97x |
| `lokad/lexer/doc-line` | 1.634 us | 1.868 us | 2.628 us | 0.87x |
| `lokad/langserv/identifier-validator` | 6.556 us | 7.505 us | 8.953 us | 0.87x |
| `lokad/lexer/identifier` | 11.798 us | 13.703 us | 16.823 us | 0.86x |
| `lokad/langserv/color-short-hex` | 0.707 us | 0.822 us | 1.007 us | 0.86x |
| `lokad/style/hex-color` | 0.425 us | 0.513 us | 0.606 us | 0.83x |
| `structural/ordered-keyword-window` | 13.125 us | 17.800 us | 31.027 us | 0.74x |
| `lokad/langserv/color-rgb-hex` | 1.361 us | 1.864 us | 2.055 us | 0.73x |
| `lokad/lexer/operator-run` | 1.715 us | 2.382 us | 2.837 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 2.526 us | 3.514 us | 4.217 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.934 us | 4.083 us | 17.940 us | 0.72x |
| `structural/ordered-keyword-pair` | 3.157 us | 5.255 us | 18.821 us | 0.60x |
| `lokad/lexer/number` | 6.339 us | 10.799 us | 11.720 us | 0.59x |
| `structural/ordered-modifier-pair` | 9.872 us | 17.579 us | 30.673 us | 0.56x |
| `lokad/langserv/helper-identifier` | 2.576 us | 4.700 us | 5.604 us | 0.55x |
| `literal/identifier-token` | 2.318 us | 4.386 us | 17.567 us | 0.53x |
| `lokad/style/cell-ref` | 0.874 us | 1.909 us | 2.159 us | 0.46x |
| `literal/call-token` | 1.850 us | 4.200 us | 17.400 us | 0.44x |
| `lokad/style/range-ref` | 0.791 us | 1.801 us | 1.950 us | 0.44x |
| `lokad/langserv/url-dashboard` | 9.913 us | 23.075 us | 23.448 us | 0.43x |
| `literal-family/type-token-family` | 9.365 us | 24.408 us | 38.388 us | 0.38x |
| `lokad/lexer/string` | 27.516 us | 82.261 us | 87.089 us | 0.33x |
| `structural/ordered-identifier-window` | 8.548 us | 28.427 us | 42.076 us | 0.30x |
| `literal-family/method-token-family` | 2.643 us | 9.453 us | 22.053 us | 0.28x |
| `literal/identifier-token-casei` | 3.944 us | 14.810 us | 28.344 us | 0.27x |
| `structural/modifier-family-to-type-window` | 66.523 us | 251.923 us | 266.060 us | 0.26x |
| `lokad/langserv/url-download` | 11.827 us | 52.061 us | 51.856 us | 0.23x |
| `lokad/folding/region-marker` | 5.193 us | 35.667 us | 61.931 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 15.893 us | 223.579 us | 237.787 us | 0.07x |
| `structural/modifier-family-to-type-pair` | 2.934 us | 132.755 us | 147.189 us | 0.02x |
| `balancing/begin-end-blocks` | 47.532 us | 4,125.032 us | 4,088.742 us | 0.01x |
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
| `structural/keyword-family-to-capitalized-identifier` | 15.979 us | 7.700 us | 21.462 us | 2.08x |
| `lokad/lexer/operator-run` | 1.722 us | 0.928 us | 1.388 us | 1.86x |
| `lokad/style/hex-color` | 0.425 us | 0.229 us | 0.310 us | 1.85x |
| `lokad/imports/module-imports` | 7.738 us | 4.418 us | 28.410 us | 1.75x |
| `lokad/docs/inline-doc-prefix` | 2.539 us | 1.456 us | 2.215 us | 1.74x |
| `lokad/lexer/number` | 6.266 us | 3.778 us | 4.890 us | 1.66x |
| `fallback/lookahead` | 6.761 us | 4.188 us | 17.036 us | 1.61x |
| `lokad/langserv/color-rgb-hex` | 1.365 us | 0.846 us | 1.100 us | 1.61x |
| `lokad/langserv/url-dashboard` | 9.862 us | 6.261 us | 6.689 us | 1.58x |
| `structural/ordered-keyword-window` | 13.064 us | 8.471 us | 21.411 us | 1.54x |
| `structural/method-family-call` | 7.829 us | 5.089 us | 18.023 us | 1.54x |
| `lokad/lexer/doc-line` | 1.631 us | 1.067 us | 1.840 us | 1.53x |
| `lokad/lexer/identifier` | 11.890 us | 7.838 us | 10.746 us | 1.52x |
| `lokad/langserv/identifier-validator` | 6.522 us | 4.302 us | 5.701 us | 1.52x |
| `lokad/langserv/color-short-hex` | 0.717 us | 0.492 us | 0.679 us | 1.46x |
| `structural/method-family-generic-call` | 6.372 us | 4.598 us | 17.915 us | 1.39x |
| `structural/modifier-family-to-type-window` | 66.489 us | 55.147 us | 69.254 us | 1.21x |
| `structural/ordered-modifier-pair` | 9.879 us | 8.575 us | 21.637 us | 1.15x |
| `lokad/style/range-ref` | 0.804 us | 0.723 us | 0.871 us | 1.11x |
| `lokad/style/cell-ref` | 0.876 us | 0.835 us | 1.049 us | 1.05x |
| `lokad/langserv/url-download` | 11.841 us | 12.179 us | 12.553 us | 0.97x |
| `lokad/langserv/helper-identifier` | 2.573 us | 2.668 us | 3.640 us | 0.96x |
| `lokad/lexer/string` | 27.562 us | 31.088 us | 33.585 us | 0.89x |
| `structural/keyword-to-generic-type` | 2.951 us | 3.515 us | 16.813 us | 0.84x |
| `structural/ordered-identifier-window` | 8.546 us | 10.566 us | 24.361 us | 0.81x |
| `literal-family/type-token-family` | 9.315 us | 11.605 us | 24.823 us | 0.80x |
| `structural/ordered-keyword-pair` | 3.161 us | 4.139 us | 16.978 us | 0.76x |
| `literal/identifier-token-casei` | 3.961 us | 5.987 us | 19.309 us | 0.66x |
| `literal-family/method-token-family` | 2.649 us | 5.151 us | 17.930 us | 0.51x |
| `literal/identifier-token` | 2.319 us | 4.773 us | 17.036 us | 0.49x |
| `literal/call-token` | 1.854 us | 3.927 us | 16.489 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.933 us | 10.816 us | 23.843 us | 0.27x |
| `lokad/folding/region-marker` | 5.083 us | 36.145 us | 61.792 us | 0.14x |
| `balancing/begin-end-blocks` | 48.387 us | 468.226 us | 501.685 us | 0.10x |
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
