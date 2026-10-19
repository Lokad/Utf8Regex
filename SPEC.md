# Lokad.Utf8Regex specification

`Lokad.Utf8Regex` is a `net10.0` library whose primary I/O surface is **UTF-8 `ReadOnlySpan<byte>` / `Span<byte>`**. Its matching unit is **one Unicode scalar**, and byte offsets and lengths are the primary result coordinates. The framework target and .NET-shaped API do not impose UTF-16 code-unit matching.

The backtracking workspace acquires balancing-head storage only for programs
whose existing global balancing flag requires it, including assertion bodies.
Other programs omit that array and its initialization/acquisition work. Required
capture/repeat/trail storage and all visited-instruction charges remain. This
lower actual storage/preparation intentionally admits tighter numeric working
or work limits; balancing rollback, retained captures and original invocation
cancellation/deadlines remain authoritative. This development change is outside
the immutable Core/Python91 and Core/PCRE92 packages.

Library cache admission uses conservative per-program charges. The complete
constructor records its successful covered capacity after scratch release,
complete facade metadata, independently charged pattern text and the replacement
child's enforced maximum. One 16 KiB constructor charge covers borrowed immutable
empty metadata, group-zero text and four fixed-size frontend arrays; dynamic major
storage keeps its existing admission. Dead helper charges and shared storage can
be counted again, causing earlier eviction. Production publication does not walk
object graphs, deduplicate identities or retain manifests/reference counts.
Development after scalar.108 additionally admits one conservative 2 KiB charge
before constructing a selected nonliteral compiled-runtime object. Its major
arrays, programs, searches and emitted payloads retain independent admission.
Four mirrored field-transfer records and their nested factory scopes are removed;
the existing outer factory owns rollback, depth and final interruption checks.
This can increase modeled byte requirements and reduce covered construction depth.
Numeric thresholds are versioned; the charge is not a physical object-size claim.
Literal runtime preparation retains its existing admission. Actual SizeOf-based
major-capacity calculations, instruction/state bounds and original interruption
authority remain.
Development after scalar.110 also allows finite ASCII Count to reuse the existing
controlled complete-branch finder when scalar dispatch proves it eligible. It
keeps bounded candidate searches, token comparisons, depth admission and terminal
original-token/deadline checks while omitting unused capture/VM scratch. Preferred
fixed-width SIMD remains first; non-ASCII, RTL, folded, context-dependent and
NonBacktracking programs retain their established paths. Lower work/depth/working
requirements are intentional route-dependent accounting, not exact heap bounds.
This source change does not alter immutable scalar.110 or retained-result contracts.
Development after scalar.109 carries complete-input validation into scalar
value-Match projection. ASCII results use their already-proved byte coordinates;
a whole-subject result reuses its known UTF-16 length. Partial Unicode results
keep their existing bounded coordinate scans. Strict whole-input validation and
terminal projection checks remain; redundant scans disappear, so numerical work
requirements can fall. Matching, captures, major admission and original authority
are unchanged. Diagnostic sentinels do not authorize these shortcuts without
the complete-input validation length proof.
Literal-family capture maps are retained only for their existing detailed LTR
multi-group literal-family projection route. Other constructors omit unused map
preparation; diagnostic summaries construct equivalent temporary views on demand.
Lower actual preparation can admit tighter construction policies while covered
cache/construction bounds remain conservative.
Grouped-literal and alternate-branch projection factories also use the existing
active-capture predicate before preparation. Groupless programs retain the same
null tables and execution routes while omitting rejected sessions and tree walks.
Captured programs keep their existing preparation and admission.
Generation/pending coordination, failed preparation rollback and externally held
program/result validity remain required. Covered delegate managed copies are
included; opaque CLR method/resolver/JIT storage, caller retention and process RSS
remain outside this declared cache bound. Public/default/common resource policy
qualification remains separate from this internal accounting contract.

This target contract was revised by explicit user direction on 2026-10-06. Former unit-subject and production BCL execution paths are superseded. Production BCL matcher/result removal is qualified through shipped dependency audits; remaining scalar coverage, former parity cleanup and resource gates are recorded separately in PLAN.md. The specification does not by itself qualify current implementation behavior.

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

Adjacent high/low surrogate spellings, raw or escaped, form one scalar within a literal run or one character-class member. Classes order range endpoints by numeric scalar value, including bounds above U+FFFF; descending supplementary bounds throw `ArgumentException`. An unpaired surrogate atom denotes no scalar and cannot match strict UTF-8. It can be repeated zero times, and negating a singleton class containing it admits every valid scalar. Groups, assertions and separate classes cannot join two such atoms into a scalar. Numeric intervals may span the surrogate gap, but matching considers only valid scalars. An explicit `[\u0000-\uffff]` includes BMP scalars; singleline dot includes every scalar plane.

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
Development pins supplementary simple-case mappings to Unicode 16.0, matching the
owned model rather than the host runtime's casing version. All 282 pairs are
one-to-one within the supplementary planes, stored in 11 fixed read-only runs;
literal/class construction and scalar/reference comparison use the same lookup.
BMP/I-cluster rules and original invocation/construction controls remain. This
correction is outside immutable scalar.62, whose Garay case pairs still miss.
The source attribution and Unicode license accompany the derived data; no Python
profile-version change or broader package qualification is implied.
The corrected lookup is included in qualified Core/PythonRe scalar.63. This pins
simple-case data, not every Core property table: general categories/shorthands
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

Development after scalar.72 starts Core template replacement's absolute deadline
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
an acceptance gate, distinct from the qualified development slices below.

Development source after scalar.77 bounds replacement-literal byte counting,
encoding and validity scans by 2048 UTF-16 code units, finishing a valid surrogate
pair at the boundary (at most 2049 units). These are template-string operations,
not subject matching. Zero-work checkpoints use the existing construction owner
and its inherited cold-replacement token/deadline. Successful numeric work and
storage charges, bulk BCL encoding, one exact admitted byte array, valid-template
text reuse and per-fragment repair remain unchanged. Earlier interruption is
intentional; this does not qualify all preparation or add a public byte/RSS cap.

Development scalar-class execution keeps linear range and independent-table
scans bounded under the original invocation. The first 32 entries of each range
or table scan fit within its atom/segment cost; each further block spends a work step
and checks the deadline directly. Folding uses the same authority and a denied
scan cannot become a successful negated predicate. Ordinary, NonBacktracking and
terminal-repeat execution share the existing class helper. Subtraction segments
store the existing six shorthand bits instead of retaining/scanning duplicate
shorthand arrays. Longer predicates may now exhaust a work cap that previously
omitted their inner scan; storage and scalar semantics are preserved. This source
correction is qualified in the immutable Core/PythonRe scalar.74 pair
([scope](artifacts/core-python-scalar74-20261017/HANDOFF.md)). It does not alter
scalar.73 or establish full construction,
retained-storage, safe-default or process-memory guarantees.

### 3.5 UTF-8 validity

The public UTF-8 APIs accept **well-formed UTF-8 only**. Invalid UTF-8 must not be silently repaired or replacement-decoded on the main API. The low-level .NET UTF-8 transcoder exposes invalid-data signaling when replacement is disabled; `Lokad.Utf8Regex` should follow that spirit and reject malformed input. Public failure mode: throw `ArgumentException` with a precise message and the offending byte offset when the input is ill-formed. ([Microsoft Learn][7])

Development fuses a complete capture-free LTR greedy scalar-class repeat when
its exact five-instruction root ends immediately in Accept and its minimum is
positive. It uses the same Rune reader/class semantics and original invocation,
with a base charge per attempted scalar and terminal authority check. No rewind
or repeat journal entries are needed; fewer actual frames/work change numeric
cutoffs intentionally. Existing workspace ownership, whole validation, class
program metering, publication/history and result admission remain. Captures,
lazy/nullable/RTL/tails and assertion segments retain their general paths; nested
RequiredEnd/resume choices are not discarded. No new instruction or public mode.
Independent CategorySet membership fanout is bounded at one for this route.
The same complete predicate also omits unused repeat count/position vectors
through the existing workspace owner. A bare simple class Count can therefore
admit working/result/depth1; general/captured/class-program scratch and required
result capacity retain admission. Actual acquisition work/storage requirements
fall; no replacement charge, new state flag or retention policy is introduced.
This source improvement does not alter immutable scalar.68.

Development Count also reuses this exact complete-root guard for one scalar
pass. It groups greedy runs by the finite/unbounded maximum, counts remaining
runs only when they meet the positive minimum, and avoids repeated match-attempt
setup and rescanning too-short suffixes. The same controlled class helper retains
inner predicate metering. One invocation/deadline/token spans the byte floor and
scan, with final interruption before publication. Existing parent working loans
are noted without acquiring local scratch. Less actual work lowers numeric
cutoffs; captures, nullable/lazy/RTL/NonBacktracking/anchors/tails keep their
existing paths. No public API, matching unit or ownership contract changes.
The immutable Core/PythonRe scalar.75 pair qualifies this optimization against
detached consumers and selective current costs
([scope](artifacts/core-python-scalar75-20261017/HANDOFF.md)); scalar.74 stays unchanged.

### 3.6 Scalar boundaries and coordinates

Qualified scalar.80 filters Python scalar search starts only from a
mandatory positive, nonfolding ASCII Set or the existing literal hint. At most
three owned instructions and the fixed128-bit mask are inspected, including one
optional capture start and a positive-minimum greedy repeat. The first/last set
bits define a necessary interval; holes remain VM-verified false candidates.
Checked span searches cover at most2048 bytes per original invocation checkpoint.
Whole strict validation precedes discovery, and an ASCII hit is a scalar boundary
even across chunk tails. Anchored operations retain their existing handling.
Failed attempts/capture resets/workspace acquisition can be omitted, lowering
numeric work/depth/working requirements without restarting the deadline/token or
removing basic metering. Captures, retained aliases and result admission remain.
No public API, owner, mode, program metadata or opaque profile-class proof is added.
These changes are included in immutable80; source cost evidence retains its own
protocol and dense-input tradeoff, separately from exact package operating costs.
Scoped qualification does not prove all target behavior or host containment.

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

These are target requirements. Current in-flight coverage is partial and must be
proved route by route; neither entry-only checks nor passing semantic comparisons
qualify it. Production BCL matcher/result dependencies are removed in the
qualified development source and must not be reintroduced to service a gap.

Qualified development slices retain their invocation token/deadline
through qualified validation, search, projection and lazy advances. Metered
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
This additive development API leaves existing value/span contracts intact and
does not itself bound externally retained results. Work and result-capacity traffic
are cumulative through validation, discovery and required snapshot shaping; working
release never refunds the capacity charges described in Section 3.7.

The target requires common construction, invocation, working-storage, result and
retained-storage policies across the core, PCRE2 and PythonRe profiles. Admit
library-owned capacity before acquisition, charge overlapping live buffers together,
and keep spent work cumulative across candidates and suboperations. A resource
breach is distinct from cancellation, timeout and no-match. Freeze compatible
public controls, ownership/lifetime rules, checkpoint precedence and qualified
safe defaults through the R01/R06 gates in PLAN.md; partial internal admission
must not be advertised as complete public bounds or a process-RSS guarantee.

