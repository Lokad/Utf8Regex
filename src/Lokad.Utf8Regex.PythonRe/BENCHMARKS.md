<!-- This file is generated from ../../PythonRe.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.PythonRe benchmarks

This page is the self-contained performance regression snapshot for the optional Python `re` adapter. It is not a second regex-engine scoreboard: compatible patterns intentionally reuse the managed `Utf8Regex` core, while this catalog measures the Python-facing operation and its required result shaping. The source of truth is [`PythonRe.Benchmarks.json`](../../PythonRe.Benchmarks.json); `--refresh-pythonre-benchmarks` and `--refresh-pythonre-benchmark-case` update the JSON and regenerate this page.

Each recorded Status applies only to its measured source, runtime, interpreter and result contract. This snapshot can contain several source generations; updating one row does not qualify the others. Measurements predating the strict UTF-8/scalar migration remain historical evidence and cannot establish current-engine semantics or performance. Current immutable dependency and finite-limit observations are linked from the [adapter README](README.md).

The comparison is deliberately limited to cases where the requested work has equivalent managed semantics:

- `PythonRe`: `Utf8PythonRegex` over UTF-8 input.
- `CPython predecoded`: the official CPython `re.Pattern` / `_sre` implementation over an already-decoded `str`; this is the strong Status baseline.
- `CPython + decode`: strict UTF-8 decoding on every operation followed by the precompiled CPython `re.Pattern`; this is contextual end-to-end evidence and cannot set Status.
- `.NET predecoded`: `System.Text.RegularExpressions.Regex` over an already-decoded `string`.
- `.NET + decode`: strict UTF-8 decoding on every operation followed by `.NET Regex`; this is retained as managed-core context.

Enumeration, split, and replacement rows include the result materialization needed by the public operation. CPython is measured inside its own long-lived process because `_sre` is a CPython core module, not a standalone engine API; interpreter startup and pattern compilation are excluded. Status requires alternating paired elapsed-time samples against predecoded CPython, equal requested work, stable lanes, bounded harness-floor sensitivity, and exact source/runtime provenance. Historical independent medians remain visible for discovery but are Unqualified. Use the PCRE2 page for the separate native PCRE2 engine comparison.

Qualified `Search`, `SearchFromOffset`, `Match`, and `FullMatch` rows use the `ConsumedGroupZeroRanges` contract: every timed operation consumes success plus group-zero byte and UTF-16 boundaries. Result hashing and value verification remain outside timing. Other result-producing rows retain their required eager public materialization.

Convenience overloads are intentionally coalesced when they do not change timed work. Matched-string helpers project the same group-zero value already represented by direct matching rows; `MatchDetailed` and `FullMatchDetailed` use the same detailed capture projection represented by `SearchDetailed`, while their anchored discovery modes are covered separately by `Match` and `FullMatch`. Offset and limit overloads receive distinct rows when they change enumeration or shaping work.

Eligible ASCII one-shot rows also measure a CPython bytes `Pattern` over the identical bytes. `Rbyte` is representation-neutral engine evidence and never sets public Status; rows without equivalent byte semantics carry an explicit exclusion reason.

## Snapshot summary

- Generated: `2026-11-04T07:28:34.1579759+00:00`
- Snapshot SHA-256: `F9748A92D649D36706074C81169D57DCABED72FAE2835E6AF95C56606FCFBCF7`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `4`
- Scaling families: `7`
- Public Status: `39` managed faster, `0` equivalent, `33` CPython faster, `14` inconclusive, `4` unqualified
- Historical point measurement environments represented: `1`
- Paired evidence from source `baae83e6ef23`: `90` rows; .NET 11.0.0-rc.3.26522.148; CPython 3.13.2
- Corpus: [`tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json`](../../tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json) (`9` vectors, SHA-256 `51793C8077BE3C54BB7A45283D08FCADB32CFEAAEE06A5A90AFEE5DBA8DEBCDC`)
- Corpus provenance limitation: The original upstream CPython version was not recorded; do not infer one from local vector names.
- Historical CPython point environments represented: `1`
- Historical CPython point baseline: `CPython 3.13.2` at `/home/vermorel/tmp/utf8regex-net11-memory/python/bin/python3.13` on Linux-6.17.0-1022-azure-x86_64-with-glibc2.39

