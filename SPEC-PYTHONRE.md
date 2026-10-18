# SPEC-PYTHONRE

`Lokad.Utf8Regex.PythonRe` is an optional sidecar package that exposes Python-style regular expression behavior over UTF-8 inputs.

## Intent

The package exists to model Python `re` semantics as a separate profile from base `Utf8Regex`, whose matching unit is one Unicode scalar over strict UTF-8. The Python profile retains its own grammar, case rules, numbering and global empty-match progression; .NET is a comparator only where behavior overlaps.

Like the base `Utf8Regex` library, this profile is culture-invariant by construction. Python `re.LOCALE` is intentionally out of scope. Python `re` bytes-pattern semantics are also intentionally out of scope in the current profile.

## Architecture

The implementation is parser-first:

1. parse Python-style source into a PythonRe AST
2. validate Python-specific semantic constraints
3. resolve lexical case rules and capture identities in the Python AST, then lower it to the shared scalar VM
4. render exact scalar atoms/ranges for retained native `Utf8Regex` controls; reuse the owned class decoder for profile atoms
5. execute through owned scalar programs and qualified core fast paths; retain
   managed-compatible rendering only as development comparator data, with no
   shipped BCL matcher/result dependency. The semantic authority remains the
   pinned CPython 3.13.2 `str`-pattern oracle

Python numbers captures by opening parenthesis, including named groups. The
owned scalar program uses group `n` at slot `n - 1` for captures, references and
conditionals, including assertion bodies and FullMatch. `lastindex` follows the
last capture closed on the successful path, rather than the largest participating
number; an outer capture can close after its children. The different named/unnamed
numbering used by rendered Core patterns remains local to text construction.
It is not retained as a cursor projection table or searched after each match.

The native rendering consumes complete scalars. Supplementary class members and ranges use numeric scalar endpoints, rather than separate surrogate classes. Python's separate `\uD800`/`\uDC00` escapes denote separate surrogate atoms and cannot combine into a scalar; they cannot match strict UTF-8. A raw supplementary character or `\U00010000` escape denotes one scalar. A surrogate-only class contains no valid scalar, and its negation admits valid scalars. Captures retain primary byte ranges and derived UTF-16 coordinates. Owned execution does not establish complete grammar coverage, public construction containment or retained-storage limits; qualify those separately from the current invocation controls.

## Raw escaped Unicode literals

Development source after scalar.90 consumes a raw escaped non-ASCII character
as one scalar: `\😀`, `[\😀]`, escaped range endpoints and their quantifiers keep
the same scalar unit as an unescaped literal. Escaped BMP and supplementary
literals use the existing pinned Python folding under their lexical options,
including scoped ASCII exclusions. Explicit `\uD83D\uDE00` escapes remain two
surrogate atoms and cannot match a strict UTF-8 emoji; `\U0001F600` is one scalar.

The parser shares raw scalar acquisition across ordinary literals and class
items, reusing the existing escaped-literal/case path. It adds no matching engine,
IR mode or Unicode table. Original construction/invocation admission, synchronous
cancellation/deadlines and retained-result validity remain. This is outside
immutable scalar.90 and its114-fixture qualification subset. The immutable
scalar.91 pair includes this repair and passes the121-fixture capture corpus,
same-byte Windows/Linux consumers and exact packaged dependency checks; see
[the handoff](artifacts/core-python-scalar91-20261018/HANDOFF.md).

## Effective global modes

Development source after scalar.90 validates the union of constructor and leading
global flags after body syntax. Separate `(?a)(?u)` groups and conflicting
explicit constructor/inline modes raise `PythonRePatternException` with
`ASCII and UNICODE flags are incompatible` (CPython uses `ValueError`). Body
syntax errors keep precedence; the global conflict precedes deferred conditional
reference and lookbehind-width validation. Scoped ASCII/Unicode overrides still
replace and restore their outer mode. One existing validator enforces this;
unused accumulated leading-flag state is removed. Original construction
admission/unwind and public argument validation retain their scope.

## Brace repetition and verbose attachment

