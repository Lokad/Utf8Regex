<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `16587DC200E4C57CA8B3590224C645E97381E4B2BCF0B6169ED831ECE0F28CFB`
- Latest managed row measurement: `2026-11-01T12:07:58.9396267+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-01T12:07:58.9396267+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `40/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `69` managed faster, `0` equivalent, `17` native faster, `15` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `2ebc01a79afb` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `2ebc01a79afb` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 1.129 us | 2.249 us | 0.50x | 0.45–0.57x | -1.127 us | 9 pairs; 32/47 ms; 28,636/20,811 ops/lane; IQR 1.023/1.089 | `Pcre2Slice` | 874.566 us | 0.275 us | 0.777 us | 1.45x | 5,744 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.320 us | 11.137 us | 0.12x | 0.11–0.13x | -9.818 us | 9 pairs; 35/46 ms; 26,565/4,086 ops/lane; IQR 1.018/1.081 | `Pcre2Slice` | 1.813 us | 0.093 us | 3.300 us | 0.40x | 3,872 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.205 us | 0.01x | 0.01–0.01x | -1.195 us | 9 pairs; 38/41 ms; 3,619,274/34,159 ops/lane; IQR 1.013/1.004 | `Pcre2Slice` | 0.207 us | 1.162 us | 1.207 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.802 us | 0.158 us | 11.41x | 11.27–11.46x | +1.644 us | 9 pairs; 40/40 ms; 22,010/251,506 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0.046 us | 0.120 us | 0.151 us | 11.96x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 6.397 us | 0.558 us | 11.50x | 11.44–11.56x | +5.843 us | 9 pairs; 39/41 ms; 6,071/74,126 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 0.123 us | 0.734 us | 0.757 us | 8.45x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.170 us | 0.284 us | 0.60x | 0.59–0.60x | -0.114 us | 9 pairs; 40/40 ms; 234,045/141,856 ops/lane; IQR 1.022/1.025 | `Pcre2Slice` | 0.178 us | 0.309 us | 0.334 us | 0.51x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.177 us | 0.350 us | 0.51x | 0.50–0.51x | -0.173 us | 9 pairs; 40/39 ms; 228,041/112,204 ops/lane; IQR 1.008/1.007 | `Pcre2Slice` | 0.186 us | 0.431 us | 0.450 us | 0.39x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.118 us | 0.282 us | 0.42x | 0.41–0.42x | -0.164 us | 9 pairs; 39/38 ms; 333,856/135,777 ops/lane; IQR 1.014/1.003 | `Pcre2Slice` | 0.133 us | 0.231 us | 0.256 us | 0.46x | 0 B | 0 B |
| `common/ip-miss` | **Inconclusive** | 15 B | 0.113 us | 0.310 us | 0.36x | 0.36–0.41x | -0.195 us | 9 pairs; 38/38 ms; 332,856/122,662 ops/lane; IQR 1.110/1.017 | `Pcre2Slice` | 0.133 us | 0.251 us | 0.270 us | 0.42x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.168 us | 0.077 us | 2.16x | 2.13–2.24x | +0.090 us | 9 pairs; 39/44 ms; 235,078/570,704 ops/lane; IQR 1.005/1.031 | `Pcre2Slice` | 0.208 us | 0.966 us | 0.987 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.067 us | 0.195 us | 0.34x | 0.34–0.34x | -0.128 us | 9 pairs; 39/39 ms; 580,394/201,933 ops/lane; IQR 1.009/1.017 | `Pcre2Backtracking` | 0.197 us | 0.300 us | 0.336 us | 0.20x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.131 us | 0.593 us | 0.22x | 0.22–0.22x | -0.461 us | 9 pairs; 40/39 ms; 305,186/65,244 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0.190 us | 0.224 us | 0.242 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 39/40 ms; 1,182,283/258,467 ops/lane; IQR 1.042/1.010 | `Pcre2Backtracking` | 0.106 us | 0.175 us | 0.195 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.110 us | 0.18x | 0.17–0.18x | -0.091 us | 9 pairs; 39/40 ms; 2,002,093/361,731 ops/lane; IQR 1.014/1.011 | `Pcre2Backtracking` | 0.064 us | 0.088 us | 0.106 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Inconclusive** | 8 B | 0.057 us | 0.121 us | 0.47x | 0.46–0.48x | -0.064 us | 9 pairs; 39/41 ms; 688,477/338,253 ops/lane; IQR 1.005/1.019 | `Pcre2Backtracking` | 0.015 us | 0.082 us | 0.099 us | 0.58x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.184 us | 0.269 us | 0.69x | 0.68–0.69x | -0.084 us | 9 pairs; 40/43 ms; 214,935/158,690 ops/lane; IQR 1.008/1.015 | `Pcre2Slice` | 0.202 us | 0.116 us | 0.150 us | 1.23x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.113 us | 0.19x | 0.19–0.19x | -0.092 us | 9 pairs; 37/40 ms; 1,715,571/355,853 ops/lane; IQR 1.031/1.016 | `Pcre2Backtracking` | 0.019 us | 0.087 us | 0.106 us | 0.20x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.110 us | 2.050 us | 0.05x | 0.05–0.06x | -1.940 us | 9 pairs; 37/46 ms; 333,614/22,230 ops/lane; IQR 1.013/1.169 | `Pcre2Literal` | 0.119 us | 0.049 us | 0.622 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.114 us | 2.315 us | 0.05x | 0.04–0.05x | -2.201 us | 9 pairs; 39/44 ms; 337,938/19,124 ops/lane; IQR 1.009/1.085 | `Pcre2Literal` | 0.173 us | 0.052 us | 0.601 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.140 us | 1.922 us | 0.07x | 0.07–0.08x | -1.783 us | 9 pairs; 39/43 ms; 276,880/22,165 ops/lane; IQR 1.006/1.066 | `Pcre2Literal` | 0.273 us | 0.225 us | 0.809 us | 0.17x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.148 us | 1.848 us | 0.08x | 0.08–0.08x | -1.699 us | 9 pairs; 41/36 ms; 278,521/19,590 ops/lane; IQR 1.037/1.031 | `Pcre2Literal` | 0.201 us | 0.200 us | 0.748 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.096 us | 0.09x | 0.09–0.09x | -0.087 us | 9 pairs; 40/40 ms; 4,686,792/418,667 ops/lane; IQR 1.006/1.034 | `Pcre2Slice` | 0.947 us | 0.067 us | 0.085 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.060 us | 0.083 us | 0.71x | 0.71–0.72x | -0.024 us | 9 pairs; 39/40 ms; 648,796/478,086 ops/lane; IQR 1.003/1.005 | `Pcre2Literal` | 0.126 us | 0.049 us | 0.071 us | 0.84x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.107 us | 0.12x | 0.12–0.13x | -0.094 us | 9 pairs; 38/40 ms; 2,891,082/369,964 ops/lane; IQR 1.009/1.009 | `Pcre2Backtracking` | 0.287 us | 0.093 us | 0.117 us | 0.11x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 0.206 us | 0.120 us | 1.71x | 1.70–1.73x | +0.086 us | 9 pairs; 34/40 ms; 165,587/330,271 ops/lane; IQR 1.015/1.013 | `Pcre2Character` | 0.148 us | 0.110 us | 0.140 us | 1.47x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.077 us | 0.208 us | 0.37x | 0.36–0.38x | -0.131 us | 9 pairs; 41/41 ms; 528,966/197,599 ops/lane; IQR 1.003/1.026 | `Pcre2Slice` | 0.780 us | 0.073 us | 0.102 us | 0.75x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 76.297 us | 158.729 us | 0.48x | 0.48–0.49x | -81.796 us | 9 pairs; 40/40 ms; 526/254 ops/lane; IQR 1.012/1.011 | `Pcre2Slice` | 393.811 us | 99.577 us | 101.354 us | 0.75x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.158 us | 274.195 us | 0.14x | 0.14–0.14x | -237.102 us | 9 pairs; 39/38 ms; 1,055/138 ops/lane; IQR 1.003/1.007 | `Pcre2Character` | 383.172 us | 215.851 us | 224.754 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.263 us | 206.522 us | 0.05x | 0.05–0.05x | -196.259 us | 9 pairs; 41/40 ms; 3,958/193 ops/lane; IQR 1.011/1.019 | `Pcre2Backtracking` | 8.022 us | 216.038 us | 218.617 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 557.377 us | 196.808 us | 2.83x | 2.81–2.85x | +360.569 us | 9 pairs; 39/40 ms; 70/204 ops/lane; IQR 1.006/1.007 | `Pcre2SingleTokenRepeat` | 8.202 us | 236.166 us | 234.819 us | 2.37x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.452 us | 22.857 us | 0.15x | 0.15–0.15x | -19.411 us | 9 pairs; 40/41 ms; 11,492/1,798 ops/lane; IQR 1.005/1.025 | `Pcre2Literal` | 1.401 us | 4.169 us | 5.887 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.298 us | 79.411 us | 0.12x | 0.12–0.12x | -70.142 us | 9 pairs; 39/40 ms; 4,200/510 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 9.221 us | 92.602 us | 93.732 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,051.688 us | 27,326.500 us | 0.15x | 0.14–0.15x | -23274.813 us | 9 pairs; 32/27 ms; 8/1 ops/lane; IQR 1.015/1.027 | `Pcre2Backtracking` | 5,506.760 us | 11,951.980 us | 18,644.267 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 5,619.700 us | 54,028.600 us | 0.10x | 0.10–0.11x | -48416.414 us | 9 pairs; 39/54 ms; 7/1 ops/lane; IQR 1.009/1.038 | `Pcre2Backtracking` | 6,325.575 us | 40,201.950 us | 46,497.350 us | 0.12x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,885.350 us | 503,911.000 us | 0.00x | 0.00–0.00x | -502054.355 us | 9 pairs; 41/504 ms; 22/1 ops/lane; IQR 1.021/1.005 | `Pcre2Character` | 1,732.770 us | 21,886.867 us | 28,656.250 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,514.367 us | 10,559.833 us | 0.52x | 0.50–0.60x | -5040.883 us | 9 pairs; 33/32 ms; 6/3 ops/lane; IQR 1.002/1.171 | `Pcre2Literal` | 1,376.953 us | 1,327.341 us | 8,093.457 us | 0.68x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,707.250 us | 145,498.500 us | 0.13x | 0.13–0.13x | -126832.650 us | 9 pairs; 37/145 ms; 2/1 ops/lane; IQR 1.034/1.023 | `Pcre2Backtracking` | 1,573.081 us | 501.668 us | 8,418.133 us | 2.22x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,744.812 us | 21,542.150 us | 0.22x | 0.22–0.22x | -16783.663 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.011/1.011 | `Pcre2Slice` | 2,838.178 us | 10,564.040 us | 18,169.600 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,905.650 us | 131,600.400 us | 0.17x | 0.17–0.18x | -108694.750 us | 9 pairs; 46/132 ms; 2/1 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 1,867.611 us | 2,297.617 us | 10,320.460 us | 2.22x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 234.277 us | 964.376 us | 0.25x | 0.24–0.25x | -732.376 us | 9 pairs; 39/40 ms; 167/41 ops/lane; IQR 1.030/1.045 | `Pcre2Backtracking` | 273.823 us | 323.114 us | 535.389 us | 0.44x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,545.700 us | 13,256.600 us | 0.94x | 0.93–0.96x | -738.800 us | 9 pairs; 38/40 ms; 3/3 ops/lane; IQR 1.016/1.010 | `Pcre2Backtracking` | 298.111 us | 15,126.050 us | 15,263.075 us | 0.82x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,853.314 us | 96,721.400 us | 0.03x | 0.03–0.03x | -93859.793 us | 9 pairs; 40/97 ms; 14/1 ops/lane; IQR 1.007/1.007 | `Pcre2Character` | 1,841.875 us | 19,382.233 us | 19,405.000 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 181.196 us | 2,685.027 us | 0.07x | 0.07–0.07x | -2505.104 us | 9 pairs; 39/40 ms; 215/15 ops/lane; IQR 1.008/1.010 | `Pcre2SingleTokenRepeat` | 184.249 us | 979.794 us | 1,402.188 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,580.208 us | 19,577.100 us | 0.08x | 0.08–0.08x | -17973.022 us | 9 pairs; 40/39 ms; 25/2 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 570.656 us | 9,918.360 us | 10,175.760 us | 0.16x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.147 us | 1.060 us | 0.14x | 0.14–0.14x | -0.912 us | 9 pairs; 40/40 ms; 271,927/37,928 ops/lane; IQR 1.018/1.008 | `Pcre2Slice` | 1.405 us | 0.251 us | 0.274 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.464 us | 0.799 us | 0.58x | 0.58–0.58x | -0.334 us | 9 pairs; 40/40 ms; 85,526/49,905 ops/lane; IQR 1.010/1.015 | `Pcre2Literal` | 0.146 us | 0.147 us | 0.169 us | 2.74x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.327 us | 1.135 us | 0.29x | 0.29–0.29x | -0.808 us | 9 pairs; 43/40 ms; 132,845/35,197 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 1.055 us | 0.348 us | 0.377 us | 0.87x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.363 us | 1.095 us | 1.24x | 1.24–1.25x | +0.268 us | 9 pairs; 39/41 ms; 28,774/37,058 ops/lane; IQR 1.005/1.006 | `Pcre2Character` | 0.271 us | 0.421 us | 0.469 us | 2.90x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.162 us | 0.832 us | 0.19x | 0.19–0.20x | -0.672 us | 9 pairs; 40/42 ms; 249,826/50,071 ops/lane; IQR 1.016/1.009 | `Pcre2Slice` | 1.179 us | 0.150 us | 0.195 us | 0.83x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.920 us | 1.069 us | 0.86x | 0.86–0.86x | -0.150 us | 9 pairs; 40/41 ms; 43,196/38,786 ops/lane; IQR 1.007/1.007 | `Pcre2Slice` | 6.234 us | 0.300 us | 0.315 us | 2.92x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.786 us | 0.812 us | 0.97x | 0.96–0.97x | -0.028 us | 9 pairs; 39/42 ms; 50,118/51,202 ops/lane; IQR 1.009/1.016 | `Pcre2Literal` | 3.494 us | 0.174 us | 0.198 us | 3.97x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.061 us | 1.148 us | 0.92x | 0.92–0.93x | -0.087 us | 9 pairs; 39/39 ms; 36,542/33,797 ops/lane; IQR 1.007/1.012 | `Pcre2Backtracking` | 5.186 us | 0.407 us | 0.428 us | 2.48x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.722 us | 1.114 us | 1.54x | 1.53–1.55x | +0.601 us | 9 pairs; 40/41 ms; 23,081/36,396 ops/lane; IQR 1.005/1.009 | `Pcre2Character` | 3.646 us | 0.472 us | 0.516 us | 3.34x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.822 us | 0.842 us | 0.98x | 0.97–0.98x | -0.019 us | 9 pairs; 43/40 ms; 52,243/47,828 ops/lane; IQR 1.009/1.006 | `Pcre2Slice` | 2.661 us | 0.179 us | 0.216 us | 3.81x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.590 us | 1.062 us | 0.56x | 0.55–0.56x | -0.472 us | 9 pairs; 44/40 ms; 75,015/37,502 ops/lane; IQR 1.003/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.525 us | 0.810 us | 0.65x | 0.65–0.65x | -0.284 us | 9 pairs; 40/40 ms; 76,190/49,669 ops/lane; IQR 1.006/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.638 us | 1.150 us | 0.56x | 0.55–0.56x | -0.514 us | 9 pairs; 40/40 ms; 63,442/34,814 ops/lane; IQR 1.010/1.011 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.423 us | 1.118 us | 1.28x | 1.27–1.28x | +0.306 us | 9 pairs; 40/41 ms; 28,215/36,886 ops/lane; IQR 1.010/1.005 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.466 us | 0.840 us | 0.55x | 0.55–0.55x | -0.375 us | 9 pairs; 38/38 ms; 80,634/45,677 ops/lane; IQR 1.009/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.276 us | — | — | — | — | — | `—` | 64.614 us | 95.849 us | 99.288 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.099 us | — | — | — | — | — | `—` | 3.171 us | 0.350 us | 0.376 us | 24.18x | 48,792 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.100 us | — | — | — | — | — | `—` | 0.838 us | 0.236 us | 0.265 us | 4.16x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.238 us | — | — | — | — | — | `—` | 2.836 us | 0.453 us | 0.485 us | 2.55x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 2.103 us | — | — | — | — | — | `—` | 0.930 us | 0.531 us | 0.576 us | 3.65x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 5.079 us | — | — | — | — | — | `—` | 1.636 us | 0.257 us | 0.295 us | 17.22x | 7,192 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.118 us | 0.081 us | 1.46x | 1.45–1.47x | +0.037 us | 9 pairs; 41/40 ms; 342,965/491,225 ops/lane; IQR 1.012/1.017 | `Pcre2Character` | 0 B | 0 B | 2.237 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.115 us | 0.11x | 0.11–0.11x | -0.103 us | 9 pairs; 39/41 ms; 3,025,320/351,814 ops/lane; IQR 1.012/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 132.425 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.454 us | 0.105 us | 4.35x | 4.29–4.38x | +0.350 us | 9 pairs; 37/40 ms; 80,789/381,721 ops/lane; IQR 1.008/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 20.519 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.468 us | 0.118 us | 4.00x | 3.93–4.00x | +0.351 us | 9 pairs; 35/41 ms; 75,474/345,643 ops/lane; IQR 1.019/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 69.075 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.467 us | 0.102 us | 4.56x | 4.53–4.58x | +0.365 us | 9 pairs; 36/40 ms; 77,915/393,027 ops/lane; IQR 1.015/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 70.356 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.561 us | 0.342 us | 1.64x | 1.63–1.65x | +0.220 us | 9 pairs; 36/41 ms; 64,982/118,570 ops/lane; IQR 1.008/1.008 | `Pcre2Character` | 0 B | 0 B | 1.406 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.617 us | 0.02x | 0.02–0.02x | -0.605 us | 9 pairs; 42/39 ms; 3,542,167/63,634 ops/lane; IQR 1.037/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 21.856 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.490 us | 0.02x | 0.02–0.02x | -0.478 us | 9 pairs; 41/37 ms; 3,519,656/76,482 ops/lane; IQR 1.011/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 20.894 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.528 us | 0.108 us | 4.96x | 4.81–4.97x | +0.419 us | 9 pairs; 36/39 ms; 68,702/360,573 ops/lane; IQR 1.010/1.025 | `Pcre2Backtracking` | 0 B | 0 B | 17.906 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.160 us | 10.370 us | 0.79x | 0.78–0.79x | -2.223 us | 9 pairs; 37/39 ms; 4,574/3,773 ops/lane; IQR 1.008/1.012 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.293 us | 1.026 us | 0.28x | 0.28–0.29x | -0.732 us | 9 pairs; 41/41 ms; 138,525/39,680 ops/lane; IQR 1.015/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 128.575 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.346 us | 0.451 us | 0.77x | 0.76–0.81x | -0.104 us | 9 pairs; 40/38 ms; 114,429/84,493 ops/lane; IQR 1.038/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 114.375 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.365 us | 0.455 us | 0.81x | 0.74–0.81x | -0.087 us | 9 pairs; 43/42 ms; 116,673/91,631 ops/lane; IQR 1.062/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 136.450 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.292 us | 0.571 us | 0.51x | 0.51–0.51x | -0.279 us | 9 pairs; 37/41 ms; 125,452/72,259 ops/lane; IQR 1.013/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 103.275 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.311 us | 0.391 us | 0.81x | 0.76–0.82x | -0.076 us | 9 pairs; 36/39 ms; 116,034/98,804 ops/lane; IQR 1.050/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 210.856 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.993 us | 8.584 us | 0.58x | 0.58–0.58x | -3.601 us | 9 pairs; 41/42 ms; 8,114/4,843 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.261 us | 0.307 us | 0.85x | 0.84–0.86x | -0.047 us | 9 pairs; 37/38 ms; 143,160/122,659 ops/lane; IQR 1.013/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 93.331 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.272 us | 0.405 us | 0.67x | 0.67–0.68x | -0.134 us | 9 pairs; 37/40 ms; 136,267/98,042 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 118.925 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.067 us | — | — | — | — | — | `—` | 0 B | — | 13.806 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.187 us | 0.279 us | 0.66x | 0.66–0.67x | -0.094 us | 9 pairs; 40/39 ms; 214,594/137,873 ops/lane; IQR 1.011/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 53.331 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.935 us | — | — | — | — | — | `—` | 0 B | — | 13.956 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.497 us | 0.661 us | 0.76x | 0.75–0.76x | -0.158 us | 9 pairs; 37/38 ms; 73,752/57,921 ops/lane; IQR 1.007/1.022 | `Pcre2Slice` | 0 B | 0 B | 64.669 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.726 us | 28.409 us | 0.34x | 0.34–0.34x | -18.687 us | 9 pairs; 41/40 ms; 4,167/1,425 ops/lane; IQR 1.011/1.007 | `Pcre2Literal` | 0 B | 0 B | 1.225 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.986 us | 1.030 us | 0.95x | 0.94–0.96x | -0.048 us | 9 pairs; 39/40 ms; 39,953/38,913 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 128.350 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.044 us | 1.168 us | 0.89x | 0.88–0.89x | -0.127 us | 9 pairs; 40/40 ms; 37,942/34,561 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 110.912 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Inconclusive** | 31 B | 0.983 us | 1.010 us | 0.97x | 0.97–0.98x | -0.027 us | 9 pairs; 39/41 ms; 40,165/40,295 ops/lane; IQR 1.011/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 136.231 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.997 us | 1.142 us | 0.87x | 0.87–0.88x | -0.143 us | 9 pairs; 39/41 ms; 39,381/35,742 ops/lane; IQR 1.016/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 101.844 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.974 us | 0.928 us | 1.05x | 1.04–1.05x | +0.045 us | 9 pairs; 38/41 ms; 39,517/44,543 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 212.363 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.811 us | 8.644 us | 0.67x | 0.67–0.67x | -2.851 us | 9 pairs; 37/39 ms; 6,425/4,499 ops/lane; IQR 1.006/1.011 | `Pcre2Character` | 0 B | 0 B | 1.387 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.947 us | 0.866 us | 1.09x | 1.08–1.10x | +0.077 us | 9 pairs; 41/39 ms; 42,891/45,407 ops/lane; IQR 1.010/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 96.031 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.973 us | 0.953 us | 1.02x | 1.02–1.03x | +0.022 us | 9 pairs; 39/41 ms; 40,218/43,381 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 123.850 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.399 us | — | — | — | — | — | `—` | 0 B | — | 13.156 us | 11,496 B |
| `pcre2/kreset-global` | **Native faster** | 26 B | 0.874 us | 0.853 us | 1.02x | 1.02–1.03x | +0.021 us | 9 pairs; 34/41 ms; 38,683/48,034 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 54.031 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.280 us | — | — | — | — | — | `—` | 0 B | — | 13.869 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.836 us | 1.218 us | 0.69x | 0.69–0.69x | -0.381 us | 9 pairs; 39/38 ms; 46,646/31,481 ops/lane; IQR 1.010/1.008 | `Pcre2Slice` | 0 B | 0 B | 64.775 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.326 us | 2.239 us | 0.59x | 0.59–0.59x | -0.914 us | 9 pairs; 39/39 ms; 29,660/17,537 ops/lane; IQR 1.009/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.641 us | 1.028 us | 0.62x | 0.62–0.63x | -0.390 us | 9 pairs; 36/41 ms; 55,946/39,443 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 132.044 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.705 us | 1.178 us | 0.60x | 0.60–0.60x | -0.470 us | 9 pairs; 40/40 ms; 56,635/34,027 ops/lane; IQR 1.012/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 114.856 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.602 us | 0.993 us | 0.61x | 0.60–0.61x | -0.391 us | 9 pairs; 39/39 ms; 65,230/38,989 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 140.650 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.605 us | 1.135 us | 0.53x | 0.53–0.54x | -0.529 us | 9 pairs; 39/40 ms; 64,458/35,078 ops/lane; IQR 1.010/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 103.106 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.593 us | 0.924 us | 0.64x | 0.63–0.64x | -0.336 us | 9 pairs; 40/41 ms; 68,037/43,862 ops/lane; IQR 1.012/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 217.713 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.827 us | 2.163 us | 0.85x | 0.84–0.85x | -0.329 us | 9 pairs; 40/40 ms; 22,040/18,349 ops/lane; IQR 1.009/1.010 | `Pcre2Character` | 0 B | 0 B | 1.381 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.582 us | 0.865 us | 0.67x | 0.66–0.67x | -0.286 us | 9 pairs; 37/39 ms; 63,590/44,798 ops/lane; IQR 1.013/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 93.644 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.585 us | 0.953 us | 0.62x | 0.61–0.62x | -0.367 us | 9 pairs; 36/41 ms; 61,946/43,525 ops/lane; IQR 1.010/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 122.062 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.003 us | — | — | — | — | — | `—` | 0 B | — | 13.444 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.482 us | 0.843 us | 0.57x | 0.57–0.58x | -0.360 us | 9 pairs; 38/41 ms; 77,837/48,904 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 52.119 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.839 us | — | — | — | — | — | `—` | 0 B | — | 14.594 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.564 us | 1.218 us | 0.46x | 0.46–0.46x | -0.654 us | 9 pairs; 39/40 ms; 69,574/33,092 ops/lane; IQR 1.015/1.018 | `Pcre2Slice` | 0 B | 0 B | 63.888 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.024 us | — | — | — | — | — | `—` | 480 B | — | 0.725 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.147 us | — | — | — | — | — | `—` | 48 B | — | 129.631 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.256 us | — | — | — | — | — | `—` | 48 B | — | 115.875 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.133 us | — | — | — | — | — | `—` | 48 B | — | 143.275 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.161 us | — | — | — | — | — | `—` | 48 B | — | 102.344 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.123 us | — | — | — | — | — | `—` | 48 B | — | 209.506 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.195 us | — | — | — | — | — | `—` | 64 B | — | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.159 us | — | — | — | — | — | `—` | 40 B | — | 94.000 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.165 us | — | — | — | — | — | `—` | 40 B | — | 121.106 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.104 us | — | — | — | — | — | `—` | 1,224 B | — | 13.031 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.107 us | — | — | — | — | — | `—` | 48 B | — | 53.531 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.915 us | — | — | — | — | — | `—` | 936 B | — | 13.800 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.925 us | — | — | — | — | — | `—` | 28,936 B | — | 64.106 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 27.534–215.227 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 11.904–91.826 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.174–13.968 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.053–47.508 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.034–1.485 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.745–44.276 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.586–21.905 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.143–97.098 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.797–70.224 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.130–4.916 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.650–367.776 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.062–0.071 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 41.435–325.227 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.851–4.975 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 17.922–135.248 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 166.860–1,340.249 us |

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
