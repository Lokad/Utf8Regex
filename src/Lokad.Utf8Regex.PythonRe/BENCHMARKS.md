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

- Generated: `2026-08-26T16:43:54.3838311+00:00`
- Snapshot SHA-256: `81B62F8A499471D4A67BF2D8B4DAA36E8EF072B5E343F002C07D1D12C85B37CD`
- Schema: `10`
- Catalog SHA-256: `2778C85CE59E3342D337F8042C6A2C34FFAD34ABA819977DD633D848D31A2B0A`
- Cases: `90`
- Lifecycle families: `4`
- Scaling families: `7`
- Public Status: `57` managed faster, `0` equivalent, `12` CPython faster, `20` inconclusive, `1` unqualified
- Historical point measurement environments represented: `12`
- Corpus: [`tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json`](../../tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json) (`9` vectors, SHA-256 `0A77376F84956A732A5B5F5D36EA884347FCBA3704DA32D4ED3F6AAFD2554E8B`)
- Corpus provenance limitation: The original upstream CPython version was not recorded; do not infer one from local vector names.
- Historical CPython point environments represented: `1`
- Historical CPython point baseline: `CPython 3.13.2` at `C:\Python313\python.exe` on Windows-11-10.0.26200-SP0

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
| `corpus/code-log-methods` | `LokadReplica/Code/Data` | 53,997 | `9ACD9AAF10E1CB2E813C33520BD3F769FA3E0D766FF9417AD5F4E3B8501B9B08` |
| `corpus/code-declarations` | `LokadReplica/Code/Data` | 53,997 | `9ACD9AAF10E1CB2E813C33520BD3F769FA3E0D766FF9417AD5F4E3B8501B9B08` |
| `corpus/sherlock-ru` | `DotNetPerformanceReplica/Stress/Data/opensubtitles/ru-sampled.txt` | 1,600,556 | `8512E14DA1DA35C8402252B174C5F2470F2C6322DC49EA78B466AFCCC14A1BAD` |