Development source after scalar.87 recognizes only complete brace-repeat syntax:
`{m}`, `{m,n}`, `{m,}`, `{,n}` and `{,}`. Other braces, including unfinished
numeric forms and `{}`, are literal text. Omitted minima are zero. Leading
repeats and repeated quantifiers retain Python's syntax failures and positions;
reversed bounds report `min repeat greater than max repeat` at the bound start.
Verbose whitespace/comments can separate an atom and its quantifier, but cannot
split a lazy/possessive modifier or turn whitespace inside braces into digits.
Scoped options, scalar captures and existing global empty-match advancement stay
on the same AST/compiler/execution paths.

The lexical brace check spends construction work while peeking, including when
the text proves literal. Its spent work is never rolled back. Original finite
construction limits, deadlines and cancellation remain authoritative; no extra
parser, matching mode or accounting owner is introduced. This repair is outside
immutable scalar.87. Immutable scalar.88 still overflows for repeat bounds above
Int32.MaxValue. Development source after scalar.88 admits finite bounds through
4294967294, separately from an unbounded maximum. Larger numeric bounds raise
PythonRePatternException with `the repetition number is too large`; CPython uses
OverflowError for this range failure and supplies no pattern position.

The AST, rendering and scalar instructions preserve each finite bound exactly.
The shared instruction and invocation-state layouts remain unchanged. Finite
repeats retain exact unsigned counts. For an unbounded repeat, after its minimum
the VM retains only whether the optional phase has begun; later totals do not
affect matching, empty-progress checks or rollback. Active positions are scalar
boundaries, so the largest optional-phase value remains distinct from inactive
state. Required nullable iterations still spend work and can be interrupted;
they are not skipped or changed into unbounded finite repeats. Construction
bounds metadata grows and remains admitted. The repair is included in qualified scalar.89;
complete Python grammar coverage remains separate.

## Ignored comments and leading flags

Development source after scalar.89 treats `(?#...)` comments as ignored syntax
in every option scope, alongside verbose whitespace and `#` line comments where
verbose mode applies. Global inline flags may follow only such leading trivia;
real atoms, alternatives and nested scopes keep their existing placement errors.
Comment groups produce no AST atom or group frame, so `a(?#note){2}` repeats `a`.
Trivia between repeat modifiers never joins a lazy or possessive suffix.

Comment scanning respects CPython's escaped-token boundaries: an escaped `)`
does not close a group comment, and an escaped LF does not end a verbose line
comment. A trailing backslash still raises `bad escape (end of pattern)`; an
unfinished group comment keeps its original start position. Pattern error
positions retain the public UTF-16 coordinate convention.

The leading-flag, atom and quantifier decisions reuse one admitted trivia scan.
Every consumed comment character spends construction work and checks the
original cancellation/deadline authority. Failed scans unwind their existing
owner; spent work is not refunded. Removing comment nodes can reduce required
construction storage/depth. This is a source correction, separate from the
immutable scalar.89 package and its original107 capture fixtures. Qualified
scalar.90 includes this correction with114 complete capture fixtures and the
existing91 semantic catalog on the same Windows/Linux package bytes.

## Quantified atoms and bracket control escapes

Development source after scalar.89 rejects direct quantification of `^`, `$`,
`\A`, `\Z`, `\b` and `\B` with `nothing to repeat` at the quantifier. Grouped
anchors and quantified lookarounds remain valid; numeric bound failures retain
precedence over atom eligibility. The parser and width analysis share one
classification of zero-width escapes.

Inside brackets, `\a`, `\b`, `\f`, `\n`, `\r`, `\t` and `\v` denote their control
scalars, including in ranges and negated classes. In particular, bracket `\b`
is backspace while outside `\b` is a word boundary. Existing scalar/capture and
construction/invocation controls remain authoritative. This correction does
not alter immutable scalar.89; it is included in qualified scalar.90.

## Nullable repetition and capture state

Python's mandatory repetitions precede the optional phase's empty-progress guard.
An empty required iteration can set a capture used by the next conditional:
`(?:(?(2)(a)|()))+` consumes `a` rather than stopping at its first empty iteration.
The same rule applies to higher positive minima, lazy and possessive repeats,
and complete supplementary scalars. Zero-minimum and fixed-count controls keep
their own limits. Captures, lastindex/lastgroup and global progression follow the
successful path, including required empty captures retained by later iterations.

