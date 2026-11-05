# Python string phase checkpoints: comparison incomplete

**Reverted without a performance conclusion.** The prototype removed GetText's
two wrapper checkpoints. Counting, result admission and decoding retained their
own bounded interruption checks, including the decoder's final empty-string
check and caller publication checks. Proposed work accounting omitted two
helper-boundary charges per selected string. That change is not retained.

Focused Release Windows/Linux per .NET10/11: Core1697/Python1190/PCRE309 PASS.
Coverage includes FindAll interruption at every observed checkpoint, original
tokens/deadlines, Unicode sizing tails, finite result/working/depth/work admission,
funded storage, captures, retained results and cursor aliases. Untimed full-result
preflight passes all nine rows; FullOpts removes exactly two Checkpoint calls,
retaining CountUtf16/DecodeUtf8 (GetText439->397 code bytes).

The single declared120-second Default-process A0/B0/B1/A1 comparison reached its
cap: exit124,120.001s including timeout overhead. A0/B0/B1 completed; A1 stopped
after its second row and has no final JSON. Incomplete process order cannot
support the planned before/after conclusion. No aggregate, allocation equivalence
claim or catalog Status is derived from partial measurements. No confirmation,
shortened rerun, source tuning or package/Full qualification follows.

The frozen baseline is the exact accepted EmptyWorkspaceReset AFTER graph,
reused without rebuilding. Only Python DLL/PDB changes; six Core/PCRE/benchmark
companions and the typed caller are identical. Normal eight-CPU startup then
CPU2; producer warm1s/min16, BCL250ms/min4, independent60ms calibration and five
alternating samples. Full result shapes, scalar/tuple strings, UTF-8 bytes and
substitution text/count are consumed after timing. BCL predecoded/decode-then
lanes are equal-result diagnostics, not CPython or resource-policy equivalents.

Fixed rows: unchanged Core literal Count first; actual one-/many-capture FindAll;
one-capture finite/live and deadline/live; actual Unicode Split; long astral Split;
unchanged UTF-8 FindAll; Unicode no-hit SubnToString. The three catalog inputs
are reproduced from their exact source patterns and repeat units. Before
preflight, the UTF-8 control digest was completed to include its result shape;
all executed producers use that identical frozen worker.

Immutable artifacts/experiments/python-string-phase-checkpoints-20261105,
verified off VM:

- evidence.tar.gz: `ACCEF7D3BEC939689DA524666184874C25CE8D727FC9F42AFBD1D18C12F5C7AB`.
- windows-correctness.zip: `5140369AB801C96950E85033AD7A2ADE5584EB86D2B00D03A27D49310E2DC24B`.

Declaration `9DB6D8971B9AF2FD221A105BFE27BB8FE8CCA28595A40186E8935A181AF431AA`
and runtime patch
`4EEF903A09F88AECBE14481956F869B68552204DAECD82CF9DA99472F2670606`
remain archived. Source is restored to accepted `c686fe3c`. Prototype test binaries
are not reusable as matching source binaries without an affected rebuild. Accepted
graphs, dev35 packages, snapshots and historical evidence remain immutable.
