# Literal search blocks own their entry work

`Utf8SearchExecutor.TryFindLiteral` no longer adds a work charge before the
existing bounded candidate finder charges the blocks it examines. Prior sticky
failure and empty-plan paths retain their charged checkpoint and original
cancellation/deadline precedence. Range guards, bounded search/comparison and
confirmation charges, terminal success/miss checks and actual admission remain.
No field, owner, flag, mode, algorithm or unchecked access was introduced.
Finite work minima can decrease. This differs from the closed range-guard
deletion and the earlier adjacent wrapper-checkpoint cleanup.

One quiet-VM .NET 11 RC3 comparison completed **20 public operations in 91.901s**
under 120s, minimum sample **43.583ms**. Fixed original `a40bda03`, parent
`26ff757a` and parent plus the Core patch load separately; Python DLL/PDB bytes
are identical. Normal eight-processor startup precedes CPU2 affinity. Existing
500ms/minimum 128-call warmup, independent 60ms calibration, five alternating
batches, full range/count checksums after timing and allocations are retained.
Equal-output predecoded/decode-then compiled Regex diagnostics accompany eligible
rows. Python empty-then-nonempty Count has an explicit diagnostic exclusion;
all three owned producers remain measured and agree.

| Public operation | Parent us | After us | Change |
| --- | ---: | ---: | ---: |
| Python Search hit | 0.07665 | 0.07392 | -3.57% |
| Python Search miss | 0.07007 | 0.06829 | -2.54% |
| Finite/live Search hit | 0.32003 | 0.31279 | -2.26% |
| Finite/live Search miss | 0.31144 | 0.30583 | -1.80% |
| Deadline/live Search miss | 0.42597 | 0.41332 | -2.97% |
| Positive trailing assertion | 0.27476 | 0.26264 | -4.41% |
| Finite Core Match, largest adverse | 0.31389 | 0.32583 | +3.81% |

**Promote under the predeclared parent gate:** useful actual Search reductions,
all adverse parent medians within 3.81%, unchanged allocations. No minimum gain
floor was added, and no confirmation or variant was run. [All observations](summary.json)
retain Unicode/offset/boundary/fold/capture, FullMatch/Count and Core gap controls.
FullOpts code generation shrinks 1689 to 1622 bytes and its stack reservation
216 to 184 bytes. That is diagnostic code generation, not public timing.

This is **not release acceptance**. Against original, this pilot's Unicode Search
is +11.84% and finite Core Match +13.19%; before-patch ratios were already close
to the allowance. They join the retained delivery controls. The prior stable
native Search +16.03% and Core gap +13.02% failures remain evidence; this pilot's
more favorable original ratios do not erase them. Keep all 189 existing delivery
cases, H5 and the newly affected public controls at the next coherent boundary.
No package, catalog, full-refresh or successful-gate replay was performed.

Release checks pass on .NET 10 and 11: **771 Core and 183 PythonRe per runtime**.
They cover both search directions, cross-block tails, all work cutoffs, cursors,
strict Unicode validation, original search/comparison/trailing/publication
interruption, admission/refusal/refunds and retained loans. New tests seed a real
ledger overflow without payload allocation, checking that even an otherwise
unmetered invocation stops before discovery and retains cancellation precedence.
An initial test setup incorrectly expected caller refusal to return a sticky
numeric denial; caller refusal throws `Utf8MemoryAdmissionException`. The corrected
tests exercise actual numeric overflow instead, preserving the original 768
passing checks. Both failed and corrected logs remain.

Driver preflight caught Core's different coordinate property names and Python's
different empty-match Count. The bridge and diagnostic eligibility were corrected
before the sole timed run; no producer semantic result or workload was removed.
All 20 operations then preflighted. Production was unchanged after timing.

Immutable raw evidence under `artifacts/experiments/literal-search-entry-20261103/`:

- `evidence.tar.gz`, SHA256
  `FB66D45A66EF77F68EAD525429B6579EBF0C9AF1281BA6492D011FF575373D1E`:
  complete frozen source, exact three Core/Python DLL/PDB sets, driver/project,
  patches, preflights, raw timing/allocation samples, codegen, scripts and exits.
- `correctness-source.zip`, SHA256
  `2F01795E79BBA01B0899470D08F2BDB2B710BEAE06C02F3CCB89D363152BEF59`:
  final production/tests/contract, initial and corrected checks, setup and export logs.

Earlier staged/published package bytes remain immutable. The next work investigates
remaining operating costs before freezing a fresh common package candidate.
