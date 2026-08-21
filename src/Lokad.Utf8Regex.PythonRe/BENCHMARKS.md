<!-- This file is generated from ../../PythonRe.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.PythonRe benchmarks

This page is the self-contained performance regression snapshot for the optional Python `re` adapter. It is not a second regex-engine scoreboard: compatible patterns intentionally reuse the managed `Utf8Regex` core, while this catalog measures the Python-facing operation and its required result shaping. The source of truth is [`PythonRe.Benchmarks.json`](../../PythonRe.Benchmarks.json); `--refresh-pythonre-benchmarks` and `--refresh-pythonre-benchmark-case` update the JSON and regenerate this page.

The comparison is deliberately limited to cases where the requested work has equivalent managed semantics:

- `PythonRe`: `Utf8PythonRegex` over UTF-8 input.
- `CPython predecoded`: the official CPython `re` engine over an already-decoded `str`.
- `CPython + decode`: strict UTF-8 decoding on every operation followed by the precompiled CPython `re.Pattern`; this is the primary Python-oracle baseline.
- `.NET predecoded`: `System.Text.RegularExpressions.Regex` over an already-decoded `string`.
- `.NET + decode`: strict UTF-8 decoding on every operation followed by `.NET Regex`; this is retained as managed-core context.

Enumeration, split, and replacement rows include the result materialization needed by the public operation. These rows answer whether Python-compatible API semantics add acceptable cost. CPython is measured inside its own long-lived process because `_sre` is a CPython core module, not a standalone engine API; interpreter startup and pattern compilation are excluded. Predecoded columns are matcher/runtime lower bounds, not end-to-end parity requirements. Use the PCRE2 page for the separate native PCRE2 engine comparison.

## Snapshot summary

- Generated: `2026-08-21T10:14:37.8911438+00:00`
- Snapshot SHA-256: `B773EA973311AD7069A3CAB91C86173DFDDE1A981F8EBF22D585B2115D429720`
- Cases: `28`
- At or below the decode-then-CPython median: `17/28`
- Measurement environments represented: `1`
- Corpus: [`tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json`](../../tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json) (`9` vectors, SHA-256 `0A77376F84956A732A5B5F5D36EA884347FCBA3704DA32D4ED3F6AAFD2554E8B`)
- Corpus provenance limitation: The original upstream CPython version was not recorded; do not infer one from local vector names.
- CPython environments represented: `1`
- CPython baseline: `CPython 3.13.2` at `C:\Python313\python.exe` on Windows-11-10.0.26200-SP0

Measured from source `9730cc32f96c` on .NET 10.0.11, Microsoft Windows 10.0.26200, Intel64 Family 6 Model 183 Stepping 1, GenuineIntel.

## Results

`vs CPython` is `PythonRe / CPython + decode`; lower is better, and `1.00x` is exact median parity. The CPython runner times inside the interpreter after startup and pattern compilation, and calibrates each CPython sample toward 100 ms up to the row's .NET iteration ceiling. Times are medians in microseconds per public operation.

