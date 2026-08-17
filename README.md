# Lokad.Utf8Regex

`Lokad.Utf8Regex` is a `net10.0` regular expression library whose semantic reference is `.NET 10`'s `System.Text.RegularExpressions.Regex`, while its primary I/O surface is UTF-8 `ReadOnlySpan<byte>`.

It is intended for workloads where the input already exists as UTF-8 bytes and you want to avoid decoding to UTF-16 before every regex operation.

```powershell
dotnet add package Lokad.Utf8Regex
dotnet add package Lokad.Utf8Regex.Pcre2
dotnet add package Lokad.Utf8Regex.PythonRe
```

The [PCRE2 support](src/Lokad.Utf8Regex.Pcre2/README.md) is optional.
The [Python `re` support](src/Lokad.Utf8Regex.PythonRe/README.md) is optional.
Release notes are tracked in [CHANGELOG.md](CHANGELOG.md).

## Support Scope

- Semantic oracle: `Utf8Regex` follows `System.Text.RegularExpressions.Regex` on `.NET 10`.
- Primary I/O model: the main API surface operates on UTF-8 `ReadOnlySpan<byte>` / `Span<byte>`, while match coordinates remain aligned with `.NET` regex semantics and UTF-16 positions.
- Primary option scope: `RegexOptions.CultureInvariant` is the main supported and performance-tested mode.
- Other options: unsupported optimizations fall back to slower `.NET`-equivalent execution paths rather than silently changing semantics.
- Execution model: some patterns lower to native UTF-8 backends, while others use fallback verification paths. This affects performance characteristics, not the semantic target.
- Byte coordinates: `Utf8ValueMatch` exposes byte offsets only when the match is byte-aligned. UTF-16 coordinates remain available even when byte alignment is not available.
- Stability: the package is suitable for early production use on `CultureInvariant` UTF-8 workloads, but still evolving in backend coverage and performance portability.

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
- Match results expose both UTF-16 coordinates and, when available, byte coordinates via `Utf8ValueMatch`.

