<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `4C26FE29E00933FDCE0FE628363381D0EF560C237E2493C348CBEB23FD6CBCA6`
- Latest managed row measurement: `2026-11-05T22:25:09.4751110+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T22:25:09.4751110+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `75` managed faster, `3` equivalent, `12` native faster, `11` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `3799baaa2b2f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `3799baaa2b2f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.650 us | 2.060 us | 0.32x | 0.30–0.33x | -1.404 us | 9 pairs; 36/31 ms; 55,855/14,865 ops/lane; IQR 1.023/1.100 | `Pcre2Slice` | 463.449 us | 0.277 us | 0.810 us | 0.80x | 1,248 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.869 us | 10.884 us | 0.08x | 0.07–0.08x | -10.024 us | 9 pairs; 36/33 ms; 41,392/3,037 ops/lane; IQR 1.012/1.105 | `Pcre2Slice` | 0.692 us | 0.095 us | 3.559 us | 0.24x | 720 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.303 us | 0.01x | 0.01–0.01x | -1.293 us | 9 pairs; 39/41 ms; 3,652,218/31,131 ops/lane; IQR 1.003/1.009 | `Pcre2Slice` | 0.102 us | 1.055 us | 1.071 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.695 us | 0.159 us | 4.39x | 4.33–4.41x | +0.535 us | 9 pairs; 38/39 ms; 54,063/245,276 ops/lane; IQR 1.009/1.024 | `Pcre2Backtracking` | 0.045 us | 0.116 us | 0.159 us | 4.38x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.286 us | 0.559 us | 4.11x | 4.07–4.14x | +1.730 us | 9 pairs; 38/39 ms; 16,775/69,782 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0.124 us | 0.708 us | 0.747 us | 3.06x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.098 us | 0.306 us | 0.32x | 0.32–0.32x | -0.208 us | 9 pairs; 39/39 ms; 398,932/127,152 ops/lane; IQR 1.008/1.005 | `Pcre2Slice` | 0.112 us | 0.308 us | 0.336 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.104 us | 0.343 us | 0.30x | 0.30–0.30x | -0.239 us | 9 pairs; 40/39 ms; 383,615/113,820 ops/lane; IQR 1.010/1.011 | `Pcre2Slice` | 0.116 us | 0.432 us | 0.464 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.078 us | 0.285 us | 0.27x | 0.27–0.28x | -0.207 us | 9 pairs; 39/40 ms; 505,816/140,242 ops/lane; IQR 1.004/1.013 | `Pcre2Slice` | 0.088 us | 0.222 us | 0.245 us | 0.32x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.080 us | 0.309 us | 0.26x | 0.26–0.26x | -0.228 us | 9 pairs; 41/40 ms; 506,703/130,252 ops/lane; IQR 1.019/1.009 | `Pcre2Slice` | 0.088 us | 0.238 us | 0.263 us | 0.30x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.171 us | 0.078 us | 2.21x | 2.16–2.36x | +0.094 us | 9 pairs; 40/42 ms; 233,721/548,015 ops/lane; IQR 1.029/1.020 | `Pcre2Slice` | 0.087 us | 0.854 us | 0.888 us | 0.19x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.032 us | 0.194 us | 0.17x | 0.17–0.17x | -0.161 us | 9 pairs; 40/43 ms; 1,241,018/219,595 ops/lane; IQR 1.002/1.011 | `Pcre2Backtracking` | 0.110 us | 0.292 us | 0.337 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.602 us | 0.10x | 0.10–0.10x | -0.542 us | 9 pairs; 40/39 ms; 661,978/64,367 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 0.132 us | 0.224 us | 0.247 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 38/41 ms; 1,136,763/267,983 ops/lane; IQR 1.027/1.008 | `Pcre2Backtracking` | 0.104 us | 0.178 us | 0.198 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.111 us | 0.17x | 0.17–0.18x | -0.092 us | 9 pairs; 36/40 ms; 1,884,572/363,823 ops/lane; IQR 1.030/1.007 | `Pcre2Backtracking` | 0.059 us | 0.088 us | 0.107 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.056 us | 0.120 us | 0.47x | 0.46–0.48x | -0.064 us | 9 pairs; 40/38 ms; 701,712/314,899 ops/lane; IQR 1.019/1.037 | `Pcre2Backtracking` | 0.012 us | 0.083 us | 0.101 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.084 us | 0.276 us | 0.31x | 0.31–0.31x | -0.192 us | 9 pairs; 40/41 ms; 472,361/148,356 ops/lane; IQR 1.015/1.018 | `Pcre2Slice` | 0.094 us | 0.118 us | 0.155 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Inconclusive** | 7 B | 0.025 us | 0.113 us | 0.22x | 0.20–0.23x | -0.088 us | 9 pairs; 42/39 ms; 1,667,610/342,199 ops/lane; IQR 1.053/1.011 | `Pcre2Backtracking` | 0.019 us | 0.088 us | 0.106 us | 0.24x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.115 us | 1.923 us | 0.06x | 0.05–0.07x | -1.806 us | 9 pairs; 40/41 ms; 344,335/21,297 ops/lane; IQR 1.008/1.221 | `Pcre2Literal` | 0.085 us | 0.050 us | 0.591 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.118 us | 1.772 us | 0.07x | 0.05–0.07x | -1.655 us | 9 pairs; 40/40 ms; 335,092/22,432 ops/lane; IQR 1.006/1.127 | `Pcre2Literal` | 0.146 us | 0.053 us | 0.578 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Managed faster** | 4,103 B | 0.154 us | 1.816 us | 0.09x | 0.07–0.09x | -1.662 us | 9 pairs; 43/33 ms; 279,513/18,208 ops/lane; IQR 1.012/1.028 | `Pcre2Literal` | 0.198 us | 0.230 us | 0.784 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.147 us | 1.953 us | 0.08x | 0.07–0.08x | -1.801 us | 9 pairs; 40/41 ms; 271,256/20,855 ops/lane; IQR 1.009/1.153 | `Pcre2Literal` | 0.168 us | 0.204 us | 0.731 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.096 us | 0.09x | 0.09–0.09x | -0.088 us | 9 pairs; 39/40 ms; 4,486,654/420,129 ops/lane; IQR 1.006/1.014 | `Pcre2Slice` | 0.248 us | 0.067 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.088 us | 0.76x | 0.75–0.81x | -0.022 us | 9 pairs; 42/41 ms; 620,198/463,102 ops/lane; IQR 1.067/1.012 | `Pcre2Literal` | 0.126 us | 0.050 us | 0.072 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.108 us | 0.12x | 0.12–0.12x | -0.095 us | 9 pairs; 39/40 ms; 2,938,395/372,849 ops/lane; IQR 1.013/1.020 | `Pcre2Backtracking` | 0.265 us | 0.094 us | 0.119 us | 0.11x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Equivalent** | 45 B | 0.121 us | 0.120 us | 1.00x | 0.99–1.01x | 0.000 us | 9 pairs; 44/41 ms; 364,958/340,388 ops/lane; IQR 1.004/1.015 | `Pcre2Character` | 0.137 us | 0.110 us | 0.141 us | 0.85x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.079 us | 0.205 us | 0.38x | 0.37–0.38x | -0.127 us | 9 pairs; 43/42 ms; 543,788/205,091 ops/lane; IQR 1.076/1.050 | `Pcre2Slice` | 0.421 us | 0.074 us | 0.107 us | 0.74x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.367 us | 157.698 us | 0.51x | 0.51–0.51x | -77.543 us | 9 pairs; 39/42 ms; 489/265 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 323.509 us | 95.093 us | 98.300 us | 0.82x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.211 us | 282.973 us | 0.13x | 0.13–0.13x | -245.822 us | 9 pairs; 39/41 ms; 1,053/145 ops/lane; IQR 1.009/1.010 | `Pcre2Character` | 262.143 us | 217.022 us | 226.717 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.291 us | 208.568 us | 0.05x | 0.04–0.05x | -199.287 us | 9 pairs; 41/41 ms; 4,415/198 ops/lane; IQR 1.018/1.046 | `Pcre2Backtracking` | 7.855 us | 212.144 us | 215.497 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 235.222 us | 201.363 us | 1.18x | 1.16–1.19x | +36.192 us | 9 pairs; 39/38 ms; 166/187 ops/lane; IQR 1.006/1.020 | `Pcre2SingleTokenRepeat` | 7.819 us | 227.817 us | 228.251 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.479 us | 23.658 us | 0.15x | 0.14–0.15x | -20.218 us | 9 pairs; 39/43 ms; 11,168/1,815 ops/lane; IQR 1.010/1.024 | `Pcre2Literal` | 1.364 us | 4.198 us | 5.832 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.423 us | 80.467 us | 0.12x | 0.12–0.12x | -71.044 us | 9 pairs; 39/40 ms; 4,171/497 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 9.224 us | 88.073 us | 88.607 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,069.262 us | 26,116.000 us | 0.16x | 0.14–0.17x | -22032.300 us | 9 pairs; 33/52 ms; 8/2 ops/lane; IQR 1.016/1.145 | `Pcre2Backtracking` | 5,542.810 us | 11,264.720 us | 17,790.300 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,279.925 us | 53,187.000 us | 0.06x | 0.06–0.06x | -49839.867 us | 9 pairs; 39/53 ms; 12/1 ops/lane; IQR 1.033/1.023 | `Pcre2Backtracking` | 3,857.793 us | 39,583.600 us | 45,905.650 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,961.910 us | 471,986.000 us | 0.00x | 0.00–0.00x | -470064.471 us | 9 pairs; 41/472 ms; 21/1 ops/lane; IQR 1.037/1.007 | `Pcre2Character` | 1,858.607 us | 22,604.867 us | 29,083.300 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,432.567 us | 11,193.400 us | 0.47x | 0.45–0.50x | -5877.033 us | 9 pairs; 33/34 ms; 6/3 ops/lane; IQR 1.024/1.085 | `Pcre2Literal` | 1,284.224 us | 1,323.937 us | 8,025.657 us | 0.68x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,606.300 us | 144,328.600 us | 0.13x | 0.13–0.13x | -125830.400 us | 9 pairs; 37/144 ms; 2/1 ops/lane; IQR 1.016/1.008 | `Pcre2Backtracking` | 1,230.154 us | 499.002 us | 8,225.583 us | 2.26x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,774.075 us | 21,682.350 us | 0.22x | 0.22–0.22x | -16902.038 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.006/1.014 | `Pcre2Slice` | 2,425.338 us | 10,406.500 us | 18,577.667 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,383.700 us | 129,782.700 us | 0.17x | 0.16–0.17x | -107769.950 us | 9 pairs; 43/130 ms; 2/1 ops/lane; IQR 1.031/1.012 | `Pcre2Backtracking` | 1,502.818 us | 2,265.730 us | 10,122.800 us | 2.11x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 161.522 us | 953.700 us | 0.17x | 0.17–0.17x | -793.287 us | 9 pairs; 39/36 ms; 242/38 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 167.677 us | 310.502 us | 526.954 us | 0.31x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,368.833 us | 13,167.133 us | 0.94x | 0.93–0.95x | -760.900 us | 9 pairs; 37/40 ms; 3/3 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 307.138 us | 14,436.250 us | 14,489.825 us | 0.85x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,746.136 us | 96,561.600 us | 0.03x | 0.03–0.03x | -93785.064 us | 9 pairs; 38/97 ms; 14/1 ops/lane; IQR 1.010/1.005 | `Pcre2Character` | 1,966.012 us | 19,229.733 us | 19,739.367 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 180.568 us | 2,756.286 us | 0.07x | 0.06–0.07x | -2577.362 us | 9 pairs; 39/39 ms; 214/14 ops/lane; IQR 1.010/1.020 | `Pcre2SingleTokenRepeat` | 182.226 us | 988.196 us | 1,410.864 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,522.888 us | 19,394.250 us | 0.08x | 0.08–0.08x | -17869.740 us | 9 pairs; 38/39 ms; 25/2 ops/lane; IQR 1.008/1.012 | `Pcre2Backtracking` | 556.579 us | 10,088.780 us | 10,130.600 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.153 us | 1.084 us | 0.14x | 0.14–0.14x | -0.930 us | 9 pairs; 40/40 ms; 258,787/36,780 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0.635 us | 0.249 us | 0.276 us | 0.56x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.493 us | 0.810 us | 0.61x | 0.61–0.61x | -0.316 us | 9 pairs; 42/39 ms; 85,142/48,489 ops/lane; IQR 1.003/1.006 | `Pcre2Literal` | 0.147 us | 0.148 us | 0.171 us | 2.89x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.293 us | 1.149 us | 0.26x | 0.25–0.26x | -0.855 us | 9 pairs; 40/39 ms; 135,957/33,986 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 1.094 us | 0.348 us | 0.381 us | 0.77x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.258 us | 1.101 us | 1.15x | 1.14–1.15x | +0.161 us | 9 pairs; 59/41 ms; 46,531/37,654 ops/lane; IQR 1.009/1.009 | `Pcre2Character` | 0.283 us | 0.414 us | 0.456 us | 2.76x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.149 us | 0.838 us | 0.18x | 0.18–0.18x | -0.691 us | 9 pairs; 41/40 ms; 274,058/47,839 ops/lane; IQR 1.014/1.010 | `Pcre2Slice` | 0.791 us | 0.151 us | 0.194 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.871 us | 1.079 us | 0.81x | 0.80–0.81x | -0.210 us | 9 pairs; 41/41 ms; 46,676/37,600 ops/lane; IQR 1.010/1.010 | `Pcre2Slice` | 2.961 us | 0.303 us | 0.318 us | 2.74x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.778 us | 0.809 us | 0.96x | 0.96–0.96x | -0.032 us | 9 pairs; 38/40 ms; 48,882/49,449 ops/lane; IQR 1.006/1.006 | `Pcre2Literal` | 3.538 us | 0.178 us | 0.198 us | 3.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.990 us | 1.150 us | 0.86x | 0.85–0.86x | -0.161 us | 9 pairs; 42/40 ms; 42,191/35,157 ops/lane; IQR 1.005/1.011 | `Pcre2Backtracking` | 5.228 us | 0.402 us | 0.439 us | 2.26x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.243 us | 1.108 us | 1.12x | 1.12–1.13x | +0.136 us | 9 pairs; 40/41 ms; 31,832/37,269 ops/lane; IQR 1.006/1.007 | `Pcre2Character` | 3.623 us | 0.469 us | 0.508 us | 2.45x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.769 us | 0.840 us | 0.91x | 0.91–0.92x | -0.073 us | 9 pairs; 39/40 ms; 51,004/48,021 ops/lane; IQR 1.007/1.007 | `Pcre2Slice` | 2.087 us | 0.174 us | 0.214 us | 3.59x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.577 us | 1.072 us | 0.54x | 0.53–0.54x | -0.497 us | 9 pairs; 38/38 ms; 65,990/35,857 ops/lane; IQR 1.013/1.010 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.496 us | 0.817 us | 0.61x | 0.60–0.61x | -0.319 us | 9 pairs; 39/41 ms; 78,452/50,169 ops/lane; IQR 1.010/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.651 us | 1.150 us | 0.56x | 0.56–0.57x | -0.503 us | 9 pairs; 42/39 ms; 64,583/34,166 ops/lane; IQR 1.014/1.013 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.855 us | 1.100 us | 0.77x | 0.77–0.78x | -0.249 us | 9 pairs; 40/40 ms; 46,767/36,440 ops/lane; IQR 1.010/1.003 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.531 us | 0.842 us | 0.63x | 0.63–0.64x | -0.308 us | 9 pairs; 39/40 ms; 74,042/46,981 ops/lane; IQR 1.019/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.154 us | — | — | — | — | — | `—` | 25.181 us | 92.031 us | 94.450 us | 0.65x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.838 us | — | — | — | — | — | `—` | 1.579 us | 0.354 us | 0.376 us | 12.86x | 7,056 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.087 us | — | — | — | — | — | `—` | 0.937 us | 0.237 us | 0.261 us | 4.17x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.236 us | — | — | — | — | — | `—` | 3.086 us | 0.453 us | 0.489 us | 2.53x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.517 us | — | — | — | — | — | `—` | 1.051 us | 0.526 us | 0.566 us | 2.68x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.735 us | — | — | — | — | — | `—` | 1.537 us | 0.265 us | 0.317 us | 11.79x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.091 us | 0.081 us | 1.11x | 1.09–1.13x | +0.009 us | 9 pairs; 42/38 ms; 458,710/474,768 ops/lane; IQR 1.055/1.016 | `Pcre2Character` | 0 B | 0 B | 2.406 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.117 us | 0.11x | 0.11–0.11x | -0.104 us | 9 pairs; 39/41 ms; 2,993,992/353,002 ops/lane; IQR 1.010/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 130.525 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.370 us | 0.100 us | 3.65x | 3.63–3.66x | +0.268 us | 9 pairs; 35/39 ms; 93,919/387,439 ops/lane; IQR 1.015/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 23.363 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.397 us | 0.115 us | 3.44x | 3.42–3.46x | +0.282 us | 9 pairs; 35/39 ms; 88,854/336,944 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 71.744 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.375 us | 0.100 us | 3.77x | 3.58–3.79x | +0.276 us | 9 pairs; 36/38 ms; 96,381/380,693 ops/lane; IQR 1.016/1.028 | `Pcre2Backtracking` | 0 B | 0 B | 71.481 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.575 us | 0.339 us | 1.69x | 1.69–1.70x | +0.237 us | 9 pairs; 38/39 ms; 65,881/116,177 ops/lane; IQR 1.011/1.009 | `Pcre2Character` | 0 B | 0 B | 2.062 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.628 us | 0.02x | 0.02–0.02x | -0.616 us | 9 pairs; 41/39 ms; 3,500,699/61,901 ops/lane; IQR 1.033/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 23.456 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.012 us | 0.493 us | 0.02x | 0.02–0.02x | -0.482 us | 9 pairs; 39/40 ms; 3,414,667/80,654 ops/lane; IQR 1.022/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 24.275 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.454 us | 0.106 us | 4.31x | 4.23–4.34x | +0.349 us | 9 pairs; 35/41 ms; 77,601/383,748 ops/lane; IQR 1.022/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 17.694 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.152 us | 10.107 us | 0.81x | 0.80–0.81x | -1.919 us | 9 pairs; 39/40 ms; 4,781/3,940 ops/lane; IQR 1.008/1.016 | `Pcre2Literal` | 0 B | 0 B | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.289 us | 1.024 us | 0.28x | 0.28–0.28x | -0.735 us | 9 pairs; 40/40 ms; 138,032/39,088 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 133.488 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.314 us | 0.455 us | 0.69x | 0.68–0.70x | -0.140 us | 9 pairs; 40/40 ms; 126,649/87,275 ops/lane; IQR 1.009/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 116.294 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.284 us | 0.453 us | 0.63x | 0.62–0.63x | -0.170 us | 9 pairs; 36/37 ms; 125,320/81,302 ops/lane; IQR 1.011/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 142.156 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.301 us | 0.572 us | 0.52x | 0.52–0.53x | -0.274 us | 9 pairs; 36/40 ms; 119,255/69,782 ops/lane; IQR 1.016/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 103.856 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.294 us | 0.388 us | 0.76x | 0.75–0.77x | -0.093 us | 9 pairs; 35/39 ms; 119,174/100,441 ops/lane; IQR 1.009/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 215.356 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.940 us | 8.650 us | 0.57x | 0.57–0.58x | -3.686 us | 9 pairs; 39/41 ms; 7,922/4,736 ops/lane; IQR 1.008/1.004 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.248 us | 0.305 us | 0.81x | 0.81–0.82x | -0.056 us | 9 pairs; 35/40 ms; 141,821/131,565 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 90.763 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.262 us | 0.398 us | 0.66x | 0.65–0.66x | -0.136 us | 9 pairs; 33/38 ms; 126,181/95,438 ops/lane; IQR 1.005/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 120.525 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.748 us | — | — | — | — | — | `—` | 0 B | — | 15.113 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.195 us | 0.296 us | 0.66x | 0.65–0.66x | -0.100 us | 9 pairs; 33/40 ms; 170,175/135,342 ops/lane; IQR 1.012/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.550 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.630 us | — | — | — | — | — | `—` | 0 B | — | 13.906 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.574 us | 0.656 us | 0.87x | 0.87–0.88x | -0.085 us | 9 pairs; 38/38 ms; 66,448/58,473 ops/lane; IQR 1.017/1.008 | `Pcre2Slice` | 0 B | 0 B | 63.931 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.629 us | 29.322 us | 0.33x | 0.33–0.33x | -19.678 us | 9 pairs; 41/41 ms; 4,288/1,403 ops/lane; IQR 1.008/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.806 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.990 us | 1.033 us | 0.95x | 0.94–0.98x | -0.053 us | 9 pairs; 40/40 ms; 40,553/38,773 ops/lane; IQR 1.022/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 130.375 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.989 us | 1.175 us | 0.84x | 0.83–0.85x | -0.186 us | 9 pairs; 39/41 ms; 39,772/35,292 ops/lane; IQR 1.009/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 112.169 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.938 us | 1.010 us | 0.93x | 0.92–0.93x | -0.072 us | 9 pairs; 39/42 ms; 42,072/41,457 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 136.875 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.942 us | 1.166 us | 0.81x | 0.81–0.81x | -0.223 us | 9 pairs; 40/41 ms; 42,326/35,370 ops/lane; IQR 1.015/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 104.856 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.918 us | 0.917 us | 1.00x | 0.99–1.00x | -0.002 us | 9 pairs; 39/41 ms; 42,469/44,935 ops/lane; IQR 1.008/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 214.369 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.584 us | 8.690 us | 0.64x | 0.64–0.64x | -3.124 us | 9 pairs; 39/40 ms; 7,035/4,570 ops/lane; IQR 1.008/1.008 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.917 us | 0.885 us | 1.04x | 1.04–1.05x | +0.035 us | 9 pairs; 37/43 ms; 40,521/48,426 ops/lane; IQR 1.020/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 90.000 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.904 us | 0.963 us | 0.94x | 0.93–0.94x | -0.059 us | 9 pairs; 40/40 ms; 44,133/41,728 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 122.825 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.951 us | — | — | — | — | — | `—` | 0 B | — | 16.481 us | 11,496 B |
| `pcre2/kreset-global` | **Equivalent** | 26 B | 0.857 us | 0.847 us | 1.01x | 1.01–1.01x | +0.011 us | 9 pairs; 42/41 ms; 49,070/48,294 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.806 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.892 us | — | — | — | — | — | `—` | 0 B | — | 15.225 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.898 us | 1.231 us | 0.73x | 0.72–0.73x | -0.337 us | 9 pairs; 40/39 ms; 44,019/31,369 ops/lane; IQR 1.016/1.012 | `Pcre2Slice` | 0 B | 0 B | 65.219 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.316 us | 2.298 us | 0.57x | 0.57–0.58x | -0.977 us | 9 pairs; 39/41 ms; 29,688/17,976 ops/lane; IQR 1.010/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.659 us | 1.028 us | 0.64x | 0.64–0.64x | -0.371 us | 9 pairs; 37/41 ms; 56,905/39,461 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 130.150 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.687 us | 1.169 us | 0.59x | 0.59–0.59x | -0.479 us | 9 pairs; 42/39 ms; 61,011/33,483 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 114.638 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.617 us | 1.010 us | 0.61x | 0.60–0.62x | -0.392 us | 9 pairs; 37/38 ms; 60,568/38,007 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 140.744 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.622 us | 1.167 us | 0.53x | 0.53–0.53x | -0.547 us | 9 pairs; 40/39 ms; 63,796/33,694 ops/lane; IQR 1.014/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 104.606 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.674 us | 0.919 us | 0.73x | 0.73–0.74x | -0.245 us | 9 pairs; 44/40 ms; 65,599/43,748 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 215.963 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.856 us | 2.149 us | 0.86x | 0.86–0.87x | -0.296 us | 9 pairs; 40/40 ms; 21,318/18,742 ops/lane; IQR 1.005/1.010 | `Pcre2Character` | 0 B | 0 B | 1.394 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.629 us | 0.860 us | 0.73x | 0.73–0.73x | -0.231 us | 9 pairs; 39/39 ms; 61,462/45,499 ops/lane; IQR 1.007/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 90.312 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.600 us | 0.971 us | 0.62x | 0.61–0.62x | -0.371 us | 9 pairs; 39/39 ms; 65,322/40,202 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 122.350 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.648 us | — | — | — | — | — | `—` | 0 B | — | 13.819 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.481 us | 0.849 us | 0.57x | 0.56–0.57x | -0.367 us | 9 pairs; 39/39 ms; 81,805/45,718 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.569 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.514 us | — | — | — | — | — | `—` | 0 B | — | 14.294 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.582 us | 1.226 us | 0.47x | 0.47–0.48x | -0.645 us | 9 pairs; 39/39 ms; 66,550/32,218 ops/lane; IQR 1.009/1.005 | `Pcre2Slice` | 0 B | 0 B | 64.919 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.629 us | — | — | — | — | — | `—` | 480 B | — | 0.725 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.151 us | — | — | — | — | — | `—` | 48 B | — | 130.769 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.258 us | — | — | — | — | — | `—` | 48 B | — | 116.869 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.164 us | — | — | — | — | — | `—` | 48 B | — | 143.394 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.147 us | — | — | — | — | — | `—` | 48 B | — | 102.412 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.129 us | — | — | — | — | — | `—` | 48 B | — | 220.700 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.840 us | — | — | — | — | — | `—` | 64 B | — | 1.381 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.169 us | — | — | — | — | — | `—` | 40 B | — | 92.756 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.170 us | — | — | — | — | — | `—` | 40 B | — | 120.612 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.945 us | — | — | — | — | — | `—` | 1,216 B | — | 13.594 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.079 us | — | — | — | — | — | `—` | 48 B | — | 53.131 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.326 us | — | — | — | — | — | `—` | 928 B | — | 15.206 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.684 us | — | — | — | — | — | `—` | 7,184 B | — | 63.575 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 30.024–241.672 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.253–92.925 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.867–12.551 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.746–45.873 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.031–1.133 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.267–40.601 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.684–22.102 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.152–97.275 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.914–70.832 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.209–5.337 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.579–359.613 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.075 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 37.538–294.701 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.819–5.048 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.130–15.439 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 101.330–838.510 us |

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
