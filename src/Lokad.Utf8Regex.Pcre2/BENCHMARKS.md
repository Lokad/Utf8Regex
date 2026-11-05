<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `23BADF3A90287ACE015074F84B654503B7CF72C04B9BD6C1A1914FB1734DFB12`
- Latest managed row measurement: `2026-11-05T01:33:15.5582456+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T01:33:15.5582456+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `77` managed faster, `1` equivalent, `12` native faster, `11` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `fb30938025f0` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `fb30938025f0` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Managed faster** | 4,103 B | 0.890 us | 2.574 us | 0.35x | 0.33–0.46x | -1.685 us | 9 pairs; 33/54 ms; 37,376/21,160 ops/lane; IQR 1.007/1.096 | `Pcre2Slice` | 563.612 us | 0.284 us | 0.818 us | 1.09x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.121 us | 11.526 us | 0.10x | 0.08–0.11x | -10.410 us | 9 pairs; 36/41 ms; 31,814/3,516 ops/lane; IQR 1.008/1.199 | `Pcre2Slice` | 0.845 us | 0.097 us | 3.388 us | 0.33x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.232 us | 0.01x | 0.01–0.01x | -1.221 us | 9 pairs; 39/41 ms; 3,614,118/33,592 ops/lane; IQR 1.010/1.012 | `Pcre2Slice` | 0.104 us | 1.090 us | 1.110 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.705 us | 0.161 us | 4.39x | 4.36–4.42x | +0.544 us | 9 pairs; 38/40 ms; 53,356/248,846 ops/lane; IQR 1.003/1.010 | `Pcre2Backtracking` | 0.049 us | 0.121 us | 0.156 us | 4.53x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.338 us | 0.573 us | 4.09x | 4.07–4.13x | +1.765 us | 9 pairs; 39/39 ms; 16,622/68,627 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0.127 us | 0.726 us | 0.756 us | 3.09x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.101 us | 0.318 us | 0.32x | 0.32–0.32x | -0.217 us | 9 pairs; 41/41 ms; 405,634/129,497 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 0.111 us | 0.315 us | 0.344 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.350 us | 0.30x | 0.30–0.30x | -0.244 us | 9 pairs; 41/38 ms; 383,997/109,714 ops/lane; IQR 1.003/1.014 | `Pcre2Slice` | 0.114 us | 0.442 us | 0.467 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.290 us | 0.28x | 0.27–0.28x | -0.211 us | 9 pairs; 42/39 ms; 532,402/134,471 ops/lane; IQR 1.007/1.008 | `Pcre2Slice` | 0.089 us | 0.230 us | 0.253 us | 0.32x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.088 us | 0.314 us | 0.28x | 0.26–0.28x | -0.227 us | 9 pairs; 43/39 ms; 493,653/124,777 ops/lane; IQR 1.074/1.007 | `Pcre2Slice` | 0.089 us | 0.244 us | 0.268 us | 0.33x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.171 us | 0.078 us | 2.18x | 2.14–2.23x | +0.092 us | 9 pairs; 39/40 ms; 227,533/517,110 ops/lane; IQR 1.003/1.019 | `Pcre2Slice` | 0.089 us | 0.929 us | 0.944 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.202 us | 0.16x | 0.16–0.17x | -0.169 us | 9 pairs; 41/43 ms; 1,246,372/215,057 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0.108 us | 0.297 us | 0.337 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.605 us | 0.10x | 0.10–0.10x | -0.545 us | 9 pairs; 40/40 ms; 662,752/65,596 ops/lane; IQR 1.002/1.009 | `Pcre2Backtracking` | 0.134 us | 0.224 us | 0.243 us | 0.25x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.22x | 0.21–0.22x | -0.121 us | 9 pairs; 38/40 ms; 1,144,134/260,408 ops/lane; IQR 1.023/1.007 | `Pcre2Backtracking` | 0.110 us | 0.185 us | 0.207 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.111 us | 0.19x | 0.19–0.19x | -0.090 us | 9 pairs; 41/40 ms; 1,950,331/358,445 ops/lane; IQR 1.036/1.017 | `Pcre2Backtracking` | 0.068 us | 0.091 us | 0.111 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.120 us | 0.47x | 0.47–0.49x | -0.063 us | 9 pairs; 40/40 ms; 693,193/329,760 ops/lane; IQR 1.010/1.023 | `Pcre2Backtracking` | 0.016 us | 0.085 us | 0.102 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.084 us | 0.281 us | 0.30x | 0.30–0.31x | -0.196 us | 9 pairs; 40/40 ms; 477,362/143,124 ops/lane; IQR 1.033/1.009 | `Pcre2Slice` | 0.095 us | 0.117 us | 0.154 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.023 us | 0.114 us | 0.20x | 0.20–0.20x | -0.091 us | 9 pairs; 40/42 ms; 1,735,651/367,794 ops/lane; IQR 1.013/1.008 | `Pcre2Backtracking` | 0.019 us | 0.090 us | 0.108 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.114 us | 2.080 us | 0.05x | 0.05–0.06x | -1.966 us | 9 pairs; 38/42 ms; 337,422/20,249 ops/lane; IQR 1.005/1.182 | `Pcre2Literal` | 0.088 us | 0.051 us | 0.625 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.121 us | 2.150 us | 0.06x | 0.05–0.07x | -2.028 us | 9 pairs; 38/46 ms; 313,979/21,601 ops/lane; IQR 1.005/1.164 | `Pcre2Literal` | 0.148 us | 0.054 us | 0.629 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.157 us | 2.247 us | 0.07x | 0.07–0.08x | -2.090 us | 9 pairs; 40/47 ms; 256,580/21,055 ops/lane; IQR 1.002/1.109 | `Pcre2Literal` | 0.201 us | 0.233 us | 0.822 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.937 us | 0.08x | 0.08–0.08x | -1.785 us | 9 pairs; 40/45 ms; 263,637/23,188 ops/lane; IQR 1.009/1.045 | `Pcre2Literal` | 0.173 us | 0.204 us | 0.808 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.098 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 40/39 ms; 4,524,795/403,957 ops/lane; IQR 1.004/1.019 | `Pcre2Slice` | 0.281 us | 0.068 us | 0.089 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.085 us | 0.80x | 0.79–0.82x | -0.017 us | 9 pairs; 41/40 ms; 601,639/465,793 ops/lane; IQR 1.013/1.016 | `Pcre2Literal` | 0.133 us | 0.051 us | 0.073 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.12–0.13x | -0.095 us | 9 pairs; 39/40 ms; 2,867,992/365,508 ops/lane; IQR 1.014/1.007 | `Pcre2Backtracking` | 0.246 us | 0.091 us | 0.113 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.118 us | 0.120 us | 0.99x | 0.98–1.63x | -0.002 us | 9 pairs; 41/42 ms; 345,890/347,245 ops/lane; IQR 1.343/1.012 | `Pcre2Character` | 0.137 us | 0.113 us | 0.143 us | 0.83x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.201 us | 0.38x | 0.37–0.38x | -0.126 us | 9 pairs; 40/40 ms; 525,714/197,981 ops/lane; IQR 1.004/1.010 | `Pcre2Slice` | 0.467 us | 0.075 us | 0.109 us | 0.70x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.391 us | 162.020 us | 0.51x | 0.51–0.51x | -79.785 us | 9 pairs; 40/40 ms; 485/248 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 372.409 us | 96.337 us | 98.781 us | 0.83x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.375 us | 281.845 us | 0.13x | 0.13–0.13x | -244.420 us | 9 pairs; 40/39 ms; 1,067/138 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 393.749 us | 217.372 us | 227.121 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.419 us | 207.914 us | 0.05x | 0.05–0.05x | -198.472 us | 9 pairs; 40/41 ms; 4,229/195 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 8.195 us | 216.874 us | 218.433 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 238.929 us | 206.272 us | 1.16x | 1.15–1.17x | +33.132 us | 9 pairs; 40/39 ms; 166/188 ops/lane; IQR 1.003/1.012 | `Pcre2SingleTokenRepeat` | 8.200 us | 231.227 us | 232.326 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.559 us | 22.658 us | 0.16x | 0.15–0.16x | -19.075 us | 9 pairs; 40/37 ms; 11,154/1,650 ops/lane; IQR 1.010/1.013 | `Pcre2Literal` | 1.383 us | 4.281 us | 5.978 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.595 us | 81.537 us | 0.12x | 0.12–0.12x | -71.955 us | 9 pairs; 40/40 ms; 4,143/493 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 9.354 us | 90.104 us | 91.129 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,149.340 us | 28,452.100 us | 0.15x | 0.14–0.15x | -24273.110 us | 9 pairs; 41/28 ms; 10/1 ops/lane; IQR 1.005/1.031 | `Pcre2Backtracking` | 14,019.333 us | 11,440.020 us | 18,394.833 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,461.858 us | 55,254.700 us | 0.06x | 0.06–0.06x | -51771.017 us | 9 pairs; 42/55 ms; 12/1 ops/lane; IQR 1.008/1.031 | `Pcre2Backtracking` | 3,831.415 us | 40,595.500 us | 47,419.200 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,947.790 us | 485,171.800 us | 0.00x | 0.00–0.00x | -483280.552 us | 9 pairs; 41/485 ms; 21/1 ops/lane; IQR 1.037/1.004 | `Pcre2Character` | 1,840.807 us | 22,813.867 us | 30,134.050 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,522.483 us | 11,381.667 us | 0.49x | 0.44–0.52x | -5852.517 us | 9 pairs; 33/34 ms; 6/3 ops/lane; IQR 1.010/1.129 | `Pcre2Literal` | 1,394.524 us | 1,398.844 us | 8,650.217 us | 0.64x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,668.100 us | 177,422.700 us | 0.11x | 0.10–0.11x | -158891.850 us | 9 pairs; 37/177 ms; 2/1 ops/lane; IQR 1.018/1.005 | `Pcre2Backtracking` | 1,283.660 us | 510.136 us | 8,584.400 us | 2.17x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,772.238 us | 21,838.550 us | 0.22x | 0.22–0.22x | -17066.313 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.004/1.015 | `Pcre2Slice` | 2,489.081 us | 10,812.440 us | 18,881.600 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,310.450 us | 133,742.100 us | 0.17x | 0.16–0.17x | -111597.850 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 1,541.576 us | 2,288.165 us | 10,216.000 us | 2.18x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 173.606 us | 973.232 us | 0.18x | 0.18–0.18x | -801.622 us | 9 pairs; 39/37 ms; 226/38 ops/lane; IQR 1.006/1.011 | `Pcre2Backtracking` | 168.360 us | 328.471 us | 535.919 us | 0.32x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,542.800 us | 13,400.133 us | 0.94x | 0.93–0.94x | -846.567 us | 9 pairs; 38/40 ms; 3/3 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 310.419 us | 14,956.325 us | 15,210.575 us | 0.82x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,945.085 us | 98,862.200 us | 0.03x | 0.03–0.03x | -95922.838 us | 9 pairs; 38/99 ms; 13/1 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 1,883.311 us | 19,712.400 us | 19,998.800 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 183.823 us | 2,785.243 us | 0.07x | 0.07–0.07x | -2600.338 us | 9 pairs; 39/39 ms; 212/14 ops/lane; IQR 1.009/1.006 | `Pcre2SingleTokenRepeat` | 186.514 us | 1,006.331 us | 1,422.398 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,550.832 us | 20,177.650 us | 0.08x | 0.08–0.08x | -18610.854 us | 9 pairs; 39/40 ms; 25/2 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 578.332 us | 10,604.260 us | 10,728.600 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.144 us | 1.097 us | 0.13x | 0.13–0.13x | -0.954 us | 9 pairs; 37/41 ms; 258,197/37,741 ops/lane; IQR 1.024/1.002 | `Pcre2Slice` | 0.729 us | 0.258 us | 0.279 us | 0.52x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.530 us | 0.830 us | 0.64x | 0.63–0.64x | -0.299 us | 9 pairs; 41/40 ms; 77,576/48,292 ops/lane; IQR 1.010/1.004 | `Pcre2Literal` | 0.154 us | 0.149 us | 0.173 us | 3.07x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.290 us | 1.177 us | 0.25x | 0.25–0.25x | -0.886 us | 9 pairs; 39/40 ms; 133,688/33,797 ops/lane; IQR 1.013/1.012 | `Pcre2Backtracking` | 1.061 us | 0.346 us | 0.372 us | 0.78x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.870 us | 1.121 us | 0.78x | 0.77–0.78x | -0.252 us | 9 pairs; 41/40 ms; 47,041/35,537 ops/lane; IQR 1.009/1.009 | `Pcre2Character` | 0.283 us | 0.428 us | 0.466 us | 1.87x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.153 us | 0.859 us | 0.18x | 0.18–0.18x | -0.706 us | 9 pairs; 40/43 ms; 260,159/49,590 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | 0.882 us | 0.155 us | 0.199 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.951 us | 1.106 us | 0.86x | 0.86–0.86x | -0.157 us | 9 pairs; 40/40 ms; 41,930/36,122 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 3.821 us | 0.306 us | 0.324 us | 2.93x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.798 us | 0.831 us | 0.96x | 0.96–0.96x | -0.032 us | 9 pairs; 39/41 ms; 49,207/48,775 ops/lane; IQR 1.001/1.005 | `Pcre2Literal` | 3.576 us | 0.179 us | 0.202 us | 3.95x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.998 us | 1.182 us | 0.85x | 0.84–0.85x | -0.181 us | 9 pairs; 40/39 ms; 40,500/33,220 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 5.273 us | 0.405 us | 0.427 us | 2.34x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.185 us | 1.135 us | 1.04x | 1.04–1.05x | +0.049 us | 9 pairs; 39/40 ms; 32,717/35,300 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 3.651 us | 0.476 us | 0.513 us | 2.31x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.792 us | 0.857 us | 0.92x | 0.92–0.93x | -0.065 us | 9 pairs; 40/40 ms; 50,429/46,944 ops/lane; IQR 1.006/1.009 | `Pcre2Slice` | 2.167 us | 0.183 us | 0.224 us | 3.54x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.574 us | 1.103 us | 0.52x | 0.52–0.52x | -0.528 us | 9 pairs; 39/40 ms; 67,460/35,968 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.503 us | 0.829 us | 0.61x | 0.60–0.61x | -0.325 us | 9 pairs; 39/40 ms; 77,047/48,232 ops/lane; IQR 1.007/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.679 us | 1.182 us | 0.58x | 0.57–0.58x | -0.501 us | 9 pairs; 35/39 ms; 51,544/32,644 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.907 us | 1.136 us | 0.80x | 0.80–0.80x | -0.226 us | 9 pairs; 39/40 ms; 42,775/34,800 ops/lane; IQR 1.015/1.009 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.477 us | 0.860 us | 0.55x | 0.55–0.56x | -0.384 us | 9 pairs; 40/39 ms; 84,335/45,871 ops/lane; IQR 1.004/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.415 us | — | — | — | — | — | `—` | 25.415 us | 95.095 us | 97.119 us | 0.64x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.450 us | — | — | — | — | — | `—` | 1.802 us | 0.360 us | 0.389 us | 16.58x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.065 us | — | — | — | — | — | `—` | 0.896 us | 0.239 us | 0.275 us | 3.88x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.211 us | — | — | — | — | — | `—` | 3.067 us | 0.457 us | 0.489 us | 2.48x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.490 us | — | — | — | — | — | `—` | 1.018 us | 0.537 us | 0.588 us | 2.53x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.402 us | — | — | — | — | — | `—` | 1.579 us | 0.266 us | 0.318 us | 13.84x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.087 us | 0.083 us | 1.05x | 1.04–1.06x | +0.004 us | 9 pairs; 40/41 ms; 458,067/489,487 ops/lane; IQR 1.006/1.023 | `Pcre2Character` | 0 B | 0 B | 3.431 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.119 us | 0.11x | 0.11–0.11x | -0.106 us | 9 pairs; 39/40 ms; 3,094,605/334,880 ops/lane; IQR 1.004/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 131.369 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.386 us | 0.106 us | 3.63x | 3.60–3.66x | +0.280 us | 9 pairs; 36/40 ms; 94,005/374,600 ops/lane; IQR 1.005/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 22.087 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.395 us | 0.117 us | 3.39x | 3.37–3.41x | +0.279 us | 9 pairs; 36/38 ms; 90,760/325,406 ops/lane; IQR 1.012/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 71.119 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.381 us | 0.105 us | 3.64x | 3.61–3.65x | +0.276 us | 9 pairs; 36/41 ms; 94,487/387,940 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 69.981 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.565 us | 0.347 us | 1.63x | 1.63–1.63x | +0.219 us | 9 pairs; 37/40 ms; 65,842/114,015 ops/lane; IQR 1.005/1.005 | `Pcre2Character` | 0 B | 0 B | 2.044 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.632 us | 0.02x | 0.02–0.02x | -0.620 us | 9 pairs; 39/39 ms; 3,422,394/62,518 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 21.762 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.508 us | 0.02x | 0.02–0.02x | -0.496 us | 9 pairs; 42/39 ms; 3,417,338/77,217 ops/lane; IQR 1.034/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 21.294 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.468 us | 0.113 us | 4.14x | 4.12–4.29x | +0.355 us | 9 pairs; 36/41 ms; 76,479/360,892 ops/lane; IQR 1.005/1.038 | `Pcre2Backtracking` | 0 B | 0 B | 20.481 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.486 us | 10.501 us | 0.81x | 0.80–0.81x | -2.027 us | 9 pairs; 40/40 ms; 4,686/3,797 ops/lane; IQR 1.008/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.293 us | 1.043 us | 0.28x | 0.28–0.28x | -0.750 us | 9 pairs; 39/40 ms; 134,440/37,961 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 129.025 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.321 us | 0.457 us | 0.70x | 0.69–0.70x | -0.138 us | 9 pairs; 38/41 ms; 117,330/88,752 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 112.456 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.296 us | 0.462 us | 0.64x | 0.62–0.64x | -0.168 us | 9 pairs; 38/41 ms; 129,305/88,257 ops/lane; IQR 1.007/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 140.519 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.332 us | 0.590 us | 0.56x | 0.56–0.57x | -0.257 us | 9 pairs; 42/40 ms; 125,496/68,355 ops/lane; IQR 1.013/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 103.644 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.293 us | 0.402 us | 0.73x | 0.72–0.73x | -0.109 us | 9 pairs; 37/40 ms; 127,132/100,407 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 214.400 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.962 us | 8.828 us | 0.56x | 0.56–0.56x | -3.866 us | 9 pairs; 40/40 ms; 8,071/4,481 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.257 us | 0.312 us | 0.83x | 0.82–0.84x | -0.052 us | 9 pairs; 36/39 ms; 138,659/124,792 ops/lane; IQR 1.025/1.027 | `Pcre2Backtracking` | 0 B | 0 B | 91.275 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.299 us | 0.400 us | 0.75x | 0.74–0.75x | -0.100 us | 9 pairs; 41/39 ms; 137,036/97,967 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 122.037 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.830 us | — | — | — | — | — | `—` | 0 B | — | 13.637 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.195 us | 0.292 us | 0.67x | 0.64–0.68x | -0.095 us | 9 pairs; 42/40 ms; 214,557/138,525 ops/lane; IQR 1.037/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 53.150 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.733 us | — | — | — | — | — | `—` | 0 B | — | 14.162 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.574 us | 0.673 us | 0.85x | 0.84–0.86x | -0.099 us | 9 pairs; 37/40 ms; 64,910/58,989 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.031 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.795 us | 28.806 us | 0.34x | 0.34–0.34x | -18.959 us | 9 pairs; 40/40 ms; 4,076/1,380 ops/lane; IQR 1.004/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.719 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.944 us | 1.072 us | 0.88x | 0.88–0.89x | -0.125 us | 9 pairs; 39/40 ms; 41,568/37,262 ops/lane; IQR 1.007/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 129.887 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.011 us | 1.186 us | 0.85x | 0.85–0.86x | -0.177 us | 9 pairs; 41/40 ms; 40,157/33,485 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 112.900 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.955 us | 1.040 us | 0.92x | 0.92–0.92x | -0.085 us | 9 pairs; 40/39 ms; 41,950/37,882 ops/lane; IQR 1.006/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 142.287 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.953 us | 1.182 us | 0.81x | 0.81–0.81x | -0.225 us | 9 pairs; 39/41 ms; 41,127/34,467 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 104.825 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.942 us | 0.949 us | 0.99x | 0.99–0.99x | -0.006 us | 9 pairs; 42/41 ms; 44,533/43,450 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 215.738 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.617 us | 8.840 us | 0.64x | 0.64–0.64x | -3.218 us | 9 pairs; 40/40 ms; 7,125/4,493 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.394 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.919 us | 0.896 us | 1.03x | 1.02–1.03x | +0.025 us | 9 pairs; 40/40 ms; 43,280/44,211 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 91.481 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.927 us | 0.967 us | 0.96x | 0.96–0.96x | -0.042 us | 9 pairs; 40/41 ms; 42,827/42,070 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 120.056 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.125 us | — | — | — | — | — | `—` | 0 B | — | 14.219 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.827 us | 0.865 us | 0.96x | 0.96–0.96x | -0.036 us | 9 pairs; 40/40 ms; 48,317/45,961 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 53.369 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.003 us | — | — | — | — | — | `—` | 0 B | — | 13.750 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.859 us | 1.257 us | 0.68x | 0.68–0.68x | -0.399 us | 9 pairs; 39/40 ms; 45,049/31,751 ops/lane; IQR 1.006/1.002 | `Pcre2Slice` | 0 B | 0 B | 64.906 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.360 us | 2.270 us | 0.60x | 0.60–0.60x | -0.910 us | 9 pairs; 40/39 ms; 29,374/17,179 ops/lane; IQR 1.004/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.713 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.641 us | 1.068 us | 0.60x | 0.60–0.60x | -0.429 us | 9 pairs; 39/40 ms; 60,732/37,209 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 129.819 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.681 us | 1.182 us | 0.58x | 0.57–0.58x | -0.503 us | 9 pairs; 35/41 ms; 51,098/34,357 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 112.925 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.632 us | 1.034 us | 0.61x | 0.61–0.61x | -0.403 us | 9 pairs; 40/39 ms; 63,640/37,866 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 142.250 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.635 us | 1.187 us | 0.53x | 0.53–0.54x | -0.553 us | 9 pairs; 40/40 ms; 62,853/33,544 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 104.287 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.684 us | 0.948 us | 0.72x | 0.72–0.73x | -0.264 us | 9 pairs; 40/40 ms; 58,958/42,681 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 216.144 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.821 us | 2.190 us | 0.83x | 0.83–0.83x | -0.371 us | 9 pairs; 40/40 ms; 22,013/18,144 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.630 us | 0.890 us | 0.71x | 0.70–0.71x | -0.260 us | 9 pairs; 40/40 ms; 63,621/45,120 ops/lane; IQR 1.006/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 91.912 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.608 us | 0.969 us | 0.63x | 0.62–0.63x | -0.364 us | 9 pairs; 40/40 ms; 65,418/41,331 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 120.438 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.767 us | — | — | — | — | — | `—` | 0 B | — | 13.269 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.488 us | 0.862 us | 0.57x | 0.57–0.57x | -0.372 us | 9 pairs; 39/40 ms; 79,465/46,055 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 54.656 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.692 us | — | — | — | — | — | `—` | 0 B | — | 14.588 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.579 us | 1.256 us | 0.46x | 0.46–0.46x | -0.674 us | 9 pairs; 39/40 ms; 68,040/31,905 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | 0 B | 0 B | 64.062 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.245 us | — | — | — | — | — | `—` | 480 B | — | 0.700 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.140 us | — | — | — | — | — | `—` | 48 B | — | 131.456 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.253 us | — | — | — | — | — | `—` | 48 B | — | 114.925 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.120 us | — | — | — | — | — | `—` | 48 B | — | 143.381 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.153 us | — | — | — | — | — | `—` | 48 B | — | 100.069 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.120 us | — | — | — | — | — | `—` | 48 B | — | 220.500 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.839 us | — | — | — | — | — | `—` | 64 B | — | 1.350 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.132 us | — | — | — | — | — | `—` | 40 B | — | 93.269 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.150 us | — | — | — | — | — | `—` | 40 B | — | 120.100 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.929 us | — | — | — | — | — | `—` | 1,216 B | — | 12.919 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.069 us | — | — | — | — | — | `—` | 48 B | — | 53.525 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.945 us | — | — | — | — | — | `—` | 928 B | — | 13.694 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.485 us | — | — | — | — | — | `—` | 23,312 B | — | 63.663 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 30.506–227.413 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.383–95.130 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.887–12.596 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.678–45.853 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.034–1.261 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.850–44.422 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.662–22.175 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.146–99.572 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.118–72.521 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.175–5.441 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.909–364.785 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.077 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.205–308.398 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.819–5.059 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.183–15.650 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 165.078–1,308.431 us |

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
