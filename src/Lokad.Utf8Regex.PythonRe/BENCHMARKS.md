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

- Generated: `2026-11-06T15:19:51.6574154+00:00`
- Snapshot SHA-256: `EF07D729501ABB539C356CAE523395A729155E205A2E533E2852DE426B70645D`
- Schema: `10`
- Catalog SHA-256: `44F1F6CBA90CD0835980E6D9269CBD95BCF05E4A4043C65C3E71B8681B579666`
- Cases: `90`
- Lifecycle families: `4`
- Scaling families: `7`
- Public Status: `39` managed faster, `2` equivalent, `39` CPython faster, `5` inconclusive, `5` unqualified
- Historical point measurement environments represented: `1`
- Paired evidence from source `f105c683013f`: `90` rows; .NET 11.0.0-rc.3.26522.148; CPython 3.13.2
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

Historical independent point columns were measured from source `f105c683013f` on .NET 11.0.0-rc.3.26522.148, Ubuntu 24.04.4 LTS, X64. Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 2.499 us | 22.405 us | 0.11x | 28.117 us | 35.214 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 2.884 us | 22.655 us | 0.13x | 28.153 us | 28.085 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.664 us | 22.290 us | 0.07x | 28.052 us | 27.898 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.319 us | 0.274 us | 1.17x | 1.794 us | 2.573 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.074 us | 0.348 us | 0.21x | 0.432 us | 0.149 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 17.550 us | 19.621 us | 0.89x | 29.170 us | 27.461 us | 0 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.686 us | 0.234 us | 2.93x | 5.913 us | 27.749 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.285 us | 0.184 us | 1.55x | 0.333 us | 0.086 us | 0 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.387 us | 0.357 us | 1.09x | 0.398 us | 0.183 us | 0 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.429 us | 0.288 us | 1.48x | 0.386 us | 0.053 us | 0 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.301 us | 0.337 us | 0.90x | 0.369 us | 0.118 us | 0 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.317 us | 0.267 us | 1.19x | 0.363 us | 0.087 us | 0 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.145 us | 0.334 us | 0.44x | 0.779 us | 71.869 us | 0 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.680 us | 0.294 us | 2.32x | 0.385 us | 0.163 us | 0 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.349 us | 0.300 us | 1.17x | 0.404 us | 0.179 us | 0 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.296 us | 0.306 us | 0.97x | 0.359 us | 0.133 us | 0 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.245 us | 0.301 us | 0.81x | 0.333 us | 0.127 us | 0 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.393 us | 0.221 us | 1.78x | 0.319 us | 0.086 us | 0 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.509 us | 0.282 us | 1.80x | 0.379 us | 0.168 us | 272 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.500 us | 0.324 us | 1.55x | 0.359 us | 0.137 us | 528 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.169 us | 0.212 us | 0.79x | 0.319 us | 0.078 us | 0 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.572 us | 0.351 us | 1.63x | 0.385 us | 0.128 us | 0 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.402 us | 0.291 us | 1.38x | 0.328 us | 0.138 us | 0 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.088 us | 0.257 us | 0.34x | 0.417 us | 0.111 us | 0 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Unqualified | 0.234 us | 0.239 us | 0.98x | 0.277 us | 0.111 us | 0 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.477 us | 0.199 us | 2.40x | 0.352 us | 0.130 us | 272 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.244 us | 0.198 us | 1.24x | 0.291 us | 0.098 us | 0 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.428 us | 0.334 us | 1.28x | 0.369 us | 0.131 us | 0 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.495 us | 0.264 us | 1.88x | 0.365 us | 0.117 us | 528 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.517 us | 0.308 us | 1.68x | 0.347 us | 0.129 us | 528 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.630 us | 0.336 us | 1.87x | 0.372 us | 0.166 us | 0 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.142 us | 0.238 us | 0.59x | 0.342 us | 0.129 us | 0 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.522 us | 0.292 us | 1.79x | 1.201 us | 0.959 us | 0 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.218 us | 0.215 us | 1.01x | 0.326 us | 0.046 us | 0 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.184 us | 0.220 us | 0.83x | 0.329 us | 0.079 us | 0 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.243 us | 0.237 us | 1.02x | 0.338 us | 0.109 us | 0 B |
| `common/date-miss` | `IsMatch` | ScalarResult | CPython faster | 2.211 us | 0.866 us | 2.55x | 1.025 us | 2.178 us | 0 B |
| `common/ip-match` | `IsMatch` | ScalarResult | Managed faster | 0.059 us | 0.322 us | 0.18x | 0.485 us | 0.275 us | 0 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | CPython faster | 1.587 us | 1.332 us | 1.19x | 1.537 us | 0.337 us | 496 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 648.519 us | 2,473.011 us | 0.26x | 2,225.750 us | 811.434 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 298.382 us | 2,585.052 us | 0.12x | 2,411.426 us | 747.458 us | 0 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 57.666 us | 429.607 us | 0.13x | 527.482 us | 323.448 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 465.314 us | 369.441 us | 1.26x | 372.690 us | 427.575 us | 184,440 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 485.467 us | 517.908 us | 0.94x | 503.421 us | 481.423 us | 184,440 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 37.781 us | 27.349 us | 1.37x | 41.690 us | 73.626 us | 4,160 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | CPython faster | 102.484 us | 61.769 us | 1.66x | 75.924 us | 84.791 us | 32,928 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 483.474 us | 213.216 us | 2.26x | 218.856 us | 374.991 us | 94,552 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 707.921 us | 350.159 us | 2.02x | 374.297 us | 730.589 us | 242,008 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 760.455 us | 1,252.997 us | 0.61x | 1,201.520 us | 828.536 us | 242,008 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 552.978 us | 624.233 us | 0.88x | 634.209 us | 373.329 us | 119,632 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 253.141 us | 545.489 us | 0.46x | 529.386 us | 162.628 us | 127,272 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 709.408 us | 1,108.930 us | 0.64x | 1,122.143 us | 1,830.659 us | 0 B |
| `count/zero` | `Count` | ScalarResult | Managed faster | 29.916 us | 91.424 us | 0.33x | 91.348 us | 1.142 us | 0 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.288 us | 2.016 us | 0.14x | 2.660 us | 0.711 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | Managed faster | 267.636 us | 381.424 us | 0.70x | 350.659 us | 69.521 us | 0 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Managed faster | 14.423 us | 49.903 us | 0.29x | 48.767 us | 21.784 us | 0 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.540 us | 90.529 us | 0.01x | 91.169 us | 1.191 us | 0 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 0.766 us | 38.687 us | 0.02x | 39.770 us | 0.758 us | 64 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | EagerMaterializedResult | CPython faster | 17.307 us | 16.448 us | 1.05x | 16.966 us | 54.134 us | 4,152 B |
| `findall/empty-progression-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 90.975 us | 43.218 us | 2.11x | 46.218 us | 48.317 us | 16,464 B |
| `findall/optional-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Equivalent | 56.116 us | 55.390 us | 1.01x | 49.490 us | 101.319 us | 4,416 B |
| `findall/named-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 151.954 us | 95.130 us | 1.60x | 101.628 us | 147.801 us | 43,352 B |
| `findall/structural-many` | `FindAllStructural` | EagerMaterializedResult | Managed faster | 150.369 us | 201.828 us | 0.75x | 195.937 us | 113.397 us | 37,160 B |
| `finditer/zero-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 0.273 us | 150.085 us | 0.00x | 140.271 us | 0.631 us | 0 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 189.693 us | 177.634 us | 1.07x | 178.588 us | 221.042 us | 122,928 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | Equivalent | 178.877 us | 181.054 us | 0.99x | 182.349 us | 228.375 us | 40,984 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | CPython faster | 95.403 us | 90.766 us | 1.05x | 91.735 us | 477.374 us | 61,488 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | Managed faster | 89.577 us | 91.788 us | 0.98x | 94.116 us | 474.917 us | 20,504 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Managed faster | 592.986 us | 17,308.210 us | 0.03x | 17,076.399 us | 351.518 us | 235,728 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Managed faster | 574.113 us | 17,305.425 us | 0.03x | 17,012.378 us | 357.946 us | 226,512 B |
| `replacement/no-match-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 2.528 us | 2.418 us | 1.04x | 3.339 us | 1.081 us | 20,016 B |
| `replacement/one-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 6,666.312 us | 16,602.461 us | 0.40x | 16,389.819 us | 0.838 us | 10,896 B |
| `replacement/named-template-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 1.274 us | 0.946 us | 1.34x | 1.241 us | 0.531 us | 624 B |
| `replacement/unmatched-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 46.744 us | 50.871 us | 0.92x | 47.456 us | 31.279 us | 3,952 B |
| `replacement/limited-string` | `ReplaceStringLimited` | EagerMaterializedResult | Inconclusive | 1.319 us | 0.452 us | 2.90x | 1.219 us | 1.078 us | 12,360 B |
| `replacement/empty-progression-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 46.454 us | 23.733 us | 1.96x | 23.642 us | 12.036 us | 2,912 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | EagerMaterializedResult | Managed faster | 153.161 us | 4,272.280 us | 0.04x | 4,248.259 us | 91.569 us | 59,040 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 285.580 us | 223.444 us | 1.28x | 227.885 us | 246.348 us | 143,488 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | CPython faster | 415.780 us | 254.998 us | 1.63x | 254.575 us | 838.864 us | 274,856 B |
| `split/no-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.423 us | 25.029 us | 0.06x | 25.785 us | 0.708 us | 8,760 B |
| `split/one-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 1.269 us | 12.051 us | 0.11x | 12.593 us | 0.769 us | 4,192 B |
| `split/limited` | `SplitStringsLimited` | EagerMaterializedResult | Inconclusive | 1.544 us | 0.545 us | 2.84x | 1.260 us | 1.261 us | 6,032 B |
| `split/optional-capture` | `SplitStrings` | EagerMaterializedResult | Managed faster | 69.505 us | 83.489 us | 0.83x | 83.986 us | 84.487 us | 34,144 B |
| `split/zero-width` | `SplitStrings` | EagerMaterializedResult | CPython faster | 126.878 us | 66.745 us | 1.91x | 79.425 us | 298.091 us | 40,016 B |
| `split/unicode` | `SplitStrings` | EagerMaterializedResult | CPython faster | 70.507 us | 44.655 us | 1.58x | 42.987 us | 55.122 us | 19,512 B |
| `split/detailed` | `SplitDetailed` | EagerMaterializedResult | Managed faster | 109.944 us | 136.357 us | 0.81x | 137.373 us | 93.267 us | 102,784 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | ScalarResult | Managed faster | 4,284.267 us | 358,137.491 us | 0.01x | 366,935.219 us | 8,608.217 us | 1,544 B |
| `corpus/code-log-methods` | `Count` | ScalarResult | Managed faster | 49.452 us | 581.188 us | 0.09x | 585.689 us | 50.402 us | 528 B |
| `corpus/code-declarations` | `Count` | ScalarResult | Managed faster | 118.216 us | 630.987 us | 0.19x | 633.484 us | 760.147 us | 528 B |
| `corpus/sherlock-ru` | `Count` | ScalarResult | Managed faster | 187.897 us | 400.957 us | 0.47x | 4,190.957 us | 5,338.180 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `captured` | `(?P<word>[A-Za-z]+)-(?P=word)` | 11 B | 17.016 us / 4,897 B | 129.922 us / 53,625 B | 202.672 us / 71,617 B | 187.522 us / 71,849 B | 60.371 us | 60.665 us |
| `fallback` | `header:[0-9]+` | 12 B | 52.472 us / 3,425 B | 261.803 us / 61,689 B | 316.256 us / 83,561 B | 272.897 us / 83,529 B | 42.014 us | 42.462 us |
| `literal` | `needle` | 20 B | 18.359 us / 2,481 B | 151.850 us / 40,313 B | 292.916 us / 50,369 B | 319.606 us / 50,337 B | 25.095 us | 24.909 us |
| `unicode` | `Шерлок Холмс` | 37 B | 5.822 us / 2,905 B | 34.362 us / 33,697 B | 55.206 us / 48,313 B | 60.341 us / 48,281 B | 36.291 us | 36.247 us |

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Reject** | 6.6 % / 19.4 % | 1.013 / 1.103 | 1.108 | 9.8 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 86.9 % / 2.4 % | 1.339 / 1.020 | 1.144 | 2.6 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Pass** | 23.9 % / 4.8 % | 1.006 / 1.011 | 1.013 | 9.1 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Pass** | 13.7 % / 1.7 % | 1.017 / 1.022 | 1.018 | 8.0 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Pass** | 0.8 % / 4.6 % | 1.009 / 1.049 | 1.045 | 9.7 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 816.5 % / 21.1 % | 1.036 / 1.017 | 1.047 | 9.5 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `Utf8Regex/OwnedScalar; findall string shaping` | **Reject** | 1.4 % / 8.5 % | 1.012 / 1.011 | 1.026 | 3.7 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Reject** — maximum spread is 1.013/1.103; maximum symmetric order effect is 1.108. Robust slopes are 0.000031 us/unit managed and 0.000346 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 0.777 us | 0.240 us | 3.239x [2.925, 3.274] | 0.903 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 1.244 us | 6.103 us | 0.202x [0.197, 0.210] | 0.959 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 1.664 us | 11.526 us | 0.144x [0.137, 0.145] | 0.962 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 2.980 us | 22.995 us | 0.130x [0.127, 0.133] | 0.975 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 86.9 %/2.4 %; maximum spread is 1.339/1.020; maximum symmetric order effect is 1.144; minimum lane duration is 2.568 ms. Robust slopes are -0.123226 us/unit managed and 0.176196 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 1.767 us | 0.570 us | 3.098x [3.058, 3.170] | 1.000 | 96 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 2.013 us | 0.945 us | 2.119x [0.778, 2.868] | 0.874 | 480 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 0.757 us | 1.274 us | 0.595x [0.581, 0.611] | 0.978 | 608 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 1.063 us | 1.980 us | 0.533x [0.529, 0.543] | 1.008 | 1,016 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.000021 us/unit managed and 0.000342 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.062 us | 0.147 us | 0.422x [0.415, 0.425] | 1.001 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.070 us | 0.489 us | 0.143x [0.143, 0.145] | 1.013 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.323 us | 5.772 us | 0.056x [0.056, 0.057] | 0.988 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 1.749 us | 22.417 us | 0.078x [0.078, 0.078] | 0.997 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.012406 us/unit managed and 0.118831 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 1.498 us | 22.947 us | 0.065x [0.063, 0.083] | 0.982 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 2.008 us | 24.635 us | 0.081x [0.080, 0.084] | 0.989 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 2.339 us | 30.189 us | 0.077x [0.077, 0.078] | 0.999 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 4.701 us | 54.169 us | 0.087x [0.087, 0.087] | 1.004 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Pass** — Stable route, residuals at most 25%, lane spreads and order effect at most 1.10, and every lane at least 5 ms. Robust slopes are 0.001225 us/unit managed and 0.000022 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.248 us | 0.222 us | 1.112x [1.103, 1.164] | 0.957 | 128 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.538 us | 0.227 us | 2.378x [2.326, 2.474] | 1.043 | 848 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 5.219 us | 0.328 us | 16.301x [15.815, 16.714] | 0.957 | 12,368 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 20.394 us | 0.587 us | 35.295x [34.557, 35.899] | 1.032 | 49,232 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 816.5 %/21.1 %. Robust slopes are 92.781422 us/unit managed and -0.661422 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 0.987 us | 10.445 us | 0.094x [0.092, 0.096] | 0.972 | 440 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 120.626 us | 6.557 us | 18.255x [18.076, 18.906] | 1.013 | 68,112 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 130.190 us | 6.609 us | 19.771x [19.557, 21.192] | 0.955 | 68,112 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 295.445 us | 6.576 us | 44.750x [44.481, 53.237] | 1.003 | 135,696 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — minimum lane duration is 3.730 ms. Robust slopes are 0.169515 us/unit managed and 0.090742 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 9.276 us | 4.617 us | 2.022x [1.993, 2.063] | 0.974 | 1,320 B |
| 192 results | 192 | 128 B | 192 | 0 B | 33.944 us | 18.469 us | 1.839x [1.828, 1.852] | 1.005 | 3,624 B |
| 768 results | 768 | 512 B | 768 | 0 B | 132.253 us | 71.178 us | 1.854x [1.813, 1.864] | 0.986 | 25,176 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 518.379 us | 276.705 us | 1.879x [1.835, 1.903] | 1.018 | 111,240 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.11x [0.11, 0.11]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.13x [0.13, 0.13]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.07x [0.07, 0.07]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 1.00x [0.99, 1.00]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 0.18x [0.18, 0.19]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 2.79x [2.78, 2.81]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.33x [1.32, 1.33]; CpythonFaster |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.05x [1.04, 1.05]; NotApplicable |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.60x [1.59, 1.61]; NotApplicable |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.83x [0.83, 0.84]; ManagedFaster |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.12x [1.12, 1.13]; CpythonFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 validation; direct ASCII literal-prefix/dot-all/literal-suffix full match; value ranges` | Rbyte 0.38x [0.38, 0.39]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.93x [1.92, 1.94]; CpythonFaster |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.89x [0.89, 0.89]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.76x [0.75, 0.76]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.67x [1.66, 1.70]; NotApplicable |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.51x [1.50, 1.52]; CpythonFaster |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.44x [1.43, 1.44]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.75x [0.74, 0.75]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.53x [1.52, 1.54]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.24x [1.24, 1.26]; NotApplicable |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.92x [0.92, 0.92]; Unqualified |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/OwnedScalar; full-match value ranges` | Rbyte 1.99x [1.99, 2.01]; CpythonFaster |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 1.16x [1.14, 1.16]; CpythonFaster |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.18x [1.15, 1.19]; NotApplicable |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.78x [1.77, 1.79]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 1.54x [1.53, 1.55]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 1.73x [1.71, 1.73]; CpythonFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.79x [0.78, 0.79]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 0.97x [0.96, 0.97]; Unqualified |
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
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/OwnedScalar; boolean result` | Rbyte 2.62x [2.60, 2.63]; CpythonFaster |
| `common/ip-match` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Rbyte 0.18x [0.18, 0.18]; NotApplicable |
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
