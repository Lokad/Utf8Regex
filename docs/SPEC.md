# Core contract

The **0.5.0 release candidate** targets .NET 10 and .NET 11. See
[release notes](../CHANGELOG.md) for release status and [migration](MIGRATION.md)
for upgrade guidance. This contract describes supported behavior; the
[API snapshot](../tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt) and executable
fixtures establish the implemented surface.

Core matching/output operations use one signature per supported input/template form. IsMatch and
Count accept optional cancellation and working allowance. A null allowance uses
the engine numeric-policy route; non-null shared storage refusal is terminal and
covered loans refund synchronously. Numeric limits/deadlines still apply.
Match accepts optional byte start, caller-owned capture slots, cancellation,
working allowance and end bound. Null start selects zero forward or the selected
end for RTL; default whole-match requests keep their existing execution paths.
Span enumeration also uses a direction-aware nullable byte start. Splits default
to int.MaxValue segments; explicit zero also means unlimited. Live tokens retain
the original invocation through every advance. These APIs do not validate subjects.

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

Byte offsets and lengths are primary. `EnumerateMatches(input, startOffsetInBytes,
cancellationToken)` starts at a scalar-aligned byte position, retaining the original
subject for anchors, boundaries and lookbehind. Starts outside the subject or in a
UTF-8 continuation byte throw `ArgumentOutOfRangeException` for `startOffsetInBytes`.
The original token, deadline and cumulative limits apply through every advance.
This replaces `EnumerateMatchesFromUtf16Offset` in 0.5 development. Offset
existence uses `Match(input, startOffsetInBytes, captures, cancellationToken).Success`;
All `FromUtf16Offset` matching APIs and UTF-16 result coordinates are removed.
Whole-subject `Match` and span enumeration use the existing `Utf8CaptureRange`.

`CountFromUtf16Offset` is also removed. Whole-subject `Count` remains. To count from
a byte start, consume one `EnumerateMatches(input, startOffsetInBytes, token)`
enumerator. Its original subject context, scalar progress, deadline and resource
budget remain cumulative. Partial counts use the iterator's matching scratch;
the general counter and iterator have not yet been consolidated.

### Caller-owned capture slots

The byte-start `Match` returns a `Utf8CaptureRange` and fills caller-owned slots
with the last participating capture of each group. Slot zero is the whole match;
`GroupNumberFromCaptureSlot` resolves each compact ordinal without allocating a
metadata array. Sparse group numbers do not require sparse buffers. Empty or short
buffers do not change matching or capture selection. Entries beyond the defined
slots remain untouched. A nonparticipating group has a default range; a
participating empty group has `Success == true` and equal start/end bytes.

`endOffsetInBytes` optionally defines the matching end. It must be a scalar
boundary at or beyond the start. Prefix context remains available, and all ranges
refer to original bytes. End anchors and lookahead observe the bounded end.
The caller supplies valid, unchanged UTF-8 for the original subject; matching
does not validate the subject or change that contract when an end bound is used.

This operation does not create coordinate maps, captured strings or capture-history
snapshots. Use the memory cursor for capture history. Matching scratch is admitted
before allocation against numeric and optional caller-owned working allowances,
then refunded synchronously. Caller-owned output is outside engine-owned result
storage, so a result-byte limit does not charge its existing capacity. Numerical
work/storage needs can differ from the removed UTF16-start existence operation.
It retains the existing bounded literal search for eligible families of up to eight
alternatives; capture-bearing and other programs use their owned scalar executor.
Boolean matching, slot matching and memory cursors also share Count's necessary
exact-prefix search when the existing instruction-level start filter cannot prove
a candidate. Every survivor is verified by the scalar executor, preserving alternative priority,
captures and original-subject context. Search uses the original cumulative
invocation, including cancellation, deadline and resource limits. Skipping
impossible starts can reduce the required work allowance.

For capture history from a byte start, use `CreateMatchCursor(memory, start,
cancellationToken)` and `MoveNext(out Utf8RangeMatch)`. Dispose an abandoned cursor.
Returned byte ranges and histories survive cursor disposal; borrowed subject memory
must remain valid and unchanged. A first-match caller can dispose immediately after
the first advance. This replaces `MatchDetailedFromUtf16Offset`; it avoids its dense
coordinate map and retains original-subject lookbehind/anchor context. Numeric
working/result requirements follow cursor scratch and snapshot publication.
Core `MatchDetailed`, `Utf8MatchContext`, `Utf8GroupContext` and
`Utf8CaptureContext` are removed. The memory cursor is the capture-history API
for whole subjects as well as byte-start searches. `Utf8RangeMatch.Value` holds
the whole byte range; `TryGetGroup` and `GetCaptures` expose last captures and
closing-order histories. Metadata is available from the compiled regex even on
a miss; the cursor returns a default result on a miss, with no group catalog.

