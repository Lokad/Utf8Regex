# Inline the existing deadline checkpoints

Two AggressiveInlining annotations on shared Poll/CheckNow remove helper calls
from controlled matching. Their bodies, work units, clock reads, polling cadence,
original token/deadline, major admission, allocation mechanisms and public API
are unchanged. No engine, owner, mode, abstraction or resource-contract revision.
Actual Unsafe.SizeOf remains. Development118 source is separate from immutable117.

The prior token-borrow hypothesis stopped without a prototype or timing: actual117
Release code already scalar-replaces token fields. The older FamilyDependencies
fabricated-plan removal is also complete; neither experiment is repeated here.

## Release code generation

Existing public verification forces the production simple-branch matcher. Before,
it has two Poll and four CheckNow call sites; after, all six are inlined. This
trades larger generated code for lower call overhead:

| Platform/runtime | Code bytes before -> after | Stack subtraction before -> after |
| --- | --- | --- |
| Windows10.0.13 |1416->2214|104->136|
| Linux10.0.12 |1416->2050|88->136|

Windows's unchanged24 public range/count/subject/minimum-work preflights agree.
Linux's adapted48 Core range/minimum-work preflights and all68 public Count lanes
agree. These are code-generation facts, not a .NET11, default-PGO or engine-wide
performance conclusion. JIT logging runs only in separate untimed processes.

## One bounded public comparison — accepted

Quiet Xeon8573C VM, CPU2/.NET10.0.12, FullOpts/R2Roff, Core-DLL-only swap. The
[fixture](Program.cs) extends the existing FamilyDependencies public Count fixture
with live-token, finite-deadline and adapter neighbors plus true unchanged .NET
predecoded/decode controls. Other DLLs and driver stay identical.120ms warmup,
80ms target, five alternating lane-order samples;60s phase/180s batch caps.
One95.526s batch includes codegen and the before/after pair. All680 samples
exceed65.921ms; maximum within-lane spread1.094. No confirmation or retuning.

Median public Count us/op, ordinary Core (compiled rows remain in numeric data):

| Workload | Unlimited before -> after | Finite before -> after | Finite/live before -> after | Finite/deadline before -> after |
| --- | --- | --- | --- | --- |
| Optional hit,13KB/2000matches |142.573->141.404|414.968->282.627|403.592->272.220|663.715->561.506|
| Optional miss,13KB/0matches |173.718->168.961|517.860->363.488|511.597->355.406|799.208->675.953|
| Fixed-width neighbor,14KB/2000matches |8.134->8.297|21.796->16.838|21.183->16.774|40.875->40.127|

Finite optional costs fall29-33%; deadline lanes15-16%. Measured PythonRe optional
neighbors fall3.8-7.7%, PCRE2 neighbors2.0-5.8%, and exact/folded/Unicode Core
family neighbors generally5-8%. Core shared-method users, including the fixture's
historically named fixed-control lanes, are affected neighbors. They are not true
untouched controls. The four actual unchanged .NET controls move-0.5..-2.3%; all
representative affected rows remain within the declared10% allowance.

Core/Python allocation medians agree; PCRE2 pooled fractional B/op readouts differ
slightly with calibrated trip counts. There is no demonstrated PCRE2 allocation
improvement or exact pooled parity claim. Source allocation mechanisms do not
change. Finite50Mwork/depth1024/64MiBworking/128MiBresult and10s timeout are
experiment allowances, not host defaults. No CPython Status, whole-engine,
default-tier, immutable-package gain or .NET11-fix claim follows.

## Focused acceptance and identities

Release Core562 simple/cursor/work/budget plus44 deadline/clock/cancellation,
Python774 finite/resource/persistent/ownership and PCRE175 resource/cancellation/
timeout/workspace/global/output-authority checks pass with no failures/skips.
All test outputs load measured CoreBAE88E2A. No new annotation-spelling test,
solution rebuild, build-server shutdown or package/platform matrix.

Baseline exact117 Core513C32967DAFA6A9BDE7324FA39F73866F686E552D7246706C31C91D5EA2CEF3;
candidateBAE88E2A657900B3DCE4BE61C88E4BED386CF58513F684E926575E600FBB841A.
Driver28C634C482AE8569B535A55FAE62517C57EBA567CD01D8CDE6143466842F7AF6.
[All raw samples, preflights, identities, codegen and summary](measurement.json).
Evidence archive SHA256
7B666829F06017E6CFB4ACDC4DBCA972CED39FFCB78A6628E62E5FE5AB834C5A.
Private source/patch/disassembly/test/transport receipts:
artifacts/deadline-inline-20261020. One temporary-driver token-overload compile
error was corrected before preflight/timing; its initial log remains. No timed
restart, immutable artifact modification, publication or consumer pin change.
Source CI is separate; broader R06/R07/R08/R09/R10 remains open.
