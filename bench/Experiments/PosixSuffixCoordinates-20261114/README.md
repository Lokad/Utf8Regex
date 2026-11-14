# POSIX suffix capture coordinates — accepted

Source df831e51 → d6a9ccc3, .NET11GA on the dedicated VM. Before projecting
captures, the existing operation already knows the whole match's UTF16 end.
For a capture ending there, count only its content and derive its UTF16 start
by subtraction. Empty end captures need no text scan. Whole-equal ranges keep
their existing reuse; interior captures keep bounded prefix/content counting.
The same private ProjectRange handles both known origins. No map, classifier,
cache, new executor or API is added; matching/selection code is unchanged.

ONE six-case public comparison, nine250ms target samples/case and1s warmup.
Core and the caller stay byte-identical; only POSIX producer changes, one per
ordinary process. Loaded DLL/PDB/source checks pass. Full byte/UTF16 capture
values are checked after timing; fixed caller-owned destinations are cleared
equally. Valid unchanged UTF8/ERE/options/floors/output guarantees are equal.
All samples exceed20ms. Count is the unchanged control. No confirmation needed.

| Complete public operation | Input bytes | Before → after µs | Observed change | Bytes/op, unchanged |
|---|---:|---:|---:|---:|
| short-sku | 6 | 0.303 → 0.305 | +0.68% | 0 |
| long-ascii-suffix | 8,193 | 21.794 → 21.954 | +0.74% | 0 |
| unicode-suffix-floor | 13 | 1.329 → 1.359 | +2.25% | 680 |
| unicode-empty-end-floor | 1,792 | 7.959 → 6.176 | -22.41% | 0 |
| finite-sku | 6 | 0.394 → 0.379 | -3.82% | 328 |
| unchanged-count | 6 | 0.164 → 0.163 | -0.52% | 0 |

ACCEPT the22.41% Unicode empty-end improvement and3.82% finite-SKU gain.
INTENTIONALLY RETAIN short-SKU+0.68%, long-ASCII+0.74%, short Unicode+2.25%.
Count moves-0.52%; no subtraction or composition. These process medians/ranges
are observations, not confidence intervals, causal instruction costs, a native
parity ratio or a broad speedup promise. Allocation behavior is unchanged.

The long Unicode case is a fixed512-scalar literal inside a capture, followed
by an empty group, at a nonzero scalar floor. It isolates actual requested
capture-coordinate work without benchmarking Core's generic search again.
Other cases cover a genuine short SKU, long disjoint ASCII runs, non-ASCII
suffix captures and finite policies. Manual expected whole/capture coordinates
are checked in every batch. The canonical POSIX comparator remains PCRE.NET's
ordered NFA UTF+NO_UTF_CHECK on verified equal-result overlap; its exclusion
rules remain, and it is not a general POSIX capture oracle.

Windows AND Linux: all388 POSIX Release checks pass EACH on net10/net11.
Coverage includes supported options, selection/empty/absent/interior/whole/suffix
groups, supplementary coordinates, floors, truncated output, finite denials,
original cancellation/deadlines, cursor aliases and retained values. The spec
records lower modeled coordinate work; resource admission and bounded original
interruption remain. No validity pass, result allocation or frame policy changes.

[Acceptance](acceptance.json) records identities, costs, checks and verified
raw hashes. Raw is under artifacts/experiments/posix-suffix-coordinate-20261114/,
copied and verified before keeper17218 terminal0. No package/Full qualification
or canonical-table refresh occurred; qualified dev86 remains immutable.
Campaign CLOSED. Do not tune this path from the same samples.
