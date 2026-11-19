# Managed PCRE2 profile

`Lokad.Utf8Regex.Pcre2` is a removable managed adapter targeting selected
**PCRE2 10.47 standard-matcher** behavior over UTF-8. It owns grammar, options,
captures, replacement and failure types. It does not add a flavor switch to Core
or promise complete native PCRE2 equivalence.

See [release notes](../CHANGELOG.md), [migration](MIGRATION.md),
[delivery evidence](../src/Lokad.Utf8Regex.Pcre2/README.md#delivery-evidence) and the
[API snapshot](../tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt).
Historical receipts retain their original product/runtime identities.

Operation families use their existing token-bearing entry with optional byte
start, match/substitution options and cancellation. Ordinary short source calls
still work; old binary bindings must be recompiled. The retired TryReplace
options-only fifth argument becomes named `substitutionOptions:`. Partial mode
remains explicit. Entry cancellation precedes input/offset/option work. The
unmetered literal shortcut remains available only at byte start zero with no
match options and no cancelable token; finite limits/deadlines and explicit
funding retain their controlled paths. No subject validation policy is added.

## Subject input contract

The host supplies already-valid UTF-8 subjects and keeps borrowed bytes unchanged
throughout use. There is no subject-validity pass, maximum-lookbehind validation
window, input policy or validation token. Validate external bytes at the host's
text boundary. This deliberately differs from native PCRE2's default UTF checking;
benchmark comparisons use native `PCRE2_NO_UTF_CHECK` with the same valid-text
guarantee. Pattern and template validation remain independent.

Malformed subjects have unspecified matching results and no invalid-offset or
rejection promise. Managed bounds checking, original cancellation/deadlines and
useful work/depth/working/result limits remain active. Byte starts must be scalar
boundaries in the original subject; lookbehind and anchors retain prefix context.
Results expose byte endpoints without UTF-16 projection or prefix scans. Explicit
`\C` byte atoms and `\K` can still produce slices requiring their own decoding
checks. Pattern/template preparation and byte-template output controls remain
independent of the subject contract.

Execution strategies are internal. Matching preserves scalar boundaries, capture
priority, global empty/nonempty retries, newline/options and original resource
and interruption authority. Literal mechanisms remain; nonliteral shape programs
and the separate character-sequence parser/executor have been retired in favor of
existing general programs. Shared token semantics remain owned by this profile.
Supported partial probes are prepared from the same admitted semantic tree,
without a second parser or token array. Structurally equivalent noncapturing
literal groups can use the same preparation; unsupported partial shapes still
fail explicitly. PCRE2 multiline caret and end-only-dollar behavior retain profile
execution where Core semantics differ.

Work, depth and covered storage are route-dependent. Numerical requirements and
operating costs can increase or decrease as strategies change. Fixed-scratch or
direct byte-range operations need no mandatory heap loan; major growing buffers
and program expansion still require admission. These controls bound modeled work and admitted storage,
not total heap, stack or process memory.

## Surface and admission

`Utf8Pcre2Regex` supplies IsMatch, Count, Match, persistent memory cursors,
EnumerateMatches, replacement, Analyze, name metadata and Probe.
Compile, match, substitution and probe options are separate types.
There is no RTL or DFA matcher.

The ordinary MatchDetailed entry point is removed. Match fills caller-owned byte
capture slots; persistent cursors publish immutable last-capture snapshots and
named metadata. Keep stable memory for a cursor, and dispose after the required
results or on abandonment. Already-published ranges survive disposal. A caller
with transient spans may copy into stable memory outside regex admission.
First-only nonmonotone K/MARK remains on primary Match; generic iteration keeps
its existing rejection. Actual partial/full Probe contexts and their failure
discriminants remain available, with the same selected drivers and authority.
No public forwarding replacement or validation token is introduced.

MatchMany and Utf8Pcre2MatchData are removed. Stream whole-match values through
EnumerateMatches, or use the memory cursor for suspended capture traversal.
Callers own any buffered prefix and explicitly request each next result; the
engine no longer performs bulk-capacity lookahead or returns isMore. Earlier
values remain valid after later failure. Host buffering is outside regex
admission; the enumerator/cursor keeps its original cumulative work, deadline and
cancellation authority. Input and caller capture output must remain disjoint.

`Match(input, startOffsetInBytes=0, captures=default, matchOptions=None,
cancellationToken=default, workingMemoryAllowance=null)` returns the existing
borrowed whole-match value and optionally fills caller-owned
`Span<Utf8Pcre2CaptureRange>` slots, including zero. A shorter destination cannot
change matching or capture selection. Excess slots stay untouched. On no match or
failure the supplied prefix clears; entry cancellation leaves it unchanged.
Captures retain numeric slot, participation, byte endpoints and UTF-8 slice
validity, including opted-in byte fragments and noncontiguous reported ranges.
Caller-owned slots consume publication work but no producer-owned result-byte
capacity. Matching buffers still require working admission. Null working
admission inherits program funding, or keeps numeric controls when unfunded.
Funded Match retains program use until the synchronous call finishes;
closed expressions reject new calls without changing output. Copied ranges and whole
values remain valid under their original borrowed-input lifetime.

Whole-only matching retains its existing native paths. Capture output uses the
existing byte-range runner and the same publication loop as immutable snapshots,
without allocating a temporary result array. Primary `Utf8Pcre2ValueMatch.Mark`
preserves the last relevant MARK on success and failure, including short capture
destinations. It reuses the VM's already-computed immutable label reference,
without another search, captured-result array or producer-owned text payload.
The reference stays valid after later calls and program close. Full/partial probe
value projections preserve their existing MARK too. Span enumeration publishes
ranges only and does not report MARK; use primary Match or a persistent cursor.
Capture cursors and full probe contexts expose numbered slots. The compiled regex
exports capture names.

Compile an expression once and invoke its instance methods. Static IsMatch,
Match and Replace convenience overloads are removed; they previously compiled a
fresh expression per call. There is no process-wide pattern cache.

Construction requires a successful PCRE2 semantic parse before reusing Core
execution/search. Core-only `\u0041`, `\p{IsGreek}` and balancing-group syntax
are not admitted through a fallback. Unsupported executable coverage fails with
`NotSupportedException`; public `NotImplementedException` is prohibited.
Grammar errors use `Pcre2CompileException`; replacement errors use the dedicated
substitution exception. The completion ledger records intentional operation
support/rejection, rather than inferring it from another engine.

Construction validates common policy, timeout, option bits and settings before
strict pattern validation/admission. Policy construction controls remain separate
from matching; Compile additionally accepts an original construction token and
observes pre-cancellation before argument work. Invalid pattern UTF-8 reports
absolute byte offsets. A match byte floor
must be in [0,input.Length]; text-aware execution requires a valid scalar boundary.

## Grammar and lexical rules

Supported families and operation-specific coverage are recorded in
[profile README](../src/Lokad.Utf8Regex.Pcre2/README.md#supported-scope-and-limitations) and the corpus.
The following rules prevent accepting broader Core syntax or losing PCRE2 scope.

| Area | Contract |
| --- | --- |
| Quantifiers | Finite bounds are at most 65535; minimum cannot exceed maximum. Checks respect quoting, comments, classes and escape operands. |
| Classes | Range endpoints are decoded scalars, including supplementary members; predicates cannot be endpoints. Invalid/reversed ranges use ClassRangeOrder. Leading/trailing literal hyphens keep their meaning. |
| Quoting | `\Q...\E` quotes inside/outside classes. Quote markers and isolated `\E` add no atom. Quoted whitespace remains literal; consuming a closing bracket inside a quote leaves the class unterminated. |
| Verb labels | Recognized labels are literal through their first closing parenthesis. No ALT_VERBNAMES; syntax-looking label text is not regex syntax. Real regex-body verbs retain normal parsing. |
| Extended modes | Comments stop at the configured newline convention. Escaped/quoted hashes and class hashes stay literal. x/xx is lexical: x after xx selects ordinary x, repeated x does not imply xx, and -x clears both. Only xx ignores unescaped class spaces/tabs. |
| Option directives | At most one hyphen; a flag on both sides is disabled. Empty `(?)`, `(?-)` and `(?-:...)` are no-ops. Reset `(?^)` clears supported i/m/n/s/x/xx/r, retains U/J, and disallows a hyphen. Scoped changes restore on exit. |
| Duplicate-name J | Permission is lexical; disabling it rejects later duplicates without deleting existing slots/names. Closing a group restores inherited permission; reset preserves J. |
| DEFINE/subroutines | Definitions retain lexical flags and declaration slots even when uncalled; ending DEFINE restores the caller's flags. Calls restore caller capture state, including previously unset values, on ordinary or ACCEPT return. Calls are not made atomic. |

Default settings forbid duplicate names, forbid `\C`, forbid lookaround `\K`,
and select Default newline/BSR. Branch-reset same-number naming follows its
separate PCRE2 rule.

## Captures and Unicode

`CaptureGroupCount` retains the validated count independently of execution
selection and match participation. It excludes group zero, respects branch-reset
number reuse and counts separate slots sharing a duplicate name. Pattern source
can be copied through `TryCopyPatternUtf8` under the
[caller-buffer metadata contract](SPEC.md#native-pattern-metadata). The funded
owner exposes both members and keeps admission until each access finishes;
copied caller storage remains valid after owner close.
`CopyNumbersForUtf8Name(nameUtf8, destination, out isMore, token)` copies all
matching slot numbers from the duplicate-preserving name table, in table order.
It reports how many fit and whether additional numbers remain. Complete strict
UTF-8 validation precedes writes; unknown names copy zero, malformed bytes throw
with a byte offset. Keys are admitted/encoded once and compared ordinally without
normalization or key strings. Vector lookup is linear with bounded token checks;
matching timeout/numeric limits do not govern this explicit metadata operation.
The funded owner retains its existing use during access. No key alias escapes.
Hosts encode string keys before lookup and can reuse the copied numbers across
matches and probes, including after the compiled expression closes.

Capture slots follow declaration order, including named groups. Names start with
a Unicode letter or underscore, then letters/digits/underscores; numeric names
such as `(?<100>a)` fail with InvalidAfterParensQuery before backend selection.
Capture names are limited to **128 UTF-8 bytes**, verb labels to **255 UTF-8 bytes**.

Cursor snapshots and full probe contexts expose the last value per slot, not Core capture history.
`NameEntryCount` and `TryCopyNameEntryUtf8(index, destination, out bytesRequired,
out groupNumber, token)` export the regex name table as caller-owned bytes.
Indices follow first-declaration order, including duplicate names for distinct
numbers; branch-reset declarations with the same name and number share an entry.
The index is not a capture slot. A short destination is unchanged and reports the
required length and capture number. Original cancellation precedes funded-open
and index checks. Metadata copying is independent of matching limits; exported
bytes remain valid after disposal. To find the first participating capture for a
name, resolve its numbers with `CopyNumbersForUtf8Name`, then inspect the numbered
slots in the returned order and take the first with `Success` set. The same rule
works with caller-owned slots, cursor snapshots and full probe contexts.

Default simple-fold equivalence is symmetric over scalars, including Kelvin,
long S, Greek variants and titlecase partners. Dotted/dotless I remain distinct.
Owned Unicode 16 case data covers BMP equivalence and supplementary pairs,
including Garay; other properties are not all pinned. Inline r restricts
caseless crossing of the ASCII/non-ASCII boundary, including references under
their lexical flags. Explicit class members remain included; r has no effect
without i. POSIX lower/upper under UCP retain their categories when caseless;
without UCP they keep ASCII caseless behavior.

`Xuc` includes $, @, grave accent and valid scalars >= U+00A0; caseless does not
add ASCII K merely because Kelvin is included. Xps/Xsp and UCP space use the
profile's shared space predicate, including NEL and U+180E. Without UCP, generic
and POSIX space remain ASCII; property escapes remain Unicode-aware.

`\X` uses pinned Unicode 16 Grapheme_Cluster_Break, Extended_Pictographic and
Indic_Conjunct_Break data. Long clusters meter bounded scalar blocks.
The profile intentionally retains Unicode Indic-conjunct and
no-break-before-ZWJ rules where native 10.47 differs, including after a regional
indicator. Extended replacement uppercasing of final sigma intentionally yields
uppercase sigma, rather than the recorded native lowercase result.
These are disclosed differences, not full Unicode/native equivalence.
A lone default `\X` under unlimited Boolean matching can answer existence from a
nonempty caller-valid subject and still charge the candidate. Explicit flags,
controlled calls, hooks and range-returning operations continue to scan clusters
under the matching budget.

## Reported ranges and global progress

Results expose reported byte start and exclusive end separately.
`HasContiguousByteRange` requires start <= end. Opted-in lookaround `\K` can
report start > end; byte extraction then throws. Single-match operations remain
supported, while Count, enumeration and replacement reject a yielded
non-monotone result with `NotSupportedException`.

`IsUtf8SliceWellFormed` can be false when explicitly allowed `\C` splits a
scalar. Such ranges expose raw bytes; hosts must check their validity before
decoding them as text.
No result exposes UTF-16 offsets or a UTF-16 projection flag. `\C` consumes one byte,
is forbidden in UTF lookbehind, and retains the caller-valid subject contract.
Successive global matches can expose individual bytes. Byte replacement can
produce invalid UTF-8 in this mode. Template replacement returns bytes only;
the host checks their validity before converting raw byte-token output to text.

Global operations retain consumed range separately from reported `\K` offsets.
After a nonempty consumed match, restart at consumed end. After an empty consumed
match, retry anchored/not-empty-at-start at the same position; if that fails,
anchoring stops iteration or discovery advances to the next scalar boundary.
Caller NotEmpty/NotEmptyAtStart flags apply to every attempt.
Replacement preserves an admitted reset-empty `\K` result that ordinary
enumeration suppresses, as pinned by operation-specific fixtures.

Analyze exposes advisory length, literal and eligibility facts.
Mark reports the last relevant named verb on success, partial or failure.

## Partial probing

Normal matching never returns partial outcomes. Probe has NoMatch, FullMatch and
PartialMatch; partial exposes only substring zero and Mark, not capture groups.

| Mode/route | Supported disposition |
| --- | --- |
| None | Full-match probing |
| Literal/curated partial drivers | Their qualified Soft/Hard decisions |
| Generic Soft full match | Returned when ordinary matching proves FullMatch |
| Other generic partial decisions | NotSupportedException; a full match cannot prove Hard priority, and a marked miss cannot prove NoMatch |

For example, Hard `(?<x>a+)` on `aaa` and Soft
`(*MARK:tag)(?<x>a+)b` on `aaa` reject outside the curated drivers.
Partial plus compile/match EndAnchored throws `Pcre2MatchException` / BadOption
before selection; pre-cancellation still wins. Operational failures throw rather
than becoming a probe kind.

## Substitution

Use PCRE2 substitution grammar, not .NET parsing. Default forms are `$$`,
`$n`, `${n}`, `$0`, `$&`, prefix/suffix, `$_`, `$+`, `$*MARK`,
`${*MARK}` and `$<name>`. Backslash processing requires Extended.
Replace/TryReplace run left-to-right and reject non-monotone successful ranges.

Replace and TryReplace take strict UTF-8 templates. They validate the complete
template before route selection, including with no match or SubstituteLiteral.
Literal fragments, escapes and selectors parse directly from bytes. Syntax errors
remain deferred instructions, preserving the no-match and unselected fallback rules.

Warm byte-template cache queries borrow the supplied span. Cold keys are copied
after major-buffer admission; their temporary working loan overlaps parser/output
storage and refunds with the operation. Only Extended separates grammar variants
in the bounded cache. Other substitution flags keep their execution meanings.
Construction uses byte extents with work, instruction and byte backstops.
These modeled costs are not stable
per-template quotas or aggregate host-retention guarantees.

Discovery, actual yielded captures/Mark, output growth and final byte copying
share one invocation. Callback/evaluator replacement and the shared public writer
are removed. Hosts compute custom replacements over `CreateMatchCursor` ranges
and capture memories. Each advance retains the regex controls; host computation,
buffers, final cancellation checks and admission belong to the host. Ordinary
cursor progression retains its documented reset-empty disposition; byte-template
replacement retains its separate PCRE2 substitution disposition for `\K`.

TryReplace supports overlapping output by staging where required. Destination
shortage leaves it unchanged with zero bytes written, or the required length
when SubstituteOverflowLength is selected; other failures throw.
See the [Core writer/output contract](SPEC.md#results-splitting-and-replacement)
for ownership and exclusions.

## Interruption and resource controls

Execution twins preserve the original token. Checks cover bounded input inspection,
search/VM/grapheme work, projection, byte output and publication.
One absolute deadline spans lazy advances and controlled preparation.
Pre-cancellation wins; timeout reports `Pcre2MatchException` / Timeout.

| Legacy execution limit | Zero | Breach |
| --- | --- | --- |
| MatchLimit | Unlimited | MatchLimit |
| DepthLimit | Unlimited | DepthLimit |
| HeapLimitInBytes | Unlimited | HeapLimit |
| ResultLimitInBytes | Unlimited | ResultLimit |

Create maps positive nullable common policies to these limits; null becomes zero,
common zero rejects, and work values above uint.MaxValue reject. Units depend on
the managed route, not native PCRE2. Proved search/count shortcuts can change work
counts or avoid VM workspace; exact denial thresholds are not version-stable.
Complete positive-minimum, unbounded greedy ASCII class-run Count also supports
finite policies, live tokens, deadlines and caller working allowances. It charges
each candidate and at most 4096 bytes per scan block, using the original shared
budget and publication checks. The counter needs no scratch or result storage:
it makes no working-allowance loan, and depth/working/result ceilings need not
reject work that acquires none of that storage. Existing shared denials remain
authoritative, including for an empty stream. Positive, nonnegated unions of
ASCII ranges also admit mixed valid UTF-8: their members are scalar-aligned ASCII
bytes and all bytes of other scalars are excluded. This needs no subject ASCII
classification or VM workspace. Eligibility scans immutable class terms with interruption/work checks
at most 64 additional terms apart, without another retained program flag. An empty
suffix is already ASCII and needs no such proof. Modeled work and working-state
requirements can decrease; a large pattern's eligibility remains bounded.
Other classes on non-ASCII subjects, lazy/bounded/caseless forms,
anchoring, match flags and diagnostic hooks retain their runners.
Eligible standalone positive, case-sensitive `\p{Sm}` Count reuses Core's scalar
category counter under the original invocation, including finite policies, live
tokens, deadlines and caller working allowances. It admits at most 4096 bytes
ending at a scalar boundary per block and checks publication, even for unlimited
calls. No capture or VM workspace is acquired. Numerical work and working-state
requirements can decrease; other categories, negation, caseless/anchored forms
and nondefault match options retain their existing selection.
Class/delimiter Count windows can reject an unproved prefix using the original
scalar predicate before VM admission. Classification retains bounded work and
the original token/deadline; viable starts retain complete verification. Skipped
attempts can reduce required work, depth and heap capacity.
Controlled Count of eligible nonempty, unanchored literals searches windows of
at most 2,048 bytes and charges long-literal comparisons separately. It keeps
the original token and deadline and acquires no capture or result buffer.
Disproved plain-alternative branches need no frame storage. For example, Count
of `cat|horse` can succeed with a one-byte heap limit; nonlinear paths that need
workspace still require admission. This limit does not cover total managed memory.
Fused scalar repeats can also execute without unused counter vectors. The first
counter-dependent repeat admits its complete state before acquisition; retries
reuse it under the same synchronous invocation lifetime. A nonpossessive optional
literal or class can use an ordinary branch without a repeat counter. Required
capture and assertion storage remains admitted. Work and heap requirements can fall.
Uncontrolled compilation can also omit rewind state for positive word-only repeats
before `\b`; explicit finite execution policies retain their established lowering.
Original tokens and working allowances still meter/admit actual optimized work.
Proved unbounded repeats excluding an ASCII pair can scan checked blocks of at
most 2048 bytes on caller-valid subjects, retaining scalar endpoints and
bounded work/interruption checks. This can reduce route-dependent work charges.
Boolean slice searches can reuse a compiler-proved first ASCII byte to skip
impossible starts. Each scan admits at most 2048 bytes, with original-token and
deadline checks even on unlimited calls. Actual candidates retain normal VM and
capture checks. Nullable, contiguous-origin and compile-anchored programs retain
their existing search. Other public operations and non-None match options keep
their existing paths. Skipped attempts reduce route-dependent work costs; these
scans retain their own admission and normal publication checks.
Depth admits live frames/assertion state
before growth. Working admission includes major workspace/output growth overlap;
result admission cumulatively covers owned capture/probe roots, staging and
returned bytes without refunds. Fixed values/inline partials need no result
payload. Host decoding, caller storage/retention, custom computation and metadata have
separate scope; these are not total heap/RSS bounds.

Copied value cursors retain independent positions and shared allowance/deadline.
First denial stays sticky; refused growth leaves reservations unchanged.
Exclusive scratch releases synchronously; retained values/aliases survive.
Byte/status publication remains metered. Capture arrays receive working/result
admission before allocation, with bounded interruption checks while filling them.
There is no coordinate endpoint table, sorting, prefix scan or per-cursor UTF-16
cache. Removing that work/storage changes numerical budget requirements; the
work/depth/working/result guarantees and interruption authority stay enforced.

Explicit Compile uses shared construction policy/timeout exceptions, independent
of invocation limits. Major buffers, instructions, AST/name metadata and expansion
are admitted conservatively before allocation. Backtracking/lexical construction
has a fixed 256-frame ceiling, tightened by the caller. Separate construction
backstops and opaque CLR/JIT exclusions remain. Cold finite-heap replacement
preparation holds its additional peak reservation through output; its checkpoints
also spend invocation work. Warm plans use their bounded cache. See
[construction contract](SPEC.md#construction-preparation-and-caches).

Under the existing shared-slice compatibility checks, Count alone can reuse the
constant-space scalar-run counter for a complete positive greedy repeat with
plain capture markers. Its count-only proof examines at most 64 instructions;
the original scalar predicates, input policy, cancellation/deadlines and useful
limits remain. Avoiding unused capture/VM storage can lower numerical work,
depth and working requirements. Other operations and existing native fast paths
retain their selection; nondefault match options keep their owned PCRE2 continuation.

Bounded ASCII Boolean plans can include subroutine calls whose authoritative
lowered body is exactly capture-start, one eligible ASCII token, capture-end and
return. They reuse the existing 64-alternative/128-token limits and construction
admission, without recursive expansion. Controlled matching, match flags and capture-returning operations retain the
owned VM and their original interruption/resource authority.
The same Boolean plans can filter nonempty ASCII conditional branches guarded
by one positive/negative lookahead token, including plain capture wrappers.
Intersecting each branch's first class with the guard or its ASCII complement
preserves the finite language without retaining an assertion executor. A terminal
unnamed `(*ACCEPT)` wrapper around that token has the same condition truth and
can use this proof; capture results still retain the PCRE2 conditional capture
flow. Empty branches, lookbehind, multi-token/Unicode guards and other control
verbs keep the VM; named ACCEPT guards and capture-dependent syntax remain
ineligible. Existing construction caps apply.
Paired outer `\b` assertions can surround a nonempty bounded ASCII language
whose first and last classes contain only word bytes. With default non-UCP
semantics, the existing Boolean runner checks their outside neighbors; Unicode
neighbors remain nonword in that mode. Interior assertions, mixed/nonword
endpoints and UCP keep their established executors. Flags, controlled calls and
capture-returning operations retain the owned VM.

Constructors and the two `Compile` representations accept immutable
`Utf8RegexPolicy` and return disposable `Utf8Pcre2Regex`, funded or not. Options
and PCRE2 compile settings remain independent inputs. Shared positive resource
fields map to the existing PCRE2 limits; work above uint.MaxValue rejects.
`ResourcePolicy` and `DefaultExecutionLimits` are read-only projections; zero in
the latter denotes unlimited. Policy field errors name `policy`. Null match
timeout uses DefaultMatchTimeout; null construction timeout is infinite.
Compile additionally accepts an original construction token, checked before policy/
pattern work and not retained for later matching. Default construction inherits
an existing authority; explicit construction/funding cannot replace an active owner.
No separate Create factory or funded facade remains. Compilation
uses the existing shared allowance before substantial acquisition, without an
extra private byte ceiling when construction MaxBytes is null. Fixed work/depth/
instruction backstops remain. Byte patterns are strictly validated, copied into
admitted owned storage and parsed directly by the existing PCRE2 grammar. Caller
reuse after compilation cannot alter the program. `Pattern` decodes on explicit
request and does not cache that string. Selected names, properties and diagnostic
operands may project text; compilation does not require a whole-pattern string.
Native construction work/extent charges use source bytes; string entries retain
UTF-16 source units. Coarse AST admission can therefore be larger for native
Unicode sources. Exact internal charges are not a stable heap-size contract.

Funded program admission also reserves the existing replacement cache's maximum
payload and metadata, even before its first use. This is a conservative per-program
loan (currently 4MiB payload plus bounded metadata), not additional eager cache
allocation. It intentionally raises the modeled program admission required by
earlier owners. Cache publication/eviction needs no additional entry lease or
funding transfer. Ordinary expressions retain their existing cache bounds.

IsMatch, Match, Count, persistent cursor creation and destination replacement
expose an optional shared working allowance. Null inherits program funding, or
keeps numeric controls when unfunded; an explicit account overrides working only.
A shared account fences unmetered literal shortcuts. Numeric limits, original
cancellation and PCRE2 timeout/limit errors still apply. Shared refusal throws
`Utf8MemoryAdmissionException` synchronously and never retries elsewhere.
Covered temporary loans refund on success, failure and interruption. Borrowed
input, fixed invocation metadata and caller retention are separate scopes.

Funded Dispose rejects new program-dependent calls/metadata and refunds after the
final active use; admitted synchronous operations finish without re-entering a
closed public gate. It does not wait, cancel or reclaim through GC. Disposal is a
no-op without funding. Pattern/options/settings/timeouts/resource projections
remain readable. Closing ends participating program admission; host retention of
closed instances and published results is outside that loan and does not force
CLR reclamation. Existing persistent cursors retain program admission through
EOF/failure/Dispose and preserve their aliases/retained range snapshots after close.

Newly funded span enumerators borrow an open producer at creation and each
advance, release its use between advances and hold no host working loan. Copies
keep independent positions and their original shared cumulative numeric budget
and absolute deadline. Close rejects later advances, including ended copies;
original entry cancellation takes precedence. Current byte endpoints, fragment
status and borrowed values remain valid with unchanged input. Use the persistent
cursor for traversal surviving close or shared working admission. Probe, detailed
metadata and owned output retain numeric controls and admitted program
use, without adding working-overload symmetry or changing drivers/MARK/partials.

`Utf8Pcre2Regex.TryReplace` retains the program's admission and accepts
byte starts, substitution/match options, the current token and an optional separate
working allowance. Cold key/parser arrays and live output borrow the same working
ledger before allocation and remain admitted through final copying. An unlimited
numeric heap ceiling does not disable host admission. Covered temporary loans
refund synchronously on completion, refusal or interruption; refusal is terminal.
Construction backstops, original deadlines and cumulative match/depth/heap/result
controls remain. Warm cache queries borrow the source and avoid key copying.

The literal destination driver remains in use. Detailed/native-value output copies
directly from its live writer, avoiding an intermediate published byte array and
its funding gap. Non-funded destination calls use the same output completion.
Scalar captures, Extended transformations, lazy errors on no match/unselected
branches, aliases and overflow-length reporting retain their profile semantics.
Removing an intermediate allocation can admit formerly refused tight result caps;
numeric charges remain route/version dependent. Copied output belongs to the caller
and survives expression disposal. No new retained-output receipt or string overload is
required for this funded route.

`CreateMatchCursor(ReadOnlyMemory<byte>, byteStart, matchOptions, token, workingAllowance)`
on expressions and program owners returns `Utf8Pcre2MatchCursor` over already-valid
unchanged text. No validity pass precedes cursor creation. Explicit starts
are scalar-aligned bytes; prefix/lookbehind/anchor context remains the original
subject. Sequential advances
can cross awaits under the creation token, absolute deadline and cumulative limits.
An advance token applies only to that synchronous call; if both cancel, the
original token wins. Aliases share position/disposal and reject concurrent or
reentrant access. Dispose early abandonment; there is no finalizer. Scalar slots/
workspace retain their admitted high-water capacity while positioned, including
across host waits; other lexical VM scratch releases per advance. This can retain
working admission longer than standalone calls. EOF, failure and Dispose drop
exclusive roots and release all remaining loans/program use synchronously.

`MoveNext(out Utf8Pcre2RangeMatch)` returns immutable numbered last-capture byte
slots and MARK. Existing native literal/character/repeat finders remain eligible;
captured cases use existing scalar or PCRE2 VMs with a final byte result shape.
PCRE2 flags retain their existing admitted VM route; empty retries can borrow
the scalar runner's anchored nonempty attempt under the original anchor origin.
Global progression uses consumed ranges, preserving `\K`, same-position nonempty
retry and CRLF/Anchored behavior. Non-monotone iteration remains unsupported.
Opted-in `\C` may return scalar fragments: inspect `IsUtf8SliceWellFormed` before
decoding; byte access itself remains available. No full capture history is added.
These range results do not provide a reusable scalar-coordinate index.

Standalone value, group, match and partial-match views no longer expose
`GetValueString`. Use `GetValueBytes` on a value/group, or `Value.GetValueBytes`
on a match context. Hosts decode selected well-formed slices outside regex
resource/interruption controls. `HasContiguousByteRange`, slice well-formedness,
byte endpoints and noncontiguous byte-access errors retain their meanings.

Range metadata survives later advances and cursor/program close. Subject bytes
and their memory manager remain borrowed. Ordinary publication ends producer
working admission for results; continued host retention has separate scope.
`MoveNextRetained` requires shared working funding and returns a disposable
`Utf8Pcre2RetainedRangeMatch`. Default publication transfers the existing capture/
metadata loan without reacquisition. A separate result account admits its charge
before working staging refunds, including conservative overlap without a funding
gap. The result charge includes its capture array and receipt, plus MARK text if
present. Program admission covers capture names. Refusal is terminal. Receipt
close refunds once; copied ranges remain valid afterward, with
continued admission then the caller's responsibility. Numeric result traffic is
cumulative regardless of live refunds. Native pattern compilation, source copying
and UTF-8 template preparation are implemented. Funded caller-destination replacement
is implemented; final package/consumer acceptance remains separate delivery work.

## Isolation and non-goals

Template `ReplaceToString` overloads are removed. `Replace` with a string or UTF-8
template and `TryReplace` preserve substitution semantics and byte output controls.
Hosts decode valid byte results explicitly; that string allocation and any final
host cancellation check fall outside the regex invocation. Callback replacement
and evaluator delegates are removed as well; use the range cursor for host output.

All PCRE2-specific implementation/types/namespaces/test fixtures remain in the
Pcre2 slice. Unavoidable Core hooks carry PCRE2-INTEGRATION-POINT.
Core loads independently. Only genuinely shared low-level infrastructure is
shared; no generic flavor framework is required.

No native PCRE2, P/Invoke, NativeLibrary, external executable or RID-native assets
are shipped. Core is the only non-BCL implementation dependency. Callouts, the
DFA matcher, invalid-UTF mode, split and full capture history are outside this
profile. Broader completion is separate from Core/PythonRe delivery.

Public examples live in the [package README](../src/Lokad.Utf8Regex.Pcre2/README.md).
The existing safety fixture executes this denial example:

```csharp
var regex = new Utf8Pcre2Regex("foo", policy: new Utf8RegexPolicy
{
    ResourcePolicy = new() { MaxWorkSteps = 1 }
});
Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8));
```
