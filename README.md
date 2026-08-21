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
| `literal/sherlock-en` | 117.015 us | 56.970 us | 490.670 us |
| `literal/sherlock-casei-en` | 165.370 us | 85.060 us | 351.665 us |
| `literal/sherlock-ru` | 1,978.395 us | 64.655 us | 2,914.300 us |
| `literal/sherlock-casei-ru` | 3,343.035 us | 221.055 us | 3,186.025 us |
| `literal/sherlock-zh` | 593.065 us | 17.135 us | 1,212.510 us |
| `literal-alternate/sherlock-en` | 260.900 us | 875.210 us | 1,107.600 us |
| `literal-alternate/sherlock-en-nomatch` | 80.185 us | 405.735 us | 643.900 us |
| `literal-alternate/sherlock-en-mixed` | 104.985 us | 376.555 us | 581.495 us |
| `literal-alternate/sherlock-casei-en` | 250.570 us | 7,569.900 us | 10,121.795 us |
| `literal-alternate/sherlock-ru` | 2,728.960 us | 3,222.225 us | 6,705.265 us |
| `literal-alternate/sherlock-casei-ru` | 19,005.735 us | 15,636.290 us | 18,507.295 us |
| `literal-alternate/sherlock-zh` | 1,018.035 us | 46.250 us | 1,455.580 us |
| `bounded-repeat/letters-en` | 504.345 us | 1,642.140 us | 1,643.380 us |
| `bounded-repeat/letters-ru` | 723.120 us | 3,537.905 us | 4,029.870 us |
| `bounded-repeat/context` | 22,424.150 us | 108,671.590 us | 109,797.940 us |
| `bounded-repeat/capitals` | 404.935 us | 36,294.885 us | 29,134.990 us |
| `dictionary/single` | 5,842.180 us | 18,567.640 us | 19,233.680 us |
| `aws-keys/full` | 1,711.445 us | 66,137.430 us | 81,620.635 us |
| `aws-keys/quick` | 3,501.965 us | 7,061.045 us | 17,640.830 us |
| `ruff-noqa/real` | 2,803.620 us | 46,291.755 us | 57,746.135 us |
| `ruff-noqa/tweaked` | 3,553.300 us | 5,352.960 us | 16,217.790 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.291 us | 0.214 us | 0.224 us |
| `common/email-miss` | `IsMatch` | 0.288 us | 0.555 us | 0.219 us |
| `common/date-match` | `IsMatch` | 0.273 us | 0.113 us | 0.120 us |
| `common/date-miss` | `IsMatch` | 0.652 us | 0.916 us | 0.360 us |
| `common/ip-match` | `IsMatch` | 0.152 us | 0.178 us | 0.105 us |
| `common/ip-miss` | `IsMatch` | 0.152 us | 0.193 us | 0.118 us |
| `common/uri-match` | `IsMatch` | 0.358 us | 0.189 us | 0.312 us |
| `common/uri-miss` | `IsMatch` | 0.259 us | 0.288 us | 0.120 us |
| `common/matches-set` | `Count` | 8.978 us | 109.730 us | 109.234 us |
| `common/matches-boundary` | `Count` | 8.785 us | 95.043 us | 95.813 us |
| `common/matches-word` | `Count` | 3.885 us | 2.618 us | 3.289 us |
| `common/matches-words` | `Count` | 3.978 us | 42.872 us | 43.449 us |
| `common/match-word` | `Match` | 0.374 us | 0.093 us | 0.808 us |
| `common/replace-words` | `Replace` | 51.744 us | 46.594 us | 46.953 us |
| `common/split-words` | `Split` | 11.235 us | 53.694 us | 53.653 us |
| `common/backtracking` | `IsMatch` | 0.194 us | 0.517 us | 0.540 us |
| `common/one-node-backtracking` | `IsMatch` | 0.179 us | 0.360 us | 0.414 us |
| `industry/mariomka-email-count` | `Count` | 7,646.180 us | 229.750 us | 5,731.260 us |
| `industry/mariomka-uri-count` | `Count` | 8,434.725 us | 1,422.620 us | 6,626.770 us |
| `industry/mariomka-ip-count` | `Count` | 3,763.020 us | 5,793.255 us | 11,918.525 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,125.160 us | 11,676.325 us | 12,159.820 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 39.744 us | 160.261 us | 263.961 us |
| `industry/rust-sherlock-ing-count` | `Count` | 7,263.710 us | 6,909.740 us | 7,145.000 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 15.840 us | 4,852.835 us | 4,934.690 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 107.465 us | 598.195 us | 757.654 us |
| `industry/leipzig-twain-count` | `Count` | 3,827.750 us | 1,713.520 us | 6,887.720 us |
| `industry/leipzig-name-family-count` | `Count` | 2,634.350 us | 6,609.210 us | 12,585.510 us |
| `industry/leipzig-river-window-count` | `Count` | 6,969.530 us | 22,327.070 us | 28,176.345 us |
| `industry/leipzig-symbol-count` | `Count` | 1,877.780 us | 12,268.920 us | 17,141.705 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.215 us | 0.109 us | 0.125 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.131 us | 0.164 us | 0.169 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.387 us | 0.127 us | 0.136 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.182 us | 0.082 us | 0.094 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.084 us | 0.075 us | 0.090 us |
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
| `literal/sherlock-en` | 104.035 us | 44.085 us | 312.715 us |
| `literal/sherlock-casei-en` | 128.515 us | 65.085 us | 290.275 us |
| `literal/sherlock-ru` | 2,103.660 us | 60.870 us | 2,919.450 us |
| `literal/sherlock-casei-ru` | 3,052.015 us | 119.950 us | 3,059.150 us |
| `literal/sherlock-zh` | 569.660 us | 15.965 us | 1,218.455 us |
| `literal-alternate/sherlock-en` | 264.120 us | 199.210 us | 438.005 us |
| `literal-alternate/sherlock-en-nomatch` | 109.160 us | 101.265 us | 378.405 us |
| `literal-alternate/sherlock-en-mixed` | 107.540 us | 98.165 us | 343.520 us |
| `literal-alternate/sherlock-casei-en` | 247.270 us | 174.155 us | 450.005 us |
| `literal-alternate/sherlock-ru` | 2,669.435 us | 1,286.745 us | 4,067.760 us |
| `literal-alternate/sherlock-casei-ru` | 4,078.460 us | 1,201.175 us | 4,284.465 us |
| `literal-alternate/sherlock-zh` | 917.675 us | 27.060 us | 1,251.285 us |
| `bounded-repeat/letters-en` | 473.295 us | 1,045.310 us | 1,093.310 us |
| `bounded-repeat/letters-ru` | 677.505 us | 1,209.325 us | 1,664.840 us |
| `bounded-repeat/context` | 32,037.820 us | 40,802.515 us | 40,278.765 us |
| `bounded-repeat/capitals` | 420.740 us | 8,813.850 us | 9,754.270 us |
| `dictionary/single` | 5,680.675 us | 43,292.105 us | 48,370.840 us |
| `aws-keys/full` | 1,578.865 us | 14,469.925 us | 25,903.735 us |
| `aws-keys/quick` | 3,527.165 us | 3,286.505 us | 14,018.620 us |
| `ruff-noqa/real` | 2,479.090 us | 13,052.210 us | 44,287.125 us |
| `ruff-noqa/tweaked` | 3,591.990 us | 4,310.135 us | 15,198.350 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.278 us | 0.049 us | 0.067 us |
| `common/email-miss` | `IsMatch` | 0.289 us | 0.055 us | 0.072 us |
| `common/date-match` | `IsMatch` | 0.270 us | 0.233 us | 0.048 us |
| `common/date-miss` | `IsMatch` | 0.499 us | 0.112 us | 0.135 us |
| `common/ip-match` | `IsMatch` | 0.151 us | 0.041 us | 0.041 us |
| `common/ip-miss` | `IsMatch` | 0.155 us | 0.042 us | 0.040 us |
| `common/uri-match` | `IsMatch` | 0.357 us | 0.049 us | 0.066 us |
| `common/uri-miss` | `IsMatch` | 0.261 us | 0.059 us | 0.054 us |
| `common/matches-set` | `Count` | 5.171 us | 56.177 us | 57.075 us |
| `common/matches-boundary` | `Count` | 4.859 us | 53.768 us | 54.986 us |
| `common/matches-word` | `Count` | 2.170 us | 1.283 us | 1.962 us |
| `common/matches-words` | `Count` | 4.098 us | 10.244 us | 11.269 us |
| `common/match-word` | `Match` | 0.377 us | 0.047 us | 0.727 us |
| `common/replace-words` | `Replace` | 14.748 us | 13.995 us | 14.290 us |
| `common/split-words` | `Split` | 11.418 us | 16.129 us | 15.890 us |
| `common/backtracking` | `IsMatch` | 0.193 us | 0.030 us | 0.042 us |
| `common/one-node-backtracking` | `IsMatch` | 0.180 us | 0.248 us | 0.269 us |
| `industry/mariomka-email-count` | `Count` | 7,124.265 us | 204.765 us | 5,059.695 us |
| `industry/mariomka-uri-count` | `Count` | 8,121.980 us | 868.845 us | 5,955.805 us |
| `industry/mariomka-ip-count` | `Count` | 3,705.760 us | 2,353.725 us | 7,202.705 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,129.525 us | 5,110.185 us | 5,030.645 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 36.097 us | 37.435 us | 147.256 us |
| `industry/rust-sherlock-ing-count` | `Count` | 2,987.640 us | 2,859.665 us | 3,086.105 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 15.680 us | 2,354.300 us | 2,528.125 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 96.726 us | 540.196 us | 702.416 us |
| `industry/leipzig-twain-count` | `Count` | 3,767.275 us | 1,636.540 us | 6,907.345 us |
| `industry/leipzig-name-family-count` | `Count` | 2,337.260 us | 2,542.795 us | 7,613.410 us |
| `industry/leipzig-river-window-count` | `Count` | 6,597.435 us | 6,273.235 us | 11,635.130 us |
| `industry/leipzig-symbol-count` | `Count` | 1,185.125 us | 1,396.950 us | 7,162.355 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.215 us | 0.043 us | 0.104 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.133 us | 0.047 us | 0.109 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.373 us | 0.038 us | 0.055 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.180 us | 0.035 us | 0.050 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.085 us | 0.035 us | 0.088 us |
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
| `literal/identifier-token` | 4.260 us | 3.303 us | 10.160 us |
| `literal/call-token` | 7.147 us | 2.920 us | 8.676 us |
| `literal/identifier-token-casei` | 8.369 us | 8.069 us | 14.384 us |
| `literal-family/type-token-family` | 8.311 us | 14.920 us | 20.985 us |
| `literal-family/method-token-family` | 5.213 us | 5.580 us | 12.410 us |
| `structural/keyword-to-generic-type` | 3.880 us | 3.104 us | 8.817 us |
| `structural/keyword-family-to-capitalized-identifier` | 44.004 us | 133.653 us | 143.171 us |
| `structural/method-family-call` | 2.770 us | 5.070 us | 11.113 us |
| `structural/ordered-keyword-pair` | 5.474 us | 3.334 us | 9.504 us |
| `structural/ordered-identifier-window` | 7.486 us | 18.221 us | 24.833 us |
| `structural/modifier-family-to-type-window` | 15.319 us | 160.682 us | 168.655 us |
| `structural/ordered-keyword-window` | 8.574 us | 12.481 us | 20.839 us |
| `structural/method-family-generic-call` | 2.602 us | 4.068 us | 10.905 us |
| `fallback/lookahead` | 3.823 us | 4.003 us | 9.862 us |
| `balancing/begin-end-blocks` | 2,613.352 us | 2,555.596 us | 2,585.161 us |
| `structural/ordered-modifier-pair` | 6.555 us | 10.841 us | 16.158 us |
| `structural/modifier-family-to-type-pair` | 11.685 us | 79.560 us | 88.931 us |
| `lokad/imports/module-imports` | 3.615 us | 2.750 us | 7.915 us |
| `lokad/folding/region-marker` | 3.655 us | 19.700 us | 24.860 us |
| `lokad/lexer/identifier` | 7.334 us | 9.369 us | 10.559 us |
| `lokad/lexer/number` | 2.739 us | 3.739 us | 4.245 us |
| `lokad/lexer/string` | 6.388 us | 25.633 us | 26.133 us |
| `lokad/lexer/operator-run` | 0.903 us | 1.122 us | 1.272 us |
| `lokad/lexer/doc-line` | 1.160 us | 1.415 us | 1.564 us |
| `lokad/docs/inline-doc-prefix` | 1.376 us | 1.479 us | 1.889 us |
| `lokad/langserv/identifier-validator` | 2.961 us | 5.063 us | 5.804 us |
| `lokad/langserv/helper-identifier` | 2.020 us | 3.049 us | 3.120 us |
| `lokad/langserv/color-short-hex` | 0.748 us | 0.488 us | 0.611 us |
| `lokad/langserv/color-rgb-hex` | 0.622 us | 0.933 us | 1.050 us |
| `lokad/langserv/url-dashboard` | 1.221 us | 4.222 us | 4.509 us |
| `lokad/langserv/url-download` | 3.587 us | 7.244 us | 7.722 us |
| `lokad/style/hex-color` | 0.232 us | 0.288 us | 0.290 us |
| `lokad/style/cell-ref` | 0.477 us | 1.062 us | 0.952 us |
| `lokad/style/range-ref` | 0.334 us | 0.753 us | 0.867 us |
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
| `literal/identifier-token` | 2.894 us | 2.677 us | 7.925 us |
| `literal/call-token` | 5.906 us | 2.221 us | 8.694 us |
| `literal/identifier-token-casei` | 5.309 us | 4.519 us | 10.984 us |
| `literal-family/type-token-family` | 8.479 us | 5.986 us | 21.491 us |
| `literal-family/method-token-family` | 5.197 us | 3.529 us | 14.493 us |
| `structural/keyword-to-generic-type` | 3.376 us | 2.811 us | 10.168 us |
| `structural/keyword-family-to-capitalized-identifier` | 47.687 us | 41.402 us | 48.851 us |
| `structural/method-family-call` | 2.747 us | 3.220 us | 9.206 us |
| `structural/ordered-keyword-pair` | 3.608 us | 2.484 us | 10.187 us |
| `structural/ordered-identifier-window` | 3.622 us | 6.189 us | 12.530 us |
| `structural/modifier-family-to-type-window` | 14.191 us | 56.008 us | 62.951 us |
| `structural/ordered-keyword-window` | 3.918 us | 5.344 us | 17.488 us |
| `structural/method-family-generic-call` | 2.606 us | 3.262 us | 9.213 us |
| `fallback/lookahead` | 3.119 us | 2.562 us | 8.074 us |
| `balancing/begin-end-blocks` | 271.009 us | 260.104 us | 269.330 us |
| `structural/ordered-modifier-pair` | 3.312 us | 4.992 us | 10.772 us |
| `structural/modifier-family-to-type-pair` | 11.802 us | 26.769 us | 34.115 us |
| `lokad/imports/module-imports` | 2.900 us | 2.750 us | 8.470 us |
| `lokad/folding/region-marker` | 3.580 us | 20.375 us | 26.955 us |
| `lokad/lexer/identifier` | 7.128 us | 9.681 us | 10.387 us |
| `lokad/lexer/number` | 2.753 us | 3.802 us | 4.465 us |
| `lokad/lexer/string` | 5.945 us | 23.852 us | 25.757 us |
| `lokad/lexer/operator-run` | 0.883 us | 1.066 us | 1.292 us |
| `lokad/lexer/doc-line` | 1.167 us | 1.408 us | 1.571 us |
| `lokad/docs/inline-doc-prefix` | 1.121 us | 1.528 us | 1.837 us |
| `lokad/langserv/identifier-validator` | 2.915 us | 4.945 us | 6.353 us |
| `lokad/langserv/helper-identifier` | 2.005 us | 2.887 us | 3.531 us |
| `lokad/langserv/color-short-hex` | 0.365 us | 0.494 us | 0.652 us |
| `lokad/langserv/color-rgb-hex` | 0.590 us | 1.016 us | 1.209 us |
| `lokad/langserv/url-dashboard` | 0.852 us | 4.176 us | 4.474 us |
| `lokad/langserv/url-download` | 1.240 us | 7.295 us | 7.393 us |
| `lokad/style/hex-color` | 0.248 us | 0.357 us | 0.288 us |
| `lokad/style/cell-ref` | 0.545 us | 0.873 us | 1.100 us |
| `lokad/style/range-ref` | 0.372 us | 0.770 us | 0.933 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
