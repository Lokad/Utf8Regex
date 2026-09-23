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

- Generated: `2026-09-23T00:20:04.4815794+00:00`
- Snapshot SHA-256: `D9762BFF0B218413A086D44CC386B33A3816E82E118E75D476B039314981B5A5`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `0`
- Scaling families: `7`
- Public Status: `27` managed faster, `0` equivalent, `2` CPython faster, `4` inconclusive, `57` unqualified
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

Historical independent point columns were measured from source `434eb20d0007` on .NET 10.0.12, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 1.846 us | 12.125 us | 0.15x | 26.189 us | 22.959 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.852 us | 12.221 us | 0.15x | 26.580 us | 15.198 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.109 us | 13.516 us | 0.15x | 26.179 us | 14.881 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | Inconclusive | 0.238 us | 0.189 us | 1.26x | 1.702 us | 2.158 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.119 us | 0.206 us | 0.58x | 0.359 us | 0.180 us | 360 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 12.917 us | 14.127 us | 0.91x | 23.252 us | 27.376 us | 14,360 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 1.022 us | 0.145 us | 6.99x | 5.296 us | 15.387 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.075 us | 0.105 us | 0.71x | 0.264 us | 0.087 us | 40 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.140 us | 0.228 us | 0.60x | 0.352 us | 0.190 us | 280 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.035 us | 0.173 us | 0.20x | 0.346 us | 0.059 us | 64 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.088 us | 0.218 us | 0.40x | 0.294 us | 0.133 us | 272 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.073 us | 0.223 us | 0.33x | 0.300 us | 0.085 us | 64 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.068 us | 0.205 us | 0.33x | 0.669 us | 69.914 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.070 us | 0.184 us | 0.38x | 0.316 us | 0.157 us | 48 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.126 us | 0.205 us | 0.62x | 0.326 us | 0.168 us | 312 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.074 us | 0.195 us | 0.38x | 0.270 us | 0.142 us | 248 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.090 us | 0.181 us | 0.50x | 0.267 us | 0.140 us | 256 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.050 us | 0.126 us | 0.40x | 0.263 us | 0.086 us | 40 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.087 us | 0.164 us | 0.52x | 0.303 us | 0.182 us | 48 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.105 us | 0.208 us | 0.51x | 0.290 us | 0.148 us | 248 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.069 us | 0.131 us | 0.53x | 0.257 us | 0.082 us | 32 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.088 us | 0.217 us | 0.41x | 0.299 us | 0.137 us | 248 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.096 us | 0.179 us | 0.54x | 0.265 us | 0.153 us | 248 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.051 us | 0.152 us | 0.34x | 0.337 us | 0.111 us | 32 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Inconclusive | 0.108 us | 0.161 us | 0.68x | 0.208 us | 0.121 us | 264 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 0.072 us | 0.115 us | 0.65x | 0.291 us | 0.129 us | 48 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.085 us | 0.114 us | 0.74x | 0.232 us | 0.099 us | 48 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.098 us | 0.198 us | 0.49x | 0.300 us | 0.147 us | 296 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.087 us | 0.186 us | 0.47x | 0.304 us | 0.111 us | 40 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.075 us | 0.193 us | 0.38x | 0.284 us | 0.138 us | 248 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.106 us | 0.220 us | 0.48x | 0.302 us | 0.166 us | 272 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.118 us | 0.143 us | 0.82x | 0.259 us | 0.125 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.525 us | 0.170 us | 3.07x | 1.045 us | 1.007 us | 0 B |
| `unicode-category/search-miss` | `Search` | Historical | Unqualified | 0.086 us | 0.140 us | 0.61x | 0.266 us | 0.048 us | 32 B |
| `greedy/search-miss` | `Search` | Historical | Unqualified | 0.111 us | 0.134 us | 0.83x | 0.253 us | 0.079 us | 32 B |
| `alternation/search-miss` | `Search` | Historical | Unqualified | 0.146 us | 0.151 us | 0.97x | 0.271 us | 0.116 us | 48 B |
| `common/date-miss` | `IsMatch` | Historical | Unqualified | 2.233 us | 1.073 us | 2.08x | 1.193 us | 2.173 us | 120 B |
| `common/ip-match` | `IsMatch` | Historical | Unqualified | 0.348 us | 0.256 us | 1.36x | 0.391 us | 0.256 us | 264 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | Historical | Unqualified | 1.127 us | 1.137 us | 0.99x | 1.349 us | 0.373 us | 896 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | Historical | Unqualified | 151.531 us | 1,484.872 us | 0.10x | 1,477.526 us | 769.633 us | 0 B |
| `class-run/count` | `Count` | Historical | Unqualified | 718.450 us | 1,773.339 us | 0.41x | 1,764.111 us | 713.278 us | 172,056 B |
| `unicode/count` | `Count` | Historical | Unqualified | 57.086 us | 270.819 us | 0.21x | 361.726 us | 311.068 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | Historical | Unqualified | 309.231 us | 270.303 us | 1.14x | 270.646 us | 409.187 us | 165,936 B |
| `findall/full-utf8` | `FindAllUtf8` | Historical | Unqualified | 302.084 us | 398.769 us | 0.76x | 400.891 us | 455.955 us | 165,936 B |
| `findall/unicode-full-strings` | `FindAllStrings` | Historical | Unqualified | 23.120 us | 22.499 us | 1.03x | 33.976 us | 77.910 us | 4,120 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | Historical | Unqualified | 51.679 us | 49.991 us | 1.03x | 61.660 us | 88.179 us | 24,600 B |
| `findall/one-capture-strings` | `FindAllStrings` | Historical | Unqualified | 198.356 us | 183.140 us | 1.08x | 180.828 us | 394.660 us | 149,792 B |
| `findall/many-capture-strings` | `FindAllStrings` | Historical | Unqualified | 326.647 us | 316.607 us | 1.03x | 313.314 us | 633.374 us | 299,296 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | Historical | Unqualified | 640.436 us | 1,054.008 us | 0.61x | 1,032.806 us | 729.040 us | 1,528,096 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | Historical | Unqualified | 339.892 us | 570.509 us | 0.60x | 570.488 us | 385.667 us | 785,704 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | Historical | Unqualified | 163.155 us | 461.709 us | 0.35x | 458.797 us | 161.015 us | 415,976 B |
| `zero-width/count` | `Count` | Historical | Unqualified | 28.095 us | 721.830 us | 0.04x | 723.352 us | 1,840.421 us | 34,840 B |
| `count/zero` | `Count` | Historical | Unqualified | 1.554 us | 76.646 us | 0.02x | 76.764 us | 1.141 us | 17,432 B |
| `count/one` | `Count` | Historical | Unqualified | 0.247 us | 1.796 us | 0.14x | 2.566 us | 0.762 us | 0 B |
| `count/empty-progression` | `Count` | Historical | Unqualified | 316.540 us | 256.612 us | 1.23x | 256.644 us | 67.664 us | 430,312 B |
| `count/from-offset` | `CountFromOffset` | Historical | Unqualified | 54.030 us | 31.245 us | 1.73x | 30.806 us | 21.236 us | 8,216 B |
| `findall/zero-strings` | `FindAllStrings` | Historical | Unqualified | 3.122 us | 76.414 us | 0.04x | 76.772 us | 1.184 us | 17,456 B |
| `findall/one-strings` | `FindAllStrings` | Historical | Unqualified | 2.236 us | 31.976 us | 0.07x | 32.300 us | 0.794 us | 8,288 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | Historical | Unqualified | 7.929 us | 14.018 us | 0.57x | 14.656 us | 54.568 us | 4,120 B |
| `findall/empty-progression-strings` | `FindAllStrings` | Historical | Unqualified | 1.755 us | 32.832 us | 0.05x | 33.704 us | 47.801 us | 4,128 B |
| `findall/optional-capture-strings` | `FindAllStrings` | Historical | Unqualified | 53.370 us | 45.383 us | 1.18x | 45.134 us | 102.840 us | 15,088 B |
| `findall/named-capture-strings` | `FindAllStrings` | Historical | Unqualified | 79.338 us | 76.127 us | 1.04x | 76.943 us | 146.238 us | 58,120 B |
| `findall/structural-many` | `FindAllStructural` | Historical | Unqualified | 86.423 us | 154.058 us | 0.56x | 153.854 us | 109.200 us | 46,640 B |
| `finditer/zero-detailed` | `FindIterDetailed` | Historical | Unqualified | 1.109 us | 152.800 us | 0.01x | 152.137 us | 0.637 us | 17,456 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | Historical | Unqualified | 110.607 us | 167.771 us | 0.66x | 167.705 us | 241.375 us | 123,144 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | Historical | Unqualified | 103.415 us | 170.518 us | 0.61x | 171.181 us | 245.928 us | 41,200 B |
| `replacement/subn-string` | `SubnString` | Historical | Unqualified | 55.662 us | 85.115 us | 0.65x | 86.880 us | 452.826 us | 61,704 B |
| `replacement/subn-utf8` | `SubnUtf8` | Historical | Unqualified | 51.295 us | 86.425 us | 0.59x | 87.374 us | 462.741 us | 20,720 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | Historical | Unqualified | 369.435 us | 14,805.288 us | 0.02x | 14,806.232 us | 335.632 us | 878,872 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | Historical | Unqualified | 374.500 us | 14,747.218 us | 0.03x | 14,645.466 us | 342.552 us | 875,768 B |
| `replacement/no-match-string` | `ReplaceString` | Historical | Unqualified | 1.512 us | 2.243 us | 0.67x | 3.058 us | 7.513 us | 20,232 B |
| `replacement/one-template-string` | `ReplaceString` | Historical | Unqualified | 1.062 us | 17,339.012 us | 0.00x | 17,212.734 us | 2.982 us | 8,496 B |
| `replacement/named-template-string` | `ReplaceString` | Historical | Unqualified | 0.705 us | 0.768 us | 0.92x | 1.037 us | 0.615 us | 360 B |
| `replacement/unmatched-template-string` | `ReplaceString` | Historical | Unqualified | 32.700 us | 42.178 us | 0.78x | 42.461 us | 39.854 us | 3,344 B |
| `replacement/limited-string` | `ReplaceStringLimited` | Historical | Unqualified | 1.014 us | 0.393 us | 2.58x | 1.044 us | 5.308 us | 12,576 B |
| `replacement/empty-progression-string` | `ReplaceString` | Historical | Unqualified | 11.378 us | 19.589 us | 0.58x | 19.918 us | 11.829 us | 1,792 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | Historical | Unqualified | 95.306 us | 3,674.679 us | 0.03x | 3,685.979 us | 85.846 us | 219,928 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | Historical | Unqualified | 207.461 us | 169.649 us | 1.22x | 170.070 us | 244.600 us | 269,640 B |
| `split/captures` | `SplitStrings` | Historical | Unqualified | 321.522 us | 198.796 us | 1.62x | 198.820 us | 787.500 us | 499,082 B |
| `split/no-separator` | `SplitStrings` | Historical | Unqualified | 1.717 us | 24.903 us | 0.07x | 24.139 us | 5.103 us | 8,920 B |
| `split/one-separator` | `SplitStrings` | Historical | Unqualified | 1.487 us | 11.635 us | 0.13x | 11.292 us | 2.912 us | 8,496 B |
| `split/limited` | `SplitStringsLimited` | Historical | Unqualified | 1.795 us | 0.433 us | 4.14x | 1.035 us | 4.123 us | 12,152 B |
| `split/optional-capture` | `SplitStrings` | Historical | Unqualified | 81.467 us | 78.331 us | 1.04x | 79.448 us | 88.821 us | 182,032 B |
| `split/zero-width` | `SplitStrings` | Historical | Unqualified | 12.142 us | 51.149 us | 0.24x | 51.780 us | 309.861 us | 48,656 B |
| `split/unicode` | `SplitStrings` | Historical | Unqualified | 24.958 us | 30.147 us | 0.83x | 32.443 us | 53.266 us | 26,104 B |
| `split/detailed` | `SplitDetailed` | Historical | Unqualified | 63.565 us | 117.574 us | 0.54x | 117.792 us | 94.755 us | 116,752 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | Historical | Unqualified | 11,183.784 us | 301,910.206 us | 0.04x | 311,991.011 us | 8,412.575 us | 12,389,328 B |
| `corpus/code-log-methods` | `Count` | Historical | Unqualified | 67.041 us | 710.456 us | 0.09x | 706.507 us | 67.662 us | 106,728 B |
| `corpus/code-declarations` | `Count` | Historical | Unqualified | 776.594 us | 738.481 us | 1.05x | 744.238 us | 770.800 us | 106,728 B |
| `corpus/sherlock-ru` | `Count` | Historical | Unqualified | 219.375 us | 621.720 us | 0.35x | 4,066.119 us | 5,240.930 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

