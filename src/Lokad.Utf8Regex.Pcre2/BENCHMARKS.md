<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Each row applies only to its recorded source, runtime and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current independent PCRE2 delivery and operating-cost evidence are linked from the [adapter README](README.md).

Compatible rows compare equivalent work against `Utf8Regex` and .NET 10 `Regex`. `.NET + decode` performs strict UTF-8 decoding for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `B113B963453FC283DE6C8B64BFCFCA72E13A5EF8644FC5A38B8A92176B692F95`
- Latest managed row measurement: `2026-09-23T03:35:55.8149791+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-09-23T03:35:54.0313037+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `26/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `0` managed faster, `0` equivalent, `0` native faster, `0` inconclusive, `101` unqualified, `25` excluded
- Rows with paired qualification evidence: `0/101`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `2c4653702b27` on .NET 10.0.12, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `2c4653702b27` on .NET 10.0.12, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

NuGet package SHA-512: `Zu3NJGiU1S7tHHaW4UdEK1WZ9LFYqPI+6Y0eiL6YPHVOHSoWjbq0x5j3uN9895DoIgO5XI/50S6dj2ZmRHirNA==`.

## Comparator dependency

The benchmark executable—not either shipped library—uses one additional dependency for PCRE2-dialect rows. It provides the native PCRE2 engine and a UTF-8 span/reusable-buffer API, avoiding a string conversion or per-call wrapper allocation in the comparator.

