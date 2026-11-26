# PythonRe contract

`Lokad.Utf8Regex.PythonRe` is the managed profile for selected CPython 3.13.2
`re` semantics over `str`. It matches Unicode scalars using pinned Unicode
15.1.0 data. Results expose byte ranges over UTF-8. Hosts derive character
positions or decode selected bytes when needed.

See the [API snapshot](../tests/Lokad.Utf8Regex.PythonRe.Tests/PublicApi.Shipped.txt)
for signatures and the [release notes](../CHANGELOG.md#upgrading) for upgrade guidance.

## Subject text contract

Supply valid UTF-8 and keep borrowed storage unchanged throughout the operation
and for as long as a cursor or borrowed result uses it. Validate external bytes
where they become text. Regex operations trust this input contract and check
the supplied offsets. Pattern, template and capture-name validation are independent.

Offsets are relative to the supplied subject, including when matching from a
nonzero offset. Start defaults to zero. `Match` and `CreateMatchCursor` accept
an end bound that defaults to the subject length. Both bounds must be on scalar
edges and satisfy `0 <= start <= end <= input.Length`. The prefix before start
remains available to anchors, boundaries and lookbehind. The selected end limits
matching and lookahead. Invalid byte bounds throw `ArgumentOutOfRangeException`.

## Operations and result shape

| Operation | Result |
| --- | --- |
| `IsMatch` and `Count` | Search from the supplied start and return existence or a nonoverlapping match count. |
| `Match` | Return a stack-only whole-match value and optionally write last captures into caller storage. |
| `CreateMatchCursor` | Advance over borrowed memory and return immutable capture snapshots. |
| `Replace` and `Subn` | Return owned UTF-8 bytes. `Subn` also returns the replacement count. |
| `SplitRanges` | Return an owned array of segment ranges and captured separators. |

`Utf8PythonMatchMode.Search` seeks at or after start. `Match` anchors discovery
at start and `FullMatch` also requires the selected end. One-shot `Match`
defaults to anchored mode. The cursor defaults to Search. Its anchored modes
publish at most one result, including a successful empty match.

Global discovery retries a nonempty match at the same position after an empty
match. If none exists, it advances by one scalar or finishes at the end.
Count, split, and substitution follow that progression.

### Captures and metadata

Capture numbers follow opening order. Slot zero is the whole match.
`CaptureGroupCount` excludes slot zero. The value match's `Success` distinguishes
failure from a successful empty match. An unsuccessful capture has zero
offsets and an empty byte view.

A caller-owned capture span requests the prefix of numbered last-capture
slots, including zero. Short spans preserve the same match and excess slots
remain untouched. Pre-cancellation preserves the output. A miss or later
failure clears the requested prefix. The whole-match value borrows the
subject independently of the capture span. Caller slots incur no result-byte
charge. Required matching scratch and publication work still need admission.

A range snapshot owns its capture metadata and borrows the subject. Default
and unsuccessful snapshots expose no capture slots. `LastIndex` identifies
the last participating capture to close on the successful path. For example,
`((a))` closes group 1 last. `LastIndex` is null when no subgroup participated.
A host can resolve it to a name for Python's `lastgroup`, using null for
an unnamed group.

`TryCopyPatternUtf8` copies the original source into a caller buffer.
`TryCopyGroupNameUtf8` copies a name by slot, using decimal labels for
unnamed slots and slot zero. `DestinationTooSmall` preserves the buffer and
reports the required byte count. Legacy string sources with unpaired
surrogates return `InvalidData` and preserve the buffer. Follow the shared
copy contract for cancellation and caller storage. [Metadata contract](SPEC.md#native-pattern-metadata)

`TryGetGroupNumber` compares strict UTF-8 names ordinally, without
normalization. Decimal strings do not alias numbered slots. Unknown names
return false with number zero. Malformed names identify their byte offset.
These metadata operations observe their own token, independently of matching
timeouts and limits, and require an open funded expression.

### Substitution and split

Pass a strict UTF-8 Python template to `Replace`, `Subn` or `SubnRetained`.
Template references include `\1` and `\g<name>`. Template validation precedes
grammar and matching, including on a subject with no match or a negative
replacement count. Zero count replaces every match. A negative count
preserves the input. Start preserves assertion context. Output copies
literal template bytes and matched scalar-aligned captures.

`SplitRanges` splits from start. Zero `maxSplit` allows unlimited splits and
a negative value performs none. A range numbered zero represents an ordinary
segment. Positive numbers represent captured separators, including absent
groups with `Success = false`. Output is admitted according to actual ranges.
When no split occurs, the result contains one range for the selected suffix.
Admission for that range covers its result array directly. A capture-free split
capped at eight matches uses bounded stack scratch and admits one result array
sized to the actual number of ranges.

## Grammar and parsing

Supported constructs include literals, classes, standard escapes, ordered
alternatives, greedy/lazy/possessive repetition, numbered and named captures,
backreferences, participation conditionals, lookahead, fixed-width lookbehind,
atomic groups, and leading and scoped flags.

| Area | Rules |
| --- | --- |
| Scalar literals | A raw or escaped raw supplementary character or `\U0001F600` is one scalar. Separate surrogate escapes such as `\uD83D\uDE00` are impossible atoms. Surrogate-only classes match no scalar. Negation uses the scalar complement. |
| Braces | Complete `{m}`, `{m,n}`, `{m,}`, `{,n}` and `{,}` forms are repeats. Other forms, including `{}`, are literal. Finite bounds through 4294967294 are distinct from infinity. Higher finite bounds produce a pattern error. |
| Verbose and comments | Trivia may separate atom and quantifier. Lazy and possessive suffixes must be adjacent. `(?#...)` is ignored in every scope. An escaped `)` keeps the comment open, and an escaped LF keeps a verbose comment open. Leading flags may follow leading trivia. |
| Modes | Global ASCII and Unicode flags conflict, including flags from separate directives or compile options. Body syntax is checked before the conflict, followed by deferred reference and width validation. Scoped ASCII and Unicode modes restore on exit. |
| Quantification | Direct quantification of `^`, `$`, `\A`, `\Z`, `\b` and `\B` is rejected. Grouped anchors and quantified lookarounds are supported. Inside brackets, `\b` represents backspace. |
| References and conditions | Backreferences require a previously closed group. Conditional names must exist when parsed. An already open named group is allowed outside lookbehind. Numeric forward IDs are checked against the final group count. Oversized IDs produce a pattern error. |
| Lookbehind | Width is measured in scalars. References and conditions inside lookbehind target closed groups declared before the outermost enclosing lookbehind. Width analysis preserves the oracle's 2^64 clamp. A fixed width beyond the 32-bit code limit reports `looks too much behind`. |
| Names | Unicode 15.1 identifier validation applies to declarations, references, conditions and replacement names. Accepted spellings are preserved and compared ordinally. |

Required nullable repeat iterations execute before the empty-progress guard
for optional iterations. Their captures can affect subsequent conditions.
For example, `(?:(?(2)(a)|()))+` consumes `a`. The rule applies to lazy and
possessive repeats.

`PythonRePatternException` provides `ErrorSource`, `ErrorKind` and `Position`.
Sources distinguish pattern text and inline flags, template text and supplied
compile options. Kinds distinguish syntax, invalid options and unsupported
features. `Position` is a zero-based **UTF-16 source position** even for byte
patterns and templates, or `-1` when unavailable. Deferred width or combined
flag failures may have no position. The public constructors default to
pattern/syntax. Unknown named replacement groups throw
`IndexOutOfRangeException`. Use structured fields and exception types for
diagnostics. Numeric pattern errors may differ from CPython in type or
position.

## Unicode and documented differences

Matching is culture invariant. Unicode literals and classes use pinned simple
lowercase mappings, their inverses and additional literal partners.
Backreferences compare simple lowercase under the flags at the reference
site. Scoped ASCII mode folds ASCII letters. Case partners may have different
UTF-8 widths.

Predefined classes and word boundaries use CPython 3.13.2 and Unicode 15.1
predicates. `\B` fails when the effective end is zero. At a nonzero start
equal to the selected end, it still evaluates the preceding scalar.
ASCII `\s` excludes U+001C–U+001F, which an explicit class can
include. Complements consume one scalar. On scoped-ASCII `\W` and `\D`, this
profile consistently accepts supplementary scalars where CPython 3.13.2
Search can skip a match that Match and FullMatch find.

Bytes patterns, LOCALE and `\N{...}` Unicode-name lookup are outside the
supported grammar. Unsupported features fail explicitly. The managed
implementation owns matching and its Unicode tables.

## Construction and execution controls

Constructors and `Compile` accept `PythonReCompileOptions` and
`Utf8RegexPolicy`. `Options` records supplied flags. `EffectiveOptions`
includes leading global flags and implied Unicode unless global ASCII is
selected. Scoped flags apply within their group. Byte patterns are validated
strictly before retained acquisition and copied by the expression. Each
`Pattern` read for a byte pattern decodes that source on request.

`Compile` observes its token before argument validation or acquisition.
Invalid policy settings name `policy`. `ConstructionPolicy` and
`ConstructionTimeout` apply before publication, independently of execution.
A null construction timeout means infinite. Default unfunded constructors can
borrow an active construction scope. Explicit compilation owns its scope.

An omitted `MatchTimeout` inherits `Utf8PythonRegex.DefaultMatchTimeout`,
initially infinite. Discovery, captures, output and publication share one
invocation with the caller token and an absolute deadline. Entry
cancellation precedes program use and argument checks. Cooperative cancellation
throws with the originating token. Deadline expiry throws
`RegexMatchTimeoutException`, retaining the original pattern on PythonRe's own
path. Delegated Core operations can propagate their own exception. Shared
working admission and finite working/result-byte policies omit the subject from
timeout diagnostics.

Eligible capture-free `IsMatch` branches can use a bounded compiled proof
after repeated searches that exceed one candidate's proof allowance. The
proof uses the same Python scalar program. Calls with a token, deadline,
resource limits or shared working admission use metered execution.

Work, depth, working bytes and result bytes follow the shared policy. Covered
storage is admitted before allocation, including overlapping capacity during
growth. Work and capacity requirements can change by version. On failure the
operation releases temporary charges and publishes no partial successful
result. [Shared resource contract](SPEC.md#cancellation-deadlines-and-resource-limits)

### Program, working and result allowances

A `ProgramMemoryAllowance` funds covered construction storage and retains
the program's conservative charge. Explicit construction limits and fixed
backstops still apply. During matching, this account also funds temporary
working capacity by default. `Match` and `CreateMatchCursor` can use a separate
working account. Program and cache charges remain on the program account.
Numeric and shared admission both apply. Shared refusal throws
`Utf8MemoryAdmissionException` and ends the affected operation.

Disposing a funded expression closes it to new matching and metadata uses.
Admitted calls and cursors hold the program until they finish. Properties
such as `Pattern` and `EffectiveOptions` remain accessible. Unfunded
expression disposal has no effect.

Replacement preparation is bounded. A cold lookup admits its immutable key
before copying, under the operation's deadline and working limits as well
as construction backstops. A key denial can report `ExecutionWorkingBytes`
and a preparation denial can report `ConstructionBytes`. Failed templates
are discarded. Active cache uses keep their program charge through eviction
or disposal until the final use ends.

Ordinary outputs transfer to the caller at publication. Result traffic stays
cumulative when temporary working charges refund. Borrowed input and
host-side copying, decoding and retention are outside regex limits. Retained
results can transfer a covered charge to the caller as described below.

## Cursor and result lifetime

`CreateMatchCursor` borrows the supplied memory. The creation token, absolute
deadline and cumulative limits apply across moves and host waits. Reference
aliases share progress and disposal. Concurrent or reentrant use is rejected.
`MoveNext` returns the snapshot for that advance. Completion, failure and
disposal release exclusive workspace and the cursor's subject reference
synchronously. Dispose an abandoned cursor. An exhausted cursor returns false.
Disposed and faulted cursors reject further advances.

An optional advance token applies only during that synchronous move. It
works alongside the creation token and affects discovery and publication.
Cancellation faults the cursor and reports the canceled caller token, with
the creation token taking precedence if both are canceled. Canceling a move
token after return has no effect on later moves. Earlier snapshots survive
advances, failure, cursor disposal and program close. Keep borrowed text
unchanged and its memory manager alive while reading snapshots.

### Retained results

`MoveNextRetained` requires a cursor with a caller-owned working allowance.
An unfunded cursor rejects it before discovery and can still advance with
`MoveNext`. `SubnRetained` requires a caller-owned program allowance.
Each operation transfers the admitted charge without an output copy or a
release and reacquisition gap. The modeled charge includes the receipt and
its result storage, excluding borrowed subject bytes. Receipt acquisition
counts towards cumulative result traffic.

Receipts expose the captured `Match` or substitution `Result`. Finish payload
property access before disposing a receipt. Disposal clears its payload
references, closes payload access and refunds once, including through aliases
and concurrent disposal. `ChargedBytes` remains readable after disposal.
Previously obtained copies remain usable under their original input contract.
Callers take over admission if they keep them after refund. Retained receipts
require explicit disposal to refund their charge.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None);
Assert.Throws<OperationCanceledException>(() => regex.Count("cat horse"u8, 0, canceled.Token));
```

See the [profile README](../src/Lokad.Utf8Regex.PythonRe/README.md) for examples and
the [benchmark scope](../src/Lokad.Utf8Regex.PythonRe/README.md#benchmarks)
for comparator details.
