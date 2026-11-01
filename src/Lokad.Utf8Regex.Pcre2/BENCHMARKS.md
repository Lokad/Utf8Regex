<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `F5564C426CBCC51A77AF3C51435FE03C47E48F2C456FF933088B4B7C97DF11A3`
- Latest managed row measurement: `2026-11-01T04:25:44.3499890+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-01T04:25:44.3499890+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `39/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `72` managed faster, `1` equivalent, `15` native faster, `13` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `8d5545276e60` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `8d5545276e60` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 870.141 us | 2.195 us | 399.57x | 339.83–433.61x | +868.005 us | 9 pairs; 38/31 ms; 44/13,942 ops/lane; IQR 1.011/1.176 | `Pcre2Slice` | 873.437 us | 0.279 us | 0.804 us | 1082.06x | 5,744 B | 0 B |
| `backtracking/capture-backreference` | **Inconclusive** | 22,528 B | 1.318 us | 11.721 us | 0.11x | 0.10–0.13x | -10.470 us | 9 pairs; 35/38 ms; 26,629/3,207 ops/lane; IQR 1.069/1.065 | `Pcre2Slice` | 1.850 us | 0.096 us | 3.256 us | 0.40x | 3,872 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.778 us | 0.01x | 0.01–0.01x | -1.767 us | 9 pairs; 27/40 ms; 2,471,308/22,549 ops/lane; IQR 1.020/1.011 | `Pcre2Slice` | 0.210 us | 1.243 us | 1.284 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 1.809 us | 0.161 us | 11.29x | 11.04–11.34x | +1.648 us | 9 pairs; 40/39 ms; 21,950/245,649 ops/lane; IQR 1.014/1.017 | `Pcre2Backtracking` | 0.046 us | 0.122 us | 0.150 us | 12.02x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 6.478 us | 0.561 us | 11.53x | 11.39–11.59x | +5.917 us | 9 pairs; 39/39 ms; 6,026/69,377 ops/lane; IQR 1.004/1.010 | `Pcre2Backtracking` | 0.125 us | 0.756 us | 0.790 us | 8.20x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.171 us | 0.295 us | 0.58x | 0.57–0.58x | -0.124 us | 9 pairs; 40/39 ms; 236,026/132,496 ops/lane; IQR 1.009/1.016 | `Pcre2Slice` | 0.171 us | 0.314 us | 0.346 us | 0.49x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.181 us | 0.353 us | 0.52x | 0.51–0.52x | -0.171 us | 9 pairs; 40/39 ms; 222,851/110,616 ops/lane; IQR 1.014/1.010 | `Pcre2Slice` | 0.184 us | 0.436 us | 0.485 us | 0.37x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.119 us | 0.287 us | 0.41x | 0.41–0.43x | -0.168 us | 9 pairs; 40/39 ms; 332,521/137,246 ops/lane; IQR 1.016/1.011 | `Pcre2Slice` | 0.132 us | 0.232 us | 0.256 us | 0.46x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.113 us | 0.308 us | 0.37x | 0.37–0.37x | -0.195 us | 9 pairs; 40/39 ms; 348,712/126,157 ops/lane; IQR 1.010/1.010 | `Pcre2Slice` | 0.132 us | 0.247 us | 0.271 us | 0.42x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.168 us | 0.076 us | 2.22x | 2.18–2.26x | +0.093 us | 9 pairs; 39/43 ms; 232,373/567,529 ops/lane; IQR 1.008/1.025 | `Pcre2Slice` | 0.205 us | 1.001 us | 1.042 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.068 us | 0.196 us | 0.35x | 0.34–0.35x | -0.128 us | 9 pairs; 40/40 ms; 592,846/204,664 ops/lane; IQR 1.004/1.015 | `Pcre2Backtracking` | 0.200 us | 0.300 us | 0.337 us | 0.20x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.130 us | 0.602 us | 0.22x | 0.21–0.22x | -0.472 us | 9 pairs; 40/40 ms; 305,012/66,792 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0.190 us | 0.225 us | 0.248 us | 0.52x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.154 us | 0.21x | 0.21–0.22x | -0.121 us | 9 pairs; 39/41 ms; 1,197,486/263,501 ops/lane; IQR 1.024/1.007 | `Pcre2Backtracking` | 0.112 us | 0.180 us | 0.199 us | 0.17x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.111 us | 0.19x | 0.18–0.19x | -0.089 us | 9 pairs; 41/39 ms; 1,949,171/352,395 ops/lane; IQR 1.023/1.020 | `Pcre2Backtracking` | 0.064 us | 0.090 us | 0.108 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.058 us | 0.121 us | 0.48x | 0.47–0.48x | -0.064 us | 9 pairs; 40/43 ms; 689,106/350,873 ops/lane; IQR 1.003/1.010 | `Pcre2Backtracking` | 0.015 us | 0.086 us | 0.103 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.186 us | 0.277 us | 0.67x | 0.67–0.68x | -0.091 us | 9 pairs; 40/44 ms; 215,618/157,645 ops/lane; IQR 1.003/1.013 | `Pcre2Slice` | 0.201 us | 0.114 us | 0.147 us | 1.26x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Inconclusive** | 7 B | 0.025 us | 0.115 us | 0.22x | 0.21–0.23x | -0.090 us | 9 pairs; 39/43 ms; 1,560,452/370,966 ops/lane; IQR 1.033/1.029 | `Pcre2Backtracking` | 0.019 us | 0.089 us | 0.107 us | 0.23x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.112 us | 2.035 us | 0.05x | 0.05–0.07x | -1.924 us | 9 pairs; 40/50 ms; 360,304/24,510 ops/lane; IQR 1.012/1.189 | `Pcre2Literal` | 0.120 us | 0.049 us | 0.581 us | 0.19x | 0 B | 0 B |
| `literal/early` | **Managed faster** | 4,103 B | 0.116 us | 1.896 us | 0.06x | 0.06–0.07x | -1.780 us | 9 pairs; 39/31 ms; 340,012/16,243 ops/lane; IQR 1.006/1.098 | `Pcre2Literal` | 0.237 us | 0.053 us | 0.580 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Managed faster** | 4,103 B | 0.141 us | 1.957 us | 0.07x | 0.07–0.08x | -1.816 us | 9 pairs; 38/37 ms; 272,519/18,929 ops/lane; IQR 1.016/1.046 | `Pcre2Literal` | 0.258 us | 0.227 us | 0.741 us | 0.19x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.141 us | 2.018 us | 0.07x | 0.06–0.07x | -1.877 us | 9 pairs; 40/43 ms; 284,817/21,341 ops/lane; IQR 1.005/1.108 | `Pcre2Literal` | 0.205 us | 0.200 us | 0.741 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.098 us | 0.09x | 0.09–0.09x | -0.089 us | 9 pairs; 41/40 ms; 4,646,707/409,216 ops/lane; IQR 1.016/1.019 | `Pcre2Slice` | 0.932 us | 0.068 us | 0.086 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.077 us | 0.084 us | 0.91x | 0.88–0.93x | -0.007 us | 9 pairs; 51/41 ms; 657,301/483,051 ops/lane; IQR 1.044/1.009 | `Pcre2Literal` | 0.128 us | 0.050 us | 0.072 us | 1.08x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Inconclusive** | 23 B | 0.014 us | 0.108 us | 0.13x | 0.12–0.13x | -0.094 us | 9 pairs; 40/40 ms; 2,889,807/372,025 ops/lane; IQR 1.019/1.021 | `Pcre2Backtracking` | 0.279 us | 0.089 us | 0.111 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 0.206 us | 0.121 us | 1.70x | 1.69–1.72x | +0.085 us | 9 pairs; 34/40 ms; 162,928/327,343 ops/lane; IQR 1.011/1.017 | `Pcre2Character` | 0.139 us | 0.111 us | 0.141 us | 1.46x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.078 us | 0.198 us | 0.39x | 0.39–0.40x | -0.120 us | 9 pairs; 41/40 ms; 521,928/200,033 ops/lane; IQR 1.005/1.021 | `Pcre2Slice` | 0.759 us | 0.073 us | 0.102 us | 0.76x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 77.468 us | 160.957 us | 0.48x | 0.48–0.48x | -83.182 us | 9 pairs; 40/42 ms; 511/261 ops/lane; IQR 1.005/1.011 | `Pcre2Slice` | 433.078 us | 99.339 us | 102.431 us | 0.76x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 37.054 us | 285.555 us | 0.13x | 0.13–0.13x | -248.606 us | 9 pairs; 39/41 ms; 1,055/145 ops/lane; IQR 1.011/1.012 | `Pcre2Character` | 422.443 us | 214.588 us | 225.144 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.426 us | 207.262 us | 0.05x | 0.05–0.05x | -197.700 us | 9 pairs; 40/41 ms; 4,229/197 ops/lane; IQR 1.003/1.008 | `Pcre2Backtracking` | 7.839 us | 218.585 us | 223.809 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 571.913 us | 202.486 us | 2.83x | 2.81–2.84x | +369.787 us | 9 pairs; 39/40 ms; 69/196 ops/lane; IQR 1.006/1.012 | `Pcre2SingleTokenRepeat` | 7.894 us | 225.552 us | 229.717 us | 2.49x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.502 us | 22.297 us | 0.16x | 0.15–0.16x | -18.795 us | 9 pairs; 40/38 ms; 11,322/1,692 ops/lane; IQR 1.006/1.014 | `Pcre2Literal` | 1.432 us | 4.156 us | 5.732 us | 0.61x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.417 us | 79.678 us | 0.12x | 0.12–0.12x | -70.233 us | 9 pairs; 39/41 ms; 4,170/516 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 9.190 us | 91.050 us | 92.923 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,144.350 us | 28,000.700 us | 0.15x | 0.14–0.16x | -23826.288 us | 9 pairs; 33/28 ms; 8/1 ops/lane; IQR 1.024/1.036 | `Pcre2Backtracking` | 5,743.933 us | 11,657.380 us | 17,966.467 us | 0.23x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Managed faster** | 16,013,977 B | 5,733.014 us | 52,629.400 us | 0.11x | 0.11–0.11x | -46805.786 us | 9 pairs; 40/53 ms; 7/1 ops/lane; IQR 1.018/1.014 | `Pcre2Backtracking` | 6,299.900 us | 42,613.600 us | 49,327.450 us | 0.12x | 3,552 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,895.405 us | 479,207.900 us | 0.00x | 0.00–0.00x | -477342.938 us | 9 pairs; 40/479 ms; 21/1 ops/lane; IQR 1.028/1.005 | `Pcre2Character` | 1,746.987 us | 22,940.633 us | 29,502.200 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,406.683 us | 11,114.250 us | 0.48x | 0.47–0.52x | -5776.000 us | 9 pairs; 32/44 ms; 6/4 ops/lane; IQR 1.008/1.056 | `Pcre2Literal` | 1,384.297 us | 1,321.082 us | 8,200.600 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,466.350 us | 141,834.900 us | 0.13x | 0.13–0.13x | -123373.600 us | 9 pairs; 37/142 ms; 2/1 ops/lane; IQR 1.016/1.012 | `Pcre2Backtracking` | 1,554.828 us | 501.908 us | 8,155.200 us | 2.26x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,715.150 us | 21,860.950 us | 0.22x | 0.21–0.22x | -17160.588 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.018/1.016 | `Pcre2Slice` | 2,792.433 us | 11,058.320 us | 18,855.900 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 23,186.500 us | 131,992.100 us | 0.18x | 0.17–0.18x | -108995.000 us | 9 pairs; 46/132 ms; 2/1 ops/lane; IQR 1.010/1.004 | `Pcre2Backtracking` | 1,844.450 us | 2,285.013 us | 10,065.640 us | 2.30x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 234.605 us | 995.638 us | 0.24x | 0.23–0.24x | -759.480 us | 9 pairs; 39/39 ms; 167/39 ops/lane; IQR 1.008/1.018 | `Pcre2Backtracking` | 279.728 us | 342.340 us | 563.218 us | 0.42x | 3,552 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Native faster** | 594,930 B | 13,539.600 us | 13,141.933 us | 1.03x | 1.03–1.04x | +430.567 us | 9 pairs; 41/39 ms; 3/3 ops/lane; IQR 1.011/1.009 | `Pcre2Backtracking` | 295.622 us | 15,694.200 us | 15,813.525 us | 0.86x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,957.146 us | 97,290.500 us | 0.03x | 0.03–0.03x | -94334.192 us | 9 pairs; 38/97 ms; 13/1 ops/lane; IQR 1.014/1.017 | `Pcre2Character` | 1,860.211 us | 19,344.400 us | 19,591.867 us | 0.15x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 182.123 us | 2,760.313 us | 0.07x | 0.07–0.07x | -2580.327 us | 9 pairs; 40/41 ms; 220/15 ops/lane; IQR 1.011/1.014 | `Pcre2SingleTokenRepeat` | 192.302 us | 992.096 us | 1,414.824 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Managed faster** | 594,930 B | 1,625.975 us | 19,848.200 us | 0.08x | 0.08–0.08x | -18234.625 us | 9 pairs; 39/40 ms; 24/2 ops/lane; IQR 1.007/1.013 | `Pcre2Backtracking` | 586.906 us | 10,832.340 us | 11,011.220 us | 0.15x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.135 us | 1.074 us | 0.13x | 0.13–0.13x | -0.940 us | 9 pairs; 39/39 ms; 287,210/36,594 ops/lane; IQR 1.010/1.012 | `Pcre2Slice` | 1.440 us | 0.251 us | 0.274 us | 0.49x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.527 us | 0.816 us | 0.64x | 0.64–0.64x | -0.294 us | 9 pairs; 39/40 ms; 73,656/49,326 ops/lane; IQR 1.012/1.010 | `Pcre2Literal` | 0.142 us | 0.145 us | 0.169 us | 3.11x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.306 us | 1.192 us | 0.26x | 0.26–0.26x | -0.885 us | 9 pairs; 40/42 ms; 129,427/35,608 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 1.055 us | 0.344 us | 0.370 us | 0.83x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.440 us | 1.129 us | 1.27x | 1.27–1.28x | +0.309 us | 9 pairs; 40/40 ms; 27,509/35,087 ops/lane; IQR 1.007/1.007 | `Pcre2Character` | 0.276 us | 0.437 us | 0.466 us | 3.09x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.171 us | 0.865 us | 0.20x | 0.20–0.20x | -0.694 us | 9 pairs; 41/40 ms; 241,652/46,524 ops/lane; IQR 1.018/1.004 | `Pcre2Slice` | 1.197 us | 0.151 us | 0.194 us | 0.88x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.926 us | 1.088 us | 0.85x | 0.85–0.85x | -0.163 us | 9 pairs; 36/42 ms; 38,458/38,487 ops/lane; IQR 1.007/1.006 | `Pcre2Slice` | 6.159 us | 0.304 us | 0.319 us | 2.90x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.782 us | 0.818 us | 0.95x | 0.95–0.96x | -0.038 us | 9 pairs; 40/40 ms; 50,676/48,913 ops/lane; IQR 1.004/1.005 | `Pcre2Literal` | 3.554 us | 0.180 us | 0.200 us | 3.92x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.965 us | 1.158 us | 0.83x | 0.83–0.84x | -0.194 us | 9 pairs; 39/41 ms; 40,072/35,387 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 5.203 us | 0.399 us | 0.424 us | 2.28x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.709 us | 1.119 us | 1.53x | 1.52–1.54x | +0.592 us | 9 pairs; 39/39 ms; 23,085/34,950 ops/lane; IQR 1.010/1.012 | `Pcre2Character` | 3.645 us | 0.473 us | 0.510 us | 3.35x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.797 us | 0.854 us | 0.93x | 0.93–0.94x | -0.058 us | 9 pairs; 39/39 ms; 49,030/45,736 ops/lane; IQR 1.005/1.007 | `Pcre2Slice` | 2.691 us | 0.181 us | 0.221 us | 3.61x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.613 us | 1.090 us | 0.56x | 0.56–0.57x | -0.475 us | 9 pairs; 39/39 ms; 63,519/36,096 ops/lane; IQR 1.003/1.008 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.534 us | 0.821 us | 0.65x | 0.65–0.65x | -0.287 us | 9 pairs; 40/41 ms; 74,762/49,747 ops/lane; IQR 1.010/1.008 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.665 us | 1.165 us | 0.57x | 0.57–0.57x | -0.503 us | 9 pairs; 40/40 ms; 59,414/33,978 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.402 us | 1.125 us | 1.25x | 1.25–1.25x | +0.283 us | 9 pairs; 41/40 ms; 29,212/35,210 ops/lane; IQR 1.016/1.015 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.483 us | 0.866 us | 0.56x | 0.56–0.56x | -0.384 us | 9 pairs; 40/41 ms; 83,019/47,117 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 62.250 us | — | — | — | — | — | `—` | 67.671 us | 97.852 us | 99.415 us | 0.63x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 9.104 us | — | — | — | — | — | `—` | 3.243 us | 0.357 us | 0.378 us | 24.09x | 48,792 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.087 us | — | — | — | — | — | `—` | 0.885 us | 0.239 us | 0.267 us | 4.07x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.226 us | — | — | — | — | — | `—` | 2.872 us | 0.454 us | 0.480 us | 2.55x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 2.123 us | — | — | — | — | — | `—` | 0.975 us | 0.530 us | 0.582 us | 3.65x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 5.212 us | — | — | — | — | — | `—` | 1.685 us | 0.256 us | 0.306 us | 17.04x | 7,192 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Native faster** | 5 B | 0.121 us | 0.084 us | 1.44x | 1.41–1.46x | +0.037 us | 9 pairs; 35/39 ms; 292,182/464,355 ops/lane; IQR 1.011/1.016 | `Pcre2Character` | 0 B | 0 B | 2.231 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.012 us | 0.126 us | 0.10x | 0.10–0.10x | -0.113 us | 9 pairs; 39/41 ms; 3,128,708/322,529 ops/lane; IQR 1.012/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 127.719 us | 111,888 B |
| `pcre2/conditional-accept-negative-lookahead` | **Inconclusive** | 3 B | 0.454 us | 0.105 us | 4.34x | 4.17–4.37x | +0.349 us | 9 pairs; 36/39 ms; 78,887/374,326 ops/lane; IQR 1.008/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 21.113 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.464 us | 0.115 us | 4.12x | 4.06–4.16x | +0.351 us | 9 pairs; 35/38 ms; 75,936/334,397 ops/lane; IQR 1.031/1.019 | `Pcre2Backtracking` | 0 B | 0 B | 69.806 us | 65,152 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.463 us | 0.105 us | 4.40x | 4.39–4.44x | +0.357 us | 9 pairs; 36/41 ms; 77,466/393,252 ops/lane; IQR 1.017/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 71.306 us | 65,112 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.577 us | 0.340 us | 1.69x | 1.68–1.71x | +0.236 us | 9 pairs; 37/41 ms; 64,347/119,179 ops/lane; IQR 1.006/1.004 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.623 us | 0.02x | 0.02–0.02x | -0.611 us | 9 pairs; 42/40 ms; 3,536,751/64,613 ops/lane; IQR 1.040/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 23.288 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Inconclusive** | 6 B | 0.012 us | 0.507 us | 0.02x | 0.02–0.02x | -0.495 us | 9 pairs; 41/40 ms; 3,387,311/78,546 ops/lane; IQR 1.039/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 23.406 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.513 us | 0.107 us | 4.84x | 4.71–4.86x | +0.406 us | 9 pairs; 37/40 ms; 72,996/374,485 ops/lane; IQR 1.005/1.026 | `Pcre2Backtracking` | 0 B | 0 B | 18.944 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.188 us | 10.285 us | 0.80x | 0.80–0.80x | -2.094 us | 9 pairs; 39/39 ms; 4,750/3,751 ops/lane; IQR 1.005/1.005 | `Pcre2Literal` | 0 B | 0 B | 0.744 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.371 us | 1.039 us | 0.36x | 0.35–0.36x | -0.667 us | 9 pairs; 46/39 ms; 124,867/37,739 ops/lane; IQR 1.009/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 126.106 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.323 us | 0.449 us | 0.72x | 0.71–0.73x | -0.126 us | 9 pairs; 38/41 ms; 117,190/90,796 ops/lane; IQR 1.010/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 114.125 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.322 us | 0.458 us | 0.71x | 0.70–0.71x | -0.134 us | 9 pairs; 41/42 ms; 126,420/91,787 ops/lane; IQR 1.012/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 142.331 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.296 us | 0.580 us | 0.51x | 0.51–0.51x | -0.284 us | 9 pairs; 34/42 ms; 114,015/72,043 ops/lane; IQR 1.010/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 101.950 us | 81,520 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.293 us | 0.390 us | 0.75x | 0.73–0.76x | -0.098 us | 9 pairs; 34/41 ms; 116,155/104,506 ops/lane; IQR 1.010/1.026 | `Pcre2Backtracking` | 0 B | 0 B | 215.519 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.063 us | 8.697 us | 0.58x | 0.58–0.59x | -3.633 us | 9 pairs; 41/41 ms; 8,061/4,723 ops/lane; IQR 1.013/1.006 | `Pcre2Character` | 0 B | 0 B | 1.406 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Inconclusive** | 13 B | 0.265 us | 0.311 us | 0.85x | 0.85–0.97x | -0.047 us | 9 pairs; 36/38 ms; 136,300/121,736 ops/lane; IQR 1.117/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 93.119 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.271 us | 0.402 us | 0.68x | 0.67–0.69x | -0.128 us | 9 pairs; 37/39 ms; 137,010/95,943 ops/lane; IQR 1.021/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 125.000 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.129 us | — | — | — | — | — | `—` | 0 B | — | 13.725 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.185 us | 0.293 us | 0.63x | 0.63–0.64x | -0.108 us | 9 pairs; 40/38 ms; 216,791/131,482 ops/lane; IQR 1.016/1.018 | `Pcre2Backtracking` | 0 B | 0 B | 52.919 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.007 us | — | — | — | — | — | `—` | 0 B | — | 16.050 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.519 us | 0.664 us | 0.78x | 0.78–0.79x | -0.145 us | 9 pairs; 36/40 ms; 70,056/60,823 ops/lane; IQR 1.008/1.021 | `Pcre2Slice` | 0 B | 0 B | 63.444 us | 55,504 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.803 us | 28.518 us | 0.34x | 0.34–0.35x | -18.700 us | 9 pairs; 41/40 ms; 4,147/1,390 ops/lane; IQR 1.007/1.007 | `Pcre2Literal` | 0 B | 0 B | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.972 us | 1.046 us | 0.93x | 0.92–0.94x | -0.074 us | 9 pairs; 37/41 ms; 38,216/38,817 ops/lane; IQR 1.005/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 128.456 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 1.026 us | 1.232 us | 0.83x | 0.82–0.84x | -0.205 us | 9 pairs; 37/41 ms; 35,853/33,509 ops/lane; IQR 1.013/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 115.787 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Inconclusive** | 31 B | 0.991 us | 1.004 us | 0.98x | 0.97–0.99x | -0.016 us | 9 pairs; 37/40 ms; 37,487/40,086 ops/lane; IQR 1.018/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 141.144 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 1.036 us | 1.147 us | 0.90x | 0.90–0.91x | -0.111 us | 9 pairs; 41/42 ms; 39,196/36,232 ops/lane; IQR 1.004/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 101.269 us | 81,520 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.940 us | 0.929 us | 1.01x | 1.00–1.01x | +0.007 us | 9 pairs; 40/39 ms; 42,095/42,441 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 216.156 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.709 us | 8.680 us | 0.66x | 0.66–0.66x | -2.974 us | 9 pairs; 41/40 ms; 7,110/4,563 ops/lane; IQR 1.004/1.004 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.903 us | 0.875 us | 1.04x | 1.03–1.04x | +0.032 us | 9 pairs; 38/41 ms; 42,215/46,577 ops/lane; IQR 1.008/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 92.794 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.907 us | 0.948 us | 0.95x | 0.94–0.96x | -0.045 us | 9 pairs; 40/42 ms; 43,916/44,388 ops/lane; IQR 1.014/1.020 | `Pcre2Backtracking` | 0 B | 0 B | 121.944 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.414 us | — | — | — | — | — | `—` | 0 B | — | 14.244 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.830 us | 0.854 us | 0.97x | 0.97–0.98x | -0.024 us | 9 pairs; 37/40 ms; 44,323/46,981 ops/lane; IQR 1.008/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 53.025 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.315 us | — | — | — | — | — | `—` | 0 B | — | 13.537 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.837 us | 1.245 us | 0.67x | 0.67–0.68x | -0.405 us | 9 pairs; 39/40 ms; 46,926/32,162 ops/lane; IQR 1.007/1.008 | `Pcre2Slice` | 0 B | 0 B | 61.494 us | 55,504 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.315 us | 2.220 us | 0.59x | 0.59–0.60x | -0.905 us | 9 pairs; 40/41 ms; 30,399/18,398 ops/lane; IQR 1.002/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.781 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.632 us | 1.055 us | 0.60x | 0.60–0.60x | -0.423 us | 9 pairs; 39/40 ms; 61,729/37,744 ops/lane; IQR 1.010/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 128.012 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.714 us | 1.236 us | 0.58x | 0.58–0.58x | -0.518 us | 9 pairs; 39/40 ms; 54,094/32,294 ops/lane; IQR 1.011/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 110.900 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.610 us | 1.015 us | 0.60x | 0.60–0.60x | -0.404 us | 9 pairs; 40/39 ms; 65,013/38,428 ops/lane; IQR 1.013/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 140.975 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.691 us | 1.152 us | 0.60x | 0.60–0.60x | -0.463 us | 9 pairs; 45/39 ms; 65,171/34,278 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 103.263 us | 81,520 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.610 us | 0.927 us | 0.66x | 0.65–0.66x | -0.318 us | 9 pairs; 41/40 ms; 66,931/42,665 ops/lane; IQR 1.009/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 213.537 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.810 us | 2.151 us | 0.84x | 0.84–0.85x | -0.339 us | 9 pairs; 39/41 ms; 21,302/18,882 ops/lane; IQR 1.005/1.008 | `Pcre2Character` | 0 B | 0 B | 1.425 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.595 us | 0.873 us | 0.68x | 0.68–0.69x | -0.277 us | 9 pairs; 41/40 ms; 68,498/46,204 ops/lane; IQR 1.011/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 92.900 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.595 us | 0.951 us | 0.63x | 0.62–0.63x | -0.353 us | 9 pairs; 39/42 ms; 65,366/44,257 ops/lane; IQR 1.014/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 122.450 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.036 us | — | — | — | — | — | `—` | 0 B | — | 13.988 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.493 us | 0.853 us | 0.58x | 0.58–0.58x | -0.359 us | 9 pairs; 39/39 ms; 78,667/45,888 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 53.462 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.915 us | — | — | — | — | — | `—` | 0 B | — | 14.344 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.562 us | 1.238 us | 0.45x | 0.45–0.46x | -0.679 us | 9 pairs; 39/40 ms; 69,933/32,489 ops/lane; IQR 1.011/1.005 | `Pcre2Slice` | 0 B | 0 B | 63.956 us | 55,504 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.755 us | — | — | — | — | — | `—` | 480 B | — | 0.738 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.167 us | — | — | — | — | — | `—` | 48 B | — | 129.012 us | 111,888 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.270 us | — | — | — | — | — | `—` | 48 B | — | 113.619 us | 99,232 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.136 us | — | — | — | — | — | `—` | 48 B | — | 140.588 us | 127,456 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.150 us | — | — | — | — | — | `—` | 48 B | — | 101.206 us | 81,520 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.128 us | — | — | — | — | — | `—` | 48 B | — | 220.794 us | 179,184 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.221 us | — | — | — | — | — | `—` | 64 B | — | 1.325 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.142 us | — | — | — | — | — | `—` | 40 B | — | 89.888 us | 72,544 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.154 us | — | — | — | — | — | `—` | 40 B | — | 122.062 us | 110,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 22.366 us | — | — | — | — | — | `—` | 1,224 B | — | 14.019 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.084 us | — | — | — | — | — | `—` | 48 B | — | 51.706 us | 53,264 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 17.307 us | — | — | — | — | — | `—` | 936 B | — | 13.925 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 5.942 us | — | — | — | — | — | `—` | 28,936 B | — | 63.312 us | 55,504 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 28.793–217.453 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.345–94.390 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.896–12.751 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 7.279–49.085 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.036–1.454 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.851–45.821 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.770–22.579 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.196–99.916 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.144–72.910 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.247–5.697 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 49.804–386.975 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.073–0.086 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 41.684–326.221 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.860–5.134 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 18.134–140.190 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 165.793–1,344.115 us |

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