The development compiler marks this repeat semantic in the existing scalar IR.
Only nullable bodies with a positive variable minimum select the two explicit
repeat instruction variants. The shared instruction layout/count, capture journal,
workspace and original invocation remain; no body duplication, new runner or
global profile mode. Core/PCRE2 retain their previous repeat rule. Correct extra
iterations may require more metered work, depth or working/result capacity than
the formerly incorrect result. All selected caps remain authoritative.

This correction is outside immutable scalar.61, which retains the demonstrated
conditional/empty-repeat defect. The existing pinned CPython capture fixture
corpus includes the new states and controls; source qualification does not rewrite
that package's historical evidence or designate a new candidate by itself.
The corrected Core/PythonRe scalar.62 pair at 3a67a6db is now qualified with
original same-byte Windows/Linux consumers, 104 capture fixtures and 91 semantic
digests. Exact identities and remaining exclusions are in
artifacts/core-python-scalar62-20261016/HANDOFF.md. The separate source cost pair
records 34–37% higher nullable Count cost; correctness does not imply performance
equivalence or revision of the checked-access 10% gate.
The subsequent qualified scalar.63 pair at a24a3983 preserves this correction and
PythonRe's pinned Unicode 15.1 implementation/data. Core supplementary casing and metered
family verification change; original detached Windows/Linux consumers, capture/
catalog controls and selective current exact-package costs qualify that boundary.


## Current scope

Supported public operations:

* `IsMatch(...)`
* `Search(...)`
* `Match(...)`
* `FullMatch(...)`
* `Count(...)`
* `FindAll(...)`
  This is a structural helper that returns full-match records, not the CPython `re.findall(...)` result-shaping surface.
* `FindAllToStrings(...)`
  This is the dedicated CPython-shaped `re.findall(...)` string result surface:
  full-match strings with no captures, group strings with one capture, and tuple-shaped string rows with multiple captures.
* `FindAllToUtf8(...)`
  This is the host-oriented UTF-8 equivalent of the CPython-shaped `findall` result surface.
* `SearchToString(...)`
* `MatchToString(...)`
* `FullMatchToString(...)`
  These are host-friendly scalar helpers that return the matched string or `null`.
* `SearchDetailed(...)`
* `MatchDetailed(...)`
* `FullMatchDetailed(...)`
* `SearchDetailedData(...)`
* `MatchDetailedData(...)`
* `FullMatchDetailedData(...)`
* `FindIterDetailed(...)`
  These expose non-`ref struct` detailed match snapshots suitable for runtime hosts.
* `Replace(...)`
* `ReplaceToString(...)`
* `SubnToString(...)`
* `Subn(...)`
  This returns UTF-8 bytes plus the replacement count for host runtimes that want to stay bytes-first.
* `ReplaceToString<TState>(...)`
* `SubnToString<TState>(...)`
* `Subn<TState>(...)`
  These support evaluator-driven replacement over detailed match snapshots.
* `Replace<TState>(...)` with `Utf8PythonUtf8MatchEvaluator<TState>`
* `Subn<TState>(...)` with `Utf8PythonUtf8MatchEvaluator<TState>`
  These support evaluator-driven replacement while keeping replacement values in UTF-8 bytes end to end.
* `SplitToStrings(...)`
* `SplitDetailed(...)`
  This exposes the CPython split output shape with capture-vs-segment metadata for runtime hosts.

Supported feature families in the current profile:

* literals and alternation
* character classes and standard escapes
* greedy, reluctant, and possessive quantifiers
* capturing and named capturing groups
* named and numeric backreferences to previously defined, closed groups
* group-participation conditionals: names must already be defined at the
  conditional header. Outside lookbehind, an already-open named group is allowed
  and numeric forward references retain final group-count validation.
  Decimal IDs beyond the managed group-index range report pattern errors rather
  than arithmetic overflow. Leading zeros retain their numeric value; malformed
  and unterminated headers keep their syntax-error precedence. Header scanning
  and oversized-reference diagnostics share construction admission.
* lookahead and fixed-width lookbehind. Within lookbehind, backreferences and
  group-participation conditions must refer to a closed group defined before the
  outermost enclosing lookbehind; nested assertions retain that restriction.
