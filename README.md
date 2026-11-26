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
| `literal/sherlock-zh` | 14.071 us | 18.828 us | 2,211.178 us | 0.75x |
| `literal/sherlock-ru` | 68.393 us | 99.458 us | 5,210.050 us | 0.69x |
| `literal-alternate/sherlock-zh` | 44.077 us | 68.560 us | 2,245.771 us | 0.64x |
| `ruff-noqa/tweaked` | 4,012.979 us | 7,709.182 us | 44,552.500 us | 0.52x |
| `literal/sherlock-casei-en` | 80.355 us | 191.218 us | 670.235 us | 0.42x |
| `literal/sherlock-casei-ru` | 150.530 us | 361.269 us | 5,715.638 us | 0.42x |
| `bounded-repeat/letters-en` | 1,115.278 us | 2,824.226 us | 2,994.912 us | 0.39x |
| `literal/sherlock-en` | 31.500 us | 86.635 us | 461.092 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,065.024 us | 5,860.546 us | 6,720.175 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,446.345 us | 4,767.269 us | 9,920.575 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 178.424 us | 597.216 us | 1,218.819 us | 0.30x |
| `dictionary/single` | 7,601.140 us | 26,415.067 us | 26,443.000 us | 0.29x |
| `bounded-repeat/capitals` | 11,854.729 us | 46,871.900 us | 50,636.700 us | 0.25x |
| `literal-alternate/sherlock-en` | 351.692 us | 1,506.242 us | 2,010.728 us | 0.23x |
| `aws-keys/quick` | 2,014.492 us | 9,549.733 us | 43,504.400 us | 0.21x |
| `literal-alternate/sherlock-en-nomatch` | 128.197 us | 645.151 us | 1,192.852 us | 0.20x |
| `aws-keys/full` | 8,851.644 us | 87,992.600 us | 118,876.000 us | 0.10x |
| `bounded-repeat/context` | 9,123.178 us | 148,838.400 us | 152,097.800 us | 0.06x |
| `ruff-noqa/real` | 3,498.341 us | 70,220.650 us | 85,914.600 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 860.098 us | 23,136.000 us | 28,498.700 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 106.068 us | 11,269.329 us | 12,171.200 us | 0.0094x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.085 us | 0.112 us | 0.150 us | 0.76x |
| `common/date-match` | `IsMatch` | 0.072 us | 0.109 us | 0.136 us | 0.67x |
| `common/ip-match` | `IsMatch` | 0.112 us | 0.174 us | 0.196 us | 0.65x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.051 us | 0.079 us | 0.094 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,214.508 us | 1,924.629 us | 10,585.633 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.085 us | 0.100 us | 0.61x |
| `industry/mariomka-email-count` | `Count` | 278.170 us | 507.630 us | 9,131.775 us | 0.55x |
| `common/email-match` | `IsMatch` | 0.142 us | 0.264 us | 0.292 us | 0.54x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.082 us | 0.098 us | 0.53x |
| `common/uri-miss` | `IsMatch` | 0.092 us | 0.185 us | 0.205 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.083 us | 0.170 us | 0.187 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 633.690 us | 1,325.787 us | 8,729.100 us | 0.48x |
| `common/match-word` | `Match` | 0.052 us | 0.129 us | 1.197 us | 0.40x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.228 us | 0.263 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.276 us | 0.731 us | 0.756 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,139.767 us | 8,398.456 us | 17,106.700 us | 0.37x |
| `common/matches-set` | `Count` | 63.506 us | 193.574 us | 196.436 us | 0.33x |
| `common/matches-word` | `Count` | 1.243 us | 4.223 us | 5.982 us | 0.29x |
| `common/replace-words` | `Replace` | 22.229 us | 77.776 us | 80.916 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.658 us | 261.530 us | 503.151 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.564 us | 0.606 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 170.069 us | 974.095 us | 1,291.127 us | 0.17x |
| `common/matches-boundary` | `Count` | 27.979 us | 166.824 us | 169.239 us | 0.17x |
| `common/split-words` | `Split` | 13.100 us | 78.439 us | 79.869 us | 0.17x |
| `common/matches-words` | `Count` | 9.378 us | 71.304 us | 73.464 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,058.489 us | 9,917.850 us | 17,080.500 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,943.590 us | 19,444.900 us | 20,104.875 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.034 us | 0.620 us | 0.656 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.347 us | 0.368 us | 0.05x |
| `industry/leipzig-river-window-count` | `Count` | 1,654.378 us | 32,553.967 us | 39,954.250 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,027.562 us | 23,423.450 us | 31,224.800 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.029 us | 0.833 us | 0.876 us | 0.03x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 240.531 us | 8,286.200 us | 8,700.489 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 291.501 us | 11,793.657 us | 12,239.783 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.991 us | 36.046 us | 2,212.453 us | 1.22x |
| `literal/sherlock-casei-en` | 80.304 us | 79.743 us | 445.408 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,065.878 us | 2,062.686 us | 2,918.400 us | 1.00x |
| `literal-alternate/sherlock-en-mixed` | 176.680 us | 180.965 us | 652.490 us | 0.98x |
| `literal-alternate/sherlock-en` | 352.309 us | 373.297 us | 792.093 us | 0.94x |
| `ruff-noqa/tweaked` | 4,010.068 us | 4,563.576 us | 39,650.050 us | 0.88x |
| `bounded-repeat/capitals` | 11,875.971 us | 13,558.650 us | 16,911.750 us | 0.88x |
| `literal/sherlock-casei-ru` | 153.366 us | 195.674 us | 5,441.407 us | 0.78x |
| `literal/sherlock-ru` | 68.410 us | 90.801 us | 5,229.493 us | 0.75x |
| `literal-alternate/sherlock-casei-en` | 106.366 us | 146.786 us | 712.390 us | 0.72x |
| `literal-alternate/sherlock-en-nomatch` | 128.419 us | 206.333 us | 645.643 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,438.836 us | 2,359.722 us | 7,444.875 us | 0.61x |
| `bounded-repeat/letters-en` | 1,107.385 us | 1,838.048 us | 1,972.485 us | 0.60x |
| `literal/sherlock-zh` | 14.089 us | 24.053 us | 2,225.958 us | 0.59x |
| `aws-keys/quick` | 2,011.521 us | 3,692.271 us | 35,660.050 us | 0.54x |
| `aws-keys/full` | 8,893.178 us | 18,681.080 us | 51,197.367 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 866.278 us | 1,976.450 us | 7,207.825 us | 0.44x |
| `literal/sherlock-en` | 31.352 us | 75.518 us | 411.325 us | 0.42x |
| `ruff-noqa/real` | 3,567.786 us | 21,235.725 us | 35,373.667 us | 0.17x |
| `dictionary/single` | 7,599.260 us | 45,958.550 us | 46,045.050 us | 0.17x |
| `bounded-repeat/context` | 9,107.789 us | 69,553.650 us | 72,897.750 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.108 us | 0.048 us | 0.069 us | 2.24x |
| `common/date-match` | `IsMatch` | 0.072 us | 0.042 us | 0.073 us | 1.73x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.033 us | 0.050 us | 1.46x |
| `common/ip-miss` | `IsMatch` | 0.263 us | 0.182 us | 0.204 us | 1.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.057 us | 0.078 us | 1.40x |
| `common/uri-miss` | `IsMatch` | 0.092 us | 0.067 us | 0.086 us | 1.37x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.041 us | 0.032 us | 0.047 us | 1.28x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.036 us | 0.052 us | 1.27x |
| `common/replace-words` | `Replace` | 22.291 us | 17.943 us | 19.411 us | 1.24x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.773 us | 58.117 us | 280.788 us | 1.17x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.054 us | 0.049 us | 0.089 us | 1.10x |
| `industry/leipzig-river-window-count` | `Count` | 1,668.915 us | 1,525.706 us | 8,534.660 us | 1.09x |
| `common/match-word` | `Match` | 0.053 us | 0.048 us | 0.970 us | 1.09x |
| `industry/mariomka-uri-count` | `Count` | 1,218.160 us | 1,121.684 us | 8,444.433 us | 1.09x |
| `common/uri-match` | `IsMatch` | 0.088 us | 0.091 us | 0.122 us | 0.97x |
| `common/email-match` | `IsMatch` | 0.074 us | 0.080 us | 0.108 us | 0.93x |
| `industry/mariomka-ip-count` | `Count` | 3,157.604 us | 3,486.977 us | 11,044.467 us | 0.91x |
| `common/matches-words` | `Count` | 9.318 us | 11.029 us | 14.124 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,031.755 us | 1,251.344 us | 7,666.220 us | 0.82x |
| `common/backtracking` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.65x |
| `common/split-words` | `Split` | 13.369 us | 20.949 us | 22.791 us | 0.64x |
| `common/matches-set` | `Count` | 62.922 us | 105.509 us | 106.744 us | 0.60x |
| `industry/mariomka-email-count` | `Count` | 279.142 us | 472.172 us | 7,855.225 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.112 us | 0.201 us | 0.235 us | 0.56x |
| `common/matches-word` | `Count` | 1.242 us | 2.363 us | 3.779 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 642.403 us | 1,341.712 us | 7,572.133 us | 0.48x |
| `industry/leipzig-name-family-count` | `Count` | 1,053.506 us | 3,021.281 us | 8,493.275 us | 0.35x |
| `common/one-node-backtracking` | `IsMatch` | 0.033 us | 0.112 us | 0.129 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.260 us | 593.093 us | 872.131 us | 0.29x |
| `common/matches-boundary` | `Count` | 28.124 us | 105.254 us | 106.584 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,938.792 us | 8,851.100 us | 9,396.475 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.089 us | 0.114 us | 0.20x |
| `industry/rust-sherlock-ing-count` | `Count` | 291.005 us | 4,788.125 us | 5,219.157 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 239.906 us | 3,996.900 us | 4,388.183 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.221 us | 6.497 us | 33.279 us | 0.96x |
| `fallback/lookahead` | 5.355 us | 6.082 us | 20.315 us | 0.88x |
| `lokad/langserv/color-short-hex` | 0.600 us | 0.830 us | 1.008 us | 0.72x |
| `structural/ordered-keyword-window` | 12.066 us | 17.903 us | 32.185 us | 0.67x |
| `structural/method-family-generic-call` | 4.485 us | 6.673 us | 21.537 us | 0.67x |
| `lokad/lexer/identifier` | 8.976 us | 13.663 us | 16.700 us | 0.66x |
| `structural/method-family-call` | 5.302 us | 8.109 us | 23.031 us | 0.65x |
| `lokad/langserv/identifier-validator` | 4.775 us | 7.445 us | 9.115 us | 0.64x |
| `structural/ordered-modifier-pair` | 10.474 us | 17.273 us | 31.774 us | 0.61x |
| `lokad/lexer/doc-line` | 1.142 us | 1.891 us | 2.628 us | 0.60x |
| `lokad/docs/inline-doc-prefix` | 2.024 us | 3.524 us | 4.318 us | 0.57x |
| `structural/keyword-to-generic-type` | 2.282 us | 4.159 us | 18.982 us | 0.55x |
| `literal/identifier-token` | 2.269 us | 4.301 us | 18.925 us | 0.53x |
| `lokad/style/hex-color` | 0.268 us | 0.509 us | 0.601 us | 0.53x |
| `lokad/langserv/helper-identifier` | 2.463 us | 4.775 us | 5.649 us | 0.52x |
| `lokad/lexer/operator-run` | 1.190 us | 2.347 us | 2.789 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.584 us | 5.440 us | 20.201 us | 0.48x |
| `lokad/style/cell-ref` | 0.863 us | 1.940 us | 2.140 us | 0.45x |
| `literal/call-token` | 1.862 us | 4.197 us | 18.566 us | 0.44x |
| `lokad/style/range-ref` | 0.788 us | 1.832 us | 1.971 us | 0.43x |
| `lokad/lexer/string` | 33.057 us | 82.355 us | 86.645 us | 0.40x |
| `literal-family/type-token-family` | 9.254 us | 23.565 us | 38.799 us | 0.39x |
| `lokad/langserv/color-rgb-hex` | 0.696 us | 1.860 us | 2.087 us | 0.37x |
| `lokad/langserv/url-dashboard` | 8.688 us | 23.385 us | 23.846 us | 0.37x |
| `lokad/lexer/number` | 3.201 us | 10.840 us | 11.751 us | 0.30x |
| `structural/ordered-identifier-window` | 8.350 us | 28.891 us | 43.587 us | 0.29x |
| `literal-family/method-token-family` | 2.632 us | 9.488 us | 23.745 us | 0.28x |
| `literal/identifier-token-casei` | 3.955 us | 15.137 us | 29.821 us | 0.26x |
| `structural/modifier-family-to-type-window` | 59.781 us | 254.114 us | 271.515 us | 0.24x |
| `lokad/langserv/url-download` | 11.028 us | 51.268 us | 51.994 us | 0.22x |
| `lokad/folding/region-marker` | 5.807 us | 35.792 us | 64.303 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.885 us | 217.215 us | 231.989 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.961 us | 129.982 us | 144.953 us | 0.02x |
| `balancing/begin-end-blocks` | 84.302 us | 4,375.933 us | 4,377.244 us | 0.02x |
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
| `lokad/docs/inline-doc-prefix` | 2.026 us | 1.441 us | 2.196 us | 1.41x |
| `lokad/imports/module-imports` | 6.239 us | 4.457 us | 30.038 us | 1.40x |
| `structural/ordered-keyword-window` | 12.068 us | 8.667 us | 22.639 us | 1.39x |
| `lokad/langserv/url-dashboard` | 8.685 us | 6.331 us | 6.872 us | 1.37x |
| `structural/keyword-family-to-capitalized-identifier` | 10.147 us | 7.730 us | 22.647 us | 1.31x |
| `lokad/lexer/operator-run` | 1.168 us | 0.924 us | 1.410 us | 1.26x |
| `fallback/lookahead` | 5.343 us | 4.231 us | 18.060 us | 1.26x |
| `structural/ordered-modifier-pair` | 10.407 us | 8.626 us | 22.885 us | 1.21x |
| `lokad/lexer/identifier` | 9.062 us | 8.018 us | 10.742 us | 1.13x |
| `lokad/langserv/identifier-validator` | 4.762 us | 4.326 us | 5.855 us | 1.10x |
| `lokad/lexer/doc-line` | 1.142 us | 1.056 us | 1.851 us | 1.08x |
| `lokad/style/range-ref` | 0.792 us | 0.739 us | 0.876 us | 1.07x |
| `lokad/lexer/string` | 33.035 us | 30.879 us | 33.590 us | 1.07x |
| `structural/modifier-family-to-type-window` | 59.490 us | 55.904 us | 72.004 us | 1.06x |
| `structural/method-family-call` | 5.343 us | 5.064 us | 19.516 us | 1.05x |
| `lokad/style/cell-ref` | 0.868 us | 0.841 us | 1.055 us | 1.03x |
| `lokad/langserv/color-short-hex` | 0.510 us | 0.504 us | 0.681 us | 1.01x |
| `structural/method-family-generic-call` | 4.477 us | 4.618 us | 18.590 us | 0.97x |
| `lokad/style/hex-color` | 0.217 us | 0.232 us | 0.316 us | 0.93x |
| `lokad/langserv/url-download` | 11.096 us | 12.175 us | 12.797 us | 0.91x |
| `lokad/langserv/helper-identifier` | 2.459 us | 2.724 us | 3.599 us | 0.90x |
| `lokad/lexer/number` | 3.161 us | 3.812 us | 4.912 us | 0.83x |
| `literal-family/type-token-family` | 9.271 us | 11.513 us | 25.894 us | 0.81x |
| `structural/ordered-identifier-window` | 8.385 us | 10.508 us | 25.257 us | 0.80x |
| `lokad/langserv/color-rgb-hex` | 0.650 us | 0.850 us | 1.109 us | 0.76x |
| `literal/identifier-token-casei` | 3.967 us | 5.956 us | 20.533 us | 0.67x |
| `structural/keyword-to-generic-type` | 2.286 us | 3.522 us | 17.645 us | 0.65x |
| `structural/ordered-keyword-pair` | 2.565 us | 4.163 us | 18.181 us | 0.62x |
| `literal/identifier-token` | 2.267 us | 4.332 us | 18.537 us | 0.52x |
| `literal-family/method-token-family` | 2.647 us | 5.166 us | 19.378 us | 0.51x |
| `literal/call-token` | 1.868 us | 3.973 us | 17.643 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.966 us | 10.826 us | 25.537 us | 0.27x |
| `balancing/begin-end-blocks` | 84.205 us | 467.862 us | 499.855 us | 0.18x |
| `lokad/folding/region-marker` | 5.828 us | 35.798 us | 63.654 us | 0.16x |
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
