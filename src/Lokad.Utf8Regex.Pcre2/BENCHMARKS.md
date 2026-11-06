<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `ED642AADE1934F9BC79F5E1D2C66861D6CD343663728EA3A9837208F10EB9F17`
- Latest managed row measurement: `2026-11-06T01:01:02.3449674+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T01:01:02.3449674+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `44/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `80` managed faster, `1` equivalent, `12` native faster, `8` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `18bc29c908ca` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `18bc29c908ca` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Managed faster** | 4,103 B | 0.697 us | 2.176 us | 0.32x | 0.30–0.34x | -1.478 us | 9 pairs; 37/42 ms; 53,220/19,166 ops/lane; IQR 1.010/1.064 | `Pcre2Slice` | 471.305 us | 0.278 us | 0.830 us | 0.84x | 1,248 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.901 us | 13.408 us | 0.07x | 0.06–0.08x | -12.519 us | 9 pairs; 37/47 ms; 40,958/3,482 ops/lane; IQR 1.021/1.114 | `Pcre2Slice` | 0.633 us | 0.095 us | 3.396 us | 0.27x | 720 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.230 us | 0.01x | 0.01–0.01x | -1.219 us | 9 pairs; 39/40 ms; 3,623,443/32,230 ops/lane; IQR 1.004/1.001 | `Pcre2Slice` | 0.102 us | 1.232 us | 1.266 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.686 us | 0.157 us | 4.35x | 4.32–4.38x | +0.529 us | 9 pairs; 38/41 ms; 54,803/258,073 ops/lane; IQR 1.006/1.011 | `Pcre2Backtracking` | 0.048 us | 0.119 us | 0.156 us | 4.40x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.404 us | 0.573 us | 4.21x | 4.18–4.24x | +1.836 us | 9 pairs; 39/40 ms; 16,211/69,497 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0.117 us | 0.746 us | 0.779 us | 3.08x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.102 us | 0.293 us | 0.35x | 0.35–0.35x | -0.191 us | 9 pairs; 41/40 ms; 402,838/136,159 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | 0.113 us | 0.314 us | 0.343 us | 0.30x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.358 us | 0.30x | 0.29–0.32x | -0.249 us | 9 pairs; 40/40 ms; 377,678/110,703 ops/lane; IQR 1.075/1.017 | `Pcre2Slice` | 0.115 us | 0.443 us | 0.464 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.078 us | 0.287 us | 0.27x | 0.27–0.27x | -0.210 us | 9 pairs; 40/39 ms; 510,570/135,708 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0.087 us | 0.232 us | 0.260 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.080 us | 0.314 us | 0.25x | 0.25–0.26x | -0.234 us | 9 pairs; 41/39 ms; 514,151/122,727 ops/lane; IQR 1.012/1.019 | `Pcre2Slice` | 0.088 us | 0.253 us | 0.274 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.174 us | 0.075 us | 2.32x | 2.29–2.38x | +0.099 us | 9 pairs; 40/38 ms; 231,132/502,252 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 0.087 us | 1.002 us | 1.027 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.198 us | 0.17x | 0.17–0.17x | -0.165 us | 9 pairs; 40/41 ms; 1,187,554/204,323 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0.110 us | 0.298 us | 0.336 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.607 us | 0.10x | 0.10–0.10x | -0.547 us | 9 pairs; 40/40 ms; 662,059/65,252 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0.135 us | 0.234 us | 0.252 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.22x | 0.21–0.22x | -0.121 us | 9 pairs; 38/40 ms; 1,155,974/256,364 ops/lane; IQR 1.019/1.016 | `Pcre2Backtracking` | 0.115 us | 0.183 us | 0.202 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.110 us | 0.19x | 0.18–0.20x | -0.089 us | 9 pairs; 42/40 ms; 1,954,548/357,863 ops/lane; IQR 1.025/1.020 | `Pcre2Backtracking` | 0.061 us | 0.090 us | 0.109 us | 0.20x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.123 us | 0.47x | 0.47–0.48x | -0.065 us | 9 pairs; 40/36 ms; 685,603/295,584 ops/lane; IQR 1.015/1.002 | `Pcre2Backtracking` | 0.016 us | 0.087 us | 0.102 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.086 us | 0.278 us | 0.31x | 0.31–0.31x | -0.192 us | 9 pairs; 41/38 ms; 475,371/138,432 ops/lane; IQR 1.004/1.011 | `Pcre2Slice` | 0.095 us | 0.118 us | 0.152 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.116 us | 0.21x | 0.19–0.22x | -0.091 us | 9 pairs; 43/40 ms; 1,731,009/342,628 ops/lane; IQR 1.044/1.023 | `Pcre2Backtracking` | 0.019 us | 0.090 us | 0.108 us | 0.23x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.125 us | 2.109 us | 0.06x | 0.05–0.07x | -1.989 us | 9 pairs; 42/47 ms; 339,887/22,106 ops/lane; IQR 1.044/1.107 | `Pcre2Literal` | 0.087 us | 0.050 us | 0.595 us | 0.21x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.125 us | 1.936 us | 0.06x | 0.06–0.07x | -1.806 us | 9 pairs; 41/34 ms; 327,214/17,757 ops/lane; IQR 1.038/1.084 | `Pcre2Literal` | 0.146 us | 0.053 us | 0.590 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 1.951 us | 0.08x | 0.08–0.08x | -1.796 us | 9 pairs; 40/40 ms; 255,026/20,564 ops/lane; IQR 1.004/1.048 | `Pcre2Literal` | 0.202 us | 0.230 us | 0.764 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.154 us | 1.827 us | 0.08x | 0.08–0.09x | -1.672 us | 9 pairs; 41/39 ms; 264,495/21,203 ops/lane; IQR 1.015/1.038 | `Pcre2Literal` | 0.172 us | 0.205 us | 0.753 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.101 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 38/40 ms; 4,361,253/400,464 ops/lane; IQR 1.003/1.015 | `Pcre2Slice` | 0.253 us | 0.069 us | 0.089 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.089 us | 0.76x | 0.75–0.77x | -0.021 us | 9 pairs; 41/39 ms; 606,044/443,037 ops/lane; IQR 1.008/1.019 | `Pcre2Literal` | 0.134 us | 0.051 us | 0.073 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.110 us | 0.13x | 0.12–0.13x | -0.095 us | 9 pairs; 41/40 ms; 2,860,585/369,201 ops/lane; IQR 1.015/1.010 | `Pcre2Backtracking` | 0.285 us | 0.092 us | 0.113 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.113 us | 0.121 us | 0.95x | 0.93–0.97x | -0.006 us | 9 pairs; 39/38 ms; 341,348/314,036 ops/lane; IQR 1.065/1.023 | `Pcre2Character` | 0.132 us | 0.112 us | 0.143 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.204 us | 0.37x | 0.37–0.38x | -0.127 us | 9 pairs; 41/40 ms; 534,042/197,118 ops/lane; IQR 1.003/1.021 | `Pcre2Slice` | 0.439 us | 0.075 us | 0.105 us | 0.72x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 81.935 us | 159.696 us | 0.51x | 0.51–0.51x | -77.761 us | 9 pairs; 40/40 ms; 486/251 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 327.522 us | 102.788 us | 104.768 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.563 us | 289.050 us | 0.14x | 0.14–0.14x | -249.435 us | 9 pairs; 40/41 ms; 1,005/142 ops/lane; IQR 1.006/1.002 | `Pcre2Character` | 259.573 us | 220.278 us | 231.531 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.448 us | 207.488 us | 0.05x | 0.05–0.05x | -198.071 us | 9 pairs; 40/40 ms; 4,245/195 ops/lane; IQR 1.001/1.013 | `Pcre2Backtracking` | 7.240 us | 228.071 us | 229.306 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 236.320 us | 206.675 us | 1.14x | 1.13–1.15x | +29.144 us | 9 pairs; 39/40 ms; 167/193 ops/lane; IQR 1.003/1.004 | `Pcre2SingleTokenRepeat` | 7.360 us | 239.704 us | 241.472 us | 0.98x | 0 B | 0 B |
| `common/matches-word` | **Inconclusive** | 8,208 B | 3.530 us | 22.811 us | 0.15x | 0.15–0.16x | -19.281 us | 9 pairs; 39/38 ms; 11,130/1,682 ops/lane; IQR 1.009/1.007 | `Pcre2Literal` | 1.381 us | 4.262 us | 5.999 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.597 us | 81.887 us | 0.12x | 0.12–0.12x | -72.286 us | 9 pairs; 40/40 ms; 4,166/484 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 9.314 us | 97.040 us | 99.011 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,190.450 us | 28,890.700 us | 0.15x | 0.14–0.15x | -24678.250 us | 9 pairs; 34/29 ms; 8/1 ops/lane; IQR 1.006/1.061 | `Pcre2Backtracking` | 5,261.230 us | 12,941.350 us | 19,596.700 us | 0.21x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,378.050 us | 55,218.200 us | 0.06x | 0.06–0.06x | -51883.375 us | 9 pairs; 41/55 ms; 12/1 ops/lane; IQR 1.025/1.034 | `Pcre2Backtracking` | 3,904.885 us | 44,516.950 us | 51,858.900 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,935.138 us | 495,289.100 us | 0.00x | 0.00–0.00x | -493401.319 us | 9 pairs; 41/495 ms; 21/1 ops/lane; IQR 1.033/1.003 | `Pcre2Character` | 1,786.345 us | 23,284.267 us | 29,709.600 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Managed faster** | 16,013,977 B | 5,571.950 us | 10,718.075 us | 0.52x | 0.48–0.52x | -5165.892 us | 9 pairs; 33/43 ms; 6/4 ops/lane; IQR 1.005/1.065 | `Pcre2Literal` | 1,274.128 us | 1,350.463 us | 8,294.929 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,755.950 us | 145,895.900 us | 0.13x | 0.13–0.13x | -127065.650 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 1,239.734 us | 506.479 us | 8,444.533 us | 2.22x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,898.875 us | 21,742.650 us | 0.22x | 0.22–0.23x | -16814.488 us | 9 pairs; 39/43 ms; 8/2 ops/lane; IQR 1.006/1.012 | `Pcre2Slice` | 2,517.790 us | 11,501.040 us | 19,469.633 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,265.700 us | 133,361.100 us | 0.17x | 0.17–0.17x | -111146.100 us | 9 pairs; 45/133 ms; 2/1 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 1,534.009 us | 2,340.495 us | 10,327.760 us | 2.16x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 160.881 us | 998.974 us | 0.16x | 0.16–0.16x | -838.363 us | 9 pairs; 39/38 ms; 243/38 ops/lane; IQR 1.002/1.017 | `Pcre2Backtracking` | 172.935 us | 351.924 us | 579.310 us | 0.28x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,232.250 us | 13,411.667 us | 0.84x | 0.83–0.84x | -2178.917 us | 9 pairs; 45/40 ms; 4/3 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 306.125 us | 16,248.325 us | 16,354.675 us | 0.69x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,914.462 us | 97,285.000 us | 0.03x | 0.03–0.03x | -94370.538 us | 9 pairs; 38/97 ms; 13/1 ops/lane; IQR 1.018/1.005 | `Pcre2Character` | 2,056.272 us | 19,874.600 us | 19,984.533 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 186.487 us | 2,849.643 us | 0.07x | 0.06–0.07x | -2663.383 us | 9 pairs; 39/40 ms; 211/14 ops/lane; IQR 1.002/1.011 | `Pcre2SingleTokenRepeat` | 191.166 us | 1,010.404 us | 1,442.245 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,600.365 us | 20,106.400 us | 0.08x | 0.08–0.08x | -18511.604 us | 9 pairs; 42/40 ms; 26/2 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 563.828 us | 10,464.780 us | 10,709.740 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.154 us | 1.089 us | 0.14x | 0.14–0.14x | -0.937 us | 9 pairs; 41/40 ms; 266,586/36,637 ops/lane; IQR 1.020/1.004 | `Pcre2Slice` | 0.661 us | 0.259 us | 0.282 us | 0.55x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.516 us | 0.825 us | 0.63x | 0.62–0.63x | -0.309 us | 9 pairs; 40/41 ms; 77,473/49,775 ops/lane; IQR 1.012/1.004 | `Pcre2Literal` | 0.142 us | 0.150 us | 0.172 us | 3.00x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.300 us | 1.228 us | 0.24x | 0.24–0.24x | -0.927 us | 9 pairs; 41/40 ms; 137,410/32,365 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 1.066 us | 0.349 us | 0.380 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.859 us | 1.122 us | 0.76x | 0.76–0.77x | -0.264 us | 9 pairs; 39/40 ms; 45,545/35,499 ops/lane; IQR 1.009/1.005 | `Pcre2Character` | 0.285 us | 0.427 us | 0.470 us | 1.83x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.164 us | 0.863 us | 0.19x | 0.19–0.19x | -0.699 us | 9 pairs; 38/41 ms; 230,328/47,023 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | 0.806 us | 0.154 us | 0.201 us | 0.81x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.884 us | 1.101 us | 0.80x | 0.80–0.81x | -0.219 us | 9 pairs; 40/40 ms; 45,459/36,044 ops/lane; IQR 1.012/1.005 | `Pcre2Slice` | 2.984 us | 0.305 us | 0.325 us | 2.72x | 0 B | 0 B |
| `simple/foo-dense` | **Equivalent** | 18 B | 0.823 us | 0.831 us | 0.99x | 0.99–1.00x | -0.006 us | 9 pairs; 40/40 ms; 48,483/47,663 ops/lane; IQR 1.005/1.003 | `Pcre2Literal` | 3.508 us | 0.182 us | 0.202 us | 4.08x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.993 us | 1.187 us | 0.84x | 0.84–0.84x | -0.194 us | 9 pairs; 40/41 ms; 40,422/34,831 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 5.240 us | 0.403 us | 0.427 us | 2.32x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.214 us | 1.133 us | 1.07x | 1.07–1.07x | +0.082 us | 9 pairs; 41/40 ms; 34,176/35,321 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 3.623 us | 0.481 us | 0.519 us | 2.34x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.796 us | 0.862 us | 0.92x | 0.92–0.93x | -0.066 us | 9 pairs; 40/40 ms; 49,612/46,753 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 2.102 us | 0.181 us | 0.223 us | 3.57x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.565 us | 1.101 us | 0.51x | 0.51–0.51x | -0.535 us | 9 pairs; 37/39 ms; 65,781/35,843 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.502 us | 0.830 us | 0.60x | 0.60–0.61x | -0.328 us | 9 pairs; 39/39 ms; 78,584/47,510 ops/lane; IQR 1.003/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.675 us | 1.183 us | 0.57x | 0.57–0.57x | -0.509 us | 9 pairs; 40/39 ms; 59,950/33,012 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.920 us | 1.131 us | 0.81x | 0.81–0.81x | -0.213 us | 9 pairs; 40/40 ms; 43,829/35,408 ops/lane; IQR 1.004/1.007 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.480 us | 0.861 us | 0.56x | 0.56–0.56x | -0.381 us | 9 pairs; 38/40 ms; 79,724/46,778 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.944 us | — | — | — | — | — | `—` | 25.827 us | 101.407 us | 104.299 us | 0.59x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.972 us | — | — | — | — | — | `—` | 1.648 us | 0.355 us | 0.380 us | 13.08x | 7,056 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.068 us | — | — | — | — | — | `—` | 0.967 us | 0.241 us | 0.268 us | 3.99x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.233 us | — | — | — | — | — | `—` | 3.112 us | 0.455 us | 0.483 us | 2.55x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.469 us | — | — | — | — | — | `—` | 1.078 us | 0.532 us | 0.585 us | 2.51x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.707 us | — | — | — | — | — | `—` | 1.558 us | 0.268 us | 0.319 us | 11.61x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.090 us | 0.085 us | 1.06x | 1.06–1.06x | +0.005 us | 9 pairs; 40/42 ms; 449,446/494,152 ops/lane; IQR 1.006/1.006 | `Pcre2Character` | 0 B | 0 B | 2.281 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.122 us | 0.11x | 0.11–0.11x | -0.109 us | 9 pairs; 39/40 ms; 3,024,041/324,800 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 131.375 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.382 us | 0.103 us | 3.69x | 3.66–3.70x | +0.278 us | 9 pairs; 36/39 ms; 93,503/376,494 ops/lane; IQR 1.012/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 23.956 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.395 us | 0.116 us | 3.38x | 3.36–3.42x | +0.278 us | 9 pairs; 36/41 ms; 90,716/349,350 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 71.275 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.374 us | 0.105 us | 3.55x | 3.52–3.56x | +0.268 us | 9 pairs; 36/40 ms; 95,835/375,562 ops/lane; IQR 1.009/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 70.394 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.665 us | 0.346 us | 1.93x | 1.91–1.94x | +0.321 us | 9 pairs; 38/39 ms; 56,857/113,222 ops/lane; IQR 1.003/1.010 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.631 us | 0.02x | 0.02–0.02x | -0.619 us | 9 pairs; 28/40 ms; 2,446,514/62,854 ops/lane; IQR 1.013/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 22.869 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.516 us | 0.02x | 0.02–0.02x | -0.504 us | 9 pairs; 40/39 ms; 3,448,204/75,694 ops/lane; IQR 1.005/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 23.512 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.471 us | 0.107 us | 4.40x | 4.38–4.45x | +0.365 us | 9 pairs; 37/40 ms; 79,465/378,159 ops/lane; IQR 1.005/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 19.212 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.385 us | 10.532 us | 0.80x | 0.79–0.80x | -2.154 us | 9 pairs; 39/35 ms; 4,668/3,282 ops/lane; IQR 1.004/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.292 us | 1.100 us | 0.27x | 0.26–0.27x | -0.808 us | 9 pairs; 40/41 ms; 135,463/37,138 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 132.537 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.310 us | 0.490 us | 0.63x | 0.63–0.63x | -0.180 us | 9 pairs; 37/39 ms; 120,435/80,280 ops/lane; IQR 1.001/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 115.963 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.284 us | 0.455 us | 0.63x | 0.62–0.63x | -0.170 us | 9 pairs; 36/38 ms; 128,235/83,585 ops/lane; IQR 1.006/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 142.037 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.348 us | 0.583 us | 0.60x | 0.60–0.60x | -0.235 us | 9 pairs; 42/41 ms; 121,808/69,474 ops/lane; IQR 1.020/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 103.269 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.304 us | 0.396 us | 0.77x | 0.76–0.77x | -0.092 us | 9 pairs; 38/40 ms; 125,096/101,221 ops/lane; IQR 1.002/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 218.975 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.142 us | 8.748 us | 0.59x | 0.58–0.59x | -3.593 us | 9 pairs; 41/40 ms; 8,055/4,518 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 0 B | 0 B | 2.138 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.255 us | 0.310 us | 0.82x | 0.82–0.83x | -0.055 us | 9 pairs; 34/39 ms; 133,439/125,218 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 94.106 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.270 us | 0.401 us | 0.67x | 0.67–0.68x | -0.131 us | 9 pairs; 37/39 ms; 135,469/97,944 ops/lane; IQR 1.004/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 123.800 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.906 us | — | — | — | — | — | `—` | 0 B | — | 13.975 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.189 us | 0.292 us | 0.65x | 0.64–0.65x | -0.103 us | 9 pairs; 40/40 ms; 212,268/136,800 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 54.419 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.812 us | — | — | — | — | — | `—` | 0 B | — | 14.206 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.600 us | 0.671 us | 0.89x | 0.89–0.90x | -0.072 us | 9 pairs; 33/39 ms; 55,567/57,742 ops/lane; IQR 1.003/1.006 | `Pcre2Slice` | 0 B | 0 B | 63.712 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.901 us | 28.979 us | 0.34x | 0.34–0.34x | -19.069 us | 9 pairs; 40/40 ms; 4,066/1,365 ops/lane; IQR 1.002/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.950 us | 1.076 us | 0.88x | 0.88–0.89x | -0.125 us | 9 pairs; 40/41 ms; 42,201/38,431 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 131.031 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.035 us | 1.228 us | 0.85x | 0.84–0.85x | -0.190 us | 9 pairs; 40/40 ms; 38,216/32,435 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 117.888 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 1.007 us | 1.043 us | 0.96x | 0.96–0.97x | -0.037 us | 9 pairs; 42/41 ms; 41,498/39,146 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 144.050 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.945 us | 1.166 us | 0.81x | 0.81–0.81x | -0.223 us | 9 pairs; 40/41 ms; 42,579/35,528 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 105.150 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.927 us | 0.954 us | 0.97x | 0.97–0.98x | -0.026 us | 9 pairs; 39/42 ms; 42,278/43,530 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 218.656 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.563 us | 8.814 us | 0.63x | 0.63–0.63x | -3.258 us | 9 pairs; 39/40 ms; 7,100/4,517 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.915 us | 0.890 us | 1.03x | 1.03–1.03x | +0.025 us | 9 pairs; 40/40 ms; 44,021/45,132 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 95.006 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.919 us | 0.964 us | 0.95x | 0.95–0.96x | -0.045 us | 9 pairs; 40/41 ms; 43,681/42,159 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 126.150 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.102 us | — | — | — | — | — | `—` | 0 B | — | 13.706 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.825 us | 0.858 us | 0.96x | 0.96–0.96x | -0.034 us | 9 pairs; 39/40 ms; 47,411/46,182 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 54.219 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.073 us | — | — | — | — | — | `—` | 0 B | — | 13.950 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.857 us | 1.257 us | 0.68x | 0.68–0.68x | -0.399 us | 9 pairs; 37/39 ms; 43,497/31,094 ops/lane; IQR 1.004/1.007 | `Pcre2Slice` | 0 B | 0 B | 65.894 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.445 us | 2.287 us | 0.63x | 0.63–0.63x | -0.840 us | 9 pairs; 40/40 ms; 27,502/17,517 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.640 us | 1.067 us | 0.60x | 0.60–0.60x | -0.429 us | 9 pairs; 40/41 ms; 62,539/38,356 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 131.769 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.680 us | 1.226 us | 0.55x | 0.55–0.56x | -0.547 us | 9 pairs; 40/40 ms; 58,289/32,342 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 116.006 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.638 us | 1.033 us | 0.62x | 0.62–0.62x | -0.393 us | 9 pairs; 40/42 ms; 62,344/40,587 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 141.381 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.626 us | 1.156 us | 0.54x | 0.54–0.54x | -0.529 us | 9 pairs; 40/39 ms; 63,960/33,755 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 106.300 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.615 us | 0.953 us | 0.65x | 0.64–0.65x | -0.337 us | 9 pairs; 40/40 ms; 64,441/42,368 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 214.519 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.906 us | 2.176 us | 0.88x | 0.87–0.88x | -0.263 us | 9 pairs; 40/40 ms; 20,826/18,524 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 0 B | 0 B | 1.387 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.601 us | 0.889 us | 0.68x | 0.67–0.68x | -0.289 us | 9 pairs; 40/40 ms; 65,913/44,744 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 94.188 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.604 us | 0.970 us | 0.62x | 0.62–0.63x | -0.365 us | 9 pairs; 39/40 ms; 65,270/41,518 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 125.394 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.803 us | — | — | — | — | — | `—` | 0 B | — | 13.906 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.490 us | 0.852 us | 0.58x | 0.57–0.58x | -0.362 us | 9 pairs; 40/40 ms; 81,444/46,646 ops/lane; IQR 1.003/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 55.375 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.732 us | — | — | — | — | — | `—` | 0 B | — | 14.494 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.600 us | 1.266 us | 0.47x | 0.47–0.48x | -0.666 us | 9 pairs; 39/40 ms; 65,272/31,359 ops/lane; IQR 1.001/1.003 | `Pcre2Slice` | 0 B | 0 B | 64.725 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.229 us | — | — | — | — | — | `—` | 480 B | — | 0.719 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.143 us | — | — | — | — | — | `—` | 48 B | — | 135.537 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.241 us | — | — | — | — | — | `—` | 48 B | — | 118.406 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.126 us | — | — | — | — | — | `—` | 48 B | — | 147.094 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.141 us | — | — | — | — | — | `—` | 48 B | — | 102.844 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.125 us | — | — | — | — | — | `—` | 48 B | — | 224.238 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.858 us | — | — | — | — | — | `—` | 64 B | — | 1.444 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.155 us | — | — | — | — | — | `—` | 40 B | — | 93.706 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.155 us | — | — | — | — | — | `—` | 40 B | — | 123.544 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.257 us | — | — | — | — | — | `—` | 1,216 B | — | 15.194 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.061 us | — | — | — | — | — | `—` | 48 B | — | 53.681 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.599 us | — | — | — | — | — | `—` | 928 B | — | 13.900 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.770 us | — | — | — | — | — | `—` | 7,184 B | — | 64.619 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 30.694–238.485 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.491–95.482 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.663–12.609 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.973–46.386 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.037–1.150 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.607–43.130 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.202–23.904 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.226–99.725 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.119–72.800 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.213–5.404 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.252–366.911 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.070–0.084 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.098–303.857 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.843–5.121 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.180–15.545 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 106.532–830.976 us |

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
