# Core contract

The **0.5.0 development source** targets .NET 10 and .NET 11. See
[release notes](../CHANGELOG.md) for release status and [migration](MIGRATION.md)
for upgrade guidance. This contract describes supported behavior; the
[API snapshot](../tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt) and executable
fixtures establish the implemented surface.

## Matching and coordinates

Core matches complete Unicode scalars over strict UTF-8. Dot consumes one scalar
subject to newline options; quantifiers repeat atoms, and combining marks remain
separate scalars. No normalization or implicit grapheme matching occurs.

| Operation | Required result |
| --- | --- |
| Dot on `😀` | One match, byte range 0/4 |
| Dot on `a😀b` | Byte ranges 0/1, 1/4, 5/1 |
| Empty pattern on `😀` | Two matches, at byte boundaries 0 and 4 |
| `^(.)\1$` on `😀😀` | Success; group 1 contains the complete first scalar |
| `a\|aa` on `aa` | `a`, following ordered alternatives |

Captures, references, lookarounds, RTL traversal and empty-match advancement
respect scalar boundaries. Greedy/lazy and alternative order apply at each
viable start. RTL changes search direction, not alternative priority.
POSIX leftmost-longest and POSIX subcapture selection are not implemented.

Byte offsets and lengths are primary. UTF-16 coordinates and string APIs are
derived interoperability. Explicit `FromUtf16Offset` entry points reject an
offset inside a supplementary scalar with `ArgumentOutOfRangeException`.
Offset resolution retains the complete subject's assertion context and the same
invocation controls.

## Input and pattern language

Runtime support keeps the admitted language stable. .NET 11's new
`RegexOptions.AnyNewLine` is unsupported and rejected. Vertical tab remains a
literal in verbose patterns. Previously rejected conditional option forms stay
rejected, and the private `_xmlC`/`_xmlD`/`_xmlI`/`_xmlW` sets retain their frozen
.NET 10 memberships. BCL comparisons use equivalent patterns or independent
expectations where .NET 11 changed behavior; they do not redefine this contract.

The entire subject must be well-formed UTF-8, including a suffix after an early
match. Malformed, overlong, surrogate-encoding and truncated input throws
`ArgumentException` with an offending byte offset. UTF-8 pattern and replacement
inputs are strict too. Interruption or resource denial can occur before a later
invalid byte is visited; successful matching cannot bypass validation.

Core admits documented .NET-style syntax: literals/classes/subtraction, inline
options, greedy/lazy repetition, captures/history, references, balancing groups,
conditionals, lookarounds, contiguous anchors, RTL and the supported
`ECMAScript` behavior. Options use `RegexOptions`. Unsupported combinations or
syntax fail during construction. BCL parsing/matching is not an execution fallback;
BCL exception messages and UTF-16 matching defects are not the contract.

Adjacent raw or escaped high/low surrogates join into one scalar within a literal
run or class member. Separate groups/assertions/classes do not join them.
Unpaired surrogate atoms cannot match strict UTF-8; zero repetitions remain
possible, and negation admits the valid scalar complement. Class ranges use
numeric scalar order through U+10FFFF, excluding the surrogate gap.
`[\u0000-\uffff]` covers BMP scalars; singleline dot covers every plane.
`Utf8Regex.Compile(ReadOnlySpan<byte>, ...)` parses strict UTF-8 directly through
the same owned scalar grammar. The call borrows its source; retained source and
literal bytes are admitted and copied before publication. Reusing the caller's
buffer afterwards cannot change the program. Both ordinary and caller-funded
program forms support independent construction/execution controls. Native
compilation does not retain a whole-pattern UTF-16 copy. `Pattern` is an explicit,
uncached text projection; its allocation belongs to the requesting caller.

Invalid syntax reports `ArgumentException` with a reason and a source offset:
`pattern`/UTF-16 for text, `patternUtf8`/UTF-8 bytes for native compilation.
Malformed bytes reject strictly; interruption/admission may precede a malformed
tail. Native construction timeouts keep the exception's `Pattern` empty rather
than decoding after expiry. Matching timeout diagnostics project text only on
failure. Representation/resource limits remain distinct from syntax.

