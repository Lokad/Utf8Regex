# Managed PCRE2 profile qualification

This ledger records the 2026-08-17 qualification of
`Lokad.Utf8Regex.Pcre2` 0.2.0. It describes the selected managed profile; it is
not a claim of complete upstream PCRE2 compatibility.

## Qualified baseline

- Qualified tracked source: `36fb5db0` (`Refresh performance qualification snapshots`).
- Runtime and benchmark measurement source: `9f3e6bcfb29d`
  (`Document retained performance kernels`). Subsequent commits changed only
  benchmark snapshots and qualification documentation.
- Target: `net10.0`.
- Public API snapshot: 188 lines, SHA-256
  `DA79944F41BC2B47A0A67FFD79C83D224116B20B600A00A4C40CFE9059CE4AEB`.
- Bootstrap closure: `legacy-specific-count = 0` and
  `bootstrap-specific-method-count = 0`.

The exact API is
[`PublicApi.Shipped.txt`](../../tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt).
The implementation contains no `Pcre2ExecutionKind`, complete-pattern
classifier, complete source-pattern equality dispatch, production
`NotImplementedException`, or legacy-specific migration entry.

## Oracle and capability boundary

The frozen oracle profile is PCRE2 10.47 tag `pcre2-10.47`, commit `f454e23`,
8-bit standard matcher, UTF and UCP enabled, Unicode 16.0.0, LF newline,
Unicode BSR, and JIT disabled. The corpus normalizer is version 1. Native PCRE2
is used only to generate frozen vectors; it is not a build or runtime
dependency.

The qualified corpus has 636 operation rows:

| Disposition | Rows |
|---|---:|
| Active and executable | 598 |
| Out of scope by specification | 24 |
| Explicit compatibility backlog | 14 |

The 598 active rows comprise 16 compile, 117 match, 97 detailed-match, 40
count, 37 enumeration, 119 probe, and 172 replacement rows. The 14 backlog
rows are 11 generic recursive RFC mailbox cases and three strict UTF-8 pattern
decoding cases. The local backlog ledgers record a reason
for every non-active row; the executable ledger rejects implicit drift.

Partial matching is intentionally curated. `Probe` has 119 active oracle rows
and is compiled from semantic character/backtracking shapes. It is not a
promise that every normally matchable pattern supports partial probing.

## Repository and package verification

The standard repository run passed 3,243 tests:

| Project | Passed |
|---|---:|
| `Lokad.Utf8Regex.Tests` | 994 |
| `Lokad.Utf8Regex.DiffTests` | 379 |
| `Lokad.Utf8Regex.Pcre2.Tests` | 1,666 |
| `Lokad.Utf8Regex.PythonRe.Tests` | 204 |

The Release solution build completed with no warnings or errors. Public API
snapshot tests, corpus completion tests, bootstrap/source guards, pool-return
tests, cache bounds, resource-limit tests, concurrency tests, and
benchmark-snapshot tests are part of that run.

The qualified `Lokad.Utf8Regex.Pcre2.0.2.0.nupkg` contains only package
metadata, changelog/license/readme/icon assets, and
`lib/net10.0/Lokad.Utf8Regex.Pcre2.dll`. Its nuspec has one implementation
dependency: `Lokad.Utf8Regex` 0.2.0. There are no RID folders, native assets,
external executables, or sibling project references in the consumer graph.
PE metadata inspection reports 213 types, 26 public types, 1,597 methods, zero
P/Invoke methods, and references only the core package plus BCL assemblies.
The reproducible `test-packaged-pcre2.ps1` qualification replaces both sibling
project references in a copied PCRE2 test project with package references, restores
through a fresh isolated package cache, and passes all 1,666 PCRE2 tests
against the packed binaries. Packages with the same prerelease version must
be consumed as a version-coherent pair; a stale global cache can otherwise
combine assemblies from different source commits.

The qualified package SHA-256 values are
`B9A86967E9C6A5A573468F1245849311B24E5354B77E12C688D21DD901A9B35D`
for `Lokad.Utf8Regex.0.2.0.nupkg` and
`57C55E99C9E765657860AD7B6F232EEE25EDAC1953B1E3CC4639EC6ABB21D796`
for `Lokad.Utf8Regex.Pcre2.0.2.0.nupkg`.

## Reviewed core integration points

