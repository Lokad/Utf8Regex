<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `488C59EA3E3687437833FD4B702BDEF368226A973CFB5A5431598F7EA632270D`
- Latest managed row measurement: `2026-11-03T21:28:10.7544287+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-03T21:28:10.7544287+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `41/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `75` managed faster, `2` equivalent, `12` native faster, `12` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `87db1ced59fc` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `87db1ced59fc` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.910 us | 2.062 us | 0.43x | 0.43–0.47x | -1.165 us | 9 pairs; 34/37 ms; 37,703/17,755 ops/lane; IQR 1.073/1.094 | `Pcre2Slice` | 516.976 us | 0.283 us | 0.816 us | 1.12x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.099 us | 11.291 us | 0.10x | 0.09–0.11x | -10.192 us | 9 pairs; 36/46 ms; 32,835/4,035 ops/lane; IQR 1.016/1.089 | `Pcre2Slice` | 1.650 us | 0.097 us | 3.618 us | 0.30x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.254 us | 0.01x | 0.01–0.01x | -1.243 us | 9 pairs; 39/40 ms; 3,576,479/32,038 ops/lane; IQR 1.006/1.003 | `Pcre2Slice` | 0.177 us | 1.102 us | 1.133 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.515 us | 0.161 us | 9.40x | 9.37–9.48x | +1.354 us | 9 pairs; 39/40 ms; 25,729/247,928 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0.049 us | 0.122 us | 0.156 us | 9.69x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 5.151 us | 0.575 us | 8.96x | 8.93–8.98x | +4.577 us | 9 pairs; 39/40 ms; 7,644/69,725 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0.115 us | 0.745 us | 0.778 us | 6.62x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.156 us | 0.293 us | 0.53x | 0.52–0.53x | -0.138 us | 9 pairs; 41/39 ms; 264,307/131,704 ops/lane; IQR 1.006/1.021 | `Pcre2Slice` | 0.158 us | 0.320 us | 0.345 us | 0.45x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.158 us | 0.358 us | 0.44x | 0.44–0.45x | -0.199 us | 9 pairs; 40/40 ms; 251,308/110,520 ops/lane; IQR 1.002/1.014 | `Pcre2Slice` | 0.166 us | 0.441 us | 0.466 us | 0.34x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.110 us | 0.292 us | 0.38x | 0.38–0.38x | -0.182 us | 9 pairs; 39/40 ms; 353,146/136,228 ops/lane; IQR 1.013/1.003 | `Pcre2Slice` | 0.112 us | 0.238 us | 0.259 us | 0.42x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.110 us | 0.317 us | 0.35x | 0.35–0.35x | -0.207 us | 9 pairs; 40/39 ms; 364,459/123,756 ops/lane; IQR 1.004/1.005 | `Pcre2Slice` | 0.112 us | 0.252 us | 0.275 us | 0.40x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.077 us | 2.26x | 2.21–2.29x | +0.097 us | 9 pairs; 38/43 ms; 220,078/556,462 ops/lane; IQR 1.008/1.027 | `Pcre2Slice` | 0.165 us | 0.935 us | 0.976 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.201 us | 0.17x | 0.17–0.17x | -0.167 us | 9 pairs; 40/40 ms; 1,191,007/199,848 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0.175 us | 0.305 us | 0.345 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.606 us | 0.10x | 0.10–0.10x | -0.545 us | 9 pairs; 40/39 ms; 657,591/64,562 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0.165 us | 0.225 us | 0.251 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.155 us | 0.22x | 0.22–0.22x | -0.121 us | 9 pairs; 39/41 ms; 1,149,980/262,236 ops/lane; IQR 1.024/1.003 | `Pcre2Backtracking` | 0.103 us | 0.183 us | 0.200 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.022 us | 0.114 us | 0.19x | 0.18–0.19x | -0.092 us | 9 pairs; 43/40 ms; 1,948,954/347,676 ops/lane; IQR 1.052/1.005 | `Pcre2Backtracking` | 0.059 us | 0.091 us | 0.109 us | 0.20x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.122 us | 0.47x | 0.46–0.48x | -0.064 us | 9 pairs; 40/40 ms; 691,333/329,067 ops/lane; IQR 1.003/1.028 | `Pcre2Backtracking` | 0.015 us | 0.085 us | 0.102 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.186 us | 0.281 us | 0.66x | 0.64–0.67x | -0.095 us | 9 pairs; 41/39 ms; 220,250/137,740 ops/lane; IQR 1.011/1.017 | `Pcre2Slice` | 0.185 us | 0.118 us | 0.154 us | 1.21x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.115 us | 0.21x | 0.20–0.21x | -0.091 us | 9 pairs; 42/40 ms; 1,767,299/346,783 ops/lane; IQR 1.043/1.007 | `Pcre2Backtracking` | 0.019 us | 0.092 us | 0.110 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.118 us | 2.191 us | 0.05x | 0.05–0.07x | -2.073 us | 9 pairs; 42/48 ms; 351,098/22,116 ops/lane; IQR 1.006/1.302 | `Pcre2Literal` | 0.091 us | 0.051 us | 0.617 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.121 us | 1.910 us | 0.06x | 0.05–0.07x | -1.789 us | 9 pairs; 40/47 ms; 329,390/24,725 ops/lane; IQR 1.004/1.186 | `Pcre2Literal` | 0.149 us | 0.054 us | 0.611 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.159 us | 2.219 us | 0.07x | 0.07–0.08x | -2.060 us | 9 pairs; 41/43 ms; 257,410/19,589 ops/lane; IQR 1.002/1.126 | `Pcre2Literal` | 0.212 us | 0.236 us | 0.794 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.153 us | 1.901 us | 0.08x | 0.08–0.08x | -1.746 us | 9 pairs; 41/39 ms; 264,313/20,471 ops/lane; IQR 1.014/1.047 | `Pcre2Literal` | 0.178 us | 0.207 us | 0.774 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.099 us | 0.09x | 0.09–0.09x | -0.090 us | 9 pairs; 39/40 ms; 4,412,456/402,667 ops/lane; IQR 1.004/1.008 | `Pcre2Slice` | 0.881 us | 0.069 us | 0.087 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.087 us | 0.78x | 0.78–0.79x | -0.019 us | 9 pairs; 40/41 ms; 589,101/474,818 ops/lane; IQR 1.007/1.012 | `Pcre2Literal` | 0.124 us | 0.051 us | 0.073 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.12–0.13x | -0.093 us | 9 pairs; 27/39 ms; 1,927,742/361,593 ops/lane; IQR 1.031/1.025 | `Pcre2Backtracking` | 0.242 us | 0.095 us | 0.118 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.118 us | 0.122 us | 0.96x | 0.95–0.97x | -0.004 us | 9 pairs; 40/39 ms; 342,305/320,574 ops/lane; IQR 1.003/1.012 | `Pcre2Character` | 0.135 us | 0.118 us | 0.148 us | 0.80x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.075 us | 0.202 us | 0.37x | 0.36–0.37x | -0.127 us | 9 pairs; 39/39 ms; 527,986/190,911 ops/lane; IQR 1.004/1.013 | `Pcre2Slice` | 0.562 us | 0.075 us | 0.107 us | 0.70x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.202 us | 159.630 us | 0.52x | 0.51–0.52x | -77.132 us | 9 pairs; 40/40 ms; 484/253 ops/lane; IQR 1.005/1.002 | `Pcre2Slice` | 420.879 us | 102.683 us | 103.470 us | 0.79x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.647 us | 284.065 us | 0.13x | 0.13–0.13x | -246.384 us | 9 pairs; 40/40 ms; 1,065/141 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 392.486 us | 221.430 us | 229.070 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.315 us | 209.974 us | 0.05x | 0.05–0.05x | -199.636 us | 9 pairs; 40/38 ms; 3,873/183 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 8.117 us | 222.941 us | 224.494 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.193 us | 215.002 us | 1.12x | 1.12–1.13x | +26.489 us | 9 pairs; 41/40 ms; 168/184 ops/lane; IQR 1.005/1.005 | `Pcre2SingleTokenRepeat` | 8.088 us | 231.156 us | 233.978 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.517 us | 22.788 us | 0.16x | 0.15–0.16x | -19.247 us | 9 pairs; 39/40 ms; 11,198/1,760 ops/lane; IQR 1.005/1.021 | `Pcre2Literal` | 1.382 us | 4.274 us | 6.021 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.584 us | 81.626 us | 0.12x | 0.12–0.12x | -72.036 us | 9 pairs; 40/40 ms; 4,160/484 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 9.398 us | 91.999 us | 94.111 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,180.767 us | 28,750.900 us | 0.14x | 0.14–0.15x | -24628.267 us | 9 pairs; 38/29 ms; 9/1 ops/lane; IQR 1.015/1.023 | `Pcre2Backtracking` | 6,058.378 us | 11,854.440 us | 18,556.900 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 5,813.471 us | 54,322.800 us | 0.11x | 0.10–0.11x | -48409.386 us | 9 pairs; 41/54 ms; 7/1 ops/lane; IQR 1.009/1.024 | `Pcre2Backtracking` | 6,465.462 us | 40,611.200 us | 47,461.550 us | 0.12x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,948.081 us | 490,106.000 us | 0.00x | 0.00–0.00x | -488154.186 us | 9 pairs; 41/490 ms; 21/1 ops/lane; IQR 1.039/1.002 | `Pcre2Character` | 1,772.790 us | 22,816.400 us | 29,711.600 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,543.200 us | 11,110.033 us | 0.50x | 0.48–0.57x | -5566.833 us | 9 pairs; 33/33 ms; 6/3 ops/lane; IQR 1.007/1.097 | `Pcre2Literal` | 1,348.187 us | 1,359.598 us | 8,292.117 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,780.100 us | 145,741.300 us | 0.13x | 0.13–0.13x | -127100.800 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.016/1.005 | `Pcre2Backtracking` | 1,591.828 us | 509.901 us | 8,561.650 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,796.375 us | 21,862.150 us | 0.22x | 0.22–0.22x | -17046.188 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.002/1.005 | `Pcre2Slice` | 2,858.328 us | 10,924.520 us | 19,214.633 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,300.000 us | 133,589.700 us | 0.17x | 0.17–0.17x | -111371.250 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 1,904.507 us | 2,351.464 us | 10,520.680 us | 2.12x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 236.208 us | 975.400 us | 0.24x | 0.24–0.25x | -738.614 us | 9 pairs; 39/37 ms; 167/38 ops/lane; IQR 1.006/1.021 | `Pcre2Backtracking` | 291.258 us | 328.307 us | 549.019 us | 0.43x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,302.333 us | 13,619.967 us | 0.83x | 0.82–0.84x | -2333.867 us | 9 pairs; 34/41 ms; 3/3 ops/lane; IQR 1.003/1.020 | `Pcre2Backtracking` | 309.125 us | 15,030.350 us | 15,208.975 us | 0.74x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,795.929 us | 98,945.500 us | 0.03x | 0.03–0.03x | -96141.414 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 2,015.900 us | 19,850.233 us | 20,186.667 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.876 us | 2,772.943 us | 0.07x | 0.07–0.07x | -2588.616 us | 9 pairs; 39/39 ms; 213/14 ops/lane; IQR 1.007/1.007 | `Pcre2SingleTokenRepeat` | 189.144 us | 1,020.159 us | 1,460.183 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,579.360 us | 19,856.800 us | 0.08x | 0.08–0.08x | -18288.188 us | 9 pairs; 39/40 ms; 25/2 ops/lane; IQR 1.011/1.006 | `Pcre2Backtracking` | 607.153 us | 10,553.960 us | 10,838.460 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.151 us | 1.106 us | 0.14x | 0.14–0.14x | -0.954 us | 9 pairs; 39/40 ms; 254,921/36,354 ops/lane; IQR 1.012/1.002 | `Pcre2Slice` | 1.454 us | 0.260 us | 0.282 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.563 us | 0.830 us | 0.68x | 0.68–0.68x | -0.266 us | 9 pairs; 42/40 ms; 73,696/48,166 ops/lane; IQR 1.005/1.002 | `Pcre2Literal` | 0.144 us | 0.149 us | 0.172 us | 3.27x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.302 us | 1.176 us | 0.26x | 0.26–0.26x | -0.874 us | 9 pairs; 39/40 ms; 129,761/33,706 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 1.070 us | 0.354 us | 0.383 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Equivalent** | 45 B | 1.133 us | 1.132 us | 1.00x | 0.99–1.00x | 0.000 us | 9 pairs; 51/40 ms; 45,013/35,483 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 0.280 us | 0.442 us | 0.490 us | 2.31x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.154 us | 0.852 us | 0.18x | 0.18–0.18x | -0.698 us | 9 pairs; 40/40 ms; 262,396/46,620 ops/lane; IQR 1.006/1.005 | `Pcre2Slice` | 1.047 us | 0.157 us | 0.203 us | 0.76x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.954 us | 1.124 us | 0.85x | 0.85–0.85x | -0.170 us | 9 pairs; 40/40 ms; 41,934/35,924 ops/lane; IQR 1.003/1.001 | `Pcre2Slice` | 6.178 us | 0.307 us | 0.323 us | 2.95x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.803 us | 0.835 us | 0.96x | 0.96–0.96x | -0.033 us | 9 pairs; 40/40 ms; 50,410/47,991 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 3.683 us | 0.196 us | 0.204 us | 3.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.994 us | 1.190 us | 0.84x | 0.83–0.84x | -0.195 us | 9 pairs; 40/40 ms; 40,514/33,776 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 5.326 us | 0.415 us | 0.438 us | 2.27x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.208 us | 1.132 us | 1.07x | 1.06–1.07x | +0.077 us | 9 pairs; 40/40 ms; 33,008/35,338 ops/lane; IQR 1.001/1.003 | `Pcre2Character` | 3.757 us | 0.496 us | 0.532 us | 2.27x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.813 us | 0.864 us | 0.94x | 0.94–0.94x | -0.051 us | 9 pairs; 40/41 ms; 49,310/47,800 ops/lane; IQR 1.001/1.004 | `Pcre2Slice` | 2.221 us | 0.183 us | 0.224 us | 3.63x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.576 us | 1.124 us | 0.51x | 0.51–0.51x | -0.548 us | 9 pairs; 41/41 ms; 70,605/36,117 ops/lane; IQR 1.002/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.546 us | 0.834 us | 0.66x | 0.65–0.66x | -0.287 us | 9 pairs; 41/40 ms; 75,395/48,133 ops/lane; IQR 1.002/1.001 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.676 us | 1.190 us | 0.57x | 0.57–0.57x | -0.514 us | 9 pairs; 40/40 ms; 58,646/33,264 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.924 us | 1.128 us | 0.82x | 0.81–0.82x | -0.204 us | 9 pairs; 39/40 ms; 41,674/35,905 ops/lane; IQR 1.013/1.012 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.496 us | 0.861 us | 0.58x | 0.57–0.58x | -0.365 us | 9 pairs; 41/40 ms; 82,810/46,852 ops/lane; IQR 1.001/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.957 us | — | — | — | — | — | `—` | 66.279 us | 99.552 us | 101.558 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.389 us | — | — | — | — | — | `—` | 2.639 us | 0.363 us | 0.387 us | 24.25x | 48,784 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.055 us | — | — | — | — | — | `—` | 0.851 us | 0.244 us | 0.270 us | 3.90x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.238 us | — | — | — | — | — | `—` | 2.949 us | 0.471 us | 0.503 us | 2.46x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.501 us | — | — | — | — | — | `—` | 0.947 us | 0.559 us | 0.603 us | 2.49x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.431 us | — | — | — | — | — | `—` | 1.481 us | 0.272 us | 0.323 us | 13.73x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.089 us | 0.084 us | 1.06x | 1.04–1.06x | +0.005 us | 9 pairs; 40/39 ms; 442,859/464,824 ops/lane; IQR 1.005/1.008 | `Pcre2Character` | 0 B | 0 B | 2.250 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.119 us | 0.11x | 0.11–0.11x | -0.106 us | 9 pairs; 39/40 ms; 3,027,650/333,715 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 129.494 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.384 us | 0.105 us | 3.64x | 3.61–3.67x | +0.279 us | 9 pairs; 35/40 ms; 92,261/376,909 ops/lane; IQR 1.010/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 20.594 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.400 us | 0.119 us | 3.39x | 3.35–3.40x | +0.282 us | 9 pairs; 36/40 ms; 89,350/339,278 ops/lane; IQR 1.008/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 73.956 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.389 us | 0.105 us | 3.72x | 3.71–3.74x | +0.285 us | 9 pairs; 35/39 ms; 90,974/369,264 ops/lane; IQR 1.030/1.026 | `Pcre2Backtracking` | 0 B | 0 B | 69.763 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.578 us | 0.348 us | 1.66x | 1.65–1.67x | +0.230 us | 9 pairs; 37/40 ms; 64,818/113,904 ops/lane; IQR 1.004/1.006 | `Pcre2Character` | 0 B | 0 B | 1.500 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.634 us | 0.02x | 0.02–0.02x | -0.621 us | 9 pairs; 41/39 ms; 3,343,105/61,293 ops/lane; IQR 1.043/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 22.169 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.012 us | 0.517 us | 0.02x | 0.02–0.02x | -0.505 us | 9 pairs; 42/39 ms; 3,428,512/76,106 ops/lane; IQR 1.034/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 22.538 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.463 us | 0.109 us | 4.25x | 4.22–4.29x | +0.354 us | 9 pairs; 36/40 ms; 77,177/366,587 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 18.025 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.198 us | 10.626 us | 0.77x | 0.75–0.78x | -2.426 us | 9 pairs; 40/40 ms; 4,840/3,788 ops/lane; IQR 1.003/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.331 us | 1.048 us | 0.32x | 0.31–0.32x | -0.717 us | 9 pairs; 44/40 ms; 133,209/38,099 ops/lane; IQR 1.005/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 130.256 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.318 us | 0.460 us | 0.69x | 0.68–0.70x | -0.142 us | 9 pairs; 37/40 ms; 117,774/86,480 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 114.938 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.298 us | 0.471 us | 0.63x | 0.63–0.64x | -0.173 us | 9 pairs; 36/40 ms; 119,977/84,256 ops/lane; IQR 1.001/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 141.387 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.295 us | 0.590 us | 0.50x | 0.50–0.50x | -0.295 us | 9 pairs; 35/39 ms; 117,594/66,494 ops/lane; IQR 1.005/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 101.831 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.375 us | 0.398 us | 0.94x | 0.76–0.94x | -0.024 us | 9 pairs; 48/41 ms; 128,770/103,466 ops/lane; IQR 1.138/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 214.706 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.934 us | 8.864 us | 0.56x | 0.56–0.56x | -3.934 us | 9 pairs; 41/40 ms; 8,236/4,505 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.263 us | 0.316 us | 0.83x | 0.83–0.84x | -0.052 us | 9 pairs; 36/37 ms; 138,325/117,583 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 90.375 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.276 us | 0.398 us | 0.69x | 0.66–0.70x | -0.122 us | 9 pairs; 38/39 ms; 136,170/97,459 ops/lane; IQR 1.027/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 122.025 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.602 us | — | — | — | — | — | `—` | 0 B | — | 13.363 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.195 us | 0.294 us | 0.67x | 0.66–0.67x | -0.099 us | 9 pairs; 40/40 ms; 205,687/135,783 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 52.812 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.524 us | — | — | — | — | — | `—` | 0 B | — | 13.606 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.580 us | 0.690 us | 0.84x | 0.81–0.84x | -0.109 us | 9 pairs; 37/40 ms; 63,945/58,406 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.806 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.841 us | 29.027 us | 0.34x | 0.34–0.34x | -19.214 us | 9 pairs; 39/40 ms; 3,998/1,375 ops/lane; IQR 1.002/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.725 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.947 us | 1.076 us | 0.88x | 0.88–0.88x | -0.130 us | 9 pairs; 37/40 ms; 39,032/37,255 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 127.787 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.009 us | 1.192 us | 0.85x | 0.85–0.85x | -0.184 us | 9 pairs; 40/41 ms; 39,850/34,595 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 112.519 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.973 us | 1.041 us | 0.94x | 0.93–0.94x | -0.068 us | 9 pairs; 41/40 ms; 41,871/37,945 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 140.325 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.961 us | 1.172 us | 0.82x | 0.82–0.82x | -0.211 us | 9 pairs; 41/40 ms; 42,314/33,786 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 102.119 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.935 us | 0.944 us | 0.99x | 0.98–0.99x | -0.012 us | 9 pairs; 39/40 ms; 42,188/41,956 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 211.238 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.489 us | 8.872 us | 0.62x | 0.62–0.62x | -3.368 us | 9 pairs; 40/40 ms; 7,314/4,480 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0 B | 0 B | 1.462 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.947 us | 0.889 us | 1.06x | 1.06–1.07x | +0.056 us | 9 pairs; 42/39 ms; 44,038/44,219 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 90.456 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.925 us | 0.969 us | 0.96x | 0.95–0.96x | -0.043 us | 9 pairs; 40/40 ms; 42,928/41,140 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 123.812 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.913 us | — | — | — | — | — | `—` | 0 B | — | 13.181 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.835 us | 0.863 us | 0.97x | 0.97–0.97x | -0.027 us | 9 pairs; 40/41 ms; 48,401/46,990 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 52.575 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.832 us | — | — | — | — | — | `—` | 0 B | — | 15.769 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.828 us | 1.255 us | 0.66x | 0.66–0.66x | -0.428 us | 9 pairs; 40/40 ms; 48,115/31,511 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.819 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.375 us | 2.334 us | 0.59x | 0.59–0.59x | -0.962 us | 9 pairs; 40/40 ms; 29,224/16,988 ops/lane; IQR 1.002/1.005 | `Pcre2Literal` | 0 B | 0 B | 1.181 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.720 us | 1.073 us | 0.67x | 0.67–0.67x | -0.352 us | 9 pairs; 42/41 ms; 58,320/38,334 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.081 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.683 us | 1.194 us | 0.57x | 0.57–0.57x | -0.513 us | 9 pairs; 40/40 ms; 59,037/33,797 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 112.619 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.621 us | 1.029 us | 0.60x | 0.60–0.61x | -0.408 us | 9 pairs; 40/40 ms; 64,646/39,221 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 140.225 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.640 us | 1.169 us | 0.55x | 0.55–0.55x | -0.528 us | 9 pairs; 40/41 ms; 62,558/34,687 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 103.731 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.628 us | 0.945 us | 0.66x | 0.66–0.67x | -0.317 us | 9 pairs; 40/40 ms; 64,186/42,454 ops/lane; IQR 1.004/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 211.075 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.837 us | 2.196 us | 0.84x | 0.83–0.84x | -0.361 us | 9 pairs; 40/40 ms; 21,846/18,218 ops/lane; IQR 1.004/1.001 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.611 us | 0.893 us | 0.68x | 0.68–0.69x | -0.281 us | 9 pairs; 40/40 ms; 65,583/44,515 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 92.644 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.609 us | 0.971 us | 0.63x | 0.63–0.63x | -0.362 us | 9 pairs; 40/40 ms; 65,849/41,350 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 123.188 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.579 us | — | — | — | — | — | `—` | 0 B | — | 13.406 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.524 us | 0.862 us | 0.61x | 0.61–0.61x | -0.338 us | 9 pairs; 39/41 ms; 75,018/47,068 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 52.587 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.481 us | — | — | — | — | — | `—` | 0 B | — | 13.606 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.538 us | 1.259 us | 0.43x | 0.43–0.43x | -0.721 us | 9 pairs; 42/40 ms; 78,302/31,593 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 0 B | 0 B | 63.075 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.102 us | — | — | — | — | — | `—` | 480 B | — | 1.163 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.137 us | — | — | — | — | — | `—` | 48 B | — | 128.988 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.252 us | — | — | — | — | — | `—` | 48 B | — | 116.919 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.126 us | — | — | — | — | — | `—` | 48 B | — | 140.025 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.136 us | — | — | — | — | — | `—` | 48 B | — | 97.144 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.121 us | — | — | — | — | — | `—` | 48 B | — | 218.463 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.925 us | — | — | — | — | — | `—` | 64 B | — | 1.406 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.148 us | — | — | — | — | — | `—` | 40 B | — | 91.744 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.136 us | — | — | — | — | — | `—` | 40 B | — | 121.700 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.641 us | — | — | — | — | — | `—` | 1,216 B | — | 13.319 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.078 us | — | — | — | — | — | `—` | 48 B | — | 52.919 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.581 us | — | — | — | — | — | `—` | 928 B | — | 13.681 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.962 us | — | — | — | — | — | `—` | 28,928 B | — | 64.775 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.579–222.196 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.401–94.556 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.018–12.658 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.252–49.031 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.038–1.461 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.666–44.318 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.808–22.572 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.196–99.952 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.162–72.638 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.252–5.742 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.408–380.253 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.079 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.268–306.302 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.862–5.134 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.408–141.493 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 173.612–1,368.832 us |

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
