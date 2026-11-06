<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `C92B83574B2C94A3F031768B579260101823297CE0E113E261A18038275631D7`
- Latest managed row measurement: `2026-11-06T21:26:28.2431714+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T21:26:28.2431714+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `74` managed faster, `1` equivalent, `12` native faster, `14` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `41a751a3d8d9` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `41a751a3d8d9` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

NuGet package SHA-512: `Zu3NJGiU1S7tHHaW4UdEK1WZ9LFYqPI+6Y0eiL6YPHVOHSoWjbq0x5j3uN9895DoIgO5XI/50S6dj2ZmRHirNA==`.

## Comparator dependency

The benchmark executable—not either shipped library—uses one additional dependency for PCRE2-dialect rows. It provides the native PCRE2 engine and a UTF-8 span/reusable-buffer API, avoiding a string conversion or per-call wrapper allocation in the comparator.

| Package | Version | Native engine | License | Source revision | Benchmark profile |
|---|---:|---|---|---|---|
| [`PCRE.NET`](https://www.nuget.org/packages/PCRE.NET/1.5.0) | `1.5.0` | `10.47 2025-10-21` | `BSD-3-Clause WITH PCRE2-exception` | `e5e5deaa30d5` | UTF-8 standard NFA matcher, validation enabled, managed-profile LF newline and Unicode \R, reusable match buffer, JIT disabled |

Admission evidence: the package has a dedicated `net10.0` asset with no managed dependencies, is strongly named, is built and tested on Windows/Linux/macOS, and its tagged source has been maintained since 2014. It bundles RID-specific native libraries, so `PrivateAssets=all` and benchmark-project-only placement are mandatory. Native replacement is left blank because PCRE.NET does not expose equivalent UTF-8 span substitution output; routing through its string API would bias the comparison.

Native build fingerprint: `64226A8AB2D3AC9BE986279368957F1D3364E89B35FDDD9EFB10FA02EB91654B`; process/OS architecture `X64/X64`; JIT support `True` targeting `x86 64bit (little endian + unaligned)`; Unicode `16.0.0`; compiled-width mask `3`; link/effective-link size `2/2` bytes.

Build defaults: newline `AnyCrLf`, `\R` `Unicode`, heap `20,000,000` KiB, match/depth/parentheses limits `10,000,000/10,000,000/250`, character tables `1,088` bytes.

Comparator qualification lifecycle (`PcreMatchBuffer8Bit`): One buffer is constructed per qualified operation row, reused for checksum, warmup, calibration, paired samples, and allocation probing, then disposed. The buffer is not thread-safe and not reentrant; a caller must provide a distinct buffer for overlapping operations. The reusable buffer retains native match data between operations.

Retained native match-data heap-frame high water: unavailable — PCRE.NET 1.5.0 does not expose retained match-data heap-frame high-water memory through its public API.

Managed qualification lifecycle: The compiled regex is reused; each public invocation or global enumerator owns its transient state and returns rented storage on disposal or completion. The compiled regex may be invoked concurrently because capture, progress, timeout, and workspace state is invocation-local. The regex retains its immutable compiled plan, not invocation workspace; shared managed pools may retain returned arrays.

`vs decode` is `Utf8Pcre2 / .NET + decode`; `R` is `Utf8Pcre2 / PCRE.NET-PCRE2 NFA`; lower is better. Rows without a 95% interval and paired-sample description contain independently measured discovery data only and cannot determine a winner. `E` is the paired median managed-minus-comparator excess when paired evidence exists and the difference between discovery medians otherwise. Paired-sample descriptions show managed/comparator median sample durations, frozen operations per lane, and managed/comparator interquartile spread ratios; a spread above 1.10 makes Status inconclusive. Allocation columns report the median of five managed-thread allocation probes per public operation; they do not measure native retained memory. A dash means that the other engine cannot perform equivalent work or the snapshot does not contain that comparator. Times are medians in microseconds per public operation.

## Compatible IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.616 us | 2.327 us | 0.27x | 0.22–0.28x | -1.710 us | 9 pairs; 36/41 ms; 57,927/17,815 ops/lane; IQR 1.007/1.256 | `Pcre2Slice` | 586.083 us | 0.278 us | 0.829 us | 0.74x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.806 us | 13.161 us | 0.06x | 0.06–0.07x | -12.359 us | 9 pairs; 36/54 ms; 44,906/4,121 ops/lane; IQR 1.014/1.214 | `Pcre2Slice` | 0.597 us | 0.097 us | 3.487 us | 0.23x | 272 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.213 us | 0.01x | 0.01–0.01x | -1.202 us | 9 pairs; 40/40 ms; 3,785,473/32,843 ops/lane; IQR 1.006/1.012 | `Pcre2Slice` | 0.111 us | 1.044 us | 1.097 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.655 us | 0.160 us | 4.10x | 4.07–4.17x | +0.495 us | 9 pairs; 36/40 ms; 55,376/250,415 ops/lane; IQR 1.019/1.011 | `Pcre2Backtracking` | 0.045 us | 0.119 us | 0.159 us | 4.12x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.335 us | 0.563 us | 4.14x | 4.12–4.16x | +1.766 us | 9 pairs; 38/39 ms; 16,483/69,610 ops/lane; IQR 1.009/1.016 | `Pcre2Backtracking` | 0.115 us | 0.704 us | 0.742 us | 3.15x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.098 us | 0.292 us | 0.34x | 0.33–0.34x | -0.194 us | 9 pairs; 40/39 ms; 403,342/132,815 ops/lane; IQR 1.008/1.010 | `Pcre2Slice` | 0.121 us | 0.310 us | 0.336 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.358 us | 0.29x | 0.29–0.30x | -0.253 us | 9 pairs; 39/36 ms; 372,050/101,321 ops/lane; IQR 1.010/1.006 | `Pcre2Slice` | 0.124 us | 0.425 us | 0.453 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.287 us | 0.28x | 0.28–0.28x | -0.206 us | 9 pairs; 40/38 ms; 506,276/133,650 ops/lane; IQR 1.009/1.012 | `Pcre2Slice` | 0.094 us | 0.221 us | 0.240 us | 0.33x | 0 B | 0 B |
| `common/ip-miss` | **Inconclusive** | 15 B | 0.088 us | 0.311 us | 0.29x | 0.28–0.33x | -0.219 us | 9 pairs; 43/38 ms; 493,001/122,678 ops/lane; IQR 1.182/1.018 | `Pcre2Slice` | 0.095 us | 0.236 us | 0.258 us | 0.34x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.171 us | 0.075 us | 2.31x | 2.26–2.37x | +0.097 us | 9 pairs; 40/40 ms; 232,930/528,744 ops/lane; IQR 1.043/1.013 | `Pcre2Slice` | 0.095 us | 0.915 us | 0.939 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.198 us | 0.16x | 0.16–0.17x | -0.166 us | 9 pairs; 39/40 ms; 1,194,636/202,472 ops/lane; IQR 1.005/1.023 | `Pcre2Backtracking` | 0.114 us | 0.290 us | 0.328 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.608 us | 0.10x | 0.10–0.10x | -0.548 us | 9 pairs; 40/40 ms; 660,219/65,723 ops/lane; IQR 1.006/1.012 | `Pcre2Backtracking` | 0.138 us | 0.225 us | 0.249 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.153 us | 0.22x | 0.22–0.22x | -0.120 us | 9 pairs; 40/39 ms; 1,194,116/252,118 ops/lane; IQR 1.016/1.012 | `Pcre2Backtracking` | 0.108 us | 0.182 us | 0.203 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.112 us | 0.18x | 0.17–0.18x | -0.092 us | 9 pairs; 40/41 ms; 2,056,515/366,579 ops/lane; IQR 1.010/1.003 | `Pcre2Backtracking` | 0.062 us | 0.089 us | 0.108 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.119 us | 0.48x | 0.47–0.49x | -0.062 us | 9 pairs; 40/38 ms; 696,534/316,507 ops/lane; IQR 1.009/1.022 | `Pcre2Backtracking` | 0.013 us | 0.084 us | 0.101 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Inconclusive** | 67 B | 0.091 us | 0.267 us | 0.34x | 0.34–0.40x | -0.172 us | 9 pairs; 44/38 ms; 487,008/140,867 ops/lane; IQR 1.175/1.023 | `Pcre2Slice` | 0.101 us | 0.116 us | 0.155 us | 0.59x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Inconclusive** | 7 B | 0.022 us | 0.116 us | 0.19x | 0.19–0.19x | -0.094 us | 9 pairs; 39/39 ms; 1,752,007/339,263 ops/lane; IQR 1.022/1.011 | `Pcre2Backtracking` | 0.019 us | 0.087 us | 0.104 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 1.852 us | 0.06x | 0.06–0.07x | -1.734 us | 9 pairs; 40/47 ms; 341,686/25,495 ops/lane; IQR 1.008/1.118 | `Pcre2Literal` | 0.085 us | 0.050 us | 0.582 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.120 us | 1.965 us | 0.06x | 0.05–0.06x | -1.844 us | 9 pairs; 41/52 ms; 341,184/26,220 ops/lane; IQR 1.006/1.151 | `Pcre2Literal` | 0.148 us | 0.053 us | 0.595 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.153 us | 1.934 us | 0.08x | 0.07–0.08x | -1.783 us | 9 pairs; 42/48 ms; 275,929/25,015 ops/lane; IQR 1.008/1.056 | `Pcre2Literal` | 0.204 us | 0.229 us | 0.765 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.139 us | 1.853 us | 0.08x | 0.07–0.08x | -1.711 us | 9 pairs; 37/39 ms; 267,517/21,304 ops/lane; IQR 1.021/1.026 | `Pcre2Literal` | 0.179 us | 0.202 us | 0.737 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.098 us | 0.09x | 0.09–0.09x | -0.090 us | 9 pairs; 39/41 ms; 4,436,539/412,557 ops/lane; IQR 1.005/1.012 | `Pcre2Slice` | 0.232 us | 0.067 us | 0.087 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.088 us | 0.76x | 0.75–0.77x | -0.021 us | 9 pairs; 39/40 ms; 589,337/458,850 ops/lane; IQR 1.004/1.027 | `Pcre2Literal` | 0.131 us | 0.051 us | 0.072 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.107 us | 0.13x | 0.12–0.13x | -0.094 us | 9 pairs; 38/40 ms; 2,869,352/368,665 ops/lane; IQR 1.010/1.009 | `Pcre2Backtracking` | 0.269 us | 0.089 us | 0.112 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.119 us | 0.122 us | 0.98x | 0.97–1.07x | -0.003 us | 9 pairs; 40/42 ms; 334,247/341,394 ops/lane; IQR 1.099/1.004 | `Pcre2Character` | 0.137 us | 0.111 us | 0.141 us | 0.84x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.074 us | 0.205 us | 0.36x | 0.36–0.37x | -0.130 us | 9 pairs; 40/42 ms; 534,545/203,770 ops/lane; IQR 1.006/1.020 | `Pcre2Slice` | 0.406 us | 0.073 us | 0.112 us | 0.66x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 81.813 us | 160.357 us | 0.51x | 0.51–0.51x | -78.127 us | 9 pairs; 39/41 ms; 479/255 ops/lane; IQR 1.008/1.006 | `Pcre2Slice` | 333.545 us | 96.412 us | 98.529 us | 0.83x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.062 us | 275.747 us | 0.14x | 0.14–0.14x | -236.905 us | 9 pairs; 39/40 ms; 997/146 ops/lane; IQR 1.007/1.007 | `Pcre2Character` | 265.207 us | 214.081 us | 225.932 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.372 us | 209.754 us | 0.04x | 0.04–0.04x | -200.353 us | 9 pairs; 40/38 ms; 4,264/180 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 7.981 us | 217.070 us | 218.417 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 236.448 us | 204.063 us | 1.16x | 1.14–1.17x | +33.378 us | 9 pairs; 39/41 ms; 166/203 ops/lane; IQR 1.009/1.007 | `Pcre2SingleTokenRepeat` | 7.955 us | 225.591 us | 226.877 us | 1.04x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.545 us | 23.867 us | 0.15x | 0.15–0.15x | -20.360 us | 9 pairs; 40/42 ms; 11,323/1,740 ops/lane; IQR 1.007/1.011 | `Pcre2Literal` | 1.366 us | 4.246 us | 5.954 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.518 us | 80.633 us | 0.12x | 0.12–0.12x | -71.045 us | 9 pairs; 40/40 ms; 4,150/492 ops/lane; IQR 1.011/1.006 | `Pcre2Backtracking` | 9.171 us | 88.623 us | 91.748 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,186.500 us | 27,487.400 us | 0.15x | 0.15–0.16x | -23287.900 us | 9 pairs; 33/27 ms; 8/1 ops/lane; IQR 1.005/1.069 | `Pcre2Backtracking` | 5,392.314 us | 11,425.543 us | 18,080.240 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,262.812 us | 53,797.300 us | 0.06x | 0.06–0.06x | -50535.038 us | 9 pairs; 26/54 ms; 8/1 ops/lane; IQR 1.011/1.026 | `Pcre2Backtracking` | 3,793.295 us | 40,424.400 us | 46,273.350 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,953.524 us | 475,506.000 us | 0.00x | 0.00–0.00x | -473579.086 us | 9 pairs; 41/476 ms; 21/1 ops/lane; IQR 1.023/1.004 | `Pcre2Character` | 1,754.009 us | 22,520.250 us | 29,716.967 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Managed faster** | 16,013,977 B | 5,440.143 us | 10,966.050 us | 0.50x | 0.47–0.54x | -5525.907 us | 9 pairs; 38/44 ms; 7/4 ops/lane; IQR 1.011/1.061 | `Pcre2Literal` | 1,253.066 us | 1,316.993 us | 8,301.880 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,521.800 us | 143,917.300 us | 0.13x | 0.13–0.13x | -125260.500 us | 9 pairs; 37/144 ms; 2/1 ops/lane; IQR 1.023/1.010 | `Pcre2Backtracking` | 1,206.873 us | 502.480 us | 8,419.367 us | 2.20x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,865.087 us | 21,782.350 us | 0.22x | 0.22–0.23x | -16945.338 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | 2,386.034 us | 10,412.300 us | 18,468.540 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,387.450 us | 132,727.500 us | 0.17x | 0.17–0.18x | -110136.750 us | 9 pairs; 45/133 ms; 2/1 ops/lane; IQR 1.028/1.012 | `Pcre2Backtracking` | 1,471.986 us | 2,248.235 us | 10,395.712 us | 2.15x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 136.849 us | 996.905 us | 0.14x | 0.13–0.14x | -863.028 us | 9 pairs; 39/38 ms; 284/38 ops/lane; IQR 1.005/1.027 | `Pcre2Backtracking` | 168.228 us | 328.093 us | 569.803 us | 0.24x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,395.800 us | 13,216.833 us | 0.86x | 0.84–0.87x | -1808.533 us | 9 pairs; 34/40 ms; 3/3 ops/lane; IQR 1.002/1.021 | `Pcre2Backtracking` | 301.751 us | 14,759.367 us | 14,844.467 us | 0.77x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,776.629 us | 98,028.500 us | 0.03x | 0.03–0.03x | -95244.607 us | 9 pairs; 39/98 ms; 14/1 ops/lane; IQR 1.011/1.003 | `Pcre2Character` | 1,897.041 us | 19,591.200 us | 19,823.600 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.050 us | 2,768.613 us | 0.07x | 0.06–0.07x | -2584.563 us | 9 pairs; 40/42 ms; 216/15 ops/lane; IQR 1.008/1.039 | `Pcre2SingleTokenRepeat` | 188.979 us | 994.625 us | 1,243.167 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Inconclusive** | 594,930 B | 1,579.533 us | 20,047.850 us | 0.08x | 0.08–0.08x | -18468.829 us | 9 pairs; 38/40 ms; 24/2 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 574.825 us | 10,086.663 us | 10,357.388 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.142 us | 1.082 us | 0.13x | 0.13–0.13x | -0.942 us | 9 pairs; 39/39 ms; 272,147/36,480 ops/lane; IQR 1.020/1.004 | `Pcre2Slice` | 0.615 us | 0.255 us | 0.276 us | 0.52x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.540 us | 0.824 us | 0.66x | 0.66–0.66x | -0.281 us | 9 pairs; 39/40 ms; 72,371/48,416 ops/lane; IQR 1.010/1.009 | `Pcre2Literal` | 0.140 us | 0.147 us | 0.172 us | 3.15x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.286 us | 1.166 us | 0.25x | 0.24–0.25x | -0.878 us | 9 pairs; 38/40 ms; 133,638/34,212 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 1.040 us | 0.352 us | 0.378 us | 0.76x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.947 us | 1.117 us | 0.84x | 0.84–0.85x | -0.173 us | 9 pairs; 41/40 ms; 43,401/35,614 ops/lane; IQR 1.009/1.009 | `Pcre2Character` | 0.274 us | 0.419 us | 0.458 us | 2.07x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.161 us | 0.841 us | 0.19x | 0.19–0.19x | -0.682 us | 9 pairs; 39/40 ms; 244,965/47,931 ops/lane; IQR 1.022/1.008 | `Pcre2Slice` | 0.774 us | 0.152 us | 0.199 us | 0.81x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.875 us | 1.095 us | 0.80x | 0.79–0.80x | -0.220 us | 9 pairs; 40/40 ms; 45,160/36,479 ops/lane; IQR 1.003/1.008 | `Pcre2Slice` | 2.355 us | 0.304 us | 0.324 us | 2.70x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.789 us | 0.822 us | 0.96x | 0.95–0.96x | -0.035 us | 9 pairs; 40/40 ms; 50,361/48,774 ops/lane; IQR 1.007/1.008 | `Pcre2Literal` | 3.553 us | 0.179 us | 0.200 us | 3.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.016 us | 1.173 us | 0.87x | 0.86–0.87x | -0.154 us | 9 pairs; 41/42 ms; 40,377/35,546 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 5.347 us | 0.406 us | 0.428 us | 2.37x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.201 us | 1.118 us | 1.07x | 1.07–1.08x | +0.082 us | 9 pairs; 40/40 ms; 33,588/36,133 ops/lane; IQR 1.008/1.004 | `Pcre2Character` | 3.653 us | 0.471 us | 0.509 us | 2.36x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.779 us | 0.838 us | 0.93x | 0.92–0.94x | -0.059 us | 9 pairs; 39/40 ms; 50,088/47,884 ops/lane; IQR 1.009/1.015 | `Pcre2Slice` | 2.120 us | 0.181 us | 0.223 us | 3.50x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.597 us | 1.101 us | 0.54x | 0.54–0.55x | -0.504 us | 9 pairs; 41/41 ms; 68,040/37,105 ops/lane; IQR 1.001/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.495 us | 0.819 us | 0.60x | 0.60–0.61x | -0.324 us | 9 pairs; 39/39 ms; 78,015/48,094 ops/lane; IQR 1.007/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.657 us | 1.170 us | 0.56x | 0.56–0.56x | -0.513 us | 9 pairs; 38/39 ms; 57,071/33,347 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.905 us | 1.122 us | 0.81x | 0.80–0.81x | -0.216 us | 9 pairs; 40/42 ms; 43,806/37,471 ops/lane; IQR 1.011/1.006 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.472 us | 0.844 us | 0.56x | 0.56–0.56x | -0.372 us | 9 pairs; 39/39 ms; 83,595/45,918 ops/lane; IQR 1.012/1.012 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.426 us | — | — | — | — | — | `—` | 25.431 us | 94.981 us | 96.181 us | 0.64x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.584 us | — | — | — | — | — | `—` | 1.449 us | 0.356 us | 0.380 us | 9.42x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.065 us | — | — | — | — | — | `—` | 0.920 us | 0.238 us | 0.267 us | 3.99x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.204 us | — | — | — | — | — | `—` | 3.026 us | 0.458 us | 0.487 us | 2.47x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.482 us | — | — | — | — | — | `—` | 1.038 us | 0.535 us | 0.573 us | 2.58x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.524 us | — | — | — | — | — | `—` | 1.464 us | 0.263 us | 0.312 us | 11.30x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.094 us | 0.083 us | 1.15x | 1.08–1.16x | +0.012 us | 9 pairs; 42/39 ms; 445,770/473,678 ops/lane; IQR 1.046/1.026 | `Pcre2Character` | 0 B | 0 B | 2.244 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.116 us | 0.11x | 0.11–0.11x | -0.104 us | 9 pairs; 40/41 ms; 3,129,579/352,092 ops/lane; IQR 1.017/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 129.444 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.373 us | 0.103 us | 3.61x | 3.58–3.68x | +0.270 us | 9 pairs; 35/39 ms; 93,902/383,034 ops/lane; IQR 1.018/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 21.663 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.401 us | 0.116 us | 3.45x | 3.42–3.51x | +0.284 us | 9 pairs; 35/41 ms; 88,268/355,309 ops/lane; IQR 1.003/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 71.475 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.388 us | 0.102 us | 3.78x | 3.69–3.80x | +0.284 us | 9 pairs; 36/40 ms; 93,721/392,751 ops/lane; IQR 1.007/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 70.169 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.659 us | 0.344 us | 1.91x | 1.90–1.92x | +0.314 us | 9 pairs; 37/40 ms; 56,658/116,862 ops/lane; IQR 1.007/1.013 | `Pcre2Character` | 0 B | 0 B | 1.387 us | 2,248 B |
| `pcre2/recursive-optional` | **Inconclusive** | 6 B | 0.017 us | 0.628 us | 0.03x | 0.02–0.03x | -0.611 us | 9 pairs; 60/40 ms; 3,603,684/63,885 ops/lane; IQR 1.202/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 23.419 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.502 us | 0.02x | 0.02–0.02x | -0.490 us | 9 pairs; 42/40 ms; 3,590,686/79,969 ops/lane; IQR 1.015/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 21.875 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.463 us | 0.107 us | 4.33x | 4.25–4.40x | +0.356 us | 9 pairs; 37/40 ms; 79,646/374,566 ops/lane; IQR 1.013/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 18.538 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.272 us | 10.383 us | 0.80x | 0.79–0.80x | -2.110 us | 9 pairs; 39/40 ms; 4,732/3,899 ops/lane; IQR 1.005/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.312 us | 1.041 us | 0.30x | 0.30–0.30x | -0.729 us | 9 pairs; 41/41 ms; 132,446/39,021 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 128.650 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.303 us | 0.463 us | 0.66x | 0.65–0.66x | -0.160 us | 9 pairs; 37/41 ms; 122,555/88,872 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.725 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.286 us | 0.452 us | 0.63x | 0.63–0.64x | -0.167 us | 9 pairs; 37/39 ms; 128,212/87,019 ops/lane; IQR 1.004/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 185.394 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.303 us | 0.591 us | 0.51x | 0.51–0.52x | -0.287 us | 9 pairs; 34/39 ms; 112,342/66,773 ops/lane; IQR 1.014/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 106.175 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.284 us | 0.370 us | 0.77x | 0.76–0.78x | -0.085 us | 9 pairs; 39/41 ms; 139,043/111,587 ops/lane; IQR 1.007/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 213.200 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.901 us | 8.708 us | 0.56x | 0.56–0.57x | -3.807 us | 9 pairs; 39/40 ms; 8,039/4,611 ops/lane; IQR 1.006/1.007 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.289 us | 0.304 us | 0.95x | 0.85–0.96x | -0.015 us | 9 pairs; 42/41 ms; 145,087/134,661 ops/lane; IQR 1.061/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 91.219 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.277 us | 0.400 us | 0.70x | 0.68–0.82x | -0.119 us | 9 pairs; 38/40 ms; 136,690/99,630 ops/lane; IQR 1.103/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 122.844 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.744 us | — | — | — | — | — | `—` | 0 B | — | 13.650 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.194 us | 0.294 us | 0.66x | 0.66–0.67x | -0.099 us | 9 pairs; 34/40 ms; 175,865/137,694 ops/lane; IQR 1.011/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 54.094 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.688 us | — | — | — | — | — | `—` | 0 B | — | 13.713 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.570 us | 0.665 us | 0.86x | 0.86–0.86x | -0.095 us | 9 pairs; 37/39 ms; 64,367/58,616 ops/lane; IQR 1.010/1.009 | `Pcre2Slice` | 0 B | 0 B | 63.781 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.719 us | 28.169 us | 0.34x | 0.34–0.35x | -18.507 us | 9 pairs; 40/40 ms; 4,140/1,422 ops/lane; IQR 1.009/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.962 us | 1.051 us | 0.92x | 0.91–0.92x | -0.089 us | 9 pairs; 40/39 ms; 41,087/37,365 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 129.981 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.002 us | 1.179 us | 0.85x | 0.85–0.85x | -0.177 us | 9 pairs; 40/40 ms; 39,627/33,708 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 115.013 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.936 us | 1.024 us | 0.92x | 0.91–0.92x | -0.086 us | 9 pairs; 40/40 ms; 43,107/39,353 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 142.131 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.984 us | 1.160 us | 0.84x | 0.84–0.85x | -0.180 us | 9 pairs; 40/42 ms; 40,369/35,809 ops/lane; IQR 1.014/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 104.644 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.936 us | 0.933 us | 1.00x | 1.00–1.01x | +0.003 us | 9 pairs; 40/39 ms; 42,953/41,633 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 212.312 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.603 us | 8.755 us | 0.64x | 0.63–0.64x | -3.166 us | 9 pairs; 41/41 ms; 7,265/4,673 ops/lane; IQR 1.009/1.004 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.905 us | 0.881 us | 1.02x | 1.02–1.03x | +0.021 us | 9 pairs; 37/40 ms; 40,436/45,981 ops/lane; IQR 1.008/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 91.675 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.911 us | 0.957 us | 0.95x | 0.94–0.96x | -0.050 us | 9 pairs; 39/39 ms; 43,111/41,180 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 123.919 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.942 us | — | — | — | — | — | `—` | 0 B | — | 13.944 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.824 us | 0.853 us | 0.97x | 0.96–0.97x | -0.028 us | 9 pairs; 40/40 ms; 48,285/46,781 ops/lane; IQR 1.008/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.625 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.873 us | — | — | — | — | — | `—` | 0 B | — | 14.562 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.846 us | 1.237 us | 0.68x | 0.68–0.69x | -0.393 us | 9 pairs; 39/39 ms; 46,501/31,526 ops/lane; IQR 1.010/1.008 | `Pcre2Slice` | 0 B | 0 B | 63.900 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.449 us | 2.236 us | 0.65x | 0.64–0.65x | -0.789 us | 9 pairs; 39/40 ms; 27,200/17,722 ops/lane; IQR 1.013/1.009 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.654 us | 1.054 us | 0.62x | 0.62–0.63x | -0.396 us | 9 pairs; 38/39 ms; 58,303/36,964 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 129.694 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.707 us | 1.180 us | 0.60x | 0.60–0.60x | -0.473 us | 9 pairs; 42/39 ms; 59,046/33,102 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 115.756 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.645 us | 1.021 us | 0.63x | 0.63–0.64x | -0.375 us | 9 pairs; 39/41 ms; 60,684/40,182 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 143.375 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.617 us | 1.153 us | 0.53x | 0.53–0.54x | -0.537 us | 9 pairs; 39/39 ms; 63,801/34,212 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 102.225 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.615 us | 0.939 us | 0.66x | 0.66–0.66x | -0.322 us | 9 pairs; 42/40 ms; 67,683/42,361 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 213.119 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.895 us | 2.150 us | 0.88x | 0.88–0.89x | -0.254 us | 9 pairs; 39/41 ms; 20,801/18,839 ops/lane; IQR 1.006/1.004 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.618 us | 0.875 us | 0.70x | 0.70–0.71x | -0.264 us | 9 pairs; 39/40 ms; 63,460/45,213 ops/lane; IQR 1.013/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 92.719 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.617 us | 0.955 us | 0.64x | 0.64–0.65x | -0.341 us | 9 pairs; 41/40 ms; 67,172/41,666 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 121.931 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.710 us | — | — | — | — | — | `—` | 0 B | — | 13.713 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.484 us | 0.846 us | 0.57x | 0.57–0.57x | -0.364 us | 9 pairs; 38/41 ms; 79,131/48,793 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 53.681 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.606 us | — | — | — | — | — | `—` | 0 B | — | 13.706 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.591 us | 1.239 us | 0.48x | 0.47–0.48x | -0.649 us | 9 pairs; 40/39 ms; 66,972/31,664 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0 B | 0 B | 64.100 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 16.974 us | — | — | — | — | — | `—` | 480 B | — | 1.163 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.133 us | — | — | — | — | — | `—` | 48 B | — | 129.312 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.235 us | — | — | — | — | — | `—` | 48 B | — | 115.294 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.115 us | — | — | — | — | — | `—` | 48 B | — | 145.812 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.147 us | — | — | — | — | — | `—` | 48 B | — | 101.956 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.116 us | — | — | — | — | — | `—` | 48 B | — | 217.100 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.883 us | — | — | — | — | — | `—` | 64 B | — | 1.975 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.157 us | — | — | — | — | — | `—` | 40 B | — | 94.938 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.145 us | — | — | — | — | — | `—` | 40 B | — | 124.069 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.980 us | — | — | — | — | — | `—` | 1,216 B | — | 13.425 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.080 us | — | — | — | — | — | `—` | 48 B | — | 54.538 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.417 us | — | — | — | — | — | `—` | 928 B | — | 13.400 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.599 us | — | — | — | — | — | `—` | 4,496 B | — | 63.475 us | 55,552 B |

## Qualified comparator plans

Plan data is captured through the comparator's public compiled-pattern information surface; JIT remains disabled for primary Status.

| Section | Case | Plan SHA-256 | Pattern | Frame | JIT | Min subject | First type/unit | Last type/unit |
|---|---|---|---:|---:|---:|---:|---|---|
| `pcre2-managed-compatible-ismatch` | `backtracking/assertion-capture` | `7A6B7C3E59FC` | 269 B | 152 B | 0 B | 6 chars | 1/97 | 1/51 |
| `pcre2-managed-compatible-ismatch` | `backtracking/capture-backreference` | `E895C9C5D31C` | 206 B | 152 B | 0 B | 3 chars | 0/ | 1/45 |
| `pcre2-managed-compatible-ismatch` | `common/backtracking` | `0ED58EBBE52D` | 173 B | 152 B | 0 B | 2 chars | 2/ | 1/115 |
| `pcre2-managed-compatible-ismatch` | `common/date-match` | `F4DA4E5B1AB9` | 183 B | 136 B | 0 B | 6 chars | 0/ | 1/47 |
| `pcre2-managed-compatible-ismatch` | `common/date-miss` | `F4DA4E5B1AB9` | 183 B | 136 B | 0 B | 6 chars | 0/ | 1/47 |
| `pcre2-managed-compatible-ismatch` | `common/email-match` | `07140C873EAF` | 495 B | 248 B | 0 B | 5 chars | 0/ | 1/46 |
| `pcre2-managed-compatible-ismatch` | `common/email-miss` | `07140C873EAF` | 495 B | 248 B | 0 B | 5 chars | 0/ | 1/46 |
| `pcre2-managed-compatible-ismatch` | `common/ip-match` | `94AD8473E42E` | 1,051 B | 136 B | 0 B | 11 chars | 0/ | 1/46 |
| `pcre2-managed-compatible-ismatch` | `common/ip-miss` | `94AD8473E42E` | 1,051 B | 136 B | 0 B | 11 chars | 0/ | 1/46 |
| `pcre2-managed-compatible-ismatch` | `common/one-node-backtracking` | `C6F9A2D1FE9C` | 165 B | 136 B | 0 B | 3 chars | 0/ | 1/46 |
| `pcre2-managed-compatible-ismatch` | `common/uri-match` | `248FC1AD2C4F` | 353 B | 136 B | 0 B | 6 chars | 0/ | 1/47 |
| `pcre2-managed-compatible-ismatch` | `common/uri-miss` | `248FC1AD2C4F` | 353 B | 136 B | 0 B | 6 chars | 0/ | 1/47 |
| `pcre2-managed-compatible-ismatch` | `industry/boostdocs-credit-card-match` | `772299501137` | 300 B | 152 B | 0 B | 18 chars | 0/ | 0/ |
| `pcre2-managed-compatible-ismatch` | `industry/boostdocs-date-match` | `3C934787A99D` | 179 B | 136 B | 0 B | 8 chars | 0/ | 1/47 |
| `pcre2-managed-compatible-ismatch` | `industry/boostdocs-float-match` | `D825BE69E98B` | 201 B | 136 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-managed-compatible-ismatch` | `industry/boostdocs-ftp-line-match` | `4470161C2446` | 232 B | 184 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-managed-compatible-ismatch` | `industry/boostdocs-postcode-match` | `4871C808F2E5` | 339 B | 136 B | 0 B | 5 chars | 0/ | 0/ |
| `pcre2-managed-compatible-ismatch` | `literal/absolute-anchored` | `AA3EF83FFFB7` | 172 B | 136 B | 0 B | 6 chars | 1/110 | 0/ |
| `pcre2-managed-compatible-ismatch` | `literal/early` | `58237AC4098D` | 171 B | 136 B | 0 B | 6 chars | 1/110 | 1/101 |
| `pcre2-managed-compatible-ismatch` | `literal/late` | `58237AC4098D` | 171 B | 136 B | 0 B | 6 chars | 1/110 | 1/101 |
| `pcre2-managed-compatible-ismatch` | `literal/missing` | `58237AC4098D` | 171 B | 136 B | 0 B | 6 chars | 1/110 | 1/101 |
| `pcre2-managed-compatible-ismatch` | `simple/ab-plus` | `B1DA634BB0A0` | 171 B | 152 B | 0 B | 2 chars | 1/97 | 1/98 |
| `pcre2-managed-compatible-ismatch` | `simple/foo-dense` | `27887C10BCDC` | 165 B | 136 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-ismatch` | `simple/foo-optional-bar` | `C6F78F9E7B0E` | 186 B | 152 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-ismatch` | `simple/httpclient-caseless` | `D304925EC56F` | 179 B | 136 B | 0 B | 10 chars | 1/104 | 1/116 |
| `pcre2-managed-compatible-ismatch` | `simple/loglevel-multiline` | `F31F751A2DCB` | 177 B | 136 B | 0 B | 8 chars | 2/ | 1/32 |
| `pcre2-managed-compatible-count` | `simple/ab-plus` | `B1DA634BB0A0` | 171 B | 152 B | 0 B | 2 chars | 1/97 | 1/98 |
| `pcre2-managed-compatible-count` | `simple/foo-dense` | `27887C10BCDC` | 165 B | 136 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-count` | `simple/foo-optional-bar` | `C6F78F9E7B0E` | 186 B | 152 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-count` | `simple/httpclient-caseless` | `D304925EC56F` | 179 B | 136 B | 0 B | 10 chars | 1/104 | 1/116 |
| `pcre2-managed-compatible-count` | `simple/loglevel-multiline` | `F31F751A2DCB` | 177 B | 136 B | 0 B | 8 chars | 2/ | 1/32 |
| `pcre2-managed-compatible-count` | `common/matches-set` | `F981F26785A0` | 165 B | 136 B | 0 B | 10 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `common/matches-boundary` | `0ED016E2D21F` | 167 B | 136 B | 0 B | 10 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `common/matches-word` | `43328856751C` | 171 B | 136 B | 0 B | 6 chars | 1/116 | 1/115 |
| `pcre2-managed-compatible-count` | `common/matches-words` | `C2B86172D364` | 199 B | 136 B | 0 B | 5 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `character/unicode-class-dense` | `94621C05B00C` | 202 B | 136 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `backtracking/alternation-repeat` | `6854D44D1C19` | 176 B | 136 B | 0 B | 2 chars | 1/97 | 1/122 |
| `pcre2-managed-compatible-count` | `industry/leipzig-name-family-count` | `672B54609A3F` | 216 B | 136 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `industry/leipzig-river-window-count` | `1D3622A048A2` | 210 B | 136 B | 0 B | 18 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `industry/leipzig-symbol-count` | `6C53959C1942` | 162 B | 136 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `industry/leipzig-twain-count` | `8A1ADF584CC9` | 169 B | 136 B | 0 B | 5 chars | 1/84 | 1/110 |
| `pcre2-managed-compatible-count` | `industry/mariomka-email-count` | `1D742A03872D` | 265 B | 136 B | 0 B | 5 chars | 0/ | 1/46 |
| `pcre2-managed-compatible-count` | `industry/mariomka-ip-count` | `94AD8473E42E` | 1,051 B | 136 B | 0 B | 11 chars | 0/ | 1/46 |
| `pcre2-managed-compatible-count` | `industry/mariomka-uri-count` | `248FC1AD2C4F` | 353 B | 136 B | 0 B | 6 chars | 0/ | 1/47 |
| `pcre2-managed-compatible-count` | `industry/rust-sherlock-holmes-window-count` | `B40600CA7551` | 218 B | 136 B | 0 B | 12 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `industry/rust-sherlock-ing-count` | `084E52597901` | 205 B | 136 B | 0 B | 5 chars | 0/ | 1/103 |
| `pcre2-managed-compatible-count` | `industry/rust-sherlock-letter-count` | `6C53959C1942` | 162 B | 136 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `industry/rust-sherlock-nonnewline-count` | `448D3EF7E1BF` | 161 B | 136 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-managed-compatible-count` | `industry/rust-sherlock-word-holmes-count` | `A6CC67CBE2DD` | 175 B | 136 B | 0 B | 8 chars | 0/ | 1/115 |
| `pcre2-managed-compatible-enumerate` | `simple/ab-plus` | `B1DA634BB0A0` | 171 B | 152 B | 0 B | 2 chars | 1/97 | 1/98 |
| `pcre2-managed-compatible-enumerate` | `simple/foo-dense` | `27887C10BCDC` | 165 B | 136 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-enumerate` | `simple/foo-optional-bar` | `C6F78F9E7B0E` | 186 B | 152 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-enumerate` | `simple/httpclient-caseless` | `D304925EC56F` | 179 B | 136 B | 0 B | 10 chars | 1/104 | 1/116 |
| `pcre2-managed-compatible-enumerate` | `simple/loglevel-multiline` | `F31F751A2DCB` | 177 B | 136 B | 0 B | 8 chars | 2/ | 1/32 |
| `pcre2-managed-compatible-matchmany` | `simple/ab-plus` | `B1DA634BB0A0` | 171 B | 152 B | 0 B | 2 chars | 1/97 | 1/98 |
| `pcre2-managed-compatible-matchmany` | `simple/foo-dense` | `27887C10BCDC` | 165 B | 136 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-matchmany` | `simple/foo-optional-bar` | `C6F78F9E7B0E` | 186 B | 152 B | 0 B | 3 chars | 1/102 | 1/111 |
| `pcre2-managed-compatible-matchmany` | `simple/httpclient-caseless` | `D304925EC56F` | 179 B | 136 B | 0 B | 10 chars | 1/104 | 1/116 |
| `pcre2-managed-compatible-matchmany` | `simple/loglevel-multiline` | `F31F751A2DCB` | 177 B | 136 B | 0 B | 8 chars | 2/ | 1/32 |
| `pcre2-special-ismatch` | `pcre2/backslash-c-literal` | `810830C9F57B` | 168 B | 136 B | 0 B | 2 chars | 1/97 | 1/101 |
| `pcre2-special-ismatch` | `pcre2/branch-reset-backref` | `4C897AC7AADD` | 199 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-ismatch` | `pcre2/conditional-accept-negative-lookahead` | `A35D94C331B2` | 198 B | 152 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-ismatch` | `pcre2/conditional-lookahead` | `AAD9C10AEE8C` | 197 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-ismatch` | `pcre2/conditional-negative-lookahead` | `AAD9C10AEE8C` | 197 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-ismatch` | `pcre2/recursive-optional` | `1A8BE40346FB` | 195 B | 168 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-special-ismatch` | `pcre2/recursive-palindrome` | `183D6337793E` | 189 B | 168 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-ismatch` | `pcre2/subroutine-prefix-digits` | `801A4E18DD7D` | 177 B | 152 B | 0 B | 5 chars | 0/ | 0/ |
| `pcre2-special-ismatch` | `pcre2/grapheme-cluster` | `61097D21621C` | 160 B | 136 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-special-count` | `pcre2/branch-reset-backref` | `4C897AC7AADD` | 199 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-count` | `pcre2/branch-reset-basic` | `74D6243E60DF` | 196 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-count` | `pcre2/branch-reset-followup` | `608E1350DA2C` | 221 B | 168 B | 0 B | 5 chars | 0/ | 1/99 |
| `pcre2-special-count` | `pcre2/branch-reset-nested` | `C80CCF8DFACB` | 216 B | 184 B | 0 B | 5 chars | 1/120 | 1/120 |
| `pcre2-special-count` | `pcre2/duplicate-names` | `6F7669A41126` | 209 B | 168 B | 0 B | 6 chars | 0/ | 0/ |
| `pcre2-special-count` | `pcre2/kreset-atomic-alt` | `372992C42837` | 179 B | 136 B | 0 B | 2 chars | 1/97 | 0/ |
| `pcre2-special-count` | `pcre2/kreset-bar-or-baz` | `FBC6C2828B07` | 197 B | 168 B | 0 B | 6 chars | 1/102 | 1/98 |
| `pcre2-special-count` | `pcre2/kreset-global` | `BD05D11289E3` | 172 B | 136 B | 0 B | 6 chars | 1/97 | 1/51 |
| `pcre2-special-count` | `pcre2/same-start-global` | `DD8CF42003A9` | 191 B | 152 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-count` | `literal/empty-unicode` | `3ACBBFCAE6C0` | 159 B | 136 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-count` | `pcre2/grapheme-cluster` | `61097D21621C` | 160 B | 136 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-special-enumerate` | `pcre2/branch-reset-backref` | `4C897AC7AADD` | 199 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-enumerate` | `pcre2/branch-reset-basic` | `74D6243E60DF` | 196 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-enumerate` | `pcre2/branch-reset-followup` | `608E1350DA2C` | 221 B | 168 B | 0 B | 5 chars | 0/ | 1/99 |
| `pcre2-special-enumerate` | `pcre2/branch-reset-nested` | `C80CCF8DFACB` | 216 B | 184 B | 0 B | 5 chars | 1/120 | 1/120 |
| `pcre2-special-enumerate` | `pcre2/duplicate-names` | `6F7669A41126` | 209 B | 168 B | 0 B | 6 chars | 0/ | 0/ |
| `pcre2-special-enumerate` | `pcre2/kreset-atomic-alt` | `372992C42837` | 179 B | 136 B | 0 B | 2 chars | 1/97 | 0/ |
| `pcre2-special-enumerate` | `pcre2/kreset-bar-or-baz` | `FBC6C2828B07` | 197 B | 168 B | 0 B | 6 chars | 1/102 | 1/98 |
| `pcre2-special-enumerate` | `pcre2/kreset-global` | `BD05D11289E3` | 172 B | 136 B | 0 B | 6 chars | 1/97 | 1/51 |
| `pcre2-special-enumerate` | `pcre2/same-start-global` | `DD8CF42003A9` | 191 B | 152 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-enumerate` | `literal/empty-unicode` | `3ACBBFCAE6C0` | 159 B | 136 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-enumerate` | `pcre2/grapheme-cluster` | `61097D21621C` | 160 B | 136 B | 0 B | 1 chars | 0/ | 0/ |
| `pcre2-special-matchmany` | `pcre2/branch-reset-backref` | `4C897AC7AADD` | 199 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-matchmany` | `pcre2/branch-reset-basic` | `74D6243E60DF` | 196 B | 152 B | 0 B | 3 chars | 0/ | 0/ |
| `pcre2-special-matchmany` | `pcre2/branch-reset-followup` | `608E1350DA2C` | 221 B | 168 B | 0 B | 5 chars | 0/ | 1/99 |
| `pcre2-special-matchmany` | `pcre2/branch-reset-nested` | `C80CCF8DFACB` | 216 B | 184 B | 0 B | 5 chars | 1/120 | 1/120 |
| `pcre2-special-matchmany` | `pcre2/duplicate-names` | `6F7669A41126` | 209 B | 168 B | 0 B | 6 chars | 0/ | 0/ |
| `pcre2-special-matchmany` | `pcre2/kreset-atomic-alt` | `372992C42837` | 179 B | 136 B | 0 B | 2 chars | 1/97 | 0/ |
| `pcre2-special-matchmany` | `pcre2/kreset-bar-or-baz` | `FBC6C2828B07` | 197 B | 168 B | 0 B | 6 chars | 1/102 | 1/98 |
| `pcre2-special-matchmany` | `pcre2/kreset-global` | `BD05D11289E3` | 172 B | 136 B | 0 B | 6 chars | 1/97 | 1/51 |
| `pcre2-special-matchmany` | `pcre2/same-start-global` | `DD8CF42003A9` | 191 B | 152 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-matchmany` | `literal/empty-unicode` | `3ACBBFCAE6C0` | 159 B | 136 B | 0 B | 0 chars | 0/ | 0/ |
| `pcre2-special-matchmany` | `pcre2/grapheme-cluster` | `61097D21621C` | 160 B | 136 B | 0 B | 1 chars | 0/ | 0/ |

## Comparator exclusions

Rows are excluded instead of timed when result checksums differ or PCRE.NET cannot expose equivalent UTF-8 work.

| Section | Case | Reason |
|---|---|---|
| `pcre2-managed-compatible-replace` | `common/replace-words` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/ab-plus` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/foo-dense` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/foo-optional-bar` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/httpclient-caseless` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/loglevel-multiline` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-count` | `pcre2/kreset-captured-repeat` | Structured checksum mismatch: managed=10D75503432F5794, comparator=9EB15884DB649A19. |
| `pcre2-special-count` | `pcre2/kreset-repeat` | Structured checksum mismatch: managed=10D75503432F5794, comparator=9EB15884DB649A19. |
| `pcre2-special-enumerate` | `pcre2/kreset-captured-repeat` | Structured checksum mismatch: managed=D112C845A4F1BD78, comparator=027ACCB0FF65E9DB. |
| `pcre2-special-enumerate` | `pcre2/kreset-repeat` | Structured checksum mismatch: managed=D112C845A4F1BD78, comparator=027ACCB0FF65E9DB. |
| `pcre2-special-matchmany` | `pcre2/kreset-captured-repeat` | Structured checksum mismatch: managed=A8517ACD40FBB4CC, comparator=454EE9D126605347. |
| `pcre2-special-matchmany` | `pcre2/kreset-repeat` | Structured checksum mismatch: managed=A8517ACD40FBB4CC, comparator=454EE9D126605347. |
| `pcre2-special-replace` | `pcre2/branch-reset-backref` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/branch-reset-basic` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/branch-reset-followup` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/branch-reset-nested` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/duplicate-names` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/kreset-atomic-alt` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/kreset-bar-or-baz` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/kreset-captured-repeat` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/kreset-global` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/kreset-repeat` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/same-start-global` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `literal/empty-unicode` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-replace` | `pcre2/grapheme-cluster` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |

