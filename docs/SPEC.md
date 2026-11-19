# Core contract

Version 0.5 targets .NET 10 and .NET 11. See
[release notes](../CHANGELOG.md) for release status and [migration](MIGRATION.md)
for upgrade guidance. This contract describes supported behavior; the
[API snapshot](../tests/Lokad.Utf8Regex.Tests/PublicApi.Shipped.txt) and executable
fixtures establish the implemented surface.

Core matching/output operations use one signature per supported input/template form.
`IsMatch` and `Count` accept optional cancellation and a working allowance. A null
allowance inherits the program account when present. Numeric limits and deadlines
apply independently of shared admission.
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
then refunded synchronously. A whole-match-only request can omit capture scratch
when the prepared program proves matching can proceed without it.
Caller-owned output is outside engine-owned result
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
the whole byte range. `GroupNumbers` identifies the numeric slots, and
`TryGetGroup(number, ...)` and `GetCaptures(number)` expose last captures and
closing-order histories. Resolve names on the compiled regex before disposal.
Read name metadata from the compiled regex independently of match success.

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
still inspect ASCII/scalar facts, and NonBacktracking batches can prepare a scalar
viability table. These are matching requirements, not subject-validity passes. No subject
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

Leading ASCII range facts are computed from the lowered instructions at
construction. Count, first-match discovery and cursors reuse them while each
invocation still owns candidate scanning and full match verification.

`NonBacktracking` is a separate owned linear mode for its admitted regular
subset. It preserves ordered choice, greedy/lazy behavior, scalar empty progress
and each group's last completed capture. Unsupported grammar fails before planning.
Nullable cycles and zero-width repeats must terminate without bound-sized
expansion. This mode does not expose ordinary backtracking capture history.
Its persistent cursor snapshots the last completed slots directly and charges
the snapshot arrays to the cumulative result budget.
For compact graphs, `IsMatch` can carry the active state set in a single mask.
It evaluates anchors and boundaries at their original scalar edges before
reusing admitted ASCII consuming transitions. Each advance spends work and
checks interruption. A tight working cap can run the same state calculation
without caching. Ordered execution supplies match ranges and captures.
NonBacktracking operations that only return whole-match ranges or counts
can use ordered execution without reserving capture registers. Requests for
subcaptures still admit their working state and retain the same priority.
Compact unconditional graphs can reuse ordered transitions across matching
scalars in a whole-range search. Byte origins stay in invocation scratch.
The cache stores which earlier thread supplied each surviving origin.
When the initial literals or positive ASCII masks prove a small complete set
of leading bytes, Boolean and ordered search can skip impossible starts in
bounded, metered blocks while no earlier path is active.
Folded proofs include the existing construction-culture case equivalents.
Active paths and their priority keep the original scalar execution. A candidate
still needs full scalar verification, including when scalars share a lead byte.
On longer first-match calls, an eligible compact graph can select the range
before reserving subcapture storage. The existing anchored replay fills the
subcaptures inside that range using the same work budget and deadline.
Short selected ranges can reuse packed viability rows and slot replay with
bounded, admitted stack scratch. Longer ranges use the ordered runner.
Persistent cursors for these graphs can return their first result before
preparing viability rows. Forward-only compact graphs reuse one admitted window
with bounded scalar lookahead across subsequent results. Graphs with cycles
can prepare a whole-subject table. Both use the original cursor budget and
deadline. First-match misses and bounded final tails can finish without preparing
rows. A refusal during a later advance faults the cursor and leaves earlier
results valid.
Count reuses the same window for longer subjects with these forward-only graphs.

Backtracking first-match search can reuse Count's mandatory exact ASCII head or
range proof. Controlled gap searches cover at most 2048 bytes per checkpoint,
and rejected neighbouring bytes still consume work. Every surviving start runs the
normal VM with captures and rollback. Nullable, contiguous-anchor, folded and RTL
roots retain their existing discovery, as do initial beginning anchors.
When an unbounded prefix class excludes its direct delimiter, and matching
does not read captures, one complete verification can cover the window.
This includes one-byte delimiters. Successful matches retain their capture
histories. Skipping impossible starts can reduce work and avoid local scratch.
An aligned delimiter followed immediately by two mandatory, unfolded hybrid
class runs can rule out a window before rewinding. The suffix needs at least
two scalars, and its second scalar must belong to one of the runs. After a
rejection, search resumes one byte after the delimiter's start so overlapping
occurrences remain eligible.
For direct unbounded classes with a complete ASCII predicate, the backward
scan can rewind up to 64 ASCII bytes per work charge. It decodes wider scalars
at their original right edges and retains the caller's byte floor.
Enclosing storage loans and final interruption and admission checks still apply.

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
no result-byte capacity. Callers can decode the segment returned by
`GetValueBytes()` in their own code and account for the resulting text there.

