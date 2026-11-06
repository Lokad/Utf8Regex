<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `FC208B4A785054E3FD4E7B127305B34A3D5C125008246370833AFC1E74563122`
- Latest managed row measurement: `2026-11-06T19:21:43.4722583+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T19:21:43.4722583+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `77` managed faster, `0` equivalent, `11` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `7c5f60bd2760` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `7c5f60bd2760` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.606 us | 2.125 us | 0.29x | 0.24–0.30x | -1.504 us | 9 pairs; 38/34 ms; 62,542/16,118 ops/lane; IQR 1.032/1.169 | `Pcre2Slice` | 461.383 us | 0.274 us | 0.790 us | 0.77x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.811 us | 12.442 us | 0.07x | 0.06–0.07x | -11.631 us | 9 pairs; 37/49 ms; 45,550/3,909 ops/lane; IQR 1.008/1.153 | `Pcre2Slice` | 0.597 us | 0.095 us | 3.538 us | 0.23x | 272 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.222 us | 0.01x | 0.01–0.01x | -1.211 us | 9 pairs; 40/39 ms; 3,642,238/32,053 ops/lane; IQR 1.012/1.005 | `Pcre2Slice` | 0.102 us | 1.127 us | 1.162 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.668 us | 0.154 us | 4.33x | 4.29–4.34x | +0.515 us | 9 pairs; 38/40 ms; 56,225/257,316 ops/lane; IQR 1.020/1.020 | `Pcre2Backtracking` | 0.045 us | 0.121 us | 0.161 us | 4.16x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.297 us | 0.562 us | 4.07x | 4.06–4.09x | +1.734 us | 9 pairs; 39/41 ms; 16,864/72,246 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 0.114 us | 0.753 us | 0.777 us | 2.96x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.291 us | 0.34x | 0.34–0.35x | -0.191 us | 9 pairs; 39/40 ms; 400,224/138,928 ops/lane; IQR 1.013/1.019 | `Pcre2Slice` | 0.113 us | 0.322 us | 0.347 us | 0.28x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.104 us | 0.349 us | 0.30x | 0.30–0.30x | -0.244 us | 9 pairs; 40/38 ms; 382,036/110,081 ops/lane; IQR 1.010/1.011 | `Pcre2Slice` | 0.115 us | 0.446 us | 0.464 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.077 us | 0.283 us | 0.27x | 0.27–0.30x | -0.205 us | 9 pairs; 39/39 ms; 505,293/136,390 ops/lane; IQR 1.031/1.009 | `Pcre2Slice` | 0.088 us | 0.235 us | 0.260 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.081 us | 0.311 us | 0.26x | 0.26–0.27x | -0.229 us | 9 pairs; 41/39 ms; 508,893/125,518 ops/lane; IQR 1.007/1.009 | `Pcre2Slice` | 0.087 us | 0.252 us | 0.277 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.174 us | 0.077 us | 2.26x | 2.22–2.28x | +0.097 us | 9 pairs; 42/41 ms; 239,686/531,225 ops/lane; IQR 1.026/1.041 | `Pcre2Slice` | 0.087 us | 0.976 us | 1.014 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Inconclusive** | 46 B | 0.043 us | 0.196 us | 0.22x | 0.17–0.22x | -0.154 us | 9 pairs; 51/41 ms; 1,183,302/207,462 ops/lane; IQR 1.185/1.011 | `Pcre2Backtracking` | 0.109 us | 0.311 us | 0.351 us | 0.12x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.589 us | 0.10x | 0.10–0.10x | -0.530 us | 9 pairs; 40/40 ms; 678,226/68,006 ops/lane; IQR 1.011/1.021 | `Pcre2Backtracking` | 0.133 us | 0.227 us | 0.252 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.153 us | 0.22x | 0.22–0.23x | -0.119 us | 9 pairs; 38/43 ms; 1,144,419/279,075 ops/lane; IQR 1.014/1.013 | `Pcre2Backtracking` | 0.106 us | 0.179 us | 0.199 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Inconclusive** | 10 B | 0.019 us | 0.112 us | 0.17x | 0.17–0.18x | -0.092 us | 9 pairs; 39/43 ms; 2,030,943/381,937 ops/lane; IQR 1.040/1.017 | `Pcre2Backtracking` | 0.059 us | 0.091 us | 0.110 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.120 us | 0.47x | 0.47–0.48x | -0.064 us | 9 pairs; 40/41 ms; 698,561/342,313 ops/lane; IQR 1.004/1.018 | `Pcre2Backtracking` | 0.012 us | 0.087 us | 0.107 us | 0.53x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.089 us | 0.273 us | 0.33x | 0.31–0.33x | -0.185 us | 9 pairs; 42/43 ms; 472,012/155,741 ops/lane; IQR 1.019/1.008 | `Pcre2Slice` | 0.094 us | 0.118 us | 0.155 us | 0.58x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.023 us | 0.117 us | 0.19x | 0.19–0.20x | -0.095 us | 9 pairs; 41/41 ms; 1,821,963/348,225 ops/lane; IQR 1.011/1.013 | `Pcre2Backtracking` | 0.019 us | 0.095 us | 0.115 us | 0.20x | 0 B | 0 B |
| `literal/absolute-anchored` | **Managed faster** | 4,103 B | 0.115 us | 1.806 us | 0.06x | 0.06–0.07x | -1.691 us | 9 pairs; 40/44 ms; 346,356/24,141 ops/lane; IQR 1.008/1.044 | `Pcre2Literal` | 0.091 us | 0.050 us | 0.576 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.119 us | 1.914 us | 0.06x | 0.05–0.07x | -1.796 us | 9 pairs; 40/41 ms; 334,827/21,675 ops/lane; IQR 1.008/1.274 | `Pcre2Literal` | 0.149 us | 0.053 us | 0.580 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.145 us | 1.914 us | 0.08x | 0.06–0.08x | -1.767 us | 9 pairs; 38/37 ms; 264,705/19,459 ops/lane; IQR 1.007/1.054 | `Pcre2Literal` | 0.205 us | 0.231 us | 0.764 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.150 us | 1.865 us | 0.08x | 0.08–0.09x | -1.715 us | 9 pairs; 41/47 ms; 271,875/25,060 ops/lane; IQR 1.008/1.096 | `Pcre2Literal` | 0.184 us | 0.202 us | 0.726 us | 0.21x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.097 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 41/39 ms; 4,637,665/403,995 ops/lane; IQR 1.007/1.021 | `Pcre2Slice` | 0.223 us | 0.071 us | 0.090 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.086 us | 0.78x | 0.76–0.79x | -0.019 us | 9 pairs; 40/40 ms; 602,596/463,379 ops/lane; IQR 1.008/1.019 | `Pcre2Literal` | 0.133 us | 0.050 us | 0.072 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.110 us | 0.12x | 0.12–0.12x | -0.097 us | 9 pairs; 39/40 ms; 2,893,102/360,620 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 0.253 us | 0.095 us | 0.116 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.116 us | 0.121 us | 0.95x | 0.94–0.98x | -0.006 us | 9 pairs; 40/38 ms; 346,843/315,862 ops/lane; IQR 1.049/1.022 | `Pcre2Character` | 0.142 us | 0.110 us | 0.142 us | 0.82x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.074 us | 0.210 us | 0.35x | 0.35–0.36x | -0.135 us | 9 pairs; 39/38 ms; 529,354/182,044 ops/lane; IQR 1.002/1.014 | `Pcre2Slice` | 0.399 us | 0.073 us | 0.109 us | 0.68x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.954 us | 156.945 us | 0.52x | 0.51–0.52x | -76.123 us | 9 pairs; 39/39 ms; 481/247 ops/lane; IQR 1.005/1.016 | `Pcre2Slice` | 320.673 us | 105.208 us | 106.835 us | 0.76x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 38.972 us | 277.392 us | 0.14x | 0.14–0.14x | -238.524 us | 9 pairs; 40/40 ms; 1,016/144 ops/lane; IQR 1.005/1.010 | `Pcre2Character` | 269.481 us | 213.933 us | 223.379 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.471 us | 205.527 us | 0.05x | 0.05–0.05x | -196.021 us | 9 pairs; 37/41 ms; 3,905/200 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 7.714 us | 215.433 us | 215.821 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 235.355 us | 210.211 us | 1.13x | 1.12–1.13x | +26.440 us | 9 pairs; 41/41 ms; 173/197 ops/lane; IQR 1.012/1.009 | `Pcre2SingleTokenRepeat` | 7.734 us | 225.008 us | 224.624 us | 1.05x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.492 us | 22.836 us | 0.15x | 0.15–0.16x | -19.344 us | 9 pairs; 39/42 ms; 11,145/1,861 ops/lane; IQR 1.002/1.018 | `Pcre2Literal` | 1.356 us | 4.195 us | 5.866 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.374 us | 79.249 us | 0.12x | 0.12–0.12x | -69.881 us | 9 pairs; 39/39 ms; 4,155/492 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 9.186 us | 88.949 us | 90.855 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,067.725 us | 27,521.800 us | 0.15x | 0.14–0.15x | -23481.750 us | 9 pairs; 33/28 ms; 8/1 ops/lane; IQR 1.012/1.043 | `Pcre2Backtracking` | 5,202.827 us | 11,609.171 us | 18,388.220 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,300.425 us | 54,044.500 us | 0.06x | 0.06–0.06x | -50749.583 us | 9 pairs; 40/54 ms; 12/1 ops/lane; IQR 1.019/1.024 | `Pcre2Backtracking` | 3,843.375 us | 40,493.750 us | 46,721.300 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,875.477 us | 472,795.500 us | 0.00x | 0.00–0.00x | -470948.936 us | 9 pairs; 41/473 ms; 22/1 ops/lane; IQR 1.027/1.006 | `Pcre2Character` | 1,799.307 us | 22,352.550 us | 29,447.867 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,464.367 us | 11,245.400 us | 0.49x | 0.47–0.53x | -5794.025 us | 9 pairs; 33/45 ms; 6/4 ops/lane; IQR 1.022/1.101 | `Pcre2Literal` | 1,274.650 us | 1,295.184 us | 8,281.400 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,543.600 us | 141,459.600 us | 0.13x | 0.13–0.13x | -122697.950 us | 9 pairs; 37/141 ms; 2/1 ops/lane; IQR 1.022/1.005 | `Pcre2Backtracking` | 1,194.651 us | 501.804 us | 8,431.367 us | 2.20x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,832.400 us | 21,530.950 us | 0.23x | 0.22–0.23x | -16698.550 us | 9 pairs; 39/43 ms; 8/2 ops/lane; IQR 1.015/1.020 | `Pcre2Slice` | 2,370.084 us | 10,928.275 us | 18,669.000 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,896.150 us | 131,885.500 us | 0.17x | 0.16–0.17x | -109456.700 us | 9 pairs; 44/132 ms; 2/1 ops/lane; IQR 1.014/1.014 | `Pcre2Backtracking` | 1,475.212 us | 2,323.403 us | 10,520.638 us | 2.08x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 138.229 us | 994.452 us | 0.14x | 0.14–0.14x | -856.224 us | 9 pairs; 39/42 ms; 282/42 ops/lane; IQR 1.008/1.048 | `Pcre2Backtracking` | 166.703 us | 317.068 us | 561.503 us | 0.25x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,205.933 us | 13,199.533 us | 0.85x | 0.84–0.86x | -1993.600 us | 9 pairs; 34/40 ms; 3/3 ops/lane; IQR 1.012/1.014 | `Pcre2Backtracking` | 301.481 us | 15,057.120 us | 15,269.020 us | 0.73x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,756.543 us | 96,249.300 us | 0.03x | 0.03–0.03x | -93508.457 us | 9 pairs; 39/96 ms; 14/1 ops/lane; IQR 1.014/1.006 | `Pcre2Character` | 1,988.629 us | 19,384.375 us | 19,505.050 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 182.770 us | 2,695.513 us | 0.07x | 0.07–0.07x | -2511.158 us | 9 pairs; 39/40 ms; 212/15 ops/lane; IQR 1.011/1.009 | `Pcre2SingleTokenRepeat` | 185.853 us | 972.405 us | 1,218.008 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Inconclusive** | 594,930 B | 1,552.522 us | 19,683.950 us | 0.08x | 0.08–0.08x | -18119.694 us | 9 pairs; 42/39 ms; 27/2 ops/lane; IQR 1.021/1.005 | `Pcre2Backtracking` | 568.876 us | 10,508.288 us | 10,745.050 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.136 us | 1.087 us | 0.13x | 0.12–0.13x | -0.951 us | 9 pairs; 39/40 ms; 289,197/37,130 ops/lane; IQR 1.010/1.015 | `Pcre2Slice` | 0.606 us | 0.252 us | 0.276 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.520 us | 0.813 us | 0.64x | 0.63–0.64x | -0.294 us | 9 pairs; 40/39 ms; 76,521/48,053 ops/lane; IQR 1.020/1.002 | `Pcre2Literal` | 0.138 us | 0.146 us | 0.169 us | 3.07x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.297 us | 1.174 us | 0.25x | 0.25–0.25x | -0.877 us | 9 pairs; 40/40 ms; 133,988/34,275 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 1.044 us | 0.351 us | 0.370 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.848 us | 1.107 us | 0.77x | 0.76–0.77x | -0.259 us | 9 pairs; 39/40 ms; 45,888/36,014 ops/lane; IQR 1.005/1.010 | `Pcre2Character` | 0.276 us | 0.421 us | 0.469 us | 1.81x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.164 us | 0.850 us | 0.20x | 0.19–0.20x | -0.683 us | 9 pairs; 39/41 ms; 238,451/47,702 ops/lane; IQR 1.022/1.004 | `Pcre2Slice` | 0.761 us | 0.153 us | 0.199 us | 0.83x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.945 us | 1.081 us | 0.87x | 0.87–0.88x | -0.139 us | 9 pairs; 39/40 ms; 41,643/37,294 ops/lane; IQR 1.015/1.010 | `Pcre2Slice` | 2.185 us | 0.299 us | 0.316 us | 2.99x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.777 us | 0.818 us | 0.95x | 0.95–0.96x | -0.041 us | 9 pairs; 39/41 ms; 50,174/49,719 ops/lane; IQR 1.008/1.013 | `Pcre2Literal` | 3.483 us | 0.178 us | 0.201 us | 3.87x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.988 us | 1.185 us | 0.83x | 0.83–0.83x | -0.200 us | 9 pairs; 41/39 ms; 41,649/33,042 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 5.167 us | 0.405 us | 0.427 us | 2.31x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.183 us | 1.105 us | 1.07x | 1.07–1.07x | +0.075 us | 9 pairs; 40/40 ms; 34,101/35,890 ops/lane; IQR 1.008/1.010 | `Pcre2Character` | 3.549 us | 0.471 us | 0.510 us | 2.32x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.778 us | 0.849 us | 0.92x | 0.91–0.92x | -0.072 us | 9 pairs; 40/40 ms; 51,567/46,618 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | 1.981 us | 0.177 us | 0.218 us | 3.57x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.571 us | 1.082 us | 0.53x | 0.52–0.53x | -0.513 us | 9 pairs; 39/39 ms; 67,806/35,977 ops/lane; IQR 1.013/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.498 us | 0.818 us | 0.61x | 0.61–0.61x | -0.321 us | 9 pairs; 39/41 ms; 78,306/49,905 ops/lane; IQR 1.005/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.649 us | 1.175 us | 0.55x | 0.55–0.56x | -0.527 us | 9 pairs; 39/41 ms; 59,420/34,605 ops/lane; IQR 1.013/1.012 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.887 us | 1.107 us | 0.80x | 0.80–0.80x | -0.220 us | 9 pairs; 39/40 ms; 44,059/36,278 ops/lane; IQR 1.008/1.009 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.543 us | 0.849 us | 0.64x | 0.64–0.64x | -0.305 us | 9 pairs; 41/39 ms; 74,646/45,496 ops/lane; IQR 1.008/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 59.285 us | — | — | — | — | — | `—` | 25.209 us | 94.691 us | 96.844 us | 0.61x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.542 us | — | — | — | — | — | `—` | 1.446 us | 0.352 us | 0.376 us | 9.42x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.049 us | — | — | — | — | — | `—` | 0.899 us | 0.240 us | 0.266 us | 3.95x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.184 us | — | — | — | — | — | `—` | 2.988 us | 0.466 us | 0.493 us | 2.40x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.453 us | — | — | — | — | — | `—` | 1.034 us | 0.534 us | 0.587 us | 2.48x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.552 us | — | — | — | — | — | `—` | 1.493 us | 0.268 us | 0.316 us | 11.24x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.085 us | 0.084 us | 1.02x | 1.01–1.03x | +0.001 us | 9 pairs; 38/40 ms; 441,984/475,547 ops/lane; IQR 1.010/1.008 | `Pcre2Character` | 0 B | 0 B | 2.312 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.012 us | 0.116 us | 0.11x | 0.11–0.11x | -0.104 us | 9 pairs; 40/41 ms; 3,221,366/350,417 ops/lane; IQR 1.010/1.026 | `Pcre2Backtracking` | 0 B | 0 B | 126.456 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Inconclusive** | 3 B | 0.372 us | 0.103 us | 3.60x | 3.53–3.66x | +0.269 us | 9 pairs; 37/40 ms; 98,211/388,212 ops/lane; IQR 1.017/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 22.038 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.386 us | 0.114 us | 3.39x | 3.37–3.42x | +0.272 us | 9 pairs; 37/42 ms; 95,993/366,554 ops/lane; IQR 1.015/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 70.656 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.378 us | 0.102 us | 3.70x | 3.56–3.71x | +0.276 us | 9 pairs; 35/39 ms; 92,754/381,769 ops/lane; IQR 1.020/1.060 | `Pcre2Backtracking` | 0 B | 0 B | 69.875 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.656 us | 0.342 us | 1.92x | 1.91–1.93x | +0.315 us | 9 pairs; 37/40 ms; 56,855/117,746 ops/lane; IQR 1.013/1.013 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.620 us | 0.02x | 0.02–0.02x | -0.608 us | 9 pairs; 43/39 ms; 3,554,013/62,940 ops/lane; IQR 1.032/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 21.794 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.497 us | 0.02x | 0.02–0.02x | -0.485 us | 9 pairs; 39/37 ms; 3,241,682/75,377 ops/lane; IQR 1.015/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 21.762 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.468 us | 0.107 us | 4.38x | 4.33–4.42x | +0.361 us | 9 pairs; 37/41 ms; 79,054/380,724 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 18.225 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.235 us | 10.415 us | 0.79x | 0.79–0.79x | -2.193 us | 9 pairs; 40/39 ms; 4,799/3,770 ops/lane; IQR 1.007/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.825 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.282 us | 1.015 us | 0.28x | 0.28–0.28x | -0.734 us | 9 pairs; 36/41 ms; 127,442/40,864 ops/lane; IQR 1.010/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 131.175 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.300 us | 0.452 us | 0.66x | 0.66–0.67x | -0.153 us | 9 pairs; 36/39 ms; 120,148/85,405 ops/lane; IQR 1.004/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 114.606 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.284 us | 0.451 us | 0.63x | 0.62–0.63x | -0.168 us | 9 pairs; 34/41 ms; 118,387/91,222 ops/lane; IQR 1.004/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 143.088 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.295 us | 0.577 us | 0.51x | 0.51–0.51x | -0.282 us | 9 pairs; 34/41 ms; 114,363/71,006 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 101.306 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.299 us | 0.392 us | 0.76x | 0.75–0.77x | -0.092 us | 9 pairs; 39/39 ms; 129,826/99,562 ops/lane; IQR 1.012/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 213.881 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.788 us | 8.641 us | 0.55x | 0.55–0.55x | -3.849 us | 9 pairs; 40/41 ms; 8,365/4,689 ops/lane; IQR 1.007/1.004 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.251 us | 0.303 us | 0.83x | 0.83–0.83x | -0.053 us | 9 pairs; 35/40 ms; 141,144/131,571 ops/lane; IQR 1.008/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 89.456 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.266 us | 0.392 us | 0.68x | 0.67–0.68x | -0.127 us | 9 pairs; 36/38 ms; 137,023/97,822 ops/lane; IQR 1.016/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 120.500 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.811 us | — | — | — | — | — | `—` | 0 B | — | 13.488 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.182 us | 0.290 us | 0.63x | 0.63–0.63x | -0.107 us | 9 pairs; 40/40 ms; 219,347/138,550 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 53.413 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.671 us | — | — | — | — | — | `—` | 0 B | — | 13.669 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.516 us | 0.672 us | 0.77x | 0.76–0.77x | -0.156 us | 9 pairs; 37/38 ms; 71,571/56,936 ops/lane; IQR 1.005/1.013 | `Pcre2Slice` | 0 B | 0 B | 63.919 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.659 us | 28.407 us | 0.34x | 0.34–0.34x | -18.728 us | 9 pairs; 40/41 ms; 4,160/1,447 ops/lane; IQR 1.005/1.009 | `Pcre2Literal` | 0 B | 0 B | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.942 us | 1.053 us | 0.90x | 0.89–0.90x | -0.110 us | 9 pairs; 39/42 ms; 40,946/39,472 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 129.300 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.978 us | 1.182 us | 0.83x | 0.82–0.83x | -0.204 us | 9 pairs; 40/40 ms; 41,238/34,183 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 115.569 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.925 us | 1.014 us | 0.91x | 0.91–0.92x | -0.088 us | 9 pairs; 41/42 ms; 44,299/40,929 ops/lane; IQR 1.006/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 139.463 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.936 us | 1.136 us | 0.82x | 0.82–0.83x | -0.201 us | 9 pairs; 40/41 ms; 42,633/36,139 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 103.519 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.962 us | 0.927 us | 1.04x | 1.03–1.04x | +0.033 us | 9 pairs; 39/40 ms; 40,918/43,271 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 212.363 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.464 us | 8.671 us | 0.63x | 0.63–0.63x | -3.241 us | 9 pairs; 40/40 ms; 7,239/4,641 ops/lane; IQR 1.009/1.010 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.904 us | 0.875 us | 1.03x | 1.03–1.04x | +0.029 us | 9 pairs; 38/41 ms; 42,375/46,899 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 91.975 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.892 us | 0.946 us | 0.94x | 0.94–0.95x | -0.053 us | 9 pairs; 39/40 ms; 43,508/42,636 ops/lane; IQR 1.013/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 120.644 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.992 us | — | — | — | — | — | `—` | 0 B | — | 13.863 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.821 us | 0.848 us | 0.97x | 0.96–0.97x | -0.028 us | 9 pairs; 40/39 ms; 48,648/46,066 ops/lane; IQR 1.010/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 52.369 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.910 us | — | — | — | — | — | `—` | 0 B | — | 13.438 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.870 us | 1.255 us | 0.69x | 0.69–0.69x | -0.389 us | 9 pairs; 40/40 ms; 45,451/31,573 ops/lane; IQR 1.009/1.007 | `Pcre2Slice` | 0 B | 0 B | 65.494 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.509 us | 2.255 us | 0.67x | 0.67–0.67x | -0.746 us | 9 pairs; 42/42 ms; 27,807/18,443 ops/lane; IQR 1.009/1.008 | `Pcre2Literal` | 0 B | 0 B | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.645 us | 1.046 us | 0.62x | 0.62–0.62x | -0.400 us | 9 pairs; 42/39 ms; 64,733/36,848 ops/lane; IQR 1.009/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 129.869 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.669 us | 1.174 us | 0.57x | 0.57–0.57x | -0.502 us | 9 pairs; 39/40 ms; 57,690/34,432 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 113.025 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.613 us | 1.010 us | 0.61x | 0.61–0.61x | -0.395 us | 9 pairs; 41/39 ms; 66,083/38,674 ops/lane; IQR 1.011/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 140.944 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.644 us | 1.143 us | 0.56x | 0.56–0.57x | -0.499 us | 9 pairs; 42/41 ms; 64,715/35,935 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 103.463 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.613 us | 0.931 us | 0.66x | 0.66–0.66x | -0.317 us | 9 pairs; 40/40 ms; 64,813/43,339 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 216.137 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.897 us | 2.152 us | 0.88x | 0.87–0.89x | -0.252 us | 9 pairs; 41/41 ms; 21,460/18,994 ops/lane; IQR 1.011/1.011 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.593 us | 0.872 us | 0.68x | 0.68–0.68x | -0.279 us | 9 pairs; 40/41 ms; 68,309/46,917 ops/lane; IQR 1.013/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 90.750 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.609 us | 0.948 us | 0.64x | 0.64–0.65x | -0.340 us | 9 pairs; 39/38 ms; 64,352/40,259 ops/lane; IQR 1.007/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 123.919 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.686 us | — | — | — | — | — | `—` | 0 B | — | 13.919 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.492 us | 0.848 us | 0.58x | 0.58–0.58x | -0.357 us | 9 pairs; 40/41 ms; 81,145/47,778 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.188 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.640 us | — | — | — | — | — | `—` | 0 B | — | 13.544 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.538 us | 1.260 us | 0.43x | 0.42–0.43x | -0.723 us | 9 pairs; 39/41 ms; 73,225/32,328 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0 B | 0 B | 63.681 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 16.340 us | — | — | — | — | — | `—` | 480 B | — | 1.106 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.117 us | — | — | — | — | — | `—` | 48 B | — | 128.463 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.190 us | — | — | — | — | — | `—` | 48 B | — | 112.425 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.106 us | — | — | — | — | — | `—` | 48 B | — | 144.850 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.101 us | — | — | — | — | — | `—` | 48 B | — | 102.600 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.086 us | — | — | — | — | — | `—` | 48 B | — | 215.781 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.671 us | — | — | — | — | — | `—` | 64 B | — | 1.363 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.116 us | — | — | — | — | — | `—` | 40 B | — | 92.963 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.125 us | — | — | — | — | — | `—` | 40 B | — | 121.131 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.523 us | — | — | — | — | — | `—` | 1,216 B | — | 14.206 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.046 us | — | — | — | — | — | `—` | 48 B | — | 52.688 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.593 us | — | — | — | — | — | `—` | 928 B | — | 13.744 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.670 us | — | — | — | — | — | `—` | 4,496 B | — | 63.362 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 32.405–263.847 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.104–93.993 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.569–12.444 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.996–48.628 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.037–1.122 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.291–42.491 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.086–23.336 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.200–97.997 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.939–70.329 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.191–5.589 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 45.950–360.068 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.074–0.082 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.124–302.630 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.845–5.017 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.109–15.522 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 102.078–818.924 us |

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
