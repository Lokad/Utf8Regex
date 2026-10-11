# Lokad.Utf8Regex

`Lokad.Utf8Regex` is a `net10.0` regular expression library with UTF-8 `ReadOnlySpan<byte>` / `Span<byte>` as its primary I/O surface. The development target is Unicode-scalar matching with primary byte coordinates: a supplementary character such as 😀 is one dot match.

The 0.4.0-scalar.1 prerelease uses Unicode-scalar matching. Published 0.3.0 packages retain the former .NET UTF-16 contract. The current core and adapters use owned execution; production BCL matching and result fallbacks and the private UTF-16-unit engine have been removed. [SPEC.md](SPEC.md) defines the target and its qualification requirements. Common resource policies, retained/result bounds, remaining preparation cleanup and complete acceptance remain unfinished. This prerelease is an experimental integration candidate.

Use a qualified candidate's exact version and immutable feed/manifest for reproducible integration. [qualify-scalar-candidate.ps1](qualify-scalar-candidate.ps1) builds and packs once, checks provenance, runs detached consumers and records package hashes; it does not publish to NuGet.org. Existing benchmark snapshots describe their recorded implementations and qualify scalar comparisons only where results agree.

Run `./qualify-scalar-candidate.ps1` from a clean committed checkout. Successful output is designated once under `artifacts/scalar-candidates/<version>/`, with `candidate.json`, a `feed/` directory and a reusable `NuGet.Config`. Consumers should pin the exact version, restore with that config and an isolated cache, and retain their package lockfile. A changed candidate requires a new version.

It is intended for workloads where the input already exists as UTF-8 bytes and you want to avoid decoding to UTF-16 before every regex operation.

```powershell
dotnet add package Lokad.Utf8Regex
dotnet add package Lokad.Utf8Regex.Pcre2
dotnet add package Lokad.Utf8Regex.PythonRe
```

The [PCRE2 support](src/Lokad.Utf8Regex.Pcre2/README.md) is optional ([benchmarks](src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md)).
The [Python `re` support](src/Lokad.Utf8Regex.PythonRe/README.md) is optional ([benchmarks](src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md)).
Release notes are tracked in [CHANGELOG.md](CHANGELOG.md).

## Support Scope

- Development semantics: Unicode-scalar matching over strict UTF-8. Dot, classes, quantifiers, capture boundaries, references and RTL traversal operate on complete scalars.
- Primary I/O model: UTF-8 `ReadOnlySpan<byte>` / `Span<byte>` with primary byte coordinates. Existing UTF-16 coordinate/string APIs provide interoperability and do not define matching width.
- Primary option scope: `RegexOptions.CultureInvariant` is the main supported and performance-tested mode.
- Execution model: owned scalar execution with qualified native UTF-8 fast paths. BCL Regex is a comparator in tests and benchmarks where its code-unit semantics overlap.
- Byte coordinates: successful core matches and captures use scalar-aligned UTF-8 byte ranges.
- Comparators: use .NET Regex only where its behavior overlaps the scalar contract; direct scalar fixtures define supplementary behavior.
- Resource scope: core instances accept a finite `Utf8ResourcePolicy.MaxWorkSteps`. One cumulative meter covers strict UTF-8 validation, scalar matching and required result shaping, including lazy advances and replacement output. Finite calls select owned scalar execution. Depth, working-byte and result-byte caps remain unsupported; defaults and static cached calls remain unlimited. Construction/replacement preparation, caller callback execution and later standalone result getters have separate scope. Work units are engine/checkpoint steps, not bytes, time or process memory.
- Stability: 0.4.0-scalar.1 is an experimental candidate. Full resource, cancellation, adapter and performance qualification remains open.

## Public API

```csharp
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;

var regex = new Utf8Regex(
    pattern: @"AKIA[0-9A-Z]{16}",
    options: RegexOptions.CultureInvariant);

ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes("""
    const key = "AKIA1234567890ABCDEF";
    """);

bool isMatch = regex.IsMatch(input);
int count = regex.Count(input);

Utf8ValueMatch first = regex.Match(input);
if (first.Success && first.IsByteAligned)
{
    Console.WriteLine($"First match at byte {first.IndexInBytes}, length {first.LengthInBytes}");
}

foreach (var match in regex.EnumerateMatches(input))
{
    if (!match.IsByteAligned)
    {
        continue;
    }

    var slice = input.Slice(match.IndexInBytes, match.LengthInBytes);
    Console.WriteLine(Encoding.UTF8.GetString(slice));
}

byte[] redacted = regex.Replace(input, "***REDACTED***");
Console.WriteLine(Encoding.UTF8.GetString(redacted));
```

