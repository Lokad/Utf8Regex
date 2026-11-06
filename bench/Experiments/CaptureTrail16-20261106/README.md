# Smaller uniform capture trails

The dev.39 named-backreference FullMatch miss reports 0.546us and 720B per call.
Inspection finds one capture, one repeat and a first 32-entry trail chunk. The
existing ordinal comparison already tests bytes directly and this miss differs
at its first byte; another matcher is not justified.

Change only `TrailChunkCapacity` from 32 to 16. Keep the same uniform chunks,
owner, journal/rollback, admission and release algorithm. The conservative first
trail reservation becomes 544 -> 288B; the separate table, other stack units and
actual `Unsafe.SizeOf<T>()` queries are unchanged. Small calls allocate less;
long histories require more chunks/headers. No pool, execution mode, capacity
ladder, new state or accounting framework.

One **40.040-second** separate Default-process ABBA, 140 samples (minimum
57.801ms), default tiering/PGO, eight-CPU startup then CPU2 affinity. Before is
accepted scalar-pair Core `3946AF42...`; after is private Core `7CCCE2D6...`.
Both use PythonRe `094B8B39...`. Result observations/digests are consumed after
timing; Core history rows include a public capture-count read. No new catalog
Status or CPython ratio is inferred.

| Public workload | Before | After | Change | Before/after allocation |
| --- | ---: | ---: | ---: | ---: |
| Literal FullMatch control | .02932us | .02931us | -.02% | 0/0B |
| Actual named-reference miss | .47021us | .45164us | -3.95% | 720/464B |
| Named-reference hit | .51022us | .49391us | -3.20% | 720/464B |
| Actual miss, finite limits/live token | .63477us | .60838us | -4.16% | 832/576B |
| Core 4096-capture history | 317.575us | 324.553us | +2.20% | 394128/408232B |
| Core 512-capture rollback | 102.504us | 104.329us | +1.78% | 69952/73304B |
| Actual many-capture string FindAll | 658.647us | 670.196us | +1.75%, uncertain | 242456/242200B |

[Process observations](cost-summary.json) support small-call gains in both after
processes, including a 256B reduction (35.6% unlimited, 30.8% finite). Large-history
allocation grows 3.58%; rollback grows 4.79%. Accept these local operating costs.
FindAll's before process medians span 641–678us and overlap the after values;
retain uncertainty instead of claiming a general throughput result. No
normalization, tuning or confirmation run.

Both preflights completed before the original timing command stopped at the
unchanged 8 GiB available-RAM guard; no samples had run. Exact archived fresh
scratch copies totaling 60352328B were retired once. The resumed timed phase
admitted immediately at 8666176 KiB, with four 30-second process caps, and finished
in 40.040 seconds. Canonical raw/packages/symbols remain unchanged; do not retry
those retirements or any older cleanup boundary.

Release validation covers captures, chunk/table boundaries, rollback, retained
aliases, scalar/RTL behavior, working admission, cancellation and deadlines on
Windows/Linux and net10/net11. Windows first hit the deliberately changed old
544B accounting pin; that assertion became 288B while SizeOf pins stayed. The
corrected pin and boundary tests pass; all other broad checks passed. Linux's
complete focused runs pass. [Validation scope](validation-summary.json) retains
the exact original/corrected logs. No Full/package qualification for this pilot.

Immutable `artifacts/experiments/capture-trail16-20261106/` archives:

- `raw-evidence.tar.gz`: `ABECCA513E68A44BEF85F8453AA73E92B651468044FD8D888E89D18326AB24EF` (1651874B, verified off VM).
- `windows-evidence.zip`: `27078568D82AF6AE4EE23B69FD53EEF9035D4D3C1F32272EE29DCD273D9047AB`.

They retain source, declarations, exact graphs, samples, readiness/retirement
proof and tests. Source parent `61347fc96ae903f2fd42f404179c13acd8fe5bc8` plus the
recorded constant patch identifies the experiment. Dev.39 staged bytes stay
immutable; batch both accepted changes into the next common candidate.
