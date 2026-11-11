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
uses the release-candidate SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The tables were refreshed on **11 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **47m05s**. The [run receipt](bench/Experiments/DeliveryDev70-20261111/full-refresh.json)
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
| `literal/sherlock-zh` | 82.697 us | 18.888 us | 2,269.182 us | 4.38x |
| `literal/sherlock-ru` | 169.074 us | 100.284 us | 5,489.557 us | 1.69x |
| `literal-alternate/sherlock-zh` | 104.931 us | 68.665 us | 2,193.768 us | 1.53x |
| `literal/sherlock-casei-en` | 157.691 us | 192.103 us | 573.326 us | 0.82x |
| `literal/sherlock-en` | 56.941 us | 86.024 us | 444.250 us | 0.66x |
| `literal/sherlock-casei-ru` | 238.719 us | 386.838 us | 5,523.971 us | 0.62x |
| `ruff-noqa/tweaked` | 4,611.671 us | 7,687.855 us | 42,018.300 us | 0.60x |
| `bounded-repeat/letters-en` | 1,111.065 us | 2,839.585 us | 2,940.015 us | 0.39x |
| `aws-keys/quick` | 3,822.362 us | 9,778.237 us | 44,962.400 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,073.527 us | 6,008.308 us | 6,809.467 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 197.183 us | 599.571 us | 1,035.384 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,546.839 us | 4,827.919 us | 10,297.000 us | 0.32x |
| `dictionary/single` | 8,312.400 us | 26,047.200 us | 26,081.033 us | 0.32x |
| `literal-alternate/sherlock-en` | 368.052 us | 1,531.745 us | 2,130.197 us | 0.24x |
| `literal-alternate/sherlock-en-nomatch` | 154.652 us | 661.296 us | 1,090.085 us | 0.23x |
| `bounded-repeat/capitals` | 9,540.650 us | 46,758.200 us | 50,261.850 us | 0.20x |
| `bounded-repeat/context` | 21,736.825 us | 148,190.200 us | 151,411.400 us | 0.15x |
| `aws-keys/full` | 8,989.129 us | 87,663.300 us | 117,701.300 us | 0.10x |
| `ruff-noqa/real` | 4,473.253 us | 71,609.050 us | 88,831.400 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 969.735 us | 23,326.975 us | 28,497.800 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 127.348 us | 11,711.986 us | 12,503.217 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,197.468 us | 505.951 us | 9,024.375 us | 2.37x |
| `common/match-word` | `Match` | 0.194 us | 0.170 us | 1.221 us | 1.14x |
| `industry/leipzig-twain-count` | `Count` | 1,176.885 us | 1,332.505 us | 8,690.120 us | 0.88x |
| `industry/mariomka-uri-count` | `Count` | 1,455.734 us | 1,899.561 us | 10,323.800 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.110 us | 0.148 us | 0.75x |
| `common/uri-miss` | `IsMatch` | 0.124 us | 0.191 us | 0.212 us | 0.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.090 us | 0.167 us | 0.185 us | 0.54x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 119.326 us | 266.489 us | 513.191 us | 0.45x |
| `common/ip-match` | `IsMatch` | 0.078 us | 0.176 us | 0.197 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.079 us | 0.186 us | 0.207 us | 0.43x |
| `common/uri-match` | `IsMatch` | 0.099 us | 0.242 us | 0.279 us | 0.41x |
| `common/replace-words` | `Replace` | 28.163 us | 78.326 us | 80.098 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.269 us | 0.295 us | 0.35x |
| `common/matches-word` | `Count` | 1.364 us | 4.388 us | 6.144 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.356 us | 0.384 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,286.812 us | 8,337.289 us | 16,966.867 us | 0.27x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.110 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.084 us | 0.099 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.502 us | 966.735 us | 1,249.549 us | 0.18x |
| `common/split-words` | `Split` | 14.616 us | 80.737 us | 82.140 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.558 us | 0.588 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,667.962 us | 9,755.212 us | 17,066.967 us | 0.17x |
| `common/matches-words` | `Count` | 9.327 us | 70.522 us | 73.342 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.077 us | 0.624 us | 0.656 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.832 us | 0.872 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,938.821 us | 19,843.350 us | 20,373.625 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,901.400 us | 22,949.575 us | 30,570.200 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,208.709 us | 32,478.433 us | 40,034.800 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 485.286 us | 8,569.522 us | 9,085.656 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.768 us | 165.990 us | 168.029 us | 0.05x |
| `common/matches-set` | `Count` | 7.840 us | 196.949 us | 197.902 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 243.320 us | 11,810.786 us | 12,349.414 us | 0.02x |
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
| `literal/sherlock-zh` | 82.988 us | 24.086 us | 2,249.288 us | 3.45x |
| `literal-alternate/sherlock-zh` | 104.950 us | 36.311 us | 2,142.619 us | 2.89x |
| `literal/sherlock-casei-en` | 157.396 us | 78.784 us | 449.420 us | 2.00x |
| `literal/sherlock-ru` | 169.015 us | 91.108 us | 5,441.129 us | 1.86x |
| `literal/sherlock-casei-ru` | 237.782 us | 196.303 us | 5,367.960 us | 1.21x |
| `literal-alternate/sherlock-en-mixed` | 198.426 us | 176.611 us | 533.019 us | 1.12x |
| `literal-alternate/sherlock-en` | 367.638 us | 348.714 us | 761.996 us | 1.05x |
| `aws-keys/quick` | 3,839.860 us | 3,692.933 us | 36,735.100 us | 1.04x |
| `ruff-noqa/tweaked` | 4,627.594 us | 4,589.235 us | 36,485.700 us | 1.01x |
| `bounded-repeat/letters-ru` | 2,065.376 us | 2,067.359 us | 2,892.831 us | 1.00x |
| `literal-alternate/sherlock-casei-en` | 127.522 us | 148.173 us | 536.669 us | 0.86x |
| `literal/sherlock-en` | 56.814 us | 75.855 us | 416.170 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 154.592 us | 211.461 us | 576.430 us | 0.73x |
| `bounded-repeat/capitals` | 9,542.450 us | 13,575.900 us | 16,499.675 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,548.769 us | 2,359.175 us | 7,587.950 us | 0.66x |
| `bounded-repeat/letters-en` | 1,110.312 us | 1,845.860 us | 1,935.890 us | 0.60x |
| `aws-keys/full` | 9,847.429 us | 18,639.560 us | 50,678.800 us | 0.53x |
| `literal-alternate/sherlock-casei-ru` | 976.583 us | 1,972.785 us | 7,255.775 us | 0.50x |
| `bounded-repeat/context` | 21,816.675 us | 69,724.200 us | 73,096.800 us | 0.31x |
| `ruff-noqa/real` | 4,581.165 us | 21,087.950 us | 35,722.900 us | 0.22x |
| `dictionary/single` | 8,309.430 us | 46,118.700 us | 46,151.900 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,198.175 us | 472.879 us | 7,736.375 us | 2.53x |
| `common/match-word` | `Match` | 0.194 us | 0.083 us | 1.020 us | 2.35x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 119.410 us | 58.633 us | 265.101 us | 2.04x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.044 us | 0.074 us | 2.01x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.056 us | 0.030 us | 0.047 us | 1.85x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.067 us | 0.086 us | 1.82x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.031 us | 0.052 us | 1.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.090 us | 1.61x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.090 us | 0.056 us | 0.077 us | 1.59x |
| `common/replace-words` | `Replace` | 28.094 us | 17.816 us | 19.267 us | 1.58x |
| `industry/leipzig-symbol-count` | `Count` | 1,903.263 us | 1,252.138 us | 7,456.300 us | 1.52x |
| `common/ip-match` | `IsMatch` | 0.073 us | 0.049 us | 0.069 us | 1.50x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.050 us | 0.070 us | 1.48x |
| `industry/leipzig-river-window-count` | `Count` | 2,218.932 us | 1,567.480 us | 8,049.820 us | 1.42x |
| `industry/mariomka-uri-count` | `Count` | 1,460.800 us | 1,124.794 us | 8,359.600 us | 1.30x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.081 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.101 us | 0.090 us | 0.116 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.089 us | 0.121 us | 1.07x |
| `industry/leipzig-twain-count` | `Count` | 1,176.931 us | 1,304.033 us | 7,610.711 us | 0.90x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.092 us | 0.129 us | 0.83x |
| `common/split-words` | `Split` | 14.642 us | 21.042 us | 23.210 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.042 us | 0.073 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,291.418 us | 3,511.150 us | 10,934.833 us | 0.65x |
| `common/matches-words` | `Count` | 9.327 us | 14.787 us | 17.223 us | 0.63x |
| `common/matches-word` | `Count` | 1.359 us | 2.383 us | 3.638 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,694.196 us | 3,032.140 us | 8,522.175 us | 0.56x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.198 us | 0.236 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.037 us | 0.055 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 177.458 us | 602.517 us | 862.004 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,935.080 us | 8,773.467 us | 9,320.038 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 482.082 us | 4,016.063 us | 4,370.578 us | 0.12x |
| `common/matches-set` | `Count` | 7.849 us | 105.706 us | 107.605 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.781 us | 105.103 us | 107.130 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 242.161 us | 4,812.519 us | 5,208.933 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.458 us | 6.430 us | 31.358 us | 1.47x |
| `structural/keyword-to-generic-type` | 5.526 us | 4.184 us | 18.497 us | 1.32x |
| `lokad/langserv/color-short-hex` | 1.647 us | 1.341 us | 1.465 us | 1.23x |
| `lokad/lexer/doc-line` | 3.315 us | 3.060 us | 3.702 us | 1.08x |
| `structural/ordered-keyword-pair` | 5.541 us | 5.347 us | 19.289 us | 1.04x |
| `lokad/lexer/identifier` | 23.899 us | 23.474 us | 25.418 us | 1.02x |
| `lokad/langserv/helper-identifier` | 6.854 us | 7.408 us | 7.846 us | 0.93x |
| `fallback/lookahead` | 5.518 us | 6.085 us | 19.601 us | 0.91x |
| `lokad/langserv/identifier-validator` | 11.294 us | 12.722 us | 14.337 us | 0.89x |
| `lokad/lexer/operator-run` | 2.855 us | 3.504 us | 3.915 us | 0.81x |
| `literal-family/method-token-family` | 7.133 us | 9.313 us | 22.244 us | 0.77x |
| `lokad/style/hex-color` | 0.573 us | 0.812 us | 0.886 us | 0.71x |
| `lokad/docs/inline-doc-prefix` | 3.340 us | 4.835 us | 5.483 us | 0.69x |
| `lokad/langserv/color-rgb-hex` | 2.005 us | 3.015 us | 3.100 us | 0.67x |
| `literal/identifier-token` | 2.797 us | 4.420 us | 17.770 us | 0.63x |
| `literal/call-token` | 2.619 us | 4.430 us | 17.893 us | 0.59x |
| `lokad/lexer/number` | 8.101 us | 14.492 us | 15.392 us | 0.56x |
| `structural/ordered-identifier-window` | 14.854 us | 28.864 us | 42.392 us | 0.51x |
| `structural/method-family-generic-call` | 3.456 us | 6.725 us | 20.366 us | 0.51x |
| `lokad/style/cell-ref` | 1.456 us | 3.016 us | 3.118 us | 0.48x |
| `structural/method-family-call` | 3.893 us | 8.316 us | 21.593 us | 0.47x |
| `literal-family/type-token-family` | 10.565 us | 24.098 us | 38.398 us | 0.44x |
| `literal/identifier-token-casei` | 6.204 us | 15.460 us | 29.225 us | 0.40x |
| `lokad/style/range-ref` | 0.907 us | 2.778 us | 2.838 us | 0.33x |
| `structural/ordered-keyword-window` | 5.514 us | 18.012 us | 31.768 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.295 us | 17.596 us | 30.986 us | 0.30x |
| `structural/modifier-family-to-type-window` | 72.287 us | 256.662 us | 272.221 us | 0.28x |
| `lokad/lexer/string` | 21.665 us | 99.786 us | 104.570 us | 0.22x |
| `lokad/folding/region-marker` | 6.108 us | 33.671 us | 63.189 us | 0.18x |
| `lokad/langserv/url-dashboard` | 2.407 us | 24.840 us | 25.101 us | 0.10x |
| `lokad/langserv/url-download` | 2.682 us | 50.649 us | 51.088 us | 0.05x |
| `structural/keyword-family-to-capitalized-identifier` | 11.452 us | 218.713 us | 232.949 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.098 us | 140.469 us | 154.661 us | 0.04x |
| `balancing/begin-end-blocks` | 5.737 us | 4,389.333 us | 4,399.778 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.440 us | 4.478 us | 28.716 us | 2.11x |
| `lokad/langserv/color-short-hex` | 1.638 us | 0.940 us | 1.141 us | 1.74x |
| `structural/keyword-to-generic-type` | 5.507 us | 3.476 us | 17.292 us | 1.58x |
| `lokad/lexer/doc-line` | 3.289 us | 2.240 us | 2.962 us | 1.47x |
| `lokad/lexer/identifier` | 23.825 us | 16.430 us | 20.420 us | 1.45x |
| `structural/keyword-family-to-capitalized-identifier` | 11.453 us | 7.920 us | 20.992 us | 1.45x |
| `structural/ordered-identifier-window` | 14.871 us | 10.630 us | 24.050 us | 1.40x |
| `lokad/langserv/helper-identifier` | 6.912 us | 4.945 us | 5.620 us | 1.40x |
| `lokad/lexer/operator-run` | 2.853 us | 2.072 us | 2.621 us | 1.38x |
| `structural/ordered-keyword-pair` | 5.550 us | 4.174 us | 17.437 us | 1.33x |
| `fallback/lookahead` | 5.515 us | 4.252 us | 17.319 us | 1.30x |
| `structural/modifier-family-to-type-window` | 72.291 us | 56.333 us | 72.071 us | 1.28x |
| `lokad/langserv/identifier-validator` | 11.145 us | 9.329 us | 11.401 us | 1.19x |
| `lokad/docs/inline-doc-prefix` | 3.329 us | 2.819 us | 3.613 us | 1.18x |
| `lokad/lexer/number` | 8.116 us | 7.405 us | 8.709 us | 1.10x |
| `lokad/style/hex-color` | 0.568 us | 0.518 us | 0.604 us | 1.09x |
| `lokad/langserv/color-rgb-hex` | 2.015 us | 1.912 us | 2.136 us | 1.05x |
| `literal/identifier-token-casei` | 6.198 us | 5.977 us | 19.793 us | 1.04x |
| `literal-family/type-token-family` | 10.582 us | 11.577 us | 25.256 us | 0.91x |
| `lokad/style/cell-ref` | 1.447 us | 1.871 us | 2.080 us | 0.77x |
| `structural/method-family-generic-call` | 3.456 us | 4.520 us | 17.484 us | 0.76x |
| `structural/method-family-call` | 3.883 us | 5.096 us | 17.930 us | 0.76x |
| `literal-family/method-token-family` | 3.885 us | 5.337 us | 18.044 us | 0.73x |
| `literal/call-token` | 2.606 us | 3.985 us | 16.771 us | 0.65x |
| `structural/ordered-keyword-window` | 5.514 us | 8.695 us | 22.248 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.295 us | 8.685 us | 21.967 us | 0.61x |
| `literal/identifier-token` | 2.795 us | 4.768 us | 17.431 us | 0.59x |
| `lokad/style/range-ref` | 0.892 us | 1.598 us | 1.650 us | 0.56x |
| `structural/modifier-family-to-type-pair` | 5.103 us | 10.925 us | 25.009 us | 0.47x |
| `lokad/lexer/string` | 21.810 us | 47.041 us | 49.666 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.385 us | 7.544 us | 8.135 us | 0.32x |
| `lokad/langserv/url-download` | 2.621 us | 13.100 us | 13.628 us | 0.20x |
| `lokad/folding/region-marker` | 6.089 us | 33.569 us | 62.727 us | 0.18x |
| `balancing/begin-end-blocks` | 5.739 us | 470.762 us | 502.138 us | 0.01x |
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