* atomic groups
* leading and scoped inline flags for `i`, `m`, `s`, `x`, `u` on `str` patterns
* Python replacement references `\\1` and `\\g<name>`
  Oversized decimal indices are syntax errors, not arithmetic overflow. Valid
  leading-zero group numbers retain their value; error formatting uses admitted
  construction text like other parser failures. The source correction is outside
  immutable scalar.63; numeric-error positions retain their existing unknown
  sentinel where no position was supplied.

* Python escape parsing for `\x..`, `\u....`, `\U........`, and octal escapes on `str` patterns
* ASCII-mode semantics for `\w`, `\W`, `\d`, `\D`, `\s`, `\S`, `\b`, `\B`

## Offset contract

All `startOffsetInBytes` parameters follow CPython `pos` semantics against the pinned 3.13.2 oracle: the match must start at or after `pos`, while `^` still anchors at the real string start (or after a newline under `MULTILINE`), lookbehind and `\b`/`\B` keep their full prefix context, and lookahead and `$`/`\Z` keep their full suffix context. `FullMatch` additionally requires the match to cover the whole `[pos, end)` input. No `endpos` parameter exists; the input end always plays that role. Value, detailed, string, and bytes entry points agree on these rules.


## Deliberate boundaries

This profile is still incomplete. In particular:

* bytes-pattern semantics are intentionally unsupported in this profile; PythonRe only targets `str` regex semantics over UTF-8 inputs
* `LOCALE` semantics are intentionally unsupported; this profile is culture-invariant by construction
* `\N{...}` named Unicode escapes are intentionally unsupported; a faithful resolver would require embedding a large Unicode-name table, which is out of scope under the package-size constraint
* development validates group names with owned CPython 3.13.2 / Unicode 15.1
  identifier data. Declarations, named references/conditionals and replacement
  references share this validator. It includes identifier-only start/continue
  characters and normalization exclusions that general Unicode categories miss;
  it rejects characters assigned only by newer host versions. Accepted spelling
  remains unchanged, so canonically equivalent names can remain distinct. The
  existing scalar-decoding/construction work checks remain; no matching kernel,
  invocation limit or cursor/result lifetime changes. The generator's `--check`
  covers this data as well as scalar folding. This source correction is beyond
  immutable scalar.52; predefined-class qualification is recorded separately below.
