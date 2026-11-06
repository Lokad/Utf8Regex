<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `B5C65F87E76A0E6FEE03029E74F549756D902ACC10AB868772CF46B1388D996F`
- Latest managed row measurement: `2026-11-06T05:56:08.0161883+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-06T05:56:08.0161883+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `74` managed faster, `1` equivalent, `13` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `6f6fdab9e8b2` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `6f6fdab9e8b2` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.648 us | 2.353 us | 0.28x | 0.23–0.30x | -1.705 us | 9 pairs; 35/36 ms; 54,089/15,409 ops/lane; IQR 1.012/1.193 | `Pcre2Slice` | 471.506 us | 0.282 us | 0.900 us | 0.72x | 992 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.855 us | 11.957 us | 0.07x | 0.07–0.08x | -11.097 us | 9 pairs; 37/41 ms; 43,373/3,403 ops/lane; IQR 1.006/1.119 | `Pcre2Slice` | 0.713 us | 0.098 us | 3.703 us | 0.23x | 464 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.232 us | 0.01x | 0.01–0.01x | -1.221 us | 9 pairs; 40/40 ms; 3,713,438/32,094 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | 0.105 us | 1.109 us | 1.143 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.710 us | 0.159 us | 4.45x | 4.43–4.49x | +0.551 us | 9 pairs; 38/39 ms; 53,730/247,418 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0.047 us | 0.126 us | 0.163 us | 4.36x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.360 us | 0.572 us | 4.12x | 4.10–4.18x | +1.787 us | 9 pairs; 40/39 ms; 16,828/67,795 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0.129 us | 0.768 us | 0.816 us | 2.89x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.298 us | 0.34x | 0.33–0.34x | -0.198 us | 9 pairs; 40/40 ms; 393,380/134,748 ops/lane; IQR 1.003/1.013 | `Pcre2Slice` | 0.115 us | 0.325 us | 0.354 us | 0.28x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.105 us | 0.345 us | 0.31x | 0.30–0.31x | -0.239 us | 9 pairs; 37/39 ms; 353,470/114,254 ops/lane; IQR 1.006/1.010 | `Pcre2Slice` | 0.118 us | 0.451 us | 0.476 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.078 us | 0.291 us | 0.27x | 0.27–0.27x | -0.213 us | 9 pairs; 39/40 ms; 501,957/135,912 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 0.090 us | 0.254 us | 0.273 us | 0.28x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.081 us | 0.312 us | 0.26x | 0.26–0.28x | -0.231 us | 9 pairs; 39/38 ms; 482,881/121,387 ops/lane; IQR 1.025/1.016 | `Pcre2Slice` | 0.090 us | 0.265 us | 0.289 us | 0.28x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.078 us | 2.25x | 2.22–2.33x | +0.096 us | 9 pairs; 38/44 ms; 217,754/567,748 ops/lane; IQR 1.047/1.009 | `Pcre2Slice` | 0.089 us | 0.963 us | 1.018 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.202 us | 0.16x | 0.16–0.17x | -0.169 us | 9 pairs; 40/41 ms; 1,188,292/203,652 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0.112 us | 0.312 us | 0.352 us | 0.09x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.610 us | 0.10x | 0.10–0.10x | -0.549 us | 9 pairs; 40/40 ms; 657,158/66,238 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0.136 us | 0.246 us | 0.273 us | 0.22x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.155 us | 0.22x | 0.21–0.22x | -0.121 us | 9 pairs; 38/40 ms; 1,141,061/256,371 ops/lane; IQR 1.023/1.002 | `Pcre2Backtracking` | 0.107 us | 0.184 us | 0.201 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.112 us | 0.19x | 0.18–0.19x | -0.092 us | 9 pairs; 41/41 ms; 1,953,820/363,339 ops/lane; IQR 1.023/1.005 | `Pcre2Backtracking` | 0.060 us | 0.094 us | 0.114 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.124 us | 0.46x | 0.46–0.47x | -0.067 us | 9 pairs; 40/40 ms; 692,340/321,525 ops/lane; IQR 1.003/1.012 | `Pcre2Backtracking` | 0.014 us | 0.088 us | 0.105 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.085 us | 0.271 us | 0.31x | 0.31–0.32x | -0.186 us | 9 pairs; 31/38 ms; 366,115/139,313 ops/lane; IQR 1.002/1.014 | `Pcre2Slice` | 0.096 us | 0.121 us | 0.158 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.115 us | 0.22x | 0.21–0.22x | -0.090 us | 9 pairs; 42/39 ms; 1,669,529/343,583 ops/lane; IQR 1.018/1.015 | `Pcre2Backtracking` | 0.019 us | 0.104 us | 0.123 us | 0.20x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.119 us | 2.087 us | 0.06x | 0.05–0.06x | -1.968 us | 9 pairs; 41/46 ms; 341,598/21,818 ops/lane; IQR 1.005/1.180 | `Pcre2Literal` | 0.089 us | 0.052 us | 0.617 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.122 us | 2.037 us | 0.06x | 0.05–0.07x | -1.915 us | 9 pairs; 40/50 ms; 330,915/24,555 ops/lane; IQR 1.003/1.287 | `Pcre2Literal` | 0.149 us | 0.054 us | 0.641 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.156 us | 1.934 us | 0.08x | 0.07–0.08x | -1.777 us | 9 pairs; 39/37 ms; 252,002/19,386 ops/lane; IQR 1.002/1.098 | `Pcre2Literal` | 0.204 us | 0.233 us | 0.834 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.153 us | 1.980 us | 0.08x | 0.07–0.08x | -1.827 us | 9 pairs; 40/42 ms; 262,775/20,983 ops/lane; IQR 1.006/1.083 | `Pcre2Literal` | 0.175 us | 0.205 us | 0.804 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Inconclusive** | 16 B | 0.015 us | 0.099 us | 0.15x | 0.09–0.15x | -0.085 us | 9 pairs; 65/41 ms; 4,400,974/411,464 ops/lane; IQR 1.332/1.010 | `Pcre2Slice` | 0.250 us | 0.071 us | 0.090 us | 0.16x | 0 B | 0 B |
| `simple/foo-dense` | **Inconclusive** | 18 B | 0.069 us | 0.088 us | 0.78x | 0.78–0.85x | -0.019 us | 9 pairs; 41/40 ms; 593,859/450,023 ops/lane; IQR 1.114/1.014 | `Pcre2Literal` | 0.132 us | 0.051 us | 0.073 us | 0.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.107 us | 0.13x | 0.12–0.13x | -0.093 us | 9 pairs; 41/39 ms; 2,891,932/360,542 ops/lane; IQR 1.012/1.025 | `Pcre2Backtracking` | 0.281 us | 0.096 us | 0.122 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.114 us | 0.123 us | 0.92x | 0.91–0.93x | -0.010 us | 9 pairs; 39/39 ms; 341,917/312,754 ops/lane; IQR 1.001/1.013 | `Pcre2Character` | 0.144 us | 0.117 us | 0.146 us | 0.78x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.082 us | 0.203 us | 0.40x | 0.37–0.41x | -0.123 us | 9 pairs; 41/41 ms; 494,342/201,498 ops/lane; IQR 1.090/1.010 | `Pcre2Slice` | 0.445 us | 0.075 us | 0.108 us | 0.76x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.152 us | 160.957 us | 0.51x | 0.51–0.51x | -78.661 us | 9 pairs; 40/40 ms; 486/247 ops/lane; IQR 1.004/1.005 | `Pcre2Slice` | 326.988 us | 108.308 us | 110.395 us | 0.74x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.943 us | 280.882 us | 0.14x | 0.14–0.14x | -241.042 us | 9 pairs; 40/40 ms; 1,006/141 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 265.927 us | 218.443 us | 228.985 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.590 us | 210.868 us | 0.05x | 0.05–0.05x | -201.272 us | 9 pairs; 41/40 ms; 4,243/191 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 8.093 us | 222.986 us | 223.333 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.118 us | 217.280 us | 1.11x | 1.10–1.11x | +23.821 us | 9 pairs; 40/41 ms; 167/188 ops/lane; IQR 1.004/1.007 | `Pcre2SingleTokenRepeat` | 8.075 us | 236.298 us | 233.307 us | 1.03x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.548 us | 22.817 us | 0.16x | 0.15–0.16x | -19.270 us | 9 pairs; 39/41 ms; 11,111/1,782 ops/lane; IQR 1.004/1.016 | `Pcre2Literal` | 1.387 us | 4.276 us | 6.157 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.630 us | 81.133 us | 0.12x | 0.12–0.12x | -71.518 us | 9 pairs; 40/40 ms; 4,161/493 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 9.320 us | 93.023 us | 96.066 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,187.788 us | 28,906.300 us | 0.14x | 0.14–0.16x | -24716.375 us | 9 pairs; 34/29 ms; 8/1 ops/lane; IQR 1.001/1.011 | `Pcre2Backtracking` | 5,317.680 us | 11,693.040 us | 18,493.833 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,342.867 us | 55,493.400 us | 0.06x | 0.06–0.06x | -52168.392 us | 9 pairs; 40/55 ms; 12/1 ops/lane; IQR 1.019/1.006 | `Pcre2Backtracking` | 3,877.415 us | 41,606.150 us | 48,018.500 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,916.105 us | 483,656.700 us | 0.00x | 0.00–0.00x | -481716.890 us | 9 pairs; 40/484 ms; 21/1 ops/lane; IQR 1.028/1.003 | `Pcre2Character` | 1,776.076 us | 23,135.567 us | 30,000.600 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,584.950 us | 11,429.800 us | 0.49x | 0.45–0.50x | -5855.033 us | 9 pairs; 34/46 ms; 6/4 ops/lane; IQR 1.002/1.073 | `Pcre2Literal` | 1,266.513 us | 1,354.880 us | 8,401.029 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,770.650 us | 145,676.800 us | 0.13x | 0.13–0.13x | -126985.050 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 1,235.512 us | 511.104 us | 8,422.033 us | 2.23x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,908.762 us | 21,881.200 us | 0.22x | 0.22–0.23x | -16942.475 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.002/1.009 | `Pcre2Slice` | 2,446.033 us | 11,142.580 us | 19,220.133 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,453.100 us | 134,158.000 us | 0.17x | 0.17–0.17x | -111704.900 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.008/1.002 | `Pcre2Backtracking` | 1,516.412 us | 2,393.568 us | 10,439.880 us | 2.15x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 159.918 us | 983.258 us | 0.16x | 0.16–0.16x | -823.340 us | 9 pairs; 39/37 ms; 247/38 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 166.940 us | 334.863 us | 542.764 us | 0.29x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,494.700 us | 13,463.200 us | 0.85x | 0.85–0.86x | -1968.733 us | 9 pairs; 34/40 ms; 3/3 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 311.085 us | 15,565.925 us | 15,724.175 us | 0.73x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,777.814 us | 98,808.100 us | 0.03x | 0.03–0.03x | -96022.321 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.004/1.001 | `Pcre2Character` | 1,924.900 us | 19,921.300 us | 20,063.367 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.399 us | 3,128.367 us | 0.06x | 0.06–0.06x | -2942.927 us | 9 pairs; 39/38 ms; 211/12 ops/lane; IQR 1.004/1.011 | `Pcre2SingleTokenRepeat` | 191.805 us | 1,020.010 us | 1,435.962 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,583.804 us | 20,083.700 us | 0.08x | 0.08–0.08x | -18497.268 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 567.043 us | 10,969.860 us | 11,073.780 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.150 us | 1.094 us | 0.14x | 0.14–0.14x | -0.945 us | 9 pairs; 41/40 ms; 273,675/36,147 ops/lane; IQR 1.014/1.005 | `Pcre2Slice` | 0.664 us | 0.263 us | 0.282 us | 0.53x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.506 us | 0.829 us | 0.61x | 0.61–0.61x | -0.324 us | 9 pairs; 40/40 ms; 79,380/48,156 ops/lane; IQR 1.007/1.003 | `Pcre2Literal` | 0.149 us | 0.148 us | 0.172 us | 2.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.334 us | 1.176 us | 0.28x | 0.28–0.28x | -0.844 us | 9 pairs; 46/40 ms; 138,252/33,992 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 1.072 us | 0.355 us | 0.388 us | 0.86x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.881 us | 1.130 us | 0.78x | 0.77–0.78x | -0.252 us | 9 pairs; 39/40 ms; 44,286/35,362 ops/lane; IQR 1.007/1.005 | `Pcre2Character` | 0.281 us | 0.458 us | 0.509 us | 1.73x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.166 us | 0.859 us | 0.19x | 0.19–0.19x | -0.693 us | 9 pairs; 43/40 ms; 261,020/46,152 ops/lane; IQR 1.016/1.004 | `Pcre2Slice` | 0.801 us | 0.157 us | 0.203 us | 0.81x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.881 us | 1.104 us | 0.80x | 0.79–0.80x | -0.224 us | 9 pairs; 40/40 ms; 44,939/36,055 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 2.848 us | 0.306 us | 0.329 us | 2.68x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.791 us | 0.841 us | 0.94x | 0.94–0.94x | -0.051 us | 9 pairs; 40/40 ms; 50,787/47,407 ops/lane; IQR 1.003/1.001 | `Pcre2Literal` | 3.548 us | 0.181 us | 0.209 us | 3.78x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.002 us | 1.187 us | 0.85x | 0.84–0.85x | -0.180 us | 9 pairs; 38/40 ms; 37,506/33,610 ops/lane; IQR 1.012/1.005 | `Pcre2Backtracking` | 5.338 us | 0.416 us | 0.443 us | 2.26x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.263 us | 1.142 us | 1.10x | 1.10–1.11x | +0.120 us | 9 pairs; 43/40 ms; 34,306/34,880 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 3.619 us | 0.498 us | 0.545 us | 2.32x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.794 us | 0.859 us | 0.93x | 0.92–0.93x | -0.064 us | 9 pairs; 40/40 ms; 49,982/46,119 ops/lane; IQR 1.004/1.004 | `Pcre2Slice` | 2.115 us | 0.183 us | 0.226 us | 3.52x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.623 us | 1.106 us | 0.56x | 0.56–0.56x | -0.482 us | 9 pairs; 40/40 ms; 64,360/35,961 ops/lane; IQR 1.003/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.510 us | 0.841 us | 0.61x | 0.61–0.61x | -0.331 us | 9 pairs; 41/40 ms; 80,191/47,860 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.673 us | 1.183 us | 0.57x | 0.57–0.57x | -0.510 us | 9 pairs; 40/40 ms; 59,982/33,485 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.912 us | 1.160 us | 0.79x | 0.78–0.79x | -0.248 us | 9 pairs; 28/41 ms; 30,666/34,977 ops/lane; IQR 1.003/1.005 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.550 us | 0.861 us | 0.64x | 0.64–0.64x | -0.311 us | 9 pairs; 40/41 ms; 73,441/47,921 ops/lane; IQR 1.003/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.362 us | — | — | — | — | — | `—` | 27.990 us | 101.097 us | 100.869 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.741 us | — | — | — | — | — | `—` | 1.611 us | 0.358 us | 0.384 us | 12.35x | 4,752 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.090 us | — | — | — | — | — | `—` | 0.905 us | 0.243 us | 0.267 us | 4.08x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.251 us | — | — | — | — | — | `—` | 3.132 us | 0.469 us | 0.499 us | 2.51x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.532 us | — | — | — | — | — | `—` | 1.023 us | 0.561 us | 0.617 us | 2.48x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.695 us | — | — | — | — | — | `—` | 1.511 us | 0.265 us | 0.315 us | 11.74x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.094 us | 0.084 us | 1.11x | 1.04–1.12x | +0.009 us | 9 pairs; 41/40 ms; 437,545/471,203 ops/lane; IQR 1.028/1.006 | `Pcre2Character` | 0 B | 0 B | 2.331 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.120 us | 0.11x | 0.10–0.11x | -0.107 us | 9 pairs; 40/40 ms; 3,139,574/337,707 ops/lane; IQR 1.004/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 128.562 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.375 us | 0.105 us | 3.58x | 3.56–3.67x | +0.270 us | 9 pairs; 35/41 ms; 94,269/390,927 ops/lane; IQR 1.022/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 20.331 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.389 us | 0.116 us | 3.36x | 3.33–3.39x | +0.273 us | 9 pairs; 36/39 ms; 93,201/335,207 ops/lane; IQR 1.003/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 70.463 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.378 us | 0.103 us | 3.65x | 3.55–3.67x | +0.273 us | 9 pairs; 35/39 ms; 93,580/379,060 ops/lane; IQR 1.007/1.030 | `Pcre2Backtracking` | 0 B | 0 B | 70.019 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.674 us | 0.348 us | 1.94x | 1.93–1.95x | +0.327 us | 9 pairs; 39/39 ms; 57,111/112,677 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 0 B | 0 B | 1.488 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.632 us | 0.02x | 0.02–0.02x | -0.620 us | 9 pairs; 42/41 ms; 3,509,917/64,085 ops/lane; IQR 1.020/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 21.994 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.012 us | 0.507 us | 0.02x | 0.02–0.02x | -0.495 us | 9 pairs; 38/38 ms; 3,217,149/75,495 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 22.619 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.462 us | 0.109 us | 4.25x | 4.25–4.29x | +0.353 us | 9 pairs; 36/40 ms; 78,986/369,996 ops/lane; IQR 1.013/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 19.494 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.542 us | 10.508 us | 0.81x | 0.81–0.82x | -1.982 us | 9 pairs; 40/40 ms; 4,735/3,766 ops/lane; IQR 1.007/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.311 us | 1.052 us | 0.30x | 0.29–0.30x | -0.742 us | 9 pairs; 41/41 ms; 131,168/38,529 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 131.375 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.334 us | 0.467 us | 0.71x | 0.71–0.72x | -0.134 us | 9 pairs; 39/41 ms; 116,967/86,932 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 114.956 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.303 us | 0.458 us | 0.66x | 0.66–0.67x | -0.155 us | 9 pairs; 34/40 ms; 112,520/87,096 ops/lane; IQR 1.004/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 141.025 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.294 us | 0.592 us | 0.50x | 0.50–0.50x | -0.298 us | 9 pairs; 33/40 ms; 111,997/68,210 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 103.213 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.300 us | 0.401 us | 0.75x | 0.74–0.76x | -0.102 us | 9 pairs; 34/39 ms; 114,371/98,315 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 213.756 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.899 us | 8.810 us | 0.56x | 0.56–0.56x | -3.913 us | 9 pairs; 40/40 ms; 8,137/4,517 ops/lane; IQR 1.002/1.001 | `Pcre2Character` | 0 B | 0 B | 1.462 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.255 us | 0.313 us | 0.82x | 0.81–0.82x | -0.058 us | 9 pairs; 36/38 ms; 139,700/122,381 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 94.606 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.272 us | 0.400 us | 0.68x | 0.68–0.68x | -0.128 us | 9 pairs; 37/40 ms; 135,796/100,476 ops/lane; IQR 1.007/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 121.650 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.891 us | — | — | — | — | — | `—` | 0 B | — | 14.631 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.190 us | 0.287 us | 0.66x | 0.66–0.66x | -0.097 us | 9 pairs; 40/40 ms; 212,539/138,706 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.062 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.790 us | — | — | — | — | — | `—` | 0 B | — | 14.838 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.587 us | 0.670 us | 0.88x | 0.86–0.88x | -0.082 us | 9 pairs; 33/40 ms; 55,469/59,443 ops/lane; IQR 1.005/1.019 | `Pcre2Slice` | 0 B | 0 B | 63.906 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 10.026 us | 28.953 us | 0.35x | 0.35–0.35x | -18.926 us | 9 pairs; 40/40 ms; 3,950/1,387 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.819 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.956 us | 1.076 us | 0.89x | 0.89–0.89x | -0.120 us | 9 pairs; 39/41 ms; 40,914/37,774 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 130.088 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.051 us | 1.197 us | 0.88x | 0.88–0.88x | -0.146 us | 9 pairs; 42/41 ms; 39,775/34,236 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.000 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.962 us | 1.028 us | 0.93x | 0.93–0.94x | -0.070 us | 9 pairs; 40/40 ms; 41,666/38,514 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 139.875 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 1.042 us | 1.171 us | 0.89x | 0.89–0.90x | -0.128 us | 9 pairs; 41/40 ms; 39,292/34,142 ops/lane; IQR 1.005/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 103.919 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.986 us | 0.957 us | 1.03x | 1.02–1.03x | +0.032 us | 9 pairs; 39/40 ms; 40,055/42,192 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 214.175 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.577 us | 8.877 us | 0.63x | 0.63–0.63x | -3.302 us | 9 pairs; 40/40 ms; 7,180/4,516 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 0 B | 0 B | 1.500 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.936 us | 0.889 us | 1.05x | 1.05–1.06x | +0.047 us | 9 pairs; 41/40 ms; 43,533/44,666 ops/lane; IQR 1.008/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 93.831 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Equivalent** | 26 B | 0.966 us | 0.973 us | 0.99x | 0.99–1.00x | -0.005 us | 9 pairs; 40/40 ms; 40,978/41,097 ops/lane; IQR 1.007/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 123.294 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.078 us | — | — | — | — | — | `—` | 0 B | — | 13.762 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.835 us | 0.859 us | 0.97x | 0.97–0.98x | -0.023 us | 9 pairs; 40/40 ms; 48,245/46,277 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 54.538 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.948 us | — | — | — | — | — | `—` | 0 B | — | 14.787 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.915 us | 1.256 us | 0.73x | 0.73–0.73x | -0.340 us | 9 pairs; 40/41 ms; 43,864/32,810 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0 B | 0 B | 64.400 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.477 us | 2.286 us | 0.65x | 0.64–0.65x | -0.809 us | 9 pairs; 40/40 ms; 27,002/17,380 ops/lane; IQR 1.002/1.004 | `Pcre2Literal` | 0 B | 0 B | 0.744 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.638 us | 1.072 us | 0.60x | 0.59–0.60x | -0.434 us | 9 pairs; 39/40 ms; 61,611/36,981 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 129.250 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.675 us | 1.186 us | 0.57x | 0.57–0.57x | -0.510 us | 9 pairs; 40/39 ms; 58,520/32,690 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 114.487 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.612 us | 1.035 us | 0.59x | 0.59–0.59x | -0.423 us | 9 pairs; 39/41 ms; 64,322/39,309 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 142.475 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.630 us | 1.169 us | 0.54x | 0.54–0.54x | -0.538 us | 9 pairs; 40/40 ms; 63,308/34,397 ops/lane; IQR 1.001/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 103.394 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.623 us | 0.959 us | 0.65x | 0.65–0.65x | -0.336 us | 9 pairs; 41/40 ms; 65,171/42,156 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 211.431 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.893 us | 2.195 us | 0.86x | 0.86–0.86x | -0.302 us | 9 pairs; 40/40 ms; 21,025/18,250 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.605 us | 0.887 us | 0.68x | 0.68–0.69x | -0.281 us | 9 pairs; 40/39 ms; 66,300/44,300 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 92.500 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.639 us | 0.970 us | 0.66x | 0.66–0.66x | -0.331 us | 9 pairs; 38/40 ms; 59,463/41,588 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 123.106 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.884 us | — | — | — | — | — | `—` | 0 B | — | 13.800 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.494 us | 0.859 us | 0.58x | 0.58–0.58x | -0.365 us | 9 pairs; 39/40 ms; 78,038/46,333 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.263 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.777 us | — | — | — | — | — | `—` | 0 B | — | 13.625 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.601 us | 1.259 us | 0.48x | 0.48–0.48x | -0.658 us | 9 pairs; 41/39 ms; 68,375/31,359 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.225 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.746 us | — | — | — | — | — | `—` | 480 B | — | 0.769 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.168 us | — | — | — | — | — | `—` | 48 B | — | 132.131 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.268 us | — | — | — | — | — | `—` | 48 B | — | 117.700 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.152 us | — | — | — | — | — | `—` | 48 B | — | 141.425 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.169 us | — | — | — | — | — | `—` | 48 B | — | 100.725 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.145 us | — | — | — | — | — | `—` | 48 B | — | 216.412 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.107 us | — | — | — | — | — | `—` | 64 B | — | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.172 us | — | — | — | — | — | `—` | 40 B | — | 93.944 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.177 us | — | — | — | — | — | `—` | 40 B | — | 123.125 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.171 us | — | — | — | — | — | `—` | 1,216 B | — | 13.488 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.100 us | — | — | — | — | — | `—` | 48 B | — | 51.862 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.753 us | — | — | — | — | — | `—` | 928 B | — | 14.781 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.638 us | — | — | — | — | — | `—` | 5,392 B | — | 63.769 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 32.761–248.453 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.454–95.521 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.931–12.726 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.036–47.416 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.035–1.172 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.767–45.719 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.771–22.685 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.233–100.067 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.160–72.781 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.219–5.478 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.117–377.026 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.077 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.098–304.638 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.849–5.099 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.180–15.865 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 110.825–863.972 us |

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
