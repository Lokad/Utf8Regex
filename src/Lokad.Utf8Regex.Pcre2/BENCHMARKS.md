<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `3A30EB516BDDA2DA0D1923CDD20327869670D1F513A25B74FDD3AABCBC57D3DF`
- Latest managed row measurement: `2026-11-04T02:55:38.3770599+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T02:55:38.3770599+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `1` equivalent, `12` native faster, `12` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `9d46422701dd` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `9d46422701dd` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.864 us | 2.195 us | 0.39x | 0.36–0.42x | -1.341 us | 9 pairs; 33/42 ms; 37,993/19,356 ops/lane; IQR 1.064/1.099 | `Pcre2Slice` | 563.675 us | 0.281 us | 0.774 us | 1.12x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Managed faster** | 22,528 B | 1.013 us | 10.325 us | 0.10x | 0.09–0.11x | -9.272 us | 9 pairs; 34/32 ms; 33,971/3,091 ops/lane; IQR 1.042/1.069 | `Pcre2Slice` | 0.639 us | 0.097 us | 3.312 us | 0.31x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.010 us | 1.195 us | 0.01x | 0.01–0.01x | -1.185 us | 9 pairs; 40/40 ms; 3,787,114/33,169 ops/lane; IQR 1.014/1.012 | `Pcre2Slice` | 0.102 us | 1.273 us | 1.305 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.683 us | 0.157 us | 4.34x | 4.32–4.38x | +0.526 us | 9 pairs; 38/41 ms; 56,133/260,311 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0.051 us | 0.124 us | 0.152 us | 4.49x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.351 us | 0.566 us | 4.13x | 4.12–4.20x | +1.783 us | 9 pairs; 41/41 ms; 17,237/72,438 ops/lane; IQR 1.006/1.017 | `Pcre2Backtracking` | 0.116 us | 0.766 us | 0.800 us | 2.94x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.286 us | 0.35x | 0.35–0.36x | -0.186 us | 9 pairs; 40/39 ms; 402,617/136,507 ops/lane; IQR 1.022/1.011 | `Pcre2Slice` | 0.108 us | 0.317 us | 0.344 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.103 us | 0.348 us | 0.30x | 0.29–0.30x | -0.245 us | 9 pairs; 40/40 ms; 389,078/115,625 ops/lane; IQR 1.011/1.006 | `Pcre2Slice` | 0.112 us | 0.446 us | 0.470 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.074 us | 0.288 us | 0.26x | 0.26–0.26x | -0.213 us | 9 pairs; 35/40 ms; 465,521/138,566 ops/lane; IQR 1.009/1.006 | `Pcre2Slice` | 0.085 us | 0.237 us | 0.259 us | 0.29x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.080 us | 0.312 us | 0.26x | 0.25–0.26x | -0.232 us | 9 pairs; 31/40 ms; 388,086/127,488 ops/lane; IQR 1.011/1.005 | `Pcre2Slice` | 0.085 us | 0.274 us | 0.276 us | 0.29x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.168 us | 0.077 us | 2.19x | 2.14–2.20x | +0.091 us | 9 pairs; 38/40 ms; 222,952/520,855 ops/lane; IQR 1.009/1.019 | `Pcre2Slice` | 0.084 us | 1.004 us | 1.053 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.191 us | 0.17x | 0.17–0.17x | -0.159 us | 9 pairs; 40/40 ms; 1,227,520/209,068 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0.105 us | 0.301 us | 0.339 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.059 us | 0.600 us | 0.10x | 0.10–0.10x | -0.541 us | 9 pairs; 40/39 ms; 676,925/65,122 ops/lane; IQR 1.005/1.010 | `Pcre2Backtracking` | 0.129 us | 0.232 us | 0.256 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.152 us | 0.21x | 0.21–0.22x | -0.119 us | 9 pairs; 37/39 ms; 1,140,834/256,496 ops/lane; IQR 1.031/1.012 | `Pcre2Backtracking` | 0.105 us | 0.182 us | 0.204 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.112 us | 0.19x | 0.18–0.19x | -0.091 us | 9 pairs; 41/41 ms; 1,956,372/363,593 ops/lane; IQR 1.028/1.018 | `Pcre2Backtracking` | 0.059 us | 0.092 us | 0.112 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.056 us | 0.119 us | 0.47x | 0.47–0.47x | -0.063 us | 9 pairs; 39/40 ms; 694,338/334,085 ops/lane; IQR 1.009/1.023 | `Pcre2Backtracking` | 0.015 us | 0.088 us | 0.105 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.086 us | 0.274 us | 0.31x | 0.30–0.33x | -0.190 us | 9 pairs; 41/39 ms; 471,193/143,174 ops/lane; IQR 1.038/1.019 | `Pcre2Slice` | 0.091 us | 0.120 us | 0.151 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.113 us | 0.22x | 0.21–0.22x | -0.088 us | 9 pairs; 42/37 ms; 1,697,384/328,930 ops/lane; IQR 1.032/1.020 | `Pcre2Backtracking` | 0.020 us | 0.093 us | 0.113 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.112 us | 1.826 us | 0.06x | 0.05–0.06x | -1.714 us | 9 pairs; 39/34 ms; 348,324/18,873 ops/lane; IQR 1.003/1.049 | `Pcre2Literal` | 0.088 us | 0.050 us | 0.576 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.119 us | 1.858 us | 0.06x | 0.05–0.07x | -1.739 us | 9 pairs; 41/35 ms; 341,270/18,575 ops/lane; IQR 1.009/1.391 | `Pcre2Literal` | 0.147 us | 0.053 us | 0.577 us | 0.21x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.154 us | 1.963 us | 0.08x | 0.06–0.08x | -1.809 us | 9 pairs; 39/36 ms; 252,901/18,400 ops/lane; IQR 1.011/1.302 | `Pcre2Literal` | 0.210 us | 0.236 us | 0.730 us | 0.21x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.151 us | 1.985 us | 0.08x | 0.06–0.08x | -1.831 us | 9 pairs; 41/36 ms; 269,339/18,131 ops/lane; IQR 1.004/1.075 | `Pcre2Literal` | 0.184 us | 0.206 us | 0.768 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.095 us | 0.09x | 0.09–0.09x | -0.086 us | 9 pairs; 39/39 ms; 4,582,937/409,472 ops/lane; IQR 1.009/1.020 | `Pcre2Slice` | 0.499 us | 0.071 us | 0.086 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.066 us | 0.085 us | 0.77x | 0.77–0.78x | -0.019 us | 9 pairs; 36/40 ms; 544,992/469,097 ops/lane; IQR 1.010/1.012 | `Pcre2Literal` | 0.127 us | 0.054 us | 0.073 us | 0.91x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.111 us | 0.12x | 0.12–0.12x | -0.098 us | 9 pairs; 38/39 ms; 2,892,783/349,399 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 0.248 us | 0.094 us | 0.115 us | 0.11x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.118 us | 0.122 us | 0.98x | 0.97–0.99x | -0.002 us | 9 pairs; 38/39 ms; 322,280/319,721 ops/lane; IQR 1.012/1.018 | `Pcre2Character` | 0.134 us | 0.115 us | 0.144 us | 0.82x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.073 us | 0.197 us | 0.38x | 0.37–0.39x | -0.122 us | 9 pairs; 40/40 ms; 548,886/202,089 ops/lane; IQR 1.067/1.013 | `Pcre2Slice` | 0.455 us | 0.077 us | 0.106 us | 0.69x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 81.121 us | 155.508 us | 0.52x | 0.52–0.53x | -74.338 us | 9 pairs; 42/40 ms; 516/256 ops/lane; IQR 1.005/1.010 | `Pcre2Slice` | 402.467 us | 102.226 us | 104.481 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 36.895 us | 284.245 us | 0.13x | 0.13–0.13x | -247.350 us | 9 pairs; 42/41 ms; 1,130/146 ops/lane; IQR 1.003/1.009 | `Pcre2Character` | 384.896 us | 219.807 us | 227.799 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.020 us | 203.919 us | 0.05x | 0.05–0.05x | -193.908 us | 9 pairs; 39/42 ms; 3,899/204 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 8.013 us | 225.580 us | 230.268 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Inconclusive** | 8,208 B | 233.722 us | 239.338 us | 0.97x | 0.96–0.99x | -6.331 us | 9 pairs; 39/49 ms; 166/203 ops/lane; IQR 1.010/1.025 | `Pcre2SingleTokenRepeat` | 7.928 us | 240.350 us | 243.559 us | 0.96x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.484 us | 22.503 us | 0.16x | 0.15–0.16x | -19.015 us | 9 pairs; 39/39 ms; 11,189/1,735 ops/lane; IQR 1.008/1.020 | `Pcre2Literal` | 1.377 us | 4.247 us | 5.863 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.410 us | 79.021 us | 0.12x | 0.12–0.12x | -69.713 us | 9 pairs; 39/40 ms; 4,134/503 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 9.347 us | 93.350 us | 96.518 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 3,960.325 us | 26,947.250 us | 0.15x | 0.14–0.15x | -23017.175 us | 9 pairs; 32/54 ms; 8/2 ops/lane; IQR 1.012/1.055 | `Pcre2Backtracking` | 6,239.189 us | 12,485.540 us | 19,339.800 us | 0.20x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,313.592 us | 53,830.800 us | 0.06x | 0.06–0.06x | -50486.892 us | 9 pairs; 40/54 ms; 12/1 ops/lane; IQR 1.020/1.012 | `Pcre2Backtracking` | 4,103.827 us | 45,345.100 us | 52,596.800 us | 0.06x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Managed faster** | 16,013,977 B | 1,886.995 us | 480,757.100 us | 0.00x | 0.00–0.00x | -478824.027 us | 9 pairs; 42/481 ms; 22/1 ops/lane; IQR 1.019/1.003 | `Pcre2Character` | 1,850.629 us | 23,431.600 us | 30,089.400 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,311.150 us | 11,492.433 us | 0.47x | 0.44–0.52x | -6077.650 us | 9 pairs; 32/34 ms; 6/3 ops/lane; IQR 1.015/1.082 | `Pcre2Literal` | 1,507.812 us | 1,385.567 us | 8,372.514 us | 0.63x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,383.100 us | 141,709.700 us | 0.13x | 0.13–0.13x | -122917.500 us | 9 pairs; 37/142 ms; 2/1 ops/lane; IQR 1.018/1.010 | `Pcre2Backtracking` | 1,605.362 us | 505.769 us | 8,485.250 us | 2.17x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,692.375 us | 21,378.100 us | 0.22x | 0.22–0.22x | -16701.675 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.009/1.003 | `Pcre2Slice` | 2,863.367 us | 11,230.340 us | 19,270.867 us | 0.24x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,453.700 us | 130,595.800 us | 0.17x | 0.16–0.17x | -109142.100 us | 9 pairs; 43/131 ms; 2/1 ops/lane; IQR 1.017/1.012 | `Pcre2Backtracking` | 1,900.337 us | 2,317.736 us | 10,341.340 us | 2.07x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 183.631 us | 973.027 us | 0.19x | 0.18–0.19x | -789.234 us | 9 pairs; 38/43 ms; 207/44 ops/lane; IQR 1.013/1.027 | `Pcre2Backtracking` | 227.410 us | 346.828 us | 556.749 us | 0.33x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,148.867 us | 13,287.100 us | 0.92x | 0.91–0.93x | -1094.967 us | 9 pairs; 36/40 ms; 3/3 ops/lane; IQR 1.012/1.010 | `Pcre2Backtracking` | 313.411 us | 15,782.775 us | 16,049.875 us | 0.76x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,830.277 us | 96,190.500 us | 0.03x | 0.03–0.03x | -93337.000 us | 9 pairs; 37/96 ms; 13/1 ops/lane; IQR 1.018/1.010 | `Pcre2Character` | 2,027.628 us | 19,889.000 us | 20,055.867 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 180.817 us | 2,736.687 us | 0.07x | 0.07–0.07x | -2556.486 us | 9 pairs; 39/41 ms; 215/15 ops/lane; IQR 1.008/1.016 | `Pcre2SingleTokenRepeat` | 187.275 us | 1,007.402 us | 1,420.936 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,528.992 us | 19,812.350 us | 0.08x | 0.08–0.08x | -18299.031 us | 9 pairs; 40/40 ms; 26/2 ops/lane; IQR 1.010/1.012 | `Pcre2Backtracking` | 579.073 us | 10,978.300 us | 11,122.620 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.150 us | 1.077 us | 0.14x | 0.14–0.14x | -0.929 us | 9 pairs; 43/42 ms; 284,550/38,531 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | 0.725 us | 0.256 us | 0.283 us | 0.53x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.470 us | 0.812 us | 0.58x | 0.58–0.58x | -0.342 us | 9 pairs; 39/41 ms; 83,483/50,035 ops/lane; IQR 1.009/1.014 | `Pcre2Literal` | 0.148 us | 0.148 us | 0.172 us | 2.73x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.289 us | 1.142 us | 0.25x | 0.25–0.26x | -0.852 us | 9 pairs; 39/39 ms; 135,003/34,017 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 1.073 us | 0.349 us | 0.383 us | 0.76x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.888 us | 1.090 us | 0.81x | 0.81–0.82x | -0.204 us | 9 pairs; 41/41 ms; 45,979/37,884 ops/lane; IQR 1.017/1.009 | `Pcre2Character` | 0.279 us | 0.435 us | 0.473 us | 1.88x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.151 us | 0.832 us | 0.18x | 0.18–0.18x | -0.681 us | 9 pairs; 39/41 ms; 257,535/49,130 ops/lane; IQR 1.003/1.011 | `Pcre2Slice` | 0.889 us | 0.156 us | 0.201 us | 0.75x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.939 us | 1.091 us | 0.86x | 0.86–0.86x | -0.153 us | 9 pairs; 40/39 ms; 42,774/36,133 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 4.524 us | 0.307 us | 0.325 us | 2.89x | 0 B | 0 B |
| `simple/foo-dense` | **Native faster** | 18 B | 0.849 us | 0.820 us | 1.03x | 1.03–1.04x | +0.024 us | 9 pairs; 39/39 ms; 45,552/47,084 ops/lane; IQR 1.017/1.014 | `Pcre2Literal` | 3.500 us | 0.181 us | 0.203 us | 4.18x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.960 us | 1.151 us | 0.83x | 0.83–0.83x | -0.193 us | 9 pairs; 41/40 ms; 42,321/34,806 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 5.236 us | 0.405 us | 0.427 us | 2.25x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.192 us | 1.102 us | 1.08x | 1.07–1.09x | +0.090 us | 9 pairs; 42/40 ms; 34,971/36,614 ops/lane; IQR 1.015/1.009 | `Pcre2Character` | 3.609 us | 0.480 us | 0.525 us | 2.27x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.791 us | 0.844 us | 0.94x | 0.93–0.94x | -0.054 us | 9 pairs; 39/39 ms; 49,872/46,291 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 2.129 us | 0.182 us | 0.223 us | 3.54x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.612 us | 1.087 us | 0.56x | 0.56–0.57x | -0.475 us | 9 pairs; 40/39 ms; 65,489/36,211 ops/lane; IQR 1.004/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.536 us | 0.818 us | 0.66x | 0.65–0.66x | -0.283 us | 9 pairs; 41/40 ms; 77,400/49,211 ops/lane; IQR 1.004/1.007 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.683 us | 1.154 us | 0.59x | 0.59–0.59x | -0.470 us | 9 pairs; 34/40 ms; 49,498/34,672 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.920 us | 1.108 us | 0.83x | 0.82–0.83x | -0.190 us | 9 pairs; 41/39 ms; 44,340/35,139 ops/lane; IQR 1.011/1.008 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.500 us | 0.845 us | 0.59x | 0.59–0.60x | -0.345 us | 9 pairs; 39/39 ms; 78,075/46,314 ops/lane; IQR 1.018/1.008 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.577 us | — | — | — | — | — | `—` | 64.932 us | 97.754 us | 102.175 us | 0.60x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.421 us | — | — | — | — | — | `—` | 2.113 us | 0.361 us | 0.385 us | 16.67x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.064 us | — | — | — | — | — | `—` | 0.816 us | 0.240 us | 0.268 us | 3.97x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.203 us | — | — | — | — | — | `—` | 2.982 us | 0.459 us | 0.493 us | 2.44x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.467 us | — | — | — | — | — | `—` | 0.911 us | 0.543 us | 0.587 us | 2.50x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.420 us | — | — | — | — | — | `—` | 1.511 us | 0.269 us | 0.317 us | 13.94x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.094 us | 0.083 us | 1.14x | 1.13–1.17x | +0.012 us | 9 pairs; 42/41 ms; 448,884/490,523 ops/lane; IQR 1.019/1.007 | `Pcre2Character` | 0 B | 0 B | 2.288 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.115 us | 0.11x | 0.11–0.11x | -0.102 us | 9 pairs; 39/41 ms; 3,034,542/352,880 ops/lane; IQR 1.003/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 129.356 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.371 us | 0.105 us | 3.53x | 3.50–3.55x | +0.266 us | 9 pairs; 35/39 ms; 95,545/374,851 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 20.619 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.379 us | 0.117 us | 3.26x | 3.22–3.28x | +0.263 us | 9 pairs; 36/39 ms; 93,923/337,023 ops/lane; IQR 1.012/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 71.119 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.385 us | 0.101 us | 3.79x | 3.77–3.81x | +0.283 us | 9 pairs; 35/39 ms; 91,306/390,306 ops/lane; IQR 1.011/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 68.850 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.567 us | 0.339 us | 1.67x | 1.66–1.67x | +0.227 us | 9 pairs; 37/38 ms; 65,971/112,260 ops/lane; IQR 1.010/1.011 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.609 us | 0.02x | 0.02–0.02x | -0.598 us | 9 pairs; 38/41 ms; 3,486,885/67,269 ops/lane; IQR 1.021/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 23.106 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.501 us | 0.02x | 0.02–0.02x | -0.489 us | 9 pairs; 40/40 ms; 3,485,185/79,875 ops/lane; IQR 1.011/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 20.887 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.455 us | 0.106 us | 4.32x | 4.24–4.36x | +0.349 us | 9 pairs; 35/42 ms; 77,290/395,978 ops/lane; IQR 1.014/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 18.569 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Inconclusive** | 320 B | 8.209 us | 10.181 us | 0.81x | 0.79–0.82x | -1.969 us | 9 pairs; 39/40 ms; 4,712/3,932 ops/lane; IQR 1.009/1.012 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.294 us | 1.029 us | 0.28x | 0.28–0.29x | -0.735 us | 9 pairs; 38/39 ms; 130,446/37,722 ops/lane; IQR 1.010/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 129.081 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.311 us | 0.455 us | 0.68x | 0.68–0.69x | -0.144 us | 9 pairs; 37/40 ms; 119,342/87,908 ops/lane; IQR 1.010/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 113.625 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.297 us | 0.451 us | 0.66x | 0.64–0.67x | -0.152 us | 9 pairs; 35/40 ms; 116,380/88,750 ops/lane; IQR 1.013/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 141.275 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.288 us | 0.578 us | 0.50x | 0.50–0.50x | -0.290 us | 9 pairs; 33/39 ms; 112,698/66,846 ops/lane; IQR 1.012/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 102.706 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.287 us | 0.391 us | 0.73x | 0.72–0.74x | -0.105 us | 9 pairs; 32/37 ms; 112,869/94,395 ops/lane; IQR 1.012/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 216.569 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.903 us | 8.828 us | 0.56x | 0.56–0.56x | -3.924 us | 9 pairs; 40/40 ms; 8,124/4,550 ops/lane; IQR 1.006/1.011 | `Pcre2Character` | 0 B | 0 B | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Inconclusive** | 13 B | 0.256 us | 0.305 us | 0.84x | 0.83–1.08x | -0.047 us | 9 pairs; 34/39 ms; 132,382/129,340 ops/lane; IQR 1.160/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 92.906 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.255 us | 0.391 us | 0.65x | 0.65–0.66x | -0.136 us | 9 pairs; 30/40 ms; 116,322/103,479 ops/lane; IQR 1.017/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 122.406 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.850 us | — | — | — | — | — | `—` | 0 B | — | 14.488 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.181 us | 0.285 us | 0.64x | 0.63–0.64x | -0.104 us | 9 pairs; 39/40 ms; 216,483/139,496 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 55.481 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.793 us | — | — | — | — | — | `—` | 0 B | — | 15.156 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.558 us | 0.662 us | 0.85x | 0.84–0.85x | -0.103 us | 9 pairs; 38/40 ms; 67,641/60,050 ops/lane; IQR 1.016/1.009 | `Pcre2Slice` | 0 B | 0 B | 64.044 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.639 us | 28.318 us | 0.34x | 0.34–0.34x | -18.680 us | 9 pairs; 40/39 ms; 4,178/1,391 ops/lane; IQR 1.016/1.013 | `Pcre2Literal` | 0 B | 0 B | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.942 us | 1.028 us | 0.91x | 0.91–0.92x | -0.088 us | 9 pairs; 39/39 ms; 40,974/37,914 ops/lane; IQR 1.009/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 127.550 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.023 us | 1.163 us | 0.88x | 0.88–0.88x | -0.137 us | 9 pairs; 43/41 ms; 42,410/35,184 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 114.681 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.918 us | 1.008 us | 0.91x | 0.90–0.91x | -0.092 us | 9 pairs; 39/42 ms; 42,826/41,399 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 140.150 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.927 us | 1.143 us | 0.81x | 0.81–0.82x | -0.220 us | 9 pairs; 40/40 ms; 42,970/35,336 ops/lane; IQR 1.007/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 102.350 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.924 us | 0.938 us | 0.99x | 0.98–0.99x | -0.014 us | 9 pairs; 38/41 ms; 41,358/43,598 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 214.238 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.388 us | 8.657 us | 0.62x | 0.62–0.63x | -3.269 us | 9 pairs; 39/40 ms; 7,181/4,649 ops/lane; IQR 1.008/1.004 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.897 us | 0.869 us | 1.03x | 1.03–1.04x | +0.026 us | 9 pairs; 38/40 ms; 42,714/45,499 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 91.281 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.907 us | 0.947 us | 0.96x | 0.95–0.97x | -0.039 us | 9 pairs; 40/40 ms; 43,920/42,009 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 121.000 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.060 us | — | — | — | — | — | `—` | 0 B | — | 14.244 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.813 us | 0.847 us | 0.96x | 0.95–0.96x | -0.035 us | 9 pairs; 39/41 ms; 47,936/48,625 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 55.312 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.964 us | — | — | — | — | — | `—` | 0 B | — | 14.669 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.828 us | 1.230 us | 0.67x | 0.67–0.67x | -0.403 us | 9 pairs; 39/40 ms; 46,650/32,648 ops/lane; IQR 1.012/1.013 | `Pcre2Slice` | 0 B | 0 B | 63.500 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.363 us | 2.233 us | 0.61x | 0.61–0.62x | -0.870 us | 9 pairs; 41/39 ms; 29,899/17,374 ops/lane; IQR 1.008/1.009 | `Pcre2Literal` | 0 B | 0 B | 0.706 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.657 us | 1.035 us | 0.63x | 0.63–0.64x | -0.377 us | 9 pairs; 40/39 ms; 60,173/37,590 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.137 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.667 us | 1.167 us | 0.57x | 0.57–0.57x | -0.501 us | 9 pairs; 38/41 ms; 57,271/35,206 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 115.225 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.620 us | 1.014 us | 0.61x | 0.61–0.61x | -0.395 us | 9 pairs; 41/41 ms; 66,364/40,230 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 141.125 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.640 us | 1.151 us | 0.56x | 0.56–0.56x | -0.509 us | 9 pairs; 41/39 ms; 63,465/34,209 ops/lane; IQR 1.007/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 102.906 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.604 us | 0.947 us | 0.64x | 0.64–0.64x | -0.340 us | 9 pairs; 39/40 ms; 64,453/41,802 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 214.575 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.796 us | 2.142 us | 0.84x | 0.84–0.84x | -0.344 us | 9 pairs; 39/40 ms; 21,543/18,856 ops/lane; IQR 1.007/1.006 | `Pcre2Character` | 0 B | 0 B | 1.500 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.601 us | 0.868 us | 0.69x | 0.69–0.70x | -0.266 us | 9 pairs; 40/41 ms; 66,545/46,910 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 90.656 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.615 us | 0.937 us | 0.66x | 0.65–0.66x | -0.323 us | 9 pairs; 39/40 ms; 63,100/42,901 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 119.213 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.693 us | — | — | — | — | — | `—` | 0 B | — | 13.463 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.481 us | 0.856 us | 0.57x | 0.56–0.57x | -0.371 us | 9 pairs; 39/39 ms; 81,486/46,050 ops/lane; IQR 1.011/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 52.663 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.629 us | — | — | — | — | — | `—` | 0 B | — | 13.806 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.566 us | 1.231 us | 0.46x | 0.46–0.46x | -0.663 us | 9 pairs; 40/40 ms; 70,167/32,258 ops/lane; IQR 1.007/1.004 | `Pcre2Slice` | 0 B | 0 B | 62.806 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.334 us | — | — | — | — | — | `—` | 480 B | — | 1.163 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.096 us | — | — | — | — | — | `—` | 48 B | — | 130.575 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.195 us | — | — | — | — | — | `—` | 48 B | — | 111.750 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.079 us | — | — | — | — | — | `—` | 48 B | — | 140.213 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.096 us | — | — | — | — | — | `—` | 48 B | — | 97.594 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.078 us | — | — | — | — | — | `—` | 48 B | — | 213.762 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.514 us | — | — | — | — | — | `—` | 64 B | — | 1.319 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.096 us | — | — | — | — | — | `—` | 40 B | — | 87.881 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.092 us | — | — | — | — | — | `—` | 40 B | — | 119.144 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.967 us | — | — | — | — | — | `—` | 1,216 B | — | 12.963 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.041 us | — | — | — | — | — | `—` | 48 B | — | 52.312 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.529 us | — | — | — | — | — | `—` | 928 B | — | 12.956 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.739 us | — | — | — | — | — | `—` | 28,928 B | — | 62.550 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.274–214.478 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.437–93.941 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.713–12.433 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.341–50.003 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.031–1.427 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.588–43.817 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.696–22.640 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.077–99.301 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.113–71.625 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.204–5.554 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 47.309–368.511 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.135–304.236 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.824–5.023 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.189–15.619 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 168.726–1,345.550 us |

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
