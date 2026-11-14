# POSIX unique capture positions

Accepted source `364a5acc`, against frozen dev89 POSIX `e8b20aa5` (VM checkout
`8ec2f992` already contained the independent Python change). Existing
`DisjointAsciiRuns` proves flat, positive, pairwise-disjoint ASCII class runs with
all capture events outside repeat bodies. Tags occur once, with no choices.
Keep their opening/closing bytes in existing state slots; omit this route's pooled
trace acquisition, events, final scan and history lookup. Other proven routes and
general interval/history selection stay intact. No new proof, selector, owner,
buffer or public API. [Acceptance/all rows](acceptance.json).

One **54.273s** seven-row ordinary-process ABBA, five150ms-target samples per
process/row, all>=20ms,150s cap, **zero confirmations**. Staged POSIX pairs differ;
every compiler-source checksum verifies, and Core/caller/.NET images agree.
Caller-guaranteed valid unchanged UTF-8, identical options and complete capture
byte/UTF-16 output. Caller capture buffers are reused, returned values checked
after timing. These are per-call public Match costs, not a native-parity refresh
or the canonical4096-record batch.

| Public Match | Before µs | After µs | Change | Allocated B/op |
|---|---:|---:|---:|---:|
| SKU captures |0.303|0.211|−30.42%|0→0 |
| Finite SKU captures |0.394|0.276|−30.00%|328→112 |
| Funded/deadline SKU captures |0.602|0.441|−26.81%|376→160 |
| Nested captures after Unicode floor |0.275|0.202|−26.52%|0→0 |

All four observed ranges separate. General-history control+1.34% overlaps, with
the same680B/op. Identical Core/.NET controls+0.044/−1.30% overlap and are not
normalizers. Retain process medians/ranges; they are not formal confidence intervals.

The read-only probe observed96B state plus224B pooled trace admission for SKU,
with0B warm unmetered heap allocation. Removing pooled capacity is not itself a
heap-allocation gain: finite328→112B and funded/deadline376→160B are measured
public differences. Match/cursor now fit128B working admission where old history
acquisition requested320B. State admission still precedes acquisition, every byte
and instruction is metered, and original cancellation/deadlines, depth/result
scope, publication clearing and synchronous refunds remain. Numerical requirements
can fall; no total heap/RSS guarantee is implied.

POSIX Release391 checks pass per Windows.NET10/11 and Linux.NET11, including
general histories/selection, all floors/output widths, nested captures, cursor
aliases, finite/funded limits, refusals and interruption. An additional empty-
capture-outside-runs theory reuses the existing all-floor/output-width/cursor
oracle; the10 focused tests pass per Windows target using the frozen producer.
Private caller setup corrections occurred before timing; no comparison was repeated.

Raw copied/hashed offVM before keeper release:
`artifacts/experiments/posix-flat-capture-audit-20261114/comparison/evidence.tar.gz`,
SHA256 `6CFB45FEC80F89F6E95209DCBF4CA0911DD1755CCD5182BF26D209A84A824A73`.
Windows checks are archived alongside it; all workers/collectors/keepers terminal0.
All572 staged package/symbol assets unchanged. No package/Full/canonical refresh.
dev89's corrected-protocol46m40s Full remains the latest milestone qualification.

This bounded simplification is closed. Keep broader POSIX capture grammar/parity
separate and prioritize remaining material capture/search/output costs; do not
generalize the unique-run shortcut to nullable, overlapping or repeated captures.
