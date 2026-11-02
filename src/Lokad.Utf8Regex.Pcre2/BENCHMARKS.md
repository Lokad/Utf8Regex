<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `8FBD5C595CFFAB981C0FC4194D46DE3D49B3C40AFD9EB8D71439D6F2CB04F095`
- Latest managed row measurement: `2026-11-02T15:45:54.0981049+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-02T15:45:54.0981049+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `75` managed faster, `1` equivalent, `11` native faster, `14` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `ea01c3cd01ae` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `ea01c3cd01ae` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.941 us | 2.257 us | 0.42x | 0.33–0.45x | -1.279 us | 9 pairs; 36/42 ms; 37,942/18,645 ops/lane; IQR 1.051/1.202 | `Pcre2Slice` | 513.764 us | 0.281 us | 0.887 us | 1.06x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.126 us | 12.836 us | 0.09x | 0.08–0.10x | -11.722 us | 9 pairs; 36/52 ms; 32,148/4,077 ops/lane; IQR 1.005/1.200 | `Pcre2Slice` | 1.704 us | 0.097 us | 4.006 us | 0.28x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.232 us | 0.01x | 0.01–0.01x | -1.221 us | 9 pairs; 40/39 ms; 3,570,958/31,879 ops/lane; IQR 1.010/1.009 | `Pcre2Slice` | 0.186 us | 1.145 us | 1.186 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.534 us | 0.160 us | 9.66x | 9.62–9.70x | +1.375 us | 9 pairs; 38/40 ms; 24,858/250,569 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0.047 us | 0.124 us | 0.163 us | 9.43x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 5.404 us | 0.577 us | 9.37x | 9.27–9.46x | +4.827 us | 9 pairs; 39/40 ms; 7,293/70,185 ops/lane; IQR 1.004/1.014 | `Pcre2Backtracking` | 0.128 us | 0.764 us | 0.804 us | 6.72x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.155 us | 0.302 us | 0.51x | 0.51–0.52x | -0.147 us | 9 pairs; 40/40 ms; 256,699/132,154 ops/lane; IQR 1.004/1.006 | `Pcre2Slice` | 0.168 us | 0.325 us | 0.355 us | 0.44x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.157 us | 0.356 us | 0.44x | 0.44–0.44x | -0.199 us | 9 pairs; 40/40 ms; 251,036/111,641 ops/lane; IQR 1.014/1.007 | `Pcre2Slice` | 0.174 us | 0.444 us | 0.467 us | 0.34x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.111 us | 0.292 us | 0.38x | 0.38–0.38x | -0.181 us | 9 pairs; 40/39 ms; 359,233/133,160 ops/lane; IQR 1.008/1.004 | `Pcre2Slice` | 0.122 us | 0.246 us | 0.267 us | 0.42x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.111 us | 0.314 us | 0.35x | 0.35–0.36x | -0.202 us | 9 pairs; 41/41 ms; 366,704/129,990 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | 0.122 us | 0.262 us | 0.285 us | 0.39x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.172 us | 0.078 us | 2.24x | 2.12–2.41x | +0.096 us | 9 pairs; 36/37 ms; 207,708/479,817 ops/lane; IQR 1.004/1.117 | `Pcre2Slice` | 0.173 us | 1.023 us | 1.055 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.200 us | 0.17x | 0.17–0.20x | -0.166 us | 9 pairs; 40/39 ms; 1,177,948/196,907 ops/lane; IQR 1.075/1.010 | `Pcre2Backtracking` | 0.185 us | 0.305 us | 0.346 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.617 us | 0.10x | 0.10–0.10x | -0.556 us | 9 pairs; 40/40 ms; 655,323/65,137 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0.176 us | 0.239 us | 0.264 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.155 us | 0.22x | 0.21–0.23x | -0.121 us | 9 pairs; 37/39 ms; 1,106,974/253,672 ops/lane; IQR 1.041/1.007 | `Pcre2Backtracking` | 0.103 us | 0.182 us | 0.203 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.114 us | 0.17x | 0.17–0.18x | -0.094 us | 9 pairs; 38/40 ms; 1,955,058/347,708 ops/lane; IQR 1.030/1.008 | `Pcre2Backtracking` | 0.064 us | 0.093 us | 0.111 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.123 us | 0.47x | 0.46–0.47x | -0.065 us | 9 pairs; 40/41 ms; 691,307/334,220 ops/lane; IQR 1.003/1.018 | `Pcre2Backtracking` | 0.016 us | 0.085 us | 0.102 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.181 us | 0.273 us | 0.67x | 0.66–0.68x | -0.091 us | 9 pairs; 40/39 ms; 221,644/142,145 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | 0.194 us | 0.119 us | 0.155 us | 1.16x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.023 us | 0.116 us | 0.20x | 0.19–0.20x | -0.093 us | 9 pairs; 41/40 ms; 1,771,782/344,957 ops/lane; IQR 1.025/1.011 | `Pcre2Backtracking` | 0.019 us | 0.096 us | 0.113 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 1.873 us | 0.06x | 0.06–0.07x | -1.755 us | 9 pairs; 40/35 ms; 339,786/18,534 ops/lane; IQR 1.001/1.138 | `Pcre2Literal` | 0.090 us | 0.050 us | 0.634 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 1.934 us | 0.06x | 0.05–0.07x | -1.813 us | 9 pairs; 40/35 ms; 329,013/18,277 ops/lane; IQR 1.005/1.233 | `Pcre2Literal` | 0.154 us | 0.054 us | 0.673 us | 0.18x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.172 us | 2.510 us | 0.07x | 0.06–0.07x | -2.337 us | 9 pairs; 47/45 ms; 274,323/17,822 ops/lane; IQR 1.109/1.097 | `Pcre2Literal` | 0.215 us | 0.237 us | 0.882 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.976 us | 0.08x | 0.07–0.08x | -1.822 us | 9 pairs; 40/40 ms; 263,546/20,123 ops/lane; IQR 1.010/1.219 | `Pcre2Literal` | 0.181 us | 0.206 us | 0.837 us | 0.18x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 40/40 ms; 4,533,403/394,567 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | 0.976 us | 0.069 us | 0.089 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.087 us | 0.78x | 0.75–0.79x | -0.019 us | 9 pairs; 40/38 ms; 592,114/438,502 ops/lane; IQR 1.002/1.034 | `Pcre2Literal` | 0.128 us | 0.051 us | 0.073 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.109 us | 0.13x | 0.12–0.13x | -0.095 us | 9 pairs; 41/40 ms; 2,908,510/364,943 ops/lane; IQR 1.015/1.007 | `Pcre2Backtracking` | 0.244 us | 0.094 us | 0.117 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.118 us | 0.121 us | 0.98x | 0.97–0.98x | -0.003 us | 9 pairs; 41/39 ms; 344,029/322,071 ops/lane; IQR 1.006/1.018 | `Pcre2Character` | 0.145 us | 0.112 us | 0.142 us | 0.83x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.075 us | 0.201 us | 0.37x | 0.37–0.38x | -0.126 us | 9 pairs; 40/39 ms; 531,033/192,381 ops/lane; IQR 1.005/1.020 | `Pcre2Slice` | 0.567 us | 0.075 us | 0.107 us | 0.70x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.635 us | 160.676 us | 0.51x | 0.51–0.52x | -78.041 us | 9 pairs; 40/40 ms; 486/252 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 411.142 us | 105.545 us | 108.317 us | 0.76x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.546 us | 284.632 us | 0.13x | 0.13–0.13x | -247.050 us | 9 pairs; 40/40 ms; 1,064/139 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 397.368 us | 217.886 us | 231.469 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.285 us | 211.133 us | 0.05x | 0.05–0.05x | -200.882 us | 9 pairs; 40/41 ms; 3,900/192 ops/lane; IQR 1.005/1.011 | `Pcre2Backtracking` | 8.011 us | 231.407 us | 229.705 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 239.598 us | 205.139 us | 1.17x | 1.16–1.18x | +34.460 us | 9 pairs; 40/39 ms; 166/192 ops/lane; IQR 1.001/1.014 | `Pcre2SingleTokenRepeat` | 8.071 us | 241.507 us | 239.985 us | 1.00x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.548 us | 24.189 us | 0.15x | 0.15–0.15x | -20.626 us | 9 pairs; 40/40 ms; 11,154/1,659 ops/lane; IQR 1.006/1.009 | `Pcre2Literal` | 1.379 us | 4.267 us | 6.101 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.766 us | 81.628 us | 0.12x | 0.12–0.12x | -71.863 us | 9 pairs; 40/40 ms; 4,094/492 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 9.329 us | 93.095 us | 93.933 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,241.567 us | 28,297.600 us | 0.15x | 0.14–0.16x | -24069.489 us | 9 pairs; 38/28 ms; 9/1 ops/lane; IQR 1.017/1.063 | `Pcre2Backtracking` | 5,899.067 us | 12,168.460 us | 19,099.867 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 5,953.886 us | 55,501.700 us | 0.11x | 0.10–0.11x | -49768.800 us | 9 pairs; 42/56 ms; 7/1 ops/lane; IQR 1.046/1.032 | `Pcre2Backtracking` | 6,357.488 us | 43,038.600 us | 48,314.450 us | 0.12x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 2,000.838 us | 480,768.700 us | 0.00x | 0.00–0.00x | -478832.733 us | 9 pairs; 42/481 ms; 21/1 ops/lane; IQR 1.035/1.003 | `Pcre2Character` | 1,779.452 us | 24,564.233 us | 30,314.300 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,563.400 us | 10,788.825 us | 0.52x | 0.50–0.56x | -5226.125 us | 9 pairs; 33/43 ms; 6/4 ops/lane; IQR 1.007/1.058 | `Pcre2Literal` | 1,404.086 us | 1,368.860 us | 8,435.450 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,868.750 us | 146,085.800 us | 0.13x | 0.13–0.13x | -127192.050 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.012/1.004 | `Pcre2Backtracking` | 1,592.291 us | 511.215 us | 8,608.267 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,793.188 us | 21,959.750 us | 0.22x | 0.22–0.22x | -17163.975 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 2,852.044 us | 11,020.260 us | 19,086.300 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,312.350 us | 137,693.400 us | 0.16x | 0.16–0.17x | -115521.750 us | 9 pairs; 45/138 ms; 2/1 ops/lane; IQR 1.011/1.005 | `Pcre2Backtracking` | 1,912.248 us | 2,353.786 us | 10,590.580 us | 2.11x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 237.502 us | 980.808 us | 0.24x | 0.24–0.25x | -742.242 us | 9 pairs; 40/39 ms; 167/40 ops/lane; IQR 1.002/1.015 | `Pcre2Backtracking` | 282.817 us | 341.318 us | 551.021 us | 0.43x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,562.567 us | 13,532.367 us | 0.93x | 0.91–0.93x | -991.133 us | 9 pairs; 38/41 ms; 3/3 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 309.945 us | 15,372.250 us | 15,617.200 us | 0.80x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,750.707 us | 99,160.600 us | 0.03x | 0.03–0.03x | -96415.443 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 2,017.600 us | 19,829.800 us | 20,152.267 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.908 us | 2,811.707 us | 0.07x | 0.07–0.07x | -2625.799 us | 9 pairs; 40/39 ms; 215/14 ops/lane; IQR 1.003/1.006 | `Pcre2SingleTokenRepeat` | 188.343 us | 1,017.354 us | 1,439.502 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,557.758 us | 19,798.100 us | 0.08x | 0.08–0.08x | -18239.569 us | 9 pairs; 41/40 ms; 26/2 ops/lane; IQR 1.005/1.010 | `Pcre2Backtracking` | 581.204 us | 10,984.340 us | 11,159.420 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.153 us | 1.123 us | 0.14x | 0.14–0.14x | -0.969 us | 9 pairs; 39/40 ms; 254,483/35,529 ops/lane; IQR 1.008/1.001 | `Pcre2Slice` | 1.421 us | 0.260 us | 0.283 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.531 us | 0.832 us | 0.64x | 0.64–0.64x | -0.301 us | 9 pairs; 41/40 ms; 76,665/47,791 ops/lane; IQR 1.004/1.001 | `Pcre2Literal` | 0.141 us | 0.150 us | 0.174 us | 3.05x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.298 us | 1.180 us | 0.25x | 0.25–0.25x | -0.881 us | 9 pairs; 40/40 ms; 134,471/33,636 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 1.075 us | 0.355 us | 0.383 us | 0.78x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.879 us | 1.126 us | 0.78x | 0.78–0.78x | -0.246 us | 9 pairs; 41/40 ms; 46,994/35,542 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 0.285 us | 0.430 us | 0.474 us | 1.85x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.154 us | 0.856 us | 0.18x | 0.18–0.18x | -0.702 us | 9 pairs; 40/40 ms; 260,815/47,131 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0.999 us | 0.155 us | 0.203 us | 0.76x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.866 us | 1.127 us | 0.77x | 0.77–0.77x | -0.261 us | 9 pairs; 40/40 ms; 46,560/35,488 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 6.665 us | 0.307 us | 0.326 us | 2.66x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.791 us | 0.836 us | 0.95x | 0.94–0.95x | -0.046 us | 9 pairs; 39/40 ms; 49,721/47,694 ops/lane; IQR 1.001/1.002 | `Pcre2Literal` | 3.606 us | 0.182 us | 0.204 us | 3.87x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.981 us | 1.195 us | 0.82x | 0.82–0.82x | -0.214 us | 9 pairs; 40/41 ms; 40,456/34,593 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 5.343 us | 0.410 us | 0.436 us | 2.25x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.226 us | 1.132 us | 1.08x | 1.08–1.09x | +0.094 us | 9 pairs; 39/40 ms; 31,533/35,607 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 3.685 us | 0.481 us | 0.515 us | 2.38x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.799 us | 0.858 us | 0.93x | 0.93–0.93x | -0.058 us | 9 pairs; 40/40 ms; 50,239/46,387 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | 2.250 us | 0.183 us | 0.223 us | 3.58x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.580 us | 1.127 us | 0.51x | 0.51–0.52x | -0.547 us | 9 pairs; 41/40 ms; 70,682/35,419 ops/lane; IQR 1.005/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.562 us | 0.836 us | 0.67x | 0.67–0.67x | -0.274 us | 9 pairs; 41/40 ms; 73,205/47,886 ops/lane; IQR 1.001/1.001 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.690 us | 1.193 us | 0.58x | 0.57–0.58x | -0.505 us | 9 pairs; 40/40 ms; 58,113/33,630 ops/lane; IQR 1.005/1.001 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.927 us | 1.133 us | 0.82x | 0.82–0.82x | -0.206 us | 9 pairs; 38/40 ms; 41,225/35,597 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.494 us | 0.857 us | 0.58x | 0.57–0.58x | -0.363 us | 9 pairs; 40/40 ms; 81,081/46,770 ops/lane; IQR 1.012/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 65.339 us | — | — | — | — | — | `—` | 82.269 us | 98.838 us | 100.338 us | 0.65x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 10.307 us | — | — | — | — | — | `—` | 2.912 us | 0.359 us | 0.386 us | 26.73x | 48,784 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.046 us | — | — | — | — | — | `—` | 0.863 us | 0.243 us | 0.271 us | 3.87x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.201 us | — | — | — | — | — | `—` | 3.021 us | 0.462 us | 0.495 us | 2.43x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.569 us | — | — | — | — | — | `—` | 0.962 us | 0.538 us | 0.588 us | 2.67x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.511 us | — | — | — | — | — | `—` | 1.507 us | 0.264 us | 0.325 us | 13.88x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Equivalent** | 5 B | 0.089 us | 0.089 us | 1.00x | 0.99–1.00x | 0.000 us | 9 pairs; 39/39 ms; 441,481/439,161 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0 B | 0 B | 2.269 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.117 us | 0.11x | 0.11–0.11x | -0.104 us | 9 pairs; 40/38 ms; 3,111,378/323,762 ops/lane; IQR 1.012/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 133.156 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.390 us | 0.103 us | 3.77x | 3.70–3.79x | +0.287 us | 9 pairs; 36/39 ms; 92,444/379,458 ops/lane; IQR 1.007/1.028 | `Pcre2Backtracking` | 0 B | 0 B | 20.675 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.407 us | 0.114 us | 3.57x | 3.45–3.60x | +0.294 us | 9 pairs; 37/37 ms; 91,140/326,858 ops/lane; IQR 1.009/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 70.275 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.392 us | 0.104 us | 3.80x | 3.79–3.80x | +0.289 us | 9 pairs; 36/38 ms; 91,711/370,545 ops/lane; IQR 1.023/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 69.494 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.571 us | 0.344 us | 1.66x | 1.65–1.67x | +0.227 us | 9 pairs; 37/39 ms; 65,442/112,656 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.635 us | 0.02x | 0.02–0.03x | -0.623 us | 9 pairs; 39/40 ms; 3,390,378/62,633 ops/lane; IQR 1.062/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 22.019 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.016 us | 0.516 us | 0.03x | 0.02–0.03x | -0.500 us | 9 pairs; 54/40 ms; 3,268,221/76,783 ops/lane; IQR 1.211/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 21.856 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.468 us | 0.110 us | 4.25x | 4.16–4.28x | +0.358 us | 9 pairs; 37/39 ms; 79,138/356,861 ops/lane; IQR 1.004/1.030 | `Pcre2Backtracking` | 0 B | 0 B | 18.456 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.819 us | 10.683 us | 0.83x | 0.82–0.83x | -1.851 us | 9 pairs; 39/39 ms; 4,453/3,691 ops/lane; IQR 1.004/1.008 | `Pcre2Literal` | 0 B | 0 B | 0.806 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.292 us | 1.051 us | 0.28x | 0.28–0.28x | -0.758 us | 9 pairs; 39/40 ms; 133,290/38,466 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.044 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.312 us | 0.476 us | 0.66x | 0.65–0.66x | -0.164 us | 9 pairs; 37/40 ms; 118,995/83,318 ops/lane; IQR 1.002/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 115.044 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.325 us | 0.492 us | 0.67x | 0.66–0.67x | -0.164 us | 9 pairs; 37/37 ms; 113,936/75,715 ops/lane; IQR 1.015/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 140.956 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.292 us | 0.599 us | 0.49x | 0.49–0.49x | -0.307 us | 9 pairs; 33/40 ms; 111,869/67,468 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 102.650 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.294 us | 0.396 us | 0.74x | 0.73–0.75x | -0.102 us | 9 pairs; 34/38 ms; 117,012/96,263 ops/lane; IQR 1.003/1.027 | `Pcre2Backtracking` | 0 B | 0 B | 211.363 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.946 us | 8.838 us | 0.56x | 0.56–0.56x | -3.888 us | 9 pairs; 40/40 ms; 8,148/4,563 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 0 B | 0 B | 1.475 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.263 us | 0.314 us | 0.84x | 0.83–0.85x | -0.051 us | 9 pairs; 37/38 ms; 139,211/122,598 ops/lane; IQR 1.003/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 91.975 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.270 us | 0.404 us | 0.67x | 0.67–0.67x | -0.133 us | 9 pairs; 37/40 ms; 136,859/98,875 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 123.438 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.716 us | — | — | — | — | — | `—` | 0 B | — | 14.325 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.188 us | 0.295 us | 0.64x | 0.64–0.66x | -0.105 us | 9 pairs; 40/40 ms; 214,156/136,788 ops/lane; IQR 1.046/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 54.013 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.599 us | — | — | — | — | — | `—` | 0 B | — | 14.831 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.604 us | 0.676 us | 0.89x | 0.89–0.90x | -0.074 us | 9 pairs; 32/40 ms; 52,686/59,463 ops/lane; IQR 1.009/1.009 | `Pcre2Slice` | 0 B | 0 B | 63.069 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.912 us | 29.188 us | 0.34x | 0.34–0.34x | -19.297 us | 9 pairs; 40/40 ms; 3,989/1,366 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 1.212 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.973 us | 1.079 us | 0.90x | 0.90–0.90x | -0.106 us | 9 pairs; 41/40 ms; 41,861/37,243 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 130.469 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.003 us | 1.194 us | 0.84x | 0.83–0.84x | -0.192 us | 9 pairs; 40/40 ms; 40,023/33,088 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 117.213 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.937 us | 1.058 us | 0.89x | 0.88–0.89x | -0.119 us | 9 pairs; 40/39 ms; 42,198/37,070 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 141.988 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.969 us | 1.171 us | 0.83x | 0.82–0.83x | -0.203 us | 9 pairs; 41/40 ms; 41,904/34,175 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 102.100 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.969 us | 0.946 us | 1.02x | 1.02–1.03x | +0.022 us | 9 pairs; 41/40 ms; 42,416/41,992 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 215.356 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.525 us | 8.875 us | 0.62x | 0.62–0.62x | -3.355 us | 9 pairs; 40/39 ms; 7,284/4,439 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.488 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.929 us | 0.897 us | 1.04x | 1.03–1.04x | +0.033 us | 9 pairs; 40/39 ms; 43,534/43,601 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 91.644 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.951 us | 0.972 us | 0.98x | 0.98–0.98x | -0.019 us | 9 pairs; 41/40 ms; 43,289/40,990 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 120.931 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.060 us | — | — | — | — | — | `—` | 0 B | — | 13.281 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.833 us | 0.866 us | 0.96x | 0.96–0.97x | -0.032 us | 9 pairs; 40/40 ms; 48,192/45,873 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.231 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.954 us | — | — | — | — | — | `—` | 0 B | — | 14.912 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.861 us | 1.268 us | 0.68x | 0.68–0.68x | -0.407 us | 9 pairs; 40/40 ms; 45,991/31,761 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 0 B | 0 B | 65.081 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.376 us | 2.297 us | 0.60x | 0.60–0.60x | -0.920 us | 9 pairs; 39/40 ms; 28,701/17,546 ops/lane; IQR 1.004/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.677 us | 1.079 us | 0.63x | 0.63–0.63x | -0.402 us | 9 pairs; 40/41 ms; 59,348/37,762 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 128.162 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.689 us | 1.197 us | 0.58x | 0.57–0.58x | -0.509 us | 9 pairs; 40/40 ms; 58,094/33,088 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 113.050 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.640 us | 1.055 us | 0.61x | 0.60–0.61x | -0.415 us | 9 pairs; 40/40 ms; 61,936/37,542 ops/lane; IQR 1.005/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 140.338 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.703 us | 1.168 us | 0.60x | 0.60–0.60x | -0.465 us | 9 pairs; 42/41 ms; 59,073/35,465 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 102.931 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.673 us | 0.946 us | 0.71x | 0.71–0.71x | -0.273 us | 9 pairs; 41/40 ms; 60,173/42,110 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 213.044 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.848 us | 2.192 us | 0.84x | 0.84–0.84x | -0.347 us | 9 pairs; 40/40 ms; 21,808/18,081 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.616 us | 0.894 us | 0.69x | 0.69–0.69x | -0.277 us | 9 pairs; 40/39 ms; 64,223/43,700 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 91.231 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.609 us | 0.970 us | 0.63x | 0.63–0.63x | -0.361 us | 9 pairs; 38/40 ms; 63,183/40,946 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 122.481 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.708 us | — | — | — | — | — | `—` | 0 B | — | 15.588 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.505 us | 0.863 us | 0.58x | 0.58–0.59x | -0.359 us | 9 pairs; 40/40 ms; 78,543/46,135 ops/lane; IQR 1.008/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 54.850 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.591 us | — | — | — | — | — | `—` | 0 B | — | 13.963 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.586 us | 1.277 us | 0.46x | 0.46–0.46x | -0.691 us | 9 pairs; 39/40 ms; 66,448/31,505 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.219 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.255 us | — | — | — | — | — | `—` | 480 B | — | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.119 us | — | — | — | — | — | `—` | 48 B | — | 130.531 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.218 us | — | — | — | — | — | `—` | 48 B | — | 115.144 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.107 us | — | — | — | — | — | `—` | 48 B | — | 143.950 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.112 us | — | — | — | — | — | `—` | 48 B | — | 100.062 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.102 us | — | — | — | — | — | `—` | 48 B | — | 217.331 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.013 us | — | — | — | — | — | `—` | 64 B | — | 1.387 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.129 us | — | — | — | — | — | `—` | 40 B | — | 92.719 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.127 us | — | — | — | — | — | `—` | 40 B | — | 124.169 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.988 us | — | — | — | — | — | `—` | 1,216 B | — | 14.162 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.056 us | — | — | — | — | — | `—` | 48 B | — | 53.894 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.803 us | — | — | — | — | — | `—` | 928 B | — | 14.369 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 6.291 us | — | — | — | — | — | `—` | 28,928 B | — | 64.606 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.339–225.861 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.543–95.546 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.908–12.958 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.314–49.388 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.033–1.451 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.454–42.197 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.780–22.732 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.282–100.006 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.145–72.415 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.241–5.590 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.930–382.961 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.146 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.507–307.504 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.850–5.089 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.539–140.844 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 174.280–1,387.800 us |

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
