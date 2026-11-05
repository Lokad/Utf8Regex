<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `CCFE4FADE83BC36AE2F467A1B2124355C0ACCC12C39C8921AD7F65E200120357`
- Latest managed row measurement: `2026-11-05T04:59:00.1536899+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T04:59:00.1536899+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `73` managed faster, `4` equivalent, `9` native faster, `15` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x1`, `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `a88922d63f64` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `a88922d63f64` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.860 us | 2.329 us | 0.39x | 0.33–0.43x | -1.425 us | 9 pairs; 32/46 ms; 37,367/19,907 ops/lane; IQR 1.023/1.133 | `Pcre2Slice` | 559.675 us | 0.280 us | 0.778 us | 1.11x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.102 us | 11.205 us | 0.10x | 0.07–0.11x | -10.122 us | 9 pairs; 36/36 ms; 32,282/3,235 ops/lane; IQR 1.044/1.286 | `Pcre2Slice` | 0.825 us | 0.096 us | 3.357 us | 0.33x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.228 us | 0.01x | 0.01–0.01x | -1.217 us | 9 pairs; 40/39 ms; 3,627,454/32,152 ops/lane; IQR 1.007/1.009 | `Pcre2Slice` | 0.101 us | 1.157 us | 1.249 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.691 us | 0.159 us | 4.36x | 4.33–4.37x | +0.532 us | 9 pairs; 38/38 ms; 54,948/242,634 ops/lane; IQR 1.003/1.012 | `Pcre2Backtracking` | 0.046 us | 0.124 us | 0.162 us | 4.26x | 0 B | 0 B |
| `common/date-miss` | **Inconclusive** | 47 B | 2.343 us | 0.577 us | 4.06x | 3.95–4.17x | +1.768 us | 9 pairs; 39/41 ms; 16,543/70,399 ops/lane; IQR 1.013/1.017 | `Pcre2Backtracking` | 0.114 us | 0.776 us | 0.806 us | 2.91x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.293 us | 0.34x | 0.34–0.35x | -0.194 us | 9 pairs; 40/41 ms; 397,423/138,246 ops/lane; IQR 1.007/1.017 | `Pcre2Slice` | 0.109 us | 0.317 us | 0.345 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.105 us | 0.352 us | 0.30x | 0.30–0.30x | -0.247 us | 9 pairs; 37/41 ms; 352,559/115,296 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | 0.111 us | 0.437 us | 0.469 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Inconclusive** | 15 B | 0.086 us | 0.291 us | 0.30x | 0.30–0.35x | -0.203 us | 9 pairs; 45/39 ms; 524,246/132,913 ops/lane; IQR 1.179/1.009 | `Pcre2Slice` | 0.085 us | 0.239 us | 0.260 us | 0.33x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.081 us | 0.314 us | 0.26x | 0.26–0.28x | -0.231 us | 9 pairs; 40/40 ms; 494,279/128,384 ops/lane; IQR 1.091/1.009 | `Pcre2Slice` | 0.085 us | 0.253 us | 0.274 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.173 us | 0.076 us | 2.26x | 2.22–2.37x | +0.096 us | 9 pairs; 40/39 ms; 232,508/507,984 ops/lane; IQR 1.002/1.029 | `Pcre2Slice` | 0.083 us | 0.982 us | 1.003 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.198 us | 0.17x | 0.17–0.17x | -0.165 us | 9 pairs; 27/39 ms; 813,034/197,747 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 0.102 us | 0.298 us | 0.338 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.607 us | 0.10x | 0.10–0.10x | -0.547 us | 9 pairs; 40/40 ms; 659,194/65,557 ops/lane; IQR 1.007/1.012 | `Pcre2Backtracking` | 0.127 us | 0.240 us | 0.262 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.21x | 0.21–0.22x | -0.121 us | 9 pairs; 38/40 ms; 1,153,165/260,794 ops/lane; IQR 1.019/1.003 | `Pcre2Backtracking` | 0.103 us | 0.180 us | 0.201 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.113 us | 0.17x | 0.17–0.18x | -0.094 us | 9 pairs; 35/40 ms; 1,828,444/356,961 ops/lane; IQR 1.028/1.008 | `Pcre2Backtracking` | 0.058 us | 0.092 us | 0.111 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.116 us | 0.49x | 0.49–0.50x | -0.059 us | 9 pairs; 40/41 ms; 694,914/352,426 ops/lane; IQR 1.005/1.014 | `Pcre2Backtracking` | 0.016 us | 0.086 us | 0.103 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.086 us | 0.277 us | 0.31x | 0.30–0.31x | -0.191 us | 9 pairs; 41/42 ms; 475,966/151,323 ops/lane; IQR 1.005/1.009 | `Pcre2Slice` | 0.090 us | 0.115 us | 0.149 us | 0.58x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.116 us | 0.22x | 0.20–0.22x | -0.091 us | 9 pairs; 43/40 ms; 1,735,766/344,647 ops/lane; IQR 1.048/1.018 | `Pcre2Backtracking` | 0.026 us | 0.093 us | 0.111 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.163 us | 0.05x | 0.05–0.06x | -2.045 us | 9 pairs; 40/49 ms; 341,884/22,617 ops/lane; IQR 1.002/1.119 | `Pcre2Literal` | 0.087 us | 0.051 us | 0.566 us | 0.21x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.121 us | 1.881 us | 0.06x | 0.06–0.07x | -1.759 us | 9 pairs; 41/42 ms; 336,029/22,505 ops/lane; IQR 1.004/1.069 | `Pcre2Literal` | 0.143 us | 0.053 us | 0.601 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 2.627 us | 0.06x | 0.05–0.08x | -2.470 us | 9 pairs; 40/51 ms; 257,232/19,558 ops/lane; IQR 1.004/1.363 | `Pcre2Literal` | 0.200 us | 0.230 us | 0.733 us | 0.21x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.152 us | 1.889 us | 0.08x | 0.07–0.09x | -1.738 us | 9 pairs; 41/37 ms; 267,259/19,535 ops/lane; IQR 1.007/1.094 | `Pcre2Literal` | 0.167 us | 0.204 us | 0.754 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.091 us | 9 pairs; 40/39 ms; 4,524,405/392,613 ops/lane; IQR 1.005/1.009 | `Pcre2Slice` | 0.270 us | 0.068 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.074 us | 0.087 us | 0.85x | 0.78–0.86x | -0.013 us | 9 pairs; 39/39 ms; 527,354/445,837 ops/lane; IQR 1.055/1.014 | `Pcre2Literal` | 0.120 us | 0.051 us | 0.073 us | 1.02x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.109 us | 0.12x | 0.12–0.12x | -0.096 us | 9 pairs; 39/40 ms; 2,889,807/363,852 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0.238 us | 0.091 us | 0.112 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Equivalent** | 45 B | 0.121 us | 0.122 us | 0.99x | 0.98–1.00x | -0.001 us | 9 pairs; 40/39 ms; 331,710/318,480 ops/lane; IQR 1.003/1.016 | `Pcre2Character` | 0.131 us | 0.135 us | 0.143 us | 0.84x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.205 us | 0.37x | 0.37–0.38x | -0.130 us | 9 pairs; 37/41 ms; 483,653/199,542 ops/lane; IQR 1.001/1.019 | `Pcre2Slice` | 0.453 us | 0.076 us | 0.113 us | 0.67x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 81.603 us | 159.486 us | 0.51x | 0.51–0.52x | -77.883 us | 9 pairs; 40/41 ms; 489/257 ops/lane; IQR 1.010/1.006 | `Pcre2Slice` | 373.328 us | 103.000 us | 104.816 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.501 us | 282.794 us | 0.13x | 0.13–0.13x | -245.423 us | 9 pairs; 40/40 ms; 1,056/140 ops/lane; IQR 1.001/1.006 | `Pcre2Character` | 385.475 us | 219.251 us | 230.176 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.209 us | 211.391 us | 0.05x | 0.05–0.05x | -201.177 us | 9 pairs; 41/39 ms; 3,970/185 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 7.943 us | 222.004 us | 226.987 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 237.324 us | 200.529 us | 1.18x | 1.18–1.20x | +36.272 us | 9 pairs; 39/37 ms; 165/184 ops/lane; IQR 1.003/1.012 | `Pcre2SingleTokenRepeat` | 7.962 us | 233.375 us | 237.541 us | 1.00x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.539 us | 22.618 us | 0.16x | 0.16–0.16x | -19.079 us | 9 pairs; 40/40 ms; 11,416/1,751 ops/lane; IQR 1.009/1.021 | `Pcre2Literal` | 1.368 us | 4.237 us | 5.771 us | 0.61x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.577 us | 81.544 us | 0.12x | 0.12–0.12x | -71.956 us | 9 pairs; 40/40 ms; 4,158/490 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 9.243 us | 93.646 us | 94.800 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,139.950 us | 28,988.100 us | 0.14x | 0.14–0.14x | -24835.200 us | 9 pairs; 33/29 ms; 8/1 ops/lane; IQR 1.012/1.017 | `Pcre2Backtracking` | 5,469.800 us | 12,503.700 us | 18,535.600 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,418.483 us | 55,559.800 us | 0.06x | 0.06–0.06x | -52106.208 us | 9 pairs; 41/56 ms; 12/1 ops/lane; IQR 1.017/1.011 | `Pcre2Backtracking` | 3,932.308 us | 43,058.250 us | 49,376.500 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,930.943 us | 479,837.600 us | 0.00x | 0.00–0.00x | -477888.129 us | 9 pairs; 41/480 ms; 21/1 ops/lane; IQR 1.037/1.002 | `Pcre2Character` | 1,884.582 us | 22,644.833 us | 28,947.900 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,533.867 us | 11,238.967 us | 0.49x | 0.47–0.54x | -5694.133 us | 9 pairs; 33/34 ms; 6/3 ops/lane; IQR 1.009/1.132 | `Pcre2Literal` | 1,351.218 us | 1,331.838 us | 8,066.014 us | 0.69x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,638.500 us | 145,622.500 us | 0.13x | 0.13–0.13x | -126987.900 us | 9 pairs; 37/146 ms; 2/1 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 1,232.446 us | 508.143 us | 8,396.400 us | 2.22x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,761.637 us | 22,037.550 us | 0.22x | 0.21–0.22x | -17290.063 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.004/1.005 | `Pcre2Slice` | 2,447.352 us | 10,966.420 us | 18,817.233 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,136.750 us | 134,362.600 us | 0.17x | 0.16–0.17x | -112149.550 us | 9 pairs; 44/134 ms; 2/1 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 1,541.433 us | 2,342.287 us | 10,414.800 us | 2.13x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 173.963 us | 996.000 us | 0.18x | 0.17–0.18x | -821.584 us | 9 pairs; 39/38 ms; 223/38 ops/lane; IQR 1.006/1.039 | `Pcre2Backtracking` | 170.064 us | 338.698 us | 553.064 us | 0.31x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Inconclusive** | 594,930 B | 12,531.933 us | 13,399.633 us | 0.93x | 0.91–0.96x | -878.733 us | 9 pairs; 38/40 ms; 3/3 ops/lane; IQR 1.011/1.011 | `Pcre2Backtracking` | 301.275 us | 15,107.900 us | 15,220.950 us | 0.82x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,775.750 us | 98,514.200 us | 0.03x | 0.03–0.03x | -95751.914 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.008/1.005 | `Pcre2Character` | 1,991.985 us | 19,903.833 us | 20,100.367 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.922 us | 2,810.520 us | 0.07x | 0.07–0.07x | -2624.977 us | 9 pairs; 40/42 ms; 214/15 ops/lane; IQR 1.003/1.024 | `Pcre2SingleTokenRepeat` | 190.310 us | 1,007.130 us | 1,448.947 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,547.304 us | 20,082.800 us | 0.08x | 0.08–0.08x | -18524.952 us | 9 pairs; 39/40 ms; 25/2 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 572.831 us | 10,824.000 us | 11,130.480 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.153 us | 1.091 us | 0.14x | 0.14–0.14x | -0.940 us | 9 pairs; 39/40 ms; 257,598/36,986 ops/lane; IQR 1.011/1.006 | `Pcre2Slice` | 0.732 us | 0.257 us | 0.278 us | 0.55x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.530 us | 0.850 us | 0.62x | 0.62–0.63x | -0.320 us | 9 pairs; 40/41 ms; 75,614/47,996 ops/lane; IQR 1.007/1.003 | `Pcre2Literal` | 0.143 us | 0.148 us | 0.173 us | 3.06x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.290 us | 1.188 us | 0.24x | 0.24–0.25x | -0.899 us | 9 pairs; 39/40 ms; 133,775/33,989 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 1.067 us | 0.342 us | 0.372 us | 0.78x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.873 us | 1.132 us | 0.77x | 0.77–0.77x | -0.258 us | 9 pairs; 40/40 ms; 46,031/35,410 ops/lane; IQR 1.004/1.007 | `Pcre2Character` | 0.273 us | 0.423 us | 0.469 us | 1.86x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.160 us | 0.859 us | 0.19x | 0.18–0.19x | -0.699 us | 9 pairs; 37/40 ms; 229,894/46,059 ops/lane; IQR 1.016/1.001 | `Pcre2Slice` | 0.858 us | 0.153 us | 0.200 us | 0.80x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.897 us | 1.146 us | 0.78x | 0.78–0.78x | -0.251 us | 9 pairs; 40/41 ms; 44,380/35,914 ops/lane; IQR 1.010/1.010 | `Pcre2Slice` | 3.779 us | 0.305 us | 0.323 us | 2.78x | 0 B | 0 B |
| `simple/foo-dense` | **Equivalent** | 18 B | 0.836 us | 0.833 us | 1.01x | 1.00–1.01x | +0.004 us | 9 pairs; 40/41 ms; 47,580/49,427 ops/lane; IQR 1.006/1.002 | `Pcre2Literal` | 3.538 us | 0.180 us | 0.203 us | 4.12x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.035 us | 1.188 us | 0.87x | 0.87–0.88x | -0.152 us | 9 pairs; 41/40 ms; 40,094/33,800 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 5.169 us | 0.400 us | 0.421 us | 2.46x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.173 us | 1.145 us | 1.02x | 1.02–1.03x | +0.027 us | 9 pairs; 39/40 ms; 33,047/34,839 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 3.601 us | 0.474 us | 0.512 us | 2.29x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.799 us | 0.852 us | 0.94x | 0.93–0.94x | -0.053 us | 9 pairs; 40/39 ms; 50,137/45,974 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | 2.092 us | 0.182 us | 0.223 us | 3.59x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.585 us | 1.154 us | 0.51x | 0.50–0.51x | -0.567 us | 9 pairs; 38/40 ms; 65,382/34,780 ops/lane; IQR 1.009/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.531 us | 0.829 us | 0.64x | 0.64–0.64x | -0.299 us | 9 pairs; 42/40 ms; 79,104/48,143 ops/lane; IQR 1.006/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.668 us | 1.181 us | 0.57x | 0.56–0.57x | -0.515 us | 9 pairs; 40/40 ms; 59,598/33,640 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.908 us | 1.142 us | 0.80x | 0.79–0.80x | -0.232 us | 9 pairs; 40/40 ms; 43,632/34,772 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.488 us | 0.860 us | 0.57x | 0.57–0.57x | -0.367 us | 9 pairs; 41/40 ms; 83,452/46,537 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.909 us | — | — | — | — | — | `—` | 25.199 us | 99.758 us | 101.041 us | 0.60x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.284 us | — | — | — | — | — | `—` | 1.765 us | 0.357 us | 0.383 us | 16.42x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.018 us | — | — | — | — | — | `—` | 0.968 us | 0.242 us | 0.271 us | 3.76x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.173 us | — | — | — | — | — | `—` | 3.096 us | 0.452 us | 0.493 us | 2.38x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.474 us | — | — | — | — | — | `—` | 1.039 us | 0.537 us | 0.587 us | 2.51x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.329 us | — | — | — | — | — | `—` | 1.593 us | 0.270 us | 0.316 us | 13.71x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.098 us | 0.086 us | 1.15x | 1.09–1.15x | +0.013 us | 9 pairs; 45/41 ms; 456,699/473,768 ops/lane; IQR 1.048/1.004 | `Pcre2Character` | 0 B | 0 B | 2.256 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.121 us | 0.10x | 0.10–0.11x | -0.108 us | 9 pairs; 39/40 ms; 3,116,434/330,923 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 128.506 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Inconclusive** | 3 B | 0.378 us | 0.106 us | 3.59x | 3.53–3.66x | +0.274 us | 9 pairs; 36/40 ms; 94,780/374,292 ops/lane; IQR 1.014/1.031 | `Pcre2Backtracking` | 0 B | 0 B | 20.719 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.395 us | 0.117 us | 3.38x | 3.36–3.41x | +0.278 us | 9 pairs; 36/40 ms; 91,221/345,207 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 70.919 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.372 us | 0.103 us | 3.62x | 3.59–3.65x | +0.270 us | 9 pairs; 35/39 ms; 95,269/377,860 ops/lane; IQR 1.006/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 69.956 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.572 us | 0.346 us | 1.65x | 1.63–1.66x | +0.226 us | 9 pairs; 38/39 ms; 65,768/113,778 ops/lane; IQR 1.005/1.005 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.630 us | 0.02x | 0.02–0.02x | -0.617 us | 9 pairs; 41/40 ms; 3,427,914/63,064 ops/lane; IQR 1.041/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 22.650 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.533 us | 0.02x | 0.02–0.02x | -0.522 us | 9 pairs; 40/39 ms; 3,464,611/73,902 ops/lane; IQR 1.015/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 21.844 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.464 us | 0.112 us | 4.16x | 4.14–4.22x | +0.352 us | 9 pairs; 37/38 ms; 78,743/344,026 ops/lane; IQR 1.004/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 18.269 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.169 us | 10.561 us | 0.77x | 0.77–0.78x | -2.414 us | 9 pairs; 40/40 ms; 4,873/3,745 ops/lane; IQR 1.006/1.008 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.291 us | 1.046 us | 0.28x | 0.28–0.28x | -0.755 us | 9 pairs; 35/40 ms; 121,345/38,003 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.050 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.367 us | 0.473 us | 0.78x | 0.77–0.78x | -0.106 us | 9 pairs; 44/39 ms; 119,749/82,639 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 112.619 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.292 us | 0.467 us | 0.63x | 0.62–0.63x | -0.175 us | 9 pairs; 38/39 ms; 130,618/83,526 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 142.425 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.310 us | 0.618 us | 0.50x | 0.50–0.50x | -0.307 us | 9 pairs; 37/39 ms; 118,739/63,829 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.312 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.288 us | 0.376 us | 0.77x | 0.76–0.78x | -0.088 us | 9 pairs; 33/40 ms; 116,196/106,578 ops/lane; IQR 1.002/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 212.588 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.793 us | 8.824 us | 0.54x | 0.54–0.55x | -4.021 us | 9 pairs; 40/40 ms; 8,380/4,526 ops/lane; IQR 1.004/1.001 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.263 us | 0.312 us | 0.84x | 0.84–0.85x | -0.049 us | 9 pairs; 37/41 ms; 139,571/129,901 ops/lane; IQR 1.002/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 90.369 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.270 us | 0.393 us | 0.69x | 0.68–0.69x | -0.124 us | 9 pairs; 38/40 ms; 140,797/101,226 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 123.750 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.812 us | — | — | — | — | — | `—` | 0 B | — | 13.887 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.187 us | 0.292 us | 0.64x | 0.64–0.64x | -0.105 us | 9 pairs; 41/40 ms; 216,403/136,898 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 54.319 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.747 us | — | — | — | — | — | `—` | 0 B | — | 13.925 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.589 us | 0.669 us | 0.87x | 0.87–0.89x | -0.085 us | 9 pairs; 32/40 ms; 54,786/59,049 ops/lane; IQR 1.013/1.008 | `Pcre2Slice` | 0 B | 0 B | 64.469 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.912 us | 28.809 us | 0.34x | 0.34–0.35x | -18.851 us | 9 pairs; 39/40 ms; 3,970/1,385 ops/lane; IQR 1.005/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.954 us | 1.053 us | 0.91x | 0.90–0.91x | -0.099 us | 9 pairs; 40/40 ms; 42,453/38,322 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 130.806 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.022 us | 1.207 us | 0.85x | 0.84–0.85x | -0.184 us | 9 pairs; 39/40 ms; 38,384/33,390 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 113.237 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.937 us | 1.034 us | 0.91x | 0.91–0.91x | -0.095 us | 9 pairs; 39/40 ms; 41,790/38,665 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 140.938 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.951 us | 1.179 us | 0.81x | 0.80–0.81x | -0.226 us | 9 pairs; 40/41 ms; 42,076/34,883 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 103.144 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.935 us | 0.952 us | 0.98x | 0.98–0.99x | -0.016 us | 9 pairs; 39/40 ms; 41,837/42,136 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 213.050 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.516 us | 8.830 us | 0.63x | 0.62–0.63x | -3.307 us | 9 pairs; 40/40 ms; 7,325/4,511 ops/lane; IQR 1.006/1.002 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.942 us | 0.888 us | 1.06x | 1.05–1.06x | +0.054 us | 9 pairs; 40/40 ms; 42,739/45,118 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 93.013 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Equivalent** | 26 B | 0.949 us | 0.963 us | 0.99x | 0.98–0.99x | -0.014 us | 9 pairs; 42/40 ms; 43,832/41,181 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 121.819 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.091 us | — | — | — | — | — | `—` | 0 B | — | 14.912 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.832 us | 0.864 us | 0.96x | 0.96–0.97x | -0.032 us | 9 pairs; 40/40 ms; 47,726/45,895 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.831 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.950 us | — | — | — | — | — | `—` | 0 B | — | 14.169 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.848 us | 1.260 us | 0.67x | 0.67–0.67x | -0.412 us | 9 pairs; 40/40 ms; 46,595/31,667 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.869 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.374 us | 2.294 us | 0.60x | 0.60–0.60x | -0.921 us | 9 pairs; 40/40 ms; 29,126/17,418 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.731 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.668 us | 1.058 us | 0.63x | 0.63–0.63x | -0.390 us | 9 pairs; 43/39 ms; 63,590/37,197 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 128.875 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.683 us | 1.213 us | 0.56x | 0.56–0.57x | -0.531 us | 9 pairs; 40/41 ms; 58,248/33,899 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 114.169 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.597 us | 1.026 us | 0.58x | 0.58–0.58x | -0.429 us | 9 pairs; 37/40 ms; 62,051/39,078 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 141.625 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.629 us | 1.181 us | 0.53x | 0.53–0.53x | -0.551 us | 9 pairs; 40/41 ms; 63,659/34,552 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 104.325 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.659 us | 0.948 us | 0.69x | 0.69–0.70x | -0.290 us | 9 pairs; 39/40 ms; 59,883/42,250 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 217.006 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.862 us | 2.190 us | 0.85x | 0.85–0.85x | -0.326 us | 9 pairs; 40/40 ms; 21,596/18,220 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.591 us | 0.890 us | 0.66x | 0.66–0.67x | -0.299 us | 9 pairs; 40/40 ms; 67,159/44,986 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 91.037 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.642 us | 0.965 us | 0.67x | 0.66–0.67x | -0.322 us | 9 pairs; 40/40 ms; 62,957/41,325 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 123.062 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.791 us | — | — | — | — | — | `—` | 0 B | — | 14.488 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.491 us | 0.864 us | 0.57x | 0.57–0.57x | -0.374 us | 9 pairs; 40/40 ms; 80,543/46,419 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 53.944 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.758 us | — | — | — | — | — | `—` | 0 B | — | 14.975 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.525 us | 1.271 us | 0.41x | 0.41–0.41x | -0.746 us | 9 pairs; 40/40 ms; 76,511/31,549 ops/lane; IQR 1.003/1.005 | `Pcre2Slice` | 0 B | 0 B | 64.531 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.473 us | — | — | — | — | — | `—` | 480 B | — | 0.713 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.101 us | — | — | — | — | — | `—` | 48 B | — | 128.431 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.196 us | — | — | — | — | — | `—` | 48 B | — | 115.112 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.097 us | — | — | — | — | — | `—` | 48 B | — | 141.619 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.109 us | — | — | — | — | — | `—` | 48 B | — | 100.469 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.085 us | — | — | — | — | — | `—` | 48 B | — | 215.725 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.886 us | — | — | — | — | — | `—` | 64 B | — | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.115 us | — | — | — | — | — | `—` | 40 B | — | 93.019 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.114 us | — | — | — | — | — | `—` | 40 B | — | 121.031 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.652 us | — | — | — | — | — | `—` | 1,216 B | — | 13.594 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.032 us | — | — | — | — | — | `—` | 48 B | — | 53.181 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.835 us | — | — | — | — | — | `—` | 928 B | — | 13.756 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.568 us | — | — | — | — | — | `—` | 23,312 B | — | 64.487 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 31.530–231.635 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.391–95.015 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.668–12.392 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.914–46.635 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.032–1.282 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.336–41.193 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.097–23.630 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.196–99.738 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.133–72.323 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.191–5.301 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.089–366.172 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 40.248–315.137 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.886–5.114 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.186–15.706 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 168.466–1,322.350 us |

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
