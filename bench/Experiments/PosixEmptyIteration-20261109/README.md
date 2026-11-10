# Redundant optional empty iterations

Accepted after `38c42eb8`. At the existing repeat zero-progress exit, discard an
empty iteration only when the counter proves at least one earlier occurrence and
the minimum is already satisfied. The saved optional exit before that body has
the same endpoint and suffix acceptance. Its body history shares all earlier
occurrences and wins by the documented shorter-history rule before descendants
break ties. Backtracking still explores inner alternatives, including consuming
ones. First and mandatory empties remain eligible. The saturated unbounded-min0
counter stays conservative. Whole-only operations preserve the same endpoint.

No cold metadata, buffer, mode, executor or Core runner change. Keep basic
transition/interruption charges, authority, bounded storage, undo/refunds and
retained results. Eliminating dominated work can lower numerical work/depth
requirements. This is not general dominance or a worst-case linear-time promise.

One **75.396s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s.
Same Core/caller, one producer per process;19 public lanes and380 samples all at
least68ms. Full results and zero loans checked outside timing. No confirmation,
package or catalog qualification.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Nullable captures |1.066|1.026|−3.7%|
| Nullable captures, finite |1.452|1.419|−2.3%|
| Nullable captures, funded |1.608|1.554|−3.4%|
| Nullable captures, deadline |1.746|1.680|−3.8%|
| Ambiguous captures |1.004|1.025|+2.1%|
| Nested captures |2.743|2.769|+1.0%|
| History12 |97.418|96.709|−0.7%|
| SKU consumer |5191.153|5165.423|−0.5%|
| Empty-pattern construction |5.233|5.330|+1.9%|
| Unchanged Core Count |0.838|0.834|−0.4%|

Allocations and funded976/960B peaks are unchanged in all lanes. Nullable gains
occur in both after processes with disjoint sample ranges in each policy lane.
Adverse control movement is retained without normalization; unchanged construction
is not attributed to pruning. [Summary](summary.json) contains all observations.
No fresh native ratio is claimed. General ambiguity and the main nullable gap
remain; do not promote this small gain into broad POSIX parity.

All198 POSIX Release checks pass per target on Windows/Linux, .NET10/11. Six new
independent cases cover required/first empties, nested alternative participation,
required suffixes, supplementary scalars/nonzero floor and a positive-minimum
saturated counter. Existing public finite admission, history-phase original-token/
deadline interruption, refunds, retained values/cursor aliases remain. The final
test-only corpus addition was checked after timing against identical producer
bytes. A bounded task-local ordinary-process comparison also finds identical
public captures/byte/UTF16 coordinates in3,168 baseline/candidate cases across
nullable bodies, bounds, suffixes and Unicode. This is a regression control, not
an independent POSIX oracle; the restricted argument and fixtures define intent.
Its first projection read byte coordinates for unsuccessful values and the
baseline correctly refused; the corrected projection only reads successful ones.
No performance measurement was repeated.

Raw timing/patches/before-after graphs/Linux checks are verified off VM at
`artifacts/experiments/posix-empty-iteration-20261109/evidence.tar.gz`, SHA-256
`DE310AF1DA3A2F7AE12A29BD7D0BD8F9DD72AB2C6B891045DCB0F55C84B449A5`.
Final corpus/Windows+Linux checks and the disposable comparison are in the adjacent
`post-checks.tar.gz`, SHA-256
`A7CB606046BFE9F2AD6A6CD42760C9B743E322DE3F82704605CCAC9C997D92C7`;
[identities](semantic-summary.json) pin its producer graphs.
Dev61 packages/snapshots remain immutable. Source CI42716409925 passes
Windows/Linux for1cc1444e.
