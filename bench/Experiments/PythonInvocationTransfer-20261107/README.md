# Borrow invocation values during Python cursor construction

Two internal constructor parameters now use `scoped in Utf8SliceInvocation`.
The cursor chain borrows the source until PythonReScalarMatchState takes its
existing owned value copy. Scoped references cannot escape; inline counters,
shared authorities, original tokens/deadlines and cursor/result lifetimes retain
their existing behavior. No state, helper, execution mode, allocation or public
API is added. There is no revised resource or matching contract.

Untimed matching Linux Release FullOpts listings identify outgoing copies in
CreateScalarCursor before the change. They disappear with readonly borrowing;
the method shrinks507->356code bytes. MatchValueScalar410 and state constructor850
remain unchanged. Other state/cursor copies remain. Code size is attribution,
not a public speedup claim.

ONE [public comparison](cost-summary.json), **87.577385798s**, separate Default
ABBA processes:17workloads/340samples/min51.8523ms. Source2aaf9d71 plus the archived
patch, Python1C49B5CB ->EED2DFAF; CoreAB81495C and caller bytes/PDBs stay fixed.
Normal eight-CPU startup precedes CPU2 affinity, default tiering/PGO,750ms/min32
warmup, independent75ms targets and five samples. One calibration confirmation
precedes sampling; there was no noise rerun, tuning variant or package refresh.
Results are consumed and checked after timing.

| Public operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual folded Search miss |0.370817|0.342712|-7.58%|0|
| Same finite/live |0.509763|0.482956|-5.26%|112|
| Same deadline/live |1.000077|0.938442|-6.16%|160|
| Folded Search hit |0.319961|0.293651|-8.22%|0|
| Long folded miss |0.492097|0.462144|-6.09%|0|
| Unicode folding partners |0.502130|0.460568|-8.28%|0|
| Anchored FullMatch |0.294690|0.259078|-12.08%|0|
| Captured class FindAll |0.884529|0.835969|-5.49%|128|
| Dense false starts |20.590106|20.754969|+0.80%|0|
| Fixed-Core partial class |128.996792|133.097407|+3.18%|0|
| Actual mail control |3529.447727|3522.150000|-0.21%|1544|
| Finite mail control |4968.340625|5008.200000|+0.80%|1656|

**Promote:** the eight target gains above hold in both processes with disjoint
overall sample ranges. All17allocation medians are unchanged. Adverse dense
false starts+0.80%/+0.164863us, finite mail+0.80%/+39.859375us and fixed-Core
partial class+3.18%/+4.100616us have overlapping ranges; folded Core control+0.81%
also overlaps. Preserve these costs without normalization or attributing changes
in identical Core bytes to an implementation edit. Late-hit ranges overlap too.
[Every process/sample](process-observations.json) retains all17rows and the
small Unicode-literal control's outlying sample. This source A/B does not update
catalog Status, establish .NET/CPython parity or erase dev.49 package regressions.

Focused Release tests pass482PythonRe cases per .NET10/11 on Windows and Linux.
Matching binaries pass102additional lifetime checks per runtime/OS: original and
advance tokens, deadlines, persistent cursor aliases, retained results, shared
working allowances and projection authority. No test mirrors the parameter edit.
The full compiler enforces scoped borrowing, including returned span cursors.

Canonical off-VM archive:
`artifacts/experiments/python-invocation-transfer-20261107/evidence.tar.gz`,
`E6BA2C8C51F1DA3B6375FADFD229DB213D7D7C6B98C9D2E9B7091C1B8E607DCD`,5826116B.
Both graphs/PDBs, source patch, worker/declaration, code generation, initial checks
and all samples are immutable. Supplemental lifetime evidence is recorded in
[acceptance](acceptance.json). Frozen Full49 graphs and all264staged package/symbol
assets remain unchanged. No Full/package matrix or NuGet publication followed.
