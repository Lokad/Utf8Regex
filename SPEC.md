# Lokad.Utf8Regex specification

`Lokad.Utf8Regex` is a `net10.0` library matching **Unicode scalars over strict
UTF-8 spans**, with byte offsets and lengths as primary result coordinates.
The contract was selected on 2026-10-06; UTF-16 matching parity is superseded.

This document defines behavior and the scope of resource controls, not proof of
complete implementation. All three staged **0.4.0-rc.1** packages are immutable;
current development is **0.4.0-rc.2-dev**. See [release notes](CHANGELOG.md),
[migration](MIGRATION.md), [host integration](HOST-INTEGRATION.md),
[current evidence](CURRENT-PERFORMANCE.md) and the separate
[PythonRe](SPEC-PYTHONRE.md)/[PCRE2](SPEC-PCRE2.md) contracts.
Historical execution narratives and exact scalar-candidate evidence remain in
[the pre-cleanup specifications](https://github.com/Lokad/Utf8Regex/tree/46318565)
and local ignored PLAN.HISTORY.md; their measurements do not qualify new bytes.

## 1. Charter

Build a managed, dependency-minimal, high-performance UTF-8 regex engine that owns supported construction and execution. Preserve useful regex grammar and supported constructs, including inline options, captures and histories, backreferences, balancing groups, conditionals, lookarounds, contiguous anchors and right-to-left matching. `RegexOptions.NonBacktracking` is a separate linear-time mode rather than the default engine.

Matching advances through complete Unicode scalars in either direction. Dot consumes one scalar subject to its newline options; quantifiers count complete atom matches. Successful match and capture endpoints are UTF-8 scalar boundaries. Supplementary scalars are not split into surrogate-half subject units. Combining marks remain separate scalars; matching does not implicitly normalize text or promote dot to grapheme matching.

Use one owned semantic front-end, with native search and execution plans derived from it. BCL Regex may be a comparator for overlapping grammar/behavior and performance, but it is not the semantic authority and must disappear from production execution. Do not retain a second engine or encoding just to reproduce exact UTF-16 parity.

## 2. Hard requirements

* Target framework: **`net10.0`** only.
* Dependencies: **none** beyond the base/standard libraries, **xUnit**, and **BenchmarkDotNet**.
* Implementation model: **fully managed**. No native code, no P/Invoke, no external regex engines, no Roslyn/source-generator package dependency.
* Options model: use **`RegexOptions`** directly.
* Timeout model: use **`TimeSpan`** and throw **`RegexMatchTimeoutException`** for timeout.
* Public primary surface: **UTF-8 spans and byte buffers**.
* Public semantic contract: Unicode-scalar matching over strict UTF-8, with primary byte coordinates. Qualify direct scalar fixtures; use .NET/other comparators only on declared overlapping semantics.
* Performance objective: beat decode-then-Regex and predecoded Regex on result-compatible workloads, while qualifying native UTF-8 cases under their scalar contract. Exclude semantically mismatched comparisons explicitly. Bound pathological backtracking with owned resource admission and cancellation.

## 3. Semantic contract

### 3.1 Pattern language and options

Retain the supported regex grammar and option model, including inline options, `RightToLeft`, `ECMAScript`, `CultureInvariant`, captures, backreferences, balancing groups, conditionals and lookarounds. Apply them to Unicode scalars. Define supplementary literals/classes/ranges and lone-surrogate pattern escapes explicitly as the owned front-end migrates; char-based parser behavior is not an implicit contract. Use .NET grammar observations where they agree with these rules. Reject unsupported syntax deliberately rather than hiding engine dependence behind a fallback.

Core uses ordered choice: at a viable match position, the first successful
alternative follows pattern order and greedy/lazy priorities. For example,
`a|aa` on `aa` returns `a`. RightToLeft reverses the search direction; it does
not select POSIX longest alternatives. Core exposes no POSIX leftmost-longest or
POSIX subcapture-selection mode. The historical [capture assessment](CAPTURE-COST.md)
records that consumer boundary; scalar matching does not imply POSIX selection.

Adjacent high/low surrogate spellings, raw or escaped, form one scalar within a literal run or one character-class member. Classes order range endpoints by numeric scalar value, including bounds above U+FFFF; descending supplementary bounds throw `ArgumentException`. An unpaired surrogate atom denotes no scalar and cannot match strict UTF-8. It can be repeated zero times, and negating a singleton class containing it admits every valid scalar. Groups, assertions and separate classes cannot join two such atoms into a scalar. Numeric intervals may span the surrogate gap, but matching considers only valid scalars. An explicit `[\u0000-\uffff]` includes BMP scalars; singleline dot includes every scalar plane.

The explicit finite Core repeat maximum `2147483647` is distinct from infinity.
Sentinel-based native families decline that edge. Consuming NonBacktracking
expansion still requires instruction/state admission and may fail construction;
compact zero-width repeats preserve their predicate/capture without proportional
expansion. This does not widen the accepted numeric grammar.

Invalid pattern syntax throws an owned `ArgumentException` naming `pattern`,
with a reason and an offset in the UTF-16 string pattern. These diagnostic
coordinates do not change scalar matching or byte result coordinates. BCL parse
exception types, error enums and messages are comparator observations, not the
product's diagnostic contract. A valid pattern that exceeds a transitional
representation is migration debt, rather than invalid syntax.

Classes proven empty during parsing use the existing impossible atom. A negated
complete scalar range is empty even with subtraction; unions of complementary
digit, whitespace or individual category predicates can establish that range.
The parser still validates the complete class and subtraction syntax, and public
operations still validate the whole UTF-8 subject. NonBacktracking repeats over
these impossible atoms need no expansion proportional to their numeric bounds;
consuming repeats retain their construction limits. Capture declarations remain
available, and a zero-minimum repeat leaves its unvisited captures unsuccessful.
Complete-range unions are recognized before case-fold range enumeration;
subtracted classes retain their own folding and complete syntax validation.

An empty pattern has one match at every scalar boundary, including both ends:
Count on `😀` is two, and Count on `a😀b` is four. Non-overlapping discovery
advances past an empty result by one complete scalar in the selected direction.
Count has no coordinate result, so primary Count must not construct a whole-
subject UTF-16 map just to enumerate positions. Explicit UTF-16 start APIs may
resolve their scalar-aligned starting boundary as admitted interoperability work.

RTL Count may stop after one failed candidate only when the first emitted
instruction is a mandatory contiguous anchor. A `\G` in one alternative does
not constrain other alternatives: `a|\Gb` over `a xa` must count both `a`s.
The ordinary candidate loop continues over complete scalars and keeps the same
invocation's work, cancellation/deadline and scratch authority. Correctly visiting
additional candidates can require more work than the former incorrect early
stop. Empty-match advancement and retained capture validity remain unchanged.

### 3.2 Replacement semantics

Retain the .NET-style replacement language (`$1`, `${name}`, `$&`, `$```, `$'`, `$+`, `$_`, `$$`) and numeric-group parsing rules, expanding captures discovered under scalar matching. Invoke replacement evaluators once per match. Right-to-left replacement discovers matches from the end and assembles output in subject order. UTF-8 output remains well formed; string-returning APIs materialize the same scalar result as a .NET string. They are not surrogate-half compatibility escape hatches.

Encode each replacement literal and each string evaluator return independently as UTF-8. A valid UTF-16 surrogate pair within that fragment denotes one scalar; an unpaired surrogate becomes U+FFFD, as with `Utf8ReplacementWriter.Append(ReadOnlySpan<char>)`. Fragments cannot join surrogate halves across substitutions, matches or original-subject gaps. This string-boundary encoding rule does not relax strict validation of UTF-8 subjects, replacement-pattern bytes or completed byte-evaluator output.

### 3.3 Culture and casing

`IgnoreCase` compares complete scalars using qualified Unicode case-equivalence rules; pin the Unicode data and culture behavior as those owned paths migrate. `CultureInvariant` selects invariant behavior. Culture-sensitive options retain the construction culture and include it in cache identity, including scoped inline folds. BCL behavior can qualify overlapping cases but cannot supply the production engine or justify folding surrogate halves. Supplementary casing and Turkish-I need direct scalar fixtures.

For the I cluster, invariant folding pairs `I` with `i`, leaving `İ` and `ı`
distinct. Non-Turkic culture folding groups `I`, `i` and `İ`, leaving `ı`
distinct; Turkic folding pairs `I` with `ı` and `İ` with `i`. The owned program
retains this construction-time choice through classes, assertions and references,
including scoped options. Changing the ambient culture during matching cannot
change its case behavior. Supplementary comparisons operate on complete Runes.
Core pins supplementary simple-case mappings to Unicode 16.0, matching the
owned model rather than the host runtime's casing version. All 282 pairs are
one-to-one within the supplementary planes, stored in 11 fixed read-only runs;
literal/class construction and scalar/reference comparison use the same lookup.
BMP/I-cluster rules and original invocation/construction controls remain. Source attribution and the Unicode license accompany the derived data.
This pins simple-case data, not every Core property table: general categories/shorthands
currently use runtime scalar property queries. PythonRe owns its separate pinned
15.1 property rules. These data-source scopes do not change scalar matching width.

### 3.4 Timeout behavior

Retain `TimeSpan` configuration and `RegexMatchTimeoutException` identity, with an
owned invocation deadline that is never restarted by a candidate, nested
verification, result projection or lazy advance. Check the deadline after bounded
library work and before publishing a result. Validation, search, matching,
capture projection, copying and output belong to the same operation authority.
Construction has its own contract; reusable instances and caches never retain an
evaluation deadline.

Core template replacement starts its absolute deadline
before cold lookup, UTF8-template validation/decoding and plan preparation. The
same deadline reaches owned execution and existing native cursors; parsing does
not earn a new match interval. Cache waits and cold construction checkpoints
observe the original token/deadline. Interrupted preparation is not published;
another factory's interruption is retried once under the caller's own authority.
Successfully prepared code survives a later matching interruption. Parent
construction hooks/capacity/authority restore on failure, and spent work remains.
Construction work/bytes keep their separate fixed backstops; they are not newly
charged to invocation work/result caps. This does not complete every preparation
or retained-storage bound.

UTF8 template fallback decoding admits its one text key before acquisition;
native direct-literal destination routes still avoid that string. Controlled
validation uses existing scalar-aligned blocks and preserves absolute invalid-byte
offsets and template-before-subject precedence. Pre-cancellation still wins and
null-template failures precede elapsed-clock checks. Their parameter is the public
`replacement`, rather than the old cache comparer argument `obj`. No new engine, budget
owner hierarchy, execution mode, allocation registry or graph accounting.

For Core/PythonRe calls with a finite `MaxWorkingBytes` or `MaxResultBytes`,
timeout exceptions have an empty `Input`: failure construction does not decode
the subject into an unadmitted diagnostic string. The existing route's `Pattern`,
configured `MatchTimeout` and exception type are preserved. Unlimited and
work/depth-only policies retain their existing diagnostic input. This removes
one subject-sized failure allocation; exception objects, pattern metadata and
complete process-memory containment remain outside the byte model. PCRE2 keeps
its independent failure contract.

Responsiveness is measured in bounded library work between checkpoints, not an
exact wall-clock return time. OS scheduling, CLR compilation/GC and arbitrary
evaluator code cannot be synchronously preempted by the library. Observe the
deadline before and after invoking an evaluator. Checkpoint precedence and those
external exclusions require qualification; a runtime's timeout rounding or
checkpoint defects do not define the owned contract. Full route coverage remains
an acceptance gate; existing scoped evidence does not prove every route.

Replacement-literal byte counting,
encoding and validity scans are bounded by 2048 UTF-16 code units, finishing a valid surrogate
pair at the boundary (at most 2049 units). These are template-string operations,
not subject matching. Zero-work checkpoints use the existing construction owner
and its inherited cold-replacement token/deadline. Successful numeric work and
storage charges, bulk BCL encoding, one exact admitted byte array, valid-template
text reuse and per-fragment repair remain unchanged. Earlier interruption is
intentional; this does not qualify all preparation or add a public byte/RSS cap.

Scalar-class execution keeps linear range and independent-table
scans bounded under the original invocation. The first 32 entries of each range
or table scan fit within its atom/segment cost; each further block spends a work step
and checks the deadline directly. Folding uses the same authority and a denied
scan cannot become a successful negated predicate. Ordinary, NonBacktracking and
terminal-repeat execution share the existing class helper. Subtraction segments
store the existing six shorthand bits instead of retaining/scanning duplicate
shorthand arrays. Longer predicates may now exhaust a work cap that previously
omitted their inner scan; storage and scalar semantics are preserved. Full construction, retained-storage,
safe-default and process-memory guarantees require separate evidence.

### 3.5 UTF-8 validity

The public UTF-8 APIs accept **well-formed UTF-8 only**. Invalid UTF-8 must not be silently repaired or replacement-decoded on the main API. The low-level .NET UTF-8 transcoder exposes invalid-data signaling when replacement is disabled; `Lokad.Utf8Regex` should follow that spirit and reject malformed input. Public failure mode: throw `ArgumentException` with a precise message and the offending byte offset when the input is ill-formed. ([Microsoft Learn][7])

Validation covers the entire subject even when matching could stop early.
Validation-only callers may omit derived counts/flags; their sentinels cannot
supply projection metadata. Metadata-producing callers retain exact counts and
flags. ASCII prefix reuse must rebase malformed-byte offsets and scalar counts
correctly. Resource denial or interruption can precede discovery of an invalid
tail, but a successful early match cannot bypass that tail.

### 3.6 Scalar boundaries and coordinates

Every successful core match and capture on valid input has a representable UTF-8 byte range. For `😀`, dot returns one match at byte offset 0 with byte length 4; `^.$` succeeds and `^..$` fails. Dot on `a😀b` returns ranges (0,1), (1,4) and (5,1). `^(.)\1$` matches `😀😀`, retaining the complete first scalar in group 1. Dot on `e` followed by U+0301 returns two scalar matches.

Existing UTF-16 coordinate properties are derived interoperability coordinates. They do not affect matching width or permit a capture endpoint inside a scalar. Primary byte operations must not require whole-subject UTF-16 conversion merely to emulate .NET. Remove obsolete half-unit encoding, non-representable-match exceptions and parity-only execution/output paths as qualified scalar consumers replace them.

### 3.7 Cancellation and resource limits

Every execution operation has an explicit `CancellationToken` twin. The token is observed **before matching begins**: an already-canceled token throws `OperationCanceledException` carrying that token, ahead of input validation, limit checks, timeout checks, and matching. There are no optional token parameters; each twin is a distinct overload.

The target requires synchronous cooperative cancellation during validation,
search, verification, matching, capture projection, copying and output, after
bounded library work and before result publication. Lazy enumerators retain the
invocation token and cumulative authority through each `MoveNext`, including a
long failed advance before any result. Copying a cursor must not restart its work
allowance, deadline or polling cadence. The token belongs to the operation, never
to the reusable regex or cache. Check before and after evaluator callbacks;
callback checks supplement engine polling. Cancellation throws
`OperationCanceledException` carrying the original token. The scheduling and
external-code exclusions in 3.4 apply; no asynchronous worker is required.

Coverage must be proved route by route; entry-only checks and semantic
comparisons alone do not qualify interruption. Production BCL matcher/result dependencies are removed in the
qualified development source and must not be reintroduced to service a gap.

Controlled routes retain their invocation token/deadline through validation,
search, projection and lazy advances. Metered
invocation transfers and copies share cumulative work/depth counters from entry;
copying a value enumerator preserves its independent position without restarting
that work allowance or the diagnostic polling cadence. Separate operations create
separate counter owners. This synchronous internal owner is not a public shared
meter and does not support concurrent advancement of copies. Infinite None-token
calls without diagnostics retain inline counters unless a forkable cursor needs
a shared storage owner. Controlled invocation copies share their aggregate ledger.
Owned mapped value cursors and split views keep published coordinate maps and
linear viability tables charged for their shared invocation. EOF, failure or
physical collection of a payload does not refund its published reservation.
Permitted copies retain independent positions and ordinary managed references;
retained results/views remain valid after another copy ends. The ledger contains
numbers and does not root abandoned payloads. There are no finalizer leases,
atomic collection-release queues, alias registries or GC-dependent credit refunds.
Setup failure before publication releases its own loans immediately, preserving
borrowed storage; exclusive temporary VM scratch also releases synchronously.

Logical reserved capacity can exceed physically live payload capacity. This
intentional conservative rule can cause earlier denial when another consumer uses
the same internal finite working allowance after abandonment/EOF; a separate
operation has an independent allowance. It preserves the covered live-capacity
upper bound without promising prompt GC, caller-retention bounds or RSS. Existing
borrowed span/value APIs require no new disposal contract. These payloads are exact
managed arrays; no pooled/native cleanup is deferred or removed. Split string
getters decode only their selected UTF-8 slice and retain no prepared decoded
subject or loan attachment. Lazy getters and fixed accounting metadata retain
their documented separate scope. Public working-byte admission is qualified
separately from this lifetime simplification; its covered contract is below.

PythonRe's incremental `CreateMatchCursor(ReadOnlyMemory<byte>)` uses an explicit
reference owner alongside the copied value-cursor contract. Aliases share its
position and lifetime; early abandonment requires disposal. EOF, failure and
disposal synchronously release scratch and subject references, with no finalizer
or collection-release ledger. Keep the borrowed subject unchanged and its memory
manager alive until closure. Detailed snapshots own their arrays/text and remain
valid after advance or disposal. Sequential host waits are permitted and count
against the original absolute deadline. Concurrent/reentrant access is rejected.
The existing value/span contracts remain intact; this cursor
does not itself bound externally retained results. Work and result-capacity traffic
are cumulative through validation, discovery and required snapshot shaping; working
release never refunds the capacity charges described in Section 3.7.

Core and PythonRe accept positive finite `Utf8ResourcePolicy` values for
`MaxWorkSteps`, `MaxDepth`, `MaxWorkingBytes` and `MaxResultBytes`, independently
or together. Null is unlimited; zero is invalid. Defaults and static cached
operations remain unlimited. Different operations have independent allowances;
this is not an application-wide shared meter. PCRE2 maps the same policy into
its separately documented managed limits and failure vocabulary.

One metered invocation spans full validation, discovery, matching, required
coordinate/capture projection, output and publication, including lazy advances.
The first configured denial is sticky across copies, requested totals saturate,
and denial is distinct from no-match, cancellation and timeout. Cancellation and
the original absolute deadline remain authoritative before covered admission and
publication. Array/CLR representability failures remain separate capacity errors.

| Control | Covered scope | Core/PythonRe denial |
| --- | --- | --- |
| `MaxWorkSteps` | Cumulative bounded engine/checkpoint work, never refunded | `ExecutionWorkSteps` |
| `MaxDepth` | Simultaneously live backtracking/continuation/assertion frames or pending linear alternatives | `ExecutionDepth` |
| `MaxWorkingBytes` | Conservative active major array/text/output-control capacity, including old/new growth overlap | `ExecutionWorkingBytes` |
| `MaxResultBytes` | Cumulative modeled major result/staging capacity requested before acquisition, never refunded | `ExecutionResultBytes` |

Denial throws `Utf8ResourceLimitExceededException` carrying the configured limit,
resource kind and requested count. Units describe the selected algorithm; they
are not bytes processed, elapsed CPU, cross-version minima or native-PCRE2 units.
Removing unnecessary work/storage may admit tighter policies. Native shortcuts
still fund bounded searches/comparisons and terminal interruption; PCRE2
attribution never permits removing basic metering. Finite routes may have a
different operating cost from unlimited shortcuts.

Depth is not pattern depth, table width, cumulative work or a memory bound.
A viability-table replay with no pending stack uses no depth allowance. Completed
live depth can fall between advances while the cumulative invocation persists.

Working admission covers slots, repeat/frame state, captures/projection,
coordinate maps, linear tables, temporary discovery/result buffers and output
staging. Headers, rounded payloads and overlapping physical capacities are
admitted before acquisition. Denied growth leaves existing state unchanged.
Exclusive scratch releases synchronously; published maps/tables that copied
cursors can still use keep their invocation-long reservations. Logical charges
may exceed physically live bytes. Publishing a result can release its working
loan without refunding cumulative result traffic.

Result admission covers rounded arrays/strings, output controls and growth/RTL
piece tables, capture histories/projection, Python capture/name arrays,
collection staging/final roots and coordinate maps. Temporary/final and shared
storage may be counted again, so this can deny earlier than a final-size check.
Fixed range/boolean/count values, borrowed subjects/program metadata, incidental
object overhead except fixed output controls, arbitrary callback allocations,
external caller retention and later standalone getters are excluded. Required
callback context/output shaping is covered. A matched-text length is not the
result-capacity limit. Retained results/aliases remain valid after later denial.

Pattern/replacement preparation keeps separate construction admission. Finite
working bytes additionally limit covered Core cold replacement-parser capacity,
including UTF-8 template-key decoding, through a nested construction ceiling.
PythonRe applies this separate ceiling to cold templates and execution-triggered
FullMatch preparation. `ConstructionBytes` can precede execution denial; totals
include enclosing construction reservations. Failure restores parent capacity/
ceiling, removes failed publication and preserves spent work. Warm text-key hits
need no preparation scope; UTF-8 keys still require decoding admission.

Preliminary byte-template validation admits work before scanning. An active
construction owner supplies cumulative admission and bounded checks; otherwise
the fixed construction-work ceiling applies directly. An oversized malformed
template can therefore report `ConstructionWorkSteps` before its invalid tail.
Python template lookup also admits one invocation step per 2048 UTF-16 template
units, rounded up, before cold or warm cache access. Its parser retains separate
construction work; these are not one combined construction/execution/retention
budget. Cached plans remain valid when a later caller cannot fund lookup.

These controls do not bound all helper allocations, arbitrary host retention,
callback execution, complete lazy preparation, CLR/JIT metadata, GC traffic or
RSS. Common live-retention envelopes and qualified safe finite defaults remain
future objectives. The existing [host composition contract](HOST-INTEGRATION.md)
records usable guarantees and their exclusions separately from those objectives.

## 4. Public API

The exact reviewed declarations are the source of truth in
[PublicApi.Shipped.txt](tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt), checked
against the built library. This specification does not maintain a second partial
signature catalog or require exhaustive .NET overload symmetry. See the
[package README](src/Lokad.Utf8Regex/README.md) for examples.

Core exposes `IsMatch`, `Count`, `Match`, `MatchDetailed`, `EnumerateMatches`,
`EnumerateSplits`, `MatchMany`, replacement APIs and capture metadata. Pattern
strings are parser interoperability input; UTF-8 pattern entry points validate
strictly. `Compile` exposes separate eager construction controls. Options use
`RegexOptions`; `Compiled` changes strategy without changing language/results.
Overloads without an explicit timeout use `DefaultMatchTimeout`, initially
`Regex.InfiniteMatchTimeout`.

`Utf8ValueMatch` is storable coordinate data. Detailed/evaluator contexts provide
groups and capture history; captures follow the documented ordering. Borrowed
ref-struct match/group/capture/split views require valid original subject storage
and cannot outlive their frame. Successful byte accessors address complete scalar
ranges; unsuccessful access follows the type's documented contract.

`EnumerateSplits` preserves count/empty-element/RTL ordering with scalar
delimiters, and does not emit captures as extra split elements. Comparators apply
only when delimiter semantics overlap. UTF-8 replacement-pattern overloads parse
the replacement language rather than treating their bytes as an opaque literal.

`TryReplace` reports `Done` or `DestinationTooSmall` for sizing. Shortage returns
`bytesWritten == 0` and leaves the destination unchanged. Invalid input/template,
construction, resource, cancellation and timeout failures still throw; a later
interruption during copying can leave a partial destination. Input/template spans
may overlap the destination. Overlapping input stages scalar output before
copying; disjoint input keeps eligible native execution and all controls.
Evaluator replacement remains allocating; destination control applies to fixed
templates.

### 4.2 UTF-16 offset compatibility APIs

Explicitly named `FromUtf16Offset` APIs accept UTF-16 coordinate offsets, resolve
them to complete scalar boundaries and execute the same scalar engine. An offset
inside a supplementary scalar throws `ArgumentOutOfRangeException` naming that
parameter. Ordinary native methods use byte coordinates. Validation, offset
resolution and execution share the original invocation and absolute deadline.
Raw shortcuts cannot bypass finite limits, timeout or a live token.

### 4.3 Intentional API extensions beyond baseline .NET Regex

`MatchMany` writes at most the destination length, returns the number written and
sets `isMore` if additional matches exist. It does not imply a continuation
protocol. `Analyze` exposes advisory native eligibility, minimum byte length,
exact-literal and required-literal facts; it does not redefine matching or dump
internal plans. `CopyRequiredLiterals` exposes stable prefilter facts;
`FixedOffsetInBytes == -1` denotes a required literal without a fixed byte offset.

## 5. Architecture

### Owned compilation and execution

Use one owned semantic front-end and one shared final capture catalog. The
pipeline is parse, analyze, prepare, immutable prepared regex, invocation runtime.
Candidate search and full execution are separate lowerings of those facts.
General execution borrows the once-prepared scalar program over the complete
validated subject. Native plans accelerate proved subsets and must retain scalar
semantics; an unavailable native projection declines optimization, not grammar.
Candidate bounds cannot truncate assertion/anchor context. RTL traverses the
subject backwards without reversing or decoding it. Instances are immutable and
thread-safe; individual copied cursors are not concurrently advanceable.

`NonBacktracking` is a distinct owned linear mode for its admitted regular
language. Its discovery guarantee spans the complete operation, including repeated
empty matches. It preserves ordered choice, greedy/lazy rules and last captures;
unsupported grammar fails before planning. Shared linear tables remain admitted
while any permitted cursor copy can retain them. See the source
[architecture](src/Lokad.Utf8Regex/ARCHITECTURE.md) for layer boundaries.

### Construction admission

One synchronous construction owner spans nested Core/adapter preparation. Covered
major buffers, instruction/state expansion, traversal work and live explicit
frames are admitted before acquisition. Fixed backstops are 1,048,576 instruction
slots, 64 MiB covered bytes, 67,108,864 work units and 65,536 live frames. Positive
`Utf8ConstructionPolicy` values tighten those ceilings; null retains them.
Ordinary constructors retain defaults. Explicit `Compile` uses its own original
token/deadline, checks pre-cancellation before argument/pattern work and checks
again before publication. It does not retain this authority in the expression.
It does not consult static caches or replace an already active construction owner.

Construction denial reports `ConstructionWorkSteps`, `ConstructionDepth`,
`ConstructionInstructionCapacity` or `ConstructionBytes` with checked requested
totals. Cancellation carries the original token. Deadline failure uses
`RegexMatchTimeoutException` with the construction duration and empty Input;
byte-pattern failures also omit Pattern to avoid diagnostic decoding. Text
patterns retain their supplied Pattern. Resulting invocation policy remains
independent of construction policy and authority.

Covered arrays use actual `Unsafe.SizeOf<T>()` element strides, a conservative
32-byte header and 16-byte payload rounding. Text includes its terminator. Shared
empty payloads cost zero. Covered helper objects use conservative numeric
allowances, generally at least 128 bytes; nonliteral runtime wrappers use a 2 KiB
allowance and retain independent major-payload admission. These are modeled
charges, not exact physical object sizes. Dead helpers are not rooted by a graph
registry. Factory failure releases its unpublished capacity; child rollback
restores byte/instruction/live-depth loans without refunding spent work or
invalidating borrowed/retained objects. Growth and final copying admit old/new
overlap. A speculative optimization cannot reset the parent's work allowance.

Core preparation no longer builds or charges the unused Enter/Exit instruction
vector formerly derived from its execution tree. Native branch plans execute
directly; backreference eligibility uses the owned tree's existing feature facts.
The real scalar and structural VM instructions retain their admission. Numeric
construction/cache charges can therefore decrease for unchanged patterns; they
are conservative implementation costs, not stable per-pattern quotas.

Covered producers include scalar IR/lowering, class/subtraction data, capture
metadata, instruction/analysis vectors, explicit parser/preparation frames,
construction maps/arenas and sorting, selected native search buffers, dynamic
emission and byte-safe graph construction. Arrays and strings retain independent
extent-based admission. Maps preserve collision/rehash interruption and atomic
failed growth; they are not retained merely to meter iterator helpers. Temporary
metadata enumerable views/boxing have no individual allocation charge. Immutable
lookups spend the currently active owner's work, never a closed original owner.
The numeric arena keeps retained/temporary totals without retaining every object.

Scalar class endpoints/counts use integers through exclusive U+110000; more than
65,535 entries cannot silently reject valid owned matching. Complex native class
projections may decline while the scalar program remains available. Scalar folding
pages, final class/range buffers and source/lifted linear graphs retain their major
admission. Borrowed capture-body arrays preserve opening order; declaration slots
remain stable through captures, references and lookbehind width analysis.

Covered work includes validation/decode passes, initialization/copy extents,
parser/conversion resumes, map probes/rehash and sort comparisons/swaps. Bounded
checkpoints use the same owner. Pattern UTF-8 decoding uses scalar-aligned blocks
of at most 4096 bytes and preserves absolute malformed-byte offsets. Native
pattern encoding/template counting polls between 2048 UTF-16 units, completing a
boundary pair (at most 2049 units). Pattern interoperability never changes subject
matching width or turns an impossible surrogate into a replacement literal.
Cleanup unwinds live depth and scratch without consuming extra metered work.

Dynamic emission admits IL, labels/fixups/locals, method/type token caches,
dispatch vectors and stack-analysis queues, including growth overlap. Serialized
IL and its managed runtime copy count toward instruction capacity. Parameter,
code and local-signature managed copies are admitted before CLR calls; one
published delegate records their final lengths for conservative retention.
Temporary emission scratch uses a numeric subtotal. It is not retained by the
published delegate. Original interruption is checked after opaque delegate
creation. CLR reflection/resolver/signature metadata and native JIT storage remain
excluded; production does not inspect private CLR fields to account for them.

Byte-safe DFA construction admits graph descriptors, state/edge vectors, closure
keys, maps and queues under the same owner. The 256-DFA-state, 2048-NFA-state,
4096-transition and one-million-optimizer-step ceilings are optimization declines;
shared resource failure/cancellation/deadline propagates. Optional bodies with
loops require the full admitted construction proof, including repeat bounds;
incomplete or duplicate transitions cannot establish eligibility.

Explicit PythonRe compilation eagerly prepares its derived FullMatch program,
including its instruction copy/metadata, even when unused. Constructors retain
retryable lazy preparation. PCRE2 has separate [construction coverage](SPEC-PCRE2.md):
literal programs admit temporary Rune staging and retained UTF-8 buffers;
character token/class/property buffers independently admit capacity and bounded
interruption, without redundant Core preparation. Lexical validation also admits
major scope buffers and bounds group depth before semantic parsing. These partial
backstops do not claim every parser/helper acquisition, all lazy preparation,
caller retention, cumulative GC traffic or RSS. Common retained/default policy
completion remains separate work.

### Native proof obligations

Every optimization preserves the complete owned grammar and selected branch
order, not just a textual spelling or favorable benchmark case. Proofs must retain
real wrapper depth, lexical options/culture, capture/atomic effects and zero/NUL
members. Failed proofs leave authoritative scalar execution available.

- Fixed byte distances require equal known scalar UTF-8 bounds for every preceding
  node and checked addition. Lower bounds saturate; overflowing upper bounds become
  unknown. Variable prefixes cannot contribute their minimum as a fixed offset.
  Nullable first/last sets include all branches and empty-path assertions.
- ASCII intersections require an ASCII subject and complete scalar/case-fiber
  proof. Folded searches retain Kelvin screening and their Unicode route. Canonical
  scalar classes and full literal verification remain authoritative.
- Non-rewinding separators require disjoint following identifier/suffix classes,
  including multipart suffixes. Atomic tails cannot lose their commitment.
  Optional branches require the relevant prefix-free/disjoint continuation proof.
- Zero width alone does not prove a greedy endpoint. Required-end assertions,
  contiguous origins after variable choices, multiline LF and nullable assertion
  continuations can require backtracking. A terminal class-repeat shortcut must
  prove its complete root/continuation guard. `([a-z]+)-` can omit useless rewind
  frames; `([a-z]+)a` cannot. Assertion RequiredEnd keeps the general route.
- Independent scalar class range/table scans meter the first 32 entries within
  their atom cost and charge/check further bounded blocks. Negation cannot turn a
  denied predicate into success. Captured, lazy, RTL, nullable or tailed class
  repeats retain state unless an existing complete proof admits elision.
- Literal/family counters preserve ordered nonoverlap and stateful exclusions.
  Count may omit unobserved capture values only when no references, conditionals,
  lookarounds, balancing or shared/explicit declaration effects require them.
  All remaining instructions and candidate searches stay metered.
- Specialized email/URI/date/query/quoted/line/balancing plans prove their entire
  grammar, including optional features, whitespace, anchors and capture identity.
  A URI body's two required characters are two scalars. Quoted escapes consume
  one complete scalar; unescaped LF and final-LF anchors follow their actual
  grammar. Greedy query/fragment extent, CR, NUL, optional backtracking and dotted
  decimal branch order cannot be inferred from a broader shortcut.

Prepared candidate kernels advance monotonically and preserve complete context
for verification. Discriminators select a literal for full verification; a few
checked anchors do not certify an unchecked prefix. Prepared search reuse must
preserve original grouped branch order. Finite native searches fund bounded windows
and comparisons under the original invocation, even when no VM scratch is needed.
Kernel bounds and adversarial witnesses are in
[COMPLEXITY.md](src/Lokad.Utf8Regex/Internal/Search/COMPLEXITY.md).

### Retention, scratch and results

Preparation caches use conservative per-program or declared root/bounded-child
charges, separate metadata/payload caps and simple eviction. Pattern text and
shared storage can be counted again. Successful covered construction, complete
facade metadata and bounded lazy children supply numeric charges; no production
object-graph walk, identity deduplication, reference count or transactional manifest
is required. One 16 KiB constructor allowance covers fixed frontend/empty metadata;
major dynamic storage retains admission. Oversized keys prepare uncached before
hashing impossible retained text. Null static patterns fail with the public
`pattern` parameter. Pending preparation/generation coordination, cancellation,
clear/shrink during outside-lock preparation and held program/result validity
survive eviction. Closed interruption/storage probes do not mutate the cache
under its lock; diagnostic callback reentrancy at publication is not supported.
Independent test-owned major graph measurements validate conservative coverage.
Opaque CLR/JIT metadata, arbitrary host retention and RSS remain outside the bound.

Runtime scratch admits complete physical extents before allocation, including
slot pairs, repeat/history/frame stacks, pending/register/visited vectors,
coordinate maps and growth overlap. Nested assertions retain parent loans.
Failure releases exclusive acquisitions; copied cursors retain published maps/
linear tables without GC-dependent credit refunds. Returned history/group arrays
are immutable and never returned to a pool while published. Temporary endpoint
staging may use the existing bounded exact pool; finite storage calls can borrow
existing leaves but cannot grow retained pool storage. No finalizer leases or
alias registry replace the documented ownership contract.

Capture projection and sorting share the caller's original work/token/deadline.
Working loans remain live through overlap with result staging and detach on
publication; cumulative result charges remain spent. Python reference cursors
release scratch/subject ownership synchronously at EOF, failure or disposal;
retained snapshots survive. These mechanisms preserve covered major capacity,
not arbitrary external result retention or process memory.

## 6. Performance and benchmarking

Performance rules:

* production paths must stay UTF-8-native; do not route normal execution through `Encoding.UTF8.GetString(...)` + built-in `Regex`
* search and full matching are distinct concerns; search-first execution is expected
* prefer checked BCL/span primitives and preserve measured SIMD algorithms; retain a custom kernel only with evidence
* avoid whole-input string materialization except for explicit compatibility APIs such as `ReplaceToString`

Testing and benchmarks:

* direct scalar fixtures define semantic acceptance; .NET 10 and other engines are differential comparators only for overlapping cases
* tests cover grammar, scalar matching, supplementary ranges/captures, replacement, RTL, empty advancement, malformed input, boundaries, resources and cancellation; retarget old surrogate-half witnesses to scalar outcomes
* benchmark methodology must use warm throughput, not cold-start `Dry` numbers, for parity decisions
* benchmark suite must report both baselines:
  * **Baseline A**: `Encoding.UTF8.GetString(input)` -> built-in `Regex` -> UTF-8 re-encoding where relevant
  * **Baseline B**: built-in `Regex` on a predecoded `string`
* benchmark catalogs identify equal-result lanes, exclusions and owned routes; historical fallback or UTF-16-parity rows do not qualify scalar execution

## 7. Acceptance criteria

The implementation is done when all of the following are true:

* Supported features obey the declared Unicode-scalar contract over strict UTF-8, with direct coverage across all public operations.
* String-returning APIs materialize the same scalar results as byte APIs.
* Every successful core match and capture has a correct scalar-aligned byte range. There is no surrogate-half execution mode.
* `RegexOptions.NonBacktracking` uses scalar matching with ordered alternatives,
  greedy/lazy choice and the regular grammar's documented restrictions. Each
  public group exposes its last completed capture, including an empty capture;
  nested/shared declarations retain their own openings. Runtime capture defects
  and timeout rounding do not define this contract.
  Zero-width repetition preserves its predicate and final capture without work
  proportional to the numeric bound. Nullable loop progress is part of the owned
  graph; empty completions exit without epsilon cycles. Parsed constructs outside
  this mode's grammar fail before planning, with no BCL validation engine.
* Supported construction/execution is owned, common construction/invocation/result/retained bounds work, and synchronous cancellation interrupts ongoing work. Obsolete parity machinery and production BCL engine dependence are removed.
* Hot native paths are allocation-light, and the benchmark suite meets qualified per-scenario thresholds against semantically comparable baselines.

[1]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex?view=net-10.0
[2]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions
[3]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-options
[4]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.matchtimeout?view=net-10.0
[5]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/substitutions-in-regular-expressions
[6]: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.RegularExpressions/src/System/Text/RegularExpressions/RegexParser.cs
[7]: https://learn.microsoft.com/en-us/dotnet/api/system.text.unicode.utf8.toutf16?view=net-10.0
[8]: https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding
[9]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions?view=net-10.0
[10]: https://learn.microsoft.com/en-us/dotnet/api/system.buffers.searchvalues-1?view=net-10.0
