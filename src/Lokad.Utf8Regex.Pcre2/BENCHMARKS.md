<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `A8D2F6805EEE919EEDD42C2B422F10F49D53819A8EFD807AE07A5145ECA1BE54`
- Latest managed row measurement: `2026-11-01T16:29:29.9450526+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-01T16:29:29.9450526+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `40/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `54` managed faster, `2` equivalent, `6` native faster, `38` inconclusive, `1` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x1`, `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `825ed1f7c4b4` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `825ed1f7c4b4` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 1.172 us | 2.351 us | 0.49x | 0.49–0.52x | -1.176 us | 9 pairs; 29/26 ms; 24,823/11,092 ops/lane; IQR 1.552/1.532 | `Pcre2Slice` | 1,516.732 us | 0.355 us | 0.931 us | 1.26x | 5,744 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.344 us | 11.744 us | 0.11x | 0.10–0.13x | -10.411 us | 9 pairs; 36/51 ms; 26,511/4,340 ops/lane; IQR 1.044/1.256 | `Pcre2Slice` | 3.136 us | 0.170 us | 4.193 us | 0.32x | 3,872 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.830 us | 0.01x | 0.01–0.01x | -1.819 us | 9 pairs; 39/39 ms; 3,647,813/21,577 ops/lane; IQR 1.008/1.002 | `Pcre2Slice` | 0.394 us | 1.790 us | 1.863 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 2.657 us | 0.253 us | 10.52x | 10.42–10.58x | +2.403 us | 9 pairs; 39/39 ms; 14,601/155,133 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0.046 us | 0.126 us | 0.159 us | 16.74x | 0 B | 0 B |
| `common/date-miss` | **Inconclusive** | 47 B | 5.294 us | 0.573 us | 9.34x | 9.21–10.33x | +4.727 us | 9 pairs; 41/39 ms; 7,707/68,349 ops/lane; IQR 1.576/1.362 | `Pcre2Backtracking` | 0.128 us | 0.779 us | 0.820 us | 6.46x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.183 us | 0.303 us | 0.60x | 0.58–0.61x | -0.126 us | 9 pairs; 42/40 ms; 226,352/132,207 ops/lane; IQR 1.039/1.011 | `Pcre2Slice` | 0.186 us | 0.325 us | 0.568 us | 0.32x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.183 us | 0.364 us | 0.50x | 0.48–0.50x | -0.181 us | 9 pairs; 40/39 ms; 219,349/107,707 ops/lane; IQR 1.003/1.014 | `Pcre2Slice` | 0.191 us | 0.453 us | 0.479 us | 0.38x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.122 us | 0.290 us | 0.42x | 0.42–0.43x | -0.168 us | 9 pairs; 40/39 ms; 327,751/135,732 ops/lane; IQR 1.024/1.015 | `Pcre2Slice` | 0.136 us | 0.252 us | 0.267 us | 0.46x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.115 us | 0.316 us | 0.37x | 0.36–0.37x | -0.200 us | 9 pairs; 40/39 ms; 344,775/124,457 ops/lane; IQR 1.016/1.010 | `Pcre2Slice` | 0.136 us | 0.263 us | 0.280 us | 0.41x | 0 B | 0 B |
| `common/one-node-backtracking` | **Inconclusive** | 52 B | 0.180 us | 0.086 us | 2.16x | 2.09–2.28x | +0.096 us | 9 pairs; 42/44 ms; 231,966/514,505 ops/lane; IQR 1.145/1.134 | `Pcre2Slice` | 0.212 us | 0.987 us | 1.042 us | 0.17x | 0 B | 0 B |
| `common/uri-match` | **Inconclusive** | 46 B | 0.034 us | 0.206 us | 0.17x | 0.16–0.18x | -0.173 us | 9 pairs; 40/43 ms; 1,182,503/206,573 ops/lane; IQR 1.671/1.511 | `Pcre2Backtracking` | 0.204 us | 0.311 us | 0.351 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.108 us | 0.976 us | 0.11x | 0.11–0.11x | -0.872 us | 9 pairs; 41/39 ms; 383,882/40,102 ops/lane; IQR 1.031/1.008 | `Pcre2Backtracking` | 0.195 us | 0.243 us | 0.269 us | 0.40x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Inconclusive** | 18 B | 0.034 us | 0.159 us | 0.21x | 0.21–0.22x | -0.124 us | 9 pairs; 39/40 ms; 1,143,217/251,520 ops/lane; IQR 1.192/1.206 | `Pcre2Backtracking` | 0.186 us | 0.288 us | 0.333 us | 0.10x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.020 us | 0.112 us | 0.18x | 0.17–0.18x | -0.092 us | 9 pairs; 38/40 ms; 1,933,460/359,907 ops/lane; IQR 1.019/1.014 | `Pcre2Backtracking` | 0.113 us | 0.151 us | 0.189 us | 0.10x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.100 us | 0.187 us | 0.53x | 0.53–0.54x | -0.087 us | 9 pairs; 39/41 ms; 394,467/217,275 ops/lane; IQR 1.016/1.008 | `Pcre2Backtracking` | 0.016 us | 0.096 us | 0.111 us | 0.90x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.324 us | 0.443 us | 0.73x | 0.72–0.74x | -0.118 us | 9 pairs; 31/38 ms; 96,988/86,048 ops/lane; IQR 1.006/1.016 | `Pcre2Slice` | 0.210 us | 0.119 us | 0.153 us | 2.12x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Inconclusive** | 7 B | 0.031 us | 0.115 us | 0.27x | 0.20–0.27x | -0.084 us | 9 pairs; 54/40 ms; 1,735,746/350,889 ops/lane; IQR 1.147/1.022 | `Pcre2Backtracking` | 0.019 us | 0.102 us | 0.117 us | 0.27x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.112 us | 1.931 us | 0.06x | 0.05–0.06x | -1.819 us | 9 pairs; 41/45 ms; 366,199/23,403 ops/lane; IQR 1.011/1.194 | `Pcre2Literal` | 0.121 us | 0.051 us | 0.643 us | 0.17x | 0 B | 0 B |
| `literal/early` | **Managed faster** | 4,103 B | 0.181 us | 4.047 us | 0.04x | 0.04–0.05x | -3.866 us | 9 pairs; 42/39 ms; 231,078/9,687 ops/lane; IQR 1.009/1.012 | `Pcre2Literal` | 0.174 us | 0.054 us | 0.624 us | 0.29x | 0 B | 0 B |
| `literal/late` | **Managed faster** | 4,103 B | 0.209 us | 4.007 us | 0.05x | 0.05–0.05x | -3.798 us | 9 pairs; 34/41 ms; 161,506/10,348 ops/lane; IQR 1.006/1.013 | `Pcre2Literal` | 0.283 us | 0.233 us | 0.874 us | 0.24x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.146 us | 2.184 us | 0.07x | 0.06–0.08x | -2.039 us | 9 pairs; 39/27 ms; 268,068/12,491 ops/lane; IQR 1.451/1.893 | `Pcre2Literal` | 0.209 us | 0.205 us | 0.720 us | 0.20x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.100 us | 0.09x | 0.09–0.09x | -0.091 us | 9 pairs; 39/40 ms; 4,445,379/401,136 ops/lane; IQR 1.007/1.005 | `Pcre2Slice` | 1.181 us | 0.073 us | 0.092 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.062 us | 0.085 us | 0.74x | 0.71–0.74x | -0.022 us | 9 pairs; 39/39 ms; 632,449/455,960 ops/lane; IQR 1.006/1.031 | `Pcre2Literal` | 0.126 us | 0.051 us | 0.073 us | 0.86x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Inconclusive** | 23 B | 0.022 us | 0.124 us | 0.16x | 0.15–0.20x | -0.102 us | 9 pairs; 65/45 ms; 2,982,411/360,673 ops/lane; IQR 1.197/1.346 | `Pcre2Backtracking` | 0.242 us | 0.090 us | 0.115 us | 0.19x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.186 us | 0.213 us | 0.87x | 0.86–0.88x | -0.027 us | 9 pairs; 38/38 ms; 204,441/180,999 ops/lane; IQR 1.008/1.009 | `Pcre2Character` | 0.139 us | 0.111 us | 0.145 us | 1.28x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.079 us | 0.214 us | 0.37x | 0.34–0.38x | -0.135 us | 9 pairs; 27/24 ms; 338,396/113,207 ops/lane; IQR 1.512/1.637 | `Pcre2Slice` | 1.229 us | 0.115 us | 0.166 us | 0.48x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 78.306 us | 162.225 us | 0.48x | 0.48–0.49x | -83.217 us | 9 pairs; 40/40 ms; 515/249 ops/lane; IQR 1.015/1.014 | `Pcre2Slice` | 404.107 us | 106.224 us | 106.287 us | 0.74x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 56.075 us | 445.713 us | 0.13x | 0.12–0.13x | -389.000 us | 9 pairs; 59/62 ms; 1,050/139 ops/lane; IQR 1.012/1.005 | `Pcre2Character` | 402.294 us | 219.809 us | 229.947 us | 0.24x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 10.245 us | 210.985 us | 0.05x | 0.05–0.05x | -200.740 us | 9 pairs; 40/41 ms; 3,942/193 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 8.184 us | 231.812 us | 262.486 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 237.528 us | 203.218 us | 1.17x | 1.16–1.18x | +35.613 us | 9 pairs; 39/39 ms; 163/191 ops/lane; IQR 1.005/1.012 | `Pcre2SingleTokenRepeat` | 8.138 us | 237.369 us | 235.382 us | 1.01x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.519 us | 23.638 us | 0.15x | 0.13–0.15x | -20.123 us | 9 pairs; 35/27 ms; 10,028/1,152 ops/lane; IQR 1.013/1.031 | `Pcre2Literal` | 1.447 us | 4.264 us | 6.153 us | 0.57x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.611 us | 81.284 us | 0.12x | 0.12–0.12x | -71.640 us | 9 pairs; 41/39 ms; 4,271/484 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 9.456 us | 89.887 us | 90.988 us | 0.11x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Managed faster** | 16,013,977 B | 5,533.800 us | 42,956.000 us | 0.13x | 0.13–0.13x | -37393.156 us | 9 pairs; 50/43 ms; 9/1 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 5,511.271 us | 12,274.367 us | 18,507.850 us | 0.30x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 5,783.114 us | 53,874.800 us | 0.11x | 0.10–0.11x | -47324.043 us | 9 pairs; 40/54 ms; 7/1 ops/lane; IQR 1.033/1.028 | `Pcre2Backtracking` | 6,620.075 us | 41,962.600 us | 47,444.600 us | 0.12x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,924.957 us | 465,506.800 us | 0.00x | 0.00–0.00x | -463639.871 us | 9 pairs; 40/466 ms; 21/1 ops/lane; IQR 1.040/1.012 | `Pcre2Character` | 1,846.954 us | 23,565.133 us | 30,733.700 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,408.683 us | 11,308.933 us | 0.48x | 0.46–0.58x | -5998.983 us | 9 pairs; 32/34 ms; 6/3 ops/lane; IQR 1.016/1.133 | `Pcre2Literal` | 1,882.019 us | 1,607.226 us | 9,665.433 us | 0.56x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,315.850 us | 140,015.500 us | 0.13x | 0.13–0.13x | -121777.950 us | 9 pairs; 37/140 ms; 2/1 ops/lane; IQR 1.027/1.008 | `Pcre2Backtracking` | 2,068.892 us | 600.031 us | 10,082.260 us | 1.82x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,720.087 us | 21,270.400 us | 0.22x | 0.22–0.22x | -16513.438 us | 9 pairs; 38/43 ms; 8/2 ops/lane; IQR 1.008/1.014 | `Pcre2Slice` | 2,829.178 us | 10,865.420 us | 19,228.800 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,531.000 us | 129,630.200 us | 0.17x | 0.16–0.17x | -108046.600 us | 9 pairs; 43/130 ms; 2/1 ops/lane; IQR 1.006/1.012 | `Pcre2Backtracking` | 1,903.415 us | 2,328.200 us | 10,446.800 us | 2.06x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 233.709 us | 968.161 us | 0.24x | 0.24–0.25x | -733.648 us | 9 pairs; 40/40 ms; 173/41 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 486.270 us | 482.400 us | 763.293 us | 0.31x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,048.467 us | 13,068.000 us | 0.92x | 0.90–0.94x | -996.267 us | 9 pairs; 36/39 ms; 3/3 ops/lane; IQR 1.007/1.022 | `Pcre2Backtracking` | 309.304 us | 15,444.675 us | 15,685.325 us | 0.77x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Inconclusive** | 594,930 B | 2,736.671 us | 95,454.800 us | 0.03x | 0.03–0.03x | -92718.129 us | 9 pairs; 38/95 ms; 14/1 ops/lane; IQR 1.018/1.017 | `Pcre2Character` | 2,363.227 us | 32,893.750 us | 33,309.900 us | 0.08x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 179.261 us | 2,721.240 us | 0.07x | 0.07–0.07x | -2541.979 us | 9 pairs; 39/41 ms; 215/15 ops/lane; IQR 1.004/1.020 | `Pcre2SingleTokenRepeat` | 189.581 us | 1,016.116 us | 1,437.346 us | 0.12x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,570.970 us | 20,109.550 us | 0.08x | 0.08–0.08x | -18534.178 us | 9 pairs; 36/40 ms; 23/2 ops/lane; IQR 1.006/1.008 | `Pcre2Backtracking` | 593.873 us | 10,511.480 us | 10,718.240 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.137 us | 1.092 us | 0.12x | 0.12–0.13x | -0.956 us | 9 pairs; 40/40 ms; 293,606/36,375 ops/lane; IQR 1.006/1.004 | `Pcre2Slice` | 1.433 us | 0.260 us | 0.281 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.501 us | 0.827 us | 0.61x | 0.60–0.62x | -0.325 us | 9 pairs; 40/40 ms; 80,642/48,205 ops/lane; IQR 1.008/1.005 | `Pcre2Literal` | 0.146 us | 0.147 us | 0.169 us | 2.96x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.334 us | 1.172 us | 0.29x | 0.28–0.29x | -0.837 us | 9 pairs; 44/40 ms; 131,953/33,756 ops/lane; IQR 1.007/1.004 | `Pcre2Backtracking` | 1.073 us | 0.347 us | 0.373 us | 0.89x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 1.776 us | 1.710 us | 1.04x | 0.98–1.06x | +0.062 us | 9 pairs; 39/42 ms; 22,208/24,799 ops/lane; IQR 1.006/1.061 | `Pcre2Character` | 0.273 us | 0.427 us | 0.474 us | 3.75x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.169 us | 0.859 us | 0.20x | 0.19–0.20x | -0.691 us | 9 pairs; 37/42 ms; 220,650/49,369 ops/lane; IQR 1.134/1.230 | `Pcre2Slice` | 2.003 us | 0.260 us | 0.330 us | 0.51x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.925 us | 1.107 us | 0.84x | 0.83–0.84x | -0.182 us | 9 pairs; 24/30 ms; 26,277/26,727 ops/lane; IQR 1.009/1.003 | `Pcre2Slice` | 6.227 us | 0.311 us | 0.328 us | 2.82x | 0 B | 0 B |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.810 us | 0.832 us | 0.97x | 0.97–1.19x | -0.024 us | 9 pairs; 20/27 ms; 24,530/32,929 ops/lane; IQR 1.682/1.352 | `Pcre2Literal` | 3.583 us | 0.182 us | 0.205 us | 3.96x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 1.079 us | 1.184 us | 0.91x | 0.91–0.92x | -0.102 us | 9 pairs; 36/40 ms; 33,497/33,761 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 5.160 us | 0.400 us | 0.428 us | 2.52x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 2.515 us | 1.775 us | 1.40x | 1.36–1.43x | +0.718 us | 9 pairs; 40/42 ms; 15,850/23,906 ops/lane; IQR 1.007/1.022 | `Pcre2Character` | 4.496 us | 0.783 us | 0.857 us | 2.94x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Equivalent** | 67 B | 0.863 us | 0.851 us | 1.01x | 1.00–1.02x | +0.010 us | 9 pairs; 26/25 ms; 30,352/29,888 ops/lane; IQR 1.007/1.017 | `Pcre2Slice` | 2.680 us | 0.182 us | 0.225 us | 3.84x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.569 us | 1.106 us | 0.51x | 0.51–0.52x | -0.538 us | 9 pairs; 32/41 ms; 55,495/37,110 ops/lane; IQR 1.002/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.908 us | 1.181 us | 0.77x | 0.72–0.78x | -0.275 us | 9 pairs; 39/38 ms; 42,703/31,940 ops/lane; IQR 1.016/1.017 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.713 us | 1.179 us | 0.61x | 0.60–0.61x | -0.468 us | 9 pairs; 43/40 ms; 60,156/33,797 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Inconclusive** | 45 B | 1.792 us | 1.728 us | 1.04x | 1.00–1.04x | +0.064 us | 9 pairs; 40/41 ms; 22,115/23,652 ops/lane; IQR 1.009/1.031 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.524 us | 0.851 us | 0.62x | 0.61–0.62x | -0.327 us | 9 pairs; 43/39 ms; 82,491/45,846 ops/lane; IQR 1.015/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 90.425 us | — | — | — | — | — | `—` | 93.916 us | 148.395 us | 150.951 us | 0.60x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.905 us | — | — | — | — | — | `—` | 3.537 us | 0.360 us | 0.385 us | 25.70x | 48,792 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.088 us | — | — | — | — | — | `—` | 0.836 us | 0.295 us | 0.471 us | 2.31x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.240 us | — | — | — | — | — | `—` | 2.957 us | 0.449 us | 0.491 us | 2.53x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.570 us | — | — | — | — | — | `—` | 1.155 us | 0.911 us | 1.013 us | 1.55x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 5.439 us | — | — | — | — | — | `—` | 1.786 us | 0.261 us | 0.301 us | 18.06x | 7,192 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.127 us | 0.130 us | 1.00x | 0.97–1.02x | 0.000 us | 9 pairs; 60/58 ms; 472,565/450,663 ops/lane; IQR 1.478/1.542 | `Pcre2Character` | 0 B | 0 B | 3.550 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Inconclusive** | 23 B | 0.013 us | 0.119 us | 0.11x | 0.11–0.11x | -0.106 us | 9 pairs; 41/42 ms; 3,123,365/349,087 ops/lane; IQR 1.542/1.562 | `Pcre2Backtracking` | 0 B | 0 B | 129.831 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Inconclusive** | 3 B | 0.688 us | 0.163 us | 4.18x | 3.60–4.25x | +0.524 us | 9 pairs; 64/60 ms; 93,218/368,865 ops/lane; IQR 1.787/1.514 | `Pcre2Backtracking` | 0 B | 0 B | 22.587 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Inconclusive** | 3 B | 0.408 us | 0.119 us | 3.46x | 3.39–3.79x | +0.290 us | 9 pairs; 37/41 ms; 89,581/343,727 ops/lane; IQR 1.255/1.152 | `Pcre2Backtracking` | 0 B | 0 B | 70.481 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Inconclusive** | 3 B | 0.658 us | 0.160 us | 4.02x | 3.61–4.34x | +0.495 us | 9 pairs; 62/61 ms; 94,841/382,042 ops/lane; IQR 1.818/1.535 | `Pcre2Backtracking` | 0 B | 0 B | 72.250 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.557 us | 0.337 us | 1.66x | 1.65–1.67x | +0.222 us | 9 pairs; 36/40 ms; 65,332/119,104 ops/lane; IQR 1.007/1.005 | `Pcre2Character` | 0 B | 0 B | 1.475 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.642 us | 0.02x | 0.02–0.02x | -0.631 us | 9 pairs; 39/39 ms; 3,166,628/60,511 ops/lane; IQR 1.025/1.025 | `Pcre2Backtracking` | 0 B | 0 B | 22.275 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.017 us | 0.635 us | 0.03x | 0.02–0.03x | -0.618 us | 9 pairs; 61/49 ms; 3,490,754/76,404 ops/lane; IQR 1.615/1.474 | `Pcre2Backtracking` | 0 B | 0 B | 21.650 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.851 us | 0.172 us | 4.93x | 4.87–5.02x | +0.677 us | 9 pairs; 35/39 ms; 41,167/225,021 ops/lane; IQR 1.010/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 18.188 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 14.262 us | 16.774 us | 0.85x | 0.85–0.86x | -2.527 us | 9 pairs; 42/40 ms; 2,951/2,410 ops/lane; IQR 1.008/1.008 | `Pcre2Literal` | 0 B | 0 B | 0.769 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Inconclusive** | 23 B | 0.309 us | 1.059 us | 0.29x | 0.28–0.29x | -0.750 us | 9 pairs; 41/40 ms; 132,477/38,241 ops/lane; IQR 1.323/1.357 | `Pcre2Backtracking` | 0 B | 0 B | 129.081 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.327 us | 0.472 us | 0.68x | 0.68–0.69x | -0.149 us | 9 pairs; 38/38 ms; 117,735/79,914 ops/lane; IQR 1.028/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 116.013 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Inconclusive** | 31 B | 0.298 us | 0.471 us | 0.63x | 0.62–0.64x | -0.173 us | 9 pairs; 38/41 ms; 128,229/86,633 ops/lane; IQR 1.012/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 141.512 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.315 us | 0.594 us | 0.53x | 0.52–0.53x | -0.279 us | 9 pairs; 38/43 ms; 119,842/71,637 ops/lane; IQR 1.004/1.002 | `Pcre2Backtracking` | 0 B | 0 B | 103.819 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.297 us | 0.406 us | 0.73x | 0.62–0.74x | -0.110 us | 9 pairs; 38/39 ms; 128,620/94,850 ops/lane; IQR 1.412/1.644 | `Pcre2Backtracking` | 0 B | 0 B | 212.900 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.750 us | 8.587 us | 0.55x | 0.55–0.56x | -3.808 us | 9 pairs; 39/41 ms; 8,183/4,827 ops/lane; IQR 1.007/1.007 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Inconclusive** | 13 B | 0.306 us | 0.314 us | 0.98x | 0.96–0.99x | -0.007 us | 9 pairs; 42/38 ms; 138,072/121,309 ops/lane; IQR 1.010/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 93.231 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.271 us | 0.400 us | 0.68x | 0.67–0.68x | -0.128 us | 9 pairs; 23/40 ms; 86,065/101,091 ops/lane; IQR 1.015/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 125.844 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.674 us | — | — | — | — | — | `—` | 0 B | — | 14.825 us | 11,496 B |
| `pcre2/kreset-global` | **Inconclusive** | 26 B | 0.196 us | 0.295 us | 0.67x | 0.66–0.67x | -0.097 us | 9 pairs; 39/40 ms; 201,675/134,218 ops/lane; IQR 1.010/1.013 | `Pcre2Backtracking` | 0 B | 0 B | 54.706 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.585 us | — | — | — | — | — | `—` | 0 B | — | 14.419 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.854 us | 1.117 us | 0.77x | 0.75–0.78x | -0.262 us | 9 pairs; 35/38 ms; 40,740/33,837 ops/lane; IQR 1.024/1.018 | `Pcre2Slice` | 0 B | 0 B | 64.381 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 16.282 us | 39.352 us | 0.41x | 0.41–0.42x | -23.068 us | 9 pairs; 38/37 ms; 2,362/947 ops/lane; IQR 1.010/1.009 | `Pcre2Literal` | 0 B | 0 B | 1.219 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Inconclusive** | 23 B | 1.695 us | 1.530 us | 1.10x | 1.01–1.12x | +0.148 us | 9 pairs; 67/58 ms; 39,535/37,952 ops/lane; IQR 1.068/1.069 | `Pcre2Backtracking` | 0 B | 0 B | 130.344 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Inconclusive** | 17 B | 1.229 us | 1.302 us | 0.94x | 0.90–1.05x | -0.073 us | 9 pairs; 27/31 ms; 22,265/23,690 ops/lane; IQR 1.566/1.354 | `Pcre2Backtracking` | 0 B | 0 B | 115.544 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Equivalent** | 31 B | 1.032 us | 1.027 us | 1.00x | 1.00–1.02x | +0.003 us | 9 pairs; 32/32 ms; 31,160/31,221 ops/lane; IQR 1.017/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 144.569 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 1.002 us | 1.162 us | 0.86x | 0.86–0.87x | -0.163 us | 9 pairs; 39/39 ms; 38,928/33,964 ops/lane; IQR 1.013/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 102.594 us | 81,544 B |
| `pcre2/duplicate-names` | **Inconclusive** | 23 B | 0.903 us | 0.967 us | 0.94x | 0.93–1.02x | -0.061 us | 9 pairs; 25/26 ms; 27,211/26,906 ops/lane; IQR 1.370/1.231 | `Pcre2Backtracking` | 0 B | 0 B | 215.144 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.630 us | 8.643 us | 0.65x | 0.65–0.65x | -3.012 us | 9 pairs; 41/40 ms; 7,286/4,654 ops/lane; IQR 1.005/1.007 | `Pcre2Character` | 0 B | 0 B | 1.438 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.933 us | 0.884 us | 1.06x | 1.05–1.07x | +0.053 us | 9 pairs; 39/40 ms; 41,755/45,378 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 92.638 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Inconclusive** | 26 B | 1.076 us | 1.022 us | 1.05x | 1.03–1.12x | +0.054 us | 9 pairs; 43/42 ms; 39,941/41,495 ops/lane; IQR 1.362/1.254 | `Pcre2Backtracking` | 0 B | 0 B | 125.781 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.906 us | — | — | — | — | — | `—` | 0 B | — | 13.900 us | 11,496 B |
| `pcre2/kreset-global` | **Inconclusive** | 26 B | 1.031 us | 0.962 us | 1.06x | 1.06–1.27x | +0.063 us | 9 pairs; 46/45 ms; 44,163/46,523 ops/lane; IQR 1.724/1.426 | `Pcre2Backtracking` | 0 B | 0 B | 85.250 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 8.317 us | — | — | — | — | — | `—` | 0 B | — | 13.287 us | 10,536 B |
| `pcre2/same-start-global` | **Inconclusive** | 9 B | 1.421 us | 1.673 us | 0.83x | 0.79–0.89x | -0.251 us | 9 pairs; 58/52 ms; 41,159/31,053 ops/lane; IQR 1.252/1.112 | `Pcre2Slice` | 0 B | 0 B | 65.031 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 2.089 us | 3.113 us | 0.67x | 0.66–0.68x | -1.009 us | 9 pairs; 40/39 ms; 19,373/12,524 ops/lane; IQR 1.011/1.012 | `Pcre2Literal` | 0 B | 0 B | 0.731 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.971 us | 1.537 us | 0.63x | 0.61–0.64x | -0.560 us | 9 pairs; 59/57 ms; 60,526/36,996 ops/lane; IQR 1.009/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 131.500 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.671 us | 1.185 us | 0.57x | 0.56–0.57x | -0.515 us | 9 pairs; 39/40 ms; 57,525/33,722 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 112.725 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.921 us | 1.526 us | 0.60x | 0.60–0.61x | -0.608 us | 9 pairs; 36/39 ms; 39,166/25,793 ops/lane; IQR 1.011/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 138.744 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.655 us | 1.170 us | 0.56x | 0.56–0.57x | -0.514 us | 9 pairs; 40/39 ms; 61,039/33,761 ops/lane; IQR 1.007/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 103.944 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.635 us | 0.960 us | 0.66x | 0.66–0.67x | -0.322 us | 9 pairs; 41/40 ms; 63,753/41,780 ops/lane; IQR 1.004/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 217.094 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Inconclusive** | 248 B | 1.834 us | 2.191 us | 0.84x | 0.83–0.96x | -0.351 us | 9 pairs; 40/40 ms; 21,947/18,275 ops/lane; IQR 1.625/1.414 | `Pcre2Character` | 0 B | 0 B | 1.387 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.622 us | 0.889 us | 0.70x | 0.69–0.71x | -0.266 us | 9 pairs; 41/40 ms; 65,852/45,509 ops/lane; IQR 1.013/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 92.100 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.599 us | 0.960 us | 0.62x | 0.62–0.63x | -0.362 us | 9 pairs; 41/40 ms; 67,837/41,218 ops/lane; IQR 1.005/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 124.800 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.636 us | — | — | — | — | — | `—` | 0 B | — | 13.144 us | 11,496 B |
| `pcre2/kreset-global` | **Inconclusive** | 26 B | 0.769 us | 1.172 us | 0.64x | 0.58–0.66x | -0.402 us | 9 pairs; 39/38 ms; 51,132/32,387 ops/lane; IQR 1.567/1.394 | `Pcre2Backtracking` | 0 B | 0 B | 53.475 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.511 us | — | — | — | — | — | `—` | 0 B | — | 13.881 us | 10,536 B |
| `pcre2/same-start-global` | **Inconclusive** | 9 B | 0.603 us | 1.308 us | 0.46x | 0.46–0.52x | -0.705 us | 9 pairs; 37/41 ms; 61,542/31,077 ops/lane; IQR 1.462/1.319 | `Pcre2Slice` | 0 B | 0 B | 66.400 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 18.817 us | — | — | — | — | — | `—` | 480 B | — | 0.706 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.148 us | — | — | — | — | — | `—` | 48 B | — | 130.287 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.254 us | — | — | — | — | — | `—` | 48 B | — | 114.931 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.887 us | — | — | — | — | — | `—` | 48 B | — | 142.600 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.140 us | — | — | — | — | — | `—` | 48 B | — | 101.719 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.122 us | — | — | — | — | — | `—` | 48 B | — | 360.719 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.146 us | — | — | — | — | — | `—` | 64 B | — | 2.013 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.905 us | — | — | — | — | — | `—` | 40 B | — | 91.719 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.936 us | — | — | — | — | — | `—` | 40 B | — | 122.088 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.669 us | — | — | — | — | — | `—` | 1,224 B | — | 13.194 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.904 us | — | — | — | — | — | `—` | 48 B | — | 53.775 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.477 us | — | — | — | — | — | `—` | 936 B | — | 23.012 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.990 us | — | — | — | — | — | `—` | 28,936 B | — | 63.288 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 27.786–228.998 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 16.654–94.132 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 3.110–12.691 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.150–80.078 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.031–1.431 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 8.951–41.401 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 7.655–22.676 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 7.943–99.798 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 10.148–71.256 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.215–5.472 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 50.642–393.798 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.064–0.102 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.836–308.894 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.909–5.120 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.245–141.322 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 176.779–1,325.424 us |

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
