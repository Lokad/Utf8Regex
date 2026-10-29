# PythonRe contract

`Lokad.Utf8Regex.PythonRe` is the optional managed Python `re` profile.
It targets CPython **3.13.2 str-pattern semantics**, with owned Unicode **15.1.0**
case, identifier and predefined-class data. It matches scalars over strict UTF-8;
byte coordinates are primary and UTF-16 coordinates are derived.

See [release notes](../CHANGELOG.md), [migration](MIGRATION.md) and
[shared resource contract](SPEC.md#cancellation-deadlines-and-resource-limits). Exact declarations are pinned in the
[API snapshot](../tests/Lokad.Utf8Regex.PythonRe.Tests/PublicApi.Shipped.txt).
This is a selected profile, not complete Python equivalence.

## Operations and result shape

| Family | Contract |
| --- | --- |
| Search / Match / FullMatch | Search from the byte floor / match at it / match the entire remaining suffix. Value, detailed, data and string forms agree. |
| IsMatch / Count | Boolean/count using the profile's discovery and empty-match rules |
| FindAll | Structural full-match records, not Python findall shaping |
| FindAllToStrings / FindAllToUtf8 | Whole values with no captures, group values with one, tuple rows with several |
| FindIterDetailed | Materialized owned snapshots |
| CreateMatchCursor | Incremental detailed snapshots over ReadOnlyMemory<byte>; explicit disposal on early stop |
| Replace / Subn | Python template or detailed text/UTF-8 callback output; Subn includes replacement count |
| SplitToStrings / SplitDetailed | Python capture-aware split shape; detailed results distinguish captures and segments |

`startOffsetInBytes` follows Python `pos` semantics: discovery starts at or
after the floor, while anchors, boundaries and lookarounds retain complete prefix
and suffix context. FullMatch covers [pos,end). There is no `endpos` parameter.
The whole subject is validated even when discovery starts later or returns early.

Captures are numbered by opening parenthesis, including named groups.
References and conditionals use those numbers. Detailed `LastIndex` is the last
capture to close on the successful path, not the largest participating number:
`((a))` closes group 1 last. `LastGroup` is that slot's name, or null when
unnamed. Misses/no participating captures have null completion metadata.
Global discovery retries a nonempty match at the same position after an empty
result before advancing one complete scalar.

Default match/group data expose empty text. Default and unsuccessful detailed
results expose empty capture/name arrays and group-zero text. Successful snapshots
retain supplied arrays and their owned values across later calls/disposal.

## Grammar and parsing

Supported families include literals/classes/standard escapes, ordered alternatives,
greedy/lazy/possessive repetition, captures/named groups, references,
participation conditionals, lookahead, fixed-width lookbehind, atomic groups and
leading/scoped flags. Python replacement references include `\1` and
`\g<name>`. Pattern error positions use UTF-16 source-string coordinates.

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

Predefined classes/boundaries use owned CPython 3.13.2/Unicode 15.1 predicates,
including complements and mixed brackets. Host Unicode categories cannot replace
them without equivalence. Shared bounded profile tables are not recharged as
per-invocation copies. Verify derived data with CPython 3.13.2:

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

`Options` retains the flags supplied by the caller. `EffectiveOptions` reports
validated global flags, including leading inline directives across allowed
comment/verbose trivia. It includes implicit Unicode unless global ASCII is
selected; scoped flags do not leak into this metadata. Hosts need not reparse
pattern headers. These enum values are PythonRe's flags, not CPython integer bits.

Constructors validate policy, flags and timeout before strict byte-pattern
decoding. Explicit Compile observes pre-cancellation first and accepts separate
construction policy/timeout/token. One owner covers decoding, parsing, semantic
validation, lowering, metadata and eager FullMatch preparation. It does not
consult static caches or retain construction authority.

Syntax/reference/lookbehind validation precedes optional native rendering.
The owned Python scalar program is authoritative. Optional Core preparation is
retained only for eligible shared native operations/search hints; backend presence
does not prove that every operation uses it. Fixed finite numeric policies omit
unmetered preparation those instances cannot execute.

All four invocation limits, cancellation, failures and major-capacity exclusions
follow the [shared resource contract](SPEC.md#cancellation-deadlines-and-resource-limits). One invocation covers
full validation, discovery, required coordinate/capture shaping, output and
publication. Work totals saturate; first denial is sticky. Terminal failed
discovery checks interruption before reporting EOF. Timeout uses
`RegexMatchTimeoutException`; owned diagnostics identify the original Python
pattern, while nested Core exceptions propagate. Finite working/result byte caps
omit diagnostic subject text.

Constructors defer general FullMatch preparation; Compile completes it eagerly.
Cold FullMatch copies the existing instruction program with an end constraint,
sharing immutable tables/names. Its token/deadline is the current invocation's;
failure is retryable and leaves no failed publication. Exact capture-free literal
value/string paths can use the complete literal finder without this preparation.
The previously rejected program-sharing prototype is not delivered.

Cold FullMatch/replacement preparation retains separate construction limits;
finite working bytes cap additional covered construction capacity. A byte failure
can report ConstructionBytes before execution. Template lookup also charges one
invocation work step per 2,048 UTF-16 template units, rounded up, including warm
cache access. Warm text-key hits need no cold scope. Failed preparation restores
parent loans without refunding spent work; one caller's interruption cannot become
another waiter's authority. These scopes do not combine invocation, preparation
and cache retention into a total memory bound.

Callbacks must be non-null even on misses; pre-cancellation wins.
Checks surround caller execution, which cannot be preempted. Required return
copying/encoding remains admitted; arbitrary callback allocations and later
standalone getters do not.

## Cursor and result lifetime

`CreateMatchCursor(ReadOnlyMemory<byte>)` retains one invocation across moves,
including host waits within its absolute deadline. Keep subject bytes unchanged
and the memory manager alive. Reference aliases share position and disposal;
concurrent/reentrant access rejects. `Current` is valid after a successful move.
EOF, failure or Dispose synchronously release exclusive scratch/subject references;
dispose when stopping early. There is no finalizer.

`MoveNext(CancellationToken)` adds interruption authority for that synchronous
advance, including discovery and required projection. It retains the original
creation token/deadline and cumulative budgets. The move token is detached before
return, so canceling it afterward cannot affect later moves. Cancellation faults
the cursor and releases scratch; earlier owned snapshots remain valid. When both
tokens are canceled, the original token takes precedence in the exception.
Two distinct cancellable tokens require a temporary linked source, disposed before
return; a sole move token or the same token needs no linked source. The ordinary
parameterless path retains its original controls. Already-ended cursors return
false; disposed, faulted and concurrent/reentrant access retain their failures.

Independent owned snapshots remain valid after moves, denial and disposal.
They do not refund cumulative result traffic or bound external host retention.
Existing borrowed span/value contracts remain unchanged. A new call gets an
independent allowance and needs independent host admission.

`MoveNext(out Utf8PythonRangeMatch)` (also with a per-advance token) publishes byte
ranges instead of text. It uses the same discovery position, captures, last-closed
metadata, interruption and cumulative budgets. Each successful range advance
clears the text-based `Current`; accessing it then throws. Owned advances may
resume on that same cursor and populate `Current` normally. Existing snapshots
remain valid across either kind of advance.

Range snapshots own immutable capture/name arrays and borrow the unchanged
`ReadOnlyMemory<byte>` subject. Their metadata survives cursor moves/disposal;
keep its memory manager alive when accessing `GetValueMemory`/`GetGroupMemory`.
Unmatched captures have `Success=false` and zero offsets, distinct from successful
empty captures. Byte offsets are relative to the supplied subject memory. No
captured text, UTF-16 conversion or whole-subject coordinate map is materialized;
these types expose byte coordinates only. Scalar-coordinate support remains open.
Result arrays remain admitted before acquisition under the existing result-traffic
and working scopes; this does not introduce caller-owned shared memory funding.

`CreateMatchCursor(input, startOffsetInBytes, token, workingMemoryAllowance)`
accepts an `IUtf8MemoryAllowance` shared directly across concurrent cursors and
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
UTF-8 bytes. Explicit tokens cover validation, discovery and result publication.

```csharp
using var canceled = new CancellationTokenSource();
canceled.Cancel();
var regex = new Utf8PythonRegex("cat|horse", PythonReCompileOptions.None);
Assert.Throws<OperationCanceledException>(() => regex.Count("cat horse"u8, 0, canceled.Token));
```

Use the [package README](../src/Lokad.Utf8Regex.PythonRe/README.md) for call examples
and [benchmarks](../src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md) for measurement scope.
Safe finite defaults, complete preparation/retention containment and full grammar
equivalence are not implied.
