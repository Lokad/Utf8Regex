<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `7D50E43165567968AE510367E13FBFD9F3B2BA31B17413F79DBB6C1D294DD265`
- Latest managed row measurement: `2026-11-04T15:40:11.1455788+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T15:40:11.1455788+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `80` managed faster, `0` equivalent, `10` native faster, `11` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `e731a4526f4b` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `e731a4526f4b` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.944 us | 2.149 us | 0.44x | 0.43–0.46x | -1.199 us | 9 pairs; 36/30 ms; 37,642/14,185 ops/lane; IQR 1.021/1.030 | `Pcre2Slice` | 556.238 us | 0.280 us | 0.838 us | 1.13x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.142 us | 13.033 us | 0.09x | 0.08–0.10x | -11.899 us | 9 pairs; 36/38 ms; 31,655/2,903 ops/lane; IQR 1.019/1.177 | `Pcre2Slice` | 0.808 us | 0.098 us | 3.495 us | 0.33x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.244 us | 0.01x | 0.01–0.01x | -1.233 us | 9 pairs; 40/40 ms; 3,690,783/31,973 ops/lane; IQR 1.005/1.001 | `Pcre2Slice` | 0.104 us | 1.252 us | 1.286 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.694 us | 0.161 us | 4.32x | 4.28–4.34x | +0.533 us | 9 pairs; 38/41 ms; 54,403/256,627 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0.046 us | 0.123 us | 0.165 us | 4.21x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.473 us | 0.577 us | 4.30x | 4.03–4.31x | +1.898 us | 9 pairs; 42/41 ms; 16,851/70,758 ops/lane; IQR 1.038/1.005 | `Pcre2Backtracking` | 0.115 us | 0.775 us | 0.802 us | 3.09x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.304 us | 0.33x | 0.33–0.33x | -0.204 us | 9 pairs; 39/40 ms; 385,904/131,265 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 0.111 us | 0.324 us | 0.351 us | 0.28x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.108 us | 0.360 us | 0.30x | 0.29–0.31x | -0.251 us | 9 pairs; 41/40 ms; 378,563/111,708 ops/lane; IQR 1.028/1.024 | `Pcre2Slice` | 0.114 us | 0.457 us | 0.479 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.103 us | 0.289 us | 0.36x | 0.27–0.36x | -0.186 us | 9 pairs; 54/39 ms; 521,996/135,559 ops/lane; IQR 1.062/1.011 | `Pcre2Slice` | 0.088 us | 0.241 us | 0.262 us | 0.39x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.080 us | 0.314 us | 0.25x | 0.25–0.26x | -0.234 us | 9 pairs; 41/41 ms; 519,428/131,948 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 0.088 us | 0.258 us | 0.277 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.173 us | 0.079 us | 2.16x | 2.12–2.27x | +0.093 us | 9 pairs; 39/40 ms; 228,054/509,353 ops/lane; IQR 1.006/1.025 | `Pcre2Slice` | 0.087 us | 1.035 us | 1.074 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.198 us | 0.17x | 0.17–0.17x | -0.164 us | 9 pairs; 40/39 ms; 1,190,698/199,778 ops/lane; IQR 1.002/1.014 | `Pcre2Backtracking` | 0.109 us | 0.313 us | 0.357 us | 0.09x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.613 us | 0.10x | 0.10–0.10x | -0.552 us | 9 pairs; 40/41 ms; 663,229/66,094 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0.131 us | 0.242 us | 0.262 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.158 us | 0.21x | 0.21–0.22x | -0.125 us | 9 pairs; 37/41 ms; 1,112,720/257,831 ops/lane; IQR 1.025/1.008 | `Pcre2Backtracking` | 0.107 us | 0.189 us | 0.203 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.022 us | 0.114 us | 0.19x | 0.18–0.19x | -0.093 us | 9 pairs; 43/40 ms; 1,947,867/351,847 ops/lane; IQR 1.053/1.012 | `Pcre2Backtracking` | 0.059 us | 0.092 us | 0.111 us | 0.20x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Inconclusive** | 8 B | 0.058 us | 0.116 us | 0.50x | 0.49–0.51x | -0.058 us | 9 pairs; 40/43 ms; 689,665/369,373 ops/lane; IQR 1.007/1.020 | `Pcre2Backtracking` | 0.016 us | 0.091 us | 0.114 us | 0.51x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.089 us | 0.278 us | 0.32x | 0.31–0.35x | -0.187 us | 9 pairs; 39/41 ms; 435,141/146,390 ops/lane; IQR 1.059/1.013 | `Pcre2Slice` | 0.094 us | 0.118 us | 0.151 us | 0.59x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.115 us | 0.22x | 0.21–0.22x | -0.090 us | 9 pairs; 42/40 ms; 1,663,403/350,621 ops/lane; IQR 1.032/1.010 | `Pcre2Backtracking` | 0.019 us | 0.097 us | 0.114 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Managed faster** | 4,103 B | 0.118 us | 2.226 us | 0.05x | 0.05–0.06x | -2.108 us | 9 pairs; 40/57 ms; 340,340/25,545 ops/lane; IQR 1.006/1.068 | `Pcre2Literal` | 0.088 us | 0.051 us | 0.616 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Managed faster** | 4,103 B | 0.121 us | 1.902 us | 0.06x | 0.06–0.07x | -1.781 us | 9 pairs; 41/34 ms; 337,585/18,069 ops/lane; IQR 1.002/1.058 | `Pcre2Literal` | 0.145 us | 0.053 us | 0.659 us | 0.18x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.145 us | 2.239 us | 0.06x | 0.06–0.08x | -2.094 us | 9 pairs; 40/47 ms; 274,690/21,177 ops/lane; IQR 1.048/1.231 | `Pcre2Literal` | 0.202 us | 0.234 us | 0.828 us | 0.17x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.153 us | 1.849 us | 0.08x | 0.08–0.09x | -1.699 us | 9 pairs; 40/35 ms; 261,038/18,805 ops/lane; IQR 1.007/1.083 | `Pcre2Literal` | 0.170 us | 0.206 us | 0.772 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.098 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 39/40 ms; 4,500,906/408,095 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 0.272 us | 0.071 us | 0.092 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.086 us | 0.78x | 0.78–0.79x | -0.019 us | 9 pairs; 40/41 ms; 595,182/473,429 ops/lane; IQR 1.005/1.007 | `Pcre2Literal` | 0.126 us | 0.051 us | 0.073 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.12–0.13x | -0.094 us | 9 pairs; 41/38 ms; 2,881,548/355,567 ops/lane; IQR 1.014/1.014 | `Pcre2Backtracking` | 0.237 us | 0.092 us | 0.113 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.116 us | 0.121 us | 0.96x | 0.95–0.97x | -0.005 us | 9 pairs; 41/39 ms; 350,597/324,921 ops/lane; IQR 1.003/1.009 | `Pcre2Character` | 0.133 us | 0.113 us | 0.144 us | 0.81x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.201 us | 0.38x | 0.38–0.38x | -0.124 us | 9 pairs; 40/39 ms; 529,018/195,042 ops/lane; IQR 1.004/1.015 | `Pcre2Slice` | 0.455 us | 0.075 us | 0.110 us | 0.69x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.595 us | 159.583 us | 0.52x | 0.51–0.52x | -77.418 us | 9 pairs; 40/40 ms; 483/251 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 384.319 us | 107.240 us | 109.483 us | 0.75x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.782 us | 283.237 us | 0.13x | 0.13–0.13x | -245.383 us | 9 pairs; 40/40 ms; 1,053/141 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 393.241 us | 217.602 us | 228.780 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.484 us | 211.921 us | 0.04x | 0.04–0.05x | -202.467 us | 9 pairs; 40/41 ms; 4,211/194 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 7.977 us | 229.181 us | 228.244 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 239.183 us | 205.466 us | 1.16x | 1.16–1.18x | +33.531 us | 9 pairs; 41/39 ms; 172/190 ops/lane; IQR 1.005/1.008 | `Pcre2SingleTokenRepeat` | 7.977 us | 239.279 us | 239.903 us | 1.00x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.554 us | 23.912 us | 0.15x | 0.15–0.15x | -20.348 us | 9 pairs; 40/40 ms; 11,143/1,675 ops/lane; IQR 1.004/1.011 | `Pcre2Literal` | 1.383 us | 4.275 us | 6.105 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.656 us | 81.172 us | 0.12x | 0.12–0.12x | -71.509 us | 9 pairs; 40/41 ms; 4,146/500 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 9.344 us | 92.260 us | 94.879 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,188.680 us | 28,573.200 us | 0.15x | 0.14–0.16x | -24388.950 us | 9 pairs; 42/29 ms; 10/1 ops/lane; IQR 1.007/1.056 | `Pcre2Backtracking` | 5,372.060 us | 12,691.750 us | 19,687.633 us | 0.21x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,544.333 us | 55,191.800 us | 0.06x | 0.06–0.07x | -51608.542 us | 9 pairs; 43/55 ms; 12/1 ops/lane; IQR 1.040/1.011 | `Pcre2Backtracking` | 3,914.108 us | 44,656.700 us | 51,910.300 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,944.214 us | 485,637.400 us | 0.00x | 0.00–0.00x | -483729.986 us | 9 pairs; 41/486 ms; 21/1 ops/lane; IQR 1.037/1.003 | `Pcre2Character` | 1,882.921 us | 23,147.500 us | 30,240.000 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,551.817 us | 10,851.350 us | 0.51x | 0.48–0.58x | -5273.783 us | 9 pairs; 33/43 ms; 6/4 ops/lane; IQR 1.006/1.141 | `Pcre2Literal` | 1,394.230 us | 1,394.915 us | 8,607.017 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,695.950 us | 145,962.300 us | 0.13x | 0.13–0.13x | -127274.700 us | 9 pairs; 37/146 ms; 2/1 ops/lane; IQR 1.011/1.002 | `Pcre2Backtracking` | 1,287.713 us | 535.094 us | 8,659.100 us | 2.16x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,817.012 us | 21,987.850 us | 0.22x | 0.22–0.22x | -17163.838 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.004/1.013 | `Pcre2Slice` | 2,509.905 us | 11,613.520 us | 19,640.667 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,636.950 us | 134,403.400 us | 0.17x | 0.17–0.17x | -111670.950 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.016/1.006 | `Pcre2Backtracking` | 1,559.312 us | 2,349.182 us | 10,568.380 us | 2.14x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 181.827 us | 974.120 us | 0.19x | 0.18–0.19x | -792.863 us | 9 pairs; 39/40 ms; 215/41 ops/lane; IQR 1.003/1.041 | `Pcre2Backtracking` | 213.577 us | 353.245 us | 568.111 us | 0.32x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,522.000 us | 13,459.200 us | 0.93x | 0.93–0.94x | -964.200 us | 9 pairs; 38/40 ms; 3/3 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 309.518 us | 16,047.400 us | 16,372.875 us | 0.76x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 3,019.831 us | 98,545.900 us | 0.03x | 0.03–0.03x | -95525.792 us | 9 pairs; 39/99 ms; 13/1 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 2,027.380 us | 19,873.400 us | 20,132.167 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.186 us | 2,768.607 us | 0.07x | 0.07–0.07x | -2583.218 us | 9 pairs; 39/39 ms; 213/14 ops/lane; IQR 1.007/1.020 | `Pcre2SingleTokenRepeat` | 190.375 us | 1,004.720 us | 1,424.529 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,581.868 us | 20,114.450 us | 0.08x | 0.08–0.08x | -18544.744 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 590.862 us | 10,949.200 us | 11,042.620 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.135 us | 1.092 us | 0.12x | 0.12–0.12x | -0.957 us | 9 pairs; 40/40 ms; 297,214/36,523 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 0.747 us | 0.261 us | 0.284 us | 0.48x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.488 us | 0.827 us | 0.59x | 0.59–0.59x | -0.340 us | 9 pairs; 40/39 ms; 81,325/47,494 ops/lane; IQR 1.003/1.004 | `Pcre2Literal` | 0.142 us | 0.150 us | 0.174 us | 2.81x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.297 us | 1.216 us | 0.24x | 0.24–0.25x | -0.919 us | 9 pairs; 40/39 ms; 134,459/32,365 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 1.073 us | 0.349 us | 0.375 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.853 us | 1.134 us | 0.75x | 0.75–0.76x | -0.280 us | 9 pairs; 38/40 ms; 44,646/35,292 ops/lane; IQR 1.005/1.005 | `Pcre2Character` | 0.281 us | 0.432 us | 0.473 us | 1.80x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.157 us | 0.854 us | 0.18x | 0.18–0.19x | -0.696 us | 9 pairs; 40/40 ms; 256,040/46,990 ops/lane; IQR 1.017/1.005 | `Pcre2Slice` | 0.876 us | 0.155 us | 0.200 us | 0.78x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.954 us | 1.107 us | 0.86x | 0.86–0.86x | -0.153 us | 9 pairs; 40/40 ms; 41,782/36,034 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 3.813 us | 0.308 us | 0.326 us | 2.93x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.791 us | 0.831 us | 0.95x | 0.95–0.96x | -0.039 us | 9 pairs; 40/40 ms; 50,357/47,630 ops/lane; IQR 1.003/1.005 | `Pcre2Literal` | 3.581 us | 0.182 us | 0.203 us | 3.89x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.982 us | 1.188 us | 0.83x | 0.82–0.83x | -0.207 us | 9 pairs; 40/40 ms; 40,625/33,444 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 5.304 us | 0.406 us | 0.430 us | 2.29x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.249 us | 1.143 us | 1.09x | 1.09–1.10x | +0.106 us | 9 pairs; 41/40 ms; 33,019/35,058 ops/lane; IQR 1.006/1.004 | `Pcre2Character` | 3.690 us | 0.482 us | 0.520 us | 2.40x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.802 us | 0.859 us | 0.94x | 0.93–0.94x | -0.055 us | 9 pairs; 40/40 ms; 50,110/46,530 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 2.157 us | 0.183 us | 0.226 us | 3.55x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.591 us | 1.103 us | 0.54x | 0.53–0.54x | -0.511 us | 9 pairs; 41/40 ms; 69,489/36,015 ops/lane; IQR 1.017/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.542 us | 0.828 us | 0.65x | 0.65–0.66x | -0.287 us | 9 pairs; 40/40 ms; 73,862/47,895 ops/lane; IQR 1.007/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.691 us | 1.189 us | 0.58x | 0.58–0.58x | -0.498 us | 9 pairs; 40/40 ms; 58,003/33,738 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.916 us | 1.143 us | 0.80x | 0.80–0.81x | -0.229 us | 9 pairs; 41/40 ms; 45,148/34,794 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.488 us | 0.857 us | 0.57x | 0.57–0.57x | -0.368 us | 9 pairs; 40/40 ms; 81,850/46,849 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.922 us | — | — | — | — | — | `—` | 25.778 us | 100.026 us | 101.507 us | 0.60x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.632 us | — | — | — | — | — | `—` | 1.845 us | 0.370 us | 0.396 us | 16.76x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.031 us | — | — | — | — | — | `—` | 0.920 us | 0.246 us | 0.275 us | 3.75x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.205 us | — | — | — | — | — | `—` | 3.107 us | 0.464 us | 0.513 us | 2.35x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.473 us | — | — | — | — | — | `—` | 1.026 us | 0.544 us | 0.597 us | 2.47x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.449 us | — | — | — | — | — | `—` | 1.632 us | 0.275 us | 0.327 us | 13.60x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.089 us | 0.085 us | 1.05x | 1.03–1.06x | +0.004 us | 9 pairs; 35/41 ms; 388,135/475,772 ops/lane; IQR 1.007/1.009 | `Pcre2Character` | 0 B | 0 B | 2.275 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.122 us | 0.11x | 0.10–0.11x | -0.109 us | 9 pairs; 26/30 ms; 2,015,795/243,592 ops/lane; IQR 1.017/1.077 | `Pcre2Backtracking` | 0 B | 0 B | 130.675 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.395 us | 0.104 us | 3.82x | 3.67–3.84x | +0.292 us | 9 pairs; 35/39 ms; 88,409/372,861 ops/lane; IQR 1.022/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 20.400 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.381 us | 0.116 us | 3.27x | 3.23–3.33x | +0.264 us | 9 pairs; 35/41 ms; 92,124/357,841 ops/lane; IQR 1.010/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 70.775 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.376 us | 0.103 us | 3.63x | 3.60–3.66x | +0.273 us | 9 pairs; 36/39 ms; 95,411/379,423 ops/lane; IQR 1.008/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 69.619 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.583 us | 0.344 us | 1.69x | 1.69–1.71x | +0.239 us | 9 pairs; 38/40 ms; 65,562/115,099 ops/lane; IQR 1.003/1.007 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.646 us | 0.02x | 0.02–0.02x | -0.634 us | 9 pairs; 40/40 ms; 3,474,560/61,287 ops/lane; IQR 1.011/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 21.750 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.507 us | 0.02x | 0.02–0.02x | -0.495 us | 9 pairs; 40/40 ms; 3,388,625/78,449 ops/lane; IQR 1.024/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 22.350 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.463 us | 0.108 us | 4.29x | 4.25–4.35x | +0.354 us | 9 pairs; 37/39 ms; 78,879/365,703 ops/lane; IQR 1.017/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 18.619 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.524 us | 10.648 us | 0.80x | 0.80–0.80x | -2.137 us | 9 pairs; 40/40 ms; 4,663/3,785 ops/lane; IQR 1.007/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.781 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.356 us | 1.139 us | 0.31x | 0.31–0.31x | -0.783 us | 9 pairs; 48/40 ms; 135,674/35,480 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 134.200 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.317 us | 0.451 us | 0.70x | 0.70–0.71x | -0.134 us | 9 pairs; 35/42 ms; 111,527/93,872 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 113.862 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.294 us | 0.468 us | 0.63x | 0.62–0.64x | -0.173 us | 9 pairs; 36/40 ms; 121,675/86,098 ops/lane; IQR 1.002/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 141.369 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.323 us | 0.587 us | 0.55x | 0.55–0.60x | -0.262 us | 9 pairs; 40/39 ms; 123,592/65,667 ops/lane; IQR 1.054/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 105.419 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.297 us | 0.404 us | 0.73x | 0.73–0.74x | -0.107 us | 9 pairs; 39/39 ms; 130,725/97,533 ops/lane; IQR 1.002/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 214.456 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.853 us | 8.875 us | 0.55x | 0.55–0.55x | -4.015 us | 9 pairs; 40/40 ms; 8,247/4,495 ops/lane; IQR 1.001/1.003 | `Pcre2Character` | 0 B | 0 B | 1.500 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.255 us | 0.309 us | 0.83x | 0.82–0.83x | -0.053 us | 9 pairs; 36/40 ms; 139,083/128,348 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 91.487 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.271 us | 0.411 us | 0.66x | 0.66–0.66x | -0.140 us | 9 pairs; 33/39 ms; 121,285/95,616 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 122.425 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.010 us | — | — | — | — | — | `—` | 0 B | — | 14.725 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.194 us | 0.298 us | 0.65x | 0.64–0.66x | -0.103 us | 9 pairs; 33/40 ms; 168,751/132,747 ops/lane; IQR 1.013/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 52.625 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.897 us | — | — | — | — | — | `—` | 0 B | — | 13.794 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.578 us | 0.706 us | 0.82x | 0.82–0.84x | -0.125 us | 9 pairs; 37/40 ms; 63,739/57,016 ops/lane; IQR 1.010/1.028 | `Pcre2Slice` | 0 B | 0 B | 63.663 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.861 us | 29.017 us | 0.34x | 0.34–0.34x | -19.153 us | 9 pairs; 39/40 ms; 3,964/1,378 ops/lane; IQR 1.001/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.945 us | 1.154 us | 0.82x | 0.82–0.82x | -0.209 us | 9 pairs; 40/40 ms; 41,851/34,836 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 127.200 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.014 us | 1.188 us | 0.85x | 0.85–0.86x | -0.175 us | 9 pairs; 41/40 ms; 40,299/33,708 ops/lane; IQR 1.003/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 112.181 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.952 us | 1.037 us | 0.92x | 0.92–0.93x | -0.086 us | 9 pairs; 41/40 ms; 42,823/38,612 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 141.975 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.953 us | 1.172 us | 0.81x | 0.81–0.82x | -0.219 us | 9 pairs; 40/40 ms; 41,473/34,364 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 103.650 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.939 us | 0.976 us | 0.96x | 0.96–0.97x | -0.036 us | 9 pairs; 39/41 ms; 41,016/41,728 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 213.156 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.554 us | 8.883 us | 0.63x | 0.62–0.63x | -3.330 us | 9 pairs; 40/40 ms; 7,227/4,483 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.926 us | 0.890 us | 1.04x | 1.04–1.04x | +0.035 us | 9 pairs; 41/39 ms; 44,182/44,155 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 91.831 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.918 us | 0.967 us | 0.95x | 0.94–0.95x | -0.051 us | 9 pairs; 40/40 ms; 43,080/41,345 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 121.725 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.273 us | — | — | — | — | — | `—` | 0 B | — | 13.450 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.839 us | 0.868 us | 0.97x | 0.97–0.97x | -0.030 us | 9 pairs; 40/40 ms; 48,266/45,626 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.469 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.131 us | — | — | — | — | — | `—` | 0 B | — | 15.519 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.913 us | 1.270 us | 0.72x | 0.72–0.72x | -0.357 us | 9 pairs; 39/41 ms; 42,457/32,058 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.619 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.364 us | 2.295 us | 0.60x | 0.59–0.60x | -0.928 us | 9 pairs; 40/40 ms; 29,355/17,481 ops/lane; IQR 1.002/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.636 us | 1.154 us | 0.55x | 0.55–0.55x | -0.518 us | 9 pairs; 41/40 ms; 64,248/34,633 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 129.106 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.689 us | 1.187 us | 0.58x | 0.58–0.58x | -0.499 us | 9 pairs; 40/39 ms; 58,256/32,930 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 111.775 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.694 us | 1.055 us | 0.66x | 0.65–0.66x | -0.361 us | 9 pairs; 42/40 ms; 59,801/38,262 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 139.375 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.630 us | 1.167 us | 0.54x | 0.54–0.54x | -0.536 us | 9 pairs; 40/40 ms; 63,344/34,268 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.219 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.664 us | 0.973 us | 0.68x | 0.68–0.68x | -0.310 us | 9 pairs; 40/41 ms; 59,720/41,927 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 218.137 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.829 us | 2.193 us | 0.83x | 0.83–0.84x | -0.363 us | 9 pairs; 40/40 ms; 21,965/18,122 ops/lane; IQR 1.001/1.004 | `Pcre2Character` | 0 B | 0 B | 1.381 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.608 us | 0.892 us | 0.68x | 0.68–0.68x | -0.285 us | 9 pairs; 39/40 ms; 64,512/44,825 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 90.088 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.669 us | 0.972 us | 0.69x | 0.69–0.69x | -0.303 us | 9 pairs; 42/40 ms; 63,028/41,020 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 125.325 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.977 us | — | — | — | — | — | `—` | 0 B | — | 13.231 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.491 us | 0.866 us | 0.57x | 0.56–0.57x | -0.376 us | 9 pairs; 39/40 ms; 79,704/46,235 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 52.631 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.828 us | — | — | — | — | — | `—` | 0 B | — | 13.938 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.533 us | 1.284 us | 0.42x | 0.41–0.42x | -0.751 us | 9 pairs; 40/41 ms; 75,277/31,649 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | 0 B | 0 B | 64.306 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.404 us | — | — | — | — | — | `—` | 480 B | — | 0.769 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.121 us | — | — | — | — | — | `—` | 48 B | — | 129.594 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.237 us | — | — | — | — | — | `—` | 48 B | — | 116.713 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.105 us | — | — | — | — | — | `—` | 48 B | — | 140.881 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.111 us | — | — | — | — | — | `—` | 48 B | — | 98.412 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.097 us | — | — | — | — | — | `—` | 48 B | — | 223.175 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.720 us | — | — | — | — | — | `—` | 64 B | — | 1.369 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.122 us | — | — | — | — | — | `—` | 40 B | — | 91.325 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.125 us | — | — | — | — | — | `—` | 40 B | — | 122.381 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.539 us | — | — | — | — | — | `—` | 1,216 B | — | 13.256 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.053 us | — | — | — | — | — | `—` | 48 B | — | 52.250 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 17.046 us | — | — | — | — | — | `—` | 928 B | — | 13.406 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 6.003 us | — | — | — | — | — | `—` | 28,928 B | — | 63.094 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 27.488–219.858 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.336–94.723 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.539–12.751 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.102–47.253 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.036–1.322 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.514–42.236 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.188–23.921 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.180–99.689 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.148–72.599 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.187–5.443 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.557–379.472 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.070–0.085 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.270–306.273 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.837–5.132 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.204–15.903 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 179.285–1,352.754 us |

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
