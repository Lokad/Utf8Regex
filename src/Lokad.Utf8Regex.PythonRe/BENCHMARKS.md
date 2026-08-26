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

Qualified `Search`, `Match`, and `FullMatch` rows use the `ConsumedGroupZeroRanges` contract: every timed operation consumes success plus group-zero byte and UTF-16 boundaries. Result hashing and value verification remain outside timing. Other result-producing rows retain their required eager public materialization.

Eligible ASCII one-shot rows also measure a CPython bytes `Pattern` over the identical bytes. `Rbyte` is representation-neutral engine evidence and never sets public Status; rows without equivalent byte semantics carry an explicit exclusion reason.

## Snapshot summary

- Generated: `2026-08-26T14:30:42.6421102+00:00`
- Snapshot SHA-256: `CD4E12F7BD7E2E0C74F1BBA7CC9AC1F1165277E1C3CE9BE5D6D40F2D56D4813A`
- Schema: `8`
- Catalog SHA-256: `C731F1AE719DADD259748DD98AD02A698CFACD7EA3503D3E6CB594129543E3F1`
- Cases: `32`
- Public Status: `18` managed faster, `0` equivalent, `1` CPython faster, `12` inconclusive, `1` unqualified
- Historical point measurement environments represented: `2`
- Corpus: [`tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json`](../../tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json) (`9` vectors, SHA-256 `0A77376F84956A732A5B5F5D36EA884347FCBA3704DA32D4ED3F6AAFD2554E8B`)
- Corpus provenance limitation: The original upstream CPython version was not recorded; do not infer one from local vector names.
- Historical CPython point environments represented: `1`
- Historical CPython point baseline: `CPython 3.13.2` at `C:\Python313\python.exe` on Windows-11-10.0.26200-SP0

## Coverage summary

This catalog currently covers `32` operation rows over `17` distinct patterns. Zero-count sections below are deliberate, visible backlog rather than implicit coverage.

| Axis | Covered values |
|---|---|
| Operation families | `Count`, `FindAllStrings`, `FindAllUtf8`, `FindIterDetailed`, `FullMatch`, `IsMatch`, `Match`, `ReplaceString`, `ReplaceUtf8`, `Search`, `SearchDetailed`, `SplitStrings`, `SubnEvaluatorString`, `SubnEvaluatorUtf8`, `SubnString`, `SubnUtf8` |
| Flags | `Ascii`, `IgnoreCase`, `None` |
| Feature families | `ASCII class run`, `ASCII word boundary`, `Anchors and fixed classes`, `Callback replacement`, `Captured separator`, `Exact literal`, `Exact literal replacement`, `Ignore-case literal`, `Literal alternation`, `Mixed-width captures`, `One capture`, `Prefix and digit repeat`, `Quantified Unicode literal`, `Separated captures`, `Separator class`, `Unicode literal` |
| Managed route classes | `ExactAsciiLiteral`, `ExactUtf8Literal`, `ManagedFallback`, `SimpleNative` |
| Result cardinalities | `Many`, `ManyEmpty`, `One`, `Zero` |
| Input-width classes | `Ascii`, `Ascii+TwoByte`, `Ascii+TwoByte+FourByte` |
| Corpus provenance | `Synthetic catalog generator` |
| Claim classes | `Composed`, `Public` |

| Result section | Rows |
|---|---:|
| Direct matching | 10 |
| Detailed and scalar projections | 1 |
| Count, FindAll, and FindIter | 13 |
| Replace and Subn | 6 |
| Split | 2 |
| Real-corpus workloads | 0 |
| Construction and first call | 0 |
| Scaling evidence | 0 |
| Comparator and semantic exclusions | 0 |

Historical point rows span more than one measurement environment. Consult the JSON row metadata before interpreting small differences as regressions or wins; qualified paired rows carry separate exact provenance.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

### Direct matching

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `literal/ismatch` | `IsMatch` | ScalarResult | Inconclusive | 1.867 us | 12.263 us | 0.15x | 14.877 us | 12.075 us | 0 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.866 us | 12.222 us | 0.15x | 14.380 us | 12.183 us | 0 B |
| `literal/search-miss` | `Search` | ConsumedGroupZeroRanges | Managed faster | 1.869 us | 12.101 us | 0.15x | 14.464 us | 10.417 us | 0 B |
| `prefix/match` | `Match` | ConsumedGroupZeroRanges | CPython faster | 0.214 us | 0.176 us | 1.21x | 0.781 us | 1.444 us | 0 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.035 us | 0.213 us | 0.16x | 0.245 us | 0.094 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 13.289 us | 14.613 us | 0.91x | 19.217 us | 25.377 us | 14,360 B |
| `literal/search-early` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.537 us | 0.164 us | 9.37x | 2.195 us | 40.187 us | 0 B |
| `anchor/fullmatch-miss` | `FullMatch` | ConsumedGroupZeroRanges | Unqualified | 0.142 us | 0.118 us | 1.15x | 0.215 us | 0.102 us | 56 B |
| `ignorecase/search-hit` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.102 us | 0.217 us | 0.46x | 0.252 us | 0.165 us | 280 B |
| `ignorecase/search-miss` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 0.041 us | 0.169 us | 0.25x | 0.263 us | 0.070 us | 64 B |