| Hook | Why it remains |
|---|---|
| Core friendship to the companion | Gives the removable package access to internal flavor-neutral mechanics without making them public. |
| Core friendship to PCRE2 tests | Verifies those internal mechanics and the isolation boundary; it is declared in source, not a project file. |
| `Utf8Regex.ByteOffsetExecution` | Executes the proven common subset while preserving UTF-8 byte coordinates. |
| Prepared byte-range iteration | Lets a flavor-neutral prepared core program expose match ranges without public projection or PCRE2 policy. |
| `Utf8ExecutionDeadline` | One monotonic timeout poller is used by both managed matchers. |
| `Utf8PooledStateStack<T>` | One bounded, disposable invocation-stack primitive serves core and PCRE2 backtracking. |
| `Utf8ScalarNeighbors` | One validated adjacent-scalar decoder supports flavor-owned boundary semantics. |
| `Utf8UnicodeCategoryExecutor` | Reuses flavor-neutral category counting over already-validated UTF-8 while the companion retains PCRE2 category semantics. |

Every hook is tagged `PCRE2-INTEGRATION-POINT`. None carries PCRE2 syntax,
options, error kinds, Unicode policy, capture rules, or replacement semantics.
The core remains buildable and usable without the companion package.

## Duplication and ownership review

The C0-C8 work established canonical core owners for compilation/preparation
(`Utf8RegexAnalysis` and `Utf8PreparedRegex`), validated input and coordinate
projection (`Utf8ValidatedInput`), search facts and candidate merging
(`Utf8SearchFacts` and `Utf8CandidatePortfolioCursor`), prepared kernels
(`Internal.Search`), operation discovery, transactional replacement output,
ASCII byte sets/scalar neighbors, and capture rollback (`Utf8CaptureSlots`).
Those commits have independent core tests and benchmark families; PCRE2 is a
consumer, not their sole justification.

The companion follows the same direction: semantic compiler -> immutable
program -> typed operation runner/cursor -> public projection. The facade
does not own semantic classification or fixture-specific match methods.
Replacement consumes the canonical cursor, and diagnostics do not influence
route selection.

Retained parallel code has a dialect or measured-algorithm reason:

- .NET, PCRE2, and Python parsing, Unicode classification, diagnostics,
  capture policy, empty progress, and replacement grammars remain separate
  semantic authorities.
- Literal and character programs are allocation-conscious fast tiers over the
  same admitted semantics; the backtracking program is their generic PCRE2
  fallback. Differential tests freeze equivalence where their domains overlap.
- Compatible `Utf8Regex`/BCL delegation is kept only for the proven common
  subset and never decides PCRE2-only behavior.
- Partial probing remains a separate operation because upstream partial
  matching has a distinct outcome and capture contract.
- Core search specializations remain separate only where warm benchmarks show
  a useful workload region; they share facts, kernels, and candidate merging.

No flavor-local byte-prefix coordinate scanner, duplicate candidate merger,
replacement byte writer, or full-pattern execution router remains in migrated
PCRE2 operations.

The final core rerun also removed an unreachable paired-window emitted backend
and the last two obsolete delegate/fixed-match replacement engines. Bounded
managed candidate verification now reuses one decoded subject and boundary map
and matches candidate spans without allocating overlapping slice strings.
The surviving literal, structural, and fallback kernels are the measured
strategies listed in `Internal/Search/COMPLEXITY.md`, not accidental copies.

## C# guideline review

The owned-source guard freezes these mechanical counts at zero:

- default-valued method or constructor parameters;
- null-forgiving expressions;
- undocumented abstract or virtual methods; and
- project-file `InternalsVisibleTo` declarations.

The conservative single-caller scan produced 141 lexical candidates. Each was
reviewed in its owning algorithm. Retained nonlocal methods are parser/lowerer
phases, mutually recursive semantic visitors, VM transaction/rollback
operations, overload families, hot Unicode predicates, or independently
testable operation adapters. Local leaf calculations are local functions.
There is no generic `Utilities` owner and no one-off public convenience API.

### Nullable inventory

Every remaining nullable member models one of these absences:

