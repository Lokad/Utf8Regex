# Current one-capture output: CPU attribution

One15-second CPU capture on source4b9b80d7, .NET11.0.0, actual public
FindAllToStrings: `item-([0-9]+)`,17408 ASCII bytes,2048 captured strings.
PythonRe consumes existing valid unchanged UTF8. Core834162df/Pythonae463cb4
DLL/PDB/source pairs are reused and verified; only the disposable caller builds.
Normal8CPU startup, then internal affinity selectsCPU3; default tiering/PGO.
Warm3151 calls, then45632 calls over15026.974ms.
Complete outputs are checked outside64-call batches. No throughput comparison,
source prototype, producer build, package or Full refresh.

| Optimized containing method | Samples | Share of all samples |
|---|---:|---:|
| TryFindExactLiteralFamilyCandidate | 4406 | 25.67% |
| TryRunTerminalCaptureRoot | 2380 | 13.87% |
| MoveNextCore | 1596 | 9.30% |
| DecodeUtf8 | 1081 | 6.30% |
| RunClassRepeat | 1075 | 6.26% |
| FindAllScalarStrings | 1046 | 6.09% |
| SeekCandidate | 786 | 4.58% |

17164 cpu-clock:u/997Hz samples over17.153s include
startup, warmup, checking, JIT, GC and runtime work. 14317 map to
managed code;2847 are native/unresolved, with zero
ambiguous non-stub map ranges. Unresolved CoreCLR is not automatically GC.
Inlined work belongs to its containing method. These are not exact phase costs,
causal CPU percentages, a public speedup or an optimization ceiling.

Source inspection confirms ASCII class repeats already use checked bounded BCL
range search. The live exact-family hint has one prepared shared-prefix bucket
and unique discriminator, but currently finds a first byte and then compares
the full selected needle. Next bounded source hypothesis: use the existing
shared prefix for complete-prefix span search and compare only the unproved
tail, preserving candidate selection and original interruption. No new cache,
descriptor, mode or capture compiler. False heads/columns, long prefixes,
Unicode byte cuts and bounded public controls must decide promotion.

Core DLL `D53BA28BB0150A7B5D0E1C71B5363813AD152E7BD1D01271EB9E285FEC3DDD9B`, Python `7282AD25539642D3D87642C09DBBC23855BE75B30F3CB563AE9F2E1191601023`,
caller `2602E673F1EBE31DE06EB798B0A1094114FDC6097CFF27134EBB3568E9640451`. Raw perf/JIT maps, frozen binaries and sources
verified offVM before keeper release; SHA256 `3F75154FBFF7DC3EDB1362F183B5204CD957AB013BA882E0F810A3F83AE10CDB`.
Raw: `artifacts/experiments/python-one-capture-cpu-20261114/`.
