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
| `literal/sherlock-zh` | 21.997 us | 18.330 us | 2,188.964 us | 1.20x |
| `literal/sherlock-ru` | 66.118 us | 95.986 us | 5,102.013 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.270 us | 68.591 us | 2,152.636 us | 0.62x |
| `aws-keys/quick` | 5,159.547 us | 9,424.538 us | 38,291.650 us | 0.55x |
| `ruff-noqa/tweaked` | 3,932.150 us | 7,566.000 us | 41,292.100 us | 0.52x |
| `aws-keys/full` | 42,512.250 us | 83,193.200 us | 113,792.500 us | 0.51x |
| `literal/sherlock-casei-en` | 79.018 us | 184.756 us | 593.339 us | 0.43x |
| `literal/sherlock-casei-ru` | 141.447 us | 349.949 us | 5,466.986 us | 0.40x |
| `bounded-repeat/letters-en` | 1,071.539 us | 2,768.175 us | 2,929.042 us | 0.39x |
| `literal/sherlock-en` | 30.996 us | 84.164 us | 444.023 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,908.221 us | 5,646.171 us | 6,533.358 us | 0.34x |
| `dictionary/single` | 7,872.445 us | 25,193.000 us | 25,168.100 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,402.548 us | 4,650.506 us | 9,952.825 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 152.441 us | 577.443 us | 1,020.326 us | 0.26x |
| `bounded-repeat/capitals` | 11,211.014 us | 43,643.000 us | 47,358.950 us | 0.26x |
| `literal-alternate/sherlock-en` | 332.840 us | 1,490.304 us | 2,055.142 us | 0.22x |
| `literal-alternate/sherlock-en-nomatch` | 123.605 us | 633.480 us | 1,077.439 us | 0.20x |
| `bounded-repeat/context` | 8,340.890 us | 142,899.600 us | 147,283.500 us | 0.06x |
| `ruff-noqa/real` | 3,382.823 us | 68,735.700 us | 103,836.900 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 850.492 us | 22,244.500 us | 27,530.600 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 114.543 us | 11,012.029 us | 11,808.186 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.098 us | 0.074 us | 0.088 us | 1.32x |
| `common/uri-match` | `IsMatch` | 0.298 us | 0.227 us | 0.260 us | 1.32x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.097 us | 0.079 us | 0.095 us | 1.23x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.098 us | 0.084 us | 0.099 us | 1.17x |
| `common/date-match` | `IsMatch` | 0.123 us | 0.107 us | 0.135 us | 1.15x |
| `common/uri-miss` | `IsMatch` | 0.196 us | 0.180 us | 0.199 us | 1.09x |
| `common/ip-match` | `IsMatch` | 0.178 us | 0.168 us | 0.188 us | 1.06x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.106 us | 0.107 us | 0.140 us | 0.99x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.142 us | 0.165 us | 0.179 us | 0.86x |
| `industry/mariomka-email-count` | `Count` | 413.237 us | 482.558 us | 8,660.020 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,404.809 us | 1,816.271 us | 9,936.450 us | 0.77x |
| `common/ip-miss` | `IsMatch` | 0.461 us | 0.692 us | 0.711 us | 0.67x |
| `common/email-match` | `IsMatch` | 0.171 us | 0.258 us | 0.282 us | 0.66x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 150.926 us | 254.203 us | 475.467 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.262 us | 0.539 us | 0.576 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 617.616 us | 1,294.662 us | 8,675.400 us | 0.48x |
| `common/match-word` | `Match` | 0.055 us | 0.122 us | 1.141 us | 0.45x |
| `common/matches-set` | `Count` | 59.566 us | 189.961 us | 192.244 us | 0.31x |
| `common/matches-word` | `Count` | 1.202 us | 4.143 us | 5.909 us | 0.29x |
| `common/replace-words` | `Replace` | 21.334 us | 74.835 us | 76.941 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,187.969 us | 8,134.690 us | 16,411.667 us | 0.27x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.315 us | 940.137 us | 1,251.497 us | 0.17x |
| `common/split-words` | `Split` | 12.868 us | 75.853 us | 77.455 us | 0.17x |
| `common/matches-boundary` | `Count` | 25.088 us | 160.940 us | 164.338 us | 0.16x |
| `common/matches-words` | `Count` | 8.972 us | 68.972 us | 70.027 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,021.939 us | 9,612.100 us | 16,943.233 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,908.112 us | 19,103.650 us | 19,565.800 us | 0.10x |
| `common/email-miss` | `IsMatch` | 0.033 us | 0.339 us | 0.369 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 842.016 us | 11,502.943 us | 12,133.471 us | 0.07x |
| `common/backtracking` | `IsMatch` | 0.055 us | 0.823 us | 0.860 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.603 us | 0.634 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,845.551 us | 31,019.733 us | 38,444.450 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 1,002.721 us | 23,206.300 us | 30,472.900 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 292.188 us | 8,014.430 us | 8,507.133 us | 0.04x |
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
| `aws-keys/full` | 42,500.300 us | 18,019.540 us | 49,405.567 us | 2.36x |
| `aws-keys/quick` | 5,233.400 us | 3,613.057 us | 35,731.700 us | 1.45x |
| `literal-alternate/sherlock-zh` | 42.391 us | 35.328 us | 2,133.663 us | 1.20x |
| `literal/sherlock-casei-en` | 78.541 us | 78.947 us | 452.770 us | 0.99x |
| `bounded-repeat/letters-ru` | 1,926.397 us | 1,990.226 us | 2,850.315 us | 0.97x |
| `literal/sherlock-zh` | 21.974 us | 23.350 us | 2,186.606 us | 0.94x |
| `literal-alternate/sherlock-en` | 332.730 us | 354.946 us | 749.093 us | 0.94x |
| `literal-alternate/sherlock-casei-en` | 113.269 us | 125.403 us | 542.521 us | 0.90x |
| `literal-alternate/sherlock-en-mixed` | 152.410 us | 172.113 us | 529.410 us | 0.89x |
| `ruff-noqa/tweaked` | 3,965.963 us | 4,505.224 us | 35,753.900 us | 0.88x |
| `bounded-repeat/capitals` | 11,191.857 us | 12,969.017 us | 16,007.750 us | 0.86x |
| `literal/sherlock-ru` | 66.444 us | 87.825 us | 5,143.600 us | 0.76x |
| `literal/sherlock-casei-ru` | 142.799 us | 221.412 us | 5,251.667 us | 0.64x |
| `literal-alternate/sherlock-en-nomatch` | 123.374 us | 195.207 us | 569.155 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,403.865 us | 2,286.997 us | 7,407.750 us | 0.61x |
| `bounded-repeat/letters-en` | 1,073.817 us | 1,771.260 us | 1,920.075 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 843.001 us | 1,895.580 us | 6,995.400 us | 0.44x |
| `literal/sherlock-en` | 30.667 us | 73.416 us | 401.513 us | 0.42x |
| `dictionary/single` | 7,877.290 us | 44,516.700 us | 44,953.950 us | 0.18x |
| `ruff-noqa/real` | 3,353.648 us | 21,545.075 us | 54,949.667 us | 0.16x |
| `bounded-repeat/context` | 8,410.278 us | 66,731.200 us | 70,476.900 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.178 us | 0.049 us | 0.069 us | 3.66x |
| `common/uri-match` | `IsMatch` | 0.298 us | 0.085 us | 0.118 us | 3.51x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.097 us | 0.030 us | 0.049 us | 3.22x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.096 us | 0.031 us | 0.046 us | 3.13x |
| `common/uri-miss` | `IsMatch` | 0.201 us | 0.065 us | 0.085 us | 3.08x |
| `common/date-match` | `IsMatch` | 0.122 us | 0.041 us | 0.070 us | 2.96x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.095 us | 0.035 us | 0.052 us | 2.69x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.141 us | 0.054 us | 0.075 us | 2.61x |
| `common/ip-miss` | `IsMatch` | 0.461 us | 0.179 us | 0.201 us | 2.58x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 150.292 us | 58.912 us | 250.336 us | 2.55x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.104 us | 0.048 us | 0.081 us | 2.19x |
| `common/email-match` | `IsMatch` | 0.169 us | 0.079 us | 0.107 us | 2.15x |
| `common/date-miss` | `IsMatch` | 0.258 us | 0.191 us | 0.228 us | 1.35x |
| `industry/mariomka-uri-count` | `Count` | 1,416.974 us | 1,091.475 us | 8,159.775 us | 1.30x |
| `common/backtracking` | `IsMatch` | 0.054 us | 0.043 us | 0.073 us | 1.26x |
| `common/replace-words` | `Replace` | 21.259 us | 17.098 us | 18.167 us | 1.24x |
| `industry/leipzig-river-window-count` | `Count` | 1,845.824 us | 1,547.033 us | 8,008.850 us | 1.19x |
| `common/match-word` | `Match` | 0.055 us | 0.047 us | 0.920 us | 1.16x |
| `industry/mariomka-email-count` | `Count` | 414.922 us | 454.199 us | 7,691.700 us | 0.91x |
| `common/matches-words` | `Count` | 8.910 us | 10.652 us | 13.856 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,004.488 us | 1,238.669 us | 7,078.125 us | 0.81x |
| `industry/mariomka-ip-count` | `Count` | 2,164.620 us | 3,433.000 us | 10,644.467 us | 0.63x |
| `common/split-words` | `Split` | 12.837 us | 20.660 us | 22.651 us | 0.62x |
| `common/matches-set` | `Count` | 59.902 us | 103.694 us | 105.596 us | 0.58x |
| `common/matches-word` | `Count` | 1.205 us | 2.295 us | 3.312 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 614.597 us | 1,262.800 us | 6,699.383 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.037 us | 0.090 us | 0.127 us | 0.41x |
| `common/email-miss` | `IsMatch` | 0.033 us | 0.089 us | 0.113 us | 0.38x |
| `industry/leipzig-name-family-count` | `Count` | 1,034.688 us | 2,990.769 us | 8,234.975 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.723 us | 574.369 us | 839.367 us | 0.29x |
| `common/matches-boundary` | `Count` | 25.741 us | 103.153 us | 104.734 us | 0.25x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,884.592 us | 8,498.356 us | 8,923.311 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 844.609 us | 4,631.606 us | 5,106.031 us | 0.18x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 291.158 us | 3,890.255 us | 4,218.089 us | 0.07x |
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
| `lokad/langserv/identifier-validator` | 14.915 us | 7.388 us | 8.781 us | 2.02x |
| `lokad/lexer/identifier` | 24.703 us | 13.344 us | 16.272 us | 1.85x |
| `fallback/lookahead` | 10.585 us | 6.015 us | 18.665 us | 1.76x |
| `structural/method-family-generic-call` | 10.966 us | 6.322 us | 19.780 us | 1.73x |
| `lokad/imports/module-imports` | 7.498 us | 6.065 us | 29.980 us | 1.24x |
| `structural/method-family-call` | 9.669 us | 7.898 us | 21.239 us | 1.22x |
| `lokad/lexer/doc-line` | 2.265 us | 1.869 us | 2.618 us | 1.21x |
| `structural/ordered-keyword-window` | 20.578 us | 17.268 us | 30.143 us | 1.19x |
| `lokad/style/cell-ref` | 2.041 us | 1.832 us | 2.069 us | 1.11x |
| `lokad/style/hex-color` | 0.506 us | 0.490 us | 0.589 us | 1.03x |
| `lokad/style/range-ref` | 1.866 us | 1.811 us | 1.943 us | 1.03x |
| `lokad/langserv/color-short-hex` | 0.806 us | 0.812 us | 0.986 us | 0.99x |
| `lokad/lexer/operator-run` | 2.256 us | 2.288 us | 2.729 us | 0.99x |
| `lokad/docs/inline-doc-prefix` | 3.155 us | 3.445 us | 4.170 us | 0.92x |
| `lokad/langserv/color-rgb-hex` | 1.409 us | 1.825 us | 2.016 us | 0.77x |
| `structural/keyword-to-generic-type` | 2.997 us | 3.988 us | 17.584 us | 0.75x |
| `lokad/langserv/helper-identifier` | 3.121 us | 4.651 us | 5.508 us | 0.67x |
| `structural/ordered-keyword-pair` | 3.513 us | 5.241 us | 18.550 us | 0.67x |
| `lokad/lexer/number` | 6.924 us | 10.568 us | 11.517 us | 0.66x |
| `structural/ordered-modifier-pair` | 10.142 us | 16.790 us | 29.428 us | 0.60x |
| `literal/identifier-token` | 2.278 us | 4.238 us | 16.983 us | 0.54x |
| `structural/ordered-identifier-window` | 14.517 us | 27.576 us | 41.005 us | 0.53x |
| `lokad/langserv/url-dashboard` | 9.884 us | 22.884 us | 23.251 us | 0.43x |
| `literal/call-token` | 1.797 us | 4.208 us | 17.072 us | 0.43x |
| `structural/modifier-family-to-type-window` | 101.515 us | 246.736 us | 263.125 us | 0.41x |
| `literal-family/type-token-family` | 9.009 us | 23.243 us | 36.375 us | 0.39x |
| `lokad/lexer/string` | 27.762 us | 82.212 us | 87.052 us | 0.34x |
| `balancing/begin-end-blocks` | 1,310.422 us | 4,269.117 us | 4,270.461 us | 0.31x |
| `literal-family/method-token-family` | 2.653 us | 9.173 us | 21.751 us | 0.29x |
| `literal/identifier-token-casei` | 3.834 us | 15.290 us | 27.888 us | 0.25x |
| `lokad/langserv/url-download` | 11.336 us | 50.131 us | 51.121 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 24.699 us | 128.190 us | 142.978 us | 0.19x |
| `lokad/folding/region-marker` | 5.024 us | 34.248 us | 60.499 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 13.984 us | 212.145 us | 226.106 us | 0.07x |
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
| `lokad/langserv/identifier-validator` | 14.936 us | 4.190 us | 5.627 us | 3.56x |
| `lokad/lexer/identifier` | 24.631 us | 7.958 us | 10.482 us | 3.10x |
| `balancing/begin-end-blocks` | 1,308.398 us | 453.604 us | 483.884 us | 2.88x |
| `lokad/style/range-ref` | 1.892 us | 0.712 us | 0.855 us | 2.66x |
| `fallback/lookahead` | 10.602 us | 4.101 us | 16.326 us | 2.59x |
| `lokad/style/cell-ref` | 2.037 us | 0.804 us | 0.993 us | 2.53x |
| `structural/ordered-keyword-window` | 20.660 us | 8.342 us | 21.135 us | 2.48x |
| `lokad/lexer/operator-run` | 2.245 us | 0.911 us | 1.348 us | 2.46x |
| `structural/method-family-generic-call` | 10.998 us | 4.470 us | 16.995 us | 2.46x |
| `structural/modifier-family-to-type-pair` | 24.837 us | 10.327 us | 23.093 us | 2.41x |
| `lokad/style/hex-color` | 0.507 us | 0.223 us | 0.309 us | 2.27x |
| `lokad/docs/inline-doc-prefix` | 3.137 us | 1.407 us | 2.143 us | 2.23x |
| `lokad/lexer/doc-line` | 2.249 us | 1.036 us | 1.817 us | 2.17x |
| `structural/method-family-call` | 9.721 us | 4.883 us | 17.734 us | 1.99x |
| `lokad/lexer/number` | 7.062 us | 3.702 us | 4.839 us | 1.91x |
| `structural/modifier-family-to-type-window` | 100.957 us | 53.726 us | 68.389 us | 1.88x |
| `structural/keyword-family-to-capitalized-identifier` | 13.998 us | 7.525 us | 20.978 us | 1.86x |
| `lokad/imports/module-imports` | 7.585 us | 4.332 us | 27.692 us | 1.75x |
| `lokad/langserv/color-short-hex` | 0.812 us | 0.471 us | 0.674 us | 1.72x |
| `lokad/langserv/color-rgb-hex` | 1.407 us | 0.839 us | 1.087 us | 1.68x |
| `lokad/langserv/url-dashboard` | 9.829 us | 6.218 us | 6.622 us | 1.58x |
| `structural/ordered-identifier-window` | 14.546 us | 10.377 us | 23.450 us | 1.40x |
| `structural/ordered-modifier-pair` | 10.166 us | 8.429 us | 20.988 us | 1.21x |
| `lokad/langserv/helper-identifier` | 3.111 us | 2.638 us | 3.496 us | 1.18x |
| `lokad/langserv/url-download` | 11.299 us | 11.884 us | 12.319 us | 0.95x |
| `lokad/lexer/string` | 27.623 us | 29.788 us | 32.185 us | 0.93x |
| `structural/keyword-to-generic-type` | 2.986 us | 3.407 us | 16.346 us | 0.88x |
| `structural/ordered-keyword-pair` | 3.494 us | 4.007 us | 16.767 us | 0.87x |
| `literal-family/type-token-family` | 9.023 us | 11.264 us | 24.010 us | 0.80x |
| `literal/identifier-token-casei` | 3.818 us | 5.736 us | 18.649 us | 0.67x |
| `literal-family/method-token-family` | 2.634 us | 5.020 us | 17.650 us | 0.52x |
| `literal/identifier-token` | 2.275 us | 4.575 us | 16.897 us | 0.50x |
| `literal/call-token` | 1.807 us | 3.857 us | 16.035 us | 0.47x |
| `lokad/folding/region-marker` | 5.080 us | 34.653 us | 60.141 us | 0.15x |
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