| Package | Version | Native engine | License | Source revision | Benchmark profile |
|---|---:|---|---|---|---|
| [`PCRE.NET`](https://www.nuget.org/packages/PCRE.NET/1.5.0) | `1.5.0` | `10.47 2025-10-21` | `BSD-3-Clause WITH PCRE2-exception` | [`e5e5deaa30d5`](https://github.com/ltrzesniewski/pcre-net/commit/e5e5deaa30d50dd3a7ac68ec7fd9e9556551a84f) | UTF-8 standard NFA matcher, validation enabled, managed-profile LF newline and Unicode \R, reusable match buffer, JIT disabled |

Admission evidence: the package has a dedicated `net10.0` asset with no managed dependencies, is strongly named, is built and tested on Windows/Linux/macOS, and its tagged source has been maintained since 2014. It bundles RID-specific native libraries, so `PrivateAssets=all` and benchmark-project-only placement are mandatory. Native replacement is left blank because PCRE.NET does not expose equivalent UTF-8 span substitution output; routing through its string API would bias the comparison.

Native build fingerprint: `64226A8AB2D3AC9BE986279368957F1D3364E89B35FDDD9EFB10FA02EB91654B`; process/OS architecture `X64/X64`; JIT support `True` targeting `x86 64bit (little endian + unaligned)`; Unicode `16.0.0`; compiled-width mask `3`; link/effective-link size `2/2` bytes.

Build defaults: newline `AnyCrLf`, `\R` `Unicode`, heap `20,000,000` KiB, match/depth/parentheses limits `10,000,000/10,000,000/250`, character tables `1,088` bytes.

Comparator qualification lifecycle (`PcreMatchBuffer8Bit`): One buffer is constructed per qualified operation row, reused for checksum, warmup, calibration, paired samples, and allocation probing, then disposed. The buffer is not thread-safe and not reentrant; a caller must provide a distinct buffer for overlapping operations. The reusable buffer retains native match data between operations.

Retained native match-data heap-frame high water: unavailable — PCRE.NET 1.5.0 does not expose retained match-data heap-frame high-water memory through its public API.

`vs decode` is `Utf8Pcre2 / .NET + decode`; `R` is `Utf8Pcre2 / PCRE.NET-PCRE2 NFA`; lower is better. Rows without a 95% interval and paired-sample description contain independently measured discovery data only and cannot determine a winner. `E` is the paired median managed-minus-comparator excess when paired evidence exists and the difference between discovery medians otherwise. Paired-sample descriptions show managed/comparator median sample durations, frozen operations per lane, and managed/comparator interquartile spread ratios; a spread above 1.10 makes Status inconclusive. Allocation columns report the median of five managed-thread allocation probes per public operation; they do not measure native retained memory. A dash means that the other engine cannot perform equivalent work or the snapshot does not contain that comparator. Times are medians in microseconds per public operation.

## Compatible IsMatch

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/assertion-capture` | **Unqualified** | 4,103 B | 7.442 us | 2.690 us | 2.77x | — | +4.752 us | — | `—` | 3.891 us | 0.674 us | 1.353 us | 5.50x | 0 B | — |
| `backtracking/capture-backreference` | **Unqualified** | 22,528 B | 4.121 us | 12.510 us | 0.33x | — | -8.389 us | — | `—` | 6.745 us | 0.157 us | 3.541 us | 1.16x | 0 B | — |
| `common/backtracking` | **Unqualified** | 49 B | 0.099 us | 1.276 us | 0.08x | — | -1.178 us | — | `—` | 0.110 us | 1.449 us | 1.418 us | 0.07x | 0 B | — |
| `common/date-match` | **Unqualified** | 47 B | 1.188 us | 0.203 us | 5.85x | — | +0.985 us | — | `—` | 0.281 us | 0.603 us | 0.654 us | 1.82x | 120 B | — |
| `common/date-miss` | **Unqualified** | 47 B | 3.171 us | 0.610 us | 5.20x | — | +2.561 us | — | `—` | 1.481 us | 2.354 us | 2.343 us | 1.35x | 120 B | — |
| `common/email-match` | **Unqualified** | 34 B | 1.226 us | 0.357 us | 3.43x | — | +0.869 us | — | `—` | 1.263 us | 0.915 us | 0.979 us | 1.25x | 0 B | — |
| `common/email-miss` | **Unqualified** | 35 B | 1.187 us | 0.490 us | 2.42x | — | +0.697 us | — | `—` | 1.110 us | 1.162 us | 1.222 us | 0.97x | 0 B | — |
| `common/ip-match` | **Unqualified** | 15 B | 0.142 us | 0.313 us | 0.45x | — | -0.171 us | — | `—` | 0.192 us | 0.586 us | 0.681 us | 0.21x | 0 B | — |
| `common/ip-miss` | **Unqualified** | 15 B | 0.141 us | 0.306 us | 0.46x | — | -0.165 us | — | `—` | 0.102 us | 0.228 us | 0.249 us | 0.56x | 0 B | — |
| `common/one-node-backtracking` | **Unqualified** | 52 B | 0.515 us | 0.073 us | 7.04x | — | +0.442 us | — | `—` | 0.143 us | 0.797 us | 0.871 us | 0.59x | 0 B | — |
| `common/uri-match` | **Unqualified** | 46 B | 0.252 us | 0.186 us | 1.35x | — | +0.065 us | — | `—` | 0.206 us | 0.297 us | 0.379 us | 0.66x | 0 B | — |
| `common/uri-miss` | **Unqualified** | 19 B | 0.256 us | 0.575 us | 0.45x | — | -0.319 us | — | `—` | 0.060 us | 0.184 us | 0.246 us | 1.04x | 0 B | — |
| `industry/boostdocs-credit-card-match` | **Unqualified** | 18 B | 0.202 us | 0.144 us | 1.40x | — | +0.058 us | — | `—` | 0.181 us | 0.162 us | 0.176 us | 1.15x | 0 B | — |
| `industry/boostdocs-date-match` | **Unqualified** | 10 B | 0.120 us | 0.103 us | 1.16x | — | +0.017 us | — | `—` | 0.085 us | 0.078 us | 0.097 us | 1.24x | 0 B | — |
| `industry/boostdocs-float-match` | **Unqualified** | 8 B | 0.221 us | 0.119 us | 1.85x | — | +0.101 us | — | `—` | 0.024 us | 0.073 us | 0.087 us | 2.53x | 0 B | — |
| `industry/boostdocs-ftp-line-match` | **Unqualified** | 67 B | 0.085 us | 0.246 us | 0.35x | — | -0.161 us | — | `—` | 0.088 us | 0.112 us | 0.134 us | 0.63x | 0 B | — |
| `industry/boostdocs-postcode-match` | **Unqualified** | 7 B | 0.178 us | 0.107 us | 1.67x | — | +0.072 us | — | `—` | 0.061 us | 0.080 us | 0.099 us | 1.80x | 0 B | — |
| `literal/absolute-anchored` | **Unqualified** | 4,103 B | 0.230 us | 1.790 us | 0.13x | — | -1.560 us | — | `—` | 1.202 us | 0.051 us | 0.594 us | 0.39x | 0 B | — |
| `literal/early` | **Unqualified** | 4,103 B | 0.261 us | 1.636 us | 0.16x | — | -1.375 us | — | `—` | 0.457 us | 0.055 us | 0.490 us | 0.53x | 0 B | — |
| `literal/late` | **Unqualified** | 4,103 B | 0.296 us | 1.632 us | 0.18x | — | -1.336 us | — | `—` | 0.498 us | 0.306 us | 0.749 us | 0.40x | 0 B | — |
| `literal/missing` | **Unqualified** | 4,096 B | 0.275 us | 1.396 us | 0.20x | — | -1.121 us | — | `—` | 0.311 us | 0.269 us | 0.728 us | 0.38x | 0 B | — |
| `simple/ab-plus` | **Unqualified** | 16 B | 0.043 us | 0.098 us | 0.44x | — | -0.056 us | — | `—` | 0.962 us | 0.109 us | 0.128 us | 0.34x | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.188 us | 0.080 us | 2.34x | — | +0.108 us | — | `—` | 0.203 us | 0.052 us | 0.069 us | 2.73x | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 0.028 us | 0.102 us | 0.27x | — | -0.074 us | — | `—` | 0.485 us | 0.095 us | 0.130 us | 0.21x | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 0.468 us | 0.118 us | 3.97x | — | +0.350 us | — | `—` | 0.257 us | 0.109 us | 0.143 us | 3.28x | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.114 us | 0.179 us | 0.64x | — | -0.065 us | — | `—` | 0.929 us | 0.077 us | 0.104 us | 1.10x | 0 B | — |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Unqualified** | 7,168 B | 94.032 us | 189.502 us | 0.50x | — | -95.470 us | — | `—` | 123.324 us | 114.095 us | 90.314 us | 1.04x | 0 B | — |
| `character/unicode-class-dense` | **Unqualified** | 7,168 B | 101.660 us | 796.518 us | 0.13x | — | -694.858 us | — | `—` | 207.302 us | 191.704 us | 203.777 us | 0.50x | 0 B | — |
| `common/matches-boundary` | **Unqualified** | 8,208 B | 18.447 us | 193.182 us | 0.10x | — | -174.735 us | — | `—` | 17.718 us | 185.976 us | 187.287 us | 0.10x | 0 B | — |
| `common/matches-set` | **Unqualified** | 8,208 B | 593.314 us | 192.161 us | 3.09x | — | +401.153 us | — | `—` | 9.656 us | 208.658 us | 209.130 us | 2.84x | 0 B | — |
| `common/matches-word` | **Unqualified** | 8,208 B | 3.973 us | 34.339 us | 0.12x | — | -30.366 us | — | `—` | 1.715 us | 4.428 us | 5.724 us | 0.69x | 0 B | — |
| `common/matches-words` | **Unqualified** | 8,208 B | 19.418 us | 126.049 us | 0.15x | — | -106.631 us | — | `—` | 18.882 us | 84.987 us | 84.381 us | 0.23x | 0 B | — |
| `industry/leipzig-name-family-count` | **Unqualified** | 16,013,977 B | 3,141.786 us | 26,186.033 us | 0.12x | — | -23044.248 us | — | `—` | 3,162.014 us | 10,635.371 us | 17,536.457 us | 0.18x | 0 B | — |
| `industry/leipzig-river-window-count` | **Unqualified** | 16,013,977 B | 11,002.250 us | 50,457.900 us | 0.22x | — | -39455.650 us | — | `—` | 10,952.529 us | 33,634.429 us | 40,638.421 us | 0.27x | 0 B | — |
| `industry/leipzig-symbol-count` | **Unqualified** | 16,013,977 B | 1,945.679 us | 450,527.900 us | 0.00x | — | -448582.221 us | — | `—` | 2,028.766 us | 24,232.748 us | 30,979.942 us | 0.06x | 0 B | — |
| `industry/leipzig-twain-count` | **Unqualified** | 16,013,977 B | 5,181.745 us | 10,273.862 us | 0.50x | — | -5092.118 us | — | `—` | 1,305.630 us | 1,478.187 us | 7,381.926 us | 0.70x | 0 B | — |
| `industry/mariomka-email-count` | **Unqualified** | 6,839,410 B | 19,195.145 us | 135,556.700 us | 0.14x | — | -116361.555 us | — | `—` | 1,702.464 us | 472.345 us | 8,222.873 us | 2.33x | 0 B | — |
| `industry/mariomka-ip-count` | **Unqualified** | 6,839,410 B | 22,979.737 us | 20,304.500 us | 1.13x | — | +2675.238 us | — | `—` | 2,659.363 us | 8,370.837 us | 16,367.650 us | 1.40x | 0 B | — |
| `industry/mariomka-uri-count` | **Unqualified** | 6,839,410 B | 24,335.244 us | 125,009.800 us | 0.19x | — | -100674.556 us | — | `—` | 2,311.611 us | 2,040.756 us | 9,793.133 us | 2.48x | 0 B | — |
| `industry/rust-sherlock-holmes-window-count` | **Unqualified** | 594,930 B | 66.629 us | 975.381 us | 0.07x | — | -908.752 us | — | `—` | 68.342 us | 296.932 us | 484.050 us | 0.14x | 0 B | — |
| `industry/rust-sherlock-ing-count` | **Unqualified** | 594,930 B | 15,256.625 us | 12,636.571 us | 1.21x | — | +2620.054 us | — | `—` | 424.569 us | 13,029.587 us | 13,254.450 us | 1.15x | 0 B | — |
| `industry/rust-sherlock-letter-count` | **Unqualified** | 594,930 B | 2,609.854 us | 92,561.200 us | 0.03x | — | -89951.346 us | — | `—` | 1,898.068 us | 17,580.011 us | 18,150.225 us | 0.14x | 0 B | — |
| `industry/rust-sherlock-nonnewline-count` | **Unqualified** | 594,930 B | 194.975 us | 5,971.625 us | 0.03x | — | -5776.650 us | — | `—` | 178.772 us | 963.409 us | 1,216.911 us | 0.16x | 0 B | — |
| `industry/rust-sherlock-word-holmes-count` | **Unqualified** | 594,930 B | 1,727.949 us | 18,555.200 us | 0.09x | — | -16827.251 us | — | `—` | 24.339 us | 8,233.116 us | 8,461.899 us | 0.20x | 0 B | — |
| `simple/ab-plus` | **Unqualified** | 16 B | 0.411 us | 1.030 us | 0.40x | — | -0.619 us | — | `—` | 1.004 us | 0.248 us | 0.291 us | 1.41x | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.623 us | 0.768 us | 0.81x | — | -0.145 us | — | `—` | 0.125 us | 0.147 us | 0.169 us | 3.69x | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.267 us | 1.120 us | 1.13x | — | +0.147 us | — | `—` | 2.487 us | 0.346 us | 0.378 us | 3.35x | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 2.562 us | 1.049 us | 2.44x | — | +1.513 us | — | `—` | 0.704 us | 0.396 us | 0.434 us | 5.90x | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.243 us | 0.819 us | 0.30x | — | -0.576 us | — | `—` | 0.916 us | 0.151 us | 0.191 us | 1.27x | 0 B | — |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Unqualified** | 16 B | 1.738 us | 0.594 us | 2.93x | — | +1.144 us | — | `—` | 1.570 us | 0.599 us | 0.614 us | 2.83x | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 1.351 us | 0.486 us | 2.78x | — | +0.865 us | — | `—` | 1.099 us | 0.415 us | 0.412 us | 3.28x | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.627 us | 0.691 us | 2.35x | — | +0.936 us | — | `—` | 1.616 us | 0.680 us | 0.694 us | 2.34x | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 2.490 us | 0.588 us | 4.23x | — | +1.902 us | — | `—` | 1.811 us | 0.574 us | 0.613 us | 4.06x | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.932 us | 0.590 us | 1.58x | — | +0.342 us | — | `—` | 1.355 us | 0.251 us | 0.298 us | 3.13x | 0 B | — |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Unqualified** | 16 B | 1.133 us | 0.527 us | 2.15x | — | +0.606 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.782 us | 0.441 us | 1.77x | — | +0.341 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.154 us | 0.618 us | 1.87x | — | +0.536 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 2.097 us | 0.579 us | 3.62x | — | +1.518 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.601 us | 0.580 us | 1.04x | — | +0.021 us | — | `—` | — | — | — | — | 0 B | — |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 80.147 us | — | — | — | — | — | `—` | 122.849 us | 107.015 us | 107.092 us | 0.75x | 9,584 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 1.184 us | — | — | — | — | — | `—` | 1.434 us | 0.703 us | 0.828 us | 1.43x | 104 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 0.963 us | — | — | — | — | — | `—` | 1.040 us | 0.576 us | 0.624 us | 1.54x | 104 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.157 us | — | — | — | — | — | `—` | 1.072 us | 0.863 us | 0.942 us | 1.23x | 104 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.547 us | — | — | — | — | — | `—` | 0.897 us | 0.502 us | 0.526 us | 2.94x | 112 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 0.604 us | — | — | — | — | — | `—` | 0.614 us | 0.247 us | 0.295 us | 2.05x | 136 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Unqualified** | 5 B | 0.161 us | 0.078 us | 2.07x | — | +0.083 us | — | `—` | 0 B | — | 29.260 us | 33,928 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.053 us | 0.115 us | 0.46x | — | -0.062 us | — | `—` | 0 B | — | 83.584 us | 126,592 B |
| `pcre2/conditional-accept-negative-lookahead` | **Unqualified** | 3 B | 0.420 us | 0.098 us | 4.30x | — | +0.322 us | — | `—` | 0 B | — | 34.690 us | 47,736 B |
| `pcre2/conditional-lookahead` | **Unqualified** | 3 B | 0.477 us | 0.107 us | 4.47x | — | +0.370 us | — | `—` | 0 B | — | 26.323 us | 49,080 B |
| `pcre2/conditional-negative-lookahead` | **Unqualified** | 3 B | 0.407 us | 0.097 us | 4.17x | — | +0.309 us | — | `—` | 0 B | — | 26.428 us | 49,080 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 0.810 us | 0.321 us | 2.52x | — | +0.489 us | — | `—` | 0 B | — | 27.223 us | 32,728 B |
| `pcre2/recursive-optional` | **Unqualified** | 6 B | 0.063 us | 0.585 us | 0.11x | — | -0.522 us | — | `—` | 0 B | — | 35.532 us | 49,936 B |
| `pcre2/recursive-palindrome` | **Unqualified** | 6 B | 0.056 us | 0.466 us | 0.12x | — | -0.410 us | — | `—` | 0 B | — | 35.945 us | 49,680 B |
| `pcre2/subroutine-prefix-digits` | **Unqualified** | 5 B | 0.508 us | 0.099 us | 5.13x | — | +0.409 us | — | `—` | 0 B | — | 33.710 us | 42,152 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 14.235 us | 26.907 us | 0.53x | — | -12.672 us | — | `—` | 0 B | — | 18.183 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.760 us | 0.995 us | 0.76x | — | -0.235 us | — | `—` | 0 B | — | 79.703 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 0.944 us | 1.134 us | 0.83x | — | -0.190 us | — | `—` | 0 B | — | 76.923 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 0.753 us | 0.960 us | 0.78x | — | -0.207 us | — | `—` | 0 B | — | 87.786 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 0.794 us | 1.098 us | 0.72x | — | -0.304 us | — | `—` | 0 B | — | 75.096 us | 120,536 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 0.774 us | 0.890 us | 0.87x | — | -0.116 us | — | `—` | 0 B | — | 86.113 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 6.677 us | 8.388 us | 0.80x | — | -1.711 us | — | `—` | 0 B | — | 27.707 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 0.785 us | 0.823 us | 0.95x | — | -0.038 us | — | `—` | 0 B | — | 70.022 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 0.757 us | 0.919 us | 0.82x | — | -0.162 us | — | `—` | 0 B | — | 78.534 us | 132,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.613 us | — | — | — | — | — | `—` | 0 B | — | 29.839 us | 38,360 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 0.179 us | 0.798 us | 0.22x | — | -0.619 us | — | `—` | 0 B | — | 54.917 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.423 us | — | — | — | — | — | `—` | 0 B | — | 30.616 us | 36,688 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 0.767 us | 1.164 us | 0.66x | — | -0.397 us | — | `—` | 0 B | — | 26.231 us | 41,896 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 15.449 us | 10.145 us | 1.52x | — | +5.304 us | — | `—` | 0 B | — | 18.270 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.944 us | 0.609 us | 1.55x | — | +0.335 us | — | `—` | 0 B | — | 81.549 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 1.006 us | 0.648 us | 1.55x | — | +0.358 us | — | `—` | 0 B | — | 77.001 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 0.920 us | 0.599 us | 1.54x | — | +0.321 us | — | `—` | 0 B | — | 84.969 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 0.919 us | 0.722 us | 1.27x | — | +0.197 us | — | `—` | 0 B | — | 74.761 us | 120,536 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 0.914 us | 0.535 us | 1.71x | — | +0.379 us | — | `—` | 0 B | — | 85.095 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 7.302 us | 3.208 us | 2.28x | — | +4.094 us | — | `—` | 0 B | — | 27.666 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 1.001 us | 0.478 us | 2.09x | — | +0.523 us | — | `—` | 0 B | — | 69.443 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 1.024 us | 0.550 us | 1.86x | — | +0.474 us | — | `—` | 0 B | — | 78.794 us | 132,616 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.948 us | — | — | — | — | — | `—` | 0 B | — | 30.068 us | 38,360 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 0.947 us | 0.459 us | 2.06x | — | +0.488 us | — | `—` | 0 B | — | 54.087 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.967 us | — | — | — | — | — | `—` | 0 B | — | 29.568 us | 36,688 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 1.148 us | 0.787 us | 1.46x | — | +0.361 us | — | `—` | 0 B | — | 26.362 us | 41,896 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 1.480 us | 1.116 us | 1.33x | — | +0.364 us | — | `—` | 0 B | — | 18.200 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.574 us | 0.617 us | 0.93x | — | -0.043 us | — | `—` | 0 B | — | 82.054 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 0.621 us | 0.617 us | 1.01x | — | +0.004 us | — | `—` | 0 B | — | 76.292 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 0.568 us | 0.595 us | 0.95x | — | -0.027 us | — | `—` | 0 B | — | 85.577 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 0.570 us | 0.726 us | 0.79x | — | -0.156 us | — | `—` | 0 B | — | 75.222 us | 120,536 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 0.571 us | 0.523 us | 1.09x | — | +0.048 us | — | `—` | 0 B | — | 83.491 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 2.292 us | 1.045 us | 2.19x | — | +1.247 us | — | `—` | 0 B | — | 27.255 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 0.591 us | 0.482 us | 1.23x | — | +0.109 us | — | `—` | 0 B | — | 68.714 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 0.589 us | 0.555 us | 1.06x | — | +0.034 us | — | `—` | 0 B | — | 78.175 us | 132,560 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.629 us | — | — | — | — | — | `—` | 0 B | — | 29.836 us | 38,360 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 0.473 us | 0.452 us | 1.05x | — | +0.021 us | — | `—` | 0 B | — | 54.254 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.548 us | — | — | — | — | — | `—` | 0 B | — | 29.478 us | 36,688 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 0.542 us | 0.793 us | 0.68x | — | -0.251 us | — | `—` | 0 B | — | 26.113 us | 41,896 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 12.468 us | — | — | — | — | — | `—` | 536 B | — | 18.184 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 0.583 us | — | — | — | — | — | `—` | 104 B | — | 81.084 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 0.650 us | — | — | — | — | — | `—` | 104 B | — | 76.110 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 0.586 us | — | — | — | — | — | `—` | 104 B | — | 85.718 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 0.605 us | — | — | — | — | — | `—` | 104 B | — | 77.191 us | 120,536 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 0.584 us | — | — | — | — | — | `—` | 104 B | — | 83.983 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.492 us | — | — | — | — | — | `—` | 120 B | — | 27.650 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 0.622 us | — | — | — | — | — | `—` | 96 B | — | 68.948 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 0.597 us | — | — | — | — | — | `—` | 96 B | — | 78.872 us | 132,560 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 16.705 us | — | — | — | — | — | `—` | 1,816 B | — | 29.431 us | 38,360 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 0.527 us | — | — | — | — | — | `—` | 104 B | — | 54.225 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 9.981 us | — | — | — | — | — | `—` | 1,528 B | — | 29.551 us | 36,688 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 0.605 us | — | — | — | — | — | `—` | 96 B | — | 26.076 us | 41,896 B |

## Qualified comparator plans

No paired plan fingerprints are recorded.

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

| Family | Operation | Points | Input range | Pattern range | Warm CPU range |
|---|---|---:|---:|---:|---:|
| `branch-repeat-linear` | `IsMatch` | 4 | 1,025–8,193 B | 12–12 B | 96.225–312.125 us |
| `branch-reset-coordinate-projection` | `EnumerateMatches` | 4 | 512–4,096 B | 15–15 B | 37.237–284.195 us |
| `candidate-heavy-misses` | `IsMatch` | 4 | 512–4,096 B | 10–10 B | 100.519–678.025 us |
| `capture-rollback` | `Count` | 4 | 65–513 B | 10–10 B | 9.122–57.028 us |
| `cartesian-literal-families` | `IsMatch` | 4 | 4–6 B | 20–116 B | 1.384–8.806 us |
| `character-class-dense` | `Count` | 4 | 896–7,168 B | 13–13 B | 10.534–54.090 us |
| `dense-non-ascii-coordinates` | `Count` | 4 | 512–4,096 B | 2–2 B | 8.691–24.119 us |
| `dense-plus-sparse-candidate-portfolios` | `Count` | 6 | 512–8,192 B | 12–12 B | 56.659–289.490 us |
| `excluded-ascii-repeat-count` | `Count` | 4 | 5,120–40,960 B | 6–6 B | 11.450–39.875 us |
| `leading-word-boundary-run-candidates` | `Count` | 4 | 510–4,092 B | 11–11 B | 3.675–6.870 us |
| `literal-family-global-cursor` | `Replace` | 4 | 2,560–20,480 B | 19–19 B | 86.785–559.875 us |
| `long-flat-patterns` | `IsMatch` | 4 | 64–512 B | 64–512 B | 0.344–0.372 us |
| `replacement-growth` | `Replace` | 4 | 64–512 B | 3–3 B | 43.972–297.481 us |
| `required-literal-all-a-miss` | `Count` | 4 | 16,384–131,072 B | 13–13 B | 2.480–8.535 us |
| `single-token-repeat-vm` | `IsMatch` | 4 | 513–4,097 B | 5–5 B | 11.988–90.675 us |
| `zero-width-iteration` | `Count` | 4 | 512–4,096 B | 5–5 B | 247.381–1,290.795 us |

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
