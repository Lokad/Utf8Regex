# PCRE2 empty fixed workspace

Baseline `acf69b79`, .NET 11 RC3 on the quiet VM. Conditional/subroutine
inspection finds no fixed-vector rentals, but initialization still calculates
eight zero-sized charges and calls eight zero-capacity rental helpers. Extend
the existing reuse return to zero repeat/capture counts. Keep `ReserveWorking(0)`
for original-token cancellation, deadlines and sticky resource refusal.
Negative argument checks, nonempty admission, dynamic call/undo stacks, pooling
and refunds are unchanged. No new state, executor or public contract.

One **75.957-second** ordinary-process ABBA, capped at 180 seconds: identical
Core/worker, independently calibrated lanes, five samples per process of at least
20 ms after 500 ms warmup. Results and zero outstanding loans are checked outside
timing. No confirmation, package qualification or catalog refresh.

| Public IsMatch operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Negative conditional, finite/live | 0.514 | 0.486 | −5.4% |
| Negative conditional, funded/live | 0.519 | 0.492 | −5.3% |
| Accept-negative conditional, finite/live | 0.512 | 0.483 | −5.6% |
| Accept-negative conditional, funded/live | 0.518 | 0.491 | −5.3% |
| Subroutine prefix, unlimited | 0.536 | 0.516 | −3.7% |
| Positive-vector backreference control, unlimited | 1.65 | 1.68 | +1.9% |

The conditional finite/funded improvement occurs in both independent processes.
Unlimited conditional movement (−1.5%/−0.1%) is uncertain. Subroutine process
ranges overlap; its finite/funded movements are not presented as established
gains. Date lanes move −1.3% to +1.5%, positive-vector controls −2.4% to +1.9%,
and unchanged Core Count +1.4%. Retain these observations without normalization
or attributing unchanged routes to the new branch. Allocations and modeled
working peaks are unchanged across all 19 lanes. [Summary](summary.json) retains
per-process medians and sample ranges. Accept the smaller initializer and the
conditional controlled-path gain; infer no new native ratio.

Focused Release PCRE2 workspace, cancellation, resource, execution-architecture
and global/value invocation-authority checks pass: **202 per target**, Windows
and Linux, .NET 10/11. New cases exercise original-token cancellation, a controlled
deadline and sticky working denial at empty initialization while preserving a
parent loan. Existing physical-capacity tests cover empty and nonempty vectors.

Raw driver, protocol, before/after producers, tests and observations are copied
off the VM at
`artifacts/experiments/pcre-empty-workspace-20261109/evidence.tar.gz`, SHA-256
`6CCE1545491C67A0A21DE58743C94EAED66B545156026FC4B1A7FD2F7B3006F4`.
Accepted dev58 packages and snapshots remain immutable; this change is later
source work.
