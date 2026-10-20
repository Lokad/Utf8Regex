# Narrow literal-family kernel dependencies

Development115-dev, parent5370a309. The existing shared family search now takes
its prepared byte union, two boundary requirements and trailing literal directly.
Raw candidate callers need none of these facts. The fabricated default2744-byte
Utf8SearchPlan is gone; no new plan, cache, executor, owner or search loop.
The confirmation helper also receives only its actual predicate inputs.

Candidate versus confirmed/RTL start/end priorities, branch ordinals, bounded
2048-byte windows, comparisons, diagnostics, original tokens/absolute deadlines,
exact numeric work/depth/working/result charges and publication remain. Union
selection moves outside the repeated window loop; its immutable property reads
allocate and charge nothing. No public API or resource-contract revision.
Strict UTF8/scalars/captures/history/NonBacktracking and retained results remain.

## Release code

The same public preflight driver forces the production controlled finder on
runtime10.0.12 and11.0.0-rc.1.26425.128,FullOpts/R2Roff. Before, each candidate call
zeros a2744-byte temporary **twice**: two MEMZERO calls on10,two343-qword store
loops on11. The large caller prologue initializes its reference-bearing frame too.
After, the temporary and both repeated initializations disappear. The finder's
stack subtraction drops3096->424bytes on10 and3080->424 on11; family kernel code
2346->2063bytes /2153->1850bytes. These are code-generation facts, not runtime11
timing or specific-runtime-defect attribution. [Identities and methods](codegen.json).
Full private disassembly and SHA-verified archives remain under
artifacts/family-dependencies-20261020; a separate actual114 inspection receipt
under artifacts/finite-optional-codegen-20261020 precedes the source decision.

## One public pair and its sole confirmation

Identical [driver](Program.cs) AF5BAE1130DF9E71202E2A6435D3B3F67E0442B977799C09BB5692C730ABD8B6,
quiet Xeon8573C CPU2,.NET10.0.12,FullOpts/R2Roff. Six public Count workloads,
ordinary/compiled,unlimited/all-four finite allowances. Constructors/startup
excluded.120ms warmup,80ms calibration,five alternating lane-order samples,
60s child/180s batch cap. First before/after32.984s; sole identical candidate-first
confirmation32.938s. All480 samples>=74.981ms; no further timing or tuning.
Max within-lane spread1.053x. Quiet gates CPU2=0%,other observed dotnet ticks0.

Public **Count**, microseconds ordinary/compiled:

| Workload | Before -> after, confirmation | Allocated bytes |
| --- | --- | --- |
| optional hit finite,13KB/2000matches |554.736->374.722 /556.501->371.813|112 unchanged|
| optional miss finite,13KB/0matches |644.175->477.534 /647.268->477.295|112 unchanged|
| optional hit unlimited |140.345->139.697 /141.082->138.501|1792 unchanged|
| optional miss unlimited |172.253->170.394 /171.835->169.670|1792 unchanged|
| fixed-width control unlimited |7.656->7.601 /7.725->7.667|0 unchanged|
| fixed-width control finite |20.535->20.428 /20.563->20.529|112 unchanged|
| boundary exact family finite |2661.062->2760.309 /2682.619->2758.031|1984 unchanged|
| folded family finite |2816.009->2938.709 /2814.497->2917.738|1984 unchanged|
| Unicode family finite |2479.291->2553.269 /2492.203->2571.056|1984 unchanged|

**Decision: accept** the smaller dependency boundary and repeated finite gains
(~26-33%). Other measured family costs increase~3-5% (first-pair maximum5.3%),
within the scoped allowance. All allocation medians and24 short-unit minimum work
budgets match. Both runtimes/binaries also agree on public counts/first byte ranges,
subject hashes and those thresholds. Threshold equality covers the declared short
cases, not every pattern's quantitative boundary.

The first pair records unlimited optional costs16-22% higher despite stable fixed
controls. Their cause is unproved; sole confirmation does not reproduce them
(controls within1.8%). Keep both observations; do not claim universal unlimited
parity, an exact throughput ceiling, default-PGO gains or a runtime11 speedup.
These are public operations, not isolated kernels. No CPython Status, snapshot,
package rebuild/qualification or fixed-original checked-access campaign changes.

[First complete raw samples](measurement.json), [sole confirmation](confirmation.json).
Baseline actual114Core1AB068EDB637A94222191820A0C5AAE461701216239FEF67F650F1C89810B6E6;
candidateD009B91B982DB9393F5EDD9952269F9CC2E9E9CC963B7B2608F5A5C99C4C20CA.
Evidence SHA2565B26497B0D6EA80B528B88995979FF857EA68FC7A407C0131AA9EE0EE6CDBCC0;
confirmation76C2B831C9ADCBFBCCDE5C5AFB5E6984B474BB431C95878F4B9B636D75DF7BBE.
Both pairs complete0. Finite50Mwork/depth1024/64MiBworking/128MiBresult are
experimental allowances, not host defaults. Immutable114/older bytes are preserved.

## Focused acceptance

Release Core1134 family/simple/deterministic/native tests plus681 primitive /
projection/offset/cursor/source-guard checks pass. These cover branch order,
scalar boundaries, malformed tails, long/cross-window branches, both directions,
all work cutoffs, finite capacity, original cancellation/deadlines and retained
results. Python630 resource/native-cursor/persistent/scalar-global/capture and
PCRE189 resource/core-grammar/literal-global/boundary checks pass. Each test output
loads the same measured D009Core. Build only affected projects,serialize mutations,
reuse Core binaries; no solution build,server shutdown,packing or package matrix.
Source CI follows separately. Broader R06/R07/R08/R09/R10 remains open.
