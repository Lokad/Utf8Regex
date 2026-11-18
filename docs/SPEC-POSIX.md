# POSIX profile contract for 0.5.0

`Lokad.Utf8Regex.Posix` is an optional .NET 10/11 adapter with an exact Core
dependency. It owns ERE admission and POSIX selection. Core, PythonRe and PCRE2
retain their contracts. This is an invariant, scalar ERE subset, not complete
locale-sensitive POSIX, GNU or BSD compatibility.

## Subject text contract

Subjects are already-valid UTF-8 text, borrowed unchanged during an operation or
cursor lifetime. The profile does not impose a whole-input validity pass or
malformed-suffix rejection as an additional POSIX compatibility requirement.
Invalid UTF-8 subjects are outside this profile's domain. External byte validation
belongs at the application's text boundary.

There is no subject-validation option, property or token. Pattern validation,
managed bounds, scalar selection, captures, byte-range publication, resource
admission and original synchronous cancellation/deadlines remain independent.
Only requested matching and output work needs to inspect text.

## Input and syntax

Patterns are well-formed Unicode strings; subjects are strict UTF-8. A matching
character is one Unicode scalar, including supplementary characters and U+0000.
Offsets refer to the original bytes; results expose only byte ranges.
A start offset must be a scalar boundary in `[0,input.Length]` and does
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
Unicode scalar. There is no culture-dependent classification.

`PosixCompileOptions.IgnoreCase` applies the invariant Unicode 16.0 simple-
lowercase relation shared with Core: scalars with the same simple lowercase
form are equivalent, with invariant `I`/`i` paired and dotted `İ`/dotless `ı`
independent. This is a single-scalar relation, not full Unicode case folding:
`Σ`/`σ` pair, final `ς` stays separate; `S`/`s` pair, long `ſ` stays separate.
It does not normalize text or expand `ß` into `ss`. Construction and execution
ignore ambient culture. Literals and bracket members, including the ASCII named
classes, accept this case closure; negation applies after closure. Thus `[k]`
accepts `K`, and `[[:upper:]]` accepts `a` and `K`, without becoming a general
Unicode category or locale class. Supplementary pairs use complete scalars.

Folding preserves the original subject and byte coordinates, even when partners
have different UTF-8 widths. `(k|kk)` on `KK` selects all four original bytes.
Whole-match longest selection and hierarchical capture priority remain unchanged;
an ordered alternation is not a substitute for folded POSIX selection. The option
composes with `Newline` and byte search floors for all operations and cursors.

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

## Public surface and lifetimes

Namespace `Lokad.Utf8Regex.Posix`; immutable, reusable `Utf8PosixRegex`:

- Constructor: `Utf8PosixRegex(pattern, options=None, policy=null)`, using the
  shared immutable `Utf8RegexPolicy`. Options contain `None`, `Newline` and `IgnoreCase`.
  Properties expose `Pattern`, `Options`, `MatchTimeout` and `GroupCount`
  (capturing parentheses, excluding zero).
- `Compile(pattern, options=None, policy=null, cancellationToken=default)` uses
  the same policy and an independent construction token, checked before pattern
  or policy work and not retained for matching.
- `IsMatch(input, token=default, workingMemoryAllowance=null)` returns existence.
- `Match(input, startOffsetInBytes=0, captures=default, token=default,
  workingMemoryAllowance=null)` returns `Utf8CaptureRange` for the selected whole
  match and writes available slots zero onward into a caller-owned capture span. Clear supplied
  output slots before publication, including after no match; leave no partial
  result on refusal/interruption. Excess slots are untouched. A shorter or empty span cannot alter
  selection, capture priority or the whole match.
- `Count(input, startOffsetInBytes=0, token=default, workingMemoryAllowance=null)`
  counts nonoverlapping selected matches.
- `CreateMatchCursor(ReadOnlyMemory<byte> input, startOffsetInBytes=0,
  token=default, workingMemoryAllowance=null)` returns a sealed disposable
  `Utf8PosixMatchCursor`. `MoveNext(out Utf8CaptureRange match,
  Span<Utf8CaptureRange> captures=default)` uses the same
  selection and cumulative invocation. No copied value cursor or finalizer lease.

This surface deliberately uses caller-owned capture spans and value coordinates.
Values survive later calls and cursor disposal without retaining regex storage or
subject bytes. Keep a cursor's borrowed memory unchanged and alive until disposal;
copied references alias one cursor, and disposal rejects further advancement.
Use a cursor from one caller at a time; concurrent advances/disposal are unsupported.
Dispose refunds temporary workspace synchronously. Callers wanting text or owned
arrays materialize them themselves. No replacement/split API, capture-history
collection, RTL option or separate NonBacktracking mode is part of this subset.

