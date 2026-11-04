# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and both optional adapters are fully managed .NET libraries.

## Quick start

```csharp
using Lokad.Utf8Regex;
using System.Text.RegularExpressions;

var regex = new Utf8Regex(@"[A-Z]{2}\d{4}", RegexOptions.CultureInvariant);
ReadOnlySpan<byte> input = "Order AB1234 is ready."u8;

bool found = regex.IsMatch(input);              // true
int count = regex.Count(input);                 // 1
Utf8ValueMatch first = regex.Match(input);       // byte index 6, length 6
byte[] redacted = regex.Replace(input, "[order]");
```

Use `EnumerateMatches` to visit matches and `MatchDetailed` for groups and
capture history. Match byte offsets and lengths can be used directly with UTF-8
slices.

## Choose a profile

| Package | Matching profile |
| --- | --- |
| [Lokad.Utf8Regex](docs/SPEC.md) | .NET-style grammar, captures/history, backreferences, lookarounds, RTL and a NonBacktracking subset |
| [Lokad.Utf8Regex.PythonRe](src/Lokad.Utf8Regex.PythonRe/README.md) | Python re-style grammar, capture numbering and result shaping |
| [Lokad.Utf8Regex.Pcre2](src/Lokad.Utf8Regex.Pcre2/README.md) | Managed PCRE2-style grammar, capture slots, substitution and partial probing |

Both adapters require the matching Core package version. Their specifications
define the supported grammar and operation boundaries.

## Unicode and input

Matching uses **Unicode scalars**. For example,
`new Utf8Regex(".").Count("A😀"u8)` returns 2; the emoji occupies four bytes.
Classes, quantifiers, captures and empty-match advancement respect scalar boundaries.

The entire subject must be valid UTF-8, including text after an early match.
Byte coordinates are primary; UTF-16 coordinates and strings are derived
conveniences. Keep borrowed input unchanged and its owner alive while a view or
cursor uses it. Owned snapshots remain valid after cursor advance or disposal.

## Cancellation and limits

Instances accept cancellation tokens, timeouts and optional work, depth, working-
capacity and result-capacity limits:

```csharp
var bounded = new Utf8Regex(
    @"(?<word>\w+)", RegexOptions.CultureInvariant,
    matchTimeout: TimeSpan.FromSeconds(1),
    resourcePolicy: new Utf8ResourcePolicy
    {
        MaxWorkSteps = 1_000_000, MaxDepth = 256,
        MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
    });

using var cancellation = new CancellationTokenSource();
int count = bounded.Count("café tea"u8, cancellation.Token); // 2
```