No lifecycle families have been published yet.

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Reject** | 10.7% / 46.8% | 1.031 / 1.119 | 1.140 | 9.6 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Pass** | 2.1% / 3.0% | 1.039 / 1.029 | 1.030 | 9.5 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 14.3% / 1.5% | 1.028 / 1.017 | 1.030 | 9.5 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Reject** | 7.4% / 1.7% | 1.024 / 1.031 | 1.175 | 7.7 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Pass** | 10.3% / 9.1% | 1.090 / 1.029 | 1.048 | 9.2 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 267.7% / 1.1% | 1.073 / 1.041 | 1.039 | 8.6 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `strict UTF-8 decode; .NET Regex; findall string shaping` | **Reject** | 1.3% / 5.5% | 1.391 / 1.026 | 1.557 | 6.3 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 10.7 %/46.8 %; maximum spread is 1.031/1.119; maximum symmetric order effect is 1.140. Robust slopes are 0.000017 us/unit managed and 0.000337 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 2.405 us | 0.174 us | 13.722x [12.016, 14.262] | 0.877 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 2.220 us | 5.863 us | 0.387x [0.369, 0.401] | 0.931 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 2.663 us | 11.479 us | 0.233x [0.219, 0.239] | 0.986 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 3.332 us | 21.911 us | 0.152x [0.151, 0.155] | 1.001 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.079333 us/unit managed and 0.150804 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 0.291 us | 0.541 us | 0.539x [0.524, 0.549] | 0.984 | 432 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 0.459 us | 0.804 us | 0.569x [0.554, 0.591] | 1.025 | 848 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 0.598 us | 1.125 us | 0.537x [0.527, 0.557] | 1.022 | 1,240 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 0.931 us | 1.734 us | 0.551x [0.523, 0.562] | 1.030 | 2,024 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000044 us/unit managed and 0.000334 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.093 us | 0.107 us | 0.861x [0.848, 0.873] | 1.000 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.109 us | 0.428 us | 0.257x [0.249, 0.260] | 0.986 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.704 us | 5.482 us | 0.128x [0.122, 0.137] | 0.970 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 3.348 us | 22.000 us | 0.153x [0.150, 0.156] | 0.976 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Reject** — maximum symmetric order effect is 1.175. Robust slopes are 0.012202 us/unit managed and 0.071544 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 3.333 us | 21.849 us | 0.153x [0.149, 0.186] | 1.044 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 3.133 us | 23.478 us | 0.132x [0.130, 0.171] | 1.175 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 3.800 us | 26.537 us | 0.141x [0.139, 0.147] | 0.984 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 6.277 us | 40.227 us | 0.156x [0.154, 0.161] | 0.989 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.001066 us/unit managed and 0.000023 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.158 us | 0.183 us | 0.870x [0.803, 0.885] | 0.954 | 376 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.381 us | 0.188 us | 2.019x [1.986, 2.034] | 0.985 | 2,368 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 4.113 us | 0.304 us | 13.675x [13.176, 16.632] | 0.986 | 33,088 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 19.151 us | 0.543 us | 36.096x [32.905, 36.668] | 0.973 | 131,392 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 267.7 %/1.1 %. Robust slopes are 258.072554 us/unit managed and 0.030540 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 6.210 us | 6.225 us | 0.990x [0.919, 1.137] | 0.986 | 33,144 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 231.039 us | 6.346 us | 36.049x [35.804, 36.906] | 1.017 | 268,968 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 341.476 us | 6.313 us | 53.662x [52.014, 56.263] | 0.972 | 268,968 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 880.158 us | 6.337 us | 138.757x [130.553, 140.351] | 0.963 | 537,906 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum spread is 1.391/1.026; maximum symmetric order effect is 1.557. Robust slopes are 0.307307 us/unit managed and 0.097199 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 16.008 us | 4.816 us | 3.327x [3.302, 5.067] | 1.144 | 20,904 B |
| 192 results | 192 | 128 B | 192 | 0 B | 59.774 us | 18.366 us | 3.264x [3.189, 3.371] | 0.991 | 83,160 B |
| 768 results | 768 | 512 B | 768 | 0 B | 240.333 us | 74.462 us | 3.190x [3.149, 3.353] | 1.033 | 332,040 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 945.056 us | 298.553 us | 3.220x [3.131, 5.548] | 0.642 | 1,327,416 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.15x [0.15, 0.15]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.11, 0.15]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.14, 0.16]; Inconclusive |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 1.08x [1.05, 1.11]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.52x [0.51, 0.53]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 6.45x [5.95, 6.56]; Inconclusive |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.53x [0.53, 0.54]; ManagedFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.57x [0.56, 0.59]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.17x [0.16, 0.17]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.36x [0.35, 0.39]; ManagedFaster |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.31x [0.31, 0.32]; ManagedFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.30x [0.29, 0.30]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.33x [0.33, 0.33]; NotApplicable |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.36x [0.35, 0.37]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.47x [0.46, 0.48]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.36x [0.35, 0.42]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.44x [0.43, 0.45]; NotApplicable |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.48x [0.46, 0.54]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.47x [0.46, 0.49]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.38x [0.38, 0.39]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiLiteralIgnoreCase; value ranges` | Rbyte 0.50x [0.50, 0.51]; ManagedFaster |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.59x [0.59, 0.60]; Inconclusive |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.48x [0.47, 0.51]; NotApplicable |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.67x [0.67, 0.68]; ManagedFaster |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.46x [0.44, 0.46]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.42x [0.41, 0.42]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.36x [0.35, 0.43]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.44x [0.42, 0.44]; ManagedFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical. |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical. |
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