Finite repeat maximum 2147483647 is distinct from infinity. Consuming expansion
requires instruction/state admission. Impossible classes and compact zero-width
repeats preserve syntax checks, predicates and capture declarations without
expansion proportional to the bound.

Backtracking zero-or-one repeats of compound bodies use ordinary ordered branches
rather than repeat counters and rollback mutations. Single-atom repeats retain
their existing fused kernels. Greedy/lazy preference, nullable captures,
assertions and RTL are preserved. Instructions, construction/work charges and
major working-buffer requirements can decrease; numeric costs depend on the
program version. Frames still require depth and working admission before growth,
and the original token/deadline governs execution and publication.

`NonBacktracking` is a separate owned linear mode for its admitted regular
subset. It preserves ordered choice, greedy/lazy behavior, scalar empty progress
and each group's last completed capture. Unsupported grammar fails before planning.
Nullable cycles and zero-width repeats must terminate without bound-sized
expansion. This mode does not expose ordinary backtracking capture history.

Scalar sizing admits one work quantum before at most 64 bytes, and exact offset
preparation one before at most 64 scalars. Viability preparation admits one before
at most 64 instruction cells per row. Scanning prepared candidate starts admits
one before at most 64 scalar-boundary lookups. Executed atom predicates retain their direct charges.
Packed viability rows reject a case-sensitive literal whose value differs from
the current scalar within the already admitted cell quantum, without a separate
failed-predicate charge. Feasible literals, folded and complex atoms, and wider
rows retain their existing charges. Numeric preparation costs can therefore fall.
Every quantum checks the original token and absolute deadline directly, and
publication checks them again. Working admission still covers scalar boundaries
and packed rows before allocation; storage and depth bounds are unchanged.
Preparation and candidate-scan work units are lower than earlier per-cell accounting;
finite limits remain enforceable, with CPU growth bounded by64 bytes/scalars/simple
cells per corresponding quantum plus separately metered atom work. This applies
to unlimited calls too.
Programs fitting one packed word precompute unconditional Fork/Jump/Capture
ancestry. Each row visits eligible consuming cells, folds successful ancestry,
then evaluates ordered anchor/boundary conditions. Wider programs keep the full
ordered evaluation. This ancestry folding changes no invocation work units or table capacity.
The immutable ancestry array uses construction admission before allocation;
cold costs and construction-limit thresholds can differ from earlier layouts.

Count recognizes a complete regular line shape from at most 64 lowered
instructions: beginning anchor, optional Unicode-whitespace star, a nonempty
case-sensitive ASCII literal whose first byte is not whitespace, greedy
non-newline dot star, and end anchor. Capture markers outside the loop bodies
are transparent to Count. Other grammars and all capture/cursor operations retain
their existing runners. The proof uses no new program metadata.

This Count path uses no viability table or local scratch. It admits every scan
of at most 2048 bytes, candidate verification of at most 64 instructions, and
backward whitespace block of at most 64 scalars, including unlimited calls.
Every quantum and final publication check the original token and deadline.
Original full-input anchors and scalar origins remain authoritative. Failed end
anchors retain overlapping newline-prefix candidates; the bounded prefix limits
repeated tail scans. Working admission observes enclosing loans with a zero-byte
local request. Work/working/depth thresholds can consequently be lower than table
execution; budget costs remain route/version dependent.

### Native pattern metadata

`CaptureGroupCount` counts defined capture slots excluding group zero. Sparse
numbers and repeated declarations of a shared slot do not inflate it; captures
need not participate in a match. `GetGroupNumbers` retains sparse identities.

`TryCopyPatternUtf8(destination, out bytesRequired, token)` copies exact original
source into caller storage without creating a source string/output buffer or
exposing a program alias.
`Done` writes `bytesRequired` bytes; `DestinationTooSmall` reports the full
required capacity without writing. Legacy text sources with unpaired surrogates
return `InvalidData`, zero required bytes and no writes. Cancellation preserves
the supplied token and may leave a partial copy. This explicit metadata operation
uses bounded token checkpoints, outside matching timeouts/numeric invocation
budgets. Caller storage remains the caller's admission/retention responsibility.
Funded owners expose both members, retain program funding during access and
reject access after close; successful copies survive owner disposal. Funded
metadata access allocates the existing managed use lease. Zero-allocation
measurements for ordinary expressions do not include that funded lease.

