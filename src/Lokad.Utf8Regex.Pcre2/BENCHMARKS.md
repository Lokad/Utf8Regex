<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `EED1478D9097BA494B075F4BB2FCC6412255664F70CA6CAAADCF30539F90B480`
- Latest managed row measurement: `2026-11-07T00:11:25.4360882+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-07T00:11:25.4360882+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `43/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `0` equivalent, `12` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `e7a65967deeb` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `e7a65967deeb` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.583 us | 2.311 us | 0.25x | 0.21–0.27x | -1.728 us | 9 pairs; 35/48 ms; 60,398/20,745 ops/lane; IQR 1.013/1.111 | `Pcre2Slice` | 544.173 us | 0.278 us | 0.805 us | 0.72x | 800 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 0.675 us | 12.163 us | 0.06x | 0.05–0.07x | -11.493 us | 9 pairs; 33/48 ms; 48,441/3,980 ops/lane; IQR 1.017/1.266 | `Pcre2Slice` | 0.506 us | 0.096 us | 3.536 us | 0.19x | 64 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.232 us | 0.01x | 0.01–0.01x | -1.221 us | 9 pairs; 41/41 ms; 3,763,373/33,076 ops/lane; IQR 1.018/1.014 | `Pcre2Slice` | 0.105 us | 1.084 us | 1.120 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.694 us | 0.154 us | 4.50x | 4.42–4.51x | +0.538 us | 9 pairs; 37/39 ms; 53,055/250,911 ops/lane; IQR 1.007/1.012 | `Pcre2Backtracking` | 0.045 us | 0.120 us | 0.158 us | 4.40x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.344 us | 0.569 us | 4.13x | 4.08–4.17x | +1.776 us | 9 pairs; 38/42 ms; 16,412/73,074 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0.115 us | 0.748 us | 0.793 us | 2.96x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.101 us | 0.291 us | 0.35x | 0.34–0.35x | -0.190 us | 9 pairs; 40/38 ms; 394,594/131,030 ops/lane; IQR 1.003/1.013 | `Pcre2Slice` | 0.114 us | 0.315 us | 0.337 us | 0.30x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.103 us | 0.356 us | 0.29x | 0.29–0.30x | -0.253 us | 9 pairs; 39/41 ms; 377,870/114,323 ops/lane; IQR 1.003/1.012 | `Pcre2Slice` | 0.120 us | 0.437 us | 0.458 us | 0.23x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.077 us | 0.284 us | 0.27x | 0.27–0.27x | -0.207 us | 9 pairs; 39/39 ms; 500,030/136,118 ops/lane; IQR 1.003/1.011 | `Pcre2Slice` | 0.091 us | 0.236 us | 0.267 us | 0.29x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.077 us | 0.308 us | 0.25x | 0.25–0.26x | -0.228 us | 9 pairs; 39/41 ms; 501,365/132,483 ops/lane; IQR 1.005/1.013 | `Pcre2Slice` | 0.092 us | 0.252 us | 0.276 us | 0.28x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.170 us | 0.074 us | 2.30x | 2.23–2.38x | +0.096 us | 9 pairs; 40/40 ms; 234,155/543,506 ops/lane; IQR 1.008/1.039 | `Pcre2Slice` | 0.090 us | 0.917 us | 0.963 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.194 us | 0.17x | 0.16–0.17x | -0.162 us | 9 pairs; 40/40 ms; 1,234,299/204,584 ops/lane; IQR 1.010/1.027 | `Pcre2Backtracking` | 0.110 us | 0.300 us | 0.345 us | 0.09x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.059 us | 0.590 us | 0.10x | 0.10–0.10x | -0.531 us | 9 pairs; 41/40 ms; 691,235/68,150 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0.136 us | 0.231 us | 0.252 us | 0.23x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.151 us | 0.22x | 0.21–0.22x | -0.119 us | 9 pairs; 38/41 ms; 1,152,683/273,006 ops/lane; IQR 1.015/1.007 | `Pcre2Backtracking` | 0.115 us | 0.180 us | 0.201 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.019 us | 0.111 us | 0.17x | 0.17–0.18x | -0.091 us | 9 pairs; 37/41 ms; 1,940,514/366,128 ops/lane; IQR 1.036/1.012 | `Pcre2Backtracking` | 0.058 us | 0.090 us | 0.112 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.056 us | 0.118 us | 0.48x | 0.47–0.48x | -0.062 us | 9 pairs; 40/42 ms; 702,294/353,332 ops/lane; IQR 1.012/1.019 | `Pcre2Backtracking` | 0.013 us | 0.084 us | 0.102 us | 0.55x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.083 us | 0.280 us | 0.30x | 0.30–0.30x | -0.197 us | 9 pairs; 40/40 ms; 478,133/142,060 ops/lane; IQR 1.010/1.012 | `Pcre2Slice` | 0.095 us | 0.117 us | 0.154 us | 0.54x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.022 us | 0.113 us | 0.20x | 0.20–0.20x | -0.091 us | 9 pairs; 38/41 ms; 1,720,613/360,699 ops/lane; IQR 1.008/1.014 | `Pcre2Backtracking` | 0.019 us | 0.093 us | 0.115 us | 0.19x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.116 us | 1.779 us | 0.07x | 0.06–0.07x | -1.663 us | 9 pairs; 39/32 ms; 339,838/17,739 ops/lane; IQR 1.013/1.075 | `Pcre2Literal` | 0.085 us | 0.050 us | 0.571 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.117 us | 1.929 us | 0.06x | 0.05–0.07x | -1.810 us | 9 pairs; 37/42 ms; 313,469/21,999 ops/lane; IQR 1.009/1.155 | `Pcre2Literal` | 0.143 us | 0.052 us | 0.590 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.151 us | 1.915 us | 0.08x | 0.07–0.09x | -1.761 us | 9 pairs; 40/38 ms; 261,192/20,008 ops/lane; IQR 1.005/1.113 | `Pcre2Literal` | 0.195 us | 0.233 us | 0.768 us | 0.20x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.138 us | 1.802 us | 0.08x | 0.07–0.08x | -1.664 us | 9 pairs; 37/44 ms; 270,416/24,452 ops/lane; IQR 1.008/1.031 | `Pcre2Literal` | 0.163 us | 0.201 us | 0.716 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.099 us | 0.09x | 0.09–0.09x | -0.091 us | 9 pairs; 39/40 ms; 4,523,819/405,620 ops/lane; IQR 1.004/1.008 | `Pcre2Slice` | 0.235 us | 0.068 us | 0.086 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.069 us | 0.086 us | 0.80x | 0.75–0.81x | -0.017 us | 9 pairs; 41/40 ms; 597,786/462,625 ops/lane; IQR 1.040/1.014 | `Pcre2Literal` | 0.120 us | 0.049 us | 0.072 us | 0.96x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.112 us | 0.12x | 0.12–0.12x | -0.098 us | 9 pairs; 22/38 ms; 1,686,139/339,394 ops/lane; IQR 1.006/1.017 | `Pcre2Backtracking` | 0.265 us | 0.092 us | 0.113 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.111 us | 0.119 us | 0.93x | 0.92–0.94x | -0.008 us | 9 pairs; 37/38 ms; 329,771/317,425 ops/lane; IQR 1.006/1.012 | `Pcre2Character` | 0.133 us | 0.115 us | 0.144 us | 0.78x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.074 us | 0.201 us | 0.37x | 0.37–0.37x | -0.127 us | 9 pairs; 40/38 ms; 541,430/188,707 ops/lane; IQR 1.005/1.008 | `Pcre2Slice` | 0.413 us | 0.072 us | 0.110 us | 0.68x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 80.629 us | 156.270 us | 0.52x | 0.51–0.52x | -75.433 us | 9 pairs; 41/42 ms; 513/266 ops/lane; IQR 1.011/1.010 | `Pcre2Slice` | 321.899 us | 98.268 us | 100.775 us | 0.80x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 39.055 us | 282.018 us | 0.14x | 0.14–0.14x | -242.738 us | 9 pairs; 39/40 ms; 1,010/142 ops/lane; IQR 1.004/1.006 | `Pcre2Character` | 264.551 us | 214.060 us | 224.731 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.394 us | 205.522 us | 0.05x | 0.05–0.05x | -196.088 us | 9 pairs; 39/40 ms; 4,165/196 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 7.034 us | 216.278 us | 219.331 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 233.958 us | 208.807 us | 1.12x | 1.12–1.13x | +25.354 us | 9 pairs; 40/38 ms; 171/184 ops/lane; IQR 1.007/1.010 | `Pcre2SingleTokenRepeat` | 6.997 us | 227.725 us | 228.975 us | 1.02x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.452 us | 23.190 us | 0.15x | 0.15–0.15x | -19.761 us | 9 pairs; 40/38 ms; 11,723/1,643 ops/lane; IQR 1.011/1.010 | `Pcre2Literal` | 1.346 us | 4.269 us | 5.761 us | 0.60x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.369 us | 79.504 us | 0.12x | 0.12–0.12x | -70.141 us | 9 pairs; 39/40 ms; 4,155/501 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 9.069 us | 88.946 us | 90.338 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,021.225 us | 27,268.950 us | 0.15x | 0.14–0.15x | -23294.238 us | 9 pairs; 32/55 ms; 8/2 ops/lane; IQR 1.015/1.055 | `Pcre2Backtracking` | 6,080.054 us | 11,585.843 us | 18,137.940 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,327.467 us | 54,382.800 us | 0.06x | 0.06–0.06x | -51055.950 us | 9 pairs; 40/54 ms; 12/1 ops/lane; IQR 1.022/1.028 | `Pcre2Backtracking` | 3,662.410 us | 40,106.850 us | 46,569.600 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,894.191 us | 469,544.300 us | 0.00x | 0.00–0.00x | -467710.927 us | 9 pairs; 42/470 ms; 22/1 ops/lane; IQR 1.031/1.005 | `Pcre2Character` | 1,796.360 us | 22,378.975 us | 28,862.533 us | 0.07x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,437.900 us | 11,069.900 us | 0.49x | 0.47–0.54x | -5649.617 us | 9 pairs; 33/44 ms; 6/4 ops/lane; IQR 1.004/1.111 | `Pcre2Literal` | 1,323.985 us | 1,288.811 us | 8,405.670 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,107.450 us | 141,469.000 us | 0.13x | 0.13–0.13x | -123124.000 us | 9 pairs; 36/141 ms; 2/1 ops/lane; IQR 1.035/1.009 | `Pcre2Backtracking` | 1,178.697 us | 494.330 us | 8,263.011 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,818.086 us | 21,364.650 us | 0.22x | 0.22–0.23x | -16544.921 us | 9 pairs; 34/43 ms; 7/2 ops/lane; IQR 1.011/1.021 | `Pcre2Slice` | 2,357.353 us | 10,730.462 us | 18,455.350 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,503.050 us | 129,248.100 us | 0.17x | 0.16–0.17x | -107755.100 us | 9 pairs; 43/129 ms; 2/1 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 1,468.863 us | 2,332.283 us | 10,357.737 us | 2.08x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 140.735 us | 1,006.500 us | 0.14x | 0.14–0.14x | -864.584 us | 9 pairs; 40/40 ms; 282/40 ops/lane; IQR 1.008/1.034 | `Pcre2Backtracking` | 158.198 us | 314.392 us | 524.083 us | 0.27x | 0 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 11,288.900 us | 13,170.667 us | 0.86x | 0.85–0.88x | -1851.792 us | 9 pairs; 45/40 ms; 4/3 ops/lane; IQR 1.005/1.013 | `Pcre2Backtracking` | 302.634 us | 14,665.500 us | 14,896.217 us | 0.76x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,777.057 us | 96,879.200 us | 0.03x | 0.03–0.03x | -94140.743 us | 9 pairs; 39/97 ms; 14/1 ops/lane; IQR 1.019/1.009 | `Pcre2Character` | 1,967.792 us | 19,354.775 us | 19,653.075 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 182.101 us | 2,703.927 us | 0.07x | 0.07–0.07x | -2521.826 us | 9 pairs; 39/41 ms; 212/15 ops/lane; IQR 1.008/1.022 | `Pcre2SingleTokenRepeat` | 181.090 us | 1,007.416 us | 1,238.875 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Inconclusive** | 594,930 B | 1,529.400 us | 19,674.250 us | 0.08x | 0.08–0.08x | -18151.462 us | 9 pairs; 40/39 ms; 26/2 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 566.918 us | 10,632.837 us | 10,853.614 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.141 us | 1.074 us | 0.13x | 0.13–0.13x | -0.933 us | 9 pairs; 39/39 ms; 276,881/36,272 ops/lane; IQR 1.013/1.006 | `Pcre2Slice` | 0.622 us | 0.255 us | 0.275 us | 0.51x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.489 us | 0.804 us | 0.61x | 0.61–0.61x | -0.315 us | 9 pairs; 41/40 ms; 84,747/49,540 ops/lane; IQR 1.007/1.009 | `Pcre2Literal` | 0.140 us | 0.145 us | 0.168 us | 2.91x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.293 us | 1.150 us | 0.25x | 0.25–0.26x | -0.857 us | 9 pairs; 40/39 ms; 137,959/33,854 ops/lane; IQR 1.011/1.010 | `Pcre2Backtracking` | 1.040 us | 0.342 us | 0.370 us | 0.79x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.858 us | 1.114 us | 0.77x | 0.77–0.78x | -0.252 us | 9 pairs; 24/40 ms; 28,145/36,125 ops/lane; IQR 1.003/1.014 | `Pcre2Character` | 0.273 us | 0.449 us | 0.464 us | 1.85x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.155 us | 0.838 us | 0.18x | 0.18–0.19x | -0.682 us | 9 pairs; 37/41 ms; 241,154/48,658 ops/lane; IQR 1.007/1.008 | `Pcre2Slice` | 0.770 us | 0.151 us | 0.196 us | 0.79x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.939 us | 1.084 us | 0.87x | 0.86–0.87x | -0.144 us | 9 pairs; 39/40 ms; 41,849/37,101 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | 2.310 us | 0.298 us | 0.316 us | 2.98x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.780 us | 0.824 us | 0.95x | 0.94–0.95x | -0.043 us | 9 pairs; 40/40 ms; 50,701/48,654 ops/lane; IQR 1.010/1.007 | `Pcre2Literal` | 3.482 us | 0.177 us | 0.198 us | 3.95x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.962 us | 1.169 us | 0.82x | 0.82–0.82x | -0.213 us | 9 pairs; 41/41 ms; 42,154/35,466 ops/lane; IQR 1.011/1.011 | `Pcre2Backtracking` | 5.143 us | 0.445 us | 0.429 us | 2.24x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.204 us | 1.113 us | 1.08x | 1.08–1.09x | +0.092 us | 9 pairs; 42/40 ms; 34,472/36,373 ops/lane; IQR 1.006/1.004 | `Pcre2Character` | 3.567 us | 0.491 us | 0.513 us | 2.35x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.781 us | 0.843 us | 0.92x | 0.92–0.93x | -0.064 us | 9 pairs; 40/40 ms; 50,607/48,017 ops/lane; IQR 1.012/1.008 | `Pcre2Slice` | 2.065 us | 0.179 us | 0.220 us | 3.54x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.570 us | 1.087 us | 0.52x | 0.52–0.53x | -0.519 us | 9 pairs; 39/41 ms; 68,449/37,599 ops/lane; IQR 1.014/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.487 us | 0.827 us | 0.59x | 0.59–0.59x | -0.338 us | 9 pairs; 41/41 ms; 83,824/49,182 ops/lane; IQR 1.009/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.644 us | 1.165 us | 0.55x | 0.55–0.55x | -0.521 us | 9 pairs; 37/41 ms; 57,090/35,106 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.913 us | 1.115 us | 0.81x | 0.81–0.82x | -0.206 us | 9 pairs; 41/40 ms; 44,700/36,109 ops/lane; IQR 1.011/1.009 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.537 us | 0.850 us | 0.63x | 0.63–0.64x | -0.313 us | 9 pairs; 40/40 ms; 74,666/46,886 ops/lane; IQR 1.006/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.289 us | — | — | — | — | — | `—` | 25.023 us | 96.199 us | 97.150 us | 0.64x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 3.582 us | — | — | — | — | — | `—` | 1.428 us | 0.349 us | 0.379 us | 9.46x | 1,152 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.046 us | — | — | — | — | — | `—` | 0.893 us | 0.238 us | 0.266 us | 3.93x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.198 us | — | — | — | — | — | `—` | 3.013 us | 0.457 us | 0.494 us | 2.43x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.475 us | — | — | — | — | — | `—` | 1.002 us | 0.537 us | 0.569 us | 2.59x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.664 us | — | — | — | — | — | `—` | 1.475 us | 0.266 us | 0.310 us | 11.82x | 400 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.085 us | 0.081 us | 1.05x | 1.03–1.06x | +0.004 us | 9 pairs; 38/40 ms; 449,409/500,989 ops/lane; IQR 1.007/1.024 | `Pcre2Character` | 0 B | 0 B | 2.288 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.012 us | 0.115 us | 0.11x | 0.11–0.11x | -0.102 us | 9 pairs; 41/40 ms; 3,257,256/349,637 ops/lane; IQR 1.017/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 128.188 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.391 us | 0.103 us | 3.78x | 3.75–3.81x | +0.287 us | 9 pairs; 35/40 ms; 89,636/391,962 ops/lane; IQR 1.009/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 20.981 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.378 us | 0.113 us | 3.35x | 3.32–3.37x | +0.265 us | 9 pairs; 35/40 ms; 91,437/356,712 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 69.681 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Inconclusive** | 3 B | 0.384 us | 0.105 us | 3.70x | 3.57–3.74x | +0.278 us | 9 pairs; 36/42 ms; 92,460/404,743 ops/lane; IQR 1.018/1.030 | `Pcre2Backtracking` | 0 B | 0 B | 71.269 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.644 us | 0.340 us | 1.90x | 1.88–1.91x | +0.304 us | 9 pairs; 37/41 ms; 56,884/119,743 ops/lane; IQR 1.009/1.007 | `Pcre2Character` | 0 B | 0 B | 1.462 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.632 us | 0.02x | 0.02–0.02x | -0.620 us | 9 pairs; 41/41 ms; 3,456,692/65,294 ops/lane; IQR 1.042/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 22.144 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.512 us | 0.02x | 0.02–0.02x | -0.500 us | 9 pairs; 42/40 ms; 3,548,881/78,344 ops/lane; IQR 1.023/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 20.825 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.455 us | 0.110 us | 4.15x | 4.12–4.21x | +0.346 us | 9 pairs; 36/40 ms; 79,712/365,595 ops/lane; IQR 1.003/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 18.806 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.458 us | 10.322 us | 0.82x | 0.82–0.83x | -1.816 us | 9 pairs; 39/38 ms; 4,644/3,688 ops/lane; IQR 1.011/1.011 | `Pcre2Literal` | 0 B | 0 B | 1.188 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.311 us | 1.023 us | 0.30x | 0.30–0.31x | -0.712 us | 9 pairs; 43/39 ms; 136,631/37,779 ops/lane; IQR 1.015/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 127.388 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.305 us | 0.458 us | 0.66x | 0.66–0.68x | -0.152 us | 9 pairs; 32/41 ms; 104,927/89,034 ops/lane; IQR 1.023/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 113.769 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.294 us | 0.458 us | 0.64x | 0.64–0.65x | -0.164 us | 9 pairs; 36/39 ms; 122,561/84,688 ops/lane; IQR 1.002/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 143.338 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.343 us | 0.570 us | 0.60x | 0.58–0.61x | -0.225 us | 9 pairs; 40/39 ms; 115,914/68,705 ops/lane; IQR 1.028/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 100.175 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.280 us | 0.357 us | 0.78x | 0.78–0.79x | -0.077 us | 9 pairs; 38/41 ms; 134,718/113,614 ops/lane; IQR 1.004/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 213.431 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.871 us | 8.643 us | 0.56x | 0.56–0.56x | -3.794 us | 9 pairs; 39/42 ms; 8,070/4,829 ops/lane; IQR 1.009/1.007 | `Pcre2Character` | 0 B | 0 B | 1.469 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.256 us | 0.298 us | 0.86x | 0.85–0.86x | -0.042 us | 9 pairs; 37/38 ms; 143,245/126,658 ops/lane; IQR 1.008/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 91.456 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 0.267 us | 0.398 us | 0.67x | 0.67–0.80x | -0.130 us | 9 pairs; 37/39 ms; 136,722/97,454 ops/lane; IQR 1.123/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 122.219 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.724 us | — | — | — | — | — | `—` | 0 B | — | 14.550 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.194 us | 0.296 us | 0.66x | 0.65–0.66x | -0.101 us | 9 pairs; 41/40 ms; 209,271/136,261 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 52.131 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.630 us | — | — | — | — | — | `—` | 0 B | — | 14.094 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.574 us | 0.658 us | 0.88x | 0.87–0.89x | -0.082 us | 9 pairs; 31/39 ms; 54,374/59,717 ops/lane; IQR 1.011/1.009 | `Pcre2Slice` | 0 B | 0 B | 63.931 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.724 us | 28.025 us | 0.35x | 0.35–0.35x | -18.311 us | 9 pairs; 37/40 ms; 3,806/1,432 ops/lane; IQR 1.009/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.825 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.928 us | 1.051 us | 0.88x | 0.88–0.89x | -0.123 us | 9 pairs; 39/40 ms; 41,809/37,935 ops/lane; IQR 1.016/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 129.463 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.982 us | 1.165 us | 0.85x | 0.84–0.85x | -0.180 us | 9 pairs; 40/41 ms; 40,394/34,898 ops/lane; IQR 1.008/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 114.894 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.956 us | 1.021 us | 0.94x | 0.93–0.94x | -0.064 us | 9 pairs; 41/40 ms; 42,760/38,941 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 141.950 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.943 us | 1.224 us | 0.77x | 0.77–0.78x | -0.277 us | 9 pairs; 39/41 ms; 41,526/33,684 ops/lane; IQR 1.014/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 103.175 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 0.978 us | 0.932 us | 1.04x | 1.04–1.05x | +0.040 us | 9 pairs; 42/41 ms; 43,263/43,784 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 211.713 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.660 us | 8.679 us | 0.66x | 0.65–0.66x | -2.988 us | 9 pairs; 40/39 ms; 6,981/4,465 ops/lane; IQR 1.011/1.015 | `Pcre2Character` | 0 B | 0 B | 1.444 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.900 us | 0.871 us | 1.03x | 1.03–1.04x | +0.027 us | 9 pairs; 39/42 ms; 43,556/48,023 ops/lane; IQR 1.008/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 92.537 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.905 us | 0.962 us | 0.94x | 0.93–0.95x | -0.054 us | 9 pairs; 39/41 ms; 43,153/42,453 ops/lane; IQR 1.015/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 120.494 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.865 us | — | — | — | — | — | `—` | 0 B | — | 12.812 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.826 us | 0.850 us | 0.97x | 0.97–0.97x | -0.025 us | 9 pairs; 41/39 ms; 49,826/45,724 ops/lane; IQR 1.010/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 52.306 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.814 us | — | — | — | — | — | `—` | 0 B | — | 13.619 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.894 us | 1.229 us | 0.73x | 0.73–0.73x | -0.333 us | 9 pairs; 39/42 ms; 43,396/33,983 ops/lane; IQR 1.004/1.006 | `Pcre2Slice` | 0 B | 0 B | 64.806 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.426 us | 2.244 us | 0.64x | 0.64–0.64x | -0.812 us | 9 pairs; 37/39 ms; 25,921/17,501 ops/lane; IQR 1.010/1.009 | `Pcre2Literal` | 0 B | 0 B | 0.831 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.650 us | 1.050 us | 0.62x | 0.61–0.62x | -0.401 us | 9 pairs; 39/39 ms; 60,109/36,886 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 132.194 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.663 us | 1.170 us | 0.57x | 0.56–0.57x | -0.504 us | 9 pairs; 39/40 ms; 59,371/34,494 ops/lane; IQR 1.006/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 113.025 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.600 us | 1.018 us | 0.59x | 0.59–0.59x | -0.414 us | 9 pairs; 38/39 ms; 62,974/38,117 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 144.081 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.615 us | 1.138 us | 0.54x | 0.54–0.54x | -0.522 us | 9 pairs; 40/39 ms; 65,117/34,105 ops/lane; IQR 1.011/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 102.825 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.648 us | 0.940 us | 0.69x | 0.69–0.69x | -0.293 us | 9 pairs; 40/40 ms; 61,311/42,343 ops/lane; IQR 1.010/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 214.363 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.847 us | 2.140 us | 0.86x | 0.86–0.86x | -0.294 us | 9 pairs; 39/39 ms; 21,269/18,276 ops/lane; IQR 1.005/1.006 | `Pcre2Character` | 0 B | 0 B | 1.462 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.629 us | 0.866 us | 0.73x | 0.72–0.73x | -0.237 us | 9 pairs; 39/40 ms; 62,525/46,073 ops/lane; IQR 1.006/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 93.850 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.626 us | 0.968 us | 0.65x | 0.65–0.65x | -0.344 us | 9 pairs; 38/40 ms; 60,781/41,802 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 121.950 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.637 us | — | — | — | — | — | `—` | 0 B | — | 13.713 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.485 us | 0.844 us | 0.57x | 0.57–0.58x | -0.358 us | 9 pairs; 39/40 ms; 79,949/46,822 ops/lane; IQR 1.018/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 53.600 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.529 us | — | — | — | — | — | `—` | 0 B | — | 13.619 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.580 us | 1.227 us | 0.47x | 0.47–0.48x | -0.647 us | 9 pairs; 39/42 ms; 67,694/34,285 ops/lane; IQR 1.011/1.006 | `Pcre2Slice` | 0 B | 0 B | 63.494 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.541 us | — | — | — | — | — | `—` | 480 B | — | 0.825 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.143 us | — | — | — | — | — | `—` | 48 B | — | 128.581 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.240 us | — | — | — | — | — | `—` | 48 B | — | 113.269 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.121 us | — | — | — | — | — | `—` | 48 B | — | 142.050 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.139 us | — | — | — | — | — | `—` | 48 B | — | 98.125 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.127 us | — | — | — | — | — | `—` | 48 B | — | 216.425 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.833 us | — | — | — | — | — | `—` | 64 B | — | 1.400 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.124 us | — | — | — | — | — | `—` | 40 B | — | 93.537 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.127 us | — | — | — | — | — | `—` | 40 B | — | 118.838 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.508 us | — | — | — | — | — | `—` | 1,216 B | — | 13.456 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.057 us | — | — | — | — | — | `—` | 48 B | — | 54.156 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.011 us | — | — | — | — | — | `—` | 928 B | — | 13.488 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.543 us | — | — | — | — | — | `—` | 4,496 B | — | 64.531 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 31.475–248.996 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.034–92.623 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.532–12.393 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.074–49.289 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.034–1.134 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.349–41.731 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.535–21.534 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.075–97.332 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 8.806–71.081 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.153–5.314 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 46.670–356.279 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.067–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.543–296.309 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.883–4.990 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.044–15.123 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 100.762–810.032 us |

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
