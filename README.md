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
| `literal/sherlock-zh` | 21.064 us | 17.772 us | 2,121.441 us | 1.19x |
| `literal/sherlock-ru` | 64.351 us | 92.250 us | 5,093.560 us | 0.70x |
| `literal-alternate/sherlock-zh` | 41.345 us | 64.946 us | 2,104.497 us | 0.64x |
| `ruff-noqa/tweaked` | 3,778.395 us | 7,181.591 us | 42,092.050 us | 0.53x |
| `aws-keys/full` | 41,430.600 us | 81,531.500 us | 109,417.800 us | 0.51x |
| `literal/sherlock-casei-ru` | 146.468 us | 346.368 us | 5,309.336 us | 0.42x |
| `literal/sherlock-casei-en` | 77.344 us | 183.013 us | 601.220 us | 0.42x |
| `aws-keys/quick` | 3,592.857 us | 9,136.589 us | 38,156.050 us | 0.39x |
| `bounded-repeat/letters-en` | 1,040.842 us | 2,668.066 us | 2,815.289 us | 0.39x |
| `literal/sherlock-en` | 30.159 us | 81.571 us | 439.984 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,907.242 us | 5,499.536 us | 6,390.600 us | 0.35x |
| `dictionary/single` | 7,651.710 us | 24,287.575 us | 24,288.925 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,361.214 us | 4,637.700 us | 9,472.100 us | 0.29x |
| `bounded-repeat/capitals` | 12,596.614 us | 44,222.850 us | 47,090.900 us | 0.28x |
| `literal-alternate/sherlock-en-mixed` | 149.984 us | 563.757 us | 979.434 us | 0.27x |
| `literal-alternate/sherlock-en` | 323.729 us | 1,460.047 us | 1,967.146 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 120.459 us | 626.249 us | 1,033.837 us | 0.19x |
| `bounded-repeat/context` | 8,353.140 us | 138,914.300 us | 142,881.300 us | 0.06x |
| `ruff-noqa/real` | 3,292.335 us | 66,638.500 us | 97,817.100 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 823.414 us | 22,171.025 us | 26,997.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 105.071 us | 10,883.029 us | 11,626.186 us | 0.0097x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/date-match` | `IsMatch` | 0.157 us | 0.104 us | 0.131 us | 1.52x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.106 us | 0.072 us | 0.085 us | 1.47x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.105 us | 0.076 us | 0.091 us | 1.37x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.106 us | 0.079 us | 0.094 us | 1.35x |
| `common/uri-match` | `IsMatch` | 0.289 us | 0.223 us | 0.260 us | 1.29x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.123 us | 0.105 us | 0.138 us | 1.17x |
| `common/ip-match` | `IsMatch` | 0.192 us | 0.167 us | 0.188 us | 1.15x |
| `common/uri-miss` | `IsMatch` | 0.198 us | 0.174 us | 0.192 us | 1.14x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.158 us | 0.158 us | 0.173 us | 1.00x |
| `industry/mariomka-email-count` | `Count` | 404.640 us | 468.189 us | 8,253.160 us | 0.86x |
| `common/email-match` | `IsMatch` | 0.208 us | 0.256 us | 0.281 us | 0.81x |
| `industry/mariomka-uri-count` | `Count` | 1,393.334 us | 1,832.667 us | 9,944.950 us | 0.76x |
| `common/ip-miss` | `IsMatch` | 0.489 us | 0.682 us | 0.698 us | 0.72x |
| `common/date-miss` | `IsMatch` | 0.333 us | 0.533 us | 0.566 us | 0.62x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 148.634 us | 249.795 us | 462.032 us | 0.60x |
| `industry/leipzig-twain-count` | `Count` | 633.643 us | 1,319.961 us | 8,412.800 us | 0.48x |
| `common/match-word` | `Match` | 0.053 us | 0.120 us | 1.095 us | 0.44x |
| `common/matches-set` | `Count` | 58.380 us | 184.443 us | 187.144 us | 0.32x |
| `common/matches-word` | `Count` | 1.176 us | 4.040 us | 5.672 us | 0.29x |
| `common/replace-words` | `Replace` | 19.627 us | 73.614 us | 75.557 us | 0.27x |
| `industry/mariomka-ip-count` | `Count` | 2,082.056 us | 7,843.040 us | 15,883.500 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 159.238 us | 920.598 us | 1,169.111 us | 0.17x |
| `common/split-words` | `Split` | 12.557 us | 74.033 us | 75.892 us | 0.17x |
| `common/matches-boundary` | `Count` | 24.997 us | 156.984 us | 158.823 us | 0.16x |
| `common/matches-words` | `Count` | 8.678 us | 66.877 us | 67.960 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 998.793 us | 9,223.122 us | 15,969.575 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,774.272 us | 18,232.180 us | 18,625.160 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.329 us | 0.356 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 844.718 us | 11,621.500 us | 12,072.071 us | 0.07x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.800 us | 0.843 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.588 us | 0.620 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 1,808.319 us | 30,262.700 us | 37,338.950 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 964.486 us | 22,287.900 us | 28,840.800 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 283.334 us | 8,316.430 us | 8,870.156 us | 0.03x |
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
| `aws-keys/full` | 41,452.950 us | 17,518.340 us | 48,259.567 us | 2.37x |
| `literal-alternate/sherlock-zh` | 41.174 us | 33.882 us | 2,030.959 us | 1.22x |
| `literal/sherlock-casei-en` | 77.477 us | 75.139 us | 408.644 us | 1.03x |
| `aws-keys/quick` | 3,607.719 us | 3,522.255 us | 34,065.600 us | 1.02x |
| `literal-alternate/sherlock-en` | 325.544 us | 323.870 us | 711.759 us | 1.01x |
| `bounded-repeat/letters-ru` | 1,897.838 us | 1,944.941 us | 2,772.541 us | 0.98x |
| `literal/sherlock-zh` | 21.092 us | 22.607 us | 2,093.719 us | 0.93x |
| `bounded-repeat/capitals` | 11,988.671 us | 12,860.433 us | 16,408.900 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 148.703 us | 165.868 us | 548.631 us | 0.90x |
| `ruff-noqa/tweaked` | 3,777.895 us | 4,331.950 us | 35,935.050 us | 0.87x |
| `literal/sherlock-ru` | 64.343 us | 84.371 us | 4,868.847 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 104.962 us | 138.707 us | 537.628 us | 0.76x |
| `literal/sherlock-casei-ru` | 145.140 us | 211.487 us | 5,129.980 us | 0.69x |
| `literal-alternate/sherlock-en-nomatch` | 120.601 us | 189.999 us | 571.773 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,362.838 us | 2,218.603 us | 7,044.667 us | 0.61x |
| `bounded-repeat/letters-en` | 1,040.005 us | 1,727.452 us | 1,836.188 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 832.177 us | 1,875.939 us | 6,779.780 us | 0.44x |
| `literal/sherlock-en` | 30.370 us | 71.970 us | 433.505 us | 0.42x |
| `dictionary/single` | 7,698.710 us | 43,179.350 us | 43,277.350 us | 0.18x |
| `ruff-noqa/real` | 3,302.287 us | 19,495.400 us | 49,072.050 us | 0.17x |
| `bounded-repeat/context` | 8,366.756 us | 65,488.450 us | 68,304.900 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.194 us | 0.046 us | 0.065 us | 4.18x |
| `common/date-match` | `IsMatch` | 0.158 us | 0.041 us | 0.068 us | 3.88x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.103 us | 0.030 us | 0.045 us | 3.47x |
| `common/uri-match` | `IsMatch` | 0.286 us | 0.084 us | 0.114 us | 3.41x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.105 us | 0.032 us | 0.049 us | 3.32x |
| `common/uri-miss` | `IsMatch` | 0.204 us | 0.063 us | 0.084 us | 3.23x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.159 us | 0.051 us | 0.072 us | 3.08x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.106 us | 0.035 us | 0.051 us | 3.02x |
| `common/ip-miss` | `IsMatch` | 0.486 us | 0.169 us | 0.193 us | 2.87x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 148.937 us | 54.314 us | 240.615 us | 2.74x |
| `common/email-match` | `IsMatch` | 0.208 us | 0.077 us | 0.102 us | 2.72x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.122 us | 0.046 us | 0.082 us | 2.68x |
| `common/date-miss` | `IsMatch` | 0.332 us | 0.191 us | 0.225 us | 1.74x |
| `industry/mariomka-uri-count` | `Count` | 1,411.237 us | 1,039.826 us | 7,981.650 us | 1.36x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.042 us | 0.070 us | 1.32x |
| `industry/leipzig-river-window-count` | `Count` | 1,809.026 us | 1,485.667 us | 7,613.180 us | 1.22x |
| `common/replace-words` | `Replace` | 19.551 us | 16.282 us | 18.447 us | 1.20x |
| `common/match-word` | `Match` | 0.053 us | 0.045 us | 0.880 us | 1.17x |
| `industry/mariomka-email-count` | `Count` | 401.520 us | 440.908 us | 7,395.800 us | 0.91x |
| `common/matches-words` | `Count` | 8.663 us | 10.212 us | 13.269 us | 0.85x |
| `industry/leipzig-symbol-count` | `Count` | 977.453 us | 1,177.591 us | 6,643.660 us | 0.83x |
| `common/split-words` | `Split` | 12.533 us | 19.697 us | 21.407 us | 0.64x |
| `industry/mariomka-ip-count` | `Count` | 2,084.386 us | 3,290.509 us | 10,271.467 us | 0.63x |
| `common/matches-set` | `Count` | 58.549 us | 100.514 us | 102.234 us | 0.58x |
| `common/matches-word` | `Count` | 1.175 us | 2.219 us | 3.231 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 633.152 us | 1,281.097 us | 7,333.918 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.084 us | 0.123 us | 0.46x |
| `industry/leipzig-name-family-count` | `Count` | 999.296 us | 2,869.226 us | 8,049.933 us | 0.35x |
| `common/email-miss` | `IsMatch` | 0.026 us | 0.085 us | 0.111 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 161.662 us | 563.634 us | 793.923 us | 0.29x |
| `common/matches-boundary` | `Count` | 25.028 us | 100.342 us | 101.359 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,771.456 us | 8,433.444 us | 8,962.422 us | 0.21x |
| `industry/rust-sherlock-ing-count` | `Count` | 847.069 us | 4,516.641 us | 4,900.500 us | 0.19x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 283.368 us | 3,732.271 us | 4,080.800 us | 0.08x |
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
| `lokad/langserv/helper-identifier` | 9.266 us | 4.501 us | 5.421 us | 2.06x |
| `lokad/lexer/doc-line` | 3.543 us | 1.800 us | 2.526 us | 1.97x |
| `lokad/langserv/identifier-validator` | 13.472 us | 7.001 us | 8.527 us | 1.92x |
| `lokad/lexer/identifier` | 24.224 us | 12.624 us | 15.584 us | 1.92x |
| `fallback/lookahead` | 10.217 us | 5.667 us | 17.848 us | 1.80x |
| `structural/method-family-generic-call` | 10.731 us | 6.223 us | 19.159 us | 1.72x |
| `lokad/langserv/color-rgb-hex` | 2.839 us | 1.700 us | 1.954 us | 1.67x |
| `lokad/docs/inline-doc-prefix` | 5.111 us | 3.331 us | 4.049 us | 1.53x |
| `lokad/imports/module-imports` | 7.677 us | 5.990 us | 30.126 us | 1.28x |
| `structural/ordered-keyword-window` | 20.878 us | 16.710 us | 29.207 us | 1.25x |
| `structural/method-family-call` | 9.547 us | 7.690 us | 20.633 us | 1.24x |
| `lokad/lexer/string` | 94.557 us | 77.047 us | 81.550 us | 1.23x |
| `lokad/langserv/color-short-hex` | 0.924 us | 0.778 us | 0.956 us | 1.19x |
| `lokad/style/hex-color` | 0.548 us | 0.464 us | 0.559 us | 1.18x |
| `lokad/style/range-ref` | 1.866 us | 1.738 us | 1.842 us | 1.07x |
| `lokad/style/cell-ref` | 1.848 us | 1.837 us | 2.053 us | 1.01x |
| `lokad/lexer/operator-run` | 2.210 us | 2.200 us | 2.684 us | 1.00x |
| `lokad/lexer/number` | 8.353 us | 10.264 us | 11.269 us | 0.81x |
| `structural/keyword-to-generic-type` | 2.941 us | 3.860 us | 17.020 us | 0.76x |
| `structural/ordered-keyword-pair` | 3.364 us | 4.969 us | 18.079 us | 0.68x |
| `structural/ordered-modifier-pair` | 9.811 us | 16.667 us | 29.274 us | 0.59x |
| `structural/ordered-identifier-window` | 14.511 us | 26.955 us | 40.368 us | 0.54x |
| `literal/identifier-token` | 2.134 us | 4.101 us | 16.598 us | 0.52x |
| `lokad/langserv/url-dashboard` | 9.798 us | 22.180 us | 22.749 us | 0.44x |
| `structural/modifier-family-to-type-window` | 103.408 us | 240.210 us | 256.221 us | 0.43x |
| `literal/call-token` | 1.748 us | 4.072 us | 16.642 us | 0.43x |
| `literal-family/method-token-family` | 3.617 us | 8.607 us | 21.010 us | 0.42x |
| `literal-family/type-token-family` | 9.347 us | 23.007 us | 36.032 us | 0.41x |
| `balancing/begin-end-blocks` | 1,295.139 us | 4,105.363 us | 4,139.721 us | 0.32x |
| `lokad/langserv/url-download` | 11.325 us | 48.272 us | 48.535 us | 0.23x |
| `literal/identifier-token-casei` | 3.264 us | 14.104 us | 26.634 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 22.312 us | 124.110 us | 136.991 us | 0.18x |
| `lokad/folding/region-marker` | 4.861 us | 33.507 us | 59.692 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 13.606 us | 202.413 us | 216.430 us | 0.07x |
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
| `lokad/docs/inline-doc-prefix` | 5.068 us | 1.376 us | 2.095 us | 3.68x |
| `lokad/langserv/color-rgb-hex` | 2.941 us | 0.802 us | 1.048 us | 3.67x |
| `lokad/langserv/helper-identifier` | 9.176 us | 2.567 us | 3.444 us | 3.57x |
| `lokad/lexer/doc-line` | 3.583 us | 1.028 us | 1.751 us | 3.48x |
| `lokad/langserv/identifier-validator` | 13.536 us | 4.031 us | 5.478 us | 3.36x |
| `lokad/lexer/identifier` | 24.326 us | 7.488 us | 10.068 us | 3.25x |
| `lokad/lexer/string` | 93.965 us | 29.201 us | 32.094 us | 3.22x |
| `balancing/begin-end-blocks` | 1,295.634 us | 443.156 us | 472.123 us | 2.92x |
| `lokad/style/range-ref` | 1.873 us | 0.692 us | 0.805 us | 2.71x |
| `fallback/lookahead` | 10.306 us | 3.974 us | 15.695 us | 2.59x |
| `structural/ordered-keyword-window` | 20.964 us | 8.156 us | 20.467 us | 2.57x |
| `lokad/lexer/operator-run` | 2.215 us | 0.884 us | 1.348 us | 2.51x |
| `lokad/style/hex-color` | 0.549 us | 0.222 us | 0.297 us | 2.48x |
| `structural/method-family-generic-call` | 10.742 us | 4.337 us | 16.655 us | 2.48x |
| `lokad/style/cell-ref` | 1.853 us | 0.797 us | 1.002 us | 2.32x |
| `lokad/lexer/number` | 8.354 us | 3.636 us | 4.708 us | 2.30x |
| `structural/modifier-family-to-type-pair` | 22.348 us | 10.076 us | 22.273 us | 2.22x |
| `lokad/langserv/color-short-hex` | 0.919 us | 0.455 us | 0.641 us | 2.02x |
| `structural/method-family-call` | 9.516 us | 4.795 us | 17.170 us | 1.98x |
| `structural/modifier-family-to-type-window` | 103.642 us | 52.995 us | 66.749 us | 1.96x |
| `structural/keyword-family-to-capitalized-identifier` | 13.598 us | 7.351 us | 20.534 us | 1.85x |
| `lokad/imports/module-imports` | 7.691 us | 4.186 us | 27.618 us | 1.84x |
| `lokad/langserv/url-dashboard` | 9.752 us | 5.963 us | 6.419 us | 1.64x |
| `structural/ordered-identifier-window` | 14.493 us | 10.127 us | 23.455 us | 1.43x |
| `structural/ordered-modifier-pair` | 9.840 us | 8.199 us | 20.693 us | 1.20x |
| `lokad/langserv/url-download` | 11.345 us | 11.465 us | 11.823 us | 0.99x |
| `structural/keyword-to-generic-type` | 2.945 us | 3.247 us | 15.941 us | 0.91x |
| `literal-family/type-token-family` | 9.332 us | 10.895 us | 23.431 us | 0.86x |
| `structural/ordered-keyword-pair` | 3.341 us | 3.901 us | 16.245 us | 0.86x |
| `literal-family/method-token-family` | 3.613 us | 5.024 us | 17.245 us | 0.72x |
| `literal/identifier-token-casei` | 3.274 us | 5.556 us | 18.144 us | 0.59x |
| `literal/identifier-token` | 2.133 us | 4.463 us | 16.089 us | 0.48x |
| `literal/call-token` | 1.742 us | 3.749 us | 15.809 us | 0.46x |
| `lokad/folding/region-marker` | 4.910 us | 34.309 us | 60.023 us | 0.14x |
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
