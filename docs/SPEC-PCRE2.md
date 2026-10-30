# Managed PCRE2 profile

`Lokad.Utf8Regex.Pcre2` is a removable managed adapter targeting selected
**PCRE2 10.47 standard-matcher** behavior over UTF-8. It owns grammar, options,
captures, replacement and failure types. It does not add a flavor switch to Core
or promise complete native PCRE2 equivalence.

See [release notes](../CHANGELOG.md), [migration](MIGRATION.md),
[qualification](../src/Lokad.Utf8Regex.Pcre2/QUALIFICATION.md) and the
[API snapshot](../tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt).
Historical receipts retain their original product/runtime identities.

## Surface and admission

`Utf8Pcre2Regex` supplies IsMatch, Count, Match/MatchDetailed,
EnumerateMatches, MatchMany, replacement, Analyze, name metadata and Probe.
Compile, match, substitution and probe options are separate types.
There is no RTL or DFA matcher.

Construction requires a successful PCRE2 semantic parse before reusing Core
execution/search. Core-only `\u0041`, `\p{IsGreek}` and balancing-group syntax
are not admitted through a fallback. Unsupported executable coverage fails with
`NotSupportedException`; public `NotImplementedException` is prohibited.
Grammar errors use `Pcre2CompileException`; replacement errors use the dedicated
substitution exception. The completion ledger records intentional operation
support/rejection, rather than inferring it from another engine.

Create validates common policy first; constructors validate timeout, option bits
and settings before strict pattern validation/admission. Explicit Compile accepts
separate construction policy/timeout/token and observes pre-cancellation before
argument work. Invalid UTF-8 reports absolute byte offsets. A match byte floor
must be in [0,input.Length]; text-aware execution requires a valid scalar boundary.

## Grammar and lexical rules

Supported families and operation-specific coverage are recorded in
[QUALIFICATION.md](../src/Lokad.Utf8Regex.Pcre2/QUALIFICATION.md) and the corpus.
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

Capture slots follow declaration order, including named groups. Names start with
a Unicode letter or underscore, then letters/digits/underscores; numeric names
such as `(?<100>a)` fail with InvalidAfterParensQuery before backend selection.
Capture names are limited to **128 UTF-8 bytes**, verb labels to **255 UTF-8 bytes**.

Detailed results expose the last value per slot, not Core capture history.
`CopyNameEntries` and `CopyNumbersForName` expose the name table;
`TryGetFirstSetGroup` returns the first set slot for a name. It does not invent
Core numeric-name aliases or sparse numeric definitions.

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
Indic_Conjunct_Break data. Long clusters meter bounded scalar blocks, separately
from validation. The profile intentionally retains Unicode Indic-conjunct and
no-break-before-ZWJ rules where native 10.47 differs, including after a regional
indicator. Extended replacement uppercasing of final sigma intentionally yields
uppercase sigma, rather than the recorded native lowercase result.
These are disclosed differences, not full Unicode/native equivalence.

## Reported ranges and global progress

Results expose reported byte start and exclusive end separately.
`HasContiguousByteRange` requires start <= end. Opted-in lookaround `\K` can
report start > end; byte extraction then throws. Single-match operations remain
supported, while Count, enumeration, MatchMany and replacement reject a yielded
non-monotone result with `NotSupportedException`.

`HasUtf16Projection` can be false when explicitly allowed `\C` splits a
scalar. Such ranges reject UTF-16/string extraction. `\C` consumes one byte,
is forbidden in UTF lookbehind, and never disables whole-subject strict validation.
Successive global matches can expose individual bytes. Byte replacement can
produce invalid UTF-8 in this mode; string replacement rejects invalid output.

Global operations retain consumed range separately from reported `\K` offsets.
After a nonempty consumed match, restart at consumed end. After an empty consumed
match, retry anchored/not-empty-at-start at the same position; if that fails,
anchoring stops iteration or discovery advances to the next scalar boundary.
Caller NotEmpty/NotEmptyAtStart flags apply to every attempt.
Replacement preserves an admitted reset-empty `\K` result that ordinary
enumeration suppresses, as pinned by operation-specific fixtures.

MatchMany fills at most the destination length and reports `isMore`; it is not
a continuation protocol. Only the returned prefix is valid. An empty destination
still probes. Input/result storage must be disjoint: this is an **unchecked caller
precondition**, not an overlap exception guarantee. Analyze exposes advisory
length/literal/eligibility facts, not an executable plan or validation exemption.
Mark reports the last relevant named verb on success, partial or failure.

## Partial probing

Normal matching never returns partial outcomes. Probe has NoMatch, FullMatch and
PartialMatch; partial exposes only substring zero and Mark, not capture groups.

| Mode/route | Supported disposition |
| --- | --- |
| None | Ordinary detailed matching |
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

Discovery, actual yielded captures/Mark, callback context, output growth and final
copy/decode share one invocation; callbacks do not rematch at reported starts.
Evaluators must be non-null, including misses, after pre-cancellation.
The shared callback writer accepts scoped stack spans and is valid only during
the callback. Arbitrary callback execution cannot be preempted or charged as
library allocations.

