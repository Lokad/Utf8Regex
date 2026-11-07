<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `B5594FBEE8165E77466B31E2EFB746B25621124AFDFE72E437ADAD65D185389D`
- Latest managed row measurement: `2026-11-07T18:29:26.8570542+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-07T18:29:26.8570542+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `74` managed faster, `0` equivalent, `13` native faster, `14` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `553acbdfc895` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `553acbdfc895` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.595 us | 2.153 us | 0.27x | 0.27–0.31x | -1.569 us | 9 pairs; 36/41 ms; 61,360/19,084 ops/lane; IQR 1.022/1.121 | `Pcre2Slice` | 452.499 us | 0.274 us | 0.794 us | 0.75x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.732 us | 11.189 us | 0.06x | 0.06–0.07x | -10.462 us | 9 pairs; 34/47 ms; 46,942/4,182 ops/lane; IQR 1.006/1.144 | `Pcre2Slice` | 0.570 us | 0.095 us | 3.585 us | 0.20x | 64 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.803 us | 0.01x | 0.01–0.01x | -1.792 us | 9 pairs; 40/39 ms; 3,614,118/21,519 ops/lane; IQR 1.014/1.033 | `Pcre2Slice` | 0.101 us | 1.113 us | 1.155 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.677 us | 0.153 us | 4.43x | 4.38–4.49x | +0.522 us | 9 pairs; 37/39 ms; 53,923/255,302 ops/lane; IQR 1.010/1.015 | `Pcre2Backtracking` | 0.048 us | 0.120 us | 0.153 us | 4.42x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.313 us | 0.560 us | 4.13x | 4.10–4.18x | +1.753 us | 9 pairs; 38/40 ms; 16,543/70,648 ops/lane; IQR 1.012/1.007 | `Pcre2Backtracking` | 0.116 us | 0.762 us | 0.783 us | 2.95x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.101 us | 0.291 us | 0.34x | 0.34–0.35x | -0.191 us | 9 pairs; 41/40 ms; 403,849/136,302 ops/lane; IQR 1.022/1.007 | `Pcre2Slice` | 0.106 us | 0.312 us | 0.342 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.103 us | 0.345 us | 0.30x | 0.30–0.30x | -0.241 us | 9 pairs; 40/41 ms; 382,745/117,696 ops/lane; IQR 1.010/1.017 | `Pcre2Slice` | 0.112 us | 0.445 us | 0.471 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.283 us | 0.28x | 0.28–0.28x | -0.203 us | 9 pairs; 40/39 ms; 501,099/137,924 ops/lane; IQR 1.009/1.010 | `Pcre2Slice` | 0.085 us | 0.235 us | 0.258 us | 0.31x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.079 us | 0.307 us | 0.26x | 0.26–0.26x | -0.228 us | 9 pairs; 40/40 ms; 509,390/129,955 ops/lane; IQR 1.012/1.011 | `Pcre2Slice` | 0.086 us | 0.253 us | 0.278 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.171 us | 0.075 us | 2.26x | 2.23–2.33x | +0.096 us | 9 pairs; 40/38 ms; 230,730/506,107 ops/lane; IQR 1.014/1.027 | `Pcre2Slice` | 0.085 us | 0.956 us | 0.995 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.192 us | 0.17x | 0.17–0.17x | -0.160 us | 9 pairs; 40/39 ms; 1,217,043/203,236 ops/lane; IQR 1.001/1.010 | `Pcre2Backtracking` | 0.102 us | 0.306 us | 0.351 us | 0.09x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.059 us | 0.597 us | 0.10x | 0.10–0.10x | -0.538 us | 9 pairs; 39/41 ms; 656,585/67,989 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0.126 us | 0.232 us | 0.256 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.150 us | 0.22x | 0.22–0.22x | -0.117 us | 9 pairs; 37/39 ms; 1,110,565/258,289 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0.113 us | 0.179 us | 0.195 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.110 us | 0.18x | 0.17–0.18x | -0.090 us | 9 pairs; 39/40 ms; 1,966,350/365,693 ops/lane; IQR 1.012/1.015 | `Pcre2Backtracking` | 0.059 us | 0.090 us | 0.108 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Inconclusive** | 8 B | 0.056 us | 0.119 us | 0.47x | 0.45–0.51x | -0.063 us | 9 pairs; 40/42 ms; 702,820/349,558 ops/lane; IQR 1.011/1.060 | `Pcre2Backtracking` | 0.015 us | 0.090 us | 0.110 us | 0.51x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.083 us | 0.272 us | 0.30x | 0.30–0.31x | -0.190 us | 9 pairs; 40/37 ms; 479,629/136,898 ops/lane; IQR 1.012/1.005 | `Pcre2Slice` | 0.088 us | 0.115 us | 0.149 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.114 us | 0.21x | 0.19–0.21x | -0.090 us | 9 pairs; 43/39 ms; 1,787,731/346,270 ops/lane; IQR 1.054/1.008 | `Pcre2Backtracking` | 0.019 us | 0.097 us | 0.113 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.116 us | 2.240 us | 0.05x | 0.05–0.06x | -2.122 us | 9 pairs; 40/47 ms; 342,152/21,129 ops/lane; IQR 1.008/1.186 | `Pcre2Literal` | 0.086 us | 0.049 us | 0.566 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.120 us | 1.782 us | 0.07x | 0.06–0.07x | -1.661 us | 9 pairs; 39/37 ms; 326,469/20,893 ops/lane; IQR 1.009/1.145 | `Pcre2Literal` | 0.142 us | 0.052 us | 0.582 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Managed faster** | 4,103 B | 0.154 us | 1.902 us | 0.08x | 0.07–0.09x | -1.748 us | 9 pairs; 40/49 ms; 261,683/25,752 ops/lane; IQR 1.007/1.084 | `Pcre2Literal` | 0.200 us | 0.227 us | 0.758 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.149 us | 1.825 us | 0.08x | 0.08–0.08x | -1.676 us | 9 pairs; 40/35 ms; 271,265/19,161 ops/lane; IQR 1.007/1.051 | `Pcre2Literal` | 0.172 us | 0.198 us | 0.715 us | 0.21x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.009 us | 0.096 us | 0.09x | 0.09–0.09x | -0.087 us | 9 pairs; 39/42 ms; 4,506,709/432,698 ops/lane; IQR 1.009/1.031 | `Pcre2Slice` | 0.218 us | 0.069 us | 0.090 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.066 us | 0.086 us | 0.78x | 0.76–0.78x | -0.019 us | 9 pairs; 40/39 ms; 603,505/454,280 ops/lane; IQR 1.008/1.029 | `Pcre2Literal` | 0.131 us | 0.050 us | 0.073 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.106 us | 0.13x | 0.12–0.13x | -0.093 us | 9 pairs; 40/39 ms; 2,881,125/368,257 ops/lane; IQR 1.003/1.028 | `Pcre2Backtracking` | 0.286 us | 0.089 us | 0.112 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.115 us | 0.121 us | 0.96x | 0.94–1.02x | -0.005 us | 9 pairs; 41/39 ms; 352,856/326,165 ops/lane; IQR 1.065/1.013 | `Pcre2Character` | 0.144 us | 0.110 us | 0.141 us | 0.82x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.074 us | 0.202 us | 0.37x | 0.36–0.37x | -0.128 us | 9 pairs; 40/41 ms; 539,791/200,861 ops/lane; IQR 1.011/1.008 | `Pcre2Slice` | 0.324 us | 0.074 us | 0.106 us | 0.70x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 77.371 us | 156.501 us | 0.49x | 0.49–0.50x | -79.282 us | 9 pairs; 40/39 ms; 518/251 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 320.389 us | 105.273 us | 107.147 us | 0.72x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 38.895 us | 277.308 us | 0.14x | 0.14–0.14x | -238.420 us | 9 pairs; 39/39 ms; 1,003/139 ops/lane; IQR 1.006/1.006 | `Pcre2Character` | 262.485 us | 214.665 us | 226.651 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.087 us | 204.188 us | 0.05x | 0.05–0.05x | -194.107 us | 9 pairs; 41/38 ms; 4,031/185 ops/lane; IQR 1.014/1.011 | `Pcre2Backtracking` | 7.644 us | 220.036 us | 218.581 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 234.711 us | 210.187 us | 1.12x | 1.10–1.12x | +25.153 us | 9 pairs; 40/40 ms; 170/191 ops/lane; IQR 1.010/1.002 | `Pcre2SingleTokenRepeat` | 7.757 us | 228.689 us | 232.047 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.343 us | 23.343 us | 0.14x | 0.14–0.15x | -19.991 us | 9 pairs; 39/38 ms; 11,560/1,636 ops/lane; IQR 1.005/1.030 | `Pcre2Literal` | 1.343 us | 4.156 us | 5.951 us | 0.56x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.437 us | 80.110 us | 0.12x | 0.12–0.12x | -70.601 us | 9 pairs; 40/39 ms; 4,242/493 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 9.217 us | 91.575 us | 92.585 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,092.300 us | 27,659.550 us | 0.15x | 0.15–0.15x | -23539.438 us | 9 pairs; 33/55 ms; 8/2 ops/lane; IQR 1.014/1.033 | `Pcre2Backtracking` | 5,396.121 us | 11,464.229 us | 18,165.200 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,224.408 us | 53,665.900 us | 0.06x | 0.06–0.06x | -50425.808 us | 9 pairs; 39/54 ms; 12/1 ops/lane; IQR 1.008/1.019 | `Pcre2Backtracking` | 3,832.515 us | 40,479.050 us | 46,919.650 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Managed faster** | 16,013,977 B | 1,879.781 us | 480,323.800 us | 0.00x | 0.00–0.00x | -478467.105 us | 9 pairs; 39/480 ms; 21/1 ops/lane; IQR 1.014/1.006 | `Pcre2Character` | 1,749.536 us | 22,284.950 us | 29,100.100 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Managed faster** | 16,013,977 B | 5,277.783 us | 10,856.850 us | 0.49x | 0.46–0.55x | -5564.300 us | 9 pairs; 32/43 ms; 6/4 ops/lane; IQR 1.010/1.093 | `Pcre2Literal` | 1,333.063 us | 1,302.754 us | 8,286.990 us | 0.64x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,628.500 us | 143,060.500 us | 0.13x | 0.13–0.13x | -124327.700 us | 9 pairs; 37/143 ms; 2/1 ops/lane; IQR 1.019/1.007 | `Pcre2Backtracking` | 1,197.543 us | 499.246 us | 8,414.289 us | 2.21x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,890.712 us | 21,400.350 us | 0.23x | 0.23–0.23x | -16495.938 us | 9 pairs; 39/43 ms; 8/2 ops/lane; IQR 1.006/1.015 | `Pcre2Slice` | 2,339.721 us | 10,903.600 us | 18,814.275 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,711.600 us | 130,478.600 us | 0.17x | 0.17–0.17x | -108909.550 us | 9 pairs; 43/130 ms; 2/1 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 1,447.337 us | 2,264.844 us | 10,276.325 us | 2.11x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 134.303 us | 978.166 us | 0.14x | 0.13–0.14x | -843.994 us | 9 pairs; 39/40 ms; 293/41 ops/lane; IQR 1.008/1.012 | `Pcre2Backtracking` | 168.688 us | 321.846 us | 570.061 us | 0.24x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Inconclusive** | 594,930 B | 12,183.233 us | 13,045.933 us | 0.94x | 0.92–0.94x | -836.333 us | 9 pairs; 37/39 ms; 3/3 ops/lane; IQR 1.012/1.005 | `Pcre2Backtracking` | 302.668 us | 14,933.033 us | 15,081.917 us | 0.81x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,768.986 us | 96,261.900 us | 0.03x | 0.03–0.03x | -93492.914 us | 9 pairs; 39/96 ms; 14/1 ops/lane; IQR 1.019/1.004 | `Pcre2Character` | 1,841.508 us | 19,680.375 us | 19,865.650 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.310 us | 2,753.871 us | 0.07x | 0.07–0.07x | -2568.358 us | 9 pairs; 38/39 ms; 206/14 ops/lane; IQR 1.006/1.006 | `Pcre2SingleTokenRepeat` | 187.077 us | 988.979 us | 1,228.312 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Inconclusive** | 594,930 B | 1,590.727 us | 19,697.400 us | 0.08x | 0.08–0.08x | -18131.008 us | 9 pairs; 41/39 ms; 26/2 ops/lane; IQR 1.026/1.016 | `Pcre2Backtracking` | 560.239 us | 10,716.375 us | 10,922.986 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.135 us | 1.080 us | 0.13x | 0.12–0.13x | -0.945 us | 9 pairs; 39/41 ms; 288,153/37,984 ops/lane; IQR 1.010/1.013 | `Pcre2Slice` | 0.582 us | 0.254 us | 0.274 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.507 us | 0.813 us | 0.63x | 0.62–0.64x | -0.300 us | 9 pairs; 41/40 ms; 80,157/49,652 ops/lane; IQR 1.013/1.012 | `Pcre2Literal` | 0.139 us | 0.147 us | 0.171 us | 2.97x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.299 us | 1.161 us | 0.26x | 0.26–0.26x | -0.861 us | 9 pairs; 40/39 ms; 133,024/33,858 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 1.031 us | 0.341 us | 0.369 us | 0.81x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.915 us | 1.112 us | 0.82x | 0.80–0.82x | -0.202 us | 9 pairs; 42/39 ms; 46,398/35,425 ops/lane; IQR 1.014/1.015 | `Pcre2Character` | 0.262 us | 0.423 us | 0.468 us | 1.95x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.153 us | 0.842 us | 0.18x | 0.18–0.18x | -0.688 us | 9 pairs; 41/41 ms; 267,500/48,758 ops/lane; IQR 1.007/1.007 | `Pcre2Slice` | 0.628 us | 0.154 us | 0.200 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.887 us | 1.081 us | 0.82x | 0.81–0.82x | -0.195 us | 9 pairs; 41/40 ms; 46,109/37,209 ops/lane; IQR 1.012/1.016 | `Pcre2Slice` | 2.230 us | 0.303 us | 0.321 us | 2.76x | 0 B | 0 B |
| `simple/foo-dense` | **Native faster** | 18 B | 0.863 us | 0.816 us | 1.06x | 1.05–1.06x | +0.045 us | 9 pairs; 38/40 ms; 44,045/48,687 ops/lane; IQR 1.010/1.014 | `Pcre2Literal` | 3.461 us | 0.175 us | 0.196 us | 4.40x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.961 us | 1.156 us | 0.83x | 0.83–0.83x | -0.196 us | 9 pairs; 37/41 ms; 38,411/35,413 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 5.167 us | 0.395 us | 0.414 us | 2.32x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.141 us | 1.101 us | 1.03x | 1.02–1.04x | +0.036 us | 9 pairs; 39/40 ms; 34,375/36,544 ops/lane; IQR 1.007/1.005 | `Pcre2Character` | 3.536 us | 0.468 us | 0.506 us | 2.25x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.788 us | 0.840 us | 0.94x | 0.93–0.94x | -0.051 us | 9 pairs; 40/41 ms; 50,311/48,571 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 1.934 us | 0.178 us | 0.219 us | 3.60x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.608 us | 1.072 us | 0.57x | 0.56–0.57x | -0.465 us | 9 pairs; 39/38 ms; 64,476/35,566 ops/lane; IQR 1.012/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.522 us | 0.814 us | 0.64x | 0.64–0.64x | -0.294 us | 9 pairs; 42/40 ms; 80,519/49,172 ops/lane; IQR 1.004/1.004 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.661 us | 1.154 us | 0.57x | 0.57–0.58x | -0.494 us | 9 pairs; 40/40 ms; 59,813/34,541 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.895 us | 1.115 us | 0.80x | 0.80–0.81x | -0.219 us | 9 pairs; 39/41 ms; 43,817/36,999 ops/lane; IQR 1.009/1.011 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.485 us | 0.840 us | 0.57x | 0.57–0.58x | -0.359 us | 9 pairs; 40/39 ms; 82,477/46,641 ops/lane; IQR 1.008/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.393 us | — | — | — | — | — | `—` | 25.184 us | 96.919 us | 98.870 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.535 us | — | — | — | — | — | `—` | 1.468 us | 0.359 us | 0.385 us | 9.17x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.114 us | — | — | — | — | — | `—` | 0.886 us | 0.240 us | 0.269 us | 4.15x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.228 us | — | — | — | — | — | `—` | 3.097 us | 0.450 us | 0.482 us | 2.55x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.498 us | — | — | — | — | — | `—` | 0.987 us | 0.529 us | 0.578 us | 2.59x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.337 us | — | — | — | — | — | `—` | 1.415 us | 0.266 us | 0.321 us | 10.40x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.087 us | 0.081 us | 1.06x | 1.06–1.08x | +0.005 us | 9 pairs; 41/39 ms; 466,826/478,800 ops/lane; IQR 1.006/1.010 | `Pcre2Character` | 0 B | 0 B | 2.212 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.012 us | 0.115 us | 0.11x | 0.11–0.11x | -0.103 us | 9 pairs; 39/43 ms; 3,106,217/374,077 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 127.162 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.390 us | 0.104 us | 3.71x | 3.68–3.76x | +0.285 us | 9 pairs; 35/40 ms; 89,769/379,939 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 21.900 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.388 us | 0.115 us | 3.39x | 3.35–3.42x | +0.273 us | 9 pairs; 37/41 ms; 94,196/359,885 ops/lane; IQR 1.010/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 71.394 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.370 us | 0.104 us | 3.55x | 3.54–3.58x | +0.266 us | 9 pairs; 35/42 ms; 93,721/401,102 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 69.612 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.826 us | 0.338 us | 2.45x | 2.38–2.47x | +0.489 us | 9 pairs; 36/39 ms; 43,401/114,900 ops/lane; IQR 1.016/1.008 | `Pcre2Character` | 0 B | 0 B | 2.031 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.633 us | 0.02x | 0.02–0.02x | -0.621 us | 9 pairs; 39/40 ms; 3,275,162/63,930 ops/lane; IQR 1.044/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 21.656 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.491 us | 0.02x | 0.02–0.02x | -0.479 us | 9 pairs; 30/38 ms; 2,575,468/78,160 ops/lane; IQR 1.032/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 21.163 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.454 us | 0.106 us | 4.26x | 4.16–4.33x | +0.346 us | 9 pairs; 38/39 ms; 83,354/370,600 ops/lane; IQR 1.005/1.025 | `Pcre2Backtracking` | 0 B | 0 B | 18.794 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.353 us | 10.366 us | 0.81x | 0.80–0.81x | -1.977 us | 9 pairs; 40/39 ms; 4,798/3,804 ops/lane; IQR 1.014/1.008 | `Pcre2Literal` | 0 B | 0 B | 1.475 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.292 us | 1.020 us | 0.29x | 0.29–0.29x | -0.728 us | 9 pairs; 40/40 ms; 137,069/39,251 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 129.344 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.348 us | 0.454 us | 0.77x | 0.73–0.78x | -0.105 us | 9 pairs; 40/40 ms; 116,018/88,854 ops/lane; IQR 1.053/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 114.456 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Inconclusive** | 31 B | 0.283 us | 0.452 us | 0.63x | 0.62–0.64x | -0.168 us | 9 pairs; 37/37 ms; 131,132/82,961 ops/lane; IQR 1.013/1.026 | `Pcre2Backtracking` | 0 B | 0 B | 142.200 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.298 us | 0.578 us | 0.51x | 0.51–0.52x | -0.279 us | 9 pairs; 37/40 ms; 125,287/69,183 ops/lane; IQR 1.015/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 105.906 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.299 us | 0.358 us | 0.83x | 0.82–0.84x | -0.060 us | 9 pairs; 34/39 ms; 112,718/109,049 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 214.244 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.913 us | 8.585 us | 0.57x | 0.57–0.58x | -3.690 us | 9 pairs; 40/41 ms; 8,064/4,824 ops/lane; IQR 1.010/1.009 | `Pcre2Character` | 0 B | 0 B | 1.456 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.259 us | 0.307 us | 0.84x | 0.84–0.85x | -0.049 us | 9 pairs; 34/40 ms; 132,141/129,371 ops/lane; IQR 1.014/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 91.450 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.266 us | 0.382 us | 0.70x | 0.69–0.71x | -0.114 us | 9 pairs; 37/40 ms; 140,535/105,815 ops/lane; IQR 1.004/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 124.200 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.885 us | — | — | — | — | — | `—` | 0 B | — | 13.556 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.193 us | 0.282 us | 0.68x | 0.68–0.69x | -0.090 us | 9 pairs; 41/38 ms; 211,474/135,131 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.712 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.742 us | — | — | — | — | — | `—` | 0 B | — | 14.838 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.509 us | 0.671 us | 0.76x | 0.75–0.76x | -0.165 us | 9 pairs; 36/40 ms; 70,399/58,913 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 0 B | 0 B | 64.231 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.755 us | 28.106 us | 0.35x | 0.35–0.35x | -18.399 us | 9 pairs; 41/40 ms; 4,182/1,437 ops/lane; IQR 1.010/1.008 | `Pcre2Literal` | 0 B | 0 B | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.935 us | 1.037 us | 0.90x | 0.90–0.91x | -0.105 us | 9 pairs; 40/40 ms; 42,497/38,781 ops/lane; IQR 1.008/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 131.306 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.975 us | 1.162 us | 0.84x | 0.84–0.84x | -0.186 us | 9 pairs; 39/40 ms; 39,977/34,793 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 113.263 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Inconclusive** | 31 B | 1.009 us | 1.035 us | 0.98x | 0.97–0.99x | -0.021 us | 9 pairs; 41/41 ms; 40,534/39,575 ops/lane; IQR 1.012/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 142.131 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.951 us | 1.144 us | 0.83x | 0.82–0.83x | -0.197 us | 9 pairs; 39/42 ms; 41,289/36,351 ops/lane; IQR 1.009/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 102.281 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.940 us | 0.921 us | 1.02x | 1.01–1.03x | +0.019 us | 9 pairs; 40/40 ms; 42,937/43,559 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 216.625 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.664 us | 8.759 us | 0.65x | 0.64–0.65x | -3.094 us | 9 pairs; 39/41 ms; 6,958/4,651 ops/lane; IQR 1.003/1.004 | `Pcre2Character` | 0 B | 0 B | 1.387 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.909 us | 0.876 us | 1.04x | 1.03–1.05x | +0.035 us | 9 pairs; 39/40 ms; 43,414/46,152 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 91.112 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.917 us | 0.947 us | 0.97x | 0.96–0.98x | -0.026 us | 9 pairs; 40/42 ms; 43,930/44,295 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 121.931 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.961 us | — | — | — | — | — | `—` | 0 B | — | 14.213 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.821 us | 0.846 us | 0.97x | 0.97–0.98x | -0.025 us | 9 pairs; 41/41 ms; 49,451/48,652 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.481 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.894 us | — | — | — | — | — | `—` | 0 B | — | 14.794 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.840 us | 1.228 us | 0.68x | 0.68–0.68x | -0.391 us | 9 pairs; 40/41 ms; 47,492/33,185 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 0 B | 0 B | 64.425 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.433 us | 2.219 us | 0.65x | 0.64–0.65x | -0.792 us | 9 pairs; 40/41 ms; 27,650/18,537 ops/lane; IQR 1.011/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.633 us | 1.034 us | 0.61x | 0.61–0.62x | -0.401 us | 9 pairs; 38/40 ms; 60,762/38,419 ops/lane; IQR 1.014/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 131.731 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.670 us | 1.163 us | 0.58x | 0.57–0.58x | -0.493 us | 9 pairs; 40/39 ms; 59,191/33,705 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 114.075 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.606 us | 1.025 us | 0.59x | 0.58–0.59x | -0.419 us | 9 pairs; 39/41 ms; 63,709/39,846 ops/lane; IQR 1.006/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 141.238 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.604 us | 1.139 us | 0.53x | 0.53–0.53x | -0.535 us | 9 pairs; 39/41 ms; 65,083/36,118 ops/lane; IQR 1.008/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 104.200 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.617 us | 0.919 us | 0.67x | 0.67–0.68x | -0.302 us | 9 pairs; 40/40 ms; 64,206/43,776 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 210.450 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.987 us | 2.132 us | 0.93x | 0.93–0.93x | -0.150 us | 9 pairs; 40/40 ms; 20,350/18,899 ops/lane; IQR 1.017/1.010 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.627 us | 0.873 us | 0.72x | 0.71–0.72x | -0.247 us | 9 pairs; 39/42 ms; 62,482/47,582 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 91.500 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.610 us | 0.946 us | 0.65x | 0.64–0.65x | -0.336 us | 9 pairs; 38/40 ms; 62,023/42,631 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 122.162 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.772 us | — | — | — | — | — | `—` | 0 B | — | 13.456 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.489 us | 0.839 us | 0.58x | 0.58–0.58x | -0.351 us | 9 pairs; 39/40 ms; 79,673/47,591 ops/lane; IQR 1.012/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 53.769 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.670 us | — | — | — | — | — | `—` | 0 B | — | 14.088 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.559 us | 1.222 us | 0.46x | 0.45–0.46x | -0.665 us | 9 pairs; 40/40 ms; 71,009/32,967 ops/lane; IQR 1.014/1.005 | `Pcre2Slice` | 0 B | 0 B | 63.188 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.559 us | — | — | — | — | — | `—` | 480 B | — | 0.713 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.136 us | — | — | — | — | — | `—` | 48 B | — | 127.487 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.252 us | — | — | — | — | — | `—` | 48 B | — | 115.669 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.124 us | — | — | — | — | — | `—` | 48 B | — | 138.931 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.131 us | — | — | — | — | — | `—` | 48 B | — | 102.688 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.119 us | — | — | — | — | — | `—` | 48 B | — | 216.662 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.906 us | — | — | — | — | — | `—` | 64 B | — | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.140 us | — | — | — | — | — | `—` | 40 B | — | 93.269 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.140 us | — | — | — | — | — | `—` | 40 B | — | 124.856 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.619 us | — | — | — | — | — | `—` | 1,216 B | — | 13.912 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.088 us | — | — | — | — | — | `—` | 48 B | — | 53.837 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.620 us | — | — | — | — | — | `—` | 928 B | — | 15.381 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.318 us | — | — | — | — | — | `—` | 4,496 B | — | 64.525 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 31.840–251.174 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.156–93.539 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.118–12.472 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.198–49.672 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.030–1.125 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 6.011–46.760 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.066–19.661 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.176–97.593 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.844–70.585 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.179–5.313 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.621–366.029 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.078 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.940–298.567 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.833–5.048 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.056–15.095 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 103.317–819.652 us |

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
