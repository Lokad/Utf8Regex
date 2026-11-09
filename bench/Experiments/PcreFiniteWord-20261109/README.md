# Bounded ASCII word-boundary proof

Accepted against frozen dev60 PCRE2/Core (`848dbb29`). A route probe confirms the
date body's12 alternatives already qualify for the existing Boolean compiler;
only its outer `\b` assertions prevent admission. Strip a matched boundary pair
after existing anchor extraction, require nonempty word-only ASCII endpoints in
every alternative, and check their outside neighbors in the same runner. One
private flag; no new executor/mode or changes to the shared anchored-DFA parser.
Default non-UCP semantics treat non-ASCII neighbors as nonword. Interior assertions,
empty/mixed endpoints, UCP/options/settings and unproved languages keep their
existing routes.64 alternatives/128 tokens, cold admission, strict whole-input
validation, controlled/flag/result VM and original authority remain.

The first ABBA aborted at an unchanged conditional control: its1M iteration cap
produced a sample below20ms. The incomplete observations are archived, not used
as acceptance evidence. The **single allowed confirmation**, with the same8M cap
for both producers, completes in **68.139s** under180s. All340 samples exceed72ms.
One producer per ordinary process, same Core/worker DLLs, quiet .NET11 RC3 VM;
public results and zero outstanding loans are checked after timing.

| Public operation | Before (µs) | After (µs) | Change |
|---|---:|---:|---:|
| Date IsMatch hit, unlimited |0.357|0.026|−92.8%|
| Date IsMatch miss, unlimited |1.639|0.247|−84.9%|
| Date hit, finite/live |2.623|1.995|−23.9%|
| Date miss, finite/live |9.222|6.671|−27.7%|
| Date hit, funded/live |1.706|1.430|−16.2%|
| Date miss, funded/live |6.343|4.928|−22.3%|
| Unchanged word-boundary hit |0.330|0.391|+18.3%|
| Unchanged word-boundary miss |0.176|0.225|+27.8%|
| Date Count |1.132|1.297|+14.5%|
| Date construction |167.849|181.062|+7.9%|
| Unchanged Core Count |0.857|0.869|+1.4%|

Execution allocations and funded peaks are unchanged; these funded cases need
zero covered workspace. Construction allocation rises70,680→78,464B (+7,784B) to
prepare the finite language. Count/word-boundary and controlled instructions are
unchanged; their movements are retained without sole-cause attribution. The
unchanged conditional control moves0.057→0.018µs, illustrating producer layout/
tiering sensitivity; do not normalize or compose these observations. The large
eligible Boolean gains justify the cold cost and observed local regressions under
the approved trade-off. [Summary](summary.json) preserves all17 lanes/processes.
This is a producer comparison, not fresh native parity or a catalog replacement.

All6,627 PCRE2 Release checks pass per target on Windows/Linux, .NET10/11.17 new
cases cover independently expected word/nonword neighbors, supplementary and
non-ASCII context, complete subject boundaries, final-newline anchors, scalar
floors, negative eligibility, captures and original-token/finite refusal. One
old possessification test assumed a Boolean VM route; it now inspects Count's
actual production VM, preserving the lowering assertion. Its first full run
failed that route assertion; the corrected run passes. VM staging also initially
declined because the Full-generated README was dirty; the guard was narrowed to
the three affected source files before any build/timing. The confirmation worker
first failed to build because archived source remained in its compile glob;
renaming that archive to `.cs.txt` fixed preparation before confirmation timing.

Raw observations, incomplete attempt, protocol, source patch, matching producers
and Linux checks are verified off VM at
`artifacts/experiments/pcre-finite-word-20261109/evidence.tar.gz`, SHA-256
`7C941C697E7DE018401DC6ACCA86F2852CE6E0EBE0F6D93F64258CE9FFDFBA26`.
Windows checks/preparation failure logs are alongside it. No package or catalog
qualification is repeated; dev60 packages/snapshots remain immutable. Source CI
42695036869 is green on Windows/Linux for77207676.
