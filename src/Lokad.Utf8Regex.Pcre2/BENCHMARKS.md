<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.Pcre2 benchmarks

This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.

Compatible rows compare equivalent work against `Utf8Regex` and .NET 10 `Regex`. `.NET + decode` performs strict UTF-8 decoding for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.

## Snapshot summary

- Schema: `5`
- Snapshot SHA-256: `889828C055BE86319EF47AFBC6014AF575A9D26359899E68D526BCBFD3F00809`
- Latest managed row measurement: `2026-08-17T10:08:03.6712825+00:00`
- Latest PCRE.NET / PCRE2 NFA measurement: `2026-08-21T08:22:11.8072070+00:00`
- Operation rows: `126` across `10` sections
- Comparable rows at or below the decode-then-.NET median: `19/60`
- Rows with a PCRE.NET / PCRE2 NFA comparator: `100/126`
- Comparator Status: `0` managed faster, `0` equivalent, `0` native faster, `0` inconclusive, `100` unqualified, `26` excluded
- Scaling families: `16`
- Managed/comparator measurement environments represented: `1/1`

Managed rows were measured from source `9f3e6bcfb29d` on .NET 10.0.8, Microsoft Windows 10.0.26200, Intel64 Family 6 Model 183 Stepping 1, GenuineIntel; Tiered PGO `runtime-default`.

PCRE.NET / PCRE2 NFA rows were measured from source `568af3f4f802` on .NET 10.0.11, Microsoft Windows 10.0.26200, Intel64 Family 6 Model 183 Stepping 1, GenuineIntel; Tiered PGO `runtime-default`, tracked dirty `True`, untracked files `True`.

NuGet package SHA-512: `Zu3NJGiU1S7tHHaW4UdEK1WZ9LFYqPI+6Y0eiL6YPHVOHSoWjbq0x5j3uN9895DoIgO5XI/50S6dj2ZmRHirNA==`.

## Comparator dependency

The benchmark executable—not either shipped library—uses one additional dependency for PCRE2-dialect rows. It provides the native PCRE2 engine and a UTF-8 span/reusable-buffer API, avoiding a string conversion or per-call wrapper allocation in the comparator.

