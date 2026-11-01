# Construct the Python invocation once

Accepted source replaces two conditional whole-invocation construction returns
with one constructor call using the optional program allowance. The existing
internal constructor's nullable annotation now reflects its already-supported
null initialization. Public null checks, funded ownership, original interruption,
numeric budgets and metering are unchanged. No fields, modes or new helpers.

Controlled Release Tier1 inspection of actual public calls found larger Python
wrappers versus the fixed original, despite identical regex instructions/masks.
This supported a construction-copy hypothesis, not a throughput conclusion.
The [original/parent inspection](original-parent-codegen-summary.json) and
[candidate inspection](candidate-codegen-summary.json) retain the actual results:

| Tier1 method | Parent code bytes | Candidate code bytes |
| --- | ---: | ---: |
| CreateScalarCursor |1852|1478|
| FullMatchCore |3918|3586|
| MatchValueScalar |3130|2806|

The cursor wrapper's stack reservation falls0x5D8→0x508. Both versions use the
same six typed public lanes, default tiering/PGO and one interleaved warmup.
These are controlled code-generation observations, not exact production PGO or
causal explanations for every remaining failed public row.

One102.939-second public A/B,18cases/four equal-result lanes on the dedicated
.NET11RC3 VM/CPU2. Frozen `59d507af` versus that source plus the archived patch;
existing repeat16 plus variable FullMatch finite/deadline cases. Typed calls,
actual-loop1s/minimum1000call warmup, independent60ms calibration, five alternating
samples of at least40.701ms. Range/count results are consumed after timing; both
predecoded/decode-then BCL lanes cover overlapping semantics.

| Public FullMatch | Before us | After us | Change |
| --- | ---: | ---: | ---: |
| Exact-class miss |0.380|0.352|−7.24%|
| Exact-class hit |0.391|0.376|−3.87%|
| Exact-class finite/live miss |0.486|0.472|−2.77%|
| Variable repeat |0.828|0.821|−0.83%|
| Lazy repeat |0.611|0.617|+1.03%|

[All costs, allocations, spreads and paired observations](summary.json).
Allocations are unchanged in every case. Largest adverse control median1.90%,
maximum adverse pair6.17%; the captured-body candidate spread1.130x remains.
No confirmation or variants. Accepted for a smaller implementation and measured
short exact-class gains; **no useful variable/lazy regression repair** was found.
Their fixed-original failures and other release performance blockers remain open.
No package, catalog or Status refresh occurred.

Full PythonRe4530 and focused Core405 Release checks pass per runtime on.NET10/11.
Existing coverage includes shared/funded working and program admission, closing
with active users, retained aliases/results, every cutoff, loans, original tokens
and deadlines. No public contract or accounting scope changes.

Immutable artifacts:

- `artifacts/experiments/python-invocation-construction-20261101/evidence.tar.gz`, SHA256 `CE9662FDC65B261C73A22D7AF9EBF22C45BE04230214DA5D464C1FDFEBF148D9`.
- Its `correctness.zip`, SHA256 `0479B9481E0ED1E91BC92DD147E0612D0578D3442051A74BEA561D6496149685`.
- `artifacts/experiments/remaining-gate-codegen-20261101/evidence.tar.gz`, SHA256 `3E7599CCB2708E6AB3BF9934D9ADB5E6013B95530B30F96E0542226EBEA85CB1`.

They preserve DLLs/PDBs, loaded identities, source/applied patch, driver, original
and candidate codegen, predeclaration, preflight, raw samples and correctness logs.
