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
| `literal/sherlock-zh` | 22.166 us | 18.715 us | 2,161.211 us | 1.18x |
| `literal/sherlock-ru` | 68.522 us | 94.790 us | 5,146.747 us | 0.72x |
| `literal-alternate/sherlock-zh` | 43.262 us | 67.195 us | 2,172.585 us | 0.64x |
| `ruff-noqa/tweaked` | 4,163.189 us | 7,741.100 us | 41,474.250 us | 0.54x |
| `literal/sherlock-casei-ru` | 154.181 us | 363.315 us | 5,490.071 us | 0.42x |
| `bounded-repeat/letters-en` | 1,186.756 us | 2,821.637 us | 2,996.248 us | 0.42x |
| `literal/sherlock-en` | 31.363 us | 85.450 us | 508.875 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,126.686 us | 5,844.531 us | 6,662.842 us | 0.36x |
| `literal/sherlock-casei-en` | 69.279 us | 197.989 us | 617.822 us | 0.35x |
| `dictionary/single` | 8,142.170 us | 25,845.000 us | 25,995.800 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,438.006 us | 4,699.863 us | 9,834.725 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 156.603 us | 610.866 us | 1,065.993 us | 0.26x |
| `bounded-repeat/capitals` | 11,633.257 us | 46,136.200 us | 49,556.250 us | 0.25x |
| `literal-alternate/sherlock-en` | 341.254 us | 1,464.409 us | 2,043.050 us | 0.23x |
| `aws-keys/quick` | 1,883.588 us | 9,468.625 us | 41,194.200 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 127.557 us | 643.896 us | 1,064.799 us | 0.20x |
| `aws-keys/full` | 8,868.078 us | 86,004.900 us | 116,308.400 us | 0.10x |
| `bounded-repeat/context` | 8,796.178 us | 147,997.200 us | 152,326.500 us | 0.06x |
| `ruff-noqa/real` | 3,562.950 us | 70,799.250 us | 86,982.700 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 867.621 us | 22,921.900 us | 28,226.650 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 108.251 us | 11,080.029 us | 11,991.243 us | 0.0098x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 379.668 us | 492.332 us | 9,023.650 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.109 us | 0.146 us | 0.74x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.061 us | 0.084 us | 0.101 us | 0.73x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.052 us | 0.076 us | 0.091 us | 0.68x |
| `industry/mariomka-uri-count` | `Count` | 1,181.597 us | 1,868.493 us | 10,500.100 us | 0.63x |
| `common/date-match` | `IsMatch` | 0.060 us | 0.109 us | 0.139 us | 0.55x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.083 us | 0.099 us | 0.53x |
| `common/email-match` | `IsMatch` | 0.137 us | 0.274 us | 0.303 us | 0.50x |
| `industry/leipzig-twain-count` | `Count` | 656.227 us | 1,375.426 us | 8,734.440 us | 0.48x |
| `common/uri-miss` | `IsMatch` | 0.081 us | 0.190 us | 0.209 us | 0.43x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.175 us | 0.195 us | 0.39x |
| `common/match-word` | `Match` | 0.050 us | 0.128 us | 1.158 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,254.175 us | 8,314.070 us | 16,808.367 us | 0.39x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.063 us | 0.169 us | 0.186 us | 0.37x |
| `common/matches-set` | `Count` | 68.098 us | 194.453 us | 196.647 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.077 us | 0.237 us | 0.272 us | 0.33x |
| `common/matches-word` | `Count` | 1.249 us | 4.284 us | 5.934 us | 0.29x |
| `common/ip-miss` | `IsMatch` | 0.209 us | 0.723 us | 0.743 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 73.420 us | 266.822 us | 509.957 us | 0.28x |
| `common/replace-words` | `Replace` | 20.310 us | 77.694 us | 78.958 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.102 us | 0.562 us | 0.596 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 169.736 us | 963.321 us | 1,265.740 us | 0.18x |
| `common/split-words` | `Split` | 13.229 us | 78.644 us | 80.415 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.696 us | 167.173 us | 169.925 us | 0.17x |
| `common/matches-words` | `Count` | 9.184 us | 71.632 us | 73.966 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,062.817 us | 9,853.362 us | 17,289.133 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,925.615 us | 19,517.225 us | 20,145.450 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 874.783 us | 12,131.943 us | 12,710.133 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.610 us | 0.641 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.016 us | 0.346 us | 0.371 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.827 us | 0.866 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,025.216 us | 23,812.925 us | 31,869.750 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,248.893 us | 32,124.500 us | 39,705.800 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 265.768 us | 8,195.620 us | 8,709.833 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 43.378 us | 35.653 us | 2,129.906 us | 1.22x |
| `bounded-repeat/letters-ru` | 2,126.453 us | 2,053.508 us | 2,896.392 us | 1.04x |
| `literal/sherlock-zh` | 22.374 us | 23.961 us | 2,162.697 us | 0.93x |
| `literal-alternate/sherlock-en` | 336.729 us | 364.340 us | 781.115 us | 0.92x |
| `literal-alternate/sherlock-en-mixed` | 157.546 us | 173.238 us | 580.419 us | 0.91x |
| `ruff-noqa/tweaked` | 4,188.850 us | 4,610.000 us | 36,838.050 us | 0.91x |
| `literal/sherlock-casei-en` | 69.149 us | 77.501 us | 440.930 us | 0.89x |
| `bounded-repeat/capitals` | 11,640.671 us | 13,556.567 us | 16,382.025 us | 0.86x |
| `literal/sherlock-ru` | 68.386 us | 86.528 us | 5,158.880 us | 0.79x |
| `literal-alternate/sherlock-casei-en` | 108.884 us | 143.804 us | 568.461 us | 0.76x |
| `literal/sherlock-casei-ru` | 153.902 us | 220.396 us | 5,294.847 us | 0.70x |
| `bounded-repeat/letters-en` | 1,183.948 us | 1,840.024 us | 1,972.916 us | 0.64x |
| `literal-alternate/sherlock-en-nomatch` | 127.292 us | 199.097 us | 583.896 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,441.810 us | 2,327.994 us | 7,427.500 us | 0.62x |
| `aws-keys/quick` | 1,881.763 us | 3,661.681 us | 36,268.433 us | 0.51x |
| `aws-keys/full` | 8,881.433 us | 18,374.580 us | 51,058.667 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 861.006 us | 1,929.695 us | 7,071.200 us | 0.45x |
| `literal/sherlock-en` | 31.358 us | 75.223 us | 424.330 us | 0.42x |
| `dictionary/single` | 8,093.840 us | 45,631.400 us | 45,934.200 us | 0.18x |
| `ruff-noqa/real` | 3,475.864 us | 21,287.350 us | 50,074.867 us | 0.16x |
| `bounded-repeat/context` | 8,795.389 us | 68,467.350 us | 71,791.750 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-date-match` | `IsMatch` | 0.057 us | 0.031 us | 0.050 us | 1.82x |
| `common/email-match` | `IsMatch` | 0.137 us | 0.081 us | 0.109 us | 1.70x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.049 us | 0.087 us | 1.65x |
| `common/ip-match` | `IsMatch` | 0.066 us | 0.047 us | 0.068 us | 1.39x |
| `common/date-match` | `IsMatch` | 0.060 us | 0.044 us | 0.073 us | 1.35x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 73.849 us | 59.227 us | 262.372 us | 1.25x |
| `common/uri-miss` | `IsMatch` | 0.080 us | 0.065 us | 0.087 us | 1.23x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.044 us | 0.035 us | 0.053 us | 1.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.038 us | 0.031 us | 0.048 us | 1.22x |
| `common/replace-words` | `Replace` | 20.286 us | 17.524 us | 18.723 us | 1.16x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.062 us | 0.055 us | 0.077 us | 1.13x |
| `industry/mariomka-uri-count` | `Count` | 1,191.195 us | 1,104.324 us | 8,556.467 us | 1.08x |
| `common/ip-miss` | `IsMatch` | 0.196 us | 0.182 us | 0.204 us | 1.08x |
| `common/match-word` | `Match` | 0.050 us | 0.048 us | 0.949 us | 1.04x |
| `industry/mariomka-ip-count` | `Count` | 3,259.404 us | 3,442.450 us | 10,896.133 us | 0.95x |
| `common/uri-match` | `IsMatch` | 0.077 us | 0.087 us | 0.120 us | 0.89x |
| `industry/mariomka-email-count` | `Count` | 381.028 us | 454.726 us | 7,751.925 us | 0.84x |
| `common/matches-words` | `Count` | 9.205 us | 11.147 us | 14.312 us | 0.83x |
| `industry/leipzig-symbol-count` | `Count` | 1,027.749 us | 1,257.231 us | 7,515.825 us | 0.82x |
| `industry/leipzig-river-window-count` | `Count` | 1,253.232 us | 1,544.040 us | 8,170.320 us | 0.81x |
| `common/matches-set` | `Count` | 68.077 us | 104.923 us | 106.580 us | 0.65x |
| `common/split-words` | `Split` | 13.274 us | 20.890 us | 22.716 us | 0.64x |
| `common/backtracking` | `IsMatch` | 0.037 us | 0.062 us | 0.088 us | 0.59x |
| `common/matches-word` | `Count` | 1.243 us | 2.348 us | 3.753 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.101 us | 0.203 us | 0.237 us | 0.50x |
| `industry/leipzig-twain-count` | `Count` | 655.676 us | 1,336.851 us | 7,713.689 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,062.473 us | 3,055.976 us | 8,435.350 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.031 us | 0.108 us | 0.129 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.841 us | 618.532 us | 884.133 us | 0.27x |
| `common/matches-boundary` | `Count` | 27.692 us | 105.366 us | 107.256 us | 0.26x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,937.903 us | 8,821.656 us | 9,432.612 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.017 us | 0.089 us | 0.116 us | 0.19x |
| `industry/rust-sherlock-ing-count` | `Count` | 869.599 us | 4,803.562 us | 5,251.953 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 264.877 us | 3,963.163 us | 4,320.944 us | 0.07x |
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
| `lokad/imports/module-imports` | 7.958 us | 6.358 us | 31.573 us | 1.25x |
| `fallback/lookahead` | 6.899 us | 6.010 us | 19.369 us | 1.15x |
| `structural/method-family-call` | 8.723 us | 8.225 us | 21.630 us | 1.06x |
| `structural/method-family-generic-call` | 6.552 us | 6.585 us | 20.473 us | 0.99x |
| `lokad/lexer/doc-line` | 1.631 us | 1.874 us | 2.647 us | 0.87x |
| `lokad/langserv/identifier-validator` | 6.398 us | 7.591 us | 8.955 us | 0.84x |
| `lokad/lexer/identifier` | 11.425 us | 13.639 us | 16.744 us | 0.84x |
| `structural/ordered-keyword-window` | 13.508 us | 17.843 us | 31.221 us | 0.76x |
| `structural/keyword-to-generic-type` | 3.060 us | 4.117 us | 17.755 us | 0.74x |
| `lokad/docs/inline-doc-prefix` | 2.493 us | 3.463 us | 4.282 us | 0.72x |
| `lokad/langserv/helper-identifier` | 3.324 us | 4.873 us | 5.751 us | 0.68x |
| `lokad/langserv/color-short-hex` | 0.537 us | 0.806 us | 1.003 us | 0.67x |
| `structural/ordered-keyword-pair` | 3.213 us | 5.305 us | 18.809 us | 0.61x |
| `structural/ordered-modifier-pair` | 10.557 us | 17.613 us | 30.597 us | 0.60x |
| `literal/identifier-token` | 2.353 us | 4.396 us | 17.456 us | 0.54x |
| `lokad/lexer/operator-run` | 1.167 us | 2.349 us | 2.801 us | 0.50x |
| `lokad/style/range-ref` | 0.816 us | 1.824 us | 2.032 us | 0.45x |
| `lokad/style/hex-color` | 0.229 us | 0.516 us | 0.606 us | 0.44x |
| `literal/call-token` | 1.859 us | 4.196 us | 17.262 us | 0.44x |
| `lokad/langserv/url-dashboard` | 10.108 us | 23.290 us | 23.740 us | 0.43x |
| `lokad/style/cell-ref` | 0.834 us | 1.947 us | 2.153 us | 0.43x |
| `literal-family/type-token-family` | 9.307 us | 23.814 us | 37.371 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.694 us | 1.834 us | 2.056 us | 0.38x |
| `lokad/lexer/string` | 27.702 us | 82.389 us | 87.239 us | 0.34x |
| `structural/ordered-identifier-window` | 8.581 us | 28.900 us | 42.160 us | 0.30x |
| `literal-family/method-token-family` | 2.649 us | 9.491 us | 22.210 us | 0.28x |
| `structural/modifier-family-to-type-window` | 68.826 us | 255.220 us | 269.331 us | 0.27x |
| `lokad/lexer/number` | 3.092 us | 11.590 us | 12.059 us | 0.27x |
| `literal/identifier-token-casei` | 3.946 us | 14.955 us | 28.844 us | 0.26x |
| `lokad/langserv/url-download` | 11.786 us | 50.964 us | 51.153 us | 0.23x |
| `lokad/folding/region-marker` | 5.238 us | 35.419 us | 62.139 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 11.088 us | 228.898 us | 236.361 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.935 us | 135.338 us | 149.963 us | 0.02x |
| `balancing/begin-end-blocks` | 46.752 us | 4,264.706 us | 4,306.983 us | 0.01x |
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
| `lokad/imports/module-imports` | 7.968 us | 4.429 us | 28.615 us | 1.80x |
| `structural/method-family-call` | 9.049 us | 5.079 us | 18.091 us | 1.78x |
| `lokad/docs/inline-doc-prefix` | 2.494 us | 1.444 us | 2.202 us | 1.73x |
| `fallback/lookahead` | 6.916 us | 4.203 us | 17.190 us | 1.65x |
| `structural/ordered-keyword-window` | 13.538 us | 8.540 us | 21.664 us | 1.59x |
| `lokad/langserv/url-dashboard` | 10.030 us | 6.330 us | 6.970 us | 1.58x |
| `lokad/lexer/doc-line` | 1.631 us | 1.069 us | 1.889 us | 1.53x |
| `lokad/langserv/identifier-validator` | 6.334 us | 4.274 us | 5.845 us | 1.48x |
| `lokad/lexer/identifier` | 11.503 us | 7.875 us | 10.759 us | 1.46x |
| `structural/method-family-generic-call` | 6.511 us | 4.615 us | 17.541 us | 1.41x |
| `structural/keyword-family-to-capitalized-identifier` | 11.064 us | 7.987 us | 21.182 us | 1.39x |
| `structural/modifier-family-to-type-window` | 68.436 us | 55.905 us | 70.785 us | 1.22x |
| `structural/ordered-modifier-pair` | 10.540 us | 8.649 us | 21.724 us | 1.22x |
| `lokad/lexer/operator-run` | 1.166 us | 0.966 us | 1.409 us | 1.21x |
| `lokad/langserv/helper-identifier` | 3.317 us | 2.751 us | 3.672 us | 1.21x |
| `lokad/style/range-ref` | 0.816 us | 0.732 us | 0.865 us | 1.11x |
| `lokad/langserv/color-short-hex` | 0.496 us | 0.489 us | 0.677 us | 1.01x |
| `lokad/style/cell-ref` | 0.837 us | 0.832 us | 1.051 us | 1.00x |
| `lokad/langserv/url-download` | 11.834 us | 12.247 us | 12.673 us | 0.97x |
| `lokad/lexer/string` | 27.715 us | 30.472 us | 33.662 us | 0.91x |
| `lokad/style/hex-color` | 0.210 us | 0.236 us | 0.317 us | 0.89x |
| `structural/keyword-to-generic-type` | 3.064 us | 3.523 us | 16.627 us | 0.87x |
| `literal-family/type-token-family` | 9.302 us | 11.546 us | 24.681 us | 0.81x |
| `structural/ordered-identifier-window` | 8.607 us | 10.703 us | 24.013 us | 0.80x |
| `lokad/lexer/number` | 3.046 us | 3.864 us | 4.934 us | 0.79x |
| `structural/ordered-keyword-pair` | 3.208 us | 4.139 us | 16.880 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 0.623 us | 0.846 us | 1.112 us | 0.74x |
| `literal/identifier-token-casei` | 3.944 us | 6.011 us | 19.186 us | 0.66x |
| `literal-family/method-token-family` | 2.651 us | 5.141 us | 17.914 us | 0.52x |
| `literal/identifier-token` | 2.350 us | 4.713 us | 17.152 us | 0.50x |
| `literal/call-token` | 1.845 us | 3.963 us | 16.246 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.934 us | 10.856 us | 23.734 us | 0.27x |
| `lokad/folding/region-marker` | 5.155 us | 36.428 us | 62.109 us | 0.14x |
| `balancing/begin-end-blocks` | 46.330 us | 463.084 us | 498.014 us | 0.10x |
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
