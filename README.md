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
| `literal/sherlock-en` | 106.470 us | 54.890 us | 303.225 us |
| `literal/sherlock-casei-en` | 156.340 us | 92.075 us | 336.115 us |
| `literal/sherlock-ru` | 1,956.030 us | 62.435 us | 3,049.405 us |
| `literal/sherlock-casei-ru` | 3,976.215 us | 224.065 us | 3,386.975 us |
| `literal/sherlock-zh` | 585.500 us | 17.050 us | 1,291.615 us |
| `literal-alternate/sherlock-en` | 252.920 us | 948.120 us | 1,273.955 us |
| `literal-alternate/sherlock-en-nomatch` | 89.030 us | 429.950 us | 811.890 us |
| `literal-alternate/sherlock-en-mixed` | 99.650 us | 365.100 us | 721.030 us |
| `literal-alternate/sherlock-casei-en` | 234.305 us | 6,383.250 us | 11,746.145 us |
| `literal-alternate/sherlock-ru` | 2,728.110 us | 3,174.725 us | 10,843.940 us |
| `literal-alternate/sherlock-casei-ru` | 17,522.535 us | 23,809.550 us | 27,583.715 us |
| `literal-alternate/sherlock-zh` | 1,033.650 us | 48.400 us | 1,265.645 us |
| `bounded-repeat/letters-en` | 504.820 us | 1,613.760 us | 1,637.235 us |
| `bounded-repeat/letters-ru` | 649.545 us | 3,524.230 us | 4,024.335 us |
| `bounded-repeat/context` | 27,913.280 us | 157,711.395 us | 162,307.165 us |
| `bounded-repeat/capitals` | 120.145 us | 38,028.710 us | 55,665.520 us |
| `dictionary/single` | 5,667.530 us | 31,170.225 us | 31,257.130 us |
| `aws-keys/full` | 1,617.725 us | 103,630.215 us | 117,509.270 us |
| `aws-keys/quick` | 3,416.245 us | 6,996.410 us | 34,786.275 us |
| `ruff-noqa/real` | 2,873.175 us | 90,537.650 us | 70,028.050 us |
| `ruff-noqa/tweaked` | 3,545.940 us | 5,154.005 us | 16,177.460 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.282 us | 0.221 us | 0.674 us |
| `common/email-miss` | `IsMatch` | 0.502 us | 0.786 us | 0.369 us |
| `common/date-match` | `IsMatch` | 0.275 us | 0.111 us | 0.109 us |
| `common/date-miss` | `IsMatch` | 0.472 us | 0.567 us | 0.391 us |
| `common/ip-match` | `IsMatch` | 0.229 us | 0.637 us | 0.115 us |
| `common/ip-miss` | `IsMatch` | 0.162 us | 0.215 us | 0.124 us |
| `common/uri-match` | `IsMatch` | 0.374 us | 0.339 us | 0.200 us |
| `common/uri-miss` | `IsMatch` | 0.751 us | 0.332 us | 0.181 us |
| `common/matches-set` | `Count` | 9.118 us | 109.981 us | 110.365 us |
| `common/matches-boundary` | `Count` | 9.008 us | 97.027 us | 96.186 us |
| `common/matches-word` | `Count` | 3.190 us | 2.574 us | 3.556 us |
| `common/matches-words` | `Count` | 4.065 us | 41.914 us | 43.082 us |
| `common/match-word` | `Match` | 0.374 us | 0.090 us | 0.895 us |
| `common/replace-words` | `Replace` | 53.884 us | 69.306 us | 81.064 us |
| `common/split-words` | `Split` | 12.911 us | 46.876 us | 47.303 us |
| `common/backtracking` | `IsMatch` | 0.194 us | 0.527 us | 0.554 us |
| `common/one-node-backtracking` | `IsMatch` | 0.183 us | 0.361 us | 0.383 us |
| `industry/mariomka-email-count` | `Count` | 7,440.960 us | 231.840 us | 5,284.920 us |
| `industry/mariomka-uri-count` | `Count` | 7,770.480 us | 1,409.460 us | 6,906.275 us |
| `industry/mariomka-ip-count` | `Count` | 3,228.925 us | 5,074.695 us | 11,177.035 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,083.150 us | 14,388.770 us | 18,491.985 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 46.423 us | 194.374 us | 398.882 us |
| `industry/rust-sherlock-ing-count` | `Count` | 7,266.115 us | 6,832.160 us | 6,934.400 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 16.270 us | 4,713.980 us | 8,329.540 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 140.186 us | 1,050.609 us | 1,703.743 us |
| `industry/leipzig-twain-count` | `Count` | 4,141.005 us | 1,570.760 us | 9,426.660 us |
| `industry/leipzig-name-family-count` | `Count` | 2,746.095 us | 6,498.255 us | 11,646.390 us |
| `industry/leipzig-river-window-count` | `Count` | 6,658.090 us | 31,866.630 us | 27,079.535 us |
| `industry/leipzig-symbol-count` | `Count` | 2,415.275 us | 12,129.340 us | 17,288.465 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.219 us | 0.109 us | 0.193 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.134 us | 0.164 us | 0.169 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.372 us | 0.083 us | 0.095 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.176 us | 0.084 us | 0.150 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.087 us | 0.075 us | 0.091 us |
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
| `literal/sherlock-en` | 94.290 us | 41.305 us | 295.855 us |
| `literal/sherlock-casei-en` | 131.875 us | 49.625 us | 377.695 us |
| `literal/sherlock-ru` | 1,903.900 us | 54.450 us | 4,378.245 us |
| `literal/sherlock-casei-ru` | 3,179.800 us | 123.775 us | 4,590.290 us |
| `literal/sherlock-zh` | 672.640 us | 16.935 us | 1,247.315 us |
| `literal-alternate/sherlock-en` | 247.145 us | 203.625 us | 615.535 us |
| `literal-alternate/sherlock-en-nomatch` | 89.810 us | 101.880 us | 377.440 us |
| `literal-alternate/sherlock-en-mixed` | 106.940 us | 101.115 us | 381.990 us |
| `literal-alternate/sherlock-casei-en` | 240.740 us | 104.485 us | 845.325 us |
| `literal-alternate/sherlock-ru` | 2,692.755 us | 1,273.215 us | 6,361.330 us |
| `literal-alternate/sherlock-casei-ru` | 6,484.605 us | 1,967.745 us | 6,155.350 us |
| `literal-alternate/sherlock-zh` | 906.765 us | 27.785 us | 1,287.425 us |
| `bounded-repeat/letters-en` | 473.040 us | 1,055.465 us | 1,100.600 us |
| `bounded-repeat/letters-ru` | 702.035 us | 1,191.020 us | 2,536.350 us |
| `bounded-repeat/context` | 35,300.440 us | 56,034.275 us | 62,446.690 us |
| `bounded-repeat/capitals` | 121.035 us | 11,673.770 us | 18,885.070 us |
| `dictionary/single` | 5,693.205 us | 76,178.825 us | 75,026.460 us |
| `aws-keys/full` | 1,504.375 us | 20,512.170 us | 40,779.615 us |
| `aws-keys/quick` | 3,674.505 us | 7,128.895 us | 28,899.715 us |
| `ruff-noqa/real` | 3,893.090 us | 23,651.770 us | 26,354.380 us |
| `ruff-noqa/tweaked` | 3,680.400 us | 4,161.190 us | 15,065.920 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.270 us | 0.051 us | 0.149 us |
| `common/email-miss` | `IsMatch` | 0.585 us | 0.079 us | 0.073 us |
| `common/date-match` | `IsMatch` | 0.645 us | 0.047 us | 0.098 us |
| `common/date-miss` | `IsMatch` | 0.616 us | 0.195 us | 0.214 us |
| `common/ip-match` | `IsMatch` | 0.198 us | 0.036 us | 0.052 us |
| `common/ip-miss` | `IsMatch` | 0.163 us | 0.034 us | 0.084 us |
| `common/uri-match` | `IsMatch` | 0.363 us | 0.064 us | 0.097 us |
| `common/uri-miss` | `IsMatch` | 0.342 us | 0.064 us | 0.047 us |
| `common/matches-set` | `Count` | 5.007 us | 55.599 us | 56.497 us |
| `common/matches-boundary` | `Count` | 5.164 us | 54.079 us | 54.938 us |
| `common/matches-word` | `Count` | 2.169 us | 1.298 us | 1.963 us |
| `common/matches-words` | `Count` | 4.065 us | 10.229 us | 11.254 us |
| `common/match-word` | `Match` | 0.379 us | 0.047 us | 0.925 us |
| `common/replace-words` | `Replace` | 17.480 us | 17.138 us | 20.021 us |
| `common/split-words` | `Split` | 10.735 us | 13.220 us | 14.413 us |
| `common/backtracking` | `IsMatch` | 0.195 us | 0.031 us | 0.042 us |
| `common/one-node-backtracking` | `IsMatch` | 0.180 us | 0.235 us | 0.260 us |
| `industry/mariomka-email-count` | `Count` | 7,238.280 us | 368.600 us | 5,129.375 us |
| `industry/mariomka-uri-count` | `Count` | 7,301.080 us | 1,110.170 us | 6,150.140 us |
| `industry/mariomka-ip-count` | `Count` | 3,101.160 us | 2,108.890 us | 7,088.155 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,057.900 us | 10,089.935 us | 8,548.640 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 42.048 us | 42.998 us | 274.233 us |
| `industry/rust-sherlock-ing-count` | `Count` | 3,008.050 us | 2,880.770 us | 3,015.420 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 17.040 us | 2,364.325 us | 3,445.300 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 167.498 us | 731.720 us | 989.636 us |
| `industry/leipzig-twain-count` | `Count` | 3,955.360 us | 1,654.755 us | 7,232.540 us |
| `industry/leipzig-name-family-count` | `Count` | 2,522.690 us | 2,418.610 us | 7,348.470 us |
| `industry/leipzig-river-window-count` | `Count` | 7,199.590 us | 6,385.765 us | 12,103.855 us |
| `industry/leipzig-symbol-count` | `Count` | 1,323.055 us | 1,510.905 us | 6,443.450 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.269 us | 0.044 us | 0.111 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.137 us | 0.047 us | 0.059 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.372 us | 0.035 us | 0.047 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.177 us | 0.035 us | 0.048 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.088 us | 0.036 us | 0.049 us |
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
| `literal/identifier-token` | 6.084 us | 3.849 us | 10.110 us |
| `literal/call-token` | 5.876 us | 2.796 us | 8.560 us |
| `literal/identifier-token-casei` | 8.524 us | 8.165 us | 14.076 us |
| `literal-family/type-token-family` | 8.340 us | 15.618 us | 20.940 us |
| `literal-family/method-token-family` | 5.249 us | 5.726 us | 11.763 us |
| `structural/keyword-to-generic-type` | 3.872 us | 3.075 us | 8.917 us |
| `structural/keyword-family-to-capitalized-identifier` | 45.641 us | 134.052 us | 136.904 us |
| `structural/method-family-call` | 2.918 us | 5.016 us | 11.522 us |
| `structural/ordered-keyword-pair` | 5.469 us | 3.288 us | 9.038 us |
| `structural/ordered-identifier-window` | 7.348 us | 18.451 us | 24.576 us |
| `structural/modifier-family-to-type-window` | 15.614 us | 161.531 us | 167.962 us |
| `structural/ordered-keyword-window` | 7.820 us | 11.494 us | 17.463 us |
| `structural/method-family-generic-call` | 2.780 us | 4.210 us | 11.649 us |
| `fallback/lookahead` | 4.761 us | 4.677 us | 10.453 us |
| `balancing/begin-end-blocks` | 2,485.841 us | 2,658.786 us | 2,728.018 us |
| `structural/ordered-modifier-pair` | 5.426 us | 10.298 us | 17.636 us |
| `structural/modifier-family-to-type-pair` | 12.829 us | 79.110 us | 88.460 us |
| `lokad/imports/module-imports` | 3.290 us | 3.235 us | 8.050 us |
| `lokad/folding/region-marker` | 3.465 us | 20.475 us | 24.970 us |
| `lokad/lexer/identifier` | 7.202 us | 10.021 us | 11.667 us |
| `lokad/lexer/number` | 3.368 us | 3.954 us | 4.412 us |
| `lokad/lexer/string` | 9.878 us | 26.426 us | 28.023 us |
| `lokad/lexer/operator-run` | 0.935 us | 1.193 us | 1.272 us |
| `lokad/lexer/doc-line` | 1.267 us | 1.436 us | 1.605 us |
| `lokad/docs/inline-doc-prefix` | 1.407 us | 1.502 us | 1.895 us |
| `lokad/langserv/identifier-validator` | 2.990 us | 5.160 us | 6.138 us |
| `lokad/langserv/helper-identifier` | 2.008 us | 3.069 us | 3.231 us |
| `lokad/langserv/color-short-hex` | 0.370 us | 0.512 us | 0.791 us |
| `lokad/langserv/color-rgb-hex` | 0.461 us | 0.971 us | 1.282 us |
| `lokad/langserv/url-dashboard` | 1.042 us | 4.243 us | 4.506 us |
| `lokad/langserv/url-download` | 5.035 us | 7.171 us | 7.348 us |
| `lokad/style/hex-color` | 0.388 us | 0.263 us | 0.293 us |
| `lokad/style/cell-ref` | 0.467 us | 0.974 us | 1.062 us |
| `lokad/style/range-ref` | 1.610 us | 0.771 us | 0.854 us |
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
| `literal/identifier-token` | 3.947 us | 3.103 us | 8.507 us |
| `literal/call-token` | 5.896 us | 2.220 us | 7.919 us |
| `literal/identifier-token-casei` | 5.421 us | 4.514 us | 10.681 us |
| `literal-family/type-token-family` | 8.300 us | 5.961 us | 11.863 us |
| `literal-family/method-token-family` | 5.174 us | 3.551 us | 9.366 us |
| `structural/keyword-to-generic-type` | 3.145 us | 2.439 us | 8.329 us |
| `structural/keyword-family-to-capitalized-identifier` | 47.051 us | 41.578 us | 48.781 us |
| `structural/method-family-call` | 2.760 us | 3.262 us | 9.888 us |
| `structural/ordered-keyword-pair` | 3.358 us | 2.596 us | 9.063 us |
| `structural/ordered-identifier-window` | 3.649 us | 5.928 us | 11.695 us |
| `structural/modifier-family-to-type-window` | 13.774 us | 56.030 us | 62.857 us |
| `structural/ordered-keyword-window` | 3.808 us | 4.977 us | 10.981 us |
| `structural/method-family-generic-call` | 2.684 us | 2.853 us | 10.159 us |
| `fallback/lookahead` | 3.038 us | 2.517 us | 8.791 us |
| `balancing/begin-end-blocks` | 273.350 us | 261.577 us | 269.409 us |
| `structural/ordered-modifier-pair` | 3.442 us | 4.465 us | 10.665 us |
| `structural/modifier-family-to-type-pair` | 11.705 us | 27.961 us | 33.099 us |
| `lokad/imports/module-imports` | 2.505 us | 2.760 us | 7.805 us |
| `lokad/folding/region-marker` | 3.570 us | 21.600 us | 25.375 us |
| `lokad/lexer/identifier` | 7.291 us | 10.164 us | 10.755 us |
| `lokad/lexer/number` | 2.784 us | 4.327 us | 4.745 us |
| `lokad/lexer/string` | 6.164 us | 25.621 us | 25.986 us |
| `lokad/lexer/operator-run` | 0.886 us | 1.165 us | 1.241 us |
| `lokad/lexer/doc-line` | 1.006 us | 1.462 us | 1.647 us |
| `lokad/docs/inline-doc-prefix` | 1.112 us | 1.649 us | 1.863 us |
| `lokad/langserv/identifier-validator` | 2.916 us | 5.461 us | 6.228 us |
| `lokad/langserv/helper-identifier` | 2.008 us | 3.171 us | 3.959 us |
| `lokad/langserv/color-short-hex` | 0.475 us | 0.559 us | 0.609 us |
| `lokad/langserv/color-rgb-hex` | 0.460 us | 1.105 us | 1.197 us |
| `lokad/langserv/url-dashboard` | 0.853 us | 4.142 us | 4.444 us |
| `lokad/langserv/url-download` | 1.181 us | 7.268 us | 7.349 us |
| `lokad/style/hex-color` | 0.391 us | 0.272 us | 0.311 us |
| `lokad/style/cell-ref` | 0.467 us | 1.115 us | 1.193 us |
| `lokad/style/range-ref` | 0.364 us | 0.843 us | 0.850 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