Replacement uses .NET-style `$1`, `${name}`, `$&`, prefix/suffix, `$+`,
`$_` and `$$` forms with the supported numeric parsing rules. RTL discovery
assembles output in subject order. `Replace` and `TryReplace` take strict
UTF-8 templates. Preparation parses and caches bytes directly. Selected capture-name
lexemes may project text under the existing name grammar. Prepared instructions
retain byte literals and original syntax flags. The cache keeps the lowered plan
and the admitted template key.

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
For a large literal template with a nonoverlapping destination, scalar matching
can retain admitted match ranges and publish directly after sizing. Its resource
charges cover the retained ranges and bounded publication work.

## Cancellation, deadlines and resource limits

A pre-canceled token wins before invocation validation and program access.
Cooperative checks cover bounded input inspection, search, matching, copying
and publication, including a failed advance before reporting EOF.
`OperationCanceledException` carries the token that triggered it. Invocation
tokens never become part of the compiled program or a cached plan.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8Regex("cat|horse");
regex.IsMatch("cat horse"u8, canceled.Token); // throws with canceled.Token
```

One absolute deadline covers a call, its cold replacement preparation and
waits between cursor advances. It is enforced cooperatively, with no
preemption of scheduling, GC or JIT work. Timeout throws
`RegexMatchTimeoutException`. Its `Input` is empty with a finite working
or result byte limit, on host-funded matching paths, and for memory cursors.
Other Core calls can include the decoded input in the diagnostic.

### Numeric limits

`Utf8ResourcePolicy` fields accept positive values. Null leaves that field
unlimited and zero is invalid. All fields default to unlimited.

| Policy | Covered scope | Denial kind |
| --- | --- | --- |
| `MaxWorkSteps` | Cumulative modeled engine work, including bounded scans and publication | `ExecutionWorkSteps` |
| `MaxDepth` | Live backtracking, continuation or assertion state, including logical depth | `ExecutionDepth` |
| `MaxWorkingBytes` | Live major scratch capacities, including growth overlap | `ExecutionWorkingBytes` |
| `MaxResultBytes` | Cumulative modeled result and staging acquisitions | `ExecutionResultBytes` |

Denial throws `Utf8ResourceLimitExceededException` with the resource kind,
limit and requested total. Totals saturate and the first denial remains in
effect across cursor copies. A denied acquisition never publishes a partial
successful result. Charges depend on the route and version. For example,
a proved repeat endpoint can avoid acquiring backtracking frames, and a
proved candidate filter can avoid VM work before verification.

A lookaround body consisting of one side-effect-free scalar class can run
without allocating a continuation. Its logical depth, directional edge and
interruption checks still apply.

A forward greedy repeat of one side-effect-free scalar atom can keep its
remaining endpoints in a single live frame. The run scan and each retry are
metered. Depth and working admission cover the frame, while capture and
assertion state retain their own admission. When the remaining instructions
prove an exact terminal literal, the engine can find the last reachable
occurrence in bounded, metered blocks and execute the tail without a rewind
frame. The usual capture and result admission applies at that endpoint.
When a bounded continuation must reach a terminal end anchor, the engine
can discard a saved repeat exit that is too far from the end. Its rollback
and interruption checks still run. A lax anchor allows a final LF, and a
multiline anchor can still succeed earlier in the subject.

A scalar program that cannot match empty and has a mandatory terminal anchor
can rule out an impossible final ASCII scalar before acquiring match scratch.
A lax anchor considers both the input end and the position before a final LF.
The matcher verifies possible endpoints, including those with a non-ASCII final scalar.

The engine admits covered storage before acquisition. It covers capture
and projection arrays, cursor tables, output staging and finalized result
storage. A shared or temporary allocation may count more than once.
Fixed scalar values, borrowed inputs and programs, caller destinations,
incidental helpers, standalone getters and host retention fall outside these
limits. Caps bound modeled capacities rather than process heap or RSS.
PythonRe uses the shared policy with its [profile-specific scopes](SPEC-PYTHONRE.md#construction-and-execution-controls).
See the [PCRE2 vocabulary and mapping](SPEC-PCRE2.md#interruption-and-resource-controls)
for its distinct failures.

### Program and working allowances

Set `ProgramMemoryAllowance` on `Utf8RegexPolicy` for shared admission of
covered compiled program storage. Core reserves conservative capacity for its
bounded replacement cache before publishing a funded program (4 MiB payload
and 64 KiB metadata). This reservation does not eagerly allocate the cache.
Disposal closes a funded program to new calls and refunds storage after
admitted calls and persistent cursors finish. Unfunded disposal is a no-op.
Pattern, options and policy properties remain readable after disposal.
Program-dependent metadata operations require an open funded expression.

`IsMatch`, `Match`, `Count`, `CreateMatchCursor` and `TryReplace` accept a
working allowance. Null inherits the program account if present. An explicit
account overrides working admission only. Span enumerators and owned-byte
`Replace` retain numeric limits and hold program admission while executing.
The numeric working limit is checked before shared working admission. Covered
storage requires both. A shared refusal throws `Utf8MemoryAdmissionException`
and the operation does not retry without funding. Temporary loans refund
synchronously on completion, failure or interruption.

Cold `TryReplace` preparation uses the same working allowance and absolute
deadline as matching and output. Covered key and parser capacity stays funded
through output, including for an uncached plan. The program account covers
retained cache capacity. Warm reuse can need less working storage. The existing
[replacement contract](#results-splitting-and-replacement) governs caller buffers
and interruption during the final copy.

### Cursor and result lifetimes

Copies of span match and split enumerators have independent positions and share
numeric limits and the original deadline. Concurrent advances are unsupported.
Creation and each advance borrow an open funded regex with no shared working
loan between calls. Original cancellation precedes the closed-program check.
Close rejects later advances on every copy. Returned ranges and views remain
usable while the borrowed subject lives unchanged. Exclusive scratch is refunded
synchronously. Numeric reservations for shared published state persist for the
invocation without keeping abandoned payloads alive.

A memory cursor can cross calls or awaits. It retains admitted program use
through regex close, and useful working state counts against its working limit
until released. Aliases share position and disposal. Concurrent or reentrant
access is rejected. The creation token, deadline and cumulative work/result
traffic continue to apply. An optional advance token applies to that advance
only, with the creation token winning when both are canceled. EOF, failure and
explicit disposal release exclusive scratch and program use synchronously.
Dispose an abandoned cursor.

`MoveNext` returns immutable byte ranges and group metadata that survive cursor
advance, failure and closure. The subject and its memory manager stay borrowed.
A group-zero-only snapshot uses one stored capture for `Value` and `GetCaptures(0)`.
When the program proves each group can capture at most once on the successful
path, the cursor can copy completed slots directly. This can reduce modeled
result traffic by avoiding temporary history arrays.
Ordinary publication ends working admission for the range result.
`MoveNextRetained` requires shared working funding and returns a
`Utf8RetainedRangeMatch`. Captures, group numbers and the receipt are admitted.
By default the staging loan transfers to the receipt. If a separate result
account is supplied, it reserves before staging is refunded. Refusal is
terminal. Closing the receipt refunds once. Result copies remain valid under
the caller's retention responsibility, and cumulative numeric result charges
persist even when a live loan refunds.

### Host admission

For Core or PythonRe with finite working cap W and result cap R, a host can
reserve `checked(W + R)` before an operation. Refuse entry if a cap is unlimited,
the sum overflows, or the reservation fails. W bounds live covered working
capacity and R bounds cumulative covered result capacity. The sum conservatively
covers their union even if the model counts a buffer twice.

Hold the reservation while covered results or cursor aliases survive, or
transfer their admission to another host owner. Fund programs, pools, shared
caches, subjects and host output separately. Admission before engine entry
needs a host deadline. This recipe describes modeled capacities. PCRE2 uses
the [profile's own limit scopes](SPEC-PCRE2.md#interruption-and-resource-controls).

## Construction, preparation and caches

The host owns each compiled expression. `Utf8RegexPolicy` sets construction
limits and timeout independently of match limits and timeout. Null construction
timeout means infinite. Null match timeout uses `DefaultMatchTimeout`.
`Compile` accepts a separate construction token and observes pre-cancellation
before examining the policy or pattern. Nested construction shares its scope.
Construction authority ends at publication. Text construction and text
`Compile` default to `RegexOptions.CultureInvariant`. UTF-8 `Compile` defaults
to `RegexOptions.None`.

| Fixed construction backstop | Ceiling |
| --- | ---: |
| Instruction slots | 1,048,576 |
| Covered bytes | 64 MiB |
| Work units | 67,108,864 |
| Live frames | 65,536 |

Positive construction policy values tighten the backstops and null keeps
the defaults. A program allowance handles byte admission for funded creation,
including an optional `MaxBytes`. Denials use `ConstructionWorkSteps`,
`ConstructionDepth`, `ConstructionInstructionCapacity` or `ConstructionBytes`.
Construction timeout has empty `Input` and uses the supplied text `Pattern`
or an empty pattern for byte source diagnostics.

Admission precedes major buffers, instruction expansion and growth overlap.
Construction work limits and interruption cover parsing, lowering and emission.
Unpublished storage is released on failure. Replacement preparation uses
its own construction backstops within the original invocation deadline.
Finite working bytes also cover cold preparation and can cause
`ConstructionBytes` before matching. Interrupted preparation does not
publish a partial plan. Bounded caches retain immutable keys and lowered plans
under conservative charges. Active users keep their admitted plans through
eviction. See the [PythonRe contract](SPEC-PYTHONRE.md#construction-and-execution-controls)
and [PCRE2 contract](SPEC-PCRE2.md#interruption-and-resource-controls) for their
profile scopes.


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
