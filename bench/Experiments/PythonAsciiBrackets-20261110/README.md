# Lower resolved Python ASCII brackets directly

Accepted source `199f7444e7f51e73fe628451a9dc4d1d2f671fa4`, baseline Python
`acb1e6f32e860dbea535a8387f1ce0c177254806`; common Core3dc2e65a. Python parsing
already resolves bracket literals/ranges and lexical case partners. Eligible
ASCII operands now produce the same admitted16-byte mask and existing Set node,
including singleton classes. This removes their rendered byte fragment, Core
parse and tree-lowering cycle. Unicode/pinned predicates and other leaves retain
their current lowering. No new matching algorithm, instruction, state, cache,
public API, policy switch or unchecked read. Bounds/work admission still precede
covered allocations, and per-operand/range work retains interruption checkpoints.

One **79.297s** Linux .NET11 RC3 ordinary-process ABBA, under180s,13 public lanes,
five samples/process, **260 samples >=43.792ms**. Identical caller/Core DLL/PDBs,
distinct Python,8 processors visible before CPU2 affinity, default runtime settings.
Construction and construction+first Count are separate; output inspection and
Python disposal occur after timing. Warm controls include complete captured
findall/split output. Read-only program inspection verifies identical instruction
and mask hashes for all four affected pattern families. No confirmation.

| Public operation | Before µs | After µs | Change | Bytes/op before → after |
|---|---:|---:|---:|---:|
| construct two captures |206.586|227.840|+10.29%, overlapping|77280 → 69648|
| construct + first Count, same |168.307|287.911|+71.06%, overlapping|77552 → 69920|
| construct scoped ASCII folding |102.586|91.201|-11.10%, overlapping|88648 → 80800|
| construct negated class |73.367|67.592|-7.87%, overlapping|61032 → 57176|
| construct eight classes |233.233|163.008|-30.11%, overlapping|156704 → 125928|
| construct + first Count, eight classes |226.268|199.092|-12.01%|156768 → 125992|
| construct byte pattern |95.422|88.749|-6.99%|77328 → 69696|
| Unicode/predicate construction control |65.401|65.414|+0.02%, overlapping|57920 → 57920|
| warm captured findall strings |805.385|778.564|-3.33%, overlapping|242008 → 242008|
| warm captured split |484.628|486.322|+0.35%, overlapping|274648 → 274648|
| unchanged Core construction |62.008|62.953|+1.52%, overlapping|49344 → 49344|

Only byte construction and eight-class first Count have disjoint overall timing
ranges. Accept the reduced duplicate work/allocations and these bounded gains;
do not claim general faster Python construction. Two-capture construction is
particularly variable: before process medians382.147/105.377µs versus after
201.650/308.238; construction+first Count528.023/110.806 versus225.978/504.646.
Their full ranges94–550 and159–463µs (construction),109–564 and103–557µs (first
Count) overlap substantially. Keep the adverse pooled medians, not a causal
regression verdict or a preferred run. No retiming/tuned variants follow; general
lifecycle uncertainty and discarded optional Core preparation remain active work.

Release Python tests **5045 per.NET10/11** pass. Focused class, lexical folding,
negation, escaped/duplicate endpoints, captures, UTF8/scalar coordinates, work/
working/result admission and cancellation are covered. The first prototype
collapsed a singleton class into Literal; the existing mask-consumer test exposed
that representation change. Final code keeps Set, with no test weakening.

Raw caller/graphs/PDBs, patch, protocol, samples and shape checks:
`artifacts/experiments/python-ascii-brackets-20261110/evidence.tar.gz`, SHA256
`0C0B0DDB560572603E05CDB0DA8DF138BB45BBCCCCFF85AFD62161D59B00AB9D`.
All observations, process ranges, allocations and exact identities are in the
summary beside the archive. Evidence is copied offVM/hash verified. Source CI
is separate; no package qualification, full catalog/README refresh or native-parity
claim. Preserve immutable dev66 packages. Timing closed; continue the roadmap.
