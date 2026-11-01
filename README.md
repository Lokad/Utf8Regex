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
uses the release-candidate SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The full baseline was refreshed on **1 November 2026** using **.NET 11 RC3**
(`11.0.0-rc.3.26522.148`) on Ubuntu 24.04.4 x64, Intel Xeon Platinum 8573C.
They measure public operations, including strict UTF-8 validation and required
result materialization. Values come from
[README.Benchmarks.json](README.Benchmarks.json); raw samples, binary identities
and [qualification verdicts](README.Parity.json) are retained. Unqualified rows
retain their observations without establishing parity. The complete source
build, both-runtime tests and all-profile benchmark refresh passed in **42m10s**;
package and consumer qualification are separate. Measurements describe each
row's recorded implementation; later source changes need their own evidence.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

Case IDs preserve upstream names: `common/ip-miss` matches a valid prefix of its
subject, so it measures a hit.

The complete Core/PythonRe/PCRE2 refresh took **45 minutes 37 seconds**, including
a fresh Release build, .NET 10/11 tests, paired comparators, lifecycle/scaling
measurements and resource checks. See the
[run receipt](bench/Experiments/Net11-20261027/subhour-refresh.json).
[refresh-benchmarks.ps1](refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](bench.ps1) supports focused single-case iterations.

