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

The tables were refreshed on **10 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks passed in **46m43s**. The [run receipt](bench/Experiments/DeliveryDev66-20261110/full-refresh.json)
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
| `literal/sherlock-zh` | 83.019 us | 18.981 us | 2,185.874 us | 4.37x |
| `literal/sherlock-ru` | 167.612 us | 97.898 us | 5,480.800 us | 1.71x |
| `literal-alternate/sherlock-zh` | 104.992 us | 69.673 us | 2,211.132 us | 1.51x |
| `literal/sherlock-casei-en` | 155.545 us | 198.012 us | 566.823 us | 0.79x |
| `literal/sherlock-en` | 58.088 us | 87.725 us | 443.438 us | 0.66x |
| `literal/sherlock-casei-ru` | 242.099 us | 393.001 us | 5,660.569 us | 0.62x |
| `ruff-noqa/tweaked` | 4,370.417 us | 7,668.364 us | 43,036.250 us | 0.57x |
| `aws-keys/quick` | 5,060.947 us | 9,510.344 us | 45,646.750 us | 0.53x |
| `bounded-repeat/letters-en` | 1,128.765 us | 2,837.181 us | 2,950.912 us | 0.40x |
| `literal-alternate/sherlock-ru` | 1,635.111 us | 4,823.281 us | 9,890.775 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,983.079 us | 6,053.062 us | 7,075.327 us | 0.33x |
| `dictionary/single` | 8,255.280 us | 26,020.633 us | 26,025.933 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 185.214 us | 603.060 us | 1,014.290 us | 0.31x |
| `literal-alternate/sherlock-en` | 392.179 us | 1,572.465 us | 2,069.947 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 154.915 us | 674.577 us | 1,110.207 us | 0.23x |
| `bounded-repeat/capitals` | 9,449.075 us | 46,327.450 us | 53,450.750 us | 0.20x |
| `bounded-repeat/context` | 20,654.150 us | 149,905.400 us | 154,230.000 us | 0.14x |
| `aws-keys/full` | 10,769.014 us | 88,201.900 us | 121,217.600 us | 0.12x |
| `ruff-noqa/real` | 4,389.756 us | 70,557.950 us | 87,077.300 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 954.950 us | 23,694.250 us | 29,196.400 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 143.164 us | 11,295.457 us | 12,000.614 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,161.291 us | 490.026 us | 8,989.875 us | 2.37x |
| `common/match-word` | `Match` | 0.196 us | 0.169 us | 1.190 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,217.142 us | 1,362.195 us | 8,724.880 us | 0.89x |
| `industry/mariomka-uri-count` | `Count` | 1,461.869 us | 1,896.888 us | 10,498.925 us | 0.77x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.082 us | 0.110 us | 0.143 us | 0.74x |
| `common/uri-miss` | `IsMatch` | 0.119 us | 0.189 us | 0.207 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.085 us | 0.102 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.170 us | 0.185 us | 0.52x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 115.272 us | 265.931 us | 578.312 us | 0.43x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.174 us | 0.195 us | 0.42x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.232 us | 0.267 us | 0.41x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.186 us | 0.207 us | 0.40x |
| `industry/leipzig-name-family-count` | `Count` | 3,660.557 us | 9,450.570 us | 16,223.690 us | 0.39x |
| `common/replace-words` | `Replace` | 29.331 us | 78.158 us | 79.215 us | 0.38x |
| `common/email-match` | `IsMatch` | 0.098 us | 0.274 us | 0.299 us | 0.36x |
| `common/matches-word` | `Count` | 1.356 us | 4.289 us | 5.999 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.349 us | 0.371 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,293.858 us | 8,256.650 us | 16,796.133 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.109 us | 0.138 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.21x |
| `common/split-words` | `Split` | 14.632 us | 78.527 us | 80.147 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.177 us | 964.931 us | 1,237.952 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.097 us | 0.559 us | 0.595 us | 0.17x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.076 us | 0.091 us | 0.13x |
| `common/matches-words` | `Count` | 9.387 us | 70.533 us | 72.294 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.619 us | 0.656 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.090 us | 0.843 us | 0.882 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,986.724 us | 19,525.800 us | 20,064.675 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,838.629 us | 23,050.400 us | 30,463.200 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,146.580 us | 32,199.985 us | 39,303.120 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 508.345 us | 8,194.430 us | 8,670.156 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.956 us | 165.341 us | 166.412 us | 0.05x |
| `common/matches-set` | `Count` | 8.251 us | 195.625 us | 197.854 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 239.881 us | 11,919.014 us | 12,502.933 us | 0.02x |
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
| `literal/sherlock-zh` | 83.002 us | 24.134 us | 2,151.646 us | 3.44x |
| `literal-alternate/sherlock-zh` | 104.975 us | 36.282 us | 2,139.757 us | 2.89x |
| `literal/sherlock-casei-en` | 155.844 us | 79.484 us | 436.024 us | 1.96x |
| `literal/sherlock-ru` | 167.459 us | 89.234 us | 5,343.980 us | 1.88x |
| `aws-keys/quick` | 5,063.720 us | 3,692.981 us | 38,684.400 us | 1.37x |
| `literal-alternate/sherlock-en` | 388.890 us | 347.212 us | 771.551 us | 1.12x |
| `literal/sherlock-casei-ru` | 243.369 us | 222.270 us | 5,396.507 us | 1.09x |
| `literal-alternate/sherlock-en-mixed` | 185.702 us | 178.310 us | 531.497 us | 1.04x |
| `bounded-repeat/letters-ru` | 1,992.121 us | 2,074.932 us | 2,939.585 us | 0.96x |
| `ruff-noqa/tweaked` | 4,377.694 us | 4,577.553 us | 38,529.300 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 143.317 us | 150.110 us | 520.633 us | 0.95x |
| `literal-alternate/sherlock-en-nomatch` | 154.762 us | 201.128 us | 578.285 us | 0.77x |
| `literal/sherlock-en` | 58.245 us | 77.474 us | 418.748 us | 0.75x |
| `literal-alternate/sherlock-ru` | 1,693.938 us | 2,366.966 us | 7,497.075 us | 0.72x |
| `bounded-repeat/capitals` | 9,519.617 us | 13,668.367 us | 20,970.250 us | 0.70x |
| `bounded-repeat/letters-en` | 1,125.946 us | 1,848.310 us | 1,934.582 us | 0.61x |
| `aws-keys/full` | 10,811.986 us | 18,589.560 us | 51,125.133 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 949.951 us | 1,982.005 us | 7,263.225 us | 0.48x |
| `bounded-repeat/context` | 20,691.400 us | 69,601.550 us | 72,867.850 us | 0.30x |
| `ruff-noqa/real` | 4,388.056 us | 21,288.125 us | 48,707.033 us | 0.21x |
| `dictionary/single` | 8,266.240 us | 46,191.350 us | 46,264.400 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,161.738 us | 454.396 us | 7,801.950 us | 2.56x |
| `common/match-word` | `Match` | 0.197 us | 0.081 us | 1.022 us | 2.42x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.044 us | 0.075 us | 2.04x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 115.867 us | 58.690 us | 268.568 us | 1.97x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.067 us | 0.085 us | 1.81x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.050 us | 0.029 us | 0.048 us | 1.72x |
| `common/replace-words` | `Replace` | 29.286 us | 17.793 us | 18.967 us | 1.65x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.051 us | 0.031 us | 0.050 us | 1.64x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.079 us | 0.049 us | 0.084 us | 1.61x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.056 us | 0.076 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.078 us | 0.050 us | 0.070 us | 1.57x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.048 us | 0.069 us | 1.52x |
| `industry/leipzig-symbol-count` | `Count` | 1,840.222 us | 1,227.068 us | 7,461.560 us | 1.50x |
| `industry/leipzig-river-window-count` | `Count` | 2,166.800 us | 1,528.632 us | 8,343.095 us | 1.42x |
| `industry/mariomka-uri-count` | `Count` | 1,466.902 us | 1,107.203 us | 8,452.025 us | 1.32x |
| `industry/leipzig-name-family-count` | `Count` | 3,663.833 us | 2,913.285 us | 9,152.670 us | 1.26x |
| `common/email-match` | `IsMatch` | 0.098 us | 0.081 us | 0.108 us | 1.20x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.090 us | 0.115 us | 1.12x |
| `common/uri-match` | `IsMatch` | 0.095 us | 0.089 us | 0.121 us | 1.07x |
| `industry/leipzig-twain-count` | `Count` | 1,216.765 us | 1,333.367 us | 7,551.236 us | 0.91x |
| `common/matches-words` | `Count` | 9.378 us | 10.846 us | 14.007 us | 0.86x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.090 us | 0.130 us | 0.86x |
| `common/split-words` | `Split` | 14.651 us | 21.375 us | 23.129 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,303.509 us | 3,532.618 us | 10,935.867 us | 0.65x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.073 us | 0.65x |
| `common/matches-word` | `Count` | 1.355 us | 2.385 us | 3.615 us | 0.57x |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.198 us | 0.235 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.012 us | 0.036 us | 0.053 us | 0.34x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 176.059 us | 598.583 us | 855.799 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,986.924 us | 8,918.678 us | 9,453.812 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 509.837 us | 4,037.479 us | 4,381.056 us | 0.13x |
| `common/matches-set` | `Count` | 8.200 us | 106.911 us | 108.472 us | 0.08x |
| `common/matches-boundary` | `Count` | 7.929 us | 106.909 us | 108.729 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 238.161 us | 4,779.169 us | 5,216.840 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.221 us | 6.414 us | 29.001 us | 1.44x |
| `lokad/langserv/color-short-hex` | 1.685 us | 1.391 us | 1.499 us | 1.21x |
| `structural/keyword-to-generic-type` | 4.947 us | 4.102 us | 15.778 us | 1.21x |
| `lokad/lexer/doc-line` | 3.320 us | 3.081 us | 3.743 us | 1.08x |
| `lokad/lexer/identifier` | 24.568 us | 23.149 us | 25.671 us | 1.06x |
| `structural/ordered-keyword-pair` | 5.538 us | 5.440 us | 16.907 us | 1.02x |
| `lokad/langserv/helper-identifier` | 7.057 us | 7.412 us | 8.189 us | 0.95x |
| `lokad/lexer/operator-run` | 3.117 us | 3.471 us | 3.925 us | 0.90x |
| `fallback/lookahead` | 5.439 us | 6.090 us | 17.412 us | 0.89x |
| `lokad/langserv/identifier-validator` | 11.373 us | 12.818 us | 14.263 us | 0.89x |
| `literal-family/method-token-family` | 7.211 us | 9.343 us | 20.391 us | 0.77x |
| `lokad/style/hex-color` | 0.612 us | 0.843 us | 0.900 us | 0.73x |
| `lokad/docs/inline-doc-prefix` | 3.399 us | 4.842 us | 5.554 us | 0.70x |
| `lokad/langserv/color-rgb-hex` | 2.062 us | 2.966 us | 3.037 us | 0.70x |
| `literal/identifier-token` | 2.800 us | 4.432 us | 16.003 us | 0.63x |
| `literal/call-token` | 2.647 us | 4.332 us | 15.542 us | 0.61x |
| `lokad/lexer/number` | 8.180 us | 14.567 us | 15.709 us | 0.56x |
| `structural/ordered-identifier-window` | 15.604 us | 28.884 us | 39.978 us | 0.54x |
| `lokad/style/cell-ref` | 1.546 us | 2.934 us | 3.026 us | 0.53x |
| `structural/method-family-generic-call` | 3.469 us | 6.758 us | 17.976 us | 0.51x |
| `structural/method-family-call` | 3.931 us | 8.124 us | 18.859 us | 0.48x |
| `literal-family/type-token-family` | 10.589 us | 24.262 us | 35.336 us | 0.44x |
| `literal/identifier-token-casei` | 5.760 us | 15.852 us | 26.630 us | 0.36x |
| `lokad/style/range-ref` | 0.908 us | 2.751 us | 2.730 us | 0.33x |
| `structural/ordered-keyword-window` | 5.573 us | 18.156 us | 29.267 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.264 us | 17.400 us | 28.449 us | 0.30x |
| `structural/modifier-family-to-type-window` | 70.419 us | 257.126 us | 270.376 us | 0.27x |
| `lokad/lexer/string` | 22.057 us | 98.585 us | 103.697 us | 0.22x |
| `lokad/folding/region-marker` | 6.000 us | 36.580 us | 61.296 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.412 us | 24.563 us | 24.992 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.487 us | 217.367 us | 229.526 us | 0.05x |
| `lokad/langserv/url-download` | 2.507 us | 50.565 us | 50.941 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.062 us | 131.605 us | 143.061 us | 0.04x |
| `balancing/begin-end-blocks` | 5.705 us | 4,350.056 us | 4,383.917 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.271 us | 4.452 us | 26.195 us | 2.08x |
| `lokad/langserv/color-short-hex` | 1.674 us | 1.013 us | 1.214 us | 1.65x |
| `lokad/lexer/operator-run` | 3.127 us | 2.059 us | 2.687 us | 1.52x |
| `lokad/lexer/doc-line` | 3.316 us | 2.300 us | 3.059 us | 1.44x |
| `structural/keyword-family-to-capitalized-identifier` | 11.427 us | 7.941 us | 18.689 us | 1.44x |
| `lokad/langserv/helper-identifier` | 6.997 us | 4.894 us | 5.919 us | 1.43x |
| `lokad/lexer/identifier` | 24.306 us | 17.094 us | 21.325 us | 1.42x |
| `structural/ordered-identifier-window` | 15.585 us | 11.065 us | 22.139 us | 1.41x |
| `structural/keyword-to-generic-type` | 4.948 us | 3.525 us | 14.653 us | 1.40x |
| `structural/ordered-keyword-pair` | 5.550 us | 4.201 us | 15.060 us | 1.32x |
| `fallback/lookahead` | 5.447 us | 4.236 us | 15.150 us | 1.29x |
| `lokad/langserv/identifier-validator` | 11.409 us | 9.206 us | 11.637 us | 1.24x |
| `structural/modifier-family-to-type-window` | 70.478 us | 57.089 us | 69.627 us | 1.23x |
| `lokad/style/hex-color` | 0.612 us | 0.523 us | 0.638 us | 1.17x |
| `lokad/docs/inline-doc-prefix` | 3.358 us | 2.878 us | 3.619 us | 1.17x |
| `lokad/langserv/color-rgb-hex` | 2.053 us | 1.872 us | 2.053 us | 1.10x |
| `lokad/lexer/number` | 8.182 us | 7.611 us | 8.739 us | 1.08x |
| `literal/identifier-token-casei` | 5.774 us | 5.927 us | 17.185 us | 0.97x |
| `literal-family/type-token-family` | 10.564 us | 11.572 us | 22.218 us | 0.91x |
| `lokad/style/cell-ref` | 1.466 us | 1.770 us | 1.947 us | 0.83x |
| `structural/method-family-generic-call` | 3.504 us | 4.526 us | 15.022 us | 0.77x |
| `structural/method-family-call` | 3.934 us | 5.117 us | 15.544 us | 0.77x |
| `literal-family/method-token-family` | 3.922 us | 5.337 us | 16.183 us | 0.73x |
| `literal/call-token` | 2.660 us | 4.012 us | 14.671 us | 0.66x |
| `structural/ordered-keyword-window` | 5.562 us | 8.685 us | 19.696 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.252 us | 8.692 us | 19.867 us | 0.60x |
| `literal/identifier-token` | 2.801 us | 4.729 us | 15.703 us | 0.59x |
| `lokad/style/range-ref` | 0.905 us | 1.537 us | 1.621 us | 0.59x |
| `lokad/lexer/string` | 22.095 us | 47.074 us | 49.958 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 5.067 us | 10.796 us | 21.745 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.414 us | 7.547 us | 8.116 us | 0.32x |
| `lokad/langserv/url-download` | 2.498 us | 12.983 us | 13.473 us | 0.19x |
| `lokad/folding/region-marker` | 6.054 us | 36.158 us | 60.498 us | 0.17x |
| `balancing/begin-end-blocks` | 5.767 us | 470.922 us | 499.912 us | 0.01x |
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
