# POSIX byte-chart layout feasibility

Functional design at frozen source faa48f5730df5c6c5f56c10222bfc12fdba6c5de.
No shipped source, package or public performance change.

One Release net11 caller build (33.35s) references existing Core/Posix DLL/PDB
pairs directly. The production ERE parser supplies syntax; runtime dependency
evaluation and group projection use explicit stacks and valid byte boundaries.

14,009 checks over210 programs pass in0.405s:8606 independent small winners,
5283 independent false closed intervals and120 prior-model scale winners across
ASCII/BMP/astral8/32/64/128 scalars. Large scale winners are frozen model
observations, not new independent proofs. No correction or confirmation.

Maximum per case:12,358 memo cells,41,693 dependency/projection transitions,
73 dependency frames,five projection frames,45 immutable syntax records.
Predeclared caps:100,000 cells,1,000,000 transitions,10,000 frames,30s. Actual
x64 SizeOf:syntax24B/key16B/decision4B/dependency28B/projection16B. Dictionary
capacity/overhead are additional; these sizes are not a managed-memory guarantee.
The earlier scalar model includes endpoint search; its larger counts cannot
be treated as an equal-work memory or performance comparison.

The immutable value records retain no parser objects. Memo keys contain only
syntax,byte interval and bounded/saturated count; values select a split/branch.
There is no trace copy,winner tree,input map or CLR recursion in the evaluator.
Streaming selection clears captured descendants when a new enclosing occurrence
starts, preventing stale inner results. The earlier closed-scope substitution
and finite key-space arguments still apply; general state may be quadratic.

Production remains blocked on concrete conservative major-capacity admission,
old/new overlap, original interruption/refunds and useful dependency depth;
bracket predicates must reuse existing tables. Public work/depth/memory policies
have not been qualified by these private model counters.

Preserve more than the static FirstAcceptanceIsLongest route: current dynamic
root/nested fast paths also use best-copy/prefix machinery with that flag false.
A blanket chart route would not establish preserved native costs or justify
removing every trace/undo consumer. Resolve those duties without a second general
executor,new switch,threshold,dormant engine or reclamation framework. Reuse
existing invocation-owned capture ranges and cursor/result lifetime authority.

Next adoption gate: affected public captures and original interruption/admission
checks, then one bounded comparison of unresolved captured contexts and ordinary
SKU/root/nested/early-result controls under both input policies. No unchanged
model replay or package matrix follows from this functional result.

Raw local evidence: artifacts/experiments/posix-byte-chart-20261112/evidence.tar.gz.
SHA256: 177178051E9430DD143DC874C3B6A39B449C8D713124CD3B73779C1E58EA122F.
Source,fixture generator/independent oracle identity,all14009 fixtures,typed
implementation,binaries/PDBs,results,preflight and integration duties are preserved.