No match or a failure after matching entry clears the defined output window.
Pre-cancellation leaves it unchanged. Interrupted publication clears the whole
defined window; extra caller entries remain untouched. A caller-funded `Utf8Regex`
retains program admission during the operation; a closed expression
rejects entry without modifying output. Returned ranges need no producer lifetime.

## Input and pattern language

Runtime support keeps the admitted language stable. .NET 11's new
`RegexOptions.AnyNewLine` is unsupported and rejected. Vertical tab remains a
literal in verbose patterns. Previously rejected conditional option forms stay
rejected, and the private `_xmlC`/`_xmlD`/`_xmlI`/`_xmlW` sets retain their frozen
.NET 10 memberships. BCL comparisons use equivalent patterns or independent
expectations where .NET 11 changed behavior; they do not redefine this contract.

### Subject input contract

Every operation and cursor requires already-valid UTF-8 for the entire subject,
with unchanged borrowed storage during use. Callers validate external bytes where
they become text, for example with `System.Text.Unicode.Utf8.IsValid`; an existing
text representation may already guarantee validity.

Core, PythonRe, PCRE2 and POSIX use this same contract. Regex operations do not
validate subjects or expose a validation policy/token. Malformed subjects have
unspecified matching results and no rejection or error-offset promise. Managed
bounds checking and the same useful work/depth/storage limits remain active.
Pattern, capture-name, replacement-template and assembled-output validation keep
their separate strict rules.

Scalar boundaries, byte offsets, captures, RTL/NonBacktracking behavior, retained
results/cursor aliases, original synchronous cancellation and absolute deadlines
are unchanged. Native byte discovery and the general VM avoid unrelated suffix
preparation when no live eligibility or algorithm state needs it. Some routes
still inspect ASCII/scalar facts, and NonBacktracking retains its scalar viability
table. These are matching requirements, not subject-validity passes. No subject
path computes UTF-16 totals or coordinate maps. Removed validation/description
work can lower numerical limits; charges remain version/route dependent.

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

Positive, case-sensitive classes containing exactly two scalars, with at least
one non-ASCII member, can reuse complete UTF-8 literal-pair plans. Encoded storage
is admitted at construction; discovery and publication share the original
execution authority. Numerical work and VM scratch requirements can decrease.

Forward plain alternatives can omit a frame when a required exact ASCII literal
disproves one branch at the current position. Inspection skips at most 32 capture
markers per branch; viable alternatives retain their original order and capture
effects. Opaque branches keep normal verification. Balancing, assertion/atomic
frames and RTL retain their existing behavior. Avoided frames can lower work,
depth and working requirements; needed state remains admitted before acquisition.

`NonBacktracking` is a separate owned linear mode for its admitted regular
subset. It preserves ordered choice, greedy/lazy behavior, scalar empty progress
and each group's last completed capture. Unsupported grammar fails before planning.
Nullable cycles and zero-width repeats must terminate without bound-sized
expansion. This mode does not expose ordinary backtracking capture history.

Backtracking first-match search can reuse Count's mandatory exact ASCII head or
range proof. Controlled gap searches cover at most 2048 bytes per checkpoint;
rejected neighbouring bytes still consume work. Every surviving start runs the
normal VM with captures and rollback. Nullable, contiguous-anchor, folded and RTL
roots retain their existing discovery, as do initial beginning anchors. Skipping
impossible starts can reduce work and avoid local scratch; enclosing storage loans,
final interruption/admission still
apply.

A complete LTR backtracking root containing only a word-boundary instruction and
Accept can omit general VM dispatch when capture history is not requested. It
shares the VM's complete-neighbor predicate, including profile-specific word sets.
Scalar-start checks, initial workspace admission/reset, entry/final work charges
and original token checks remain. Deadline polling keeps its existing step cadence;
operation/cursor publication retains its original checks. No extra state or
allocation is introduced. RTL, NonBacktracking and extended roots keep their paths.

Boolean/count/value matching share the same conservative unobserved-capture proof.
Eligible owned backtracking programs omit capture slots and their trails when
captures cannot affect matching. Backreferences, conditions, lookarounds, balancing,
shared/explicit declarations, NonBacktracking and alternate/unknown programs retain
their capture slots. In an assertion-free latest-value run, plain capture starts
need undo entries only while a frame can restore an earlier value. Closing entries
retain multi-group completion order; a sole slot needs them only for rollback.
History, assertions, opening journals and balancing retain their required entries.
For one unique capture, references after its local close can use the completed
slot without an opening journal. References before closure or inside its body,
external assertion slots, shared declarations and other excluded shapes retain
the separate opening history. Capture values and required rollback remain intact.
Detailed and cursor results retain all captures. Eliminated bookkeeping can reduce work
and working-byte requirements; other VM buffers, original interruption and result
admission remain in force.

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

