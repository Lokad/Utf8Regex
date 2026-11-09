# Deferred Python decoded-text return

**Deferred; exact producer source restored.** `GetText` checks immediately after
the shared decoder's terminal checkpoint under the same invocation, with only
private loan bookkeeping between them. The trial removed that outer duplicate,
keeping entry checks, admitted string extents, bounded decoding, terminal checks
and final publication. No new state, decoder, cache or execution mode.

One **79.088s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s.
Same current Core/caller, one producer per process;20 public lanes and400 samples,
all at least27ms. Full string values/checksums and zero loans checked outside
timing. No confirmation: the principal consumer did not improve and this one-line
change does not justify another timing campaign. No package or catalog run.

| Public operation | Before (µs) | Trial (µs) | Change |
|---|---:|---:|---:|
| Many-capture strings |678.510|695.711|+2.5%|
| Many-capture strings, finite/live |1013.737|1050.994|+3.7%|
| One-capture strings |474.588|474.890|+0.1%|
| Unicode strings |191.476|183.338|−4.3%|
| Rejected-first strings, finite/live |425.800|460.568|+8.2%|
| Unchanged range cursor |683.865|708.848|+3.7%|
| Unchanged captured Count, finite/live |505.095|519.147|+2.8%|
| Unchanged Core Count |0.860|0.861|+0.1%|

Allocations are unchanged in every lane. Both main after-process medians are
higher, but ranges partly overlap and unchanged operations move too. Do not
attribute every delta to the removed checkpoint or normalize by controls.
The simpler boundary did not establish a useful public benefit; retain the
existing implementation. [Summary](summary.json) preserves all observations.

All5,012 PythonRe Release checks passed per target on Windows/Linux, .NET10/11,
including original cancellation/deadlines, finite work/result/working admission,
empty/absent/Unicode outputs and retained results/cursor aliases/refunds. After
restoring the one changed source file,516 focused ownership/string-interruption/
finite-result checks pass per target on Windows. Rebuilt Linux net11 Python bytes
match the baseline exactly. No tests were added to preserve a reverted change.

Raw patch/protocol, before-after graphs, Linux checks and samples are verified
off VM at `artifacts/experiments/python-decode-return-20261109/evidence.tar.gz`,
SHA-256 `93453692A36B13C224BC15D83AD6DBB2A8499DFD922A0B798DE2A1005B26275E`.
Windows trial/restored checks and restored Linux build identity are alongside it.
Dev61 packages/snapshots remain immutable. Close this exact trial.
