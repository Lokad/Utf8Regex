<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `1E98A6A7FA4985EFA5C79627827E905DA59F915D751873C7243150C40273BEFB`
- Latest managed row measurement: `2026-11-04T20:50:47.1218104+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T20:50:47.1218104+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `41/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `79` managed faster, `0` equivalent, `13` native faster, `9` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `ba457088fc45` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `ba457088fc45` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.898 us | 2.149 us | 0.42x | 0.38–0.44x | -1.215 us | 9 pairs; 35/40 ms; 38,788/18,668 ops/lane; IQR 1.057/1.165 | `Pcre2Slice` | 546.137 us | 0.275 us | 0.778 us | 1.15x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Managed faster** | 22,528 B | 1.106 us | 11.221 us | 0.10x | 0.09–0.10x | -10.124 us | 9 pairs; 36/38 ms; 32,790/3,421 ops/lane; IQR 1.008/1.028 | `Pcre2Slice` | 0.795 us | 0.095 us | 3.364 us | 0.33x | 2,256 B | 0 B |
| `common/backtracking` | **Inconclusive** | 49 B | 0.016 us | 1.942 us | 0.01x | 0.01–0.01x | -1.926 us | 9 pairs; 61/44 ms; 3,731,941/22,735 ops/lane; IQR 1.215/1.007 | `Pcre2Slice` | 0.101 us | 1.037 us | 1.090 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.676 us | 0.155 us | 4.34x | 4.29–4.37x | +0.519 us | 9 pairs; 37/43 ms; 54,941/276,952 ops/lane; IQR 1.011/1.019 | `Pcre2Backtracking` | 0.048 us | 0.119 us | 0.159 us | 4.24x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.244 us | 0.557 us | 4.04x | 4.01–4.05x | +1.688 us | 9 pairs; 39/42 ms; 17,310/75,607 ops/lane; IQR 1.012/1.007 | `Pcre2Backtracking` | 0.128 us | 0.710 us | 0.748 us | 3.00x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.098 us | 0.290 us | 0.34x | 0.34–0.34x | -0.192 us | 9 pairs; 39/41 ms; 396,170/141,040 ops/lane; IQR 1.010/1.013 | `Pcre2Slice` | 0.110 us | 0.307 us | 0.339 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.104 us | 0.344 us | 0.30x | 0.30–0.30x | -0.241 us | 9 pairs; 40/40 ms; 387,818/115,772 ops/lane; IQR 1.004/1.020 | `Pcre2Slice` | 0.112 us | 0.437 us | 0.446 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.079 us | 0.282 us | 0.28x | 0.28–0.28x | -0.202 us | 9 pairs; 40/42 ms; 511,519/147,505 ops/lane; IQR 1.012/1.012 | `Pcre2Slice` | 0.085 us | 0.224 us | 0.248 us | 0.32x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.079 us | 0.303 us | 0.26x | 0.26–0.26x | -0.224 us | 9 pairs; 41/40 ms; 512,490/130,404 ops/lane; IQR 1.003/1.011 | `Pcre2Slice` | 0.084 us | 0.238 us | 0.260 us | 0.30x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.170 us | 0.076 us | 2.24x | 2.17–2.32x | +0.094 us | 9 pairs; 40/39 ms; 236,365/516,287 ops/lane; IQR 1.007/1.032 | `Pcre2Slice` | 0.083 us | 0.883 us | 0.916 us | 0.19x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.196 us | 0.17x | 0.17–0.17x | -0.163 us | 9 pairs; 39/40 ms; 1,189,679/204,769 ops/lane; IQR 1.019/1.016 | `Pcre2Backtracking` | 0.104 us | 0.289 us | 0.330 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.059 us | 0.591 us | 0.10x | 0.10–0.10x | -0.532 us | 9 pairs; 40/38 ms; 668,935/64,233 ops/lane; IQR 1.007/1.011 | `Pcre2Backtracking` | 0.128 us | 0.221 us | 0.238 us | 0.25x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.21x | 0.21–0.22x | -0.120 us | 9 pairs; 38/40 ms; 1,153,241/258,556 ops/lane; IQR 1.031/1.011 | `Pcre2Backtracking` | 0.104 us | 0.181 us | 0.200 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.109 us | 0.19x | 0.18–0.19x | -0.089 us | 9 pairs; 42/40 ms; 2,037,415/371,104 ops/lane; IQR 1.017/1.015 | `Pcre2Backtracking` | 0.057 us | 0.088 us | 0.106 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.056 us | 0.114 us | 0.49x | 0.48–0.51x | -0.058 us | 9 pairs; 39/38 ms; 697,148/333,519 ops/lane; IQR 1.011/1.017 | `Pcre2Backtracking` | 0.016 us | 0.082 us | 0.099 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.083 us | 0.273 us | 0.30x | 0.30–0.31x | -0.190 us | 9 pairs; 41/41 ms; 493,474/149,353 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | 0.091 us | 0.115 us | 0.150 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.113 us | 0.21x | 0.20–0.22x | -0.089 us | 9 pairs; 43/40 ms; 1,792,621/355,237 ops/lane; IQR 1.032/1.018 | `Pcre2Backtracking` | 0.018 us | 0.086 us | 0.104 us | 0.23x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.114 us | 2.029 us | 0.06x | 0.05–0.07x | -1.914 us | 9 pairs; 40/32 ms; 345,671/15,991 ops/lane; IQR 1.004/1.076 | `Pcre2Literal` | 0.087 us | 0.050 us | 0.589 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Managed faster** | 4,103 B | 0.117 us | 1.784 us | 0.07x | 0.06–0.07x | -1.667 us | 9 pairs; 38/44 ms; 326,309/24,652 ops/lane; IQR 1.012/1.041 | `Pcre2Literal` | 0.143 us | 0.052 us | 0.583 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.151 us | 2.112 us | 0.07x | 0.07–0.08x | -1.960 us | 9 pairs; 43/43 ms; 280,776/20,193 ops/lane; IQR 1.014/1.246 | `Pcre2Literal` | 0.199 us | 0.229 us | 0.754 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.147 us | 1.790 us | 0.08x | 0.08–0.09x | -1.642 us | 9 pairs; 40/40 ms; 269,766/22,446 ops/lane; IQR 1.013/1.078 | `Pcre2Literal` | 0.165 us | 0.200 us | 0.752 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.094 us | 0.09x | 0.09–0.09x | -0.086 us | 9 pairs; 42/38 ms; 4,874,604/408,286 ops/lane; IQR 1.006/1.016 | `Pcre2Slice` | 0.265 us | 0.067 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.073 us | 0.086 us | 0.85x | 0.84–0.85x | -0.013 us | 9 pairs; 44/40 ms; 603,882/468,839 ops/lane; IQR 1.006/1.008 | `Pcre2Literal` | 0.120 us | 0.050 us | 0.072 us | 1.01x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Inconclusive** | 23 B | 0.019 us | 0.112 us | 0.17x | 0.12–0.17x | -0.093 us | 9 pairs; 54/40 ms; 2,887,791/359,772 ops/lane; IQR 1.180/1.019 | `Pcre2Backtracking` | 0.229 us | 0.088 us | 0.111 us | 0.17x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.113 us | 0.121 us | 0.93x | 0.92–0.94x | -0.008 us | 9 pairs; 41/40 ms; 362,516/328,121 ops/lane; IQR 1.007/1.018 | `Pcre2Character` | 0.131 us | 0.110 us | 0.143 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.074 us | 0.195 us | 0.38x | 0.38–0.38x | -0.122 us | 9 pairs; 40/43 ms; 547,896/218,920 ops/lane; IQR 1.010/1.020 | `Pcre2Slice` | 0.436 us | 0.074 us | 0.105 us | 0.71x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.628 us | 158.249 us | 0.51x | 0.51–0.51x | -77.621 us | 9 pairs; 40/40 ms; 490/254 ops/lane; IQR 1.010/1.010 | `Pcre2Slice` | 387.636 us | 95.550 us | 96.651 us | 0.83x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 36.400 us | 281.236 us | 0.13x | 0.13–0.13x | -244.644 us | 9 pairs; 39/40 ms; 1,072/143 ops/lane; IQR 1.005/1.005 | `Pcre2Character` | 382.692 us | 213.740 us | 223.229 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.236 us | 205.593 us | 0.04x | 0.04–0.05x | -196.359 us | 9 pairs; 40/40 ms; 4,308/196 ops/lane; IQR 1.007/1.012 | `Pcre2Backtracking` | 7.960 us | 210.048 us | 214.150 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 232.299 us | 199.161 us | 1.17x | 1.15–1.17x | +33.435 us | 9 pairs; 39/43 ms; 168/218 ops/lane; IQR 1.004/1.006 | `Pcre2SingleTokenRepeat` | 8.016 us | 222.117 us | 227.525 us | 1.02x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.451 us | 22.294 us | 0.15x | 0.15–0.16x | -18.835 us | 9 pairs; 38/41 ms; 11,049/1,828 ops/lane; IQR 1.004/1.007 | `Pcre2Literal` | 1.358 us | 4.144 us | 5.923 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.443 us | 80.282 us | 0.12x | 0.12–0.12x | -70.768 us | 9 pairs; 40/40 ms; 4,230/494 ops/lane; IQR 1.007/1.012 | `Pcre2Backtracking` | 9.081 us | 86.223 us | 89.651 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,061.463 us | 27,168.500 us | 0.15x | 0.14–0.16x | -23093.413 us | 9 pairs; 32/27 ms; 8/1 ops/lane; IQR 1.016/1.069 | `Pcre2Backtracking` | 5,341.770 us | 10,975.980 us | 17,384.200 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,394.658 us | 53,869.300 us | 0.06x | 0.06–0.06x | -50564.233 us | 9 pairs; 41/54 ms; 12/1 ops/lane; IQR 1.024/1.026 | `Pcre2Backtracking` | 3,793.736 us | 38,762.700 us | 45,535.700 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,901.223 us | 479,544.100 us | 0.00x | 0.00–0.00x | -477698.364 us | 9 pairs; 42/480 ms; 22/1 ops/lane; IQR 1.023/1.006 | `Pcre2Character` | 1,743.141 us | 22,095.800 us | 29,057.450 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,427.300 us | 10,771.167 us | 0.50x | 0.47–0.53x | -5343.867 us | 9 pairs; 33/32 ms; 6/3 ops/lane; IQR 1.003/1.053 | `Pcre2Literal` | 1,259.678 us | 1,314.520 us | 8,052.414 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,589.450 us | 142,615.100 us | 0.13x | 0.13–0.13x | -123824.350 us | 9 pairs; 37/143 ms; 2/1 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 1,230.773 us | 496.471 us | 8,206.417 us | 2.27x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,748.250 us | 21,538.150 us | 0.22x | 0.22–0.22x | -16733.475 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.014/1.007 | `Pcre2Slice` | 2,459.471 us | 10,170.360 us | 18,013.500 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,855.650 us | 130,166.000 us | 0.17x | 0.17–0.17x | -108437.400 us | 9 pairs; 44/130 ms; 2/1 ops/lane; IQR 1.009/1.004 | `Pcre2Backtracking` | 1,499.379 us | 2,218.054 us | 10,203.680 us | 2.14x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 179.345 us | 965.498 us | 0.18x | 0.18–0.19x | -788.641 us | 9 pairs; 39/39 ms; 216/40 ops/lane; IQR 1.006/1.031 | `Pcre2Backtracking` | 163.628 us | 307.577 us | 527.985 us | 0.34x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,939.600 us | 13,066.267 us | 0.91x | 0.90–0.91x | -1161.900 us | 9 pairs; 36/39 ms; 3/3 ops/lane; IQR 1.010/1.009 | `Pcre2Backtracking` | 296.856 us | 14,366.900 us | 14,484.475 us | 0.82x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,745.871 us | 97,486.700 us | 0.03x | 0.03–0.03x | -94742.364 us | 9 pairs; 38/97 ms; 14/1 ops/lane; IQR 1.009/1.008 | `Pcre2Character` | 1,834.875 us | 19,442.667 us | 19,483.700 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 181.658 us | 2,671.487 us | 0.07x | 0.07–0.07x | -2491.708 us | 9 pairs; 39/40 ms; 215/15 ops/lane; IQR 1.013/1.008 | `Pcre2SingleTokenRepeat` | 183.762 us | 980.281 us | 1,406.295 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,494.033 us | 19,642.250 us | 0.08x | 0.08–0.08x | -18137.917 us | 9 pairs; 40/39 ms; 27/2 ops/lane; IQR 1.010/1.005 | `Pcre2Backtracking` | 572.329 us | 10,166.760 us | 10,222.320 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.149 us | 1.066 us | 0.14x | 0.14–0.14x | -0.918 us | 9 pairs; 40/40 ms; 271,452/37,936 ops/lane; IQR 1.015/1.007 | `Pcre2Slice` | 0.707 us | 0.253 us | 0.275 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.495 us | 0.805 us | 0.61x | 0.61–0.61x | -0.313 us | 9 pairs; 38/40 ms; 77,823/49,868 ops/lane; IQR 1.017/1.021 | `Pcre2Literal` | 0.151 us | 0.146 us | 0.168 us | 2.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.292 us | 1.159 us | 0.25x | 0.25–0.25x | -0.867 us | 9 pairs; 38/40 ms; 131,150/34,777 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 1.051 us | 0.337 us | 0.365 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.833 us | 1.110 us | 0.75x | 0.75–0.76x | -0.275 us | 9 pairs; 38/41 ms; 45,786/36,637 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | 0.270 us | 0.415 us | 0.454 us | 1.84x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.156 us | 0.845 us | 0.19x | 0.18–0.19x | -0.688 us | 9 pairs; 37/40 ms; 239,171/47,769 ops/lane; IQR 1.012/1.010 | `Pcre2Slice` | 0.867 us | 0.149 us | 0.194 us | 0.80x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.872 us | 1.076 us | 0.81x | 0.81–0.81x | -0.204 us | 9 pairs; 41/40 ms; 47,398/37,502 ops/lane; IQR 1.009/1.008 | `Pcre2Slice` | 3.762 us | 0.302 us | 0.319 us | 2.73x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.771 us | 0.811 us | 0.95x | 0.94–0.95x | -0.042 us | 9 pairs; 39/39 ms; 51,051/48,077 ops/lane; IQR 1.014/1.005 | `Pcre2Literal` | 3.472 us | 0.177 us | 0.198 us | 3.90x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.975 us | 1.165 us | 0.84x | 0.84–0.84x | -0.188 us | 9 pairs; 40/41 ms; 40,651/35,525 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 5.111 us | 0.390 us | 0.411 us | 2.37x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.176 us | 1.108 us | 1.06x | 1.06–1.07x | +0.070 us | 9 pairs; 40/40 ms; 34,374/36,160 ops/lane; IQR 1.005/1.008 | `Pcre2Character` | 3.595 us | 0.468 us | 0.510 us | 2.31x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.791 us | 0.848 us | 0.94x | 0.93–0.94x | -0.055 us | 9 pairs; 40/41 ms; 50,165/47,804 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 2.121 us | 0.177 us | 0.218 us | 3.63x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.570 us | 1.072 us | 0.53x | 0.53–0.53x | -0.501 us | 9 pairs; 42/41 ms; 72,925/37,947 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.527 us | 0.815 us | 0.65x | 0.64–0.65x | -0.290 us | 9 pairs; 38/41 ms; 72,492/50,067 ops/lane; IQR 1.007/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.707 us | 1.162 us | 0.61x | 0.61–0.62x | -0.452 us | 9 pairs; 40/39 ms; 55,961/33,722 ops/lane; IQR 1.003/1.012 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.897 us | 1.105 us | 0.81x | 0.81–0.82x | -0.208 us | 9 pairs; 41/39 ms; 45,570/35,175 ops/lane; IQR 1.012/1.009 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.469 us | 0.836 us | 0.56x | 0.56–0.56x | -0.367 us | 9 pairs; 39/40 ms; 82,511/47,294 ops/lane; IQR 1.003/1.008 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.531 us | — | — | — | — | — | `—` | 25.181 us | 93.881 us | 94.579 us | 0.64x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.389 us | — | — | — | — | — | `—` | 1.821 us | 0.352 us | 0.379 us | 16.85x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 0.994 us | — | — | — | — | — | `—` | 0.919 us | 0.239 us | 0.266 us | 3.74x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.142 us | — | — | — | — | — | `—` | 2.972 us | 0.442 us | 0.469 us | 2.44x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.437 us | — | — | — | — | — | `—` | 1.026 us | 0.528 us | 0.575 us | 2.50x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.318 us | — | — | — | — | — | `—` | 1.561 us | 0.266 us | 0.318 us | 13.59x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.086 us | 0.081 us | 1.05x | 1.04–1.06x | +0.004 us | 9 pairs; 40/38 ms; 467,916/474,364 ops/lane; IQR 1.007/1.019 | `Pcre2Character` | 0 B | 0 B | 2.200 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.116 us | 0.11x | 0.11–0.11x | -0.103 us | 9 pairs; 39/38 ms; 3,051,941/332,260 ops/lane; IQR 1.007/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 131.144 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.368 us | 0.103 us | 3.57x | 3.54–3.59x | +0.265 us | 9 pairs; 35/40 ms; 96,395/388,488 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 20.413 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.383 us | 0.116 us | 3.30x | 3.28–3.35x | +0.267 us | 9 pairs; 36/40 ms; 93,572/349,190 ops/lane; IQR 1.009/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 70.606 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.366 us | 0.104 us | 3.54x | 3.51–3.56x | +0.262 us | 9 pairs; 35/39 ms; 94,578/376,187 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 71.281 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.566 us | 0.339 us | 1.67x | 1.66–1.68x | +0.227 us | 9 pairs; 39/39 ms; 68,852/115,996 ops/lane; IQR 1.011/1.012 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.659 us | 0.02x | 0.02–0.02x | -0.648 us | 9 pairs; 37/40 ms; 3,383,959/60,023 ops/lane; IQR 1.009/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 21.450 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.503 us | 0.02x | 0.02–0.02x | -0.491 us | 9 pairs; 42/39 ms; 3,621,274/77,221 ops/lane; IQR 1.015/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 23.169 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.451 us | 0.104 us | 4.34x | 4.28–4.39x | +0.347 us | 9 pairs; 36/41 ms; 80,078/398,431 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 18.856 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.191 us | 10.423 us | 0.79x | 0.76–0.79x | -2.204 us | 9 pairs; 40/42 ms; 4,843/4,040 ops/lane; IQR 1.008/1.031 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.294 us | 1.038 us | 0.28x | 0.28–0.28x | -0.746 us | 9 pairs; 39/40 ms; 132,587/38,591 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 130.319 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.308 us | 0.467 us | 0.66x | 0.64–0.67x | -0.157 us | 9 pairs; 37/43 ms; 121,651/91,103 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 109.438 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.282 us | 0.443 us | 0.64x | 0.63–0.65x | -0.160 us | 9 pairs; 33/39 ms; 118,191/87,161 ops/lane; IQR 1.014/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 141.850 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.324 us | 0.573 us | 0.57x | 0.56–0.57x | -0.249 us | 9 pairs; 39/39 ms; 121,734/68,624 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 102.125 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.276 us | 0.386 us | 0.71x | 0.71–0.72x | -0.112 us | 9 pairs; 36/39 ms; 129,879/100,584 ops/lane; IQR 1.006/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 212.356 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.704 us | 8.663 us | 0.55x | 0.54–0.55x | -3.906 us | 9 pairs; 39/39 ms; 8,289/4,549 ops/lane; IQR 1.020/1.015 | `Pcre2Character` | 0 B | 0 B | 1.356 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.258 us | 0.307 us | 0.84x | 0.83–0.84x | -0.051 us | 9 pairs; 36/41 ms; 140,487/134,257 ops/lane; IQR 1.018/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 93.400 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.264 us | 0.391 us | 0.67x | 0.66–0.87x | -0.128 us | 9 pairs; 36/40 ms; 135,226/101,081 ops/lane; IQR 1.179/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 120.287 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.842 us | — | — | — | — | — | `—` | 0 B | — | 13.294 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.182 us | 0.290 us | 0.63x | 0.62–0.63x | -0.108 us | 9 pairs; 41/40 ms; 224,540/137,654 ops/lane; IQR 1.012/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 52.981 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.722 us | — | — | — | — | — | `—` | 0 B | — | 13.537 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.562 us | 0.666 us | 0.84x | 0.83–0.85x | -0.107 us | 9 pairs; 38/40 ms; 67,879/60,469 ops/lane; IQR 1.012/1.011 | `Pcre2Slice` | 0 B | 0 B | 63.681 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.682 us | 28.297 us | 0.34x | 0.34–0.34x | -18.621 us | 9 pairs; 40/40 ms; 4,096/1,408 ops/lane; IQR 1.005/1.009 | `Pcre2Literal` | 0 B | 0 B | 0.706 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.933 us | 1.097 us | 0.85x | 0.85–0.85x | -0.166 us | 9 pairs; 39/41 ms; 41,860/37,233 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 128.744 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.918 us | 1.189 us | 0.77x | 0.77–0.78x | -0.271 us | 9 pairs; 37/43 ms; 40,732/35,908 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 109.956 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.905 us | 1.000 us | 0.91x | 0.90–0.91x | -0.095 us | 9 pairs; 39/38 ms; 42,850/38,196 ops/lane; IQR 1.011/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 137.238 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.932 us | 1.143 us | 0.82x | 0.81–0.82x | -0.208 us | 9 pairs; 39/40 ms; 42,276/35,218 ops/lane; IQR 1.015/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 102.456 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.991 us | 0.926 us | 1.08x | 1.07–1.08x | +0.071 us | 9 pairs; 42/41 ms; 42,509/43,847 ops/lane; IQR 1.010/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 210.562 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.315 us | 8.620 us | 0.62x | 0.61–0.62x | -3.307 us | 9 pairs; 38/40 ms; 7,168/4,635 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 0 B | 0 B | 1.344 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.977 us | 0.871 us | 1.12x | 1.11–1.13x | +0.104 us | 9 pairs; 42/41 ms; 43,496/47,193 ops/lane; IQR 1.010/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 92.531 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.893 us | 0.970 us | 0.92x | 0.92–0.94x | -0.074 us | 9 pairs; 38/41 ms; 42,926/41,894 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 122.375 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.059 us | — | — | — | — | — | `—` | 0 B | — | 13.688 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.812 us | 0.846 us | 0.96x | 0.95–0.97x | -0.032 us | 9 pairs; 40/41 ms; 49,735/49,040 ops/lane; IQR 1.015/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 52.719 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.996 us | — | — | — | — | — | `—` | 0 B | — | 13.650 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.828 us | 1.207 us | 0.68x | 0.68–0.69x | -0.380 us | 9 pairs; 38/41 ms; 46,178/33,742 ops/lane; IQR 1.007/1.008 | `Pcre2Slice` | 0 B | 0 B | 63.425 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.343 us | 2.238 us | 0.60x | 0.60–0.60x | -0.903 us | 9 pairs; 40/41 ms; 30,127/18,418 ops/lane; IQR 1.011/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.630 us | 1.095 us | 0.57x | 0.57–0.58x | -0.465 us | 9 pairs; 40/40 ms; 63,479/36,285 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 132.969 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.665 us | 1.179 us | 0.56x | 0.56–0.56x | -0.515 us | 9 pairs; 39/42 ms; 58,808/35,721 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 113.850 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.605 us | 1.007 us | 0.60x | 0.60–0.60x | -0.401 us | 9 pairs; 39/40 ms; 64,800/39,951 ops/lane; IQR 1.012/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 140.819 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.612 us | 1.131 us | 0.54x | 0.54–0.54x | -0.520 us | 9 pairs; 39/40 ms; 63,721/35,425 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 102.400 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.605 us | 0.932 us | 0.65x | 0.65–0.65x | -0.328 us | 9 pairs; 37/42 ms; 61,836/45,016 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 210.719 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.772 us | 2.143 us | 0.83x | 0.83–0.83x | -0.369 us | 9 pairs; 39/40 ms; 22,098/18,887 ops/lane; IQR 1.006/1.004 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.588 us | 0.867 us | 0.68x | 0.67–0.68x | -0.279 us | 9 pairs; 38/39 ms; 63,783/45,223 ops/lane; IQR 1.014/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 92.531 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.639 us | 0.970 us | 0.66x | 0.65–0.67x | -0.331 us | 9 pairs; 42/41 ms; 65,086/42,081 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 120.625 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.692 us | — | — | — | — | — | `—` | 0 B | — | 13.037 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.485 us | 0.838 us | 0.58x | 0.57–0.58x | -0.354 us | 9 pairs; 40/40 ms; 81,688/47,795 ops/lane; IQR 1.015/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 53.344 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.568 us | — | — | — | — | — | `—` | 0 B | — | 13.381 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.523 us | 1.221 us | 0.43x | 0.43–0.43x | -0.694 us | 9 pairs; 39/39 ms; 75,008/31,672 ops/lane; IQR 1.016/1.016 | `Pcre2Slice` | 0 B | 0 B | 64.513 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 16.598 us | — | — | — | — | — | `—` | 480 B | — | 1.113 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.078 us | — | — | — | — | — | `—` | 48 B | — | 129.275 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.166 us | — | — | — | — | — | `—` | 48 B | — | 116.319 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.071 us | — | — | — | — | — | `—` | 48 B | — | 139.537 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.077 us | — | — | — | — | — | `—` | 48 B | — | 98.500 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.054 us | — | — | — | — | — | `—` | 48 B | — | 219.319 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.728 us | — | — | — | — | — | `—` | 64 B | — | 1.344 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.066 us | — | — | — | — | — | `—` | 40 B | — | 91.256 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.062 us | — | — | — | — | — | `—` | 40 B | — | 119.619 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.236 us | — | — | — | — | — | `—` | 1,216 B | — | 13.131 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.007 us | — | — | — | — | — | `—` | 48 B | — | 53.156 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.438 us | — | — | — | — | — | `—` | 928 B | — | 14.006 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.660 us | — | — | — | — | — | `—` | 28,928 B | — | 65.388 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 26.492–210.109 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.103–92.756 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.078–12.306 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.854–46.793 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.038–1.299 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.176–39.691 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.504–21.572 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.236–96.725 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.874–70.115 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.193–5.374 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.808–367.532 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.468–299.277 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.860–5.060 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.114–15.784 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 164.502–1,284.117 us |

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
