# General POSIX history dominance: bounded model

A conservative comparison survives all common accepted continuations in the
bounded corpus. This supports a structural investigation, not a production
speedup or a new engine. Six current public32-scalar root-alternative cases
still refuse100001/100000 work steps (ASCII/BMP/astral,both input policies).

## Candidate rule and argument

For one program/options, valid unchanged subject and logical start, require equal
pc/position, repeat counts, empty-progress positions and active tag-opening
positions. ERE captures do not control acceptance: both histories have the same
future control choices and tag actions. Respect **all** semantic tags, including
implicit parts, in existing hierarchical priority and parent-occurrence order.

Before the deciding tag, require equal completed histories and equal partial
opening extents. Compare only a first unequal pair of fully completed occurrences.
Decline strict prefixes, unresolved occurrences or unpaired parent scopes.
Earlier completed priorities are fixed; corresponding open earlier priorities
close/append identically under a common future. By induction their histories stay
equal. The first deciding completed start/end cannot be changed by appending or
closing later events. Later priorities cannot repair that difference. Identical
scoped histories also remain equivalent. This is deliberately a partial order,
not a total rank assigned to unfinished histories or a claim of a minimal key.

## Evidence and counterexamples

The independent parser/syntax-tree occurrence oracle and a lowered-state model
cover48programs/23subjects:finite/unbounded/nullable/nested repetitions, root
alternatives, anchors/newline, BMP and astral input. Final model5.031s under30s,
with hard per-counter2-million admissions.151869thread states,406190pairs and
392126common accepted continuations:251486decisive nonterminal comparisons,
94884incomparable pairs,13104identity checks,94identical-history coalescences.
3018independent winners and2160public whole/capture observations agree. The model
uses scalar ordinals internally; a fixed-subject UTF8 boundary bijection supplies
actual public byte offsets. No library or benchmark is timed.

- Counts matter:`(a|aa){0,3}`/`aaaa` reaches the same pc/position/openings/progress
  with counts2versus3; accepted future ends are{3,4}versus{3}.
- Progress matters:`((a?)*)*`/`aa` has equal counts/pc/position/openings but different
  empty-progress state; future ends are{1,2}versus{1}.
- Final slots fail:`(a|aa)*`/`aaa` selects the final capture at(2,1); ranking only
  final slots prefers(1,2), despite equal whole endpoints.
- A strict-prefix history order can reverse after appending the same events.
  That example is an **abstract scoped trace**, not a claim that it reaches the
  same compiled control key. It justifies declining without a stronger proof.

## Integration limit and next action

Existing live DFS frames save ancestor prefixes of the current trace. They do
not retain independent competing histories with the decisive differences above.
Scanning those frames is insufficient. A replacement needs an admitted state/
history representation; prove its memory cost, preserve useful limits/original
interruption/retained results and remove superseded machinery. No unbounded map,
compatibility switch or second dormant executor follows from this model.
General history remains an architectural debt, not solved production behavior.
The next delivery boundary can ship the already accepted Core/POSIX batch;
subsequent representation work needs its own bounded feasibility decision.

Frozen qualified Core/POSIX DLL/PDBs reused from the parent-reachability experiment;
loaded hashes verify before/after. Only a private caller was built. No production
edits, VM benchmark, package or table refresh. Private bracket/normalization and
negative-control preparation corrections are retained; no claim that preliminary
models were final. Canonical local evidence, including source, complete programs,
traces, public refusals and proof notes:
`artifacts/experiments/posix-dominance-model-20261112/model-evidence.tar.gz`
SHA256: 589F3B9F660F8FC03FFE39A179AF0F2E4DE27361D783DD44DBAF1E0A0F9483A9 .
