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
and resource checks completed in **47m25s**. The [run receipt](bench/Experiments/DeliveryDev82-20261113/full-refresh.json)
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
| `literal/sherlock-zh` | 94.422 us | 18.378 us | 2,159.611 us | 5.14x |
| `literal-alternate/sherlock-zh` | 115.134 us | 65.356 us | 2,148.141 us | 1.76x |
| `literal/sherlock-ru` | 165.890 us | 96.609 us | 5,155.727 us | 1.72x |
| `literal/sherlock-casei-en` | 154.660 us | 181.574 us | 569.750 us | 0.85x |
| `literal/sherlock-en` | 55.459 us | 83.099 us | 452.711 us | 0.67x |
| `literal/sherlock-casei-ru` | 231.244 us | 354.379 us | 5,615.108 us | 0.65x |
| `ruff-noqa/tweaked` | 4,503.171 us | 7,661.564 us | 36,939.300 us | 0.59x |
| `bounded-repeat/letters-en` | 1,106.591 us | 2,733.911 us | 2,884.131 us | 0.40x |
| `aws-keys/quick` | 3,600.124 us | 9,234.956 us | 45,538.950 us | 0.39x |
| `bounded-repeat/letters-ru` | 1,968.569 us | 5,662.214 us | 6,474.000 us | 0.35x |
| `dictionary/single` | 7,937.590 us | 24,512.633 us | 24,563.975 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,498.727 us | 4,675.218 us | 9,551.775 us | 0.32x |
| `literal-alternate/sherlock-en-mixed` | 175.882 us | 570.188 us | 1,018.002 us | 0.31x |
| `literal-alternate/sherlock-en` | 365.472 us | 1,467.902 us | 2,036.100 us | 0.25x |
| `literal-alternate/sherlock-en-nomatch` | 147.982 us | 624.221 us | 1,047.241 us | 0.24x |
| `bounded-repeat/capitals` | 9,108.478 us | 43,864.800 us | 47,861.250 us | 0.21x |
| `bounded-repeat/context` | 21,652.250 us | 142,724.100 us | 146,590.700 us | 0.15x |
| `aws-keys/full` | 10,374.800 us | 82,669.700 us | 114,464.800 us | 0.13x |
| `ruff-noqa/real` | 4,474.150 us | 68,480.400 us | 98,524.200 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 924.261 us | 22,440.700 us | 27,577.950 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 119.376 us | 11,141.343 us | 11,962.214 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 966.261 us | 470.933 us | 8,477.000 us | 2.05x |
| `common/match-word` | `Match` | 0.189 us | 0.167 us | 1.110 us | 1.13x |
| `industry/leipzig-twain-count` | `Count` | 1,230.907 us | 1,343.224 us | 8,373.133 us | 0.92x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.076 us | 0.106 us | 0.142 us | 0.72x |
| `industry/mariomka-uri-count` | `Count` | 1,216.237 us | 1,786.930 us | 9,865.275 us | 0.68x |
| `common/uri-miss` | `IsMatch` | 0.121 us | 0.183 us | 0.202 us | 0.66x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.081 us | 0.097 us | 0.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.163 us | 0.180 us | 0.53x |
| `common/ip-match` | `IsMatch` | 0.074 us | 0.165 us | 0.184 us | 0.45x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 104.599 us | 244.884 us | 463.688 us | 0.43x |
| `common/uri-match` | `IsMatch` | 0.092 us | 0.220 us | 0.255 us | 0.42x |
| `common/ip-miss` | `IsMatch` | 0.069 us | 0.177 us | 0.196 us | 0.39x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.256 us | 0.279 us | 0.37x |
| `common/replace-words` | `Replace` | 26.331 us | 73.668 us | 76.865 us | 0.36x |
| `common/matches-word` | `Count` | 1.295 us | 4.014 us | 5.635 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.107 us | 0.339 us | 0.365 us | 0.32x |
| `industry/mariomka-ip-count` | `Count` | 2,070.300 us | 8,099.580 us | 16,221.667 us | 0.26x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.106 us | 0.135 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.079 us | 0.094 us | 0.20x |
| `common/split-words` | `Split` | 14.083 us | 73.593 us | 75.195 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 168.314 us | 929.870 us | 1,198.217 us | 0.18x |
| `industry/leipzig-name-family-count` | `Count` | 1,660.767 us | 9,442.444 us | 16,727.333 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.094 us | 0.550 us | 0.583 us | 0.17x |
| `common/matches-words` | `Count` | 8.893 us | 66.431 us | 67.493 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.079 us | 0.595 us | 0.628 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.009 us | 0.074 us | 0.087 us | 0.13x |
| `common/backtracking` | `IsMatch` | 0.086 us | 0.813 us | 0.856 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,748.416 us | 18,979.700 us | 19,545.025 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,483.115 us | 22,218.575 us | 29,379.800 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,006.314 us | 30,615.833 us | 37,651.900 us | 0.07x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 453.139 us | 8,154.070 us | 8,749.000 us | 0.06x |
| `common/matches-boundary` | `Count` | 6.816 us | 164.933 us | 165.570 us | 0.04x |
| `common/matches-set` | `Count` | 6.808 us | 189.124 us | 190.128 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 226.287 us | 11,871.229 us | 12,502.086 us | 0.02x |
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
| `literal/sherlock-zh` | 94.716 us | 23.323 us | 2,163.056 us | 4.06x |
| `literal-alternate/sherlock-zh` | 114.150 us | 34.465 us | 2,029.868 us | 3.31x |
| `literal/sherlock-casei-en` | 157.445 us | 77.452 us | 401.476 us | 2.03x |
| `literal/sherlock-ru` | 166.944 us | 87.546 us | 5,150.407 us | 1.91x |
| `literal/sherlock-casei-ru` | 231.309 us | 189.787 us | 5,353.627 us | 1.22x |
| `aws-keys/quick` | 3,703.373 us | 3,502.809 us | 36,844.950 us | 1.06x |
| `literal-alternate/sherlock-en-mixed` | 176.065 us | 168.786 us | 516.954 us | 1.04x |
| `literal-alternate/sherlock-en` | 365.928 us | 355.485 us | 735.144 us | 1.03x |
| `ruff-noqa/tweaked` | 4,467.353 us | 4,470.767 us | 35,163.500 us | 1.00x |
| `bounded-repeat/letters-ru` | 1,962.969 us | 1,995.358 us | 2,788.093 us | 0.98x |
| `literal-alternate/sherlock-casei-en` | 120.093 us | 126.701 us | 600.282 us | 0.95x |
| `literal/sherlock-en` | 55.270 us | 72.880 us | 421.576 us | 0.76x |
| `literal-alternate/sherlock-en-nomatch` | 146.970 us | 198.589 us | 566.327 us | 0.74x |
| `bounded-repeat/capitals` | 9,155.156 us | 13,027.783 us | 16,047.475 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,501.700 us | 2,270.015 us | 7,256.483 us | 0.66x |
| `bounded-repeat/letters-en` | 1,100.101 us | 1,767.079 us | 1,898.179 us | 0.62x |
| `aws-keys/full` | 10,430.575 us | 17,652.380 us | 51,206.333 us | 0.59x |
| `literal-alternate/sherlock-casei-ru` | 922.143 us | 1,899.200 us | 7,036.975 us | 0.49x |
| `bounded-repeat/context` | 21,283.600 us | 67,811.550 us | 70,348.900 us | 0.31x |
| `ruff-noqa/real` | 4,485.888 us | 20,101.350 us | 51,547.933 us | 0.22x |
| `dictionary/single` | 7,880.510 us | 44,058.950 us | 43,765.550 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.189 us | 0.075 us | 0.984 us | 2.51x |
| `industry/mariomka-email-count` | `Count` | 972.795 us | 439.368 us | 7,570.900 us | 2.21x |
| `common/backtracking` | `IsMatch` | 0.085 us | 0.042 us | 0.072 us | 2.05x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 104.416 us | 55.444 us | 261.713 us | 1.88x |
| `common/uri-miss` | `IsMatch` | 0.118 us | 0.064 us | 0.084 us | 1.86x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.053 us | 0.029 us | 0.045 us | 1.81x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.053 us | 0.031 us | 0.048 us | 1.73x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.077 us | 0.047 us | 0.082 us | 1.64x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.086 us | 0.054 us | 0.075 us | 1.60x |
| `common/replace-words` | `Replace` | 26.481 us | 16.618 us | 17.826 us | 1.59x |
| `common/ip-miss` | `IsMatch` | 0.074 us | 0.047 us | 0.066 us | 1.57x |
| `common/ip-match` | `IsMatch` | 0.070 us | 0.046 us | 0.066 us | 1.52x |
| `industry/leipzig-river-window-count` | `Count` | 2,013.545 us | 1,473.685 us | 7,520.840 us | 1.37x |
| `industry/leipzig-symbol-count` | `Count` | 1,474.594 us | 1,202.755 us | 6,918.040 us | 1.23x |
| `common/email-miss` | `IsMatch` | 0.106 us | 0.087 us | 0.113 us | 1.22x |
| `common/email-match` | `IsMatch` | 0.094 us | 0.077 us | 0.104 us | 1.21x |
| `industry/mariomka-uri-count` | `Count` | 1,214.240 us | 1,076.959 us | 8,208.150 us | 1.13x |
| `common/uri-match` | `IsMatch` | 0.091 us | 0.086 us | 0.117 us | 1.06x |
| `industry/leipzig-twain-count` | `Count` | 1,239.805 us | 1,303.810 us | 7,378.620 us | 0.95x |
| `common/one-node-backtracking` | `IsMatch` | 0.078 us | 0.095 us | 0.124 us | 0.82x |
| `common/split-words` | `Split` | 14.031 us | 19.699 us | 21.794 us | 0.71x |
| `common/date-match` | `IsMatch` | 0.027 us | 0.042 us | 0.073 us | 0.63x |
| `common/matches-words` | `Count` | 8.901 us | 14.389 us | 16.875 us | 0.62x |
| `industry/mariomka-ip-count` | `Count` | 2,060.530 us | 3,390.365 us | 10,476.867 us | 0.61x |
| `common/matches-word` | `Count` | 1.293 us | 2.228 us | 3.251 us | 0.58x |
| `industry/leipzig-name-family-count` | `Count` | 1,644.972 us | 2,958.169 us | 7,901.750 us | 0.56x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.192 us | 0.227 us | 0.49x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.035 us | 0.051 us | 0.30x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.460 us | 566.771 us | 814.711 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,751.305 us | 8,509.667 us | 9,162.956 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 452.660 us | 3,825.500 us | 4,198.500 us | 0.12x |
| `common/matches-boundary` | `Count` | 6.877 us | 102.664 us | 104.246 us | 0.07x |
| `common/matches-set` | `Count` | 6.768 us | 101.188 us | 102.270 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 227.079 us | 4,589.853 us | 5,112.080 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.036 us | 6.149 us | 30.482 us | 1.47x |
| `structural/keyword-to-generic-type` | 4.897 us | 3.933 us | 17.910 us | 1.25x |
| `lokad/langserv/color-short-hex` | 1.573 us | 1.287 us | 1.396 us | 1.22x |
| `lokad/lexer/doc-line` | 3.084 us | 2.893 us | 3.457 us | 1.07x |
| `lokad/lexer/identifier` | 22.753 us | 21.927 us | 24.562 us | 1.04x |
| `structural/ordered-keyword-pair` | 5.349 us | 5.187 us | 18.927 us | 1.03x |
| `lokad/langserv/helper-identifier` | 7.044 us | 7.042 us | 7.396 us | 1.00x |
| `lokad/style/hex-color` | 0.699 us | 0.796 us | 0.843 us | 0.88x |
| `lokad/langserv/identifier-validator` | 10.739 us | 12.269 us | 13.344 us | 0.88x |
| `fallback/lookahead` | 5.123 us | 5.856 us | 19.088 us | 0.87x |
| `lokad/lexer/operator-run` | 2.682 us | 3.288 us | 3.648 us | 0.82x |
| `literal-family/method-token-family` | 6.982 us | 8.938 us | 21.789 us | 0.78x |
| `lokad/docs/inline-doc-prefix` | 3.171 us | 4.730 us | 5.274 us | 0.67x |
| `lokad/langserv/color-rgb-hex` | 1.958 us | 2.940 us | 2.883 us | 0.67x |
| `literal/identifier-token` | 2.686 us | 4.209 us | 17.203 us | 0.64x |
| `literal/call-token` | 2.487 us | 4.107 us | 17.075 us | 0.61x |
| `lokad/lexer/number` | 7.676 us | 13.780 us | 14.324 us | 0.56x |
| `structural/method-family-generic-call` | 3.296 us | 6.251 us | 19.177 us | 0.53x |
| `structural/ordered-identifier-window` | 14.031 us | 27.230 us | 40.870 us | 0.52x |
| `structural/method-family-call` | 3.730 us | 7.709 us | 20.665 us | 0.48x |
| `lokad/style/cell-ref` | 1.359 us | 2.928 us | 2.843 us | 0.46x |
| `literal-family/type-token-family` | 10.175 us | 22.486 us | 36.264 us | 0.45x |
| `literal/identifier-token-casei` | 5.801 us | 14.300 us | 28.398 us | 0.41x |
| `lokad/style/range-ref` | 0.831 us | 2.594 us | 2.565 us | 0.32x |
| `structural/ordered-modifier-pair` | 5.145 us | 16.971 us | 30.055 us | 0.30x |
| `structural/ordered-keyword-window` | 5.158 us | 17.165 us | 30.751 us | 0.30x |
| `structural/modifier-family-to-type-window` | 65.945 us | 249.989 us | 263.029 us | 0.26x |
| `lokad/lexer/string` | 20.569 us | 93.279 us | 97.621 us | 0.22x |
| `lokad/folding/region-marker` | 5.934 us | 35.360 us | 61.479 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.311 us | 23.627 us | 24.500 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 10.990 us | 208.792 us | 224.221 us | 0.05x |
| `lokad/langserv/url-download` | 2.413 us | 48.606 us | 49.118 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 4.761 us | 124.348 us | 137.992 us | 0.04x |
| `balancing/begin-end-blocks` | 5.462 us | 4,144.347 us | 4,123.505 us | 0.0013x |
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
| `lokad/imports/module-imports` | 8.949 us | 4.217 us | 27.817 us | 2.12x |
| `lokad/langserv/color-short-hex` | 1.576 us | 0.918 us | 1.103 us | 1.72x |
| `lokad/langserv/helper-identifier` | 6.958 us | 4.546 us | 5.329 us | 1.53x |
| `lokad/lexer/doc-line` | 3.093 us | 2.034 us | 2.788 us | 1.52x |
| `structural/keyword-to-generic-type` | 4.847 us | 3.306 us | 16.887 us | 1.47x |
| `lokad/lexer/operator-run` | 2.670 us | 1.828 us | 2.389 us | 1.46x |
| `structural/keyword-family-to-capitalized-identifier` | 10.825 us | 7.503 us | 20.593 us | 1.44x |
| `lokad/style/hex-color` | 0.698 us | 0.485 us | 0.567 us | 1.44x |
| `lokad/lexer/identifier` | 22.562 us | 15.770 us | 19.536 us | 1.43x |
| `structural/ordered-identifier-window` | 14.050 us | 10.169 us | 23.622 us | 1.38x |
| `structural/ordered-keyword-pair` | 5.328 us | 3.963 us | 16.982 us | 1.34x |
| `fallback/lookahead` | 5.132 us | 4.104 us | 16.938 us | 1.25x |
| `lokad/langserv/identifier-validator` | 10.607 us | 8.577 us | 10.422 us | 1.24x |
| `structural/modifier-family-to-type-window` | 66.040 us | 54.219 us | 68.920 us | 1.22x |
| `lokad/docs/inline-doc-prefix` | 3.162 us | 2.705 us | 3.409 us | 1.17x |
| `lokad/lexer/number` | 7.639 us | 6.936 us | 8.000 us | 1.10x |
| `lokad/langserv/color-rgb-hex` | 1.948 us | 1.770 us | 1.936 us | 1.10x |
| `literal/identifier-token-casei` | 5.809 us | 5.707 us | 19.332 us | 1.02x |
| `literal-family/type-token-family` | 10.169 us | 11.146 us | 24.513 us | 0.91x |
| `lokad/style/cell-ref` | 1.360 us | 1.730 us | 1.897 us | 0.79x |
| `structural/method-family-call` | 3.710 us | 4.919 us | 17.330 us | 0.75x |
| `structural/method-family-generic-call` | 3.277 us | 4.362 us | 16.773 us | 0.75x |
| `literal-family/method-token-family` | 3.814 us | 5.135 us | 17.812 us | 0.74x |
| `literal/call-token` | 2.488 us | 3.779 us | 16.151 us | 0.66x |
| `structural/ordered-keyword-window` | 5.183 us | 8.249 us | 21.618 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.190 us | 8.333 us | 21.545 us | 0.62x |
| `lokad/style/range-ref` | 0.828 us | 1.396 us | 1.454 us | 0.59x |
| `literal/identifier-token` | 2.683 us | 4.578 us | 16.859 us | 0.59x |
| `structural/modifier-family-to-type-pair` | 4.753 us | 10.210 us | 23.291 us | 0.47x |
| `lokad/lexer/string` | 20.533 us | 44.304 us | 47.550 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.298 us | 7.393 us | 8.051 us | 0.31x |
| `lokad/langserv/url-download` | 2.405 us | 12.458 us | 12.960 us | 0.19x |
| `lokad/folding/region-marker` | 5.924 us | 34.356 us | 60.984 us | 0.17x |
| `balancing/begin-end-blocks` | 5.486 us | 446.266 us | 476.159 us | 0.01x |
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
