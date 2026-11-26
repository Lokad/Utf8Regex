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
| `literal/sherlock-zh` | 14.143 us | 18.850 us | 2,204.739 us | 0.75x |
| `literal/sherlock-ru` | 68.931 us | 97.263 us | 5,364.814 us | 0.71x |
| `literal-alternate/sherlock-zh` | 43.268 us | 68.407 us | 2,189.371 us | 0.63x |
| `ruff-noqa/tweaked` | 3,953.105 us | 7,570.091 us | 41,475.400 us | 0.52x |
| `literal/sherlock-casei-en` | 80.715 us | 190.247 us | 676.963 us | 0.42x |
| `literal/sherlock-casei-ru` | 154.537 us | 373.371 us | 5,559.046 us | 0.41x |
| `bounded-repeat/letters-en` | 1,152.473 us | 2,854.865 us | 2,996.692 us | 0.40x |
| `literal/sherlock-en` | 31.881 us | 86.840 us | 522.681 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,081.300 us | 5,858.585 us | 6,726.683 us | 0.36x |
| `dictionary/single` | 8,271.610 us | 26,466.833 us | 26,447.200 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,450.777 us | 4,736.947 us | 9,922.525 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 156.785 us | 595.133 us | 1,110.050 us | 0.26x |
| `bounded-repeat/capitals` | 11,829.271 us | 46,663.150 us | 50,328.750 us | 0.25x |
| `literal-alternate/sherlock-en` | 345.708 us | 1,531.624 us | 2,082.876 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 128.733 us | 666.655 us | 1,297.217 us | 0.19x |
| `aws-keys/quick` | 1,860.002 us | 9,730.944 us | 43,431.050 us | 0.19x |
| `aws-keys/full` | 8,605.356 us | 87,412.600 us | 121,857.700 us | 0.10x |
| `bounded-repeat/context` | 9,097.533 us | 148,523.800 us | 153,978.400 us | 0.06x |
| `ruff-noqa/real` | 3,449.927 us | 75,428.700 us | 92,325.400 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 860.879 us | 23,104.850 us | 28,363.000 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 106.338 us | 11,169.986 us | 11,996.000 us | 0.0095x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.086 us | 0.109 us | 0.147 us | 0.79x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.076 us | 0.091 us | 0.66x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.109 us | 0.139 us | 0.65x |
| `common/ip-match` | `IsMatch` | 0.113 us | 0.175 us | 0.194 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.083 us | 0.101 us | 0.64x |
| `industry/mariomka-uri-count` | `Count` | 1,201.952 us | 1,894.398 us | 10,498.267 us | 0.63x |
| `industry/mariomka-email-count` | `Count` | 280.408 us | 507.289 us | 9,164.925 us | 0.55x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.044 us | 0.082 us | 0.099 us | 0.54x |
| `common/email-match` | `IsMatch` | 0.143 us | 0.271 us | 0.298 us | 0.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.082 us | 0.161 us | 0.178 us | 0.51x |
| `industry/leipzig-twain-count` | `Count` | 632.202 us | 1,331.441 us | 8,651.700 us | 0.47x |
| `common/uri-miss` | `IsMatch` | 0.089 us | 0.191 us | 0.211 us | 0.47x |
| `common/match-word` | `Match` | 0.051 us | 0.130 us | 1.175 us | 0.39x |
| `industry/mariomka-ip-count` | `Count` | 3,131.868 us | 8,154.840 us | 21,858.567 us | 0.38x |
| `common/ip-miss` | `IsMatch` | 0.277 us | 0.732 us | 0.755 us | 0.38x |
| `common/uri-match` | `IsMatch` | 0.082 us | 0.235 us | 0.269 us | 0.35x |
| `common/matches-set` | `Count` | 63.000 us | 195.851 us | 197.352 us | 0.32x |
| `common/matches-word` | `Count` | 1.254 us | 4.232 us | 5.966 us | 0.30x |
| `common/replace-words` | `Replace` | 20.962 us | 78.401 us | 81.203 us | 0.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 67.968 us | 261.674 us | 505.005 us | 0.26x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.561 us | 0.596 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.756 us | 968.925 us | 1,283.343 us | 0.17x |
| `common/matches-boundary` | `Count` | 28.716 us | 168.520 us | 169.848 us | 0.17x |
| `common/split-words` | `Split` | 13.214 us | 77.651 us | 79.044 us | 0.17x |
| `common/matches-words` | `Count` | 9.219 us | 70.265 us | 71.462 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,078.693 us | 9,914.825 us | 17,067.467 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,945.313 us | 19,558.225 us | 20,139.375 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.621 us | 0.648 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,688.711 us | 32,159.233 us | 39,484.550 us | 0.05x |
| `common/email-miss` | `IsMatch` | 0.018 us | 0.352 us | 0.378 us | 0.05x |
| `industry/leipzig-symbol-count` | `Count` | 1,029.353 us | 24,763.575 us | 32,277.700 us | 0.04x |
| `common/backtracking` | `IsMatch` | 0.034 us | 0.835 us | 0.879 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 238.804 us | 8,376.167 us | 8,835.178 us | 0.03x |
| `industry/rust-sherlock-ing-count` | `Count` | 286.497 us | 11,670.800 us | 12,463.914 us | 0.02x |
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
| `literal-alternate/sherlock-zh` | 43.686 us | 36.107 us | 2,186.462 us | 1.21x |
| `literal/sherlock-casei-en` | 80.713 us | 79.648 us | 514.620 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,071.176 us | 2,062.819 us | 2,961.565 us | 1.00x |
| `literal-alternate/sherlock-en` | 345.961 us | 346.593 us | 820.153 us | 1.00x |
| `bounded-repeat/capitals` | 11,885.229 us | 13,519.117 us | 16,406.475 us | 0.88x |
| `literal-alternate/sherlock-en-mixed` | 156.251 us | 179.394 us | 562.285 us | 0.87x |
| `ruff-noqa/tweaked` | 3,965.389 us | 4,577.971 us | 36,466.000 us | 0.87x |
| `literal/sherlock-ru` | 69.078 us | 93.800 us | 5,289.927 us | 0.74x |
| `literal-alternate/sherlock-casei-en` | 106.991 us | 145.403 us | 731.559 us | 0.74x |
| `literal/sherlock-casei-ru` | 155.118 us | 224.463 us | 5,392.100 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 128.812 us | 204.452 us | 643.584 us | 0.63x |
| `bounded-repeat/letters-en` | 1,150.026 us | 1,837.762 us | 1,966.037 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,453.115 us | 2,349.384 us | 7,392.975 us | 0.62x |
| `literal/sherlock-zh` | 14.111 us | 23.888 us | 2,257.786 us | 0.59x |
| `aws-keys/quick` | 1,864.544 us | 3,604.005 us | 36,147.700 us | 0.52x |
| `aws-keys/full` | 8,616.100 us | 18,643.520 us | 54,492.267 us | 0.46x |
| `literal-alternate/sherlock-casei-ru` | 865.418 us | 1,972.187 us | 7,112.100 us | 0.44x |
| `literal/sherlock-en` | 31.675 us | 76.271 us | 436.393 us | 0.42x |
| `dictionary/single` | 8,205.889 us | 46,132.600 us | 46,271.400 us | 0.18x |
| `ruff-noqa/real` | 3,495.223 us | 21,269.700 us | 36,082.400 us | 0.16x |
| `bounded-repeat/context` | 9,250.667 us | 69,535.600 us | 72,413.700 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.108 us | 0.048 us | 0.068 us | 2.27x |
| `common/date-match` | `IsMatch` | 0.071 us | 0.044 us | 0.074 us | 1.62x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.032 us | 0.049 us | 1.55x |
| `common/ip-miss` | `IsMatch` | 0.270 us | 0.180 us | 0.207 us | 1.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.055 us | 0.076 us | 1.42x |
| `common/uri-miss` | `IsMatch` | 0.092 us | 0.067 us | 0.088 us | 1.37x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.046 us | 0.035 us | 0.052 us | 1.30x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.040 us | 0.032 us | 0.048 us | 1.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 68.189 us | 57.976 us | 266.992 us | 1.18x |
| `common/replace-words` | `Replace` | 20.879 us | 18.101 us | 19.554 us | 1.15x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.054 us | 0.049 us | 0.088 us | 1.10x |
| `industry/leipzig-river-window-count` | `Count` | 1,710.704 us | 1,566.094 us | 8,181.975 us | 1.09x |
| `industry/mariomka-uri-count` | `Count` | 1,208.865 us | 1,121.406 us | 8,589.300 us | 1.08x |
| `common/match-word` | `Match` | 0.051 us | 0.047 us | 0.960 us | 1.07x |
| `common/email-match` | `IsMatch` | 0.075 us | 0.081 us | 0.107 us | 0.92x |
| `common/uri-match` | `IsMatch` | 0.082 us | 0.090 us | 0.121 us | 0.91x |
| `industry/mariomka-ip-count` | `Count` | 3,116.329 us | 3,426.248 us | 10,940.933 us | 0.91x |
| `common/matches-words` | `Count` | 9.258 us | 10.942 us | 14.079 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 1,028.427 us | 1,242.985 us | 7,400.025 us | 0.83x |
| `common/backtracking` | `IsMatch` | 0.032 us | 0.043 us | 0.074 us | 0.75x |
| `common/split-words` | `Split` | 13.228 us | 20.528 us | 22.346 us | 0.64x |
| `common/matches-set` | `Count` | 62.954 us | 105.425 us | 107.315 us | 0.60x |
| `industry/mariomka-email-count` | `Count` | 280.662 us | 472.310 us | 7,800.525 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.198 us | 0.235 us | 0.56x |
| `common/matches-word` | `Count` | 1.244 us | 2.360 us | 3.529 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 636.371 us | 1,306.586 us | 7,352.250 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,101.428 us | 3,042.284 us | 8,469.875 us | 0.36x |
| `common/one-node-backtracking` | `IsMatch` | 0.036 us | 0.125 us | 0.130 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.444 us | 600.420 us | 864.825 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.916 us | 105.597 us | 106.584 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,955.603 us | 8,749.144 us | 9,403.288 us | 0.22x |
| `common/email-miss` | `IsMatch` | 0.020 us | 0.090 us | 0.116 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 242.887 us | 3,945.615 us | 4,321.522 us | 0.06x |
| `industry/rust-sherlock-ing-count` | `Count` | 286.795 us | 4,762.038 us | 5,222.513 us | 0.06x |
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
| `lokad/imports/module-imports` | 6.016 us | 6.472 us | 32.569 us | 0.93x |
| `fallback/lookahead` | 5.319 us | 6.041 us | 20.573 us | 0.88x |
| `lokad/langserv/color-short-hex` | 0.591 us | 0.831 us | 1.009 us | 0.71x |
| `lokad/lexer/identifier` | 9.229 us | 13.607 us | 16.362 us | 0.68x |
| `structural/ordered-keyword-window` | 12.021 us | 17.736 us | 32.741 us | 0.68x |
| `structural/method-family-generic-call` | 4.424 us | 6.629 us | 21.472 us | 0.67x |
| `lokad/langserv/identifier-validator` | 4.921 us | 7.456 us | 9.098 us | 0.66x |
| `structural/method-family-call` | 5.318 us | 8.078 us | 22.430 us | 0.66x |
| `lokad/lexer/doc-line` | 1.205 us | 1.894 us | 2.607 us | 0.64x |
| `structural/ordered-modifier-pair` | 10.747 us | 17.090 us | 31.536 us | 0.63x |
| `lokad/docs/inline-doc-prefix` | 2.030 us | 3.464 us | 4.278 us | 0.59x |
| `structural/keyword-to-generic-type` | 2.289 us | 4.123 us | 19.050 us | 0.56x |
| `lokad/langserv/helper-identifier` | 2.477 us | 4.723 us | 5.613 us | 0.52x |
| `literal/identifier-token` | 2.260 us | 4.340 us | 18.513 us | 0.52x |
| `lokad/lexer/operator-run` | 1.193 us | 2.360 us | 2.814 us | 0.51x |
| `structural/ordered-keyword-pair` | 2.571 us | 5.331 us | 20.032 us | 0.48x |
| `lokad/style/hex-color` | 0.237 us | 0.516 us | 0.600 us | 0.46x |
| `lokad/style/range-ref` | 0.834 us | 1.834 us | 1.976 us | 0.45x |
| `literal/call-token` | 1.857 us | 4.294 us | 18.535 us | 0.43x |
| `lokad/style/cell-ref` | 0.852 us | 1.985 us | 2.113 us | 0.43x |
| `lokad/langserv/color-rgb-hex` | 0.735 us | 1.805 us | 2.059 us | 0.41x |
| `lokad/lexer/string` | 32.439 us | 81.867 us | 85.830 us | 0.40x |
| `literal-family/type-token-family` | 9.275 us | 23.862 us | 39.097 us | 0.39x |
| `lokad/langserv/url-dashboard` | 9.083 us | 23.425 us | 23.922 us | 0.39x |
| `lokad/lexer/number` | 3.209 us | 10.832 us | 12.106 us | 0.30x |
| `structural/ordered-identifier-window` | 8.295 us | 28.574 us | 42.701 us | 0.29x |
| `literal-family/method-token-family` | 2.642 us | 9.149 us | 23.582 us | 0.29x |
| `literal/identifier-token-casei` | 3.934 us | 14.924 us | 29.891 us | 0.26x |
| `structural/modifier-family-to-type-window` | 61.138 us | 255.801 us | 272.228 us | 0.24x |
| `lokad/langserv/url-download` | 11.312 us | 51.496 us | 51.813 us | 0.22x |
| `lokad/folding/region-marker` | 5.664 us | 35.646 us | 64.045 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 9.897 us | 220.680 us | 237.781 us | 0.04x |
| `structural/modifier-family-to-type-pair` | 2.935 us | 131.183 us | 146.968 us | 0.02x |
| `balancing/begin-end-blocks` | 47.838 us | 4,332.111 us | 4,380.400 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.123 us | 6.339 us | 6.856 us | 1.44x |
| `lokad/docs/inline-doc-prefix` | 2.018 us | 1.434 us | 2.202 us | 1.41x |
| `structural/ordered-keyword-window` | 12.004 us | 8.606 us | 23.234 us | 1.39x |
| `lokad/imports/module-imports` | 5.977 us | 4.422 us | 29.660 us | 1.35x |
| `structural/keyword-family-to-capitalized-identifier` | 9.932 us | 7.674 us | 22.911 us | 1.29x |
| `fallback/lookahead` | 5.311 us | 4.211 us | 18.263 us | 1.26x |
| `structural/ordered-modifier-pair` | 10.724 us | 8.562 us | 22.814 us | 1.25x |
| `lokad/lexer/operator-run` | 1.158 us | 0.945 us | 1.415 us | 1.23x |
| `lokad/lexer/identifier` | 9.154 us | 7.801 us | 10.718 us | 1.17x |
| `lokad/langserv/identifier-validator` | 4.950 us | 4.273 us | 5.838 us | 1.16x |
| `lokad/lexer/doc-line` | 1.209 us | 1.059 us | 1.840 us | 1.14x |
| `structural/modifier-family-to-type-window` | 61.308 us | 56.264 us | 72.057 us | 1.09x |
| `lokad/lexer/string` | 32.879 us | 30.563 us | 33.622 us | 1.08x |
| `lokad/style/range-ref` | 0.784 us | 0.736 us | 0.869 us | 1.07x |
| `structural/method-family-call` | 5.347 us | 5.048 us | 18.924 us | 1.06x |
| `lokad/langserv/color-short-hex` | 0.510 us | 0.489 us | 0.679 us | 1.04x |
| `lokad/style/cell-ref` | 0.856 us | 0.836 us | 1.057 us | 1.02x |
| `structural/method-family-generic-call` | 4.426 us | 4.630 us | 18.457 us | 0.96x |
| `lokad/style/hex-color` | 0.218 us | 0.232 us | 0.314 us | 0.94x |
| `lokad/langserv/helper-identifier` | 2.469 us | 2.703 us | 3.652 us | 0.91x |
| `lokad/langserv/url-download` | 11.277 us | 12.469 us | 13.050 us | 0.90x |
| `lokad/lexer/number` | 3.202 us | 3.865 us | 4.921 us | 0.83x |
| `literal-family/type-token-family` | 9.254 us | 11.514 us | 25.994 us | 0.80x |
| `structural/ordered-identifier-window` | 8.308 us | 10.630 us | 24.990 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 0.645 us | 0.846 us | 1.103 us | 0.76x |
| `literal/identifier-token-casei` | 3.949 us | 5.481 us | 20.620 us | 0.72x |
| `structural/keyword-to-generic-type` | 2.295 us | 3.452 us | 17.875 us | 0.66x |
| `structural/ordered-keyword-pair` | 2.563 us | 4.148 us | 18.258 us | 0.62x |
| `literal-family/method-token-family` | 2.650 us | 5.324 us | 19.368 us | 0.50x |
| `literal/identifier-token` | 2.259 us | 4.679 us | 18.195 us | 0.48x |
| `literal/call-token` | 1.852 us | 3.979 us | 17.568 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 2.936 us | 10.647 us | 25.114 us | 0.28x |
| `lokad/folding/region-marker` | 5.657 us | 35.963 us | 63.542 us | 0.16x |
| `balancing/begin-end-blocks` | 47.782 us | 468.113 us | 496.556 us | 0.10x |
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
