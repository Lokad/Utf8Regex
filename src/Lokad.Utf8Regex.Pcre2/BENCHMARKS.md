<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `C3E6CFBCD205126712CF3F9DE8243BB5ED2B379CF81F85EF0926B32D899EC089`
- Latest managed row measurement: `2026-11-02T03:53:37.6022082+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-02T03:53:37.6022082+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `41/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `76` managed faster, `0` equivalent, `13` native faster, `11` inconclusive, `1` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `2c6cb298c484` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `2c6cb298c484` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.887 us | 2.279 us | 0.39x | 0.37–0.43x | -1.392 us | 9 pairs; 34/47 ms; 37,805/20,572 ops/lane; IQR 1.052/1.161 | `Pcre2Slice` | 514.127 us | 0.282 us | 0.836 us | 1.06x | 4,128 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.102 us | 13.382 us | 0.08x | 0.08–0.10x | -12.289 us | 9 pairs; 36/36 ms; 32,261/2,705 ops/lane; IQR 1.036/1.170 | `Pcre2Slice` | 1.750 us | 0.097 us | 3.849 us | 0.29x | 2,256 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.985 us | 0.01x | 0.01–0.01x | -1.974 us | 9 pairs; 27/39 ms; 2,432,891/19,684 ops/lane; IQR 1.017/1.006 | `Pcre2Slice` | 0.210 us | 1.092 us | 1.137 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.518 us | 0.164 us | 9.27x | 9.23–9.33x | +1.354 us | 9 pairs; 38/43 ms; 25,354/260,259 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0.047 us | 0.123 us | 0.162 us | 9.35x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 5.258 us | 0.576 us | 9.11x | 9.06–9.15x | +4.683 us | 9 pairs; 40/41 ms; 7,560/71,731 ops/lane; IQR 1.003/1.007 | `Pcre2Backtracking` | 0.130 us | 0.733 us | 0.755 us | 6.96x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.175 us | 0.294 us | 0.59x | 0.59–0.60x | -0.120 us | 9 pairs; 40/39 ms; 227,492/134,229 ops/lane; IQR 1.007/1.021 | `Pcre2Slice` | 0.178 us | 0.316 us | 0.347 us | 0.50x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.184 us | 0.359 us | 0.51x | 0.51–0.51x | -0.175 us | 9 pairs; 41/41 ms; 220,309/113,797 ops/lane; IQR 1.007/1.010 | `Pcre2Slice` | 0.184 us | 0.442 us | 0.465 us | 0.40x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.121 us | 0.291 us | 0.42x | 0.41–0.42x | -0.171 us | 9 pairs; 40/40 ms; 329,012/138,955 ops/lane; IQR 1.004/1.009 | `Pcre2Slice` | 0.133 us | 0.236 us | 0.256 us | 0.47x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.115 us | 0.312 us | 0.37x | 0.36–0.37x | -0.198 us | 9 pairs; 40/38 ms; 346,541/122,537 ops/lane; IQR 1.001/1.010 | `Pcre2Slice` | 0.132 us | 0.253 us | 0.273 us | 0.42x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.178 us | 0.073 us | 2.42x | 2.26–2.49x | +0.104 us | 9 pairs; 41/37 ms; 231,554/499,801 ops/lane; IQR 1.008/1.042 | `Pcre2Slice` | 0.209 us | 0.951 us | 0.981 us | 0.18x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.203 us | 0.16x | 0.16–0.17x | -0.169 us | 9 pairs; 39/41 ms; 1,174,335/202,578 ops/lane; IQR 1.005/1.004 | `Pcre2Backtracking` | 0.203 us | 0.306 us | 0.346 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.061 us | 0.617 us | 0.10x | 0.10–0.10x | -0.556 us | 9 pairs; 40/40 ms; 655,250/65,181 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 0.192 us | 0.237 us | 0.257 us | 0.24x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.034 us | 0.155 us | 0.22x | 0.22–0.22x | -0.122 us | 9 pairs; 38/40 ms; 1,142,311/257,949 ops/lane; IQR 1.019/1.004 | `Pcre2Backtracking` | 0.108 us | 0.187 us | 0.205 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.111 us | 0.18x | 0.18–0.18x | -0.091 us | 9 pairs; 39/39 ms; 1,948,085/352,517 ops/lane; IQR 1.017/1.005 | `Pcre2Backtracking` | 0.064 us | 0.091 us | 0.109 us | 0.18x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.112 us | 0.52x | 0.51–0.52x | -0.054 us | 9 pairs; 40/39 ms; 689,366/344,694 ops/lane; IQR 1.006/1.012 | `Pcre2Backtracking` | 0.015 us | 0.087 us | 0.103 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Inconclusive** | 67 B | 0.191 us | 0.275 us | 0.69x | 0.69–0.79x | -0.084 us | 9 pairs; 26/38 ms; 135,137/137,157 ops/lane; IQR 1.134/1.006 | `Pcre2Slice` | 0.205 us | 0.118 us | 0.153 us | 1.24x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.117 us | 0.20x | 0.20–0.20x | -0.093 us | 9 pairs; 40/41 ms; 1,678,115/346,793 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0.020 us | 0.093 us | 0.110 us | 0.22x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.120 us | 2.260 us | 0.05x | 0.05–0.06x | -2.139 us | 9 pairs; 42/42 ms; 350,278/18,675 ops/lane; IQR 1.004/1.257 | `Pcre2Literal` | 0.120 us | 0.050 us | 0.622 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.117 us | 1.824 us | 0.06x | 0.05–0.07x | -1.707 us | 9 pairs; 40/33 ms; 340,619/17,936 ops/lane; IQR 1.004/1.110 | `Pcre2Literal` | 0.179 us | 0.054 us | 0.619 us | 0.19x | 0 B | 0 B |
| `literal/late` | **Managed faster** | 4,103 B | 0.144 us | 1.943 us | 0.07x | 0.07–0.08x | -1.799 us | 9 pairs; 40/42 ms; 278,532/21,530 ops/lane; IQR 1.002/1.036 | `Pcre2Literal` | 0.285 us | 0.233 us | 0.826 us | 0.17x | 0 B | 0 B |
| `literal/missing` | **Managed faster** | 4,096 B | 0.143 us | 2.025 us | 0.07x | 0.07–0.07x | -1.882 us | 9 pairs; 39/36 ms; 274,997/17,607 ops/lane; IQR 1.008/1.093 | `Pcre2Literal` | 0.216 us | 0.205 us | 0.778 us | 0.18x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.091 us | 9 pairs; 38/38 ms; 4,372,887/379,212 ops/lane; IQR 1.002/1.015 | `Pcre2Slice` | 0.938 us | 0.071 us | 0.090 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Inconclusive** | 18 B | 0.063 us | 0.088 us | 0.72x | 0.70–0.73x | -0.025 us | 9 pairs; 40/39 ms; 636,684/446,416 ops/lane; IQR 1.008/1.010 | `Pcre2Literal` | 0.122 us | 0.051 us | 0.073 us | 0.86x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.014 us | 0.110 us | 0.12x | 0.12–0.12x | -0.097 us | 9 pairs; 39/40 ms; 2,870,295/361,040 ops/lane; IQR 1.008/1.008 | `Pcre2Backtracking` | 0.246 us | 0.091 us | 0.114 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.111 us | 0.124 us | 0.90x | 0.89–0.94x | -0.012 us | 9 pairs; 34/40 ms; 306,004/319,482 ops/lane; IQR 1.072/1.026 | `Pcre2Character` | 0.138 us | 0.113 us | 0.144 us | 0.77x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.085 us | 0.199 us | 0.43x | 0.43–0.43x | -0.114 us | 9 pairs; 43/40 ms; 503,524/198,829 ops/lane; IQR 1.011/1.018 | `Pcre2Slice` | 0.574 us | 0.076 us | 0.107 us | 0.79x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 78.795 us | 162.669 us | 0.48x | 0.48–0.49x | -84.010 us | 9 pairs; 40/40 ms; 509/248 ops/lane; IQR 1.002/1.008 | `Pcre2Slice` | 405.341 us | 102.379 us | 102.527 us | 0.77x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.665 us | 282.486 us | 0.13x | 0.13–0.13x | -244.759 us | 9 pairs; 40/39 ms; 1,067/138 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 386.115 us | 217.981 us | 226.939 us | 0.17x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.894 us | 211.094 us | 0.05x | 0.05–0.05x | -201.156 us | 9 pairs; 40/40 ms; 4,004/191 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 8.198 us | 215.123 us | 218.832 us | 0.05x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 240.692 us | 205.406 us | 1.17x | 1.16–1.18x | +34.695 us | 9 pairs; 41/37 ms; 169/182 ops/lane; IQR 1.001/1.010 | `Pcre2SingleTokenRepeat` | 8.190 us | 227.783 us | 229.485 us | 1.05x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.567 us | 23.909 us | 0.15x | 0.15–0.15x | -20.325 us | 9 pairs; 40/39 ms; 11,086/1,635 ops/lane; IQR 1.012/1.022 | `Pcre2Literal` | 1.443 us | 4.279 us | 6.045 us | 0.59x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.578 us | 81.502 us | 0.12x | 0.12–0.12x | -71.939 us | 9 pairs; 40/40 ms; 4,182/494 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 9.421 us | 91.735 us | 94.364 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 4,142.262 us | 28,600.500 us | 0.14x | 0.14–0.15x | -24479.788 us | 9 pairs; 33/29 ms; 8/1 ops/lane; IQR 1.014/1.061 | `Pcre2Backtracking` | 5,594.011 us | 11,530.240 us | 18,299.933 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 5,939.157 us | 54,606.500 us | 0.11x | 0.11–0.11x | -48572.514 us | 9 pairs; 42/55 ms; 7/1 ops/lane; IQR 1.029/1.027 | `Pcre2Backtracking` | 6,425.387 us | 41,475.900 us | 47,496.950 us | 0.13x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,953.890 us | 490,754.000 us | 0.00x | 0.00–0.00x | -488797.119 us | 9 pairs; 41/491 ms; 21/1 ops/lane; IQR 1.038/1.004 | `Pcre2Character` | 1,794.328 us | 24,191.600 us | 30,296.350 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Managed faster** | 16,013,977 B | 5,537.333 us | 11,054.833 us | 0.50x | 0.47–0.52x | -5547.983 us | 9 pairs; 33/33 ms; 6/3 ops/lane; IQR 1.006/1.036 | `Pcre2Literal` | 1,405.858 us | 1,360.770 us | 8,359.686 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,705.600 us | 145,345.000 us | 0.13x | 0.13–0.13x | -126735.100 us | 9 pairs; 37/145 ms; 2/1 ops/lane; IQR 1.011/1.005 | `Pcre2Backtracking` | 1,588.612 us | 510.795 us | 8,555.050 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,833.100 us | 21,856.000 us | 0.22x | 0.22–0.22x | -17023.275 us | 9 pairs; 39/44 ms; 8/2 ops/lane; IQR 1.004/1.008 | `Pcre2Slice` | 2,882.606 us | 10,660.720 us | 18,821.300 us | 0.26x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 22,506.150 us | 134,182.700 us | 0.17x | 0.17–0.17x | -111575.950 us | 9 pairs; 45/134 ms; 2/1 ops/lane; IQR 1.010/1.003 | `Pcre2Backtracking` | 1,903.430 us | 2,317.441 us | 10,428.520 us | 2.16x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 238.888 us | 1,088.424 us | 0.22x | 0.21–0.23x | -849.536 us | 9 pairs; 39/37 ms; 164/34 ops/lane; IQR 1.004/1.057 | `Pcre2Backtracking` | 281.310 us | 322.823 us | 536.042 us | 0.45x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,497.700 us | 13,384.933 us | 0.93x | 0.93–0.94x | -889.800 us | 9 pairs; 37/40 ms; 3/3 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 314.473 us | 14,750.250 us | 15,126.125 us | 0.83x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,813.871 us | 98,635.000 us | 0.03x | 0.03–0.03x | -95817.407 us | 9 pairs; 39/99 ms; 14/1 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 1,907.878 us | 19,873.233 us | 20,078.133 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 184.693 us | 2,780.113 us | 0.07x | 0.07–0.07x | -2595.727 us | 9 pairs; 40/42 ms; 215/15 ops/lane; IQR 1.004/1.014 | `Pcre2SingleTokenRepeat` | 189.969 us | 1,010.770 us | 1,441.252 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,573.952 us | 20,142.950 us | 0.08x | 0.08–0.08x | -18568.450 us | 9 pairs; 39/40 ms; 25/2 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 586.113 us | 10,418.340 us | 10,537.160 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.151 us | 1.097 us | 0.14x | 0.14–0.14x | -0.945 us | 9 pairs; 41/40 ms; 268,125/36,599 ops/lane; IQR 1.012/1.002 | `Pcre2Slice` | 1.474 us | 0.262 us | 0.284 us | 0.53x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.480 us | 0.830 us | 0.58x | 0.58–0.58x | -0.350 us | 9 pairs; 40/40 ms; 82,305/48,750 ops/lane; IQR 1.005/1.003 | `Pcre2Literal` | 0.146 us | 0.149 us | 0.172 us | 2.79x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.353 us | 1.178 us | 0.30x | 0.30–0.30x | -0.824 us | 9 pairs; 47/40 ms; 131,668/33,904 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 1.070 us | 0.345 us | 0.372 us | 0.95x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.883 us | 1.132 us | 0.78x | 0.78–0.78x | -0.252 us | 9 pairs; 41/40 ms; 46,685/35,256 ops/lane; IQR 1.002/1.003 | `Pcre2Character` | 0.276 us | 0.427 us | 0.470 us | 1.88x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.168 us | 0.850 us | 0.20x | 0.20–0.20x | -0.681 us | 9 pairs; 40/40 ms; 238,532/46,856 ops/lane; IQR 1.008/1.008 | `Pcre2Slice` | 1.006 us | 0.155 us | 0.203 us | 0.83x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.969 us | 1.124 us | 0.86x | 0.86–0.87x | -0.156 us | 9 pairs; 41/41 ms; 42,652/36,137 ops/lane; IQR 1.006/1.005 | `Pcre2Slice` | 6.153 us | 0.307 us | 0.323 us | 3.00x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.798 us | 0.835 us | 0.96x | 0.96–0.96x | -0.037 us | 9 pairs; 39/40 ms; 49,296/47,929 ops/lane; IQR 1.002/1.001 | `Pcre2Literal` | 3.610 us | 0.182 us | 0.207 us | 3.86x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.046 us | 1.175 us | 0.89x | 0.89–0.89x | -0.130 us | 9 pairs; 40/39 ms; 38,097/33,173 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 5.224 us | 0.401 us | 0.424 us | 2.47x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.221 us | 1.139 us | 1.07x | 1.06–1.08x | +0.082 us | 9 pairs; 38/40 ms; 31,426/35,130 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 3.718 us | 0.481 us | 0.518 us | 2.36x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.874 us | 0.862 us | 1.02x | 1.01–1.02x | +0.014 us | 9 pairs; 38/41 ms; 43,842/47,232 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 2.190 us | 0.184 us | 0.227 us | 3.86x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.563 us | 1.127 us | 0.50x | 0.50–0.50x | -0.564 us | 9 pairs; 41/40 ms; 73,576/35,390 ops/lane; IQR 1.002/1.001 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.499 us | 0.836 us | 0.60x | 0.60–0.60x | -0.335 us | 9 pairs; 38/40 ms; 76,329/47,724 ops/lane; IQR 1.003/1.003 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.651 us | 1.184 us | 0.55x | 0.55–0.55x | -0.534 us | 9 pairs; 39/40 ms; 60,165/33,740 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.913 us | 1.138 us | 0.80x | 0.80–0.80x | -0.225 us | 9 pairs; 41/40 ms; 45,191/34,903 ops/lane; IQR 1.002/1.002 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.482 us | 0.861 us | 0.56x | 0.56–0.56x | -0.379 us | 9 pairs; 39/41 ms; 81,600/47,035 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.588 us | — | — | — | — | — | `—` | 67.468 us | 99.968 us | 101.014 us | 0.62x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.183 us | — | — | — | — | — | `—` | 2.692 us | 0.361 us | 0.386 us | 23.77x | 48,792 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.061 us | — | — | — | — | — | `—` | 0.822 us | 0.246 us | 0.272 us | 3.90x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.223 us | — | — | — | — | — | `—` | 2.950 us | 0.455 us | 0.482 us | 2.54x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.613 us | — | — | — | — | — | `—` | 0.912 us | 0.542 us | 0.586 us | 2.75x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 4.488 us | — | — | — | — | — | `—` | 1.581 us | 0.273 us | 0.321 us | 13.98x | 728 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.088 us | 0.084 us | 1.06x | 1.05–1.13x | +0.005 us | 9 pairs; 37/40 ms; 418,442/472,449 ops/lane; IQR 1.034/1.008 | `Pcre2Character` | 0 B | 0 B | 2.300 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.013 us | 0.118 us | 0.11x | 0.11–0.11x | -0.105 us | 9 pairs; 16/38 ms; 1,226,979/321,594 ops/lane; IQR 1.008/1.024 | `Pcre2Backtracking` | 0 B | 0 B | 128.981 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.380 us | 0.108 us | 3.52x | 3.48–3.53x | +0.272 us | 9 pairs; 36/41 ms; 94,196/374,486 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 20.756 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.397 us | 0.115 us | 3.45x | 3.42–3.49x | +0.283 us | 9 pairs; 35/39 ms; 88,862/337,288 ops/lane; IQR 1.010/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 71.506 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.376 us | 0.111 us | 3.39x | 3.33–3.41x | +0.265 us | 9 pairs; 36/39 ms; 95,225/352,398 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 69.963 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.570 us | 0.342 us | 1.66x | 1.66–1.67x | +0.228 us | 9 pairs; 37/40 ms; 64,933/115,674 ops/lane; IQR 1.001/1.007 | `Pcre2Character` | 0 B | 0 B | 1.481 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.635 us | 0.02x | 0.02–0.02x | -0.623 us | 9 pairs; 28/32 ms; 2,400,707/50,262 ops/lane; IQR 1.009/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 22.881 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.011 us | 0.518 us | 0.02x | 0.02–0.02x | -0.507 us | 9 pairs; 39/40 ms; 3,406,975/76,787 ops/lane; IQR 1.022/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 21.756 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.480 us | 0.106 us | 4.55x | 4.50–4.61x | +0.375 us | 9 pairs; 37/40 ms; 77,352/373,791 ops/lane; IQR 1.015/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 18.244 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.558 us | 13.183 us | 0.65x | 0.65–0.65x | -4.624 us | 9 pairs; 40/41 ms; 4,717/3,110 ops/lane; IQR 1.004/1.001 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.300 us | 1.049 us | 0.29x | 0.28–0.29x | -0.750 us | 9 pairs; 39/40 ms; 130,321/37,872 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 132.119 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.327 us | 0.460 us | 0.72x | 0.71–0.77x | -0.129 us | 9 pairs; 37/40 ms; 113,936/86,019 ops/lane; IQR 1.066/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 115.606 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.299 us | 0.468 us | 0.64x | 0.64–0.64x | -0.169 us | 9 pairs; 38/38 ms; 127,817/81,859 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 144.725 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.307 us | 0.594 us | 0.52x | 0.51–0.52x | -0.286 us | 9 pairs; 35/41 ms; 114,164/68,713 ops/lane; IQR 1.001/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 102.919 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.320 us | 0.406 us | 0.78x | 0.73–0.79x | -0.089 us | 9 pairs; 41/40 ms; 128,465/99,244 ops/lane; IQR 1.070/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 215.656 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.829 us | 8.812 us | 0.55x | 0.55–0.55x | -3.962 us | 9 pairs; 40/40 ms; 8,294/4,552 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 0 B | 0 B | 1.475 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Inconclusive** | 13 B | 0.270 us | 0.320 us | 0.85x | 0.85–1.07x | -0.049 us | 9 pairs; 37/38 ms; 137,734/120,076 ops/lane; IQR 1.137/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 93.144 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.276 us | 0.400 us | 0.69x | 0.69–0.69x | -0.124 us | 9 pairs; 37/39 ms; 133,389/98,566 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 122.900 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.663 us | — | — | — | — | — | `—` | 0 B | — | 13.206 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.189 us | 0.296 us | 0.64x | 0.64–0.64x | -0.107 us | 9 pairs; 41/40 ms; 214,491/134,907 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 53.737 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.568 us | — | — | — | — | — | `—` | 0 B | — | 15.000 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.521 us | 0.674 us | 0.77x | 0.73–0.77x | -0.155 us | 9 pairs; 37/39 ms; 70,911/58,606 ops/lane; IQR 1.015/1.038 | `Pcre2Slice` | 0 B | 0 B | 64.575 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.933 us | 30.855 us | 0.32x | 0.32–0.32x | -20.925 us | 9 pairs; 40/40 ms; 4,058/1,293 ops/lane; IQR 1.003/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.756 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 1.010 us | 1.059 us | 0.95x | 0.95–0.96x | -0.049 us | 9 pairs; 41/41 ms; 40,593/38,682 ops/lane; IQR 1.004/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.738 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.055 us | 1.197 us | 0.88x | 0.87–0.89x | -0.142 us | 9 pairs; 38/41 ms; 36,438/33,973 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 114.269 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 1.001 us | 1.036 us | 0.97x | 0.96–0.97x | -0.036 us | 9 pairs; 40/40 ms; 39,663/38,496 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 142.094 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.996 us | 1.177 us | 0.85x | 0.84–0.86x | -0.181 us | 9 pairs; 41/40 ms; 41,009/33,583 ops/lane; IQR 1.011/1.001 | `Pcre2Backtracking` | 0 B | 0 B | 104.875 us | 81,544 B |
| `pcre2/duplicate-names` | **Native faster** | 23 B | 1.005 us | 0.960 us | 1.05x | 1.04–1.06x | +0.044 us | 9 pairs; 38/40 ms; 38,220/42,180 ops/lane; IQR 1.003/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 215.269 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.521 us | 8.879 us | 0.62x | 0.62–0.62x | -3.365 us | 9 pairs; 40/40 ms; 7,298/4,529 ops/lane; IQR 1.001/1.001 | `Pcre2Character` | 0 B | 0 B | 1.456 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.929 us | 0.888 us | 1.05x | 1.04–1.05x | +0.042 us | 9 pairs; 40/40 ms; 42,665/44,830 ops/lane; IQR 1.009/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 93.131 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Native faster** | 26 B | 1.024 us | 0.970 us | 1.06x | 1.05–1.06x | +0.056 us | 9 pairs; 41/40 ms; 40,400/41,420 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 125.969 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.912 us | — | — | — | — | — | `—` | 0 B | — | 13.469 us | 11,496 B |
| `pcre2/kreset-global` | **Inconclusive** | 26 B | 0.884 us | 0.868 us | 1.02x | 1.01–1.03x | +0.020 us | 9 pairs; 42/40 ms; 47,661/46,609 ops/lane; IQR 1.009/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.106 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.801 us | — | — | — | — | — | `—` | 0 B | — | 13.806 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.988 us | 1.256 us | 0.79x | 0.78–0.80x | -0.270 us | 9 pairs; 40/41 ms; 39,996/32,809 ops/lane; IQR 1.009/1.004 | `Pcre2Slice` | 0 B | 0 B | 64.338 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.354 us | 2.406 us | 0.56x | 0.56–0.56x | -1.053 us | 9 pairs; 40/40 ms; 29,654/16,623 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.775 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.624 us | 1.057 us | 0.59x | 0.59–0.59x | -0.433 us | 9 pairs; 40/40 ms; 64,188/37,664 ops/lane; IQR 1.002/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 129.969 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.722 us | 1.199 us | 0.60x | 0.60–0.60x | -0.476 us | 9 pairs; 40/41 ms; 55,277/33,938 ops/lane; IQR 1.001/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 114.731 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.613 us | 1.037 us | 0.59x | 0.59–0.60x | -0.424 us | 9 pairs; 38/40 ms; 61,922/38,993 ops/lane; IQR 1.001/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 144.725 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.654 us | 1.166 us | 0.56x | 0.56–0.56x | -0.511 us | 9 pairs; 41/39 ms; 63,176/33,773 ops/lane; IQR 1.001/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 103.112 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.656 us | 0.955 us | 0.69x | 0.69–0.69x | -0.300 us | 9 pairs; 40/40 ms; 60,862/42,151 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 216.575 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.817 us | 2.192 us | 0.83x | 0.83–0.83x | -0.377 us | 9 pairs; 40/40 ms; 21,858/18,354 ops/lane; IQR 1.003/1.002 | `Pcre2Character` | 0 B | 0 B | 1.413 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.607 us | 0.887 us | 0.68x | 0.68–0.69x | -0.281 us | 9 pairs; 40/40 ms; 66,680/44,583 ops/lane; IQR 1.003/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 94.237 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.611 us | 0.965 us | 0.63x | 0.63–0.63x | -0.354 us | 9 pairs; 41/41 ms; 66,594/42,986 ops/lane; IQR 1.002/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 122.381 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.567 us | — | — | — | — | — | `—` | 0 B | — | 14.562 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.499 us | 0.867 us | 0.58x | 0.57–0.58x | -0.367 us | 9 pairs; 41/40 ms; 81,294/46,551 ops/lane; IQR 1.007/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 53.050 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.460 us | — | — | — | — | — | `—` | 0 B | — | 14.238 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.546 us | 1.254 us | 0.44x | 0.43–0.44x | -0.708 us | 9 pairs; 40/40 ms; 73,107/32,284 ops/lane; IQR 1.005/1.000 | `Pcre2Slice` | 0 B | 0 B | 63.288 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.994 us | — | — | — | — | — | `—` | 480 B | — | 1.175 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.142 us | — | — | — | — | — | `—` | 48 B | — | 132.738 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.258 us | — | — | — | — | — | `—` | 48 B | — | 117.719 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.133 us | — | — | — | — | — | `—` | 48 B | — | 141.644 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.134 us | — | — | — | — | — | `—` | 48 B | — | 100.494 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.129 us | — | — | — | — | — | `—` | 48 B | — | 221.181 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.180 us | — | — | — | — | — | `—` | 64 B | — | 1.431 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.142 us | — | — | — | — | — | `—` | 40 B | — | 94.881 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.136 us | — | — | — | — | — | `—` | 40 B | — | 122.956 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.995 us | — | — | — | — | — | `—` | 1,224 B | — | 13.338 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.077 us | — | — | — | — | — | `—` | 48 B | — | 52.919 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.523 us | — | — | — | — | — | `—` | 936 B | — | 13.631 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.851 us | — | — | — | — | — | `—` | 28,936 B | — | 64.900 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 30.220–239.157 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.532–95.755 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.165–12.726 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.307–49.861 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.033–1.483 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.430–42.318 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.761–22.647 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 12.486–100.176 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.153–72.809 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.204–5.447 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 50.222–393.308 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.065–0.075 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 38.838–302.633 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.852–5.144 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.608–141.913 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 170.422–1,338.329 us |

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
