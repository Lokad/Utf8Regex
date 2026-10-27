# Bounded benchmark output and retired ledger producers

Development remains `0.4.0-rc.2-dev`. Final tooling source `2c745703`; shipped
library and test sources are unchanged from source `7650e7a5`. This
milestone fixes the thirty-fifth review at checkout `2ef9ae66`. No package,
published snapshot or immutable historical artifact is refreshed. The frozen
unpublished RC1 still has known PCRE2 defects and must not be released.

## Delivered changes

- `9182fb5d`: benchmark wall-time admission now applies to pending output drains
  after direct-parent exit. Timeout returns 124, keeps live-process-tree killing,
  drains buffered diagnostics for at most one additional second, then closes the
  readers. The timeout path no longer calls an unbounded WaitForExit. Normal
  output/exit handling remains. AGENTS documents the boundary: when the parent
  has already exited, terminating an orphan is not guaranteed. No process
  supervisor, cancellation object, switch or new matching control is retained.
- `04dbfa47`/`2c745703`: retire both PCRE2 ledger CLI producers. Remove their
  hard-coded catalogs/floors, old dotnet-run child launch, row/text parser, three
  format/parse helpers and the remaining ledger-only timing wrapper. These are
  reachable obsolete tooling, not shipped runtimes. Final reference tracing finds
  no surviving definitions/callers; real range sinks and the shared median core
  retain snapshot/per-case consumers. The removal is 228 C# lines, with no
  compatibility aliases or replacement reporter framework.
- Public evidence links now target tracked RC1 receipts/identities. The archival
  ledger states the publication block and points to current performance evidence;
  detailed local-only handoffs are identified as absent from a source checkout.
  Original handoffs and archived evidence remain unchanged.

## Focused validation

Seven synthetic-host controls use an exact wrapper copy. They exercise ordinary
success/exit 7, Unicode stdout/stderr with unterminated final lines, 128 full lines
under simultaneous pipe pressure, live-parent timeout, exited-parent success and
failure with inherited pipes, and explicitly disabled timeout. All pass. The
one-second live-parent timeout exits 124 in 1.482 s and leaves no live child. The
two exited-parent cases exit 124 in 2.473/2.397 s, including launch and the fixed
one-second drain grace. The control explicitly cleans those known test orphans;
this is not attributed to the wrapper. Disabled timeout drains the four-second
child and returns success. No real engine workload was timed.

A matched before/after fault comparison uses the same synthetic DLL and mode:
old wrapper direct exit 4.486 s/0; fixed wrapper 2.439 s/124. These measure fault
containment, not matching throughput or a <=10% execution-cost qualification.

An initial control included the outer harness's pipe EOF wait in the timer.
Orphans can inherit those outer handles too: it incorrectly classified a >4 s
outer drain as a wrapper timeout failure. Direct process observation shows the
first, untokened correction already exited in 2.534 s while outer EOF arrived at
4.376 s. The harness now records process exit before explicitly cleaning its own
identified child and awaiting EOF. The investigated reader-cancellation object
was removed. `harness-pipe-scope.json` preserves this distinction.

The benchmark project builds in Release with zero warnings/errors. Unchanged
library references are reused with BuildProjectReferences=false; all ten local
library DLL/PDB copies retain their exact pre-milestone hashes. The final build
follows removal of the last unreferenced timing wrapper. No solution build or
library rebuild was repeated locally; no build servers were stopped.

Six initial CLI checks at `04dbfa47` pass using the built host: PCRE2 inspection,
PCRE2/PythonRe generated-page verification, Core parity verification and rejection
of both retired ledger commands. Retained commands return 0; retired commands
return the existing unknown-option exit 2. Initial rejection controls invoked
PowerShell via -Command, which mapped the script's nonzero status to 1; only those
two controls were corrected to use -File and rerun. No ledger timing executed.
Final source differs only by the unreferenced private timing-wrapper deletion;
its host rebuild and focused inspection are recorded separately.

All 27 non-experiment Markdown files have tracked file/directory targets for their
local links; the three ignored-artifact links are gone. Historical experiment
receipts retain their original scope and local paths. This one-off check adds no
permanent link/qualification framework.

## Evidence and remaining scope

`review.md` preserves the original assessment. Wrapper controls, their source,
matched fault comparison, CLI results/log excerpts, build output, module identity,
tracked-link check and protected-file hashes accompany this receipt. Final
Windows/Linux source CI
40341296362 passes,
including both disabled-dynamic-code steps. Earlier source `04dbfa47` also passes
CI40340526037. Three spaced status requests were made while local work continued.
This does not claim Linux execution of the local Windows wrapper fault controls.

Strict UTF-8/scalars, captures, NB/RTL, cursor/result lifetimes, cancellation,
absolute deadlines and useful work/depth/working/result/state bounds are unchanged.
No public-operation cost or package qualification is claimed. Fresh immutable
common-version packages, downstream consumers and cumulative costs against fixed
`844dcbad` remain deferred. Closed accounting/checked-access/layout/retention
campaigns and optional overlap enforcement remain closed/deferred. All 18
protected snapshot/RC1/handoff files match; PLAN/history remain ignored.