Profile comparisons are in the
[PythonRe](src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md) and
[PCRE2](src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md) benchmark pages.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `literal/sherlock-zh` | 130.950 us | 18.218 us | 2,108.833 us | 7.19x |
| `literal-alternate/sherlock-zh` | 239.302 us | 67.345 us | 2,133.791 us | 3.55x |
| `bounded-repeat/capitals` | 122,936.700 us | 44,928.000 us | 51,042.900 us | 2.74x |
| `literal/sherlock-ru` | 239.739 us | 97.539 us | 5,177.960 us | 2.46x |
| `ruff-noqa/tweaked` | 10,424.840 us | 7,579.925 us | 40,632.250 us | 1.38x |
| `literal/sherlock-casei-en` | 165.981 us | 195.154 us | 588.747 us | 0.85x |
| `literal/sherlock-en` | 67.778 us | 82.694 us | 444.502 us | 0.82x |
| `literal/sherlock-casei-ru` | 245.447 us | 355.631 us | 5,418.367 us | 0.69x |
| `aws-keys/quick` | 4,887.055 us | 9,416.017 us | 41,943.400 us | 0.52x |
| `bounded-repeat/letters-en` | 1,004.296 us | 2,724.500 us | 2,830.700 us | 0.37x |
| `literal-alternate/sherlock-ru` | 1,659.358 us | 4,702.109 us | 9,763.133 us | 0.35x |
| `literal-alternate/sherlock-en-mixed` | 203.686 us | 590.805 us | 1,044.065 us | 0.34x |
| `bounded-repeat/letters-ru` | 1,852.825 us | 5,783.778 us | 6,498.175 us | 0.32x |
| `dictionary/single` | 7,989.300 us | 25,299.500 us | 25,384.550 us | 0.32x |
| `literal-alternate/sherlock-en` | 385.727 us | 1,483.526 us | 2,041.030 us | 0.26x |
| `literal-alternate/sherlock-en-nomatch` | 161.682 us | 639.785 us | 1,041.757 us | 0.25x |
| `bounded-repeat/context` | 19,864.133 us | 143,399.000 us | 145,790.100 us | 0.14x |
| `aws-keys/full` | 8,583.980 us | 83,561.500 us | 114,559.800 us | 0.10x |
| `ruff-noqa/real` | 4,547.133 us | 68,219.400 us | 100,033.500 us | 0.07x |
| `literal-alternate/sherlock-casei-ru` | 1,065.210 us | 22,484.033 us | 27,673.850 us | 0.05x |
| `literal-alternate/sherlock-casei-en` | 208.612 us | 11,089.800 us | 11,773.000 us | 0.02x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/mariomka-email-count` | `Count` | 1,473.682 us | 481.639 us | 8,851.133 us | 3.06x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.184 us | 0.111 us | 0.147 us | 1.65x |
| `common/match-word` | `Match` | 0.194 us | 0.161 us | 1.116 us | 1.21x |
| `industry/leipzig-twain-count` | `Count` | 1,319.645 us | 1,384.741 us | 8,979.000 us | 0.95x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 236.688 us | 254.368 us | 492.292 us | 0.93x |
| `industry/mariomka-uri-count` | `Count` | 1,734.097 us | 1,871.032 us | 10,270.133 us | 0.93x |
| `common/replace-words` | `Replace` | 70.863 us | 76.971 us | 79.441 us | 0.92x |
| `common/uri-miss` | `IsMatch` | 0.168 us | 0.200 us | 0.219 us | 0.84x |
| `common/uri-match` | `IsMatch` | 0.172 us | 0.223 us | 0.261 us | 0.77x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.092 us | 0.134 us | 0.164 us | 0.68x |
| `common/ip-match` | `IsMatch` | 0.113 us | 0.168 us | 0.187 us | 0.67x |
| `common/email-match` | `IsMatch` | 0.161 us | 0.261 us | 0.284 us | 0.62x |
| `common/ip-miss` | `IsMatch` | 0.110 us | 0.182 us | 0.200 us | 0.61x |
| `common/email-miss` | `IsMatch` | 0.170 us | 0.340 us | 0.364 us | 0.50x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.078 us | 0.167 us | 0.184 us | 0.47x |
| `industry/leipzig-name-family-count` | `Count` | 4,292.708 us | 9,974.867 us | 17,378.750 us | 0.43x |
| `common/matches-word` | `Count` | 1.382 us | 4.114 us | 5.747 us | 0.34x |
| `common/one-node-backtracking` | `IsMatch` | 0.199 us | 0.601 us | 0.625 us | 0.33x |
| `industry/mariomka-ip-count` | `Count` | 2,636.295 us | 8,138.000 us | 16,561.400 us | 0.32x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.105 us | 0.133 us | 0.27x |
| `common/backtracking` | `IsMatch` | 0.191 us | 0.799 us | 0.834 us | 0.24x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.017 us | 0.081 us | 0.098 us | 0.21x |
| `common/split-words` | `Split` | 14.106 us | 75.852 us | 77.572 us | 0.19x |
| `common/date-miss` | `IsMatch` | 0.094 us | 0.539 us | 0.569 us | 0.17x |
| `industry/leipzig-river-window-count` | `Count` | 5,794.667 us | 33,423.850 us | 50,003.300 us | 0.17x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 210.926 us | 1,217.541 us | 1,391.383 us | 0.17x |
| `common/matches-words` | `Count` | 9.176 us | 68.606 us | 69.841 us | 0.13x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.010 us | 0.077 us | 0.092 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,862.021 us | 19,083.400 us | 20,019.300 us | 0.10x |
| `industry/leipzig-symbol-count` | `Count` | 1,865.544 us | 21,682.950 us | 36,040.650 us | 0.09x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 509.814 us | 7,976.271 us | 8,506.183 us | 0.06x |
| `common/matches-boundary` | `Count` | 7.588 us | 158.203 us | 162.635 us | 0.05x |
| `common/matches-set` | `Count` | 7.706 us | 187.803 us | 189.749 us | 0.04x |
| `industry/rust-sherlock-ing-count` | `Count` | 253.870 us | 11,343.780 us | 11,881.360 us | 0.02x |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `bounded-repeat/capitals` | 122,800.900 us | 13,253.125 us | 19,682.867 us | 9.27x |
| `literal-alternate/sherlock-zh` | 232.183 us | 35.547 us | 2,103.367 us | 6.53x |
| `literal/sherlock-zh` | 132.441 us | 23.152 us | 2,096.136 us | 5.72x |
| `literal/sherlock-ru` | 239.863 us | 88.644 us | 5,180.090 us | 2.71x |
| `ruff-noqa/tweaked` | 10,986.220 us | 4,511.967 us | 34,573.850 us | 2.43x |
| `literal/sherlock-casei-en` | 167.692 us | 74.345 us | 452.151 us | 2.26x |
| `literal-alternate/sherlock-casei-en` | 207.174 us | 142.557 us | 546.880 us | 1.45x |
| `aws-keys/quick` | 4,885.864 us | 3,497.887 us | 33,296.850 us | 1.40x |
| `literal/sherlock-casei-ru` | 244.050 us | 194.960 us | 5,298.230 us | 1.25x |
| `literal-alternate/sherlock-en-mixed` | 203.813 us | 179.482 us | 569.304 us | 1.14x |
| `literal-alternate/sherlock-en` | 388.205 us | 363.674 us | 746.209 us | 1.07x |
| `literal/sherlock-en` | 68.121 us | 72.191 us | 462.131 us | 0.94x |
| `bounded-repeat/letters-ru` | 1,849.232 us | 2,007.858 us | 2,788.544 us | 0.92x |
| `literal-alternate/sherlock-en-nomatch` | 161.499 us | 198.094 us | 564.815 us | 0.82x |
| `literal-alternate/sherlock-ru` | 1,625.888 us | 2,268.096 us | 7,267.633 us | 0.72x |
| `bounded-repeat/letters-en` | 1,019.722 us | 1,770.793 us | 1,877.378 us | 0.58x |
| `literal-alternate/sherlock-casei-ru` | 1,061.976 us | 1,935.419 us | 7,066.733 us | 0.55x |
| `aws-keys/full` | 9,457.980 us | 18,245.633 us | 50,038.650 us | 0.52x |
| `bounded-repeat/context` | 20,151.933 us | 67,057.900 us | 69,401.300 us | 0.30x |
| `ruff-noqa/real` | 4,546.183 us | 20,241.700 us | 52,100.900 us | 0.22x |
| `dictionary/single` | 8,055.014 us | 44,452.600 us | 44,384.850 us | 0.18x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/backtracking` | `IsMatch` | 0.195 us | 0.042 us | 0.071 us | 4.71x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 237.612 us | 61.455 us | 278.800 us | 3.87x |
| `industry/leipzig-river-window-count` | `Count` | 6,020.617 us | 1,577.994 us | 16,026.100 us | 3.82x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.183 us | 0.049 us | 0.088 us | 3.72x |
| `common/replace-words` | `Replace` | 68.085 us | 18.677 us | 19.924 us | 3.65x |
| `industry/mariomka-email-count` | `Count` | 1,487.776 us | 445.905 us | 7,751.300 us | 3.34x |
| `common/uri-miss` | `IsMatch` | 0.166 us | 0.066 us | 0.082 us | 2.53x |
| `common/match-word` | `Match` | 0.194 us | 0.078 us | 0.970 us | 2.50x |
| `common/ip-match` | `IsMatch` | 0.112 us | 0.047 us | 0.066 us | 2.40x |
| `common/one-node-backtracking` | `IsMatch` | 0.198 us | 0.086 us | 0.124 us | 2.29x |
| `common/ip-miss` | `IsMatch` | 0.110 us | 0.048 us | 0.069 us | 2.27x |
| `common/email-match` | `IsMatch` | 0.160 us | 0.080 us | 0.105 us | 2.01x |
| `common/email-miss` | `IsMatch` | 0.170 us | 0.086 us | 0.111 us | 1.98x |
| `common/uri-match` | `IsMatch` | 0.171 us | 0.086 us | 0.116 us | 1.98x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.091 us | 0.048 us | 0.076 us | 1.88x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.053 us | 0.029 us | 0.046 us | 1.79x |
| `industry/mariomka-uri-count` | `Count` | 1,738.979 us | 1,102.391 us | 8,435.300 us | 1.58x |
| `industry/leipzig-symbol-count` | `Count` | 1,884.289 us | 1,293.088 us | 14,405.700 us | 1.46x |
| `industry/leipzig-name-family-count` | `Count` | 4,386.108 us | 3,038.929 us | 8,514.833 us | 1.44x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.074 us | 0.056 us | 0.078 us | 1.34x |
| `industry/leipzig-twain-count` | `Count` | 1,353.963 us | 1,331.437 us | 5,677.733 us | 1.02x |
| `common/matches-words` | `Count` | 9.086 us | 10.744 us | 13.794 us | 0.85x |
| `industry/mariomka-ip-count` | `Count` | 2,628.735 us | 3,408.007 us | 10,910.950 us | 0.77x |
| `common/split-words` | `Split` | 14.086 us | 20.926 us | 22.789 us | 0.67x |
| `common/date-match` | `IsMatch` | 0.028 us | 0.042 us | 0.072 us | 0.66x |
| `common/matches-word` | `Count` | 1.356 us | 2.286 us | 3.428 us | 0.59x |
| `common/date-miss` | `IsMatch` | 0.095 us | 0.197 us | 0.229 us | 0.48x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.011 us | 0.036 us | 0.052 us | 0.30x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 513.461 us | 3,834.579 us | 4,286.583 us | 0.13x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 212.046 us | 1,641.661 us | 2,133.335 us | 0.13x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,859.650 us | 17,698.750 us | 18,327.033 us | 0.11x |
| `common/matches-set` | `Count` | 7.696 us | 103.495 us | 104.938 us | 0.07x |
| `common/matches-boundary` | `Count` | 7.466 us | 101.564 us | 104.103 us | 0.07x |
| `industry/rust-sherlock-ing-count` | `Count` | 255.046 us | 4,640.436 us | 5,086.070 us | 0.05x |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 50.745 us | 9.622 us | 39.402 us | 5.27x |
| `structural/keyword-to-generic-type` | 8.995 us | 5.479 us | 18.929 us | 1.64x |
| `lokad/lexer/doc-line` | 4.608 us | 3.077 us | 3.684 us | 1.50x |
| `lokad/lexer/identifier` | 30.950 us | 22.181 us | 26.030 us | 1.40x |
| `structural/ordered-keyword-pair` | 10.170 us | 7.555 us | 21.238 us | 1.35x |
| `lokad/langserv/helper-identifier` | 9.608 us | 7.367 us | 8.102 us | 1.30x |
| `lokad/langserv/color-short-hex` | 1.647 us | 1.345 us | 1.487 us | 1.22x |
| `lokad/lexer/operator-run` | 5.882 us | 4.924 us | 6.035 us | 1.19x |
| `lokad/style/hex-color` | 1.172 us | 1.157 us | 1.336 us | 1.01x |
| `fallback/lookahead` | 6.165 us | 6.120 us | 17.569 us | 1.01x |
| `lokad/docs/inline-doc-prefix` | 4.567 us | 4.606 us | 5.398 us | 0.99x |
| `lokad/langserv/identifier-validator` | 17.170 us | 17.790 us | 15.054 us | 0.97x |
| `literal-family/method-token-family` | 8.099 us | 9.261 us | 20.118 us | 0.87x |
| `literal/identifier-token` | 4.492 us | 5.704 us | 18.878 us | 0.79x |
| `lokad/lexer/number` | 10.891 us | 14.060 us | 15.117 us | 0.77x |
| `lokad/langserv/color-rgb-hex` | 2.076 us | 2.980 us | 3.095 us | 0.70x |
| `literal/call-token` | 3.068 us | 4.573 us | 19.159 us | 0.67x |
| `lokad/style/cell-ref` | 1.927 us | 2.880 us | 3.248 us | 0.67x |
| `structural/method-family-call` | 4.800 us | 8.129 us | 19.462 us | 0.59x |
| `structural/method-family-generic-call` | 5.413 us | 9.470 us | 22.861 us | 0.57x |
| `literal-family/type-token-family` | 12.088 us | 25.260 us | 36.311 us | 0.48x |
| `literal/identifier-token-casei` | 7.137 us | 14.914 us | 26.681 us | 0.48x |
| `lokad/style/range-ref` | 1.202 us | 2.838 us | 2.897 us | 0.42x |
| `structural/ordered-modifier-pair` | 6.269 us | 17.791 us | 28.657 us | 0.35x |
| `structural/ordered-keyword-window` | 9.122 us | 27.172 us | 41.750 us | 0.34x |
| `structural/modifier-family-to-type-window` | 128.837 us | 414.607 us | 428.149 us | 0.31x |
| `lokad/lexer/string` | 29.515 us | 95.323 us | 101.383 us | 0.31x |
| `structural/keyword-family-to-capitalized-identifier` | 81.672 us | 356.755 us | 373.914 us | 0.23x |
| `structural/ordered-identifier-window` | 6.216 us | 28.408 us | 38.458 us | 0.22x |
| `lokad/folding/region-marker` | 7.050 us | 35.909 us | 83.659 us | 0.20x |
| `structural/modifier-family-to-type-pair` | 25.108 us | 129.983 us | 140.662 us | 0.19x |
| `lokad/langserv/url-dashboard` | 2.965 us | 24.624 us | 25.071 us | 0.12x |
| `lokad/langserv/url-download` | 2.814 us | 54.855 us | 55.275 us | 0.05x |
| `balancing/begin-end-blocks` | 9.704 us | 7,078.113 us | 7,091.113 us | 0.0014x |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary comparator for execution time. `.NET + decode` is a secondary end-to-end indicator. `Ratio` is Utf8Regex / .NET predecoded using the matching ordinary or compiled lanes; lower is better. Tables sort by decreasing ratio.
Ratios describe recorded medians; qualification verdicts and unqualified rows are recorded separately in [README.Parity.json](README.Parity.json).

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---:|---:|---:|---:|
| `lokad/imports/module-imports` | 50.479 us | 5.556 us | 34.785 us | 9.09x |
| `structural/keyword-family-to-capitalized-identifier` | 81.624 us | 10.759 us | 24.545 us | 7.59x |
| `structural/keyword-to-generic-type` | 9.057 us | 3.627 us | 14.590 us | 2.50x |
| `structural/modifier-family-to-type-pair` | 24.888 us | 10.846 us | 21.617 us | 2.29x |
| `lokad/lexer/operator-run` | 5.976 us | 2.662 us | 3.722 us | 2.24x |
| `structural/ordered-keyword-pair` | 10.156 us | 4.730 us | 18.086 us | 2.15x |
| `lokad/lexer/doc-line` | 4.330 us | 2.260 us | 2.911 us | 1.92x |
| `lokad/langserv/helper-identifier` | 9.635 us | 5.029 us | 6.085 us | 1.92x |
| `lokad/lexer/identifier` | 30.929 us | 17.292 us | 20.268 us | 1.79x |
| `lokad/langserv/color-short-hex` | 1.649 us | 0.995 us | 1.208 us | 1.66x |
| `lokad/style/hex-color` | 1.166 us | 0.736 us | 0.900 us | 1.58x |
| `structural/modifier-family-to-type-window` | 130.655 us | 83.489 us | 99.546 us | 1.56x |
| `lokad/docs/inline-doc-prefix` | 4.380 us | 2.810 us | 3.511 us | 1.56x |
| `fallback/lookahead` | 6.209 us | 4.264 us | 15.244 us | 1.46x |
| `lokad/lexer/number` | 10.747 us | 7.433 us | 8.502 us | 1.45x |
| `lokad/langserv/identifier-validator` | 17.254 us | 12.521 us | 11.849 us | 1.38x |
| `literal/identifier-token-casei` | 7.125 us | 5.901 us | 17.442 us | 1.21x |
| `lokad/langserv/color-rgb-hex` | 2.072 us | 1.949 us | 2.084 us | 1.06x |
| `literal-family/type-token-family` | 12.105 us | 11.478 us | 22.415 us | 1.05x |
| `lokad/style/cell-ref` | 1.924 us | 1.883 us | 2.210 us | 1.02x |
| `structural/method-family-call` | 4.834 us | 5.061 us | 15.872 us | 0.96x |
| `structural/method-family-generic-call` | 5.459 us | 5.739 us | 18.569 us | 0.95x |
| `literal-family/method-token-family` | 4.712 us | 5.264 us | 15.853 us | 0.90x |
| `literal/identifier-token` | 4.443 us | 5.680 us | 18.570 us | 0.78x |
| `structural/ordered-keyword-window` | 9.095 us | 11.969 us | 25.736 us | 0.76x |
| `lokad/style/range-ref` | 1.206 us | 1.633 us | 1.722 us | 0.74x |
| `structural/ordered-modifier-pair` | 6.257 us | 8.731 us | 19.726 us | 0.72x |
| `lokad/lexer/string` | 29.524 us | 45.695 us | 48.715 us | 0.65x |
| `literal/call-token` | 3.085 us | 4.814 us | 17.510 us | 0.64x |
| `structural/ordered-identifier-window` | 6.464 us | 10.490 us | 20.927 us | 0.62x |
| `lokad/langserv/url-dashboard` | 2.929 us | 7.406 us | 7.888 us | 0.40x |
| `lokad/langserv/url-download` | 2.825 us | 13.098 us | 13.940 us | 0.22x |
| `lokad/folding/region-marker` | 7.334 us | 36.916 us | 82.012 us | 0.20x |
| `balancing/begin-end-blocks` | 9.584 us | 767.037 us | 776.319 us | 0.01x |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
