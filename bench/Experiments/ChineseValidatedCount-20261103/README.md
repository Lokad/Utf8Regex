# Checked Chinese validation/count fusion: rejected

**Rejected and fully reverted**, source `fd765532`, revert `0c688527`.
The real813478B mixed-width Chinese literal target regressed **170–171%** with
tight samples. Dense mixed hits and short subjects improved, but this does not
advance the leading public workload. No confirmation, variant or tuning campaign.

The prototype added a checked common-UTF8 SIMD phase to the existing scalar
validated counter. Its same loaded vector correlated the first complete scalar;
overlapping at most two tail bytes kept subsequent starts scalar-aligned. Full
SequenceEqual and nonoverlap checks remained. Eligibility widened from all-three-
byte literals to three-byte-leading literals. No retained storage, execution mode,
unchecked API or resource-accounting change was introduced. All added code and its
candidate-only tests are removed; no dormant implementation remains.

One **94.503s**, 15-case public A/B used frozen clean `87db1ced` Full DLL/PDBs.
Only candidate Core was built; baseline, benchmark callers, PythonRe and PCRE2
bytes stayed identical. Normal eight-CPU startup then CPU2, existing2s/min512
warmup, independent60ms calibration, five alternating samples, >=53ms batches,
and complete output checks after timing. Allocation medians were unchanged.

| Public Count operation | Before us | Candidate us | Change |
| --- | ---: | ---: | ---: |
| Actual Chinese, ordinary |133.992|362.120|+170.26%|
| Actual Chinese, compiled |133.886|362.891|+171.05%|
| Finite/live token |1244.437|1230.116|-1.15%|
| Deadline/live token |2041.657|2027.397|-0.70%|
| Dense mixed hits |27.47|9.18|-66.60%|
| Dense first-scalar misses |2.74|9.46|+245.25%|
| Dense all-three-byte hits |6.58|8.87|+34.69%|
| Corpus plus supplementary tail |134.56|362.24|+169.21%|
| Short mixed subject |0.23|0.12|-47.16%|
| RTL control |134.04|479.94|+258.05%|
| NonBacktracking control |133.58|363.25|+171.94%|

[All observations](summary.json) retain constrained scalars, Cyrillic/family/ASCII
controls and spreads. Both real-target spreads are at most1.02; noise does not
justify confirmation. The public RTL/NB costs also show that private front-gate
exclusion assertions are not sufficient evidence for unchanged public routes.
No private-kernel claim, speculative code-generation cause or universal regression
threshold is needed for this rejection. The target moved strongly the wrong way.

Before timing, Release focused137 checks/target and hardware-intrinsics-disabled12
checks/target passed on .NET10/11, including every vector offset/tail, widths1–4,
constrained E0/ED/F0/F4, malformed global offsets, nonoverlap, numeric admission
and original cancellation. After revert,125 focused checks/target pass. Production,
test and benchmark-project trees match `d05b008d` exactly. VM source is restored,
and its original Core DLL/PDB bytes were copied back without rebuilding baseline.

Immutable evidence in `artifacts/experiments/chinese-validated-count-20261103/`:

- `evidence.tar.gz`: `A3BFD31C884B2671AC616EAB36D18C4806691A6D539CFC76399210DE572AC141`.
- `correctness-source.zip`: `5B616EE71D3AF9CAAE474F89FC02EC257FCE1979CFB052DC95239EAF883626B4`.
- `restoration.zip`: `3ECD4583A3F66FB7804805110970260232AF6C36E9F0471081D6B6F15EAD34A0`.

Dev.19 packages, its43m08s Full refresh, published tables and older experimental
evidence remain unchanged. Defer this fusion approach. Next inspect the4.39x
short ASCII IsMatch gap's live validation entry, separately from the previously
rejected selector modification; no new prototype or measured gain is claimed yet.
