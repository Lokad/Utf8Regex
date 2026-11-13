# Consume proved POSIX class runs without impossible choices

Accepted source `c9f3c19c`. The existing compiler proof admits flat, positive,
mutually disjoint ASCII classes with nonempty minima and captures outside their
repeat bodies. Shortening a run cannot help its next disjoint class or an anchor.
The existing scalar repeat handler now consumes that unique run directly.
Capture events and general matching stay in their original implementations;
no second executor, pattern-specific dispatch or public option is introduced.

**Accounting revision:** each examined byte stays metered. Impossible alternative
exits, their counter undo and unused repeat-counter capacity are omitted. Work,
pending depth and working requirements may decrease. General alternatives,
capture traces, original token/deadline, refusal cleanup and aliases remain
governed. [The contract](../../../docs/SPEC-POSIX.md#interruption-and-admission)
documents this change. No subject-validity or coordinate-preparation pass is added.

## One public comparison

Quiet VM, .NET11.0.0. Exact Full83 POSIX pair reused after source-equivalence
proof at `8dd326e3`; common Core `117cbeb5` and the caller are byte-identical
across ordinary processes. Both Posix DLL/PDB/source checks pass and changing
DLL hashes differ. Valid unchanged UTF8, same options and complete three-slot
public Match output. The fixed-capture control retains its matching algorithm
but shares workspace setup; this is not a native parity or conformance result.

| Public Match | Before µs | After µs | Observed change | Allocated B before → after |
|---|---:|---:|---:|---:|
| sku-captures | 0.617 | 0.294 | -52.3% | 0 → 0 |
| sku-finite-live | 0.805 | 0.425 | -47.2% | 760 → 328 |
| long-runs | 12.416 | 1.665 | -86.6% | 20552 → 0 |
| miss | 1.104 | 0.420 | -61.9% | 0 → 0 |
| unchanged-fixed-captures | 0.334 | 0.295 | -11.5% | 0 → 0 |

Retain the control movement (-11.5%) and different chosen processors; do not
normalize these observations into exact causal gains. All changed rows improve
with disjoint sample ranges. Long input has256 letters followed by256 digits;
finite/live SKU uses work10M/depth4096/working16MiB/result1 and a live token.
No weakening of the caller-owned output contract explains the allocation drop.

Nine samples/case,1000ms warmup,250ms target, actual samples >=224ms;
120s cap/process. At most64 value results retained, checked after timing.
One A/B, no confirmation or tuned follow-up. Complete raw samples and provenance
are retained under `artifacts/experiments/posix-disjoint-20261113/`, hash-verified
offVM before keeper release. [Acceptance](acceptance.json) retains all ranges.

## Checks and delivery

All385 Release POSIX tests pass per target on Windows and Linux. Coverage includes
bounded repeat minima/maxima, newline anchors, Unicode floors/byte coordinates,
short capture destinations, count/cursors/retained aliases and real-loop original
cancellation/deadline. A4099-byte result now fits depth1/working1KiB; a64-step
policy still refuses without partial output and preserves the caller17-byte loan.
Other library pairs remain unchanged. Caller-only path and unsuccessful-result
JSON corrections happened before timing; failed preparation logs are preserved.

All524 existing package/symbol files remain immutable. Accepted dev83 excludes
this later source change. No package, native catalog or Full refresh was repeated;
a future delivery uses a fresh common version. Frozen README tables are unchanged.
