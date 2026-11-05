<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `FD491E4B7FA107B600D7DF58D8ACB1C0629AD0C64DDA91DDAAA305ED402606DE`
- Latest managed row measurement: `2026-11-04T23:57:42.4255305+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T23:57:42.4255305+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `0` equivalent, `10` native faster, `15` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `434359064e2f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `434359064e2f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.827 us | 2.458 us | 0.34x | 0.29–0.46x | -1.632 us | 9 pairs; 30/48 ms; 36,772/19,355 ops/lane; IQR 1.067/1.309 | `Pcre2Slice` | 559.412 us | 0.278 us | 0.782 us | 1.06x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.117 us | 12.977 us | 0.09x | 0.08–0.09x | -11.857 us | 9 pairs; 35/58 ms; 31,387/4,495 ops/lane; IQR 1.004/1.079 | `Pcre2Slice` | 0.794 us | 0.097 us | 3.410 us | 0.33x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.207 us | 0.01x | 0.01–0.01x | -1.196 us | 9 pairs; 40/39 ms; 3,647,305/32,410 ops/lane; IQR 1.018/1.010 | `Pcre2Slice` | 0.100 us | 1.274 us | 1.291 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.681 us | 0.158 us | 4.30x | 4.26–4.35x | +0.524 us | 9 pairs; 39/40 ms; 57,058/253,815 ops/lane; IQR 1.011/1.020 | `Pcre2Backtracking` | 0.046 us | 0.122 us | 0.156 us | 4.37x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.319 us | 0.567 us | 4.10x | 4.05–4.11x | +1.751 us | 9 pairs; 38/40 ms; 16,441/70,593 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 0.126 us | 0.793 us | 0.820 us | 2.83x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.098 us | 0.295 us | 0.33x | 0.33–0.34x | -0.197 us | 9 pairs; 39/41 ms; 394,205/137,285 ops/lane; IQR 1.014/1.030 | `Pcre2Slice` | 0.105 us | 0.321 us | 0.348 us | 0.28x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.103 us | 0.347 us | 0.30x | 0.29–0.30x | -0.245 us | 9 pairs; 40/40 ms; 389,309/115,294 ops/lane; IQR 1.010/1.005 | `Pcre2Slice` | 0.110 us | 0.479 us | 0.755 us | 0.14x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.078 us | 0.287 us | 0.27x | 0.27–0.27x | -0.209 us | 9 pairs; 41/39 ms; 527,808/136,390 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 0.085 us | 0.243 us | 0.261 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.080 us | 0.310 us | 0.26x | 0.25–0.26x | -0.228 us | 9 pairs; 41/40 ms; 506,516/129,188 ops/lane; IQR 1.014/1.014 | `Pcre2Slice` | 0.084 us | 0.256 us | 0.277 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.169 us | 0.074 us | 2.27x | 2.22–2.49x | +0.094 us | 9 pairs; 40/41 ms; 235,411/555,192 ops/lane; IQR 1.049/1.048 | `Pcre2Slice` | 0.082 us | 1.015 us | 1.043 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.201 us | 0.16x | 0.16–0.17x | -0.167 us | 9 pairs; 39/41 ms; 1,192,191/205,250 ops/lane; IQR 1.057/1.012 | `Pcre2Backtracking` | 0.103 us | 0.312 us | 0.355 us | 0.09x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.059 us | 0.594 us | 0.10x | 0.10–0.10x | -0.536 us | 9 pairs; 29/39 ms; 488,919/65,442 ops/lane; IQR 1.011/1.013 | `Pcre2Backtracking` | 0.126 us | 0.242 us | 0.266 us | 0.22x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.032 us | 0.150 us | 0.22x | 0.21–0.22x | -0.118 us | 9 pairs; 36/39 ms; 1,113,962/262,426 ops/lane; IQR 1.025/1.010 | `Pcre2Backtracking` | 0.109 us | 0.182 us | 0.198 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.112 us | 0.18x | 0.17–0.18x | -0.092 us | 9 pairs; 40/41 ms; 2,019,444/362,810 ops/lane; IQR 1.017/1.007 | `Pcre2Backtracking` | 0.057 us | 0.092 us | 0.109 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.121 us | 0.47x | 0.47–0.48x | -0.064 us | 9 pairs; 40/41 ms; 701,879/338,867 ops/lane; IQR 1.018/1.021 | `Pcre2Backtracking` | 0.015 us | 0.092 us | 0.110 us | 0.52x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.084 us | 0.278 us | 0.30x | 0.30–0.31x | -0.194 us | 9 pairs; 40/41 ms; 479,547/147,501 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 0.090 us | 0.115 us | 0.149 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.023 us | 0.115 us | 0.20x | 0.19–0.20x | -0.092 us | 9 pairs; 41/40 ms; 1,757,940/350,717 ops/lane; IQR 1.028/1.017 | `Pcre2Backtracking` | 0.019 us | 0.099 us | 0.115 us | 0.20x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.115 us | 1.972 us | 0.06x | 0.05–0.07x | -1.858 us | 9 pairs; 40/37 ms; 346,594/18,982 ops/lane; IQR 1.012/1.130 | `Pcre2Literal` | 0.086 us | 0.049 us | 0.594 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.118 us | 2.225 us | 0.05x | 0.05–0.06x | -2.107 us | 9 pairs; 39/54 ms; 332,205/24,215 ops/lane; IQR 1.004/1.071 | `Pcre2Literal` | 0.142 us | 0.053 us | 0.592 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.151 us | 2.132 us | 0.07x | 0.06–0.08x | -1.982 us | 9 pairs; 40/44 ms; 263,313/20,443 ops/lane; IQR 1.008/1.166 | `Pcre2Literal` | 0.196 us | 0.228 us | 0.755 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.148 us | 1.861 us | 0.08x | 0.07–0.08x | -1.714 us | 9 pairs; 40/38 ms; 269,080/20,489 ops/lane; IQR 1.007/1.016 | `Pcre2Literal` | 0.169 us | 0.200 us | 0.749 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.099 us | 0.09x | 0.09–0.09x | -0.090 us | 9 pairs; 39/39 ms; 4,447,301/397,056 ops/lane; IQR 1.002/1.008 | `Pcre2Slice` | 0.263 us | 0.070 us | 0.089 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.087 us | 0.77x | 0.76–0.77x | -0.020 us | 9 pairs; 39/39 ms; 584,382/454,532 ops/lane; IQR 1.011/1.008 | `Pcre2Literal` | 0.122 us | 0.050 us | 0.071 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.108 us | 0.12x | 0.12–0.12x | -0.095 us | 9 pairs; 39/39 ms; 2,932,478/364,355 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0.236 us | 0.091 us | 0.112 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.113 us | 0.122 us | 0.93x | 0.92–0.93x | -0.009 us | 9 pairs; 38/39 ms; 337,182/322,901 ops/lane; IQR 1.007/1.010 | `Pcre2Character` | 0.130 us | 0.113 us | 0.142 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.082 us | 0.199 us | 0.41x | 0.37–0.41x | -0.120 us | 9 pairs; 46/42 ms; 564,060/213,179 ops/lane; IQR 1.106/1.017 | `Pcre2Slice` | 0.437 us | 0.073 us | 0.107 us | 0.77x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.539 us | 155.922 us | 0.52x | 0.51–0.52x | -75.242 us | 9 pairs; 39/41 ms; 489/260 ops/lane; IQR 1.003/1.012 | `Pcre2Slice` | 369.946 us | 107.241 us | 108.792 us | 0.74x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.161 us | 285.463 us | 0.13x | 0.13–0.13x | -248.540 us | 9 pairs; 41/40 ms; 1,092/141 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 389.202 us | 214.426 us | 223.020 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.088 us | 205.958 us | 0.05x | 0.05–0.05x | -195.901 us | 9 pairs; 39/41 ms; 3,839/200 ops/lane; IQR 1.006/1.018 | `Pcre2Backtracking` | 7.979 us | 226.070 us | 231.514 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 237.755 us | 204.975 us | 1.16x | 1.15–1.17x | +33.581 us | 9 pairs; 40/41 ms; 168/200 ops/lane; IQR 1.005/1.008 | `Pcre2SingleTokenRepeat` | 7.896 us | 233.845 us | 236.774 us | 1.00x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.451 us | 23.185 us | 0.15x | 0.15–0.15x | -19.714 us | 9 pairs; 39/41 ms; 11,406/1,761 ops/lane; IQR 1.006/1.005 | `Pcre2Literal` | 1.371 us | 4.196 us | 5.987 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.478 us | 79.236 us | 0.12x | 0.12–0.12x | -69.761 us | 9 pairs; 39/40 ms; 4,126/500 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 9.128 us | 94.092 us | 94.694 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,044.500 us | 28,686.500 us | 0.14x | 0.14–0.15x | -24685.450 us | 9 pairs; 40/29 ms; 10/1 ops/lane; IQR 1.019/1.033 | `Pcre2Backtracking` | 5,419.700 us | 12,522.900 us | 19,143.967 us | 0.21x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,376.117 us | 55,057.800 us | 0.06x | 0.06–0.06x | -51681.683 us | 9 pairs; 41/55 ms; 12/1 ops/lane; IQR 1.022/1.014 | `Pcre2Backtracking` | 3,842.557 us | 44,131.650 us | 51,021.000 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,902.850 us | 485,238.000 us | 0.00x | 0.00–0.00x | -483376.450 us | 9 pairs; 42/485 ms; 22/1 ops/lane; IQR 1.032/1.008 | `Pcre2Character` | 1,756.300 us | 22,776.733 us | 29,482.450 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Managed faster** | 16,013,977 B | 5,576.217 us | 11,090.367 us | 0.50x | 0.48–0.52x | -5507.883 us | 9 pairs; 33/33 ms; 6/3 ops/lane; IQR 1.012/1.070 | `Pcre2Literal` | 1,304.695 us | 1,310.848 us | 8,194.471 us | 0.68x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,317.250 us | 142,318.700 us | 0.13x | 0.13–0.13x | -124261.500 us | 9 pairs; 37/142 ms; 2/1 ops/lane; IQR 1.016/1.010 | `Pcre2Backtracking` | 1,223.207 us | 498.460 us | 8,232.050 us | 2.23x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,734.238 us | 21,644.700 us | 0.22x | 0.22–0.22x | -16898.025 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.011/1.015 | `Pcre2Slice` | 2,476.257 us | 11,242.860 us | 18,885.500 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,788.100 us | 131,357.100 us | 0.17x | 0.16–0.17x | -109656.400 us | 9 pairs; 44/131 ms; 2/1 ops/lane; IQR 1.008/1.011 | `Pcre2Backtracking` | 1,543.730 us | 2,262.709 us | 10,004.267 us | 2.18x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 171.549 us | 910.200 us | 0.19x | 0.19–0.19x | -738.842 us | 9 pairs; 38/40 ms; 222/44 ops/lane; IQR 1.006/1.021 | `Pcre2Backtracking` | 167.015 us | 343.432 us | 565.612 us | 0.30x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,260.300 us | 13,148.100 us | 0.85x | 0.85–0.87x | -1892.400 us | 9 pairs; 34/39 ms; 3/3 ops/lane; IQR 1.012/1.014 | `Pcre2Backtracking` | 288.257 us | 16,306.400 us | 16,259.225 us | 0.69x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,702.950 us | 96,945.400 us | 0.03x | 0.03–0.03x | -94209.236 us | 9 pairs; 38/97 ms; 14/1 ops/lane; IQR 1.011/1.011 | `Pcre2Character` | 1,938.415 us | 19,588.733 us | 19,843.233 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 182.669 us | 2,673.150 us | 0.07x | 0.07–0.07x | -2490.221 us | 9 pairs; 39/37 ms; 213/14 ops/lane; IQR 1.011/1.012 | `Pcre2SingleTokenRepeat` | 182.741 us | 994.125 us | 1,429.805 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Inconclusive** | 594,930 B | 1,531.168 us | 19,796.650 us | 0.08x | 0.08–0.08x | -18228.662 us | 9 pairs; 38/40 ms; 25/2 ops/lane; IQR 1.026/1.009 | `Pcre2Backtracking` | 570.606 us | 10,846.120 us | 10,997.380 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.149 us | 1.077 us | 0.14x | 0.14–0.14x | -0.928 us | 9 pairs; 39/41 ms; 263,893/38,225 ops/lane; IQR 1.011/1.014 | `Pcre2Slice` | 0.710 us | 0.257 us | 0.277 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.479 us | 0.817 us | 0.59x | 0.58–0.59x | -0.339 us | 9 pairs; 40/41 ms; 82,881/49,596 ops/lane; IQR 1.006/1.004 | `Pcre2Literal` | 0.159 us | 0.147 us | 0.170 us | 2.82x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.294 us | 1.153 us | 0.25x | 0.25–0.26x | -0.860 us | 9 pairs; 39/39 ms; 133,837/34,016 ops/lane; IQR 1.010/1.010 | `Pcre2Backtracking` | 1.033 us | 0.343 us | 0.372 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.858 us | 1.110 us | 0.77x | 0.77–0.78x | -0.255 us | 9 pairs; 41/41 ms; 48,241/36,770 ops/lane; IQR 1.009/1.006 | `Pcre2Character` | 0.277 us | 0.424 us | 0.465 us | 1.85x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.159 us | 0.846 us | 0.19x | 0.18–0.19x | -0.689 us | 9 pairs; 39/41 ms; 246,587/48,226 ops/lane; IQR 1.013/1.008 | `Pcre2Slice` | 0.828 us | 0.150 us | 0.193 us | 0.83x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.892 us | 1.089 us | 0.82x | 0.81–0.82x | -0.197 us | 9 pairs; 41/39 ms; 45,844/36,152 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | 3.665 us | 0.305 us | 0.317 us | 2.81x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.791 us | 0.829 us | 0.95x | 0.95–0.97x | -0.037 us | 9 pairs; 41/41 ms; 52,412/49,435 ops/lane; IQR 1.006/1.008 | `Pcre2Literal` | 3.485 us | 0.176 us | 0.200 us | 3.96x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.961 us | 1.153 us | 0.83x | 0.83–0.84x | -0.192 us | 9 pairs; 40/41 ms; 41,592/35,912 ops/lane; IQR 1.004/1.012 | `Pcre2Backtracking` | 5.234 us | 0.401 us | 0.426 us | 2.25x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.158 us | 1.124 us | 1.03x | 1.03–1.04x | +0.036 us | 9 pairs; 39/41 ms; 33,585/36,343 ops/lane; IQR 1.006/1.004 | `Pcre2Character` | 3.567 us | 0.475 us | 0.511 us | 2.26x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.787 us | 0.852 us | 0.92x | 0.92–0.93x | -0.066 us | 9 pairs; 39/42 ms; 49,909/49,322 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 2.141 us | 0.179 us | 0.219 us | 3.59x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.548 us | 1.074 us | 0.51x | 0.51–0.51x | -0.526 us | 9 pairs; 40/40 ms; 72,653/37,471 ops/lane; IQR 1.017/1.017 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.497 us | 0.817 us | 0.61x | 0.61–0.61x | -0.318 us | 9 pairs; 40/41 ms; 80,937/49,689 ops/lane; IQR 1.006/1.006 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.675 us | 1.154 us | 0.59x | 0.58–0.59x | -0.478 us | 9 pairs; 38/39 ms; 56,189/33,697 ops/lane; IQR 1.007/1.017 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.909 us | 1.128 us | 0.81x | 0.80–0.81x | -0.216 us | 9 pairs; 27/41 ms; 29,551/36,176 ops/lane; IQR 1.015/1.007 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.471 us | 0.854 us | 0.55x | 0.55–0.56x | -0.380 us | 9 pairs; 41/41 ms; 86,290/47,899 ops/lane; IQR 1.008/1.009 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 59.440 us | — | — | — | — | — | `—` | 24.968 us | 99.276 us | 99.396 us | 0.60x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.426 us | — | — | — | — | — | `—` | 1.772 us | 0.351 us | 0.380 us | 16.92x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.018 us | — | — | — | — | — | `—` | 0.930 us | 0.238 us | 0.266 us | 3.82x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.152 us | — | — | — | — | — | `—` | 3.094 us | 0.449 us | 0.480 us | 2.40x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.453 us | — | — | — | — | — | `—` | 1.007 us | 0.542 us | 0.574 us | 2.53x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.234 us | — | — | — | — | — | `—` | 1.599 us | 0.262 us | 0.312 us | 13.58x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.087 us | 0.081 us | 1.07x | 1.05–1.08x | +0.006 us | 9 pairs; 41/41 ms; 471,094/506,551 ops/lane; IQR 1.014/1.015 | `Pcre2Character` | 0 B | 0 B | 2.281 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.119 us | 0.11x | 0.11–0.11x | -0.106 us | 9 pairs; 39/39 ms; 3,042,996/329,317 ops/lane; IQR 1.014/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 133.756 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.377 us | 0.103 us | 3.70x | 3.57–3.74x | +0.275 us | 9 pairs; 35/41 ms; 92,024/399,915 ops/lane; IQR 1.018/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 20.931 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.387 us | 0.115 us | 3.33x | 3.23–3.40x | +0.273 us | 9 pairs; 35/39 ms; 89,234/339,598 ops/lane; IQR 1.023/1.063 | `Pcre2Backtracking` | 0 B | 0 B | 71.444 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.369 us | 0.100 us | 3.66x | 3.65–3.69x | +0.268 us | 9 pairs; 36/40 ms; 96,481/400,869 ops/lane; IQR 1.027/1.030 | `Pcre2Backtracking` | 0 B | 0 B | 67.912 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.561 us | 0.344 us | 1.63x | 1.62–1.65x | +0.218 us | 9 pairs; 37/40 ms; 66,268/115,963 ops/lane; IQR 1.008/1.005 | `Pcre2Character` | 0 B | 0 B | 1.488 us | 2,248 B |
| `pcre2/recursive-optional` | **Inconclusive** | 6 B | 0.012 us | 0.614 us | 0.02x | 0.02–0.02x | -0.602 us | 9 pairs; 38/39 ms; 3,223,610/63,973 ops/lane; IQR 1.056/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 23.950 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.498 us | 0.02x | 0.02–0.02x | -0.486 us | 9 pairs; 41/39 ms; 3,427,466/78,902 ops/lane; IQR 1.043/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 22.319 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.468 us | 0.109 us | 4.34x | 4.27–4.37x | +0.360 us | 9 pairs; 37/41 ms; 78,879/373,601 ops/lane; IQR 1.013/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 18.012 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.180 us | 10.499 us | 0.78x | 0.77–0.79x | -2.307 us | 9 pairs; 39/42 ms; 4,773/4,026 ops/lane; IQR 1.006/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.296 us | 1.026 us | 0.29x | 0.29–0.29x | -0.730 us | 9 pairs; 39/40 ms; 131,414/38,876 ops/lane; IQR 1.006/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 129.312 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.342 us | 0.454 us | 0.75x | 0.68–0.76x | -0.116 us | 9 pairs; 41/40 ms; 119,429/89,060 ops/lane; IQR 1.098/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 112.694 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.286 us | 0.455 us | 0.63x | 0.62–0.63x | -0.169 us | 9 pairs; 37/38 ms; 130,702/84,276 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 141.969 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.304 us | 0.583 us | 0.52x | 0.52–0.52x | -0.282 us | 9 pairs; 38/41 ms; 123,907/69,749 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 106.194 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.292 us | 0.363 us | 0.80x | 0.80–0.81x | -0.071 us | 9 pairs; 37/41 ms; 125,903/112,517 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 213.906 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.747 us | 8.751 us | 0.54x | 0.54–0.54x | -4.016 us | 9 pairs; 39/39 ms; 8,230/4,406 ops/lane; IQR 1.011/1.007 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.257 us | 0.304 us | 0.84x | 0.84–0.85x | -0.047 us | 9 pairs; 33/39 ms; 128,539/127,800 ops/lane; IQR 1.011/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 90.244 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.263 us | 0.390 us | 0.68x | 0.67–0.69x | -0.126 us | 9 pairs; 37/38 ms; 139,939/98,089 ops/lane; IQR 1.006/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 123.362 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.824 us | — | — | — | — | — | `—` | 0 B | — | 13.731 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.182 us | 0.289 us | 0.63x | 0.62–0.63x | -0.108 us | 9 pairs; 39/41 ms; 216,535/141,405 ops/lane; IQR 1.017/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 53.031 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.758 us | — | — | — | — | — | `—` | 0 B | — | 13.844 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.588 us | 0.665 us | 0.88x | 0.88–0.89x | -0.079 us | 9 pairs; 38/40 ms; 64,874/60,342 ops/lane; IQR 1.005/1.009 | `Pcre2Slice` | 0 B | 0 B | 63.131 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 11.000 us | 28.600 us | 0.38x | 0.38–0.39x | -17.610 us | 9 pairs; 44/41 ms; 4,038/1,421 ops/lane; IQR 1.009/1.013 | `Pcre2Literal` | 0 B | 0 B | 0.806 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.935 us | 1.041 us | 0.90x | 0.89–0.90x | -0.105 us | 9 pairs; 39/40 ms; 42,005/38,830 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 130.338 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.020 us | 1.160 us | 0.88x | 0.87–0.88x | -0.144 us | 9 pairs; 40/41 ms; 39,236/35,244 ops/lane; IQR 1.004/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 113.669 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.932 us | 1.021 us | 0.91x | 0.90–0.92x | -0.091 us | 9 pairs; 38/41 ms; 41,282/40,538 ops/lane; IQR 1.014/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 142.012 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.924 us | 1.161 us | 0.79x | 0.79–0.80x | -0.239 us | 9 pairs; 40/38 ms; 43,639/32,548 ops/lane; IQR 1.008/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 103.225 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.922 us | 0.945 us | 0.98x | 0.97–0.99x | -0.018 us | 9 pairs; 39/43 ms; 41,876/44,993 ops/lane; IQR 1.012/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 210.294 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.413 us | 8.768 us | 0.62x | 0.61–0.62x | -3.351 us | 9 pairs; 39/39 ms; 7,217/4,496 ops/lane; IQR 1.007/1.012 | `Pcre2Character` | 0 B | 0 B | 2.131 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Inconclusive** | 13 B | 0.896 us | 0.874 us | 1.02x | 1.02–1.03x | +0.020 us | 9 pairs; 40/42 ms; 44,449/47,674 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 92.150 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.976 us | 0.953 us | 1.02x | 1.02–1.03x | +0.023 us | 9 pairs; 41/40 ms; 42,307/42,415 ops/lane; IQR 1.013/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 121.850 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.048 us | — | — | — | — | — | `—` | 0 B | — | 13.481 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.813 us | 0.838 us | 0.97x | 0.96–0.97x | -0.026 us | 9 pairs; 40/41 ms; 49,411/49,007 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 53.219 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.976 us | — | — | — | — | — | `—` | 0 B | — | 14.081 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.845 us | 1.247 us | 0.68x | 0.68–0.68x | -0.401 us | 9 pairs; 41/41 ms; 48,923/32,653 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.638 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.376 us | 2.247 us | 0.61x | 0.61–0.62x | -0.870 us | 9 pairs; 39/39 ms; 28,527/17,408 ops/lane; IQR 1.013/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.825 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.660 us | 1.037 us | 0.64x | 0.64–0.64x | -0.375 us | 9 pairs; 41/40 ms; 61,789/38,243 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 130.344 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.643 us | 1.165 us | 0.55x | 0.55–0.56x | -0.524 us | 9 pairs; 39/39 ms; 60,487/33,352 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 115.000 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.647 us | 1.010 us | 0.64x | 0.64–0.64x | -0.361 us | 9 pairs; 40/41 ms; 62,358/40,190 ops/lane; IQR 1.002/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 141.244 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.622 us | 1.158 us | 0.54x | 0.53–0.54x | -0.538 us | 9 pairs; 41/38 ms; 65,288/32,652 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.912 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.649 us | 0.941 us | 0.69x | 0.69–0.69x | -0.292 us | 9 pairs; 39/39 ms; 60,070/41,615 ops/lane; IQR 1.007/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 213.350 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.777 us | 2.163 us | 0.82x | 0.82–0.83x | -0.383 us | 9 pairs; 39/40 ms; 21,828/18,295 ops/lane; IQR 1.003/1.010 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.667 us | 0.863 us | 0.77x | 0.76–0.78x | -0.199 us | 9 pairs; 44/40 ms; 65,390/46,410 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 92.750 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.599 us | 0.950 us | 0.63x | 0.63–0.63x | -0.351 us | 9 pairs; 39/39 ms; 65,900/41,553 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 120.800 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.797 us | — | — | — | — | — | `—` | 0 B | — | 14.162 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.490 us | 0.838 us | 0.58x | 0.58–0.59x | -0.350 us | 9 pairs; 38/41 ms; 77,708/48,581 ops/lane; IQR 1.012/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.456 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.682 us | — | — | — | — | — | `—` | 0 B | — | 13.806 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.536 us | 1.252 us | 0.43x | 0.42–0.43x | -0.716 us | 9 pairs; 40/40 ms; 74,375/32,029 ops/lane; IQR 1.006/1.002 | `Pcre2Slice` | 0 B | 0 B | 62.269 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.030 us | — | — | — | — | — | `—` | 480 B | — | 1.163 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.077 us | — | — | — | — | — | `—` | 48 B | — | 129.400 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.170 us | — | — | — | — | — | `—` | 48 B | — | 114.156 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.058 us | — | — | — | — | — | `—` | 48 B | — | 142.912 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.077 us | — | — | — | — | — | `—` | 48 B | — | 102.763 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.062 us | — | — | — | — | — | `—` | 48 B | — | 223.613 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.705 us | — | — | — | — | — | `—` | 64 B | — | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.080 us | — | — | — | — | — | `—` | 40 B | — | 91.450 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.069 us | — | — | — | — | — | `—` | 40 B | — | 121.806 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.685 us | — | — | — | — | — | `—` | 1,216 B | — | 13.631 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.013 us | — | — | — | — | — | `—` | 48 B | — | 53.175 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.525 us | — | — | — | — | — | `—` | 928 B | — | 13.850 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.317 us | — | — | — | — | — | `—` | 23,312 B | — | 62.044 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.193–226.398 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.122–93.932 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.963–12.389 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.669–44.745 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.033–1.286 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.626–43.320 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.093–23.623 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.157–99.402 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.054–72.106 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.165–5.426 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 45.814–369.837 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.642–294.464 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.817–4.977 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.132–15.332 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 167.050–1,293.735 us |

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
