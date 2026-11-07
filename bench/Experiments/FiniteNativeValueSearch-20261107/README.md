# Finite exact-literal value Search

Zero-offset value `Search` now uses its existing complete literal helper with
finite limits, live cancellation and deadlines. Three exclusions are removed;
no executor, field, public overload or execution switch is added. The helper
already shares the original invocation, validates the whole subject before
discovery and derives coordinates for actual hits. Captured/other patterns and
nonzero floors retain their existing routes. SPEC-PYTHONRE documents that numeric
work and required VM capacity can fall; admission and interruption remain.

ONE [public comparison](cost-summary.json), **82.447778361s**, separate
Default-process ABBA,16workloads/320samples/min54.0487ms. Exact Core714B97B8 and
caller bytes unchanged; Python8BFE1349 ->AFDD69EB on source364eff9a plus the patch.
[Before-timing declaration](comparison-declaration.json) retains full hashes and
scope. Each worker uses750ms/min32 warmup, independent75ms targets, five samples,
one calibration confirmation before sampling, and result checks after timing.
No repeat campaign, timed comparator lane, package or full refresh.

| Public value operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual Unicode miss finite/live |1.25|0.44|-64.43%|112/112|
| Same deadline/live |1.36|0.57|-58.44%|160/160|
| Same token only |1.24|0.46|-62.53%|112/112|
| Unicode hit finite/live |2.31|1.59|-31.02%|112/112|
| Short ASCII hit finite/live |0.37|0.26|-29.73%|112/112|
| Full2048B Unicode hit finite |1.54|1.71|+11.13%|112/112|
| Full4096B ASCII hit finite |0.50|0.38|-24.27%|112/112|
| Unicode-prefix hit finite |0.46|0.32|-29.31%|112/112|
| Long Unicode miss finite/live |59.09|12.91|-78.15%|112/112|
| Short ASCII miss finite/live |0.34|0.15|-56.55%|112/112|

**Promote with the full-hit trade-off retained.** Finite/live, deadline, token,
hit, ASCII-full-hit, prefix and long-miss gains persist in both candidate processes
with disjoint sample ranges. Full Unicode hit is persistently slower with disjoint
ranges: +11.13%/0.17099us. Accept this local cost for the controlled-search gains;
no universal10% gate applies. Allocations are unchanged in every row.

Nonzero-floor+4.25%/0.05325us has interleaving ranges; this path remains scalar.
Core Count+0.56%, unlimited Unicode miss-2.12%, captured-1.80% and FullMatch-1.76%
also interleave; no normalization or causal attribution. Unlimited short ASCII
miss improves30.99% in both processes, disjoint. The actual837B subject hash05D2E466
matches Full48; these are this caller's operation costs, not new catalog/.NET/
CPython qualification ratios. Original finite policies remain enforceable.

Focused Release checks pass97 per runtime on Windows and Linux, including10new
cases: tiny working/result limits, hits/misses/empty patterns, primary/derived
coordinates, nonzero scalar boundaries, unsupported capture/assertion shapes,
strict malformed tails, original token/absolute deadline, every positive work
cutoff/exact boundary and retained13B parent loans. A new test first treated the
internal zero/unlimited sentinel as a finite cutoff; only that setup was corrected.
Failed and corrected logs remain. An archive-wrapper path was corrected before
the sole measured launch; no producer or measurement retry occurred.

Earlier [mail attribution](mail-attribution.json) is independent: one ordinary
Windows sampled-thread-time profile puts36.81% in validation and54.32% in counting,
without supporting a rewind-specific change. It does not measure VM throughput.
Its source/trace archive is retained separately and no mail rewrite accompanies
this bounded-hosting change.

Canonical evidence under `artifacts/experiments/`:

- finite-native-value-search-20261107/evidence.tar.gz:
  `AF9A4F77B03C8F13C62D2E112D91467D70A3DB728BDC52055413E0DFEC4D22C3`,2933296B,offVM verified.
- mail-word-attribution-20261107/evidence.tar.gz:
  `3C1F0F577321753A071E21CCB84AF773404E5F0956B5BA8D4E6C83927EB45F04`,280999B.

All published/staged package bytes remain immutable. Remaining parity and delivery
work stays active; no publication is authorized.
