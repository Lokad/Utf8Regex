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
| `literal/sherlock-zh` | 133.302 us | 19.028 us | 2,175.613 us | 7.01x |
| `bounded-repeat/letters-en` | 12,403.267 us | 2,865.400 us | 2,951.118 us | 4.33x |
| `literal-alternate/sherlock-zh` | 239.471 us | 71.275 us | 2,519.578 us | 3.36x |
| `bounded-repeat/capitals` | 215,831.500 us | 66,104.000 us | 84,542.000 us | 3.27x |
| `literal/sherlock-ru` | 261.880 us | 100.627 us | 5,408.938 us | 2.60x |
| `ruff-noqa/tweaked` | 15,609.000 us | 9,627.957 us | 54,611.600 us | 1.62x |
| `literal/sherlock-casei-en` | 176.572 us | 192.612 us | 638.928 us | 0.92x |
| `literal/sherlock-en` | 87.655 us | 95.850 us | 511.572 us | 0.91x |
| `aws-keys/quick` | 6,685.238 us | 9,676.320 us | 43,393.900 us | 0.69x |
| `literal/sherlock-casei-ru` | 242.973 us | 389.938 us | 5,695.033 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,697.677 us | 4,847.473 us | 10,130.367 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 200.665 us | 604.019 us | 1,061.381 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,957.315 us | 6,042.844 us | 6,853.900 us | 0.32x |
| `dictionary/single` | 8,388.200 us | 26,052.600 us | 25,998.650 us | 0.32x |
| `literal-alternate/sherlock-en` | 409.215 us | 1,541.909 us | 2,117.238 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 168.054 us | 665.495 us | 1,335.937 us | 0.25x |
| `bounded-repeat/context` | 21,148.200 us | 148,584.500 us | 152,191.800 us | 0.14x |
| `aws-keys/full` | 8,962.280 us | 86,209.200 us | 102,686.700 us | 0.10x |
| `ruff-noqa/real` | 4,679.822 us | 70,735.500 us | 87,237.200 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,532.484 us | 31,541.500 us | 33,967.300 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 330.159 us | 16,641.900 us | 17,653.200 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,780.527 us | 988.390 us | 1,280.449 us | 4.84x |
| `industry/mariomka-email-count` | `Count` | 1,520.615 us | 490.404 us | 8,946.667 us | 3.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 82,988.200 us | 31,167.900 us | 32,561.700 us | 2.66x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.328 us | 0.175 us | 0.229 us | 1.88x |
| `common/match-word` | `Match` | 0.203 us | 0.163 us | 1.153 us | 1.24x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 257.802 us | 262.970 us | 516.956 us | 0.98x |
| `industry/leipzig-twain-count` | `Count` | 1,326.974 us | 1,401.215 us | 16,066.100 us | 0.95x |
| `industry/mariomka-uri-count` | `Count` | 1,765.390 us | 1,954.558 us | 10,446.633 us | 0.90x |
| `common/replace-words` | `Replace` | 69.808 us | 78.338 us | 79.286 us | 0.89x |
| `common/uri-miss` | `IsMatch` | 0.182 us | 0.207 us | 0.226 us | 0.88x |
| `common/uri-match` | `IsMatch` | 0.320 us | 0.394 us | 0.464 us | 0.81x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.104 us | 0.134 us | 0.134 us | 0.77x |
| `common/ip-match` | `IsMatch` | 0.126 us | 0.179 us | 0.201 us | 0.71x |
| `common/email-match` | `IsMatch` | 0.179 us | 0.268 us | 0.292 us | 0.67x |
| `common/ip-miss` | `IsMatch` | 0.121 us | 0.195 us | 0.214 us | 0.62x |
| `common/email-miss` | `IsMatch` | 0.188 us | 0.352 us | 0.377 us | 0.54x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.086 us | 0.169 us | 0.187 us | 0.51x |
| `industry/leipzig-name-family-count` | `Count` | 4,269.158 us | 10,139.160 us | 17,399.100 us | 0.42x |
| `common/one-node-backtracking` | `IsMatch` | 0.214 us | 0.623 us | 0.661 us | 0.34x |
| `common/matches-word` | `Count` | 1.697 us | 5.397 us | 7.562 us | 0.31x |
| `industry/mariomka-ip-count` | `Count` | 2,696.340 us | 8,720.075 us | 31,596.450 us | 0.31x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.108 us | 0.137 us | 0.26x |
| `common/backtracking` | `IsMatch` | 0.197 us | 0.839 us | 0.882 us | 0.23x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.019 us | 0.084 us | 0.101 us | 0.22x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.574 us | 0.612 us | 0.19x |
| `industry/leipzig-river-window-count` | `Count` | 6,139.922 us | 32,656.450 us | 49,199.500 us | 0.19x |
| `common/split-words` | `Split` | 14.806 us | 79.613 us | 81.139 us | 0.19x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.018 us | 0.122 us | 0.151 us | 0.15x |
| `common/matches-words` | `Count` | 9.490 us | 69.603 us | 71.227 us | 0.14x |
| `industry/leipzig-symbol-count` | `Count` | 1,828.625 us | 23,707.200 us | 31,235.400 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 741.097 us | 12,192.883 us | 9,035.783 us | 0.06x |
| `common/matches-boundary` | `Count` | 8.346 us | 174.371 us | 176.008 us | 0.05x |
| `common/matches-set` | `Count` | 8.376 us | 195.611 us | 198.432 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 271.517 us | 11,983.800 us | 12,356.300 us | 0.02x |
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
| `bounded-repeat/capitals` | 215,856.500 us | 17,554.225 us | 28,371.133 us | 12.30x |
| `bounded-repeat/letters-en` | 12,400.400 us | 1,862.929 us | 1,965.165 us | 6.66x |
| `literal/sherlock-zh` | 132.993 us | 24.176 us | 2,187.033 us | 5.50x |
| `literal-alternate/sherlock-zh` | 239.339 us | 44.598 us | 2,219.985 us | 5.37x |
| `literal/sherlock-ru` | 254.134 us | 90.777 us | 5,397.633 us | 2.80x |
| `ruff-noqa/tweaked` | 14,342.600 us | 5,351.764 us | 50,336.500 us | 2.68x |
| `literal/sherlock-casei-en` | 176.922 us | 78.336 us | 455.832 us | 2.26x |
| `aws-keys/quick` | 6,396.812 us | 3,881.654 us | 33,347.600 us | 1.65x |
| `literal-alternate/sherlock-casei-en` | 330.941 us | 203.446 us | 799.000 us | 1.63x |
| `literal/sherlock-casei-ru` | 243.672 us | 200.918 us | 5,603.567 us | 1.21x |
| `literal-alternate/sherlock-en` | 408.828 us | 344.884 us | 813.578 us | 1.19x |
| `literal-alternate/sherlock-en-mixed` | 201.022 us | 181.046 us | 571.483 us | 1.11x |
| `bounded-repeat/letters-ru` | 1,958.477 us | 2,081.888 us | 2,912.367 us | 0.94x |
| `literal-alternate/sherlock-en-nomatch` | 167.969 us | 203.291 us | 673.152 us | 0.83x |
| `literal/sherlock-en` | 71.586 us | 95.917 us | 500.143 us | 0.75x |
| `literal-alternate/sherlock-ru` | 1,689.547 us | 2,365.709 us | 7,564.533 us | 0.71x |
| `literal-alternate/sherlock-casei-ru` | 1,535.733 us | 2,242.173 us | 8,844.400 us | 0.68x |
| `aws-keys/full` | 9,874.440 us | 18,882.233 us | 31,993.250 us | 0.52x |
| `bounded-repeat/context` | 21,581.350 us | 69,664.400 us | 72,863.900 us | 0.31x |
| `ruff-noqa/real` | 4,688.700 us | 21,622.867 us | 34,436.600 us | 0.22x |
| `dictionary/single` | 8,386.850 us | 46,436.050 us | 46,488.150 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,778.827 us | 605.864 us | 878.485 us | 7.89x |
| `industry/rust-sherlock-letter-count` | `Count` | 82,432.200 us | 15,647.100 us | 16,753.850 us | 5.27x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 276.557 us | 59.682 us | 303.314 us | 4.63x |
| `common/backtracking` | `IsMatch` | 0.197 us | 0.044 us | 0.075 us | 4.54x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.324 us | 0.077 us | 0.128 us | 4.21x |
| `industry/leipzig-river-window-count` | `Count` | 6,211.033 us | 1,628.347 us | 17,876.467 us | 3.81x |
| `common/replace-words` | `Replace` | 69.651 us | 18.766 us | 20.255 us | 3.71x |
| `industry/mariomka-email-count` | `Count` | 1,519.468 us | 456.758 us | 7,831.000 us | 3.33x |
| `common/uri-miss` | `IsMatch` | 0.182 us | 0.066 us | 0.086 us | 2.78x |
| `common/match-word` | `Match` | 0.203 us | 0.075 us | 0.993 us | 2.69x |
| `common/ip-match` | `IsMatch` | 0.125 us | 0.047 us | 0.069 us | 2.64x |
| `common/ip-miss` | `IsMatch` | 0.121 us | 0.049 us | 0.072 us | 2.46x |
| `common/one-node-backtracking` | `IsMatch` | 0.214 us | 0.088 us | 0.129 us | 2.43x |
| `common/email-match` | `IsMatch` | 0.180 us | 0.082 us | 0.108 us | 2.19x |
| `common/uri-match` | `IsMatch` | 0.320 us | 0.147 us | 0.205 us | 2.18x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.104 us | 0.048 us | 0.049 us | 2.15x |
| `common/email-miss` | `IsMatch` | 0.189 us | 0.089 us | 0.115 us | 2.12x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.063 us | 0.032 us | 0.049 us | 1.94x |
| `industry/mariomka-uri-count` | `Count` | 1,772.979 us | 1,110.880 us | 8,523.967 us | 1.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.088 us | 0.056 us | 0.079 us | 1.56x |
| `industry/leipzig-symbol-count` | `Count` | 1,895.514 us | 1,276.924 us | 7,015.767 us | 1.48x |
| `industry/leipzig-name-family-count` | `Count` | 4,356.675 us | 3,135.606 us | 8,498.733 us | 1.39x |
| `industry/leipzig-twain-count` | `Count` | 1,370.350 us | 1,410.774 us | 13,519.367 us | 0.97x |
| `common/matches-words` | `Count` | 9.484 us | 11.022 us | 14.046 us | 0.86x |
| `industry/mariomka-ip-count` | `Count` | 2,709.573 us | 3,532.092 us | 25,571.350 us | 0.77x |
| `common/split-words` | `Split` | 14.827 us | 21.553 us | 23.288 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.084 us | 0.67x |
| `common/date-miss` | `IsMatch` | 0.111 us | 0.204 us | 0.236 us | 0.54x |
| `common/matches-word` | `Count` | 1.630 us | 3.090 us | 4.255 us | 0.53x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.020 us | 0.055 us | 0.083 us | 0.36x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 842.461 us | 4,519.777 us | 4,980.908 us | 0.19x |
| `common/matches-boundary` | `Count` | 8.316 us | 105.692 us | 107.623 us | 0.08x |
| `common/matches-set` | `Count` | 8.367 us | 107.719 us | 108.646 us | 0.08x |
| `industry/rust-sherlock-ing-count` | `Count` | 272.531 us | 4,843.209 us | 5,351.738 us | 0.06x |
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
| `lokad/folding/region-marker` | 5,329.290 us | 36.539 us | 61.095 us | 145.85x |
| `lokad/imports/module-imports` | 51.202 us | 9.069 us | 39.502 us | 5.65x |
| `lokad/lexer/identifier` | 101.302 us | 21.445 us | 25.736 us | 4.72x |
| `lokad/langserv/helper-identifier` | 30.129 us | 7.005 us | 7.844 us | 4.30x |
| `lokad/lexer/string` | 311.895 us | 95.086 us | 101.342 us | 3.28x |
| `structural/keyword-to-generic-type` | 5.845 us | 4.139 us | 15.972 us | 1.41x |
| `lokad/langserv/color-short-hex` | 1.880 us | 1.385 us | 1.574 us | 1.36x |
| `lokad/lexer/operator-run` | 6.811 us | 5.146 us | 6.128 us | 1.32x |
| `lokad/lexer/doc-line` | 5.831 us | 4.460 us | 5.957 us | 1.31x |
| `structural/ordered-keyword-pair` | 6.351 us | 5.398 us | 17.183 us | 1.18x |
| `fallback/lookahead` | 6.343 us | 6.117 us | 17.633 us | 1.04x |
| `lokad/docs/inline-doc-prefix` | 7.355 us | 7.323 us | 7.175 us | 1.00x |
| `lokad/langserv/identifier-validator` | 11.972 us | 12.760 us | 14.392 us | 0.94x |
| `lokad/style/hex-color` | 0.779 us | 0.846 us | 0.930 us | 0.92x |
| `literal-family/method-token-family` | 8.146 us | 9.464 us | 19.979 us | 0.86x |
| `lokad/langserv/color-rgb-hex` | 3.187 us | 4.307 us | 4.795 us | 0.74x |
| `lokad/lexer/number` | 10.933 us | 14.886 us | 15.597 us | 0.73x |
| `structural/method-family-generic-call` | 4.461 us | 6.710 us | 18.474 us | 0.66x |
| `literal/call-token` | 3.768 us | 5.758 us | 19.716 us | 0.65x |
| `literal/identifier-token` | 2.826 us | 4.446 us | 16.180 us | 0.64x |
| `structural/method-family-call` | 4.894 us | 8.300 us | 19.054 us | 0.59x |
| `literal-family/type-token-family` | 12.185 us | 24.478 us | 36.402 us | 0.50x |
| `lokad/style/cell-ref` | 2.084 us | 4.296 us | 3.389 us | 0.49x |
| `literal/identifier-token-casei` | 7.370 us | 15.239 us | 27.353 us | 0.48x |
| `lokad/style/range-ref` | 1.992 us | 4.136 us | 4.386 us | 0.48x |
| `structural/ordered-keyword-window` | 6.907 us | 18.209 us | 29.381 us | 0.38x |
| `structural/ordered-modifier-pair` | 6.020 us | 17.578 us | 28.927 us | 0.34x |
| `structural/keyword-family-to-capitalized-identifier` | 75.096 us | 226.435 us | 238.537 us | 0.33x |
| `structural/modifier-family-to-type-window` | 78.053 us | 257.501 us | 270.770 us | 0.30x |
| `structural/ordered-identifier-window` | 6.533 us | 45.035 us | 58.889 us | 0.15x |
| `structural/modifier-family-to-type-pair` | 28.473 us | 217.787 us | 231.625 us | 0.13x |
| `lokad/langserv/url-dashboard` | 5.078 us | 40.945 us | 41.839 us | 0.12x |
| `lokad/langserv/url-download` | 2.911 us | 51.445 us | 51.675 us | 0.06x |
| `balancing/begin-end-blocks` | 7.646 us | 4,438.825 us | 4,451.150 us | 0.0017x |
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
| `lokad/folding/region-marker` | 5,316.170 us | 36.834 us | 60.794 us | 144.33x |
| `structural/keyword-family-to-capitalized-identifier` | 75.252 us | 7.756 us | 19.226 us | 9.70x |
| `lokad/imports/module-imports` | 51.139 us | 5.473 us | 30.611 us | 9.34x |
| `lokad/lexer/string` | 304.325 us | 46.620 us | 50.538 us | 6.53x |
| `lokad/langserv/helper-identifier` | 29.997 us | 4.643 us | 5.786 us | 6.46x |
| `lokad/lexer/identifier` | 100.981 us | 16.479 us | 25.949 us | 6.13x |
| `lokad/lexer/operator-run` | 6.794 us | 2.793 us | 3.806 us | 2.43x |
| `lokad/lexer/doc-line` | 7.009 us | 3.135 us | 4.561 us | 2.24x |
| `structural/modifier-family-to-type-pair` | 28.502 us | 15.310 us | 33.778 us | 1.86x |
| `lokad/langserv/color-short-hex` | 1.971 us | 1.066 us | 1.268 us | 1.85x |
| `lokad/docs/inline-doc-prefix` | 7.335 us | 4.084 us | 3.854 us | 1.80x |
| `structural/keyword-to-generic-type` | 5.814 us | 3.555 us | 14.740 us | 1.64x |
| `structural/ordered-keyword-pair` | 6.295 us | 4.212 us | 15.256 us | 1.49x |
| `fallback/lookahead` | 6.317 us | 4.272 us | 15.301 us | 1.48x |
| `lokad/style/hex-color` | 0.783 us | 0.555 us | 0.634 us | 1.41x |
| `lokad/lexer/number` | 10.792 us | 7.694 us | 8.646 us | 1.40x |
| `structural/modifier-family-to-type-window` | 78.214 us | 56.556 us | 68.975 us | 1.38x |
| `literal/identifier-token-casei` | 7.358 us | 5.942 us | 17.728 us | 1.24x |
| `lokad/langserv/identifier-validator` | 11.958 us | 9.701 us | 11.668 us | 1.23x |
| `lokad/langserv/color-rgb-hex` | 3.197 us | 2.721 us | 3.244 us | 1.17x |
| `literal-family/type-token-family` | 12.140 us | 11.774 us | 22.851 us | 1.03x |
| `structural/method-family-generic-call` | 4.455 us | 4.650 us | 15.468 us | 0.96x |
| `structural/method-family-call` | 4.890 us | 5.117 us | 15.479 us | 0.96x |
| `literal-family/method-token-family` | 4.768 us | 5.347 us | 15.790 us | 0.89x |
| `lokad/style/range-ref` | 1.916 us | 2.338 us | 2.591 us | 0.82x |
| `structural/ordered-keyword-window` | 6.934 us | 8.740 us | 20.134 us | 0.79x |
| `literal/call-token` | 3.786 us | 4.890 us | 17.964 us | 0.77x |
| `lokad/style/cell-ref` | 1.957 us | 2.657 us | 2.156 us | 0.74x |
| `structural/ordered-modifier-pair` | 6.124 us | 8.771 us | 19.738 us | 0.70x |
| `literal/identifier-token` | 2.820 us | 4.761 us | 15.457 us | 0.59x |
| `structural/ordered-identifier-window` | 7.206 us | 15.170 us | 28.326 us | 0.48x |
| `lokad/langserv/url-dashboard` | 5.065 us | 11.490 us | 12.441 us | 0.44x |
| `lokad/langserv/url-download` | 2.899 us | 13.082 us | 13.543 us | 0.22x |
| `balancing/begin-end-blocks` | 7.631 us | 475.876 us | 502.198 us | 0.02x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