## Scaling evidence

Scaling rows are mechanism and complexity guards, not direct .NET parity claims.

| Family | Operation | Points | Input range | Pattern range | Warm elapsed range |
|---|---|---:|---:|---:|---:|
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 32.483–251.021 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.276–94.691 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.836–14.300 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.129–49.831 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.031–1.123 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.484–42.179 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.617–21.893 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.198–98.914 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.009–71.009 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.184–5.385 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.601–359.936 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.075 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 40.109–316.381 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.817–4.972 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.112–15.396 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 103.372–836.797 us |

## Reproduce and refresh

Run from the repository root in `Release` through `./bench.ps1`:

```powershell
./bench.ps1 -CommandArgs "--verify-pcre2-comparator-case","simple/foo-dense"
./bench.ps1 -CommandArgs "--verify-pcre2-qualification-consistency"
./bench.ps1 -CommandArgs "--qualify-pcre2-comparator-case","simple/foo-dense","9"
./bench.ps1 -CommandArgs "--qualify-pcre2-comparator-case-reversed","simple/foo-dense","9"
./bench.ps1 -CommandArgs "--measure-pcre2-native-buffer-cost","simple/foo-dense","200","5"
./bench.ps1 -CommandArgs "--measure-pcre2-native-auto-possess-cost","common/email-miss","20","5"
./bench.ps1 -CommandArgs "--measure-pcre2-match-count-scaling","literal","200","5"
./bench.ps1 -CommandArgs "--emit-pcre2-priority-report","relative"
./bench.ps1 -CommandArgs "--emit-pcre2-priority-report","absolute"
./bench.ps1 -CommandArgs "--measure-pcre2-compatible-case","common/email-match","200","7"
./bench.ps1 -CommandArgs "--measure-pcre2-special-case","pcre2/branch-reset-basic","200","7"
./bench.ps1 -CommandArgs "--refresh-pcre2-benchmark-case","common/email-match","200","7"
./bench.ps1 -CommandArgs "--refresh-pcre2-native-baseline-case","pcre2/branch-reset-basic","200","7"
./bench.ps1 -CommandArgs "--refresh-pcre2-native-baselines","pcre2-special-ismatch","200","7"
./bench.ps1 -CommandArgs "--verify-pcre2-benchmark-markdown"
```

The benchmark catalog is [`Utf8Pcre2BenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/Utf8Pcre2BenchmarkCatalog.cs). The semantic boundary remains the managed profile described in [`SPEC-PCRE2.md`](../../docs/SPEC-PCRE2.md), not the set of rows for which .NET has a comparator.