Current public support is narrower: core and PythonRe accept finite
`Utf8ResourcePolicy.MaxWorkSteps`, `MaxDepth`, `MaxWorkingBytes` and
`MaxResultBytes`. PCRE2 maps the same policy to its separately documented
managed execution limits, including result bytes; see `SPEC-PCRE2.md`.
A finite core/PythonRe policy selects owned metered
execution with one cumulative invocation through full strict UTF-8 validation,
discovery, required coordinate/capture projection, output and publication. Lazy
advances share that allowance, including copies of core value cursors; copies keep
independent positions and retained aliases/results remain valid after work denial.
Development after scalar.101 also lets controlled Core IsMatch/Count reject an
absent planner-proved exact required literal before acquiring VM state. The
existing bounded2048byte search borrows the complete validated input and original
invocation. The pilot serves the owning unanchored LTR program without existing
prefix guidance; presence retains normal verification. Numeric work and required
state can decrease, so smaller policies may admit the same false/zero outcome.
Whole-input UTF8 validity, original cancellation/deadlines and basic metering stay.
Fixed accounting metadata keeps its existing separate scope. Unmetered/native/
anchored/RTL/prefix-guided routes retain their selection; immutable101 is unchanged.
Core Count may reuse the existing bounded native counter for a prepared nonempty
exact LTR literal, including ordinary ASCII literal captures. It shares the
validated invocation and requires no capture/continuation scratch, depth or result
capacity. Route-dependent admission can therefore succeed below the former VM
scratch requirement. Boundary/trailing assertions, contiguous anchors,
shared/explicit numbering and balancing retain their qualified routes; captured
Unicode and NonBacktracking patterns without a native plan also retain the VM.
Offset interoperability maps keep their separate working/result admission.
Development after scalar.108 removes the duplicate post-candidate charge from
finite forward exact-literal Count. A bounded search/comparison admission also
covers its constant-time count increment; the terminal checkpoint still precedes
publication. Basic search metering, whole-input validation and the original
cancellation/deadline authority remain. Numerical work requirements can decrease
on this shared Core/PythonRe/PCRE2 path without changing storage/depth/result scope.
Development after scalar.109 also rechecks the original token after this terminal
work checkpoint, before publishing the count. Cancellation arriving at that
checkpoint cannot escape as a successful integer result. Numeric work, storage
admission, counting algorithms and deadline behavior are unchanged.
Scalar.87 also reuses the existing scalar word-delimited email Count family
before general VM dispatch. Development source after scalar.87 extends this to
the prepared URI Count family. Eligibility requires the original
capture-free, nonfolded LTR program, excluding ECMAScript and NonBacktracking.
Whole-subject strict validation shares the invocation; delimiter searches have
bounded windows and scheme/head/body/query/fragment scalar steps keep work, token
and deadline checks. URI host and continuation each require a complete scalar;
Unicode whitespace ends the token, and empty query/fragment tails remain valid.
The counters use the same scalar word predicate as the VM and need no capture,
continuation or result storage. Tight depth/working/result caps can therefore
admit these calls below the former VM scratch requirement; numerical work needs
can fall. Other programs and operations retain their qualified routes. This does
not extend construction, retained-storage or default guarantees.
Development source after scalar.88 declines structural-family lowering when a
separator overlaps the following identifier-start class or consuming suffix
literal/class, including multipart suffixes. The proof reuses the existing
constant-time ASCII class-disjointness predicate; identifier-tail rewind keeps
its existing implementation. The non-rewinding separator cannot prove these shapes;
existing owned matching must preserve its backtracking. Immutable scalar.88
miscounts the pattern \b(?:foo|bar)\s* followed by a literal space over
"foo   bar   " as six unlimited or zero finite instead of two. Correct execution
can require different state/work admission than the former incorrect result;
selected limits remain authoritative.
Terminal separator progression and trailing-boundary rewind also require the
complete owned plan. Scalar.88 can truncate an unlimited greedy Match at the
prefix or miss a finite Count before a trailing boundary. These declines affect
optimization selection, not grammar admission, scalar units or caller coordinates.
An atomic identifier-tail group must also retain its commitment. The structural
verifier can rewind ordinary tails, so preparation must not strip an atomic
wrapper from such a loop. These shapes use the existing owned path, preserving
atomic failure and the original resource controls.
Development source after scalar.89 preserves Core's explicit finite maximum
`2147483647` separately from infinity through grammar fixups and scalar lowering.
Sentinel-based direct families conservatively decline that finite edge. A
NonBacktracking consuming repeat must satisfy the existing finite expansion
admission; it can fail construction capacity instead of becoming an unbounded
loop. Existing compact zero-width repeats do not require that expansion. This
does not widen the Core grammar's accepted numeric range or alter immutable89.
The bound repair and Count state omission below are included in qualified90;
its exact-package default Count timings remain unqualified, separately from the
closed source FullOpts cost evidence.
Development source after scalar.89 also omits unobserved capture values for Core
Count on an original backtracking program whose existing tree features prove
no references, conditionals or lookarounds. Balancing, shared/explicit capture
declarations and unknown/alternate programs retain their state. The same VM
still charges every capture instruction, while omitted slots/trail writes lower
actual storage and acquisition work. Numerical working/work requirements can
fall. Captures used by matching, other operations/profiles, scalar progression,
retained results and the original invocation authority remain unchanged. This
is execution over the same program, with no new engine or cached program variant.
Development source after scalar.87 also lets finite Core Count borrow the
existing invocation-aware ASCII structural-family search and verifier. The
original zero-offset LTR program must be nonempty, nonfolded and free of stateful
capture numbering, balancing, lookarounds and conditional tests. Full validation
must prove ASCII, and overlapping prefixes retain scalar verification. Search,
verification and terminal checks share the original invocation, without VM
capture/continuation storage. This changes route-dependent work/storage admission;
it does not add returned capture values or extend the other operations.
Development source also reuses this existing metered driver for prepared exact
LTR families with two or three nonempty branches and no stateful/anchor/folding
exclusions. Count publishes no capture values, so ordinary distinct captures need
no slots; the driver retains ordered non-overlapping matching and full-subject
validation. Removing VM verification/workspace lowers actual work/depth/working
requirements and can admit calls below the previous scratch cap. It does not
remove per-window/predicate metering or the terminal interruption check. Larger,
empty and excluded families retain their existing routes. This is included in
the private scalar.49 pair; scalar.48 predates it. It is not a CPU-time equivalence
claim.
Subsequent development source extends the existing metered counter to root,
zero-offset, invariant, nonempty LTR ASCII-folded literals/families without
captures or other stateful/search constraints. Full strict validation, bounded
Kelvin screening and search borrow the same invocation, including finite controls,
caller token and original absolute deadline. Eligible forward folded family
candidate discovery borrows the prepared first-byte union for one bounded search
instead of separate branch scans; original ordinal verification and confirmation
remain. Subsequent source extends that same bounded reuse to prepared exact LTR
families. Each candidate window still charges the original invocation before
searching at most 2048 bytes; full comparison/confirmation and terminal checks
remain. Removed branch scans can lower required work, without new scratch,
owners or an unmetered route. This exact-family extension is outside scalar.61.
Reverse, candidate-only and unprepared discovery retain their existing paths. Kelvin screening charges before
windows of at most2050 bytes (2048 starts plus two lookahead bytes). Relevant
Kelvin retains spent work and uses scalar execution; other construction cultures,
RTL, empty/captured/anchored/assertion-dependent and NonBacktracking routes retain
their existing handling. Native success requires no VM depth/working/result
scratch and may admit tighter limits. This source change is outside scalar.60.
Controlled Count for one exact forward literal without boundary/trailing
conditions invokes the same bounded candidate/comparison kernels directly. It
omits generic match construction and unconditional boundary confirmation,
reducing charged work while retaining per-block/comparison metering, diagnostic
candidates and a terminal interruption check before publication. Other shapes
keep the existing counter. This is a route-specific work change, not a CPU-time
equivalence guarantee.

Qualified scalar.81 uses the existing prepared searcher for
exact needles up to 16 bytes. The complete search span, including needle
lookahead, is at most 2048 bytes per original charged checkpoint. This bounded
search includes verification in its window charge, instead of separately visiting
and charging every first-byte candidate. Longer and folded needles retain their
separate bounded comparisons. Numeric work requirements can decrease; spent
work, sticky denial, original token/deadline and terminal publication checks remain.
No new scratch, owner, policy switch or unchecked access is introduced. This
improvement is qualified in scalar.81; earlier scalar.80 evidence stays immutable.

Qualified scalar.81 also reuses that conservative native eligibility for
finite value Match: nonempty exact forward literals and eligible two/three-literal
families, keeping the existing stateful/boundary/trailing exclusions. Discovery
and derived-coordinate projection borrow the original invocation after complete
strict validation. Ordered priority, scalar-aligned ranges, token/deadline,
sticky denial and terminal checks remain. Unused VM attempts/slots can disappear,
lowering numerical work/storage requirements without refunding spent work.
Detailed captures keep their own VM; detailed results and offset maps retain
working/result admission. This adds no public API, owner or policy switch and
does not alter earlier bytes or prove full SPEC/host coverage.

Finite IsMatch uses the same eligibility and original validated invocation for
native discovery, returning only the boolean with no coordinate projection or
unused VM slots. Original work/token/deadline and terminal checks remain. Removed
real VM work/storage can lower numerical requirements; offset-map admission and
excluded semantic shapes retain their existing paths. No new API or policy mode.

Development after scalar.81 avoids unused UTF16 metadata for owned boolean calls.
Validation-only and metadata-producing entry points share the same charged,
complete-scalar block loop and exact error-offset rebasing. Existing checked
ASCII and validity-only strategies serve the boolean path; metadata-producing
callers retain counts/flags. Validation-only flags are never used for projection
or maps. Original token/deadline, sticky work denial, terminal checks and major
admission remain; valid-input work cutoffs are unchanged. No new owner/public
mode or unchecked access. This source change is outside immutable scalar.81.

Family verification rejects exact branches with a different first byte under
their existing branch charge, and invokes confirmation only when a boundary or
trailing predicate exists. Full comparisons remain bounded and charged, including
same-first-byte false candidates; accepted and terminal interruption checks remain.
Removed operations can lower required work. Ordinal priority, reverse end priority,
original-subject predicates and scalar coordinates are unchanged.
Scalar.4 filters LTR scalar-VM searches only when their initial
instruction is a mandatory beginning anchor. Absolute origin or LF line starts
are candidates; checked scans cover at most 2048 bytes per invocation checkpoint.
Match verification and full validation remain unchanged. Fewer impossible attempts
may lower the selected route's work requirement; this is not a fixed scalar/byte
charge or a change to finite-limit failure semantics.
Scalar.50-dev also rejects impossible anchored starts when the next instruction
is an exact unfolded ASCII literal and the program cannot match empty. The
existing line-start helper performs this necessary byte test; the scalar VM
verifies every survivor. Empty, folded, Unicode-leading, RTL and NonBacktracking
paths retain their existing behavior. The test spends an input-probe step under
the original invocation authority. Avoided VM attempts omit their capture resets,
instruction work and workspace acquisition, so required work/working capacity
may decrease; all-hit inputs can spend extra probe work. Full strict validation,
sticky denial, original cancellation/deadlines, retained results and parent loans
remain unchanged.
Denial throws `Utf8ResourceLimitExceededException` with `ExecutionWorkSteps`, the
configured limit and a saturating requested count. A cap is in bounded engine/
checkpoint steps, whose granularity may change, not bytes, wall time or RSS.
Successful early hits still require full-input validation; a work denial can
precede discovery of a malformed tail. Cancellation and absolute timeout remain
authoritative at existing checkpoints. Pattern/replacement preparation has separate
construction admission; arbitrary caller callback execution and later standalone
result getters are outside this execution cap. Required callback output shaping is
charged. Different operations have independent allowances; a policy is not a shared
application-wide meter. Core static cached operations have no policy parameter and
retain unlimited defaults. Finite policy routes can have different performance
from qualified unlimited native shortcuts.

Development source after scalar.95 also uses finite `MaxWorkingBytes` as a
separate ceiling on additional covered Core replacement-parser capacity. UTF-8
template-key decoding and nested parsing share that ceiling before acquisition;
decoded keys still need admission on warm calls. Text-key cache hits add no
preparation scope. `ConstructionBytes` may precede execution denial and its totals
include the enclosing construction reservation. Failure restores the parent limit
and capacity, removes failed cache publication and never refunds admitted work.
Original cancellation/deadlines, fixed construction backstops and retained values
remain; invocation work/result counters and cache/host retention keep separate
scope. This does not establish common cumulative or retained/RSS metering.

The immutable Core/PythonRe scalar.96 pair includes this Core preparation change
and the removal of obsolete internal offset adapters. Same-byte Windows/Linux
package consumers, public denial/retry/token witnesses and producer CI pass;
qualified operating costs and inconclusive lanes are recorded in
artifacts/core-python-scalar96-20261018/HANDOFF.md. Public APIs, scalar/capture
semantics, live projection maps and original resource/ownership scopes remain.

The preliminary byte-template validator admits its byte-count work before reading
the template. An active construction owner supplies cumulative admission and
bounded parent interruption checkpoints. Without such an owner, the scan enforces
the existing fixed construction-work ceiling directly, without another owner
allocation. Its phase admission is separate from later preparation and invocation
counts. An oversized malformed template can therefore report
`ConstructionWorkSteps` before an invalid-tail error. Funded strict validation,
absolute byte-error offsets and direct native replacement eligibility remain.

Development after scalar.85 reuses complete Core literal discovery for exact,
capture-free Python FullMatch value/string results. The one invocation covers
full strict validation, suffix start/end checks, derived coordinates and required
output admission. These calls avoid per-scalar VM dispatch and lazy preparation;
general/detailed/capture-dependent routes and explicit eager Compile remain.
Actual work requirements may fall; controls, ownership and defaults do not change.

