# Reject an impossible Python anchored start before screening the subject

Match/FullMatch reuse the existing bounded necessary-start ASCII interval proof
before optional Core required-literal screening. Only a provably impossible byte
can finish here; possible starts retain screening and scalar verification.
Restrict this work to anchored operations with an existing Core preparation.
No new fields, stored analysis, engine, mode or unchecked access. The ordinary
Search/FindAll cursor is unchanged. Strict validation remains first; original
cancellation/deadline, captures, coordinates and admission remain authoritative.

**Scope matters.** The primary comparison used `(needle)`, whose original path
already lacked an expensive whole-input prefilter. It measures 64 B of avoided
scratch and small execution savings, not the intended whole-subject scan.
The earlier P1 case was actually `(?=bad)(bad)([0-9]+)`. A four-case supplement
qualifies that existing case using exactly the same frozen producers. Keep both
comparisons, their different callers, and all adverse controls.

| Existing lookahead case, 1 MiB miss | Validate before → after µs | AssumeValid before → after µs |
|---|---:|---:|
| Match |36.810 → 19.622 (-46.69%)|16.693 → 0.139 (-99.17%)|
| FullMatch |36.782 → 19.595 (-46.73%)|16.725 → 0.139 (-99.17%)|

Ranges are disjoint; allocation remains zero. Tiny successful calls of this same
lookahead pattern move +0.60%/+0.04%, overlapping, at unchanged 800 B. These are
complete public value operations copying requested byte/UTF16 coordinates;
results are checked after timing. Validate still pays its full subject pass.
This is producer evidence, not a CPython/native-parity result.

For the primary captured-literal cases, trusted misses improve 31–38%, with
64 B removed: 1 MiB Match0.196→0.135µs, FullMatch0.206→0.134µs. Finite/live
0.251→0.169µs and deadline/live0.332→0.223µs each drop64 B. Default 4 KiB
miss0.266→0.221µs and tiny miss0.220→0.158µs improve too. Preserve costs:
trusted tiny success+9.57% (+25ns, disjoint); strict+10.84% (+29ns, overlapping);
tiny captured FullMatch+6.16%/+6.17% (+28–29ns, disjoint); strict Unicode-fold
control+5.81% (+23ns, disjoint). Basic ambiguous-start screening still works
under finite limits; its small adverse timings overlap.

Dense FindAll moves+0.52% strict/+9.98% trusted with overlapping, bimodal ranges
and unchanged242008 B. No dense equivalence/gain claim. Strict Core controls move
+7.79%/+3.21% in the primary run, overlapping; supplemental controls+1.22%/-0.94%
overlap. Do not normalize them away. Accept the roughly17µs useful scan savings
and scratch-free misses with these explicit tiny-operation costs. Further
capture-heavy execution work remains on the roadmap.

Primary two-policy ordinary-process ABBA:155.221s,14 lanes,560 samples>=43.513ms.
Supplement:46.623s,4 lanes,160 samples>=125.405ms; same Core/Python DLL/PDB pairs.
Each comparison has a common caller and distinct Python producers; Core is
identical throughout. Eight CPUs at startup, CPU2 affinity afterward; no timing
runtime overrides. No producer/original caller changes after timing starts; no
timed result is discarded. Initial preflight expected a work refusal from the
small-budget probe but the baseline refused working capacity first; correcting
that assertion reused frozen graphs and occurred before timing. Both outcomes
and the original scope mistake remain in the archives. No further confirmation.

Windows full Python suite5067/target passes.NET10/11; after four targeted case
additions, final anchored26/target pass. Linux affected420/target pass, including
necessary-start/zero-width/atomic proofs, offsets, malformed suffixes, nullable/
folded captures, useful mandatory-literal rejection, finite work/working limits
and original interruption. SPEC-PYTHONRE records scratch-free anchored refusal
and route-dependent resource costs. There is no admission for unused VM buffers.

Baseline source8a37ec05, candidate plus archived production patch. Raw graphs,
PDBs, callers, protocols and every observation are verified offVM under
`artifacts/experiments/python-anchored-start-20261110/`:

- `evidence.tar.gz`: `FCC220C6B3670ED288046F2806B89F68E2F6FD8CAE932B8536F1311104D43169`.
- `scope-evidence.tar.gz`: `B4CB41A2817FDDE4FED7052B58B88211590C1007926E762E86714BD3BB173F10`.
- `linux-checks.tar.gz`: `3D47FFD74EEE82D4A3F205ECF955BEF9B9A54178D3574CFA35A41F0EC5D524AD`.
- Initial preflight: `62C9A876EB25D5F83C2BF7FA0226BA29BFA87F07153C77EF5B9CB210946F012C`.

Full derived summaries and Windows logs accompany them. No package/Full refresh,
ordinary table rewrite or change to immutable published/staged artifacts.
