<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `5CE494A9124E9A0109224E988325D835104D0E3573972F028F4E846BEEF9E13E`
- Latest managed row measurement: `2026-11-07T12:24:45.1050448+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-07T12:24:45.1050448+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `72` managed faster, `2` equivalent, `12` native faster, `15` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `6032b9286c6b` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `6032b9286c6b` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.605 us | 2.527 us | 0.24x | 0.22–0.28x | -1.923 us | 9 pairs; 36/49 ms; 58,935/19,496 ops/lane; IQR 1.017/1.119 | `Pcre2Slice` | 459.737 us | 0.277 us | 0.810 us | 0.75x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.701 us | 10.537 us | 0.07x | 0.06–0.07x | -9.825 us | 9 pairs; 33/34 ms; 46,783/3,188 ops/lane; IQR 1.018/1.145 | `Pcre2Slice` | 0.523 us | 0.096 us | 3.553 us | 0.20x | 64 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.925 us | 0.01x | 0.01–0.01x | -1.914 us | 9 pairs; 41/36 ms; 3,743,310/18,507 ops/lane; IQR 1.011/1.015 | `Pcre2Slice` | 0.105 us | 1.074 us | 1.108 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.689 us | 0.157 us | 4.43x | 4.39–4.48x | +0.534 us | 9 pairs; 37/40 ms; 54,158/256,503 ops/lane; IQR 1.023/1.017 | `Pcre2Backtracking` | 0.045 us | 0.122 us | 0.163 us | 4.22x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.406 us | 0.564 us | 4.25x | 4.25–4.27x | +1.839 us | 9 pairs; 40/41 ms; 16,597/73,261 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0.114 us | 0.757 us | 0.798 us | 3.02x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.292 us | 0.34x | 0.34–0.34x | -0.193 us | 9 pairs; 39/41 ms; 396,939/138,787 ops/lane; IQR 1.014/1.015 | `Pcre2Slice` | 0.113 us | 0.314 us | 0.341 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.349 us | 0.30x | 0.30–0.31x | -0.243 us | 9 pairs; 40/41 ms; 375,058/116,029 ops/lane; IQR 1.005/1.006 | `Pcre2Slice` | 0.118 us | 0.443 us | 0.471 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.078 us | 0.289 us | 0.27x | 0.27–0.27x | -0.210 us | 9 pairs; 40/39 ms; 517,297/134,838 ops/lane; IQR 1.008/1.017 | `Pcre2Slice` | 0.090 us | 0.248 us | 0.259 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.079 us | 0.309 us | 0.26x | 0.25–0.26x | -0.230 us | 9 pairs; 37/40 ms; 469,400/129,518 ops/lane; IQR 1.010/1.003 | `Pcre2Slice` | 0.090 us | 0.252 us | 0.273 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.078 us | 2.20x | 2.15–2.29x | +0.094 us | 9 pairs; 40/43 ms; 232,418/550,201 ops/lane; IQR 1.009/1.021 | `Pcre2Slice` | 0.089 us | 0.935 us | 0.972 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.198 us | 0.17x | 0.17–0.17x | -0.165 us | 9 pairs; 39/41 ms; 1,185,253/206,579 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0.110 us | 0.295 us | 0.338 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.614 us | 0.10x | 0.10–0.10x | -0.553 us | 9 pairs; 40/41 ms; 657,642/66,437 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0.135 us | 0.234 us | 0.259 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.21x | 0.21–0.22x | -0.121 us | 9 pairs; 38/40 ms; 1,142,510/258,275 ops/lane; IQR 1.024/1.003 | `Pcre2Backtracking` | 0.104 us | 0.178 us | 0.199 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.109 us | 0.18x | 0.18–0.19x | -0.090 us | 9 pairs; 38/39 ms; 1,952,967/360,289 ops/lane; IQR 1.029/1.008 | `Pcre2Backtracking` | 0.058 us | 0.089 us | 0.109 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.121 us | 0.47x | 0.46–0.48x | -0.064 us | 9 pairs; 40/38 ms; 707,525/318,279 ops/lane; IQR 1.019/1.017 | `Pcre2Backtracking` | 0.014 us | 0.083 us | 0.102 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Inconclusive** | 67 B | 0.093 us | 0.273 us | 0.34x | 0.31–0.35x | -0.181 us | 9 pairs; 44/43 ms; 473,359/156,098 ops/lane; IQR 1.106/1.014 | `Pcre2Slice` | 0.095 us | 0.115 us | 0.153 us | 0.61x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.116 us | 0.21x | 0.20–0.21x | -0.092 us | 9 pairs; 42/40 ms; 1,736,686/347,697 ops/lane; IQR 1.046/1.022 | `Pcre2Backtracking` | 0.019 us | 0.091 us | 0.112 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.117 us | 2.192 us | 0.05x | 0.05–0.06x | -2.075 us | 9 pairs; 40/45 ms; 339,608/20,371 ops/lane; IQR 1.008/1.165 | `Pcre2Literal` | 0.085 us | 0.051 us | 0.588 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 1.896 us | 0.06x | 0.06–0.07x | -1.775 us | 9 pairs; 40/33 ms; 328,972/17,320 ops/lane; IQR 1.005/1.080 | `Pcre2Literal` | 0.147 us | 0.053 us | 0.579 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.154 us | 1.918 us | 0.08x | 0.07–0.08x | -1.765 us | 9 pairs; 39/38 ms; 255,571/19,990 ops/lane; IQR 1.003/1.135 | `Pcre2Literal` | 0.201 us | 0.228 us | 0.764 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.151 us | 1.926 us | 0.08x | 0.08–0.08x | -1.774 us | 9 pairs; 40/38 ms; 265,999/19,847 ops/lane; IQR 1.012/1.054 | `Pcre2Literal` | 0.172 us | 0.201 us | 0.718 us | 0.21x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.096 us | 0.09x | 0.09–0.09x | -0.088 us | 9 pairs; 39/41 ms; 4,442,554/426,639 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | 0.212 us | 0.067 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.066 us | 0.084 us | 0.79x | 0.78–0.79x | -0.018 us | 9 pairs; 38/40 ms; 566,852/471,841 ops/lane; IQR 1.004/1.007 | `Pcre2Literal` | 0.128 us | 0.050 us | 0.072 us | 0.91x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.108 us | 0.12x | 0.12–0.13x | -0.094 us | 9 pairs; 40/40 ms; 3,003,139/374,258 ops/lane; IQR 1.017/1.014 | `Pcre2Backtracking` | 0.246 us | 0.088 us | 0.111 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.124 us | 0.123 us | 1.01x | 1.01–1.02x | +0.002 us | 9 pairs; 45/40 ms; 359,716/328,055 ops/lane; IQR 1.007/1.010 | `Pcre2Character` | 0.134 us | 0.109 us | 0.141 us | 0.88x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.082 us | 0.207 us | 0.40x | 0.39–0.40x | -0.125 us | 9 pairs; 45/40 ms; 542,915/195,141 ops/lane; IQR 1.018/1.013 | `Pcre2Slice` | 0.338 us | 0.073 us | 0.112 us | 0.73x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.541 us | 157.280 us | 0.51x | 0.51–0.52x | -76.371 us | 9 pairs; 40/40 ms; 502/253 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 343.781 us | 99.680 us | 103.409 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.040 us | 278.725 us | 0.14x | 0.14–0.14x | -239.678 us | 9 pairs; 40/38 ms; 1,026/137 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 256.471 us | 215.508 us | 225.718 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.499 us | 209.375 us | 0.05x | 0.05–0.05x | -199.902 us | 9 pairs; 39/41 ms; 4,137/197 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 8.139 us | 219.674 us | 221.416 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 238.667 us | 204.773 us | 1.17x | 1.16–1.18x | +34.693 us | 9 pairs; 41/41 ms; 170/202 ops/lane; IQR 1.006/1.016 | `Pcre2SingleTokenRepeat` | 8.126 us | 229.194 us | 233.380 us | 1.02x | 0 B | 0 B |
| `common/matches-word` | **Inconclusive** | 8,208 B | 3.513 us | 23.423 us | 0.15x | 0.15–0.16x | -19.885 us | 9 pairs; 39/41 ms; 11,143/1,734 ops/lane; IQR 1.006/1.046 | `Pcre2Literal` | 1.397 us | 4.263 us | 5.979 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.510 us | 81.646 us | 0.12x | 0.11–0.12x | -72.150 us | 9 pairs; 39/41 ms; 4,149/500 ops/lane; IQR 1.008/1.023 | `Pcre2Backtracking` | 9.190 us | 90.405 us | 92.687 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,151.238 us | 27,351.700 us | 0.15x | 0.15–0.16x | -23191.800 us | 9 pairs; 33/27 ms; 8/1 ops/lane; IQR 1.012/1.033 | `Pcre2Backtracking` | 5,739.257 us | 11,732.486 us | 18,411.840 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,251.067 us | 54,822.200 us | 0.06x | 0.06–0.06x | -51496.342 us | 9 pairs; 39/55 ms; 12/1 ops/lane; IQR 1.020/1.018 | `Pcre2Backtracking` | 3,817.271 us | 39,753.450 us | 46,245.700 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,916.305 us | 470,421.200 us | 0.00x | 0.00–0.00x | -468551.662 us | 9 pairs; 40/470 ms; 21/1 ops/lane; IQR 1.027/1.003 | `Pcre2Character` | 1,760.579 us | 22,508.800 us | 29,573.867 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,444.000 us | 11,157.233 us | 0.49x | 0.48–0.55x | -5651.117 us | 9 pairs; 33/33 ms; 6/3 ops/lane; IQR 1.017/1.102 | `Pcre2Literal` | 1,313.540 us | 1,309.848 us | 8,356.120 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,546.450 us | 142,458.900 us | 0.13x | 0.13–0.13x | -123927.250 us | 9 pairs; 37/142 ms; 2/1 ops/lane; IQR 1.015/1.010 | `Pcre2Backtracking` | 1,213.532 us | 509.095 us | 8,475.956 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,879.188 us | 21,775.550 us | 0.22x | 0.22–0.23x | -16884.563 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.004/1.015 | `Pcre2Slice` | 2,375.975 us | 10,999.143 us | 18,715.375 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Inconclusive** | 6,839,410 B | 22,788.300 us | 137,171.900 us | 0.17x | 0.16–0.17x | -114383.600 us | 9 pairs; 46/137 ms; 2/1 ops/lane; IQR 1.036/1.016 | `Pcre2Backtracking` | 1,469.287 us | 2,288.121 us | 10,416.025 us | 2.19x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 139.635 us | 1,013.741 us | 0.14x | 0.13–0.15x | -866.878 us | 9 pairs; 40/40 ms; 286/39 ops/lane; IQR 1.047/1.105 | `Pcre2Backtracking` | 167.870 us | 322.780 us | 594.460 us | 0.23x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,347.200 us | 13,272.433 us | 0.85x | 0.85–0.86x | -1977.567 us | 9 pairs; 34/40 ms; 3/3 ops/lane; IQR 1.006/1.012 | `Pcre2Backtracking` | 289.010 us | 15,184.780 us | 15,286.740 us | 0.74x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,788.743 us | 97,664.800 us | 0.03x | 0.03–0.03x | -94876.057 us | 9 pairs; 39/98 ms; 14/1 ops/lane; IQR 1.012/1.006 | `Pcre2Character` | 1,832.080 us | 19,522.075 us | 19,665.925 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.768 us | 2,770.350 us | 0.07x | 0.07–0.07x | -2584.086 us | 9 pairs; 39/39 ms; 210/14 ops/lane; IQR 1.006/1.016 | `Pcre2SingleTokenRepeat` | 185.738 us | 1,002.690 us | 1,238.569 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,574.329 us | 20,064.750 us | 0.08x | 0.08–0.08x | -18496.417 us | 9 pairs; 38/40 ms; 24/2 ops/lane; IQR 1.007/1.011 | `Pcre2Backtracking` | 552.012 us | 11,136.571 us | 11,074.957 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.137 us | 1.083 us | 0.13x | 0.13–0.13x | -0.947 us | 9 pairs; 40/39 ms; 291,367/35,911 ops/lane; IQR 1.009/1.006 | `Pcre2Slice` | 0.564 us | 0.253 us | 0.275 us | 0.50x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.539 us | 0.820 us | 0.66x | 0.66–0.67x | -0.279 us | 9 pairs; 39/40 ms; 73,211/49,401 ops/lane; IQR 1.016/1.008 | `Pcre2Literal` | 0.138 us | 0.145 us | 0.167 us | 3.22x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.296 us | 1.168 us | 0.25x | 0.25–0.25x | -0.872 us | 9 pairs; 40/40 ms; 134,415/34,101 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 1.035 us | 0.341 us | 0.367 us | 0.81x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.865 us | 1.112 us | 0.78x | 0.77–0.78x | -0.246 us | 9 pairs; 41/39 ms; 46,922/35,341 ops/lane; IQR 1.008/1.008 | `Pcre2Character` | 0.275 us | 0.417 us | 0.462 us | 1.87x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.150 us | 0.847 us | 0.18x | 0.17–0.18x | -0.697 us | 9 pairs; 40/41 ms; 269,562/48,999 ops/lane; IQR 1.018/1.031 | `Pcre2Slice` | 0.628 us | 0.152 us | 0.197 us | 0.76x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.893 us | 1.092 us | 0.82x | 0.81–0.82x | -0.201 us | 9 pairs; 40/41 ms; 44,783/37,852 ops/lane; IQR 1.009/1.008 | `Pcre2Slice` | 2.212 us | 0.299 us | 0.316 us | 2.82x | 0 B | 0 B |
| `simple/foo-dense` | **Equivalent** | 18 B | 0.828 us | 0.820 us | 1.01x | 1.01–1.01x | +0.005 us | 9 pairs; 41/39 ms; 49,227/47,839 ops/lane; IQR 1.008/1.009 | `Pcre2Literal` | 3.528 us | 0.177 us | 0.198 us | 4.18x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.022 us | 1.423 us | 0.72x | 0.72–0.72x | -0.399 us | 9 pairs; 40/40 ms; 39,013/28,260 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 5.121 us | 0.400 us | 0.421 us | 2.43x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.172 us | 1.118 us | 1.05x | 1.05–1.06x | +0.056 us | 9 pairs; 39/40 ms; 33,542/36,104 ops/lane; IQR 1.012/1.008 | `Pcre2Character` | 3.564 us | 0.465 us | 0.505 us | 2.32x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.780 us | 0.848 us | 0.92x | 0.92–0.92x | -0.067 us | 9 pairs; 39/39 ms; 50,363/46,298 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 1.886 us | 0.179 us | 0.221 us | 3.52x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.625 us | 1.091 us | 0.57x | 0.57–0.57x | -0.466 us | 9 pairs; 42/39 ms; 66,512/35,889 ops/lane; IQR 1.011/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.494 us | 0.822 us | 0.60x | 0.60–0.60x | -0.330 us | 9 pairs; 41/41 ms; 83,303/49,397 ops/lane; IQR 1.005/1.007 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.663 us | 1.431 us | 0.46x | 0.46–0.46x | -0.772 us | 9 pairs; 40/40 ms; 60,496/28,219 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.865 us | 1.109 us | 0.78x | 0.78–0.78x | -0.246 us | 9 pairs; 39/40 ms; 45,565/35,879 ops/lane; IQR 1.003/1.004 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.525 us | 0.847 us | 0.62x | 0.62–0.62x | -0.324 us | 9 pairs; 41/40 ms; 78,833/47,327 ops/lane; IQR 1.010/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.569 us | — | — | — | — | — | `—` | 25.356 us | 97.527 us | 98.344 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.445 us | — | — | — | — | — | `—` | 1.424 us | 0.355 us | 0.380 us | 9.06x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.071 us | — | — | — | — | — | `—` | 0.913 us | 0.240 us | 0.270 us | 3.96x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.190 us | — | — | — | — | — | `—` | 3.075 us | 0.448 us | 0.487 us | 2.44x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.841 us | — | — | — | — | — | `—` | 0.999 us | 0.524 us | 0.567 us | 3.25x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.330 us | — | — | — | — | — | `—` | 1.389 us | 0.272 us | 0.322 us | 10.34x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.088 us | 0.081 us | 1.07x | 1.07–1.09x | +0.006 us | 9 pairs; 40/40 ms; 456,963/493,604 ops/lane; IQR 1.007/1.025 | `Pcre2Character` | 0 B | 0 B | 2.325 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.012 us | 0.117 us | 0.11x | 0.11–0.11x | -0.104 us | 9 pairs; 39/41 ms; 3,133,695/350,974 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 130.806 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.388 us | 0.102 us | 3.78x | 3.77–3.81x | +0.286 us | 9 pairs; 36/39 ms; 92,244/386,075 ops/lane; IQR 1.009/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 21.113 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.381 us | 0.117 us | 3.26x | 3.23–3.27x | +0.263 us | 9 pairs; 35/41 ms; 92,914/349,254 ops/lane; IQR 1.010/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 71.075 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.372 us | 0.102 us | 3.63x | 3.61–3.64x | +0.269 us | 9 pairs; 36/40 ms; 95,867/388,553 ops/lane; IQR 1.020/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 70.394 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.658 us | 0.342 us | 1.93x | 1.92–1.94x | +0.317 us | 9 pairs; 38/41 ms; 57,682/120,737 ops/lane; IQR 1.013/1.016 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.627 us | 0.02x | 0.02–0.02x | -0.615 us | 9 pairs; 42/40 ms; 3,533,414/63,251 ops/lane; IQR 1.011/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 21.481 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.526 us | 0.02x | 0.02–0.02x | -0.515 us | 9 pairs; 38/42 ms; 3,404,025/80,137 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 21.494 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.453 us | 0.106 us | 4.28x | 4.18–4.32x | +0.347 us | 9 pairs; 36/38 ms; 80,082/356,857 ops/lane; IQR 1.005/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 18.250 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.315 us | 10.622 us | 0.78x | 0.78–0.79x | -2.317 us | 9 pairs; 40/41 ms; 4,785/3,821 ops/lane; IQR 1.009/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.700 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.288 us | 1.109 us | 0.26x | 0.26–0.26x | -0.822 us | 9 pairs; 39/42 ms; 134,459/37,651 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 130.825 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.301 us | 0.465 us | 0.65x | 0.64–0.65x | -0.163 us | 9 pairs; 33/40 ms; 108,835/85,902 ops/lane; IQR 1.003/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 112.631 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.321 us | 0.451 us | 0.72x | 0.66–0.72x | -0.127 us | 9 pairs; 39/39 ms; 122,598/86,062 ops/lane; IQR 1.072/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 141.950 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.307 us | 0.584 us | 0.52x | 0.51–0.53x | -0.275 us | 9 pairs; 36/40 ms; 115,832/68,754 ops/lane; IQR 1.006/1.028 | `Pcre2Backtracking` | 0 B | 0 B | 99.838 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.323 us | 0.398 us | 0.81x | 0.81–0.82x | -0.076 us | 9 pairs; 36/39 ms; 112,399/98,395 ops/lane; IQR 1.005/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 208.731 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.847 us | 8.664 us | 0.56x | 0.56–0.56x | -3.777 us | 9 pairs; 38/40 ms; 7,748/4,567 ops/lane; IQR 1.007/1.010 | `Pcre2Character` | 0 B | 0 B | 1.394 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.258 us | 0.305 us | 0.85x | 0.84–0.85x | -0.047 us | 9 pairs; 35/40 ms; 136,494/131,060 ops/lane; IQR 1.018/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 90.631 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.268 us | 0.401 us | 0.67x | 0.66–0.67x | -0.133 us | 9 pairs; 30/39 ms; 112,996/98,438 ops/lane; IQR 1.002/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 122.219 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.729 us | — | — | — | — | — | `—` | 0 B | — | 13.556 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.193 us | 0.288 us | 0.67x | 0.66–0.68x | -0.095 us | 9 pairs; 39/41 ms; 204,342/143,060 ops/lane; IQR 1.014/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 52.744 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.618 us | — | — | — | — | — | `—` | 0 B | — | 13.825 us | 10,536 B |
| `pcre2/same-start-global` | **Inconclusive** | 9 B | 0.594 us | 0.679 us | 0.88x | 0.85–0.91x | -0.079 us | 9 pairs; 36/38 ms; 60,410/56,679 ops/lane; IQR 1.029/1.034 | `Pcre2Slice` | 0 B | 0 B | 63.650 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.877 us | 28.469 us | 0.35x | 0.35–0.35x | -18.579 us | 9 pairs; 40/40 ms; 4,075/1,390 ops/lane; IQR 1.019/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.725 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.949 us | 1.071 us | 0.89x | 0.88–0.89x | -0.122 us | 9 pairs; 39/40 ms; 41,480/37,071 ops/lane; IQR 1.015/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 127.200 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.025 us | 1.174 us | 0.87x | 0.87–0.88x | -0.148 us | 9 pairs; 39/41 ms; 37,656/35,062 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 116.800 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.968 us | 1.017 us | 0.96x | 0.95–0.96x | -0.044 us | 9 pairs; 40/40 ms; 41,170/39,837 ops/lane; IQR 1.007/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 140.062 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.977 us | 1.165 us | 0.84x | 0.83–0.85x | -0.188 us | 9 pairs; 41/39 ms; 41,891/33,860 ops/lane; IQR 1.017/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 101.100 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.942 us | 0.943 us | 1.00x | 0.99–1.00x | -0.002 us | 9 pairs; 38/40 ms; 40,125/42,267 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 210.944 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.627 us | 8.842 us | 0.64x | 0.64–0.64x | -3.193 us | 9 pairs; 42/40 ms; 7,509/4,529 ops/lane; IQR 1.011/1.011 | `Pcre2Character` | 0 B | 0 B | 1.981 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.906 us | 0.874 us | 1.03x | 1.03–1.04x | +0.031 us | 9 pairs; 39/40 ms; 42,854/45,363 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 91.644 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.917 us | 0.965 us | 0.95x | 0.95–0.97x | -0.046 us | 9 pairs; 43/41 ms; 46,454/42,982 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 120.394 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.004 us | — | — | — | — | — | `—` | 0 B | — | 14.406 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.808 us | 0.847 us | 0.95x | 0.95–0.96x | -0.039 us | 9 pairs; 40/39 ms; 49,931/46,498 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 52.006 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.897 us | — | — | — | — | — | `—` | 0 B | — | 13.769 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.913 us | 1.252 us | 0.73x | 0.73–0.73x | -0.338 us | 9 pairs; 41/40 ms; 44,401/32,113 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | 0 B | 0 B | 64.725 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.436 us | 2.246 us | 0.64x | 0.63–0.64x | -0.810 us | 9 pairs; 39/40 ms; 27,455/17,729 ops/lane; IQR 1.003/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.706 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.619 us | 1.072 us | 0.58x | 0.58–0.58x | -0.454 us | 9 pairs; 39/40 ms; 62,375/37,233 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 127.675 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.691 us | 1.171 us | 0.59x | 0.59–0.59x | -0.484 us | 9 pairs; 41/40 ms; 59,377/34,051 ops/lane; IQR 1.009/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 115.781 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.608 us | 1.018 us | 0.60x | 0.59–0.60x | -0.411 us | 9 pairs; 39/41 ms; 64,178/40,619 ops/lane; IQR 1.009/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 139.338 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.633 us | 1.172 us | 0.54x | 0.54–0.54x | -0.535 us | 9 pairs; 42/42 ms; 66,857/35,924 ops/lane; IQR 1.011/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 101.481 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.637 us | 0.955 us | 0.67x | 0.67–0.67x | -0.319 us | 9 pairs; 38/41 ms; 60,295/42,854 ops/lane; IQR 1.010/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 207.194 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.902 us | 2.165 us | 0.88x | 0.87–0.89x | -0.268 us | 9 pairs; 40/41 ms; 20,805/18,853 ops/lane; IQR 1.008/1.009 | `Pcre2Character` | 0 B | 0 B | 2.100 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.595 us | 0.881 us | 0.67x | 0.67–0.67x | -0.287 us | 9 pairs; 40/39 ms; 67,803/44,569 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 89.450 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.625 us | 0.966 us | 0.65x | 0.64–0.65x | -0.340 us | 9 pairs; 41/40 ms; 65,340/40,980 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 122.938 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.706 us | — | — | — | — | — | `—` | 0 B | — | 13.725 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.526 us | 0.847 us | 0.62x | 0.62–0.63x | -0.321 us | 9 pairs; 41/41 ms; 77,135/48,039 ops/lane; IQR 1.011/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 52.450 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.592 us | — | — | — | — | — | `—` | 0 B | — | 14.869 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.536 us | 1.240 us | 0.43x | 0.43–0.43x | -0.703 us | 9 pairs; 37/40 ms; 68,641/32,424 ops/lane; IQR 1.009/1.011 | `Pcre2Slice` | 0 B | 0 B | 64.144 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.671 us | — | — | — | — | — | `—` | 480 B | — | 0.713 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.123 us | — | — | — | — | — | `—` | 48 B | — | 129.656 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.213 us | — | — | — | — | — | `—` | 48 B | — | 112.588 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.113 us | — | — | — | — | — | `—` | 48 B | — | 140.431 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.124 us | — | — | — | — | — | `—` | 48 B | — | 99.100 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.112 us | — | — | — | — | — | `—` | 48 B | — | 213.994 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.451 us | — | — | — | — | — | `—` | 64 B | — | 2.031 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.123 us | — | — | — | — | — | `—` | 40 B | — | 92.312 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.135 us | — | — | — | — | — | `—` | 40 B | — | 119.975 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.002 us | — | — | — | — | — | `—` | 1,216 B | — | 13.113 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.058 us | — | — | — | — | — | `—` | 48 B | — | 54.256 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.464 us | — | — | — | — | — | `—` | 928 B | — | 13.731 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.567 us | — | — | — | — | — | `—` | 4,496 B | — | 64.869 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 31.747–253.512 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.258–93.125 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.904–14.521 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.090–49.492 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.032–1.124 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.413–41.939 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.614–22.117 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.127–98.461 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.998–71.521 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.184–5.475 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.246–371.622 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.075 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.964–302.191 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.837–5.038 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.062–15.148 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 106.477–831.774 us |

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
