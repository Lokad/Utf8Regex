<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `A1B57853E2835BAEACFFAE2D9E0E946525FCCB6F3169D658B346713CFCD977D2`
- Latest managed row measurement: `2026-11-06T15:31:42.0388705+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T15:31:42.0388705+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `0` equivalent, `12` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `f105c683013f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `f105c683013f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.611 us | 2.145 us | 0.28x | 0.24–0.30x | -1.538 us | 9 pairs; 36/33 ms; 58,484/15,460 ops/lane; IQR 1.014/1.132 | `Pcre2Slice` | 470.517 us | 0.280 us | 0.848 us | 0.72x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.816 us | 12.649 us | 0.06x | 0.06–0.07x | -11.837 us | 9 pairs; 37/44 ms; 45,157/3,457 ops/lane; IQR 1.009/1.156 | `Pcre2Slice` | 0.609 us | 0.097 us | 3.639 us | 0.22x | 272 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.216 us | 0.01x | 0.01–0.01x | -1.206 us | 9 pairs; 39/41 ms; 3,640,552/33,304 ops/lane; IQR 1.003/1.006 | `Pcre2Slice` | 0.106 us | 1.097 us | 1.110 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.673 us | 0.159 us | 4.19x | 4.16–4.23x | +0.511 us | 9 pairs; 38/39 ms; 55,908/244,127 ops/lane; IQR 1.005/1.011 | `Pcre2Backtracking` | 0.046 us | 0.120 us | 0.161 us | 4.17x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.356 us | 0.574 us | 4.10x | 4.07–4.12x | +1.780 us | 9 pairs; 39/41 ms; 16,639/71,671 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0.117 us | 0.722 us | 0.750 us | 3.14x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.101 us | 0.295 us | 0.34x | 0.34–0.34x | -0.194 us | 9 pairs; 34/40 ms; 339,954/136,132 ops/lane; IQR 1.004/1.017 | `Pcre2Slice` | 0.115 us | 0.315 us | 0.343 us | 0.30x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.107 us | 0.360 us | 0.30x | 0.30–0.30x | -0.253 us | 9 pairs; 40/39 ms; 369,610/109,096 ops/lane; IQR 1.003/1.005 | `Pcre2Slice` | 0.119 us | 0.449 us | 0.468 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.291 us | 0.28x | 0.28–0.28x | -0.210 us | 9 pairs; 40/39 ms; 499,795/135,315 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | 0.092 us | 0.232 us | 0.252 us | 0.32x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.081 us | 0.317 us | 0.26x | 0.25–0.26x | -0.235 us | 9 pairs; 40/41 ms; 497,061/128,707 ops/lane; IQR 1.021/1.009 | `Pcre2Slice` | 0.092 us | 0.244 us | 0.266 us | 0.30x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.077 us | 2.23x | 2.18–2.34x | +0.094 us | 9 pairs; 40/43 ms; 231,317/557,544 ops/lane; IQR 1.006/1.029 | `Pcre2Slice` | 0.091 us | 0.923 us | 0.961 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.196 us | 0.17x | 0.17–0.17x | -0.163 us | 9 pairs; 40/41 ms; 1,183,697/207,089 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0.112 us | 0.298 us | 0.340 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.613 us | 0.10x | 0.10–0.10x | -0.552 us | 9 pairs; 40/39 ms; 669,932/63,496 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0.137 us | 0.231 us | 0.257 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Inconclusive** | 18 B | 0.034 us | 0.158 us | 0.21x | 0.21–0.22x | -0.124 us | 9 pairs; 38/40 ms; 1,142,823/256,248 ops/lane; IQR 1.030/1.012 | `Pcre2Backtracking` | 0.112 us | 0.183 us | 0.205 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.110 us | 0.18x | 0.18–0.18x | -0.090 us | 9 pairs; 41/41 ms; 2,070,075/375,849 ops/lane; IQR 1.010/1.014 | `Pcre2Backtracking` | 0.059 us | 0.091 us | 0.110 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.125 us | 0.46x | 0.46–0.47x | -0.067 us | 9 pairs; 40/41 ms; 690,310/330,723 ops/lane; IQR 1.002/1.016 | `Pcre2Backtracking` | 0.013 us | 0.085 us | 0.103 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.084 us | 0.277 us | 0.30x | 0.30–0.31x | -0.192 us | 9 pairs; 40/43 ms; 471,610/156,187 ops/lane; IQR 1.007/1.011 | `Pcre2Slice` | 0.097 us | 0.116 us | 0.156 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.115 us | 0.21x | 0.20–0.21x | -0.091 us | 9 pairs; 44/41 ms; 1,821,773/357,465 ops/lane; IQR 1.019/1.008 | `Pcre2Backtracking` | 0.019 us | 0.088 us | 0.107 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.120 us | 1.854 us | 0.06x | 0.06–0.07x | -1.734 us | 9 pairs; 40/31 ms; 337,353/16,882 ops/lane; IQR 1.001/1.038 | `Pcre2Literal` | 0.086 us | 0.051 us | 0.598 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 1.942 us | 0.06x | 0.06–0.07x | -1.820 us | 9 pairs; 41/42 ms; 337,199/21,867 ops/lane; IQR 1.005/1.051 | `Pcre2Literal` | 0.143 us | 0.054 us | 0.598 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 1.930 us | 0.08x | 0.07–0.08x | -1.774 us | 9 pairs; 40/34 ms; 258,437/17,469 ops/lane; IQR 1.006/1.199 | `Pcre2Literal` | 0.202 us | 0.233 us | 0.794 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.890 us | 0.08x | 0.08–0.08x | -1.738 us | 9 pairs; 26/39 ms; 170,770/20,549 ops/lane; IQR 1.008/1.056 | `Pcre2Literal` | 0.170 us | 0.206 us | 0.769 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 38/40 ms; 4,367,975/402,410 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | 0.234 us | 0.068 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.087 us | 0.78x | 0.75–0.78x | -0.019 us | 9 pairs; 33/39 ms; 493,651/442,417 ops/lane; IQR 1.005/1.025 | `Pcre2Literal` | 0.127 us | 0.051 us | 0.073 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.109 us | 0.13x | 0.12–0.13x | -0.095 us | 9 pairs; 41/38 ms; 2,911,094/346,802 ops/lane; IQR 1.019/1.026 | `Pcre2Backtracking` | 0.255 us | 0.091 us | 0.114 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.123 us | 0.92x | 0.91–0.94x | -0.010 us | 9 pairs; 39/39 ms; 342,857/317,794 ops/lane; IQR 1.004/1.030 | `Pcre2Character` | 0.149 us | 0.112 us | 0.143 us | 0.80x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.076 us | 0.210 us | 0.36x | 0.35–0.37x | -0.134 us | 9 pairs; 40/42 ms; 528,315/200,257 ops/lane; IQR 1.005/1.028 | `Pcre2Slice` | 0.414 us | 0.074 us | 0.113 us | 0.67x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.407 us | 160.354 us | 0.51x | 0.51–0.52x | -77.801 us | 9 pairs; 40/40 ms; 484/252 ops/lane; IQR 1.004/1.003 | `Pcre2Slice` | 343.244 us | 96.715 us | 99.262 us | 0.83x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.582 us | 282.603 us | 0.14x | 0.14–0.14x | -242.985 us | 9 pairs; 40/40 ms; 1,018/143 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 268.476 us | 217.014 us | 227.842 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.205 us | 210.666 us | 0.05x | 0.05–0.05x | -200.528 us | 9 pairs; 38/41 ms; 3,679/194 ops/lane; IQR 1.009/1.004 | `Pcre2Backtracking` | 7.237 us | 220.379 us | 223.743 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.012 us | 214.455 us | 1.12x | 1.12–1.13x | +26.181 us | 9 pairs; 40/42 ms; 167/198 ops/lane; IQR 1.002/1.008 | `Pcre2SingleTokenRepeat` | 7.279 us | 230.769 us | 233.094 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Inconclusive** | 8,208 B | 3.554 us | 22.997 us | 0.16x | 0.15–0.16x | -19.419 us | 9 pairs; 40/39 ms; 11,143/1,700 ops/lane; IQR 1.004/1.041 | `Pcre2Literal` | 1.400 us | 4.267 us | 6.049 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.557 us | 81.687 us | 0.12x | 0.12–0.12x | -72.094 us | 9 pairs; 40/40 ms; 4,172/489 ops/lane; IQR 1.003/1.016 | `Pcre2Backtracking` | 9.428 us | 90.326 us | 92.593 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,153.150 us | 28,782.400 us | 0.14x | 0.14–0.15x | -24639.450 us | 9 pairs; 33/29 ms; 8/1 ops/lane; IQR 1.004/1.013 | `Pcre2Backtracking` | 6,244.646 us | 11,584.814 us | 18,582.480 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,363.742 us | 54,089.000 us | 0.06x | 0.06–0.06x | -50756.150 us | 9 pairs; 40/54 ms; 12/1 ops/lane; IQR 1.012/1.034 | `Pcre2Backtracking` | 3,748.029 us | 40,234.450 us | 47,753.900 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,940.186 us | 484,529.900 us | 0.00x | 0.00–0.00x | -482636.210 us | 9 pairs; 41/485 ms; 21/1 ops/lane; IQR 1.027/1.001 | `Pcre2Character` | 1,807.595 us | 23,572.700 us | 30,475.167 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,540.800 us | 11,768.867 us | 0.48x | 0.46–0.50x | -6165.100 us | 9 pairs; 33/35 ms; 6/3 ops/lane; IQR 1.004/1.050 | `Pcre2Literal` | 1,303.928 us | 1,329.142 us | 8,585.722 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,834.250 us | 144,726.100 us | 0.13x | 0.13–0.13x | -126202.900 us | 9 pairs; 38/145 ms; 2/1 ops/lane; IQR 1.016/1.006 | `Pcre2Backtracking` | 1,240.852 us | 505.210 us | 8,543.222 us | 2.20x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,908.200 us | 21,803.050 us | 0.23x | 0.22–0.23x | -16910.563 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.005/1.009 | `Pcre2Slice` | 2,461.861 us | 10,624.350 us | 18,838.860 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,342.100 us | 134,241.900 us | 0.17x | 0.17–0.17x | -112061.550 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 1,511.648 us | 2,317.956 us | 10,527.087 us | 2.12x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 139.026 us | 1,012.510 us | 0.14x | 0.13–0.14x | -875.109 us | 9 pairs; 40/39 ms; 291/39 ops/lane; IQR 1.007/1.016 | `Pcre2Backtracking` | 171.726 us | 319.281 us | 570.809 us | 0.24x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,569.400 us | 13,479.100 us | 0.93x | 0.93–0.94x | -890.100 us | 9 pairs; 38/40 ms; 3/3 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 311.832 us | 14,907.460 us | 15,156.580 us | 0.83x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,818.179 us | 98,956.800 us | 0.03x | 0.03–0.03x | -96128.764 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 2,055.857 us | 19,767.575 us | 20,080.875 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.692 us | 2,772.180 us | 0.07x | 0.07–0.07x | -2586.649 us | 9 pairs; 40/42 ms; 213/15 ops/lane; IQR 1.005/1.014 | `Pcre2SingleTokenRepeat` | 189.688 us | 1,007.941 us | 1,246.171 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,590.048 us | 20,220.600 us | 0.08x | 0.08–0.08x | -18638.094 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.009/1.001 | `Pcre2Backtracking` | 571.395 us | 10,380.725 us | 10,756.700 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.136 us | 1.098 us | 0.12x | 0.12–0.12x | -0.961 us | 9 pairs; 40/40 ms; 290,430/36,531 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 0.660 us | 0.257 us | 0.278 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.533 us | 0.827 us | 0.65x | 0.64–0.65x | -0.292 us | 9 pairs; 37/42 ms; 69,713/50,262 ops/lane; IQR 1.009/1.004 | `Pcre2Literal` | 0.160 us | 0.149 us | 0.172 us | 3.11x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.337 us | 1.177 us | 0.29x | 0.29–0.29x | -0.840 us | 9 pairs; 45/40 ms; 133,135/33,937 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 1.080 us | 0.347 us | 0.380 us | 0.89x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.906 us | 1.127 us | 0.80x | 0.80–0.81x | -0.221 us | 9 pairs; 39/40 ms; 43,451/35,642 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0.284 us | 0.421 us | 0.460 us | 1.97x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.154 us | 0.864 us | 0.18x | 0.18–0.18x | -0.710 us | 9 pairs; 40/40 ms; 262,370/46,422 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 0.790 us | 0.156 us | 0.199 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.956 us | 1.099 us | 0.87x | 0.87–0.87x | -0.141 us | 9 pairs; 40/40 ms; 41,793/36,131 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 2.645 us | 0.304 us | 0.325 us | 2.95x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.796 us | 0.833 us | 0.95x | 0.95–0.96x | -0.038 us | 9 pairs; 40/41 ms; 50,082/48,985 ops/lane; IQR 1.002/1.003 | `Pcre2Literal` | 3.591 us | 0.180 us | 0.202 us | 3.95x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.988 us | 1.189 us | 0.83x | 0.83–0.83x | -0.201 us | 9 pairs; 40/40 ms; 40,524/33,597 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 5.255 us | 0.402 us | 0.424 us | 2.33x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.225 us | 1.139 us | 1.07x | 1.07–1.08x | +0.085 us | 9 pairs; 42/40 ms; 34,262/35,062 ops/lane; IQR 1.007/1.004 | `Pcre2Character` | 3.684 us | 0.471 us | 0.510 us | 2.40x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.797 us | 0.861 us | 0.93x | 0.92–0.93x | -0.064 us | 9 pairs; 39/40 ms; 49,487/46,317 ops/lane; IQR 1.004/1.008 | `Pcre2Slice` | 2.061 us | 0.182 us | 0.224 us | 3.56x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.577 us | 1.101 us | 0.52x | 0.52–0.52x | -0.524 us | 9 pairs; 41/40 ms; 70,451/36,243 ops/lane; IQR 1.002/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.500 us | 0.831 us | 0.60x | 0.60–0.60x | -0.331 us | 9 pairs; 41/40 ms; 82,707/48,422 ops/lane; IQR 1.001/1.001 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.708 us | 1.189 us | 0.60x | 0.60–0.60x | -0.480 us | 9 pairs; 40/40 ms; 56,238/33,592 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.176 us | 1.143 us | 1.03x | 1.03–1.03x | +0.034 us | 9 pairs; 52/40 ms; 44,135/35,021 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.540 us | 0.860 us | 0.63x | 0.62–0.63x | -0.319 us | 9 pairs; 40/40 ms; 74,419/46,678 ops/lane; IQR 1.006/1.009 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.719 us | — | — | — | — | — | `—` | 25.767 us | 97.684 us | 100.759 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.315 us | — | — | — | — | — | `—` | 1.581 us | 0.362 us | 0.386 us | 11.17x | 3,024 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.089 us | — | — | — | — | — | `—` | 0.910 us | 0.245 us | 0.273 us | 3.99x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.226 us | — | — | — | — | — | `—` | 3.115 us | 0.460 us | 0.499 us | 2.46x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.798 us | — | — | — | — | — | `—` | 1.009 us | 0.529 us | 0.578 us | 3.11x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.688 us | — | — | — | — | — | `—` | 1.510 us | 0.274 us | 0.327 us | 11.27x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.092 us | 0.084 us | 1.10x | 1.09–1.59x | +0.008 us | 9 pairs; 39/39 ms; 425,206/458,543 ops/lane; IQR 1.432/1.017 | `Pcre2Character` | 0 B | 0 B | 2.194 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.121 us | 0.11x | 0.10–0.11x | -0.108 us | 9 pairs; 40/38 ms; 2,999,016/310,113 ops/lane; IQR 1.005/1.039 | `Pcre2Backtracking` | 0 B | 0 B | 131.113 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.389 us | 0.105 us | 3.71x | 3.68–3.73x | +0.284 us | 9 pairs; 35/40 ms; 89,417/380,481 ops/lane; IQR 1.009/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 20.944 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.388 us | 0.116 us | 3.35x | 3.33–3.37x | +0.271 us | 9 pairs; 38/41 ms; 96,617/353,822 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 71.575 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.385 us | 0.109 us | 3.54x | 3.52–3.67x | +0.276 us | 9 pairs; 36/40 ms; 93,146/366,041 ops/lane; IQR 1.042/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 71.287 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.660 us | 0.348 us | 1.90x | 1.88–1.91x | +0.313 us | 9 pairs; 37/39 ms; 56,710/113,264 ops/lane; IQR 1.003/1.011 | `Pcre2Character` | 0 B | 0 B | 1.488 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.633 us | 0.02x | 0.02–0.02x | -0.621 us | 9 pairs; 43/40 ms; 3,539,844/63,660 ops/lane; IQR 1.018/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 21.994 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.517 us | 0.02x | 0.02–0.02x | -0.506 us | 9 pairs; 38/40 ms; 3,379,306/77,631 ops/lane; IQR 1.018/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 22.900 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.489 us | 0.108 us | 4.52x | 4.48–4.55x | +0.380 us | 9 pairs; 37/39 ms; 76,579/363,443 ops/lane; IQR 1.003/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 18.581 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.462 us | 10.436 us | 0.81x | 0.80–0.81x | -1.997 us | 9 pairs; 39/40 ms; 4,654/3,821 ops/lane; IQR 1.006/1.010 | `Pcre2Literal` | 0 B | 0 B | 1.438 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.335 us | 1.065 us | 0.32x | 0.31–0.32x | -0.728 us | 9 pairs; 44/41 ms; 130,725/38,205 ops/lane; IQR 1.010/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 133.444 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.313 us | 0.475 us | 0.65x | 0.64–0.66x | -0.165 us | 9 pairs; 33/41 ms; 104,703/85,558 ops/lane; IQR 1.024/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 114.787 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.294 us | 0.456 us | 0.64x | 0.63–0.65x | -0.162 us | 9 pairs; 34/41 ms; 116,726/89,144 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 141.156 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.295 us | 0.588 us | 0.50x | 0.50–0.50x | -0.294 us | 9 pairs; 33/40 ms; 112,543/68,074 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 105.062 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.331 us | 0.397 us | 0.83x | 0.82–0.84x | -0.066 us | 9 pairs; 40/41 ms; 121,585/102,211 ops/lane; IQR 1.007/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 217.269 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.012 us | 8.797 us | 0.57x | 0.57–0.57x | -3.786 us | 9 pairs; 40/40 ms; 8,011/4,540 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 0 B | 0 B | 2.169 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.263 us | 0.309 us | 0.85x | 0.84–0.85x | -0.047 us | 9 pairs; 37/41 ms; 139,816/133,236 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 91.606 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.270 us | 0.400 us | 0.68x | 0.67–0.68x | -0.129 us | 9 pairs; 37/39 ms; 136,670/97,108 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 122.306 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.837 us | — | — | — | — | — | `—` | 0 B | — | 14.338 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.189 us | 0.299 us | 0.63x | 0.63–0.63x | -0.110 us | 9 pairs; 40/41 ms; 212,882/136,859 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 53.819 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.703 us | — | — | — | — | — | `—` | 0 B | — | 13.694 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.592 us | 0.675 us | 0.87x | 0.87–0.88x | -0.084 us | 9 pairs; 32/39 ms; 54,062/57,284 ops/lane; IQR 1.008/1.009 | `Pcre2Slice` | 0 B | 0 B | 65.806 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.918 us | 28.993 us | 0.34x | 0.34–0.34x | -19.082 us | 9 pairs; 39/40 ms; 3,960/1,384 ops/lane; IQR 1.003/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.731 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.939 us | 1.067 us | 0.88x | 0.88–0.88x | -0.128 us | 9 pairs; 38/41 ms; 40,659/38,665 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 130.037 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.004 us | 1.191 us | 0.84x | 0.84–0.84x | -0.189 us | 9 pairs; 40/40 ms; 39,788/33,811 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.544 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.954 us | 1.029 us | 0.93x | 0.92–0.93x | -0.073 us | 9 pairs; 39/40 ms; 40,397/39,303 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 142.781 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.951 us | 1.170 us | 0.81x | 0.81–0.82x | -0.218 us | 9 pairs; 40/40 ms; 42,098/33,954 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 103.500 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.897 us | 0.941 us | 0.95x | 0.95–0.96x | -0.043 us | 9 pairs; 39/41 ms; 43,706/43,084 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 213.338 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.598 us | 8.929 us | 0.63x | 0.62–0.63x | -3.336 us | 9 pairs; 40/40 ms; 7,140/4,504 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0 B | 0 B | 1.406 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.934 us | 0.892 us | 1.05x | 1.04–1.05x | +0.041 us | 9 pairs; 40/39 ms; 42,914/44,130 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 91.594 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.929 us | 1.003 us | 0.93x | 0.92–0.93x | -0.072 us | 9 pairs; 40/41 ms; 42,775/40,739 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 124.406 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.067 us | — | — | — | — | — | `—` | 0 B | — | 13.256 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.836 us | 0.877 us | 0.95x | 0.95–0.96x | -0.040 us | 9 pairs; 36/41 ms; 43,582/46,553 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.181 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.012 us | — | — | — | — | — | `—` | 0 B | — | 13.537 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.920 us | 1.247 us | 0.74x | 0.73–0.74x | -0.328 us | 9 pairs; 40/40 ms; 43,598/32,109 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.812 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.434 us | 2.297 us | 0.63x | 0.62–0.63x | -0.862 us | 9 pairs; 40/40 ms; 27,681/17,413 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.744 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.648 us | 1.071 us | 0.61x | 0.60–0.61x | -0.424 us | 9 pairs; 40/40 ms; 61,089/37,342 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 131.794 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.686 us | 1.192 us | 0.58x | 0.57–0.58x | -0.507 us | 9 pairs; 41/41 ms; 59,154/34,383 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 117.763 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.646 us | 1.024 us | 0.63x | 0.63–0.63x | -0.377 us | 9 pairs; 40/42 ms; 61,677/40,518 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 144.125 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.642 us | 1.167 us | 0.55x | 0.55–0.55x | -0.525 us | 9 pairs; 40/40 ms; 62,606/34,682 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.094 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.634 us | 0.941 us | 0.67x | 0.67–0.68x | -0.309 us | 9 pairs; 40/40 ms; 63,171/42,778 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 215.031 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.935 us | 2.198 us | 0.88x | 0.88–0.88x | -0.262 us | 9 pairs; 40/41 ms; 20,422/18,692 ops/lane; IQR 1.005/1.002 | `Pcre2Character` | 0 B | 0 B | 1.394 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.612 us | 0.890 us | 0.69x | 0.68–0.69x | -0.277 us | 9 pairs; 40/41 ms; 65,638/45,667 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 93.869 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.610 us | 1.037 us | 0.59x | 0.59–0.59x | -0.426 us | 9 pairs; 40/40 ms; 65,395/38,513 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 122.975 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.767 us | — | — | — | — | — | `—` | 0 B | — | 14.025 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.491 us | 0.866 us | 0.57x | 0.56–0.57x | -0.375 us | 9 pairs; 40/40 ms; 80,821/46,340 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 54.094 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.680 us | — | — | — | — | — | `—` | 0 B | — | 14.887 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.609 us | 1.259 us | 0.48x | 0.48–0.49x | -0.649 us | 9 pairs; 41/40 ms; 66,499/31,964 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | 0 B | 0 B | 65.825 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.604 us | — | — | — | — | — | `—` | 480 B | — | 0.675 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.175 us | — | — | — | — | — | `—` | 48 B | — | 132.294 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.251 us | — | — | — | — | — | `—` | 48 B | — | 116.088 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.138 us | — | — | — | — | — | `—` | 48 B | — | 143.056 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.166 us | — | — | — | — | — | `—` | 48 B | — | 101.769 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.131 us | — | — | — | — | — | `—` | 48 B | — | 216.113 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.065 us | — | — | — | — | — | `—` | 64 B | — | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.161 us | — | — | — | — | — | `—` | 40 B | — | 93.569 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.141 us | — | — | — | — | — | `—` | 40 B | — | 124.469 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.130 us | — | — | — | — | — | `—` | 1,216 B | — | 13.863 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.103 us | — | — | — | — | — | `—` | 48 B | — | 53.744 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.869 us | — | — | — | — | — | `—` | 928 B | — | 15.050 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.817 us | — | — | — | — | — | `—` | 4,496 B | — | 64.681 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 33.998–255.028 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.381–94.692 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.858–14.427 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.307–49.828 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.033–1.144 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 6.132–48.196 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.790–22.702 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.185–99.688 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.158–74.974 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.246–5.753 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.374–361.737 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 41.054–320.218 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.873–4.138 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.132–15.709 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 108.003–829.297 us |

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