<!-- BEGIN GENERATED DOTNET_PERFORMANCE BENCHMARKS -->
## DotNetPerformance Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---:|---:|---:|
| `literal/sherlock-en` | 114.090 us | 63.290 us | 515.730 us |
| `literal/sherlock-casei-en` | 153.145 us | 97.400 us | 540.610 us |
| `literal/sherlock-ru` | 1,950.685 us | 66.625 us | 3,223.920 us |
| `literal/sherlock-casei-ru` | 3,857.490 us | 219.925 us | 3,073.305 us |
| `literal/sherlock-zh` | 578.660 us | 17.190 us | 1,634.585 us |
| `literal-alternate/sherlock-en` | 257.265 us | 958.060 us | 1,368.410 us |
| `literal-alternate/sherlock-en-nomatch` | 80.455 us | 395.005 us | 633.725 us |
| `literal-alternate/sherlock-en-mixed` | 94.925 us | 384.130 us | 664.895 us |
| `literal-alternate/sherlock-casei-en` | 229.210 us | 7,006.700 us | 7,006.315 us |
| `literal-alternate/sherlock-ru` | 2,859.675 us | 3,341.320 us | 6,644.790 us |
| `literal-alternate/sherlock-casei-ru` | 18,367.955 us | 15,061.530 us | 17,833.990 us |
| `literal-alternate/sherlock-zh` | 1,549.660 us | 45.765 us | 1,650.745 us |
| `bounded-repeat/letters-en` | 498.485 us | 1,822.758 us | 2,562.436 us |
| `bounded-repeat/letters-ru` | 785.520 us | 5,048.247 us | 4,542.601 us |
| `bounded-repeat/context` | 22,181.665 us | 87,284.315 us | 88,231.660 us |
| `bounded-repeat/capitals` | 110.945 us | 26,534.150 us | 28,829.025 us |
| `dictionary/single` | 5,014.850 us | 17,138.465 us | 16,897.645 us |
| `aws-keys/full` | 1,906.785 us | 90,314.280 us | 115,506.850 us |
| `aws-keys/quick` | 4,258.480 us | 7,742.685 us | 37,498.105 us |
| `ruff-noqa/real` | 2,965.055 us | 90,843.940 us | 112,979.305 us |
| `ruff-noqa/tweaked` | 3,713.190 us | 5,370.250 us | 35,178.400 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.314 us | 0.213 us | 0.234 us |
| `common/email-miss` | `IsMatch` | 0.345 us | 0.291 us | 0.305 us |
| `common/date-match` | `IsMatch` | 0.296 us | 0.101 us | 0.128 us |
| `common/date-miss` | `IsMatch` | 0.488 us | 0.669 us | 0.759 us |
| `common/ip-match` | `IsMatch` | 0.221 us | 0.177 us | 0.188 us |
| `common/ip-miss` | `IsMatch` | 0.162 us | 0.179 us | 0.201 us |
| `common/uri-match` | `IsMatch` | 0.404 us | 0.198 us | 0.219 us |
| `common/uri-miss` | `IsMatch` | 0.301 us | 0.173 us | 0.191 us |
| `common/matches-set` | `Count` | 9.463 us | 114.039 us | 116.900 us |
| `common/matches-boundary` | `Count` | 9.742 us | 102.138 us | 103.911 us |
| `common/matches-word` | `Count` | 4.084 us | 7.184 us | 5.326 us |
| `common/matches-words` | `Count` | 4.166 us | 43.090 us | 43.811 us |
| `common/match-word` | `Match` | 0.425 us | 0.360 us | 2.834 us |
| `common/replace-words` | `Replace` | 66.624 us | 47.906 us | 89.140 us |
| `common/split-words` | `Split` | 15.025 us | 47.110 us | 49.147 us |
| `common/backtracking` | `IsMatch` | 0.203 us | 0.533 us | 0.555 us |
| `common/one-node-backtracking` | `IsMatch` | 0.198 us | 0.358 us | 0.385 us |
| `industry/mariomka-email-count` | `Count` | 2,016.213 us | 233.755 us | 5,460.111 us |
| `industry/mariomka-uri-count` | `Count` | 2,373.694 us | 1,110.650 us | 6,229.063 us |
| `industry/mariomka-ip-count` | `Count` | 3,200.650 us | 5,565.222 us | 10,265.026 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,137.253 us | 12,138.907 us | 11,290.193 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 46.086 us | 160.223 us | 272.079 us |
| `industry/rust-sherlock-ing-count` | `Count` | 139.185 us | 7,040.675 us | 7,107.958 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 16.002 us | 4,928.868 us | 5,306.936 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 104.861 us | 625.594 us | 751.229 us |
| `industry/leipzig-twain-count` | `Count` | 3,573.358 us | 1,449.205 us | 6,524.792 us |
| `industry/leipzig-name-family-count` | `Count` | 2,047.833 us | 6,630.381 us | 11,614.398 us |
| `industry/leipzig-river-window-count` | `Count` | 6,739.285 us | 21,658.285 us | 26,917.356 us |
| `industry/leipzig-symbol-count` | `Count` | 1,335.472 us | 12,563.092 us | 17,104.881 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.236 us | 0.101 us | 0.125 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.162 us | 0.147 us | 0.159 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.434 us | 0.078 us | 0.091 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.199 us | 0.076 us | 0.092 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.097 us | 0.070 us | 0.087 us |
<!-- END GENERATED DOTNET_PERFORMANCE BENCHMARKS -->

