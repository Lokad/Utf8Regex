# Reuse prepared small-family search in literal replacement

**Accepted**, source `78aa44c7`. Unlimited ASCII literal replacement can use the
already-prepared small literal-family cursor used by Split, Count and IsMatch.
The existing shared getter now has a name covering its production consumers;
its eligibility and prepared storage are unchanged. No new matcher, state,
execution mode, API, allocation algorithm or accounting precision.

The original range ledger still computes the admitted exact output size and
emits bytes. Actual deadline, diagnostics and NonBacktracking guards retain their
previous cursor. Live tokens, finite budgets, captures/complex/Unicode/RTL or
other declined shapes retain existing execution. Strict whole-subject UTF-8,
scalar endpoints, branch ordering, captures, lifetimes, original cancellation/
deadlines and useful limits/refunds remain. Search/output metering is preserved;
no finite accounting semantics were revised. The initially considered structural
global-kernel hook does not apply to this literal family and was not implemented.

The actual catalog pattern is tempus|magna|semper, literal replacement amoveatur,
with the repeated ASCII paragraph. Both comparator forms materialize UTF-8 output,
including encoding the BCL string result. Source inspection confirmed this
contract before changing execution; no string-only versus byte-output claim.

One **62.7807219s**, 12-case public A/B used the frozen `b5899253` graph. Only
candidate Core was rebuilt; PythonRe, PCRE2 and benchmark DLL/PDB bytes are
identical. Normal eight-CPU startup then CPU2; managed1.5s/min64, BCL250ms/min64
warmup, independent60ms calibration and five alternating samples. Complete UTF-8
output batches are checked after timing. All240 samples qualify, minimum owned sample22.32ms.
All median allocations are unchanged. Unchanged Count ran FIRST and moved+0.17%.
No confirmation, implementation variant or additional timing campaign followed.

| Public replacement operation | Before us | After us | Change | Allocated B, both |
| --- | ---: | ---: | ---: | ---: |
| Actual common/replace-words |62.90|27.43|-56.39%|13648|
| Same, compiled |62.71|27.08|-56.82%|13648|
| Native byte template |62.26|27.19|-56.34%|13648|
| Native destination control |65.23|63.46|-2.71%|4120|
| Finite limits/live token |158.15|160.93|+1.76%|57056|
| Deadline/live token |261.07|266.03|+1.90%|57104|
| Mixed-Unicode control |66.65|67.99|+2.01%|13664|
| Overlapping alternatives |128.54|130.20|+1.29%|22920|
| Dense adjacent control |120.48|123.89|+2.83%|21576|
| Large-copy control |20.24|20.43|+0.94%|65576|
| No-hit control |13.77|20.62|+49.72%|65560|

Actual ordinary/compiled samples are tight, spreads at most1.02, saving about35us.
No-hit medians cost about6.85us more, but their spreads1.67/1.23 and large-copy
spreads1.94/1.57 remain noisy. These are retained observations, not a stable
49.72% causal estimate or equivalent controls. The representative target gains
support promotion with that unresolved local cost. The user's local-cost/major-
gain policy applies; there is no universal10% release ceiling.
[All observations](cost-summary.json) retain spreads, qualification and identities.

Release focused **1524 checks per .NET10/11** pass across replacement, small-family
admission and construction. New public witnesses compare outputs at32 alignments
and8 tails with an independent BCL oracle, including overlapping branch order,
Unicode replacement output, RTL/NonBacktracking and declined mixed/captured/
boundary/folded forms. A diagnostic miss still enters bounded search checkpoints
and propagates the original exception, then permits reuse. Existing interruption,
range admission, alias/lifetime and output tests remain. No new public contract.

Only existing common/replace-words ordinary/compiled rows were refreshed on clean
accepted source. [Selected evidence](selected-ratios.json): compiled ratio
**3.622 ->1.60x** (still Gap), ordinary **0.370x** (Win), about27.8us/13648B.
Catalog movement and the matched public A/B are separate observations, not a
cumulative percentage. Other176 case measurements and identities are unchanged.
Three generated files were hash-imported; four blocks/six descending tables/
178ratios and matching-binary snapshot guards3/runtime pass without rebuilding.

Dev.21's packages and43m21.5s Full refresh remain immutable and predate this work.
This follows the accepted Python boundary batch; the next coherent delivery
boundary qualifies their combined package graph. No per-pilot package/Full matrix,
new qualification framework, or reopened Chinese/Unicode-class campaign.

Immutable evidence under artifacts/experiments/literal-family-replacement-20261104/:

- evidence.tar.gz: `D6E4F150248170B0C423CC67915C3683F9A6FAF64AFE7368FE61DA417A812952`.
- correctness-source.zip: `2B274576E28D9BCD54561FEE6BD679C679B730384D7435440DF918DABD6FD03C`.
- selective.tar.gz: `8052332BA81086451A2503B245938A5B0D138D4968BAC4A36091FBD330177A81`.
- import-guards.zip: ABE422C8C8F5172ED036EB02E97365F2359BCEF1F3D565AB51392AAB970ACADE.