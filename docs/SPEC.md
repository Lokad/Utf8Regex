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
match. With the default `Utf8InputValidation.Validate`, malformed, overlong,
surrogate-encoding and truncated input throws `ArgumentException` with an offending
byte offset. Interruption or resource denial can occur before a later invalid byte
is visited. UTF-8 pattern and replacement validation is independent and remains strict.

### Subject input validation

Core and all three compatibility packages select an immutable `inputValidation`
constructor/factory option and expose it as read-only `InputValidation`:

```csharp
public enum Utf8InputValidation { Validate = 0, AssumeValid }

var regex = new Utf8Regex("needle", inputValidation: Utf8InputValidation.AssumeValid);
```

`Validate` is the default and validates the entire subject, including an unsearched
prefix or suffix after an early hit, at most once per logical operation or cursor
lifetime. `AssumeValid` omits that validity pass: the caller guarantees the entire
subject is valid UTF-8 and borrowed bytes remain unchanged throughout use. It
provides no malformed-subject rejection guarantee. There is no per-call override
or public validation token. Static matching conveniences keep default validation.

The policy covers matching/counting, detailed results, replacements, splits and
value/persistent cursors wherever a profile offers them, including funded and
derived operation programs. Managed bounds checking, Unicode/scalar semantics,
byte coordinates, cooperative cancellation/deadlines and resource admission
remain active. Required coordinate/ASCII classification, scalar decoding and
result sizing may still inspect input; these are not a mandatory validity pass.
Pattern, capture-name, replacement-template and assembled-output validation keep
their separate existing rules. Trusting subject input does not trust those inputs.
Work requirements can decrease when validation is omitted or redundant validation
is removed; finite policies remain enforceable and version/route dependent.

An integration may select AssumeValid once when its own text representation
already guarantees valid UTF-8. It must still keep borrowed storage alive and
unchanged. Comparisons between policies must label this different input contract;
the existing recorded benchmark tables use Validate unless explicitly stated.

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
strict whole-subject validation and final interruption/admission still apply.

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

`Count` can reuse the prepared literal-window counter on validated mixed UTF-8
subjects for capture-free windows without separator classes and with dot gaps up
to 2048 scalars. Gap endpoints count scalars while literal searches use byte
coordinates. The original invocation observes cancellation/deadlines throughout;
finite public numeric policies retain their owned counter. Working capacities
and work charges can differ from general VM execution.

Ordered-window branch admission covers one exact prefix comparison of at most
2048 bytes; longer prefixes retain separately admitted comparison chunks.
Branch order, boundary predicates, scalar gaps and publication checks keep the
original interruption authority. Removing duplicate short-prefix admission can
reduce numerical work requirements; basic metering remains active.

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

