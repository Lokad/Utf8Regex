# Bounded literal-window proof experiment

Frozen before source2ebc01a7 (delivery65c3b761 changes only data/receipts).
Candidate adds only WordBoundary/NonWordBoundary to the existing ASCII/zero-width
prefix proof; no runtime runner or candidate kind changes. Preserve actual
Core DLL/PDB bytes between lanes. Published/staged packages stay immutable.

Current owned diagnostic: date-miss47bytes,42candidates/111VMsteps, no candidate
search. Cold diagnostics are not public throughput evidence. Existing rejection
of the LeadingAsciiSet boundary pilot remains closed.

Focused new public scalar-origin/capture/eligibility/strict-validation checks
and full affected PCRE2 Release suite on .NET10/11. Compile only the affected
adapter for VM timing; reuse existing Core. Preflight all equal results first.

One <=120second comparison on the quiet VM, fifteen existing public workloads:
date hit/miss, finite/live hit/miss, finite/live/deadline hit/miss, no-digit miss,
dense failed starts, dense Count, Unicode, captured Count, bounded nonword Count,
unchanged UCP/candidate/literal controls. Existing predecoded/decode BCL lanes
are diagnostic and excluded where semantics differ. Warm1s/min1000calls,
independent60ms calibration, five alternating samples, each>=20ms. Preallocated
result batches are fully consumed after timing, allocations measured separately.
Retain all medians/spreads/adverse observations. Accept public-value improvement
with <=10% affected public regression; at most one justified selected noise
confirmation, then stop. No variants or full/package refresh for a small change.
The fixed-original cumulative allowance remains a delivery-boundary gate;
the completed dev.4 comparison is representative coverage, not universal parity.
