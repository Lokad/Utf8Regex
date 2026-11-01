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

The full baseline was refreshed on **1 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Unqualified rows
retain their observations without establishing parity. The complete source
build, both-runtime tests and all-profile benchmark refresh passed in **42m10s**;
package and consumer qualification are separate. Measurements describe each
row's recorded implementation; later source changes need their own evidence.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

The complete Core/PythonRe/PCRE2 refresh took **45 minutes 37 seconds**, including
a fresh Release build, .NET 10/11 tests, paired comparators, lifecycle/scaling
measurements and resource checks. See the
[run receipt](bench/Experiments/Net11-20261027/subhour-refresh.json).
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
| `literal/sherlock-zh` | 131.673 us | 18.398 us | 2,174.317 us | 7.16x |
| `literal-alternate/sherlock-zh` | 242.220 us | 65.389 us | 2,114.922 us | 3.70x |
| `bounded-repeat/capitals` | 122,465.300 us | 44,657.200 us | 50,764.900 us | 2.74x |
| `literal/sherlock-ru` | 236.491 us | 93.741 us | 5,012.770 us | 2.52x |
| `ruff-noqa/tweaked` | 10,368.620 us | 7,615.663 us | 40,760.450 us | 1.36x |
| `literal/sherlock-casei-en` | 167.761 us | 195.806 us | 584.144 us | 0.86x |
| `literal/sherlock-en` | 68.585 us | 83.040 us | 468.135 us | 0.83x |
| `literal/sherlock-casei-ru` | 250.425 us | 381.689 us | 5,707.689 us | 0.66x |
| `aws-keys/quick` | 5,750.870 us | 9,569.080 us | 43,835.350 us | 0.60x |
| `bounded-repeat/letters-en` | 1,033.617 us | 2,773.184 us | 2,841.522 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,661.465 us | 4,634.209 us | 9,923.967 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 191.655 us | 587.950 us | 1,050.762 us | 0.33x |
| `dictionary/single` | 8,073.986 us | 25,392.100 us | 25,608.750 us | 0.32x |
| `bounded-repeat/letters-ru` | 1,879.067 us | 5,992.067 us | 6,730.913 us | 0.31x |
| `literal-alternate/sherlock-en` | 384.740 us | 1,449.331 us | 1,996.916 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 162.533 us | 629.923 us | 1,040.565 us | 0.26x |
| `bounded-repeat/context` | 20,031.567 us | 144,933.900 us | 146,578.300 us | 0.14x |
| `aws-keys/full` | 10,365.950 us | 83,890.700 us | 112,368.300 us | 0.12x |
| `ruff-noqa/real` | 4,464.608 us | 68,218.600 us | 99,923.700 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,050.513 us | 22,259.433 us | 27,652.050 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 221.157 us | 10,890.660 us | 11,768.980 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,498.576 us | 482.630 us | 8,866.500 us | 3.11x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.178 us | 0.107 us | 0.145 us | 1.67x |
| `common/match-word` | `Match` | 0.200 us | 0.163 us | 1.156 us | 1.23x |
| `industry/leipzig-twain-count` | `Count` | 1,240.966 us | 1,301.082 us | 8,364.783 us | 0.95x |
| `industry/mariomka-uri-count` | `Count` | 1,729.607 us | 1,880.011 us | 10,334.600 us | 0.92x |
| `common/uri-miss` | `IsMatch` | 0.168 us | 0.184 us | 0.204 us | 0.91x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 240.639 us | 265.278 us | 516.680 us | 0.91x |
| `common/replace-words` | `Replace` | 69.098 us | 76.937 us | 78.527 us | 0.90x |
| `common/uri-match` | `IsMatch` | 0.173 us | 0.222 us | 0.253 us | 0.78x |
| `common/ip-miss` | `IsMatch` | 0.125 us | 0.182 us | 0.199 us | 0.69x |
| `common/ip-match` | `IsMatch` | 0.114 us | 0.170 us | 0.191 us | 0.67x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.082 us | 0.098 us | 0.64x |
| `common/email-match` | `IsMatch` | 0.166 us | 0.265 us | 0.289 us | 0.63x |
| `common/email-miss` | `IsMatch` | 0.174 us | 0.337 us | 0.360 us | 0.52x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.164 us | 0.181 us | 0.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,131.658 us | 9,515.067 us | 16,646.000 us | 0.43x |
| `common/one-node-backtracking` | `IsMatch` | 0.201 us | 0.600 us | 0.633 us | 0.34x |
| `common/matches-word` | `Count` | 1.379 us | 4.142 us | 5.808 us | 0.33x |
| `industry/mariomka-ip-count` | `Count` | 2,700.195 us | 8,297.043 us | 16,810.700 us | 0.33x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.107 us | 0.138 us | 0.26x |
| `common/backtracking` | `IsMatch` | 0.188 us | 0.827 us | 0.863 us | 0.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.080 us | 0.095 us | 0.21x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 173.433 us | 946.581 us | 1,243.912 us | 0.18x |
| `common/split-words` | `Split` | 14.369 us | 78.850 us | 80.819 us | 0.18x |
| `industry/leipzig-river-window-count` | `Count` | 5,693.556 us | 31,470.750 us | 45,506.400 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.558 us | 0.590 us | 0.17x |
| `common/matches-words` | `Count` | 9.281 us | 69.645 us | 71.059 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.076 us | 0.088 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,946.119 us | 19,474.550 us | 20,416.967 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,844.682 us | 22,462.833 us | 29,783.900 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 500.154 us | 8,403.186 us | 8,958.633 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.771 us | 158.523 us | 161.619 us | 0.05x |
| `common/matches-set` | `Count` | 7.815 us | 188.593 us | 191.530 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.744 us | 11,811.980 us | 12,456.920 us | 0.02x |
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
| `bounded-repeat/capitals` | 122,679.500 us | 13,147.125 us | 19,444.400 us | 9.33x |
| `literal-alternate/sherlock-zh` | 231.474 us | 35.089 us | 2,071.508 us | 6.60x |
| `literal/sherlock-zh` | 131.634 us | 23.394 us | 2,169.229 us | 5.63x |
| `literal/sherlock-ru` | 238.161 us | 84.988 us | 5,077.220 us | 2.80x |
| `ruff-noqa/tweaked` | 10,691.400 us | 4,534.483 us | 34,921.300 us | 2.36x |
| `literal/sherlock-casei-en` | 168.922 us | 79.206 us | 431.941 us | 2.13x |
| `aws-keys/quick` | 6,926.022 us | 3,523.133 us | 35,319.800 us | 1.97x |
| `literal-alternate/sherlock-casei-en` | 211.684 us | 142.554 us | 563.521 us | 1.48x |
| `literal/sherlock-casei-ru` | 245.740 us | 220.626 us | 5,441.480 us | 1.11x |
| `literal-alternate/sherlock-en-mixed` | 193.697 us | 176.511 us | 549.990 us | 1.10x |
| `literal-alternate/sherlock-en` | 387.678 us | 361.357 us | 766.870 us | 1.07x |
| `bounded-repeat/letters-ru` | 1,903.741 us | 2,036.300 us | 2,850.389 us | 0.93x |
| `literal/sherlock-en` | 68.318 us | 73.916 us | 415.929 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 161.608 us | 198.304 us | 588.036 us | 0.81x |
| `literal-alternate/sherlock-ru` | 1,642.025 us | 2,303.865 us | 7,363.300 us | 0.71x |
| `aws-keys/full` | 10,973.550 us | 18,528.267 us | 48,436.700 us | 0.59x |
| `bounded-repeat/letters-en` | 1,033.788 us | 1,789.432 us | 1,864.396 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 1,049.043 us | 1,926.073 us | 7,011.067 us | 0.54x |
| `bounded-repeat/context` | 20,484.733 us | 68,221.100 us | 70,809.700 us | 0.30x |
| `ruff-noqa/real` | 4,494.058 us | 20,798.267 us | 52,949.050 us | 0.22x |
| `dictionary/single` | 8,063.029 us | 44,677.650 us | 44,460.800 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/backtracking` | `IsMatch` | 0.186 us | 0.044 us | 0.073 us | 4.26x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 241.087 us | 58.134 us | 253.062 us | 4.15x |
| `industry/leipzig-river-window-count` | `Count` | 5,672.678 us | 1,493.549 us | 13,610.967 us | 3.80x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.178 us | 0.047 us | 0.086 us | 3.76x |
| `common/replace-words` | `Replace` | 66.479 us | 18.712 us | 19.963 us | 3.55x |
| `industry/mariomka-email-count` | `Count` | 1,513.288 us | 453.354 us | 7,652.967 us | 3.34x |
| `common/ip-miss` | `IsMatch` | 0.125 us | 0.048 us | 0.067 us | 2.64x |
| `common/match-word` | `Match` | 0.199 us | 0.081 us | 1.007 us | 2.46x |
| `common/ip-match` | `IsMatch` | 0.113 us | 0.047 us | 0.067 us | 2.40x |
| `common/one-node-backtracking` | `IsMatch` | 0.201 us | 0.088 us | 0.126 us | 2.29x |
| `common/uri-miss` | `IsMatch` | 0.166 us | 0.076 us | 0.085 us | 2.18x |
| `common/email-match` | `IsMatch` | 0.167 us | 0.079 us | 0.105 us | 2.11x |
| `common/uri-match` | `IsMatch` | 0.173 us | 0.086 us | 0.117 us | 2.01x |
| `common/email-miss` | `IsMatch` | 0.172 us | 0.086 us | 0.111 us | 2.00x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.028 us | 0.048 us | 1.78x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.030 us | 0.048 us | 1.76x |
| `industry/mariomka-uri-count` | `Count` | 1,733.055 us | 1,075.253 us | 8,432.067 us | 1.61x |
| `industry/leipzig-symbol-count` | `Count` | 1,861.796 us | 1,271.475 us | 6,883.567 us | 1.46x |
| `industry/leipzig-name-family-count` | `Count` | 4,158.031 us | 3,025.318 us | 8,284.233 us | 1.37x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.074 us | 0.054 us | 0.074 us | 1.36x |
| `industry/leipzig-twain-count` | `Count` | 1,249.905 us | 1,266.027 us | 6,819.714 us | 0.99x |
| `common/matches-words` | `Count` | 9.256 us | 10.852 us | 13.661 us | 0.85x |
| `industry/mariomka-ip-count` | `Count` | 2,690.270 us | 3,480.067 us | 11,150.100 us | 0.77x |
| `common/split-words` | `Split` | 14.377 us | 20.578 us | 22.352 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.072 us | 0.67x |
| `common/matches-word` | `Count` | 1.365 us | 2.301 us | 3.444 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.200 us | 0.232 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.036 us | 0.051 us | 0.34x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 498.724 us | 3,938.585 us | 4,356.808 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 173.258 us | 1,458.974 us | 1,817.993 us | 0.12x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,937.204 us | 18,381.650 us | 19,202.967 us | 0.11x |
| `common/matches-boundary` | `Count` | 7.801 us | 102.439 us | 104.285 us | 0.08x |
| `common/matches-set` | `Count` | 7.714 us | 102.745 us | 105.579 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 238.821 us | 4,743.582 us | 5,211.900 us | 0.05x |
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
| `lokad/imports/module-imports` | 30.771 us | 6.290 us | 28.562 us | 4.89x |
| `lokad/lexer/doc-line` | 4.286 us | 2.954 us | 3.635 us | 1.45x |
| `structural/keyword-to-generic-type` | 5.650 us | 4.018 us | 15.492 us | 1.41x |
| `lokad/lexer/identifier` | 30.118 us | 22.516 us | 25.033 us | 1.34x |
| `lokad/langserv/helper-identifier` | 9.298 us | 7.088 us | 7.909 us | 1.31x |
| `lokad/langserv/color-short-hex` | 1.608 us | 1.333 us | 1.445 us | 1.21x |
| `structural/ordered-keyword-pair` | 6.056 us | 5.125 us | 16.529 us | 1.18x |
| `lokad/lexer/operator-run` | 3.721 us | 3.340 us | 3.803 us | 1.11x |
| `fallback/lookahead` | 5.827 us | 5.824 us | 16.788 us | 1.00x |
| `lokad/style/hex-color` | 0.739 us | 0.787 us | 0.844 us | 0.94x |
| `lokad/langserv/identifier-validator` | 11.360 us | 12.197 us | 13.703 us | 0.93x |
| `lokad/docs/inline-doc-prefix` | 4.254 us | 4.627 us | 5.450 us | 0.92x |
| `literal-family/method-token-family` | 7.913 us | 8.977 us | 19.982 us | 0.88x |
| `lokad/lexer/number` | 10.536 us | 14.159 us | 15.151 us | 0.74x |
| `literal/call-token` | 3.044 us | 4.198 us | 15.781 us | 0.73x |
| `lokad/langserv/color-rgb-hex` | 2.045 us | 2.894 us | 2.967 us | 0.71x |
| `structural/method-family-generic-call` | 4.338 us | 6.441 us | 17.647 us | 0.67x |
| `lokad/style/cell-ref` | 1.868 us | 2.885 us | 2.898 us | 0.65x |
| `literal/identifier-token` | 2.705 us | 4.233 us | 15.260 us | 0.64x |
| `structural/method-family-call` | 4.755 us | 7.766 us | 18.721 us | 0.61x |
| `literal-family/type-token-family` | 11.822 us | 23.659 us | 34.306 us | 0.50x |
| `literal/identifier-token-casei` | 7.179 us | 14.857 us | 26.055 us | 0.48x |
| `lokad/style/range-ref` | 1.160 us | 2.616 us | 2.674 us | 0.44x |
| `structural/ordered-keyword-window` | 6.669 us | 17.312 us | 28.052 us | 0.39x |
| `structural/ordered-modifier-pair` | 5.999 us | 16.612 us | 27.325 us | 0.36x |
| `structural/keyword-family-to-capitalized-identifier` | 72.021 us | 211.054 us | 223.674 us | 0.34x |
| `lokad/lexer/string` | 29.056 us | 94.054 us | 98.743 us | 0.31x |
| `structural/modifier-family-to-type-window` | 75.573 us | 249.981 us | 264.076 us | 0.30x |
| `structural/ordered-identifier-window` | 6.346 us | 28.036 us | 39.182 us | 0.23x |
| `structural/modifier-family-to-type-pair` | 24.540 us | 125.372 us | 137.909 us | 0.20x |
| `lokad/folding/region-marker` | 6.946 us | 36.916 us | 59.999 us | 0.19x |
| `lokad/langserv/url-dashboard` | 2.838 us | 24.071 us | 24.453 us | 0.12x |
| `lokad/langserv/url-download` | 2.784 us | 48.931 us | 49.089 us | 0.06x |
| `balancing/begin-end-blocks` | 7.234 us | 4,270.817 us | 4,309.275 us | 0.0017x |
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
| `structural/keyword-family-to-capitalized-identifier` | 71.951 us | 7.502 us | 18.192 us | 9.59x |
| `lokad/imports/module-imports` | 30.927 us | 4.326 us | 25.973 us | 7.15x |
| `structural/modifier-family-to-type-pair` | 24.500 us | 10.541 us | 21.607 us | 2.32x |
| `lokad/langserv/helper-identifier` | 9.415 us | 4.746 us | 5.689 us | 1.98x |
| `lokad/lexer/doc-line` | 4.270 us | 2.258 us | 2.926 us | 1.89x |
| `lokad/lexer/operator-run` | 3.725 us | 1.973 us | 2.639 us | 1.89x |
| `lokad/lexer/identifier` | 30.022 us | 16.868 us | 19.725 us | 1.78x |
| `structural/keyword-to-generic-type` | 5.630 us | 3.381 us | 14.426 us | 1.66x |
| `lokad/langserv/color-short-hex` | 1.608 us | 0.995 us | 1.187 us | 1.62x |
| `lokad/docs/inline-doc-prefix` | 4.279 us | 2.769 us | 3.483 us | 1.55x |
| `structural/ordered-keyword-pair` | 6.130 us | 4.012 us | 14.658 us | 1.53x |
| `fallback/lookahead` | 5.922 us | 4.091 us | 14.509 us | 1.45x |
| `lokad/lexer/number` | 10.621 us | 7.545 us | 8.713 us | 1.41x |
| `lokad/style/hex-color` | 0.741 us | 0.531 us | 0.578 us | 1.40x |
| `structural/modifier-family-to-type-window` | 75.838 us | 55.454 us | 67.059 us | 1.37x |
| `literal/identifier-token-casei` | 7.211 us | 5.792 us | 16.928 us | 1.25x |
| `lokad/langserv/identifier-validator` | 11.326 us | 9.103 us | 10.928 us | 1.24x |
| `lokad/langserv/color-rgb-hex` | 2.023 us | 1.841 us | 2.048 us | 1.10x |
| `lokad/style/cell-ref` | 1.869 us | 1.722 us | 1.844 us | 1.09x |
| `literal-family/type-token-family` | 11.875 us | 11.264 us | 21.815 us | 1.05x |
| `structural/method-family-call` | 4.781 us | 4.989 us | 15.486 us | 0.96x |
| `structural/method-family-generic-call` | 4.309 us | 4.506 us | 14.872 us | 0.96x |
| `literal-family/method-token-family` | 4.692 us | 5.191 us | 16.227 us | 0.90x |
| `lokad/style/range-ref` | 1.157 us | 1.442 us | 1.501 us | 0.80x |
| `structural/ordered-keyword-window` | 6.683 us | 8.382 us | 19.098 us | 0.80x |
| `literal/call-token` | 3.020 us | 3.859 us | 14.702 us | 0.78x |
| `structural/ordered-modifier-pair` | 5.976 us | 8.405 us | 19.207 us | 0.71x |
| `literal/identifier-token` | 2.729 us | 4.191 us | 15.046 us | 0.65x |
| `lokad/lexer/string` | 29.048 us | 45.213 us | 48.138 us | 0.64x |
| `structural/ordered-identifier-window` | 6.332 us | 10.628 us | 20.968 us | 0.60x |
| `lokad/langserv/url-dashboard` | 2.844 us | 7.440 us | 7.797 us | 0.38x |
| `lokad/langserv/url-download` | 2.791 us | 12.568 us | 13.270 us | 0.22x |
| `lokad/folding/region-marker` | 6.942 us | 36.709 us | 59.048 us | 0.19x |
| `balancing/begin-end-blocks` | 7.285 us | 458.002 us | 483.677 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