| Package | Version | Native engine | License | Source revision | Benchmark profile |
|---|---:|---|---|---|---|
| [`PCRE.NET`](https://www.nuget.org/packages/PCRE.NET/1.5.0) | `1.5.0` | `10.47 2025-10-21` | `BSD-3-Clause WITH PCRE2-exception` | [`e5e5deaa30d5`](https://github.com/ltrzesniewski/pcre-net/commit/e5e5deaa30d50dd3a7ac68ec7fd9e9556551a84f) | UTF-8 standard NFA matcher, validation enabled, reusable match buffer, JIT disabled |

Admission evidence: the package has a dedicated `net10.0` asset with no managed dependencies, is strongly named, is built and tested on Windows/Linux/macOS, and its tagged source has been maintained since 2014. It bundles RID-specific native libraries, so `PrivateAssets=all` and benchmark-project-only placement are mandatory. Native replacement is left blank because PCRE.NET does not expose equivalent UTF-8 span substitution output; routing through its string API would bias the comparison.

`vs decode` is `Utf8Pcre2 / .NET + decode`; `discovery R` is `Utf8Pcre2 / PCRE.NET-PCRE2 NFA`; lower is better. Existing independently measured ratios are discovery data only and cannot determine a winner until a paired qualification protocol records a qualified Status. A dash means that the other engine cannot perform equivalent work or the snapshot does not contain that comparator. Times are medians in microseconds per public operation.

## Compatible IsMatch

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 warm alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `backtracking/assertion-capture` | **Unqualified** | 4,103 B | 1.160 us | 0.957 us | 1.21x | 2.813 us | 0.180 us | 0.563 us | 2.06x | 0 B |
| `backtracking/capture-backreference` | **Unqualified** | 22,528 B | 1.126 us | 7.380 us | 0.15x | 3.254 us | 0.056 us | 2.032 us | 0.55x | 0 B |
| `common/backtracking` | **Unqualified** | 49 B | 4.907 us | 0.811 us | 6.05x | 0.256 us | 0.664 us | 0.687 us | 7.15x | 0 B |
| `common/date-match` | **Unqualified** | 47 B | 0.246 us | 0.217 us | 1.13x | 0.286 us | 0.079 us | 0.100 us | 2.48x | 120 B |
| `common/date-miss` | **Unqualified** | 47 B | 0.709 us | 0.407 us | 1.74x | 0.870 us | 0.517 us | 0.532 us | 1.33x | 120 B |
| `common/email-match` | **Unqualified** | 34 B | 1.351 us | 0.370 us | 3.65x | 0.350 us | 0.173 us | 0.189 us | 7.17x | 0 B |
| `common/email-miss` | **Unqualified** | 35 B | 3.981 us | 0.250 us | 15.93x | 0.211 us | 0.244 us | 0.254 us | 15.70x | 0 B |
| `common/ip-match` | **Unqualified** | 15 B | 0.732 us | 0.195 us | 3.76x | 0.198 us | 0.129 us | 0.144 us | 5.09x | 0 B |
| `common/ip-miss` | **Unqualified** | 15 B | 0.656 us | 0.210 us | 3.13x | 0.168 us | 0.137 us | 0.144 us | 4.54x | 0 B |
| `common/one-node-backtracking` | **Unqualified** | 52 B | 0.193 us | 0.071 us | 2.71x | 0.206 us | 0.497 us | 0.535 us | 0.36x | 0 B |
| `common/uri-match` | **Unqualified** | 46 B | 1.608 us | 0.272 us | 5.91x | 0.523 us | 0.169 us | 0.194 us | 8.29x | 0 B |
| `common/uri-miss` | **Unqualified** | 19 B | 1.152 us | 0.386 us | 2.98x | 0.192 us | 0.122 us | 0.130 us | 8.86x | 0 B |
| `industry/boostdocs-credit-card-match` | **Unqualified** | 18 B | 0.564 us | 0.116 us | 4.87x | 0.240 us | 0.095 us | 0.108 us | 5.24x | 0 B |
| `industry/boostdocs-date-match` | **Unqualified** | 10 B | 0.391 us | 0.084 us | 4.65x | 0.293 us | 0.051 us | 0.061 us | 6.43x | 0 B |
| `industry/boostdocs-float-match` | **Unqualified** | 8 B | 0.515 us | 0.115 us | 4.49x | 0.195 us | 0.051 us | 0.058 us | 8.88x | 0 B |
| `industry/boostdocs-ftp-line-match` | **Unqualified** | 67 B | 2.200 us | 0.459 us | 4.80x | 0.202 us | 0.063 us | 0.079 us | 27.88x | 0 B |
| `industry/boostdocs-postcode-match` | **Unqualified** | 7 B | 0.390 us | 0.147 us | 2.66x | 0.483 us | 0.053 us | 0.061 us | 6.38x | 0 B |
| `literal/absolute-anchored` | **Unqualified** | 4,103 B | 0.208 us | 0.870 us | 0.24x | 0.951 us | 0.029 us | 0.378 us | 0.55x | 0 B |
| `literal/early` | **Unqualified** | 4,103 B | 0.211 us | 0.840 us | 0.25x | 0.908 us | 0.030 us | 0.493 us | 0.43x | 0 B |
| `literal/late` | **Unqualified** | 4,103 B | 0.247 us | 1.200 us | 0.21x | 0.637 us | 0.155 us | 0.510 us | 0.48x | 0 B |
| `literal/missing` | **Unqualified** | 4,096 B | 0.237 us | 0.965 us | 0.25x | 0.632 us | 0.140 us | 0.513 us | 0.46x | 0 B |
| `simple/ab-plus` | **Unqualified** | 16 B | 0.281 us | 0.088 us | 3.18x | 0.371 us | 0.040 us | 0.048 us | 5.85x | 0 B |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.139 us | 0.072 us | 1.94x | 0.525 us | 0.031 us | 0.040 us | 3.50x | 0 B |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 0.335 us | 0.086 us | 3.89x | 1.293 us | 0.053 us | 0.068 us | 4.93x | 0 B |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 0.232 us | 0.108 us | 2.15x | 0.929 us | 0.070 us | 0.091 us | 2.54x | 0 B |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 0.704 us | 0.309 us | 2.28x | 0.371 us | 0.043 us | 0.056 us | 12.50x | 0 B |

## Compatible Count

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 warm alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `backtracking/alternation-repeat` | **Unqualified** | 7,168 B | 431.717 us | 133.156 us | 3.24x | 74.342 us | 57.488 us | 58.430 us | 7.39x | 0 B |
| `character/unicode-class-dense` | **Unqualified** | 7,168 B | 32.644 us | 487.656 us | 0.07x | 130.954 us | 124.096 us | 127.889 us | 0.26x | 0 B |
| `common/matches-boundary` | **Unqualified** | 8,208 B | 9.501 us | 128.548 us | 0.07x | 8.356 us | 117.024 us | 121.438 us | 0.08x | 0 B |
| `common/matches-set` | **Unqualified** | 8,208 B | 135.401 us | 129.796 us | 1.04x | 4.678 us | 125.153 us | 128.204 us | 1.06x | 0 B |
| `common/matches-word` | **Unqualified** | 8,208 B | 2.701 us | 20.421 us | 0.13x | 2.325 us | 2.574 us | 3.487 us | 0.77x | 0 B |
| `common/matches-words` | **Unqualified** | 8,208 B | 5.081 us | 76.068 us | 0.07x | 4.598 us | 49.344 us | 51.659 us | 0.10x | 0 B |
| `industry/leipzig-name-family-count` | **Unqualified** | 16,013,977 B | 2,340.700 us | 18,407.840 us | 0.13x | 2,170.700 us | 6,984.575 us | 11,906.325 us | 0.20x | 0 B |
| `industry/leipzig-river-window-count` | **Unqualified** | 16,013,977 B | 22,675.717 us | 33,826.200 us | 0.67x | 6,390.667 us | 24,735.717 us | 29,394.467 us | 0.77x | 0 B |
| `industry/leipzig-symbol-count` | **Unqualified** | 16,013,977 B | 1,318.927 us | 267,173.700 us | 0.00x | 1,406.190 us | 13,962.068 us | 19,392.968 us | 0.07x | 0 B |
| `industry/leipzig-twain-count` | **Unqualified** | 16,013,977 B | 3,972.578 us | 9,664.589 us | 0.41x | 4,456.674 us | 1,505.897 us | 6,770.319 us | 0.59x | 0 B |
| `industry/mariomka-email-count` | **Unqualified** | 6,839,410 B | 14,827.000 us | 107,631.200 us | 0.14x | 7,132.118 us | 236.335 us | 5,447.888 us | 2.72x | 0 B |
| `industry/mariomka-ip-count` | **Unqualified** | 6,839,410 B | 15,191.957 us | 12,876.913 us | 1.18x | 3,159.179 us | 6,295.879 us | 11,488.879 us | 1.32x | 0 B |
| `industry/mariomka-uri-count` | **Unqualified** | 6,839,410 B | 27,766.882 us | 85,235.400 us | 0.33x | 11,221.527 us | 1,556.864 us | 7,443.482 us | 3.73x | 0 B |
| `industry/rust-sherlock-holmes-window-count` | **Unqualified** | 594,930 B | 1,701.026 us | 602.238 us | 2.82x | 44.616 us | 187.749 us | 311.021 us | 5.47x | 0 B |
| `industry/rust-sherlock-ing-count` | **Excluded** | 594,930 B | 7,000.719 us | — | — | 8,297.556 us | 8,591.075 us | 9,062.328 us | 0.77x | 0 B |
| `industry/rust-sherlock-letter-count` | **Unqualified** | 594,930 B | 1,643.127 us | 54,717.300 us | 0.03x | 1,124.908 us | 12,370.932 us | 17,419.332 us | 0.09x | 0 B |
| `industry/rust-sherlock-nonnewline-count` | **Unqualified** | 594,930 B | 152.928 us | 3,810.537 us | 0.04x | 146.972 us | 908.616 us | 1,427.111 us | 0.11x | 0 B |
| `industry/rust-sherlock-word-holmes-count` | **Unqualified** | 594,930 B | 2,715.809 us | 13,773.712 us | 0.20x | 27.383 us | 6,190.496 us | 5,827.548 us | 0.47x | 0 B |
| `simple/ab-plus` | **Unqualified** | 16 B | 1.141 us | 0.617 us | 1.85x | 0.555 us | 0.147 us | 0.158 us | 7.21x | 0 B |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.315 us | 0.472 us | 0.67x | 0.617 us | 0.087 us | 0.098 us | 3.22x | 0 B |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.075 us | 0.650 us | 1.65x | 1.231 us | 0.194 us | 0.205 us | 5.25x | 0 B |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 1.038 us | 0.703 us | 1.48x | 0.772 us | 0.265 us | 0.295 us | 3.53x | 0 B |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 1.518 us | 0.608 us | 2.50x | 0.507 us | 0.085 us | 0.101 us | 15.03x | 0 B |

## Compatible EnumerateMatches

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 warm alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Unqualified** | 16 B | 2.912 us | 0.284 us | 10.25x | 9.226 us | 0.201 us | 0.208 us | 13.97x | 0 B |
| `simple/foo-dense` | **Unqualified** | 18 B | 3.607 us | 0.211 us | 17.10x | 9.630 us | 0.105 us | 0.114 us | 31.55x | 0 B |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 4.496 us | 0.322 us | 13.96x | 16.080 us | 0.411 us | 0.433 us | 10.38x | 0 B |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 8.144 us | 0.383 us | 21.26x | 13.023 us | 0.287 us | 0.339 us | 23.99x | 0 B |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 6.288 us | 0.439 us | 14.32x | 11.926 us | 0.097 us | 0.205 us | 30.69x | 0 B |

## Compatible MatchMany

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 warm alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `simple/ab-plus` | **Unqualified** | 16 B | 1.329 us | 0.291 us | 4.57x | — | — | — | — | 0 B |
| `simple/foo-dense` | **Unqualified** | 18 B | 0.271 us | 0.210 us | 1.29x | — | — | — | — | 0 B |
| `simple/foo-optional-bar` | **Unqualified** | 23 B | 1.454 us | 0.309 us | 4.71x | — | — | — | — | 0 B |
| `simple/httpclient-caseless` | **Unqualified** | 45 B | 0.442 us | 0.380 us | 1.16x | — | — | — | — | 0 B |
| `simple/loglevel-multiline` | **Unqualified** | 67 B | 1.533 us | 0.440 us | 3.48x | — | — | — | — | 0 B |

## Compatible Replace

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 warm alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `common/replace-words` | **Excluded** | 8,208 B | 30.085 us | — | — | 64.896 us | 66.076 us | 65.144 us | 0.46x | 9,576 B |
| `simple/ab-plus` | **Excluded** | 16 B | 1.602 us | — | — | 5.814 us | 0.330 us | 0.215 us | 7.46x | 96 B |
| `simple/foo-dense` | **Excluded** | 18 B | 0.325 us | — | — | 4.644 us | 0.131 us | 0.141 us | 2.30x | 96 B |
| `simple/foo-optional-bar` | **Excluded** | 23 B | 1.228 us | — | — | 0.296 us | 0.244 us | 0.259 us | 4.73x | 96 B |
| `simple/httpclient-caseless` | **Excluded** | 45 B | 0.494 us | — | — | 6.417 us | 0.349 us | 0.473 us | 1.05x | 104 B |
| `simple/loglevel-multiline` | **Excluded** | 67 B | 1.852 us | — | — | 4.335 us | 0.162 us | 0.303 us | 6.11x | 128 B |

## PCRE2-only IsMatch

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Pcre2 warm alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `pcre2/backslash-c-literal` | **Unqualified** | 5 B | 0.160 us | 0.059 us | 2.73x | 0 B | 15.210 us | 31,424 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 0.598 us | 0.089 us | 6.75x | 0 B | 20.332 us | 38,736 B |
| `pcre2/conditional-accept-negative-lookahead` | **Unqualified** | 3 B | 0.299 us | 0.071 us | 4.19x | 0 B | 42.999 us | 45,296 B |
| `pcre2/conditional-lookahead` | **Unqualified** | 3 B | 0.297 us | 0.159 us | 1.88x | 0 B | 18.627 us | 56,168 B |
| `pcre2/conditional-negative-lookahead` | **Unqualified** | 3 B | 0.299 us | 0.131 us | 2.29x | 0 B | 20.289 us | 56,208 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 0.744 us | 0.295 us | 2.52x | 0 B | 32.632 us | 31,600 B |
| `pcre2/recursive-optional` | **Unqualified** | 6 B | 3.076 us | 0.379 us | 8.12x | 0 B | 21.815 us | 49,792 B |
| `pcre2/recursive-palindrome` | **Unqualified** | 6 B | 2.165 us | 0.371 us | 5.84x | 0 B | 21.970 us | 49,424 B |
| `pcre2/subroutine-prefix-digits` | **Unqualified** | 5 B | 0.596 us | 0.073 us | 8.22x | 0 B | 21.353 us | 41,376 B |

## PCRE2-only Count

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Pcre2 warm alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 5.822 us | 16.037 us | 0.36x | 0 B | 27.778 us | 37,200 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 4.273 us | 0.604 us | 7.08x | 0 B | 17.551 us | 38,736 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 0.853 us | 0.676 us | 1.26x | 0 B | 19.301 us | 39,288 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 2.991 us | 0.604 us | 4.95x | 0 B | 19.176 us | 43,696 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 0.967 us | 0.689 us | 1.40x | 0 B | 17.950 us | 43,304 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 2.503 us | 0.541 us | 4.63x | 0 B | 24.577 us | 83,248 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 2.311 us | 4.834 us | 0.48x | 0 B | 18.705 us | 31,600 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 0.946 us | 0.498 us | 1.90x | 0 B | 16.437 us | 36,752 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 0.805 us | 0.552 us | 1.46x | 0 B | 18.100 us | 41,728 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 1.883 us | — | — | 0 B | 16.105 us | 35,552 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 0.654 us | 0.493 us | 1.33x | 0 B | 16.647 us | 35,376 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 1.830 us | — | — | 0 B | 16.074 us | 34,016 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 1.955 us | 0.691 us | 2.83x | 0 B | 16.539 us | 44,360 B |

## PCRE2-only EnumerateMatches

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Pcre2 warm alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 7.297 us | 7.062 us | 1.03x | 0 B | 11.439 us | 37,200 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 5.950 us | 0.335 us | 17.76x | 0 B | 17.087 us | 38,736 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 4.311 us | 0.333 us | 12.95x | 0 B | 17.497 us | 39,288 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 7.321 us | 0.330 us | 22.18x | 0 B | 32.857 us | 43,696 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 4.381 us | 0.413 us | 10.61x | 0 B | 22.111 us | 43,304 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 7.027 us | 0.271 us | 25.93x | 0 B | 25.009 us | 83,248 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 5.894 us | 2.134 us | 2.76x | 0 B | 16.762 us | 31,600 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 4.455 us | 0.231 us | 19.29x | 0 B | 16.322 us | 36,752 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 4.200 us | 0.284 us | 14.79x | 0 B | 18.507 us | 41,728 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 5.294 us | — | — | 0 B | 15.867 us | 35,552 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 4.059 us | 0.223 us | 18.20x | 0 B | 16.110 us | 35,376 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 5.249 us | — | — | 0 B | 15.778 us | 34,016 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 5.313 us | 0.435 us | 12.21x | 0 B | 16.206 us | 44,360 B |

## PCRE2-only MatchMany

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Pcre2 warm alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `literal/empty-unicode` | **Unqualified** | 320 B | 0.694 us | 0.607 us | 1.14x | 0 B | 11.560 us | 37,200 B |
| `pcre2/branch-reset-backref` | **Unqualified** | 23 B | 2.598 us | 0.337 us | 7.71x | 0 B | 17.071 us | 38,736 B |
| `pcre2/branch-reset-basic` | **Unqualified** | 17 B | 0.897 us | 0.321 us | 2.79x | 0 B | 17.641 us | 39,288 B |
| `pcre2/branch-reset-followup` | **Unqualified** | 31 B | 2.681 us | 0.328 us | 8.17x | 0 B | 18.478 us | 43,696 B |
| `pcre2/branch-reset-nested` | **Unqualified** | 22 B | 1.017 us | 0.413 us | 2.46x | 0 B | 18.129 us | 43,304 B |
| `pcre2/duplicate-names` | **Unqualified** | 23 B | 2.540 us | 0.276 us | 9.20x | 0 B | 23.951 us | 83,248 B |
| `pcre2/grapheme-cluster` | **Unqualified** | 248 B | 0.924 us | 0.597 us | 1.55x | 0 B | 16.800 us | 31,600 B |
| `pcre2/kreset-atomic-alt` | **Unqualified** | 13 B | 0.874 us | 0.230 us | 3.80x | 0 B | 16.725 us | 36,752 B |
| `pcre2/kreset-bar-or-baz` | **Unqualified** | 26 B | 0.850 us | 0.284 us | 2.99x | 0 B | 17.599 us | 41,728 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 1.905 us | — | — | 0 B | 15.935 us | 35,552 B |
| `pcre2/kreset-global` | **Unqualified** | 26 B | 0.672 us | 0.221 us | 3.04x | 0 B | 16.314 us | 35,376 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 1.859 us | — | — | 0 B | 15.115 us | 34,016 B |
| `pcre2/same-start-global` | **Unqualified** | 9 B | 1.749 us | 0.433 us | 4.04x | 0 B | 18.739 us | 44,360 B |

## PCRE2-only Replace

| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | discovery R | Utf8Pcre2 warm alloc | Construction CPU | Construction alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `literal/empty-unicode` | **Excluded** | 320 B | 6.430 us | — | — | 528 B | 23.757 us | 37,200 B |
| `pcre2/branch-reset-backref` | **Excluded** | 23 B | 3.965 us | — | — | 96 B | 17.361 us | 38,736 B |
| `pcre2/branch-reset-basic` | **Excluded** | 17 B | 0.973 us | — | — | 96 B | 17.455 us | 39,288 B |
| `pcre2/branch-reset-followup` | **Excluded** | 31 B | 2.756 us | — | — | 96 B | 18.800 us | 43,696 B |
| `pcre2/branch-reset-nested` | **Excluded** | 22 B | 1.093 us | — | — | 96 B | 18.242 us | 43,304 B |
| `pcre2/duplicate-names` | **Excluded** | 23 B | 2.822 us | — | — | 96 B | 24.277 us | 83,248 B |
| `pcre2/grapheme-cluster` | **Excluded** | 248 B | 3.011 us | — | — | 112 B | 17.038 us | 31,600 B |
| `pcre2/kreset-atomic-alt` | **Excluded** | 13 B | 0.960 us | — | — | 88 B | 20.914 us | 36,752 B |
| `pcre2/kreset-bar-or-baz` | **Excluded** | 26 B | 0.928 us | — | — | 88 B | 18.828 us | 41,728 B |
| `pcre2/kreset-captured-repeat` | **Excluded** | 14 B | 8.216 us | — | — | 1,808 B | 17.314 us | 35,552 B |
| `pcre2/kreset-global` | **Excluded** | 26 B | 0.755 us | — | — | 96 B | 17.230 us | 35,376 B |
| `pcre2/kreset-repeat` | **Excluded** | 14 B | 3.768 us | — | — | 1,520 B | 16.361 us | 34,016 B |
| `pcre2/same-start-global` | **Excluded** | 9 B | 1.864 us | — | — | 88 B | 16.443 us | 44,360 B |

## Comparator exclusions

Rows are excluded instead of timed when result checksums differ or PCRE.NET cannot expose equivalent UTF-8 work.

| Section | Case | Reason |
|---|---|---|
| `pcre2-managed-compatible-count` | `industry/rust-sherlock-ing-count` | Checksum mismatch: managed=2081, native=1829. |
| `pcre2-managed-compatible-replace` | `common/replace-words` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/ab-plus` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/foo-dense` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/foo-optional-bar` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/httpclient-caseless` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-managed-compatible-replace` | `simple/loglevel-multiline` | PCRE.NET does not expose equivalent UTF-8 span replacement output. |
| `pcre2-special-count` | `pcre2/kreset-captured-repeat` | Checksum mismatch: managed=3, native=12. |
| `pcre2-special-count` | `pcre2/kreset-repeat` | Checksum mismatch: managed=3, native=12. |
| `pcre2-special-enumerate` | `pcre2/kreset-captured-repeat` | Checksum mismatch: managed=21, native=87. |
| `pcre2-special-enumerate` | `pcre2/kreset-repeat` | Checksum mismatch: managed=21, native=87. |
| `pcre2-special-matchmany` | `pcre2/kreset-captured-repeat` | Checksum mismatch: managed=21, native=39. |
| `pcre2-special-matchmany` | `pcre2/kreset-repeat` | Checksum mismatch: managed=21, native=39. |
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
./bench.ps1 -CommandArgs "--measure-pcre2-compatible-case","common/email-match","200","7"
./bench.ps1 -CommandArgs "--measure-pcre2-special-case","pcre2/branch-reset-basic","200","7"
./bench.ps1 -CommandArgs "--refresh-pcre2-benchmark-case","common/email-match","200","7"
./bench.ps1 -CommandArgs "--refresh-pcre2-native-baseline-case","pcre2/branch-reset-basic","200","7"
./bench.ps1 -CommandArgs "--refresh-pcre2-native-baselines","pcre2-special-ismatch","200","7"
./bench.ps1 -CommandArgs "--verify-pcre2-benchmark-markdown"
```

The benchmark catalog is [`Utf8Pcre2BenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/Utf8Pcre2BenchmarkCatalog.cs). The semantic boundary remains the managed profile described in [`SPEC-PCRE2.md`](../../SPEC-PCRE2.md), not the set of rows for which .NET has a comparator.
