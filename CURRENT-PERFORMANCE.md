# Selected current-source performance

The latest exact-package observation uses immutable Core/PythonRe scalar.100,
producer `fa0e260880caf40b3ba139f6e7fa196276d8c783`, on .NET 10.0.12. Existing
BenchmarkDotNet Short runs in-process to preserve the staged shipping DLLs.
Eight public lanes include both .NET baselines and all four finite Python limits.
Unicode Count averaged 53.21 µs / 0 B; Python tuples averaged 572.62 µs / 132,792 B
unlimited, 714.09 µs / 136,992 B finite and 690.32 µs / 136,997 B with a live token.
The finite mean difference is about 25%, with broad three-iteration confidence
intervals. It is not an overhead ceiling or source-regression claim. This protocol
differs from the earlier process-isolated observations below. [Full costs,
uncertainty, comparators and raw evidence](artifacts/core-python-scalar100-20261018/vm-cost/RESULT.md)
include a first archival failure whose measurements are excluded, followed by one
complete 49-second replacement. No further timing run or Status update follows.

## Earlier source measurements

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

## Python capture materialization with finite limits

Source `54318595b190abf4037a5a368ec25170814a7d63` adds a small fixture to the
existing BDN runner for catalog case `findall/unicode-capture-utf8`. Pattern
`(é+)-(𝒜𝒜|𝒜)` produces 1,024 two-capture tuples over 11 KiB of mixed-width UTF-8.
Setup checks every capture value for agreement. Timed operations return complete
owned UTF-8 tuples; constructors and correctness traversal are outside timing.
Both .NET comparators include UTF-8 tuple projection. This compares overlapping
results with composed .NET operations; it does not compare with CPython.

One Short run completed in 66.51 seconds on the same VM/runtime/toolchain:

| Operation | Mean | Allocated |
| --- | ---: | ---: |
| PythonRe, unlimited |625.2 µs|129.68 KiB|
| PythonRe, finite |650.5 µs|133.78 KiB|
| PythonRe, finite + live token |649.8 µs|133.78 KiB|
| Predecoded .NET + tuple projection |310.4 µs|692.34 KiB|
| Decode + .NET + tuple projection |322.6 µs|705.37 KiB|

Finite policy: 50 million work steps, depth 1,024, working capacity 64 MiB and
result capacity 128 MiB. The observed finite means are about 4% above unlimited,
with 4,200 additional bytes per operation. Three measured iterations give broad
confidence intervals: this is descriptive operating-cost evidence, not proof of
an overhead ceiling. The predecoded .NET lane varies particularly widely. Python
uses substantially less allocation here; the .NET timing means are lower.

[Raw samples, confidence intervals and scope](artifacts/bdn-python-findall-20261018/RESULT.md)
remain separate from exact-package99 evidence, closed private-loop campaigns,
CPython Status and historical snapshots. No second timing run was needed for
this coarse observation; no precise percentage or general performance claim is
promoted.

A subsequent mandatory Unicode prefix-search prototype was rejected and reverted:
its finite public calls slowed by 75–94%, while unchanged .NET controls stayed
within 6.2% of their original means. Allocations were unchanged. The accepted
source and measurements above remain current; [the bounded experiment receipt](artifacts/python-unicode-prefix-20261018/RESULT.md)
preserves the failed candidate and evidence.
