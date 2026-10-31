<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `6D9B1B2E09F3AE2285072C0605EB9A8875AF88B9CFCE03BEF2422EC51C2C6D53`
- Latest managed row measurement: `2026-10-31T22:59:15.7954934+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-10-31T22:59:15.7954934+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `37/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `47` managed faster, `1` equivalent, `10` native faster, `42` inconclusive, `1` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `2/2`

Managed rows span more than one measurement environment. Consult the JSON row metadata before interpreting small differences as regressions or wins.

Comparator rows span more than one measurement environment. Consult each row's `PcreNetNativeEnvironment` metadata before comparing small differences.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 908.753 us | 2.455 us | 369.38x | 312.63–396.94x | +906.348 us | 9 pairs; 39/33 ms; 43/13,428 ops/lane; IQR 1.007/1.131 | `Pcre2Slice` | 879.643 us | 0.286 us | 1.086 us | 836.48x | 5,744 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.389 us | 12.223 us | 0.11x | 0.11–0.14x | -10.849 us | 9 pairs; 24/26 ms; 17,216/2,091 ops/lane; IQR 1.172/1.142 | `Pcre2Slice` | 2.778 us | 0.101 us | 3.884 us | 0.36x | 3,872 B | 0 B |
| `common/backtracking` | **Inconclusive** | 49 B | 0.016 us | 1.576 us | 0.01x | 0.01–0.01x | -1.560 us | 9 pairs; 58/50 ms; 3,555,864/32,034 ops/lane; IQR 1.316/1.173 | `Pcre2Slice` | 0.386 us | 1.768 us | 1.835 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Inconclusive** | 47 B | 1.980 us | 0.164 us | 12.05x | 11.66–12.23x | +1.815 us | 9 pairs; 39/40 ms; 19,833/242,712 ops/lane; IQR 1.015/1.008 | `Pcre2Backtracking` | 0.048 us | 0.127 us | 0.162 us | 12.23x | 0 B | 0 B |
| `common/date-miss` | **Inconclusive** | 47 B | 8.302 us | 0.660 us | 12.58x | 11.80–13.15x | +7.642 us | 9 pairs; 50/48 ms; 5,985/72,889 ops/lane; IQR 1.725/1.549 | `Pcre2Backtracking` | 0.120 us | 0.747 us | 0.798 us | 10.40x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.188 us | 0.307 us | 0.61x | 0.61–0.63x | -0.119 us | 9 pairs; 40/39 ms; 214,514/127,614 ops/lane; IQR 1.011/1.006 | `Pcre2Slice` | 0.332 us | 0.517 us | 0.587 us | 0.32x | 0 B | 0 B |
| `common/email-miss` | **Inconclusive** | 35 B | 0.195 us | 0.357 us | 0.54x | 0.53–0.56x | -0.164 us | 9 pairs; 40/40 ms; 207,841/111,754 ops/lane; IQR 1.011/1.022 | `Pcre2Slice` | 0.199 us | 0.449 us | 0.487 us | 0.40x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.135 us | 0.293 us | 0.46x | 0.45–0.46x | -0.158 us | 9 pairs; 40/39 ms; 297,893/132,495 ops/lane; IQR 1.012/1.012 | `Pcre2Slice` | 0.153 us | 0.249 us | 0.271 us | 0.50x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.124 us | 0.315 us | 0.39x | 0.39–0.40x | -0.191 us | 9 pairs; 40/40 ms; 323,169/127,182 ops/lane; IQR 1.007/1.011 | `Pcre2Slice` | 0.259 us | 0.403 us | 0.458 us | 0.27x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.182 us | 0.080 us | 2.42x | 2.24–2.57x | +0.107 us | 9 pairs; 40/27 ms; 222,133/336,731 ops/lane; IQR 1.541/1.403 | `Pcre2Slice` | 0.222 us | 0.976 us | 1.013 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.069 us | 0.204 us | 0.34x | 0.34–0.34x | -0.135 us | 9 pairs; 39/40 ms; 572,215/194,697 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0.318 us | 0.516 us | 0.618 us | 0.11x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.189 us | 0.916 us | 0.21x | 0.20–0.21x | -0.728 us | 9 pairs; 30/37 ms; 160,039/40,301 ops/lane; IQR 1.023/1.022 | `Pcre2Backtracking` | 0.209 us | 0.225 us | 0.255 us | 0.74x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.156 us | 0.22x | 0.21–0.22x | -0.122 us | 9 pairs; 39/39 ms; 1,151,659/251,695 ops/lane; IQR 1.026/1.014 | `Pcre2Backtracking` | 0.186 us | 0.300 us | 0.357 us | 0.09x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Inconclusive** | 10 B | 0.022 us | 0.114 us | 0.19x | 0.18–0.20x | -0.093 us | 9 pairs; 42/40 ms; 1,920,117/353,082 ops/lane; IQR 1.221/1.172 | `Pcre2Backtracking` | 0.077 us | 0.093 us | 0.114 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.120 us | 0.49x | 0.48–0.49x | -0.062 us | 9 pairs; 40/42 ms; 692,367/346,825 ops/lane; IQR 1.005/1.014 | `Pcre2Backtracking` | 0.017 us | 0.091 us | 0.114 us | 0.51x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.199 us | 0.261 us | 0.77x | 0.75–0.77x | -0.061 us | 9 pairs; 40/42 ms; 201,320/162,530 ops/lane; IQR 1.009/1.013 | `Pcre2Slice` | 0.214 us | 0.125 us | 0.157 us | 1.27x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.026 us | 0.115 us | 0.22x | 0.22–0.23x | -0.089 us | 9 pairs; 40/41 ms; 1,561,450/358,669 ops/lane; IQR 1.010/1.011 | `Pcre2Backtracking` | 0.029 us | 0.149 us | 0.189 us | 0.14x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.177 us | 2.000 us | 0.09x | 0.08–0.09x | -1.823 us | 9 pairs; 40/44 ms; 227,133/22,106 ops/lane; IQR 1.009/1.095 | `Pcre2Literal` | 0.125 us | 0.054 us | 0.612 us | 0.29x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.178 us | 2.067 us | 0.09x | 0.08–0.10x | -1.889 us | 9 pairs; 40/42 ms; 226,220/20,358 ops/lane; IQR 1.004/1.119 | `Pcre2Literal` | 0.385 us | 0.080 us | 0.682 us | 0.26x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.218 us | 2.006 us | 0.11x | 0.10–0.11x | -1.787 us | 9 pairs; 36/32 ms; 163,521/15,960 ops/lane; IQR 1.004/1.089 | `Pcre2Literal` | 0.276 us | 0.241 us | 0.838 us | 0.26x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.210 us | 1.880 us | 0.11x | 0.10–0.12x | -1.673 us | 9 pairs; 33/40 ms; 159,688/21,221 ops/lane; IQR 1.010/1.074 | `Pcre2Literal` | 0.220 us | 0.211 us | 0.792 us | 0.26x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.101 us | 0.09x | 0.09–0.09x | -0.092 us | 9 pairs; 40/39 ms; 4,510,974/391,120 ops/lane; IQR 1.002/1.004 | `Pcre2Slice` | 1.106 us | 0.076 us | 0.134 us | 0.07x | 0 B | 0 B |
| `simple/foo-dense` | **Native faster** | 18 B | 0.222 us | 0.139 us | 1.60x | 1.58–1.69x | +0.083 us | 9 pairs; 39/39 ms; 174,298/277,310 ops/lane; IQR 1.063/1.007 | `Pcre2Literal` | 0.143 us | 0.054 us | 0.074 us | 3.01x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Inconclusive** | 23 B | 0.023 us | 0.178 us | 0.13x | 0.13–0.14x | -0.154 us | 9 pairs; 37/37 ms; 1,630,108/207,469 ops/lane; IQR 1.226/1.342 | `Pcre2Backtracking` | 0.512 us | 0.148 us | 0.202 us | 0.11x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 0.395 us | 0.188 us | 2.14x | 2.11–2.32x | +0.205 us | 9 pairs; 46/63 ms; 115,709/332,333 ops/lane; IQR 1.396/1.508 | `Pcre2Character` | 0.277 us | 0.188 us | 0.259 us | 1.52x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.076 us | 0.204 us | 0.37x | 0.36–0.38x | -0.128 us | 9 pairs; 41/39 ms; 541,495/193,481 ops/lane; IQR 1.001/1.021 | `Pcre2Slice` | 1.345 us | 0.122 us | 0.198 us | 0.38x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 114.513 us | 238.800 us | 0.48x | 0.48–0.48x | -123.916 us | 9 pairs; 41/42 ms; 358/176 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 412.389 us | 106.995 us | 109.000 us | 1.05x | 0 B | 0 B |
| `character/unicode-class-dense` | **Inconclusive** | 7,168 B | 53.339 us | 431.841 us | 0.13x | 0.12–0.13x | -378.634 us | 9 pairs; 37/39 ms; 693/91 ops/lane; IQR 1.162/1.215 | `Pcre2Character` | 432.749 us | 219.549 us | 230.150 us | 0.23x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.484 us | 210.057 us | 0.05x | 0.04–0.05x | -200.506 us | 9 pairs; 40/40 ms; 4,175/191 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 7.996 us | 223.388 us | 225.923 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 583.204 us | 217.629 us | 2.68x | 2.67–2.71x | +365.598 us | 9 pairs; 40/40 ms; 68/185 ops/lane; IQR 1.003/1.007 | `Pcre2SingleTokenRepeat` | 7.967 us | 230.027 us | 232.434 us | 2.51x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.573 us | 23.854 us | 0.15x | 0.15–0.15x | -20.288 us | 9 pairs; 40/39 ms; 11,103/1,636 ops/lane; IQR 1.003/1.018 | `Pcre2Literal` | 2.343 us | 4.270 us | 6.160 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.598 us | 82.358 us | 0.12x | 0.12–0.12x | -72.705 us | 9 pairs; 34/41 ms; 3,573/497 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 9.412 us | 90.117 us | 91.973 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 5,622.850 us | 42,881.100 us | 0.13x | 0.13–0.13x | -37225.800 us | 9 pairs; 34/43 ms; 6/1 ops/lane; IQR 1.025/1.009 | `Pcre2Backtracking` | 8,194.400 us | 17,366.500 us | 25,427.400 us | 0.22x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 8,785.140 us | 75,026.000 us | 0.11x | 0.10–0.12x | -65690.280 us | 9 pairs; 44/75 ms; 5/1 ops/lane; IQR 1.146/1.065 | `Pcre2Backtracking` | 6,921.100 us | 40,932.500 us | 48,072.000 us | 0.18x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 2,362.562 us | 518,959.100 us | 0.00x | 0.00–0.00x | -516860.562 us | 9 pairs; 50/519 ms; 21/1 ops/lane; IQR 1.227/1.162 | `Pcre2Character` | 2,460.919 us | 39,263.000 us | 48,408.500 us | 0.05x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,550.533 us | 11,751.000 us | 0.47x | 0.44–0.51x | -6222.867 us | 9 pairs; 33/47 ms; 6/4 ops/lane; IQR 1.007/1.104 | `Pcre2Literal` | 1,524.111 us | 1,360.558 us | 8,566.317 us | 0.65x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Inconclusive** | 6,839,410 B | 18,926.950 us | 146,690.600 us | 0.13x | 0.13–0.13x | -127740.200 us | 9 pairs; 38/147 ms; 2/1 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 1,602.373 us | 510.694 us | 8,464.300 us | 2.24x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,869.514 us | 21,979.600 us | 0.22x | 0.22–0.22x | -17123.186 us | 9 pairs; 34/44 ms; 7/2 ops/lane; IQR 1.009/1.011 | `Pcre2Slice` | 2,886.894 us | 10,950.860 us | 18,931.367 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Inconclusive** | 6,839,410 B | 23,552.150 us | 135,171.200 us | 0.17x | 0.17–0.18x | -110828.700 us | 9 pairs; 47/135 ms; 2/1 ops/lane; IQR 1.008/1.013 | `Pcre2Backtracking` | 1,934.352 us | 2,361.891 us | 10,486.420 us | 2.25x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Inconclusive** | 594,930 B | 250.746 us | 1,208.088 us | 0.21x | 0.20–0.21x | -957.219 us | 9 pairs; 40/50 ms; 158/41 ops/lane; IQR 1.003/1.023 | `Pcre2Backtracking` | 298.341 us | 319.412 us | 540.886 us | 0.46x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,799.567 us | 13,492.300 us | 0.95x | 0.95–0.95x | -694.067 us | 9 pairs; 38/40 ms; 3/3 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 313.746 us | 15,421.850 us | 15,595.075 us | 0.82x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Inconclusive** | 594,930 B | 3,528.209 us | 110,066.200 us | 0.03x | 0.02–0.06x | -106537.991 us | 9 pairs; 39/110 ms; 11/1 ops/lane; IQR 1.274/1.321 | `Pcre2Character` | 64,775.600 us | 19,973.550 us | 20,208.650 us | 0.17x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.508 us | 2,748.650 us | 0.07x | 0.07–0.07x | -2563.684 us | 9 pairs; 39/38 ms; 212/14 ops/lane; IQR 1.003/1.008 | `Pcre2SingleTokenRepeat` | 10,637.580 us | 993.874 us | 1,275.868 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,643.617 us | 20,403.450 us | 0.08x | 0.08–0.08x | -18764.420 us | 9 pairs; 38/41 ms; 23/2 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 599.512 us | 10,382.475 us | 10,775.100 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.139 us | 1.102 us | 0.13x | 0.13–0.13x | -0.963 us | 9 pairs; 40/40 ms; 287,492/36,280 ops/lane; IQR 1.004/1.003 | `Pcre2Slice` | 2.434 us | 0.409 us | 0.460 us | 0.30x | 0 B | 0 B |
| `simple/foo-dense` | **Inconclusive** | 18 B | 0.506 us | 0.831 us | 0.61x | 0.59–0.61x | -0.324 us | 9 pairs; 41/40 ms; 80,888/48,036 ops/lane; IQR 1.006/1.003 | `Pcre2Literal` | 0.145 us | 0.149 us | 0.173 us | 2.93x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.491 us | 1.654 us | 0.30x | 0.29–0.30x | -1.162 us | 9 pairs; 39/40 ms; 79,821/24,296 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 1.063 us | 0.346 us | 0.374 us | 1.31x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 2.197 us | 1.557 us | 1.41x | 1.39–1.42x | +0.631 us | 9 pairs; 38/38 ms; 17,198/24,149 ops/lane; IQR 1.028/1.018 | `Pcre2Character` | 0.286 us | 0.424 us | 0.468 us | 4.69x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.155 us | 0.857 us | 0.18x | 0.18–0.18x | -0.701 us | 9 pairs; 40/41 ms; 258,247/47,497 ops/lane; IQR 1.004/1.005 | `Pcre2Slice` | 1.296 us | 0.157 us | 0.204 us | 0.76x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Native faster** | 16 B | 1.621 us | 1.548 us | 1.04x | 1.03–1.05x | +0.064 us | 9 pairs; 41/39 ms; 25,066/25,386 ops/lane; IQR 1.010/1.023 | `Pcre2Slice` | 6.462 us | 0.309 us | 0.323 us | 5.02x | 0 B | 0 B |
| `simple/foo-dense` | **Equivalent** | 18 B | 0.848 us | 0.838 us | 1.01x | 1.01–1.02x | +0.011 us | 9 pairs; 40/40 ms; 46,666/47,864 ops/lane; IQR 1.008/1.001 | `Pcre2Literal` | 3.600 us | 0.181 us | 0.203 us | 4.17x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.027 us | 1.195 us | 0.86x | 0.85–0.95x | -0.166 us | 9 pairs; 41/40 ms; 39,459/33,684 ops/lane; IQR 1.004/1.047 | `Pcre2Backtracking` | 7.571 us | 0.645 us | 0.698 us | 1.47x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 2.809 us | 1.590 us | 1.77x | 1.71–1.80x | +1.218 us | 9 pairs; 39/42 ms; 14,007/26,239 ops/lane; IQR 1.013/1.009 | `Pcre2Character` | 4.614 us | 0.758 us | 0.840 us | 3.35x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.839 us | 0.867 us | 0.97x | 0.96–0.97x | -0.029 us | 9 pairs; 41/40 ms; 49,387/46,393 ops/lane; IQR 1.003/1.004 | `Pcre2Slice` | 4.509 us | 0.287 us | 0.361 us | 2.32x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.987 us | 1.571 us | 0.63x | 0.63–0.64x | -0.584 us | 9 pairs; 38/41 ms; 38,959/26,026 ops/lane; IQR 1.008/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.516 us | 0.837 us | 0.62x | 0.61–0.62x | -0.321 us | 9 pairs; 39/40 ms; 74,700/48,038 ops/lane; IQR 1.012/1.002 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.089 us | 1.636 us | 0.66x | 0.65–0.67x | -0.554 us | 9 pairs; 40/39 ms; 36,312/23,841 ops/lane; IQR 1.004/1.009 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.417 us | 1.148 us | 1.23x | 1.23–1.24x | +0.269 us | 9 pairs; 40/40 ms; 28,003/34,482 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.484 us | 0.867 us | 0.56x | 0.56–0.56x | -0.383 us | 9 pairs; 41/40 ms; 83,735/46,260 ops/lane; IQR 1.004/1.002 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 65.328 us | — | — | — | — | — | `—` | 73.079 us | 96.994 us | 99.244 us | 0.66x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 10.384 us | — | — | — | — | — | `—` | 3.552 us | 0.369 us | 0.398 us | 26.11x | 48,792 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.131 us | — | — | — | — | — | `—` | 0.904 us | 0.248 us | 0.280 us | 4.04x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.275 us | — | — | — | — | — | `—` | 3.863 us | 0.464 us | 0.500 us | 2.55x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 2.146 us | — | — | — | — | — | `—` | 1.016 us | 0.547 us | 0.595 us | 3.61x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 9.560 us | — | — | — | — | — | `—` | 3.118 us | 0.502 us | 0.598 us | 15.97x | 7,192 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.199 us | 0.088 us | 2.27x | 2.20–2.27x | +0.110 us | 9 pairs; 40/29 ms; 200,429/331,539 ops/lane; IQR 1.015/1.025 | `Pcre2Character` | 0 B | 0 B | 3.862 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Inconclusive** | 23 B | 0.017 us | 0.165 us | 0.11x | 0.11–0.12x | -0.148 us | 9 pairs; 52/55 ms; 3,065,892/331,535 ops/lane; IQR 1.337/1.284 | `Pcre2Backtracking` | 0 B | 0 B | 131.812 us | 111,888 B |
| `pcre2/conditional-accept-negative-lookahead` | **Inconclusive** | 3 B | 0.576 us | 0.127 us | 4.53x | 4.44–4.97x | +0.449 us | 9 pairs; 43/48 ms; 74,830/378,057 ops/lane; IQR 1.431/1.351 | `Pcre2Backtracking` | 0 B | 0 B | 127.831 us | 19,377 B |
| `pcre2/conditional-lookahead` | **Inconclusive** | 3 B | 0.480 us | 0.121 us | 3.99x | 3.92–4.01x | +0.358 us | 9 pairs; 36/40 ms; 75,433/327,549 ops/lane; IQR 1.011/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 341.100 us | 65,153 B |
| `pcre2/conditional-negative-lookahead` | **Inconclusive** | 3 B | 0.712 us | 0.153 us | 4.69x | 4.61–4.73x | +0.558 us | 9 pairs; 46/47 ms; 64,285/306,896 ops/lane; IQR 1.187/1.174 | `Pcre2Backtracking` | 0 B | 0 B | 465.006 us | 65,113 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.655 us | 0.347 us | 1.89x | 1.88–1.90x | +0.310 us | 9 pairs; 30/30 ms; 45,072/86,557 ops/lane; IQR 1.011/1.011 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.634 us | 0.02x | 0.02–0.02x | -0.623 us | 9 pairs; 40/39 ms; 3,477,018/62,155 ops/lane; IQR 1.011/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 23.650 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.012 us | 0.505 us | 0.02x | 0.02–0.03x | -0.493 us | 9 pairs; 25/27 ms; 2,009,738/53,881 ops/lane; IQR 1.364/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 22.506 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Inconclusive** | 5 B | 0.549 us | 0.109 us | 5.03x | 4.97–5.30x | +0.437 us | 9 pairs; 38/40 ms; 69,081/365,470 ops/lane; IQR 1.115/1.051 | `Pcre2Backtracking` | 0 B | 0 B | 81.394 us | 14,113 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Inconclusive** | 320 B | 13.604 us | 16.053 us | 0.83x | 0.80–0.84x | -2.530 us | 9 pairs; 62/59 ms; 4,591/3,705 ops/lane; IQR 1.210/1.221 | `Pcre2Literal` | 0 B | 0 B | 1.163 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.310 us | 1.048 us | 0.30x | 0.29–0.30x | -0.738 us | 9 pairs; 29/32 ms; 91,978/30,534 ops/lane; IQR 1.011/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 232.812 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Inconclusive** | 17 B | 0.367 us | 0.468 us | 0.69x | 0.62–0.87x | -0.145 us | 9 pairs; 40/39 ms; 109,491/82,789 ops/lane; IQR 1.158/1.214 | `Pcre2Backtracking` | 0 B | 0 B | 119.400 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Inconclusive** | 31 B | 0.428 us | 0.726 us | 0.59x | 0.58–0.66x | -0.297 us | 9 pairs; 33/36 ms; 77,510/49,884 ops/lane; IQR 1.442/1.648 | `Pcre2Backtracking` | 0 B | 0 B | 241.844 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.333 us | 0.609 us | 0.55x | 0.55–0.55x | -0.276 us | 9 pairs; 40/42 ms; 118,530/68,133 ops/lane; IQR 1.011/1.021 | `Pcre2Backtracking` | 0 B | 0 B | 104.931 us | 81,520 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.308 us | 0.398 us | 0.78x | 0.74–0.78x | -0.087 us | 9 pairs; 37/39 ms; 121,243/97,329 ops/lane; IQR 1.013/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 213.981 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Inconclusive** | 248 B | 8.300 us | 11.719 us | 0.69x | 0.58–0.73x | -3.639 us | 9 pairs; 36/37 ms; 4,350/3,118 ops/lane; IQR 1.770/1.422 | `Pcre2Character` | 0 B | 0 B | 1.494 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.267 us | 0.321 us | 0.83x | 0.83–0.83x | -0.054 us | 9 pairs; 37/39 ms; 137,055/122,665 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 96.106 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.387 us | 0.637 us | 0.61x | 0.61–0.62x | -0.245 us | 9 pairs; 35/38 ms; 89,773/59,635 ops/lane; IQR 1.011/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 204.488 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.235 us | — | — | — | — | — | `—` | 0 B | — | 14.319 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.199 us | 0.294 us | 0.68x | 0.67–0.69x | -0.095 us | 9 pairs; 40/40 ms; 201,859/136,182 ops/lane; IQR 1.014/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 54.619 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 7.321 us | — | — | — | — | — | `—` | 0 B | — | 14.594 us | 10,536 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 0.538 us | 0.787 us | 0.68x | 0.66–0.69x | -0.251 us | 9 pairs; 20/27 ms; 37,474/33,766 ops/lane; IQR 1.013/1.018 | `Pcre2Slice` | 0 B | 0 B | 64.631 us | 55,504 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.824 us | 28.991 us | 0.34x | 0.34–0.34x | -19.177 us | 9 pairs; 40/40 ms; 4,031/1,380 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.787 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 1.021 us | 1.079 us | 0.94x | 0.93–0.95x | -0.062 us | 9 pairs; 40/41 ms; 39,548/37,703 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 134.512 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.019 us | 1.203 us | 0.85x | 0.84–0.85x | -0.183 us | 9 pairs; 40/40 ms; 39,194/33,213 ops/lane; IQR 1.002/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 114.581 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Native faster** | 31 B | 1.571 us | 1.480 us | 1.06x | 1.05–1.07x | +0.090 us | 9 pairs; 40/41 ms; 25,325/27,577 ops/lane; IQR 1.005/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 143.231 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 1.068 us | 1.230 us | 0.87x | 0.85–0.91x | -0.161 us | 9 pairs; 40/40 ms; 37,401/32,631 ops/lane; IQR 1.026/1.058 | `Pcre2Backtracking` | 0 B | 0 B | 105.125 us | 81,520 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 1.546 us | 1.356 us | 1.13x | 1.07–1.16x | +0.182 us | 9 pairs; 41/41 ms; 26,671/30,195 ops/lane; IQR 1.150/1.085 | `Pcre2Backtracking` | 0 B | 0 B | 215.856 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 9.974 us | 12.508 us | 0.80x | 0.79–0.81x | -2.537 us | 9 pairs; 39/39 ms; 3,909/3,098 ops/lane; IQR 1.009/1.007 | `Pcre2Character` | 0 B | 0 B | 2.288 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Inconclusive** | 13 B | 0.937 us | 0.902 us | 1.05x | 1.04–1.26x | +0.041 us | 9 pairs; 25/29 ms; 26,310/31,956 ops/lane; IQR 1.657/1.372 | `Pcre2Backtracking` | 0 B | 0 B | 94.237 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 1.571 us | 1.364 us | 1.12x | 1.05–1.16x | +0.149 us | 9 pairs; 60/57 ms; 38,026/41,627 ops/lane; IQR 1.135/1.108 | `Pcre2Backtracking` | 0 B | 0 B | 125.513 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.313 us | — | — | — | — | — | `—` | 0 B | — | 14.488 us | 11,496 B |
| `pcre2/kreset-global` | **Native faster** | 26 B | 1.456 us | 1.230 us | 1.18x | 1.16–1.20x | +0.222 us | 9 pairs; 68/57 ms; 47,026/45,957 ops/lane; IQR 1.014/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 76.800 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.265 us | — | — | — | — | — | `—` | 0 B | — | 15.144 us | 10,536 B |
| `pcre2/same-start-global` | **Inconclusive** | 9 B | 1.501 us | 1.765 us | 0.84x | 0.73–0.85x | -0.268 us | 9 pairs; 69/56 ms; 46,060/31,505 ops/lane; IQR 1.214/1.174 | `Pcre2Slice` | 0 B | 0 B | 106.688 us | 55,504 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.397 us | 2.296 us | 0.61x | 0.60–0.61x | -0.900 us | 9 pairs; 41/40 ms; 29,076/17,530 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 1.119 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.640 us | 1.078 us | 0.59x | 0.59–0.60x | -0.437 us | 9 pairs; 37/38 ms; 58,509/35,070 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 138.319 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.687 us | 1.206 us | 0.57x | 0.57–0.57x | -0.518 us | 9 pairs; 41/41 ms; 59,080/33,956 ops/lane; IQR 1.003/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 183.794 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.926 us | 1.485 us | 0.62x | 0.62–0.63x | -0.560 us | 9 pairs; 41/36 ms; 44,203/24,065 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 145.688 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Inconclusive** | 22 B | 0.730 us | 1.368 us | 0.54x | 0.52–0.57x | -0.649 us | 9 pairs; 31/33 ms; 41,990/24,003 ops/lane; IQR 1.499/1.383 | `Pcre2Backtracking` | 0 B | 0 B | 161.575 us | 81,520 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.622 us | 0.959 us | 0.65x | 0.64–0.65x | -0.337 us | 9 pairs; 26/29 ms; 42,607/30,458 ops/lane; IQR 1.078/1.078 | `Pcre2Backtracking` | 0 B | 0 B | 217.688 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Inconclusive** | 248 B | 3.024 us | 3.124 us | 0.97x | 0.86–0.98x | -0.100 us | 9 pairs; 65/57 ms; 21,432/18,249 ops/lane; IQR 1.093/1.117 | `Pcre2Character` | 0 B | 0 B | 1.356 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.607 us | 0.896 us | 0.68x | 0.68–0.68x | -0.291 us | 9 pairs; 40/40 ms; 65,460/45,122 ops/lane; IQR 1.001/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 149.200 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.615 us | 0.966 us | 0.64x | 0.64–0.67x | -0.352 us | 9 pairs; 25/28 ms; 40,866/29,362 ops/lane; IQR 1.067/1.063 | `Pcre2Backtracking` | 0 B | 0 B | 204.350 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.142 us | — | — | — | — | — | `—` | 0 B | — | 14.931 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.771 us | 1.260 us | 0.61x | 0.61–0.62x | -0.488 us | 9 pairs; 38/38 ms; 49,072/30,110 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 54.013 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.028 us | — | — | — | — | — | `—` | 0 B | — | 15.062 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.585 us | 1.262 us | 0.47x | 0.46–0.48x | -0.674 us | 9 pairs; 23/27 ms; 39,322/21,735 ops/lane; IQR 1.039/1.004 | `Pcre2Slice` | 0 B | 0 B | 63.612 us | 55,504 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 19.043 us | — | — | — | — | — | `—` | 480 B | — | 1.075 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.348 us | — | — | — | — | — | `—` | 48 B | — | 134.119 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.301 us | — | — | — | — | — | `—` | 48 B | — | 118.844 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.941 us | — | — | — | — | — | `—` | 48 B | — | 248.325 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.206 us | — | — | — | — | — | `—` | 48 B | — | 103.463 us | 81,520 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.169 us | — | — | — | — | — | `—` | 48 B | — | 366.087 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.567 us | — | — | — | — | — | `—` | 64 B | — | 1.825 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.185 us | — | — | — | — | — | `—` | 40 B | — | 96.856 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.173 us | — | — | — | — | — | `—` | 40 B | — | 123.956 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 23.352 us | — | — | — | — | — | `—` | 1,224 B | — | 15.600 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.118 us | — | — | — | — | — | `—` | 48 B | — | 53.675 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 17.503 us | — | — | — | — | — | `—` | 936 B | — | 15.688 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 10.300 us | — | — | — | — | — | `—` | 28,936 B | — | 64.506 us | 55,504 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.846–214.914 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 16.208–95.564 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.096–19.913 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.276–48.888 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.037–1.508 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.552–43.585 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.778–29.831 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 7.401–110.629 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.137–79.620 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.842–9.160 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.712–382.133 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.130–0.168 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 42.360–331.942 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 1.349–5.109 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.719–141.685 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 283.080–1,346.667 us |

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
