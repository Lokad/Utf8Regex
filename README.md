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
| `literal/sherlock-en` | 162.000 us | 281.720 us | 1,221.980 us |
| `literal/sherlock-casei-en` | 374.160 us | 254.620 us | 1,267.760 us |
| `literal/sherlock-ru` | 3,259.840 us | 191.220 us | 5,908.320 us |
| `literal/sherlock-casei-ru` | 8,359.842 us | 551.756 us | 6,187.667 us |
| `literal/sherlock-zh` | 1,037.320 us | 50.880 us | 2,491.920 us |
| `literal-alternate/sherlock-en` | 1,113.340 us | 2,717.980 us | 3,666.860 us |
| `literal-alternate/sherlock-en-nomatch` | 466.600 us | 1,157.860 us | 2,116.880 us |
| `literal-alternate/sherlock-en-mixed` | 725.820 us | 1,281.840 us | 2,128.200 us |
| `literal-alternate/sherlock-casei-en` | 9,596.960 us | 18,535.760 us | 17,276.360 us |
| `literal-alternate/sherlock-ru` | 6,795.860 us | 11,419.820 us | 17,284.880 us |
| `literal-alternate/sherlock-casei-ru` | 42,489.568 us | 35,158.292 us | 42,175.430 us |
| `literal-alternate/sherlock-zh` | 1,689.977 us | 159.924 us | 2,414.127 us |
| `bounded-repeat/letters-en` | 2,524.180 us | 6,091.920 us | 7,033.340 us |
| `bounded-repeat/letters-ru` | 1,134.040 us | 20,393.300 us | 11,527.800 us |
| `bounded-repeat/context` | 55,630.060 us | 235,888.300 us | 237,327.300 us |
| `bounded-repeat/capitals` | 605.640 us | 72,003.460 us | 74,098.020 us |
| `dictionary/single` | 15,207.140 us | 47,242.880 us | 45,104.860 us |
| `aws-keys/full` | 3,863.940 us | 136,222.200 us | 163,992.080 us |
| `aws-keys/quick` | 12,111.660 us | 17,081.720 us | 36,628.600 us |
| `ruff-noqa/real` | 4,218.780 us | 101,483.080 us | 115,395.840 us |
| `ruff-noqa/tweaked` | 8,110.240 us | 14,553.140 us | 26,733.740 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.620 us | 0.415 us | 0.445 us |
| `common/email-miss` | `IsMatch` | 0.657 us | 0.620 us | 0.662 us |
| `common/date-match` | `IsMatch` | 0.529 us | 0.200 us | 0.246 us |
| `common/date-miss` | `IsMatch` | 1.781 us | 1.388 us | 1.299 us |
| `common/ip-match` | `IsMatch` | 0.326 us | 0.340 us | 0.366 us |
| `common/ip-miss` | `IsMatch` | 0.336 us | 0.337 us | 0.385 us |
| `common/uri-match` | `IsMatch` | 0.948 us | 0.393 us | 0.455 us |
| `common/uri-miss` | `IsMatch` | 0.548 us | 0.346 us | 0.402 us |
| `common/matches-set` | `Count` | 16.653 us | 494.038 us | 268.460 us |
| `common/matches-boundary` | `Count` | 17.605 us | 503.021 us | 274.597 us |
| `common/matches-word` | `Count` | 7.390 us | 10.606 us | 18.463 us |
| `common/matches-words` | `Count` | 32.593 us | 140.441 us | 146.190 us |
| `common/match-word` | `Match` | 0.802 us | 0.339 us | 2.028 us |
| `common/replace-words` | `Replace` | 184.790 us | 116.060 us | 114.678 us |
| `common/split-words` | `Split` | 190.430 us | 115.802 us | 112.834 us |
| `common/backtracking` | `IsMatch` | 0.197 us | 1.528 us | 1.655 us |
| `common/one-node-backtracking` | `IsMatch` | 0.326 us | 1.053 us | 1.132 us |
| `industry/mariomka-email-count` | `Count` | 3,220.956 us | 878.118 us | 12,584.236 us |
| `industry/mariomka-uri-count` | `Count` | 3,737.988 us | 2,879.194 us | 12,945.246 us |
| `industry/mariomka-ip-count` | `Count` | 5,741.404 us | 12,295.925 us | 21,910.386 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,953.930 us | 23,881.804 us | 23,956.355 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 326.159 us | 520.624 us | 977.209 us |
| `industry/rust-sherlock-ing-count` | `Count` | 511.784 us | 14,514.400 us | 14,853.077 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 55.848 us | 11,246.374 us | 11,153.037 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 187.978 us | 1,368.658 us | 1,825.452 us |
| `industry/leipzig-twain-count` | `Count` | 2,271.859 us | 2,376.895 us | 9,205.381 us |
| `industry/leipzig-name-family-count` | `Count` | 4,350.771 us | 13,685.295 us | 20,464.993 us |
| `industry/leipzig-river-window-count` | `Count` | 11,817.110 us | 51,539.364 us | 56,626.382 us |
| `industry/leipzig-symbol-count` | `Count` | 3,483.919 us | 22,148.081 us | 29,140.508 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.285 us | 0.223 us | 0.359 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.324 us | 0.310 us | 0.381 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.794 us | 0.137 us | 0.169 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.644 us | 0.139 us | 0.185 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.251 us | 0.135 us | 0.163 us |
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
| `literal/sherlock-en` | 145.940 us | 152.600 us | 1,027.160 us |
| `literal/sherlock-casei-en` | 385.000 us | 124.320 us | 1,030.200 us |
| `literal/sherlock-ru` | 2,478.060 us | 146.220 us | 5,804.680 us |
| `literal/sherlock-casei-ru` | 8,326.845 us | 252.049 us | 6,069.783 us |
| `literal/sherlock-zh` | 1,029.980 us | 52.600 us | 2,420.440 us |
| `literal-alternate/sherlock-en` | 1,087.520 us | 1,368.780 us | 2,282.900 us |
| `literal-alternate/sherlock-en-nomatch` | 439.920 us | 215.520 us | 1,044.360 us |
| `literal-alternate/sherlock-en-mixed` | 620.100 us | 220.440 us | 1,078.540 us |
| `literal-alternate/sherlock-casei-en` | 2,523.180 us | 1,722.620 us | 3,152.360 us |
| `literal-alternate/sherlock-ru` | 3,753.620 us | 2,367.640 us | 8,874.800 us |
| `literal-alternate/sherlock-casei-ru` | 9,868.287 us | 1,988.016 us | 7,807.837 us |
| `literal-alternate/sherlock-zh` | 1,582.044 us | 90.160 us | 2,285.439 us |
| `bounded-repeat/letters-en` | 2,381.880 us | 5,641.240 us | 5,411.960 us |
| `bounded-repeat/letters-ru` | 1,121.040 us | 5,941.840 us | 7,186.360 us |
| `bounded-repeat/context` | 45,701.660 us | 59,173.360 us | 63,855.440 us |
| `bounded-repeat/capitals` | 517.380 us | 13,934.820 us | 20,004.700 us |
| `dictionary/single` | 11,598.280 us | 78,731.400 us | 78,549.660 us |
| `aws-keys/full` | 3,082.240 us | 18,038.640 us | 44,193.800 us |
| `aws-keys/quick` | 8,204.820 us | 5,409.720 us | 31,621.960 us |
| `ruff-noqa/real` | 3,152.640 us | 23,950.060 us | 50,331.720 us |
| `ruff-noqa/tweaked` | 7,944.100 us | 8,105.040 us | 29,929.520 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.616 us | 0.264 us | 0.326 us |
| `common/email-miss` | `IsMatch` | 0.646 us | 0.277 us | 0.329 us |
| `common/date-match` | `IsMatch` | 0.572 us | 0.128 us | 0.170 us |
| `common/date-miss` | `IsMatch` | 1.676 us | 0.750 us | 0.818 us |
| `common/ip-match` | `IsMatch` | 0.328 us | 0.092 us | 0.118 us |
| `common/ip-miss` | `IsMatch` | 0.333 us | 0.090 us | 0.118 us |
| `common/uri-match` | `IsMatch` | 0.731 us | 0.140 us | 0.187 us |
| `common/uri-miss` | `IsMatch` | 0.528 us | 0.110 us | 0.139 us |
| `common/matches-set` | `Count` | 15.496 us | 361.779 us | 121.451 us |
| `common/matches-boundary` | `Count` | 15.703 us | 319.512 us | 111.790 us |
| `common/matches-word` | `Count` | 7.095 us | 4.311 us | 5.938 us |
| `common/matches-words` | `Count` | 32.708 us | 18.595 us | 31.954 us |
| `common/match-word` | `Match` | 0.707 us | 0.128 us | 2.005 us |
| `common/replace-words` | `Replace` | 54.890 us | 24.098 us | 40.650 us |
| `common/split-words` | `Split` | 56.500 us | 23.939 us | 33.124 us |
| `common/backtracking` | `IsMatch` | 0.200 us | 0.092 us | 0.134 us |
| `common/one-node-backtracking` | `IsMatch` | 0.317 us | 0.103 us | 0.150 us |
| `industry/mariomka-email-count` | `Count` | 2,914.005 us | 655.523 us | 10,787.569 us |
| `industry/mariomka-uri-count` | `Count` | 3,529.226 us | 1,525.893 us | 11,755.138 us |
| `industry/mariomka-ip-count` | `Count` | 5,249.826 us | 4,209.686 us | 14,291.237 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,990.654 us | 12,506.863 us | 12,872.643 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 294.859 us | 99.642 us | 550.776 us |
| `industry/rust-sherlock-ing-count` | `Count` | 291.703 us | 4,849.998 us | 5,154.971 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 55.373 us | 3,824.495 us | 4,254.235 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 186.736 us | 964.347 us | 1,314.323 us |
| `industry/leipzig-twain-count` | `Count` | 2,213.092 us | 2,373.659 us | 9,415.944 us |
| `industry/leipzig-name-family-count` | `Count` | 4,255.648 us | 3,486.898 us | 10,260.238 us |
| `industry/leipzig-river-window-count` | `Count` | 11,572.932 us | 9,022.458 us | 15,682.683 us |
| `industry/leipzig-symbol-count` | `Count` | 3,562.634 us | 2,450.014 us | 9,586.975 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.281 us | 0.116 us | 0.141 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.347 us | 0.129 us | 0.126 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.654 us | 0.064 us | 0.098 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.551 us | 0.067 us | 0.089 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.221 us | 0.072 us | 0.091 us |
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
| `literal/identifier-token` | 14.380 us | 10.960 us | 20.360 us |
| `literal/call-token` | 16.080 us | 9.560 us | 20.120 us |
| `literal/identifier-token-casei` | 34.020 us | 26.628 us | 43.529 us |
| `literal-family/type-token-family` | 27.965 us | 45.567 us | 75.830 us |
| `literal-family/method-token-family` | 74.600 us | 13.672 us | 29.477 us |
| `structural/keyword-to-generic-type` | 16.860 us | 10.020 us | 19.300 us |
| `structural/keyword-family-to-capitalized-identifier` | 274.720 us | 520.640 us | 535.800 us |
| `structural/method-family-call` | 11.016 us | 19.065 us | 28.132 us |
| `structural/ordered-keyword-pair` | 21.480 us | 12.260 us | 22.020 us |
| `structural/ordered-identifier-window` | 23.880 us | 66.440 us | 83.320 us |
| `structural/modifier-family-to-type-window` | 128.860 us | 638.740 us | 648.700 us |
| `structural/ordered-keyword-window` | 25.260 us | 46.940 us | 57.120 us |
| `structural/method-family-generic-call` | 11.749 us | 16.351 us | 27.958 us |
| `fallback/lookahead` | 17.280 us | 13.560 us | 24.400 us |
| `balancing/begin-end-blocks` | 8,159.760 us | 8,439.260 us | 10,338.000 us |
| `structural/ordered-modifier-pair` | 17.200 us | 36.420 us | 47.020 us |
| `structural/modifier-family-to-type-pair` | 111.020 us | 317.840 us | 328.240 us |
| `lokad/imports/module-imports` | 14.170 us | 6.003 us | 34.810 us |
| `lokad/folding/region-marker` | 27.880 us | 171.880 us | 167.720 us |
| `lokad/lexer/identifier` | 45.311 us | 40.030 us | 42.649 us |
| `lokad/lexer/number` | 14.595 us | 11.611 us | 15.575 us |
| `lokad/lexer/string` | 34.658 us | 61.009 us | 70.960 us |
| `lokad/lexer/operator-run` | 5.396 us | 4.674 us | 5.942 us |
| `lokad/lexer/doc-line` | 5.768 us | 4.141 us | 5.780 us |
| `lokad/docs/inline-doc-prefix` | 8.174 us | 4.754 us | 6.731 us |
| `lokad/langserv/identifier-validator` | 18.225 us | 18.153 us | 22.058 us |
| `lokad/langserv/helper-identifier` | 13.838 us | 12.878 us | 15.142 us |
| `lokad/langserv/color-short-hex` | 3.432 us | 1.781 us | 2.366 us |
| `lokad/langserv/color-rgb-hex` | 4.373 us | 2.893 us | 3.471 us |
| `lokad/langserv/url-dashboard` | 12.582 us | 9.881 us | 10.751 us |
| `lokad/langserv/url-download` | 16.239 us | 19.220 us | 18.238 us |
| `lokad/style/hex-color` | 1.529 us | 0.812 us | 1.116 us |
| `lokad/style/cell-ref` | 2.652 us | 2.707 us | 3.434 us |
| `lokad/style/range-ref` | 1.874 us | 2.067 us | 2.291 us |
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
| `literal/identifier-token` | 16.140 us | 6.440 us | 15.700 us |
| `literal/call-token` | 16.400 us | 8.980 us | 19.060 us |
| `literal/identifier-token-casei` | 31.256 us | 10.402 us | 28.168 us |
| `literal-family/type-token-family` | 31.246 us | 10.568 us | 28.621 us |
| `literal-family/method-token-family` | 61.354 us | 6.126 us | 22.467 us |
| `structural/keyword-to-generic-type` | 18.500 us | 6.020 us | 15.820 us |
| `structural/keyword-family-to-capitalized-identifier` | 110.720 us | 81.540 us | 96.720 us |
| `structural/method-family-call` | 5.180 us | 8.527 us | 20.634 us |
| `structural/ordered-keyword-pair` | 23.000 us | 6.640 us | 20.260 us |
| `structural/ordered-identifier-window` | 22.680 us | 25.000 us | 32.580 us |
| `structural/modifier-family-to-type-window` | 127.980 us | 144.060 us | 155.420 us |
| `structural/ordered-keyword-window` | 28.640 us | 18.820 us | 26.920 us |
| `structural/method-family-generic-call` | 10.810 us | 8.829 us | 22.657 us |
| `fallback/lookahead` | 19.120 us | 9.580 us | 19.660 us |
| `balancing/begin-end-blocks` | 695.080 us | 678.840 us | 735.780 us |
| `structural/ordered-modifier-pair` | 17.900 us | 12.040 us | 21.740 us |
| `structural/modifier-family-to-type-pair` | 106.500 us | 61.200 us | 71.840 us |
| `lokad/imports/module-imports` | 13.670 us | 6.131 us | 36.711 us |
| `lokad/folding/region-marker` | 28.480 us | 162.400 us | 171.220 us |
| `lokad/lexer/identifier` | 42.981 us | 30.069 us | 42.675 us |
| `lokad/lexer/number` | 14.251 us | 11.342 us | 15.280 us |
| `lokad/lexer/string` | 33.578 us | 60.945 us | 70.007 us |
| `lokad/lexer/operator-run` | 5.622 us | 4.636 us | 5.888 us |
| `lokad/lexer/doc-line` | 5.825 us | 3.917 us | 5.638 us |
| `lokad/docs/inline-doc-prefix` | 7.646 us | 4.849 us | 6.524 us |
| `lokad/langserv/identifier-validator` | 17.959 us | 15.912 us | 21.072 us |
| `lokad/langserv/helper-identifier` | 13.072 us | 12.079 us | 15.046 us |
| `lokad/langserv/color-short-hex` | 3.041 us | 1.781 us | 2.192 us |
| `lokad/langserv/color-rgb-hex` | 4.004 us | 2.823 us | 3.506 us |
| `lokad/langserv/url-dashboard` | 10.403 us | 9.601 us | 10.633 us |
| `lokad/langserv/url-download` | 15.245 us | 16.673 us | 17.429 us |
| `lokad/style/hex-color` | 1.449 us | 0.855 us | 1.040 us |
| `lokad/style/cell-ref` | 2.629 us | 2.715 us | 3.112 us |
| `lokad/style/range-ref` | 1.993 us | 2.091 us | 2.236 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