Notes:
- Inputs must be well-formed UTF-8.
- The library also exposes static helpers such as `Utf8Regex.IsMatch(...)`, `Utf8Regex.Count(...)`, `Utf8Regex.Match(...)`, `Utf8Regex.EnumerateMatches(...)`, and `Utf8Regex.Replace(...)`.
- Match results expose primary byte coordinates and derived UTF-16 coordinates via `Utf8ValueMatch`.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary CPU parity comparator. `.NET + decode` is a secondary end-to-end indicator.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---:|---:|---:|
| `literal/sherlock-en` | 58.350 us | 84.315 us | 523.080 us |
| `literal/sherlock-casei-en` | 88.400 us | 185.005 us | 628.530 us |
| `literal/sherlock-ru` | 219.465 us | 94.520 us | 4,917.695 us |
| `literal/sherlock-casei-ru` | 238.545 us | 396.730 us | 5,303.930 us |
| `literal/sherlock-zh` | 118.745 us | 18.685 us | 1,955.735 us |
| `literal-alternate/sherlock-en` | 323.090 us | 1,514.050 us | 1,998.180 us |
| `literal-alternate/sherlock-en-nomatch` | 149.005 us | 662.220 us | 1,142.080 us |
| `literal-alternate/sherlock-en-mixed` | 188.850 us | 647.115 us | 1,104.515 us |
| `literal-alternate/sherlock-casei-en` | 168.235 us | 11,280.650 us | 11,823.090 us |
| `literal-alternate/sherlock-ru` | 1,493.130 us | 5,070.395 us | 9,990.875 us |
| `literal-alternate/sherlock-casei-ru` | 1,008.120 us | 25,009.200 us | 29,936.545 us |
| `literal-alternate/sherlock-zh` | 211.965 us | 70.140 us | 2,001.000 us |
| `bounded-repeat/letters-en` | 715.585 us | 2,695.990 us | 2,773.080 us |
| `bounded-repeat/letters-ru` | 962.075 us | 6,106.345 us | 6,858.190 us |
| `bounded-repeat/context` | 27,134.150 us | 144,079.410 us | 147,573.635 us |
| `bounded-repeat/capitals` | 227.185 us | 42,791.630 us | 45,949.935 us |
| `dictionary/single` | 7,794.570 us | 26,416.150 us | 26,364.440 us |
| `aws-keys/full` | 994.190 us | 84,502.835 us | 99,492.795 us |
| `aws-keys/quick` | 3,463.965 us | 9,187.530 us | 23,541.635 us |
| `ruff-noqa/real` | 2,078.350 us | 66,548.420 us | 81,604.930 us |
| `ruff-noqa/tweaked` | 3,821.310 us | 7,195.250 us | 21,762.130 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.042 us | 0.260 us | 0.283 us |
| `common/email-miss` | `IsMatch` | 0.053 us | 0.350 us | 0.379 us |
| `common/date-match` | `IsMatch` | 0.356 us | 0.208 us | 0.322 us |
| `common/date-miss` | `IsMatch` | 0.084 us | 0.561 us | 0.599 us |
| `common/ip-match` | `IsMatch` | 0.015 us | 0.168 us | 0.186 us |
| `common/ip-miss` | `IsMatch` | 0.015 us | 0.180 us | 0.196 us |
| `common/uri-match` | `IsMatch` | 0.023 us | 0.222 us | 0.257 us |
| `common/uri-miss` | `IsMatch` | 0.085 us | 0.189 us | 0.204 us |
| `common/matches-set` | `Count` | 8.850 us | 191.220 us | 191.289 us |
| `common/matches-boundary` | `Count` | 7.484 us | 162.805 us | 164.332 us |
| `common/matches-word` | `Count` | 1.377 us | 4.001 us | 5.334 us |
| `common/matches-words` | `Count` | 9.440 us | 72.994 us | 74.046 us |
| `common/match-word` | `Match` | 0.144 us | 0.168 us | 1.068 us |
| `common/replace-words` | `Replace` | 41.401 us | 79.762 us | 81.285 us |
| `common/split-words` | `Split` | 11.342 us | 79.671 us | 81.178 us |
| `common/backtracking` | `IsMatch` | 0.067 us | 0.926 us | 0.960 us |
| `common/one-node-backtracking` | `IsMatch` | 0.186 us | 0.618 us | 0.657 us |
| `industry/mariomka-email-count` | `Count` | 1,372.405 us | 471.750 us | 7,838.960 us |
| `industry/mariomka-uri-count` | `Count` | 1,823.205 us | 1,808.095 us | 9,537.320 us |
| `industry/mariomka-ip-count` | `Count` | 2,431.670 us | 7,971.680 us | 15,730.065 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,786.050 us | 18,116.345 us | 18,560.065 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.609 us | 270.243 us | 480.288 us |
| `industry/rust-sherlock-ing-count` | `Count` | 244.455 us | 11,825.665 us | 12,176.000 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 26.060 us | 7,848.365 us | 8,207.430 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 184.582 us | 914.451 us | 1,223.935 us |
| `industry/leipzig-twain-count` | `Count` | 1,142.800 us | 1,219.955 us | 7,665.445 us |
| `industry/leipzig-name-family-count` | `Count` | 2,916.040 us | 9,541.705 us | 16,195.670 us |
| `industry/leipzig-river-window-count` | `Count` | 10,823.390 us | 31,599.510 us | 38,578.735 us |
| `industry/leipzig-symbol-count` | `Count` | 1,991.815 us | 20,267.130 us | 27,102.850 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.115 us | 0.426 us | 0.547 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.336 us | 0.160 us | 0.174 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.078 us | 0.114 us | 0.141 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.096 us | 0.115 us | 0.142 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.038 us | 0.104 us | 0.134 us |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary CPU parity comparator. `.NET + decode` is a secondary end-to-end indicator.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---:|---:|---:|
| `literal/sherlock-en` | 59.285 us | 75.240 us | 502.085 us |
| `literal/sherlock-casei-en` | 90.770 us | 75.365 us | 514.820 us |
| `literal/sherlock-ru` | 220.690 us | 86.570 us | 4,923.715 us |
| `literal/sherlock-casei-ru` | 240.415 us | 195.615 us | 5,114.395 us |
| `literal/sherlock-zh` | 119.990 us | 23.380 us | 1,953.330 us |
| `literal-alternate/sherlock-en` | 319.335 us | 334.160 us | 803.360 us |
| `literal-alternate/sherlock-en-nomatch` | 151.835 us | 201.625 us | 700.615 us |
| `literal-alternate/sherlock-en-mixed` | 191.415 us | 172.000 us | 632.995 us |
| `literal-alternate/sherlock-casei-en` | 178.610 us | 156.895 us | 640.135 us |
| `literal-alternate/sherlock-ru` | 1,492.070 us | 2,262.510 us | 7,102.000 us |
| `literal-alternate/sherlock-casei-ru` | 1,000.535 us | 1,886.800 us | 6,727.750 us |
| `literal-alternate/sherlock-zh` | 209.700 us | 39.880 us | 1,978.890 us |
| `bounded-repeat/letters-en` | 699.605 us | 1,704.845 us | 1,794.765 us |
| `bounded-repeat/letters-ru` | 959.350 us | 1,877.150 us | 2,650.195 us |
| `bounded-repeat/context` | 27,559.025 us | 62,342.640 us | 65,500.120 us |
| `bounded-repeat/capitals` | 230.350 us | 12,773.860 us | 15,371.930 us |
| `dictionary/single` | 7,781.810 us | 72,672.750 us | 72,754.080 us |
| `aws-keys/full` | 1,014.720 us | 17,055.300 us | 31,256.470 us |
| `aws-keys/quick` | 3,600.965 us | 3,456.485 us | 17,941.630 us |
| `ruff-noqa/real` | 2,092.655 us | 18,852.560 us | 33,401.680 us |
| `ruff-noqa/tweaked` | 3,809.075 us | 4,334.945 us | 19,020.320 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.044 us | 0.079 us | 0.111 us |
| `common/email-miss` | `IsMatch` | 0.056 us | 0.090 us | 0.118 us |
| `common/date-match` | `IsMatch` | 0.354 us | 0.048 us | 0.140 us |
| `common/date-miss` | `IsMatch` | 0.085 us | 0.192 us | 0.226 us |
| `common/ip-match` | `IsMatch` | 0.015 us | 0.050 us | 0.069 us |
| `common/ip-miss` | `IsMatch` | 0.016 us | 0.050 us | 0.070 us |
| `common/uri-match` | `IsMatch` | 0.023 us | 0.086 us | 0.116 us |
| `common/uri-miss` | `IsMatch` | 0.090 us | 0.069 us | 0.081 us |
| `common/matches-set` | `Count` | 8.809 us | 96.917 us | 98.811 us |
| `common/matches-boundary` | `Count` | 7.809 us | 96.114 us | 97.343 us |
| `common/matches-word` | `Count` | 1.375 us | 2.180 us | 3.247 us |
| `common/matches-words` | `Count` | 9.414 us | 12.846 us | 13.939 us |
| `common/match-word` | `Match` | 0.138 us | 0.075 us | 0.975 us |
| `common/replace-words` | `Replace` | 41.251 us | 17.387 us | 18.545 us |
| `common/split-words` | `Split` | 11.384 us | 18.542 us | 19.741 us |
| `common/backtracking` | `IsMatch` | 0.070 us | 0.045 us | 0.071 us |
| `common/one-node-backtracking` | `IsMatch` | 0.194 us | 0.088 us | 0.129 us |
| `industry/mariomka-email-count` | `Count` | 1,370.805 us | 435.905 us | 7,447.575 us |
| `industry/mariomka-uri-count` | `Count` | 1,827.390 us | 1,048.145 us | 8,594.335 us |
| `industry/mariomka-ip-count` | `Count` | 2,442.660 us | 3,304.415 us | 10,594.780 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,776.235 us | 8,179.070 us | 8,710.415 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.580 us | 60.914 us | 264.197 us |
| `industry/rust-sherlock-ing-count` | `Count` | 245.540 us | 4,427.060 us | 4,725.720 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 24.660 us | 3,704.960 us | 4,052.085 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 185.644 us | 574.575 us | 855.025 us |
| `industry/leipzig-twain-count` | `Count` | 1,163.025 us | 1,272.055 us | 7,041.965 us |
| `industry/leipzig-name-family-count` | `Count` | 2,944.305 us | 2,725.505 us | 8,764.415 us |
| `industry/leipzig-river-window-count` | `Count` | 10,845.340 us | 8,914.110 us | 15,428.355 us |
| `industry/leipzig-symbol-count` | `Count` | 2,034.780 us | 1,263.270 us | 7,343.370 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.121 us | 0.126 us | 0.209 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.046 us | 0.064 us | 0.075 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.038 us | 0.056 us | 0.081 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.048 us | 0.058 us | 0.086 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.041 us | 0.039 us | 0.081 us |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

