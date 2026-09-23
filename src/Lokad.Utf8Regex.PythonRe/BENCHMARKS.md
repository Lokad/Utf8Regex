<!-- This file is generated from ../../PythonRe.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.PythonRe benchmarks

This page is the self-contained performance regression snapshot for the optional Python `re` adapter. It is not a second regex-engine scoreboard: compatible patterns intentionally reuse the managed `Utf8Regex` core, while this catalog measures the Python-facing operation and its required result shaping. The source of truth is [`PythonRe.Benchmarks.json`](../../PythonRe.Benchmarks.json); `--refresh-pythonre-benchmarks` and `--refresh-pythonre-benchmark-case` update the JSON and regenerate this page.

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

- Generated: `2026-09-23T04:18:41.1815102+00:00`
- Snapshot SHA-256: `AD3059EAFBD00B6D792FEE3D60C9DB99E74D95757DF2ECC1ED21CAE9C9914BAF`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `0`
- Scaling families: `7`
- Public Status: `42` managed faster, `0` equivalent, `3` CPython faster, `19` inconclusive, `26` unqualified
- Historical point measurement environments represented: `1`
- Corpus: [`tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json`](../../tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json) (`9` vectors, SHA-256 `51793C8077BE3C54BB7A45283D08FCADB32CFEAAEE06A5A90AFEE5DBA8DEBCDC`)
- Corpus provenance limitation: The original upstream CPython version was not recorded; do not infer one from local vector names.
- Historical CPython point environments represented: `1`
- Historical CPython point baseline: `CPython 3.12.3` at `/usr/bin/python3` on Linux-6.17.0-1022-azure-x86_64-with-glibc2.39

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
| Construction and first call | 0 |
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

