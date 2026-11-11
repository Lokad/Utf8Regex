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
and resource checks passed in **46m28s**. The [run receipt](bench/Experiments/DeliveryDev69-20261111/full-refresh.json)
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
| `literal/sherlock-zh` | 82.924 us | 18.986 us | 2,195.937 us | 4.37x |
| `literal/sherlock-ru` | 167.824 us | 99.447 us | 5,352.221 us | 1.69x |
| `literal-alternate/sherlock-zh` | 104.792 us | 69.292 us | 2,203.285 us | 1.51x |
| `literal/sherlock-casei-en` | 158.507 us | 203.514 us | 619.281 us | 0.78x |
| `literal/sherlock-casei-ru` | 244.406 us | 367.168 us | 5,766.485 us | 0.67x |
| `literal/sherlock-en` | 57.141 us | 87.252 us | 468.126 us | 0.65x |
| `ruff-noqa/tweaked` | 4,540.176 us | 7,771.691 us | 43,306.900 us | 0.58x |
| `bounded-repeat/letters-en` | 1,127.431 us | 2,857.440 us | 2,968.515 us | 0.39x |
| `aws-keys/quick` | 3,767.855 us | 9,727.750 us | 41,188.750 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,009.729 us | 6,008.954 us | 6,824.745 us | 0.33x |
| `literal-alternate/sherlock-ru` | 1,543.673 us | 4,765.962 us | 9,997.225 us | 0.32x |
| `dictionary/single` | 8,416.100 us | 26,282.233 us | 26,301.500 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 188.843 us | 601.329 us | 1,038.092 us | 0.31x |
| `literal-alternate/sherlock-en` | 375.620 us | 1,523.624 us | 2,041.059 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 155.848 us | 649.632 us | 1,084.179 us | 0.24x |
| `bounded-repeat/capitals` | 9,596.325 us | 46,892.950 us | 53,929.250 us | 0.20x |
| `bounded-repeat/context` | 20,654.650 us | 148,841.900 us | 152,553.200 us | 0.14x |
| `aws-keys/full` | 8,939.943 us | 87,179.600 us | 123,310.500 us | 0.10x |
| `ruff-noqa/real` | 4,365.622 us | 75,580.100 us | 90,906.800 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 974.783 us | 23,278.075 us | 28,528.500 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 130.545 us | 11,343.171 us | 12,213.029 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,213.130 us | 507.726 us | 9,014.500 us | 2.39x |
| `common/match-word` | `Match` | 0.194 us | 0.173 us | 1.155 us | 1.12x |
| `industry/leipzig-twain-count` | `Count` | 1,269.335 us | 1,394.075 us | 8,760.740 us | 0.91x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.094 us | 0.111 us | 0.148 us | 0.85x |
| `industry/mariomka-uri-count` | `Count` | 1,458.487 us | 1,886.022 us | 10,281.300 us | 0.77x |
| `common/uri-miss` | `IsMatch` | 0.124 us | 0.195 us | 0.215 us | 0.64x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.085 us | 0.102 us | 0.59x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.094 us | 0.169 us | 0.187 us | 0.55x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 120.308 us | 268.968 us | 512.049 us | 0.45x |
| `common/ip-miss` | `IsMatch` | 0.084 us | 0.191 us | 0.210 us | 0.44x |
| `common/ip-match` | `IsMatch` | 0.080 us | 0.182 us | 0.202 us | 0.44x |
| `common/uri-match` | `IsMatch` | 0.096 us | 0.237 us | 0.276 us | 0.41x |
| `common/replace-words` | `Replace` | 28.111 us | 78.184 us | 81.134 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.296 us | 0.321 us | 0.32x |
| `common/matches-word` | `Count` | 1.380 us | 4.332 us | 6.089 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.346 us | 0.371 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,323.830 us | 8,406.389 us | 16,820.367 us | 0.28x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.109 us | 0.139 us | 0.26x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.082 us | 0.098 us | 0.21x |
| `common/date-miss` | `IsMatch` | 0.109 us | 0.564 us | 0.600 us | 0.19x |
| `common/split-words` | `Split` | 14.584 us | 80.197 us | 81.714 us | 0.18x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.667 us | 1,036.632 us | 1,332.385 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,668.156 us | 10,000.737 us | 17,206.700 us | 0.17x |
| `common/matches-words` | `Count` | 9.363 us | 71.332 us | 73.250 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.612 us | 0.644 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.089 us | 0.843 us | 0.884 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,993.169 us | 19,874.350 us | 20,402.225 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,871.895 us | 22,759.800 us | 30,120.750 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 2,167.983 us | 32,287.433 us | 39,596.300 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 517.979 us | 8,575.078 us | 9,048.333 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.848 us | 164.810 us | 166.873 us | 0.05x |
| `common/matches-set` | `Count` | 7.805 us | 196.380 us | 197.955 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 240.248 us | 12,073.714 us | 12,588.500 us | 0.02x |
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
| `literal/sherlock-zh` | 82.946 us | 24.157 us | 2,190.038 us | 3.43x |
| `literal-alternate/sherlock-zh` | 104.995 us | 36.030 us | 2,164.531 us | 2.91x |
| `literal/sherlock-casei-en` | 158.719 us | 79.433 us | 450.551 us | 2.00x |
| `literal/sherlock-ru` | 168.073 us | 91.354 us | 5,341.014 us | 1.84x |
| `literal/sherlock-casei-ru` | 244.258 us | 226.581 us | 5,542.864 us | 1.08x |
| `literal-alternate/sherlock-en-mixed` | 188.688 us | 180.999 us | 598.300 us | 1.04x |
| `aws-keys/quick` | 3,766.457 us | 3,685.348 us | 36,011.667 us | 1.02x |
| `literal-alternate/sherlock-en` | 375.807 us | 375.557 us | 793.947 us | 1.00x |
| `ruff-noqa/tweaked` | 4,535.371 us | 4,615.271 us | 37,885.000 us | 0.98x |
| `bounded-repeat/letters-ru` | 2,002.932 us | 2,067.251 us | 2,881.215 us | 0.97x |
| `literal-alternate/sherlock-casei-en` | 130.870 us | 147.945 us | 579.647 us | 0.88x |
| `literal-alternate/sherlock-en-nomatch` | 155.706 us | 208.699 us | 606.221 us | 0.75x |
| `literal/sherlock-en` | 57.299 us | 76.990 us | 412.046 us | 0.74x |
| `bounded-repeat/capitals` | 9,599.325 us | 13,773.500 us | 21,366.525 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,543.069 us | 2,367.338 us | 7,509.283 us | 0.65x |
| `bounded-repeat/letters-en` | 1,130.859 us | 1,853.822 us | 1,941.564 us | 0.61x |
| `aws-keys/full` | 10,196.543 us | 18,637.060 us | 55,696.933 us | 0.55x |
| `literal-alternate/sherlock-casei-ru` | 973.296 us | 1,986.213 us | 7,156.275 us | 0.49x |
| `bounded-repeat/context` | 20,626.700 us | 69,935.650 us | 73,191.400 us | 0.29x |
| `ruff-noqa/real` | 4,387.361 us | 21,045.575 us | 48,242.200 us | 0.21x |
| `dictionary/single` | 8,400.967 us | 46,117.350 us | 46,144.450 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,214.940 us | 473.110 us | 7,777.950 us | 2.57x |
| `common/match-word` | `Match` | 0.194 us | 0.082 us | 0.989 us | 2.36x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 120.784 us | 58.777 us | 285.333 us | 2.05x |
| `common/backtracking` | `IsMatch` | 0.088 us | 0.044 us | 0.074 us | 1.99x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.094 us | 0.049 us | 0.090 us | 1.91x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.065 us | 0.087 us | 1.87x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.055 us | 0.031 us | 0.048 us | 1.78x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.055 us | 0.032 us | 0.051 us | 1.72x |
| `common/ip-miss` | `IsMatch` | 0.083 us | 0.051 us | 0.071 us | 1.63x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.089 us | 0.056 us | 0.076 us | 1.58x |
| `common/replace-words` | `Replace` | 28.016 us | 18.148 us | 19.643 us | 1.54x |
| `industry/leipzig-symbol-count` | `Count` | 1,873.744 us | 1,226.797 us | 7,362.200 us | 1.53x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.049 us | 0.069 us | 1.53x |
| `industry/leipzig-river-window-count` | `Count` | 2,175.942 us | 1,525.662 us | 7,650.560 us | 1.43x |
| `industry/mariomka-uri-count` | `Count` | 1,463.358 us | 1,116.631 us | 8,467.200 us | 1.31x |
| `common/email-match` | `IsMatch` | 0.096 us | 0.081 us | 0.108 us | 1.18x |
| `common/email-miss` | `IsMatch` | 0.100 us | 0.091 us | 0.120 us | 1.10x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.091 us | 0.122 us | 1.03x |
| `industry/leipzig-twain-count` | `Count` | 1,268.469 us | 1,348.720 us | 7,503.200 us | 0.94x |
| `common/one-node-backtracking` | `IsMatch` | 0.075 us | 0.097 us | 0.128 us | 0.78x |
| `common/split-words` | `Split` | 14.567 us | 21.232 us | 22.902 us | 0.69x |
| `industry/mariomka-ip-count` | `Count` | 2,329.815 us | 3,496.018 us | 10,922.000 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.029 us | 0.043 us | 0.074 us | 0.67x |
| `common/matches-words` | `Count` | 9.370 us | 15.036 us | 17.751 us | 0.62x |
| `common/matches-word` | `Count` | 1.374 us | 2.404 us | 3.900 us | 0.57x |
| `industry/leipzig-name-family-count` | `Count` | 1,672.059 us | 3,026.908 us | 8,197.000 us | 0.55x |
| `common/date-miss` | `IsMatch` | 0.110 us | 0.201 us | 0.237 us | 0.55x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.053 us | 0.31x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 175.970 us | 603.840 us | 861.296 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,988.063 us | 8,916.300 us | 9,447.000 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 517.435 us | 4,027.400 us | 4,404.822 us | 0.13x |
| `common/matches-boundary` | `Count` | 7.842 us | 105.517 us | 107.461 us | 0.07x |
| `common/matches-set` | `Count` | 7.830 us | 106.393 us | 108.154 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 239.588 us | 4,822.481 us | 5,246.160 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.406 us | 6.447 us | 31.642 us | 1.46x |
| `structural/keyword-to-generic-type` | 5.108 us | 4.169 us | 18.369 us | 1.23x |
| `lokad/langserv/color-short-hex` | 1.649 us | 1.399 us | 1.544 us | 1.18x |
| `lokad/lexer/identifier` | 25.993 us | 23.837 us | 26.632 us | 1.09x |
| `lokad/lexer/doc-line` | 3.272 us | 3.051 us | 3.856 us | 1.07x |
| `structural/ordered-keyword-pair` | 5.570 us | 5.362 us | 19.374 us | 1.04x |
| `lokad/langserv/helper-identifier` | 6.884 us | 7.439 us | 8.065 us | 0.93x |
| `lokad/langserv/identifier-validator` | 11.317 us | 12.782 us | 14.445 us | 0.89x |
| `fallback/lookahead` | 5.312 us | 6.109 us | 19.628 us | 0.87x |
| `lokad/lexer/operator-run` | 2.888 us | 3.529 us | 3.957 us | 0.82x |
| `literal-family/method-token-family` | 7.215 us | 9.273 us | 22.326 us | 0.78x |
| `lokad/style/hex-color` | 0.568 us | 0.832 us | 0.883 us | 0.68x |
| `lokad/docs/inline-doc-prefix` | 3.331 us | 5.035 us | 5.548 us | 0.66x |
| `lokad/langserv/color-rgb-hex` | 2.012 us | 3.107 us | 3.200 us | 0.65x |
| `literal/identifier-token` | 2.820 us | 4.379 us | 17.804 us | 0.64x |
| `literal/call-token` | 2.598 us | 4.330 us | 17.635 us | 0.60x |
| `lokad/lexer/number` | 8.100 us | 14.990 us | 15.752 us | 0.54x |
| `structural/method-family-generic-call` | 3.465 us | 6.717 us | 20.167 us | 0.52x |
| `structural/ordered-identifier-window` | 14.725 us | 28.877 us | 42.437 us | 0.51x |
| `lokad/style/cell-ref` | 1.458 us | 3.035 us | 3.002 us | 0.48x |
| `structural/method-family-call` | 3.906 us | 8.354 us | 21.471 us | 0.47x |
| `literal-family/type-token-family` | 10.548 us | 24.023 us | 37.899 us | 0.44x |
| `literal/identifier-token-casei` | 5.897 us | 15.884 us | 29.113 us | 0.37x |
| `lokad/style/range-ref` | 0.898 us | 2.782 us | 2.775 us | 0.32x |
| `structural/ordered-keyword-window` | 5.576 us | 17.932 us | 32.061 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.470 us | 17.619 us | 31.446 us | 0.31x |
| `structural/modifier-family-to-type-window` | 71.018 us | 256.302 us | 270.636 us | 0.28x |
| `lokad/lexer/string` | 21.859 us | 97.684 us | 103.658 us | 0.22x |
| `lokad/folding/region-marker` | 6.055 us | 37.347 us | 63.586 us | 0.16x |
| `lokad/langserv/url-dashboard` | 2.335 us | 24.906 us | 25.287 us | 0.09x |
| `structural/keyword-family-to-capitalized-identifier` | 11.476 us | 218.752 us | 232.989 us | 0.05x |
| `lokad/langserv/url-download` | 2.527 us | 53.145 us | 53.779 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.029 us | 136.542 us | 149.786 us | 0.04x |
| `balancing/begin-end-blocks` | 5.735 us | 4,192.556 us | 4,194.439 us | 0.0014x |
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
| `lokad/imports/module-imports` | 9.432 us | 4.454 us | 28.445 us | 2.12x |
| `lokad/langserv/color-short-hex` | 1.657 us | 1.005 us | 1.217 us | 1.65x |
| `lokad/lexer/identifier` | 26.007 us | 17.066 us | 20.995 us | 1.52x |
| `structural/keyword-to-generic-type` | 5.106 us | 3.481 us | 17.217 us | 1.47x |
| `lokad/lexer/doc-line` | 3.289 us | 2.263 us | 3.044 us | 1.45x |
| `structural/keyword-family-to-capitalized-identifier` | 11.425 us | 7.904 us | 24.634 us | 1.45x |
| `lokad/langserv/helper-identifier` | 6.882 us | 4.795 us | 5.847 us | 1.44x |
| `lokad/lexer/operator-run` | 2.888 us | 2.038 us | 2.648 us | 1.42x |
| `structural/ordered-identifier-window` | 14.708 us | 10.606 us | 24.009 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.570 us | 4.171 us | 17.418 us | 1.34x |
| `fallback/lookahead` | 5.323 us | 4.252 us | 17.339 us | 1.25x |
| `structural/modifier-family-to-type-window` | 71.249 us | 57.194 us | 72.544 us | 1.25x |
| `lokad/langserv/identifier-validator` | 11.355 us | 9.407 us | 11.515 us | 1.21x |
| `lokad/docs/inline-doc-prefix` | 3.338 us | 2.864 us | 3.690 us | 1.17x |
| `lokad/style/hex-color` | 0.568 us | 0.517 us | 0.609 us | 1.10x |
| `lokad/lexer/number` | 8.087 us | 7.467 us | 8.729 us | 1.08x |
| `literal/identifier-token-casei` | 5.889 us | 5.924 us | 19.675 us | 0.99x |
| `lokad/langserv/color-rgb-hex` | 2.016 us | 2.034 us | 2.284 us | 0.99x |
| `literal-family/type-token-family` | 10.581 us | 11.593 us | 24.738 us | 0.91x |
| `lokad/style/cell-ref` | 1.465 us | 1.745 us | 1.927 us | 0.84x |
| `structural/method-family-call` | 3.885 us | 5.070 us | 17.909 us | 0.77x |
| `structural/method-family-generic-call` | 3.459 us | 4.531 us | 17.383 us | 0.76x |
| `literal-family/method-token-family` | 3.905 us | 5.363 us | 18.121 us | 0.73x |
| `literal/call-token` | 2.593 us | 3.985 us | 16.710 us | 0.65x |
| `structural/ordered-keyword-window` | 5.555 us | 8.730 us | 22.509 us | 0.64x |
| `structural/ordered-modifier-pair` | 5.488 us | 8.713 us | 22.303 us | 0.63x |
| `literal/identifier-token` | 2.812 us | 4.741 us | 17.141 us | 0.59x |
| `lokad/style/range-ref` | 0.897 us | 1.536 us | 1.640 us | 0.58x |
| `structural/modifier-family-to-type-pair` | 5.055 us | 10.609 us | 24.289 us | 0.48x |
| `lokad/lexer/string` | 21.935 us | 47.500 us | 51.921 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.303 us | 8.094 us | 8.691 us | 0.28x |
| `lokad/langserv/url-download` | 2.527 us | 13.149 us | 13.719 us | 0.19x |
| `lokad/folding/region-marker` | 6.095 us | 38.173 us | 63.623 us | 0.16x |
| `balancing/begin-end-blocks` | 5.751 us | 470.809 us | 501.004 us | 0.01x |
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
