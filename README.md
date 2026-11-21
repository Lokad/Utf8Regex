# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes ongoing 0.5.0 development. Its API and execution machinery
are being simplified. Published 0.4.0 and frozen development packages retain
their original behavior; development package qualification is not a release.

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
`ReadOnlyMemory<byte>` for groups and capture history. Match byte ranges can be used directly with UTF-8
slices. `EnumerateMatches(input, startOffsetInBytes, cancellationToken)` searches
from a scalar-aligned byte position while retaining full context for assertions.
The byte-start `Match` can fill caller-owned `Utf8CaptureRange` slots; slot zero
is the whole match and `GroupNumberFromCaptureSlot` resolves sparse group numbers.
Use the persistent memory cursor when capture history is required.
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

See [release notes](CHANGELOG.md) and the [migration guide](docs/MIGRATION.md)
for the 0.4.0 changes.

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
| `literal/sherlock-zh` | 21.306 us | 18.035 us | 2,108.722 us | 1.18x |
| `aws-keys/full` | 65,226.400 us | 81,472.800 us | 114,067.400 us | 0.80x |
| `literal/sherlock-ru` | 65.912 us | 94.818 us | 5,095.293 us | 0.70x |
| `literal-alternate/sherlock-zh` | 41.565 us | 66.602 us | 2,125.340 us | 0.62x |
| `ruff-noqa/tweaked` | 3,768.819 us | 7,205.867 us | 43,832.150 us | 0.52x |
| `aws-keys/quick` | 4,190.050 us | 9,127.989 us | 40,163.100 us | 0.46x |
| `literal/sherlock-casei-en` | 76.707 us | 181.592 us | 523.503 us | 0.42x |
| `literal/sherlock-casei-ru` | 147.743 us | 350.630 us | 5,378.647 us | 0.42x |
| `bounded-repeat/letters-en` | 1,067.536 us | 2,660.900 us | 2,844.844 us | 0.40x |
| `literal/sherlock-en` | 30.335 us | 85.696 us | 489.072 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,955.995 us | 5,584.771 us | 6,459.783 us | 0.35x |
| `dictionary/single` | 7,862.110 us | 24,315.125 us | 24,463.500 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,386.158 us | 4,589.700 us | 9,638.975 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 153.339 us | 594.734 us | 997.171 us | 0.26x |
| `bounded-repeat/capitals` | 10,668.938 us | 43,882.800 us | 47,990.900 us | 0.24x |
| `literal-alternate/sherlock-en` | 331.334 us | 1,406.961 us | 1,944.646 us | 0.24x |
| `literal-alternate/sherlock-en-nomatch` | 120.213 us | 623.796 us | 1,039.773 us | 0.19x |
| `bounded-repeat/context` | 13,770.100 us | 140,315.500 us | 143,862.900 us | 0.10x |
| `ruff-noqa/real` | 3,325.542 us | 67,059.300 us | 99,867.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 829.712 us | 22,289.600 us | 27,142.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 98.265 us | 10,826.071 us | 11,621.257 us | 0.0091x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.190 us | 0.103 us | 0.130 us | 1.85x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.118 us | 0.073 us | 0.086 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.115 us | 0.078 us | 0.094 us | 1.47x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.113 us | 0.080 us | 0.098 us | 1.42x |
| `common/email-match` | `IsMatch` | 0.317 us | 0.260 us | 0.285 us | 1.22x |
| `common/uri-match` | `IsMatch` | 0.264 us | 0.227 us | 0.265 us | 1.16x |
| `common/ip-match` | `IsMatch` | 0.194 us | 0.169 us | 0.185 us | 1.15x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.112 us | 0.105 us | 0.137 us | 1.07x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.158 us | 0.161 us | 0.178 us | 0.98x |
| `common/uri-miss` | `IsMatch` | 0.171 us | 0.189 us | 0.206 us | 0.90x |
| `industry/mariomka-email-count` | `Count` | 406.544 us | 478.309 us | 8,273.080 us | 0.85x |
| `industry/mariomka-uri-count` | `Count` | 1,567.555 us | 1,852.415 us | 9,338.625 us | 0.85x |
| `common/ip-miss` | `IsMatch` | 0.540 us | 0.704 us | 0.709 us | 0.77x |
| `common/date-miss` | `IsMatch` | 0.350 us | 0.535 us | 0.569 us | 0.65x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 140.836 us | 248.799 us | 472.979 us | 0.57x |
| `industry/leipzig-twain-count` | `Count` | 599.872 us | 1,244.492 us | 8,231.600 us | 0.48x |
| `common/match-word` | `Match` | 0.056 us | 0.161 us | 1.093 us | 0.35x |
| `common/matches-set` | `Count` | 60.039 us | 185.045 us | 185.358 us | 0.32x |
| `common/matches-word` | `Count` | 1.181 us | 4.080 us | 5.787 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,140.566 us | 7,842.920 us | 15,969.233 us | 0.27x |
| `common/replace-words` | `Replace` | 19.875 us | 72.858 us | 75.446 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.718 us | 922.798 us | 1,196.062 us | 0.18x |
| `common/split-words` | `Split` | 12.619 us | 75.781 us | 77.164 us | 0.17x |
| `common/matches-boundary` | `Count` | 23.952 us | 156.285 us | 158.623 us | 0.15x |
| `common/matches-words` | `Count` | 8.843 us | 66.633 us | 68.500 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,015.532 us | 9,486.867 us | 16,582.667 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,906.315 us | 18,686.520 us | 19,217.050 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.028 us | 0.330 us | 0.355 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 970.151 us | 11,384.943 us | 11,790.771 us | 0.09x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.799 us | 0.836 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.588 us | 0.620 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,688.200 us | 30,238.533 us | 37,666.750 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 998.087 us | 21,862.550 us | 29,349.300 us | 0.05x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 306.835 us | 8,101.940 us | 8,575.878 us | 0.04x |
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
| `aws-keys/full` | 65,636.400 us | 17,591.120 us | 52,134.267 us | 3.73x |
| `aws-keys/quick` | 4,351.800 us | 3,484.905 us | 34,362.700 us | 1.25x |
| `literal-alternate/sherlock-zh` | 41.719 us | 34.344 us | 2,066.446 us | 1.21x |
| `literal/sherlock-casei-en` | 76.570 us | 74.374 us | 391.510 us | 1.03x |
| `bounded-repeat/letters-ru` | 1,949.141 us | 2,065.946 us | 2,887.531 us | 0.94x |
| `literal-alternate/sherlock-en` | 331.219 us | 351.731 us | 762.651 us | 0.94x |
| `literal/sherlock-zh` | 21.318 us | 22.901 us | 2,084.608 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 153.927 us | 169.163 us | 533.592 us | 0.91x |
| `ruff-noqa/tweaked` | 3,794.552 us | 4,370.422 us | 38,391.000 us | 0.87x |
| `bounded-repeat/capitals` | 10,782.575 us | 12,919.133 us | 15,702.550 us | 0.83x |
| `literal-alternate/sherlock-casei-en` | 97.564 us | 125.501 us | 535.633 us | 0.78x |
| `literal/sherlock-ru` | 65.866 us | 85.493 us | 4,956.373 us | 0.77x |
| `literal/sherlock-casei-ru` | 146.939 us | 215.175 us | 5,113.400 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 121.390 us | 195.824 us | 601.084 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,384.736 us | 2,252.597 us | 7,191.500 us | 0.61x |
| `bounded-repeat/letters-en` | 1,070.349 us | 1,749.309 us | 1,885.178 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 838.718 us | 1,883.063 us | 6,977.975 us | 0.45x |
| `literal/sherlock-en` | 30.287 us | 74.926 us | 425.247 us | 0.40x |
| `bounded-repeat/context` | 13,742.333 us | 65,606.050 us | 68,147.450 us | 0.21x |
| `dictionary/single` | 7,823.390 us | 43,457.500 us | 43,390.100 us | 0.18x |
| `ruff-noqa/real` | 3,329.113 us | 19,758.425 us | 53,288.367 us | 0.17x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.182 us | 0.042 us | 0.070 us | 4.34x |
| `common/email-match` | `IsMatch` | 0.319 us | 0.076 us | 0.103 us | 4.17x |
| `common/ip-match` | `IsMatch` | 0.195 us | 0.048 us | 0.066 us | 4.10x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.116 us | 0.030 us | 0.046 us | 3.93x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.113 us | 0.031 us | 0.050 us | 3.60x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.120 us | 0.033 us | 0.052 us | 3.58x |
| `common/ip-miss` | `IsMatch` | 0.543 us | 0.170 us | 0.193 us | 3.20x |
| `common/uri-match` | `IsMatch` | 0.263 us | 0.086 us | 0.115 us | 3.07x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.160 us | 0.053 us | 0.073 us | 3.03x |
| `common/uri-miss` | `IsMatch` | 0.170 us | 0.065 us | 0.084 us | 2.62x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 141.658 us | 56.068 us | 246.047 us | 2.53x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.113 us | 0.046 us | 0.080 us | 2.44x |
| `common/date-miss` | `IsMatch` | 0.348 us | 0.189 us | 0.225 us | 1.84x |
| `industry/mariomka-uri-count` | `Count` | 1,556.446 us | 1,062.675 us | 7,968.200 us | 1.46x |
| `common/backtracking` | `IsMatch` | 0.054 us | 0.042 us | 0.070 us | 1.29x |
| `common/replace-words` | `Replace` | 20.255 us | 16.610 us | 17.796 us | 1.22x |
| `industry/leipzig-river-window-count` | `Count` | 1,707.293 us | 1,441.329 us | 7,351.300 us | 1.18x |
| `industry/mariomka-email-count` | `Count` | 410.229 us | 447.064 us | 7,529.540 us | 0.92x |
| `common/matches-words` | `Count` | 8.838 us | 10.534 us | 13.689 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,016.425 us | 1,217.156 us | 6,823.700 us | 0.84x |
| `common/match-word` | `Match` | 0.056 us | 0.076 us | 0.967 us | 0.73x |
| `industry/mariomka-ip-count` | `Count` | 2,150.429 us | 3,319.391 us | 10,278.733 us | 0.65x |
| `common/split-words` | `Split` | 12.700 us | 20.053 us | 22.231 us | 0.63x |
| `common/matches-set` | `Count` | 60.199 us | 100.430 us | 101.446 us | 0.60x |
| `common/matches-word` | `Count` | 1.177 us | 2.223 us | 3.329 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 602.002 us | 1,225.115 us | 7,148.380 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.085 us | 0.122 us | 0.44x |
| `common/email-miss` | `IsMatch` | 0.033 us | 0.085 us | 0.111 us | 0.38x |
| `industry/leipzig-name-family-count` | `Count` | 1,038.417 us | 2,979.846 us | 7,921.400 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 162.676 us | 562.563 us | 807.327 us | 0.29x |
| `common/matches-boundary` | `Count` | 23.699 us | 99.823 us | 101.633 us | 0.24x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,887.082 us | 8,562.789 us | 9,631.322 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 961.976 us | 4,568.747 us | 4,960.081 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 310.118 us | 3,813.740 us | 4,170.332 us | 0.08x |
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
| `lokad/docs/inline-doc-prefix` | 9.492 us | 4.463 us | 5.146 us | 2.13x |
| `lokad/langserv/identifier-validator` | 23.937 us | 11.941 us | 13.643 us | 2.00x |
| `lokad/lexer/number` | 27.585 us | 13.945 us | 14.772 us | 1.98x |
| `lokad/lexer/identifier` | 42.281 us | 21.917 us | 24.417 us | 1.93x |
| `lokad/lexer/doc-line` | 5.585 us | 2.935 us | 3.460 us | 1.90x |
| `lokad/style/hex-color` | 1.380 us | 0.782 us | 0.836 us | 1.77x |
| `fallback/lookahead` | 10.243 us | 5.823 us | 18.026 us | 1.76x |
| `structural/method-family-generic-call` | 10.465 us | 6.234 us | 19.319 us | 1.68x |
| `lokad/langserv/helper-identifier` | 11.316 us | 7.034 us | 7.363 us | 1.61x |
| `lokad/lexer/string` | 157.814 us | 98.296 us | 103.277 us | 1.61x |
| `lokad/langserv/color-short-hex` | 2.059 us | 1.293 us | 1.392 us | 1.59x |
| `lokad/langserv/color-rgb-hex` | 4.524 us | 2.877 us | 2.914 us | 1.57x |
| `lokad/lexer/operator-run` | 4.826 us | 3.383 us | 3.733 us | 1.43x |
| `lokad/style/range-ref` | 3.389 us | 2.557 us | 2.614 us | 1.33x |
| `lokad/imports/module-imports` | 7.986 us | 6.173 us | 30.338 us | 1.29x |
| `lokad/style/cell-ref` | 3.322 us | 2.596 us | 2.821 us | 1.28x |
| `structural/ordered-keyword-window` | 20.809 us | 16.862 us | 29.362 us | 1.23x |
| `structural/method-family-call` | 9.033 us | 7.639 us | 20.423 us | 1.18x |
| `structural/keyword-to-generic-type` | 2.950 us | 3.899 us | 17.323 us | 0.76x |
| `structural/ordered-keyword-pair` | 3.425 us | 5.047 us | 18.863 us | 0.68x |
| `literal/identifier-token` | 2.615 us | 4.111 us | 16.608 us | 0.64x |
| `structural/ordered-modifier-pair` | 9.681 us | 16.245 us | 29.015 us | 0.60x |
| `structural/ordered-identifier-window` | 14.836 us | 27.442 us | 40.568 us | 0.54x |
| `lokad/langserv/url-dashboard` | 12.128 us | 23.420 us | 24.029 us | 0.52x |
| `literal/call-token` | 1.737 us | 4.087 us | 16.490 us | 0.43x |
| `literal-family/method-token-family` | 3.603 us | 8.593 us | 20.942 us | 0.42x |
| `literal-family/type-token-family` | 9.287 us | 22.349 us | 35.093 us | 0.42x |
| `structural/modifier-family-to-type-window` | 99.804 us | 242.812 us | 257.315 us | 0.41x |
| `balancing/begin-end-blocks` | 1,439.528 us | 4,105.905 us | 4,130.668 us | 0.35x |
| `literal/identifier-token-casei` | 3.752 us | 14.395 us | 27.345 us | 0.26x |
| `lokad/langserv/url-download` | 12.137 us | 51.641 us | 51.565 us | 0.24x |
| `structural/modifier-family-to-type-pair` | 20.090 us | 126.121 us | 140.408 us | 0.16x |
| `lokad/folding/region-marker` | 4.976 us | 32.798 us | 60.720 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 26.053 us | 213.390 us | 226.242 us | 0.12x |
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
| `lokad/lexer/number` | 27.361 us | 7.157 us | 8.107 us | 3.82x |
| `lokad/docs/inline-doc-prefix` | 9.470 us | 2.621 us | 3.297 us | 3.61x |
| `structural/keyword-family-to-capitalized-identifier` | 25.998 us | 7.387 us | 20.620 us | 3.52x |
| `lokad/lexer/string` | 158.418 us | 46.150 us | 50.083 us | 3.43x |
| `balancing/begin-end-blocks` | 1,432.517 us | 447.237 us | 475.588 us | 3.20x |
| `lokad/style/hex-color` | 1.383 us | 0.506 us | 0.578 us | 2.73x |
| `lokad/langserv/identifier-validator` | 24.032 us | 8.941 us | 10.718 us | 2.69x |
| `lokad/lexer/identifier` | 40.369 us | 15.499 us | 19.565 us | 2.60x |
| `lokad/langserv/color-rgb-hex` | 4.485 us | 1.735 us | 1.935 us | 2.59x |
| `lokad/lexer/doc-line` | 5.558 us | 2.164 us | 2.819 us | 2.57x |
| `fallback/lookahead` | 10.294 us | 4.028 us | 15.827 us | 2.56x |
| `structural/ordered-keyword-window` | 20.668 us | 8.134 us | 20.534 us | 2.54x |
| `lokad/langserv/helper-identifier` | 11.338 us | 4.485 us | 5.326 us | 2.53x |
| `lokad/lexer/operator-run` | 4.829 us | 1.990 us | 2.458 us | 2.43x |
| `structural/method-family-generic-call` | 10.484 us | 4.383 us | 16.490 us | 2.39x |
| `lokad/style/range-ref` | 3.348 us | 1.416 us | 1.507 us | 2.36x |
| `lokad/langserv/color-short-hex` | 2.045 us | 0.930 us | 1.097 us | 2.20x |
| `lokad/style/cell-ref` | 3.316 us | 1.628 us | 1.801 us | 2.04x |
| `structural/modifier-family-to-type-pair` | 19.978 us | 10.198 us | 22.859 us | 1.96x |
| `lokad/imports/module-imports` | 7.989 us | 4.201 us | 27.837 us | 1.90x |
| `structural/method-family-call` | 9.067 us | 4.810 us | 17.244 us | 1.88x |
| `structural/modifier-family-to-type-window` | 99.655 us | 53.151 us | 66.641 us | 1.87x |
| `lokad/langserv/url-dashboard` | 12.228 us | 7.204 us | 7.778 us | 1.70x |
| `structural/ordered-identifier-window` | 14.870 us | 9.981 us | 23.016 us | 1.49x |
| `structural/ordered-modifier-pair` | 9.734 us | 8.197 us | 20.953 us | 1.19x |
| `lokad/langserv/url-download` | 12.076 us | 12.583 us | 13.153 us | 0.96x |
| `structural/keyword-to-generic-type` | 2.954 us | 3.296 us | 16.044 us | 0.90x |
| `structural/ordered-keyword-pair` | 3.407 us | 3.930 us | 16.948 us | 0.87x |
| `literal-family/type-token-family` | 9.272 us | 10.836 us | 23.053 us | 0.86x |
| `literal-family/method-token-family` | 3.608 us | 4.793 us | 16.787 us | 0.75x |
| `literal/identifier-token-casei` | 3.755 us | 5.675 us | 18.299 us | 0.66x |
| `literal/identifier-token` | 2.610 us | 4.457 us | 16.440 us | 0.59x |
| `literal/call-token` | 1.733 us | 3.737 us | 15.621 us | 0.46x |
| `lokad/folding/region-marker` | 4.822 us | 33.894 us | 60.783 us | 0.14x |
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
