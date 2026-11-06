# Skip an impossible Python nonempty retry

After a complete capture-free LTR greedy scalar repeat returns empty, it has no
consuming endpoint at that position. The existing cursor now skips its impossible
nonempty retry, reusing failure's scalar advance, EOF checks and workspace cleanup.
The five-instruction proof covers one scalar atom and excludes lazy, captured,
alternating, constrained-tail and NonBacktracking programs. No field, executor,
allocation, unchecked operation or API was added.

Strict whole-subject UTF-8, scalar boundaries, captures and retained results remain.
The same invocation controls interruption and admission; only redundant VM retry
work disappears. Numeric work costs can decrease, as documented in
[the PythonRe contract](../../../docs/SPEC-PYTHONRE.md).

Windows and Linux each pass the complete PythonRe suite on .NET10/11:
4868checks per host. Fourteen new public cases compare against an equivalent
assertion-bearing program on the general retry path, including all four UTF-8
widths, nonzero starts, EOF, lazy/alternative/captured exclusions, string/byte
results, coordinates, Count/Split/Replace, cursor aliases and retained results.
Existing finite admission and every-observed-checkpoint token/deadline checks pass.
Only affected producer/test projects were built; Core references were reused.

ONE **79.652547351s** separate Default-process ABBA comparison against the exact
qualified dev.45 graph:13public rows/260samples/minimum66.1051ms, no confirmation.
Only PythonRe DLL/PDB changes; Core, PCRE2, caller and benchmark companions are
identical. Normal eight-CPU startup precedes CPU2 affinity; warmup1s/min128,
independent75ms target and20ms floor. Complete results are checked after timing.
The driver records the actual `x*` five-instruction program in each process.

| Public operation | Before us | After us | Change | B/op |
| --- | ---: | ---: | ---: | ---: |
| Actual empty FindAll strings | 74.85 | 52.86 | -29.38% | 16464 |
| Finite/live | 100.49 | 76.79 | -23.59% | 20664 |
| Deadline/live | 137.30 | 110.27 | -19.69% | 16624 |
| Mixed-scalar empty FindAll | 83.08 | 59.51 | -28.36% | 16464 |
| Nonempty greedy runs | 53.98 | 42.86 | -20.59% | 11320 |
| Folded greedy runs, including Kelvin sign | 51.96 | 43.13 | -16.99% | 10296 |
| Actual empty replacement | 79.99 | 56.96 | -28.79% | 6008 |
| Empty UTF-8 FindAll | 80.73 | 62.60 | -22.46% | 16464 |

All allocations are unchanged. Primary/controlled/mixed and run workloads have
disjoint before/after sample ranges. UTF-8 after-process medians58.80/66.27us
remain below both before80.05/81.45us; retain that process variation.
Lazy+0.92%, alternative+1.99% and captured-repeat+2.77% observations stay in
[the complete results](cost-summary.json), including spreads/process medians.
The unchanged native control+0.24% is not a normalizer. One-capture output-1.34%
uses the same changed method but a different branch; no causal gain is claimed.
Accept the substantial primary gains with these local costs.

Equal-result .NET compiled Matches/materialized-string diagnostics take37.43us/
127544B predecoded and44.26us/128592B with decoding. They are not CPython or
finite-policy equivalents. Existing catalog observations and Status are unchanged;
no full refresh or package/platform matrix was repeated. Immutable staged dev.45
packages and the eight Full benchmark DLL/PDBs remain unchanged.

Canonical off-VM evidence under `artifacts/experiments/empty-greedy-retry-20261106/`:

- evidence.tar.gz: `BE3BCEFA1F5837F007E5621E8227C938CB05BC07F9BBF249427C1389EC4AC082`,9719579B.
- windows-evidence.zip: `C32C88AAB75323C25BD0931BC37AB045AC0E5BD8F8A2B1C7918B6F45FDA347B0`.

The exporter initially reported a directory-change warning while creating its
excluded archive inside that directory. The same bytes passed GNU tar's complete
content comparison with exit0, then off-VM SHA verification. No measurements,
tests or archive generation were repeated; [the note](archive-note.json) records it.
