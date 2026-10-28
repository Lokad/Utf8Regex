<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `D9DE0D3835C0D072E9B274D02D2750EF31CF7908379CF16928B4498B754DEC6E`
- Latest managed row measurement: `2026-10-28T11:52:05.8744738+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-10-28T11:52:05.8744738+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `33/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `66` managed faster, `3` equivalent, `19` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x1`, `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 859.106 us | 2.301 us | 372.91x | 344.92–412.28x | +856.952 us | 9 pairs; 40/31 ms; 47/13,641 ops/lane; IQR 1.004/1.168 | `Pcre2Slice` | 862.447 us | 0.282 us | 0.901 us | 952.98x | 5,744 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.288 us | 12.125 us | 0.11x | 0.10–0.12x | -10.863 us | 9 pairs; 36/44 ms; 27,871/3,613 ops/lane; IQR 1.016/1.200 | `Pcre2Slice` | 1.876 us | 0.097 us | 3.460 us | 0.37x | 3,872 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.234 us | 0.01x | 0.01–0.01x | -1.223 us | 9 pairs; 39/39 ms; 3,617,609/31,963 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 4.730 us | 1.074 us | 1.112 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.873 us | 0.158 us | 11.80x | 11.73–11.95x | +1.716 us | 9 pairs; 39/39 ms; 21,009/246,389 ops/lane; IQR 1.003/1.016 | `Pcre2Backtracking` | 0.051 us | 0.123 us | 0.165 us | 11.35x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 6.730 us | 0.575 us | 11.70x | 11.66–11.80x | +6.155 us | 9 pairs; 40/40 ms; 5,974/69,639 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0.116 us | 0.767 us | 0.816 us | 8.25x | 0 B | 0 B |
| `common/email-match` | **Inconclusive** | 34 B | 1.660 us | 0.296 us | 5.60x | 5.45–5.83x | +1.364 us | 9 pairs; 37/41 ms; 22,086/137,231 ops/lane; IQR 1.047/1.006 | `Pcre2Slice` | 1.788 us | 0.320 us | 0.351 us | 4.73x | 5,856 B | 0 B |
| `common/email-miss` | **Native faster** | 35 B | 1.876 us | 0.356 us | 5.26x | 5.21–5.38x | +1.521 us | 9 pairs; 37/39 ms; 19,457/110,568 ops/lane; IQR 1.027/1.014 | `Pcre2Slice` | 1.957 us | 0.447 us | 0.473 us | 3.97x | 5,856 B | 0 B |
| `common/ip-match` | **Native faster** | 15 B | 1.065 us | 0.288 us | 3.70x | 3.64–3.78x | +0.777 us | 9 pairs; 36/40 ms; 33,914/138,753 ops/lane; IQR 1.005/1.013 | `Pcre2Slice` | 1.173 us | 0.241 us | 0.266 us | 4.00x | 3,568 B | 0 B |
| `common/ip-miss` | **Native faster** | 15 B | 1.100 us | 0.316 us | 3.51x | 3.47–3.59x | +0.783 us | 9 pairs; 35/39 ms; 31,488/122,281 ops/lane; IQR 1.023/1.016 | `Pcre2Slice` | 1.133 us | 0.257 us | 0.279 us | 3.94x | 3,568 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.164 us | 0.076 us | 2.15x | 2.05–2.21x | +0.088 us | 9 pairs; 40/38 ms; 241,580/499,978 ops/lane; IQR 1.002/1.036 | `Pcre2Slice` | 0.188 us | 0.619 us | 0.660 us | 0.25x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.069 us | 0.198 us | 0.35x | 0.35–0.35x | -0.128 us | 9 pairs; 40/40 ms; 581,587/204,488 ops/lane; IQR 1.002/1.012 | `Pcre2Backtracking` | 3.092 us | 0.309 us | 0.349 us | 0.20x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.134 us | 0.613 us | 0.22x | 0.22–0.22x | -0.478 us | 9 pairs; 40/40 ms; 299,637/65,523 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 5.992 us | 0.237 us | 0.261 us | 0.51x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.153 us | 0.22x | 0.22–0.22x | -0.120 us | 9 pairs; 39/40 ms; 1,159,792/259,211 ops/lane; IQR 1.020/1.009 | `Pcre2Backtracking` | 1.546 us | 0.184 us | 0.203 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.111 us | 0.19x | 0.18–0.19x | -0.090 us | 9 pairs; 41/38 ms; 1,947,506/345,683 ops/lane; IQR 1.019/1.014 | `Pcre2Backtracking` | 0.089 us | 0.093 us | 0.114 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.059 us | 0.118 us | 0.50x | 0.49–0.50x | -0.059 us | 9 pairs; 40/40 ms; 685,655/343,439 ops/lane; IQR 1.005/1.016 | `Pcre2Backtracking` | 0.014 us | 0.090 us | 0.110 us | 0.53x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Native faster** | 67 B | 1.425 us | 0.278 us | 5.13x | 5.09–5.34x | +1.147 us | 9 pairs; 35/38 ms; 24,608/136,761 ops/lane; IQR 1.024/1.015 | `Pcre2Slice` | 2.155 us | 0.119 us | 0.151 us | 9.41x | 5,760 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.118 us | 0.19x | 0.19–0.19x | -0.096 us | 9 pairs; 39/41 ms; 1,755,468/350,781 ops/lane; IQR 1.011/1.005 | `Pcre2Backtracking` | 0.021 us | 0.097 us | 0.116 us | 0.19x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.167 us | 1.948 us | 0.09x | 0.08–0.09x | -1.779 us | 9 pairs; 40/43 ms; 240,237/21,972 ops/lane; IQR 1.007/1.093 | `Pcre2Literal` | 0.134 us | 0.051 us | 0.658 us | 0.25x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.170 us | 1.981 us | 0.09x | 0.08–0.09x | -1.812 us | 9 pairs; 40/39 ms; 237,996/19,866 ops/lane; IQR 1.001/1.081 | `Pcre2Literal` | 0.248 us | 0.054 us | 0.639 us | 0.27x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.211 us | 1.930 us | 0.11x | 0.09–0.12x | -1.719 us | 9 pairs; 34/42 ms; 161,085/22,006 ops/lane; IQR 1.016/1.175 | `Pcre2Literal` | 0.293 us | 0.235 us | 0.838 us | 0.25x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.198 us | 1.755 us | 0.11x | 0.10–0.12x | -1.557 us | 9 pairs; 32/45 ms; 161,865/25,768 ops/lane; IQR 1.002/1.117 | `Pcre2Literal` | 0.229 us | 0.205 us | 0.839 us | 0.24x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 40/39 ms; 4,523,429/392,278 ops/lane; IQR 1.004/1.007 | `Pcre2Slice` | 0.982 us | 0.071 us | 0.090 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Native faster** | 18 B | 0.127 us | 0.086 us | 1.48x | 1.44–1.49x | +0.041 us | 9 pairs; 40/39 ms; 314,158/457,098 ops/lane; IQR 1.003/1.037 | `Pcre2Literal` | 0.152 us | 0.051 us | 0.074 us | 1.73x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.12–0.13x | -0.094 us | 9 pairs; 27/38 ms; 1,907,057/353,561 ops/lane; IQR 1.025/1.009 | `Pcre2Backtracking` | 0.265 us | 0.093 us | 0.115 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 0.280 us | 0.123 us | 2.29x | 2.28–2.33x | +0.158 us | 9 pairs; 36/40 ms; 127,149/324,110 ops/lane; IQR 1.031/1.019 | `Pcre2Character` | 0.160 us | 0.113 us | 0.146 us | 1.91x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.081 us | 0.201 us | 0.40x | 0.39–0.41x | -0.119 us | 9 pairs; 44/40 ms; 544,067/199,373 ops/lane; IQR 1.031/1.010 | `Pcre2Slice` | 0.809 us | 0.075 us | 0.111 us | 0.73x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.780 us | 160.345 us | 0.52x | 0.51–0.52x | -77.565 us | 9 pairs; 41/39 ms; 492/243 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 405.979 us | 107.077 us | 106.801 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.670 us | 282.900 us | 0.13x | 0.13–0.13x | -245.230 us | 9 pairs; 40/39 ms; 1,051/138 ops/lane; IQR 1.002/1.004 | `Pcre2Character` | 483.109 us | 219.925 us | 229.445 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.696 us | 211.049 us | 0.05x | 0.05–0.05x | -201.383 us | 9 pairs; 40/40 ms; 4,100/191 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 996.429 us | 221.433 us | 224.323 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 574.441 us | 216.794 us | 2.65x | 2.64–2.66x | +357.137 us | 9 pairs; 40/43 ms; 70/199 ops/lane; IQR 1.004/1.005 | `Pcre2SingleTokenRepeat` | 131.756 us | 230.612 us | 233.295 us | 2.46x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.566 us | 23.565 us | 0.15x | 0.15–0.16x | -20.005 us | 9 pairs; 39/39 ms; 11,064/1,638 ops/lane; IQR 1.005/1.027 | `Pcre2Literal` | 1.450 us | 4.258 us | 6.120 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.605 us | 80.884 us | 0.12x | 0.12–0.12x | -71.279 us | 9 pairs; 40/41 ms; 4,153/512 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 9.496 us | 91.138 us | 93.338 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,164.733 us | 29,065.200 us | 0.14x | 0.14–0.15x | -24935.756 us | 9 pairs; 37/29 ms; 9/1 ops/lane; IQR 1.019/1.051 | `Pcre2Backtracking` | 6,575.837 us | 12,095.360 us | 19,171.300 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 19,900.850 us | 55,016.300 us | 0.36x | 0.36–0.37x | -34700.450 us | 9 pairs; 40/55 ms; 2/1 ops/lane; IQR 1.020/1.020 | `Pcre2Backtracking` | 21,830.867 us | 42,672.850 us | 49,210.600 us | 0.40x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,954.710 us | 484,161.000 us | 0.00x | 0.00–0.00x | -482200.314 us | 9 pairs; 41/484 ms; 21/1 ops/lane; IQR 1.038/1.001 | `Pcre2Character` | 1,406,977.100 us | 23,211.567 us | 30,649.750 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,538.817 us | 12,097.567 us | 0.46x | 0.44–0.47x | -6541.650 us | 9 pairs; 33/36 ms; 6/3 ops/lane; IQR 1.008/1.055 | `Pcre2Literal` | 1,470.897 us | 1,396.718 us | 8,530.433 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 19,075.800 us | 145,776.700 us | 0.13x | 0.13–0.13x | -126765.750 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 1,629.994 us | 535.543 us | 8,557.667 us | 2.23x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Native faster** | 6,839,410 B | 1,090,744.800 us | 21,934.650 us | 49.58x | 49.27–50.13x | +1068663.550 us | 9 pairs; 1091/44 ms; 1/2 ops/lane; IQR 1.002/1.011 | `Pcre2Slice` | 1,117,759.800 us | 10,780.800 us | 19,008.167 us | 57.38x | 3,568 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 23,451.750 us | 133,634.200 us | 0.18x | 0.17–0.18x | -110259.550 us | 9 pairs; 47/134 ms; 2/1 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 1,911.744 us | 2,369.495 us | 10,525.480 us | 2.23x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 776.420 us | 994.023 us | 0.78x | 0.76–0.79x | -216.211 us | 9 pairs; 39/40 ms; 50/40 ops/lane; IQR 1.004/1.029 | `Pcre2Backtracking` | 893.911 us | 321.336 us | 544.902 us | 1.42x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Equivalent** | 594,930 B | 13,670.067 us | 13,582.767 us | 1.01x | 1.01–1.01x | +92.600 us | 9 pairs; 41/41 ms; 3/3 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 320.392 us | 15,071.075 us | 15,315.350 us | 0.89x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,798.900 us | 98,402.000 us | 0.03x | 0.03–0.03x | -95616.114 us | 9 pairs; 39/98 ms; 14/1 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 58,116.500 us | 19,909.100 us | 20,140.600 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.621 us | 2,902.357 us | 0.06x | 0.06–0.06x | -2717.130 us | 9 pairs; 40/41 ms; 214/14 ops/lane; IQR 1.004/1.013 | `Pcre2SingleTokenRepeat` | 10,708.580 us | 1,018.168 us | 1,435.688 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,615.992 us | 20,753.300 us | 0.08x | 0.08–0.08x | -19142.856 us | 9 pairs; 40/42 ms; 25/2 ops/lane; IQR 1.007/1.003 | `Pcre2Backtracking` | 275,646.100 us | 10,973.180 us | 11,288.200 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.157 us | 1.097 us | 0.14x | 0.14–0.14x | -0.941 us | 9 pairs; 40/40 ms; 256,592/36,686 ops/lane; IQR 1.004/1.003 | `Pcre2Slice` | 1.913 us | 0.266 us | 0.290 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.555 us | 0.828 us | 0.67x | 0.66–0.67x | -0.274 us | 9 pairs; 40/40 ms; 72,396/48,563 ops/lane; IQR 1.011/1.003 | `Pcre2Literal` | 0.149 us | 0.148 us | 0.173 us | 3.21x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.327 us | 1.184 us | 0.28x | 0.28–0.28x | -0.856 us | 9 pairs; 40/40 ms; 122,281/33,847 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 1.069 us | 0.354 us | 0.384 us | 0.85x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.492 us | 1.130 us | 1.32x | 1.31–1.32x | +0.362 us | 9 pairs; 40/40 ms; 27,028/35,506 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 0.268 us | 0.430 us | 0.475 us | 3.14x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.174 us | 0.861 us | 0.20x | 0.20–0.20x | -0.687 us | 9 pairs; 40/40 ms; 228,427/46,630 ops/lane; IQR 1.004/1.003 | `Pcre2Slice` | 1.253 us | 0.155 us | 0.202 us | 0.86x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.891 us | 1.109 us | 0.80x | 0.80–0.80x | -0.218 us | 9 pairs; 39/40 ms; 44,331/35,983 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 6.601 us | 0.311 us | 0.328 us | 2.72x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.788 us | 0.839 us | 0.94x | 0.94–0.94x | -0.051 us | 9 pairs; 39/40 ms; 49,691/48,243 ops/lane; IQR 1.003/1.001 | `Pcre2Literal` | 3.617 us | 0.181 us | 0.202 us | 3.90x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.982 us | 1.240 us | 0.79x | 0.79–0.80x | -0.256 us | 9 pairs; 40/40 ms; 40,695/32,579 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 5.290 us | 0.410 us | 0.435 us | 2.26x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.721 us | 1.135 us | 1.52x | 1.51–1.52x | +0.587 us | 9 pairs; 40/40 ms; 23,102/35,194 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 3.703 us | 0.488 us | 0.530 us | 3.25x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.811 us | 0.857 us | 0.95x | 0.94–0.95x | -0.046 us | 9 pairs; 40/40 ms; 48,871/46,908 ops/lane; IQR 1.001/1.003 | `Pcre2Slice` | 2.827 us | 0.182 us | 0.224 us | 3.62x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.543 us | 1.109 us | 0.49x | 0.49–0.49x | -0.565 us | 9 pairs; 40/40 ms; 73,316/36,166 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.499 us | 0.838 us | 0.60x | 0.59–0.60x | -0.339 us | 9 pairs; 40/40 ms; 79,848/47,763 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.712 us | 1.239 us | 0.58x | 0.57–0.58x | -0.526 us | 9 pairs; 40/40 ms; 55,486/32,413 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.421 us | 1.133 us | 1.25x | 1.25–1.25x | +0.285 us | 9 pairs; 40/40 ms; 28,231/35,387 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.528 us | 0.851 us | 0.62x | 0.62–0.62x | -0.324 us | 9 pairs; 40/40 ms; 75,128/47,378 ops/lane; IQR 1.004/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 63.119 us | — | — | — | — | — | `—` | 71.694 us | 99.724 us | 101.507 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 10.560 us | — | — | — | — | — | `—` | 3.455 us | 0.372 us | 0.399 us | 26.48x | 48,784 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.026 us | — | — | — | — | — | `—` | 0.887 us | 0.245 us | 0.277 us | 3.71x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.205 us | — | — | — | — | — | `—` | 3.104 us | 0.471 us | 0.508 us | 2.37x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 2.039 us | — | — | — | — | — | `—` | 0.997 us | 0.547 us | 0.596 us | 3.42x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 5.432 us | — | — | — | — | — | `—` | 1.755 us | 0.262 us | 0.309 us | 17.59x | 7,184 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.191 us | 0.084 us | 2.28x | 2.25–2.29x | +0.107 us | 9 pairs; 40/41 ms; 212,232/493,015 ops/lane; IQR 1.007/1.024 | `Pcre2Character` | 0 B | 0 B | 2.013 us | 2,696 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.120 us | 0.11x | 0.11–0.11x | -0.107 us | 9 pairs; 39/39 ms; 3,029,399/324,901 ops/lane; IQR 1.005/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 127.106 us | 111,248 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.542 us | 0.102 us | 5.33x | 5.31–5.37x | +0.440 us | 9 pairs; 36/39 ms; 65,749/380,976 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 20.981 us | 18,568 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.552 us | 0.116 us | 4.76x | 4.72–4.82x | +0.438 us | 9 pairs; 36/38 ms; 65,749/327,530 ops/lane; IQR 1.011/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 67.769 us | 64,672 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.538 us | 0.104 us | 5.18x | 5.14–5.18x | +0.434 us | 9 pairs; 36/39 ms; 67,817/379,051 ops/lane; IQR 1.007/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 68.925 us | 64,632 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.659 us | 0.345 us | 1.91x | 1.88–1.91x | +0.314 us | 9 pairs; 39/39 ms; 59,471/114,211 ops/lane; IQR 1.001/1.010 | `Pcre2Character` | 0 B | 0 B | 1.250 us | 2,232 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.640 us | 0.02x | 0.02–0.02x | -0.628 us | 9 pairs; 41/40 ms; 3,414,964/62,648 ops/lane; IQR 1.018/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 20.788 us | 21,360 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.505 us | 0.02x | 0.02–0.02x | -0.492 us | 9 pairs; 41/39 ms; 3,393,596/76,318 ops/lane; IQR 1.035/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 20.744 us | 20,848 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.582 us | 0.113 us | 5.15x | 5.02–5.18x | +0.469 us | 9 pairs; 38/41 ms; 65,384/362,681 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 17.481 us | 13,656 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.728 us | 10.491 us | 0.83x | 0.83–0.84x | -1.766 us | 9 pairs; 39/40 ms; 4,516/3,781 ops/lane; IQR 1.004/1.004 | `Pcre2Literal` | 0 B | 0 B | 1.131 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.330 us | 1.068 us | 0.31x | 0.31–0.31x | -0.737 us | 9 pairs; 37/40 ms; 112,931/37,776 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 127.956 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.348 us | 0.463 us | 0.75x | 0.75–0.76x | -0.115 us | 9 pairs; 33/41 ms; 93,595/88,185 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 111.069 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.327 us | 0.460 us | 0.71x | 0.71–0.71x | -0.134 us | 9 pairs; 37/39 ms; 114,095/83,861 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 138.525 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.328 us | 0.595 us | 0.55x | 0.55–0.56x | -0.266 us | 9 pairs; 35/40 ms; 105,422/67,516 ops/lane; IQR 1.017/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 100.750 us | 80,768 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.318 us | 0.365 us | 0.87x | 0.85–0.87x | -0.049 us | 9 pairs; 37/39 ms; 115,723/106,759 ops/lane; IQR 1.004/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 212.181 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.217 us | 8.897 us | 0.59x | 0.59–0.59x | -3.681 us | 9 pairs; 40/40 ms; 7,652/4,497 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0 B | 0 B | 1.250 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.299 us | 0.312 us | 0.96x | 0.96–0.97x | -0.012 us | 9 pairs; 34/39 ms; 113,061/124,735 ops/lane; IQR 1.020/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 89.256 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.338 us | 0.415 us | 0.81x | 0.71–0.82x | -0.077 us | 9 pairs; 38/39 ms; 113,113/94,504 ops/lane; IQR 1.114/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 120.562 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.309 us | — | — | — | — | — | `—` | 0 B | — | 14.469 us | 11,256 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.217 us | 0.284 us | 0.76x | 0.74–0.77x | -0.068 us | 9 pairs; 35/38 ms; 159,385/134,125 ops/lane; IQR 1.002/1.032 | `Pcre2Backtracking` | 0 B | 0 B | 53.125 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.187 us | — | — | — | — | — | `—` | 0 B | — | 13.387 us | 10,296 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.537 us | 0.677 us | 0.79x | 0.79–0.80x | -0.139 us | 9 pairs; 37/39 ms; 69,743/57,047 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | 0 B | 0 B | 61.050 us | 55,256 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.870 us | 29.199 us | 0.34x | 0.34–0.34x | -19.337 us | 9 pairs; 40/40 ms; 4,095/1,369 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.706 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.959 us | 1.073 us | 0.89x | 0.89–0.90x | -0.116 us | 9 pairs; 39/41 ms; 40,742/37,956 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 130.644 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.002 us | 1.202 us | 0.83x | 0.83–0.84x | -0.200 us | 9 pairs; 40/40 ms; 39,665/33,501 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 112.638 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.935 us | 1.030 us | 0.91x | 0.91–0.91x | -0.093 us | 9 pairs; 40/41 ms; 42,769/39,436 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 138.438 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.943 us | 1.175 us | 0.80x | 0.80–0.80x | -0.233 us | 9 pairs; 39/40 ms; 41,595/34,007 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 101.031 us | 80,768 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.942 us | 0.946 us | 1.00x | 0.99–1.00x | -0.004 us | 9 pairs; 40/40 ms; 42,175/42,544 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 213.550 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.903 us | 8.868 us | 0.67x | 0.66–0.67x | -2.968 us | 9 pairs; 40/40 ms; 6,799/4,499 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 0 B | 0 B | 1.219 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Equivalent** | 13 B | 0.918 us | 0.903 us | 1.02x | 1.01–1.02x | +0.015 us | 9 pairs; 39/40 ms; 42,394/44,737 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 90.244 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.915 us | 1.017 us | 0.90x | 0.90–0.90x | -0.102 us | 9 pairs; 39/40 ms; 42,391/39,705 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 122.344 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.463 us | — | — | — | — | — | `—` | 0 B | — | 13.688 us | 11,256 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.836 us | 0.861 us | 0.97x | 0.97–0.97x | -0.026 us | 9 pairs; 40/40 ms; 48,298/46,189 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.619 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.344 us | — | — | — | — | — | `—` | 0 B | — | 14.806 us | 10,296 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.870 us | 1.258 us | 0.69x | 0.69–0.69x | -0.388 us | 9 pairs; 40/39 ms; 45,990/31,083 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 0 B | 0 B | 61.069 us | 55,256 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.370 us | 2.288 us | 0.60x | 0.60–0.60x | -0.917 us | 9 pairs; 40/40 ms; 28,939/17,396 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.706 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.634 us | 1.080 us | 0.59x | 0.58–0.59x | -0.446 us | 9 pairs; 40/40 ms; 63,078/37,381 ops/lane; IQR 1.006/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 129.400 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.723 us | 1.197 us | 0.60x | 0.60–0.61x | -0.476 us | 9 pairs; 40/40 ms; 55,685/33,837 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 113.425 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.661 us | 1.029 us | 0.64x | 0.64–0.64x | -0.370 us | 9 pairs; 41/40 ms; 62,604/38,919 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 138.244 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.633 us | 1.174 us | 0.54x | 0.54–0.54x | -0.540 us | 9 pairs; 40/40 ms; 62,989/34,305 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 101.894 us | 80,768 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.628 us | 0.945 us | 0.66x | 0.66–0.67x | -0.318 us | 9 pairs; 40/40 ms; 64,274/42,138 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 212.200 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.861 us | 2.187 us | 0.85x | 0.85–0.85x | -0.325 us | 9 pairs; 40/42 ms; 21,468/19,038 ops/lane; IQR 1.004/1.001 | `Pcre2Character` | 0 B | 0 B | 1.219 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.630 us | 0.903 us | 0.70x | 0.69–0.70x | -0.273 us | 9 pairs; 40/41 ms; 63,110/45,120 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 90.006 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.613 us | 1.020 us | 0.60x | 0.60–0.60x | -0.407 us | 9 pairs; 40/40 ms; 65,382/39,485 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 121.088 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.172 us | — | — | — | — | — | `—` | 0 B | — | 14.625 us | 11,256 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.502 us | 0.861 us | 0.58x | 0.58–0.58x | -0.359 us | 9 pairs; 40/40 ms; 79,326/46,050 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.350 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.047 us | — | — | — | — | — | `—` | 0 B | — | 13.525 us | 10,296 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.531 us | 1.258 us | 0.42x | 0.42–0.42x | -0.727 us | 9 pairs; 39/40 ms; 73,749/31,945 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | 0 B | 0 B | 63.038 us | 55,256 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.712 us | — | — | — | — | — | `—` | 480 B | — | 0.675 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.101 us | — | — | — | — | — | `—` | 48 B | — | 129.675 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.210 us | — | — | — | — | — | `—` | 48 B | — | 112.031 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.091 us | — | — | — | — | — | `—` | 48 B | — | 138.400 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.104 us | — | — | — | — | — | `—` | 48 B | — | 102.750 us | 80,768 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.087 us | — | — | — | — | — | `—` | 48 B | — | 214.438 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.331 us | — | — | — | — | — | `—` | 64 B | — | 1.219 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.113 us | — | — | — | — | — | `—` | 40 B | — | 91.600 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.110 us | — | — | — | — | — | `—` | 40 B | — | 124.325 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.910 us | — | — | — | — | — | `—` | 1,216 B | — | 13.775 us | 11,256 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.027 us | — | — | — | — | — | `—` | 48 B | — | 52.788 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 17.238 us | — | — | — | — | — | `—` | 928 B | — | 13.438 us | 10,296 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 10.067 us | — | — | — | — | — | `—` | 28,928 B | — | 62.031 us | 55,256 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.253–234.347 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.464–95.579 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.111–12.625 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.288–49.282 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.037–1.492 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.822–45.261 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.811–22.721 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.284–100.046 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.197–72.366 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.182–5.083 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.351–378.059 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.128–0.137 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 40.225–312.314 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.855–5.123 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.693–143.088 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 175.718–1,367.355 us |

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
