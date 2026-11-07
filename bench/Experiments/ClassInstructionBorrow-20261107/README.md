# Borrow the immutable scalar-class instruction

The private TryMatchScalarClass parameter now uses `in Utf8SliceInstruction`.
Its callers borrow their immutable instruction rather than passing a copy for
each classification. This single modifier preserves the predicate, scalar units,
work/depth/storage admission, original token/deadline checks and retained results.
No field, opcode, helper, mode, allocation or public API is added. Existing profile
specifications apply unchanged; release notes record the implementation change.

Untimed Linux Release FullOpts listings on matching source graphs show the repeat
loop's outgoing stack copy replaced by the instruction's address. Predicate code
is648->638bytes, the no-rewind proof4691->4270 and RunClassRepeat2165->1926. This
identifies a live copy cost; smaller code alone is not public performance evidence.

ONE [public comparison](cost-summary.json), **93.447169970s**, separate Default
processes in ABBA order,18workloads/360samples/min69.6775ms. Source2cc4f756 plus
only the modifier; CoreED9C2237 ->5442096D. Python5B75B7FA,PCRED2494007 and caller
F0D6C4D0 bytes/PDBs remain fixed. Normal eight-CPU startup precedes CPU2 affinity,
default PGO/tiering,750ms/min32warmup, independent75ms targets, five samples and
one calibration confirmation before sampling. Results are checked after timing.
No rerun, noise confirmation, Full or package qualification followed.

| Public Count operation | Before us | After us | Change | B/op both |
| --- | ---: | ---: | ---: | ---: |
| Actual mail corpus |3743.65|3586.41|-4.20%|1544|
| Same finite/live |5041.90|5044.46|+0.05%|1656|
| Same deadline/live |5668.98|5682.36|+0.24%|1704|
| Dense ASCII class union |138.71|129.33|-6.77%|0|
| Long captured class union |51.81|52.66|+1.63%|64|
| Partial-letter mask |155.69|148.38|-4.70%|0|
| Folded category |174.84|158.77|-9.19%|0|

**Promote:** mail, dense ASCII, partial-letter and folded gains persist in both
processes with disjoint overall sample ranges. All18allocation medians are
unchanged. Long captured+1.63%/+0.84337us has overlapping ranges, as do Unicode
hit+0.77%/+0.01165us, finite/deadline mail and other control movements. Retain
adverse observations without normalization or an unmeasured causal explanation.
[Every process/sample](process-observations.json) and [acceptance](acceptance.json)
remain separate from qualified .NET/CPython ratios or a benchmark Status.
This does not erase dev.49's cumulative mail/partial/folded regressions or claim
full parity; those immutable package observations remain valid.

Focused Release checks pass on Windows/Linux, each .NET10/11: Core1053,
PythonRe38,PCRE2=46. Existing checks cover scalar boundaries, full malformed input,
category folding/complements/subtraction, captures/retained aliases, RTL and
NonBacktracking, finite work cutoffs/exact boundaries, parent loans and original
cancellation/deadlines. No test mirrors the one-word implementation.

The copied declaration retains an erroneous BeforeVersion48 label. Its source,
Core/companion/caller hashes were correct before timing; all four loaded versions
are49+2cc4f756. [Label clarification](declaration-clarification.json) preserves this
metadata defect. This is a source A/B with fixed49companions, not another48->49
package comparison. No producer/caller/input/timing correction occurred.

Canonical off-VM archive:
`artifacts/experiments/class-instruction-borrow-20261107/evidence.tar.gz`,
`EF55E189607A0F7A5AD809D1DAFBC9F2BB5F79110A764273E68D59E161F7F543`,5747263B.
It preserves both graphs/PDBs, source patch, worker and declarations, every sample,
code generation and Windows/Linux checks. All sessions are terminal/closed.
All264staged package/symbol bytes and the frozen Full48/49 graphs stay unchanged.
No NuGet publication is authorized. Further qualification waits for a material
new delivery batch.
