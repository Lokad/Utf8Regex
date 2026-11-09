# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.

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
| [Lokad.Utf8Regex.Posix](src/Lokad.Utf8Regex.Posix/README.md) | POSIX ERE subset with leftmost-longest selection and hierarchical capture priority; new in 0.5.0 |

Adapters require the matching Core package version. Their specifications
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

The tables were refreshed on **9 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **46m13s**. The [run receipt](bench/Experiments/DeliveryDev61-20261109/full-refresh.json)
records the scope and timings; package and consumer qualification are separate.
Each row retains its recorded implementation. Selective updates do not imply a
new complete run; the full-run receipt applies to its recorded revision.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

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
| `literal/sherlock-zh` | 82.386 us | 18.945 us | 2,182.177 us | 4.35x |
| `literal/sherlock-ru` | 169.659 us | 98.186 us | 5,281.107 us | 1.73x |
| `literal-alternate/sherlock-zh` | 105.251 us | 70.057 us | 2,189.641 us | 1.50x |
| `literal/sherlock-casei-en` | 161.532 us | 190.833 us | 608.487 us | 0.85x |
| `literal/sherlock-en` | 56.460 us | 85.741 us | 426.687 us | 0.66x |
| `literal/sherlock-casei-ru` | 249.654 us | 391.198 us | 5,623.062 us | 0.64x |
| `ruff-noqa/tweaked` | 4,488.541 us | 8,029.470 us | 44,106.950 us | 0.56x |
| `aws-keys/quick` | 4,884.531 us | 9,628.062 us | 40,961.400 us | 0.51x |
| `bounded-repeat/letters-en` | 1,115.032 us | 2,852.689 us | 2,960.281 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,667.580 us | 4,901.925 us | 10,200.250 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,996.345 us | 6,041.269 us | 6,840.700 us | 0.33x |
| `dictionary/single` | 8,345.460 us | 26,305.267 us | 26,320.900 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 185.813 us | 600.604 us | 996.295 us | 0.31x |
| `literal-alternate/sherlock-en` | 385.864 us | 1,512.200 us | 2,083.586 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 153.498 us | 665.073 us | 1,080.561 us | 0.23x |
| `bounded-repeat/capitals` | 9,469.062 us | 46,195.850 us | 53,251.800 us | 0.20x |
| `bounded-repeat/context` | 20,761.300 us | 148,648.700 us | 152,580.200 us | 0.14x |
| `aws-keys/full` | 9,501.143 us | 86,932.000 us | 119,718.900 us | 0.11x |
| `ruff-noqa/real` | 4,391.978 us | 70,693.050 us | 87,995.700 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 959.637 us | 23,193.475 us | 28,405.150 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 125.099 us | 11,526.000 us | 12,348.886 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,207.983 us | 491.920 us | 8,992.700 us | 2.46x |
| `common/match-word` | `Match` | 0.200 us | 0.167 us | 1.138 us | 1.19x |
| `industry/leipzig-twain-count` | `Count` | 1,162.860 us | 1,336.315 us | 8,725.200 us | 0.87x |
| `industry/mariomka-uri-count` | `Count` | 1,439.428 us | 1,911.510 us | 10,560.575 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.112 us | 0.151 us | 0.71x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.195 us | 0.213 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.101 us | 0.60x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.152 us | 266.271 us | 497.992 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.079 us | 0.164 us | 0.181 us | 0.48x |
| `common/ip-match` | `IsMatch` | 0.077 us | 0.183 us | 0.204 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,013.142 us | 9,857.575 us | 17,249.100 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.241 us | 0.279 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.192 us | 0.213 us | 0.39x |
| `common/replace-words` | `Replace` | 28.240 us | 76.762 us | 78.309 us | 0.37x |
| `common/email-match` | `IsMatch` | 0.097 us | 0.278 us | 0.306 us | 0.35x |
| `common/matches-word` | `Count` | 1.350 us | 4.291 us | 6.077 us | 0.31x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.024 us | 0.082 us | 0.098 us | 0.30x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.348 us | 0.373 us | 0.29x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.108 us | 0.139 us | 0.27x |
| `industry/mariomka-ip-count` | `Count` | 2,248.615 us | 8,635.133 us | 17,279.633 us | 0.26x |
| `common/split-words` | `Split` | 14.578 us | 78.577 us | 80.152 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.163 us | 967.294 us | 1,235.410 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.087 us | 0.565 us | 0.596 us | 0.15x |
| `common/matches-words` | `Count` | 9.379 us | 70.807 us | 72.467 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.622 us | 0.655 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.091 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.098 us | 0.847 us | 0.894 us | 0.12x |
| `industry/leipzig-river-window-count` | `Count` | 3,356.822 us | 32,564.100 us | 40,175.900 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,961.171 us | 19,765.300 us | 20,331.750 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,845.771 us | 22,932.475 us | 30,200.950 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 480.329 us | 8,440.722 us | 8,875.533 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.862 us | 166.090 us | 167.886 us | 0.05x |
| `common/matches-set` | `Count` | 7.885 us | 195.718 us | 197.374 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 265.344 us | 11,891.714 us | 12,425.350 us | 0.02x |
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
| `literal/sherlock-zh` | 82.767 us | 24.066 us | 2,185.203 us | 3.44x |
| `literal-alternate/sherlock-zh` | 105.150 us | 36.714 us | 2,149.047 us | 2.86x |
| `literal/sherlock-casei-en` | 162.151 us | 79.070 us | 407.749 us | 2.05x |
| `literal/sherlock-ru` | 170.065 us | 89.179 us | 5,290.443 us | 1.91x |
| `aws-keys/quick` | 4,880.906 us | 3,608.807 us | 36,077.000 us | 1.35x |
| `literal-alternate/sherlock-en` | 389.347 us | 341.999 us | 771.219 us | 1.14x |
| `literal/sherlock-casei-ru` | 250.528 us | 222.537 us | 5,376.036 us | 1.13x |
| `literal-alternate/sherlock-en-mixed` | 185.752 us | 176.728 us | 550.270 us | 1.05x |
| `ruff-noqa/tweaked` | 4,513.741 us | 4,622.653 us | 39,646.050 us | 0.98x |
| `bounded-repeat/letters-ru` | 1,992.926 us | 2,074.449 us | 2,875.154 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 124.577 us | 133.761 us | 562.143 us | 0.93x |
| `literal/sherlock-en` | 56.511 us | 75.223 us | 405.606 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 153.251 us | 207.645 us | 575.481 us | 0.74x |
| `bounded-repeat/capitals` | 9,460.288 us | 13,682.683 us | 21,634.950 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,622.340 us | 2,377.637 us | 7,517.875 us | 0.68x |
| `bounded-repeat/letters-en` | 1,116.562 us | 1,848.515 us | 1,926.367 us | 0.60x |
| `aws-keys/full` | 9,495.814 us | 18,552.680 us | 53,261.300 us | 0.51x |
| `literal-alternate/sherlock-casei-ru` | 963.404 us | 1,982.950 us | 7,111.025 us | 0.49x |
| `bounded-repeat/context` | 21,231.675 us | 70,178.500 us | 73,843.250 us | 0.30x |
| `ruff-noqa/real` | 4,417.676 us | 21,048.475 us | 34,422.000 us | 0.21x |
| `dictionary/single` | 8,351.211 us | 46,261.900 us | 46,298.800 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,214.556 us | 459.520 us | 7,775.725 us | 2.64x |
| `common/match-word` | `Match` | 0.199 us | 0.081 us | 0.992 us | 2.47x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 134.948 us | 58.916 us | 293.617 us | 2.29x |
| `industry/leipzig-river-window-count` | `Count` | 3,360.196 us | 1,490.955 us | 7,650.350 us | 2.25x |
| `common/backtracking` | `IsMatch` | 0.097 us | 0.044 us | 0.074 us | 2.22x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.068 us | 0.085 us | 1.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.052 us | 0.031 us | 0.049 us | 1.65x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.049 us | 0.088 us | 1.63x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.031 us | 0.048 us | 1.62x |
| `common/ip-match` | `IsMatch` | 0.077 us | 0.048 us | 0.070 us | 1.61x |
| `common/replace-words` | `Replace` | 28.239 us | 17.878 us | 20.703 us | 1.58x |
| `common/ip-miss` | `IsMatch` | 0.075 us | 0.050 us | 0.070 us | 1.51x |
| `industry/leipzig-symbol-count` | `Count` | 1,845.580 us | 1,245.521 us | 7,238.640 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.077 us | 0.057 us | 0.078 us | 1.35x |
| `industry/leipzig-name-family-count` | `Count` | 4,068.294 us | 3,090.860 us | 8,278.700 us | 1.32x |
| `industry/mariomka-uri-count` | `Count` | 1,441.170 us | 1,096.551 us | 8,475.325 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.082 us | 0.108 us | 1.17x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.090 us | 0.117 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.090 us | 0.120 us | 1.07x |
| `industry/leipzig-twain-count` | `Count` | 1,172.974 us | 1,293.832 us | 7,191.738 us | 0.91x |
| `common/one-node-backtracking` | `IsMatch` | 0.080 us | 0.089 us | 0.129 us | 0.90x |
| `common/matches-words` | `Count` | 9.380 us | 10.792 us | 14.017 us | 0.87x |
| `common/split-words` | `Split` | 14.588 us | 20.716 us | 22.388 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.073 us | 0.65x |
| `industry/mariomka-ip-count` | `Count` | 2,246.085 us | 3,487.086 us | 11,076.567 us | 0.64x |
| `common/matches-word` | `Count` | 1.349 us | 2.383 us | 3.721 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.199 us | 0.237 us | 0.44x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,967.531 us | 8,928.611 us | 9,505.625 us | 0.22x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 178.362 us | 1,490.571 us | 1,823.745 us | 0.12x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 474.954 us | 3,986.937 us | 4,329.150 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.827 us | 105.349 us | 107.140 us | 0.07x |
| `common/matches-set` | `Count` | 7.883 us | 106.512 us | 108.173 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 262.941 us | 4,810.556 us | 5,240.287 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.235 us | 6.445 us | 29.187 us | 1.59x |
| `structural/keyword-to-generic-type` | 5.555 us | 4.100 us | 15.752 us | 1.36x |
| `lokad/langserv/color-short-hex` | 1.687 us | 1.383 us | 1.472 us | 1.22x |
| `lokad/lexer/doc-line` | 3.387 us | 3.080 us | 3.611 us | 1.10x |
| `lokad/lexer/identifier` | 23.936 us | 23.198 us | 27.020 us | 1.03x |
| `structural/ordered-keyword-pair` | 5.392 us | 5.391 us | 17.091 us | 1.00x |
| `lokad/langserv/helper-identifier` | 7.254 us | 7.381 us | 7.932 us | 0.98x |
| `fallback/lookahead` | 5.628 us | 6.088 us | 17.148 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.339 us | 12.521 us | 14.253 us | 0.91x |
| `lokad/lexer/operator-run` | 2.913 us | 3.470 us | 3.820 us | 0.84x |
| `literal-family/method-token-family` | 7.175 us | 9.319 us | 19.892 us | 0.77x |
| `lokad/style/hex-color` | 0.608 us | 0.842 us | 0.887 us | 0.72x |
| `lokad/docs/inline-doc-prefix` | 3.403 us | 4.799 us | 5.404 us | 0.71x |
| `lokad/langserv/color-rgb-hex` | 2.027 us | 2.951 us | 2.983 us | 0.69x |
| `literal/identifier-token` | 2.795 us | 4.402 us | 15.754 us | 0.63x |
| `literal/call-token` | 2.635 us | 4.376 us | 15.544 us | 0.60x |
| `lokad/lexer/number` | 8.367 us | 14.191 us | 15.438 us | 0.59x |
| `structural/ordered-identifier-window` | 16.185 us | 28.951 us | 40.075 us | 0.56x |
| `lokad/style/cell-ref` | 1.481 us | 2.871 us | 2.965 us | 0.52x |
| `structural/method-family-generic-call` | 3.448 us | 6.710 us | 17.895 us | 0.51x |
| `structural/method-family-call` | 3.982 us | 8.187 us | 18.799 us | 0.49x |
| `literal-family/type-token-family` | 10.569 us | 23.947 us | 35.543 us | 0.44x |
| `literal/identifier-token-casei` | 6.161 us | 15.225 us | 26.486 us | 0.40x |
| `lokad/style/range-ref` | 0.922 us | 2.779 us | 2.757 us | 0.33x |
| `structural/ordered-modifier-pair` | 5.639 us | 17.708 us | 28.603 us | 0.32x |
| `structural/ordered-keyword-window` | 5.537 us | 17.814 us | 28.932 us | 0.31x |
| `structural/modifier-family-to-type-window` | 78.111 us | 255.472 us | 268.164 us | 0.31x |
| `lokad/lexer/string` | 22.041 us | 95.891 us | 101.518 us | 0.23x |
| `lokad/folding/region-marker` | 6.036 us | 36.481 us | 61.074 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.541 us | 24.696 us | 25.028 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.474 us | 222.097 us | 233.573 us | 0.05x |
| `lokad/langserv/url-download` | 2.522 us | 53.466 us | 53.603 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.043 us | 136.381 us | 147.622 us | 0.04x |
| `balancing/begin-end-blocks` | 5.805 us | 4,384.961 us | 4,399.729 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.318 us | 4.442 us | 26.337 us | 2.32x |
| `lokad/langserv/color-short-hex` | 1.689 us | 1.017 us | 1.182 us | 1.66x |
| `structural/keyword-to-generic-type` | 5.572 us | 3.553 us | 14.679 us | 1.57x |
| `lokad/langserv/helper-identifier` | 7.242 us | 4.666 us | 5.766 us | 1.55x |
| `lokad/lexer/doc-line` | 3.356 us | 2.195 us | 2.922 us | 1.53x |
| `structural/ordered-identifier-window` | 16.171 us | 10.744 us | 21.607 us | 1.51x |
| `structural/keyword-family-to-capitalized-identifier` | 11.482 us | 7.861 us | 18.585 us | 1.46x |
| `lokad/lexer/operator-run` | 2.913 us | 2.017 us | 2.510 us | 1.44x |
| `lokad/lexer/identifier` | 23.970 us | 17.211 us | 21.674 us | 1.39x |
| `structural/modifier-family-to-type-window` | 78.206 us | 56.429 us | 68.884 us | 1.39x |
| `fallback/lookahead` | 5.641 us | 4.261 us | 14.952 us | 1.32x |
| `structural/ordered-keyword-pair` | 5.397 us | 4.171 us | 15.240 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.349 us | 9.097 us | 11.447 us | 1.25x |
| `lokad/docs/inline-doc-prefix` | 3.399 us | 2.772 us | 3.514 us | 1.23x |
| `lokad/style/hex-color` | 0.608 us | 0.514 us | 0.602 us | 1.18x |
| `lokad/lexer/number` | 8.366 us | 7.496 us | 8.567 us | 1.12x |
| `lokad/langserv/color-rgb-hex` | 2.043 us | 1.861 us | 2.068 us | 1.10x |
| `literal/identifier-token-casei` | 6.149 us | 5.926 us | 17.218 us | 1.04x |
| `literal-family/type-token-family` | 10.543 us | 11.560 us | 22.426 us | 0.91x |
| `lokad/style/cell-ref` | 1.481 us | 1.745 us | 1.923 us | 0.85x |
| `structural/method-family-call` | 3.974 us | 5.097 us | 15.526 us | 0.78x |
| `structural/method-family-generic-call` | 3.460 us | 4.524 us | 15.083 us | 0.76x |
| `literal-family/method-token-family` | 3.924 us | 5.348 us | 15.779 us | 0.73x |
| `literal/call-token` | 2.639 us | 4.002 us | 14.618 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.656 us | 8.690 us | 19.574 us | 0.65x |
| `structural/ordered-keyword-window` | 5.524 us | 8.644 us | 19.460 us | 0.64x |
| `lokad/style/range-ref` | 0.921 us | 1.496 us | 1.571 us | 0.62x |
| `literal/identifier-token` | 2.801 us | 4.740 us | 15.596 us | 0.59x |
| `lokad/lexer/string` | 21.975 us | 46.204 us | 49.220 us | 0.48x |
| `structural/modifier-family-to-type-pair` | 5.033 us | 10.685 us | 22.000 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.567 us | 7.530 us | 8.062 us | 0.34x |
| `lokad/langserv/url-download` | 2.537 us | 13.010 us | 13.624 us | 0.19x |
| `lokad/folding/region-marker` | 6.038 us | 36.908 us | 60.914 us | 0.16x |
| `balancing/begin-end-blocks` | 5.819 us | 470.757 us | 499.237 us | 0.01x |
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
