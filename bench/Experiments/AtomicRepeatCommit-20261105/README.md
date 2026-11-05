# Skip repeat choices immediately discarded by an atomic commit

**Accepted.** The actual PythonRe `a++b` and `(?>ab+)c` programs end a pure greedy
repeat at `AtomicCommit`. Reuse the existing class-repeat loop instead of creating
shorter repeat choices that this commit discards. The existing bounded capture-end
walk remains; capture endings, enclosing atomic marker and commit still execute.
One condition and comment change; no new runner, state, selector or unchecked API.
Lazy atomic repeats, RTL, folded literal atoms and compound bodies retain the VM.

Strict complete-subject UTF-8/scalars, captures/history, assertion required ends,
retained aliases, original synchronous cancellation/deadlines and owned admission/
refunds remain. Intentional resource-cost revision: fewer VM instructions, repeat
frames and journal loans. The existing scalar loop remains metered, and the atomic
marker still requires admission. [Resource contract](../../../docs/SPEC.md).

One **102.464s** A0/B0/B1/A1 compares the exact frozen dev34 Full graph (source
`fd8c2d7e`) with parent `4e3402f3` plus the archived runtime/tests patch. The parent
adds generated data only. The baseline is reused without rebuilding; six adapter/
benchmark DLL/PDB companions and the worker are identical. One producer per
ordinary Default process, normal eight-CPU startup then CPU2. Producer warmup one
second/minimum16 calls, BCL250ms/minimum4, independent60ms calibration and five
alternating samples. All180 producer samples exceed34.9547ms. Complete typed
range/value digests are checked after timing. Ref-struct fields are copied inside
identical timed wrappers. BCL uses equivalent atomic syntax for possessive repeats;
predecoded and decode-then lanes are diagnostics on overlapping semantics, not
CPython/catalog verdicts. [All observations and process spreads](cost-summary.json).

| Public operation | Before us | After us | Change | B/op before / after |
| --- | ---: | ---: | ---: | ---: |
| core-literal-control | 2.485 | 2.484 | -0.02% | 0 / 0 |
| actual-possessive-search | 0.639 | 0.438 | -31.48% | 1056 / 592 |
| possessive-finite-live | 0.840 | 0.612 | -27.14% | 1168 / 704 |
| possessive-deadline-live | 1.078 | 0.822 | -23.73% | 1216 / 752 |
| actual-atomic-hit | 0.626 | 0.443 | -29.22% | 1056 / 592 |
| actual-atomic-miss | 0.662 | 0.475 | -28.33% | 1056 / 592 |
| captured-atomic-search | 0.922 | 0.702 | -23.92% | 3248 / 2784 |
| lazy-atomic-control | 1.157 | 1.148 | -0.77% | 1056 / 1056 |
| greedy-neighbor | 0.352 | 0.352 | +0.10% | 64 / 64 |

Both after-process medians beat both before medians on all six affected Search
rows. The unchanged Core literal control moves -0.02%; no normalization is applied.
Lazy atomic and ordinary greedy neighbors remain essentially flat, with overlapping
process observations; their pooled changes are not claimed as causal gains. The
untimed allocation preflights agree. FullOpts retains RunClassRepeat; proof-helper
code grows3146->3163bytes, without an isolated kernel speed claim. No confirmation,
source tuning, package qualification or Full refresh follows this individual pilot.

Windows/Linux focused Release per .NET10/11: Core1684/Python1108/PCRE181 PASS. New
Core26 shapes cover captures/history/shared names/balancing, nested repeats/atomic
markers, min/max/empty/failure alternatives, assertions/required ends, BMP/astral,
RTL and lazy/compound/folded controls. Python20 cases cover scalar byte/value
aliases, strict malformed full tails, working/work/depth/result refusal, funded
loan cleanup and captures retained after disposal, original cancellation/deadline
during an8192-scalar repeat and successful reuse after interruption. Seven extra
PCRE atomic cases exercise the same production runner through its public adapter.

Immutable artifacts/experiments/atomic-repeat-commit-20261105, verified off VM:

- evidence.tar.gz: `2CCE60A0CC1021E0E448EF36417135D35EE13D4B8C3013D96B49B71748A95198`.
- windows-correctness.zip: `2D4789C200E1E209D3DF35BB7CC3C1CF385FDC41C2F334190B9A25CE0D126785`.
- Earlier untimed route-inspection.zip: `60163AEC05212877091F3366104329BB557B05FE61F0A21B151ABE45C2CEBB7C`.

Declaration `F8C3D33D89C3CAF2DB82303A9FE6D8B7EACE1C501F43A7AF805EFAB923E7FE52`
and four-file runtime/tests patch
`D4D0E378C57DA260746B22AEAA12CFDF9956EF81F39F3FF2BC3AFE538FE14C3A` are
unchanged. Documentation follows measurement; no further runtime edits. The eight
frozen dev34 Full DLL/PDBs and all staged packages/symbols/raw evidence remain
immutable. Combine useful source work at a future coherent delivery boundary.