Ordered literal-window whole-pattern execution is retired, including its Count,
separator, lookahead and cursor shortcuts. Existing scalar programs determine
greedy/lazy gap widths, branch priority, captures and byte ranges against the
original subject. Shared literal and candidate-window searches remain filters;
they do not establish a complete match. Execution cost and numerical work, depth
and covered storage can increase. Admission before major allocation and original
synchronous cancellation/deadlines remain required.

### Native pattern metadata

`CaptureGroupCount` counts defined capture slots excluding group zero. Sparse
numbers and repeated declarations of a shared slot do not inflate it; captures
need not participate in a match. For ordinals zero through `CaptureGroupCount`,
`GroupNumberFromCaptureSlot` returns actual group numbers in compact slot order,
preserving sparse and shared identities. The copied-array `GetGroupNumbers` is
removed; callers allocate collections when needed, outside regex controls. Each
metadata access retains its open-producer funding guard. Separate calls do not
form an atomic snapshot across program close; retained match results keep their
own lifetime contract.

`TryCopyGroupNameUtf8(captureSlot, destination, out bytesRequired, token)` exports
one slot's name without a string array or a program-storage alias. Visit the same
compact ordinals; unnamed slots use their actual decimal group number. The former
`GetGroupNames` clone API is removed. `Done` writes the complete name;
`DestinationTooSmall` reports its full UTF-8 size without writing. Cancellation
can leave a partial copy. Slot bounds are checked after original-token and funded
access checks; names come from validated canonical metadata. This explicit copy
uses the same ownership and interruption contract as pattern copying below.
The string-key `GroupNumberFromName` and string-returning `GroupNameFromNumber`
are removed. Use `GroupNumberFromUtf8Name` for valid UTF-8 keys and enumerate
compact slots for name export. Unknown byte keys return -1; named/sparse aliases
and dense numeric parsing keep their existing rules. Text-key encoding and host
collections are caller work. Parser-internal string resolution remains separate.

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

`Utf8CaptureRange` is storable byte-range data without an input owner. Default
means no match: `Success` is false and both byte endpoints are zero. A successful
empty match has `Success` true and equal start/end endpoints. Endpoints are always
readable. Whole-match enumeration and `Match` use this same type; there is no
alignment flag, optional byte-range projection or UTF-16 coordinate state. Native
and scalar cursors retain only byte positions, and never scan gaps to derive
character coordinates. Cursor copies retain independent positions while sharing
the original cumulative invocation limits. Published ranges survive advance,
failure and cursor exhaustion. Removing coordinate preparation can decrease
modeled work, working and result-capacity needs.
Capture cursors publish immutable byte-range snapshots without UTF-16 coordinate
maps or captured strings. Capture storage is admitted before allocation and
publication observes the original cancellation, deadline and cumulative limits.
Modeled budget needs follow cursor scratch and snapshot storage, and can differ
from the removed detailed views. A cursor timeout retains pattern and timeout
identity but omits decoded input from the exception.

Borrowed views require unchanged subject bytes and a live owner. Returned
histories and owned snapshots remain valid after later discovery, denial or cursor
closure. Split preserves count/empty/RTL ordering and does not insert delimiter
captures as extra elements. `Utf8ValueSplit` stores only byte start and length;
it has no UTF-16 coordinates or alignment flag. Its bytes form complete scalar
segments. Controlled scalar splitting consumes the existing match cursor's byte
outcomes without a subject coordinate map or UTF-16 totals. Count one retains
only the original invocation and returns the whole subject.
Removing split maps lowers modeled working admission; match-state buffers and
bounded work, cancellation and deadline checks remain. Views stay valid after
EOF or interruption while their original input remains unchanged and alive.
Publishing a borrowed split view allocates no result payload and therefore spends
no result-byte capacity. A host's explicit `GetValueString` decoding is outside
that capacity, like other borrowed-view getters.

Replacement uses .NET-style `$1`, `${name}`, `$&`, prefix/suffix, `$+`,
`$_` and `$$` forms with the supported numeric parsing rules. RTL discovery
assembles output in subject order. UTF-8 template overloads parse/cache source
bytes directly; no whole-template string is required. Selected capture-name
lexemes may project text under the existing name grammar. Prepared instructions
retain byte literals and original syntax flags, without retaining parser tokens,
literal text or a duplicate source. Each string literal is encoded independently: an unpaired
surrogate becomes U+FFFD and cannot join a fragment from another substitution.
Subjects are caller-valid; template bytes remain strict.