`GroupNumberFromUtf8Name(name, token)` validates the entire UTF-8 name and
returns its exact ordinal slot, or -1. Dense numeric-only catalogs preserve
leading-zero numeric lookup; named/sparse catalogs preserve their existing exact
keys. No normalization occurs. Keys are encoded and admitted once at compilation;
lookup scans the immutable key vector without allocating strings. Work is linear
in retained keys/compared bytes, with bounded token checkpoints. The funded owner
exposes the same lookup under its existing program use. Malformed names report
ArgumentException with a byte offset, even when a preceding prefix is unknown.
This explicit metadata work remains outside matching timeout/numeric budgets.

## Case and culture

`CultureInvariant` is the primary supported/performance-tested mode.
Culture-sensitive folding captures construction culture, including inline scopes
and cache identity; later ambient culture changes cannot alter the expression.

| I-cluster mode | Equivalent members |
| --- | --- |
| Invariant | I/i; İ and ı remain distinct |
| Non-Turkic culture | I/i/İ; ı remains distinct |
| Turkic culture | I/ı and İ/i |

Case-equivalence mappings use owned Unicode 16.0 data for BMP and supplementary
scalars. General categories/shorthands use runtime scalar properties.
[PythonRe](SPEC-PYTHONRE.md) owns its
separate Unicode 15.1 rules.

## Results, splitting and replacement

`Utf8ValueMatch` is storable coordinate data; default means no match, distinct
from a successful empty match. Unsuccessful values have zero UTF-16 coordinates;
`TryGetByteRange` returns false/zero, and byte properties throw without a range.
Default borrowed match/capture views expose empty values and zero coordinates.
An unsuccessful operation retains unsuccessful group zero; a default view has no
groups. Detailed results expose the owned capture history.

Borrowed views require unchanged subject bytes and a live owner. Returned
histories and owned snapshots remain valid after later discovery, denial or cursor
closure. Split preserves count/empty/RTL ordering and does not insert delimiter
captures as extra elements.

Replacement uses .NET-style `$1`, `${name}`, `$&`, prefix/suffix, `$+`,
`$_` and `$$` forms with the supported numeric parsing rules. RTL discovery
assembles output in subject order. UTF-8 template overloads parse/cache source
bytes directly; no whole-template string is required. Selected capture-name
lexemes may project text under the existing name grammar. Prepared instructions
retain byte literals and original syntax flags, without retaining parser tokens,
literal text or a duplicate source. Each string literal/callback return is encoded independently: an unpaired
surrogate becomes U+FFFD and cannot join a fragment from another substitution.
Subject/template bytes and completed byte-callback output remain strict.

Native discovery/capture-map eligibility is preserved. Grouped template expansion
splices original byte views through the admitted output writer; text results
decode only the final output. Scalar fall-throughs likewise emit the requested
byte/destination form directly. Existing derived coordinate maps remain where
native capture shaping requires them; this does not make UTF-16 the matching unit.

Evaluators must be non-null even on misses; failure names `evaluator`.
Pre-cancellation takes precedence. `Utf8ReplacementWriter` is callback-scoped;
copies share output, and resetting to default clears output before joining the
active invocation on append. Outside a callback, append throws even for empty
fragments; `AppendAsciiByte` validates its argument first. Scoped byte/char
span appends copy/encode before returning, so local stack buffers are supported.

`TryReplace` returns `Done` or `DestinationTooSmall`. Shortage writes zero
bytes and leaves the destination unchanged. Input/template overlap is supported;
overlapping input stages output. Other failures throw, and interruption during
the final copy can leave partial destination contents.

## Cancellation, deadlines and resource limits

Explicit token overloads observe pre-cancellation before validation and preserve
the original token in `OperationCanceledException`. Cooperative checks cover
bounded validation, search, matching, projection, copying, output and publication.
A long failed advance must check before reporting EOF. Tokens belong to the
invocation, never the reusable program/cache.

One absolute deadline covers an invocation and its lazy advances, including waits
between moves and controlled cold preparation. Timeout throws
`RegexMatchTimeoutException`. A finite working or result byte cap makes its
`Input` empty, avoiding an unadmitted diagnostic decode. Work/depth-only and
unlimited calls retain their existing diagnostic input. Scheduling, GC/JIT and
arbitrary caller callbacks cannot be preempted; checks surround callbacks.

