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

- Generated: `2026-11-02T03:42:40.1196558+00:00`
- Snapshot SHA-256: `F6D7B5473F0576866F2EC80BA22802EC3743F635DE46F59C53919FBCE67CFFBE`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `4`
- Scaling families: `7`
- Public Status: `29` managed faster, `0` equivalent, `46` CPython faster, `12` inconclusive, `3` unqualified
- Historical point measurement environments represented: `1`
- Paired evidence from source `2c6cb298c484`: `90` rows; .NET 11.0.0-rc.3.26522.148; CPython 3.13.2
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

Historical independent point columns were measured from source `2c6cb298c484` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 3.631 us | 22.410 us | 0.16x | 28.090 us | 25.066 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.328 us | 22.677 us | 0.10x | 28.207 us | 30.652 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.295 us | 22.294 us | 0.10x | 28.135 us | 31.896 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.305 us | 0.274 us | 1.12x | 1.790 us | 2.582 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.093 us | 0.348 us | 0.27x | 0.434 us | 0.152 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 93.061 us | 19.700 us | 4.72x | 29.218 us | 27.514 us | 43,752 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 1.309 us | 0.235 us | 5.58x | 5.939 us | 30.509 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.383 us | 0.184 us | 2.09x | 0.351 us | 0.083 us | 64 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.445 us | 0.357 us | 1.25x | 0.400 us | 0.177 us | 0 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.465 us | 0.288 us | 1.62x | 0.385 us | 0.053 us | 0 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.361 us | 0.336 us | 1.07x | 0.372 us | 0.116 us | 0 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.389 us | 0.268 us | 1.45x | 0.363 us | 0.087 us | 0 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.149 us | 0.335 us | 0.45x | 0.793 us | 76.095 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.986 us | 0.295 us | 3.35x | 0.385 us | 0.158 us | 3,552 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.414 us | 0.301 us | 1.38x | 0.406 us | 0.181 us | 0 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.341 us | 0.307 us | 1.11x | 0.348 us | 0.127 us | 0 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.301 us | 0.302 us | 0.99x | 0.332 us | 0.130 us | 0 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.416 us | 0.220 us | 1.89x | 0.319 us | 0.089 us | 0 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.772 us | 0.283 us | 2.73x | 0.381 us | 0.170 us | 2,256 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.116 us | 0.324 us | 3.45x | 0.362 us | 0.138 us | 3,552 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.180 us | 0.213 us | 0.85x | 0.320 us | 0.080 us | 0 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.180 us | 0.355 us | 3.31x | 0.386 us | 0.127 us | 3,552 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.466 us | 0.292 us | 1.60x | 0.329 us | 0.143 us | 0 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.107 us | 0.257 us | 0.42x | 0.416 us | 0.116 us | 0 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | CPython faster | 0.298 us | 0.239 us | 1.25x | 0.278 us | 0.110 us | 0 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.728 us | 0.198 us | 3.67x | 0.352 us | 0.124 us | 2,256 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.295 us | 0.200 us | 1.47x | 0.292 us | 0.099 us | 0 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.433 us | 0.334 us | 1.30x | 0.369 us | 0.129 us | 0 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 1.727 us | 0.263 us | 6.59x | 0.369 us | 0.117 us | 3,552 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.219 us | 0.311 us | 3.89x | 0.348 us | 0.122 us | 3,552 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.742 us | 0.335 us | 2.21x | 0.371 us | 0.153 us | 0 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.151 us | 0.239 us | 0.63x | 0.342 us | 0.125 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.902 us | 0.292 us | 3.10x | 1.205 us | 0.917 us | 0 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.236 us | 0.214 us | 1.10x | 0.331 us | 0.048 us | 0 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.218 us | 0.223 us | 0.98x | 0.329 us | 0.081 us | 0 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.312 us | 0.239 us | 1.31x | 0.340 us | 0.114 us | 0 B |
| `common/date-miss` | `IsMatch` | ScalarResult | CPython faster | 3.606 us | 0.869 us | 4.15x | 1.022 us | 2.276 us | 3,568 B |
| `common/ip-match` | `IsMatch` | ScalarResult | Inconclusive | 1.273 us | 0.325 us | 3.84x | 0.485 us | 0.275 us | 3,568 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | CPython faster | 1.959 us | 1.330 us | 1.47x | 1.519 us | 0.331 us | 2,480 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 840.120 us | 2,455.593 us | 0.34x | 2,258.099 us | 806.770 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 937.024 us | 2,581.630 us | 0.36x | 2,412.798 us | 742.658 us | 0 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 62.511 us | 429.017 us | 0.15x | 524.732 us | 318.221 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 425.003 us | 370.856 us | 1.14x | 373.893 us | 432.723 us | 184,440 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 461.042 us | 521.346 us | 0.88x | 507.845 us | 457.232 us | 184,440 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 40.065 us | 27.556 us | 1.45x | 41.396 us | 72.757 us | 4,160 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | CPython faster | 103.276 us | 62.069 us | 1.66x | 76.521 us | 82.682 us | 32,936 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 526.423 us | 214.529 us | 2.45x | 221.061 us | 398.205 us | 112,944 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 754.778 us | 352.330 us | 2.14x | 378.096 us | 661.322 us | 260,400 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 828.869 us | 1,248.007 us | 0.66x | 1,199.076 us | 764.646 us | 260,400 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 574.599 us | 623.502 us | 0.92x | 630.482 us | 374.040 us | 132,792 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 263.423 us | 549.954 us | 0.48x | 530.940 us | 156.517 us | 141,568 B |
| `zero-width/count` | `Count` | ScalarResult | CPython faster | 1,656.474 us | 1,112.152 us | 1.49x | 1,122.665 us | 1,920.173 us | 0 B |
| `count/zero` | `Count` | ScalarResult | Managed faster | 0.478 us | 91.530 us | 0.01x | 92.309 us | 1.143 us | 0 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.317 us | 2.016 us | 0.16x | 2.672 us | 0.693 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | CPython faster | 461.795 us | 380.429 us | 1.21x | 352.073 us | 71.062 us | 3,552 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Managed faster | 18.490 us | 50.281 us | 0.37x | 49.149 us | 21.752 us | 0 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.595 us | 90.789 us | 0.01x | 91.776 us | 1.245 us | 0 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.776 us | 38.953 us | 0.02x | 39.471 us | 0.779 us | 64 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | EagerMaterializedResult | CPython faster | 21.574 us | 16.303 us | 1.32x | 16.661 us | 56.155 us | 4,152 B |
| `findall/empty-progression-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 144.646 us | 43.634 us | 3.32x | 46.034 us | 49.645 us | 20,016 B |
| `findall/optional-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 87.676 us | 55.678 us | 1.58x | 49.219 us | 105.138 us | 14,008 B |
| `findall/named-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 156.918 us | 94.914 us | 1.65x | 101.370 us | 150.023 us | 45,336 B |
| `findall/structural-many` | `FindAllStructural` | EagerMaterializedResult | Managed faster | 169.409 us | 202.240 us | 0.84x | 191.726 us | 112.021 us | 55,552 B |
| `finditer/zero-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 0.318 us | 140.712 us | 0.00x | 141.069 us | 0.638 us | 0 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 234.039 us | 178.677 us | 1.31x | 181.345 us | 213.583 us | 122,928 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | CPython faster | 223.307 us | 180.458 us | 1.24x | 178.939 us | 220.398 us | 40,984 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | CPython faster | 116.544 us | 91.667 us | 1.27x | 92.723 us | 474.873 us | 61,488 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | CPython faster | 111.982 us | 91.852 us | 1.22x | 93.317 us | 481.355 us | 20,504 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Managed faster | 622.802 us | 17,259.677 us | 0.04x | 16,869.286 us | 350.488 us | 237,712 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Managed faster | 609.281 us | 17,225.257 us | 0.04x | 17,253.256 us | 346.658 us | 228,496 B |
| `replacement/no-match-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 1.974 us | 2.411 us | 0.82x | 3.368 us | 1.112 us | 20,016 B |
| `replacement/one-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 6,946.587 us | 16,568.562 us | 0.42x | 16,264.844 us | 0.860 us | 12,880 B |
| `replacement/named-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 1.623 us | 0.935 us | 1.74x | 1.234 us | 0.521 us | 2,608 B |
| `replacement/unmatched-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 64.139 us | 51.010 us | 1.26x | 47.619 us | 32.623 us | 9,424 B |
| `replacement/limited-string` | `ReplaceStringLimited` | EagerMaterializedResult | Inconclusive | 1.291 us | 0.453 us | 2.83x | 1.238 us | 1.062 us | 12,360 B |
| `replacement/empty-progression-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 72.857 us | 23.780 us | 3.07x | 23.543 us | 11.823 us | 6,464 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | EagerMaterializedResult | Managed faster | 159.828 us | 4,303.627 us | 0.04x | 4,335.868 us | 89.212 us | 61,024 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 285.996 us | 224.790 us | 1.27x | 227.281 us | 242.384 us | 143,488 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 429.398 us | 257.921 us | 1.67x | 256.803 us | 826.935 us | 276,776 B |
| `split/no-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.353 us | 25.039 us | 0.05x | 25.839 us | 0.714 us | 8,760 B |
| `split/one-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.302 us | 12.072 us | 0.11x | 12.657 us | 0.785 us | 4,192 B |
| `split/limited` | `SplitStringsLimited` | EagerMaterializedResult | Inconclusive | 1.582 us | 0.544 us | 2.90x | 1.273 us | 1.222 us | 6,032 B |
| `split/optional-capture` | `SplitStrings` | EagerMaterializedResult | CPython faster | 100.227 us | 82.571 us | 1.22x | 86.028 us | 91.380 us | 39,616 B |
| `split/zero-width` | `SplitStrings` | EagerMaterializedResult | CPython faster | 252.169 us | 66.575 us | 3.78x | 79.436 us | 312.059 us | 40,016 B |
| `split/unicode` | `SplitStrings` | EagerMaterializedResult | CPython faster | 143.951 us | 44.688 us | 3.22x | 42.806 us | 58.250 us | 19,512 B |
| `split/detailed` | `SplitDetailed` | EagerMaterializedResult | Managed faster | 111.366 us | 136.153 us | 0.82x | 137.680 us | 90.799 us | 104,704 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | ScalarResult | CPython faster | 1,282,583.400 us | 358,965.642 us | 3.58x | 368,511.732 us | 9,162.417 us | 3,568 B |
| `corpus/code-log-methods` | `Count` | ScalarResult | Managed faster | 35.725 us | 580.308 us | 0.06x | 586.768 us | 56.196 us | 1,872 B |
| `corpus/code-declarations` | `Count` | ScalarResult | Managed faster | 78.275 us | 630.893 us | 0.12x | 632.932 us | 777.349 us | 3,552 B |
| `corpus/sherlock-ru` | `Count` | ScalarResult | Managed faster | 255.247 us | 402.452 us | 0.63x | 4,141.677 us | 5,722.411 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `captured` | `(?P<word>[A-Za-z]+)-(?P=word)` | 11 B | 18.184 us / 4,897 B | 136.653 us / 53,625 B | 213.912 us / 71,617 B | 188.956 us / 73,833 B | 59.915 us | 60.315 us |
| `fallback` | `header:[0-9]+` | 12 B | 52.691 us / 3,425 B | 242.178 us / 61,689 B | 322.450 us / 83,561 B | 271.484 us / 83,593 B | 42.627 us | 42.476 us |
| `literal` | `needle` | 20 B | 18.978 us / 2,481 B | 153.728 us / 40,313 B | 298.609 us / 50,369 B | 321.453 us / 50,337 B | 25.517 us | 25.243 us |
| `unicode` | `Шерлок Холмс` | 37 B | 6.222 us / 2,905 B | 34.894 us / 33,697 B | 55.584 us / 48,313 B | 60.069 us / 48,281 B | 36.583 us | 36.528 us |

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 8.7 % / 13.1 % | 1.004 / 1.036 | 1.033 | 9.9 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 55.4 % / 1.9 % | 2.078 / 1.033 | 1.406 | 3.4 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 16.8 % / 4.6 % | 1.004 / 1.015 | 1.018 | 9.9 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Pass** | 13.5 % / 1.4 % | 1.007 / 1.036 | 1.041 | 8.6 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Pass** | 1.0 % / 8.2 % | 1.023 / 1.058 | 1.077 | 9.7 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 834.0 % / 26.2 % | 1.170 / 1.084 | 1.115 | 9.7 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `Utf8Regex/OwnedScalar; findall string shaping` | **Reject** | 0.4 % / 13.2 % | 1.056 / 1.010 | 1.033 | 4.0 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000019 us/unit managed and 0.000350 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 1.094 us | 0.261 us | 4.191x [4.163, 4.221] | 1.003 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 1.540 us | 6.111 us | 0.252x [0.244, 0.259] | 0.974 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 1.713 us | 11.584 us | 0.148x [0.143, 0.149] | 0.968 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 2.339 us | 23.246 us | 0.101x [0.100, 0.103] | 0.973 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 55.4 %/1.9 %; maximum spread is 2.078/1.033; maximum symmetric order effect is 1.406; minimum lane duration is 3.388 ms. Robust slopes are -0.097992 us/unit managed and 0.174792 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 1.882 us | 0.571 us | 3.308x [3.279, 3.449] | 1.028 | 96 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 2.061 us | 0.940 us | 2.123x [1.022, 2.722] | 1.406 | 2,400 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 1.027 us | 1.283 us | 0.797x [0.785, 0.827] | 0.979 | 2,528 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 1.312 us | 1.978 us | 0.663x [0.652, 0.677] | 1.025 | 2,784 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000029 us/unit managed and 0.000342 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.076 us | 0.147 us | 0.514x [0.511, 0.520] | 0.997 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.086 us | 0.489 us | 0.175x [0.174, 0.177] | 1.018 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.469 us | 5.749 us | 0.082x [0.080, 0.082] | 0.989 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 2.303 us | 22.469 us | 0.102x [0.102, 0.103] | 0.999 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.010236 us/unit managed and 0.114533 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 2.423 us | 23.593 us | 0.106x [0.102, 0.119] | 0.961 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 2.560 us | 24.760 us | 0.103x [0.101, 0.104] | 0.973 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 2.701 us | 30.237 us | 0.089x [0.088, 0.090] | 0.994 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 5.476 us | 53.026 us | 0.103x [0.101, 0.104] | 0.981 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.001221 us/unit managed and 0.000023 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.268 us | 0.223 us | 1.223x [1.191, 1.243] | 0.975 | 128 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.559 us | 0.227 us | 2.459x [2.429, 2.569] | 1.033 | 848 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 5.303 us | 0.345 us | 15.203x [14.876, 16.448] | 0.928 | 12,368 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 20.260 us | 0.601 us | 34.140x [33.398, 35.329] | 1.030 | 49,232 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 834.0 %/26.2 %; maximum spread is 1.170/1.084; maximum symmetric order effect is 1.115. Robust slopes are 86.767879 us/unit managed and -0.901057 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 1.239 us | 12.020 us | 0.103x [0.103, 0.116] | 0.942 | 2,360 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 122.445 us | 6.598 us | 18.477x [17.983, 18.918] | 0.966 | 70,032 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 129.487 us | 6.583 us | 19.627x [19.568, 20.002] | 0.988 | 70,032 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 282.206 us | 6.656 us | 43.095x [40.514, 49.758] | 1.115 | 137,616 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 4.037 ms. Robust slopes are 0.202469 us/unit managed and 0.090019 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 11.013 us | 4.604 us | 2.388x [2.357, 2.561] | 1.033 | 2,728 B |
| 192 results | 192 | 128 B | 192 | 0 B | 40.257 us | 18.464 us | 2.187x [2.172, 2.194] | 0.996 | 5,032 B |
| 768 results | 768 | 512 B | 768 | 0 B | 157.216 us | 70.192 us | 2.226x [2.208, 2.256] | 0.987 | 26,584 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 621.513 us | 277.262 us | 2.238x [2.219, 2.254] | 0.990 | 112,648 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.16x [0.16, 0.16]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.10x [0.10, 0.10]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.10x [0.10, 0.10]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 0.95x [0.94, 0.95]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 0.23x [0.23, 0.23]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 5.32x [5.31, 5.33]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.79x [1.79, 1.80]; CpythonFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.20x [1.19, 1.20]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.75x [1.74, 1.76]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.00x [0.99, 1.00]; Unqualified |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.37x [1.37, 1.38]; CpythonFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.40x [0.39, 0.40]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.79x [2.54, 2.81]; Inconclusive |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.02x [1.01, 1.03]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.92x [0.92, 0.92]; Unqualified |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.77x [1.76, 1.78]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.29x [2.18, 2.31]; Inconclusive |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.19x [3.03, 3.21]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.80x [0.79, 0.80]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.12x [3.00, 3.18]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.44x [1.40, 1.46]; NotApplicable |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 1.18x [1.17, 1.18]; CpythonFaster |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 3.04x [2.89, 3.08]; Inconclusive |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 1.39x [1.38, 1.40]; CpythonFaster |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.20x [1.20, 1.20]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 6.24x [6.17, 6.26]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.43x [3.36, 3.45]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 2.03x [2.02, 2.04]; CpythonFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.93x [0.92, 0.93]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 1.23x [1.23, 1.24]; CpythonFaster |
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
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; boolean result` | Rbyte 4.26x [4.24, 4.28]; CpythonFaster |
| `common/ip-match` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; boolean result` | Rbyte 3.84x [3.74, 3.88]; Inconclusive |
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
