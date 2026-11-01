# Packed-row work sharing: rejected

The prototype carried unused work allowance across complete NonBacktracking
viability rows: for width w<=64, floor(64/w) rows per admission. Every atom
charge and direct interruption check stayed, with no storage/layout/engine changes.
The production implementation and proposed contract changes were fully restored.

One [predeclared](PREDECLARED.md) quiet-VM [public comparison](comparison.json)
took 92.073 seconds on .NET11 RC3, CPUmask4, 14cases/four equal-result lanes,
1s/min64 warmup, independent60ms calibration, five alternating samples (minimum
60.946ms). [Paired results](summary.json) show stable regressions well above10%,
so no confirmation or further variant campaign was justified. This is a public
performance rejection; correctness passed and allocations were unchanged.

| Public operation | Before us | Prototype us | Change |
| --- | ---: | ---: | ---: |
| region-ordinary | 1372.783 | 1338.666 | -2.49% |
| region-compiled | 2323.502 | 2931.031 | +26.15% |
| region-live | 2267.944 | 2854.306 | +25.85% |
| region-finite-live | 2365.294 | 2921.447 | +23.51% |
| region-deadline-live | 3153.044 | 3474.103 | +10.18% |
| unicode-nb | 463.289 | 593.682 | +28.14% |
| wide-nb | 2394.134 | 2359.972 | -1.43% |
| nullable-nb | 111.717 | 111.760 | +0.04% |
| dense-ascii-nb | 468.332 | 614.216 | +31.15% |
| dense-unicode-nb | 299.851 | 379.843 | +26.68% |
| category-count-deadline-live | 661.458 | 578.558 | -12.53% |
| category-single-search-deadline-live | 1193.356 | 1153.972 | -3.30% |
| non-nb-scalar-control | 4.837 | 4.773 | -1.31% |
| literal-control | 0.395 | 0.392 | -0.64% |

All compiled-region pairs regressed25.30..28.25%; dense ASCII30.20..32.17%.
Ordinary-region/category improvements do not compensate. No specific JIT cause
is established by these timings. Keep existing per-row admission; do not describe
this unshipped numeric work change as the current contract.

[371 production-state probes](state-verification.json) preserved viability bits,
captures, offsets, replay work, storage and loans, apart from exactly predicted
row-unit reduction. [Release checks](verification.json) passed709 Core and28
NonBacktracking differential checks per runtime. After restoration709 Core checks
pass per runtime, including14 retained theory rows proving the EXISTING per-row
work, every refusal/loan refund, UTF-8 offsets and actual-row token/deadline checks.
The retained tests exercise production directly, without new diagnostic hooks.

Source parent5973835d; beforebinary source092b8e68. Frozen before/after DLL/PDBs,
harnesses, raw durations, state probes and patch are retained in the immutable
[raw archive](../../../artifacts/experiments/nb-row-quanta-20261101/evidence.tar.gz):
`BA5069DF3C39E14EB51DE9C49920BB4CE50AA184102D58BE5BC0BA8A8E3B963D`.
[Prototype source/correctness logs](../../../artifacts/experiments/nb-row-quanta-20261101/prototype-correctness.zip):
`A3F36EAF1376A174353A989AA98A2A9346E3E8F566F2AFEE993B1BAC6EBBFF9C`.

No benchmark snapshots were refreshed, packages regenerated or full matrices run
for the rejected change. Published/staged artifacts remain immutable.