Predecoded .NET is the primary CPU parity comparator. `.NET + decode` is a secondary end-to-end indicator.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---:|---:|---:|
| `literal/identifier-token` | 2.187 us | 4.012 us | 14.635 us |
| `literal/call-token` | 3.438 us | 3.965 us | 14.557 us |
| `literal/identifier-token-casei` | 5.347 us | 15.496 us | 25.811 us |
| `literal-family/type-token-family` | 10.820 us | 24.953 us | 35.699 us |
| `literal-family/method-token-family` | 8.827 us | 8.966 us | 19.876 us |
| `structural/keyword-to-generic-type` | 4.066 us | 3.841 us | 14.840 us |
| `structural/keyword-family-to-capitalized-identifier` | 76.970 us | 213.901 us | 228.874 us |
| `structural/method-family-call` | 4.235 us | 7.683 us | 18.751 us |
| `structural/ordered-keyword-pair` | 4.186 us | 5.293 us | 16.094 us |
| `structural/ordered-identifier-window` | 4.897 us | 29.340 us | 39.482 us |
| `structural/modifier-family-to-type-window` | 25.148 us | 263.031 us | 276.133 us |
| `structural/ordered-keyword-window` | 5.143 us | 18.339 us | 28.891 us |
| `structural/method-family-generic-call` | 3.835 us | 6.259 us | 17.356 us |
| `fallback/lookahead` | 4.379 us | 5.966 us | 16.432 us |
| `balancing/begin-end-blocks` | 8.980 us | 4,213.635 us | 4,211.590 us |
| `structural/ordered-modifier-pair` | 4.094 us | 16.771 us | 27.233 us |
| `structural/modifier-family-to-type-pair` | 23.209 us | 128.188 us | 138.855 us |
| `lokad/imports/module-imports` | 3.430 us | 4.860 us | 14.280 us |
| `lokad/folding/region-marker` | 5.720 us | 37.445 us | 48.315 us |
| `lokad/lexer/identifier` | 12.179 us | 16.470 us | 20.153 us |
| `lokad/lexer/number` | 4.107 us | 7.046 us | 8.067 us |
| `lokad/lexer/string` | 10.965 us | 42.663 us | 45.789 us |
| `lokad/lexer/operator-run` | 1.407 us | 1.837 us | 2.470 us |
| `lokad/lexer/doc-line` | 1.675 us | 2.008 us | 2.887 us |
| `lokad/docs/inline-doc-prefix` | 1.695 us | 2.587 us | 3.536 us |
| `lokad/langserv/identifier-validator` | 4.061 us | 8.785 us | 10.558 us |
| `lokad/langserv/helper-identifier` | 3.018 us | 4.201 us | 5.533 us |
| `lokad/langserv/color-short-hex` | 0.691 us | 0.894 us | 1.115 us |
| `lokad/langserv/color-rgb-hex` | 0.840 us | 1.750 us | 2.004 us |
| `lokad/langserv/url-dashboard` | 1.415 us | 6.941 us | 7.467 us |
| `lokad/langserv/url-download` | 2.221 us | 12.208 us | 12.681 us |
| `lokad/style/hex-color` | 0.339 us | 0.472 us | 0.560 us |
| `lokad/style/cell-ref` | 0.734 us | 1.734 us | 1.916 us |
| `lokad/style/range-ref` | 0.467 us | 1.390 us | 1.482 us |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

