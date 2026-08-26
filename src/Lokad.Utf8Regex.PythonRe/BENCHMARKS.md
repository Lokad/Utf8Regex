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

- Generated: `2026-08-26T11:13:58.7849466+00:00`
- Snapshot SHA-256: `8E49D32AC528F5732A366A590B3A05BB788EB36D6BA3329C247908C7AB4CFEBA`
- Schema: `6`
- Cases: `28`
- Public Status: `2` managed faster, `0` equivalent, `0` CPython faster, `1` inconclusive, `25` unqualified
- Measurement environments represented: `1`
- Corpus: [`tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json`](../../tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json) (`9` vectors, SHA-256 `0A77376F84956A732A5B5F5D36EA884347FCBA3704DA32D4ED3F6AAFD2554E8B`)
- Corpus provenance limitation: The original upstream CPython version was not recorded; do not infer one from local vector names.
- CPython environments represented: `1`
- CPython baseline: `CPython 3.13.2` at `C:\Python313\python.exe` on Windows-11-10.0.26200-SP0

Measured from source `9730cc32f96c` on .NET 10.0.11, Microsoft Windows 10.0.26200, Intel64 Family 6 Model 183 Stepping 1, GenuineIntel.

## Results

`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.

| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | Historical | Unqualified | 0.501 us | 2.489 us | 0.20x | 2.602 us | 0.350 us | 896 B |
| `class-run/count` | `Count` | Historical | Unqualified | 504.865 us | 1,415.960 us | 0.36x | 1,424.645 us | 620.138 us | 172,056 B |
| `family/count` | `Count` | Historical | Unqualified | 68.200 us | 1,240.842 us | 0.05x | 1,231.496 us | 462.747 us | 0 B |
| `findall/full-strings` | `FindAllStrings` | Historical | Unqualified | 244.742 us | 303.417 us | 0.81x | 219.217 us | 248.829 us | 165,936 B |
| `findall/full-utf8` | `FindAllUtf8` | Historical | Unqualified | 268.899 us | 309.247 us | 0.87x | 406.854 us | 318.232 us | 165,936 B |
| `findall/many-capture-strings` | `FindAllStrings` | Historical | Unqualified | 388.206 us | 196.091 us | 1.98x | 201.521 us | 449.797 us | 1,331,488 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | Historical | Unqualified | 412.906 us | 789.879 us | 0.52x | 873.741 us | 534.325 us | 1,528,096 B |
| `findall/one-capture-strings` | `FindAllStrings` | Historical | Unqualified | 278.100 us | 124.845 us | 2.23x | 126.792 us | 359.547 us | 936,224 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | Historical | Unqualified | 299.098 us | 359.654 us | 0.83x | 359.520 us | 335.570 us | 785,704 B |
| `findall/unicode-full-strings` | `FindAllStrings` | Historical | Unqualified | 45.029 us | 21.572 us | 2.09x | 24.031 us | 42.099 us | 24,600 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | Historical | Unqualified | 58.445 us | 38.623 us | 1.51x | 47.145 us | 60.234 us | 24,600 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | Historical | Unqualified | 173.765 us | 3,466.165 us | 0.05x | 3,139.237 us | 153.941 us | 406,736 B |
| `literal/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 0.036 us | 0.209 us | 0.17x | 0.245 us | 0.094 us | 0 B |
| `literal/ismatch` | `IsMatch` | Historical | Unqualified | 14.562 us | 12.284 us | 1.19x | 14.877 us | 12.075 us | 131,512 B |
| `literal/search` | `Search` | ConsumedGroupZeroRanges | Inconclusive | 1.959 us | 12.535 us | 0.15x | 14.380 us | 12.183 us | 0 B |
| `literal/search-miss` | `Search` | Historical | Unqualified | 14.456 us | 12.207 us | 1.18x | 14.464 us | 10.417 us | 131,096 B |
| `prefix/match` | `Match` | Historical | Unqualified | 1.626 us | 0.170 us | 9.59x | 0.781 us | 1.444 us | 33,024 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | Historical | Unqualified | 214.404 us | 9,837.727 us | 0.02x | 9,648.155 us | 181.239 us | 878,784 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | Historical | Unqualified | 209.053 us | 10,068.573 us | 0.02x | 9,560.736 us | 179.957 us | 863,360 B |
| `replacement/fixed-string` | `ReplaceString` | Historical | Unqualified | 158.222 us | 112.937 us | 1.40x | 116.107 us | 157.065 us | 147,938 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | Historical | Unqualified | 159.901 us | 122.880 us | 1.30x | 126.579 us | 162.701 us | 188,922 B |
| `replacement/subn-string` | `SubnString` | Historical | Unqualified | 127.518 us | 62.295 us | 2.05x | 64.130 us | 121.769 us | 74,209 B |
| `replacement/subn-utf8` | `SubnUtf8` | Historical | Unqualified | 130.351 us | 61.397 us | 2.12x | 62.087 us | 120.898 us | 94,713 B |
| `split/captures` | `SplitStrings` | Historical | Unqualified | 135.959 us | 142.920 us | 0.95x | 142.175 us | 134.880 us | 499,032 B |
| `split/no-captures` | `SplitStrings` | Historical | Unqualified | 73.508 us | 128.601 us | 0.57x | 126.317 us | 74.816 us | 171,328 B |
| `unicode/count` | `Count` | Historical | Unqualified | 92.217 us | 211.684 us | 0.44x | 271.193 us | 185.111 us | 0 B |
| `unicode/fullmatch` | `FullMatch` | ConsumedGroupZeroRanges | Managed faster | 12.936 us | 13.967 us | 0.93x | 19.217 us | 25.377 us | 14,360 B |
| `zero-width/count` | `Count` | Historical | Unqualified | 18.474 us | 584.444 us | 0.03x | 578.748 us | 295.137 us | 34,840 B |