The policy's `ResourcePolicy` and `MatchTimeout` govern each invocation;
`ConstructionPolicy` and `ConstructionTimeout` govern compilation. Null timeouts
mean infinite; explicit zero timeouts are invalid. Default constructors inherit an
existing internal construction authority. Explicit Compile or explicit
construction/funding controls own one construction scope and cannot replace an
active owner. Numeric bounds remain independent of shared memory admission.

`ProgramMemoryAllowance` admits covered program storage before publication.
Dispose funded regexes: closing rejects new matching calls but admitted calls and
cursors retain funding until completion or disposal. A null per-operation working
allowance inherits program funding; an explicit allowance overrides it for working
storage only. Refusal is terminal. Dispose without program funding is a no-op.
Scalar metadata and copied byte ranges remain valid after close. Cursor aliases
share one lifetime; early termination requires disposal, with no finalizer or GC
refund guarantee. Program charges use the existing conservative construction
capacity model rather than exact live-object measurement.

`Utf8CaptureRange` exposes `Success`, inclusive `StartOffsetInBytes` and exclusive
`EndOffsetInBytes`. An absent capture is the default value; a participating empty
capture has equal endpoints and `Success=true`. Publication copies the selected
byte endpoints without scanning the prefix or matched content. No UTF-16
coordinates, alignment flags, coordinate map or cursor coordinate cache remain.
Removing this output work can lower modeled work requirements; matching,
capture selection, allocation admission and interruption checks stay metered.

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
Scalar execution retains earliest-start/longest-whole selection and Boolean
existence. An acceptance at physical input end is already maximal. Other general
acceptances can use the normal consuming predicates to exclude the next scalar:
the global walk has no instruction-count cap and polls at most32 instructions
apart. If a predicate accepts it, the existing at-most64-instruction/2048-byte
reachability refinement may still exclude it from the accepted prefix. Unknown
operations, decoding failure or a cutoff keep input end as the conservative
ceiling. A longer acceptance can refresh an unresolved ceiling. These proofs use
the original work/interruption authority; they neither select captures nor
allocate a second execution state.

Fixed sequences without choices or repetitions have one parse. Existing proofs
also retain first acceptance for homogeneous greedy literal runs, suitable equal
literal alternatives, eligible flat ASCII-origin runs and repeated literal
captures. Those routes keep one trace and project it directly, except the flat
run case below. First and
mandatory empty participation remain; redundant optional empty tails lose.
No separate saved-best trace or competing-history copy/comparison remains.
One greedy single-scalar repeat after a fixed scalar prefix, followed only by
capture operations, also has one longest parse. Its ordinary scalar interpreter
keeps counters, decoding, predicates and checkpoints but omits impossible shorter
exit frames. A failing optional atom selects that exit directly. Body captures,
forks and preceding repeats retain their existing rules. This can lower
pending-depth/working requirements without weakening work
or interruption enforcement; scalar values and finite repeat bounds are unchanged.
Fixed scalar suffixes and anchors around one such repeat still require ordinary
endpoint rewind and its frames. First successful greedy acceptance nevertheless
maximizes the total scalar count and determines every non-repeated capture edge.
It can reuse the native trace without general capture-chart construction. These
tails do not receive the terminal-exit annotation; pending depth stays bounded by
the actual scalar alternatives, and numerical working/work requirements may change.
For positive ASCII-origin literals/classes, invariant lowercase-fiber keys can
also prove folded run boundaries. Intersecting scalar closures necessarily share
an ASCII key. This proof changes selection eligibility only: Unicode partners
use ordinary scalar consumption and the existing trace, with original byte
positions. It does not admit a Unicode partner to an ASCII byte shortcut.
When the first proved run has a positive minimum, its existing scalar predicate
can reject an impossible start before capture events or repeat counters execute.
Nullable or unproved prefixes decline this check. State reset, scalar-aligned
candidate order and the original work/interruption authority remain; numeric
work requirements can decrease without introducing another executor or replay.
Flat positive ASCII class or unfolded ASCII literal runs also allow direct run
consumption in the scalar executor when greedy boundaries prove the selected
parse. A required piece must be disjoint from all preceding variable runs; an
overlapping repeated successor must have minimum zero. Finite maxima remain effective.
The same proof covers a single run and mandatory fixed ASCII-origin literals or
classes between runs. Each required piece is disjoint from earlier variable
runs; fixed pieces do not create rewind choices. This can omit frames, undo and
counter capacity for patterns such as `Z(a+)()`, without a second executor.
Any anchor in a nullable sequence declines this proof, because an interior anchor can require
rewinding an earlier run. A multiline end anchor with an LF-consuming run
declines only direct byte consumption. Positive disjoint runs retain semantic
first acceptance through ordinary scalar execution and trace projection: their
intervening boundaries are unique, and greedy rewind tries final endpoints
longest first. For example, `^([a-z]+)([[:space:]]+)$` with `Newline` on
`a \nb` selects `a `; consuming the LF cannot turn that hit into a miss.
Each examined byte
remains metered; impossible alternative exits, their undo journal and unused
repeat-counter capacity are omitted. Work, pending depth and working admission
can therefore be lower for these programs; capture selection is unchanged.
Their public capture events occur outside repeat bodies, without forks or repeated
occurrences. Construction can admit one balanced compiler-only wrapper around a
atom after verifying its tag is not a public capture. The original syntax
tags stay in the program; this proved route skips that private occurrence history.
Repeated public captures, required overlapping successors, anchored nullable
sequences and uncertain bodies retain their existing selection. Existing state
slots keep the selected public opening/closing byte offsets;
no trace is acquired or replayed for this route. State admission remains before
acquisition, while numerical work/working requirements can fall. Other proven
routes and general histories retain their existing trace/interval behavior.

