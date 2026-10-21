# Unread structural state and remaining helper cleanup

2026-10-21, development `0.4.0-rc.2-dev`. Original checkout `a3b1972c`,
compiled producer `b11afb82`; measured production source `097c8343`, with test-only oracle repair `06c30382`. This is source
qualification, not a new package candidate. Immutable artifacts remain unchanged.

## Delivered

- Removed `Utf8StructuralLinearInstructionProgram.Search` and
  `Utf8StructuralLinearProgram.RunPlan`, including constructor/factory arguments.
  Both were unread copies. The original `SimplePatternPlan.RunPlan` and its
  prepared byte search remain live in production executors. Structural/scalar
  instructions, deterministic programs, native/SIMD algorithms and finite
  invocation authority remain.
- Removed the always-null `Utf8WindowSearchFacts.MaxGap`. Its sole factory is
  WithinLines, whose preparation now supplies the same null directly. General
  prepared-window gap support remains. Removed unused related HasValue getters.
- Removed unused overloads/predicates/accessors from ASCII classes/search,
  ordered windows, scalar byte-set/copy helpers, frontend analysis, cache/size
  forwarding and the multi-literal wrapper. Live same-named overloads, strategy
  lengths, byte copies, dynamic-emission roots and actual size queries remain.
- Removed only the unused PCRE2 runner overload with **two positions and no
  caller-owned workspace**, plus the coordinate-only internal value factory and
  its now-unreferenced private constructor. The one-start-position overload is
  live and remains: the review's approximate `:155` source location referred to
  that live neighbor, while its IL signature correctly identified the unused
  two-position overload. Public APIs and workspace/result lifetimes are unchanged.
- Trimmed 22 file/directory-layout assertions and historical retired-name/source
  call checks from the architecture fixture. Dependency/member restrictions,
  isolation, directional ownership and behavioral/resource checks remain.
  Repeated instruction access checks reference stability, not complete
  immutability. One construction fixture drops the removed constructor argument.
- CI exposed a reflective test consumer of the removed execution-tree/node
  ObjectBytes getters, missed by the static census. Their original size formulas
  now live in the existing test graph oracle. Its physical-coverage and exact
  publication-boundary assertions remain; production acquisition admission is
  unchanged. No test-only size getter needs to remain in the library.
- Corrected the search complexity owner and conditional PCRE2 syntax-tree
  ownership. Historical receipts and closed campaigns remain intact. No new
  facade, analyzer, execution mode, compatibility switch or framework.

## Actual layout and accounting effects

| Actual x64 value | Original bytes | Candidate bytes |
| --- | ---: | ---: |
| Structural linear program | 4,088 | 4,016 |
| Its instruction program | 184 | 168 |
| Window facts | 80 | 72 |
| Complete operation cursor | 1,440 | 1,440 |

The instruction-program reduction is included in the complete program's 72-byte
reduction; these are not additive whole-object savings. Removing borrowed search
copies does not remove the original search allocation. The selected ordinary
bounded-class construction allocates 94,952 → 94,880 B/call. Selected matching
allocations are unchanged. These facts do not establish RSS or throughput gains.

Actual Unsafe.SizeOf-based array/carrier admissions now reflect smaller storage,
so modeled construction/cache requirements may decrease. Core/PythonRe/PCRE2
scalar semantics, strict full-subject validation, captures, RTL, NonBacktracking,
original cancellation/deadlines, useful work/depth/working/result/state bounds,
and retained result/alias lifetimes keep their existing contracts.

## Bounded cost comparison

Fixed original versus candidate, identical driver DLL bytes, local Windows/.NET
10, default tiers and one logical CPU. Warm public Count/IsMatch workloads include
ordinary/compiled native bounded-class routes, a fixed-token route and all four
finite controls. Two unchanged .NET Count controls distinguish system movement.
Ordinary construction is a separate lane, not matching-performance evidence.

