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

Core's default Validate policy checks the entire subject, including text after
an early match. PythonRe and POSIX consume existing valid text; PCRE2 checks its
native UTF-mode window and has an explicit trusted-input bypass.
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
and resource checks completed in **46m40s**. The [run receipt](bench/Experiments/DeliveryDev89-20261114/full-refresh.json)
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
| `literal/sherlock-zh` | 71.155 us | 19.004 us | 2,195.594 us | 3.74x |
| `literal/sherlock-ru` | 160.624 us | 97.280 us | 5,344.314 us | 1.65x |
| `literal-alternate/sherlock-zh` | 93.652 us | 69.010 us | 2,206.536 us | 1.36x |
| `literal/sherlock-casei-en` | 157.786 us | 190.524 us | 577.702 us | 0.83x |
| `literal/sherlock-en` | 65.412 us | 87.937 us | 464.970 us | 0.74x |
| `literal/sherlock-casei-ru` | 242.367 us | 366.955 us | 5,559.446 us | 0.66x |
| `ruff-noqa/tweaked` | 4,783.581 us | 8,042.809 us | 45,632.300 us | 0.59x |
| `aws-keys/quick` | 4,082.137 us | 9,587.450 us | 46,078.400 us | 0.43x |
| `bounded-repeat/letters-en` | 1,113.790 us | 2,820.911 us | 2,935.850 us | 0.39x |
| `bounded-repeat/letters-ru` | 2,122.992 us | 5,889.277 us | 6,704.550 us | 0.36x |
| `literal-alternate/sherlock-ru` | 1,643.165 us | 4,854.119 us | 10,090.475 us | 0.34x |
| `literal-alternate/sherlock-en-mixed` | 193.505 us | 596.068 us | 1,016.159 us | 0.32x |
| `dictionary/single` | 8,278.240 us | 26,170.600 us | 26,148.200 us | 0.32x |
| `literal-alternate/sherlock-en` | 394.653 us | 1,543.684 us | 2,101.279 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 162.683 us | 678.370 us | 1,099.044 us | 0.24x |
| `bounded-repeat/capitals` | 9,501.288 us | 46,089.650 us | 49,842.000 us | 0.21x |
| `bounded-repeat/context` | 22,021.950 us | 149,498.200 us | 153,289.600 us | 0.15x |
| `aws-keys/full` | 11,163.057 us | 89,231.200 us | 119,191.100 us | 0.13x |
| `ruff-noqa/real` | 4,829.381 us | 71,189.050 us | 87,359.100 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,003.371 us | 23,419.975 us | 28,732.250 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 166.482 us | 11,813.786 us | 12,566.367 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 951.135 us | 491.381 us | 8,989.375 us | 1.94x |
| `common/match-word` | `Match` | 0.196 us | 0.173 us | 1.154 us | 1.13x |
| `industry/leipzig-twain-count` | `Count` | 1,281.239 us | 1,318.793 us | 8,430.689 us | 0.97x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.110 us | 0.146 us | 0.70x |
| `common/uri-miss` | `IsMatch` | 0.116 us | 0.188 us | 0.207 us | 0.62x |
| `industry/mariomka-uri-count` | `Count` | 1,159.053 us | 1,886.234 us | 10,194.450 us | 0.61x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.085 us | 0.102 us | 0.58x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.170 us | 0.191 us | 0.51x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 109.906 us | 260.959 us | 488.722 us | 0.42x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.175 us | 0.195 us | 0.40x |
| `common/ip-miss` | `IsMatch` | 0.070 us | 0.186 us | 0.205 us | 0.38x |
| `common/uri-match` | `IsMatch` | 0.089 us | 0.242 us | 0.273 us | 0.37x |
| `common/replace-words` | `Replace` | 28.095 us | 77.510 us | 80.660 us | 0.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.272 us | 0.298 us | 0.35x |
| `common/matches-word` | `Count` | 1.362 us | 4.299 us | 6.014 us | 0.32x |
| `common/email-miss` | `IsMatch` | 0.098 us | 0.347 us | 0.372 us | 0.28x |
| `industry/mariomka-ip-count` | `Count` | 2,130.775 us | 8,398.800 us | 16,843.067 us | 0.25x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.112 us | 0.142 us | 0.25x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.016 us | 0.083 us | 0.098 us | 0.20x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 181.118 us | 974.851 us | 1,263.969 us | 0.19x |
| `common/split-words` | `Split` | 14.601 us | 80.705 us | 82.121 us | 0.18x |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.571 us | 0.599 us | 0.17x |
| `industry/leipzig-name-family-count` | `Count` | 1,709.677 us | 10,053.725 us | 17,182.233 us | 0.17x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.091 us | 0.13x |
| `common/matches-words` | `Count` | 9.355 us | 71.058 us | 72.536 us | 0.13x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.621 us | 0.657 us | 0.12x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.835 us | 0.879 us | 0.10x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,852.651 us | 20,001.250 us | 20,476.575 us | 0.09x |
| `industry/leipzig-symbol-count` | `Count` | 1,650.367 us | 23,042.775 us | 30,689.950 us | 0.07x |
| `industry/leipzig-river-window-count` | `Count` | 2,205.757 us | 32,467.567 us | 39,926.100 us | 0.07x |
| `common/matches-boundary` | `Count` | 10.553 us | 166.958 us | 168.381 us | 0.06x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.759 us | 8,626.389 us | 9,169.422 us | 0.06x |
| `common/matches-set` | `Count` | 9.594 us | 198.107 us | 200.065 us | 0.05x |
| `industry/rust-sherlock-ing-count` | `Count` | 243.550 us | 12,103.071 us | 12,780.000 us | 0.02x |
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
| `literal/sherlock-zh` | 71.093 us | 24.160 us | 2,176.965 us | 2.94x |
| `literal-alternate/sherlock-zh` | 93.698 us | 36.335 us | 2,159.664 us | 2.58x |
| `literal/sherlock-casei-en` | 158.103 us | 78.169 us | 444.948 us | 2.02x |
| `literal/sherlock-ru` | 160.898 us | 89.196 us | 5,239.286 us | 1.80x |
| `literal-alternate/sherlock-casei-en` | 167.976 us | 133.660 us | 596.180 us | 1.26x |
| `literal/sherlock-casei-ru` | 242.979 us | 196.065 us | 5,448.847 us | 1.24x |
| `literal-alternate/sherlock-en` | 395.483 us | 347.273 us | 790.450 us | 1.14x |
| `aws-keys/quick` | 4,101.726 us | 3,715.067 us | 38,736.300 us | 1.10x |
| `literal-alternate/sherlock-en-mixed` | 194.080 us | 177.139 us | 572.096 us | 1.10x |
| `ruff-noqa/tweaked` | 4,796.381 us | 4,628.353 us | 40,429.700 us | 1.04x |
| `bounded-repeat/letters-ru` | 2,124.867 us | 2,073.049 us | 2,888.146 us | 1.02x |
| `literal/sherlock-en` | 65.474 us | 76.709 us | 454.624 us | 0.85x |
| `literal-alternate/sherlock-en-nomatch` | 163.308 us | 208.320 us | 584.898 us | 0.78x |
| `bounded-repeat/capitals` | 9,507.450 us | 13,639.150 us | 16,502.250 us | 0.70x |
| `literal-alternate/sherlock-ru` | 1,643.428 us | 2,362.969 us | 7,513.775 us | 0.70x |
| `bounded-repeat/letters-en` | 1,116.231 us | 1,856.883 us | 1,949.738 us | 0.60x |
| `aws-keys/full` | 10,292.114 us | 19,185.700 us | 51,485.900 us | 0.54x |
| `literal-alternate/sherlock-casei-ru` | 1,005.362 us | 1,995.395 us | 7,244.975 us | 0.50x |
| `bounded-repeat/context` | 21,943.900 us | 69,822.850 us | 73,441.250 us | 0.31x |
| `ruff-noqa/real` | 4,859.875 us | 21,130.425 us | 47,938.933 us | 0.23x |
| `dictionary/single` | 8,282.690 us | 46,066.550 us | 46,019.750 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/match-word` | `Match` | 0.194 us | 0.080 us | 1.012 us | 2.44x |
| `industry/mariomka-email-count` | `Count` | 951.420 us | 460.799 us | 7,824.875 us | 2.06x |
| `common/backtracking` | `IsMatch` | 0.087 us | 0.044 us | 0.075 us | 1.95x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 110.077 us | 58.560 us | 268.523 us | 1.88x |
| `common/uri-miss` | `IsMatch` | 0.115 us | 0.066 us | 0.088 us | 1.74x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.048 us | 0.030 us | 0.046 us | 1.63x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.031 us | 0.049 us | 1.60x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.078 us | 0.049 us | 0.086 us | 1.60x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.087 us | 0.055 us | 0.077 us | 1.60x |
| `common/replace-words` | `Replace` | 28.039 us | 17.972 us | 19.467 us | 1.56x |
| `common/ip-match` | `IsMatch` | 0.071 us | 0.049 us | 0.069 us | 1.45x |
| `industry/leipzig-river-window-count` | `Count` | 2,207.583 us | 1,539.284 us | 7,734.180 us | 1.43x |
| `common/ip-miss` | `IsMatch` | 0.070 us | 0.050 us | 0.070 us | 1.41x |
| `industry/leipzig-symbol-count` | `Count` | 1,675.104 us | 1,228.525 us | 7,213.800 us | 1.36x |
| `common/email-match` | `IsMatch` | 0.095 us | 0.082 us | 0.109 us | 1.15x |
| `common/email-miss` | `IsMatch` | 0.097 us | 0.090 us | 0.116 us | 1.08x |
| `industry/mariomka-uri-count` | `Count` | 1,161.786 us | 1,101.721 us | 8,420.975 us | 1.05x |
| `industry/leipzig-twain-count` | `Count` | 1,282.325 us | 1,278.580 us | 7,347.222 us | 1.00x |
| `common/uri-match` | `IsMatch` | 0.089 us | 0.089 us | 0.121 us | 0.99x |
| `common/one-node-backtracking` | `IsMatch` | 0.074 us | 0.101 us | 0.130 us | 0.73x |
| `common/split-words` | `Split` | 14.602 us | 20.964 us | 22.817 us | 0.70x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.043 us | 0.073 us | 0.65x |
| `common/matches-words` | `Count` | 9.338 us | 15.306 us | 17.763 us | 0.61x |
| `industry/mariomka-ip-count` | `Count` | 2,136.953 us | 3,545.595 us | 11,028.133 us | 0.60x |
| `industry/leipzig-name-family-count` | `Count` | 1,727.332 us | 3,040.677 us | 8,222.400 us | 0.57x |
| `common/matches-word` | `Count` | 1.356 us | 2.424 us | 3.744 us | 0.56x |
| `common/date-miss` | `IsMatch` | 0.100 us | 0.203 us | 0.238 us | 0.50x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.013 us | 0.036 us | 0.053 us | 0.36x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 182.363 us | 598.317 us | 869.716 us | 0.30x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,853.893 us | 8,920.078 us | 9,520.487 us | 0.21x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 502.539 us | 4,000.905 us | 4,378.183 us | 0.13x |
| `common/matches-boundary` | `Count` | 10.556 us | 105.791 us | 107.710 us | 0.10x |
| `common/matches-set` | `Count` | 9.593 us | 105.899 us | 107.542 us | 0.09x |
| `industry/rust-sherlock-ing-count` | `Count` | 245.818 us | 4,810.887 us | 5,239.280 us | 0.05x |
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
| `lokad/imports/module-imports` | 9.355 us | 6.498 us | 30.929 us | 1.44x |
| `structural/keyword-to-generic-type` | 5.647 us | 4.117 us | 18.589 us | 1.37x |
| `lokad/langserv/color-short-hex` | 1.672 us | 1.376 us | 1.506 us | 1.22x |
| `lokad/lexer/doc-line` | 3.236 us | 3.025 us | 3.755 us | 1.07x |
| `structural/ordered-keyword-pair` | 5.590 us | 5.395 us | 19.600 us | 1.04x |
| `lokad/lexer/identifier` | 23.562 us | 23.264 us | 25.964 us | 1.01x |
| `lokad/langserv/helper-identifier` | 7.196 us | 7.394 us | 7.994 us | 0.97x |
| `fallback/lookahead` | 5.926 us | 6.115 us | 19.576 us | 0.97x |
| `lokad/style/hex-color` | 0.775 us | 0.844 us | 0.904 us | 0.92x |
| `lokad/langserv/identifier-validator` | 11.165 us | 12.620 us | 13.935 us | 0.88x |
| `literal-family/method-token-family` | 7.622 us | 9.376 us | 22.425 us | 0.81x |
| `lokad/lexer/operator-run` | 2.819 us | 3.473 us | 3.996 us | 0.81x |
| `lokad/docs/inline-doc-prefix` | 3.304 us | 4.879 us | 5.657 us | 0.68x |
| `lokad/langserv/color-rgb-hex` | 2.020 us | 3.014 us | 2.992 us | 0.67x |
| `literal/identifier-token` | 2.791 us | 4.232 us | 17.911 us | 0.66x |
| `literal/call-token` | 2.634 us | 4.371 us | 18.268 us | 0.60x |
| `lokad/lexer/number` | 7.999 us | 14.443 us | 15.331 us | 0.55x |
| `structural/method-family-generic-call` | 3.472 us | 6.769 us | 20.469 us | 0.51x |
| `structural/ordered-identifier-window` | 14.574 us | 28.951 us | 42.519 us | 0.50x |
| `lokad/style/cell-ref` | 1.429 us | 2.966 us | 3.085 us | 0.48x |
| `structural/method-family-call` | 3.858 us | 8.027 us | 21.221 us | 0.48x |
| `literal-family/type-token-family` | 10.600 us | 24.476 us | 38.362 us | 0.43x |
| `literal/identifier-token-casei` | 5.371 us | 15.152 us | 29.094 us | 0.35x |
| `structural/ordered-keyword-window` | 5.514 us | 17.696 us | 31.760 us | 0.31x |
| `lokad/style/range-ref` | 0.883 us | 2.851 us | 2.886 us | 0.31x |
| `structural/ordered-modifier-pair` | 5.250 us | 17.544 us | 31.311 us | 0.30x |
| `structural/modifier-family-to-type-window` | 69.247 us | 257.185 us | 270.572 us | 0.27x |
| `lokad/lexer/string` | 21.612 us | 96.641 us | 102.050 us | 0.22x |
| `lokad/folding/region-marker` | 6.363 us | 37.533 us | 62.095 us | 0.17x |
| `lokad/langserv/url-dashboard` | 2.368 us | 24.619 us | 25.104 us | 0.10x |
| `structural/keyword-family-to-capitalized-identifier` | 11.422 us | 219.001 us | 232.573 us | 0.05x |
| `lokad/langserv/url-download` | 2.517 us | 53.199 us | 53.806 us | 0.05x |
| `structural/modifier-family-to-type-pair` | 5.008 us | 133.561 us | 147.357 us | 0.04x |
| `balancing/begin-end-blocks` | 5.734 us | 4,386.300 us | 4,414.356 us | 0.0013x |
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
| `lokad/imports/module-imports` | 9.465 us | 4.456 us | 28.139 us | 2.12x |
| `lokad/langserv/color-short-hex` | 1.663 us | 1.021 us | 1.207 us | 1.63x |
| `structural/keyword-to-generic-type` | 5.647 us | 3.487 us | 17.407 us | 1.62x |
| `lokad/langserv/helper-identifier` | 7.234 us | 4.717 us | 5.805 us | 1.53x |
| `lokad/style/hex-color` | 0.778 us | 0.519 us | 0.620 us | 1.50x |
| `lokad/lexer/doc-line` | 3.235 us | 2.254 us | 3.080 us | 1.43x |
| `structural/keyword-family-to-capitalized-identifier` | 11.457 us | 7.986 us | 21.323 us | 1.43x |
| `fallback/lookahead` | 5.921 us | 4.247 us | 17.316 us | 1.39x |
| `lokad/lexer/identifier` | 23.502 us | 16.925 us | 20.383 us | 1.39x |
| `structural/ordered-identifier-window` | 14.593 us | 10.545 us | 24.373 us | 1.38x |
| `lokad/lexer/operator-run` | 2.815 us | 2.062 us | 2.584 us | 1.37x |
| `structural/ordered-keyword-pair` | 5.592 us | 4.182 us | 17.669 us | 1.34x |
| `structural/modifier-family-to-type-window` | 68.614 us | 56.416 us | 71.474 us | 1.22x |
| `lokad/langserv/identifier-validator` | 11.053 us | 9.254 us | 11.039 us | 1.19x |
| `lokad/docs/inline-doc-prefix` | 3.313 us | 2.906 us | 3.631 us | 1.14x |
| `lokad/lexer/number` | 8.004 us | 7.515 us | 8.450 us | 1.07x |
| `lokad/langserv/color-rgb-hex` | 2.013 us | 1.916 us | 2.084 us | 1.05x |
| `literal-family/type-token-family` | 10.615 us | 11.564 us | 24.840 us | 0.92x |
| `literal/identifier-token-casei` | 5.353 us | 5.977 us | 19.887 us | 0.90x |
| `lokad/style/cell-ref` | 1.423 us | 1.835 us | 2.009 us | 0.78x |
| `structural/method-family-generic-call` | 3.467 us | 4.536 us | 17.602 us | 0.76x |
| `structural/method-family-call` | 3.872 us | 5.113 us | 17.922 us | 0.76x |
| `literal-family/method-token-family` | 3.906 us | 5.365 us | 18.232 us | 0.73x |
| `literal/call-token` | 2.638 us | 4.018 us | 17.303 us | 0.66x |
| `structural/ordered-keyword-window` | 5.498 us | 8.661 us | 22.272 us | 0.63x |
| `structural/ordered-modifier-pair` | 5.277 us | 8.647 us | 22.307 us | 0.61x |
| `literal/identifier-token` | 2.796 us | 4.729 us | 17.601 us | 0.59x |
| `lokad/style/range-ref` | 0.885 us | 1.565 us | 1.628 us | 0.57x |
| `structural/modifier-family-to-type-pair` | 5.019 us | 10.747 us | 24.571 us | 0.47x |
| `lokad/lexer/string` | 21.609 us | 47.117 us | 50.195 us | 0.46x |
| `lokad/langserv/url-dashboard` | 2.363 us | 7.523 us | 8.128 us | 0.31x |
| `lokad/langserv/url-download` | 2.515 us | 12.921 us | 13.467 us | 0.19x |
| `lokad/folding/region-marker` | 6.392 us | 36.892 us | 61.076 us | 0.17x |
| `balancing/begin-end-blocks` | 5.731 us | 469.770 us | 501.130 us | 0.01x |
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
