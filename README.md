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
| `literal/sherlock-zh` | 21.875 us | 18.680 us | 2,179.662 us | 1.17x |
| `literal/sherlock-ru` | 66.190 us | 96.184 us | 5,206.171 us | 0.69x |
| `literal-alternate/sherlock-zh` | 42.508 us | 66.820 us | 2,168.677 us | 0.64x |
| `aws-keys/full` | 49,149.550 us | 84,692.700 us | 116,185.300 us | 0.58x |
| `aws-keys/quick` | 5,113.873 us | 9,804.663 us | 43,172.850 us | 0.52x |
| `ruff-noqa/tweaked` | 3,864.590 us | 7,513.927 us | 41,220.500 us | 0.51x |
| `literal/sherlock-casei-en` | 78.247 us | 185.948 us | 615.784 us | 0.42x |
| `literal/sherlock-casei-ru` | 146.326 us | 360.804 us | 5,535.921 us | 0.41x |
| `bounded-repeat/letters-en` | 1,114.275 us | 2,786.346 us | 2,901.050 us | 0.40x |
| `literal/sherlock-en` | 31.162 us | 85.721 us | 466.710 us | 0.36x |
| `bounded-repeat/letters-ru` | 2,010.089 us | 5,734.508 us | 6,648.317 us | 0.35x |
| `dictionary/single` | 8,018.360 us | 25,640.433 us | 25,638.133 us | 0.31x |
| `literal-alternate/sherlock-ru` | 1,426.349 us | 4,766.144 us | 9,869.750 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 154.206 us | 605.908 us | 1,037.182 us | 0.25x |
| `bounded-repeat/capitals` | 11,011.000 us | 44,396.950 us | 48,067.450 us | 0.25x |
| `literal-alternate/sherlock-en` | 346.395 us | 1,506.980 us | 2,044.358 us | 0.23x |
| `literal-alternate/sherlock-en-nomatch` | 125.860 us | 639.719 us | 1,052.429 us | 0.20x |
| `bounded-repeat/context` | 11,084.243 us | 145,794.400 us | 149,843.400 us | 0.08x |
| `ruff-noqa/real` | 3,392.870 us | 68,549.500 us | 100,642.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 830.287 us | 22,726.950 us | 28,193.100 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 101.212 us | 11,093.000 us | 12,051.600 us | 0.0091x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.118 us | 0.076 us | 0.090 us | 1.56x |
| `common/date-match` | `IsMatch` | 0.158 us | 0.106 us | 0.134 us | 1.50x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.117 us | 0.081 us | 0.096 us | 1.44x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.114 us | 0.083 us | 0.099 us | 1.38x |
| `common/uri-match` | `IsMatch` | 0.283 us | 0.226 us | 0.264 us | 1.25x |
| `common/ip-match` | `IsMatch` | 0.214 us | 0.175 us | 0.193 us | 1.23x |
| `common/email-match` | `IsMatch` | 0.325 us | 0.267 us | 0.293 us | 1.21x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.111 us | 0.109 us | 0.142 us | 1.03x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.165 us | 0.166 us | 0.182 us | 1.00x |
| `common/uri-miss` | `IsMatch` | 0.192 us | 0.200 us | 0.219 us | 0.96x |
| `industry/mariomka-email-count` | `Count` | 423.793 us | 490.504 us | 8,874.100 us | 0.86x |
| `industry/mariomka-uri-count` | `Count` | 1,588.565 us | 1,845.987 us | 9,789.375 us | 0.86x |
| `common/ip-miss` | `IsMatch` | 0.576 us | 0.701 us | 0.743 us | 0.82x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 153.943 us | 251.664 us | 487.249 us | 0.61x |
| `common/date-miss` | `IsMatch` | 0.321 us | 0.554 us | 0.593 us | 0.58x |
| `industry/leipzig-twain-count` | `Count` | 620.990 us | 1,288.784 us | 8,509.720 us | 0.48x |
| `common/match-word` | `Match` | 0.058 us | 0.170 us | 1.149 us | 0.34x |
| `common/matches-set` | `Count` | 60.942 us | 190.149 us | 192.530 us | 0.32x |
| `common/matches-word` | `Count` | 1.219 us | 4.161 us | 5.862 us | 0.29x |
| `common/replace-words` | `Replace` | 20.636 us | 74.244 us | 77.325 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,204.486 us | 8,470.056 us | 16,738.367 us | 0.26x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.878 us | 951.781 us | 1,213.703 us | 0.17x |
| `common/split-words` | `Split` | 13.023 us | 77.763 us | 79.390 us | 0.17x |
| `common/matches-boundary` | `Count` | 24.438 us | 160.483 us | 161.475 us | 0.15x |
| `common/matches-words` | `Count` | 9.055 us | 68.156 us | 69.231 us | 0.13x |
| `industry/leipzig-name-family-count` | `Count` | 1,036.378 us | 9,775.212 us | 17,236.000 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,910.365 us | 19,138.775 us | 19,901.975 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,004.422 us | 11,801.971 us | 12,305.514 us | 0.09x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.340 us | 0.363 us | 0.08x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.823 us | 0.875 us | 0.07x |
| `common/one-node-backtracking` | `IsMatch` | 0.038 us | 0.594 us | 0.623 us | 0.06x |
| `industry/leipzig-river-window-count` | `Count` | 1,794.557 us | 31,404.467 us | 39,195.350 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 995.553 us | 22,887.425 us | 30,547.950 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 301.255 us | 8,404.389 us | 8,942.311 us | 0.04x |
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
| `aws-keys/full` | 49,021.950 us | 18,154.380 us | 50,428.500 us | 2.70x |
| `aws-keys/quick` | 5,189.360 us | 3,680.310 us | 35,090.450 us | 1.41x |
| `literal-alternate/sherlock-zh` | 42.719 us | 35.211 us | 2,123.340 us | 1.21x |
| `literal/sherlock-casei-en` | 77.959 us | 77.790 us | 437.593 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,998.905 us | 2,013.155 us | 2,903.392 us | 0.99x |
| `literal-alternate/sherlock-en` | 349.083 us | 363.880 us | 754.547 us | 0.96x |
| `literal/sherlock-zh` | 22.049 us | 23.789 us | 2,185.105 us | 0.93x |
| `literal-alternate/sherlock-en-mixed` | 157.093 us | 173.296 us | 551.026 us | 0.91x |
| `ruff-noqa/tweaked` | 3,901.590 us | 4,473.783 us | 36,286.050 us | 0.87x |
| `bounded-repeat/capitals` | 11,121.929 us | 13,248.033 us | 16,174.050 us | 0.84x |
| `literal/sherlock-ru` | 66.206 us | 86.763 us | 5,146.380 us | 0.76x |
| `literal-alternate/sherlock-casei-en` | 101.218 us | 142.735 us | 562.619 us | 0.71x |
| `literal/sherlock-casei-ru` | 142.290 us | 222.164 us | 5,325.707 us | 0.64x |
| `literal-alternate/sherlock-en-nomatch` | 125.798 us | 199.904 us | 575.858 us | 0.63x |
| `bounded-repeat/letters-en` | 1,108.613 us | 1,795.155 us | 1,921.305 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,411.039 us | 2,314.939 us | 7,337.517 us | 0.61x |
| `literal-alternate/sherlock-casei-ru` | 824.451 us | 1,935.146 us | 7,211.675 us | 0.43x |
| `literal/sherlock-en` | 31.082 us | 75.198 us | 424.231 us | 0.41x |
| `dictionary/single` | 8,031.810 us | 45,072.550 us | 44,989.750 us | 0.18x |
| `ruff-noqa/real` | 3,382.382 us | 20,421.975 us | 55,103.900 us | 0.17x |
| `bounded-repeat/context` | 11,265.043 us | 68,370.300 us | 71,359.200 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.217 us | 0.048 us | 0.069 us | 4.55x |
| `common/email-match` | `IsMatch` | 0.325 us | 0.079 us | 0.107 us | 4.11x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.117 us | 0.031 us | 0.048 us | 3.75x |
| `common/date-match` | `IsMatch` | 0.159 us | 0.043 us | 0.071 us | 3.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.115 us | 0.033 us | 0.051 us | 3.54x |
| `common/uri-match` | `IsMatch` | 0.283 us | 0.087 us | 0.118 us | 3.26x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.118 us | 0.036 us | 0.054 us | 3.25x |
| `common/ip-miss` | `IsMatch` | 0.571 us | 0.178 us | 0.202 us | 3.20x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.165 us | 0.054 us | 0.075 us | 3.06x |
| `common/uri-miss` | `IsMatch` | 0.191 us | 0.066 us | 0.087 us | 2.90x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 152.865 us | 56.607 us | 270.847 us | 2.70x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.112 us | 0.048 us | 0.082 us | 2.34x |
| `common/date-miss` | `IsMatch` | 0.320 us | 0.197 us | 0.236 us | 1.63x |
| `industry/mariomka-uri-count` | `Count` | 1,594.945 us | 1,110.016 us | 8,359.725 us | 1.44x |
| `common/backtracking` | `IsMatch` | 0.056 us | 0.043 us | 0.073 us | 1.30x |
| `industry/leipzig-river-window-count` | `Count` | 1,817.005 us | 1,485.744 us | 8,129.400 us | 1.22x |
| `common/replace-words` | `Replace` | 20.681 us | 17.119 us | 20.154 us | 1.21x |
| `industry/mariomka-email-count` | `Count` | 424.549 us | 456.398 us | 7,715.425 us | 0.93x |
| `common/matches-words` | `Count` | 9.007 us | 10.743 us | 13.872 us | 0.84x |
| `industry/leipzig-symbol-count` | `Count` | 1,011.243 us | 1,229.570 us | 7,373.960 us | 0.82x |
| `common/match-word` | `Match` | 0.058 us | 0.084 us | 0.984 us | 0.70x |
| `common/split-words` | `Split` | 13.139 us | 20.651 us | 22.377 us | 0.64x |
| `industry/mariomka-ip-count` | `Count` | 2,191.811 us | 3,452.743 us | 10,564.833 us | 0.63x |
| `common/matches-set` | `Count` | 61.264 us | 103.123 us | 104.805 us | 0.59x |
| `common/matches-word` | `Count` | 1.210 us | 2.283 us | 3.508 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 624.191 us | 1,272.797 us | 7,269.844 us | 0.49x |
| `common/one-node-backtracking` | `IsMatch` | 0.039 us | 0.088 us | 0.128 us | 0.44x |
| `industry/leipzig-name-family-count` | `Count` | 1,045.690 us | 3,011.000 us | 8,379.275 us | 0.35x |
| `common/email-miss` | `IsMatch` | 0.027 us | 0.087 us | 0.113 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 165.332 us | 584.692 us | 844.047 us | 0.28x |
| `common/matches-boundary` | `Count` | 24.353 us | 102.583 us | 104.017 us | 0.24x |
| `industry/rust-sherlock-ing-count` | `Count` | 995.905 us | 4,729.619 us | 5,120.120 us | 0.21x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,890.388 us | 17,682.560 us | 12,663.917 us | 0.11x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 299.894 us | 3,901.365 us | 4,291.433 us | 0.08x |
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
| `lokad/langserv/identifier-validator` | 25.004 us | 12.430 us | 13.931 us | 2.01x |
| `lokad/docs/inline-doc-prefix` | 9.404 us | 4.694 us | 5.390 us | 2.00x |
| `lokad/lexer/number` | 26.725 us | 13.526 us | 14.912 us | 1.98x |
| `lokad/lexer/doc-line` | 5.642 us | 3.014 us | 3.614 us | 1.87x |
| `lokad/lexer/identifier` | 41.359 us | 22.621 us | 25.429 us | 1.83x |
| `fallback/lookahead` | 10.558 us | 5.880 us | 18.395 us | 1.80x |
| `lokad/style/hex-color` | 1.384 us | 0.822 us | 0.881 us | 1.68x |
| `lokad/lexer/string` | 160.075 us | 95.265 us | 100.154 us | 1.68x |
| `structural/method-family-generic-call` | 10.846 us | 6.636 us | 20.283 us | 1.63x |
| `lokad/langserv/color-rgb-hex` | 4.570 us | 2.942 us | 2.951 us | 1.55x |
| `lokad/langserv/color-short-hex` | 2.093 us | 1.349 us | 1.441 us | 1.55x |
| `lokad/langserv/helper-identifier` | 11.536 us | 7.449 us | 8.070 us | 1.55x |
| `lokad/lexer/operator-run` | 4.891 us | 3.449 us | 3.780 us | 1.42x |
| `lokad/style/range-ref` | 3.594 us | 2.612 us | 2.736 us | 1.38x |
| `lokad/imports/module-imports` | 8.185 us | 6.267 us | 31.335 us | 1.31x |
| `structural/ordered-keyword-window` | 21.616 us | 17.301 us | 30.409 us | 1.25x |
| `lokad/style/cell-ref` | 3.405 us | 2.754 us | 2.912 us | 1.24x |
| `structural/method-family-call` | 9.506 us | 7.911 us | 21.180 us | 1.20x |
| `structural/keyword-to-generic-type` | 3.046 us | 3.977 us | 17.617 us | 0.77x |
| `structural/ordered-keyword-pair` | 3.543 us | 5.282 us | 18.776 us | 0.67x |
| `literal/identifier-token` | 2.694 us | 4.299 us | 16.950 us | 0.63x |
| `structural/ordered-modifier-pair` | 10.028 us | 17.024 us | 30.112 us | 0.59x |
| `structural/ordered-identifier-window` | 15.365 us | 28.414 us | 41.280 us | 0.54x |
| `lokad/langserv/url-dashboard` | 12.267 us | 23.836 us | 24.336 us | 0.51x |
| `literal/call-token` | 1.797 us | 4.240 us | 17.133 us | 0.42x |
| `literal-family/method-token-family` | 3.732 us | 8.944 us | 21.743 us | 0.42x |
| `structural/modifier-family-to-type-window` | 103.616 us | 252.189 us | 267.556 us | 0.41x |
| `literal-family/type-token-family` | 9.781 us | 24.585 us | 39.130 us | 0.40x |
| `balancing/begin-end-blocks` | 1,479.060 us | 4,298.683 us | 4,335.917 us | 0.34x |
| `literal/identifier-token-casei` | 3.877 us | 15.687 us | 28.226 us | 0.25x |
| `lokad/langserv/url-download` | 12.213 us | 52.230 us | 52.974 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 20.504 us | 131.948 us | 146.510 us | 0.16x |
| `lokad/folding/region-marker` | 5.275 us | 35.221 us | 63.364 us | 0.15x |
| `structural/keyword-family-to-capitalized-identifier` | 26.567 us | 213.604 us | 227.876 us | 0.12x |
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
| `lokad/lexer/number` | 26.944 us | 6.906 us | 8.130 us | 3.90x |
| `structural/keyword-family-to-capitalized-identifier` | 26.662 us | 7.604 us | 21.236 us | 3.51x |
| `lokad/docs/inline-doc-prefix` | 9.329 us | 2.707 us | 3.528 us | 3.45x |
| `lokad/lexer/string` | 160.993 us | 46.839 us | 50.365 us | 3.44x |
| `balancing/begin-end-blocks` | 1,483.575 us | 467.707 us | 496.769 us | 3.17x |
| `lokad/style/range-ref` | 4.036 us | 1.468 us | 1.583 us | 2.75x |
| `lokad/langserv/identifier-validator` | 25.094 us | 9.142 us | 11.145 us | 2.74x |
| `lokad/style/hex-color` | 1.385 us | 0.506 us | 0.591 us | 2.73x |
| `fallback/lookahead` | 10.667 us | 4.126 us | 16.234 us | 2.59x |
| `lokad/langserv/color-rgb-hex` | 4.558 us | 1.783 us | 1.965 us | 2.56x |
| `lokad/lexer/identifier` | 41.834 us | 16.390 us | 20.579 us | 2.55x |
| `structural/ordered-keyword-window` | 21.490 us | 8.452 us | 21.160 us | 2.54x |
| `lokad/lexer/doc-line` | 5.709 us | 2.248 us | 2.969 us | 2.54x |
| `lokad/lexer/operator-run` | 4.916 us | 2.004 us | 2.524 us | 2.45x |
| `structural/method-family-generic-call` | 10.821 us | 4.545 us | 17.378 us | 2.38x |
| `lokad/langserv/helper-identifier` | 11.531 us | 4.877 us | 5.925 us | 2.36x |
| `lokad/langserv/color-short-hex` | 2.092 us | 0.969 us | 1.126 us | 2.16x |
| `structural/modifier-family-to-type-pair` | 20.600 us | 10.604 us | 23.067 us | 1.94x |
| `structural/method-family-call` | 9.524 us | 4.944 us | 17.646 us | 1.93x |
| `lokad/style/cell-ref` | 3.398 us | 1.769 us | 1.946 us | 1.92x |
| `lokad/imports/module-imports` | 8.211 us | 4.346 us | 28.557 us | 1.89x |
| `structural/modifier-family-to-type-window` | 103.965 us | 55.213 us | 69.447 us | 1.88x |
| `lokad/langserv/url-dashboard` | 12.284 us | 7.258 us | 7.832 us | 1.69x |
| `structural/ordered-identifier-window` | 15.444 us | 10.719 us | 24.008 us | 1.44x |
| `structural/ordered-modifier-pair` | 10.105 us | 8.448 us | 21.057 us | 1.20x |
| `lokad/langserv/url-download` | 12.226 us | 12.626 us | 13.226 us | 0.97x |
| `structural/keyword-to-generic-type` | 3.045 us | 3.414 us | 16.408 us | 0.89x |
| `structural/ordered-keyword-pair` | 3.563 us | 4.046 us | 16.908 us | 0.88x |
| `literal-family/type-token-family` | 9.789 us | 11.500 us | 24.579 us | 0.85x |
| `literal-family/method-token-family` | 3.754 us | 5.182 us | 17.731 us | 0.72x |
| `literal/identifier-token-casei` | 3.866 us | 5.763 us | 18.814 us | 0.67x |
| `literal/identifier-token` | 2.683 us | 4.196 us | 16.773 us | 0.64x |
| `literal/call-token` | 1.801 us | 3.889 us | 16.148 us | 0.46x |
| `lokad/folding/region-marker` | 5.228 us | 36.250 us | 62.448 us | 0.14x |
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
