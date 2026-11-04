<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `957DFDE8868CEB7A0AEBE9ACC49CD07A59556A35CA4A6781E8E0C96CDE14E9BB`
- Latest managed row measurement: `2026-11-04T00:42:52.9618441+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-04T00:42:52.9618441+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `78` managed faster, `1` equivalent, `12` native faster, `10` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `fe169a8fc3b0` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `fe169a8fc3b0` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.918 us | 2.095 us | 0.44x | 0.41–0.47x | -1.168 us | 9 pairs; 35/43 ms; 37,631/20,667 ops/lane; IQR 1.025/1.067 | `Pcre2Slice` | 547.770 us | 0.281 us | 0.973 us | 0.94x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.138 us | 12.130 us | 0.10x | 0.09–0.11x | -10.977 us | 9 pairs; 33/42 ms; 29,342/3,457 ops/lane; IQR 1.017/1.170 | `Pcre2Slice` | 0.670 us | 0.097 us | 3.607 us | 0.32x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.250 us | 0.01x | 0.01–0.01x | -1.239 us | 9 pairs; 41/39 ms; 3,661,059/31,099 ops/lane; IQR 1.006/1.008 | `Pcre2Slice` | 0.107 us | 1.248 us | 1.288 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.701 us | 0.159 us | 4.42x | 4.37–4.43x | +0.543 us | 9 pairs; 38/40 ms; 53,789/249,224 ops/lane; IQR 1.006/1.011 | `Pcre2Backtracking` | 0.047 us | 0.121 us | 0.156 us | 4.48x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.355 us | 0.583 us | 4.04x | 4.01–4.11x | +1.771 us | 9 pairs; 39/41 ms; 16,685/70,095 ops/lane; IQR 1.009/1.017 | `Pcre2Backtracking` | 0.116 us | 0.763 us | 0.810 us | 2.91x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.100 us | 0.299 us | 0.34x | 0.33–0.34x | -0.198 us | 9 pairs; 37/40 ms; 363,368/135,353 ops/lane; IQR 1.012/1.012 | `Pcre2Slice` | 0.114 us | 0.319 us | 0.348 us | 0.29x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.106 us | 0.349 us | 0.30x | 0.30–0.30x | -0.243 us | 9 pairs; 40/40 ms; 380,560/113,552 ops/lane; IQR 1.003/1.006 | `Pcre2Slice` | 0.117 us | 0.457 us | 0.484 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.081 us | 0.287 us | 0.28x | 0.28–0.29x | -0.205 us | 9 pairs; 41/40 ms; 505,440/138,626 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | 0.090 us | 0.239 us | 0.261 us | 0.31x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.076 us | 0.317 us | 0.24x | 0.24–0.24x | -0.240 us | 9 pairs; 38/40 ms; 500,969/127,711 ops/lane; IQR 1.001/1.011 | `Pcre2Slice` | 0.090 us | 0.254 us | 0.278 us | 0.28x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.174 us | 0.074 us | 2.34x | 2.28–2.39x | +0.100 us | 9 pairs; 40/40 ms; 230,254/541,731 ops/lane; IQR 1.009/1.041 | `Pcre2Slice` | 0.088 us | 1.021 us | 1.061 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.199 us | 0.17x | 0.17–0.17x | -0.166 us | 9 pairs; 39/40 ms; 1,173,697/203,197 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0.110 us | 0.302 us | 0.343 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.611 us | 0.10x | 0.10–0.10x | -0.550 us | 9 pairs; 40/39 ms; 657,373/63,090 ops/lane; IQR 1.002/1.010 | `Pcre2Backtracking` | 0.133 us | 0.232 us | 0.260 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.156 us | 0.21x | 0.21–0.22x | -0.123 us | 9 pairs; 38/41 ms; 1,138,568/261,581 ops/lane; IQR 1.025/1.009 | `Pcre2Backtracking` | 0.106 us | 0.184 us | 0.224 us | 0.15x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.022 us | 0.114 us | 0.20x | 0.18–0.20x | -0.092 us | 9 pairs; 44/40 ms; 1,954,621/349,435 ops/lane; IQR 1.067/1.007 | `Pcre2Backtracking` | 0.059 us | 0.091 us | 0.118 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.123 us | 0.47x | 0.46–0.50x | -0.065 us | 9 pairs; 40/38 ms; 688,201/305,558 ops/lane; IQR 1.013/1.040 | `Pcre2Backtracking` | 0.016 us | 0.087 us | 0.102 us | 0.57x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Inconclusive** | 67 B | 0.084 us | 0.276 us | 0.31x | 0.30–0.35x | -0.189 us | 9 pairs; 39/41 ms; 464,142/147,100 ops/lane; IQR 1.153/1.008 | `Pcre2Slice` | 0.096 us | 0.117 us | 0.152 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.025 us | 0.116 us | 0.22x | 0.20–0.22x | -0.091 us | 9 pairs; 44/40 ms; 1,734,101/347,908 ops/lane; IQR 1.040/1.006 | `Pcre2Backtracking` | 0.026 us | 0.091 us | 0.110 us | 0.23x | 0 B | 0 B |
| `literal/absolute-anchored` | **Managed faster** | 4,103 B | 0.118 us | 1.914 us | 0.06x | 0.06–0.07x | -1.796 us | 9 pairs; 38/44 ms; 320,266/22,961 ops/lane; IQR 1.004/1.042 | `Pcre2Literal` | 0.088 us | 0.052 us | 0.681 us | 0.17x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.121 us | 1.813 us | 0.07x | 0.06–0.07x | -1.692 us | 9 pairs; 41/32 ms; 335,817/17,445 ops/lane; IQR 1.004/1.116 | `Pcre2Literal` | 0.145 us | 0.054 us | 0.654 us | 0.18x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.155 us | 2.170 us | 0.07x | 0.06–0.08x | -2.013 us | 9 pairs; 40/42 ms; 255,647/19,308 ops/lane; IQR 1.005/1.176 | `Pcre2Literal` | 0.206 us | 0.238 us | 0.885 us | 0.17x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.151 us | 1.811 us | 0.08x | 0.08–0.09x | -1.660 us | 9 pairs; 39/45 ms; 260,828/24,623 ops/lane; IQR 1.006/1.068 | `Pcre2Literal` | 0.183 us | 0.207 us | 0.852 us | 0.18x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.091 us | 9 pairs; 40/39 ms; 4,518,361/388,652 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 0.518 us | 0.068 us | 0.087 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.068 us | 0.085 us | 0.80x | 0.80–0.85x | -0.016 us | 9 pairs; 40/40 ms; 591,076/469,693 ops/lane; IQR 1.073/1.013 | `Pcre2Literal` | 0.124 us | 0.051 us | 0.073 us | 0.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.109 us | 0.12x | 0.12–0.13x | -0.096 us | 9 pairs; 39/40 ms; 2,889,807/363,408 ops/lane; IQR 1.011/1.014 | `Pcre2Backtracking` | 0.240 us | 0.093 us | 0.116 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.118 us | 0.122 us | 0.96x | 0.96–0.98x | -0.004 us | 9 pairs; 41/39 ms; 346,473/315,946 ops/lane; IQR 1.003/1.009 | `Pcre2Character` | 0.135 us | 0.114 us | 0.145 us | 0.82x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.075 us | 0.201 us | 0.37x | 0.37–0.37x | -0.127 us | 9 pairs; 40/39 ms; 529,177/192,436 ops/lane; IQR 1.001/1.004 | `Pcre2Slice` | 0.461 us | 0.075 us | 0.106 us | 0.71x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 82.051 us | 159.306 us | 0.52x | 0.51–0.52x | -76.882 us | 9 pairs; 40/40 ms; 488/250 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 400.550 us | 104.385 us | 104.954 us | 0.78x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.857 us | 282.609 us | 0.13x | 0.13–0.13x | -244.451 us | 9 pairs; 40/40 ms; 1,044/141 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 388.450 us | 221.475 us | 232.791 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.499 us | 212.405 us | 0.04x | 0.04–0.04x | -202.884 us | 9 pairs; 40/40 ms; 4,189/187 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 7.123 us | 228.420 us | 229.254 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 241.747 us | 218.000 us | 1.11x | 1.11–1.12x | +23.866 us | 9 pairs; 40/41 ms; 165/187 ops/lane; IQR 1.002/1.008 | `Pcre2SingleTokenRepeat` | 7.212 us | 240.402 us | 239.845 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Inconclusive** | 8,208 B | 3.512 us | 22.838 us | 0.15x | 0.15–0.16x | -19.326 us | 9 pairs; 39/41 ms; 11,213/1,784 ops/lane; IQR 1.003/1.017 | `Pcre2Literal` | 1.392 us | 4.237 us | 6.125 us | 0.57x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.755 us | 81.932 us | 0.12x | 0.12–0.12x | -72.176 us | 9 pairs; 40/40 ms; 4,074/489 ops/lane; IQR 1.000/1.004 | `Pcre2Backtracking` | 9.325 us | 97.648 us | 97.579 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,246.822 us | 27,472.200 us | 0.16x | 0.15–0.17x | -23158.056 us | 9 pairs; 38/27 ms; 9/1 ops/lane; IQR 1.013/1.077 | `Pcre2Backtracking` | 6,170.611 us | 13,192.125 us | 20,546.367 us | 0.21x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,679.045 us | 55,763.400 us | 0.07x | 0.06–0.08x | -51851.855 us | 9 pairs; 40/56 ms; 11/1 ops/lane; IQR 1.043/1.011 | `Pcre2Backtracking` | 3,976.615 us | 44,441.900 us | 51,821.700 us | 0.07x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,998.462 us | 474,490.000 us | 0.00x | 0.00–0.00x | -472541.162 us | 9 pairs; 42/474 ms; 21/1 ops/lane; IQR 1.037/1.003 | `Pcre2Character` | 1,873.222 us | 23,118.333 us | 30,834.450 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Managed faster** | 16,013,977 B | 5,630.483 us | 11,975.875 us | 0.47x | 0.46–0.47x | -6336.175 us | 9 pairs; 34/48 ms; 6/4 ops/lane; IQR 1.006/1.007 | `Pcre2Literal` | 1,508.541 us | 1,408.850 us | 8,725.867 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,773.250 us | 146,409.300 us | 0.13x | 0.13–0.13x | -127707.700 us | 9 pairs; 38/146 ms; 2/1 ops/lane; IQR 1.012/1.006 | `Pcre2Backtracking` | 1,636.590 us | 532.944 us | 8,573.767 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,808.788 us | 22,088.900 us | 0.22x | 0.22–0.22x | -17228.950 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.002/1.012 | `Pcre2Slice` | 2,932.906 us | 11,575.200 us | 19,593.100 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,303.200 us | 133,952.300 us | 0.17x | 0.16–0.17x | -111452.000 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 1,936.627 us | 2,374.983 us | 10,561.820 us | 2.11x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 188.989 us | 978.590 us | 0.19x | 0.19–0.20x | -790.259 us | 9 pairs; 39/41 ms; 206/42 ops/lane; IQR 1.006/1.021 | `Pcre2Backtracking` | 219.967 us | 351.376 us | 575.114 us | 0.33x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,310.767 us | 13,193.167 us | 0.93x | 0.93–0.94x | -898.433 us | 9 pairs; 37/40 ms; 3/3 ops/lane; IQR 1.002/1.010 | `Pcre2Backtracking` | 316.086 us | 15,985.275 us | 16,226.350 us | 0.76x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,786.564 us | 98,867.000 us | 0.03x | 0.03–0.03x | -96060.971 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.007/1.005 | `Pcre2Character` | 1,890.289 us | 19,891.433 us | 20,122.767 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 185.414 us | 2,707.713 us | 0.07x | 0.07–0.07x | -2522.569 us | 9 pairs; 40/41 ms; 214/15 ops/lane; IQR 1.006/1.007 | `Pcre2SingleTokenRepeat` | 189.133 us | 1,002.528 us | 1,434.817 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,544.124 us | 20,381.400 us | 0.08x | 0.08–0.08x | -18845.872 us | 9 pairs; 39/41 ms; 25/2 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 599.036 us | 10,870.280 us | 11,124.140 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.150 us | 1.104 us | 0.14x | 0.14–0.14x | -0.952 us | 9 pairs; 40/40 ms; 266,647/36,448 ops/lane; IQR 1.009/1.003 | `Pcre2Slice` | 0.735 us | 0.258 us | 0.282 us | 0.53x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.486 us | 0.827 us | 0.59x | 0.59–0.59x | -0.341 us | 9 pairs; 39/40 ms; 81,185/48,361 ops/lane; IQR 1.003/1.004 | `Pcre2Literal` | 0.145 us | 0.150 us | 0.175 us | 2.78x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.289 us | 1.174 us | 0.25x | 0.25–0.25x | -0.885 us | 9 pairs; 39/40 ms; 134,604/34,136 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 1.071 us | 0.351 us | 0.381 us | 0.76x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.876 us | 1.126 us | 0.78x | 0.78–0.78x | -0.251 us | 9 pairs; 41/40 ms; 46,496/35,348 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0.281 us | 0.428 us | 0.474 us | 1.85x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.152 us | 0.855 us | 0.18x | 0.18–0.18x | -0.703 us | 9 pairs; 40/39 ms; 265,074/45,801 ops/lane; IQR 1.003/1.005 | `Pcre2Slice` | 0.905 us | 0.152 us | 0.202 us | 0.75x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.955 us | 1.125 us | 0.85x | 0.85–0.85x | -0.170 us | 9 pairs; 40/41 ms; 41,488/36,571 ops/lane; IQR 1.004/1.003 | `Pcre2Slice` | 4.534 us | 0.304 us | 0.325 us | 2.94x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.790 us | 0.831 us | 0.95x | 0.95–0.95x | -0.041 us | 9 pairs; 40/40 ms; 50,926/48,103 ops/lane; IQR 1.004/1.002 | `Pcre2Literal` | 3.573 us | 0.181 us | 0.204 us | 3.87x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.993 us | 1.189 us | 0.84x | 0.83–0.84x | -0.196 us | 9 pairs; 40/40 ms; 40,297/33,593 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 5.286 us | 0.405 us | 0.430 us | 2.31x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.196 us | 1.135 us | 1.05x | 1.05–1.06x | +0.061 us | 9 pairs; 40/40 ms; 33,159/35,194 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 3.679 us | 0.481 us | 0.520 us | 2.30x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.795 us | 0.864 us | 0.92x | 0.92–0.93x | -0.068 us | 9 pairs; 38/40 ms; 47,351/46,819 ops/lane; IQR 1.003/1.005 | `Pcre2Slice` | 2.158 us | 0.182 us | 0.225 us | 3.53x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.579 us | 1.127 us | 0.51x | 0.51–0.52x | -0.548 us | 9 pairs; 37/40 ms; 64,596/35,280 ops/lane; IQR 1.004/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.500 us | 0.832 us | 0.60x | 0.60–0.60x | -0.332 us | 9 pairs; 39/40 ms; 78,868/48,154 ops/lane; IQR 1.002/1.001 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.710 us | 1.194 us | 0.59x | 0.59–0.60x | -0.485 us | 9 pairs; 40/40 ms; 55,720/33,239 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.883 us | 1.133 us | 0.78x | 0.78–0.78x | -0.251 us | 9 pairs; 36/40 ms; 41,220/35,189 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.475 us | 0.864 us | 0.55x | 0.55–0.55x | -0.391 us | 9 pairs; 39/40 ms; 82,905/46,468 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 61.111 us | — | — | — | — | — | `—` | 65.134 us | 102.960 us | 104.882 us | 0.58x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 6.683 us | — | — | — | — | — | `—` | 2.219 us | 0.360 us | 0.386 us | 17.32x | 20,880 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.002 us | — | — | — | — | — | `—` | 0.804 us | 0.248 us | 0.276 us | 3.64x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.149 us | — | — | — | — | — | `—` | 2.947 us | 0.461 us | 0.494 us | 2.33x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.469 us | — | — | — | — | — | `—` | 0.920 us | 0.542 us | 0.592 us | 2.48x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.406 us | — | — | — | — | — | `—` | 1.537 us | 0.272 us | 0.326 us | 13.50x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.093 us | 0.084 us | 1.12x | 1.11–1.12x | +0.010 us | 9 pairs; 42/40 ms; 454,946/479,984 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 0 B | 0 B | 2.219 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.118 us | 0.11x | 0.11–0.11x | -0.105 us | 9 pairs; 40/41 ms; 3,008,308/346,605 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 131.262 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.385 us | 0.104 us | 3.67x | 3.65–3.69x | +0.280 us | 9 pairs; 36/39 ms; 93,193/377,523 ops/lane; IQR 1.009/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 20.219 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.391 us | 0.116 us | 3.33x | 3.33–3.35x | +0.272 us | 9 pairs; 37/40 ms; 93,691/346,615 ops/lane; IQR 1.010/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 85.800 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.397 us | 0.105 us | 3.78x | 3.74–3.79x | +0.292 us | 9 pairs; 37/40 ms; 92,951/378,046 ops/lane; IQR 1.007/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 71.881 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.586 us | 0.346 us | 1.70x | 1.68–1.71x | +0.240 us | 9 pairs; 38/39 ms; 65,135/112,502 ops/lane; IQR 1.012/1.010 | `Pcre2Character` | 0 B | 0 B | 1.475 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.636 us | 0.02x | 0.02–0.02x | -0.624 us | 9 pairs; 42/39 ms; 3,470,114/60,604 ops/lane; IQR 1.052/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 23.988 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.016 us | 0.509 us | 0.03x | 0.02–0.03x | -0.493 us | 9 pairs; 54/38 ms; 3,439,759/75,029 ops/lane; IQR 1.188/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 21.650 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.470 us | 0.107 us | 4.38x | 4.24–4.39x | +0.363 us | 9 pairs; 37/40 ms; 79,438/371,980 ops/lane; IQR 1.004/1.026 | `Pcre2Backtracking` | 0 B | 0 B | 18.369 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.291 us | 10.564 us | 0.79x | 0.78–0.79x | -2.224 us | 9 pairs; 39/40 ms; 4,753/3,784 ops/lane; IQR 1.007/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.825 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.307 us | 1.053 us | 0.29x | 0.29–0.29x | -0.746 us | 9 pairs; 41/41 ms; 132,600/38,567 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.481 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.310 us | 0.466 us | 0.67x | 0.66–0.67x | -0.156 us | 9 pairs; 37/38 ms; 119,521/81,590 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 115.956 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.296 us | 0.464 us | 0.64x | 0.63–0.64x | -0.169 us | 9 pairs; 36/41 ms; 122,099/87,649 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 141.794 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.296 us | 0.589 us | 0.50x | 0.50–0.51x | -0.293 us | 9 pairs; 36/39 ms; 122,829/65,789 ops/lane; IQR 1.005/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 104.894 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.295 us | 0.394 us | 0.75x | 0.74–0.76x | -0.100 us | 9 pairs; 38/38 ms; 130,487/97,232 ops/lane; IQR 1.003/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 217.831 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.845 us | 8.842 us | 0.55x | 0.55–0.55x | -4.000 us | 9 pairs; 40/40 ms; 8,238/4,533 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.263 us | 0.307 us | 0.86x | 0.85–0.88x | -0.044 us | 9 pairs; 37/40 ms; 140,887/128,785 ops/lane; IQR 1.024/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 94.694 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.270 us | 0.402 us | 0.67x | 0.67–0.68x | -0.132 us | 9 pairs; 38/40 ms; 141,786/99,201 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 121.463 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.803 us | — | — | — | — | — | `—` | 0 B | — | 13.325 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.197 us | 0.303 us | 0.64x | 0.64–0.65x | -0.108 us | 9 pairs; 41/40 ms; 207,387/132,005 ops/lane; IQR 1.016/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 54.444 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.706 us | — | — | — | — | — | `—` | 0 B | — | 14.069 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.523 us | 0.680 us | 0.77x | 0.76–0.78x | -0.154 us | 9 pairs; 37/41 ms; 71,583/59,610 ops/lane; IQR 1.002/1.010 | `Pcre2Slice` | 0 B | 0 B | 65.225 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.884 us | 28.976 us | 0.34x | 0.34–0.34x | -19.088 us | 9 pairs; 35/40 ms; 3,569/1,378 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | 0 B | 0 B | 0.794 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.951 us | 1.058 us | 0.90x | 0.89–0.90x | -0.104 us | 9 pairs; 40/40 ms; 42,017/38,109 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 131.975 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.012 us | 1.196 us | 0.85x | 0.85–0.85x | -0.182 us | 9 pairs; 41/40 ms; 40,315/33,078 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 115.500 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.960 us | 1.026 us | 0.94x | 0.93–0.94x | -0.066 us | 9 pairs; 38/39 ms; 39,738/38,255 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 143.225 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.958 us | 1.168 us | 0.82x | 0.82–0.82x | -0.212 us | 9 pairs; 41/40 ms; 42,397/34,081 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 104.112 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.957 us | 0.946 us | 1.01x | 1.01–1.01x | +0.011 us | 9 pairs; 41/40 ms; 42,381/42,051 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 216.381 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.645 us | 8.930 us | 0.63x | 0.63–0.63x | -3.286 us | 9 pairs; 40/40 ms; 7,109/4,471 ops/lane; IQR 1.001/1.002 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.917 us | 0.889 us | 1.03x | 1.03–1.03x | +0.026 us | 9 pairs; 39/40 ms; 43,010/45,087 ops/lane; IQR 1.002/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 95.481 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.926 us | 0.963 us | 0.96x | 0.96–0.96x | -0.036 us | 9 pairs; 39/40 ms; 42,540/41,633 ops/lane; IQR 1.003/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 122.081 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.028 us | — | — | — | — | — | `—` | 0 B | — | 13.262 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.829 us | 0.861 us | 0.96x | 0.96–0.97x | -0.031 us | 9 pairs; 39/40 ms; 47,282/46,319 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 53.837 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.947 us | — | — | — | — | — | `—` | 0 B | — | 13.831 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.872 us | 1.259 us | 0.69x | 0.69–0.69x | -0.387 us | 9 pairs; 40/40 ms; 46,251/32,080 ops/lane; IQR 1.004/1.003 | `Pcre2Slice` | 0 B | 0 B | 65.787 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.361 us | 2.287 us | 0.59x | 0.59–0.60x | -0.926 us | 9 pairs; 40/40 ms; 29,201/17,350 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.781 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.660 us | 1.052 us | 0.63x | 0.63–0.63x | -0.392 us | 9 pairs; 43/41 ms; 65,831/39,432 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 133.956 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.683 us | 1.197 us | 0.57x | 0.57–0.57x | -0.513 us | 9 pairs; 40/40 ms; 58,447/33,756 ops/lane; IQR 1.001/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 114.556 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.662 us | 1.027 us | 0.65x | 0.64–0.65x | -0.365 us | 9 pairs; 40/40 ms; 60,303/39,065 ops/lane; IQR 1.007/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 144.394 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.698 us | 1.168 us | 0.60x | 0.59–0.60x | -0.473 us | 9 pairs; 42/40 ms; 59,584/33,955 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 104.250 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.619 us | 0.943 us | 0.66x | 0.65–0.66x | -0.323 us | 9 pairs; 40/40 ms; 64,013/42,334 ops/lane; IQR 1.003/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 215.444 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.799 us | 2.192 us | 0.82x | 0.82–0.82x | -0.394 us | 9 pairs; 40/40 ms; 22,051/18,206 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | 0 B | 0 B | 1.456 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.618 us | 0.891 us | 0.69x | 0.69–0.70x | -0.273 us | 9 pairs; 41/40 ms; 65,950/45,275 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 91.131 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.610 us | 0.965 us | 0.63x | 0.63–0.64x | -0.356 us | 9 pairs; 38/41 ms; 62,856/42,878 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 122.850 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.757 us | — | — | — | — | — | `—` | 0 B | — | 13.575 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.493 us | 0.861 us | 0.57x | 0.57–0.57x | -0.368 us | 9 pairs; 40/40 ms; 81,202/46,883 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 54.269 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.673 us | — | — | — | — | — | `—` | 0 B | — | 14.756 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.586 us | 1.257 us | 0.47x | 0.46–0.47x | -0.672 us | 9 pairs; 40/40 ms; 67,871/31,741 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | 0 B | 0 B | 64.756 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.260 us | — | — | — | — | — | `—` | 480 B | — | 0.725 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.093 us | — | — | — | — | — | `—` | 48 B | — | 132.019 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.187 us | — | — | — | — | — | `—` | 48 B | — | 118.963 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.070 us | — | — | — | — | — | `—` | 48 B | — | 145.219 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.081 us | — | — | — | — | — | `—` | 48 B | — | 100.975 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.073 us | — | — | — | — | — | `—` | 48 B | — | 222.062 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.710 us | — | — | — | — | — | `—` | 64 B | — | 2.106 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.079 us | — | — | — | — | — | `—` | 40 B | — | 91.206 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.087 us | — | — | — | — | — | `—` | 40 B | — | 125.950 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.880 us | — | — | — | — | — | `—` | 1,216 B | — | 15.450 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.012 us | — | — | — | — | — | `—` | 48 B | — | 54.969 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.625 us | — | — | — | — | — | `—` | 928 B | — | 14.400 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.939 us | — | — | — | — | — | `—` | 28,928 B | — | 66.150 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.850–225.804 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.550–95.804 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.201–12.858 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.390–49.220 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.048–1.494 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.857–45.547 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 6.195–24.014 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.217–99.968 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.129–72.791 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.220–5.520 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.005–374.244 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.069–0.076 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.871–313.796 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.859–3.882 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.192–15.759 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 177.319–1,363.695 us |

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
