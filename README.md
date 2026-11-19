# Lokad.Utf8Regex

Regular expressions directly over UTF-8 spans for **.NET 10 and .NET 11**. Match, count,
enumerate and replace without first decoding the subject to a string.
Core and the optional adapters are fully managed .NET libraries.
This README describes ongoing 0.5.0 development. Its API and execution machinery
are being simplified. Published 0.4.0 and frozen development packages retain
their original behavior; development package qualification is not a release.

## Quick start

```csharp
using Lokad.Utf8Regex;
using System.Text.RegularExpressions;

var regex = new Utf8Regex(@"[A-Z]{2}\d{4}", RegexOptions.CultureInvariant);
ReadOnlySpan<byte> input = "Order AB1234 is ready."u8;

bool found = regex.IsMatch(input);              // true
int count = regex.Count(input);                 // 1
Utf8CaptureRange first = regex.Match(input, 0, []); // bytes [6, 12)
byte[] redacted = regex.Replace(input, "[order]"u8);
```

Use `EnumerateMatches` to visit whole matches and `CreateMatchCursor` over
`ReadOnlyMemory<byte>` for groups and capture history. Match byte ranges can be used directly with UTF-8
slices. `EnumerateMatches(input, startOffsetInBytes, cancellationToken)` searches
from a scalar-aligned byte position while retaining full context for assertions.
The byte-start `Match` can fill caller-owned `Utf8CaptureRange` slots; slot zero
is the whole match and `GroupNumberFromCaptureSlot` resolves sparse group numbers.
Use the persistent memory cursor when capture history is required.
Copy a slot's name into caller storage with `TryCopyGroupNameUtf8`; metadata
collections and text decoding belong to the host.

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

All four profiles consume already-valid UTF-8 text. Match and capture results
expose byte coordinates only; callers derive character positions or strings.
Keep borrowed input unchanged and its owner alive while a view or cursor uses it.
Owned snapshots remain valid after cursor advance or disposal.

## Subject input contract

Validate external bytes where they become text, using
`System.Text.Unicode.Utf8.IsValid` when validity is not already guaranteed by the
host's text representation. Regex operations do not perform a separate subject
validity pass and expose no validation policy or token.