Historical independent point columns were measured from source `30f9b58c44cc` on .NET 10.0.12, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 1.759 us | 12.139 us | 0.14x | 26.157 us | 21.753 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.919 us | 12.356 us | 0.15x | 26.397 us | 15.507 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.871 us | 12.245 us | 0.15x | 25.872 us | 14.743 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.211 us | 0.164 us | 1.30x | 1.635 us | 2.192 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.145 us | 0.227 us | 0.64x | 0.362 us | 0.175 us | 360 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 15.154 us | 13.838 us | 1.09x | 23.220 us | 27.260 us | 14,360 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 1.037 us | 0.145 us | 7.18x | 5.304 us | 15.841 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.069 us | 0.104 us | 0.66x | 0.265 us | 0.085 us | 40 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.115 us | 0.240 us | 0.48x | 0.347 us | 0.200 us | 280 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.051 us | 0.177 us | 0.29x | 0.348 us | 0.055 us | 64 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.086 us | 0.215 us | 0.39x | 0.292 us | 0.126 us | 272 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.072 us | 0.208 us | 0.34x | 0.296 us | 0.083 us | 64 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.063 us | 0.213 us | 0.29x | 0.667 us | 72.246 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.064 us | 0.191 us | 0.34x | 0.309 us | 0.160 us | 48 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.122 us | 0.182 us | 0.67x | 0.322 us | 0.170 us | 312 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.087 us | 0.195 us | 0.45x | 0.281 us | 0.135 us | 248 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.096 us | 0.181 us | 0.53x | 0.261 us | 0.140 us | 256 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.057 us | 0.126 us | 0.45x | 0.255 us | 0.085 us | 40 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.078 us | 0.159 us | 0.49x | 0.296 us | 0.177 us | 48 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.090 us | 0.199 us | 0.45x | 0.283 us | 0.148 us | 248 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.048 us | 0.129 us | 0.37x | 0.252 us | 0.076 us | 32 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.085 us | 0.215 us | 0.40x | 0.293 us | 0.139 us | 248 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.098 us | 0.178 us | 0.55x | 0.259 us | 0.154 us | 248 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.053 us | 0.152 us | 0.35x | 0.334 us | 0.108 us | 32 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Managed faster | 0.079 us | 0.144 us | 0.54x | 0.205 us | 0.118 us | 264 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.083 us | 0.106 us | 0.78x | 0.289 us | 0.139 us | 48 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.087 us | 0.114 us | 0.77x | 0.230 us | 0.095 us | 48 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.093 us | 0.197 us | 0.47x | 0.287 us | 0.139 us | 296 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.063 us | 0.189 us | 0.33x | 0.289 us | 0.110 us | 40 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.102 us | 0.215 us | 0.47x | 0.274 us | 0.135 us | 248 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.112 us | 0.232 us | 0.48x | 0.300 us | 0.173 us | 272 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.119 us | 0.143 us | 0.83x | 0.257 us | 0.124 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.294 us | 0.177 us | 1.67x | 1.016 us | 0.999 us | 0 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.055 us | 0.148 us | 0.37x | 0.264 us | 0.045 us | 32 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.064 us | 0.136 us | 0.47x | 0.262 us | 0.076 us | 32 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.089 us | 0.148 us | 0.60x | 0.265 us | 0.111 us | 48 B |
| `common/date-miss` | `IsMatch` | Historical | Unqualified | 2.180 us | 1.073 us | 2.03x | 1.191 us | 2.181 us | 120 B |
| `common/ip-match` | `IsMatch` | Historical | Unqualified | 0.351 us | 0.255 us | 1.37x | 0.391 us | 0.264 us | 264 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | Inconclusive | 0.282 us | 0.783 us | 0.35x | 1.331 us | 0.357 us | 896 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Inconclusive | 70.036 us | 1,355.226 us | 0.05x | 1,477.809 us | 764.313 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 386.476 us | 1,401.141 us | 0.28x | 1,747.536 us | 697.325 us | 172,056 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 25.337 us | 228.452 us | 0.11x | 360.670 us | 310.392 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | Inconclusive | 207.911 us | 227.929 us | 0.90x | 267.831 us | 405.500 us | 165,936 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 163.113 us | 308.266 us | 0.53x | 386.740 us | 455.065 us | 165,936 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 12.096 us | 16.155 us | 0.74x | 33.968 us | 72.601 us | 4,120 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Inconclusive | 32.393 us | 37.762 us | 0.85x | 61.262 us | 82.955 us | 24,600 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 108.427 us | 128.736 us | 0.84x | 180.681 us | 387.484 us | 149,792 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 179.990 us | 205.639 us | 0.87x | 308.273 us | 625.652 us | 299,296 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Inconclusive | 340.432 us | 725.307 us | 0.47x | 1,037.212 us | 719.675 us | 1,528,096 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 211.538 us | 345.114 us | 0.60x | 567.932 us | 382.628 us | 785,704 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Inconclusive | 95.770 us | 336.809 us | 0.28x | 454.376 us | 162.644 us | 415,976 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 17.161 us | 591.391 us | 0.03x | 720.494 us | 1,814.579 us | 34,840 B |
| `count/zero` | `Count` | ScalarResult | Inconclusive | 1.272 us | 49.590 us | 0.03x | 76.829 us | 1.133 us | 17,432 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.171 us | 1.097 us | 0.16x | 2.416 us | 0.705 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | Inconclusive | 168.993 us | 186.195 us | 0.89x | 254.161 us | 67.819 us | 430,312 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Inconclusive | 31.282 us | 25.875 us | 1.22x | 30.059 us | 20.884 us | 8,216 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 1.959 us | 48.675 us | 0.04x | 76.976 us | 1.174 us | 17,456 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 1.413 us | 20.913 us | 0.07x | 32.008 us | 0.791 us | 8,288 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | Historical | Unqualified | 7.744 us | 13.640 us | 0.57x | 14.357 us | 55.401 us | 4,120 B |
| `findall/empty-progression-strings` | `FindAllStrings` | Historical | Unqualified | 1.669 us | 32.332 us | 0.05x | 32.750 us | 48.585 us | 4,128 B |
| `findall/optional-capture-strings` | `FindAllStrings` | Historical | Unqualified | 52.122 us | 45.246 us | 1.15x | 45.175 us | 100.983 us | 15,088 B |
| `findall/named-capture-strings` | `FindAllStrings` | Historical | Unqualified | 76.638 us | 75.083 us | 1.02x | 76.012 us | 143.500 us | 58,120 B |
| `findall/structural-many` | `FindAllStructural` | Historical | Unqualified | 84.959 us | 153.155 us | 0.55x | 154.318 us | 105.079 us | 46,640 B |
| `finditer/zero-detailed` | `FindIterDetailed` | Historical | Unqualified | 1.079 us | 150.581 us | 0.01x | 152.775 us | 0.610 us | 17,456 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 52.081 us | 113.982 us | 0.46x | 169.060 us | 239.518 us | 123,144 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | Managed faster | 45.898 us | 115.441 us | 0.40x | 171.480 us | 246.972 us | 41,200 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | Managed faster | 25.452 us | 57.897 us | 0.44x | 85.808 us | 456.811 us | 61,704 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | Inconclusive | 23.901 us | 70.203 us | 0.38x | 86.854 us | 452.245 us | 20,720 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Inconclusive | 210.027 us | 9,829.700 us | 0.02x | 14,665.680 us | 344.504 us | 878,872 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Managed faster | 213.837 us | 9,775.917 us | 0.02x | 14,662.325 us | 341.058 us | 875,768 B |
| `replacement/no-match-string` | `ReplaceString` | Historical | Unqualified | 1.447 us | 2.270 us | 0.64x | 3.032 us | 7.428 us | 20,232 B |
| `replacement/one-template-string` | `ReplaceString` | Historical | Unqualified | 1.055 us | 16,940.993 us | 0.00x | 16,912.899 us | 2.834 us | 8,496 B |
| `replacement/named-template-string` | `ReplaceString` | Historical | Unqualified | 0.674 us | 0.774 us | 0.87x | 1.014 us | 0.489 us | 360 B |
| `replacement/unmatched-template-string` | `ReplaceString` | Historical | Unqualified | 30.283 us | 41.912 us | 0.72x | 42.345 us | 31.077 us | 3,344 B |
| `replacement/limited-string` | `ReplaceStringLimited` | Historical | Unqualified | 0.916 us | 0.395 us | 2.32x | 1.017 us | 5.174 us | 12,576 B |
| `replacement/empty-progression-string` | `ReplaceString` | Historical | Unqualified | 11.399 us | 19.178 us | 0.59x | 19.637 us | 11.689 us | 1,792 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | Historical | Unqualified | 94.261 us | 3,689.305 us | 0.03x | 3,742.458 us | 84.972 us | 219,928 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 109.667 us | 127.460 us | 0.85x | 170.175 us | 245.445 us | 269,640 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 327.524 us | 153.988 us | 2.12x | 199.787 us | 792.465 us | 499,048 B |
| `split/no-separator` | `SplitStrings` | Historical | Unqualified | 1.511 us | 23.871 us | 0.06x | 22.865 us | 4.948 us | 8,920 B |
| `split/one-separator` | `SplitStrings` | Historical | Unqualified | 1.529 us | 11.879 us | 0.13x | 11.303 us | 2.801 us | 8,496 B |
| `split/limited` | `SplitStringsLimited` | Historical | Unqualified | 1.784 us | 0.436 us | 4.09x | 1.022 us | 4.071 us | 12,152 B |
| `split/optional-capture` | `SplitStrings` | Historical | Unqualified | 77.009 us | 78.122 us | 0.99x | 79.484 us | 85.441 us | 182,032 B |
| `split/zero-width` | `SplitStrings` | Historical | Unqualified | 11.191 us | 50.568 us | 0.22x | 51.485 us | 303.356 us | 48,656 B |
| `split/unicode` | `SplitStrings` | Historical | Unqualified | 25.038 us | 29.876 us | 0.84x | 32.289 us | 53.040 us | 26,104 B |
| `split/detailed` | `SplitDetailed` | Historical | Unqualified | 58.427 us | 116.567 us | 0.50x | 117.248 us | 92.702 us | 116,752 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | Historical | Unqualified | 11,527.140 us | 300,655.377 us | 0.04x | 310,195.596 us | 8,387.885 us | 12,389,328 B |
| `corpus/code-log-methods` | `Count` | Historical | Unqualified | 44.697 us | 702.853 us | 0.06x | 704.705 us | 39.323 us | 106,728 B |
| `corpus/code-declarations` | `Count` | Historical | Unqualified | 742.765 us | 738.176 us | 1.01x | 742.189 us | 749.590 us | 106,728 B |
| `corpus/sherlock-ru` | `Count` | Historical | Unqualified | 218.150 us | 622.352 us | 0.35x | 4,058.866 us | 5,004.240 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