Core/PythonRe accept positive nullable `Utf8ResourcePolicy` values; null is
unlimited and zero is invalid. Defaults and static cached calls are unlimited.

| Policy | Covered scope | Denial kind |
| --- | --- | --- |
| `MaxWorkSteps` | Cumulative bounded engine/checkpoint work | `ExecutionWorkSteps` |
| `MaxDepth` | Live backtracking/continuation frames, logical assertion depth, pending linear alternatives or native nesting records | `ExecutionDepth` |
| `MaxWorkingBytes` | Conservative major active capacities, including growth overlap | `ExecutionWorkingBytes` |
| `MaxResultBytes` | Cumulative modeled result/staging capacity, admitted before acquisition without refunds | `ExecutionResultBytes` |

Denial throws `Utf8ResourceLimitExceededException` with kind, limit and requested
total. Totals saturate, and first denial stays sticky across copies. Denial is not
no-match or partial success. Numerical charges depend on route/version; they are
not byte counts, CPU instructions, elapsed time or portable per-pattern minima.
The invocation checks its numeric working ceiling before caller-funded admission;
live reservations, overlap and the first denial remain shared across aliases.
Native shortcuts still meter bounded search, comparison and publication.
Literal-search blocks own their work charges; entering the search helper adds
no separate work unit for a nonempty admitted plan. Empty plans and prior resource
failures retain their checkpoint, and terminal success/miss checks still observe
the original token and deadline. Candidates without boundary or trailing-literal
requirements omit an empty confirmation step; their terminal publication remains
charged. These shortcuts can lower finite work requirements.
Pure LTR exact families of three to eight needles can reuse complete-needle search
in admitted spans of at most2048 bytes when no boundary or trailing-literal
confirmation is required. Needles above1024 bytes keep the existing generic path.
The same invocation charges eligibility, each branch/window and publication;
avoiding false-candidate replay can reduce work, while dense hits can cost more.
Caller validation, enclosing regex verification and original interruption remain.
Scalar Count can search an exact ASCII delimiter after a proved leading
class run, with or without intervening whitespace. The actual scalar predicate
rewinds a conservative candidate window; the VM verifies every retained start.
Skipped starts need no VM work or scratch. Whole-subject strict validation,
bounded search/rewind checkpoints and admission before live VM storage remain.
Mandatory anchors can precede Count's ASCII start filter; the VM still checks
their full original-subject context. Skipped starts can lower required work/storage.
Scalar-class and unfolded literal repetitions with a proved endpoint can omit
unused repeat-journal storage. Live counter vectors and captures still require
admission; examined scalars/classifications and terminal interruption remain metered.
Complete capture-free greedy atom repeats, including nullable ones, can also omit
unused counter vectors. Empty-match advancement and nonempty retries retain their
profile rules; required work/working capacity can be lower than earlier builds.
Pure literal runs can lower working and depth requirements; repeats requiring
rewinding or captures inside the body retain their continuation frames.
Nonempty bounded runs before a positive word boundary can also omit useless
rewinds when a bounded ASCII input check proves every shorter endpoint lies
between word scalars. This uses the profile's actual word predicate and can stop
at an ASCII nonword scalar excluded by the repeated class. The proof
is charged; successful admission can require less working capacity and depth,
while declined proofs can increase work. Zero-minimum and uncertain runs keep
their existing continuations.
Template replacement reads final completed captures without materializing capture
histories. Evaluator callbacks retain their profile's histories. Avoiding those
temporary snapshots can lower work and covered working/result charges; execution,
output staging and final output remain under their original admission and
interruption controls.
Validated ASCII literal-family cursors reuse byte coordinates for UTF-16
projection. Avoided gap scans consume no validation-work units; search and
per-match interruption checks remain.
Complete literal discovery can also use a proved positive literal suffix,
confirmed under the same bounded comparison budget without consuming the suffix.
Eligible controlled ASCII Boolean calls reuse the complete simple-branch search
with its bounded checkpoints, without scalar capture/backtracking workspace.
Full strict subject validation still precedes the decision. Unsupported shapes,
mixed Unicode input, RTL and NonBacktracking keep their existing paths. The
controlled invocation owner remains; this is not a general zero-allocation claim.
For example, the fixed predicate `^[A-Z]{3}[0-9]{4}$` keeps this Boolean path
with a live caller token and finite numeric limits. Its fixed invocation metadata
still allocates. The separate caller-funded entry point can use scalar scratch;
that storage remains admitted and refunded independently of program ownership.
Native counters admit their fixed scratch capacity against working bytes before
use and record active nesting against depth. A Count or value-Match shortcut can
avoid capture workspace; a declined shortcut retains its spent work when scalar
execution resumes.

