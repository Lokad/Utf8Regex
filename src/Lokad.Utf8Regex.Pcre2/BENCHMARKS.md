<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `E505A2E75F9045F3EEE9AAFE78C2BBB37753CECC7A5DDC1C7BC383C4E5F75C44`
- Latest managed row measurement: `2026-11-04T05:07:14.4729504+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T05:07:14.4729504+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `42/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `74` managed faster, `1` equivalent, `13` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `60491532fe8a` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `60491532fe8a` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.894 us | 2.195 us | 0.41x | 0.36–0.45x | -1.304 us | 9 pairs; 30/42 ms; 33,794/18,944 ops/lane; IQR 1.021/1.131 | `Pcre2Slice` | 549.506 us | 0.283 us | 0.802 us | 1.11x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.127 us | 13.361 us | 0.08x | 0.08–0.09x | -12.230 us | 9 pairs; 35/46 ms; 31,368/3,473 ops/lane; IQR 1.010/1.183 | `Pcre2Slice` | 0.619 us | 0.096 us | 3.347 us | 0.34x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.233 us | 0.01x | 0.01–0.01x | -1.222 us | 9 pairs; 40/40 ms; 3,653,745/32,230 ops/lane; IQR 1.004/1.008 | `Pcre2Slice` | 0.104 us | 1.087 us | 1.130 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.687 us | 0.159 us | 4.30x | 4.29–4.34x | +0.528 us | 9 pairs; 36/39 ms; 53,112/245,541 ops/lane; IQR 1.011/1.014 | `Pcre2Backtracking` | 0.047 us | 0.122 us | 0.159 us | 4.32x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.366 us | 0.569 us | 4.15x | 4.11–4.17x | +1.796 us | 9 pairs; 38/40 ms; 15,863/69,577 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | 0.115 us | 0.741 us | 0.774 us | 3.06x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.104 us | 0.298 us | 0.35x | 0.34–0.37x | -0.195 us | 9 pairs; 41/39 ms; 391,738/132,104 ops/lane; IQR 1.073/1.010 | `Pcre2Slice` | 0.109 us | 0.319 us | 0.346 us | 0.30x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.107 us | 0.380 us | 0.28x | 0.28–0.29x | -0.272 us | 9 pairs; 41/40 ms; 379,850/106,268 ops/lane; IQR 1.043/1.003 | `Pcre2Slice` | 0.113 us | 0.442 us | 0.465 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.290 us | 0.28x | 0.27–0.28x | -0.210 us | 9 pairs; 40/40 ms; 497,965/136,742 ops/lane; IQR 1.006/1.022 | `Pcre2Slice` | 0.087 us | 0.241 us | 0.260 us | 0.31x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.076 us | 0.315 us | 0.24x | 0.24–0.25x | -0.238 us | 9 pairs; 38/39 ms; 496,460/125,043 ops/lane; IQR 1.010/1.006 | `Pcre2Slice` | 0.087 us | 0.256 us | 0.278 us | 0.27x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.173 us | 0.076 us | 2.29x | 2.18–2.35x | +0.099 us | 9 pairs; 40/42 ms; 231,406/561,154 ops/lane; IQR 1.005/1.034 | `Pcre2Slice` | 0.086 us | 0.974 us | 1.005 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.044 us | 0.203 us | 0.22x | 0.22–0.24x | -0.159 us | 9 pairs; 53/40 ms; 1,188,415/196,477 ops/lane; IQR 1.016/1.002 | `Pcre2Backtracking` | 0.108 us | 0.307 us | 0.348 us | 0.13x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.617 us | 0.10x | 0.09–0.10x | -0.556 us | 9 pairs; 40/40 ms; 655,484/65,573 ops/lane; IQR 1.002/1.040 | `Pcre2Backtracking` | 0.130 us | 0.228 us | 0.254 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.156 us | 0.21x | 0.21–0.22x | -0.122 us | 9 pairs; 38/40 ms; 1,138,342/256,408 ops/lane; IQR 1.025/1.011 | `Pcre2Backtracking` | 0.105 us | 0.182 us | 0.200 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.114 us | 0.17x | 0.17–0.18x | -0.094 us | 9 pairs; 38/39 ms; 1,936,428/344,057 ops/lane; IQR 1.017/1.005 | `Pcre2Backtracking` | 0.059 us | 0.092 us | 0.111 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.122 us | 0.47x | 0.47–0.48x | -0.064 us | 9 pairs; 40/43 ms; 692,711/355,852 ops/lane; IQR 1.007/1.014 | `Pcre2Backtracking` | 0.016 us | 0.087 us | 0.104 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.086 us | 0.291 us | 0.30x | 0.29–0.31x | -0.203 us | 9 pairs; 41/40 ms; 473,683/138,804 ops/lane; IQR 1.011/1.010 | `Pcre2Slice` | 0.094 us | 0.117 us | 0.155 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.116 us | 0.22x | 0.19–0.22x | -0.091 us | 9 pairs; 44/39 ms; 1,749,493/332,063 ops/lane; IQR 1.056/1.022 | `Pcre2Backtracking` | 0.019 us | 0.094 us | 0.112 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.119 us | 1.871 us | 0.06x | 0.05–0.06x | -1.752 us | 9 pairs; 41/41 ms; 343,019/22,106 ops/lane; IQR 1.008/1.162 | `Pcre2Literal` | 0.088 us | 0.051 us | 0.649 us | 0.18x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.127 us | 1.903 us | 0.07x | 0.06–0.07x | -1.775 us | 9 pairs; 42/42 ms; 330,262/21,897 ops/lane; IQR 1.045/1.054 | `Pcre2Literal` | 0.142 us | 0.054 us | 0.587 us | 0.22x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.154 us | 2.412 us | 0.07x | 0.06–0.08x | -2.255 us | 9 pairs; 39/58 ms; 255,830/23,936 ops/lane; IQR 1.018/1.189 | `Pcre2Literal` | 0.205 us | 0.237 us | 0.824 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.151 us | 1.839 us | 0.08x | 0.08–0.09x | -1.688 us | 9 pairs; 40/31 ms; 266,408/16,828 ops/lane; IQR 1.001/1.015 | `Pcre2Literal` | 0.177 us | 0.206 us | 0.812 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.101 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 39/40 ms; 4,428,108/402,358 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 0.498 us | 0.071 us | 0.090 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.085 us | 0.81x | 0.80–0.81x | -0.016 us | 9 pairs; 40/40 ms; 590,502/475,288 ops/lane; IQR 1.005/1.016 | `Pcre2Literal` | 0.120 us | 0.051 us | 0.073 us | 0.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.113 us | 0.13x | 0.12–0.13x | -0.099 us | 9 pairs; 41/39 ms; 2,872,601/349,350 ops/lane; IQR 1.018/1.015 | `Pcre2Backtracking` | 0.246 us | 0.091 us | 0.113 us | 0.13x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.117 us | 0.123 us | 0.95x | 0.94–1.00x | -0.006 us | 9 pairs; 41/41 ms; 349,534/329,081 ops/lane; IQR 1.071/1.020 | `Pcre2Character` | 0.131 us | 0.112 us | 0.143 us | 0.82x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.074 us | 0.202 us | 0.37x | 0.36–0.37x | -0.128 us | 9 pairs; 40/40 ms; 536,449/197,864 ops/lane; IQR 1.005/1.014 | `Pcre2Slice` | 0.460 us | 0.074 us | 0.111 us | 0.67x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.255 us | 159.871 us | 0.51x | 0.51–0.52x | -77.942 us | 9 pairs; 39/40 ms; 479/251 ops/lane; IQR 1.004/1.005 | `Pcre2Slice` | 424.965 us | 107.772 us | 107.304 us | 0.77x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.757 us | 282.091 us | 0.13x | 0.13–0.13x | -244.328 us | 9 pairs; 40/39 ms; 1,048/139 ops/lane; IQR 1.001/1.004 | `Pcre2Character` | 389.976 us | 219.025 us | 229.152 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.851 us | 210.092 us | 0.05x | 0.05–0.05x | -200.133 us | 9 pairs; 40/33 ms; 4,079/158 ops/lane; IQR 1.012/1.011 | `Pcre2Backtracking` | 7.977 us | 218.977 us | 221.035 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 239.596 us | 206.494 us | 1.16x | 1.15–1.17x | +32.925 us | 9 pairs; 40/41 ms; 169/198 ops/lane; IQR 1.001/1.014 | `Pcre2SingleTokenRepeat` | 7.959 us | 233.285 us | 235.881 us | 1.02x | 0 B | 0 B |
| `common/matches-word` | **Inconclusive** | 8,208 B | 3.550 us | 24.569 us | 0.14x | 0.14–0.15x | -21.028 us | 9 pairs; 40/40 ms; 11,173/1,642 ops/lane; IQR 1.005/1.019 | `Pcre2Literal` | 1.382 us | 4.284 us | 6.061 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Inconclusive** | 8,208 B | 9.602 us | 81.646 us | 0.12x | 0.11–0.12x | -71.978 us | 9 pairs; 40/40 ms; 4,123/495 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 9.330 us | 92.558 us | 94.051 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,166.587 us | 28,387.600 us | 0.15x | 0.14–0.15x | -24234.363 us | 9 pairs; 33/28 ms; 8/1 ops/lane; IQR 1.008/1.041 | `Pcre2Backtracking` | 5,564.060 us | 11,743.720 us | 18,447.033 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,506.817 us | 55,617.800 us | 0.06x | 0.06–0.06x | -52165.683 us | 9 pairs; 42/56 ms; 12/1 ops/lane; IQR 1.024/1.020 | `Pcre2Backtracking` | 4,029.208 us | 40,251.100 us | 46,858.050 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,943.300 us | 482,679.500 us | 0.00x | 0.00–0.00x | -480731.548 us | 9 pairs; 41/483 ms; 21/1 ops/lane; IQR 1.039/1.002 | `Pcre2Character` | 1,905.681 us | 22,977.900 us | 29,698.800 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,540.283 us | 11,065.200 us | 0.50x | 0.47–0.57x | -5505.617 us | 9 pairs; 33/44 ms; 6/4 ops/lane; IQR 1.007/1.103 | `Pcre2Literal` | 1,449.917 us | 1,350.090 us | 8,288.100 us | 0.67x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,768.000 us | 145,666.400 us | 0.13x | 0.13–0.13x | -126822.450 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.011/1.003 | `Pcre2Backtracking` | 1,626.688 us | 515.127 us | 8,597.867 us | 2.18x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,793.550 us | 21,776.800 us | 0.22x | 0.22–0.22x | -16997.475 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.005/1.005 | `Pcre2Slice` | 2,882.511 us | 10,988.260 us | 19,065.500 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,399.300 us | 134,663.500 us | 0.17x | 0.16–0.17x | -112466.600 us | 9 pairs; 45/135 ms; 2/1 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 1,917.489 us | 2,348.536 us | 10,547.940 us | 2.12x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 188.256 us | 995.129 us | 0.19x | 0.19–0.19x | -806.305 us | 9 pairs; 39/38 ms; 209/38 ops/lane; IQR 1.004/1.029 | `Pcre2Backtracking` | 225.523 us | 316.131 us | 529.970 us | 0.36x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,161.500 us | 13,436.400 us | 0.83x | 0.83–0.84x | -2259.158 us | 9 pairs; 45/40 ms; 4/3 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 316.772 us | 15,036.025 us | 15,277.700 us | 0.73x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,753.686 us | 98,801.000 us | 0.03x | 0.03–0.03x | -96038.900 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.003/1.001 | `Pcre2Character` | 2,032.580 us | 19,787.333 us | 20,161.300 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.285 us | 2,758.936 us | 0.07x | 0.07–0.07x | -2573.556 us | 9 pairs; 40/39 ms; 216/14 ops/lane; IQR 1.001/1.012 | `Pcre2SingleTokenRepeat` | 189.484 us | 1,021.260 us | 1,429.248 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,580.424 us | 20,154.000 us | 0.08x | 0.08–0.08x | -18562.276 us | 9 pairs; 40/40 ms; 25/2 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 582.153 us | 10,656.360 us | 10,820.340 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.136 us | 1.095 us | 0.12x | 0.12–0.12x | -0.959 us | 9 pairs; 40/41 ms; 294,563/37,081 ops/lane; IQR 1.006/1.002 | `Pcre2Slice` | 0.716 us | 0.259 us | 0.280 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.511 us | 0.829 us | 0.62x | 0.62–0.63x | -0.313 us | 9 pairs; 41/40 ms; 80,282/48,500 ops/lane; IQR 1.018/1.006 | `Pcre2Literal` | 0.151 us | 0.148 us | 0.172 us | 2.96x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.296 us | 1.179 us | 0.25x | 0.25–0.25x | -0.884 us | 9 pairs; 41/40 ms; 137,826/33,670 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 1.079 us | 0.346 us | 0.376 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.907 us | 1.129 us | 0.80x | 0.80–0.81x | -0.222 us | 9 pairs; 39/40 ms; 42,929/35,503 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0.282 us | 0.426 us | 0.475 us | 1.91x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.171 us | 0.849 us | 0.20x | 0.20–0.20x | -0.682 us | 9 pairs; 38/40 ms; 224,459/47,145 ops/lane; IQR 1.013/1.008 | `Pcre2Slice` | 0.903 us | 0.158 us | 0.203 us | 0.84x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.909 us | 1.109 us | 0.82x | 0.82–0.82x | -0.200 us | 9 pairs; 41/40 ms; 44,745/36,131 ops/lane; IQR 1.001/1.001 | `Pcre2Slice` | 4.416 us | 0.307 us | 0.325 us | 2.80x | 0 B | 0 B |
| `simple/foo-dense` | **Native faster** | 18 B | 0.877 us | 0.830 us | 1.06x | 1.05–1.06x | +0.047 us | 9 pairs; 40/40 ms; 45,612/48,043 ops/lane; IQR 1.005/1.002 | `Pcre2Literal` | 3.555 us | 0.181 us | 0.202 us | 4.34x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.940 us | 1.185 us | 0.79x | 0.79–0.80x | -0.245 us | 9 pairs; 40/40 ms; 42,613/33,699 ops/lane; IQR 1.002/1.005 | `Pcre2Backtracking` | 5.214 us | 0.402 us | 0.425 us | 2.21x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.213 us | 1.131 us | 1.07x | 1.07–1.08x | +0.081 us | 9 pairs; 41/40 ms; 34,153/35,272 ops/lane; IQR 1.003/1.003 | `Pcre2Character` | 3.644 us | 0.483 us | 0.518 us | 2.34x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.804 us | 0.861 us | 0.93x | 0.93–0.94x | -0.057 us | 9 pairs; 40/39 ms; 50,297/45,469 ops/lane; IQR 1.003/1.006 | `Pcre2Slice` | 2.151 us | 0.183 us | 0.223 us | 3.60x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.628 us | 1.110 us | 0.57x | 0.56–0.57x | -0.482 us | 9 pairs; 41/40 ms; 64,910/36,137 ops/lane; IQR 1.003/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.512 us | 0.832 us | 0.62x | 0.61–0.62x | -0.320 us | 9 pairs; 41/40 ms; 79,565/47,800 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.715 us | 1.187 us | 0.60x | 0.60–0.60x | -0.472 us | 9 pairs; 38/40 ms; 53,438/33,456 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.938 us | 1.131 us | 0.83x | 0.83–0.83x | -0.194 us | 9 pairs; 40/40 ms; 42,438/35,488 ops/lane; IQR 1.003/1.004 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.503 us | 0.862 us | 0.58x | 0.58–0.59x | -0.357 us | 9 pairs; 39/40 ms; 77,371/46,487 ops/lane; IQR 1.009/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.262 us | — | — | — | — | — | `—` | 25.671 us | 99.448 us | 101.059 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.505 us | — | — | — | — | — | `—` | 2.201 us | 0.362 us | 0.391 us | 16.65x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.021 us | — | — | — | — | — | `—` | 0.946 us | 0.243 us | 0.275 us | 3.71x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.201 us | — | — | — | — | — | `—` | 3.131 us | 0.455 us | 0.494 us | 2.43x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.492 us | — | — | — | — | — | `—` | 1.040 us | 0.542 us | 0.591 us | 2.53x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.334 us | — | — | — | — | — | `—` | 1.598 us | 0.265 us | 0.317 us | 13.68x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.088 us | 0.084 us | 1.05x | 1.04–1.13x | +0.004 us | 9 pairs; 39/40 ms; 449,544/476,618 ops/lane; IQR 1.091/1.008 | `Pcre2Character` | 0 B | 0 B | 2.225 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.117 us | 0.11x | 0.11–0.11x | -0.104 us | 9 pairs; 40/40 ms; 3,133,445/345,571 ops/lane; IQR 1.009/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 127.744 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.381 us | 0.106 us | 3.62x | 3.60–3.65x | +0.275 us | 9 pairs; 35/40 ms; 91,091/376,309 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 20.200 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.389 us | 0.117 us | 3.32x | 3.30–3.34x | +0.272 us | 9 pairs; 36/41 ms; 92,315/350,513 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 73.713 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.403 us | 0.106 us | 3.80x | 3.69–3.83x | +0.297 us | 9 pairs; 38/41 ms; 93,074/384,577 ops/lane; IQR 1.022/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 70.125 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.584 us | 0.343 us | 1.70x | 1.68–1.72x | +0.241 us | 9 pairs; 39/39 ms; 66,421/113,415 ops/lane; IQR 1.006/1.006 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.638 us | 0.02x | 0.02–0.02x | -0.626 us | 9 pairs; 38/40 ms; 3,266,049/63,006 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 21.606 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.509 us | 0.02x | 0.02–0.02x | -0.497 us | 9 pairs; 43/41 ms; 3,505,536/80,314 ops/lane; IQR 1.029/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 21.038 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.471 us | 0.107 us | 4.40x | 4.29–4.43x | +0.364 us | 9 pairs; 37/38 ms; 77,521/355,854 ops/lane; IQR 1.003/1.027 | `Pcre2Backtracking` | 0 B | 0 B | 18.575 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.439 us | 10.380 us | 0.81x | 0.80–0.82x | -1.962 us | 9 pairs; 43/39 ms; 5,052/3,764 ops/lane; IQR 1.003/1.015 | `Pcre2Literal` | 0 B | 0 B | 0.812 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.297 us | 1.048 us | 0.28x | 0.28–0.28x | -0.750 us | 9 pairs; 34/40 ms; 115,886/37,917 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 129.162 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Inconclusive** | 17 B | 0.421 us | 0.470 us | 0.90x | 0.76–0.90x | -0.050 us | 9 pairs; 47/41 ms; 112,270/87,386 ops/lane; IQR 1.103/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 115.969 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.303 us | 0.472 us | 0.64x | 0.64–0.65x | -0.170 us | 9 pairs; 38/41 ms; 124,493/86,959 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 140.588 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.295 us | 0.591 us | 0.50x | 0.50–0.50x | -0.295 us | 9 pairs; 37/39 ms; 124,105/65,709 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 104.112 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.297 us | 0.365 us | 0.81x | 0.79–0.82x | -0.069 us | 9 pairs; 34/37 ms; 114,440/100,713 ops/lane; IQR 1.010/1.054 | `Pcre2Backtracking` | 0 B | 0 B | 212.738 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.835 us | 9.233 us | 0.52x | 0.52–0.52x | -4.402 us | 9 pairs; 38/40 ms; 7,927/4,299 ops/lane; IQR 1.003/1.004 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.261 us | 0.314 us | 0.83x | 0.83–0.84x | -0.053 us | 9 pairs; 36/39 ms; 139,591/124,159 ops/lane; IQR 1.003/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 91.769 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.271 us | 0.405 us | 0.67x | 0.66–0.67x | -0.135 us | 9 pairs; 37/38 ms; 135,533/94,767 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 124.869 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.798 us | — | — | — | — | — | `—` | 0 B | — | 13.456 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.196 us | 0.298 us | 0.66x | 0.66–0.67x | -0.101 us | 9 pairs; 40/40 ms; 202,339/133,602 ops/lane; IQR 1.018/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 53.369 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.708 us | — | — | — | — | — | `—` | 0 B | — | 15.194 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.581 us | 0.677 us | 0.86x | 0.85–0.86x | -0.097 us | 9 pairs; 38/39 ms; 65,955/57,321 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.931 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.858 us | 29.015 us | 0.34x | 0.34–0.34x | -19.136 us | 9 pairs; 39/40 ms; 3,995/1,387 ops/lane; IQR 1.005/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.825 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.954 us | 1.054 us | 0.90x | 0.90–0.91x | -0.102 us | 9 pairs; 40/39 ms; 42,178/36,698 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 131.350 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.988 us | 1.185 us | 0.83x | 0.83–0.84x | -0.197 us | 9 pairs; 39/40 ms; 39,518/33,865 ops/lane; IQR 1.010/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 112.763 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.974 us | 1.035 us | 0.94x | 0.94–0.94x | -0.062 us | 9 pairs; 40/39 ms; 41,299/38,060 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 140.287 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.960 us | 1.167 us | 0.82x | 0.82–0.83x | -0.207 us | 9 pairs; 40/41 ms; 41,364/35,275 ops/lane; IQR 1.005/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 102.981 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.938 us | 0.951 us | 0.99x | 0.98–0.99x | -0.012 us | 9 pairs; 40/40 ms; 42,124/41,824 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 212.319 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.676 us | 9.241 us | 0.61x | 0.61–0.62x | -3.570 us | 9 pairs; 41/40 ms; 7,162/4,299 ops/lane; IQR 1.003/1.006 | `Pcre2Character` | 0 B | 0 B | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.922 us | 0.891 us | 1.03x | 1.03–1.04x | +0.031 us | 9 pairs; 40/40 ms; 43,304/45,311 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 91.550 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.924 us | 0.966 us | 0.96x | 0.95–0.96x | -0.043 us | 9 pairs; 40/41 ms; 42,931/42,324 ops/lane; IQR 1.003/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 123.263 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.035 us | — | — | — | — | — | `—` | 0 B | — | 14.769 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.827 us | 0.859 us | 0.96x | 0.94–0.96x | -0.033 us | 9 pairs; 40/41 ms; 48,481/47,274 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 53.181 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.924 us | — | — | — | — | — | `—` | 0 B | — | 14.725 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.916 us | 1.260 us | 0.73x | 0.72–0.73x | -0.343 us | 9 pairs; 40/40 ms; 43,800/31,429 ops/lane; IQR 1.001/1.008 | `Pcre2Slice` | 0 B | 0 B | 65.112 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.390 us | 2.288 us | 0.61x | 0.61–0.61x | -0.892 us | 9 pairs; 40/40 ms; 28,730/17,478 ops/lane; IQR 1.002/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.672 us | 1.055 us | 0.64x | 0.63–0.64x | -0.386 us | 9 pairs; 40/40 ms; 59,836/37,554 ops/lane; IQR 1.009/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 129.831 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.690 us | 1.193 us | 0.58x | 0.58–0.58x | -0.502 us | 9 pairs; 40/41 ms; 57,547/33,958 ops/lane; IQR 1.007/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 113.119 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.653 us | 1.030 us | 0.63x | 0.63–0.64x | -0.376 us | 9 pairs; 38/41 ms; 58,099/39,613 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 140.569 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.632 us | 1.168 us | 0.54x | 0.54–0.54x | -0.534 us | 9 pairs; 40/41 ms; 63,469/34,763 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 102.925 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.666 us | 0.955 us | 0.70x | 0.70–0.70x | -0.290 us | 9 pairs; 40/40 ms; 60,303/41,960 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 212.469 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.789 us | 2.268 us | 0.79x | 0.79–0.79x | -0.485 us | 9 pairs; 40/40 ms; 22,283/17,515 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 0 B | 0 B | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.604 us | 0.886 us | 0.68x | 0.68–0.69x | -0.282 us | 9 pairs; 39/40 ms; 64,949/45,388 ops/lane; IQR 1.006/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 89.662 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.621 us | 0.964 us | 0.64x | 0.64–0.65x | -0.343 us | 9 pairs; 40/40 ms; 64,624/41,195 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 121.875 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.739 us | — | — | — | — | — | `—` | 0 B | — | 13.238 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.493 us | 0.861 us | 0.57x | 0.56–0.57x | -0.368 us | 9 pairs; 40/40 ms; 81,238/46,872 ops/lane; IQR 1.005/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 53.419 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.642 us | — | — | — | — | — | `—` | 0 B | — | 13.575 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.524 us | 1.268 us | 0.41x | 0.41–0.42x | -0.743 us | 9 pairs; 39/41 ms; 74,663/32,256 ops/lane; IQR 1.001/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.006 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.477 us | — | — | — | — | — | `—` | 480 B | — | 0.750 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.109 us | — | — | — | — | — | `—` | 48 B | — | 127.256 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.231 us | — | — | — | — | — | `—` | 48 B | — | 113.181 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.093 us | — | — | — | — | — | `—` | 48 B | — | 140.200 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.113 us | — | — | — | — | — | `—` | 48 B | — | 102.300 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.095 us | — | — | — | — | — | `—` | 48 B | — | 215.769 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.949 us | — | — | — | — | — | `—` | 64 B | — | 1.481 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.111 us | — | — | — | — | — | `—` | 40 B | — | 92.281 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.112 us | — | — | — | — | — | `—` | 40 B | — | 124.300 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.959 us | — | — | — | — | — | `—` | 1,216 B | — | 13.606 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.043 us | — | — | — | — | — | `—` | 48 B | — | 53.112 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.690 us | — | — | — | — | — | `—` | 928 B | — | 14.700 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.739 us | — | — | — | — | — | `—` | 28,928 B | — | 65.044 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 27.475–218.801 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.464–95.141 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.293–14.481 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.163–49.186 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.035–1.392 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.838–45.178 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.707–22.259 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.193–99.833 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.138–72.301 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.220–5.505 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.427–377.129 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 37.982–294.673 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.861–5.103 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.138–15.481 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 170.399–1,338.576 us |

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