Other requested captures select completed syntax intervals after the scalar
executor fixes the whole winner. Immutable syntax records carry conservative
maximum byte consumption; impossible extents and split endpoints are excluded
before exploration. Keys contain syntax, valid byte start/end and a bounded or
saturated repetition count. A memo decision selects failure, a branch or a split;
it does not retain an unfinished history or an input coordinate map. An explicit
admitted dependency stack avoids CLR recursion. The closed-subexpression order
retains earlier-branch participation, concatenation priority, full repeated
occurrence ordering and required empty iterations. Projection streams the selected
children into final group ranges, clearing descendants when a new enclosing
capture occurs. It never compares only the last returned slots.

Retained interval state depends on syntax, subject boundaries and repeat counts,
not the number of explored histories. It can be quadratic in the number of subject
boundaries; this is not a linear-memory or linear-time promise. Scalar whole/
existence discovery can still require expensive search. Short capture destinations
and cursor aliases retain the same selection and original cumulative authority.

Construction keeps scalar-instruction, traversal, byte and depth bounds and admits
major immutable selector arrays before allocation. Runtime charges dependency,
split and projection work under the same invocation, including capacity growth.
Depth limits bound pending scalar alternatives or live dependency/projection
frames in their respective phases. These depths and numerical work requirements
can differ from the former DFS-history requirements. General scalar-choice buffers
are refunded before closed capture selection, and all remaining loans refund
synchronously on completion, refusal, cancellation or disposal.

Dictionary growth uses a conservative capacity charge: four typed key/value-array
and index-array extents, with actual element strides and old/new growth overlap.
It is major-buffer admission, not per-object or hash-probe accounting. Native
trace/state buffers retain their existing admission. Ordinary general captures
can allocate dictionary storage where warmed history traversal previously did
not; first-acceptance routes retain their existing allocation strategy. Recheck
finite policies when upgrading rather than relying on exact prior step/byte totals.

Scalar scans and capture publication retain bounded original interruption checks.
Results copy byte endpoints without a text-coordinate or validity pass. Scalar
units, the valid-text contract, captures and original-token/deadline behavior stay.

Working limits cover modeled matching/history/projection capacity, not borrowed
input, the compiled program, fixed owner metadata, all GC traffic or process RAM.
Caller-owned capture destinations and fixed value coordinates need no producer
result payload; admit any producer-owned result staging before allocation.
These APIs currently create no producer-owned result payload or result staging;
any positive `MaxResultBytes` therefore admits their value/caller-destination output.
Matching traces, interval cells and projection dependencies remain working storage. This limit does not cap arrays/text
that callers create afterward. Timeout exceptions omit subject text.
Shared allowance loans release on completion, refusal, cancellation or disposal.
Whole-only calls admit only their required repeat/alternative state; flat patterns
can need no covered buffer. A cursor reserves native tag or final-group capacity at construction because
later advances can request captures, and acquires trace/interval storage only
when needed for that advance. Switching output sizes through aliases preserves capture selection
and retained values; it does not reset cumulative work or interruption authority.

Synchronous calls borrow a checked state span, using at most128 integers of stack
storage and an exact heap vector for larger programs. Cursors retain their own
exact heap vector. Both admit the same conservative array-capacity charge before
use and keep the existing work charge and synchronous refund. This storage choice
does not change scalar selection or retained values; route-dependent numeric
requirements remain subject to the admission rules above.

## Qualification boundary

[Independent selection fixtures](../tests/Lokad.Utf8Regex.Posix.Tests/Corpus/selection.json)
pin ambiguous, nested/repeated/nullable, absent/empty and supplementary-scalar
coordinates before execution changes. Qualification covers every capture-span size,
public discovery/count/cursor routes, valid text/scalar-aligned offsets, original-token
and deadline interruption, each finite limit, shared refusal and alias disposal
on both target frameworks. Fixture data alone is not implementation qualification.

The [profile README](../src/Lokad.Utf8Regex.Posix/README.md#benchmarks) records warm
public-operation and finite-policy costs. Four-package delivery also checks both
target frameworks, Windows/Linux and isolated package consumers. Subsequent
changes need a fresh candidate at the
next delivery boundary. No complete POSIX equivalence or general native throughput
parity is claimed.
