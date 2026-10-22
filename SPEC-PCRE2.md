# Side-by-side PCRE2 support in `Lokad.Utf8Regex`

Staged packages are **0.4.0-rc.1** with exact Core dependencies; development is
**0.4.0-rc.2-dev**. See [qualification](src/Lokad.Utf8Regex.Pcre2/QUALIFICATION.md),
[migration](MIGRATION.md) and [host integration](HOST-INTEGRATION.md).
Historical scalar-candidate narratives/receipts remain in
[git](https://github.com/Lokad/Utf8Regex/blob/46318565/SPEC-PCRE2.md) and local ignored
PLAN.HISTORY.md. This contract does not imply complete upstream equivalence or
requalify historical measurements.

### 1. Top-level decision

Keep the existing `Utf8Regex` type bound to **strict UTF-8 Unicode-scalar
matching**, with byte coordinates and derived UTF-16 interoperability. Exact .NET
code-unit matching is superseded. Add the removable sibling `Utf8Pcre2Regex` for
**PCRE2 standard matcher semantics** on UTF-8 input; retain explicit opt-ins for
its isolated byte features. Do not add a flavor flag or a shared option bag to the
core. Core capture history and PCRE2 last-capture/duplicate-name rules retain their
own contracts; .NET is an overlapping test/benchmark comparator only.

Development after frozen RC1 requires a successful PCRE2 semantic parse before
reusing Core execution/search. If all profile compilers decline, construction
throws `NotSupportedException`; the original text is not accepted merely because
it compiles as Core grammar. Undocumented .NET-only forms such as `\u0041`,
`\p{IsGreek}` and balancing groups are no longer accepted. Existing shared fast
paths retain their profile eligibility proofs. PCRE2 physical capture numbering
and duplicate-name rules apply, without Core's named-group numbering or aliases.

`Create` validates its resource policy first. PCRE2 constructors then validate
match timeout, option bits and compile settings, before pattern-byte decoding and construction
admission. This preserves string-entry error precedence while making invalid
arguments win over malformed byte patterns or admission denial. Pattern-dependent
checks still run after strict decoding. Finite invocation controls alone do not
bound pattern construction.

Explicit `Utf8Pcre2Regex.Compile` text/UTF-8 factories
accept `Utf8ConstructionPolicy`, a separate construction timeout and cancellation
token. Pre-cancellation precedes argument/pattern work; construction policy,
execution policy and construction timeout are validated before the existing
constructor argument checks. One synchronous numeric construction owner covers
admitted eager decoding, parsing and lowering, plus the final publication check.
Nested compilation inherits that owner. Positive caps tighten the fixed backstops;
they do not relax them. Explicit compilation cannot replace an active owner.
The token and construction deadline are not retained for later matching or lazy
replacement preparation. Existing constructors and `Create` retain their defaults.

Construction refusal uses shared `Utf8ResourceLimitExceededException` with a
`Construction*` kind, cancellation preserves the original token, and construction
timeout uses `RegexMatchTimeoutException` with the construction duration and empty
`Input`. Its `Pattern` is the supplied text, or empty for byte inputs to avoid
decoding solely for an exception. PCRE grammar errors retain `Pcre2CompileException`;
later match failures retain `Pcre2MatchException`. Strict byte validation and both
decode passes checkpoint scalar-aligned blocks of at most 4096 bytes; malformed
input retains its absolute byte offset. The original work charges are preserved.

Pattern validation admits its text traversal before scanning and retains bounded
original-token/deadline checkpoints. Candidate presence skips irrelevant policy
passes. Group-scope vectors admit capacity and old/new growth overlap; lookaround
policy scans combine option state and assertion context in one byte vector.
Prospective lexical nesting obeys the caller's depth ceiling and the existing
256-frame PCRE2 backstop before growth. Comments, escaped operands, option flags,
POSIX tokens and control-verb searches retain bounded scans. Conservative charges
and earlier construction refusal are intentional. These limits do not cover every
incidental helper, CLR/JIT metadata, lazy preparation, retention or process memory.

Core preparation is skipped when profile options make every applicable Core route
ineligible. Final operation selection drops an unused original Core graph. Shared
slice slots directly own their immutable program and PCRE2 fallback; finite
literal routes retain the separate derived Core expression they execute. Diagnostic
translation flags describe live ownership. Captures and public operations retain
their profile semantics.

Development after frozen RC1 adds backtracking-compiler admission: before parsing,
reserve a conservative 256 bytes per UTF-16 pattern character for transient AST
vectors, capture/name maps and metadata copies. This is a modeled capacity charge,
can count shared storage twice and stays charged on successful compilation; it is
not an estimate of total managed allocation. Each lowerer's separate name lookup
also admits 256 bytes per name entry, including nested assertion lowerers.

Character compilation now shares the construction owner too. Token-vector
capacity counts toward instructions and bytes; class-term vectors, property
normalization storage and returned text admit their major extents before
allocation, including growth/final-copy overlap. Traversals and delimiter scans
use bounded interruption checkpoints. Backtracking's single-token parser shares
these controls. Earlier refusal is intentional; incidental helpers and whole-heap
memory remain outside the modeled scope. Character programs no longer prepare
an unused Core expression. Literal compilation releases its temporary Rune vector
after encoding; runtime literal atoms/anchors and character syntax-node/offset
mirrors are removed. Probe's live backtracking tree remains.
Instruction and auxiliary program
arrays separately admit capacity and growth overlap before allocation. Parsing,
lowering and reference-following analysis share work/interruption and depth
admission. This CLR-recursive compiler has a fixed 256-frame construction
backstop; a smaller public depth limit tightens it. Earlier construction refusal
is intentional. Other validation/character-parser coverage exclusions remain.

Compile-time syntax checks apply to quantifier bounds and option/escape grammar
in context, outside quoted text, comments, classes and escape operands. Finite
quantifier bounds are at most 65535 and the lower bound cannot exceed the upper.
If all compilers decline, construction throws `NotSupportedException` instead of
returning an expression with no executable operation. Selection starts with the
owned semantic program and optional eligible Core, then publishes complete
operation slots once. Legacy/no-operation foundations are removed.

Finite-literal and bounded-ASCII optional expansion shares construction work,
depth and major capacity admission before acquiring buffers. Coarse state/map
allowances can count sharing again and remain reserved until eager construction
finishes; smaller caps can therefore refuse earlier. Literal text/encoding,
alternative arrays and growth/copy overlap remain covered, with original
interruption throughout bounded traversal. Capture maps are omitted when no
backreference consumes them; published capture semantics still use the owned
matcher. Fixed expansion caps supplement the caller's admission, rather than
substituting for it. These controls do not bound all allocations or RSS.

Character-class ranges compare decoded Unicode-scalar endpoints. Raw
`[😀-😃]` and escaped `[\x{1F600}-\x{1F603}]` have the same membership; UTF-16
source characters and escape spellings do not define endpoint order. A range
requires two scalar endpoints: a shorthand or POSIX predicate cannot be an
endpoint. Invalid or reversed ranges throw `Pcre2CompileException` with the
existing `ClassRangeOrder` kind. Leading and trailing literal hyphens retain
their normal class meaning.

`\Q...\E` quotes literal scalars inside character classes as well as outside
them. Quoted hyphens, brackets, carets and backslashes retain literal membership;
unquoted range delimiters still compare scalar endpoints across quote boundaries.
Quote markers, including an isolated `\E`, contribute no atom. Whitespace inside
quotes remains literal with extended-more mode. A quote that consumes the class's
closing bracket leaves an unterminated class; empty quotes do not create a
repeatable atom. Pattern syntax guards must make the same distinction.

Recognized backtracking-control verb labels are literal text ending at the first
closing parenthesis; this profile does not expose `ALT_VERBNAMES`. Parentheses,
brackets, escape spellings and callout/quantifier-looking text inside a label do
not become pattern syntax. Regex-body constructs such as `(*atomic:...)` and
syntax after a label remain subject to normal validation. Lexical passes share
the recognized-label skip and its bounded construction-work/interruption checks.

Extended-mode `#` comments are ignored by admission guards and parsers through
their configured newline terminator, including CRLF, Unicode and NUL conventions.
Global and scoped `x`/`xx` changes restore the inherited mode when their group
ends; class whitespace under `xx` does not turn a literal hash into a comment.
Escaped/quoted hashes, classes and control-verb labels retain literal meaning.
Ignored text cannot trigger syntax restrictions, and real syntax after comments
or classes remains checked. Comment scanning borrows the original construction
work/deadline/cancellation owner at bounded checkpoints.

Inline extended settings apply per directive: `(?x)` selects ordinary extended
mode even after `(?xx)`, repeated `(?x)(?x)` does not enable `xx`, and `(?-x)`
clears both modes. Scoped settings restore the inherited mode after their group.
Under `xx`, unescaped class spaces/tabs are ignored; under `x`, they are literal.
Owned profile execution must retain these rules through captures, coordinates,
references and replacement instead of delegating incompatible class semantics.

Reset-option forms `(?^:...)` and `(?^)` clear the supported `i`, `m`, `n`, `s`,
`x` and `xx` settings before applying following supported flags. They retain
ungreedy (`U`) and duplicate-name (`J`) state. Scoped settings restore on exit;
global directives apply to the current scope. `-` cannot appear in a reset
directive and must fail construction with `InvalidAfterParensQuery`.

Duplicate-name permission also follows lexical option scope. `(?J:...)` enables
it for that group, and `(?-J:...)` disables it there; `(?J)` and `(?-J)` apply
through the remainder of their containing group, including later alternatives.
Closing any group restores its inherited permission. Reset preserves `J`.
Turning permission off rejects subsequent duplicate declarations without removing
previously admitted slots or names. A disallowed duplicate throws
`Pcre2CompileException` with `InvalidAfterParensQuery` before backend selection.
Capture slots retain declaration order and by-name lookup selects the first set
slot; option scope does not localize the pattern's name table.

An inline option directive permits at most one hyphen. A second hyphen fails
construction with `Pcre2CompileException` / `InvalidAfterParensQuery`; a setting
appearing on both sides of the single hyphen is disabled. Empty `(?)` is a valid
no-op, including before captures, and contributes neither an atom nor a slot.
Quoted, class, comment and control-verb-label text retains its lexical meaning.
The empty single-hyphen forms `(?-)` and `(?-:...)` likewise leave inherited
options unchanged. They preserve PCRE2 declaration-order capture slots and
numeric-reference meaning when named and unnamed groups are mixed.

`(?(DEFINE)...)` is also an option scope. Its definitions retain their lexical
case, line, dot, extended, greediness and capture settings for subroutine calls;
closing the definition group restores the inherited settings for following code.
Definition capture slots remain part of the pattern's declaration-order table,
including when the definition is not called.

`Utf8Pcre2Regex` targets the **PCRE2 standard matcher** only. The alternative DFA matcher is explicitly **out of scope** for this API family because PCRE2 documents it as a different, non-Perl-compatible algorithm with different behavior. ([PCRE][2])

`Utf8Pcre2Regex` is a **strictly managed .NET profile layer**. It must not bind to upstream native PCRE2 through P/Invoke, `NativeLibrary`, external executables, RID-specific native assets, or any other non-managed dependency. The only acceptable non-BCL implementation dependency is the existing `Lokad.Utf8Regex` library itself.

### 2. Core design rules

Semantics are chosen by **type**, not by a runtime mode flag. `Utf8Regex` supplies
the core strict UTF-8 Unicode-scalar contract. `Utf8Pcre2Regex` supplies the
selected PCRE2 profile. Shared implementation is allowed below the public
surface, while grammars, option models, capture results and replacement rules
retain their profile-specific contracts. .NET code-unit behavior is an
overlapping comparator, not the core matching contract.

For PCRE2, the detailed public result model must be **numeric-slot-first**, with explicit access to the **name table**. Singular by-name lookup may exist only as a convenience and must be clearly documented as “first set group for that name,” not as the primary truth. This follows PCRE2’s own behavior: with duplicate names, `pcre2_substring_number_from_name()` can report “not unique,” while the convenience by-name extractors scan the groups for that name and return the first one that is set. ([PCRE][3])

Capture names may start with a Unicode letter or underscore, followed by letters,
decimal digits or underscores. A digit-starting definition such as `(?<100>a)` is
invalid PCRE2 syntax; it must throw `Pcre2CompileException` with the existing
`InvalidAfterParensQuery` kind before selecting an execution backend. Do not admit
.NET explicit numeric definitions through the foundation or invent a sparse-slot
extension to repair their result projection. Ordinary capture slots and numeric
backreferences retain PCRE2 declaration-order numbering. ([PCRE][7])

The selected eight-bit profile limits capture names to **128 UTF-8 bytes** and
backtracking-control verb labels to **255 UTF-8 bytes**. These limits measure the
encoded name, excluding syntax delimiters; UTF-16 length is insufficient for
non-ASCII names. Overlong declarations must fail before backend selection with
`Pcre2CompileException` and the existing `InvalidAfterParensQuery` kind. Literal,
quoted, class and comment text does not become a declaration. Validation retains
bounded construction-work admission and the original interruption authority.

PCRE2 match values must be modeled using **reported start and end offsets**, not just `Index + Length`. This is necessary because PCRE2 documents that `\K` inside positive lookahead can yield a successful match whose reported start offset is greater than its end offset. ([PCRE][3])

Partial matching must be exposed through a **dedicated tri-state probing API**, not smuggled into normal “successful match” APIs. PCRE2 documents partial matching as a distinct outcome (`PCRE2_ERROR_PARTIAL`), and when it occurs only substring 0 is defined; the rest of the ovector is undefined. ([PCRE][4])

PCRE2 replacement must use **PCRE2 substitution semantics**, not `.NET` substitution semantics. By default, only `$...` forms are special in PCRE2 replacement strings; backslash processing becomes special only when extended substitution is enabled. Supported default forms include `$$`, `$n`, `${n}`, `$0`, `$&`, ``$````, `$'`, `$_`, `$+`, `$*MARK`, `${*MARK}`, and `$<name>`. ([PCRE][3])

Because this library is text-centric and UTF-8-native, the default PCRE2 profile must **forbid `\C`** and must **not** opt into the legacy `\K`-inside-lookaround compatibility mode. Both remain available as explicit opt-ins. This is principled because PCRE2 documents that `\C` matches a single code unit even in UTF mode and can split a multi-code-unit character, and it documents that lookaround `\K` is forbidden by default and only re-enabled by an extra compile option. ([PCRE][5])

### 3. Frozen public surface

The exact mechanically enforced contract is
[PublicApi.Shipped.txt](tests/Lokad.Utf8Regex.Pcre2.Tests/PublicApi.Shipped.txt).
This document does not maintain a second incomplete declaration catalog; the
[package README](src/Lokad.Utf8Regex.Pcre2/README.md) gives public examples.
No signature, option or exception surface changes as part of this cleanup.

`Utf8Pcre2Regex` supplies boolean/count/value/detailed/global matching,
replacement, analysis, named-capture metadata and explicit partial `Probe`.
Compile/match/substitution/probe options are separate types. Settings define
newline/BSR, duplicate names and explicit byte/\K opt-ins. `Create` maps common
invocation policy; `Compile` additionally accepts eager construction controls.
Result contexts borrow the unchanged UTF-8 subject. Group/name APIs preserve
PCRE2 slot and name-table semantics; they do not expose Core capture history.
`Probe` exposes full, partial or no-match; partial results expose only substring0
and Mark, and its detailed accessor is valid only for the corresponding kind.
Analysis is advisory: minimum byte length, required literals and possible byte-
splitting/non-monotone offsets do not authorize bypassing validation or matching.

### 4. Semantic contract

Core retains its separate scalar, capture-history and replacement contracts;
PCRE2 semantics are selected by the adapter type.

`Utf8Pcre2Regex` uses **PCRE2 standard matcher semantics in UTF-8 mode**. The DFA matcher is outside the managed profile. `RightToLeft` does not exist on the PCRE2 side and must not be emulated. All public start positions on the PCRE2 side are expressed as **byte offsets into the UTF-8 input**, not UTF-16 code-unit offsets. ([PCRE][2])

The PCRE2 side owns its own compile options, match options, substitution options, compile settings, and execution limits. Do not reuse `RegexOptions` and do not pretend `TimeSpan` is the only resource-control model. `TimeSpan MatchTimeout` is allowed only as an additional managed guard for consistency with the library’s broader shape; the primary PCRE2-facing resource knobs are `MatchLimit`, `DepthLimit`, and `HeapLimitInBytes`. ([PCRE][6])

All byte-start-offset APIs validate that `startOffsetInBytes` is in the inclusive range `[0, input.Length]`. If the offset is out of range, they throw `ArgumentOutOfRangeException`. If the offset falls in the middle of an ill-formed or incomplete UTF-8 sequence and the operation requires UTF-aware semantics, the operation throws the same input-validation exception family used elsewhere by the library for invalid UTF-8 input.

The default compile settings for `Utf8Pcre2Regex` are:

* `AllowDuplicateNames = false`
* `BackslashC = Forbid`
* `AllowLookaroundBackslashK = false`
* `Newline = Default`
* `Bsr = Default`

Those defaults are deliberate. PCRE2 allows duplicate names only when requested, except for the special same-number branch-reset case; `\C` is dangerous for UTF text because it matches a single code unit and can split a UTF character; and lookaround `\K` is forbidden by default in modern PCRE2 and only re-enabled by an extra compatibility option. ([PCRE][7])

The detailed PCRE2 group model exposes **only the final value of each capture slot**. There is no `.NET`-style `CaptureCount` / `GetCapture(i)` surface on PCRE2 detailed results. This is intentional and must not be approximated, because `.NET` exposes full capture history for repeated captures while PCRE2 standard matching returns only the last portion matched by a repeated group. ([Microsoft Learn][1])

The profile supports PCRE2's `Xuc`
property through the existing scalar predicate dispatch. It matches `$`, `@`,
U+0060 (grave accent), and valid scalars at or above U+00A0. Surrogate encodings
remain invalid UTF-8 subjects. `\p{Xuc}`, its complement and bracket forms retain
the same set under caseless matching: Kelvin sign matches, while ASCII `K` does
not. Repetition, captures and lookbehind use the existing execution and resource
controls; no Unicode-name table, new matcher or native dependency is added.

The shared predicate unifies Unicode space classification for `Xps`,
`Xsp` and UCP generic/POSIX space. Property escapes now include U+0085 (NEL)
and U+180E (Mongolian vowel separator), matching native PCRE2 10.47. Property
escapes remain Unicode-aware without UCP; generic `\s` and POSIX space retain
their ASCII behavior when UCP is off. The shared predicate retains the existing
ASCII fast path and reuses the caller's Unicode category; no lookup table,
execution mode or resource-policy change is introduced.

Caseless literals, class ranges and references use default simple-fold scalar
equivalence: all partners are symmetric, including Kelvin sign, long S, Greek
symbol variants and titlecase partners. A class negates the complete positive
set; folding neither normalizes text nor expands one scalar into multiple scalars.
Dotted/dotless I are distinct in the default profile. The profile pins
BMP fold equivalence to Unicode 16 using shared owned data and PCRE2-specific
partners. Supplementary matching and extended-replacement simple casing reuse
the owned Unicode 16 pairs, including Garay, without host casing dependencies.
BMP replacement transforms retain their existing Rune route. This does not pin
all Unicode properties or establish complete Unicode-version compatibility.
A recorded native PCRE2 10.47 comparator difference turns final sigma
(U+03C2) into lowercase sigma (U+03C3) under uppercasing replacement escapes;
this profile produces uppercase sigma (U+03A3). The twelve observations cover
four capture/literal transforms under three invocation policies. This is a
disclosed replacement-casing difference, not a matching-coordinate difference.
Keep the existing uppercase result rather than copy the native result.
Exact observations remain in the historical qualification evidence; this is a
known replacement-casing boundary, not a new performance or compatibility claim.

Inline `r` restricts caseless matching across the ASCII/non-ASCII boundary.
`(?ir)k` matches `K` but not Kelvin sign; `(?ir)[kK]` still includes both
explicit members. Non-ASCII pairs such as é/É and supplementary scalar pairs
retain ordinary folding. The setting can be scoped or disabled with `-r`,
is cleared by `(?^)`, and is captured lexically by subroutine definitions.
References use the setting at the reference site. It has no folding effect when
i is off. No new public compile option or execution mode is introduced.

POSIX lower/upper with UCP retain their respective Unicode categories under
caseless options; without UCP they keep ASCII caseless behavior. This differs
from the caseless handling of explicit Unicode Lu/Ll property escapes.

A subroutine or recursive call restores the caller's capture state when it
returns, including previously unset captures. Captures set only in the callee
are not published as caller captures. Previously set caller values and their
byte ranges survive; captures enclosing the call can still capture its consumed
subject. This applies to ordinary and `(*ACCEPT)` returns and does not make
subroutine calls atomic. References and capture conditions observe the restored
state. Coverage is recorded separately in the PCRE2 qualification document.

By-name metadata is **name-table-first**. `CopyNameEntries(...)` and `CopyNumbersForName(...)` are the primary APIs. `TryGetFirstSetGroup(name, ...)` is allowed only as a convenience and must follow PCRE2’s by-name convenience behavior: when duplicate names exist, it returns the first set group for that name. ([PCRE][3])

Normal matching APIs (`IsMatch`, `Count`, `Match`, `MatchDetailed`, `EnumerateMatches`, `Replace`, `MatchMany`) do **not** expose partial-match behavior. Partial matching is available only through `Probe(...)`. A partial probe result exposes only substring 0 plus `Mark`; it does not expose numbered or named capture groups. This matches PCRE2’s documented partial-match contract, where partial is a distinct outcome and the rest of the ovector is undefined. ([PCRE][4])

PCRE2 10.47 rejects soft/hard partial matching combined with compile-time or
match-time `EndAnchored`. `Probe` reports this invalid combination as
`Pcre2MatchException` with `Pcre2ErrorKind.BadOption`, before driver selection;
the token overload still observes pre-cancellation first. Non-partial end anchoring
remains supported, including backtracking to another alternative or later candidate.
([PCRE2 10.47 match source](https://github.com/PCRE2Project/pcre2/blob/pcre2-10.47/src/pcre2_match.c#L7098))

`Utf8Pcre2ValueMatch` and `Utf8Pcre2GroupContext` use raw **reported start and end offsets** in bytes, with an exclusive end. `HasContiguousByteRange` is `true` only when `start <= end`. This is required because of `\K` semantics in positive lookahead. When `AllowLookaroundBackslashK` is `false`, the engine rejects such patterns or pattern features at compile time. When it is `true`, `Match` and `MatchDetailed` may return non-monotone offsets; `GetValueBytes()` then throws because there is no contiguous byte slice. `Utf8Pcre2ValueMatch` is therefore a `ref struct` carrier that remains tied to the input span for byte and string extraction. ([PCRE][3])

`HasUtf16Projection` is `true` only when the reported match/group can be projected exactly to UTF-16 coordinates and to a `string`. This is usually true in the default profile. It can become `false` only when the caller explicitly allows `\C` and the match/group splits a UTF-8 scalar. PCRE2 documents that `\C` matches a single code unit in UTF mode and can break up multi-unit characters. ([PCRE][5])

Opting into `\C` does not opt out of subject validation: the complete subject must still be well-formed UTF-8, while each `\C` atom consumes exactly one byte. Global matching resumes at the consumed byte offset, so successive `\C` matches may expose the individual bytes of one scalar; the normal empty-match retry and progress rule still applies. Byte-returning replacement APIs preserve these byte semantics and can therefore produce non-UTF-8 output when a substitution leaves only part of a scalar. String-returning replacement APIs reject such an output. Match/group UTF-16 coordinate access and string extraction throw when `HasUtf16Projection` is `false`. `\C` remains forbidden in UTF lookbehind.

PCRE2 replacement uses PCRE2 substitution syntax. By default, only dollar-based substitutions are recognized. Backslash escapes in replacement text are processed only when `Pcre2SubstitutionOptions.Extended` is set. The selected default substitution forms are:

* `$$`
* `$n`
* `${n}`
* `$0`
* `$&`
* ``$````
* `$'`
* `$_`
* `$+`
* `$*MARK`
* `${*MARK}`
* `$<name>`

This is a semantic requirement; do not use `.NET` replacement parsing on the PCRE2 side. ([PCRE][3])

`Replace` and `TryReplace` operate left-to-right only. If the pattern was compiled with `AllowLookaroundBackslashK = true` and a successful match would end before it starts, replacement APIs throw `NotSupportedException`. This mirrors the fact that PCRE2 substitution explicitly does not support those successful `\K` lookaround cases. The same restriction applies to APIs that inherently require forward global progress over successive matches: `Count`, `EnumerateMatches`, and `MatchMany` also throw `NotSupportedException` when the next successful match in the requested iteration would report `EndOffsetInBytes < StartOffsetInBytes`. Single-match APIs (`IsMatch`, `Match`, `MatchDetailed`, `Probe`) remain supported. ([PCRE][3])

For global left-to-right operations (`Count`, `EnumerateMatches`, `MatchMany`, and all replacement APIs), the runtime tracks a private **consumed range** separately from the public reported range. Iteration begins at `startOffsetInBytes`; after a nonempty consumed match, the next search begins at the consumed end. `\K` may change the reported start but never the restart position. After an empty consumed match, the runtime first retries the same position with the PCRE2 anchored/not-empty-at-start disposition. If that retry fails, compile or per-call anchoring stops iteration; otherwise it advances to the next UTF-8 scalar boundary. Iteration stops when no next boundary exists. Caller-supplied `Pcre2MatchOptions.NotEmpty` and `Pcre2MatchOptions.NotEmptyAtStart` continue to apply on every attempt, and partial matching is never consulted by these loops.

All global operations share that cursor, but their result disposition can differ where upstream PCRE2 substitution semantics require it. In particular, replacement preserves an admitted reset-empty `\K` result that ordinary global enumeration suppresses. This behavior is frozen by operation-specific corpus vectors rather than inferred from public reported offsets.

The `Mark` property on `Utf8Pcre2MatchContext` and `Utf8Pcre2ProbeResult` returns the last encountered named backtracking verb on the relevant path, if any. It is available after success, partial, or no-match, matching PCRE2’s documented behavior. ([PCRE][3])

Failure contracts must be explicit:

* invalid pattern text, invalid compile-option combinations, unsupported constructs under the selected library profile, or invalid compile settings cause construction to throw a dedicated PCRE2 compile exception
* match-time resource exhaustion (`MatchLimit`, `DepthLimit`, `HeapLimitInBytes`) throws a dedicated PCRE2 match exception that preserves the modeled PCRE2 failure kind
* managed timeout expiration throws the same timeout exception family already used by the library’s `.NET`-semantic surface
* invalid replacement syntax or replacement-time substitution errors throw a dedicated PCRE2 substitution exception
* `Probe(...)` reports only `NoMatch`, `FullMatch`, or `PartialMatch`; resource exhaustion and other operational failures are not folded into `Utf8Pcre2ProbeKind` and instead throw

Supported-profile boundary for completion is also explicit:

* public APIs must either execute successfully, throw a documented dedicated PCRE2 exception, or throw `NotSupportedException` for an intentional `SPEC-PCRE2` rejection
* raw public `NotImplementedException` is prohibited; implementation backlog must be classified as a supported operation, a dedicated profile error, or an intentional `NotSupportedException` rejection
* non-monotone iterative `\K` cases are rejected for left-to-right global APIs (`Count`, `EnumerateMatches`, `MatchMany`, all replacement modes), even when single-match APIs remain supported
* `Probe(...)` is intentionally a smaller surface than `Match(...)`; patterns outside the curated partial-probe profile must fail explicitly rather than silently approximating managed `.NET` behavior
* every accepted corpus pattern has an explicit operation-capability disposition; replacement rejects only the documented profile cases and never falls through to an unrelated backend or grammar
* the executable corpus plus `Utf8Pcre2CompletionLedgerTests` are the closure mechanism for this boundary; newly supported or newly rejected cases must be reflected there so the ledger remains a deliberate snapshot instead of drifting implicitly
* performance work must prefer reuse of existing managed `Lokad.Utf8Regex` infrastructure where semantics allow it; do not introduce a second regex engine architecture under the PCRE2 profile

### Cancellation, timeouts, and resource limits

The scalar route can count an already proved non-empty, left-to-right
literal language with the owned scalar runner under the original invocation.
It rents scratch once for the whole Count and retains validation, candidate search,
verification, token, absolute deadline and structured failure authority. Empty
languages and non-default match options keep their existing profile progression.
Capture-dependent matching/shaping still uses the full program; the count language
can resolve constant captures/references only through its existing compiler proof.
Count publishes no owned result payload. Eliminating capture state can admit
smaller working/depth/work allowances than the former VM route; units and acquired
capacity are algorithm-dependent, not exact native-PCRE2 counter equivalence.

Eligible finite literal Count reuses Core's bounded metered literal Count backend
for an already compiled finite literal language with no boundary/reset projection:
one exact nonempty literal, or two/three ordered exact alternatives, with no
folding or search boundary/trailing requirement. Whole-subject validation and
the original invocation, byte floor, cancellation, absolute deadline and profile
failure vocabulary remain. Other languages/options keep their existing route.
Count needs no projected result or VM rewind frames on this path; smaller
work/depth/working allowances may therefore admit it. Required captures/results
and remaining scalar VM storage retain their own pre-acquisition admission.

Subsequent source reuses the existing repeat rollback stack alongside frames in
one lexical Count/search scope. Each attempt truncates logical rollback state;
nested assertions own separate stacks. Actual growth remains admitted and charged,
and the actual cursor budget releases both capacities in finally. Public copied
cursors acquire no owned scratch field. Other stacks remain unchanged.
Held high-water capacity can increase later/nested working overlap; fewer actual
rentals reduce work. These intentional numeric-cutoff changes do not refund spent
work, weaken capacity bounds or add a GC-dependent lifetime.

Lexical Count/search also lends one fixed-vector workspace across the
same lexical Count/search owner. It acquires on the first actual candidate, so
an impossible short subject needs no workspace. Each attempt resets its logical
vectors; nested assertions keep separate storage charged alongside the parent.
The workspace retains no budget reference: explicit acquisition and finally
release use the actual owner's original authority. Reuse still performs the
synchronous cancellation/deadline/sticky-denial check; all visited instructions
retain their charges. Only actual acquisitions spend acquisition work, and rental
diagnostics count acquisitions rather than reused attempts. Longer live capacity
can change working overlap and fewer rents can admit tighter work limits. No
public copied cursor acquires scratch ownership, new pool/lease/mode or GC timing
dependency. Scalar captures, retained results and exact major admission remain.

When the admitted search fact proves a leading single literal, the scalar counter
can seek its next occurrence through the existing bounded metered search before
VM verification. Interior anchors, empty, RTL, folding and unproved start plans
do not acquire this eligibility. The original invocation still owns validation,
seeking, verification and publication; skipped impossible starts can reduce work
requirements. Literal-family seeking remains on its prior eligibility rules.

Nonempty slice Count also reuses that whole-Count scratch scope when the
already admitted shared slice program proves nonempty left-to-right matching.
No new language proof or search plan is inferred. Empty programs retain their
retry/advancement cursor, and explicit match options retain the existing fallback.
Scratch stays admitted until Count finishes or fails, then releases synchronously;
capture/reference state and the original invocation remain authoritative. Work
units and major state needs can change with this lifetime, so historical numeric
cutoffs are not preserved as artificial denials.

Every execution operation has an explicit `CancellationToken` twin. An already-canceled token throws `OperationCanceledException` at entry; validation, matching and required result processing retain the original token and absolute deadline at bounded synchronous checkpoints. Lazy `EnumerateMatches` observes the token at creation and during advances. Evaluator-driven replacement additionally observes the token before every callback on all paths, so canceling from inside the callback aborts deterministically.

`MatchLimit` enforcement depends on the selected managed route; work units are not
interchangeable with native PCRE2 or another managed engine. A breach raises
`Pcre2MatchException` with the `MatchLimit` kind. Owned slice global cursors use a
cumulative invocation across advances. Single-shot `IsMatch`/`Match` also fund
validation, discovery and required result projection under the original limit.
Direct literal paths retain this metering: a boolean first-hit result can fit a
limit that denies the corresponding value result's endpoint projection. Stepwise
routes charge their candidate/VM work as well (see `Pcre2SingleShotLimitTests`).
The shared finite forward exact-literal Count path admits the constant-time count
increment with its bounded search/comparison and retains its terminal check.
This route can need fewer `MatchLimit` units; strict
validation and the original token/deadline remain authoritative.
`DepthLimit` and
`HeapLimitInBytes` constrain backtracking frames and workspace; admitted output
builders also charge their covered major working capacities before acquisition.
On the shared owned slice, depth counts simultaneously live fork/repeat frames
and assertion markers. The prospective depth is checked before frame growth;
assertion entry checks it before acquiring local repeat or continuation storage.
A depth-denied entry acquires no such storage. Combined depth/heap failures can
therefore report depth earlier; original work, cancellation and deadline checks
remain authoritative. Other managed routes retain their documented metering units.
Breaches report `DepthLimit` or `HeapLimit`. Zero disables each limit and defaults
are unlimited. Timeout breach on owned routes reports `Timeout`. The shared
`Utf8ResourcePolicy` maps onto these limits through explicit `Utf8Pcre2Regex.Create` factories
(null becomes legacy zero; overflow and zero budgets are rejected). This PCRE2
profile also maps positive `MaxResultBytes` to the managed extension
`ResultLimitInBytes`; legacy zero is unlimited. `ResultLimit` reports cumulative
modeled major publication/staging denial before allocation, without refunds or
truncated success. Capture/probe arrays, range/output staging and returned byte/
string roots share the invocation. Caller-owned storage, compiled metadata, VM
scratch, standalone getters/callback allocations and arbitrary retention are
excluded. Scalar value/count/boolean results and inline partials need no owned
result capacity. The core/PythonRe policy remains described in `SPEC.md` Section
3.7; these models do not bound process RSS.

Controlled copies of native/global and owned-slice value cursors keep independent
positions but share the original work/depth/working enforcement authority and
absolute deadline. Copying no longer forks a finite allowance; same-invocation
consumption can fail earlier. Independent public calls create independent budgets.
Working admission checks the deadline before acquiring major capacity, records
maximum requested peaks including refusals, and leaves outstanding reservations
unchanged on denial. The first resource denial remains sticky with its PCRE2 kind.
Exclusive scratch releases synchronously and can reuse capacity without refunding
work. Retained ranges/capture views and permitted aliases stay valid. Native work
units, step polling cadence, diagnostic meaning and bounded stacks remain; native
steps do not invoke scalar-slice diagnostic hooks. Fixed enforcement metadata is
outside modeled working payload and this is not an exact CLR heap or RSS bound.

Owned slice template replacement with `None` match options and owned slice callbacks
with any match options share one discovery invocation with capture projection and
output building. Explicit-option slice callbacks use the retained native program's
detailed cursor. Their captures describe the actual global yield, including a
nonempty retry at the same position and the original not-empty-at-start disposition;
they do not perform a fresh first-match query at the reported start. Legacy
backtracking callbacks with captures or suppressed unreset empty matches also share
that invocation, including explicit match options. Their existing
detailed cursor supplies captures and MARK without a second match query, preserving
native step cadence and diagnostic meaning. Capture-free native callbacks borrow
that invocation into their selected value cursor, reusable group-zero array and
byte/text output, retaining optimized literal/character/repeat/family selectors.
Callback output uses one scope with the request policy; its separate Unlimited
allowance is removed. Growth/final
overlap and the unchanged-subject result after no match are admitted before allocation
and copied in bounded blocks with the original token/absolute deadline. Callback
buffer controls, writer resets and final byte/text shaping use that same allowance;
nested operations restore the outer scope. The shared Core writer lifetime applies:
append outside a replacement callback throws `InvalidOperationException`, including
empty fragments; `AppendAsciiByte` rejects non-ASCII arguments first.
`MatchLimit` and working heap cover these stages cumulatively, so cases that
fitted former separate matching/writing allowances
can fail earlier. The first resource denial retains its PCRE2 kind. Publication releases
temporary working charges while returned arrays, capture views and aliases remain
valid. Fixed slice `ReplaceToString` validates, counts and decodes directly from its
staged UTF-8 bytes under that invocation, admitting the returned string while staging
remains live. It acquires no extra final encoded byte array. An unchanged no-match
subject is borrowed while its returned string is admitted. Charges can differ from
the former byte-copy-plus-detached-decode path; final text work and capacity are now
covered. Controlled global value-cursor coordinate projection also uses the existing
bounded scalar endpoint helper under the original authority. MatchLimit includes
projection work; ASCII endpoints add two steps per successful yield. Non-ASCII
projection advances in at most 2,048-byte scalar-aligned windows. A refused projection
preserves the previous published value and first resource denial; native candidate/
backtracking diagnostics retain their meaning. UTF-16 coordinates remain derived
interoperability data, with byte/scalar matching and restart positions unchanged.
These modeled charges do not bound caller retention or RSS. Native/core-grammar
discovery and required capture/output shaping share their original invocation,
including cumulative result capacity. Specialized partial probes use the same
authority for loops, bounded searches, projection and full arrays. Initial subject
validation shares original invocation work/interruption. Replacement preparation
checkpoints share that work; cold preparation with finite heap borrows an additional
byte ceiling from the existing construction budget and holds a conservative
additional-peak reservation through replacement. Existing parent loans, historical
peaks and tighter construction backstops are preserved. Warm reuse borrows immutable
cached plans under their separate bounded cache policy, so cold and warm working
requirements can differ. These capacity charges are conservative, not process RSS
or arbitrary caller retention. Complete construction coverage, safe defaults,
live-retention policies and broader profile coverage remain separate completion gates.

```csharp
var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1 };
var regex = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8));
```

### 5. Deliberate managed-profile non-goals

PCRE2 callouts and callback execution are outside this profile.

The PCRE2 DFA matcher is outside this profile.

Do not attempt a fake unification of `.NET` and PCRE2 result types beyond shared low-level internals such as UTF-8 validation, byte/UTF-16 coordinate translation, vectorized literal search, timeout polling, and `Utf8ReplacementWriter`.

Do not add `.NET`-shaped group-name APIs such as `GroupNumberFromName(string)` on the PCRE2 type. The name-table APIs above are the required public shape.

### 6. Implementation isolation rules

PCRE2 support must be implemented as an isolated feature slice so that it can be removed later without destabilizing the `.NET`-semantic core.

Required rules:

* all PCRE2 implementation files live under `src/Lokad.Utf8Regex.Pcre2/`
* every PCRE2-specific public type, internal type, namespace, folder, and test fixture name must contain `Pcre2`
* PCRE2-specific helpers must not be added anonymously to the core project; if a helper exists only for PCRE2, it belongs in the PCRE2 project
* any unavoidable cross-project hooks in the core library must be minimal, explicitly documented, and marked with a stable comment tag such as `PCRE2-INTEGRATION-POINT`
* flavor-agnostic sharing is allowed only for genuinely shared low-level utilities; do not introduce generic “regex flavor” abstractions unless both sides truly need them
* the PCRE2 profile must remain fully managed; no P/Invoke, no native library loading, no external PCRE2 binary, and no RID-specific native packaging
* `Lokad.Utf8Regex` is the sole acceptable implementation dependency beyond the .NET BCL; any performance work should first look for reuse of existing managed search, validation, span, projection, and replacement infrastructure

Preferred structure:

* `src/Lokad.Utf8Regex.Pcre2/` for implementation
* `tests/...Pcre2...` for PCRE2-specific tests

Packaging and loading policy must also preserve removability:

* there is no native PCRE2 loading, RID packaging, or “library unavailable” behavior in this project profile
* the base `Lokad.Utf8Regex` project must not require PCRE2 to load or function

[1]: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.group.captures?view=net-10.0 "Group.Captures Property (System.Text.RegularExpressions) | Microsoft Learn"
[2]: https://pcre.org/current/doc/html/pcre2matching.html?utm_source=chatgpt.com "pcre2matching specification - Perl Compatible Regular Expressions"
[3]: https://pcre.org/current/doc/html/pcre2api.html "pcre2api specification"
[4]: https://pcre.org/current/doc/html/pcre2partial.html "pcre2partial specification"
[5]: https://www.pcre.org/current/doc/html/pcre2unicode.html "pcre2unicode specification"
[6]: https://www.pcre.org/current/doc/html/pcre2_match.html?utm_source=chatgpt.com "pcre2_match specification"
[7]: https://www.pcre.org/current/doc/html/pcre2pattern.html "pcre2pattern specification"