The initial harness attempt hit its 60-second cap before producing samples.
Subject/call floors were reduced and cold results/routes preflighted; the exact
slow lane in the aborted attempt was not diagnosed. One complete comparison and
its sole reversed-process-order
confirmation ran with unchanged final driver bytes and protocol. Calibration
targets 35 ms/45 ms; actual raw sample durations/spreads remain visible rather
than being represented as guaranteed floors. No repeated tuning or package
qualification. Raw runs, summary, driver and launcher are alongside this receipt.

| Operation | Initial change | Confirmation | Original -> candidate B/op |
| --- | ---: | ---: | ---: |
| core/ascii-run/Count | -15.31% | +2.36% | 1,680 -> 1,680 |
| core/ascii-run-compiled/Count | -18.60% | +18.28% | 1,680 -> 1,680 |
| core/ascii-run-finite-four/Count | -20.65% | +17.26% | 1,792 -> 1,792 |
| core/ascii-run/IsMatch-late | -24.71% | +16.70% | 1,680 -> 1,680 |
| core/fixed-token/Count | -21.03% | +13.48% | 1,680 -> 1,680 |
| control/predecoded-Regex/Count | -24.76% | +12.04% | 0 -> 0 |
| control/decode-then-Regex/Count | -22.11% | +18.98% | 6,168 -> 6,168 |
| construction/ascii-run | -22.68% | +6.25% | 94,952 -> 94,880 |

Initial controls improve 24.76%/22.11%; confirmation controls worsen 12.04%/18.98%.
Public lane changes reverse direction, and spreads reach 3.18x for matching and
5.99x for construction. Timing is **inconclusive**. These data neither attribute
a speedup nor demonstrate a repeated above-gate public regression, and do not
establish a precise final 10% bound. The mechanical dead-state deletion is
retained with actual layout/allocation evidence; actual-package/host cost
acceptance remains deferred. No further timing run or new optimization is active.
Public Count uses 3,456 UTF-8 bytes; the finite lane uses 108 bytes, and late-hit
IsMatch uses 4,110 bytes. This is not a same-size finite-overhead qualification.

Final cost interpretation and validation are recorded in `cost-summary.json`
and `validation.json`. Timing ends after this pair and confirmation; no benchmark
snapshot/Status update or actual-package cost acceptance follows from these runs.

## Validation and source closeout

891 focused Release checks passed after the deletion. CI at `097c8343` caught six
Core graph-oracle reflection failures on each platform, while Diff/PythonRe/PCRE2
passed. Moving the original size formulas into the test oracle preserved its
physical-coverage and exact-publication assertions. The repaired affected set
passes 47 checks, and the full local Core suite passes all 39,585 without skips
in 2m05s. No production source changed after the measured `097c8343`.

[Final CI 39620833813](https://github.com/Lokad/Utf8Regex/actions/runs/39620833813)
is green at exact `06c30382` on both Windows and Linux, with the complete Release
solution build and all four fixture projects. Existing generated README parity,
PythonRe/PCRE2 markdown and read-only producer/PE/PDB/compiled-source reuse checks
pass. The latter accepts matching `097c8343` binaries after the test-only repair.

Affected builds were incremental, matching binaries reused, and benchmark children
ran sequentially. No habitual server shutdown, repeated package qualification or
new validation framework. Four snapshots/nine frozen RC1 files retain their
hashes and lengths. Source qualification does not qualify a fresh package.

## Remaining delivery work

Previously rejected PythonRe root pruning remains deferred; canonical literal
counting, empty-literal FullMatch and real candidate hints remain live. Completed
checked-access and prior performance campaigns stay closed. The next separate
delivery boundary remains fresh immutable packages, final API and isolated
Awk/Lython/PCRE2 consumers, and actual-package/host operating-cost acceptance.
Frozen common RC1 stays unpublished with its known PCRE2 defects; do not repack,
relabel or publish those bytes.