## Coverage summary

This catalog currently covers `90` operation rows over `45` distinct patterns. Zero-count sections below are deliberate, visible backlog rather than implicit coverage.

| Axis | Covered values |
|---|---|
| Operation families | `Count`, `CountFromOffset`, `FindAllStrings`, `FindAllStringsFromOffset`, `FindAllStructural`, `FindAllUtf8`, `FindIterDetailed`, `FullMatch`, `IsMatch`, `Match`, `ReplaceEvaluatorString`, `ReplaceString`, `ReplaceStringLimited`, `ReplaceUtf8`, `Search`, `SearchDetailed`, `SearchFromOffset`, `SplitDetailed`, `SplitStrings`, `SplitStringsLimited`, `SubnEvaluatorString`, `SubnEvaluatorUtf8`, `SubnString`, `SubnUtf8` |
| Flags | `Ascii`, `DotAll`, `IgnoreCase`, `Multiline`, `None`, `Unicode`, `Verbose` |
| Feature families | `ASCII class run`, `ASCII digit run`, `ASCII word boundary`, `ASCII word category`, `Anchors and fixed classes`, `Atomic group`, `Bounded exact literal replacement`, `Bounded separator class`, `Callback replacement`, `Captured separator`, `Captured separator metadata`, `Date recognition`, `Declaration and identifier structure`, `Dot-all wildcard`, `Email-like extraction`, `Empty-capable repeat`, `Empty-match replacement`, `Exact literal`, `Exact literal replacement`, `Exact literal with start offset`, `Fixed-width lookbehind`, `Greedy repeat`, `IPv4 recognition`, `Ignore-case literal`, `Leading inline flags`, `Literal alternation`, `Logging token family`, `Mixed-width captures`, `Multiline anchors`, `Named capture`, `Named capture and backreference`, `Named template expansion`, `Numeric capture and backreference`, `Numeric template expansion`, `One capture`, `Optional capture`, `Optional captured separator`, `Positive lookahead`, `Possessive repeat`, `Prefix and digit repeat`, `Quantified Unicode literal`, `Reluctant repeat`, `Scoped inline flags`, `Separated captures`, `Separator class`, `Supplementary exact literal`, `Unicode exact literal`, `Unicode literal`, `Unicode separator class`, `Unicode word category`, `Unmatched optional-group expansion`, `Verbose classes` |
| Managed route classes | `ExactAsciiLiteral`, `ExactUtf8Literal`, `GeneralNative`, `ManagedFallback`, `SimpleNative` |
| Result cardinalities | `Few`, `Four`, `Many`, `ManyEmpty`, `One`, `Three`, `Two`, `Zero` |
| Input-width classes | `Ascii`, `Ascii+ThreeByte`, `Ascii+TwoByte`, `Ascii+TwoByte+FourByte`, `Ascii+TwoByte+ThreeByte`, `Ascii+TwoByte+ThreeByte+FourByte`, `FourByte`, `ThreeByte` |
| Corpus provenance | `DotNetPerformanceReplica/Public/Data/mariomka.txt.gz`, `DotNetPerformanceReplica/Stress/Data/opensubtitles/ru-sampled.txt`, `LokadReplica/Code/Data`, `Shared definition common/date-miss`, `Shared definition common/ip-match`, `Synthetic catalog generator` |
| Claim classes | `Composed`, `Public` |

| Result section | Rows |
|---|---:|
| Direct matching | 38 |
| Detailed and scalar projections | 1 |
| Count, FindAll, and FindIter | 25 |
| Replace and Subn | 13 |
| Split | 9 |
| Real-corpus workloads | 4 |
| Construction and first call | 4 |
| Scaling evidence | 7 |
| Comparator and semantic exclusions | 0 |

