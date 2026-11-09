# Bounded history-buffer cleanup

Accepted after `97a8df7f`. POSIX cleanup and trace-suffix discard previously popped
each value-only record separately. The existing admitted buffer now accepts a
checked count; POSIX drops at most64 records after each original work/interruption
checkpoint. Reference-bearing records clear their slots, while value-only records
update the count once. Undo mutations still replay individually. No history
ranking, storage, pooling, admission, execution mode or ownership change.

One **75.549s** separate-process ABBA on the quiet .NET11 RC3 VM, within180s;
19 public lanes and380 samples, all at least70ms. One producer graph per process,
same caller and unchanged Core literal-control implementation. Both Core and POSIX
DLLs change; their exact identities are checked independently for each pair.
Full results and zero outstanding loans are consumed outside timing. No
confirmation, package or full-catalog run.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Nested captures |2.979|2.734|−8.2%|
| History8 |14.080|12.923|−8.2%|
| History12 |109.108|97.064|−11.0%|
| Ambiguous Count |0.329|0.315|−4.4%|
| SKU consumer |5178.443|5098.760|−1.5%|
| Empty captures |0.732|0.732|−0.1%|
| Nullable captures |1.070|1.072|+0.1%|
| Nullable captures, finite |1.424|1.431|+0.5%|
| Empty captures, funded |1.188|1.160|−2.4%|
| Nullable captures, deadline |1.713|1.714|+0.1%|
| Absent-pattern construction |4.449|4.526|+1.7%|
| Unchanged Core Count |0.833|0.834|+0.05%|

All allocation counts and funded peaks remain unchanged (empty976B, nullable960B).
Nested/history gains occur in both after processes with disjoint sample ranges.
Small lane movement and adverse construction timing are retained without
normalization. Construction implementation is unchanged; that movement is not
attributed to cleanup. [Summary](summary.json) retains all lanes/processes/ranges.
General nullable selection remains a gap. No refreshed native parity claim.

All192 POSIX and39 focused shared-buffer/state-lifetime Release checks pass per
target on Windows/Linux, .NET10/11. New public cases cross63/64/65 supplementary
scalar histories at a nonzero floor, then consume the empty tail through a cursor
alias and verify retained values/refunds. A distinct reference-bearing buffer type
checks invalid counts, zero/full discard, cleared borrowed slots and retained
capacity. Existing history-phase original-token/deadline interruption and finite
refusal tests remain. The original per64 work checkpoints are preserved.

The first new buffer test reused another test's pool type, changing its expected
retained capacity. Both OS caught that interference before timing. Giving the new
test its own type passed without a production change; failed logs and the initial
and final patches are retained. Exactly one performance comparison followed.

Raw patches, worker, before/after graphs, Linux checks and observations are verified
off VM at `artifacts/experiments/posix-bulk-pop-20261109/evidence.tar.gz`, SHA-256
`58E745963E21DD01E4125E37B8D2101BE47C82891C0711A7EC1DE86EE7565F89`.
Windows checks and the test-only resume script are alongside it. Dev61 packages
and snapshots remain immutable. Source CI follows the scoped commit.
