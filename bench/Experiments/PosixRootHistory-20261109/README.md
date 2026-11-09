# POSIX root-history bounds

Fixed baseline `cf7bdf55`, .NET 11 RC3 on the quiet VM. Compilation wraps every
POSIX expression in tag zero, outside alternatives/repeats; every acceptance has
one opening at trace index zero and its closing last. Compare child occurrences
inside those known bounds rather than searching both histories for the root.
Descendant scopes, winner selection, storage and public APIs are unchanged.
Instruction/scan metering and original interruption remain; work counts can fall
with the removed searches, as the existing contract permits.

One **67.534-second** ordinary-process ABBA under a declared 180-second cap:
same Core/worker, five samples per row/process after warmup and independent
calibration. Full returned values and zero working loans are checked outside
timing. No confirmation or catalog/package qualification.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Empty captures, unlimited | 1.585 | 1.500 | −5.3% |
| Empty captures, finite/live | 2.007 | 1.937 | −3.5% |
| Empty captures, funded/live | 2.164 | 2.130 | −1.5% |
| Empty captures, deadline/live | 2.342 | 2.241 | −4.3% |
| Nullable captures, unlimited | 1.119 | 1.085 | −3.0% |
| History length 8 | 14.16 | 13.24 | −6.5% |
| History length 12 | 110.21 | 102.66 | −6.8% |
| Ambiguous captures | 1.046 | 1.081 | +3.4% |

Allocations are unchanged across all 17 rows. The history cases separate in both
processes/sample ranges; smaller changes have overlapping ranges and remain
estimates. The ambiguous-capture increase is retained, not explained away.
Unchanged POSIX whole Count moves −2.6%, Core Count +0.6%; do not normalize by
controls or compose a new native ratio from these deltas. This modest simplification
is accepted; exhaustive full-capture ambiguity remains a separate large cost.

Focused Release POSIX checks pass on Windows/Linux and .NET 10/11, including
independent root-empty alternatives, a public root capture, nested repetition and
supplementary captures at a nonzero floor. Existing capture-window, original
cancellation/deadline, finite refusal, alias and synchronous refund checks pass.

[Summary](summary.json) retains process medians and sample ranges. Raw inputs,
outputs, protocol, patch, matching binaries and Linux checks are local at
`artifacts/experiments/posix-root-history-20261109/evidence.tar.gz`, SHA-256
`A1D4E31173BC8B3B131DE6771FBF8D3CFC157A9030D1B4E4EB07D435FB6C8126`.
Windows checks are alongside it. The archive is copied off the VM and verified.
Historical package bytes and presentation snapshots are unchanged.
