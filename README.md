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
| `literal/sherlock-en` | 101.790 us | 61.805 us | 343.485 us |
| `literal/sherlock-casei-en` | 144.440 us | 106.185 us | 374.845 us |
| `literal/sherlock-ru` | 1,924.695 us | 68.120 us | 3,045.675 us |
| `literal/sherlock-casei-ru` | 3,044.665 us | 240.500 us | 3,055.875 us |
| `literal/sherlock-zh` | 559.255 us | 16.550 us | 1,207.400 us |
| `literal-alternate/sherlock-en` | 261.905 us | 890.075 us | 1,148.265 us |
| `literal-alternate/sherlock-en-nomatch` | 84.705 us | 398.120 us | 638.875 us |
| `literal-alternate/sherlock-en-mixed` | 102.820 us | 384.890 us | 614.785 us |
| `literal-alternate/sherlock-casei-en` | 233.825 us | 6,562.475 us | 6,863.435 us |
| `literal-alternate/sherlock-ru` | 2,735.660 us | 3,240.625 us | 6,130.860 us |
| `literal-alternate/sherlock-casei-ru` | 17,723.915 us | 14,857.485 us | 17,926.670 us |
| `literal-alternate/sherlock-zh` | 890.885 us | 47.000 us | 1,245.240 us |
| `bounded-repeat/letters-en` | 465.855 us | 1,612.230 us | 1,657.595 us |
| `bounded-repeat/letters-ru` | 596.760 us | 3,619.975 us | 4,104.405 us |
| `bounded-repeat/context` | 31,329.760 us | 127,137.780 us | 115,084.055 us |
| `bounded-repeat/capitals` | 131.435 us | 27,171.805 us | 30,068.325 us |
| `dictionary/single` | 7,759.040 us | 20,565.555 us | 19,574.455 us |
| `aws-keys/full` | 1,521.155 us | 55,177.845 us | 66,017.090 us |
| `aws-keys/quick` | 3,481.495 us | 7,077.505 us | 17,643.130 us |
| `ruff-noqa/real` | 2,578.135 us | 44,964.400 us | 56,892.910 us |
| `ruff-noqa/tweaked` | 4,601.045 us | 5,585.960 us | 17,696.060 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.342 us | 0.206 us | 0.225 us |
| `common/email-miss` | `IsMatch` | 0.353 us | 0.293 us | 0.324 us |
| `common/date-match` | `IsMatch` | 0.348 us | 0.094 us | 0.125 us |
| `common/date-miss` | `IsMatch` | 0.098 us | 0.350 us | 0.375 us |
| `common/ip-match` | `IsMatch` | 0.148 us | 0.180 us | 0.198 us |
| `common/ip-miss` | `IsMatch` | 0.151 us | 0.185 us | 0.208 us |
| `common/uri-match` | `IsMatch` | 0.356 us | 0.183 us | 0.214 us |
| `common/uri-miss` | `IsMatch` | 0.337 us | 0.255 us | 0.275 us |
| `common/matches-set` | `Count` | 5.275 us | 109.480 us | 110.703 us |
| `common/matches-boundary` | `Count` | 4.923 us | 95.237 us | 96.955 us |
| `common/matches-word` | `Count` | 2.172 us | 2.606 us | 3.296 us |
| `common/matches-words` | `Count` | 4.868 us | 53.578 us | 59.253 us |
| `common/match-word` | `Match` | 0.134 us | 0.099 us | 0.928 us |
| `common/replace-words` | `Replace` | 49.664 us | 47.323 us | 48.343 us |
| `common/split-words` | `Split` | 9.853 us | 46.075 us | 47.151 us |
| `common/backtracking` | `IsMatch` | 0.223 us | 0.526 us | 0.543 us |
| `common/one-node-backtracking` | `IsMatch` | 0.219 us | 0.347 us | 0.390 us |
| `industry/mariomka-email-count` | `Count` | 7,529.760 us | 271.395 us | 5,403.975 us |
| `industry/mariomka-uri-count` | `Count` | 8,288.115 us | 1,166.715 us | 6,283.825 us |
| `industry/mariomka-ip-count` | `Count` | 3,795.490 us | 6,365.960 us | 13,267.535 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,141.175 us | 11,516.995 us | 11,618.020 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 36.239 us | 159.441 us | 294.276 us |
| `industry/rust-sherlock-ing-count` | `Count` | 7,157.540 us | 6,876.925 us | 7,071.305 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 17.190 us | 4,673.585 us | 4,826.220 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 95.967 us | 590.684 us | 760.000 us |
| `industry/leipzig-twain-count` | `Count` | 3,553.905 us | 1,499.735 us | 6,885.780 us |
| `industry/leipzig-name-family-count` | `Count` | 2,789.465 us | 7,007.495 us | 13,787.755 us |
| `industry/leipzig-river-window-count` | `Count` | 6,573.295 us | 21,611.395 us | 26,958.305 us |
| `industry/leipzig-symbol-count` | `Count` | 1,502.990 us | 12,594.450 us | 18,365.290 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.228 us | 0.110 us | 0.126 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.133 us | 0.157 us | 0.172 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.507 us | 0.123 us | 0.146 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.182 us | 0.078 us | 0.098 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.089 us | 0.076 us | 0.090 us |
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
| `literal/sherlock-en` | 102.240 us | 47.315 us | 363.795 us |
| `literal/sherlock-casei-en` | 144.155 us | 59.695 us | 347.195 us |
| `literal/sherlock-ru` | 1,953.020 us | 57.015 us | 3,117.190 us |
| `literal/sherlock-casei-ru` | 2,972.755 us | 126.985 us | 2,980.820 us |
| `literal/sherlock-zh` | 551.585 us | 16.910 us | 1,204.300 us |
| `literal-alternate/sherlock-en` | 265.480 us | 211.780 us | 495.820 us |
| `literal-alternate/sherlock-en-nomatch` | 87.400 us | 108.555 us | 358.205 us |
| `literal-alternate/sherlock-en-mixed` | 104.200 us | 106.115 us | 337.545 us |
| `literal-alternate/sherlock-casei-en` | 232.875 us | 110.145 us | 361.430 us |
| `literal-alternate/sherlock-ru` | 2,759.630 us | 1,293.145 us | 4,176.940 us |
| `literal-alternate/sherlock-casei-ru` | 4,133.860 us | 1,200.800 us | 4,079.340 us |
| `literal-alternate/sherlock-zh` | 896.925 us | 30.125 us | 1,220.595 us |
| `bounded-repeat/letters-en` | 466.050 us | 1,052.600 us | 1,099.950 us |
| `bounded-repeat/letters-ru` | 598.600 us | 1,232.555 us | 1,711.030 us |
| `bounded-repeat/context` | 31,049.060 us | 43,136.940 us | 52,552.125 us |
| `bounded-repeat/capitals` | 144.845 us | 8,410.540 us | 10,831.240 us |
| `dictionary/single` | 6,676.050 us | 49,696.690 us | 46,934.130 us |
| `aws-keys/full` | 1,438.840 us | 11,350.520 us | 22,380.445 us |
| `aws-keys/quick` | 3,544.835 us | 3,309.195 us | 14,517.180 us |
| `ruff-noqa/real` | 2,769.480 us | 12,986.155 us | 24,346.590 us |
| `ruff-noqa/tweaked` | 4,126.595 us | 4,484.860 us | 18,677.985 us |

