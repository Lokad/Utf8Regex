<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `1B62E9C36BB155958C4EC5D00351B45010D4077C9132FAB674842D5D097E59FF`
- Latest managed row measurement: `2026-11-07T06:34:28.4134405+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-07T06:34:28.4134405+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `74` managed faster, `1` equivalent, `12` native faster, `14` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `6f855d8ec62e` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `6f855d8ec62e` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.597 us | 2.523 us | 0.24x | 0.23–0.29x | -1.925 us | 9 pairs; 35/44 ms; 58,525/17,279 ops/lane; IQR 1.025/1.186 | `Pcre2Slice` | 571.226 us | 0.280 us | 0.823 us | 0.73x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.707 us | 12.758 us | 0.05x | 0.05–0.06x | -12.058 us | 9 pairs; 34/62 ms; 48,179/4,832 ops/lane; IQR 1.016/1.190 | `Pcre2Slice` | 0.528 us | 0.097 us | 3.585 us | 0.20x | 64 B | 0 B |
| `common/backtracking` | **Inconclusive** | 49 B | 0.017 us | 1.226 us | 0.01x | 0.01–0.01x | -1.210 us | 9 pairs; 61/41 ms; 3,655,273/33,221 ops/lane; IQR 1.290/1.004 | `Pcre2Slice` | 0.114 us | 1.103 us | 1.126 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.682 us | 0.155 us | 4.41x | 4.38–4.45x | +0.527 us | 9 pairs; 37/42 ms; 53,657/268,766 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0.046 us | 0.121 us | 0.157 us | 4.34x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.309 us | 0.556 us | 4.15x | 4.11–4.18x | +1.754 us | 9 pairs; 38/40 ms; 16,442/71,393 ops/lane; IQR 1.020/1.009 | `Pcre2Backtracking` | 0.117 us | 0.725 us | 0.760 us | 3.04x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.293 us | 0.34x | 0.33–0.34x | -0.195 us | 9 pairs; 39/39 ms; 392,015/133,587 ops/lane; IQR 1.010/1.005 | `Pcre2Slice` | 0.125 us | 0.317 us | 0.342 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.104 us | 0.343 us | 0.30x | 0.30–0.31x | -0.239 us | 9 pairs; 40/41 ms; 388,661/118,903 ops/lane; IQR 1.006/1.010 | `Pcre2Slice` | 0.131 us | 0.434 us | 0.464 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.077 us | 0.284 us | 0.27x | 0.27–0.27x | -0.206 us | 9 pairs; 39/41 ms; 502,688/142,896 ops/lane; IQR 1.008/1.013 | `Pcre2Slice` | 0.100 us | 0.232 us | 0.257 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.086 us | 0.305 us | 0.28x | 0.26–0.29x | -0.219 us | 9 pairs; 43/37 ms; 498,638/122,214 ops/lane; IQR 1.041/1.019 | `Pcre2Slice` | 0.099 us | 0.245 us | 0.272 us | 0.32x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.168 us | 0.076 us | 2.21x | 2.21–2.22x | +0.092 us | 9 pairs; 40/39 ms; 236,118/520,945 ops/lane; IQR 1.004/1.007 | `Pcre2Slice` | 0.099 us | 0.954 us | 0.983 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.193 us | 0.17x | 0.17–0.17x | -0.160 us | 9 pairs; 40/38 ms; 1,216,875/197,559 ops/lane; IQR 1.003/1.017 | `Pcre2Backtracking` | 0.121 us | 0.298 us | 0.337 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.597 us | 0.10x | 0.10–0.10x | -0.537 us | 9 pairs; 40/42 ms; 667,363/70,014 ops/lane; IQR 1.008/1.013 | `Pcre2Backtracking` | 0.145 us | 0.227 us | 0.250 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.155 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 38/40 ms; 1,142,197/257,257 ops/lane; IQR 1.023/1.012 | `Pcre2Backtracking` | 0.106 us | 0.182 us | 0.202 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.110 us | 0.18x | 0.17–0.18x | -0.090 us | 9 pairs; 38/40 ms; 1,944,472/368,887 ops/lane; IQR 1.022/1.016 | `Pcre2Backtracking` | 0.061 us | 0.090 us | 0.109 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.115 us | 0.50x | 0.48–0.50x | -0.058 us | 9 pairs; 41/40 ms; 710,357/344,522 ops/lane; IQR 1.004/1.012 | `Pcre2Backtracking` | 0.016 us | 0.085 us | 0.101 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Inconclusive** | 67 B | 0.083 us | 0.265 us | 0.31x | 0.31–0.32x | -0.181 us | 9 pairs; 40/41 ms; 479,743/155,341 ops/lane; IQR 1.006/1.024 | `Pcre2Slice` | 0.106 us | 0.118 us | 0.150 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.112 us | 0.22x | 0.20–0.22x | -0.088 us | 9 pairs; 44/40 ms; 1,800,563/355,346 ops/lane; IQR 1.058/1.011 | `Pcre2Backtracking` | 0.019 us | 0.090 us | 0.105 us | 0.23x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.116 us | 2.040 us | 0.06x | 0.05–0.06x | -1.926 us | 9 pairs; 41/48 ms; 352,877/23,325 ops/lane; IQR 1.009/1.149 | `Pcre2Literal` | 0.085 us | 0.050 us | 0.568 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.118 us | 1.917 us | 0.06x | 0.05–0.06x | -1.798 us | 9 pairs; 39/49 ms; 334,075/25,423 ops/lane; IQR 1.010/1.199 | `Pcre2Literal` | 0.142 us | 0.053 us | 0.600 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.152 us | 2.010 us | 0.08x | 0.06–0.08x | -1.858 us | 9 pairs; 39/37 ms; 256,415/18,416 ops/lane; IQR 1.006/1.254 | `Pcre2Literal` | 0.199 us | 0.229 us | 0.760 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.147 us | 1.930 us | 0.08x | 0.07–0.08x | -1.783 us | 9 pairs; 39/42 ms; 266,052/21,549 ops/lane; IQR 1.009/1.104 | `Pcre2Literal` | 0.168 us | 0.201 us | 0.713 us | 0.21x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.009 us | 0.095 us | 0.09x | 0.09–0.16x | -0.085 us | 9 pairs; 38/39 ms; 4,380,932/413,568 ops/lane; IQR 1.691/1.025 | `Pcre2Slice` | 0.250 us | 0.069 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.076 us | 0.086 us | 0.88x | 0.86–0.89x | -0.011 us | 9 pairs; 45/41 ms; 600,783/478,900 ops/lane; IQR 1.005/1.018 | `Pcre2Literal` | 0.122 us | 0.050 us | 0.075 us | 1.01x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.106 us | 0.12x | 0.12–0.13x | -0.093 us | 9 pairs; 40/39 ms; 2,992,625/366,981 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 0.277 us | 0.089 us | 0.112 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.111 us | 0.120 us | 0.93x | 0.92–0.94x | -0.008 us | 9 pairs; 36/38 ms; 326,654/315,762 ops/lane; IQR 1.006/1.016 | `Pcre2Character` | 0.130 us | 0.111 us | 0.143 us | 0.78x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.073 us | 0.199 us | 0.37x | 0.36–0.37x | -0.126 us | 9 pairs; 40/42 ms; 541,825/211,637 ops/lane; IQR 1.009/1.030 | `Pcre2Slice` | 0.429 us | 0.074 us | 0.107 us | 0.68x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 81.117 us | 158.793 us | 0.51x | 0.51–0.51x | -77.496 us | 9 pairs; 39/41 ms; 484/257 ops/lane; IQR 1.007/1.008 | `Pcre2Slice` | 333.865 us | 99.541 us | 100.521 us | 0.81x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 38.081 us | 273.010 us | 0.14x | 0.14–0.14x | -234.970 us | 9 pairs; 41/40 ms; 1,066/146 ops/lane; IQR 1.011/1.007 | `Pcre2Character` | 268.835 us | 216.023 us | 226.769 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.148 us | 205.287 us | 0.04x | 0.04–0.04x | -196.138 us | 9 pairs; 39/38 ms; 4,263/187 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 8.027 us | 216.910 us | 220.124 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 234.420 us | 211.325 us | 1.11x | 1.10–1.11x | +22.894 us | 9 pairs; 39/40 ms; 166/189 ops/lane; IQR 1.004/1.006 | `Pcre2SingleTokenRepeat` | 7.989 us | 231.114 us | 231.493 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.472 us | 22.217 us | 0.16x | 0.15–0.16x | -18.734 us | 9 pairs; 39/39 ms; 11,251/1,760 ops/lane; IQR 1.008/1.013 | `Pcre2Literal` | 1.364 us | 4.196 us | 5.728 us | 0.61x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.325 us | 78.560 us | 0.12x | 0.12–0.12x | -69.231 us | 9 pairs; 39/41 ms; 4,207/520 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 9.248 us | 89.861 us | 91.017 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,291.475 us | 27,948.100 us | 0.15x | 0.15–0.16x | -23665.363 us | 9 pairs; 34/28 ms; 8/1 ops/lane; IQR 1.009/1.041 | `Pcre2Backtracking` | 5,843.629 us | 11,449.200 us | 18,242.240 us | 0.24x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,227.100 us | 53,848.100 us | 0.06x | 0.06–0.06x | -50552.125 us | 9 pairs; 39/54 ms; 12/1 ops/lane; IQR 1.008/1.015 | `Pcre2Backtracking` | 3,843.735 us | 39,873.750 us | 46,492.450 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,958.248 us | 462,566.200 us | 0.00x | 0.00–0.00x | -460656.424 us | 9 pairs; 41/463 ms; 21/1 ops/lane; IQR 1.033/1.001 | `Pcre2Character` | 1,768.174 us | 22,847.775 us | 29,382.567 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,436.333 us | 10,802.575 us | 0.50x | 0.45–0.54x | -5348.108 us | 9 pairs; 33/43 ms; 6/4 ops/lane; IQR 1.006/1.130 | `Pcre2Literal` | 1,354.538 us | 1,312.597 us | 8,353.810 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,402.300 us | 140,707.000 us | 0.13x | 0.13–0.13x | -122304.700 us | 9 pairs; 37/141 ms; 2/1 ops/lane; IQR 1.024/1.014 | `Pcre2Backtracking` | 1,212.214 us | 523.120 us | 8,481.489 us | 2.17x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,795.250 us | 21,423.350 us | 0.22x | 0.22–0.23x | -16628.100 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.010/1.027 | `Pcre2Slice` | 2,420.991 us | 10,695.450 us | 18,732.380 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,813.700 us | 129,883.400 us | 0.17x | 0.17–0.17x | -108098.650 us | 9 pairs; 44/130 ms; 2/1 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 1,493.900 us | 2,281.250 us | 10,427.425 us | 2.09x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 134.367 us | 1,002.103 us | 0.13x | 0.13–0.14x | -867.735 us | 9 pairs; 40/39 ms; 300/39 ops/lane; IQR 1.010/1.012 | `Pcre2Backtracking` | 165.902 us | 321.102 us | 561.928 us | 0.24x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,368.550 us | 13,199.700 us | 0.87x | 0.86–0.88x | -1709.792 us | 9 pairs; 45/40 ms; 4/3 ops/lane; IQR 1.015/1.019 | `Pcre2Backtracking` | 309.047 us | 14,931.900 us | 15,282.540 us | 0.74x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Inconclusive** | 594,930 B | 2,749.929 us | 97,121.800 us | 0.03x | 0.03–0.03x | -94359.993 us | 9 pairs; 38/97 ms; 14/1 ops/lane; IQR 1.011/1.007 | `Pcre2Character` | 1,984.982 us | 19,770.200 us | 19,913.025 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 182.494 us | 2,724.486 us | 0.07x | 0.07–0.07x | -2543.172 us | 9 pairs; 40/38 ms; 217/14 ops/lane; IQR 1.016/1.018 | `Pcre2SingleTokenRepeat` | 187.801 us | 1,009.821 us | 1,251.871 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,544.031 us | 19,804.700 us | 0.08x | 0.08–0.08x | -18260.669 us | 9 pairs; 40/40 ms; 26/2 ops/lane; IQR 1.009/1.016 | `Pcre2Backtracking` | 577.473 us | 10,573.212 us | 10,923.971 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.141 us | 1.063 us | 0.13x | 0.13–0.13x | -0.922 us | 9 pairs; 41/39 ms; 291,724/36,896 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0.656 us | 0.260 us | 0.282 us | 0.50x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.488 us | 0.816 us | 0.60x | 0.60–0.62x | -0.325 us | 9 pairs; 40/40 ms; 81,407/48,435 ops/lane; IQR 1.012/1.010 | `Pcre2Literal` | 0.146 us | 0.146 us | 0.170 us | 2.87x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.292 us | 1.150 us | 0.25x | 0.25–0.26x | -0.859 us | 9 pairs; 39/40 ms; 134,446/34,900 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 1.043 us | 0.344 us | 0.374 us | 0.78x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.849 us | 1.098 us | 0.77x | 0.77–0.78x | -0.249 us | 9 pairs; 39/41 ms; 45,563/37,432 ops/lane; IQR 1.007/1.006 | `Pcre2Character` | 0.261 us | 0.425 us | 0.467 us | 1.82x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.150 us | 0.846 us | 0.18x | 0.18–0.18x | -0.695 us | 9 pairs; 42/40 ms; 277,426/47,209 ops/lane; IQR 1.008/1.003 | `Pcre2Slice` | 0.793 us | 0.155 us | 0.200 us | 0.75x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.929 us | 1.080 us | 0.86x | 0.86–0.86x | -0.153 us | 9 pairs; 39/39 ms; 42,040/36,467 ops/lane; IQR 1.014/1.020 | `Pcre2Slice` | 2.297 us | 0.304 us | 0.325 us | 2.86x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.770 us | 0.814 us | 0.95x | 0.94–0.95x | -0.044 us | 9 pairs; 41/40 ms; 53,324/49,628 ops/lane; IQR 1.006/1.008 | `Pcre2Literal` | 3.568 us | 0.180 us | 0.202 us | 3.81x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.981 us | 1.163 us | 0.84x | 0.83–0.85x | -0.187 us | 9 pairs; 43/40 ms; 44,073/33,987 ops/lane; IQR 1.014/1.017 | `Pcre2Backtracking` | 5.174 us | 0.391 us | 0.418 us | 2.35x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.181 us | 1.105 us | 1.07x | 1.07–1.08x | +0.079 us | 9 pairs; 39/39 ms; 32,905/35,408 ops/lane; IQR 1.007/1.010 | `Pcre2Character` | 3.596 us | 0.460 us | 0.500 us | 2.36x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.782 us | 0.842 us | 0.93x | 0.92–0.93x | -0.060 us | 9 pairs; 41/40 ms; 51,993/47,516 ops/lane; IQR 1.011/1.012 | `Pcre2Slice` | 2.016 us | 0.176 us | 0.216 us | 3.62x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.593 us | 1.087 us | 0.54x | 0.54–0.55x | -0.498 us | 9 pairs; 40/40 ms; 67,040/36,604 ops/lane; IQR 1.008/1.010 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.484 us | 0.816 us | 0.59x | 0.59–0.60x | -0.332 us | 9 pairs; 39/40 ms; 81,135/49,524 ops/lane; IQR 1.012/1.008 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.643 us | 1.163 us | 0.55x | 0.55–0.55x | -0.521 us | 9 pairs; 40/39 ms; 61,603/33,924 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.908 us | 1.112 us | 0.82x | 0.81–0.82x | -0.202 us | 9 pairs; 41/41 ms; 44,703/36,627 ops/lane; IQR 1.008/1.012 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.472 us | 0.840 us | 0.56x | 0.56–0.56x | -0.367 us | 9 pairs; 41/40 ms; 85,902/47,621 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 59.186 us | — | — | — | — | — | `—` | 25.080 us | 95.097 us | 96.291 us | 0.61x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.603 us | — | — | — | — | — | `—` | 1.420 us | 0.355 us | 0.377 us | 9.55x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.043 us | — | — | — | — | — | `—` | 0.932 us | 0.235 us | 0.264 us | 3.95x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.207 us | — | — | — | — | — | `—` | 3.072 us | 0.445 us | 0.476 us | 2.54x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.453 us | — | — | — | — | — | `—` | 1.052 us | 0.529 us | 0.570 us | 2.55x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.649 us | — | — | — | — | — | `—` | 1.453 us | 0.262 us | 0.306 us | 11.92x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.091 us | 0.082 us | 1.11x | 1.11–1.18x | +0.009 us | 9 pairs; 41/39 ms; 447,998/483,875 ops/lane; IQR 1.078/1.008 | `Pcre2Character` | 0 B | 0 B | 2.200 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.012 us | 0.116 us | 0.11x | 0.11–0.11x | -0.103 us | 9 pairs; 39/39 ms; 3,145,099/339,181 ops/lane; IQR 1.014/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 128.719 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.385 us | 0.103 us | 3.76x | 3.72–3.79x | +0.284 us | 9 pairs; 36/38 ms; 94,513/371,924 ops/lane; IQR 1.017/1.025 | `Pcre2Backtracking` | 0 B | 0 B | 20.769 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.385 us | 0.113 us | 3.45x | 3.39–3.46x | +0.275 us | 9 pairs; 34/41 ms; 89,526/361,125 ops/lane; IQR 1.014/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 70.781 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.387 us | 0.104 us | 3.74x | 3.73–3.82x | +0.284 us | 9 pairs; 36/40 ms; 92,515/384,962 ops/lane; IQR 1.036/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 70.650 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.654 us | 0.335 us | 1.94x | 1.93–1.96x | +0.316 us | 9 pairs; 39/40 ms; 59,755/117,984 ops/lane; IQR 1.009/1.015 | `Pcre2Character` | 0 B | 0 B | 1.337 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.637 us | 0.02x | 0.02–0.02x | -0.625 us | 9 pairs; 43/40 ms; 3,593,639/62,926 ops/lane; IQR 1.047/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 22.431 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.494 us | 0.02x | 0.02–0.02x | -0.482 us | 9 pairs; 43/39 ms; 3,643,588/79,871 ops/lane; IQR 1.027/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 21.850 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.460 us | 0.105 us | 4.37x | 4.30–4.41x | +0.354 us | 9 pairs; 37/38 ms; 80,614/364,175 ops/lane; IQR 1.004/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 17.894 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.212 us | 10.240 us | 0.80x | 0.80–0.81x | -2.024 us | 9 pairs; 40/41 ms; 4,848/4,013 ops/lane; IQR 1.006/1.008 | `Pcre2Literal` | 0 B | 0 B | 0.781 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.295 us | 1.024 us | 0.29x | 0.29–0.29x | -0.728 us | 9 pairs; 40/40 ms; 134,478/39,283 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 130.900 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.304 us | 0.464 us | 0.65x | 0.65–0.66x | -0.160 us | 9 pairs; 36/41 ms; 119,208/87,430 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 115.369 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.287 us | 0.440 us | 0.65x | 0.61–0.66x | -0.153 us | 9 pairs; 37/38 ms; 129,474/85,211 ops/lane; IQR 1.019/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 138.194 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.341 us | 0.574 us | 0.59x | 0.59–0.60x | -0.235 us | 9 pairs; 38/39 ms; 111,471/68,329 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 104.050 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.292 us | 0.396 us | 0.74x | 0.73–0.75x | -0.103 us | 9 pairs; 34/40 ms; 115,527/101,098 ops/lane; IQR 1.005/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 213.575 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.795 us | 8.679 us | 0.56x | 0.55–0.56x | -3.857 us | 9 pairs; 41/41 ms; 8,585/4,696 ops/lane; IQR 1.011/1.008 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.284 us | 0.301 us | 0.95x | 0.94–0.95x | -0.016 us | 9 pairs; 42/40 ms; 148,561/131,842 ops/lane; IQR 1.008/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 90.706 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.266 us | 0.400 us | 0.67x | 0.66–0.67x | -0.133 us | 9 pairs; 37/41 ms; 138,915/103,174 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 119.862 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.818 us | — | — | — | — | — | `—` | 0 B | — | 14.537 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.190 us | 0.292 us | 0.65x | 0.65–0.66x | -0.102 us | 9 pairs; 33/42 ms; 173,133/142,753 ops/lane; IQR 1.022/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 53.319 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.718 us | — | — | — | — | — | `—` | 0 B | — | 13.525 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.565 us | 0.669 us | 0.84x | 0.84–0.84x | -0.107 us | 9 pairs; 36/40 ms; 63,161/59,066 ops/lane; IQR 1.011/1.010 | `Pcre2Slice` | 0 B | 0 B | 64.888 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.779 us | 28.444 us | 0.34x | 0.34–0.35x | -18.665 us | 9 pairs; 40/41 ms; 4,098/1,426 ops/lane; IQR 1.005/1.007 | `Pcre2Literal` | 0 B | 0 B | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.881 us | 1.061 us | 0.83x | 0.83–0.84x | -0.175 us | 9 pairs; 39/43 ms; 43,858/40,182 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 132.219 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.036 us | 1.173 us | 0.89x | 0.88–0.89x | -0.131 us | 9 pairs; 40/40 ms; 38,383/34,223 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 116.100 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.939 us | 1.000 us | 0.94x | 0.94–0.95x | -0.060 us | 9 pairs; 40/40 ms; 42,806/40,470 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 141.006 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.925 us | 1.154 us | 0.80x | 0.80–0.80x | -0.232 us | 9 pairs; 39/41 ms; 42,049/35,215 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 101.713 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.924 us | 0.934 us | 0.99x | 0.98–0.99x | -0.011 us | 9 pairs; 39/39 ms; 42,185/41,733 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 215.844 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.480 us | 8.625 us | 0.64x | 0.63–0.64x | -3.113 us | 9 pairs; 40/39 ms; 7,225/4,507 ops/lane; IQR 1.008/1.006 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.907 us | 0.868 us | 1.05x | 1.04–1.08x | +0.041 us | 9 pairs; 39/39 ms; 43,236/45,173 ops/lane; IQR 1.031/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 91.775 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.976 us | 0.956 us | 1.02x | 1.01–1.02x | +0.019 us | 9 pairs; 40/41 ms; 41,037/42,980 ops/lane; IQR 1.012/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 122.612 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.960 us | — | — | — | — | — | `—` | 0 B | — | 14.050 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.818 us | 0.848 us | 0.96x | 0.96–0.97x | -0.032 us | 9 pairs; 40/41 ms; 48,692/48,808 ops/lane; IQR 1.009/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 53.375 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.877 us | — | — | — | — | — | `—` | 0 B | — | 14.200 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.808 us | 1.305 us | 0.62x | 0.61–0.62x | -0.503 us | 9 pairs; 40/39 ms; 49,909/30,247 ops/lane; IQR 1.010/1.010 | `Pcre2Slice` | 0 B | 0 B | 64.006 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.416 us | 2.225 us | 0.64x | 0.63–0.64x | -0.809 us | 9 pairs; 39/39 ms; 27,552/17,344 ops/lane; IQR 1.004/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.637 us | 1.064 us | 0.60x | 0.60–0.60x | -0.425 us | 9 pairs; 41/41 ms; 63,625/38,516 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 131.356 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.654 us | 1.168 us | 0.56x | 0.56–0.57x | -0.513 us | 9 pairs; 36/39 ms; 55,461/33,275 ops/lane; IQR 1.013/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 113.938 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.628 us | 0.996 us | 0.63x | 0.63–0.63x | -0.370 us | 9 pairs; 40/40 ms; 63,878/40,335 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 142.213 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.623 us | 1.143 us | 0.54x | 0.54–0.55x | -0.521 us | 9 pairs; 42/41 ms; 67,076/35,531 ops/lane; IQR 1.010/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 100.513 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.624 us | 0.936 us | 0.67x | 0.66–0.67x | -0.313 us | 9 pairs; 38/41 ms; 61,406/43,697 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 212.419 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.888 us | 2.147 us | 0.88x | 0.88–0.88x | -0.256 us | 9 pairs; 39/41 ms; 20,838/19,012 ops/lane; IQR 1.013/1.015 | `Pcre2Character` | 0 B | 0 B | 1.381 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.603 us | 0.867 us | 0.69x | 0.69–0.70x | -0.265 us | 9 pairs; 39/41 ms; 64,787/47,696 ops/lane; IQR 1.011/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 90.475 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.627 us | 0.960 us | 0.65x | 0.65–0.66x | -0.331 us | 9 pairs; 42/40 ms; 66,947/42,123 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 120.119 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.687 us | — | — | — | — | — | `—` | 0 B | — | 13.450 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.489 us | 0.850 us | 0.58x | 0.57–0.58x | -0.364 us | 9 pairs; 41/40 ms; 83,598/47,497 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 52.825 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.589 us | — | — | — | — | — | `—` | 0 B | — | 13.262 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.492 us | 1.230 us | 0.40x | 0.40–0.40x | -0.738 us | 9 pairs; 39/40 ms; 79,828/32,532 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | 0 B | 0 B | 64.006 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 16.751 us | — | — | — | — | — | `—` | 480 B | — | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.118 us | — | — | — | — | — | `—` | 48 B | — | 124.987 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.211 us | — | — | — | — | — | `—` | 48 B | — | 113.062 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.106 us | — | — | — | — | — | `—` | 48 B | — | 143.162 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.111 us | — | — | — | — | — | `—` | 48 B | — | 101.806 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.089 us | — | — | — | — | — | `—` | 48 B | — | 216.488 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.723 us | — | — | — | — | — | `—` | 64 B | — | 1.319 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.109 us | — | — | — | — | — | `—` | 40 B | — | 92.094 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.121 us | — | — | — | — | — | `—` | 40 B | — | 123.469 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.579 us | — | — | — | — | — | `—` | 1,216 B | — | 12.950 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.059 us | — | — | — | — | — | `—` | 48 B | — | 53.763 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.411 us | — | — | — | — | — | `—` | 928 B | — | 13.406 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.677 us | — | — | — | — | — | `—` | 4,496 B | — | 64.256 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 31.334–249.699 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.337–94.611 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.842–14.227 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.988–48.798 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.032–1.103 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.299–41.750 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.007–23.487 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.070–97.874 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.954–70.288 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.213–5.617 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.892–367.334 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.066–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.504–306.193 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.814–4.903 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.075–15.048 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 100.839–806.566 us |

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
