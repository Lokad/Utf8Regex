# Borrow the immutable PCRE2 instruction token

Baseline committedba9c810a, engineecfd28a1. The existing Tier1 VM still stages
the token property before calling TryMatchToken. Replace that auto-property with
a readonly field at the same original declaration position; pass the token to
the existing helper by in-reference. No new field storage, ownership annotation,
mode, engine, selector, algorithm, resource charge/checkpoint or unchecked access.
Token references do not escape the synchronous helper; retained values/results
remain unchanged. Other callers can pass their existing locals/array elements.
Rebuild internal friend consumers together; no public API change. Preserve the
accepted request/instruction borrow and ASCII-boundary changes, and keep the
rejected diagnostic extraction completely absent.

Inspect actual SizeOf for instruction, token and request and actual Release
Tier1 code both lanes. Existing focused diagnostic/shared-budget/resource/
interruption tests and full affected PCRE2 Release .NET10/11 suite. Core bytes
are identical between the compared lanes. No forwarding tests or solution build.

One <=120s quiet-VM public A/B with the existing17case date driver: hit/miss,
finite/live/deadline hit/miss, no-digit miss, dense failed starts, dense Count,
Unicode, UCP, nonword/captured Count, Unicode-heavy and subroutine/bounded/literal
controls. Warm1.5s/min1000, independent60ms calibration, five alternating samples
each>=20ms, complete result-batch consumption and16million-call/64MiB batch cap.
Preserve all observations/allocations. At most one justified selected noise
confirmation, no variants. Accept only useful public gain with <=10% adverse
public cost; otherwise restore completely. Fixed-original cumulative gate stays
at a coherent delivery boundary. No package/full-catalog requalification.
