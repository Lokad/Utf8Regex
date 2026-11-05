# Execute assertion-free roots in the existing VM

**Accepted.** Existing TryMatchAt calls the SAME RunSegment directly when a root
requests no capture history, has no assertion bodies and does not use the existing
terminal-class-repeat optimization. RunFrom retains assertion/history/terminal
handling. No new helper, runner, hint, prepared state, mode or unchecked API.

Only Lookahead/Lookbehind suspend RunSegment and own assertion-body entries. A
fresh root's RequiredEnd is null; no-history outcomes already have null histories.
The otherwise empty continuation cleanup releases zero, without charges or host
callbacks. Workspace initialization/admission/reset, instruction/work/backtracking/
depth charges, capture trails, failure cleanup, RTL normalization, UTF8/scalars,
aliases/results and original interruption/publication stay unchanged. No public
contract or numeric accounting revision.

[CPU attribution](../PythonSplitProfile-20261105/README.md) motivated the change;
its sampled method shares are not predicted savings. [Release FullOpts](codegen-summary.json)
shows the direct RunSegment leg,836->1078bytes in TryMatchAt. The method still calls
RunFrom for general cases; no method-wide call-removal or isolated speedup claim.

Windows/Linux focused Release each.NET10/11 pass Core1645/Python1075/PCRE241.
Existing tests cover assertions/history, captured/global roots, BMP/astral/folds,
RTL/NonBacktracking, malformed full subjects, finite work/depth/working/results,
original token/deadline and retained cursor/results. No forwarding/existence test.
All nine independent public digests agree. Matching0dc baseline reused without
rebuild, on receipt-only parent6328eb74. Only Core changes; six companions/caller
remain byte-identical.

ONE **106.286s** separate-Default-process ABBA, normal8CPU startup thenCPU2,
producer1s/min16/BCL250ms/min4 warmup, independent60ms calibration/five alternating
samples. All180producer samples exceed41.612ms. Complete typed results are digested
after timing. [All observations/identities/allocations](cost-summary.json).

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| Unchanged Core Count, FIRST |2.501|2.488|−0.52%|0/0|
| Actual Unicode Split |129.014|99.239|−23.08%|19512/19512|
| Same finite/live |160.650|134.432|−16.32%|23712/23712|
| Same deadline/live |216.813|180.796|−16.61%|23760/23760|
| Captured Unicode FindAll |229.526|205.571|−10.44%|25248/25248|
| Long astral Split |1744.127|1363.895|−21.80%|49272/49272|
| Assertion-driver control |420.138|419.282|−0.20%|8080/8080|
| Captured ASCII Split neighbor |178.445|147.862|−17.14%, wider before|86264/86264|
| Core category-mask neighbor |279.412|232.782|−16.69%|64/64|

Actual after-process medians100.284/98.164us beat both before125.777/132.390.
Finite134.761/133.515 versus156.285/165.309 and deadline178.642/183.584 versus
212.853/220.422 support the controlled gains. Captured ASCII's before167.024/
190.654us varies substantially, versus148.039/147.734 after: retain the pooled
17.14% and uncertainty, rather than claiming that precise effect is stable.
The category workload is an affected neighbor, not the unchanged control. Do not
normalize using either control. All allocations stay equal. No confirmation,
tuning, profiler replay, CPython Status or universal10% threshold.

Immutable artifacts/experiments/root-segment-dispatch-20261105:

- evidence.tar.gz: `717152B02026CD989A22244E71EE4464D9B6DEC1E4A6A97355FF31A72F22964D`.
- windows-correctness.zip: `738D45C3623529929127F59E26193B0539BC732F0AC5933212FE36FFFA3AFD27`.

Declaration9201287A/patch048EB65D, source, before/after graphs, codegen, caller,
preflights, every sample and terminal0 remain. This comparison precedes the next
common package version. Batch final catalog/platform/package/consumer validation
once at that coherent delivery boundary; do not refresh one row immediately before
the Full run. Staged/published packages and previous Full/snapshots remain immutable.
