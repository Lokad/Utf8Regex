<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `CDFBAF7545E95913B6CEE2780293E9CBE204FE7C6DEB088BA096C0EAE4A36102`
- Latest managed row measurement: `2026-11-06T03:16:16.1605293+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T03:16:16.1605293+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `78` managed faster, `1` equivalent, `12` native faster, `10` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `07b10eabfb99` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `07b10eabfb99` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.676 us | 2.234 us | 0.30x | 0.28–0.33x | -1.565 us | 9 pairs; 36/41 ms; 52,772/18,304 ops/lane; IQR 1.023/1.124 | `Pcre2Slice` | 471.020 us | 0.281 us | 0.847 us | 0.80x | 1,248 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.882 us | 11.766 us | 0.08x | 0.07–0.08x | -10.846 us | 9 pairs; 36/44 ms; 41,396/3,743 ops/lane; IQR 1.021/1.084 | `Pcre2Slice` | 0.658 us | 0.097 us | 3.625 us | 0.24x | 720 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.226 us | 0.01x | 0.01–0.01x | -1.215 us | 9 pairs; 41/41 ms; 3,683,695/33,299 ops/lane; IQR 1.012/1.005 | `Pcre2Slice` | 0.106 us | 1.042 us | 1.101 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.702 us | 0.158 us | 4.42x | 4.37–4.47x | +0.541 us | 9 pairs; 37/40 ms; 53,370/254,467 ops/lane; IQR 1.017/1.023 | `Pcre2Backtracking` | 0.046 us | 0.123 us | 0.161 us | 4.36x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.387 us | 0.574 us | 4.14x | 4.12–4.18x | +1.813 us | 9 pairs; 39/40 ms; 16,460/69,409 ops/lane; IQR 1.004/1.011 | `Pcre2Backtracking` | 0.125 us | 0.772 us | 0.808 us | 2.96x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.107 us | 0.318 us | 0.34x | 0.32–0.34x | -0.210 us | 9 pairs; 43/39 ms; 396,787/121,663 ops/lane; IQR 1.021/1.009 | `Pcre2Slice` | 0.114 us | 0.321 us | 0.345 us | 0.31x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.105 us | 0.355 us | 0.30x | 0.30–0.30x | -0.250 us | 9 pairs; 40/39 ms; 384,069/110,772 ops/lane; IQR 1.004/1.006 | `Pcre2Slice` | 0.119 us | 0.448 us | 0.475 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.287 us | 0.28x | 0.27–0.28x | -0.207 us | 9 pairs; 36/39 ms; 456,333/136,215 ops/lane; IQR 1.013/1.016 | `Pcre2Slice` | 0.091 us | 0.239 us | 0.260 us | 0.31x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.081 us | 0.314 us | 0.26x | 0.25–0.28x | -0.234 us | 9 pairs; 41/40 ms; 507,303/126,641 ops/lane; IQR 1.094/1.008 | `Pcre2Slice` | 0.090 us | 0.252 us | 0.278 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.172 us | 0.076 us | 2.26x | 2.24–2.35x | +0.096 us | 9 pairs; 40/43 ms; 233,331/556,090 ops/lane; IQR 1.010/1.021 | `Pcre2Slice` | 0.090 us | 0.910 us | 0.971 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.197 us | 0.17x | 0.17–0.17x | -0.164 us | 9 pairs; 39/42 ms; 1,169,998/214,407 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0.112 us | 0.305 us | 0.346 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.605 us | 0.10x | 0.10–0.10x | -0.544 us | 9 pairs; 40/38 ms; 664,230/62,728 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0.140 us | 0.242 us | 0.257 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.22x | 0.21–0.22x | -0.120 us | 9 pairs; 37/39 ms; 1,118,704/253,266 ops/lane; IQR 1.026/1.004 | `Pcre2Backtracking` | 0.108 us | 0.182 us | 0.198 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.022 us | 0.109 us | 0.20x | 0.18–0.20x | -0.087 us | 9 pairs; 43/39 ms; 1,949,316/353,921 ops/lane; IQR 1.064/1.021 | `Pcre2Backtracking` | 0.060 us | 0.092 us | 0.112 us | 0.20x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.123 us | 0.47x | 0.46–0.47x | -0.066 us | 9 pairs; 40/38 ms; 690,770/308,971 ops/lane; IQR 1.011/1.010 | `Pcre2Backtracking` | 0.015 us | 0.090 us | 0.108 us | 0.53x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.085 us | 0.277 us | 0.31x | 0.30–0.31x | -0.192 us | 9 pairs; 40/37 ms; 471,244/133,912 ops/lane; IQR 1.011/1.004 | `Pcre2Slice` | 0.097 us | 0.121 us | 0.155 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.118 us | 0.19x | 0.18–0.19x | -0.096 us | 9 pairs; 40/42 ms; 1,804,564/358,143 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0.019 us | 0.098 us | 0.113 us | 0.19x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.099 us | 0.06x | 0.05–0.06x | -1.981 us | 9 pairs; 41/46 ms; 345,029/22,051 ops/lane; IQR 1.004/1.087 | `Pcre2Literal` | 0.086 us | 0.050 us | 0.567 us | 0.21x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.120 us | 1.974 us | 0.06x | 0.05–0.07x | -1.853 us | 9 pairs; 41/38 ms; 338,070/19,367 ops/lane; IQR 1.009/1.259 | `Pcre2Literal` | 0.145 us | 0.054 us | 0.636 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.155 us | 2.213 us | 0.07x | 0.06–0.08x | -2.059 us | 9 pairs; 41/44 ms; 261,786/19,765 ops/lane; IQR 1.005/1.264 | `Pcre2Literal` | 0.202 us | 0.232 us | 0.798 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.869 us | 0.08x | 0.08–0.08x | -1.717 us | 9 pairs; 40/38 ms; 263,905/20,084 ops/lane; IQR 1.004/1.056 | `Pcre2Literal` | 0.175 us | 0.204 us | 0.746 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.097 us | 0.09x | 0.09–0.09x | -0.088 us | 9 pairs; 39/40 ms; 4,488,767/415,534 ops/lane; IQR 1.007/1.013 | `Pcre2Slice` | 0.251 us | 0.074 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.089 us | 0.75x | 0.75–0.76x | -0.022 us | 9 pairs; 40/40 ms; 592,522/451,267 ops/lane; IQR 1.006/1.009 | `Pcre2Literal` | 0.125 us | 0.050 us | 0.073 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.109 us | 0.12x | 0.12–0.13x | -0.095 us | 9 pairs; 39/42 ms; 2,884,295/381,781 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0.269 us | 0.094 us | 0.117 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.114 us | 0.122 us | 0.94x | 0.93–0.99x | -0.007 us | 9 pairs; 38/41 ms; 329,402/337,397 ops/lane; IQR 1.060/1.002 | `Pcre2Character` | 0.133 us | 0.111 us | 0.144 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.075 us | 0.208 us | 0.36x | 0.36–0.36x | -0.133 us | 9 pairs; 40/40 ms; 537,185/194,646 ops/lane; IQR 1.002/1.012 | `Pcre2Slice` | 0.426 us | 0.074 us | 0.109 us | 0.69x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.463 us | 159.400 us | 0.52x | 0.51–0.52x | -77.046 us | 9 pairs; 40/40 ms; 486/250 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 325.229 us | 105.126 us | 105.070 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.266 us | 278.676 us | 0.14x | 0.14–0.14x | -239.444 us | 9 pairs; 40/40 ms; 1,010/143 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 263.715 us | 216.515 us | 228.679 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.390 us | 211.395 us | 0.04x | 0.04–0.04x | -201.999 us | 9 pairs; 40/42 ms; 4,233/198 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 8.044 us | 223.416 us | 226.684 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 238.734 us | 204.576 us | 1.17x | 1.16–1.18x | +34.373 us | 9 pairs; 40/39 ms; 166/191 ops/lane; IQR 1.009/1.017 | `Pcre2SingleTokenRepeat` | 8.053 us | 231.469 us | 234.174 us | 1.02x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.523 us | 23.577 us | 0.15x | 0.15–0.15x | -20.054 us | 9 pairs; 40/38 ms; 11,278/1,616 ops/lane; IQR 1.006/1.011 | `Pcre2Literal` | 1.373 us | 4.207 us | 5.891 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.413 us | 80.212 us | 0.12x | 0.12–0.12x | -70.811 us | 9 pairs; 39/40 ms; 4,159/502 ops/lane; IQR 1.007/1.018 | `Pcre2Backtracking` | 9.198 us | 91.334 us | 93.104 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,206.275 us | 27,834.800 us | 0.15x | 0.15–0.16x | -23631.100 us | 9 pairs; 34/56 ms; 8/2 ops/lane; IQR 1.006/1.038 | `Pcre2Backtracking` | 5,463.970 us | 11,678.120 us | 18,389.000 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,343.133 us | 54,187.000 us | 0.06x | 0.06–0.06x | -50843.867 us | 9 pairs; 40/54 ms; 12/1 ops/lane; IQR 1.010/1.020 | `Pcre2Backtracking` | 3,835.664 us | 40,485.650 us | 46,467.450 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,894.909 us | 479,392.300 us | 0.00x | 0.00–0.00x | -477473.459 us | 9 pairs; 42/479 ms; 22/1 ops/lane; IQR 1.020/1.002 | `Pcre2Character` | 1,765.745 us | 23,231.633 us | 29,717.900 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,458.083 us | 9,733.267 us | 0.57x | 0.47–0.63x | -4231.267 us | 9 pairs; 33/29 ms; 6/3 ops/lane; IQR 1.017/1.265 | `Pcre2Literal` | 1,368.389 us | 1,336.075 us | 8,154.429 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,707.800 us | 144,532.500 us | 0.13x | 0.13–0.13x | -125912.800 us | 9 pairs; 37/145 ms; 2/1 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 1,201.502 us | 503.251 us | 8,402.433 us | 2.23x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Inconclusive** | 6,839,410 B | 4,883.113 us | 21,479.800 us | 0.22x | 0.22–0.23x | -16658.188 us | 9 pairs; 39/43 ms; 8/2 ops/lane; IQR 1.013/1.023 | `Pcre2Slice` | 2,410.610 us | 10,826.960 us | 18,771.967 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,092.900 us | 133,586.900 us | 0.17x | 0.16–0.17x | -111326.850 us | 9 pairs; 44/134 ms; 2/1 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 1,487.968 us | 2,315.445 us | 10,225.740 us | 2.16x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 157.947 us | 995.538 us | 0.16x | 0.16–0.16x | -838.654 us | 9 pairs; 39/40 ms; 245/40 ops/lane; IQR 1.017/1.013 | `Pcre2Backtracking` | 167.930 us | 320.970 us | 534.668 us | 0.30x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,136.567 us | 13,227.800 us | 0.91x | 0.91–0.92x | -1159.667 us | 9 pairs; 36/40 ms; 3/3 ops/lane; IQR 1.011/1.014 | `Pcre2Backtracking` | 305.978 us | 14,992.875 us | 15,170.850 us | 0.80x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,772.914 us | 97,686.800 us | 0.03x | 0.03–0.03x | -94898.779 us | 9 pairs; 39/98 ms; 14/1 ops/lane; IQR 1.013/1.007 | `Pcre2Character` | 2,030.444 us | 19,798.767 us | 20,060.000 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.298 us | 2,757.187 us | 0.07x | 0.07–0.07x | -2573.560 us | 9 pairs; 39/41 ms; 213/15 ops/lane; IQR 1.005/1.015 | `Pcre2SingleTokenRepeat` | 191.537 us | 1,013.210 us | 1,256.318 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,643.909 us | 20,076.650 us | 0.08x | 0.08–0.08x | -18428.215 us | 9 pairs; 38/40 ms; 23/2 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 577.937 us | 10,775.580 us | 11,060.900 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.154 us | 1.091 us | 0.14x | 0.14–0.14x | -0.938 us | 9 pairs; 40/40 ms; 257,760/36,356 ops/lane; IQR 1.010/1.006 | `Pcre2Slice` | 0.655 us | 0.259 us | 0.281 us | 0.55x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.492 us | 0.823 us | 0.60x | 0.60–0.60x | -0.330 us | 9 pairs; 39/40 ms; 78,295/48,414 ops/lane; IQR 1.004/1.004 | `Pcre2Literal` | 0.151 us | 0.147 us | 0.170 us | 2.89x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.297 us | 1.225 us | 0.24x | 0.24–0.24x | -0.928 us | 9 pairs; 40/40 ms; 134,636/32,574 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 1.076 us | 0.353 us | 0.375 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.914 us | 1.121 us | 0.82x | 0.81–0.82x | -0.206 us | 9 pairs; 39/40 ms; 43,073/35,399 ops/lane; IQR 1.006/1.008 | `Pcre2Character` | 0.285 us | 0.430 us | 0.472 us | 1.94x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.147 us | 0.851 us | 0.17x | 0.17–0.17x | -0.704 us | 9 pairs; 41/41 ms; 277,951/48,254 ops/lane; IQR 1.006/1.009 | `Pcre2Slice` | 0.787 us | 0.153 us | 0.202 us | 0.73x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.950 us | 1.097 us | 0.87x | 0.86–0.87x | -0.145 us | 9 pairs; 39/40 ms; 40,843/36,869 ops/lane; IQR 1.009/1.006 | `Pcre2Slice` | 2.958 us | 0.306 us | 0.325 us | 2.92x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.784 us | 0.829 us | 0.94x | 0.94–0.95x | -0.046 us | 9 pairs; 40/40 ms; 50,831/47,856 ops/lane; IQR 1.004/1.004 | `Pcre2Literal` | 3.486 us | 0.180 us | 0.202 us | 3.88x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.014 us | 1.178 us | 0.86x | 0.86–0.87x | -0.163 us | 9 pairs; 42/41 ms; 41,420/34,501 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 5.202 us | 0.422 us | 0.432 us | 2.35x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.211 us | 1.130 us | 1.07x | 1.07–1.07x | +0.081 us | 9 pairs; 42/41 ms; 34,472/36,344 ops/lane; IQR 1.007/1.003 | `Pcre2Character` | 3.620 us | 0.489 us | 0.527 us | 2.30x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.791 us | 0.847 us | 0.93x | 0.93–0.94x | -0.061 us | 9 pairs; 40/40 ms; 50,112/46,912 ops/lane; IQR 1.007/1.012 | `Pcre2Slice` | 2.103 us | 0.182 us | 0.225 us | 3.52x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.615 us | 1.101 us | 0.56x | 0.56–0.56x | -0.484 us | 9 pairs; 40/40 ms; 65,168/35,927 ops/lane; IQR 1.007/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.492 us | 0.824 us | 0.60x | 0.60–0.60x | -0.331 us | 9 pairs; 39/40 ms; 79,217/47,957 ops/lane; IQR 1.010/1.012 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.653 us | 1.179 us | 0.55x | 0.55–0.55x | -0.527 us | 9 pairs; 38/40 ms; 58,334/33,805 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.905 us | 1.118 us | 0.81x | 0.80–0.82x | -0.214 us | 9 pairs; 42/40 ms; 46,643/35,486 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.475 us | 0.852 us | 0.56x | 0.55–0.56x | -0.380 us | 9 pairs; 40/40 ms; 83,120/47,089 ops/lane; IQR 1.006/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.345 us | — | — | — | — | — | `—` | 25.374 us | 97.417 us | 98.051 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.976 us | — | — | — | — | — | `—` | 1.621 us | 0.362 us | 0.383 us | 12.98x | 7,056 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.081 us | — | — | — | — | — | `—` | 0.941 us | 0.234 us | 0.259 us | 4.17x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.197 us | — | — | — | — | — | `—` | 3.067 us | 0.467 us | 0.492 us | 2.43x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.479 us | — | — | — | — | — | `—` | 1.068 us | 0.540 us | 0.587 us | 2.52x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.651 us | — | — | — | — | — | `—` | 1.525 us | 0.261 us | 0.312 us | 11.71x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.090 us | 0.083 us | 1.07x | 1.07–1.08x | +0.006 us | 9 pairs; 40/39 ms; 451,562/469,931 ops/lane; IQR 1.009/1.017 | `Pcre2Character` | 0 B | 0 B | 2.206 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.120 us | 0.11x | 0.10–0.11x | -0.107 us | 9 pairs; 40/36 ms; 3,030,683/301,313 ops/lane; IQR 1.011/1.045 | `Pcre2Backtracking` | 0 B | 0 B | 128.531 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.394 us | 0.103 us | 3.83x | 3.72–3.85x | +0.292 us | 9 pairs; 36/39 ms; 91,983/374,417 ops/lane; IQR 1.008/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 21.950 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.393 us | 0.118 us | 3.36x | 3.31–3.36x | +0.276 us | 9 pairs; 36/41 ms; 92,165/345,769 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 70.263 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.386 us | 0.104 us | 3.71x | 3.65–3.73x | +0.282 us | 9 pairs; 36/40 ms; 93,649/385,724 ops/lane; IQR 1.011/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 69.787 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.663 us | 0.344 us | 1.93x | 1.92–1.94x | +0.320 us | 9 pairs; 38/39 ms; 57,913/112,812 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.633 us | 0.02x | 0.02–0.02x | -0.621 us | 9 pairs; 39/39 ms; 3,418,378/61,314 ops/lane; IQR 1.008/1.025 | `Pcre2Backtracking` | 0 B | 0 B | 21.844 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.497 us | 0.02x | 0.02–0.02x | -0.485 us | 9 pairs; 41/38 ms; 3,427,316/77,290 ops/lane; IQR 1.039/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 20.894 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.464 us | 0.110 us | 4.23x | 4.19–4.25x | +0.354 us | 9 pairs; 36/40 ms; 78,558/359,508 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 18.300 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.391 us | 10.586 us | 0.80x | 0.79–0.80x | -2.153 us | 9 pairs; 39/41 ms; 4,673/3,831 ops/lane; IQR 1.007/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.290 us | 1.038 us | 0.28x | 0.28–0.28x | -0.747 us | 9 pairs; 39/39 ms; 133,625/37,769 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 128.650 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.336 us | 0.462 us | 0.72x | 0.71–0.73x | -0.131 us | 9 pairs; 36/40 ms; 107,249/85,800 ops/lane; IQR 1.024/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 111.369 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.295 us | 0.456 us | 0.65x | 0.64–0.65x | -0.161 us | 9 pairs; 33/38 ms; 111,423/82,699 ops/lane; IQR 1.018/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 145.050 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.300 us | 0.585 us | 0.51x | 0.51–0.52x | -0.283 us | 9 pairs; 36/41 ms; 120,123/69,853 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 101.969 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.292 us | 0.369 us | 0.80x | 0.79–0.80x | -0.075 us | 9 pairs; 38/43 ms; 129,743/116,155 ops/lane; IQR 1.009/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 212.713 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.909 us | 8.776 us | 0.56x | 0.56–0.56x | -3.880 us | 9 pairs; 40/40 ms; 8,162/4,538 ops/lane; IQR 1.006/1.006 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.259 us | 0.307 us | 0.85x | 0.84–0.86x | -0.046 us | 9 pairs; 36/42 ms; 138,626/136,708 ops/lane; IQR 1.003/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 93.006 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.270 us | 0.400 us | 0.68x | 0.67–0.68x | -0.130 us | 9 pairs; 36/39 ms; 132,306/97,034 ops/lane; IQR 1.002/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 122.081 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.776 us | — | — | — | — | — | `—` | 0 B | — | 14.594 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.200 us | 0.291 us | 0.69x | 0.68–0.69x | -0.091 us | 9 pairs; 41/40 ms; 206,232/138,585 ops/lane; IQR 1.019/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 51.056 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.694 us | — | — | — | — | — | `—` | 0 B | — | 13.844 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.580 us | 0.673 us | 0.86x | 0.86–0.87x | -0.091 us | 9 pairs; 37/39 ms; 63,748/57,894 ops/lane; IQR 1.009/1.007 | `Pcre2Slice` | 0 B | 0 B | 65.031 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.877 us | 28.471 us | 0.35x | 0.34–0.35x | -18.621 us | 9 pairs; 39/40 ms; 3,978/1,389 ops/lane; IQR 1.004/1.007 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.957 us | 1.070 us | 0.90x | 0.89–0.90x | -0.111 us | 9 pairs; 38/40 ms; 39,676/37,085 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 128.731 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.006 us | 1.189 us | 0.84x | 0.83–0.85x | -0.186 us | 9 pairs; 41/40 ms; 40,534/33,431 ops/lane; IQR 1.006/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 114.862 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.964 us | 1.026 us | 0.94x | 0.94–0.95x | -0.061 us | 9 pairs; 39/40 ms; 40,291/39,161 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 141.031 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 1.016 us | 1.163 us | 0.87x | 0.87–0.88x | -0.147 us | 9 pairs; 44/42 ms; 43,219/36,040 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 103.556 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.935 us | 0.940 us | 0.99x | 0.99–1.00x | -0.007 us | 9 pairs; 40/40 ms; 42,364/42,902 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 213.556 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.593 us | 8.846 us | 0.63x | 0.63–0.63x | -3.260 us | 9 pairs; 39/40 ms; 7,055/4,507 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.909 us | 0.883 us | 1.03x | 1.03–1.03x | +0.025 us | 9 pairs; 41/40 ms; 45,609/45,074 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 92.850 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.912 us | 0.961 us | 0.95x | 0.94–0.96x | -0.047 us | 9 pairs; 39/39 ms; 43,170/41,066 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 121.713 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.940 us | — | — | — | — | — | `—` | 0 B | — | 13.688 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.835 us | 0.861 us | 0.97x | 0.96–0.98x | -0.026 us | 9 pairs; 39/41 ms; 46,813/47,229 ops/lane; IQR 1.016/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 54.219 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.871 us | — | — | — | — | — | `—` | 0 B | — | 15.006 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.871 us | 1.252 us | 0.70x | 0.69–0.70x | -0.381 us | 9 pairs; 41/39 ms; 46,922/31,153 ops/lane; IQR 1.007/1.006 | `Pcre2Slice` | 0 B | 0 B | 64.181 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.469 us | 2.270 us | 0.65x | 0.64–0.65x | -0.804 us | 9 pairs; 40/41 ms; 27,439/18,034 ops/lane; IQR 1.006/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.647 us | 1.064 us | 0.61x | 0.60–0.61x | -0.415 us | 9 pairs; 40/39 ms; 61,794/36,652 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 129.225 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.681 us | 1.190 us | 0.57x | 0.57–0.58x | -0.509 us | 9 pairs; 40/41 ms; 58,273/34,302 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 114.069 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.610 us | 1.029 us | 0.59x | 0.59–0.59x | -0.421 us | 9 pairs; 41/42 ms; 66,472/41,118 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 143.219 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.619 us | 1.163 us | 0.53x | 0.53–0.54x | -0.544 us | 9 pairs; 37/41 ms; 60,268/35,187 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.600 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.634 us | 0.939 us | 0.68x | 0.67–0.68x | -0.305 us | 9 pairs; 37/40 ms; 58,481/42,406 ops/lane; IQR 1.007/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 211.950 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.908 us | 2.194 us | 0.87x | 0.87–0.88x | -0.281 us | 9 pairs; 40/40 ms; 20,773/18,192 ops/lane; IQR 1.007/1.004 | `Pcre2Character` | 0 B | 0 B | 1.356 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.616 us | 0.879 us | 0.70x | 0.70–0.70x | -0.262 us | 9 pairs; 37/41 ms; 60,371/46,528 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 93.094 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.643 us | 0.964 us | 0.67x | 0.66–0.68x | -0.322 us | 9 pairs; 42/40 ms; 65,847/41,639 ops/lane; IQR 1.005/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 122.406 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.665 us | — | — | — | — | — | `—` | 0 B | — | 13.694 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.512 us | 0.855 us | 0.60x | 0.60–0.60x | -0.343 us | 9 pairs; 42/41 ms; 81,875/48,015 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.025 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.613 us | — | — | — | — | — | `—` | 0 B | — | 14.300 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.582 us | 1.257 us | 0.46x | 0.46–0.47x | -0.676 us | 9 pairs; 39/41 ms; 66,626/32,979 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | 0 B | 0 B | 63.513 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.035 us | — | — | — | — | — | `—` | 480 B | — | 0.713 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.121 us | — | — | — | — | — | `—` | 48 B | — | 126.963 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.208 us | — | — | — | — | — | `—` | 48 B | — | 114.513 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.093 us | — | — | — | — | — | `—` | 48 B | — | 141.488 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.114 us | — | — | — | — | — | `—` | 48 B | — | 101.112 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.098 us | — | — | — | — | — | `—` | 48 B | — | 216.662 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.744 us | — | — | — | — | — | `—` | 64 B | — | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.144 us | — | — | — | — | — | `—` | 40 B | — | 94.888 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.127 us | — | — | — | — | — | `—` | 40 B | — | 122.044 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.836 us | — | — | — | — | — | `—` | 1,216 B | — | 13.500 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.038 us | — | — | — | — | — | `—` | 48 B | — | 52.850 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.634 us | — | — | — | — | — | `—` | 928 B | — | 13.856 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.654 us | — | — | — | — | — | `—` | 7,184 B | — | 63.656 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 32.333–248.400 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.326–95.122 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.049–12.611 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.836–47.608 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.036–1.141 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.670–44.339 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.636–22.194 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.192–99.441 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.067–72.396 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.176–5.479 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.749–376.830 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.801–303.052 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.844–5.079 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.157–15.506 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 104.524–826.234 us |

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
