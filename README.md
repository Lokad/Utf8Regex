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

The tables were refreshed on **8 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m26s**. The [run receipt](bench/Experiments/DeliveryDev56-20261108/full-refresh.json)
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
| `literal/sherlock-zh` | 82.340 us | 18.874 us | 2,177.689 us | 4.36x |
| `literal/sherlock-ru` | 166.865 us | 98.205 us | 5,266.700 us | 1.70x |
| `literal-alternate/sherlock-zh` | 104.517 us | 70.409 us | 2,182.840 us | 1.48x |
| `literal/sherlock-casei-en` | 165.403 us | 192.210 us | 588.706 us | 0.86x |
| `literal/sherlock-en` | 57.741 us | 85.508 us | 466.181 us | 0.68x |
| `literal/sherlock-casei-ru` | 250.688 us | 387.684 us | 5,583.993 us | 0.65x |
| `aws-keys/quick` | 6,076.950 us | 9,774.163 us | 44,913.300 us | 0.62x |
| `ruff-noqa/tweaked` | 4,342.583 us | 7,549.182 us | 41,359.250 us | 0.58x |
| `bounded-repeat/letters-en` | 1,086.696 us | 2,821.696 us | 2,921.515 us | 0.39x |
| `literal-alternate/sherlock-ru` | 1,677.341 us | 4,758.619 us | 9,847.550 us | 0.35x |
| `bounded-repeat/letters-ru` | 1,965.792 us | 5,986.569 us | 6,760.125 us | 0.33x |
| `dictionary/single` | 8,299.640 us | 26,400.600 us | 26,422.933 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 186.570 us | 598.480 us | 1,013.692 us | 0.31x |
| `literal-alternate/sherlock-en` | 381.738 us | 1,523.712 us | 2,033.103 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 163.388 us | 670.414 us | 1,075.969 us | 0.24x |
| `bounded-repeat/capitals` | 9,530.925 us | 45,576.000 us | 49,064.650 us | 0.21x |
| `bounded-repeat/context` | 21,502.375 us | 148,424.800 us | 152,526.100 us | 0.14x |
| `aws-keys/full` | 10,670.211 us | 87,622.600 us | 116,713.600 us | 0.12x |
| `ruff-noqa/real` | 4,535.265 us | 70,728.250 us | 86,789.000 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 955.177 us | 23,075.400 us | 28,569.450 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 131.504 us | 11,051.557 us | 11,884.971 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,181.834 us | 491.617 us | 8,998.800 us | 2.40x |
| `common/match-word` | `Match` | 0.199 us | 0.173 us | 1.146 us | 1.15x |
| `industry/leipzig-twain-count` | `Count` | 1,213.122 us | 1,358.088 us | 8,749.520 us | 0.89x |
| `industry/mariomka-uri-count` | `Count` | 1,450.356 us | 1,942.020 us | 10,508.800 us | 0.75x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.081 us | 0.109 us | 0.146 us | 0.75x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.100 us | 0.60x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.208 us | 0.226 us | 0.57x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.711 us | 267.198 us | 502.667 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.164 us | 0.182 us | 0.47x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.176 us | 0.196 us | 0.42x |
| `industry/leipzig-name-family-count` | `Count` | 4,101.363 us | 9,840.237 us | 16,833.433 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.187 us | 0.205 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.234 us | 0.272 us | 0.39x |
| `common/replace-words` | `Replace` | 28.109 us | 76.285 us | 77.976 us | 0.37x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.030 us | 0.083 us | 0.099 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.275 us | 0.302 us | 0.35x |
| `common/matches-word` | `Count` | 1.345 us | 4.284 us | 5.999 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.346 us | 0.376 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,206.212 us | 8,327.510 us | 16,810.033 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.138 us | 0.26x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.803 us | 959.098 us | 1,226.149 us | 0.18x |
| `common/split-words` | `Split` | 14.724 us | 80.432 us | 82.098 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.558 us | 0.591 us | 0.17x |
| `common/matches-words` | `Count` | 9.375 us | 70.949 us | 72.763 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.620 us | 0.655 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.077 us | 0.090 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.885 us | 0.924 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,937.090 us | 19,481.350 us | 20,141.950 us | 0.10x |
| `industry/leipzig-river-window-count` | `Count` | 3,317.348 us | 34,039.667 us | 41,130.550 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,829.457 us | 22,787.575 us | 30,365.200 us | 0.08x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 508.880 us | 8,763.144 us | 9,024.578 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.487 us | 163.772 us | 165.174 us | 0.05x |
| `common/matches-set` | `Count` | 7.496 us | 194.675 us | 197.638 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.073 us | 11,804.114 us | 12,414.643 us | 0.02x |
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
| `literal/sherlock-zh` | 82.642 us | 23.941 us | 2,156.853 us | 3.45x |
| `literal-alternate/sherlock-zh` | 104.349 us | 36.108 us | 2,124.953 us | 2.89x |
| `literal/sherlock-casei-en` | 167.048 us | 85.076 us | 423.611 us | 1.96x |
| `literal/sherlock-ru` | 167.110 us | 88.717 us | 5,249.100 us | 1.88x |
| `aws-keys/quick` | 6,527.342 us | 3,604.990 us | 36,308.800 us | 1.81x |
| `literal/sherlock-casei-ru` | 251.785 us | 195.473 us | 5,310.627 us | 1.29x |
| `literal-alternate/sherlock-en` | 380.361 us | 346.921 us | 731.378 us | 1.10x |
| `literal-alternate/sherlock-en-mixed` | 185.566 us | 179.218 us | 542.756 us | 1.04x |
| `ruff-noqa/tweaked` | 4,407.089 us | 4,588.835 us | 35,780.750 us | 0.96x |
| `bounded-repeat/letters-ru` | 1,963.446 us | 2,164.914 us | 2,979.692 us | 0.91x |
| `literal-alternate/sherlock-casei-en` | 131.972 us | 146.676 us | 627.531 us | 0.90x |
| `literal-alternate/sherlock-en-nomatch` | 163.050 us | 203.658 us | 585.635 us | 0.80x |
| `literal/sherlock-en` | 57.955 us | 75.444 us | 447.582 us | 0.77x |
| `bounded-repeat/capitals` | 9,584.625 us | 13,779.000 us | 16,826.575 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,623.791 us | 2,365.366 us | 7,427.250 us | 0.69x |
| `bounded-repeat/letters-en` | 1,084.139 us | 1,835.155 us | 1,915.172 us | 0.59x |
| `aws-keys/full` | 10,710.275 us | 18,607.120 us | 50,392.300 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 953.643 us | 1,960.074 us | 7,158.275 us | 0.49x |
| `bounded-repeat/context` | 20,610.825 us | 69,512.350 us | 72,927.300 us | 0.30x |
| `ruff-noqa/real` | 4,542.829 us | 20,793.425 us | 48,459.933 us | 0.22x |
| `dictionary/single` | 8,309.733 us | 45,818.600 us | 45,860.000 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.199 us | 0.076 us | 1.019 us | 2.63x |
| `industry/mariomka-email-count` | `Count` | 1,185.890 us | 457.047 us | 7,757.900 us | 2.59x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 132.681 us | 58.395 us | 251.742 us | 2.27x |
| `industry/leipzig-river-window-count` | `Count` | 3,356.226 us | 1,494.108 us | 7,838.960 us | 2.25x |
| `common/backtracking` | `IsMatch` | 0.095 us | 0.045 us | 0.075 us | 2.13x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.029 us | 0.049 us | 1.74x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.067 us | 0.087 us | 1.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.049 us | 0.087 us | 1.68x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.049 us | 0.030 us | 0.046 us | 1.65x |
| `common/replace-words` | `Replace` | 28.209 us | 17.313 us | 20.150 us | 1.63x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.050 us | 0.070 us | 1.55x |
| `industry/leipzig-symbol-count` | `Count` | 1,833.012 us | 1,232.267 us | 6,864.125 us | 1.49x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.050 us | 0.070 us | 1.48x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.076 us | 0.056 us | 0.077 us | 1.35x |
| `industry/leipzig-name-family-count` | `Count` | 4,096.321 us | 3,075.804 us | 8,421.775 us | 1.33x |
| `industry/mariomka-uri-count` | `Count` | 1,452.679 us | 1,096.159 us | 8,471.350 us | 1.33x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.117 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.088 us | 0.120 us | 1.02x |
| `common/one-node-backtracking` | `IsMatch` | 0.081 us | 0.088 us | 0.128 us | 0.92x |
| `industry/leipzig-twain-count` | `Count` | 1,213.677 us | 1,332.488 us | 7,666.227 us | 0.91x |
| `common/matches-words` | `Count` | 9.350 us | 10.950 us | 14.042 us | 0.85x |
| `common/split-words` | `Split` | 14.758 us | 21.260 us | 23.204 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.044 us | 0.074 us | 0.66x |
| `industry/mariomka-ip-count` | `Count` | 2,208.006 us | 3,464.414 us | 10,923.400 us | 0.64x |
| `common/matches-word` | `Count` | 1.344 us | 2.374 us | 3.529 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.198 us | 0.237 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.036 us | 0.052 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,939.692 us | 8,812.167 us | 9,358.400 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.645 us | 3,930.540 us | 4,297.039 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.455 us | 1,472.763 us | 1,808.960 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.454 us | 105.090 us | 106.885 us | 0.07x |
| `common/matches-set` | `Count` | 7.474 us | 106.310 us | 108.012 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 263.087 us | 4,821.225 us | 5,245.293 us | 0.05x |
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
| `lokad/imports/module-imports` | 10.470 us | 6.347 us | 29.489 us | 1.65x |
| `structural/keyword-to-generic-type` | 5.522 us | 4.078 us | 16.371 us | 1.35x |
| `lokad/langserv/color-short-hex` | 1.666 us | 1.362 us | 1.480 us | 1.22x |
| `lokad/lexer/doc-line` | 3.368 us | 2.987 us | 3.587 us | 1.13x |
| `structural/ordered-keyword-pair` | 5.590 us | 5.335 us | 16.818 us | 1.05x |
| `lokad/lexer/identifier` | 23.937 us | 23.172 us | 25.395 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.167 us | 7.339 us | 7.873 us | 0.98x |
| `fallback/lookahead` | 5.674 us | 6.088 us | 17.146 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.323 us | 12.555 us | 13.825 us | 0.90x |
| `lokad/lexer/operator-run` | 2.915 us | 3.517 us | 3.883 us | 0.83x |
| `literal-family/method-token-family` | 7.545 us | 9.212 us | 19.870 us | 0.82x |
| `lokad/style/hex-color` | 0.608 us | 0.808 us | 0.864 us | 0.75x |
| `lokad/docs/inline-doc-prefix` | 3.432 us | 4.796 us | 5.375 us | 0.72x |
| `lokad/langserv/color-rgb-hex` | 2.042 us | 2.969 us | 2.960 us | 0.69x |
| `literal/identifier-token` | 2.792 us | 4.390 us | 15.598 us | 0.64x |
| `literal/call-token` | 2.620 us | 4.322 us | 15.755 us | 0.61x |
| `lokad/lexer/number` | 8.349 us | 14.569 us | 15.299 us | 0.57x |
| `structural/ordered-identifier-window` | 15.985 us | 28.688 us | 39.730 us | 0.56x |
| `structural/method-family-generic-call` | 3.456 us | 6.648 us | 18.179 us | 0.52x |
| `lokad/style/cell-ref` | 1.502 us | 2.899 us | 2.959 us | 0.52x |
| `structural/method-family-call` | 3.910 us | 8.263 us | 19.218 us | 0.47x |
| `literal-family/type-token-family` | 10.529 us | 24.155 us | 35.345 us | 0.44x |
| `literal/identifier-token-casei` | 5.894 us | 16.813 us | 27.557 us | 0.35x |
| `lokad/style/range-ref` | 0.926 us | 2.739 us | 2.693 us | 0.34x |
| `structural/ordered-modifier-pair` | 5.452 us | 17.249 us | 28.286 us | 0.32x |
| `structural/modifier-family-to-type-window` | 80.702 us | 255.557 us | 268.035 us | 0.32x |
| `structural/ordered-keyword-window` | 5.487 us | 17.923 us | 28.938 us | 0.31x |
| `lokad/lexer/string` | 23.016 us | 96.076 us | 101.364 us | 0.24x |
| `lokad/folding/region-marker` | 5.999 us | 35.880 us | 60.165 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.561 us | 24.475 us | 24.765 us | 0.10x |
| `lokad/langserv/url-download` | 2.797 us | 52.526 us | 53.096 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 11.454 us | 220.070 us | 230.427 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.994 us | 129.456 us | 141.313 us | 0.04x |
| `balancing/begin-end-blocks` | 5.769 us | 4,420.039 us | 4,430.035 us | 0.0013x |
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
| `lokad/imports/module-imports` | 10.473 us | 4.407 us | 26.746 us | 2.38x |
| `lokad/langserv/color-short-hex` | 1.669 us | 0.998 us | 1.187 us | 1.67x |
| `structural/keyword-to-generic-type` | 5.533 us | 3.514 us | 15.200 us | 1.57x |
| `lokad/langserv/helper-identifier` | 7.196 us | 4.634 us | 5.708 us | 1.55x |
| `lokad/lexer/doc-line` | 3.369 us | 2.180 us | 2.958 us | 1.55x |
| `structural/ordered-identifier-window` | 15.910 us | 10.429 us | 21.332 us | 1.53x |
| `structural/keyword-family-to-capitalized-identifier` | 11.384 us | 7.797 us | 18.509 us | 1.46x |
| `lokad/lexer/operator-run` | 2.918 us | 2.000 us | 2.625 us | 1.46x |
| `lokad/lexer/identifier` | 23.780 us | 16.385 us | 20.222 us | 1.45x |
| `structural/modifier-family-to-type-window` | 80.665 us | 56.860 us | 68.943 us | 1.42x |
| `structural/ordered-keyword-pair` | 5.620 us | 4.150 us | 14.998 us | 1.35x |
| `fallback/lookahead` | 5.676 us | 4.228 us | 14.923 us | 1.34x |
| `lokad/langserv/identifier-validator` | 11.329 us | 8.981 us | 11.093 us | 1.26x |
| `lokad/docs/inline-doc-prefix` | 3.428 us | 2.749 us | 3.435 us | 1.25x |
| `lokad/style/hex-color` | 0.609 us | 0.520 us | 0.597 us | 1.17x |
| `lokad/lexer/number` | 8.341 us | 7.542 us | 8.357 us | 1.11x |
| `lokad/langserv/color-rgb-hex` | 2.036 us | 1.874 us | 2.051 us | 1.09x |
| `literal/identifier-token-casei` | 5.904 us | 5.914 us | 17.206 us | 1.00x |
| `literal-family/type-token-family` | 10.528 us | 11.462 us | 22.417 us | 0.92x |
| `lokad/style/cell-ref` | 1.499 us | 1.792 us | 1.957 us | 0.84x |
| `structural/method-family-call` | 3.906 us | 5.081 us | 15.711 us | 0.77x |
| `structural/method-family-generic-call` | 3.450 us | 4.526 us | 15.267 us | 0.76x |
| `literal-family/method-token-family` | 3.876 us | 5.336 us | 15.787 us | 0.73x |
| `literal/call-token` | 2.626 us | 4.003 us | 14.871 us | 0.66x |
| `structural/ordered-modifier-pair` | 5.471 us | 8.612 us | 19.569 us | 0.64x |
| `structural/ordered-keyword-window` | 5.499 us | 8.694 us | 19.561 us | 0.63x |
| `lokad/style/range-ref` | 0.927 us | 1.487 us | 1.565 us | 0.62x |
| `literal/identifier-token` | 2.793 us | 4.743 us | 15.369 us | 0.59x |
| `lokad/lexer/string` | 23.067 us | 46.301 us | 50.011 us | 0.50x |
| `structural/modifier-family-to-type-pair` | 5.005 us | 10.616 us | 21.867 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.565 us | 7.515 us | 8.042 us | 0.34x |
| `lokad/langserv/url-download` | 2.779 us | 12.996 us | 13.571 us | 0.21x |
| `lokad/folding/region-marker` | 6.006 us | 36.358 us | 60.671 us | 0.17x |
| `balancing/begin-end-blocks` | 5.782 us | 472.328 us | 496.447 us | 0.01x |
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