### Reused subjects and corpus identities

Input hashes cover the exact decoded subject re-encoded as strict UTF-8 and timed by all lanes.

| Case | Source definition or corpus | UTF-8 bytes | SHA-256 |
|---|---|---:|---|
| `common/date-miss` | `Shared definition common/date-miss` | 47 | `1C2ACBD374A589D0C859E3C8E71C5374170E8293CCC5BB092805C8AE9D7098FA` |
| `common/ip-match` | `Shared definition common/ip-match` | 15 | `4F16BA476C8DC1B6333EFC00F6059D451373AABF9F4E5F75EB8ECE21A39563A2` |
| `corpus/mail-email-count` | `DotNetPerformanceReplica/Public/Data/mariomka.txt.gz` | 6,839,410 | `7B7F70C9CA999B2BEDE85B7ED8E37C9193EDCED196F4AED29651E37EF4F8E979` |
| `corpus/code-log-methods` | `LokadReplica/Code/Data` | 53,352 | `E25F45289E44A6DE119149C10B258E8416A8E24E4FF01D94ED4753FA5A9A351B` |
| `corpus/code-declarations` | `LokadReplica/Code/Data` | 53,352 | `E25F45289E44A6DE119149C10B258E8416A8E24E4FF01D94ED4753FA5A9A351B` |
| `corpus/sherlock-ru` | `DotNetPerformanceReplica/Stress/Data/opensubtitles/ru-sampled.txt` | 1,570,556 | `7FFDDB21336A1BFB4A9E2DF4BB77EEA0305C0010A57C5D3C56E0DFEAD9E80A90` |

