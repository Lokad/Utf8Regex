# Complete bounded word proofs at excluded separators

Source `aada866c` repairs the dense finite word Count cost demonstrated by the
[initial word-run proof](../WordBoundaryRepeat-20261104/README.md). When its
bounded ASCII lookahead reaches a nonword scalar, the existing metered class
query now checks whether the repeated atom excludes it. An excluded separator
stops the run, so only the preceding word scalars can be consumed. The same
repeat executor can omit all shorter, useless boundary alternatives.

Nonword-accepting dot/negated classes, Unicode uncertainty, zero minima and RTL
keep their original paths. No API, execution mode, storage mechanism or unchecked
access was added. Proof work, admission, original interruption and publication
remain under the existing invocation.

One **83.5596214s**, sixteen-case public A/B compares the accepted061ac4e5 graph
against the Core-only refinement. PythonRe/PCRE2/benchmark DLL/PDB are identical.
Normal eight-CPU startup then CPU2; managed1.5s/min128 warmup, BCL250ms/min64,
independent60ms calibration, five alternating samples. All owned samples>=53.7734ms.
Complete public results are checked after timing. Unchanged native literal Count
ran first and moved−0.07%. No confirmation or source variant.

The [full observations](cost-summary.json) retain sample spreads and allocations.
Dense finite word Count improves **38.06%**,208.10->128.91us and3664->176B.
Captured Count improves **20.47%**,109.13->86.79us and5744->2256B. Date miss stays
effectively unchanged(+0.01%,80B); date hit−3.27%, finite miss−0.65%, deadline
miss−1.10%, Core finite miss−2.72%. Native PCRE2/default−1.43% has a1.09 spread;
UCP−4.19% remains a secondary observation, not a native catalog Status claim.
Unicode digits+0.57%, negative boundary+2.44% and RTL+0.87% remain visible.
Zero/negative-boundary sample spreads reach1.18/1.15; no guessed runtime cause.
Do not stitch this comparison to the prior trial as a cumulative percentage.

Focused Release checks pass400/59/44 per runtime across Core/PythonRe/PCRE2.
One added space-accepting test expectation was corrected from two matches to
three; its actual matched values and the BCL overlap agree. Broader checks then
pass per .NET10/11: Core41312, differential1503, PythonRe4720, PCRE26507. All test
copies of Core were hash-verified before reuse. Tests cover real low-depth
admission, exhaustive work cutoffs/parent-loan refunds, captures, required ends,
space-accepting fallback, Unicode predicates and original interruption.

SPEC/CHANGELOG describe excluded separators and revised modeled requirements.
The prior date catalog row remains its real061ac4e5 observation: the new A/B date
miss changes only+0.01%, so no data is refreshed merely to update provenance.
The stopped Unicode-class campaign stays closed. Existing dev.20 packages and
44m08s Full evidence remain immutable; the accepted two-change word batch is
ready for one new delivery boundary.

Immutable archives in `artifacts/experiments/word-boundary-barrier-20261104/`:

- Public A/B: `7B06B6238FBD6D4C9B8F6591B31FF7293C0CF6F0E048F03852711AF8E148852F`.
- Source/correctness: `A87A72BC75E28C56D71E5E00C9D5C3BA0AB302A01809C01747B49CC9C11E9284`.