TryReplace supports overlapping output by staging where required. Destination
shortage leaves it unchanged with zero bytes written; other failures throw.
See the [Core writer/output contract](SPEC.md#results-splitting-and-replacement)
for ownership and exclusions.

## Interruption and resource controls

Execution twins preserve the original token. Checks cover validation, bounded
search/VM/grapheme work, projection, output, callback boundaries and publication.
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
Depth admits live frames/assertion state
before growth. Working admission includes major workspace/output growth overlap;
result admission cumulatively covers owned capture/probe roots, staging and
returned bytes/text without refunds. Fixed values/inline partials need no result
payload. Standalone getters, caller storage/retention, callbacks and metadata have
separate scope; these are not total heap/RSS bounds.

Copied value cursors retain independent positions and shared allowance/deadline.
First denial stays sticky; refused growth leaves reservations unchanged.
Exclusive scratch releases synchronously; retained values/aliases survive.
Required UTF-16 projection is metered and incremental where available, without
a subject-wide map for slice enumeration/MatchMany.

Explicit Compile uses shared construction policy/timeout exceptions, independent
of invocation limits. Major buffers, instructions, AST/name metadata and expansion
are admitted conservatively before allocation. Backtracking/lexical construction
has a fixed 256-frame ceiling, tightened by the caller. Separate construction
backstops and opaque CLR/JIT exclusions remain. Cold finite-heap replacement
preparation holds its additional peak reservation through output; its checkpoints
also spend invocation work. Warm plans use their bounded cache. See
[construction contract](SPEC.md#construction-preparation-and-caches).

`Compile(..., token, programMemoryAllowance)` returns a disposable
`Utf8Pcre2RegexProgramOwner` for text or strict UTF-8 pattern input. Compilation
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

The owner exposes Boolean matching and Count with byte starts, PCRE2 match flags
and each call's cancellation token. Temporary storage uses program funding by
default; an explicit working allowance selects a separate host account. Ordinary
expressions also accept shared working admission through their full IsMatch/Count
signatures. Existing numeric limits, strict whole-subject validation, original
cancellation and PCRE2 timeout/limit errors still apply. Shared refusal throws
`Utf8MemoryAdmissionException` synchronously and never retries elsewhere.
Covered temporary loans refund on success, failure and interruption. Borrowed
input, fixed invocation metadata and caller retention are separate scopes.

Program Dispose rejects new calls and drops the expression root before the final
refund; active calls keep their local root and charge until they finish. It does
not wait, force cancellation or reclaim through GC. The underlying expression
cannot escape. The owner also creates persistent range cursors; funded replacement
remains unfinished. Existing copied span cursors keep their independent-position/
alias contract.

`CreateMatchCursor(ReadOnlyMemory<byte>, byteStart, matchOptions, token, workingAllowance)`
on expressions and program owners returns `Utf8Pcre2MatchCursor`. The entire
unchanged subject is validated once. Explicit starts are scalar-aligned bytes;
prefix/lookbehind/anchor context remains the original subject. Sequential advances
can cross awaits under the creation token, absolute deadline and cumulative limits.
An advance token applies only to that synchronous call; if both cancel, the
original token wins. Aliases share position/disposal and reject concurrent or
reentrant access. Dispose early abandonment; there is no finalizer. Scalar slots/
workspace retain their admitted high-water capacity while positioned, including
across host waits; other lexical VM scratch releases per advance. This can retain
working admission longer than standalone calls. EOF, failure and Dispose drop
exclusive roots and release all remaining loans/program use synchronously.

`MoveNext(out Utf8Pcre2RangeMatch)` returns immutable last-capture byte slots,
duplicate-preserving names and Mark, without captured text or mandatory UTF-16
projection. Existing native literal/character/repeat finders remain eligible;
captured cases use existing scalar or PCRE2 VMs with a final byte result shape.
PCRE2 flags retain their existing admitted VM route; empty retries can borrow
the scalar runner's anchored nonempty attempt under the original anchor origin.
Global progression uses consumed ranges, preserving `\K`, same-position nonempty
retry and CRLF/Anchored behavior. Non-monotone iteration remains unsupported.
Opted-in `\C` may return scalar fragments: inspect `IsUtf8SliceWellFormed` before
decoding; byte access itself remains available. No full capture history is added.
Optional reusable scalar-coordinate projection remains unfinished.

Range metadata survives later advances and cursor/program close. Subject bytes
and their memory manager remain borrowed. Ordinary publication ends producer
working admission for results; continued host retention has separate scope.
`MoveNextRetained` requires shared working funding and returns a disposable
`Utf8Pcre2RetainedRangeMatch`. Default publication transfers the existing capture/
metadata loan without reacquisition. A separate result account admits its charge
before working staging refunds, including conservative overlap without a funding
gap. Shared names/mark text count again for independent retention. Refusal is
terminal. Receipt close refunds once; copied ranges remain valid afterward, with
continued admission then the caller's responsibility. Numeric result traffic is
cumulative regardless of live refunds. Native pattern compilation and source
copying are implemented; UTF-8 template preparation remains unfinished.

## Isolation and non-goals

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
var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1 };
var regex = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8));
```