After scalar matching, `MatchDetailed` can reuse the identity coordinate map
when every returned capture history endpoint lies in a proved ASCII prefix.
The subject is still validated completely and borrowed unchanged. Lookaround
captures beyond the whole match are included in the proof; Unicode in the
required prefix retains full mapping, as do already admitted UTF-16 offset maps.
History and prefix scans have bounded interruption/work checkpoints. Avoiding
map arrays can lower working/result charges and admit formerly denied calls;
failed proofs can add time and work. Capture arrays remain admitted before allocation.

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
Proved unbounded scalar repeats excluding exactly two ASCII values can scan in
checked byte spans of at most 2048 bytes per work/deadline checkpoint. Whole-subject
UTF-8 validation precedes execution; ASCII stops and final endpoints stay on scalar
boundaries. Minimums above one, finite maxima and folding retain scalar-counted
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
Already-prepared families of three or four distinct heads, with needles of 3–16
bytes, can share the ordinary checked search inside each admitted window.
Complete verification precedes recovery of the original branch ordinal. One
combined scan replaces separate per-needle charges; working/result admission,
bounded interruption and terminal publication retain the original invocation.
Raw forward exact pairs can combine both short-needle scans into one checked
primitive of at most 2048 bytes. Complete verification stays inside that bound;
ordered branch confirmation and final publication still use the original
invocation. Combining scans can reduce numerical work charges, without changing
depth, working/result admission or retained-result lifetimes. Longer, folded,
reverse and unsupported-hardware paths keep their existing bounded scans.
Finite-policy and caller-funded `Count` also admit four-to-eight complete literal
alternatives to this existing bounded search. Avoiding VM scratch can lower work,
depth and working requirements. Explicit UTF-16 offsets still require admission
for their coordinate map; result-producing operations retain their existing
native eligibility.
Count can also reuse a proved positive ASCII simple-pattern plan on a valid
UTF-8 subject containing unrelated wider scalars. The existing proof must
guarantee that accepted spans contain only ASCII; opaque Unicode constraints,
folding, RTL/NonBacktracking, nullable/stateful cases and boundary/lookaround
requirements retain their existing routes. The counter borrows the original
validated invocation, including live/funded limits and interruption, without
revalidating or allocating unneeded VM capture/continuation storage. Numeric
work, depth and working requirements can decrease; real allocations remain
admitted before acquisition. This changes no returned capture or result lifetime.
Caller-funded Count requests only subject facts consumed by its selected route:
Unicode category/letter counting retains its supplementary-scalar description,
and ASCII word counting retains ASCII eligibility. Other byte counters check
subject validity under the immutable policy without deriving unused UTF-16
metadata. They keep one original invocation and synchronous interruption,
admission and refunds. Avoided metadata work can lower numeric work requirements.
Scalar Count can search an exact ASCII delimiter after a proved leading
class run, with or without intervening whitespace. The actual scalar predicate
rewinds a conservative candidate window; the VM verifies every retained start.
For a direct class/literal prefix, only the actual class is rewound, including
whitespace that belongs to it. Explicit whitespace/gap prefixes retain their
conservative union. Omitting impossible starts across nonmember whitespace can
lower numerical work requirements without changing allocation admission.
Skipped starts need no VM work or scratch. Whole-subject strict validation,
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

Finite-policy zero-origin LTR ASCII `Count` can reuse the existing structural
family search and linear verifier when the family's leading word-boundary proof
resolves proper-start prefix overlap, for example declaration keywords followed
by identifiers. Whole-subject validation, search, verification and publication
share the original work/token/deadline scope. Same-start or unproved overlaps,
mixed Unicode, folding, RTL and NonBacktracking retain their existing routes.
Avoiding scalar VM scratch can lower route-dependent work, depth and working
requirements; invocation metadata still allocates.

Finite structural/simple `Count` needs validity and ASCII eligibility, not UTF-16
lengths or supplementary-scalar metadata. It carries the existing ASCII proof
through the same bounded validation blocks; validation work charges, error offsets
and interruption points stay unchanged. Coordinate-producing operations still
derive their complete metadata. Unicode subjects retain scalar matching.

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

The scalar counter for a complete capture-free greedy literal or contiguous
ASCII range repeat can skip nonmembers through checked span search. Active runs
retain their original minimum/maximum splitting. Controlled searches charge
before each block of at most 2048 bytes and preserve the original token,
deadline and final publication check. Strict whole-subject validation still
precedes counting; no capture workspace or result payload is introduced.
Nonmember work charges can fall from per-scalar steps to bounded scan units.
Unproved classes, folding, RTL and NonBacktracking retain their existing paths.

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
expression (`\p{L}`) can Count using the existing category/letter kernels,
including caller-funded Count. Validate checks the whole UTF-8 subject first;
AssumeValid relies on the caller's validity contract. These counters classify
every encountered scalar directly and need no UTF-16 or supplementary-width
description. Trusted Count therefore omits the description pass and its work
charges. Native counting and admission remain unchanged. One work unit admits at
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
limits can admit this constant-space route. A bounded proof over at most 64
instructions can ignore plain capture markers around or inside that repeat for
Count, which publishes no capture values. Actual capture-returning operations
retain their original execution and storage. Lazy, nullable, trailing, RTL and
NonBacktracking shapes retain their existing paths. Matching semantics
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

The proved Unicode word-delimited token counter charges ASCII head/body and
interior-dot searches in blocks of at most 2048 bytes, with scalar word checks
at Unicode fall-throughs. These are bounded native work units rather than one
unit per ASCII character. The same invocation retains finite work admission,
original cancellation/deadlines and terminal checks; no working/result payload
is acquired by this counter.

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
[benchmark summary](../bench/Snapshots/Core.json).
