# Deferred PCRE2 call-stack undo omission

Trial: record subroutine call-stack push/pop mutations only when a pending
backtracking frame can observe them. The real call stack, capture/repeat restore,
VM/candidate charges and depth checks remained. Call entry used its saved frame
count; return/ACCEPT used the surviving count after cuts. No new executor.

One quiet-VM .NET 11 RC3 ordinary-process ABBA completed in **64.015 seconds**
under a declared 180-second cap: seven public IsMatch patterns unlimited/finite
live, plus unchanged negative-conditional and Core Count controls. Same Core and
worker DLLs throughout; 500 ms warmup, independent 75 ms calibration, five samples
per process, 20 ms floor. Complete booleans checked outside timing.

| Public operation | Before µs | Candidate µs | Change |
|---|---:|---:|---:|
| Straight subroutine prefix | 0.48 | 0.35 | -27.1% |
| Same, finite/live | 1.38 | 1.20 | -12.6% |
| Sequential calls | 0.55 | 0.38 | -30.7% |
| Same, finite/live | 0.82 | 0.62 | -24.4% |
| Inner alternative, finite/live | 1.06 | 1.22 | +14.3% |
| Outer alternative, finite/live | 1.14 | 1.40 | +23.4% |
| ACCEPT return, unlimited | 0.71 | 0.85 | +19.4% |
| Recursion, unlimited | 1.65 | 2.07 | +25.6% |
| Recursion, finite/live | 1.92 | 2.46 | +28.3% |

Straight-call finite allocations fall by 536 B; other allocations stay unchanged.
Controls move -2.3%/-0.2%. The broad alternative/recursive costs outweigh the
narrower benefit for this delivery. This is a trade-off decision, not a universal
10% gate. The runtime/code-generation cause is unestablished. **Reverted and
deferred**, with no confirmation or additional tuning campaign. Native ratios
remain historical; do not manufacture new ones from the straight-call gain.

Windows focused Release checks passed on both targets: caller captures,
inner/outer/later alternatives, ACCEPT/atomic cuts, recursion, original authority,
work/depth/heap/shared admission and result policies. Trial-only tests were removed
with the implementation. No Linux package qualification or repacking for a rejected
candidate. [summary.json](summary.json) retains all process medians/ranges/allocations.

Raw worker, patch, fixed binaries and samples:
`artifacts/experiments/pcre-call-undo-20261109/vm-evidence.tar.gz`, SHA-256
`5BE45C6C37B22D8E1EE01C53EFD6ABAEFA379A0C00C3F26844D928DBCF229364`.
The Windows check log and trial test patch are alongside it.
