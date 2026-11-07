<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `B26E6012EA4A17458F61B6B49B34B42549673C4637B35132A7AB6EE364B3911F`
- Latest managed row measurement: `2026-11-07T21:07:29.9517706+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-07T21:07:29.9517706+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `1` equivalent, `13` native faster, `11` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x1`, `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `d60679252cf6` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `d60679252cf6` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.609 us | 2.291 us | 0.26x | 0.23–0.30x | -1.688 us | 9 pairs; 35/39 ms; 57,263/16,992 ops/lane; IQR 1.022/1.155 | `Pcre2Slice` | 456.634 us | 0.275 us | 0.813 us | 0.75x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.694 us | 10.640 us | 0.07x | 0.06–0.07x | -9.945 us | 9 pairs; 33/35 ms; 47,014/3,307 ops/lane; IQR 1.017/1.095 | `Pcre2Slice` | 0.566 us | 0.096 us | 3.590 us | 0.19x | 64 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.212 us | 0.01x | 0.01–0.01x | -1.201 us | 9 pairs; 39/41 ms; 3,618,608/34,121 ops/lane; IQR 1.010/1.005 | `Pcre2Slice` | 0.105 us | 1.281 us | 1.282 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.657 us | 0.156 us | 4.21x | 4.18–4.26x | +0.502 us | 9 pairs; 37/40 ms; 56,274/257,691 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0.048 us | 0.121 us | 0.151 us | 4.36x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.312 us | 0.565 us | 4.10x | 4.06–4.13x | +1.744 us | 9 pairs; 39/40 ms; 16,919/71,586 ops/lane; IQR 1.010/1.010 | `Pcre2Backtracking` | 0.116 us | 0.768 us | 0.798 us | 2.90x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.296 us | 0.33x | 0.33–0.33x | -0.198 us | 9 pairs; 36/39 ms; 363,878/131,765 ops/lane; IQR 1.011/1.005 | `Pcre2Slice` | 0.108 us | 0.321 us | 0.346 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.348 us | 0.30x | 0.30–0.30x | -0.243 us | 9 pairs; 40/41 ms; 378,649/116,265 ops/lane; IQR 1.009/1.005 | `Pcre2Slice` | 0.113 us | 0.451 us | 0.472 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Inconclusive** | 15 B | 0.090 us | 0.287 us | 0.31x | 0.27–0.32x | -0.198 us | 9 pairs; 43/42 ms; 474,774/146,780 ops/lane; IQR 1.116/1.015 | `Pcre2Slice` | 0.087 us | 0.238 us | 0.261 us | 0.34x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.078 us | 0.311 us | 0.25x | 0.25–0.25x | -0.233 us | 9 pairs; 41/39 ms; 526,020/124,840 ops/lane; IQR 1.008/1.016 | `Pcre2Slice` | 0.087 us | 0.256 us | 0.276 us | 0.28x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.170 us | 0.077 us | 2.21x | 2.18–2.26x | +0.093 us | 9 pairs; 40/41 ms; 234,033/531,689 ops/lane; IQR 1.009/1.016 | `Pcre2Slice` | 0.093 us | 1.029 us | 1.047 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.197 us | 0.17x | 0.17–0.17x | -0.163 us | 9 pairs; 39/41 ms; 1,183,638/207,813 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0.106 us | 0.313 us | 0.354 us | 0.09x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.596 us | 0.10x | 0.10–0.10x | -0.536 us | 9 pairs; 40/40 ms; 664,429/67,577 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0.131 us | 0.236 us | 0.262 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.032 us | 0.151 us | 0.21x | 0.21–0.22x | -0.119 us | 9 pairs; 38/40 ms; 1,164,249/265,433 ops/lane; IQR 1.021/1.021 | `Pcre2Backtracking` | 0.108 us | 0.181 us | 0.200 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.110 us | 0.19x | 0.18–0.19x | -0.090 us | 9 pairs; 40/41 ms; 1,946,256/375,841 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0.061 us | 0.092 us | 0.110 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Inconclusive** | 8 B | 0.057 us | 0.123 us | 0.47x | 0.46–0.48x | -0.065 us | 9 pairs; 40/37 ms; 697,471/303,754 ops/lane; IQR 1.008/1.025 | `Pcre2Backtracking` | 0.014 us | 0.091 us | 0.111 us | 0.52x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.087 us | 0.272 us | 0.32x | 0.31–0.33x | -0.186 us | 9 pairs; 42/40 ms; 484,002/147,467 ops/lane; IQR 1.060/1.011 | `Pcre2Slice` | 0.090 us | 0.114 us | 0.152 us | 0.58x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Inconclusive** | 7 B | 0.023 us | 0.113 us | 0.21x | 0.20–0.21x | -0.089 us | 9 pairs; 44/41 ms; 1,862,612/363,335 ops/lane; IQR 1.020/1.023 | `Pcre2Backtracking` | 0.019 us | 0.095 us | 0.111 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Managed faster** | 4,103 B | 0.116 us | 1.808 us | 0.06x | 0.06–0.07x | -1.693 us | 9 pairs; 41/42 ms; 356,627/23,258 ops/lane; IQR 1.003/1.023 | `Pcre2Literal` | 0.087 us | 0.049 us | 0.565 us | 0.21x | 0 B | 0 B |
| `literal/early` | **Managed faster** | 4,103 B | 0.127 us | 1.693 us | 0.07x | 0.06–0.08x | -1.569 us | 9 pairs; 42/43 ms; 331,861/25,173 ops/lane; IQR 1.019/1.038 | `Pcre2Literal` | 0.147 us | 0.053 us | 0.592 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Managed faster** | 4,103 B | 0.154 us | 1.976 us | 0.08x | 0.07–0.08x | -1.821 us | 9 pairs; 40/38 ms; 258,789/19,169 ops/lane; IQR 1.015/1.059 | `Pcre2Literal` | 0.206 us | 0.228 us | 0.771 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.150 us | 2.168 us | 0.07x | 0.06–0.08x | -2.019 us | 9 pairs; 41/48 ms; 274,556/22,294 ops/lane; IQR 1.010/1.198 | `Pcre2Literal` | 0.184 us | 0.201 us | 0.717 us | 0.21x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.098 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 40/39 ms; 4,628,249/399,756 ops/lane; IQR 1.010/1.010 | `Pcre2Slice` | 0.219 us | 0.070 us | 0.089 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.066 us | 0.086 us | 0.77x | 0.76–0.78x | -0.020 us | 9 pairs; 41/39 ms; 612,665/457,173 ops/lane; IQR 1.010/1.015 | `Pcre2Literal` | 0.132 us | 0.051 us | 0.072 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.107 us | 0.12x | 0.12–0.12x | -0.094 us | 9 pairs; 38/40 ms; 2,894,806/372,194 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 0.276 us | 0.091 us | 0.113 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.112 us | 0.119 us | 0.94x | 0.93–0.95x | -0.007 us | 9 pairs; 38/40 ms; 343,601/338,133 ops/lane; IQR 1.005/1.035 | `Pcre2Character` | 0.134 us | 0.111 us | 0.141 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.073 us | 0.199 us | 0.37x | 0.37–0.37x | -0.125 us | 9 pairs; 39/40 ms; 532,490/201,845 ops/lane; IQR 1.011/1.017 | `Pcre2Slice` | 0.338 us | 0.074 us | 0.105 us | 0.70x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 76.301 us | 156.708 us | 0.49x | 0.49–0.50x | -79.883 us | 9 pairs; 39/39 ms; 511/249 ops/lane; IQR 1.015/1.025 | `Pcre2Slice` | 328.921 us | 107.812 us | 107.702 us | 0.71x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.364 us | 306.611 us | 0.13x | 0.13–0.13x | -267.217 us | 9 pairs; 41/43 ms; 1,037/141 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 282.979 us | 216.567 us | 226.750 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.487 us | 206.966 us | 0.05x | 0.05–0.05x | -197.557 us | 9 pairs; 40/41 ms; 4,170/198 ops/lane; IQR 1.013/1.007 | `Pcre2Backtracking` | 7.715 us | 221.067 us | 225.120 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 236.579 us | 203.996 us | 1.16x | 1.15–1.18x | +32.112 us | 9 pairs; 39/39 ms; 165/190 ops/lane; IQR 1.005/1.016 | `Pcre2SingleTokenRepeat` | 7.835 us | 233.491 us | 235.950 us | 1.00x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.342 us | 23.522 us | 0.14x | 0.14–0.14x | -20.172 us | 9 pairs; 40/41 ms; 11,824/1,756 ops/lane; IQR 1.007/1.016 | `Pcre2Literal` | 1.339 us | 4.124 us | 5.887 us | 0.57x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.413 us | 80.108 us | 0.12x | 0.12–0.12x | -70.735 us | 9 pairs; 39/40 ms; 4,179/496 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 9.057 us | 92.720 us | 93.677 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,098.038 us | 28,404.500 us | 0.15x | 0.14–0.15x | -24226.625 us | 9 pairs; 33/28 ms; 8/1 ops/lane; IQR 1.007/1.030 | `Pcre2Backtracking` | 5,291.527 us | 12,642.200 us | 19,110.450 us | 0.21x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,329.842 us | 54,511.900 us | 0.06x | 0.06–0.06x | -51222.983 us | 9 pairs; 40/55 ms; 12/1 ops/lane; IQR 1.014/1.008 | `Pcre2Backtracking` | 3,830.425 us | 43,993.750 us | 50,728.500 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,962.276 us | 486,816.900 us | 0.00x | 0.00–0.00x | -484915.705 us | 9 pairs; 41/487 ms; 21/1 ops/lane; IQR 1.026/1.005 | `Pcre2Character` | 1,746.958 us | 22,863.125 us | 29,592.967 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,274.850 us | 11,716.100 us | 0.45x | 0.44–0.49x | -6389.200 us | 9 pairs; 32/35 ms; 6/3 ops/lane; IQR 1.009/1.058 | `Pcre2Literal` | 1,266.444 us | 1,300.008 us | 8,392.450 us | 0.63x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,636.750 us | 143,405.300 us | 0.13x | 0.13–0.13x | -124768.550 us | 9 pairs; 37/143 ms; 2/1 ops/lane; IQR 1.020/1.004 | `Pcre2Backtracking` | 1,211.024 us | 501.919 us | 8,340.044 us | 2.23x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,826.500 us | 21,550.700 us | 0.22x | 0.22–0.23x | -16730.875 us | 9 pairs; 39/43 ms; 8/2 ops/lane; IQR 1.008/1.009 | `Pcre2Slice` | 2,326.688 us | 11,372.700 us | 19,326.150 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,024.000 us | 131,611.400 us | 0.17x | 0.17–0.17x | -109499.100 us | 9 pairs; 44/132 ms; 2/1 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 1,495.627 us | 2,256.500 us | 10,342.337 us | 2.13x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 135.798 us | 991.373 us | 0.14x | 0.14–0.14x | -856.151 us | 9 pairs; 40/40 ms; 292/40 ops/lane; IQR 1.014/1.006 | `Pcre2Backtracking` | 167.090 us | 350.569 us | 605.472 us | 0.22x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,315.667 us | 13,320.800 us | 0.92x | 0.92–0.93x | -1022.600 us | 9 pairs; 37/40 ms; 3/3 ops/lane; IQR 1.012/1.010 | `Pcre2Backtracking` | 298.853 us | 16,140.200 us | 16,291.980 us | 0.76x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,792.550 us | 97,018.500 us | 0.03x | 0.03–0.03x | -94229.814 us | 9 pairs; 39/97 ms; 14/1 ops/lane; IQR 1.005/1.006 | `Pcre2Character` | 2,000.600 us | 19,629.200 us | 19,758.950 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.381 us | 2,773.821 us | 0.07x | 0.07–0.07x | -2589.858 us | 9 pairs; 39/39 ms; 214/14 ops/lane; IQR 1.014/1.012 | `Pcre2SingleTokenRepeat` | 188.261 us | 992.839 us | 1,242.862 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Inconclusive** | 594,930 B | 1,557.377 us | 19,743.850 us | 0.08x | 0.08–0.08x | -18210.673 us | 9 pairs; 40/39 ms; 26/2 ops/lane; IQR 1.020/1.006 | `Pcre2Backtracking` | 549.996 us | 10,640.075 us | 10,928.100 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.141 us | 1.083 us | 0.13x | 0.13–0.13x | -0.942 us | 9 pairs; 41/39 ms; 288,885/36,380 ops/lane; IQR 1.010/1.007 | `Pcre2Slice` | 0.597 us | 0.255 us | 0.276 us | 0.51x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.485 us | 0.812 us | 0.60x | 0.60–0.60x | -0.326 us | 9 pairs; 41/40 ms; 84,534/48,830 ops/lane; IQR 1.009/1.008 | `Pcre2Literal` | 0.135 us | 0.145 us | 0.168 us | 2.89x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.286 us | 1.160 us | 0.25x | 0.25–0.25x | -0.874 us | 9 pairs; 39/40 ms; 135,783/34,596 ops/lane; IQR 1.005/1.013 | `Pcre2Backtracking` | 1.028 us | 0.344 us | 0.370 us | 0.77x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.852 us | 1.106 us | 0.77x | 0.76–0.77x | -0.253 us | 9 pairs; 39/41 ms; 45,645/36,707 ops/lane; IQR 1.014/1.014 | `Pcre2Character` | 0.262 us | 0.420 us | 0.460 us | 1.85x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.148 us | 0.827 us | 0.18x | 0.18–0.18x | -0.679 us | 9 pairs; 40/40 ms; 270,338/48,925 ops/lane; IQR 1.010/1.005 | `Pcre2Slice` | 0.651 us | 0.154 us | 0.198 us | 0.75x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.937 us | 1.092 us | 0.86x | 0.85–0.86x | -0.154 us | 9 pairs; 40/41 ms; 42,997/37,861 ops/lane; IQR 1.004/1.002 | `Pcre2Slice` | 2.297 us | 0.300 us | 0.318 us | 2.95x | 0 B | 0 B |
| `simple/foo-dense` | **Inconclusive** | 18 B | 0.790 us | 0.810 us | 0.97x | 0.96–0.98x | -0.023 us | 9 pairs; 41/41 ms; 52,174/50,321 ops/lane; IQR 1.014/1.016 | `Pcre2Literal` | 3.549 us | 0.176 us | 0.198 us | 3.99x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.046 us | 1.172 us | 0.89x | 0.89–0.90x | -0.129 us | 9 pairs; 43/40 ms; 41,133/34,516 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 5.145 us | 0.397 us | 0.421 us | 2.49x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.302 us | 1.111 us | 1.17x | 1.16–1.18x | +0.185 us | 9 pairs; 41/40 ms; 31,695/36,045 ops/lane; IQR 1.009/1.009 | `Pcre2Character` | 3.593 us | 0.469 us | 0.504 us | 2.58x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.778 us | 0.848 us | 0.92x | 0.91–0.92x | -0.070 us | 9 pairs; 39/41 ms; 50,112/47,769 ops/lane; IQR 1.012/1.008 | `Pcre2Slice` | 1.949 us | 0.178 us | 0.216 us | 3.61x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.599 us | 1.096 us | 0.55x | 0.55–0.55x | -0.498 us | 9 pairs; 39/41 ms; 64,624/37,071 ops/lane; IQR 1.004/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.497 us | 0.815 us | 0.61x | 0.61–0.61x | -0.316 us | 9 pairs; 41/41 ms; 82,186/49,807 ops/lane; IQR 1.009/1.009 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.706 us | 1.174 us | 0.60x | 0.60–0.60x | -0.465 us | 9 pairs; 40/40 ms; 56,200/34,273 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.905 us | 1.110 us | 0.82x | 0.81–0.82x | -0.203 us | 9 pairs; 40/41 ms; 44,259/36,775 ops/lane; IQR 1.007/1.010 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.491 us | 0.851 us | 0.58x | 0.57–0.58x | -0.358 us | 9 pairs; 39/41 ms; 79,422/48,306 ops/lane; IQR 1.016/1.009 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 59.882 us | — | — | — | — | — | `—` | 25.300 us | 97.297 us | 99.103 us | 0.60x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.569 us | — | — | — | — | — | `—` | 1.462 us | 0.357 us | 0.381 us | 9.37x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.035 us | — | — | — | — | — | `—` | 0.989 us | 0.239 us | 0.267 us | 3.87x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.206 us | — | — | — | — | — | `—` | 3.081 us | 0.455 us | 0.488 us | 2.47x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.474 us | — | — | — | — | — | `—` | 1.101 us | 0.529 us | 0.575 us | 2.56x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.507 us | — | — | — | — | — | `—` | 1.438 us | 0.262 us | 0.311 us | 11.29x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.085 us | 0.081 us | 1.04x | 1.02–1.05x | +0.004 us | 9 pairs; 38/37 ms; 446,594/457,767 ops/lane; IQR 1.006/1.027 | `Pcre2Character` | 0 B | 0 B | 2.244 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.012 us | 0.115 us | 0.11x | 0.11–0.11x | -0.103 us | 9 pairs; 40/40 ms; 3,269,036/349,691 ops/lane; IQR 1.017/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 129.463 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.390 us | 0.103 us | 3.77x | 3.73–3.79x | +0.287 us | 9 pairs; 36/39 ms; 91,867/377,620 ops/lane; IQR 1.007/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 21.163 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.388 us | 0.114 us | 3.40x | 3.37–3.42x | +0.274 us | 9 pairs; 36/41 ms; 91,983/363,454 ops/lane; IQR 1.009/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 70.400 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.387 us | 0.106 us | 3.66x | 3.63–3.67x | +0.281 us | 9 pairs; 35/39 ms; 91,310/369,964 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 67.487 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.813 us | 0.341 us | 2.39x | 2.34–2.43x | +0.473 us | 9 pairs; 35/39 ms; 43,095/114,323 ops/lane; IQR 1.012/1.011 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.617 us | 0.02x | 0.02–0.02x | -0.606 us | 9 pairs; 40/40 ms; 3,544,083/64,347 ops/lane; IQR 1.016/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 21.519 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.502 us | 0.02x | 0.02–0.02x | -0.490 us | 9 pairs; 40/39 ms; 3,352,940/77,738 ops/lane; IQR 1.032/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 21.131 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.461 us | 0.107 us | 4.31x | 4.27–4.35x | +0.354 us | 9 pairs; 36/42 ms; 78,486/397,451 ops/lane; IQR 1.006/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 18.550 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.243 us | 10.100 us | 0.81x | 0.81–0.82x | -1.916 us | 9 pairs; 41/40 ms; 4,966/3,949 ops/lane; IQR 1.008/1.008 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.293 us | 1.031 us | 0.28x | 0.28–0.29x | -0.737 us | 9 pairs; 41/42 ms; 139,462/40,658 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 130.312 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.310 us | 0.460 us | 0.67x | 0.65–0.68x | -0.150 us | 9 pairs; 37/40 ms; 119,708/87,127 ops/lane; IQR 1.003/1.036 | `Pcre2Backtracking` | 0 B | 0 B | 113.112 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.284 us | 0.478 us | 0.59x | 0.59–0.60x | -0.194 us | 9 pairs; 36/37 ms; 126,181/78,205 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 139.631 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.294 us | 0.574 us | 0.51x | 0.51–0.51x | -0.283 us | 9 pairs; 36/40 ms; 120,957/69,406 ops/lane; IQR 1.009/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 98.612 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.283 us | 0.374 us | 0.76x | 0.76–0.77x | -0.088 us | 9 pairs; 33/42 ms; 115,481/111,862 ops/lane; IQR 1.004/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 212.744 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.927 us | 8.657 us | 0.57x | 0.57–0.57x | -3.735 us | 9 pairs; 40/40 ms; 8,206/4,589 ops/lane; IQR 1.006/1.005 | `Pcre2Character` | 0 B | 0 B | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.258 us | 0.310 us | 0.83x | 0.83–0.84x | -0.052 us | 9 pairs; 37/39 ms; 143,095/125,458 ops/lane; IQR 1.003/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 89.656 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.263 us | 0.387 us | 0.69x | 0.67–0.69x | -0.121 us | 9 pairs; 37/41 ms; 139,157/106,628 ops/lane; IQR 1.012/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 119.013 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.795 us | — | — | — | — | — | `—` | 0 B | — | 15.906 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.183 us | 0.281 us | 0.66x | 0.64–0.66x | -0.096 us | 9 pairs; 40/38 ms; 215,583/136,768 ops/lane; IQR 1.005/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 53.131 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.661 us | — | — | — | — | — | `—` | 0 B | — | 13.650 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.496 us | 0.662 us | 0.75x | 0.74–0.75x | -0.169 us | 9 pairs; 35/41 ms; 71,496/61,229 ops/lane; IQR 1.005/1.009 | `Pcre2Slice` | 0 B | 0 B | 64.562 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.775 us | 30.264 us | 0.32x | 0.32–0.32x | -20.565 us | 9 pairs; 33/40 ms; 3,363/1,330 ops/lane; IQR 1.006/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.951 us | 1.060 us | 0.89x | 0.89–0.91x | -0.112 us | 9 pairs; 40/39 ms; 42,544/36,944 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 128.713 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.993 us | 1.169 us | 0.85x | 0.84–0.86x | -0.173 us | 9 pairs; 39/40 ms; 39,377/33,825 ops/lane; IQR 1.013/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 112.875 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.924 us | 1.015 us | 0.91x | 0.91–0.92x | -0.087 us | 9 pairs; 42/41 ms; 45,122/39,996 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 140.656 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.968 us | 1.139 us | 0.85x | 0.85–0.86x | -0.168 us | 9 pairs; 38/40 ms; 39,341/35,292 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 113.706 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.980 us | 0.944 us | 1.04x | 1.03–1.04x | +0.034 us | 9 pairs; 40/42 ms; 40,685/44,071 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 212.100 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.638 us | 8.636 us | 0.65x | 0.65–0.66x | -2.972 us | 9 pairs; 39/41 ms; 6,923/4,771 ops/lane; IQR 1.006/1.005 | `Pcre2Character` | 0 B | 0 B | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.910 us | 0.875 us | 1.04x | 1.04–1.05x | +0.034 us | 9 pairs; 40/41 ms; 43,686/47,093 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 90.500 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Equivalent** | 26 B | 0.958 us | 0.951 us | 1.00x | 0.99–1.02x | -0.003 us | 9 pairs; 41/41 ms; 42,925/43,488 ops/lane; IQR 1.007/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 122.375 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.033 us | — | — | — | — | — | `—` | 0 B | — | 15.769 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.813 us | 0.841 us | 0.97x | 0.97–0.98x | -0.024 us | 9 pairs; 41/40 ms; 50,503/48,128 ops/lane; IQR 1.011/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 51.325 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.843 us | — | — | — | — | — | `—` | 0 B | — | 13.981 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.834 us | 1.228 us | 0.68x | 0.67–0.68x | -0.397 us | 9 pairs; 41/39 ms; 49,178/31,890 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | 0 B | 0 B | 63.825 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.456 us | 2.240 us | 0.65x | 0.65–0.65x | -0.786 us | 9 pairs; 41/40 ms; 28,218/17,687 ops/lane; IQR 1.004/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.725 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.621 us | 1.058 us | 0.59x | 0.58–0.59x | -0.438 us | 9 pairs; 38/40 ms; 61,969/37,569 ops/lane; IQR 1.014/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 126.438 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.663 us | 1.166 us | 0.57x | 0.57–0.58x | -0.499 us | 9 pairs; 38/41 ms; 56,713/34,913 ops/lane; IQR 1.006/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 113.362 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.593 us | 1.006 us | 0.59x | 0.59–0.59x | -0.413 us | 9 pairs; 35/40 ms; 59,752/39,777 ops/lane; IQR 1.010/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 140.488 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.628 us | 1.154 us | 0.54x | 0.54–0.55x | -0.526 us | 9 pairs; 40/41 ms; 62,972/35,843 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 101.412 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.645 us | 0.949 us | 0.68x | 0.68–0.69x | -0.300 us | 9 pairs; 39/39 ms; 60,514/41,605 ops/lane; IQR 1.012/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 215.175 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.995 us | 2.161 us | 0.92x | 0.92–0.93x | -0.165 us | 9 pairs; 39/40 ms; 19,463/18,733 ops/lane; IQR 1.011/1.006 | `Pcre2Character` | 0 B | 0 B | 1.369 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.593 us | 0.884 us | 0.67x | 0.67–0.67x | -0.290 us | 9 pairs; 38/41 ms; 63,672/46,699 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 91.456 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.593 us | 0.955 us | 0.62x | 0.62–0.62x | -0.361 us | 9 pairs; 39/39 ms; 64,957/41,289 ops/lane; IQR 1.013/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 120.975 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.682 us | — | — | — | — | — | `—` | 0 B | — | 13.569 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.481 us | 0.849 us | 0.57x | 0.57–0.57x | -0.367 us | 9 pairs; 38/41 ms; 77,997/48,768 ops/lane; IQR 1.007/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 53.456 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.599 us | — | — | — | — | — | `—` | 0 B | — | 14.219 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.514 us | 1.235 us | 0.42x | 0.42–0.42x | -0.721 us | 9 pairs; 40/40 ms; 77,054/32,481 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0 B | 0 B | 63.825 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.132 us | — | — | — | — | — | `—` | 480 B | — | 0.719 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.130 us | — | — | — | — | — | `—` | 48 B | — | 127.081 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.231 us | — | — | — | — | — | `—` | 48 B | — | 109.906 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.105 us | — | — | — | — | — | `—` | 48 B | — | 137.219 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.135 us | — | — | — | — | — | `—` | 48 B | — | 98.362 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.109 us | — | — | — | — | — | `—` | 48 B | — | 215.856 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.766 us | — | — | — | — | — | `—` | 64 B | — | 2.056 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.144 us | — | — | — | — | — | `—` | 40 B | — | 91.287 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.140 us | — | — | — | — | — | `—` | 40 B | — | 122.200 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.617 us | — | — | — | — | — | `—` | 1,216 B | — | 13.363 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.064 us | — | — | — | — | — | `—` | 48 B | — | 54.181 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.458 us | — | — | — | — | — | `—` | 928 B | — | 14.419 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.515 us | — | — | — | — | — | `—` | 4,496 B | — | 62.025 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 30.651–239.598 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.168–94.159 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.912–14.818 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.977–48.269 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.033–1.140 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.421–42.266 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.043–19.925 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.116–98.108 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.976–71.208 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.163–5.517 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.213–362.366 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.551–301.467 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.838–5.020 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.041–14.977 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 101.949–839.302 us |

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
