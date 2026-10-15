# Managed PCRE2 profile qualification

## Private scalar.54 candidate (2026-10-16)

Immutable Core/PCRE2 **0.4.0-scalar.54**, producer
`6632836223957eec125cdfea077135352c5d16b1`, completes the post53 source batch.
The archive is `artifacts/package-qualification/archive/6632836223957eec125cdfea077135352c5d16b1/`;
manifest SHA256:
`C7E8D9C3F9B7FCF2030CE460CCF13BC98C3698C1371CF5C92C4E7599CE00D67D`.
One affected producer closure build and one pack per package/symbol; existing
archive verification passes all seven artifacts, clean revision, NuSpec,
assembly, SourceLink and portable-PDB/symbol agreement.

| Package | nupkg SHA256 | Loaded DLL SHA256 |
| --- | --- | --- |
| Core | DDEB6AAF727FD7F4BBB222FC51BF999E3D31DFDE7FE78E6133ADDD196E64EBDD | 85AE7B5CEF11BC2F54D67B51CDAD3CCDFE4BDAECE3E3967750F7AD03B8A4B475 |
| PCRE2 | 3585081396FBB655F239B270830174A5BACB8900AE912EDF554CEADDC62E76F6 | E325DA5146C119E1AAD48B6771B28E9B5E3EB21EA9AE0BB13C04EAF5EB8FD39C |

