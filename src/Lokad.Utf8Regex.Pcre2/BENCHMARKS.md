<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `FFF31EAD670B33A4B655AFE5BF9384A23521DE8C04C191218A92C76CBFB374CF`
- Latest managed row measurement: `2026-11-05T12:33:48.2113098+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T12:33:48.2113098+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `41/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `75` managed faster, `0` equivalent, `13` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `6cf844681cb6` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `6cf844681cb6` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.972 us | 2.117 us | 0.47x | 0.45–0.49x | -1.102 us | 9 pairs; 36/33 ms; 36,944/15,529 ops/lane; IQR 1.053/1.075 | `Pcre2Slice` | 477.914 us | 0.285 us | 0.868 us | 1.12x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.107 us | 12.425 us | 0.09x | 0.09–0.10x | -11.319 us | 9 pairs; 39/40 ms; 34,809/3,224 ops/lane; IQR 1.024/1.134 | `Pcre2Slice` | 0.843 us | 0.099 us | 3.732 us | 0.30x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.229 us | 0.01x | 0.01–0.01x | -1.218 us | 9 pairs; 39/40 ms; 3,647,475/32,910 ops/lane; IQR 1.011/1.005 | `Pcre2Slice` | 0.106 us | 1.151 us | 1.147 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.684 us | 0.158 us | 4.32x | 4.27–4.34x | +0.525 us | 9 pairs; 38/40 ms; 55,399/255,746 ops/lane; IQR 1.002/1.010 | `Pcre2Backtracking` | 0.049 us | 0.125 us | 0.163 us | 4.18x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.388 us | 0.574 us | 4.16x | 4.09–4.17x | +1.813 us | 9 pairs; 40/40 ms; 16,612/69,081 ops/lane; IQR 1.001/1.010 | `Pcre2Backtracking` | 0.127 us | 0.731 us | 0.769 us | 3.11x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.102 us | 0.301 us | 0.34x | 0.33–0.34x | -0.200 us | 9 pairs; 40/39 ms; 393,159/130,981 ops/lane; IQR 1.003/1.009 | `Pcre2Slice` | 0.113 us | 0.321 us | 0.344 us | 0.30x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.107 us | 0.352 us | 0.30x | 0.30–0.32x | -0.245 us | 9 pairs; 40/39 ms; 371,158/111,043 ops/lane; IQR 1.016/1.004 | `Pcre2Slice` | 0.118 us | 0.438 us | 0.466 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.081 us | 0.289 us | 0.28x | 0.28–0.28x | -0.209 us | 9 pairs; 31/40 ms; 384,071/139,523 ops/lane; IQR 1.002/1.010 | `Pcre2Slice` | 0.088 us | 0.245 us | 0.263 us | 0.31x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.080 us | 0.317 us | 0.25x | 0.25–0.25x | -0.236 us | 9 pairs; 42/38 ms; 522,341/119,801 ops/lane; IQR 1.007/1.006 | `Pcre2Slice` | 0.089 us | 0.257 us | 0.275 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.077 us | 2.25x | 2.18–2.31x | +0.096 us | 9 pairs; 40/40 ms; 231,674/516,417 ops/lane; IQR 1.009/1.047 | `Pcre2Slice` | 0.087 us | 0.989 us | 1.022 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.201 us | 0.17x | 0.17–0.17x | -0.167 us | 9 pairs; 39/40 ms; 1,164,435/198,985 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0.110 us | 0.305 us | 0.342 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.593 us | 0.10x | 0.10–0.10x | -0.532 us | 9 pairs; 40/39 ms; 657,177/66,319 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0.135 us | 0.230 us | 0.250 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.157 us | 0.22x | 0.22–0.22x | -0.123 us | 9 pairs; 38/40 ms; 1,134,682/253,081 ops/lane; IQR 1.029/1.004 | `Pcre2Backtracking` | 0.116 us | 0.179 us | 0.197 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.113 us | 0.19x | 0.18–0.19x | -0.092 us | 9 pairs; 41/40 ms; 1,952,876/357,228 ops/lane; IQR 1.024/1.011 | `Pcre2Backtracking` | 0.066 us | 0.092 us | 0.113 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.116 us | 0.49x | 0.49–0.50x | -0.059 us | 9 pairs; 40/39 ms; 691,580/332,007 ops/lane; IQR 1.008/1.018 | `Pcre2Backtracking` | 0.015 us | 0.088 us | 0.108 us | 0.53x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.087 us | 0.278 us | 0.31x | 0.31–0.32x | -0.192 us | 9 pairs; 41/39 ms; 470,793/141,283 ops/lane; IQR 1.002/1.014 | `Pcre2Slice` | 0.094 us | 0.123 us | 0.157 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Inconclusive** | 7 B | 0.023 us | 0.117 us | 0.20x | 0.20–0.23x | -0.094 us | 9 pairs; 40/41 ms; 1,728,954/347,371 ops/lane; IQR 1.051/1.004 | `Pcre2Backtracking` | 0.019 us | 0.092 us | 0.114 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.191 us | 0.05x | 0.05–0.06x | -2.073 us | 9 pairs; 40/49 ms; 339,881/22,486 ops/lane; IQR 1.005/1.139 | `Pcre2Literal` | 0.089 us | 0.053 us | 0.637 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 1.905 us | 0.06x | 0.05–0.07x | -1.782 us | 9 pairs; 28/24 ms; 231,740/12,851 ops/lane; IQR 1.006/1.248 | `Pcre2Literal` | 0.157 us | 0.057 us | 0.651 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.155 us | 2.060 us | 0.08x | 0.07–0.08x | -1.905 us | 9 pairs; 40/32 ms; 257,332/15,457 ops/lane; IQR 1.006/1.052 | `Pcre2Literal` | 0.206 us | 0.242 us | 0.860 us | 0.18x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.149 us | 2.010 us | 0.08x | 0.07–0.08x | -1.856 us | 9 pairs; 40/36 ms; 265,492/18,070 ops/lane; IQR 1.041/1.021 | `Pcre2Literal` | 0.177 us | 0.210 us | 0.770 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.015 us | 0.099 us | 0.15x | 0.09–0.15x | -0.087 us | 9 pairs; 68/40 ms; 4,496,467/400,352 ops/lane; IQR 1.348/1.013 | `Pcre2Slice` | 0.265 us | 0.073 us | 0.090 us | 0.17x | 0 B | 0 B |
| `simple/foo-dense` | **Inconclusive** | 18 B | 0.076 us | 0.085 us | 0.89x | 0.77–0.90x | -0.009 us | 9 pairs; 45/39 ms; 594,150/459,147 ops/lane; IQR 1.123/1.019 | `Pcre2Literal` | 0.140 us | 0.054 us | 0.074 us | 1.04x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.12–0.13x | -0.094 us | 9 pairs; 41/39 ms; 2,859,545/362,295 ops/lane; IQR 1.014/1.024 | `Pcre2Backtracking` | 0.251 us | 0.093 us | 0.113 us | 0.13x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.123 us | 0.92x | 0.92–0.93x | -0.009 us | 9 pairs; 38/39 ms; 331,184/318,401 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 0.146 us | 0.117 us | 0.146 us | 0.78x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.210 us | 0.36x | 0.36–0.36x | -0.135 us | 9 pairs; 41/39 ms; 538,042/187,589 ops/lane; IQR 1.002/1.009 | `Pcre2Slice` | 0.432 us | 0.079 us | 0.114 us | 0.67x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.304 us | 160.549 us | 0.51x | 0.51–0.51x | -78.143 us | 9 pairs; 40/39 ms; 485/245 ops/lane; IQR 1.002/1.004 | `Pcre2Slice` | 331.279 us | 105.379 us | 105.240 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.675 us | 281.939 us | 0.13x | 0.13–0.13x | -244.245 us | 9 pairs; 40/39 ms; 1,074/139 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 283.092 us | 218.762 us | 228.065 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.454 us | 211.208 us | 0.04x | 0.04–0.05x | -201.740 us | 9 pairs; 40/40 ms; 4,277/191 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 7.959 us | 217.801 us | 221.019 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.290 us | 203.670 us | 1.19x | 1.18–1.20x | +37.891 us | 9 pairs; 40/39 ms; 167/191 ops/lane; IQR 1.002/1.012 | `Pcre2SingleTokenRepeat` | 7.948 us | 229.202 us | 232.276 us | 1.04x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.555 us | 23.685 us | 0.15x | 0.15–0.15x | -20.138 us | 9 pairs; 39/39 ms; 11,108/1,658 ops/lane; IQR 1.005/1.015 | `Pcre2Literal` | 1.380 us | 4.292 us | 6.139 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.603 us | 81.371 us | 0.12x | 0.12–0.12x | -71.766 us | 9 pairs; 40/40 ms; 4,164/491 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 9.333 us | 90.661 us | 91.860 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,185.720 us | 28,771.700 us | 0.15x | 0.14–0.15x | -24585.980 us | 9 pairs; 42/29 ms; 10/1 ops/lane; IQR 1.006/1.022 | `Pcre2Backtracking` | 5,693.120 us | 12,273.180 us | 19,509.200 us | 0.21x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,441.625 us | 55,660.800 us | 0.06x | 0.06–0.06x | -52174.092 us | 9 pairs; 41/56 ms; 12/1 ops/lane; IQR 1.034/1.018 | `Pcre2Backtracking` | 3,718.386 us | 41,388.350 us | 47,625.100 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 2,000.814 us | 486,960.000 us | 0.00x | 0.00–0.00x | -485019.505 us | 9 pairs; 42/487 ms; 21/1 ops/lane; IQR 1.033/1.001 | `Pcre2Character` | 1,806.279 us | 22,941.933 us | 29,855.800 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,527.783 us | 11,558.400 us | 0.48x | 0.45–0.59x | -6028.150 us | 9 pairs; 33/35 ms; 6/3 ops/lane; IQR 1.006/1.086 | `Pcre2Literal` | 1,304.372 us | 1,355.048 us | 8,382.343 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,812.450 us | 145,564.800 us | 0.13x | 0.13–0.13x | -126681.650 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 1,253.334 us | 506.052 us | 8,490.600 us | 2.22x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,823.500 us | 21,922.900 us | 0.22x | 0.22–0.22x | -17099.400 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.002/1.013 | `Pcre2Slice` | 2,460.490 us | 10,675.420 us | 18,723.233 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,351.350 us | 134,346.500 us | 0.17x | 0.17–0.17x | -111764.250 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.020/1.007 | `Pcre2Backtracking` | 1,539.097 us | 2,314.750 us | 10,445.620 us | 2.14x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 164.660 us | 966.295 us | 0.17x | 0.17–0.18x | -801.635 us | 9 pairs; 39/41 ms; 239/42 ops/lane; IQR 1.001/1.043 | `Pcre2Backtracking` | 167.422 us | 339.126 us | 627.031 us | 0.26x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,496.167 us | 13,402.867 us | 0.93x | 0.93–0.94x | -896.067 us | 9 pairs; 37/40 ms; 3/3 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 312.987 us | 14,680.550 us | 14,923.350 us | 0.84x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 3,021.077 us | 98,581.900 us | 0.03x | 0.03–0.03x | -95566.592 us | 9 pairs; 39/99 ms; 13/1 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 1,897.937 us | 19,908.267 us | 20,096.200 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.013 us | 2,817.479 us | 0.07x | 0.06–0.07x | -2631.939 us | 9 pairs; 40/39 ms; 215/14 ops/lane; IQR 1.004/1.014 | `Pcre2SingleTokenRepeat` | 186.792 us | 1,013.902 us | 1,431.174 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,591.425 us | 20,211.050 us | 0.08x | 0.08–0.08x | -18610.908 us | 9 pairs; 38/40 ms; 24/2 ops/lane; IQR 1.013/1.004 | `Pcre2Backtracking` | 561.385 us | 10,091.320 us | 10,277.480 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.137 us | 1.098 us | 0.12x | 0.12–0.13x | -0.961 us | 9 pairs; 40/40 ms; 291,088/36,419 ops/lane; IQR 1.003/1.001 | `Pcre2Slice` | 0.661 us | 0.259 us | 0.281 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.524 us | 0.830 us | 0.63x | 0.63–0.63x | -0.306 us | 9 pairs; 40/40 ms; 76,640/48,249 ops/lane; IQR 1.006/1.003 | `Pcre2Literal` | 0.143 us | 0.149 us | 0.172 us | 3.04x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.291 us | 1.180 us | 0.25x | 0.25–0.25x | -0.889 us | 9 pairs; 33/40 ms; 114,975/33,861 ops/lane; IQR 1.001/1.001 | `Pcre2Backtracking` | 1.078 us | 0.344 us | 0.373 us | 0.78x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.926 us | 1.132 us | 0.82x | 0.82–0.82x | -0.205 us | 9 pairs; 41/40 ms; 44,206/35,348 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 0.275 us | 0.431 us | 0.475 us | 1.95x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.169 us | 0.857 us | 0.20x | 0.20–0.20x | -0.687 us | 9 pairs; 38/40 ms; 224,988/46,678 ops/lane; IQR 1.008/1.001 | `Pcre2Slice` | 0.801 us | 0.158 us | 0.202 us | 0.84x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.877 us | 1.108 us | 0.79x | 0.78–0.79x | -0.232 us | 9 pairs; 40/40 ms; 45,967/36,205 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 3.771 us | 0.304 us | 0.321 us | 2.73x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.791 us | 0.834 us | 0.95x | 0.95–0.95x | -0.043 us | 9 pairs; 40/40 ms; 50,077/47,976 ops/lane; IQR 1.002/1.000 | `Pcre2Literal` | 3.524 us | 0.182 us | 0.201 us | 3.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.989 us | 1.189 us | 0.83x | 0.83–0.83x | -0.200 us | 9 pairs; 40/40 ms; 40,685/33,843 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 5.232 us | 0.403 us | 0.425 us | 2.33x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.207 us | 1.147 us | 1.05x | 1.05–1.06x | +0.062 us | 9 pairs; 38/40 ms; 31,626/34,827 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 3.637 us | 0.482 us | 0.526 us | 2.30x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.803 us | 0.858 us | 0.94x | 0.93–0.94x | -0.055 us | 9 pairs; 40/41 ms; 49,903/47,400 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 2.133 us | 0.184 us | 0.224 us | 3.59x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.565 us | 1.108 us | 0.51x | 0.51–0.51x | -0.543 us | 9 pairs; 40/39 ms; 71,003/34,809 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.504 us | 0.834 us | 0.61x | 0.60–0.61x | -0.329 us | 9 pairs; 41/40 ms; 80,424/47,830 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.684 us | 1.188 us | 0.58x | 0.57–0.58x | -0.505 us | 9 pairs; 41/40 ms; 59,642/33,804 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.946 us | 1.148 us | 0.82x | 0.82–0.83x | -0.202 us | 9 pairs; 39/40 ms; 41,618/34,973 ops/lane; IQR 1.001/1.003 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.489 us | 0.860 us | 0.57x | 0.57–0.57x | -0.372 us | 9 pairs; 39/40 ms; 80,239/46,738 ops/lane; IQR 1.004/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.658 us | — | — | — | — | — | `—` | 25.832 us | 98.375 us | 99.194 us | 0.63x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.421 us | — | — | — | — | — | `—` | 1.769 us | 0.364 us | 0.386 us | 16.63x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.074 us | — | — | — | — | — | `—` | 0.908 us | 0.244 us | 0.273 us | 3.93x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.227 us | — | — | — | — | — | `—` | 3.095 us | 0.455 us | 0.485 us | 2.53x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.487 us | — | — | — | — | — | `—` | 1.004 us | 0.544 us | 0.590 us | 2.52x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.876 us | — | — | — | — | — | `—` | 1.519 us | 0.276 us | 0.327 us | 11.85x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.089 us | 0.085 us | 1.05x | 1.04–1.06x | +0.004 us | 9 pairs; 41/38 ms; 457,455/452,513 ops/lane; IQR 1.002/1.010 | `Pcre2Character` | 0 B | 0 B | 2.219 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.119 us | 0.11x | 0.11–0.11x | -0.106 us | 9 pairs; 40/40 ms; 3,123,737/338,047 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 129.338 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.397 us | 0.108 us | 3.67x | 3.65–3.69x | +0.289 us | 9 pairs; 36/41 ms; 90,276/377,546 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 22.212 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.392 us | 0.118 us | 3.34x | 3.30–3.38x | +0.274 us | 9 pairs; 36/41 ms; 92,423/345,644 ops/lane; IQR 1.010/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 71.237 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.376 us | 0.103 us | 3.65x | 3.63–3.67x | +0.273 us | 9 pairs; 36/39 ms; 95,207/377,906 ops/lane; IQR 1.021/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 69.900 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.585 us | 0.345 us | 1.70x | 1.69–1.70x | +0.240 us | 9 pairs; 33/39 ms; 56,390/112,332 ops/lane; IQR 1.004/1.009 | `Pcre2Character` | 0 B | 0 B | 1.462 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.635 us | 0.02x | 0.02–0.02x | -0.623 us | 9 pairs; 42/40 ms; 3,431,204/62,539 ops/lane; IQR 1.023/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 23.056 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.503 us | 0.02x | 0.02–0.02x | -0.490 us | 9 pairs; 42/39 ms; 3,418,526/76,675 ops/lane; IQR 1.039/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 21.519 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.462 us | 0.109 us | 4.24x | 4.21–4.29x | +0.354 us | 9 pairs; 36/39 ms; 78,374/358,367 ops/lane; IQR 1.005/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 19.375 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.568 us | 10.639 us | 0.81x | 0.80–0.81x | -2.069 us | 9 pairs; 41/41 ms; 4,773/3,847 ops/lane; IQR 1.004/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.307 us | 1.048 us | 0.29x | 0.29–0.29x | -0.741 us | 9 pairs; 40/40 ms; 131,439/38,375 ops/lane; IQR 1.007/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 127.575 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.321 us | 0.462 us | 0.69x | 0.69–0.70x | -0.142 us | 9 pairs; 38/37 ms; 118,972/80,885 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 114.406 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.294 us | 0.464 us | 0.63x | 0.63–0.69x | -0.170 us | 9 pairs; 38/41 ms; 129,990/87,968 ops/lane; IQR 1.051/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 139.938 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.303 us | 0.593 us | 0.51x | 0.51–0.51x | -0.290 us | 9 pairs; 37/40 ms; 122,837/67,115 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 102.831 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.283 us | 0.401 us | 0.71x | 0.70–0.71x | -0.118 us | 9 pairs; 36/40 ms; 128,718/99,844 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 214.800 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.908 us | 8.847 us | 0.55x | 0.55–0.56x | -3.940 us | 9 pairs; 40/40 ms; 8,102/4,548 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.262 us | 0.309 us | 0.85x | 0.85–0.86x | -0.047 us | 9 pairs; 37/41 ms; 141,548/131,817 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 91.875 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.270 us | 0.400 us | 0.67x | 0.67–0.68x | -0.130 us | 9 pairs; 37/40 ms; 136,813/99,825 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 121.669 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.796 us | — | — | — | — | — | `—` | 0 B | — | 13.494 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.198 us | 0.293 us | 0.67x | 0.66–0.68x | -0.096 us | 9 pairs; 41/39 ms; 208,682/132,446 ops/lane; IQR 1.012/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 53.606 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.721 us | — | — | — | — | — | `—` | 0 B | — | 14.006 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.575 us | 0.681 us | 0.85x | 0.84–0.85x | -0.103 us | 9 pairs; 37/40 ms; 64,479/58,298 ops/lane; IQR 1.013/1.006 | `Pcre2Slice` | 0 B | 0 B | 64.294 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.821 us | 28.929 us | 0.34x | 0.34–0.34x | -19.081 us | 9 pairs; 39/40 ms; 3,973/1,381 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.812 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.958 us | 1.070 us | 0.90x | 0.89–0.90x | -0.112 us | 9 pairs; 40/40 ms; 41,550/37,157 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 132.744 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.097 us | 1.196 us | 0.92x | 0.92–0.92x | -0.101 us | 9 pairs; 44/41 ms; 39,947/33,972 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 114.250 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.942 us | 1.035 us | 0.91x | 0.91–0.91x | -0.094 us | 9 pairs; 38/39 ms; 40,452/38,152 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 143.206 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.955 us | 1.169 us | 0.82x | 0.81–0.82x | -0.214 us | 9 pairs; 40/40 ms; 41,782/33,830 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 102.869 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.982 us | 0.945 us | 1.04x | 1.03–1.04x | +0.037 us | 9 pairs; 42/40 ms; 42,348/42,628 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 215.438 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.611 us | 8.911 us | 0.63x | 0.63–0.63x | -3.304 us | 9 pairs; 40/40 ms; 7,129/4,485 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.924 us | 0.888 us | 1.04x | 1.04–1.04x | +0.036 us | 9 pairs; 39/40 ms; 42,523/44,935 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 92.438 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.926 us | 0.969 us | 0.95x | 0.95–0.96x | -0.044 us | 9 pairs; 39/40 ms; 42,585/41,462 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 124.469 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.007 us | — | — | — | — | — | `—` | 0 B | — | 14.688 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.831 us | 0.868 us | 0.96x | 0.96–0.96x | -0.036 us | 9 pairs; 40/40 ms; 48,015/46,009 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.062 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.914 us | — | — | — | — | — | `—` | 0 B | — | 14.075 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.920 us | 1.266 us | 0.73x | 0.72–0.73x | -0.345 us | 9 pairs; 40/40 ms; 43,694/31,412 ops/lane; IQR 1.001/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.931 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.417 us | 2.282 us | 0.62x | 0.62–0.62x | -0.865 us | 9 pairs; 40/40 ms; 28,534/17,548 ops/lane; IQR 1.007/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.731 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.627 us | 1.076 us | 0.58x | 0.58–0.58x | -0.449 us | 9 pairs; 39/40 ms; 61,628/36,752 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 128.894 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.685 us | 1.198 us | 0.57x | 0.57–0.57x | -0.513 us | 9 pairs; 38/40 ms; 55,399/33,327 ops/lane; IQR 1.000/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 114.769 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.665 us | 1.034 us | 0.64x | 0.64–0.65x | -0.368 us | 9 pairs; 40/40 ms; 60,517/38,466 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 140.656 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.655 us | 1.170 us | 0.56x | 0.56–0.56x | -0.515 us | 9 pairs; 38/40 ms; 58,618/34,334 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 102.781 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.664 us | 0.946 us | 0.70x | 0.70–0.70x | -0.282 us | 9 pairs; 40/40 ms; 59,610/42,159 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 214.331 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.825 us | 2.201 us | 0.83x | 0.83–0.83x | -0.376 us | 9 pairs; 40/40 ms; 21,853/18,185 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.622 us | 0.888 us | 0.70x | 0.70–0.71x | -0.268 us | 9 pairs; 40/40 ms; 63,903/44,651 ops/lane; IQR 1.012/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 94.725 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.611 us | 0.969 us | 0.63x | 0.63–0.63x | -0.358 us | 9 pairs; 40/40 ms; 65,567/41,560 ops/lane; IQR 1.000/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 121.644 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.706 us | — | — | — | — | — | `—` | 0 B | — | 13.625 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.492 us | 0.869 us | 0.57x | 0.57–0.57x | -0.377 us | 9 pairs; 40/40 ms; 80,725/46,338 ops/lane; IQR 1.000/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 54.138 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.633 us | — | — | — | — | — | `—` | 0 B | — | 14.525 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.595 us | 1.263 us | 0.47x | 0.47–0.47x | -0.669 us | 9 pairs; 41/40 ms; 68,338/31,695 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.688 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.786 us | — | — | — | — | — | `—` | 480 B | — | 0.731 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.153 us | — | — | — | — | — | `—` | 48 B | — | 129.075 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.254 us | — | — | — | — | — | `—` | 48 B | — | 113.956 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.144 us | — | — | — | — | — | `—` | 48 B | — | 140.225 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.155 us | — | — | — | — | — | `—` | 48 B | — | 102.050 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.135 us | — | — | — | — | — | `—` | 48 B | — | 222.919 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.856 us | — | — | — | — | — | `—` | 64 B | — | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.152 us | — | — | — | — | — | `—` | 40 B | — | 94.362 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.160 us | — | — | — | — | — | `—` | 40 B | — | 121.944 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.328 us | — | — | — | — | — | `—` | 1,216 B | — | 14.425 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.079 us | — | — | — | — | — | `—` | 48 B | — | 52.619 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.766 us | — | — | — | — | — | `—` | 928 B | — | 13.887 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.858 us | — | — | — | — | — | `—` | 23,312 B | — | 63.263 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.734–230.838 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.477–95.413 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.003–12.742 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.054–46.665 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.036–1.307 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.736–44.577 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.753–22.541 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.215–99.885 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.143–72.362 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.215–5.484 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.375–369.730 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.077 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.193–297.204 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.875–5.099 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.193–15.577 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 151.434–1,211.455 us |

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