No lifecycle families have been published yet.

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 7.1% / 9.7% | 1.020 / 1.022 | 1.051 | 9.6 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 2.8% / 3.8% | 1.051 / 1.103 | 1.093 | 9.0 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 15.0% / 6.4% | 1.009 / 1.029 | 1.016 | 9.1 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Reject** | 6.9% / 1.5% | 1.019 / 1.029 | 1.165 | 7.7 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Reject** | 4.0% / 8.9% | 1.097 / 1.036 | 1.072 | 3.5 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 342.3% / 1.3% | 1.104 / 1.041 | 1.032 | 5.5 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `strict UTF-8 decode; .NET Regex; findall string shaping` | **Reject** | 21.2% / 7.2% | 1.324 / 1.036 | 1.300 | 6.3 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000011 us/unit managed and 0.000321 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 2.362 us | 0.167 us | 14.106x [12.863, 14.756] | 0.951 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 2.481 us | 5.517 us | 0.442x [0.437, 0.457] | 0.987 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 2.605 us | 10.717 us | 0.243x [0.238, 0.247] | 1.001 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 3.268 us | 21.240 us | 0.154x [0.152, 0.156] | 0.979 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum spread is 1.051/1.103. Robust slopes are 0.074841 us/unit managed and 0.151422 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 0.299 us | 0.544 us | 0.541x [0.476, 0.590] | 1.093 | 432 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 0.432 us | 0.814 us | 0.534x [0.513, 0.538] | 1.017 | 848 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 0.581 us | 1.125 us | 0.515x [0.502, 0.556] | 0.968 | 1,240 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 0.900 us | 1.739 us | 0.518x [0.487, 0.544] | 1.040 | 2,024 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000043 us/unit managed and 0.000323 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.095 us | 0.104 us | 0.912x [0.900, 0.933] | 1.008 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.108 us | 0.428 us | 0.257x [0.249, 0.257] | 1.016 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.691 us | 5.438 us | 0.127x [0.126, 0.130] | 1.005 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 3.224 us | 21.098 us | 0.153x [0.152, 0.155] | 0.988 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Reject** — maximum symmetric order effect is 1.165. Robust slopes are 0.009651 us/unit managed and 0.071116 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 3.288 us | 21.267 us | 0.155x [0.150, 0.185] | 1.010 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 3.329 us | 22.868 us | 0.146x [0.142, 0.186] | 1.165 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 3.724 us | 26.236 us | 0.139x [0.138, 0.145] | 1.013 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 6.122 us | 39.711 us | 0.154x [0.153, 0.157] | 1.003 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 3.462 ms. Robust slopes are 0.000868 us/unit managed and 0.000023 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.151 us | 0.176 us | 0.855x [0.832, 0.889] | 1.063 | 376 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.341 us | 0.182 us | 1.834x [1.819, 1.896] | 1.013 | 2,368 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 3.737 us | 0.298 us | 12.586x [11.899, 13.226] | 0.933 | 33,088 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 14.356 us | 0.530 us | 27.065x [26.322, 28.081] | 1.023 | 131,392 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 342.3 %/1.3 %; maximum spread is 1.104/1.041. Robust slopes are 246.195200 us/unit managed and -0.008716 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 5.539 us | 6.210 us | 0.880x [0.571, 0.949] | 0.977 | 33,144 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 213.815 us | 6.095 us | 34.985x [34.073, 36.965] | 1.012 | 268,968 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 328.766 us | 6.077 us | 54.042x [51.964, 56.105] | 0.972 | 268,968 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 857.883 us | 6.211 us | 138.902x [135.205, 142.575] | 1.032 | 537,911 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum spread is 1.324/1.036; maximum symmetric order effect is 1.300. Robust slopes are 0.345467 us/unit managed and 0.095581 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 15.718 us | 4.853 us | 3.273x [3.218, 5.021] | 1.126 | 20,904 B |
| 192 results | 192 | 128 B | 192 | 0 B | 58.816 us | 17.914 us | 3.283x [3.197, 3.368] | 1.000 | 83,160 B |
| 768 results | 768 | 512 B | 768 | 0 B | 227.098 us | 72.605 us | 3.154x [3.080, 3.296] | 0.983 | 332,040 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 1,200.040 us | 295.806 us | 4.049x [3.141, 4.272] | 0.769 | 1,327,418 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.14x [0.14, 0.15]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.15, 0.16]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.15, 0.15]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 1.11x [1.08, 1.13]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.55x [0.54, 0.57]; Inconclusive |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 6.65x [6.56, 6.79]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.51x [0.50, 0.51]; ManagedFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.46x [0.44, 0.48]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.23x [0.22, 0.24]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.34x [0.33, 0.36]; ManagedFaster |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.31x [0.31, 0.31]; ManagedFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.27x [0.26, 0.27]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.30x [0.24, 0.30]; NotApplicable |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.42x [0.40, 0.44]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.50x [0.49, 0.50]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.41x [0.40, 0.41]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.42x [0.42, 0.42]; NotApplicable |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.43x [0.42, 0.44]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.33x [0.33, 0.34]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.38x [0.37, 0.40]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiLiteralIgnoreCase; value ranges` | Rbyte 0.51x [0.51, 0.53]; ManagedFaster |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.48x [0.46, 0.50]; ManagedFaster |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.62x [0.62, 0.62]; NotApplicable |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.69x [0.67, 0.70]; ManagedFaster |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.44x [0.42, 0.45]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.30x [0.24, 0.31]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.43x [0.42, 0.46]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.44x [0.42, 0.46]; ManagedFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.41x [0.40, 0.43]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 0.54x [0.53, 0.55]; ManagedFaster |
| `capture/search-detailed` | `_sre C Pattern.search + Python detailed projection` | `Utf8Regex/FallbackRegex; detailed capture projection` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `family/count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactUtf8Literals; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `class-run/count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/FallbackRegex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `unicode/count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactUtf8Literal; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/full-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/full-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `Utf8Regex/FallbackRegex; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-full-strings` | `_sre C Pattern.findall` | `Utf8Regex; exact-literal count; repeated immutable string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-full-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `Utf8Regex/ExactUtf8Literal; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/one-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex ValueMatch enumeration; direct trailing-capture string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/many-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex ValueMatch enumeration; direct separated-capture tuple shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/many-capture-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `strict UTF-8 decode; .NET Regex; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-capture-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `strict UTF-8 decode; .NET Regex; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `iteration/finditer-detailed` | `_sre scanner + Python detailed projection` | `strict UTF-8 decode; .NET Regex; detailed iteration shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `zero-width/count` | `_sre scanner + Python finditer/sum` | `strict UTF-8 decode; adapter ASCII-boundary loop; scalar result` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/fixed-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/fixed-utf8` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/subn-string` | `_sre C Pattern.subn` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/subn-utf8` | `_sre C Pattern.subn` | `Utf8Regex/ExactAsciiLiteral; replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-string` | `_sre C Pattern.subn + Python callback` | `strict UTF-8 decode; .NET Regex callback replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-utf8` | `_sre C Pattern.subn + Python callback` | `strict UTF-8 decode; .NET Regex callback replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/no-captures` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/captures` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/zero` | `_sre scanner + Python finditer/sum` | `Utf8Regex/FallbackRegex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/one` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/empty-progression` | `_sre scanner + Python finditer/sum` | `strict UTF-8 decode; .NET Regex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `count/from-offset` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactAsciiLiteral; Python-style count progression from a nonzero byte offset` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/zero-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/one-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/from-offset-strings` | `_sre C Pattern.findall` | `Utf8Regex/ExactAsciiLiteral; findall string shaping from a nonzero byte offset` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/empty-progression-strings` | `_sre C Pattern.findall` | `strict UTF-8 validation; direct ASCII greedy-star progression; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/optional-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex ValueMatch enumeration; direct optional exact-capture string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/named-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex ValueMatch enumeration; direct trailing-capture string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/structural-many` | `_sre scanner + Python group-zero projection` | `Utf8Regex/FallbackRegex; group-zero structural match shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `finditer/zero-detailed` | `_sre scanner + Python detailed projection` | `strict UTF-8 decode; .NET Regex; detailed iteration shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/no-match-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/one-template-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/named-template-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/unmatched-template-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/limited-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/empty-progression-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; direct .NET Regex greedy exact-repeat replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-replace-string` | `_sre C Pattern.sub + Python callback` | `strict UTF-8 decode; .NET Regex callback replacement; string shaping without count` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/no-separator` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/one-separator` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/limited` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex bounded split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/optional-capture` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/zero-width` | `_sre C Pattern.split` | `strict UTF-8 decode; adapter ASCII-boundary split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/unicode` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/detailed` | `_sre C Pattern.split + Python split-item metadata projection` | `strict UTF-8 decode; .NET Regex split; item-metadata shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical. |
| `common/ip-match` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical. |
| `corpus/mail-email-count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/FallbackRegex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `corpus/code-log-methods` | `_sre scanner + Python finditer/sum` | `Utf8Regex/FallbackRegex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `corpus/code-declarations` | `_sre scanner + Python finditer/sum` | `Utf8Regex/FallbackRegex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
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