Both original detached Windows and Linux runs pass 5,428 checks, zero failures,
against identical Windows-produced packages. Each uses exact source-mapped
references, a tested lockfile, isolated cache, retained TRX and zero
ProjectReferences. Feed/cache/package SHA512 and loaded DLL identities match.
Linux tests start beneath the clean checkout for source-inspection fixtures;
no fixture recovery, restore replay, Linux producer build or repack is needed.
Source Release 5,428 and focused Debug 164 pass. Producer CI passes on both
Windows and Linux:
[run 38817963640](https://github.com/Lokad/Utf8Regex/actions/runs/38817963640).

The selected scalar/capture/MARK contract, cancellation/deadlines, four finite
invocation controls and documented cursor/result lifetimes remain. This batch
includes local-scan workspace reuse, UTF-8 metadata bounds, extended comments
and configured newlines, per-directive x/xx execution and supported scoped/
captured reset forms. Curated native source comparisons cover 35 comment,
48 mode and 80 reset cases; they do not establish complete upstream parity.
Exact DLLs contain only allowed `Unsafe.SizeOf`, no unchecked reads, generated
inline-array helpers, native imports or BCL matcher/result members. Lython's
stricter SizeOf acceptance remains unresolved. Complete public construction,
safe defaults, live retention, host containment and broader profile completion
remain separate objectives.

One bounded current-cost observation used the exact packaged DLLs on the quiet
VM, Release FullOpts/.NET 10.0.12/CPU2, five alternating samples after warmup and
calibration. Finite lanes set work 50M, depth 1024, working 64 MiB and result
128 MiB together; a third adds a live uncancelled token. These are measurement
allowances, not host defaults. Construction is excluded; public validation,
discovery and required projection are included. Selected medians, microseconds:

| Public operation | Unlimited | Finite | Finite+token |
| --- | ---: | ---: | ---: |
| Literal IsMatch,4104B | 0.67 | 0.67 | 0.68 |
| Literal Count,4104B | 0.69 | 1.80 | 1.83 |
| Captured Count,3000B/1000matches | 145.85 | 561.74 | 580.83 |
| Captured MatchDetailed,first match | 1.07 | 1.38 | 1.38 |
| Email Count,3840B/128matches | 654.63 | 662.41 | 674.48 |
| Scalar quoted-class/MARK Count,768B/64matches | 164.78 | 196.00 | 197.21 |
| Scalar quoted-class/MARK MatchDetailed,first match | 4.52 | 5.08 | 4.94 |
| Soft captured Probe,10B | 0.35 | 0.53 | 0.53 |

Email Count allocates about 458 KB per call; finite captured Count still costs
3.85x and basic metering stays. ASCII predecoded/decode-then-Regex controls cost
81.38/83.26 us for captured Count and 42.55/41.73 us for email Count, exposing
remaining gaps. Scalar/control-label and Probe rows exclude non-equivalent BCL
operations. Six samples in two short lanes fell below the requested 50 ms
minimum (actual minimum 43.72 ms), and short-lane spreads reach 70.5%; pool
variation remains explicit. No repeat or general equivalence/Status claim.
This is current operating cost, distinct from the completed fixed-original
checked-access regression gate and the earlier paired workspace experiment.

`artifacts/pcre2-scalar54-delivery-20261016/HANDOFF.md` and `delivery-receipt.json`
provide exact restore guidance, platform locks/TRXs/identities, member inventory,
raw samples, driver and provenance/symbol hashes. Core/PythonRe52 and scalar53
remain unchanged. No PythonRe54, publication, consumer pin/default changes or
adoption is implied. Embedded documentation retains its preparing54 state at
producer freeze; these packages are not repacked for later documentation.

## Private scalar.53 candidate (2026-10-15)

Immutable Core/PCRE2 **0.4.0-scalar.53**, producer
`aba3e050249a44541bcd93a61c131086bdd481b6`, has scoped private producer acceptance.
The archive is `artifacts/package-qualification/archive/aba3e050249a44541bcd93a61c131086bdd481b6/`;
its manifest SHA256 is
`7A4307511451E80BD7AAA3F3F4CB98F674F201B309171D32FDCC62F6312001B1`.
The affected producer closure was built once and each package/symbol packed once.
Existing archive verification checks all seven artifacts, clean source revision,
NuSpec/assembly/SourceLink agreement and matching portable PDB/symbol identities.

| Package | nupkg SHA256 | Loaded DLL SHA256 |
| --- | --- | --- |
| Core | A38187747C996E056471F8DD03CE6820D704ADF15D91B90E35D4DC51BC9FCFCD | 04D6394C2D1497025C216550B5FA3087112E8CE7ED0D03424B8BF4B3DF58167B |
| PCRE2 | 35DE7C1BBE3D5C187965D21106BB879961EFC90EAF21117B70600F0BC88C89F2 | 2690F3E46C8F6198D6C1DEF356C4DCEC4F87F13738DC81C7DF365E7316F00C38 |

Detached Windows passes 5270 checks against these packages, with zero
ProjectReferences. Its original resolved assets, cache/feed and loaded DLLs match.
The wrapper's attempted default lock parameters did not apply to the consumer
helper; a separate exact-version locked restore replay reproduces every library
SHA512 in the tested graph. The original archive is unchanged, and its test run
is not described as lockfile-pinned.

Linux consumes the same Windows-produced bytes with exact source-mapped package
references/lock and zero ProjectReferences. Initially 5259 checks passed and 11
source/snapshot-discovery checks failed because the test directory was outside
the checkout ancestor path. Moving only that directory beneath the clean checkout
and rerunning exactly those 11 with `--no-build --no-restore` passed all 11. Both
TRXs and the recovery filter are retained: 5270 distinct passing checks after
recovery, rather than one uninterrupted green run. No Linux producer build or
repack occurred. Source Release 5270 and focused Debug 225 also pass;
[producer CI](https://github.com/Lokad/Utf8Regex/actions/runs/38806242710) is green.

The scoped contract includes strict UTF-8/scalars, byte ranges, captures/MARK,
the selected profile, synchronous cancellation/deadlines and four finite
invocation controls. It corrects scalar range endpoints, quoted classes and
literal verb labels. Probe retains its curated admission boundary. Complete
construction/default/live-retention policies, host containment and broader
profile completion remain unfinished. Exact DLL inventories contain only allowed
`Unsafe.SizeOf`, no generated inline-array helpers or native imports, and no BCL
matcher/result members. Lython's stricter SizeOf gate remains unreconciled.

One bounded current-cost run used the exact DLLs on the quiet VM, Release
FullOpts/.NET 10.0.12/CPU2: five alternating samples, each at least 50.62 ms, after
warmup/calibration. Finite lanes set work 50M, depth 1024, working 64 MiB and
result 128 MiB together; a third lane adds a live uncancelled token. These are
measurement allowances, not host defaults. Selected medians in microseconds:

| Public operation | Unlimited | Finite | Finite+token |
| --- | ---: | ---: | ---: |
| Literal IsMatch,4104B | 0.70 | 0.70 | 0.72 |
| Literal Count,4104B | 0.70 | 1.82 | 1.81 |
| Captured Count,3000B/1000matches | 144.00 | 551.95 | 580.10 |
| Captured MatchDetailed,first match | 1.05 | 1.33 | 1.35 |
| Email Count,3840B/128matches | 1071.57 | 1082.88 | 1131.12 |
| Scalar quoted-class/MARK Count,768B/64matches | 163.73 | 195.02 | 195.78 |
| Scalar quoted-class/MARK MatchDetailed,first match | 4.23 | 4.72 | 4.87 |
| Soft captured Probe,10B | 0.33 | 0.53 | 0.53 |

ASCII-overlap rows include predecoded/decode-then-Regex controls. Captured Count
controls 80.18/82.09 us and email Count 39.98/40.70 us expose concrete cost gaps.
Email Count allocates about 4.98 MB per call even without finite limits; captured
Count finite overhead is 3.83x. These remain improvement priorities; metering
stays. Sample spreads reach 35% and pooled allocations vary. This is current
operating-cost evidence, not unchecked-conversion regression qualification,
general performance parity or a benchmark-snapshot Status refresh.

Local evidence and exact restore guidance are in
`artifacts/pcre2-scalar53-delivery-20261015/HANDOFF.md`, `delivery-receipt.json`,
platform identities/TRXs, `exact-member-references.json`, `public-cost.jsonl`
and `public-cost-summary.json`. Core/PythonRe52 remains independently qualified;
there is no PythonRe53 designation, publication, consumer pin/default change
or downstream adoption claim. Embedded package documentation retains its
preparing53 state at producer freeze; immutable bytes are not repacked for docs.

## Capture admission correction (development, 2026-10-06)

The published coherent 0.3.0 package pair admitted .NET-style `(?<1>a)` and
`(?<100>a)` definitions; the latter lost its participating capture during dense
detailed projection. Current-source reproduction confirmed the same behavior.
Native PCRE2 10.47 rejects names starting with ASCII or Unicode decimal digits.
Development now rejects those definitions before any backend/foundation is chosen,
using `Pcre2CompileException` and the existing `InvalidAfterParensQuery` kind.
This is a profile conformance correction, not a sparse-numbering API extension.

`Pcre2NumericCaptureAdmissionTests` covers the three naming syntaxes, constructor
and policy-factory surfaces, scoped comments/literals, valid declaration-order
slots, name tables, unmatched captures, backreferences, supplementary coordinates
and replacement callback views. The core .NET numeric-definition contract remains
separate. Public API declarations and native dependency boundaries are unchanged.
Detached coherent packed consumers are qualified with `test-packaged-pcre2.ps1`.
Published 0.3.0 remains the reported affected release; local development packages
with that same version need isolated caches and an explicit source identity.

Additional native-valid probes found quoted-class matching and parenthesized
MARK-label structural gaps. Current source corrects both: shared lexical validation
keeps recognized verb labels literal, and the existing scalar class parser honors
quote boundaries, literal punctuation, empty markers and scalar range endpoints.
Guards preserve actual syntax checks outside those literal regions. Former
admission-only fixtures now assert actual matches and capture slots. Native10.47
controls and focused tests record these source corrections separately from
immutable package qualification; they are not sparse numeric-capture extensions.

## Package provenance enforcement (development, 2026-10-07)

The `test-packaged-*.ps1` flows build the affected dependency closure incrementally
before explicit packing, or reuse matching producer binaries with `-SkipBuild`.
Before detached tests, they require the NuSpec commit, embedded assembly
revision and portable-PDB SourceLink mappings to agree with the captured HEAD;
the PDB identity must also match the assembly. Metadata inspection does not load
or execute package assemblies. `package-provenance.json` records payload hashes
and source state; archival manifests also hash that report and the symbol packages.
Archival qualification requires clean tracked files and no untracked package sources.
The PCRE2 archive verifier independently checks these payloads and rejects legacy
archives without this evidence. Qualify and verify a fresh committed revision with:

```powershell
./test-packaged-pcre2.ps1 -Configuration Release -Archive
./verify-pcre2-qualification.ps1 -QualificationId (git rev-parse HEAD)
```

`test-package-provenance.ps1 -PackagePath <core.nupkg> -SourceRevision <commit>`
checks a valid package and eight deliberately altered fixture copies, including
stale assembly/SourceLink stamps, missing symbols and a mismatched PDB.

The read-only historical audit found stale embedded/SourceLink revisions in 23
of 30 archives (46 of 60 packages), with all recorded artifact hashes intact.
Those archives retain their functional observations, but their claimed clean-commit
provenance is superseded. A stale stamp does not establish which source changes
were actually compiled. The other seven archives have matching metadata; this
alone does not supply the new rebuild/source-state evidence.

## Historical qualification

> Historical ledger for the 0.2.0 qualification (2026-08-21). Current release
> qualification work is tracked in [PLAN.md](../../PLAN.md).

This ledger records the 2026-08-21 qualification of
`Lokad.Utf8Regex.Pcre2` 0.2.0. It describes the selected managed profile; it is
not a claim of complete upstream PCRE2 compatibility.

The qualified files are frozen under
`artifacts/package-qualification/archive/e02af985a3bb5bcbb42dfecc04e40f931e58e1c4/`.
The archive command refused a dirty tracked worktree and refused to overwrite
an existing revision directory. The 2026-10-07 audit confirmed the four artifact
hashes, but both assemblies and SourceLink maps carry `ca4204b5d11c0528cf05f20e4b3f9d5c32601c34`.
The historical source identity below is therefore unproven; the current verifier
rejects this archive. Its functional observations and original ledger remain history.

Generic paths such as `artifacts/nuget/*.nupkg` and
`PCRE2.Benchmarks.json` remain mutable working outputs and are not identified
by the hashes below. The archived benchmark snapshot is qualified only as the
exact input to the generated page and priority report; its per-row measurement
provenance remains mixed and must be refreshed under [PLAN.md](../../PLAN.md) before an
all-case performance claim.

## Qualified baseline

- Qualified package/runtime source: `e02af985a3bb5bcbb42dfecc04e40f931e58e1c4`
  (`Repair snapshot IDs after local history rewrite`).
- The subsequent qualification-ledger commit changes documentation only and
  does not alter the archived packages, API snapshot, or benchmark snapshot.
- Target: `net10.0`.
- Public API snapshot: 171 lines, SHA-256
  `95DE836850E9E37C5E9D5ABAEA501A40231BCA0D7ADA7C72217F6F7E34562083`.
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

The standard repository run passed 3,265 tests:

| Project | Passed |
|---|---:|
| `Lokad.Utf8Regex.Tests` | 1,010 |
| `Lokad.Utf8Regex.DiffTests` | 378 |
| `Lokad.Utf8Regex.Pcre2.Tests` | 1,667 |
| `Lokad.Utf8Regex.PythonRe.Tests` | 210 |

The Release solution build completed with no warnings or errors. Public API
snapshot tests, corpus completion tests, bootstrap/source guards, pool-return
tests, cache bounds, resource-limit tests, concurrency tests, and
benchmark-snapshot tests are part of that run.

The qualified `Lokad.Utf8Regex.Pcre2.0.2.0.nupkg` contains only package
metadata, changelog/license/readme/icon assets, and the assembly plus XML
documentation under `lib/net10.0`. Its nuspec has one implementation
dependency: `Lokad.Utf8Regex` 0.2.0. There are no RID folders, native assets,
external executables, or sibling project references in the consumer graph.
PE metadata inspection reports 211 types, 26 public types, 1,592 methods, zero
P/Invoke methods, and references only the core package plus BCL assemblies.
The reproducible `test-packaged-pcre2.ps1` qualification replaces both sibling
project references in a copied PCRE2 test project with package references, restores
through a fresh isolated package cache, and passes all 1,667 PCRE2 tests
against the packed binaries. Packages with the same prerelease version must
be consumed as a version-coherent pair; a stale global cache can otherwise
combine assemblies from different source commits.

The qualified package SHA-256 values are
`0E6E989BCE08E57ED9E83FB297C8FAB5AEDF1D6C2D6E4AA079F0E9F7C8F32CBE`
for `Lokad.Utf8Regex.0.2.0.nupkg` and
`E4D3C3B3CEFA97F72F13F478A26ED4F9094034BE7F274CC8E491039E11CA9208`
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

The repository-wide Roslyn/source guard freezes these mechanical counts at
zero across core, PCRE2, and PythonRe:

- default-valued method or constructor parameters;
- null-forgiving expressions;
- defensive null guards on non-nullable production contracts;
- undocumented effectively-public declarations; and
- project-file `InternalsVisibleTo` declarations.

All shipped projects generate XML documentation and promote missing-public-doc
warning `CS1591` to an error. A semantic one-reference scan, which resolves
symbols and excludes self-recursion, freezes the repository-wide reviewed
ceiling at 1,019. Retained nonlocal methods are concentrated in parser/lowerer
phases, mutually recursive semantic visitors, VM transaction/rollback
operations, overload families, hot Unicode predicates, or independently
testable operation adapters. This inventory is not a mandate for a bulk
local-function rewrite. There is no generic `Utilities` owner and no one-off
public convenience API.

### Nullable inventory

Every remaining nullable member models one of these absences:

| Member family | Modeled absence |
|---|---|
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

The archived `PCRE2.Benchmarks.json` separates construction, first-call allocation, warm
temporary allocation, and warm throughput for compatible and PCRE2-specific
operations. Its scaling families vary pattern length, Cartesian alternatives,
dense/sparse candidates, candidate-heavy misses, branching, non-ASCII
projection, character classes, zero-width progress, capture rollback, and
replacement growth at four sizes. The accepted snapshot contains 126
operation rows and 66 scaling points in 16 families; its SHA-256 is
`BB3122EDB0DD9C6C8F9AFB1BAAA5407B2C2AC4B7903D6F4D7FC81B03127FF1FD`.

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