General-pattern replacements use the existing scalar byte
template engine for both allocated and caller-owned output. They select that
engine before subject preparation and do not construct UTF-16 match/group views
or repeat subject validation. Literal replacement paths retain their existing
search and output implementations. Existing derived coordinate maps remain for
legacy detailed-match results until their separate migration.

Core's evaluator replacement overloads, evaluator delegates and `ReplaceToString`
are removed in 0.5 development. Hosts compute custom substitutions over the byte capture cursor.
Scalar templates use last-capture byte ranges without callback history or subject maps.
Scalar byte/destination output does not request whole-input UTF-16 totals. Hosts decode
returned bytes when text materialization is needed; that allocation and its cost
belong to the host rather than the regex invocation's output budget.
The former shared `Utf8ReplacementWriter` is removed now that all evaluator APIs
are retired. Byte-template output keeps its owned scope and admitted buffers;
there is no thread-local callback writer or implicit output-scope discovery.

`TryReplace` returns `Done` or `DestinationTooSmall`. Shortage writes zero
bytes and leaves the destination unchanged. Input/template overlap is supported;
overlapping input stages output. Other failures throw, and interruption during
the final copy can leave partial destination contents.

## Cancellation, deadlines and resource limits

Explicit token overloads observe pre-cancellation before validation and preserve
the original token in `OperationCanceledException`. Cooperative checks cover
bounded input inspection, search, matching, copying, output and publication.
A long failed advance must check before reporting EOF. Tokens belong to the
invocation, never the reusable program/cache.

One absolute deadline covers an invocation and its lazy advances, including waits
between moves and controlled cold preparation. Timeout throws
`RegexMatchTimeoutException`. A finite working or result byte cap makes its
`Input` empty, avoiding an unadmitted diagnostic decode. Work/depth-only and
unlimited calls retain their existing diagnostic input. Scheduling and GC/JIT
cannot be preempted.

