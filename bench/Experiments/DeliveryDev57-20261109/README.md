# POSIX review and four-profile dev.57 delivery

Frozen source `03f6839e`, common `0.5.0-dev.57`, targets .NET 10/11 for Core,
PythonRe, PCRE2 and POSIX. Adapters pin Core exactly. Packages and symbols are
in `artifacts/nuget/`; the immutable Windows qualification is under
`artifacts/package-candidates/0.5.0-dev.57/`. Nothing is published.

## Review and accepted changes

The POSIX interpreter is live and owns different selection semantics; no
unreachable production engine/helper was established. Removed duplicate parser
corpus and internal winner/iteration replays, including their shared production
scalar-advance logic. Public cases have independent IDs; language tests execute
the public API. Added independently derived repeated/nullable partitions and
phase-observed history interruption, while preserving targeted admission/rollback
tests. Source checks cover both runtimes.

The existing evidence verifier now checks required lanes/orders, finite sample
alignment, native identities, recomputed statuses and exact raw/summary agreement.
A valid isolated fixture passed and nine targeted corruptions failed. These are
correctness probes, not performance evidence.
The probes cover missing deadline output, native identity, coherent raw/summary
drift, false status, sample alignment, an extra native lane, summary value, false
exclusion and incorrect units.

The POSIX catalog has 25 timed lanes instead of 66. It measures longest selection,
capture participation, nesting/nullable histories and representative finite-policy
costs. Unicode/nonzero floors are part of selection; the unambiguous SKU consumer
is a separate control. Generic Core search/count/validation cases are not repeated.
Native ratios require verified equal results; incompatible capture histories stay
excluded, with their managed costs and history-growth series retained.

Three accepted simplifications have separate bounded, ordinary-process comparisons:

- [Validated coordinate projection](../PosixValidatedProjection-20261109/README.md)
  avoids repeated UTF-8 validation. Funded empty captures improve 7.1%, SKU finite
  lanes 8.7–13.3%, long-prefix Unicode projection 20.8%; allocations are unchanged.
- [Whole-only histories](../PosixWholeOutput-20261109/README.md) omits unobservable
  tag state while retaining earliest-longest acceptance. Changed whole Match/Count/
  cursor workloads improve 41–81%, with fewer Match/Count allocations. Requested
  captures retain full hierarchical selection; cursor aliases may change output
  span sizes without losing validity or cumulative resource authority.
- [ASCII class representation](../PosixAsciiClasses-20261109/README.md) uses existing
  masks and retains only non-ASCII ranges. SKU finite lanes improve 5.9–6.6%;
  construction allocation falls from 13,616 to 13,072 bytes. Construction timing
  and small unlimited deltas remain uncertain.

Strict whole-input UTF-8, scalar coordinates, original cancellation/deadlines and
useful work/depth/working/result controls remain. Numerical admission can fall as
redundant storage/work disappears. Working limits model major capacity and growth
overlap, not every CLR allocation or process RAM. Value/caller-owned capture
outputs create no producer result payload; a positive result cap admits them.

## Closed trials and remaining work

The [shared ASCII text trial](../SharedAsciiText-20261109/README.md) regressed an
unlimited Python single-capture workload 329.4%; the
[PCRE2 call-undo trial](../PcreCallUndo-20261109/README.md) improved straight calls
27–31% but regressed alternatives/ACCEPT/recursion 14–28%. Both were reverted,
including trial-only tests. No confirmation, repeated tuning or package matrix
was spent on rejected implementations. This is not a universal 10% gate.

Full-capture POSIX ambiguity remains expensive and nonlinear. General history
dominance/TDFA work needs a separate correctness proof; no alternate engine was
added. Python capture-output/VM and PCRE2 call/conditional costs remain priorities.
Strict UTF-8 input validation is required even against predecoded comparators.
The final catalog records those gaps; isolated gains are not composed into ratios.

## Current refresh