## Operation ownership and managed route

These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.

| Case | CPython operation owner | Managed route | Byte control / engine evidence |
|---|---|---|---|
| `capture/search-detailed` | `_sre C Pattern.search + Python detailed projection` | `Utf8Regex/FallbackRegex; detailed capture projection` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `class-run/count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/FallbackRegex; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `family/count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactUtf8Literals; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/full-strings` | `_sre C Pattern.findall` | `Utf8Regex/FallbackRegex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/full-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `Utf8Regex/FallbackRegex; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/many-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/many-capture-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `strict UTF-8 decode; .NET Regex; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/one-capture-strings` | `_sre C Pattern.findall` | `strict UTF-8 decode; .NET Regex; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-capture-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `strict UTF-8 decode; .NET Regex; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-full-strings` | `_sre C Pattern.findall` | `Utf8Regex/ExactUtf8Literal; findall string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `findall/unicode-full-utf8` | `_sre C Pattern.findall + Python UTF-8 projection` | `Utf8Regex/ExactUtf8Literal; findall UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `iteration/finditer-detailed` | `_sre scanner + Python detailed projection` | `strict UTF-8 decode; .NET Regex; detailed iteration shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `literal/fullmatch` | `_sre C Pattern.fullmatch` | `Utf8Regex/AsciiSimplePattern; full-match value ranges` | Rbyte 0.15x [0.15, 0.16]; ManagedFaster |
| `literal/ismatch` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; boolean result` | Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical. |
| `literal/search` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Rbyte 0.15x [0.15, 0.16]; Inconclusive |
| `literal/search-miss` | `_sre C Pattern.search` | `Utf8Regex/ExactAsciiLiteral; value ranges` | Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical. |
| `prefix/match` | `_sre C Pattern.match` | `Utf8Regex/FallbackRegex; anchored value ranges` | Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical. |
| `replacement/evaluator-string` | `_sre C Pattern.subn + Python callback` | `strict UTF-8 decode; .NET Regex callback replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/evaluator-utf8` | `_sre C Pattern.subn + Python callback` | `strict UTF-8 decode; .NET Regex callback replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/fixed-string` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/fixed-utf8` | `_sre C Pattern.sub` | `strict UTF-8 decode; .NET Regex replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/subn-string` | `_sre C Pattern.subn` | `strict UTF-8 decode; .NET Regex replacement; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `replacement/subn-utf8` | `_sre C Pattern.subn` | `strict UTF-8 decode; .NET Regex replacement; UTF-8 shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/captures` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `split/no-captures` | `_sre C Pattern.split` | `strict UTF-8 decode; .NET Regex split; string shaping` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `unicode/count` | `_sre scanner + Python finditer/sum` | `Utf8Regex/ExactUtf8Literal; Python-style count progression` | Excluded: the first byte-control profile is limited to one-shot matching operations. |
| `unicode/fullmatch` | `_sre C Pattern.fullmatch` | `strict UTF-8 decode; .NET Regex; full-match value ranges` | Excluded: pattern or subject is not entirely ASCII. |
| `zero-width/count` | `_sre scanner + Python finditer/sum` | `strict UTF-8 decode; adapter ASCII-boundary loop; scalar result` | Excluded: the first byte-control profile is limited to one-shot matching operations. |

## Reproduce and refresh

Run from the repository root in `Release` through `./bench.ps1`:

```powershell
./bench.ps1 -CommandArgs "--measure-pythonre-paired-case","literal/search","9"
./bench.ps1 -CommandArgs "--measure-pythonre-paired-case-reversed","literal/search","9"
./bench.ps1 -CommandArgs "--qualify-pythonre-case","literal/search","9"
./bench.ps1 -CommandArgs "--measure-pythonre-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmark-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmarks","200","7"
./bench.ps1 -CommandArgs "--verify-pythonre-benchmark-markdown"
```

The benchmark catalog and projection logic live in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).
