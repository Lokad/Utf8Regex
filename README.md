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
| `literal/sherlock-zh` | 21.992 us | 18.410 us | 2,175.977 us | 1.19x |
| `literal/sherlock-ru` | 68.721 us | 95.272 us | 5,401.936 us | 0.72x |
| `literal-alternate/sherlock-zh` | 42.721 us | 67.846 us | 2,163.759 us | 0.63x |
| `ruff-noqa/tweaked` | 4,073.505 us | 7,637.691 us | 45,230.300 us | 0.53x |
| `literal/sherlock-casei-en` | 79.505 us | 185.968 us | 573.899 us | 0.43x |
| `literal/sherlock-casei-ru` | 151.542 us | 364.257 us | 5,487.000 us | 0.42x |
| `bounded-repeat/letters-en` | 1,080.467 us | 2,760.707 us | 2,925.935 us | 0.39x |
| `literal/sherlock-en` | 31.305 us | 83.767 us | 450.977 us | 0.37x |
| `bounded-repeat/letters-ru` | 2,058.765 us | 5,721.014 us | 6,482.875 us | 0.36x |
| `dictionary/single` | 8,145.230 us | 25,334.400 us | 25,282.867 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,407.959 us | 4,711.056 us | 9,641.050 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 157.756 us | 582.841 us | 1,020.031 us | 0.27x |
| `bounded-repeat/capitals` | 11,383.514 us | 45,556.650 us | 48,896.000 us | 0.25x |
| `literal-alternate/sherlock-en` | 343.392 us | 1,511.588 us | 2,029.705 us | 0.23x |
| `aws-keys/quick` | 1,797.217 us | 9,233.625 us | 45,330.450 us | 0.19x |
| `literal-alternate/sherlock-en-nomatch` | 125.052 us | 643.204 us | 1,058.839 us | 0.19x |
| `aws-keys/full` | 8,530.789 us | 85,065.000 us | 118,740.800 us | 0.10x |
| `bounded-repeat/context` | 8,583.233 us | 145,013.300 us | 148,287.700 us | 0.06x |
| `ruff-noqa/real` | 3,375.722 us | 68,289.400 us | 97,258.400 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 852.754 us | 23,131.300 us | 28,491.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 98.822 us | 11,057.043 us | 11,981.843 us | 0.0089x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 372.796 us | 479.895 us | 8,791.650 us | 0.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.075 us | 0.105 us | 0.140 us | 0.71x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.081 us | 0.097 us | 0.68x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.050 us | 0.074 us | 0.087 us | 0.67x |
| `industry/mariomka-uri-count` | `Count` | 1,162.258 us | 1,813.433 us | 10,170.300 us | 0.64x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.108 us | 0.138 us | 0.64x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.045 us | 0.080 us | 0.094 us | 0.57x |
| `common/email-match` | `IsMatch` | 0.128 us | 0.263 us | 0.288 us | 0.49x |
| `industry/leipzig-twain-count` | `Count` | 624.259 us | 1,304.048 us | 8,532.525 us | 0.48x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.167 us | 0.188 us | 0.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.072 us | 0.167 us | 0.182 us | 0.43x |
| `common/match-word` | `Match` | 0.049 us | 0.127 us | 1.153 us | 0.39x |
| `common/uri-miss` | `IsMatch` | 0.075 us | 0.199 us | 0.220 us | 0.38x |
| `industry/mariomka-ip-count` | `Count` | 3,143.112 us | 8,309.867 us | 16,580.133 us | 0.38x |
| `common/matches-set` | `Count` | 65.632 us | 188.947 us | 190.216 us | 0.35x |
| `common/uri-match` | `IsMatch` | 0.075 us | 0.234 us | 0.268 us | 0.32x |
| `common/ip-miss` | `IsMatch` | 0.219 us | 0.696 us | 0.722 us | 0.32x |
| `common/matches-word` | `Count` | 1.208 us | 4.129 us | 5.910 us | 0.29x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 71.350 us | 256.930 us | 488.082 us | 0.28x |
| `common/replace-words` | `Replace` | 19.936 us | 74.909 us | 78.118 us | 0.27x |
| `common/date-miss` | `IsMatch` | 0.107 us | 0.543 us | 0.576 us | 0.20x |
| `common/matches-boundary` | `Count` | 28.983 us | 165.982 us | 165.195 us | 0.17x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 164.529 us | 949.901 us | 1,241.461 us | 0.17x |
| `common/split-words` | `Split` | 12.926 us | 76.431 us | 78.775 us | 0.17x |
| `common/matches-words` | `Count` | 9.030 us | 67.841 us | 68.742 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,054.844 us | 9,737.225 us | 16,480.833 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,901.090 us | 19,150.100 us | 19,928.100 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 863.345 us | 11,640.829 us | 12,319.257 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.029 us | 0.593 us | 0.619 us | 0.05x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.804 us | 0.842 us | 0.04x |
| `industry/leipzig-symbol-count` | `Count` | 992.183 us | 22,451.250 us | 29,646.350 us | 0.04x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.338 us | 0.360 us | 0.04x |
| `industry/leipzig-river-window-count` | `Count` | 1,202.560 us | 31,272.167 us | 39,132.150 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 262.188 us | 8,233.320 us | 8,609.500 us | 0.03x |
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
| `literal-alternate/sherlock-zh` | 42.571 us | 35.478 us | 2,116.722 us | 1.20x |
| `bounded-repeat/letters-ru` | 2,067.632 us | 2,014.142 us | 2,843.944 us | 1.03x |
| `literal/sherlock-casei-en` | 79.162 us | 77.293 us | 422.736 us | 1.02x |
| `literal-alternate/sherlock-en` | 342.955 us | 358.425 us | 748.256 us | 0.96x |
| `literal/sherlock-zh` | 21.880 us | 23.411 us | 2,151.108 us | 0.93x |
| `ruff-noqa/tweaked` | 4,092.837 us | 4,558.388 us | 40,971.050 us | 0.90x |
| `literal-alternate/sherlock-en-mixed` | 154.299 us | 174.567 us | 643.251 us | 0.88x |
| `bounded-repeat/capitals` | 11,421.314 us | 13,213.450 us | 16,266.450 us | 0.86x |
| `literal/sherlock-casei-ru` | 152.029 us | 195.074 us | 5,211.487 us | 0.78x |
| `literal/sherlock-ru` | 67.722 us | 87.904 us | 5,395.247 us | 0.77x |
| `literal-alternate/sherlock-casei-en` | 97.081 us | 142.883 us | 566.658 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 124.946 us | 199.284 us | 567.328 us | 0.63x |
| `literal-alternate/sherlock-ru` | 1,412.147 us | 2,276.179 us | 7,309.483 us | 0.62x |
| `bounded-repeat/letters-en` | 1,079.914 us | 1,775.421 us | 1,915.731 us | 0.61x |
| `aws-keys/quick` | 1,822.931 us | 3,631.486 us | 37,162.600 us | 0.50x |
| `aws-keys/full` | 8,729.644 us | 18,182.140 us | 53,885.567 us | 0.48x |
| `literal-alternate/sherlock-casei-ru` | 848.100 us | 1,920.142 us | 6,986.875 us | 0.44x |
| `literal/sherlock-en` | 31.064 us | 74.215 us | 427.636 us | 0.42x |
| `dictionary/single` | 8,103.570 us | 44,853.600 us | 45,087.100 us | 0.18x |
| `ruff-noqa/real` | 3,400.396 us | 20,558.925 us | 51,475.600 us | 0.17x |
| `bounded-repeat/context` | 8,615.233 us | 67,700.700 us | 70,883.450 us | 0.13x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.127 us | 0.078 us | 0.105 us | 1.62x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.074 us | 0.046 us | 0.083 us | 1.60x |
| `common/date-match` | `IsMatch` | 0.069 us | 0.043 us | 0.073 us | 1.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.045 us | 0.030 us | 0.048 us | 1.53x |
| `common/ip-match` | `IsMatch` | 0.069 us | 0.047 us | 0.068 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.069 us | 0.054 us | 0.073 us | 1.29x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.038 us | 0.029 us | 0.045 us | 1.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.042 us | 0.033 us | 0.051 us | 1.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 70.757 us | 56.613 us | 254.099 us | 1.25x |
| `common/uri-miss` | `IsMatch` | 0.075 us | 0.062 us | 0.086 us | 1.21x |
| `common/ip-miss` | `IsMatch` | 0.212 us | 0.176 us | 0.200 us | 1.20x |
| `common/replace-words` | `Replace` | 19.964 us | 17.101 us | 18.162 us | 1.17x |
| `industry/mariomka-uri-count` | `Count` | 1,152.374 us | 1,071.962 us | 8,123.250 us | 1.08x |
| `industry/mariomka-ip-count` | `Count` | 3,192.108 us | 3,377.517 us | 10,630.767 us | 0.95x |
| `common/uri-match` | `IsMatch` | 0.075 us | 0.085 us | 0.118 us | 0.88x |
| `common/matches-words` | `Count` | 9.025 us | 10.582 us | 13.740 us | 0.85x |
| `industry/mariomka-email-count` | `Count` | 376.238 us | 448.416 us | 7,489.075 us | 0.84x |
| `common/backtracking` | `IsMatch` | 0.036 us | 0.043 us | 0.072 us | 0.83x |
| `common/match-word` | `Match` | 0.049 us | 0.060 us | 0.927 us | 0.82x |
| `industry/leipzig-river-window-count` | `Count` | 1,208.592 us | 1,500.592 us | 8,076.400 us | 0.81x |
| `industry/leipzig-symbol-count` | `Count` | 984.425 us | 1,262.031 us | 7,186.000 us | 0.78x |
| `common/split-words` | `Split` | 12.950 us | 20.060 us | 22.082 us | 0.65x |
| `common/matches-set` | `Count` | 65.336 us | 102.639 us | 104.853 us | 0.64x |
| `common/date-miss` | `IsMatch` | 0.108 us | 0.194 us | 0.232 us | 0.56x |
| `common/matches-word` | `Count` | 1.210 us | 2.316 us | 3.421 us | 0.52x |
| `industry/leipzig-twain-count` | `Count` | 624.852 us | 1,287.808 us | 6,610.500 us | 0.49x |
| `industry/leipzig-name-family-count` | `Count` | 1,060.869 us | 2,975.084 us | 8,203.950 us | 0.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 163.738 us | 580.675 us | 857.038 us | 0.28x |
| `common/matches-boundary` | `Count` | 28.820 us | 103.246 us | 104.457 us | 0.28x |
| `common/one-node-backtracking` | `IsMatch` | 0.028 us | 0.104 us | 0.126 us | 0.27x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,919.312 us | 8,538.589 us | 9,426.511 us | 0.22x |
| `industry/rust-sherlock-ing-count` | `Count` | 860.168 us | 4,672.612 us | 5,090.147 us | 0.18x |
| `common/email-miss` | `IsMatch` | 0.015 us | 0.087 us | 0.114 us | 0.17x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 261.755 us | 3,857.915 us | 4,186.205 us | 0.07x |
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
| `fallback/lookahead` | 5.325 us | 5.743 us | 18.580 us | 0.93x |
| `lokad/imports/module-imports` | 5.664 us | 6.172 us | 30.221 us | 0.92x |
| `structural/method-family-call` | 6.841 us | 7.753 us | 21.134 us | 0.88x |
| `structural/method-family-generic-call` | 5.259 us | 6.433 us | 19.681 us | 0.82x |
| `lokad/langserv/helper-identifier` | 3.625 us | 4.455 us | 5.329 us | 0.81x |
| `lokad/lexer/identifier` | 10.570 us | 13.253 us | 16.112 us | 0.80x |
| `structural/keyword-to-generic-type` | 2.913 us | 3.968 us | 17.336 us | 0.73x |
| `structural/ordered-keyword-window` | 11.982 us | 17.122 us | 30.036 us | 0.70x |
| `lokad/langserv/color-short-hex` | 0.481 us | 0.768 us | 0.957 us | 0.63x |
| `structural/ordered-modifier-pair` | 9.908 us | 16.750 us | 29.633 us | 0.59x |
| `structural/ordered-keyword-pair` | 3.098 us | 5.253 us | 18.206 us | 0.59x |
| `lokad/langserv/identifier-validator` | 3.956 us | 7.255 us | 8.709 us | 0.55x |
| `lokad/lexer/doc-line` | 0.993 us | 1.822 us | 2.516 us | 0.55x |
| `literal/identifier-token` | 2.246 us | 4.153 us | 16.459 us | 0.54x |
| `lokad/docs/inline-doc-prefix` | 1.679 us | 3.362 us | 4.063 us | 0.50x |
| `lokad/lexer/operator-run` | 0.994 us | 2.223 us | 2.655 us | 0.45x |
| `literal/call-token` | 1.779 us | 4.117 us | 16.599 us | 0.43x |
| `lokad/style/hex-color` | 0.203 us | 0.492 us | 0.574 us | 0.41x |
| `lokad/style/cell-ref` | 0.754 us | 1.827 us | 2.057 us | 0.41x |
| `lokad/style/range-ref` | 0.713 us | 1.747 us | 1.898 us | 0.41x |
| `lokad/langserv/url-dashboard` | 9.235 us | 23.359 us | 23.782 us | 0.40x |
| `literal-family/type-token-family` | 8.829 us | 22.844 us | 36.214 us | 0.39x |
| `lokad/lexer/string` | 29.759 us | 79.095 us | 84.145 us | 0.38x |
| `lokad/langserv/color-rgb-hex` | 0.601 us | 1.733 us | 1.986 us | 0.35x |
| `structural/ordered-identifier-window` | 8.466 us | 27.993 us | 41.161 us | 0.30x |
| `literal-family/method-token-family` | 2.518 us | 8.984 us | 21.409 us | 0.28x |
| `lokad/lexer/number` | 2.729 us | 10.555 us | 11.251 us | 0.26x |
| `literal/identifier-token-casei` | 3.754 us | 14.774 us | 27.643 us | 0.25x |
| `structural/modifier-family-to-type-window` | 56.992 us | 244.132 us | 257.216 us | 0.23x |
| `lokad/langserv/url-download` | 11.028 us | 50.806 us | 51.631 us | 0.22x |
| `lokad/folding/region-marker` | 5.502 us | 34.168 us | 60.918 us | 0.16x |
| `structural/keyword-family-to-capitalized-identifier` | 10.702 us | 213.906 us | 226.703 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 2.803 us | 124.846 us | 138.522 us | 0.02x |
| `balancing/begin-end-blocks` | 45.314 us | 4,256.056 us | 4,250.578 us | 0.01x |
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
| `lokad/langserv/url-dashboard` | 9.146 us | 6.036 us | 6.568 us | 1.52x |
| `structural/ordered-keyword-window` | 12.034 us | 8.351 us | 20.919 us | 1.44x |
| `structural/keyword-family-to-capitalized-identifier` | 10.752 us | 7.558 us | 20.392 us | 1.42x |
| `structural/method-family-call` | 6.875 us | 4.867 us | 17.814 us | 1.41x |
| `lokad/langserv/helper-identifier` | 3.617 us | 2.571 us | 3.452 us | 1.41x |
| `lokad/imports/module-imports` | 5.738 us | 4.233 us | 27.487 us | 1.36x |
| `lokad/lexer/identifier` | 10.510 us | 7.826 us | 10.354 us | 1.34x |
| `fallback/lookahead` | 5.337 us | 4.039 us | 16.392 us | 1.32x |
| `lokad/docs/inline-doc-prefix` | 1.688 us | 1.395 us | 2.125 us | 1.21x |
| `structural/ordered-modifier-pair` | 9.963 us | 8.317 us | 21.073 us | 1.20x |
| `structural/method-family-generic-call` | 5.211 us | 4.413 us | 17.152 us | 1.18x |
| `lokad/lexer/operator-run` | 0.977 us | 0.873 us | 1.342 us | 1.12x |
| `structural/modifier-family-to-type-window` | 57.426 us | 53.966 us | 67.430 us | 1.06x |
| `lokad/style/range-ref` | 0.711 us | 0.701 us | 0.828 us | 1.01x |
| `lokad/lexer/string` | 29.547 us | 29.562 us | 32.415 us | 1.00x |
| `lokad/lexer/doc-line` | 1.000 us | 1.012 us | 1.784 us | 0.99x |
| `lokad/langserv/color-short-hex` | 0.442 us | 0.462 us | 0.650 us | 0.96x |
| `lokad/style/cell-ref` | 0.755 us | 0.794 us | 1.012 us | 0.95x |
| `lokad/langserv/identifier-validator` | 3.948 us | 4.172 us | 5.577 us | 0.95x |
| `lokad/langserv/url-download` | 11.063 us | 11.728 us | 12.339 us | 0.94x |
| `structural/keyword-to-generic-type` | 2.899 us | 3.358 us | 16.070 us | 0.86x |
| `lokad/style/hex-color` | 0.187 us | 0.219 us | 0.298 us | 0.86x |
| `structural/ordered-identifier-window` | 8.418 us | 10.169 us | 23.505 us | 0.83x |
| `literal-family/type-token-family` | 8.857 us | 10.980 us | 23.604 us | 0.81x |
| `structural/ordered-keyword-pair` | 3.093 us | 3.999 us | 16.482 us | 0.77x |
| `lokad/lexer/number` | 2.800 us | 3.685 us | 4.771 us | 0.76x |
| `lokad/langserv/color-rgb-hex` | 0.558 us | 0.804 us | 1.092 us | 0.69x |
| `literal/identifier-token-casei` | 3.754 us | 5.615 us | 18.632 us | 0.67x |
| `literal-family/method-token-family` | 2.582 us | 4.934 us | 17.592 us | 0.52x |
| `literal/identifier-token` | 2.247 us | 4.505 us | 16.282 us | 0.50x |
| `literal/call-token` | 1.759 us | 3.791 us | 15.598 us | 0.46x |
| `structural/modifier-family-to-type-pair` | 2.851 us | 10.424 us | 23.430 us | 0.27x |
| `lokad/folding/region-marker` | 5.555 us | 34.512 us | 59.903 us | 0.16x |
| `balancing/begin-end-blocks` | 46.753 us | 450.382 us | 481.393 us | 0.10x |
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
