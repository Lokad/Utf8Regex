# POSIX profile contract for 0.5.0

`Lokad.Utf8Regex.Posix` is an optional .NET 10/11 adapter with an exact Core
dependency. It owns ERE admission and POSIX selection. Core, PythonRe and PCRE2
retain their contracts. This is an invariant, scalar ERE subset, not complete
locale-sensitive POSIX, GNU or BSD compatibility.

## Subject input validation

The immutable constructor/factory `inputValidation` option and read-only
`InputValidation` property follow the [shared subject contract](SPEC.md#subject-input-validation).
`Validate` is default: strict whole-input validation runs at most once per operation
or cursor lifetime. `AssumeValid` omits that pass under the caller's valid-UTF-8,
unchanged-borrowed-input guarantee. Pattern/replacement checks, managed bounds,
Unicode semantics, coordinates, cancellation/deadlines and resource admission
remain independent. Below, mandatory subject-validation statements apply to
Validate mode; coordinate/scalar/result work can still inspect input in either mode.
No per-call policy or public validation-token API is introduced.

## Input and syntax

Patterns are well-formed Unicode strings; subjects are strict UTF-8. A matching
character is one Unicode scalar, including supplementary characters and U+0000.
Validate mode checks the entire subject before matching, including bytes outside a requested
search range. Primary offsets refer to the original bytes; UTF-16 coordinates are
derived. A start offset must be a scalar boundary in `[0,input.Length]` and does
not rebase anchors or remove prefix context.

| Admitted ERE | Meaning |
|---|---|
| Literals, concatenation, `|`, `(expression)` | Case-sensitive scalar matching; parentheses capture in opening order |
| `.`, `[abc]`, `[^abc]`, `[a-z]` | One scalar; ranges use scalar-value order, without locale collation |
| `*`, `+`, `?`, `{m}`, `{m,}`, `{m,n}` | Repetition; interval numbers are 0–255, ordered when both are present |
| `^`, `$` | Absolute subject edges by default; both remain special outside brackets |
| `[[:name:]]` | The twelve fixed ASCII classes listed below |
| Empty expression, branch or `()` | Participating empty match; an explicit supported extension |
| Backslash + ERE punctuation | Literal punctuation outside brackets: `.^$[]()|*+?{}\` |

In brackets, `]` is literal first (after optional `^`), `-` first/last, and
backslash is an ordinary character. Descending ranges, malformed intervals,
adjacent repetition operators and repetition of anchors are errors. Braces
outside valid intervals must be escaped.

The named classes are `alnum`, `alpha`, `blank`, `cntrl`, `digit`, `graph`, `lower`,
`print`, `punct`, `space`, `upper` and `xdigit`, with ASCII C-locale membership.
`space` is HT/LF/VT/FF/CR/space; `blank` is HT/space; `cntrl` includes DEL;
`graph` is U+0021–U+007E and `print` U+0020–U+007E. Negation can match any other
Unicode scalar. There is no culture-dependent classification or case folding.

`PosixCompileOptions.Newline` makes `^`/`$` recognize edges after/before LF as
well as absolute edges, and excludes LF from dot and negated bracket expressions.
Without it, dot and negated brackets can match LF, and `$` means true end (no
special position before a trailing newline). CR has no special treatment.

Reject BRE-specific operator interpretations, collating/equivalence elements, backreferences, lookarounds,
word boundaries, shorthand/property escapes, named/noncapturing groups, inline
flags, lazy/possessive repetition, atomic/balancing/conditional groups and unknown
options. Do not reinterpret these as Core syntax or invoke another matcher.

## Selection and captures

Choose the earliest possible whole-match start, then the greatest end at that
start. Resolve equal whole matches through hierarchical POSIX subpattern priority:
outer nodes before descendants, left parts before right parts. This includes
unparenthesized concatenation/repetition parts, not only returned capture slots.
Associate concatenation to the right for this hierarchy: the first part takes
priority over the remaining suffix. Parentheses preserve their explicit scope.

Within that hierarchy, compare occurrences in iteration order, preferring an
earlier start and then a later end. Participation, including empty, beats absence.
Repeat histories are compared before discarding earlier iterations; if otherwise
equal, a shorter history wins over redundant empty iterations. Descendant
occurrences are compared within their enclosing occurrence. These rules follow
the tagged-history interpretation in [Trofimovich, POSIX section, pp. 10–12](https://re2c.org/2017_trofimovich_tagged_deterministic_finite_automata_with_lookahead.pdf).

Return only the last capture from the winning occurrence history. A group absent
in the final iteration of its enclosing group is absent, even if it participated
earlier. Capture numbering follows opening parentheses, starting at one; zero is
the whole match. Empty captures retain `Success=true` and their actual position;
absent captures use the default unsuccessful value. Do not select by the lengths
of the final returned slots or by the first accepting ordered path.

Historical interpretations identify ambiguity across implementations; see
[WG15 interpretation 135](https://www.open-std.org/JTC1/SC22/WG15/docs/rr/9945-2/9945-2-135.html).
The rules and fixtures here choose one documented policy. Native implementations
and ordered Core/.NET are not universal capture oracles.

## Initial public surface and lifetimes

Namespace `Lokad.Utf8Regex.Posix`; immutable, reusable `Utf8PosixRegex`:

- Constructors: pattern only; or pattern, `PosixCompileOptions`, timeout and
  `Utf8ResourcePolicy`. Options initially contain `None` and `Newline` only.
  Properties expose `Pattern`, `Options`, `MatchTimeout` and `GroupCount`
  (capturing parentheses, excluding zero).
- `Compile` additionally accepts `Utf8ConstructionPolicy`, a construction timeout
  and original cancellation token. Use existing construction admission/backstops.
- `IsMatch(input, token=default, workingMemoryAllowance=null)` returns existence.
- `Match(input, startOffsetInBytes=0, token=default, workingMemoryAllowance=null)`
  returns `Utf8ValueMatch` for the selected whole match.
- `Match(input, Span<Utf8ValueMatch> groups, startOffsetInBytes=0, token=default,
  workingMemoryAllowance=null)` also writes slots zero onward. Clear supplied
  output slots before publication, including after no match; leave no partial
  result on refusal/interruption. Excess slots are untouched. A shorter or empty span cannot alter
  selection, capture priority or the whole match.
- `Count(input, startOffsetInBytes=0, token=default, workingMemoryAllowance=null)`
  counts nonoverlapping selected matches.
- `CreateMatchCursor(ReadOnlyMemory<byte> input, startOffsetInBytes=0,
  token=default, workingMemoryAllowance=null)` returns a sealed disposable
  `Utf8PosixMatchCursor`. `MoveNext(out Utf8ValueMatch match)` and
  `MoveNext(Span<Utf8ValueMatch> groups, out Utf8ValueMatch match)` use the same
  selection and cumulative invocation. No copied value cursor or finalizer lease.

This surface deliberately uses caller-owned capture spans and value coordinates.
Values survive later calls and cursor disposal without retaining regex storage or
subject bytes. Keep a cursor's borrowed memory unchanged and alive until disposal;
copied references alias one cursor, and disposal rejects further advancement.
Use a cursor from one caller at a time; concurrent advances/disposal are unsupported.
Dispose refunds temporary workspace synchronously. Callers wanting text or owned
arrays materialize them themselves. No replacement/split API, capture-history
collection, RTL option or separate NonBacktracking mode is part of this subset.

After a nonempty match, resume at its end. After an empty match, advance one scalar;
the empty match at subject end is emitted once and completes iteration. For
example, `a*` on `ba` yields empty at 0, `a` at 1 and empty at 2. Count and cursor
agree, including nonzero starts.

## Interruption and admission

Use the [shared resource vocabulary](SPEC.md#cancellation-deadlines-and-resource-limits):
original synchronous cancellation, one absolute deadline from invocation entry,
finite work/depth/working/result policies and caller-owned working admission.
The cursor keeps its original token/deadline and cumulative work/result charges
across advances. No fallback, retry under another budget or partial successful
answer after refusal. Boolean existence can stop on a proved hit. Selected whole
matches and Count finish longest-match competition; operations returning captures
also resolve hierarchical capture priority. Whole-only output can omit unobservable
tag histories because this ERE subset has no capture-dependent acceptance.
Without requested captures, an acceptance at input end proves the longest whole
match and can stop competition immediately; other endpoints retain competition.
An internal body tag with exactly its enclosing capture's occurrences and
boundaries can be omitted; descendant priority and public captures remain intact.
For greedy pieces of one scalar literal, with every tag outside repeated bodies,
a bounded construction proof can select the first acceptance: suffix minima are
reserved, the total endpoint is maximal, and nested tags retain preorder priority.
A flat two-way alternative of capture-wrapped literal words can reuse this proof
when both have the same positive scalar length and no consuming suffix: tied
endpoints prefer the earlier branch's participating whole-span tag. Empty/unequal
branches, nested forks and assertions retain general competition.
The proof uses accounted program metadata, without a second executor.
A flat chain of positive-minimum greedy ASCII class runs can also stop at its
first acceptance when all classes are mutually disjoint and tags stay outside
repeated bodies. Each transition then has one possible boundary; the last run's
greedy endpoint is longest, with no competing capture history. Overlapping,
nullable, negated or non-ASCII classes keep general competition.
A root repetition of one capture containing only a greedy uniform literal run
can use the same first-acceptance proof when the inner minimum is zero or one
and the outer repeat is bounded or has a positive minimum. Feasible total lengths
are contiguous; greedy success maximizes the endpoint, then occurrence lengths
in order, retaining necessary empty tails. Larger inner minima, minimum-zero
unbounded outer repeats, nested captures and consuming suffixes keep general
history ranking. This changes neither capture policy nor the execution machinery.
At a proven first acceptance, the workspace transfers its admitted capture trace
to the selected-history buffer instead of allocating and copying a second trace.
General competition still keeps a separate best history. Both buffers retain
their normal reset/release lifetimes; a reused cursor can retain capacity in both.
When a repeat counter proves earlier occurrences and the minimum is satisfied,
a redundant optional empty iteration can be discarded in favor of its saved exit
at the same position. First and required empty occurrences remain eligible.

General search may visit longer fixed-width scalar alternatives first (literals,
dots and bracket atoms), preserving original
semantic tag priority. Once a saved whole match reaches a proved endpoint ceiling, a completed
losing occurrence prefix of the first root child can be discarded: forward
continuation cannot repair its earlier boundaries. Unclosed occurrences and
descendants of unresolved higher-priority parents remain inconclusive. This uses
the same admitted histories and rollback; other ambiguous histories can still
require exponential exploration. It is not a general linear-time guarantee.

History comparison can skip an exact event prefix shared with the saved best:
saving proves the prefix, appending preserves it, and rollback shortens the bound.
Skipped occurrences still count as prior participation when ranking an extra empty
tail. This retains the same pruning rule, traversal and admitted storage; work
requirements can decrease by removing repeated comparisons of unchanged events.

A bounded proof can also pass over a leading chain of once-entered enclosures
when all body edges stay inside the innermost scope and its suffix consists only
of single-scalar atoms (literals, dot or brackets) and capture markers. A fixed
scalar count determines a unique boundary in valid UTF-8 despite varying byte
widths. At a maximal whole endpoint those earlier tags
have fixed starts and ends, so the same losing-prefix rule can compare their first
unfixed child. If the first acceptance already reaches the whole ceiling,
variable or branched suffixes can use the same comparison when every preceding
parent's accepted end equals its independent alphabet or physical-input ceiling.
Suffix edges must stay between successive parent closes, so those parents
participate once and cannot be skipped or reopened. A shorter parent loses at its
own priority; equal parent boundaries expose the child. An initially shorter
acceptance or an unproved parent bound/scope retains general competition.
This uses one local tag choice, no retained program metadata/storage;
it can remove losing descendant exploration and lower numerical work requirements.

Physical input end is always a valid ceiling. After the first general acceptance,
a proof over at most 64 instructions can establish that every consuming atom is
an unfolded scalar literal or positive ASCII-only bracket. A conservative scalar
interval containing those atoms cannot cross its first excluded scalar. A checked
search of at most 2048 following bytes can therefore prove an earlier ceiling.
ASCII alphabets keep their span search; wider literal alphabets decode complete
scalars with a work checkpoint every 64 scalars. A probe cut inside a scalar,
or another proof/search miss, keeps physical input end. Whole-only selection can stop at that
ceiling; captured selection keeps the same hierarchical history rules. Actual
input, anchors, strict validation and scalar/coordinate output remain unchanged.
Proof/search use bounded work checkpoints and no additional retained storage.

Bound construction traversal/instructions and admit major arrays before acquiring
them, including growth overlap. Charge matching transitions, candidate discovery,
history comparison/copy and scalar/coordinate scans at bounded interruption
checkpoints. Bound pending alternatives/history workspace and recursion depth;
do not promise worst-case linear matching merely because ERE is regular.
Coordinate projection counts already-validated scalar ranges through the shared
bounded UTF-16 sizing helper; it does not validate those ranges a second time.
Numerical work/depth/working requirements depend on the execution route and can
change between versions, including when redundant internal histories are omitted.
Recheck finite policies when upgrading; these limits do not measure exact heap size.

Working limits cover modeled matching/history/projection capacity, not borrowed
input, the compiled program, fixed owner metadata, all GC traffic or process RAM.
Caller-owned capture destinations and fixed value coordinates need no producer
result payload; admit any producer-owned result staging before allocation.
These APIs currently create no producer-owned result payload or result staging;
any positive `MaxResultBytes` therefore admits their value/caller-destination output.
Matching histories remain working storage. This limit does not cap arrays/text
that callers create afterward. Timeout exceptions omit subject text.
Shared allowance loans release on completion, refusal, cancellation or disposal.
Whole-only calls admit only their required repeat/alternative state; flat patterns
can need no covered buffer. A cursor reserves tag capacity at construction because
later advances can request captures, but collects histories only when needed for
that advance. Switching output sizes through aliases preserves capture selection
and retained values; it does not reset cumulative work or interruption authority.

Synchronous calls borrow a checked state span, using at most128 integers of stack
storage and an exact heap vector for larger programs. Cursors retain their own
exact heap vector. Both admit the same conservative array-capacity charge before
use and keep the existing work charge and synchronous refund. This storage choice
does not lower numerical limits or change traversal, histories or retained values.

## Qualification boundary

[Independent selection fixtures](../tests/Lokad.Utf8Regex.Posix.Tests/Corpus/selection.json)
pin ambiguous, nested/repeated/nullable, absent/empty and supplementary-scalar
coordinates before execution changes. Qualification covers every capture-span size,
public discovery/count/cursor routes, malformed subjects/offsets, original-token
and deadline interruption, each finite limit, shared refusal and alias disposal
on both target frameworks. Fixture data alone is not implementation qualification.

The [profile README](../src/Lokad.Utf8Regex.Posix/README.md#benchmarks) records warm
public-operation and finite-policy costs. Four-package delivery also checks both
target frameworks, Windows/Linux and isolated package consumers; the
[0.5.0-dev.73 receipt](../bench/Experiments/DeliveryDev73-20261111/README.md)
records the accepted boundary. Subsequent changes need a fresh candidate at the
next delivery boundary. No complete POSIX equivalence or general native throughput
parity is claimed.
