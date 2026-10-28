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
uses the SDK pinned in `global.json`, a release candidate. Published 0.4.0
packages target .NET 10. Benchmark refreshes now target .NET 11; existing rows
keep their original runtime identities until remeasured.

## Benchmarks

The tables below come from [README.Benchmarks.json](README.Benchmarks.json).
The 0.5 refresh uses independent lane calibration and alternating sample order;
counts and raw durations are retained. Core samples below 20 ms cannot set parity
status. Python and .NET result batches consume checksums after timing, while
including required result materialization. CPython paired measurements and the
native PCRE2 comparator retain their own semantic and qualification gates.
Rows retain their measured source/runtime versions and do not qualify later
implementations. Profile comparisons are in the
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
| `aws-keys/quick` | 3,892,909.500 us | 9,946.050 us | 43,766.750 us | 391.40x |
| `ruff-noqa/tweaked` | 2,049,613.300 us | 7,960.200 us | 43,682.550 us | 257.48x |
| `aws-keys/full` | 4,913,885.700 us | 85,935.500 us | 121,136.400 us | 57.18x |
| `bounded-repeat/capitals` | 765,702.300 us | 47,100.950 us | 53,194.700 us | 16.26x |
| `bounded-repeat/context` | 1,110,050.900 us | 148,741.900 us | 152,118.300 us | 7.46x |
| `literal/sherlock-zh` | 133.056 us | 18.989 us | 2,185.000 us | 7.01x |
| `ruff-noqa/real` | 429,091.700 us | 71,169.800 us | 89,015.500 us | 6.03x |
| `bounded-repeat/letters-en` | 12,278.700 us | 2,835.494 us | 2,950.835 us | 4.33x |
| `literal-alternate/sherlock-zh` | 236.367 us | 69.670 us | 2,181.591 us | 3.39x |
| `literal/sherlock-ru` | 247.409 us | 97.602 us | 5,306.210 us | 2.53x |
| `literal/sherlock-casei-en` | 170.643 us | 190.831 us | 580.143 us | 0.89x |
| `literal/sherlock-en` | 70.161 us | 85.809 us | 465.229 us | 0.82x |
| `literal/sherlock-casei-ru` | 239.221 us | 368.570 us | 5,642.989 us | 0.65x |
| `literal-alternate/sherlock-ru` | 1,707.683 us | 4,740.482 us | 9,950.000 us | 0.36x |
| `bounded-repeat/letters-ru` | 1,959.927 us | 6,013.289 us | 6,800.837 us | 0.33x |
| `dictionary/single` | 8,281.400 us | 25,638.250 us | 25,785.750 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 198.390 us | 622.280 us | 1,049.458 us | 0.32x |
| `literal-alternate/sherlock-en` | 401.429 us | 1,494.656 us | 2,045.030 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 167.501 us | 666.392 us | 1,114.198 us | 0.25x |
| `literal-alternate/sherlock-casei-ru` | 1,090.147 us | 23,266.933 us | 28,614.050 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 220.998 us | 11,485.720 us | 12,302.900 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-ip-count` | `Count` | 1,106,611.500 us | 8,621.767 us | 17,384.800 us | 128.35x |
| `common/one-node-backtracking` | `IsMatch` | 34.895 us | 0.611 us | 0.643 us | 57.11x |
| `industry/leipzig-symbol-count` | `Count` | 1,152,702.300 us | 23,036.733 us | 30,488.150 us | 50.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 231,343.700 us | 8,453.017 us | 8,894.100 us | 27.37x |
| `common/uri-miss` | `IsMatch` | 4.732 us | 0.190 us | 0.207 us | 24.88x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 1.450 us | 0.111 us | 0.144 us | 13.06x |
| `common/uri-match` | `IsMatch` | 2.618 us | 0.232 us | 0.268 us | 11.30x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 1.420 us | 0.167 us | 0.188 us | 8.52x |
| `common/ip-match` | `IsMatch` | 1.100 us | 0.177 us | 0.195 us | 6.23x |
| `common/ip-miss` | `IsMatch` | 1.122 us | 0.190 us | 0.209 us | 5.90x |
| `common/email-match` | `IsMatch` | 1.571 us | 0.276 us | 0.305 us | 5.68x |
| `common/email-miss` | `IsMatch` | 1.920 us | 0.348 us | 0.369 us | 5.52x |
| `common/backtracking` | `IsMatch` | 4.407 us | 0.835 us | 0.879 us | 5.28x |
| `common/matches-boundary` | `Count` | 833.867 us | 164.674 us | 166.856 us | 5.06x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,808.973 us | 979.187 us | 1,276.603 us | 4.91x |
| `industry/mariomka-email-count` | `Count` | 1,525.918 us | 492.350 us | 9,040.367 us | 3.10x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 789.700 us | 267.504 us | 498.465 us | 2.95x |
| `industry/rust-sherlock-letter-count` | `Count` | 46,658.450 us | 19,592.933 us | 20,452.233 us | 2.38x |
| `common/match-word` | `Match` | 0.204 us | 0.166 us | 1.201 us | 1.23x |
| `industry/leipzig-twain-count` | `Count` | 1,322.174 us | 1,395.949 us | 8,810.200 us | 0.95x |
| `common/replace-words` | `Replace` | 69.173 us | 76.562 us | 78.618 us | 0.90x |
| `industry/mariomka-uri-count` | `Count` | 1,749.428 us | 1,943.896 us | 10,451.900 us | 0.90x |
| `industry/leipzig-river-window-count` | `Count` | 19,542.333 us | 32,374.150 us | 46,719.000 us | 0.60x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.085 us | 0.102 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 4,216.867 us | 9,795.667 us | 17,176.000 us | 0.43x |
| `common/matches-set` | `Count` | 78.910 us | 194.124 us | 197.086 us | 0.41x |
| `common/matches-word` | `Count` | 1.422 us | 4.262 us | 6.079 us | 0.33x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.020 us | 0.084 us | 0.100 us | 0.23x |
| `common/split-words` | `Split` | 14.617 us | 78.730 us | 80.361 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.561 us | 0.592 us | 0.18x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.078 us | 0.091 us | 0.14x |
| `common/matches-words` | `Count` | 9.542 us | 69.688 us | 71.353 us | 0.14x |
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
| `aws-keys/quick` | 3,895,893.300 us | 3,640.179 us | 35,024.000 us | 1070.25x |
| `ruff-noqa/tweaked` | 2,053,154.800 us | 4,588.427 us | 40,022.600 us | 447.46x |
| `aws-keys/full` | 4,906,551.500 us | 18,958.467 us | 52,097.150 us | 258.81x |
| `bounded-repeat/capitals` | 763,827.400 us | 13,650.775 us | 20,355.567 us | 55.95x |
| `ruff-noqa/real` | 426,829.900 us | 21,288.733 us | 34,792.900 us | 20.05x |
| `bounded-repeat/context` | 1,115,509.100 us | 70,337.900 us | 73,045.100 us | 15.86x |
| `literal-alternate/sherlock-zh` | 247.035 us | 36.260 us | 2,168.896 us | 6.81x |
| `bounded-repeat/letters-en` | 12,380.160 us | 1,836.996 us | 1,950.331 us | 6.74x |
| `literal/sherlock-zh` | 133.114 us | 24.089 us | 2,180.178 us | 5.53x |
| `literal/sherlock-ru` | 253.819 us | 89.106 us | 5,270.370 us | 2.85x |
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
| `dictionary/single` | 8,292.643 us | 46,077.650 us | 45,978.600 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/leipzig-symbol-count` | `Count` | 1,153,094.300 us | 1,235.305 us | 6,850.600 us | 933.45x |
| `common/one-node-backtracking` | `IsMatch` | 34.735 us | 0.089 us | 0.128 us | 390.51x |
| `industry/mariomka-ip-count` | `Count` | 1,106,995.400 us | 3,525.667 us | 11,334.350 us | 313.98x |
| `common/backtracking` | `IsMatch` | 4.408 us | 0.044 us | 0.075 us | 99.71x |
| `common/uri-miss` | `IsMatch` | 4.703 us | 0.065 us | 0.084 us | 72.81x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 231,533.400 us | 3,980.992 us | 4,399.225 us | 58.16x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 1.447 us | 0.048 us | 0.085 us | 29.97x |
| `common/uri-match` | `IsMatch` | 2.613 us | 0.088 us | 0.120 us | 29.56x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 1.442 us | 0.055 us | 0.078 us | 26.35x |
| `common/ip-miss` | `IsMatch` | 1.142 us | 0.048 us | 0.070 us | 23.56x |
| `common/ip-match` | `IsMatch` | 1.093 us | 0.047 us | 0.068 us | 23.48x |
| `common/email-miss` | `IsMatch` | 1.937 us | 0.089 us | 0.115 us | 21.80x |
| `common/email-match` | `IsMatch` | 1.586 us | 0.107 us | 0.134 us | 14.86x |
| `industry/leipzig-river-window-count` | `Count` | 21,083.300 us | 1,584.697 us | 16,530.400 us | 13.30x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 789.033 us | 63.330 us | 270.703 us | 12.46x |
| `common/matches-boundary` | `Count` | 834.275 us | 106.333 us | 107.975 us | 7.85x |
| `industry/rust-sherlock-letter-count` | `Count` | 44,934.700 us | 8,940.667 us | 9,616.660 us | 5.03x |
| `common/replace-words` | `Replace` | 69.116 us | 19.141 us | 20.760 us | 3.61x |
| `industry/mariomka-email-count` | `Count` | 1,529.000 us | 455.287 us | 7,853.633 us | 3.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,784.783 us | 1,499.603 us | 1,806.293 us | 3.19x |
| `common/match-word` | `Match` | 0.203 us | 0.080 us | 0.994 us | 2.53x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.030 us | 0.049 us | 1.83x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.052 us | 0.031 us | 0.048 us | 1.67x |
| `industry/mariomka-uri-count` | `Count` | 1,751.900 us | 1,099.909 us | 8,499.333 us | 1.59x |
| `industry/leipzig-name-family-count` | `Count` | 4,283.100 us | 3,108.941 us | 8,415.833 us | 1.38x |
| `industry/leipzig-twain-count` | `Count` | 1,320.782 us | 1,333.897 us | 5,548.167 us | 0.99x |
| `common/matches-words` | `Count` | 9.540 us | 11.005 us | 14.130 us | 0.87x |
| `common/matches-set` | `Count` | 78.954 us | 105.581 us | 107.626 us | 0.75x |
| `common/split-words` | `Split` | 14.632 us | 21.080 us | 22.875 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.030 us | 0.044 us | 0.074 us | 0.68x |
| `common/matches-word` | `Count` | 1.422 us | 2.363 us | 3.649 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.100 us | 0.203 us | 0.238 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.035 us | 0.053 us | 0.36x |
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
| `lokad/folding/region-marker` | 6,454.150 us | 36.535 us | 60.756 us | 176.66x |
| `structural/keyword-family-to-capitalized-identifier` | 3,824.200 us | 216.721 us | 228.324 us | 17.65x |
| `lokad/docs/inline-doc-prefix` | 36.588 us | 2.678 us | 3.589 us | 13.66x |
| `lokad/lexer/number` | 86.260 us | 6.887 us | 8.347 us | 12.53x |
| `lokad/langserv/url-dashboard` | 89.317 us | 7.363 us | 7.886 us | 12.13x |
| `lokad/langserv/url-download` | 145.917 us | 12.990 us | 13.483 us | 11.23x |
| `lokad/style/hex-color` | 5.491 us | 0.496 us | 0.613 us | 11.07x |
| `lokad/lexer/doc-line` | 20.241 us | 2.157 us | 3.053 us | 9.38x |
| `lokad/imports/module-imports` | 37.087 us | 4.439 us | 26.804 us | 8.36x |
| `lokad/style/cell-ref` | 13.353 us | 1.749 us | 1.995 us | 7.64x |
| `lokad/style/range-ref` | 10.809 us | 1.469 us | 1.595 us | 7.36x |
| `lokad/lexer/operator-run` | 13.210 us | 1.971 us | 2.578 us | 6.70x |
| `lokad/langserv/helper-identifier` | 31.950 us | 4.882 us | 6.212 us | 6.54x |
| `lokad/lexer/string` | 286.829 us | 44.581 us | 49.472 us | 6.43x |
| `lokad/lexer/identifier` | 106.308 us | 16.652 us | 21.048 us | 6.38x |
| `balancing/begin-end-blocks` | 12,620.675 us | 4,222.675 us | 4,233.725 us | 2.99x |
| `lokad/langserv/color-short-hex` | 1.682 us | 1.104 us | 1.247 us | 1.52x |
| `structural/keyword-to-generic-type` | 5.830 us | 4.118 us | 15.750 us | 1.42x |
| `lokad/langserv/identifier-validator` | 11.724 us | 9.702 us | 11.734 us | 1.21x |
| `structural/ordered-keyword-pair` | 6.110 us | 5.521 us | 17.198 us | 1.11x |
| `fallback/lookahead` | 6.352 us | 6.116 us | 17.564 us | 1.04x |
| `lokad/langserv/color-rgb-hex` | 2.070 us | 2.064 us | 2.219 us | 1.00x |
| `literal-family/method-token-family` | 8.184 us | 9.224 us | 19.857 us | 0.89x |
| `structural/modifier-family-to-type-window` | 210.234 us | 259.023 us | 272.374 us | 0.81x |
| `literal/call-token` | 3.050 us | 4.431 us | 15.650 us | 0.69x |
| `structural/method-family-generic-call` | 4.440 us | 6.529 us | 17.976 us | 0.68x |
| `literal/identifier-token` | 2.795 us | 4.393 us | 15.651 us | 0.64x |
| `structural/method-family-call` | 4.852 us | 8.149 us | 19.027 us | 0.60x |
| `literal-family/type-token-family` | 12.127 us | 25.124 us | 36.032 us | 0.48x |
| `literal/identifier-token-casei` | 7.452 us | 15.546 us | 27.323 us | 0.48x |
| `structural/ordered-keyword-window` | 6.874 us | 18.003 us | 29.815 us | 0.38x |
| `structural/ordered-modifier-pair` | 6.073 us | 17.663 us | 28.785 us | 0.34x |
| `structural/ordered-identifier-window` | 6.493 us | 29.202 us | 40.545 us | 0.22x |
| `structural/modifier-family-to-type-pair` | 24.891 us | 135.047 us | 147.705 us | 0.18x |
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
| `structural/keyword-family-to-capitalized-identifier` | 3,820.514 us | 7.706 us | 18.859 us | 495.80x |
| `lokad/folding/region-marker` | 6,420.163 us | 37.288 us | 58.982 us | 172.18x |
| `balancing/begin-end-blocks` | 12,894.325 us | 471.357 us | 499.274 us | 27.36x |
| `lokad/docs/inline-doc-prefix` | 37.181 us | 2.731 us | 3.643 us | 13.61x |
| `lokad/lexer/number` | 88.666 us | 6.987 us | 8.527 us | 12.69x |
| `lokad/langserv/url-dashboard` | 88.801 us | 7.434 us | 8.055 us | 11.94x |
| `lokad/style/hex-color` | 5.636 us | 0.498 us | 0.595 us | 11.31x |
| `lokad/langserv/url-download` | 145.706 us | 12.969 us | 13.454 us | 11.23x |
| `lokad/lexer/doc-line` | 20.079 us | 2.181 us | 3.067 us | 9.20x |
| `lokad/imports/module-imports` | 37.317 us | 4.443 us | 26.603 us | 8.40x |
| `lokad/style/cell-ref` | 13.998 us | 1.747 us | 1.972 us | 8.01x |
| `lokad/style/range-ref` | 10.727 us | 1.502 us | 1.600 us | 7.14x |
| `lokad/lexer/operator-run` | 13.443 us | 1.958 us | 2.585 us | 6.86x |
| `lokad/lexer/string` | 294.849 us | 44.984 us | 48.666 us | 6.55x |
| `lokad/langserv/helper-identifier` | 31.510 us | 4.847 us | 6.279 us | 6.50x |
| `lokad/lexer/identifier` | 105.150 us | 16.647 us | 20.500 us | 6.32x |
| `structural/modifier-family-to-type-window` | 210.581 us | 56.469 us | 69.163 us | 3.73x |
| `structural/modifier-family-to-type-pair` | 24.921 us | 10.652 us | 21.717 us | 2.34x |
| `structural/keyword-to-generic-type` | 5.833 us | 3.536 us | 14.677 us | 1.65x |
| `lokad/langserv/color-short-hex` | 1.664 us | 1.040 us | 1.260 us | 1.60x |
| `fallback/lookahead` | 6.326 us | 4.242 us | 15.214 us | 1.49x |
| `structural/ordered-keyword-pair` | 6.104 us | 4.183 us | 15.261 us | 1.46x |
| `lokad/langserv/identifier-validator` | 11.723 us | 9.216 us | 11.625 us | 1.27x |
| `literal/identifier-token-casei` | 7.474 us | 5.975 us | 17.260 us | 1.25x |
| `lokad/langserv/color-rgb-hex` | 2.083 us | 1.957 us | 2.165 us | 1.06x |
| `literal-family/type-token-family` | 12.130 us | 11.700 us | 22.231 us | 1.04x |
| `structural/method-family-generic-call` | 4.433 us | 4.631 us | 15.032 us | 0.96x |
| `structural/method-family-call` | 4.847 us | 5.107 us | 15.916 us | 0.95x |
| `literal-family/method-token-family` | 4.782 us | 5.380 us | 15.742 us | 0.89x |
| `structural/ordered-keyword-window` | 6.885 us | 8.642 us | 20.102 us | 0.80x |
| `literal/call-token` | 3.055 us | 4.019 us | 14.705 us | 0.76x |
| `structural/ordered-modifier-pair` | 6.042 us | 8.654 us | 19.744 us | 0.70x |
| `structural/ordered-identifier-window` | 6.484 us | 10.777 us | 21.938 us | 0.60x |
| `literal/identifier-token` | 2.791 us | 4.756 us | 15.064 us | 0.59x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
