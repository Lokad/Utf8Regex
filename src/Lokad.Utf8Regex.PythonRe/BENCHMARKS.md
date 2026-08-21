<!-- This file is generated from ../../PythonRe.Benchmarks.json. Do not edit benchmark rows by hand. -->

# Lokad.Utf8Regex.PythonRe benchmarks

This page is the self-contained performance regression snapshot for the optional Python `re` adapter. It is not a second regex-engine scoreboard: compatible patterns intentionally reuse the managed `Utf8Regex` core, while this catalog measures the Python-facing operation and its required result shaping. The source of truth is [`PythonRe.Benchmarks.json`](../../PythonRe.Benchmarks.json); `--refresh-pythonre-benchmarks` and `--refresh-pythonre-benchmark-case` update the JSON and regenerate this page.

The comparison is deliberately limited to cases where the requested work has equivalent managed semantics:

- `PythonRe`: `Utf8PythonRegex` over UTF-8 input.
- `.NET predecoded`: `System.Text.RegularExpressions.Regex` over an already-decoded `string`.
- `.NET + decode`: strict UTF-8 decoding on every operation followed by `.NET Regex`; this is retained as managed-core context.

Enumeration, split, and replacement rows include the result materialization needed by the public operation. This legacy schema-2 snapshot predates the direct CPython baseline; the next complete refresh migrates it to schema 3 with official CPython `re` measurements. Predecoded columns are matcher/runtime lower bounds, not end-to-end parity requirements.

## Snapshot summary

- Generated: `2026-08-17T11:00:40.6037232+00:00`
- Snapshot SHA-256: `A27411462F0EF822DEE064F02924DA94307D16AE6B5B0EAB73168C7192CD0A9D`
- Cases: `28`
- At or below the decode-then-.NET median: `12/28`
- Measurement environments represented: `1`
- Corpus: [`tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json`](../../tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json) (`9` vectors, SHA-256 `0A77376F84956A732A5B5F5D36EA884347FCBA3704DA32D4ED3F6AAFD2554E8B`)
- Corpus provenance limitation: The original upstream CPython version was not recorded; do not infer one from local vector names.

Measured from source `9f3e6bcfb29d` on .NET 10.0.8, Microsoft Windows 10.0.26200, Intel64 Family 6 Model 183 Stepping 1, GenuineIntel.

## Results

`vs decode` is `PythonRe / .NET + decode`; lower is better, and `1.00x` is exact median parity. Times are medians in microseconds per public operation.

