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

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `aws-keys/quick` | 3,892,158.200 us | 10,051.733 us | 45,555.700 us | 387.21x |
| `ruff-noqa/tweaked` | 2,063,869.800 us | 8,034.157 us | 41,922.250 us | 256.89x |
| `aws-keys/full` | 4,910,443.600 us | 88,461.100 us | 122,403.800 us | 55.51x |
| `bounded-repeat/capitals` | 794,366.300 us | 46,578.650 us | 52,698.600 us | 17.05x |
| `bounded-repeat/context` | 1,111,934.300 us | 149,076.700 us | 152,851.300 us | 7.46x |
| `literal/sherlock-zh` | 133.123 us | 18.994 us | 2,189.775 us | 7.01x |
| `ruff-noqa/real` | 430,790.900 us | 72,482.600 us | 88,417.800 us | 5.94x |
| `bounded-repeat/letters-en` | 12,485.575 us | 2,840.983 us | 2,960.547 us | 4.39x |
| `literal-alternate/sherlock-zh` | 236.767 us | 70.602 us | 2,191.292 us | 3.35x |
| `literal/sherlock-ru` | 255.877 us | 103.376 us | 5,234.850 us | 2.48x |
| `literal/sherlock-casei-en` | 170.442 us | 203.364 us | 580.068 us | 0.84x |
| `literal/sherlock-en` | 70.347 us | 87.706 us | 463.807 us | 0.80x |
| `literal/sherlock-casei-ru` | 240.268 us | 390.568 us | 5,631.000 us | 0.62x |
| `literal-alternate/sherlock-ru` | 1,709.150 us | 4,759.091 us | 10,015.333 us | 0.36x |
| `literal-alternate/sherlock-en-mixed` | 208.895 us | 626.532 us | 1,051.467 us | 0.33x |
| `bounded-repeat/letters-ru` | 1,953.265 us | 5,985.222 us | 6,773.225 us | 0.33x |
| `dictionary/single` | 8,323.833 us | 26,444.700 us | 26,444.250 us | 0.31x |
| `literal-alternate/sherlock-en` | 408.495 us | 1,488.371 us | 2,009.388 us | 0.27x |
| `literal-alternate/sherlock-en-nomatch` | 167.728 us | 666.991 us | 1,103.410 us | 0.25x |
| `literal-alternate/sherlock-casei-ru` | 1,111.541 us | 23,797.067 us | 29,467.300 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 217.275 us | 11,136.960 us | 11,971.060 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-ip-count` | `Count` | 1,104,112.200 us | 8,448.067 us | 17,031.800 us | 130.69x |
| `common/one-node-backtracking` | `IsMatch` | 35.128 us | 0.612 us | 0.646 us | 57.39x |
| `industry/leipzig-symbol-count` | `Count` | 1,241,114.200 us | 23,047.033 us | 31,222.800 us | 53.85x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 225,642.800 us | 8,620.200 us | 9,164.300 us | 26.18x |
| `common/uri-miss` | `IsMatch` | 4.753 us | 0.194 us | 0.213 us | 24.49x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 1.334 us | 0.111 us | 0.144 us | 11.99x |
| `common/uri-match` | `IsMatch` | 2.650 us | 0.237 us | 0.276 us | 11.20x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 1.378 us | 0.170 us | 0.186 us | 8.10x |
| `common/ip-miss` | `IsMatch` | 1.151 us | 0.187 us | 0.206 us | 6.15x |
| `common/email-match` | `IsMatch` | 1.571 us | 0.264 us | 0.290 us | 5.95x |
| `common/ip-match` | `IsMatch` | 1.046 us | 0.181 us | 0.201 us | 5.78x |
| `common/email-miss` | `IsMatch` | 1.926 us | 0.355 us | 0.375 us | 5.43x |
| `common/backtracking` | `IsMatch` | 4.403 us | 0.858 us | 0.899 us | 5.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,928.640 us | 981.381 us | 1,254.781 us | 5.02x |
| `common/matches-boundary` | `Count` | 830.828 us | 170.616 us | 172.427 us | 4.87x |
| `industry/mariomka-email-count` | `Count` | 1,527.967 us | 493.433 us | 8,987.433 us | 3.10x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 801.270 us | 263.848 us | 500.793 us | 3.04x |
| `industry/rust-sherlock-letter-count` | `Count` | 46,163.100 us | 19,715.333 us | 20,218.600 us | 2.34x |
| `common/match-word` | `Match` | 0.202 us | 0.166 us | 1.171 us | 1.22x |
| `industry/leipzig-twain-count` | `Count` | 1,326.079 us | 1,404.295 us | 9,048.400 us | 0.94x |
| `industry/mariomka-uri-count` | `Count` | 1,741.359 us | 1,885.529 us | 10,492.567 us | 0.92x |
| `common/replace-words` | `Replace` | 69.198 us | 78.711 us | 80.560 us | 0.88x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.084 us | 0.101 us | 0.59x |
| `industry/leipzig-river-window-count` | `Count` | 19,838.233 us | 34,401.800 us | 49,580.900 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 4,276.442 us | 9,881.850 us | 17,266.550 us | 0.43x |
| `common/matches-set` | `Count` | 81.097 us | 192.243 us | 193.810 us | 0.42x |
| `common/matches-word` | `Count` | 1.419 us | 4.322 us | 6.014 us | 0.33x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.140 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.020 us | 0.084 us | 0.099 us | 0.23x |
| `common/split-words` | `Split` | 14.741 us | 81.390 us | 82.556 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.101 us | 0.562 us | 0.600 us | 0.18x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.076 us | 0.091 us | 0.14x |
| `common/matches-words` | `Count` | 9.467 us | 70.838 us | 72.466 us | 0.13x |
| `industry/rust-sherlock-ing-count` | `Count` | 253.373 us | 11,982.820 us | 12,475.780 us | 0.02x |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `aws-keys/quick` | 3,900,974.700 us | 3,703.293 us | 38,003.750 us | 1053.38x |
| `ruff-noqa/tweaked` | 2,064,267.600 us | 4,592.764 us | 37,106.050 us | 449.46x |
| `aws-keys/full` | 4,920,572.500 us | 19,096.867 us | 50,853.850 us | 257.66x |
| `bounded-repeat/capitals` | 793,819.400 us | 13,723.575 us | 20,810.300 us | 57.84x |
| `ruff-noqa/real` | 431,958.300 us | 21,559.933 us | 36,464.150 us | 20.04x |
| `bounded-repeat/context` | 1,111,491.000 us | 70,050.600 us | 73,051.700 us | 15.87x |
| `literal-alternate/sherlock-zh` | 248.116 us | 36.257 us | 2,177.429 us | 6.84x |
| `bounded-repeat/letters-en` | 12,525.750 us | 1,851.011 us | 1,934.865 us | 6.77x |
| `literal/sherlock-zh` | 133.091 us | 24.115 us | 2,189.733 us | 5.52x |
| `literal/sherlock-ru` | 255.333 us | 90.868 us | 5,232.720 us | 2.81x |
| `literal/sherlock-casei-en` | 170.833 us | 76.838 us | 447.409 us | 2.22x |
| `literal-alternate/sherlock-casei-en` | 217.787 us | 148.012 us | 535.996 us | 1.47x |
| `literal/sherlock-casei-ru` | 239.678 us | 197.966 us | 5,351.960 us | 1.21x |
| `literal-alternate/sherlock-en-mixed` | 208.403 us | 185.632 us | 555.830 us | 1.12x |
| `literal-alternate/sherlock-en` | 410.651 us | 373.917 us | 763.216 us | 1.10x |
| `bounded-repeat/letters-ru` | 1,945.192 us | 2,079.016 us | 2,870.889 us | 0.94x |
| `literal/sherlock-en` | 70.437 us | 76.500 us | 447.005 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 167.467 us | 203.320 us | 585.682 us | 0.82x |
| `literal-alternate/sherlock-ru` | 1,694.103 us | 2,368.123 us | 7,478.733 us | 0.72x |
| `literal-alternate/sherlock-casei-ru` | 1,114.870 us | 1,995.827 us | 7,249.933 us | 0.56x |
| `dictionary/single` | 8,331.983 us | 46,180.500 us | 46,190.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/leipzig-symbol-count` | `Count` | 1,244,717.800 us | 1,298.403 us | 7,652.500 us | 958.65x |
| `common/one-node-backtracking` | `IsMatch` | 35.213 us | 0.086 us | 0.129 us | 409.05x |
| `industry/mariomka-ip-count` | `Count` | 1,104,351.900 us | 3,509.713 us | 11,203.600 us | 314.66x |
| `common/backtracking` | `IsMatch` | 4.379 us | 0.045 us | 0.075 us | 97.34x |
| `common/uri-miss` | `IsMatch` | 4.734 us | 0.064 us | 0.085 us | 73.70x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 225,612.100 us | 3,981.838 us | 4,404.183 us | 56.66x |
| `common/uri-match` | `IsMatch` | 2.648 us | 0.087 us | 0.120 us | 30.43x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 1.347 us | 0.048 us | 0.083 us | 27.80x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 1.368 us | 0.055 us | 0.076 us | 25.09x |
| `common/ip-match` | `IsMatch` | 1.102 us | 0.048 us | 0.068 us | 23.19x |
| `common/ip-miss` | `IsMatch` | 1.113 us | 0.049 us | 0.069 us | 22.87x |
| `common/email-miss` | `IsMatch` | 1.897 us | 0.089 us | 0.117 us | 21.40x |
| `common/email-match` | `IsMatch` | 1.504 us | 0.081 us | 0.108 us | 18.57x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 804.703 us | 59.063 us | 284.053 us | 13.62x |
| `industry/leipzig-river-window-count` | `Count` | 20,635.467 us | 1,670.878 us | 18,346.333 us | 12.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 4,907.982 us | 601.433 us | 856.913 us | 8.16x |
| `common/matches-boundary` | `Count` | 833.818 us | 105.283 us | 106.954 us | 7.92x |
| `industry/rust-sherlock-letter-count` | `Count` | 44,608.200 us | 8,806.067 us | 9,334.167 us | 5.07x |
| `common/replace-words` | `Replace` | 69.094 us | 19.207 us | 21.029 us | 3.60x |
| `industry/mariomka-email-count` | `Count` | 1,538.124 us | 461.103 us | 7,788.133 us | 3.34x |
| `common/match-word` | `Match` | 0.202 us | 0.080 us | 0.999 us | 2.52x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.032 us | 0.049 us | 1.71x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.052 us | 0.032 us | 0.049 us | 1.60x |
| `industry/mariomka-uri-count` | `Count` | 1,745.593 us | 1,125.243 us | 8,536.600 us | 1.55x |
| `industry/leipzig-name-family-count` | `Count` | 4,313.608 us | 3,042.541 us | 8,363.533 us | 1.42x |
| `industry/leipzig-twain-count` | `Count` | 1,328.571 us | 1,341.111 us | 5,675.700 us | 0.99x |
| `common/matches-words` | `Count` | 9.479 us | 10.819 us | 14.056 us | 0.88x |
| `common/matches-set` | `Count` | 81.035 us | 106.697 us | 108.781 us | 0.76x |
| `common/split-words` | `Split` | 14.739 us | 20.867 us | 22.741 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.075 us | 0.66x |
| `common/matches-word` | `Count` | 1.416 us | 2.370 us | 3.629 us | 0.60x |
| `common/date-miss` | `IsMatch` | 0.102 us | 0.205 us | 0.236 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.036 us | 0.053 us | 0.34x |
| `industry/rust-sherlock-ing-count` | `Count` | 253.764 us | 4,827.791 us | 5,258.660 us | 0.05x |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/folding/region-marker` | 6,376.075 us | 33.190 us | 60.740 us | 192.11x |
| `structural/keyword-family-to-capitalized-identifier` | 3,843.214 us | 220.940 us | 233.609 us | 17.39x |
| `lokad/docs/inline-doc-prefix` | 36.033 us | 2.698 us | 3.553 us | 13.35x |
| `lokad/lexer/number` | 84.043 us | 6.884 us | 8.312 us | 12.21x |
| `lokad/langserv/url-dashboard` | 86.607 us | 7.402 us | 7.951 us | 11.70x |
| `lokad/style/hex-color` | 5.690 us | 0.501 us | 0.598 us | 11.36x |
| `lokad/langserv/url-download` | 145.865 us | 12.867 us | 13.345 us | 11.34x |
| `lokad/lexer/doc-line` | 20.105 us | 2.199 us | 3.068 us | 9.14x |
| `lokad/imports/module-imports` | 37.382 us | 4.447 us | 26.223 us | 8.41x |
| `lokad/style/cell-ref` | 13.019 us | 1.687 us | 1.948 us | 7.72x |
| `lokad/style/range-ref` | 10.459 us | 1.473 us | 1.594 us | 7.10x |
| `lokad/lexer/operator-run` | 13.378 us | 1.911 us | 2.523 us | 7.00x |
| `lokad/langserv/helper-identifier` | 29.759 us | 4.531 us | 5.866 us | 6.57x |
| `lokad/lexer/string` | 283.981 us | 43.983 us | 48.468 us | 6.46x |
| `lokad/lexer/identifier` | 99.864 us | 16.102 us | 20.515 us | 6.20x |
| `balancing/begin-end-blocks` | 12,500.300 us | 4,170.700 us | 4,204.608 us | 3.00x |
| `lokad/langserv/color-short-hex` | 1.665 us | 1.067 us | 1.192 us | 1.56x |
| `structural/keyword-to-generic-type` | 5.780 us | 4.145 us | 15.956 us | 1.39x |
| `lokad/langserv/identifier-validator` | 11.687 us | 9.612 us | 11.108 us | 1.22x |
| `structural/ordered-keyword-pair` | 6.093 us | 5.467 us | 16.907 us | 1.11x |
| `lokad/langserv/color-rgb-hex` | 2.087 us | 1.995 us | 2.036 us | 1.05x |
| `fallback/lookahead` | 6.316 us | 6.102 us | 17.124 us | 1.03x |
| `literal-family/method-token-family` | 8.158 us | 9.311 us | 20.080 us | 0.88x |
| `structural/modifier-family-to-type-window` | 212.102 us | 257.382 us | 269.899 us | 0.82x |
| `literal/call-token` | 3.055 us | 4.346 us | 15.673 us | 0.70x |
| `structural/method-family-generic-call` | 4.421 us | 6.706 us | 18.198 us | 0.66x |
| `literal/identifier-token` | 2.790 us | 4.387 us | 15.861 us | 0.64x |
| `structural/method-family-call` | 4.837 us | 8.089 us | 19.052 us | 0.60x |
| `literal-family/type-token-family` | 12.126 us | 24.742 us | 35.551 us | 0.49x |
| `literal/identifier-token-casei` | 7.029 us | 15.009 us | 26.614 us | 0.47x |
| `structural/ordered-keyword-window` | 6.862 us | 17.824 us | 29.608 us | 0.39x |
| `structural/ordered-modifier-pair` | 6.066 us | 17.238 us | 28.949 us | 0.35x |
| `structural/ordered-identifier-window` | 6.501 us | 29.032 us | 39.917 us | 0.22x |
| `structural/modifier-family-to-type-pair` | 24.837 us | 135.881 us | 148.003 us | 0.18x |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `structural/keyword-family-to-capitalized-identifier` | 3,835.357 us | 7.742 us | 19.343 us | 495.40x |
| `lokad/folding/region-marker` | 6,322.875 us | 34.242 us | 59.622 us | 184.65x |
| `balancing/begin-end-blocks` | 12,574.750 us | 470.728 us | 502.145 us | 26.71x |
| `lokad/docs/inline-doc-prefix` | 36.375 us | 2.700 us | 3.589 us | 13.47x |
| `lokad/lexer/number` | 85.267 us | 6.988 us | 8.397 us | 12.20x |
| `lokad/langserv/url-dashboard` | 86.701 us | 7.277 us | 7.872 us | 11.91x |
| `lokad/langserv/url-download` | 145.640 us | 12.897 us | 13.438 us | 11.29x |
| `lokad/style/hex-color` | 5.525 us | 0.492 us | 0.589 us | 11.23x |
| `lokad/lexer/doc-line` | 19.975 us | 2.198 us | 3.022 us | 9.09x |
| `lokad/imports/module-imports` | 37.323 us | 4.441 us | 26.372 us | 8.40x |
| `lokad/style/cell-ref` | 13.274 us | 1.684 us | 1.918 us | 7.88x |
| `lokad/style/range-ref` | 10.671 us | 1.484 us | 1.594 us | 7.19x |
| `lokad/lexer/operator-run` | 13.279 us | 1.903 us | 2.544 us | 6.98x |
| `lokad/langserv/helper-identifier` | 29.577 us | 4.554 us | 5.876 us | 6.49x |
| `lokad/lexer/string` | 285.263 us | 44.390 us | 48.548 us | 6.43x |
| `lokad/lexer/identifier` | 99.110 us | 16.102 us | 20.506 us | 6.16x |
| `structural/modifier-family-to-type-window` | 211.891 us | 56.144 us | 67.842 us | 3.77x |
| `structural/modifier-family-to-type-pair` | 24.858 us | 10.864 us | 21.721 us | 2.29x |
| `lokad/langserv/color-short-hex` | 1.662 us | 0.981 us | 1.209 us | 1.69x |
| `structural/keyword-to-generic-type` | 5.798 us | 3.551 us | 14.909 us | 1.63x |
| `fallback/lookahead` | 6.318 us | 4.245 us | 14.912 us | 1.49x |
| `structural/ordered-keyword-pair` | 6.117 us | 4.180 us | 15.033 us | 1.46x |
| `lokad/langserv/identifier-validator` | 11.740 us | 8.895 us | 11.360 us | 1.32x |
| `literal/identifier-token-casei` | 7.020 us | 5.994 us | 17.190 us | 1.17x |
| `lokad/langserv/color-rgb-hex` | 2.078 us | 1.801 us | 2.037 us | 1.15x |
| `literal-family/type-token-family` | 12.115 us | 11.557 us | 22.248 us | 1.05x |
| `structural/method-family-generic-call` | 4.449 us | 4.641 us | 15.277 us | 0.96x |
| `structural/method-family-call` | 4.850 us | 5.097 us | 15.726 us | 0.95x |
| `literal-family/method-token-family` | 4.766 us | 5.350 us | 15.928 us | 0.89x |
| `structural/ordered-keyword-window` | 6.847 us | 8.666 us | 20.137 us | 0.79x |
| `literal/call-token` | 3.051 us | 3.984 us | 14.635 us | 0.77x |
| `structural/ordered-modifier-pair` | 6.024 us | 8.664 us | 20.059 us | 0.70x |
| `structural/ordered-identifier-window` | 6.491 us | 10.851 us | 21.463 us | 0.60x |
| `literal/identifier-token` | 2.784 us | 4.732 us | 15.594 us | 0.59x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
