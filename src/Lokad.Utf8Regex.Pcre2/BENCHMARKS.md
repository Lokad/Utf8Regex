<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `D13452E7874BAA1EF4D9D3395AEF6737E8DFF9E74F36A17F6B27B8DDC63E4A6E`
- Latest managed row measurement: `2026-11-07T03:48:41.8512794+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-07T03:48:41.8512794+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `72` managed faster, `1` equivalent, `12` native faster, `16` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `adda7bf55fa2` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `adda7bf55fa2` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.599 us | 2.199 us | 0.27x | 0.26–0.29x | -1.604 us | 9 pairs; 36/36 ms; 60,091/16,166 ops/lane; IQR 1.023/1.073 | `Pcre2Slice` | 468.438 us | 0.284 us | 0.835 us | 0.72x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.737 us | 11.565 us | 0.06x | 0.06–0.07x | -10.831 us | 9 pairs; 35/44 ms; 46,894/3,822 ops/lane; IQR 1.014/1.154 | `Pcre2Slice` | 0.520 us | 0.097 us | 3.459 us | 0.21x | 64 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.244 us | 0.01x | 0.01–0.01x | -1.233 us | 9 pairs; 39/40 ms; 3,634,159/31,821 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 0.106 us | 1.096 us | 1.125 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.689 us | 0.162 us | 4.24x | 4.21–4.28x | +0.526 us | 9 pairs; 37/40 ms; 53,252/245,832 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 0.048 us | 0.121 us | 0.162 us | 4.26x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.301 us | 0.572 us | 4.03x | 3.99–4.07x | +1.730 us | 9 pairs; 39/39 ms; 16,948/68,884 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0.117 us | 0.735 us | 0.772 us | 2.98x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.295 us | 0.34x | 0.34–0.34x | -0.195 us | 9 pairs; 40/39 ms; 396,710/132,478 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0.115 us | 0.318 us | 0.345 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.355 us | 0.30x | 0.30–0.30x | -0.249 us | 9 pairs; 39/40 ms; 371,167/111,845 ops/lane; IQR 1.009/1.008 | `Pcre2Slice` | 0.119 us | 0.434 us | 0.461 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.093 us | 0.290 us | 0.32x | 0.30–0.32x | -0.197 us | 9 pairs; 47/40 ms; 501,660/137,463 ops/lane; IQR 1.050/1.013 | `Pcre2Slice` | 0.091 us | 0.233 us | 0.256 us | 0.36x | 0 B | 0 B |
| `common/ip-miss` | **Inconclusive** | 15 B | 0.087 us | 0.317 us | 0.27x | 0.25–0.28x | -0.230 us | 9 pairs; 43/40 ms; 498,296/125,869 ops/lane; IQR 1.035/1.013 | `Pcre2Slice` | 0.091 us | 0.249 us | 0.270 us | 0.32x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.174 us | 0.077 us | 2.26x | 2.24–2.32x | +0.097 us | 9 pairs; 40/40 ms; 229,382/515,609 ops/lane; IQR 1.003/1.012 | `Pcre2Slice` | 0.091 us | 0.965 us | 0.991 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.195 us | 0.17x | 0.17–0.17x | -0.161 us | 9 pairs; 40/40 ms; 1,189,309/204,647 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0.112 us | 0.300 us | 0.338 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.613 us | 0.10x | 0.10–0.10x | -0.553 us | 9 pairs; 40/40 ms; 656,172/65,670 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0.137 us | 0.232 us | 0.258 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.155 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 37/39 ms; 1,120,231/251,639 ops/lane; IQR 1.024/1.012 | `Pcre2Backtracking` | 0.105 us | 0.188 us | 0.209 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.112 us | 0.18x | 0.17–0.18x | -0.092 us | 9 pairs; 38/40 ms; 1,956,372/354,588 ops/lane; IQR 1.027/1.009 | `Pcre2Backtracking` | 0.060 us | 0.092 us | 0.111 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.116 us | 0.50x | 0.49–0.50x | -0.058 us | 9 pairs; 40/38 ms; 690,417/324,139 ops/lane; IQR 1.006/1.017 | `Pcre2Backtracking` | 0.013 us | 0.085 us | 0.103 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.086 us | 0.277 us | 0.31x | 0.31–0.31x | -0.191 us | 9 pairs; 37/39 ms; 430,222/142,364 ops/lane; IQR 1.001/1.014 | `Pcre2Slice` | 0.097 us | 0.118 us | 0.155 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.116 us | 0.21x | 0.19–0.21x | -0.092 us | 9 pairs; 42/39 ms; 1,760,715/334,914 ops/lane; IQR 1.032/1.009 | `Pcre2Backtracking` | 0.020 us | 0.090 us | 0.109 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Managed faster** | 4,103 B | 0.127 us | 1.932 us | 0.07x | 0.06–0.07x | -1.805 us | 9 pairs; 43/40 ms; 339,082/20,938 ops/lane; IQR 1.018/1.043 | `Pcre2Literal` | 0.086 us | 0.050 us | 0.586 us | 0.22x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.123 us | 2.078 us | 0.06x | 0.05–0.07x | -1.955 us | 9 pairs; 42/43 ms; 337,952/20,873 ops/lane; IQR 1.003/1.213 | `Pcre2Literal` | 0.151 us | 0.054 us | 0.595 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 2.117 us | 0.07x | 0.07–0.08x | -1.961 us | 9 pairs; 40/35 ms; 257,473/16,735 ops/lane; IQR 1.005/1.091 | `Pcre2Literal` | 0.204 us | 0.233 us | 0.772 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.153 us | 1.879 us | 0.08x | 0.07–0.09x | -1.724 us | 9 pairs; 40/40 ms; 264,011/21,464 ops/lane; IQR 1.021/1.134 | `Pcre2Literal` | 0.180 us | 0.206 us | 0.745 us | 0.21x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.097 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 40/38 ms; 4,504,773/389,502 ops/lane; IQR 1.002/1.004 | `Pcre2Slice` | 0.234 us | 0.071 us | 0.089 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.088 us | 0.78x | 0.77–0.78x | -0.019 us | 9 pairs; 41/38 ms; 594,454/434,272 ops/lane; IQR 1.027/1.020 | `Pcre2Literal` | 0.130 us | 0.051 us | 0.073 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Inconclusive** | 23 B | 0.018 us | 0.112 us | 0.17x | 0.12–0.17x | -0.094 us | 9 pairs; 53/40 ms; 2,869,561/357,953 ops/lane; IQR 1.191/1.012 | `Pcre2Backtracking` | 0.282 us | 0.091 us | 0.113 us | 0.16x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.122 us | 0.93x | 0.92–0.94x | -0.008 us | 9 pairs; 36/39 ms; 313,798/318,927 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 0.137 us | 0.111 us | 0.143 us | 0.80x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.204 us | 0.37x | 0.36–0.38x | -0.128 us | 9 pairs; 40/41 ms; 528,692/199,756 ops/lane; IQR 1.007/1.029 | `Pcre2Slice` | 0.408 us | 0.074 us | 0.114 us | 0.67x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.265 us | 160.927 us | 0.51x | 0.51–0.51x | -78.662 us | 9 pairs; 40/40 ms; 487/250 ops/lane; IQR 1.008/1.006 | `Pcre2Slice` | 337.149 us | 100.127 us | 101.836 us | 0.81x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.538 us | 282.721 us | 0.14x | 0.14–0.14x | -243.087 us | 9 pairs; 40/40 ms; 1,011/141 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 270.927 us | 222.183 us | 228.489 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.428 us | 212.319 us | 0.04x | 0.04–0.04x | -202.868 us | 9 pairs; 40/39 ms; 4,231/186 ops/lane; IQR 1.002/1.009 | `Pcre2Backtracking` | 8.143 us | 218.094 us | 222.225 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 237.966 us | 206.201 us | 1.15x | 1.15–1.16x | +31.836 us | 9 pairs; 39/40 ms; 165/192 ops/lane; IQR 1.001/1.008 | `Pcre2SingleTokenRepeat` | 8.178 us | 229.762 us | 231.791 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.557 us | 23.097 us | 0.15x | 0.15–0.16x | -19.525 us | 9 pairs; 40/41 ms; 11,151/1,767 ops/lane; IQR 1.004/1.010 | `Pcre2Literal` | 1.370 us | 4.248 us | 5.988 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.662 us | 81.252 us | 0.12x | 0.12–0.12x | -71.591 us | 9 pairs; 40/40 ms; 4,123/492 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 9.325 us | 90.367 us | 91.322 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,188.700 us | 29,058.300 us | 0.14x | 0.14–0.15x | -24867.113 us | 9 pairs; 34/29 ms; 8/1 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 5,674.557 us | 11,544.243 us | 18,513.400 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,317.675 us | 55,052.400 us | 0.06x | 0.06–0.06x | -51710.375 us | 9 pairs; 40/55 ms; 12/1 ops/lane; IQR 1.008/1.015 | `Pcre2Backtracking` | 3,662.948 us | 42,559.350 us | 47,409.450 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,940.929 us | 480,019.400 us | 0.00x | 0.00–0.00x | -478075.610 us | 9 pairs; 41/480 ms; 21/1 ops/lane; IQR 1.029/1.003 | `Pcre2Character` | 1,785.444 us | 22,821.575 us | 29,805.600 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,560.150 us | 11,332.967 us | 0.49x | 0.48–0.52x | -5809.233 us | 9 pairs; 33/34 ms; 6/3 ops/lane; IQR 1.010/1.071 | `Pcre2Literal` | 1,305.934 us | 1,323.668 us | 8,337.811 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,776.750 us | 144,657.300 us | 0.13x | 0.13–0.13x | -125866.500 us | 9 pairs; 38/145 ms; 2/1 ops/lane; IQR 1.009/1.003 | `Pcre2Backtracking` | 1,232.861 us | 508.367 us | 8,571.022 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,920.762 us | 22,185.450 us | 0.22x | 0.22–0.22x | -17267.613 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.002/1.008 | `Pcre2Slice` | 2,465.790 us | 10,853.938 us | 18,883.300 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,582.600 us | 134,095.400 us | 0.17x | 0.17–0.17x | -111538.400 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.023/1.009 | `Pcre2Backtracking` | 1,540.918 us | 2,309.470 us | 10,720.375 us | 2.11x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 144.117 us | 987.252 us | 0.15x | 0.14–0.15x | -843.068 us | 9 pairs; 40/39 ms; 275/40 ops/lane; IQR 1.003/1.028 | `Pcre2Backtracking` | 162.530 us | 319.955 us | 557.885 us | 0.26x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,478.600 us | 13,491.767 us | 0.85x | 0.85–0.85x | -2011.733 us | 9 pairs; 34/40 ms; 3/3 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 308.594 us | 15,034.780 us | 15,353.180 us | 0.75x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,809.857 us | 98,724.400 us | 0.03x | 0.03–0.03x | -95920.621 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.007/1.002 | `Pcre2Character` | 1,919.822 us | 19,887.925 us | 20,162.875 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.358 us | 2,724.913 us | 0.07x | 0.07–0.07x | -2538.496 us | 9 pairs; 39/41 ms; 211/15 ops/lane; IQR 1.006/1.006 | `Pcre2SingleTokenRepeat` | 191.440 us | 1,019.611 us | 1,249.919 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,595.512 us | 20,200.750 us | 0.08x | 0.08–0.08x | -18590.252 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.007/1.002 | `Pcre2Backtracking` | 574.309 us | 10,627.029 us | 10,881.757 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.139 us | 1.098 us | 0.13x | 0.13–0.13x | -0.960 us | 9 pairs; 40/41 ms; 287,511/37,228 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 0.631 us | 0.261 us | 0.282 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.490 us | 0.833 us | 0.59x | 0.59–0.59x | -0.342 us | 9 pairs; 40/40 ms; 81,181/48,053 ops/lane; IQR 1.005/1.002 | `Pcre2Literal` | 0.147 us | 0.148 us | 0.172 us | 2.85x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.296 us | 1.184 us | 0.25x | 0.25–0.25x | -0.888 us | 9 pairs; 40/40 ms; 133,600/33,583 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 1.062 us | 0.347 us | 0.372 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.930 us | 1.130 us | 0.82x | 0.82–0.82x | -0.200 us | 9 pairs; 40/40 ms; 42,473/35,428 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 0.279 us | 0.424 us | 0.468 us | 1.99x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.161 us | 0.861 us | 0.19x | 0.19–0.19x | -0.700 us | 9 pairs; 42/40 ms; 263,044/46,682 ops/lane; IQR 1.014/1.002 | `Pcre2Slice` | 0.774 us | 0.155 us | 0.204 us | 0.79x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.893 us | 1.109 us | 0.80x | 0.80–0.81x | -0.217 us | 9 pairs; 40/40 ms; 45,220/36,264 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | 2.362 us | 0.307 us | 0.325 us | 2.75x | 0 B | 0 B |
| `simple/foo-dense` | **Inconclusive** | 18 B | 0.845 us | 0.835 us | 1.01x | 1.01–1.02x | +0.010 us | 9 pairs; 40/40 ms; 46,978/47,793 ops/lane; IQR 1.009/1.002 | `Pcre2Literal` | 3.590 us | 0.181 us | 0.203 us | 4.16x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.032 us | 1.188 us | 0.87x | 0.87–0.87x | -0.155 us | 9 pairs; 40/40 ms; 38,288/33,563 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 5.239 us | 0.404 us | 0.426 us | 2.42x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.213 us | 1.137 us | 1.07x | 1.06–1.08x | +0.077 us | 9 pairs; 40/40 ms; 32,896/34,994 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 3.689 us | 0.500 us | 0.542 us | 2.24x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.795 us | 0.860 us | 0.93x | 0.92–0.93x | -0.064 us | 9 pairs; 40/40 ms; 50,433/46,029 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 2.108 us | 0.183 us | 0.225 us | 3.54x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.568 us | 1.107 us | 0.51x | 0.51–0.51x | -0.539 us | 9 pairs; 40/40 ms; 70,755/35,908 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Inconclusive** | 18 B | 0.499 us | 0.835 us | 0.60x | 0.59–0.65x | -0.333 us | 9 pairs; 39/40 ms; 77,760/47,849 ops/lane; IQR 1.091/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.733 us | 1.187 us | 0.62x | 0.62–0.62x | -0.454 us | 9 pairs; 41/41 ms; 55,670/34,576 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.946 us | 1.135 us | 0.83x | 0.83–0.84x | -0.190 us | 9 pairs; 42/40 ms; 44,858/35,141 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.480 us | 0.861 us | 0.56x | 0.56–0.56x | -0.380 us | 9 pairs; 40/40 ms; 84,204/46,744 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.159 us | — | — | — | — | — | `—` | 25.949 us | 97.697 us | 100.128 us | 0.61x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.680 us | — | — | — | — | — | `—` | 1.456 us | 0.364 us | 0.390 us | 9.43x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.104 us | — | — | — | — | — | `—` | 0.922 us | 0.246 us | 0.276 us | 4.00x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.226 us | — | — | — | — | — | `—` | 3.060 us | 0.457 us | 0.492 us | 2.49x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.526 us | — | — | — | — | — | `—` | 1.041 us | 0.545 us | 0.588 us | 2.60x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.686 us | — | — | — | — | — | `—` | 1.499 us | 0.271 us | 0.319 us | 11.55x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.089 us | 0.084 us | 1.05x | 1.03–1.06x | +0.004 us | 9 pairs; 41/40 ms; 458,647/469,560 ops/lane; IQR 1.010/1.028 | `Pcre2Character` | 0 B | 0 B | 2.300 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.125 us | 0.10x | 0.10–0.10x | -0.112 us | 9 pairs; 39/39 ms; 3,113,719/312,632 ops/lane; IQR 1.003/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 128.231 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.375 us | 0.103 us | 3.62x | 3.59–3.64x | +0.271 us | 9 pairs; 36/39 ms; 95,013/381,118 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 20.444 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.389 us | 0.119 us | 3.27x | 3.24–3.28x | +0.270 us | 9 pairs; 35/40 ms; 90,909/337,249 ops/lane; IQR 1.013/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 71.287 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.395 us | 0.104 us | 3.78x | 3.75–3.80x | +0.291 us | 9 pairs; 36/40 ms; 90,415/378,850 ops/lane; IQR 1.002/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 70.537 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.655 us | 0.347 us | 1.89x | 1.89–1.90x | +0.309 us | 9 pairs; 38/39 ms; 57,704/113,636 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.635 us | 0.02x | 0.02–0.02x | -0.623 us | 9 pairs; 42/40 ms; 3,514,780/62,869 ops/lane; IQR 1.026/1.026 | `Pcre2Backtracking` | 0 B | 0 B | 21.669 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.518 us | 0.02x | 0.02–0.02x | -0.506 us | 9 pairs; 40/40 ms; 3,494,943/77,116 ops/lane; IQR 1.014/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 20.881 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.479 us | 0.106 us | 4.53x | 4.42–4.55x | +0.373 us | 9 pairs; 37/40 ms; 76,693/376,148 ops/lane; IQR 1.013/1.040 | `Pcre2Backtracking` | 0 B | 0 B | 18.163 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.456 us | 10.440 us | 0.81x | 0.80–0.82x | -1.994 us | 9 pairs; 41/41 ms; 4,825/3,893 ops/lane; IQR 1.006/1.012 | `Pcre2Literal` | 0 B | 0 B | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.300 us | 1.064 us | 0.28x | 0.28–0.28x | -0.763 us | 9 pairs; 37/40 ms; 123,983/38,023 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 127.694 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.328 us | 0.463 us | 0.70x | 0.69–0.71x | -0.138 us | 9 pairs; 37/38 ms; 113,400/81,863 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 114.263 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Inconclusive** | 31 B | 0.380 us | 0.479 us | 0.79x | 0.66–0.80x | -0.100 us | 9 pairs; 48/40 ms; 125,666/84,217 ops/lane; IQR 1.127/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 140.125 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.308 us | 0.590 us | 0.52x | 0.52–0.53x | -0.283 us | 9 pairs; 30/40 ms; 98,961/67,057 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 102.100 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.300 us | 0.399 us | 0.76x | 0.73–0.77x | -0.097 us | 9 pairs; 37/40 ms; 124,862/99,384 ops/lane; IQR 1.023/1.028 | `Pcre2Backtracking` | 0 B | 0 B | 215.137 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.967 us | 8.821 us | 0.56x | 0.56–0.56x | -3.860 us | 9 pairs; 40/40 ms; 8,106/4,522 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 0 B | 0 B | 1.475 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.261 us | 0.317 us | 0.83x | 0.82–0.85x | -0.054 us | 9 pairs; 36/40 ms; 138,901/127,096 ops/lane; IQR 1.005/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 92.400 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.302 us | 0.408 us | 0.74x | 0.67–0.74x | -0.106 us | 9 pairs; 41/40 ms; 137,417/98,268 ops/lane; IQR 1.077/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 123.431 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.862 us | — | — | — | — | — | `—` | 0 B | — | 15.656 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.199 us | 0.293 us | 0.68x | 0.67–0.68x | -0.095 us | 9 pairs; 34/39 ms; 170,184/133,856 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 54.438 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.783 us | — | — | — | — | — | `—` | 0 B | — | 14.887 us | 10,536 B |
| `pcre2/same-start-global` | **Inconclusive** | 9 B | 0.530 us | 0.671 us | 0.79x | 0.77–0.80x | -0.141 us | 9 pairs; 37/39 ms; 69,981/58,295 ops/lane; IQR 1.004/1.014 | `Pcre2Slice` | 0 B | 0 B | 64.000 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.916 us | 29.125 us | 0.34x | 0.34–0.34x | -19.219 us | 9 pairs; 40/40 ms; 3,999/1,381 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 1.175 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.961 us | 1.121 us | 0.86x | 0.85–0.86x | -0.160 us | 9 pairs; 40/39 ms; 41,328/35,129 ops/lane; IQR 1.006/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 126.656 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.994 us | 1.197 us | 0.83x | 0.83–0.84x | -0.201 us | 9 pairs; 40/41 ms; 39,951/34,290 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 113.812 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.936 us | 1.036 us | 0.90x | 0.90–0.91x | -0.099 us | 9 pairs; 40/39 ms; 42,294/37,980 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 143.806 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.938 us | 1.181 us | 0.79x | 0.79–0.80x | -0.243 us | 9 pairs; 42/41 ms; 44,919/35,028 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 101.406 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.985 us | 0.948 us | 1.04x | 1.04–1.05x | +0.037 us | 9 pairs; 39/40 ms; 39,984/42,211 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 213.544 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.575 us | 8.879 us | 0.63x | 0.63–0.63x | -3.304 us | 9 pairs; 40/40 ms; 7,212/4,516 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.923 us | 0.892 us | 1.03x | 1.03–1.04x | +0.030 us | 9 pairs; 40/41 ms; 43,553/46,073 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 92.944 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Equivalent** | 26 B | 0.970 us | 0.968 us | 1.00x | 1.00–1.00x | +0.003 us | 9 pairs; 40/40 ms; 41,565/41,191 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 122.787 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.123 us | — | — | — | — | — | `—` | 0 B | — | 13.469 us | 11,496 B |
| `pcre2/kreset-global` | **Inconclusive** | 26 B | 0.848 us | 0.868 us | 0.98x | 0.97–0.98x | -0.020 us | 9 pairs; 41/40 ms; 48,066/46,071 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 55.156 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.105 us | — | — | — | — | — | `—` | 0 B | — | 13.875 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.855 us | 1.261 us | 0.68x | 0.68–0.69x | -0.404 us | 9 pairs; 39/40 ms; 45,295/31,635 ops/lane; IQR 1.009/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.731 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.457 us | 2.288 us | 0.64x | 0.63–0.64x | -0.832 us | 9 pairs; 40/37 ms; 27,591/16,271 ops/lane; IQR 1.004/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.831 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.708 us | 1.120 us | 0.63x | 0.63–0.64x | -0.412 us | 9 pairs; 45/40 ms; 63,958/35,868 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 132.787 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.683 us | 1.189 us | 0.57x | 0.57–0.58x | -0.506 us | 9 pairs; 40/40 ms; 58,064/33,927 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.450 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.618 us | 1.037 us | 0.60x | 0.59–0.60x | -0.419 us | 9 pairs; 40/40 ms; 65,285/38,596 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 141.625 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.624 us | 1.178 us | 0.53x | 0.53–0.53x | -0.553 us | 9 pairs; 37/40 ms; 59,572/33,589 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 101.312 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.634 us | 0.947 us | 0.67x | 0.66–0.67x | -0.313 us | 9 pairs; 40/40 ms; 62,695/42,272 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 217.331 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.937 us | 2.194 us | 0.88x | 0.88–0.89x | -0.253 us | 9 pairs; 40/40 ms; 20,778/18,341 ops/lane; IQR 1.002/1.004 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.607 us | 0.886 us | 0.69x | 0.68–0.69x | -0.279 us | 9 pairs; 40/39 ms; 66,281/44,408 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 94.481 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.607 us | 0.970 us | 0.63x | 0.62–0.63x | -0.364 us | 9 pairs; 40/40 ms; 66,583/41,011 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 123.112 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.811 us | — | — | — | — | — | `—` | 0 B | — | 13.588 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.501 us | 0.867 us | 0.58x | 0.58–0.58x | -0.365 us | 9 pairs; 40/40 ms; 79,526/46,116 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.225 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.738 us | — | — | — | — | — | `—` | 0 B | — | 13.969 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.591 us | 1.259 us | 0.47x | 0.46–0.47x | -0.668 us | 9 pairs; 40/39 ms; 67,471/31,106 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 0 B | 0 B | 66.188 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.278 us | — | — | — | — | — | `—` | 480 B | — | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.143 us | — | — | — | — | — | `—` | 48 B | — | 128.637 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.245 us | — | — | — | — | — | `—` | 48 B | — | 114.100 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.131 us | — | — | — | — | — | `—` | 48 B | — | 140.613 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.145 us | — | — | — | — | — | `—` | 48 B | — | 99.069 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.132 us | — | — | — | — | — | `—` | 48 B | — | 215.725 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.894 us | — | — | — | — | — | `—` | 64 B | — | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.147 us | — | — | — | — | — | `—` | 40 B | — | 93.994 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.154 us | — | — | — | — | — | `—` | 40 B | — | 120.200 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.314 us | — | — | — | — | — | `—` | 1,216 B | — | 13.200 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.083 us | — | — | — | — | — | `—` | 48 B | — | 53.788 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 17.048 us | — | — | — | — | — | `—` | 928 B | — | 14.300 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.751 us | — | — | — | — | — | `—` | 4,496 B | — | 65.131 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 32.123–250.644 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.336–94.541 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.631–12.888 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.147–50.808 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.035–1.149 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 6.055–46.995 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.186–23.958 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.186–99.975 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.152–72.316 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.219–5.514 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.359–376.824 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.077 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 40.109–312.354 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.856–5.121 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.140–15.745 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 106.588–827.842 us |

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
