# Reuse projected character counts in Python Detailed captures

Parent 2323dbff, scalar.113-dev. SnapshotDetailed already projects each successful
capture's derived coordinate pair. Pass its difference to the existing GetText
helper instead of searching the boundary map twice again. The optional internal
count adds no public mode/API. Unknown-count consumers keep the existing
ASCII/whole-input/map/range-validation behavior. Both original GetText checkpoints,
decode admission/failure translation, exact work/result/working charges, scalar
captures, empty/unmatched groups, names/completion metadata, immutable text sharing
and owned snapshots remain. No helper, owner, matcher or unchecked API is added.

## One public pair, accepted and closed

Quiet Xeon8573C VM CPU2 / .NET 10.0.12 Release FullOpts/R2R-off. Only PythonRe DLL
changes; Core and driver are frozen. Full values and byte/derived-coordinate ranges
are checked untimed. Typed sinks retain timed outputs without boxing or full
result traversal. 120 ms warmup / 80 ms calibration per lane, five alternating sample
orders, 60 s child / 140 s outer caps. All 200 samples exceed 65.47 ms; all processes exit 0.
Quiet CPU2=0%, other observed dotnet tick deltas=0. No confirmation or more timing.

Public medians, unlimited / all four finite budgets:

| Operation | Unlimited before -> after | Finite before -> after |
| --- | --- | --- |
| Mixed Detailed,128 matches |224.541->182.149us (-18.9%)|232.926->193.898us (-16.8%)|
| Mixed Detailed,2048 matches |3.683->3.067ms (-16.7%)|3.817->3.207ms (-16.0%)|
| Mixed Search,32B |2.042->1.961us (-4.0%)|2.076->2.006us (-3.4%)|
| Mixed Search,128KiB |579.345->581.255us (+0.3%)|596.131->593.618us (-0.4%)|
| ASCII Detailed,128 matches |109.576->114.104us (+4.1%)|123.443->126.108us (+2.2%)|
| ASCII Detailed,2048 matches |1.753->1.814ms (+3.5%)|1.941->1.993ms (+2.7%)|
| ASCII Search,32B |1.388->1.495us (+7.7%)|1.542->1.581us (+2.5%)|

Accept the scoped tradeoff: mixed enumeration improves in all four lanes; ASCII
medians fit the predeclared allowance. This is not a universal 10% ceiling,
default-PGO/package/whole-engine result or CPython Status. Tiny unlimited ASCII
has 1.172x sample spread. Unchanged Core controls, compiled/warmed first, stay within
0.7%; .NET predecoded +2.7%, decoded -3.8% (1.302x spread). Unknown-count FindAll
neighbor -0.8% / +1.0%; its helper's new argument branch is part of that observation.

Acquisition behavior/modeled charges remain. Main allocation medians are identical:
2048 mixed results 728880 B unlimited / 740823.25 B finite;128 mixed 42312 B unlimited.
Finite128 varies fractionally 48262.04->48265.92 B; neighboring pooled-result rows
also have fractional CLR variation. Keep those observations, not a byte-exact
allocation claim. [Every raw sample and summary](measurement.json).

## Correctness, code generation and identities

Focused Release PythonRe 587+190 pass, no failures/skips. Existing independent
Detailed-versus-ordinary-GetText tests still pass at every work cutoff and compare
exact result/working extents. Other witnesses cover mixed-width/supplementary/
lookbehind/empty/unmatched captures, original cancellation/deadline during decode,
sticky denial, parent loans and retained snapshots after disposal/input mutation.
No implementation-mirroring tests or per-edit package matrix are added.

Windows FullOpts SnapshotDetailed 570->592 B / GetText 493->512 B; Linux 547->620 B /
416->440 B. Required map calls remain and the supplied-count branch bypasses the
helper's pair. Raw patches/JIT/tests/preflights remain in
`artifacts/python-detailed-coordinate-reuse-20261020/`. Earlier closed trials,
immutable 112/older and PCRE94, handoffs/pins, snapshots and Status stay unchanged.

SHA256 identities:

- Core: 9A9E17F3255CAB8E8973961991BA01910C9FEC5EB6D1E235516674A0B2B4A534.
- Before PythonRe: 5C8616AF2C71682206A320273251A2B0E8B1FB23C69E1A1CE5BEC6191416E0CC.
- After PythonRe: A1ED10B26323ABA01268D87D01BC879719DC0F5A84B4DAE1072C0552995DDEF9.
- Driver: 1F997106AB00A460F03C601C88B88B74815C758A17FAFDBDD81EA88F64A1589C.
- Downloaded archive: 109140CE65DC1D58BA656E99F6F6630275477365DA5A18E026AF9749E3AFF844.

Frozen driver uses CoreLibraryPath/PythonLibraryPath overrides to reference DLLs
without rebuilding libraries. Run `--preflight <new.jsonl>` or one new output path
for measurement. CreateNew preserves receipts. This documents reproducibility,
not a new framework or an instruction to repeat the closed pair.

Subsequent source qualification rejects the default-valued parameter (repository
ceiling0). The correction supplies explicit null at the unknown-count caller and
removes the parameter default. Every one of1060 executable Python method bodies,
signatures, local signatures, stack bounds and exception regions matches this
measured candidate; parameter Optional/HasDefault attributes change4112->0.
The guard is preserved,113 failed package bytes remain intact and corrected114
has its own identity. No matching code or numeric contract changes and no repeated
performance pair. Exact proof:artifacts/python-detailed-coordinate-reuse-20261020/
explicit-count-il-proof.json. This does not qualify114 package bytes by itself.