These caps are examples. Defaults and static helpers have unlimited invocation
budgets. Byte limits cover modeled engine/result capacities, not total process
memory. `Compile` offers separate construction controls.
See the [resource contract](docs/SPEC.md#cancellation-deadlines-and-resource-limits)
for failure, deadline and ownership details.

See [release notes](CHANGELOG.md) and the [migration guide](docs/MIGRATION.md)
for the 0.4.0 changes.

## Runtime support

The 0.5.0 development source targets `net10.0` and `net11.0`. .NET 11 currently
uses the release-candidate SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The tables were refreshed on **4 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **43m21s**. The [run receipt](bench/Experiments/DeliveryDev21-20261104/full-refresh.json)
records the scope and timings; package and consumer qualification are separate.
Each row retains its recorded implementation. Selective updates do not imply a
new complete run; the full-run receipt applies to its recorded revision.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

[refresh-benchmarks.ps1](refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](bench.ps1) supports focused single-case iterations.

Profile comparisons are in the
[PythonRe](src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md) and
[PCRE2](src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md) benchmark pages.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 133.555 us | 19.016 us | 2,188.388 us | 7.02x |
| `literal-alternate/sherlock-zh` | 242.218 us | 69.540 us | 2,220.826 us | 3.48x |
| `literal/sherlock-ru` | 252.035 us | 98.739 us | 5,287.610 us | 2.55x |
| `ruff-noqa/tweaked` | 10,659.640 us | 8,000.643 us | 43,014.250 us | 1.33x |
| `literal/sherlock-casei-en` | 171.630 us | 203.349 us | 609.623 us | 0.84x |
| `literal/sherlock-en` | 69.721 us | 88.084 us | 474.146 us | 0.79x |
| `aws-keys/quick` | 6,771.100 us | 10,161.260 us | 43,721.700 us | 0.67x |
| `literal/sherlock-casei-ru` | 255.276 us | 393.721 us | 5,762.067 us | 0.65x |
| `bounded-repeat/letters-en` | 1,075.068 us | 2,824.672 us | 2,920.418 us | 0.38x |
| `literal-alternate/sherlock-ru` | 1,736.783 us | 4,745.818 us | 9,927.167 us | 0.37x |
| `literal-alternate/sherlock-en-mixed` | 212.268 us | 607.800 us | 1,038.687 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,983.704 us | 6,013.944 us | 6,801.663 us | 0.33x |
| `dictionary/single` | 8,263.286 us | 26,408.200 us | 26,411.300 us | 0.31x |
| `literal-alternate/sherlock-en` | 414.624 us | 1,532.882 us | 2,072.059 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 168.206 us | 685.054 us | 1,120.127 us | 0.25x |
| `bounded-repeat/capitals` | 9,614.967 us | 46,117.250 us | 53,603.200 us | 0.21x |
| `aws-keys/full` | 10,878.720 us | 86,645.800 us | 118,319.100 us | 0.13x |
| `bounded-repeat/context` | 16,421.267 us | 148,741.100 us | 152,314.600 us | 0.11x |
| `ruff-noqa/real` | 4,611.055 us | 70,597.400 us | 88,287.700 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,091.365 us | 23,119.833 us | 28,513.450 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 224.197 us | 11,162.740 us | 11,984.680 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,526.661 us | 514.433 us | 9,015.667 us | 2.97x |
| `common/match-word` | `Match` | 0.199 us | 0.166 us | 1.134 us | 1.19x |
| `industry/mariomka-uri-count` | `Count` | 1,756.148 us | 1,858.082 us | 10,418.233 us | 0.95x |
| `industry/leipzig-twain-count` | `Count` | 1,266.895 us | 1,354.333 us | 8,847.000 us | 0.94x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.102 us | 0.111 us | 0.148 us | 0.93x |
| `common/replace-words` | `Replace` | 68.182 us | 77.299 us | 79.244 us | 0.88x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 194.771 us | 263.293 us | 506.324 us | 0.74x |
| `common/uri-miss` | `IsMatch` | 0.138 us | 0.189 us | 0.208 us | 0.73x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.084 us | 0.101 us | 0.61x |
| `common/uri-match` | `IsMatch` | 0.116 us | 0.237 us | 0.271 us | 0.49x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.171 us | 0.187 us | 0.46x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.173 us | 0.194 us | 0.44x |
| `industry/leipzig-name-family-count` | `Count` | 4,127.583 us | 9,863.783 us | 16,889.350 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.188 us | 0.208 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.271 us | 0.300 us | 0.36x |
| `industry/mariomka-ip-count` | `Count` | 2,698.926 us | 8,383.800 us | 16,863.000 us | 0.32x |
| `common/matches-word` | `Count` | 1.358 us | 4.266 us | 5.987 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.353 us | 0.375 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.099 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.693 us | 972.400 us | 1,260.064 us | 0.18x |
| `common/split-words` | `Split` | 14.551 us | 79.536 us | 81.031 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.558 us | 0.590 us | 0.16x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.612 us | 0.644 us | 0.13x |
| `common/matches-words` | `Count` | 9.423 us | 73.063 us | 74.611 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.848 us | 0.895 us | 0.11x |
| `industry/leipzig-river-window-count` | `Count` | 3,512.386 us | 33,049.550 us | 46,828.900 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,984.035 us | 19,731.800 us | 20,604.967 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,805.893 us | 23,328.767 us | 30,733.300 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 509.234 us | 8,317.417 us | 8,864.267 us | 0.06x |
| `common/matches-boundary` | `Count` | 8.244 us | 165.128 us | 167.141 us | 0.05x |
| `common/matches-set` | `Count` | 7.885 us | 198.673 us | 201.454 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 282.799 us | 11,973.940 us | 12,556.325 us | 0.02x |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal-alternate/sherlock-zh` | 242.794 us | 36.455 us | 2,160.239 us | 6.66x |
| `literal/sherlock-zh` | 133.884 us | 24.118 us | 2,154.846 us | 5.55x |
| `literal/sherlock-ru` | 248.665 us | 89.786 us | 5,216.000 us | 2.77x |
| `ruff-noqa/tweaked` | 10,734.880 us | 4,611.573 us | 38,529.350 us | 2.33x |
| `literal/sherlock-casei-en` | 171.708 us | 80.985 us | 487.276 us | 2.12x |
| `aws-keys/quick` | 5,969.000 us | 3,716.164 us | 37,190.200 us | 1.61x |
| `literal-alternate/sherlock-casei-en` | 222.460 us | 147.724 us | 534.105 us | 1.51x |
| `literal/sherlock-casei-ru` | 257.580 us | 223.094 us | 5,450.770 us | 1.15x |
| `literal-alternate/sherlock-en-mixed` | 212.126 us | 186.023 us | 618.461 us | 1.14x |
| `literal-alternate/sherlock-en` | 416.862 us | 376.623 us | 806.873 us | 1.11x |
| `bounded-repeat/letters-ru` | 1,984.823 us | 2,074.960 us | 2,891.650 us | 0.96x |
| `literal/sherlock-en` | 69.908 us | 77.380 us | 426.390 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 168.278 us | 202.704 us | 577.887 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,701.280 us | 2,364.259 us | 7,484.167 us | 0.72x |
| `bounded-repeat/capitals` | 9,609.433 us | 13,703.250 us | 21,109.967 us | 0.70x |
| `bounded-repeat/letters-en` | 1,079.138 us | 1,866.714 us | 1,935.696 us | 0.58x |
| `aws-keys/full` | 10,942.620 us | 19,017.300 us | 51,432.300 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 1,090.760 us | 1,981.700 us | 7,362.133 us | 0.55x |
| `bounded-repeat/context` | 16,554.750 us | 69,931.000 us | 72,853.500 us | 0.24x |
| `ruff-noqa/real` | 4,609.164 us | 21,674.067 us | 34,138.300 us | 0.21x |
| `dictionary/single` | 8,258.883 us | 46,335.750 us | 46,314.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/replace-words` | `Replace` | 68.271 us | 18.847 us | 20.496 us | 3.62x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 194.783 us | 60.362 us | 262.743 us | 3.23x |
| `industry/mariomka-email-count` | `Count` | 1,539.924 us | 478.640 us | 7,887.900 us | 3.22x |
| `common/match-word` | `Match` | 0.198 us | 0.078 us | 1.004 us | 2.56x |
| `industry/leipzig-river-window-count` | `Count` | 3,534.036 us | 1,584.515 us | 13,357.600 us | 2.23x |
| `common/backtracking` | `IsMatch` | 0.096 us | 0.044 us | 0.075 us | 2.19x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.103 us | 0.049 us | 0.088 us | 2.11x |
| `common/uri-miss` | `IsMatch` | 0.140 us | 0.067 us | 0.086 us | 2.09x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.058 us | 0.029 us | 0.046 us | 1.97x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.050 us | 1.68x |
| `industry/mariomka-uri-count` | `Count` | 1,753.941 us | 1,107.493 us | 8,510.567 us | 1.58x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.051 us | 0.070 us | 1.48x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.050 us | 0.071 us | 1.48x |
| `industry/leipzig-symbol-count` | `Count` | 1,811.921 us | 1,234.188 us | 6,909.700 us | 1.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,142.542 us | 3,003.641 us | 8,204.067 us | 1.38x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.078 us | 1.35x |
| `common/uri-match` | `IsMatch` | 0.116 us | 0.089 us | 0.121 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.081 us | 0.108 us | 1.19x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.116 us | 1.12x |
| `industry/leipzig-twain-count` | `Count` | 1,271.308 us | 1,283.848 us | 5,665.333 us | 0.99x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.087 us | 0.128 us | 0.90x |
| `common/matches-words` | `Count` | 9.391 us | 11.018 us | 13.961 us | 0.85x |
| `industry/mariomka-ip-count` | `Count` | 2,703.553 us | 3,519.347 us | 11,130.250 us | 0.77x |
| `common/split-words` | `Split` | 14.546 us | 21.542 us | 23.190 us | 0.68x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.045 us | 0.074 us | 0.64x |
| `common/matches-word` | `Count` | 1.353 us | 2.364 us | 3.676 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.204 us | 0.239 us | 0.43x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.053 us | 0.30x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 506.253 us | 3,977.177 us | 4,392.808 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 179.629 us | 1,485.456 us | 1,829.724 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,982.738 us | 17,484.900 us | 18,436.600 us | 0.11x |
| `common/matches-boundary` | `Count` | 8.234 us | 106.751 us | 108.395 us | 0.08x |
| `common/matches-set` | `Count` | 7.899 us | 106.098 us | 108.650 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 278.416 us | 4,841.836 us | 5,265.290 us | 0.06x |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 15.644 us | 6.378 us | 29.276 us | 2.45x |
| `structural/keyword-to-generic-type` | 5.123 us | 4.129 us | 15.767 us | 1.24x |
| `lokad/langserv/color-short-hex` | 1.679 us | 1.380 us | 1.523 us | 1.22x |
| `lokad/lexer/doc-line` | 3.345 us | 3.064 us | 3.773 us | 1.09x |
| `lokad/lexer/identifier` | 24.060 us | 22.882 us | 25.985 us | 1.05x |
| `structural/ordered-keyword-pair` | 5.581 us | 5.516 us | 17.351 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.197 us | 7.335 us | 7.961 us | 0.98x |
| `lokad/langserv/identifier-validator` | 11.448 us | 12.688 us | 13.948 us | 0.90x |
| `fallback/lookahead` | 5.491 us | 6.114 us | 17.145 us | 0.90x |
| `lokad/lexer/operator-run` | 2.925 us | 3.413 us | 3.886 us | 0.86x |
| `literal-family/method-token-family` | 7.203 us | 9.286 us | 19.902 us | 0.78x |
| `lokad/style/hex-color` | 0.606 us | 0.808 us | 0.874 us | 0.75x |
| `lokad/langserv/color-rgb-hex` | 2.064 us | 2.999 us | 3.096 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.347 us | 4.875 us | 5.713 us | 0.69x |
| `literal/identifier-token` | 2.814 us | 4.431 us | 15.622 us | 0.64x |
| `lokad/lexer/number` | 8.888 us | 14.527 us | 15.456 us | 0.61x |
| `literal/call-token` | 2.619 us | 4.336 us | 15.615 us | 0.60x |
| `structural/method-family-generic-call` | 3.486 us | 6.651 us | 17.982 us | 0.52x |
| `lokad/style/cell-ref` | 1.500 us | 2.949 us | 3.086 us | 0.51x |
| `structural/method-family-call` | 3.881 us | 8.146 us | 18.933 us | 0.48x |
| `literal-family/type-token-family` | 10.601 us | 24.874 us | 35.766 us | 0.43x |
| `literal/identifier-token-casei` | 6.123 us | 15.454 us | 27.065 us | 0.40x |
| `lokad/style/range-ref` | 0.926 us | 2.756 us | 2.867 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.703 us | 17.403 us | 28.301 us | 0.33x |
| `structural/ordered-keyword-window` | 5.497 us | 17.785 us | 28.986 us | 0.31x |
| `structural/modifier-family-to-type-window` | 77.151 us | 258.381 us | 271.461 us | 0.30x |
| `lokad/lexer/string` | 23.938 us | 92.910 us | 98.347 us | 0.26x |
| `lokad/folding/region-marker` | 7.021 us | 36.737 us | 60.980 us | 0.19x |
| `structural/modifier-family-to-type-pair` | 23.642 us | 135.082 us | 147.908 us | 0.18x |
| `structural/ordered-identifier-window` | 5.010 us | 29.117 us | 40.062 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.606 us | 24.453 us | 24.985 us | 0.11x |
| `structural/keyword-family-to-capitalized-identifier` | 18.174 us | 220.839 us | 234.082 us | 0.08x |
| `lokad/langserv/url-download` | 2.577 us | 52.800 us | 53.332 us | 0.05x |
| `balancing/begin-end-blocks` | 5.760 us | 4,408.408 us | 4,412.983 us | 0.0013x |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 15.695 us | 4.469 us | 26.317 us | 3.51x |
| `structural/keyword-family-to-capitalized-identifier` | 18.297 us | 7.791 us | 18.755 us | 2.35x |
| `structural/modifier-family-to-type-pair` | 23.590 us | 10.768 us | 21.884 us | 2.19x |
| `lokad/langserv/color-short-hex` | 1.682 us | 1.039 us | 1.220 us | 1.62x |
| `lokad/langserv/helper-identifier` | 7.192 us | 4.770 us | 5.936 us | 1.51x |
| `structural/keyword-to-generic-type` | 5.109 us | 3.543 us | 14.655 us | 1.44x |
| `lokad/lexer/doc-line` | 3.343 us | 2.332 us | 3.158 us | 1.43x |
| `lokad/lexer/operator-run` | 2.926 us | 2.072 us | 2.566 us | 1.41x |
| `lokad/lexer/identifier` | 23.894 us | 17.036 us | 21.227 us | 1.40x |
| `structural/modifier-family-to-type-window` | 76.980 us | 57.007 us | 68.463 us | 1.35x |
| `structural/ordered-keyword-pair` | 5.599 us | 4.174 us | 15.251 us | 1.34x |
| `fallback/lookahead` | 5.507 us | 4.261 us | 14.900 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.384 us | 9.373 us | 11.343 us | 1.21x |
| `lokad/lexer/number` | 8.842 us | 7.586 us | 8.583 us | 1.17x |
| `lokad/style/hex-color` | 0.607 us | 0.537 us | 0.627 us | 1.13x |
| `lokad/docs/inline-doc-prefix` | 3.357 us | 2.996 us | 3.851 us | 1.12x |
| `lokad/langserv/color-rgb-hex` | 2.064 us | 1.967 us | 2.169 us | 1.05x |
| `literal/identifier-token-casei` | 6.133 us | 5.944 us | 17.248 us | 1.03x |
| `literal-family/type-token-family` | 10.582 us | 11.603 us | 22.238 us | 0.91x |
| `lokad/style/cell-ref` | 1.502 us | 1.913 us | 2.014 us | 0.79x |
| `structural/method-family-generic-call` | 3.479 us | 4.534 us | 15.132 us | 0.77x |
| `structural/method-family-call` | 3.887 us | 5.118 us | 15.499 us | 0.76x |
| `literal-family/method-token-family` | 3.911 us | 5.345 us | 15.783 us | 0.73x |
| `structural/ordered-modifier-pair` | 5.724 us | 8.683 us | 19.580 us | 0.66x |
| `literal/call-token` | 2.624 us | 4.019 us | 14.615 us | 0.65x |
| `structural/ordered-keyword-window` | 5.484 us | 8.666 us | 19.493 us | 0.63x |
| `literal/identifier-token` | 2.816 us | 4.739 us | 15.333 us | 0.59x |
| `lokad/style/range-ref` | 0.926 us | 1.621 us | 1.684 us | 0.57x |
| `lokad/lexer/string` | 23.982 us | 44.740 us | 48.064 us | 0.54x |
| `structural/ordered-identifier-window` | 5.010 us | 10.707 us | 21.458 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.610 us | 7.556 us | 8.224 us | 0.35x |
| `lokad/langserv/url-download` | 2.578 us | 13.136 us | 13.604 us | 0.20x |
| `lokad/folding/region-marker` | 7.021 us | 36.513 us | 59.456 us | 0.19x |
| `balancing/begin-end-blocks` | 5.777 us | 471.480 us | 498.213 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