### Public/Common and Industry Workloads

These rows mix `Count(...)`, `IsMatch(...)`, `Match(...)`, `Replace(...)`, and `Split(...)` depending on the case.

| Case | Operation | Utf8Regex Compiled CPU | .NET compiled predecoded CPU | .NET compiled + decode CPU |
|---|---|---:|---:|---:|
| `common/email-match` | `IsMatch` | 0.340 us | 0.049 us | 0.067 us |
| `common/email-miss` | `IsMatch` | 0.352 us | 0.055 us | 0.074 us |
| `common/date-match` | `IsMatch` | 0.360 us | 0.033 us | 0.051 us |
| `common/date-miss` | `IsMatch` | 0.099 us | 0.116 us | 0.139 us |
| `common/ip-match` | `IsMatch` | 0.150 us | 0.042 us | 0.058 us |
| `common/ip-miss` | `IsMatch` | 0.152 us | 0.042 us | 0.058 us |
| `common/uri-match` | `IsMatch` | 0.368 us | 0.061 us | 0.080 us |
| `common/uri-miss` | `IsMatch` | 0.346 us | 0.039 us | 0.055 us |
| `common/matches-set` | `Count` | 5.393 us | 56.228 us | 56.913 us |
| `common/matches-boundary` | `Count` | 5.049 us | 53.061 us | 55.069 us |
| `common/matches-word` | `Count` | 2.228 us | 1.288 us | 1.923 us |
| `common/matches-words` | `Count` | 5.208 us | 11.915 us | 13.668 us |
| `common/match-word` | `Match` | 0.137 us | 0.049 us | 0.807 us |
| `common/replace-words` | `Replace` | 16.013 us | 14.714 us | 15.702 us |
| `common/split-words` | `Split` | 9.798 us | 13.344 us | 14.482 us |
| `common/backtracking` | `IsMatch` | 0.225 us | 0.030 us | 0.050 us |
| `common/one-node-backtracking` | `IsMatch` | 0.223 us | 0.235 us | 0.266 us |
| `industry/mariomka-email-count` | `Count` | 7,262.980 us | 243.010 us | 5,300.885 us |
| `industry/mariomka-uri-count` | `Count` | 7,765.685 us | 701.870 us | 5,940.900 us |
| `industry/mariomka-ip-count` | `Count` | 3,903.405 us | 2,745.730 us | 8,612.965 us |
| `industry/rust-sherlock-letter-count` | `Count` | 1,131.180 us | 4,910.380 us | 5,034.000 us |
| `industry/rust-sherlock-holmes-window-count` | `Count` | 37.382 us | 37.146 us | 154.528 us |
| `industry/rust-sherlock-ing-count` | `Count` | 3,067.535 us | 2,924.195 us | 3,097.070 us |
| `industry/rust-sherlock-word-holmes-count` | `Count` | 17.390 us | 2,382.395 us | 2,555.580 us |
| `industry/rust-sherlock-nonnewline-count` | `Count` | 94.951 us | 545.803 us | 695.071 us |
| `industry/leipzig-twain-count` | `Count` | 3,538.530 us | 1,436.020 us | 6,843.480 us |
| `industry/leipzig-name-family-count` | `Count` | 2,522.475 us | 2,838.820 us | 9,297.760 us |
| `industry/leipzig-river-window-count` | `Count` | 6,504.235 us | 6,409.920 us | 12,350.230 us |
| `industry/leipzig-symbol-count` | `Count` | 1,560.840 us | 1,517.615 us | 6,318.600 us |
| `industry/boostdocs-ftp-line-match` | `IsMatch` | 0.228 us | 0.046 us | 0.067 us |
| `industry/boostdocs-credit-card-match` | `IsMatch` | 0.137 us | 0.051 us | 0.064 us |
| `industry/boostdocs-postcode-match` | `IsMatch` | 0.511 us | 0.038 us | 0.058 us |
| `industry/boostdocs-date-match` | `IsMatch` | 0.186 us | 0.036 us | 0.050 us |
| `industry/boostdocs-float-match` | `IsMatch` | 0.090 us | 0.035 us | 0.049 us |
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
| `literal/identifier-token` | 2.811 us | 3.079 us | 8.738 us |
| `literal/call-token` | 6.056 us | 2.802 us | 8.861 us |
| `literal/identifier-token-casei` | 5.790 us | 9.861 us | 16.534 us |
| `literal-family/type-token-family` | 8.441 us | 15.826 us | 21.855 us |
| `literal-family/method-token-family` | 5.438 us | 5.984 us | 12.712 us |
| `structural/keyword-to-generic-type` | 3.197 us | 3.194 us | 9.102 us |
| `structural/keyword-family-to-capitalized-identifier` | 70.725 us | 169.841 us | 199.446 us |
| `structural/method-family-call` | 2.996 us | 5.240 us | 11.377 us |
| `structural/ordered-keyword-pair` | 3.596 us | 3.502 us | 10.081 us |
| `structural/ordered-identifier-window` | 3.657 us | 18.943 us | 25.427 us |
| `structural/modifier-family-to-type-window` | 14.757 us | 178.019 us | 172.100 us |
| `structural/ordered-keyword-window` | 5.191 us | 16.907 us | 25.065 us |
| `structural/method-family-generic-call` | 2.954 us | 4.167 us | 11.074 us |
| `fallback/lookahead` | 5.170 us | 5.010 us | 11.603 us |
| `balancing/begin-end-blocks` | 2,629.000 us | 2,626.709 us | 2,654.350 us |
| `structural/ordered-modifier-pair` | 3.385 us | 11.462 us | 20.150 us |
| `structural/modifier-family-to-type-pair` | 11.859 us | 82.148 us | 90.405 us |
| `lokad/imports/module-imports` | 2.485 us | 2.720 us | 8.030 us |
| `lokad/folding/region-marker` | 4.015 us | 21.105 us | 26.980 us |
| `lokad/lexer/identifier` | 11.442 us | 15.300 us | 19.162 us |
| `lokad/lexer/number` | 4.838 us | 7.762 us | 8.392 us |
| `lokad/lexer/string` | 9.417 us | 44.839 us | 46.837 us |
| `lokad/lexer/operator-run` | 1.112 us | 1.672 us | 2.082 us |
| `lokad/lexer/doc-line` | 1.037 us | 1.406 us | 1.833 us |
| `lokad/docs/inline-doc-prefix` | 1.133 us | 1.611 us | 1.922 us |
| `lokad/langserv/identifier-validator` | 2.945 us | 5.489 us | 6.421 us |
| `lokad/langserv/helper-identifier` | 2.017 us | 3.149 us | 3.433 us |
| `lokad/langserv/color-short-hex` | 0.364 us | 0.548 us | 0.609 us |
| `lokad/langserv/color-rgb-hex` | 0.455 us | 1.123 us | 1.186 us |
| `lokad/langserv/url-dashboard` | 0.883 us | 4.288 us | 4.542 us |
| `lokad/langserv/url-download` | 1.266 us | 7.332 us | 7.608 us |
| `lokad/style/hex-color` | 0.200 us | 0.310 us | 0.307 us |
| `lokad/style/cell-ref` | 0.559 us | 1.056 us | 1.286 us |
| `lokad/style/range-ref` | 0.299 us | 0.796 us | 0.866 us |
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
| `literal/identifier-token` | 2.797 us | 2.317 us | 8.068 us |
| `literal/call-token` | 5.969 us | 2.305 us | 8.718 us |
| `literal/identifier-token-casei` | 5.981 us | 5.120 us | 11.515 us |
| `literal-family/type-token-family` | 8.465 us | 6.065 us | 12.171 us |
| `literal-family/method-token-family` | 6.012 us | 4.149 us | 11.134 us |
| `structural/keyword-to-generic-type` | 3.255 us | 2.504 us | 8.869 us |
| `structural/keyword-family-to-capitalized-identifier` | 68.513 us | 49.242 us | 59.548 us |
| `structural/method-family-call` | 2.793 us | 3.314 us | 9.487 us |
| `structural/ordered-keyword-pair` | 3.531 us | 2.640 us | 9.115 us |
| `structural/ordered-identifier-window` | 3.672 us | 6.440 us | 12.276 us |
| `structural/modifier-family-to-type-window` | 14.039 us | 56.817 us | 64.628 us |
| `structural/ordered-keyword-window` | 5.124 us | 7.071 us | 16.538 us |
| `structural/method-family-generic-call` | 2.668 us | 2.908 us | 9.745 us |
| `fallback/lookahead` | 4.429 us | 2.730 us | 10.146 us |
| `balancing/begin-end-blocks` | 279.736 us | 275.982 us | 279.100 us |
| `structural/ordered-modifier-pair` | 3.458 us | 5.064 us | 11.725 us |
| `structural/modifier-family-to-type-pair` | 11.909 us | 27.379 us | 34.486 us |
| `lokad/imports/module-imports` | 2.675 us | 2.785 us | 8.165 us |
| `lokad/folding/region-marker` | 4.085 us | 22.145 us | 29.510 us |
| `lokad/lexer/identifier` | 11.280 us | 15.256 us | 18.483 us |
| `lokad/lexer/number` | 4.418 us | 7.459 us | 8.299 us |
| `lokad/lexer/string` | 10.540 us | 43.193 us | 48.534 us |
| `lokad/lexer/operator-run` | 1.110 us | 1.590 us | 1.874 us |
| `lokad/lexer/doc-line` | 1.040 us | 1.439 us | 1.759 us |
| `lokad/docs/inline-doc-prefix` | 1.133 us | 1.616 us | 1.939 us |
| `lokad/langserv/identifier-validator` | 2.952 us | 5.483 us | 6.085 us |
| `lokad/langserv/helper-identifier` | 2.013 us | 3.195 us | 3.588 us |
| `lokad/langserv/color-short-hex` | 0.362 us | 0.531 us | 0.591 us |
| `lokad/langserv/color-rgb-hex` | 0.457 us | 1.091 us | 1.166 us |
| `lokad/langserv/url-dashboard` | 0.919 us | 4.215 us | 4.548 us |
| `lokad/langserv/url-download` | 1.234 us | 7.278 us | 7.543 us |
| `lokad/style/hex-color` | 0.201 us | 0.287 us | 0.299 us |
| `lokad/style/cell-ref` | 0.647 us | 1.081 us | 1.355 us |
| `lokad/style/range-ref` | 0.295 us | 0.769 us | 0.885 us |
<!-- END GENERATED LOKAD_COMPILED BENCHMARKS -->
