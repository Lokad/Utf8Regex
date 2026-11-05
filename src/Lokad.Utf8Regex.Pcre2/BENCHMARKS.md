<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `7098F1592FEF52B8C4E5BBFB02FE29F48115B9EDB6053C67F29F0CC27193A00C`
- Latest managed row measurement: `2026-11-05T20:06:36.7074334+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T20:06:36.7074334+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `79` managed faster, `1` equivalent, `11` native faster, `10` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `d03c31479a5a` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `d03c31479a5a` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.651 us | 2.159 us | 0.30x | 0.28–0.31x | -1.508 us | 9 pairs; 32/34 ms; 48,590/15,598 ops/lane; IQR 1.016/1.078 | `Pcre2Slice` | 471.430 us | 0.282 us | 0.862 us | 0.76x | 1,248 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.903 us | 12.507 us | 0.07x | 0.07–0.08x | -11.621 us | 9 pairs; 36/44 ms; 39,955/3,531 ops/lane; IQR 1.032/1.118 | `Pcre2Slice` | 0.702 us | 0.098 us | 3.612 us | 0.25x | 720 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.207 us | 0.01x | 0.01–0.01x | -1.196 us | 9 pairs; 40/39 ms; 3,641,057/32,136 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | 0.104 us | 1.042 us | 1.073 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.696 us | 0.157 us | 4.39x | 4.37–4.49x | +0.536 us | 9 pairs; 36/40 ms; 52,280/255,862 ops/lane; IQR 1.007/1.019 | `Pcre2Backtracking` | 0.048 us | 0.124 us | 0.158 us | 4.40x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.295 us | 0.566 us | 4.06x | 4.04–4.08x | +1.729 us | 9 pairs; 39/39 ms; 17,083/68,636 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0.129 us | 0.738 us | 0.785 us | 2.92x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.306 us | 0.32x | 0.32–0.33x | -0.207 us | 9 pairs; 40/41 ms; 398,701/134,141 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | 0.112 us | 0.314 us | 0.341 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.349 us | 0.30x | 0.30–0.31x | -0.244 us | 9 pairs; 41/41 ms; 384,096/118,519 ops/lane; IQR 1.009/1.005 | `Pcre2Slice` | 0.116 us | 0.435 us | 0.451 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.077 us | 0.288 us | 0.27x | 0.27–0.27x | -0.210 us | 9 pairs; 38/40 ms; 496,875/138,352 ops/lane; IQR 1.008/1.004 | `Pcre2Slice` | 0.090 us | 0.234 us | 0.257 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.082 us | 0.311 us | 0.26x | 0.26–0.26x | -0.230 us | 9 pairs; 41/38 ms; 499,412/122,296 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | 0.090 us | 0.249 us | 0.272 us | 0.30x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.172 us | 0.076 us | 2.25x | 2.21–2.29x | +0.096 us | 9 pairs; 40/41 ms; 233,137/538,088 ops/lane; IQR 1.012/1.009 | `Pcre2Slice` | 0.090 us | 0.899 us | 0.918 us | 0.19x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.202 us | 0.16x | 0.16–0.17x | -0.169 us | 9 pairs; 41/41 ms; 1,237,562/201,151 ops/lane; IQR 1.009/1.019 | `Pcre2Backtracking` | 0.110 us | 0.302 us | 0.343 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.603 us | 0.10x | 0.10–0.10x | -0.543 us | 9 pairs; 40/39 ms; 658,965/64,256 ops/lane; IQR 1.012/1.012 | `Pcre2Backtracking` | 0.136 us | 0.224 us | 0.248 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.152 us | 0.22x | 0.21–0.22x | -0.119 us | 9 pairs; 38/41 ms; 1,172,018/269,361 ops/lane; IQR 1.015/1.007 | `Pcre2Backtracking` | 0.114 us | 0.181 us | 0.201 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.113 us | 0.19x | 0.17–0.19x | -0.093 us | 9 pairs; 41/39 ms; 1,952,437/346,720 ops/lane; IQR 1.032/1.014 | `Pcre2Backtracking` | 0.061 us | 0.091 us | 0.110 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.120 us | 0.47x | 0.47–0.48x | -0.063 us | 9 pairs; 40/40 ms; 707,908/336,783 ops/lane; IQR 1.011/1.027 | `Pcre2Backtracking` | 0.015 us | 0.089 us | 0.105 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.085 us | 0.268 us | 0.32x | 0.31–0.34x | -0.181 us | 9 pairs; 40/38 ms; 469,068/141,573 ops/lane; IQR 1.060/1.029 | `Pcre2Slice` | 0.096 us | 0.117 us | 0.152 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.116 us | 0.19x | 0.19–0.19x | -0.094 us | 9 pairs; 39/41 ms; 1,761,365/348,596 ops/lane; IQR 1.005/1.021 | `Pcre2Backtracking` | 0.019 us | 0.098 us | 0.113 us | 0.20x | 0 B | 0 B |
| `literal/absolute-anchored` | **Managed faster** | 4,103 B | 0.117 us | 1.946 us | 0.06x | 0.06–0.06x | -1.829 us | 9 pairs; 40/47 ms; 345,199/24,228 ops/lane; IQR 1.004/1.043 | `Pcre2Literal` | 0.086 us | 0.050 us | 0.597 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.121 us | 1.880 us | 0.06x | 0.05–0.07x | -1.759 us | 9 pairs; 41/39 ms; 336,857/20,589 ops/lane; IQR 1.010/1.159 | `Pcre2Literal` | 0.143 us | 0.052 us | 0.590 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 2.098 us | 0.07x | 0.05–0.09x | -1.943 us | 9 pairs; 40/30 ms; 256,037/14,486 ops/lane; IQR 1.009/1.112 | `Pcre2Literal` | 0.199 us | 0.229 us | 0.810 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.150 us | 1.822 us | 0.08x | 0.08–0.09x | -1.674 us | 9 pairs; 40/38 ms; 267,540/20,992 ops/lane; IQR 1.005/1.072 | `Pcre2Literal` | 0.167 us | 0.204 us | 0.787 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.095 us | 0.09x | 0.09–0.09x | -0.087 us | 9 pairs; 38/40 ms; 4,437,853/424,959 ops/lane; IQR 1.010/1.011 | `Pcre2Slice` | 0.251 us | 0.070 us | 0.087 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.088 us | 0.75x | 0.74–0.77x | -0.022 us | 9 pairs; 40/39 ms; 596,807/438,351 ops/lane; IQR 1.012/1.022 | `Pcre2Literal` | 0.126 us | 0.050 us | 0.071 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.105 us | 0.13x | 0.13–0.13x | -0.092 us | 9 pairs; 39/40 ms; 2,888,745/381,700 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0.282 us | 0.094 us | 0.117 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.121 us | 0.119 us | 1.02x | 1.01–1.02x | +0.002 us | 9 pairs; 43/41 ms; 352,406/341,964 ops/lane; IQR 1.003/1.016 | `Pcre2Character` | 0.133 us | 0.111 us | 0.140 us | 0.86x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.083 us | 0.198 us | 0.42x | 0.42–0.43x | -0.115 us | 9 pairs; 46/38 ms; 556,627/190,484 ops/lane; IQR 1.010/1.018 | `Pcre2Slice` | 0.435 us | 0.076 us | 0.110 us | 0.76x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 81.545 us | 157.522 us | 0.52x | 0.51–0.52x | -76.005 us | 9 pairs; 39/40 ms; 475/252 ops/lane; IQR 1.009/1.008 | `Pcre2Slice` | 322.061 us | 102.632 us | 103.986 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.021 us | 276.043 us | 0.13x | 0.13–0.13x | -239.171 us | 9 pairs; 39/39 ms; 1,047/141 ops/lane; IQR 1.007/1.010 | `Pcre2Character` | 260.457 us | 217.083 us | 226.270 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.312 us | 209.424 us | 0.04x | 0.04–0.04x | -200.176 us | 9 pairs; 39/40 ms; 4,235/190 ops/lane; IQR 1.012/1.015 | `Pcre2Backtracking` | 7.842 us | 214.671 us | 217.491 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 237.096 us | 201.988 us | 1.17x | 1.17–1.18x | +34.688 us | 9 pairs; 41/40 ms; 172/196 ops/lane; IQR 1.007/1.007 | `Pcre2SingleTokenRepeat` | 7.714 us | 226.512 us | 231.086 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.500 us | 22.765 us | 0.15x | 0.15–0.16x | -19.276 us | 9 pairs; 40/39 ms; 11,319/1,714 ops/lane; IQR 1.009/1.022 | `Pcre2Literal` | 1.358 us | 4.167 us | 5.912 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.510 us | 80.939 us | 0.12x | 0.12–0.12x | -71.416 us | 9 pairs; 39/39 ms; 4,152/477 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 9.144 us | 90.268 us | 89.063 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,109.550 us | 28,110.500 us | 0.15x | 0.14–0.15x | -23999.613 us | 9 pairs; 33/28 ms; 8/1 ops/lane; IQR 1.012/1.014 | `Pcre2Backtracking` | 5,465.410 us | 11,691.480 us | 18,289.967 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 3,276.908 us | 53,260.600 us | 0.06x | 0.06–0.06x | -50007.683 us | 9 pairs; 39/53 ms; 12/1 ops/lane; IQR 1.030/1.023 | `Pcre2Backtracking` | 3,626.343 us | 39,248.350 us | 45,492.050 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,927.455 us | 480,222.100 us | 0.00x | 0.00–0.00x | -478286.232 us | 9 pairs; 42/480 ms; 22/1 ops/lane; IQR 1.037/1.007 | `Pcre2Character` | 1,777.448 us | 22,507.867 us | 29,148.350 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,498.117 us | 10,622.925 us | 0.52x | 0.49–0.57x | -5075.258 us | 9 pairs; 33/42 ms; 6/4 ops/lane; IQR 1.015/1.068 | `Pcre2Literal` | 1,280.410 us | 1,318.302 us | 8,074.114 us | 0.68x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,539.900 us | 143,013.300 us | 0.13x | 0.13–0.13x | -124491.500 us | 9 pairs; 37/143 ms; 2/1 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 1,235.941 us | 500.831 us | 8,129.150 us | 2.28x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,799.700 us | 21,625.650 us | 0.22x | 0.22–0.22x | -16823.650 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.009/1.009 | `Pcre2Slice` | 2,450.533 us | 10,820.460 us | 18,461.767 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,302.900 us | 132,983.900 us | 0.17x | 0.17–0.17x | -110687.150 us | 9 pairs; 45/133 ms; 2/1 ops/lane; IQR 1.007/1.013 | `Pcre2Backtracking` | 1,523.256 us | 2,288.700 us | 10,193.340 us | 2.19x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 161.340 us | 975.562 us | 0.17x | 0.16–0.17x | -814.257 us | 9 pairs; 40/38 ms; 245/39 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 159.669 us | 306.819 us | 522.285 us | 0.31x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,390.150 us | 13,232.567 us | 0.86x | 0.85–0.87x | -1898.417 us | 9 pairs; 46/40 ms; 4/3 ops/lane; IQR 1.008/1.018 | `Pcre2Backtracking` | 306.628 us | 14,552.100 us | 14,742.500 us | 0.77x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,946.931 us | 97,831.500 us | 0.03x | 0.03–0.03x | -94908.031 us | 9 pairs; 38/98 ms; 13/1 ops/lane; IQR 1.016/1.005 | `Pcre2Character` | 1,981.996 us | 19,606.967 us | 19,746.100 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Inconclusive** | 594,930 B | 182.581 us | 2,720.753 us | 0.07x | 0.07–0.07x | -2538.431 us | 9 pairs; 40/41 ms; 217/15 ops/lane; IQR 1.008/1.028 | `Pcre2SingleTokenRepeat` | 189.912 us | 992.394 us | 1,393.833 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,523.569 us | 19,872.600 us | 0.08x | 0.08–0.08x | -18352.465 us | 9 pairs; 40/40 ms; 26/2 ops/lane; IQR 1.011/1.013 | `Pcre2Backtracking` | 552.199 us | 10,465.500 us | 10,761.860 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.145 us | 1.093 us | 0.13x | 0.13–0.13x | -0.948 us | 9 pairs; 42/40 ms; 292,099/36,511 ops/lane; IQR 1.010/1.009 | `Pcre2Slice` | 0.633 us | 0.254 us | 0.274 us | 0.53x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.488 us | 0.814 us | 0.60x | 0.60–0.60x | -0.326 us | 9 pairs; 40/39 ms; 82,328/48,228 ops/lane; IQR 1.009/1.007 | `Pcre2Literal` | 0.142 us | 0.147 us | 0.168 us | 2.91x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.299 us | 1.167 us | 0.26x | 0.25–0.26x | -0.869 us | 9 pairs; 40/41 ms; 135,169/34,854 ops/lane; IQR 1.010/1.005 | `Pcre2Backtracking` | 1.068 us | 0.350 us | 0.377 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.862 us | 1.109 us | 0.78x | 0.78–0.78x | -0.243 us | 9 pairs; 40/40 ms; 46,947/35,698 ops/lane; IQR 1.009/1.003 | `Pcre2Character` | 0.275 us | 0.422 us | 0.468 us | 1.84x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.159 us | 0.837 us | 0.19x | 0.19–0.19x | -0.677 us | 9 pairs; 41/41 ms; 260,850/48,717 ops/lane; IQR 1.021/1.004 | `Pcre2Slice` | 0.790 us | 0.153 us | 0.198 us | 0.80x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.943 us | 1.101 us | 0.86x | 0.85–0.87x | -0.155 us | 9 pairs; 40/41 ms; 41,929/37,226 ops/lane; IQR 1.013/1.007 | `Pcre2Slice` | 2.944 us | 0.302 us | 0.320 us | 2.95x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.785 us | 0.815 us | 0.96x | 0.96–0.97x | -0.030 us | 9 pairs; 40/40 ms; 50,416/49,626 ops/lane; IQR 1.007/1.006 | `Pcre2Literal` | 3.534 us | 0.179 us | 0.200 us | 3.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.005 us | 1.164 us | 0.86x | 0.86–0.87x | -0.159 us | 9 pairs; 40/40 ms; 39,698/34,533 ops/lane; IQR 1.013/1.012 | `Pcre2Backtracking` | 5.166 us | 0.406 us | 0.431 us | 2.33x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.248 us | 1.113 us | 1.12x | 1.11–1.12x | +0.132 us | 9 pairs; 42/40 ms; 33,720/35,771 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 3.594 us | 0.479 us | 0.515 us | 2.42x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.788 us | 0.839 us | 0.94x | 0.93–0.94x | -0.054 us | 9 pairs; 40/41 ms; 50,468/48,799 ops/lane; IQR 1.007/1.006 | `Pcre2Slice` | 2.087 us | 0.182 us | 0.220 us | 3.57x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.610 us | 1.090 us | 0.56x | 0.56–0.56x | -0.479 us | 9 pairs; 37/39 ms; 60,139/36,071 ops/lane; IQR 1.008/1.012 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.501 us | 0.817 us | 0.61x | 0.61–0.62x | -0.315 us | 9 pairs; 39/39 ms; 78,818/48,194 ops/lane; IQR 1.007/1.007 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.669 us | 1.163 us | 0.58x | 0.57–0.58x | -0.494 us | 9 pairs; 40/40 ms; 59,839/34,031 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.866 us | 1.109 us | 0.78x | 0.78–0.78x | -0.242 us | 9 pairs; 40/41 ms; 45,935/36,673 ops/lane; IQR 1.009/1.006 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.467 us | 0.838 us | 0.55x | 0.55–0.56x | -0.373 us | 9 pairs; 41/40 ms; 87,675/48,137 ops/lane; IQR 1.009/1.010 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 59.145 us | — | — | — | — | — | `—` | 24.843 us | 92.983 us | 94.351 us | 0.63x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.840 us | — | — | — | — | — | `—` | 1.598 us | 0.355 us | 0.374 us | 12.96x | 7,056 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.081 us | — | — | — | — | — | `—` | 0.915 us | 0.238 us | 0.261 us | 4.14x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.220 us | — | — | — | — | — | `—` | 3.033 us | 0.455 us | 0.481 us | 2.54x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.500 us | — | — | — | — | — | `—` | 0.992 us | 0.525 us | 0.573 us | 2.62x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.607 us | — | — | — | — | — | `—` | 1.467 us | 0.260 us | 0.304 us | 11.88x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.093 us | 0.085 us | 1.10x | 1.09–1.11x | +0.008 us | 9 pairs; 41/39 ms; 441,401/457,371 ops/lane; IQR 1.012/1.015 | `Pcre2Character` | 0 B | 0 B | 2.225 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.116 us | 0.11x | 0.11–0.11x | -0.103 us | 9 pairs; 39/40 ms; 3,056,241/347,750 ops/lane; IQR 1.009/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 127.325 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.409 us | 0.104 us | 3.95x | 3.76–3.97x | +0.305 us | 9 pairs; 38/40 ms; 92,049/387,965 ops/lane; IQR 1.020/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 21.406 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Inconclusive** | 3 B | 0.408 us | 0.118 us | 3.46x | 3.37–3.54x | +0.291 us | 9 pairs; 37/40 ms; 89,967/336,542 ops/lane; IQR 1.019/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 70.312 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.393 us | 0.102 us | 3.82x | 3.72–3.85x | +0.289 us | 9 pairs; 36/38 ms; 91,740/373,939 ops/lane; IQR 1.009/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 69.481 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.581 us | 0.341 us | 1.71x | 1.69–1.71x | +0.240 us | 9 pairs; 38/41 ms; 64,616/119,301 ops/lane; IQR 1.008/1.011 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.642 us | 0.02x | 0.02–0.02x | -0.630 us | 9 pairs; 28/39 ms; 2,390,734/61,013 ops/lane; IQR 1.014/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 22.044 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.012 us | 0.502 us | 0.02x | 0.02–0.02x | -0.490 us | 9 pairs; 42/40 ms; 3,529,450/80,527 ops/lane; IQR 1.031/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 20.906 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.469 us | 0.108 us | 4.34x | 4.32–4.38x | +0.361 us | 9 pairs; 37/41 ms; 78,071/382,804 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 19.475 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.254 us | 10.445 us | 0.79x | 0.78–0.80x | -2.183 us | 9 pairs; 39/39 ms; 4,783/3,747 ops/lane; IQR 1.016/1.010 | `Pcre2Literal` | 0 B | 0 B | 1.163 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.301 us | 1.046 us | 0.29x | 0.28–0.29x | -0.741 us | 9 pairs; 32/42 ms; 107,808/39,690 ops/lane; IQR 1.013/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 127.338 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.313 us | 0.472 us | 0.67x | 0.66–0.67x | -0.158 us | 9 pairs; 37/41 ms; 119,854/86,078 ops/lane; IQR 1.007/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 114.662 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.301 us | 0.460 us | 0.66x | 0.65–0.67x | -0.159 us | 9 pairs; 32/42 ms; 104,677/91,983 ops/lane; IQR 1.007/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 140.275 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.313 us | 0.592 us | 0.53x | 0.52–0.53x | -0.278 us | 9 pairs; 39/40 ms; 126,253/66,827 ops/lane; IQR 1.010/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 102.400 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.300 us | 0.391 us | 0.77x | 0.75–0.77x | -0.092 us | 9 pairs; 38/39 ms; 125,672/98,499 ops/lane; IQR 1.012/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 211.162 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.846 us | 8.737 us | 0.55x | 0.55–0.56x | -3.890 us | 9 pairs; 40/40 ms; 8,170/4,522 ops/lane; IQR 1.005/1.005 | `Pcre2Character` | 0 B | 0 B | 1.488 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.256 us | 0.311 us | 0.83x | 0.82–0.83x | -0.054 us | 9 pairs; 33/37 ms; 129,632/117,893 ops/lane; IQR 1.010/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 89.950 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.269 us | 0.393 us | 0.69x | 0.68–0.69x | -0.123 us | 9 pairs; 37/42 ms; 136,028/107,946 ops/lane; IQR 1.017/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 118.731 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.830 us | — | — | — | — | — | `—` | 0 B | — | 13.769 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.195 us | 0.292 us | 0.67x | 0.66–0.68x | -0.096 us | 9 pairs; 39/40 ms; 200,855/138,905 ops/lane; IQR 1.014/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.706 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.711 us | — | — | — | — | — | `—` | 0 B | — | 15.050 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.564 us | 0.663 us | 0.85x | 0.85–0.86x | -0.098 us | 9 pairs; 37/39 ms; 64,823/59,331 ops/lane; IQR 1.007/1.009 | `Pcre2Slice` | 0 B | 0 B | 64.594 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.724 us | 28.708 us | 0.34x | 0.34–0.34x | -18.939 us | 9 pairs; 39/39 ms; 3,993/1,375 ops/lane; IQR 1.008/1.009 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.983 us | 1.044 us | 0.94x | 0.93–0.94x | -0.064 us | 9 pairs; 39/38 ms; 39,728/36,568 ops/lane; IQR 1.018/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 130.056 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.070 us | 1.175 us | 0.91x | 0.90–0.91x | -0.106 us | 9 pairs; 40/40 ms; 37,556/33,662 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 111.269 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.929 us | 1.031 us | 0.90x | 0.89–0.91x | -0.099 us | 9 pairs; 40/41 ms; 43,037/39,690 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 141.713 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.945 us | 1.161 us | 0.81x | 0.81–0.82x | -0.217 us | 9 pairs; 38/41 ms; 40,377/35,253 ops/lane; IQR 1.009/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 104.075 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.943 us | 0.945 us | 1.00x | 0.99–1.00x | 0.000 us | 9 pairs; 38/41 ms; 40,147/43,326 ops/lane; IQR 1.011/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 213.525 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.588 us | 8.798 us | 0.63x | 0.63–0.64x | -3.227 us | 9 pairs; 40/40 ms; 7,114/4,535 ops/lane; IQR 1.003/1.011 | `Pcre2Character` | 0 B | 0 B | 1.519 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.910 us | 0.882 us | 1.03x | 1.03–1.04x | +0.028 us | 9 pairs; 40/40 ms; 43,853/45,158 ops/lane; IQR 1.008/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 92.331 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.913 us | 0.964 us | 0.94x | 0.94–0.95x | -0.054 us | 9 pairs; 39/40 ms; 42,938/41,446 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 121.062 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.932 us | — | — | — | — | — | `—` | 0 B | — | 14.231 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.826 us | 0.858 us | 0.96x | 0.96–0.97x | -0.035 us | 9 pairs; 39/39 ms; 47,761/45,528 ops/lane; IQR 1.009/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 53.250 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.889 us | — | — | — | — | — | `—` | 0 B | — | 16.119 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.897 us | 1.238 us | 0.72x | 0.72–0.72x | -0.346 us | 9 pairs; 40/41 ms; 44,829/32,809 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 0 B | 0 B | 64.088 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.329 us | 2.259 us | 0.59x | 0.59–0.59x | -0.929 us | 9 pairs; 40/42 ms; 29,764/18,396 ops/lane; IQR 1.006/1.009 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.637 us | 1.039 us | 0.61x | 0.61–0.62x | -0.400 us | 9 pairs; 41/41 ms; 63,898/39,266 ops/lane; IQR 1.011/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 128.350 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.666 us | 1.174 us | 0.57x | 0.57–0.57x | -0.507 us | 9 pairs; 40/41 ms; 60,377/34,675 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 115.250 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.644 us | 1.028 us | 0.62x | 0.62–0.63x | -0.387 us | 9 pairs; 41/40 ms; 64,158/39,296 ops/lane; IQR 1.010/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 138.938 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.621 us | 1.163 us | 0.53x | 0.53–0.54x | -0.544 us | 9 pairs; 40/40 ms; 65,130/34,830 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 104.644 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.657 us | 0.938 us | 0.70x | 0.70–0.70x | -0.281 us | 9 pairs; 40/40 ms; 60,775/42,180 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 214.656 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.856 us | 2.174 us | 0.85x | 0.85–0.86x | -0.319 us | 9 pairs; 40/41 ms; 21,321/18,824 ops/lane; IQR 1.007/1.005 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.624 us | 0.868 us | 0.72x | 0.72–0.72x | -0.242 us | 9 pairs; 39/41 ms; 62,292/47,171 ops/lane; IQR 1.017/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 92.181 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.595 us | 0.958 us | 0.62x | 0.62–0.62x | -0.362 us | 9 pairs; 40/39 ms; 67,705/41,081 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 120.325 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.689 us | — | — | — | — | — | `—` | 0 B | — | 13.775 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.485 us | 0.854 us | 0.57x | 0.56–0.57x | -0.370 us | 9 pairs; 40/40 ms; 82,213/47,117 ops/lane; IQR 1.009/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 52.706 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.693 us | — | — | — | — | — | `—` | 0 B | — | 14.613 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.528 us | 1.246 us | 0.42x | 0.42–0.42x | -0.721 us | 9 pairs; 40/40 ms; 75,880/32,168 ops/lane; IQR 1.010/1.013 | `Pcre2Slice` | 0 B | 0 B | 63.619 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.233 us | — | — | — | — | — | `—` | 480 B | — | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.142 us | — | — | — | — | — | `—` | 48 B | — | 128.144 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.219 us | — | — | — | — | — | `—` | 48 B | — | 112.312 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.122 us | — | — | — | — | — | `—` | 48 B | — | 140.062 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.145 us | — | — | — | — | — | `—` | 48 B | — | 100.088 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.123 us | — | — | — | — | — | `—` | 48 B | — | 214.131 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.760 us | — | — | — | — | — | `—` | 64 B | — | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.141 us | — | — | — | — | — | `—` | 40 B | — | 91.281 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.143 us | — | — | — | — | — | `—` | 40 B | — | 121.175 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.362 us | — | — | — | — | — | `—` | 1,216 B | — | 14.444 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.085 us | — | — | — | — | — | `—` | 48 B | — | 52.744 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.772 us | — | — | — | — | — | `—` | 928 B | — | 13.512 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.612 us | — | — | — | — | — | `—` | 7,184 B | — | 63.413 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 30.307–244.099 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.417–94.185 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.089–14.346 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.742–47.173 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.031–1.121 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.139–40.020 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.644–22.075 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.156–99.554 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.101–72.072 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.193–5.447 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.162–368.179 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.922–307.349 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.826–5.085 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.194–15.797 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 107.483–867.713 us |

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
