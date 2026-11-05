<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `B80528812187F63C37E8CB598DBE4F0ABBA223BBAE7D998FA54D95C0DAE46393`
- Latest managed row measurement: `2026-11-05T08:17:20.3977680+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T08:17:20.3977680+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `78` managed faster, `1` equivalent, `12` native faster, `10` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `7f152e64a646` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `7f152e64a646` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.875 us | 2.446 us | 0.38x | 0.33–0.40x | -1.516 us | 9 pairs; 33/43 ms; 37,980/17,560 ops/lane; IQR 1.069/1.182 | `Pcre2Slice` | 564.285 us | 0.282 us | 0.865 us | 1.01x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.146 us | 11.718 us | 0.10x | 0.08–0.11x | -10.566 us | 9 pairs; 34/45 ms; 29,243/3,867 ops/lane; IQR 1.015/1.268 | `Pcre2Slice` | 0.838 us | 0.097 us | 3.609 us | 0.32x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.230 us | 0.01x | 0.01–0.01x | -1.220 us | 9 pairs; 39/39 ms; 3,652,218/31,969 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 0.101 us | 1.061 us | 1.106 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.700 us | 0.158 us | 4.42x | 4.38–4.47x | +0.541 us | 9 pairs; 37/38 ms; 52,612/243,942 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 0.046 us | 0.124 us | 0.159 us | 4.40x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.362 us | 0.572 us | 4.14x | 4.10–4.16x | +1.787 us | 9 pairs; 40/41 ms; 17,099/71,684 ops/lane; IQR 1.001/1.011 | `Pcre2Backtracking` | 0.115 us | 0.751 us | 0.793 us | 2.98x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.299 us | 0.33x | 0.33–0.34x | -0.199 us | 9 pairs; 39/40 ms; 392,979/135,277 ops/lane; IQR 1.002/1.007 | `Pcre2Slice` | 0.110 us | 0.318 us | 0.348 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.358 us | 0.30x | 0.29–0.30x | -0.252 us | 9 pairs; 40/39 ms; 374,364/110,206 ops/lane; IQR 1.003/1.010 | `Pcre2Slice` | 0.114 us | 0.442 us | 0.471 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.289 us | 0.28x | 0.27–0.28x | -0.208 us | 9 pairs; 42/40 ms; 525,085/136,846 ops/lane; IQR 1.003/1.008 | `Pcre2Slice` | 0.086 us | 0.240 us | 0.263 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.094 us | 0.315 us | 0.30x | 0.30–0.30x | -0.222 us | 9 pairs; 42/40 ms; 450,145/126,024 ops/lane; IQR 1.017/1.003 | `Pcre2Slice` | 0.086 us | 0.255 us | 0.279 us | 0.34x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.172 us | 0.078 us | 2.21x | 2.20–2.42x | +0.094 us | 9 pairs; 40/41 ms; 230,657/521,644 ops/lane; IQR 1.056/1.010 | `Pcre2Slice` | 0.084 us | 0.935 us | 0.968 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.200 us | 0.17x | 0.17–0.17x | -0.166 us | 9 pairs; 40/39 ms; 1,187,469/193,066 ops/lane; IQR 1.001/1.012 | `Pcre2Backtracking` | 0.105 us | 0.307 us | 0.346 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.607 us | 0.10x | 0.10–0.10x | -0.546 us | 9 pairs; 40/40 ms; 658,486/65,267 ops/lane; IQR 1.002/1.010 | `Pcre2Backtracking` | 0.128 us | 0.239 us | 0.259 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.157 us | 0.21x | 0.21–0.22x | -0.123 us | 9 pairs; 38/40 ms; 1,141,458/253,373 ops/lane; IQR 1.022/1.005 | `Pcre2Backtracking` | 0.105 us | 0.185 us | 0.205 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.114 us | 0.17x | 0.17–0.18x | -0.094 us | 9 pairs; 38/42 ms; 1,949,026/365,808 ops/lane; IQR 1.022/1.009 | `Pcre2Backtracking` | 0.059 us | 0.092 us | 0.111 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.118 us | 0.49x | 0.49–0.50x | -0.060 us | 9 pairs; 40/40 ms; 690,546/340,471 ops/lane; IQR 1.003/1.016 | `Pcre2Backtracking` | 0.015 us | 0.090 us | 0.108 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.084 us | 0.274 us | 0.31x | 0.30–0.31x | -0.190 us | 9 pairs; 39/39 ms; 467,674/143,417 ops/lane; IQR 1.008/1.018 | `Pcre2Slice` | 0.091 us | 0.121 us | 0.158 us | 0.53x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.115 us | 0.22x | 0.19–0.22x | -0.090 us | 9 pairs; 44/40 ms; 1,765,398/349,009 ops/lane; IQR 1.056/1.018 | `Pcre2Backtracking` | 0.020 us | 0.096 us | 0.113 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 1.995 us | 0.06x | 0.05–0.06x | -1.877 us | 9 pairs; 41/48 ms; 352,322/24,183 ops/lane; IQR 1.000/1.184 | `Pcre2Literal` | 0.088 us | 0.052 us | 0.642 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 2.041 us | 0.06x | 0.05–0.07x | -1.919 us | 9 pairs; 40/46 ms; 328,886/22,437 ops/lane; IQR 1.002/1.246 | `Pcre2Literal` | 0.144 us | 0.054 us | 0.617 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.154 us | 2.180 us | 0.07x | 0.06–0.07x | -2.023 us | 9 pairs; 39/39 ms; 255,288/17,991 ops/lane; IQR 1.003/1.139 | `Pcre2Literal` | 0.206 us | 0.236 us | 0.831 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.915 us | 0.08x | 0.07–0.09x | -1.763 us | 9 pairs; 39/34 ms; 258,329/17,674 ops/lane; IQR 1.001/1.109 | `Pcre2Literal` | 0.178 us | 0.205 us | 0.815 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.101 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 40/40 ms; 4,513,693/396,024 ops/lane; IQR 1.001/1.009 | `Pcre2Slice` | 0.278 us | 0.073 us | 0.092 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.069 us | 0.085 us | 0.80x | 0.80–0.81x | -0.017 us | 9 pairs; 41/39 ms; 594,549/460,958 ops/lane; IQR 1.006/1.005 | `Pcre2Literal` | 0.122 us | 0.051 us | 0.073 us | 0.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.110 us | 0.13x | 0.12–0.13x | -0.096 us | 9 pairs; 41/40 ms; 2,885,600/364,132 ops/lane; IQR 1.017/1.012 | `Pcre2Backtracking` | 0.239 us | 0.093 us | 0.116 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.123 us | 0.93x | 0.92–0.93x | -0.009 us | 9 pairs; 38/39 ms; 330,703/314,528 ops/lane; IQR 1.005/1.020 | `Pcre2Character` | 0.133 us | 0.111 us | 0.143 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.202 us | 0.37x | 0.37–0.38x | -0.126 us | 9 pairs; 36/39 ms; 479,807/194,133 ops/lane; IQR 1.004/1.013 | `Pcre2Slice` | 0.457 us | 0.075 us | 0.111 us | 0.68x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.613 us | 159.987 us | 0.52x | 0.51–0.52x | -77.452 us | 9 pairs; 40/40 ms; 487/252 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 374.780 us | 105.421 us | 107.630 us | 0.77x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.544 us | 282.027 us | 0.13x | 0.13–0.13x | -244.594 us | 9 pairs; 40/40 ms; 1,057/141 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 395.473 us | 224.013 us | 232.842 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.514 us | 212.107 us | 0.04x | 0.04–0.05x | -202.593 us | 9 pairs; 40/42 ms; 4,174/196 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 8.208 us | 218.358 us | 221.625 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 236.890 us | 217.121 us | 1.09x | 1.09–1.10x | +19.703 us | 9 pairs; 39/40 ms; 166/182 ops/lane; IQR 1.002/1.006 | `Pcre2SingleTokenRepeat` | 8.229 us | 230.418 us | 232.061 us | 1.02x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.544 us | 22.857 us | 0.16x | 0.15–0.16x | -19.314 us | 9 pairs; 39/38 ms; 11,135/1,676 ops/lane; IQR 1.005/1.014 | `Pcre2Literal` | 1.389 us | 4.285 us | 6.005 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.885 us | 81.936 us | 0.12x | 0.10–0.12x | -72.047 us | 9 pairs; 41/39 ms; 4,160/477 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 9.348 us | 90.170 us | 92.837 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,160.188 us | 27,999.900 us | 0.15x | 0.14–0.15x | -23814.450 us | 9 pairs; 33/28 ms; 8/1 ops/lane; IQR 1.005/1.040 | `Pcre2Backtracking` | 5,344.960 us | 11,590.520 us | 18,478.800 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,492.200 us | 55,345.200 us | 0.06x | 0.06–0.07x | -51853.000 us | 9 pairs; 42/55 ms; 12/1 ops/lane; IQR 1.030/1.017 | `Pcre2Backtracking` | 3,979.954 us | 42,066.550 us | 48,548.900 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,946.895 us | 470,794.700 us | 0.00x | 0.00–0.00x | -468916.562 us | 9 pairs; 41/471 ms; 21/1 ops/lane; IQR 1.036/1.002 | `Pcre2Character` | 1,925.063 us | 23,057.033 us | 29,639.850 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,542.450 us | 10,738.200 us | 0.52x | 0.47–0.60x | -5175.617 us | 9 pairs; 33/32 ms; 6/3 ops/lane; IQR 1.004/1.227 | `Pcre2Literal` | 1,362.478 us | 1,394.374 us | 8,472.000 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,909.450 us | 145,866.400 us | 0.13x | 0.13–0.13x | -126845.750 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 1,251.120 us | 510.228 us | 8,535.783 us | 2.22x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,805.900 us | 21,958.700 us | 0.22x | 0.22–0.22x | -17139.663 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.009/1.009 | `Pcre2Slice` | 2,510.150 us | 10,943.320 us | 18,854.300 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,280.150 us | 134,580.400 us | 0.17x | 0.16–0.17x | -112355.200 us | 9 pairs; 45/135 ms; 2/1 ops/lane; IQR 1.009/1.003 | `Pcre2Backtracking` | 1,556.279 us | 2,330.023 us | 10,357.560 us | 2.15x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 175.655 us | 980.008 us | 0.18x | 0.18–0.19x | -804.282 us | 9 pairs; 39/39 ms; 224/40 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 175.463 us | 323.441 us | 545.495 us | 0.32x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,402.267 us | 13,438.233 us | 0.85x | 0.85–0.85x | -2031.333 us | 9 pairs; 34/40 ms; 3/3 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 310.987 us | 14,988.050 us | 15,180.525 us | 0.75x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,803.057 us | 98,869.800 us | 0.03x | 0.03–0.03x | -96073.307 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 1,896.170 us | 19,944.633 us | 20,136.933 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.189 us | 2,807.507 us | 0.07x | 0.07–0.07x | -2624.096 us | 9 pairs; 40/39 ms; 215/14 ops/lane; IQR 1.006/1.005 | `Pcre2SingleTokenRepeat` | 191.667 us | 1,015.688 us | 1,429.269 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,551.027 us | 20,082.200 us | 0.08x | 0.08–0.08x | -18532.704 us | 9 pairs; 40/40 ms; 26/2 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 582.529 us | 10,739.360 us | 10,977.780 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.152 us | 1.103 us | 0.14x | 0.14–0.14x | -0.951 us | 9 pairs; 39/40 ms; 256,765/36,706 ops/lane; IQR 1.003/1.000 | `Pcre2Slice` | 0.746 us | 0.261 us | 0.282 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.490 us | 0.831 us | 0.59x | 0.59–0.59x | -0.341 us | 9 pairs; 40/40 ms; 81,907/48,447 ops/lane; IQR 1.002/1.001 | `Pcre2Literal` | 0.146 us | 0.148 us | 0.173 us | 2.83x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.301 us | 1.183 us | 0.25x | 0.25–0.25x | -0.882 us | 9 pairs; 40/40 ms; 133,600/34,103 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 1.078 us | 0.351 us | 0.376 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.889 us | 1.130 us | 0.79x | 0.78–0.79x | -0.242 us | 9 pairs; 41/40 ms; 46,059/35,634 ops/lane; IQR 1.002/1.004 | `Pcre2Character` | 0.285 us | 0.426 us | 0.472 us | 1.88x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.158 us | 0.852 us | 0.19x | 0.18–0.19x | -0.695 us | 9 pairs; 39/40 ms; 245,983/47,254 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 0.868 us | 0.156 us | 0.201 us | 0.78x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.897 us | 1.116 us | 0.80x | 0.80–0.80x | -0.219 us | 9 pairs; 40/40 ms; 44,888/35,965 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 3.885 us | 0.306 us | 0.325 us | 2.76x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.794 us | 0.835 us | 0.95x | 0.95–0.95x | -0.041 us | 9 pairs; 40/40 ms; 50,330/48,122 ops/lane; IQR 1.000/1.001 | `Pcre2Literal` | 3.555 us | 0.180 us | 0.202 us | 3.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.009 us | 1.190 us | 0.85x | 0.85–0.85x | -0.182 us | 9 pairs; 41/40 ms; 40,375/33,614 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 5.261 us | 0.415 us | 0.441 us | 2.29x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.177 us | 1.133 us | 1.04x | 1.03–1.04x | +0.041 us | 9 pairs; 39/40 ms; 32,987/35,200 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 3.617 us | 0.482 us | 0.518 us | 2.27x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.796 us | 0.857 us | 0.93x | 0.93–0.93x | -0.062 us | 9 pairs; 40/41 ms; 49,850/47,615 ops/lane; IQR 1.001/1.003 | `Pcre2Slice` | 2.131 us | 0.182 us | 0.226 us | 3.52x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.566 us | 1.115 us | 0.51x | 0.51–0.51x | -0.550 us | 9 pairs; 40/40 ms; 71,446/35,756 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.524 us | 0.838 us | 0.62x | 0.62–0.63x | -0.316 us | 9 pairs; 40/40 ms; 76,859/48,135 ops/lane; IQR 1.007/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.674 us | 1.190 us | 0.57x | 0.56–0.57x | -0.516 us | 9 pairs; 40/40 ms; 59,674/33,396 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.943 us | 1.136 us | 0.83x | 0.83–0.83x | -0.191 us | 9 pairs; 41/40 ms; 43,154/35,269 ops/lane; IQR 1.001/1.003 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.481 us | 0.856 us | 0.56x | 0.56–0.56x | -0.375 us | 9 pairs; 40/41 ms; 82,318/47,429 ops/lane; IQR 1.001/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.644 us | — | — | — | — | — | `—` | 25.742 us | 97.353 us | 98.318 us | 0.64x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.674 us | — | — | — | — | — | `—` | 1.842 us | 0.370 us | 0.393 us | 16.96x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.047 us | — | — | — | — | — | `—` | 0.941 us | 0.244 us | 0.273 us | 3.84x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.190 us | — | — | — | — | — | `—` | 3.149 us | 0.469 us | 0.504 us | 2.36x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.480 us | — | — | — | — | — | `—` | 1.055 us | 0.543 us | 0.592 us | 2.50x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.478 us | — | — | — | — | — | `—` | 1.605 us | 0.268 us | 0.322 us | 13.90x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.089 us | 0.085 us | 1.05x | 1.04–1.05x | +0.004 us | 9 pairs; 41/40 ms; 457,059/467,878 ops/lane; IQR 1.006/1.012 | `Pcre2Character` | 0 B | 0 B | 2.269 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.127 us | 0.10x | 0.10–0.10x | -0.114 us | 9 pairs; 40/39 ms; 3,034,191/306,243 ops/lane; IQR 1.009/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 131.681 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.385 us | 0.107 us | 3.60x | 3.56–3.62x | +0.278 us | 9 pairs; 35/40 ms; 91,075/373,054 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 20.306 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.392 us | 0.115 us | 3.41x | 3.39–3.42x | +0.277 us | 9 pairs; 36/40 ms; 92,494/350,043 ops/lane; IQR 1.021/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 70.556 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.375 us | 0.106 us | 3.58x | 3.54–3.63x | +0.270 us | 9 pairs; 36/40 ms; 96,132/381,579 ops/lane; IQR 1.026/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 69.175 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.566 us | 0.347 us | 1.64x | 1.63–1.65x | +0.220 us | 9 pairs; 37/39 ms; 64,458/112,972 ops/lane; IQR 1.005/1.005 | `Pcre2Character` | 0 B | 0 B | 1.531 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.657 us | 0.02x | 0.02–0.02x | -0.646 us | 9 pairs; 40/39 ms; 3,418,675/59,717 ops/lane; IQR 1.012/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 22.019 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.505 us | 0.02x | 0.02–0.02x | -0.493 us | 9 pairs; 41/39 ms; 3,388,187/77,440 ops/lane; IQR 1.025/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 21.119 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.456 us | 0.109 us | 4.18x | 4.16–4.23x | +0.347 us | 9 pairs; 37/40 ms; 80,424/367,651 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 18.137 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.274 us | 10.523 us | 0.79x | 0.78–0.79x | -2.252 us | 9 pairs; 39/41 ms; 4,712/3,943 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.301 us | 1.062 us | 0.28x | 0.28–0.28x | -0.761 us | 9 pairs; 41/40 ms; 135,309/37,926 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 131.275 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.326 us | 0.478 us | 0.68x | 0.68–0.69x | -0.152 us | 9 pairs; 39/40 ms; 119,064/83,598 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 113.513 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.290 us | 0.461 us | 0.63x | 0.63–0.63x | -0.171 us | 9 pairs; 33/40 ms; 115,548/87,284 ops/lane; IQR 1.002/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 143.850 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.304 us | 0.592 us | 0.51x | 0.51–0.51x | -0.291 us | 9 pairs; 37/40 ms; 120,488/67,377 ops/lane; IQR 1.016/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 105.406 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.294 us | 0.398 us | 0.74x | 0.73–0.74x | -0.103 us | 9 pairs; 36/38 ms; 121,672/96,563 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 213.200 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.854 us | 8.868 us | 0.55x | 0.55–0.55x | -4.015 us | 9 pairs; 38/40 ms; 7,906/4,520 ops/lane; IQR 1.007/1.002 | `Pcre2Character` | 0 B | 0 B | 1.475 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.255 us | 0.310 us | 0.83x | 0.82–0.83x | -0.054 us | 9 pairs; 34/40 ms; 133,197/128,284 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 93.125 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.274 us | 0.403 us | 0.68x | 0.68–0.68x | -0.129 us | 9 pairs; 37/39 ms; 136,455/95,518 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 122.537 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.913 us | — | — | — | — | — | `—` | 0 B | — | 15.100 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.187 us | 0.302 us | 0.62x | 0.62–0.63x | -0.114 us | 9 pairs; 33/39 ms; 174,767/130,373 ops/lane; IQR 1.014/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 54.200 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.827 us | — | — | — | — | — | `—` | 0 B | — | 14.713 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.568 us | 0.678 us | 0.84x | 0.83–0.84x | -0.110 us | 9 pairs; 39/40 ms; 69,157/58,859 ops/lane; IQR 1.012/1.003 | `Pcre2Slice` | 0 B | 0 B | 64.744 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 10.578 us | 28.902 us | 0.37x | 0.36–0.37x | -18.324 us | 9 pairs; 44/40 ms; 4,201/1,378 ops/lane; IQR 1.001/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.943 us | 1.087 us | 0.87x | 0.86–0.87x | -0.145 us | 9 pairs; 38/40 ms; 40,554/36,607 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 128.781 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.009 us | 1.211 us | 0.83x | 0.83–0.84x | -0.202 us | 9 pairs; 40/39 ms; 39,947/32,402 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 115.625 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.979 us | 1.032 us | 0.95x | 0.95–0.95x | -0.054 us | 9 pairs; 42/41 ms; 42,670/39,230 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 145.025 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.944 us | 1.173 us | 0.80x | 0.80–0.81x | -0.231 us | 9 pairs; 39/40 ms; 41,608/34,096 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 102.675 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.948 us | 0.942 us | 1.01x | 1.00–1.01x | +0.006 us | 9 pairs; 40/41 ms; 42,228/43,063 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 212.412 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.567 us | 8.873 us | 0.63x | 0.63–0.63x | -3.304 us | 9 pairs; 40/40 ms; 7,154/4,501 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.924 us | 0.890 us | 1.04x | 1.04–1.04x | +0.032 us | 9 pairs; 40/40 ms; 43,519/44,993 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 92.562 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.929 us | 0.967 us | 0.96x | 0.96–0.96x | -0.037 us | 9 pairs; 41/39 ms; 43,911/40,631 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 122.044 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.133 us | — | — | — | — | — | `—` | 0 B | — | 13.650 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.836 us | 0.864 us | 0.97x | 0.97–0.97x | -0.028 us | 9 pairs; 39/40 ms; 47,097/46,098 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.106 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.046 us | — | — | — | — | — | `—` | 0 B | — | 14.012 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.915 us | 1.261 us | 0.73x | 0.72–0.73x | -0.346 us | 9 pairs; 40/40 ms; 43,845/31,536 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | 0 B | 0 B | 63.888 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.369 us | 2.292 us | 0.60x | 0.60–0.60x | -0.923 us | 9 pairs; 40/40 ms; 29,546/17,545 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.665 us | 1.089 us | 0.61x | 0.61–0.61x | -0.423 us | 9 pairs; 40/40 ms; 59,903/36,622 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 130.287 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.718 us | 1.213 us | 0.59x | 0.59–0.59x | -0.496 us | 9 pairs; 42/41 ms; 58,251/33,388 ops/lane; IQR 1.001/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 114.688 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.662 us | 1.031 us | 0.64x | 0.64–0.64x | -0.370 us | 9 pairs; 41/39 ms; 62,355/38,145 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 141.469 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.642 us | 1.174 us | 0.55x | 0.55–0.55x | -0.531 us | 9 pairs; 40/40 ms; 62,294/33,997 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 103.331 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.655 us | 0.942 us | 0.70x | 0.69–0.70x | -0.286 us | 9 pairs; 39/40 ms; 58,964/42,371 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 213.600 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.812 us | 2.194 us | 0.83x | 0.82–0.83x | -0.382 us | 9 pairs; 40/41 ms; 22,123/18,870 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.612 us | 0.888 us | 0.69x | 0.69–0.69x | -0.275 us | 9 pairs; 40/40 ms; 65,155/44,902 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 91.331 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.637 us | 0.968 us | 0.66x | 0.66–0.66x | -0.331 us | 9 pairs; 41/40 ms; 65,104/40,907 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 121.881 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.850 us | — | — | — | — | — | `—` | 0 B | — | 13.331 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.501 us | 0.863 us | 0.58x | 0.58–0.58x | -0.362 us | 9 pairs; 39/40 ms; 78,198/46,464 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.244 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.768 us | — | — | — | — | — | `—` | 0 B | — | 14.144 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.583 us | 1.265 us | 0.46x | 0.46–0.46x | -0.683 us | 9 pairs; 40/39 ms; 68,346/31,152 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 0 B | 0 B | 65.388 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.984 us | — | — | — | — | — | `—` | 480 B | — | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.118 us | — | — | — | — | — | `—` | 48 B | — | 128.900 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.215 us | — | — | — | — | — | `—` | 48 B | — | 116.688 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.098 us | — | — | — | — | — | `—` | 48 B | — | 141.194 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.112 us | — | — | — | — | — | `—` | 48 B | — | 100.369 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.098 us | — | — | — | — | — | `—` | 48 B | — | 219.537 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.858 us | — | — | — | — | — | `—` | 64 B | — | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.122 us | — | — | — | — | — | `—` | 40 B | — | 94.125 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.108 us | — | — | — | — | — | `—` | 40 B | — | 124.312 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.144 us | — | — | — | — | — | `—` | 1,216 B | — | 14.938 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.049 us | — | — | — | — | — | `—` | 48 B | — | 53.069 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.717 us | — | — | — | — | — | `—` | 928 B | — | 13.750 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.622 us | — | — | — | — | — | `—` | 23,312 B | — | 63.519 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.935–228.429 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.469–95.292 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.544–12.743 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.826–45.838 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.036–1.280 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.876–45.284 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.696–22.231 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.254–100.015 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.124–72.359 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.189–5.473 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.326–376.782 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.134 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.559–308.606 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.856–5.141 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.200–15.773 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 172.072–1,337.808 us |

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