One [Full](full-refresh.json) passed all **85 steps in 2,822.389 seconds
(47m02s)** under the unchanged 3,600-second cap. It includes one Release build,
both-runtime source and disabled-dynamic-code checks, all four .NET 11 catalogs,
paired comparators, lifecycle/scaling, resource costs and final evidence checks.
Eight generated README/summary files were imported once. Five snapshot checks per
runtime pass afterward with the Full's matching binaries; no rebuild or repeat.

The final POSIX catalog measures empty captures at 1.58 µs (9.24× native), nullable
captures at 1.16 µs (5.82×), and the separate SKU control at 5.12 ms per 4,096 calls
(7.57×). SKU finite/funded/deadline lanes cost 6.35/7.00/7.52 ms. Full-capture
history costs at 4/8/12 bytes remain 2.12/14.49/113.97 µs, without a native ratio.
The new Count result is 0.31 µs/144 B; public histories/captures remain unchanged.

Python many-capture output remains 911.14/351.45 µs (2.59× CPython); one-capture
output is 2.09×. PCRE2 date match/miss remain 4.36/4.29× and negative conditionals
3.79×. Its subroutine row is now Inconclusive because lane-order medians differ
2.92%; Python limited replacement/split are also Inconclusive. Four Python rows
are Unqualified because removable comparator harness floors exceed the existing
allowance. Keep those reasons rather than promote their ratios.

These catalogs are current operating costs, not isolated causal comparisons.
Core algorithms did not change: hex-color moves 0.61→0.75 µs (~24%) and quick AWS
keys 6.08→6.65 ms (+9.4%). Retain adverse observations without attributing them to
the POSIX changes, composing speedups or reopening completed trials.

Raw evidence is local at
`artifacts/experiments/delivery-dev57-20261109/full-evidence.tar.gz`, SHA-256
`D66080A2E6D1612F262BCF4B277712D67DEE8EBED891AAE620EC27B9210C8BB9`.
The archive is copied off the volatile VM and hash-verified. This is not an
archive-service/upload requirement. Ordinary CI consumes compact summaries offline.

## Packages and consumers

Windows qualification passed in 432.056 seconds: ten detached fixtures, both
targets, 110,662 checks against the exact eight package/symbol files. Original
Awk19/Lython118 checks pass per target, and public hosting probes finish with zero
root loans. Twelve original restore audits and all 477 actual compiler inputs,
including generated files, were captured. Source CI run `42607437052` is green
on Windows/Linux; performance and package/consumer acceptance remain separate.

Linux accepted the same ten detached fixtures/110,662 checks, original Awk/Lython
copies and public probes on both runtimes, without producer rebuild/repack. All
eight assets, 28 restore identities and 14 TRX identities passed their audit.
Core has no package dependency; adapters pin `[0.5.0-dev.57]`, with no native assets.
All 308 older staged files are byte-identical; eight new files bring the total
to 316. Original consumer sources change only three exact producer pins.

Linux evidence is local at
`artifacts/experiments/delivery-dev57-20261109/linux-evidence.tar.gz`, SHA-256
`C55617321A0FFC564B272079AF686C1C219B2AA1EE09FB575A980AB131BC10CD`.
Both volatile-VM archives are copied off and verified. The structured
[acceptance record](acceptance.json) distinguishes this qualification from
publication, downstream adoption, complete flavor equivalence and further tuning.

The imported summaries and authored candidate-status links follow the frozen
producer revision. They do not change or requalify package bytes. Final relevant
CI for `3d0bec30` passed Windows/Linux, run `42615453342`, completed
2026-11-09 at 12:22:57 UTC. This receipt-only closure needs no repeated matrix.

The request audit covers the production/dead-material review, independent tests,
POSIX-specific benchmark scope, measured simplifications and current parity queue
for all three adapters. R0–R5's bounded batch and this package/consumer/operating-cost
boundary are complete. The larger gaps above remain follow-up work; R4 completed
its investigation without a promoted change. No general parity claim.

Automatic approval review rejected deletion of temporary delivery extraction/input
directories with a policy block. Those ignored task directories remain intact;
qualified assets and accepted evidence are unaffected.
