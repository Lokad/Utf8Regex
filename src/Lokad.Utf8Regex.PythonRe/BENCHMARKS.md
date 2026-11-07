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

- Generated: `2026-11-07T20:55:31.4841699+00:00`
- Snapshot SHA-256: `A0BF8F16E55705D5845B1A07839D2A636C0F39E64AA936CA871E48BDF510C673`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `4`
- Scaling families: `7`
- Public Status: `44` managed faster, `2` equivalent, `30` CPython faster, `9` inconclusive, `5` unqualified
- Historical point measurement environments represented: `1`
- Paired evidence from source `d60679252cf6`: `90` rows; .NET 11.0.0-rc.3.26522.148; CPython 3.13.2
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

Historical independent point columns were measured from source `d60679252cf6` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 2.451 us | 22.070 us | 0.11x | 27.380 us | 31.796 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.600 us | 22.280 us | 0.12x | 27.304 us | 24.869 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.632 us | 21.978 us | 0.07x | 27.727 us | 21.714 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.313 us | 0.268 us | 1.16x | 1.740 us | 2.526 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.068 us | 0.343 us | 0.20x | 0.425 us | 0.145 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 18.292 us | 19.485 us | 0.94x | 28.483 us | 27.394 us | 0 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.665 us | 0.232 us | 2.87x | 5.762 us | 12.649 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.251 us | 0.182 us | 1.38x | 0.324 us | 0.079 us | 0 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.307 us | 0.351 us | 0.88x | 0.387 us | 0.171 us | 0 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.208 us | 0.284 us | 0.73x | 0.379 us | 0.052 us | 0 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.250 us | 0.330 us | 0.76x | 0.365 us | 0.114 us | 0 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.272 us | 0.262 us | 1.04x | 0.354 us | 0.085 us | 0 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.143 us | 0.330 us | 0.43x | 0.757 us | 70.555 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.464 us | 0.293 us | 1.60x | 0.374 us | 0.148 us | 0 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.316 us | 0.297 us | 1.06x | 0.397 us | 0.173 us | 0 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.262 us | 0.301 us | 0.87x | 0.340 us | 0.122 us | 0 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.182 us | 0.294 us | 0.62x | 0.325 us | 0.125 us | 0 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.307 us | 0.218 us | 1.41x | 0.312 us | 0.084 us | 0 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.375 us | 0.278 us | 1.35x | 0.373 us | 0.155 us | 64 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.426 us | 0.317 us | 1.34x | 0.351 us | 0.129 us | 528 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.120 us | 0.209 us | 0.57x | 0.313 us | 0.074 us | 0 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.451 us | 0.346 us | 1.31x | 0.382 us | 0.120 us | 0 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.320 us | 0.287 us | 1.12x | 0.318 us | 0.134 us | 0 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.087 us | 0.256 us | 0.34x | 0.410 us | 0.104 us | 0 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Unqualified | 0.201 us | 0.234 us | 0.85x | 0.271 us | 0.104 us | 0 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.350 us | 0.197 us | 1.78x | 0.342 us | 0.123 us | 64 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.199 us | 0.194 us | 1.03x | 0.287 us | 0.096 us | 0 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.310 us | 0.332 us | 0.94x | 0.360 us | 0.125 us | 0 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.456 us | 0.258 us | 1.75x | 0.364 us | 0.110 us | 528 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.455 us | 0.304 us | 1.49x | 0.345 us | 0.118 us | 528 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.588 us | 0.331 us | 1.78x | 0.364 us | 0.150 us | 0 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.135 us | 0.235 us | 0.58x | 0.333 us | 0.124 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.141 us | 0.289 us | 0.49x | 1.176 us | 0.884 us | 0 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.171 us | 0.214 us | 0.80x | 0.324 us | 0.047 us | 0 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.131 us | 0.217 us | 0.60x | 0.320 us | 0.075 us | 0 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.205 us | 0.231 us | 0.88x | 0.341 us | 0.109 us | 0 B |
| `common/date-miss` | `IsMatch` | ScalarResult | CPython faster | 1.777 us | 0.852 us | 2.09x | 0.993 us | 2.186 us | 0 B |
| `common/ip-match` | `IsMatch` | ScalarResult | Managed faster | 0.092 us | 0.314 us | 0.29x | 0.480 us | 0.267 us | 0 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | Managed faster | 0.848 us | 1.300 us | 0.66x | 1.543 us | 0.308 us | 496 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 638.363 us | 2,423.680 us | 0.26x | 2,189.307 us | 791.071 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 281.752 us | 2,547.554 us | 0.11x | 2,375.466 us | 726.574 us | 0 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 57.080 us | 430.048 us | 0.13x | 522.584 us | 307.204 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 518.264 us | 366.596 us | 1.41x | 361.001 us | 413.008 us | 184,440 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Equivalent | 517.200 us | 513.839 us | 1.01x | 496.563 us | 516.323 us | 184,440 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 37.213 us | 27.418 us | 1.37x | 40.539 us | 68.666 us | 4,160 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | CPython faster | 99.568 us | 61.174 us | 1.63x | 75.030 us | 79.540 us | 32,928 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 469.980 us | 211.277 us | 2.23x | 218.475 us | 364.735 us | 94,344 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 901.988 us | 343.761 us | 2.63x | 374.024 us | 573.687 us | 242,008 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 960.568 us | 1,252.363 us | 0.76x | 1,173.183 us | 736.977 us | 242,008 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 531.587 us | 616.665 us | 0.87x | 615.019 us | 356.253 us | 119,632 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 303.190 us | 538.697 us | 0.57x | 518.257 us | 147.862 us | 127,272 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 583.431 us | 1,102.484 us | 0.53x | 1,104.472 us | 1,780.757 us | 0 B |
| `count/zero` | `Count` | ScalarResult | Managed faster | 0.489 us | 90.141 us | 0.01x | 90.104 us | 1.107 us | 0 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.272 us | 1.988 us | 0.14x | 2.619 us | 0.683 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | Managed faster | 168.278 us | 374.616 us | 0.45x | 343.964 us | 68.792 us | 0 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Managed faster | 13.905 us | 49.125 us | 0.28x | 48.397 us | 21.150 us | 0 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.527 us | 88.926 us | 0.01x | 90.416 us | 1.168 us | 0 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.734 us | 38.126 us | 0.02x | 38.991 us | 0.749 us | 64 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | EagerMaterializedResult | CPython faster | 16.811 us | 15.851 us | 1.06x | 16.630 us | 51.353 us | 4,152 B |
| `findall/empty-progression-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 64.384 us | 42.779 us | 1.50x | 45.811 us | 47.080 us | 16,464 B |
| `findall/optional-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Inconclusive | 56.304 us | 55.463 us | 1.01x | 47.979 us | 98.570 us | 4,208 B |
| `findall/named-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 162.645 us | 94.346 us | 1.74x | 100.673 us | 138.429 us | 43,144 B |
| `findall/structural-many` | `FindAllStructural` | EagerMaterializedResult | Managed faster | 192.107 us | 201.842 us | 0.95x | 189.741 us | 102.688 us | 37,160 B |
| `finditer/zero-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 0.251 us | 140.681 us | 0.00x | 139.206 us | 0.686 us | 0 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 184.423 us | 173.669 us | 1.06x | 179.614 us | 213.997 us | 122,928 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | Equivalent | 173.808 us | 175.676 us | 0.99x | 175.110 us | 217.454 us | 40,984 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | Inconclusive | 92.251 us | 90.279 us | 1.03x | 89.879 us | 449.507 us | 61,488 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | Managed faster | 87.515 us | 91.036 us | 0.96x | 91.507 us | 449.912 us | 20,504 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Managed faster | 585.319 us | 16,601.309 us | 0.04x | 16,882.638 us | 317.822 us | 235,520 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Managed faster | 595.972 us | 17,178.526 us | 0.03x | 16,591.169 us | 322.429 us | 226,304 B |
| `replacement/no-match-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 1.182 us | 2.364 us | 0.50x | 3.243 us | 1.181 us | 13,336 B |
| `replacement/one-template-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 1.818 us | 19,472.275 us | 0.00x | 15,766.646 us | 0.876 us | 10,896 B |
| `replacement/named-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 1.239 us | 0.900 us | 1.38x | 1.203 us | 0.507 us | 416 B |
| `replacement/unmatched-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 47.447 us | 50.555 us | 0.94x | 46.400 us | 31.300 us | 3,744 B |
| `replacement/limited-string` | `ReplaceStringLimited` | EagerMaterializedResult | Inconclusive | 1.048 us | 0.452 us | 2.31x | 1.195 us | 1.032 us | 12,360 B |
| `replacement/empty-progression-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 34.930 us | 23.331 us | 1.49x | 23.383 us | 11.169 us | 2,912 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | EagerMaterializedResult | Managed faster | 149.131 us | 4,197.089 us | 0.04x | 4,232.120 us | 80.837 us | 58,832 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 279.435 us | 222.240 us | 1.27x | 221.196 us | 235.460 us | 143,488 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 430.681 us | 254.069 us | 1.70x | 253.880 us | 771.081 us | 274,648 B |
| `split/no-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.154 us | 24.694 us | 0.05x | 25.223 us | 0.701 us | 8,760 B |
| `split/one-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.122 us | 11.763 us | 0.09x | 12.261 us | 0.754 us | 4,192 B |
| `split/limited` | `SplitStringsLimited` | EagerMaterializedResult | Inconclusive | 1.407 us | 0.542 us | 2.56x | 1.246 us | 1.178 us | 6,032 B |
| `split/optional-capture` | `SplitStrings` | EagerMaterializedResult | Managed faster | 74.328 us | 81.520 us | 0.91x | 81.116 us | 82.620 us | 33,936 B |
| `split/zero-width` | `SplitStrings` | EagerMaterializedResult | CPython faster | 114.799 us | 66.643 us | 1.73x | 76.740 us | 292.852 us | 40,016 B |
| `split/unicode` | `SplitStrings` | EagerMaterializedResult | CPython faster | 70.198 us | 43.611 us | 1.61x | 42.294 us | 50.596 us | 19,512 B |
| `split/detailed` | `SplitDetailed` | EagerMaterializedResult | Managed faster | 108.122 us | 134.090 us | 0.81x | 134.519 us | 87.921 us | 102,576 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | ScalarResult | Managed faster | 2,062.812 us | 351,877.392 us | 0.01x | 361,284.996 us | 8,399.483 us | 1,544 B |
| `corpus/code-log-methods` | `Count` | ScalarResult | Managed faster | 49.074 us | 573.472 us | 0.09x | 568.616 us | 50.541 us | 528 B |
| `corpus/code-declarations` | `Count` | ScalarResult | Managed faster | 79.964 us | 615.230 us | 0.13x | 616.897 us | 769.085 us | 528 B |
| `corpus/sherlock-ru` | `Count` | ScalarResult | Managed faster | 187.996 us | 391.899 us | 0.48x | 4,101.975 us | 5,130.280 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `captured` | `(?P<word>[A-Za-z]+)-(?P=word)` | 11 B | 15.381 us / 4,897 B | 133.912 us / 53,625 B | 195.981 us / 71,617 B | 184.703 us / 71,641 B | 59.864 us | 58.491 us |
| `fallback` | `header:[0-9]+` | 12 B | 49.122 us / 3,425 B | 221.684 us / 61,689 B | 297.047 us / 83,561 B | 266.384 us / 83,529 B | 41.343 us | 42.232 us |
| `literal` | `needle` | 20 B | 17.456 us / 2,481 B | 146.850 us / 40,313 B | 238.912 us / 50,369 B | 312.616 us / 50,337 B | 25.197 us | 25.366 us |
| `unicode` | `Шерлок Холмс` | 37 B | 5.922 us / 2,905 B | 34.884 us / 33,697 B | 55.487 us / 48,313 B | 59.897 us / 48,281 B | 36.357 us | 35.139 us |

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Reject** | 4.4 % / 53.0 % | 1.022 / 1.049 | 1.059 | 9.5 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 88.2 % / 0.7 % | 1.891 / 1.039 | 1.472 | 4.8 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 22.7 % / 2.3 % | 1.027 / 1.045 | 1.070 | 9.0 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Reject** | 7.3 % / 1.5 % | 1.026 / 1.044 | 1.033 | 3.0 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Pass** | 2.3 % / 6.7 % | 1.053 / 1.027 | 1.032 | 9.3 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 872.9 % / 21.6 % | 1.037 / 1.030 | 1.022 | 8.7 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `Utf8Regex/OwnedScalar; findall string shaping` | **Reject** | 2.0 % / 0.7 % | 1.019 / 1.017 | 1.015 | 3.8 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 4.4 %/53.0 %. Robust slopes are 0.000029 us/unit managed and 0.000340 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 0.676 us | 0.248 us | 2.707x [2.618, 2.828] | 1.048 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 1.088 us | 6.105 us | 0.178x [0.174, 0.186] | 0.944 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 1.541 us | 11.543 us | 0.134x [0.126, 0.141] | 0.975 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 2.634 us | 22.626 us | 0.116x [0.111, 0.118] | 0.987 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 88.2 %/0.7 %; maximum spread is 1.891/1.039; maximum symmetric order effect is 1.472; minimum lane duration is 4.785 ms. Robust slopes are -0.139436 us/unit managed and 0.179749 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 1.771 us | 0.572 us | 3.082x [2.977, 3.154] | 0.995 | 96 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 2.019 us | 0.934 us | 2.080x [1.232, 3.618] | 1.472 | 480 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 0.725 us | 1.282 us | 0.563x [0.554, 0.566] | 0.981 | 608 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 0.957 us | 2.011 us | 0.476x [0.465, 0.492] | 0.956 | 864 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000020 us/unit managed and 0.000338 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.060 us | 0.145 us | 0.414x [0.408, 0.433] | 0.965 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.068 us | 0.482 us | 0.142x [0.140, 0.146] | 1.012 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.312 us | 5.580 us | 0.056x [0.053, 0.057] | 1.001 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 1.655 us | 22.251 us | 0.075x [0.071, 0.077] | 1.070 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 3.032 ms. Robust slopes are 0.009424 us/unit managed and 0.117541 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 1.794 us | 23.207 us | 0.077x [0.074, 0.104] | 0.968 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 1.924 us | 24.580 us | 0.078x [0.077, 0.079] | 1.002 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 2.248 us | 30.267 us | 0.074x [0.074, 0.075] | 1.011 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 4.527 us | 53.057 us | 0.085x [0.083, 0.089] | 1.002 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.001147 us/unit managed and 0.000022 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.221 us | 0.222 us | 1.004x [0.985, 1.033] | 0.969 | 128 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.493 us | 0.222 us | 2.229x [2.144, 2.272] | 0.991 | 848 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 5.016 us | 0.335 us | 14.960x [14.484, 15.834] | 1.000 | 12,368 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 18.996 us | 0.587 us | 32.392x [31.705, 34.240] | 1.018 | 49,232 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 872.9 %/21.6 %. Robust slopes are 91.810554 us/unit managed and -0.705716 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 0.872 us | 10.576 us | 0.083x [0.081, 0.084] | 1.001 | 232 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 118.064 us | 6.395 us | 18.101x [17.397, 18.755] | 1.010 | 67,904 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 128.222 us | 6.549 us | 19.504x [19.297, 20.407] | 0.984 | 67,904 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 291.532 us | 6.497 us | 45.490x [44.013, 48.940] | 1.022 | 135,488 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 3.771 ms. Robust slopes are 0.177753 us/unit managed and 0.088942 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 9.701 us | 4.508 us | 2.130x [2.066, 2.205] | 1.015 | 1,320 B |
| 192 results | 192 | 128 B | 192 | 0 B | 36.182 us | 17.365 us | 2.103x [2.027, 2.107] | 0.993 | 3,624 B |
| 768 results | 768 | 512 B | 768 | 0 B | 137.877 us | 69.080 us | 1.996x [1.957, 2.055] | 1.005 | 25,176 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 547.337 us | 272.419 us | 2.031x [1.921, 2.040] | 1.010 | 111,240 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.11x [0.11, 0.11]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.12x [0.12, 0.12]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.07x [0.07, 0.07]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 0.99x [0.97, 1.00]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 0.17x [0.17, 0.18]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 2.76x [2.73, 2.77]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.18x [1.18, 1.19]; CpythonFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.84x [0.84, 0.85]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.78x [0.77, 0.80]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.70x [0.69, 0.71]; ManagedFaster |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.98x [0.97, 0.99]; Unqualified |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.38x [0.38, 0.39]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.33x [1.32, 1.35]; CpythonFaster |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.80x [0.79, 0.80]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.57x [0.57, 0.58]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.31x [1.30, 1.33]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.12x [1.11, 1.15]; CpythonFaster |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.24x [1.22, 1.25]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.53x [0.53, 0.54]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.22x [1.21, 1.23]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.01x [1.00, 1.02]; NotApplicable |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.81x [0.80, 0.81]; Unqualified |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.49x [1.46, 1.60]; CpythonFaster |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.95x [0.94, 0.95]; Unqualified |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.87x [0.86, 0.87]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.66x [1.62, 1.69]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.37x [1.33, 1.38]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.63x [1.61, 1.64]; CpythonFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.57x [0.56, 0.57]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 0.84x [0.83, 0.84]; Unqualified |
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
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; boolean result` | Rbyte 2.13x [2.13, 2.15]; CpythonFaster |
| `common/ip-match` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Rbyte 0.29x [0.28, 0.29]; NotApplicable |
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
