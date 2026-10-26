# Thirty-fifth review — 2026-10-26

Checkout `2ef9ae66f25f7948b6456975d66b95450b9f1b3a`, current product source
`7650e7a5`, development `0.4.0-rc.2-dev`. This review examines failure containment,
obsolete evidence producers and documentation portability rather than reopening
closed matching/accounting/checked-access campaigns. Product source, binaries,
PLAN/history and protected artifacts are unchanged.

One reproduced tooling defect and two bounded cleanup items merit follow-up.
No new release-blocking matcher defect or broad engine purge is established.

## 1. Benchmark wall-time cap stops applying after parent exit (P2, tooling)

`bench.ps1:119` requires `!$benchmarkProcess.HasExited` before marking a timeout.
However, the surrounding loop also waits for stdout/stderr EOF. A child can keep
an inherited pipe open after the direct parent exits. The loop then keeps draining
without applying the configured cap, and ultimately returns the parent's success
code. The final cleanup also depends on the parent still being alive.

An exact copy of the current wrapper, with only its benchmark payload replaced by
an ignored synthetic .NET host, reproduces the boundary:

| Case | Requested cap | Observed wall time | Exit |
| --- | ---: | ---: | ---: |
| Parent exits successfully; child holds output for four seconds | 1 s | 4.408 s | 0 |
| Parent remains alive while the same child waits | 1 s | 1.852 s | 124 |

The first receipt verifies byte-identical wrapper copies. There is no real
matching/performance workload and no claim that an ordinary current benchmark has
hung. This is a bounded fault-injection demonstration of the cap's contract.
An indefinitely surviving child would leave the same wait unbounded. Existing
PCRE2 ledger child launching also inherits stderr, so inherited-pipe children are
not excluded by the repository's process model.

Recommendation: include outstanding output drains in the deadline and bound
cleanup after direct-parent exit. Preserve live-parent descendant termination
and exit 124. Make the supported descendant-cleanup boundary explicit; do not
silently claim complete containment after the parent handle is gone. Prefer a
small wrapper correction over a general process supervisor or additional modes.

Focused acceptance: ordinary success/failure codes and streamed output remain;
both live-parent and exited-parent pipe-holding witnesses terminate under a
bounded grace period. No engine benchmark or package matrix is needed.

Evidence: `wrapper-wall-cap.json`, `wrapper-live-parent-control.json`, exact
`bench.ps1` copy and `wrapper-host/Program.cs`. The four-second child exits
naturally in the failure witness; the positive control exercises existing kill.

## 2. Retired PCRE2 ledgers still have live duplicate producers (P3)

The ledger page explicitly declares itself historical and directs consumers to
`PCRE2.Benchmarks.json`, generated Markdown and row provenance. Yet
`Program.Pcre2.cs:129` still exposes both `--emit-pcre2-perf-ledger` and
`--emit-pcre2-managed-perf-ledger`.

`BenchmarkInspectReporter.Pcre2.cs:1074` maintains two hard-coded case lists, a
16,384-iteration floor, independent three-sample tables and separate formatting.
The first route adds an old `dotnet run --project ... --no-build` child launcher,
row record and console-text parser. It selects the checkout's benchmark project
rather than the running assembly; there is no local per-child timeout. It is
separate from current snapshot production and ordinary per-case diagnostics.

Reference tracing finds the child launcher/row/parser and three generic-looking
helpers (`TryParseMicrosecondsLine`, `ExtractValueAfterColon`, `FormatLedgerValue`)
only in these ledger routes. The shared helpers are in
`BenchmarkInspectReporter.cs:6262`; the CLI branches are their entry points.
They are reachable legacy tooling, not unreachable production runtimes.

Recommendation: retire both ledger commands and their exclusively owned runners,
record/parser/child launch and formatting helpers. Keep the historical receipt,
snapshot/generated page, priority reporting and useful `--measure-pcre2-case`
operations. This removes a second report producer and obsolete process path
without adding another reporter abstraction. It does not require changing any
shipped library or timing unchanged public workloads.

Focused acceptance: benchmark Release build, reference search confirming complete
removal, and smoke checks for retained relevant inspection/verification commands.
Do not run either expensive legacy ledger merely to justify removing it.

## 3. Three documentation links work only in this maintainer checkout (P3)

A tracked-target audit of 27 non-experiment Markdown files finds three links into
ignored artifacts: `UNCHECKED-ACCESS.md:9`/`:10` and
`bench/Lokad.Utf8Regex.Benchmarks/Pcre2PerfLedger.md:20`. Their targets exist here
but are absent from Git and a fresh source checkout. A filesystem-only link scan
reports no missing targets and therefore misses this portability defect.

The ledger's adjacent paragraph also still presents RC1 as the staged release
without repeating the known-defect/publication block. Its linked qualification
page is accurate; the root README and other release guidance already state the
block. This is a small stale pointer, not evidence that RC1 was published.

Recommendation: point public links to the existing tracked release receipt and
identity JSON. Mention local-only handoff paths as such, and align the short
ledger pointer with the current RC1 warning/current-performance page. Preserve
all immutable original files; do not commit local handoffs or rebuild candidates.
Validate the few changed links against Git-tracked targets. No permanent new link
qualification framework is warranted.

Evidence: `documentation-links.json` records all three local-but-untracked links.

## Checks that did not justify further cleanup

- Current Core/PythonRe operation-consistency controls: 103 patterns, 21 subjects,
  four Core modes (ordinary/Compiled/all-finite/RTL) and two PythonRe policies.
  All 12,978 combinations agree internally between boolean, first-match success,
  Count and enumeration/detailed collection. There are no unsupported cases.
  This is bounded consistency evidence, not independent semantic-oracle coverage,
  equality of RTL/LTR ranges or package qualification.
- Construction culture is present in cache identity. Retryable Python FullMatch
  preparation deliberately uses PublicationOnly and retains the original deadline.
  Span/durable cursor wrappers bind different subject lifetimes to shared state.
  These mechanisms have real consumers; removal is not supported by this review.
- String replacement fragment repair is explicit and does not relax strict UTF-8
  subjects. The scoped writer correction has no new copying layer to remove.
- The inspected documented benchmark command tokens still have implementation
  handlers. No broad command rename or documentation rewrite is justified.

Only ignored scratch projects were built against existing Release products.
No product rebuild, full suite, real performance campaign, package/snapshot
refresh, CI API request, VM access, external repository edit or PLAN change.
The earlier source CI and package/cumulative-cost gates retain their own scope.
Integrity manifests verify unchanged plans, product DLL/PDBs, wrapper and all 18
protected historical files. The five original untracked TODO handoffs remain.
