# Isolate PCRE2 preparation failures and retire accidental contracts

2026-10-22. Reviewed source `6ce279f2`, frozen matching before producer
`66180b1c68c4c27af0d1371e57b290d033936011`. Delivered source:
`921550ad` (PCRE2 authority), `a8578e21` (diagnostic API), `07780283` (guards/docs).
Development remains **0.4.0-rc.2-dev**. No package is newly qualified or published;
the frozen unpublished RC1 bytes remain unchanged and must not be released.

## Corrected behavior and compatibility

Two simultaneous PCRE2 replacements can share cold template preparation. Before
the fix, cancelling the preparer caused an unrelated waiter to throw the same
OperationCanceledException with the preparer's token, even when the waiter had
no token or a different uncancelled token. A later retry succeeded.

The adapter now checks the waiter's own authority after a shared invocation
failure and permits one independent preparation at that acquisition boundary.
The generic cache is unchanged: ready values still share, failed entries are
removed, and no shared retry loop or new coordination mode was introduced. Own
tokens, absolute deadlines and sticky limits remain authoritative; template
errors remain errors. A subsequent ordinary lookup can publish a preparation
under its own authority; the independent recovery itself does not publish.

The public regression fixture covers bytes/text results, UTF-8 templates and
destination output, tokenless/live-token waiters, own cancellation/deadline,
foreign timeout/work failure, successful contention and genuine template errors.
Diagnostic handshakes only schedule calls. The work-failure case spends the
owner's real invocation allowance through production checkpoints; it does not
inject a synthetic failure. The identical original public witness now returns
the correct output for both uncancelled followers while the owner retains its
own cancellation. Before/after witness JSON and source are retained here.

**Intentional API contraction:** Utf8CountDiagnostics and Utf8IsMatchDiagnostics
are internal. Their producers were already internal, with only friend tests and
benchmark consumers. Removed 45 listed public members and two type entries from
the reviewed API snapshot. Code that constructed these records breaks and must
remove that dependency; MIGRATION.md and CHANGELOG.md state this explicitly.
No new public diagnostic operations were added. Friend tooling still compiles.

Deleted the two PCRE2 migration-spelling fixtures and older PythonRe-only
TypeReference inventory. The existing shared consumed-assembly/member policy
remains. Replaced cursor source-spelling/retired-name checks with the real shared
cursor field-type invariant on consumed assemblies. Behavioral cursor/alias,
resource, dependency, layer and removability checks remain. PCRE2 architecture
now describes actual optional typed ProbeRoot ownership rather than deleted tags.

Strict full-subject UTF-8/scalars, captures/RTL/NonBacktracking, retained results,
original-token synchronous cancellation, deadlines, meaningful allocation
admission and useful work/depth/working/result/state limits remain. Actual
Unsafe.SizeOf queries and the completed checked-access/accounting work remain.

## Bounded public costs; timing closed

One same-process Release comparison loads the identical driver in separate
before/after contexts, pinned to logical CPU 20 on Windows/.NET 10.0.13 with
default tiers. Public outputs are checked before measurement. Returned arrays/
strings retain the last result and use identical post-call checksum traversal.
Cold churn cycles 64 preallocated templates against the existing 16-entry cache.
The finite row enables all four controls together: 50 million work steps, depth
1,024, 64 MiB working and 128 MiB result. These are experiment allowances.
Finite and unlimited calls can select different routes; their ratio is not an
isolated metering-overhead measurement.

The initial nine-pair run takes 36.92 s. Affected medians range -12.66% to +8.69%,
but the unchanged control changes +30.12%; this run is inconclusive. Its sole
predeclared reversed-first confirmation uses the same driver/product bytes,
2.5 s warmup per version/lane and 17 pairs, taking 42.10 s:

| Public operation | Before ns | After ns | Median change | Before/after B/op |
| --- | ---: | ---: | ---: | ---: |
| PCRE2 warm template, byte result | 5,180.73 | 4,663.81 | -9.98% | 448 / 448 |
| PCRE2 warm template, four finite controls | 3,784.52 | 3,531.17 | -6.69% | 712 / 712 |
| PCRE2 Unicode captures, text result | 3,351.96 | 3,391.23 | +1.17% | 11,376 / 11,376 |
| PCRE2 UTF-8 template, destination output | 2,378.07 | 2,347.36 | -1.29% | 480 / 480 |
| PCRE2 cold template churn | 2,838.46 | 2,822.89 | -0.55% | 2,256 / 2,256 |
| Unchanged Core Count miss, 128 KiB | 6,895.08 | 6,867.21 | -0.40% | 0 / 0 |

Affected medians fit the 10% allowance and allocations are unchanged. Spreads
remain as wide as 3.28x, and the shortest confirmation sample is 28.06 ms despite
40 ms calibration. This supports accepting the bounded correctness fix; it does
not establish precise equivalence, causal speedups, package-wide/cumulative
acceptance or universal finite-control overhead. Timing is closed: no third run,
VM campaign, kernel timing or snapshot refresh. The driver preflight corrected
its generated numeric-reference spelling before any timed samples were taken.

## Validation and remaining delivery work

Focused Release Core API/cursor/dependency/cache/authority checks pass; focused
PythonRe API/dependency/lazy/cache checks pass. Full local PCRE2 Release passes
6,019 tests in nine seconds, including the new deterministic regression fixture.
The affected project closures build incrementally and matching binaries are
reused; one final benchmark/library closure validates friend tooling. Existing
generated-page/parity and complete producer/PE/PDB/compiled-source checks pass.
Exact scope, logs/stream-capture limitations and CI state belong to validation.json
and ci.json. Source CI covers Windows/Linux all four fixture projects.
Final source CI 39796746585
is green at `07780283fba3d24ec4d7e791641dd76420c80457`.

immutable-files.json verifies four snapshots, nine frozen RC1 files and five
original TODOs. PLAN/history remain ignored. No normal build-server shutdown,
full local solution rebuild, new qualification framework or repeated immutable
package qualification was performed.

The next release still needs a fresh common-version immutable candidate, exact
Core pins, final package/provenance and isolated downstream Windows/Linux checks,
plus representative actual-package/host finite-control and cumulative <=10%
cost acceptance against the preserved correctness-equivalent fixed baseline.
Those are separate delivery gates; broader PCRE2 profile completion must not
block scoped Core/PythonRe delivery. This batch closes the reviewed defect and
small cleanup, and does not claim those remaining gates are complete.