Core/PythonRe accept positive nullable `Utf8ResourcePolicy` values; null is
unlimited and zero is invalid. Defaults are unlimited.

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
The shared backtracking VM uses small fixed frame, capture-trail and repeat-journal
chunks. Short searches and captures need less initial capacity; deep runs acquire
more chunks, headers and growth checkpoints. Every covered acquisition still
requires admission first.
Capture-trail chunks now hold eight entries. Brief captures reserve less, while
long histories can require more chunk headers and growth work. Working requirements
can therefore rise or fall with this version even when captures are unchanged.
Root repeat state is acquired only when execution needs mutable counters. Fused
runs can fit tighter working limits, including Count when captures are unused.
Bounded greedy Dot repeats with a proved terminal ASCII literal can select their
last scalar endpoint without repeat rewinds. Original captures and interruption
checks still execute; work, depth and working requirements can be lower.
Counter-dependent execution still admits the complete vectors before allocation;
the invocation retains them across retries and releases them synchronously.
Assertion children retain their separate storage lifetime. Numerical work and
working requirements can change; counter-heavy paths can cost more time.
The invocation checks its numeric working ceiling before caller-funded admission;
live reservations, overlap and the first denial remain shared across aliases.
Native shortcuts still meter bounded search, comparison and publication.
When capture history is unobserved, a complete forward root comprising an ASCII
literal prefix and one outer capture of a terminal positive ASCII interval `+`
can reuse the admitted repeat runner. Two nonnested positive ASCII interval `+`
captures can share that runner when a nonempty exact ASCII literal separates
them and its first byte is excluded from the first interval. The whole-root proof
inspects at most64 instructions once at program construction and retains one
boolean eligibility fact; existing workspace/slots remain initialized,
both multi-group closing entries retain their admission and completion order,
and slots publish only after complete success and interruption checks. Basic
literal, repeat-block and publication metering remains. Removed dispatch can
lower numerical work totals. Repeated-root orchestration uses ordinary VM-step
checks rather than additional forced deadline clock reads. Explicit original-token
checks remain, and the repeat runner checks deadlines per bounded byte block and
at each completed repeat; public publication checks retain the original authority.
This clock-read omission changes neither work charges nor storage/result admission.
A complete root containing one capture of a positive, unfolded ASCII set can
similarly omit VM setup and capture-marker/Accept dispatch. It keeps the existing
scalar decoder and set predicate, admitted workspace/slots, and charged entry and
publication checks. It has one consuming endpoint and no competing closing order;
numerical work can decrease without changing memory admission or returned captures.
History, assertions/required ends, nested or repeated captures, folding, lazy or
bounded repeats, RTL/NonBacktracking and opaque roots retain their current paths.
Proved unbounded scalar repeats excluding exactly two ASCII values can scan in
checked byte spans of at most 2048 bytes per work/deadline checkpoint. Whole-subject
The caller supplies valid UTF-8; ASCII stops and final endpoints
stay on scalar boundaries. Minimums above one, finite maxima and folding retain scalar-counted
execution; paths requiring rewind retain their existing handling. Work charges
can fall; no scratch storage is added.
An unbounded Dot without Singleline uses the same bounded scan with LF as its
only stop when its minimum is zero or one and the existing endpoint proof allows
commitment. Capture instructions and final interruption checks still execute.
A complete greedy Singleline Dot/end tail can commit EOF when no assertion
requires another endpoint. For unbounded minimum0/1 runs, the same checkpoints
advance bounded byte blocks without rewind storage; lazy and consuming tails
retain their choices. Numerical work, depth and working requirements can fall.
Direct candidate windows for unbounded minimum-one repeats excluding an ASCII
pair can also rewind in checked blocks of at most 2048 bytes. Delimiter discovery
and full VM verification remain; scalar floors and original interruption are
preserved. Avoiding per-scalar rewind checks can lower required work steps.
The bounded no-rewind proof can look through capture openings for direct literal,
dot and unfolded Set atoms; folded/table predicates keep their earlier route.
Capture endings retain their existing eligibility. The markers still execute with
their original writes, journals and completion order at the selected endpoint;
no capture is elided. An excluded following atom makes shorter endpoints useless
independently of those boundaries. Two unfolded ASCII Sets use checked64-bit
intersections, with one work charge per inspected half, instead of per-member
class dispatch. Overlap retains rewind behavior. Removed exploration/proof work
can lower required work/depth and working capacity. The same32-marker cap,
required assertion ends, scalar matching, original interruption and allocation
admission remain.
Literal-search blocks own their work charges; entering the search helper adds
no separate work unit for a nonempty admitted plan. Empty plans and prior resource
failures retain their checkpoint, and terminal success/miss checks still observe
the original token and deadline. Candidates without boundary or trailing-literal
requirements omit an empty confirmation step; their terminal publication remains
charged. These shortcuts can lower finite work requirements.
Controlled exact single-literal searches through32 bytes can search complete
needles within admitted spans of at most 2048 bytes, including lookahead. They
retain pre-scan and terminal token/deadline checks, without separately charging
every first-byte candidate. Numerical work requirements can therefore fall;
larger and folded needles retain separately admitted comparisons.
Pure LTR exact families of three to eight needles can reuse complete-needle search
in admitted spans of at most 2048 bytes when no boundary or trailing-literal
confirmation is required. Needles above1024 bytes keep the existing generic path.
The same invocation charges eligibility, each bounded scan and publication;
avoiding false-candidate replay can reduce work, while dense hits can cost more.
Caller validation, enclosing regex verification and original interruption remain.
Eligible finite whole-value `Match` calls admit up to eight exact alternatives
through the same bounded discovery and projection without VM working storage.
Offset maps and detailed capture results retain their separate admission.
Already-prepared families of three or four distinct heads, with needles of 3–16
bytes, can share the ordinary checked search inside each admitted window.
Complete verification precedes recovery of the original branch ordinal. One
combined scan replaces separate per-needle charges; working/result admission,
bounded interruption and terminal publication retain the original invocation.
Raw forward exact pairs can combine both short-needle scans into one checked
primitive of at most 2048 bytes. Complete verification stays inside that bound;
the shared short-pair primitive can return its verified branch ID without another
literal comparison before candidate publication.
Ordered branch confirmation and final publication still use the original
invocation. Combining scans can reduce numerical work charges, without changing
depth, working/result admission or retained-result lifetimes. Longer, folded,
reverse and unsupported-hardware paths keep their existing bounded scans.
Finite-policy and caller-funded `Count` also admit four-to-eight complete literal
alternatives to this existing bounded search. Avoiding VM scratch can lower work,
depth and working requirements. Result-producing operations retain their own admission and native eligibility.
General patterns, including bounded classes and captured finite branches, use
owned scalar execution. Work, depth and working-storage requirements depend on
the selected program; retiring a shortcut can increase them. Real storage is
admitted before acquisition. Native counters classify only the subject facts
they consume, under the original work, cancellation and deadline authority.
Subjects are caller-valid UTF-8; no validity pass or UTF-16 metadata is required.
Scalar Count can search an exact ASCII delimiter after a proved leading
class run, with or without intervening whitespace. The actual scalar predicate
rewinds a conservative candidate window; the VM verifies every retained start.
For a direct class/literal prefix, only the actual class is rewound, including
whitespace that belongs to it. Explicit whitespace/gap prefixes retain their
conservative union. Omitting impossible starts across nonmember whitespace can
lower numerical work requirements without changing allocation admission.
Skipped starts need no VM work or scratch. The caller-valid subject contract,
bounded search/rewind checkpoints and admission before live VM storage remain.
A backwards class step proves membership at the rewound start. When it makes
no progress, bounded classification can discard only the unproved window prefix;
every remaining viable start retains full verification. Fully rejected windows
advance to the next delimiter before VM admission. Avoided attempts can lower
numerical work, depth and working requirements; classification still spends work
under the original token and deadline.
Mandatory anchors can precede Count's ASCII start filter; the VM still checks
their full original-subject context. Skipped starts can lower required work/storage.
Scalar-class and unfolded literal repetitions with a proved endpoint can omit
unused repeat-journal storage. Live counter vectors and captures still require
admission; examined scalars/classifications and terminal interruption remain metered.
Lazy pure-atom repeats can also omit rollback when a following required literal
excludes every consumed scalar: no shorter endpoint can match that literal.
Unconstrained lazy roots and overlapping tails retain their normal choices.
Greedy pure-atom repeats before an atomic commit can omit shorter choices that
the commit would discard. Capture endings, the atomic marker and commit still
execute; examined scalars remain metered. Working, depth and work requirements
can decrease. Lazy atomic repeats retain their distinct endpoint order.
Positive unfolded contiguous ASCII sets in these no-rewind, unbounded repeats
(minimum zero or one) use checked range searches over blocks of at most 2048 bytes.
Each block and final publication spend basic work/check the original interruption
authority, including unlimited calls. Endpoints remain scalar boundaries. No
local buffer is introduced; numerical work thresholds can be lower than the
per-scalar loop. Other set shapes, bounded repeats and paths needing rewinds keep their rules.
Complete capture-free greedy atom repeats, including nullable ones, can also omit
unused counter vectors. Empty-match advancement and nonempty retries retain their
profile rules; required work/working capacity can be lower than earlier builds.
Pure literal runs can lower working and depth requirements; repeats requiring
rewinding or captures inside the body retain their continuation frames.
Greedy class runs before two exact literal alternatives can omit rewinds when
both first literals are excluded by the class. Capture-marker inspection is
bounded; normal alternatives and capture effects still execute. Avoided journal
storage can lower working requirements, while declined proofs can increase work.
The same greedy proof can use a required positive, unfolded ASCII set, directly
or at the start of a nonempty simple repeat. It checks at most128 following
scalars against the current profile's exact predicate under the original work,
token and deadline authority. Capture effects still execute. Working/depth needs
can decrease while proof work increases; nullable or uncertain followers retain
normal backtracking.
Nonempty bounded runs before a positive word boundary can also omit useless
rewinds when a bounded ASCII input check proves every shorter endpoint lies
between word scalars. This uses the profile's actual word predicate and can stop
at an ASCII nonword scalar excluded by the repeated class. The proof
is charged; successful admission can require less working capacity and depth,
while declined proofs can increase work. Zero-minimum and uncertain runs keep
their existing continuations.
Template replacement reads final completed captures without materializing capture
histories. Avoiding temporary snapshots can lower work and covered working/result charges; execution,
output staging and final output remain under their original admission and
interruption controls.
ASCII literal-family cursors publish byte coordinates directly. Search and
per-match interruption checks remain; no subject coordinate map is built.
Boundary-wrapped literal alternations, including shared prefixes, use literal
analysis and the same emitted literal-family factory. Its existing eligible
fused counter combines ASCII eligibility and counting; it does not validate UTF-8.
Unicode neighbors, ordered prefix retries, RTL and controlled operations retain
their applicable scalar boundary and interruption rules.
Complete literal discovery can also use a proved positive literal suffix,
confirmed under the same bounded comparison budget without consuming the suffix.
Native counters admit their fixed scratch capacity against working bytes before
use and record active nesting against depth. A Count or value-Match shortcut can
avoid capture workspace; a declined shortcut retains its spent work when scalar
execution resumes.