| Member family | Modeled absence |
|---|---|
| `Pcre2MatchException.ErrorKind`, `Pcre2SubstitutionException.ErrorKind` | The public compatibility contract permits an exception without a modeled PCRE2 sub-kind. |
| Match/probe `_groups`, `_nameEntries`, and `_mark`; public `Mark` | A default/no-match ref-struct value has no group/name storage, and no `(*MARK)` may have been encountered. |
| Enumerator `_matches`, `PendingException`, `_managedBoundaryMap` | The selected cursor is not materialized, no deferred error exists, or ASCII input needs no boundary map. |
| Conditional `Assertion` | A conditional is capture/recursion/subroutine based rather than assertion based. |
| Character/backtracking `LeadingAsciiByte` | No safe single-byte leading accelerator can be proven. |
| Backtracking lowerer's `_thenPatches` | No unresolved `(*THEN)` target has been emitted. |
| Backtracking result `Mark` and runner `out mark` | The successful/failing path encountered no named mark. |
| Compatible-backend `TryCreate...` outputs | The PCRE2 request is not equivalent to that backend. |
| Optional boundary maps, group-name/name-entry inputs | ASCII projection or an unnamed capture table needs no auxiliary object. |
| Partial-probe best literal/first partial and character-parser `Rune?` | No candidate has been found yet, or the class term is not one scalar. |
| Replacement message, parsed plan, segment builder, name table, mark, and bounded cache value | The caller supplied no detail, the replacement is not in the simple grammar/cache, no special segment exists yet, or the referenced metadata is absent. |

Temporary pooled arrays use nullable locals only to distinguish “not rented”
from an owned rental in `finally`; no nullable compiled-program payload encodes
a tag invariant.

### Primitive and tuple inventory

No tuple-shaped field, property, or domain result remains; multi-value domains
use named records such as byte positions/ranges, instructions, capture
checkpoints, name entries, and replacement resolutions. Primitive exceptions
are deliberate boundary or representation choices:

- public integers/unsigned integers are PCRE2 byte offsets, counts, capture
  numbers, and match/depth/heap limits;
- parser integers are source offsets, capture/repeat slots, and Unicode scalar
  values;
- VM integers/bytes are compact instruction operands, program counters, and
  UTF-8 code units in hot loops;
- strings are patterns, replacements, capture names, marks, and diagnostics;
  and
- booleans are locally named semantic facts or public projection facts.

Closed policy choices use enums, and cross-phase coordinates, ranges, limits,
instructions, captures, and operation programs use named domain types. A
narrower wrapper around every VM operand would add size/conversion cost without
preventing a state that the instruction factory and invariant checks do not
already prevent.

## Allocation and complexity qualification

The compiled program is immutable. Invocation arrays are stack allocated for
small cases or rented from `ArrayPool<T>`/`Utf8PooledStateStack<T>` and returned
on every exit. Backtracking checkpoints are constant-size markers; capture
rollback is proportional to mutations since the checkpoint. Repeat storage is
sized from compiled slot counts and reachable work, never directly from an
unchecked syntactic maximum. The replacement-plan cache is concurrency-safe
and trimmed to 16 entries. `IsMatch` and `Count` do not construct detailed
public match results; `MatchMany` writes caller-owned storage and reports
truncation.

`PCRE2.Benchmarks.json` separates construction, first-call allocation, warm
temporary allocation, and warm throughput for compatible and PCRE2-specific
operations. Its scaling families vary pattern length, Cartesian alternatives,
dense/sparse candidates, candidate-heavy misses, branching, non-ASCII
projection, character classes, zero-width progress, capture rollback, and
replacement growth at four sizes. The accepted snapshot contains 126
operation rows and 66 scaling points in 16 families; its SHA-256 is
`A565BD95D4B152CF1CFB07F02C88576DAAD557EE2DC33E0EAEAE98669AE2BA25`.

Across each family's scaling points, candidate misses, branch/repeat,
coordinate projection, character classes, zero-width iteration, capture
rollback, and replacement track input/output growth approximately linearly.
Non-result warm allocation is zero (or one byte from integer rounding), while
replacement allocation scales with the required returned array. The accepted
priority report still records constant-factor gaps on several complex
whole-document compatible `Count` cases. Residual inspection attributes the
largest URI, river-window, and email gaps to generic VM work rather than
validation, projection, or wrapper allocation. They remain explicit future
semantic-search priorities; 0.2.0 does not claim throughput parity.
Detailed ratios and the adaptive warm-measurement protocol are in
[`Pcre2PerfLedger.md`](../../bench/Lokad.Utf8Regex.Benchmarks/Pcre2PerfLedger.md).

Required returned arrays/strings and caller-requested detailed results are not
counted as preventable temporary allocation. No retained cache grows with
subject length, and the accepted scaling review contains no unexplained
quadratic curve.
