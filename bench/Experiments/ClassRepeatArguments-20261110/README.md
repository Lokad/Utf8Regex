# Reuse loaded class-repeat instructions — rejected

**Rejected; production restored.** Following the [actual CPU sample](../PythonCaptureProfile-20261110/README.md),
pass the repeat/atom instructions already loaded by the commit proof into the
existing RunClassRepeat helper by readonly reference. The terminal-root caller
supplies its same two instructions. Remove the helper's duplicate array reads;
change no algorithm, loop, allocation, owner, state, mode, resource charge or
public contract. This differs from the closed RunSegment readonly-binding trial.

One **79.306s** public-process ABBA on the quiet .NET11 RC3 VM, within180s:
20 lanes/400 samples, minimum28.144ms. Same Python/caller, distinct Core DLL/PDBs;
helper-signature preflight confirms the change. Normal8CPU startup then affinity4,
default tiering/PGO,500ms warmup and independently calibrated75ms/five samples.
Full tuples/strings and funded parent loans are checked outside timing.

| Public operation | Before (µs) | Trial (µs) | Change |
|---|---:|---:|---:|
| Many-capture strings |725.15|762.00|+5.08%|
| Same, finite/live |1108.35|1134.94|+2.40%|
| Same, funded/live |1233.80|1217.25|−1.34%|
| Same, deadline/live |1641.51|1677.48|+2.19%|
| Captured Count |416.54|415.83|−0.17%|
| Range cursor |700.60|699.46|−0.16%|
| Range cursor, finite/live |926.93|962.61|+3.85%|
| One-capture strings |627.54|493.54|−21.35%, bimodal|
| One-capture strings, finite/live |568.18|587.62|+3.42%|
| Unchanged Core Count |0.864|0.879|+1.79%|

The intended many-capture benefit is not demonstrated. Its process medians are
698.36/742.88µs before versus713.87/806.35 after; overall ranges overlap. Funded
movement is small/uncertain. One-capture observations span470–1207µs in both
versions; their apparent median gain is not a stable improvement. Preserve all
observations without normalization or causal claims about unchanged controls.
[Summary](summary.json) retains every process/range/parent loan.

All allocation medians and funded peaks are unchanged, including242008B for
many-capture strings,246208B finite,246232B funded. Funded many-capture program
loan51872B is preserved; temporary peak311648B remains. Reject for no useful
intended-workload gain, not a universal slowdown threshold. No tuned variant,
confirmation, extra platform/package matrix or catalog refresh follows.

Candidate and restored source each pass10519 focused Core Release checks on
Windows/Linux .NET11, including captures, scalar repeats, work cutoffs, depth/
working/result controls, original cancellation/deadlines and NonBacktracking.
No production/test edit remains. Rebuild restored affected binaries before reuse;
the VM restored Core matches the frozen baseline. Existing two-runtime source
CI remains accepted; this discarded private refactor adds no release surface.

Core before `43DC7D1A390BDE1FA6A37A20A77FA17AD2B66CAC11CDA3B3D7C994E48F227BF1`,
trial `8BC3A90E8D7CBEC5615E3B473AB3006E65815F47D952D5D5C19937F1E8DE86F2`.
Python stays `DF38E39FA76011B9D9A6FC0D6F635FF9814E6F1BEE8D7F3B8DAABFC5C740DF94`.
Raw patch/protocol/graphs/preflights/Linux checks and samples verified offVM:
`artifacts/experiments/class-repeat-arguments-20261110/evidence.tar.gz`, SHA256
`B51C0813BA167F5050360DB3404A6B2723AA9CF755E0F5AFA8AD03F1719D1AA2`.
Windows and restoration logs are alongside it. Immutable dev63 packages and
authoritative snapshots remain unchanged. Do not replay this argument-flow trial.
