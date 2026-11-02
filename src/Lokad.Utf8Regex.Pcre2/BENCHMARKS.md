<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `A0CDFA32229547FED6158A8FE1F9EA9705120B7B8A9773EE3A782671EA420C89`
- Latest managed row measurement: `2026-11-02T00:31:51.3785679+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-02T00:31:51.3785679+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `41/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `73` managed faster, `4` equivalent, `11` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `27078deb7d80` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `27078deb7d80` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.888 us | 2.254 us | 0.42x | 0.37–0.44x | -1.335 us | 9 pairs; 33/34 ms; 37,497/15,097 ops/lane; IQR 1.080/1.121 | `Pcre2Slice` | 518.058 us | 0.282 us | 0.851 us | 1.04x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.143 us | 12.847 us | 0.09x | 0.08–0.11x | -11.704 us | 9 pairs; 36/40 ms; 31,919/3,127 ops/lane; IQR 1.014/1.222 | `Pcre2Slice` | 1.774 us | 0.097 us | 3.702 us | 0.31x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.237 us | 0.01x | 0.01–0.01x | -1.226 us | 9 pairs; 39/40 ms; 3,634,663/32,478 ops/lane; IQR 1.006/1.003 | `Pcre2Slice` | 0.216 us | 1.276 us | 1.289 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.504 us | 0.160 us | 9.42x | 9.32–9.45x | +1.344 us | 9 pairs; 38/39 ms; 25,309/242,962 ops/lane; IQR 1.011/1.018 | `Pcre2Backtracking` | 0.046 us | 0.123 us | 0.152 us | 9.90x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 5.180 us | 0.571 us | 9.08x | 9.00–9.12x | +4.611 us | 9 pairs; 40/40 ms; 7,642/69,210 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0.129 us | 0.766 us | 0.795 us | 6.52x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.174 us | 0.295 us | 0.59x | 0.59–0.59x | -0.121 us | 9 pairs; 40/39 ms; 228,429/132,477 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | 0.180 us | 0.320 us | 0.347 us | 0.50x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.183 us | 0.361 us | 0.51x | 0.50–0.51x | -0.177 us | 9 pairs; 41/39 ms; 221,855/108,774 ops/lane; IQR 1.002/1.007 | `Pcre2Slice` | 0.194 us | 0.449 us | 0.470 us | 0.39x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.120 us | 0.294 us | 0.41x | 0.41–0.41x | -0.173 us | 9 pairs; 40/40 ms; 332,770/134,997 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 0.143 us | 0.239 us | 0.257 us | 0.47x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.115 us | 0.314 us | 0.37x | 0.36–0.37x | -0.200 us | 9 pairs; 40/41 ms; 347,255/128,971 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0.143 us | 0.252 us | 0.274 us | 0.42x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.171 us | 0.076 us | 2.26x | 2.18–2.31x | +0.096 us | 9 pairs; 40/38 ms; 231,998/505,665 ops/lane; IQR 1.007/1.025 | `Pcre2Slice` | 0.218 us | 1.019 us | 1.052 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.034 us | 0.199 us | 0.17x | 0.16–0.17x | -0.165 us | 9 pairs; 40/39 ms; 1,175,293/196,283 ops/lane; IQR 1.004/1.033 | `Pcre2Backtracking` | 0.212 us | 0.300 us | 0.339 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.615 us | 0.10x | 0.10–0.10x | -0.554 us | 9 pairs; 40/40 ms; 656,832/64,941 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0.199 us | 0.235 us | 0.256 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.157 us | 0.22x | 0.22–0.22x | -0.123 us | 9 pairs; 39/39 ms; 1,143,564/248,990 ops/lane; IQR 1.022/1.010 | `Pcre2Backtracking` | 0.105 us | 0.185 us | 0.204 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.113 us | 0.19x | 0.18–0.19x | -0.092 us | 9 pairs; 41/41 ms; 1,951,493/364,694 ops/lane; IQR 1.014/1.010 | `Pcre2Backtracking` | 0.067 us | 0.091 us | 0.110 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.059 us | 0.126 us | 0.47x | 0.46–0.47x | -0.068 us | 9 pairs; 40/41 ms; 682,617/322,221 ops/lane; IQR 1.004/1.016 | `Pcre2Backtracking` | 0.016 us | 0.087 us | 0.102 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.188 us | 0.279 us | 0.68x | 0.67–0.68x | -0.090 us | 9 pairs; 40/41 ms; 213,003/146,304 ops/lane; IQR 1.005/1.009 | `Pcre2Slice` | 0.211 us | 0.117 us | 0.150 us | 1.25x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.116 us | 0.19x | 0.19–0.19x | -0.094 us | 9 pairs; 39/40 ms; 1,765,653/347,497 ops/lane; IQR 1.020/1.012 | `Pcre2Backtracking` | 0.019 us | 0.091 us | 0.110 us | 0.20x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.112 us | 2.096 us | 0.05x | 0.05–0.06x | -1.984 us | 9 pairs; 40/50 ms; 355,411/23,794 ops/lane; IQR 1.002/1.207 | `Pcre2Literal` | 0.119 us | 0.050 us | 0.628 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.117 us | 1.899 us | 0.06x | 0.06–0.07x | -1.781 us | 9 pairs; 40/38 ms; 344,487/20,248 ops/lane; IQR 1.003/1.102 | `Pcre2Literal` | 0.174 us | 0.054 us | 0.638 us | 0.18x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.148 us | 2.783 us | 0.05x | 0.05–0.06x | -2.635 us | 9 pairs; 40/47 ms; 266,818/16,980 ops/lane; IQR 1.013/1.120 | `Pcre2Literal` | 0.274 us | 0.233 us | 0.796 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.144 us | 1.920 us | 0.07x | 0.07–0.08x | -1.777 us | 9 pairs; 40/38 ms; 280,484/19,765 ops/lane; IQR 1.008/1.026 | `Pcre2Literal` | 0.208 us | 0.205 us | 0.767 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.091 us | 9 pairs; 39/40 ms; 4,426,426/398,868 ops/lane; IQR 1.009/1.013 | `Pcre2Slice` | 1.003 us | 0.069 us | 0.086 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.066 us | 0.085 us | 0.78x | 0.78–0.80x | -0.018 us | 9 pairs; 42/39 ms; 631,234/462,085 ops/lane; IQR 1.036/1.008 | `Pcre2Literal` | 0.122 us | 0.051 us | 0.073 us | 0.90x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.109 us | 0.12x | 0.12–0.13x | -0.096 us | 9 pairs; 39/42 ms; 2,870,923/382,756 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0.246 us | 0.091 us | 0.114 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.110 us | 0.123 us | 0.89x | 0.89–0.90x | -0.013 us | 9 pairs; 38/39 ms; 343,487/315,506 ops/lane; IQR 1.001/1.011 | `Pcre2Character` | 0.136 us | 0.113 us | 0.146 us | 0.76x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.086 us | 0.203 us | 0.42x | 0.39–0.43x | -0.117 us | 9 pairs; 40/39 ms; 467,205/194,220 ops/lane; IQR 1.032/1.004 | `Pcre2Slice` | 0.574 us | 0.074 us | 0.106 us | 0.81x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 78.559 us | 162.839 us | 0.48x | 0.48–0.48x | -84.172 us | 9 pairs; 40/40 ms; 511/245 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | 420.130 us | 102.174 us | 104.027 us | 0.76x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.244 us | 283.866 us | 0.13x | 0.13–0.13x | -246.622 us | 9 pairs; 40/39 ms; 1,066/138 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 394.070 us | 219.779 us | 229.280 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.290 us | 210.795 us | 0.05x | 0.05–0.05x | -200.527 us | 9 pairs; 40/41 ms; 3,898/195 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 8.069 us | 228.172 us | 229.621 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.265 us | 218.317 us | 1.11x | 1.10–1.11x | +23.235 us | 9 pairs; 40/38 ms; 164/175 ops/lane; IQR 1.001/1.004 | `Pcre2SingleTokenRepeat` | 8.064 us | 235.791 us | 238.821 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.555 us | 23.447 us | 0.15x | 0.15–0.15x | -19.903 us | 9 pairs; 39/41 ms; 11,103/1,760 ops/lane; IQR 1.005/1.037 | `Pcre2Literal` | 1.466 us | 4.225 us | 5.884 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.584 us | 81.358 us | 0.12x | 0.12–0.12x | -71.768 us | 9 pairs; 40/40 ms; 4,153/490 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 9.424 us | 93.101 us | 95.045 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,222.688 us | 28,962.700 us | 0.15x | 0.14–0.15x | -24741.663 us | 9 pairs; 34/29 ms; 8/1 ops/lane; IQR 1.017/1.022 | `Pcre2Backtracking` | 5,911.289 us | 12,526.180 us | 19,330.800 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 5,891.214 us | 60,658.800 us | 0.10x | 0.09–0.10x | -54813.714 us | 9 pairs; 41/61 ms; 7/1 ops/lane; IQR 1.038/1.005 | `Pcre2Backtracking` | 6,474.312 us | 44,258.150 us | 51,381.900 us | 0.11x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,957.176 us | 493,288.800 us | 0.00x | 0.00–0.00x | -491398.105 us | 9 pairs; 41/493 ms; 21/1 ops/lane; IQR 1.038/1.003 | `Pcre2Character` | 1,776.734 us | 23,053.567 us | 29,530.000 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,545.183 us | 11,662.733 us | 0.47x | 0.45–0.54x | -6151.350 us | 9 pairs; 33/35 ms; 6/3 ops/lane; IQR 1.007/1.064 | `Pcre2Literal` | 1,411.292 us | 1,353.815 us | 8,136.757 us | 0.68x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,856.350 us | 145,144.700 us | 0.13x | 0.13–0.13x | -126281.050 us | 9 pairs; 38/145 ms; 2/1 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 1,597.878 us | 508.024 us | 8,526.533 us | 2.21x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,855.425 us | 21,896.000 us | 0.22x | 0.22–0.22x | -17039.250 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.004/1.016 | `Pcre2Slice` | 2,864.167 us | 11,239.880 us | 19,308.700 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,368.450 us | 134,447.400 us | 0.17x | 0.16–0.17x | -112203.200 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.014/1.004 | `Pcre2Backtracking` | 1,914.807 us | 2,306.000 us | 10,447.760 us | 2.14x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 235.386 us | 994.085 us | 0.24x | 0.23–0.24x | -759.140 us | 9 pairs; 40/41 ms; 169/41 ops/lane; IQR 1.007/1.015 | `Pcre2Backtracking` | 282.524 us | 341.007 us | 560.787 us | 0.42x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,243.933 us | 13,534.733 us | 0.90x | 0.90–0.91x | -1305.333 us | 9 pairs; 37/41 ms; 3/3 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 315.529 us | 16,053.450 us | 16,333.325 us | 0.75x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,804.229 us | 98,725.900 us | 0.03x | 0.03–0.03x | -95930.386 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 1,888.607 us | 19,853.733 us | 20,030.767 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 186.030 us | 2,747.236 us | 0.07x | 0.07–0.07x | -2562.878 us | 9 pairs; 40/38 ms; 213/14 ops/lane; IQR 1.008/1.008 | `Pcre2SingleTokenRepeat` | 191.538 us | 1,004.820 us | 1,430.474 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,618.438 us | 20,140.150 us | 0.08x | 0.08–0.08x | -18500.758 us | 9 pairs; 39/40 ms; 24/2 ops/lane; IQR 1.011/1.027 | `Pcre2Backtracking` | 572.263 us | 10,425.380 us | 10,667.740 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.149 us | 1.086 us | 0.14x | 0.14–0.14x | -0.939 us | 9 pairs; 40/40 ms; 265,868/37,149 ops/lane; IQR 1.014/1.005 | `Pcre2Slice` | 1.471 us | 0.259 us | 0.281 us | 0.53x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.504 us | 0.829 us | 0.61x | 0.60–0.61x | -0.325 us | 9 pairs; 40/40 ms; 79,128/48,145 ops/lane; IQR 1.015/1.001 | `Pcre2Literal` | 0.149 us | 0.149 us | 0.173 us | 2.91x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.305 us | 1.182 us | 0.26x | 0.26–0.26x | -0.876 us | 9 pairs; 40/40 ms; 130,773/34,021 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 1.055 us | 0.351 us | 0.376 us | 0.81x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.874 us | 1.126 us | 0.78x | 0.77–0.78x | -0.253 us | 9 pairs; 41/41 ms; 46,944/36,195 ops/lane; IQR 1.002/1.007 | `Pcre2Character` | 0.270 us | 0.431 us | 0.472 us | 1.85x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.156 us | 0.850 us | 0.18x | 0.18–0.18x | -0.694 us | 9 pairs; 39/40 ms; 249,288/47,234 ops/lane; IQR 1.006/1.009 | `Pcre2Slice` | 1.015 us | 0.156 us | 0.202 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.939 us | 1.102 us | 0.85x | 0.85–0.86x | -0.163 us | 9 pairs; 40/40 ms; 42,326/36,495 ops/lane; IQR 1.004/1.005 | `Pcre2Slice` | 6.709 us | 0.308 us | 0.325 us | 2.89x | 0 B | 0 B |
| `simple/foo-dense` | **Native faster** | 18 B | 0.862 us | 0.829 us | 1.04x | 1.04–1.04x | +0.033 us | 9 pairs; 43/40 ms; 49,779/47,998 ops/lane; IQR 1.003/1.005 | `Pcre2Literal` | 3.607 us | 0.182 us | 0.204 us | 4.23x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.049 us | 1.192 us | 0.88x | 0.88–0.88x | -0.142 us | 9 pairs; 40/39 ms; 37,673/32,987 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 5.192 us | 0.404 us | 0.427 us | 2.45x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.247 us | 1.130 us | 1.10x | 1.10–1.11x | +0.115 us | 9 pairs; 39/40 ms; 31,191/35,275 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 3.727 us | 0.481 us | 0.521 us | 2.39x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Equivalent** | 67 B | 0.867 us | 0.860 us | 1.01x | 1.00–1.01x | +0.007 us | 9 pairs; 40/40 ms; 46,705/46,653 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 2.198 us | 0.184 us | 0.228 us | 3.80x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.569 us | 1.102 us | 0.52x | 0.52–0.52x | -0.532 us | 9 pairs; 40/40 ms; 70,517/36,074 ops/lane; IQR 1.003/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.544 us | 0.831 us | 0.65x | 0.65–0.66x | -0.286 us | 9 pairs; 39/40 ms; 72,611/47,806 ops/lane; IQR 1.011/1.008 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.667 us | 1.191 us | 0.56x | 0.56–0.56x | -0.524 us | 9 pairs; 39/39 ms; 58,389/32,970 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.909 us | 1.132 us | 0.81x | 0.80–0.81x | -0.219 us | 9 pairs; 40/40 ms; 44,043/35,114 ops/lane; IQR 1.008/1.012 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.505 us | 0.858 us | 0.59x | 0.58–0.59x | -0.354 us | 9 pairs; 39/40 ms; 77,510/46,528 ops/lane; IQR 1.011/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 63.184 us | — | — | — | — | — | `—` | 67.513 us | 99.985 us | 102.134 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 10.034 us | — | — | — | — | — | `—` | 2.776 us | 0.357 us | 0.382 us | 26.30x | 48,792 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.055 us | — | — | — | — | — | `—` | 0.841 us | 0.243 us | 0.272 us | 3.89x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.219 us | — | — | — | — | — | `—` | 2.936 us | 0.454 us | 0.485 us | 2.51x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.540 us | — | — | — | — | — | `—` | 0.940 us | 0.536 us | 0.612 us | 2.51x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.506 us | — | — | — | — | — | `—` | 1.521 us | 0.268 us | 0.317 us | 14.20x | 728 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Equivalent** | 5 B | 0.085 us | 0.084 us | 1.00x | 1.00–1.02x | 0.000 us | 9 pairs; 39/40 ms; 455,319/470,775 ops/lane; IQR 1.007/1.007 | `Pcre2Character` | 0 B | 0 B | 2.325 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.118 us | 0.11x | 0.10–0.11x | -0.105 us | 9 pairs; 39/38 ms; 3,086,589/319,822 ops/lane; IQR 1.006/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 132.725 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.383 us | 0.106 us | 3.60x | 3.52–3.65x | +0.276 us | 9 pairs; 36/39 ms; 94,053/365,019 ops/lane; IQR 1.005/1.031 | `Pcre2Backtracking` | 0 B | 0 B | 21.531 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.386 us | 0.117 us | 3.31x | 3.28–3.32x | +0.269 us | 9 pairs; 35/40 ms; 91,740/342,874 ops/lane; IQR 1.014/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 71.144 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.386 us | 0.103 us | 3.74x | 3.62–3.77x | +0.283 us | 9 pairs; 35/39 ms; 91,814/375,596 ops/lane; IQR 1.005/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 70.775 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.561 us | 0.347 us | 1.62x | 1.61–1.63x | +0.215 us | 9 pairs; 37/40 ms; 65,770/114,296 ops/lane; IQR 1.005/1.004 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.011 us | 0.647 us | 0.02x | 0.02–0.03x | -0.635 us | 9 pairs; 40/39 ms; 3,514,309/60,544 ops/lane; IQR 1.061/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 21.788 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.514 us | 0.02x | 0.02–0.02x | -0.502 us | 9 pairs; 38/39 ms; 3,253,214/76,169 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 21.000 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.465 us | 0.116 us | 4.00x | 3.99–4.02x | +0.349 us | 9 pairs; 36/41 ms; 77,137/356,971 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 18.188 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.356 us | 10.539 us | 0.79x | 0.79–0.80x | -2.181 us | 9 pairs; 40/41 ms; 4,770/3,873 ops/lane; IQR 1.004/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.299 us | 1.048 us | 0.29x | 0.28–0.29x | -0.749 us | 9 pairs; 40/41 ms; 133,544/39,459 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 131.662 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.314 us | 0.465 us | 0.68x | 0.67–0.68x | -0.151 us | 9 pairs; 33/39 ms; 106,657/83,803 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.838 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.312 us | 0.467 us | 0.67x | 0.66–0.68x | -0.155 us | 9 pairs; 38/38 ms; 122,129/81,763 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 142.719 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.321 us | 0.575 us | 0.56x | 0.56–0.56x | -0.254 us | 9 pairs; 38/40 ms; 117,313/68,826 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 102.469 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.325 us | 0.408 us | 0.80x | 0.78–0.80x | -0.081 us | 9 pairs; 41/40 ms; 126,436/98,959 ops/lane; IQR 1.004/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 213.281 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.788 us | 8.822 us | 0.54x | 0.54–0.55x | -4.036 us | 9 pairs; 40/41 ms; 8,284/4,619 ops/lane; IQR 1.005/1.002 | `Pcre2Character` | 0 B | 0 B | 1.494 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.270 us | 0.311 us | 0.87x | 0.85–0.88x | -0.040 us | 9 pairs; 37/39 ms; 136,983/124,846 ops/lane; IQR 1.016/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 94.219 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.283 us | 0.398 us | 0.71x | 0.71–0.71x | -0.116 us | 9 pairs; 37/41 ms; 129,404/102,369 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 121.225 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.728 us | — | — | — | — | — | `—` | 0 B | — | 13.600 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.198 us | 0.293 us | 0.67x | 0.67–0.68x | -0.096 us | 9 pairs; 40/40 ms; 199,700/134,811 ops/lane; IQR 1.012/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 53.425 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.596 us | — | — | — | — | — | `—` | 0 B | — | 13.575 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.522 us | 0.655 us | 0.79x | 0.79–0.80x | -0.135 us | 9 pairs; 37/40 ms; 71,879/61,201 ops/lane; IQR 1.004/1.007 | `Pcre2Slice` | 0 B | 0 B | 63.688 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 10.050 us | 28.927 us | 0.35x | 0.35–0.35x | -18.863 us | 9 pairs; 40/40 ms; 3,940/1,387 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 1.010 us | 1.065 us | 0.95x | 0.94–0.95x | -0.056 us | 9 pairs; 40/40 ms; 39,405/37,348 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 127.044 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.050 us | 1.193 us | 0.88x | 0.88–0.89x | -0.138 us | 9 pairs; 38/41 ms; 36,647/34,195 ops/lane; IQR 1.018/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.906 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.974 us | 1.026 us | 0.95x | 0.94–0.95x | -0.051 us | 9 pairs; 40/39 ms; 40,554/38,157 ops/lane; IQR 1.011/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 140.438 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 1.047 us | 1.171 us | 0.89x | 0.89–0.89x | -0.127 us | 9 pairs; 42/40 ms; 39,657/34,140 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 103.119 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 1.002 us | 0.999 us | 1.00x | 1.00–1.01x | +0.003 us | 9 pairs; 38/40 ms; 37,993/40,498 ops/lane; IQR 1.007/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 215.675 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.490 us | 8.888 us | 0.62x | 0.62–0.62x | -3.398 us | 9 pairs; 41/41 ms; 7,433/4,573 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.959 us | 0.885 us | 1.08x | 1.08–1.10x | +0.075 us | 9 pairs; 39/41 ms; 40,716/46,135 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 91.794 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Equivalent** | 26 B | 0.962 us | 0.962 us | 1.00x | 0.99–1.01x | 0.000 us | 9 pairs; 41/41 ms; 42,756/42,668 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 123.281 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.964 us | — | — | — | — | — | `—` | 0 B | — | 13.319 us | 11,496 B |
| `pcre2/kreset-global` | **Inconclusive** | 26 B | 0.881 us | 0.867 us | 1.02x | 1.01–1.02x | +0.014 us | 9 pairs; 42/40 ms; 47,750/46,394 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 52.444 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.795 us | — | — | — | — | — | `—` | 0 B | — | 14.656 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.920 us | 1.245 us | 0.74x | 0.74–0.74x | -0.326 us | 9 pairs; 41/39 ms; 44,781/31,655 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 0 B | 0 B | 63.562 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.358 us | 2.294 us | 0.59x | 0.59–0.59x | -0.934 us | 9 pairs; 40/40 ms; 29,805/17,571 ops/lane; IQR 1.002/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.652 us | 1.066 us | 0.61x | 0.61–0.61x | -0.414 us | 9 pairs; 42/40 ms; 64,695/37,887 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 131.500 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.708 us | 1.194 us | 0.59x | 0.59–0.60x | -0.484 us | 9 pairs; 42/40 ms; 59,953/33,530 ops/lane; IQR 1.009/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.144 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.623 us | 1.047 us | 0.60x | 0.60–0.60x | -0.422 us | 9 pairs; 38/40 ms; 60,661/38,357 ops/lane; IQR 1.004/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 143.463 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.637 us | 1.164 us | 0.55x | 0.54–0.55x | -0.525 us | 9 pairs; 35/40 ms; 54,720/34,503 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 103.906 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.625 us | 1.001 us | 0.62x | 0.62–0.63x | -0.376 us | 9 pairs; 40/41 ms; 64,098/40,772 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 212.500 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.807 us | 2.180 us | 0.83x | 0.83–0.83x | -0.376 us | 9 pairs; 41/40 ms; 22,654/18,339 ops/lane; IQR 1.008/1.009 | `Pcre2Character` | 0 B | 0 B | 2.119 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.611 us | 0.886 us | 0.69x | 0.69–0.69x | -0.275 us | 9 pairs; 41/40 ms; 66,931/45,112 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 93.119 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.613 us | 0.962 us | 0.64x | 0.63–0.64x | -0.350 us | 9 pairs; 40/40 ms; 65,781/41,365 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 125.463 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.601 us | — | — | — | — | — | `—` | 0 B | — | 13.125 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.514 us | 0.866 us | 0.60x | 0.59–0.60x | -0.348 us | 9 pairs; 41/40 ms; 78,856/46,354 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.494 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.509 us | — | — | — | — | — | `—` | 0 B | — | 13.994 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.549 us | 1.250 us | 0.44x | 0.44–0.44x | -0.703 us | 9 pairs; 39/40 ms; 71,819/31,928 ops/lane; IQR 1.009/1.007 | `Pcre2Slice` | 0 B | 0 B | 63.938 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.564 us | — | — | — | — | — | `—` | 480 B | — | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.109 us | — | — | — | — | — | `—` | 48 B | — | 132.912 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.212 us | — | — | — | — | — | `—` | 48 B | — | 115.188 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.101 us | — | — | — | — | — | `—` | 48 B | — | 143.425 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.109 us | — | — | — | — | — | `—` | 48 B | — | 99.844 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.091 us | — | — | — | — | — | `—` | 48 B | — | 217.444 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.154 us | — | — | — | — | — | `—` | 64 B | — | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.131 us | — | — | — | — | — | `—` | 40 B | — | 92.875 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.115 us | — | — | — | — | — | `—` | 40 B | — | 124.112 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.779 us | — | — | — | — | — | `—` | 1,224 B | — | 13.319 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.050 us | — | — | — | — | — | `—` | 48 B | — | 53.413 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.486 us | — | — | — | — | — | `—` | 936 B | — | 13.588 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 6.412 us | — | — | — | — | — | `—` | 28,936 B | — | 64.556 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.711–215.245 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.369–92.598 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.057–14.614 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.283–48.292 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.034–1.496 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.545–42.723 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.743–22.526 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.245–99.833 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.109–72.494 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.216–5.568 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 49.877–390.067 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.063–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.232–306.269 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.864–5.077 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.135–137.200 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 166.578–1,318.856 us |

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
