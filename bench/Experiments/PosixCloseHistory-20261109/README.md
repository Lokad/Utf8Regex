# Deferred POSIX close-only history

Baseline `8742cb4f`, .NET 11 RC3 quiet VM. Trial replaces each opening/closing
event pair with one completed occurrence: tag, start, end and the close-count at
opening. Descendant closings occupy that count through the parent close; the
same tag cannot nest in this ERE grammar. Comparison remains parent-first with
the existing empty-tail rule. One extra active-start int/tag and undo writes are
needed; the trial adds no engine, pruning, execution mode or accounting mechanism.

**Deferred; exact production and corpus changes reverted.** One 67.307-second
ordinary-process ABBA under a declared 180-second cap, identical Core/worker,
independent calibration, five samples/process/row of at least 20 ms after warmup.
Full results and zero loans checked outside timing. No confirmation: deterministic
storage increases and the consumer trade-off already justify deferral. This is
not application of the obsolete universal 10% gate.

| Public operation | Before (µs) | Trial (µs) | Change |
|---|---:|---:|---:|
| Empty captures | 1.50 | 1.44 | −4.0% |
| Absent capture | 0.70 | 0.64 | −8.7% |
| History length 8 | 13.21 | 12.08 | −8.6% |
| History length 12 | 103.23 | 90.54 | −12.3% |
| SKU control, 4,096 operations | 5,172 | 5,740 | +11.0% |
| Whole Count control | 0.34 | 0.35 | +4.0% |
| Unchanged Core Count | 0.84 | 0.91 | +8.4% |

History and SKU differences occur in both processes. The unchanged control also
moves, so do not normalize these observations or claim a refreshed native ratio.
Finite/funded/deadline capture timings improve approximately 2–3%. Allocations
increase: empty 208→232 B, nullable 200→216 B, history 192→208 B; even whole-only
workspace owners gain 8 B. Funded empty capacity grows 976→1,120 B and nullable
960→1,104 B. SKU allocations grow 983,040→1,146,880 B per batch. Shorter traces do
not establish a sufficient downstream gain for this storage/consumer trade-off.
[Summary](summary.json) retains all process medians, ranges, allocations and peaks.

The trial passed all 177 focused POSIX Release checks per target on Windows/Linux,
.NET 10/11. Three temporary independent cases covered a child absent in the last
parent iteration, a required empty final parent, and disjoint supplementary/BMP
children at a nonzero floor. Existing authority, finite refusal, aliases and
refund cases passed. Those trial cases are preserved in the raw patch rather
than adding tests for a reverted representation.

Driver, protocol, exact patch, before/after binaries, tests and observations are
copied off the VM at
`artifacts/experiments/posix-close-history-20261109/evidence.tar.gz`, SHA-256
`87E4A0CC117A62533605E4C4DD562258EFA63E8EB14989832036FE71C21145D8`.
Existing packages, catalog snapshots and public contracts are unchanged. No
package qualification or catalog refresh was run; do not repeat this exact trial.