Development source after scalar.81 stages Python string Split output directly
in the existing metered string-reference buffer, without detailed-item metadata
that the operation discards. Detailed Split retains its metadata route. Both
share scalar discovery, full validation, original token/deadline and result/
working admission. Reduced actual staging may lower numerical work/byte needs;
no spent-work refund or caller-retention guarantee is introduced.

Source after scalar.82 allows capture-free nonempty Python cursor search to
borrow an existing Core complete literal/family matcher under the original
validated invocation. Complete-result eligibility is separate from fallback-start
guidance. It omits redundant VM attempts/storage, without resetting controls or
changing group-zero/scalar ranges. Current-position Match and derived FullMatch
retain their position/end constraints; stateful/empty/excluded patterns retain
their existing routes. Numerical work/working needs can decrease with removed
work/storage; output and retained-result contracts remain unchanged.

PythonRe scalar.5 reuses one existing exclusive VM workspace for each
scalar cursor. It stays charged while positioned, including host waits and result
projection; EOF, failure and disposal release it synchronously. Private repeat,
heads and stack storage is reset between attempts and never exposed by owned
snapshots. Scratch/publication overlap may require a higher working cap than the
former per-move release; denial remains structured and cumulative result traffic
is never refunded. Working capacity is an upper bound, not a stable cross-version
minimum. No GC-based credit or new pool is involved. The larger persistent owner
adds fixed allocation overhead; measured bulk savings do not prove every path is
faster. The immutable scalar.4 package predates this change.

Core and PythonRe also accept `MaxResultBytes`, alone or with work. It caps cumulative
modeled major result/staging capacity requested during the operation: rounded array/
string extents, core output controls, output growth/RTL piece tables, capture history/
projection and Python capture/name arrays, collection staging/final roots and
coordinate maps. Charges precede covered acquisition, include temporary/final
capacity and growth, and remain spent after release or later failure. Shared,
reused or empty storage can be charged conservatively again. This intentionally
denies some operations earlier than a final payload-size check. No result identities,
reference counts or GC refunds are required. Coordinate conversion can need a map
even for a fixed result, so derived-offset operations can consume result capacity.
Fixed range/boolean/count values, borrowed subjects/program metadata, arbitrary
caller callback allocations and later standalone getters are excluded. Required
callback context/output shaping is included. Incidental object overhead is outside
this model except the existing fixed output-control charges. It is neither exact GC
allocation traffic nor a bound on matched text length, live caller retention or RSS.
Denial reports `ExecutionResultBytes`; requested totals saturate and the first
work/result/depth denial remains sticky across invocation/cursor copies. Previously
returned results and permitted aliases stay valid. Cancellation/deadline checks
precede covered result admission. Destination shortage remains its documented
status; a later interruption during copying can leave a partial destination.

Core and PythonRe also accept `MaxDepth`, alone or with work/result caps. It bounds
live backtracking/continuation frames, including assertion state, or pending
alternatives in the linear closure runner. Admission precedes acquiring a new frame
or copying/publishing a pending alternative. Denial reports `ExecutionDepth`, the
configured cap and requested peak. Depth measures actual nesting on the selected
route: a linear viability-table replay has no pending stack and uses no depth
allowance. Different operations/routes may therefore need different caps. This is
not pattern/construction depth, cumulative work, workspace/table width or a memory
bound. Separate cursor advances keep the original invocation authority and first
denial, while completed frame depth may fall between matches. Retained results and
permitted aliases stay valid. Finite depth selects owned execution and still
requires full-input strict UTF-8 validation; cancellation and the original deadline
remain authoritative at existing checkpoints.

Development source after scalar.77 also removes useless fallback frames for a
forward greedy single-scalar class repeat followed by an exact literal outside
that class, optionally separated by one capture closing. Giving back a consumed
scalar cannot make that literal succeed. Capture closing and the following
instructions still execute normally. For example, `([a-z]+)-` needs no repeat
fallback frames, while `([a-z]+)a` retains them. Actual removed state/work can lower
the required depth, working capacity and work cutoffs; remaining admission,
predicate metering and original interruption remain. This is not a new policy
mode or a change to greedy/capture semantics. Scalar.77 predates this change.

Development source after scalar.78 extends the same scanner to a terminal
greedy scalar-class repeat, optionally followed by one capture closing, then
Accept, only when the cursor has no RequiredEnd. Assertion callers with an
explicit end can still reject Accept and need shorter alternatives; they retain
the VM path. Python FullMatch inserts an end anchor and retains its handling.
Nonempty retries, capture closing/history and upstream restoration stay ordinary.
Actual removed frames/journal writes/dispatch lower numerical requirements; work
metering, original interruption and remaining storage/result admission remain.

Core and PythonRe accept `MaxWorkingBytes`, alone or with other supported caps.
It bounds conservative modeled active major array/text and output-control capacity:
slot/repeat/frame state, captures/projection, coordinate maps, linear tables,
temporary discovery/result buffers and output staging. Covered acquisitions admit
headers/rounding and old/new growth overlap before allocation. Exclusive scratch
releases synchronously; published copied-cursor maps/tables remain charged for the
shared invocation. Logical reservations can exceed physically live payloads and
deny earlier. Required result publication can release working capacity; cumulative
result-capacity traffic is never refunded. Owned Python cursors release their
exclusive discovery/shaping state on EOF, failure or Dispose.

Denial reports `ExecutionWorkingBytes`, the configured maximum and saturating
requested aggregate capacity. Denied reservations acquire nothing and leave
existing bytes unchanged. First work/depth/working/result denial is sticky. Work
and the original token/deadline remain shared across phases and cursor copies.
Successful matching still validates the complete strict UTF-8 subject. Array/CLR
representability failures are separate capacity failures, not manufactured
configured-limit breaches. Borrowed subjects/programs, construction/replacement
preparation, incidental owner/deadline/object overhead, arbitrary callback
allocations, returned caller retention and later standalone getters have separate
scope. Covered output controls retain their fixed charges. This is neither exact
GC traffic/heap usage nor a caller live-retention or process-RSS limit.

`MatchTimeout` defaults to
`Regex.InfiniteMatchTimeout`. PCRE2 also accepts finite values for all four
invocation budget kinds within its profile ranges through explicit factories,
mapping result capacity to the managed
`ResultLimitInBytes` extension. Its metering units, `ResultLimit` failure and
publication/staging exclusions are defined in `SPEC-PCRE2.md`; they do not imply
shared host ownership or a live-retention/RSS bound. These remain staged controls,
not completion of common construction, ownership or safe-default gates.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8Regex("cat|horse");
Assert.Throws<OperationCanceledException>(() => regex.IsMatch("cat horse"u8, canceled.Token));
```

## 4. Public API

The public API is split into:

* the baseline .NET-mirroring surface
* intentional `Lokad.Utf8Regex` extensions

## 4.1 Baseline .NET-mirroring surface

Use this public surface.

```csharp
namespace Lokad.Utf8Regex;

using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

public sealed class Utf8Regex
{
    public Utf8Regex(string pattern);
    public Utf8Regex(string pattern, RegexOptions options);
    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static TimeSpan DefaultMatchTimeout { get; set; }

    public string Pattern { get; }
    public RegexOptions Options { get; }
    public TimeSpan MatchTimeout { get; }

    public bool IsMatch(ReadOnlySpan<byte> input);
    public int Count(ReadOnlySpan<byte> input);
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input);
    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input);
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input);
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input);
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count);

    public byte[] Replace(ReadOnlySpan<byte> input, string replacement);
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8);
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator);

    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement);
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator);

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten);

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten);

    public int GroupNumberFromName(string name);
    public string GroupNameFromNumber(int i);
    public string[] GetGroupNames();
    public int[] GetGroupNumbers();

    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern);
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static int Count(ReadOnlySpan<byte> input, string pattern);
    public static int Count(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static int Count(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, string pattern);
    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, string pattern);
    public static Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, string pattern);
    public static Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, string pattern, RegexOptions options);
    public static Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, string pattern, RegexOptions options, TimeSpan matchTimeout);

    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern);
    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern, int count);
    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern, int count, RegexOptions options);
    public static Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, string pattern, int count, RegexOptions options, TimeSpan matchTimeout);

    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement);
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, RegexOptions options);
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, RegexOptions options, TimeSpan matchTimeout);
}

public readonly struct Utf8ValueMatch
{
    public bool Success { get; }
    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }
    public int LengthInBytes { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }
}

public readonly ref struct Utf8MatchContext
{
    public bool Success { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }

    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned

    public int GroupCount { get; }
    public Utf8GroupContext GetGroup(int number);
    public Utf8GroupContext GetGroup(string name);
    public bool TryGetGroup(int number, out Utf8GroupContext group);
    public bool TryGetGroup(string name, out Utf8GroupContext group);

    public string GetValueString();
}

public readonly ref struct Utf8GroupContext
{
    public bool Success { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }
    public int CaptureCount { get; }

    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned
    public ReadOnlySpan<byte> GetValueBytes();   // throws if not byte-aligned
    public string GetValueString();

    public Utf8CaptureContext GetCapture(int index);
}

public readonly ref struct Utf8CaptureContext
{
    public bool Success { get; }
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }

    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned

    public ReadOnlySpan<byte> GetValueBytes();   // throws if not byte-aligned
    public string GetValueString();
}

public readonly ref struct Utf8ValueSplit
{
    public bool IsByteAligned { get; }
    public int IndexInBytes { get; }      // throws if not byte-aligned
    public int LengthInBytes { get; }     // throws if not byte-aligned
    public int IndexInUtf16 { get; }
    public int LengthInUtf16 { get; }

    public ReadOnlySpan<byte> GetValueBytes();   // throws if not byte-aligned
    public string GetValueString();
}

public delegate void Utf8MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref Utf8ReplacementWriter writer,
    ref TState state);

public delegate string Utf16MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref TState state);

public ref struct Utf8ReplacementWriter
{
    public void Append(ReadOnlySpan<byte> utf8);
    public void Append(ReadOnlySpan<char> utf16);
    public void AppendAsciiByte(byte value);
    public void Append(Rune value);
}
```

Baseline API notes:

* This surface mirrors span-based `.NET Regex` APIs where possible, but with `ReadOnlySpan<byte>` as the primary input. Materialized `Matches` / `Split` collections are intentionally deferred. `TryReplace` uses `OperationStatus`. ([Microsoft Learn][1])
* `RegexOptions.Compiled` selects an execution strategy with the same scalar semantics; it must not change the accepted language or results.
* Overloads without an explicit timeout use `Utf8Regex.DefaultMatchTimeout`, whose initial value must be `Regex.InfiniteMatchTimeout`.
* `Utf8ValueMatch` is the cheap, storable coordinate result. Rich group/capture inspection is provided by `MatchDetailed(...)` and by evaluator contexts.
* Successful core matches and captures on valid input are byte-aligned at scalar boundaries. Byte accessors expose their ranges; UTF-16 properties are derived coordinates. An unsuccessful group still follows its documented unsuccessful-access contract.
* `Utf8GroupContext.CaptureCount` and `GetCapture(int index)` must match .NET capture history ordering.
* `Utf8ValueSplitEnumerator` preserves the span API's count/empty-element/RTL ordering conventions, using scalar matches. Captures are not emitted as separate split elements. Compare with `.NET Regex.EnumerateSplits` only when delimiter matching semantics overlap.
* `replacementPatternUtf8` overloads interpret bytes as UTF-8 encoded .NET replacement patterns, not as literal bytes.
* All `ref struct` match/group/capture/split views are ephemeral over the original input span and cannot outlive the calling frame or input buffer.
* `TryReplace` returns `Done` or `DestinationTooSmall` only for destination sizing. On `DestinationTooSmall`, `bytesWritten == 0` and callers must treat `destination` as unchanged. Invalid UTF-8, invalid replacement text, invalid regex construction and timeout still throw. Successful scalar matches do not introduce non-representable byte results.
* `TryReplace` permits input and UTF-8 replacement spans to overlap the destination. Overlapping input uses the existing staged scalar output before final copying, preserving unread subject bytes at the cost of staging allocation. Disjoint input keeps its qualified native route; finite admission, cancellation and deadlines remain in force.
* Evaluator-based replacement is currently the allocating replacement path; allocation control is provided only for fixed replacement-pattern overloads.

## 4.2 UTF-16 offset compatibility APIs

These APIs accept explicitly named **UTF-16 coordinate offsets** for interoperability with .NET strings. The offsets must resolve to complete scalar boundaries; they do not request UTF-16-unit matching. An offset inside a supplementary scalar throws `ArgumentOutOfRangeException`, naming the offset parameter. The preferred native surface uses UTF-8 spans and byte coordinates.

Development scalar.73 retains the original invocation through anchored one-shot
offset matching after validation and coordinate preparation. Exact anchors reuse
the controlled enumeration cursor; ASCII culture-invariant folded anchors reuse
its existing folded modes. Other folds, scalar subjects and unproved shapes keep
the scalar route. Raw shortcuts require no finite timeout, live token or finite
invocation policy. A borrowed anchored cursor reuses the caller's owner/deadline
without acquiring another finite invocation. No public API or matching unit changes.
The immutable Core/PythonRe scalar.73 pair qualifies this correction against
detached consumers and selective current costs; its rejected prototype timings
remain separate. [Qualification and limitations](artifacts/core-python-scalar73-20261017/HANDOFF.md).

```csharp
public sealed class Utf8Regex
{
    public bool IsMatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public int CountFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public Utf8ValueMatch MatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public Utf8MatchContext MatchDetailedFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset);
}
```

Rules for these compatibility APIs:

* The `utf16Offset` parameter is measured in UTF-16 code units, exactly like
  `.NET Regex`.
* Resolve these offsets into scalar boundaries and execute the same scalar engine. No alternate char-unit matching mode is implied.
* Method names must keep the UTF-16 coordinate system explicit; do not expose
  these as plain `startat` overloads on otherwise byte-native methods.

## 4.3 Intentional API extensions beyond baseline .NET Regex

```csharp
public sealed class Utf8Regex
{
    public int MatchMany(
        ReadOnlySpan<byte> input,
        Span<Utf8ValueMatch> destination,
        out bool isMore);