Nonliteral identifier families use their existing scalar program for all public
operations, including finite-policy counting and cursors. They no longer prepare
a separate ASCII verifier, classify subjects to select that verifier, or retain
family cursor state. Their helper-only construction charges disappear; major
program/search buffers and instruction/state expansion retain construction admission.
Scalar execution can require more work, depth or scratch than the retired counters.
Search, matching and publication retain one original work/token/deadline scope.
Literal search/emission and shared candidate-window searches remain independent.

Nonliteral facade shortcuts (URI/IP, identifier/query/reference validators,
word/category and delimiter counters) are retired. These languages execute the
existing scalar program, including finite and caller-funded operations. Modeled
work, depth and working/result capacity follow that program and may increase;
previous constant-space kernel cutoffs are not API guarantees. Scalar extents,
captures, RTL/NonBacktracking, original synchronous cancellation/deadlines and
major-buffer admission remain required. Anchored exact/folded literals keep their
qualified byte searches and case-fiber exclusions. Genuine optimizations inside
the scalar VM remain part of that program.

The scalar counter for a complete capture-free greedy literal or contiguous
ASCII range repeat can skip nonmembers through checked span search. Active runs
retain their original minimum/maximum splitting. Controlled searches charge
before each block of at most 2048 bytes and preserve the original token,
deadline and final publication check. Subjects satisfy the caller-valid text contract. No capture workspace
or result payload is introduced.
Nonmember work charges can fall from per-scalar steps to bounded scan units.
Unproved classes, folding, RTL and NonBacktracking retain their existing paths.