### Detailed and scalar projections

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | EagerMaterializedResult | Inconclusive | 0.268 us | 0.811 us | 0.33x | 2.602 us | 0.350 us | 896 B |

### Count, FindAll, and FindIter

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `family/count` | `Count` | ScalarResult | Managed faster | 64.802 us | 1,188.974 us | 0.05x | 1,231.496 us | 462.747 us | 0 B |
| `class-run/count` | `Count` | ScalarResult | Inconclusive | 399.054 us | 1,353.800 us | 0.29x | 1,424.645 us | 620.138 us | 172,056 B |
| `unicode/count` | `Count` | ScalarResult | Managed faster | 25.061 us | 205.214 us | 0.12x | 271.193 us | 185.111 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 182.238 us | 206.084 us | 0.88x | 219.217 us | 248.829 us | 165,936 B |
| `findall/full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 169.190 us | 303.243 us | 0.56x | 406.854 us | 318.232 us | 165,936 B |
| `findall/unicode-full-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 4.527 us | 15.463 us | 0.29x | 24.031 us | 42.099 us | 4,120 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 29.778 us | 36.007 us | 0.82x | 47.145 us | 60.234 us | 24,600 B |
| `findall/one-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Inconclusive | 112.377 us | 123.314 us | 0.91x | 126.792 us | 359.547 us | 149,792 B |
| `findall/many-capture-strings` | `FindAllStrings` | EagerMaterializedResult | Managed faster | 183.432 us | 191.044 us | 0.96x | 201.521 us | 449.797 us | 299,296 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Inconclusive | 366.239 us | 735.313 us | 0.49x | 873.741 us | 534.325 us | 1,528,096 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | EagerMaterializedResult | Managed faster | 206.199 us | 353.349 us | 0.58x | 359.520 us | 335.570 us | 785,704 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | EagerMaterializedResult | Managed faster | 108.439 us | 335.475 us | 0.32x | 3,139.237 us | 153.941 us | 406,736 B |
| `zero-width/count` | `Count` | ScalarResult | Managed faster | 17.574 us | 553.707 us | 0.03x | 578.748 us | 295.137 us | 34,840 B |

### Replace and Subn

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `replacement/fixed-string` | `ReplaceString` | EagerMaterializedResult | Managed faster | 55.177 us | 102.086 us | 0.54x | 116.107 us | 157.065 us | 123,392 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | EagerMaterializedResult | Inconclusive | 49.207 us | 104.676 us | 0.47x | 126.579 us | 162.701 us | 41,448 B |
| `replacement/subn-string` | `SubnString` | EagerMaterializedResult | Managed faster | 37.388 us | 66.767 us | 0.54x | 64.130 us | 121.769 us | 61,952 B |
| `replacement/subn-utf8` | `SubnUtf8` | EagerMaterializedResult | Managed faster | 25.925 us | 53.623 us | 0.48x | 62.087 us | 120.898 us | 20,968 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | EagerMaterializedResult | Inconclusive | 251.022 us | 9,848.200 us | 0.02x | 9,648.155 us | 181.239 us | 878,784 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | EagerMaterializedResult | Inconclusive | 289.710 us | 9,923.240 us | 0.02x | 9,560.736 us | 179.957 us | 863,360 B |

### Split

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `split/no-captures` | `SplitStrings` | EagerMaterializedResult | Managed faster | 74.370 us | 115.228 us | 0.65x | 126.317 us | 74.816 us | 171,328 B |
| `split/captures` | `SplitStrings` | EagerMaterializedResult | Inconclusive | 136.868 us | 137.931 us | 0.99x | 142.175 us | 134.880 us | 499,032 B |

### Real-corpus workloads

No benchmark rows are cataloged in this section yet.

### Construction and first call

No benchmark rows are cataloged in this section yet.

### Scaling evidence

No benchmark rows are cataloged in this section yet.

### Comparator and semantic exclusions

No benchmark rows are cataloged in this section yet.


## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Rbyte 0.15x [0.15, 0.16]; ManagedFaster |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.15, 0.15]; ManagedFaster |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.15, 0.16]; ManagedFaster |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges` | Rbyte 1.01x [0.90, 1.05]; NotApplicable |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.15x [0.14, 0.15]; ManagedFaster |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `literal/search-early` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 7.72x [7.13, 13.87]; Inconclusive |
| `anchor/fullmatch-miss` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.86x [0.84, 8.70]; Unqualified |
| `ignorecase/search-hit` | `_sre C Pattern.search` | `Utf8Regex/AsciiLiteralIgnoreCase; value ranges` | Rbyte 0.41x [0.39, 0.46]; Inconclusive |
| `ignorecase/search-miss` | `_sre C Pattern.search` | `Utf8Regex/AsciiLiteralIgnoreCase; value ranges` | Rbyte 0.17x [0.16, 0.19]; Inconclusive |
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
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmarks","200","7"
./bench.ps1 -CommandArgs "--verify-pythonre-benchmark-markdown"
```

The case definitions live in [`PythonReBenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkCatalog.cs); timed projection logic lives in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).
