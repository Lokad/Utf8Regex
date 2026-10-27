# Prepare emitted kernels once and retire shadow metadata

2026-10-21. Original checkout `b433aced`, matching producer `097c8343`;
measured source `08cb2ed6`, development `0.4.0-rc.2-dev`. This is source
qualification, not a package release. Frozen RC1 stays unpublished and unchanged.

## Delivered

- Engine selection reads actual prepared capabilities. Deleted the compiled
  analysis record, its two descriptive enums and the two policy classes that
  constructed labels. Selection no longer lowers and discards a kernel plan.
  `EmittedKernel` and `StructuralLinearAutomaton` selected the same runtime and
  emission option; the single internal label now uses the latter, with actual
  emitted capability still reported by that runtime.
- The compiled structural runtime prepares its optional immutable kernel once.
  The program's identifier-family direct Count borrows that same object and
  delegates, including a legitimate null decline. Ordinary construction keeps
  its direct native kernel. Invocation scratch, budgets and result ownership
  are not shared. No new cache, framework, switch or matching algorithm.
- Deleted the unreachable StructuralFamily factory branch, runtime, engine kind
  and unused route marker. Synthetic scalar/RTL examples now use production
  selection; separate live linear-runtime coverage remains. Actual structural
  identifier execution, structural linear and owned scalar routes remain.
- Deleted the shadow search-semantics record, two enums and parameter/copy
  plumbing. No production execution read those flags. Actual candidate kind,
  confirmation, projection, overlap restart, exit and scalar advancement remain.
  Canonical-source, zero-admission and public-operation checks remain.
- Removed three unread Utf8SearchPlan reference fields, keeping constructor
  locals and prepared searches. Deleted the unused positive-class union and
  reverse exact-set wrapper, fallback prefilter forwarding, cache-scope/max
  forwarding, PCRE2 facade group-name/depth/payload forwarding and backtracking
  NoMatch getter. Actual storage scopes, counters, group metadata and matching
  states remain. Core replacement PayloadBytes stays for its reflective lazy
  retention test; static references alone did not justify deleting it.

## Contract and actual storage

Scalar byte coordinates, strict full-subject UTF-8 validation, captures,
RTL/NonBacktracking, synchronous original-token cancellation, deadlines,
work/depth/working/result/state limits and retained result/alias lifetime keep
their existing scope. Removing real acquisitions and smaller storage can reduce
modeled construction/cache requirements; these are conservative resource models,
not bounds on total CLR/JIT memory. The shared object contains immutable code
and plans, never invocation state. Actual Unsafe.SizeOf queries remain.

| Actual x64 carrier | Original bytes | Candidate bytes |
| --- | ---: | ---: |
| Search plan | 2,744 | 2,720 |
| Candidate source | 440 | 440 |
| Operation disposition | 896 | 896 |
| Complete cursor | 1,440 | 1,440 |

Padding absorbs the removed flags in candidate/operation structs. Do not claim
those removals shrink the cursor, heap or RSS. The same compiled kernel object
is now retained by both real consumers; the original had separate objects and
delegates. Both versions still report actual emitted-kernel capability.

## One bounded comparison, closed

Linux/.NET 10.0.12, VM CPU7, default tiers, identical driver DLL bytes and one
fixed original. Five alternating samples follow bounded warmup and calibration;
raw samples, durations/spreads, hashes and driver are alongside this receipt.
Public inputs are 464 UTF-8 bytes, except late-hit IsMatch at 4,115 bytes. Finite
Count uses that same 464-byte input with work1M, depth2048, working/result16MiB,
infinite timeout and no token. Both sides check results before sampling.

The VM is reachable but its 29 GB root disk is full and background logging
services are busy. Drivers/output used an isolated RAM directory, leaving
existing files untouched. CPU preflight records background activity. An initial
untimed harness check corrected a.*b's expected count from one to eight because
of subject newlines; there was no product failure or aborted timed campaign.

| Public operation / unchanged control | Median change | Original -> candidate B/op |
| --- | ---: | ---: |
| Ordinary identifier Count | -0.85% | 3,552 -> 3,552 |
| Compiled identifier Count | -5.09% | 3,552 -> 3,552 |
| Compiled identifier Count, all four finite controls | +0.87% | 3,664 -> 3,664 |
| Compiled identifier Match | -2.62% | 3,552 -> 3,552 |
| Compiled identifier IsMatch, late hit | +6.10% | 3,552 -> 3,552 |
| Compiled identifier EnumerateMatches | +0.47% | 89,168 -> 89,168 |
| Compiled ordered-window Count | +0.10% | 1,680 -> 1,680 |
| Guided Count | +0.74% | 3,552 -> 3,552 |
| Predecoded Regex Count control | -2.13% | 0 -> 0 |
| Decode-then-Regex Count control | -1.82% | 904 -> 904 |

All selected public median changes fit the 10% allowance; no confirmation was
needed. Some window/control samples have outliers up to 1.86x spread; these data
do not prove an exact universal bound or attribute a speedup. Public lanes,
controls and isolated construction are reported separately. Finite Count takes
about 32.06 -> 32.33 us, versus same-size unlimited compiled Count 36.44 -> 34.59 us;
route-specific differences do not establish general finite-limit overhead.
No snapshot, Status, historical experiment or package qualification was reopened.

Compiled construction uses exactly 16 new instances per expression per side:
one untimed warm instance and five batches of three. Identifier construction
allocates **175,168 -> 113,472 B/instance**, down 61,696 B (35.22%). Its observed
median time falls 30.80%, but the short samples and 1.84x candidate spread make
that descriptive, not a precise throughput claim. Ordered-window construction
allocates 92,440 -> 92,248 B, with median time +0.05%. These managed allocation
measurements do not include every JIT/native allocation or demonstrate RSS.

## Validation and remaining delivery

537 first-step focused Core checks passed; the final affected Core set passes
821 and PCRE2 passes 295. Full local Core passes 39,584 without skips in 1m22s.
Initial fixture compilation and an incorrect replacement metadata assertion were
corrected; tests now check the real public scalar outcome rather than a removed
shadow flag. Existing graph, admission, lifetime and resource checks remain.

Generated README parity and PythonRe/PCRE2 benchmark pages verify unchanged.
Existing read-only producer/PE/PDB/compiled-source preflight accepts matching
08cb2ed6 binaries. Four snapshots and nine frozen RC1 files retain hashes/lengths.
CI 39626984182 is green
at exact `08cb2ed6` on Windows/Linux, with all four fixture projects. Incremental
affected builds, matching binary reuse, one source CI boundary; no routine server shutdown or package matrix.

Fresh immutable packages, final API and isolated Awk/Lython/PCRE2 consumer checks,
and actual-package/host operating-cost acceptance remain a separate next delivery
boundary. Rejected PythonRe root pruning stays deferred; canonical literal
counting, empty FullMatch and accepted candidate hints remain live. No new broad
review, accounting precision, checked-access campaign or perf tuning is active.
