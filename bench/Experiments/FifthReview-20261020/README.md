# Fifth review: lexical consistency and temporary Python state

Development source **0.4.0-rc.2-dev**, implemented in `82c9b029` and `877c7743`.
PLAN.md was rewritten around five bounded deliverables; earlier execution detail
remains in ignored PLAN.HISTORY.md. Broader work is deferred.

- PCRE2 code/name/structure/policy guards share complete escape-operand skipping.
  Control operands cannot become class terminators, quotes or apparent group
  syntax. The parser remains the semantic authority; real callouts, invalid names,
  backslash-C/K policies, malformed tails and construction polling remain checked.
- PythonRe removes its retained translation field and the redundant capture-count
  copy. Runtime counts come from the owned scalar program. When no optional Core
  route can consume native text, construction omits rendering and numbering
  scratch while retaining reference and scalar-lookbehind validation. Name maps
  remain temporary inputs to the compiler. Useful native paths are unchanged.
- Python factory state contains constructor arguments rather than unused kind or
  initializer mirrors. [All 27 actual factory admissions](factory-admission.json)
  remain 128 bytes/two work steps before allocation; payload and depth controls
  remain separate. Actual `Unsafe.SizeOf` queries stay.
- PythonRe benchmark output reports loaded product/runner versions and hashes
  separately from checkout revision. Existing freshness rejects missing or changed
  Core/PythonRe identities; historical snapshots are unchanged. Runner source and
  operation/protocol fingerprints still govern runner freshness. No new protocol
  framework or snapshot qualification campaign was added.
- The [existing worktree gate](worktree-gate.json) excludes exactly five preserved
  root handoffs and still detects unrelated untracked source. PCRE2 qualification
  docs distinguish frozen package evidence, known publication blockers and later
  source fixes. SPEC-PYTHONRE and the changelog describe the construction changes.

[Focused Release checks](focused-checks.json) cover public text/byte constructors,
escapes, names, scalar/capture/replacement/FullMatch behavior, finite admission,
interruption and source/member inventories. Filters overlap. Initial local tests
caught and corrected a mistaken capture-count subtraction: the VM's count already
excludes whole-match slot zero. Final passing evidence is identified separately.

[One bounded comparison](measurement.json) uses frozen before DLLs and existing
Release CLI lanes, sequentially, with 45-second child caps and unchanged BCL
controls. No confirmation or broader benchmark refresh was run.

| Selected observation | Before -> after | Interpretation |
| --- | --- | --- |
| PCRE2 warm public IsMatch | 0.752 -> 0.790 us; 3,872 B unchanged | +5.05% median |
| PythonRe warm public Search | 4.059 -> 3.894 us; 0 B unchanged | -4.07% median |
| Python scalar-only construction | 35.763 -> 31.856 us; 14,098 -> 13,058 B | -10.92%; 1,040 B removed |
| Python construct + first FullMatch | 40.462 -> 33.131 us; 20,530 -> 19,490 B | -18.12%; distinct from warm matching |
| PCRE2 construction / first IsMatch | 195.312 -> 217.906 / 3.038 -> 3.394 us | +11.57% / +11.72%; wide small-batch tails |

Selected warm public medians fit the 10% allowance. PCRE2 construction/first-use
numbers do not establish warm-execution regression or construction equivalence.
Control variation and sample ranges limit precision; no universal throughput or
new benchmark Status claim is made. The earlier first-use release concern remains
open. Stop timing; this source cleanup does not qualify a fresh release.

[Original RC1 identities](rc1-unchanged.json) are unchanged. No package, symbol,
manifest, zip or historical snapshot was rebuilt or rewritten. The five original
untracked TODOs remain untouched; both planning files remain ignored. A future
corrected immutable candidate and downstream qualification remain separate work.

[Complete Windows/Linux source CI39459649444](source-ci.json) is terminal green
at exact `877c77433afeaf8b2ee2b9a118fd967ee46af7ac`. All four suites pass on both
platforms with no failed/skipped results. Existing analyzers and five duplicate
PCRE2 discovery-ID warnings remain. Later changes contain docs/receipts only;
source CI does not substitute for final package or isolated-consumer evidence.
