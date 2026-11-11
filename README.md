# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes 0.5.0 development source. Published 0.4.0 and frozen
development candidates retain the scope recorded in their release evidence.

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
The default Validate policy checks this; AssumeValid makes it a caller guarantee.
Byte coordinates are primary; UTF-16 coordinates and strings are derived
conveniences. Keep borrowed input unchanged and its owner alive while a view or
cursor uses it. Owned snapshots remain valid after cursor advance or disposal.

## Subject validation policy

Choose once when constructing an engine or through its factory:

```csharp
var regex = new Utf8Regex("needle", inputValidation: Utf8InputValidation.AssumeValid);
// regex.InputValidation is read-only.
```

`Validate` (default) checks the whole subject at most once per operation or cursor
lifetime. `AssumeValid` omits that pass when the caller guarantees valid UTF-8 and
unchanged borrowed bytes. All four packages inherit the choice through matching,
counting, output, splitting and cursors where supported. Pattern/replacement
validation, checked memory access, scalar semantics and cooperative cancellation
remain. Coordinate/result work may still scan input. Read the
[contract](docs/SPEC.md#subject-input-validation) before choosing AssumeValid.
Representative [policy measurements](bench/Experiments/InputValidation-20261110/README.md)
report both input contracts separately, including finite-limit and output costs.

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
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The tables were refreshed on **11 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m17s**. The [run receipt](bench/Experiments/DeliveryDev73-20261111/full-refresh.json)
records the scope and timings; package and consumer qualification are separate.
Each row retains its recorded implementation. Selective updates do not imply a
new complete run; the full-run receipt applies to its recorded revision.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

Focused Core counts accept a final validation policy, for example
`--measure-readme-public-case industry/mariomka-email-count 1 9 AssumeValid`.
These measurements label their input contract and leave recorded tables unchanged.

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
| `literal/sherlock-zh` | 80.129 us | 18.623 us | 2,195.109 us | 4.30x |
| `literal/sherlock-ru` | 167.910 us | 96.216 us | 5,253.733 us | 1.75x |
| `literal-alternate/sherlock-zh` | 100.575 us | 67.006 us | 2,196.180 us | 1.50x |
| `literal/sherlock-casei-en` | 153.168 us | 185.453 us | 610.851 us | 0.83x |
| `literal/sherlock-en` | 55.671 us | 82.506 us | 469.601 us | 0.67x |
| `literal/sherlock-casei-ru` | 227.372 us | 355.793 us | 5,466.350 us | 0.64x |
| `ruff-noqa/tweaked` | 4,544.176 us | 7,649.827 us | 41,439.750 us | 0.59x |
| `bounded-repeat/letters-en` | 1,130.788 us | 2,752.986 us | 2,919.119 us | 0.41x |
| `aws-keys/quick` | 3,684.524 us | 9,629.812 us | 46,153.700 us | 0.38x |
| `bounded-repeat/letters-ru` | 2,020.559 us | 5,788.507 us | 6,573.217 us | 0.35x |
| `dictionary/single` | 8,201.140 us | 25,634.733 us | 25,731.900 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,499.898 us | 4,746.006 us | 10,051.400 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 182.084 us | 581.057 us | 1,010.900 us | 0.31x |
| `literal-alternate/sherlock-en` | 365.253 us | 1,508.594 us | 2,046.068 us | 0.24x |
| `literal-alternate/sherlock-en-nomatch` | 150.717 us | 649.093 us | 1,065.204 us | 0.23x |
| `bounded-repeat/capitals` | 9,225.122 us | 45,597.650 us | 48,559.400 us | 0.20x |
| `bounded-repeat/context` | 22,384.125 us | 144,924.700 us | 149,261.400 us | 0.15x |
| `aws-keys/full` | 10,831.962 us | 85,501.500 us | 114,679.200 us | 0.13x |
| `ruff-noqa/real` | 4,492.435 us | 69,567.050 us | 99,225.500 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 944.221 us | 22,886.775 us | 28,010.550 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 129.769 us | 11,424.486 us | 12,505.443 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,175.654 us | 495.776 us | 8,793.225 us | 2.37x |
| `common/match-word` | `Match` | 0.185 us | 0.164 us | 1.081 us | 1.13x |
| `industry/leipzig-twain-count` | `Count` | 1,194.913 us | 1,332.253 us | 8,660.567 us | 0.90x |
| `industry/mariomka-uri-count` | `Count` | 1,422.274 us | 1,847.538 us | 10,368.133 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.080 us | 0.110 us | 0.144 us | 0.73x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.198 us | 0.217 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.085 us | 0.101 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.169 us | 0.187 us | 0.53x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 118.413 us | 262.273 us | 523.868 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.169 us | 0.189 us | 0.45x |
| `common/uri-match` | `IsMatch` | 0.095 us | 0.226 us | 0.263 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.185 us | 0.203 us | 0.42x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.267 us | 0.293 us | 0.36x |
| `common/replace-words` | `Replace` | 26.930 us | 77.210 us | 80.448 us | 0.35x |
| `common/matches-word` | `Count` | 1.320 us | 4.099 us | 5.772 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.346 us | 0.370 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,300.579 us | 8,299.156 us | 16,953.767 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.109 us | 0.138 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.100 us | 0.20x |
| `common/split-words` | `Split` | 14.352 us | 77.588 us | 79.384 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 174.630 us | 968.872 us | 1,241.448 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.551 us | 0.577 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,615.319 us | 9,850.275 us | 17,081.000 us | 0.16x |
| `common/matches-words` | `Count` | 9.087 us | 69.457 us | 70.640 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.076 us | 0.090 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.604 us | 0.636 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.824 us | 0.866 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,814.188 us | 19,480.875 us | 20,101.525 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,872.095 us | 22,840.650 us | 30,226.950 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,167.003 us | 32,345.367 us | 39,714.800 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 511.872 us | 8,376.367 us | 8,882.267 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.042 us | 161.793 us | 163.437 us | 0.04x |
| `common/matches-set` | `Count` | 6.960 us | 193.471 us | 193.919 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.726 us | 11,842.400 us | 12,355.671 us | 0.02x |
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
| `literal/sherlock-zh` | 79.753 us | 23.577 us | 2,178.774 us | 3.38x |
| `literal-alternate/sherlock-zh` | 99.783 us | 34.983 us | 2,140.811 us | 2.85x |
| `literal/sherlock-casei-en` | 153.233 us | 77.053 us | 414.577 us | 1.99x |
| `literal/sherlock-ru` | 168.521 us | 88.347 us | 5,157.840 us | 1.91x |
| `literal/sherlock-casei-ru` | 229.043 us | 191.169 us | 5,201.464 us | 1.20x |
| `literal-alternate/sherlock-en` | 367.274 us | 338.303 us | 807.493 us | 1.09x |
| `literal-alternate/sherlock-en-mixed` | 181.845 us | 172.639 us | 580.591 us | 1.05x |
| `aws-keys/quick` | 3,711.281 us | 3,589.441 us | 37,496.900 us | 1.03x |
| `literal-alternate/sherlock-casei-en` | 129.738 us | 127.090 us | 577.662 us | 1.02x |
| `ruff-noqa/tweaked` | 4,597.924 us | 4,568.235 us | 36,023.400 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,032.253 us | 2,023.524 us | 2,855.935 us | 1.00x |
| `literal/sherlock-en` | 55.325 us | 72.871 us | 425.980 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 150.135 us | 199.835 us | 551.370 us | 0.75x |
| `bounded-repeat/capitals` | 9,237.200 us | 13,160.550 us | 16,012.800 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,499.987 us | 2,305.852 us | 7,372.450 us | 0.65x |
| `bounded-repeat/letters-en` | 1,120.718 us | 1,784.307 us | 1,909.149 us | 0.63x |
| `aws-keys/full` | 10,901.250 us | 18,225.340 us | 49,575.067 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 937.141 us | 1,929.495 us | 6,996.625 us | 0.49x |
| `bounded-repeat/context` | 21,564.950 us | 68,556.550 us | 71,654.600 us | 0.31x |
| `ruff-noqa/real` | 4,502.041 us | 20,762.975 us | 52,372.967 us | 0.22x |
| `dictionary/single` | 8,206.911 us | 45,640.000 us | 45,210.750 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,169.228 us | 463.851 us | 7,580.050 us | 2.52x |
| `common/match-word` | `Match` | 0.186 us | 0.075 us | 0.977 us | 2.49x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.043 us | 0.073 us | 2.03x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 118.454 us | 59.968 us | 282.713 us | 1.98x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.065 us | 0.086 us | 1.84x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.055 us | 0.031 us | 0.047 us | 1.80x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.056 us | 0.031 us | 0.049 us | 1.78x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.086 us | 1.63x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.090 us | 0.055 us | 0.077 us | 1.62x |
| `common/replace-words` | `Replace` | 27.197 us | 16.992 us | 19.848 us | 1.60x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.047 us | 0.067 us | 1.53x |
| `industry/leipzig-symbol-count` | `Count` | 1,875.893 us | 1,249.811 us | 7,073.000 us | 1.50x |
| `common/ip-miss` | `IsMatch` | 0.072 us | 0.048 us | 0.070 us | 1.48x |
| `industry/leipzig-river-window-count` | `Count` | 2,220.326 us | 1,520.858 us | 7,869.060 us | 1.46x |
| `industry/mariomka-uri-count` | `Count` | 1,407.919 us | 1,100.704 us | 8,345.633 us | 1.28x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.080 us | 0.106 us | 1.20x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.090 us | 0.115 us | 1.09x |
| `common/uri-match` | `IsMatch` | 0.090 us | 0.088 us | 0.120 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,197.906 us | 1,307.266 us | 7,590.220 us | 0.92x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.089 us | 0.127 us | 0.83x |
| `common/split-words` | `Split` | 14.307 us | 20.375 us | 22.281 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.072 us | 0.67x |
| `industry/mariomka-ip-count` | `Count` | 2,299.755 us | 3,536.995 us | 10,891.033 us | 0.65x |
| `common/matches-words` | `Count` | 9.128 us | 14.803 us | 17.239 us | 0.62x |
| `common/matches-word` | `Count` | 1.310 us | 2.275 us | 3.370 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 1,615.196 us | 3,065.328 us | 8,380.250 us | 0.53x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.196 us | 0.233 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.037 us | 0.054 us | 0.29x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.165 us | 617.398 us | 866.385 us | 0.28x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,805.226 us | 8,687.067 us | 9,282.288 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 512.510 us | 4,026.163 us | 4,370.139 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.045 us | 103.643 us | 105.081 us | 0.07x |
| `common/matches-set` | `Count` | 6.945 us | 103.162 us | 103.749 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 237.027 us | 4,809.944 us | 5,220.987 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.182 us | 6.367 us | 31.180 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.225 us | 4.125 us | 18.497 us | 1.27x |
| `lokad/langserv/color-short-hex` | 1.653 us | 1.371 us | 1.495 us | 1.21x |
| `lokad/lexer/doc-line` | 3.246 us | 3.052 us | 3.711 us | 1.06x |
| `lokad/lexer/identifier` | 23.245 us | 22.881 us | 25.752 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.599 us | 5.527 us | 19.907 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.241 us | 7.321 us | 7.717 us | 0.99x |
| `fallback/lookahead` | 5.490 us | 6.182 us | 19.934 us | 0.89x |
| `lokad/langserv/identifier-validator` | 11.158 us | 12.581 us | 13.686 us | 0.89x |
| `lokad/style/hex-color` | 0.709 us | 0.815 us | 0.848 us | 0.87x |
| `lokad/lexer/operator-run` | 2.788 us | 3.476 us | 3.886 us | 0.80x |
| `literal-family/method-token-family` | 7.198 us | 9.197 us | 22.126 us | 0.78x |
| `lokad/langserv/color-rgb-hex` | 1.997 us | 2.885 us | 2.875 us | 0.69x |
| `lokad/docs/inline-doc-prefix` | 3.268 us | 4.882 us | 5.459 us | 0.67x |
| `literal/identifier-token` | 2.787 us | 4.412 us | 17.836 us | 0.63x |
| `literal/call-token` | 2.569 us | 4.291 us | 17.936 us | 0.60x |
| `lokad/lexer/number` | 8.003 us | 14.459 us | 15.387 us | 0.55x |
| `structural/method-family-generic-call` | 3.476 us | 6.776 us | 20.638 us | 0.51x |
| `structural/ordered-identifier-window` | 14.671 us | 28.606 us | 42.209 us | 0.51x |
| `structural/method-family-call` | 3.855 us | 8.021 us | 21.138 us | 0.48x |
| `lokad/style/cell-ref` | 1.387 us | 2.971 us | 2.947 us | 0.47x |
| `literal-family/type-token-family` | 10.528 us | 23.795 us | 38.202 us | 0.44x |
| `literal/identifier-token-casei` | 5.974 us | 15.379 us | 29.540 us | 0.39x |
| `lokad/style/range-ref` | 0.857 us | 2.727 us | 2.659 us | 0.31x |
| `structural/ordered-keyword-window` | 5.479 us | 17.637 us | 31.584 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.193 us | 17.320 us | 30.934 us | 0.30x |
| `structural/modifier-family-to-type-window` | 70.406 us | 254.708 us | 270.692 us | 0.28x |
| `lokad/lexer/string` | 21.473 us | 99.076 us | 104.225 us | 0.22x |
| `lokad/folding/region-marker` | 6.092 us | 35.477 us | 61.483 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.391 us | 23.964 us | 24.445 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.549 us | 220.575 us | 235.160 us | 0.05x |
| `lokad/langserv/url-download` | 2.394 us | 52.077 us | 51.678 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.974 us | 131.003 us | 147.258 us | 0.04x |
| `balancing/begin-end-blocks` | 5.772 us | 4,354.178 us | 4,364.861 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.135 us | 4.424 us | 28.623 us | 2.07x |
| `lokad/langserv/color-short-hex` | 1.648 us | 0.976 us | 1.156 us | 1.69x |
| `lokad/langserv/helper-identifier` | 7.261 us | 4.677 us | 5.776 us | 1.55x |
| `structural/keyword-to-generic-type` | 5.208 us | 3.475 us | 17.367 us | 1.50x |
| `structural/keyword-family-to-capitalized-identifier` | 11.463 us | 8.041 us | 21.287 us | 1.43x |
| `lokad/style/hex-color` | 0.714 us | 0.502 us | 0.581 us | 1.42x |
| `lokad/lexer/identifier` | 23.168 us | 16.703 us | 20.095 us | 1.39x |
| `lokad/lexer/doc-line` | 3.294 us | 2.381 us | 3.056 us | 1.38x |
| `lokad/lexer/operator-run` | 2.797 us | 2.024 us | 2.569 us | 1.38x |
| `structural/ordered-identifier-window` | 14.667 us | 10.680 us | 24.323 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.633 us | 4.203 us | 17.892 us | 1.34x |
| `fallback/lookahead` | 5.483 us | 4.228 us | 17.436 us | 1.30x |
| `structural/modifier-family-to-type-window` | 70.355 us | 56.104 us | 71.428 us | 1.25x |
| `lokad/langserv/identifier-validator` | 10.888 us | 8.965 us | 11.020 us | 1.21x |
| `lokad/docs/inline-doc-prefix` | 3.285 us | 2.769 us | 3.555 us | 1.19x |
| `lokad/langserv/color-rgb-hex` | 1.953 us | 1.771 us | 1.968 us | 1.10x |
| `lokad/lexer/number` | 7.979 us | 7.577 us | 8.519 us | 1.05x |
| `literal/identifier-token-casei` | 5.967 us | 5.980 us | 19.936 us | 1.00x |
| `literal-family/type-token-family` | 10.547 us | 11.537 us | 25.241 us | 0.91x |
| `lokad/style/cell-ref` | 1.381 us | 1.726 us | 1.842 us | 0.80x |
| `structural/method-family-call` | 3.867 us | 5.054 us | 17.800 us | 0.77x |
| `structural/method-family-generic-call` | 3.488 us | 4.570 us | 17.833 us | 0.76x |
| `literal-family/method-token-family` | 3.902 us | 5.341 us | 18.042 us | 0.73x |
| `literal/call-token` | 2.559 us | 3.964 us | 16.854 us | 0.65x |
| `structural/ordered-keyword-window` | 5.508 us | 8.572 us | 22.124 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.173 us | 8.479 us | 21.989 us | 0.61x |
| `lokad/style/range-ref` | 0.860 us | 1.431 us | 1.550 us | 0.60x |
| `literal/identifier-token` | 2.792 us | 4.735 us | 17.259 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 4.988 us | 10.576 us | 24.293 us | 0.47x |
| `lokad/lexer/string` | 21.508 us | 47.156 us | 50.783 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.365 us | 7.392 us | 8.023 us | 0.32x |
| `lokad/langserv/url-download` | 2.390 us | 12.648 us | 13.122 us | 0.19x |
| `lokad/folding/region-marker` | 6.086 us | 36.119 us | 62.660 us | 0.17x |
| `balancing/begin-end-blocks` | 5.749 us | 466.754 us | 495.326 us | 0.01x |
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
