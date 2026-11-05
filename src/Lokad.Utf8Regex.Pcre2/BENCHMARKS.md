<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent requested work against `Utf8Regex` and `Regex` on the recorded .NET runtime. `.NET + decode` decodes the verified-valid UTF-8 subject for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. Malformed-input handling is not measured by these rows. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

Enumeration and MatchMany consume byte ranges. Managed public results also supply derived UTF-16 coordinates; the native comparator returns byte ranges. These are public API costs under the stated result contracts, not isolated matching-kernel comparisons. Native global enumeration validates its first search and reuses that validation for subsequent matches.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `FDE07F93B0C2C7A826BAE47BC6A9C1300EC08DB3E7972ECE29574B254D1FE4F2`
- Latest managed row measurement: `2026-11-05T16:55:22.0656502+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-11-05T16:55:22.0656502+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `44/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `77` managed faster, `1` equivalent, `11` native faster, `12` inconclusive, `0` unqualified, `25` excluded
- Rows with paired qualification evidence: `101/101`
- Qualification processor sets: `single-least-contended-allowed-processor 0x10`, `single-least-contended-allowed-processor 0x2`, `single-least-contended-allowed-processor 0x20`, `single-least-contended-allowed-processor 0x4`, `single-least-contended-allowed-processor 0x40`, `single-least-contended-allowed-processor 0x8`, `single-least-contended-allowed-processor 0x80`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `bc820332e517` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `bc820332e517` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Inconclusive** | 4,103 B | 0.673 us | 2.382 us | 0.28x | 0.24–0.31x | -1.711 us | 9 pairs; 36/51 ms; 53,683/21,250 ops/lane; IQR 1.009/1.235 | `Pcre2Slice` | 463.695 us | 0.280 us | 0.910 us | 0.74x | 1,248 B | 0 B |
| `backtracking/capture-backreference` | **Managed faster** | 22,528 B | 0.808 us | 10.586 us | 0.08x | 0.08–0.08x | -9.754 us | 9 pairs; 35/39 ms; 43,286/3,671 ops/lane; IQR 1.034/1.028 | `Pcre2Slice` | 0.669 us | 0.096 us | 3.612 us | 0.22x | 720 B | 0 B |
| `common/backtracking` | **Managed faster** | 49 B | 0.011 us | 1.243 us | 0.01x | 0.01–0.01x | -1.232 us | 9 pairs; 40/39 ms; 3,624,111/31,540 ops/lane; IQR 1.018/1.005 | `Pcre2Slice` | 0.105 us | 1.298 us | 1.257 us | 0.01x | 0 B | 0 B |
| `common/date-match` | **Native faster** | 47 B | 0.679 us | 0.155 us | 4.37x | 4.36–4.39x | +0.524 us | 9 pairs; 38/40 ms; 55,637/259,158 ops/lane; IQR 1.008/1.005 | `Pcre2Backtracking` | 0.046 us | 0.123 us | 0.161 us | 4.22x | 0 B | 0 B |
| `common/date-miss` | **Native faster** | 47 B | 2.245 us | 0.575 us | 3.98x | 3.88–4.00x | +1.681 us | 9 pairs; 38/40 ms; 17,045/69,829 ops/lane; IQR 1.019/1.025 | `Pcre2Backtracking` | 0.123 us | 0.784 us | 0.819 us | 2.74x | 0 B | 0 B |
| `common/email-match` | **Managed faster** | 34 B | 0.099 us | 0.295 us | 0.33x | 0.33–0.34x | -0.196 us | 9 pairs; 39/39 ms; 395,185/132,104 ops/lane; IQR 1.009/1.006 | `Pcre2Slice` | 0.114 us | 0.324 us | 0.356 us | 0.28x | 0 B | 0 B |
| `common/email-miss` | **Managed faster** | 35 B | 0.105 us | 0.355 us | 0.29x | 0.29–0.30x | -0.250 us | 9 pairs; 35/39 ms; 332,574/110,573 ops/lane; IQR 1.005/1.003 | `Pcre2Slice` | 0.118 us | 0.457 us | 0.484 us | 0.22x | 0 B | 0 B |
| `common/ip-match` | **Managed faster** | 15 B | 0.080 us | 0.291 us | 0.27x | 0.27–0.27x | -0.212 us | 9 pairs; 39/39 ms; 495,970/134,144 ops/lane; IQR 1.002/1.006 | `Pcre2Slice` | 0.089 us | 0.238 us | 0.261 us | 0.30x | 0 B | 0 B |
| `common/ip-miss` | **Managed faster** | 15 B | 0.079 us | 0.311 us | 0.26x | 0.25–0.26x | -0.232 us | 9 pairs; 40/39 ms; 502,199/125,145 ops/lane; IQR 1.022/1.007 | `Pcre2Slice` | 0.089 us | 0.255 us | 0.280 us | 0.28x | 0 B | 0 B |
| `common/one-node-backtracking` | **Native faster** | 52 B | 0.171 us | 0.076 us | 2.25x | 2.23–2.33x | +0.095 us | 9 pairs; 40/38 ms; 232,006/502,885 ops/lane; IQR 1.010/1.021 | `Pcre2Slice` | 0.089 us | 1.027 us | 1.052 us | 0.16x | 0 B | 0 B |
| `common/uri-match` | **Managed faster** | 46 B | 0.033 us | 0.197 us | 0.17x | 0.17–0.17x | -0.164 us | 9 pairs; 40/40 ms; 1,190,605/201,107 ops/lane; IQR 1.002/1.008 | `Pcre2Backtracking` | 0.108 us | 0.314 us | 0.347 us | 0.10x | 0 B | 0 B |
| `common/uri-miss` | **Managed faster** | 19 B | 0.060 us | 0.604 us | 0.10x | 0.10–0.10x | -0.545 us | 9 pairs; 40/40 ms; 672,918/66,423 ops/lane; IQR 1.004/1.013 | `Pcre2Backtracking` | 0.133 us | 0.241 us | 0.268 us | 0.22x | 0 B | 0 B |
| `industry/boostdocs-credit-card-match` | **Managed faster** | 18 B | 0.033 us | 0.152 us | 0.22x | 0.21–0.22x | -0.119 us | 9 pairs; 38/39 ms; 1,151,804/257,979 ops/lane; IQR 1.018/1.005 | `Pcre2Backtracking` | 0.111 us | 0.178 us | 0.200 us | 0.16x | 0 B | 0 B |
| `industry/boostdocs-date-match` | **Managed faster** | 10 B | 0.021 us | 0.112 us | 0.19x | 0.18–0.19x | -0.091 us | 9 pairs; 40/41 ms; 1,942,794/362,983 ops/lane; IQR 1.016/1.019 | `Pcre2Backtracking` | 0.066 us | 0.092 us | 0.110 us | 0.19x | 0 B | 0 B |
| `industry/boostdocs-float-match` | **Managed faster** | 8 B | 0.057 us | 0.120 us | 0.48x | 0.47–0.48x | -0.063 us | 9 pairs; 39/40 ms; 688,852/334,495 ops/lane; IQR 1.008/1.015 | `Pcre2Backtracking` | 0.016 us | 0.091 us | 0.113 us | 0.51x | 0 B | 0 B |
| `industry/boostdocs-ftp-line-match` | **Managed faster** | 67 B | 0.084 us | 0.279 us | 0.30x | 0.30–0.31x | -0.196 us | 9 pairs; 39/40 ms; 469,782/141,548 ops/lane; IQR 1.020/1.013 | `Pcre2Slice` | 0.094 us | 0.116 us | 0.150 us | 0.56x | 0 B | 0 B |
| `industry/boostdocs-postcode-match` | **Managed faster** | 7 B | 0.024 us | 0.110 us | 0.22x | 0.20–0.22x | -0.086 us | 9 pairs; 42/40 ms; 1,738,355/367,750 ops/lane; IQR 1.031/1.016 | `Pcre2Backtracking` | 0.019 us | 0.097 us | 0.115 us | 0.21x | 0 B | 0 B |
| `literal/absolute-anchored` | **Inconclusive** | 4,103 B | 0.117 us | 2.168 us | 0.05x | 0.05–0.07x | -2.050 us | 9 pairs; 40/45 ms; 342,914/20,984 ops/lane; IQR 1.007/1.142 | `Pcre2Literal` | 0.087 us | 0.050 us | 0.595 us | 0.20x | 0 B | 0 B |
| `literal/early` | **Inconclusive** | 4,103 B | 0.121 us | 2.433 us | 0.05x | 0.05–0.06x | -2.314 us | 9 pairs; 38/55 ms; 317,148/22,594 ops/lane; IQR 1.005/1.309 | `Pcre2Literal` | 0.147 us | 0.053 us | 0.614 us | 0.20x | 0 B | 0 B |
| `literal/late` | **Inconclusive** | 4,103 B | 0.146 us | 1.906 us | 0.08x | 0.07–0.08x | -1.759 us | 9 pairs; 38/43 ms; 257,702/22,744 ops/lane; IQR 1.006/1.102 | `Pcre2Literal` | 0.214 us | 0.231 us | 0.824 us | 0.18x | 0 B | 0 B |
| `literal/missing` | **Inconclusive** | 4,096 B | 0.150 us | 2.244 us | 0.07x | 0.06–0.08x | -2.093 us | 9 pairs; 40/50 ms; 267,246/22,060 ops/lane; IQR 1.005/1.179 | `Pcre2Literal` | 0.189 us | 0.204 us | 0.770 us | 0.19x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.009 us | 0.099 us | 0.09x | 0.09–0.09x | -0.090 us | 9 pairs; 39/39 ms; 4,494,539/399,634 ops/lane; IQR 1.006/1.006 | `Pcre2Slice` | 0.253 us | 0.071 us | 0.090 us | 0.10x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.067 us | 0.086 us | 0.78x | 0.77–0.79x | -0.019 us | 9 pairs; 40/40 ms; 591,571/463,465 ops/lane; IQR 1.007/1.028 | `Pcre2Literal` | 0.133 us | 0.049 us | 0.072 us | 0.94x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.013 us | 0.107 us | 0.13x | 0.12–0.13x | -0.094 us | 9 pairs; 39/40 ms; 2,931,385/370,048 ops/lane; IQR 1.004/1.011 | `Pcre2Backtracking` | 0.245 us | 0.093 us | 0.112 us | 0.12x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.117 us | 0.123 us | 0.94x | 0.93–0.95x | -0.007 us | 9 pairs; 41/38 ms; 350,514/313,694 ops/lane; IQR 1.016/1.036 | `Pcre2Character` | 0.137 us | 0.113 us | 0.147 us | 0.80x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Inconclusive** | 67 B | 0.081 us | 0.199 us | 0.40x | 0.39–0.41x | -0.118 us | 9 pairs; 43/38 ms; 534,816/193,547 ops/lane; IQR 1.006/1.040 | `Pcre2Slice` | 0.430 us | 0.074 us | 0.105 us | 0.77x | 0 B | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Managed faster** | 7,168 B | 81.586 us | 158.978 us | 0.51x | 0.51–0.51x | -77.727 us | 9 pairs; 39/40 ms; 484/249 ops/lane; IQR 1.005/1.006 | `Pcre2Slice` | 323.310 us | 109.447 us | 106.896 us | 0.76x | 0 B | 0 B |
| `character/unicode-class-dense` | **Managed faster** | 7,168 B | 36.971 us | 315.502 us | 0.12x | 0.12–0.12x | -278.521 us | 9 pairs; 40/47 ms; 1,095/148 ops/lane; IQR 1.007/1.006 | `Pcre2Character` | 282.627 us | 225.805 us | 226.600 us | 0.16x | 0 B | 0 B |
| `common/matches-boundary` | **Managed faster** | 8,208 B | 9.423 us | 208.934 us | 0.05x | 0.04–0.05x | -199.499 us | 9 pairs; 39/41 ms; 4,136/198 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 7.946 us | 229.161 us | 229.473 us | 0.04x | 0 B | 0 B |
| `common/matches-set` | **Native faster** | 8,208 B | 234.754 us | 215.719 us | 1.09x | 1.08–1.09x | +19.297 us | 9 pairs; 39/42 ms; 166/197 ops/lane; IQR 1.004/1.009 | `Pcre2SingleTokenRepeat` | 7.859 us | 241.241 us | 238.925 us | 0.98x | 0 B | 0 B |
| `common/matches-word` | **Managed faster** | 8,208 B | 3.501 us | 22.495 us | 0.16x | 0.15–0.16x | -18.998 us | 9 pairs; 40/39 ms; 11,425/1,755 ops/lane; IQR 1.015/1.021 | `Pcre2Literal` | 1.369 us | 4.219 us | 6.039 us | 0.58x | 0 B | 0 B |
| `common/matches-words` | **Managed faster** | 8,208 B | 9.495 us | 80.272 us | 0.12x | 0.12–0.12x | -70.777 us | 9 pairs; 39/40 ms; 4,132/495 ops/lane; IQR 1.009/1.014 | `Pcre2Backtracking` | 9.246 us | 93.660 us | 92.772 us | 0.10x | 0 B | 0 B |
| `industry/leipzig-name-family-count` | **Inconclusive** | 16,013,977 B | 4,023.188 us | 27,584.400 us | 0.14x | 0.14–0.15x | -23631.300 us | 9 pairs; 32/28 ms; 8/1 ops/lane; IQR 1.021/1.052 | `Pcre2Backtracking` | 5,361.500 us | 12,489.620 us | 19,553.933 us | 0.21x | 0 B | 0 B |
| `industry/leipzig-river-window-count` | **Inconclusive** | 16,013,977 B | 3,404.992 us | 54,542.400 us | 0.06x | 0.06–0.06x | -51089.725 us | 9 pairs; 41/55 ms; 12/1 ops/lane; IQR 1.017/1.018 | `Pcre2Backtracking` | 3,785.071 us | 44,255.350 us | 51,680.500 us | 0.07x | 1,056 B | 0 B |
| `industry/leipzig-symbol-count` | **Inconclusive** | 16,013,977 B | 1,900.438 us | 473,064.500 us | 0.00x | 0.00–0.00x | -471122.033 us | 9 pairs; 40/473 ms; 21/1 ops/lane; IQR 1.036/1.008 | `Pcre2Character` | 1,880.530 us | 23,175.800 us | 29,784.050 us | 0.06x | 0 B | 0 B |
| `industry/leipzig-twain-count` | **Inconclusive** | 16,013,977 B | 5,529.267 us | 11,497.600 us | 0.48x | 0.46–0.50x | -6029.017 us | 9 pairs; 33/34 ms; 6/3 ops/lane; IQR 1.014/1.070 | `Pcre2Literal` | 1,369.192 us | 1,384.079 us | 8,387.983 us | 0.66x | 0 B | 0 B |
| `industry/mariomka-email-count` | **Managed faster** | 6,839,410 B | 18,471.700 us | 141,824.900 us | 0.13x | 0.13–0.13x | -123440.850 us | 9 pairs; 37/142 ms; 2/1 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 1,240.322 us | 506.618 us | 8,417.050 us | 2.19x | 0 B | 0 B |
| `industry/mariomka-ip-count` | **Managed faster** | 6,839,410 B | 4,734.825 us | 21,758.200 us | 0.22x | 0.22–0.22x | -17016.850 us | 9 pairs; 38/44 ms; 8/2 ops/lane; IQR 1.013/1.005 | `Pcre2Slice` | 2,390.773 us | 11,094.680 us | 18,625.833 us | 0.25x | 0 B | 0 B |
| `industry/mariomka-uri-count` | **Managed faster** | 6,839,410 B | 21,919.900 us | 131,989.600 us | 0.17x | 0.16–0.17x | -110320.200 us | 9 pairs; 44/132 ms; 2/1 ops/lane; IQR 1.022/1.007 | `Pcre2Backtracking` | 1,503.576 us | 2,302.252 us | 10,163.760 us | 2.16x | 0 B | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Managed faster** | 594,930 B | 167.495 us | 956.567 us | 0.18x | 0.17–0.18x | -788.510 us | 9 pairs; 40/40 ms; 236/42 ops/lane; IQR 1.013/1.016 | `Pcre2Backtracking` | 164.832 us | 350.677 us | 573.758 us | 0.29x | 1,528 B | 0 B |
| `industry/rust-sherlock-ing-count` | **Managed faster** | 594,930 B | 12,385.667 us | 13,277.733 us | 0.93x | 0.93–0.94x | -886.833 us | 9 pairs; 37/40 ms; 3/3 ops/lane; IQR 1.006/1.006 | `Pcre2Backtracking` | 303.118 us | 15,519.475 us | 15,752.225 us | 0.79x | 0 B | 0 B |
| `industry/rust-sherlock-letter-count` | **Managed faster** | 594,930 B | 2,888.269 us | 97,654.700 us | 0.03x | 0.03–0.03x | -94766.431 us | 9 pairs; 38/98 ms; 13/1 ops/lane; IQR 1.008/1.004 | `Pcre2Character` | 1,875.315 us | 19,662.367 us | 19,977.967 us | 0.14x | 0 B | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Managed faster** | 594,930 B | 183.446 us | 2,749.171 us | 0.07x | 0.07–0.07x | -2566.500 us | 9 pairs; 39/38 ms; 215/14 ops/lane; IQR 1.009/1.015 | `Pcre2SingleTokenRepeat` | 187.997 us | 997.936 us | 1,404.867 us | 0.13x | 0 B | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Inconclusive** | 594,930 B | 1,557.708 us | 19,985.650 us | 0.08x | 0.08–0.08x | -18427.942 us | 9 pairs; 39/40 ms; 25/2 ops/lane; IQR 1.008/1.014 | `Pcre2Backtracking` | 577.370 us | 10,754.940 us | 10,928.980 us | 0.14x | 0 B | 0 B |
| `simple/ab-plus` | **Managed faster** | 16 B | 0.153 us | 1.087 us | 0.14x | 0.14–0.14x | -0.937 us | 9 pairs; 39/40 ms; 254,155/36,360 ops/lane; IQR 1.015/1.004 | `Pcre2Slice` | 0.648 us | 0.261 us | 0.277 us | 0.55x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.498 us | 0.818 us | 0.61x | 0.60–0.61x | -0.322 us | 9 pairs; 41/40 ms; 82,235/48,290 ops/lane; IQR 1.009/1.006 | `Pcre2Literal` | 0.141 us | 0.148 us | 0.174 us | 2.87x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.299 us | 1.172 us | 0.25x | 0.25–0.26x | -0.873 us | 9 pairs; 40/42 ms; 135,354/35,958 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 1.077 us | 0.351 us | 0.373 us | 0.80x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.900 us | 1.123 us | 0.80x | 0.80–0.81x | -0.224 us | 9 pairs; 39/40 ms; 42,771/35,456 ops/lane; IQR 1.007/1.003 | `Pcre2Character` | 0.279 us | 0.419 us | 0.472 us | 1.91x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.153 us | 0.850 us | 0.18x | 0.18–0.18x | -0.697 us | 9 pairs; 41/40 ms; 263,996/46,666 ops/lane; IQR 1.008/1.012 | `Pcre2Slice` | 0.795 us | 0.155 us | 0.200 us | 0.77x | 0 B | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.883 us | 1.096 us | 0.81x | 0.80–0.81x | -0.210 us | 9 pairs; 40/41 ms; 44,744/37,439 ops/lane; IQR 1.006/1.007 | `Pcre2Slice` | 2.991 us | 0.305 us | 0.322 us | 2.74x | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.799 us | 0.828 us | 0.96x | 0.96–0.97x | -0.030 us | 9 pairs; 40/40 ms; 50,439/48,733 ops/lane; IQR 1.007/1.008 | `Pcre2Literal` | 3.518 us | 0.179 us | 0.199 us | 4.02x | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.969 us | 1.196 us | 0.81x | 0.81–0.81x | -0.229 us | 9 pairs; 36/41 ms; 36,914/34,222 ops/lane; IQR 1.005/1.005 | `Pcre2Backtracking` | 5.231 us | 0.401 us | 0.424 us | 2.28x | 0 B | 0 B |
| `simple/httpclient-caseless` | **Native faster** | 45 B | 1.230 us | 1.143 us | 1.08x | 1.07–1.08x | +0.086 us | 9 pairs; 42/40 ms; 33,846/34,781 ops/lane; IQR 1.004/1.002 | `Pcre2Character` | 3.598 us | 0.478 us | 0.515 us | 2.39x | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.802 us | 0.852 us | 0.94x | 0.94–0.95x | -0.050 us | 9 pairs; 40/41 ms; 49,826/48,236 ops/lane; IQR 1.005/1.004 | `Pcre2Slice` | 2.056 us | 0.178 us | 0.222 us | 3.61x | 0 B | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Managed faster** | 16 B | 0.550 us | 1.091 us | 0.50x | 0.50–0.51x | -0.541 us | 9 pairs; 39/39 ms; 71,665/36,023 ops/lane; IQR 1.008/1.007 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |
| `simple/foo-dense` | **Managed faster** | 18 B | 0.542 us | 0.824 us | 0.66x | 0.66–0.66x | -0.284 us | 9 pairs; 39/41 ms; 71,325/50,075 ops/lane; IQR 1.006/1.005 | `Pcre2Literal` | — | — | — | — | 0 B | 0 B |
| `simple/foo-optional-bar` | **Managed faster** | 23 B | 0.695 us | 1.207 us | 0.58x | 0.57–0.58x | -0.510 us | 9 pairs; 41/41 ms; 58,411/33,571 ops/lane; IQR 1.006/1.003 | `Pcre2Backtracking` | — | — | — | — | 0 B | 0 B |
| `simple/httpclient-caseless` | **Managed faster** | 45 B | 0.908 us | 1.127 us | 0.81x | 0.80–0.81x | -0.215 us | 9 pairs; 40/40 ms; 43,585/35,857 ops/lane; IQR 1.012/1.017 | `Pcre2Character` | — | — | — | — | 0 B | 0 B |
| `simple/loglevel-multiline` | **Managed faster** | 67 B | 0.474 us | 0.854 us | 0.55x | 0.55–0.56x | -0.380 us | 9 pairs; 41/40 ms; 86,163/46,604 ops/lane; IQR 1.004/1.004 | `Pcre2Slice` | — | — | — | — | 0 B | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Regex elapsed | .NET predecoded elapsed | .NET + decode elapsed | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 60.207 us | — | — | — | — | — | `—` | 25.172 us | 97.613 us | 100.634 us | 0.60x | 13,648 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 4.884 us | — | — | — | — | — | `—` | 1.629 us | 0.352 us | 0.383 us | 12.76x | 7,056 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 1.077 us | — | — | — | — | — | `—` | 0.915 us | 0.237 us | 0.268 us | 4.02x | 48 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.244 us | — | — | — | — | — | `—` | 3.069 us | 0.454 us | 0.487 us | 2.55x | 48 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.485 us | — | — | — | — | — | `—` | 1.031 us | 0.536 us | 0.578 us | 2.57x | 56 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 3.823 us | — | — | — | — | — | `—` | 1.529 us | 0.262 us | 0.314 us | 12.18x | 720 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Inconclusive** | 5 B | 0.086 us | 0.084 us | 1.02x | 1.02–1.02x | +0.002 us | 9 pairs; 37/42 ms; 430,841/499,144 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0 B | 0 B | 3.644 us | 2,712 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.013 us | 0.141 us | 0.09x | 0.09–0.09x | -0.128 us | 9 pairs; 40/38 ms; 3,099,606/272,718 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 129.881 us | 111,912 B |
| `pcre2/conditional-accept-negative-lookahead` | **Native faster** | 3 B | 0.386 us | 0.104 us | 3.73x | 3.70–3.76x | +0.283 us | 9 pairs; 39/39 ms; 99,854/380,783 ops/lane; IQR 1.010/1.004 | `Pcre2Backtracking` | 0 B | 0 B | 20.288 us | 19,056 B |
| `pcre2/conditional-lookahead` | **Native faster** | 3 B | 0.395 us | 0.116 us | 3.38x | 3.35–3.42x | +0.279 us | 9 pairs; 36/39 ms; 90,093/336,951 ops/lane; IQR 1.016/1.017 | `Pcre2Backtracking` | 0 B | 0 B | 70.794 us | 65,176 B |
| `pcre2/conditional-negative-lookahead` | **Native faster** | 3 B | 0.365 us | 0.104 us | 3.49x | 3.46–3.53x | +0.260 us | 9 pairs; 35/40 ms; 96,545/384,529 ops/lane; IQR 1.006/1.010 | `Pcre2Backtracking` | 0 B | 0 B | 70.069 us | 65,136 B |
| `pcre2/grapheme-cluster` | **Native faster** | 248 B | 0.573 us | 0.342 us | 1.68x | 1.67–1.68x | +0.230 us | 9 pairs; 38/40 ms; 66,104/116,012 ops/lane; IQR 1.011/1.009 | `Pcre2Character` | 0 B | 0 B | 2.087 us | 2,248 B |
| `pcre2/recursive-optional` | **Managed faster** | 6 B | 0.012 us | 0.625 us | 0.02x | 0.02–0.02x | -0.614 us | 9 pairs; 42/41 ms; 3,492,614/66,104 ops/lane; IQR 1.035/1.011 | `Pcre2Backtracking` | 0 B | 0 B | 21.863 us | 21,584 B |
| `pcre2/recursive-palindrome` | **Managed faster** | 6 B | 0.012 us | 0.518 us | 0.02x | 0.02–0.02x | -0.506 us | 9 pairs; 38/41 ms; 3,198,698/79,846 ops/lane; IQR 1.038/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 21.019 us | 21,072 B |
| `pcre2/subroutine-prefix-digits` | **Native faster** | 5 B | 0.466 us | 0.111 us | 4.22x | 4.17–4.27x | +0.356 us | 9 pairs; 36/41 ms; 78,228/374,614 ops/lane; IQR 1.006/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 18.444 us | 13,760 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 8.206 us | 10.286 us | 0.80x | 0.79–0.80x | -2.086 us | 9 pairs; 39/39 ms; 4,791/3,799 ops/lane; IQR 1.014/1.012 | `Pcre2Literal` | 0 B | 0 B | 0.806 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.293 us | 1.075 us | 0.27x | 0.27–0.27x | -0.780 us | 9 pairs; 40/42 ms; 134,642/38,991 ops/lane; IQR 1.010/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 132.075 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.319 us | 0.457 us | 0.69x | 0.69–0.70x | -0.140 us | 9 pairs; 38/39 ms; 118,679/85,220 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 116.362 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.286 us | 0.453 us | 0.63x | 0.63–0.63x | -0.167 us | 9 pairs; 37/39 ms; 128,447/85,832 ops/lane; IQR 1.011/1.016 | `Pcre2Backtracking` | 0 B | 0 B | 140.500 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.295 us | 0.585 us | 0.51x | 0.50–0.51x | -0.289 us | 9 pairs; 34/41 ms; 115,848/69,907 ops/lane; IQR 1.003/1.015 | `Pcre2Backtracking` | 0 B | 0 B | 102.331 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.282 us | 0.393 us | 0.71x | 0.70–0.72x | -0.112 us | 9 pairs; 33/39 ms; 118,307/99,019 ops/lane; IQR 1.007/1.022 | `Pcre2Backtracking` | 0 B | 0 B | 208.856 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 4.897 us | 8.711 us | 0.56x | 0.56–0.57x | -3.774 us | 9 pairs; 41/41 ms; 8,296/4,717 ops/lane; IQR 1.009/1.011 | `Pcre2Character` | 0 B | 0 B | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.251 us | 0.307 us | 0.82x | 0.82–0.82x | -0.055 us | 9 pairs; 35/42 ms; 140,943/135,951 ops/lane; IQR 1.005/1.003 | `Pcre2Backtracking` | 0 B | 0 B | 94.375 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.266 us | 0.402 us | 0.66x | 0.66–0.67x | -0.136 us | 9 pairs; 36/39 ms; 134,364/97,724 ops/lane; IQR 1.005/1.023 | `Pcre2Backtracking` | 0 B | 0 B | 121.544 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.724 us | — | — | — | — | — | `—` | 0 B | — | 14.463 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.195 us | 0.289 us | 0.68x | 0.64–0.68x | -0.092 us | 9 pairs; 42/40 ms; 217,623/136,879 ops/lane; IQR 1.044/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 51.500 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.658 us | — | — | — | — | — | `—` | 0 B | — | 13.812 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.563 us | 0.659 us | 0.85x | 0.84–0.86x | -0.096 us | 9 pairs; 36/40 ms; 64,501/60,508 ops/lane; IQR 1.021/1.012 | `Pcre2Slice` | 0 B | 0 B | 64.044 us | 55,552 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 9.778 us | 28.840 us | 0.34x | 0.34–0.34x | -19.059 us | 9 pairs; 40/41 ms; 4,125/1,420 ops/lane; IQR 1.005/1.006 | `Pcre2Literal` | 0 B | 0 B | 0.769 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.946 us | 1.094 us | 0.87x | 0.86–0.87x | -0.144 us | 9 pairs; 41/40 ms; 43,726/36,759 ops/lane; IQR 1.010/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 131.981 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.959 us | 1.185 us | 0.81x | 0.80–0.81x | -0.228 us | 9 pairs; 41/41 ms; 42,406/34,787 ops/lane; IQR 1.009/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 113.987 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.931 us | 1.024 us | 0.91x | 0.91–0.91x | -0.092 us | 9 pairs; 39/39 ms; 42,310/38,426 ops/lane; IQR 1.006/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 140.500 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.940 us | 1.158 us | 0.81x | 0.81–0.82x | -0.215 us | 9 pairs; 40/42 ms; 42,218/36,050 ops/lane; IQR 1.006/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 103.556 us | 81,544 B |
| `pcre2/duplicate-names` | **Equivalent** | 23 B | 0.936 us | 0.929 us | 1.01x | 1.00–1.01x | +0.009 us | 9 pairs; 40/39 ms; 42,472/41,964 ops/lane; IQR 1.007/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 213.162 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 5.548 us | 8.744 us | 0.63x | 0.63–0.63x | -3.213 us | 9 pairs; 35/40 ms; 6,392/4,546 ops/lane; IQR 1.007/1.006 | `Pcre2Character` | 0 B | 0 B | 1.462 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Native faster** | 13 B | 0.914 us | 0.883 us | 1.03x | 1.03–1.05x | +0.029 us | 9 pairs; 41/43 ms; 45,268/48,470 ops/lane; IQR 1.018/1.014 | `Pcre2Backtracking` | 0 B | 0 B | 93.194 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.909 us | 0.956 us | 0.95x | 0.95–0.95x | -0.048 us | 9 pairs; 41/40 ms; 44,607/41,473 ops/lane; IQR 1.011/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 120.275 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.927 us | — | — | — | — | — | `—` | 0 B | — | 14.644 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.821 us | 0.865 us | 0.95x | 0.95–0.95x | -0.044 us | 9 pairs; 40/40 ms; 48,147/46,175 ops/lane; IQR 1.003/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 54.038 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.860 us | — | — | — | — | — | `—` | 0 B | — | 13.825 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.869 us | 1.231 us | 0.70x | 0.70–0.71x | -0.366 us | 9 pairs; 40/39 ms; 46,206/31,668 ops/lane; IQR 1.004/1.004 | `Pcre2Slice` | 0 B | 0 B | 62.462 us | 55,552 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Managed faster** | 320 B | 1.462 us | 2.274 us | 0.64x | 0.64–0.64x | -0.814 us | 9 pairs; 42/41 ms; 28,921/18,210 ops/lane; IQR 1.004/1.002 | `Pcre2Literal` | 0 B | 0 B | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Managed faster** | 23 B | 0.630 us | 1.096 us | 0.58x | 0.57–0.58x | -0.465 us | 9 pairs; 40/40 ms; 63,983/36,583 ops/lane; IQR 1.010/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 129.006 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Managed faster** | 17 B | 0.674 us | 1.187 us | 0.57x | 0.56–0.57x | -0.511 us | 9 pairs; 35/39 ms; 51,539/33,174 ops/lane; IQR 1.005/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 113.819 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Managed faster** | 31 B | 0.683 us | 1.026 us | 0.67x | 0.66–0.67x | -0.341 us | 9 pairs; 40/41 ms; 59,006/39,715 ops/lane; IQR 1.007/1.009 | `Pcre2Backtracking` | 0 B | 0 B | 139.831 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Managed faster** | 22 B | 0.630 us | 1.150 us | 0.55x | 0.55–0.55x | -0.517 us | 9 pairs; 40/41 ms; 63,474/35,616 ops/lane; IQR 1.008/1.006 | `Pcre2Backtracking` | 0 B | 0 B | 102.700 us | 81,544 B |
| `pcre2/duplicate-names` | **Managed faster** | 23 B | 0.653 us | 0.934 us | 0.70x | 0.70–0.70x | -0.279 us | 9 pairs; 40/39 ms; 60,526/41,777 ops/lane; IQR 1.009/1.008 | `Pcre2Backtracking` | 0 B | 0 B | 215.075 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Managed faster** | 248 B | 1.778 us | 2.141 us | 0.83x | 0.83–0.83x | -0.360 us | 9 pairs; 40/39 ms; 22,358/18,395 ops/lane; IQR 1.004/1.003 | `Pcre2Character` | 0 B | 0 B | 1.419 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Managed faster** | 13 B | 0.602 us | 0.880 us | 0.69x | 0.68–0.69x | -0.275 us | 9 pairs; 38/40 ms; 63,731/45,097 ops/lane; IQR 1.006/1.005 | `Pcre2Backtracking` | 0 B | 0 B | 88.694 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Managed faster** | 26 B | 0.626 us | 0.964 us | 0.65x | 0.65–0.66x | -0.336 us | 9 pairs; 40/42 ms; 64,025/43,149 ops/lane; IQR 1.009/1.007 | `Pcre2Backtracking` | 0 B | 0 B | 119.263 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.596 us | — | — | — | — | — | `—` | 0 B | — | 13.363 us | 11,496 B |
| `pcre2/kreset-global` | **Managed faster** | 26 B | 0.489 us | 0.866 us | 0.56x | 0.56–0.57x | -0.377 us | 9 pairs; 40/41 ms; 81,379/47,318 ops/lane; IQR 1.015/1.012 | `Pcre2Backtracking` | 0 B | 0 B | 53.200 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 4.527 us | — | — | — | — | — | `—` | 0 B | — | 13.512 us | 10,536 B |
| `pcre2/same-start-global` | **Managed faster** | 9 B | 0.586 us | 1.246 us | 0.47x | 0.47–0.47x | -0.663 us | 9 pairs; 40/40 ms; 68,751/32,168 ops/lane; IQR 1.012/1.010 | `Pcre2Slice` | 0 B | 0 B | 64.562 us | 55,552 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 elapsed | PCRE.NET / PCRE2 NFA elapsed | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction elapsed | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 17.566 us | — | — | — | — | — | `—` | 480 B | — | 0.762 us | 1,488 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 1.160 us | — | — | — | — | — | `—` | 48 B | — | 129.637 us | 111,912 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 1.273 us | — | — | — | — | — | `—` | 48 B | — | 116.513 us | 99,256 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 1.150 us | — | — | — | — | — | `—` | 48 B | — | 141.950 us | 127,480 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.158 us | — | — | — | — | — | `—` | 48 B | — | 101.463 us | 81,544 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 1.121 us | — | — | — | — | — | `—` | 48 B | — | 221.762 us | 179,232 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 6.746 us | — | — | — | — | — | `—` | 64 B | — | 1.450 us | 2,248 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 1.149 us | — | — | — | — | — | `—` | 40 B | — | 93.250 us | 72,568 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 1.158 us | — | — | — | — | — | `—` | 40 B | — | 122.194 us | 110,720 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 21.815 us | — | — | — | — | — | `—` | 1,216 B | — | 14.338 us | 11,496 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 1.084 us | — | — | — | — | — | `—` | 48 B | — | 53.212 us | 53,288 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 16.380 us | — | — | — | — | — | `—` | 928 B | — | 14.731 us | 10,536 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 4.886 us | — | — | — | — | — | `—` | 7,184 B | — | 63.419 us | 55,552 B |

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
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 34.372–249.808 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 12.251–93.422 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 2.975–12.478 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 6.818–49.194 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 0.037–1.144 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 5.492–43.026 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 5.691–22.176 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 6.184–99.336 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 9.111–71.722 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 1.174–5.419 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 48.204–370.961 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.068–0.074 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 39.589–310.436 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 0.842–5.038 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 2.116–15.266 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 109.105–861.886 us |

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
