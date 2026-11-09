# Python captured-string checkpoints

Baseline `05087a04`, .NET 11 RC3 quiet VM. Multi-capture `FindAllToStrings`
checked immediately after `GetText`, although that helper already checks before
and after admitted decoding. Remove that duplicate for participating groups;
absent groups keep their explicit checkpoint. Keep tuple/result admission,
strict validation, VM/trail/discovery, decoding and final publication unchanged.
No new state, executor or execution mode. Work charges can fall with the removed
duplicate, consistent with existing version-dependent work accounting.

One **60.279-second** ordinary-process ABBA, capped at 180 seconds: identical
Core/worker, independently calibrated lanes, five samples/process of at least
20 ms after 500 ms warmup. Full output values and zero loans checked outside
timing. No confirmation, package qualification or catalog refresh.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Many-capture strings, unlimited | 844.06 | 812.02 | −3.8% |
| Many-capture strings, finite/live | 1,266.31 | 1,190.34 | −6.0% |
| Absent tuples, unlimited | 32.30 | 31.88 | −1.3% |
| Unicode tuples | 109.66 | 109.56 | −0.1% |
| One-capture control, unlimited | 1,222.35 | 1,211.63 | −0.9% |
| Unchanged Core Count | 0.86 | 0.89 | +3.5% |

Both independent processes improve the affected many-capture operation:
unlimited before 831.9/848.8 µs versus after 798.8/815.3; finite before
1,267.4/1,253.8 versus after 1,157.0/1,212.0. Allocations are unchanged across
all 15 lanes, including 242,008/246,208 B for unlimited/finite many captures.
Absent and Unicode differences are small and uncertain. The unchanged one-capture
lane remains much slower unlimited than finite in both versions; no change to
that route or general producer stability is claimed.

Other unchanged lanes move too: captured Count +2.1%/−9.9%, range cursors about
−3.7%/−3.8%, Unicode single strings +1.9%, FullMatch +2.1%, early literal −1.2%.
Retain these observations without normalization or attributing them to the tuple
branch. Accept the simpler adjacent-check contract and the affected public
many-capture improvement; infer no refreshed CPython ratio or composed gain.
[Summary](summary.json) retains process medians, sample ranges and allocations.

All PythonRe Release checks pass, **5,009 per target**, Windows/Linux, .NET 10/11,
including existing cancellation/deadline, finite work/result refusal, absent/empty
and Unicode shaping, cursor/result lifetime and refund coverage. `GetText` retains
the original invocation at both boundaries; absent-group loops remain bounded.
The public API, memory admission and interruption guarantees do not change.

Raw protocol/driver, exact patch, before/after producers, tests and observations
are copied off the VM at
`artifacts/experiments/python-shaping-checkpoints-20261109/evidence.tar.gz`, SHA-256
`267184D434E5671A3E34F229AF0174781F2B5D5F1FA48BB57BB15BE6AABA9122`.
Dev58 packages and presentation snapshots remain immutable; this is later source.