Historical independent point columns were measured from source `baae83e6ef23` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 2.470 us | 22.361 us | 0.11x | 28.035 us | 37.466 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.290 us | 22.711 us | 0.10x | 28.151 us | 29.078 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.269 us | 22.211 us | 0.10x | 28.033 us | 37.508 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.317 us | 0.275 us | 1.16x | 1.786 us | 2.551 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.068 us | 0.347 us | 0.20x | 0.434 us | 0.148 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 15.920 us | 19.747 us | 0.81x | 29.342 us | 28.076 us | 0 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 1.280 us | 0.235 us | 5.44x | 5.945 us | 30.635 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.340 us | 0.184 us | 1.84x | 0.333 us | 0.084 us | 64 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.420 us | 0.358 us | 1.17x | 0.400 us | 0.174 us | 0 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.414 us | 0.288 us | 1.44x | 0.388 us | 0.052 us | 0 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.331 us | 0.336 us | 0.99x | 0.371 us | 0.116 us | 0 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.360 us | 0.266 us | 1.35x | 0.363 us | 0.086 us | 0 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.140 us | 0.336 us | 0.42x | 0.782 us | 71.945 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 1.026 us | 0.297 us | 3.44x | 0.386 us | 0.154 us | 3,552 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.349 us | 0.301 us | 1.16x | 0.405 us | 0.176 us | 0 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.291 us | 0.307 us | 0.95x | 0.350 us | 0.124 us | 0 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.248 us | 0.303 us | 0.82x | 0.331 us | 0.126 us | 0 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.382 us | 0.222 us | 1.72x | 0.321 us | 0.087 us | 0 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.736 us | 0.284 us | 2.60x | 0.383 us | 0.162 us | 2,256 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.958 us | 0.324 us | 2.96x | 0.363 us | 0.138 us | 3,552 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.164 us | 0.212 us | 0.77x | 0.319 us | 0.078 us | 0 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.187 us | 0.354 us | 3.37x | 0.384 us | 0.123 us | 3,552 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.439 us | 0.293 us | 1.50x | 0.328 us | 0.136 us | 0 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.082 us | 0.260 us | 0.31x | 0.416 us | 0.109 us | 0 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Unqualified | 0.237 us | 0.239 us | 0.99x | 0.279 us | 0.111 us | 0 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.715 us | 0.199 us | 3.59x | 0.351 us | 0.124 us | 2,256 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.251 us | 0.196 us | 1.28x | 0.299 us | 0.098 us | 0 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.406 us | 0.333 us | 1.22x | 0.368 us | 0.127 us | 0 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.968 us | 0.262 us | 3.70x | 0.368 us | 0.113 us | 3,552 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.970 us | 0.311 us | 3.10x | 0.348 us | 0.129 us | 3,552 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.777 us | 0.335 us | 2.32x | 0.372 us | 0.158 us | 0 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.118 us | 0.237 us | 0.50x | 0.342 us | 0.133 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.839 us | 0.293 us | 2.87x | 1.202 us | 0.882 us | 0 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.213 us | 0.214 us | 1.00x | 0.325 us | 0.047 us | 0 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.177 us | 0.220 us | 0.80x | 0.327 us | 0.077 us | 0 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.276 us | 0.238 us | 1.16x | 0.340 us | 0.111 us | 0 B |
| `common/date-miss` | `IsMatch` | ScalarResult | CPython faster | 2.415 us | 0.865 us | 2.79x | 1.020 us | 2.201 us | 80 B |
| `common/ip-match` | `IsMatch` | ScalarResult | Managed faster | 0.061 us | 0.323 us | 0.19x | 0.482 us | 0.266 us | 0 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | Inconclusive | 2.047 us | 1.330 us | 1.52x | 1.568 us | 0.321 us | 2,480 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 825.405 us | 2,431.089 us | 0.34x | 2,254.749 us | 833.241 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 284.277 us | 2,576.777 us | 0.11x | 2,412.139 us | 739.521 us | 0 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 60.394 us | 432.696 us | 0.14x | 532.596 us | 311.563 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 429.105 us | 368.290 us | 1.17x | 369.779 us | 425.646 us | 184,440 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 454.661 us | 517.805 us | 0.88x | 509.157 us | 471.908 us | 184,440 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 37.240 us | 27.330 us | 1.36x | 41.402 us | 75.107 us | 4,160 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | CPython faster | 103.997 us | 62.099 us | 1.67x | 76.616 us | 88.441 us | 32,928 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 555.984 us | 214.693 us | 2.59x | 220.681 us | 385.493 us | 112,944 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 749.128 us | 350.933 us | 2.14x | 378.005 us | 642.688 us | 260,400 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 831.228 us | 1,254.224 us | 0.66x | 1,199.914 us | 704.307 us | 260,400 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 577.618 us | 622.106 us | 0.93x | 623.554 us | 367.670 us | 131,176 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 262.442 us | 541.348 us | 0.49x | 535.504 us | 154.839 us | 141,568 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 809.760 us | 1,108.048 us | 0.73x | 1,121.494 us | 1,813.164 us | 0 B |
| `count/zero` | `Count` | ScalarResult | Managed faster | 29.898 us | 91.102 us | 0.33x | 92.256 us | 1.141 us | 0 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.244 us | 1.985 us | 0.12x | 2.688 us | 0.726 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | Managed faster | 253.605 us | 378.579 us | 0.67x | 355.030 us | 70.526 us | 0 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Managed faster | 20.596 us | 49.828 us | 0.41x | 48.726 us | 21.712 us | 0 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.538 us | 90.736 us | 0.01x | 91.426 us | 1.190 us | 0 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.726 us | 38.935 us | 0.02x | 39.502 us | 0.763 us | 64 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | EagerMaterializedResult | CPython faster | 18.042 us | 16.198 us | 1.11x | 17.039 us | 56.472 us | 4,152 B |
| `findall/empty-progression-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 84.694 us | 43.352 us | 1.96x | 46.413 us | 48.831 us | 16,464 B |
| `findall/optional-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 99.417 us | 55.749 us | 1.79x | 49.606 us | 105.198 us | 14,008 B |
| `findall/named-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 167.074 us | 94.815 us | 1.76x | 101.914 us | 143.418 us | 45,336 B |
| `findall/structural-many` | `FindAllStructural` | EagerMaterializedResult | Managed faster | 166.017 us | 202.260 us | 0.82x | 193.161 us | 107.019 us | 55,552 B |
| `finditer/zero-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 0.267 us | 142.736 us | 0.00x | 143.004 us | 0.620 us | 0 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 184.402 us | 177.051 us | 1.04x | 179.239 us | 220.234 us | 122,928 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | Managed faster | 174.157 us | 179.665 us | 0.97x | 206.201 us | 226.629 us | 40,984 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | Inconclusive | 92.151 us | 90.329 us | 1.02x | 91.283 us | 475.060 us | 61,488 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | Managed faster | 87.815 us | 91.893 us | 0.95x | 93.497 us | 479.303 us | 20,504 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Managed faster | 618.183 us | 17,492.739 us | 0.04x | 17,441.740 us | 336.208 us | 237,712 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Managed faster | 598.252 us | 17,325.036 us | 0.03x | 17,260.317 us | 335.692 us | 228,496 B |
| `replacement/no-match-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 1.759 us | 2.410 us | 0.73x | 3.357 us | 1.074 us | 20,016 B |
| `replacement/one-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 6,967.350 us | 16,320.868 us | 0.43x | 16,345.538 us | 0.855 us | 12,880 B |
| `replacement/named-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 1.572 us | 0.925 us | 1.70x | 1.223 us | 0.505 us | 2,608 B |
| `replacement/unmatched-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 70.350 us | 50.729 us | 1.39x | 47.663 us | 31.603 us | 9,424 B |
| `replacement/limited-string` | `ReplaceStringLimited` | EagerMaterializedResult | Inconclusive | 1.272 us | 0.456 us | 2.80x | 1.235 us | 1.052 us | 12,360 B |
| `replacement/empty-progression-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 44.202 us | 23.505 us | 1.88x | 23.377 us | 11.324 us | 2,912 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | EagerMaterializedResult | Managed faster | 158.333 us | 4,302.359 us | 0.04x | 4,283.344 us | 84.043 us | 61,024 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 284.410 us | 224.459 us | 1.27x | 227.017 us | 245.333 us | 143,488 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 459.668 us | 254.796 us | 1.80x | 256.294 us | 633.495 us | 276,776 B |
| `split/no-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.220 us | 24.994 us | 0.05x | 25.670 us | 0.704 us | 8,760 B |
| `split/one-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.269 us | 12.067 us | 0.11x | 12.611 us | 0.783 us | 4,192 B |
| `split/limited` | `SplitStringsLimited` | EagerMaterializedResult | Inconclusive | 1.566 us | 0.541 us | 2.90x | 1.264 us | 1.196 us | 6,032 B |
| `split/optional-capture` | `SplitStrings` | EagerMaterializedResult | CPython faster | 108.004 us | 84.679 us | 1.28x | 83.946 us | 85.518 us | 39,616 B |
| `split/zero-width` | `SplitStrings` | EagerMaterializedResult | CPython faster | 146.319 us | 67.004 us | 2.18x | 78.478 us | 304.093 us | 40,016 B |
| `split/unicode` | `SplitStrings` | EagerMaterializedResult | CPython faster | 157.923 us | 44.879 us | 3.52x | 42.720 us | 54.619 us | 19,512 B |
| `split/detailed` | `SplitDetailed` | EagerMaterializedResult | Managed faster | 122.940 us | 135.735 us | 0.90x | 136.174 us | 90.897 us | 104,704 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | ScalarResult | Managed faster | 4,788.427 us | 357,238.112 us | 0.01x | 367,368.247 us | 9,297.400 us | 3,568 B |
| `corpus/code-log-methods` | `Count` | ScalarResult | Managed faster | 52.264 us | 582.057 us | 0.09x | 581.775 us | 61.056 us | 1,872 B |
| `corpus/code-declarations` | `Count` | ScalarResult | Managed faster | 67.218 us | 629.129 us | 0.11x | 635.344 us | 727.386 us | 3,552 B |
| `corpus/sherlock-ru` | `Count` | ScalarResult | Managed faster | 260.513 us | 401.479 us | 0.65x | 4,143.258 us | 5,685.789 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `captured` | `(?P<word>[A-Za-z]+)-(?P=word)` | 11 B | 15.881 us / 4,897 B | 132.463 us / 53,625 B | 197.119 us / 71,617 B | 191.181 us / 73,833 B | 60.917 us | 61.142 us |
| `fallback` | `header:[0-9]+` | 12 B | 56.153 us / 3,425 B | 242.734 us / 61,689 B | 296.250 us / 83,561 B | 265.128 us / 83,593 B | 42.647 us | 42.521 us |
| `literal` | `needle` | 20 B | 18.675 us / 2,481 B | 153.537 us / 40,313 B | 340.381 us / 50,369 B | 324.203 us / 50,337 B | 24.671 us | 24.958 us |
| `unicode` | `Шерлок Холмс` | 37 B | 5.916 us / 2,905 B | 35.319 us / 33,697 B | 56.131 us / 48,313 B | 60.188 us / 48,281 B | 36.502 us | 36.562 us |

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 5.1 % / 16.3 % | 1.008 / 1.038 | 1.047 | 9.5 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 52.9 % / 0.9 % | 2.060 / 1.032 | 1.363 | 3.4 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 18.0 % / 5.6 % | 1.003 / 1.016 | 1.010 | 9.5 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Pass** | 9.9 % / 1.1 % | 1.005 / 1.028 | 1.022 | 8.0 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Pass** | 0.4 % / 10.1 % | 1.028 / 1.041 | 1.056 | 8.3 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 757.4 % / 21.9 % | 1.054 / 1.119 | 1.074 | 9.4 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `Utf8Regex/OwnedScalar; findall string shaping` | **Reject** | 1.2 % / 7.4 % | 1.072 / 1.015 | 1.047 | 4.4 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000014 us/unit managed and 0.000350 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 1.304 us | 0.241 us | 5.346x [5.051, 5.586] | 0.961 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 1.503 us | 6.115 us | 0.246x [0.239, 0.255] | 0.962 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 1.681 us | 11.526 us | 0.146x [0.139, 0.146] | 0.955 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 2.308 us | 23.262 us | 0.099x [0.094, 0.102] | 0.968 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 52.9 %/0.9 %; maximum spread is 2.060/1.032; maximum symmetric order effect is 1.363; minimum lane duration is 3.381 ms. Robust slopes are -0.091235 us/unit managed and 0.177518 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 1.846 us | 0.573 us | 3.211x [3.148, 3.301] | 1.000 | 96 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 2.010 us | 0.937 us | 2.130x [1.008, 2.636] | 1.363 | 2,400 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 1.033 us | 1.275 us | 0.811x [0.788, 0.812] | 0.987 | 2,528 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 1.314 us | 1.998 us | 0.656x [0.645, 0.672] | 0.978 | 2,784 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000029 us/unit managed and 0.000340 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.061 us | 0.146 us | 0.414x [0.410, 0.418] | 1.000 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.070 us | 0.488 us | 0.144x [0.143, 0.145] | 1.010 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.447 us | 5.719 us | 0.078x [0.078, 0.078] | 0.999 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 2.273 us | 22.358 us | 0.102x [0.102, 0.103] | 1.000 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.009403 us/unit managed and 0.116392 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 1.796 us | 23.125 us | 0.079x [0.077, 0.152] | 0.980 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 1.918 us | 24.608 us | 0.078x [0.076, 0.079] | 0.979 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 2.260 us | 30.262 us | 0.075x [0.074, 0.075] | 1.005 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 4.651 us | 52.743 us | 0.088x [0.087, 0.089] | 0.979 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.001219 us/unit managed and 0.000022 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.240 us | 0.223 us | 1.081x [1.074, 1.112] | 0.979 | 128 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.534 us | 0.223 us | 2.370x [2.355, 2.450] | 1.014 | 848 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 5.194 us | 0.344 us | 15.245x [14.967, 16.403] | 0.947 | 12,368 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 20.186 us | 0.572 us | 35.418x [35.055, 36.337] | 1.019 | 49,232 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 757.4 %/21.9 %; maximum spread is 1.054/1.119. Robust slopes are 84.662286 us/unit managed and -0.648245 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 1.244 us | 10.520 us | 0.118x [0.103, 0.124] | 1.039 | 2,360 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 117.319 us | 6.541 us | 18.117x [17.545, 19.149] | 0.972 | 70,032 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 130.583 us | 6.535 us | 19.981x [19.506, 20.285] | 0.984 | 70,032 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 274.078 us | 6.646 us | 41.241x [37.997, 50.611] | 1.074 | 137,616 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 4.358 ms. Robust slopes are 0.204177 us/unit managed and 0.092339 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 11.097 us | 4.622 us | 2.378x [2.376, 2.811] | 1.047 | 2,728 B |
| 192 results | 192 | 128 B | 192 | 0 B | 40.767 us | 18.603 us | 2.190x [2.182, 2.197] | 0.995 | 5,032 B |
| 768 results | 768 | 512 B | 768 | 0 B | 158.881 us | 71.919 us | 2.216x [2.199, 2.219] | 0.992 | 26,584 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 625.862 us | 283.179 us | 2.204x [2.190, 2.217] | 0.990 | 112,648 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.11x [0.11, 0.11]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.10x [0.10, 0.10]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.10x [0.10, 0.10]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 0.98x [0.98, 0.99]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 0.17x [0.17, 0.17]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 5.21x [5.19, 5.22]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.58x [1.58, 1.70]; CpythonFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.13x [1.13, 1.14]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.56x [1.55, 1.56]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.91x [0.91, 0.92]; Unqualified |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.29x [1.28, 1.32]; CpythonFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.37x [0.37, 0.37]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.86x [2.69, 2.92]; Inconclusive |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.88x [0.87, 0.88]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.75x [0.75, 0.76]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.62x [1.61, 1.63]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.18x [2.10, 2.22]; Inconclusive |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 2.73x [2.50, 2.76]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.73x [0.73, 0.74]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.15x [3.04, 3.17]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.35x [1.35, 1.36]; NotApplicable |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.94x [0.93, 0.94]; Unqualified |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.98x [2.84, 3.01]; Inconclusive |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 1.19x [1.18, 1.19]; CpythonFaster |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.12x [1.12, 1.13]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.48x [3.25, 3.54]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 2.93x [2.72, 2.98]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 2.12x [2.11, 2.13]; CpythonFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.76x [0.75, 0.76]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 1.09x [1.09, 1.10]; CpythonFaster |
| `capture/search-detailed` | `_sre C Pattern.search + Python detailed projection` | `Utf8Regex/FallbackRegex; detailed capture projection` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `family/count` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `class-run/count` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `unicode/count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactUtf8Literal; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/full-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/full-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `Utf8Regex/FallbackRegex; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-full-strings` | `_sre C Pattern.findall` | `Utf8Regex; exact-literal count; repeated immutable string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-full-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `Utf8Regex/ExactUtf8Literal; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/one-capture-strings` | `_sre C Pattern.findall` | `owned scalar VM; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/many-capture-strings` | `_sre C Pattern.findall` | `owned scalar VM; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/many-capture-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `owned scalar VM; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-capture-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `owned scalar VM; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `iteration/finditer-detailed` | `_sre scanner + Python detailed projection` | `owned scalar VM; detailed iteration shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `zero-width/count` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/fixed-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/fixed-utf8` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/subn-string` | `_sre C Pattern.subn` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/subn-utf8` | `_sre C Pattern.subn` | `Utf8Regex/ExactAsciiLiteral; replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-string` | `_sre C Pattern.subn + Python callback` | `owned scalar VM; callback replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-utf8` | `_sre C Pattern.subn + Python callback` | `owned scalar VM; callback replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/no-captures` | `_sre C Pattern.split` | `owned scalar VM; split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/captures` | `_sre C Pattern.split` | `owned scalar VM; split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/zero` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/one` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/empty-progression` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/from-offset` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count from a nonzero offset` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/zero-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/one-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/from-offset-strings` | `_sre C Pattern.findall` | `owned scalar VM or qualified native literal enumeration; findall string shaping from a nonzero byte offset` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/empty-progression-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/optional-capture-strings` | `_sre C Pattern.findall` | `owned scalar VM; direct optional exact-capture string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/named-capture-strings` | `_sre C Pattern.findall` | `owned scalar VM; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/structural-many` | `_sre scanner + Python group-zero projection` | `Utf8Regex/FallbackRegex; group-zero structural match shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `finditer/zero-detailed` | `_sre scanner + Python detailed projection` | `owned scalar VM; detailed iteration shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/no-match-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/one-template-string` | `_sre C Pattern.sub` | `Utf8Regex/FallbackRegex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/named-template-string` | `_sre C Pattern.sub` | `Utf8Regex/FallbackRegex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/unmatched-template-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactUtf8Literals; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/limited-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/empty-progression-string` | `_sre C Pattern.sub` | `Utf8Regex/FallbackRegex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-replace-string` | `_sre C Pattern.sub + Python callback` | `owned scalar VM; callback replacement; string shaping without count` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/no-separator` | `_sre C Pattern.split` | `owned scalar VM; split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/one-separator` | `_sre C Pattern.split` | `owned scalar VM; split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/limited` | `_sre C Pattern.split` | `owned scalar VM; bounded split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/optional-capture` | `_sre C Pattern.split` | `owned scalar VM; split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/zero-width` | `_sre C Pattern.split` | `owned scalar VM; split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/unicode` | `_sre C Pattern.split` | `owned scalar VM; split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/detailed` | `_sre C Pattern.split + Python split-item metadata projection` | `owned scalar VM; split; item-metadata shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; boolean result` | Rbyte 2.87x [2.86, 2.89]; CpythonFaster |
| `common/ip-match` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Rbyte 0.19x [0.19, 0.19]; NotApplicable |
| `corpus/mail-email-count` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `corpus/code-log-methods` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `corpus/code-declarations` | `_sre scanner + Python finditer/sum` | `owned scalar VM; byte-driven Python count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `corpus/sherlock-ru` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactUtf8Literal; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |

## Reproduce and refresh

Run from the repository root in `Release` through `./bench.ps1`:

```powershell
./bench.ps1 -CommandArgs "--measure-pythonre-paired-case","literal/search","9"
./bench.ps1 -CommandArgs "--measure-pythonre-paired-case-reversed","literal/search","9"
./bench.ps1 -CommandArgs "--qualify-pythonre-case","literal/search","9"
./bench.ps1 -CommandArgs "--resume-pythonre-qualifications","9","17","4"
./bench.ps1 -CommandArgs "--emit-pythonre-priority-report"
./bench.ps1 -CommandArgs "--emit-pythonre-coverage-report"
./bench.ps1 -CommandArgs "--verify-pythonre-coverage-contract"
./bench.ps1 -CommandArgs "--measure-pythonre-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--measure-pythonre-one-shot-phases","literal/search-early","20","5"
./bench.ps1 -CommandArgs "--measure-pythonre-shaping-case","capture/search-detailed","20","5"
./bench.ps1 -CommandArgs "--measure-pythonre-findall-phases","findall/full-strings","20","5"
./bench.ps1 -CommandArgs "--measure-pythonre-replacement-phases","replacement/fixed-string","20","5"
./bench.ps1 -CommandArgs "--measure-pythonre-split-phases","split/zero-width","20","5"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmark-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-lifecycle","32","5"
./bench.ps1 -CommandArgs "--refresh-pythonre-scaling","5"
./bench.ps1 -CommandArgs "--verify-pythonre-scaling"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmarks","200","7"
./bench.ps1 -CommandArgs "--verify-pythonre-benchmark-markdown"
```

The case definitions live in [`PythonReBenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkCatalog.cs); timed projection logic lives in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).
