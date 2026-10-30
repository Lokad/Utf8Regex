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

The full baseline was refreshed on **28 October 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Four Core rows
remain unqualified; their displayed medians do not establish parity. Measurements
describe each row's recorded implementation; selected rows are refreshed
incrementally and do not qualify later source changes.

Historical ordinary Lokad script rows used a compiled .NET comparator. New
refreshes align compilation flags; the release refresh will replace those older
comparisons. Compiled rows already use matching flags.

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
| `literal/sherlock-zh` | 133.056 us | 18.989 us | 2,185.000 us | 7.01x |
| `bounded-repeat/letters-en` | 12,278.700 us | 2,835.494 us | 2,950.835 us | 4.33x |
| `literal-alternate/sherlock-zh` | 236.367 us | 69.670 us | 2,181.591 us | 3.39x |
| `bounded-repeat/capitals` | 130,590.400 us | 45,550.450 us | 54,041.750 us | 2.87x |
| `literal/sherlock-ru` | 247.409 us | 97.602 us | 5,306.210 us | 2.53x |
| `aws-keys/full` | 144,004.800 us | 87,536.000 us | 118,498.000 us | 1.65x |
| `ruff-noqa/tweaked` | 12,323.100 us | 8,272.286 us | 43,925.600 us | 1.49x |
| `aws-keys/quick` | 19,486.600 us | 16,437.575 us | 75,330.950 us | 1.19x |
| `literal/sherlock-casei-en` | 170.643 us | 190.831 us | 580.143 us | 0.89x |
| `literal/sherlock-en` | 70.161 us | 85.809 us | 465.229 us | 0.82x |
| `literal/sherlock-casei-ru` | 239.221 us | 368.570 us | 5,642.989 us | 0.65x |
| `literal-alternate/sherlock-ru` | 1,707.683 us | 4,740.482 us | 9,950.000 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,959.927 us | 6,013.289 us | 6,800.837 us | 0.33x |
| `dictionary/single` | 8,281.400 us | 25,638.250 us | 25,785.750 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 198.390 us | 622.280 us | 1,049.458 us | 0.32x |
| `literal-alternate/sherlock-en` | 401.429 us | 1,494.656 us | 2,045.030 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 167.501 us | 666.392 us | 1,114.198 us | 0.25x |
| `bounded-repeat/context` | 21,666.533 us | 148,540.900 us | 151,593.000 us | 0.15x |
| `ruff-noqa/real` | 4,539.175 us | 71,054.600 us | 85,527.300 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 1,090.147 us | 23,266.933 us | 28,614.050 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 220.998 us | 11,485.720 us | 12,302.900 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/leipzig-symbol-count` | `Count` | 1,152,702.300 us | 23,036.733 us | 30,488.150 us | 50.04x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,808.973 us | 979.187 us | 1,276.603 us | 4.91x |
| `industry/mariomka-email-count` | `Count` | 1,525.918 us | 492.350 us | 9,040.367 us | 3.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 46,658.450 us | 19,592.933 us | 20,452.233 us | 2.38x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.180 us | 0.110 us | 0.147 us | 1.64x |
| `common/match-word` | `Match` | 0.204 us | 0.166 us | 1.201 us | 1.23x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 253.993 us | 263.998 us | 501.541 us | 0.96x |
| `industry/leipzig-twain-count` | `Count` | 1,322.174 us | 1,395.949 us | 8,810.200 us | 0.95x |
| `common/replace-words` | `Replace` | 69.173 us | 76.562 us | 78.618 us | 0.90x |
| `industry/mariomka-uri-count` | `Count` | 1,749.428 us | 1,943.896 us | 10,451.900 us | 0.90x |
| `common/uri-miss` | `IsMatch` | 0.166 us | 0.204 us | 0.226 us | 0.81x |
| `common/uri-match` | `IsMatch` | 0.167 us | 0.234 us | 0.270 us | 0.71x |
| `common/ip-match` | `IsMatch` | 0.125 us | 0.181 us | 0.201 us | 0.69x |
| `common/email-match` | `IsMatch` | 0.179 us | 0.269 us | 0.293 us | 0.67x |
| `common/ip-miss` | `IsMatch` | 0.124 us | 0.189 us | 0.209 us | 0.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.085 us | 0.102 us | 0.58x |
| `common/email-miss` | `IsMatch` | 0.183 us | 0.346 us | 0.373 us | 0.53x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.083 us | 0.163 us | 0.179 us | 0.51x |
| `industry/leipzig-name-family-count` | `Count` | 4,216.867 us | 9,795.667 us | 17,176.000 us | 0.43x |
| `common/matches-word` | `Count` | 1.422 us | 4.262 us | 6.079 us | 0.33x |
| `industry/mariomka-ip-count` | `Count` | 2,654.368 us | 8,643.717 us | 17,226.200 us | 0.31x |
| `common/one-node-backtracking` | `IsMatch` | 0.189 us | 0.628 us | 0.659 us | 0.30x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.020 us | 0.084 us | 0.100 us | 0.23x |
| `common/backtracking` | `IsMatch` | 0.185 us | 0.849 us | 0.893 us | 0.22x |
| `industry/leipzig-river-window-count` | `Count` | 5,991.589 us | 32,223.300 us | 39,726.275 us | 0.19x |
| `common/split-words` | `Split` | 14.617 us | 78.730 us | 80.361 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.561 us | 0.592 us | 0.18x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.078 us | 0.091 us | 0.14x |
| `common/matches-words` | `Count` | 9.542 us | 69.688 us | 71.353 us | 0.14x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 522.382 us | 8,730.300 us | 9,193.533 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.002 us | 163.076 us | 166.566 us | 0.04x |
| `common/matches-set` | `Count` | 7.892 us | 199.831 us | 201.207 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 266.929 us | 11,941.220 us | 12,497.400 us | 0.02x |
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
| `bounded-repeat/capitals` | 131,413.700 us | 13,637.300 us | 21,514.867 us | 9.64x |
| `aws-keys/full` | 144,399.700 us | 19,072.800 us | 52,446.750 us | 7.57x |
| `literal-alternate/sherlock-zh` | 247.035 us | 36.260 us | 2,168.896 us | 6.81x |
| `bounded-repeat/letters-en` | 12,380.160 us | 1,836.996 us | 1,950.331 us | 6.74x |
| `literal/sherlock-zh` | 133.114 us | 24.089 us | 2,180.178 us | 5.53x |
| `aws-keys/quick` | 19,536.733 us | 4,010.462 us | 60,315.700 us | 4.87x |
| `literal/sherlock-ru` | 253.819 us | 89.106 us | 5,270.370 us | 2.85x |
| `ruff-noqa/tweaked` | 12,938.525 us | 4,613.482 us | 41,076.400 us | 2.80x |
| `literal/sherlock-casei-en` | 170.842 us | 80.031 us | 444.352 us | 2.13x |
| `literal-alternate/sherlock-casei-en` | 232.271 us | 146.494 us | 617.257 us | 1.59x |
| `literal-alternate/sherlock-en-mixed` | 199.769 us | 178.879 us | 573.268 us | 1.12x |
| `literal-alternate/sherlock-en` | 400.820 us | 369.001 us | 809.971 us | 1.09x |
| `literal/sherlock-casei-ru` | 242.555 us | 226.651 us | 5,354.450 us | 1.07x |
| `bounded-repeat/letters-ru` | 1,972.931 us | 2,054.656 us | 2,880.750 us | 0.96x |
| `literal/sherlock-en` | 70.005 us | 75.927 us | 450.423 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 167.491 us | 201.830 us | 583.055 us | 0.83x |
| `literal-alternate/sherlock-ru` | 1,699.050 us | 2,360.773 us | 7,447.167 us | 0.72x |
| `literal-alternate/sherlock-casei-ru` | 1,097.370 us | 1,978.031 us | 7,160.333 us | 0.55x |
| `bounded-repeat/context` | 21,747.767 us | 69,797.200 us | 72,683.400 us | 0.31x |
| `ruff-noqa/real` | 4,532.050 us | 21,373.100 us | 34,989.250 us | 0.21x |
| `dictionary/single` | 8,292.643 us | 46,077.650 us | 45,978.600 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/leipzig-symbol-count` | `Count` | 1,153,094.300 us | 1,235.305 us | 6,850.600 us | 933.45x |
| `industry/rust-sherlock-letter-count` | `Count` | 44,934.700 us | 8,940.667 us | 9,616.660 us | 5.03x |
| `common/backtracking` | `IsMatch` | 0.190 us | 0.044 us | 0.075 us | 4.34x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 252.827 us | 58.641 us | 263.159 us | 4.31x |
| `industry/leipzig-river-window-count` | `Count` | 6,004.767 us | 1,560.524 us | 8,842.462 us | 3.85x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.179 us | 0.049 us | 0.088 us | 3.67x |
| `common/replace-words` | `Replace` | 69.116 us | 19.141 us | 20.760 us | 3.61x |
| `industry/mariomka-email-count` | `Count` | 1,529.000 us | 455.287 us | 7,853.633 us | 3.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,784.783 us | 1,499.603 us | 1,806.293 us | 3.19x |
| `common/uri-miss` | `IsMatch` | 0.167 us | 0.063 us | 0.085 us | 2.62x |
| `common/ip-match` | `IsMatch` | 0.124 us | 0.049 us | 0.070 us | 2.55x |
| `common/match-word` | `Match` | 0.203 us | 0.080 us | 0.994 us | 2.53x |
| `common/ip-miss` | `IsMatch` | 0.121 us | 0.050 us | 0.072 us | 2.43x |
| `common/email-match` | `IsMatch` | 0.178 us | 0.082 us | 0.109 us | 2.17x |
| `common/one-node-backtracking` | `IsMatch` | 0.190 us | 0.088 us | 0.129 us | 2.15x |
| `common/email-miss` | `IsMatch` | 0.182 us | 0.089 us | 0.116 us | 2.05x |
| `common/uri-match` | `IsMatch` | 0.168 us | 0.089 us | 0.120 us | 1.90x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.030 us | 0.049 us | 1.83x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.052 us | 0.031 us | 0.048 us | 1.67x |
| `industry/mariomka-uri-count` | `Count` | 1,751.900 us | 1,099.909 us | 8,499.333 us | 1.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.085 us | 0.056 us | 0.077 us | 1.52x |
| `industry/leipzig-name-family-count` | `Count` | 4,283.100 us | 3,108.941 us | 8,415.833 us | 1.38x |
| `industry/leipzig-twain-count` | `Count` | 1,320.782 us | 1,333.897 us | 5,548.167 us | 0.99x |
| `common/matches-words` | `Count` | 9.540 us | 11.005 us | 14.130 us | 0.87x |
| `industry/mariomka-ip-count` | `Count` | 2,655.547 us | 3,546.340 us | 11,183.800 us | 0.75x |
| `common/split-words` | `Split` | 14.632 us | 21.080 us | 22.875 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.030 us | 0.044 us | 0.074 us | 0.68x |
| `common/matches-word` | `Count` | 1.422 us | 2.363 us | 3.649 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.100 us | 0.203 us | 0.238 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.035 us | 0.053 us | 0.36x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 524.364 us | 3,986.215 us | 4,417.133 us | 0.13x |
| `common/matches-set` | `Count` | 7.813 us | 105.702 us | 108.164 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.001 us | 103.918 us | 107.271 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 267.806 us | 4,816.909 us | 5,222.220 us | 0.06x |
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
| `lokad/folding/region-marker` | 5,376.030 us | 37.099 us | 60.876 us | 144.91x |
| `lokad/lexer/operator-run` | 13.210 us | 1.971 us | 2.578 us | 6.70x |
| `lokad/langserv/helper-identifier` | 31.950 us | 4.882 us | 6.212 us | 6.54x |
| `lokad/lexer/string` | 286.829 us | 44.581 us | 49.472 us | 6.43x |
| `lokad/lexer/identifier` | 106.308 us | 16.652 us | 21.048 us | 6.38x |
| `lokad/imports/module-imports` | 30.638 us | 6.343 us | 28.987 us | 4.83x |
| `lokad/langserv/color-short-hex` | 1.682 us | 1.104 us | 1.247 us | 1.52x |
| `structural/keyword-to-generic-type` | 5.830 us | 4.118 us | 15.750 us | 1.42x |
| `lokad/lexer/doc-line` | 4.420 us | 3.124 us | 3.864 us | 1.41x |
| `lokad/langserv/identifier-validator` | 11.724 us | 9.702 us | 11.734 us | 1.21x |
| `structural/ordered-keyword-pair` | 6.110 us | 5.521 us | 17.198 us | 1.11x |
| `fallback/lookahead` | 6.352 us | 6.116 us | 17.564 us | 1.04x |
| `lokad/langserv/color-rgb-hex` | 2.070 us | 2.064 us | 2.219 us | 1.00x |
| `lokad/style/hex-color` | 0.792 us | 0.821 us | 0.895 us | 0.96x |
| `lokad/docs/inline-doc-prefix` | 4.300 us | 4.833 us | 5.686 us | 0.89x |
| `literal-family/method-token-family` | 8.184 us | 9.224 us | 19.857 us | 0.89x |
| `structural/modifier-family-to-type-window` | 210.234 us | 259.023 us | 272.374 us | 0.81x |
| `lokad/lexer/number` | 10.325 us | 14.391 us | 15.615 us | 0.72x |
| `literal/call-token` | 3.050 us | 4.431 us | 15.650 us | 0.69x |
| `structural/method-family-generic-call` | 4.440 us | 6.529 us | 17.976 us | 0.68x |
| `lokad/style/cell-ref` | 2.004 us | 2.949 us | 3.091 us | 0.68x |
| `literal/identifier-token` | 2.795 us | 4.393 us | 15.651 us | 0.64x |
| `structural/method-family-call` | 4.852 us | 8.149 us | 19.027 us | 0.60x |
| `literal-family/type-token-family` | 12.127 us | 25.124 us | 36.032 us | 0.48x |
| `literal/identifier-token-casei` | 7.452 us | 15.546 us | 27.323 us | 0.48x |
| `lokad/style/range-ref` | 1.192 us | 2.773 us | 2.805 us | 0.43x |
| `structural/ordered-keyword-window` | 6.874 us | 18.003 us | 29.815 us | 0.38x |
| `structural/ordered-modifier-pair` | 6.073 us | 17.663 us | 28.785 us | 0.34x |
| `structural/keyword-family-to-capitalized-identifier` | 74.910 us | 220.031 us | 231.281 us | 0.34x |
| `structural/ordered-identifier-window` | 6.493 us | 29.202 us | 40.545 us | 0.22x |
| `structural/modifier-family-to-type-pair` | 24.891 us | 135.047 us | 147.705 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.975 us | 24.828 us | 25.057 us | 0.12x |
| `lokad/langserv/url-download` | 2.800 us | 51.138 us | 51.700 us | 0.05x |
| `balancing/begin-end-blocks` | 7.333 us | 4,248.158 us | 4,248.675 us | 0.0017x |
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
| `lokad/folding/region-marker` | 5,395.440 us | 37.293 us | 60.206 us | 144.68x |
| `structural/keyword-family-to-capitalized-identifier` | 74.881 us | 7.748 us | 19.121 us | 9.67x |
| `lokad/imports/module-imports` | 30.623 us | 4.405 us | 26.662 us | 6.95x |
| `lokad/lexer/operator-run` | 13.443 us | 1.958 us | 2.585 us | 6.86x |
| `lokad/lexer/string` | 294.849 us | 44.984 us | 48.666 us | 6.55x |
| `lokad/langserv/helper-identifier` | 31.510 us | 4.847 us | 6.279 us | 6.50x |
| `lokad/lexer/identifier` | 105.150 us | 16.647 us | 20.500 us | 6.32x |
| `structural/modifier-family-to-type-window` | 210.581 us | 56.469 us | 69.163 us | 3.73x |
| `structural/modifier-family-to-type-pair` | 24.921 us | 10.652 us | 21.717 us | 2.34x |
| `lokad/lexer/doc-line` | 4.505 us | 2.366 us | 3.125 us | 1.90x |
| `structural/keyword-to-generic-type` | 5.833 us | 3.536 us | 14.677 us | 1.65x |
| `lokad/langserv/color-short-hex` | 1.664 us | 1.040 us | 1.260 us | 1.60x |
| `fallback/lookahead` | 6.326 us | 4.242 us | 15.214 us | 1.49x |
| `lokad/style/hex-color` | 0.820 us | 0.551 us | 0.627 us | 1.49x |
| `lokad/docs/inline-doc-prefix` | 4.300 us | 2.925 us | 3.755 us | 1.47x |
| `structural/ordered-keyword-pair` | 6.104 us | 4.183 us | 15.261 us | 1.46x |
| `lokad/lexer/number` | 10.320 us | 7.660 us | 9.106 us | 1.35x |
| `lokad/langserv/identifier-validator` | 11.723 us | 9.216 us | 11.625 us | 1.27x |
| `literal/identifier-token-casei` | 7.474 us | 5.975 us | 17.260 us | 1.25x |
| `lokad/style/cell-ref` | 1.977 us | 1.845 us | 1.983 us | 1.07x |
| `lokad/langserv/color-rgb-hex` | 2.083 us | 1.957 us | 2.165 us | 1.06x |
| `literal-family/type-token-family` | 12.130 us | 11.700 us | 22.231 us | 1.04x |
| `structural/method-family-generic-call` | 4.433 us | 4.631 us | 15.032 us | 0.96x |
| `structural/method-family-call` | 4.847 us | 5.107 us | 15.916 us | 0.95x |
| `literal-family/method-token-family` | 4.782 us | 5.380 us | 15.742 us | 0.89x |
| `structural/ordered-keyword-window` | 6.885 us | 8.642 us | 20.102 us | 0.80x |
| `lokad/style/range-ref` | 1.198 us | 1.544 us | 1.616 us | 0.78x |
| `literal/call-token` | 3.055 us | 4.019 us | 14.705 us | 0.76x |
| `structural/ordered-modifier-pair` | 6.042 us | 8.654 us | 19.744 us | 0.70x |
| `structural/ordered-identifier-window` | 6.484 us | 10.777 us | 21.938 us | 0.60x |
| `literal/identifier-token` | 2.791 us | 4.756 us | 15.064 us | 0.59x |
| `lokad/langserv/url-dashboard` | 2.976 us | 8.010 us | 8.506 us | 0.37x |
| `lokad/langserv/url-download` | 2.812 us | 13.067 us | 13.564 us | 0.22x |
| `balancing/begin-end-blocks` | 7.342 us | 467.528 us | 489.277 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