On fully validated ASCII subjects of at most 2048 bytes, a proved zero-origin
operator or quoted-string prefix can publish its value-Match range through the existing
bounded prefix helper. The original invocation meters discovery and publication,
including cancellation/deadline checks. Wider or mixed subjects, nonzero origins,
RTL/NonBacktracking and detailed captures retain their existing paths. A tight
working/result limit can therefore admit the range without VM capture workspace;
this changes route-dependent resource cost, not matching semantics. Quoted-string
value matching does not materialize captures; detailed matching keeps all scalar
capture histories and escape/newline semantics.

The same ASCII size envelope can find a complete identifier token of the form
`[A-Za-z][A-Za-z0-9]*` from zero origin using the existing checked finder. Its
selected start can be nonzero; byte and derived UTF-16 coordinates retain that
index. Discovery and publication share the original bounded work/token/deadline
scope without VM repeat/capture arrays. This can reduce working/depth admission;
mixed Unicode, larger subjects, nonzero origins, RTL/NonBacktracking, global
IgnoreCase and unproved shapes keep their existing paths. Detailed captures are
unchanged.

A proved anchored identifier prefix, such as invariant
`\G[a-z][a-z0-9_]*` with IgnoreCase, can use the same ASCII size envelope and its
existing prefix helper. The construction-captured culture must produce the
complete ASCII head/tail masks; variants with different masks retain scalar
execution. Unicode subjects, including Kelvin/Turkic scalars, retain scalar
folding. Validation, bounded interruption/publication and detailed captures stay
unchanged; this route can avoid VM workspace and nesting records.

A complete greedy star excluding one ASCII scalar (for example `[^\n]*`) can
use zero-origin span Count by searching for that byte after strict whole-subject UTF-8 validation.
The byte cannot occur inside a wider scalar. Scans cover at most 256 bytes per
work/interruption checkpoint and carry nonempty runs across block boundaries;
the original invocation also checks final publication. Empty matches still
advance one scalar. This constant-space counter needs no VM capture workspace,
so tight working/result/depth limits can admit it. Work charges are bounded scan
units rather than VM instructions. RTL, NonBacktracking, ECMAScript, folded and unproved
grammar shapes keep their existing routes; detailed results are unchanged.

Greedy unbounded word repeats with a positive minimum, optionally enclosed by
word boundaries and ordinary captures, can Count fully validated ASCII through their existing whole-pattern
proof. That counter needs no capture workspace and admits one work unit before
each bounded 32-byte scan plus publication; the original token/deadline cover
validation and counting. Mixed UTF-8 retains scalar membership and the same spent
invocation authority. Lazy/finite-repeat and extended-class shapes, RTL,
NonBacktracking and ECMAScript retain their admitted owned paths. Tight working/depth/result policies can
therefore admit a constant-space count that formerly required VM scratch; this
changes route-dependent resource costs, not matching semantics.

A complete case-sensitive single-category expression or proven Unicode-letter
expression (`\p{L}`) can Count using the existing category/letter kernels after
full strict validation, including caller-funded Count. One work unit admits at
most 4096 bytes ending on a scalar boundary; publication has its own checkpoint.
The original token/deadline cover validation and counting, including supplementary
scalars. This constant-space route needs no capture workspace or nesting records;
tight working/depth/result limits can therefore admit it. RTL, NonBacktracking,
ECMAScript and unproved compound expressions retain their owned routes; folded
single-category expressions also retain their scalar route. Letter Count now
uses this existing block admission rather than VM instruction charges, so its
route-dependent work and working-storage costs can be lower.

