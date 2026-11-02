<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `294027CCC3291AF426FA4C397FECB82AC91D3463D3E37FBA4795E9F1A7D83019`
- Latest managed row measurement: `2026-11-02T21:41:17.9580847+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-02T21:41:17.9580847+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `77` managed faster, `1` equivalent, `11` native faster, `12` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `16839b38bce3` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `16839b38bce3` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Managed faster** | 4,103 B | 0.887 us | 2.157 us | 0.41x | 0.39–0.44x | -1.270 us | 9 pairs; 34/37 ms; 38,102/17,197 ops/lane; IQR 1.009/1.051 | `Pcre2Slice` | 520.650 us | 0.280 us | 0.842 us | 1.05x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.100 us | 11.180 us | 0.10x | 0.08–0.10x | -10.068 us | 9 pairs; 37/47 ms; 34,094/4,204 ops/lane; IQR 1.013/1.251 | `Pcre2Slice` | 1.665 us | 0.097 us | 3.560 us | 0.31x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.230 us | 0.01x | 0.01–0.01x | -1.219 us | 9 pairs; 39/39 ms; 3,549,361/31,770 ops/lane; IQR 1.011/1.009 | `Pcre2Slice` | 0.176 us | 1.277 us | 1.295 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.523 us | 0.158 us | 9.61x | 9.53–9.69x | +1.366 us | 9 pairs; 39/41 ms; 25,378/259,278 ops/lane; IQR 1.006/1.013 | `Pcre2Backtracking` | 0.046 us | 0.124 us | 0.157 us | 9.72x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 5.283 us | 0.577 us | 9.14x | 9.07–9.21x | +4.705 us | 9 pairs; 39/40 ms; 7,473/69,140 ops/lane; IQR 1.006/1.011 | `Pcre2Backtracking` | 0.127 us | 0.776 us | 0.804 us | 6.57x | 0 B | 0 B |
| `common/email-match` | **Inconclusive** | 34 B | 0.177 us | 0.297 us | 0.60x | 0.52–0.60x | -0.120 us | 9 pairs; 47/40 ms; 263,025/134,269 ops/lane; IQR 1.146/1.017 | `Pcre2Slice` | 0.158 us | 0.327 us | 0.354 us | 0.50x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.170 us | 0.358 us | 0.48x | 0.47–0.50x | -0.187 us | 9 pairs; 38/41 ms; 224,888/113,472 ops/lane; IQR 1.048/1.002 | `Pcre2Slice` | 0.165 us | 0.462 us | 0.478 us | 0.36x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.108 us | 0.292 us | 0.37x | 0.37–0.37x | -0.183 us | 9 pairs; 40/39 ms; 365,163/133,912 ops/lane; IQR 1.004/1.006 | `Pcre2Slice` | 0.113 us | 0.252 us | 0.269 us | 0.40x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.111 us | 0.309 us | 0.36x | 0.36–0.36x | -0.198 us | 9 pairs; 41/40 ms; 364,329/128,751 ops/lane; IQR 1.018/1.006 | `Pcre2Slice` | 0.113 us | 0.267 us | 0.285 us | 0.39x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.076 us | 2.28x | 2.26–2.31x | +0.098 us | 9 pairs; 39/40 ms; 226,348/527,031 ops/lane; IQR 1.008/1.023 | `Pcre2Slice` | 0.164 us | 1.049 us | 1.068 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.198 us | 0.17x | 0.17–0.17x | -0.165 us | 9 pairs; 40/39 ms; 1,183,571/196,545 ops/lane; IQR 1.004/1.013 | `Pcre2Backtracking` | 0.174 us | 0.316 us | 0.358 us | 0.09x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.612 us | 0.10x | 0.10–0.10x | -0.551 us | 9 pairs; 40/40 ms; 657,278/65,011 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0.168 us | 0.251 us | 0.267 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.157 us | 0.21x | 0.21–0.22x | -0.123 us | 9 pairs; 37/41 ms; 1,106,013/264,771 ops/lane; IQR 1.022/1.003 | `Pcre2Backtracking` | 0.103 us | 0.182 us | 0.202 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.112 us | 0.19x | 0.18–0.19x | -0.092 us | 9 pairs; 41/42 ms; 1,952,655/369,353 ops/lane; IQR 1.018/1.008 | `Pcre2Backtracking` | 0.059 us | 0.091 us | 0.110 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.122 us | 0.48x | 0.46–0.48x | -0.064 us | 9 pairs; 40/42 ms; 689,188/340,860 ops/lane; IQR 1.002/1.024 | `Pcre2Backtracking` | 0.014 us | 0.093 us | 0.112 us | 0.52x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.188 us | 0.280 us | 0.67x | 0.66–0.68x | -0.093 us | 9 pairs; 41/40 ms; 220,318/143,228 ops/lane; IQR 1.019/1.012 | `Pcre2Slice` | 0.184 us | 0.118 us | 0.150 us | 1.26x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.115 us | 0.22x | 0.20–0.22x | -0.090 us | 9 pairs; 42/40 ms; 1,699,530/348,246 ops/lane; IQR 1.058/1.023 | `Pcre2Backtracking` | 0.019 us | 0.101 us | 0.118 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.218 us | 0.05x | 0.05–0.06x | -2.100 us | 9 pairs; 41/56 ms; 350,085/25,129 ops/lane; IQR 1.005/1.129 | `Pcre2Literal` | 0.096 us | 0.051 us | 0.606 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 1.799 us | 0.07x | 0.06–0.07x | -1.677 us | 9 pairs; 40/41 ms; 325,307/22,717 ops/lane; IQR 1.006/1.079 | `Pcre2Literal` | 0.154 us | 0.054 us | 0.633 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.155 us | 2.088 us | 0.08x | 0.07–0.08x | -1.931 us | 9 pairs; 40/34 ms; 259,774/16,122 ops/lane; IQR 1.005/1.058 | `Pcre2Literal` | 0.218 us | 0.234 us | 0.847 us | 0.18x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.151 us | 1.853 us | 0.08x | 0.08–0.08x | -1.701 us | 9 pairs; 40/34 ms; 263,388/18,597 ops/lane; IQR 1.006/1.040 | `Pcre2Literal` | 0.189 us | 0.205 us | 0.756 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.097 us | 0.09x | 0.09–0.09x | -0.088 us | 9 pairs; 40/39 ms; 4,490,882/404,806 ops/lane; IQR 1.004/1.019 | `Pcre2Slice` | 0.926 us | 0.070 us | 0.090 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.087 us | 0.78x | 0.75–0.78x | -0.019 us | 9 pairs; 40/38 ms; 593,230/438,834 ops/lane; IQR 1.006/1.013 | `Pcre2Literal` | 0.134 us | 0.051 us | 0.073 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.13–0.13x | -0.094 us | 9 pairs; 41/40 ms; 2,863,606/366,380 ops/lane; IQR 1.021/1.018 | `Pcre2Backtracking` | 0.244 us | 0.092 us | 0.114 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.121 us | 0.122 us | 0.98x | 0.96–1.00x | -0.002 us | 9 pairs; 42/44 ms; 344,575/360,756 ops/lane; IQR 1.034/1.017 | `Pcre2Character` | 0.139 us | 0.115 us | 0.144 us | 0.84x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.206 us | 0.37x | 0.36–0.37x | -0.130 us | 9 pairs; 41/40 ms; 540,128/195,110 ops/lane; IQR 1.003/1.009 | `Pcre2Slice` | 0.543 us | 0.076 us | 0.106 us | 0.72x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.345 us | 160.171 us | 0.51x | 0.51–0.52x | -77.853 us | 9 pairs; 40/40 ms; 485/252 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | 417.463 us | 106.634 us | 108.636 us | 0.76x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.713 us | 281.620 us | 0.13x | 0.13–0.13x | -243.902 us | 9 pairs; 40/40 ms; 1,063/141 ops/lane; IQR 1.008/1.004 | `Pcre2Character` | 394.317 us | 228.914 us | 242.879 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.566 us | 209.282 us | 0.05x | 0.05–0.05x | -198.731 us | 9 pairs; 39/40 ms; 3,685/192 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 7.928 us | 236.152 us | 233.608 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 240.410 us | 214.284 us | 1.12x | 1.12–1.12x | +25.913 us | 9 pairs; 40/40 ms; 168/187 ops/lane; IQR 1.003/1.003 | `Pcre2SingleTokenRepeat` | 7.891 us | 245.439 us | 244.456 us | 0.98x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.548 us | 24.216 us | 0.15x | 0.14–0.15x | -20.639 us | 9 pairs; 40/42 ms; 11,170/1,718 ops/lane; IQR 1.001/1.018 | `Pcre2Literal` | 1.380 us | 4.275 us | 6.040 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.601 us | 81.935 us | 0.12x | 0.12–0.12x | -72.343 us | 9 pairs; 40/40 ms; 4,165/491 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 9.324 us | 95.094 us | 95.567 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,184.500 us | 28,391.300 us | 0.15x | 0.14–0.15x | -24240.114 us | 9 pairs; 29/28 ms; 7/1 ops/lane; IQR 1.013/1.046 | `Pcre2Backtracking` | 5,458.350 us | 12,614.400 us | 19,331.633 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 5,787.914 us | 55,423.400 us | 0.10x | 0.10–0.11x | -49713.614 us | 9 pairs; 41/55 ms; 7/1 ops/lane; IQR 1.021/1.012 | `Pcre2Backtracking` | 6,310.300 us | 45,304.250 us | 52,274.600 us | 0.11x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,948.333 us | 491,627.800 us | 0.00x | 0.00–0.00x | -489674.000 us | 9 pairs; 41/492 ms; 21/1 ops/lane; IQR 1.038/1.003 | `Pcre2Character` | 1,825.007 us | 23,132.667 us | 30,015.450 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,571.217 us | 10,491.400 us | 0.53x | 0.48–0.55x | -4953.067 us | 9 pairs; 33/42 ms; 6/4 ops/lane; IQR 1.006/1.110 | `Pcre2Literal` | 1,368.197 us | 1,356.865 us | 8,351.533 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,895.550 us | 145,310.400 us | 0.13x | 0.13–0.13x | -126504.550 us | 9 pairs; 38/145 ms; 2/1 ops/lane; IQR 1.012/1.004 | `Pcre2Backtracking` | 1,597.812 us | 514.957 us | 8,532.000 us | 2.21x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,748.475 us | 22,027.400 us | 0.22x | 0.21–0.22x | -17264.450 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.006/1.011 | `Pcre2Slice` | 2,851.528 us | 11,373.200 us | 19,344.133 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,445.750 us | 133,385.600 us | 0.17x | 0.17–0.17x | -111178.450 us | 9 pairs; 45/133 ms; 2/1 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 1,897.104 us | 2,342.086 us | 10,437.300 us | 2.15x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 234.941 us | 948.002 us | 0.25x | 0.24–0.25x | -712.738 us | 9 pairs; 40/41 ms; 170/43 ops/lane; IQR 1.003/1.038 | `Pcre2Backtracking` | 283.374 us | 351.560 us | 578.104 us | 0.41x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,158.100 us | 13,414.633 us | 0.91x | 0.90–0.91x | -1259.767 us | 9 pairs; 36/40 ms; 3/3 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 313.209 us | 16,380.975 us | 16,596.175 us | 0.73x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 3,014.446 us | 98,516.700 us | 0.03x | 0.03–0.03x | -95489.477 us | 9 pairs; 39/99 ms; 13/1 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 1,890.681 us | 19,911.433 us | 20,152.367 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.072 us | 2,746.436 us | 0.07x | 0.07–0.07x | -2560.796 us | 9 pairs; 40/38 ms; 214/14 ops/lane; IQR 1.007/1.016 | `Pcre2SingleTokenRepeat` | 187.954 us | 1,005.582 us | 1,248.706 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,556.064 us | 20,071.250 us | 0.08x | 0.08–0.08x | -18508.978 us | 9 pairs; 39/40 ms; 25/2 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 580.258 us | 10,863.200 us | 10,989.720 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.137 us | 1.100 us | 0.12x | 0.12–0.12x | -0.962 us | 9 pairs; 40/40 ms; 291,061/36,529 ops/lane; IQR 1.005/1.001 | `Pcre2Slice` | 1.410 us | 0.258 us | 0.282 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.513 us | 0.833 us | 0.62x | 0.61–0.62x | -0.319 us | 9 pairs; 40/40 ms; 78,490/48,117 ops/lane; IQR 1.011/1.002 | `Pcre2Literal` | 0.166 us | 0.150 us | 0.173 us | 2.97x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.301 us | 1.176 us | 0.26x | 0.25–0.26x | -0.876 us | 9 pairs; 41/40 ms; 135,700/33,823 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 1.056 us | 0.347 us | 0.378 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.872 us | 1.129 us | 0.77x | 0.77–0.77x | -0.256 us | 9 pairs; 40/40 ms; 45,518/35,566 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 0.286 us | 0.441 us | 0.487 us | 1.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.156 us | 0.856 us | 0.18x | 0.18–0.18x | -0.701 us | 9 pairs; 40/40 ms; 257,560/46,856 ops/lane; IQR 1.001/1.003 | `Pcre2Slice` | 1.009 us | 0.157 us | 0.200 us | 0.78x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.955 us | 1.106 us | 0.86x | 0.86–0.87x | -0.150 us | 9 pairs; 40/39 ms; 42,349/35,720 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 6.368 us | 0.306 us | 0.323 us | 2.95x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.813 us | 0.838 us | 0.97x | 0.96–0.98x | -0.025 us | 9 pairs; 39/40 ms; 48,045/48,039 ops/lane; IQR 1.004/1.002 | `Pcre2Literal` | 3.535 us | 0.181 us | 0.204 us | 3.99x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.983 us | 1.182 us | 0.83x | 0.83–0.84x | -0.197 us | 9 pairs; 40/40 ms; 40,593/33,812 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 5.264 us | 0.405 us | 0.425 us | 2.32x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.210 us | 1.137 us | 1.06x | 1.06–1.07x | +0.072 us | 9 pairs; 41/40 ms; 34,140/35,177 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 3.626 us | 0.491 us | 0.527 us | 2.30x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.800 us | 0.861 us | 0.93x | 0.92–0.93x | -0.063 us | 9 pairs; 40/40 ms; 49,787/46,890 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 2.114 us | 0.182 us | 0.224 us | 3.57x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.577 us | 1.105 us | 0.52x | 0.52–0.52x | -0.528 us | 9 pairs; 41/40 ms; 70,929/35,894 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.538 us | 0.836 us | 0.64x | 0.64–0.65x | -0.297 us | 9 pairs; 43/40 ms; 79,192/48,069 ops/lane; IQR 1.002/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.724 us | 1.182 us | 0.61x | 0.61–0.61x | -0.458 us | 9 pairs; 40/37 ms; 55,143/30,954 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.929 us | 1.144 us | 0.81x | 0.81–0.82x | -0.215 us | 9 pairs; 40/40 ms; 43,556/35,326 ops/lane; IQR 1.009/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.493 us | 0.874 us | 0.56x | 0.56–0.56x | -0.382 us | 9 pairs; 41/41 ms; 82,597/47,100 ops/lane; IQR 1.002/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 63.291 us | — | — | — | — | — | `—` | 69.453 us | 101.300 us | 103.126 us | 0.61x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.728 us | — | — | — | — | — | `—` | 2.684 us | 0.363 us | 0.384 us | 25.30x | 48,784 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.050 us | — | — | — | — | — | `—` | 0.830 us | 0.247 us | 0.276 us | 3.81x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.190 us | — | — | — | — | — | `—` | 2.994 us | 0.539 us | 0.536 us | 2.22x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.534 us | — | — | — | — | — | `—` | 0.928 us | 0.554 us | 0.601 us | 2.55x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.533 us | — | — | — | — | — | `—` | 1.506 us | 0.271 us | 0.318 us | 14.25x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.089 us | 0.087 us | 1.02x | 1.02–1.03x | +0.002 us | 9 pairs; 40/39 ms; 444,282/450,179 ops/lane; IQR 1.005/1.006 | `Pcre2Character` | 0 B | 0 B | 2.275 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.122 us | 0.11x | 0.11–0.11x | -0.109 us | 9 pairs; 39/40 ms; 3,016,964/327,026 ops/lane; IQR 1.005/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 129.419 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.392 us | 0.107 us | 3.67x | 3.66–3.68x | +0.286 us | 9 pairs; 37/40 ms; 93,670/375,137 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 21.144 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.386 us | 0.118 us | 3.29x | 3.26–3.30x | +0.269 us | 9 pairs; 36/40 ms; 91,991/338,522 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 71.725 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.390 us | 0.101 us | 3.85x | 3.83–3.86x | +0.289 us | 9 pairs; 35/39 ms; 90,073/387,060 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 72.013 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.577 us | 0.345 us | 1.68x | 1.67–1.68x | +0.233 us | 9 pairs; 37/42 ms; 63,403/120,512 ops/lane; IQR 1.007/1.011 | `Pcre2Character` | 0 B | 0 B | 2.131 us | 2,248 B |
| `pcre2/recursive-optional` | **Inconclusive** | 6 B | 0.016 us | 0.628 us | 0.03x | 0.02–0.03x | -0.611 us | 9 pairs; 56/37 ms; 3,428,213/59,012 ops/lane; IQR 1.230/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 23.306 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.012 us | 0.508 us | 0.02x | 0.02–0.02x | -0.496 us | 9 pairs; 41/38 ms; 3,501,634/74,673 ops/lane; IQR 1.022/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 22.400 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.458 us | 0.111 us | 4.13x | 4.11–4.14x | +0.347 us | 9 pairs; 36/41 ms; 79,615/366,884 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 17.881 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.387 us | 10.445 us | 0.80x | 0.79–0.81x | -2.080 us | 9 pairs; 36/40 ms; 4,243/3,844 ops/lane; IQR 1.009/1.011 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.296 us | 1.051 us | 0.28x | 0.28–0.28x | -0.757 us | 9 pairs; 38/42 ms; 128,029/39,594 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 130.062 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.309 us | 0.470 us | 0.66x | 0.65–0.66x | -0.161 us | 9 pairs; 37/40 ms; 120,701/84,630 ops/lane; IQR 1.001/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 114.281 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.291 us | 0.488 us | 0.60x | 0.59–0.61x | -0.196 us | 9 pairs; 35/38 ms; 121,838/78,818 ops/lane; IQR 1.019/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 143.225 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.295 us | 0.593 us | 0.50x | 0.49–0.50x | -0.297 us | 9 pairs; 34/40 ms; 113,984/66,969 ops/lane; IQR 1.005/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 103.812 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.285 us | 0.412 us | 0.69x | 0.68–0.71x | -0.126 us | 9 pairs; 33/40 ms; 116,734/98,174 ops/lane; IQR 1.004/1.028 | `Pcre2Backtracking` | 0 B | 0 B | 216.713 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.846 us | 8.866 us | 0.55x | 0.55–0.55x | -4.018 us | 9 pairs; 40/40 ms; 8,226/4,536 ops/lane; IQR 1.002/1.004 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.262 us | 0.311 us | 0.84x | 0.84–0.85x | -0.050 us | 9 pairs; 37/39 ms; 140,687/126,814 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 92.644 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.263 us | 0.395 us | 0.67x | 0.66–0.67x | -0.131 us | 9 pairs; 36/40 ms; 136,735/100,723 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 122.606 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.641 us | — | — | — | — | — | `—` | 0 B | — | 14.162 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.189 us | 0.286 us | 0.66x | 0.65–0.67x | -0.097 us | 9 pairs; 33/39 ms; 175,267/136,086 ops/lane; IQR 1.009/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 53.200 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.529 us | — | — | — | — | — | `—` | 0 B | — | 14.075 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.555 us | 0.679 us | 0.82x | 0.81–0.82x | -0.123 us | 9 pairs; 37/40 ms; 66,469/58,389 ops/lane; IQR 1.012/1.003 | `Pcre2Slice` | 0 B | 0 B | 64.169 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.835 us | 28.986 us | 0.34x | 0.34–0.34x | -19.147 us | 9 pairs; 40/40 ms; 4,095/1,372 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.978 us | 1.053 us | 0.93x | 0.92–0.93x | -0.076 us | 9 pairs; 41/40 ms; 41,592/38,433 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 131.031 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.003 us | 1.218 us | 0.82x | 0.82–0.83x | -0.216 us | 9 pairs; 38/42 ms; 38,106/34,203 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 116.094 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.938 us | 1.055 us | 0.89x | 0.89–0.89x | -0.117 us | 9 pairs; 39/40 ms; 41,554/37,924 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 140.738 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.953 us | 1.169 us | 0.81x | 0.81–0.82x | -0.217 us | 9 pairs; 41/41 ms; 42,575/34,656 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 104.131 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.948 us | 0.946 us | 1.00x | 1.00–1.01x | +0.002 us | 9 pairs; 40/40 ms; 42,582/42,066 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 214.375 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.532 us | 8.912 us | 0.62x | 0.62–0.62x | -3.370 us | 9 pairs; 40/40 ms; 7,267/4,483 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.954 us | 0.892 us | 1.07x | 1.07–1.07x | +0.061 us | 9 pairs; 41/40 ms; 43,323/45,193 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 91.581 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.928 us | 0.960 us | 0.97x | 0.96–0.97x | -0.032 us | 9 pairs; 40/40 ms; 43,195/41,299 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 123.225 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.918 us | — | — | — | — | — | `—` | 0 B | — | 13.650 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.830 us | 0.856 us | 0.97x | 0.96–0.97x | -0.026 us | 9 pairs; 40/40 ms; 48,506/46,788 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.294 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.790 us | — | — | — | — | — | `—` | 0 B | — | 14.006 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.859 us | 1.262 us | 0.68x | 0.68–0.68x | -0.404 us | 9 pairs; 40/42 ms; 46,938/33,394 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | 0 B | 0 B | 64.650 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.386 us | 2.290 us | 0.60x | 0.60–0.61x | -0.906 us | 9 pairs; 41/40 ms; 29,427/17,424 ops/lane; IQR 1.003/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.627 us | 1.054 us | 0.59x | 0.59–0.60x | -0.427 us | 9 pairs; 38/40 ms; 61,040/37,854 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 131.537 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.683 us | 1.220 us | 0.56x | 0.56–0.56x | -0.537 us | 9 pairs; 40/41 ms; 58,525/33,198 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 116.094 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.675 us | 1.053 us | 0.64x | 0.64–0.64x | -0.378 us | 9 pairs; 41/40 ms; 60,212/37,838 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 142.644 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.637 us | 1.167 us | 0.54x | 0.54–0.55x | -0.532 us | 9 pairs; 40/40 ms; 62,659/33,966 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 104.450 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.668 us | 0.944 us | 0.71x | 0.70–0.71x | -0.279 us | 9 pairs; 37/41 ms; 55,501/42,986 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 214.488 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.856 us | 2.200 us | 0.84x | 0.84–0.85x | -0.344 us | 9 pairs; 42/40 ms; 22,401/18,224 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.627 us | 0.889 us | 0.70x | 0.70–0.71x | -0.263 us | 9 pairs; 38/39 ms; 60,616/44,424 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 94.537 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.633 us | 0.967 us | 0.66x | 0.65–0.66x | -0.333 us | 9 pairs; 40/40 ms; 62,878/41,634 ops/lane; IQR 1.008/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 122.513 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.589 us | — | — | — | — | — | `—` | 0 B | — | 13.969 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.494 us | 0.861 us | 0.57x | 0.57–0.58x | -0.366 us | 9 pairs; 40/40 ms; 79,953/46,100 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.831 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.491 us | — | — | — | — | — | `—` | 0 B | — | 13.931 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.528 us | 1.267 us | 0.42x | 0.42–0.42x | -0.739 us | 9 pairs; 40/41 ms; 75,516/32,049 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | 0 B | 0 B | 64.456 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.182 us | — | — | — | — | — | `—` | 480 B | — | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.111 us | — | — | — | — | — | `—` | 48 B | — | 130.606 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.211 us | — | — | — | — | — | `—` | 48 B | — | 116.731 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.099 us | — | — | — | — | — | `—` | 48 B | — | 142.637 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.109 us | — | — | — | — | — | `—` | 48 B | — | 101.619 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.094 us | — | — | — | — | — | `—` | 48 B | — | 221.700 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.992 us | — | — | — | — | — | `—` | 64 B | — | 1.381 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.111 us | — | — | — | — | — | `—` | 40 B | — | 89.994 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.112 us | — | — | — | — | — | `—` | 40 B | — | 124.256 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.608 us | — | — | — | — | — | `—` | 1,216 B | — | 13.688 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.040 us | — | — | — | — | — | `—` | 48 B | — | 53.250 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.618 us | — | — | — | — | — | `—` | 928 B | — | 14.594 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.942 us | — | — | — | — | — | `—` | 28,928 B | — | 63.950 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.549–219.166 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.503–94.971 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.214–12.687 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.238–49.177 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.036–1.503 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.318–41.210 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.777–22.719 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.235–99.841 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.124–72.642 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.225–5.750 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 49.757–390.019 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.077 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.053–305.821 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.879–5.176 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.226–140.342 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 174.954–1,364.816 us |

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
