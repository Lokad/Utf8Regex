<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Compatible rows compare equivalent work against `Utf8Regex` and .NET 10 `Regex`. `.NET + decode` performs strict UTF-8 decoding for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

## Snapshot summary

- Schema: `9`
- Snapshot SHA-256: `53C3E0D2AB8DB0930F02076A09E4B89C07C6BACD4201D6D723A77F36D1D7E40F`
- Latest managed row measurement: `2026-09-22T23:20:16.7696164+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-09-22T23:20:14.9905448+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `29/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `101/126`
- Comparator Status: `0` managed faster, `0` equivalent, `0` native faster, `0` inconclusive, `101` unqualified, `25` excluded
- Rows with paired qualification evidence: `0/101`
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `fd906c6a6b36` on .NET 10.0.12, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `fd906c6a6b36` on .NET 10.0.12, Ubuntu 24.04.4 LTS, X64; Tiered PGO `runtime-default`, tracked dirty `False`, untracked files `False`.

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
| `backtracking/assertion-capture` | **Unqualified** | 4,103 B | 7.323 us | 2.709 us | 2.70x | — | +4.614 us | — | `—` | 3.484 us | 0.680 us | 1.322 us | 5.54x | 0 B | — |
| `backtracking/capture-backreference` | **Unqualified** | 22,528 B | 4.263 us | 13.511 us | 0.32x | — | -9.248 us | — | `—` | 6.760 us | 0.162 us | 3.610 us | 1.18x | 0 B | — |
| `common/backtracking` | **Unqualified** | 49 B | 0.103 us | 1.292 us | 0.08x | — | -1.189 us | — | `—` | 0.107 us | 1.504 us | 1.467 us | 0.07x | 0 B | — |
| `common/date-match` | **Unqualified** | 47 B | 1.235 us | 0.207 us | 5.96x | — | +1.028 us | — | `—` | 0.272 us | 0.619 us | 0.719 us | 1.72x | 120 B | — |
| `common/date-miss` | **Unqualified** | 47 B | 3.624 us | 0.700 us | 5.18x | — | +2.925 us | — | `—` | 1.525 us | 2.662 us | 2.629 us | 1.38x | 120 B | — |
| `common/email-match` | **Unqualified** | 34 B | 1.159 us | 0.452 us | 2.56x | — | +0.707 us | — | `—` | 1.251 us | 0.908 us | 1.019 us | 1.14x | 0 B | — |
| `common/email-miss` | **Unqualified** | 35 B | 0.857 us | 0.522 us | 1.64x | — | +0.335 us | — | `—` | 0.813 us | 1.227 us | 1.274 us | 0.67x | 0 B | — |
| `common/ip-match` | **Unqualified** | 15 B | 0.129 us | 0.337 us | 0.38x | — | -0.207 us | — | `—` | 0.186 us | 0.618 us | 0.689 us | 0.19x | 0 B | — |
| `common/ip-miss` | **Unqualified** | 15 B | 0.134 us | 0.309 us | 0.43x | — | -0.175 us | — | `—` | 0.099 us | 0.225 us | 0.258 us | 0.52x | 0 B | — |
| `common/one-node-backtracking` | **Unqualified** | 52 B | 0.544 us | 0.072 us | 7.52x | — | +0.472 us | — | `—` | 0.135 us | 0.784 us | 0.847 us | 0.64x | 0 B | — |
| `common/uri-match` | **Unqualified** | 46 B | 0.233 us | 0.193 us | 1.21x | — | +0.040 us | — | `—` | 0.199 us | 0.306 us | 0.371 us | 0.63x | 0 B | — |
| `common/uri-miss` | **Unqualified** | 19 B | 0.232 us | 0.612 us | 0.38x | — | -0.381 us | — | `—` | 0.057 us | 0.195 us | 0.246 us | 0.94x | 0 B | — |
| `industry/boostdocs-credit-card-match` | **Unqualified** | 18 B | 0.224 us | 0.148 us | 1.51x | — | +0.076 us | — | `—` | 0.190 us | 0.170 us | 0.186 us | 1.21x | 0 B | — |
| `industry/boostdocs-date-match` | **Unqualified** | 10 B | 0.135 us | 0.105 us | 1.28x | — | +0.029 us | — | `—` | 0.082 us | 0.083 us | 0.102 us | 1.32x | 0 B | — |
| `industry/boostdocs-float-match` | **Unqualified** | 8 B | 0.228 us | 0.111 us | 2.06x | — | +0.117 us | — | `—` | 0.023 us | 0.076 us | 0.088 us | 2.60x | 0 B | — |
| `industry/boostdocs-ftp-line-match` | **Unqualified** | 67 B | 0.092 us | 0.263 us | 0.35x | — | -0.171 us | — | `—` | 0.093 us | 0.111 us | 0.148 us | 0.62x | 0 B | — |
| `industry/boostdocs-postcode-match` | **Unqualified** | 7 B | 0.033 us | 0.109 us | 0.30x | — | -0.077 us | — | `—` | 0.060 us | 0.084 us | 0.109 us | 0.30x | 0 B | — |
| `literal/absolute-anchored` | **Unqualified** | 4,103 B | 0.226 us | 2.277 us | 0.10x | — | -2.051 us | — | `—` | 1.275 us | 0.053 us | 0.574 us | 0.39x | 0 B | — |
| `literal/early` | **Unqualified** | 4,103 B | 0.252 us | 2.322 us | 0.11x | — | -2.070 us | — | `—` | 0.467 us | 0.058 us | 0.520 us | 0.48x | 0 B | — |
| `literal/late` | **Unqualified** | 4,103 B | 0.283 us | 1.692 us | 0.17x | — | -1.409 us | — | `—` | 0.497 us | 0.305 us | 0.778 us | 0.36x | 0 B | — |
| `literal/missing` | **Unqualified** | 4,096 B | 0.265 us | 1.389 us | 0.19x | — | -1.124 us | — | `—` | 0.318 us | 0.275 us | 0.737 us | 0.36x | 0 B | — |
| `simple/ab-plus` | **Unqualified** | 16 B | 0.044 us | 0.090 us | 0.49x | — | -0.046 us | — | `—` | 1.003 us | 0.094 us | 0.111 us | 0.39x | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.177 us | 0.082 us | 2.16x | — | +0.096 us | — | `—` | 0.216 us | 0.052 us | 0.069 us | 2.56x | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 0.029 us | 0.106 us | 0.27x | — | -0.077 us | — | `—` | 0.443 us | 0.088 us | 0.115 us | 0.25x | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 0.474 us | 0.118 us | 4.02x | — | +0.357 us | — | `—` | 0.347 us | 0.107 us | 0.139 us | 3.41x | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.116 us | 0.200 us | 0.58x | — | -0.084 us | — | `—` | 0.893 us | 0.078 us | 0.109 us | 1.07x | 0 B | — |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Unqualified** | 7,168 B | 79.965 us | 202.941 us | 0.39x | — | -122.976 us | — | `—` | 116.171 us | 109.451 us | 89.469 us | 0.89x | 0 B | — |
| `character/unicode-class-dense` | **Unqualified** | 7,168 B | 107.006 us | 802.669 us | 0.13x | — | -695.663 us | — | `—` | 208.063 us | 193.541 us | 204.615 us | 0.52x | 0 B | — |
| `common/matches-boundary` | **Unqualified** | 8,208 B | 19.787 us | 198.266 us | 0.10x | — | -178.479 us | — | `—` | 19.122 us | 183.954 us | 188.489 us | 0.10x | 0 B | — |
| `common/matches-set` | **Unqualified** | 8,208 B | 585.787 us | 192.094 us | 3.05x | — | +393.693 us | — | `—` | 10.843 us | 213.293 us | 210.465 us | 2.78x | 0 B | — |
| `common/matches-word` | **Unqualified** | 8,208 B | 4.630 us | 36.699 us | 0.13x | — | -32.069 us | — | `—` | 3.069 us | 4.500 us | 5.777 us | 0.80x | 0 B | — |
| `common/matches-words` | **Unqualified** | 8,208 B | 20.137 us | 129.144 us | 0.16x | — | -109.007 us | — | `—` | 21.250 us | 85.441 us | 83.880 us | 0.24x | 0 B | — |
| `industry/leipzig-name-family-count` | **Unqualified** | 16,013,977 B | 3,101.971 us | 26,035.800 us | 0.12x | — | -22933.829 us | — | `—` | 3,200.257 us | 10,405.829 us | 17,217.371 us | 0.18x | 0 B | — |
| `industry/leipzig-river-window-count` | **Unqualified** | 16,013,977 B | 11,003.014 us | 52,536.100 us | 0.21x | — | -41533.086 us | — | `—` | 11,013.921 us | 34,512.400 us | 41,305.771 us | 0.27x | 0 B | — |
| `industry/leipzig-symbol-count` | **Unqualified** | 16,013,977 B | 1,890.145 us | 456,431.200 us | 0.00x | — | -454541.055 us | — | `—` | 1,973.682 us | 21,472.789 us | 28,154.004 us | 0.07x | 0 B | — |
| `industry/leipzig-twain-count` | **Unqualified** | 16,013,977 B | 5,362.002 us | 10,232.678 us | 0.52x | — | -4870.676 us | — | `—` | 1,215.443 us | 1,437.704 us | 7,029.207 us | 0.76x | 0 B | — |
| `industry/mariomka-email-count` | **Unqualified** | 6,839,410 B | 19,284.436 us | 137,299.000 us | 0.14x | — | -118014.564 us | — | `—` | 1,704.455 us | 468.327 us | 8,212.209 us | 2.35x | 0 B | — |
| `industry/mariomka-ip-count` | **Unqualified** | 6,839,410 B | 22,539.325 us | 20,651.225 us | 1.09x | — | +1888.100 us | — | `—` | 2,655.650 us | 8,468.763 us | 16,108.925 us | 1.40x | 0 B | — |
| `industry/mariomka-uri-count` | **Unqualified** | 6,839,410 B | 34,071.843 us | 127,098.200 us | 0.27x | — | -93026.357 us | — | `—` | 2,296.714 us | 1,969.057 us | 9,800.729 us | 3.48x | 0 B | — |
| `industry/rust-sherlock-holmes-window-count` | **Unqualified** | 594,930 B | 65.438 us | 967.785 us | 0.07x | — | -902.347 us | — | `—` | 67.024 us | 301.144 us | 466.193 us | 0.14x | 0 B | — |
| `industry/rust-sherlock-ing-count` | **Unqualified** | 594,930 B | 13,393.611 us | 12,963.429 us | 1.03x | — | +430.183 us | — | `—` | 433.633 us | 13,238.172 us | 13,366.522 us | 1.00x | 0 B | — |
| `industry/rust-sherlock-letter-count` | **Unqualified** | 594,930 B | 2,677.492 us | 92,537.300 us | 0.03x | — | -89859.808 us | — | `—` | 1,826.840 us | 17,828.976 us | 18,297.478 us | 0.15x | 0 B | — |
| `industry/rust-sherlock-nonnewline-count` | **Unqualified** | 594,930 B | 194.785 us | 6,059.738 us | 0.03x | — | -5864.953 us | — | `—` | 179.490 us | 976.600 us | 1,241.390 us | 0.16x | 0 B | — |
| `industry/rust-sherlock-word-holmes-count` | **Unqualified** | 594,930 B | 1,856.929 us | 19,072.180 us | 0.10x | — | -17215.251 us | — | `—` | 24.486 us | 8,209.293 us | 8,435.175 us | 0.22x | 0 B | — |
| `simple/ab-plus` | **Unqualified** | 16 B | 0.430 us | 1.068 us | 0.40x | — | -0.638 us | — | `—` | 0.999 us | 0.259 us | 0.277 us | 1.55x | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.653 us | 0.803 us | 0.81x | — | -0.150 us | — | `—` | 0.117 us | 0.153 us | 0.169 us | 3.86x | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.296 us | 1.150 us | 1.13x | — | +0.146 us | — | `—` | 2.520 us | 0.331 us | 0.368 us | 3.52x | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 2.633 us | 1.090 us | 2.42x | — | +1.543 us | — | `—` | 0.679 us | 0.408 us | 0.472 us | 5.58x | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.261 us | 0.829 us | 0.31x | — | -0.568 us | — | `—` | 0.964 us | 0.157 us | 0.214 us | 1.22x | 0 B | — |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Unqualified** | 16 B | 1.835 us | 0.629 us | 2.92x | — | +1.206 us | — | `—` | 1.559 us | 0.626 us | 0.626 us | 2.93x | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 1.342 us | 0.507 us | 2.65x | — | +0.835 us | — | `—` | 1.087 us | 0.433 us | 0.430 us | 3.12x | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.608 us | 0.729 us | 2.21x | — | +0.879 us | — | `—` | 1.658 us | 0.663 us | 0.710 us | 2.26x | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 2.457 us | 0.594 us | 4.14x | — | +1.863 us | — | `—` | 1.912 us | 0.630 us | 0.667 us | 3.68x | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.996 us | 0.600 us | 1.66x | — | +0.396 us | — | `—` | 1.259 us | 0.256 us | 0.294 us | 3.39x | 0 B | — |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Unqualified** | 16 B | 1.209 us | 0.570 us | 2.12x | — | +0.639 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.812 us | 0.440 us | 1.85x | — | +0.372 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.203 us | 0.623 us | 1.93x | — | +0.580 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 2.073 us | 0.595 us | 3.48x | — | +1.478 us | — | `—` | — | — | — | — | 0 B | — |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.615 us | 0.578 us | 1.06x | — | +0.037 us | — | `—` | — | — | — | — | 0 B | — |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 79.005 us | — | — | — | — | — | `—` | 125.958 us | 105.360 us | 107.359 us | 0.74x | 9,584 B | — |
| `simple/ab-plus` | **Excluded** | 16 B | 1.256 us | — | — | — | — | — | `—` | 1.473 us | 0.722 us | 0.843 us | 1.49x | 104 B | — |
| `simple/foo-dense` | **Excluded** | 18 B | 0.989 us | — | — | — | — | — | `—` | 1.049 us | 0.603 us | 0.646 us | 1.53x | 104 B | — |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 0.716 us | — | — | — | — | — | `—` | 1.028 us | 0.530 us | 0.587 us | 1.22x | 104 B | — |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 1.656 us | — | — | — | — | — | `—` | 0.902 us | 0.521 us | 0.552 us | 3.00x | 112 B | — |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 0.531 us | — | — | — | — | — | `—` | 0.648 us | 0.260 us | 0.308 us | 1.72x | 136 B | — |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Unqualified** | 5 B | 0.168 us | 0.082 us | 2.04x | — | +0.086 us | — | `—` | 0 B | — | 28.583 us | 33,928 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.055 us | 0.117 us | 0.47x | — | -0.062 us | — | `—` | 0 B | — | 83.529 us | 126,592 B |
| `pcre2/conditional-accept-negative-lookahead` | **Unqualified** | 3 B | 0.452 us | 0.105 us | 4.31x | — | +0.347 us | — | `—` | 0 B | — | 34.323 us | 47,736 B |
| `pcre2/conditional-lookahead` | **Unqualified** | 3 B | 0.511 us | 0.109 us | 4.67x | — | +0.402 us | — | `—` | 0 B | — | 26.421 us | 49,080 B |
| `pcre2/conditional-negative-lookahead` | **Unqualified** | 3 B | 0.419 us | 0.104 us | 4.02x | — | +0.314 us | — | `—` | 0 B | — | 25.646 us | 49,080 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 0.967 us | 0.346 us | 2.80x | — | +0.621 us | — | `—` | 0 B | — | 27.355 us | 32,728 B |
| `pcre2/recursive-optional` | **Unqualified** | 6 B | 0.057 us | 0.605 us | 0.09x | — | -0.548 us | — | `—` | 0 B | — | 34.306 us | 49,936 B |
| `pcre2/recursive-palindrome` | **Unqualified** | 6 B | 0.058 us | 0.481 us | 0.12x | — | -0.423 us | — | `—` | 0 B | — | 34.314 us | 49,680 B |
| `pcre2/subroutine-prefix-digits` | **Unqualified** | 5 B | 0.543 us | 0.102 us | 5.34x | — | +0.442 us | — | `—` | 0 B | — | 32.296 us | 42,152 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 14.650 us | 27.679 us | 0.53x | — | -13.029 us | — | `—` | 0 B | — | 17.458 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.766 us | 0.992 us | 0.77x | — | -0.226 us | — | `—` | 0 B | — | 79.668 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 0.920 us | 1.139 us | 0.81x | — | -0.219 us | — | `—` | 0 B | — | 76.096 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 0.788 us | 1.025 us | 0.77x | — | -0.237 us | — | `—` | 0 B | — | 86.353 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 0.844 us | 1.105 us | 0.76x | — | -0.261 us | — | `—` | 0 B | — | 72.863 us | 120,536 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 0.778 us | 0.953 us | 0.82x | — | -0.175 us | — | `—` | 0 B | — | 86.772 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 6.783 us | 8.568 us | 0.79x | — | -1.785 us | — | `—` | 0 B | — | 27.668 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 0.820 us | 0.874 us | 0.94x | — | -0.054 us | — | `—` | 0 B | — | 68.266 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 0.784 us | 0.930 us | 0.84x | — | -0.146 us | — | `—` | 0 B | — | 79.730 us | 132,696 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.645 us | — | — | — | — | — | `—` | 0 B | — | 30.122 us | 38,360 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 0.180 us | 0.806 us | 0.22x | — | -0.626 us | — | `—` | 0 B | — | 55.373 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.505 us | — | — | — | — | — | `—` | 0 B | — | 29.230 us | 36,688 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 0.761 us | 1.196 us | 0.64x | — | -0.435 us | — | `—` | 0 B | — | 25.740 us | 41,896 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 17.427 us | 10.239 us | 1.70x | — | +7.188 us | — | `—` | 0 B | — | 17.942 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.970 us | 0.643 us | 1.51x | — | +0.327 us | — | `—` | 0 B | — | 79.158 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 1.020 us | 0.642 us | 1.59x | — | +0.378 us | — | `—` | 0 B | — | 74.045 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 0.962 us | 0.634 us | 1.52x | — | +0.328 us | — | `—` | 0 B | — | 82.994 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 0.964 us | 0.743 us | 1.30x | — | +0.221 us | — | `—` | 0 B | — | 73.058 us | 120,536 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 0.952 us | 0.550 us | 1.73x | — | +0.402 us | — | `—` | 0 B | — | 83.772 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 7.535 us | 3.244 us | 2.32x | — | +4.291 us | — | `—` | 0 B | — | 27.347 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 1.038 us | 0.486 us | 2.14x | — | +0.552 us | — | `—` | 0 B | — | 68.588 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 1.029 us | 0.564 us | 1.82x | — | +0.465 us | — | `—` | 0 B | — | 78.362 us | 132,560 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 4.109 us | — | — | — | — | — | `—` | 0 B | — | 29.627 us | 38,360 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 1.700 us | 0.465 us | 3.66x | — | +1.235 us | — | `—` | 0 B | — | 53.244 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.974 us | — | — | — | — | — | `—` | 0 B | — | 28.358 us | 36,688 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 2.064 us | 0.860 us | 2.40x | — | +1.204 us | — | `—` | 0 B | — | 26.133 us | 41,896 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 1.609 us | 1.125 us | 1.43x | — | +0.484 us | — | `—` | 0 B | — | 17.532 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.595 us | 0.626 us | 0.95x | — | -0.031 us | — | `—` | 0 B | — | 78.838 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 0.641 us | 0.639 us | 1.00x | — | +0.002 us | — | `—` | 0 B | — | 74.104 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 0.595 us | 0.633 us | 0.94x | — | -0.038 us | — | `—` | 0 B | — | 83.086 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 0.576 us | 0.732 us | 0.79x | — | -0.156 us | — | `—` | 0 B | — | 73.346 us | 120,536 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 0.576 us | 0.567 us | 1.02x | — | +0.009 us | — | `—` | 0 B | — | 88.063 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 2.383 us | 1.089 us | 2.19x | — | +1.294 us | — | `—` | 0 B | — | 26.884 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 0.602 us | 0.494 us | 1.22x | — | +0.108 us | — | `—` | 0 B | — | 67.939 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 0.575 us | 0.573 us | 1.00x | — | +0.002 us | — | `—` | 0 B | — | 76.656 us | 132,560 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 3.682 us | — | — | — | — | — | `—` | 0 B | — | 29.482 us | 38,360 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 0.492 us | 0.452 us | 1.09x | — | +0.040 us | — | `—` | 0 B | — | 52.863 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.519 us | — | — | — | — | — | `—` | 0 B | — | 29.688 us | 36,688 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 0.560 us | 0.825 us | 0.68x | — | -0.265 us | — | `—` | 0 B | — | 25.440 us | 41,896 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 12.904 us | — | — | — | — | — | `—` | 536 B | — | 17.631 us | 32,176 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 0.614 us | — | — | — | — | — | `—` | 104 B | — | 79.337 us | 126,592 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 0.654 us | — | — | — | — | — | `—` | 104 B | — | 74.785 us | 121,864 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 0.594 us | — | — | — | — | — | `—` | 104 B | — | 83.408 us | 137,344 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 0.614 us | — | — | — | — | — | `—` | 104 B | — | 72.683 us | 120,536 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 0.583 us | — | — | — | — | — | `—` | 104 B | — | 82.593 us | 163,824 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 7.725 us | — | — | — | — | — | `—` | 120 B | — | 26.940 us | 32,728 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 0.647 us | — | — | — | — | — | `—` | 96 B | — | 68.244 us | 103,952 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 0.627 us | — | — | — | — | — | `—` | 96 B | — | 76.385 us | 132,560 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 16.861 us | — | — | — | — | — | `—` | 1,816 B | — | 29.417 us | 38,360 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 0.527 us | — | — | — | — | — | `—` | 104 B | — | 53.551 us | 87,216 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 8.363 us | — | — | — | — | — | `—` | 1,528 B | — | 29.147 us | 36,688 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 0.592 us | — | — | — | — | — | `—` | 96 B | — | 25.932 us | 41,896 B |

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

The benchmark catalog is [`Utf8Pcre2BenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/Utf8Pcre2BenchmarkCatalog.cs). The semantic boundary remains the managed profile described in [`SPEC-PCRE2.md`](../../SPEC-PCRE2.md), not the set of rows for which .NET has a comparator.
