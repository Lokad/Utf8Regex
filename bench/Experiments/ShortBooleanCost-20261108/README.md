# Warm boolean matching on short subjects

Qualification of the consumer handoff against source c80612cb and published
Core 0.4.0. No implementation change, package qualification or catalog refresh.

Compile `^[A-Z]{3}[0-9]{4}$` once, then call public
`IsMatch(ReadOnlySpan<byte>, CancellationToken)` on 65,536 alternating `ABC0123`
and `abc0123` subjects. Exactly 32,768 positives; input and compilation are outside
timing. The original Awk 24.092 ms / 112.198 MiB report includes the consumer;
the following costs belong to the producer alone.

## Results

One 44.847-second run on the quiet eight-CPU Linux VM, .NET 11 RC3, affinity CPU2
after runtime startup. Ordinary-process ABBA, identical common worker DLL,
one producer per process. Each row warms for at least 500 ms/four batches,
calibrates independently to 75 ms, then records five samples of at least 20 ms.
Complete batch checksums are consumed after timing. No confirmation or tuning.

The ranges below are the two process medians, not confidence intervals.
Full per-process medians, sample extrema, allocations and identities are in
[summary.json](summary.json).

| Public workload | 0.4.0 ns/subject | Current ns/subject | Allocated bytes, old → current |
|---|---:|---:|---:|
| Reported mixed subjects | 420.04–435.55 | 163.86–163.93 | 1,792 → 112 |
| All positive | 482.75–491.27 | 143.18–149.26 | 1,792 → 112 |
| All negative | 336.13–336.39 | 179.08–182.62 | 1,792 → 112 |
| Unicode alternative | 648.69–653.58 | 405.34–427.78 | 3,664 → 640 |
| Mixed, finite policy | 477.45–489.84 | 171.13–173.17 | 1,792 → 112 |
| Mixed, finite policy + deadline | 591.95–594.42 | 369.97–380.61 | 1,840 → 160 |
| Unchanged predecoded BCL control | 27.66–28.27 | 29.29–36.11 | 0 → 0 |

Current substantially addresses the old report: approximately 2.6× faster on its
mixed workload, with 112 MiB falling to 7 MiB of producer allocations per batch.
Current finite controls add about 5%; deadlines remain a larger absolute cost.
The BCL control moved, particularly in current-b. Do not normalize it away or
interpret these observations as a precise causal decomposition. The producer
improvement is much larger and disjoint in both processes.

A separate current-only worker uses the new funded overload: 195.30 ns/subject,
112 B/call, zero reservations and zero outstanding working loans. A zero allowance
admits this scratch-free pattern; storage-demanding lookahead correctly refuses
and leaves no loan. The remaining 112 B is shared invocation metadata, outside
the modeled workspace allowance. Whole-worker collected heap deltas include
prepared controls/pools and do not establish exact regex retained storage.

Both producers pass .NET 10 and .NET 11 preflights for counts, malformed UTF-8,
original-token cancellation and finite-work refusal. These are focused checks,
not a new package matrix or a claim that downstream consumers adopted current.

## Reproduction and remaining work

[Worker.cs](Worker.cs) and [Worker.csproj](Worker.csproj) retain the caller. Build
with `CorePath` pointing to 0.4.0, then run identical worker bytes in separate
directories with the respective Core DLL. Compile with `Funded=true` against
current Core only for the additional governed row. The .NET 10 baseline asset is
also used under .NET 11. Exact loaded identities are in the summary.

Raw observations, bounded protocol and runner are ordinary ignored outputs under
`artifacts/experiments/short-boolean-cost-20261108/`; no remote archive is required.
The comparison does not prescribe another engine or workspace API. Remaining
metadata allocation and latency are later opportunities; the approved POSIX
contract and implementation remain next.
