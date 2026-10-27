# Seventh review: construction admission and unused preparation

Development **0.4.0-rc.2-dev**, final source `884b156b`. PLAN.md was rewritten
before implementation around J1–J6. All six deliverables are complete; the prior
plan and execution detail remain in ignored PLAN.HISTORY.md. Both planning files
stay ignored and the five original consumer TODO handoffs are preserved.

- PCRE2 lexical guards use existing admitted temporary buffers instead of raw
  stacks. Extended-mode and assertion scope share one byte per group; temporary
  loans return to the original construction owner. Major capacity and lexical
  depth admit before growth, with the existing 256-frame recursive backstop and
  smaller caller limits. Whole-text work precharge is supplemented by bounded
  interruption, including scans whose helpers skip offsets. Irrelevant policy
  passes are skipped after a bounded candidate scan. Contextual callout, `\C`,
  `\K`, name, class, quote, escape and x/xx behavior remains tested.
- Benchmark qualification checks actual compiled source through existing
  portable PDB SHA256 document checksums and PE/PDB association, in addition to
  producer revision/build inputs. It rejects changed compiled code carrying a
  plausible producer, replaced loaded images, missing/mismatched symbols and
  unbuilt source changes. Matching binaries pass. SDK-generated `obj` documents
  remain covered by project/build inputs; this is not universal build attestation.
  No additional persisted identity/protocol axis or snapshot refresh was added.
- Public null text patterns now throw `ArgumentNullException("pattern")` across
  Core/PythonRe/PCRE2 constructors and factories. Policy/options/timeout validation
  and explicit Compile pre-cancellation retain their established precedence;
  empty strings and default UTF-8 spans remain empty patterns.
- PCRE2 omits Core preparation where existing profile/option proofs exclude its
  consumers. Completed operation slots retain only their actual Core owner:
  finite captured literals retain the derived literal Core, while shared slices
  retain their immutable slice payload and native fallback. Unused original Core
  graphs and three facade diagnostic forwards are gone. Ownership checks now
  verify real consumers instead of preserving discarded preparation.
- The sizing-only 22-field `CachedConstructionState` record and its field-parity
  test are deleted. Cached facade admission uses one conservative **512-byte**
  charge, formerly 208 modeled bytes, and direct constructor arguments. This can
  deny/evict earlier; actual allocation is unchanged. Major children retain their
  separate admission, parent rollback and real `Unsafe.SizeOf` layout queries.
- Specs, host integration, architecture and changelog describe current parsing,
  ownership, admission and null contracts; obsolete character-preparation claims,
  slice comments and duplicated PythonRe prose are removed.

[Focused Release checks](focused-checks.json) cover direct/public text/byte
construction, lexical bounds and parent loans, original cancellation/deadlines,
captures/options, live owner identity, null/error precedence, cache and resource
behavior. Filters overlap. Initial stale ownership assertions and the first CI
failure are disclosed. The latter exposed a finite-owner empty expectation and
an overzealous syntactic ban on public null guards. Functional checks and the
other source guideline rules remain. Generated benchmark pages/parity verify.

[Compiled-source evidence](compiled-sources.json) includes an actual altered Core
constant (16 KiB to 17 KiB) with current producer metadata: the previous guard
accepted the reviewed witness; the new guard refuses. Original DLLs/source and
published evidence were not replaced by the isolated fixtures.

[One bounded comparison](measurement.json) used frozen original DLLs, identical
Release drivers, five samples, 45-second child caps and unchanged literal/BCL
controls. Construction and public matching costs are distinct:

| Observation | Before → after | Scope |
| --- | --- | --- |
| PCRE2 Caseless public Count | 51.948 → 51.485 µs | −0.89%; 0 B unchanged |
| Captured finite-literal public Count | 8.197 → 8.230 µs | +0.40%; 0 B unchanged |
| Literal public Count control | 5.462 → 5.448 µs | −0.24%; 0 B unchanged |
| Caseless construction | 246.825 → 28.356 µs; 34,776 → 6,408 B | Unused preparation removed |
| Non-LF construction | 305.444 → 22.300 µs; 45,064 → 6,792 B | Unused preparation removed |
| Anchored reference construction | 330.863 → 25.194 µs; 47,552 → 6,744 B | Unused preparation removed |
| Captured finite-literal construction | 567.831 → 490.881 µs; 101,168 → 101,928 B | Actual derived Core retained |
| Cached facade, single confirmation | 86.475 → 89.569 µs | +3.58%; 41,416 B unchanged |

Initial cached construction was unstable (+220%); the sole confirmation used
2,000 warm calls and an unchanged public-constructor control. That control moved
−11.38%; several BCL controls also varied substantially. Selected public medians
meet the 10% allowance, but short construction tails/control variation limit
precision. Timing is finished. No universal performance equivalence, benchmark
Status, or reopened checked-access/first-use/backreference campaign is claimed.
Measured after producer `3dd88cb9` has identical shipped C# to final `884b156b`;
the latter changes tests and one benchmark-only nullable check.

[Complete Windows/Linux source CI](source-ci.json) is terminal green at exact
`884b156b6a2ad5b0da0781937aff4c2f2b04375a`:
run 39567603690.
All four suites pass on both platforms, no failed/skipped tests or duplicate
discovery warnings. Existing compiler/analyzer warnings remain. Closeout is
documentation/receipts only; no repeated complete matrix or package qualification.

[Nine original RC1 hashes](rc1-unchanged.json) and all four benchmark snapshot/
parity JSON files are unchanged. Frozen common RC1 remains unpublished with known
PCRE2 defects. A corrected immutable candidate, final package/downstream checks,
unresolved delivery costs and publication remain deferred. This source milestone
does not establish package readiness or broader profile completion.
