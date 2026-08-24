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
| `literal/sherlock-en` | 91.075 us | 54.820 us | 301.935 us |
| `literal/sherlock-casei-en` | 46.400 us | 91.440 us | 346.345 us |
| `literal/sherlock-ru` | 1,998.505 us | 62.535 us | 3,041.335 us |
| `literal/sherlock-casei-ru` | 3,182.870 us | 222.075 us | 3,211.835 us |
| `literal/sherlock-zh` | 562.255 us | 16.745 us | 1,224.980 us |
| `literal-alternate/sherlock-en` | 189.430 us | 889.845 us | 1,182.335 us |
| `literal-alternate/sherlock-en-nomatch` | 80.720 us | 382.690 us | 587.420 us |
| `literal-alternate/sherlock-en-mixed` | 98.090 us | 360.225 us | 584.590 us |
| `literal-alternate/sherlock-casei-en` | 247.205 us | 6,697.645 us | 7,098.240 us |
| `literal-alternate/sherlock-ru` | 2,786.800 us | 3,311.305 us | 6,141.330 us |
| `literal-alternate/sherlock-casei-ru` | 18,797.290 us | 15,917.325 us | 18,963.595 us |
| `literal-alternate/sherlock-zh` | 941.755 us | 47.410 us | 1,302.345 us |
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
| `common/date-match` | `IsMatch` | 0.116 us | 0.111 us | 0.134 us |
| `common/date-miss` | `IsMatch` | 0.058 us | 0.326 us | 0.373 us |
| `common/ip-match` | `IsMatch` | 0.012 us | 0.098 us | 0.110 us |
| `common/ip-miss` | `IsMatch` | 0.012 us | 0.126 us | 0.140 us |
| `common/uri-match` | `IsMatch` | 0.021 us | 0.131 us | 0.151 us |
| `common/uri-miss` | `IsMatch` | 0.066 us | 0.186 us | 0.203 us |
| `common/matches-set` | `Count` | 4.782 us | 109.182 us | 109.603 us |
| `common/matches-boundary` | `Count` | 5.200 us | 94.762 us | 94.962 us |
| `common/matches-word` | `Count` | 1.995 us | 2.782 us | 3.483 us |
| `common/matches-words` | `Count` | 4.308 us | 41.725 us | 43.841 us |
| `common/match-word` | `Match` | 0.416 us | 0.098 us | 0.753 us |
| `common/replace-words` | `Replace` | 47.756 us | 46.897 us | 48.029 us |
| `common/split-words` | `Split` | 9.907 us | 49.342 us | 50.061 us |
| `common/backtracking` | `IsMatch` | 0.013 us | 0.531 us | 0.552 us |
| `common/one-node-backtracking` | `IsMatch` | 0.255 us | 0.351 us | 0.388 us |
| `industry/mariomka-email-count` | `Count` | 5,275.316 us | 279.074 us | 5,280.228 us |
| `industry/mariomka-uri-count` | `Count` | 5,931.430 us | 1,080.190 us | 6,028.280 us |
| `industry/mariomka-ip-count` | `Count` | 4,894.530 us | 5,072.805 us | 10,121.885 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,086.050 us | 11,588.785 us | 11,727.325 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 34.789 us | 153.624 us | 278.592 us |
| `industry/rust-sherlock-ing-count` | `Count` | 146.775 us | 6,859.935 us | 7,017.960 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 15.315 us | 4,590.530 us | 4,811.805 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 89.546 us | 586.837 us | 709.028 us |
| `industry/leipzig-twain-count` | `Count` | 3,353.450 us | 1,527.160 us | 6,216.910 us |
| `industry/leipzig-name-family-count` | `Count` | 1,900.345 us | 6,086.520 us | 10,905.570 us |
| `industry/leipzig-river-window-count` | `Count` | 6,490.630 us | 21,346.275 us | 26,630.385 us |
| `industry/leipzig-symbol-count` | `Count` | 1,164.275 us | 12,406.570 us | 17,701.575 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.060 us | 0.109 us | 0.124 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.155 us | 0.162 us | 0.169 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.086 us | 0.081 us | 0.093 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.010 us | 0.047 us | 0.058 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.036 us | 0.071 us | 0.086 us |
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
| `literal/sherlock-en` | 90.585 us | 41.465 us | 283.840 us |
| `literal/sherlock-casei-en` | 46.520 us | 64.090 us | 317.370 us |
| `literal/sherlock-ru` | 1,989.115 us | 54.340 us | 3,032.015 us |
| `literal/sherlock-casei-ru` | 3,013.295 us | 119.285 us | 3,132.735 us |
| `literal/sherlock-zh` | 572.780 us | 19.220 us | 1,266.205 us |
| `literal-alternate/sherlock-en` | 191.340 us | 211.260 us | 462.655 us |
| `literal-alternate/sherlock-en-nomatch` | 79.250 us | 108.785 us | 347.315 us |
| `literal-alternate/sherlock-en-mixed` | 97.060 us | 107.300 us | 334.685 us |
| `literal-alternate/sherlock-casei-en` | 233.715 us | 103.945 us | 418.935 us |
| `literal-alternate/sherlock-ru` | 2,796.185 us | 1,381.810 us | 4,354.285 us |
| `literal-alternate/sherlock-casei-ru` | 4,166.555 us | 1,234.755 us | 4,178.580 us |
| `literal-alternate/sherlock-zh` | 915.995 us | 28.325 us | 1,360.875 us |
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
| `common/date-match` | `IsMatch` | 0.119 us | 0.227 us | 0.246 us |
| `common/date-miss` | `IsMatch` | 0.057 us | 0.114 us | 0.137 us |
| `common/ip-match` | `IsMatch` | 0.012 us | 0.033 us | 0.041 us |
| `common/ip-miss` | `IsMatch` | 0.012 us | 0.032 us | 0.042 us |
| `common/uri-match` | `IsMatch` | 0.037 us | 0.053 us | 0.073 us |
| `common/uri-miss` | `IsMatch` | 0.068 us | 0.054 us | 0.067 us |
| `common/matches-set` | `Count` | 4.858 us | 54.161 us | 55.632 us |
| `common/matches-boundary` | `Count` | 5.151 us | 53.382 us | 54.005 us |
| `common/matches-word` | `Count` | 1.954 us | 1.263 us | 2.066 us |
| `common/matches-words` | `Count` | 4.333 us | 10.131 us | 11.459 us |
| `common/match-word` | `Match` | 0.421 us | 0.046 us | 0.664 us |
| `common/replace-words` | `Replace` | 14.613 us | 14.630 us | 14.868 us |
| `common/split-words` | `Split` | 9.576 us | 14.752 us | 14.592 us |
| `common/backtracking` | `IsMatch` | 0.013 us | 0.028 us | 0.045 us |
| `common/one-node-backtracking` | `IsMatch` | 0.257 us | 0.231 us | 0.260 us |
| `industry/mariomka-email-count` | `Count` | 5,280.718 us | 251.636 us | 5,312.416 us |
| `industry/mariomka-uri-count` | `Count` | 5,569.105 us | 633.815 us | 5,519.565 us |
| `industry/mariomka-ip-count` | `Count` | 5,034.210 us | 2,179.545 us | 7,062.355 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,129.775 us | 4,640.010 us | 5,019.195 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 34.232 us | 35.973 us | 151.620 us |
| `industry/rust-sherlock-ing-count` | `Count` | 148.135 us | 2,818.325 us | 3,154.965 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 16.935 us | 2,321.720 us | 2,480.790 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 93.301 us | 522.667 us | 663.024 us |
| `industry/leipzig-twain-count` | `Count` | 3,362.255 us | 1,534.235 us | 6,038.530 us |
| `industry/leipzig-name-family-count` | `Count` | 1,914.825 us | 2,141.075 us | 6,962.170 us |
| `industry/leipzig-river-window-count` | `Count` | 6,562.410 us | 6,616.195 us | 11,591.330 us |
| `industry/leipzig-symbol-count` | `Count` | 1,204.060 us | 1,180.960 us | 6,426.605 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.062 us | 0.046 us | 0.062 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.038 us | 0.047 us | 0.061 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.029 us | 0.035 us | 0.046 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.012 us | 0.021 us | 0.031 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.037 us | 0.035 us | 0.047 us |
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
| `literal/identifier-token` | 2.558 us | 3.238 us | 8.753 us |
| `literal/call-token` | 6.312 us | 2.698 us | 8.534 us |
| `literal/identifier-token-casei` | 5.567 us | 8.521 us | 13.697 us |
| `literal-family/type-token-family` | 8.883 us | 15.979 us | 22.261 us |
| `literal-family/method-token-family` | 5.659 us | 5.698 us | 11.660 us |
| `structural/keyword-to-generic-type` | 3.204 us | 3.302 us | 9.202 us |
| `structural/keyword-family-to-capitalized-identifier` | 45.323 us | 132.100 us | 142.134 us |
| `structural/method-family-call` | 2.786 us | 5.303 us | 11.897 us |
| `structural/ordered-keyword-pair` | 3.425 us | 3.345 us | 9.219 us |
| `structural/ordered-identifier-window` | 4.323 us | 18.431 us | 24.803 us |
| `structural/modifier-family-to-type-window` | 14.075 us | 164.592 us | 164.381 us |
| `structural/ordered-keyword-window` | 3.764 us | 11.366 us | 18.346 us |
| `structural/method-family-generic-call` | 2.642 us | 4.333 us | 10.937 us |
| `fallback/lookahead` | 2.987 us | 3.950 us | 9.545 us |
| `balancing/begin-end-blocks` | 2,415.066 us | 2,382.982 us | 2,382.488 us |
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
| `lokad/langserv/color-short-hex` | 0.564 us | 0.520 us | 0.576 us |
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
| `literal/identifier-token` | 2.444 us | 2.343 us | 8.276 us |
| `literal/call-token` | 6.442 us | 2.416 us | 8.306 us |
| `literal/identifier-token-casei` | 5.707 us | 4.770 us | 10.596 us |
| `literal-family/type-token-family` | 8.580 us | 5.888 us | 12.879 us |
| `literal-family/method-token-family` | 5.813 us | 3.870 us | 9.519 us |
| `structural/keyword-to-generic-type` | 3.375 us | 2.604 us | 8.606 us |
| `structural/keyword-family-to-capitalized-identifier` | 45.065 us | 43.417 us | 52.467 us |
| `structural/method-family-call` | 2.824 us | 3.524 us | 9.832 us |
| `structural/ordered-keyword-pair` | 3.366 us | 2.694 us | 8.655 us |
| `structural/ordered-identifier-window` | 3.659 us | 6.742 us | 11.743 us |
| `structural/modifier-family-to-type-window` | 13.524 us | 57.715 us | 66.023 us |
| `structural/ordered-keyword-window` | 3.827 us | 5.220 us | 11.857 us |
| `structural/method-family-generic-call` | 2.660 us | 3.102 us | 9.322 us |
| `fallback/lookahead` | 3.048 us | 2.479 us | 7.788 us |
| `balancing/begin-end-blocks` | 278.545 us | 261.384 us | 275.908 us |
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
| `lokad/langserv/color-short-hex` | 0.545 us | 0.502 us | 0.632 us |
| `lokad/langserv/color-rgb-hex` | 0.673 us | 0.907 us | 1.027 us |
| `lokad/langserv/url-dashboard` | 0.864 us | 4.146 us | 4.563 us |
| `lokad/langserv/url-download` | 1.282 us | 7.290 us | 7.413 us |
| `lokad/style/hex-color` | 0.231 us | 0.258 us | 0.279 us |
| `lokad/style/cell-ref` | 0.444 us | 1.011 us | 1.023 us |
| `lokad/style/range-ref` | 0.274 us | 0.742 us | 0.831 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
