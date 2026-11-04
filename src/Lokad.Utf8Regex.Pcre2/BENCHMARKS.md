<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `98DDB3118EFCC19132BC8AEA2B479A4B8643882AF9ADF5096F018C24721189E9`
- Latest managed row measurement: `2026-11-04T11:43:30.5710371+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T11:43:30.5710371+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `1` equivalent, `13` native faster, `11` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `00a9756189c2` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `00a9756189c2` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.919 us | 2.523 us | 0.37x | 0.33–0.44x | -1.593 us | 9 pairs; 35/43 ms; 37,737/16,893 ops/lane; IQR 1.023/1.210 | `Pcre2Slice` | 585.395 us | 0.281 us | 0.916 us | 1.00x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.142 us | 12.662 us | 0.09x | 0.08–0.11x | -11.508 us | 9 pairs; 36/43 ms; 31,895/3,384 ops/lane; IQR 1.016/1.240 | `Pcre2Slice` | 0.710 us | 0.097 us | 3.920 us | 0.29x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.234 us | 0.01x | 0.01–0.01x | -1.223 us | 9 pairs; 39/40 ms; 3,638,531/32,172 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | 0.104 us | 1.080 us | 1.132 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.699 us | 0.159 us | 4.39x | 4.37–4.42x | +0.539 us | 9 pairs; 37/40 ms; 53,368/253,287 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0.046 us | 0.126 us | 0.159 us | 4.39x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.389 us | 0.571 us | 4.16x | 4.13–4.20x | +1.819 us | 9 pairs; 41/41 ms; 17,065/71,022 ops/lane; IQR 1.012/1.007 | `Pcre2Backtracking` | 0.130 us | 0.777 us | 0.815 us | 2.93x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.101 us | 0.291 us | 0.35x | 0.35–0.35x | -0.190 us | 9 pairs; 40/39 ms; 399,143/134,604 ops/lane; IQR 1.008/1.006 | `Pcre2Slice` | 0.110 us | 0.319 us | 0.348 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.105 us | 0.362 us | 0.29x | 0.29–0.29x | -0.257 us | 9 pairs; 33/41 ms; 318,396/112,460 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0.114 us | 0.443 us | 0.465 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.086 us | 0.283 us | 0.31x | 0.28–0.31x | -0.197 us | 9 pairs; 45/38 ms; 523,891/135,796 ops/lane; IQR 1.030/1.006 | `Pcre2Slice` | 0.088 us | 0.245 us | 0.267 us | 0.32x | 0 B | 0 B |
| `common/ip-miss` | **Inconclusive** | 15 B | 0.095 us | 0.313 us | 0.30x | 0.26–0.30x | -0.221 us | 9 pairs; 46/41 ms; 489,306/129,627 ops/lane; IQR 1.170/1.004 | `Pcre2Slice` | 0.087 us | 0.261 us | 0.283 us | 0.33x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.079 us | 2.20x | 2.18–2.31x | +0.094 us | 9 pairs; 40/42 ms; 228,601/527,943 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 0.086 us | 0.921 us | 0.943 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.199 us | 0.17x | 0.17–0.17x | -0.166 us | 9 pairs; 40/40 ms; 1,186,939/200,179 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0.107 us | 0.310 us | 0.353 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.614 us | 0.10x | 0.10–0.10x | -0.553 us | 9 pairs; 40/39 ms; 657,475/63,600 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0.130 us | 0.244 us | 0.266 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.157 us | 0.21x | 0.21–0.22x | -0.123 us | 9 pairs; 38/40 ms; 1,151,940/256,080 ops/lane; IQR 1.018/1.003 | `Pcre2Backtracking` | 0.102 us | 0.184 us | 0.202 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.114 us | 0.17x | 0.17–0.18x | -0.094 us | 9 pairs; 39/41 ms; 1,976,953/358,187 ops/lane; IQR 1.025/1.011 | `Pcre2Backtracking` | 0.059 us | 0.093 us | 0.112 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.120 us | 0.48x | 0.47–0.49x | -0.062 us | 9 pairs; 40/37 ms; 689,706/312,298 ops/lane; IQR 1.003/1.019 | `Pcre2Backtracking` | 0.016 us | 0.085 us | 0.103 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.086 us | 0.287 us | 0.30x | 0.30–0.30x | -0.201 us | 9 pairs; 41/41 ms; 471,295/144,801 ops/lane; IQR 1.005/1.012 | `Pcre2Slice` | 0.094 us | 0.117 us | 0.152 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.117 us | 0.21x | 0.19–0.21x | -0.093 us | 9 pairs; 42/40 ms; 1,747,627/345,612 ops/lane; IQR 1.039/1.009 | `Pcre2Backtracking` | 0.019 us | 0.094 us | 0.114 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.016 us | 0.06x | 0.05–0.06x | -1.898 us | 9 pairs; 40/44 ms; 340,232/21,770 ops/lane; IQR 1.003/1.117 | `Pcre2Literal` | 0.088 us | 0.051 us | 0.635 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.123 us | 2.084 us | 0.06x | 0.05–0.07x | -1.960 us | 9 pairs; 41/40 ms; 331,593/19,024 ops/lane; IQR 1.002/1.206 | `Pcre2Literal` | 0.145 us | 0.054 us | 0.631 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.147 us | 1.897 us | 0.08x | 0.07–0.08x | -1.748 us | 9 pairs; 38/38 ms; 255,888/19,822 ops/lane; IQR 1.006/1.067 | `Pcre2Literal` | 0.204 us | 0.235 us | 0.850 us | 0.17x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.898 us | 0.08x | 0.08–0.09x | -1.747 us | 9 pairs; 40/44 ms; 265,035/23,318 ops/lane; IQR 1.001/1.155 | `Pcre2Literal` | 0.180 us | 0.206 us | 0.832 us | 0.18x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.098 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 40/40 ms; 4,536,933/408,212 ops/lane; IQR 1.003/1.014 | `Pcre2Slice` | 0.565 us | 0.069 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.069 us | 0.086 us | 0.80x | 0.78–0.80x | -0.017 us | 9 pairs; 41/39 ms; 591,667/453,481 ops/lane; IQR 1.002/1.020 | `Pcre2Literal` | 0.123 us | 0.051 us | 0.073 us | 0.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.13–0.13x | -0.093 us | 9 pairs; 41/40 ms; 2,872,391/367,778 ops/lane; IQR 1.018/1.016 | `Pcre2Backtracking` | 0.242 us | 0.097 us | 0.119 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.123 us | 0.93x | 0.93–0.93x | -0.009 us | 9 pairs; 38/39 ms; 330,317/320,077 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 0.134 us | 0.112 us | 0.143 us | 0.80x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.204 us | 0.37x | 0.37–0.38x | -0.128 us | 9 pairs; 40/41 ms; 529,719/201,928 ops/lane; IQR 1.001/1.011 | `Pcre2Slice` | 0.461 us | 0.075 us | 0.107 us | 0.71x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.416 us | 159.885 us | 0.52x | 0.51–0.52x | -77.370 us | 9 pairs; 40/40 ms; 490/251 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 392.849 us | 101.751 us | 104.251 us | 0.79x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.987 us | 285.068 us | 0.13x | 0.13–0.13x | -247.010 us | 9 pairs; 40/40 ms; 1,058/141 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 386.662 us | 222.481 us | 233.589 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.912 us | 211.605 us | 0.05x | 0.05–0.05x | -201.659 us | 9 pairs; 40/39 ms; 3,990/183 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 8.186 us | 231.224 us | 236.843 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 240.040 us | 205.337 us | 1.17x | 1.16–1.18x | +34.360 us | 9 pairs; 40/39 ms; 166/191 ops/lane; IQR 1.001/1.011 | `Pcre2SingleTokenRepeat` | 8.178 us | 242.904 us | 246.095 us | 0.98x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.550 us | 22.505 us | 0.16x | 0.15–0.16x | -18.969 us | 9 pairs; 39/40 ms; 11,059/1,758 ops/lane; IQR 1.008/1.022 | `Pcre2Literal` | 1.381 us | 4.250 us | 6.117 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.589 us | 81.692 us | 0.12x | 0.12–0.12x | -72.101 us | 9 pairs; 40/40 ms; 4,137/484 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 9.353 us | 92.554 us | 94.379 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,170.210 us | 28,903.600 us | 0.14x | 0.14–0.15x | -24698.010 us | 9 pairs; 42/29 ms; 10/1 ops/lane; IQR 1.010/1.030 | `Pcre2Backtracking` | 5,867.311 us | 12,094.740 us | 18,588.700 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,561.375 us | 54,506.200 us | 0.07x | 0.06–0.07x | -51043.142 us | 9 pairs; 43/55 ms; 12/1 ops/lane; IQR 1.030/1.013 | `Pcre2Backtracking` | 3,974.869 us | 41,113.450 us | 48,071.850 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,937.471 us | 484,465.500 us | 0.00x | 0.00–0.00x | -482511.195 us | 9 pairs; 41/484 ms; 21/1 ops/lane; IQR 1.037/1.002 | `Pcre2Character` | 1,894.881 us | 23,137.367 us | 30,200.150 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,575.533 us | 11,319.150 us | 0.49x | 0.47–0.51x | -5769.217 us | 9 pairs; 33/45 ms; 6/4 ops/lane; IQR 1.013/1.066 | `Pcre2Literal` | 1,318.803 us | 1,355.202 us | 8,378.587 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,810.700 us | 146,122.300 us | 0.13x | 0.13–0.13x | -127333.200 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.010/1.003 | `Pcre2Backtracking` | 1,281.145 us | 510.468 us | 8,523.967 us | 2.21x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,811.062 us | 21,973.700 us | 0.22x | 0.22–0.22x | -17148.113 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.004/1.012 | `Pcre2Slice` | 2,485.881 us | 11,103.020 us | 19,195.067 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,751.950 us | 134,139.200 us | 0.17x | 0.17–0.17x | -111244.200 us | 9 pairs; 46/134 ms; 2/1 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 1,563.233 us | 2,369.705 us | 10,503.580 us | 2.17x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 189.439 us | 967.485 us | 0.20x | 0.19–0.20x | -778.337 us | 9 pairs; 38/39 ms; 203/40 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 227.905 us | 324.477 us | 536.668 us | 0.35x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,160.975 us | 13,316.833 us | 0.84x | 0.83–0.84x | -2136.992 us | 9 pairs; 45/40 ms; 4/3 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 307.458 us | 15,012.875 us | 15,185.475 us | 0.73x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,742.407 us | 99,928.700 us | 0.03x | 0.03–0.03x | -97175.843 us | 9 pairs; 38/100 ms; 14/1 ops/lane; IQR 1.002/1.006 | `Pcre2Character` | 1,885.748 us | 19,890.467 us | 20,062.533 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.566 us | 2,789.240 us | 0.07x | 0.07–0.07x | -2603.836 us | 9 pairs; 40/42 ms; 214/15 ops/lane; IQR 1.003/1.008 | `Pcre2SingleTokenRepeat` | 182.977 us | 1,016.752 us | 1,434.055 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,561.142 us | 20,093.950 us | 0.08x | 0.08–0.08x | -18539.888 us | 9 pairs; 41/40 ms; 26/2 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 583.524 us | 10,627.040 us | 10,899.340 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.137 us | 1.098 us | 0.12x | 0.12–0.12x | -0.961 us | 9 pairs; 40/40 ms; 292,335/36,360 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0.729 us | 0.259 us | 0.280 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.523 us | 0.831 us | 0.63x | 0.63–0.64x | -0.307 us | 9 pairs; 39/40 ms; 75,304/47,920 ops/lane; IQR 1.005/1.001 | `Pcre2Literal` | 0.160 us | 0.149 us | 0.172 us | 3.04x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.295 us | 1.183 us | 0.25x | 0.25–0.25x | -0.888 us | 9 pairs; 39/40 ms; 131,947/33,788 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 1.075 us | 0.356 us | 0.383 us | 0.77x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.931 us | 1.127 us | 0.83x | 0.82–0.83x | -0.197 us | 9 pairs; 41/40 ms; 43,622/35,495 ops/lane; IQR 1.001/1.004 | `Pcre2Character` | 0.285 us | 0.433 us | 0.473 us | 1.97x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.150 us | 0.855 us | 0.18x | 0.17–0.18x | -0.705 us | 9 pairs; 40/40 ms; 267,251/47,091 ops/lane; IQR 1.002/1.004 | `Pcre2Slice` | 0.917 us | 0.156 us | 0.203 us | 0.74x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.961 us | 1.125 us | 0.85x | 0.85–0.86x | -0.164 us | 9 pairs; 40/40 ms; 41,708/35,552 ops/lane; IQR 1.003/1.000 | `Pcre2Slice` | 4.481 us | 0.307 us | 0.326 us | 2.95x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.813 us | 0.833 us | 0.97x | 0.97–0.98x | -0.021 us | 9 pairs; 40/40 ms; 49,590/48,150 ops/lane; IQR 1.003/1.004 | `Pcre2Literal` | 3.584 us | 0.182 us | 0.203 us | 3.99x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.039 us | 1.191 us | 0.87x | 0.87–0.88x | -0.152 us | 9 pairs; 41/40 ms; 39,879/33,318 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 5.217 us | 0.416 us | 0.440 us | 2.36x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.206 us | 1.135 us | 1.06x | 1.06–1.07x | +0.071 us | 9 pairs; 40/40 ms; 32,846/35,422 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 3.667 us | 0.484 us | 0.518 us | 2.33x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.800 us | 0.861 us | 0.93x | 0.93–0.93x | -0.062 us | 9 pairs; 40/40 ms; 49,842/46,604 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 2.171 us | 0.187 us | 0.225 us | 3.55x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.566 us | 1.122 us | 0.50x | 0.50–0.51x | -0.556 us | 9 pairs; 39/40 ms; 69,394/35,586 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.510 us | 0.834 us | 0.61x | 0.61–0.61x | -0.324 us | 9 pairs; 40/40 ms; 79,234/48,443 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.727 us | 1.192 us | 0.61x | 0.61–0.61x | -0.465 us | 9 pairs; 38/40 ms; 52,761/33,601 ops/lane; IQR 1.001/1.000 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.177 us | 1.135 us | 1.04x | 1.03–1.04x | +0.043 us | 9 pairs; 50/40 ms; 42,391/35,466 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.494 us | 0.861 us | 0.57x | 0.57–0.58x | -0.368 us | 9 pairs; 40/40 ms; 81,115/46,352 ops/lane; IQR 1.006/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.597 us | — | — | — | — | — | `—` | 35.746 us | 99.205 us | 100.738 us | 0.61x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.563 us | — | — | — | — | — | `—` | 2.209 us | 0.360 us | 0.385 us | 17.04x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.080 us | — | — | — | — | — | `—` | 0.946 us | 0.243 us | 0.271 us | 3.98x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.246 us | — | — | — | — | — | `—` | 3.087 us | 0.461 us | 0.496 us | 2.51x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.522 us | — | — | — | — | — | `—` | 1.061 us | 0.547 us | 0.589 us | 2.58x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.459 us | — | — | — | — | — | `—` | 1.598 us | 0.270 us | 0.322 us | 13.87x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.091 us | 0.083 us | 1.09x | 1.09–1.16x | +0.008 us | 9 pairs; 41/39 ms; 448,324/474,532 ops/lane; IQR 1.088/1.022 | `Pcre2Character` | 0 B | 0 B | 2.281 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.120 us | 0.11x | 0.11–0.11x | -0.107 us | 9 pairs; 39/39 ms; 3,007,158/329,611 ops/lane; IQR 1.006/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 129.219 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.381 us | 0.108 us | 3.56x | 3.52–3.58x | +0.273 us | 9 pairs; 36/41 ms; 95,705/379,447 ops/lane; IQR 1.012/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 20.413 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.394 us | 0.120 us | 3.30x | 3.29–3.31x | +0.275 us | 9 pairs; 36/41 ms; 91,744/340,230 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 71.719 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.392 us | 0.106 us | 3.69x | 3.66–3.70x | +0.286 us | 9 pairs; 36/40 ms; 91,892/377,813 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 70.044 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.580 us | 0.347 us | 1.67x | 1.67–1.68x | +0.233 us | 9 pairs; 38/40 ms; 66,238/113,715 ops/lane; IQR 1.006/1.008 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.673 us | 0.02x | 0.02–0.02x | -0.661 us | 9 pairs; 38/40 ms; 3,317,439/59,636 ops/lane; IQR 1.007/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 23.794 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.529 us | 0.02x | 0.02–0.02x | -0.517 us | 9 pairs; 40/40 ms; 3,425,674/74,987 ops/lane; IQR 1.017/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 22.556 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.467 us | 0.108 us | 4.36x | 4.30–4.37x | +0.360 us | 9 pairs; 37/41 ms; 78,750/375,521 ops/lane; IQR 1.007/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 18.837 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.435 us | 10.493 us | 0.80x | 0.79–0.81x | -2.096 us | 9 pairs; 39/39 ms; 4,622/3,691 ops/lane; IQR 1.006/1.007 | `Pcre2Literal` | 0 B | 0 B | 0.831 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.301 us | 1.049 us | 0.29x | 0.29–0.29x | -0.747 us | 9 pairs; 41/40 ms; 135,322/37,901 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 132.231 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.319 us | 0.468 us | 0.68x | 0.68–0.68x | -0.149 us | 9 pairs; 36/41 ms; 113,071/87,103 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 116.331 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.293 us | 0.476 us | 0.61x | 0.61–0.62x | -0.184 us | 9 pairs; 38/39 ms; 129,573/82,259 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 142.219 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.305 us | 0.585 us | 0.52x | 0.52–0.52x | -0.280 us | 9 pairs; 35/40 ms; 115,935/68,026 ops/lane; IQR 1.011/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 102.287 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.284 us | 0.397 us | 0.71x | 0.71–0.73x | -0.113 us | 9 pairs; 35/40 ms; 124,504/101,713 ops/lane; IQR 1.001/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 214.650 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.844 us | 8.820 us | 0.55x | 0.55–0.55x | -3.985 us | 9 pairs; 41/40 ms; 8,402/4,538 ops/lane; IQR 1.003/1.001 | `Pcre2Character` | 0 B | 0 B | 1.525 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.262 us | 0.313 us | 0.84x | 0.83–0.84x | -0.050 us | 9 pairs; 37/40 ms; 140,977/127,538 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 92.200 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.273 us | 0.402 us | 0.68x | 0.67–0.68x | -0.130 us | 9 pairs; 37/39 ms; 134,769/97,822 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 123.869 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.815 us | — | — | — | — | — | `—` | 0 B | — | 14.519 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.186 us | 0.294 us | 0.63x | 0.63–0.64x | -0.107 us | 9 pairs; 37/40 ms; 199,759/134,541 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 52.800 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.706 us | — | — | — | — | — | `—` | 0 B | — | 14.244 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.573 us | 0.677 us | 0.85x | 0.84–0.85x | -0.103 us | 9 pairs; 37/40 ms; 65,278/58,682 ops/lane; IQR 1.006/1.013 | `Pcre2Slice` | 0 B | 0 B | 63.638 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.873 us | 29.147 us | 0.34x | 0.34–0.34x | -19.263 us | 9 pairs; 40/42 ms; 4,048/1,431 ops/lane; IQR 1.001/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.951 us | 1.061 us | 0.90x | 0.89–0.90x | -0.109 us | 9 pairs; 40/41 ms; 41,937/38,339 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 129.781 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.021 us | 1.200 us | 0.85x | 0.85–0.86x | -0.179 us | 9 pairs; 41/40 ms; 39,854/33,151 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 114.731 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.939 us | 1.036 us | 0.91x | 0.90–0.91x | -0.097 us | 9 pairs; 40/40 ms; 42,241/38,945 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 142.175 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.947 us | 1.171 us | 0.81x | 0.81–0.81x | -0.224 us | 9 pairs; 40/39 ms; 41,927/33,589 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 105.394 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.947 us | 0.945 us | 1.00x | 1.00–1.01x | +0.001 us | 9 pairs; 40/40 ms; 42,563/41,966 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 213.850 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.567 us | 8.868 us | 0.63x | 0.63–0.63x | -3.297 us | 9 pairs; 40/40 ms; 7,169/4,507 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 0 B | 0 B | 1.538 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.948 us | 0.889 us | 1.07x | 1.06–1.07x | +0.058 us | 9 pairs; 41/40 ms; 43,310/44,723 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 91.412 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.929 us | 0.964 us | 0.96x | 0.96–0.97x | -0.035 us | 9 pairs; 40/40 ms; 43,192/41,518 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 125.150 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.131 us | — | — | — | — | — | `—` | 0 B | — | 14.794 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.842 us | 0.870 us | 0.97x | 0.97–0.97x | -0.027 us | 9 pairs; 40/40 ms; 47,991/46,277 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.288 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.010 us | — | — | — | — | — | `—` | 0 B | — | 14.156 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.849 us | 1.259 us | 0.67x | 0.67–0.68x | -0.411 us | 9 pairs; 40/41 ms; 46,765/32,405 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | 0 B | 0 B | 63.438 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.384 us | 2.292 us | 0.60x | 0.60–0.60x | -0.909 us | 9 pairs; 40/40 ms; 29,207/17,476 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.658 us | 1.058 us | 0.62x | 0.62–0.62x | -0.399 us | 9 pairs; 41/40 ms; 61,875/37,865 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 129.050 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.690 us | 1.199 us | 0.58x | 0.57–0.58x | -0.509 us | 9 pairs; 40/40 ms; 58,309/33,313 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 115.513 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.666 us | 1.029 us | 0.65x | 0.64–0.65x | -0.364 us | 9 pairs; 40/40 ms; 60,592/38,922 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 141.213 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.663 us | 1.169 us | 0.57x | 0.56–0.57x | -0.508 us | 9 pairs; 40/41 ms; 59,677/34,690 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 103.306 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.620 us | 0.947 us | 0.65x | 0.65–0.66x | -0.327 us | 9 pairs; 40/41 ms; 64,110/43,323 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 210.444 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.818 us | 2.197 us | 0.83x | 0.83–0.83x | -0.377 us | 9 pairs; 41/40 ms; 22,402/18,239 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 0 B | 0 B | 1.406 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.613 us | 0.886 us | 0.69x | 0.69–0.70x | -0.272 us | 9 pairs; 40/39 ms; 65,916/44,427 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 92.656 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.613 us | 0.966 us | 0.64x | 0.63–0.64x | -0.352 us | 9 pairs; 40/40 ms; 64,491/41,483 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 122.306 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.805 us | — | — | — | — | — | `—` | 0 B | — | 13.494 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.491 us | 0.870 us | 0.56x | 0.56–0.57x | -0.379 us | 9 pairs; 39/40 ms; 79,755/45,721 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 53.013 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.757 us | — | — | — | — | — | `—` | 0 B | — | 14.125 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.582 us | 1.251 us | 0.47x | 0.46–0.47x | -0.670 us | 9 pairs; 40/40 ms; 68,924/31,950 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.737 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.657 us | — | — | — | — | — | `—` | 480 B | — | 0.781 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.137 us | — | — | — | — | — | `—` | 48 B | — | 136.044 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.243 us | — | — | — | — | — | `—` | 48 B | — | 114.775 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.122 us | — | — | — | — | — | `—` | 48 B | — | 140.113 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.137 us | — | — | — | — | — | `—` | 48 B | — | 100.850 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.121 us | — | — | — | — | — | `—` | 48 B | — | 219.950 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.817 us | — | — | — | — | — | `—` | 64 B | — | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.135 us | — | — | — | — | — | `—` | 40 B | — | 95.075 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.137 us | — | — | — | — | — | `—` | 40 B | — | 121.381 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.917 us | — | — | — | — | — | `—` | 1,216 B | — | 13.606 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.082 us | — | — | — | — | — | `—` | 48 B | — | 53.306 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.868 us | — | — | — | — | — | `—` | 928 B | — | 13.931 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.955 us | — | — | — | — | — | `—` | 28,928 B | — | 64.100 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.180–224.215 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.516–95.925 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.373–14.792 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.294–50.108 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.035–1.474 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.494–42.406 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.691–22.348 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.256–100.055 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.153–72.631 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.200–5.505 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.555–379.180 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 40.450–314.443 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.835–5.125 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.178–15.823 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 174.809–1,375.622 us |

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