<!-- BEGIN GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->
## DotNetPerformance Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the `DotNetPerformanceReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---:|---:|---:|
| `literal/sherlock-en` | 94.630 us | 51.035 us | 291.710 us |
| `literal/sherlock-casei-en` | 130.990 us | 51.700 us | 315.875 us |
| `literal/sherlock-ru` | 1,935.235 us | 71.500 us | 3,006.920 us |
| `literal/sherlock-casei-ru` | 3,006.825 us | 116.350 us | 2,997.100 us |
| `literal/sherlock-zh` | 701.110 us | 16.175 us | 1,357.620 us |
| `literal-alternate/sherlock-en` | 253.055 us | 228.590 us | 453.475 us |
| `literal-alternate/sherlock-en-nomatch` | 80.045 us | 110.545 us | 559.840 us |
| `literal-alternate/sherlock-en-mixed` | 101.840 us | 97.340 us | 567.690 us |
| `literal-alternate/sherlock-casei-en` | 235.970 us | 104.195 us | 346.000 us |
| `literal-alternate/sherlock-ru` | 2,713.570 us | 1,267.255 us | 4,178.720 us |
| `literal-alternate/sherlock-casei-ru` | 4,269.375 us | 1,309.965 us | 4,048.985 us |
| `literal-alternate/sherlock-zh` | 896.710 us | 27.935 us | 1,225.750 us |
| `bounded-repeat/letters-en` | 535.621 us | 1,254.683 us | 1,384.101 us |
| `bounded-repeat/letters-ru` | 651.472 us | 1,278.034 us | 1,731.050 us |
| `bounded-repeat/context` | 22,281.635 us | 42,708.990 us | 56,128.870 us |
| `bounded-repeat/capitals` | 117.500 us | 8,149.000 us | 11,683.480 us |
| `dictionary/single` | 4,810.835 us | 38,986.755 us | 38,168.155 us |
| `aws-keys/full` | 1,440.865 us | 18,413.370 us | 41,606.325 us |
| `aws-keys/quick` | 3,665.630 us | 3,391.260 us | 30,125.925 us |
| `ruff-noqa/real` | 3,335.780 us | 26,244.465 us | 47,122.170 us |
| `ruff-noqa/tweaked` | 3,547.250 us | 4,435.455 us | 30,706.930 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.312 us | 0.446 us | 0.465 us |
| `common/email-miss` | `IsMatch` | 0.333 us | 0.458 us | 0.245 us |
| `common/date-match` | `IsMatch` | 0.303 us | 0.225 us | 0.250 us |
| `common/date-miss` | `IsMatch` | 0.452 us | 0.115 us | 0.139 us |
| `common/ip-match` | `IsMatch` | 0.161 us | 0.043 us | 0.059 us |
| `common/ip-miss` | `IsMatch` | 0.166 us | 0.044 us | 0.059 us |
| `common/uri-match` | `IsMatch` | 0.566 us | 0.066 us | 0.150 us |
| `common/uri-miss` | `IsMatch` | 0.302 us | 0.057 us | 0.071 us |
| `common/matches-set` | `Count` | 9.203 us | 97.810 us | 202.499 us |
| `common/matches-boundary` | `Count` | 4.940 us | 54.633 us | 55.539 us |
| `common/matches-word` | `Count` | 3.585 us | 1.357 us | 3.912 us |
| `common/matches-words` | `Count` | 4.125 us | 10.581 us | 11.813 us |
| `common/match-word` | `Match` | 0.829 us | 0.073 us | 1.369 us |
| `common/replace-words` | `Replace` | 17.842 us | 15.270 us | 28.106 us |
| `common/split-words` | `Split` | 11.024 us | 13.541 us | 20.113 us |
| `common/backtracking` | `IsMatch` | 0.204 us | 0.036 us | 0.052 us |
| `common/one-node-backtracking` | `IsMatch` | 0.203 us | 0.058 us | 0.073 us |
| `industry/mariomka-email-count` | `Count` | 1,970.039 us | 219.223 us | 5,180.626 us |
| `industry/mariomka-uri-count` | `Count` | 2,461.830 us | 669.853 us | 5,702.917 us |
| `industry/mariomka-ip-count` | `Count` | 3,295.989 us | 2,221.847 us | 6,988.531 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,128.874 us | 5,186.630 us | 5,214.307 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 36.806 us | 40.413 us | 378.054 us |
| `industry/rust-sherlock-ing-count` | `Count` | 131.020 us | 3,008.543 us | 3,124.505 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 15.161 us | 2,429.840 us | 2,652.896 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 90.535 us | 871.132 us | 1,256.145 us |
| `industry/leipzig-twain-count` | `Count` | 3,267.684 us | 1,604.681 us | 6,715.483 us |
| `industry/leipzig-name-family-count` | `Count` | 2,054.541 us | 2,238.198 us | 6,994.503 us |
| `industry/leipzig-river-window-count` | `Count` | 6,665.832 us | 6,593.333 us | 11,386.079 us |
| `industry/leipzig-symbol-count` | `Count` | 1,260.314 us | 1,171.766 us | 6,373.450 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.244 us | 0.047 us | 0.070 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.151 us | 0.047 us | 0.063 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.536 us | 0.037 us | 0.052 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.201 us | 0.036 us | 0.050 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.096 us | 0.036 us | 0.052 us |
<!-- END GENERATED DOTNET_PERFORMANCE_COMPILED BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD BENCHMARKS -->
## Lokad Benchmarks

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex`: direct UTF-8 input
- `.NET predecoded`: `.NET Regex` on an already-decoded `string`
- `.NET + decode`: `Encoding.UTF8.GetString(...)` on each operation, then `.NET Regex`

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---:|---:|---:|
| `literal/identifier-token` | 2.873 us | 3.044 us | 14.913 us |
| `literal/call-token` | 6.157 us | 2.691 us | 16.488 us |
| `literal/identifier-token-casei` | 5.641 us | 8.352 us | 29.903 us |
| `literal-family/type-token-family` | 8.550 us | 32.719 us | 46.195 us |
| `literal-family/method-token-family` | 5.570 us | 5.701 us | 22.700 us |
| `structural/keyword-to-generic-type` | 3.557 us | 3.121 us | 16.425 us |
| `structural/keyword-family-to-capitalized-identifier` | 45.221 us | 270.733 us | 274.979 us |
| `structural/method-family-call` | 2.894 us | 5.186 us | 21.358 us |
| `structural/ordered-keyword-pair` | 4.270 us | 3.962 us | 18.972 us |
| `structural/ordered-identifier-window` | 3.924 us | 20.048 us | 62.684 us |
| `structural/modifier-family-to-type-window` | 15.764 us | 304.127 us | 306.640 us |
| `structural/ordered-keyword-window` | 3.885 us | 11.532 us | 32.685 us |
| `structural/method-family-generic-call` | 2.632 us | 4.639 us | 17.771 us |
| `fallback/lookahead` | 3.830 us | 4.396 us | 10.247 us |
| `balancing/begin-end-blocks` | 5,164.090 us | 5,104.702 us | 5,119.828 us |
| `structural/ordered-modifier-pair` | 3.861 us | 10.109 us | 30.955 us |
| `structural/modifier-family-to-type-pair` | 11.700 us | 233.160 us | 83.660 us |
| `lokad/imports/module-imports` | 3.065 us | 3.515 us | 8.575 us |
| `lokad/folding/region-marker` | 3.290 us | 48.410 us | 25.395 us |
| `lokad/lexer/identifier` | 7.481 us | 9.398 us | 10.770 us |
| `lokad/lexer/number` | 2.864 us | 4.426 us | 4.587 us |
| `lokad/lexer/string` | 6.667 us | 25.598 us | 26.315 us |
| `lokad/lexer/operator-run` | 0.925 us | 1.188 us | 1.944 us |
| `lokad/lexer/doc-line` | 1.066 us | 1.457 us | 1.655 us |
| `lokad/docs/inline-doc-prefix` | 1.124 us | 1.598 us | 2.124 us |
| `lokad/langserv/identifier-validator` | 3.788 us | 5.686 us | 6.274 us |
| `lokad/langserv/helper-identifier` | 2.010 us | 3.197 us | 3.516 us |
| `lokad/langserv/color-short-hex` | 1.066 us | 0.571 us | 0.718 us |
| `lokad/langserv/color-rgb-hex` | 1.353 us | 1.016 us | 1.080 us |
| `lokad/langserv/url-dashboard` | 1.025 us | 4.142 us | 4.615 us |
| `lokad/langserv/url-download` | 3.900 us | 7.215 us | 7.685 us |
| `lokad/style/hex-color` | 0.475 us | 0.510 us | 0.730 us |
| `lokad/style/cell-ref` | 1.203 us | 0.932 us | 1.134 us |
| `lokad/style/range-ref` | 1.723 us | 0.787 us | 0.827 us |
<!-- END GENERATED LOKAD BENCHMARKS -->

<!-- BEGIN GENERATED LOKAD_COMPILED BENCHMARKS -->
## Lokad Benchmarks (Compiled)

These numbers are stored in `README.Benchmarks.json` and refreshed incrementally from the combined `LokadReplica` suite in `Release`. They compare:
- `Utf8Regex Compiled`: direct UTF-8 input using `Utf8Regex(..., options | RegexOptions.Compiled)`
- `.NET compiled predecoded`: compiled `.NET Regex` on an already-decoded `string`
- `.NET compiled + decode`: `Encoding.UTF8.GetString(...)` on each operation, then compiled `.NET Regex`

This combined suite covers Lokad production-style workloads, mixing coding-agent-style codebase probes over a plausible C# corpus with Lokad script whole-document counts and anchored per-sample prefix-match loops.

| Case | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---:|---:|---:|
| `literal/identifier-token` | 2.854 us | 2.349 us | 15.097 us |
| `literal/call-token` | 5.935 us | 2.228 us | 15.631 us |
| `literal/identifier-token-casei` | 5.415 us | 8.289 us | 21.640 us |
| `literal-family/type-token-family` | 8.570 us | 9.532 us | 21.955 us |
| `literal-family/method-token-family` | 5.506 us | 4.135 us | 19.908 us |
| `structural/keyword-to-generic-type` | 3.473 us | 2.851 us | 17.981 us |
| `structural/keyword-family-to-capitalized-identifier` | 75.414 us | 66.353 us | 79.591 us |
| `structural/method-family-call` | 2.926 us | 3.437 us | 19.078 us |
| `structural/ordered-keyword-pair` | 4.338 us | 2.508 us | 15.697 us |
| `structural/ordered-identifier-window` | 3.677 us | 14.459 us | 24.887 us |
| `structural/modifier-family-to-type-window` | 17.166 us | 107.296 us | 122.968 us |
| `structural/ordered-keyword-window` | 3.887 us | 10.559 us | 26.571 us |
| `structural/method-family-generic-call` | 2.669 us | 2.967 us | 18.107 us |
| `fallback/lookahead` | 3.613 us | 3.043 us | 15.979 us |
| `balancing/begin-end-blocks` | 683.019 us | 605.813 us | 636.086 us |
| `structural/ordered-modifier-pair` | 3.714 us | 7.727 us | 20.989 us |
| `structural/modifier-family-to-type-pair` | 106.500 us | 61.200 us | 71.840 us |
| `lokad/imports/module-imports` | 13.670 us | 6.131 us | 36.711 us |
| `lokad/folding/region-marker` | 28.480 us | 162.400 us | 171.220 us |
| `lokad/lexer/identifier` | 7.549 us | 10.126 us | 11.049 us |
| `lokad/lexer/number` | 2.739 us | 4.063 us | 4.653 us |
| `lokad/lexer/string` | 6.270 us | 23.045 us | 25.120 us |
| `lokad/lexer/operator-run` | 0.901 us | 1.285 us | 1.282 us |
| `lokad/lexer/doc-line` | 1.051 us | 1.387 us | 1.597 us |
| `lokad/docs/inline-doc-prefix` | 1.101 us | 1.764 us | 1.975 us |
| `lokad/langserv/identifier-validator` | 2.779 us | 5.845 us | 6.388 us |
| `lokad/langserv/helper-identifier` | 1.967 us | 3.164 us | 3.553 us |
| `lokad/langserv/color-short-hex` | 0.353 us | 0.662 us | 0.662 us |
| `lokad/langserv/color-rgb-hex` | 0.600 us | 1.070 us | 1.113 us |
| `lokad/langserv/url-dashboard` | 0.884 us | 4.185 us | 4.414 us |
| `lokad/langserv/url-download` | 15.245 us | 16.673 us | 17.429 us |
| `lokad/style/hex-color` | 1.449 us | 0.855 us | 1.040 us |
| `lokad/style/cell-ref` | 0.500 us | 1.049 us | 1.148 us |
| `lokad/style/range-ref` | 0.303 us | 0.878 us | 0.907 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
