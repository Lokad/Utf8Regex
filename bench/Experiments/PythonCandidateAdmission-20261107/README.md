# Candidate-admission short circuit: not retained

**Rejected; production restored.** The trial moved the existing pure native
eligibility query behind prepared-prefix admission in CanUseCandidateRegex.
Truth conditions, scalar verification, work/storage admission, original controls
and public APIs were unchanged. No helper, state, mode or framework was added.

The live Linux Release listing showed the original eager native query and the
trial's earlier prefix return.187->142code bytes confirmed less classification
work, not a public speedup. Prototype checks pass482PythonRe cases per .NET10/11
on Windows and Linux, covering folded/native/scalar/capture/alias/resource routes.

ONE [public comparison](cost-summary.json), **88.273327804s**, separate Default
ABBA processes,17workloads/340samples/min65.6941ms. Source9e2c10a4 plus archived
patch, Python90A679C8 ->042AF979; CoreE408EDFE and caller bytes/PDBs fixed. Normal
eight-CPU startup thenCPU2;750ms/min32warmup, independent75ms targets, five samples
and one calibration confirmation before sampling. Results checked after timing.

| Public operation | Before us | Trial us | Change |
| --- | ---: | ---: | ---: |
| Actual folded Search miss |0.366|0.355|-2.88%|
| Same finite/live |0.500|0.490|-2.02%|
| Same deadline/live |0.995|0.986|-0.91%|
| Folded Search hit |0.314|0.308|-1.98%|
| Captured class FindAll |0.868|0.888|+2.26%|
| Unicode literal miss control |0.150|0.159|+5.43%|
| Fixed-Core partial class control |124.30|131.08|+5.46%|
| Actual mail control |3466.33|3468.97|+0.08%|
| Finite mail control |4866.94|4934.18|+1.38%|

The target miss improves in both processes with disjoint overall ranges, but the
absolute saving is only10.527ns. The Unicode literal cost8.176ns and identical-Core
control cost6.7826us are also persistent/disjoint. The latter cannot be assigned
to a changed Core implementation: Core and caller bytes are identical. Keep all
observations, rather than normalize against that control or invent a code-layout
cause. Finite/deadline/hit, captured FindAll and mail ranges interleave. Every
allocation median is unchanged. [Process data](process-observations.json) retains
the complete inputs, results, calibration/sample durations and producer identities.

The small target improvement does not justify retaining this trial amid its
adverse neighbors and control movement. This is a bounded decision, not an
above10% rejection or proof that short-circuiting generally regresses. No noise
confirmation, tuning variant, package qualification or Full refresh was run.
No new benchmark Status or .NET/CPython parity claim is made.

Canonical off-VM archive:
`artifacts/experiments/python-candidate-admission-20261107/evidence.tar.gz`,
`DB9680AADF41ECD4332C73EBCEED7B7085D37F979CBB41601B71801DA956FB02`,5966465B.
The exact patch, before/after graphs/PDBs, worker/declaration, code generation,
Windows/Linux checks and every sample remain immutable. Restoration validation
is recorded separately. All264staged package/symbol bytes and frozen Full49
assets remain unchanged; the previously accepted instruction-borrowing gain stays.

Restored production passes the same482checks per runtime/OS. Windows DLL bytes
match the baseline; source C# is unchanged. [Restoration](restoration.json) records
the closed checks. Supplemental `restoration.zip` SHA256:
`94992359A166627B799954EDB7D1535AC0AACC99983E03FB8987F09B3991CA3C`.
