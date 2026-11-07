<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `5F06FD02A255AFB0348681B1BBF8922556B9FF9474D16E363BC740BD91E4620F`
- Latest managed row measurement: `2026-11-07T09:37:08.6107747+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-07T09:37:08.6107747+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `1` equivalent, `11` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `34d359376257` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `34d359376257` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.600 us | 2.536 us | 0.24x | 0.23–0.25x | -1.942 us | 9 pairs; 36/46 ms; 59,316/18,002 ops/lane; IQR 1.034/1.049 | `Pcre2Slice` | 456.555 us | 0.273 us | 0.807 us | 0.74x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.724 us | 11.050 us | 0.07x | 0.06–0.07x | -10.310 us | 9 pairs; 34/38 ms; 47,320/3,438 ops/lane; IQR 1.017/1.102 | `Pcre2Slice` | 0.503 us | 0.095 us | 3.596 us | 0.20x | 64 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.225 us | 0.01x | 0.01–0.01x | -1.214 us | 9 pairs; 40/39 ms; 3,622,775/31,857 ops/lane; IQR 1.013/1.015 | `Pcre2Slice` | 0.102 us | 1.062 us | 1.101 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.664 us | 0.155 us | 4.26x | 4.24–4.27x | +0.509 us | 9 pairs; 37/40 ms; 55,574/258,846 ops/lane; IQR 1.012/1.008 | `Pcre2Backtracking` | 0.047 us | 0.118 us | 0.158 us | 4.20x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.355 us | 0.568 us | 4.14x | 4.11–4.17x | +1.785 us | 9 pairs; 39/40 ms; 16,447/69,695 ops/lane; IQR 1.007/1.003 | `Pcre2Backtracking` | 0.115 us | 0.714 us | 0.752 us | 3.13x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.299 us | 0.34x | 0.33–0.34x | -0.199 us | 9 pairs; 40/40 ms; 395,007/133,426 ops/lane; IQR 1.004/1.008 | `Pcre2Slice` | 0.113 us | 0.313 us | 0.337 us | 0.30x | 0 B | 0 B |
| `common/email-miss` | **Inconclusive** | 35 B | 0.108 us | 0.349 us | 0.31x | 0.29–0.31x | -0.241 us | 9 pairs; 41/40 ms; 378,477/114,745 ops/lane; IQR 1.014/1.042 | `Pcre2Slice` | 0.118 us | 0.439 us | 0.460 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.077 us | 0.288 us | 0.27x | 0.27–0.27x | -0.211 us | 9 pairs; 40/40 ms; 520,406/137,272 ops/lane; IQR 1.009/1.005 | `Pcre2Slice` | 0.090 us | 0.229 us | 0.250 us | 0.31x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.080 us | 0.309 us | 0.26x | 0.26–0.26x | -0.228 us | 9 pairs; 41/40 ms; 505,103/130,125 ops/lane; IQR 1.021/1.009 | `Pcre2Slice` | 0.089 us | 0.239 us | 0.261 us | 0.31x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.171 us | 0.075 us | 2.27x | 2.24–2.30x | +0.095 us | 9 pairs; 38/42 ms; 223,220/557,538 ops/lane; IQR 1.010/1.016 | `Pcre2Slice` | 0.088 us | 0.921 us | 0.956 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.196 us | 0.17x | 0.17–0.17x | -0.163 us | 9 pairs; 40/41 ms; 1,222,970/210,159 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 0.113 us | 0.296 us | 0.336 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.599 us | 0.10x | 0.10–0.10x | -0.538 us | 9 pairs; 40/39 ms; 665,456/64,334 ops/lane; IQR 1.017/1.012 | `Pcre2Backtracking` | 0.134 us | 0.223 us | 0.245 us | 0.25x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.153 us | 0.22x | 0.21–0.22x | -0.120 us | 9 pairs; 38/39 ms; 1,155,052/252,889 ops/lane; IQR 1.022/1.013 | `Pcre2Backtracking` | 0.112 us | 0.183 us | 0.204 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.110 us | 0.17x | 0.17–0.18x | -0.091 us | 9 pairs; 39/41 ms; 2,016,803/376,879 ops/lane; IQR 1.027/1.011 | `Pcre2Backtracking` | 0.061 us | 0.088 us | 0.107 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.121 us | 0.47x | 0.46–0.48x | -0.064 us | 9 pairs; 40/42 ms; 701,611/342,934 ops/lane; IQR 1.005/1.024 | `Pcre2Backtracking` | 0.013 us | 0.083 us | 0.100 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.083 us | 0.275 us | 0.30x | 0.30–0.31x | -0.192 us | 9 pairs; 40/38 ms; 474,775/139,493 ops/lane; IQR 1.003/1.006 | `Pcre2Slice` | 0.095 us | 0.114 us | 0.154 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.115 us | 0.19x | 0.19–0.19x | -0.093 us | 9 pairs; 39/42 ms; 1,755,703/368,832 ops/lane; IQR 1.013/1.016 | `Pcre2Backtracking` | 0.019 us | 0.087 us | 0.104 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.124 us | 2.157 us | 0.06x | 0.05–0.06x | -2.031 us | 9 pairs; 43/54 ms; 343,316/24,817 ops/lane; IQR 1.026/1.142 | `Pcre2Literal` | 0.089 us | 0.051 us | 0.577 us | 0.22x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.126 us | 2.006 us | 0.06x | 0.05–0.07x | -1.881 us | 9 pairs; 42/43 ms; 334,375/21,540 ops/lane; IQR 1.067/1.176 | `Pcre2Literal` | 0.147 us | 0.053 us | 0.603 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.155 us | 1.981 us | 0.08x | 0.07–0.08x | -1.826 us | 9 pairs; 40/42 ms; 255,888/21,434 ops/lane; IQR 1.015/1.047 | `Pcre2Literal` | 0.205 us | 0.232 us | 0.761 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.149 us | 1.931 us | 0.08x | 0.07–0.09x | -1.782 us | 9 pairs; 40/33 ms; 268,812/16,906 ops/lane; IQR 1.009/1.141 | `Pcre2Literal` | 0.177 us | 0.202 us | 0.735 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.096 us | 0.09x | 0.09–0.09x | -0.087 us | 9 pairs; 24/41 ms; 2,707,610/425,393 ops/lane; IQR 1.012/1.012 | `Pcre2Slice` | 0.217 us | 0.068 us | 0.088 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.066 us | 0.083 us | 0.80x | 0.77–0.80x | -0.017 us | 9 pairs; 40/40 ms; 603,787/476,510 ops/lane; IQR 1.005/1.028 | `Pcre2Literal` | 0.126 us | 0.050 us | 0.072 us | 0.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.106 us | 0.13x | 0.12–0.13x | -0.093 us | 9 pairs; 40/41 ms; 2,984,335/381,805 ops/lane; IQR 1.013/1.013 | `Pcre2Backtracking` | 0.274 us | 0.089 us | 0.110 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.122 us | 0.118 us | 1.04x | 1.02–1.05x | +0.005 us | 9 pairs; 43/40 ms; 352,345/337,889 ops/lane; IQR 1.019/1.031 | `Pcre2Character` | 0.141 us | 0.111 us | 0.138 us | 0.88x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.081 us | 0.200 us | 0.40x | 0.38–0.41x | -0.121 us | 9 pairs; 43/39 ms; 536,308/193,802 ops/lane; IQR 1.054/1.011 | `Pcre2Slice` | 0.336 us | 0.073 us | 0.111 us | 0.73x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.921 us | 156.365 us | 0.52x | 0.51–0.52x | -74.965 us | 9 pairs; 39/41 ms; 487/262 ops/lane; IQR 1.009/1.006 | `Pcre2Slice` | 332.254 us | 96.422 us | 97.511 us | 0.83x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 38.941 us | 281.681 us | 0.14x | 0.13–0.14x | -242.740 us | 9 pairs; 39/40 ms; 1,013/141 ops/lane; IQR 1.008/1.025 | `Pcre2Character` | 261.633 us | 216.601 us | 221.374 us | 0.18x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.272 us | 208.016 us | 0.04x | 0.04–0.04x | -198.730 us | 9 pairs; 39/41 ms; 4,158/196 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 7.917 us | 216.597 us | 216.837 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 232.642 us | 201.087 us | 1.16x | 1.15–1.17x | +31.872 us | 9 pairs; 39/39 ms; 166/193 ops/lane; IQR 1.008/1.015 | `Pcre2SingleTokenRepeat` | 7.853 us | 229.178 us | 229.556 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.512 us | 22.598 us | 0.16x | 0.15–0.16x | -19.094 us | 9 pairs; 39/39 ms; 11,122/1,739 ops/lane; IQR 1.005/1.006 | `Pcre2Literal` | 1.357 us | 4.154 us | 5.889 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.403 us | 80.369 us | 0.12x | 0.12–0.12x | -70.996 us | 9 pairs; 40/41 ms; 4,228/511 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 9.112 us | 88.952 us | 90.368 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,137.325 us | 27,360.000 us | 0.15x | 0.15–0.16x | -23228.025 us | 9 pairs; 33/55 ms; 8/2 ops/lane; IQR 1.014/1.036 | `Pcre2Backtracking` | 5,931.862 us | 11,614.571 us | 18,470.060 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,222.908 us | 54,017.600 us | 0.06x | 0.06–0.06x | -50794.692 us | 9 pairs; 39/54 ms; 12/1 ops/lane; IQR 1.009/1.032 | `Pcre2Backtracking` | 3,863.225 us | 40,667.650 us | 47,842.700 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Managed faster** | 16,013,977 B | 1,902.005 us | 474,835.000 us | 0.00x | 0.00–0.00x | -472989.200 us | 9 pairs; 42/475 ms; 22/1 ops/lane; IQR 1.024/1.006 | `Pcre2Character` | 1,808.740 us | 22,504.625 us | 29,479.033 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,451.717 us | 10,949.100 us | 0.50x | 0.47–0.56x | -5498.933 us | 9 pairs; 33/33 ms; 6/3 ops/lane; IQR 1.006/1.116 | `Pcre2Literal` | 1,294.197 us | 1,307.105 us | 8,239.090 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,506.350 us | 143,731.200 us | 0.13x | 0.13–0.13x | -125193.900 us | 9 pairs; 37/144 ms; 2/1 ops/lane; IQR 1.014/1.015 | `Pcre2Backtracking` | 1,217.379 us | 501.367 us | 8,411.944 us | 2.20x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,808.900 us | 21,469.650 us | 0.22x | 0.22–0.23x | -16686.964 us | 9 pairs; 34/43 ms; 7/2 ops/lane; IQR 1.002/1.009 | `Pcre2Slice` | 2,367.978 us | 10,721.312 us | 18,561.720 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,914.250 us | 132,493.800 us | 0.17x | 0.16–0.17x | -110350.400 us | 9 pairs; 44/132 ms; 2/1 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 1,487.045 us | 2,235.403 us | 10,232.275 us | 2.14x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 137.795 us | 1,004.293 us | 0.14x | 0.13–0.14x | -865.817 us | 9 pairs; 40/41 ms; 288/41 ops/lane; IQR 1.011/1.009 | `Pcre2Backtracking` | 165.435 us | 319.964 us | 578.660 us | 0.24x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,330.100 us | 13,288.767 us | 0.86x | 0.84–0.87x | -1906.200 us | 9 pairs; 34/40 ms; 3/3 ops/lane; IQR 1.008/1.025 | `Pcre2Backtracking` | 304.104 us | 14,583.950 us | 14,720.567 us | 0.77x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,981.538 us | 96,448.400 us | 0.03x | 0.03–0.03x | -93483.254 us | 9 pairs; 39/96 ms; 13/1 ops/lane; IQR 1.018/1.011 | `Pcre2Character` | 1,868.698 us | 19,487.250 us | 19,605.150 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 176.193 us | 2,689.207 us | 0.06x | 0.06–0.07x | -2516.226 us | 9 pairs; 39/38 ms; 223/14 ops/lane; IQR 1.008/1.021 | `Pcre2SingleTokenRepeat` | 186.006 us | 994.633 us | 1,343.022 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,564.727 us | 19,767.900 us | 0.08x | 0.08–0.08x | -18242.015 us | 9 pairs; 41/40 ms; 26/2 ops/lane; IQR 1.023/1.015 | `Pcre2Backtracking` | 556.079 us | 10,292.913 us | 10,721.100 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.149 us | 1.075 us | 0.14x | 0.14–0.14x | -0.925 us | 9 pairs; 40/39 ms; 265,400/36,217 ops/lane; IQR 1.014/1.017 | `Pcre2Slice` | 0.575 us | 0.255 us | 0.275 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.541 us | 0.812 us | 0.67x | 0.66–0.67x | -0.271 us | 9 pairs; 40/39 ms; 73,955/48,296 ops/lane; IQR 1.008/1.006 | `Pcre2Literal` | 0.142 us | 0.147 us | 0.170 us | 3.18x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.298 us | 1.146 us | 0.26x | 0.26–0.26x | -0.848 us | 9 pairs; 39/41 ms; 131,523/36,181 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 1.038 us | 0.338 us | 0.362 us | 0.82x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.889 us | 1.113 us | 0.80x | 0.79–0.81x | -0.222 us | 9 pairs; 42/41 ms; 47,453/36,386 ops/lane; IQR 1.011/1.007 | `Pcre2Character` | 0.262 us | 0.417 us | 0.457 us | 1.95x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.150 us | 0.843 us | 0.18x | 0.18–0.18x | -0.692 us | 9 pairs; 40/39 ms; 263,161/46,598 ops/lane; IQR 1.013/1.006 | `Pcre2Slice` | 0.620 us | 0.153 us | 0.199 us | 0.75x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.935 us | 1.084 us | 0.86x | 0.86–0.87x | -0.150 us | 9 pairs; 39/42 ms; 42,025/38,726 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | 2.205 us | 0.302 us | 0.319 us | 2.93x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.774 us | 0.808 us | 0.96x | 0.95–0.96x | -0.036 us | 9 pairs; 42/38 ms; 53,829/47,597 ops/lane; IQR 1.010/1.005 | `Pcre2Literal` | 3.525 us | 0.178 us | 0.200 us | 3.88x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.982 us | 1.170 us | 0.84x | 0.83–0.85x | -0.188 us | 9 pairs; 39/41 ms; 39,304/35,109 ops/lane; IQR 1.009/1.016 | `Pcre2Backtracking` | 5.131 us | 0.391 us | 0.415 us | 2.36x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.174 us | 1.113 us | 1.05x | 1.05–1.06x | +0.061 us | 9 pairs; 38/40 ms; 32,567/36,125 ops/lane; IQR 1.005/1.007 | `Pcre2Character` | 3.597 us | 0.471 us | 0.518 us | 2.27x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.795 us | 0.843 us | 0.94x | 0.94–0.95x | -0.047 us | 9 pairs; 41/42 ms; 51,680/49,487 ops/lane; IQR 1.008/1.008 | `Pcre2Slice` | 1.917 us | 0.179 us | 0.218 us | 3.65x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.559 us | 1.089 us | 0.51x | 0.51–0.52x | -0.528 us | 9 pairs; 41/41 ms; 74,226/37,415 ops/lane; IQR 1.007/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.498 us | 0.820 us | 0.61x | 0.61–0.61x | -0.322 us | 9 pairs; 41/41 ms; 81,789/49,576 ops/lane; IQR 1.007/1.017 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.708 us | 1.164 us | 0.61x | 0.60–0.61x | -0.457 us | 9 pairs; 42/39 ms; 59,171/33,759 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.875 us | 1.113 us | 0.78x | 0.78–0.79x | -0.242 us | 9 pairs; 39/39 ms; 44,432/35,254 ops/lane; IQR 1.003/1.018 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.478 us | 0.847 us | 0.57x | 0.56–0.57x | -0.367 us | 9 pairs; 41/39 ms; 85,302/46,131 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.132 us | — | — | — | — | — | `—` | 25.381 us | 96.758 us | 98.861 us | 0.61x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.463 us | — | — | — | — | — | `—` | 1.391 us | 0.352 us | 0.375 us | 9.24x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.051 us | — | — | — | — | — | `—` | 0.865 us | 0.237 us | 0.264 us | 3.99x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.209 us | — | — | — | — | — | `—` | 3.095 us | 0.452 us | 0.483 us | 2.50x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.484 us | — | — | — | — | — | `—` | 0.995 us | 0.526 us | 0.571 us | 2.60x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.339 us | — | — | — | — | — | `—` | 1.378 us | 0.258 us | 0.311 us | 10.75x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.097 us | 0.083 us | 1.17x | 1.08–1.18x | +0.015 us | 9 pairs; 44/39 ms; 454,600/464,399 ops/lane; IQR 1.016/1.011 | `Pcre2Character` | 0 B | 0 B | 2.306 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.118 us | 0.11x | 0.11–0.11x | -0.105 us | 9 pairs; 39/40 ms; 3,022,646/338,452 ops/lane; IQR 1.012/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 133.925 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.387 us | 0.104 us | 3.75x | 3.67–3.78x | +0.283 us | 9 pairs; 37/39 ms; 95,585/372,827 ops/lane; IQR 1.010/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 20.956 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.389 us | 0.113 us | 3.43x | 3.38–3.45x | +0.275 us | 9 pairs; 37/38 ms; 95,318/334,549 ops/lane; IQR 1.004/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 70.369 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.380 us | 0.104 us | 3.68x | 3.64–3.71x | +0.276 us | 9 pairs; 35/42 ms; 92,892/401,003 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 70.519 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.658 us | 0.340 us | 1.94x | 1.92–1.95x | +0.318 us | 9 pairs; 37/40 ms; 56,687/116,695 ops/lane; IQR 1.007/1.005 | `Pcre2Character` | 0 B | 0 B | 2.100 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.618 us | 0.02x | 0.02–0.02x | -0.606 us | 9 pairs; 42/40 ms; 3,527,075/64,127 ops/lane; IQR 1.035/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 22.012 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.537 us | 0.02x | 0.02–0.02x | -0.526 us | 9 pairs; 40/39 ms; 3,546,640/73,287 ops/lane; IQR 1.004/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 22.269 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Inconclusive** | 5 B | 0.465 us | 0.107 us | 4.34x | 4.25–4.47x | +0.356 us | 9 pairs; 35/40 ms; 74,260/373,155 ops/lane; IQR 1.018/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 19.025 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.162 us | 10.233 us | 0.80x | 0.79–0.80x | -2.058 us | 9 pairs; 39/41 ms; 4,746/4,007 ops/lane; IQR 1.003/1.011 | `Pcre2Literal` | 0 B | 0 B | 1.188 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.284 us | 1.028 us | 0.28x | 0.28–0.28x | -0.741 us | 9 pairs; 38/40 ms; 133,470/38,583 ops/lane; IQR 1.011/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 129.975 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.307 us | 0.452 us | 0.68x | 0.67–0.68x | -0.146 us | 9 pairs; 37/38 ms; 120,981/83,137 ops/lane; IQR 1.007/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 113.219 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.293 us | 0.454 us | 0.64x | 0.64–0.65x | -0.162 us | 9 pairs; 34/40 ms; 117,316/86,957 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 141.463 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.293 us | 0.578 us | 0.51x | 0.50–0.51x | -0.285 us | 9 pairs; 34/42 ms; 114,485/72,256 ops/lane; IQR 1.005/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 103.037 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.300 us | 0.393 us | 0.76x | 0.74–0.76x | -0.096 us | 9 pairs; 39/39 ms; 128,649/99,471 ops/lane; IQR 1.003/1.027 | `Pcre2Backtracking` | 0 B | 0 B | 213.506 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.833 us | 8.698 us | 0.56x | 0.56–0.56x | -3.836 us | 9 pairs; 40/41 ms; 8,177/4,658 ops/lane; IQR 1.008/1.007 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.262 us | 0.310 us | 0.84x | 0.84–0.86x | -0.048 us | 9 pairs; 36/40 ms; 139,543/130,169 ops/lane; IQR 1.029/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 91.088 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.258 us | 0.395 us | 0.66x | 0.65–0.66x | -0.137 us | 9 pairs; 34/39 ms; 132,044/97,803 ops/lane; IQR 1.018/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 123.825 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.868 us | — | — | — | — | — | `—` | 0 B | — | 14.206 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.195 us | 0.292 us | 0.66x | 0.66–0.67x | -0.098 us | 9 pairs; 33/41 ms; 169,130/139,150 ops/lane; IQR 1.018/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 51.644 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.738 us | — | — | — | — | — | `—` | 0 B | — | 13.781 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.549 us | 0.649 us | 0.85x | 0.83–0.85x | -0.101 us | 9 pairs; 35/39 ms; 64,296/60,529 ops/lane; IQR 1.009/1.023 | `Pcre2Slice` | 0 B | 0 B | 64.412 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.825 us | 28.529 us | 0.34x | 0.34–0.35x | -18.699 us | 9 pairs; 40/39 ms; 4,024/1,380 ops/lane; IQR 1.007/1.010 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.968 us | 1.047 us | 0.92x | 0.92–0.93x | -0.079 us | 9 pairs; 40/39 ms; 41,363/37,554 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 131.044 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.001 us | 1.219 us | 0.82x | 0.82–0.82x | -0.216 us | 9 pairs; 39/39 ms; 39,309/32,360 ops/lane; IQR 1.011/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 115.763 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.934 us | 1.017 us | 0.92x | 0.92–0.92x | -0.081 us | 9 pairs; 40/40 ms; 42,686/39,788 ops/lane; IQR 1.013/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 142.588 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.924 us | 1.157 us | 0.80x | 0.80–0.80x | -0.234 us | 9 pairs; 38/39 ms; 41,442/33,644 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 102.244 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.926 us | 0.924 us | 1.00x | 1.00–1.01x | +0.001 us | 9 pairs; 39/40 ms; 42,443/43,252 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 212.406 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.583 us | 8.706 us | 0.64x | 0.64–0.64x | -3.142 us | 9 pairs; 40/40 ms; 7,097/4,569 ops/lane; IQR 1.006/1.008 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.896 us | 0.874 us | 1.03x | 1.02–1.03x | +0.023 us | 9 pairs; 39/39 ms; 43,269/44,491 ops/lane; IQR 1.012/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 92.856 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.922 us | 0.951 us | 0.97x | 0.97–0.97x | -0.028 us | 9 pairs; 41/40 ms; 44,705/41,863 ops/lane; IQR 1.014/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 122.338 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.056 us | — | — | — | — | — | `—` | 0 B | — | 14.850 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.815 us | 0.863 us | 0.94x | 0.94–0.95x | -0.048 us | 9 pairs; 40/41 ms; 48,853/47,733 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 52.850 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.964 us | — | — | — | — | — | `—` | 0 B | — | 14.150 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.855 us | 1.231 us | 0.69x | 0.69–0.70x | -0.377 us | 9 pairs; 41/41 ms; 47,451/32,918 ops/lane; IQR 1.006/1.013 | `Pcre2Slice` | 0 B | 0 B | 63.694 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.464 us | 2.249 us | 0.65x | 0.65–0.65x | -0.792 us | 9 pairs; 40/39 ms; 27,420/17,316 ops/lane; IQR 1.009/1.014 | `Pcre2Literal` | 0 B | 0 B | 0.719 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.627 us | 1.049 us | 0.59x | 0.59–0.60x | -0.432 us | 9 pairs; 39/39 ms; 62,453/37,518 ops/lane; IQR 1.023/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 130.575 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.658 us | 1.211 us | 0.55x | 0.54–0.55x | -0.551 us | 9 pairs; 39/40 ms; 58,820/32,714 ops/lane; IQR 1.013/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 113.237 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.627 us | 1.021 us | 0.62x | 0.61–0.62x | -0.392 us | 9 pairs; 41/41 ms; 65,609/39,829 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 141.131 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.635 us | 1.163 us | 0.55x | 0.54–0.55x | -0.526 us | 9 pairs; 41/40 ms; 64,033/34,509 ops/lane; IQR 1.007/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 101.094 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.615 us | 0.921 us | 0.67x | 0.67–0.67x | -0.306 us | 9 pairs; 39/39 ms; 62,868/42,762 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 212.019 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.899 us | 2.155 us | 0.89x | 0.88–0.89x | -0.247 us | 9 pairs; 39/40 ms; 20,727/18,598 ops/lane; IQR 1.009/1.008 | `Pcre2Character` | 0 B | 0 B | 1.387 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.595 us | 0.875 us | 0.68x | 0.68–0.68x | -0.279 us | 9 pairs; 40/40 ms; 67,630/46,017 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 91.269 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.608 us | 0.951 us | 0.64x | 0.64–0.65x | -0.339 us | 9 pairs; 41/40 ms; 67,449/42,321 ops/lane; IQR 1.013/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 127.156 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.834 us | — | — | — | — | — | `—` | 0 B | — | 13.881 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.494 us | 0.867 us | 0.57x | 0.56–0.57x | -0.373 us | 9 pairs; 40/40 ms; 81,551/46,338 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 52.675 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.731 us | — | — | — | — | — | `—` | 0 B | — | 14.137 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.532 us | 1.234 us | 0.43x | 0.43–0.43x | -0.701 us | 9 pairs; 41/40 ms; 76,736/32,363 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.312 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 16.890 us | — | — | — | — | — | `—` | 480 B | — | 0.713 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.136 us | — | — | — | — | — | `—` | 48 B | — | 131.400 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.230 us | — | — | — | — | — | `—` | 48 B | — | 113.725 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.108 us | — | — | — | — | — | `—` | 48 B | — | 140.062 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.128 us | — | — | — | — | — | `—` | 48 B | — | 100.588 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.101 us | — | — | — | — | — | `—` | 48 B | — | 217.025 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.699 us | — | — | — | — | — | `—` | 64 B | — | 1.375 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.132 us | — | — | — | — | — | `—` | 40 B | — | 90.225 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.112 us | — | — | — | — | — | `—` | 40 B | — | 122.894 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.881 us | — | — | — | — | — | `—` | 1,216 B | — | 13.125 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.048 us | — | — | — | — | — | `—` | 48 B | — | 53.244 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.568 us | — | — | — | — | — | `—` | 928 B | — | 13.931 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.532 us | — | — | — | — | — | `—` | 4,496 B | — | 62.775 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 30.917–238.570 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.226–92.284 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 1.575–12.400 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.140–49.037 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.035–1.124 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.471–42.395 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.566–21.853 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.149–97.917 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.956–71.477 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.197–5.337 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.984–364.864 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.075 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.794–301.678 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.821–5.043 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.074–15.156 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 104.048–824.114 us |

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
