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
The owned backtracker checks the original invocation between blocks of long
backreference comparisons, including exact bytes and scalar case folding.

## Surface and admission

`Utf8Pcre2Regex` supplies IsMatch, Count, Match, persistent memory cursors,
EnumerateMatches, replacement, name metadata and Probe.
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
Boolean and range-returning operations share the cluster decoder.

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

### Construction

Constructors and `Compile` accept `Utf8RegexPolicy` separately from PCRE2 options
and compile settings. Invalid policy values name `policy`. `Compile` checks its
construction token before policy and pattern work. `ConstructionTimeout` defaults
to infinite. Construction authority ends when the program is published.
A constructor using default construction settings can borrow an active scope.
An explicit construction scope cannot replace an active owner.

Both pattern representations use the [shared construction contract](SPEC.md#construction-preparation-and-caches)
for admission and failures. PCRE2 also caps backtracking and lexical construction
at 256 live frames, which the caller can tighten. A funded build with no explicit
`MaxBytes` uses its shared account for covered bytes. The other construction
backstops still apply. Byte patterns are validated and copied before parsing.
Construction work and extents use source bytes for byte patterns and UTF-16 units
for string patterns. `Pattern` decodes byte source on request.

### Invocation

A pre-canceled token wins before argument validation or program access.
Interruption checks cover bounded search, matching, rollback, projection, copying
and publication. Cancellation throws `OperationCanceledException` with the token
that triggered it. One absolute deadline covers execution, including cold
replacement preparation and waits between cursor advances. Expiry throws
`Pcre2MatchException` with `ErrorKind` of `Timeout`. A null `MatchTimeout` uses
`Utf8Pcre2Regex.DefaultMatchTimeout`, initially infinite.

`ResourcePolicy` exposes the selected `Utf8ResourcePolicy`.
`DefaultExecutionLimits` is its read-only PCRE2 projection. Each policy field
accepts a positive value or null for unlimited. Null projects to zero.
`MaxWorkSteps` cannot exceed `uint.MaxValue`. A numeric denial throws
`Pcre2MatchException` with the corresponding `ErrorKind`.

| Policy | Projected limit | Covered scope | Error kind |
| --- | --- | --- | --- |
| `MaxWorkSteps` | `MatchLimit` | Cumulative modeled work, including scanning, retries and publication | `MatchLimit` |
| `MaxDepth` | `DepthLimit` | Live backtracking, subroutine and assertion depth | `DepthLimit` |
| `MaxWorkingBytes` | `HeapLimitInBytes` | Live major workspace and output buffers, including growth overlap | `HeapLimit` |
| `MaxResultBytes` | `ResultLimitInBytes` | Cumulative covered result and staging storage | `ResultLimit` |

The limits bound modeled capacities. Borrowed input, caller destinations, host
retention and decoding, standalone metadata queries, and incidental allocations
fall outside the execution limits. Fixed values and inline partial results can
require no result payload. Scanning repeat state at subroutine entry consumes
work steps even when the slots are inactive. Which storage and work an operation
needs depends on the pattern, route and library version. Exact thresholds can
differ from native PCRE2. Failed admission leaves existing reservations unchanged.
Sticky denials remain in effect across copies of a span enumerator.

Active subroutine calls and pending retries share admitted call records. A
retry retains the call state it can restore. Returning from a call or cutting
retries releases the unused record slots for reuse within the invocation.
Whole-value scalar search borrows the original invocation. It only admits
capture slots when matching reads them. Calls that return captures still
admit the required capture state and result storage.

### Program and working allowances

Set `ProgramMemoryAllowance` to admit covered program storage, including a
conservative reservation for the bounded replacement cache. The cache reservation
covers 4 MiB of payload plus bounded metadata without eager buffer allocation.

`IsMatch`, `Match`, `Count`, `CreateMatchCursor` and `TryReplace` accept an optional
working allowance. Null inherits the program account if present. An explicit
account overrides working admission only. Covered acquisitions must pass both
numeric and shared admission. `Utf8MemoryAdmissionException` reports shared
refusal synchronously. Refusal ends the operation and temporary loans refund on
success, failure or interruption. Probe and owned-byte `Replace` retain numeric
controls and admitted program use.

Cold `TryReplace` key and parser buffers use the working allowance within the
original deadline. Their covered storage remains admitted through output copying.
Warm cache lookup borrows the key bytes. Template preparation also retains its
construction backstops. [Substitution](#substitution) describes output behavior.

Disposal of a funded expression rejects new program-dependent calls and metadata
queries. Admitted synchronous calls can finish, and persistent cursors keep their
program use until EOF, failure or disposal. The account refunds after the last
admitted use. Pattern, options, compile settings and policy projections remain
readable. Disposal is a no-op on an unfunded expression.

### Cursor and result lifetimes

Span enumerator copies have independent positions and share the cumulative
numeric budget and deadline. Creation and each advance borrow an open funded
expression. They release that use between advances and carry no shared working
loan. Expression close prevents further advances, including on ended copies.
Entry cancellation still wins. Published values remain valid while the subject
lives unchanged.

A memory cursor retains program admission through expression close. Its aliases
share position and disposal. Concurrent or reentrant access is rejected. The
creation token, absolute deadline and cumulative limits apply across advances,
including host waits. An advance token applies only to that call. The creation
token takes precedence if both are canceled. The cursor may retain charged
workspace between advances. EOF, failure or disposal releases exclusive scratch
and program use synchronously. Dispose an abandoned cursor explicitly.

`MoveNext` publishes immutable last-capture ranges and MARK. They survive later
advances and cursor or program close. The input memory and its manager remain
borrowed. Use `GetValueBytes` on the value or group and inspect
`IsUtf8SliceWellFormed` before decoding an opted-in byte fragment.

`MoveNextRetained` requires shared working funding and returns a disposable
`Utf8Pcre2RetainedRangeMatch`. The result charge covers its capture array,
receipt and MARK text, when present. Capture names remain covered by the program.
The working loan can transfer to the receipt. A separate result account must
admit the charge before working staging refunds. Refusal is terminal. Disposal
refunds once, while existing range copies remain valid under the caller's
retention responsibility. Numeric result traffic remains cumulative after refunds.

## Isolation and non-goals

`Replace` and `TryReplace` take UTF-8 templates and produce byte output.
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
using var regex = new Utf8Pcre2Regex("foo", policy: new Utf8RegexPolicy
{
    ResourcePolicy = new() { MaxWorkSteps = 1 }
});
var error = Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8));
Assert.Equal(Pcre2ErrorKind.MatchLimit, error.ErrorKind);
```