A complete greedy set repeat with a positive minimum (for example
`[A-Za-z]{8,13}`) can Count scalar runs without VM repeat/capture arrays. Full
strict validation, one work charge per examined scalar and final publication
checks remain. Complemented/folded sets retain scalar membership; positive
unfolded sets can use their existing ASCII mask. Tight working/depth/result
limits can admit this constant-space route. Captured, lazy, nullable, trailing,
RTL and NonBacktracking shapes retain their existing paths. Matching semantics
are unchanged; route-dependent workspace/depth and work charges can decrease.

An uncaptured, case-sensitive finite repeat of disjoint classes, such as
`(?:[A-Z][a-z]+\s*){10,100}`, can Count through its existing prepared plan after
a complete-root check. The leading and trailing classes must be ASCII; the
original separator predicate retains Unicode membership. Full strict validation
precedes counting. Work admission covers at most 2048 search bytes, 64 trailing
ASCII bytes or 64 separator scalars per quantum; non-ASCII separator predicates
retain their direct atom charges. Original-token/deadline checks include final
publication. No local VM arrays or frames are needed, while zero-byte working
admission still checks enclosing loans and the first denial. Work/depth/working
costs can therefore fall; other grammars and capture/cursor operations retain
their existing implementations and lifetimes.

Bounded literal assertions can avoid child VM storage while still admitting
their logical assertion depth under the original work, token and deadline scope.

Copied value cursors have independent positions but share allowance/deadline and
published map/table reservations; concurrent advancement is unsupported.
Exclusive scratch releases synchronously. Published reservations are not refunded
at EOF or GC, and the numeric ledger does not root abandoned payloads.
No finalizer lease, alias registry or GC-dependent refund is required.

