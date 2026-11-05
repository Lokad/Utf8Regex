<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `DCA5DEFB81EC721DDC1BC5A3D331C4226909D81D92C3F2AD00AB60CF3DA763DA`
- Latest managed row measurement: `2026-11-05T14:57:43.6217647+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T14:57:43.6217647+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `2` equivalent, `12` native faster, `11` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `fd8c2d7e2586` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `fd8c2d7e2586` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.822 us | 2.401 us | 0.34x | 0.32–0.38x | -1.570 us | 9 pairs; 35/44 ms; 42,594/18,179 ops/lane; IQR 1.056/1.158 | `Pcre2Slice` | 469.043 us | 0.287 us | 0.877 us | 0.94x | 2,784 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.077 us | 11.791 us | 0.09x | 0.09–0.11x | -10.698 us | 9 pairs; 37/46 ms; 34,253/3,865 ops/lane; IQR 1.013/1.242 | `Pcre2Slice` | 0.850 us | 0.099 us | 3.774 us | 0.29x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.840 us | 0.01x | 0.01–0.01x | -1.829 us | 9 pairs; 40/40 ms; 3,620,773/21,735 ops/lane; IQR 1.010/1.052 | `Pcre2Slice` | 0.102 us | 1.094 us | 1.156 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.692 us | 0.160 us | 4.32x | 4.32–4.35x | +0.532 us | 9 pairs; 37/40 ms; 53,594/249,493 ops/lane; IQR 1.010/1.010 | `Pcre2Backtracking` | 0.049 us | 0.126 us | 0.163 us | 4.25x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.310 us | 0.574 us | 4.02x | 4.00–4.06x | +1.732 us | 9 pairs; 39/40 ms; 16,829/70,083 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0.118 us | 0.738 us | 0.777 us | 2.97x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.102 us | 0.298 us | 0.34x | 0.34–0.34x | -0.197 us | 9 pairs; 41/39 ms; 404,567/131,574 ops/lane; IQR 1.003/1.015 | `Pcre2Slice` | 0.112 us | 0.319 us | 0.345 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.358 us | 0.29x | 0.29–0.30x | -0.253 us | 9 pairs; 39/41 ms; 373,179/113,324 ops/lane; IQR 1.005/1.015 | `Pcre2Slice` | 0.115 us | 0.444 us | 0.471 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.082 us | 0.294 us | 0.28x | 0.28–0.28x | -0.211 us | 9 pairs; 41/40 ms; 502,731/135,379 ops/lane; IQR 1.005/1.012 | `Pcre2Slice` | 0.087 us | 0.244 us | 0.260 us | 0.32x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.078 us | 0.316 us | 0.25x | 0.24–0.25x | -0.238 us | 9 pairs; 39/40 ms; 500,128/127,254 ops/lane; IQR 1.026/1.006 | `Pcre2Slice` | 0.087 us | 0.259 us | 0.278 us | 0.28x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.078 us | 2.23x | 2.18–2.29x | +0.096 us | 9 pairs; 40/40 ms; 231,880/509,319 ops/lane; IQR 1.004/1.031 | `Pcre2Slice` | 0.086 us | 0.965 us | 1.009 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.200 us | 0.17x | 0.17–0.17x | -0.167 us | 9 pairs; 40/40 ms; 1,198,913/200,828 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0.105 us | 0.307 us | 0.348 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.608 us | 0.10x | 0.10–0.10x | -0.547 us | 9 pairs; 40/39 ms; 655,776/64,705 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0.131 us | 0.234 us | 0.258 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.156 us | 0.21x | 0.21–0.22x | -0.123 us | 9 pairs; 38/40 ms; 1,141,061/255,775 ops/lane; IQR 1.020/1.004 | `Pcre2Backtracking` | 0.106 us | 0.185 us | 0.204 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.022 us | 0.114 us | 0.19x | 0.18–0.19x | -0.093 us | 9 pairs; 42/41 ms; 1,958,857/361,575 ops/lane; IQR 1.027/1.011 | `Pcre2Backtracking` | 0.059 us | 0.093 us | 0.112 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.122 us | 0.48x | 0.47–0.48x | -0.063 us | 9 pairs; 40/40 ms; 691,691/332,853 ops/lane; IQR 1.004/1.011 | `Pcre2Backtracking` | 0.015 us | 0.090 us | 0.107 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.083 us | 0.281 us | 0.30x | 0.30–0.30x | -0.197 us | 9 pairs; 39/40 ms; 472,194/143,238 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | 0.093 us | 0.120 us | 0.156 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.115 us | 0.21x | 0.20–0.21x | -0.091 us | 9 pairs; 39/40 ms; 1,623,786/350,996 ops/lane; IQR 1.021/1.006 | `Pcre2Backtracking` | 0.019 us | 0.095 us | 0.114 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.266 us | 0.05x | 0.05–0.07x | -2.149 us | 9 pairs; 40/51 ms; 339,925/22,598 ops/lane; IQR 1.003/1.208 | `Pcre2Literal` | 0.088 us | 0.053 us | 0.643 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 2.279 us | 0.05x | 0.04–0.07x | -2.156 us | 9 pairs; 40/36 ms; 331,147/15,806 ops/lane; IQR 1.002/1.441 | `Pcre2Literal` | 0.145 us | 0.057 us | 0.695 us | 0.18x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 1.911 us | 0.08x | 0.07–0.08x | -1.754 us | 9 pairs; 40/40 ms; 257,322/20,757 ops/lane; IQR 1.011/1.034 | `Pcre2Literal` | 0.205 us | 0.239 us | 0.926 us | 0.17x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.151 us | 1.790 us | 0.09x | 0.08–0.09x | -1.635 us | 9 pairs; 40/37 ms; 263,710/20,503 ops/lane; IQR 1.021/1.027 | `Pcre2Literal` | 0.178 us | 0.210 us | 0.798 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.015 us | 0.098 us | 0.16x | 0.09–0.16x | -0.083 us | 9 pairs; 70/39 ms; 4,505,547/400,953 ops/lane; IQR 1.370/1.017 | `Pcre2Slice` | 0.265 us | 0.073 us | 0.090 us | 0.17x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.086 us | 0.78x | 0.77–0.79x | -0.019 us | 9 pairs; 40/40 ms; 590,296/467,000 ops/lane; IQR 1.001/1.016 | `Pcre2Literal` | 0.121 us | 0.054 us | 0.074 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.113 us | 0.13x | 0.12–0.13x | -0.098 us | 9 pairs; 41/40 ms; 2,893,741/359,444 ops/lane; IQR 1.025/1.007 | `Pcre2Backtracking` | 0.238 us | 0.092 us | 0.113 us | 0.13x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.122 us | 0.94x | 0.94–0.94x | -0.007 us | 9 pairs; 38/41 ms; 329,847/334,131 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 0.133 us | 0.117 us | 0.143 us | 0.80x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.202 us | 0.38x | 0.37–0.38x | -0.126 us | 9 pairs; 40/38 ms; 532,149/189,832 ops/lane; IQR 1.002/1.012 | `Pcre2Slice` | 0.435 us | 0.078 us | 0.112 us | 0.68x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.254 us | 161.416 us | 0.51x | 0.51–0.51x | -79.189 us | 9 pairs; 40/40 ms; 486/245 ops/lane; IQR 1.005/1.006 | `Pcre2Slice` | 333.605 us | 104.490 us | 106.299 us | 0.77x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.596 us | 291.786 us | 0.13x | 0.13–0.13x | -254.288 us | 9 pairs; 40/41 ms; 1,059/140 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 287.210 us | 218.193 us | 228.807 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.430 us | 213.249 us | 0.04x | 0.04–0.04x | -203.816 us | 9 pairs; 40/40 ms; 4,247/188 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 8.217 us | 220.137 us | 220.853 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 242.068 us | 206.619 us | 1.17x | 1.17–1.18x | +35.449 us | 9 pairs; 40/39 ms; 167/187 ops/lane; IQR 1.002/1.006 | `Pcre2SingleTokenRepeat` | 8.268 us | 235.660 us | 237.281 us | 1.02x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.543 us | 23.354 us | 0.15x | 0.15–0.16x | -19.809 us | 9 pairs; 39/40 ms; 11,061/1,725 ops/lane; IQR 1.006/1.024 | `Pcre2Literal` | 1.388 us | 4.271 us | 6.063 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.592 us | 82.194 us | 0.12x | 0.12–0.12x | -72.584 us | 9 pairs; 40/41 ms; 4,153/500 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 9.346 us | 91.357 us | 92.936 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,177.300 us | 28,240.000 us | 0.15x | 0.14–0.15x | -24062.700 us | 9 pairs; 42/28 ms; 10/1 ops/lane; IQR 1.014/1.031 | `Pcre2Backtracking` | 5,397.870 us | 11,688.820 us | 18,277.633 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,480.133 us | 55,610.300 us | 0.06x | 0.06–0.06x | -52116.342 us | 9 pairs; 42/56 ms; 12/1 ops/lane; IQR 1.040/1.008 | `Pcre2Backtracking` | 3,752.843 us | 40,943.350 us | 47,805.150 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,946.576 us | 474,381.600 us | 0.00x | 0.00–0.00x | -472432.286 us | 9 pairs; 41/474 ms; 21/1 ops/lane; IQR 1.037/1.003 | `Pcre2Character` | 1,858.311 us | 23,517.000 us | 30,575.850 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,554.600 us | 11,090.633 us | 0.50x | 0.47–0.52x | -5519.400 us | 9 pairs; 33/33 ms; 6/3 ops/lane; IQR 1.005/1.074 | `Pcre2Literal` | 1,420.363 us | 1,396.246 us | 8,702.633 us | 0.64x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,817.650 us | 145,830.000 us | 0.13x | 0.13–0.13x | -126962.000 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.015/1.009 | `Pcre2Backtracking` | 1,275.880 us | 535.241 us | 8,557.600 us | 2.20x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,760.500 us | 21,881.800 us | 0.22x | 0.22–0.22x | -17121.300 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.001/1.011 | `Pcre2Slice` | 2,514.780 us | 10,953.080 us | 18,894.567 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,226.000 us | 133,693.600 us | 0.17x | 0.17–0.17x | -111437.250 us | 9 pairs; 44/134 ms; 2/1 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 1,536.239 us | 2,344.023 us | 10,427.060 us | 2.13x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 164.419 us | 983.782 us | 0.17x | 0.17–0.17x | -819.406 us | 9 pairs; 40/38 ms; 243/39 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 164.432 us | 316.787 us | 536.291 us | 0.31x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,614.733 us | 13,384.800 us | 0.94x | 0.94–0.95x | -759.933 us | 9 pairs; 38/40 ms; 3/3 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 306.994 us | 15,046.350 us | 15,244.125 us | 0.83x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 3,017.100 us | 99,383.600 us | 0.03x | 0.03–0.03x | -96373.885 us | 9 pairs; 39/99 ms; 13/1 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 2,024.268 us | 19,863.833 us | 20,183.000 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.398 us | 2,834.529 us | 0.07x | 0.07–0.07x | -2649.288 us | 9 pairs; 40/40 ms; 214/14 ops/lane; IQR 1.003/1.003 | `Pcre2SingleTokenRepeat` | 191.824 us | 1,021.088 us | 1,468.763 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,552.228 us | 20,170.800 us | 0.08x | 0.08–0.08x | -18618.048 us | 9 pairs; 39/40 ms; 25/2 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 576.760 us | 10,612.580 us | 10,840.400 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.155 us | 1.097 us | 0.14x | 0.14–0.14x | -0.942 us | 9 pairs; 41/40 ms; 264,823/36,678 ops/lane; IQR 1.008/1.000 | `Pcre2Slice` | 0.686 us | 0.261 us | 0.281 us | 0.55x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.490 us | 0.830 us | 0.59x | 0.59–0.59x | -0.340 us | 9 pairs; 40/40 ms; 82,006/47,628 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | 0.144 us | 0.148 us | 0.173 us | 2.83x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.299 us | 1.178 us | 0.25x | 0.25–0.25x | -0.878 us | 9 pairs; 40/40 ms; 132,349/33,864 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 1.124 us | 0.346 us | 0.376 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.916 us | 1.129 us | 0.81x | 0.81–0.82x | -0.212 us | 9 pairs; 40/40 ms; 44,173/35,440 ops/lane; IQR 1.005/1.007 | `Pcre2Character` | 0.289 us | 0.427 us | 0.473 us | 1.94x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.165 us | 0.856 us | 0.19x | 0.18–0.20x | -0.690 us | 9 pairs; 39/40 ms; 238,486/46,870 ops/lane; IQR 1.015/1.002 | `Pcre2Slice` | 0.819 us | 0.155 us | 0.202 us | 0.82x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.891 us | 1.105 us | 0.81x | 0.80–0.81x | -0.215 us | 9 pairs; 40/40 ms; 44,840/36,346 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 3.837 us | 0.306 us | 0.325 us | 2.74x | 0 B | 0 B |
| `simple/foo-dense` | **Equivalent** | 18 B | 0.816 us | 0.831 us | 0.98x | 0.98–0.98x | -0.016 us | 9 pairs; 41/40 ms; 50,371/48,304 ops/lane; IQR 1.002/1.001 | `Pcre2Literal` | 3.603 us | 0.182 us | 0.203 us | 4.01x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.978 us | 1.189 us | 0.82x | 0.82–0.82x | -0.210 us | 9 pairs; 40/40 ms; 40,663/33,519 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 5.312 us | 0.402 us | 0.424 us | 2.31x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.172 us | 1.134 us | 1.04x | 1.03–1.04x | +0.040 us | 9 pairs; 39/40 ms; 33,008/35,239 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 3.684 us | 0.484 us | 0.530 us | 2.21x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.796 us | 0.858 us | 0.93x | 0.93–0.93x | -0.060 us | 9 pairs; 40/40 ms; 50,096/46,095 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | 2.129 us | 0.183 us | 0.225 us | 3.55x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.584 us | 1.104 us | 0.53x | 0.53–0.53x | -0.521 us | 9 pairs; 41/40 ms; 69,984/36,235 ops/lane; IQR 1.002/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.516 us | 0.834 us | 0.62x | 0.62–0.63x | -0.318 us | 9 pairs; 41/40 ms; 79,461/48,015 ops/lane; IQR 1.006/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.781 us | 1.189 us | 0.66x | 0.65–0.66x | -0.408 us | 9 pairs; 45/40 ms; 58,119/33,711 ops/lane; IQR 1.004/1.001 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.908 us | 1.133 us | 0.80x | 0.80–0.80x | -0.225 us | 9 pairs; 38/40 ms; 42,262/35,697 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.477 us | 0.856 us | 0.56x | 0.55–0.56x | -0.379 us | 9 pairs; 40/39 ms; 84,249/45,839 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 65.600 us | — | — | — | — | — | `—` | 25.996 us | 99.138 us | 99.798 us | 0.66x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.442 us | — | — | — | — | — | `—` | 1.752 us | 0.364 us | 0.391 us | 16.50x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.047 us | — | — | — | — | — | `—` | 0.931 us | 0.244 us | 0.272 us | 3.85x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.200 us | — | — | — | — | — | `—` | 3.125 us | 0.455 us | 0.485 us | 2.47x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.448 us | — | — | — | — | — | `—` | 1.039 us | 0.540 us | 0.587 us | 2.47x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.850 us | — | — | — | — | — | `—` | 1.539 us | 0.271 us | 0.321 us | 11.98x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.087 us | 0.084 us | 1.05x | 1.04–1.05x | +0.004 us | 9 pairs; 37/39 ms; 427,621/463,487 ops/lane; IQR 1.005/1.013 | `Pcre2Character` | 0 B | 0 B | 2.244 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.120 us | 0.11x | 0.11–0.11x | -0.107 us | 9 pairs; 39/40 ms; 3,029,516/334,988 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 127.900 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.391 us | 0.104 us | 3.78x | 3.65–3.79x | +0.287 us | 9 pairs; 37/39 ms; 94,135/378,979 ops/lane; IQR 1.016/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 19.900 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.395 us | 0.116 us | 3.40x | 3.32–3.44x | +0.279 us | 9 pairs; 37/41 ms; 92,502/356,440 ops/lane; IQR 1.013/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 71.200 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.377 us | 0.105 us | 3.59x | 3.57–3.61x | +0.272 us | 9 pairs; 36/40 ms; 94,574/385,119 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 70.175 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.586 us | 0.345 us | 1.70x | 1.69–1.72x | +0.241 us | 9 pairs; 39/39 ms; 66,185/113,009 ops/lane; IQR 1.006/1.011 | `Pcre2Character` | 0 B | 0 B | 1.488 us | 2,248 B |
| `pcre2/recursive-optional` | **Inconclusive** | 6 B | 0.017 us | 0.630 us | 0.03x | 0.02–0.03x | -0.613 us | 9 pairs; 57/39 ms; 3,425,823/62,293 ops/lane; IQR 1.188/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 21.656 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.016 us | 0.539 us | 0.03x | 0.02–0.03x | -0.523 us | 9 pairs; 58/39 ms; 3,518,082/72,135 ops/lane; IQR 1.168/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 21.587 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.463 us | 0.109 us | 4.28x | 4.23–4.29x | +0.356 us | 9 pairs; 37/40 ms; 80,404/364,197 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 18.225 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.459 us | 10.673 us | 0.79x | 0.79–0.79x | -2.219 us | 9 pairs; 40/40 ms; 4,718/3,702 ops/lane; IQR 1.005/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.302 us | 1.051 us | 0.29x | 0.29–0.29x | -0.748 us | 9 pairs; 41/41 ms; 136,397/38,605 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 131.419 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.347 us | 0.469 us | 0.74x | 0.67–0.75x | -0.123 us | 9 pairs; 41/38 ms; 118,599/81,651 ops/lane; IQR 1.097/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 114.131 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.287 us | 0.467 us | 0.62x | 0.61–0.63x | -0.178 us | 9 pairs; 37/40 ms; 127,425/85,650 ops/lane; IQR 1.009/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 142.506 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.298 us | 0.588 us | 0.51x | 0.50–0.51x | -0.292 us | 9 pairs; 34/40 ms; 112,527/67,432 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 102.756 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.319 us | 0.399 us | 0.80x | 0.72–0.81x | -0.081 us | 9 pairs; 41/37 ms; 129,767/92,607 ops/lane; IQR 1.080/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 215.069 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.804 us | 8.880 us | 0.54x | 0.54–0.54x | -4.074 us | 9 pairs; 40/40 ms; 8,255/4,510 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.550 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.261 us | 0.312 us | 0.84x | 0.83–0.84x | -0.051 us | 9 pairs; 37/40 ms; 140,528/127,860 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 91.338 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.279 us | 0.410 us | 0.68x | 0.66–0.68x | -0.131 us | 9 pairs; 38/36 ms; 135,061/87,533 ops/lane; IQR 1.019/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 124.600 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.912 us | — | — | — | — | — | `—` | 0 B | — | 13.331 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.187 us | 0.294 us | 0.64x | 0.63–0.64x | -0.107 us | 9 pairs; 40/40 ms; 213,332/134,600 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 53.644 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.787 us | — | — | — | — | — | `—` | 0 B | — | 15.231 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.548 us | 0.674 us | 0.81x | 0.79–0.82x | -0.127 us | 9 pairs; 38/40 ms; 68,994/59,909 ops/lane; IQR 1.008/1.007 | `Pcre2Slice` | 0 B | 0 B | 65.956 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.850 us | 28.966 us | 0.34x | 0.34–0.34x | -19.105 us | 9 pairs; 40/40 ms; 4,021/1,392 ops/lane; IQR 1.004/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.954 us | 1.066 us | 0.89x | 0.89–0.90x | -0.114 us | 9 pairs; 39/39 ms; 41,245/37,026 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 132.844 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.038 us | 1.201 us | 0.86x | 0.86–0.87x | -0.165 us | 9 pairs; 38/40 ms; 36,963/33,309 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 116.469 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.949 us | 1.031 us | 0.92x | 0.92–0.93x | -0.082 us | 9 pairs; 40/40 ms; 42,345/38,325 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 142.106 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.964 us | 1.174 us | 0.82x | 0.82–0.82x | -0.211 us | 9 pairs; 40/40 ms; 41,504/34,195 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 103.575 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.942 us | 0.949 us | 0.99x | 0.99–1.00x | -0.006 us | 9 pairs; 37/41 ms; 39,189/42,784 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 215.363 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.814 us | 8.874 us | 0.66x | 0.65–0.66x | -3.061 us | 9 pairs; 42/40 ms; 7,233/4,498 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 0 B | 0 B | 1.506 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.943 us | 0.891 us | 1.06x | 1.06–1.06x | +0.052 us | 9 pairs; 41/40 ms; 43,249/45,049 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 92.000 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.920 us | 0.988 us | 0.93x | 0.93–0.93x | -0.068 us | 9 pairs; 40/40 ms; 43,064/40,510 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 122.537 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.144 us | — | — | — | — | — | `—` | 0 B | — | 13.369 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.831 us | 0.869 us | 0.96x | 0.95–0.96x | -0.038 us | 9 pairs; 40/40 ms; 47,782/46,312 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.650 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.002 us | — | — | — | — | — | `—` | 0 B | — | 13.812 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.847 us | 1.252 us | 0.68x | 0.67–0.68x | -0.406 us | 9 pairs; 39/40 ms; 46,171/32,255 ops/lane; IQR 1.004/1.002 | `Pcre2Slice` | 0 B | 0 B | 64.625 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.364 us | 2.262 us | 0.60x | 0.60–0.61x | -0.894 us | 9 pairs; 40/40 ms; 29,364/17,705 ops/lane; IQR 1.005/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.781 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.670 us | 1.070 us | 0.63x | 0.62–0.63x | -0.400 us | 9 pairs; 38/40 ms; 56,491/36,993 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 131.750 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.684 us | 1.205 us | 0.57x | 0.57–0.57x | -0.521 us | 9 pairs; 40/40 ms; 58,626/33,254 ops/lane; IQR 1.000/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 114.112 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.656 us | 1.032 us | 0.63x | 0.63–0.64x | -0.378 us | 9 pairs; 40/41 ms; 60,504/39,686 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 140.419 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.669 us | 1.174 us | 0.57x | 0.57–0.57x | -0.506 us | 9 pairs; 40/40 ms; 60,032/33,996 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 102.950 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.665 us | 0.948 us | 0.70x | 0.70–0.71x | -0.283 us | 9 pairs; 40/40 ms; 60,686/42,180 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 217.019 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.806 us | 2.194 us | 0.82x | 0.82–0.83x | -0.385 us | 9 pairs; 40/40 ms; 22,263/18,270 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.610 us | 0.889 us | 0.69x | 0.68–0.69x | -0.280 us | 9 pairs; 38/40 ms; 62,690/45,013 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 93.931 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.654 us | 0.990 us | 0.66x | 0.66–0.66x | -0.335 us | 9 pairs; 42/40 ms; 63,637/40,663 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 121.831 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.811 us | — | — | — | — | — | `—` | 0 B | — | 13.406 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.532 us | 0.867 us | 0.61x | 0.61–0.61x | -0.335 us | 9 pairs; 38/40 ms; 70,557/46,556 ops/lane; IQR 1.005/1.000 | `Pcre2Backtracking` | 0 B | 0 B | 53.550 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.677 us | — | — | — | — | — | `—` | 0 B | — | 13.850 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.576 us | 1.256 us | 0.46x | 0.45–0.46x | -0.679 us | 9 pairs; 40/40 ms; 68,959/31,804 ops/lane; IQR 1.006/1.002 | `Pcre2Slice` | 0 B | 0 B | 63.900 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.789 us | — | — | — | — | — | `—` | 480 B | — | 1.188 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.107 us | — | — | — | — | — | `—` | 48 B | — | 130.250 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.202 us | — | — | — | — | — | `—` | 48 B | — | 117.044 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.088 us | — | — | — | — | — | `—` | 48 B | — | 142.919 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.109 us | — | — | — | — | — | `—` | 48 B | — | 100.856 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.091 us | — | — | — | — | — | `—` | 48 B | — | 219.956 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.023 us | — | — | — | — | — | `—` | 64 B | — | 1.375 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.124 us | — | — | — | — | — | `—` | 40 B | — | 93.825 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.101 us | — | — | — | — | — | `—` | 40 B | — | 122.669 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.975 us | — | — | — | — | — | `—` | 1,216 B | — | 13.637 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.035 us | — | — | — | — | — | `—` | 48 B | — | 52.975 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.631 us | — | — | — | — | — | `—` | 928 B | — | 13.475 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 6.071 us | — | — | — | — | — | `—` | 17,936 B | — | 63.756 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 32.819–262.459 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.447–94.895 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.065–12.678 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.824–47.443 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.033–1.152 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.387–41.914 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.765–22.717 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.225–100.021 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.137–72.840 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.240–5.707 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.249–378.605 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.081 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.085–304.399 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.886–5.142 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.175–15.743 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 115.483–898.180 us |

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
