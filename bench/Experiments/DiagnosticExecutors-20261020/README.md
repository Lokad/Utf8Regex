# Delete diagnostic execution layers

Parentcac8d440. This is source/caller qualification, not a performance experiment.
The public production matcher, allocation/admission and metering bodies are unchanged.

## Deleted and retained

Core simple-pattern diagnostics had nine names with only benchmark consumers:
four metadata mirrors, fixed-length/fixed-alternation and repeated-digit whole/find
executors, and the per-branch alternation summary. Delete both outer and Inspection
forwarding layers. Tooling reads existing prepared metadata and calls the existing
policy/kernels directly. Drop the obsolete formatter and its one console row;
retain candidate and fixed-branch verification helpers with production consumers.
No replacement dispatcher, runtime accessor, mode or stored state.

Six more Debug aliases forwarded Match/IsMatch to direct, simple-pattern or
head-tail production implementations. Delete both alias layers; existing tests and
tooling now call the actual implementation. Intentional internal tradeoff: those
six existing methods become internal for existing friend assemblies. The public
API, signatures/preconditions, guards and bodies stay unchanged. This adds no
executor and exposes no new public bypass of full-input validation or limits.

PCRE2 raw Count, raw enumeration index sum, native materialization and isolated
first replacement expansion had only benchmark consumers. Remove them, two exclusive
loop helpers and the capture-copy helper left unused by deleting expansion replay.
Their old diagnostic rows, ledger fields/parsing and derived ratios are removed.
Existing public Count/enumeration/MatchMany/Replace measurements and adjacent
enumerator phases remain; old private-attribution timings are historical.
Keep the actual driver, candidate-search, template parser/writer, prepared Count
bridge and original resource/cancellation/lifetime behavior with real consumers.

The standalone template-writer forwarding constructor is now test-only. Delete it;
nine existing test calls construct the identical invocation explicitly and use
the production constructor. Their heap/work ceilings, token, absolute deadline,
extent checks and cleanup assertions are unchanged. The production constructor
receiving discovery/shaping authority keeps its exact body.

## Focused validation

- Release Core simple-pattern/compiled-route/finite checks:6719 pass. After moving
  direct callers, original route/construction/exact-literal/compiled fixtures:372 pass.
- Release PCRE2 public enumeration/cancellation/resource/architecture/translation/
  scalar replacement checks:224 pass. After constructor/orphan deletion, template
  authority, extent, byte expansion, admission and interruption checks:159 pass.
- Release PythonRe scalar-global/finite-work/findall-ownership/cancellation:280 pass.
- Incremental benchmark builds succeed. Bounded existing postcode, credit-card,
  branch-reset and doc-line smoke commands exercise direct fixed-alternation,
  repeated-digit, public PCRE2 operations and direct Match caller paths. Expected
  route/result checks pass. Tiny diagnostic timings are not performance evidence,
  snapshot refresh, CPython Status or a source/package speedup claim.

No failures/skips. No new existence tests or benchmark framework. Source-text proof
compares all six production helper bodies after undoing only private->internal;
the whole matcher is identical after excluding only the removed formatter. PCRE2
facade/writer likewise differ only by the orphan/forwarding constructor and comment.
Logs/caller maps/proofs live in artifacts/simple-pattern-diagnostic-cleanup-20261020
and artifacts/pcre2-attribution-prune-20261020. No production live-path change to
time and no repeated package/platform qualification per deletion.

Immutable Core/Python115 and exactCore/PCRE94 plus all older staged/published bytes
stay fixed. Broader diagnostic cleanup remains: Core composite replay/dispatch
helpers and PCRE2 diagnostic Count/IsMatch wrappers with genuine test consumers.
Migrate tests to production implementations; retain useful counters and public
diagnostics. Full original R01-R10 remains open.
