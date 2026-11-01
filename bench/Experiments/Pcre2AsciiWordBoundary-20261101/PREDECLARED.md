# Reuse checked ASCII word-boundary adjacency

Before exact committede1cd08c54 engine/data423a8145. Candidate short-circuits
Pcre2CharacterSemantics.IsWordBoundary only when the existing checked Core
TryGetAsciiBoundary returns a result. Both actual neighbors must be ASCII/absent.
Non-ASCII retains PCRE2's current scalar membership/invalid-boundary behavior,
including UCP and joiners. Do not use Core's full Unicode policy. No new predicate,
engine, selector, candidate plan, accounting or unchecked API. Core edit is an
integration-marker comment only; freeze identical Core DLL/PDBs in the A/B.

Public ASCII pair matrix, scalar-origin/Unicode/UCP/joiner/retained capture and
Count/empty progression checks, production invalid-boundary checks, malformed
tail/original cancellation/work refusal; existing partial/probe checks. Full
affected PCRE2 Release suite on .NET10/11. No forwarding executor or solution/
package qualification. Preserve work/depth/working/result and deadline checkpoints.

One <=120s quiet-VM public A/B with17cases: date hit/miss, finite/live/deadline
hit/miss, no-digit miss, dense failed starts, dense Count, Unicode, UCP, nonword
Count, captured Count, Unicode-heavy fallback, unchanged subroutine/bounded/literal
controls. Existing typed delegate driver and complete result consumption;
warm1.5s/min1000, independent60ms calibration, five alternating samples each>=20ms,
16million-call/64MiB result-batch cap, allocations retained. At most one selected
noise confirmation if justified, no variants or status resampling. Promote only
useful public benefit within10% adverse cost. Fixed-original cumulative allowance
remains a coherent delivery gate; do not stack losses or imply universal parity.
Immutable staged/published bytes and closed prefilter deferrals remain unchanged.