This applies to matching, counting, replacements, splits and cursors in Core,
PythonRe, PCRE2 and POSIX. Malformed subjects have unspecified matching results;
managed bounds checking, cancellation and enforceable resource controls remain.
Pattern, replacement-template and assembled-output checks remain independent.
Live route eligibility, scalar decoding and output sizing may still inspect text.
See the [input contract](docs/SPEC.md#subject-input-contract).

## Cancellation and limits

Instances accept cancellation tokens, timeouts and optional work, depth, working-
capacity and result-capacity limits:

```csharp
var bounded = new Utf8Regex(
    @"(?<word>\w+)", RegexOptions.CultureInvariant,
    policy: new Utf8RegexPolicy
    {
        MatchTimeout = TimeSpan.FromSeconds(1),
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 256,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
        }
    });

using var cancellation = new CancellationTokenSource();
int count = bounded.Count("café tea"u8, cancellation.Token); // 2
```

Core operations have optional cancellation parameters. IsMatch/Count also accept
`workingMemoryAllowance:` for shared temporary-storage admission. Omitting it or
passing null keeps numeric limits and inherits program funding when selected;
a refused non-null account fails
without retry. For span enumeration, name the token `cancellationToken:`.

These caps are examples. Defaults have unlimited invocation
budgets. Byte limits cover modeled engine/result capacities, not total process
memory. Constructors and `Compile` accept immutable `Utf8RegexPolicy` construction
controls and optional program funding. Dispose funded expressions. Span enumerators
borrow an open regex; persistent cursors can retain admission after it closes.
See the [resource contract](docs/SPEC.md#cancellation-deadlines-and-resource-limits)
for failure, deadline and ownership details.

See [release notes](CHANGELOG.md) and the [migration guide](docs/MIGRATION.md)
for the 0.4.0 changes.

## Runtime support

The 0.5.0 release candidate targets `net10.0` and `net11.0`. .NET 11
uses the stable SDK pinned in [global.json](global.json) and is the
primary benchmark baseline. .NET 10 remains a compatibility target, with both
runtimes checked in Windows/Linux CI. Published 0.4.0 packages target .NET 10.

## Benchmarks

The **18 November 2026** refresh measured source `654d5716` on **.NET 11.0.0**,
Ubuntu 24.04.4 x64 and an Intel Xeon Platinum 8573C. The Release build, .NET 10/11
tests, all-profile catalogs, paired comparisons, lifecycle, scaling and resource
checks completed and verified in **52m17s**.

The measured operations start with valid, unchanged UTF-8 and include the required
result materialization. [The Core summary](bench/Snapshots/Core.json) records each
row's source, qualification and snapshot identity. [Generality measurements](docs/BENCHMARKS.md)
cover ordinary patterns and calls with finite limits, deadlines and cancellation.

Ordinary and compiled rows use matching .NET compilation flags. All tables rank
`Utf8Regex / .NET predecoded` ratios from highest to lowest; lower is better.

The `common/ip-miss` row was refreshed separately on `edf04445` with the
corrected miss input.

[refresh-benchmarks.ps1](tools/refresh-benchmarks.ps1) runs the full cycle or selected
phases; [bench.ps1](tools/bench.ps1) supports focused single-case iterations.

Focused Core counts use the caller-valid text contract, for example
`--measure-readme-public-case industry/mariomka-email-count 1 9`.
These measurements label that contract and leave recorded tables unchanged.

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
| `literal/sherlock-zh` | 21.984 us | 18.427 us | 2,130.109 us | 1.19x |
| `literal/sherlock-ru` | 68.466 us | 96.178 us | 5,266.893 us | 0.71x |
| `aws-keys/full` | 60,206.400 us | 84,730.200 us | 113,738.700 us | 0.71x |
| `literal-alternate/sherlock-zh` | 41.020 us | 65.706 us | 2,123.620 us | 0.62x |
| `literal/sherlock-casei-en` | 101.084 us | 180.790 us | 584.396 us | 0.56x |
| `aws-keys/quick` | 4,944.980 us | 9,348.325 us | 44,240.500 us | 0.53x |
| `ruff-noqa/tweaked` | 3,640.076 us | 7,305.364 us | 37,057.150 us | 0.50x |
| `literal/sherlock-casei-ru` | 149.773 us | 351.498 us | 5,528.436 us | 0.43x |
| `bounded-repeat/letters-en` | 1,056.307 us | 2,690.161 us | 2,821.226 us | 0.39x |
| `literal/sherlock-en` | 30.552 us | 83.632 us | 477.007 us | 0.37x |
| `bounded-repeat/letters-ru` | 1,890.277 us | 5,530.379 us | 6,384.925 us | 0.34x |
| `dictionary/single` | 7,841.430 us | 24,785.525 us | 24,872.200 us | 0.32x |
| `literal-alternate/sherlock-ru` | 1,371.304 us | 4,511.876 us | 9,424.150 us | 0.30x |
| `literal-alternate/sherlock-en-mixed` | 152.182 us | 569.890 us | 991.998 us | 0.27x |
| `literal-alternate/sherlock-en` | 349.404 us | 1,493.231 us | 2,004.847 us | 0.23x |
| `bounded-repeat/capitals` | 8,673.056 us | 42,568.850 us | 46,276.800 us | 0.20x |
| `literal-alternate/sherlock-en-nomatch` | 123.325 us | 637.595 us | 1,064.867 us | 0.19x |
| `bounded-repeat/context` | 21,179.025 us | 141,017.200 us | 145,075.100 us | 0.15x |
| `ruff-noqa/real` | 3,143.438 us | 66,956.300 us | 97,357.800 us | 0.05x |
| `literal-alternate/sherlock-casei-ru` | 820.246 us | 22,156.875 us | 27,397.900 us | 0.04x |
| `literal-alternate/sherlock-casei-en` | 129.250 us | 11,187.729 us | 12,083.200 us | 0.01x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `industry/boostdocs-float-match` | `IsMatch` | 0.316 us | 0.073 us | 0.086 us | 4.32x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.384 us | 0.103 us | 0.139 us | 3.72x |
| `common/date-match` | `IsMatch` | 0.379 us | 0.104 us | 0.129 us | 3.66x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.276 us | 0.077 us | 0.092 us | 3.59x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.275 us | 0.079 us | 0.095 us | 3.46x |
| `common/ip-match` | `IsMatch` | 0.581 us | 0.171 us | 0.189 us | 3.41x |
| `common/date-miss` | `IsMatch` | 1.724 us | 0.526 us | 0.560 us | 3.28x |
| `common/email-match` | `IsMatch` | 0.720 us | 0.256 us | 0.282 us | 2.82x |
| `common/uri-match` | `IsMatch` | 0.577 us | 0.226 us | 0.260 us | 2.55x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.334 us | 0.161 us | 0.178 us | 2.07x |
| `industry/mariomka-uri-count` | `Count` | 3,239.525 us | 1,871.841 us | 10,155.575 us | 1.73x |
| `common/ip-miss` | `IsMatch` | 1.031 us | 0.696 us | 0.716 us | 1.48x |
| `common/uri-miss` | `IsMatch` | 0.181 us | 0.175 us | 0.195 us | 1.03x |
| `common/match-word` | `Match` | 0.153 us | 0.162 us | 1.093 us | 0.94x |
| `industry/mariomka-email-count` | `Count` | 450.262 us | 487.790 us | 8,420.520 us | 0.92x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 137.712 us | 247.526 us | 470.529 us | 0.56x |
| `industry/leipzig-twain-count` | `Count` | 635.650 us | 1,325.878 us | 8,499.244 us | 0.48x |
| `common/matches-set` | `Count` | 58.965 us | 184.956 us | 187.392 us | 0.32x |
| `common/matches-word` | `Count` | 1.206 us | 4.041 us | 5.695 us | 0.30x |
| `common/replace-words` | `Replace` | 21.386 us | 73.305 us | 75.280 us | 0.29x |
| `common/backtracking` | `IsMatch` | 0.235 us | 0.814 us | 0.854 us | 0.29x |
| `industry/mariomka-ip-count` | `Count` | 2,187.414 us | 8,163.830 us | 16,348.500 us | 0.27x |
| `common/split-words` | `Split` | 14.048 us | 73.704 us | 75.094 us | 0.19x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 160.619 us | 908.357 us | 1,185.512 us | 0.18x |
| `common/matches-boundary` | `Count` | 21.713 us | 159.455 us | 162.189 us | 0.14x |
| `common/matches-words` | `Count` | 8.814 us | 69.461 us | 70.789 us | 0.13x |
| `common/email-miss` | `IsMatch` | 0.036 us | 0.327 us | 0.352 us | 0.11x |
| `industry/leipzig-name-family-count` | `Count` | 997.735 us | 9,455.167 us | 16,322.633 us | 0.11x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,851.225 us | 18,525.400 us | 18,972.050 us | 0.10x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,105.777 us | 11,542.443 us | 12,136.643 us | 0.10x |
| `common/one-node-backtracking` | `IsMatch` | 0.044 us | 0.582 us | 0.615 us | 0.08x |
| `industry/leipzig-river-window-count` | `Count` | 1,707.229 us | 30,486.333 us | 37,922.000 us | 0.06x |
| `industry/leipzig-symbol-count` | `Count` | 972.016 us | 21,715.850 us | 28,712.500 us | 0.04x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 309.596 us | 8,051.420 us | 8,492.578 us | 0.04x |
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
| `aws-keys/full` | 59,490.900 us | 17,620.280 us | 48,850.133 us | 3.38x |
| `aws-keys/quick` | 5,025.188 us | 3,473.059 us | 36,103.200 us | 1.45x |
| `literal/sherlock-casei-en` | 101.204 us | 76.224 us | 427.927 us | 1.33x |
| `literal-alternate/sherlock-zh` | 41.040 us | 33.665 us | 2,078.406 us | 1.22x |
| `literal-alternate/sherlock-en` | 348.313 us | 337.299 us | 745.385 us | 1.03x |
| `literal/sherlock-zh` | 21.944 us | 23.490 us | 2,120.234 us | 0.93x |
| `bounded-repeat/letters-ru` | 1,878.578 us | 2,035.095 us | 2,871.656 us | 0.92x |
| `literal-alternate/sherlock-casei-en` | 128.144 us | 142.174 us | 585.129 us | 0.90x |
| `literal-alternate/sherlock-en-mixed` | 151.020 us | 169.775 us | 536.845 us | 0.89x |
| `ruff-noqa/tweaked` | 3,681.219 us | 4,331.000 us | 35,856.767 us | 0.85x |
| `literal/sherlock-ru` | 68.500 us | 86.761 us | 5,169.053 us | 0.79x |
| `literal/sherlock-casei-ru` | 149.923 us | 216.145 us | 5,293.329 us | 0.69x |
| `bounded-repeat/capitals` | 8,691.633 us | 12,828.150 us | 15,650.800 us | 0.68x |
| `literal-alternate/sherlock-en-nomatch` | 122.828 us | 191.869 us | 567.234 us | 0.64x |
| `literal-alternate/sherlock-ru` | 1,374.221 us | 2,242.250 us | 7,126.050 us | 0.61x |
| `bounded-repeat/letters-en` | 1,055.025 us | 1,761.984 us | 1,875.408 us | 0.60x |
| `literal-alternate/sherlock-casei-ru` | 849.864 us | 1,877.650 us | 6,704.000 us | 0.45x |
| `literal/sherlock-en` | 30.683 us | 73.692 us | 407.437 us | 0.42x |
| `bounded-repeat/context` | 21,169.275 us | 65,884.450 us | 68,573.800 us | 0.32x |
| `dictionary/single` | 7,930.450 us | 43,882.100 us | 44,058.900 us | 0.18x |
| `ruff-noqa/real` | 3,156.624 us | 20,091.825 us | 50,447.567 us | 0.16x |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled elapsed | .NET compiled predecoded elapsed | .NET compiled + decode elapsed | Ratio (lower is better) |
|---|---|---:|---:|---:|---:|
| `common/ip-match` | `IsMatch` | 0.579 us | 0.046 us | 0.065 us | 12.46x |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.277 us | 0.029 us | 0.045 us | 9.40x |
| `common/email-match` | `IsMatch` | 0.723 us | 0.078 us | 0.103 us | 9.29x |
| `industry/boostdocs-float-match` | `IsMatch` | 0.316 us | 0.034 us | 0.051 us | 9.28x |
| `common/date-miss` | `IsMatch` | 1.734 us | 0.189 us | 0.225 us | 9.18x |
| `common/date-match` | `IsMatch` | 0.381 us | 0.042 us | 0.069 us | 9.17x |
| `industry/boostdocs-date-match` | `IsMatch` | 0.274 us | 0.030 us | 0.048 us | 9.04x |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.387 us | 0.046 us | 0.083 us | 8.44x |
| `common/uri-match` | `IsMatch` | 0.574 us | 0.084 us | 0.116 us | 6.83x |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.323 us | 0.054 us | 0.073 us | 6.04x |
| `common/ip-miss` | `IsMatch` | 1.024 us | 0.171 us | 0.196 us | 6.00x |
| `common/backtracking` | `IsMatch` | 0.230 us | 0.041 us | 0.071 us | 5.63x |
| `industry/mariomka-uri-count` | `Count` | 3,216.167 us | 1,070.186 us | 8,153.925 us | 3.01x |
| `common/uri-miss` | `IsMatch` | 0.181 us | 0.063 us | 0.083 us | 2.86x |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 138.150 us | 55.615 us | 255.819 us | 2.48x |
| `common/match-word` | `Match` | 0.151 us | 0.078 us | 0.955 us | 1.95x |
| `common/replace-words` | `Replace` | 21.482 us | 16.957 us | 20.059 us | 1.27x |
| `industry/leipzig-river-window-count` | `Count` | 1,713.220 us | 1,498.757 us | 7,833.020 us | 1.14x |
| `industry/mariomka-email-count` | `Count` | 466.191 us | 458.059 us | 7,715.900 us | 1.02x |
| `industry/leipzig-symbol-count` | `Count` | 974.286 us | 1,177.105 us | 6,982.120 us | 0.83x |
| `common/matches-words` | `Count` | 8.733 us | 10.701 us | 13.717 us | 0.82x |
| `common/split-words` | `Split` | 13.952 us | 19.775 us | 21.765 us | 0.71x |
| `industry/mariomka-ip-count` | `Count` | 2,196.229 us | 3,342.604 us | 10,453.300 us | 0.66x |
| `common/matches-set` | `Count` | 58.981 us | 99.639 us | 102.422 us | 0.59x |
| `common/one-node-backtracking` | `IsMatch` | 0.045 us | 0.083 us | 0.122 us | 0.54x |
| `common/matches-word` | `Count` | 1.200 us | 2.269 us | 3.396 us | 0.53x |
| `industry/leipzig-twain-count` | `Count` | 633.647 us | 1,285.927 us | 7,407.110 us | 0.49x |
| `common/email-miss` | `IsMatch` | 0.037 us | 0.086 us | 0.111 us | 0.43x |
| `industry/leipzig-name-family-count` | `Count` | 1,008.476 us | 2,915.719 us | 7,940.175 us | 0.35x |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 163.205 us | 579.832 us | 824.848 us | 0.28x |
| `industry/rust-sherlock-ing-count` | `Count` | 1,111.615 us | 4,564.800 us | 4,996.069 us | 0.24x |
| `industry/rust-sherlock-letter-count` | `Count` | 1,851.839 us | 8,319.240 us | 8,723.322 us | 0.22x |
| `common/matches-boundary` | `Count` | 21.866 us | 99.195 us | 100.875 us | 0.22x |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 311.220 us | 3,790.025 us | 4,141.874 us | 0.08x |
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
| `lokad/docs/inline-doc-prefix` | 13.114 us | 4.612 us | 5.187 us | 2.84x |
| `lokad/style/cell-ref` | 7.383 us | 2.640 us | 2.865 us | 2.80x |
| `lokad/lexer/string` | 237.185 us | 89.695 us | 94.749 us | 2.64x |
| `lokad/style/range-ref` | 6.317 us | 2.485 us | 2.640 us | 2.54x |
| `lokad/langserv/identifier-validator` | 29.541 us | 11.973 us | 13.037 us | 2.47x |
| `structural/ordered-modifier-pair` | 39.990 us | 16.241 us | 28.926 us | 2.46x |
| `structural/method-family-generic-call` | 14.948 us | 6.202 us | 19.084 us | 2.41x |
| `lokad/lexer/doc-line` | 6.701 us | 2.874 us | 3.426 us | 2.33x |
| `lokad/lexer/number` | 30.972 us | 13.511 us | 14.425 us | 2.29x |
| `lokad/langserv/color-short-hex` | 2.960 us | 1.303 us | 1.358 us | 2.27x |
| `lokad/lexer/identifier` | 48.109 us | 22.172 us | 25.142 us | 2.17x |
| `lokad/langserv/helper-identifier` | 14.979 us | 6.984 us | 7.435 us | 2.14x |
| `lokad/style/hex-color` | 1.647 us | 0.802 us | 0.849 us | 2.05x |
| `structural/method-family-call` | 15.536 us | 7.599 us | 20.206 us | 2.04x |
| `lokad/lexer/operator-run` | 6.781 us | 3.439 us | 3.612 us | 1.97x |
| `fallback/lookahead` | 11.205 us | 5.697 us | 17.972 us | 1.97x |
| `lokad/langserv/color-rgb-hex` | 4.791 us | 2.824 us | 2.809 us | 1.70x |
| `structural/ordered-keyword-pair` | 8.291 us | 5.045 us | 18.436 us | 1.64x |
| `lokad/imports/module-imports` | 8.415 us | 5.990 us | 29.452 us | 1.40x |
| `structural/keyword-to-generic-type` | 5.094 us | 3.898 us | 17.482 us | 1.31x |
| `lokad/langserv/url-dashboard` | 29.279 us | 22.994 us | 23.711 us | 1.27x |
| `structural/ordered-keyword-window` | 19.468 us | 16.886 us | 29.785 us | 1.15x |
| `lokad/langserv/url-download` | 49.166 us | 50.398 us | 51.084 us | 0.98x |
| `literal/identifier-token` | 2.615 us | 4.111 us | 16.678 us | 0.64x |
| `structural/ordered-identifier-window` | 14.320 us | 27.287 us | 40.465 us | 0.52x |
| `structural/modifier-family-to-type-window` | 118.877 us | 241.427 us | 255.307 us | 0.49x |
| `structural/modifier-family-to-type-pair` | 58.720 us | 123.144 us | 136.007 us | 0.48x |
| `literal/call-token` | 1.850 us | 4.058 us | 16.919 us | 0.46x |
| `literal-family/method-token-family` | 3.666 us | 8.715 us | 21.395 us | 0.42x |
| `literal-family/type-token-family` | 9.419 us | 22.542 us | 35.740 us | 0.42x |
| `balancing/begin-end-blocks` | 1,466.358 us | 4,114.600 us | 4,124.500 us | 0.36x |
| `structural/keyword-family-to-capitalized-identifier` | 62.315 us | 205.474 us | 218.253 us | 0.30x |
| `literal/identifier-token-casei` | 3.421 us | 14.238 us | 27.707 us | 0.24x |
| `lokad/folding/region-marker` | 4.754 us | 33.842 us | 61.542 us | 0.14x |
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
| `structural/keyword-family-to-capitalized-identifier` | 62.323 us | 7.260 us | 19.817 us | 8.58x |
| `structural/modifier-family-to-type-pair` | 58.981 us | 10.304 us | 22.742 us | 5.72x |
| `lokad/lexer/string` | 232.983 us | 43.699 us | 46.017 us | 5.33x |
| `lokad/docs/inline-doc-prefix` | 13.098 us | 2.636 us | 3.368 us | 4.97x |
| `structural/ordered-modifier-pair` | 39.860 us | 8.215 us | 20.655 us | 4.85x |
| `lokad/style/range-ref` | 6.333 us | 1.352 us | 1.461 us | 4.68x |
| `lokad/style/cell-ref` | 7.417 us | 1.614 us | 1.811 us | 4.59x |
| `lokad/lexer/number` | 30.835 us | 6.764 us | 7.865 us | 4.56x |
| `lokad/langserv/url-dashboard` | 29.266 us | 7.063 us | 7.638 us | 4.14x |
| `lokad/langserv/url-download` | 49.263 us | 12.362 us | 12.917 us | 3.99x |
| `lokad/lexer/operator-run` | 6.763 us | 1.875 us | 2.379 us | 3.61x |
| `lokad/langserv/identifier-validator` | 29.741 us | 8.375 us | 10.423 us | 3.55x |
| `lokad/langserv/helper-identifier` | 15.047 us | 4.427 us | 5.432 us | 3.40x |
| `structural/method-family-generic-call` | 14.862 us | 4.392 us | 16.381 us | 3.38x |
| `balancing/begin-end-blocks` | 1,462.102 us | 441.502 us | 471.297 us | 3.31x |
| `lokad/lexer/doc-line` | 6.745 us | 2.041 us | 2.735 us | 3.31x |
| `lokad/style/hex-color` | 1.636 us | 0.495 us | 0.567 us | 3.30x |
| `lokad/langserv/color-short-hex` | 2.966 us | 0.908 us | 1.069 us | 3.27x |
| `structural/method-family-call` | 15.546 us | 4.832 us | 16.901 us | 3.22x |
| `lokad/lexer/identifier` | 47.620 us | 16.142 us | 19.875 us | 2.95x |
| `fallback/lookahead` | 11.181 us | 3.989 us | 15.859 us | 2.80x |
| `lokad/langserv/color-rgb-hex` | 4.777 us | 1.725 us | 1.947 us | 2.77x |
| `structural/ordered-keyword-window` | 19.425 us | 8.178 us | 20.921 us | 2.38x |
| `structural/modifier-family-to-type-window` | 118.853 us | 53.159 us | 67.147 us | 2.24x |
| `structural/ordered-keyword-pair` | 8.289 us | 3.936 us | 16.611 us | 2.11x |
| `lokad/imports/module-imports` | 8.366 us | 4.218 us | 27.069 us | 1.98x |
| `structural/keyword-to-generic-type` | 5.093 us | 3.280 us | 16.369 us | 1.55x |
| `structural/ordered-identifier-window` | 14.288 us | 10.034 us | 23.031 us | 1.42x |
| `literal-family/type-token-family` | 9.408 us | 10.938 us | 23.592 us | 0.86x |
| `literal-family/method-token-family` | 3.642 us | 5.072 us | 17.450 us | 0.72x |
| `literal/identifier-token-casei` | 3.410 us | 5.663 us | 18.317 us | 0.60x |
| `literal/identifier-token` | 2.628 us | 4.496 us | 16.130 us | 0.58x |
| `literal/call-token` | 1.845 us | 3.749 us | 15.755 us | 0.49x |
| `lokad/folding/region-marker` | 4.795 us | 35.110 us | 60.806 us | 0.14x |
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