| Case | Operation | Input | PythonRe CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | PythonRe alloc | .NET + decode alloc |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `capture/search-detailed` | `SearchDetailed` | 22 B | 0.651 us | 0.183 us | 0.191 us | 3.41x | 896 B | 824 B |
| `class-run/count` | `Count` | 86,016 B | 450.602 us | 413.259 us | 433.524 us | 1.04x | 172,056 B | 172,056 B |
| `family/count` | `Count` | 86,016 B | 64.907 us | 453.454 us | 480.275 us | 0.14x | 0 B | 172,056 B |
| `findall/full-strings` | `FindAllStrings` | 21,504 B | 224.116 us | 253.827 us | 254.485 us | 0.88x | 165,936 B | 936,640 B |
| `findall/full-utf8` | `FindAllUtf8` | 21,504 B | 197.810 us | 264.049 us | 270.536 us | 0.73x | 165,936 B | 1,059,584 B |
| `findall/many-capture-strings` | `FindAllStrings` | 18,432 B | 379.918 us | 393.020 us | 430.323 us | 0.88x | 1,331,488 B | 1,364,624 B |
| `findall/many-capture-utf8` | `FindAllUtf8` | 18,432 B | 392.339 us | 473.517 us | 465.959 us | 0.84x | 1,528,096 B | 1,594,064 B |
| `findall/one-capture-strings` | `FindAllStrings` | 17,408 B | 222.569 us | 214.185 us | 231.229 us | 0.96x | 936,224 B | 969,360 B |
| `findall/unicode-capture-utf8` | `FindAllUtf8` | 11,264 B | 223.301 us | 207.336 us | 225.052 us | 0.99x | 785,704 B | 788,128 B |
| `findall/unicode-full-strings` | `FindAllStrings` | 15,360 B | 48.156 us | 31.913 us | 41.276 us | 1.17x | 24,600 B | 165,424 B |
| `findall/unicode-full-utf8` | `FindAllUtf8` | 15,360 B | 41.980 us | 36.101 us | 48.124 us | 0.87x | 24,600 B | 190,064 B |
| `iteration/finditer-detailed` | `FindIterDetailed` | 4,608 B | 103.726 us | 94.932 us | 93.609 us | 1.11x | 406,736 B | 406,768 B |
| `literal/fullmatch` | `FullMatch` | 64 B | 0.183 us | 0.072 us | 0.091 us | 2.00x | 0 B | 360 B |
| `literal/ismatch` | `IsMatch` | 65,542 B | 39.597 us | 3.417 us | 34.374 us | 1.15x | 131,320 B | 131,112 B |
| `literal/search` | `Search` | 65,542 B | 38.473 us | 3.089 us | 33.554 us | 1.15x | 131,320 B | 131,320 B |
| `literal/search-miss` | `Search` | 65,536 B | 35.252 us | 3.400 us | 31.899 us | 1.11x | 131,096 B | 131,096 B |
| `prefix/match` | `Match` | 16,397 B | 1.628 us | 0.062 us | 1.396 us | 1.17x | 33,024 B | 33,024 B |
| `replacement/evaluator-string` | `SubnEvaluatorString` | 6,144 B | 207.179 us | 185.138 us | 186.871 us | 1.11x | 878,784 B | 891,096 B |
| `replacement/evaluator-utf8` | `SubnEvaluatorUtf8` | 6,144 B | 203.500 us | 172.483 us | 175.376 us | 1.16x | 863,360 B | 900,336 B |
| `replacement/fixed-string` | `ReplaceString` | 32,768 B | 153.702 us | 148.711 us | 153.355 us | 1.00x | 147,938 B | 147,506 B |
| `replacement/fixed-utf8` | `ReplaceUtf8` | 32,768 B | 156.055 us | 155.948 us | 151.992 us | 1.03x | 188,922 B | 188,490 B |
| `replacement/subn-string` | `SubnString` | 16,384 B | 131.104 us | 117.984 us | 118.892 us | 1.10x | 74,209 B | 499,928 B |
| `replacement/subn-utf8` | `SubnUtf8` | 16,384 B | 126.606 us | 126.389 us | 121.792 us | 1.04x | 94,713 B | 520,432 B |
| `split/captures` | `SplitStrings` | 11,776 B | 132.095 us | 130.206 us | 133.364 us | 0.99x | 499,032 B | 499,032 B |
| `split/no-captures` | `SplitStrings` | 11,776 B | 70.239 us | 68.273 us | 70.803 us | 0.99x | 171,328 B | 171,328 B |
| `unicode/count` | `Count` | 122,880 B | 93.709 us | 100.445 us | 196.640 us | 0.48x | 0 B | 139,288 B |
| `unicode/fullmatch` | `FullMatch` | 13,312 B | 14.165 us | 7.603 us | 14.090 us | 1.01x | 14,360 B | 14,568 B |
| `zero-width/count` | `Count` | 17,408 B | 18.894 us | 295.879 us | 302.571 us | 0.06x | 34,840 B | 34,840 B |

## Reproduce and refresh

Run from the repository root in `Release` through `./bench.ps1`:

```powershell
./bench.ps1 -CommandArgs "--measure-pythonre-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmark-case","literal/search","200","7"
./bench.ps1 -CommandArgs "--refresh-pythonre-benchmarks","200","7"
./bench.ps1 -CommandArgs "--verify-pythonre-benchmark-markdown"
```

The benchmark catalog and projection logic live in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).
