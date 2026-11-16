# One-shot Python text reuses ledger admission

Accepted: remove the always-null character-count parameter and forced selected-
range CountUtf16 call from MaterializeMatchText. Native/scalar one-shot strings
use the existing result ledger, as batch strings already do. No new decoder,
field, cache, execution mode, API or subject-validation pass. Larger ranges keep
bounded exact sizing. Up to4096 UTF8 bytes use conservative byte-derived capacity:
short Unicode one-shot results may need more working/result allowance and deny
earlier. Admission precedes allocation; original interruption/refunds remain.
SPEC and profile README record this intentional capacity/work revision.

Windows and SAME-byte Linux PythonRe5226 checks pass per.NET10/11, including
4095/4096/4097-byte public result admission, native/scalar text, offsets, Unicode,
work cutoffs, cancellation/deadlines, retained values and funded refunds.
Core DLL/PDB stays identical; only Python and its changed test host build.

The initial timing stops at its first row's20ms sample floor, before producing a
complete comparison. Its raw failure is retained. The SOLE confirmation reuses
identical producer pairs/tests, with1s warmup and one calibration confirmation.
One completed102.406s ordinary-process ABBA/180s cap,12 public operations, five
150ms-target samples/process/row, all>=20ms; normal8CPU startup thenCPU2. Complete
nullable strings/tuples and funded refunds checked after timing. Caller-valid
unchanged UTF8, Python re over str; finite/funded row includes live token/deadline.

| Public operation | Before us | After us | Change | Sample ranges |
|---|---:|---:|---:|---|
| Scalar ASCII search text | .372 | .364 | −2.34% | overlapping |
| Scalar Unicode match text | .315 | .302 | −4.15% | separated |
| Scalar search at byte offset | .323 | .316 | −2.03% | separated |
| Funded Unicode match text | .728 | .691 | −5.13% | separated |
| Scalar Unicode literal search text | .212 | .205 | −3.19% | overlapping |
| Native DotAll full-match text | .143 | .140 | −1.88% | overlapping |
| Scalar4096-byte full-match text | 2.601 | 2.505 | −3.67% | separated |
| Larger output, existing sizing | 241.821 | 241.702 | −.05% | overlapping |
| Unchanged miss path | .130 | .126 | −3.09% | separated |
| Unchanged Python tuple output | 414.749 | 371.857 | −10.34% | separated |
| Identical Core literal Count | 12.055 | 12.034 | −.18% | overlapping |
| Identical.NET Count | 22.176 | 22.192 | +.07% | overlapping |

All allocation medians agree. Keep the smaller preparation path and focused
benefits, retaining separated unchanged-Python movement and all process ranges;
do not attribute that control movement, normalize or claim exact causal gains,
whole-profile speedup or CPython parity. The initial native-Unicode-search label
was rejected by public preflight and corrected before timing; DotAll's native
route is independently asserted. No further confirmation/tuning.

CoreFBBC8B3D is fixed; Python3472A74E→AD728320. DLL/PDB,393 compiler documents,
input/member/caller identities verify. Every older/dev.101 package remains
unchanged,676 feed files. No package/Full or canonical refresh. Raw verified offVM;
initial worker/collector exit1, confirmation worker/collector exit0. Both owned
VM task roots retired after accepted hashes and zero live references.

Artifacts: artifacts/experiments/python-one-shot-ledger-20261116/ and
artifacts/experiments/python-one-shot-ledger-measure-20261116/. SHA256:
initial6B72A8256E6367DA985B54C31B2842FD8BE31DCA21558E5810A9E252DA3E6838;
confirmation701E6BA8DF2CBCE484864E9B93D17D39F9FB8DC3EF6AD46AAA4D13F4AFDC5107;
validation CCEAED9AC284A47DC29F91B30F21DAB19750CE48DB1840D66B072DA1E32D8712.