Predecoded .NET is the primary CPU parity comparator. `.NET + decode` is a secondary end-to-end indicator.

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---:|---:|---:|
| `literal/identifier-token` | 2.180 us | 4.751 us | 14.292 us |
| `literal/call-token` | 3.440 us | 3.767 us | 13.758 us |
| `literal/identifier-token-casei` | 5.386 us | 5.517 us | 16.183 us |
| `literal-family/type-token-family` | 10.776 us | 10.844 us | 21.733 us |
| `literal-family/method-token-family` | 2.904 us | 5.112 us | 15.852 us |
| `structural/keyword-to-generic-type` | 4.062 us | 3.310 us | 13.884 us |
| `structural/keyword-family-to-capitalized-identifier` | 77.363 us | 86.135 us | 97.485 us |
| `structural/method-family-call` | 4.248 us | 4.804 us | 15.704 us |
| `structural/ordered-keyword-pair` | 4.200 us | 3.917 us | 14.230 us |
| `structural/ordered-identifier-window` | 4.828 us | 10.298 us | 20.605 us |
| `structural/modifier-family-to-type-window` | 25.057 us | 104.715 us | 117.020 us |
| `structural/ordered-keyword-window` | 5.086 us | 8.477 us | 19.579 us |
| `structural/method-family-generic-call` | 3.826 us | 4.314 us | 14.875 us |
| `fallback/lookahead` | 4.332 us | 3.990 us | 14.137 us |
| `balancing/begin-end-blocks` | 9.205 us | 483.885 us | 493.135 us |
| `structural/ordered-modifier-pair` | 4.094 us | 8.276 us | 18.550 us |
| `structural/modifier-family-to-type-pair` | 23.491 us | 56.601 us | 68.097 us |
| `lokad/imports/module-imports` | 3.455 us | 4.775 us | 14.070 us |
| `lokad/folding/region-marker` | 6.125 us | 38.840 us | 46.440 us |
| `lokad/lexer/identifier` | 12.176 us | 16.303 us | 20.013 us |
| `lokad/lexer/number` | 4.102 us | 6.882 us | 8.127 us |
| `lokad/lexer/string` | 10.881 us | 41.806 us | 45.972 us |
| `lokad/lexer/operator-run` | 1.389 us | 1.830 us | 2.473 us |
| `lokad/lexer/doc-line` | 1.763 us | 2.007 us | 2.879 us |
| `lokad/docs/inline-doc-prefix` | 1.671 us | 2.590 us | 3.495 us |
| `lokad/langserv/identifier-validator` | 4.050 us | 8.486 us | 10.490 us |
| `lokad/langserv/helper-identifier` | 3.122 us | 4.264 us | 5.421 us |
| `lokad/langserv/color-short-hex` | 0.677 us | 0.897 us | 1.098 us |
| `lokad/langserv/color-rgb-hex` | 0.860 us | 1.752 us | 2.005 us |
| `lokad/langserv/url-dashboard` | 1.375 us | 7.101 us | 7.655 us |
| `lokad/langserv/url-download` | 2.168 us | 12.474 us | 12.844 us |
| `lokad/style/hex-color` | 0.340 us | 0.473 us | 0.558 us |
| `lokad/style/cell-ref` | 0.745 us | 1.699 us | 1.901 us |
| `lokad/style/range-ref` | 0.460 us | 1.349 us | 1.478 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
