# Terminal Dot tails under the revised performance policy

**Accepted.** Parent `08be272cff403a8a12560e081f54c0c6e3ff6149`. Reassess the
[corrected, previously rejected prototype](../DotTerminalTail-20261101/README.md)
on current source. The old dual-ALC timings remain historical; this comparison
uses separate ordinary processes. No checked-access campaign was reopened.

The existing bounded greedy Dot-repeat helper can prove a complete unfolded
ASCII tail ending at Accept, select its last scalar endpoint, then execute the
original capture/tail instructions. Proof storage is fixed64 bytes; maxima8..511
and capture inspection are bounded. Unknown/nonterminal/folded/RTL tails keep
their paths. Strict whole-input validation, per-scalar work, original token and
deadline checks, captures/history and aliases remain. Fewer rewinds can reduce
route-dependent work/depth/working requirements; no public API or new mode.

Current unlimited and deadline-only corpus Count calls use the native window
executor. The first unlimited-route coverage guard correctly failed **before
timing**. The final preflight proves that finite/live public Count on the actual
594930-byte corpus/7matches JIT-compiles the new helper, without hooks or modes.
Native paths remain controls; no unlimited corpus speedup is attributed here.

One **77.159-second** Default-process ABBA comparison: twelve public Count
workloads,240 producer samples, minimum72.491ms. Normal eight-CPU startup then
CPU2 affinity,1s/min128 warm calls, independent75ms calibration, five samples;
complete result batches are consumed after timing. Only Core DLL/PDB changes;
worker and benchmark DLL/PDBs are identical. No confirmation or tuning.
[All observations](summary.json) retain process medians, ranges and allocations.

| Public Count | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Actual window, ordinary native control | 134.818 | 134.385 | -0.32% | 0/0 |
| Actual window, compiled native control | 135.107 | 133.423 | -1.25% | 0/0 |
| Actual window, finite limits/live token | 195.912 | 168.191 | -14.15% | 1640/112 |
| Actual window, deadline/live native control | 296.547 | 302.436 | +1.99% | 160/160 |
| River native control | 3338.963 | 3317.063 | -0.66% | 0/0 |
| Dense Unicode native control | 33.428 | 33.449 | +0.06% | 0/0 |
| False-tail Unicode native control | 30.663 | 30.481 | -0.60% | 0/0 |
| ASCII native control | 20.075 | 19.819 | -1.28% | 0/0 |
| Captured-tail control | 55.046 | 68.830 | +25.04% | 1528/1528 |
| Captured literal-repeat control | 13.556 | 14.941 | +10.22% | 6168/6168 |
| Folded literal-repeat control | 23.202 | 24.311 | +4.78% | 6168/6168 |
| Exact-literal control | 0.34191 | 0.33929 | -0.77% | 0/0 |

Accept the finite-operation27.72us saving and1528B/93.17% allocation reduction,
while explicitly retaining the local costs. Captured-tail+13.78us, captured-
repeat+1.38us and folded-repeat+1.11us have disjoint before/after sample ranges;
both after-process medians are higher. Their compiler/runtime cause is unproven.
The finite improvement also has disjoint ranges and both after medians below
both before. Native-control movement is not a source-effect claim or normalization.

Separate equal-result .NET Count diagnostics complete: window58.180us predecoded/
306.285us decode-then, River1480.508/7811.340us. Those comparators do not enforce
the finite policy and are not new catalog Status. Existing benchmark snapshots
and their source identities remain unchanged.

Focused Release checks pass on Windows/Linux and .NET10/11: Core394 (Windows
268+126 disjoint selections), PythonRe92, PCRE2178 per runtime. Restored tests
include the corrected final-denial witness: every denied work cutoff must return
failure and clear partial capture slots while preserving the caller's loan.
Other checks cover scalar widths, greedy endpoints, required child ends,64/65-byte
tails, captures, RTL/NB, malformed input, aliases, depth/working/result admission
and original interruption. [Core specification](../../../docs/SPEC.md) and
[migration guide](../../../docs/MIGRATION.md) record lower numerical requirements.

Full CI on `9f3f0012` subsequently failed one obsolete workspace witness:
Core42036/42037 per runtime on both OS. It expected fused Count to require
rewind storage. The corrected test accepts that Count under1 working byte and
still requires typed working denial for a capture-start tail with genuine VM
storage needs. Exact pre-fix reproduction fails both Windows targets; the two
affected fixtures then pass41 checks per runtime on Windows/Linux. Production
source and timed assets are unchanged; no timings or package checks are repeated.
The failed CI remains recorded as failed, separate from follow-up CI.

Immutable raw under `artifacts/experiments/dot-tail-policy-20261106/`:

- `evidence.tar.gz`, SHA256
  `D5FD0B0893DC1785340B739857B217B42462C2F4B061A61948937D9DF24F05B9`:
  exact before/after DLL/PDBs, worker, route preflight/code generation, declaration,
  raw public measurements, source patch/checksums, Linux checks and process logs.
- `correctness-source.zip`: Windows checks, complete final source/tests/docs,
  patch, analyzer, source identities and summary; SHA256
  `46B1D2F904CD1BDB9B38AD4CECF58B50B4B9302161557BB83A3C5CCC0F63A64E`.
- `workspace-witness.zip`: failed CI logs, pre-fix reproduction, corrected test
  source/patch and focused Windows/Linux results; SHA256
  `9A053A69292A7E6932714696CF79E6049C99B02128B58F1C1A77D45D1D123AE6`.

All eight frozen Full DLL/PDBs remain unchanged. Existing qualified packages,
symbols and published bytes are untouched. No package or Full refresh was repeated;
that qualification belongs to the next coherent delivery boundary.
