# Selected current-source performance

On 2026-10-18, the existing BenchmarkDotNet Short job measured public Count on
24 KiB of UTF-8 containing 4,096 `café` matches. Source revision was
`118d471311d74e7a5aa88b97495a370a0b731095`. All three public results agree during
setup. This BMP fixture has overlapping scalar and .NET code-unit semantics.

Core averaged 50.15 µs with no observed managed allocation, versus 157.88 µs
for predecoded .NET and 286.41 µs / 40,984 B for decode-then-.NET. These are
approximately 3.15× and 5.71× throughput ratios on this selected dense workload.
The Short job has only three measured iterations: its 99.9% confidence intervals
are broad, although Core's interval does not overlap either comparator's.
This establishes a useful current disposition for this family, not a general
performance claim or a source-change speedup.

The dedicated VM ran .NET 10.0.12 on one logical processor, with ordinary runtime
defaults and BenchmarkDotNet's pilot, three warmup iterations and three measured
iterations. Its generated worker was compiled with the installed .NET 11 RC1 SDK
and targeted .NET 10. This is neither a .NET 11 runtime comparison nor an exact
package99 measurement. Constructors are outside the timed operations; Count
includes the required strict UTF-8 validation. Public budgets are unlimited here;
finite-limit overhead and Python result shaping are outside this observation.

The generated report below has trailing whitespace normalized. Raw log, samples, setup failures,
input identity and source/toolchain evidence are preserved in
[the experiment receipt](artifacts/bdn-multilingual-count-20261018/RESULT.md).
No README snapshot row or historical Status was rewritten. Qualified private
Core/PythonRe99 and its partial finite/current cost evidence remain separate.

## Original BenchmarkDotNet report

```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
INTEL XEON PLATINUM 8573C 2.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100-rc.1.26425.128
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method          | CaseId               | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------- |--------------------- |----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| Utf8Regex       | count(...)teral [26] |  50.15 μs | 12.153 μs | 0.666 μs |  1.00 |    0.02 |      - |         - |          NA |
| DecodeThenRegex | count(...)teral [26] | 286.41 μs | 14.291 μs | 0.783 μs |  5.71 |    0.07 | 0.4883 |   40984 B |          NA |
| PredecodedRegex | count(...)teral [26] | 157.88 μs |  7.215 μs | 0.395 μs |  3.15 |    0.04 |      - |         - |          NA |