    public Utf8RegexAnalysis Analyze();
}

public readonly struct Utf8RegexAnalysis
{
    public bool IsFullyNative { get; }
    public bool MayFallbackOnNonAsciiInput { get; }
    public bool IsExactLiteral { get; }
    public int MinRequiredLength { get; }

    public int CopyRequiredLiterals(
        Span<Utf8RequiredLiteral> destination,
        out bool isMore);
}

public readonly struct Utf8RequiredLiteral
{
    public bool IsCaseInsensitive { get; }
    public int FixedOffsetInBytes { get; }   // -1 when not fixed
    public ReadOnlyMemory<byte> Utf8Bytes { get; }
}
```

These additions are intentional:

* `MatchMany(...)` is the allocation-control multi-match API: it writes up to `destination.Length` matches, returns `written`, and sets `isMore` when additional matches exist beyond what fit.
* `Analyze()` exposes a small stable search/planning surface for higher-level engines: native eligibility, minimum required length, exact-literal detection, and required literals suitable for prechecks and indexing.

Rules for these extensions:

* `MatchMany(...)` does not imply a continuation protocol. `isMore` is only a clean termination signal for callers that intentionally cap result count.
* `CopyRequiredLiterals(...)` exposes only stable prefilter facts. It is not a public dump of the internal execution plan.
* `Utf8RequiredLiteral.FixedOffsetInBytes == -1` means the literal is required but not at a fixed byte offset.
* `Analyze()` is advisory for planning and prechecks; matching semantics are still defined only by the regex APIs themselves.

## 5. Architecture

Development preparation caches bound entry metadata and use conservative charges
per entry for their declared immutable program graph or root/bounded-descendant
scope. Keys use direct length-based charges without a graph builder or identity
map. Replacement programs in all three profiles use direct bounded length/fixed
wrapper charges, with each storage-bearing slot counted independently; no builder
or identity map is needed for those flat schemas. The main provider reads the
completed program's immutable conservative charge without graph traversal.
Key/value text, shared
slots within replacement plans and storage across entries can be charged again
and cause earlier eviction. Entries retain a
numeric charge, with no global identity registry,
reference counts or payload manifests. Production graph collectors and temporary
identity builders are retired under the qualified per-program certificate.
Development after scalar.86 preflights the existing constant-time key charge
before hashing or lookup. Keys larger than the immutable payload cap prepare
uncached under the same construction owner/admission; they cannot be retained.
Retainable-key coordination, eviction, interruption and held values remain.
This avoids impossible-key scans, not every admitted key-comparer operation.
Core static entry points reject null patterns with ArgumentNullException naming
pattern before cache-key metadata or construction work.
Independent test-owned graph/major-array
and lazy-descendant coverage remains required. Cache admission/eviction preserve
pending preparation coordination, cancellation, construction work and reentrant
clear/shrink. Eviction does not invalidate caller-held programs or results. These
internal bounds cover cache retention, not caller retention, process RSS, or a
complete public policy. Further construction-helper simplification and complete
public working/ownership policies remain under the review recorded in PLAN.md.

The architecture must keep these invariants:

* one owned semantic front-end for the declared scalar language
* one shared catalog of final capture identities, consumed unchanged by execution
  and capture shaping; no independent BCL capture-number translation
* separate lowerings for candidate search and full execution
* scalar-aware UTF-8 subject traversal and primary byte ranges
* native `RightToLeft`; never emulate it by reversing the input
* `RegexOptions.NonBacktracking` is a separate owned linear-time scalar search mode
  whose discovery guarantee covers the complete Count, enumeration, split or
  replacement invocation, including repeated empty matches. Any shared batch
  storage must be admitted before allocation and remain charged while a copied
  cursor can still use it.
* `Utf8Regex` instances are immutable and thread-safe

Implementation details may evolve, but fast paths must be derived from the same semantic front-end rather than from separate pattern parsers.

Current construction backstop: scalar-lowering arrays share an owned admission
scope across synchronous nested core/adaptor construction. The provisional
maximums are 1,048,576 live instruction slots and 64 MiB of covered construction
capacity. The byte model charges actual element strides, a conservative 32-byte
array header and payload rounded to 16 bytes; shared empty arrays cost zero.
Covered helper objects, including Python AST nodes and class items, reserve the
larger of 128 bytes and their rounded typed state extent before the factory runs.
Referenced arrays and text have independent length-based charges; shared immutable
empties are borrowed. Construction stores no object-retaining registry or linked
object checkpoints. Dead helpers can become collectible while their numeric charges
remain reserved until scope completion or rollback. Failed factories release their
attempted reservation; failed children restore numeric byte/depth capacity without
refunding admitted work or invalidating caller-held objects. This coarser model can
deny some valid large patterns earlier. It bounds covered capacity, not arbitrary
GC allocation traffic, retained caller storage or process RSS.
Core scalar classes use integer counts and full scalar range endpoints through
the exclusive bound U+110000. Payloads larger than 65,535 entries do not decline
owned matching or become syntax errors. Class data, merge/fold vectors, property
name text, descriptors and optional native preparation projections share byte/work
admission, including growth and final-copy overlap. The VM and scalar byte-width
analysis consume the integer data. Existing qualified native preparation still
uses a bounded class projection; unavailable projections decline only that native
tree, while the authoritative scalar tree continues to serve operations. Retiring
the projection and its consumers remains cleanup work. Initial class member lists,
other lexical/IR objects and complete retained/cache policies remain uncovered.
Core preparation compiles and retains the scalar program once from the owned
tree. Public matching borrows that program; native NonBacktracking eligibility
reads tree/catalog facts rather than compiling and discarding another program.
Prepared and operation-cursor objects admit conservative field-state charges
before acquisition, while referenced scalar/native
payloads keep independent loans. Their shared immutable empty cursor state is
borrowed. Failed preparation restores parent numeric byte/depth loans without
refunding work. These covered acquisitions do not establish complete retained
cache policies. Shipped BCL matcher/result retirement is qualified separately.
Owned candidate verification borrows the cached scalar program and executes on
the complete validated subject. Conservative candidate ends never truncate
lookaround/anchor context; successful starts and ends are scalar byte boundaries.
Verification results retain only byte ranges. Requested UTF-16 match coordinates
are derived separately under the same invocation, without decoding the subject.
Capture scratch reserves both complete array extents, including headers, before
acquisition and releases them on success, miss,
breach or cancellation; work, deadline, token and contiguous-anchor origin remain
with the caller across candidates. Search-guided and compiled candidate matching
no longer use BCL verification, conditional bypass or count demotion. The decoded
bounded verifier, duplicate forwarding/program/decoded-state plumbing and demotion
threshold/route are retired. The search-guided emitter passes the same invocation
and cached scalar verifier through its generated loop; it retains no decoded
subject or independent BCL verification path.
Candidate portfolios borrow the same invocation. Up to two sources keep merge
state inline; larger portfolios reserve all three physical vectors together
before acquisition from the shared bounded exact pool. Small vectors use a
declared power-of-two capacity; large vectors use the source count. Constructor
failure and disposal restore parent loans. Finite storage calls may borrow
existing leaves but cannot initialize or grow pool retention. Initialization,
source advancement and merge-selection loops check the same caller authority;
individual search kernels still need their complete work/cancellation gates.
No-capture verification uses shared empties without per-attempt allocation.
These lexical workspaces have one owner; public copied cursor/result lifetimes
remain a separate qualification. Shipped BCL matching/result consumers and
factories are retired. Complete shared public policies and R07 remain open.
Internal compiled/simple/structural fallback consumers now borrow the admitted
scalar program for mixed UTF-8 and reverse matching. Their general IsMatch/Count
paths retain byte coordinates; requested Match projection shares the invocation.
Absolute caller deadlines and direct controlled work/token/storage owners survive
the transfer. General compiled match/split cursor factories use owned scalar
execution, admitted coordinate maps and invocation-long published reservations, without decoded subjects
or BCL enumerators. Scalar byte floors preserve full assertion context and NFA
starts. Maps and NFA tables stay admitted while cursor copies can still retain
them; EOF releases temporary VM scratch rather than invalidating those copies.
The former BCL match/enumerator bridges, verifier-only cursor arguments and
quoted/Unicode-count engine callbacks are removed. Bounded letter runs count
complete scalars; zero-minimum shapes decline to the scalar VM. These consumer
replacements do not qualify remaining result/metadata holders,
shared pool/cache policies or the final dependency and R07 gates.
Python parser branch/sequence/class-item arrays, capture metadata,
their collection owners, and old/new growth and final-copy overlap
share admission. Completed AST arrays and immutable metadata tables retain their
loans through the enclosing construction owner; parser scratch releases on every
exit. The array arena keeps only temporary/retained byte totals, without an
array-reference registry or identity transfer scans. Explicit retained publication
charges the typed major extent again, including shared/repeated storage; earlier
construction denial is intentional. Failure releases both totals, success keeps
retained charges, and cleanup never refunds work or depends on GC timing.
Immutable metadata lookup/enumeration spends the current construction
owner's work, rather than a closed original owner. Temporary generic enumerable
views and boxed enumerators have no individual byte or acquisition-work charge;
their backing tables retain major-capacity admission. View/enumerator acquisition
still checks the original token/deadline, and table traversal still meters work.
This source simplification after scalar.81 may admit tighter numerical policies;
it does not promise accounting for every allocation or caller-retained storage.
Capture bodies use one opening-order array: a null slot marks an open group,
and closing stores its body at the same index. The parser transfers the admitted
final array through the numeric arena; retained aliases survive scratch cleanup.
Width/reference lookup charges the current construction owner without a hash
table or separate open-group buffer. Covered work totals may decrease.
Mutable construction maps hash/probe once for insertion. Growth keeps the old
table until rehash and the final metered probe succeed; failure releases the new
capacity without publishing a partial insertion. Collision chains still spend
bounded work and observe the enclosing token/deadline.
Scalar class folding uses sparse 256-code-point bit pages with admitted directory,
page/key vectors and sorting, replacing unadmitted SortedSet trees. It preserves
the profile's ASCII/Unicode closures and BMP/astral run ordering. Page scans,
membership updates, string-key hash/comparison extents and admitted metadata
enumeration charge covered work. Cleanup uses indexed release without allocating
or resuming a metered iterator. Other parser strings and preparation iterator
objects remain uncovered; this is not a complete construction policy.
Python parser group-name slices and literal-escape text, and translator rendered
patterns, generated names, class/range tables, numeric projection arrays and
ASCII selector output share construction admission. Character vectors admit
growth overlap; materialized strings admit their length, terminator and rounded
header before copying. Numeric admission does not keep discarded strings alive.
Hex/octal digits and integer/hex formatting use borrowed spans or fixed stack
scratch. This preserves bounded string-pattern interoperability; scalar matching
and byte coordinates are unchanged. Exception-message formatting, preparation
iterator objects, other construction consumers and retained-cache policies
remain uncovered. Construction loans do not establish RSS or total GC bounds.
The Python raw parser now stores alternation/sequence/group continuations in an
admitted array of explicit frames, including old/new growth overlap; it creates
no compiler-generated iterator for those grammar walkers. Root depth is checked
before acquiring the frame-stack descriptor, and every exit releases scratch
arrays and live depth without spending cleanup work. Production parser objects
admit conservative field-state charges before
acquisition; a parser explicitly supplied by an internal caller remains borrowed.
This does not cover the remaining core/conversion/emission iterators or diagnostic
formatting, and does not establish a complete construction or retained policy.
Python translation, reference/lookbehind checks, capture/iteration eligibility,
scalar width analysis and exact-literal extraction also use admitted value-frame
stacks. Their former yield continuations and captured closures are removed;
borrowed AST/class lists are traversed by index. Width memoization retains
reference identity for backreference DAGs, scalar widths and exact clamping.
Construction-vector enumeration uses a value enumerator without acquiring or
boxing a heap descriptor. Shared value-frame stacks check root/child depth before
descriptor/array acquisition and unwind depth on every exit. Other preparation,
core/conversion/emission iterators, diagnostic text and complete retained/RSS
policies remain outside this partial construction backstop.
The shared core grammar parser also uses admitted value frames for alternation,
concatenation, groups, lookarounds and conditional branches. Its six former yield
continuations and heap parse context are removed. Root depth and frame storage
admission precede parser-state acquisition; child growth admits overlapping arrays.
Failure releases live depth/scratch and restores parent object loans without
refunding work. Continuation-array scratch releases before capture numbering;
the conservative buffer-helper charge remains reserved through construction closure. The
frame's referenced nodes, text and collections retain separate ownership: their
acquisition admission, other shared preparation/emission walkers and retained
facade descriptors remain unfinished.

Shared scalar VM analysis and emission also use admitted value frames and borrowed
child cursors. Their yield iterators and captured analysis predicates are removed.
Analysis admits root depth/storage before acquiring its fact-map descriptor; both
walkers admit child depth and array overlap, meter traversal and unwind scratch on
failure. Nested assertion program frames precede lowerer-context acquisition.
Alternation/repeat patch buffers admit their descriptors; conditional expression
guards emit directly without synthetic IR nodes/branch arrays. Referenced IR,
other lowerer descriptors, program objects and numeric names still require their
separate admission; this does not close all preparation or retained storage.

Shared find min/max/longest-literal postorder facts, grouped-literal spans,
selected branch/optional-loop projections, nested expansion and the facade
capture predicate also use admitted value frames. Parent cursors save before
child admission; branch scratch contexts are acquired after frame admission.
Temporary fact/result maps use admitted descriptors and arrays. Scratch arrays,
descriptors and live depth release on successful results as well as decline or
resource failure, without refunding work. The shared yield-based preparation
driver and its heap iterator continuations are removed; the two duplicate
literal walkers share one implementation. These frames borrow existing root
walkers, strings, builders and result collections, whose separate admission is
still incomplete. The byte-safe verifier compiler uses admitted value
continuations, step builders, a temporary minimum-length memo and a temporary
payload registry. Root depth/storage precede descriptor acquisition; child
frames admit before nested builders. Capture, group and atomic wrappers consume
actual depth and preserve their enclosing variable-loop permission. Only proved
terminal or disjoint continuations admit variable loops; optional alternatives
and terminal alternatives require prefix-free arms. Optional arms and a required
following literal must have neither prefix relation: a longer optional arm can
otherwise hide a shorter successful path. Zero width alone does not certify a
greedy endpoint. Admitted continuation proofs require that a failed assertion
cannot become true at an earlier iteration endpoint or skipped optional arm.
Non-multiline end conditions preserve endpoint order; multiline end proofs
exclude LF from iteration starts. Boundary proofs use complete first/last scalar
sets, positive minimum repetition and uniform word membership. Nullable whole
suffixes qualify their empty-path assertions as well as first-set disjointness.
Contiguous origins after a variable choice and unproved assertion rewinds use
owned scalar execution.
First/last facts share admitted value continuations, include all nullable starts
or ends and invalidate every partial union when a branch or case fiber is
unproved. The continuation proof borrows existing nodes, admits real wrapper
depth and scratch, and unwinds pure proof loans without refunding work.
Alternative minimum-width scans spend shared
work before each visit. Successful compilation transfers the arrays reachable
from the returned step/text/alternative graph. Collapsed subprograms and unused
probe payloads release their loans after memo references are dropped; scratch
arrays and live depth unwind on every exit without refunding work. Temporary
object descriptors remain funded until their construction owner closes. Native
step arrays use byte admission; this does not close the shared instruction and
default-policy gates. The compiler is a stack value borrowing its memo and
registry; its former heap context, request objects, six yield iterators and
unadmitted step lists are removed. Prefix/text/alternative producers use admitted
arrays and complete lexical scalar proofs; speculative producers transfer
successful loans before the caller registers them. Nullable following masks
include every nullable child and every possible following first scalar.
Candidate preparation admits its search-plan roots, final structural stage
vectors, required-window and fallback candidate vectors. Selected structural
plans include transforms, boundary requirements, literal guards and bounds in
one final vector. Verification enrichment borrows the original plan and inserts
all prefix, fixed-literal, canonical class and minimum-length guards in one
admitted copy. Class cardinality and single-member guards read canonical masks
without acquiring member arrays. Invalid required windows decline before either
endpoint searcher acquires storage. Derived literal UTF-16 lengths are an admitted
interoperability cache; discovery continues over scalar boundaries and byte
extents. The discarded intermediate stage copies, separate enrichment methods
and unused fixed-set candidate-searcher producer are removed. Failed byte, work
or depth admission restores the parent loans and live depth without work refunds.
These selected producers do not close opaque preparation, whole-portfolio or
common instruction/default-policy qualification.
Prepared delimited and structured ASCII token searches derive membership from
canonical masks instead of retained opaque SearchValues. Their roots admit real
depth and definition scans before copying literal-finder bytes; failed admission
restores parent loans and depth without work refunds. Raw token operations prepare
one shared matcher per invocation and unwind its temporary literal loan. The two
duplicated raw matchers and repeated per-match preparation are removed. Structured
tokens preserve independent greedy first/second body runs, including disjoint
classes and overlapping splits. Optional delimiter absence uses nullable byte
state, so NUL is a complete scalar and a present delimiter followed by an empty
star class still consumes the delimiter. Selected token classification now
borrows the owned scalar tree, admits every actual wrapper depth, meters the
complete proof and transfers only final literal arrays plus typed ASCII masks.
Escaped atoms and NUL members retain canonical scalar membership; packed tail
sets and the duplicate textual class scanner are removed. ASCII intersections
execute only on ASCII subjects. The Unicode word email specialization separately
proves the complete word predicates and punctuation. Greedy heads overlapping
the primary delimiter, unsupported secondary literals and unproved shapes stay
on the scalar matcher. Delimited middle runs cannot accept tail-only members
before their separator. This selected cleanup does not close classification for
other families, the complete search portfolio, other opaque search preparation
or common invocation/resource policy.
Identifier, operator, Unicode category/letter, bounded word and single-ASCII-
exclusion count families now qualify from the owned tree and canonical scalar
predicates. Equivalent escaped and grouped spellings retain their native routes.
Actual wrapper depth, temporary proof capacity and scans are admitted; only a
selected negated-run infix retains a copied literal array. Shared borrowed
queries replace duplicate token walks and textual class/category/count scanners.
Nested repetitions do not lose inner bounds when queried as atoms. ASCII
intersections remain fenced by their subject envelope; Unicode letter/category
counts prove complete predicates. The negated-run finder selects the greedy
head endpoint before consuming its tail and never rewinds before the caller
floor. Other textual families, full portfolio and common policy remain open.
Literal edge families now borrow owned scalar nodes instead of rescanning pattern
text. Escaped/captured fragments, lexical anchor options and complete ASCII case
fibers qualify their final byte payload; mixed sensitive letters and wider fibers
retain scalar execution. Real wrappers, proof/copy work and the final literal
array share construction admission; scratch retains no descriptor or copied tree.
Leading-dot discovery proves the complete non-LF scalar language and preserves
the first successful line and its greedy endpoint. A trailing literal may itself
consume LF. Raw literal-edge parsers, their strict decoder and the ClassifyPattern
entry are removed. Other textual families and common resource policy stay open.
Date, URI and the ordered dotted-decimal profile now borrow the same owned tree
and admitted wrapper frames. Their plans contain values without copied grammar
or retained proof scratch. Date digit masks prove only the ASCII intersection;
Unicode digits and boundaries use scalar execution. Positive finite fields use
their declared minimum rather than a fixed six-byte search floor. URI proofs
require the full Unicode word category union and Unicode whitespace exclusions.
The body requires two complete scalars; a supplementary scalar supplies one.
Fragments accept NUL, and their greedy extent includes any apparent URI inside
the fragment. Dotted-decimal octet proofs preserve alternative order and the
existing two-or-three-digit grammar, with one selector for matching and counting.
Lazy, atomic, reordered, folded or otherwise unproved shapes retain scalar grammar
execution. The five former textual token classifiers and duplicate IPv4 selector
and diagnostic route are physically removed.
Number and quoted-string prefixes, signed decimal, hex color and spreadsheet
whole profiles now prove complete owned grammar, including every optional feature
their fixed matcher accepts. Missing sign/fraction/exponent/dot/hash/colon features
cannot select a broader shortcut. Lexical beginning and final-LF anchors are
proved at their nodes; multiline and strict-end shapes keep scalar execution.
Folded ASCII intersections use borrowed positive scalar ranges and the captured
construction culture, including non-ASCII members supplying ASCII fibers. This
does not qualify non-ASCII subjects for the ASCII shortcut. Quoted prefixes prove
the complete negative body class and non-LF escaped scalar; unescaped LF is allowed,
and a backslash consumes one complete scalar. One shared quoted matcher replaces
duplicated loops and three opaque/static stop holders. Color and spreadsheet
matchers exclude one permitted final LF from their byte length. Eight old textual
classifier/helpers are physically removed. Other families and common resource
and retained policies remain open.
Prefix-until-byte, trimmed optional-prefix, line-title, quoted-line and leading-
digit-tail routes now prove owned scalar grammar using shared admitted frames.
Fragmented literals borrow their actual capture/group wrappers; only selected
final byte arrays survive qualification. Whitespace proofs cover the complete
Unicode predicate. Optional trimmed prefixes require disjoint, non-whitespace
first bytes. Leading-digit tails require an end arm and at most eight non-digit,
non-LF separators; unproved alternatives retain scalar execution. Greedy optional
whitespace may consume a final LF. Line counts consume Unicode indentation and
complete prefixes; quoted counts use the exact declared prefix and allow Unicode
whitespace and raw quoted bodies spanning LF. The quoted grammar's literal-dot
arm does not admit backslash escapes. Eleven textual methods, the PrefixFamilies
file, a copied-array/closure candidate probe and its verifier delegate are removed.
Email, balancing and digit/hex-query routes now prove their complete owned scalar
grammar. The family analyzer dispatches only on that tree; its remaining textual
selectors and five partial files are removed. Balancing proves the actual shared
capture-stack identity and rejects outside effects on that stack. Email and
balancing retain value plans; query proofs borrow shared cursor/literal slices and
fund only their five/six final byte arrays. Three bounded UInt128 value masks
preserve prefix dot predicates separately from literal bytes, including NUL.
Prefixes beyond 128 positions, Turkic folding, sensitive lexical fragments and
unknown shapes retain owned scalar execution. Query matching requires the declared
equals sign, honors optional segment/value backtracking, preserves CR and negative
value classes, and returns the exact final-LF extent. Duplicate URL suffix delegates
and caches are removed. Positive ASCII definition queries borrow canonical
endpoints instead of replaying a full scalar token. This qualification does not
close the whole-search, retained or common-policy gates.
Compiled runtime preference borrows the admitted plan, candidate literal and
ordered prefix guards. Existing runtime-kind and reason gates retain their choices;
short weak prefilters and exact-length/window exclusions use constant queries.
Fixed-distance coverage compares wide byte offsets, so wrapped Int32 coordinates
cannot certify a prefix. Each visited literal byte and guard consumes construction
work without copying inputs or retaining a policy object. Selection shares the
factory's depth and cumulative budget; child failure restores parent loans and
depth while consumed scan work remains charged. The unused direct-route option
normalizer and the preference method's former BCL-engine name are removed. This
is an owned scalar execution hint, not an alternate matching contract.
Deterministic prefix, literal-family, leading-mask, byte-safety and guard walks
also use admitted value frames and metered visits, comparisons and ASCII copies.
Incomplete branches invalidate whole-family proofs and partial mask unions.
Whitespace
guard preparation borrows the remaining children instead of manufacturing a
node/array. Ignore-case anchor validation has no input-sized stack scratch; it
admits only the successful folded byte array and transfers that loan to its
enclosing construction owner. Other preparation producers retain separate
admission work. Qualified ASCII replay intersections defer non-ASCII subjects to
scalar execution; surrogate-half class membership does not determine loop
admission or minimum scalar counts.
Front-end quoted-run/quote checks, window phrases, finite byte widths and line
counts, trailing literals and deterministic prefix sets also use admitted value
frames. Branch loop/alternate discovery, feature census and NonBacktracking
grammar checks share the same construction owner for storage, depth and visits.
Saved cursors retain source-order choices and reverse-source diagnostic order;
failed children stop later work. Successful, declined and exceptional exits release
scratch without refunding work. Whitespace prefix analysis borrows a child cursor
and preserves the owned tree's parent links instead of manufacturing a parent.
Output builders, projection strings/spans and other preparation payloads remain
separate admission work.
Feature census reads grammar nodes directly. The former per-node atomicity,
capture-containment, backtracking and loop-membership analysis, its four sets,
retained semantic field and orphaned backtracking predicate are removed. Its
only production loop query was redundant with the admitted feature cursor's
own loop-kind census. Tests retain live grammar/features, capture metadata,
deep parent ownership and scalar operation/resource invariants instead of
pinning unused BCL runtime facts.
Required-literal family preparation also admits wrapper/collector value frames,
temporary family/candidate/member vectors, ordinal membership maps and arena
tracking. Single-character text admits its string extent;
existing node text remains borrowed. Sorting, membership hashing/equality,
selection and copying spend shared work. Only the selected reference array
transfers its payload loan; candidate arrays/maps and live depth unwind on every
exit. Numeric object/string charges stay reserved until their owner closes.
Decline/failure restores parent loans without refunding work. These search facts
are heuristics, not scalar matching widths or complete retained-cache policies.
Native eligibility and simple-plan case/tail checks admit root/ancestor value
frames and every resume; opposite case checks share one traversal. Literal
validation charges each decoded pattern scalar before the bounded decode.
Pure boolean scans restore all temporary loans and descriptors on return.
Flat NonBacktracking admission finishes its scan before scalar lowering, shares
the same owner and rolls back a declined proof's lowered payload as well as
scan scratch; only an accepted program transfers. The unused lookaround
eligibility helper is removed rather than given a second implementation.
These checks gate optimizations, not supported scalar grammar or general policies.
Native literal-family preparation admits ancestor frames, its local heuristic
budget, scratch branch/product/reference arrays, copied single-character and
concatenated/folded text and conservative builder-helper charges. Final reference or
ASCII byte arrays transfer their loans; scratch and failed speculation recover
under the shared owner, without refunding work. Folded set-to-literal conversion
requires a positive whole class; negation/subtraction cannot use exclusion ranges
as a literal proof. ASCII-whitespace wire subtraction uses admitted segment text
and vectors rather than the legacy predicate's raw buffers. Node/character/query/
product/copy work is metered before execution. Pattern text stays interoperability
input, not a UTF-16 subject unit. Other native producers and general retained
policies remain incomplete.
Native quoted-relation LF counting uses admitted ancestor continuations without
the former stack/map or copied single-character strings. Its LF bound is distinct
from front-end CR-or-LF line safety. Shared construction class queries retain
borrowed plain wires, admit complete subtraction segments once, and meter queries
before scanning. ASCII structural literal/alternate bytes and suffix-part arrays
have admitted output loans; failed suffix or parent-family speculation recovers
all child loans. Quote/separator proofs unwind temporary loans, retain wrapper
depth and avoid quote-string copies. Prefix comparisons spend work before byte
scans. Existing local grammar, branch order, zero-minimum separators and checked
line-count overflow are retained.
Native literal/lookahead preparation admits final UTF-8 bytes and alternative
reference arrays under the same speculative owner as its child text. Encoding
validates complete scalars before acquisition and meters both passes; it cannot
turn an impossible surrogate atom into U+FFFD. Lookahead concatenation uses the
admitted text buffer. Flat captured/alternative patterns use borrowed spans,
without Split or substring intermediates, and scan alternatives in linear work.
Literal/alternative wrapper ancestors remain in live-depth admission; late
boundary, empty, unsupported or folded-Unicode decline restores all child loans
without refunding work. Successful scratch arrays unwind and output arrays/text
retain their loans.
Search-fact initializers are value carriers; their immutable reference storage
is admitted before acquisition using conservative field-state charges. Default/shared empty facts borrow one static carrier. Byte,
alternative, set and window payloads remain independently owned by their producers;
this storage admission does not admit an unconverted payload retroactively.
Simple literal expansion admits reference vectors, Cartesian byte copies and
final arrays, retaining original order and expansion limits. Positive ASCII and
folded class proofs use scalar value masks rather than raw byte copies/HashSet
scratch. Prefix replacements admit old/new overlap and release superseded arrays;
failed children cannot mutate their parent's vector. The native singleton route
reuses its final byte payload. Its admitted ASCII string serves literal-pattern
diagnostics/classification only; no subject is decoded and bytes are not re-encoded.
The enclosing speculative attempt recovers child arrays/text/fact storage on late
decline without refunding work. Structural identifier plans also admit singleton
suffix arrays, compiled suffix metadata, exact verifier steps and linear verifier
arrays before acquisition. The former temporary step list is replaced by an
exact final array; linear instructions share the owning plan's compiled suffix
payload rather than rebuilding it. Ordered-window plans admit trailing searcher
arrays and paired literal/separator arrays, borrow singleton trailing bytes
without an intermediate reference array, and select the first shortest literal
with a metered scan instead of sorting. Literal overlap, suffix compilation,
kernel classification and exact trailing-search preparation charge their bounded
scans before execution. Structural attempts keep root/wrapper continuations live;
late decline or denial recovers completed child loans and never refunds work.
Quoted-relation edge extraction borrows original children instead of acquiring
a synthetic concatenation and changing their parent links.
Structural linear preparation admits instruction and deterministic-step arrays
against both construction bytes and instruction capacity before acquisition.
Fixed-width check arrays use byte admission; ineligible views decline before
acquiring those arrays. Conversion, eligibility and classification scans share
construction work and the owner's original cancellation/deadline authority.
The class-run and fixed-token routes derive both views from one instruction
program. Failed conversion or multi-literal search preparation restores parent
capacity without refunding work. Scalar lowering admits generated decimal group
name strings and formats/classifies them with bounded stack scratch. These
additional acquisitions can reach the provisional construction backstop earlier;
they preserve matching and capture-name semantics and do not qualify a complete
retained-cache certificate or public construction policy.
Upper-word identifier anchor selection returns the existing ASCII value-mask
carrier without heap scratch or provisional byte arrays. Eligibility shares that
selector; native Count/FindNext use at most 128 bytes of stack scratch, and emitted
lowering admits one final selected-byte array through the class-copy producer.
Distinct-byte priority, repeated-prefix rarity weighting and later-offset ties
are preserved. Prefix/offset scans charge the active construction owner's work
and interruption authority; runtime selection polls the caller's original absolute
deadline without creating a construction owner. Fixed mask classification and
stack copies are bounded independently of prefix length. Other simple-plan
branch/token/search extraction, complete token-run/search-set acquisition, remaining
structural predicate/scalar-width work and complete retained/shared policies
remain open. This is buffer/work/depth admission for covered preparation, not
complete resource policy, retained storage or RSS qualification.
ASCII class byte extraction admits its exact output before acquisition and
charges initialization and ascending mask-bit copies. Shared empties are borrowed;
complements remain restricted to ASCII. Owned byte-search preparation either copies
caller bytes into an admitted independent array or reuses the class producer's
owned output. The reusable search kernel has no front-end budget dependency.
Covered run, head/tail and fixed-distance preparation now builds owned byte-set
state: four membership masks, two fixed nibble lookup tables, range bounds and
representation flags. Admit its conservative field-state charge
before object acquisition, and charge bounded table-building work before scanning.
Head/tail and fixed-distance preparation derive this state directly from masks,
without temporary class arrays. Their former opaque SearchValues fields and the
unused retained TailBytes field are removed. The kernel handles forward/reverse
and complemented searches over the full byte domain; the ASCII class factory
keeps complements restricted to ASCII. Vector loads stay within the subject,
including overlapping tail loads, and scalar execution remains available.
Count-only eligibility and projected-mask checks do not materialize class arrays.
Two runtime validators use fixed 128-byte stack scratch instead of acquiring
class arrays or entering construction scopes. Generic byte-search constructors
share the same flavor-neutral construction owner in Internal/Construction. They
admit one independent copy from an array or borrowed span, then derive large-set
state from that owned copy. Shared empty searches acquire nothing. Five multi-literal
producers pass stack spans directly, avoiding temporary arrays before the admitted
copy. The front end retains only its ASCII-class bridge; redundant generic factories
are removed. Exact multi-literal trie construction now admits temporary node/edge
value buffers (including growth overlap and descriptors), frozen node/key/child
arrays, the root map and its byte-search state. Iterative insertion, bounded sibling
visits, freezing and in-place sorting share construction work. Temporary arrays
release before return; numeric helper charges stay reserved until the enclosing owner
closes. One fixed 256-entry stack row handles child ordering. Empty tries borrow
all state. The superseded node objects, per-node dictionaries, list and sorting
arrays are removed; runtime trie layout and literal priority are unchanged.
Exact multi-literal automaton construction admits one growable transition matrix
and value-state buffer with growth overlap, an exact temporary failure queue and
the final matrix/output extents. Iterative insertion, failure completion and output
projection share work. The final matrix transfers when its capacity is exact;
otherwise its final copy admits old/new overlap. Temporary arrays release before
return; numeric helper charges stay reserved until construction closes. Empty automata borrow
all state. Mutable node objects, per-node transition/flag arrays, List/Queue
storage, unused node APIs and redundant output initialization are removed.
Runtime automaton layout and matching methods remain unchanged.
Packed SIMD nibble, scalar nibble, offset-mask and rare-byte prefilter construction
admits every retained offset/map, nested mask and vector array plus shared byte-search
state. Scans, table initialization, selection and projection share construction work
and nested depth. Offset selection retains only three stack scores; weighted folded
SIMD selection tests its three selected offsets instead of a length-sized bitmap.
The old score list, LINQ ordering/materialization and selection bitmap are removed.
Empty families borrow all extents, including the rare-byte map. Runtime field layouts,
frequency/tie ordering, masks and candidate methods remain unchanged.
Packed-discriminator construction admits its literal/id maps, required anchor
tables and shared byte-search state. Absent anchors borrow empty extents; families
larger than the byte alphabet reject before offset scanning. Leading UTF-8 segment
preparation validates eligibility before acquiring an admitted segment-reference
buffer. It copies each distinct segment once and transfers or admits its final
reference array in first-encounter order. Its numeric helper charge stays reserved until the
enclosing owner closes. Per-bucket literal references and their bucket type,
assignment/count state, duplicate copies, List storage and repeated growth are
removed; literal verification uses the wrapper's original literal list. The unchanged
shortest-length scan is shared across these builders and mask preparation. Packed
runtime fields and forward methods are unchanged; segment candidates use the same
ordered prefixes directly, with constant storage for identical-prefix families;
packed reverse search now excludes a rejected candidate to guarantee progress.
Budget/scope semantics and provisional defaults remain unchanged.
Exact literal-set preparation admits final ordered bucket/reference arrays,
prefix copies, second-byte membership filters, prefix discriminator maps and the
first-byte map/search. Fixed byte-alphabet count/index rows replace List grouping.
The owned byte-search carrier replaces opaque first-byte and unique-anchor
SearchValues state. Empty families borrow every extent; empty literals preserve
their bucket and map behavior under normal admission. Anchor selection scans
bucket references directly without flattening. Only selected anchors/tries are
built; a borrowed-data trie releases its admitted temporary reference vector
after copying keys, order and lengths. Child failure refunds acquisitions and
retainers, preserves parent state, and never refunds spent work. The never-selected
second-byte search tier, its enum/dispatch/runtime methods and search object,
unused first-byte/anchor byte carriers, eager unused anchors/tries and raw/LINQ
construction paths are removed. The live second-byte membership filter and
candidate/priority behavior remain. Reverse common-prefix search retains the
complete prefix of its last permitted start; the former truncated window could
miss final matches. This is construction capacity, not a complete
public/retained policy or a performance qualification.
ASCII-folded literal-set preparation admits normalized byte payloads, ordered
bucket/reference arrays, the first-byte map and owned byte search, plus the
conservative helper state charge. An empty family
borrows all state; a preflight finds empty alternatives before acquiring discarded
normalizations. Fixed alphabet scratch replaces grouping lists and intermediate
search-byte copies. Eligible correlated masks receive an admitted temporary
reference vector in grouped order, released after the masks copy their state;
ineligible mask preparation borrows original lengths without flattening. Failed
children restore parent storage, retainers and depth while work remains spent.
This preserves ASCII folding, bucket/literal order and runtime verification.
Small ASCII-family preparation meters eligibility, ranks offsets in fixed
four-entry scratch, and admits only the selected anchor byte search, filter
arrays/values and pair-dispatch entries alongside its exact search data. Three
literal dispatch borrows its inputs. One-bucket families decline before acquiring
exact metadata. Its exact-data mode retains literal/discriminator maps and
first-byte search without unused prefix copies or second-byte membership arrays;
the full exact-search mode keeps those live consumers' metadata unchanged.
Length-sized candidate lists/arrays, sorting/LINQ, opaque anchor
and filter SearchValues, unused original-list retention and the unreachable
fallback are removed. Selected metadata stays bounded independently of the number
of candidate offsets. A prefix discriminator selects a literal for full
verification; checking that byte and a few anchor/filter positions does not prove
the common prefix. The old unchecked-prefix helper is removed.
Multi-literal wrapper construction admits root/child depth, selection scans and
the retained reference vectors required by borrowed exact/folded searches. It
borrows their payloads and preserves grouped order without charging parent data
again. Raw exact construction selects packed, earliest, automaton, trie or direct
search before acquiring that route's structures; discarded exact buckets,
prefix copies and anchor/trie preparation are no longer built. Selection uses
fixed byte-alphabet scratch rather than retained candidate state. Automaton
trie verification and earliest/hybrid Count helpers remain live; a supplied
search's trie cannot be borrowed blindly because its branch order may differ
from the wrapper's grouped vector. Earliest search meters one length scan and
borrows its literal vector. Empty wrappers preserve their kinds without acquiring
state. Failures restore child storage, retainers and depth while work stays spent.
The runtime short ASCII-family counter admits eligibility, pair discrimination,
scoring and selected byte-search state under one root. Four-byte fixed scratch
replaces all offset-candidate arrays, and the unused longest-length calculation
is removed. The selected membership search retains distinct bytes only, avoiding
duplicate values and an unnecessary owned set. The counter borrows its two to four
literals, preserving pair scores,
tie-breaking and nonoverlapping matching. Runtime three-byte BMP-family guard
preparation admits active value buffers, final ordered reference vectors and each
distinct three-byte copy. Inactive second/third guards borrow empties; a too-short
family declines before acquisition. The caller still proves the BMP scalar shape,
and the execution path still validates strict UTF-8 and compares full literals.
Its preparation helper does not independently establish scalar validity. Mutable
lists, intermediate scalar-reference arrays and the short counter's opaque SearchValues
are removed. The literal runtime root, composite trailing-search holder and
selected exact/family wrapper objects now admit complete transfer field state and
retainers. Root depth covers their eligibility scans and child preparation. The
small runtime family borrows an already ordered vector or admits its sorted copy,
and uses owned byte search; ineligible vectors acquire no candidate state. BMP and
leading-segment preflights, primitive minimum-length scanning and composite copies
share the owner. The top-level factory constructs only its selected literal or
nonliteral implementation, without a discarded second runtime. Unused runtime
enumeration state, duplicate ASCII scanning and obsolete family-wide BMP helpers
are removed. Other runtime objects, opaque state and remaining CLR/JIT metadata
remain separate construction scope debt; owned dynamic emission is covered below.
Substring preparation borrows exact needles and meters short exact anchor scans.
Folded candidate searches receive admitted ASCII needles; their folded bytes are
borrowed when already canonical, otherwise one final copy is admitted. Empty and
single-byte searches acquire no folded payload, and short searches retain no
unused shift table. Longer searches admit the final 256-entry shift table and
meter folding, filling, preferred-index histogram clearing, scoring and anchor
selection under the shared construction root. Failed children restore parent capacity and depth
while consumed work remains spent. Reverse folded searches keep the bytes needed
to verify complete candidates. Ordered-window and reverse-suffix execution borrow
prepared literal searches instead of rebuilding them per operation. A suffix
part stores its canonical prepared search by value rather than a duplicate
literal-array field; admitted suffix arrays use that actual element stride.
Runtime tier selection, anchor scores and tie-breaking remain the same. The
packed-pair experiment is retired: its production thresholds cannot be met by
the current byte-frequency scores. Remove its retained pair, scoring and matcher
code, enum member and three forced benchmark probes. Derive the remaining
none/short/vector tier from the existing options and needle length, without
retaining another policy field. Repeated-anchor candidates remain covered by
independent first/last/count oracles on the live kernels. These byte candidate
kernels do not define Unicode case folding; the scalar engine
owns non-ASCII folding and complete-scalar verification.
Search dispositions borrow canonical prefilter sources and the admitted fallback
candidate vector. The search plan derives candidate, confirmation and operation
views using constant queries instead of retaining duplicate payload and policy
copies. The selector takes a readonly reference and evaluates each source once;
these getters acquire no arrays, objects, work or depth. Runtime owners can retain
a consumed operation view. Preserve source priority, semantics, confirmation,
projection and observability choices. The small projection marker remains stored
to preserve the distinction between a default plan and a prepared empty plan;
UTF-16 projection remains derived interoperability metadata. Admission of objects
that embed the plan follows its actual reduced field extent.
Pattern-dependent emitted anchors, Cyrillic fallback anchors and PCRE2 leading/
repeat sets use the same owned immutable byte-mask kernel. Mask scans and field
state share byte/work/depth admission; PCRE2 unions canonical ASCII masks rather
than allocating class-byte or LINQ intermediate arrays. Failed set preparation
cannot publish a class cache. Repeated preparation borrows that cache.
Cyrillic strategy preparation borrows pattern slices, admits literal/unit/byte
payloads and its complete strategy field state, and releases the prefilter's
temporary reference vector. It prepares shared-anchor storage only when the
SIMD prefilter declines. The scalar fallback remains available without intrinsics.
Declines release speculative capacity/depth while work remains spent.
Search-guided fallback has one canonical managed candidate-search and scalar-
verification implementation. Its former optional emitted IsMatch/Count wrapper,
capability/policy and retained field are removed. Public fallback discovery uses
the owned scalar program before compiled-runtime dispatch; the internal search
mode remains available. This retirement implies no public matching speedup.
Live specialized emitters preserve the admission and interruption rules below.

Owned dynamic-method construction shares the enclosing budget for serialized IL,
labels, branch fixups, locals, method/type token caches, temporary dispatch vectors,
stack-analysis queues and complete builder/wrapper descriptors. Structural class
collection uses owned vectors/maps; literal dispatch and kernel-lowering metadata
admit their acquired arrays while borrowing existing literal payloads. Every
growth and final copy admits overlapping extents. Labels/locals and the emitter
are value carriers; no ILGenerator, LocalBuilder, SignatureHelper or independent
construction owner retains a second set of library program buffers.
Temporary dynamic-emission arrays use one numeric scratch subtotal rather than
object-retaining descriptors. Each major array is admitted before allocation and
remains charged until builder disposal, even if GC collects an unreferenced array.
Disposal does not invalidate caller-held arrays or published delegates. Removing
descriptor storage/work can lower construction byte/work requirements; runtime
copy admission, instruction/depth bounds and interruption remain unchanged.

Serialized IL byte capacity conservatively counts against the provisional
instruction-capacity gate, including the managed runtime code copy. Parameter,
code and local-signature managed copies are admitted separately before CLR calls.
Conservative delegate field-state charges are admitted before
publication. Each builder publishes one delegate with an immutable receipt for
its final CLR-managed parameter, code and local-signature copies. The receipt
uses final array lengths and modeled headers, not transient construction peaks.
Emitted wrapper descriptors include the inline receipt state. Retained collection
reserves delegate metadata plus these final managed copies conservatively;
aliases share one allowance. Independent runtime inspection qualifies the actual
copy extents. Production does not inspect private CLR fields or retain emission
scratch. Owned code, fixup and stack walks spend shared work; pending stack
analysis frames share live depth. Resource failures, cancellation and deadlines
restore child capacity/depth/object loans while preserving spent work. The same
interruption authority is checked again after opaque delegate creation.
CLR reflection records, token resolver internals, dynamic-method metadata,
method-signature metadata and native JIT storage remain the named runtime-metadata
exclusion, distinct from these admitted and retained managed copies. Complete
adapter program/AST coverage, safe defaults and retained policies remain
unqualified; covered eager construction uses the public admission described below.
This covered producer does not establish a process RSS or cumulative GC bound.
Byte-safe DFA construction shares the enclosing construction owner for its
compiler and graph descriptors, deterministic vectors, NFA states and edges,
closure keys, work queues, transition buckets and maps. Graph growth and final
copies admit both old and new extents before acquisition. Borrowed linear
programs retain their independent ownership. Pending NFA and closure frames
share live depth; proof, graph and collection work stays charged after declines.
The 256-state DFA, 2,048-state NFA, 4,096-transition and one-million-step optimizer
ceilings remain heuristic declines. Shared resource failures, cancellation and
deadlines propagate instead of selecting an unmetered route. Failed children
restore the parent's capacity and depth; successful graphs transfer their final
arrays. Internal construction interruption authority belongs to the shared
owner. These graph bounds participate in covered public eager construction;
they do not prove complete construction, lazy-storage or invocation coverage.
Optional byte-safe bodies containing loop atoms use the admitted NFA-to-DFA
construction path. The former deterministic append shortcut could add duplicate
transitions, accept an incomplete body or exceed an inner repeat bound; it is
removed, including its repeated-loop helper and redundant shape proof. Fixed-atom
optional bodies and the top-level disjoint-loop kernel retain their existing
paths. Supported shape admission remains separate from direct-kernel eligibility.
Graph growth may reach the existing optimizer/resource backstops earlier; a
declined DFA still leaves the supported owned linear/scalar route available.
Graph emission recognizes the same negative/int.MaxValue open-ended sentinel
representations as shape admission and the direct kernel.
Eight fixed executor byte sets now use bounded immutable value fields and share
the existing owned byte-mask/SIMD kernel with admitted pattern-dependent sets.
No shipped SearchValues reference or runtime ASCII category-map array remains.
The 128 ASCII category bytes are borrowed compiler data; scalar category fixtures
qualify every entry and preserve mixed-width category counting. Fixed table
initialization uses constant bounded stack scratch and acquires no heap program
buffer. Pattern-dependent set descriptors keep their original byte/work/depth
loans; moving the search kernel does not reset or relax admission. Shared fixed
value/embedded data has a declared constant footprint, distinct from per-pattern
and invocation capacity. Complete retained/shared policy remains unfinished.
Remaining upstream preparation metadata and complete retained/shared preparation
remain uncovered. Their complete
admission remains mandatory; the covered array loans are not a complete memory bound.
Find preparation admits trailing-anchor/leading-literal ancestor frames and
longest-literal postorder frames/maps, with cumulative visits and literal-run
selection work. Literal builders and copied character/run strings share byte
admission. Existing pattern strings are
borrowed. Returned text transfers its loans; pure anchor results and declined
literal scans restore scratch and numeric helper charges. Frame/map arrays and live depth
unwind on every exit; successful text/object charges remain reserved
until construction closes. The unused leading-anchor fact/producer and redundant
cached tree root are removed. Search widths bound consumed scalar UTF-8 bytes:
literal pairs decode once per scalar, plain ranges exclude surrogate code points,
and complex classes/case fibers retain sound conservative bounds. Conditions
measure their yes/no branches with an implicit empty else; references have no
independent finite maximum. Arithmetic saturates the lower bound at int.MaxValue
and makes an overflowing upper bound unknown. A fixed distance requires equal
known byte bounds for every preceding node and checked addition. Variable prefixes
end the proof instead of contributing their minimum. Fixed-literal preparation
admits its width map, ancestor frames and copied character text; the old raw stack
and result holder are removed. ASCII-set offsets include complete encoded literal
prefixes; folded variable-width sets cannot supply an ASCII-only fixed certificate.
Finite ordered-window gaps reuse these scalar byte bounds, including folded
literals, Unicode classes and dot. Combined gap arithmetic declines on overflow
instead of wrapping into a false maximum. Line-break counts are an independent
optional proof; plain class-wire membership is metered before scanning, and class
subtraction declines that proof without invoking its legacy allocating predicate.
The duplicate window-width formulas and unused line-safety frame state are removed.
Alternate/set results and the facts container still require complete admission;
these facts do not prove complete construction/retained bounds or that other
native width producers meet the scalar contract.
Byte-safe execution-tree and program lowering also admit value frames, visits,
temporary reference maps, copied child arrays and conservative node/tree/program charges. Program vectors charge aggregate instruction slots
as well as bytes, including growth/final-copy overlap. Fixed-capture layout
preparation admits its eligibility scan, frames, temporary index map and compact
capture array; the retained hash table is removed. Completed results transfer
payload loans, while decline or failure restores parent loans/depth without
refunding work. Other preparation producers and retained-cache policies remain
uncovered; these charges do not establish complete construction bounds.
Python scalar conversion also uses admitted value frames, deleting its captured
yield continuation. Its arena/map helpers and direct structural IR nodes
admit conservative state charges before acquisition, including base Kind and
initializer fields where the extent exceeds the helper floor. Numeric capture-name
strings admit their buffers and final copy; the immutable whole-match name is borrowed.
Failed or declined conversion restores parent loans/depth without refunding
work, and releases frame-array scratch before lowering. Core fragment-parser
and leaf-conversion IR objects, other core producers and lazy retained preparation
remain separate admission work; these charges do not establish complete policies.
Adapter UTF-8 pattern entries borrow bytes until the shared constructor scope.
Validation/counting precedes decoded-text admission and acquisition. The string
charge is a 32-byte header plus its UTF-16 payload and terminator rounded to 16
bytes; shared empty text costs zero. That retained loan remains live through
parsing, nested core compilation and lowering. Caller-owned string patterns are
borrowed. Profile-specific UTF-8 errors and policy-validation ordering remain.
Pattern text is parser interoperability; matching consumes Unicode scalars.
Covered construction work shares a cumulative counter: a byte per validation,
counting or decode pass; an element per admitted array initialization/copy; an
iteration per covered core/PythonRe parser and owned tree-conversion loop; and
one unit per map probe/rehash, sort comparison/swap or preparation resume.
Core parser bulk delimiter scans and literal copies admit conservative extents.
Native-literal validation/counting and UTF8 encoding retain their whole-pass
work admission and Rune algorithms, polling the same construction authority
between2048 UTF16 pattern units. A scalar crossing the chunk end is completed,
so the bounded interval is at most2049 units; matching remains over UTF8 scalars.
These checkpoints charge no additional numeric work and add no allocation.
Short spans require no extra poll. Interruption can precede a later malformed
tail or byte acquisition; failure during encoding releases unpublished capacity.
Development after scalar.71 also bounds construction UTF8 decoding's count and
fill passes with the existing scalar-aligned4096-byte block helper and checked
codec span operations. Strict versus repairing fallback, absolute malformed-byte
indices/offending bytes, two whole-pass numeric work charges and one admitted
string remain. Chunk checkpoints borrow the same owner and charge zero additional
work; short inputs keep their checkpoints. Interruption can precede a later
malformed tail, and failed fill releases only unpublished string capacity. Later
chunk decoder diagnostic text is owned; its error index remains input-relative.
No temporary array, retained owner, new scanner or policy mode is introduced.
Other text/encoding producers retain separate coverage.
The owned in-place heap sort admits each comparison/swap and propagates resource
failures directly. Core/PythonRe parsers and preparation walkers share live
explicit-frame depth; denied pushes restore their parent and release admitted
scratch, and cleanup spends no work. Python alternation, sequence and group parsing suspends child
work on that stack; conditional branches reuse the same sequence parser.
Python rendering, reference/lookbehind validation, scalar-width analysis and
capture/iteration/literal fast-path checks also suspend child traversal on that
stack. Width memoization uses reference identity and admitted temporary maps;
lookbehind validation shares its memo across the translation. Literal extraction
flattens once into its consumer's text rather than building recursive intermediate
strings. Translator loops and prefix comparisons admit work; group-name ordering
and range merging use the admitted heap sort.
Ordinal text normalization admits one conservative source-length times needle-
length search pass. Nonoverlapping searches resume beyond each match; fixed
production needles therefore incur linear search admission, with interruption
checkpoints between searches. Output copying and growth keep their independent
work/storage admission, including expansion. Remaining suffixes are not charged
again as whole passes.
The core facade's capture census uses the same explicit admitted traversal,
including capture-free nested trees; it no longer recurses on the CLR stack.
Python scalar widths use UInt128 intermediates and the CPython 3.13.2 2^64
getwidth clamp/sentinel. Fixedness does not collapse at int.MaxValue; fixed
lookbehind widths above the 32-bit code limit report "looks too much behind".
Provisional limits are 67,108,864 work units and 65,536 covered live frames.
The explicit Core/PythonRe `Compile` factories expose caller-selected eager
admission through `Utf8ConstructionPolicy`: covered work, live traversal depth,
instruction capacity and major bytes. Null retains the fixed backstop; a positive
value tightens that ceiling. The work/storage units and coverage above apply;
this is not every allocation, CLR/JIT metadata, lazy preparation, caller retention
or process RSS. Existing constructors retain their defaults. Construction timeout
and cancellation are separate from the resulting expression's execution policy.
One synchronous owner spans eager compilation, with a final interruption check
before publication; pre-cancellation precedes all pattern/argument work. Deadline
denial uses `RegexMatchTimeoutException`; byte-pattern diagnostics omit Pattern
to avoid additional decoding. Factories do not consult static pattern caches.
The explicit PythonRe factory also completes its derived FullMatch program in
this owner before publication. Its instruction copy and metadata consume the
construction allowance upfront, even if FullMatch is unused; no construction
authority is retained. PythonRe constructors keep retryable lazy preparation.
Replacement preparation and other uncovered acquisitions remain separate.
PythonRe development additionally reuses finite invocation working bytes as an
independent additional-capacity ceiling for cold template parsing and
execution-triggered FullMatch preparation, as specified
in SPEC-PYTHONRE.md; this does not unify construction/execution/retention budgets.
PCRE2 exposes the same four construction caps and independent interruption through
its own `Compile` factories; its coverage and failures are in `SPEC-PCRE2.md`.
Exhaustion reports ConstructionWorkSteps, ConstructionDepth,
ConstructionInstructionCapacity or ConstructionBytes with checked requested
totals. Failed children roll back capacity/depth but never refund admitted work;
neither nested construction nor a declined speculative plan can reset it.
These remain partial counters: other producers, descriptors and work/storage
paths require separate qualification. The current Python translator uses admitted
text, arrays, buffers and maps instead of the former raw builders/collections.
PythonRe facade capture-name arrays/text now share that owner, and replacement
name lookup borrows the parser's immutable admitted map. These covered extents
do not establish complete construction controls, retained policies or safe defaults.
It covers core/PythonRe tree-to-scalar conversion maps/frames, child/literal
arrays, decoded class/subtraction payloads, instruction vectors, analysis/capture
maps, lowering/preparation frames, auxiliary vectors, nullable-graph discovery,
epsilon-order preparation and newly acquired program metadata arrays. Temporary
IR arrays release after lowering; class tables and other escaping arrays retain
their byte loans with the completed program. Old/new growth and final-copy overlap, completed
assertion programs and source/lifted NonBacktracking graphs remain charged
while live. Admission precedes acquisition. Exhaustion throws
`Utf8ResourceLimitExceededException` with resource kind, maximum and requested
capacity, and never selects a BCL engine. A failed construction restores its
parent's admission state. These covered producers participate in the explicit
construction policy above; coverage remains limited to the extents described in
this section. Independently borrowed payloads, temporary immutable-metadata
enumerable views/boxing, opaque CLR/JIT storage and later lazy preparation retain
their stated exclusions.
PCRE2's separate program construction is qualified in `SPEC-PCRE2.md`. Complete
coverage, common retained-storage policy and qualified safe defaults remain open.
The byte charge bounds defined covered live capacity, not process RSS or
cumulative GC allocations.

The managed PCRE2 backtracking VM admits its fixed repeat/capture and optional
subroutine-restore vectors together before acquisition. Its eight growable state
stacks use the same invocation working ledger, including physical array headers,
exact capacity and old/new copy overlap. Nested assertion workspaces retain the
parent's loans. Partial acquisition and disposal restore all workspace loans;
spent work remains spent. Subroutine-restore arrays remain absent when that
grammar is unused. Acquisition and bounded stack copies check the current
deadline and spend shared work. The shared exact pool bounds retention across
core and adapter types, including multiple simultaneous leaves of one type and
capacity; finite heap calls cannot grow retained pool storage.
The former PCRE2-only stack helper in the core is retired. This qualifies VM
workspace payloads only: projection/result vectors, other adapter kernels,
complete public working/retained authority and full cancellation coverage remain
separate migration gates. Heap bytes do not certify process RSS or GC traffic.

PCRE2 backtracking capture materialization now shares that VM ledger. Assertion
capture vectors remain admitted through their parent's merge or negative result;
failure and cleanup return them without work refunds. Final group arrays and any
heap endpoint staging are admitted together before acquisition, while VM vectors
remain live. Endpoint sorting, group loops and scalar-safe projection windows
spend shared work and check deadlines. Temporary staging returns to the bounded
pool; final arrays remain charged through VM exit and detach when the result is
published. Detached arrays stay immutable and valid across later operations and
cursor advancement. PCRE2's public result-byte policy separately admits cumulative
major publication/staging before allocation, including these capture arrays;
see `SPEC-PCRE2.md` for coverage and `ResultLimit` failures. Publication does not
establish an external live-retention limit. Complete combined working/retained
policy and uncovered producers remain separate qualification work.

Shared slice capture-slot pairs admit both complete exact arrays, including
headers, before acquisition; failure and release restore that reservation.
PCRE2 slice detailed matches and cursor capture re-queries use that same batch
projector, borrowing the slice invocation's storage, work, deadline and token.
Their zero-based capture slots and reversed lookbehind edges normalize directly;
no one-based slot copy or per-capture prefix scan is needed. Group and endpoint
arrays are admitted alongside live slice slots. The final group loan remains
live through slot cleanup and detaches only on publication. Controlled sorting
and projection windows observe live cancellation and the existing deadline,
without restarting either counter. Other facade/core projection producers,
external retained-result limits and complete public policies remain open.

Shared slice backtracking and NonBacktracking capture histories admit complete
root and leaf array extents before acquisition. Backtracking history counting
and open-edge scratch use the bounded exact pool, with their loans held alongside
live runner storage and the final histories. Balancing compaction admits old and
new leaves together until its bounded copy completes. Returned byte histories
retain their loans until the consumer releases or transfers them; their arrays
are never returned to a pool. Derived UTF-16 capture projection separately admits
its summary vectors and complete history graph, including group zero, while
source histories remain live. This conversion preserves scalar capture edges
and ordering. Work cutoffs, cancellation and deadlines restore parent loans
without refunding spent work. Shared backtracking repeat/head vectors and nested
assertion repeat pairs also admit complete aggregate array extents. Chunk growth
reserves the new table and chunk together while the old table remains live, then
copies in bounded blocks before publishing. Denied growth leaves its parent
unchanged. Work, cancellation and deadline failures preserve their vocabulary.
NonBacktracking thread/register/pending/visited vectors, persistent scalar
boundary/viability tables and replay openings fund complete exact array extents;
empty vectors use shared arrays. Invocation-derived boundary maps separately
admit their offset and boundary-bit arrays while caller loans remain live.
Workspace/ownership descriptors and unqualified producers remain separate
accounting work. Public copied lifetimes and live-retained result limits remain
separate gates. Invocation result-capacity traffic is described in Section 3.7;
these producer loans do not bound process RSS.

## 6. Performance and benchmarking

Performance rules:

* production paths must stay UTF-8-native; do not route normal execution through `Encoding.UTF8.GetString(...)` + built-in `Regex`
* search and full matching are distinct concerns; search-first execution is expected
* prefer BCL vectorized primitives first, then `SearchValues<T>`, then manual intrinsics only when measured
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