| Case | Operation | Input | PythonRe CPU | CPython predecoded CPU | CPython + decode CPU | vs CPython | .NET + decode CPU | vs .NET | PythonRe alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | 22 B | 0.501 us | 2.489 us | 2.602 us | 0.19x | 0.350 us | 1.43x | 896 B |
| `class-run/count` | `Count` | 86,016 B | 504.865 us | 1,415.960 us | 1,424.645 us | 0.35x | 620.138 us | 0.81x | 172,056 B |
| `family/count` | `Count` | 86,016 B | 68.200 us | 1,240.842 us | 1,231.496 us | 0.06x | 462.747 us | 0.15x | 0 B |
| `findall/full-strings` | `FindAllStrings` | 21,504 B | 244.742 us | 303.417 us | 219.217 us | 1.12x | 248.829 us | 0.98x | 165,936 B |
| `findall/full-utf8` | `FindAllUtf8` | 21,504 B | 268.899 us | 309.247 us | 406.854 us | 0.66x | 318.232 us | 0.84x | 165,936 B |
| `findall/many-capture-strings` | `FindAllStrings` | 18,432 B | 388.206 us | 196.091 us | 201.521 us | 1.93x | 449.797 us | 0.86x | 1,331,488 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | 18,432 B | 412.906 us | 789.879 us | 873.741 us | 0.47x | 534.325 us | 0.77x | 1,528,096 B |
| `findall/one-capture-strings` | `FindAllStrings` | 17,408 B | 278.100 us | 124.845 us | 126.792 us | 2.19x | 359.547 us | 0.77x | 936,224 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 11,264 B | 299.098 us | 359.654 us | 359.520 us | 0.83x | 335.570 us | 0.89x | 785,704 B |
| `findall/unicode-full-strings` | `FindAllStrings` | 15,360 B | 45.029 us | 21.572 us | 24.031 us | 1.87x | 42.099 us | 1.07x | 24,600 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 15,360 B | 58.445 us | 38.623 us | 47.145 us | 1.24x | 60.234 us | 0.97x | 24,600 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | 4,608 B | 173.765 us | 3,466.165 us | 3,139.237 us | 0.06x | 153.941 us | 1.13x | 406,736 B |
| `literal/fullmatch` | `FullMatch` | 64 B | 0.195 us | 0.207 us | 0.245 us | 0.80x | 0.094 us | 2.08x | 0 B |
| `literal/ismatch` | `IsMatch` | 65,542 B | 14.562 us | 12.284 us | 14.877 us | 0.98x | 12.075 us | 1.21x | 131,512 B |
| `literal/search` | `Search` | 65,542 B | 12.860 us | 12.224 us | 14.380 us | 0.89x | 12.183 us | 1.06x | 131,564 B |
| `literal/search-miss` | `Search` | 65,536 B | 14.456 us | 12.207 us | 14.464 us | 1.00x | 10.417 us | 1.39x | 131,096 B |
| `prefix/match` | `Match` | 16,397 B | 1.626 us | 0.170 us | 0.781 us | 2.08x | 1.444 us | 1.13x | 33,024 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 6,144 B | 214.404 us | 9,837.727 us | 9,648.155 us | 0.02x | 181.239 us | 1.18x | 878,784 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 6,144 B | 209.053 us | 10,068.573 us | 9,560.736 us | 0.02x | 179.957 us | 1.16x | 863,360 B |
| `replacement/fixed-string` | `ReplaceString` | 32,768 B | 158.222 us | 112.937 us | 116.107 us | 1.36x | 157.065 us | 1.01x | 147,938 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 32,768 B | 159.901 us | 122.880 us | 126.579 us | 1.26x | 162.701 us | 0.98x | 188,922 B |
| `replacement/subn-string` | `SubnString` | 16,384 B | 127.518 us | 62.295 us | 64.130 us | 1.99x | 121.769 us | 1.05x | 74,209 B |
| `replacement/subn-utf8` | `SubnUtf8` | 16,384 B | 130.351 us | 61.397 us | 62.087 us | 2.10x | 120.898 us | 1.08x | 94,713 B |
| `split/captures` | `SplitStrings` | 11,776 B | 135.959 us | 142.920 us | 142.175 us | 0.96x | 134.880 us | 1.01x | 499,032 B |
| `split/no-captures` | `SplitStrings` | 11,776 B | 73.508 us | 128.601 us | 126.317 us | 0.58x | 74.816 us | 0.98x | 171,328 B |
| `unicode/count` | `Count` | 122,880 B | 92.217 us | 211.684 us | 271.193 us | 0.34x | 185.111 us | 0.50x | 0 B |
| `unicode/fullmatch` | `FullMatch` | 13,312 B | 27.265 us | 12.613 us | 19.217 us | 1.42x | 25.377 us | 1.07x | 14,360 B |
| `zero-width/count` | `Count` | 17,408 B | 18.474 us | 584.444 us | 578.748 us | 0.03x | 295.137 us | 0.06x | 34,840 B |

## Reproduce and refresh

Run from the repository root in `Release` through `./bench.ps1`:

```powershell
./bench.ps1 -CommandArgs "--measure-pythonre-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmark-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmarks","200","7"
./bench.ps1 -CommandArgs "--verify-pythonre-benchmark-markdown"
```

The benchmark catalog and projection logic live in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).
