# Benchmark Corpora

`Lokad.Utf8Regex` benchmarks use a local curated corpus catalog instead of importing an external benchmark suite.

The intent is to reproduce the *composition* of strong regex benchmark suites without taking a dependency on them:

- separate no-match scans from dense-match workloads
- include literal, alternation, lookaround, backreference, replacement, and right-to-left families
- include ASCII, BMP-heavy UTF-8, and supplementary-scalar text
- benchmark both native-success and explicit-fallback scenarios
- compare against two baselines:
  - `UTF-8 decode -> Regex`
  - predecoded `Regex`

Corpora currently live as deterministic generators in `bench/Lokad.Utf8Regex.Benchmarks/Utf8RegexBenchmarkCatalog.cs`.

If benchmark data later grows beyond what is convenient in source form, add checked-in UTF-8 corpus files here and keep them deterministic and license-clean.
