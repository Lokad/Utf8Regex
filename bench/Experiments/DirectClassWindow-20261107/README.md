# Rewind only the actual class before a direct delimiter

The shared candidate helper previously rewound a whitespace run before the
leading class even for a direct class/literal prefix. Nonmember whitespace
cannot join that prefix; this admitted impossible starts on mail mentions.
Reuse its existing direct-shape proof and omit that unrelated rewind. The actual
scalar predicate still includes whitespace members and respects repeat bounds.
Explicit whitespace/gap prefixes retain their conservative union; the existing
negated-pair SIMD path, complete verifier and allocation admission stay unchanged.
No helper, state, framework, mode or public API is added.

An untimed production-verifier replay of the exact6839410B corpus retains all
92matches/2856windows while attempts fall12531->3780 and observed work142304->57373.
This attributes removed work, not CPU time. The changed production helper agrees
with the earlier diagnostic class-only calculation. Scalar/capture/reference,
original cancellation/deadline and lifetime contracts remain. Numerical work
requirements can decrease; Core/Python specifications and release notes say so.

ONE [public comparison](cost-summary.json), **98.452809381s**, separate Default
ABBA processes:19workloads/380samples/min71.517ms. Source01ff9438 plus the archived
patch, CoreAD747E5B ->DE8A5516; Python2CD2D361,PCRED2494007 and caller0D0D9427
bytes/PDBs fixed. Normal eight-CPU startup thenCPU2, default tiering/PGO,750ms/
min32warmup, independent75ms targets and five samples. One calibration
confirmation precedes sampling. Results are consumed and checked after timing.
No noise confirmation, tuned variant or new benchmark Status follows.

| Public Count operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual mail |3593.35|2246.20|-37.49%|1544|
| Same finite/live |5002.13|3336.09|-33.31%|1656|
| Same deadline/live |5630.95|3562.56|-36.73%|1704|
| Dense class union |133.09|126.32|-5.09%|0|
| Long captured class |52.58|52.04|-1.02%|64|
| Partial-letter neighbor |146.92|153.81|+4.69%|0|
| Folded category control |155.60|150.35|-3.38%|0|
| Explicit whitespace control |179.70|180.53|+0.46%|1072|

**Promote:** the three actual mail gains and dense-class gain hold in both
processes with disjoint overall ranges. All19allocation medians are unchanged.
Partial-letter+4.69%/+6.889405us has higher medians in both after processes but
overlapping overall ranges; retain the cost. Whitespace+0.46%/+0.824459us and
Unicode-hit+0.77%/+11.169ns also overlap. The unchanged native Core literal
control improves3.79% with disjoint ranges; give it no causal credit and do not
normalize against it. The archive retains every process/sample and allocation.
Finite/live adds1408.77us before and1089.89us after versus ordinary matching:
absolute overhead falls, while relative overhead rises39.20%->48.52%.
This source A/B does not revise qualified dev.49 ratios or erase its regressions.

Focused Release checks pass on Windows/Linux and each .NET10/11: Core1649,
Python104,PCRE2=46. The final delimiter fixture adds two whitespace-literal rows
after timing and passes40cases per runtime/OS; only tests changed and Linux Core
DLL bytes still match the measured after graph. Coverage includes class-owned
whitespace, explicit gaps, bounds, Unicode, captures/references, positive work
cutoffs/exact boundary, parent loans and original interruption. Initial harness
fixes used the existing diagnostic clock, adequate finite depth and neutral
failure mapping; the fixed PCRE reference path was corrected before timing.
Failed/corrected preparation logs are preserved. [Acceptance](acceptance.json).

Immutable off-VM evidence:
`artifacts/experiments/mail-failed-window-20261107/evidence.tar.gz`,
`44D08BEF13E44F0927A9752E3BB8CA5509730D7113D32478A4A6765FE9CE08C4`,9507211B.
It holds source/patch, both graphs/PDBs, worker/declaration, replay and all samples/
checks. Supplemental final tests/patch are hashed in acceptance. All264staged
assets and frozen Full49graph remain unchanged. No individual-change package/
Full matrix or NuGet publication was performed; batch delivery remains separate.
