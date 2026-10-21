# Correct DFA transitions and remove unused input machinery

Development **0.4.0-rc.2-dev**, source
`61429e69b72474ef9289768eb0effe12ed2c7492`. PLAN.md was rewritten before the
implementation as L1–L5. Both planning files remain ignored; prior plans and
execution detail stay in PLAN.HISTORY.md. The original five consumer handoffs
and immutable package artifacts are preserved. No new package is qualified.

The release-blocking public false negative is corrected: `^\d*1$` over `11`
now succeeds in unlimited IsMatch, as it already did in finite IsMatch, Count
and detailed matching. Exact-class buckets previously left digit and singleton
transitions competing; execution took only the first. Construction now splits
the ASCII alphabet into disjoint classes, unions their applicable NFA targets
before closure, and coalesces equal destinations. The existing admitted buffers,
state/work ceilings, original interruption and owned capture/option fallback remain.
[Before/after witnesses and validation](correctness-and-validation.json) retain
the actual competing edges and 21 independent public outcomes; the corrected
published graphs have disjoint outgoing edges. Larger public witnesses agree too.

The production DFA input is now its real five-operation flat class language.
Removed byte/text/alternative/nested-program kinds and factories, their payloads,
nested continuation actions and optional-program compilation. Removed unused
anchor kind/options storage and its generic helper; the caller admits only
default non-multiline anchors. Flat DFA/NFA construction, live float execution,
closures and transition tables remain. Useful state/growth/refusal/rollback/
original-token/deadline tests exercise actual flat inputs. Tests whose sole
purpose was synthetic nested/text input are removed. Construction work/depth
and capacity requirements can change as unused frames disappear and correct
subset construction performs actual additional work; public limits remain useful
route-dependent bounds, not native-equivalent units or total-heap containment.

Removed ten verified unused overloads/forwarding helpers in replacement,
projection, IL, Python decoding/copying, PCRE2 cursor creation and repeated-segment
inspection. Real controlled implementations, emitted/reflected roots, actual
Unsafe.SizeOf queries and public APIs remain. The cache's obsolete Revision tuple
field, stale source-guard comment and completed schema-9 PCRE2 migration command
are retired. Current JSON loading/measurement/rendering remains; four benchmark/
parity snapshots are unchanged and their existing read-only verifiers pass.

[ASCII fixture consolidation](ascii-consolidation.json) replaces 423 duplicate
bodies with two plain helpers: **45,218 → 17,005 lines**. All **6,430 discovered
case identities**, including multiplicity, agree exactly. Restoring the extracted
bodies reproduces the original fixture after normalizing line endings: providers, attributes,
independent expectations and cold expression per operation are unchanged. The
full affected fixture passes. No generator, test DSL or extra qualification layer.

All four Release suites pass on Windows and Linux at the exact source above:
[CI 39584852325](https://github.com/Lokad/Utf8Regex/actions/runs/39584852325).
No failed/skipped cases or duplicate discovery warnings. Existing compiler/
analyzer warnings remain. [Validation receipt](correctness-and-validation.json)
includes focused checks, compiled-source reuse, CI log identities and repairs.
An initial test selected a nonexistent cancellation overload; it was corrected
to the existing start/options/token API. Guarded edit helpers stopped on overload
matching/encoding errors; their own uncommitted changes were restored from the
committed checkpoint and applied with explicit UTF-8. Final source checks pass.

[Bounded public cost evidence](measurement.json) preserves one initial run and
its **single allowed noise confirmation**, with identical driver bytes within
each pair. Original producer `84ebc039` and candidate `61429e69` use the same local
.NET 10.0.13 runtime/default tiers and one common logical processor. Both use
correctness-equivalent subjects; the buggy `11` result is never timed as a valid
baseline. [Initial driver](driver-initial.cs.txt) and
[confirmation driver](driver-confirmation.cs.txt) are frozen text, not shipped tools.
Each child has a 45-second outer cap. There is no further timing campaign.

The initial run is inconclusive: late transitions, short samples and a large
unchanged Core control shift. Confirmation warms through the actual timed loop
for at least one second and 100k calls per lane (Core control one million,
allocation-heavy finite lane 2k). Five alternating samples use at least 100k
trips, or 2k for finite. Every measured confirmation sample exceeds 71 ms.

| Public 1 KiB workload / control | Before → after, µs | Median change | B/op |
| --- | --- | --- | --- |
| PCRE2 float IsMatch | 3.337 → 2.852 | −14.53% | 0 unchanged |
| PCRE2 digit-suffix hit IsMatch | 3.381 → 3.165 | −6.39% | 0 unchanged |
| PCRE2 digit-suffix miss IsMatch | 3.361 → 3.182 | −5.31% | 0 unchanged |
| PCRE2 float, four finite controls | 78.185 → 78.053 | −0.17% | 343,304 unchanged |
| Core literal Count control, 896 B | 0.783 → 0.756 | −3.48% | 0 unchanged |
| Predecoded Regex float control | 1.300 → 1.154 | −11.21% | 0 unchanged |
| Decode-then-Regex float control | 1.516 → 1.441 | −4.92% | 2,072 unchanged |

No selected public median regresses; allocations are unchanged. Improving
controls and roughly 1.2× suffix spreads prevent attributed speedup or universal
equivalence claims. The 10% allowance applies to representative public execution.
Finite float still costs about 27× unlimited and allocates substantially; its
different owned route is a disclosed continuing operating cost. Limits in this
experiment are 1M work, depth 4096, 16 MiB working/result each, not defaults.
This one lane does not qualify package-wide finite overhead or benchmark Status.

Construction is separate: confirmation float/suffix medians rise **7.66%/5.43%**;
allocations fall **78,216 → 75,488 / 59,352 → 59,320 B**. Initial construction
medians moved in the opposite direction; no persistent causal cost claim follows.
Both use five retained 16-instance samples after 16 warm instances.

L1–L5 are complete. Fresh coherent immutable packages, final package/API/isolated
consumer qualification, actual-package operating costs and publication remain
deferred. Frozen RC1 stays unpublished with known PCRE2 defects and is never
repacked or relabeled; this milestone does not test the new witness against RC1.
Strict UTF-8/scalars, captures/references/lookarounds, RTL/NonBacktracking/empty
progression, original cancellation/deadlines, useful limits and cursor/result
lifetimes remain required. Earlier closed campaigns stay closed.
