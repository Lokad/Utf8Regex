# Batch impossible ASCII word-interior candidates

Accepted source `1efc4964f60696df54ca3bfb741574d1026984c7`; common46 freeze
`41a751a3d8d9ba344b7ccdbfe9f0814d44571b00` changes release metadata afterward.
[Source CI](source-ci.json)42289147358 is terminal GREEN on Windows/Linux, CLOSED.

The existing Python positive-boundary proof now borrows Core's allocation-free
`WordSearch` to skip at most2048 ASCII word bytes per loop. Standard boundary
predicates and Python's exact fixed word predicate share that set; other
predicates retain scalar advancement. First-attempt scratch admission stays ahead
of the proof. Non-ASCII neighbors, possible boundaries and EOF still reach the
verifier. No executor, retained program buffer or unchecked operation was added.

**Accounting change:** the existing loop checkpoint meters each bounded block
instead of every impossible scalar start. Original tokens/deadlines, useful work
limits and storage/depth/result admission remain. The
[PythonRe contract](../../../docs/SPEC-PYTHONRE.md) records these semantics.

An untimed [actual-date audit](actual-date-audit.json) confirms the47B corpus,
17instructions, leading positive boundary with Python's fixed word predicate and
necessary digit range48..57. The before graph is the accepted greedy-empty-retry
candidate; only PythonRe DLL/PDB changes. Core, PCRE2, caller and benchmark
companions are identical.

Release Windows/Linux each.NET10/11 pass1138focused checks covering boundaries,
finite limits, interruption, durable aliases, retained results and empty retries.
New witnesses exercise2047/2048/2049/4096-byte runs, nonzero offsets, scoped ASCII,
Unicode/combining/astral neighbors, captures, negative boundaries and EOF.
Six initial failures per Windows runtime were stale diagnostic-cadence witnesses:
4KiB now finishes before the unchanged64-work hook.200000-byte witnesses restore
in-scan injection; the Unicode fixture now measures controlled validation to
prove injection occurs afterward. Original failures remain archived. Runtime code
was not retuned; only affected producer/test projects were built.

ONE **85.646198316s** Default-process ABBA:14public rows/280samples,
minimum67.4177ms, no confirmation. Normal8CPU startup thenCPU2;1s/min128 warmup,
independent75ms target/20ms floor. Complete results are checked after timing.

| Public operation | Before us | After us | Change | B/op |
| --- | ---: | ---: | ---: | ---: |
| Actual date miss | 1.55 | 1.52 | -2.47% | 0 |
| Finite/live date miss | 1.98 | 1.92 | -2.99% | 112 |
| Deadline/live date miss | 2.36 | 2.30 | -2.62% | 160 |
| Dense almost-dates | 575.42 | 587.66 | +2.13% | 0 |
| Long word-interior miss | 109.12 | 1.02 | -99.07% | 0 |
| Finite/live long-word miss | 147.98 | 1.17 | -99.21% | 112 |
| Boundary-only Count through EOF | 132.04 | 1.11 | -99.16% | 0 |

All allocations are unchanged. The long-run/boundary gains have disjoint ranges.
Actual miss process medians1.506/1.603us before and1.511/1.534us after interleave;
the pooled2.47% is not a precise persistent gain. Dense almost-date medians
581.29/574.65us before versus604.61/572.40us after also interleave; retain its
adverse observation. [All results](cost-summary.json) preserve date hit-0.80%,
captured date-2.82%, Unicode prefix-3.47%, negative-boundary+0.30%, empty-retry
neighbor+1.41% and one-capture-0.87%, with process medians/ranges. These are not
attributed gains on declining branches. Native control+0.51% is not a normalizer.

Equal-result compiled .NET IsMatch diagnostics take0.20us/0B predecoded and
0.24us/120B with decoding on the actual ASCII date miss. They are not CPython or
finite-policy equivalents. Accept the substantial bounded-scan savings with the
documented small costs and uncertainty; no new catalog Status or universal
slowdown gate. Existing snapshots, immutable packages and eight Full DLL/PDBs
remain unchanged. Full/package matrices wait for the coherent delivery batch.

Canonical off-VM `artifacts/experiments/boundary-word-span-20261106/` evidence:

- evidence.tar.gz: `0470FF2007DD34DC05FDD27F6A1292D869A78273D34E141D8FD6575277290A7D`,9718218B.
- windows-evidence.zip: `F4C499D51B3B7359C92D78C4573639E8326DF9C45FF7D4D3B219D91E6112EA4D`.
