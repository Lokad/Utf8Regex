<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `B44935B0C91ABD448D1EEB0A49592C4B432D843F32887CAF0BB9A042095FEFB6`
- Latest managed row measurement: `2026-11-01T09:48:16.3524325+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-01T09:48:16.3524325+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `40/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `71` managed faster, `1` equivalent, `15` native faster, `14` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `2/2`

Managed rows span more than one measurement environment. Consult the JSON row metadata before interpreting small differences as regressions or wins.

Comparator rows span more than one measurement environment. Consult each row's `PcreNetNativeEnvironment` metadata before comparing small differences.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 1.240 us | 2.489 us | 0.52x | 0.44–0.62x | -1.128 us | 9 pairs; 33/42 ms; 27,007/16,694 ops/lane; IQR 1.114/1.200 | `Pcre2Slice` | 897.588 us | 0.282 us | 0.805 us | 1.54x | 5,744 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.322 us | 11.291 us | 0.12x | 0.11–0.13x | -9.889 us | 9 pairs; 35/50 ms; 26,783/4,408 ops/lane; IQR 1.028/1.151 | `Pcre2Slice` | 1.870 us | 0.095 us | 3.230 us | 0.41x | 3,872 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.217 us | 0.01x | 0.01–0.01x | -1.206 us | 9 pairs; 41/42 ms; 3,749,378/34,094 ops/lane; IQR 1.006/1.011 | `Pcre2Slice` | 0.214 us | 1.038 us | 1.076 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.852 us | 0.161 us | 11.47x | 11.44–11.66x | +1.691 us | 9 pairs; 37/41 ms; 20,202/256,737 ops/lane; IQR 1.017/1.005 | `Pcre2Backtracking` | 0.047 us | 0.121 us | 0.160 us | 11.57x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 6.427 us | 0.555 us | 11.58x | 11.48–11.72x | +5.879 us | 9 pairs; 38/41 ms; 5,965/74,002 ops/lane; IQR 1.009/1.013 | `Pcre2Backtracking` | 0.112 us | 0.727 us | 0.758 us | 8.48x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.169 us | 0.287 us | 0.60x | 0.59–0.60x | -0.116 us | 9 pairs; 40/41 ms; 234,008/142,138 ops/lane; IQR 1.017/1.017 | `Pcre2Slice` | 0.174 us | 0.314 us | 0.339 us | 0.50x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.179 us | 0.349 us | 0.52x | 0.51–0.52x | -0.169 us | 9 pairs; 41/40 ms; 228,453/113,741 ops/lane; IQR 1.016/1.013 | `Pcre2Slice` | 0.185 us | 0.435 us | 0.454 us | 0.40x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.117 us | 0.283 us | 0.41x | 0.41–0.41x | -0.167 us | 9 pairs; 39/39 ms; 334,740/138,586 ops/lane; IQR 1.011/1.006 | `Pcre2Slice` | 0.139 us | 0.228 us | 0.246 us | 0.47x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.113 us | 0.298 us | 0.38x | 0.37–0.38x | -0.186 us | 9 pairs; 39/39 ms; 348,197/130,477 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 0.139 us | 0.243 us | 0.261 us | 0.43x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.166 us | 0.075 us | 2.20x | 2.18–2.30x | +0.091 us | 9 pairs; 38/43 ms; 227,717/569,152 ops/lane; IQR 1.010/1.018 | `Pcre2Slice` | 0.210 us | 0.888 us | 0.913 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.067 us | 0.198 us | 0.34x | 0.34–0.34x | -0.131 us | 9 pairs; 40/41 ms; 594,960/205,772 ops/lane; IQR 1.011/1.019 | `Pcre2Backtracking` | 0.205 us | 0.306 us | 0.346 us | 0.19x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.130 us | 0.605 us | 0.22x | 0.21–0.22x | -0.474 us | 9 pairs; 40/40 ms; 303,793/65,732 ops/lane; IQR 1.008/1.014 | `Pcre2Backtracking` | 0.193 us | 0.225 us | 0.249 us | 0.52x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.152 us | 0.22x | 0.21–0.22x | -0.120 us | 9 pairs; 37/41 ms; 1,137,620/267,516 ops/lane; IQR 1.026/1.012 | `Pcre2Backtracking` | 0.103 us | 0.184 us | 0.200 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.110 us | 0.18x | 0.17–0.18x | -0.091 us | 9 pairs; 37/39 ms; 1,954,475/356,401 ops/lane; IQR 1.041/1.018 | `Pcre2Backtracking` | 0.063 us | 0.088 us | 0.107 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.056 us | 0.123 us | 0.46x | 0.45–0.47x | -0.067 us | 9 pairs; 39/41 ms; 688,161/336,123 ops/lane; IQR 1.008/1.014 | `Pcre2Backtracking` | 0.014 us | 0.084 us | 0.100 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Inconclusive** | 67 B | 0.206 us | 0.275 us | 0.75x | 0.68–0.76x | -0.070 us | 9 pairs; 44/40 ms; 212,038/144,757 ops/lane; IQR 1.106/1.013 | `Pcre2Slice` | 0.202 us | 0.119 us | 0.151 us | 1.36x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.113 us | 0.20x | 0.20–0.20x | -0.091 us | 9 pairs; 40/39 ms; 1,795,429/345,664 ops/lane; IQR 1.019/1.004 | `Pcre2Backtracking` | 0.019 us | 0.085 us | 0.104 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.111 us | 1.730 us | 0.06x | 0.06–0.07x | -1.619 us | 9 pairs; 39/35 ms; 355,878/19,970 ops/lane; IQR 1.006/1.052 | `Pcre2Literal` | 0.117 us | 0.050 us | 0.590 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.115 us | 1.703 us | 0.07x | 0.06–0.07x | -1.588 us | 9 pairs; 37/39 ms; 323,879/22,884 ops/lane; IQR 1.008/1.032 | `Pcre2Literal` | 0.172 us | 0.052 us | 0.590 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.145 us | 2.050 us | 0.07x | 0.06–0.08x | -1.905 us | 9 pairs; 39/49 ms; 271,482/24,093 ops/lane; IQR 1.004/1.114 | `Pcre2Literal` | 0.277 us | 0.231 us | 0.753 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.141 us | 2.075 us | 0.07x | 0.06–0.07x | -1.935 us | 9 pairs; 40/41 ms; 287,597/19,841 ops/lane; IQR 1.007/1.063 | `Pcre2Literal` | 0.213 us | 0.203 us | 0.769 us | 0.18x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.009 us | 0.095 us | 0.09x | 0.09–0.15x | -0.086 us | 9 pairs; 39/40 ms; 4,527,335/422,404 ops/lane; IQR 1.616/1.022 | `Pcre2Slice` | 0.948 us | 0.067 us | 0.085 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.061 us | 0.083 us | 0.74x | 0.72–0.74x | -0.022 us | 9 pairs; 40/39 ms; 654,465/473,572 ops/lane; IQR 1.008/1.024 | `Pcre2Literal` | 0.124 us | 0.050 us | 0.072 us | 0.85x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.107 us | 0.12x | 0.12–0.12x | -0.094 us | 9 pairs; 39/40 ms; 2,896,939/370,098 ops/lane; IQR 1.012/1.012 | `Pcre2Backtracking` | 0.280 us | 0.090 us | 0.110 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 0.205 us | 0.121 us | 1.70x | 1.68–1.71x | +0.084 us | 9 pairs; 40/40 ms; 197,088/330,302 ops/lane; IQR 1.007/1.017 | `Pcre2Character` | 0.133 us | 0.109 us | 0.139 us | 1.48x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.075 us | 0.199 us | 0.38x | 0.38–0.38x | -0.124 us | 9 pairs; 39/40 ms; 515,263/202,139 ops/lane; IQR 1.010/1.013 | `Pcre2Slice` | 0.769 us | 0.074 us | 0.106 us | 0.71x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 75.655 us | 159.062 us | 0.48x | 0.47–0.48x | -83.041 us | 9 pairs; 40/40 ms; 529/253 ops/lane; IQR 1.008/1.008 | `Pcre2Slice` | 395.766 us | 95.062 us | 96.888 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 36.673 us | 283.899 us | 0.13x | 0.13–0.13x | -247.293 us | 9 pairs; 39/43 ms; 1,066/152 ops/lane; IQR 1.003/1.007 | `Pcre2Character` | 425.069 us | 214.183 us | 222.443 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.083 us | 207.076 us | 0.05x | 0.05–0.05x | -196.993 us | 9 pairs; 40/41 ms; 3,965/197 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 7.763 us | 216.575 us | 216.656 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 562.623 us | 203.213 us | 2.79x | 2.75–2.81x | +359.719 us | 9 pairs; 40/39 ms; 71/194 ops/lane; IQR 1.015/1.016 | `Pcre2SingleTokenRepeat` | 7.753 us | 225.778 us | 226.832 us | 2.48x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.467 us | 22.773 us | 0.15x | 0.15–0.16x | -19.334 us | 9 pairs; 39/41 ms; 11,211/1,822 ops/lane; IQR 1.018/1.008 | `Pcre2Literal` | 1.403 us | 4.133 us | 5.893 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.376 us | 79.918 us | 0.12x | 0.12–0.12x | -70.559 us | 9 pairs; 39/39 ms; 4,172/492 ops/lane; IQR 1.003/1.016 | `Pcre2Backtracking` | 9.192 us | 87.053 us | 89.162 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,043.387 us | 26,894.400 us | 0.15x | 0.15–0.15x | -22848.100 us | 9 pairs; 32/27 ms; 8/1 ops/lane; IQR 1.020/1.052 | `Pcre2Backtracking` | 6,303.375 us | 11,277.440 us | 18,320.100 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 5,705.643 us | 56,922.400 us | 0.10x | 0.10–0.11x | -51110.686 us | 9 pairs; 40/57 ms; 7/1 ops/lane; IQR 1.012/1.087 | `Pcre2Backtracking` | 6,231.111 us | 40,427.650 us | 46,514.450 us | 0.12x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,893.959 us | 471,778.100 us | 0.00x | 0.00–0.00x | -469932.386 us | 9 pairs; 42/472 ms; 22/1 ops/lane; IQR 1.036/1.003 | `Pcre2Character` | 1,770.983 us | 22,271.300 us | 28,788.950 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,377.933 us | 11,213.175 us | 0.48x | 0.45–0.54x | -5814.342 us | 9 pairs; 32/45 ms; 6/4 ops/lane; IQR 1.008/1.156 | `Pcre2Literal` | 1,442.431 us | 1,361.908 us | 8,322.325 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,467.400 us | 143,989.300 us | 0.13x | 0.13–0.13x | -125605.750 us | 9 pairs; 37/144 ms; 2/1 ops/lane; IQR 1.011/1.016 | `Pcre2Backtracking` | 1,563.836 us | 498.458 us | 8,077.133 us | 2.29x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,686.100 us | 21,723.850 us | 0.22x | 0.21–0.22x | -17037.750 us | 9 pairs; 37/43 ms; 8/2 ops/lane; IQR 1.011/1.009 | `Pcre2Slice` | 2,763.442 us | 10,440.400 us | 17,909.000 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,664.500 us | 129,875.400 us | 0.17x | 0.17–0.18x | -107223.800 us | 9 pairs; 45/130 ms; 2/1 ops/lane; IQR 1.027/1.004 | `Pcre2Backtracking` | 1,837.719 us | 2,216.013 us | 9,917.450 us | 2.29x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 232.230 us | 984.332 us | 0.24x | 0.23–0.25x | -751.570 us | 9 pairs; 38/43 ms; 165/44 ops/lane; IQR 1.008/1.027 | `Pcre2Backtracking` | 276.393 us | 313.891 us | 534.485 us | 0.43x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Inconclusive** | 594,930 B | 13,141.967 us | 13,021.267 us | 1.01x | 0.99–1.03x | +119.200 us | 9 pairs; 39/39 ms; 3/3 ops/lane; IQR 1.016/1.015 | `Pcre2Backtracking` | 295.007 us | 14,482.275 us | 14,697.900 us | 0.89x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,925.531 us | 95,939.100 us | 0.03x | 0.03–0.03x | -92997.438 us | 9 pairs; 38/96 ms; 13/1 ops/lane; IQR 1.008/1.004 | `Pcre2Character` | 1,941.515 us | 19,014.600 us | 19,385.467 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 181.170 us | 2,664.321 us | 0.07x | 0.07–0.07x | -2482.716 us | 9 pairs; 39/37 ms; 218/14 ops/lane; IQR 1.008/1.020 | `Pcre2SingleTokenRepeat` | 183.590 us | 982.548 us | 1,375.489 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,580.020 us | 19,581.050 us | 0.08x | 0.08–0.08x | -17992.826 us | 9 pairs; 40/39 ms; 25/2 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 559.706 us | 10,154.180 us | 10,028.160 us | 0.16x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.142 us | 1.064 us | 0.13x | 0.13–0.13x | -0.921 us | 9 pairs; 42/39 ms; 295,557/37,070 ops/lane; IQR 1.010/1.006 | `Pcre2Slice` | 1.444 us | 0.252 us | 0.274 us | 0.52x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.480 us | 0.809 us | 0.59x | 0.59–0.59x | -0.330 us | 9 pairs; 39/42 ms; 80,539/51,539 ops/lane; IQR 1.010/1.013 | `Pcre2Literal` | 0.141 us | 0.147 us | 0.169 us | 2.83x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.300 us | 1.159 us | 0.26x | 0.26–0.26x | -0.858 us | 9 pairs; 38/39 ms; 127,641/33,643 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 1.048 us | 0.335 us | 0.365 us | 0.82x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.408 us | 1.116 us | 1.26x | 1.26–1.27x | +0.290 us | 9 pairs; 42/39 ms; 29,716/35,350 ops/lane; IQR 1.011/1.013 | `Pcre2Character` | 0.270 us | 0.415 us | 0.462 us | 3.05x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.153 us | 0.839 us | 0.18x | 0.18–0.18x | -0.686 us | 9 pairs; 39/39 ms; 255,951/46,801 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 1.225 us | 0.154 us | 0.199 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.907 us | 1.083 us | 0.84x | 0.83–0.84x | -0.177 us | 9 pairs; 40/39 ms; 44,514/35,906 ops/lane; IQR 1.017/1.008 | `Pcre2Slice` | 6.127 us | 0.311 us | 0.314 us | 2.89x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.777 us | 0.811 us | 0.95x | 0.95–0.96x | -0.037 us | 9 pairs; 39/42 ms; 50,227/51,191 ops/lane; IQR 1.015/1.014 | `Pcre2Literal` | 3.515 us | 0.177 us | 0.200 us | 3.87x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.963 us | 1.161 us | 0.83x | 0.83–0.83x | -0.195 us | 9 pairs; 39/39 ms; 40,335/33,740 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 5.081 us | 0.396 us | 0.418 us | 2.30x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.676 us | 1.110 us | 1.51x | 1.50–1.52x | +0.569 us | 9 pairs; 39/42 ms; 23,005/37,788 ops/lane; IQR 1.003/1.008 | `Pcre2Character` | 3.617 us | 0.464 us | 0.510 us | 3.29x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.800 us | 0.841 us | 0.95x | 0.95–0.96x | -0.040 us | 9 pairs; 40/40 ms; 49,542/47,193 ops/lane; IQR 1.011/1.009 | `Pcre2Slice` | 2.664 us | 0.179 us | 0.219 us | 3.65x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.606 us | 1.077 us | 0.56x | 0.56–0.57x | -0.472 us | 9 pairs; 40/38 ms; 66,017/35,681 ops/lane; IQR 1.009/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.494 us | 0.819 us | 0.60x | 0.60–0.61x | -0.323 us | 9 pairs; 39/41 ms; 79,797/50,018 ops/lane; IQR 1.007/1.003 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.660 us | 1.168 us | 0.57x | 0.56–0.57x | -0.507 us | 9 pairs; 41/41 ms; 61,694/35,365 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.393 us | 1.121 us | 1.24x | 1.23–1.24x | +0.271 us | 9 pairs; 39/40 ms; 28,267/35,525 ops/lane; IQR 1.018/1.022 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.508 us | 0.842 us | 0.60x | 0.60–0.61x | -0.334 us | 9 pairs; 39/41 ms; 76,035/48,349 ops/lane; IQR 1.009/1.010 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.960 us | — | — | — | — | — | `—` | 64.897 us | 94.483 us | 98.626 us | 0.63x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.376 us | — | — | — | — | — | `—` | 3.260 us | 0.356 us | 0.375 us | 24.98x | 48,792 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.059 us | — | — | — | — | — | `—` | 0.857 us | 0.237 us | 0.269 us | 3.94x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.228 us | — | — | — | — | — | `—` | 2.834 us | 0.444 us | 0.478 us | 2.57x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 2.090 us | — | — | — | — | — | `—` | 0.955 us | 0.525 us | 0.582 us | 3.59x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 5.338 us | — | — | — | — | — | `—` | 1.701 us | 0.257 us | 0.309 us | 17.29x | 7,192 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.118 us | 0.082 us | 1.43x | 1.42–1.45x | +0.035 us | 9 pairs; 39/40 ms; 334,871/485,276 ops/lane; IQR 1.016/1.015 | `Pcre2Character` | 0 B | 0 B | 2.225 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.116 us | 0.11x | 0.10–0.11x | -0.103 us | 9 pairs; 40/37 ms; 3,189,876/322,412 ops/lane; IQR 1.017/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 129.094 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.460 us | 0.103 us | 4.50x | 4.49–4.52x | +0.358 us | 9 pairs; 35/39 ms; 75,381/376,817 ops/lane; IQR 1.020/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 21.837 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.457 us | 0.114 us | 4.01x | 3.95–4.05x | +0.343 us | 9 pairs; 36/38 ms; 79,503/329,791 ops/lane; IQR 1.007/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 69.138 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.454 us | 0.102 us | 4.43x | 4.35–4.47x | +0.352 us | 9 pairs; 37/41 ms; 80,634/401,544 ops/lane; IQR 1.005/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 70.325 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.578 us | 0.342 us | 1.69x | 1.68–1.70x | +0.236 us | 9 pairs; 38/39 ms; 65,581/115,126 ops/lane; IQR 1.007/1.010 | `Pcre2Character` | 0 B | 0 B | 1.413 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.618 us | 0.02x | 0.02–0.02x | -0.606 us | 9 pairs; 40/40 ms; 3,378,289/64,629 ops/lane; IQR 1.053/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 21.831 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.497 us | 0.02x | 0.02–0.02x | -0.485 us | 9 pairs; 40/40 ms; 3,457,604/80,877 ops/lane; IQR 1.015/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 22.712 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.512 us | 0.106 us | 4.84x | 4.78–4.90x | +0.406 us | 9 pairs; 36/39 ms; 71,291/367,882 ops/lane; IQR 1.004/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 17.712 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.151 us | 10.171 us | 0.80x | 0.79–0.81x | -2.030 us | 9 pairs; 41/38 ms; 5,004/3,771 ops/lane; IQR 1.007/1.011 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.299 us | 1.016 us | 0.30x | 0.29–0.30x | -0.716 us | 9 pairs; 40/37 ms; 133,191/36,819 ops/lane; IQR 1.016/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 126.800 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.308 us | 0.459 us | 0.67x | 0.67–0.68x | -0.151 us | 9 pairs; 34/38 ms; 109,874/83,924 ops/lane; IQR 1.012/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 112.006 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.288 us | 0.460 us | 0.63x | 0.62–0.64x | -0.171 us | 9 pairs; 33/41 ms; 113,970/89,350 ops/lane; IQR 1.027/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 139.512 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.296 us | 0.572 us | 0.52x | 0.52–0.52x | -0.277 us | 9 pairs; 34/39 ms; 113,636/67,352 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.781 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.301 us | 0.392 us | 0.77x | 0.76–0.80x | -0.090 us | 9 pairs; 39/39 ms; 127,715/98,875 ops/lane; IQR 1.015/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 214.075 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.049 us | 8.718 us | 0.58x | 0.57–0.58x | -3.702 us | 9 pairs; 40/40 ms; 7,952/4,609 ops/lane; IQR 1.015/1.013 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.261 us | 0.309 us | 0.84x | 0.84–0.85x | -0.049 us | 9 pairs; 37/42 ms; 141,388/137,074 ops/lane; IQR 1.008/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 93.713 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.278 us | 0.396 us | 0.71x | 0.69–0.71x | -0.116 us | 9 pairs; 37/39 ms; 134,762/97,855 ops/lane; IQR 1.007/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 120.713 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.195 us | — | — | — | — | — | `—` | 0 B | — | 13.475 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.185 us | 0.293 us | 0.62x | 0.61–0.63x | -0.110 us | 9 pairs; 41/40 ms; 219,191/134,914 ops/lane; IQR 1.015/1.027 | `Pcre2Backtracking` | 0 B | 0 B | 52.031 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.033 us | — | — | — | — | — | `—` | 0 B | — | 13.819 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.542 us | 0.655 us | 0.82x | 0.81–0.83x | -0.114 us | 9 pairs; 38/38 ms; 69,642/58,431 ops/lane; IQR 1.024/1.011 | `Pcre2Slice` | 0 B | 0 B | 63.513 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.609 us | 28.042 us | 0.34x | 0.34–0.34x | -18.446 us | 9 pairs; 39/39 ms; 4,067/1,384 ops/lane; IQR 1.006/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.769 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.937 us | 1.047 us | 0.89x | 0.89–0.90x | -0.114 us | 9 pairs; 39/41 ms; 42,058/38,773 ops/lane; IQR 1.010/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 128.931 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.981 us | 1.167 us | 0.84x | 0.83–0.84x | -0.192 us | 9 pairs; 40/40 ms; 40,730/34,112 ops/lane; IQR 1.018/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 115.819 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.993 us | 1.052 us | 0.95x | 0.93–0.95x | -0.055 us | 9 pairs; 41/39 ms; 41,620/37,482 ops/lane; IQR 1.017/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 140.588 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.932 us | 1.143 us | 0.81x | 0.80–0.82x | -0.218 us | 9 pairs; 39/39 ms; 42,153/33,726 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 102.531 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.929 us | 0.924 us | 1.00x | 1.00–1.01x | +0.002 us | 9 pairs; 41/42 ms; 44,205/45,444 ops/lane; IQR 1.013/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 207.113 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.755 us | 8.784 us | 0.65x | 0.65–0.66x | -3.034 us | 9 pairs; 41/40 ms; 7,074/4,560 ops/lane; IQR 1.007/1.007 | `Pcre2Character` | 0 B | 0 B | 1.375 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.906 us | 0.874 us | 1.04x | 1.03–1.05x | +0.036 us | 9 pairs; 39/42 ms; 43,401/47,893 ops/lane; IQR 1.011/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 92.756 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.918 us | 0.940 us | 0.97x | 0.97–0.98x | -0.026 us | 9 pairs; 41/40 ms; 44,871/43,000 ops/lane; IQR 1.010/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 122.775 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.353 us | — | — | — | — | — | `—` | 0 B | — | 13.594 us | 11,496 B |
| `pcre2/kreset-global` | **Inconclusive** | 26 B | 0.835 us | 0.848 us | 0.98x | 0.96–0.99x | -0.021 us | 9 pairs; 40/42 ms; 47,713/49,373 ops/lane; IQR 1.019/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 54.325 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.168 us | — | — | — | — | — | `—` | 0 B | — | 13.781 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.853 us | 1.233 us | 0.69x | 0.69–0.69x | -0.379 us | 9 pairs; 40/41 ms; 47,358/32,908 ops/lane; IQR 1.009/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.281 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.324 us | 2.242 us | 0.59x | 0.59–0.59x | -0.921 us | 9 pairs; 40/41 ms; 29,900/18,160 ops/lane; IQR 1.011/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.646 us | 1.038 us | 0.62x | 0.62–0.62x | -0.392 us | 9 pairs; 37/40 ms; 56,967/38,413 ops/lane; IQR 1.008/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 128.450 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.717 us | 1.190 us | 0.60x | 0.60–0.61x | -0.470 us | 9 pairs; 40/42 ms; 55,847/35,204 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 114.381 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.600 us | 1.033 us | 0.58x | 0.58–0.59x | -0.433 us | 9 pairs; 37/40 ms; 61,223/39,164 ops/lane; IQR 1.012/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 139.912 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.643 us | 1.141 us | 0.56x | 0.56–0.57x | -0.496 us | 9 pairs; 41/41 ms; 63,225/36,270 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 100.294 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.618 us | 0.922 us | 0.67x | 0.66–0.67x | -0.303 us | 9 pairs; 39/40 ms; 62,477/43,454 ops/lane; IQR 1.008/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 212.681 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.833 us | 2.143 us | 0.85x | 0.85–0.86x | -0.314 us | 9 pairs; 40/40 ms; 21,983/18,838 ops/lane; IQR 1.006/1.005 | `Pcre2Character` | 0 B | 0 B | 1.962 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.593 us | 0.874 us | 0.68x | 0.68–0.68x | -0.281 us | 9 pairs; 40/41 ms; 66,773/47,017 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 91.044 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.602 us | 0.943 us | 0.63x | 0.63–0.64x | -0.342 us | 9 pairs; 41/39 ms; 67,837/41,822 ops/lane; IQR 1.008/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 122.269 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.046 us | — | — | — | — | — | `—` | 0 B | — | 13.287 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.497 us | 0.852 us | 0.58x | 0.58–0.59x | -0.355 us | 9 pairs; 40/39 ms; 80,145/46,307 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.737 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.921 us | — | — | — | — | — | `—` | 0 B | — | 13.700 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.590 us | 1.233 us | 0.48x | 0.48–0.48x | -0.642 us | 9 pairs; 42/40 ms; 71,164/32,131 ops/lane; IQR 1.012/1.006 | `Pcre2Slice` | 0 B | 0 B | 63.469 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.010 us | — | — | — | — | — | `—` | 480 B | — | 1.113 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.147 us | — | — | — | — | — | `—` | 48 B | — | 128.500 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.243 us | — | — | — | — | — | `—` | 48 B | — | 116.575 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.122 us | — | — | — | — | — | `—` | 48 B | — | 140.894 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.143 us | — | — | — | — | — | `—` | 48 B | — | 101.787 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.124 us | — | — | — | — | — | `—` | 48 B | — | 222.956 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.490 us | — | — | — | — | — | `—` | 64 B | — | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.157 us | — | — | — | — | — | `—` | 40 B | — | 91.838 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.158 us | — | — | — | — | — | `—` | 40 B | — | 120.431 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.718 us | — | — | — | — | — | `—` | 1,224 B | — | 14.119 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.082 us | — | — | — | — | — | `—` | 48 B | — | 52.200 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 17.220 us | — | — | — | — | — | `—` | 936 B | — | 14.531 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 6.094 us | — | — | — | — | — | `—` | 28,936 B | — | 64.369 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.225–215.370 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.050–93.048 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.134–12.376 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.210–49.056 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.041–1.461 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.990–46.354 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.597–22.041 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.604–98.474 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.855–71.141 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.250–5.690 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.337–376.300 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.070–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.902–312.584 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.849–4.964 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 17.909–136.491 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 163.083–1,308.472 us |

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
