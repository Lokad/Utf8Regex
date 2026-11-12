# Current captured Python output: CPU attribution

One diagnostic capture after `47c8eb2a`, using its already measured Core pair and
the unchanged dev.74 Python pair. Reuse the archived public caller byte-for-byte;
no producer/caller build, source prototype, throughput comparison or package refresh.
This supersedes older CPU attribution for selecting the next cause, without
changing any historical benchmark observations or optimization decisions.

The caller runs `FindAllToStrings` for `([a-z]+)-([0-9]+)` over 18432 ASCII bytes,
with AssumeValid's caller-guaranteed valid unchanged UTF-8 contract. Each result
has 2048 two-value tuples. Complete values are checked after each 64-call batch.
There are 1727 warm calls and 19456 profiled calls in 15.040 seconds; this is not
a public timing measurement. Runtime .NET11.0.0, normal eight-CPU startup followed
by CPU2 affinity, default tiering/PGO, cpu-clock:u at 997Hz, 30-second outer cap.

| Flat sample location | Samples | Share |
|---|---:|---:|
| RunSegment |5122|30.27%|
| MoveNextCore |1465|8.66%|
| TryRunClassRepeatWithoutRewind |1313|7.76%|
| RunClassRepeat |1174|6.94%|
| DecodeUtf8 |1023|6.05%|
| FindAllScalarStrings |692|4.09%|
| SeekCandidate |534|3.16%|
| AllocateArray |346|2.04%|
| ChargeResultBytes |240|1.42%|
| TryChargeResultBytes |211|1.25%|

16921 samples span 16.851 seconds, including startup, warmup, checks, JIT, GC and
runtime work. 13729 map to managed code, 3192 are native/unresolved, with no
ambiguous non-stub map attribution. Unresolved CoreCLR alone accounts for 14.25%.
Inlined work belongs to its containing method. These are flat diagnostic samples,
not phase durations, an exact CPU breakdown, a causal proof or a speedup ceiling.

The captured JIT dump locates 1451 RunSegment samples at instruction-field loading
and 1003 at the switch comparison; only 118 hit the first entry-clear store.
This supports investigating shared instruction dispatch before more string-decoder
work. It does not establish that either particular instruction caused those
cycles, justify replaying closed readonly-binding/argument/dispatch trials, or
support another automatic constructor/outlining campaign. The actual captured
Tier1 method is 9750 native bytes; its call context differs from the failure-exit
gate's 10175-byte method. Do not compose those diagnostic sizes into a new gain.

Loaded/staged Core SHA256 is
`5ED67903CA9A5316D730F9F603BCA8913DDC98E293F0F4A4CEEA0AD6824B0464`,
Python `7ECE2912CF3078A63F2AA09B22C0B9F9A5D436A392F12B512962A43EEBA3C27F`,
caller `62DF0FD90719AF2E7F3AD503C3489F1C430ECBB694F01F5B8A45313119C7BCD1`.
DLL/PDB and caller manifests verify before/after. Producer source/PDB identities
are the pairs already verified by the [failure-exit gate](../SharedVmFailureExit-20261112/README.md).
All 452 immutable staged assets remain untouched.

Canonical offVM evidence under artifacts/experiments/python-current-vm-20261112:

- Complete raw: `11F38DD9D6B7E663EF96291E10FABCCF033A86C916796028C42E2EFD508D5979`.
- Native-code analysis: `7549EF1F274E8F6321CDB556C25AABBCF9E6A22E9E9B73610D20FC1318852300`.
- Initial export: `ECA59796BC3439300B0ADBC4F56097A1AFA6B26ADC6269225550AB84A1207A3D`.

The public capture succeeded. Initial export failed reading its root-owned JIT
dump; the completed export reads that exact PID's existing file through sudo tar.
Both archives are preserved and hash verified. No workload replay, check rerun or
raw-archive mutation. The initial export/keeper ends 1, the completion ends 0;
the keeper was released only after both archives were verified offVM.

Broader Python output, strict Core validation and general POSIX history costs
remain open. The next source proposal needs a distinct live mechanism and a
bounded public-operation decision; sampling alone is insufficient to promote it.
