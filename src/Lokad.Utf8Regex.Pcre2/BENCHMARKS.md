<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `F41BCF0898030BAED40D3616F1076ABC0D343196F75AE3CCCB3CA647FD7D25FD`
- Latest managed row measurement: `2026-11-06T12:03:37.3952669+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T12:03:37.3952669+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `79` managed faster, `1` equivalent, `12` native faster, `9` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `f7e13648950a` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `f7e13648950a` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.646 us | 2.722 us | 0.24x | 0.23–0.28x | -2.079 us | 9 pairs; 36/37 ms; 55,321/13,608 ops/lane; IQR 1.014/1.194 | `Pcre2Slice` | 475.507 us | 0.281 us | 0.912 us | 0.71x | 928 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.845 us | 12.416 us | 0.07x | 0.06–0.07x | -11.572 us | 9 pairs; 38/41 ms; 44,737/3,310 ops/lane; IQR 1.013/1.115 | `Pcre2Slice` | 0.630 us | 0.097 us | 3.704 us | 0.23x | 400 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.210 us | 0.01x | 0.01–0.01x | -1.199 us | 9 pairs; 40/39 ms; 3,601,539/32,147 ops/lane; IQR 1.013/1.009 | `Pcre2Slice` | 0.107 us | 1.167 us | 1.179 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.703 us | 0.159 us | 4.42x | 4.39–4.45x | +0.545 us | 9 pairs; 37/41 ms; 52,648/254,600 ops/lane; IQR 1.010/1.012 | `Pcre2Backtracking` | 0.046 us | 0.122 us | 0.164 us | 4.28x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.364 us | 0.573 us | 4.13x | 4.12–4.14x | +1.791 us | 9 pairs; 40/39 ms; 16,817/68,679 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0.114 us | 0.757 us | 0.798 us | 2.96x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.101 us | 0.293 us | 0.35x | 0.34–0.36x | -0.191 us | 9 pairs; 37/39 ms; 367,075/133,156 ops/lane; IQR 1.027/1.009 | `Pcre2Slice` | 0.116 us | 0.317 us | 0.345 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.107 us | 0.359 us | 0.30x | 0.30–0.30x | -0.252 us | 9 pairs; 40/39 ms; 373,207/109,222 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 0.120 us | 0.436 us | 0.471 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.079 us | 0.290 us | 0.27x | 0.27–0.27x | -0.212 us | 9 pairs; 39/40 ms; 501,195/136,501 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 0.092 us | 0.234 us | 0.257 us | 0.31x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.079 us | 0.313 us | 0.25x | 0.25–0.26x | -0.234 us | 9 pairs; 39/38 ms; 492,937/121,267 ops/lane; IQR 1.006/1.013 | `Pcre2Slice` | 0.092 us | 0.251 us | 0.274 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.174 us | 0.078 us | 2.23x | 2.20–2.30x | +0.096 us | 9 pairs; 39/39 ms; 226,071/493,480 ops/lane; IQR 1.006/1.023 | `Pcre2Slice` | 0.091 us | 0.980 us | 1.016 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.198 us | 0.17x | 0.17–0.17x | -0.165 us | 9 pairs; 40/40 ms; 1,185,008/201,520 ops/lane; IQR 1.002/1.009 | `Pcre2Backtracking` | 0.114 us | 0.298 us | 0.341 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.616 us | 0.10x | 0.10–0.10x | -0.555 us | 9 pairs; 40/39 ms; 658,949/64,155 ops/lane; IQR 1.009/1.008 | `Pcre2Backtracking` | 0.137 us | 0.237 us | 0.259 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.155 us | 0.22x | 0.21–0.22x | -0.122 us | 9 pairs; 37/39 ms; 1,113,908/253,958 ops/lane; IQR 1.025/1.009 | `Pcre2Backtracking` | 0.104 us | 0.184 us | 0.205 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.114 us | 0.17x | 0.17–0.18x | -0.094 us | 9 pairs; 38/40 ms; 1,948,229/352,106 ops/lane; IQR 1.028/1.008 | `Pcre2Backtracking` | 0.060 us | 0.092 us | 0.114 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.121 us | 0.48x | 0.47–0.49x | -0.062 us | 9 pairs; 40/41 ms; 682,434/343,079 ops/lane; IQR 1.003/1.017 | `Pcre2Backtracking` | 0.013 us | 0.085 us | 0.105 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.084 us | 0.280 us | 0.30x | 0.30–0.31x | -0.195 us | 9 pairs; 40/39 ms; 468,061/138,821 ops/lane; IQR 1.005/1.011 | `Pcre2Slice` | 0.096 us | 0.118 us | 0.155 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.116 us | 0.21x | 0.19–0.21x | -0.091 us | 9 pairs; 42/39 ms; 1,755,233/341,496 ops/lane; IQR 1.037/1.019 | `Pcre2Backtracking` | 0.019 us | 0.093 us | 0.115 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 1.920 us | 0.06x | 0.06–0.07x | -1.801 us | 9 pairs; 41/47 ms; 343,148/24,684 ops/lane; IQR 1.003/1.100 | `Pcre2Literal` | 0.086 us | 0.050 us | 0.611 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 2.045 us | 0.06x | 0.06–0.06x | -1.922 us | 9 pairs; 40/50 ms; 324,193/24,522 ops/lane; IQR 1.003/1.034 | `Pcre2Literal` | 0.143 us | 0.055 us | 0.621 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.157 us | 2.452 us | 0.06x | 0.06–0.08x | -2.296 us | 9 pairs; 40/54 ms; 256,162/21,982 ops/lane; IQR 1.001/1.176 | `Pcre2Literal` | 0.204 us | 0.233 us | 0.884 us | 0.18x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.154 us | 1.884 us | 0.08x | 0.08–0.08x | -1.731 us | 9 pairs; 40/30 ms; 260,254/15,945 ops/lane; IQR 1.006/1.055 | `Pcre2Literal` | 0.171 us | 0.206 us | 0.755 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.097 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 40/39 ms; 4,531,639/402,617 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0.229 us | 0.069 us | 0.093 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.075 us | 0.085 us | 0.88x | 0.77–0.89x | -0.010 us | 9 pairs; 39/38 ms; 527,014/447,506 ops/lane; IQR 1.039/1.009 | `Pcre2Literal` | 0.124 us | 0.051 us | 0.075 us | 1.00x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.13–0.13x | -0.093 us | 9 pairs; 41/39 ms; 2,894,018/361,575 ops/lane; IQR 1.018/1.006 | `Pcre2Backtracking` | 0.275 us | 0.093 us | 0.117 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.122 us | 0.93x | 0.92–0.93x | -0.009 us | 9 pairs; 36/38 ms; 319,193/308,733 ops/lane; IQR 1.005/1.012 | `Pcre2Character` | 0.135 us | 0.112 us | 0.144 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.082 us | 0.202 us | 0.40x | 0.37–0.41x | -0.123 us | 9 pairs; 43/40 ms; 528,919/198,460 ops/lane; IQR 1.077/1.008 | `Pcre2Slice` | 0.413 us | 0.075 us | 0.113 us | 0.72x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.200 us | 160.358 us | 0.51x | 0.51–0.52x | -78.058 us | 9 pairs; 40/40 ms; 481/252 ops/lane; IQR 1.007/1.009 | `Pcre2Slice` | 332.837 us | 105.750 us | 106.848 us | 0.77x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.488 us | 289.647 us | 0.14x | 0.14–0.14x | -250.159 us | 9 pairs; 40/40 ms; 1,001/139 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 270.444 us | 219.714 us | 229.839 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.451 us | 209.727 us | 0.05x | 0.04–0.05x | -200.235 us | 9 pairs; 40/39 ms; 4,271/188 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 7.197 us | 225.292 us | 225.625 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 237.974 us | 206.127 us | 1.15x | 1.14–1.16x | +31.327 us | 9 pairs; 39/41 ms; 163/197 ops/lane; IQR 1.003/1.011 | `Pcre2SingleTokenRepeat` | 7.196 us | 235.295 us | 236.748 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.546 us | 23.291 us | 0.15x | 0.15–0.16x | -19.746 us | 9 pairs; 40/40 ms; 11,158/1,736 ops/lane; IQR 1.003/1.012 | `Pcre2Literal` | 1.377 us | 4.291 us | 6.083 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.574 us | 81.080 us | 0.12x | 0.12–0.12x | -71.505 us | 9 pairs; 40/40 ms; 4,160/497 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 9.319 us | 92.070 us | 93.785 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,237.312 us | 29,060.700 us | 0.15x | 0.14–0.15x | -24812.788 us | 9 pairs; 34/29 ms; 8/1 ops/lane; IQR 1.007/1.043 | `Pcre2Backtracking` | 6,100.300 us | 11,817.629 us | 18,954.450 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,397.142 us | 55,841.100 us | 0.06x | 0.06–0.06x | -52443.958 us | 9 pairs; 41/56 ms; 12/1 ops/lane; IQR 1.028/1.025 | `Pcre2Backtracking` | 3,685.600 us | 40,398.300 us | 47,699.850 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,933.581 us | 498,135.400 us | 0.00x | 0.00–0.00x | -496183.676 us | 9 pairs; 41/498 ms; 21/1 ops/lane; IQR 1.032/1.004 | `Pcre2Character` | 1,775.909 us | 23,185.200 us | 30,045.233 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,554.250 us | 12,007.833 us | 0.46x | 0.45–0.49x | -6452.500 us | 9 pairs; 33/36 ms; 6/3 ops/lane; IQR 1.001/1.098 | `Pcre2Literal` | 1,364.400 us | 1,366.886 us | 8,851.233 us | 0.63x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,878.300 us | 145,529.100 us | 0.13x | 0.13–0.13x | -126888.900 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 1,249.890 us | 530.155 us | 8,647.311 us | 2.18x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,920.175 us | 21,952.050 us | 0.22x | 0.22–0.23x | -17031.875 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.003/1.008 | `Pcre2Slice` | 2,430.535 us | 10,982.271 us | 19,146.400 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,372.900 us | 134,045.300 us | 0.17x | 0.17–0.17x | -111682.700 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 1,509.468 us | 2,326.476 us | 10,726.375 us | 2.09x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 167.234 us | 990.022 us | 0.17x | 0.17–0.17x | -823.041 us | 9 pairs; 40/36 ms; 240/36 ops/lane; IQR 1.005/1.018 | `Pcre2Backtracking` | 162.037 us | 320.990 us | 595.271 us | 0.28x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,216.267 us | 13,493.533 us | 0.91x | 0.90–0.91x | -1250.500 us | 9 pairs; 37/40 ms; 3/3 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 314.441 us | 15,189.340 us | 15,463.400 us | 0.79x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 3,038.392 us | 99,214.800 us | 0.03x | 0.03–0.03x | -96179.685 us | 9 pairs; 39/99 ms; 13/1 ops/lane; IQR 1.004/1.001 | `Pcre2Character` | 1,999.547 us | 19,951.625 us | 20,117.475 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 190.481 us | 2,805.207 us | 0.07x | 0.07–0.07x | -2614.741 us | 9 pairs; 38/39 ms; 201/14 ops/lane; IQR 1.002/1.010 | `Pcre2SingleTokenRepeat` | 188.465 us | 1,009.048 us | 1,248.286 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,592.940 us | 20,211.550 us | 0.08x | 0.08–0.08x | -18615.782 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 576.684 us | 10,430.487 us | 10,634.943 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.152 us | 1.162 us | 0.13x | 0.13–0.13x | -1.010 us | 9 pairs; 42/40 ms; 273,967/34,411 ops/lane; IQR 1.012/1.002 | `Pcre2Slice` | 0.647 us | 0.259 us | 0.282 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.518 us | 0.828 us | 0.63x | 0.62–0.63x | -0.311 us | 9 pairs; 42/41 ms; 80,729/49,115 ops/lane; IQR 1.008/1.003 | `Pcre2Literal` | 0.149 us | 0.150 us | 0.176 us | 2.95x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.304 us | 1.176 us | 0.26x | 0.26–0.26x | -0.872 us | 9 pairs; 41/40 ms; 135,099/33,733 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 1.066 us | 0.355 us | 0.382 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.865 us | 1.133 us | 0.76x | 0.76–0.77x | -0.266 us | 9 pairs; 24/40 ms; 27,770/35,483 ops/lane; IQR 1.002/1.005 | `Pcre2Character` | 0.287 us | 0.427 us | 0.470 us | 1.84x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.154 us | 0.864 us | 0.18x | 0.18–0.18x | -0.709 us | 9 pairs; 39/40 ms; 250,273/46,680 ops/lane; IQR 1.003/1.005 | `Pcre2Slice` | 0.774 us | 0.154 us | 0.201 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.954 us | 1.175 us | 0.81x | 0.81–0.81x | -0.221 us | 9 pairs; 39/40 ms; 41,200/34,160 ops/lane; IQR 1.002/1.001 | `Pcre2Slice` | 2.772 us | 0.305 us | 0.324 us | 2.95x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.796 us | 0.832 us | 0.96x | 0.95–0.96x | -0.036 us | 9 pairs; 40/40 ms; 50,441/48,105 ops/lane; IQR 1.001/1.002 | `Pcre2Literal` | 3.618 us | 0.182 us | 0.204 us | 3.90x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.978 us | 1.185 us | 0.83x | 0.82–0.83x | -0.204 us | 9 pairs; 39/39 ms; 40,349/32,925 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 5.320 us | 0.414 us | 0.435 us | 2.25x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.209 us | 1.147 us | 1.05x | 1.05–1.06x | +0.062 us | 9 pairs; 40/41 ms; 33,314/35,326 ops/lane; IQR 1.002/1.004 | `Pcre2Character` | 3.695 us | 0.477 us | 0.517 us | 2.34x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.802 us | 0.860 us | 0.94x | 0.93–0.94x | -0.056 us | 9 pairs; 40/39 ms; 50,437/45,911 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 2.112 us | 0.183 us | 0.226 us | 3.56x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.567 us | 1.174 us | 0.48x | 0.48–0.49x | -0.607 us | 9 pairs; 37/40 ms; 65,797/34,065 ops/lane; IQR 1.004/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.511 us | 0.833 us | 0.61x | 0.61–0.61x | -0.322 us | 9 pairs; 42/40 ms; 81,701/48,294 ops/lane; IQR 1.001/1.003 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.686 us | 1.187 us | 0.58x | 0.58–0.58x | -0.499 us | 9 pairs; 41/39 ms; 59,708/32,477 ops/lane; IQR 1.008/1.001 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.919 us | 1.149 us | 0.80x | 0.80–0.80x | -0.230 us | 9 pairs; 42/40 ms; 45,486/34,836 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.532 us | 0.860 us | 0.62x | 0.61–0.62x | -0.328 us | 9 pairs; 39/40 ms; 74,159/46,330 ops/lane; IQR 1.006/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.579 us | — | — | — | — | — | `—` | 25.736 us | 100.192 us | 101.693 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.448 us | — | — | — | — | — | `—` | 1.589 us | 0.362 us | 0.390 us | 11.40x | 4,176 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.089 us | — | — | — | — | — | `—` | 0.936 us | 0.242 us | 0.275 us | 3.97x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.256 us | — | — | — | — | — | `—` | 3.154 us | 0.466 us | 0.518 us | 2.43x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.538 us | — | — | — | — | — | `—` | 1.025 us | 0.563 us | 0.584 us | 2.63x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.692 us | — | — | — | — | — | `—` | 1.516 us | 0.274 us | 0.328 us | 11.25x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.097 us | 0.086 us | 1.13x | 1.13–1.14x | +0.011 us | 9 pairs; 43/39 ms; 443,605/460,584 ops/lane; IQR 1.001/1.005 | `Pcre2Character` | 0 B | 0 B | 3.275 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.118 us | 0.11x | 0.11–0.11x | -0.105 us | 9 pairs; 40/40 ms; 3,038,177/343,126 ops/lane; IQR 1.005/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 132.606 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.389 us | 0.105 us | 3.71x | 3.66–3.72x | +0.284 us | 9 pairs; 36/39 ms; 92,619/369,457 ops/lane; IQR 1.005/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 20.562 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.393 us | 0.115 us | 3.41x | 3.34–3.43x | +0.278 us | 9 pairs; 36/38 ms; 91,323/332,989 ops/lane; IQR 1.004/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 71.119 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.372 us | 0.104 us | 3.58x | 3.51–3.60x | +0.268 us | 9 pairs; 35/39 ms; 95,487/373,201 ops/lane; IQR 1.005/1.028 | `Pcre2Backtracking` | 0 B | 0 B | 72.000 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.667 us | 0.348 us | 1.92x | 1.91–1.95x | +0.319 us | 9 pairs; 38/39 ms; 56,382/112,569 ops/lane; IQR 1.014/1.002 | `Pcre2Character` | 0 B | 0 B | 1.494 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.640 us | 0.02x | 0.02–0.02x | -0.629 us | 9 pairs; 40/39 ms; 3,490,754/60,708 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 22.006 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.512 us | 0.02x | 0.02–0.02x | -0.500 us | 9 pairs; 39/41 ms; 3,377,419/79,832 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 22.087 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.467 us | 0.109 us | 4.29x | 4.20–4.33x | +0.358 us | 9 pairs; 37/40 ms; 79,073/369,175 ops/lane; IQR 1.004/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 19.850 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.339 us | 10.464 us | 0.80x | 0.79–0.80x | -2.108 us | 9 pairs; 40/38 ms; 4,755/3,678 ops/lane; IQR 1.002/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.302 us | 1.055 us | 0.29x | 0.29–0.29x | -0.752 us | 9 pairs; 33/39 ms; 107,644/37,219 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 132.006 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.307 us | 0.482 us | 0.64x | 0.63–0.64x | -0.176 us | 9 pairs; 36/37 ms; 117,001/77,032 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 117.281 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.299 us | 0.464 us | 0.64x | 0.64–0.65x | -0.166 us | 9 pairs; 33/40 ms; 112,076/86,776 ops/lane; IQR 1.014/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 143.769 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.292 us | 0.592 us | 0.49x | 0.49–0.50x | -0.299 us | 9 pairs; 34/40 ms; 115,078/67,167 ops/lane; IQR 1.005/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 104.756 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.296 us | 0.380 us | 0.78x | 0.77–0.79x | -0.084 us | 9 pairs; 35/42 ms; 118,581/111,068 ops/lane; IQR 1.002/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 220.719 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.001 us | 8.825 us | 0.57x | 0.57–0.57x | -3.822 us | 9 pairs; 40/40 ms; 8,024/4,526 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0 B | 0 B | 1.500 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.262 us | 0.311 us | 0.85x | 0.84–0.85x | -0.048 us | 9 pairs; 36/41 ms; 136,501/132,023 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 92.519 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.273 us | 0.405 us | 0.68x | 0.65–0.68x | -0.131 us | 9 pairs; 37/39 ms; 136,820/96,754 ops/lane; IQR 1.021/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 124.819 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.816 us | — | — | — | — | — | `—` | 0 B | — | 14.194 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.197 us | 0.293 us | 0.67x | 0.66–0.67x | -0.097 us | 9 pairs; 25/40 ms; 128,098/136,827 ops/lane; IQR 1.003/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 53.794 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.708 us | — | — | — | — | — | `—` | 0 B | — | 15.281 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.575 us | 0.674 us | 0.85x | 0.85–0.86x | -0.098 us | 9 pairs; 31/39 ms; 54,575/58,336 ops/lane; IQR 1.007/1.003 | `Pcre2Slice` | 0 B | 0 B | 64.700 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.915 us | 29.132 us | 0.34x | 0.34–0.34x | -19.213 us | 9 pairs; 40/40 ms; 4,011/1,373 ops/lane; IQR 1.002/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.984 us | 1.070 us | 0.92x | 0.92–0.92x | -0.087 us | 9 pairs; 41/40 ms; 41,529/37,490 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 130.531 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.045 us | 1.195 us | 0.88x | 0.87–0.88x | -0.147 us | 9 pairs; 40/41 ms; 38,377/34,280 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 117.438 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.993 us | 1.034 us | 0.96x | 0.96–0.96x | -0.040 us | 9 pairs; 41/40 ms; 40,900/38,834 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 143.300 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.959 us | 1.169 us | 0.82x | 0.82–0.82x | -0.210 us | 9 pairs; 40/40 ms; 41,992/34,252 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 103.831 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.940 us | 0.956 us | 0.98x | 0.98–0.99x | -0.015 us | 9 pairs; 39/40 ms; 41,193/42,063 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 221.806 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.600 us | 8.873 us | 0.63x | 0.63–0.63x | -3.273 us | 9 pairs; 40/40 ms; 7,193/4,515 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 0 B | 0 B | 2.100 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.922 us | 0.891 us | 1.04x | 1.03–1.04x | +0.033 us | 9 pairs; 40/40 ms; 42,970/44,569 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 93.919 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.924 us | 0.966 us | 0.96x | 0.96–0.96x | -0.041 us | 9 pairs; 40/40 ms; 42,926/41,525 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 124.131 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.981 us | — | — | — | — | — | `—` | 0 B | — | 14.425 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.839 us | 0.871 us | 0.97x | 0.96–0.97x | -0.030 us | 9 pairs; 39/40 ms; 46,675/46,028 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 54.394 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.914 us | — | — | — | — | — | `—` | 0 B | — | 15.125 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.848 us | 1.256 us | 0.67x | 0.67–0.68x | -0.411 us | 9 pairs; 40/40 ms; 46,705/31,681 ops/lane; IQR 1.002/1.008 | `Pcre2Slice` | 0 B | 0 B | 65.519 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.497 us | 2.305 us | 0.65x | 0.65–0.65x | -0.808 us | 9 pairs; 40/40 ms; 26,841/17,423 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.631 us | 1.070 us | 0.59x | 0.59–0.59x | -0.439 us | 9 pairs; 40/40 ms; 63,251/37,468 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 129.856 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.681 us | 1.193 us | 0.57x | 0.57–0.57x | -0.512 us | 9 pairs; 40/40 ms; 58,342/33,553 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 115.100 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.615 us | 1.034 us | 0.59x | 0.59–0.60x | -0.419 us | 9 pairs; 40/39 ms; 64,560/38,005 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 141.588 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.639 us | 1.166 us | 0.55x | 0.55–0.55x | -0.528 us | 9 pairs; 40/41 ms; 63,095/34,827 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 104.344 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.611 us | 0.940 us | 0.65x | 0.65–0.65x | -0.329 us | 9 pairs; 40/41 ms; 64,731/43,263 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 221.031 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.904 us | 2.191 us | 0.87x | 0.87–0.87x | -0.285 us | 9 pairs; 40/40 ms; 20,834/18,252 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0 B | 0 B | 1.394 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.604 us | 0.888 us | 0.68x | 0.68–0.68x | -0.285 us | 9 pairs; 40/40 ms; 66,235/44,817 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 92.987 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.653 us | 0.968 us | 0.67x | 0.67–0.68x | -0.316 us | 9 pairs; 40/40 ms; 61,260/41,453 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 123.756 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.732 us | — | — | — | — | — | `—` | 0 B | — | 13.650 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.497 us | 0.869 us | 0.57x | 0.57–0.57x | -0.371 us | 9 pairs; 40/41 ms; 81,193/47,314 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.969 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.643 us | — | — | — | — | — | `—` | 0 B | — | 14.919 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.535 us | 1.262 us | 0.42x | 0.42–0.43x | -0.726 us | 9 pairs; 40/39 ms; 74,885/31,272 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 0 B | 0 B | 65.444 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.028 us | — | — | — | — | — | `—` | 480 B | — | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.158 us | — | — | — | — | — | `—` | 48 B | — | 129.831 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.267 us | — | — | — | — | — | `—` | 48 B | — | 118.431 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.152 us | — | — | — | — | — | `—` | 48 B | — | 143.681 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.159 us | — | — | — | — | — | `—` | 48 B | — | 101.088 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.147 us | — | — | — | — | — | `—` | 48 B | — | 222.694 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.099 us | — | — | — | — | — | `—` | 64 B | — | 1.475 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.168 us | — | — | — | — | — | `—` | 40 B | — | 94.144 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.188 us | — | — | — | — | — | `—` | 40 B | — | 122.506 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.026 us | — | — | — | — | — | `—` | 1,216 B | — | 13.750 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.092 us | — | — | — | — | — | `—` | 48 B | — | 54.150 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.359 us | — | — | — | — | — | `—` | 928 B | — | 13.969 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.838 us | — | — | — | — | — | `—` | 5,392 B | — | 65.475 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 34.466–260.401 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.451–95.537 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.610–12.739 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.069–49.364 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.034–1.161 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 6.064–47.545 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.788–22.639 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.174–99.887 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.120–74.698 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.222–5.429 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.156–376.012 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 41.121–321.339 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.834–5.084 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.100–15.416 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 108.767–845.270 us |

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