Historical point rows span more than one measurement environment. Consult the JSON row metadata before interpreting small differences as regressions or wins; qualified paired rows carry separate exact provenance.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Managed faster | 1.849 us | 12.088 us | 0.15x | 14.877 us | 12.075 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.835 us | 12.258 us | 0.15x | 14.380 us | 12.183 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.836 us | 12.116 us | 0.15x | 14.464 us | 10.417 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.226 us | 0.182 us | 1.22x | 0.781 us | 1.444 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.034 us | 0.206 us | 0.16x | 0.245 us | 0.094 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Inconclusive | 13.209 us | 13.268 us | 1.00x | 19.217 us | 25.377 us | 14,360 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | CPython faster | 0.983 us | 0.149 us | 6.60x | 2.195 us | 40.187 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 0.140 us | 0.110 us | 1.27x | 0.215 us | 0.102 us | 56 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.102 us | 0.233 us | 0.44x | 0.252 us | 0.165 us | 280 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.055 us | 0.179 us | 0.31x | 0.263 us | 0.070 us | 64 B |
| `multiline/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.090 us | 0.250 us | 0.38x | 0.262 us | 0.132 us | 272 B |
| `multiline/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.072 us | 0.212 us | 0.34x | 0.414 us | 0.109 us | 64 B |
| `dotall/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | CPython faster | 4.531 us | 0.209 us | 21.91x | 0.367 us | 8.799 us | 8,232 B |
| `verbose/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.062 us | 0.192 us | 0.32x | 0.239 us | 0.143 us | 48 B |
| `unicode-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.137 us | 0.188 us | 0.73x | 0.256 us | 0.192 us | 312 B |
| `ascii-option/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.118 us | 0.209 us | 0.57x | 0.216 us | 0.132 us | 248 B |
| `lookahead/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.090 us | 0.191 us | 0.48x | 0.203 us | 0.138 us | 256 B |
| `lookbehind/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.066 us | 0.131 us | 0.50x | 0.222 us | 0.150 us | 40 B |
| `backreference/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.076 us | 0.167 us | 0.46x | 0.212 us | 0.182 us | 48 B |
| `atomic/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.097 us | 0.225 us | 0.44x | 0.219 us | 0.129 us | 248 B |
| `possessive/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.059 us | 0.134 us | 0.44x | 0.252 us | 0.092 us | 32 B |
| `reluctant/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.088 us | 0.220 us | 0.39x | 0.237 us | 0.119 us | 248 B |
| `scoped-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.098 us | 0.188 us | 0.52x | 0.233 us | 0.157 us | 248 B |
| `supplementary/fullmatch-hit` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.051 us | 0.156 us | 0.32x | 0.241 us | 0.116 us | 32 B |
| `search-offset/literal-hit` | `SearchFromOffset` | ConsumedGroupZeroRanges | Managed faster | 0.068 us | 0.146 us | 0.46x | 0.173 us | 0.125 us | 264 B |
| `backreference/named-fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.082 us | 0.109 us | 0.76x | 0.210 us | 0.144 us | 48 B |
| `lookahead/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.086 us | 0.121 us | 0.70x | 0.189 us | 0.146 us | 48 B |
| `lookbehind/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.104 us | 0.207 us | 0.50x | 0.224 us | 0.140 us | 256 B |
| `atomic/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.073 us | 0.199 us | 0.37x | 0.284 us | 0.133 us | 40 B |
| `possessive/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.085 us | 0.197 us | 0.43x | 0.213 us | 0.122 us | 248 B |
| `leading-inline/search-hit` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.115 us | 0.234 us | 0.49x | 0.261 us | 0.169 us | 272 B |
| `unicode-literal/search-hit` | `Search` | ConsumedGroupZeroRanges | Unqualified | 0.127 us | 0.145 us | 0.87x | 0.227 us | 0.135 us | 368 B |
| `unicode-literal/search-miss` | `Search` | ConsumedGroupZeroRanges | CPython faster | 1.659 us | 0.171 us | 9.61x | 0.732 us | 0.499 us | 2,856 B |
| `unicode-category/search-miss` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.053 us | 0.145 us | 0.36x | 0.209 us | 0.066 us | 32 B |
| `greedy/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.059 us | 0.131 us | 0.45x | 0.207 us | 0.094 us | 32 B |
| `alternation/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 0.095 us | 0.153 us | 0.61x | 0.211 us | 0.193 us | 48 B |
| `common/date-miss` | `IsMatch` | ScalarResult | Managed faster | 0.513 us | 0.844 us | 0.62x | 0.913 us | 0.837 us | 120 B |
| `common/ip-match` | `IsMatch` | ScalarResult | Managed faster | 0.176 us | 0.215 us | 0.82x | 0.302 us | 0.335 us | 264 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | Managed faster | 0.227 us | 0.807 us | 0.28x | 2.602 us | 0.350 us | 896 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 63.820 us | 1,178.302 us | 0.05x | 1,231.496 us | 462.747 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Managed faster | 378.907 us | 1,379.543 us | 0.27x | 1,424.645 us | 620.138 us | 172,056 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 27.890 us | 225.034 us | 0.12x | 271.193 us | 185.111 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 179.003 us | 208.762 us | 0.85x | 219.217 us | 248.829 us | 165,936 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 171.193 us | 300.291 us | 0.56x | 406.854 us | 318.232 us | 165,936 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 4.353 us | 15.434 us | 0.28x | 24.031 us | 42.099 us | 4,120 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 29.785 us | 36.030 us | 0.82x | 47.145 us | 60.234 us | 24,600 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Inconclusive | 121.136 us | 132.878 us | 0.91x | 126.792 us | 359.547 us | 149,792 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Inconclusive | 172.960 us | 189.047 us | 0.92x | 201.521 us | 449.797 us | 299,296 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Inconclusive | 315.279 us | 720.236 us | 0.44x | 873.741 us | 534.325 us | 1,528,096 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 167.463 us | 351.373 us | 0.48x | 359.520 us | 335.570 us | 785,704 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Inconclusive | 109.276 us | 335.383 us | 0.32x | 3,139.237 us | 153.941 us | 406,736 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 17.064 us | 552.448 us | 0.03x | 578.748 us | 295.137 us | 34,840 B |
| `count/zero` | `Count` | ScalarResult | Managed faster | 1.185 us | 49.212 us | 0.02x | 50.859 us | 1.161 us | 17,432 B |
| `count/one` | `Count` | ScalarResult | Managed faster | 0.149 us | 1.114 us | 0.13x | 1.475 us | 1.116 us | 0 B |
| `count/empty-progression` | `Count` | ScalarResult | Managed faster | 165.105 us | 180.274 us | 0.91x | 188.349 us | 73.541 us | 430,312 B |
| `count/from-offset` | `CountFromOffset` | ScalarResult | Managed faster | 15.339 us | 25.984 us | 0.59x | 25.283 us | 31.181 us | 8,216 B |
| `findall/zero-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 1.979 us | 48.825 us | 0.04x | 51.524 us | 1.706 us | 17,456 B |
| `findall/one-strings` | `FindAllStrings` | EagerMaterializedResult | Inconclusive | 1.720 us | 23.415 us | 0.07x | 22.611 us | 1.026 us | 8,288 B |
| `findall/from-offset-strings` | `FindAllStringsFromOffset` | EagerMaterializedResult | CPython faster | 18.824 us | 9.843 us | 1.93x | 10.674 us | 71.831 us | 4,120 B |
| `findall/empty-progression-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 62.158 us | 26.836 us | 2.32x | 28.178 us | 80.188 us | 239,168 B |
| `findall/optional-capture-strings` | `FindAllStrings` | EagerMaterializedResult | CPython faster | 57.545 us | 36.329 us | 1.60x | 42.166 us | 96.178 us | 209,648 B |
| `findall/named-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 38.190 us | 56.282 us | 0.68x | 64.074 us | 88.452 us | 58,120 B |
| `findall/structural-many` | `FindAllStructural` | EagerMaterializedResult | Managed faster | 57.560 us | 132.026 us | 0.44x | 127.430 us | 102.261 us | 46,640 B |
| `finditer/zero-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 0.476 us | 84.168 us | 0.01x | 87.823 us | 0.605 us | 8,728 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 51.571 us | 101.969 us | 0.50x | 116.107 us | 157.065 us | 123,392 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | Managed faster | 48.330 us | 103.779 us | 0.46x | 126.579 us | 162.701 us | 41,448 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | Inconclusive | 27.241 us | 52.703 us | 0.52x | 64.130 us | 121.769 us | 61,952 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | Managed faster | 24.296 us | 53.962 us | 0.45x | 62.087 us | 120.898 us | 20,968 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Inconclusive | 199.100 us | 9,682.283 us | 0.02x | 9,648.155 us | 181.239 us | 878,784 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Inconclusive | 240.965 us | 11,046.480 us | 0.02x | 9,560.736 us | 179.957 us | 863,360 B |
| `replacement/no-match-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 1.113 us | 1.312 us | 0.85x | 1.760 us | 7.544 us | 20,480 B |
| `replacement/one-template-string` | `ReplaceString` | EagerMaterializedResult | Inconclusive | 0.796 us | 8,889.617 us | 0.00x | 9,208.909 us | 4.552 us | 9,024 B |
| `replacement/named-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 0.394 us | 0.542 us | 0.73x | 0.738 us | 1.013 us | 688 B |
| `replacement/unmatched-template-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 17.208 us | 29.906 us | 0.58x | 32.309 us | 26.724 us | 3,768 B |
| `replacement/limited-string` | `ReplaceStringLimited` | EagerMaterializedResult | CPython faster | 0.812 us | 0.288 us | 2.80x | 0.651 us | 6.122 us | 16,912 B |
| `replacement/empty-progression-string` | `ReplaceString` | EagerMaterializedResult | CPython faster | 30.094 us | 14.399 us | 2.06x | 15.395 us | 45.950 us | 113,392 B |
| `replacement/evaluator-replace-string` | `ReplaceEvaluatorString` | EagerMaterializedResult | Managed faster | 62.608 us | 2,712.965 us | 0.02x | 2,880.355 us | 186.608 us | 219,840 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | Managed faster | 67.945 us | 114.055 us | 0.60x | 126.317 us | 74.816 us | 171,328 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | Managed faster | 130.333 us | 137.672 us | 0.94x | 142.175 us | 134.880 us | 499,032 B |
| `split/no-separator` | `SplitStrings` | EagerMaterializedResult | Managed faster | 0.491 us | 8.825 us | 0.06x | 12.599 us | 6.263 us | 8,792 B |
| `split/one-separator` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 0.471 us | 4.327 us | 0.11x | 5.209 us | 8.565 us | 8,408 B |
| `split/limited` | `SplitStringsLimited` | EagerMaterializedResult | CPython faster | 0.593 us | 0.359 us | 1.63x | 1.402 us | 19.035 us | 12,032 B |
| `split/optional-capture` | `SplitStrings` | EagerMaterializedResult | Managed faster | 48.205 us | 54.651 us | 0.89x | 61.871 us | 114.940 us | 182,032 B |
| `split/zero-width` | `SplitStrings` | EagerMaterializedResult | CPython faster | 171.063 us | 41.930 us | 4.09x | 72.923 us | 223.310 us | 239,120 B |
| `split/unicode` | `SplitStrings` | EagerMaterializedResult | Managed faster | 14.060 us | 24.573 us | 0.57x | 29.746 us | 57.306 us | 26,104 B |
| `split/detailed` | `SplitDetailed` | EagerMaterializedResult | Inconclusive | 51.596 us | 80.395 us | 0.63x | 82.944 us | 59.984 us | 313,360 B |

### Real-corpus workloads

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `corpus/mail-email-count` | `Count` | ScalarResult | Inconclusive | 7,849.075 us | 247,944.800 us | 0.03x | 256,415.700 us | 5,555.200 us | 12,389,328 B |
| `corpus/code-log-methods` | `Count` | ScalarResult | Inconclusive | 4.871 us | 534.928 us | 0.01x | 548.467 us | 23.764 us | 0 B |
| `corpus/code-declarations` | `Count` | ScalarResult | Inconclusive | 221.759 us | 560.005 us | 0.40x | 567.742 us | 159.385 us | 217,696 B |
| `corpus/sherlock-ru` | `Count` | ScalarResult | Managed faster | 136.295 us | 233.065 us | 0.57x | 2,482.780 us | 3,356.785 us | 0 B |

### Construction and first call

These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.

| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `captured` | `(?P<word>[A-Za-z]+)-(?P=word)` | 11 B | 1.006 us / 3,104 B | 47.517 us / 42,528 B | 48.102 us / 52,688 B | 53.027 us / 53,768 B | 35.308 us | 35.678 us |
| `fallback` | `header:[0-9]+` | 12 B | 4.541 us / 2,208 B | 134.734 us / 225,096 B | 147.191 us / 233,152 B | 466.831 us / 234,040 B | 24.248 us | 24.858 us |
| `literal` | `needle` | 20 B | 1.387 us / 1,328 B | 34.409 us / 49,352 B | 38.612 us / 54,848 B | 40.180 us / 54,848 B | 14.784 us | 14.756 us |
| `unicode` | `Шерлок Холмс` | 37 B | 0.512 us / 1,808 B | 24.964 us / 35,256 B | 29.700 us / 42,096 B | 31.147 us / 43,128 B | 20.923 us | 21.042 us |

### Scaling evidence

These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim. The JSON retains every alternating paired sample, lane order, batch duration, managed allocation, GC deltas, warmup, and CPU placement so noisy curves remain auditable.

| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C | Max order effect | Min lane |
|---|---|---|---:|---|---|---:|---:|---:|---:|
| `candidate-position` | candidate byte position | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Reject** | 2.2% / 57.7% | 1.405 / 1.122 | 2.049 | 9.7 ms |
| `capture-count` | capture group count | `SearchDetailed` | 4 | `Utf8Regex/AsciiSimplePattern; detailed capture projection` | **Reject** | 7.4% / 7.4% | 1.121 / 1.195 | 1.095 | 8.8 ms |
| `input-length` | UTF-8 input bytes | `Search` | 4 | `Utf8Regex/ExactAsciiLiteral; value ranges` | **Reject** | 111.1% / 1.4% | 1.056 / 1.017 | 1.036 | 9.5 ms |
| `match-count` | discovered match count | `Count` | 4 | `Utf8Regex/ExactAsciiLiteral; Python-style count progression` | **Reject** | 19.3% / 6.2% | 1.181 / 1.171 | 1.229 | 6.1 ms |
| `output-growth` | replacement output UTF-8 bytes | `ReplaceString` | 4 | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | **Reject** | 37.3% / 1.8% | 1.192 / 1.214 | 1.336 | 6.9 ms |
| `unicode-coordinate-density` | UTF-8 bytes per scalar | `SearchDetailed` | 4 | `Utf8Regex/ExactAsciiLiteral; detailed capture projection` | **Reject** | 237.8% / 7.2% | 2.197 / 1.213 | 2.352 | 7.7 ms |
| `zero-width-progression` | zero-width-aware result count | `FindAllStrings` | 4 | `strict UTF-8 decode; .NET Regex; findall string shaping` | **Reject** | 45.6% / 25.5% | 1.300 / 1.173 | 1.459 | 2.8 ms |

#### `candidate-position`

Dimension: candidate byte position. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 2.2%/57.7%; maximum spread is 1.405/1.122; maximum symmetric order effect is 2.049. Robust slopes are 0.000016 us/unit managed and 0.000194 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| byte 0 | 0 | 65,536 B | 1 | 0 B | 1.030 us | 0.156 us | 6.610x [6.218, 6.864] | 1.009 | 0 B |
| byte 16384 | 16,384 | 65,536 B | 1 | 0 B | 1.257 us | 3.153 us | 0.395x [0.387, 0.402] | 1.004 | 0 B |
| byte 32768 | 32,768 | 65,536 B | 1 | 0 B | 1.568 us | 6.177 us | 0.230x [0.204, 0.715] | 2.049 | 0 B |
| byte 65530 | 65,530 | 65,536 B | 1 | 0 B | 2.066 us | 13.480 us | 0.154x [0.151, 0.350] | 1.280 | 0 B |

#### `capture-count`

Dimension: capture group count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum spread is 1.121/1.195. Robust slopes are 0.062382 us/unit managed and 0.110203 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 captures | 0 | 8 B | 1 | 0 B | 0.140 us | 0.412 us | 0.334x [0.312, 0.390] | 1.038 | 432 B |
| 2 captures | 2 | 8 B | 3 | 0 B | 0.294 us | 0.653 us | 0.445x [0.365, 0.546] | 1.095 | 848 B |
| 4 captures | 4 | 8 B | 5 | 0 B | 0.382 us | 0.899 us | 0.438x [0.387, 0.568] | 1.000 | 1,240 B |
| 8 captures | 8 | 8 B | 9 | 0 B | 0.653 us | 1.214 us | 0.473x [0.447, 0.558] | 0.923 | 2,024 B |

#### `input-length`

Dimension: UTF-8 input bytes. Result contract: `ConsumedGroupZeroRanges`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 111.1%/1.4%. Robust slopes are 0.000024 us/unit managed and 0.000183 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 64 B | 64 | 64 B | 0 | 0 B | 0.219 us | 0.099 us | 2.215x [2.105, 2.256] | 0.970 | 0 B |
| 1024 B | 1,024 | 1,024 B | 0 | 0 B | 0.075 us | 0.278 us | 0.269x [0.262, 0.282] | 1.036 | 0 B |
| 16384 B | 16,384 | 16,384 B | 0 | 0 B | 0.430 us | 3.083 us | 0.139x [0.139, 0.140] | 0.995 | 0 B |
| 65536 B | 65,536 | 65,536 B | 0 | 0 B | 1.851 us | 12.071 us | 0.154x [0.146, 0.158] | 0.978 | 0 B |

#### `match-count`

Dimension: discovered match count. Result contract: `ScalarResult`. Samples: `5`. Fit gate: **Reject** — maximum spread is 1.181/1.171; maximum symmetric order effect is 1.229. Robust slopes are 0.006171 us/unit managed and 0.042671 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 matches | 0 | 65,536 B | 0 | 0 B | 1.459 us | 13.328 us | 0.108x [0.099, 0.126] | 0.978 | 0 B |
| 16 matches | 16 | 65,536 B | 16 | 0 B | 1.987 us | 15.284 us | 0.136x [0.123, 0.191] | 1.229 | 0 B |
| 64 matches | 64 | 65,536 B | 64 | 0 B | 2.341 us | 15.615 us | 0.133x [0.130, 0.158] | 0.925 | 0 B |
| 256 matches | 256 | 65,536 B | 256 | 0 B | 2.726 us | 24.909 us | 0.110x [0.108, 0.116] | 1.019 | 0 B |

#### `output-growth`

Dimension: replacement output UTF-8 bytes. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 37.3%/1.8%; maximum spread is 1.192/1.214; maximum symmetric order effect is 1.336. Robust slopes are 0.001682 us/unit managed and 0.000037 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 25 B output | 25 | 10 B | 1 | 25 B | 0.172 us | 0.166 us | 1.048x [0.787, 1.251] | 1.027 | 648 B |
| 265 B output | 265 | 10 B | 1 | 265 B | 0.704 us | 0.179 us | 4.035x [3.221, 4.772] | 0.991 | 4,424 B |
| 4105 B output | 4,105 | 10 B | 1 | 4,105 B | 7.374 us | 0.312 us | 28.524x [18.815, 30.966] | 0.749 | 58,472 B |
| 16393 B output | 16,393 | 10 B | 1 | 16,393 B | 26.789 us | 0.771 us | 34.073x [27.000, 48.172] | 0.761 | 252,888 B |

#### `unicode-coordinate-density`

Dimension: UTF-8 bytes per scalar. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 237.8%/7.2%; maximum spread is 2.197/1.213; maximum symmetric order effect is 2.352. Robust slopes are 58.227311 us/unit managed and 0.334789 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1-byte scalars | 1 | 16,390 B | 16,384 | 0 B | 3.576 us | 3.503 us | 0.858x [0.645, 1.461] | 0.604 | 33,432 B |
| 2-byte scalars | 2 | 32,774 B | 16,384 | 0 B | 44.797 us | 3.929 us | 11.635x [11.120, 12.591] | 0.932 | 101,104 B |
| 3-byte scalars | 3 | 49,158 B | 16,384 | 0 B | 77.196 us | 4.350 us | 19.307x [15.974, 23.275] | 0.814 | 101,104 B |
| 4-byte scalars | 4 | 65,542 B | 16,384 | 0 B | 229.278 us | 4.248 us | 57.787x [44.398, 119.920] | 2.352 | 201,456 B |

#### `zero-width-progression`

Dimension: zero-width-aware result count. Result contract: `EagerMaterializedResult`. Samples: `5`. Fit gate: **Reject** — maximum relative residual is 45.6%/25.5%; maximum spread is 1.300/1.173; maximum symmetric order effect is 1.459; minimum lane duration is 2.811 ms. Robust slopes are 0.155984 us/unit managed and 0.072200 us/unit CPython.

| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Order effect | Managed allocation |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 48 results | 48 | 32 B | 48 | 0 B | 12.082 us | 3.757 us | 3.188x [2.797, 5.035] | 0.989 | 20,816 B |
| 192 results | 192 | 128 B | 192 | 0 B | 23.532 us | 16.067 us | 1.510x [1.422, 2.113] | 0.931 | 82,880 B |
| 768 results | 768 | 512 B | 768 | 0 B | 93.415 us | 60.006 us | 1.557x [1.348, 2.236] | 1.151 | 330,992 B |
| 3072 results | 3,072 | 2,048 B | 3,072 | 0 B | 588.587 us | 209.744 us | 2.397x [1.679, 6.740] | 1.459 | 1,323,296 B |


### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.15x [0.14, 0.15]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.15, 0.15]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.15, 0.16]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 1.01x [0.98, 1.02]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.15x [0.14, 0.15]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 6.22x [6.16, 6.30]; CpythonFaster |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 1.00x [0.99, 1.01]; Unqualified |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiLiteralIgnoreCase; value ranges` | Rbyte 0.42x [0.41, 0.43]; Inconclusive |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiLiteralIgnoreCase; value ranges` | Rbyte 0.26x [0.25, 0.26]; ManagedFaster |
| `multiline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.36x [0.35, 0.37]; ManagedFaster |
| `multiline/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.32x [0.31, 0.33]; ManagedFaster |
| `dotall/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 19.33x [18.82, 20.17]; NotApplicable |
| `verbose/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.28x [0.28, 0.29]; NotApplicable |
| `unicode-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `ascii-option/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.53x [0.52, 0.55]; NotApplicable |
| `lookahead/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.44x [0.41, 0.45]; ManagedFaster |
| `lookbehind/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.44x [0.44, 0.45]; ManagedFaster |
| `backreference/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.41x [0.39, 0.41]; NotApplicable |
| `atomic/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.41x [0.40, 0.44]; NotApplicable |
| `possessive/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.39x [0.38, 0.39]; NotApplicable |
| `reluctant/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.38x [0.37, 0.38]; NotApplicable |
| `scoped-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiLiteralIgnoreCase; value ranges` | Rbyte 0.49x [0.48, 0.50]; ManagedFaster |
| `supplementary/fullmatch-hit` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `search-offset/literal-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges from a nonzero byte offset` | Rbyte 0.42x [0.41, 0.44]; Inconclusive |
| `backreference/named-fullmatch-miss` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Rbyte 0.60x [0.58, 0.61]; NotApplicable |
| `lookahead/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.63x [0.61, 0.66]; Inconclusive |
| `lookbehind/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.47x [0.47, 0.48]; ManagedFaster |
| `atomic/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.33x [0.33, 0.33]; NotApplicable |
| `possessive/search-hit` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.40x [0.40, 0.43]; NotApplicable |
| `leading-inline/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiSimplePattern; value ranges` | Rbyte 0.46x [0.45, 0.47]; ManagedFaster |
| `unicode-literal/search-hit` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literal; value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `unicode-category/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Excluded: Locale or Unicode flags are not valid for equivalent CPython bytes patterns. |
| `greedy/search-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; value ranges` | Rbyte 0.39x [0.39, 0.40]; NotApplicable |
| `alternation/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactUtf8Literals; value ranges` | Rbyte 0.55x [0.53, 0.59]; ManagedFaster |
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
| `findall/empty-progression-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/optional-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/named-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex ValueMatch enumeration; direct trailing-capture string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/structural-many` | `_sre scanner + Python group-zero projection` | `Utf8Regex/FallbackRegex; group-zero structural match shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `finditer/zero-detailed` | `_sre scanner + Python detailed projection` | `strict UTF-8 decode; .NET Regex; detailed iteration shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/no-match-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/one-template-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/named-template-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/unmatched-template-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/limited-string` | `_sre C Pattern.sub` | `Utf8Regex/ExactAsciiLiteral; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/empty-progression-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex; replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-replace-string` | `_sre C Pattern.sub + Python callback` | `strict UTF-8 decode; .NET Regex callback replacement; string shaping without count` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/no-separator` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/one-separator` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/limited` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex bounded split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/optional-capture` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/zero-width` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/unicode` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/detailed` | `_sre C Pattern.split + Python split-item metadata projection` | `strict UTF-8 decode; .NET Regex split; item-metadata shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `common/date-miss` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Rbyte 0.85x [0.83, 0.88]; NotApplicable |
| `common/ip-match` | `_sre C Pattern.search` | `Utf8Regex/FallbackRegex; boolean result` | Rbyte 0.78x [0.76, 0.82]; NotApplicable |
| `corpus/mail-email-count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/FallbackRegex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `corpus/code-log-methods` | `_sre scanner + Python finditer/sum` | `Utf8Regex/AsciiStructuralIdentifierFamily; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
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
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmark-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-lifecycle","32","5"
./bench.ps1 -CommandArgs "--refresh-pythonre-scaling","5"
./bench.ps1 -CommandArgs "--verify-pythonre-scaling"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmarks","200","7"
./bench.ps1 -CommandArgs "--verify-pythonre-benchmark-markdown"
```

The case definitions live in [`PythonReBenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkCatalog.cs); timed projection logic lives in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).
