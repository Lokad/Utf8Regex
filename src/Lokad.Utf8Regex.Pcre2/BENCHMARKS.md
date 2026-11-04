<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `F0FD5F8750F4340C83C6F30D68AC66E523A5082830A49DBC27E6161956B8F8FA`
- Latest managed row measurement: `2026-11-04T18:47:03.4726949+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T18:47:03.4726949+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `1` equivalent, `11` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `11a26e26db67` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `11a26e26db67` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.879 us | 2.279 us | 0.38x | 0.36–0.42x | -1.400 us | 9 pairs; 34/42 ms; 38,150/18,219 ops/lane; IQR 1.042/1.114 | `Pcre2Slice` | 541.011 us | 0.276 us | 0.779 us | 1.13x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.080 us | 9.979 us | 0.11x | 0.08–0.12x | -8.905 us | 9 pairs; 36/44 ms; 33,094/4,457 ops/lane; IQR 1.011/1.226 | `Pcre2Slice` | 0.852 us | 0.095 us | 3.336 us | 0.32x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.205 us | 0.01x | 0.01–0.01x | -1.194 us | 9 pairs; 41/41 ms; 3,880,166/33,973 ops/lane; IQR 1.012/1.006 | `Pcre2Slice` | 0.101 us | 1.088 us | 1.126 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.677 us | 0.155 us | 4.36x | 4.32–4.43x | +0.522 us | 9 pairs; 38/40 ms; 56,135/256,036 ops/lane; IQR 1.010/1.019 | `Pcre2Backtracking` | 0.045 us | 0.119 us | 0.152 us | 4.45x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.250 us | 0.555 us | 4.05x | 4.03–4.09x | +1.695 us | 9 pairs; 38/41 ms; 17,013/73,992 ops/lane; IQR 1.013/1.006 | `Pcre2Backtracking` | 0.122 us | 0.719 us | 0.742 us | 3.03x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.290 us | 0.34x | 0.34–0.34x | -0.191 us | 9 pairs; 39/40 ms; 398,850/136,709 ops/lane; IQR 1.012/1.007 | `Pcre2Slice` | 0.108 us | 0.307 us | 0.335 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.102 us | 0.346 us | 0.30x | 0.29–0.30x | -0.243 us | 9 pairs; 40/39 ms; 393,768/114,095 ops/lane; IQR 1.010/1.009 | `Pcre2Slice` | 0.111 us | 0.428 us | 0.451 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.074 us | 0.287 us | 0.26x | 0.26–0.26x | -0.213 us | 9 pairs; 38/40 ms; 510,567/138,722 ops/lane; IQR 1.008/1.005 | `Pcre2Slice` | 0.085 us | 0.228 us | 0.248 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.075 us | 0.309 us | 0.24x | 0.24–0.24x | -0.235 us | 9 pairs; 37/40 ms; 493,379/129,544 ops/lane; IQR 1.012/1.008 | `Pcre2Slice` | 0.086 us | 0.244 us | 0.264 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.168 us | 0.075 us | 2.23x | 2.19–2.27x | +0.092 us | 9 pairs; 40/40 ms; 240,463/526,781 ops/lane; IQR 1.008/1.018 | `Pcre2Slice` | 0.083 us | 0.944 us | 0.976 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.191 us | 0.18x | 0.18–0.18x | -0.156 us | 9 pairs; 44/40 ms; 1,265,747/211,110 ops/lane; IQR 1.008/1.014 | `Pcre2Backtracking` | 0.105 us | 0.293 us | 0.333 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.059 us | 0.598 us | 0.10x | 0.10–0.10x | -0.539 us | 9 pairs; 39/41 ms; 668,003/68,284 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0.126 us | 0.221 us | 0.252 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.147 us | 0.22x | 0.22–0.23x | -0.114 us | 9 pairs; 37/39 ms; 1,140,153/266,407 ops/lane; IQR 1.035/1.007 | `Pcre2Backtracking` | 0.099 us | 0.180 us | 0.200 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.109 us | 0.17x | 0.17–0.18x | -0.090 us | 9 pairs; 37/39 ms; 1,966,277/358,849 ops/lane; IQR 1.031/1.010 | `Pcre2Backtracking` | 0.057 us | 0.089 us | 0.107 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.056 us | 0.113 us | 0.50x | 0.49–0.51x | -0.057 us | 9 pairs; 40/41 ms; 704,309/365,504 ops/lane; IQR 1.015/1.034 | `Pcre2Backtracking` | 0.016 us | 0.083 us | 0.099 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Inconclusive** | 67 B | 0.081 us | 0.266 us | 0.31x | 0.30–0.31x | -0.184 us | 9 pairs; 37/40 ms; 458,389/149,906 ops/lane; IQR 1.010/1.021 | `Pcre2Slice` | 0.091 us | 0.115 us | 0.147 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.114 us | 0.22x | 0.21–0.22x | -0.089 us | 9 pairs; 42/39 ms; 1,697,989/342,967 ops/lane; IQR 1.024/1.010 | `Pcre2Backtracking` | 0.019 us | 0.089 us | 0.104 us | 0.23x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.115 us | 2.089 us | 0.05x | 0.05–0.06x | -1.975 us | 9 pairs; 41/38 ms; 354,956/18,344 ops/lane; IQR 1.003/1.118 | `Pcre2Literal` | 0.085 us | 0.050 us | 0.607 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.125 us | 1.734 us | 0.07x | 0.06–0.07x | -1.607 us | 9 pairs; 44/43 ms; 348,078/24,650 ops/lane; IQR 1.053/1.179 | `Pcre2Literal` | 0.144 us | 0.053 us | 0.609 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Managed faster** | 4,103 B | 0.150 us | 2.018 us | 0.07x | 0.07–0.08x | -1.865 us | 9 pairs; 39/40 ms; 262,042/19,651 ops/lane; IQR 1.012/1.037 | `Pcre2Literal` | 0.201 us | 0.227 us | 0.802 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.150 us | 1.765 us | 0.09x | 0.08–0.09x | -1.613 us | 9 pairs; 40/37 ms; 266,085/20,690 ops/lane; IQR 1.007/1.035 | `Pcre2Literal` | 0.178 us | 0.201 us | 0.767 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.009 us | 0.099 us | 0.09x | 0.09–0.15x | -0.089 us | 9 pairs; 38/42 ms; 4,423,251/421,144 ops/lane; IQR 1.632/1.016 | `Pcre2Slice` | 0.264 us | 0.068 us | 0.087 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.069 us | 0.084 us | 0.82x | 0.76–0.83x | -0.015 us | 9 pairs; 42/39 ms; 615,523/460,512 ops/lane; IQR 1.046/1.034 | `Pcre2Literal` | 0.125 us | 0.050 us | 0.072 us | 0.96x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.106 us | 0.13x | 0.12–0.13x | -0.093 us | 9 pairs; 41/40 ms; 3,053,749/377,141 ops/lane; IQR 1.011/1.014 | `Pcre2Backtracking` | 0.233 us | 0.088 us | 0.111 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.116 us | 0.118 us | 0.97x | 0.96–0.98x | -0.003 us | 9 pairs; 40/40 ms; 347,043/333,541 ops/lane; IQR 1.009/1.004 | `Pcre2Character` | 0.133 us | 0.109 us | 0.140 us | 0.82x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.074 us | 0.202 us | 0.36x | 0.36–0.41x | -0.129 us | 9 pairs; 37/43 ms; 495,373/210,929 ops/lane; IQR 1.089/1.032 | `Pcre2Slice` | 0.439 us | 0.073 us | 0.107 us | 0.69x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.686 us | 157.109 us | 0.51x | 0.51–0.52x | -76.379 us | 9 pairs; 40/41 ms; 490/261 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | 374.102 us | 97.259 us | 100.096 us | 0.81x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.025 us | 277.477 us | 0.13x | 0.13–0.13x | -240.499 us | 9 pairs; 41/41 ms; 1,096/146 ops/lane; IQR 1.008/1.003 | `Pcre2Character` | 375.989 us | 211.824 us | 220.367 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.234 us | 205.682 us | 0.04x | 0.04–0.05x | -196.534 us | 9 pairs; 39/39 ms; 4,235/190 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 7.585 us | 217.837 us | 218.672 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 237.802 us | 212.386 us | 1.12x | 1.10–1.13x | +25.300 us | 9 pairs; 39/41 ms; 166/195 ops/lane; IQR 1.010/1.009 | `Pcre2SingleTokenRepeat` | 7.578 us | 228.236 us | 230.550 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.488 us | 22.379 us | 0.16x | 0.15–0.16x | -18.891 us | 9 pairs; 39/39 ms; 11,169/1,745 ops/lane; IQR 1.007/1.033 | `Pcre2Literal` | 1.371 us | 4.141 us | 5.811 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.315 us | 79.164 us | 0.12x | 0.11–0.12x | -69.765 us | 9 pairs; 40/39 ms; 4,249/494 ops/lane; IQR 1.012/1.033 | `Pcre2Backtracking` | 9.137 us | 89.136 us | 92.003 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,100.262 us | 28,086.900 us | 0.15x | 0.14–0.16x | -23998.325 us | 9 pairs; 33/28 ms; 8/1 ops/lane; IQR 1.027/1.031 | `Pcre2Backtracking` | 5,319.870 us | 11,740.280 us | 18,437.067 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,362.150 us | 54,086.600 us | 0.06x | 0.06–0.07x | -50742.333 us | 9 pairs; 40/54 ms; 12/1 ops/lane; IQR 1.024/1.021 | `Pcre2Backtracking` | 3,814.079 us | 40,354.250 us | 47,072.750 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,907.771 us | 476,480.900 us | 0.00x | 0.00–0.00x | -474640.986 us | 9 pairs; 40/476 ms; 21/1 ops/lane; IQR 1.028/1.008 | `Pcre2Character` | 1,744.448 us | 22,263.433 us | 28,748.900 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,478.133 us | 11,145.250 us | 0.50x | 0.46–0.57x | -5572.667 us | 9 pairs; 33/45 ms; 6/4 ops/lane; IQR 1.019/1.126 | `Pcre2Literal` | 1,331.424 us | 1,366.433 us | 8,301.100 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,387.400 us | 140,726.100 us | 0.13x | 0.13–0.13x | -122338.700 us | 9 pairs; 37/141 ms; 2/1 ops/lane; IQR 1.016/1.009 | `Pcre2Backtracking` | 1,264.085 us | 521.379 us | 8,407.617 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,716.188 us | 21,575.750 us | 0.22x | 0.22–0.22x | -16851.488 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.008/1.005 | `Pcre2Slice` | 2,467.962 us | 10,808.600 us | 18,833.467 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,776.100 us | 131,463.200 us | 0.17x | 0.16–0.17x | -109687.100 us | 9 pairs; 44/131 ms; 2/1 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 1,531.803 us | 2,274.243 us | 10,086.300 us | 2.16x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 178.791 us | 969.336 us | 0.18x | 0.18–0.19x | -788.440 us | 9 pairs; 41/38 ms; 229/39 ops/lane; IQR 1.015/1.014 | `Pcre2Backtracking` | 165.819 us | 321.742 us | 534.764 us | 0.33x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,428.333 us | 13,094.267 us | 0.95x | 0.93–0.95x | -665.933 us | 9 pairs; 37/39 ms; 3/3 ops/lane; IQR 1.006/1.023 | `Pcre2Backtracking` | 297.506 us | 14,428.075 us | 14,849.150 us | 0.84x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Inconclusive** | 594,930 B | 2,699.243 us | 96,317.400 us | 0.03x | 0.03–0.03x | -93657.557 us | 9 pairs; 38/96 ms; 14/1 ops/lane; IQR 1.018/1.005 | `Pcre2Character` | 1,982.715 us | 19,767.800 us | 19,872.267 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 181.348 us | 2,755.200 us | 0.07x | 0.07–0.07x | -2573.895 us | 9 pairs; 40/39 ms; 220/14 ops/lane; IQR 1.009/1.013 | `Pcre2SingleTokenRepeat` | 180.929 us | 984.127 us | 1,226.603 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,535.073 us | 19,620.300 us | 0.08x | 0.08–0.08x | -18093.227 us | 9 pairs; 40/39 ms; 26/2 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 574.152 us | 10,662.200 us | 10,697.180 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.151 us | 1.075 us | 0.14x | 0.14–0.14x | -0.923 us | 9 pairs; 41/39 ms; 270,810/36,560 ops/lane; IQR 1.010/1.011 | `Pcre2Slice` | 0.696 us | 0.252 us | 0.274 us | 0.55x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.505 us | 0.813 us | 0.62x | 0.62–0.63x | -0.306 us | 9 pairs; 38/41 ms; 75,101/50,445 ops/lane; IQR 1.008/1.008 | `Pcre2Literal` | 0.138 us | 0.144 us | 0.168 us | 3.01x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.289 us | 1.147 us | 0.25x | 0.25–0.25x | -0.857 us | 9 pairs; 39/40 ms; 135,290/34,681 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 1.026 us | 0.336 us | 0.360 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.884 us | 1.107 us | 0.80x | 0.79–0.80x | -0.228 us | 9 pairs; 40/39 ms; 45,368/35,215 ops/lane; IQR 1.012/1.008 | `Pcre2Character` | 0.286 us | 0.411 us | 0.456 us | 1.94x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.157 us | 0.837 us | 0.19x | 0.19–0.19x | -0.681 us | 9 pairs; 41/41 ms; 263,197/48,571 ops/lane; IQR 1.016/1.005 | `Pcre2Slice` | 0.844 us | 0.152 us | 0.197 us | 0.80x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.880 us | 1.085 us | 0.81x | 0.81–0.81x | -0.207 us | 9 pairs; 40/40 ms; 45,796/37,009 ops/lane; IQR 1.017/1.015 | `Pcre2Slice` | 3.798 us | 0.301 us | 0.321 us | 2.75x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.780 us | 0.815 us | 0.95x | 0.95–0.96x | -0.037 us | 9 pairs; 39/40 ms; 50,568/48,974 ops/lane; IQR 1.006/1.007 | `Pcre2Literal` | 3.489 us | 0.177 us | 0.199 us | 3.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.961 us | 1.188 us | 0.81x | 0.80–0.81x | -0.227 us | 9 pairs; 38/40 ms; 39,041/33,461 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 5.156 us | 0.393 us | 0.413 us | 2.33x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.181 us | 1.115 us | 1.06x | 1.06–1.07x | +0.066 us | 9 pairs; 39/41 ms; 32,793/36,607 ops/lane; IQR 1.009/1.008 | `Pcre2Character` | 3.544 us | 0.465 us | 0.498 us | 2.37x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.780 us | 0.836 us | 0.93x | 0.93–0.94x | -0.056 us | 9 pairs; 39/39 ms; 50,282/47,173 ops/lane; IQR 1.009/1.008 | `Pcre2Slice` | 2.123 us | 0.179 us | 0.221 us | 3.53x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.588 us | 1.070 us | 0.55x | 0.55–0.55x | -0.484 us | 9 pairs; 41/40 ms; 70,239/36,986 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.484 us | 0.812 us | 0.60x | 0.59–0.60x | -0.328 us | 9 pairs; 39/41 ms; 80,302/49,990 ops/lane; IQR 1.005/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.682 us | 1.201 us | 0.57x | 0.57–0.57x | -0.521 us | 9 pairs; 40/40 ms; 58,873/33,126 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.871 us | 1.101 us | 0.79x | 0.78–0.80x | -0.230 us | 9 pairs; 38/40 ms; 44,166/35,944 ops/lane; IQR 1.006/1.005 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.473 us | 0.837 us | 0.57x | 0.56–0.57x | -0.363 us | 9 pairs; 41/39 ms; 87,402/47,023 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.170 us | — | — | — | — | — | `—` | 25.259 us | 94.483 us | 96.364 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.327 us | — | — | — | — | — | `—` | 1.751 us | 0.353 us | 0.384 us | 16.48x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 0.998 us | — | — | — | — | — | `—` | 0.927 us | 0.237 us | 0.269 us | 3.72x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.132 us | — | — | — | — | — | `—` | 3.069 us | 0.441 us | 0.475 us | 2.38x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.385 us | — | — | — | — | — | `—` | 1.024 us | 0.522 us | 0.570 us | 2.43x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.230 us | — | — | — | — | — | `—` | 1.536 us | 0.262 us | 0.306 us | 13.81x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.092 us | 0.082 us | 1.13x | 1.12–1.64x | +0.011 us | 9 pairs; 42/39 ms; 457,787/478,652 ops/lane; IQR 1.358/1.007 | `Pcre2Character` | 0 B | 0 B | 3.400 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.115 us | 0.11x | 0.11–0.11x | -0.102 us | 9 pairs; 38/40 ms; 3,017,659/350,235 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 128.144 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.384 us | 0.105 us | 3.65x | 3.64–3.68x | +0.278 us | 9 pairs; 36/40 ms; 92,758/378,244 ops/lane; IQR 1.011/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 20.606 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.371 us | 0.114 us | 3.25x | 3.24–3.29x | +0.256 us | 9 pairs; 34/41 ms; 92,905/357,261 ops/lane; IQR 1.016/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 69.944 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.376 us | 0.106 us | 3.54x | 3.53–3.57x | +0.269 us | 9 pairs; 34/40 ms; 91,637/381,074 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 67.875 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.555 us | 0.338 us | 1.64x | 1.64–1.66x | +0.218 us | 9 pairs; 37/40 ms; 65,765/119,127 ops/lane; IQR 1.012/1.009 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.619 us | 0.02x | 0.02–0.02x | -0.607 us | 9 pairs; 45/41 ms; 3,766,798/66,195 ops/lane; IQR 1.012/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 23.275 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.499 us | 0.02x | 0.02–0.02x | -0.488 us | 9 pairs; 37/39 ms; 3,373,797/78,317 ops/lane; IQR 1.018/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 22.837 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.460 us | 0.107 us | 4.30x | 4.25–4.32x | +0.353 us | 9 pairs; 36/39 ms; 77,789/368,274 ops/lane; IQR 1.003/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 19.788 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.114 us | 10.140 us | 0.80x | 0.79–0.81x | -2.000 us | 9 pairs; 39/41 ms; 4,866/4,075 ops/lane; IQR 1.007/1.013 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.288 us | 1.037 us | 0.28x | 0.28–0.28x | -0.749 us | 9 pairs; 40/41 ms; 139,293/39,103 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 128.463 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.313 us | 0.461 us | 0.68x | 0.68–0.68x | -0.148 us | 9 pairs; 35/38 ms; 111,038/83,405 ops/lane; IQR 1.008/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 109.138 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.284 us | 0.454 us | 0.63x | 0.62–0.63x | -0.170 us | 9 pairs; 36/42 ms; 128,562/92,333 ops/lane; IQR 1.010/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 141.881 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.299 us | 0.583 us | 0.51x | 0.51–0.52x | -0.284 us | 9 pairs; 34/40 ms; 113,415/69,236 ops/lane; IQR 1.006/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 102.856 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.294 us | 0.395 us | 0.75x | 0.74–0.76x | -0.100 us | 9 pairs; 34/38 ms; 114,336/96,245 ops/lane; IQR 1.009/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 211.569 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.904 us | 8.643 us | 0.57x | 0.57–0.57x | -3.734 us | 9 pairs; 42/40 ms; 8,467/4,686 ops/lane; IQR 1.005/1.002 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.258 us | 0.307 us | 0.84x | 0.83–0.85x | -0.049 us | 9 pairs; 36/40 ms; 140,089/131,778 ops/lane; IQR 1.008/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 89.806 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.263 us | 0.397 us | 0.66x | 0.66–0.67x | -0.134 us | 9 pairs; 35/38 ms; 134,864/95,389 ops/lane; IQR 1.014/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 119.056 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.809 us | — | — | — | — | — | `—` | 0 B | — | 13.425 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.182 us | 0.283 us | 0.64x | 0.64–0.64x | -0.102 us | 9 pairs; 40/39 ms; 220,748/138,332 ops/lane; IQR 1.009/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 53.938 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.688 us | — | — | — | — | — | `—` | 0 B | — | 14.137 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.567 us | 0.656 us | 0.86x | 0.86–0.87x | -0.093 us | 9 pairs; 37/37 ms; 65,863/56,370 ops/lane; IQR 1.011/1.010 | `Pcre2Slice` | 0 B | 0 B | 63.581 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 10.304 us | 28.344 us | 0.36x | 0.36–0.36x | -18.038 us | 9 pairs; 42/41 ms; 4,081/1,433 ops/lane; IQR 1.004/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.806 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.930 us | 1.056 us | 0.88x | 0.87–0.88x | -0.126 us | 9 pairs; 40/40 ms; 43,303/37,845 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 129.206 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.970 us | 1.163 us | 0.84x | 0.83–0.84x | -0.185 us | 9 pairs; 40/42 ms; 40,752/35,704 ops/lane; IQR 1.006/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 113.200 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.920 us | 1.012 us | 0.91x | 0.91–0.91x | -0.092 us | 9 pairs; 40/40 ms; 43,629/39,928 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 140.350 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.996 us | 1.151 us | 0.87x | 0.86–0.87x | -0.152 us | 9 pairs; 40/41 ms; 40,339/35,400 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 100.250 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.923 us | 0.920 us | 1.01x | 1.00–1.01x | +0.006 us | 9 pairs; 41/41 ms; 44,018/44,037 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 214.613 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.464 us | 8.874 us | 0.62x | 0.61–0.62x | -3.384 us | 9 pairs; 40/41 ms; 7,230/4,637 ops/lane; IQR 1.020/1.012 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.896 us | 0.858 us | 1.04x | 1.03–1.05x | +0.037 us | 9 pairs; 38/41 ms; 42,777/47,871 ops/lane; IQR 1.003/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 91.494 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.903 us | 0.952 us | 0.95x | 0.95–0.96x | -0.046 us | 9 pairs; 39/40 ms; 43,454/42,023 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 122.713 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.874 us | — | — | — | — | — | `—` | 0 B | — | 13.556 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.810 us | 0.843 us | 0.96x | 0.96–0.96x | -0.033 us | 9 pairs; 39/41 ms; 47,663/48,407 ops/lane; IQR 1.014/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.987 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.854 us | — | — | — | — | — | `—` | 0 B | — | 13.531 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.838 us | 1.221 us | 0.68x | 0.68–0.69x | -0.382 us | 9 pairs; 40/39 ms; 47,178/32,224 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0 B | 0 B | 64.269 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.343 us | 2.225 us | 0.60x | 0.60–0.61x | -0.879 us | 9 pairs; 41/41 ms; 30,603/18,604 ops/lane; IQR 1.010/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.825 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.617 us | 1.043 us | 0.59x | 0.59–0.59x | -0.425 us | 9 pairs; 40/40 ms; 65,536/37,884 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.869 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.715 us | 1.171 us | 0.61x | 0.60–0.61x | -0.457 us | 9 pairs; 41/39 ms; 57,555/33,235 ops/lane; IQR 1.012/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 113.575 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.644 us | 1.009 us | 0.64x | 0.63–0.64x | -0.368 us | 9 pairs; 39/40 ms; 60,029/39,858 ops/lane; IQR 1.012/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 139.856 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.653 us | 1.143 us | 0.57x | 0.57–0.57x | -0.491 us | 9 pairs; 37/39 ms; 57,233/33,816 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 102.081 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.650 us | 0.918 us | 0.71x | 0.70–0.71x | -0.271 us | 9 pairs; 38/42 ms; 58,245/45,338 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 211.256 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.764 us | 2.149 us | 0.82x | 0.82–0.83x | -0.388 us | 9 pairs; 39/40 ms; 22,008/18,711 ops/lane; IQR 1.008/1.010 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.633 us | 0.863 us | 0.74x | 0.73–0.74x | -0.228 us | 9 pairs; 41/38 ms; 64,181/44,567 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 93.662 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.597 us | 0.936 us | 0.64x | 0.63–0.64x | -0.340 us | 9 pairs; 38/41 ms; 63,786/43,886 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 121.525 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.731 us | — | — | — | — | — | `—` | 0 B | — | 13.519 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.480 us | 0.843 us | 0.57x | 0.57–0.57x | -0.363 us | 9 pairs; 40/40 ms; 82,999/47,946 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 53.606 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.648 us | — | — | — | — | — | `—` | 0 B | — | 14.181 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.584 us | 1.231 us | 0.47x | 0.47–0.48x | -0.647 us | 9 pairs; 40/40 ms; 68,241/32,743 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | 0 B | 0 B | 64.987 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 16.625 us | — | — | — | — | — | `—` | 480 B | — | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.072 us | — | — | — | — | — | `—` | 48 B | — | 129.606 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.175 us | — | — | — | — | — | `—` | 48 B | — | 113.806 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.069 us | — | — | — | — | — | `—` | 48 B | — | 140.850 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.077 us | — | — | — | — | — | `—` | 48 B | — | 99.312 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.048 us | — | — | — | — | — | `—` | 48 B | — | 215.919 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.635 us | — | — | — | — | — | `—` | 64 B | — | 1.475 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.065 us | — | — | — | — | — | `—` | 40 B | — | 92.912 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.068 us | — | — | — | — | — | `—` | 40 B | — | 120.494 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.659 us | — | — | — | — | — | `—` | 1,216 B | — | 13.431 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 0.986 us | — | — | — | — | — | `—` | 48 B | — | 53.575 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.598 us | — | — | — | — | — | `—` | 928 B | — | 13.662 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.736 us | — | — | — | — | — | `—` | 28,928 B | — | 62.525 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 27.612–216.547 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.040–91.732 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.105–12.410 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.937–47.535 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.034–1.319 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.161–40.109 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.968–22.774 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.146–97.290 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.875–70.683 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.197–5.379 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.640–363.125 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.066–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 37.577–298.574 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.877–4.986 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.119–15.061 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 163.301–1,307.249 us |

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
