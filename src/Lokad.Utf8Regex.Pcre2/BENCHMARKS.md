<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `C6B692E968146EE48CBA65FD018C56A117EBCB15217C71972CF945EE348AA383`
- Latest managed row measurement: `2026-11-04T07:39:31.3164192+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T07:39:31.3164192+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `77` managed faster, `1` equivalent, `13` native faster, `10` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `baae83e6ef23` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `baae83e6ef23` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.939 us | 2.375 us | 0.42x | 0.35–0.47x | -1.411 us | 9 pairs; 35/51 ms; 36,844/21,597 ops/lane; IQR 1.062/1.180 | `Pcre2Slice` | 546.464 us | 0.283 us | 0.827 us | 1.14x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Managed faster** | 22,528 B | 1.078 us | 10.153 us | 0.11x | 0.10–0.11x | -9.081 us | 9 pairs; 34/28 ms; 31,946/2,715 ops/lane; IQR 1.027/1.092 | `Pcre2Slice` | 0.655 us | 0.097 us | 3.351 us | 0.32x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.235 us | 0.01x | 0.01–0.01x | -1.224 us | 9 pairs; 39/40 ms; 3,630,468/32,148 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0.111 us | 1.175 us | 1.211 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.705 us | 0.159 us | 4.45x | 4.41–4.46x | +0.547 us | 9 pairs; 38/42 ms; 53,959/261,740 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0.049 us | 0.121 us | 0.156 us | 4.52x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.356 us | 0.572 us | 4.12x | 4.03–4.16x | +1.782 us | 9 pairs; 40/40 ms; 17,122/69,835 ops/lane; IQR 1.002/1.017 | `Pcre2Backtracking` | 0.126 us | 0.750 us | 0.782 us | 3.01x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.101 us | 0.305 us | 0.33x | 0.33–0.33x | -0.204 us | 9 pairs; 40/40 ms; 396,593/131,807 ops/lane; IQR 1.001/1.005 | `Pcre2Slice` | 0.118 us | 0.316 us | 0.352 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.360 us | 0.29x | 0.29–0.30x | -0.253 us | 9 pairs; 40/40 ms; 375,549/110,499 ops/lane; IQR 1.007/1.012 | `Pcre2Slice` | 0.121 us | 0.444 us | 0.466 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.281 us | 0.29x | 0.28–0.29x | -0.201 us | 9 pairs; 40/39 ms; 493,867/137,999 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0.094 us | 0.244 us | 0.265 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.077 us | 0.312 us | 0.25x | 0.24–0.25x | -0.235 us | 9 pairs; 38/38 ms; 498,013/121,567 ops/lane; IQR 1.001/1.014 | `Pcre2Slice` | 0.094 us | 0.258 us | 0.281 us | 0.27x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.180 us | 0.079 us | 2.27x | 2.24–2.28x | +0.100 us | 9 pairs; 41/39 ms; 229,217/496,723 ops/lane; IQR 1.012/1.016 | `Pcre2Slice` | 0.093 us | 0.975 us | 1.003 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.035 us | 0.197 us | 0.18x | 0.18–0.18x | -0.162 us | 9 pairs; 41/40 ms; 1,168,378/202,912 ops/lane; IQR 1.004/1.016 | `Pcre2Backtracking` | 0.114 us | 0.304 us | 0.344 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.615 us | 0.10x | 0.10–0.10x | -0.552 us | 9 pairs; 40/40 ms; 649,523/64,575 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0.139 us | 0.234 us | 0.255 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.155 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 38/39 ms; 1,145,677/251,590 ops/lane; IQR 1.018/1.001 | `Pcre2Backtracking` | 0.103 us | 0.175 us | 0.197 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.113 us | 0.17x | 0.17–0.18x | -0.093 us | 9 pairs; 38/42 ms; 1,942,959/374,646 ops/lane; IQR 1.021/1.007 | `Pcre2Backtracking` | 0.064 us | 0.091 us | 0.111 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.109 us | 0.53x | 0.53–0.54x | -0.051 us | 9 pairs; 40/40 ms; 690,887/367,475 ops/lane; IQR 1.003/1.009 | `Pcre2Backtracking` | 0.016 us | 0.088 us | 0.106 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.086 us | 0.282 us | 0.30x | 0.30–0.31x | -0.196 us | 9 pairs; 40/39 ms; 470,517/138,065 ops/lane; IQR 1.003/1.006 | `Pcre2Slice` | 0.102 us | 0.119 us | 0.153 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.114 us | 0.21x | 0.19–0.21x | -0.090 us | 9 pairs; 42/39 ms; 1,753,873/343,110 ops/lane; IQR 1.019/1.031 | `Pcre2Backtracking` | 0.019 us | 0.096 us | 0.115 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.297 us | 0.05x | 0.05–0.06x | -2.178 us | 9 pairs; 40/52 ms; 340,189/22,566 ops/lane; IQR 1.056/1.134 | `Pcre2Literal` | 0.091 us | 0.051 us | 0.638 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 1.842 us | 0.07x | 0.06–0.07x | -1.721 us | 9 pairs; 40/38 ms; 325,141/20,532 ops/lane; IQR 1.003/1.138 | `Pcre2Literal` | 0.149 us | 0.053 us | 0.625 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.154 us | 2.014 us | 0.08x | 0.07–0.08x | -1.860 us | 9 pairs; 39/39 ms; 255,790/19,555 ops/lane; IQR 1.009/1.028 | `Pcre2Literal` | 0.207 us | 0.237 us | 0.823 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.922 us | 0.08x | 0.08–0.08x | -1.770 us | 9 pairs; 39/44 ms; 259,816/22,695 ops/lane; IQR 1.002/1.049 | `Pcre2Literal` | 0.183 us | 0.208 us | 0.766 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.099 us | 0.09x | 0.09–0.09x | -0.090 us | 9 pairs; 39/40 ms; 4,462,215/408,032 ops/lane; IQR 1.007/1.018 | `Pcre2Slice` | 0.507 us | 0.070 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.085 us | 0.79x | 0.78–0.79x | -0.018 us | 9 pairs; 40/42 ms; 591,948/487,879 ops/lane; IQR 1.002/1.008 | `Pcre2Literal` | 0.128 us | 0.051 us | 0.073 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.111 us | 0.13x | 0.12–0.13x | -0.097 us | 9 pairs; 41/39 ms; 2,870,923/350,460 ops/lane; IQR 1.014/1.018 | `Pcre2Backtracking` | 0.241 us | 0.091 us | 0.113 us | 0.13x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.116 us | 0.123 us | 0.95x | 0.94–0.95x | -0.006 us | 9 pairs; 40/40 ms; 344,271/328,590 ops/lane; IQR 1.005/1.011 | `Pcre2Character` | 0.138 us | 0.112 us | 0.144 us | 0.81x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.214 us | 0.35x | 0.35–0.36x | -0.138 us | 9 pairs; 41/38 ms; 535,638/176,925 ops/lane; IQR 1.002/1.008 | `Pcre2Slice` | 0.462 us | 0.076 us | 0.108 us | 0.70x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.786 us | 159.548 us | 0.52x | 0.52–0.52x | -77.137 us | 9 pairs; 40/40 ms; 479/251 ops/lane; IQR 1.002/1.006 | `Pcre2Slice` | 397.335 us | 104.022 us | 104.330 us | 0.79x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.765 us | 284.246 us | 0.13x | 0.13–0.13x | -246.481 us | 9 pairs; 40/39 ms; 1,064/137 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 385.774 us | 220.006 us | 231.734 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.876 us | 210.683 us | 0.05x | 0.05–0.05x | -200.841 us | 9 pairs; 42/41 ms; 4,246/196 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 8.219 us | 225.061 us | 226.920 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.111 us | 206.545 us | 1.17x | 1.16–1.17x | +34.660 us | 9 pairs; 41/41 ms; 170/197 ops/lane; IQR 1.001/1.004 | `Pcre2SingleTokenRepeat` | 8.238 us | 228.718 us | 230.365 us | 1.05x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.546 us | 22.967 us | 0.15x | 0.15–0.16x | -19.410 us | 9 pairs; 40/41 ms; 11,142/1,778 ops/lane; IQR 1.003/1.010 | `Pcre2Literal` | 1.377 us | 4.257 us | 5.970 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.599 us | 81.711 us | 0.12x | 0.12–0.12x | -72.118 us | 9 pairs; 40/40 ms; 4,122/491 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 9.318 us | 89.417 us | 90.889 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,230.940 us | 29,289.500 us | 0.15x | 0.14–0.16x | -25039.850 us | 9 pairs; 42/29 ms; 10/1 ops/lane; IQR 1.013/1.057 | `Pcre2Backtracking` | 5,763.922 us | 11,735.600 us | 18,290.867 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,529.450 us | 55,332.300 us | 0.06x | 0.06–0.06x | -51778.500 us | 9 pairs; 42/55 ms; 12/1 ops/lane; IQR 1.032/1.009 | `Pcre2Backtracking` | 3,917.777 us | 41,401.800 us | 48,184.000 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,938.905 us | 482,176.600 us | 0.00x | 0.00–0.00x | -480217.576 us | 9 pairs; 41/482 ms; 21/1 ops/lane; IQR 1.037/1.002 | `Pcre2Character` | 1,799.703 us | 22,855.433 us | 29,735.250 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,575.600 us | 10,232.333 us | 0.54x | 0.48–0.64x | -4686.667 us | 9 pairs; 33/31 ms; 6/3 ops/lane; IQR 1.007/1.103 | `Pcre2Literal` | 1,506.788 us | 1,393.592 us | 8,494.283 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,796.400 us | 146,230.300 us | 0.13x | 0.13–0.13x | -127256.450 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.016/1.007 | `Pcre2Backtracking` | 1,618.991 us | 507.294 us | 8,521.717 us | 2.21x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,777.225 us | 21,945.900 us | 0.22x | 0.22–0.22x | -17164.513 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.001/1.020 | `Pcre2Slice` | 2,874.928 us | 11,121.240 us | 19,081.800 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,330.300 us | 134,495.200 us | 0.17x | 0.17–0.17x | -112234.400 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 1,917.211 us | 2,313.826 us | 10,450.080 us | 2.14x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 188.378 us | 1,002.974 us | 0.19x | 0.18–0.19x | -815.015 us | 9 pairs; 39/38 ms; 208/38 ops/lane; IQR 1.005/1.019 | `Pcre2Backtracking` | 223.197 us | 331.597 us | 546.641 us | 0.34x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,573.833 us | 13,374.767 us | 0.86x | 0.86–0.87x | -1811.033 us | 9 pairs; 35/40 ms; 3/3 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 320.055 us | 15,304.250 us | 15,499.525 us | 0.75x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,800.429 us | 98,244.400 us | 0.03x | 0.03–0.03x | -95433.979 us | 9 pairs; 39/98 ms; 14/1 ops/lane; IQR 1.005/1.005 | `Pcre2Character` | 1,891.641 us | 19,838.633 us | 20,118.900 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.951 us | 2,818.000 us | 0.07x | 0.07–0.07x | -2634.321 us | 9 pairs; 39/39 ms; 213/14 ops/lane; IQR 1.008/1.007 | `Pcre2SingleTokenRepeat` | 189.435 us | 1,012.022 us | 1,440.548 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,584.672 us | 20,067.800 us | 0.08x | 0.08–0.08x | -18475.660 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 585.475 us | 10,401.040 us | 10,603.980 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.151 us | 1.098 us | 0.14x | 0.14–0.14x | -0.947 us | 9 pairs; 38/40 ms; 249,949/36,259 ops/lane; IQR 1.014/1.001 | `Pcre2Slice` | 0.721 us | 0.258 us | 0.280 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.515 us | 0.861 us | 0.60x | 0.59–0.60x | -0.348 us | 9 pairs; 39/40 ms; 75,225/46,412 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0.144 us | 0.149 us | 0.174 us | 2.96x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.297 us | 1.180 us | 0.25x | 0.25–0.25x | -0.882 us | 9 pairs; 39/40 ms; 131,523/34,089 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 1.079 us | 0.345 us | 0.371 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.877 us | 1.130 us | 0.78x | 0.77–0.78x | -0.253 us | 9 pairs; 40/40 ms; 45,380/35,534 ops/lane; IQR 1.002/1.004 | `Pcre2Character` | 0.277 us | 0.444 us | 0.470 us | 1.87x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.161 us | 0.857 us | 0.19x | 0.18–0.19x | -0.698 us | 9 pairs; 42/40 ms; 260,930/46,433 ops/lane; IQR 1.023/1.004 | `Pcre2Slice` | 0.883 us | 0.157 us | 0.200 us | 0.81x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.905 us | 1.105 us | 0.82x | 0.82–0.82x | -0.201 us | 9 pairs; 40/40 ms; 43,660/36,142 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 4.531 us | 0.305 us | 0.325 us | 2.79x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.795 us | 0.833 us | 0.95x | 0.95–0.96x | -0.038 us | 9 pairs; 40/40 ms; 50,503/48,416 ops/lane; IQR 1.001/1.003 | `Pcre2Literal` | 3.539 us | 0.183 us | 0.204 us | 3.90x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.994 us | 1.244 us | 0.80x | 0.80–0.80x | -0.249 us | 9 pairs; 39/40 ms; 38,930/32,173 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 5.229 us | 0.400 us | 0.423 us | 2.35x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.222 us | 1.136 us | 1.08x | 1.07–1.08x | +0.085 us | 9 pairs; 40/40 ms; 32,718/34,894 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 3.603 us | 0.480 us | 0.520 us | 2.35x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.812 us | 0.861 us | 0.94x | 0.94–0.95x | -0.048 us | 9 pairs; 41/40 ms; 50,031/46,208 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 2.133 us | 0.185 us | 0.227 us | 3.58x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.588 us | 1.104 us | 0.53x | 0.53–0.54x | -0.517 us | 9 pairs; 35/40 ms; 60,312/35,991 ops/lane; IQR 1.010/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.534 us | 0.834 us | 0.64x | 0.64–0.64x | -0.301 us | 9 pairs; 40/40 ms; 74,857/47,884 ops/lane; IQR 1.004/1.001 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.705 us | 1.241 us | 0.57x | 0.57–0.57x | -0.535 us | 9 pairs; 40/40 ms; 56,520/32,255 ops/lane; IQR 1.005/1.000 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.350 us | 1.132 us | 1.19x | 1.19–1.19x | +0.217 us | 9 pairs; 57/40 ms; 42,043/35,448 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.491 us | 0.863 us | 0.57x | 0.57–0.57x | -0.370 us | 9 pairs; 41/40 ms; 82,697/45,832 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 65.310 us | — | — | — | — | — | `—` | 25.733 us | 99.310 us | 98.159 us | 0.67x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.578 us | — | — | — | — | — | `—` | 2.194 us | 0.357 us | 0.385 us | 17.09x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.054 us | — | — | — | — | — | `—` | 0.920 us | 0.245 us | 0.277 us | 3.80x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.208 us | — | — | — | — | — | `—` | 3.126 us | 0.454 us | 0.488 us | 2.48x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.490 us | — | — | — | — | — | `—` | 1.034 us | 0.542 us | 0.593 us | 2.51x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.410 us | — | — | — | — | — | `—` | 1.582 us | 0.272 us | 0.323 us | 13.64x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.088 us | 0.086 us | 1.02x | 1.01–1.03x | +0.002 us | 9 pairs; 40/40 ms; 450,677/470,234 ops/lane; IQR 1.007/1.006 | `Pcre2Character` | 0 B | 0 B | 2.188 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.122 us | 0.11x | 0.10–0.11x | -0.108 us | 9 pairs; 40/39 ms; 3,006,813/322,700 ops/lane; IQR 1.005/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 129.488 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.373 us | 0.103 us | 3.62x | 3.58–3.64x | +0.270 us | 9 pairs; 35/39 ms; 94,705/379,798 ops/lane; IQR 1.005/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 20.988 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.390 us | 0.117 us | 3.31x | 3.26–3.40x | +0.273 us | 9 pairs; 36/39 ms; 92,582/327,924 ops/lane; IQR 1.009/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 71.900 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.395 us | 0.106 us | 3.72x | 3.66–3.75x | +0.289 us | 9 pairs; 35/39 ms; 89,550/368,876 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 69.831 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.576 us | 0.343 us | 1.68x | 1.66–1.69x | +0.233 us | 9 pairs; 38/39 ms; 65,091/112,988 ops/lane; IQR 1.004/1.010 | `Pcre2Character` | 0 B | 0 B | 1.413 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.636 us | 0.02x | 0.02–0.02x | -0.623 us | 9 pairs; 42/39 ms; 3,468,125/61,890 ops/lane; IQR 1.030/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 22.100 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.511 us | 0.02x | 0.02–0.03x | -0.498 us | 9 pairs; 41/39 ms; 3,498,674/76,539 ops/lane; IQR 1.043/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 22.637 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.470 us | 0.112 us | 4.20x | 4.17–4.33x | +0.358 us | 9 pairs; 37/40 ms; 78,370/361,461 ops/lane; IQR 1.019/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 18.337 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.378 us | 10.612 us | 0.79x | 0.78–0.79x | -2.250 us | 9 pairs; 40/41 ms; 4,796/3,863 ops/lane; IQR 1.007/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.302 us | 1.048 us | 0.29x | 0.29–0.29x | -0.747 us | 9 pairs; 40/40 ms; 131,198/37,795 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 133.713 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.317 us | 0.475 us | 0.67x | 0.66–0.67x | -0.158 us | 9 pairs; 39/40 ms; 122,372/84,981 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 114.781 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.288 us | 0.481 us | 0.60x | 0.59–0.60x | -0.193 us | 9 pairs; 33/41 ms; 115,935/84,628 ops/lane; IQR 1.004/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 141.444 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.293 us | 0.593 us | 0.50x | 0.49–0.50x | -0.299 us | 9 pairs; 32/39 ms; 110,362/66,270 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 104.625 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.291 us | 0.363 us | 0.80x | 0.79–0.81x | -0.073 us | 9 pairs; 34/41 ms; 116,740/112,564 ops/lane; IQR 1.005/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 215.075 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.867 us | 8.831 us | 0.55x | 0.55–0.55x | -3.952 us | 9 pairs; 40/40 ms; 8,291/4,530 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.255 us | 0.308 us | 0.83x | 0.82–0.83x | -0.054 us | 9 pairs; 36/39 ms; 139,945/127,327 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 92.013 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.273 us | 0.403 us | 0.68x | 0.67–0.68x | -0.130 us | 9 pairs; 37/39 ms; 135,360/97,538 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 121.969 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.848 us | — | — | — | — | — | `—` | 0 B | — | 13.919 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.187 us | 0.296 us | 0.63x | 0.63–0.63x | -0.108 us | 9 pairs; 40/39 ms; 214,762/133,312 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 53.056 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.741 us | — | — | — | — | — | `—` | 0 B | — | 16.188 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.584 us | 0.676 us | 0.86x | 0.86–0.87x | -0.094 us | 9 pairs; 37/40 ms; 63,442/59,440 ops/lane; IQR 1.015/1.012 | `Pcre2Slice` | 0 B | 0 B | 64.756 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.845 us | 29.000 us | 0.34x | 0.34–0.34x | -19.149 us | 9 pairs; 40/40 ms; 4,052/1,377 ops/lane; IQR 1.005/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.980 us | 1.058 us | 0.93x | 0.92–0.93x | -0.078 us | 9 pairs; 41/39 ms; 41,449/37,287 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 130.963 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.011 us | 1.192 us | 0.85x | 0.84–0.85x | -0.183 us | 9 pairs; 40/40 ms; 39,856/33,350 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 114.444 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.948 us | 1.056 us | 0.90x | 0.89–0.90x | -0.108 us | 9 pairs; 41/40 ms; 42,769/37,833 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 142.575 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.993 us | 1.179 us | 0.84x | 0.83–0.85x | -0.184 us | 9 pairs; 41/40 ms; 41,311/34,189 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 103.131 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.948 us | 0.959 us | 0.99x | 0.99–0.99x | -0.011 us | 9 pairs; 40/40 ms; 42,386/41,637 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 215.131 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.560 us | 8.974 us | 0.62x | 0.62–0.62x | -3.416 us | 9 pairs; 40/40 ms; 7,229/4,465 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.950 us | 0.893 us | 1.06x | 1.06–1.07x | +0.057 us | 9 pairs; 41/39 ms; 43,667/44,026 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 90.694 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Native faster** | 26 B | 1.000 us | 0.967 us | 1.03x | 1.03–1.04x | +0.033 us | 9 pairs; 43/40 ms; 43,234/41,104 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 124.456 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.101 us | — | — | — | — | — | `—` | 0 B | — | 13.625 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.836 us | 0.859 us | 0.97x | 0.97–0.98x | -0.022 us | 9 pairs; 40/40 ms; 47,959/46,545 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.925 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.957 us | — | — | — | — | — | `—` | 0 B | — | 13.581 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.856 us | 1.250 us | 0.68x | 0.68–0.69x | -0.395 us | 9 pairs; 39/40 ms; 45,818/32,044 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 0 B | 0 B | 64.987 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.391 us | 2.300 us | 0.60x | 0.60–0.61x | -0.911 us | 9 pairs; 40/40 ms; 28,807/17,289 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.725 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.639 us | 1.058 us | 0.60x | 0.60–0.61x | -0.419 us | 9 pairs; 40/40 ms; 62,577/38,033 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 128.694 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.680 us | 1.194 us | 0.57x | 0.57–0.57x | -0.513 us | 9 pairs; 40/40 ms; 58,489/33,201 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 113.900 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.623 us | 1.036 us | 0.60x | 0.60–0.60x | -0.413 us | 9 pairs; 40/40 ms; 64,110/38,731 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 140.994 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.629 us | 1.173 us | 0.54x | 0.53–0.54x | -0.544 us | 9 pairs; 38/40 ms; 60,173/34,480 ops/lane; IQR 1.003/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 103.550 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.626 us | 0.957 us | 0.65x | 0.65–0.66x | -0.331 us | 9 pairs; 40/40 ms; 64,395/41,666 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 214.550 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.839 us | 2.208 us | 0.83x | 0.83–0.83x | -0.370 us | 9 pairs; 41/39 ms; 22,040/17,846 ops/lane; IQR 1.003/1.001 | `Pcre2Character` | 0 B | 0 B | 1.387 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.607 us | 0.891 us | 0.68x | 0.68–0.68x | -0.283 us | 9 pairs; 37/40 ms; 61,485/44,801 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 93.812 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.640 us | 0.969 us | 0.66x | 0.65–0.66x | -0.331 us | 9 pairs; 37/41 ms; 58,196/41,902 ops/lane; IQR 1.007/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 122.463 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.756 us | — | — | — | — | — | `—` | 0 B | — | 13.925 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.506 us | 0.858 us | 0.59x | 0.59–0.59x | -0.354 us | 9 pairs; 38/40 ms; 75,834/46,286 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.506 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.700 us | — | — | — | — | — | `—` | 0 B | — | 14.094 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.585 us | 1.260 us | 0.46x | 0.46–0.47x | -0.675 us | 9 pairs; 40/40 ms; 67,843/31,936 ops/lane; IQR 1.007/1.002 | `Pcre2Slice` | 0 B | 0 B | 63.944 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.729 us | — | — | — | — | — | `—` | 480 B | — | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.125 us | — | — | — | — | — | `—` | 48 B | — | 128.856 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.242 us | — | — | — | — | — | `—` | 48 B | — | 113.619 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.104 us | — | — | — | — | — | `—` | 48 B | — | 142.588 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.120 us | — | — | — | — | — | `—` | 48 B | — | 101.294 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.107 us | — | — | — | — | — | `—` | 48 B | — | 217.381 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.908 us | — | — | — | — | — | `—` | 64 B | — | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.133 us | — | — | — | — | — | `—` | 40 B | — | 93.956 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.137 us | — | — | — | — | — | `—` | 40 B | — | 121.138 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.060 us | — | — | — | — | — | `—` | 1,216 B | — | 13.606 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.060 us | — | — | — | — | — | `—` | 48 B | — | 52.731 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.612 us | — | — | — | — | — | `—` | 928 B | — | 13.512 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 6.045 us | — | — | — | — | — | `—` | 28,928 B | — | 64.088 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.887–223.968 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.479–95.463 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.171–12.686 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.275–49.745 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.032–1.472 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.751–45.046 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.765–22.654 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.305–99.985 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.169–73.070 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.194–5.601 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.564–379.239 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.107–301.193 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.950–5.553 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.189–15.657 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 178.920–1,359.900 us |

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
