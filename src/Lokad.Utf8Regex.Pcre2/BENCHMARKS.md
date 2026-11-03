<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `27BEA3FE9E714728293E0360424A19C0CE1E52FB41B4CF4A1D84063ADD5A25AC`
- Latest managed row measurement: `2026-11-03T18:40:33.3932852+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-03T18:40:33.3932852+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `41/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `77` managed faster, `1` equivalent, `11` native faster, `12` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `60022add0dce` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `60022add0dce` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.869 us | 2.212 us | 0.41x | 0.39–0.42x | -1.282 us | 9 pairs; 33/31 ms; 38,366/14,112 ops/lane; IQR 1.073/1.050 | `Pcre2Slice` | 518.666 us | 0.284 us | 0.851 us | 1.02x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Managed faster** | 22,528 B | 1.122 us | 12.690 us | 0.09x | 0.08–0.10x | -11.558 us | 9 pairs; 37/57 ms; 33,100/4,462 ops/lane; IQR 1.010/1.081 | `Pcre2Slice` | 1.674 us | 0.097 us | 3.678 us | 0.30x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.235 us | 0.01x | 0.01–0.01x | -1.224 us | 9 pairs; 40/40 ms; 3,635,335/32,006 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 0.178 us | 1.107 us | 1.135 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.502 us | 0.160 us | 9.39x | 9.38–9.40x | +1.341 us | 9 pairs; 39/40 ms; 25,671/251,102 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 0.049 us | 0.121 us | 0.156 us | 9.64x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 5.241 us | 0.578 us | 9.07x | 8.95–9.12x | +4.660 us | 9 pairs; 40/41 ms; 7,562/70,212 ops/lane; IQR 1.001/1.009 | `Pcre2Backtracking` | 0.115 us | 0.748 us | 0.784 us | 6.68x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.167 us | 0.297 us | 0.56x | 0.54–0.57x | -0.130 us | 9 pairs; 44/40 ms; 261,576/133,189 ops/lane; IQR 1.018/1.021 | `Pcre2Slice` | 0.160 us | 0.322 us | 0.345 us | 0.48x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.160 us | 0.359 us | 0.45x | 0.44–0.45x | -0.198 us | 9 pairs; 41/39 ms; 256,070/110,010 ops/lane; IQR 1.001/1.007 | `Pcre2Slice` | 0.168 us | 0.454 us | 0.474 us | 0.34x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.108 us | 0.289 us | 0.37x | 0.37–0.38x | -0.180 us | 9 pairs; 40/40 ms; 366,229/136,997 ops/lane; IQR 1.007/1.006 | `Pcre2Slice` | 0.114 us | 0.233 us | 0.254 us | 0.43x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.110 us | 0.319 us | 0.34x | 0.34–0.35x | -0.209 us | 9 pairs; 40/39 ms; 366,147/121,687 ops/lane; IQR 1.008/1.003 | `Pcre2Slice` | 0.114 us | 0.246 us | 0.269 us | 0.41x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.173 us | 0.079 us | 2.20x | 2.10–2.30x | +0.094 us | 9 pairs; 39/38 ms; 228,445/487,024 ops/lane; IQR 1.003/1.066 | `Pcre2Slice` | 0.166 us | 0.942 us | 0.956 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Inconclusive** | 46 B | 0.034 us | 0.207 us | 0.16x | 0.16–0.17x | -0.173 us | 9 pairs; 38/40 ms; 1,117,486/195,648 ops/lane; IQR 1.004/1.021 | `Pcre2Backtracking` | 0.177 us | 0.302 us | 0.343 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.612 us | 0.10x | 0.10–0.10x | -0.552 us | 9 pairs; 40/40 ms; 659,175/65,898 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0.168 us | 0.227 us | 0.251 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.155 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 38/40 ms; 1,151,891/255,855 ops/lane; IQR 1.024/1.006 | `Pcre2Backtracking` | 0.103 us | 0.186 us | 0.207 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.112 us | 0.18x | 0.17–0.18x | -0.093 us | 9 pairs; 38/40 ms; 1,934,000/353,147 ops/lane; IQR 1.028/1.007 | `Pcre2Backtracking` | 0.059 us | 0.090 us | 0.109 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.122 us | 0.48x | 0.47–0.48x | -0.064 us | 9 pairs; 40/42 ms; 687,561/343,518 ops/lane; IQR 1.004/1.015 | `Pcre2Backtracking` | 0.015 us | 0.085 us | 0.102 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.182 us | 0.273 us | 0.66x | 0.66–0.68x | -0.092 us | 9 pairs; 40/41 ms; 217,488/148,097 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 0.186 us | 0.121 us | 0.158 us | 1.15x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Inconclusive** | 7 B | 0.026 us | 0.115 us | 0.23x | 0.20–0.23x | -0.089 us | 9 pairs; 40/39 ms; 1,526,567/340,300 ops/lane; IQR 1.103/1.012 | `Pcre2Backtracking` | 0.019 us | 0.089 us | 0.108 us | 0.24x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.114 us | 1.889 us | 0.06x | 0.05–0.06x | -1.775 us | 9 pairs; 40/44 ms; 351,913/23,429 ops/lane; IQR 1.004/1.118 | `Pcre2Literal` | 0.091 us | 0.050 us | 0.643 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.123 us | 1.725 us | 0.07x | 0.07–0.07x | -1.601 us | 9 pairs; 40/32 ms; 326,103/18,822 ops/lane; IQR 1.001/1.076 | `Pcre2Literal` | 0.150 us | 0.054 us | 0.633 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.155 us | 1.977 us | 0.08x | 0.07–0.08x | -1.823 us | 9 pairs; 40/38 ms; 258,871/18,991 ops/lane; IQR 1.004/1.104 | `Pcre2Literal` | 0.217 us | 0.233 us | 0.807 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.154 us | 1.926 us | 0.08x | 0.07–0.08x | -1.773 us | 9 pairs; 40/41 ms; 263,113/21,120 ops/lane; IQR 1.002/1.051 | `Pcre2Literal` | 0.183 us | 0.205 us | 0.778 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.015 us | 0.098 us | 0.16x | 0.09–0.16x | -0.082 us | 9 pairs; 69/40 ms; 4,481,094/404,069 ops/lane; IQR 1.366/1.018 | `Pcre2Slice` | 0.934 us | 0.069 us | 0.087 us | 0.18x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.070 us | 0.085 us | 0.82x | 0.78–0.83x | -0.015 us | 9 pairs; 41/39 ms; 587,870/455,758 ops/lane; IQR 1.009/1.009 | `Pcre2Literal` | 0.124 us | 0.051 us | 0.073 us | 0.95x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.109 us | 0.13x | 0.13–0.13x | -0.094 us | 9 pairs; 41/40 ms; 2,859,337/367,319 ops/lane; IQR 1.017/1.022 | `Pcre2Backtracking` | 0.240 us | 0.096 us | 0.120 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.122 us | 0.94x | 0.93–0.98x | -0.007 us | 9 pairs; 38/39 ms; 331,412/321,604 ops/lane; IQR 1.046/1.004 | `Pcre2Character` | 0.135 us | 0.113 us | 0.144 us | 0.79x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.076 us | 0.207 us | 0.37x | 0.36–0.37x | -0.131 us | 9 pairs; 40/40 ms; 532,781/195,022 ops/lane; IQR 1.002/1.015 | `Pcre2Slice` | 0.555 us | 0.077 us | 0.107 us | 0.71x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.895 us | 160.520 us | 0.52x | 0.51–0.52x | -77.799 us | 9 pairs; 40/41 ms; 482/254 ops/lane; IQR 1.002/1.007 | `Pcre2Slice` | 427.617 us | 99.391 us | 100.888 us | 0.82x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.436 us | 286.499 us | 0.13x | 0.13–0.13x | -249.033 us | 9 pairs; 40/40 ms; 1,062/138 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 392.114 us | 217.863 us | 227.188 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.963 us | 211.095 us | 0.05x | 0.05–0.05x | -201.132 us | 9 pairs; 39/39 ms; 3,911/186 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 7.209 us | 220.255 us | 220.828 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 238.068 us | 216.684 us | 1.10x | 1.10–1.10x | +21.560 us | 9 pairs; 40/40 ms; 166/186 ops/lane; IQR 1.001/1.005 | `Pcre2SingleTokenRepeat` | 7.281 us | 232.193 us | 235.950 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.534 us | 22.899 us | 0.15x | 0.15–0.16x | -19.366 us | 9 pairs; 39/41 ms; 11,139/1,771 ops/lane; IQR 1.005/1.012 | `Pcre2Literal` | 1.409 us | 4.289 us | 6.115 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.629 us | 81.317 us | 0.12x | 0.12–0.12x | -71.690 us | 9 pairs; 40/40 ms; 4,165/494 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 9.325 us | 92.867 us | 94.964 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,178.040 us | 28,948.000 us | 0.15x | 0.14–0.16x | -24736.320 us | 9 pairs; 42/29 ms; 10/1 ops/lane; IQR 1.011/1.139 | `Pcre2Backtracking` | 5,629.600 us | 11,767.800 us | 18,921.967 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 5,946.729 us | 55,745.300 us | 0.11x | 0.10–0.11x | -49803.643 us | 9 pairs; 42/56 ms; 7/1 ops/lane; IQR 1.026/1.017 | `Pcre2Backtracking` | 6,408.075 us | 40,566.750 us | 47,447.600 us | 0.13x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,946.567 us | 497,184.800 us | 0.00x | 0.00–0.00x | -495304.457 us | 9 pairs; 41/497 ms; 21/1 ops/lane; IQR 1.038/1.002 | `Pcre2Character` | 1,821.557 us | 24,253.200 us | 30,477.500 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Managed faster** | 16,013,977 B | 5,536.167 us | 11,055.700 us | 0.50x | 0.49–0.53x | -5504.458 us | 9 pairs; 33/44 ms; 6/4 ops/lane; IQR 1.006/1.063 | `Pcre2Literal` | 1,419.186 us | 1,401.513 us | 8,621.967 us | 0.64x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,733.800 us | 145,618.900 us | 0.13x | 0.13–0.13x | -127030.050 us | 9 pairs; 37/146 ms; 2/1 ops/lane; IQR 1.011/1.004 | `Pcre2Backtracking` | 1,598.834 us | 508.424 us | 8,545.400 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,778.413 us | 21,883.700 us | 0.22x | 0.22–0.22x | -17082.538 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.002/1.012 | `Pcre2Slice` | 2,871.533 us | 10,915.260 us | 18,920.133 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,313.600 us | 134,309.000 us | 0.17x | 0.17–0.17x | -111902.300 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 1,919.112 us | 2,394.545 us | 10,503.320 us | 2.12x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 236.022 us | 1,016.441 us | 0.23x | 0.23–0.24x | -780.878 us | 9 pairs; 39/45 ms; 167/44 ops/lane; IQR 1.004/1.041 | `Pcre2Backtracking` | 287.267 us | 333.447 us | 558.535 us | 0.42x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,557.800 us | 13,350.733 us | 0.87x | 0.86–0.87x | -1799.200 us | 9 pairs; 35/40 ms; 3/3 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 312.816 us | 15,159.700 us | 15,493.500 us | 0.75x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,932.262 us | 98,703.100 us | 0.03x | 0.03–0.03x | -95766.146 us | 9 pairs; 38/99 ms; 13/1 ops/lane; IQR 1.003/1.001 | `Pcre2Character` | 2,033.944 us | 20,082.200 us | 20,399.400 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.149 us | 2,764.680 us | 0.07x | 0.07–0.07x | -2579.707 us | 9 pairs; 40/41 ms; 214/15 ops/lane; IQR 1.004/1.029 | `Pcre2SingleTokenRepeat` | 190.002 us | 1,008.092 us | 1,429.224 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,559.232 us | 20,318.100 us | 0.08x | 0.08–0.08x | -18757.580 us | 9 pairs; 39/41 ms; 25/2 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 579.565 us | 10,758.840 us | 10,949.200 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.152 us | 1.099 us | 0.14x | 0.14–0.14x | -0.946 us | 9 pairs; 39/40 ms; 256,666/36,125 ops/lane; IQR 1.011/1.000 | `Pcre2Slice` | 1.419 us | 0.259 us | 0.280 us | 0.54x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.541 us | 0.831 us | 0.65x | 0.65–0.66x | -0.288 us | 9 pairs; 40/40 ms; 73,537/48,043 ops/lane; IQR 1.009/1.002 | `Pcre2Literal` | 0.141 us | 0.149 us | 0.172 us | 3.14x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.290 us | 1.190 us | 0.24x | 0.24–0.24x | -0.900 us | 9 pairs; 39/40 ms; 133,775/33,938 ops/lane; IQR 1.002/1.001 | `Pcre2Backtracking` | 1.061 us | 0.355 us | 0.385 us | 0.75x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.930 us | 1.128 us | 0.83x | 0.82–0.83x | -0.197 us | 9 pairs; 43/40 ms; 46,323/35,399 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 0.278 us | 0.426 us | 0.467 us | 1.99x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.166 us | 0.862 us | 0.19x | 0.19–0.19x | -0.695 us | 9 pairs; 41/40 ms; 245,337/46,368 ops/lane; IQR 1.014/1.002 | `Pcre2Slice` | 0.985 us | 0.155 us | 0.202 us | 0.82x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.891 us | 1.105 us | 0.81x | 0.81–0.81x | -0.214 us | 9 pairs; 40/40 ms; 44,535/36,110 ops/lane; IQR 1.002/1.001 | `Pcre2Slice` | 6.494 us | 0.308 us | 0.325 us | 2.74x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.818 us | 0.845 us | 0.97x | 0.96–0.98x | -0.026 us | 9 pairs; 38/41 ms; 46,685/48,032 ops/lane; IQR 1.013/1.001 | `Pcre2Literal` | 3.576 us | 0.182 us | 0.203 us | 4.03x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.035 us | 1.189 us | 0.87x | 0.87–0.87x | -0.153 us | 9 pairs; 42/40 ms; 40,184/33,616 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 5.265 us | 0.421 us | 0.441 us | 2.35x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.180 us | 1.135 us | 1.04x | 1.04–1.04x | +0.044 us | 9 pairs; 39/37 ms; 32,927/32,881 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 3.669 us | 0.478 us | 0.517 us | 2.28x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.798 us | 0.863 us | 0.92x | 0.92–0.93x | -0.065 us | 9 pairs; 40/40 ms; 49,888/46,698 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | 2.187 us | 0.183 us | 0.225 us | 3.55x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.590 us | 1.106 us | 0.53x | 0.53–0.53x | -0.516 us | 9 pairs; 42/40 ms; 70,633/35,985 ops/lane; IQR 1.002/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.544 us | 0.845 us | 0.64x | 0.64–0.65x | -0.301 us | 9 pairs; 40/41 ms; 73,185/48,023 ops/lane; IQR 1.004/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.728 us | 1.188 us | 0.61x | 0.61–0.61x | -0.460 us | 9 pairs; 40/40 ms; 55,319/33,705 ops/lane; IQR 1.001/1.001 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.904 us | 1.134 us | 0.80x | 0.79–0.80x | -0.231 us | 9 pairs; 39/40 ms; 42,750/34,929 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.479 us | 0.863 us | 0.55x | 0.55–0.56x | -0.384 us | 9 pairs; 39/39 ms; 82,502/44,839 ops/lane; IQR 1.001/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.597 us | — | — | — | — | — | `—` | 66.972 us | 99.250 us | 101.027 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.940 us | — | — | — | — | — | `—` | 2.817 us | 0.361 us | 0.390 us | 25.48x | 48,784 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.032 us | — | — | — | — | — | `—` | 0.862 us | 0.244 us | 0.275 us | 3.76x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.183 us | — | — | — | — | — | `—` | 3.017 us | 0.470 us | 0.505 us | 2.34x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.529 us | — | — | — | — | — | `—` | 0.974 us | 0.540 us | 0.594 us | 2.58x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.476 us | — | — | — | — | — | `—` | 1.517 us | 0.274 us | 0.330 us | 13.57x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.089 us | 0.086 us | 1.04x | 1.04–1.05x | +0.004 us | 9 pairs; 38/41 ms; 421,071/473,768 ops/lane; IQR 1.002/1.007 | `Pcre2Character` | 0 B | 0 B | 2.312 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.121 us | 0.10x | 0.10–0.11x | -0.108 us | 9 pairs; 40/39 ms; 3,134,819/324,211 ops/lane; IQR 1.001/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 132.012 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.390 us | 0.104 us | 3.75x | 3.63–3.76x | +0.286 us | 9 pairs; 35/39 ms; 90,212/373,201 ops/lane; IQR 1.005/1.025 | `Pcre2Backtracking` | 0 B | 0 B | 20.225 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.393 us | 0.120 us | 3.27x | 3.25–3.30x | +0.273 us | 9 pairs; 36/39 ms; 92,207/328,008 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 70.100 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.393 us | 0.105 us | 3.76x | 3.59–3.77x | +0.289 us | 9 pairs; 37/39 ms; 94,096/372,386 ops/lane; IQR 1.016/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 69.544 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.569 us | 0.347 us | 1.64x | 1.63–1.65x | +0.222 us | 9 pairs; 37/39 ms; 65,520/113,578 ops/lane; IQR 1.005/1.003 | `Pcre2Character` | 0 B | 0 B | 1.488 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.633 us | 0.02x | 0.02–0.02x | -0.620 us | 9 pairs; 42/40 ms; 3,445,183/63,192 ops/lane; IQR 1.033/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 22.800 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.016 us | 0.511 us | 0.03x | 0.02–0.03x | -0.495 us | 9 pairs; 58/39 ms; 3,500,855/76,166 ops/lane; IQR 1.230/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 21.156 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.471 us | 0.109 us | 4.32x | 4.25–4.35x | +0.362 us | 9 pairs; 37/40 ms; 77,919/366,911 ops/lane; IQR 1.004/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 20.613 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.446 us | 10.483 us | 0.80x | 0.80–0.81x | -2.057 us | 9 pairs; 40/39 ms; 4,744/3,707 ops/lane; IQR 1.006/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.800 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.296 us | 1.052 us | 0.28x | 0.28–0.28x | -0.756 us | 9 pairs; 39/40 ms; 132,495/38,193 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 129.856 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.348 us | 0.476 us | 0.72x | 0.71–0.73x | -0.131 us | 9 pairs; 40/41 ms; 114,121/85,802 ops/lane; IQR 1.032/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 115.619 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.325 us | 0.478 us | 0.68x | 0.68–0.68x | -0.152 us | 9 pairs; 38/39 ms; 117,252/80,650 ops/lane; IQR 1.001/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 142.075 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.301 us | 0.587 us | 0.51x | 0.51–0.51x | -0.285 us | 9 pairs; 35/39 ms; 117,381/65,625 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 103.475 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.300 us | 0.398 us | 0.75x | 0.75–0.76x | -0.098 us | 9 pairs; 32/40 ms; 106,151/99,863 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 213.137 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.858 us | 8.851 us | 0.55x | 0.55–0.55x | -3.992 us | 9 pairs; 40/40 ms; 8,208/4,540 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.512 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.256 us | 0.310 us | 0.83x | 0.82–0.83x | -0.053 us | 9 pairs; 36/37 ms; 139,523/118,018 ops/lane; IQR 1.008/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 93.787 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.271 us | 0.399 us | 0.68x | 0.68–0.68x | -0.127 us | 9 pairs; 38/40 ms; 138,653/99,976 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 121.994 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.729 us | — | — | — | — | — | `—` | 0 B | — | 14.481 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.187 us | 0.295 us | 0.64x | 0.63–0.66x | -0.107 us | 9 pairs; 40/39 ms; 212,944/132,520 ops/lane; IQR 1.011/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.656 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.635 us | — | — | — | — | — | `—` | 0 B | — | 14.019 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.590 us | 0.675 us | 0.87x | 0.87–0.88x | -0.085 us | 9 pairs; 36/39 ms; 60,634/58,254 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | 0 B | 0 B | 63.106 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.800 us | 28.933 us | 0.34x | 0.34–0.34x | -19.120 us | 9 pairs; 39/40 ms; 4,030/1,380 ops/lane; IQR 1.002/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.781 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.964 us | 1.075 us | 0.90x | 0.89–0.90x | -0.109 us | 9 pairs; 41/40 ms; 42,431/37,296 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 133.650 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.999 us | 1.191 us | 0.84x | 0.83–0.84x | -0.191 us | 9 pairs; 40/40 ms; 39,788/33,285 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 116.300 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.943 us | 1.036 us | 0.91x | 0.91–0.91x | -0.091 us | 9 pairs; 39/40 ms; 40,848/38,200 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 140.281 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.942 us | 1.178 us | 0.80x | 0.80–0.80x | -0.236 us | 9 pairs; 40/40 ms; 41,961/33,636 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 102.194 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.956 us | 0.952 us | 1.00x | 1.00–1.01x | +0.004 us | 9 pairs; 40/40 ms; 41,874/42,160 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 216.781 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.472 us | 8.877 us | 0.62x | 0.61–0.62x | -3.407 us | 9 pairs; 40/40 ms; 7,264/4,496 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 0 B | 0 B | 1.456 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.956 us | 0.896 us | 1.07x | 1.07–1.07x | +0.061 us | 9 pairs; 42/41 ms; 43,462/45,221 ops/lane; IQR 1.001/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 92.100 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.927 us | 0.966 us | 0.96x | 0.95–0.96x | -0.039 us | 9 pairs; 40/40 ms; 43,000/41,807 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 125.406 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.051 us | — | — | — | — | — | `—` | 0 B | — | 14.681 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.828 us | 0.863 us | 0.96x | 0.96–0.96x | -0.034 us | 9 pairs; 40/40 ms; 48,625/46,452 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 52.931 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.937 us | — | — | — | — | — | `—` | 0 B | — | 13.831 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.915 us | 1.261 us | 0.72x | 0.72–0.73x | -0.347 us | 9 pairs; 40/40 ms; 43,490/31,556 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | 0 B | 0 B | 64.775 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.386 us | 2.289 us | 0.60x | 0.60–0.61x | -0.905 us | 9 pairs; 40/40 ms; 28,736/17,428 ops/lane; IQR 1.002/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.671 us | 1.075 us | 0.62x | 0.62–0.62x | -0.405 us | 9 pairs; 36/41 ms; 54,106/37,704 ops/lane; IQR 1.001/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 133.238 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.693 us | 1.197 us | 0.58x | 0.58–0.58x | -0.504 us | 9 pairs; 40/40 ms; 58,345/33,022 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 115.694 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.652 us | 1.031 us | 0.63x | 0.63–0.63x | -0.381 us | 9 pairs; 42/40 ms; 63,736/38,680 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 140.331 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.673 us | 1.191 us | 0.56x | 0.56–0.57x | -0.519 us | 9 pairs; 40/39 ms; 59,584/33,138 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 105.338 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.662 us | 0.951 us | 0.70x | 0.69–0.70x | -0.289 us | 9 pairs; 40/41 ms; 59,758/42,850 ops/lane; IQR 1.001/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 219.162 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.835 us | 2.194 us | 0.84x | 0.83–0.84x | -0.360 us | 9 pairs; 40/40 ms; 21,551/18,221 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.608 us | 0.894 us | 0.68x | 0.68–0.68x | -0.287 us | 9 pairs; 40/40 ms; 65,894/44,606 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 96.256 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.608 us | 0.970 us | 0.63x | 0.63–0.63x | -0.362 us | 9 pairs; 38/40 ms; 62,784/41,158 ops/lane; IQR 1.000/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 125.737 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.645 us | — | — | — | — | — | `—` | 0 B | — | 13.356 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.540 us | 0.862 us | 0.63x | 0.62–0.63x | -0.322 us | 9 pairs; 41/40 ms; 76,672/46,380 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 55.269 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.544 us | — | — | — | — | — | `—` | 0 B | — | 16.019 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.537 us | 1.254 us | 0.43x | 0.43–0.43x | -0.717 us | 9 pairs; 40/39 ms; 74,915/31,305 ops/lane; IQR 1.001/1.003 | `Pcre2Slice` | 0 B | 0 B | 65.156 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.769 us | — | — | — | — | — | `—` | 480 B | — | 0.731 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.111 us | — | — | — | — | — | `—` | 48 B | — | 133.081 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.224 us | — | — | — | — | — | `—` | 48 B | — | 117.162 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.101 us | — | — | — | — | — | `—` | 48 B | — | 141.031 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.114 us | — | — | — | — | — | `—` | 48 B | — | 101.744 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.095 us | — | — | — | — | — | `—` | 48 B | — | 217.269 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.900 us | — | — | — | — | — | `—` | 64 B | — | 1.406 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.110 us | — | — | — | — | — | `—` | 40 B | — | 93.287 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.105 us | — | — | — | — | — | `—` | 40 B | — | 123.537 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.018 us | — | — | — | — | — | `—` | 1,216 B | — | 13.381 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.024 us | — | — | — | — | — | `—` | 48 B | — | 53.006 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.758 us | — | — | — | — | — | `—` | 928 B | — | 13.869 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 6.049 us | — | — | — | — | — | `—` | 28,928 B | — | 63.513 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 29.592–227.495 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.499–95.284 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.657–12.747 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 14.447–106.111 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.036–1.443 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.827–45.441 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.777–22.847 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.202–101.039 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.144–72.831 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.203–5.504 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 50.637–395.707 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.569–307.347 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.865–5.150 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.254–137.299 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 168.892–1,358.764 us |

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
