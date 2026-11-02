# Explicit default constructor branch rejected

**Closed; fully reverted.** The trial selected the existing three-argument
invocation constructor when PythonRe had no caller-funded program; funded calls
kept their existing four-argument constructor and allowance. This changed no
contract, state, algorithm or metering. It did not produce the predeclared useful
native gain. No variant or confirmation was attempted.

One quiet-VM .NET 11 RC3/CPU2 comparison completed 16 public cases in **74.877s**
under a 90-second cap. Separate contexts load fixed original `a40bda03`, parent
`0ab395a9`, and parent plus the archived patch; current Core DLL bytes match.
Two equal-result diagnostics use predecoded/decode-then compiled Regex. Warmup
is 500ms/minimum 128 calls per lane, independently calibrated 60ms batches,
five alternating samples, minimum **47.519ms**. Full range/count digests are
consumed after timing; runtime startup precedes in-process affinity.

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Native Search hit |0.086|0.093|+7.74%|
| Native Search miss |0.078|0.086|+10.18%|
| Native FullMatch hit |0.082|0.093|+13.19%|
| Native FullMatch miss |0.081|0.091|+12.55%|
| Folded FullMatch |82.314|81.271|-1.27%|
| Suffixed FullMatch |36.535|36.236|-0.82%|
| Lazy short FullMatch |0.644|0.650|+1.00%|
| Nullable atomic Search |1.144|1.123|-1.82%|
| Unchanged native Count |0.115|0.114|-0.90%|

All allocation medians are unchanged. Native regressions have tight spreads;
there is no basis for a noise confirmation. Other Unicode/offset/finite/live/
deadline/repeat controls and simultaneous original ratios remain in `summary.json`.
This subset does not replace the failed complete dev.11 gate. The original has
no counterpart for the new funding API; its contracts retain functional coverage,
without presenting an unfunded original as a funded cost comparator.

Generated public FullMatch caller code grew from 2,894 to 3,975 bytes and its main
frame from 2,056 to 3,240 bytes. That accompanies the negative public result;
it does not isolate the precise cause. No annotation or alternate layout is kept.
Complete Release PythonRe passes **4,583 per runtime** on .NET 10 and 11, including
funded close/overlap/admission/aliases, strict UTF-8, scalars, captures and original
tokens/deadlines. No package, platform, catalog or complete gate was rerun.

Raw driver/protocol, patch, three DLL/PDB sets, codegen and all samples/logs:
`artifacts/experiments/python-default-invocation-20261102/evidence.tar.gz`, SHA256
`B37D8764FB1417ACA78C8B6B62DA67B1C74455B9315FE9DB580F7062E8AA0FF6`.
`correctness.zip`: SHA256 `A13E5764AD4CD93DA2A731121D3F36BEF1B53DA2AEDD6C5FC9AF79A26940DA8C`.

Next work traces redundant phase-boundary checkpoints while preserving actual
validation/search work, bounded interruption and final publication checks.
Published/staged packages remain immutable.
