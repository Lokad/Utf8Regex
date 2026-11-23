# POSIX profile contract

`Lokad.Utf8Regex.Posix` implements an invariant subset of POSIX extended
regular expressions (ERE). It uses Unicode scalars for matching and owns
leftmost-longest selection and capture priority.
[API snapshot](../tests/Lokad.Utf8Regex.Posix.Tests/PublicApi.Shipped.txt)

## Subject text contract

Supply valid UTF-8 and keep borrowed bytes unchanged during matching and cursor
use. Validate external bytes where they become text. Regex operations trust
that guarantee. Pattern validation is independent.

A matching character is one scalar, including supplementary characters and
U+0000. Start offsets must be scalar boundaries between zero and the subject
length. They preserve the original byte coordinates and anchor context.

## Input and syntax

Patterns are well-formed Unicode strings.

| Admitted ERE | Meaning |
| --- | --- |
| Literals, concatenation, `\|`, `(expression)` | Scalar matching. Parentheses capture in opening order. |
| `.`, `[abc]`, `[^abc]`, `[a-z]` | One scalar. Ranges follow scalar value order. |
| `*`, `+`, `?`, `{m}`, `{m,}`, `{m,n}` | Repetition. Interval bounds range from 0 to 255 in ascending order. |
| `^`, `$` | Absolute subject edges by default. Special outside brackets. |
| `[[:name:]]` | One of the twelve fixed ASCII classes below. |
| Empty expression, branch or `()` | Participating empty match. |
| Backslash before ERE punctuation | Literal `.^$[]()\|*+?{}\\` outside brackets. |

Inside brackets, `]` is literal first after optional `^`, `-` is literal
first or last, and backslash is ordinary. Escape braces outside valid
intervals. Descending ranges, malformed intervals, adjacent quantifiers and
repetition of anchors are errors.

The fixed C-locale classes are `alnum`, `alpha`, `blank`, `cntrl`, `digit`,
`graph`, `lower`, `print`, `punct`, `space`, `upper` and `xdigit`.
`space` includes HT/LF/VT/FF/CR/space. `blank` includes HT and space.
`cntrl` includes DEL. `graph` covers U+0021–U+007E and `print`
U+0020–U+007E. Negation can match other scalars.

`IgnoreCase` uses invariant Unicode 16 simple-lowercase equivalence.
`I`/`i` and `Σ`/`σ` pair. Dotted and dotless I, final sigma and long S
remain separate from those ordinary pairs. Folding applies to literals and
class members before negation. `[k]` accepts `K` and `[[:upper:]]` accepts
`a` and `K` with this option. Folding compares one scalar at a time, with
no normalization or expansions such as `ß` to `ss`. Byte ranges retain
the original widths. `(k|kk)` on `KK` takes all four bytes.

`Newline` allows `^` after LF and `$` before LF, in addition to absolute
edges. It excludes LF from dot and negated brackets. With default options
dot and negated brackets can include LF and `$` means true end. CR has no
special meaning.

BRE, collating or equivalence elements, backreferences, assertions other
than `^` and `$`, shorthand or property escapes, extended groups and
non-ERE quantifier suffixes are outside this subset.

## Selection and captures

Choose the earliest start and then the greatest end at that start. For
ties, compare the hierarchy outer before inner and left before right,
including unparenthesized parts. Concatenation associates to the right in
this hierarchy. Parentheses preserve their own scope.

Compare occurrences in iteration order, preferring participation over
absence, then earlier starts and later ends. Compare descendant occurrences
within their enclosing occurrence. A shorter history wins over redundant
empty iterations when otherwise equal. Independent comparisons pin the
chosen interpretation. [Tagged-history rules, pp. 10–12](https://re2c.org/2017_trofimovich_tagged_deterministic_finite_automata_with_lookahead.pdf) ·
[WG15 interpretation 135](https://www.open-std.org/JTC1/SC22/WG15/docs/rr/9945-2/9945-2-135.html) ·
[Selection fixtures](../tests/Lokad.Utf8Regex.Posix.Tests/Corpus/selection.json)

Return the last capture from the winning history. A group absent in the
final iteration of its enclosing group is absent. `GroupCount` excludes
slot zero, which holds the whole match. An absent capture is the default
unsuccessful value. A participating empty capture retains its byte position
and `Success = true`.

## Public surface and lifetimes

`IsMatch` returns existence. `Match` returns `Utf8CaptureRange` and can
fill a caller span from slot zero. Short destinations preserve selection
and excess slots remain untouched. A miss or later failure clears the
requested prefix. Entry cancellation preserves it. Ranges own byte
coordinates and remain usable after later calls or disposal.

`Count` and `CreateMatchCursor` discover nonoverlapping matches. After a
nonempty match, resume at its end. After an empty match, advance one scalar.
An empty match at subject end is emitted once. Cursor aliases share position
and disposal. Use one caller at a time, keep the borrowed memory alive and
dispose a cursor on abandonment. Completion, failure and disposal refund
workspace synchronously. The caller can supply capture slots on each advance.

Replacement, splitting, RTL, capture history and a separate NonBacktracking
mode are outside this subset.

## Interruption and admission

`Utf8RegexPolicy` separates construction limits and timeout from invocation
limits and timeout. Null timeouts mean infinite. `Compile` checks its
construction token before pattern or policy work. Default constructors can
borrow an active construction scope. Major buffers and instructions need
admission before allocation. [Shared construction rules](SPEC.md#construction-preparation-and-caches)

One original token and deadline cover each operation and all cursor advances
and waits. Cooperative cancellation reports the original token. Expiry
throws `RegexMatchTimeoutException` with empty `Input`. Refusal ends the
operation without partial output. Work covers discovery, matching, capture
selection and publication. Proved case-sensitive ASCII runs that need no
line-end rewind charge one step and check the deadline before searching each
block of at most 64 bytes. Discovery can use the same bounded scan to skip
rejected ASCII starts when the first required atom proves their exclusion.
Depth covers live alternatives and capture
dependencies. General capture workspace can grow quadratically in subject
boundaries. It requires working admission, including growth overlap.
Proved direct runs reserve capture workspace for public groups only.

POSIX publishes fixed ranges or writes caller-owned slots. Any positive
`MaxResultBytes` admits those outputs. Cursor creation can admit capture
slots for later advances. Caller arrays, decoded text and borrowed subject
bytes fall outside execution-byte limits. Numerical requirements can change
with the pattern and version. [Shared resource rules](SPEC.md#cancellation-deadlines-and-resource-limits)

`ProgramMemoryAllowance` funds covered program storage. An optional working
allowance overrides program funding for workspace only. Funded close rejects
new calls and refunds after admitted calls and cursors finish. Unfunded disposal
is a no-op. Pattern metadata and copied ranges survive close.

[Profile examples and benchmarks](../src/Lokad.Utf8Regex.Posix/README.md)
