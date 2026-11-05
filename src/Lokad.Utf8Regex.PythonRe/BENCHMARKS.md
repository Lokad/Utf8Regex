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

- Generated: `2026-11-05T22:14:15.6458412+00:00`
- Snapshot SHA-256: `CEC3C0919C63CB47DBFB8099D428DCBF0C94022BB0CDD8B25B85313496BB54F8`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `4`
- Scaling families: `7`
- Public Status: `40` managed faster, `2` equivalent, `35` CPython faster, `8` inconclusive, `5` unqualified
- Historical point measurement environments represented: `1`
- Paired evidence from source `3799baaa2b2f`: `90` rows; .NET 11.0.0-rc.3.26522.148; CPython 3.13.2
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

Historical independent point columns were measured from source `3799baaa2b2f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 2.444 us | 22.086 us | 0.11x | 27.740 us | 31.063 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.831 us | 22.278 us | 0.13x | 27.503 us | 27.821 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.641 us | 22.103 us | 0.07x | 27.709 us | 25.653 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.310 us | 0.271 us | 1.15x | 1.776 us | 2.534 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.071 us | 0.343 us | 0.21x | 0.433 us | 0.148 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 16.471 us | 19.508 us | 0.84x | 28.930 us | 27.156 us | 0 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.672 us | 0.231 us | 2.91x | 5.780 us | 18.379 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.307 us | 0.182 us | 1.69x | 0.326 us | 0.085 us | 64 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.398 us | 0.354 us | 1.13x | 0.392 us | 0.181 us | 0 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.426 us | 0.285 us | 1.49x | 0.382 us | 0.051 us | 0 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.304 us | 0.331 us | 0.92x | 0.365 us | 0.110 us | 0 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.318 us | 0.264 us | 1.20x | 0.355 us | 0.084 us | 0 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.130 us | 0.331 us | 0.40x | 0.756 us | 69.881 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.699 us | 0.289 us | 2.43x | 0.375 us | 0.152 us | 64 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.355 us | 0.295 us | 1.20x | 0.395 us | 0.166 us | 0 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.290 us | 0.300 us | 0.96x | 0.342 us | 0.124 us | 0 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.236 us | 0.296 us | 0.80x | 0.327 us | 0.122 us | 0 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.378 us | 0.217 us | 1.75x | 0.315 us | 0.084 us | 0 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.585 us | 0.280 us | 2.10x | 0.372 us | 0.158 us | 720 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.521 us | 0.319 us | 1.63x | 0.356 us | 0.128 us | 592 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.155 us | 0.211 us | 0.74x | 0.316 us | 0.075 us | 0 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.603 us | 0.347 us | 1.73x | 0.376 us | 0.127 us | 64 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.410 us | 0.288 us | 1.42x | 0.323 us | 0.133 us | 0 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.086 us | 0.254 us | 0.34x | 0.411 us | 0.103 us | 0 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Unqualified | 0.228 us | 0.235 us | 0.97x | 0.274 us | 0.104 us | 0 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.549 us | 0.196 us | 2.81x | 0.347 us | 0.124 us | 720 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.240 us | 0.196 us | 1.23x | 0.286 us | 0.096 us | 0 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.420 us | 0.330 us | 1.27x | 0.362 us | 0.121 us | 0 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.511 us | 0.258 us | 1.98x | 0.366 us | 0.112 us | 592 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.534 us | 0.303 us | 1.76x | 0.343 us | 0.117 us | 592 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.619 us | 0.334 us | 1.85x | 0.368 us | 0.154 us | 0 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.138 us | 0.233 us | 0.59x | 0.339 us | 0.119 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.513 us | 0.287 us | 1.77x | 1.177 us | 0.877 us | 0 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.216 us | 0.212 us | 1.02x | 0.320 us | 0.046 us | 0 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.170 us | 0.219 us | 0.78x | 0.323 us | 0.075 us | 0 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.233 us | 0.230 us | 1.01x | 0.333 us | 0.108 us | 0 B |
| `common/date-miss` | `IsMatch` | ScalarResult | CPython faster | 2.226 us | 0.852 us | 2.61x | 0.988 us | 2.144 us | 80 B |
| `common/ip-match` | `IsMatch` | ScalarResult | Managed faster | 0.058 us | 0.313 us | 0.19x | 0.482 us | 0.260 us | 0 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | Inconclusive | 1.674 us | 1.349 us | 1.24x | 1.494 us | 0.313 us | 944 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 639.025 us | 2,440.465 us | 0.26x | 2,231.968 us | 777.062 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 284.616 us | 2,560.744 us | 0.11x | 2,362.873 us | 716.759 us | 0 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 58.269 us | 428.982 us | 0.14x | 517.827 us | 309.547 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 441.767 us | 367.828 us | 1.20x | 360.862 us | 407.400 us | 184,440 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 459.350 us | 516.610 us | 0.89x | 499.080 us | 437.751 us | 184,440 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 36.285 us | 27.497 us | 1.32x | 40.693 us | 71.978 us | 4,160 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | CPython faster | 100.420 us | 61.630 us | 1.63x | 73.992 us | 81.643 us | 32,928 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 472.628 us | 213.765 us | 2.20x | 217.414 us | 386.514 us | 95,000 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 683.782 us | 348.567 us | 1.96x | 370.453 us | 587.129 us | 242,456 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 745.997 us | 1,250.937 us | 0.60x | 1,167.363 us | 726.157 us | 242,456 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 526.742 us | 619.623 us | 0.85x | 610.389 us | 348.808 us | 120,080 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 246.661 us | 537.258 us | 0.46x | 515.212 us | 149.407 us | 127,720 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 679.497 us | 1,116.272 us | 0.61x | 1,109.361 us | 1,792.776 us | 0 B |
| `count/zero` | `Count` | ScalarResult | Managed faster | 29.136 us | 89.848 us | 0.33x | 90.548 us | 1.111 us | 0 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.256 us | 2.003 us | 0.13x | 2.607 us | 0.683 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | Managed faster | 251.922 us | 374.951 us | 0.67x | 349.003 us | 68.648 us | 0 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Managed faster | 14.209 us | 49.394 us | 0.29x | 48.334 us | 21.162 us | 0 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.544 us | 89.707 us | 0.01x | 89.710 us | 1.176 us | 0 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.742 us | 38.453 us | 0.02x | 38.612 us | 0.758 us | 64 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | EagerMaterializedResult | CPython faster | 16.659 us | 15.893 us | 1.04x | 16.494 us | 54.634 us | 4,152 B |
| `findall/empty-progression-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 83.445 us | 42.828 us | 1.94x | 45.581 us | 45.257 us | 16,464 B |
| `findall/optional-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Equivalent | 54.955 us | 55.111 us | 1.00x | 48.018 us | 98.044 us | 4,800 B |
| `findall/named-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 153.638 us | 93.827 us | 1.63x | 99.232 us | 141.759 us | 43,800 B |
| `findall/structural-many` | `FindAllStructural` | EagerMaterializedResult | Managed faster | 148.298 us | 201.240 us | 0.74x | 188.683 us | 106.497 us | 37,608 B |
| `finditer/zero-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 0.268 us | 137.862 us | 0.00x | 137.679 us | 0.693 us | 0 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 187.266 us | 175.104 us | 1.07x | 175.637 us | 213.475 us | 122,928 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | Equivalent | 178.253 us | 176.627 us | 1.01x | 176.943 us | 221.515 us | 40,984 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | Managed faster | 93.825 us | 102.701 us | 0.91x | 90.561 us | 447.756 us | 61,488 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | Inconclusive | 89.347 us | 91.356 us | 0.98x | 92.389 us | 449.653 us | 20,504 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Managed faster | 586.930 us | 16,939.960 us | 0.03x | 16,945.043 us | 339.092 us | 236,176 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Managed faster | 570.039 us | 17,100.491 us | 0.03x | 16,679.481 us | 345.694 us | 226,960 B |
| `replacement/no-match-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 2.174 us | 2.375 us | 0.92x | 3.301 us | 1.225 us | 20,016 B |
| `replacement/one-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 6,660.925 us | 16,271.103 us | 0.41x | 16,124.287 us | 0.819 us | 11,344 B |
| `replacement/named-template-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 1.355 us | 0.907 us | 1.50x | 1.184 us | 0.505 us | 1,072 B |
| `replacement/unmatched-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 48.414 us | 50.706 us | 0.96x | 46.946 us | 30.973 us | 4,336 B |
| `replacement/limited-string` | `ReplaceStringLimited` | EagerMaterializedResult | Inconclusive | 1.180 us | 0.449 us | 2.63x | 1.216 us | 1.035 us | 12,360 B |
| `replacement/empty-progression-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 44.563 us | 23.465 us | 1.91x | 23.052 us | 11.266 us | 2,912 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | EagerMaterializedResult | Managed faster | 149.385 us | 4,280.453 us | 0.03x | 4,221.470 us | 83.903 us | 59,488 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 287.243 us | 224.099 us | 1.28x | 222.752 us | 234.101 us | 143,488 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 427.060 us | 255.703 us | 1.66x | 249.000 us | 779.218 us | 275,240 B |
| `split/no-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.212 us | 24.663 us | 0.05x | 25.267 us | 0.712 us | 8,760 B |
| `split/one-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.181 us | 11.985 us | 0.10x | 12.428 us | 0.753 us | 4,192 B |
| `split/limited` | `SplitStringsLimited` | EagerMaterializedResult | Inconclusive | 1.454 us | 0.534 us | 2.79x | 1.231 us | 1.181 us | 6,032 B |
| `split/optional-capture` | `SplitStrings` | EagerMaterializedResult | Managed faster | 70.369 us | 83.568 us | 0.84x | 84.925 us | 82.905 us | 34,528 B |
| `split/zero-width` | `SplitStrings` | EagerMaterializedResult | CPython faster | 124.550 us | 66.133 us | 1.89x | 77.320 us | 291.043 us | 40,016 B |
| `split/unicode` | `SplitStrings` | EagerMaterializedResult | CPython faster | 115.891 us | 43.547 us | 2.66x | 41.654 us | 50.079 us | 19,512 B |
| `split/detailed` | `SplitDetailed` | EagerMaterializedResult | Managed faster | 112.190 us | 134.799 us | 0.83x | 134.140 us | 90.228 us | 103,168 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | ScalarResult | Managed faster | 4,074.100 us | 352,583.127 us | 0.01x | 363,205.014 us | 8,358.550 us | 1,544 B |
| `corpus/code-log-methods` | `Count` | ScalarResult | Managed faster | 47.684 us | 572.153 us | 0.08x | 576.243 us | 52.130 us | 528 B |
| `corpus/code-declarations` | `Count` | ScalarResult | Managed faster | 117.241 us | 620.001 us | 0.19x | 624.231 us | 698.411 us | 592 B |
| `corpus/sherlock-ru` | `Count` | ScalarResult | Managed faster | 181.753 us | 397.318 us | 0.46x | 4,066.130 us | 5,048.910 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `captured` | `(?P<word>[A-Za-z]+)-(?P=word)` | 11 B | 17.947 us / 4,897 B | 127.609 us / 53,625 B | 202.109 us / 71,617 B | 184.256 us / 72,297 B | 60.089 us | 60.153 us |
| `fallback` | `header:[0-9]+` | 12 B | 49.803 us / 3,425 B | 348.962 us / 61,689 B | 335.400 us / 83,561 B | 268.597 us / 83,593 B | 41.867 us | 41.826 us |
| `literal` | `needle` | 20 B | 18.850 us / 2,481 B | 146.897 us / 40,313 B | 304.306 us / 50,369 B | 310.466 us / 50,337 B | 25.097 us | 25.203 us |
| `unicode` | `Шерлок Холмс` | 37 B | 5.500 us / 2,905 B | 34.594 us / 33,697 B | 55.772 us / 48,313 B | 59.497 us / 48,281 B | 35.235 us | 34.212 us |

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Reject** | 7.0 % / 67.9 % | 1.020 / 1.054 | 1.077 | 9.8 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 78.3 % / 0.6 % | 1.038 / 1.029 | 1.061 | 7.5 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Reject** | 24.6 % / 3.2 % | 1.018 / 1.017 | 1.015 | 4.1 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Pass** | 11.4 % / 1.0 % | 1.029 / 1.027 | 1.046 | 8.3 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Pass** | 4.8 % / 2.8 % | 1.023 / 1.038 | 1.032 | 8.1 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 913.3 % / 23.8 % | 1.142 / 1.082 | 1.125 | 8.8 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `Utf8Regex/OwnedScalar; findall string shaping` | **Reject** | 1.4 % / 6.8 % | 1.034 / 1.023 | 1.017 | 3.2 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 7.0 %/67.9 %. Robust slopes are 0.000030 us/unit managed and 0.000344 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 0.775 us | 0.241 us | 3.141x [2.978, 3.313] | 0.928 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 1.228 us | 6.103 us | 0.202x [0.200, 0.207] | 0.967 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 1.669 us | 11.701 us | 0.142x [0.138, 0.147] | 0.952 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 2.954 us | 22.886 us | 0.129x [0.127, 0.135] | 0.968 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 78.3 %/0.6 %. Robust slopes are -0.154406 us/unit managed and 0.174473 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 1.740 us | 0.571 us | 3.061x [2.907, 4.052] | 0.943 | 96 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 2.328 us | 0.920 us | 2.516x [2.436, 2.655] | 1.006 | 864 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 0.773 us | 1.276 us | 0.608x [0.596, 0.614] | 0.991 | 992 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 1.018 us | 1.964 us | 0.517x [0.509, 0.537] | 0.988 | 1,248 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 4.052 ms. Robust slopes are 0.000021 us/unit managed and 0.000338 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.066 us | 0.145 us | 0.454x [0.437, 0.460] | 0.985 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.071 us | 0.487 us | 0.144x [0.139, 0.149] | 0.998 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.321 us | 5.635 us | 0.057x [0.055, 0.058] | 0.985 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 1.744 us | 22.264 us | 0.079x [0.077, 0.080] | 1.003 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.008866 us/unit managed and 0.116053 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 1.879 us | 22.815 us | 0.082x [0.079, 0.099] | 0.962 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 1.973 us | 24.369 us | 0.081x [0.080, 0.082] | 0.997 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 2.304 us | 30.162 us | 0.076x [0.074, 0.079] | 1.029 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 4.655 us | 52.478 us | 0.088x [0.087, 0.091] | 1.046 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.001190 us/unit managed and 0.000022 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.252 us | 0.218 us | 1.128x [1.074, 1.168] | 1.006 | 128 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.513 us | 0.225 us | 2.304x [2.217, 2.325] | 1.003 | 848 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 5.061 us | 0.314 us | 16.037x [15.018, 16.273] | 0.969 | 12,368 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 19.815 us | 0.570 us | 35.280x [34.558, 35.705] | 1.010 | 49,232 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 913.3 %/23.8 %; maximum spread is 1.142/1.082; maximum symmetric order effect is 1.125. Robust slopes are 85.940841 us/unit managed and -0.760943 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 0.948 us | 10.998 us | 0.086x [0.080, 0.092] | 0.981 | 824 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 115.762 us | 6.465 us | 17.690x [17.292, 18.206] | 1.015 | 68,496 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 128.371 us | 6.435 us | 19.884x [19.727, 20.464] | 0.990 | 68,496 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 276.095 us | 6.522 us | 43.354x [40.737, 50.107] | 0.889 | 136,080 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 3.212 ms. Robust slopes are 0.177855 us/unit managed and 0.089674 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 9.544 us | 4.505 us | 2.092x [2.055, 2.204] | 0.999 | 1,384 B |
| 192 results | 192 | 128 B | 192 | 0 B | 35.669 us | 18.033 us | 1.953x [1.898, 2.031] | 0.994 | 3,688 B |
| 768 results | 768 | 512 B | 768 | 0 B | 137.611 us | 70.414 us | 1.972x [1.905, 1.984] | 0.993 | 25,240 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 547.384 us | 271.850 us | 1.966x [1.946, 2.028] | 0.983 | 111,304 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.11x [0.11, 0.11]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.13x [0.13, 0.13]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.07x [0.07, 0.07]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 0.97x [0.96, 0.98]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 0.18x [0.18, 0.18]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 2.77x [2.76, 2.80]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.44x [1.43, 1.49]; CpythonFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.09x [1.08, 1.10]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.63x [1.61, 1.65]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.85x [0.85, 0.86]; Unqualified |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.15x [1.14, 1.16]; CpythonFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.35x [0.34, 0.35]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.01x [2.00, 2.04]; CpythonFaster |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.89x [0.88, 0.89]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.74x [0.74, 0.74]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.64x [1.63, 1.66]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.75x [1.74, 1.77]; CpythonFaster |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.52x [1.50, 1.53]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.69x [0.68, 0.70]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.63x [1.60, 1.64]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.29x [1.27, 1.30]; NotApplicable |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.90x [0.90, 0.91]; Unqualified |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.34x [2.30, 2.36]; CpythonFaster |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 1.14x [1.13, 1.15]; CpythonFaster |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.18x [1.18, 1.19]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.88x [1.86, 1.90]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.61x [1.60, 1.64]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.70x [1.70, 1.72]; CpythonFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.73x [0.73, 0.74]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 0.93x [0.93, 0.94]; Unqualified |
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
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; boolean result` | Rbyte 2.66x [2.66, 2.68]; CpythonFaster |
| `common/ip-match` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Rbyte 0.18x [0.18, 0.19]; NotApplicable |
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