A complete greedy set repeat with a positive minimum (for example
`[A-Za-z]{8,13}`) can Count scalar runs without VM repeat/capture arrays. The
caller-valid input contract, one work charge per examined scalar and final
publication checks remain. Complemented/folded sets retain scalar membership; positive
unfolded sets can use their existing ASCII mask. Tight working/depth/result
limits can admit this constant-space route. A bounded proof over at most 64
instructions can ignore plain capture markers around or inside that repeat for
Count, which publishes no capture values. Actual capture-returning operations
retain their original execution and storage. Lazy, nullable, trailing, RTL and
NonBacktracking shapes retain their existing paths. Matching semantics
are unchanged; route-dependent workspace/depth and work charges can decrease.

Token-window, disjoint repeated-segment and quoted-relation whole-pattern plans
are retired, including the finite repeated-segment Count shortcut. These patterns
use existing scalar execution. Work, depth and covered storage follow that program
and may increase, including on misses. No constant-space or exact numerical
cutoff is promised for these shapes. Scalar extents, captures, retained results,
original-token/deadline interruption and major-buffer admission remain required.

Count can reject impossible mandatory ASCII prefixes before acquiring local VM
continuation storage. The bounded prefix filter accepts no match: surviving
candidates still run the scalar verifier. Finite misses can require less working
capacity; actual VM execution retains its admission, work and original
cancellation/deadline scope. Captures and control-flow effects remain VM decisions.

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

String shaping reuses a known UTF-16 length when available. An unknown complete
valid UTF-8 range of at most4096 bytes can instead admit the string capacity for
`byteLength` UTF-16 units before decoding. This conservative upper bound avoids a
separate sizing pass; short Unicode output can therefore require more modeled
working/result bytes than its actual UTF-16 length. Larger unknown ranges retain
bounded exact sizing/decoding. Original interruption checks surround the bounded
decode; publication/refund and cumulative result-traffic rules are unchanged.

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
storage through that allowance. Subjects are caller-valid unchanged UTF-8; the
original token/deadline cover matching. Covered loans refund on completion or failure.
Numeric work/depth/working/result limits remain independently enforceable. Fixed
invocation metadata, borrowed input and the compiled expression are separate.
Funded timeout diagnostics omit subject text rather than decoding it without
admission. Retained literal searches and scalar execution keep bounded metering.

Core constructors and `Compile` accept one immutable `Utf8RegexPolicy` for match
limits/timeout, construction limits/timeout and optional `ProgramMemoryAllowance`.
Both funded and unfunded factories return `Utf8Regex`; the separate
`Utf8RegexProgramOwner` is removed. Null match timeout uses DefaultMatchTimeout;
null construction timeout is infinite. Compile has a separate construction token,
checked before policy/pattern work and not retained for matching. Default
constructors inherit internal admission; explicit Compile/construction/funding
controls own one scope and cannot replace an active owner. Text construction and
Compile default to CultureInvariant; UTF8 Compile retains its None options default.

Funded creation admits covered compilation before acquisition and prepays the
bounded replacement cache (4MiB payload plus 64KiB metadata) before publication.
This is conservative capacity, not eager cache allocation; construction/host byte
allowances must cover it. Dispose rejects new uses and refunds after the last
admitted call or persistent cursor. Original controls remain authoritative for
admitted work. Unfunded Dispose is a no-op. Pattern/options/timeouts/resource policy
remain readable; program-dependent metadata retains its open-gate requirement.

Shared working admission is available on IsMatch, Match, Count, CreateMatchCursor
and UTF8-template TryReplace. Null inherits program funding when present; an
explicit account overrides working admission only. Span enumerators, owned-byte
Replace and text-template TryReplace retain numeric work/working/result limits
without a host working account. Program funding still protects their active calls.
Caller retention remains outside operation admission.

