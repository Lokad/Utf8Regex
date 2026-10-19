# Remove the rejected-start byte VM

Parent01227efc, source111-dev. Both AsciiSimplePatternPlan constructors produce
complete ordered finite branches or an anchored whole-input specialization.
Non-ASCII inputs, capture histories, finite controls and NonBacktracking retain
their scalar/controlled routes. The general byte VM was called only after those
complete branches rejected an unanchored candidate. Its other callers were tests.

Remove that redundant retry,192-line continuation implementation, prefix wrappers
and exclusive atom helpers:370 source lines removed,6 added. Keep live instruction
metadata, literal-prefix/search helpers, native/SIMD kernels and shared storage.
Retarget useful semantic/deep/admission/loan/pool tests to production paths;
remove obsolete byte-VM allocation/dispatch expectations. No public API/accounting
contract changes, admission reduction or new execution mechanism.

Release: Corefocused6765/full39715,differential1504,Pythonfocused165,PCREfocused80
pass without skips. Tested Core DLLs equal candidate SHA below. Public Count,
IsMatch and value-Match offset/tail checks are extended; scalar captures,
NonBacktracking, malformed UTF8, cancellation/deadlines and resource tests remain.

## Bounded public-operation evidence

Quiet Xeon8573C VM CPU2, .NET10.0.12. Same driver; only Core bytes swapped.
One initial default-tier pair14.91s plus its sole candidate-first FullOpts/R2R-off
confirmation17.43s, five samples/lane/assembly,60s child/140s batch caps. Initial
samples16-48ms fail the50ms gate and the unchanged ordinary control moves-39%:
those hit+28-29%/miss-25% medians are inconclusive, retained verbatim. Confirmation
has120/120 samples>=67.70ms, max spread1.043x; no further timing or tuning.

Confirmation median public **Count**, microseconds (ordinary/compiled):

| Subject | Unlimited before -> after | Finite before -> after | Allocated bytes U/F |
| --- | --- | --- | --- |
| optional hit,13KB/2000matches |1786.92->1734.94 /1792.94->1737.75|1439.75->1346.44 /1437.86->1343.49|1792/3664,unchanged|
| optional miss,13KB/0matches |2545.42->845.58 /2524.34->842.42|2288.07->2147.61 /2296.47->2134.82|1792/3664,unchanged|
| unchanged fixed control,14KB/2000matches |7.708->7.560 /7.717->7.593|21.485->20.179 /21.432->20.029|0/112,unchanged|

Finite uses50M work/depth1024/64MiB working/128MiB result, experimental allowances,
not safe defaults. Finite implementation is unchanged and its~6% movement follows
the finite control; no causal finite improvement claim. The miss observation
supports removing redundant verification; no default-PGO equivalence, universal
speedup, whole-engine parity or immutable110 performance claim. Optional hits and
finite Count remain costly; this is not a new candidate-search algorithm.

[Raw samples and both summaries](measurement.json) retain every observation.
Source receipt:artifacts/ascii-rejection-simplification-20261019/RESULT.md.
Core baseline4CFE81E6969968DD3403CD1CE176A200D4134D5109163E3EDF812CC7B02A3BA5;
candidate7F240AF14BF6E780F5356B6223BF779932960F21AEB0C5D406DBDC9B47C00B90.
Harness45F6D7730B1A184B977BBC98DA1180E778A9DB7128303A38EBA2903790B38407.

The frozen fixture builds against an explicit CoreAssemblyPath. For reproduction,
use separate output directories, then swap only matching Core DLLs beside the
same driver; run Release on the quiet VM CPU2. Initial tiering is default; the
confirmation explicitly sets DOTNET_TieredCompilation=0,DOTNET_ReadyToRun=0.
Constructors/startup are excluded. This fixture is not a permanent policy switch,
package qualifier or benchmark framework. Staged/published packages are untouched.