Required capture/projection arrays, maps, output controls, staging/final roots and
growth overlap are covered. Fixed scalar values, borrowed subjects/programs,
incidental helpers, standalone getters, arbitrary callback allocations and host
retention are excluded. Shared/temporary/final storage may count again.
The limits do not bound total heap/RSS. PCRE2 uses its own
[limit names and failure vocabulary](SPEC-PCRE2.md#interruption-and-resource-controls).

PythonRe's range cursor and range split publish byte metadata without captured
text or a coordinate map. They retain the same preallocation working/result
admission; immutable range/name arrays are owned and subject bytes are borrowed.
This reduces requested output capacity, without converting result traffic into
a live-retention bound or claiming a shared caller-owned allowance.

PythonRe's explicit shared-working cursor overload accepts `IUtf8MemoryAllowance`
from the host. The existing ledger reserves before covered acquisitions and
refunds exclusive storage after users finish. Shared refusal is a distinct
`Utf8MemoryAdmissionException` and cannot relax numeric controls. The interface
alone does not fund other API families or caller-owned output retention; each
accepting API defines its covered lifetime.

Core `IsMatch(input, token, workingMemoryAllowance)` and
`Count(input, token, workingMemoryAllowance)` admit modeled temporary matching
storage through that allowance. Strict whole-subject validation and the original
token/deadline precede matching; covered loans refund on completion or failure.
Numeric work/depth/working/result limits remain independently enforceable. Fixed
invocation metadata, borrowed input and the compiled expression are separate.
Funded timeout diagnostics omit subject text rather than decoding it without
admission. Native literal, word and category counters keep bounded metering.

Core's `Compile(..., token, programMemoryAllowance)` returns a disposable
`Utf8RegexProgramOwner`. It admits conservative compilation capacities before
acquisition and transfers their charge to explicit ownership. The owner offers
Boolean matching, Count, byte-template destination replacement and persistent range cursors, using its program allowance for working storage by
default; the optional working allowance selects a separate host child account.
The private expression cannot escape the owner. Dispose rejects new calls and
refunds compilation after the final active call/cursor; active users retain their
program reference and finish under their original controls. This new owner keeps
legacy copied span-enumerator lifetimes separate. PCRE2 has its own funded program owner, byte-range memory cursor
and retained receipts under the [profile contract](SPEC-PCRE2.md).

`Utf8RegexProgramOwner.TryReplace(input, replacementUtf8, destination, out written,
token, workingAllowance)` uses strict native templates and caller-owned output.
The program's prepaid conservative cache maximum covers retained keys/plans.
Cold key/parser capacity borrows the invocation's existing working ledger and
stays admitted through output, including uncached plans. Preparation and execution
therefore share host admission and numeric live-capacity overlap, without a
refund/reacquire gap. Covered temporary loans refund synchronously, even when
preparation publishes a cached plan before execution fails. Warm reuse can need
less working capacity. Construction work/depth/instruction and 64MiB preparation
backstops remain; cold preparation retains the existing construction-byte refusal
vocabulary. Work/depth/result controls also apply during matching and output.

This funded destination API uses the existing controlled scalar replacement
route, including native literal candidate discovery, with the original deadline
and token; it does not promise ordinary unlimited grouped-worker throughput.
RTL, NonBacktracking, capture expansion, aliases and short-destination behavior
are preserved. The destination belongs to the caller and remains valid after
program close. No additional retained output receipt or string convenience is
required for this route.

`CreateMatchCursor(ReadOnlyMemory<byte>, startOffsetInBytes, token, workingAllowance)`
on Core expressions and program owners returns `Utf8RegexMatchCursor`. Null start
selects zero for LTR or the subject end for RTL; explicit starts are scalar-aligned
bytes within the original subject, preserving assertion context. The cursor
validates the entire unchanged subject once. Byte discovery and capture histories
need neither captured strings nor a whole-subject UTF-16 map. NonBacktracking
retains its existing scalar viability table and last-capture semantics; other
Core paths preserve all capture histories, sparse numbers and repeated names.
Sequential calls can cross awaits, under one absolute deadline and cumulative
work/result traffic. Each MoveNext accepts an additional current-call token;
original-token cancellation wins if both are canceled. Completed advance tokens
are not retained. Aliases share position and disposal; concurrent/reentrant access
is rejected. EOF, failure and explicit Dispose drop exclusive scratch/program
roots and refund their loans synchronously. Dispose early abandonment; no finalizer.

`MoveNext(out Utf8RangeMatch)` publishes immutable ranges, histories and shared
name/number metadata. Metadata survives later advances and cursor/program close;
subject bytes and their memory manager remain borrowed. Ordinary publication ends
working admission for the result: continued caller retention has separate scope.
`MoveNextRetained` instead returns `Utf8RetainedRangeMatch`, requiring shared working
funding. It conservatively charges shared metadata/name text again for an independent
lifetime. By default the already admitted staging loan transfers unchanged. A
separate result allowance reserves before staging is refunded, so that handoff
admits overlap without an accounting gap. Refusal is terminal. Closing a receipt
refunds exactly once; previously copied range values remain usable, with continued
retention then the caller's admission responsibility. Borrowed input is not charged
again. Numeric result traffic remains cumulative even when live loans refund.

Native subject/range discovery, pattern compilation, caller-buffer source/name
metadata and byte-template preparation/cache paths are implemented. Core funded
caller-destination replacement and the profile-specific PCRE2 equivalent are
implemented; final package/consumer acceptance remains
separate parts of the complete UTF-8 public pipeline.

### Host admission

For a Core/PythonRe instance with finite working cap W and result cap R, a host
can reserve `checked(W + R)` before entry or cursor creation. Refuse entry if a cap
is unlimited, addition overflows, or host admission fails. Covered active working
capacity is at most W; covered result/staging acquisition is cumulatively at most R.
Their union is conservatively bounded by W+R, even when storage counts twice.
A refused Requested total can exceed a cap without acquiring that allocation.

Hold the reservation until all covered results and permitted cursor aliases are
gone or transferred to another admitted host owner. Return, EOF and disposal do
not authorize a refund while owned snapshots survive. Each new call needs separate
admission; configure work, depth, timeout and cancellation independently.

Fund programs/preparation, subjects, caches/pools, incidental objects, standalone
getters, callbacks and host containers/copies separately. Borrowed memory stays
alive and unchanged. Host admission before engine entry needs its own deadline.
This recipe covers modeled payloads, not exact heap/RSS, total allocations or a
PCRE2 envelope; it does not establish downstream adoption. Source witnesses are
`Utf8ExecutionBudget.TryChargeResultBytes`, `Utf8InvocationResultLedger.ReleaseSince`
and `Utf8PythonMatchCursor.MoveNext`, with finite-result/working/cursor fixtures.

## Construction, preparation and caches

Explicit `Compile` accepts separate construction policy, timeout and token;
pre-cancellation wins, nested preparation shares the owner, and authority is not
retained for execution. It does not consult static caches. Ordinary constructors
keep fixed backstops; positive construction values tighten them, null keeps them.
The three profiles' caller-funded program overloads instead delegate byte admission to
their shared allowance, with an optional additional `MaxBytes`; the other
backstops below remain. See [program lifetime](SPEC-PYTHONRE.md#construction-and-execution-controls).

| Fixed construction backstop | Ceiling |
| --- | ---: |
| Instruction slots | 1,048,576 |
| Covered bytes | 64 MiB |
| Work units | 67,108,864 |
| Live frames | 65,536 |

Construction denial uses `ConstructionWorkSteps`, `ConstructionDepth`,
`ConstructionInstructionCapacity` or `ConstructionBytes`. Construction
timeout uses `RegexMatchTimeoutException`, empty Input, and the supplied text
Pattern (empty for byte-pattern diagnostics).

Admission precedes major buffers, instruction/state expansion and growth/copy
overlap. Work/checkpoints bound parsing, traversal, maps/sorting, decoding and
emission. Charges use actual `Unsafe.SizeOf<T>()` strides and conservative
headers/rounding; helper-object/hash-probe precision is not an objective.
Failure unwinds unpublished capacity/depth without refunding spent work or
invalidating borrowed objects. CLR/JIT metadata and complete heap accounting
are excluded.

Cold replacement preparation has separate construction backstops under the
original invocation token/deadline. Finite working bytes additionally cap covered
cold parser/key capacity; a denial can be `ConstructionBytes` before execution.
Warm text and byte keys skip cold parsing. Native hits borrow the caller span;
misses admit/copy immutable byte keys under the same construction owner as parser
capacity. Text/native entries share one bounded cache with independent keys,
allowing earlier eviction. Retained values contain only the lowered byte plan,
literal payload and original flags. Shared payload slots still count again in
conservative cache charges; no identity collector is needed. Construction remains
a conservative capacity backstop, including discarded intermediate storage.
Native source extents use bytes, so admission/work requirements may differ from
legacy decoded-key charges. PythonRe funded hits keep their existing active uses;
Core program owners retain their existing conservative cache-capacity reservation.
Preliminary byte-template scans admit work before reading through the construction
owner or fixed scan ceiling. Interrupted preparation is retryable and never
published. Ordinary policies do not combine construction/execution/retention.
PythonRe's caller-funded Compile additionally funds replacement preparation,
cache/active-plan retention and default execution/result staging through its
program account; see its
[lifetime contract](SPEC-PYTHONRE.md#construction-and-execution-controls). This
does not change Core/PCRE2's ordinary cache scopes. PythonRe retained range and
UTF-8 template substitution receipts can explicitly take over their admitted
result loan; other returned output remains caller-owned after publication.

Caches use conservative per-program charges, separate metadata/payload caps and
eviction; shared storage may be counted twice. Eviction/clear/shrink preserve held
programs/results and cancellable preparation coordination. No retained-object
graph, identity deduplication or transactional manifest is required.

## Implementation and qualification

Production execution is owned and managed: parse/analyze/prepare, immutable
program, invocation runtime. No BCL matcher/result engine, native imports or
unchecked memory reads are shipped. Actual `Unsafe.SizeOf<T>()` is permitted.
`Compiled` affects strategy only; disabled dynamic code uses existing
non-emitted execution. NativeAOT/trimming are not qualified.

Native search is an optimization of the same scalar semantics. Eligibility must
prove full grammar/options/capture/branch order, scalar widths, assertion context,
case fibers and progress. Failure declines the optimization, not the grammar.
See [architecture](../src/Lokad.Utf8Regex/ARCHITECTURE.md) and
[search complexity](../src/Lokad.Utf8Regex/Internal/Search/COMPLEXITY.md).

Qualify scalar/boundary, grammar, captures, RTL/NonBacktracking, malformed-input,
interruption, admission and lifetime behavior through public fixtures. Benchmark
only equal-result workloads with warm throughput against both strict-decode and
predecoded controls. Historical measurements are not current qualification:
[benchmark snapshots](../README.Benchmarks.json).