* the execution classifier for `Utf8Regex` is conservative by design
* admitted scalar programs use owned CPython 3.13.2 / Unicode 15.1.0 case data.
  Unicode literals and classes include simple-lower inverses and the extra
  literal partners. Backreferences compare the captured/input scalars using
  simple lowercase only; they do not apply the literal-only partners. ASCII
  scopes fold ASCII letters only, and references follow the flags at the
  reference rather than the capture. Supplementary case pairs consume one
  scalar on each side even when their byte widths differ. The former Turkic,
  scoped-ASCII, Unicode-under-ASCII and complex ASCII-reference regressions
  now execute and pass. See the [CPython compiler](https://github.com/python/cpython/blob/v3.13.2/Lib/re/_compiler.py)
  for the profile distinction; the shipped code owns its implementation and
  derived data. `python tools/generate-pythonre-scalar-case-data.py --check`
  verifies the pinned data with CPython 3.13.2; no Python runtime is shipped.
* native direct matching, detailed discovery and Count now classify supplementary
  word/digit members and scalar-adjacent boundaries through the owned core. The
  comparator spellings remain `[\p{L}\p{N}_]` for `\w`,
  `[\s\x1c-\x1f]` for `\s`, and decimal digits for `\d`. Development lowers
  predefined classes directly to owned CPython 3.13.2 / Unicode 15.1 predicates
  in the existing class instructions. Fixed ASCII masks and sorted scalar ranges
  avoid per-pattern data expansion; non-ASCII lookup is logarithmically bounded.
  Bounded process-wide profile data and cached static predicates are shared,
  rather than charged as a new copy in every invocation; major per-program and
  invocation arrays retain their existing admission. This does not add a process
  memory or arbitrary caller-retention guarantee.
  Their complements, brackets and word boundaries retain the whole scalar domain.
  Mixed classes reuse existing alternatives and scalar negation. Host-category
  native paths require equivalence and are not selected for these predicates.
  Admitted scalar programs
  now serve captured/global iteration, empty-capable result shaping, substitution
  and split with Python's same-position nonempty retry. Capture snapshots preserve
  Python numbering and scalar byte ranges, including lookbehind. Fixed-width
  lookbehind finds its start by stepping back the validated number of scalars,
  then executes its body forward to the original edge. Repeated captures retain
  their last forward value; assertions share the existing rollback machinery.
  Detailed owned matches and borrowed contexts expose nullable `LastIndex` and
  `LastGroup`, derived from capture-close order on the successful path. Group zero
  is excluded; `((a))` closes group 1 last. The name comes from that same Python
  number, and is null when the last capture is unnamed. Failed matches or matches
  with no participating capture have null completion metadata. Owned metadata
  remains valid after cursor advances, failure or disposal.
  One-shot search/match/fullmatch and detailed data likewise use the profile
  scalar program, with byte starts and the original assertion context. General FullMatch
  adds an end assertion before discovery so alternatives can backtrack to a
  complete result. Production BCL matching and result shaping have been removed;
  complete common resource gates remain open. Immutable scalar.52 predates the
  class correction: with host Unicode 16 data it wrongly admits U+1C89/U+1E5D0
  as word members and U+1E5F1 as a word/digit. The direct predicate source fixes
  that demonstrated gap without changing Core's general-category contract.
  The earlier expanded-pattern prototype remains rejected for its measured
  public/preparation costs; its bytes and evidence are preserved separately in
  `artifacts/python-category-version-20261016/`. Current direct-predicate evidence
  is in `artifacts/python-pinned-predicates-20261016/`. Source acceptance does not
  change a frozen candidate or qualify every remaining profile objective.
* CPython 3.13.2 searches a supplementary scalar with a scoped-ASCII complement
  (`(?a:\W)`, `(?a:\D)` and bracket forms) and misses, while its own match/fullmatch
  find the whole scalar. Search, Match, and FullMatch here stay mutually consistent
  and follow the match/fullmatch oracle.

Unsupported or incomplete cases should fail explicitly rather than silently approximating Python semantics.

Source beyond immutable scalar.68 may execute a bare positive greedy class run
inside the existing shared VM without per-scalar rewind/journal state. Exact
capture-free terminal-root eligibility uses the same pinned class predicates;
no Python class expansion or second matcher is introduced. The same complete
predicate omits unused repeat vectors through the existing workspace owner;
bare simple Count may admit working/result/depth1, while actual class-program/
capture/result storage remains admitted. Removed acquisition work changes numeric
cutoffs without a replacement charge or new ownership flag. Original scalar work
charges, token/deadline and working/result admission remain, while fewer actual
frames/work can admit tighter caps. Captured/lazy/nullable/tail/assertion/full-end
contexts retain their general paths. The measured tradeoff includes a small
captured-class Count cost from sharing class matching; current source evidence
is separate from package qualification and broad profile performance claims.

## Cancellation and resource contract

Constructors validate an explicit resource policy first, then compile flags and
the timeout, before decoding UTF-8 pattern bytes or acquiring preparation storage.
Unsupported/incompatible flags and an invalid timeout therefore take precedence
over malformed pattern bytes and construction admission. Valid arguments retain
strict pattern decoding and the existing shared construction backstops. These
checks do not provide a public construction budget or construction cancellation.
The explicit `Utf8PythonRegex.Compile` text/UTF-8 factories additionally accept
`Utf8ConstructionPolicy`, a construction timeout and construction token. They
share one eager owner through strict decoding, parsing, translation, scalar
compilation, capture metadata and preparation of the derived FullMatch program.
That program's instruction copy and metadata are admitted before publication,
even when the caller never uses FullMatch. Explicit compilation can therefore
cost more or reach a construction limit that formerly deferred preparation;
it does not retain a construction owner, policy or token for future calls.
Positive work/depth/instruction/byte limits
tighten the fixed ceilings; null retains them. Limits bound covered admission,
including major growth overlap, rather than every acquisition or process memory.
Pre-cancellation wins over argument/decoding failures; later cancellation keeps
the original token and a deadline failure throws `RegexMatchTimeoutException`.
For UTF-8 pattern timeout failures the exception's Pattern is empty, avoiding an
unbounded diagnostic decode. Construction authority ends before publication;
the expression's invocation policy/token/timeout remain independent. Replacement
preparation, CLR/JIT metadata, incidental acquisitions
and caller retention retain separate scope. Static caches are not consulted.
Development after scalar.85 lets exact, capture-free literal FullMatch value and
string operations borrow Core's complete literal finder under the caller's one
invocation. Both byte endpoints must cover the requested suffix, including for
empty literals. Full-subject UTF8 validation, derived UTF16 coordinates, original
cancellation/deadline and all four finite controls remain. These calls require no
lazy full-program preparation or per-scalar VM dispatch; explicit Compile still
eagerly prepares its existing programs. Required text remains result-admitted.
Detailed, captured, folded, alternating and assertion routes retain their existing
handling. Actual removed work may lower numerical requirements; public units,
ownership and unlimited defaults do not change. Immutable85 predates this source.
Development after scalar.71 counts and decodes UTF8 pattern bytes in scalar-aligned
blocks of at most4096bytes under that same construction owner. The two original
numeric work charges and single admitted string remain; no intermediate text/byte
buffer is acquired. Strict malformed-byte indices/offending bytes stay relative
to the whole input, although inner decoder diagnostic wording can differ for a
later block. Cancellation/deadline can precede a later malformed suffix; failed
fill releases unpublished string capacity and preserves parent reservations.
Subsequent source acquires non-surrogate semantic BMP literal nodes directly
through the existing admitted scalar-node factory, rather than rendering and
reparsing each atom through the Core frontend. Python parsing has already expanded
case partners; the old leaf conversion and direct node both use FoldCase=false.
Uncased literals under IgnoreCase therefore need no extra option switch. Classes,
raw/astral fragments and isolated surrogate rejection retain their required routes.
The same scalar lowerer, VM, captures, references and pinned Python case data remain.
Less actual parser work/storage/depth intentionally changes numeric construction
cutoffs: long patterns can fit the fixed backstop that formerly denied them.
Explicit finite construction caps, original interruption and pre-acquisition major
admission remain; no new Core hook, engine, owner, mode or accounting layer.
Constructors still defer FullMatch preparation. For scalar FullMatch
value/context/data/string calls, invocation authority is created before accessing
that lazy program; explicitly compiled instances already have it. Preparation copies the existing
top-level scalar instructions, replaces their terminal Accept with an absolute
end anchor, and appends Accept. Existing instruction targets and backtracking
remain intact. Immutable predicate tables, assertion programs and capture names
are shared; the expression no longer retains its parse tree to compile again.
New instruction storage and program metadata retain construction admission.
Elapsed cold preparation therefore
counts toward the original absolute match deadline; the cursor does not restart
it. Cold compilation observes that call's token and deadline at the existing
construction checkpoints, preserving any enclosing construction authority.
Preparation retains fixed separate construction backstops and is not charged to
execution work/bytes. Interrupted or otherwise failed lazy preparation is
retryable; a fresh invocation can compile again. Concurrent cold callers may
each compile under their own bounded construction scope; one immutable program
is published and reused. Successfully published code survives later call
interruption. Warm calls retain their existing execution checkpoints.

Finite-timeout bulk `FindAll`, `FindAllToStrings` and `FindAllToUtf8` create
invocation authority before subject validation or byte-floor projection. Eligible
native collection borrows that invocation through discovery and required shaping;
it does not start a second deadline after validating a prefix. Native byte entry
needs no full coordinate map; derived UTF-16 offsets still follow scalar boundaries.
The canonical counted string-literal route already shares its invocation. Source
beyond immutable scalar.64 also permits nonempty,capture-free exact literal families
to use the existing prepared cursor with finite invocation limits and live tokens.
Range staging uses the standard result buffer: work,working/result capacity and
growth overlap are admitted before acquisition,shared with final shaping. Neutral
helper failures report the original public resource kind; timeout diagnostics use
caller byte limits. Less VM scratch/depth can admit tighter policies,while extra
range staging may consume more result traffic. Other controlled/canonical/captured
routes retain their guards. No new public policy switch or ownership model.
The same profile-safe, nonempty capture-free family guard also permits Count
to borrow the caller's invocation. Eligible two/three-literal plans reuse the
existing metered native counter after whole-subject validation; other plans
retain owned counting. This pure counter needs no result payload or VM frames,
so very small working/result/depth caps can succeed. Work, original cancellation,
the absolute deadline and byte-bounded diagnostics still apply. Captured, empty,
folded and lexical-sensitive shapes retain their existing Python routes.
These source corrections preserve the original immutable63/64 receipts.
They are included in the separately qualified private Core/PythonRe scalar.65
pair at eceb60da: same-byte Windows/Linux consumers,capture/catalog controls,
exact dependencies/provenance and current end-to-end costs. See
artifacts/core-python-scalar65-20261016/HANDOFF.md for scope and remaining limits;
this does not imply full profile or host-containment completion.

Development beyond scalar.65 can also skip impossible search starts when the
owned program begins with a mandatory exact ASCII literal. Checked span searches
use bounded, metered windows; every candidate still runs the same Python VM with
its pinned and lexical predicates. Match/FullMatch remain anchored. Whole-subject
validation, original cancellation/deadline and storage admission remain in force.
A miss can avoid VM workspace and therefore succeed under smaller working/depth
caps. This does not enable Core's broader fixed-distance character-class plan or
alter result ownership. Current source costs are separate from immutable65.

Template Replace/Subn byte and string operations also create their invocation
before replacement-cache access. Existing cache checkpoints observe the call's
token/deadline during lookup, pending-entry waits and publication; cold parsing
borrows that authority through the construction checkpoints. Failure rolls back
preparation and removes its pending entry, allowing a fresh call. A cancelled
waiter does not invalidate another caller's preparation or a retained cached plan.
If the preparing caller is interrupted, a healthy waiter checks its own authority
and prepares once without caching, rather than inheriting the other caller's
interruption or entering an unbounded retry loop. A later fresh call may cache it.
The same invocation continues through subject validation, discovery and output
shaping. Construction work/depth remain separate from invocation counts.
Development source additionally reuses finite `MaxWorkingBytes` as a ceiling on
additional covered cold-template parser capacity, through the existing nested
construction owner. Fixed construction ceilings still apply. A byte denial is
`ConstructionBytes`; its `Limit` and `Requested` include the enclosing construction
reservation. It may therefore precede execution denial even when the subject has
no matches. Failure restores the parent ceiling/capacity and removes the pending
cache entry; work stays spent. Cache admission, borrowed template keys and retained
plans remain separate, so this does not establish common cumulative metering or a
combined invocation/retention envelope. Warm cache hits allocate no preparation
scope or checkpoint closure. This change is outside immutable scalar.93.

Uncached FullMatch preparation triggered by execution uses the same separate
additional-capacity ceiling when `MaxWorkingBytes` is finite. Failure leaves the
retryable lazy program unpublished and restores the enclosing owner; its byte
denial can precede invocation denial. Warm calls acquire no preparation scope.
The explicit `Compile` factory still completes FullMatch directly under its
eager construction policy and does not borrow the future invocation byte cap.
Its completed construction token/deadline is not retained for later calls.

Capture-name metadata shares the existing construction owner. The public-name
array and named-entry array are admitted before acquisition; numeric group labels
admit their formatted text capacity before materialization. Named strings and the
parser's immutable name map are borrowed. One filled array replaces the former
scratch/output pair, and direct entry filling replaces a temporary list/copy.
Replacement lookup reuses the same immutable parser map rather than duplicating
it. Helper failure restores its parent's numeric storage/depth while spent work
remains charged. Conservative text charges can double-count shared small-number
strings. Public getters still return caller-owned array copies with their existing
standalone-getter scope. These are covered major metadata extents, not complete
construction/live-retention/default policies or a process-memory guarantee.

Every execution operation has an explicit `CancellationToken` twin that throws `OperationCanceledException` carrying an already-canceled token before matching begins, ahead of input validation. `FindIterDetailed` materializes its results. Scalar global operations retain one invocation across whole-subject validation, discovery, capture mapping, bounded copying/decoding and callback boundaries. Tokens interrupt work synchronously; cancellation from an evaluator is observed immediately after it returns. The operation deadline persists across matches and callbacks instead of restarting for each discovery call. Library code does not interrupt arbitrary user callback code.

Timeouts pass through with `RegexMatchTimeoutException` identity. PythonRe supports
finite `Utf8ResourcePolicy.MaxWorkSteps`. One counter covers full-input validation,
candidate/backtracking work, required coordinate mapping, bounded result copying,
decoding, callback-return encoding and publication. The explicit match cursor
keeps that counter across moves; each separate operation starts a new invocation.
Exhaustion throws `Utf8ResourceLimitExceededException` with `ExecutionWorkSteps`,
`Limit` and `Requested`. Counters saturate at `ulong.MaxValue`; a denied request
beyond that value reports `Requested = ulong.MaxValue` without wrapping.

A step is a bounded engine/checkpoint unit, not one byte or scalar. Work counts
may change between releases; validation/copy loops operate in bounded blocks.
Eligible nonempty searches borrow an existing exact mandatory-prefix plan to skip
impossible scalar starts. The Python VM still verifies candidates, captures and
assertions. Anchored operations and empty-match retry preserve their existing
paths; finite candidate scans charge the same invocation in bounded blocks.
The limit does not bound wall time, retained output bytes or process
memory. Pattern compilation, lazy full-match compilation and replacement-plan
preparation retain separate construction backstops; they are outside execution
work counts. Arbitrary evaluator execution and later standalone result getters
are outside this cap; the library charges encoding/copying the evaluator's return.
Cancellation and the absolute invocation deadline remain independently enforced.

When either invocation byte cap is finite, timeout failures omit diagnostic
subject text: `RegexMatchTimeoutException.Input` is empty. This applies to
one-shot, bulk, durable cursor and cold preparation failures. The original
timeout remains authoritative, with the existing route's pattern metadata and
configured duration; no new result admission replaces it. Unlimited and
work/depth-only policies keep their diagnostic input. Omitting this copy does
not bound exception metadata, callbacks, retained host storage or process RSS.

PythonRe and core also admit finite `MaxResultBytes`, charging modeled major
arrays/text, capture and name roots, coordinate maps, staging and output growth
before acquisition. The cumulative counter never refunds and may conservatively
charge reused storage again. Exhaustion reports `ExecutionResultBytes` with
`Limit` and `Requested`. It covers required result capacity traffic, not live
caller retention or process memory. Compilation/preparation, arbitrary callback
execution and allocations, fixed values, borrowed subjects and later standalone
getters have separate scope. Completion metadata reuses the existing rollback
trail and maps Python slots with bounded metered traversal; enlarged result
structs participate in the usual array/staging extent charges.

PythonRe and core also admit positive finite `MaxDepth` and `MaxWorkingBytes`.
Depth bounds live engine backtracking/continuation frames or pending linear
alternatives before acquisition. Working bytes conservatively cover major active
arrays, coordinate maps, output controls and old/new growth overlap. Denials report
`ExecutionDepth` or `ExecutionWorkingBytes` with `Limit` and `Requested`; zero
budgets remain invalid. Exclusive scratch releases synchronously; published
copied-cursor reservations retain their invocation-long credits. Borrowed subjects,
programs, incidental metadata and caller-retained results have separate scope.
Cold replacement parsing and execution-triggered FullMatch preparation also
borrow this byte value as the separate phase
ceiling described above; other preparation keeps its documented scope.
These controls do not establish exact GC traffic, process-memory bounds,
common construction/live-retention policies or safe defaults.

`CreateMatchCursor(ReadOnlyMemory<byte>)` is an incremental, explicitly owned
alternative to the materialized `FindIterDetailed` array. Its offset/token overloads
retain a single invocation across advances, including host waits in the original
absolute deadline. The memory subject must stay unchanged and its manager alive
until EOF or disposal. Early abandonment requires `Dispose`; EOF/failure clean up
synchronously, without a finalizer. Reference aliases share position and lifetime;
concurrent/reentrant access is rejected. `Current` is valid only after a successful
advance, while independently owned snapshots remain valid after future advances
or disposal. Exclusive cursor scratch stays charged while positioned, including
projection and host waits, and is released on EOF, failure or disposal. Existing
span/value API lifetimes are unchanged. Common construction, live retained-result
and safe-default policies remain unfinished.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None);
Assert.Throws<OperationCanceledException>(() => regex.Count("cat horse"u8, 0, canceled.Token));
```
