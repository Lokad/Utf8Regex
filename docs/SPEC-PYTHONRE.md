# PythonRe contract

`Lokad.Utf8Regex.PythonRe` is the optional managed Python `re` profile.
It targets CPython **3.13.2 str-pattern semantics**, with owned Unicode **15.1.0**
case, identifier and predefined-class data. It matches scalars over strict UTF-8;
borrowed results expose byte spans and byte coordinates only, with no string
getters. Callers decode selected bytes or derive character positions from
their text when needed; no result exposes scalar or UTF-16 coordinates.

See [release notes](../CHANGELOG.md), [migration](MIGRATION.md) and
[shared resource contract](SPEC.md#cancellation-deadlines-and-resource-limits). Exact declarations are pinned in the
[API snapshot](../tests/Lokad.Utf8Regex.PythonRe.Tests/PublicApi.Shipped.txt).
This is a selected profile, not complete Python equivalence.

## Subject text contract

This profile models Python `re` over `str`. A subject is already-valid UTF-8 text;
the caller keeps borrowed storage unchanged during an operation or cursor lifetime.
Validate external bytes where they become text, before passing them to this API.
Regex operations do not perform a whole-subject validity pass, and malformed-prefix
or malformed-suffix rejection is not a compatibility requirement. Invalid UTF-8
subjects are outside this profile's domain.

There is no subject-validation option, property or token. Pattern, replacement and
capture-name validation remain independent. Managed bounds, Unicode scalars, byte
offsets, captures, cooperative cancellation/deadlines and resource admission remain
active. Requested coordinate/output work can still inspect the relevant text;
whole-subject descriptions are not a prerequisite for byte-only discovery.

## Operations and result shape

| Family | Contract |
| --- | --- |
| Match | Stack-only whole-match value plus optional caller-owned capture slots. Utf8PythonMatchMode selects Search / anchored Match (default) / FullMatch. |
| IsMatch / Count | Boolean/count using the profile's discovery and empty-match rules |
| CreateMatchCursor | Immutable capture snapshots borrowing ReadOnlyMemory<byte>; Search advances successively, Match/FullMatch publish at most one result; explicit disposal on early stop |
| Replace / Subn | Python template expansion into owned UTF-8 bytes; Subn includes replacement count |
| SplitRanges | Python capture-aware split as byte ranges; slot number and participation distinguish captures, unmatched groups and ordinary segments |

Value operations, Count and derived byte outputs use one
token-bearing signature per supported input/template representation. Byte offsets,
replacement/split counts default to zero; cancellation defaults to None. Entry
cancellation remains before program use and argument/input work. Ordinary short
source calls are unchanged; recompile old binaries and adapt shorter delegates
with explicit lambdas when needed. The cursor has one optional working-allowance parameter: null inherits program
funding or numeric engine limits; a non-null account overrides working funding.
Refusal remains terminal and original-token cancellation takes precedence.

`startOffsetInBytes` follows Python `pos` semantics: discovery starts at or
after the floor, while anchors, boundaries and lookarounds retain complete prefix
and suffix context within the selected end bound. FullMatch covers [pos,end).
The optional endOffsetInBytes must be a scalar-aligned byte boundary with
0 <= start <= end <= input.Length. It preserves prefix/lookbehind origin and
limits lookahead. Python character endpos/clamping is not exposed.
An early result needs no validity scan of the remaining text.

Nonempty scalar programs can reject an absent mandatory exact literal, proved by
their existing native rendering, before acquiring VM scratch. The search observes
the same work budget, token and deadline as matching and result processing. Presence
alone does not prove a match; tight working/depth/result limits may admit a
scratch-free miss that formerly required VM capacity. Charges remain dependent
on the selected route and version.

Anchored Match/FullMatch can reuse a bounded necessary-start proof to reject an
impossible starting byte before mandatory-literal screening or VM scratch. This
never establishes success; possible starts retain screening and verification.
The original invocation still checks interruption and resource admission.

Nonliteral prefix/digit Match and DotAll prefix/suffix FullMatch use the shared
scalar program. FullMatch requires the selected end during backtracking. Work,
depth and scratch requirements depend on
the selected program; retirement of a shortcut can increase these costs without
changing the input, matching, lifetime or interruption contracts.

Shared scalar execution can scan no-rewind, unbounded positive ASCII interval
repeats in blocks of at most 2048 bytes. Basic work and original interruption are
checked before each block and at completion, even without finite limits. Scalar
endpoints, captures and admission remain; route-dependent work totals can decrease.

A complete ASCII-prefix root with one capture around a terminal positive interval
`+` can reuse that runner for latest-capture operations. Its bounded whole-root
proof retains the existing admitted workspace/slots, LastIndex and retained
results. Two nonnested positive ASCII interval `+` captures can also reuse it
when a nonempty exact ASCII literal separates them and its first byte cannot be
consumed by the first interval. Both existing closing-journal entries remain
admitted, preserving `LastIndex` without another completion flag or map.
Literal, repeat-block and publication checks keep the original authority;
removed VM dispatch can lower work totals. FullMatch end constraints and other
root shapes retain their existing verification. No capture history is discarded.
A complete root consisting of one positive unfolded ASCII set capture can also
omit VM setup and marker/Accept dispatch, retaining the same scalar decoder and
predicate. Split strings/ranges and other latest-capture consumers keep their
workspace/slot admission, LastIndex, result lifetimes and original interruption.
Entry/publication remain metered; numerical work can decrease. FullMatch end
constraints, history and other root shapes keep their existing verification.

A prepared prefix trie can search a whole remaining suffix of at most 256 bytes
between charged entry/completion checkpoints. It returns only a necessary start;
the scalar verifier retains ordered matching and captures. Reusing shared prefixes
can lower numerical work charges without changing allocation or admission scope.

A prepared exact family with one first-byte bucket and a unique discriminator
can search its shared prefix in bounded windows, then check the discriminator
and only the still-unproved tail. Prefixes longer than1024 bytes retain chunked
confirmation beyond that searched prefix. Earliest complete candidate selection,
scalar verification/captures and original interruption remain; avoiding repeated
prefix confirmation can lower work units without adding storage.

A straight exact ASCII literal run can also prove the byte distance to a
mandatory, disjoint later ASCII interval. Search intersects its head and later
interval using checked range searches; the scalar VM still verifies the full
prefix, assertions and captures. Program inspection is interrupted in bounded
chunks and retains no additional buffer. Match/FullMatch remain anchored; opaque
or possibly empty suffixes keep their existing candidate loop. Numerical work
charges may change as repeated failed verification is avoided.

Search-based operations can also narrow candidate starts for a positive greedy
ASCII set repeat followed directly by an exact ASCII delimiter, allowing capture
markers between them, including short subjects of at least 16 bytes.
Existing checked, metered search and scalar rewind locate
a necessary window; the profile VM still verifies its candidates with complete
prefix context and original capture/backreference order. Match and FullMatch
retain anchored verification. The text contract and admission for used capture
slots remain; avoiding impossible starts can lower execution work and required VM
workspace. Search blocks, rewound scalars, verification and publication keep the
same invocation's cancellation, deadline and resource authority.
Direct class/delimiter windows rewind the class itself, including any accepted
whitespace. Explicit whitespace/gap prefixes keep their conservative windows;
omitting impossible starts can lower numerical work requirements.
After a leading positive word boundary, the same search can rewind a short
bounded class of up to three Unicode scalars before an exact ASCII delimiter.
The verifier still checks the boundary against the original subject.
The first necessary ASCII-range candidate can be verified before window search
when an excluded delimiter lies within its 64-byte prefix. A proved complete
no-rewind capture root needs no delimiter preview: its existing metered repeat
runner can verify that first candidate directly, including a long first run.
A failed verification resumes the existing window search and scalar progression. Captures still come
from the VM; no result is inferred from the prefix. Actual discovery work changes
and rejection-heavy subjects can cost more, under the same original authority.

A leading positive class can also guide starts using its complete ASCII mask.
This includes bounded unions of predefined Unicode classes and masks with a
positive range entirely above ASCII. The seeker skips only the ASCII bytes the
mask's interval excludes and returns wider candidates to the full scalar
verifier. Unproved class shapes retain their existing routes. Skipping impossible
starts can reduce numerical work and VM scratch requirements under the same
invocation.

Value `Search` from byte zero can borrow the existing complete literal search
under finite limits, a live token and an absolute deadline. The caller supplies
valid text; literal values return the searcher's byte endpoints without
coordinate scans. No VM workspace or capture storage
is needed for that route. Numerical work/depth/working requirements can decrease;
actual payload admission and original interruption authority remain. Other
patterns and nonzero offsets retain their existing routes.

The shared scalar runner can also omit impossible plain-alternative frames using
a bounded required-ASCII-literal check, as described in [Core](SPEC.md). Captures,
ordered choice and original invocation controls remain; required capacity can fall.

Capture-free programs proved unable to match empty can Count through the shared
scalar counter, using Python's compiled predicates and instructions, without
subject validation or unused UTF-16 coordinates. A leading class
run followed by an exact ASCII delimiter can bound candidate windows; scalar
membership and complete VM verification remain authoritative. This can reduce
work and scratch on misses. Captured and empty-capable programs retain their
profile cursor, including empty-then-nonempty retries. Original cancellation,
deadlines, major-buffer admission and caller-funded refunds still apply.
The shared class rewind can classify an unproved window prefix before VM
admission, reusing membership already established by a backwards step. Fully
rejected Count windows need no VM storage; viable starts retain full verification.
This can lower numerical work, depth and working requirements.

Count can also borrow Core's complete, case-sensitive nonempty literal proof for
up to eight alternatives, including captures whose values Count discards. The
existing bounded search shares matching's work, token and deadline authority.
These calls need no VM workspace or capture storage; numerical working/depth/work
requirements can decrease. Discovery and result-producing operations retain their
own eligibility, and patterns without a compatible native rendering retain Python
scalar execution. This does not change matching order, captured results or limits
on actual result payloads.

A complete positive greedy scalar repeat can also Count with Core's constant-space
run counter when a bounded proof of at most 64 instructions finds only plain
capture markers around or inside that repeat. Count has no capture observer;
matching, findall and other capture-returning operations keep their original
capture handling. Scalar membership, byte floors, the valid-text contract,
original interruption and admission remain. Numerical work/depth/working costs
can decrease; nullable, lazy, trailing and capture-dependent programs keep their routes.

Other nonnullable iteration-compatible captured roots can use the existing scalar
counter when retained Core preparation proves that captures have no dependent
readers. Count then omits capture slots/journals and the capture-producing cursor;
Python's own scalar program still defines lexical folding, classes and matching.
References, conditions, assertions and unknown preparation retain their previous
routes. This can lower numerical work/depth/working requirements; valid-text input,
original interruption, major-buffer admission and capture-returning APIs remain.

Prepared short three/four-literal families with distinct leading bytes can share
Core's checked search within the existing windows of at most 2048 bytes. Count and
necessary-start discovery charge the combined bounded scan instead of each
needle separately; work requirements can fall. Candidate discovery still needs
complete Python verification, including captures and tails. The valid-text contract,
original interruption, storage admission and terminal publication are unchanged.

Necessary ASCII start hints can enter a mandatory atomic body and skip impossible
starts. The profile VM still verifies the complete body, atomic commit and
captures. Caller-guaranteed valid text and the original interruption authority remain;
skipping failed attempts can reduce work charges.
Unicode-class ASCII projections do not classify an unrelated subject suffix.
Candidate search proves the skipped ASCII prefix within its existing2048-byte
blocks, then returns a non-ASCII candidate to the original Unicode verifier.
An immediate admitted ASCII start needs no such scan. Existing whole-subject
ASCII facts can be reused when a coordinate consumer already requested them.
This can lower numerical work requirements without changing captures, scalar
boundaries, major allocation admission or synchronous original interruption.

Word boundaries share Core's checked ASCII byte comparison. PythonRe's Unicode
word table still classifies wider neighbors. After VM scratch has been admitted,
a positive leading word boundary can skip ASCII word interiors in blocks of at
most 2048 bytes, with an interruption and work checkpoint per block. Complete
capture-free boundary-only programs reuse this proof without a consuming start
hint. Non-ASCII neighbors, possible boundaries and EOF retain complete VM
verification. The skipped attempts can lower version-dependent work charges.

Capture-free exact literals, including two or three alternatives, followed by
an exact positive literal lookahead can borrow complete native discovery. The
same invocation confirms the suffix before publishing, excluding it from the
returned span. Search, counting and cursors avoid VM scratch; captured results
and unsupported assertions retain Python verification. Single-literal FullMatch
also checks both endpoints without preparing a second scalar program. Work and
depth charges reflect the work and state actually used.

An unconditional failure program also finishes before VM scratch, after an
original-invocation publication checkpoint. A sequence of an
atomic greedy unbounded unfolded literal repeat followed immediately by the
same required literal (for example `a++a`) is provably impossible and lowers to
the existing failure instruction. Capture catalogs retain all declared groups;
enclosing alternatives and assertions retain their normal semantics. Lazy,
finite, folded and compound atoms retain verification. Construction charges the
bounded adjacency scan; execution can admit a constant-space miss under tighter
working/depth/result limits than its former VM route.

`CaptureGroupCount` reports the validated group count excluding group zero,
independently of match participation. `TryCopyPatternUtf8` follows the
[caller-buffer metadata contract](SPEC.md#native-pattern-metadata), preserving
original source and rejecting unpaired legacy surrogates. Funded expressions
hold their existing program use during a copy and reject new copies after close.
`TryGetGroupNumber(nameUtf8, out number, token)` validates a complete UTF-8 name
and looks up named captures only; numeric strings do not alias slots. Unknown
names return false/zero, malformed bytes throw with their byte offset. Admitted
UTF-8 keys are compared ordinally without normalization or temporary strings.
The immutable vector lookup is linear, uses bounded token checkpoints and holds
funded program use. It is explicit metadata outside matching limits.

`TryCopyGroupNameUtf8(captureSlot, destination, out bytesRequired, token)` copies
one name in Python opening order, including slot zero and decimal labels for
unnamed slots. It follows the shared caller-buffer metadata contract: a short
destination is unchanged, the complete UTF-8 size is reported, and cancellation
uses the original token at bounded checkpoints. Pre-cancellation precedes bad-slot
or closed-program work. Funded copies require an open expression, consistently
with source copying and name lookup. Caller bytes remain valid afterward.
`GetGroupNames` is removed, including its formerly unguarded post-close access.
The scalar program's admitted canonical names serve this API; the duplicate
public-name table and its numeric-string builder are removed. Modeled construction
work/storage can decrease; major-buffer admission and useful limits remain.

Captures are numbered by opening parenthesis, including named groups.
References and conditionals use those numbers. Detailed `LastIndex` is the last
capture to close on the successful path, not the largest participating number:
`((a))` closes group 1 last. Misses and matches without a participating capture
have a null `LastIndex`. Hosts implementing Python's `lastgroup` can resolve that
number using the names copied from the compiled regex and return null for an
unnamed group. With one
group, participation in its completed slot establishes `LastIndex` without a
completion-journal scan. Unneeded rollback storage can be omitted on linear paths.
References after the sole unique capture closes can also use its completed slot
without a separate opening journal. References that can observe an open capture
or an external assertion slot retain their opening history.
Numeric work/working requirements may decrease, with admission before needed
allocation and interruption/result contracts preserved.
Global discovery retries a nonempty match at the same position after an empty
result before advancing one complete scalar. A complete capture-free boundary-only
program skips that impossible nonempty retry. A complete LTR greedy scalar repeat
can also skip it after returning empty: it has already exhausted its consuming
endpoints at that position. Both use the same scalar advancement, EOF checks and
workspace cleanup. Lazy, captured, alternating and constrained-tail programs keep
their retry. Eliminated instructions lower work requirements,
with the original cancellation/deadline and byte/depth/result admission preserved.

Default and unsuccessful range results expose no capture slots. Successful range
results own immutable numbered captures and borrow the unchanged subject. Names
belong to the compiled expression. Text materialization belongs to the host.

## Grammar and parsing

UTF-8 pattern overloads validate strict input and parse scalars directly through
the same Python grammar. Retained source owns an admitted byte copy; caller
buffer reuse after compilation cannot alter the expression. `Pattern` explicitly
projects an uncached string for byte-compiled expressions. Name/escape diagnostics
may project selected lexemes. Core preparation and scalar leaves emit admitted
UTF-8 directly, preserving lexical flag scopes. Exact literal preparation and
prefix proofs also retain bytes. Scalar/cursor setup borrows its expression for
timeout diagnostics; successful operations do not implicitly request `Pattern`.

`FindAllToStrings`, `FindAllToUtf8`, their shaped result carriers and the legacy
structural `FindAll`/`Utf8PythonMatchData` collector are removed. Whole-match and
capture byte ranges come from `CreateMatchCursor` over stable
`ReadOnlyMemory<byte>`. The host selects whole values or capture groups and
collects, copies or decodes them as needed. Unmatched groups expose unsuccessful
ranges and empty byte views; callers choose their output representation.
Host collections, byte copies and strings are outside regex memory/work/
interruption controls. Cursor discovery and immutable capture snapshots retain
their cumulative work, working/result admission and original token/deadline.
Callers with transient spans must retain suitable subject memory for the cursor.
Range metadata survives advances, cursor disposal and program close; byte views
continue to borrow unchanged input.
Borrowed literal Count trusts the subject's valid-text contract without deriving
discarded UTF-16 totals. No absolute-coordinate index is retained.

Splitting stages byte ranges for segments and captures, including absent groups,
with result admission and bounded interruption checks. It neither sizes nor
decodes the selected text. Discovery and final publication retain the original
invocation's authority.

One-shot `Match` in Search/Match/FullMatch mode returns borrowed byte ranges over
caller-guaranteed valid text. `SearchToString`, `MatchToString` and
`FullMatchToString` are removed, together with their separate decoding/admission
path. Check `Success` to distinguish no match from a successful empty range.
The host can decode `GetValueBytes()` when needed; that caller allocation is
outside the regex invocation's memory/work/interruption controls. Discovery and
publication retain their original authority and useful limits. No whole-subject
coordinate or character-count scan is required for these values. Caller-owned
capture spans request a prefix of numbered last-capture slots including zero.
Short buffers do not alter matching and excess slots remain untouched. Pre-canceled
entry preserves output; no match or later failure clears the requested prefix.
The result does not borrow output slots, so whole-match bytes survive slot reuse.
Caller destinations add no result-array allocation or result-byte charge; the
existing VM still admits its major scratch and observes bounded projection checks.
Use the capture cursor when closing order matters. Its `LastIndex` reflects the
last capture to close even when a larger slot also participated.
Search/FullMatch methods are removed; use mode and recompile callers. Name old
positional tokens cancellationToken: because captures is now the third parameter.
Match accepts optional working admission; null inherits program/numeric funding,
and supplied refusal is terminal. Pattern/template validation remain independent.

Owned byte output retains independent admission and bounded copying. Exact
ASCII-literal substitution does not describe the subject or replacement in
UTF-16 before byte discovery/output. Replacement validation remains separate.
Byte output needs neither character counts nor whole-subject ASCII classification.
Caller-valid unchanged text, original interruption and meaningful work/memory
controls remain authoritative through publication.

Supported families include literals/classes/standard escapes, ordered alternatives,
greedy/lazy/possessive repetition, captures/named groups, references,
participation conditionals, lookahead, fixed-width lookbehind, atomic groups and
leading/scoped flags. Python replacement references include `\1` and
`\g<name>`.

`PythonRePatternException` exposes immutable `ErrorSource`, `ErrorKind` and
`Position`, without requiring message parsing. `ErrorSource` distinguishes pattern
text (including inline flags), replacement text and supplied compile options.
`ErrorKind` distinguishes syntax/reference/width errors, invalid options and
deliberately unsupported features. The existing public exception constructors
default to pattern/syntax; messages and exception types retain their meanings.
`Position` is a zero-based **UTF-16** source-string position, including for a
UTF-8 pattern overload, or `-1` when unavailable. It is neither a byte position
nor a Unicode-scalar index. Positions identify the offending template token or
reference when known; failed deferred width validation and combined global flag
validation may have no position. No source text or whole-source index is retained
to manufacture one. Pattern syntax still precedes combined inline-option errors.
For byte sources, a known failed prefix is counted with construction interruption
to derive that legacy position, without decoding the whole source or building an
index. Strict validation precedes source acquisition; storage is admitted before
copying, and covered work units remain version-dependent.

Unknown named replacement groups retain `IndexOutOfRangeException`, identifying
a replacement-name lookup error by type; they expose no position. Cancellation,
deadlines, resource/admission failures and argument errors retain their existing
distinct types and original-token guarantees. These are structured distinctions;
diagnostic prose is not a parsing protocol.

| Area | Rules and important boundaries |
| --- | --- |
| Unicode literals | Raw supplementary characters, escaped raw non-ASCII characters and `\U0001F600` are one scalar. Separate `\uD83D\uDE00` escapes remain impossible surrogate atoms; they do not join. Surrogate-only classes match no scalar; negation uses the valid scalar complement. |
| Braces | Only complete `{m}`, `{m,n}`, `{m,}`, `{,n}`, `{,}` are repeats. Other forms, including `{}`, are literal. Finite bounds through 4294967294 remain distinct from infinity; larger bounds report a pattern error rather than arithmetic overflow. |
| Verbose/comments | Trivia may separate atom and quantifier, but not join lazy/possessive suffixes. `(?#...)` is ignored in every scope. Escaped `)` does not close it; escaped LF does not end a verbose comment. Leading flags may follow only leading trivia. |
| Modes | Effective global ASCII/Unicode flags cannot conflict, including separate leading directives or constructor flags. Body syntax errors precede this conflict; it precedes deferred reference/width validation. Scoped a/u overrides restore on exit. |
| Quantification | Direct `^`, `$`, `\A`, `\Z`, `\b`, `\B` quantification rejects. Grouped anchors and quantified lookarounds remain admitted. In brackets, `\b` is backspace and the standard control escapes retain scalar values. |
| References/conditions | References require previously defined closed groups. Conditional names must exist at the header; outside lookbehind an already-open named group is allowed, and numeric forward IDs receive final group-count validation. Oversized IDs fail without overflow. |
| Lookbehind | Width is measured in scalars. References/conditions must target a closed group defined before the outermost enclosing lookbehind, including nested assertions. Analysis preserves the oracle's 2^64 width clamp; a fixed width beyond the 32-bit code limit reports `looks too much behind`. |
| Names | Owned Unicode 15.1 identifier validation applies to declarations, references, conditions and replacements. Accepted spelling is preserved; canonically equivalent names need not be identical. |

Required nullable repetitions run before the optional phase's empty-progress
guard. Captures from a required empty iteration can affect the next conditional:
`(?:(?(2)(a)|()))+` consumes `a`. Lazy/possessive repeats retain this rule;
work and state remain admitted. Numeric range errors can use different exception
types/position sentinels from CPython; no Python exception-type identity is promised.

## Unicode and documented differences

The profile is culture-invariant. Unicode literals/classes use pinned simple-lower
inverses and additional literal partners; references compare simple lowercase
only, under flags at the reference site. Scoped ASCII folds ASCII letters only.
Supplementary partners remain one scalar even when byte widths differ.

Predefined classes and boundaries use owned CPython 3.13.2/Unicode 15.1
predicates, including complements and mixed brackets. ASCII `\s` excludes
U+001C–U+001F, and a bracket can still include them explicitly.
Complements consume one Unicode scalar. Shared profile tables are reused
across invocations. Verify derived data with CPython 3.13.2:

```powershell
python tools/generate-pythonre-scalar-case-data.py --check
```

CPython 3.13.2 Search can miss a supplementary scalar for scoped-ASCII
`\W`/`\D` complements although Match/FullMatch succeed. This profile keeps
Search/Match/FullMatch consistent with that Match/FullMatch behavior.

Bytes-pattern semantics, LOCALE and `\N{...}` Unicode-name lookup are excluded.
Unsupported cases fail explicitly. No Python runtime or BCL matcher/results are
shipped.

## Construction and execution controls

### Compile and prepare

Constructors and `Compile` accept PythonRe compile options and one immutable
`Utf8RegexPolicy`. `Options` reports the supplied flags. `EffectiveOptions`
includes validated leading global flags and implicit Unicode unless global
ASCII is selected. Scoped flags affect matching only. The enum uses PythonRe
values.

Settings are validated before pattern conversion and allocation. Invalid policy
settings name `policy`. Pattern diagnostics follow the source and position
rules above. `Compile` observes its construction token before validating
arguments or acquiring storage.

`ConstructionPolicy` and `ConstructionTimeout` govern compilation independently
of matching. Null construction timeout means infinite. Construction prepares the
same scalar program for Search, Match and FullMatch before publication. Constructors
with default construction settings and no funding can borrow an active construction
scope. An explicit compilation requires its own construction scope.

The Python scalar program defines the profile's semantics. Eligible literal
operations and candidate searches can reuse Core helpers under the same input
contract. Work and storage charges depend on the selected route and version.

### Invocation

Null `MatchTimeout` uses `Utf8PythonRegex.DefaultMatchTimeout`, initially
infinite. A single invocation covers discovery, capture range preparation,
output and publication. It uses the original token and one absolute deadline.
Construction tokens expire with construction. The four execution limits, typed
denials and covered capacities follow the
[shared resource contract](SPEC.md#cancellation-deadlines-and-resource-limits).

Cancellation is synchronous and keeps the originating token. Deadline expiry
throws `RegexMatchTimeoutException`. Timeout diagnostics from PythonRe keep the
original pattern. A delegated Core operation can propagate its own exception.
Shared working admission or finite working/result byte limits omit the subject
from timeout diagnostics. An interrupted operation reports failure before
publishing its result or reporting the end of discovery.

### Program and working memory

A supplied `ProgramMemoryAllowance` admits covered construction storage and
retains a conservative charge for the completed program. A funded build without
`MaxBytes` relies on that account for covered bytes. Explicit byte limits and
the existing instruction, work and depth backstops still apply. Failed
construction refunds discarded reservations. The accounting covers modeled
capacities and can count shared storage more than once.

Funded expressions default to that account for covered temporary working
storage, including growth overlap and output staging. `Match` and
`CreateMatchCursor` can select a separate working account. Program and cache
charges remain on the program account. Covered storage must pass both numeric
and shared admission. A shared refusal throws
`Utf8MemoryAdmissionException`. Exclusive temporary loans are returned at
completion, failure or disposal.

Disposing a funded expression rejects new matching uses and operations that
borrow the program's metadata with `ObjectDisposedException`. Active calls
and persistent cursors retain their program use until finished. Disposal
leaves them running. Properties such as `Pattern` and `EffectiveOptions`
remain readable. Unfunded expression disposal is a no-op.

Ordinary result storage transfers to the caller at publication. Retained range
and UTF-8 substitution receipts can keep the covered result charge on an
account after return. Numeric result charges are cumulative even when working
loans refund. Borrowed input, caller retention of ordinary results, and
host-side decoding, allocation or custom output stay outside the engine's
modeled capacities.

### Replacement preparation

The bounded cache keeps validated template keys and plans. Warm lookups borrow
the supplied span. Template lookup and matching draw from the same execution
work budget. A cold lookup admits the immutable key before copying and
keeps its working charge during preparation. Cold template
preparation retains its construction backstops within the original invocation
deadline. Finite working bytes also cap additional covered preparation storage.
A numeric working-byte denial on the template key reports
`ExecutionWorkingBytes`. A preparation byte denial can report
`ConstructionBytes` before matching.

Funded cache entries charge the program account until their last use, including
after eviction. Failure releases discarded storage. An interrupted preparer
cannot lend its token or deadline to a different caller. Disposal stops
retention while existing admitted calls can finish.

## Cursor and result lifetime

`CreateMatchCursor(ReadOnlyMemory<byte>)` retains one invocation across moves,
including host waits within its absolute deadline. Optional mode defaults to
Utf8PythonMatchMode.Search; Match anchors at startOffsetInBytes and FullMatch
requires the complete remaining prefix through endOffsetInBytes. Both anchored
modes publish at most one result, including an empty result. The optional end
defaults to input.Length. Start/end must satisfy 0 <= start <= end <= input.Length
and fall on scalar boundaries. The prefix before start remains available to anchors
and lookbehind; lookahead cannot read past end. Bounds do not validate the subject.
FullMatch checks its required end under the original invocation deadline.
Reference aliases share
position and disposal; concurrent/reentrant access rejects. Advance with
`MoveNext(out Utf8PythonRangeMatch match, token = default)` and keep the returned
value. There is no text `Current` or no-output advance. EOF, failure or Dispose
synchronously release exclusive scratch and the cursor's subject references;
dispose when stopping early. There is no finalizer.

The optional move token adds interruption authority for that synchronous advance,
including discovery and required range publication. The original creation token,
deadline and cumulative budgets remain authoritative. The move token is detached
before return, so canceling it afterward cannot affect later moves. Cancellation
faults the cursor and releases scratch; earlier ranges remain valid. When both
tokens are canceled, the original token takes precedence in the exception.
Two distinct cancellable tokens require a temporary linked source, disposed before
return; a sole move token or the same token needs no linked source. Omitting the
move token retains the original controls. Already-ended cursors return false;
disposed, faulted and concurrent/reentrant access retain their failures.

Host decoding, copying and collection are outside regex controls and need their
own admission and final interruption checks. Neither ordinary range publication
nor one-shot owned results bound external host retention. A new call gets an
independent allowance and needs independent host admission.

Range snapshots own immutable capture arrays and borrow the unchanged
`ReadOnlyMemory<byte>` subject. Their metadata survives cursor moves/disposal;
keep its memory manager alive when accessing `GetValueMemory`/`GetGroupMemory`.
Unmatched captures have `Success=false` and zero offsets, distinct from successful
empty captures. Byte offsets are relative to the supplied subject memory. No
captured text, UTF-16 conversion or whole-subject coordinate map is materialized;
these types expose byte coordinates only. Hosts derive character positions.
Result arrays are admitted before acquisition under the result-traffic/working
scopes. Ordinary range publication releases temporary funding; it does not bound
caller retention.

`MoveNextRetained(out Utf8PythonRetainedRangeMatch, token = default)` uses that
same range shape but transfers its already admitted charge to a disposable
receipt. The cursor must have a caller-owned working allowance, either from
funded Compile or its explicit factory argument. An unfunded cursor rejects this
method before discovery and can still use ordinary advances. No Release/TryReserve
gap occurs during transfer. `ChargedBytes` reports the modeled amount, including
the receipt object and capture array. Borrowed subject storage remains excluded.
Numeric result traffic includes
that acquisition and is never refunded by disposal or transfer.

The receipt's `Match` owns immutable range metadata and survives cursor/program
disposal and host waits. Dispose the receipt after retained users finish, or take
over admission for copies kept afterward. Dispose refunds exactly once, including
concurrent calls/receipt aliases, and closes later `Match` access. Copies already
obtained remain valid under their original borrowed-input contract; admission for
continued retention after this explicit release is the caller's responsibility.
Finish `Match` accesses before disposal. Disposal clears the receipt's own payload
references; keeping its historical charge record does not keep a subject alive.
There is no finalizer. Retaining receipts can exhaust the shared allowance;
discarding and disposing them supports bounded-live streaming.

```csharp
// regex was compiled with a caller-owned program allowance.
using var cursor = regex.CreateMatchCursor(subject);
while (cursor.MoveNextRetained(out var retained, cancellationToken))
{
    using (retained) Consume(retained.Match);
}
```

Byte-template substitution copies valid encoded literals and scalar-aligned
captures through an admitted writer and owned final byte array. It performs no
UTF-16 sizing/decoding and has no raw callback fragments to validate. Strict
template parsing/encoding remains independent of the trusted subject contract.
The shared worker has one template operation; per-match snapshot loans, callback
rollback and string completion are gone. Writer/final-root overlap, typed denial,
original cancellation/deadline and retained-byte transfer remain enforced.

`SubnRetained(input, replacementUtf8, count = 0, startOffsetInBytes = 0, cancellationToken = default)`
requires caller-funded Compile and returns `Utf8PythonRetainedSubnResult`. Its
`Result` contains the owned UTF-8 byte array and replacement count. It uses the
ordinary template validation, native/scalar matching and output pipeline, without
another output copy or a Release/TryReserve gap. Invalid templates are validated
even on no match. Count, byte offsets, independent template checks, scalar progression,
the original token/deadline and numeric resource limits retain their meanings.
The receipt conservatively charges its object and final output; temporary match
and writer storage is refunded when the operation finishes. Numeric result traffic
includes acquisition of the receipt and is not refunded by ownership transfer.

The result survives expression disposal and host waits. `ChargedBytes` reports its
original modeled charge, including after disposal. Finish `Result` property reads
before disposal. Dispose clears the receipt's payload references, closes later
`Result` access and refunds once, including concurrent Dispose calls and aliases.
Previously obtained byte arrays remain valid; the caller owns admission for
continued retention after explicit release. No finalizer is involved. Host text collections and ordinary
bulk byte output remain outside these two receipt APIs.

`Replace`, `Subn` and `SubnRetained` take a `ReadOnlySpan<byte>` replacement template,
with optional count, byte start and cancellation token. Templates encode Python's
Unicode template grammar in UTF-8. Parsing, literal fragments, cache keys and output stay in bytes;
only selected group-name/error lexemes may project text. Complete strict template
validation precedes grammar errors and matching, including misses and negative
counts. An exact replacement consumes the immutable literal produced by that
validated plan. Template `Position` retains its published UTF-16 units, derived
from the failed prefix.

Warm native lookup borrows the supplied span. Cold preparation admits an immutable
byte-key copy before acquisition and keeps its working loan through retained-cache
admission; denial never retries without limits. That temporary key overlaps parser
capacity under the working-byte ceiling and fixed construction backstop. Retained
cache capacity has its separate conservative bound. Failed templates are not retained.
Funded key/plan storage remains charged through eviction/disposal until its last
active user finishes. Numeric work/depth/working/result limits and the original
operation token/deadline apply; neither caller buffers nor total process memory
become covered by this API.

Capture ranges expose byte start/end offsets. The former `TryGetScalarRange`
query and its optional scalar-prefix index are removed. Unicode scalar matching,
lookaround extents, empty progression and capture priority are unchanged. Hosts
that need character positions derive them outside the regex invocation's work,
memory and interruption scope. Cursor advances retain creation/current-call
cancellation and the original deadline; retained byte snapshots keep their
existing lifetime and admission contracts.

`CreateMatchCursor(input, startOffsetInBytes, token, workingMemoryAllowance)`
accepts an optional `IUtf8MemoryAllowance` shared directly across concurrent cursors and
other host users. Existing working-buffer admission reserves additional modeled
bytes before acquisition, including old/new growth overlap. Refusal throws
`Utf8MemoryAdmissionException` with the additional requested amount; it never
becomes no-match, an uncapped retry or a new independent allowance. Numeric
work/depth/working/result limits, original token and deadline still apply.
No default working-byte cap or cumulative traffic quota is imposed by this API.
Timeout exceptions omit Input under shared admission, avoiding a subject-sized
diagnostic decode outside the allowance after expiry.

EOF, failure and cursor disposal synchronously refund exclusive working storage;
aliases share this lifetime and cannot double-refund. Required result/staging
arrays are funded until publication, then transfer to the caller's retention
scope. Input/program/cache storage, incidental owners, callbacks, later getters
and caller-retained results remain outside **this shared working allowance**.
This is the first shared-admission path, not the handoff's complete aggregate
compilation/cache/result-ownership guarantee. Implementations must be thread-safe,
fast and non-reentrant into regex; refusal/throw leaves the allowance unchanged,
and Release must not throw or observe cancellation.

`SplitRanges` returns owned `Utf8PythonCaptureRange` metadata over the original
input: number zero is an ordinary segment, positive numbers are captured
separators, and unmatched separators retain `Success=false`. It preserves Python
empty-match progression, suffix/start-offset behavior and maxSplit semantics
(zero unlimited, negative no splits). The final array grows from actual output;
a no-separator split emits one range without reserving hypothetical matches or
copying/decoding the subject. The caller slices its unchanged input to consume
UTF-8 bytes. Explicit tokens cover discovery, shaping and result publication.
`SplitToStrings`, `SplitDetailed` and `Utf8PythonSplitItem` are removed. Hosts may
decode successful range slices and represent unmatched captures separately.
That host work and its strings are outside regex work/memory/cancellation limits;
the library still admits its range arrays before allocation and never publishes
partial output on denial or interruption.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None);
Assert.Throws<OperationCanceledException>(() => regex.Count("cat horse"u8, 0, canceled.Token));
```

Use the [package README](../src/Lokad.Utf8Regex.PythonRe/README.md) for call examples
and [benchmarks](../src/Lokad.Utf8Regex.PythonRe/README.md#benchmarks) for measurement scope.
Safe finite defaults, complete preparation/retention containment and full grammar
equivalence are not implied.
