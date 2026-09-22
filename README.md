# Lokad.Utf8Regex

`Lokad.Utf8Regex` is a `net10.0` regular expression library whose semantic reference is `.NET 10`'s `System.Text.RegularExpressions.Regex`, while its primary I/O surface is UTF-8 `ReadOnlySpan<byte>`.

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
| `literal/sherlock-en` | 60.900 us | 89.200 us | 532.865 us |
| `literal/sherlock-casei-en` | 89.230 us | 189.150 us | 669.015 us |
| `literal/sherlock-ru` | 227.830 us | 97.960 us | 5,040.095 us |
| `literal/sherlock-casei-ru` | 252.575 us | 399.280 us | 5,366.405 us |
| `literal/sherlock-zh` | 121.810 us | 19.825 us | 2,045.805 us |
| `literal-alternate/sherlock-en` | 312.350 us | 1,540.540 us | 2,060.080 us |
| `literal-alternate/sherlock-en-nomatch` | 153.875 us | 662.995 us | 1,092.750 us |
| `literal-alternate/sherlock-en-mixed` | 191.625 us | 628.195 us | 1,132.890 us |
| `literal-alternate/sherlock-casei-en` | 172.295 us | 12,072.620 us | 12,766.455 us |
| `literal-alternate/sherlock-ru` | 1,523.750 us | 4,974.105 us | 9,995.310 us |
| `literal-alternate/sherlock-casei-ru` | 1,032.030 us | 25,377.445 us | 30,495.990 us |
| `literal-alternate/sherlock-zh` | 214.415 us | 72.740 us | 2,030.015 us |
| `bounded-repeat/letters-en` | 727.780 us | 2,763.135 us | 2,851.295 us |
| `bounded-repeat/letters-ru` | 967.825 us | 6,173.830 us | 7,006.330 us |
| `bounded-repeat/context` | 27,865.840 us | 144,744.655 us | 148,429.160 us |
| `bounded-repeat/capitals` | 232.250 us | 44,837.135 us | 48,183.895 us |
| `dictionary/single` | 7,826.570 us | 26,204.900 us | 26,449.180 us |
| `aws-keys/full` | 998.895 us | 89,000.595 us | 104,359.960 us |
| `aws-keys/quick` | 3,483.360 us | 8,976.865 us | 23,252.975 us |
| `ruff-noqa/real` | 2,181.015 us | 66,869.230 us | 82,367.730 us |
| `ruff-noqa/tweaked` | 3,776.710 us | 7,378.755 us | 21,843.700 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.043 us | 0.264 us | 0.290 us |
| `common/email-miss` | `IsMatch` | 0.049 us | 0.355 us | 0.371 us |
| `common/date-match` | `IsMatch` | 0.358 us | 0.200 us | 0.304 us |
| `common/date-miss` | `IsMatch` | 0.086 us | 0.586 us | 0.621 us |
| `common/ip-match` | `IsMatch` | 0.015 us | 0.175 us | 0.191 us |
| `common/ip-miss` | `IsMatch` | 0.016 us | 0.186 us | 0.214 us |
| `common/uri-match` | `IsMatch` | 0.022 us | 0.223 us | 0.256 us |
| `common/uri-miss` | `IsMatch` | 0.088 us | 0.191 us | 0.207 us |
| `common/matches-set` | `Count` | 7.786 us | 196.247 us | 193.381 us |
| `common/matches-boundary` | `Count` | 8.909 us | 162.658 us | 168.787 us |
| `common/matches-word` | `Count` | 1.407 us | 4.094 us | 5.362 us |
| `common/matches-words` | `Count` | 9.693 us | 79.024 us | 80.504 us |
| `common/match-word` | `Match` | 0.145 us | 0.164 us | 1.088 us |
| `common/replace-words` | `Replace` | 42.723 us | 85.304 us | 86.979 us |
| `common/split-words` | `Split` | 11.505 us | 84.363 us | 85.553 us |
| `common/backtracking` | `IsMatch` | 0.066 us | 0.908 us | 0.944 us |
| `common/one-node-backtracking` | `IsMatch` | 0.198 us | 0.627 us | 0.673 us |
| `industry/mariomka-email-count` | `Count` | 1,409.185 us | 506.595 us | 8,099.970 us |
| `industry/mariomka-uri-count` | `Count` | 1,844.575 us | 1,770.130 us | 9,428.890 us |
| `industry/mariomka-ip-count` | `Count` | 2,456.400 us | 7,958.705 us | 15,780.140 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,878.125 us | 18,143.235 us | 18,686.205 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.858 us | 276.456 us | 511.098 us |
| `industry/rust-sherlock-ing-count` | `Count` | 250.250 us | 11,431.110 us | 11,795.130 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 26.160 us | 7,899.825 us | 8,291.715 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 188.435 us | 934.046 us | 1,229.330 us |
| `industry/leipzig-twain-count` | `Count` | 1,200.110 us | 1,284.855 us | 8,227.335 us |
| `industry/leipzig-name-family-count` | `Count` | 3,012.375 us | 10,005.775 us | 16,826.855 us |
| `industry/leipzig-river-window-count` | `Count` | 10,891.195 us | 31,893.515 us | 39,041.570 us |
| `industry/leipzig-symbol-count` | `Count` | 1,980.130 us | 26,283.140 us | 33,812.315 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.114 us | 0.419 us | 0.554 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.335 us | 0.231 us | 0.320 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.084 us | 0.118 us | 0.148 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.098 us | 0.119 us | 0.144 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.038 us | 0.107 us | 0.137 us |
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
| `literal/sherlock-en` | 60.140 us | 77.065 us | 505.945 us |
| `literal/sherlock-casei-en` | 89.710 us | 77.885 us | 557.620 us |
| `literal/sherlock-ru` | 228.390 us | 88.215 us | 5,037.710 us |
| `literal/sherlock-casei-ru` | 253.450 us | 205.030 us | 5,177.820 us |
| `literal/sherlock-zh` | 121.380 us | 24.730 us | 2,048.840 us |
| `literal-alternate/sherlock-en` | 309.015 us | 338.295 us | 820.825 us |
| `literal-alternate/sherlock-en-nomatch` | 154.310 us | 204.595 us | 626.315 us |
| `literal-alternate/sherlock-en-mixed` | 193.170 us | 184.815 us | 687.875 us |
| `literal-alternate/sherlock-casei-en` | 175.940 us | 158.395 us | 655.200 us |
| `literal-alternate/sherlock-ru` | 1,517.365 us | 2,280.480 us | 7,288.275 us |
| `literal-alternate/sherlock-casei-ru` | 1,022.310 us | 1,895.550 us | 6,915.895 us |
| `literal-alternate/sherlock-zh` | 212.385 us | 39.385 us | 1,982.155 us |
| `bounded-repeat/letters-en` | 727.450 us | 1,722.950 us | 1,819.650 us |
| `bounded-repeat/letters-ru` | 956.220 us | 1,980.020 us | 2,762.025 us |
| `bounded-repeat/context` | 27,885.310 us | 62,556.680 us | 66,308.540 us |
| `bounded-repeat/capitals` | 235.890 us | 12,836.635 us | 15,625.345 us |
| `dictionary/single` | 7,844.970 us | 73,213.255 us | 73,141.225 us |
| `aws-keys/full` | 1,008.890 us | 17,124.625 us | 31,676.325 us |
| `aws-keys/quick` | 3,480.665 us | 3,382.830 us | 17,665.190 us |
| `ruff-noqa/real` | 2,193.005 us | 19,028.430 us | 33,924.910 us |
| `ruff-noqa/tweaked` | 3,781.845 us | 4,361.095 us | 18,881.985 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.045 us | 0.080 us | 0.111 us |
| `common/email-miss` | `IsMatch` | 0.050 us | 0.088 us | 0.120 us |
| `common/date-match` | `IsMatch` | 0.363 us | 0.047 us | 0.140 us |
| `common/date-miss` | `IsMatch` | 0.088 us | 0.195 us | 0.226 us |
| `common/ip-match` | `IsMatch` | 0.016 us | 0.051 us | 0.071 us |
| `common/ip-miss` | `IsMatch` | 0.016 us | 0.053 us | 0.071 us |
| `common/uri-match` | `IsMatch` | 0.023 us | 0.087 us | 0.115 us |
| `common/uri-miss` | `IsMatch` | 0.093 us | 0.069 us | 0.081 us |
| `common/matches-set` | `Count` | 7.744 us | 98.984 us | 100.956 us |
| `common/matches-boundary` | `Count` | 9.113 us | 97.873 us | 99.644 us |
| `common/matches-word` | `Count` | 1.399 us | 2.225 us | 3.431 us |
| `common/matches-words` | `Count` | 9.699 us | 13.125 us | 14.395 us |
| `common/match-word` | `Match` | 0.148 us | 0.074 us | 1.003 us |
| `common/replace-words` | `Replace` | 42.075 us | 18.086 us | 19.544 us |
| `common/split-words` | `Split` | 11.488 us | 18.726 us | 20.451 us |
| `common/backtracking` | `IsMatch` | 0.071 us | 0.046 us | 0.071 us |
| `common/one-node-backtracking` | `IsMatch` | 0.202 us | 0.091 us | 0.130 us |
| `industry/mariomka-email-count` | `Count` | 1,405.820 us | 474.550 us | 7,768.705 us |
| `industry/mariomka-uri-count` | `Count` | 1,845.805 us | 1,036.875 us | 8,417.435 us |
| `industry/mariomka-ip-count` | `Count` | 2,461.215 us | 3,347.850 us | 10,776.965 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,876.140 us | 8,239.370 us | 8,678.345 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 66.925 us | 59.678 us | 299.704 us |
| `industry/rust-sherlock-ing-count` | `Count` | 249.435 us | 4,459.115 us | 4,838.490 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 25.180 us | 3,766.880 us | 4,093.780 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 186.952 us | 593.259 us | 882.420 us |
| `industry/leipzig-twain-count` | `Count` | 1,211.715 us | 1,309.350 us | 7,455.540 us |
| `industry/leipzig-name-family-count` | `Count` | 3,061.710 us | 2,808.715 us | 8,911.430 us |
| `industry/leipzig-river-window-count` | `Count` | 10,937.875 us | 8,943.845 us | 15,670.135 us |
| `industry/leipzig-symbol-count` | `Count` | 1,988.860 us | 1,180.420 us | 7,577.030 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.118 us | 0.121 us | 0.205 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.046 us | 0.064 us | 0.152 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.039 us | 0.058 us | 0.086 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.049 us | 0.060 us | 0.087 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.043 us | 0.040 us | 0.085 us |
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
| `literal/identifier-token` | 2.246 us | 4.130 us | 14.804 us |
| `literal/call-token` | 3.455 us | 4.121 us | 14.814 us |
| `literal/identifier-token-casei` | 5.537 us | 15.379 us | 26.008 us |
| `literal-family/type-token-family` | 11.021 us | 25.336 us | 35.851 us |
| `literal-family/method-token-family` | 8.261 us | 9.015 us | 20.043 us |
| `structural/keyword-to-generic-type` | 4.215 us | 3.881 us | 15.134 us |
| `structural/keyword-family-to-capitalized-identifier` | 77.915 us | 217.399 us | 232.752 us |
| `structural/method-family-call` | 4.302 us | 7.806 us | 18.846 us |
| `structural/ordered-keyword-pair` | 4.837 us | 5.189 us | 16.162 us |
| `structural/ordered-identifier-window` | 4.934 us | 29.082 us | 40.259 us |
| `structural/modifier-family-to-type-window` | 25.808 us | 261.979 us | 275.138 us |
| `structural/ordered-keyword-window` | 5.088 us | 18.215 us | 29.339 us |
| `structural/method-family-generic-call` | 3.907 us | 6.293 us | 18.370 us |
| `fallback/lookahead` | 4.432 us | 5.846 us | 16.363 us |
| `balancing/begin-end-blocks` | 9.050 us | 4,285.680 us | 4,288.485 us |
| `structural/ordered-modifier-pair` | 4.223 us | 16.939 us | 27.966 us |
| `structural/modifier-family-to-type-pair` | 23.696 us | 133.736 us | 145.565 us |
| `lokad/imports/module-imports` | 3.430 us | 4.375 us | 14.180 us |
| `lokad/folding/region-marker` | 5.745 us | 39.765 us | 48.690 us |
| `lokad/lexer/identifier` | 11.159 us | 16.452 us | 20.212 us |
| `lokad/lexer/number` | 4.174 us | 7.310 us | 8.451 us |
| `lokad/lexer/string` | 9.983 us | 44.753 us | 48.678 us |
| `lokad/lexer/operator-run` | 1.421 us | 1.897 us | 2.519 us |
| `lokad/lexer/doc-line` | 1.620 us | 2.023 us | 2.913 us |
| `lokad/docs/inline-doc-prefix` | 1.741 us | 2.718 us | 3.595 us |
| `lokad/langserv/identifier-validator` | 3.910 us | 9.021 us | 11.058 us |
| `lokad/langserv/helper-identifier` | 3.440 us | 4.223 us | 5.394 us |
| `lokad/langserv/color-short-hex` | 0.679 us | 0.928 us | 1.142 us |
| `lokad/langserv/color-rgb-hex` | 0.846 us | 1.757 us | 1.982 us |
| `lokad/langserv/url-dashboard` | 1.440 us | 7.026 us | 7.601 us |
| `lokad/langserv/url-download` | 2.296 us | 12.305 us | 12.800 us |
| `lokad/style/hex-color` | 0.316 us | 0.495 us | 0.586 us |
| `lokad/style/cell-ref` | 0.752 us | 1.714 us | 1.911 us |
| `lokad/style/range-ref` | 0.474 us | 1.392 us | 1.506 us |
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
| `literal/identifier-token` | 2.251 us | 4.419 us | 14.674 us |
| `literal/call-token` | 3.465 us | 3.844 us | 13.995 us |
| `literal/identifier-token-casei` | 5.603 us | 5.656 us | 16.593 us |
| `literal-family/type-token-family` | 10.812 us | 10.908 us | 21.705 us |
| `literal-family/method-token-family` | 2.947 us | 5.145 us | 16.001 us |
| `structural/keyword-to-generic-type` | 4.229 us | 3.365 us | 14.108 us |
| `structural/keyword-family-to-capitalized-identifier` | 77.156 us | 86.338 us | 99.015 us |
| `structural/method-family-call` | 4.298 us | 4.861 us | 15.527 us |
| `structural/ordered-keyword-pair` | 4.817 us | 3.954 us | 14.558 us |
| `structural/ordered-identifier-window` | 4.867 us | 10.409 us | 21.632 us |
| `structural/modifier-family-to-type-window` | 25.436 us | 105.343 us | 117.934 us |
| `structural/ordered-keyword-window` | 5.092 us | 8.527 us | 19.057 us |
| `structural/method-family-generic-call` | 3.883 us | 4.335 us | 15.804 us |
| `fallback/lookahead` | 4.384 us | 3.990 us | 14.186 us |
| `balancing/begin-end-blocks` | 9.490 us | 494.880 us | 503.670 us |
| `structural/ordered-modifier-pair` | 4.158 us | 8.286 us | 18.912 us |
| `structural/modifier-family-to-type-pair` | 23.721 us | 56.557 us | 68.489 us |
| `lokad/imports/module-imports` | 3.580 us | 4.395 us | 14.300 us |
| `lokad/folding/region-marker` | 6.060 us | 40.025 us | 48.575 us |
| `lokad/lexer/identifier` | 11.166 us | 16.293 us | 20.062 us |
| `lokad/lexer/number` | 4.174 us | 7.151 us | 8.442 us |
| `lokad/lexer/string` | 9.929 us | 43.742 us | 47.818 us |
| `lokad/lexer/operator-run` | 1.403 us | 1.870 us | 2.509 us |
| `lokad/lexer/doc-line` | 1.624 us | 2.097 us | 2.970 us |
| `lokad/docs/inline-doc-prefix` | 1.764 us | 2.699 us | 3.616 us |
| `lokad/langserv/identifier-validator` | 3.947 us | 8.814 us | 10.754 us |
| `lokad/langserv/helper-identifier` | 2.990 us | 4.183 us | 5.445 us |
| `lokad/langserv/color-short-hex` | 0.688 us | 0.923 us | 1.126 us |
| `lokad/langserv/color-rgb-hex` | 0.848 us | 1.752 us | 2.021 us |
| `lokad/langserv/url-dashboard` | 1.372 us | 7.204 us | 7.709 us |
| `lokad/langserv/url-download` | 2.227 us | 12.302 us | 12.798 us |
| `lokad/style/hex-color` | 0.323 us | 0.500 us | 0.594 us |
| `lokad/style/cell-ref` | 1.126 us | 1.693 us | 1.891 us |
| `lokad/style/range-ref` | 0.476 us | 1.382 us | 1.500 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
