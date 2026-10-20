# Fourth review: PCRE2 construction and focused simplification

Development source **0.4.0-rc.2-dev**, starting at `eb96f003`. Implementation is
`83bc1b89` and `0cdae03e`. This source milestone does not rebuild or qualify a
package. The original three **0.4.0-rc.1** packages remain immutable/unpublished;
their producer has the reviewed parser/lowerer/validator gaps. Publishing PCRE2
requires a fresh corrected candidate at a later delivery boundary.

- Backtracking parsing, lowering and reference-following analysis use the
  existing construction owner. Recursive frames have a fixed 256-frame cap,
  tightened by smaller public limits. Instruction and auxiliary program vectors
  use existing admitted buffers, including growth/final-copy overlap. Transient
  AST/maps/metadata have a conservative 256-byte charge per pattern character;
  each assertion lowerer's separate name lookup also admits 256 bytes per entry.
  Charges can count shared storage twice and cause earlier refusal. No helper
  registry, new accounting framework or whole-heap guarantee is introduced.
- Removed five fixture-specific validation conditions and their now-orphaned
  lexical scan variant. Contextual bounds/flags/escapes replace them; quoted and
  escaped payloads remain literal. Final all-declined compilation now throws
  `NotSupportedException`; intermediate empty foundations still support overlays.
  Obsolete tests of published `None` programs now assert construction refusal;
  the ineffective spelling guard is replaced by behavioral variants.
- Moved the unchanged 81-line lead-byte sampler/type from Core into benchmark
  tooling. Retention tests keep actual readonly/catalog/graph invariants without
  pinning exact field counts. No matching algorithm or public API was removed.
- Benchmark output distinguishes checkout revision from loaded assembly producer
  versions/hashes. Existing resumable measurements compare those identities too;
  old snapshots remain historical and are not rewritten. Constructor diagnostics
  expose requested/effective samples and correctly describe one shared JIT prime.
- PLAN's stale/misencoded heading was replaced; the complete old plan is archived
  in ignored PLAN.HISTORY.md. Broader work remains deferred.

[Focused Release checks](focused-checks.json) cover the reported bypasses,
conditional recursion, numeric subroutine dependencies, original cancellation,
scalar/reference operations, invalid syntax/context controls, source/API/member
inventory and Core/PythonRe invocation/construction routes. The wider local
PCRE2 suite passed before the final additional witnesses; final source CI covers
the complete boundary. Original analyzer/duplicate-discovery warnings remain.
[Diagnostic smoke](diagnostic-smoke.json) exercises the relocated sampler.

[Bounded measurement](measurement.json) retains exact DLL identities and logs:

| Observation | Before -> after | Interpretation |
| --- | --- | --- |
| Backtracking construction | 192.887 -> 200.312 us; 57968 -> 57960 B median | +3.85%; small batches with wide tails |
| Backtracking first IsMatch | 3.212 -> 3.431 us; 3872 B unchanged | First-use only; does not close the earlier latency concern |
| Initial warm public IsMatch | 1.491 -> 2.366 us | Short/noisy samples; prompted the sole confirmation |
| Confirmation warm public IsMatch | 2.420 -> 2.574 us; 3872 B unchanged | +6.36%, within the 10% allowance for this workload |
| Confirmation unchanged decode/predecoded controls | 2.556 -> 2.540 / 0.079 -> 0.077 us | -0.63% / -2.53% |

The confirmation runs after then frozen before, 20,000 operations/five samples
on one logical processor with 45-second child caps. Stop measuring. Constructor
literal controls also changed because obsolete lexical scans disappeared; they
are not an unchanged matching control. No universal performance equivalence,
snapshot/Status refresh or reopening of the checked-access campaign is claimed.
The previous milestone's first-use release concern remains explicitly open.

[Nine original release identities](rc1-unchanged.json) remain unchanged. PLAN and
HISTORY stay ignored; the five original untracked handoffs remain untouched.
[Complete Windows/Linux source CI39452239922](source-ci.json) is terminal green
at exact `0cdae03e8ca8bb25683c61a56c2e08d704725ed3`. All four suites pass on each
platform with no failed/skipped results. Later commits contain receipts only.
Source validation does not qualify a new package or isolated downstream consumer.
