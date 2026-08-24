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

Predecoded .NET is the primary CPU parity comparator. `.NET + decode` is a secondary end-to-end indicator.

All stress rows below are for `Count(...)`.
Ignore-case `sherlock-casei-*` rows use `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`.

### Stress Count Workloads

| Case | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---:|---:|---:|
| `literal/sherlock-en` | 37.320 us | 59.360 us | 300.640 us |
| `literal/sherlock-casei-en` | 46.400 us | 91.440 us | 346.345 us |
| `literal/sherlock-ru` | 135.220 us | 63.975 us | 2,936.920 us |
| `literal/sherlock-casei-ru` | 156.020 us | 221.960 us | 3,050.170 us |
| `literal/sherlock-zh` | 75.980 us | 15.350 us | 1,200.325 us |
| `literal-alternate/sherlock-en` | 189.430 us | 889.845 us | 1,182.335 us |
| `literal-alternate/sherlock-en-nomatch` | 80.720 us | 382.690 us | 587.420 us |
| `literal-alternate/sherlock-en-mixed` | 98.090 us | 360.225 us | 584.590 us |
| `literal-alternate/sherlock-casei-en` | 102.010 us | 6,626.390 us | 6,831.040 us |
| `literal-alternate/sherlock-ru` | 989.840 us | 3,261.630 us | 6,680.870 us |
| `literal-alternate/sherlock-casei-ru` | 804.150 us | 14,984.220 us | 17,745.025 us |
| `literal-alternate/sherlock-zh` | 126.880 us | 47.515 us | 1,240.655 us |
| `bounded-repeat/letters-en` | 457.630 us | 1,563.585 us | 1,611.130 us |
| `bounded-repeat/letters-ru` | 579.175 us | 3,540.370 us | 3,998.585 us |
| `bounded-repeat/context` | 22,982.495 us | 84,661.705 us | 84,505.605 us |
| `bounded-repeat/capitals` | 114.970 us | 26,212.255 us | 28,774.240 us |
| `dictionary/single` | 5,825.490 us | 17,045.710 us | 16,939.550 us |
| `aws-keys/full` | 1,117.895 us | 53,639.510 us | 63,907.025 us |
| `aws-keys/quick` | 3,114.530 us | 6,563.045 us | 16,632.210 us |
| `ruff-noqa/real` | 2,174.260 us | 45,059.240 us | 55,125.895 us |
| `ruff-noqa/tweaked` | 3,180.910 us | 5,102.060 us | 15,604.845 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.082 us | 0.311 us | 0.327 us |
| `common/email-miss` | `IsMatch` | 0.042 us | 0.192 us | 0.215 us |
| `common/date-match` | `IsMatch` | 0.078 us | 0.112 us | 0.134 us |
| `common/date-miss` | `IsMatch` | 0.058 us | 0.326 us | 0.373 us |
| `common/ip-match` | `IsMatch` | 0.012 us | 0.098 us | 0.110 us |
| `common/ip-miss` | `IsMatch` | 0.012 us | 0.126 us | 0.140 us |
| `common/uri-match` | `IsMatch` | 0.021 us | 0.131 us | 0.151 us |
| `common/uri-miss` | `IsMatch` | 0.054 us | 0.189 us | 0.202 us |
| `common/matches-set` | `Count` | 4.782 us | 109.182 us | 109.603 us |
| `common/matches-boundary` | `Count` | 5.200 us | 94.762 us | 94.962 us |
| `common/matches-word` | `Count` | 0.818 us | 2.595 us | 3.934 us |
| `common/matches-words` | `Count` | 4.308 us | 41.725 us | 43.841 us |
| `common/match-word` | `Match` | 0.341 us | 0.096 us | 0.863 us |
| `common/replace-words` | `Replace` | 47.756 us | 46.897 us | 48.029 us |
| `common/split-words` | `Split` | 9.907 us | 49.342 us | 50.061 us |
| `common/backtracking` | `IsMatch` | 0.013 us | 0.531 us | 0.552 us |
| `common/one-node-backtracking` | `IsMatch` | 0.108 us | 0.360 us | 0.395 us |
| `industry/mariomka-email-count` | `Count` | 893.660 us | 284.890 us | 5,737.960 us |
| `industry/mariomka-uri-count` | `Count` | 1,187.985 us | 1,242.050 us | 7,404.190 us |
| `industry/mariomka-ip-count` | `Count` | 1,824.800 us | 5,033.010 us | 9,931.020 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,086.050 us | 11,588.785 us | 11,727.325 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 34.789 us | 153.624 us | 278.592 us |
| `industry/rust-sherlock-ing-count` | `Count` | 146.775 us | 6,859.935 us | 7,017.960 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 15.315 us | 4,590.530 us | 4,811.805 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 89.546 us | 586.837 us | 709.028 us |
| `industry/leipzig-twain-count` | `Count` | 735.615 us | 1,342.280 us | 6,356.585 us |
| `industry/leipzig-name-family-count` | `Count` | 1,900.345 us | 6,086.520 us | 10,905.570 us |
| `industry/leipzig-river-window-count` | `Count` | 6,490.630 us | 21,346.275 us | 26,630.385 us |
| `industry/leipzig-symbol-count` | `Count` | 1,164.275 us | 12,406.570 us | 17,701.575 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.054 us | 0.105 us | 0.129 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.155 us | 0.162 us | 0.169 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.078 us | 0.084 us | 0.092 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.010 us | 0.047 us | 0.058 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.033 us | 0.069 us | 0.082 us |
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
| `literal/sherlock-en` | 38.690 us | 45.360 us | 279.415 us |
| `literal/sherlock-casei-en` | 46.520 us | 64.090 us | 317.370 us |
| `literal/sherlock-ru` | 135.285 us | 53.165 us | 2,871.750 us |
| `literal/sherlock-casei-ru` | 154.295 us | 119.685 us | 2,935.240 us |
| `literal/sherlock-zh` | 77.305 us | 17.085 us | 1,217.315 us |
| `literal-alternate/sherlock-en` | 191.340 us | 211.260 us | 462.655 us |
| `literal-alternate/sherlock-en-nomatch` | 79.250 us | 108.785 us | 347.315 us |
| `literal-alternate/sherlock-en-mixed` | 97.060 us | 107.300 us | 334.685 us |
| `literal-alternate/sherlock-casei-en` | 101.645 us | 112.485 us | 361.380 us |
| `literal-alternate/sherlock-ru` | 1,000.980 us | 1,255.825 us | 4,337.605 us |
| `literal-alternate/sherlock-casei-ru` | 784.000 us | 1,208.200 us | 4,123.440 us |
| `literal-alternate/sherlock-zh` | 131.520 us | 27.980 us | 1,259.150 us |
| `bounded-repeat/letters-en` | 457.130 us | 1,035.565 us | 1,074.710 us |
| `bounded-repeat/letters-ru` | 576.000 us | 1,188.625 us | 1,634.560 us |
| `bounded-repeat/context` | 22,695.695 us | 36,931.145 us | 40,699.305 us |
| `bounded-repeat/capitals` | 123.920 us | 7,843.695 us | 9,778.400 us |
| `dictionary/single` | 6,002.640 us | 39,236.800 us | 40,003.640 us |
| `aws-keys/full` | 1,094.645 us | 11,824.485 us | 22,525.180 us |
| `aws-keys/quick` | 3,080.245 us | 3,072.375 us | 13,064.815 us |
| `ruff-noqa/real` | 2,179.170 us | 12,410.280 us | 22,529.105 us |
| `ruff-noqa/tweaked` | 3,379.220 us | 4,175.275 us | 15,028.935 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.068 us | 0.094 us | 0.109 us |
| `common/email-miss` | `IsMatch` | 0.043 us | 0.119 us | 0.147 us |
| `common/date-match` | `IsMatch` | 0.080 us | 0.234 us | 0.248 us |
| `common/date-miss` | `IsMatch` | 0.057 us | 0.114 us | 0.137 us |
| `common/ip-match` | `IsMatch` | 0.012 us | 0.033 us | 0.041 us |
| `common/ip-miss` | `IsMatch` | 0.012 us | 0.032 us | 0.042 us |
| `common/uri-match` | `IsMatch` | 0.037 us | 0.053 us | 0.073 us |
| `common/uri-miss` | `IsMatch` | 0.055 us | 0.055 us | 0.069 us |
| `common/matches-set` | `Count` | 4.858 us | 54.161 us | 55.632 us |
| `common/matches-boundary` | `Count` | 5.151 us | 53.382 us | 54.005 us |
| `common/matches-word` | `Count` | 0.828 us | 1.262 us | 2.437 us |
| `common/matches-words` | `Count` | 4.333 us | 10.131 us | 11.459 us |
| `common/match-word` | `Match` | 0.346 us | 0.050 us | 0.720 us |
| `common/replace-words` | `Replace` | 14.613 us | 14.630 us | 14.868 us |
| `common/split-words` | `Split` | 9.576 us | 14.752 us | 14.592 us |
| `common/backtracking` | `IsMatch` | 0.013 us | 0.028 us | 0.045 us |
| `common/one-node-backtracking` | `IsMatch` | 0.110 us | 0.233 us | 0.265 us |
| `industry/mariomka-email-count` | `Count` | 928.785 us | 248.145 us | 5,952.420 us |
| `industry/mariomka-uri-count` | `Count` | 1,088.820 us | 867.740 us | 6,092.450 us |
| `industry/mariomka-ip-count` | `Count` | 1,822.080 us | 2,175.360 us | 7,098.180 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,129.775 us | 4,640.010 us | 5,019.195 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 34.232 us | 35.973 us | 151.620 us |
| `industry/rust-sherlock-ing-count` | `Count` | 148.135 us | 2,818.325 us | 3,154.965 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 16.935 us | 2,321.720 us | 2,480.790 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 93.301 us | 522.667 us | 663.024 us |
| `industry/leipzig-twain-count` | `Count` | 744.155 us | 1,303.320 us | 6,316.620 us |
| `industry/leipzig-name-family-count` | `Count` | 1,914.825 us | 2,141.075 us | 6,962.170 us |
| `industry/leipzig-river-window-count` | `Count` | 6,562.410 us | 6,616.195 us | 11,591.330 us |
| `industry/leipzig-symbol-count` | `Count` | 1,204.060 us | 1,180.960 us | 6,426.605 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.055 us | 0.044 us | 0.066 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.038 us | 0.047 us | 0.061 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.029 us | 0.034 us | 0.047 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.012 us | 0.021 us | 0.031 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.034 us | 0.035 us | 0.046 us |
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
| `literal/identifier-token` | 1.356 us | 3.182 us | 8.357 us |
| `literal/call-token` | 2.955 us | 3.005 us | 9.089 us |
| `literal/identifier-token-casei` | 3.378 us | 8.053 us | 13.869 us |
| `literal-family/type-token-family` | 5.283 us | 15.789 us | 22.301 us |
| `literal-family/method-token-family` | 4.994 us | 5.858 us | 11.869 us |
| `structural/keyword-to-generic-type` | 2.119 us | 3.150 us | 8.729 us |
| `structural/keyword-family-to-capitalized-identifier` | 36.304 us | 131.928 us | 144.025 us |
| `structural/method-family-call` | 2.786 us | 5.303 us | 11.897 us |
| `structural/ordered-keyword-pair` | 2.481 us | 3.070 us | 8.844 us |
| `structural/ordered-identifier-window` | 4.323 us | 18.431 us | 24.803 us |
| `structural/modifier-family-to-type-window` | 14.075 us | 164.592 us | 164.381 us |
| `structural/ordered-keyword-window` | 3.764 us | 11.366 us | 18.346 us |
| `structural/method-family-generic-call` | 2.642 us | 4.333 us | 10.937 us |
| `fallback/lookahead` | 2.075 us | 4.022 us | 9.946 us |
| `balancing/begin-end-blocks` | 4.825 us | 2,430.888 us | 2,438.179 us |
| `structural/ordered-modifier-pair` | 3.229 us | 9.876 us | 15.715 us |
| `structural/modifier-family-to-type-pair` | 11.449 us | 77.155 us | 83.332 us |
| `lokad/imports/module-imports` | 2.590 us | 2.810 us | 8.275 us |
| `lokad/folding/region-marker` | 3.400 us | 20.375 us | 25.545 us |
| `lokad/lexer/identifier` | 6.631 us | 9.191 us | 9.909 us |
| `lokad/lexer/number` | 2.379 us | 3.661 us | 4.104 us |
| `lokad/lexer/string` | 6.019 us | 23.996 us | 26.203 us |
| `lokad/lexer/operator-run` | 0.822 us | 1.129 us | 1.284 us |
| `lokad/lexer/doc-line` | 0.969 us | 1.330 us | 1.517 us |
| `lokad/docs/inline-doc-prefix` | 0.986 us | 1.618 us | 1.900 us |
| `lokad/langserv/identifier-validator` | 3.271 us | 5.187 us | 5.883 us |
| `lokad/langserv/helper-identifier` | 1.753 us | 3.227 us | 2.987 us |
| `lokad/langserv/color-short-hex` | 0.541 us | 0.568 us | 0.791 us |
| `lokad/langserv/color-rgb-hex` | 0.670 us | 0.999 us | 1.121 us |
| `lokad/langserv/url-dashboard` | 0.857 us | 4.313 us | 4.498 us |
| `lokad/langserv/url-download` | 1.209 us | 7.502 us | 7.668 us |
| `lokad/style/hex-color` | 0.180 us | 0.279 us | 0.285 us |
| `lokad/style/cell-ref` | 0.434 us | 1.050 us | 1.048 us |
| `lokad/style/range-ref` | 0.270 us | 0.787 us | 0.777 us |
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
| `literal/identifier-token` | 1.372 us | 2.309 us | 7.803 us |
| `literal/call-token` | 2.995 us | 2.264 us | 8.312 us |
| `literal/identifier-token-casei` | 3.437 us | 4.504 us | 9.848 us |
| `literal-family/type-token-family` | 5.344 us | 6.030 us | 12.286 us |
| `literal-family/method-token-family` | 2.832 us | 3.491 us | 9.635 us |
| `structural/keyword-to-generic-type` | 2.109 us | 2.441 us | 7.984 us |
| `structural/keyword-family-to-capitalized-identifier` | 36.567 us | 42.864 us | 50.004 us |
| `structural/method-family-call` | 2.824 us | 3.524 us | 9.832 us |
| `structural/ordered-keyword-pair` | 2.267 us | 2.562 us | 8.185 us |
| `structural/ordered-identifier-window` | 3.659 us | 6.742 us | 11.743 us |
| `structural/modifier-family-to-type-window` | 13.524 us | 57.715 us | 66.023 us |
| `structural/ordered-keyword-window` | 3.827 us | 5.220 us | 11.857 us |
| `structural/method-family-generic-call` | 2.660 us | 3.102 us | 9.322 us |
| `fallback/lookahead` | 2.089 us | 2.493 us | 8.224 us |
| `balancing/begin-end-blocks` | 4.971 us | 263.179 us | 270.546 us |
| `structural/ordered-modifier-pair` | 3.215 us | 4.523 us | 9.933 us |
| `structural/modifier-family-to-type-pair` | 11.314 us | 26.395 us | 32.253 us |
| `lokad/imports/module-imports` | 2.550 us | 2.785 us | 8.445 us |
| `lokad/folding/region-marker` | 3.400 us | 20.560 us | 26.095 us |
| `lokad/lexer/identifier` | 6.589 us | 8.853 us | 9.838 us |
| `lokad/lexer/number` | 2.409 us | 3.636 us | 4.080 us |
| `lokad/lexer/string` | 5.782 us | 23.717 us | 25.572 us |
| `lokad/lexer/operator-run` | 0.945 us | 1.137 us | 1.208 us |
| `lokad/lexer/doc-line` | 0.961 us | 1.402 us | 1.484 us |
| `lokad/docs/inline-doc-prefix` | 0.996 us | 1.568 us | 1.806 us |
| `lokad/langserv/identifier-validator` | 3.130 us | 5.158 us | 5.660 us |
| `lokad/langserv/helper-identifier` | 1.803 us | 2.814 us | 3.194 us |
| `lokad/langserv/color-short-hex` | 0.539 us | 0.578 us | 0.662 us |
| `lokad/langserv/color-rgb-hex` | 0.673 us | 0.907 us | 1.027 us |
| `lokad/langserv/url-dashboard` | 0.864 us | 4.146 us | 4.563 us |
| `lokad/langserv/url-download` | 1.282 us | 7.290 us | 7.413 us |
| `lokad/style/hex-color` | 0.231 us | 0.258 us | 0.279 us |
| `lokad/style/cell-ref` | 0.444 us | 1.011 us | 1.023 us |
| `lokad/style/range-ref` | 0.274 us | 0.742 us | 0.831 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
