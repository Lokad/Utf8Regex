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

- Generated: `2026-11-04T20:39:52.2131871+00:00`
- Snapshot SHA-256: `467C44F0D835628ED458C91AF56FA7FBF8524BC9156A3F00EAEC483491271F1D`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `4`
- Scaling families: `7`
- Public Status: `37` managed faster, `0` equivalent, `37` CPython faster, `12` inconclusive, `4` unqualified
- Historical point measurement environments represented: `1`
- Paired evidence from source `ba457088fc45`: `90` rows; .NET 11.0.0-rc.3.26522.148; CPython 3.13.2
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

Historical independent point columns were measured from source `ba457088fc45` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 2.402 us | 22.035 us | 0.11x | 27.365 us | 32.396 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.795 us | 21.955 us | 0.13x | 27.018 us | 46.101 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.634 us | 22.010 us | 0.07x | 27.810 us | 37.853 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.307 us | 0.268 us | 1.15x | 1.748 us | 2.509 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.073 us | 0.341 us | 0.21x | 0.418 us | 0.151 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 15.207 us | 19.372 us | 0.79x | 28.272 us | 26.768 us | 0 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.668 us | 0.230 us | 2.91x | 5.785 us | 18.493 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.337 us | 0.180 us | 1.88x | 0.324 us | 0.084 us | 64 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.422 us | 0.352 us | 1.20x | 0.387 us | 0.178 us | 0 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.411 us | 0.286 us | 1.44x | 0.391 us | 0.051 us | 0 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.332 us | 0.332 us | 1.00x | 0.363 us | 0.119 us | 0 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.356 us | 0.264 us | 1.35x | 0.353 us | 0.086 us | 0 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.142 us | 0.332 us | 0.43x | 0.759 us | 79.599 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.957 us | 0.290 us | 3.32x | 0.372 us | 0.156 us | 3,552 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.345 us | 0.294 us | 1.18x | 0.396 us | 0.177 us | 0 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.296 us | 0.303 us | 0.98x | 0.343 us | 0.125 us | 0 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.239 us | 0.294 us | 0.81x | 0.321 us | 0.126 us | 0 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.375 us | 0.216 us | 1.72x | 0.308 us | 0.084 us | 0 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.735 us | 0.276 us | 2.64x | 0.373 us | 0.164 us | 2,256 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.916 us | 0.321 us | 2.85x | 0.355 us | 0.139 us | 3,552 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.162 us | 0.208 us | 0.78x | 0.310 us | 0.076 us | 0 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.158 us | 0.341 us | 3.42x | 0.379 us | 0.119 us | 3,552 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.439 us | 0.288 us | 1.53x | 0.314 us | 0.136 us | 0 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.084 us | 0.253 us | 0.34x | 0.407 us | 0.109 us | 0 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Unqualified | 0.231 us | 0.233 us | 0.99x | 0.271 us | 0.108 us | 0 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.683 us | 0.194 us | 3.53x | 0.342 us | 0.124 us | 2,256 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.235 us | 0.194 us | 1.22x | 0.282 us | 0.096 us | 0 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.400 us | 0.356 us | 1.12x | 0.360 us | 0.126 us | 0 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.926 us | 0.257 us | 3.58x | 0.360 us | 0.112 us | 3,552 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.990 us | 0.303 us | 3.26x | 0.337 us | 0.120 us | 3,552 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.808 us | 0.330 us | 2.46x | 0.363 us | 0.157 us | 0 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.136 us | 0.241 us | 0.57x | 0.337 us | 0.126 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.517 us | 0.288 us | 1.79x | 1.173 us | 0.860 us | 0 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.213 us | 0.210 us | 1.01x | 0.320 us | 0.046 us | 0 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.177 us | 0.217 us | 0.82x | 0.322 us | 0.077 us | 0 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.243 us | 0.234 us | 1.05x | 0.330 us | 0.108 us | 0 B |
| `common/date-miss` | `IsMatch` | ScalarResult | CPython faster | 2.426 us | 0.859 us | 2.83x | 0.975 us | 2.266 us | 80 B |
| `common/ip-match` | `IsMatch` | ScalarResult | Managed faster | 0.062 us | 0.313 us | 0.20x | 0.476 us | 0.268 us | 0 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | CPython faster | 1.925 us | 1.296 us | 1.49x | 1.514 us | 0.311 us | 2,480 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 675.561 us | 2,434.825 us | 0.28x | 2,186.532 us | 770.153 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 291.037 us | 2,507.735 us | 0.12x | 2,351.336 us | 704.035 us | 0 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 57.929 us | 427.442 us | 0.14x | 508.444 us | 300.773 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 428.778 us | 364.185 us | 1.18x | 361.336 us | 415.507 us | 184,440 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 460.570 us | 513.393 us | 0.90x | 500.017 us | 430.866 us | 184,440 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 34.060 us | 26.902 us | 1.26x | 41.007 us | 71.948 us | 4,160 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | CPython faster | 95.350 us | 61.150 us | 1.56x | 73.451 us | 80.324 us | 32,928 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 497.932 us | 209.627 us | 2.38x | 215.674 us | 395.318 us | 112,944 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 722.613 us | 344.566 us | 2.08x | 366.878 us | 744.997 us | 260,400 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 775.523 us | 1,232.558 us | 0.63x | 1,188.130 us | 700.268 us | 260,400 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 565.367 us | 613.173 us | 0.92x | 608.600 us | 361.875 us | 131,176 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 255.398 us | 532.273 us | 0.48x | 508.718 us | 154.726 us | 141,568 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 832.537 us | 1,079.725 us | 0.77x | 1,099.435 us | 1,944.270 us | 0 B |
| `count/zero` | `Count` | ScalarResult | Managed faster | 29.350 us | 90.104 us | 0.33x | 89.273 us | 1.092 us | 0 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.278 us | 1.975 us | 0.14x | 2.615 us | 0.688 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | Managed faster | 253.385 us | 365.899 us | 0.69x | 343.706 us | 68.082 us | 0 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Managed faster | 14.514 us | 48.765 us | 0.30x | 47.693 us | 21.718 us | 0 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.504 us | 88.608 us | 0.01x | 89.553 us | 1.199 us | 0 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.740 us | 37.991 us | 0.02x | 38.206 us | 0.747 us | 64 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | EagerMaterializedResult | CPython faster | 16.204 us | 15.750 us | 1.03x | 16.287 us | 53.395 us | 4,152 B |
| `findall/empty-progression-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 87.730 us | 42.796 us | 2.05x | 45.571 us | 46.426 us | 16,464 B |
| `findall/optional-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 72.193 us | 54.243 us | 1.33x | 47.621 us | 98.464 us | 12,328 B |
| `findall/named-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 162.417 us | 92.779 us | 1.75x | 98.641 us | 141.397 us | 45,336 B |
| `findall/structural-many` | `FindAllStructural` | EagerMaterializedResult | Managed faster | 162.180 us | 199.696 us | 0.81x | 185.056 us | 105.712 us | 55,552 B |
| `finditer/zero-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 0.269 us | 148.487 us | 0.00x | 137.369 us | 0.609 us | 0 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 195.437 us | 173.382 us | 1.12x | 175.992 us | 212.173 us | 122,928 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | CPython faster | 183.384 us | 174.587 us | 1.06x | 178.916 us | 217.122 us | 40,984 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | CPython faster | 97.194 us | 88.560 us | 1.10x | 89.991 us | 454.640 us | 61,488 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | CPython faster | 93.062 us | 88.977 us | 1.05x | 94.224 us | 458.015 us | 20,504 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Managed faster | 601.859 us | 16,996.021 us | 0.04x | 16,678.871 us | 329.070 us | 237,712 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Managed faster | 615.299 us | 16,872.193 us | 0.04x | 16,662.749 us | 329.314 us | 228,496 B |
| `replacement/no-match-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 1.849 us | 2.367 us | 0.78x | 3.301 us | 1.057 us | 20,016 B |
| `replacement/one-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 6,611.875 us | 16,082.359 us | 0.41x | 15,916.107 us | 0.824 us | 12,880 B |
| `replacement/named-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 1.579 us | 0.889 us | 1.78x | 1.197 us | 0.513 us | 2,608 B |
| `replacement/unmatched-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 60.754 us | 49.705 us | 1.23x | 46.500 us | 30.928 us | 7,744 B |
| `replacement/limited-string` | `ReplaceStringLimited` | EagerMaterializedResult | Inconclusive | 1.270 us | 0.448 us | 2.86x | 1.186 us | 1.023 us | 12,360 B |
| `replacement/empty-progression-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 48.180 us | 23.278 us | 2.07x | 22.884 us | 10.966 us | 2,912 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | EagerMaterializedResult | Managed faster | 155.292 us | 4,259.138 us | 0.04x | 4,092.244 us | 83.605 us | 61,024 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 291.290 us | 218.577 us | 1.33x | 221.320 us | 236.698 us | 143,488 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 443.799 us | 250.789 us | 1.77x | 249.079 us | 874.560 us | 276,776 B |
| `split/no-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.253 us | 24.550 us | 0.05x | 24.938 us | 0.679 us | 8,760 B |
| `split/one-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.198 us | 11.907 us | 0.10x | 12.230 us | 0.749 us | 4,192 B |
| `split/limited` | `SplitStringsLimited` | EagerMaterializedResult | Inconclusive | 1.462 us | 0.545 us | 2.65x | 1.234 us | 1.182 us | 6,032 B |
| `split/optional-capture` | `SplitStrings` | EagerMaterializedResult | CPython faster | 87.231 us | 80.831 us | 1.07x | 83.519 us | 84.329 us | 37,936 B |
| `split/zero-width` | `SplitStrings` | EagerMaterializedResult | CPython faster | 147.375 us | 65.925 us | 2.23x | 77.428 us | 319.171 us | 40,016 B |
| `split/unicode` | `SplitStrings` | EagerMaterializedResult | CPython faster | 163.265 us | 43.612 us | 3.75x | 41.997 us | 52.618 us | 19,512 B |
| `split/detailed` | `SplitDetailed` | EagerMaterializedResult | Managed faster | 117.624 us | 132.928 us | 0.88x | 133.884 us | 87.513 us | 104,704 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | ScalarResult | Managed faster | 4,438.558 us | 348,703.166 us | 0.01x | 359,889.894 us | 8,447.917 us | 3,568 B |
| `corpus/code-log-methods` | `Count` | ScalarResult | Managed faster | 48.256 us | 566.121 us | 0.09x | 566.962 us | 51.204 us | 1,872 B |
| `corpus/code-declarations` | `Count` | ScalarResult | Managed faster | 77.059 us | 615.021 us | 0.12x | 616.686 us | 772.766 us | 3,552 B |
| `corpus/sherlock-ru` | `Count` | ScalarResult | Managed faster | 213.944 us | 393.265 us | 0.55x | 4,015.980 us | 5,136.322 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `captured` | `(?P<word>[A-Za-z]+)-(?P=word)` | 11 B | 16.900 us / 4,897 B | 129.553 us / 53,625 B | 200.347 us / 71,617 B | 189.559 us / 73,833 B | 60.336 us | 60.214 us |
| `fallback` | `header:[0-9]+` | 12 B | 53.356 us / 3,425 B | 252.375 us / 61,689 B | 312.059 us / 83,561 B | 266.137 us / 83,593 B | 41.531 us | 42.558 us |
| `literal` | `needle` | 20 B | 18.372 us / 2,481 B | 149.847 us / 40,313 B | 293.200 us / 50,369 B | 314.059 us / 50,337 B | 24.883 us | 24.917 us |
| `unicode` | `Шерлок Холмс` | 37 B | 5.987 us / 2,905 B | 33.466 us / 33,697 B | 53.094 us / 48,313 B | 59.284 us / 48,281 B | 36.471 us | 35.743 us |

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 6.3 % / 19.1 % | 1.031 / 1.064 | 1.084 | 9.7 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 53.5 % / 6.3 % | 2.129 / 1.044 | 1.360 | 3.2 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 22.3 % / 4.0 % | 1.034 / 1.026 | 1.040 | 5.9 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Pass** | 10.0 % / 4.3 % | 1.029 / 1.042 | 1.049 | 8.5 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Reject** | 3.9 % / 7.6 % | 1.018 / 1.062 | 1.103 | 9.7 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 612.9 % / 26.4 % | 1.092 / 1.029 | 1.143 | 8.9 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `Utf8Regex/OwnedScalar; findall string shaping` | **Reject** | 1.6 % / 3.6 % | 1.087 / 1.027 | 1.046 | 4.6 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000030 us/unit managed and 0.000347 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 0.769 us | 0.239 us | 3.254x [2.940, 3.295] | 0.923 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 1.219 us | 6.013 us | 0.199x [0.194, 0.212] | 0.923 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 1.655 us | 12.202 us | 0.139x [0.129, 0.145] | 0.930 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 2.894 us | 22.604 us | 0.127x [0.124, 0.131] | 0.968 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 53.5 %/6.3 %; maximum spread is 2.129/1.044; maximum symmetric order effect is 1.360; minimum lane duration is 3.177 ms. Robust slopes are -0.093351 us/unit managed and 0.164718 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 1.754 us | 0.568 us | 3.110x [3.034, 3.693] | 0.988 | 96 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 1.929 us | 0.952 us | 2.056x [0.891, 2.644] | 1.360 | 2,400 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 0.966 us | 1.270 us | 0.764x [0.733, 0.778] | 0.963 | 2,528 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 1.214 us | 1.914 us | 0.638x [0.605, 0.650] | 1.020 | 2,784 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000021 us/unit managed and 0.000337 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.062 us | 0.146 us | 0.425x [0.420, 0.445] | 0.966 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.070 us | 0.492 us | 0.146x [0.142, 0.150] | 1.040 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.327 us | 5.646 us | 0.058x [0.056, 0.060] | 0.995 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 1.726 us | 22.223 us | 0.078x [0.077, 0.078] | 0.992 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.008795 us/unit managed and 0.108798 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 1.873 us | 23.484 us | 0.082x [0.079, 0.098] | 0.962 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 1.966 us | 24.573 us | 0.080x [0.078, 0.083] | 0.953 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 2.307 us | 29.422 us | 0.078x [0.076, 0.082] | 0.987 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 4.559 us | 53.325 us | 0.088x [0.085, 0.088] | 0.968 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum symmetric order effect is 1.103. Robust slopes are 0.001186 us/unit managed and 0.000024 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.241 us | 0.212 us | 1.150x [1.114, 1.206] | 0.950 | 128 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.507 us | 0.218 us | 2.308x [2.119, 2.352] | 1.003 | 848 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 5.023 us | 0.335 us | 15.009x [14.417, 16.759] | 0.907 | 12,368 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 19.804 us | 0.578 us | 34.821x [33.371, 35.767] | 1.016 | 49,232 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 612.9 %/26.4 %; maximum symmetric order effect is 1.143. Robust slopes are 92.531541 us/unit managed and -0.846603 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 1.177 us | 11.761 us | 0.100x [0.097, 0.104] | 0.998 | 2,360 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 117.756 us | 6.464 us | 18.071x [18.014, 18.275] | 1.008 | 70,032 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 126.218 us | 6.499 us | 19.612x [18.738, 20.082] | 0.983 | 70,032 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 293.200 us | 6.577 us | 44.522x [42.475, 49.987] | 1.143 | 137,616 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 4.618 ms. Robust slopes are 0.194367 us/unit managed and 0.088656 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 10.967 us | 4.629 us | 2.413x [2.362, 2.791] | 1.046 | 2,728 B |
| 192 results | 192 | 128 B | 192 | 0 B | 39.593 us | 18.047 us | 2.186x [2.171, 2.240] | 0.991 | 5,032 B |
| 768 results | 768 | 512 B | 768 | 0 B | 150.939 us | 67.800 us | 2.242x [2.156, 2.272] | 0.957 | 26,584 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 598.750 us | 273.059 us | 2.200x [2.159, 2.281] | 0.984 | 112,648 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.11x [0.11, 0.11]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.13x [0.13, 0.13]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.07x [0.07, 0.07]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 0.97x [0.97, 0.98]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 0.18x [0.18, 0.19]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 2.78x [2.76, 2.78]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.59x [1.56, 1.71]; CpythonFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.15x [1.14, 1.16]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.56x [1.55, 1.59]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.88x [0.87, 0.92]; Inconclusive |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.29x [1.27, 1.30]; CpythonFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.38x [0.38, 0.38]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.77x [2.58, 2.87]; Inconclusive |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.90x [0.89, 0.91]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.75x [0.74, 0.75]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.63x [1.61, 1.64]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.23x [2.09, 2.27]; Inconclusive |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 2.65x [2.37, 2.71]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.73x [0.73, 0.74]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.13x [2.95, 3.18]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.38x [1.37, 1.39]; NotApplicable |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.93x [0.93, 0.94]; Unqualified |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 2.92x [2.76, 3.01]; Inconclusive |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 1.12x [1.11, 1.12]; CpythonFaster |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.13x [1.13, 1.15]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.37x [3.13, 3.50]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 3.00x [2.77, 3.07]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 2.23x [2.21, 2.28]; Inconclusive |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.76x [0.76, 0.77]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 0.98x [0.97, 0.99]; Unqualified |
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
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; boolean result` | Rbyte 2.91x [2.89, 2.94]; CpythonFaster |
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
