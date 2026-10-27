<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `3998896E928679110DA522AEE63F46C40D4A982030CD26427BC903069C2AEAC8`
- Latest managed row measurement: `2026-10-27T19:12:39.4681022+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-10-27T19:12:39.4681022+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `32/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `65` managed faster, `1` equivalent, `21` native faster, `14` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `scheduler-default unavailable`, `single-least-contended-allowed-processor 0x4`
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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 856.100 us | 2.310 us | 370.10x | 338.14–405.90x | +853.845 us | 9 pairs; 39/39 ms; 46/16,698 ops/lane; IQR 1.002/1.162 | `Pcre2Slice` | 868.519 us | 0.283 us | 0.940 us | 911.17x | 5,744 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.266 us | 11.184 us | 0.11x | 0.10–0.12x | -9.890 us | 9 pairs; 36/40 ms; 28,410/3,555 ops/lane; IQR 1.049/1.135 | `Pcre2Slice` | 1.858 us | 0.097 us | 3.647 us | 0.35x | 3,872 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.241 us | 0.01x | 0.01–0.01x | -1.230 us | 9 pairs; 39/40 ms; 3,620,607/31,899 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | 4.727 us | 1.168 us | 1.268 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.946 us | 0.159 us | 12.24x | 12.09–12.35x | +1.784 us | 9 pairs; 39/41 ms; 19,978/256,817 ops/lane; IQR 1.009/1.025 | `Pcre2Backtracking` | 0.047 us | 0.124 us | 0.168 us | 11.60x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 6.717 us | 0.575 us | 11.68x | 11.59–11.75x | +6.143 us | 9 pairs; 40/31 ms; 5,967/53,332 ops/lane; IQR 1.003/1.013 | `Pcre2Backtracking` | 0.132 us | 0.756 us | 0.803 us | 8.37x | 0 B | 0 B |
| `common/email-match` | **Inconclusive** | 34 B | 1.661 us | 0.304 us | 5.48x | 5.34–5.59x | +1.352 us | 9 pairs; 36/40 ms; 21,709/133,153 ops/lane; IQR 1.047/1.009 | `Pcre2Slice` | 1.620 us | 0.321 us | 0.346 us | 4.80x | 5,856 B | 0 B |
| `common/email-miss` | **Native faster** | 35 B | 1.862 us | 0.358 us | 5.18x | 5.11–5.37x | +1.498 us | 9 pairs; 36/40 ms; 19,331/110,919 ops/lane; IQR 1.050/1.008 | `Pcre2Slice` | 1.824 us | 0.448 us | 0.482 us | 3.86x | 5,856 B | 0 B |
| `common/ip-match` | **Native faster** | 15 B | 1.085 us | 0.291 us | 3.65x | 3.60–3.86x | +0.787 us | 9 pairs; 35/40 ms; 32,598/137,767 ops/lane; IQR 1.036/1.014 | `Pcre2Slice` | 1.109 us | 0.243 us | 0.268 us | 4.04x | 3,568 B | 0 B |
| `common/ip-miss` | **Inconclusive** | 15 B | 1.134 us | 0.312 us | 3.63x | 3.56–3.69x | +0.819 us | 9 pairs; 37/39 ms; 32,615/126,514 ops/lane; IQR 1.022/1.015 | `Pcre2Slice` | 1.156 us | 0.256 us | 0.281 us | 4.04x | 3,568 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 34.774 us | 0.076 us | 457.49x | 444.36–470.14x | +34.696 us | 9 pairs; 40/42 ms; 1,164/557,839 ops/lane; IQR 1.005/1.046 | `Pcre2Slice` | 37.073 us | 0.972 us | 1.057 us | 32.89x | 3,552 B | 0 B |
| `common/uri-match` | **Inconclusive** | 46 B | 0.070 us | 0.198 us | 0.35x | 0.34–0.36x | -0.128 us | 9 pairs; 40/42 ms; 574,039/212,057 ops/lane; IQR 1.008/1.022 | `Pcre2Backtracking` | 3.120 us | 0.306 us | 0.346 us | 0.20x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.133 us | 0.612 us | 0.22x | 0.22–0.22x | -0.480 us | 9 pairs; 40/42 ms; 299,029/67,834 ops/lane; IQR 1.007/1.010 | `Pcre2Backtracking` | 5.866 us | 0.236 us | 0.261 us | 0.51x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.035 us | 0.158 us | 0.22x | 0.21–0.22x | -0.123 us | 9 pairs; 39/40 ms; 1,130,418/250,735 ops/lane; IQR 1.023/1.016 | `Pcre2Backtracking` | 1.532 us | 0.174 us | 0.195 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Inconclusive** | 10 B | 0.020 us | 0.113 us | 0.18x | 0.18–0.19x | -0.093 us | 9 pairs; 40/39 ms; 1,937,215/345,894 ops/lane; IQR 1.054/1.026 | `Pcre2Backtracking` | 0.094 us | 0.092 us | 0.116 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.117 us | 0.50x | 0.49–0.51x | -0.059 us | 9 pairs; 40/41 ms; 686,818/350,192 ops/lane; IQR 1.006/1.033 | `Pcre2Backtracking` | 0.015 us | 0.087 us | 0.106 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Native faster** | 67 B | 1.455 us | 0.283 us | 5.13x | 5.01–5.30x | +1.171 us | 9 pairs; 36/39 ms; 24,512/137,562 ops/lane; IQR 1.038/1.018 | `Pcre2Slice` | 2.109 us | 0.122 us | 0.155 us | 9.39x | 5,760 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.116 us | 0.20x | 0.20–0.21x | -0.093 us | 9 pairs; 39/39 ms; 1,660,032/333,990 ops/lane; IQR 1.042/1.010 | `Pcre2Backtracking` | 0.021 us | 0.093 us | 0.113 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.165 us | 1.813 us | 0.09x | 0.07–0.10x | -1.648 us | 9 pairs; 40/36 ms; 244,230/19,780 ops/lane; IQR 1.006/1.235 | `Pcre2Literal` | 0.133 us | 0.051 us | 0.616 us | 0.27x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.170 us | 1.916 us | 0.09x | 0.07–0.10x | -1.746 us | 9 pairs; 41/42 ms; 238,432/21,747 ops/lane; IQR 1.004/1.357 | `Pcre2Literal` | 0.241 us | 0.055 us | 0.642 us | 0.27x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.203 us | 1.994 us | 0.10x | 0.10–0.11x | -1.791 us | 9 pairs; 40/39 ms; 196,872/19,508 ops/lane; IQR 1.002/1.063 | `Pcre2Literal` | 0.296 us | 0.237 us | 0.856 us | 0.24x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.206 us | 1.891 us | 0.11x | 0.10–0.12x | -1.692 us | 9 pairs; 41/37 ms; 201,596/19,557 ops/lane; IQR 1.042/1.096 | `Pcre2Literal` | 0.240 us | 0.206 us | 0.805 us | 0.26x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.101 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 39/40 ms; 4,482,627/397,413 ops/lane; IQR 1.011/1.010 | `Pcre2Slice` | 0.986 us | 0.070 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Native faster** | 18 B | 0.128 us | 0.092 us | 1.40x | 1.39–1.41x | +0.036 us | 9 pairs; 40/41 ms; 312,475/444,056 ops/lane; IQR 1.005/1.004 | `Pcre2Literal` | 0.151 us | 0.051 us | 0.074 us | 1.74x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.116 us | 0.12x | 0.12–0.13x | -0.102 us | 9 pairs; 40/40 ms; 2,902,070/347,782 ops/lane; IQR 1.029/1.018 | `Pcre2Backtracking` | 0.270 us | 0.091 us | 0.114 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 0.282 us | 0.124 us | 2.26x | 2.25–2.28x | +0.157 us | 9 pairs; 37/39 ms; 129,937/312,608 ops/lane; IQR 1.009/1.012 | `Pcre2Character` | 0.159 us | 0.119 us | 0.148 us | 1.91x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.072 us | 0.204 us | 0.36x | 0.35–0.36x | -0.131 us | 9 pairs; 35/41 ms; 486,828/199,107 ops/lane; IQR 1.004/1.012 | `Pcre2Slice` | 0.782 us | 0.077 us | 0.114 us | 0.64x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.451 us | 161.773 us | 0.50x | 0.49–0.50x | -81.359 us | 9 pairs; 40/40 ms; 496/248 ops/lane; IQR 1.007/1.012 | `Pcre2Slice` | 404.712 us | 100.804 us | 105.003 us | 0.77x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.167 us | 282.107 us | 0.13x | 0.13–0.13x | -244.960 us | 9 pairs; 40/39 ms; 1,080/140 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 492.267 us | 224.199 us | 237.407 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.562 us | 210.832 us | 0.05x | 0.04–0.05x | -201.293 us | 9 pairs; 40/39 ms; 4,181/186 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 1,012.388 us | 226.160 us | 230.147 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 581.657 us | 203.116 us | 2.86x | 2.85–2.88x | +377.709 us | 9 pairs; 40/38 ms; 68/188 ops/lane; IQR 1.003/1.008 | `Pcre2SingleTokenRepeat` | 132.203 us | 242.671 us | 245.839 us | 2.37x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.550 us | 24.089 us | 0.15x | 0.14–0.15x | -20.486 us | 9 pairs; 39/41 ms; 11,108/1,706 ops/lane; IQR 1.004/1.027 | `Pcre2Literal` | 1.443 us | 4.301 us | 6.124 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.617 us | 81.452 us | 0.12x | 0.12–0.12x | -71.814 us | 9 pairs; 40/40 ms; 4,150/486 ops/lane; IQR 1.005/1.001 | `Pcre2Backtracking` | 9.484 us | 91.430 us | 94.000 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,173.170 us | 28,609.600 us | 0.15x | 0.14–0.15x | -24402.500 us | 9 pairs; 42/29 ms; 10/1 ops/lane; IQR 1.006/1.016 | `Pcre2Backtracking` | 6,509.312 us | 12,403.380 us | 19,162.900 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 20,089.600 us | 55,099.300 us | 0.36x | 0.36–0.37x | -35183.300 us | 9 pairs; 40/55 ms; 2/1 ops/lane; IQR 1.013/1.016 | `Pcre2Backtracking` | 21,958.733 us | 41,520.450 us | 48,497.550 us | 0.41x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,946.543 us | 485,614.300 us | 0.00x | 0.00–0.00x | -483652.867 us | 9 pairs; 41/486 ms; 21/1 ops/lane; IQR 1.037/1.003 | `Pcre2Character` | 1,414,419.800 us | 23,024.267 us | 30,696.100 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,494.000 us | 11,764.600 us | 0.47x | 0.43–0.51x | -6260.933 us | 9 pairs; 33/35 ms; 6/3 ops/lane; IQR 1.007/1.086 | `Pcre2Literal` | 1,464.057 us | 1,402.656 us | 8,528.180 us | 0.64x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,906.000 us | 146,384.200 us | 0.13x | 0.13–0.13x | -127468.750 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 1,609.334 us | 512.856 us | 8,565.583 us | 2.21x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Native faster** | 6,839,410 B | 1,084,370.500 us | 21,851.300 us | 49.59x | 49.30–49.90x | +1062487.600 us | 9 pairs; 1084/44 ms; 1/2 ops/lane; IQR 1.002/1.007 | `Pcre2Slice` | 1,129,484.500 us | 10,936.480 us | 19,694.400 us | 55.06x | 3,568 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 23,293.150 us | 133,627.500 us | 0.17x | 0.17–0.18x | -110485.250 us | 9 pairs; 47/134 ms; 2/1 ops/lane; IQR 1.012/1.005 | `Pcre2Backtracking` | 1,917.693 us | 2,287.591 us | 10,248.200 us | 2.27x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 776.656 us | 986.831 us | 0.79x | 0.78–0.81x | -210.175 us | 9 pairs; 39/41 ms; 50/42 ops/lane; IQR 1.004/1.016 | `Pcre2Backtracking` | 895.656 us | 328.731 us | 582.251 us | 1.33x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Native faster** | 594,930 B | 14,032.167 us | 13,472.133 us | 1.04x | 1.04–1.05x | +562.133 us | 9 pairs; 42/40 ms; 3/3 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 319.043 us | 15,369.375 us | 15,719.475 us | 0.89x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,808.571 us | 36,998.900 us | 0.08x | 0.08–0.08x | -34195.393 us | 9 pairs; 39/37 ms; 14/1 ops/lane; IQR 1.003/1.004 | `Pcre2Character` | 58,714.100 us | 20,062.233 us | 20,540.733 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.669 us | 2,819.333 us | 0.07x | 0.06–0.07x | -2634.195 us | 9 pairs; 40/42 ms; 213/15 ops/lane; IQR 1.013/1.014 | `Pcre2SingleTokenRepeat` | 11,116.420 us | 1,014.337 us | 1,446.750 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,631.360 us | 20,109.800 us | 0.08x | 0.08–0.08x | -18478.440 us | 9 pairs; 41/40 ms; 25/2 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 279,636.200 us | 10,352.160 us | 10,755.840 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.169 us | 1.092 us | 0.15x | 0.15–0.16x | -0.923 us | 9 pairs; 39/40 ms; 229,511/36,521 ops/lane; IQR 1.007/1.001 | `Pcre2Slice` | 1.989 us | 0.261 us | 0.285 us | 0.59x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.536 us | 0.832 us | 0.65x | 0.64–0.65x | -0.295 us | 9 pairs; 40/40 ms; 75,153/48,198 ops/lane; IQR 1.003/1.001 | `Pcre2Literal` | 0.156 us | 0.149 us | 0.175 us | 3.06x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.330 us | 1.181 us | 0.28x | 0.28–0.28x | -0.851 us | 9 pairs; 40/40 ms; 119,690/33,586 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 1.109 us | 0.349 us | 0.381 us | 0.87x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.410 us | 1.126 us | 1.25x | 1.25–1.26x | +0.286 us | 9 pairs; 40/40 ms; 28,685/35,515 ops/lane; IQR 1.003/1.004 | `Pcre2Character` | 0.273 us | 0.461 us | 0.498 us | 2.83x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.172 us | 0.850 us | 0.20x | 0.20–0.20x | -0.678 us | 9 pairs; 40/41 ms; 232,529/48,426 ops/lane; IQR 1.004/1.002 | `Pcre2Slice` | 1.261 us | 0.158 us | 0.202 us | 0.85x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.946 us | 1.105 us | 0.85x | 0.85–0.86x | -0.161 us | 9 pairs; 40/40 ms; 42,234/36,230 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | 6.596 us | 0.306 us | 0.324 us | 2.92x | 0 B | 0 B |
| `simple/foo-dense` | **Equivalent** | 18 B | 0.830 us | 0.838 us | 0.99x | 0.99–1.00x | -0.007 us | 9 pairs; 41/40 ms; 49,066/47,628 ops/lane; IQR 1.004/1.003 | `Pcre2Literal` | 3.674 us | 0.184 us | 0.203 us | 4.09x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.004 us | 1.198 us | 0.84x | 0.84–0.84x | -0.193 us | 9 pairs; 41/39 ms; 40,596/32,877 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 5.328 us | 0.406 us | 0.428 us | 2.35x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.793 us | 1.133 us | 1.58x | 1.57–1.59x | +0.659 us | 9 pairs; 40/40 ms; 22,393/35,531 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 3.770 us | 0.509 us | 0.540 us | 3.32x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.811 us | 0.856 us | 0.95x | 0.94–0.95x | -0.044 us | 9 pairs; 40/41 ms; 49,483/48,378 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | 2.801 us | 0.182 us | 0.223 us | 3.64x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.607 us | 1.160 us | 0.52x | 0.52–0.53x | -0.551 us | 9 pairs; 40/40 ms; 65,168/34,436 ops/lane; IQR 1.006/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.498 us | 0.839 us | 0.59x | 0.59–0.60x | -0.342 us | 9 pairs; 40/40 ms; 80,515/48,045 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.759 us | 1.198 us | 0.63x | 0.63–0.64x | -0.438 us | 9 pairs; 42/39 ms; 55,205/32,962 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.413 us | 1.135 us | 1.25x | 1.24–1.25x | +0.279 us | 9 pairs; 40/39 ms; 28,011/34,715 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.493 us | 0.855 us | 0.58x | 0.57–0.58x | -0.362 us | 9 pairs; 40/40 ms; 81,569/46,810 ops/lane; IQR 1.007/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 63.092 us | — | — | — | — | — | `—` | 71.236 us | 98.032 us | 99.896 us | 0.63x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 10.058 us | — | — | — | — | — | `—` | 3.361 us | 0.357 us | 0.384 us | 26.17x | 48,784 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 0.977 us | — | — | — | — | — | `—` | 0.905 us | 0.243 us | 0.274 us | 3.56x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.139 us | — | — | — | — | — | `—` | 3.057 us | 0.455 us | 0.491 us | 2.32x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 2.036 us | — | — | — | — | — | `—` | 1.004 us | 0.564 us | 0.608 us | 3.35x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 5.462 us | — | — | — | — | — | `—` | 1.738 us | 0.275 us | 0.323 us | 16.90x | 7,184 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.198 us | 0.087 us | 2.27x | 2.26–2.29x | +0.111 us | 9 pairs; 41/41 ms; 209,158/466,343 ops/lane; IQR 1.003/1.011 | `Pcre2Character` | 0 B | 0 B | 2.000 us | 2,696 B |
| `pcre2/branch-reset-backref` | **Inconclusive** | 23 B | 0.013 us | 0.123 us | 0.10x | 0.10–0.11x | -0.111 us | 9 pairs; 40/38 ms; 3,129,579/307,806 ops/lane; IQR 1.018/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 128.131 us | 111,248 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.599 us | 0.107 us | 5.63x | 5.59–5.73x | +0.493 us | 9 pairs; 38/41 ms; 63,528/379,724 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 20.113 us | 18,568 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.565 us | 0.118 us | 4.80x | 4.76–4.83x | +0.448 us | 9 pairs; 38/42 ms; 66,743/352,711 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 67.800 us | 64,672 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.583 us | 0.105 us | 5.52x | 5.50–5.60x | +0.478 us | 9 pairs; 38/40 ms; 65,091/382,971 ops/lane; IQR 1.002/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 68.963 us | 64,632 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.638 us | 0.347 us | 1.84x | 1.84–1.85x | +0.292 us | 9 pairs; 37/40 ms; 58,791/114,401 ops/lane; IQR 1.004/1.006 | `Pcre2Character` | 0 B | 0 B | 1.225 us | 2,232 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.633 us | 0.02x | 0.02–0.02x | -0.622 us | 9 pairs; 40/40 ms; 3,494,943/63,312 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 21.350 us | 21,360 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.509 us | 0.02x | 0.02–0.02x | -0.498 us | 9 pairs; 40/39 ms; 3,450,928/77,469 ops/lane; IQR 1.015/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 21.381 us | 20,848 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.598 us | 0.110 us | 5.46x | 5.37–5.52x | +0.487 us | 9 pairs; 33/39 ms; 54,909/357,785 ops/lane; IQR 1.011/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 18.694 us | 13,656 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.284 us | 11.056 us | 0.75x | 0.73–0.75x | -2.776 us | 9 pairs; 40/41 ms; 4,836/3,746 ops/lane; IQR 1.002/1.020 | `Pcre2Literal` | 0 B | 0 B | 0.675 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.334 us | 1.051 us | 0.32x | 0.32–0.32x | -0.718 us | 9 pairs; 39/40 ms; 117,235/37,604 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 127.162 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.342 us | 0.467 us | 0.73x | 0.72–0.74x | -0.126 us | 9 pairs; 37/41 ms; 109,343/87,570 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 110.875 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.320 us | 0.490 us | 0.65x | 0.64–0.66x | -0.171 us | 9 pairs; 38/39 ms; 118,662/80,377 ops/lane; IQR 1.007/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 135.637 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.368 us | 0.590 us | 0.62x | 0.58–0.63x | -0.224 us | 9 pairs; 41/39 ms; 112,409/66,402 ops/lane; IQR 1.052/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 100.475 us | 80,768 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.323 us | 0.372 us | 0.87x | 0.86–0.88x | -0.049 us | 9 pairs; 38/39 ms; 119,260/105,206 ops/lane; IQR 1.008/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 214.044 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.128 us | 8.954 us | 0.57x | 0.57–0.58x | -3.834 us | 9 pairs; 40/40 ms; 7,810/4,517 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 0 B | 0 B | 1.688 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.293 us | 0.315 us | 0.93x | 0.92–0.93x | -0.022 us | 9 pairs; 37/37 ms; 127,188/118,336 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 89.050 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.302 us | 0.400 us | 0.75x | 0.75–0.76x | -0.099 us | 9 pairs; 36/39 ms; 120,933/97,025 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 119.737 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.119 us | — | — | — | — | — | `—` | 0 B | — | 14.031 us | 11,256 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.217 us | 0.287 us | 0.76x | 0.74–0.76x | -0.070 us | 9 pairs; 35/39 ms; 162,306/137,167 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.256 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.988 us | — | — | — | — | — | `—` | 0 B | — | 13.562 us | 10,296 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.608 us | 0.674 us | 0.90x | 0.90–0.91x | -0.066 us | 9 pairs; 39/40 ms; 64,761/59,231 ops/lane; IQR 1.007/1.011 | `Pcre2Slice` | 0 B | 0 B | 61.362 us | 55,256 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.865 us | 28.981 us | 0.34x | 0.34–0.34x | -19.119 us | 9 pairs; 37/40 ms; 3,762/1,387 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.694 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.952 us | 1.063 us | 0.89x | 0.89–0.90x | -0.113 us | 9 pairs; 40/30 ms; 41,972/28,252 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 128.162 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.024 us | 1.193 us | 0.86x | 0.85–0.86x | -0.172 us | 9 pairs; 40/40 ms; 39,162/33,210 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 110.662 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.934 us | 1.028 us | 0.91x | 0.91–0.91x | -0.090 us | 9 pairs; 40/39 ms; 42,369/38,053 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 136.031 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.995 us | 1.171 us | 0.85x | 0.85–0.85x | -0.177 us | 9 pairs; 41/40 ms; 41,318/34,077 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 100.406 us | 80,768 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 1.020 us | 0.972 us | 1.05x | 1.05–1.05x | +0.049 us | 9 pairs; 42/40 ms; 41,403/40,709 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 216.319 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.885 us | 8.961 us | 0.66x | 0.65–0.66x | -3.091 us | 9 pairs; 40/41 ms; 6,840/4,562 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0 B | 0 B | 1.188 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.923 us | 0.890 us | 1.04x | 1.04–1.04x | +0.034 us | 9 pairs; 40/40 ms; 43,537/44,998 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 92.119 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.930 us | 0.997 us | 0.93x | 0.93–0.93x | -0.069 us | 9 pairs; 40/40 ms; 43,487/40,466 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 121.019 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.291 us | — | — | — | — | — | `—` | 0 B | — | 13.488 us | 11,256 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.827 us | 0.865 us | 0.96x | 0.95–0.96x | -0.038 us | 9 pairs; 40/40 ms; 47,981/46,473 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 52.994 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.164 us | — | — | — | — | — | `—` | 0 B | — | 13.725 us | 10,296 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.868 us | 1.253 us | 0.69x | 0.69–0.69x | -0.386 us | 9 pairs; 40/40 ms; 46,531/32,163 ops/lane; IQR 1.001/1.004 | `Pcre2Slice` | 0 B | 0 B | 61.225 us | 55,256 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.328 us | 2.290 us | 0.58x | 0.58–0.58x | -0.962 us | 9 pairs; 40/40 ms; 30,099/17,481 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.656 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.625 us | 1.060 us | 0.59x | 0.59–0.59x | -0.435 us | 9 pairs; 39/40 ms; 61,781/37,604 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 126.894 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.679 us | 1.201 us | 0.57x | 0.56–0.57x | -0.520 us | 9 pairs; 39/41 ms; 58,053/34,054 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 111.125 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.616 us | 1.031 us | 0.60x | 0.60–0.60x | -0.415 us | 9 pairs; 39/41 ms; 63,788/39,383 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 137.106 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.628 us | 1.170 us | 0.54x | 0.54–0.54x | -0.542 us | 9 pairs; 38/39 ms; 60,502/33,472 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 100.356 us | 80,768 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.649 us | 0.969 us | 0.67x | 0.67–0.67x | -0.320 us | 9 pairs; 38/40 ms; 58,386/40,910 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 210.900 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.836 us | 2.212 us | 0.83x | 0.83–0.83x | -0.375 us | 9 pairs; 36/41 ms; 19,700/18,440 ops/lane; IQR 1.014/1.011 | `Pcre2Character` | 0 B | 0 B | 1.219 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.602 us | 0.886 us | 0.68x | 0.68–0.68x | -0.285 us | 9 pairs; 39/40 ms; 65,549/45,333 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 88.669 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.615 us | 0.967 us | 0.64x | 0.63–0.64x | -0.351 us | 9 pairs; 40/40 ms; 64,715/41,142 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 121.550 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.002 us | — | — | — | — | — | `—` | 0 B | — | 14.869 us | 11,256 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.520 us | 0.865 us | 0.60x | 0.60–0.61x | -0.346 us | 9 pairs; 41/40 ms; 78,963/45,830 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 52.975 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.882 us | — | — | — | — | — | `—` | 0 B | — | 13.425 us | 10,296 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.554 us | 1.254 us | 0.44x | 0.44–0.44x | -0.700 us | 9 pairs; 42/40 ms; 75,481/31,738 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | 0 B | 0 B | 61.237 us | 55,256 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.066 us | — | — | — | — | — | `—` | 480 B | — | 0.669 us | 1,464 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.039 us | — | — | — | — | — | `—` | 48 B | — | 128.906 us | 111,248 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.157 us | — | — | — | — | — | `—` | 48 B | — | 111.919 us | 98,592 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.051 us | — | — | — | — | — | `—` | 48 B | — | 136.988 us | 126,728 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.040 us | — | — | — | — | — | `—` | 48 B | — | 98.094 us | 80,768 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.051 us | — | — | — | — | — | `—` | 48 B | — | 211.019 us | 177,832 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.379 us | — | — | — | — | — | `—` | 64 B | — | 1.188 us | 2,232 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.063 us | — | — | — | — | — | `—` | 40 B | — | 89.963 us | 71,768 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.058 us | — | — | — | — | — | `—` | 40 B | — | 120.975 us | 109,952 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.000 us | — | — | — | — | — | `—` | 1,216 B | — | 13.394 us | 11,256 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 0.998 us | — | — | — | — | — | `—` | 48 B | — | 53.256 us | 53,160 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.475 us | — | — | — | — | — | `—` | 928 B | — | 13.669 us | 10,296 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 9.980 us | — | — | — | — | — | `—` | 28,928 B | — | 61.481 us | 55,256 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 48.399–187.180 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.005–90.346 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.543–12.532 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 13.402–48.623 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.024–1.466 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 4.856–37.688 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.884–23.502 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.288–101.244 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.130–72.694 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.045–5.539 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 64.092–377.436 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.128–0.193 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 77.855–308.681 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.819–5.031 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 29.017–111.196 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 323.824–1,272.985 us |

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
