# Eighth review: bounded expansion and simpler construction

Development **0.4.0-rc.2-dev**, final source `2e8d071f`. PLAN.md was rewritten
before implementation around K1–K6. Earlier plans/execution detail remain in
ignored PLAN.HISTORY.md; both planning files and the five original TODO handoffs
are preserved. This source cleanup does not qualify or publish a new package.

- PCRE2 finite-language evaluation now admits coarse state/map capacity, literal
  text and actual alternative/encoding arrays through the original construction
  owner. Traversal and candidate reduction poll original interruption and depth.
  Capture dictionaries are omitted where no backreference consumes them; capture
  results still use the owned matcher. Bounded-ASCII expansion uses existing
  admitted buffers/arrays, including growth overlap. Fixed shape caps supplement
  public admission. Temporary expansion charges can count shared storage again
  and remain until eager completion; earlier denial is intentional.
- PCRE2 selects operations from the actual owned parse and optional eligible
  Core, then publishes one complete program. The injected foundation factory,
  dummy legacy syntax/no-operation programs and final None enumeration branch
  are removed. Slice operation slots share one immutable payload/fallback wrapper.
  Real native/Core/candidate consumers, captures and option fallbacks remain.
- Prepared/scalar/IL builder field mirrors are removed. Direct construction uses
  conservative **2,048 / 512 / 512-byte** carrier admission, replacing modeled
  1,024 / 144 / 144 bytes. Independent padded-field bounds and actual payload/
  parent-loan checks remain. Real array/value `Unsafe.SizeOf` queries stay. Smaller
  carriers and actual algorithmic state vectors remain outside this bounded
  three-carrier change; they are later opportunities, not release blockers.
- Cache publication drops its extra revision/retry protocol and redundant
  full-miss validation. Closed interruption/storage/comparer probes cannot mutate
  the cache under its lock. Outside-lock preparation still has generation
  protection, failure retry, cancellable waits and real clear/shrink/reentrancy
  checks. Held aliases remain valid; eviction remains conservative. Synthetic
  mutation from diagnostic publication callbacks is no longer an internal contract.
- The completed reverse README-to-JSON migration, dedicated parser and CLI/
  instructions are deleted. Its parser would reconstruct all 178 rows without
  provenance/allocation evidence before overwriting the authoritative snapshot.
  Current measurement, JSON loading/rendering and parity verification remain.
- Syntax-only optional/null-suppression/friend-location/document-trivia tests and
  mirrored-field existence requirements are removed, along with unassigned
  `NativeAsciiCharClassRun`. Nullable compilation, CS1591 documentation errors,
  API/member/dependency inventories and functional control tests remain.

[Allocation witnesses](optimizer-witnesses.json) isolate each optimizer **after
root parsing** under a one-byte allowance. Finite evaluation's reviewed
1,024-capture allocation before refusal falls **18,315,720 → 808 B**; bounded
128-repeat expansion falls **206,808 → 1,896 B**. Its first checkpoint now occurs
after 224 B rather than 205,736 B. The tiny ceiling is not a successful public
Compile claim. Public 8 MiB modeled calls still accept all reviewed finite
witnesses with correct matches/routes; cumulative allocation at 1,024 captures
falls 25,044,936 → 8,337,360 B. That total is not a live-capacity or RSS guarantee.

[Focused Release checks](focused-checks.json) cover pre-growth admission,
work/depth, parent rollback, original cancellation/deadlines, public text/byte
factories and finite controls, scalar/capture/cursor/output behavior, emission,
cache races/aliases and independent carrier bounds. Filters overlap. Initial
overpadding and constructor-shape assumptions are disclosed with their repairs.
Generated benchmark pages/parity verify. [Contracts/removals](simplification-contracts.json)
identify the intentional accounting and internal diagnostic-contract changes.

[One bounded cost comparison](measurement.json) uses identical driver bytes,
frozen original DLLs, five warm samples and sequential 45-second child caps.
These are selected local **unlimited** public workloads; finite-control
correctness is covered separately, not a new finite-overhead qualification.

| Public observation | Before → after | Change / allocation |
| --- | --- | --- |
| Bounded ASCII PCRE2 IsMatch | 0.037 → 0.037 µs | +1.25%; 0 B unchanged |
| Literal Core Count control | 3.808 → 3.764 µs | −1.17%; 0 B unchanged |
| Static Core cache-hit IsMatch | 0.193 → 0.192 µs | −0.73%; 0 B unchanged |
| Core cached replacement | 17.091 → 17.370 µs | +1.63%; 2,424 B unchanged |
| Finite captured PCRE2 Count | 13.923 → 2.635 µs | Unattributed variation; 0 B unchanged |

Finite Count's predecoded control changed −44.74% and transitioned within its
before samples; the decode control was stable. Its large apparent gain is not an
attributed engine speedup. Selected public medians remain within the 10% allowance;
all allocations are unchanged. Construction is separate: many captures −10.44%,
bounded ASCII −19.47%, prepared Core −6.55%, emitted Core +4.05%, cached facade
−1.23%, with small allocation reductions. No confirmation was needed/used;
timing is finished. No universal equivalence or benchmark Status claim.

[Final Windows/Linux source CI](source-ci.json) covers all four suites at exact
`2e8d071f6bd35c5554d3c2cc52a13e7f827e980d`:
[run 39577333702](https://github.com/Lokad/Utf8Regex/actions/runs/39577333702).
It is terminal green: all four suites pass on both platforms, with no failed,
skipped or duplicate discovery results. Existing analyzer warnings remain.
The first boundary exposed two stale source inventory locations/expectations:
the final source has no BCL `Regex.Escape`, and eligible Core construction moved
to the compiler/finite analyzer. The repaired inventory keeps the strict engine
boundary and pins the actual constructors. Measured after `84ebc039` has identical
shipped C# to final source; the repair changes tests/documentation only.

[Four snapshot/parity JSON files](snapshots-unchanged.json) and
[nine original RC1 hashes](rc1-unchanged.json) are unchanged. No package, symbols,
zip, manifest or handoff was rebuilt/rewritten. Frozen common RC1 remains
unpublished with known PCRE2 defects. A corrected immutable candidate, final
package/downstream qualification, prior first-use/backreference and actual-package
operating costs, broader profile/default work and publication remain deferred.
