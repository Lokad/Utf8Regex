<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `DE3D3BD5F99547E77B6D71302619DA6AFB6A8AAE7D8FF901DD2B8EB4296B19D9`
- Latest managed row measurement: `2026-11-06T09:38:54.3133229+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T09:38:54.3133229+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `74` managed faster, `2` equivalent, `13` native faster, `12` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `1d5b0b80453f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `1d5b0b80453f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.631 us | 2.144 us | 0.29x | 0.26–0.30x | -1.511 us | 9 pairs; 36/38 ms; 57,859/17,735 ops/lane; IQR 1.010/1.057 | `Pcre2Slice` | 485.499 us | 0.282 us | 0.905 us | 0.70x | 928 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.834 us | 12.387 us | 0.07x | 0.06–0.08x | -11.545 us | 9 pairs; 37/55 ms; 44,451/4,437 ops/lane; IQR 1.009/1.128 | `Pcre2Slice` | 0.631 us | 0.098 us | 3.898 us | 0.21x | 400 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.256 us | 0.01x | 0.01–0.01x | -1.245 us | 9 pairs; 40/40 ms; 3,645,446/31,502 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 0.128 us | 1.082 us | 1.122 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.664 us | 0.158 us | 4.20x | 4.19–4.23x | +0.506 us | 9 pairs; 37/39 ms; 55,449/247,018 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0.046 us | 0.120 us | 0.161 us | 4.13x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.320 us | 0.570 us | 4.07x | 4.05–4.09x | +1.747 us | 9 pairs; 40/39 ms; 17,302/69,224 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0.126 us | 0.707 us | 0.749 us | 3.10x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.300 us | 0.33x | 0.33–0.36x | -0.199 us | 9 pairs; 39/41 ms; 392,866/135,906 ops/lane; IQR 1.074/1.002 | `Pcre2Slice` | 0.136 us | 0.313 us | 0.342 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.358 us | 0.30x | 0.30–0.32x | -0.251 us | 9 pairs; 40/39 ms; 375,664/108,719 ops/lane; IQR 1.077/1.007 | `Pcre2Slice` | 0.139 us | 0.431 us | 0.459 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.085 us | 0.289 us | 0.30x | 0.29–0.30x | -0.204 us | 9 pairs; 40/40 ms; 468,391/136,807 ops/lane; IQR 1.003/1.010 | `Pcre2Slice` | 0.113 us | 0.226 us | 0.251 us | 0.34x | 0 B | 0 B |
| `common/ip-miss` | **Inconclusive** | 15 B | 0.081 us | 0.316 us | 0.26x | 0.25–0.26x | -0.235 us | 9 pairs; 41/38 ms; 510,270/119,819 ops/lane; IQR 1.050/1.005 | `Pcre2Slice` | 0.113 us | 0.242 us | 0.262 us | 0.31x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.172 us | 0.077 us | 2.24x | 2.19–2.29x | +0.095 us | 9 pairs; 40/40 ms; 230,889/515,359 ops/lane; IQR 1.006/1.039 | `Pcre2Slice` | 0.112 us | 0.918 us | 0.957 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.198 us | 0.17x | 0.17–0.17x | -0.164 us | 9 pairs; 40/40 ms; 1,184,916/204,739 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 0.132 us | 0.294 us | 0.335 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.618 us | 0.10x | 0.10–0.10x | -0.558 us | 9 pairs; 40/40 ms; 664,718/64,352 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 0.156 us | 0.220 us | 0.251 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.156 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 37/40 ms; 1,122,505/253,430 ops/lane; IQR 1.025/1.008 | `Pcre2Backtracking` | 0.107 us | 0.184 us | 0.205 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.113 us | 0.18x | 0.17–0.18x | -0.093 us | 9 pairs; 39/40 ms; 1,943,463/349,250 ops/lane; IQR 1.008/1.012 | `Pcre2Backtracking` | 0.070 us | 0.090 us | 0.109 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.114 us | 0.51x | 0.49–0.51x | -0.056 us | 9 pairs; 40/37 ms; 685,696/323,647 ops/lane; IQR 1.009/1.025 | `Pcre2Backtracking` | 0.014 us | 0.085 us | 0.103 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.092 us | 0.280 us | 0.33x | 0.31–0.33x | -0.189 us | 9 pairs; 43/39 ms; 468,715/139,340 ops/lane; IQR 1.034/1.010 | `Pcre2Slice` | 0.118 us | 0.118 us | 0.157 us | 0.59x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.119 us | 0.20x | 0.20–0.20x | -0.095 us | 9 pairs; 40/40 ms; 1,678,966/335,833 ops/lane; IQR 1.015/1.008 | `Pcre2Backtracking` | 0.019 us | 0.090 us | 0.109 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.117 us | 2.234 us | 0.05x | 0.05–0.06x | -2.116 us | 9 pairs; 37/54 ms; 315,450/24,142 ops/lane; IQR 1.001/1.319 | `Pcre2Literal` | 0.111 us | 0.051 us | 0.636 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.128 us | 2.097 us | 0.06x | 0.06–0.07x | -1.969 us | 9 pairs; 44/51 ms; 339,826/24,282 ops/lane; IQR 1.001/1.272 | `Pcre2Literal` | 0.165 us | 0.054 us | 0.641 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 1.976 us | 0.08x | 0.07–0.08x | -1.819 us | 9 pairs; 40/46 ms; 257,404/23,361 ops/lane; IQR 1.002/1.070 | `Pcre2Literal` | 0.220 us | 0.236 us | 0.881 us | 0.18x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.151 us | 2.001 us | 0.07x | 0.07–0.08x | -1.850 us | 9 pairs; 40/40 ms; 264,414/19,925 ops/lane; IQR 1.004/1.113 | `Pcre2Literal` | 0.194 us | 0.207 us | 0.851 us | 0.18x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.098 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 24/39 ms; 2,682,809/396,563 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 0.253 us | 0.068 us | 0.089 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.092 us | 0.74x | 0.74–0.74x | -0.024 us | 9 pairs; 37/40 ms; 550,994/435,431 ops/lane; IQR 1.001/1.002 | `Pcre2Literal` | 0.145 us | 0.051 us | 0.073 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.112 us | 0.12x | 0.12–0.12x | -0.099 us | 9 pairs; 39/40 ms; 2,893,635/352,669 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0.294 us | 0.090 us | 0.113 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.116 us | 0.123 us | 0.95x | 0.95–0.97x | -0.006 us | 9 pairs; 41/40 ms; 348,622/327,831 ops/lane; IQR 1.002/1.009 | `Pcre2Character` | 0.157 us | 0.112 us | 0.143 us | 0.81x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.075 us | 0.198 us | 0.38x | 0.38–0.38x | -0.123 us | 9 pairs; 40/39 ms; 534,239/196,103 ops/lane; IQR 1.003/1.011 | `Pcre2Slice` | 0.445 us | 0.075 us | 0.112 us | 0.67x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.281 us | 160.926 us | 0.51x | 0.51–0.51x | -78.567 us | 9 pairs; 40/40 ms; 485/247 ops/lane; IQR 1.005/1.002 | `Pcre2Slice` | 339.776 us | 97.271 us | 98.953 us | 0.83x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.299 us | 285.206 us | 0.14x | 0.14–0.14x | -245.859 us | 9 pairs; 40/40 ms; 1,018/142 ops/lane; IQR 1.002/1.004 | `Pcre2Character` | 266.471 us | 217.743 us | 225.786 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.259 us | 231.716 us | 0.04x | 0.04–0.04x | -221.509 us | 9 pairs; 40/42 ms; 3,896/182 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 8.317 us | 215.911 us | 218.726 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.036 us | 215.241 us | 1.12x | 1.12–1.13x | +25.788 us | 9 pairs; 40/41 ms; 165/191 ops/lane; IQR 1.001/1.005 | `Pcre2SingleTokenRepeat` | 8.285 us | 230.651 us | 232.346 us | 1.04x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.553 us | 23.481 us | 0.15x | 0.15–0.15x | -19.899 us | 9 pairs; 39/39 ms; 11,071/1,662 ops/lane; IQR 1.008/1.021 | `Pcre2Literal` | 1.397 us | 4.257 us | 6.034 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.665 us | 82.235 us | 0.12x | 0.12–0.12x | -72.570 us | 9 pairs; 40/40 ms; 4,132/487 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 9.351 us | 89.366 us | 91.158 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,200.538 us | 27,452.200 us | 0.15x | 0.14–0.16x | -23253.338 us | 9 pairs; 34/27 ms; 8/1 ops/lane; IQR 1.002/1.061 | `Pcre2Backtracking` | 6,128.178 us | 11,504.500 us | 18,617.500 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,412.600 us | 55,080.400 us | 0.06x | 0.06–0.06x | -51667.800 us | 9 pairs; 41/55 ms; 12/1 ops/lane; IQR 1.030/1.021 | `Pcre2Backtracking` | 3,983.162 us | 41,716.700 us | 48,019.750 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,936.581 us | 485,578.900 us | 0.00x | 0.00–0.00x | -483690.162 us | 9 pairs; 41/486 ms; 21/1 ops/lane; IQR 1.030/1.002 | `Pcre2Character` | 1,779.007 us | 23,393.800 us | 30,219.800 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,586.133 us | 11,685.800 us | 0.48x | 0.45–0.53x | -6094.317 us | 9 pairs; 34/35 ms; 6/3 ops/lane; IQR 1.003/1.118 | `Pcre2Literal` | 1,320.911 us | 1,361.253 us | 8,416.457 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,869.050 us | 146,266.500 us | 0.13x | 0.13–0.13x | -127189.150 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 1,224.002 us | 508.641 us | 8,528.583 us | 2.21x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,932.238 us | 22,057.500 us | 0.22x | 0.22–0.23x | -17153.213 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | 2,428.876 us | 10,645.780 us | 18,680.833 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,424.650 us | 133,772.500 us | 0.17x | 0.17–0.17x | -111433.450 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.014/1.005 | `Pcre2Backtracking` | 1,500.488 us | 2,296.965 us | 10,441.620 us | 2.15x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 164.107 us | 1,004.784 us | 0.16x | 0.16–0.17x | -840.419 us | 9 pairs; 40/38 ms; 241/38 ops/lane; IQR 1.003/1.023 | `Pcre2Backtracking` | 171.431 us | 318.497 us | 538.124 us | 0.30x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,651.933 us | 13,498.500 us | 0.86x | 0.86–0.87x | -1857.533 us | 9 pairs; 35/40 ms; 3/3 ops/lane; IQR 1.003/1.016 | `Pcre2Backtracking` | 307.713 us | 14,648.375 us | 14,917.375 us | 0.78x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,798.629 us | 98,457.200 us | 0.03x | 0.03–0.03x | -95663.121 us | 9 pairs; 39/98 ms; 14/1 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 1,919.496 us | 19,918.267 us | 20,093.933 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.850 us | 2,847.477 us | 0.07x | 0.06–0.07x | -2661.347 us | 9 pairs; 39/37 ms; 212/13 ops/lane; IQR 1.005/1.015 | `Pcre2SingleTokenRepeat` | 188.308 us | 1,012.642 us | 1,429.976 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,610.968 us | 20,201.400 us | 0.08x | 0.08–0.08x | -18588.716 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 564.577 us | 9,924.633 us | 10,049.180 us | 0.16x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.149 us | 1.097 us | 0.14x | 0.13–0.14x | -0.947 us | 9 pairs; 38/40 ms; 252,840/36,464 ops/lane; IQR 1.011/1.002 | `Pcre2Slice` | 0.642 us | 0.259 us | 0.281 us | 0.53x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.568 us | 0.828 us | 0.69x | 0.68–0.69x | -0.261 us | 9 pairs; 37/40 ms; 65,673/48,544 ops/lane; IQR 1.004/1.003 | `Pcre2Literal` | 0.146 us | 0.149 us | 0.172 us | 3.31x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.310 us | 1.183 us | 0.26x | 0.26–0.26x | -0.873 us | 9 pairs; 41/40 ms; 131,571/33,820 ops/lane; IQR 1.001/1.001 | `Pcre2Backtracking` | 1.076 us | 0.343 us | 0.372 us | 0.83x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.875 us | 1.130 us | 0.78x | 0.77–0.78x | -0.253 us | 9 pairs; 40/40 ms; 45,911/35,295 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0.285 us | 0.426 us | 0.466 us | 1.88x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.155 us | 0.856 us | 0.18x | 0.18–0.18x | -0.700 us | 9 pairs; 40/40 ms; 257,585/47,104 ops/lane; IQR 1.001/1.004 | `Pcre2Slice` | 0.808 us | 0.156 us | 0.201 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.888 us | 1.111 us | 0.80x | 0.80–0.80x | -0.223 us | 9 pairs; 40/40 ms; 44,721/36,243 ops/lane; IQR 1.000/1.001 | `Pcre2Slice` | 2.703 us | 0.307 us | 0.327 us | 2.72x | 0 B | 0 B |
| `simple/foo-dense` | **Equivalent** | 18 B | 0.833 us | 0.832 us | 1.00x | 1.00–1.00x | 0.000 us | 9 pairs; 42/40 ms; 49,951/48,105 ops/lane; IQR 1.002/1.004 | `Pcre2Literal` | 3.603 us | 0.182 us | 0.204 us | 4.09x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.017 us | 1.191 us | 0.86x | 0.85–0.86x | -0.171 us | 9 pairs; 39/40 ms; 38,339/33,663 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 5.288 us | 0.400 us | 0.423 us | 2.40x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.173 us | 1.145 us | 1.02x | 1.02–1.03x | +0.028 us | 9 pairs; 39/39 ms; 33,118/34,435 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 3.706 us | 0.479 us | 0.516 us | 2.27x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.804 us | 0.856 us | 0.94x | 0.94–0.94x | -0.052 us | 9 pairs; 41/40 ms; 50,452/46,426 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | 2.135 us | 0.183 us | 0.225 us | 3.58x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.571 us | 1.110 us | 0.51x | 0.51–0.52x | -0.538 us | 9 pairs; 40/40 ms; 70,384/36,341 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.505 us | 0.831 us | 0.61x | 0.61–0.61x | -0.326 us | 9 pairs; 40/40 ms; 78,284/48,287 ops/lane; IQR 1.004/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.717 us | 1.191 us | 0.60x | 0.60–0.60x | -0.474 us | 9 pairs; 40/40 ms; 56,117/33,542 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.321 us | 1.138 us | 1.16x | 1.16–1.16x | +0.182 us | 9 pairs; 53/40 ms; 40,478/35,227 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.541 us | 0.862 us | 0.63x | 0.63–0.63x | -0.320 us | 9 pairs; 40/40 ms; 74,274/46,429 ops/lane; IQR 1.001/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 63.055 us | — | — | — | — | — | `—` | 25.842 us | 96.933 us | 98.041 us | 0.64x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.473 us | — | — | — | — | — | `—` | 1.813 us | 0.358 us | 0.386 us | 11.59x | 4,176 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.095 us | — | — | — | — | — | `—` | 0.922 us | 0.239 us | 0.266 us | 4.12x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.248 us | — | — | — | — | — | `—` | 3.107 us | 0.451 us | 0.482 us | 2.59x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.519 us | — | — | — | — | — | `—` | 1.036 us | 0.563 us | 0.584 us | 2.60x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.584 us | — | — | — | — | — | `—` | 1.512 us | 0.266 us | 0.321 us | 11.16x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.087 us | 0.085 us | 1.03x | 1.02–1.03x | +0.003 us | 9 pairs; 38/39 ms; 435,657/465,132 ops/lane; IQR 1.003/1.006 | `Pcre2Character` | 0 B | 0 B | 2.369 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.122 us | 0.11x | 0.10–0.11x | -0.109 us | 9 pairs; 40/38 ms; 3,126,718/314,314 ops/lane; IQR 1.013/1.025 | `Pcre2Backtracking` | 0 B | 0 B | 130.931 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.389 us | 0.107 us | 3.64x | 3.62–3.66x | +0.282 us | 9 pairs; 36/41 ms; 92,431/378,547 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 20.369 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.396 us | 0.118 us | 3.36x | 3.33–3.40x | +0.279 us | 9 pairs; 36/41 ms; 92,128/342,526 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 72.138 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.394 us | 0.100 us | 3.94x | 3.88–3.98x | +0.294 us | 9 pairs; 36/39 ms; 90,034/392,138 ops/lane; IQR 1.004/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 70.875 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.671 us | 0.348 us | 1.94x | 1.92–1.96x | +0.326 us | 9 pairs; 38/41 ms; 55,969/118,891 ops/lane; IQR 1.006/1.014 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.634 us | 0.02x | 0.02–0.02x | -0.621 us | 9 pairs; 42/39 ms; 3,405,942/61,422 ops/lane; IQR 1.042/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 21.606 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.016 us | 0.517 us | 0.03x | 0.02–0.03x | -0.502 us | 9 pairs; 54/39 ms; 3,421,203/75,308 ops/lane; IQR 1.197/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 21.231 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.462 us | 0.109 us | 4.24x | 4.23–4.26x | +0.353 us | 9 pairs; 37/40 ms; 79,230/367,101 ops/lane; IQR 1.015/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 18.181 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.561 us | 11.386 us | 0.75x | 0.75–0.75x | -2.829 us | 9 pairs; 40/43 ms; 4,714/3,782 ops/lane; IQR 1.005/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.298 us | 1.052 us | 0.28x | 0.28–0.28x | -0.754 us | 9 pairs; 41/39 ms; 136,455/37,401 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 129.500 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.327 us | 0.461 us | 0.71x | 0.70–0.71x | -0.135 us | 9 pairs; 39/40 ms; 120,364/86,986 ops/lane; IQR 1.004/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 113.419 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.295 us | 0.459 us | 0.64x | 0.64–0.64x | -0.165 us | 9 pairs; 38/41 ms; 128,246/88,562 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 140.863 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.300 us | 0.586 us | 0.51x | 0.51–0.52x | -0.287 us | 9 pairs; 37/40 ms; 124,331/67,795 ops/lane; IQR 1.003/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 104.000 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.299 us | 0.368 us | 0.81x | 0.79–0.82x | -0.070 us | 9 pairs; 35/39 ms; 116,806/107,096 ops/lane; IQR 1.016/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 213.906 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.028 us | 8.826 us | 0.57x | 0.57–0.57x | -3.796 us | 9 pairs; 40/40 ms; 8,001/4,519 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 0 B | 0 B | 1.525 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.255 us | 0.310 us | 0.82x | 0.82–0.83x | -0.055 us | 9 pairs; 33/39 ms; 128,950/126,394 ops/lane; IQR 1.009/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 92.881 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.271 us | 0.410 us | 0.66x | 0.66–0.66x | -0.139 us | 9 pairs; 37/41 ms; 135,616/100,946 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 124.112 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.770 us | — | — | — | — | — | `—` | 0 B | — | 13.456 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.189 us | 0.298 us | 0.64x | 0.63–0.66x | -0.107 us | 9 pairs; 40/40 ms; 213,225/132,635 ops/lane; IQR 1.043/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.244 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.708 us | — | — | — | — | — | `—` | 0 B | — | 14.438 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.554 us | 0.673 us | 0.83x | 0.82–0.85x | -0.116 us | 9 pairs; 36/39 ms; 64,178/57,788 ops/lane; IQR 1.037/1.009 | `Pcre2Slice` | 0 B | 0 B | 64.312 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.973 us | 29.038 us | 0.34x | 0.34–0.34x | -19.060 us | 9 pairs; 40/40 ms; 4,025/1,382 ops/lane; IQR 1.001/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.985 us | 1.072 us | 0.92x | 0.91–0.92x | -0.088 us | 9 pairs; 41/41 ms; 41,891/37,807 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 128.375 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.996 us | 1.194 us | 0.83x | 0.83–0.84x | -0.199 us | 9 pairs; 40/39 ms; 40,010/33,006 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 114.175 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.945 us | 1.038 us | 0.91x | 0.91–0.91x | -0.091 us | 9 pairs; 40/39 ms; 41,853/37,880 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 141.825 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.975 us | 1.183 us | 0.82x | 0.82–0.83x | -0.208 us | 9 pairs; 41/41 ms; 41,996/34,375 ops/lane; IQR 1.006/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 102.838 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.941 us | 0.953 us | 0.99x | 0.98–0.99x | -0.011 us | 9 pairs; 39/41 ms; 41,960/42,815 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 216.562 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.590 us | 8.876 us | 0.63x | 0.62–0.63x | -3.282 us | 9 pairs; 40/40 ms; 7,202/4,540 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 0 B | 0 B | 1.506 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.918 us | 0.893 us | 1.03x | 1.02–1.03x | +0.023 us | 9 pairs; 40/40 ms; 43,773/45,245 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 93.450 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.921 us | 0.969 us | 0.95x | 0.95–0.95x | -0.049 us | 9 pairs; 40/40 ms; 43,138/41,606 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 122.550 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.059 us | — | — | — | — | — | `—` | 0 B | — | 13.713 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.835 us | 0.873 us | 0.96x | 0.95–0.96x | -0.038 us | 9 pairs; 38/40 ms; 46,062/45,752 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.225 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.952 us | — | — | — | — | — | `—` | 0 B | — | 14.831 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.926 us | 1.265 us | 0.73x | 0.73–0.73x | -0.339 us | 9 pairs; 40/41 ms; 43,557/32,016 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | 0 B | 0 B | 64.600 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.485 us | 2.271 us | 0.65x | 0.65–0.66x | -0.786 us | 9 pairs; 40/40 ms; 26,989/17,606 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.633 us | 1.077 us | 0.59x | 0.59–0.59x | -0.443 us | 9 pairs; 39/41 ms; 61,385/37,947 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 130.525 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.719 us | 1.189 us | 0.60x | 0.60–0.61x | -0.471 us | 9 pairs; 43/40 ms; 59,256/33,969 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.237 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.616 us | 1.038 us | 0.60x | 0.59–0.60x | -0.421 us | 9 pairs; 40/41 ms; 64,741/39,303 ops/lane; IQR 1.002/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 143.419 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.626 us | 1.174 us | 0.53x | 0.53–0.54x | -0.546 us | 9 pairs; 38/40 ms; 61,122/34,205 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.731 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.640 us | 0.952 us | 0.67x | 0.66–0.68x | -0.311 us | 9 pairs; 40/40 ms; 63,246/41,758 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 213.881 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.962 us | 2.197 us | 0.89x | 0.89–0.90x | -0.235 us | 9 pairs; 41/40 ms; 20,793/18,297 ops/lane; IQR 1.004/1.001 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.614 us | 0.891 us | 0.69x | 0.68–0.69x | -0.276 us | 9 pairs; 40/42 ms; 64,787/47,236 ops/lane; IQR 1.009/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 92.869 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.629 us | 0.971 us | 0.65x | 0.65–0.65x | -0.343 us | 9 pairs; 40/40 ms; 62,960/41,402 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 123.062 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.763 us | — | — | — | — | — | `—` | 0 B | — | 13.188 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.493 us | 0.873 us | 0.56x | 0.56–0.57x | -0.380 us | 9 pairs; 40/40 ms; 80,555/46,363 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.156 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.671 us | — | — | — | — | — | `—` | 0 B | — | 13.875 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.533 us | 1.262 us | 0.42x | 0.42–0.43x | -0.728 us | 9 pairs; 40/40 ms; 75,118/31,671 ops/lane; IQR 1.004/1.004 | `Pcre2Slice` | 0 B | 0 B | 63.681 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.046 us | — | — | — | — | — | `—` | 480 B | — | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.159 us | — | — | — | — | — | `—` | 48 B | — | 131.556 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.269 us | — | — | — | — | — | `—` | 48 B | — | 115.144 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.149 us | — | — | — | — | — | `—` | 48 B | — | 143.056 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.152 us | — | — | — | — | — | `—` | 48 B | — | 102.925 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.147 us | — | — | — | — | — | `—` | 48 B | — | 219.456 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.035 us | — | — | — | — | — | `—` | 64 B | — | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.175 us | — | — | — | — | — | `—` | 40 B | — | 95.263 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.167 us | — | — | — | — | — | `—` | 40 B | — | 120.394 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.283 us | — | — | — | — | — | `—` | 1,216 B | — | 13.594 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.094 us | — | — | — | — | — | `—` | 48 B | — | 53.062 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.736 us | — | — | — | — | — | `—` | 928 B | — | 13.719 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.761 us | — | — | — | — | — | `—` | 5,392 B | — | 64.263 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 33.553–259.361 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.434–94.952 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.112–12.685 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.079–48.810 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.034–1.165 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.923–46.176 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.704–22.347 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.525–100.034 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.146–72.481 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.198–5.510 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.353–376.727 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.077 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 40.747–317.687 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.888–5.173 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.106–15.426 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 109.151–847.868 us |

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
