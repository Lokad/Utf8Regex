# Borrow immutable NonBacktracking anchor instructions

The packed conditional-row loop now borrows its checked instruction array element,
and the existing anchor helper takes that instruction by readonly reference.
This removes a local/outgoing value copy on a live route. Algorithms, allocations,
all work charges, admission, original-token cancellation/deadlines, scalar matching,
captures and cursor/result lifetimes are unchanged. No public contract change,
new metadata, engine, mode, unsafe access or framework.

[Read-only attribution](attribution.json) on the exact canonical region corpus
(46429bytes/46410scalars/15states) identifies preparation as dominant. Independent
phase timings are not additive or public speedup evidence. [Current VM Tier1
codegen](codegen-summary.json) verifies removal of the outgoing anchor instruction
copy; the anchor call remains. Packed code decreases2244->2187bytes, which by
itself establishes no execution-time claim. The atom helper was already inlined;
this change preserves every direct atom check and every existing work quantum.

One [predeclared](PREDECLARED.md) [public comparison](comparison.json) took91.354s
on the quiet Ubuntu VM, .NET11RC3, CPUmask4. Fourteen cases/four equal-result lanes,
1s/min64 warmup, independent60ms calibration,5alternating samples (minimum59.624ms).
Full UTF-8 validation is included; completed result batches are consumed after
timing. Frozen source092b8e68 before; parent322f4b0f plus the archived two-line
patch after (intervening Core production source is unchanged).

| Public operation | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| region-ordinary | 1385.564 | 1325.541 | -4.33% |
| region-compiled | 1430.500 | 1383.270 | -3.30% |
| region-live | 1446.100 | 1402.277 | -3.03% |
| region-finite-live | 1571.553 | 1551.795 | -1.26% |
| region-deadline-live | 2826.316 | 2892.434 | +2.34% |
| unicode-nb | 342.554 | 335.137 | -2.16% |
| wide-nb | 2391.431 | 2388.456 | -0.12% |
| nullable-nb | 81.175 | 79.605 | -1.93% |
| dense-ascii-nb | 329.573 | 311.810 | -5.39% |
| dense-unicode-nb | 235.280 | 225.128 | -4.32% |
| category-count-deadline-live | 648.538 | 654.495 | +0.92% |
| category-single-search-deadline-live | 1158.394 | 1114.025 | -3.83% |
| non-nb-scalar-control | 4.741 | 4.750 | +0.19% |
| literal-control | 0.405 | 0.405 | -0.08% |

**Promote.** All ordinary/compiled primary pairs improve3.06..5.31% and2.92..3.69%
respectively. Deadline-enabled primary+2.34% is retained, with all its pairs
+0.53..3.99%; category deadline+0.92% and non-NB control+0.19% are also retained.
Every lane's allocation is unchanged. [Pairs/spreads](summary.json) retain raw
uncertainty; no confirmation or variant campaign was needed. These are incremental
public costs, not universal parity or cumulative release qualification.

[Release checks](verification.json) pass709 focused Core and28 NonBacktracking
differential checks per runtime. [371 state probes](state-verification.json) are
byte-for-byte identical before/after, including viability, ALL work counts,
captures/offsets, replay, working peaks/admission and13-byte loan refunds.
No new test framework or forwarding executor was introduced.

- [Raw measurements, current before/after codegen, harnesses, patch and DLL/PDBs](../../../artifacts/experiments/nb-instruction-borrow-20261101/evidence.tar.gz):
  `89515743CD4601501AF39ADF78364F4EF2560E860655DC0F9A20874556D2FF83`.
- [Exact prototype source/correctness logs](../../../artifacts/experiments/nb-instruction-borrow-20261101/correctness-source.zip):
  `B527515C54FFFE09FF031797B468AB3A282916D07796E81C411EC2594CA9C86F`.

No package/full-catalog qualification was repeated. Historical/staged bytes remain
immutable; a fresh final-source package/platform/cumulative gate stays separate.
Only the region rows were selectively refreshed from clean committed source
`60563cb3`: ordinary1346.065us/37.60x and compiled1350.179us/37.74x predecoded
.NET11, both still Gap, allocations557416bytes (previous38.57x/38.83x). These
separate point estimates are not the controlled A/B percentage. All30 samples
exceeded47.719ms. Allothercases, identities, statuses, lifecycle/scaling and
worst-first ordering remain unchanged. Existing parity verification and3snapshot
guards per runtime pass. No broad package/platform/catalog repetition.
[Selective raw/generated/binary evidence](../../../artifacts/experiments/nb-instruction-borrow-20261101/selective-evidence.tar.gz):
`E39E5FC9FD0B4791BB047BD83E63E32FE36B79B2D8ABF2737C020FB9392FF357`.
Committed Core DLL SHA256:
`AE3926ED79BB72CA7DC09317F2E5236A61272734BB8041EE9732D492D3678BC4`.