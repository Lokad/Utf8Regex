# Current many-capture output: CPU attribution

One diagnostic capture on the committed 20174326 C# body, reusing the exact Core/
Python pairs from the accepted captured-class comparison and the unchanged
archived public caller. No producer/caller build, source prototype, throughput
comparison or package qualification. The private Core DLL still reports its
c3bea6e1 compilation revision; its Windows-CRLF inputs and PDB/source checks are
preserved in that comparison. Git's LF normalization changes no C# behavior.

Actual `FindAllToStrings`, `([a-z]+)-([0-9]+)`, 18432 ASCII bytes, 2048 two-value
tuples. AssumeValid trusts valid unchanged UTF-8. There are 2405 warm calls and
30016 profiled calls over 15.017s, with complete checks after each 64-call batch.
.NET 11.0.0, normal eight-CPU startup then CPU2, default tiering/PGO, cpu-clock:u
at 997Hz, 30-second outer cap. This is not a public timing measurement.

| Optimized Tier1 sample location | Samples | Share of all samples |
|---|---:|---:|
| Complete capture-root helper |2397|14.20%|
| MoveNextCore |1898|11.24%|
| RunClassRepeat |1817|10.76%|
| DecodeUtf8 |1411|8.36%|
| FindAllScalarStrings |1123|6.65%|
| SeekCandidate |706|4.18%|
| Result-array allocation |522|3.09%|

16885 samples span 16.832s, including startup, warmup, checksum, JIT, GC and runtime
work. 12358 map to managed code; 4527 are native/unresolved, with no ambiguous
non-stub mapping. Unresolved CoreCLR accounts for 21.09%; it is not automatically
GC attribution. Inlined work belongs to its containing method. Flat locations
are not phase durations, causal instruction costs or speedup ceilings.

**RunSegment has zero samples** on this now-proved complete root. The earlier
30.27% RunSegment attribution predates that optimization and must not drive more
general VM-dispatch trials for this workload. Source inspection also confirms
ASCII interval bounds already reside in the existing instruction operand: range
decoding is shifts/masks, not another mask scan requiring cached metadata.

Next distinct source hypothesis: complete capture roots do not read repeat
counts, repeat positions or balancing heads. Investigate deferring their repeated
initialization until general VM execution, keeping capture-slot clearing,
workspace admission, interruption and lifetime contracts. Sampling alone does
not promote that change; one bounded public comparison must decide it.

Loaded Core 4200A9FA18B4960331B6C8E3112A08F0FCB4D8C2DAE8928417386AFF823A3733,
Python DA8FE78E231929B3F30FA8330507A69E0E9DACD88927B863C8375230070D016B,
caller 62DF0FD90719AF2E7F3AD503C3489F1C430ECBB694F01F5B8A45313119C7BCD1.
Staged/loaded hashes and frozen pairs verify before/after. No package bytes change.
Raw artifacts/experiments/python-post77-cpu-20261112/evidence.tar.gz:
93A2C691FDFAF7E1D686F8EA329568F7B0C72C1C3FC430A70B98919C7EE6C392.
Evidence is verified offVM before keeper release; the capture is not a parity claim.
