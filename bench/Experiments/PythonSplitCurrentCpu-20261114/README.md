# Current captured-split CPU observation

Frozen dev86 source `06ef69e4`, .NET11GA, current source-built Core/Python DLL/PDBs.
Actual public PythonRe SplitToStrings, `([,;])`, the catalog's10752 valid UTF8
bytes (`alpha,beta;gamma,delta;` repeated512 times). Caller-valid unchanged text;
no subject-validation option/pass.2048 separator matches produce4097 complete
strings, checked after every64-call batch. Loaded images match staged hashes;
source/PDB checks and before/after producer/caller identity checks pass.

ONE cpu-clock:u capture at997Hz,1.5s warmup/15s workload/30s outer cap.17159
whole-process samples span17.146s, including startup, warmup, JIT, checks and
runtime work.10466 map to managed code,6693 native/unresolved; zero ambiguous
non-stub mappings. Inlined work belongs to its containing method.

| Sample location | Samples | Share of all samples |
|---|---:|---:|
| NATIVE/UNRESOLVED /home/vermorel/.dotnet/shared/Microsoft.NETCore.App/11.0.0/libcoreclr.so [unknown] | 4481 | 26.11% |
| DecodeUtf8 | 2015 | 11.74% |
| SplitScalar | 1635 | 9.53% |
| MoveNextCore | 1573 | 9.17% |
| NATIVE/UNRESOLVED /usr/lib/x86_64-linux-gnu/ld-linux-x86-64.so.2 __tls_get_addr | 1332 | 7.76% |
| SeekCandidate | 1066 | 6.21% |
| TryRunTerminalCaptureRoot | 1034 | 6.03% |
| TryPush | 494 | 2.88% |
| NATIVE/UNRESOLVED /home/vermorel/.dotnet/shared/Microsoft.NETCore.App/11.0.0/libclrjit.so [unknown] | 403 | 2.35% |
| TryChargeResultBytes | 377 | 2.20% |
| ChargeResultBytes | 370 | 2.16% |
| GetPointerToFirstInvalidByte | 340 | 1.98% |

Costs are distributed across text construction, split orchestration, cursor
progression, candidate search and capture roots. No new whole-input description
or validation issue is established. Split already gets selected byte ranges;
another map-removal/decoder framework is unsupported by this evidence.
26.11% unresolved CoreCLR is NOT automatically GC time. Native TLS samples do
not establish that diagnostics or cancellation checks caused them. These flat
samples are not phase durations, exact instruction costs or speedup ceilings.

Next bounded source hypothesis: unknown-length one-byte ASCII output can use
char.ToString instead of the generic UTF8 decoder, after existing conservative
admission and interruption checkpoints. Other lengths/non-ASCII/exact-known
decoding must keep their behavior. No cache or new fields. Focused correctness
and one complete public A/B with an unchanged non-string control must decide;
sampling alone does not promote it. No repeat CPU capture for this case.

Raw SHA256 `7345596B7C7B04FA11206F97C16D9D3DC90CCAAEA1126FF5E8A91EE6D5FF506E` verified offVM before keeper62385 terminal0, under
`artifacts/experiments/python-split-current-cpu-20261114/`.
[Acceptance](acceptance.json) retains loaded identities and all sample mappings.
Only a private diagnostic caller was built; no producer rebuild, package/Full
qualification, public throughput comparison or parity-table refresh occurred.
Qualified dev86 and all548 package/symbol bytes remain unchanged.
