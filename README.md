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
Core's default Validate policy checks this. PythonRe and POSIX consume existing
valid text; PCRE2 has its own UTF-mode checking and trusted-input bypass.
Byte coordinates are primary; UTF-16 coordinates and strings are derived
conveniences. Keep borrowed input unchanged and its owner alive while a view or
cursor uses it. Owned snapshots remain valid after cursor advance or disposal.

## Subject input contracts

Core selects its validation policy once at construction or through its factory:

```csharp
var regex = new Utf8Regex("needle", inputValidation: Utf8InputValidation.AssumeValid);
// regex.InputValidation is read-only.
```

`Validate` (default) checks the whole subject at most once per operation or cursor
lifetime. `AssumeValid` omits that pass when the caller guarantees valid UTF-8 and
unchanged borrowed bytes. Core operations and cursors inherit this choice.
PythonRe models Python `re` over `str`; PythonRe and POSIX require already-valid
UTF-8 text and omit regex-operation validity passes. Validate external bytes where
they become text. PCRE2 retains explicit UTF-mode validation controls.
Pattern/replacement
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

The tables were refreshed on **13 November 2026** using **.NET 11**
(`11.0.0`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Existing tables use **Validate**; AssumeValid
measurements have a separate caller-validity contract. Values come from
[the compact Core summary](bench/Snapshots/Core.json). Qualification verdicts,
recorded source identities and snapshot hashes remain in that summary. Unqualified rows
retain their observations without establishing parity. The complete Release
build, .NET 10/11 tests, all-profile catalogs, paired comparators, lifecycle/scaling
and resource checks completed in **46m58s**. The [run receipt](bench/Experiments/DeliveryDev81-20261113/full-refresh.json)
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
| `literal/sherlock-zh` | 95.766 us | 18.586 us | 2,165.861 us | 5.15x |
| `literal/sherlock-ru` | 173.720 us | 94.569 us | 5,204.240 us | 1.84x |
| `literal-alternate/sherlock-zh` | 114.866 us | 67.090 us | 2,194.862 us | 1.71x |
| `literal/sherlock-casei-en` | 152.461 us | 180.141 us | 585.750 us | 0.85x |
| `literal/sherlock-en` | 55.887 us | 84.350 us | 448.344 us | 0.66x |
| `literal/sherlock-casei-ru` | 236.181 us | 356.701 us | 5,587.100 us | 0.66x |
| `ruff-noqa/tweaked` | 4,270.867 us | 8,029.827 us | 43,630.950 us | 0.53x |
| `aws-keys/quick` | 3,908.779 us | 9,451.388 us | 39,787.400 us | 0.41x |
| `bounded-repeat/letters-en` | 1,079.721 us | 2,770.882 us | 2,919.077 us | 0.39x |
| `bounded-repeat/letters-ru` | 1,993.121 us | 5,731.693 us | 6,554.792 us | 0.35x |
| `literal-alternate/sherlock-ru` | 1,519.364 us | 4,720.681 us | 10,116.625 us | 0.32x |
| `dictionary/single` | 8,047.764 us | 25,719.100 us | 25,619.467 us | 0.31x |
| `literal-alternate/sherlock-en-mixed` | 177.794 us | 607.911 us | 1,043.158 us | 0.29x |
| `literal-alternate/sherlock-en` | 382.120 us | 1,468.285 us | 2,019.238 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 149.295 us | 635.397 us | 1,078.547 us | 0.23x |
| `bounded-repeat/capitals` | 9,208.688 us | 44,197.050 us | 46,743.450 us | 0.21x |
| `bounded-repeat/context` | 21,675.875 us | 142,446.200 us | 147,013.900 us | 0.15x |
| `aws-keys/full` | 8,641.033 us | 83,332.700 us | 117,991.000 us | 0.10x |
| `ruff-noqa/real` | 4,327.972 us | 68,266.150 us | 99,436.100 us | 0.06x |
| `literal-alternate/sherlock-casei-ru` | 925.811 us | 22,329.725 us | 27,572.450 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 130.006 us | 11,302.643 us | 12,009.943 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,014.439 us | 486.118 us | 8,832.650 us | 2.09x |
| `common/match-word` | `Match` | 0.195 us | 0.167 us | 1.114 us | 1.16x |
| `industry/leipzig-twain-count` | `Count` | 1,158.209 us | 1,294.632 us | 8,410.344 us | 0.89x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.109 us | 0.145 us | 0.71x |
| `industry/mariomka-uri-count` | `Count` | 1,254.559 us | 1,878.464 us | 10,318.050 us | 0.67x |
| `common/uri-miss` | `IsMatch` | 0.122 us | 0.196 us | 0.216 us | 0.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.050 us | 0.082 us | 0.098 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.163 us | 0.182 us | 0.53x |
| `common/ip-match` | `IsMatch` | 0.076 us | 0.175 us | 0.195 us | 0.44x |
| `common/ip-miss` | `IsMatch` | 0.077 us | 0.183 us | 0.200 us | 0.42x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 106.760 us | 260.812 us | 487.329 us | 0.41x |
| `common/uri-match` | `IsMatch` | 0.094 us | 0.229 us | 0.261 us | 0.41x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.263 us | 0.289 us | 0.36x |
| `common/replace-words` | `Replace` | 27.241 us | 78.814 us | 79.254 us | 0.35x |
| `common/matches-word` | `Count` | 1.329 us | 4.235 us | 5.902 us | 0.31x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.344 us | 0.372 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,100.903 us | 8,147.480 us | 16,327.833 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.108 us | 0.135 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.081 us | 0.095 us | 0.20x |
| `common/split-words` | `Split` | 14.448 us | 77.419 us | 79.043 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 171.231 us | 962.454 us | 1,265.772 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.543 us | 0.581 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,649.404 us | 9,777.150 us | 16,202.533 us | 0.17x |
| `common/matches-words` | `Count` | 9.244 us | 69.137 us | 70.504 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.076 us | 0.607 us | 0.640 us | 0.12x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.074 us | 0.090 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.859 us | 0.901 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,746.025 us | 18,943.925 us | 19,666.425 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,615.566 us | 23,780.650 us | 31,424.350 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,150.646 us | 31,781.733 us | 38,946.350 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 465.054 us | 8,164.810 us | 8,607.011 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.051 us | 165.235 us | 165.347 us | 0.04x |
| `common/matches-set` | `Count` | 6.908 us | 190.009 us | 190.423 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 231.322 us | 11,821.943 us | 12,321.843 us | 0.02x |
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
| `literal/sherlock-zh` | 95.112 us | 23.387 us | 2,156.763 us | 4.07x |
| `literal-alternate/sherlock-zh` | 114.063 us | 35.157 us | 2,122.837 us | 3.24x |
| `literal/sherlock-ru` | 172.535 us | 85.525 us | 5,118.367 us | 2.02x |
| `literal/sherlock-casei-en` | 151.790 us | 76.020 us | 418.516 us | 2.00x |
| `literal/sherlock-casei-ru` | 237.997 us | 192.104 us | 5,373.873 us | 1.24x |
| `aws-keys/quick` | 3,877.289 us | 3,655.900 us | 35,406.167 us | 1.06x |
| `literal-alternate/sherlock-en` | 380.174 us | 360.937 us | 767.282 us | 1.05x |
| `literal-alternate/sherlock-en-mixed` | 178.691 us | 173.974 us | 579.496 us | 1.03x |
| `bounded-repeat/letters-ru` | 1,992.159 us | 2,004.289 us | 2,800.885 us | 0.99x |
| `ruff-noqa/tweaked` | 4,287.717 us | 4,457.929 us | 37,710.750 us | 0.96x |
| `literal-alternate/sherlock-casei-en` | 128.899 us | 142.059 us | 601.989 us | 0.91x |
| `literal/sherlock-en` | 55.461 us | 74.348 us | 425.409 us | 0.75x |
| `literal-alternate/sherlock-en-nomatch` | 148.575 us | 199.857 us | 612.974 us | 0.74x |
| `bounded-repeat/capitals` | 9,216.517 us | 13,382.433 us | 16,895.200 us | 0.69x |
| `literal-alternate/sherlock-ru` | 1,519.531 us | 2,321.282 us | 7,489.600 us | 0.65x |
| `bounded-repeat/letters-en` | 1,073.525 us | 1,807.626 us | 1,902.050 us | 0.59x |
| `aws-keys/full` | 10,482.300 us | 17,987.120 us | 52,198.300 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 919.199 us | 1,911.659 us | 7,110.225 us | 0.48x |
| `bounded-repeat/context` | 21,253.600 us | 67,218.750 us | 70,104.950 us | 0.32x |
| `ruff-noqa/real` | 4,355.944 us | 20,137.675 us | 54,397.667 us | 0.22x |
| `dictionary/single` | 8,087.910 us | 44,857.300 us | 44,725.950 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.195 us | 0.077 us | 0.974 us | 2.52x |
| `industry/mariomka-email-count` | `Count` | 1,016.692 us | 453.302 us | 7,625.675 us | 2.24x |
| `common/backtracking` | `IsMatch` | 0.086 us | 0.043 us | 0.072 us | 2.01x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.055 us | 0.029 us | 0.046 us | 1.88x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.065 us | 0.086 us | 1.87x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 107.446 us | 58.962 us | 263.586 us | 1.82x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.054 us | 0.031 us | 0.049 us | 1.74x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.048 us | 0.083 us | 1.62x |
| `common/replace-words` | `Replace` | 27.397 us | 17.125 us | 18.266 us | 1.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.055 us | 0.075 us | 1.59x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.048 us | 0.067 us | 1.50x |
| `common/ip-miss` | `IsMatch` | 0.071 us | 0.048 us | 0.067 us | 1.48x |
| `industry/leipzig-river-window-count` | `Count` | 2,167.853 us | 1,543.870 us | 7,829.120 us | 1.40x |
| `industry/leipzig-symbol-count` | `Count` | 1,617.049 us | 1,249.760 us | 7,248.820 us | 1.29x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.080 us | 0.107 us | 1.17x |
| `industry/mariomka-uri-count` | `Count` | 1,253.568 us | 1,100.496 us | 8,379.475 us | 1.14x |
| `common/email-miss` | `IsMatch` | 0.099 us | 0.089 us | 0.115 us | 1.11x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.087 us | 0.118 us | 1.05x |
| `industry/leipzig-twain-count` | `Count` | 1,163.933 us | 1,276.992 us | 7,372.850 us | 0.91x |
| `common/matches-words` | `Count` | 9.179 us | 11.072 us | 14.212 us | 0.83x |
| `common/one-node-backtracking` | `IsMatch` | 0.073 us | 0.094 us | 0.128 us | 0.78x |
| `common/split-words` | `Split` | 14.265 us | 20.717 us | 22.465 us | 0.69x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.043 us | 0.073 us | 0.64x |
| `industry/mariomka-ip-count` | `Count` | 2,114.531 us | 3,451.859 us | 10,716.200 us | 0.61x |
| `common/matches-word` | `Count` | 1.326 us | 2.360 us | 3.424 us | 0.56x |
| `industry/leipzig-name-family-count` | `Count` | 1,674.387 us | 3,067.512 us | 8,178.825 us | 0.55x |
| `common/date-miss` | `IsMatch` | 0.096 us | 0.195 us | 0.232 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.035 us | 0.053 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 172.861 us | 588.556 us | 855.093 us | 0.29x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,747.926 us | 8,491.000 us | 9,017.433 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 472.731 us | 3,863.545 us | 4,225.732 us | 0.12x |
| `common/matches-boundary` | `Count` | 7.060 us | 102.898 us | 105.600 us | 0.07x |
| `common/matches-set` | `Count` | 6.926 us | 104.090 us | 105.511 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 236.108 us | 4,706.525 us | 5,154.113 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.170 us | 6.316 us | 31.488 us | 1.45x |
| `lokad/langserv/color-short-hex` | 1.613 us | 1.299 us | 1.411 us | 1.24x |
| `structural/keyword-to-generic-type` | 4.921 us | 3.975 us | 18.283 us | 1.24x |
| `lokad/lexer/doc-line` | 3.192 us | 2.995 us | 3.549 us | 1.07x |
| `lokad/langserv/helper-identifier` | 7.144 us | 6.995 us | 7.477 us | 1.02x |
| `lokad/lexer/identifier` | 22.930 us | 22.581 us | 24.821 us | 1.02x |
| `structural/ordered-keyword-pair` | 5.200 us | 5.229 us | 19.419 us | 0.99x |
| `lokad/langserv/identifier-validator` | 11.271 us | 12.562 us | 13.628 us | 0.90x |
| `fallback/lookahead` | 5.183 us | 5.915 us | 19.417 us | 0.88x |
| `lokad/lexer/operator-run` | 2.743 us | 3.425 us | 3.747 us | 0.80x |
| `literal-family/method-token-family` | 6.985 us | 9.033 us | 21.818 us | 0.77x |
| `lokad/style/hex-color` | 0.559 us | 0.798 us | 0.845 us | 0.70x |
| `lokad/docs/inline-doc-prefix` | 3.247 us | 4.814 us | 5.313 us | 0.67x |
| `lokad/langserv/color-rgb-hex` | 1.968 us | 3.014 us | 2.941 us | 0.65x |
| `literal/identifier-token` | 2.704 us | 4.225 us | 17.327 us | 0.64x |
| `literal/call-token` | 2.493 us | 4.141 us | 17.478 us | 0.60x |
| `lokad/lexer/number` | 7.806 us | 14.394 us | 15.026 us | 0.54x |
| `structural/method-family-generic-call` | 3.314 us | 6.546 us | 19.829 us | 0.51x |
| `structural/ordered-identifier-window` | 14.136 us | 28.031 us | 41.798 us | 0.50x |
| `lokad/style/cell-ref` | 1.394 us | 2.876 us | 2.909 us | 0.48x |
| `structural/method-family-call` | 3.729 us | 7.838 us | 20.628 us | 0.48x |
| `literal-family/type-token-family` | 10.312 us | 23.317 us | 37.337 us | 0.44x |
| `literal/identifier-token-casei` | 5.534 us | 15.216 us | 28.661 us | 0.36x |
| `lokad/style/range-ref` | 0.867 us | 2.668 us | 2.684 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.264 us | 16.548 us | 29.990 us | 0.32x |
| `structural/ordered-keyword-window` | 5.253 us | 17.692 us | 31.043 us | 0.30x |
| `structural/modifier-family-to-type-window` | 67.458 us | 249.031 us | 262.738 us | 0.27x |
| `lokad/lexer/string` | 20.792 us | 92.371 us | 97.686 us | 0.23x |
| `lokad/folding/region-marker` | 5.938 us | 35.985 us | 61.945 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.363 us | 24.151 us | 24.483 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.012 us | 211.184 us | 225.723 us | 0.05x |
| `lokad/langserv/url-download` | 2.433 us | 49.574 us | 49.974 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.891 us | 132.452 us | 147.391 us | 0.04x |
| `balancing/begin-end-blocks` | 5.507 us | 4,172.553 us | 4,200.778 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.173 us | 4.335 us | 28.426 us | 2.12x |
| `lokad/langserv/color-short-hex` | 1.622 us | 0.939 us | 1.143 us | 1.73x |
| `lokad/langserv/helper-identifier` | 6.999 us | 4.463 us | 5.442 us | 1.57x |
| `lokad/lexer/doc-line` | 3.150 us | 2.071 us | 2.818 us | 1.52x |
| `structural/keyword-to-generic-type` | 4.889 us | 3.333 us | 17.074 us | 1.47x |
| `structural/keyword-family-to-capitalized-identifier` | 11.239 us | 7.677 us | 21.319 us | 1.46x |
| `lokad/lexer/operator-run` | 2.756 us | 1.895 us | 2.446 us | 1.45x |
| `lokad/lexer/identifier` | 22.776 us | 16.155 us | 19.539 us | 1.41x |
| `structural/ordered-identifier-window` | 14.098 us | 10.156 us | 23.943 us | 1.39x |
| `structural/ordered-keyword-pair` | 5.195 us | 4.001 us | 17.296 us | 1.30x |
| `lokad/langserv/identifier-validator` | 11.371 us | 8.794 us | 10.905 us | 1.29x |
| `fallback/lookahead` | 5.165 us | 4.108 us | 17.200 us | 1.26x |
| `structural/modifier-family-to-type-window` | 66.587 us | 54.499 us | 69.963 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.236 us | 2.701 us | 3.458 us | 1.20x |
| `lokad/style/hex-color` | 0.563 us | 0.498 us | 0.586 us | 1.13x |
| `lokad/langserv/color-rgb-hex` | 1.961 us | 1.811 us | 2.036 us | 1.08x |
| `lokad/lexer/number` | 7.855 us | 7.288 us | 8.154 us | 1.08x |
| `literal/identifier-token-casei` | 5.519 us | 5.779 us | 19.297 us | 0.96x |
| `literal-family/type-token-family` | 10.317 us | 11.318 us | 24.536 us | 0.91x |
| `lokad/style/cell-ref` | 1.396 us | 1.780 us | 1.896 us | 0.78x |
| `structural/method-family-call` | 3.742 us | 4.922 us | 17.415 us | 0.76x |
| `structural/method-family-generic-call` | 3.318 us | 4.374 us | 16.928 us | 0.76x |
| `literal-family/method-token-family` | 3.741 us | 5.171 us | 17.852 us | 0.72x |
| `literal/call-token` | 2.506 us | 3.834 us | 16.548 us | 0.65x |
| `structural/ordered-keyword-window` | 5.309 us | 8.426 us | 21.834 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.265 us | 8.399 us | 21.732 us | 0.63x |
| `lokad/style/range-ref` | 0.864 us | 1.426 us | 1.495 us | 0.61x |
| `literal/identifier-token` | 2.684 us | 4.562 us | 16.772 us | 0.59x |
| `lokad/lexer/string` | 20.856 us | 44.490 us | 47.852 us | 0.47x |
| `structural/modifier-family-to-type-pair` | 4.900 us | 10.489 us | 24.238 us | 0.47x |
| `lokad/langserv/url-dashboard` | 2.368 us | 7.363 us | 7.997 us | 0.32x |
| `lokad/langserv/url-download` | 2.435 us | 12.575 us | 13.160 us | 0.19x |
| `lokad/folding/region-marker` | 6.010 us | 36.034 us | 61.991 us | 0.17x |
| `balancing/begin-end-blocks` | 5.490 us | 450.298 us | 478.756 us | 0.01x |
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
