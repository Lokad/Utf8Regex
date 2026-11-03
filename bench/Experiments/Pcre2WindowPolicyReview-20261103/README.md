# Reassess bounded PCRE2 windows under the local-trade-off policy

Accepted source `6b17c1e0`: the same three-line proof archived on November1 now
admits zero-width word/nonword boundaries within an otherwise ASCII-consuming
prefix. The existing <=64-byte literal-window analysis, candidate selector and
scalar VM still own boundary checks, captures, options and interruption. There
is no new executor, mode, storage mechanism, unchecked access or Core change.
The existing window payload/preparation is now reachable for these shapes.

The [November1 rejection](../Pcre2BoundedLiteralWindow-20261101/README.md) and its
raw data remain unchanged. That decision applied a universal10% ceiling. The
user now accepts local regressions for relatively major gains; this reassessment
uses current binaries and preserves all observed costs. It does not reopen the
separate rejected leading-set, state-reset, decode or diagnostic-charge ideas.

One **88.098s**, fifteen-case public A/B, baseline `d54d9cf2` graph versus the
current adapter. Core DLL/PDB bytes are identical throughout. Quiet VM,
.NET11RC3, normal eight-CPU startup then CPU2,2s/min512 managed and250ms/min128
comparator warmup, independent60ms calibration and five alternating samples.
Every result is consumed after timing; minimum24.963ms. No confirmation/variant.
Allocations are unchanged everywhere. Nonoverlapping .NET Unicode-boundary
semantics are explicitly excluded from diagnostic comparisons.

| Public operation | Before us | After us | Change | B/op before/after |
| --- | ---: | ---: | ---: | ---: |
| date-hit |1.520|0.654|-57.00%|0/0|
| date-miss |5.466|2.504|-54.19%|0/0|
| finite-hit |2.541|2.708|6.60%|1592/1592|
| finite-miss |8.241|8.683|5.37%|1592/1592|
| deadline-miss |8.968|9.728|8.48%|1640/1640|
| no-digit-miss |351.080|0.265|-99.92%|0/0|
| dense-candidates |455.911|248.430|-45.51%|0/0|
| captured-count |64.429|71.588|11.11%|0/0|
| ucp-control |2.426|2.821|16.31%|0/0|

[All observations](summary.json) retain deadline hit, dense/nonword Count,
Unicode and unchanged controls. UCP+16.31% (~0.40us) and capturedCount+11.11%
(~7.16us) are explicit accepted costs beside date-54..57%, dense failures-45.5%
and no-digit misses-99.92%. Tight UCP spreads support recording that cost; its
runtime cause remains unresolved. Do not invent JIT attribution. Unchanged
candidate/literal controls move+0.48/+0.83%, not claimed source effects.

Full PCRE2 Release checks6498/runtime pass on .NET10/11. Restored semantic tests
compare against the production VM with candidate search disabled at every scalar
origin, preserving captures, embeddedNUL, Unicode context, width/options/UCP
exclusions, malformed tails and original-token cancellation. Existing finite,
deadline, lifetime and resource tests remain. Numeric work thresholds can change
with route selection, as already documented in SPEC-PCRE2; no public API change.

Only two date records are selectively refreshed. The standard point refresh
leaves native Status Unqualified until the existing paired protocol runs; exactly
these two rows then receive nine alternating pairs using the same built graph.
Native ratios move **9.40x/8.96x ->4.221x/4.219x**, still NativeFaster,0B. Intervals
are[4.201,4.265]/[4.212,4.234], minimum paired samples36.616ms/39.147ms. This
protocol chooses the least-contended CPU (0x40/0x2), separately from the CPU2
causal A/B. All other cases, scaling and identities remain; existing Markdown
and qualification verifiers pass. Reused snapshot guard1/runtime passes.

The first export named the wrong benchmark-page path after both measurements
and verifiers passed. Corrected archive export reused those results/binaries;
no measurement was repeated. Historical dev.19 packages and frozen43m08s Full
remain unchanged; no package/platform/full-matrix qualification or publication.

Immutable evidence under `artifacts/experiments/pcre2-window-policy-20261103/`:

- `evidence.tar.gz`: `47415E75731028831E0E750DB2574C988E1D9A6C1E742807F5148959445EB229`.
- `correctness-source.zip`: `B49B475DD012F816E082083BB5429DC78814CF9B45734D4B153E6B3AA5ABEEB0`.
- `selective.tar.gz`: `3455EC945C1A2FDDF029A429948C6C87B0E50BB7A8EA31D57AFB96C8832112F0`.
- `paired.tar.gz`: `2625F6C489FE1B8BF9AAD13AA9945C371012D5CCE06507503FD5D1EFB3BF87AD`.