Copied span match/split enumerators keep independent positions and original
shared numeric authority. A funded span borrows an open regex: creation and each
advance hold program admission, with original cancellation checked before the
closed gate. Close between advances rejects future advances on every copy;
retained Current values remain valid under the unchanged input lifetime. They
hold no host working loan between advances and need no disposal/finalizer lease.
Use a persistent memory cursor for shared working funding or traversal surviving
regex close. PCRE2 retains its own funded program owner, byte-range memory cursor
and retained receipts under the [profile contract](SPEC-PCRE2.md).

`Utf8Regex.TryReplace(input, replacementUtf8, destination, out written,
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
on Core expressions returns `Utf8RegexMatchCursor`. Null start
selects zero for LTR or the subject end for RTL; explicit starts are scalar-aligned
bytes within the original subject, preserving assertion context. The cursor borrows already-valid unchanged text without a validity pass. Byte discovery and capture
histories need neither captured strings nor a whole-subject UTF-16 map. NonBacktracking
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

Core exposes instance operations over expressions owned by the caller. Static
cached matching/replacement operations are removed in 0.5 development, together
with their private global program cache. Replacement preparation and adapter
payload caches retain conservative bounds on complete immutable keys/values;
shared storage can be charged more than once. Cache bounds are not process or
total-program memory bounds.

Core constructors/Compile accept construction policy/timeout through Utf8RegexPolicy;
Compile retains a separate construction token. Other profiles use their documented
creation forms. Construction pre-cancellation wins, nested preparation shares
the owner, and authority is not retained for execution. Ordinary constructors
keep fixed backstops; positive construction values tighten them, null keeps them.
The profiles' caller-funded creation forms delegate byte admission to
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

Lowering can complete the ASCII membership of an eligible positive, unfolded
general-category class in its existing program mask. It admits the bounded
128-value classification as construction work, and any replacement mask before
allocation, without mutating source masks. Construction work/covered bytes can
increase while later scalar table lookups decrease; wider scalars retain their
original predicates. Folded, negated, range, predicate and class-program cases
retain their existing preparation.

Cold replacement preparation has separate construction backstops under the
original invocation token/deadline. Finite working bytes additionally cap covered
cold parser/key capacity; a denial can be `ConstructionBytes` before execution.
Warm text and byte keys skip cold parsing. Native hits borrow the caller span;
misses admit/copy immutable byte keys under the same construction owner as parser
capacity. Text/native entries share one bounded cache with independent keys,
allowing earlier eviction. Retained values contain only the lowered byte plan,
literal payload and original flags. Shared payload slots still count again in
conservative cache charges; no identity collector is needed. Eliminating unused
preparation or compacting plan descriptors can lower charged work and capacities.
Construction remains a conservative capacity backstop, including discarded
intermediate storage.
Native source extents use bytes, so admission/work requirements may differ from
legacy decoded-key charges. PythonRe funded hits keep their existing active uses;
Funded Core expressions retain the conservative cache-capacity reservation.
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

Nonliteral simple-pattern execution, its standalone fixed-token programs and
whole-pattern validators are removed. These patterns use the owned scalar VM. This
can increase work or working-storage requirements for a given numerical limit.
Admission, original-token cancellation, deadlines, scalar semantics and byte
capture ranges still apply to the selected program.

Production execution is owned and managed: parse/analyze/prepare, immutable
program, invocation runtime. No BCL matcher/result engine, native imports or
unchecked memory reads are shipped. Actual `Unsafe.SizeOf<T>()` is permitted.
`Compiled` affects strategy only. General patterns currently use the owned scalar
interpreter; the removed shape-specific IL emitters are not a general compiled
backend. Literal-family emission remains. Disabled dynamic code uses existing
non-emitted execution. NativeAOT/trimming are not qualified.

General-engine programs do not construct a second specialized runtime or verifier.
Removing that unused preparation can lower modeled construction requirements;
pattern/state/buffer admission and execution limits retain their existing scope.

Native search is an optimization of the same scalar semantics. Eligibility must
prove full grammar/options/capture/branch order, scalar widths, assertion context,
case fibers and progress. Failure declines the optimization, not the grammar.
See [architecture](../src/Lokad.Utf8Regex/ARCHITECTURE.md) and
[search complexity](../src/Lokad.Utf8Regex/Internal/Search/COMPLEXITY.md).

Qualify scalar/boundary, grammar, captures, RTL/NonBacktracking, malformed-input,
interruption, admission and lifetime behavior through public fixtures. Benchmark
only equal-result workloads with warm throughput against both strict-decode and
predecoded controls. Historical measurements are not current qualification:
[benchmark summary](../bench/Snapshots/Core.json).
